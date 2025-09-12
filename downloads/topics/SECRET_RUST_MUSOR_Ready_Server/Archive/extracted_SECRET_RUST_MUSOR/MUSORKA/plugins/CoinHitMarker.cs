using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("CoinHitMarker", "", "1.0.0")]
    class CoinHitMarker : RustPlugin
    {
        [PluginReference]
        Plugin ImageLibrary;

        static CoinHitMarker ins;
        PluginConfig config;

        public class PluginConfig
        {
            [JsonProperty("Время отображение маркера")]
            public float DrawTime;
            [JsonProperty("Маркер попадания")]
            public MarkerSetting HitUrl;
            [JsonProperty("Маркер ранения")]
            public MarkerSetting WoundUrl;
            [JsonProperty("Маркер смерти")]
            public MarkerSetting DeathUrl;
        }

        public class MarkerSetting
        {
            [JsonProperty("Изображение маркера")]
            public string MarkerUrl;
            [JsonProperty("Размеры маркера (Min)")]
            public string OffsetMin;
            [JsonProperty("Размеры маркера (Max)")]
            public string OffsetMax;
        }

        protected override void LoadDefaultConfig()
        {
            config = new PluginConfig
            {
                DrawTime = 2.0f,
                HitUrl = new MarkerSetting
                {
                    MarkerUrl = "https://i.imgur.com/LXglZUq.png",
                    OffsetMin = "-15 -15",
                    OffsetMax = "15 15"
                },
                WoundUrl = new MarkerSetting
                {
                    MarkerUrl = "https://images-ext-1.discordapp.net/external/W3-iMDxJ7GBtIOJq6FI7AWJIpweSjTSixcpoS-3vYDo/https/i.imgur.com/ZjLZmzu.png",
                    OffsetMin = "-15 -15",
                    OffsetMax = "15 15",
                },
                DeathUrl = new MarkerSetting
                {
                    MarkerUrl = "https://i.imgur.com/F3HZ3b3.png",
                    OffsetMin = "-15 -15",
                    OffsetMax = "15 15",
                }
            };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        private void OnServerInitialized()
        {
            ins = this;
            AddImage(config.HitUrl.MarkerUrl);
            AddImage(config.WoundUrl.MarkerUrl);
            AddImage(config.DeathUrl.MarkerUrl);
            var c = new CuiElementContainer();
            c.Add(new CuiElement()
            {
                Parent = "Hud",
                Name = containerHit,
                Components =
                {
                    new CuiRawImageComponent{Png = GetImage(config.HitUrl.MarkerUrl), Color = "{color}", FadeIn = 0.2f},
                    new CuiRectTransformComponent{AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = config.HitUrl.OffsetMin, OffsetMax = config.HitUrl.OffsetMax}
                }
            });
            c.Add(new CuiElement()
            {
                Parent = containerHit,
                Name = "Dmg",
                Components =
                {
                    new CuiTextComponent{Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.65", FontSize = 12, Text = "{dmg}", FadeIn = 2.0f },
                    new CuiRectTransformComponent{AnchorMax = "0.5 0", AnchorMin = "0.5 0", OffsetMin = "-105 -25", OffsetMax = "105 -5"}
                }
            });

            hitContainer = c.ToJson();

            c = new CuiElementContainer();
            c.Add(new CuiElement()
            {
                Parent = "Hud",
                Name = containerDeath,
                Components =
                {
                    new CuiRawImageComponent{Png = GetImage(config.WoundUrl.MarkerUrl), Color = "1 1 1 0.65", FadeIn = 0.2f},
                    new CuiRectTransformComponent{AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = config.WoundUrl.OffsetMin, OffsetMax = config.WoundUrl.OffsetMax}
                }
            });
            woundContainer = c.ToJson();

            c = new CuiElementContainer();
            c.Add(new CuiElement()
            {
                Parent = "Hud",
                Name = containerDeath,
                Components =
                {
                    new CuiRawImageComponent{Png = GetImage(config.DeathUrl.MarkerUrl), Color = "{color}", FadeIn = 0.2f},
                    new CuiRectTransformComponent{AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = config.DeathUrl.OffsetMin, OffsetMax = config.DeathUrl.OffsetMax}
                }
            });
            deathContainer = c.ToJson();
        }

        string hitContainer;
        string woundContainer;
        string deathContainer;

        string containerHit = "HitConitaner";
        string containerDeath = "DeathConitaner";

        void AddImage(string url) => ImageLibrary.Call("AddImage", url, url);
        string GetImage(string name) => ImageLibrary.Call<string>("GetImage", name);

        Dictionary<BasePlayer, Timer> hitTimer = new Dictionary<BasePlayer, Timer>();

        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, containerHit);
                CuiHelper.DestroyUi(player, containerDeath);
            }
        }

        void DrawWoundContainer(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, containerDeath);
            CuiHelper.AddUi(player, woundContainer);

            timer.Once(config.DrawTime, () =>
            {
                CuiHelper.DestroyUi(player, containerDeath);
            });
        }

        void DrawDeathContainer(BasePlayer player, bool IsHS)
        {
            CuiHelper.DestroyUi(player, containerDeath);
            var c = deathContainer.Replace("{color}", IsHS ? "0.8 0 0 1" : "1 1 1 0.65");
            CuiHelper.AddUi(player, c);

            timer.Once(config.DrawTime, () =>
            {
                CuiHelper.DestroyUi(player, containerDeath);
            });
        }

        void DrawDamageContainer(BasePlayer player, double dmg, bool isHS, bool isFriend = false, string friendName = "")
        {
            if (player.IsNpc || !player.userID.IsSteamId()) return;
            if (hitTimer.ContainsKey(player))
                hitTimer[player].Destroy();

            CuiHelper.DestroyUi(player, containerHit);
            var container = hitContainer.Replace("{color}", isFriend ? "0 1 0 0.8" : isHS ? "1 0 0 0.5" : "1 1 1 0.5").Replace("{dmg}", isFriend ? friendName : dmg.ToString());
            CuiHelper.AddUi(player, container);

            hitTimer[player] = timer.Once(config.DrawTime, () =>
            {
                CuiHelper.DestroyUi(player, containerHit);
            });
        }

        object OnPlayerWound(BasePlayer player, HitInfo info)
        {
            var attacker = player?.lastAttacker as BasePlayer;
            if (attacker == null) return null;
            DrawWoundContainer(attacker);
            return null;
        }

        //object CanBeWounded(BasePlayer player, HitInfo info)
        //{
        //    if (!player.IsWounded() && !player.IsCrawling())
        //        return true;
        //    else return null;
        //} 

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            var player = entity.ToPlayer();
            if (player == null) return;
            var attacker = info?.Initiator as BasePlayer;
            if (attacker == null) return;
            DrawDeathContainer(attacker, info.isHeadshot);
        }
        [PluginReference]
        Plugin Clans;
        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            var victim = entity.ToPlayer();
            if (victim == null || hitInfo == null) return;
            var attacker = hitInfo.InitiatorPlayer;
            if (attacker == null) return;
            if (attacker == victim) return;

            var isHS = hitInfo.isHeadshot;

            var isMate = (bool)(Clans?.Call("HasFriend", attacker.userID, victim.userID) ?? false) || (victim.Team != null && victim.Team.members.Contains(attacker.userID)) || (attacker.Team != null && attacker.Team.members.Contains(victim.userID));

            NextTick(() =>
            {
                if (victim.IsDead() || victim.IsWounded() || victim.IsCrawling()) return;

                var damage = Math.Ceiling(hitInfo.damageTypes.Total());
                DrawDamageContainer(attacker, damage, isHS, isMate, victim.displayName);

            });
        }

    }
}
