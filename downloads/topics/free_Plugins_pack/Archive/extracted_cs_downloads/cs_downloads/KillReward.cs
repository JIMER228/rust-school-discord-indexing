using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Oxide.Plugins
{
    [Info("KillReward", "Termin", "1.3.1")]
    [Description("پخش صدا و نمایش پیام پاداش برای کشتن")]
    public class KillReward : RustPlugin
    {
        [PluginReference]
        private Plugin ServerRewards;

        private const int REWARD_AMOUNT = 2;
        private const float MESSAGE_DURATION = 4f;

        private void OnPlayerDeath(BasePlayer victim, HitInfo info)
        {
            if (victim == null || info == null) return;

            BasePlayer attacker = info.InitiatorPlayer;
            if (attacker == null || attacker == victim) return;

            // چک می‌کنیم که قربانی پلیر باشه نه NPC
            if (!IsValidPlayer(victim)) return;

            // اضافه کردن پوینت
            if (ServerRewards != null)
            {
                ServerRewards.Call("AddPoints", attacker.userID, REWARD_AMOUNT);
            }

            // نمایش پیام روی صفحه
            ShowRewardMessage(attacker);
        }

        // چک می‌کنه که آیا پلیر واقعی هست یا NPC
        private bool IsValidPlayer(BasePlayer player)
        {
            if (player == null) return false;
            
            // چک می‌کنیم که NPC نباشه
            if (player.IsNpc) return false;
            
            // چک می‌کنیم که ساینتیست یا NPC دیگه‌ای نباشه
            if (!player.userID.IsSteamId()) return false;
            
            // چک می‌کنیم که SteamID معتبر داشته باشه
            if (player.UserIDString.Length != 17) return false;

            return true;
        }

        private void ShowRewardMessage(BasePlayer player)
        {
            string panelName = "KillReward_HUD";
            
            var elements = new CuiElementContainer();
            
            elements.Add(new CuiElement
            {
                Parent = "Hud",
                Name = panelName,
                Components = 
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.1",
                        AnchorMax = "0.5 0.1",
                        OffsetMin = "-50 0",
                        OffsetMax = "50 30"
                    },
                    new CuiTextComponent
                    {
                        Text = $"+{REWARD_AMOUNT} RP",
                        FontSize = 20,
                        Align = TextAnchor.MiddleCenter,
                        Color = "0.38 0.96 0.42 1"
                    },
                    new CuiOutlineComponent
                    {
                        Color = "0 0 0 1",
                        Distance = "1 1"
                    }
                }
            });

            CuiHelper.DestroyUi(player, panelName);
            CuiHelper.AddUi(player, elements);

            timer.Once(MESSAGE_DURATION, () =>
            {
                if (player != null && player.IsConnected)
                {
                    CuiHelper.DestroyUi(player, panelName);
                }
            });
        }

        private void OnServerInitialized()
        {
            if (!ServerRewards)
            {
                PrintError("ServerRewards plugin is not loaded! Points will not be awarded.");
            }
        }
    }
}