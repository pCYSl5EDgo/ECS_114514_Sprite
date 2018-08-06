using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Rendering;

using UnityEngine;

using System;
using System.Linq;

namespace Wahren
{
    [RequireComponent(typeof(Camera))]
    public class Manager : MonoBehaviour
    {

        // Unlitシェーダー、シェーダー内部でビルボードの挙動を取らせている。
        // 移動方向(Heading.x)に応じて左右を反転させる。
        // Meshが一部描画されないのはなぜなのか……
        [SerializeField] Shader Unlit_Position;

        // Unlitシェーダー、シェーダー内部でビルボードの挙動を取らせている。
        // 移動方向(Heading.x)に応じて左右を反転させる。
        // HeadingとPositionをコピペしてGPUに渡すためComputeBufferを2本使用している。
        // Meshが一部描画されないのはなぜなのか……
        [SerializeField] Shader Unlit_Position_DoubleBuffer;

        // ごく普通のUnlitシェーダー。
        // ビルボードな挙動はしない。
        [SerializeField] Shader Unlit_MeshInstanceRenderer;

        // スプライトの種類数に制限は設けない。
        [SerializeField] Sprite[] UnitSprites;
        [SerializeField] [Range(0, 2)] int currentMode;

        Camera mainCamera;

        readonly Heading[] Angles = new Heading[360];
        // 描画方法を三通り試すため、Worldを３つ用意する。アクセスの利便性のため配列にする。
        // EntityManagerは各World固有の存在。
        // EntityManagerは内部にArchetypeManagerを一つ持つ。
        // EntityManager.CreateArchetypeでEntityArchetypeを作るならそれぞれのWorldで作る必要がある。
        // よってWorldとEntityArchetypeのタプルで管理する方がよいだろう。
        readonly (World world, EntityArchetype archetype)[] WorldArchetypeTupleArray = new(World, EntityArchetype)[3];

        // SpriteRendererSharedComponentとMeshInstanceRendererが両方共にクラスならボクシングとかを気にする必要がなく、Array[]とかの形にして変数の個数を減らせたのだが。
        SpriteRendererSharedComponent[] renderer1;
        SpriteRendererSharedComponent[] renderer2;
        MeshInstanceRenderer[] renderer3;

        void Awake()
        {
            var collection = World.AllWorlds;
            for (int i = 0; i < WorldArchetypeTupleArray.Length; i++)
                WorldArchetypeTupleArray[i].world = collection[i];
            WorldArchetypeTupleArray[0].archetype = WorldArchetypeTupleArray[0].world.GetExistingManager<EntityManager>().CreateArchetype(typeof(Position), typeof(Heading), typeof(MoveSpeed), typeof(MoveForward), typeof(SpriteRendererSharedComponent));
            WorldArchetypeTupleArray[1].archetype = WorldArchetypeTupleArray[1].world.GetExistingManager<EntityManager>().CreateArchetype(typeof(Position), typeof(Heading), typeof(MoveSpeed), typeof(MoveForward), typeof(SpriteRendererSharedComponent));
            WorldArchetypeTupleArray[2].archetype = WorldArchetypeTupleArray[2].world.GetExistingManager<EntityManager>().CreateArchetype(typeof(Position), typeof(Heading), typeof(MoveSpeed), typeof(MoveForward), typeof(TransformMatrix), typeof(MeshInstanceRenderer));
            WorldArchetypeTupleArray[1].world.GetExistingManager<SpriteRenderSystem_DoubleBuffer>().Camera = WorldArchetypeTupleArray[0].world.GetExistingManager<SpriteRenderSystem>().Camera = mainCamera = GetComponent<Camera>();
            InitializeRenderer();
            InitializeAngle();
            const int count = 114514;
            // 114514体のEntityを生成する。
            SpawnEntities(count, ref WorldArchetypeTupleArray[0], renderer1);
            SpawnEntities(count, ref WorldArchetypeTupleArray[1], renderer2);
            SpawnEntities(count, ref WorldArchetypeTupleArray[2], renderer3);
            ChooseWorldToRun(currentMode);
        }

        private void ChooseWorldToRun(int mode)
        {
            currentMode = mode;
            // World.Activeに指定していないWorldは勝手に動作する。Enabled = falseにしてやらないと普通に動作してレンダリングとかする。
            for (int i = 0; i < WorldArchetypeTupleArray.Length; i++)
                if (i != currentMode)
                    StopWorld(WorldArchetypeTupleArray[i].world);
            RunWorld(World.Active = WorldArchetypeTupleArray[currentMode].world);
        }

        void InitializeRenderer()
        {
            renderer1 = new SpriteRendererSharedComponent[UnitSprites.Length];
            for (int i = 0; i < UnitSprites.Length; i++)
                renderer1[i] = new SpriteRendererSharedComponent(Unlit_Position, UnitSprites[i]);
            renderer2 = new SpriteRendererSharedComponent[UnitSprites.Length];
            for (int i = 0; i < UnitSprites.Length; i++)
                renderer2[i] = new SpriteRendererSharedComponent(Unlit_Position_DoubleBuffer, UnitSprites[i]);
            renderer3 = new MeshInstanceRenderer[UnitSprites.Length];
            for (int i = 0; i < UnitSprites.Length; i++)
                renderer3[i] = CreateMeshInstanceRenderer(UnitSprites[i]);
        }

        void Update()
        {
            var deltaMove = Time.deltaTime * 10;
            var transform = this.transform;
            if (Input.GetKey(KeyCode.W))
                transform.position += deltaMove * Vector3.forward;
            if (Input.GetKey(KeyCode.A))
                transform.position += deltaMove * Vector3.left;
            if (Input.GetKey(KeyCode.S))
                transform.position += deltaMove * Vector3.back;
            if (Input.GetKey(KeyCode.D))
                transform.position += deltaMove * Vector3.right;
            if (Input.GetKey(KeyCode.Q))
                transform.position += deltaMove * Vector3.up;
            if (Input.GetKey(KeyCode.E))
                transform.position += deltaMove * Vector3.down;
            if (Input.GetMouseButtonDown(0) && Physics.Raycast(mainCamera.ScreenPointToRay(Input.mousePosition), out var hitInfo))
                transform.LookAt(hitInfo.point);
            if (Input.GetKeyDown(KeyCode.Space))
            {
                var MoveForwardSystem = World.Active.GetExistingManager<MoveForwardSystem>();
                MoveForwardSystem.Enabled = !MoveForwardSystem.Enabled;
            }
            if (Input.GetKey(KeyCode.Escape))
                Application.Quit();
            // 描画方法の切り替え。
            if (Input.GetKeyDown(KeyCode.Alpha0) && currentMode != 0)
                ChooseWorldToRun(0);
            else if (Input.GetKeyDown(KeyCode.Alpha1) && currentMode != 1)
                ChooseWorldToRun(1);
            else if (Input.GetKeyDown(KeyCode.Alpha2) && currentMode != 2)
                ChooseWorldToRun(2);
        }

        void SpawnEntities<T>(int count, ref (World world, EntityArchetype archetype) pair, T[] array) where T : struct, ISharedComponentData
        {
            var manager = pair.world.GetExistingManager<EntityManager>();
            var restLength = (count - UnitSprites.Length) / UnitSprites.Length;
            var srcEntities = new NativeArray<Entity>(UnitSprites.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            var firstEntities = new NativeArray<Entity>(count - (restLength + 1) * UnitSprites.Length + restLength, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            var restEntities = new NativeArray<Entity>(restLength, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            try
            {
                InitializeSourceEntities(manager, ref pair.archetype, ref srcEntities, array);
                InitializeEntities(manager, ref srcEntities, array, ref firstEntities, ref restEntities);
            }
            finally
            {
                srcEntities.Dispose();
                firstEntities.Dispose();
                restEntities.Dispose();
            }
        }


        // 1°ずつ変化するベクトルを設定する。

        void InitializeAngle()
        {
            for (int i = 0; i < Angles.Length; i++)
            {
                var angle = math.radians(i);
                Angles[i] = new Heading { Value = new float3(math.cos(angle), 0, math.sin(angle)) };
            }
        }

        // NativeArray<T>の構造体のサイズは8byte(void*)+4byte(int)+4byte(Allocator)=16byteであると思われる。
        // この程度ならref使わなくてもいいか？　呼び出し回数も少ないことであるし。
        void InitializeSourceEntities<T>(EntityManager manager, ref EntityArchetype archetype, ref NativeArray<Entity> src, T[] shared) where T : struct, ISharedComponentData
        {
            manager.CreateEntity(archetype, src);
            for (int i = 0; i < src.Length; i++)
            {
                var entity = src[i];
                // SetSharedComponentはChunk移動（創造）を起こすので最小限度にする。
                manager.SetSharedComponentData(entity, shared[i]);
                manager.SetComponentData(entity, new MoveSpeed { speed = UnityEngine.Random.Range(0.1f, 2f) });
                manager.SetComponentData(entity, Angles[(int)UnityEngine.Random.Range(0f, 360f)]);
            }
        }

        void InitializeEntities<T>(EntityManager manager, ref NativeArray<Entity> srcEntities, T[] array, ref NativeArray<Entity> firstEntities, ref NativeArray<Entity> restEntities) where T : struct, ISharedComponentData
        {
            if (array == null) throw new ArgumentNullException();
            // Instantiateした場合はISharedComponentDataが最初から適切に設定されているためChunkについて頭を悩ませなくて良い。
            manager.Instantiate(srcEntities[0], firstEntities);
            for (int i = 0; i < firstEntities.Length; i++)
            {
                var entity = firstEntities[i];
                manager.SetComponentData(entity, new MoveSpeed { speed = UnityEngine.Random.Range(0.1f, 2f) });
                manager.SetComponentData(entity, Angles[i % 360]);
            }
            for (int i = 1; i < srcEntities.Length; i++)
            {
                manager.Instantiate(srcEntities[i], restEntities);
                for (int j = 0; j < restEntities.Length; j++)
                {
                    var entity = restEntities[j];
                    manager.SetComponentData(entity, new MoveSpeed { speed = UnityEngine.Random.Range(0.1f, 2f) });
                    manager.SetComponentData(entity, Angles[j % 360]);
                }
            }
        }

        void StopWorld(World world)
        {
            foreach (var item in world.BehaviourManagers)
            {
                var system = item as ComponentSystemBase;
                if (system == null) continue;
                system.Enabled = false;
            }
        }
        void RunWorld(World world)
        {
            foreach (var item in world.BehaviourManagers)
            {
                var system = item as ComponentSystemBase;
                if (system == null) continue;
                system.Enabled = true;
            }
        }


        // スプライトからMeshとMaterialを作成する。
        MeshInstanceRenderer CreateMeshInstanceRenderer(Sprite sprite)
        {
            var mesh = new Mesh();
            // LINQ使うのは遅いが、少量であるから手を抜いている。
            var vertices = Array.ConvertAll(sprite.vertices, _ => (Vector3)_).ToList();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, sprite.uv.ToList());
            mesh.SetTriangles(Array.ConvertAll(sprite.triangles, _ => (int)_), 0);

            return new MeshInstanceRenderer()
            {
                mesh = mesh,
                material = new Material(Unlit_MeshInstanceRenderer)
                {
                    enableInstancing = true,
                    mainTexture = sprite.texture
                }
            };
        }
    }
}