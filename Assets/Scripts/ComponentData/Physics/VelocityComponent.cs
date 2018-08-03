using Unity.Entities;
using Unity.Mathematics;

namespace Wahren.Physics
{
    public struct VelocityComponent : IComponentData
    {
        public float3 Value;
        public VelocityComponent(float3 value)
        {
            Value = value;
        }
    }
}