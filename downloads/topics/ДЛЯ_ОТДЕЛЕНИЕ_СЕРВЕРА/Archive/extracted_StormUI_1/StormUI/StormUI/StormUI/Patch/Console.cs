using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StormUI.Patch
{
    [HarmonyPatch(typeof(ConsoleSystem), "RunFile")]
    public class ConsoleSystem_Patch
    {
        static void Postfix(ConsoleSystem.Option options, string strFile)
        {
            ConVar.Culling.env = false;
            ConVar.Culling.toggle = false;
            ConVar.Graphics.grassshadows = false;
        }
    }
}
