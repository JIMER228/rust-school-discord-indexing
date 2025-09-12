using HarmonyLib;
using Rust.UI;
using StormUI.UI;
using UnityEngine;

namespace StormUI.Patch
{
    //[HarmonyPatch(typeof(LoadingScreen), "CancelLoading")] // patch to disable button "disconnect"
    #region LoadingScreeen CancelLoading
    class LoadingScreen_CancelLoading_Patch
    {
        static bool Prefix()
        {
            return false;
        }
    }
    #endregion

    [HarmonyPatch(typeof(Bootstrap), "Start")] // patch to replace default UI at startup game
    #region Bootstrap Start
    class Bootstrap_Start_Patch
    {
        static void Prefix(Bootstrap __instance)
        {
            InitializeStartImage._background = true; // 
            CanvasGroup canvasGroup = __instance.GetComponentInChildren<CanvasGroup>();
            if (canvasGroup != null) canvasGroup.alpha = 0;
        }
    }
    #endregion

    [HarmonyPatch(typeof(Bootstrap), "LoadingUpdate")] // patch to replace default text at startup game
    #region BootstrapLoadingUpdate
    class Bootstrap_LoadingUpdate_Patch
    {
        static void Prefix(Bootstrap __instance, string str)
        {
            InitializeStartImage._text = str;
            switch (str)
            {
                case "Done":
                    InitializeStartImage._text = string.Empty;
                    break;
            }
        }
    }
    #endregion

    [HarmonyPatch(typeof(MenuBackgroundVideo), "LoadVideoList")]
    class MenuBackgroundVideo_LoadVideoList_Patch
    {
        static void Prefix(MenuBackgroundVideo __instance)
        {
            InitializeStartImage._background = false;
        }
    }
}
