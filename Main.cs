using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TMPro;
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

        public static float speedUp = 1.34164f;
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

        public static void IncurDamage(int damage, bool lethal = false)
        {
            damage = (PT2.gale_interacter.stats.hp <= damage) ? (PT2.gale_interacter.stats.hp - (lethal ? 0 : 1)) : damage;

            PT2.gale_interacter.stats.hp -= damage;
            PT2.hud_heart.J_UpdateHealth(PT2.gale_interacter.stats.hp, PT2.gale_interacter.stats.max_hp, false, false);
            PT2.gale_interacter.DisplayNumAboveHead(damage, DamageNumberLogic.DISPLAY_STYLE.GALE_DAMAGE, true);

            if (PT2.gale_interacter.stats.hp <= 0)
                goToStateMethod.Invoke(PT2.gale_script, new object[] { Enum.Parse(galeStateEnum, "DYING"), 0f });
        }
        static MethodInfo goToStateMethod = AccessTools.Method(typeof(GaleLogicOne), "_GoToState");
        static Type galeStateEnum = AccessTools.Inner(typeof(GaleLogicOne), "GALE_STATE");

        public static bool UseStamina(float amount, bool checkEnough = false)
        {
            if (!checkEnough)
            {
                useStaminaMethod.Invoke(PT2.gale_script, new object[] { amount, false });
                return true;
            }

            bool enough = (bool)enoughStaminaMethod.Invoke(PT2.gale_script, new object[] { true });
            if (enough)
                UseStamina(amount);
            return enough;
        }
        static MethodInfo enoughStaminaMethod = AccessTools.Method(typeof(GaleLogicOne), "_EnoughStamina");
        static MethodInfo useStaminaMethod = AccessTools.Method(typeof(GaleLogicOne), "_UseUpStamina");
    }

    // Reverse the speedup in Gail-related methods
    [HarmonyPatch]
    class SpeedUpExceptions
    {
        static MethodInfo get_deltaTimeMethod = typeof(Time)
            .GetProperty("deltaTime").GetGetMethod();
        static MethodInfo get_timeMethod = typeof(Time)
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
    [HarmonyPatch(typeof(ItemGridLogic), "_SetMaxHp")]
    public static class HPDisplay_Patch
    {
        static FieldInfo maxHpTextField = AccessTools.Field(typeof(ItemGridLogic), "_max_hp_text");
        public static void Postfix(ref ItemGridLogic __instance)
        {
            TextMeshPro maxHpText = (TextMeshPro)maxHpTextField.GetValue(__instance);
            maxHpText.text = PT2.gale_interacter.stats.max_hp.ToString();
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

    // Damage on energy overuse
    [HarmonyPatch(typeof(GaleLogicOne), "_EnoughStamina")]
    public static class Exhaustion_Patch
    {
        private static float exhaustionTimer = 0f;
        public static void Postfix(bool use_fx, bool __result)
        {
            if (use_fx && !__result && Time.unscaledTime > exhaustionTimer + 0.3f)
            {
                exhaustionTimer = Time.unscaledTime;
                Main.IncurDamage(1);
            }
        }
    }
    // Fall damage (on failed ukemi)
    [HarmonyPatch(typeof(GaleLogicOne), "_GetLandingLagAmt")]
    public static class FallDamage_Patch
    {
        static FieldInfo fallingTimeField = AccessTools.Field(typeof(GaleLogicOne), "_fall_time");
        public static void Postfix()
        {
            float fallingTime = (float)fallingTimeField.GetValue(PT2.gale_script);
            bool ukemi = PT2.director.control.num_frames_since_last_SPRINT_PRESSED < (int)(7 * Main.speedUp);
            if (fallingTime > 0.67f && !ukemi)
            {
                int damage = (int)(fallingTime * 3f) - 1;
                Main.IncurDamage(damage, true);

                float volume = (damage > 1) ? 0.85f : 0.7f;
                PT2.sound_g.PlayCommonSfx(180, PT2.gale_script.GetTransform().position, volume, 0f, global::GL.M_RandomPitch(1f, 0.03f), 0f);
            }
        }
    }

    // Only 1 spear
    [HarmonyPatch(typeof(GaleLogicOne), "GetGaleObject")]
    public static class Spear_Patch
    {
        static FieldInfo spearIndexField = typeof(GaleLogicOne)
            .GetField("_my_javelin_index", BindingFlags.Instance | BindingFlags.NonPublic);
        public static void Prefix(GALE_OBJ_REQ gale_obj_request)
        {
            if (gale_obj_request == GALE_OBJ_REQ.P1_JAVELIN_TOOL)
                spearIndexField.SetValue(PT2.gale_script, 0);
        }
    }

    // Nerf Spear Bomb energy cost
    [HarmonyPatch(typeof(GaleLogicOne), "_ThrowJavelin")]
    public static class SpearBomb_Patch
    {
        static MethodInfo useStaminaMethod = AccessTools.Method(typeof(GaleLogicOne), "_UseUpStamina");
        public static void Prefix(bool super_charged_spear, GaleLogicOne __instance)
        {
            if (super_charged_spear)
                Main.UseStamina(32.5f);
        }
    }
}