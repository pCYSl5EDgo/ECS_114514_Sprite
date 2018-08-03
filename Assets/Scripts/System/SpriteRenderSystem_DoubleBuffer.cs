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
    [UpdateAfter(typeof(MeshInstanceRendererSystem))]
    sealed class SpriteRenderSystem_DoubleBuffer : ComponentSystem
    {
        const int Stride = sizeof(float) * 3;
        public Bounds Bounds = new Bounds(Vector3.zero, Vector3.one * 300);
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
            g = GetComponentGroup(ComponentType.ReadOnly<Position>(), ComponentType.ReadOnly<Heading>(), ComponentType.ReadOnly<SpriteRendererSharedComponent>());
        }
        protected override void OnUpdate()
        {
            sharedComponents.Clear();
            sharedIndices.Clear();
            // MeshInstanceRendererSystemを真似て書いている。
            // あちらではMatrix4x4[]を用意して1023個ずつEntityをGraphics.DrawMeshInstancedしている。
            // その際にTransformMatrixをむりくりMatrix4x4に読み替えたりコピーしたりしているようなので多分遅い。
            // あと、preview8のコメント文でDarwMeshInstancedの引数にNativeArray/Sliceが使えないことを嘆いている。
            EntityManager.GetAllUniqueSharedComponentDatas(sharedComponents, sharedIndices);
            using (var filter = g.CreateForEachFilter(sharedComponents))
            {
                for (int i = 1; i < sharedComponents.Count; i++)
                {
                    var positionDataArray = g.GetComponentDataArray<Position>(filter, i);
                    var headingDataArray = g.GetComponentDataArray<Heading>(filter, i);
                    Render(i, ref positionDataArray, ref headingDataArray);
                }
            }
        }

        void Render(int i, ref ComponentDataArray<Position> positionDataArray, ref ComponentDataArray<Heading> headingDataArray)
        {
            var length = positionDataArray.Length;
            if (length == 0)
                return;
            var sprite = sharedComponents[i];
            var index = sharedIndices[i];
            if (!buffers.TryGetValue(index, out var buffer))
            {
                buffers.Add(index, buffer = (new ComputeBuffer(length, Stride), new ComputeBuffer(length, Stride)));
                sprite.material.SetBuffer(ShaderProperty_PositionBuffer, buffer.Item1);
                sprite.material.SetBuffer(ShaderProperty_HeadingBuffer, buffer.Item2);
            }
            if (buffer.Item1.count < length)
            {
                buffer.Item1.Release();
                buffer.Item2.Release();
                buffers.Add(index, buffer = (new ComputeBuffer(length, Stride), new ComputeBuffer(length, Stride)));
                sprite.material.SetBuffer(ShaderProperty_PositionBuffer, buffer.Item1);
                sprite.material.SetBuffer(ShaderProperty_HeadingBuffer, buffer.Item2);
            }
            //メッシュの頂点数
            args[0] = sprite.mesh.GetIndexCount(0);
            // 何体描画するか
            args[1] = (uint)length;
            argsBuffer.SetData(args, 0, 0, 2);
            // 時代はSpan<T>、ただしUnityの場合はNativeSlice<T>
            // Allocator.Persistentにしてクラスフィールドにしたほうがいいのかどうなのかいまいちわからない。
            using (var position = new NativeArray<Position>(length, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
            {
                positionDataArray.CopyTo(position);
                buffer.Item1.SetData(position, 0, 0, length);
            }
            using (var heading = new NativeArray<Heading>(length, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
            {
                headingDataArray.CopyTo(heading);
                buffer.Item2.SetData(heading, 0, 0, length);
            }
            // 影を描画しないしさせない鉄の意志。
            Graphics.DrawMeshInstancedIndirect(sprite.mesh, 0, sprite.material, Bounds, argsBuffer, 0, null, ShadowCastingMode.Off, false, 0, Camera, LightProbeUsage.Off, null);
        }

        protected override void OnStopRunning()
        {
            foreach (var buffer in buffers.Values)
            {
                buffer.Item1.Release();
                buffer.Item2.Release();
            }
            buffers.Clear();
        }
        protected override void OnDestroyManager()
        {
            if (!object.ReferenceEquals(argsBuffer, null))
                argsBuffer.Release();
            foreach (var buffer in buffers.Values)
            {
                buffer.Item1.Release();
                buffer.Item2.Release();
            }
            buffers.Clear();
        }
    }
}