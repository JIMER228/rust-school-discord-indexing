using Oxide.Core.Plugins;
using System.Collections.Generic;
using System.IO;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("StaffChat", "Termin", "1.1.1")]
    [Description("Enables a private chat for staff members with the staffchat.use permission and logs all chat messages.")]
    public class StaffChat : RustPlugin
    {
        private const string permissionName = "staffchat.use";
        private string logFilePath;

        void Init()
        {
            permission.RegisterPermission(permissionName, this);
            Puts("StaffChat plugin has been loaded!");

            // تعیین مسیر فایل لاگ
            logFilePath = Path.Combine(Interface.Oxide.LogDirectory, "StaffChatLog.txt");

            // اطمینان از وجود فایل لاگ
            if (!File.Exists(logFilePath))
            {
                File.Create(logFilePath).Dispose();
            }
        }

        [ChatCommand("ac")]
        void StaffChatCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionName))
            {
                player.ChatMessage("You do not have permission to use this command.");
                return;
            }

            if (args.Length == 0)
            {
                player.ChatMessage("Usage: /ac <message>");
                return;
            }

            string message = string.Join(" ", args);
            SendStaffChatMessage(player.displayName, message);
            LogStaffChatMessage(player.displayName, message);
        }

        void SendStaffChatMessage(string playerName, string message)
        {
            foreach (BasePlayer target in BasePlayer.activePlayerList)
            {
                if (permission.UserHasPermission(target.UserIDString, permissionName))
                {
                    target.ChatMessage($"<color=#ff0000>[Staff Chat]</color> <color=#00ff00>{playerName}:</color> {message}");
                }
            }
        }

        void LogStaffChatMessage(string playerName, string message)
        {
            string logMessage = $"{System.DateTime.Now}: {playerName}: {message}\n";
            File.AppendAllText(logFilePath, logMessage);
        }
    }
}
