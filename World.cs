using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using static UnityModManagerNet.UnityModManager;

namespace NineStars
{
    // Modded levels
    [HarmonyPatch(typeof(LevelBuildLogic), "_LoadLevel")]
    public static class LevelMod_Patch
    {
        public static string modLevelsPath;
        public static string[] moddedLevels;

        static FieldInfo levelPathPrefixField = AccessTools.Field(typeof(LevelBuildLogic), "_level_path_prefix");

        public static void Load(ModEntry modEntry)
        {
            modLevelsPath = Path.Combine(modEntry.Path, "ModifiedLevels");

            moddedLevels = Directory.GetFiles(modLevelsPath);
            for (int i = 0; i < moddedLevels.Length; i++)
                moddedLevels[i] = Path.GetFileNameWithoutExtension(moddedLevels[i]);
        }
        public static void Prefix(ref LevelBuildLogic __instance, string new_level_name)
        {
            //Main.logger.Log("Loading level : " + new_level_name);
            if (moddedLevels.Contains(new_level_name.ToLower()))
            {
                levelPathPrefixField.SetValue(__instance, modLevelsPath + "/");
            }
        }
        public static void Postfix(ref LevelBuildLogic __instance)
        {
            string path = Application.dataPath + "/StreamingAssets/Levels/";
            if (!Directory.Exists(path))
                path = Application.dataPath + "/Resources/Data/StreamingAssets/levels/"; // mirroring LevelBuildLogic's Awake method
            levelPathPrefixField.SetValue(__instance, path);
        }
    }

    // Custom Lines and Dialogue, etc.
    [HarmonyPatch]
    public static class DB_Patch
    {
        private static string[] lines = {
            "ID,CODE,NAME,LINE,NOTES,TAGS",
            "NS_DIFF_CHOICE,\"PROFILE,static;TXT_BOX_WIDTH,1000;VOICE,static;POS,0.5,0.5;AUTO_LOCK;CHOICE,*1*\",,\"<*_>.<*_>.<*_>.<*____>\\n\\n\\n||<sprite=50><sprite=50><sprite=50><sprite=50><sprite=50><*_><sprite=50><*__><sprite=50><*___><sprite=50><*____><sprite=50>\",,",
            ",\"PROFILE,static;TXT_BOX_WIDTH,1000;VOICE,static;POS,0.5,0.5;CHOICE,NS_DIFF_CHOICE,DIFF_CHOICE_SET+4\",,\"At <sprite=50><sprite=50><sprite=50><sprite=50><sprite=50><sprite=50><sprite=50><sprite=50><sprite=50>, the following will be active:\\n\\n<size=-5><#FFECBD>  - Time is sped up, except for Gail\\n  - Health and Energy is severely limited\\n  - Puzzles are more difficult, often unfair\\n  - And more... </color></size>\n\n\nProceed?||No||Yes\",,",
        };
        private static string[] misc = {
            "BASE,TRANSLATE_SWITCH,TRANSLATE_PC,NOTES",
            "NS_PAUSE_TUT,<sprite=11> or <sprite=10> :{0}{0}Pause repeatedly,@-Options or @-Inventory :{0}{0}Pause Repeatedly,",
        };

        private static string SPLIT_RE = ",(?=(?:[^\"]*\"[^\"]*\")*(?![^\"]*\"))";
        private static string LINE_SPLIT_RE = "\\r\\n|\\n\\r|\\n|\\r";
        private static char[] TRIM_CHARS = new char[] { '"' };

        public static List<Dictionary<string, string>> lineData;
        public static List<Dictionary<string, string>> miscData;

        public static void Load()
        {
            lineData = ReadCSV(lines);
            miscData = ReadCSV(misc);
        }

        static IEnumerable<MethodBase> TargetMethods()
        {
            return AccessTools.GetDeclaredMethods(typeof(DB))
                .Where(method => (new string[] {
                    "_LoadDialogueAndCutsceneLines",
                    "_LoadTranslateMap",
                }).Any(method.Name.Contains))
                .Cast<MethodBase>();
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            int counter = int.MinValue;
            string csvData;
            switch (__originalMethod.Name)
            {
                case "_LoadDialogueAndCutsceneLines":
                    csvData = "lineData";
                    break;
                case "_LoadTranslateMap":
                    csvData = "miscData";
                    break;
                default:
                    throw new NotImplementedException();
            }
            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Call && instruction.operand == CodeInstruction.Call(typeof(CSVReader), "Read").operand)
                {
                    counter = 0;
                }

                if (counter == 2)
                {
                    yield return new CodeInstruction(OpCodes.Ldloc_1);
                    yield return CodeInstruction.LoadField(typeof(DB_Patch), csvData);
                    yield return CodeInstruction.Call(typeof(List<Dictionary<string, string>>), "AddRange");
                }
                yield return instruction;
                counter++;
            }
        }

        public static List<Dictionary<string, string>> ReadCSV(string[] array) //Copied from CSVReader.Read()
        {
            List<Dictionary<string, string>> list = new List<Dictionary<string, string>>();

            if (array.Length <= 1)
            {
                return list;
            }
            string[] array2 = Regex.Split(array[0], SPLIT_RE);
            for (int i = 1; i < array.Length; i++)
            {
                string[] array3 = Regex.Split(array[i], SPLIT_RE);
                Dictionary<string, string> dictionary = new Dictionary<string, string>();
                int num = 0;
                while (num < array2.Length && num < array3.Length)
                {
                    string text = array3[num];
                    text = text.TrimStart(TRIM_CHARS).TrimEnd(TRIM_CHARS);
                    dictionary[array2[num]] = text;
                    num++;
                }
                list.Add(dictionary);
            }
            return list;
        }
    }


    // Pooki mad
    [HarmonyPatch(typeof(AnimalLifeLogic), "InitializePuki")]
    public static class Pooki_Patch
    {
        public static void Prefix(string object_code)
        {
            PT2.save_file.OC_Write(object_code + "angry");
        }
    }
}
