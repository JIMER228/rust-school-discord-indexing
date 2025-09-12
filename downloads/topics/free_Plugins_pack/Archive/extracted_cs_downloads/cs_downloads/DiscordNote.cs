/*
 * Discord: termin.77
 * Telegram: @termin_77
 * Version: 1.0.0
 * Last Update: 2024/03/19
*/

using Oxide.Core;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using UnityEngine;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("Discord Note", "termin", "1.0.0")]
    [Description("Gives players a note with Discord link")]
    
    public class DiscordNote : RustPlugin
    {
        private const string DISCORD_LINK = "DiscordLink:  https://discord.gg/uX2tnHTUew";
        private const string IMAGE_URL = "https://s6.uupload.ir/files/e5d0b9e74328b8ce389bd8dd7f4e56b0_nmi.png";
        private const float COOLDOWN_MINUTES = 5f;
        
        private Dictionary<ulong, DateTime> cooldowns = new Dictionary<ulong, DateTime>();
        
        [PluginReference]
        private Plugin ImageLibrary;

        void OnServerInitialized()
        {
            if (!ImageLibrary)
            {
                PrintError("ImageLibrary is not loaded! Please install it from uMod.");
                return;
            }

            if (ImageLibrary.Call<bool>("HasImage", "discord_note") == false)
            {
                ImageLibrary.Call("AddImage", IMAGE_URL, "discord_note");
                Puts("Discord note image added to ImageLibrary!");
            }
        }

        [ChatCommand("discord")]
        private void DiscordCommand(BasePlayer player, string command, string[] args)
        {
            if (IsOnCooldown(player))
            {
                var timeLeft = GetCooldownTimeLeft(player);
                SendMessage(player, $"Please wait {timeLeft} minutes before using this command again!");
                return;
            }

            var note = ItemManager.CreateByName("note", 1);
            if (note == null) return;

            if (ImageLibrary != null)
            {
                ulong skinId = ImageLibrary.Call<ulong>("GetImage", "discord_note");
                if (skinId != 0)
                {
                    note.skin = skinId;
                }
            }

            note.text = DISCORD_LINK;
            note.MarkDirty();
            
            if (!player.inventory.GiveItem(note))
            {
                note.Remove();
                return;
            }

            SetCooldown(player);
            CreateInfoPopup(player);
            
            timer.Once(5f, () => 
            {
                DestroyUI(player);
            });
        }

        private bool IsOnCooldown(BasePlayer player)
        {
            if (!cooldowns.ContainsKey(player.userID))
                return false;

            return (DateTime.Now - cooldowns[player.userID]).TotalMinutes < COOLDOWN_MINUTES;
        }

        private void SetCooldown(BasePlayer player)
        {
            cooldowns[player.userID] = DateTime.Now;
        }

        private int GetCooldownTimeLeft(BasePlayer player)
        {
            if (!cooldowns.ContainsKey(player.userID))
                return 0;

            var timeLeft = COOLDOWN_MINUTES - (DateTime.Now - cooldowns[player.userID]).TotalMinutes;
            return (int)Math.Ceiling(timeLeft);
        }

        private void SendMessage(BasePlayer player, string message)
        {
            Player.Message(player, message, "Discord Note");
        }

        private void CreateInfoPopup(BasePlayer player)
        {
            var elements = new CuiElementContainer();

            elements.Add(new CuiPanel
            {
                CursorEnabled = false,
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-200 -30", OffsetMax = "200 30" },
                Image = { Color = "0 0 0 0.9" }
            }, "Overlay", "DiscordNote");

            elements.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Text = 
                { 
                    Text = "Check your inventory for Discord link!", 
                    Color = "1 1 1 1", 
                    FontSize = 20, 
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-bold.ttf"
                }
            }, "DiscordNote");

            CuiHelper.AddUi(player, elements);
        }

        private void DestroyUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "DiscordNote");
        }

        void OnServerSave()
        {
            var now = DateTime.Now;
            var keysToRemove = new List<ulong>();
            
            foreach (var kvp in cooldowns)
            {
                if ((now - kvp.Value).TotalMinutes >= COOLDOWN_MINUTES)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                cooldowns.Remove(key);
            }
        }
    }
}