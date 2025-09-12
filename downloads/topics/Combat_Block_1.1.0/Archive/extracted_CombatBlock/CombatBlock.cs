using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Plugins;
using Newtonsoft.Json;
using UnityEngine;
using System.Linq;
using Oxide.Core;
using System;

namespace Oxide.Plugins
{
    [Info("CombatBlock", "King", "1.1.0")]
    public class CombatBlock : RustPlugin
    {
        #region [Vars]
        [PluginReference] private Plugin Friends = null;

        private const string Layer = "CombatBlock.Layer";
        private const Boolean LanguageEn = false;
        private static CombatBlock plugin = null;
        #endregion

        #region [Oxide-Api]
		private void Init()
		{
			plugin = this;

			Unsubscribe("OnPlayerDeath");
		}

        private void OnServerInitialized()
        {
            if (config.CombatSettings.UnBlockDeath)
                Subscribe("OnPlayerDeath");
        }

        private void Unload()
        {
			foreach (BasePlayer player in BasePlayer.activePlayerList)
				CuiHelper.DestroyUi(player, Layer);

			Array.ForEach(_components.Values.ToArray(), combat =>
			{
				if (combat != null)
					combat.Kill();
			});

            plugin = null;
            config = null;
        }
        #endregion

        #region [Rust-Api]
        private void OnPlayerAttack(BasePlayer attacker, HitInfo info)
        {
            if (attacker == null || !attacker.userID.IsSteamId() || info == null || info.HitEntity == null) return;

            BasePlayer player = info.HitEntity as BasePlayer;
            if (player == null || !player.userID.IsSteamId() || IsTeammates(attacker.userID, player.userID)) return;

            if (config.CombatSettings.BlockGiveDamage)
                AddCombatBlock(attacker);
            if (config.CombatSettings.BlockRecevingDamage)
                AddCombatBlock(player);
        }

        private void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (player == null || !player.userID.IsSteamId()) return;

			CombatManager combatManager = GetCombatManager(player.userID);
			if (combatManager == null) return;

            combatManager.Kill();
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (player == null || !player.userID.IsSteamId()) return;

			CombatManager combatManager = GetCombatManager(player.userID);
			if (combatManager == null) return;

            combatManager.Kill();
        }

        private object OnServerCommand(ConsoleSystem.Arg args)
        {
            BasePlayer findPlayer = args.Player();
            if (findPlayer == null || GetCombatManager(findPlayer.userID) == null) return null;

            String command = args.cmd.FullName;
            command = command.Insert(0, "/");

            if (config.CombatSettings.BlockCommand.Contains(command.ToLower()))
            {
                String Lang = lang.GetLanguage(findPlayer.UserIDString);
                findPlayer.ChatMessage(Lang == "ru" ? "Данную команду нельзя использовать во время блокировки." : "This command cannot be used during blocking");
                return false;
            }

            return null;
        }

        private object OnUserCommand(IPlayer player, String command, String[] args)
        {
            BasePlayer findPlayer = player.Object as BasePlayer;
            if (findPlayer == null || GetCombatManager(findPlayer.userID) == null) return null;

            command = command.Insert(0, "/");
            if (config.CombatSettings.BlockCommand.Contains(command.ToLower()))
            {
                String Lang = lang.GetLanguage(findPlayer.UserIDString);
                findPlayer.ChatMessage(Lang == "ru" ? "Данную команду нельзя использовать во время блокировки." : "This command cannot be used during blocking");
                return false;
            }

            return null;
        }
        #endregion

        #region [Component]
		private readonly Dictionary<UInt64, CombatManager> _components = new Dictionary<UInt64, CombatManager>();

        private class CombatManager : FacepunchBehaviour
        {
            #region [Fields]
            private BasePlayer combatPlayer;

            private Boolean startedCombat;

            private Single startTime;

            private Single cooldownTime;
            #endregion

            #region [Init]
			private void Awake()
			{
				combatPlayer = GetComponent<BasePlayer>();

				plugin._components[combatPlayer.userID] = this;

                enabled = false;
			}

			public void Init()
			{
                startTime = Time.time;

                cooldownTime = config.CombatSettings.Duration;

                MainUi();

				enabled = true;

				startedCombat = true;
			}
            #endregion

            #region [Interface]
            private void MainUi()
            {
                CuiElementContainer container = new CuiElementContainer();

				container.Add(new CuiPanel
				{
					RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0" },
					Image = { Color = "0 0 0 0" }
				}, "Hud", Layer);

                container.Add(new CuiPanel
                {
                    Image = { Color = "1 0.96 0.88 0.15" },
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "-199.5 82", OffsetMax = "179.5 102" }
                }, Layer, Layer + ".Main");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Text = { Text = GetText(plugin.lang.GetLanguage(combatPlayer.UserIDString)), Font = "robotocondensed-regular.ttf", Color = "1 1 1 0.75", FontSize = 14, Align = TextAnchor.MiddleCenter }
                }, Layer + ".Main", Layer + ".Main" + ".Text");

                Single pTime = 1.0f - ((Time.time - startTime) / cooldownTime);
                if (pTime > 0)
                {
					container.Add(new CuiPanel
					{
						RectTransform = { AnchorMin = $"0 0", AnchorMax = $"{Mathf.Min(pTime, 1f)} 0.075" },
						Image = { Color = "0.438 0.572 0.182 1", Sprite = "assets/content/ui/ui.background.transparent.linearltr.tga" }
					}, Layer + ".Main", Layer + ".Main" + ".Line");
                }

				CuiHelper.DestroyUi(combatPlayer, Layer);
				CuiHelper.AddUi(combatPlayer, container);
            }

            private void UpdateUI()
            {
                CuiElementContainer container = new CuiElementContainer();

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Text = { Text = GetText(plugin.lang.GetLanguage(combatPlayer.UserIDString)), Font = "robotocondensed-regular.ttf", Color = "1 1 1 0.75", FontSize = 14, Align = TextAnchor.MiddleCenter }
                }, Layer + ".Main", Layer + ".Main" + ".Text");

                Single pTime = 1.0f - ((Time.time - startTime) / cooldownTime);
                if (pTime > 0)
                {
					container.Add(new CuiPanel
					{
						RectTransform = { AnchorMin = $"0 0", AnchorMax = $"{Mathf.Min(pTime, 1f)} 0.075" },
						Image = { Color = "0.438 0.572 0.182 1", Sprite = "assets/content/ui/ui.background.transparent.linearltr.tga" }
					}, Layer + ".Main", Layer + ".Main" + ".Line");
                }

                CuiHelper.DestroyUi(combatPlayer, Layer + ".Main" + ".Text");
				CuiHelper.DestroyUi(combatPlayer, Layer + ".Main" + ".Line");
				CuiHelper.AddUi(combatPlayer, container);
            }
            #endregion

            #region [Update]
			private void FixedUpdate()
			{
				if (!startedCombat) return;

				Single CurrentTime = Time.time - startTime;
				if (CurrentTime > cooldownTime)
				{
					Kill();
					return;
				}

				UpdateUI();
			}
            #endregion

            #region [Utils]
            public void UpdateCombatBlock() =>
                startTime = Time.time;

			private Int32 GetCombatTime() =>
                Mathf.RoundToInt(startTime + cooldownTime - Time.time);

            private String GetText(String language = "ru")
            {
                String result = String.Empty;

                switch (language)
                {
                    case "ru":
                    {
                        result += $"Блокировка: {Format(GetCombatTime(), " секунд", " секунды", " секунду")}";
                        break;
                    }
                    default:
                    {
                        result += $"Blocking: {Format(GetCombatTime(), " seconds", " seconds", " second")}";
                        break;
                    }
                }
                
                return result; 
            }

            private static String Format(Int32 units, String form1, String form2, String form3)
            {
                var tmp = units % 10;

                if (units >= 5 && units <= 20 || tmp >= 5 && tmp <= 9)
                    return $"{units}{form1}";

                if (tmp >= 2 && tmp <= 4)
                    return $"{units}{form2}";

                return $"{units}{form3}";
            }
            #endregion

            #region [Destroy]
			private void OnDestroy()
			{
				CancelInvoke();

				CuiHelper.DestroyUi(combatPlayer, Layer);

				plugin?._components.Remove(combatPlayer.userID);

				Destroy(this);
			}

			public void Kill()
			{
				enabled = false;

				startedCombat = false;

				DestroyImmediate(this);
			}
            #endregion
        }
        #endregion

        #region [Utils]
		private Boolean IsTeammates(UInt64 playerID, UInt64 friendID) =>
			playerID == friendID || 
            RelationshipManager.ServerInstance.FindPlayersTeam(playerID)?.members?.Contains(friendID) == true || 
            Friends != null && Friends.Call<Boolean>("IsFriend", playerID, friendID);

		private CombatManager GetCombatManager(UInt64 playerID)
		{
			CombatManager combatManager;
			return _components.TryGetValue(playerID, out combatManager) ? combatManager : null;
		}

        private void AddCombatBlock(BasePlayer player)
        {
            if (player == null || !player.IsConnected) return;

            CombatManager combatManager;
            combatManager = GetCombatManager(player.userID);
            if (combatManager != null)
            {
                combatManager.UpdateCombatBlock();
                return;
            }

            combatManager = player.gameObject.AddComponent<CombatManager>();
            combatManager.Init();
        }
        #endregion

        #region [Api]
		private Boolean HasCombatBlock(UInt64 playerID)
		{
			if (_components.ContainsKey(playerID))
                return true;

            return false;
		}
        #endregion

        #region [Config]
        private static PluginConfig config;

        protected override void LoadDefaultConfig()
        {
            config = PluginConfig.DefaultConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();

            if (config.PluginVersion < Version)
                UpdateConfigValues();

            Config.WriteObject(config, true);
        }

        private void UpdateConfigValues()
        {
            PluginConfig baseConfig = PluginConfig.DefaultConfig();
            if (config.PluginVersion < new VersionNumber(1, 1, 0))
            {
                PrintWarning("Config update detected! Updating config values...");
                PrintWarning("Config update completed!");
            }

            config.PluginVersion = Version;
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        internal class CombatSetting
        {
            [JsonProperty(LanguageEn ? "Blocking duration" : "Длительность блокировки")]
            public Single Duration;

            [JsonProperty(LanguageEn ? "Block when a player is hit ?" : "Блокировать при попадании по игроку ?")]
            public Boolean BlockGiveDamage;

            [JsonProperty(LanguageEn ? "Block when taking damage from a player ?" : "Блокировать при получении урона от игрока ?")]
            public Boolean BlockRecevingDamage;

            [JsonProperty(LanguageEn ? "Remove block after death ?" : "Снимать блокировку при смерти ?")]
            public Boolean UnBlockDeath;

            [JsonProperty(LanguageEn ? "What commands should I block during the combat block ?" : "Какие команды блокировать при комбат блоке ?")]
            public List<String> BlockCommand = new List<String>();
        }
            
        private class PluginConfig
        {
            [JsonProperty(LanguageEn ? "Basic settings" : "Основные настройки")]
            public CombatSetting CombatSettings;

            [JsonProperty(LanguageEn ? "Config version" : "Версия конфигурации")] 
            public VersionNumber PluginVersion = new VersionNumber();

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                 {
                    CombatSettings = new CombatSetting()
                    {
                        Duration = 10f,
                        BlockGiveDamage = true,
                        BlockRecevingDamage = true,
                        UnBlockDeath = true,
                        BlockCommand = new List<String>()
                        {
                            "/tpr",
                            "/tpa",
                            "/home"
                        }
                    },
                    PluginVersion = new VersionNumber(),
                };
            }
        }
        #endregion
    }
}