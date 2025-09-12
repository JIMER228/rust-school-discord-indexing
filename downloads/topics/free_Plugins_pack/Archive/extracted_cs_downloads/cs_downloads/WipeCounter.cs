/*___    ¦¦¦_ _¦¦¦¦  _¦¦¦¦  ___¦¦¯¯¯¦¦¦¦¦¦___¦¦¦¦¦¦
¦¦¦¦¦¦_ ¦¦¦¦¯¦¯ ¦¦¦ ¦¦¦ ¯¦¦   ¦¦¦   ¦¦   ¯¦  ¦¦¦ ¦¦
¦¦¦¦ _¦¦¦¦¦    ¦¦¦¦¦¦¦¦___¦   ¦¦¦   ¦¦¦¦  ¦ ¦¦¦¦ ¦¦
¦¦¦¦¦¯  ¦¦¦    ¦¦¦ ¦¦¦  ¦¦¦¦¦¦_¦¦¦  ¦¦¦  _¦ ¦¦¦¦ ¦ 
¦¦¦  ¯¦¦¦¦¦¦   ¦¦¦¦¦¦¦¦¦¦¯¦ ¦¦¦¦¦   ¦¦¦¦¦¦¦ ¦¦¦¦ ¦ 
¦¦¦¦¦¦¯¦¦ ¦¦   ¦  ¦ ¦¦   ¦  ¦¦¦¦¦   ¦¦ ¦¦ ¦ ¦ ¦¦   
¦¦¦   ¦ ¦  ¦      ¦  ¦   ¦  ¦ ¦¦¦    ¦ ¦  ¦   ¦    
 ¦    ¦ ¦      ¦   ¦ ¦   ¦  ¦ ¦ ¦      ¦    ¦      
 ¦             ¦         ¦  ¦   ¦      ¦  */
using HarmonyLib;
using Oxide.Core.Plugins;
using System;
namespace Oxide.Plugins
{
    [Info("WipeCounter", "bmgjet", "1.1.0")]
    [Description("Shows count down to wipe in server list")]
    public class WipeCounter : RustPlugin
    {
        [AutoPatch]
        [HarmonyPatch(typeof(World), "GetServerBrowserMapName")]
        internal class World_GetServerBrowserMapName
        {
            [HarmonyPrefix]
            static bool Prefix(ref string __result)
            {
                try
                {
                    DateTimeOffset dateTimeOffset = DateTimeOffset.UtcNow.AddDays((double)WipeTimer.daysToAddTest).AddHours((double)WipeTimer.hoursToAddTest);
                    TimeSpan timeSpan = WipeTimer.serverinstance.GetWipeTime(dateTimeOffset) - dateTimeOffset;
                    __result = string.Format("Wipe in: {0}d {1}h", timeSpan.Days, timeSpan.Hours);
                    return false;
                }
                catch { }
                return true;
            }
        }
    }
}