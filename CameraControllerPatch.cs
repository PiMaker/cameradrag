using Game;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;
using Unity.Mathematics;

namespace CameraDrag
{
    [HarmonyPatch(typeof(CameraController))]
    public class CameraControllerPatch
    {
        private static bool originalEdgeScrolling;
        public static float2 Delta;

        [HarmonyPrefix]
        [HarmonyPatch("UpdateCamera", MethodType.Normal)]
        public static void UpdateCameraPrefix(CameraController __instance)
        {
            // run our own code as part of the camera update loop
            Mod.Instance.UpdateMod();

            // copy so we can easily access static fields via `ldsfld`
            Delta = Mod.Instance.Delta;

            // disable edge scrolling while dragging
            originalEdgeScrolling = __instance.edgeScrolling;
            __instance.edgeScrolling &= !Mod.Instance.Dragging;
        }

        [HarmonyPostfix]
        [HarmonyPatch("UpdateCamera", MethodType.Normal)]
        public static void UpdateCameraPostfix(CameraController __instance)
        {
            __instance.edgeScrolling = originalEdgeScrolling;
        }

        [HarmonyTranspiler]
        [HarmonyPatch("UpdateCamera", MethodType.Normal)]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var foundMathRadiansCall = false;
            var addedDeltaLoad = false;

            var foundMRotateLoad = false;
            var addedMRotateReadValueOverride = false;

            //foreach (var item in instructions)
            //    Mod.log.Info(item.ToString());

            foreach (var instruction in instructions)
            {
                // handle rotation disabling
                if (!foundMRotateLoad && instruction.LoadsField(AccessTools.DeclaredField(typeof(CameraController), "m_RotateAction")))
                {
                    Mod.log.Info($"Transpiler::UpdateCamera 1: {instruction}");

                    // load Mod.Instance instead, pop `this` to keep stack intact
                    yield return new CodeInstruction(OpCodes.Pop);
                    yield return CodeInstruction.LoadField(typeof(Mod), nameof(Mod.Instance));
                    foundMRotateLoad = true;
                    continue;
                }
                else if (foundMRotateLoad && !addedMRotateReadValueOverride)
                {
                    Mod.log.Info($"Transpiler::UpdateCamera 2: {instruction}");

                    // replace the call to m_RotateAction::ReadValue with our own
                    yield return CodeInstruction.Call(typeof(Mod), nameof(Mod.ReadRotateValue));
                    addedMRotateReadValueOverride = true;
                    continue;
                }
                // handle injecting our delta (2-stage detection)
                else if (!foundMathRadiansCall && instruction.Calls(AccessTools.Method(typeof(math), nameof(math.radians), new[] { typeof(float2) })))
                {
                    Mod.log.Info($"Transpiler::UpdateCamera 3: {instruction}");

                    // finish the call to math.radians(float2)
                    yield return instruction;
                    foundMathRadiansCall = true;
                    continue;
                }
                else if (foundMathRadiansCall && !addedDeltaLoad)
                {
                    Mod.log.Info($"Transpiler::UpdateCamera 4: {instruction}");

                    // finish loading the retval from math.radians
                    yield return instruction;

                    // insert our delta addition override
                    yield return new CodeInstruction(OpCodes.Ldloc_0); // load @float
                    yield return CodeInstruction.LoadField(typeof(CameraControllerPatch), nameof(Delta)); // load Delta field
                    yield return CodeInstruction.Call("Unity.Mathematics.float2:op_Addition", new[] { typeof(float2), typeof(float2) }); // call math.float2::op_Addition(float2)
                    yield return new CodeInstruction(OpCodes.Stloc_0); // store @float

                    addedDeltaLoad = true;
                    continue;
                }

                yield return instruction;
            }
        }
    }
}

/*

decompilation excerpt from `UpdateCamera:UpdateCamera`

`ldloc.0` (local @ index 0) is a float2 added to `m_Pivot`, which is later applied to the camera (after collision checks)
we simply add our `delta` to this local variable before that (but after existing movement options like WASD or screen-edge)

...
ldarg.0 NULL [Label8]
ldfld Unity.Mathematics.float2 Game.CameraController::m_Angle
call static Unity.Mathematics.float2 Unity.Mathematics.math::radians(Unity.Mathematics.float2 x)
stloc.s 4 (Unity.Mathematics.float2)
  <inserted-code>
    ldloc.0 NULL
    ldsfld Unity.Mathematics.float2 CameraDrag.CameraControllerPatch::Delta
    call static Unity.Mathematics.float2 Unity.Mathematics.float2::op_Addition(Unity.Mathematics.float2 a, Unity.Mathematics.float2 b)
    stloc.0 NULL
  </inserted-code>
ldloca.s 5 (Unity.Mathematics.float3)
ldloc.s 4 (Unity.Mathematics.float2)
ldfld System.Single Unity.Mathematics.float2::x
call static System.Single Unity.Mathematics.math::sin(System.Single x)
neg NULL
stfld System.Single Unity.Mathematics.float3::x
...

similarly for the rotation disabling, we just hijack the m_RotateAction::ReadValue call and replace it with our own from Mod.Instance

original:
```
ldarg.0 NULL
ldfld Game.Input.ProxyAction Game.CameraController::m_RotateAction
callvirt virtual UnityEngine.Vector2 Game.Input.ProxyAction::ReadValue()
```

patched:
```
ldarg.0 NULL
pop NULL
ldfld CameraDrag.Mod CameraDrag.Mod::Instance
callvirt UnityEngine.Vector2 CameraDrag.Mod::ReadRotateValue()
```

*/