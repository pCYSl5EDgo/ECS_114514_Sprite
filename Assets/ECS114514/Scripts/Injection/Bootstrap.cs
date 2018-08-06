using UnityEngine;

using Unity.Entities;
using Unity.Transforms;
using Unity.Rendering;
using Unity.Mathematics;

using System;
using System.Reflection;

namespace Wahren
{
    static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            var worlds = new World[3];

            // もしもPure ECSではなくGameObjectEntityなどを使用してHybrid ECSを使いたいと言うならば、Hookする必要がある。
            // ただ、その場合Unity.Entities.GameObjectArrayInjectionHookインスタンスが必要だが、internalなクラスであるのでリフレクションが必須となる。
            // 参考までにここに書いておく。
            
            // var assembly = Assembly.GetAssembly(typeof(GameObjectEntity));
            // InjectionHookSupport.RegisterHook(Activator.CreateInstance(assembly.GetType("Unity.Entities.GameObjectArrayInjectionHook")) as InjectionHook);
            // InjectionHookSupport.RegisterHook(Activator.CreateInstance(assembly.GetType("Unity.Entities.TransformAccessArrayInjectionHook")) as InjectionHook);
            // InjectionHookSupport.RegisterHook(Activator.CreateInstance(assembly.GetType("Unity.Entities.ComponentArrayInjectionHook")) as InjectionHook);
            
            
            // ComponentSystemPatchAttributeというクラスに付く属性が存在する。
            // 用途はイマイチ不明であるが、ComponentSystemの動作の上書きでもするのだろうか？
            // ScriptBehaviourUpdateOrder.csで言及されている。要研究。

            // var AddComponentSystemPatch = typeof(World).GetMethod("AddComponentSystemPatch", BindingFlags.NonPublic | BindingFlags.Instance);
            // AddComponentSystemPatch.Invoke(world, new object[] { SpriteRenderSystem_DoubleBufferType });
            
            PlayerLoopManager.RegisterDomainUnload(DomainUnloadShutdown, 10000);
            
            var Bound = new float4(-50, -50, 50, 50);
            var MoveForwardSystemType = typeof(MoveForwardSystem);

            // 最低限必要なSystemだけに絞るべし。

            worlds[0] = World.Active = new World("world0");
            worlds[0].CreateManager<BoundReflectSystem>().Region = Bound;
            // 内部実装上ジェネリクスはTypeオブジェクトを引数に取るメソッドのラッパーでしかない。
            worlds[0].CreateManager(MoveForwardSystemType);
            worlds[0].CreateManager(typeof(SpriteRenderSystem));
            
            worlds[1] = new World("world1");
            worlds[1].CreateManager<BoundReflectSystem>().Region = Bound;
            worlds[1].CreateManager(MoveForwardSystemType);
            worlds[1].CreateManager(typeof(SpriteRenderSystem_DoubleBuffer));

            worlds[2] = new World("world2");
            worlds[2].CreateManager<BoundReflectSystem>().Region = Bound;
            worlds[2].CreateManager(MoveForwardSystemType);
            worlds[2].CreateManager(typeof(TransformSystem));
            worlds[2].CreateManager(typeof(MeshInstanceRendererSystem));
            
            ScriptBehaviourUpdateOrder.UpdatePlayerLoop(worlds);
        }
        static void DomainUnloadShutdown()
        {
            World.DisposeAllWorlds();
            ScriptBehaviourUpdateOrder.UpdatePlayerLoop();
        }
    }
}