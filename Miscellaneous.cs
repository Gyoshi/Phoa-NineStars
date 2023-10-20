using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace NineStars
{

    // Damage on energy overuse
    [HarmonyPatch(typeof(GaleLogicOne), "_EnoughStamina")]
    public static class Exhaustion_Patch
    {
        private static float exhaustionTimer = 0f;
        public static void Postfix(bool use_fx, bool __result)
        {
            if (use_fx && !__result && UnityEngine.Time.unscaledTime > exhaustionTimer + 0.3f)
            {
                exhaustionTimer = UnityEngine.Time.unscaledTime;
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
                int damage = (int)(fallingTime * 5f - 2.25f); // increments around after 7.5,9,13 tiles
                damage = (damage > 3) ? 3 : damage;

                float volume = (damage > 1) ? 0.85f : 0.7f;
                PT2.sound_g.PlayCommonSfx(180, PT2.gale_script.GetTransform().position, volume, 0f, global::GL.M_RandomPitch(1f, 0.03f), 0f);

                Main.IncurDamage(damage, true);
            }
        }
    }
    [HarmonyPatch(typeof(GaleLogicOne), "_GoToState")]
    public static class DieFromFallDamage_Patch
    {
        public static bool Prefix(object new_state)
        {
            bool dying = new_state.ToString() == "DYING";
            bool limbo = LevelBuildLogic.level_name == "limbo";
            if (!dying && !limbo && PT2.gale_interacter.stats.hp <= 0)
            {
                goToStateMethod.Invoke(PT2.gale_script, new object[] { Enum.Parse(galeStateEnum, "DYING"), 0f });
                return false;
            }
            return true;
        }
        static MethodInfo goToStateMethod = AccessTools.Method(typeof(GaleLogicOne), "_GoToState");
        static Type galeStateEnum = AccessTools.Inner(typeof(GaleLogicOne), "GALE_STATE");
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
        public static void Prefix(bool super_charged_spear, GaleLogicOne __instance)
        {
            if (super_charged_spear)
                Main.UseStamina(32.5f);
        }
    }

    // Fast eating energy cost
    [HarmonyPatch(typeof(GaleLogicOne), "_SpeedUpEatingProcess")]
    public static class FastEating_Patch
    {
        public static bool Prefix()
        {
            return Main.UseStamina(5f, true);
        }
    }

    // Drowning Damage
    [HarmonyPatch(typeof(GaleLogicOne), "_STATE_Drowning")]
    public static class Drown_Patch
    {
        public static void Prefix()
        {
            Hitbox.AttackStat attack = DB.AT_map["drowning"];
            attack.damage_amount = PT2.gale_interacter.stats.max_hp / 5 + 2;
            DB.AT_map["drowning"] = attack;
        }
    }
}
