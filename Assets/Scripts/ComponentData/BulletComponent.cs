using Unity.Entities;
using Unity.Mathematics;

namespace Wahren
{
    struct BulletComponent : IComponentData
    {
        public float2x2 start;
        public float3 position;
    }
}