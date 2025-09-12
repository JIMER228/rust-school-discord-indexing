using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Rust.UI;
using StormUI.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace StormUI
{
    [BepInPlugin("storm.ui.mod", "stormrust", "1.0")]
    public class StormMod : BasePlugin
    {
        public static Harmony harmony;
        public override void Load()
        {
            harmony = new Harmony("storm.ui.mod");
            harmony.PatchAll();

            var component = AddComponent<InitializeStartImage>();
            AddComponent<InitializationServerImage>();
            UnityEngine.Object.DontDestroyOnLoad(component.gameObject);
        }
    }
}
