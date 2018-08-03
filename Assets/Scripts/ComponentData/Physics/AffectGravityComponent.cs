using Unity.Entities;
using Unity.Mathematics;

namespace Wahren.Physics
{
    public struct AffectGravityComponent : IComponentData
    {
        public static readonly float3 Gravity = new float3(0, -9.81f, 0);
    }
}