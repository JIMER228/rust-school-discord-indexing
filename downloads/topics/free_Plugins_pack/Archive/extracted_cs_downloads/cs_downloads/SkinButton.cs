using Oxide.Core;
using Oxide.Game.Rust.Cui;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Skin Button", "Malmo", "1.0.0")]
    [Description("Adds a button to lockers to skin all items in the locker.")]
    public class SkinButton : RustPlugin
    {
        #region Fields

        private PluginConfig _config;
        private PluginData _data;
        
        private const string SkinFX = "assets/prefabs/deployable/repair bench/effects/skinchange_spraypaint.prefab";
        private const string SaveFX = "assets/prefabs/weapons/toolgun/effects/attack.prefab";

        private static readonly float[] XOffsets =
        {
            193.5f,
            502.5f,
        };
        
        private static readonly int[] YOffsets = {
            387,
            249,
            109,
        };
        
        private const string UIName = "skinbutton.";
        private static string UiName(string name) => UIName + name;

        #endregion

        #region Oxide Hooks

        private void Init()
        {
            _config = Config.ReadObject<PluginConfig>();

            RegisterCommands();
            RegisterPermissions();
            LoadData();
        }

        private void Unload()
        {
            foreach(var player in BasePlayer.activePlayerList)
            {
                DestroyUI(player);
            }
        }

        private void OnLootEntity(BasePlayer player, Locker entity)
        {
            ShowUI(player, entity);
        }

        private void OnLootEntityEnd(BasePlayer player, Locker entity)
        {
            DestroyUI(player);
        }

        #endregion

        #region Commands

        private void RegisterCommands()
        {
            cmd.AddConsoleCommand("skinbutton.skin", this, nameof(SkinButtonCommand));
            cmd.AddConsoleCommand("skinbutton.save", this, nameof(SaveButtonCommand));
        }

        private void SkinButtonCommand(ConsoleSystem.Arg arg)
        {
            int index;
            Locker locker;
            BasePlayer player;
            
            if(!TryParseLocker(arg, out locker, out index, out player)) return;
            
            var items = GetLockerInventory(locker, index);
            if (!items.Any()) return;

            var playerSkins = GetPlayerSkins(player);
            if (playerSkins == null) return;
            
            foreach (var item in items)
            {
                var pos = item.position;
                
                if (item.info == null || !item.info.HasSkins)
                {
                    continue;
                }

                if (!playerSkins.ContainsKey(item.info.shortname)) continue;
                
                var preferredSkin = playerSkins[item.info.shortname];
                if (item.skin == preferredSkin) continue;
                
                item.skin = preferredSkin;
                
                if(item.GetHeldEntity() != null)
                {
                    item.GetHeldEntity().skinID = preferredSkin;
                }
                
                if (item.GetWorldEntity() != null)
                {
                    item.GetWorldEntity().skinID = preferredSkin;
                }
                
                item.RemoveFromContainer();
                item.MoveToContainer(locker.inventory, pos);
            }
            
            PlaySound(player, SkinFX);
        }
        
        private void SaveButtonCommand(ConsoleSystem.Arg arg)
        {
            int index;
            Locker locker;
            BasePlayer player;
            
            if(!TryParseLocker(arg, out locker, out index, out player)) return;

            var items = GetLockerInventory(locker, index);

            if (!items.Any()) return;
            
            foreach (var item in items)
            {
                if (item == null || item.info == null || !item.info.HasSkins)
                {
                    continue;
                }
                
                SavePlayerSkin(player, item.info.shortname, item.skin);
            }
            
            SaveData();
            
            PlaySound(player, SaveFX);
        }

        private bool TryParseLocker(ConsoleSystem.Arg arg, out Locker locker, out int index, out BasePlayer player)
        {
            index = 0;
            locker = null;
            
            player = arg.Player();
            if (player == null) return false;

            if (!UserHasPermission(player)) return false;

            var lockerId = arg.GetULong(0);
            index = arg.GetInt(1);
            
            var entity = BaseNetworkable.serverEntities.Find(new NetworkableId(lockerId));
            if (entity == null)
            {
                return false;
            }

            locker = entity as Locker;
            return locker != null;
        }

        #endregion

        #region Permissions

        private const string PermissionPrefix = "skinbutton.";

        private void RegisterPermissions()
        {
            permission.RegisterPermission(PermissionPrefix + "use", this);
        }
        
        private bool UserHasPermission(BasePlayer player) =>
            permission.UserHasPermission(player.UserIDString, PermissionPrefix + "use");

        #endregion

        #region Helper Methods

        private static List<Item> GetLockerInventory(Locker locker, int num)
        {
            var totalSlots = locker.inventorySlots;
            var slotsEach = totalSlots / 3;
            
            var items = new List<Item>();
            
            for (var i = num * slotsEach; i < (num + 1) * slotsEach; i++)
            {
                var item = locker.inventory.GetSlot(i);
                
                if (item == null)
                {
                    continue;
                }
                
                items.Add(item);
            }
            
            return items;
        }
        
        private void PlaySound(BasePlayer player, string sound)
        {
            EffectNetwork.Send(
                new Effect(sound, player, 0, new Vector3(),
                    new Vector3()), player.Connection);
        }

        #endregion
        
        #region UI

        private static void DestroyUI(BasePlayer player)
        {
            for (var i = 0; i < 3; i++)
            {
                for (var j = 0; j < 2; j++)
                {
                    CuiHelper.DestroyUi(player, UiName("root" + i + j));
                }
            }
        }
        
        private void ShowUI(BasePlayer player, BaseNetworkable locker)
        {
            DestroyUI(player);

            if (!UserHasPermission(player)) return;
            
            var container = new CuiElementContainer();

            for (var i = 0; i < 3; i++)
            {
                for (var j = 0; j < 2; j++)
                {
                    var action = j == 0 ? "skin" : "save";
                    var buttonConfig = action == "skin" ? _config.SkinButton : _config.SaveButton;
                    
                    container.Add(new CuiPanel
                    {
                        Image =
                        {
                            Color = "0 0 0 0"
                        },
                        RectTransform =
                        {
                            AnchorMin = "0.5 0",
                            AnchorMax = "0.5 0",
                            OffsetMin = $"{XOffsets[j]} {YOffsets[i]}",
                            OffsetMax = $"{XOffsets[j]} {YOffsets[i]}",
                        },
                        CursorEnabled = false,
                    }, "Overlay", UiName("root" + i + j));

                    container.Add(new CuiButton
                    {
                        Button =
                        {
                            Command = $"skinbutton.{action} {locker.net.ID.Value} {i}",
                            Color = buttonConfig.Color
                        },
                        RectTransform =
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "0 0",
                            OffsetMin = "0 0",
                            OffsetMax = "70 20"
                        },
                        Text =
                        {
                            Text = action == "skin" ? GetMessage(MessageType.SkinButton, player) : GetMessage(MessageType.SaveButton, player),
                            Align = TextAnchor.MiddleCenter,
                            FontSize = 12,
                            Color = buttonConfig.TextColor
                        }
                    }, UiName("root" + i + j), UiName("button" + i + j));
                }
            }

            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Config

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(GetDefaultConfig(), true);
        }
        
        private static PluginConfig GetDefaultConfig()
        {
            return new PluginConfig();
        }

        private class PluginConfig
        {
            [JsonProperty("Button: Skin")] public ButtonConfig SkinButton { get; set; } = new ButtonConfig()
            {
                Color = "1 0.7 0.3 0.5",
                TextColor = "1 0.8 0.3 1"
            };

            [JsonProperty("Button: Save")]
            public ButtonConfig SaveButton { get; set; } = new ButtonConfig()
            {
                Color = "0.3 0.7 1 0.5",
                TextColor = "0.6 0.8 1 1"
            };
        }

        private class ButtonConfig
        {
            [JsonProperty("Color")]
            public string Color { get; set; } = "1 0 0 0.5";
            
            [JsonProperty("Text Color")]
            public string TextColor { get; set; } = "1 1 1 1";
        }

        #endregion

        #region Lang

        private enum MessageType
        {
            SkinButton,
            SaveButton,
        }

        private readonly Hash<object, string> _langMessages = new Hash<object, string>
        {
            [MessageType.SkinButton] = "Skin",
            [MessageType.SaveButton] = "Save",
        };
        
        protected override void LoadDefaultMessages()
        {
            var dictionary = new Dictionary<string, string>();
            foreach (var pair in _langMessages)
            {
                var key = pair.Key.ToString();
                var value = pair.Value;
                dictionary.Add(key, value);
            }

            lang.RegisterMessages(dictionary, this);
        }
        
        private string GetMessage(MessageType messageKey, BasePlayer player, params object[] args)
        {
            return GetMessage(messageKey, player.UserIDString, args);
        }

        private string GetMessage(MessageType messageKey, string playerID, params object[] args)
        {
            return string.Format(lang.GetMessage(messageKey.ToString(), this, playerID), args);
        }

        #endregion

        #region Data Methods

        private void SavePlayerSkin(BasePlayer player, string shortname, ulong skinId)
        {
            var skins = GetPlayerSkins(player);
            if (skins.ContainsKey(shortname))
            {
                skins[shortname] = skinId;
            }
            else
            {
                skins.Add(shortname, skinId);
            }
        }
        
        private Hash<string, ulong> GetPlayerSkins(BasePlayer player, bool createIfNotExists = true)
        {
            Hash<string, ulong> skins;
            
            if (_data.PlayerSkins.TryGetValue(player.userID, out skins))
            {
                return skins;
            }

            if (!createIfNotExists)
            {
                return null;
            }

            skins = new Hash<string, ulong>();
            _data.PlayerSkins.Add(player.userID, skins);
            return skins;
        }

        #endregion
        
        #region Data

        private void LoadData()
        {
            _data = Interface.Oxide.DataFileSystem.ReadObject<PluginData>(nameof(SkinButton));
            
            if (_data?.PlayerSkins == null)
            {
                _data = new PluginData();
            }
        }
        
        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(nameof(SkinButton), _data);
        }
        
        private class PluginData
        {
            public Hash<ulong, Hash<string, ulong>> PlayerSkins { get; set; } = new Hash<ulong, Hash<string, ulong>>();
        }

        #endregion

    }
}
