using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Facepunch.Extend;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("BattlePass","Netrunner","1.0")]
    public class BattlePass : RustPlugin
    {
        #region Fields

        [PluginReference] private Plugin ImageLibrary;
        
        enum RewardStatus
        {
            Locked,
            Open,
            NotClaimed,
            Claimed
        }

        enum RewardType
        {
            Item,
            Blueprint,
            Command
        }
        
        //Item (shortname skinID amount)
        //Blueprint (itemID)
        //Command (command)

        #endregion

        #region Config

        class Level
        {
            [JsonProperty("Имеет бесплатную награду")]
            public bool HasFreeReward = false;
            [JsonProperty("Настройку бесплатной награды")]
            public RewardSettings FreeReward = new RewardSettings();
            [JsonProperty("Имеет платную награду")]
            public bool HasPaidReward = false;
            [JsonProperty("Настройку платной награды")]
            public RewardSettings PaidReward;
        }
        class ExpSettings
        {
            [JsonProperty("Опыт за животных")] 
            public int ExpAnimal = 2;
            [JsonProperty("Опыт за NPC")] 
            public int ExpNPC = 2;
            [JsonProperty("Опыт за бочку")] 
            public int ExpBarrel = 2;
            [JsonProperty("Опыт за добычу")] 
            public int ExpGather = 2;
            [JsonProperty("Опыт за вертолет")] 
            public int ExpHeli = 2;
            [JsonProperty("Опыт за танк")] 
            public int ExpBradly = 2;
            
            
        }

        class RewardSettings
        {
            [JsonProperty("Название награды")] 
            public string Name = "Награда";
            [JsonProperty("Описание награды")] 
            public string Description = "Описание";
            [JsonProperty("Тип награды")] 
            public RewardType Type = RewardType.Item;
            [JsonProperty("Настройка награды")] 
            public string Data = "sulfur 0 1";
            [JsonProperty("Изображение")] 
            public string Image = "https://rustlabs.com/img/items180/sulfur.png";
        }
        
        static Configuration config = new Configuration();

        class Configuration
        {
            
            [JsonProperty("Максимальный уровень")] 
            public int MaxLvl;
            
            [JsonProperty("Опыт за животных")] 
            public int ExpAnimal;
            [JsonProperty("Опыт за NPC")] 
            public int ExpNPC;
            [JsonProperty("Опыт за бочку")] 
            public int ExpBarrel;
            [JsonProperty("Опыт за добычу")] 
            public int ExpGather;
            [JsonProperty("Опыт за вертолет")] 
            public int ExpHeli;
            [JsonProperty("Опыт за танк")] 
            public int ExpBradly;
            
            [JsonProperty("Настройка уровней")] 
            public Dictionary<int,Level>Levels;
            
            
            /*[JsonProperty("Настройка ежедневных миссий")] 
            public List<MisssionSettings>Daily;
            
            [JsonProperty("Настройка еженедельных миссий")] 
            public List<MisssionSettings>Weekly;
            
            [JsonProperty("Настройка разовых миссий")] 
            public List<MisssionSettings>Single;*/
            
            public static Configuration GetNewConfiguration()
            {
                return new Configuration
                {
                    MaxLvl = 20,
                    ExpAnimal = 2, 
                    ExpNPC = 2, 
                    ExpBarrel = 2,
                    ExpGather = 2,
                    ExpHeli = 2,
                    ExpBradly = 2,
                    Levels = new Dictionary<int, Level>
                    {
                        [1] = new Level(),
                        [1] = new Level(),
                    },
                    
                    /*Daily = new List<MisssionSettings>{new MisssionSettings()},
                    Weekly = new List<MisssionSettings>{new MisssionSettings()},
                    Single = new List<MisssionSettings>{new MisssionSettings()}*/
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
        
        class LevelInfo
        {
            public RewardStatus freeStatus;
            
            public RewardStatus paidStatus;
        }

        Dictionary<ulong,PlayerData>_playerData = new Dictionary<ulong, PlayerData>();
        class PlayerData
        {
            public ulong PlayerID; 
            public int CurrentLvl = 0;

            public int CurrentXP = 0;

            public bool isPaidPlayer = false;
            
            public Dictionary<int,LevelInfo> Rewards = new Dictionary<int,LevelInfo>();
            
            /*public List<Mission> DailyMissions = new List<Mission>();
            
            public List<Mission> WeeklyMissions = new List<Mission>();

            public List<Mission> SingleMissions = new List<Mission>();*/


            public void UpdateRewardInfo()
            {
                foreach (var level in config.Levels)
                    if (!Rewards.ContainsKey(level.Key))
                        Rewards.Add(level.Key,new LevelInfo
                        {
                            freeStatus = RewardStatus.Open,
                            paidStatus = RewardStatus.Locked
                        });
            }
            
            #region XP
            
            public void GainXP(int amount)
            {
                if (CurrentXP+amount<1000) CurrentXP += amount;
                else
                {
                    int extra = CurrentXP + amount - 1000;
                    UpdateLvl(1,extra);
                }
            }

            public void UpdateLvl(int amount, int extraXP = 0)
            {
                CurrentLvl += amount;
                OpenLevelRewards(CurrentLvl);
                CurrentXP = 0 + extraXP;
            }

            void OpenLevelRewards(int lvl)
            {
                if (config.Levels[lvl].HasFreeReward) Rewards[lvl].freeStatus = RewardStatus.NotClaimed;

                if (config.Levels[lvl].HasPaidReward)
                    if (isPaidPlayer) Rewards[lvl].paidStatus = RewardStatus.NotClaimed;
            }

            #endregion

            #region PlayerInfo

            public void InitPlayerData()
            {
                Rewards = new Dictionary<int, LevelInfo>();
                foreach (var level in config.Levels)
                    Rewards.Add(level.Key,new LevelInfo
                    {
                        freeStatus = RewardStatus.Open,
                        paidStatus = RewardStatus.Locked
                    });
                /*UpdateDaily();
                UpdateWeekly();
                InitSingle();*/
            }

            public void UnlockPaidRewards()
            {
                foreach (var level in Rewards.Values) level.paidStatus = RewardStatus.Open;
                foreach (var level in Rewards.Values.Take(CurrentLvl)) level.paidStatus = RewardStatus.NotClaimed;
            }

            #endregion

        }

        void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("BattlePass/Players",_playerData);
        }

        void LoadData()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile("BattlePass/Players")) _playerData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerData>>("BattlePass/Players");
        }

        #endregion

        #region Hooks

        void OnServerInitialized()
        {
            AddDefaultImages();
            foreach (var level in config.Levels.Values)
            {
                if (level.HasFreeReward) ImageLibrary.Call("AddImage",level.FreeReward.Image,level.FreeReward.Image);

                if (level.HasPaidReward) ImageLibrary.Call("AddImage",level.PaidReward.Image,level.PaidReward.Image);
            }

            LoadData();
            foreach (var player in BasePlayer.activePlayerList) OnPlayerConnected(player);
            SaveData();
        }
        
        void OnPlayerConnected(BasePlayer player)
        {
            if (!_playerData.ContainsKey(player.userID))
            {
                _playerData.Add(player.userID,new PlayerData());
                _playerData[player.userID].InitPlayerData();
            }
            _playerData[player.userID].UpdateRewardInfo();
        }
        
        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (info == null) return;
            if (info.InitiatorPlayer == null || entity == null) return;
            if (!_playerData.ContainsKey(info.InitiatorPlayer.userID)) return;

            BasePlayer player = info.InitiatorPlayer;
            if (player == null || !_playerData.ContainsKey(player.userID)) return;
            if (entity.name.Contains("barrel")) _playerData[player.userID].GainXP(config.ExpBarrel);

            if (entity is BaseAnimalNPC) _playerData[player.userID].GainXP(config.ExpAnimal);

            if (entity is ScarecrowNPC || entity is NPCDwelling || entity is ScientistNPC) _playerData[player.userID].GainXP(config.ExpNPC);

            if (entity is BradleyAPC) _playerData[player.userID].GainXP(config.ExpBradly);

            if (entity is BaseHelicopter) _playerData[player.userID].GainXP(config.ExpHeli);
            
        }

        void Unload()
        {
            SaveData();
        }

        #endregion


        #region Methods

        void AddDefaultImages() 
        {
            ImageLibrary.Call("AddImage", "https://i.imgur.com/AEWjmy1.png", "claimed");
            ImageLibrary.Call("AddImage", "https://i.imgur.com/vqf3UnA.png", "notclaimed");
            ImageLibrary.Call("AddImage", "https://i.imgur.com/vU0S6Lc.png", "lock");
            
            ImageLibrary.Call("AddImage", "https://i.imgur.com/NExeF1X.png", "logo");
            ImageLibrary.Call("AddImage", "https://i.ibb.co/1GcSypW/back1.png", "lback");
            ImageLibrary.Call("AddImage", "https://i.imgur.com/QcMLq8j.png", "expback");
            ImageLibrary.Call("AddImage", "https://i.imgur.com/QcMLq8j.png", "layer");
            ImageLibrary.Call("AddImage", "https://i.imgur.com/SbuDZ8Z.png", "rBack");
            ImageLibrary.Call("AddImage", "https://i.imgur.com/sn0PkQy.png", "rIcon");
            
            ImageLibrary.Call("AddImage", "https://i.imgur.com/noMTzFe.png", "bar");
        }
        
        int GetPlayerPage(BasePlayer player)
        {
            int page = (int) Math.Floor((double) (_playerData[player.userID].CurrentLvl / 10)+1);
            return page;
        }

        void ClaimReward(BasePlayer player, RewardSettings reward,int lvl,bool paid = false)
        {
            
            var data = reward.Data.Split(' ');
            if (paid)
            {
                switch (reward.Type)
                {
                    case RewardType.Item:
                        Item item = ItemManager.CreateByPartialName(data[0], data[2].ToInt());
                        if (data[1] != "0") item.skin = Convert.ToUInt64(data[1]);
                        /*if (!player.inventory.GiveItem(item))
                        {
                            SendReply(player,$"Недостаточно места");
                            return;
                        }*/
                        player.GiveItem(item);
                        item.MarkDirty();
                        _playerData[player.userID].Rewards[lvl].paidStatus = RewardStatus.Claimed;
                        break;
                    case RewardType.Blueprint:
                        Item targetitem = ItemManager.CreateByPartialName(data[0], data[2].ToInt());
                        Item blueprint = ItemManager.CreateByItemID(-996920608);
                        blueprint.blueprintTarget = targetitem.info.itemid;
                        /*if (!player.inventory.GiveItem(blueprint))
                        {
                            SendReply(player,$"Недостаточно места");
                            return;
                        }*/
                        player.GiveItem(blueprint);
                        _playerData[player.userID].Rewards[lvl].paidStatus = RewardStatus.Claimed;
                        break;
                    case RewardType.Command:
					PrintWarning(reward.Data);
					string rewards = reward.Data.Replace("userid",player.UserIDString);
                        Server.Command(rewards);
						PrintWarning(rewards);
                        _playerData[player.userID].Rewards[lvl].paidStatus = RewardStatus.Claimed;
                        break;
                }
            }
            else
            {
                PrintWarning($"[{data.Length}]{data[0]} {data[1]} {data[2]}");
                switch (reward.Type)
                {
                    case RewardType.Item:
                        Item item = ItemManager.CreateByPartialName(data[0], data[2].ToInt());
                        if (data[1] != "0") item.skin = Convert.ToUInt64(data[1]);

                        if (!player.inventory.GiveItem(item))
                        {
                            SendReply(player,$"Недостаточно места");
                            return;
                        }
                        player.GiveItem(item);
                        item.MarkDirty();
                        _playerData[player.userID].Rewards[lvl].freeStatus = RewardStatus.Claimed;
                        break;
                    case RewardType.Blueprint:
                        Item targetitem = ItemManager.CreateByPartialName(data[0], data[2].ToInt());
                        Item blueprint = ItemManager.CreateByItemID(-996920608);
                        blueprint.blueprintTarget = targetitem.info.itemid;
                        if (!player.inventory.GiveItem(blueprint))
                        {
                            SendReply(player,$"Недостаточно места");
                            return;
                        }
                        player.GiveItem(blueprint);
                        _playerData[player.userID].Rewards[lvl].freeStatus = RewardStatus.Claimed;
                        break;
                    case RewardType.Command:
                        Server.Command(reward.Data.Replace("userid",player.UserIDString));
                        _playerData[player.userID].Rewards[lvl].freeStatus = RewardStatus.Claimed;
                        break;
                }
            }
            
            int page = (int) Math.Floor((double) (lvl / 10) + 1);
            BattlePassUI(player,page);
        }

        #endregion

        #region UI

        private string _layer = "BattlePassUI";

        void BattlePassUI(BasePlayer player,int page = 1)
        {
            if (!_playerData.ContainsKey(player.userID))
            {
                PrintError("No player Data");
                return;
            }
            CuiHelper.DestroyUi(player, _layer);
            CuiElementContainer container = new CuiElementContainer();

            /*container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = {Color = HexToCuiColor("#d3cba8")},
                RectTransform = {AnchorMin = "0.5 0.5",AnchorMax = "0.5 0.5",OffsetMin = "-500 -150",OffsetMax = "500 150"}
            }, "Overlay", _layer);*/
            container.Add(new CuiElement
            {
                Parent = "ContentUI",
                Name = _layer,
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.5",AnchorMax = "0.5 0.5",OffsetMin = "-500 -150",OffsetMax = "500 150"
                    },
                    new CuiRawImageComponent{Png = (string) ImageLibrary.Call("GetImage","layer")},
                    new CuiNeedsCursorComponent()
                }
            });
            
            container.Add(new CuiPanel
            {
                Image = {Color = "0 0 0 0"},
                RectTransform = {AnchorMin = "0 1",AnchorMax = "0 1",OffsetMin = "0 5",OffsetMax = "100 105"}
            }, _layer,"Logo");

            container.Add(new CuiElement
            {
                Parent = "Logo",
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",AnchorMax = "1 1"
                    },
                    new CuiRawImageComponent{Png = (string) ImageLibrary.Call("GetImage","logo")}
                }
            });

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0 0",AnchorMax = "1 1"},
                Text = {Align = TextAnchor.MiddleCenter,FontSize = 45,Text = _playerData[player.userID].CurrentLvl.ToString(),Color = HexToCuiColor("#cecece")},
            }, "Logo", "PlayerLevel");

            container.Add(new CuiElement
            {
                Parent = "PlayerLevel",
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "1 0",AnchorMax = "1 0",OffsetMin = "-10 -3",OffsetMax = "145 42"
                    },
                    new CuiRawImageComponent{Png = (string) ImageLibrary.Call("GetImage","lback")}
                }
            });

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "1 0",AnchorMax = "1 0",OffsetMin = "0 0",OffsetMax = "150 60"},
                Text = {Align = TextAnchor.LowerLeft,Color = HexToCuiColor("#cecece"),FontSize = 35,Text = "УРОВЕНЬ"}
            }, "Logo","TextLevel");


            #region EXP AND Buttons

            /*container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "1 1",AnchorMax = "1 1",OffsetMin = "-400 0",OffsetMax = "5 40"},
                Image = {Color = "0 0 0 0.5"}
            }, _layer, "ExpLayer");*/

            container.Add(new CuiElement
            {
                Parent = _layer,
                Name = "ExpLayer",
                Components =
                {
                    new CuiRectTransformComponent{AnchorMin = "1 1",AnchorMax = "1 1",OffsetMin = "-400 -0.5",OffsetMax = "0 40"},
                    new CuiRawImageComponent{Png = (string) ImageLibrary.Call("GetImage","expback")}
                }
            });

            /*container.Add(new CuiPanel
            {
                Image = {Color = "1 0 0 0.6"},
                RectTransform = {AnchorMin = "0 0",AnchorMax = "0 0",OffsetMin = "10 10",OffsetMax = "250 30"}
            }, "ExpLayer", "ExpBar");*/
            container.Add(new CuiElement
            {
                Parent = "ExpLayer",
                Name = "ExpBar",
                Components =
                {
                    new CuiRectTransformComponent{AnchorMin = "0 0",AnchorMax = "0 0",OffsetMin = "10 10",OffsetMax = "250 30"},
                    new CuiRawImageComponent{Png = (string) ImageLibrary.Call("GetImage","bar")}
                }
            });
            
            container.Add(new CuiPanel
            {
                Image = {Color = HexToCuiColor("#fceb80c0"),Material = "assets/icons/greyout.mat"},
                RectTransform = {AnchorMin = "0 0",AnchorMax = $"{(double)_playerData[player.userID].CurrentXP/1000} 1",OffsetMin = "4 2",OffsetMax = "-4 -2"}
            }, "ExpBar","PlayerExp");
            
            //PrintWarning($"{_playerData[player.userID].CurrentXP/1000}");
            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "1 0.5",AnchorMax = "1 0.5",OffsetMin = "-125 -15",OffsetMax = "-5 15"},
                Text = {Text = $"{_playerData[player.userID].CurrentXP}/1000",FontSize = 21,Align = TextAnchor.MiddleRight}
            }, "ExpLayer", "ExpText");
            /*container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "1 0",AnchorMax = "1 0",OffsetMin = "-200 -32",OffsetMax = "-120 -2"},
                Button = {Color = HexToCuiColor("#948b38"),Command = "bpquest",Close = _layer},
                Text = {Align = TextAnchor.MiddleCenter,FontSize = 21,Text = "Миссии"}
            }, _layer);*/

            #endregion
            
            double startx= 27, padding = 5, height = 125, lenght = 90;
            
            //int lvl = 
            
            for (int i = page*10-9; i < page*10+1; i++)
            {
                Level level = config.Levels[i];
                /*container.Add(new CuiPanel
                {
                    Image = {Color = HexToCuiColor("#353435")},
                    RectTransform = {AnchorMin = "0 0",AnchorMax = "0 0",OffsetMin = $"{startx} 0",OffsetMax = $"{startx+lenght} 300"},
                }, _layer,$"Level{i}");*/
                
                container.Add(new CuiElement
                {
                    Parent = _layer,
                    Name = $"Level{i}",
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",AnchorMax = "0 0",OffsetMin = $"{startx} 2",OffsetMax = $"{startx+lenght} 298"
                        },
                        new CuiRawImageComponent{Png = (string) ImageLibrary.Call("GetImage","rBack")}
                    }
                });
                

                container.Add(new CuiLabel
                {
                    RectTransform = {AnchorMin = "0 1",AnchorMax = "1 1",OffsetMin = "0 -20",OffsetMax = "0 0"},
                    Text = {Text = $"Lvl. {i}",FontSize = 16,Align = TextAnchor.UpperCenter}
                }, $"Level{i}");


                #region FreeReward

                container.Add(new CuiPanel
                {
                    //Image = {Color = HexToCuiColor("#aba4a4")},
                    Image = {Color = "0 0 0 0"},
                    RectTransform = {AnchorMin = "0 0.5",AnchorMax = "0 0.5",OffsetMin = $"{0} {0}",OffsetMax = $"{lenght} {height}"}
                },$"Level{i}", $"Free{i}");
                if (level.HasFreeReward)
                {
                    container.Add(new CuiElement
                    {
                        Parent = $"Free{i}",
                        Components =
                        {
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "15 -75", OffsetMax = "75 -15"
                            },
                            new CuiRawImageComponent
                            {
                                Png = (string) ImageLibrary.Call("GetImage", "rIcon")
                            }
                        }
                    });
                    container.Add(new CuiElement
                    {
                        Parent = $"Free{i}",
                        Name = $"fReward{i}",
                        Components =
                        {
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "15 -75", OffsetMax = "75 -15"
                            },
                            new CuiRawImageComponent
                            {
                                Png = (string) ImageLibrary.Call("GetImage", level.FreeReward.Image)
                            }
                        }
                    });

                    switch (_playerData[player.userID].Rewards[i].freeStatus)
                    {
                        case RewardStatus.Locked:
                            container.Add(new CuiElement
                            {
                                Parent = $"fReward{i}",
                                Components =
                                {
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-20 -20", OffsetMax = "10 10"
                                    },
                                    new CuiRawImageComponent {Png = (string) ImageLibrary.Call("GetImage", "lock")}
                                }
                            });
                            break;
                        case RewardStatus.Claimed:
                            container.Add(new CuiElement
                            {
                                Parent = $"fReward{i}",
                                Components =
                                {
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-15 -15", OffsetMax = "15 15"
                                    },
                                    new CuiRawImageComponent {Png = (string) ImageLibrary.Call("GetImage", "claimed")}
                                }
                            });
                            break;
                        case RewardStatus.NotClaimed:
                            container.Add(new CuiElement
                            {
                                Parent = $"fReward{i}",
                                Components =
                                {
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-15 -15", OffsetMax = "15 15"
                                    },
                                    new CuiRawImageComponent {Png = (string) ImageLibrary.Call("GetImage", "notclaimed")}
                                }
                            });
                            container.Add(new CuiButton
                            {
                                RectTransform =
                                    {AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "5 5", OffsetMax = "85 30"},
                                Button = {Color = HexToCuiColor("#871717"), Command = $"claimfree {i}"},
                                Text = {Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.9", Text = "Получить"}
                            }, $"Free{i}");
                            break;
                    }
                }
                

                #endregion

                #region PaidReward

                container.Add(new CuiPanel
                {
                    //Image = {Color = HexToCuiColor("#595959")},
                    Image = {Color = "0 0 0 0"},
                    RectTransform = {AnchorMin = "0 0.5",AnchorMax = "0 0.5",OffsetMin = $"0 {-height}",OffsetMax = $"{lenght} {0}"}
                },$"Level{i}", $"Paid{i}");
                if (level.HasPaidReward)
                {
                    container.Add(new CuiElement
                    {
                        Parent = $"Paid{i}",
                        Components =
                        {
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "15 -75", OffsetMax = "75 -15"
                            },
                            new CuiRawImageComponent
                            {
                                Png = (string) ImageLibrary.Call("GetImage", "rIcon")
                            }
                        }
                    });
                    container.Add(new CuiElement
                    {
                        Parent = $"Paid{i}",
                        Name = $"pReward{i}",
                        Components =
                        {
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "15 -75", OffsetMax = "75 -15"
                            },
                            new CuiRawImageComponent
                            {
                                Png = (string) ImageLibrary.Call("GetImage", level.PaidReward.Image)
                            }
                        }
                    });

                    switch (_playerData[player.userID].Rewards[i].paidStatus)
                    {
                        case RewardStatus.Locked:
                            container.Add(new CuiElement
                            {
                                Parent = $"pReward{i}",
                                Components =
                                {
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-20 -20", OffsetMax = "10 10"
                                    },
                                    new CuiRawImageComponent {Png = (string) ImageLibrary.Call("GetImage", "lock")}
                                }
                            });
                            break;
                        case RewardStatus.Claimed:
                            container.Add(new CuiElement
                            {
                                Parent = $"pReward{i}",
                                Components =
                                {
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-15 -15", OffsetMax = "15 15"
                                    },
                                    new CuiRawImageComponent {Png = (string) ImageLibrary.Call("GetImage", "claimed")}
                                }
                            });
                            break;
                        case RewardStatus.NotClaimed:
                            container.Add(new CuiElement
                            {
                                Parent = $"pReward{i}",
                                Components =
                                {
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-15 -15", OffsetMax = "15 15"
                                    },
                                    new CuiRawImageComponent {Png = (string) ImageLibrary.Call("GetImage", "notclaimed")}
                                }
                            });
                            container.Add(new CuiButton
                            {
                                RectTransform =
                                    {AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "5 5", OffsetMax = "85 30"},
                                Button = {Color = HexToCuiColor("#871717"), Command = $"claimpaid {i}"},
                                Text = {Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.9", Text = "Получить"}
                            }, $"Paid{i}");
                            break;
                    }
                }
                #endregion

                startx += lenght + padding;

            }

            /*container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "1 0",AnchorMax = "1 0",OffsetMin = "-100 -40",OffsetMax = "-10 -5"},
                Button = {Color = HexToCuiColor("#993232"),Close = _layer},
                Text = {Align = TextAnchor.MiddleCenter,Text = "Закрыть",Color = HexToCuiColor("#d3bc8e")}
            }, _layer);*/
            if (page<config.MaxLvl/10)
                container.Add(new CuiButton
                {
                    
                    //RectTransform = {AnchorMin = "1 0",AnchorMax = "1 0",OffsetMin = "-195 -40",OffsetMax = "-105 -5"},
                    RectTransform = {AnchorMin = "1 0",AnchorMax = "1 0",OffsetMin = "-200 -40",OffsetMax = "-10 -5"},
                    Button = {Color = HexToCuiColor("#993232"),Close = _layer,Command = $"switchbpp {page+1}"},
                    Text = {Align = TextAnchor.MiddleCenter,Text = "ДАЛЕЕ",Color = HexToCuiColor("#d3bc8e")}
                }, _layer);

            if (page>1)
                container.Add(new CuiButton
                {
                    //RectTransform = {AnchorMin = "1 0",AnchorMax = "1 0",OffsetMin = "-295 -40",OffsetMax = "-200 -5"},
                    RectTransform = {AnchorMin = "1 0",AnchorMax = "1 0",OffsetMin = "-295 -40",OffsetMax = "-105 -5"},
                    Button = {Color = HexToCuiColor("#993232"),Close = _layer,Command = $"switchbpp {page-1}"},
                    Text = {Align = TextAnchor.MiddleCenter,Text = "НАЗАД",Color = HexToCuiColor("#d3bc8e")}
                }, _layer);

            CuiHelper.AddUi(player, container);
            //timer.Once(10f, ()=>CuiHelper.DestroyUi(player, _layer));
        }


        

        #endregion

        #region Commands

        [ConsoleCommand("switchbpp")]
        void BattlePassPage(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null || !arg.HasArgs()) return;
            
            BattlePassUI(arg.Player(),arg.Args[0].ToInt());
        }

        [ChatCommand("battlepass")]
        void OpenBattlePass(BasePlayer player, string command, string[] args)
        {
            BattlePassUI(player,GetPlayerPage(player));
        }

        [ConsoleCommand("openbp")]
        void BPCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            BattlePassUI(player,GetPlayerPage(player));
        }

        [ConsoleCommand("claimfree")]
        void ClaimFreeRewardCMD(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null || !arg.HasArgs()) return;
            BasePlayer player = arg.Player();
            int lvl = arg.Args[0].ToInt();
            if (_playerData[player.userID].Rewards[lvl].freeStatus == RewardStatus.NotClaimed) ClaimReward(player,config.Levels[lvl].FreeReward,lvl);
        }
        
        [ConsoleCommand("claimpaid")]
        void ClaimPaidRewardCMD(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null || !arg.HasArgs()) return;
            BasePlayer player = arg.Player();
            int lvl = arg.Args[0].ToInt();
            if (_playerData[player.userID].Rewards[lvl].paidStatus == RewardStatus.NotClaimed) ClaimReward(player,config.Levels[lvl].PaidReward,lvl,true);
        }

        [ConsoleCommand("gradebp")]
        void OpenPaidBP(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null)
            {
                if (!arg.Player().IsAdmin)
                {
                    return;
                }
            }

            ulong id = Convert.ToUInt64(arg.Args[0]);
            _playerData[id].isPaidPlayer = true;
            _playerData[id].UnlockPaidRewards();
            SaveData();
        }

        #endregion
        
        #region Helpers
        
        private static string HexToCuiColor(string hex)
        {
            if (string.IsNullOrEmpty(hex)) hex = "#FFFFFFFF";
            var str = hex.Trim('#');
            if (str.Length == 6) str += "FF";
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
            return $"{color.r:F2} {color.g:F2} {color.b:F2} {color.a:F2}";
        }

        #endregion
    }
}