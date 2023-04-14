using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace NineStars
{
    // Reverse the speedup in Gail-related methods
    [HarmonyPatch]
    class SpeedUpExceptions
    {
        static MethodInfo get_deltaTimeMethod = typeof(UnityEngine.Time)
            .GetProperty("deltaTime").GetGetMethod();
        static MethodInfo get_timeMethod = typeof(UnityEngine.Time)
            .GetProperty("time").GetGetMethod();
        static MethodInfo set_animSpeedMethod = typeof(Animator)
            .GetProperty("speed").GetSetMethod();
        static FieldInfo ukemiFramesField = typeof(ControlAdapter)
            .GetField("num_frames_since_last_SPRINT_PRESSED");

        static IEnumerable<MethodBase> TargetMethods()
        {
            IEnumerable<MethodInfo> gailMethods = AccessTools.GetDeclaredMethods(typeof(GaleLogicOne))
                .Where(method => method.Name.EndsWith("Update") || method.Name.StartsWith("_STATE") ||
                (new string[] {
                    "__NormalJumpConditions",
                    "__RestoreTagFromInjury",
                    "_CheckForFallImpact",
                    "_CheckForSlippage",
                    "_CrankLamp",
                    "_GettingComboed",
                    "_GoToState",
                    "_HorizontalMovementInWater",
                    "_IncreaseMiscCount",
                    "_JavelinOrGunMovement",
                    "_LampDepletionPerFrame",
                    "_MovementVx",
                    "_PlayedOcarinaSong",
                    "InformEvent",
                    "SA_animate",
                    "SendGaleCommand",
                    "TIDAL_PerformAction",
                }).Any(method.Name.Contains));

            return AccessTools.GetDeclaredMethods(typeof(LiftableMover))
                .Where(method => method.Name == "_STATE_FN_RiseAboveGale")
                .Concat(gailMethods)
                .Cast<MethodBase>();
        }
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {

            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Call && instruction.operand == get_deltaTimeMethod)
                {
                    yield return instruction;
                    yield return CodeInstruction.LoadField(typeof(Main), "inverseSpeedUp");
                    yield return new CodeInstruction(OpCodes.Mul);
                }
                else if (instruction.opcode == OpCodes.Call && instruction.operand == get_timeMethod) // Door interact broken?
                {
                    yield return instruction;
                    yield return CodeInstruction.LoadField(typeof(Main), "inverseSpeedUp");
                    yield return new CodeInstruction(OpCodes.Mul);
                }
                else if (instruction.opcode == OpCodes.Callvirt && instruction.operand == set_animSpeedMethod)
                {
                    yield return CodeInstruction.LoadField(typeof(Main), "inverseSpeedUp");
                    yield return new CodeInstruction(OpCodes.Mul);
                    yield return instruction;
                }
                else if (instruction.opcode == OpCodes.Ldfld && instruction.operand == ukemiFramesField)
                {
                    yield return instruction;
                    yield return new CodeInstruction(OpCodes.Conv_R4);
                    yield return CodeInstruction.LoadField(typeof(Main), "inverseSpeedUp");
                    yield return new CodeInstruction(OpCodes.Mul);
                    yield return new CodeInstruction(OpCodes.Conv_I4);
                }
                else
                    yield return instruction;
            }
        }
    }
    [HarmonyPatch(typeof(GaleLogicOne), "Start")]
    public static class GailStart_Patch
    {
        static FieldInfo animField = typeof(GaleLogicOne)
            .GetField("_anim", BindingFlags.Instance | BindingFlags.NonPublic);
        public static void Postfix(ref GaleLogicOne __instance)
        {
            Animator anim = (Animator)animField.GetValue(__instance);
            anim.speed = Main.inverseSpeedUp;
            animField.SetValue(__instance, anim);
        }
    }
    [HarmonyPatch(typeof(GaleLogicOne), "SendGaleCommand")]
    public static class DoorUp_Patch
    {
        public static void Prefix(GALE_CMD gale_cmd, ref float level)
        {
            if (gale_cmd == GALE_CMD.PREVENT_DOOR_UP_SPAM)
            {
                level *= Main.inverseSpeedUp;
            }
        }
    }
    [HarmonyPatch(typeof(Mover2), "PerformLedgeDrop")]
    public static class LedgeDrop_Patch
    {
        public static void Prefix(ref float time_to_ignore_owp)
        {
            time_to_ignore_owp *= Main.speedUp;
        }
    }
}
