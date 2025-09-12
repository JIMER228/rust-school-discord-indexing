using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Expedition","Baks","0.1")]
    public class Expedition : RustPlugin
    {
        #region Fields

        private static Expedition _;
        enum Rarity
        {
            Default,
            Rare,
            Epic,
            Legendary
        }

        [PluginReference] private Plugin ImageLibrary,EconomicsEvo;
        
        Dictionary<Rarity,string> rarityColors = new Dictionary<Rarity, string>
        {
            [Rarity.Default] = "#636365D9",
            [Rarity.Rare] = "#3e3eabD9",
            [Rarity.Epic] = "#673eabD9",
            [Rarity.Legendary] = "#dfb22fD9"
        };
        
        Dictionary<Rarity,int> _raritySettings = new Dictionary<Rarity, int>
        {
            [Rarity.Default] = 50,
            [Rarity.Rare] = 35,
            [Rarity.Epic] = 10,
            [Rarity.Legendary] = 5
        };

        #endregion

        #region Config

        class ExpeditionItem
        {
            [JsonProperty("ShortName")]
            public string ShortName = "sulfur";
            [JsonProperty("Отображаемое имя")]
            public string DisplayName = "";
            [JsonProperty("Является чертежом?")]
            public bool isBlueprint = false;
            [JsonProperty("Является командой?")]
            public bool isCommand = false;
            [JsonProperty("Скин")]
            public ulong Skin = 0;
            [JsonProperty("Количество")]
            public int Amount = 10;
            [JsonProperty("Редкость")]
            public Rarity Rarity = Rarity.Rare;
            [JsonProperty("Картинка")]
            public string Image = "https://rustlabs.com/img/items180/sulfur.png";
        }

        class ExpeditionInfo
        {
            [JsonProperty("Описание")] 
            public string Description;
            [JsonProperty("Название")]
            public string DisplayName;
            [JsonProperty("Иконка")]
            public string Icon;
            [JsonProperty("Изображение")]
            public string FullImage;
            [JsonProperty("Цена запуска")]
            public int Price = 100;
            [JsonProperty("Время(в секундах)")]
            public int Time = 100;
            [JsonProperty("Предметы")]
            public List<ExpeditionItem> Items;
        }
        
        static Configuration config = new Configuration();

        class Configuration
        {
            [JsonProperty("Ключ валюты")]
            public string CurrencyKey;
            [JsonProperty("Настройка наемников")] 
            public Dictionary<string,ExpeditionInfo> Expeditions = new Dictionary<string,ExpeditionInfo>();
            public static Configuration GetNewConfiguration()
            {
                return new Configuration
                {
                    Expeditions = new Dictionary<string,ExpeditionInfo>
                    {
                        ["merc"] = new ExpeditionInfo
                        {
                            FullImage = "https://i.ibb.co/vvkQCRr/full.png",
                            Icon = "https://i.ibb.co/fvvFBWM/icn.png",
                            Items = new List<ExpeditionItem>
                            {
                                new ExpeditionItem()
                            },
                            DisplayName = "Вампир",
                            Description = "Давный давно, в далёкой галактике и т.д. и т.п жил был вампир"
                        },
                        ["merc1"] = new ExpeditionInfo
                        {
                            FullImage = "https://i.ibb.co/vvkQCRr/full.png",
                            Icon = "https://i.ibb.co/fvvFBWM/icn.png",
                            Items = new List<ExpeditionItem>(),
                            DisplayName = "Вампир",
                            Description = "Давный давно, в далёкой галактике и т.д. и т.п жил был вампир"
                        },
                        ["merc2"] = new ExpeditionInfo
                        {
                            FullImage = "https://i.ibb.co/vvkQCRr/full.png",
                            Icon = "https://i.ibb.co/fvvFBWM/icn.png",
                            Items = new List<ExpeditionItem>(),
                            DisplayName = "Вампир",
                            Description = "Давный давно, в далёкой галактике и т.д. и т.п жил был вампир"
                        },
                        ["merc3"] = new ExpeditionInfo
                        {
                            FullImage = "https://i.ibb.co/vvkQCRr/full.png",
                            Icon = "https://i.ibb.co/fvvFBWM/icn.png",
                            Items = new List<ExpeditionItem>(),
                            DisplayName = "Вампир",
                            Description = "Давный давно, в далёкой галактике и т.д. и т.п жил был вампир"
                        },
                        ["merc4"] = new ExpeditionInfo
                        {
                            FullImage = "https://i.ibb.co/vvkQCRr/full.png",
                            Icon = "https://i.ibb.co/fvvFBWM/icn.png",
                            Items = new List<ExpeditionItem>(),
                            DisplayName = "Вампир",
                            Description = "Давный давно, в далёкой галактике и т.д. и т.п жил был вампир"
                        },
                        ["merc5"] = new ExpeditionInfo
                        {
                            FullImage = "https://i.ibb.co/vvkQCRr/full.png",
                            Icon = "https://i.ibb.co/fvvFBWM/icn.png",
                            Items = new List<ExpeditionItem>(),
                            DisplayName = "Вампир",
                            Description = "Давный давно, в далёкой галактике и т.д. и т.п жил был вампир"
                        },
                        ["merc6"] = new ExpeditionInfo
                        {
                            FullImage = "https://i.ibb.co/vvkQCRr/full.png",
                            Icon = "https://i.ibb.co/fvvFBWM/icn.png",
                            Items = new List<ExpeditionItem>(),
                            DisplayName = "Вампир",
                            Description = "Давный давно, в далёкой галактике и т.д. и т.п жил был вампир"
                        },
                        ["merc7"] = new ExpeditionInfo
                        {
                            FullImage = "https://i.ibb.co/vvkQCRr/full.png",
                            Icon = "https://i.ibb.co/fvvFBWM/icn.png",
                            Items = new List<ExpeditionItem>(),
                            DisplayName = "Вампир",
                            Description = "Давный давно, в далёкой галактике и т.д. и т.п жил был вампир"
                        },
                    } 
                };
            }
        }
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) PrintWarning("NULL");
                if (config == null) LoadDefaultConfig();
            }
            catch
            {
                PrintError($"Не удалось найти конфигурацию 'oxide/config/{Name}', Создание конфига!!");
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }
        protected override void LoadDefaultConfig() => config = Configuration.GetNewConfiguration();
        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion

        #region Data

        Dictionary<ulong, Dictionary<string, ExpeditionData>> _playerData =
            new Dictionary<ulong, Dictionary<string, ExpeditionData>>();
        class ExpeditionData
        {
            public bool IsOpen;
            
            public bool IsActive;
            
            public DateTime EndTime;
            
            public ExpeditionItem[] Inventory = new ExpeditionItem[4];


            public static ExpeditionData New()
            {
                return new ExpeditionData
                {
                    IsOpen = false,
                    EndTime = DateTime.UtcNow,
                    IsActive = false,
                    Inventory = new ExpeditionItem[4],
                };
            }

            public void EndExpedition(string key)
            {
                _.PrintWarning($"ended {key}");
                IsActive = false;
                for (int i = 0; i < 4; i++)
                {
                    Inventory[i] = GenerateItem(key);
                    _.PrintWarning(Inventory[i].DisplayName);
                }
            }

            ExpeditionItem GenerateItem(string key)
            {
                ExpeditionItem item;
                item = config.Expeditions[key].Items.Where(p => p.Rarity == GetPrizeRarity()).ToList().GetRandom();
                _.PrintError(config.Expeditions[key].Items.Where(p => p.Rarity == GetPrizeRarity()).ToList().Count.ToString());
                if (item == null)
                {
                    _.PrintError($"NULL ITEM");
                }
                else
                {
                    _.PrintWarning(item.DisplayName);
                }
                return item;
            }

            

            Rarity GetPrizeRarity()
            {
                int random = UnityEngine.Random.Range(0, 100);
                if (random<50) return Rarity.Default;
                if (random<85) return Rarity.Rare;
                if (random<95) return Rarity.Epic;
                return Rarity.Legendary;
            }
        }

        void InitPlayerData(BasePlayer player)
        {
            if (!_playerData.ContainsKey(player.userID))
            {
                _playerData.Add(player.userID,new Dictionary<string, ExpeditionData>());
                foreach (var expedition in config.Expeditions) _playerData[player.userID].Add(expedition.Key,ExpeditionData.New());
            }

            PrintWarning($"{_playerData[player.userID].ToList().GetRandom().Value.Inventory.Length}");
            if (!_expeditionPool.ContainsKey(player.userID)) _expeditionPool.Add(player.userID,new Dictionary<string, DateTime>());
        }

        void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name,_playerData);
        }

        void LoadData()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile(Name)) _playerData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, Dictionary<string, ExpeditionData>>>(Name);
        }

        #endregion

        #region Hooks

        void OnServerInitialized()
        {
            _ = this;
            LoadData();
            LoadImages();
            ImageLibrary.Call("AddImage", "https://i.ibb.co/9qkW0bs/itemwhite.png","eitem");
            ImageLibrary.Call("AddImage", "https://i.ibb.co/yFdrjmT/expBack.png","eback");
            ImageLibrary.Call("AddImage", "https://i.ibb.co/3Ng1JNd/exlock.png","elock");
            ImageLibrary.Call("AddImage", "https://rustlabs.com/img/items180/blueprintbase.png","ebp");
            InvokeHandler.Instance.InvokeRepeating(CheckExpeditionTime,10f,1f);
            foreach (var player in BasePlayer.activePlayerList) InitPlayerData(player);
        }

        void OnPlayerConnected(BasePlayer player)
        {
            InitPlayerData(player);
        }

        void Unload()
        {
            SaveData();
            InvokeHandler.Instance.CancelInvoke(CheckExpeditionTime);
        }

        #endregion

        #region Methods

        void GiveReward(BasePlayer player, string key)
        {
            ExpeditionData data = _playerData[player.userID][key];

            PrintWarning("Give reward");
            for (int i = 0; i < 4; i++)
            {
                if (data.Inventory[i] != null) GivePrize(player, data.Inventory[i]);
                data.Inventory[i] = null;
            }
            ExpeditionUI(player,key);
        }
        void GivePrize(BasePlayer player,ExpeditionItem expeditionItem)
        {
            if (expeditionItem.isCommand) Server.Command(expeditionItem.ShortName.Replace("userid",player.UserIDString));

            if (expeditionItem.isBlueprint)
            {
                Item targetitem = ItemManager.CreateByPartialName(expeditionItem.ShortName);
                Item create = ItemManager.CreateByItemID(-996920608);

                var info = ItemManager.FindItemDefinition(targetitem.info.shortname);
                create.blueprintTarget = info.itemid;
                if (player.inventory.containerMain.capacity-player.inventory.containerMain.itemList.Count<1)
                    create.DropAndTossUpwards(player.transform.position);
                else
                    player.GiveItem(create);
                return;
            }

            Item item = ItemManager.CreateByPartialName(expeditionItem.ShortName, expeditionItem.Amount,
                expeditionItem.Skin);
            player.GiveItem(item);
        }

        void LoadImages()
        {
            foreach (var expedition in config.Expeditions)
            {
                ImageLibrary.Call("AddImage", expedition.Value.Icon, $"{expedition.Key}icon");
                ImageLibrary.Call("AddImage", expedition.Value.FullImage, $"{expedition.Key}img");
                foreach (var expeditionItem in expedition.Value.Items) ImageLibrary.Call("AddImage", expeditionItem.Image, expeditionItem.Image);
            }
        }

        bool CanBuyExpedition(ulong userid,string key)
        {
            return (double) EconomicsEvo.Call("GetBalance", userid, config.CurrencyKey) >=
                   config.Expeditions[key].Price;
        }

        void BuyExpedition(ulong userid, string key)
        {
            EconomicsEvo.Call("RemoveBalanceByID", userid, config.CurrencyKey, config.Expeditions[key].Price);
            _playerData[userid][key].IsOpen = true;
            ExpeditionUI(BasePlayer.FindByID(userid),key);
        }

        bool CanStartExpedition(ulong userid,string key)
        {
            if (!_playerData[userid][key].IsOpen || _playerData[userid][key].IsActive || (double) EconomicsEvo.Call("GetBalance",userid,config.CurrencyKey)<config.Expeditions[key].Price) return false;

            return true;
        }

        string GetButtonText(ExpeditionData data)
        {
            if (!data.IsOpen) return "ПРИЗВАТЬ";

            if (data.IsActive)
                return "В ПУТИ";
            if (data.Inventory[0] != null || data.Inventory[1] != null || data.Inventory[2] != null ||
                data.Inventory[3] != null)
                return "Забрать награду";
            else
                return "ОТПРАВИТЬ В ЭКСПЕДИЦИЮ";
        }

        string GetButtonCommand(ExpeditionData data, string key)
        {
            if (!data.IsOpen) return $"exbuy {key}";
            if (data.Inventory[0] != null || data.Inventory[1] != null || data.Inventory[2] != null ||
                data.Inventory[3] != null)
                return $"exreward {key}";
            return $"exstart {key}";
        }

        #endregion

        #region Time

        Dictionary<ulong,Dictionary<string,DateTime>>_expeditionPool = new Dictionary<ulong, Dictionary<string, DateTime>>();

        void CheckExpeditionTime()
        {
            foreach (var playertime in _expeditionPool.ToList())
            foreach (var expedition in playertime.Value.ToList())
                if (expedition.Value<DateTime.UtcNow) EndPlayerExpedition(playertime.Key,expedition.Key);
        }

        void EndPlayerExpedition(ulong playerid, string key)
        {
            PrintWarning($"End expedition {playerid} : {key}");
            PrintWarning($"playerdata : {_playerData.ContainsKey(playerid)}");
            //_playerData[playerid].Remove(key);
            _expeditionPool[playerid].Remove(key);
            _playerData[playerid][key].EndExpedition(key);
            ExpeditionUI(BasePlayer.FindByID(playerid), key);
        }

        void StartExpedition(ulong userid, string key)
        {
            PrintWarning($"Start key:{key}");
            EconomicsEvo.Call("RemoveBalanceByID", userid, config.CurrencyKey, config.Expeditions[key].Price);
            DateTime endTime = DateTime.UtcNow.AddSeconds(config.Expeditions[key].Time);
            _expeditionPool[userid].Add(key,endTime);
            _playerData[userid][key].IsActive = true;
            _playerData[userid][key].EndTime = endTime;
            ExpeditionUI(BasePlayer.FindByID(userid),key);
        }
        

        #endregion

        #region UI

        private string _layer = "ExpeditionUI";

        void ChooseUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, _layer);
            CuiElementContainer container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                Image = {Color = "0 0 0 0"},
                RectTransform = {AnchorMin = "0.5 0.5",AnchorMax = "0.5 0.5",OffsetMin = "-500 -270",OffsetMax = "500 270"}
            }, "ContentUI", _layer);

            double padding = 10, x = 10, y = -250,height = 250,lenght = 210;
            foreach (var expedition in config.Expeditions)
            {
                container.Add(new CuiPanel
                {
                    RectTransform = {AnchorMin = "0 1",AnchorMax = "0 1",OffsetMin = $"{x} {y}",OffsetMax = $"{x+lenght} {y+height}"},
                    Image = {Color = "0 0 0 0"}
                }, _layer, _layer + expedition.Key);
                container.Add(new CuiElement
                {
                    Parent = _layer + expedition.Key,
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = (string) ImageLibrary.Call("GetImage","eback")
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",AnchorMax = "1 1"
                        }
                    }
                });
                
                container.Add(new CuiElement
                {
                    Parent = _layer + expedition.Key,
                    Name = $"{expedition.Key}icon",
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = (string) ImageLibrary.Call("GetImage",$"{expedition.Key}icon")
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 1",AnchorMax = "0.5 1",OffsetMin = "-80 -195",OffsetMax = "80 -35"
                        }
                    }
                });
                if (!_playerData[player.userID][expedition.Key].IsOpen)
                {
                    container.Add(new CuiElement
                    {
                        Parent  = $"{expedition.Key}icon",
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                Png = (string) ImageLibrary.Call("GetImage",$"elock")
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0",AnchorMax = "1 1"
                            }
                        }
                    });
                }
                
                

                container.Add(new CuiButton
                {
                    Button = {Color = "0 0 0 0.8",Command = $"expchoose {expedition.Key}"},
                    Text = {Text = "ПОДРОБНЕЕ",Align = TextAnchor.MiddleCenter},
                    RectTransform = {AnchorMin = "0 0",AnchorMax = "1 0",OffsetMin = "5 5",OffsetMax = "-5 25"}
                }, _layer + expedition.Key);
                container.Add(new CuiButton
                {
                    Button = {Color = "0 0 0 0"},
                    Text = {Text = expedition.Value.DisplayName,Align = TextAnchor.MiddleCenter},
                    RectTransform = {AnchorMin = "0 1",AnchorMax = "1 1",OffsetMin = "5 -25",OffsetMax = "-5 -5"}
                }, _layer + expedition.Key);
                
                x += lenght + padding;
                if (x+lenght>990)
                {
                    x = 10;
                    y -= height + padding;
                }
            }
            CuiHelper.AddUi(player, container);
        }

        void PersonalUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, _layer);
            CuiElementContainer container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                Image = {Color = "0 0 0 0"},
                RectTransform = {AnchorMin = "0.5 0.5",AnchorMax = "0.5 0.5",OffsetMin = "-500 -270",OffsetMax = "500 270"}
            }, "ContentUI", _layer);
        }

        void ExpeditionUI(BasePlayer player, string key)
        {
            if (!config.Expeditions.ContainsKey(key))
            {
                PrintError($"{key} Error config");
                return;
            }
            if (!_playerData[player.userID].ContainsKey(key))
            {
                PrintError($"{key} Error data");
                return;
            }
            ExpeditionInfo info = config.Expeditions[key];
            ExpeditionData data = _playerData[player.userID][key];
            CuiHelper.DestroyUi(player, _layer);
            CuiElementContainer container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                Image = {Color = "0 0 0 0"},
                RectTransform = {AnchorMin = "0.5 0.5",AnchorMax = "0.5 0.5",OffsetMin = "-500 -270",OffsetMax = "500 270"}
            }, "ContentUI", _layer);
            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "220 -130", OffsetMax = "640 -10"},
                Text = {Text = info.DisplayName, Align = TextAnchor.LowerLeft, FontSize = 32}
            }, _layer);
            container.Add(new CuiElement
            {
                Parent = _layer,
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Png = (string) ImageLibrary.Call("GetImage",$"{key}img")
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1",AnchorMax = "0 1",OffsetMin = "10 -500",OffsetMax = "210 -130",
                    }
                }
            });

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0 1",AnchorMax = "0 1",OffsetMin = "220 -250",OffsetMax = "640 -130"},
                Text = {Text = info.Description,Align = TextAnchor.UpperLeft,FontSize = 18}
            }, _layer);
            container.Add(new CuiLabel
            {
                Text = {Align = TextAnchor.MiddleLeft,FontSize = 26,Text = "ИНВЕНТАРЬ"},
                RectTransform = {AnchorMin = "0 1",AnchorMax = "0 1",OffsetMin = "220 -280",OffsetMax = "640 -250"}
            }, _layer);
            double x = 220, lenght = 60,padding = 10;
            for (int i = 0; i < 4; i++)
            {
                container.Add(new CuiPanel
                {
                    Image = {Color = "0 0 0 0.8"},
                    RectTransform = {AnchorMin = "0 1",AnchorMax = "0 1",OffsetMin = $"{x} -340",OffsetMax = $"{x+lenght} -280"}
                }, _layer,$"ExpSlot{i}");
                if (data.Inventory[i] != null)
                {
                    container.Add(new CuiElement
                    {
                        Parent = $"ExpSlot{i}",
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                Png = (string) ImageLibrary.Call("GetImage",data.Inventory[i].Image)
                            }
                        }
                    });
                }
                x += lenght + padding;
            }

            container.Add(new CuiButton
            {
                Button = {Command =GetButtonCommand(data,key),Color = HexToRustFormat("#f8db7d97")},
                Text = {Text = GetButtonText(data),Align = TextAnchor.MiddleCenter,Color = "0 0 0 1"},
                RectTransform = {AnchorMin = "0 1",AnchorMax = "0 1",OffsetMin = "220 -420",OffsetMax = "360 -360"}
            }, _layer,"ExBtn");
            if (data.IsActive)
            {
                container.Add(new CuiLabel
                {
                    RectTransform = {AnchorMin = "0 0",AnchorMax = "1 0.5"},
                    Text = {Text = new TimeSpan(data.EndTime.Ticks-DateTime.UtcNow.Ticks).ToShortString().Replace('-',' '),Align = TextAnchor.LowerCenter,Color = "0 0 0 1"}
                }, "ExBtn");
            }
            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0 1",AnchorMax = "0 1",OffsetMin = "370 -420",OffsetMax = "800 -360"},
                Text = {Align = TextAnchor.MiddleLeft,Text = $"СТОИМОСТЬ: {info.Price} ЭССЕНЦИИ КРОВИ",Color = HexToRustFormat("#f8db7d97")},
            }, _layer,"Price");
            container.Add(new CuiButton
            {
                Button = {Command = $"ereward {key}",Color = HexToRustFormat("#f8db7d97")},
                Text = {Text = "Возможные награды",Align = TextAnchor.MiddleCenter,Color = "0 0 0 1"},
                RectTransform = {AnchorMin = "0 1",AnchorMax = "0 1",OffsetMin = "220 -500",OffsetMax = "460 -440"}
            }, _layer);

            CuiHelper.AddUi(player, container);
        }

        void RewardUI(BasePlayer player, string key)
        {
            CuiElementContainer container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                Image = {Color = "0 0 0 0.5"},
                RectTransform = {AnchorMin = "0 0",AnchorMax = "1 1"}
            }, _layer, "ExpReward");

            container.Add(new CuiPanel
            {
                Image = {Color = HexToRustFormat("#3b3b3b")},
                RectTransform = {AnchorMin = "0.5 0.5",AnchorMax = "0.5 0.5",OffsetMin = "-360 -180",OffsetMax = "360 180"}
            }, "ExpReward", "RewardContainer");
            double padding = 10, side = 60, x = padding, y = 0 - padding - side;

            int i = 0;
            foreach (var item in config.Expeditions[key].Items.Where(p => p.Rarity == Rarity.Default))
            {
                container.Add(new CuiElement
                {
                    Parent = "RewardContainer",
                    Name = $"RItem{i}",
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = (string) ImageLibrary.Call("GetImage","eitem"),Color = HexToRustFormat("#bdb599")
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1",AnchorMax = "0 1",OffsetMin = $"{x} {y}",OffsetMax = $"{x+side} {y+side}"
                        }
                    }
                });
                if (item.isBlueprint)
                    container.Add(new CuiElement
                    {
                        Parent = $"RItem{i}",
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                Png = (string) ImageLibrary.Call("GetImage","blueprintbase")
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0",AnchorMax = "1 1",OffsetMin = $"5 5",OffsetMax = $"-5 -5"
                            }
                        }
                    });
                container.Add(new CuiElement
                {
                    Parent = $"RItem{i}",
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = (string) ImageLibrary.Call("GetImage",item.Image)
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",AnchorMax = "1 1",OffsetMin = $"5 5",OffsetMax = $"-5 -5"
                        }
                    }
                });
                x += side + padding; 
                //y += side + padding;
                i++;
                if (x+side>720)
                {
                    x = padding;
                    y -= side + padding;
                }
            }
            foreach (var item in config.Expeditions[key].Items.Where(p => p.Rarity == Rarity.Rare))
            {
                container.Add(new CuiElement
                {
                    Parent = "RewardContainer",
                    Name = $"RItem{i}",
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = (string) ImageLibrary.Call("GetImage","eitem"),Color = HexToRustFormat("#2f77ba")
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1",AnchorMax = "0 1",OffsetMin = $"{x} {y}",OffsetMax = $"{x+side} {y+side}"
                        }
                    }
                });
                if (item.isBlueprint)
                    container.Add(new CuiElement
                    {
                        Parent = $"RItem{i}",
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                Png = (string) ImageLibrary.Call("GetImage","blueprintbase")
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0",AnchorMax = "1 1",OffsetMin = $"5 5",OffsetMax = $"-5 -5"
                            }
                        }
                    });
                container.Add(new CuiElement
                {
                    Parent = $"RItem{i}",
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = (string) ImageLibrary.Call("GetImage",item.Image)
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",AnchorMax = "1 1",OffsetMin = $"5 5",OffsetMax = $"-5 -5"
                        }
                    }
                });
                x += side + padding;
                //y += side + padding;
                i++;
                if (x+side>720)
                {
                    x = padding;
                    y -= side + padding;
                }
            }
            
            foreach (var item in config.Expeditions[key].Items.Where(p => p.Rarity == Rarity.Epic))
            {
                container.Add(new CuiElement
                {
                    Parent = "RewardContainer",
                    Name = $"RItem{i}",
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = (string) ImageLibrary.Call("GetImage","eitem"),Color = HexToRustFormat("#5b2fba")
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1",AnchorMax = "0 1",OffsetMin = $"{x} {y}",OffsetMax = $"{x+side} {y+side}"
                        }
                    }
                });
                if (item.isBlueprint)
                    container.Add(new CuiElement
                    {
                        Parent = $"RItem{i}",
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                Png = (string) ImageLibrary.Call("GetImage","blueprintbase")
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0",AnchorMax = "1 1",OffsetMin = $"5 5",OffsetMax = $"-5 -5"
                            }
                        }
                    });
                container.Add(new CuiElement
                {
                    Parent = $"RItem{i}",
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = (string) ImageLibrary.Call("GetImage",item.Image)
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",AnchorMax = "1 1",OffsetMin = $"5 5",OffsetMax = $"-5 -5"
                        }
                    }
                });
                x += side + padding;
                //y += side + padding;
                i++;
                if (x+side>720)
                {
                    x = padding;
                    y -= side + padding;
                }
            }
            
            foreach (var item in config.Expeditions[key].Items.Where(p => p.Rarity == Rarity.Legendary))
            {
                container.Add(new CuiElement
                {
                    Parent = "RewardContainer",
                    Name = $"RItem{i}",
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = (string) ImageLibrary.Call("GetImage","eitem"),Color = HexToRustFormat("#ba9c2f")
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1",AnchorMax = "0 1",OffsetMin = $"{x} {y}",OffsetMax = $"{x+side} {y+side}"
                        }
                    }
                });
                if (item.isBlueprint)
                    container.Add(new CuiElement
                    {
                        Parent = $"RItem{i}",
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                Png = (string) ImageLibrary.Call("GetImage","blueprintbase")
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0",AnchorMax = "1 1",OffsetMin = $"5 5",OffsetMax = $"-5 -5"
                            }
                        }
                    });
                container.Add(new CuiElement
                {
                    Parent = $"RItem{i}",
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = (string) ImageLibrary.Call("GetImage",item.Image)
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",AnchorMax = "1 1",OffsetMin = $"5 5",OffsetMax = $"-5 -5"
                        }
                    }
                });
                x += side + padding;
                //y += side + padding;
                i++;
                if (x+side>720)
                {
                    x = padding;
                    y -= side + padding;
                }
            }

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0 1",AnchorMax = "1 1",OffsetMin = "0 2",OffsetMax = "0 40"},
                Text = {Text = $"Возможные награды",Align = TextAnchor.LowerLeft,FontSize = 24}
            }, "RewardContainer");

            container.Add(new CuiButton
            {
                Button = {Close = "ExpReward",Color = HexToRustFormat("#f8db7d97")},
                Text = {Text = "Закрыть",Align = TextAnchor.MiddleCenter,FontSize = 18,Color = "0 0 0 1"},
                RectTransform = {AnchorMin = "1 0",AnchorMax = "1 0",OffsetMin = "-120 -40",OffsetMax = "0 -2"}
            }, "RewardContainer");

            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Commands

        [ConsoleCommand("ereward")]
        void ShowRewardCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null || !arg.HasArgs()) return;
            RewardUI(arg.Player(),arg.Args[0]);
        }

        [ConsoleCommand("exreward")]
        void GivePrizeCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null || !arg.HasArgs()) return;
            GiveReward(arg.Player(),arg.Args[0]);
        }

        [ConsoleCommand("expedui")]
        void OpenUICommand(ConsoleSystem.Arg arg)
        {
            ChooseUI(arg.Player());
        }

        [ConsoleCommand("expchoose")]
        void ExpeditionUICmd(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null || !arg.HasArgs()) return;
            ExpeditionUI(arg.Player(),arg.Args[0]);
        }
        [ConsoleCommand("exstart")]
        void StartCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null || !arg.HasArgs()) return;
            if (!config.Expeditions.ContainsKey(arg.Args[0])) return;
            if (CanStartExpedition(arg.Player().userID,arg.Args[0])) StartExpedition(arg.Player().userID,arg.Args[0]);
        }

        [ConsoleCommand("exbuy")]
        void ExpeditionBuyCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null || !arg.HasArgs()) return;
            if (CanBuyExpedition(arg.Player().userID,arg.Args[0])) BuyExpedition(arg.Player().userID,arg.Args[0]);
        }

        #endregion

        #region Helper Methods

        private static string HexToRustFormat(string hex)
        {
            if (string.IsNullOrEmpty(hex))
            {
                hex = "#FFFFFFFF";
            }

            var str = hex.Trim('#');

            if (str.Length == 6)
                str += "FF";

            if (str.Length != 8)
            {
                throw new Exception(hex);
                throw new InvalidOperationException("Cannot convert a wrong format.");
            }

            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);
            var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);

            Color color = new Color32(r, g, b, a);

            return string.Format("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a);
        }

        #endregion
    }
}