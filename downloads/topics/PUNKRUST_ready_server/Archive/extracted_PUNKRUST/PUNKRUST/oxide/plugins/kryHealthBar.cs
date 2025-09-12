using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins 
{
    [Info ("kryHealthbar", "xkrystalll", "1.0.3")]
    class kryHealthBar : RustPlugin 
    {
        #region Classes
        private class kryPlayer : FacepunchBehaviour
        {
            public static Dictionary<BasePlayer, kryPlayer> Players = new Dictionary<BasePlayer, kryPlayer>();
            BasePlayer _player;
            Timer _showTimer;
            bool _displayBar;
            public void Awake()
            {
                var attachedPlayer = GetComponent<BasePlayer>();
                if ( attachedPlayer == null || !attachedPlayer.IsConnected )
                {
                    return;
                }

                _player = attachedPlayer;
                Players[_player] = this;
            }
            public void DamageTrigger(int newHp, BaseEntity target, bool isDead, bool isWounded)
            {
                if (!Instance.IsReady)
                    return;
                if (_showTimer != null)
                {
                    Instance.DestroyUI(_player);
                    DestroyTimer();
                }
                Instance.UI_DrawBar(_player, newHp, target, isDead, isWounded);
                _showTimer = Instance.timer.Once(Instance.cfg.showTime, () => 
                {
                    Instance.DestroyUI(_player);
                    DestroyTimer();
                });
            }
            public void DestroyTimer()
            {
                _showTimer?.Destroy();
                _showTimer = null;
            }
            public void Destroy() => Destroy(this);
        }
        #endregion

        #region Fields
        private List<ulong> _displayBar = new List<ulong>();
        private const string PERM_SHOW = "kryHealthBar.show";
        private const string Layer = "ui.kryHealthBar.bg";
        private List<string> uiComponents;
        private static kryHealthBar Instance;
        private bool IsReady = false;
        private const float FadeOutTime = 0.3f;
        #endregion

        #region Methods
        private void ChangeHealthBar(BasePlayer p)
        {
            if (_displayBar.Contains(p.userID))
            {
                _displayBar.Remove(p.userID);
                p.ChatMessage("Healthbar - <color=green>ON</color>");
            }
            else
            {
                _displayBar.Add(p.userID);
                p.ChatMessage("Healthbar - <color=red>OFF</color>");
            }
        }
        private bool CheckPermissionToDisplay(BasePlayer p, BaseEntity entity)
        {
            if (p == null || entity == null)
                return false;
                
            if (_displayBar.Contains(p.userID) || !p.IPlayer.HasPermission(PERM_SHOW))
                return false;

            bool result = true;

            if (cfg.displaySettings.disableToEveryThing && entity.GetComponent<NPCAutoTurret>() != null && entity.GetComponent<AutoTurret>() != null)
                return false;
            if (!cfg.displaySettings.onBBDamage && entity.GetComponent<BuildingBlock>() != null)
                result = false;
            if (!cfg.displaySettings.onMinicopterDamage && (entity.GetComponent<MiniCopter>() != null || entity.GetComponent<HotAirBalloon>() != null || entity.GetComponent<BaseVehicleModule>() != null || entity.GetComponent<BaseVehicle>() != null))
                result = false;
            if (!cfg.displaySettings.onDeployedDamage  && (entity.GetComponent<Construction>() != null || entity.GetComponent<StorageContainer>() != null || entity.GetComponent<BaseOven>() != null || entity.GetComponent<BaseLadder>() != null || entity.GetComponent<ResearchTable>() != null || entity.GetComponent<MixingTable>() != null || entity.GetComponent<SleepingBag>() != null || entity.GetComponent<AutoTurret>() != null || entity.GetComponent<NPCAutoTurret>() != null || entity.GetComponent<HelicopterTurret>() != null || entity.GetComponent<FlameTurret>() != null))
                result = false;
            if (!cfg.displaySettings.onBotDamage && entity.GetComponent<BaseAnimalNPC>() == null && (entity.GetComponent<ScientistNPC>() != null || entity.GetComponent<BaseNpc>() != null))
                result = false;
            if (!cfg.displaySettings.onPlayerDamage && entity.ToPlayer() != null)
                result = false;
            if (!cfg.displaySettings.onAnimalDamage && (entity.GetComponent<BaseAnimalNPC>() != null || entity.GetComponent<BaseRidableAnimal>() != null))
                result = false;

            return result;
        }
        private void DestroyUI(BasePlayer p) 
        {
            foreach (string item in uiComponents)
                CuiHelper.DestroyUi(p, item);
        }
        private void AddPlayerInfo(BasePlayer p)
        {
            kryPlayer kryPlayer;
            if ( !kryPlayer.Players.TryGetValue(p, out kryPlayer) )
                kryPlayer = p.gameObject.AddComponent<kryPlayer>();
        }
        private kryPlayer GetPlayer(BasePlayer p) => kryPlayer.Players.GetValueOrDefault(p);
        private void DestroyAll<T>() where T : MonoBehaviour
        {
            foreach (var type in UnityEngine.Object.FindObjectsOfType<T>())
                UnityEngine.Object.Destroy(type);
        }
        private static string HexToRustFormat(string hex, float opacity = 1f)
		{
			Color color = Color.black;
			if (!string.IsNullOrEmpty(hex))
			{
				var str = hex.Trim('#');

				var op = byte.Parse(string.Format($"{Mathf.RoundToInt(byte.MaxValue * opacity)}"), NumberStyles.Float);

				var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
				var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
				var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

				if (str.Length == 8)
					op = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);

				color = new Color32(r, g, b, op);
			}
			return $"{color.r:F2} {color.g:F2} {color.b:F2} {color.a:F2}";
		}
        #endregion

        #region Hooks
        void OnServerInitialized()
        {
            Instance = this;
            LoadConfig();
            LoadData();
            BasePlayer.activePlayerList.ToList().ForEach(AddPlayerInfo);
            uiComponents = new List<string>()
            {
                Layer + ".bar.line",
                Layer + ".hp.text",
                Layer + ".plus.image",
                Layer + ".hp.text",
                Layer
            };
            permission.RegisterPermission(PERM_SHOW, this);

            Puts("Plugin was loaded!");
            IsReady = true;
        }
        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            try
            {
                if (info == null || entity == null)
                    return null;

                if (info.InitiatorPlayer == null || !info.InitiatorPlayer.UserIDString.StartsWith("7656119") || info.InitiatorPlayer.IsNpc || !IsReady)
                    return null;

                if (entity.GetEntity().ToPlayer() != null)
                    if (info.InitiatorPlayer.userID == entity.GetEntity().ToPlayer().userID)
                        return null;
                
                if (!CheckPermissionToDisplay(info.InitiatorPlayer, entity.GetEntity()))
                    return null;
                NextTick(() => 
                {
                    if (entity == null)
                    {
                        GetPlayer(info.InitiatorPlayer)?.DamageTrigger(0, null, true, false);
                        return;
                    }
                    GetPlayer(info.InitiatorPlayer)?.DamageTrigger((int)Math.Ceiling(entity.health), entity, entity.IsAlive() ? false : true, entity.ToPlayer() != null ? entity.ToPlayer().IsWounded() : false );
                });
            } 
            catch { return null; }
            return null;
        }
        void Unload()
        {
            Instance = null;
            DestroyAll<kryPlayer>();
            kryPlayer.Players.Clear();
            BasePlayer.activePlayerList.ToList().ForEach(DestroyUI);
            SaveData();
        }
        void OnPlayerConnected(BasePlayer p) => AddPlayerInfo(p);
        #endregion

        #region UI
        private void UI_DrawBar(BasePlayer p, int HP, BaseEntity target, bool isDead, bool isWounded)
        {
            if (p == null || !p.IsConnected)
                return;

            double AnMaxX = 0;
            if (target != null)
                AnMaxX = HP / target.MaxHealth();

            var container = new CuiElementContainer();
            container.Add(new CuiElement() 
            {
                Name = Layer,
                Parent = "Hud",
                FadeOut = FadeOutTime,
                Components = 
                {
                    new CuiImageComponent { Color = HexToRustFormat("#88888837"), FadeIn = FadeOutTime },
                    new CuiRectTransformComponent { AnchorMin = "0.421875 0.11666669", AnchorMax = "0.571875 0.15222237" }
                }
            });
            container.Add(new CuiElement() 
            {
                Name = Layer + ".plus.image",
                Parent = Layer,
                Components = 
                {
                    new CuiImageComponent { Color = HexToRustFormat("#DDAD75"), Sprite = "assets/icons/facepunch.png" },
                    new CuiRectTransformComponent { AnchorMin = "0 0.0549992", AnchorMax = "0.1133337 0.9049962" }
                }
            });
            container.Add(new CuiElement()
            {
                Name = Layer + ".bar.line.bg",
                Parent = Layer,
                FadeOut = FadeOutTime,
                Components =
                {
                    new CuiImageComponent { Color = "0 0 0 0.1" },
                    new CuiRectTransformComponent { AnchorMin = "0.1291676 0.09374949", AnchorMax = "0.97 0.8749962" }
                }
            });
            container.Add(new CuiElement() 
            {
                Name = Layer + ".bar.line",
                Parent = Layer + ".bar.line.bg",
                FadeOut = FadeOutTime,
                Components = 
                {
                    new CuiImageComponent { Color = HexToRustFormat(cfg.healthLineColor) },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = (isDead) ? "0" : (AnMaxX) + $" 0.95" }
                }
            });
            container.Add(new CuiElement() 
            {
                Name = Layer + ".hp.text",
                Parent = Layer,
                Components = 
                {
                    new CuiTextComponent { Text = (isDead) ? "УБИТ" : (isWounded) ? "РАНЕН" : HP.ToString(), FadeIn = 0.24f, FontSize = 14, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleLeft }, 
                    new CuiRectTransformComponent { AnchorMin = "0.1750008 0", AnchorMax = "1 1" }
                }
            });
            CuiHelper.AddUi(p, container);
        }
        #endregion

        #region Config
        private ConfigData cfg;
        public class ConfigData
        {
            [JsonProperty("Time to display health bar")]
            public float showTime;
            [JsonProperty("Health line color (HEX)")]
            public string healthLineColor;
            [JsonProperty("Text color (HEX)")]
            public string textColor;
            [JsonProperty("Display settings")]
            public DisplaySettings displaySettings;

            public class DisplaySettings
            {
                [JsonProperty("Display bar on damage bot?")]
                public bool onBotDamage;
                [JsonProperty("Disable display for everything?")]
                public bool disableToEveryThing;
                [JsonProperty("Display bar on damage building block?")]
                public bool onBBDamage;
                [JsonProperty("Display bar on damage player?")]
                public bool onPlayerDamage;
                [JsonProperty("Display bar on damage minicopter, airballon, etc?")]
                public bool onMinicopterDamage;
                [JsonProperty("Display bar on damage deployed items?")]
                public bool onDeployedDamage;
                [JsonProperty("Display bar on damage animal?")]
                public bool onAnimalDamage;
            } 
        }
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData()
            {
                showTime = 3f,
                healthLineColor = "#AB4040FF",
                textColor = "#FFFFFFC9",
                displaySettings = new ConfigData.DisplaySettings(){ onBotDamage = true, disableToEveryThing = false, onBBDamage = true, onPlayerDamage = true, onMinicopterDamage = true, onDeployedDamage = true, onAnimalDamage = true }
            };
            SaveConfig(config);
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            cfg = Config.ReadObject<ConfigData>();
            SaveConfig(cfg);
        }
        void SaveConfig(object config) => Config.WriteObject(config, true);
        #endregion
    
        #region Data
        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject("noDisplayBar", _displayBar);
        void LoadData()
        { 
            _displayBar = Interface.Oxide?.DataFileSystem?.ReadObject<List<ulong>>("noDisplayBar")
                ?? new List<ulong>();
        }
        #endregion

        #region Commands
        [ChatCommand("healthbar")]
        void cmdSwitchHealthbar(BasePlayer p) 
        {
            if (p == null || !p.IsConnected)
                return;
            if (!p.IPlayer.HasPermission(PERM_SHOW))
                p.ChatMessage($"You <color=red>dont have</color> permission for this.");
            ChangeHealthBar(p);
        }
        #endregion
    }
}