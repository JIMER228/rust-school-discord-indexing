using System;
using Oxide.Core;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("AutoSecureOff", "TeRMiN", "1.0.0")]
    [Description("Automatically runs a console command every 30 seconds.")]
    public class AutoSecureOff : RustPlugin
    {
        private const string CommandToRun = "server.secure 0"; // دستور مورد نظر
        private const float Interval = 30f; // فاصله زمانی بین اجراها

        void OnServerInitialized()
        {
            timer.Every(Interval, () => ExecuteCommand());
            Puts("AutoConsoleCommand has been initialized and is running every 30 seconds!");
        }

        private void ExecuteCommand()
        {
            ConsoleSystem.Run(ConsoleSystem.Option.Server, CommandToRun);
            Puts($"Executed: {CommandToRun}");
        }
    }
}
