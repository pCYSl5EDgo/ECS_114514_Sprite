using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using Unity.Collections;

using UnityEngine;
using UnityEngine.Rendering;

using System.Collections.Generic;

namespace Wahren
{
    [UpdateAfter(typeof(UnityEngine.Experimental.PlayerLoop.PreLateUpdate.ParticleSystemBeginUpdateAll))]
    sealed class SpriteRenderSystem_DoubleBuffer : ComponentSystem
    {
        // このSystemで描画するEntityだけを識別するためのISharedComponentData
        public struct Tag : ISharedComponentData { }
        // IL2CPPでビルドする際には定数化したほうが圧倒的にいいらしい。
        // Marshal.SizeOf<Heading>の値を使う。
        const int Stride = sizeof(float) * 3;
        public Bounds Bounds = new Bounds(Vector3.zero, Vector3.one * 300);
        // 全カメラに対して描画するならnull
        // 特定１つに対して描画するなら非null
        public Camera Camera;
        uint[] args = new uint[5];
        ComputeBuffer argsBuffer;

        // ECS標準の[Inject]で注入やらなんやらは実際ComponentGroupをやりくりすることで行っているそうだ。
        ComponentGroup g;
        int ShaderProperty_PositionBuffer = Shader.PropertyToID("_PositionBuffer");
        int ShaderProperty_HeadingBuffer = Shader.PropertyToID("_HeadingBuffer");

        readonly List<SpriteRendererSharedComponent> sharedComponents = new List<SpriteRendererSharedComponent>();
        readonly List<int> sharedIndices = new List<int>();
        readonly Dictionary<int, (ComputeBuffer, ComputeBuffer)> buffers = new Dictionary<int, (ComputeBuffer, ComputeBuffer)>();

        protected override void OnCreateManager(int capacity)
        {
            argsBuffer = new ComputeBuffer(args.Length, sizeof(uint), ComputeBufferType.IndirectArguments);
            // ComponentType.Subtractive<T>()はTをArchetypeに持たないということを意味する。
            g = GetComponentGroup(ComponentType.Subtractive<MeshCulledComponent>(), ComponentType.ReadOnly<Position>(), ComponentType.ReadOnly<Heading>(), ComponentType.ReadOnly<SpriteRendererSharedComponent>(), ComponentType.ReadOnly<Tag>());
        }
        // MeshInstanceRendererSystemを真似て書いている。
        // あちらではMatrix4x4[]を用意して1023個ずつEntityをGraphics.DrawMeshInstancedしている。
        // その際にTransformMatrixをむりくりMatrix4x4に読み替えたりコピーしたりしているようなので多分遅い。
        // あと、preview8のコメント文でDarwMeshInstancedの引数にNativeArray/Sliceが使えないことを嘆いている。
        protected override void OnUpdate()
        {
            sharedComponents.Clear();
            sharedIndices.Clear();
            // sharedIndicesはsharedComponentが同一である限り変化しないようだ。
            // Set(Add)SharedComponentした順にindexが増える。
            EntityManager.GetAllUniqueSharedComponentDatas(sharedComponents, sharedIndices);
            // 公式によるとfilterかけるのはかなり低コスト。
            // NativeList<T>に対応せず、List<T>にしか対応していないのナンデ？
            using (var filter = g.CreateForEachFilter(sharedComponents))
            {
                for (int i = 0; i < sharedComponents.Count; i++)
                {
                    // meshとmaterialいずれかがnullの場合は描画できないので飛ばす。
                    var sprite = sharedComponents[i];
                    // DestroyしないならSystem.Object.ReferenceEquals使ってもいいじゃない。
                    if (object.ReferenceEquals(sprite.material, null) || object.ReferenceEquals(sprite.mesh, null))
                        continue;
                    // ComponentDataArrayの実態はポインタ(+もろもろ)だそうだ。
                    var positionDataArray = g.GetComponentDataArray<Position>(filter, i);
                    var headingDataArray = g.GetComponentDataArray<Heading>(filter, i);
                    // 本当ならC#7.2で導入されたin引数でin filterにすることでGetComponentDataArray<Heading>のタイミングを遅らせたい。
                    Render(i, ref sprite, ref positionDataArray, ref headingDataArray);
                }
            }
        }

        void Render(int i, ref SpriteRendererSharedComponent sprite, ref ComponentDataArray<Position> positionDataArray, ref ComponentDataArray<Heading> headingDataArray)
        {
            var length = positionDataArray.Length;
            if (length == 0)
                return;
            var index = sharedIndices[i];
            // Unity公式のGraphics.DrawMeshInstancedIndirectのサンプルでは
            // 描画する数が前フレームの数と一致しない場合には必ず毎フレームComputeBufferをReleaseして更にnewしていた。
            if (!buffers.TryGetValue(index, out var buffer))
            {
                buffers[index] = buffer = (new ComputeBuffer(length, Stride), new ComputeBuffer(length, Stride));
                sprite.material.SetBuffer(ShaderProperty_PositionBuffer, buffer.Item1);
                sprite.material.SetBuffer(ShaderProperty_HeadingBuffer, buffer.Item2);
            }
            // 短ければnewするのは当然だが必要量より長い分にはnewしないほうがいいのではなかろうか。
            if (buffer.Item1.count < length)
            {
                buffer.Item1.Release();
                buffer.Item2.Release();
                buffers[index] = buffer = (new ComputeBuffer(length, Stride), new ComputeBuffer(length, Stride));
                // MaterialPropertyBlockではなくMaterialに直に設定する。
                sprite.material.SetBuffer(ShaderProperty_PositionBuffer, buffer.Item1);
                sprite.material.SetBuffer(ShaderProperty_HeadingBuffer, buffer.Item2);
            }
            args[0] = sprite.mesh.GetIndexCount(0);
            // 何体描画するか
            args[1] = (uint)length;
            args[2] = sprite.mesh.GetIndexStart(0); // このプログラムの場合は0固定だが書いておく
            args[3] = sprite.mesh.GetBaseVertex(0); // このプログラムの場合は0固定だが書いておく
            argsBuffer.SetData(args, 0, 0, 4);
            // 時代はSpan<T>、ただしUnityの場合はNativeSlice<T>
            // Allocator.Persistentにしてクラスフィールドにしたほうがいいのかどうなのかいまいちわからない。
            using (var position = new NativeArray<Position>(length, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
            {
                positionDataArray.CopyTo(position);
                // なぜNativeSliceを扱えないのか（困惑）
                // lengthを指定することで必要量だけ書き換えよう。
                // GPUへの転送量がこれで抑えられるのかどうかは不明。
                // 抑えられないようならば描画数が変わる度にComputeBufferをnewしなくてはならないだろう。
                buffer.Item1.SetData(position, 0, 0, length);
            }
            using (var heading = new NativeArray<Heading>(length, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
            {
                // heading.Slice().SliceConvert<Position>();
                // なりと書けば、PositionとHeadingのstruct layoutが同一であるため簡単に流用できる。
                // メモリアロケーションが本当に気になるならばやってみるのもいいかもしれない。
                headingDataArray.CopyTo(heading);
                buffer.Item2.SetData(heading, 0, 0, length);
            }
            // 影を描画しないしさせない鉄の意志。
            Graphics.DrawMeshInstancedIndirect(sprite.mesh, 0, sprite.material, Bounds, argsBuffer, 0, null, ShadowCastingMode.Off, false, 0, Camera, LightProbeUsage.Off, null);
        }

        // 描画しないなら解放する。
        protected override void OnStopRunning()
        {
            using (var e = buffers.Values.GetEnumerator())
                while (e.MoveNext())
                {
                    var buffer = e.Current;
                    buffer.Item1.Release();
                    buffer.Item2.Release();
                }
            buffers.Clear();
        }
        protected override void OnDestroyManager()
        {
            if (!object.ReferenceEquals(argsBuffer, null))
                argsBuffer.Release();
            using (var e = buffers.Values.GetEnumerator())
                while (e.MoveNext())
                {
                    var buffer = e.Current;
                    buffer.Item1.Release();
                    buffer.Item2.Release();
                }
            buffers.Clear();
        }
    }
}