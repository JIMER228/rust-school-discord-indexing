// Name: Kits
// Documentation: https://gist.github.com/JVCVkrSzVqsfEcwJqk7N/cec76ff33a5653acd3f13418b065190e
// Changelog:
// * [1.1.3] Added new kit options (fadeout, visibility)
// * [1.1.2] Added config option to see all kits (if player don't have permission)
// * [1.1.1] Fixed console command
// * [1.1.0] Some improvements, added multi gui support
// * [1.0.0] Release
// 
// End
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Kits", "Orange", "1.1.32")]
    [Description("Kits with features for your server! Made by Orange#0900")]
    public class Kits : RustPlugin
    {
        #region Vars

        private const string command = "kit";

        private class Kit
        {
            [JsonProperty(PropertyName = "1. Name")]
            public string name;

            [JsonProperty(PropertyName = "2. Display name")]
            public string displayName;

            [JsonProperty(PropertyName = "3. Permission")]
            public string permission;

            [JsonProperty(PropertyName = "4. Cooldown")]
            public int cooldown;

            [JsonProperty(PropertyName = "5. Wipe-block time")]
            public int block;

            [JsonProperty(PropertyName = "6. Icon")]
            public string url;

            [JsonProperty(PropertyName = "7. Description")]
            public string description;

            [JsonProperty(PropertyName = "8. Max uses")]
            public int uses;

            [JsonProperty(PropertyName = "9. Show to players without permission")]
            public bool showWithoutPermission;

            [JsonProperty(PropertyName = "Items:")]
            public List<ExtendedItem> items;
        }

        private class BaseItem
        {
            public string shortname;
            public int amount;
            public ulong skin;
        }

        private class ExtendedItem : BaseItem
        {
            public string container;
            public int position;
            public bool blueprint;
            public string displayName;
            public Dictionary<string, int> contents;
        }

        #endregion
        
        #region Image Library

        [PluginReference] private Plugin ImageLibrary, OneVSOne;

        public bool HasImage(string imageName, ulong imageId) => (bool)ImageLibrary.Call("HasImage", imageName, imageId);
        public bool Download(string url, string shortname, ulong skin = 0) => (bool)ImageLibrary?.Call("AddImage", url, shortname, skin);
        private void AddImage(string shortname, string url)
        {
            if (!HasImage(shortname, 0))
            {
                Download(url, shortname);
                timer.Once(1f, () => AddImage(url, shortname));
            }
        }

        private string GetImage(string name)
        {
            return ImageLibrary?.Call<string>("GetImage", name);
        }

        #endregion

        #region Oxide Hooks

        private void OnServerInitialized()
        {
            LoadData();
            lang.RegisterMessages(EN, this);
            
            cmd.AddChatCommand(command, this, "Command");
            cmd.AddConsoleCommand(command, this, "Command");

            if (!permission.PermissionExists("kits.unknown"))
            {
                permission.RegisterPermission("Kits.Unknown", this);
            }
            
            AddImage("DeanoMax", "https://i.imgur.com/BrYlSnb.png");
            
            foreach (var kit in config.kits)
            {
                if (!permission.PermissionExists(kit.permission))
                {
                    permission.RegisterPermission(kit.permission, this);
                }

                if(!string.IsNullOrEmpty(kit.url))AddImage(kit.name, kit.url);
            }

            foreach (var item in config.kits.SelectMany(x => x.items).Distinct())
            {
                var name = item.shortname;
                AddImage(name, $"https://rustlabs.com/img/items180/{name}.png");
            }

            if (!config.useAutoKits)
            {
                Unsubscribe("OnPlayerRespawned");
            }
        }

        private void Unload()
        {
            SaveData();
        }

        private void OnNewSave()
        {
            WipeData();
        }
        
        private void OnPlayerRespawned(BasePlayer player)
        {
            GiveAutoKit(player);
        }

        #endregion

        #region Config

        private static ConfigData config;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "1. Data save interval")]
            public int saveInterval;

            [JsonProperty(PropertyName = "2. Use auto-kits")]
            public bool useAutoKits;

            [JsonProperty(PropertyName = "3. GUI number")]
            public int guiNumber;

            [JsonProperty(PropertyName = "4. Fade out time")]
            public float fadeOut;

            [JsonProperty(PropertyName = "Auto-kits:")]
            public List<Kit> autokits;

            [JsonProperty(PropertyName = "Kit list:")]
            public List<Kit> kits;
        }

        private ConfigData GetDefaultConfig()
        {
            return new ConfigData
            {
                saveInterval = 300,
                guiNumber = 0,
                fadeOut = 1f,
                useAutoKits = false,
                kits = new List<Kit>
                {
                    new Kit
                    {
                        name = "starter",
                        displayName = "Starter",
                        permission = "",
                        cooldown = 600,
                        block = 0,
                        description = "Start items",
                        url = "https://i.imgur.com/IIP8QMF.png",
                        uses = 0,
                        items = new List<ExtendedItem>
                        {
                            new ExtendedItem
                            {
                                shortname = "stonehatchet",
                                amount = 1
                            },
                            new ExtendedItem
                            {
                                shortname = "stone.pickaxe",
                                amount = 1
                            }
                        }
                    }
                },
                autokits = new List<Kit>
                {
                    new Kit
                    {
                        permission = "",
                        items = new List<ExtendedItem>
                        {
                            new ExtendedItem
                            {
                                shortname = "stonehatchet",
                                amount = 1
                            },
                            new ExtendedItem
                            {
                                shortname = "stone.pickaxe",
                                amount = 1
                            }
                        }
                    }
                }
            };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<ConfigData>();

                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            PrintError("Configuration file is corrupt(or not exists), creating new one!");
            config = GetDefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        #endregion

        #region Data

        private const string filename = "Temp/Kits/players_data";
        private PlayerData data = new PlayerData();

        private class PlayerData
        {
            public Dictionary<ulong, Dictionary<string, double>> cooldowns = new Dictionary<ulong, Dictionary<string, double>>();
            public Dictionary<ulong, Dictionary<string, int>> uses = new Dictionary<ulong, Dictionary<string, int>>();
        }

        private void LoadData()
        {
            try
            {
                data = Interface.Oxide.DataFileSystem.ReadObject<PlayerData>(filename);
            }
            catch (Exception e)
            {
                PrintWarning(e.Message);
            }

            SaveData();
            timer.Every(config.saveInterval, SaveData);
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(filename, data);
        }
        
        private void WipeData()
        {
            data.cooldowns.Clear();
            data.uses.Clear();
            SaveData();
        }

        #endregion

        #region Localization

        private Dictionary<string, string> EN = new Dictionary<string, string>
        {
            {"Permission", "You don't have permission to use that!"},
            {"Cooldown", "Cooldown for {0} seconds!"},
            {"Blocked", "Kit is blocked for '{0}' seconds since wipe"},
            {"Added", "You successfully added kit '{0}' with '{1}' items"},
            {"Removed", "You successfully removed kit '{0}'"},
            {"No Kit", "Can't find kit with name '{0}'"},
            {"Uses", "You already used maximal amount [{0}] of that kit!"},
            
            {"ImageLibrary", "Image library not installed!"},
            {"Day", "{0} d"},
            {"Hour", "{0} h"},
            {"Minute", "{0} m"},
            {"Second", "{0} s"},
            {"Header GUI", "Following kits available:"},
            {"Description GUI", "Description: {0}"},
            {"Cooldown GUI", "Cooldown - {0}"},
            {"Uses GUI", "Uses - {0}"},
            {"Kit available", "Available"},
            {"Kit unavailable", "{0}"},
        };

        private string getMessage(string playerID, string key, params object[] args)
        {
            return string.Format(lang.GetMessage(key, this, playerID), args);
        }

        private void message(BasePlayer player, string key, params object[] args)
        {
            if (player == null) {return;}
            var message = getMessage(player.UserIDString, key, args);
            player.ChatMessage(message);
        }

        #endregion

        #region Commands

        private void Command(ConsoleSystem.Arg arg)
        {
            Command(arg.Player(), arg.Args);
        }

        private void Command(BasePlayer player, string command, string[] args)
        {
            Command(player, args);
        }

        private void Command(BasePlayer player, string[] args)
        {
            if (player == null)
            {
                return;
            }

            if(OneVSOne != null && OneVSOne.Call<bool>("IsEventPlayer", player)) return;

            if (args == null || args.Length == 0)
            {
                CreateKits(player);
                return;
            }

            var action = args[0].ToLower();
            var name = args.Length > 1 ? args[1] : "null";
            
            switch (action)
            {
                case "true":
                    CreateKits(player);
                    break;
                
                case "add":
                    AddKit(player, name);
                    break;

                case "remove":
                    RemoveKit(player, name);
                    break;
                
                case "info":
                    CreateInfo(player, GetKitValueByName(name));
                    break;

                default:
                    TryGiveKit(player, action);
                    break;
            }
        }

        #endregion
        
        #region Helpers

        private double Now()
        {
            return DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;
        }

        private int Passed(double a)
        {
            return Convert.ToInt32(Now() - a);
        }

        private double SaveTime()
        {
            return SaveRestore.SaveCreatedTime.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;
        }
        
        private string GetTimeString(int time, string userID)
        {
            var timeString = string.Empty;
            var days = Convert.ToInt32(time / 86400);
            time = time % 86400;
            if (days > 0)
            {
                timeString +=  $" {getMessage(userID, "Day", days)}";
            }
            
            var hours = Convert.ToInt32(time / 3600);
            time = time % 3600;
            if (hours > 0)
            {
                if (days > 0)
                {
                    timeString += ", ";
                }
                
                timeString += $" {getMessage(userID, "Hour", hours)}";
            }
            
            var minutes = Convert.ToInt32(time / 60);
            time = time % 60;
            if (minutes > 0)
            {
                if (hours > 0)
                {
                    timeString += ", ";
                }
                
                timeString += $" {getMessage(userID, "Minute", minutes)}";
            }
            
            var seconds = Convert.ToInt32(time);
            if (seconds > 0)
            {
                if (minutes > 0)
                {
                    timeString += ", ";
                }
                
                timeString += $" {getMessage(userID, "Second", seconds)}";
            }

            return timeString;
        }

        #endregion

        #region Core
        
        private int GetBlockTime(int time)
        {
            return time - Passed(SaveTime());
        }

        private int GetPassed(ulong id, string name)
        {
            data.cooldowns.TryAdd(id, new Dictionary<string, double>());
            data.cooldowns[id].TryAdd(name, 0);
            return Passed(data.cooldowns[id][name]);
        }

        private int GetUses(ulong id, string name)
        {
            data.uses.TryAdd(id, new Dictionary<string, int>());
            data.uses[id].TryAdd(name, 0);
            return data.uses[id][name];
        }

        private bool HasPermission(string id, string name)
        {
            return string.IsNullOrEmpty(name) || permission.UserHasPermission(id, name);
        }

        private List<ExtendedItem> GetPlayerItems(BasePlayer player)
        {
            var container = player.inventory;
            var items = new List<ExtendedItem>();

            foreach (var item in container.containerMain.itemList)
            {
                if (item.position < 24)
                {
                    items.Add(new ExtendedItem
                    {
                        shortname = item.info.shortname,
                        amount = item.amount,
                        skin = item.skin,
                        container = "Main",
                        position = item.position,
                        blueprint = item.IsBlueprint(),
                        displayName = item.name,
                        contents = GetContents(item)
                    });
                }
            }

            foreach (var item in container.containerWear.itemList)
            {
                items.Add(new ExtendedItem
                {
                    shortname = item.info.shortname,
                    amount = item.amount,
                    skin = item.skin,
                    container = "Wear",
                    position = item.position,
                    blueprint = item.IsBlueprint(),
                    displayName = item.name,
                    contents = GetContents(item)
                });
            }

            foreach (var item in container.containerBelt.itemList)
            {
                items.Add(new ExtendedItem
                {
                    shortname = item.info.shortname,
                    amount = item.amount,
                    skin = item.skin,
                    container = "Belt",
                    position = item.position,
                    blueprint = item.IsBlueprint(),
                    displayName = item.name,
                    contents = GetContents(item)
                });
            }

            return items;
        }

        private void AddKit(BasePlayer player, string name)
        {
            if (!player.IsAdmin)
            {
                message(player, "Permission");
                return;
            }

            var items = GetPlayerItems(player);

            config.kits.Add(new Kit
            {
                name = name,
                items = items,
                cooldown = 3600,
                permission = "Kits.Unknown",
                description = "Kit description",
                url = "",
                displayName = "",
                block = 0,
                uses = 0
            });

            SaveConfig();
            message(player, "Added", name, items.Count);
        }

        private void RemoveKit(BasePlayer player, string name)
        {
            if (!player.IsAdmin)
            {
                message(player, "Permission");
                return;
            }

            var kit = GetKitValueByName(name);
            
            if (kit != null)
            {
                config.kits.Remove(kit);
                SaveConfig();
                message(player, "Removed", name);
            }
            else
            {
                message(player, "Can't find", name);
            }
        }

        private void TryGiveKit(BasePlayer player, string name)
        {
            if (!CanUse(player))
            {
                return;
            }
            
            var kit = GetKitValueByName(name);
            if (kit == null)
            {
                message(player, "No Kit", name);
                return;
            }

            var id = player.userID;

            if (!HasPermission(id.ToString(), kit.permission))
            {
                message(player, "Permission");
                return;
            }

            var block = GetBlockTime(kit.block);
            if (block > 0)
            {
                message(player, "Blocked", block);
                return;
            }

            var uses = GetUses(id, kit.name);
            if (kit.uses != 0 && uses >= kit.uses)
            {
                message(player, "Uses", kit.uses);
                return;
            }

            var cooldown = kit.cooldown - GetPassed(id, kit.name);
            if (cooldown > 0)
            {
                message(player, "Cooldown", cooldown);
                return;
            }

            data.cooldowns[id][kit.name] = Now();
            data.uses[id][kit.name]++;
            GiveKit(player, kit);
            CreateKits(player);
        }

        private void GiveKit(BasePlayer player, Kit kit)
        {
            if (kit?.items == null)
            {
                return;
            }
            
            foreach (var value in kit.items)
            {
                GiveItem(value, player);
            }
        }

        private void GiveKit(BasePlayer player, string name)
        {
            var kit = GetKitValueByName(name);
            if (kit == null)
            {
                PrintError($"Can't find kit with name '{name}'!!!");
                return;
            }

            GiveKit(player, kit);
        }

        private Kit GetKitValueByName(string name)
        {
            try
            {
                return config.kits.First(x => string.Equals(x.name, name, StringComparison.CurrentCultureIgnoreCase));
            }
            catch
            {
                return null;
            }
        }

        private bool CanUse(BasePlayer player)
        {
            return Interface.Oxide.CallHook("canRedeemKit", player) == null &&
                   Interface.Oxide.CallHook("CanUseKit", player) == null;
        }
        
        private void GiveAutoKit(BasePlayer player)
        {
            foreach (var item in player.inventory.AllItems().ToList())
            {
                if (item == null) {continue;}
                if (item.position > 23) {continue;}
                item.GetHeldEntity()?.Kill();
                item.DoRemove();
            }
            
            player.inventory.containerWear.MarkDirty();
            player.inventory.containerMain.MarkDirty();
            player.inventory.containerBelt.MarkDirty();
            player.SendNetworkUpdate();
            
            GiveKit(player, GetAutoKit(player.UserIDString));
        }

        private Kit GetAutoKit(string playerID)
        {
            foreach (var value in config.autokits)
            {
                if (HasPermission(playerID, value.permission))
                {
                    return value;
                }
            }

            return null;
        }

        private Dictionary<string, int> GetContents(Item item)
        {
            var items = new Dictionary<string, int>();
            
            if (item.contents != null)
            {
                foreach (var mod in item.contents.itemList)
                {
                    var name = mod.info.shortname;
                    var amount = mod.amount;
                    items.TryAdd(name, 0);
                    items[name] += amount;
                }
            }
            
            var weapon = item?.GetHeldEntity()?.GetComponent<BaseProjectile>()?.primaryMagazine;
            if (weapon != null)
            {
                var name = weapon.ammoType.shortname;
                var amount = weapon.contents;
                items.TryAdd(name, 0);
                items[name] += amount;
            }
            
            return items;
        }

        private void GiveItem(ExtendedItem info, BasePlayer player)
        {
            var item = ItemManager.CreateByName(info.shortname, info.amount, info.skin);
            if (item == null) {return;}
            item.name = info.displayName;

            // TODO: Blueprint

            if (info.contents != null)
            {
                foreach (var pair in info.contents)
                {
                    var name = pair.Key;
                    var amount = pair.Value;
                    var mod = ItemManager.CreateByName(name, amount);
                    if (mod == null) {continue;}
                    if (!mod.MoveToContainer(item.contents))
                    {
                        mod.GetHeldEntity()?.Kill();
                        mod.DoRemove();
                    }
                }
            }
            
            var position = info.position;

            switch (info.container)
            {
                case "Main":
                    item.MoveToContainer(player.inventory.containerMain, position);
                    break;
                case "Wear":
                    item.MoveToContainer(player.inventory.containerWear, position);
                    break;
                case "Belt":
                    item.MoveToContainer(player.inventory.containerBelt, position);
                    break;
            }

            if (item.GetRootContainer() == null)
            {
                player.GiveItem(item);
            }
        }

        #endregion
        
        #region GUI

        private const string elemHud = "kits.hud";
        private const string elemMain = "kits.main";
        private const string elemInfo = "kits.info";
        private const string outlineColor = "0 0 0 1";
        private const string outlineDistance = "1.0 -0.5";

        #region Core

        private void CreateKits(BasePlayer player)
        {
            if (ImageLibrary == null)
            {
                message(player, "ImageLibrary");
                return;
            }
            
            var kits = config.kits.Where(value => value.showWithoutPermission || HasPermission(player.UserIDString, value.permission)).ToList();
            if (kits.Count == 0) {return;}
            var container = GetContainerKits(kits, player.userID);
            CuiHelper.DestroyUi(player, elemHud);
            CuiHelper.AddUi(player, container);
        }

        private void CreateInfo(BasePlayer player, Kit kit)
        {
            if (ImageLibrary == null)
            {
                message(player, "ImageLibrary");
                return;
            }

            if (kit == null) {return;}
            var container = GetContainerInfo(kit, player.userID);
            CuiHelper.DestroyUi(player, elemInfo);
            CuiHelper.AddUi(player, container);
        }

        private CuiElementContainer GetContainerKits(List<Kit> kits, ulong playerID)
        {
            switch (config.guiNumber)
            {
                case 481:
                    return GetKitPanel1(kits, playerID);
                    
                case 513:
                    return GetKitPanel2(kits, playerID);
                
                case 622:
                    return GetKitPanel3(kits, playerID);
                    
                default:
                    return new CuiElementContainer();
            }
        }

        private CuiElementContainer GetContainerInfo(Kit kit, ulong playerID)
        {
            switch (config.guiNumber)
            {
                case 481:
                    return GetKitContent1(kit, playerID);
                
                case 513:
                    return GetKitContent1(kit, playerID);
                
                case 622:
                    return GetKitContent1(kit, playerID);
                    
                default:
                    return new CuiElementContainer();
            }
        }

        #endregion

        #region GUI #1

        private CuiElementContainer GetKitPanel1(List<Kit> kits, ulong userID)
        {
            var baseX = -0.1;
            var baseY = 0.8;
            var sizeX = 0.22;
            var sizeY = 0.2;
            var offsetX = 0.02;
            var offsetY = 0.05;
            var x = baseX;
            var y = baseY;
            var userIDs = userID.ToString();
            var container = new CuiElementContainer
            {
                new CuiElement // Hud
                {
                    Name = elemHud,
                    Parent = "Hud.Menu",
                    FadeOut = config.fadeOut,
                    Components =
                    {
                        new CuiButtonComponent
                        {
                            Color = "0.25 0.25 0.25 0.75",
                            Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
                            Close = elemHud
                        },
                        new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"},
                        new CuiNeedsCursorComponent()
                    }
                },
                new CuiElement // Text
                {
                    Parent = elemHud,
                    FadeOut = config.fadeOut,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = getMessage(userIDs, "Header GUI"),
                            Color = "1 0.71 0.51 1",
                            FontSize = 25,
                            Align = TextAnchor.MiddleCenter
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.0 0.7",
                            AnchorMax = "1.0 0.8"
                        }
                    }
                },
                new CuiElement // Main Panel
                {
                    Name = elemMain,
                    Parent = elemHud,
                    FadeOut = config.fadeOut,
                    Components =
                    {
                        new CuiButtonComponent {Color = "1 1 1 0"},
                        new CuiRectTransformComponent {AnchorMin = "0.2 0.2", AnchorMax = "0.8 0.7"}
                    }
                }
            };

            for (var i = 0; i < kits.Count; i++)
            {
                if (i != 0 && i % 5 == 0)
                {
                    x = baseX;
                    y -= sizeY + offsetY;
                }

                var kit = kits[i];
                var id = kit.name;
                var cooldown = kit.cooldown - GetPassed(userID, kit.name);
                var key = cooldown > 0 ? "Kit unavailable" : "Kit available";
                var cooldownText = getMessage(userIDs, key, GetTimeString(cooldown, userIDs));

                if (string.IsNullOrEmpty(kit.url))
                {
                    container.Add(new CuiElement
                    {
                        Name = id,
                        Parent = elemMain,
                        FadeOut = config.fadeOut,
                        Components =
                        {
                            new CuiImageComponent
                            {
                                Color = "0.25 0.25 0.25 0.5"
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = $"{x} {y}",
                                AnchorMax = $"{x + sizeX} {y + sizeY}"
                            }
                        }
                    });
                }
                else
                {
                    container.Add(new CuiElement
                    {
                        Name = id,
                        Parent = elemMain,
                        FadeOut = config.fadeOut,
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                Png = GetImage(id),
                                Color = "1 1 1 1"
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = $"{x} {y}",
                                AnchorMax = $"{x + sizeX} {y + sizeY}"
                            }
                        }
                    });
                }

                container.Add(new CuiElement // Kit name
                {
                    Parent = id,
                    FadeOut = config.fadeOut,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = kit.displayName,
                            Align = TextAnchor.MiddleCenter,
                            Color = "1 1 1 1",
                            FontSize = 15
                        },
                        new CuiOutlineComponent
                        {
                            Color = outlineColor,
                            Distance = outlineDistance
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0.7",
                            AnchorMax = "1 0.95"
                        }
                    }
                });

                container.Add(new CuiElement // Info text
                {
                    Parent = id,
                    FadeOut = config.fadeOut,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = "?",
                            Align = TextAnchor.UpperRight,
                            Color = "1 1 1 1",
                            FontSize = 15
                        },
                        new CuiOutlineComponent
                        {
                            Color = outlineColor,
                            Distance = outlineDistance
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0.5",
                            AnchorMax = "0.95 0.95"
                        }
                    }
                });

                container.Add(new CuiElement // Cooldown
                {
                    Parent = id,
                    FadeOut = config.fadeOut,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = cooldownText,
                            Align = TextAnchor.LowerCenter
                        },
                        new CuiOutlineComponent
                        {
                            Color = outlineColor,
                            Distance = outlineDistance
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0.03",
                            AnchorMax = "1 0.5"
                        }
                    }
                });

                container.Add(new CuiElement // Info button
                {
                    Parent = id,
                    FadeOut = config.fadeOut,
                    Components =
                    {
                        new CuiButtonComponent
                        {
                            Command = $"{command} info {kit.name}",
                            Color = "1 1 1 0"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0.77",
                            AnchorMax = "1 1"
                        }
                    }
                });

                container.Add(new CuiElement // Get button
                {
                    Parent = id,
                    FadeOut = config.fadeOut,
                    Components =
                    {
                        new CuiButtonComponent
                        {
                            Command = $"{command} {kit.name}",
                            Color = "1 1 1 0"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 0.77"
                        }
                    }
                });

                x += sizeX + offsetX;
            }

            return container;
        }

        private CuiElementContainer GetKitContent1(Kit kit, ulong userID)
        {
            var baseX = 0.3;
            var baseY = 0.6;
            var sizeX = 0.05;
            var sizeY = 0.08;
            var offsetX = 0.005;
            var offsetY = 0.01;
            var x = baseX;
            var y = baseY;
            var items = kit.items;
            var userIDs = userID.ToString();
            
            var container = new CuiElementContainer
            {
                new CuiElement // Hud
                {
                    Name = elemInfo,
                    Parent = "Hud.Menu",
                    Components =
                    {
                        new CuiButtonComponent
                        {
                            Color = "0.25 0.25 0.25 0.75",
                            Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
                            Close = elemInfo
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1"
                        }
                    }
                },
                new CuiElement // Name
                {
                    Parent = elemInfo,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = kit.displayName.ToUpper(),
                            Color = "1 1 1 1",
                            FontSize = 30,
                            Align = TextAnchor.MiddleCenter
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.0 0.7",
                            AnchorMax = "1 1"
                        }
                    }
                },
                new CuiElement // Info
                {
                    Parent = elemInfo,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = getMessage(userIDs, "Description GUI", kit.description) + "\n" +
                                   getMessage(userIDs, "Cooldown GUI", GetTimeString(kit.cooldown, userIDs)) + "\n" +
                                   getMessage(userIDs, kit.uses > 0 ? "Uses GUI" : "", kit.uses),
                            Color = "1 1 1 1",
                            FontSize = 15,
                            Align = TextAnchor.MiddleCenter
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.0 0.7",
                            AnchorMax = "1 0.8"
                        }
                    }
                }
            };

            for (var i = 0; i < items.Count; i++)
            {
                if (i != 0 && i % 8 == 0)
                {
                    x = baseX;
                    y -= sizeY + offsetY;
                }

                var item = items[i];

                container.Add(new CuiElement // Panel
                {
                    Parent = elemInfo,
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = "0.5 0.5 0.5 0.75"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = $"{x} {y}",
                            AnchorMax = $"{x + sizeX} {y + sizeY}"
                        }
                    }
                });

                container.Add(new CuiElement // Image
                {
                    Parent = elemInfo,
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = GetImage(item.shortname)
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = $"{x} {y}",
                            AnchorMax = $"{x + sizeX} {y + sizeY}"
                        }
                    }
                });

                container.Add(new CuiElement // Amount
                {
                    Parent = elemInfo,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = $"x {item.amount}",
                            Align = TextAnchor.LowerRight
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = $"{x} {y}",
                            AnchorMax = $"{x + sizeX} {y + sizeY}"
                        }
                    }
                });

                x += sizeX + offsetX;
            }

            return container;
        }

        #endregion

        #region GUI #2

        private CuiElementContainer GetKitPanel2(List<Kit> kits, ulong userID)
        {
            var baseX = 0.0;
            var baseY = 0.8;
            var sizeX = 0.22;
            var sizeY = 0.2;
            var offsetX = 0.02;
            var offsetY = 0.05;
            var x = baseX;
            var y = baseY;
            var userIDs = userID.ToString();
            var container = new CuiElementContainer
            {
                new CuiElement // Hud
                {
                    Name = elemHud,
                    Parent = "Hud.Menu",
                    FadeOut = config.fadeOut,
                    Components =
                    {
                        new CuiButtonComponent
                        {
                            Color = "0.25 0.25 0.25 0",
                            Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
                            Close = elemHud
                        },
                        new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"},
                        new CuiNeedsCursorComponent()
                    }
                },
                new CuiElement // Main Panel
                {
                    Name = elemMain,
                    Parent = elemHud,
                    FadeOut = config.fadeOut,
                    Components =
                    {
                        new CuiButtonComponent {Color = "1 1 1 0"},
                        new CuiRectTransformComponent {AnchorMin = "0.2 0.2", AnchorMax = "0.8 0.7"}
                    }
                }
            };

            for (var i = 0; i < kits.Count; i++)
            {
                if (i != 0 && i % 4 == 0)
                {
                    x = baseX;
                    y -= sizeY + offsetY;
                }

                var kit = kits[i];
                var id = kit.name;
                var cooldown = kit.cooldown - GetPassed(userID, kit.name);
                var key = cooldown > 0 ? "Kit unavailable" : "Kit available";
                var cooldownText = getMessage(userIDs, key, GetTimeString(cooldown, userIDs));

                if (string.IsNullOrEmpty(kit.url))
                {
                    container.Add(new CuiElement
                    {
                        Name = id,
                        Parent = elemMain,
                        FadeOut = config.fadeOut,
                        Components =
                        {
                            new CuiImageComponent
                            {
                                Color = "0.8 0 0 1",
								Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                            },
							new CuiOutlineComponent
							{
								Color = outlineColor,
								Distance = "4.0 -4.0"
							},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = $"{x} {y}",
                                AnchorMax = $"{x + sizeX} {y + sizeY}"
                            }
                        }
                    });
                }
                else
                {
                    container.Add(new CuiElement
                    {
                        Name = id,
                        Parent = elemMain,
                        FadeOut = config.fadeOut,
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                Png = GetImage(id),
                                Color = "1 1 1 1"
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = $"{x} {y}",
                                AnchorMax = $"{x + sizeX} {y + sizeY}"
                            }
                        }
                    });
                }

                container.Add(new CuiElement // Kit name
                {
                    Parent = id,
                    FadeOut = config.fadeOut,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = kit.displayName,
                            Align = TextAnchor.UpperLeft,
                            Color = "1 1 1 1",
                            FontSize = 20
                        },
                        new CuiOutlineComponent
                        {
                            Color = outlineColor,
                            Distance = outlineDistance
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.05 0.3",
                            AnchorMax = "1 0.95"
                        }
                    }
                });

                container.Add(new CuiElement // Cooldown
                {
                    Parent = id,
                    FadeOut = config.fadeOut,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = cooldownText,
                            Align = TextAnchor.LowerRight
                        },
                        new CuiOutlineComponent
                        {
                            Color = outlineColor,
                            Distance = outlineDistance
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0.03",
                            AnchorMax = "0.9 0.3"
                        }
                    }
                });

                container.Add(new CuiElement // Info button
                {
                    Parent = id,
                    FadeOut = config.fadeOut,
                    Components =
                    {
                        new CuiButtonComponent
                        {
                            Command = $"{command} info {kit.name}",
                            Color = "1 1 1 0"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0.3",
                            AnchorMax = "1 1"
                        }
                    }
                });

                container.Add(new CuiElement // Get button
                {
                    Parent = id,
                    FadeOut = config.fadeOut,
                    Components =
                    {
                        new CuiButtonComponent
                        {
                            Command = $"{command} {kit.name}",
                            Color = "1 1 1 0"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 0.3"
                        }
                    }
                });

                x += sizeX + offsetX;
            }

            return container;
        }

        #endregion

        #region GUI #3

        private CuiElementContainer GetKitPanel3(List<Kit> kits, ulong userID)
        {
            var baseX = 0.4;
            var baseY = 0.75;
            var sizeX = 0.17;
            var sizeY = 0.15;
            var offsetX = 0.02;
            var offsetY = 0.05;
            var x = baseX;
            var y = baseY;
            var userIDs = userID.ToString();
            var container = new CuiElementContainer
            {
                new CuiElement
                {
					Name = elemHud,
                    Parent = "Hud.Menu",
                    FadeOut = config.fadeOut,
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = GetImage("DeanoMax"),
                            Color = "1 1 1 1"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.2 0.2", 
							AnchorMax = "0.8 0.7"
                        },
                        new CuiNeedsCursorComponent()
                    }
                },
                {
                    new CuiButton
                    {
                        Button = { Close = elemHud, Color = "0 0 0 0"},
                        Text = { Text = "X", FontSize = 15, Color = "1 1 1 1", Align = TextAnchor.MiddleCenter},
                        RectTransform = { AnchorMin = "0.95 0.93", AnchorMax = "0.99 0.99"},
                        FadeOut = config.fadeOut,
                    }, elemHud
                }
            };

            for (var i = 0; i < kits.Count; i++)
            {
                if (i != 0 && i % 3 == 0)
                {
                    x = baseX;
                    y -= sizeY + offsetY;
                }

                var kit = kits[i];
                var id = kit.name;
                var cooldown = kit.cooldown - GetPassed(userID, kit.name);
                var key = cooldown > 0 ? "Kit unavailable" : "Kit available";
                var cooldownText = getMessage(userIDs, key, GetTimeString(cooldown, userIDs));

                if (string.IsNullOrEmpty(kit.url))
                {
                    container.Add(new CuiElement
                    {
                        Name = id,
                        Parent = elemHud,
                        FadeOut = config.fadeOut,
                        Components =
                        {
                            new CuiImageComponent
                            {
                                Color = "0.5 0.5 0.5 0.25",
								Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = $"{x} {y}",
                                AnchorMax = $"{x + sizeX} {y + sizeY}"
                            }
                        }
                    });
                }
                else
                {
                    container.Add(new CuiElement
                    {
                        Name = id,
                        Parent = elemHud,
                        FadeOut = config.fadeOut,
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                Png = GetImage(id),
                                Color = "1 1 1 1"
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = $"{x} {y}",
                                AnchorMax = $"{x + sizeX} {y + sizeY}"
                            }
                        }
                    });
                }

                container.Add(new CuiElement // Kit name
                {
                    Parent = id,
                    FadeOut = config.fadeOut,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = kit.displayName,
                            Align = TextAnchor.MiddleCenter,
                            Color = "1 1 1 1",
                            FontSize = 15
                        },
                        new CuiOutlineComponent
                        {
                            Color = outlineColor,
                            Distance = outlineDistance
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0.5",
                            AnchorMax = "1 0.95"
                        }
                    }
                });

                container.Add(new CuiElement // Cooldown
                {
                    Parent = id,
                    FadeOut = config.fadeOut,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = cooldownText,
                            Align = TextAnchor.LowerRight
                        },
                        new CuiOutlineComponent
                        {
                            Color = outlineColor,
                            Distance = outlineDistance
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0.03",
                            AnchorMax = "0.9 0.5"
                        }
                    }
                });

                container.Add(new CuiElement // Info button
                {
                    Parent = id,
                    FadeOut = config.fadeOut,
                    Components =
                    {
                        new CuiButtonComponent
                        {
                            Command = $"{command} info {kit.name}",
                            Color = "1 1 1 0"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0.3",
                            AnchorMax = "1 1"
                        }
                    }
                });

                container.Add(new CuiElement // Get button
                {
                    Parent = id,
                    FadeOut = config.fadeOut,
                    Components =
                    {
                        new CuiButtonComponent
                        {
                            Command = $"{command} {kit.name}",
                            Color = "1 1 1 0"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 0.3"
                        }
                    }
                });

                x += sizeX + offsetX;
            }

            return container;
        }

        #endregion
        
        #endregion
    }
}