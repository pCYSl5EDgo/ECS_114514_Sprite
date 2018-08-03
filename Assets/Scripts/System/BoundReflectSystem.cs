using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;

namespace Wahren
{
    [UpdateBefore(typeof(MoveForwardSystem))]
    sealed class BoundReflectSystem : JobComponentSystem
    {
        public float4 Region;
        [BurstCompile]
        struct Job : IJobProcessComponentData<Position, MoveSpeed, Heading>
        {
            public float4 Region;
            public void Execute(ref Position data0, [ReadOnly]ref MoveSpeed data1, ref Heading data2)
            {
                if (data0.Value.x < Region.x)
                {
                    data0.Value.x = Region.x;
                    data2.Value.x *= -1;
                }
                else if (data0.Value.x > Region.z)
                {
                    data0.Value.x = Region.z;
                    data2.Value.x *= -1;
                }
                if (data0.Value.z < Region.y)
                {
                    data0.Value.z = Region.y;
                    data2.Value.z *= -1;
                }
                if (data0.Value.z > Region.w)
                {
                    data0.Value.z = Region.w;
                    data2.Value.z *= -1;
                }
            }
        }
        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            return new Job
            {
                Region = Region
            }.Schedule(this, 1024*16, inputDeps);
        }
    }
}