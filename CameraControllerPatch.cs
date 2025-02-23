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
        private static float2 delta;

        [HarmonyPrefix]
        [HarmonyPatch("UpdateCamera", MethodType.Normal)]
        public static void UpdateCameraPrefix(CameraController __instance)
        {
            Mod.Instance.UpdateMod();
            delta = Mod.Instance.Delta; // copy so we can easily access the static field via `ldsfld`
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
            var foundFirst = false;
            var foundSecond = false;
            foreach (var instruction in instructions)
            {
                if (!foundFirst && instruction.Calls(AccessTools.Method(typeof(math), nameof(math.radians), new[] { typeof(float2) })))
                {
                    Mod.log.Debug($"Transpiler::UpdateCamera: {instruction}");

                    // finish the call to math.radians(float2)
                    yield return instruction;
                    foundFirst = true;
                    continue;
                }
                else if (foundFirst && !foundSecond)
                {
                    Mod.log.Debug($"Transpiler::UpdateCamera: {instruction}");

                    // finish loading the retval from math.radians
                    yield return instruction;

                    // insert our delta addition override
                    yield return new CodeInstruction(OpCodes.Ldloc_0); // load @float
                    yield return CodeInstruction.LoadField(typeof(CameraControllerPatch), nameof(delta)); // load delta field
                    yield return CodeInstruction.Call("Unity.Mathematics.float2:op_Addition", new[] { typeof(float2), typeof(float2) }); // call math.float2::op_Addition(float2)
                    yield return new CodeInstruction(OpCodes.Stloc_0); // store @float

                    foundSecond = true;
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
    ldsfld Unity.Mathematics.float2 CameraDrag.CameraControllerPatch::delta
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

*/