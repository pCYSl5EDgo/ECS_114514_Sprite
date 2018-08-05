#if UNITY_DISABLE_AUTOMATIC_SYSTEM_BOOTSTRAP
using UnityEngine;

using Unity.Entities;
using Unity.Transforms;
using Unity.Rendering;

using System;
using System.Reflection;

namespace Wahren
{
    static class AutomaticWorldBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            var world = World.Active = new World("world1");
            // var assembly = Assembly.GetAssembly(typeof(GameObjectEntity));
            // InjectionHookSupport.RegisterHook(Activator.CreateInstance(assembly.GetType("Unity.Entities.GameObjectArrayInjectionHook")) as InjectionHook);
            // InjectionHookSupport.RegisterHook(Activator.CreateInstance(assembly.GetType("Unity.Entities.TransformAccessArrayInjectionHook")) as InjectionHook);
            // InjectionHookSupport.RegisterHook(Activator.CreateInstance(assembly.GetType("Unity.Entities.ComponentArrayInjectionHook")) as InjectionHook);
            PlayerLoopManager.RegisterDomainUnload(DomainUnloadShutdown, 10000);
            world.CreateManager(typeof(EndFrameBarrier));
            world.CreateManager(typeof(BoundReflectSystem));
            world.CreateManager(typeof(TransformInputBarrier));
            world.CreateManager(typeof(MoveForwardSystem));
            world.CreateManager(typeof(TransformSystem));
            world.CreateManager(typeof(MeshCullingBarrier));
            world.CreateManager(typeof(SpriteRenderSystem_DoubleBuffer));
            world.CreateManager(typeof(EntityManager));
            // var AddComponentSystemPatch = typeof(World).GetMethod("AddComponentSystemPatch", BindingFlags.NonPublic | BindingFlags.Instance);
            // AddComponentSystemPatch.Invoke(world, new object[] { SpriteRenderSystem_DoubleBufferType });
            // AddComponentSystemPatch.Invoke(world, new object[] { MoveForwardSystemType });
            // AddComponentSystemPatch.Invoke(world, new object[] { BoundReflectSystemType });
            // AddComponentSystemPatch.Invoke(world, new object[] { EndFrameBarrierType });
            // AddComponentSystemPatch.Invoke(world, new object[] { TransformSystemType });
            // AddComponentSystemPatch.Invoke(world, new object[] { TransformInputBarrierType });
            // AddComponentSystemPatch.Invoke(world, new object[] { MeshCullingBarrierType });
            ScriptBehaviourUpdateOrder.UpdatePlayerLoop(world);
        }
        static void DomainUnloadShutdown()
        {
            World.DisposeAllWorlds();
            ScriptBehaviourUpdateOrder.UpdatePlayerLoop();
        }
    }
}
#endif