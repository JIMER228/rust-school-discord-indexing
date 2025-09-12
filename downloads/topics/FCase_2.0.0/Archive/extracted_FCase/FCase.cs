using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("FCase", "Sempai", "2.0.0")]
    public class FCase : RustPlugin
    {

        #region Var

        [PluginReference] private Plugin ImageLibrary, FNotification;
        
        #endregion
        
        
        #region Data
        

        public class StorageList
        {
            
            public string PrefabSpawn;
            public string Image;

            public int UniversalNumber;
        }


        public Dictionary<ulong, List<StorageList>> _playerData = new Dictionary<ulong, List<StorageList>>();

        #endregion
        
        
        #region Hooks


        object CanLootEntity(BasePlayer player, ResourceExtractorFuelStorage container)
        {
            if (player == null || container == null)
            {
                return null;
            }

            if (container.OwnerID == 1156)
            {
                OpenSwitchCase(player);
                return false;
            }
            return null;
        }
        
        private object OnTeamInvite(BasePlayer inviter, BasePlayer target)
        {
            if (target.OwnerID != 1156) return null;
            OpenSwitchCase(inviter);
            return true;
        }

        void OnServerInitialized()
        {
            PrintWarning("\n-----------------------------\n " +" Author - Sempai#3239\n " +" VK - https://vk.com/rustnastroika\n " +" Forum - https://topplugin.ru\n " +" Discord - https://discord.gg/5DPTsRmd3G\n" +"-----------------------------");  
            try
            {
                _playerData = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<ulong, List<StorageList>>>(Name);
                if (_playerData == null)
                    _playerData = new Dictionary<ulong, List<StorageList>>();
            }
            catch
            {
                _playerData = new Dictionary<ulong, List<StorageList>>();
            }
            if (ImageLibrary == null || !ImageLibrary.IsLoaded)
            {
                PrintError("IMAGE LIBRARY IS NOT INSTALLED!");
            }
            else
            {
                var imagesList = new Dictionary<string, string>();

                foreach (var imageKeys in config._SettingsCase)
                {
                    if (!string.IsNullOrEmpty(imageKeys.Image) && !imagesList.ContainsKey(imageKeys.Image))
                    {
                        imagesList.Add(imageKeys.Image, imageKeys.Image);
                    }
                }

                ImageLibrary?.Call("ImportImageList", Title, imagesList, 0UL, true);
            }
            
            
        }

        void Unload()
        {
            Interface.GetMod().DataFileSystem.WriteObject(Name, _playerData);
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (!_playerData.ContainsKey(player.userID))
                _playerData.Add(player.userID, new List<StorageList>());
        }
        

        #endregion


        #region UI


        public string Layer = "UI_FCase";
        
        public string HexToCuiColor(string HEX, float Alpha = 100)
        {
            if (string.IsNullOrEmpty(HEX)) HEX = "#FFFFFF";

            var str = HEX.Trim('#');
            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

            return $"{(double) r / 255} {(double) g / 255} {(double) b / 255} {Alpha / 100}";
        }


        void OpenSwitchCase(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, Layer);
            CuiElementContainer container = new CuiElementContainer();

            #region Panel | Color Panel

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Image = { Color = "0 0 0 0.9", Material = "assets/content/ui/uibackgroundblur.mat"}
            }, "Overlay", Layer);
            
            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiImageComponent { Color = "0 0 0 0.9", Sprite = "assets/content/ui/ui.background.transparent.radial.psd" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" }
                }
            });

            #endregion

            #region Close

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "-100 -100", AnchorMax = "100 100", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Close = Layer},
                Text = { Text = "" }
            }, Layer);

            #endregion
            
            
            #region Panel Main

            
            container.Add(new CuiButton 
            {
                RectTransform = { AnchorMin = "0.06093755 0.1592593", AnchorMax = "0.940625 0.8444445"},
                Button =
                {
                    Command = $"",
                    Color = HexToCuiColor("#fffdfc", 5), Material = "assets/content/ui/uibackgroundblur.mat"
                },
                Text =
                {
                    Text = "", Color = HexToCuiColor("#fffdfc"),
                    Align = TextAnchor.MiddleLeft, FontSize = 13, Font = "robotocondensed-bold.ttf"
                },

            }, Layer);

            #endregion

            #region Balance

            container.Add(new CuiButton 
            {
                RectTransform = { AnchorMin = "0.07135417 0.1796296", AnchorMax = "0.1765625 0.2259259"},
                Button =
                {
                    Command = $"",
                    Color = HexToCuiColor("#4094b9"), Sprite = "assets/content/ui/ui.background.tile.psd"
                },
                Text =
                {
                    Text = $"Баланс: {GetAmount(player.inventory.AllItems(), config._itemSetup.ShortName, config._itemSetup.SkinID)}", Color = HexToCuiColor("#fcf7f6"),
                    Align = TextAnchor.MiddleCenter, FontSize = 16, Font = "robotocondensed-bold.ttf"
                },

            }, Layer, Layer + ".Balance");

            #endregion

            #region Comment

            container.Add(new CuiButton 
            {
                RectTransform = { AnchorMin = "0.1848959 0.1796296", AnchorMax = "0.9296875 0.2259259"},
                Button =
                {
                    Command = $"",
                    Color = "0 0 0 0"
                },
                Text =
                {
                    Text = $"Я продаю уникальные ящики с различным лутом за Золотые монеты! Золотые монеты можно добыть со всех видов бочек, ящиков и разных ивентов там их больше всего! Для того, что бы открыть ящики - зайдите в /menu и нажмите 'Инвентарь ящиков'.", Color = HexToCuiColor("#fcf7f6"),
                    Align = TextAnchor.MiddleLeft, FontSize = 14, Font = "robotocondensed-regular.ttf"
                },

            }, Layer);

            #endregion


            foreach (var check in config._SettingsCase.Select((i, t) => new { A = i, B = t }))
            {
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin =
                            $"{0.07135417 + check.B * 0.173 - Math.Floor((float) check.B / 5) * 5 * 0.173} {0.5435185 - Math.Floor((float) check.B/ 5) * 0.30}",
                        AnchorMax =
                            $"{0.2354167 + check.B * 0.173 - Math.Floor((float) check.B / 5) * 5 * 0.173} {0.825917 - Math.Floor((float) check.B / 5) * 0.30}",
                        OffsetMax = "0 0"
                    },
                    Button =
                    {
                        Color = HexToCuiColor("#000000", 50),
                        Command = ""
                    },
                    Text =
                    {
                        Text = $"", Align = TextAnchor.LowerRight, Font = "robotocondensed-bold.ttf", FontSize = 15
                    }
                }, Layer, Layer + $".{check.B}.ItemCaseList");
                
                container.Add(new CuiElement
                {
                    Parent = Layer + $".{check.B}.ItemCaseList",
                    Components =
                    {
                        new CuiRawImageComponent
                            {FadeIn = 0.3f, Png = (string)ImageLibrary.Call("GetImage", check.A.Image)},
                        new CuiRectTransformComponent
                            {AnchorMin = "0.2571428 0.4491943", AnchorMax = "0.7619047 0.9705225"}
                    }
                });
                
                container.Add(new CuiElement()
                {
                    Parent = Layer + $".{check.B}.ItemCaseList",
                    Components =
                    {
                        new CuiTextComponent{Color = HexToCuiColor("#E7DDD4"),Text = $"<b>{check.A.DisplayName}</b>\n<size=10>Цена: {check.A.Price}</size>", Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "robotocondensed-regular.ttf"},
                        new CuiRectTransformComponent{AnchorMin = "0 0.2295154", AnchorMax = "1 0.4491944"},
                    }
                });
                
                container.Add(new CuiButton 
                {
                    RectTransform = { AnchorMin = "0.2603174 0.06557581", AnchorMax = "0.7428571 0.2295153"},
                    Button =
                    {
                        Command = GetAmount(player.inventory.AllItems(), config._itemSetup.ShortName, config._itemSetup.SkinID) > check.A.Price ? $"UI_FCaseHandler buyItem {check.A.DisplayName}" : "",
                        Color = HexToCuiColor("#95B641"), Material = "assets/content/ui/uibackgroundblur.mat"
                    },
                    Text =
                    {
                        Text = "Купить", Color = HexToCuiColor("#FFFFFA"),
                        Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "robotocondensed-bold.ttf"
                    },

                }, Layer + $".{check.B}.ItemCaseList");
                
            }

            CuiHelper.AddUi(player, container);
        }


        public void OpenInventoryPage(BasePlayer player, int page)
        {
            var data = _playerData[player.userID];
            int pagex = page + 1;
            
            var container = new CuiElementContainer();


            CuiHelper.DestroyUi(player, Layer + ".Vpered");
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.264584 0.2027778", AnchorMax = "0.2963542 0.2490829" },
                Button =
                {
                    Color = HexToCuiColor("#fffdfc", 10), Material = "assets/content/ui/uibackgroundblur.mat",
                    Command = pagex > 0 && (pagex - 1) * 50 < data.Count
                        ? $"UI_FCaseHandler pageInventory {page + 1}"
                        : ""
                },
                Text =
                {
                    Text = "+", Color = HexToCuiColor("#fcf7f6"),
                    Align = TextAnchor.MiddleCenter, FontSize = 18, Font = "robotocondensed-bold.ttf"
                }
            }, Layer, Layer + ".Vpered");
            
            CuiHelper.DestroyUi(player, Layer + ".Nazad");
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.1958331 0.2027778", AnchorMax = "0.2276038 0.2490829" },
                Button =
                {
                    Color = HexToCuiColor("#fffdfc", 10), Material = "assets/content/ui/uibackgroundblur.mat",
                    Command = page != 1
                        ? $"UI_FCaseHandler pageInventory {page - 1}"
                        : ""
                },
                Text =
                {
                    Text = "-", Color = HexToCuiColor("#fcf7f6"),
                    Align = TextAnchor.MiddleCenter, FontSize = 18, Font = "robotocondensed-bold.ttf"
                }
            }, Layer, Layer + ".Nazad");
            
            CuiHelper.DestroyUi(player, Layer + ".Page");
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.2302085 0.2027778", AnchorMax = "0.2619792 0.2490829" },
                Button =
                {
                    Color = HexToCuiColor("#000000", 50),
                },
                Text =
                {
                    Text = $"{page}", Color = HexToCuiColor("#fcf7f6"),
                    Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "robotocondensed-regular.ttf"
                }
            }, Layer, Layer + ".Page");

            for (int i = 0; i < 50; i++)
            {
                CuiHelper.DestroyUi(player, Layer + $".{i}.StorageList");
            }

            foreach (var check in data.Select((i, t) => new { A = i, B = t - (page - 1) * 50 }).Skip((page - 1) * 50).Take(50))
            {
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin =
                            $"{0.203125 + check.B * 0.0605 - Math.Floor((float)check.B / 10) * 10 * 0.0605} {0.6962963 - Math.Floor((float)check.B / 10) * 0.105}",
                        AnchorMax =
                            $"{0.2541667 + check.B * 0.0605 - Math.Floor((float)check.B / 10) * 10 * 0.0605} {0.7870371 - Math.Floor((float)check.B / 10) * 0.105}",
                        OffsetMax = "0 0"
                    },
                    Button =
                    {
                        Color = "0 0 0 0",
                        Command = $"UI_FCaseHandler giveInventory {check.A.UniversalNumber} {check.B}",
                    },
                    Text =
                    {
                        Text = $"", Align = TextAnchor.LowerRight, Font = "robotocondensed-bold.ttf", FontSize = 15
                    }
                }, Layer, Layer + $".{check.B}.StorageList");
                
                container.Add(new CuiElement
                {
                    Parent = Layer + $".{check.B}.StorageList",
                    Components =
                    {
                        new CuiRawImageComponent
                            {FadeIn = 0.3f, Png = (string)ImageLibrary.Call("GetImage", check.A.Image)},
                        new CuiRectTransformComponent
                            {AnchorMin = "0 0", AnchorMax = "1 1"}
                    }
                });
            }

            CuiHelper.AddUi(player, container);
        }

        #endregion


        #region Functional
        
        private List<ItemSettings> GetRandom(SettingsCase rate)
        {
            List<ItemSettings> result = Pool.GetList<ItemSettings>();
            ;
            var lootitemcount = rate._itemList?.Count;
            int amount = Mathf.RoundToInt(UnityEngine.Random.Range(
                Convert.ToSingle(Mathf.Min(rate.CapacityMin, rate.CapacityMax)) * 100f,
                Convert.ToSingle(Mathf.Max(rate.CapacityMin, rate.CapacityMax)) * 100f) / 100f);
            if (lootitemcount > 0 && amount > lootitemcount && lootitemcount < 36)
                amount = (int)lootitemcount;
            for (int i = 0; i < amount; i++)
            {
                ItemSettings item = null;
                int iteration = 0;
                do
                {
                    iteration++;

                    ItemSettings randomItem = rate._itemList.GetRandom();
                    if (result.Contains(randomItem))
                        continue;
                    item = randomItem;
                } while (item == null && iteration < 1000);

                if (item != null)
                    result.Add(item);
            }

            return result;
        }
        
        
        private void ClearContainer(LootContainer container)
        {
            if (container.inventory == null)
            {
                container.inventory = new ItemContainer();
                container.inventory.ServerInitialize(null, 36);
                container.inventory.GiveUID();
            }
            else
            {
                while (container.inventory.itemList.Count > 0)
                {
                    Item item = container.inventory.itemList[0];
                    item.RemoveFromContainer();
                    item.Remove(0f);
                }

                container.inventory.capacity = 36;
            }

            container.inventory.itemList.Clear();
            container.inventory.Clear();
            ItemManager.DoRemoves();
        }
        
        
        private int Take(IEnumerable<Item> itemList, string shortname, ulong skinId, int iAmount)
        {
            int num1 = 0;
            if (iAmount == 0)
                return 0;

            var list = Facepunch.Pool.GetList<Item>();
            foreach (var obj in itemList)
            {
                if (obj.info.shortname == shortname && obj.skin == skinId)
                {
                    int num2 = iAmount - num1;
                    if (num2 > 0)
                    {
                        if (obj.amount > num2)
                        {
                            obj.MarkDirty();
                            obj.amount -= num2;
                            num1 += num2;

                            break;
                        }

                        if (obj.amount <= num2)
                        {
                            num1 += obj.amount;
                            list.Add(obj);
                        }

                        if (num1 == iAmount)
                            break;
                    }
                }
            }

            foreach (var obj in list)
                obj.Remove();
            Facepunch.Pool.FreeList<Item>(ref list);
            return num1;
        }
        

        public int GetAmount(IEnumerable<Item> itemList, string shortname, ulong skinId)
        {
            int num = 0;
            foreach (Item obj in itemList)
            {
                if (obj.info.shortname == shortname && obj.skin == skinId)
                    num += obj.amount;
            }

            return num;
        }

        #endregion


        #region ChatCommand && ConsoleCommand


        [ConsoleCommand("UI_FCaseHandler")]
        void FCaseHandler(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();


            if (args.Args[0] == "pageInventory")
            {
                int page = 1;
                if (int.TryParse(args.Args[1], out page))
                {
                    OpenInventoryPage(player, page);
                }
            }
            else if (args.Args[0] == "giveInventory")
            {
                var find = _playerData[player.userID].FirstOrDefault(p => p.UniversalNumber == int.Parse(args.Args[1]));
                if (find == null) return;
                var findConfig = config._SettingsCase.FirstOrDefault(p => p.PrefabName == find.PrefabSpawn);
                if (findConfig == null) return;
                
                Vector3 forward = player.GetNetworkRotation() * Vector3.forward;
                forward.y = 0;
                var position = player.transform.position + forward.normalized * 1;


                var entity = GameManager.server.CreateEntity(find.PrefabSpawn, position);
                if (entity == null) return;

                entity.Spawn();

                var container = entity.GetComponent<LootContainer>();
                if (container == null) return;


                ClearContainer(container);

                var getList = GetRandom(findConfig);
                
                foreach (var kItemLoot in getList)
                {
                    Item item = kItemLoot.GiveItem();
                    if (item == null)
                    {
                        item.Remove(0f);
                        continue;
                    }
                    
                    item.OnVirginSpawn();
                    if (!item.MoveToContainer(container.inventory, -1, true))
                    {
                        item.Remove(0f);
                    }
                }
                
                container.inventory.capacity = container.inventory.itemList.Count;
                container.inventory.MarkDirty();
                container.inventory.SetFlag(ItemContainer.Flag.NoItemInput, true);
                container.SendNetworkUpdate();
                ItemManager.DoRemoves();
                
                
                
                _playerData[player.userID].Remove(find);

                CuiHelper.DestroyUi(player, Layer + $".{int.Parse(args.Args[2])}.StorageList");
            }
            else if (args.Args[0] == "buyItem")
            {
                var find = config._SettingsCase.FirstOrDefault(p =>
                    p.DisplayName == string.Join(" ", args.Args.Skip(1).ToArray()));
                
                if (find == null) return;

                int amount = GetAmount(player.inventory.AllItems(), config._itemSetup.ShortName,
                    config._itemSetup.SkinID);

                if (amount < find.Price)
                {
                    player.ChatMessage("У вас недостаточно баланса для покупки!");
                    return;
                }
                Take(player.inventory.AllItems(), config._itemSetup.ShortName, config._itemSetup.SkinID, find.Price);
                if (find.TypeItem == TypeItem.Command)
                {
                    Server.Command(find.Command.Replace("%STEAMID%", player.UserIDString));
                }
                else if (find.TypeItem == TypeItem.Storage)
                {
                    var data = _playerData[player.userID];
                    data.Add(new StorageList
                    {
                        PrefabSpawn = find.PrefabName,
                        Image = find.Image,
                        UniversalNumber = UnityEngine.Random.Range(Int32.MinValue, Int32.MaxValue)
                    });
                    FNotification?.CallHook("SendNotify", player, "Вы получили кейс!",
                        "Что бы забрать кейс - /cstore", find.Image, "0 0", "0 0", 10);
                }
                else if (find.TypeItem == TypeItem.Item)
                {
                    var item = ItemManager.CreateByName(find.ShortName, find.Amount);
                    player.GiveItem(item);
                }

                var container = new CuiElementContainer();

                CuiHelper.DestroyUi(player, Layer + ".Balance");
                
                container.Add(new CuiButton 
                {
                    RectTransform = { AnchorMin = "0.07135417 0.1796296", AnchorMax = "0.1765625 0.2259259"},
                    Button =
                    {
                        Command = $"",
                        Color = HexToCuiColor("#4094b9"), Sprite = "assets/content/ui/ui.background.tile.psd"
                    },
                    Text =
                    {
                        Text = $"Баланс: {GetAmount(player.inventory.AllItems(), config._itemSetup.ShortName, config._itemSetup.SkinID)}", Color = HexToCuiColor("#fcf7f6"),
                        Align = TextAnchor.MiddleCenter, FontSize = 16, Font = "robotocondensed-bold.ttf"
                    },

                }, Layer, Layer + ".Balance");
                

                CuiHelper.AddUi(player, container);
            }
        }


        [ChatCommand("cstore")]
        void StoreOpen(BasePlayer player, string command, string[] args)
        {
            CuiHelper.DestroyUi(player, Layer);
            
            CuiElementContainer container = new CuiElementContainer();

            #region Panel | Color Panel

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Image = { Color = "0 0 0 0.9", Material = "assets/content/ui/uibackgroundblur.mat"}
            }, "Overlay", Layer);
            
            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiImageComponent { Color = "0 0 0 0.9", Sprite = "assets/content/ui/ui.background.transparent.radial.psd" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" }
                }
            });

            #endregion

            #region Close

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "-100 -100", AnchorMax = "100 100", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Close = Layer},
                Text = { Text = "" }
            }, Layer);

            #endregion
            
            
            #region Panel Main

            
            container.Add(new CuiButton 
            {
                RectTransform = { AnchorMin = "0.1927083 0.1972222", AnchorMax = "0.8088542 0.8055556"},
                Button =
                {
                    Command = $"",
                    Color = HexToCuiColor("#fffdfc", 5), Material = "assets/content/ui/uibackgroundblur.mat"
                },
                Text =
                {
                    Text = "", Color = HexToCuiColor("#fffdfc"),
                    Align = TextAnchor.MiddleLeft, FontSize = 13, Font = "robotocondensed-bold.ttf"
                },

            }, Layer);

            #endregion


            CuiHelper.AddUi(player, container);

            OpenInventoryPage(player, 1);
        }

        #endregion
        
        
        #region Configuration


        private PluginConfig config;

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
            if (config.PluginVersion < new VersionNumber(1, 0, 0))
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

        public class ItemSetup
        {
            [JsonProperty("ShortName")] 
            public string ShortName;

            [JsonProperty("SkinID")] 
            public ulong SkinID;
        }

        public enum TypeItem
        {
            Storage = 1,
            Command = 2,
            Item = 3,
        }


        public class ItemSettings
        {
            [JsonProperty("ShortName")]         public string ShortName;

            [JsonProperty("Min Amount")]           public int MinAmount;

            [JsonProperty("Max Amount")]           public int MaxAmount;

            [JsonProperty("SkinID")]                public ulong SkinID;

            [JsonProperty("DisplayName")]     public string DisplayName;


            public Item GiveItem()
            {
                var item = ItemManager.CreateByName(ShortName, UnityEngine.Random.Range(MinAmount, MaxAmount), SkinID);
                if (!string.IsNullOrEmpty(DisplayName))
                    item.name = DisplayName;

                return item;
            }
        }




        public class SettingsCase
        {
            [JsonProperty("DisplayName")] 
            public string DisplayName;
            
            [JsonProperty("Type ( 1 - Storage, 2 - Command, 3 - Item )")] 
            public TypeItem TypeItem;

            [JsonProperty("ShortName ( Type == 3 )")] 
            public string ShortName;

            [JsonProperty("Amount ( Type == 3 )")] 
            public int Amount;

            [JsonProperty("PrefabName ( Type == 1 )")]
            public string PrefabName;

            [JsonProperty("Image")] 
            public string Image;

            [JsonProperty("Command ( Type == 2 )")] 
            public string Command;

            [JsonProperty("Price")] 
            public int Price;
            
            [JsonProperty("Минимальное количество предметов, которые могут выпасть из ящика ( Type == 1 )")]
            public int CapacityMin;
            
            [JsonProperty("Максимальное количество предметов, которые могут выпасть из ящика ( Type == 1 )")]
            public int CapacityMax;

            [JsonProperty("Предметы, которые могут выпасть из ящика ( Type == 1 )")]
            public List<ItemSettings> _itemList = new List<ItemSettings>();

        }


        private class PluginConfig
        {
            [JsonProperty("Настройка предмета, за которую будут покупаться различные товары")]
            public ItemSetup _itemSetup = new ItemSetup();

            [JsonProperty("Настройка продажи товаров")]
            public List<SettingsCase> _SettingsCase = new List<SettingsCase>();

            [JsonProperty("Версия конфигурации")]
            public VersionNumber PluginVersion = new VersionNumber();

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    _itemSetup = new ItemSetup()
                    {
                        ShortName = "bleach",
                        SkinID = 2916405375,
                    },
                    _SettingsCase = new List<SettingsCase>()
                    {
                        new SettingsCase()
                        {
                            DisplayName = "Кейс для переноски",
                            TypeItem = TypeItem.Command,
                            ShortName = "",
                            Amount = 1,
                            PrefabName = "",
                            Image = "https://i.imgur.com/2P2lU8V.png",
                            Command = "uranum.give case %STEAMID%",
                            Price = 1,
                            CapacityMin = 1,
                            CapacityMax = 1,
                            _itemList = new List<ItemSettings>()
                        },
                        new SettingsCase()
                        {
                            DisplayName = "Ящик с вертолета",
                            TypeItem = TypeItem.Storage,
                            ShortName = "",
                            Amount = 1,
                            PrefabName = "assets/bundled/prefabs/radtown/crate_normal.prefab",
                            Image = "https://i.imgur.com/2P2lU8V.png",
                            Command = "",
                            Price = 100,
                            CapacityMin = 2,
                            CapacityMax = 4,
                            _itemList = new List<ItemSettings>()
                            {
                                new ItemSettings()
                                {
                                    ShortName = "gunpowder",
                                    MinAmount = 1500,
                                    MaxAmount = 3500,
                                    SkinID = 0,
                                    DisplayName = "",
                                },
                                new ItemSettings()
                                {
                                    ShortName = "sulfur",
                                    MinAmount = 3000,
                                    MaxAmount = 6000,
                                    SkinID = 0,
                                    DisplayName = "",
                                },
                                new ItemSettings()
                                {
                                    ShortName = "metal.fragments",
                                    MinAmount = 1500,
                                    MaxAmount = 3000,
                                    SkinID = 0,
                                    DisplayName = ""
                                }
                            }
                        },
                        new SettingsCase()
                        {
                            DisplayName = "Элитный ящик",
                            TypeItem = TypeItem.Storage,
                            ShortName = "",
                            Amount = 1,
                            PrefabName = "assets/bundled/prefabs/radtown/crate_normal.prefab",
                            Image = "https://i.imgur.com/2P2lU8V.png",
                            Command = "",
                            Price = 25,
                            CapacityMin = 2,
                            CapacityMax = 4,
                            _itemList = new List<ItemSettings>()
                            {
                                new ItemSettings()
                                {
                                    ShortName = "gunpowder",
                                    MinAmount = 1500,
                                    MaxAmount = 3500,
                                    SkinID = 0,
                                    DisplayName = "",
                                },
                                new ItemSettings()
                                {
                                    ShortName = "sulfur",
                                    MinAmount = 3000,
                                    MaxAmount = 6000,
                                    SkinID = 0,
                                    DisplayName = "",
                                },
                                new ItemSettings()
                                {
                                    ShortName = "metal.fragments",
                                    MinAmount = 1500,
                                    MaxAmount = 3000,
                                    SkinID = 0,
                                    DisplayName = ""
                                }
                            }
                        },
                        new SettingsCase()
                        {
                            DisplayName = "Ящик с танка",
                            TypeItem = TypeItem.Storage,
                            ShortName = "",
                            Amount = 1,
                            PrefabName = "assets/bundled/prefabs/radtown/crate_normal.prefab",
                            Image = "https://i.imgur.com/2P2lU8V.png",
                            Command = "",
                            Price = 25,
                            CapacityMin = 2,
                            CapacityMax = 4,
                            _itemList = new List<ItemSettings>()
                            {
                                new ItemSettings()
                                {
                                    ShortName = "gunpowder",
                                    MinAmount = 1500,
                                    MaxAmount = 3500,
                                    SkinID = 0,
                                    DisplayName = "",
                                },
                                new ItemSettings()
                                {
                                    ShortName = "sulfur",
                                    MinAmount = 3000,
                                    MaxAmount = 6000,
                                    SkinID = 0,
                                    DisplayName = "",
                                },
                                new ItemSettings()
                                {
                                    ShortName = "metal.fragments",
                                    MinAmount = 1500,
                                    MaxAmount = 3000,
                                    SkinID = 0,
                                    DisplayName = ""
                                }
                            }
                        },
                        new SettingsCase()
                        {
                            DisplayName = "Ящик с компонентами",
                            TypeItem = TypeItem.Storage,
                            ShortName = "",
                            Amount = 1,
                            PrefabName = "assets/bundled/prefabs/radtown/crate_normal.prefab",
                            Image = "https://i.imgur.com/2P2lU8V.png",
                            Command = "",
                            Price = 10,
                            CapacityMin = 2,
                            CapacityMax = 4,
                            _itemList = new List<ItemSettings>()
                            {
                                new ItemSettings()
                                {
                                    ShortName = "gunpowder",
                                    MinAmount = 1500,
                                    MaxAmount = 3500,
                                    SkinID = 0,
                                    DisplayName = "",
                                },
                                new ItemSettings()
                                {
                                    ShortName = "sulfur",
                                    MinAmount = 3000,
                                    MaxAmount = 6000,
                                    SkinID = 0,
                                    DisplayName = "",
                                },
                                new ItemSettings()
                                {
                                    ShortName = "metal.fragments",
                                    MinAmount = 1500,
                                    MaxAmount = 3000,
                                    SkinID = 0,
                                    DisplayName = ""
                                }
                            }
                        },
                        new SettingsCase()
                        {
                            DisplayName = "Ящик с ресурсами",
                            TypeItem = TypeItem.Storage,
                            ShortName = "",
                            Amount = 1,
                            PrefabName = "assets/bundled/prefabs/radtown/crate_normal.prefab",
                            Image = "https://i.imgur.com/2P2lU8V.png",
                            Command = "",
                            Price = 20,
                            CapacityMin = 2,
                            CapacityMax = 4,
                            _itemList = new List<ItemSettings>()
                            {
                                new ItemSettings()
                                {
                                    ShortName = "gunpowder",
                                    MinAmount = 1500,
                                    MaxAmount = 3500,
                                    SkinID = 0,
                                    DisplayName = "",
                                },
                                new ItemSettings()
                                {
                                    ShortName = "sulfur",
                                    MinAmount = 3000,
                                    MaxAmount = 6000,
                                    SkinID = 0,
                                    DisplayName = "",
                                },
                                new ItemSettings()
                                {
                                    ShortName = "metal.fragments",
                                    MinAmount = 1500,
                                    MaxAmount = 3000,
                                    SkinID = 0,
                                    DisplayName = ""
                                }
                            }
                        },
                        new SettingsCase()
                        {
                            DisplayName = "Ящик ученых",
                            TypeItem = TypeItem.Storage,
                            ShortName = "",
                            Amount = 1,
                            PrefabName = "assets/bundled/prefabs/radtown/crate_normal.prefab",
                            Image = "https://i.imgur.com/2P2lU8V.png",
                            Command = "",
                            Price = 25,
                            CapacityMin = 2,
                            CapacityMax = 4,
                            _itemList = new List<ItemSettings>()
                            {
                                new ItemSettings()
                                {
                                    ShortName = "gunpowder",
                                    MinAmount = 1500,
                                    MaxAmount = 3500,
                                    SkinID = 0,
                                    DisplayName = "",
                                },
                                new ItemSettings()
                                {
                                    ShortName = "sulfur",
                                    MinAmount = 3000,
                                    MaxAmount = 6000,
                                    SkinID = 0,
                                    DisplayName = "",
                                },
                                new ItemSettings()
                                {
                                    ShortName = "metal.fragments",
                                    MinAmount = 1500,
                                    MaxAmount = 3000,
                                    SkinID = 0,
                                    DisplayName = ""
                                }
                            }
                        },
                    },
                    PluginVersion = new VersionNumber(),
                };
            }
        }

        #endregion
        
    }
}