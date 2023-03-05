using HarmonyLib;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using UnityModManagerNet;

namespace NineStars
{
#if DEBUG
    [EnableReloading]
#endif
    internal static class Main
    {
        public static int startHP = 10;
        public static float startEnergy = 60f;
        public static int rubyHP = 1;
        public static float gemEnergy = 5f;

        public static float speedUp = 2f;
        public static float inverseSpeedUp = 1f / speedUp;

        public static Harmony harmony;
        public static UnityModManager.ModEntry.ModLogger logger;

        static void Load(UnityModManager.ModEntry modEntry)
        {
            logger = modEntry.Logger;

            modEntry.OnUpdate = OnUpdate;
#if DEBUG
            modEntry.OnUnload = Unload;
            Harmony.DEBUG = true;
#endif
            harmony = new Harmony(modEntry.Info.Id);
            harmony.PatchAll();

        }
#if DEBUG
        static bool Unload(UnityModManager.ModEntry modEntry)
        {
            harmony.UnpatchAll(modEntry.Info.Id);

            return true;
        }
#endif

        static void OnUpdate(UnityModManager.ModEntry modEntry, float dt)
        {
            if (Time.timeScale == 1f)
                Time.timeScale = speedUp;
        }
    }

    // Reverse the speedup in Gail-related methods
    [HarmonyPatch]
    class SpeedUpExceptions
    {
        static MethodInfo get_deltaTimeMethod = typeof(Time)
            .GetProperty("deltaTime").GetGetMethod();
        static MethodInfo set_animSpeedMethod = typeof(Animator)
            .GetProperty("speed").GetSetMethod();
        static FieldInfo ukemiFramesField = typeof(ControlAdapter)
            .GetField("num_frames_since_last_SPRINT_PRESSED");

        static IEnumerable<MethodBase> TargetMethods()
        {
            return AccessTools.GetDeclaredMethods(typeof(GaleLogicOne))
                .Where(method => method.Name.EndsWith("Update") || method.Name.StartsWith("_STATE") ||
                (new string[] { 
                    "_IncreaseMiscCount",
                    "__RestoreTagFromInjury",
                    "_CheckForFallImpact",
                    "_CheckForSlippage",
                    "_HorizontalMovementInWater",
                    "__NormalJumpConditions",
                    "_MovementVx",
                    "_LampDepletionPerFrame",
                    "_JavelinOrGunMovement",
                    "SendGaleCommand",
                    "_GoToState",
                    "_CrankLamp",
                    "SA_animate"
                }).Any(method.Name.Contains))
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
                else if (instruction.opcode == OpCodes.Callvirt && instruction.operand == set_animSpeedMethod)
                {
                    yield return CodeInstruction.LoadField(typeof(Main), "inverseSpeedUp");
                    yield return new CodeInstruction(OpCodes.Mul);
                    yield return instruction;
                }
                else if (instruction.opcode == OpCodes.Ldfld && instruction.operand == ukemiFramesField)
                {
                    yield return instruction;
                    yield return new CodeInstruction (OpCodes.Conv_R4);
                    yield return CodeInstruction.LoadField(typeof(Main), "inverseSpeedUp");
                    yield return new CodeInstruction(OpCodes.Mul);
                    yield return new CodeInstruction(OpCodes.Conv_I4);
                }
                else
                    yield return instruction;
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
    
    // Lower Health & Energy
    [HarmonyPatch(typeof(SaveFile), "_NS_ProcessSaveDataString")]
    public static class LoadSave_Patch
    {
        public static void Postfix()
        {
            int rubies = PT2.save_file.GetInt(2);
            PT2.gale_interacter.stats.max_hp = Main.startHP + Main.rubyHP * rubies;
            if (PT2.gale_interacter.stats.hp > PT2.gale_interacter.stats.max_hp)
                PT2.gale_interacter.stats.hp = PT2.gale_interacter.stats.max_hp;
            PT2.hud_heart.J_UpdateHealth(PT2.gale_interacter.stats.hp, PT2.gale_interacter.stats.max_hp, false, true);
            int gems = PT2.save_file.GetInt(3);
            PT2.gale_interacter.stats.max_stamina = Main.startEnergy + (float)gems * Main.gemEnergy;
            PT2.gale_interacter.stats.stamina = PT2.gale_interacter.stats.max_stamina / 2f;
            PT2.hud_stamina.J_InitializeStaminaHud(PT2.gale_interacter.stats.max_stamina);
        }
    }
    [HarmonyPatch(typeof(GaleLogicOne), "ApplyGaleUpgrade")]
    public static class Upgrade_Patch
    {
        public static bool Prefix(GaleInteracter.STAT_UPGRADE upgrade_type)
        {
            if (upgrade_type == GaleInteracter.STAT_UPGRADE.HEALTH_UPGRADE)
            {
                GaleInteracter gale_interacter2 = PT2.gale_interacter;
                gale_interacter2.stats.max_hp = gale_interacter2.stats.max_hp + Main.rubyHP;
                PT2.gale_interacter.stats.hp = PT2.gale_interacter.stats.max_hp;
                PT2.hud_heart.J_UpdateHealth(PT2.gale_interacter.stats.hp, PT2.gale_interacter.stats.max_hp, false, false);
                PT2.hud_heart.J_UpgradeFX();
            }
            if (upgrade_type == GaleInteracter.STAT_UPGRADE.STAMINA_UPGRADE)
            {
                GaleInteracter gale_interacter = PT2.gale_interacter;
                gale_interacter.stats.max_stamina = gale_interacter.stats.max_stamina + Main.gemEnergy;
                PT2.hud_stamina.J_UpgradeFX();
            }
            return false;
        }
    }

    // Halve effect of energy buff
    [HarmonyPatch(typeof(GaleLogicOne), "VariableUpdate")]
    public static class Buff_Patch
    {
        public static void Postfix(ref GaleLogicOne __instance)
        {
            if (PT2.gale_interacter.stats.stamina_buff > 0f)
            {
                if (__instance.stamina_stun > 0f)
                    __instance.stamina_stun += 2f * Main.inverseSpeedUp * Time.deltaTime;
                else if (PT2.gale_interacter.stats.stamina < PT2.gale_interacter.stats.max_stamina)
                    PT2.gale_interacter.stats.stamina -= 50f * Main.inverseSpeedUp * Time.deltaTime;

                PT2.hud_stamina.J_SetCurrentStamina(PT2.gale_interacter.stats.stamina);
            }
        }
    }
}