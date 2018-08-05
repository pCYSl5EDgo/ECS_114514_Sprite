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
        // x:最小X
        // y:最小Z
        // z:最大X
        // w:最大Z
        public float4 Region;
        [BurstCompile]
        struct Job : IJobProcessComponentData<Position, MoveSpeed, Heading>
        {
            public float4 Region;
            public void Execute(ref Position data0, [ReadOnly]ref MoveSpeed data1, ref Heading data2)
            {
                // 範囲内にクランプする。
                data0.Value.xz = math.clamp(data0.Value.xz, Region.xy, Region.zw);
                // 範囲境界にあるならば
                var xzxz = math.equal(data0.Value.xzxz, Region);
                // 移動方向を反転させる
                data2.Value.x *= math.any(xzxz.xz) ? -1 : 1;
                data2.Value.z *= math.any(xzxz.yw) ? -1 : 1;
            }
        }
        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            return new Job
            {
                Region = Region
            }.Schedule(this, 1024 * 16, inputDeps);
        }
    }
}