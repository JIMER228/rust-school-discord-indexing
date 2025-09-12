/*
 * Discord: termin.77
 * Telegram: @termin_77
 * Version: 1.0.0
 * Last Update: 2024/03/19
*/

using Oxide.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Auto Chat", "termin", "1.0.0")]
    [Description("Send sequential messages in chat automatically")]
    
    public class AutoChat : RustPlugin
    {
        private List<string> messages = new List<string>();
        private Timer messageTimer;
        private const float MESSAGE_INTERVAL = 600f;
        private int currentMessageIndex = 0;
        private const string PERMISSION_USE = "autochat.admin";

        private void Init()
        {
            permission.RegisterPermission(PERMISSION_USE, this);
            LoadDefaultMessages();
            StartMessageTimer();
        }

        private void LoadDefaultMessages()
        {
            messages = new List<string>
            {
                @"<color=yellow>[PersianToxic] </color><color=green> :heart: <color=#90D5FF>https://discord.gg/uX2tnHTUew</color> :heart:",
                @"<color=yellow>[PersianToxic]</color><color=green> Dar Soorat Moshahede Hargoone Raftar Mashkook Az</color> <b><color=red>F7 <color=green>Estefade Konid !</color>",
                @"<color=yellow>[PersianToxic]</color><color=green> Baraye Spawn Dadan Minicopter Az <color=red>/mymini</color> Estefade Konid !</color>",
                @"<color=yellow>[PersianToxic]</color><color=green> Baraye Spawn Dadan Horse Az <color=red>/horse</color> Estefade Konid !</color>",
                @"<color=yellow>[PersianToxic]</color><color=green> Baraye Kharid Rank Mitoonid Be <color=red>Discord</color> Morajee Konid! </color> <color=#FFA500>/Discord</color>",
                @"<color=yellow>[PersianToxic]</color><color=green> Baraye Didan Info Server Az <color=red>/info</color> Estefade Konid !</color>",
                @"<color=yellow>[PersianToxic]</color><color=green> Baraye Baz Shodan Panel Kits Az <color=red>/kit</color> Estefade Konid !</color>",
                @"<color=yellow>[PersianToxic]</color><color=green> Baraye On\Off Kardan Crosshair Az <color=red>/hair</color> Estefade Konid !</color>",
                @"<color=yellow>[PersianToxic]</color><color=green> Baraye Avaz Kardan Lebas Zir Khod Az <color=red>/wear</color> Estefade Konid !</color>",
                @"<color=yellow>[PersianToxic]</color><color=green> Baraye Off\On Kardan Skin Lebas Enemy Az <color=red>/noskin</color> Estefade Konid !</color>",
                @"<color=yellow>[PersianToxic]</color><color=green> Baraye Didan Top Players Az <color=red>/top</color> Estefade Konid !</color>",
                @"<color=yellow>[PersianToxic]</color><color=green> Baraye Baz Shodan Panel Clans Az <color=red>/clan</color> Estefade Konid !</color>",
                @"<color=yellow>[PersianToxic]</color><color=green> Baraye Kharid Az Shop Az <color=red>/shop</color> Estefade Konid !</color>",
                @"<color=yellow>[PersianToxic]</color><color=green> Dar Soorat Moshahede Cheater Az Elam Un Name Cheater Dakhel Chat Parhiz Konid !</color>",
                @"<color=yellow>[PersianToxic]</color><color=green> Baraye Kharid <color=red>Offline Raid Protection</color> Az <color=red>TC</color> Eghdam Konid !</color>",
                @"<color=yellow>[PersianToxic]</color><color=green> Dar Soorat Moshahehde Hargoone Bug Shoma Movazafid Un Ro Be Admin Etelaa Bedid !</color>",
                @"<color=yellow>[PersianToxic]</color><color=green> Baraye Taghir Skin Base Khod Az <color=red>/bskin</color> Estefade Konid !</color>",
                @"<color=yellow>[PersianToxic]</color><color=green> Baraye Remove Kardan Door Ya Wall Ya Deployed Items Az <color=red>/remove</color> Estefade Konid !</color>",
                @"<color=yellow>[PersianToxic]</color><color=green>Server 3X PersianToxic: <color=red>3X.PersianTox.iR</color></color>",
                @"<color=yellow>[PersianToxic]</color><color=green> Baraye Dadan Payam Shakhsi Be Yek Player Az <color=red>/pm</color> Estefade Konid !</color>"
            };
        }

        private void StartMessageTimer()
        {
            messageTimer?.Destroy();
            messageTimer = timer.Every(MESSAGE_INTERVAL, () => 
            {
                if (messages.Count > 0)
                {
                    Server.Broadcast(messages[currentMessageIndex]);
                    currentMessageIndex++;
                    if (currentMessageIndex >= messages.Count)
                    {
                        currentMessageIndex = 0;
                    }
                }
            });
        }

        void OnServerShutdown()
        {
            messageTimer?.Destroy();
        }

        [ChatCommand("msg")]
        private void ForceNextMessageCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PERMISSION_USE)) return;
            
            if (messages.Count > 0)
            {
                Server.Broadcast(messages[currentMessageIndex]);
                currentMessageIndex++;
                if (currentMessageIndex >= messages.Count)
                {
                    currentMessageIndex = 0;
                }
                messageTimer?.Destroy();
                StartMessageTimer();
                SendReply(player, "Message sent and timer reset!");
            }
        }

        [ChatCommand("addmsg")]
        private void AddMessageCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PERMISSION_USE)) return;
            
            if (args.Length == 0)
            {
                SendReply(player, "Usage: /addmsg <message>");
                return;
            }

            string newMessage = string.Join(" ", args);
            messages.Add(newMessage);
            SendReply(player, $"Added new message: {newMessage}");
        }

        [ChatCommand("listmsg")]
        private void ListMessagesCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PERMISSION_USE)) return;

            SendReply(player, "Current Messages:");
            for (int i = 0; i < messages.Count; i++)
            {
                string currentIndicator = (i == currentMessageIndex) ? " [NEXT]" : "";
                SendReply(player, $"{i + 1}. {messages[i]}{currentIndicator}");
            }
        }

        [ChatCommand("delmsg")]
        private void DeleteMessageCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PERMISSION_USE)) return;

            if (args.Length == 0 || !int.TryParse(args[0], out int index))
            {
                SendReply(player, "Usage: /delmsg <number>");
                return;
            }

            index--;
            if (index >= 0 && index < messages.Count)
            {
                string deletedMessage = messages[index];
                messages.RemoveAt(index);
                
                if (index <= currentMessageIndex)
                {
                    currentMessageIndex--;
                    if (currentMessageIndex < 0)
                        currentMessageIndex = 0;
                }
                
                SendReply(player, $"Deleted message: {deletedMessage}");
            }
            else
            {
                SendReply(player, "Invalid message number!");
            }
        }

        [ChatCommand("nextmsg")]
        private void ShowNextMessageCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PERMISSION_USE)) return;
            
            if (messages.Count > 0)
            {
                SendReply(player, $"Next message will be ({currentMessageIndex + 1}): {messages[currentMessageIndex]}");
            }
        }
    }
}