using HarmonyLib;
using Rust.UI;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StormUI.Patch
{ // Срабатывает при загрузке меню, удаляем лишние блоки, смещаем что необходимо
    [HarmonyPatch(typeof(MenuBackgroundVideo), "NextVideo")]
    public class MBV_Patch
    {
        // Удаляемые элементы
        static List<string> disallowedItems = new List<string>()
        {
            "NEWS",
            "Inventory",
            "Item Store",
            "Workshop",
            "Rust+"
        };

        static void Prefix()
        {
            StormMod.harmony.Patch(typeof(TweakUIToggle).GetMethod("ResetToConvar"), prefix: new HarmonyMethod(typeof(TweakUIToggle_Patch2).GetMethod("Prefix")));

            Defines.NoShadowsCommand = new ConsoleSystem.Command()
            {
                Name = "booster.noshadows",
                FullName = "booster.noshadows",
                Variable = true,
                SetOveride = new System.Action<string>((string val) =>
                {
                    Defines._noShadows = (val == "1");
                }),
                GetOveride = new System.Func<string>(() =>
                {
                    return Defines._noShadows ? "1" : "0";
                })
            };

            Defines.NoGrassCommand = new ConsoleSystem.Command()
            {
                Name = "booster.nograss",
                FullName = "booster.nograss",
                Variable = true,
                SetOveride = new System.Action<string>((string val) =>
                {
                    Defines._noGrass = (val == "1");
                }),
                GetOveride = new System.Func<string>(() =>
                {
                    return Defines._noGrass ? "1" : "0";
                })
            };

            ConsoleSystem.Index.Client.Dict.Add("booster.noshadows", Defines.NoShadowsCommand);
            ConsoleSystem.Index.Client.Dict.Add("booster.nograss", Defines.NoGrassCommand);
            ConsoleSystem.Index.Client.GlobalDict.Add("booster.noshadows", Defines.NoShadowsCommand);
            ConsoleSystem.Index.Client.GlobalDict.Add("booster.nograss", Defines.NoGrassCommand);

            Defines.isMenuInitialized = true;

            if (SingletonComponent<MainMenuSystem>.Instance == null) return;

            // Убирает фон от "NEW" бейджа
            foreach (MaskableGraphic maskableGraphic in Object.FindObjectsOfType<MaskableGraphic>())
                if (maskableGraphic != null && maskableGraphic.name == "Updated News")
                    Object.Destroy(maskableGraphic);

            // Убирает текст от "NEW" бейджа
            foreach (TMP_Text text in Object.FindObjectsOfType<TMP_Text>())
                if (text != null && text.text == "NEW")
                    Object.Destroy(text);

            // Ищит и удаляет лишние вкладки в меню
            foreach (Rust.UI.RustText component in Object.FindObjectsOfType<Rust.UI.RustText>())
            {
                if (component != null && component.name != null)
                {
                    if (disallowedItems.Contains(component.text))
                    {
                        component.enabled = false;
                        component.alpha = 0;
                        component.text = string.Empty;

                        RectTransform transform = component.GetComponent<RectTransform>();
                        if (transform != null)
                        {
                            transform.localScale = Vector3.zero;
                            transform.anchorMin = Vector2.zero;
                            transform.anchorMax = Vector2.zero;
                            transform.offsetMin = Vector2.zero;
                            transform.offsetMax = Vector2.zero;
                        }
                    }
                    else
                    {
                        if (component.text == "PLAY GAME")
                        {
                            RectTransform rectTransform = component.GetComponent<RectTransform>();
                            rectTransform.anchoredPosition = new Vector2(0, -60);

                            component.text = "Играть";
                        }
                        if (component.text == "Options")
                        {
                            RectTransform rectTransform = component.GetComponent<RectTransform>();
                            rectTransform.anchoredPosition = new Vector2(0, 190);

                            component.text = "Настройки";
                        }
                        if (component.text == "Quit")
                        {
                            RectTransform rectTransform = component.GetComponent<RectTransform>();
                            rectTransform.anchoredPosition = new Vector2(0, 200);

                            component.text = "Выйти";
                        }
                    }
                }
            }
        }
    }

    // Убирает кнопки в правом нижнем углу Discord, Twitter хуитер и тд..
    [HarmonyPatch(typeof(RustLayout), "Awake")]
    class RustLayout_Awake_Patch
    {
        static void Postfix(RustLayout __instance)
        {
            foreach (Image image in __instance.GetComponentsInChildren<Image>())
            {
                if (__instance.name == "Bottom")
                    Object.Destroy(image);
            }
        }
    }
}
