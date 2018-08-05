using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Jobs;
using Unity.Burst;

using UnityEngine;

using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;

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


        [SerializeField] Sprite[] UnitSprites;
        [SerializeField] [Range(0, 2)] int currentMode;

        Camera mainCamera;

        readonly Heading[] Angles = new Heading[360];

        MoveForwardSystem MoveForwardSystem;
        TransformSystem TransformSystem;
        SpriteRendererSharedComponent[] renderer1;
        SpriteRendererSharedComponent[] renderer2;
        MeshInstanceRenderer[] renderer3;
        ComponentType[][] ComponentTypeArray = new ComponentType[3][];
        ComponentSystem[] RenderingSystemArray = new ComponentSystem[3];

        bool isNowStopMoveSystem = false;

        void Awake()
        {
            mainCamera = GetComponent<Camera>();
            var active = World.Active;
            TransformSystem = active.GetExistingManager<TransformSystem>();
            MoveForwardSystem = active.GetExistingManager<MoveForwardSystem>();
            ComponentTypeArray[0] = new ComponentType[] { typeof(Position), typeof(Heading), typeof(MoveSpeed), typeof(MoveForward), typeof(SpriteRenderSystem.Tag) };
            ComponentTypeArray[1] = new ComponentType[] { typeof(Position), typeof(Heading), typeof(MoveSpeed), typeof(MoveForward), typeof(SpriteRenderSystem_DoubleBuffer.Tag) };
            ComponentTypeArray[2] = new ComponentType[] { typeof(Position), typeof(Heading), typeof(MoveSpeed), typeof(MoveForward), typeof(TransformMatrix) };
            RenderingSystemArray[0] = active.GetExistingManager<SpriteRenderSystem>();
            RenderingSystemArray[1] = active.GetExistingManager<SpriteRenderSystem_DoubleBuffer>();
            RenderingSystemArray[2] = active.GetExistingManager<MeshInstanceRendererSystem>();
            renderer1 = new SpriteRendererSharedComponent[UnitSprites.Length];
            for (int i = 0; i < UnitSprites.Length; i++)
                renderer1[i] = new SpriteRendererSharedComponent(Unlit_Position, UnitSprites[i]);
            renderer2 = new SpriteRendererSharedComponent[UnitSprites.Length];
            for (int i = 0; i < UnitSprites.Length; i++)
                renderer2[i] = new SpriteRendererSharedComponent(Unlit_Position_DoubleBuffer, UnitSprites[i]);
            renderer3 = new MeshInstanceRenderer[UnitSprites.Length];
            for (int i = 0; i < UnitSprites.Length; i++)
                renderer3[i] = CreateMeshInstanceRenderer(UnitSprites[i]);
            var BoundRefecltSystem = active.GetOrCreateManager<BoundReflectSystem>();
            BoundRefecltSystem.Region = new float4(-50, -50, 50, 50);
            InitializeAngle();
            switch (currentMode)
            {
                case 0:
                    TransformSystem.Enabled = false;
                    Spawn114514Entities(renderer1, ComponentTypeArray[currentMode]);
                    break;
                case 1:
                    TransformSystem.Enabled = false;
                    Spawn114514Entities(renderer2, ComponentTypeArray[currentMode]);
                    break;
                case 2:
                    Spawn114514Entities(renderer3, ComponentTypeArray[currentMode]);
                    break;
            }
        }


        void Update()
        {
            var deltaTime = Time.deltaTime * 10;
            var transform = this.transform;
            var manager = World.Active.GetExistingManager<EntityManager>();
            if (Input.GetKey(KeyCode.W))
                transform.position += deltaTime * Vector3.forward;
            if (Input.GetKey(KeyCode.A))
                transform.position += deltaTime * Vector3.left;
            if (Input.GetKey(KeyCode.S))
                transform.position += deltaTime * Vector3.back;
            if (Input.GetKey(KeyCode.D))
                transform.position += deltaTime * Vector3.right;
            if (Input.GetKey(KeyCode.Q))
                transform.position += deltaTime * Vector3.up;
            if (Input.GetKey(KeyCode.E))
                transform.position += deltaTime * Vector3.down;
            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (isNowStopMoveSystem)
                    MoveForwardSystem.Enabled = true;
                else
                    MoveForwardSystem.Enabled = false;
                isNowStopMoveSystem = !isNowStopMoveSystem;
            }
            if (Input.GetKey(KeyCode.Escape))
                Application.Quit();
            // 描画方法の切り替え。
            if (Input.GetKey(KeyCode.Alpha0) && currentMode != 0)
            {
                currentMode = 0;
                // 不要なシステムはEnabledをfalseにする。
                TransformSystem.Enabled = false;
                manager.DestroyEntity(manager.GetAllEntities());
                RenderingSystemArray[0].Enabled = true;
                RenderingSystemArray[1].Enabled = false;
                RenderingSystemArray[2].Enabled = false;
                Spawn114514Entities(renderer1, ComponentTypeArray[currentMode]);
            }
            else if (Input.GetKey(KeyCode.Alpha1) && currentMode != 1)
            {
                currentMode = 1;
                TransformSystem.Enabled = false;
                manager.DestroyEntity(manager.GetAllEntities());
                RenderingSystemArray[0].Enabled = false;
                RenderingSystemArray[1].Enabled = true;
                RenderingSystemArray[2].Enabled = false;
                Spawn114514Entities(renderer2, ComponentTypeArray[currentMode]);
            }
            else if (Input.GetKey(KeyCode.Alpha2) && currentMode != 2)
            {
                currentMode = 2;
                TransformSystem.Enabled = true;
                manager.DestroyEntity(manager.GetAllEntities());
                RenderingSystemArray[0].Enabled = false;
                RenderingSystemArray[1].Enabled = false;
                RenderingSystemArray[2].Enabled = true;
                Spawn114514Entities(renderer3, ComponentTypeArray[currentMode]);
            }
        }

        // 114514体のEntityを生成する。
        void Spawn114514Entities<T>(T[] array, params ComponentType[] archetypes) where T : struct, ISharedComponentData
        {
            var active = World.Active;
            var manager = active.GetExistingManager<EntityManager>();
            EntityArchetype archetype;
            {
                var tmp = new ComponentType[(archetypes?.Length ?? 0) + 1];
                if (archetypes != null && archetypes.Length != 0)
                    Array.Copy(archetypes, tmp, archetypes.Length);
                tmp[tmp.Length - 1] = typeof(T);
                archetype = manager.CreateArchetype(tmp);
            }
            var restLength = (114514 - UnitSprites.Length) / UnitSprites.Length;
            using (var srcEntities = new NativeArray<Entity>(UnitSprites.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
            {
                var entitiesArray = new NativeArray<Entity>[UnitSprites.Length];
                InitializeSourceEntities(manager, archetype, srcEntities, array);
                InitializeEntitiesArray(entitiesArray);
                InitializeEntities(manager, srcEntities, array, entitiesArray);
                DisposeEntitiesArray(entitiesArray);
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

        void InitializeSourceEntities<T>(EntityManager manager, EntityArchetype archetype, NativeArray<Entity> src, T[] shared) where T : struct, ISharedComponentData
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

        void InitializeEntitiesArray(NativeArray<Entity>[] entitiesArray)
        {
            const int V = 114514;
            var restLength = (V - UnitSprites.Length) / UnitSprites.Length;
            entitiesArray[0] = new NativeArray<Entity>(V - UnitSprites.Length - (restLength * (UnitSprites.Length - 1)), Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            for (int i = 1; i < UnitSprites.Length; i++)
                entitiesArray[i] = new NativeArray<Entity>(restLength, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        }

        void InitializeEntities<T>(EntityManager manager, NativeArray<Entity> srcEntities, T[] array, NativeArray<Entity>[] entitiesArray) where T : struct, ISharedComponentData
        {
            if (array == null) throw new ArgumentNullException();
            for (int i = 0; i < srcEntities.Length; i++)
            {
                var outputEntities = entitiesArray[i];
                // Instantiateした場合はISharedComponentDataが最初から適切に設定されているためChunkについて頭を悩ませなくて良い。
                manager.Instantiate(srcEntities[i], outputEntities);
                for (int j = 0; j < outputEntities.Length; j++)
                {
                    var entity = outputEntities[j];
                    manager.SetComponentData(entity, new MoveSpeed { speed = UnityEngine.Random.Range(0.1f, 2f) });
                    manager.SetComponentData(entity, Angles[j % 360]);
                }
            }
        }

        void DisposeEntitiesArray(NativeArray<Entity>[] entitiesArray)
        {
            for (int i = 0; i < entitiesArray.Length; i++)
                entitiesArray[i].Dispose();
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