using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StormUI.Patch
{
    // Метод замены слов на свои
    public class Translatation
    {
        public static void Replace()
        {
            foreach (KeyValuePair<string, string> phrase in Defines.Translation)
                if (Translate.translations.ContainsKey(phrase.Key))
                    Translate.translations[phrase.Key] = phrase.Value;
        }
    }

    [HarmonyPatch(typeof(ConVar.Global))]
    [HarmonyPatch("language", MethodType.Setter)]
    public class ConVar_Language_Patch
    {
        static bool Prefix()
        {
            return Defines.isMenuInitialized;
        }
    }

    // Инициализация перевода
    [HarmonyPatch(typeof(Translate), "Init")]
    public class Translate_Patch
    {
        static void Postfix()
        {
            // Ставим английский, иначе не заменяться / удаляться многие элементы

            if (Translate.GetLanguage() == "en")
            {
                return;
            }
            Translate.language = "en";
            Translate.LoadLanguage(Translate.language);
            Translate.SetLanguage("en");

            ConVar.Global.language = "en";

            Translatation.Replace();
        }
    }

    // Смена языка
    [HarmonyPatch(typeof(Translate), "SetLanguage")]
    public class Translate_Patch2
    {
        static void Postfix(string str)
        {
            Translatation.Replace();
        }
    }

    [HarmonyPatch(typeof(Translate), "LoadLanguage")]
    public class Translate_Patch3
    {
        static void Postfix(string lang)
        {
            Translatation.Replace();
        }
    }
}
