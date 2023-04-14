using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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

        public static int inventoryBinding = 2;

        public const string nineStarsString = "<sprite=50><sprite=50><sprite=50><sprite=50><sprite=50><sprite=50><sprite=50><sprite=50><sprite=50>";

        public static Harmony harmony;
        public static UnityModManager.ModEntry.ModLogger logger;

        static void Load(UnityModManager.ModEntry modEntry)
        {
            logger = modEntry.Logger;

            LevelMod_Patch.Load(modEntry);
            DB_Patch.Load();

#if DEBUG
            modEntry.OnUnload = Unload;
            Harmony.DEBUG = false;
#endif
            modEntry.OnToggle = OnToggle;

            modEntry.OnUpdate = OnUpdate;
            harmony = new Harmony(modEntry.Info.Id);
        }
        static bool Unload(UnityModManager.ModEntry modEntry)
        {
            harmony.UnpatchAll(modEntry.Info.Id);
            return true;
        }
        static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            logger.Log("___OnToggle " + value);
            
            if (value)
                harmony.PatchAll();
            else
                harmony.UnpatchAll(modEntry.Info.Id);
            
            if (LevelBuildLogic.level_name == null)
                return true;

            //PT2.LoadLevel("limbo", 0, Vector3.zero, true, 0f, false, true);
            if (LevelBuildLogic.level_name == "game_start")
            {
                PT2.director.current_opening_menu.GameStart();
                PT2.director.current_opening_menu = null;
                //PT2.director.current_opening_menu.Initialize();
            }
            DB.ReloadGameData();
            SaveFile.file_0_string_data = null;
            SaveFile.file_1_string_data = null;
            SaveFile.file_2_string_data = null;
            SaveFile.file_3_string_data = null;
            PT2.director.QuitToTitleScreen();

            return true;
        }

        static void OnUpdate(UnityModManager.ModEntry modEntry, float dt)
        {
            if (UnityEngine.Time.timeScale == 1f)
                UnityEngine.Time.timeScale = speedUp;
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

    // Opening menu stars
    [HarmonyPatch(typeof(OpeningMenuLogic), "_STATE_GameTile")]
    public static class OpeningMenu_Patch
    {
        static FieldInfo timerField = AccessTools.Field(typeof(OpeningMenuLogic), "_fx_timer");
        static float starTimer = 0f;
        static float starPeriod = 3f;
        public static void Postfix(ref OpeningMenuLogic __instance)
        {
            starTimer += UnityEngine.Time.deltaTime;

            string text = Main.nineStarsString;
            text = "<size=9>" + text + "</size>";

            if (!PT2.director.control.CONFIRM_PRESSED)
            {
                __instance.info_text.text = text + "\n<size=7>Demo</size>\n\n\n";
                __instance.info_text.alpha = TiltedSine(2 * (float)Math.PI * starTimer / starPeriod);
            }
        }

        public static float TiltedSine(float rad)
        {
            return (float)(0.4375f * Math.Sin(rad) - 0.109375f * Math.Sin(2 * rad) + 0.0208333f * Math.Sin(3 * rad) + 0.5f);
        }
    }

    // Schadenfreude
    [HarmonyPatch(typeof(DirectorLogic), "_OptionsExecuteCmd")]
    public static class AccessMenu_Patch
    {
        public static bool Prefix(string cmd_option)
        {
            if (cmd_option == "_OPT_ACCESS_D")
            {
                PT2.director.QuitToDesktop();
                DB.TRANSLATE_map["_OPT_ACCESS_D"] = Main.nineStarsString;
                return false;
            }
            return true;
        }
    }

    // Separate Save Files
    [HarmonyPatch]
    public static class SaveData_Patch
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            return AccessTools.GetDeclaredMethods(typeof(SaveDataHandler))
                .Where(method => (new string[] {
                    "save",
                    "load",
                }).Any(method.Name.Contains))
                .Cast<MethodBase>();
        }
        public static void Prefix(ref string filename)
        {
            if (!filename.StartsWith("settings"))
                filename = "NineStars_" + filename;
        }
    }

    // Thanks for playing Demo
    [HarmonyPatch(typeof(DB), "GetLine")]
    public static class DemoEnd_Patch
    {
        public static void Prefix(ref string line_id_expression)
        {
            int[] statusInventory = (int[])AccessTools.Field(typeof(SaveFile), "_status_inventory").GetValue(PT2.save_file);
            if (line_id_expression == "DEATH_SCRIPT" && statusInventory[262 - 232] == 21)
                line_id_expression = "NS_DEMO_END";
        }
    }
}