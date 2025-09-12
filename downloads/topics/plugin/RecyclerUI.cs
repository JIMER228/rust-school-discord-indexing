using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using Newtonsoft.Json;

namespace Carbon.Plugins
{
    [Info("RecyclerUI", "DeepSeekR1", "1.0.8")]
    [Description("Opens recycler UI with a chat command")]
    public class RecyclerUI : CarbonPlugin
    {
        private const string
            AdminPermission = "recyclerui.admin",
            UsePermission = "recyclerui.use",
            CooldownBypassPermission = "recyclerui.bypass";

        private readonly Dictionary<ulong, (Recycler recycler, BasePlayer player)> _recyclers = new();
        private readonly Dictionary<string, long> _cooldowns = new();
        private ConfigData _config = new();

        #region Configuration

        private sealed class ConfigData
        {
            [JsonProperty("Settings")]
            public SettingsData Settings = new();

            public sealed class SettingsData
            {
                [JsonProperty("Cooldown (in minutes)")]
                public float Cooldown = 5.0f;

                [JsonProperty("Maximum Radiation")]
                public float RadiationMax = 1.0f;

                [JsonProperty("Refund Ratio")]
                public float RefundRatio = 0.5f;

                [JsonProperty("Allowed In Safe Zones")]
                public bool AllowedInSafeZones = true;

                [JsonProperty("Instant Recycling")]
                public bool InstantRecycling;

                [JsonProperty("Command To Open Recycler")]
                public string RecycleCommand = "rec";

                [JsonProperty("Recyclable Types")]
                public List<string> RecyclableTypes = new()
                {
                    "Ammunition", "Attire", "Common", "Component", "Construction",
                    "Electrical", "Fun", "Items", "Medical", "Misc", "Tool", "Traps", "Weapon"
                };

                [JsonProperty("Blacklisted Items")]
                public List<string> Blacklist = new();
            }
        }

        protected override void LoadDefaultConfig()
        {
            _config = new ConfigData();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<ConfigData>();
                if (_config == null)
                {
                    LoadDefaultConfig();
                }

                SaveConfig();
            }
            catch (Exception ex)
            {
                PrintWarning((object)$"Configuration error: {ex.Message}");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        #endregion Configuration

        #region Initialization

        private void Init()
        {
            // Register permissions
            if (!permission.PermissionExists(AdminPermission))
            {
                permission.RegisterPermission(AdminPermission, this);
            }

            if (!permission.PermissionExists(UsePermission))
            {
                permission.RegisterPermission(UsePermission, this);
            }

            if (!permission.PermissionExists(CooldownBypassPermission))
            {
                permission.RegisterPermission(CooldownBypassPermission, this);
            }
        }

        private void Unload()
        {
            DestroyAllRecyclers();
        }

        #endregion Initialization

        #region Commands

        [ChatCommand("rec")]
        private void RecyclerCommand(BasePlayer player, string command, string[] args)
        {
            if (args.Length > 0 && args[0].Equals("reloadconfig", StringComparison.OrdinalIgnoreCase) && HasAdminPermission(player))
            {
                LoadConfig();
                player.ChatMessage(GetMessage("Reloaded", player.UserIDString));
                return;
            }

            if (!CanPlayerOpenRecycler(player))
            {
                return;
            }

            OpenRecycler(player);

            if (_config.Settings.Cooldown > 0 && !permission.UserHasPermission(player.UserIDString, CooldownBypassPermission))
            {
                _cooldowns[player.UserIDString] = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + (long)(_config.Settings.Cooldown * 60);
            }
        }

        [ChatCommand("purgerecyclers")]
        private void PurgeRecyclersCommand(BasePlayer player, string command, string[] args)
        {
            if (!HasAdminPermission(player))
            {
                player.ChatMessage(GetMessage("NoPermission", player.UserIDString));
                return;
            }

            DestroyAllRecyclers();
            player.ChatMessage(GetMessage("RecyclersDestroyed", player.UserIDString));
        }

        #endregion Commands

        #region Recycler Management

        private void OpenRecycler(BasePlayer player)
        {
            if (player == null)
            {
                return;
            }

            DestroyRecycler(player);
            CreateRecycler(player);
        }

        private void CreateRecycler(BasePlayer player)
        {
            Recycler? recycler = GameManager.server.CreateEntity("assets/bundled/prefabs/static/recycler_static.prefab",
                player.transform.position.WithY(-5f)) as Recycler;

            if (recycler == null)
            {
                player.ChatMessage(GetMessage("RecyclerError", player.UserIDString));
                return;
            }

            recycler.enableSaving = false;
            recycler.Spawn();
            recycler.GetComponent<BaseEntity>().EnableSaving(false);

            if (!recycler.IsValid())
            {
                recycler.Kill();
                return;
            }

            recycler.SetFlag(BaseEntity.Flags.Reserved8, true);
            recycler.pickup.enabled = false;
            recycler.skinID = 1337;

            _ = player.inventory.loot.StartLootingEntity(recycler, false);
            player.inventory.loot.AddContainer(recycler.inventory);
            player.inventory.loot.SendImmediate();

            SendRPCMessage(player, recycler);

            _recyclers.Add(recycler.net.ID.Value, (recycler, player));
        }

        private void SendRPCMessage(BasePlayer player, Recycler recycler)
        {
            if (player.IsConnected)
            {
                player.SendNetworkUpdateImmediate();
                recycler.SendNetworkUpdate();
                player.inventory.loot.SendImmediate();
            }
        }

        private void DestroyRecycler(BasePlayer player)
        {
            foreach (KeyValuePair<ulong, (Recycler recycler, BasePlayer player)> entry in _recyclers)
            {
                if (entry.Value.player.userID == player.userID && entry.Value.recycler.IsValid())
                {
                    entry.Value.recycler.Kill();
                    _ = _recyclers.Remove(entry.Key);
                    break;
                }
            }
        }

        private void DestroyAllRecyclers()
        {
            foreach (KeyValuePair<ulong, (Recycler recycler, BasePlayer player)> entry in _recyclers.Where(entry => entry.Value.recycler.IsValid()))
            {
                entry.Value.recycler.Kill();
            }
            _recyclers.Clear();
        }

        #endregion Recycler Management

        #region Hooks

        private void OnLootEntityEnd(BasePlayer player, Recycler recycler)
        {
            if (player != null)
            {
                DestroyRecycler(player);
            }
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player != null)
            {
                DestroyRecycler(player);
            }
        }

        private bool? CanAcceptItem(ItemContainer container, Item item, int targetPos)
        {
            if (item?.info == null)
            {
                return null;
            }

            if (container?.entityOwner is not Recycler recycler || !IsRecycleBox(recycler))
            {
                return null;
            }

            if (targetPos < 6)
            {
                string type = item.info.category.ToString();
                if (!_config.Settings.RecyclableTypes.Contains(type) || _config.Settings.Blacklist.Contains(item.info.shortname))
                {
                    return false;
                }

                if (_config.Settings.InstantRecycling)
                {
                    _ = timer.Once(0.0625f, () =>
                    {
                        if (!recycler.IsOn())
                        {
                            recycler.InvokeRepeating(recycler.RecycleThink, 0.0625f, 0.0625f);
                            recycler.SetFlag(BaseEntity.Flags.On, true);
                            recycler.SendNetworkUpdateImmediate();
                        }
                    });
                }
            }

            return null;
        }

        #endregion Hooks

        #region Helpers

        private bool HasAdminPermission(BasePlayer player)
        {
            return permission.UserHasPermission(player.UserIDString, AdminPermission);
        }

        private bool IsRecycleBox(BaseNetworkable entity)
        {
            return entity.IsValid() && _recyclers.ContainsKey(entity.net.ID.Value);
        }

        private bool IsOnCooldown(BasePlayer player)
        {
            if (!_cooldowns.TryGetValue(player.UserIDString, out long cooldownTime))
            {
                return false;
            }

            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= cooldownTime)
            {
                _ = _cooldowns.Remove(player.UserIDString);
                return false;
            }

            return true;
        }

        private bool CanPlayerOpenRecycler(BasePlayer player)
        {
            if (player?.IsConnected != true)
            {
                return false;
            }

            if (!permission.UserHasPermission(player.UserIDString, UsePermission))
            {
                player.ChatMessage(GetMessage("NoPermission", player.UserIDString));
                return false;
            }

            if (IsOnCooldown(player))
            {
                player.ChatMessage(GetMessage("Cooldown", player.UserIDString));
                return false;
            }

            if (player.IsWounded())
            {
                player.ChatMessage(GetMessage("Wounded", player.UserIDString));
                return false;
            }

            if (_config.Settings.RadiationMax > 0 && player.radiationLevel > _config.Settings.RadiationMax)
            {
                player.ChatMessage(GetMessage("Radiation", player.UserIDString));
                return false;
            }

            if (!_config.Settings.AllowedInSafeZones && player.InSafeZone())
            {
                player.ChatMessage(GetMessage("SafeZone", player.UserIDString));
                return false;
            }

            return true;
        }

        #endregion Helpers

        #region Localization

        private string GetMessage(string key, string userId)
        {
            return lang.GetMessage(key, this, userId);
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPermission"] = "You don't have permission to use this command!",
                ["RecyclerError"] = "Failed to create recycler interface!",
                ["Reloaded"] = "Configuration has been reloaded!",
                ["RecyclersDestroyed"] = "All recyclers have been destroyed!",
                ["Cooldown"] = "You must wait before using the recycler again!",
                ["Wounded"] = "You cannot use the recycler while wounded!",
                ["Radiation"] = "You cannot use the recycler with this much radiation!",
                ["SafeZone"] = "You cannot use the recycler in a safe zone!"
            }, this);
        }

        #endregion Localization
    }
}