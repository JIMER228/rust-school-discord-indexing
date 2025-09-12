using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("InputKeyDetect", "", "1.0.0")]
    public class InputKeyDetect : RustPlugin
    {
        private List<string> keyName = new List<string> { "Home", "Delete", "Insert", "End","[","]","х","ъ","rightarrow","x" };
        
        private string Permission = "InputKeyDetect.allowed";

        private void Init() => permission.RegisterPermission(Permission, this);
        
        private void OnServerInitialized()
        {
            foreach (var player in BasePlayer.activePlayerList)
                OnPlayerInit(player);
        }
        
        private void OnPlayerInit(BasePlayer player)
        {
            for (int i = 0; i < keyName.Count; i++)
                player.SendConsoleCommand($"bind {keyName[i]} onpress {keyName[i]}");
        }
        
        [ConsoleCommand("onpress")]
        private void cmdOnPress(ConsoleSystem.Arg arg)
        {
            if(arg.connection == null) return;
            var player = arg.Player();
            var nameKey = arg.Args[0];
            
            DetectMessage($"<color=red>{player.displayName}</color><color=#ffffff> нажал подозрительную кнопку</color> <color=red>{nameKey}</color>");
        }

        private void DetectMessage(string message)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if(player.IsAdmin())
                    SendReply(player, message);
            }
        }
    }
}