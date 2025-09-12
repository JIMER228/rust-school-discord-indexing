using System.Collections.Generic;
using UnityEngine;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Plugins;
using System;
using Oxide.Core;
using System.Linq;

//Внутренняя версия 1.0.5
namespace Oxide.Plugins
{
    [Info("RustStats", "Own3r/rever", "0.3.9")]
    [Description("GUI Top and Stats")]
    class RustStats : RustPlugin
    {
        #region OtherStaff

        [PluginReference] Plugin ImageLibrary, AspectRatio;

        public string GetImage(string shortname, ulong skin = 0) => (string)ImageLibrary?.Call("GetImage", shortname, skin);
        public bool AddImage(string url, string shortname, ulong skin = 0) => (bool)ImageLibrary?.Call("AddImage", url, shortname, skin);
        public string GetUserAspectRatio(ulong userId) => (string)AspectRatio?.Call("GetUserAspectRatio", userId);

        public Dictionary<ulong, int> PVPKill = new Dictionary<ulong, int>();
        public Dictionary<ulong, int> PVPDeath = new Dictionary<ulong, int>();
        public Dictionary<ulong, int> Gather = new Dictionary<ulong, int>();
        public Dictionary<ulong, int> QuarryGather = new Dictionary<ulong, int>();
        public Dictionary<ulong, int> Time = new Dictionary<ulong, int>();

        public ulong playeruserID;

        public string KillMsg,
                      DeathMsg,
                      GatherMsg,
                      QuarryGatherMsg,
                      TimeMsg,
                      KillValue,
                      DeathValue,
                      GatherValue,
                      QuarryGatherValue,
                      TimeValue,
                      playerdisplayName;

        public int Hours, Min, iTop;

        public class DataStorage
        {
            public Dictionary<ulong, STATSDATA> RustStatsData = new Dictionary<ulong, STATSDATA>();
        }

        public class STATSDATA
        {
            public string Name;
            public int Time;
            public int Coins;
            public int Kill;
            public int KillPVE = 0;
            public int Death;
            public int PVEDeath;
            public int Suicide;

            public int Wood;
            public int Stone;
            public int SulfurOre;
            public int MetalOre;
            public int HighMetalOre;

            public int QuarryStone;
            public int QuarrySulfurOre;
            public int QuarryMetalOre;
            public int QuarryHighMetalOre;
        }

        DataStorage data;

        void OnServerInitialized()
        {
            try
            {
                ImageLibrary.Call("isLoaded", null);
            }
            catch (Exception)
            {
                PrintWarning("No Image Library.. load ImageLibrary to use this Plugin", Name);
                Interface.Oxide.UnloadPlugin(Name);
                return;
            }

            AddImage("https://i.imgur.com/FK4ku3e.png", "pickaxe");
            AddImage("https://i.imgur.com/voHQmCL.png", "quarry");
            AddImage("https://i.imgur.com/ghWa8OZ.png", "kill");
            AddImage("https://i.imgur.com/2Lr7EBf.png", "death");
            AddImage("https://i.imgur.com/UvFR1Fp.png", "time");
            AddImage("https://gspics.org/images/2020/01/19/59EvQ.png", "animal");

            LoadData();
            foreach (var player in BasePlayer.activePlayerList)
            {
                CheckUser(player);
                connect[player.userID] = DateTime.Now;
            }

            timer.Repeat(1800f, 0, () => SaveData());
        }

        void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Title, data);
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            DateTime time;
            if (!connect.TryGetValue(player.userID, out time)) return;
            data.RustStatsData[player.userID].Time += (int)(DateTime.Now - time).TotalSeconds;
            connect.Remove(player.userID);
        }

        void LoadData()
        {
            data = Interface.Oxide.DataFileSystem.ReadObject<DataStorage>(Title);
            if (data != null) return;

            data = new DataStorage();
            SaveData();
        }
        Dictionary<ulong, DateTime> connect = new Dictionary<ulong, DateTime>();
        void OnPlayerConnected(BasePlayer player)
        {
            CheckUser(player);
            connect[player.userID] = DateTime.Now;
        }

        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList) ChatDestroyGui(player);
            foreach (BasePlayer player in BasePlayer.activePlayerList) OnPlayerDisconnected(player, null);
            SaveData();
        }
        #endregion

        #region Resource Stats

        private void CheckUser(BasePlayer player)
        {
            if (!data.RustStatsData.ContainsKey(player.userID))
            {
                data.RustStatsData.Add(player.userID,
                    new STATSDATA
                    {
                        Name = player.displayName,
                        Time = 0,
                        Coins = 0,
                        Stone = 0,
                        SulfurOre = 0,
                        MetalOre = 0,
                        HighMetalOre = 0,

                        Kill = 0,
                        KillPVE = 0,
                        Death = 0,
                        PVEDeath = 0,
                        Suicide = 0,

                        QuarryStone = 0,
                        QuarryMetalOre = 0,
                        QuarrySulfurOre = 0,
                        QuarryHighMetalOre = 0
                    });
            }
            else
                data.RustStatsData[player.userID].Name = player.displayName;
        }

        private string GetName(ulong id)
        {
            if (data.RustStatsData.ContainsKey(id)) return data.RustStatsData[id].Name;
            return "Без ника";
        }

        private void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            if (item == null || entity == null) return;

            var player = entity.ToPlayer();
            if (player == null) return;

            switch (item.info.shortname)
            {
                case "wood":
                    data.RustStatsData[player.userID].Wood = data.RustStatsData[player.userID].Wood + item.amount;
                    break;
                case "stones":
                    data.RustStatsData[player.userID].Stone = data.RustStatsData[player.userID].Stone + item.amount;
                    break;
                case "sulfur.ore":
                    data.RustStatsData[player.userID].SulfurOre = data.RustStatsData[player.userID].SulfurOre + item.amount;
                    break;
                case "metal.ore":
                    data.RustStatsData[player.userID].MetalOre = data.RustStatsData[player.userID].MetalOre + item.amount;
                    break;
                case "hq.metal.ore":
                    data.RustStatsData[player.userID].HighMetalOre = data.RustStatsData[player.userID].HighMetalOre + item.amount;
                    break;
            }
        }

        private void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (player == null || item == null) return;

            switch (item.info.shortname)
            {
                case "wood":
                    data.RustStatsData[player.userID].Wood = data.RustStatsData[player.userID].Wood + item.amount;
                    break;
                case "stones":
                    data.RustStatsData[player.userID].Stone = data.RustStatsData[player.userID].Stone + item.amount;
                    break;
                case "sulfur.ore":
                    data.RustStatsData[player.userID].SulfurOre = data.RustStatsData[player.userID].SulfurOre + item.amount;
                    break;
                case "metal.ore":
                    data.RustStatsData[player.userID].MetalOre = data.RustStatsData[player.userID].MetalOre + item.amount;
                    break;
                case "hq.metal.ore":
                    data.RustStatsData[player.userID].HighMetalOre = data.RustStatsData[player.userID].HighMetalOre + item.amount;
                    break;
            }
        }

        private void OnCollectiblePickup(Item item, BasePlayer player)
        {
            if (item == null || player == null) return;

            switch (item.info.shortname)
            {
                case "wood":
                    data.RustStatsData[player.userID].Wood = data.RustStatsData[player.userID].Wood + item.amount;
                    break;
                case "stones":
                    data.RustStatsData[player.userID].Stone = data.RustStatsData[player.userID].Stone + item.amount;
                    break;
                case "sulfur.ore":
                    data.RustStatsData[player.userID].SulfurOre = data.RustStatsData[player.userID].SulfurOre + item.amount;
                    break;
                case "metal.ore":
                    data.RustStatsData[player.userID].MetalOre = data.RustStatsData[player.userID].MetalOre + item.amount;
                    break;
            }
        }

        #endregion

        #region Quarry Stats

        private void OnQuarryGather(MiningQuarry quarry, Item item)
        {
            if (quarry == null || item == null || quarry != null && quarry.OwnerID == 0) return;

            playeruserID = quarry.OwnerID;
            var player = BasePlayer.Find(playeruserID.ToString());
            if (player == null) return;

            CheckUser(player);

            switch (item.info.shortname)
            {
                case "stones":
                    data.RustStatsData[playeruserID].QuarryStone = data.RustStatsData[playeruserID].QuarryStone + item.amount;
                    break;
                case "sulfur.ore":
                    data.RustStatsData[playeruserID].QuarrySulfurOre = data.RustStatsData[playeruserID].QuarrySulfurOre + item.amount;
                    break;
                case "metal.ore":
                    data.RustStatsData[playeruserID].QuarryMetalOre = data.RustStatsData[playeruserID].QuarryMetalOre + item.amount;
                    break;
                case "hq.metal.ore":
                    data.RustStatsData[playeruserID].QuarryHighMetalOre = data.RustStatsData[playeruserID].QuarryHighMetalOre + item.amount;
                    break;
            }
        }

        #endregion

        #region PVE PVP SUICIDE Stats
        private bool IsNPC(BasePlayer player)
        {
            if (player is NPCPlayer) return true;
            if (!(player.userID >= 76560000000000000L || player.userID <= 0L)) return true;
            return false;
        }

        private void OnEntityDeath(BaseEntity victim, HitInfo info)
        {
            if (victim == null || info == null || info.Initiator == null) return;
            if (victim is BasePlayer)
            {
                BasePlayer player = victim.ToPlayer();
                if (player == null || IsNPC(player)) return;
                CheckUser(player);
                BasePlayer initiator = info.InitiatorPlayer;

                if (info.Initiator.IsNpc || initiator != null && IsNPC(initiator))
                {
                    data.RustStatsData[player.userID].PVEDeath++;
                }
                else if (initiator != null && !IsNPC(initiator))
                {
                    if (player == initiator)
                    {
                        data.RustStatsData[player.userID].Suicide++;
                        return;
                    }
                    CheckUser(initiator);
                    data.RustStatsData[initiator.userID].Coins++;
                    data.RustStatsData[initiator.userID].Kill++;
                    data.RustStatsData[player.userID].Death++;
                }
            }
            else if (victim is BaseAnimalNPC && info.InitiatorPlayer != null && !IsNPC(info.InitiatorPlayer))
            {
                BasePlayer initiator = info.InitiatorPlayer;
                CheckUser(initiator);
                data.RustStatsData[initiator.userID].KillPVE++;
            }
        }

        #endregion

        #region GUI TOP AND STATS

        [ChatCommand("top")]
        void CreateUI(BasePlayer player)
        {
            var Container = new CuiElementContainer();
            var UserAspectRatio = GetUserAspectRatio(player.userID) ?? string.Empty;

            switch (UserAspectRatio)
            {
                case "16x9":
                    CreateGui(player, Container, "0 0", "1 1");
                    break;
                case "16x10":
                    CreateGui(player, Container, "0 0.02", "1 1");
                    break;
                case "4x3":
                    CreateGui(player, Container, "0 0.2", "1 1");
                    break;
                case "5x4":
                    CreateGui(player, Container, "0 0.22", "1 1");
                    break;
                default:
                    CreateGui(player, Container, "0 0", "1 1");
                    break;
            }
        }

        void CreateGui(BasePlayer player, CuiElementContainer Container, string AnchorMin, string AnchorMax)
        {
            ChatDestroyGui(player);
            GetKillTop();
            GetDeathTop();
            GetGatherTop();
            GetPlayTime();
            DateTime time;
            double totaltime;
            if (connect.TryGetValue(player.userID, out time)) totaltime = (DateTime.Now - time).TotalSeconds;
            else totaltime = 0f;
            TimeSpan span = TimeSpan.FromSeconds(data.RustStatsData[player.userID].Time + totaltime);
            Hours = (int)span.TotalHours;
            Min = span.Minutes;
            Container.Add(new CuiElement
            {
                Name = "AspectRatioMain",
                Parent = "Overlay",
                Components = {
                    new CuiImageComponent {
                         Color = "0 0 0 0.8", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat", Sprite = "assets/content/ui/ui.background.transparent.radial.psd"
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    },
                    new CuiNeedsCursorComponent()
                }
            });

            var TitlePanelName = CuiHelper.GetGuid();

            Container.Add(new CuiElement
            {
                Name = TitlePanelName,
                Parent = "AspectRatioMain",
                Components = {
                    new CuiImageComponent {
                        Color = "0 0 0 0.0"
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = "0.075 0.15",
                        AnchorMax = "0.325 0.89"
                    }
                }
            });

            Container.Add(new CuiElement
            {
                Name = TitlePanelName,
                Parent = "AspectRatioMain",
                Components = {
                    new CuiTextComponent {
                        Text = $"<size=24>{player.displayName}</size>\n<color=#ECBE13>{Hours} час(а) {Min} минут(у)</color>",
                        Align = TextAnchor.UpperCenter
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = "0.135 0.15",
                        AnchorMax = "0.265 0.87"
                    }
                }
            });

            Container.Add(new CuiElement
            {
                Name = TitlePanelName,
                Parent = "AspectRatioMain",
                Components = {
                    new CuiTextComponent {
                        Text =
                            "Животных убито:\nPVP Убийств: \nPVP Смертей: \nPVE Смертей: \nСуицидов: \n\n\n\n\nДерево: \nКамень: \nЖелезная руда: \nСерная руда: \nHQM Руда: \n\n\n\n\nКамень: \nЖелезная руда: \nСерная руда: \nHQM Руда:",
                        Align = TextAnchor.UpperLeft
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = "0.135 0.15",
                        AnchorMax = "0.265 0.78"
                    }
                }
            });
            Container.Add(new CuiElement
            {
                Name = TitlePanelName,
                Parent = "AspectRatioMain",
                Components = {
                    new CuiTextComponent {
                        Text =
                            $"<color=#ECBE13>{data.RustStatsData[player.userID].KillPVE}\n{data.RustStatsData[player.userID].Kill}\n{data.RustStatsData[player.userID].Death}\n{data.RustStatsData[player.userID].PVEDeath}\n{data.RustStatsData[player.userID].Suicide}\n\n\n\n\n{data.RustStatsData[player.userID].Wood}\n{data.RustStatsData[player.userID].Stone}\n{data.RustStatsData[player.userID].MetalOre}\n{data.RustStatsData[player.userID].SulfurOre}\n{data.RustStatsData[player.userID].HighMetalOre}\n\n\n\n\n{data.RustStatsData[player.userID].QuarryStone}\n{data.RustStatsData[player.userID].QuarryMetalOre}\n{data.RustStatsData[player.userID].QuarrySulfurOre}\n{data.RustStatsData[player.userID].QuarryHighMetalOre}</color>",
                        Align = TextAnchor.UpperRight
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = "0.135 0.15",
                        AnchorMax = "0.265 0.78"
                    }
                }
            });
            Container.Add(new CuiElement
            {
                Name = TitlePanelName,
                Parent = "AspectRatioMain",
                Components = {
                    new CuiRawImageComponent {
                        Png = GetImage("pickaxe")
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = "0.145 0.59",
                        AnchorMax = "0.175 0.64"
                    }
                }
            });
            Container.Add(new CuiElement
            {
                Name = TitlePanelName,
                Parent = "AspectRatioMain",
                Components = {
                    new CuiTextComponent {
                        Text = "ДОБЫТО РУКАМИ",
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = "0.165 0.59",
                        AnchorMax = "0.265 0.64"
                    }
                }
            });

            Container.Add(new CuiElement
            {
                Name = TitlePanelName,
                Parent = "AspectRatioMain",
                Components = {
                    new CuiRawImageComponent {
                        Png = GetImage("quarry")
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = "0.140 0.39",
                        AnchorMax = "0.170 0.44"
                    }
                }
            });
            Container.Add(new CuiElement
            {
                Name = TitlePanelName,
                Parent = "AspectRatioMain",
                Components = {
                    new CuiTextComponent {
                        Text = $"ДОБЫТО КАРЬЕРОМ",
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = "0.165 0.39",
                        AnchorMax = "0.265 0.44"
                    }
                }
            });

            Container.Add(new CuiElement
            {
                Name = TitlePanelName,
                Parent = "AspectRatioMain",
                Components = {
                    new CuiImageComponent {
                        Color = "0 0 0 0.0"
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = "0.375 0.15",
                        AnchorMax = "0.625 0.9"
                    }
                }
            });
            Container.Add(new CuiElement
            {
                Name = TitlePanelName,
                Parent = "AspectRatioMain",
                Components = {
                    new CuiTextComponent {
                        Text = $"<size=24>ТОП ПО PVP</size>",
                        Align = TextAnchor.UpperCenter
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = "0.375 0.15",
                        AnchorMax = "0.625 0.875"
                    }
                }
            });
            Container.Add(new CuiElement
            {
                Name = TitlePanelName,
                Parent = "AspectRatioMain",
                Components = {
                    new CuiTextComponent {
                        Text = $"\n\n{KillMsg}\n\n\n\n{DeathMsg}",
                        Align = TextAnchor.UpperLeft
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = "0.415 0.15",
                        AnchorMax = "0.585 0.81"
                    }
                }
            });
            Container.Add(new CuiElement
            {
                Name = TitlePanelName,
                Parent = "AspectRatioMain",
                Components = {
                    new CuiTextComponent {
                        Text = $"<color=#ECBE13>\n\n{KillValue}\n\n\n\n{DeathValue}</color>",
                        Align = TextAnchor.UpperRight
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = "0.415 0.15",
                        AnchorMax = "0.585 0.81"
                    }
                }
            });
            Container.Add(new CuiElement
            {
                Name = TitlePanelName,
                Parent = "AspectRatioMain",
                Components = {
                    new CuiTextComponent {
                        Text = $"{TimeMsg}",
                        Align = TextAnchor.UpperLeft
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = "0.4 0.15",
                        AnchorMax = "0.6 0.3"
                    }
                }
            });
            Container.Add(new CuiElement
            {
                Name = TitlePanelName,
                Parent = "AspectRatioMain",
                Components = {
                    new CuiTextComponent {
                        Text = $"<color=#ECBE13>{TimeValue}</color>",
                        Align = TextAnchor.UpperRight
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = "0.4 0.15",
                        AnchorMax = "0.6 0.3"
                    }
                }
            });
            Container.Add(new CuiElement
            {
                Name = TitlePanelName,
                Parent = "AspectRatioMain",
                Components = {
                    new CuiRawImageComponent {
                        Png = GetImage("kill")
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = "0.455 0.78",
                        AnchorMax = "0.490 0.835"
                    }
                }
            });
            Container.Add(new CuiElement
            {
                Name = TitlePanelName,
                Parent = "AspectRatioMain",
                Components = {
                    new CuiTextComponent {
                        Text = $"УБИЙЦЫ",
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = "0.45 0.75",
                        AnchorMax = "0.585 0.85"
                    }
                }
            });
            Container.Add(new CuiElement
            {
                Name = TitlePanelName,
                Parent = "AspectRatioMain",
                Components = {
                    new CuiRawImageComponent {
                        Png = GetImage("death")
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = "0.460 0.590",
                        AnchorMax = "0.485 0.640"
                    }
                }
            });
            Container.Add(new CuiElement
            {
                Name = TitlePanelName,
                Parent = "AspectRatioMain",
                Components = {
                    new CuiTextComponent {
                        Text = $"ЖЕРТВЫ",
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = "0.445 0.575",
                        AnchorMax = "0.58 0.65"
                    }
                }
            });
            Container.Add(new CuiElement
            {
                Name = TitlePanelName,
                Parent = "AspectRatioMain",
                Components = {
                    new CuiRawImageComponent {
                        Png = GetImage("time")
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = "0.44 0.3325",
                        AnchorMax = "0.465 0.3825"
                    }
                }
            });
            Container.Add(new CuiElement
            {
                Name = TitlePanelName,
                Parent = "AspectRatioMain",
                Components = {
                    new CuiTextComponent {
                        Text = $"ТОП ПО ВРЕМЕНИ",
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = "0.440 0.15",
                        AnchorMax = "0.590 0.565"
                    }
                }
            });

            Container.Add(new CuiElement
            {
                Name = TitlePanelName,
                Parent = "AspectRatioMain",
                Components = {
                    new CuiImageComponent {
                        Color = "0 0 0 0.0"
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = "0.675 0.35",
                        AnchorMax = "0.925 0.9"
                    }
                }
            });
            Container.Add(new CuiElement
            {
                Name = TitlePanelName,
                Parent = "AspectRatioMain",
                Components = {
                    new CuiTextComponent {
                        Text = $"<size=24>ТОП ПО РЕСУРСАМ</size>",
                        Align = TextAnchor.UpperCenter
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = "0.7 0.80",
                        AnchorMax = "0.9 0.875"
                    }
                }
            });
            string[] pve = GetPVEKill();
            Container.Add(new CuiElement
            {
                Name = TitlePanelName,
                Parent = "AspectRatioMain",
                Components = {
                    new CuiTextComponent {
                        Text = $"\n\n{GatherMsg}\n\n\n\n{pve[0]}",
                        Align = TextAnchor.UpperLeft
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = "0.715 0.15",
                        AnchorMax = "0.885 0.81"
                    }
                }
            });
            Container.Add(new CuiElement
            {
                Name = TitlePanelName,
                Parent = "AspectRatioMain",
                Components = {
                    new CuiTextComponent {
                        Text = $"<color=#00C2FF>\n\n{GatherValue}\n\n\n\n{pve[1]}</color>",
                        Align = TextAnchor.UpperRight
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = "0.715 0.15",
                        AnchorMax = "0.885 0.81"
                    }
                }
            });
            Container.Add(new CuiElement
            {
                Name = TitlePanelName,
                Parent = "AspectRatioMain",
                Components = {
                    new CuiRawImageComponent {
                        Png = GetImage("pickaxe")
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = "0.745 0.780",
                        AnchorMax = "0.775 0.830"
                    }
                }
            });
            Container.Add(new CuiElement
            {
                Name = TitlePanelName,
                Parent = "AspectRatioMain",
                Components = {
                    new CuiTextComponent {
                        Text = $"ДОБЫТО РУКАМИ",
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = "0.725 0.75",
                        AnchorMax = "0.905 0.85"
                    }
                }
            });

            Container.Add(new CuiElement
            {
                Name = TitlePanelName,
                Parent = "AspectRatioMain",
                Components = {
                    new CuiRawImageComponent {
                        Png = GetImage("animal")
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = "0.740 0.585",
                        AnchorMax = "0.77 0.635"
                    }
                }
            });
            Container.Add(new CuiElement
            {
                Name = TitlePanelName,
                Parent = "AspectRatioMain",
                Components = {
                    new CuiTextComponent {
                        Text = $"ТОП ПО ЖИВОТНЫМ",
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = "0.725 0.575",
                        AnchorMax = "0.905 0.65"
                    }
                }
            });

            Container.Add(new CuiElement
            {
                Name = TitlePanelName,
                Parent = "AspectRatioMain",
                Components = {
                    new CuiTextComponent {
                        Text = $"<size=18>ЗАКРЫТЬ</size>",
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = "0.675 0.15",
                        AnchorMax = "0.925 0.325"
                    }
                }
            });
            Container.Add(new CuiElement
            {
                Name = TitlePanelName,
                Parent = "AspectRatioMain",
                Components = {
                    new CuiButtonComponent {
                        Command = $"statsclose {player}",
                        Color = "0 0 0 0.0"
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = "0.675 0.15",
                        AnchorMax = "0.925 0.325"
                    }
                }
            });

            CuiHelper.AddUi(player, Container);
        }

        [ConsoleCommand("statsclose")]
        void CmdDestroyGui(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            CuiHelper.DestroyUi(player, "AspectRatioMain");
            CuiHelper.DestroyUi(player, "cgasd12e");
        }

        [ChatCommand("statsclose")]
        void ChatDestroyGui(BasePlayer player)
        {
            if (player == null) return;

            CuiHelper.DestroyUi(player, "AspectRatioMain");
            CuiHelper.DestroyUi(player, "cgasd12e");
        }

        #endregion

        #region TOP

        public string GetKillTop()
        {
            KillMsg = "";
            KillValue = "";
            PVPKill.Clear();

            foreach (var player in data.RustStatsData)
            {
                PVPKill[player.Key] = player.Value.Kill;
            }

            iTop = 0;

            foreach (var rank in PVPKill.OrderByDescending(rank => rank.Value))
            {
                playeruserID = rank.Key;
                playerdisplayName = data.RustStatsData[playeruserID].Name;
                iTop++;
                KillMsg += $"{iTop}. {playerdisplayName}:\n";
                KillValue += $"{rank.Value}\n";
                if (iTop == 5) break;
            }

            return KillMsg;
        }

        public string GetDeathTop()
        {
            DeathMsg = "";
            DeathValue = "";
            PVPDeath.Clear();

            foreach (var player in data.RustStatsData)
            {
                PVPDeath[player.Key] = player.Value.Death;
            }

            iTop = 0;

            foreach (var rank in PVPDeath.OrderByDescending(rank => rank.Value))
            {
                playeruserID = rank.Key;
                playerdisplayName = data.RustStatsData[playeruserID].Name;
                iTop++;
                DeathMsg += $"{iTop}. {playerdisplayName}:\n";
                DeathValue += $"{rank.Value}\n";
                if (iTop == 5) break;
            }

            return DeathMsg;
        }

        public string GetGatherTop()
        {
            GatherMsg = "";
            GatherValue = "";
            Gather.Clear();

            foreach (var player in data.RustStatsData)
            {
                Gather[player.Key] = player.Value.Stone + player.Value.SulfurOre + player.Value.MetalOre + player.Value.HighMetalOre;
            }

            iTop = 0;

            foreach (var rank in Gather.OrderByDescending(rank => rank.Value))
            {
                playeruserID = rank.Key;
                playerdisplayName = data.RustStatsData[playeruserID].Name;
                iTop++;
                GatherMsg += $"{iTop}. {playerdisplayName}:\n";
                GatherValue += $"{rank.Value}\n";
                if (iTop == 5) break;
            }

            return GatherMsg;
        }

        public string[] GetPVEKill()
        {
            string Msg = "";
            string Value = "";
            int i = 0;
            foreach (var rank in data.RustStatsData.Values.OrderByDescending(x => x.KillPVE))
            {
                i++;
                Msg += $"{i}. {rank.Name}:\n";
                Value += $"{rank.KillPVE}\n";
                if (i == 5) break;
            }

            return new string[] { Msg, Value };
        }

        public string GetPlayTime()
        {
            TimeMsg = "";
            TimeValue = "";
            Time.Clear();

            foreach (var player in data.RustStatsData)
            {
                DateTime time;
                if (connect.TryGetValue(player.Key, out time)) Time[player.Key] = player.Value.Time + (int)(DateTime.Now - time).TotalSeconds;
                else Time[player.Key] = player.Value.Time;
            }

            iTop = 0;

            foreach (var rank in Time.OrderByDescending(rank => rank.Value))
            {
                playeruserID = rank.Key;
                int totaltime = rank.Value;
                playerdisplayName = data.RustStatsData[playeruserID].Name;
                iTop++;
                TimeSpan span = TimeSpan.FromSeconds(rank.Value);
                Hours = (int)span.TotalHours;
                Min = span.Minutes;
                TimeMsg += $"{iTop}. {playerdisplayName}:\n";
                if (Hours <= 0)
                {
                    TimeValue += ($"{Min} минут(у)\n");
                }
                else
                {
                    TimeValue += $"{Hours} час(а) {Min} минут(у)\n";
                }

                if (iTop == 5) break;
            }

            return TimeMsg;
        }

        #endregion
    }
}
