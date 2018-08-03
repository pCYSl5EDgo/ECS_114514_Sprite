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

namespace Wahren
{
    [RequireComponent(typeof(Camera))]
    public class Manager : MonoBehaviour
    {
        [SerializeField] Shader Unlit_Position;
        [SerializeField] Shader Unlit_Position_DoubleBuffer;
        [SerializeField] Shader Unlit_MeshInstanceRenderer;
        [SerializeField] Sprite[] UnitSprites;
        Camera mainCamera;
        EntityManager manager;
        MoveForwardSystem MoveForwardSystem;
        TransformSystem TransformSystem;
        SpriteRenderSystem render1;
        SpriteRenderSystem_DoubleBuffer render2;
        MeshInstanceRendererSystem render3;

        bool isNowStopMoveSystem = false;

        void Awake()
        {
            var active = World.Active;
            manager = active.GetOrCreateManager<EntityManager>();
            render1 = active.GetOrCreateManager<SpriteRenderSystem>();
            mainCamera = render1.Camera = GetComponent<Camera>();
            var BoundReflectSystem = active.GetOrCreateManager<BoundReflectSystem>();
            BoundReflectSystem.Region = new float4(-5, -5, 5, 5);
            MoveForwardSystem = active.GetOrCreateManager<MoveForwardSystem>();
            TransformSystem = active.GetOrCreateManager<TransformSystem>();
            TransformSystem.Enabled = false;
            // ２番目のレンダリングシステムを停止。
            render2 = active.GetOrCreateManager<SpriteRenderSystem_DoubleBuffer>();
            render2.Camera = mainCamera;
            render2.Enabled = false;
            // ３番目のレンダリングシステムを停止。
            render3 = active.GetOrCreateManager<MeshInstanceRendererSystem>();
            render3.Enabled = false;
            // 114514体のユニットを作成。
            Spawn114514Entities();
        }

        void Update()
        {
            var deltaTime = Time.deltaTime * 10;
            var transform = this.transform;
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
            if (Input.GetMouseButtonDown(0) && UnityEngine.Physics.Raycast(mainCamera.ScreenPointToRay(Input.mousePosition), out var hitInfo))
            {

            }
            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (isNowStopMoveSystem)
                    StartMovingSystem();
                else
                    StopMovingSystem();
                isNowStopMoveSystem = !isNowStopMoveSystem;
            }
            if (Input.GetKey(KeyCode.Escape))
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_STANDALONE
                Application.Quit();
#endif
            }
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                render2.Enabled = false;
                render3.Enabled = false;
                render1.Enabled = true;
                TransformSystem.Enabled = false;
            }
            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                render1.Enabled = false;
                render3.Enabled = false;
                render2.Enabled = true;
                TransformSystem.Enabled = false;
            }
            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                render1.Enabled = false;
                render2.Enabled = false;
                render3.Enabled = true;
                TransformSystem.Enabled = true;
            }
        }

        void StopMovingSystem()
        {
            MoveForwardSystem.Enabled = false;
        }

        void StartMovingSystem()
        {
            MoveForwardSystem.Enabled = true;
        }

        void Spawn114514Entities()
        {
            var spriteRenderers = new SpriteRendererSharedComponent[UnitSprites.Length];
            // このコードは描画の性能比較のために記述しているので本来は取り除くのが望ましい。
            var meshInstanceRenderers = new MeshInstanceRenderer[UnitSprites.Length];
            for (int i = 0; i < UnitSprites.Length; i++)
            {
                spriteRenderers[i] = new SpriteRendererSharedComponent(Unlit_Position, UnitSprites[i]);
                meshInstanceRenderers[i] = CreateMeshInstanceRenderer(UnitSprites[i]);
            }
            // CompontentTypeはTypeからimplicitに型変換される。
            var archetype = manager.CreateArchetype(typeof(Position), typeof(SpriteRendererSharedComponent), typeof(MeshInstanceRenderer), typeof(TransformMatrix), typeof(Heading), typeof(MoveForward), typeof(MoveSpeed));

            // 360°全方向に中心から移動させる。 using文使いたいが、書き込み不可なので使えない。
            var angles = new NativeArray<Heading>(360, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            for (int i = 0; i < angles.Length; i++)
            {
                var angle = math.radians(i);
                angles[i] = new Heading { Value = new float3(math.cos(angle), 0, math.sin(angle)) };
            }
            using (var entities = new NativeArray<Entity>(114514, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
            {
                manager.CreateEntity(archetype, entities);
                // 非Blittable型をJob Systemでは扱えないため同期的に処理し無くてはならない。
                for (int i = 0, j = 0; i < entities.Length; i++, j++)
                {
                    if (j == UnitSprites.Length)
                        j = 0;
                    // ここで無駄にメモリアロケーションが発生するのが悔しい。
                    manager.SetSharedComponentData(entities[i], spriteRenderers[j]);
                    manager.SetSharedComponentData(entities[i], meshInstanceRenderers[j]);
                }
                new Job
                {
                    angles = angles,
                    buf = World.Active.GetOrCreateManager<EndFrameBarrier>().CreateCommandBuffer(),
                    entities = entities,
                    seed = 1214219319u,
                }.Schedule(entities.Length, 1024).Complete();
            }
        }

        [BurstCompile]
        struct Job : IJobParallelFor
        {
            [ReadOnly] [DeallocateOnJobCompletion] public NativeArray<Heading> angles;
            [ReadOnly] public NativeArray<Entity> entities;
            public EntityCommandBuffer.Concurrent buf;
            public uint seed;
            public void Execute(int index)
            {
                var e = entities[index];
                buf.SetComponent(e, angles[index % 360]);
                // 乱数生成
                var i = (uint)index;
                i = i ^ (i << 13);
                i = i ^ (i >> 17);
                i = i ^ (i << 15);
                // 移動速度は0.1~2.0の範囲に制限。
                buf.SetComponent(e, new MoveSpeed { speed = 0.1f + 1.9f * (float)i / (float)(uint.MaxValue) });
            }
        }

        MeshInstanceRenderer CreateMeshInstanceRenderer(Sprite sprite)
        {
            var mesh = new Mesh();
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