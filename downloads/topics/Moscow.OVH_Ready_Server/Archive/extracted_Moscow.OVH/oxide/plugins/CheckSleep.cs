using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("CheckSleep", "Hougan", "0.0.1")]
    public class CheckSleep : RustPlugin
    {
        [ConsoleCommand("getsleep")]
        private void CmdConsoleCheck(ConsoleSystem.Arg args)
        {
            if (args.Player() != null) return;
            
            PrintError($"Sleepers amount: {BasePlayer.sleepingPlayerList.Count} p.");
        }

        [ConsoleCommand("getcomponent")]
        private void CmdConsoleComp(ConsoleSystem.Arg args)
        {
            if (args.Player() == null) return;
            
            PrintWarning($"Components of {args.Player().displayName}");
            foreach (var check in args.Player().GetComponents(typeof(Component)))
                PrintError(check.GetType().ToString());
        }

        private bool FPSCounter = false;
        private void OnServerInitialized()
        {
            timer.Every(1, () =>
            {
                if (FPSCounter) PrintError($"FPS: {Performance.report.frameRate}");    
            });
        }

        [ConsoleCommand("fpsswitch")]
        private void CmdConsoleSwitch(ConsoleSystem.Arg args) => FPSCounter = !FPSCounter; 

        [ConsoleCommand("getcomponentatall")]
        private void CmdConsoleCompAll(ConsoleSystem.Arg args)
        {
            if (args.Player() != null) return;

            var result = new Dictionary<string, int>();
            foreach (var check in UnityEngine.Object.FindObjectsOfType(typeof(Component)))
            {
                if (!result.ContainsKey(check.GetType().ToString()))
                    result.Add(check.GetType().ToString(), 0);

                result[check.GetType().ToString()]++;
            }

            foreach (var check in result.OrderByDescending(p => p.Value).Where(p => p.Key.Contains("Oxide")))
                PrintError($"{check.Key} -> {check.Value} p.");  
        }
    }
}