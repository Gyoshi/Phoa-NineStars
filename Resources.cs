using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;

namespace NineStars
{
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
        [HarmonyPatch(typeof(SaveFile), "_SummonGameOverScreen")]
    public static class SilenceDeathBlare_Patch
    {
        public static void Prefix()
        {
            PT2.hud_heart.ForceCancelBlareSfx();
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
                    __instance.stamina_stun += 2f * Main.inverseSpeedUp * UnityEngine.Time.deltaTime;
                else if (PT2.gale_interacter.stats.stamina < PT2.gale_interacter.stats.max_stamina)
                    PT2.gale_interacter.stats.stamina -= 50f * Main.inverseSpeedUp * UnityEngine.Time.deltaTime;

                PT2.hud_stamina.J_SetCurrentStamina(PT2.gale_interacter.stats.stamina);
            }
        }
    }

    //// Fewer inventory slots
    //[HarmonyPatch(typeof(SaveFile), "_NS_CompactSaveDataAsString")]
    //public static class Save_Patch
    //{
    //    public static void Postfix(ref string __result)
    //    {
    //        Main.logger.Log(" Original save : \n" + __result);
    //        string[] array = __result.Split(new char[] { ',' });
    //        array[12] = (int.Parse(array[13]) + Main.inventoryBinding).ToString();
    //        __result = string.Join(",", array);
    //        Main.logger.Log(" Modified save : \n" + __result);
    //    }
    //}    
    
    // Food Nerf
    [HarmonyPatch(typeof(DB), "_LoadItemDefinitions")]
    public static class Food_Patch
    {
        public static string HalveString(string str)
        {
            int value = int.Parse(str);
            value = (value == 250) ? 250 : (value / 2 + 1);
            if (value > 20 && value % 10 == 1)
                value -= 1;

            return value.ToString();
        }
        public static void Postfix()
        {
            var regex = new Regex(@"(hp,)(-?[0-9]*)");
            for (int i = 0; i < DB.ITEM_DEFS.Length; i++)
            {
                var item = DB.ITEM_DEFS[i];
                if (item.commands == null)
                    continue;
                item.commands = regex.Replace(item.commands, m => m.Groups[1].Value + HalveString(m.Groups[2].Value));
                DB.ITEM_DEFS[i] = item;
            }
        }
    }
    [HarmonyPatch(typeof(GaleInteracter), "Apply_TIDAL_effects")]
    public static class Eat_Patch
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ldc_I4_S && instruction.operand.ToString() == "50")
                    instruction.operand = 25; // Reduce threshold for healing sound effect
                yield return instruction;
            }
        }
    }
}
