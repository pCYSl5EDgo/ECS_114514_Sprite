using Unity.Entities;

namespace Wahren
{
    struct RotationComponent : IComponentData
    {
        public float radian;
        public RotationComponent(float radian)
        {
            this.radian = radian;
        }
    }
}