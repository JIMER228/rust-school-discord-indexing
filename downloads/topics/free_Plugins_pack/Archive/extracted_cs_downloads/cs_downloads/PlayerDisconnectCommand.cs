using System;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("PlayerDisconnectCommand", "YourName", "1.0.2")]
    [Description("Executes a console command when a player disconnects")]
    public class PlayerDisconnectCommand : RustPlugin
    {
        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            // اجرای دستور با تاخیر برای جلوگیری از خطای تغییر مجموعه در حین شمارش
            timer.Once(0.1f, () => {
                Puts("Executing command: server.secure 0");
                ExecuteConsoleCommand("server.secure 0");
            });
        }

        private void ExecuteConsoleCommand(string command)
        {
            ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), command);
        }
    }
}
