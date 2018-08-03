using Unity.Entities;
using Unity.Collections;
using Unity.Rendering;
using Unity.Transforms;

namespace Wahren
{
    sealed class BulletRenderSystem : ComponentSystem
    {
        ComponentGroup g;
        protected override void OnCreateManager(int capacity)
        {

        }
        protected override void OnUpdate()
        {
            // throw new System.NotImplementedException();
        }
    }
}