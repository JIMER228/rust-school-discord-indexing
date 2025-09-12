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
    [Info("TournamentBoloto", "always work", "1.0.0")]
    public class TournamentBoloto : RustPlugin
    {
        #region [Vars]
        [PluginReference] private Plugin RustMap = null, Clans = null;
        public string Layer = "UI_Layer";
        private bool newSave = false;

        public List<MapMarkerGenericRadius> RadiusMarker = new List<MapMarkerGenericRadius>();
        public List<VendingMachineMapMarker> VendingMarker = new List<VendingMachineMapMarker>();
        public List<ulong> _List = new List<ulong>();
        public List<string> _WhiteList = new List<string>();
        #endregion

        #region [Data-Clans]
        public List<CupData> CupboardData = new List<CupData>();
        public class CupData
        {
            public string nameClan;
            public int Warn;
            public int Point;
            public int raidPoint;
            public bool isRaided;
            public ulong netID;
        }

        private void LoadData() => CupboardData = Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<List<CupData>>("TournamentBoloto/Data");
        private void SaveData() => Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("TournamentBoloto/Data", CupboardData);
        #endregion

        #region [Destory-History]
        public List<HistoryData> DestroyHistory = new List<HistoryData>();
        public class HistoryData
        {
            public string nameClan;
            public string deathClan;
            public string DataTime;

            public int timeDestroy;
        }

        private void LoadHistory() => DestroyHistory = Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<List<HistoryData>>("TournamentBoloto/History");
        private void SaveHistory() => Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("TournamentBoloto/History", DestroyHistory);
        #endregion

        #region [Oxide]
        private void OnNewSave() => newSave = true;

        private void OnServerInitialized()
        {
            if (!config.useWhiteList)
                Unsubscribe(nameof(CanUserLogin));

            cmd.AddChatCommand(config.openMenuCommand, this, "cmdRaidTop");
            cmd.AddChatCommand(config.openHistoryMenuCommand, this, "cmdHistoryTop");

            ServerMgr.Instance.InvokeRepeating(CheckRegistrationTime, 0f, 60f);
            timer.Every(5, TimeHandle);

            if (newSave)
            {
                LoadData();
                LoadHistory();
                DestroyHistory.Clear();
                CupboardData.Clear();
            }
            else
            {
                LoadData();
                LoadHistory();
                foreach (var data in CupboardData)
                {
                    var entity = BaseNetworkable.serverEntities.Find(new NetworkableId(data.netID));
                    if (entity == null && data.isRaided == false)
                    {
                        data.isRaided = true;
                    }
                    if (entity != null && data.isRaided == false && data.Warn < config.MaxWarn)
                    {
                        if (config._RustMapsSettings.UseRustMap)
                            AddRustMap(data);
                        if (config._MarkerInGameSettings.UseGameMarker)
                            AddGenereticMarker(data);

                        _List.Add(data.netID);
                    }
                }
            }
        }

        private void Unload()
        {
            SaveData();
            SaveHistory();
            foreach (var data in CupboardData)
            {
                if (data.isRaided == false && data.Warn < config.MaxWarn)
                {
                    if (config._RustMapsSettings.UseRustMap)
                        RemoveRustMap(data);
                    if (config._MarkerInGameSettings.UseGameMarker)  
                        RemoveGenereticMarker(data);
                }
            }

            ServerMgr.Instance.CancelInvoke(CheckRegistrationTime);

            foreach (var player in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(player, Layer);
        }
        #endregion

        #region [Rust]
        private object OnEntityTakeDamage(BuildingPrivlidge entity, HitInfo info)
        {
            if (entity == null || info == null) return null;
            if (info.InitiatorPlayer == null) return null;

            BasePlayer player = info.InitiatorPlayer;
            if (player == null) return null;
            if (!_List.Contains(entity.net.ID.Value)) return null;

            var clan = GetClanTag(player.userID);
            if (string.IsNullOrEmpty(clan))
            {
                if (player.SecondsSinceAttacked > 5)
                {
                    player.ChatMessage("Вы не можете наносить урон по турнирному шкафу без клана!");
                    player.lastAttackedTime = UnityEngine.Time.time;
                    return false;
                }
                return false;
            }

            var findData = CupboardData.FirstOrDefault(p => p.nameClan == clan);
            if (findData == null)
            {
                if (player.SecondsSinceAttacked > 5)
                {
                    player.ChatMessage("Вы не имеете право наносить урон турнирному шкафу без участия в тунире!");
                    player.lastAttackedTime = UnityEngine.Time.time;
                    return false;
                }
                return false;
            }
            
            var IsFriend = (bool)Clans?.CallHook("IsTeammates", entity.OwnerID, player.userID);
            if (IsFriend)
            {
                if (player.SecondsSinceAttacked > 5)
                {
                    player.ChatMessage("Вы не имеете право уничтожать турнирный шкаф своего же клана!");
                    player.lastAttackedTime = UnityEngine.Time.time;
                    return false;
                }
                return false;
            }

            return null;
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return;

            BasePlayer player = info.InitiatorPlayer;
            if (player == null) return;
            if (!_List.Contains(entity.net.ID.Value)) return;

            var find = CupboardData.FirstOrDefault(p => p.netID == entity.net.ID.Value);
            if (find == null) return;

            if (config._RustMapsSettings.UseRustMap)
                RemoveRustMap(find);
            if (config._MarkerInGameSettings.UseGameMarker)  
                RemoveGenereticMarker(find);

            find.isRaided = true;
            if (find.Warn >= config.MaxWarn) return;

            Server.Broadcast($"Клан {find.nameClan} был зарейжен игроком {player.displayName}");

            var nameClan = GetClanTag(player.userID);
            if (string.IsNullOrEmpty(nameClan)) return;

            var findClan = CupboardData.FirstOrDefault(p => p.nameClan == nameClan);
            if (findClan == null) return;

            Clans?.CallHook("GetPointRaid", find.nameClan, player.userID);
            findClan.Point += find.raidPoint;

            DestroyHistory.Add(new HistoryData()
            {
                nameClan = nameClan,
                deathClan = find.nameClan,
                DataTime = $"{DateTime.Now.ToString("t")} {DateTime.Now.Day}/{DateTime.Now.Month}/{DateTime.Now.Year}",
                timeDestroy = (int)DateTime.UtcNow.Subtract(epoch).TotalSeconds
            });

            _List.Remove(find.netID);
            SaveHistory();
            SaveData();
        }

        private object OnEntityGroundMissing(BaseEntity entity)
        {
            if (entity == null) return null;
            if (_List.Contains(entity.net.ID.Value)) return false;
            return null;
        }

        private string canRemove(BasePlayer player, BaseEntity entity)
        {
            if (player == null || entity == null) return null;
            var build = entity as BuildingPrivlidge;
            if (build == null) return null;
            if (!_List.Contains(entity.net.ID.Value)) return null;
            return "Нельзя ремувать турнирный шкаф!";
        }

        private void OnLootEntity(BasePlayer player, BuildingPrivlidge entity)
        {
            if (player == null || entity == null) return;
            CuiHelper.DestroyUi(player, Layer);
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                RectTransform = { AnchorMin="0.5 0", AnchorMax="0.5 0", OffsetMin="193.5 16", OffsetMax="431 97" },
                Image = { Color = "1 0.96 0.88 0.15" }
            }, "Overlay", Layer);

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiTextComponent { Text = $"УЧАСТНИЕ В КЛАНОВОМ ТУРНИРЕ", Color = "1 1 1 0.65", FontSize = 12, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" },
                    new CuiRectTransformComponent { AnchorMin = "0 0.7", AnchorMax = "0.991 1" },
                    new CuiOutlineComponent{ Color = "0 0 0 1", Distance = "0.25 0.25" },
                }
            });

            container.Add(new CuiElement
            {
                Name = "CupRegisterMenu",
                Parent = Layer,
                Components =
                    {
                        new CuiImageComponent { Color = "0.3773585 0.3755785 0.3755785 1", Material = "assets/icons/greyout.mat" },
                        new CuiRectTransformComponent { AnchorMin = "0.08 0.08", AnchorMax = "0.925 0.68" }
                    }
            });

            string nameClan = GetClanTag(player.userID);
            if (string.IsNullOrEmpty(nameClan))
            {
                container.Add(new CuiElement
                {
                    Parent = "CupRegisterMenu",
                    Components =
                    {
                        new CuiTextComponent { Text = $"УЧАСТИЕ ЗАПРЕЩЕНО", Color = "1 1 1 0.65", FontSize = 16, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" },
                        new CuiRectTransformComponent { AnchorMin = "0 0.33", AnchorMax = "0.991 1" },
                        new CuiOutlineComponent{ Color = "0 0 0 1", Distance = "0.25 0.25" },
                    }
                });
                    
                container.Add(new CuiElement
                {
                    Parent = "CupRegisterMenu",
                    Components =
                    {
                        new CuiTextComponent { Text = $"Вы не состоите в клане!", Color = "1 1 1 0.45", FontSize = 8, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 0.5" },
                        new CuiOutlineComponent{ Color = "0 0 0 1", Distance = "0.025 0.025" },
                    }
                });

                CuiHelper.AddUi(player, container);
                return;
            }

            var find = CupboardData.FirstOrDefault(p => p.nameClan == nameClan);
            if (find != null)
            {
                if (find.isRaided)
                {
                    container.Add(new CuiElement
                    {
                        Parent = "CupRegisterMenu",
                        Components =
                        {
                            new CuiTextComponent { Text = $"УЧАСТИЕ ЗАПРЕЩЕНО", Color = "1 1 1 0.65", FontSize = 16, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" },
                            new CuiRectTransformComponent { AnchorMin = "0 0.33", AnchorMax = "0.991 1" },
                            new CuiOutlineComponent{Color = "0 0 0 1", Distance = "0.25 0.25"},
                        }
                    });
                    
                    container.Add(new CuiElement
                    {
                        Parent = "CupRegisterMenu",
                        Components =
                        {
                            new CuiTextComponent { Text = $"Ваш турнирный шкаф уже был уничтожен", Color = "1 1 1 0.45", FontSize = 10, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" },
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 0.5" },
                            new CuiOutlineComponent{Color = "0 0 0 1", Distance = "0.025 0.025"},
                        }
                    });

                    CuiHelper.AddUi(player, container);
                    return;
                }

                if (find.Warn >= config.MaxWarn)
                {
                    container.Add(new CuiElement
                    {
                        Parent = "CupRegisterMenu",
                        Components =
                        {
                            new CuiTextComponent { Text = $"УЧАСТИЕ ЗАПРЕЩЕНО", Color = "1 1 1 0.65", FontSize = 16, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" },
                            new CuiRectTransformComponent { AnchorMin = "0 0.33", AnchorMax = "0.991 1" },
                            new CuiOutlineComponent{Color = "0 0 0 1", Distance = "0.25 0.25"},
                        }
                    });
                    
                    container.Add(new CuiElement
                    {
                        Parent = "CupRegisterMenu",
                        Components =
                        {
                            new CuiTextComponent { Text = $"Ваш клан был снят с турнира", Color = "1 1 1 0.45", FontSize = 10, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" },
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 0.5" },
                            new CuiOutlineComponent{Color = "0 0 0 1", Distance = "0.025 0.025"},
                        }
                    });

                    CuiHelper.AddUi(player, container);
                    return;
                }

                container.Add(new CuiElement
                {
                    Parent = "CupRegisterMenu",
                    Components =
                    {
                        new CuiTextComponent { Text = $"УЧАСТИЕ ЗАПРЕЩЕНО", Color = "1 1 1 0.65", FontSize = 16, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" },
                        new CuiRectTransformComponent { AnchorMin = "0 0.33", AnchorMax = "0.991 1" },
                        new CuiOutlineComponent{ Color = "0 0 0 1", Distance = "0.25 0.25" },
                    }
                });
                    
                container.Add(new CuiElement
                {
                    Parent = "CupRegisterMenu",
                    Components =
                    {
                        new CuiTextComponent { Text = $"Вы уже участвуете!", Color = "1 1 1 0.45", FontSize = 10, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 0.5" },
                        new CuiOutlineComponent{ Color = "0 0 0 1", Distance = "0.025 0.025" },
                    }
                });
                
                CuiHelper.AddUi(player, container);
                return;
            }

            if (_List.Contains(entity.net.ID.Value))
            {
                container.Add(new CuiElement
                {
                    Parent = "CupRegisterMenu",
                    Components =
                    {
                        new CuiTextComponent { Text = $"УЧАСТИЕ ЗАПРЕЩЕНО", Color = "1 1 1 0.65", FontSize = 16, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" },
                        new CuiRectTransformComponent { AnchorMin = "0 0.33", AnchorMax = "0.991 1" },
                        new CuiOutlineComponent{ Color = "0 0 0 1", Distance = "0.25 0.25" },
                    }
                });
                    
                container.Add(new CuiElement
                {
                    Parent = "CupRegisterMenu",
                    Components =
                    {
                        new CuiTextComponent { Text = $"Данный шкаф уже участвует в турнире!", Color = "1 1 1 0.45", FontSize = 10, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 0.5" },
                        new CuiOutlineComponent{ Color = "0 0 0 1", Distance = "0.025 0.025" },
                    }
                });

                CuiHelper.AddUi(player, container);
                return;
            }

            if (IsBlocked() == 0)
            {
                container.Add(new CuiElement
                {
                    Parent = "CupRegisterMenu",
                    Components =
                    {
                        new CuiTextComponent { Text = $"УЧАСТИЕ ЗАПРЕЩЕНО", Color = "1 1 1 0.65", FontSize = 16, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" },
                        new CuiRectTransformComponent { AnchorMin = "0 0.33", AnchorMax = "0.991 1" },
                        new CuiOutlineComponent{ Color = "0 0 0 1", Distance = "0.25 0.25" },
                    }
                });
                    
                container.Add(new CuiElement
                {
                    Parent = "CupRegisterMenu",
                    Components =
                    {
                        new CuiTextComponent { Text = $"Прошло 24 часа!", Color = "1 1 1 0.45", FontSize = 10, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 0.5" },
                        new CuiOutlineComponent{ Color = "0 0 0 1", Distance = "0.025 0.025" },
                    }
                });

                CuiHelper.AddUi(player, container);
                return;
            }

            bool GetLeader = IsLeader(player);
            if (!GetLeader)
            {
                container.Add(new CuiElement
                {
                    Parent = "CupRegisterMenu",
                    Components =
                    {
                        new CuiTextComponent { Text = $"УЧАСТИЕ ЗАПРЕЩЕНО", Color = "1 1 1 0.65", FontSize = 16, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" },
                        new CuiRectTransformComponent { AnchorMin = "0 0.33", AnchorMax = "0.991 1" },
                        new CuiOutlineComponent{ Color = "0 0 0 1", Distance = "0.25 0.25" },
                    }
                });
                    
                container.Add(new CuiElement
                {
                    Parent = "CupRegisterMenu",
                    Components =
                    {
                        new CuiTextComponent { Text = $"Вы не являетесь главной своего клана!", Color = "1 1 1 0.45", FontSize = 8, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 0.5" },
                        new CuiOutlineComponent{ Color = "0 0 0 1", Distance = "0.025 0.025" },
                    }
                });

                CuiHelper.AddUi(player, container);
                return;
            }

            if (ClanPoint(player) < config.MinPoints)
            {
                container.Add(new CuiElement
                {
                    Parent = "CupRegisterMenu",
                    Components =
                    {
                        new CuiTextComponent { Text = $"УЧАСТИЕ ЗАПРЕЩЕНО", Color = "1 1 1 0.65", FontSize = 16, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" },
                        new CuiRectTransformComponent { AnchorMin = "0 0.33", AnchorMax = "0.991 1" },
                        new CuiOutlineComponent{Color = "0 0 0 1", Distance = "0.25 0.25"},
                    }
                });
                    
                container.Add(new CuiElement
                {
                    Parent = "CupRegisterMenu",
                    Components =
                    {
                        new CuiTextComponent { Text = $"недостаточно клановых очков", Color = "1 1 1 0.45", FontSize = 10, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 0.5" },
                        new CuiOutlineComponent{Color = "0 0 0 1", Distance = "0.025 0.025"},
                    }
                });
                CuiHelper.AddUi(player, container);
                return;
            }

            container.Add(new CuiElement
            {
                Parent = "CupRegisterMenu",
                Components =
                {
                    new CuiTextComponent { Text = $"УЧАСТИЕ РАЗРЕШЕНО", Color = "1 1 1 0.65", FontSize = 16, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" },
                    new CuiRectTransformComponent { AnchorMin = "0 0.33", AnchorMax = "0.991 1" },
                    new CuiOutlineComponent{Color = "0 0 0 1", Distance = "0.25 0.25"},
                }
            });

            container.Add(new CuiElement
            {
                Parent = "CupRegisterMenu",
                Components =
                {
                    new CuiTextComponent { Text = $"Зарегистрироваться на турнир", Color = "1 1 1 0.45", FontSize = 10, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 0.5" },
                    new CuiOutlineComponent{Color = "0 0 0 1", Distance = "0.025 0.025"},
                    }
            });

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Button = { Color = "0 0 0 0",Material = "assets/icons/greyout.mat", Command = $"regtc {entity.net.ID}" }
            }, "CupRegisterMenu");
                
            CuiHelper.AddUi(player, container);
        }

        private void OnLootEntityEnd(BasePlayer player, BuildingPrivlidge entity) => CuiHelper.DestroyUi(player, Layer);

        private object CanUserLogin(string name, string id)
        {
            if (IsBlocked() != 0) return null;
            var player = covalence.Players.FindPlayerById(id);
            if (player.IsAdmin) return null;
            if (_WhiteList.Contains(id)) return null;
            var clan = GetClanTag(ulong.Parse(id));
            if (string.IsNullOrEmpty(clan)) return "На сервере начался турнир, вы не можете зайти без клана!";
            var findClan = CupboardData.FirstOrDefault(p => p.nameClan == clan);
            if (findClan == null) return "На сервере начался турнир, ваш клан не участвует в турнире!";
            return null;
        }
        #endregion

        #region [Gui]
        private void cmdRaidTop(BasePlayer player)
        {
            TopRaid(player, 0);
        }

        private void TopRaid(BasePlayer player, int page)
        {
            CuiHelper.DestroyUi(player, "TopRaidMenu");
            var container = new CuiElementContainer();
     
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Material = "assets/content/ui/uibackgroundblur.mat", Color = "0 0 0 0.77" }
            }, "Overlay", "TopRaidMenu");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Button = { Color = "0.36 0.33 0.28 0.3", Material = "assets/icons/greyout.mat", Close = "TopRaidMenu" }
            }, "TopRaidMenu");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-229 -109.5", OffsetMax = "231 195" },
                Image = { Color = "0.3773585 0.3755785 0.3755785 0.3407843", Material = "assets/icons/greyout.mat" }
            }, "TopRaidMenu", "TopRaidMenuLayer");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0.935", AnchorMax = $"1 1" },
                Image = { Color = "0 0 0 0" }
            }, "TopRaidMenuLayer", "DescriptionsParent");

            container.Add(new CuiElement
            {
                Parent = "DescriptionsParent",
                Components =
                {
                        new CuiTextComponent { Text = $"#", Color = "1 1 1 1", Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" },
                        new CuiRectTransformComponent { AnchorMin = $"0.02 0", AnchorMax = $"1 0.9", OffsetMax = "0 0" },
                }
            });

            container.Add(new CuiElement
            {
                Parent = "DescriptionsParent",
                Components =
                {
                        new CuiTextComponent { Text = $"НАЗВАНИЕ КЛАНА", Color = "1 1 1 1", Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" },
                        new CuiRectTransformComponent { AnchorMin = $"0.099 0", AnchorMax = $"1 0.9", OffsetMax = "0 0" },
                }
            });

            container.Add(new CuiElement
            {
                Parent = "DescriptionsParent",
                Components =
                {
                        new CuiTextComponent { Text = $"ОБЩЕЕ КОЛ-ВО ОЧКОВ", Color = "1 1 1 1", Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" },
                        new CuiRectTransformComponent { AnchorMin = $"0.358 0", AnchorMax = $"1 0.9", OffsetMax = "0 0" },
                }
            });

            container.Add(new CuiElement
            {
                Parent = "DescriptionsParent",
                Components =
                {
                        new CuiTextComponent { Text = $"ОЧКИ ЗА РЕЙД", Color = "1 1 1 1", Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" },
                        new CuiRectTransformComponent { AnchorMin = $"0.662 0", AnchorMax = $"1 0.9", OffsetMax = "0 0" },
                }
            });

            container.Add(new CuiElement
            {
                Parent = "DescriptionsParent",
                Components =
                {
                        new CuiTextComponent { Text = $"ПРЕДЫ", Color = "1 1 1 1", Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" },
                        new CuiRectTransformComponent { AnchorMin = $"0.88 0", AnchorMax = $"1 0.9", OffsetMax = "0 0" },
                }
            });

            for (int y = 0; y < 10; y++)
            {
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"0.0107 {0.8563 - y * 0.0783}", AnchorMax = $"0.9875 {0.927 - y * 0.0783}" },
                    Image = { Color = "0 0 0 0.5" }
                },"TopRaidMenuLayer", "TopRaidMenuLayer" + $".TopLine{y}");
            }

            int i = 0;
            var sortedData = CupboardData.Where(x => !x.isRaided && x.Warn < config.MaxWarn).OrderByDescending(x => x.Point);
            foreach (var check in sortedData.Skip(page * 10).Take(10))
            {
                container.Add(new CuiLabel
                {
                    Text = { Text = $"{i + 1 + (page * 10)}", Color = "1 1 1 1", Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" },
                    RectTransform = { AnchorMin = $"0.012 0", AnchorMax = $"1 1" },
                }, "TopRaidMenuLayer" + $".TopLine{i}");

                container.Add(new CuiLabel
                {
                    Text = { Text = $"{check.nameClan}", Color = "1 1 1 1", Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" },
                    RectTransform = { AnchorMin = $"0.0925 0", AnchorMax = $"1 1" },
                }, "TopRaidMenuLayer" + $".TopLine{i}");

                container.Add(new CuiLabel
                {
                    Text = { Text = $"{check.Point}", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" },
                    RectTransform = { AnchorMin = $"0.348 0", AnchorMax = $"0.6 1" },
                }, "TopRaidMenuLayer" + $".TopLine{i}");

                container.Add(new CuiLabel
                {
                    Text = { Text = $"{check.raidPoint}", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" },
                    RectTransform = { AnchorMin = $"0.67 0", AnchorMax = $"0.825 1" },
                }, "TopRaidMenuLayer" + $".TopLine{i}");

                container.Add(new CuiLabel
                {
                    Text = { Text = $"{check.Warn}/{config.MaxWarn}", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" },
                    RectTransform = { AnchorMin = $"0.9 0", AnchorMax = $"0.95 1" },
                }, "TopRaidMenuLayer" + $".TopLine{i}");

                i++;
            }

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = $"0.095 0.02", AnchorMax = $"0.18 0.1225" },
                Image = { Color = "0 0 0 0.4", Material = "assets/icons/greyout.mat" }
            }, "TopRaidMenuLayer", "TopRaidMenuLayer_pagetext");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 1" },
                Text = { Text = $"{page + 1}", Color = "1 1 1 1", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, "TopRaidMenuLayer_pagetext");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.18 0.02", AnchorMax = $"0.264 0.125" },
                Button = { Color = "0.46 0.44 0.42 0.6", Material = "assets/icons/greyout.mat", Command = sortedData.Skip(10 * (page + 1)).Count() > 0 ? $"topraid {page + 1}" : "" },
                Text = { Text = $"+", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 18, Font = "robotocondensed-bold.ttf" }
            }, "TopRaidMenuLayer");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.011 0.02", AnchorMax = $"0.095 0.125" },
                Button = { Color = "0.46 0.44 0.42 0.6", Material = "assets/icons/greyout.mat", Command = page >= 1 ? $"topraid {page - 1}" : "" },
                Text = { Text = $"-", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 18, Font = "robotocondensed-bold.ttf" }
            }, "TopRaidMenuLayer");

            CuiHelper.AddUi(player, container);
        }
    
        private void cmdHistoryTop(BasePlayer player)
        {
            HistoryTop(player, 0);
        }

        private void HistoryTop(BasePlayer player, int page)
        {
            CuiHelper.DestroyUi(player, "TopHistory");
            var container = new CuiElementContainer();
     
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Material = "assets/content/ui/uibackgroundblur.mat", Color = "0 0 0 0.77" }
            }, "Overlay", "TopHistory");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Button = { Color = "0.36 0.33 0.28 0.3", Material = "assets/icons/greyout.mat", Close = "TopHistory" }
            }, "TopHistory");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-229 -109.5", OffsetMax = "231 195" },
                Image = { Color = "0.3773585 0.3755785 0.3755785 0.3407843", Material = "assets/icons/greyout.mat" }
            }, "TopHistory", "TopHistoryMenuLayer");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0.935", AnchorMax = $"1 1" },
                Image = { Color = "0 0 0 0" }
            }, "TopHistoryMenuLayer", "DescriptionsParent");

            container.Add(new CuiElement
            {
                Parent = "DescriptionsParent",
                Components =
                {
                        new CuiTextComponent { Text = $"НАЗВАНИЕ КЛАНА", Color = "1 1 1 1", Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" },
                        new CuiRectTransformComponent { AnchorMin = $"0.025 0", AnchorMax = $"1 0.87", OffsetMax = "0 0" },
                }
            });

            container.Add(new CuiElement
            {
                Parent = "DescriptionsParent",
                Components =
                {
                        new CuiTextComponent { Text = $"НАЗВАНИЕ ЗАРЕЙЖЕННОГО КЛАНА", Color = "1 1 1 1", Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" },
                        new CuiRectTransformComponent { AnchorMin = $"0.33 0", AnchorMax = $"1 0.87", OffsetMax = "0 0" },
                }
            });

            container.Add(new CuiElement
            {
                Parent = "DescriptionsParent",
                Components =
                {
                        new CuiTextComponent { Text = $"ВРЕМЯ", Color = "1 1 1 1", Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" },
                        new CuiRectTransformComponent { AnchorMin = $"0.82 0", AnchorMax = $"1 0.87", OffsetMax = "0 0" },
                }
            });

            for (int y = 0; y < 10; y++)
            {
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"0.0107 {0.8563 - y * 0.0783}", AnchorMax = $"0.9875 {0.927 - y * 0.0783}" },
                    Image = { Color = "0 0 0 0.5" }
                },"TopHistoryMenuLayer", "TopHistoryMenuLayer" + $".TopLine{y}");
            }

            int i = 0;
            var sortedData = DestroyHistory.OrderByDescending(x => x.timeDestroy);
            foreach (var check in sortedData.Skip(page * 10).Take(10))
            {
                container.Add(new CuiLabel
                {
                    Text = { Text = $"{check.nameClan}", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 13, Font = "robotocondensed-regular.ttf" },
                    RectTransform = {AnchorMin = $"0.025 0", AnchorMax = $"0.175 1" },
                }, "TopHistoryMenuLayer" + $".TopLine{i}");

                container.Add(new CuiLabel
                {
                    Text = { Text = $"{check.deathClan}", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 13, Font = "robotocondensed-regular.ttf" },
                    RectTransform = {AnchorMin = $"0.33 0", AnchorMax = $"0.69 1" },
                }, "TopHistoryMenuLayer" + $".TopLine{i}");

                container.Add(new CuiLabel
                {
                    Text = { Text = $"{check.DataTime}", Color = "1 1 1 1", Align = TextAnchor.MiddleLeft, FontSize = 13, Font = "robotocondensed-regular.ttf" },
                    RectTransform = { AnchorMin = $"0.78 0", AnchorMax = $"1 1" },
                }, "TopHistoryMenuLayer" + $".TopLine{i}");

                i++;
            }

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = $"0.095 0.02", AnchorMax = $"0.18 0.1225" },
                Image = { Color = "0 0 0 0.4", Material = "assets/icons/greyout.mat" }
            }, "TopHistoryMenuLayer", "TopHistoryMenuLayer_pagetext");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 1" },
                Text = { Text = $"{page + 1}", Color = "1 1 1 1", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, "TopHistoryMenuLayer_pagetext");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.18 0.02", AnchorMax = $"0.264 0.125" },
                Button = { Color = "0.46 0.44 0.42 0.6", Material = "assets/icons/greyout.mat", Command = sortedData.Skip(10 * (page + 1)).Count() > 0 ? $"tophistory {page + 1}" : "" },
                Text = { Text = $"+", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 18, Font = "robotocondensed-bold.ttf" }
            }, "TopHistoryMenuLayer");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.011 0.02", AnchorMax = $"0.095 0.125" },
                Button = { Color = "0.46 0.44 0.42 0.6", Material = "assets/icons/greyout.mat", Command = page >= 1 ? $"tophistory {page - 1}" : "" },
                Text = { Text = $"-", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 18, Font = "robotocondensed-bold.ttf" }
            }, "TopHistoryMenuLayer");

            CuiHelper.AddUi(player, container);
        }
        #endregion

        #region [GenericMarkers]
        private void AddGenereticMarker(CupData data)
        {
            var entity = BaseNetworkable.serverEntities.Find(new NetworkableId(data.netID));
            if (entity == null) return;

            var position = entity.transform.position;
            position.x += UnityEngine.Random.Range(-30f, 30f);
            position.z += UnityEngine.Random.Range(-30f, 30f);

            MapMarkerGenericRadius mapMarker = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", position) as MapMarkerGenericRadius;
            mapMarker.alpha = 0.6f;
            mapMarker.color1 = ConvertToColor(config._MarkerInGameSettings.MarkerColor);
            mapMarker.name = data.nameClan;
            mapMarker.radius = config._MarkerInGameSettings.MarkerRadius;
            RadiusMarker.Add(mapMarker);
            mapMarker.Spawn();
            mapMarker.SendUpdate();

            VendingMachineMapMarker vendingMarker = GameManager.server.CreateEntity("assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab", position) as VendingMachineMapMarker;
            vendingMarker.markerShopName = data.nameClan;
            VendingMarker.Add(vendingMarker);
            vendingMarker.Spawn();
        }

        private void RemoveGenereticMarker(CupData data)
        {
            var marker = RadiusMarker.FirstOrDefault(p => p.name == data.nameClan);
            if (marker != null)
            {
                if (marker != null && !marker.IsDestroyed)
                {
                    RadiusMarker.Remove(marker);
                    marker.Kill();
                    marker.SendUpdate();
                }
            }

            var vendingMarker = VendingMarker.FirstOrDefault(p => p.markerShopName == data.nameClan);
            if (vendingMarker != null)
            {
                if (vendingMarker != null && !vendingMarker.IsDestroyed)
                {
                    VendingMarker.Remove(vendingMarker);
                    vendingMarker.Kill();
                }
            }
        }
        #endregion

        #region [RustMaps-Marker]
        private void AddRustMap(CupData data)
        {
            RemoveRustMap(data);
            var entity = BaseNetworkable.serverEntities.Find(new NetworkableId(data.netID));
            var position = entity.transform.position;
            position.x += UnityEngine.Random.Range(-30f, 30f);
            position.z += UnityEngine.Random.Range(-30f, 30f);
            RustMap?.Call("ApiAddPointUrl", config._RustMapsSettings.Icon, $"Marker{data.netID}", position, data.nameClan, config._RustMapsSettings.sizeMarker);
        }

        private void RemoveRustMap(CupData data) => RustMap?.Call("ApiRemovePointUrl", $"Marker{data.netID}");
        #endregion

        #region [ConsoleCommand && ChatCommand]
        [ConsoleCommand("regtc")]
        void cmdRegClan(ConsoleSystem.Arg args)
        {
            BasePlayer player = args?.Player();
            if (player == null || !args.HasArgs()) return;
            
            var nameClan = GetClanTag(player.userID);
            if (string.IsNullOrEmpty(nameClan)) return;

            ulong building = 0;
            if (ulong.TryParse(args.Args[0], out building))
            {
                BuildingPrivlidge entity = BaseNetworkable.serverEntities.Find(new NetworkableId(building)) as BuildingPrivlidge;
                if (entity == null) return;

                CupData data = new CupData()
                {
                    nameClan = nameClan,
                    Warn = 0,
                    Point = 0,
                    raidPoint = GetRaidPoint(player, entity),
                    isRaided = false,
                    netID = entity.net.ID.Value,
                };

                CupboardData.Add(data);

                if (config._RustMapsSettings.UseRustMap)
                    AddRustMap(data);
                if (config._MarkerInGameSettings.UseGameMarker)
                    AddGenereticMarker(data);

                player.ChatMessage("Вы успешно зарегистрировали свой клан!");
                player.EndLooting();
                _List.Add(entity.net.ID.Value);
                SaveData();
            }
        }

        [ChatCommand("tournament")]
        void CommandTournament(BasePlayer player, string command, string[] args)
        {
            if(!player.IsAdmin) return;
            if (args.Length == 0)
            {
                    player.ChatMessage("Доступные команды:"
                                       + "\n/tournament give point - Выдать очки клану" 
                                       + "\n/tournament give raidpoint - Выдать очки за рейд клану"
                                       + "\n/tournament give warn - Выдать предупреждения клану"
                                       + "\n\n/tournament remove point - Снять очки клану"
                                       + "\n/tournament remove raidpoint - Снять очки за рейд клану"
                                       + "\n/tournament remove warn - Снять предупреждения клану"
                                       + "\n\n/tournament whitelist add - Добавить игрока в белый список"
                                       + "\n/tournament whitelist remove - Удалить игрока из белого списка");
                return;
            }
            if (args[0] == "give")
            {
                if (args[1] == "point")
                {
                    if (args.Length < 3)
                    {
                        player.ChatMessage("/tournament give point TAG AMOUNT");
                        return;
                    }
                    var find = CupboardData.FirstOrDefault(p => p.nameClan == args[2]);
                    if (find == null)
                    {
                        player.ChatMessage("Клан не найден!");
                        return;
                    }
                    int amount = Convert.ToInt32(args[3]);
                    if (amount == null) return;
                    if(amount > 0)
                    {
                        find.Point += amount;
                        player.ChatMessage($"Вы выдали клану {find.nameClan}, {amount} очков!");
                        SaveData();
                    }
                }
                if (args[1] == "raidpoint")
                {
                    if (args.Length < 3)
                    {
                        player.ChatMessage("/tournament give raidpoint TAG AMOUNT");
                        return;
                    }
                    var find = CupboardData.FirstOrDefault(p => p.nameClan == args[2]);
                    if (find == null)
                    {
                        player.ChatMessage("Клан не найден!");
                        return;
                    }
                    int amount = Convert.ToInt32(args[3]);
                    if(amount > 0)
                    {
                        find.raidPoint += amount;
                        player.ChatMessage($"Вы выдали клану {find.nameClan}, {amount} рейд-очков!");
                        SaveData();
                    }
                }
                if (args[1] == "warn")
                {
                    if (args.Length < 3)
                    {
                        player.ChatMessage("/tournament give warn TAG");
                        return;
                    }
                    var find = CupboardData.FirstOrDefault(p => p.nameClan == args[2]);
                    if (find == null)
                    {
                        player.ChatMessage("Клан не найден!");
                        return;
                    }
                    if (find.Warn >= config.MaxWarn)
                    {
                        player.ChatMessage($"Вы не можете выдать больше {config.MaxWarn} предупреждений!");
                        return;
                    }
                    else
                    {
                        find.Warn++;
                        player.ChatMessage($"Вы выдали предупреждние клану {find.nameClan}");
                        Server.Broadcast($"Клан {find.nameClan} получил предупреждения от администратора {covalence.Players.FindPlayer(player.UserIDString).Name}");
                        SaveData();
                    }
                    if (find.Warn >= config.MaxWarn)
                    {
                        if (config._RustMapsSettings.UseRustMap)
                            RemoveRustMap(find);
                        if (config._MarkerInGameSettings.UseGameMarker)  
                            RemoveGenereticMarker(find);
                        Server.Broadcast($"Клан {find.nameClan} был снят с турнира за максимальное количество предупреждений, администратором {covalence.Players.FindPlayer(player.UserIDString).Name}");
                    }
                }
            }
            if (args[0] == "remove")
            {
                if (args[1] == "point")
                {
                    if (args.Length < 3)
                    {
                        player.ChatMessage("/tournament remove point TAG AMOUNT");
                        return;
                    }
                    var find = CupboardData.FirstOrDefault(p => p.nameClan == args[2]);
                    if (find == null)
                    {
                        player.ChatMessage("Клан не найден!");
                        return;
                    }
                    int amount = Convert.ToInt32(args[3]);
                    if (amount == null) return;
                    if(amount > 0)
                    {
                        find.Point -= amount;
                        player.ChatMessage($"Вы сняли клану {find.nameClan}, {amount} очков!");
                        SaveData();
                    }
                }
                if (args[1] == "raidpoint")
                {
                    if (args.Length < 3)
                    {
                        player.ChatMessage("/tournament remove raidpoint TAG AMOUNT");
                        return;
                    }
                    var find = CupboardData.FirstOrDefault(p => p.nameClan == args[2]);
                    if (find == null)
                    {
                        player.ChatMessage("Клан не найден!");
                        return;
                    }
                    int amount = Convert.ToInt32(args[3]);
                    if(amount > 0)
                    {
                        find.raidPoint -= amount;
                        player.ChatMessage($"Вы сняли клану {find.nameClan}, {amount} рейд-очков!");
                        SaveData();
                    }
                }
                if (args[1] == "warn")
                {
                    if (args.Length < 3)
                    {
                        player.ChatMessage("/tournament remove warn TAG AMOUNT");
                        return;
                    }
                    var find = CupboardData.FirstOrDefault(p => p.nameClan == args[2]);
                    if (find == null)
                    {
                        player.ChatMessage("Клан не найден!");
                        return;
                    }
                    if (find.Warn == 0)
                    {
                        player.ChatMessage("Нельзя снять варны когда их 0!");
                        return;
                    }
                    else
                    {
                        find.Warn--;
                        player.ChatMessage($"Вы убрали предупреждение клану {find.nameClan}");
                        SaveData();
                    }
                }
            }
            if (args[0] == "whitelist")
            {
                if (args[1] == "add")
                {
                    ulong steamID = 0;
                    if (args.Length < 3 || !ulong.TryParse(args[2], out steamID))
                    {
                        player.ChatMessage("/tournament whitelist add STEAMID");
                        return;
                    }
                    if (steamID <= 76560000000000000L)
                    {
                        player.ChatMessage("/tournament whitelist add STEAMID");
                        return;
                    }
                    _WhiteList.Add(args[2]);
                    player.ChatMessage($"Вы успешно добавили игрока {args[2]} в белый список!");
                }
                if (args[1] == "remove")
                {
                    ulong steamID = 0;
                    if (args.Length < 3 || !ulong.TryParse(args[2], out steamID))
                    {
                        player.ChatMessage("/tournament whitelist add STEAMID");
                        return;
                    }
                    if (steamID <= 76560000000000000L)
                    {
                        player.ChatMessage("/tournament whitelist add STEAMID");
                        return;
                    }
                    if (!_WhiteList.Contains(args[2]))
                    {
                        player.ChatMessage("Игрок не находится в белом списке!");
                        return;
                    }
                    _WhiteList.Remove(args[2]);
                    player.ChatMessage($"Вы успешно удалили игрока {args[2]} из белого списка!");
                }
            }
        }

        [ConsoleCommand("topraid")]
        private void cmdTopRaidPage(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            TopRaid(player, arg.Args != null ? int.Parse(arg.Args[0]) : 0);
        }

        [ConsoleCommand("tophistory")]
        private void cmdTopHistoryPage(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            HistoryTop(player, arg.Args != null ? int.Parse(arg.Args[0]) : 0);
        }
        #endregion

        #region [Functional]
        private bool CheckRegistration(string nameClan)
        {
            var findData = CupboardData.FirstOrDefault(p => p.nameClan == nameClan);
            if (findData == null || findData.isRaided || findData.Warn >= config.MaxWarn)
            {
                return false;
            }
            return true;
        }

        static readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);
        static double CurrentTime() => DateTime.UtcNow.Subtract(epoch).TotalSeconds;
        private double IsBlocked()
        {

            var lefTime = SaveRestore.SaveCreatedTime.ToUniversalTime().Subtract(epoch).TotalSeconds + (3600 * config.HoursReg) - CurrentTime();
            return lefTime > 0 ? lefTime : 0;
        }

        private Color ConvertToColor(string color)
        {
            if (color.StartsWith("#")) color = color.Substring(1);
            int red = int.Parse(color.Substring(0, 2), NumberStyles.AllowHexSpecifier);
            int green = int.Parse(color.Substring(2, 2), NumberStyles.AllowHexSpecifier);
            int blue = int.Parse(color.Substring(4, 2), NumberStyles.AllowHexSpecifier);
            return new Color((float)red / 255, (float)green / 255, (float)blue / 255);
        }

		private void TimeHandle()
		{
            foreach(var mapMarker in RadiusMarker)
            {
                if (mapMarker != null && !mapMarker.IsDestroyed)
                {
                    mapMarker.SendUpdate();
                }
            }
		}

        private void CheckRegistrationTime()
        {
            if (IsBlocked() == 0)
            {
                Server.Broadcast("Регистрация в турнире: <color=#9ACD32>Запрещена</color>");
                ServerMgr.Instance.CancelInvoke(CheckRegistrationTime);
            }
        }

        private int GetRaidPoint(BasePlayer player, BuildingPrivlidge build)
        {
            if (!config._RaidPointSettings.useAutomaticRaidPoint) return 0;

            if (config._RaidPointSettings.useQualityCeiling)
            {
                int buildingBlocks = build.GetBuilding().buildingBlocks.Count;
                int RaidPoint = (int)Math.Ceiling(buildingBlocks / config._RaidPointSettings.CeilingObject);

                return RaidPoint;
            }
            else
            {
                var clanPoint = ClanPoint(player);
                int RaidPoint = (int)Math.Ceiling(clanPoint / config._RaidPointSettings.CeilingPoint);

                return RaidPoint;
            }
        }

        private int ClanPoint(BasePlayer player) => (int)Clans?.Call("GetClanPoints", player.userID);
        private string GetClanTag(ulong id) => (string)Clans?.CallHook("GetClanTag", id);
        private bool IsLeader(BasePlayer player) => (bool)Clans?.CallHook("GetClanOwner", player.userID);
        #endregion

        #region [Api]
        private Dictionary<string, int> GetTopsRaid()
        {
            Dictionary<string, int> clansList = new Dictionary<string, int>();

            foreach (var key in (from x in CupboardData select x).Where(x => !x.isRaided && x.Warn < config.MaxWarn).OrderByDescending(x => x.Point).Take(5))
                clansList.Add(key.nameClan, key.Point);

            return clansList;
        }

        private Dictionary<string, string> GetTopsHistory()
        {
            Dictionary<string, string> clansList = new Dictionary<string, string>();

            foreach (var key in (from x in DestroyHistory select x).OrderByDescending(x => x.timeDestroy).Take(5))
                clansList.Add(key.nameClan, key.deathClan);

            return clansList;
        }
        #endregion

        #region [Config]
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
            if (config.PluginVersion < Version)
            {
                config.PluginVersion = Version;
                if (Version == new VersionNumber(1, 1, 3))
                {
                    config.useWhiteList = true;

                    config._RaidPointSettings.useAutomaticRaidPoint = true;
                    config._RaidPointSettings.useQualityCeiling = true;
                    config._RaidPointSettings.CeilingPoint = 1000;
                    config._RaidPointSettings.CeilingObject = 500;
                }

                PrintWarning("Config checked completed!");
            }
            config.PluginVersion = Version;
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        public class RaidPointSettings
        {
            [JsonProperty("Использовать автоматическое начисления рейд-очков ?")]
            public bool useAutomaticRaidPoint;

            [JsonProperty("Использовать в качестве делителя обьекты ? ( true - обьекты, false - очки )")]
            public bool useQualityCeiling;

            [JsonProperty("На сколько делить клановые очки для получения рейд-очков ?")]
            public float CeilingPoint;

            [JsonProperty("На сколько делить обьекты для получения рейд-очков ?")]
            public float CeilingObject;
        }

        public class RustMapsSettings
        {
            [JsonProperty("Добавить поддержку RustMap ?")]
            public bool UseRustMap;

            [JsonProperty("Иконка для карты RustMap")]
            public string Icon;

            [JsonProperty("Размеры иконки на карте RustMap")]
            public float sizeMarker;
        }

        public class MarkerInGameSettings
        {
            [JsonProperty("Добавить поддержку отметки на карте G ?")]
            public bool UseGameMarker;

            [JsonProperty("Цвет радиуса")] 
            public string MarkerColor;

            [JsonProperty("Радиус маркера")] 
            public float MarkerRadius;
        }

        private class PluginConfig
        {
            [JsonProperty("Команда для открытия топа")]
            public string openMenuCommand;

            [JsonProperty("Команда для истории рейдов")]
            public string openHistoryMenuCommand;

            [JsonProperty("Минимальное количество очков для регистрации")]
            public int MinPoints;

            [JsonProperty("После скольки часов после вайпа нельзя регистрироватся?")]
            public int HoursReg;

            [JsonProperty("Максимальное количество предупреждений ?")]
            public int MaxWarn;

            [JsonProperty("Использовать вайтлист по окончания регистрации ?")]
            public bool useWhiteList;

            [JsonProperty("Настройки RustMap")] 
            public RustMapsSettings _RustMapsSettings = new RustMapsSettings();

            [JsonProperty("Настройки отметки на карте ( G )")] 
            public MarkerInGameSettings _MarkerInGameSettings = new MarkerInGameSettings();

            [JsonProperty("Настройки рейд очков при регистрации")] 
            public RaidPointSettings _RaidPointSettings = new RaidPointSettings();

            [JsonProperty("Config version")]
            public VersionNumber PluginVersion = new VersionNumber();

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    openMenuCommand = "ttop",
                    openHistoryMenuCommand = "history",
                    MinPoints = 1000,
                    HoursReg = 24,
                    MaxWarn = 2,
                    _RustMapsSettings = new RustMapsSettings()
                    {
                        UseRustMap = false,
                        Icon = "https://i.ibb.co/gj54RB5/1.png",
                        sizeMarker = 0.06f,
                    },
                    _MarkerInGameSettings = new MarkerInGameSettings()
                    {
                        UseGameMarker = false,
                        MarkerColor = "#ffb700",
                        MarkerRadius = 1f,
                    },
                    _RaidPointSettings = new RaidPointSettings()
                    {
                        useAutomaticRaidPoint = true,
                        useQualityCeiling = true,
                        CeilingPoint = 1000,
                        CeilingObject = 500,
                    },
                    PluginVersion = new VersionNumber()
                };
            }
        }
        #endregion
    }
}