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
    [Info("ClanTournament", "vins", "0.0.1")]
    public class ClanTournament : RustPlugin
    {

        [PluginReference] private Plugin Clans;
        public class CupSettings
        {
            public ulong ownerclan;
            public bool isremove;
            public uint build;
        }
        public List<CupSettings> ClanData = new List<CupSettings>();

        [PluginReference] private Plugin RustMap;
        void AddMarker(CupSettings settings, string icon = "https://i.ibb.co/gj54RB5/1.png")
        {
            if (string.IsNullOrEmpty(icon))
            {
                PrintWarning("Иконка нуль! Маркер не добавился!");
                return;
            }
            RemoveMarker(settings);
            string name = Clans?.Call("ClanAlready", settings.ownerclan) as string;
            RustMap?.Call("ApiAddPointUrl", icon, $"priv{settings.build}", BaseNetworkable.serverEntities.Find(settings.build)?.transform.position, name, 0.06f);
        }
        void RemoveMarker(CupSettings settings)
        {
            RustMap?.Call("ApiRemovePointUrl", $"priv{settings.build}");
        }

        void OnServerInitialized()
        {
            try
            {
                ClanData = Interface.GetMod().DataFileSystem.ReadObject<List<CupSettings>>(Name);
            }
            catch
            {
                ClanData = new List<CupSettings>();
            }
            if (ClanData.Count > 0)
            {
                foreach (var cup in ClanData)
                {
                    var entity = BaseNetworkable.serverEntities.Find(cup.build);
                    if (entity == null && cup.isremove == false)
                    {
                        cup.isremove = true;
                    }
                    if (entity != null && cup.isremove == false)
                    {
                        AddMarker(cup);
                    }
                }
            }
        }

        void OnServerSave()
        {
            if (ClanData != null)
                Interface.Oxide.DataFileSystem.WriteObject(Name, ClanData);
        }

        void Unload()
        {
            if (ClanData.Count > 0)
            {
                foreach (var cup in ClanData)
                {
                    if (cup.isremove == false)
                    {
                        RemoveMarker(cup);
                    }
                }
            }
            if (ClanData != null)
                Interface.Oxide.DataFileSystem.WriteObject(Name, ClanData);
        }

        string CanRemove(BasePlayer player, BaseEntity entity)
        {
            if (!(entity as BuildingPrivlidge)) return null;
            var find = ClanData.FirstOrDefault(p => p.build == entity.net.ID);
            if (find != null && find.isremove == false)
            {
                return "Вы не имеете право удалять данный шкаф!";
            }
            return null;
        }
        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return null;
            if (info.InitiatorPlayer == null) return null;
            if (entity as BuildingPrivlidge)
            {
                var find = ClanData.FirstOrDefault(p => p.build == entity.net.ID);
                if (find != null)
                {
                    var clan = Clans?.CallHook("CheckClans", entity.net.ID, info.InitiatorPlayer.OwnerID);
                    if (clan is bool && (bool)clan)
                    {
                        info.InitiatorPlayer.ChatMessage("Вы не имеете право уничтожать турнирный шкаф своего же клана!");
                        info.damageTypes.ScaleAll(0f);
                        return false;
                    }
                    if (entity.OwnerID == info.InitiatorPlayer.userID)
                    {
                        info.InitiatorPlayer.ChatMessage("Вы не имеете право уничтожать турнирный шкаф своего же клана!");
                        info.damageTypes.ScaleAll(0f);
                        return false;
                    }
                }
            }
            return null;
        }



        bool CheckTournament(ulong owner)
        {
            var find = ClanData.FirstOrDefault(p => p.ownerclan == owner);
            if (find == null)
            {
                return false;
            }
            if (find.isremove)
            {
                return false;
            }
            return true;
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null) return;
            if (entity as BuildingPrivlidge && info != null && info.InitiatorPlayer != null)
            {
                var find = ClanData.FirstOrDefault(p => p.build == entity.net.ID);
                if (find != null)
                {
                    if (find.isremove == true)
                    {
                        info.InitiatorPlayer.ChatMessage("Произошла критическая ошибка! Плагин не выдал очки");
                        return;
                    }
                    Clans?.CallHook("ScoreRemove", find.ownerclan, info.InitiatorPlayer.userID);
                    info.InitiatorPlayer.ChatMessage("<color=orange>Вы уничтожили турнирный шкаф чужого клана!</color>");
                    find.isremove = true;
                    string name = Clans?.Call("ClanAlready", find.ownerclan) as string;
                    string clanAttacker = Clans?.Call("ClanAlready", info.InitiatorPlayer.userID) as string;
                    foreach (var player13 in BasePlayer.activePlayerList)
                    {
                        player13.ChatMessage($"Клан <color=orange>{clanAttacker}</color> взорвал шкаф клана <color=orange>{name}</color>");
                    }
                    RemoveMarker(find);
                    LogToFile("ClanTournament", $"Клан {name} вылетел из турнира в {DateTime.Now.ToString(CultureInfo.InvariantCulture)}", this);
                }
            }
        }

        public string CupLayer = "UI_CupLayer";

        static readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);
        static double CurrentTime() { return DateTime.UtcNow.Subtract(epoch).TotalSeconds; }

        private double IsBlocked()
        {
            var lefTime = SaveRestore.SaveCreatedTime.ToUniversalTime().Subtract(epoch).TotalSeconds + 86400 - CurrentTime();
            return lefTime > 0 ? lefTime : 0;
        }

        private static T GetLookEntity<T>(BasePlayer player)
        {
            RaycastHit hit;
            if (Physics.Raycast(player.eyes.HeadRay(), out hit) == false)
            {
                return default(T);
            }

            var entity = hit.GetEntity();
            if (entity == null)
            {
                return default(T);
            }

            return entity.GetComponent<T>();
        }

        [ChatCommand("regtc")]
        void RegClanV2(BasePlayer player)
        {
            var entity = GetLookEntity<BuildingPrivlidge>(player);
            uint ent = entity.net.ID;
            var build = entity.GetBuildingPrivilege(entity.WorldSpaceBounds()).GetBuilding();
            var foundationlist = build.decayEntities.ToList().FindAll(p => p.PrefabName == "assets/prefabs/building core/foundation/foundation.prefab" || p.PrefabName == "assets/prefabs/building core/foundation.triangle/foundation.triangle.prefab");
            object clans = Clans?.CallHook("ClanPoint", player.userID);
            object clan = Clans?.CallHook("ClanCount", player.userID);

            if (IsBlocked() == 0)
            {
                player.ChatMessage("Прошло 26 часов. Вы опоздали!");
                return;
            }

            if (clan is bool && !(bool)clan)
            {
                player.ChatMessage("Либо вы не состоите в клане, либо вы не глава клана.");
                return;
            }

            if (foundationlist.Count < 100)
            {
                player.ChatMessage("Нужно иметь больше 100 фундаментов!");
                return;
            }

            if (clans is int && (int)clans < 1000)
            {
                player.ChatMessage("Нужно иметь больше 1000 очков!");
                return;
            }

            var find = ClanData.FirstOrDefault(p => p.ownerclan == player.userID);
            if (find != null)
            {
                if (find.isremove == true)
                {
                    player.ChatMessage("Ваш шкаф был уничтожен!");
                    return;
                }

                player.ChatMessage("Вы уже участвуете в турнире!");
                return;
            }


            CupSettings settings = new CupSettings()
            {
                ownerclan = player.userID,
                isremove = false,
                build = ent
            };
            ClanData.Add(settings);
            player.ChatMessage("Вы успешно зарегистрировались на турнир!");
            CuiHelper.DestroyUi(player, CupLayer);
            AddMarker(settings);
            string name = Clans?.Call("ClanAlready", settings.ownerclan) as string;
            LogToFile("ClanTournament", $"Клан {name} зарегистрировались на турнир в {DateTime.Now.ToString(CultureInfo.InvariantCulture)}", this);
        }

        [ConsoleCommand("registration.clanssss")]
        void RegClan(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            uint ent = uint.Parse(args.Args[0]);
            CupSettings settings = new CupSettings()
            {
                ownerclan = player.userID,
                isremove = false,
                build = ent
            };

            ClanData.Add(settings);
            player.ChatMessage("Вы успешно зарегистрировались на турнир!");
            CuiHelper.DestroyUi(player, CupLayer);
            AddMarker(settings);
            string name = Clans?.Call("ClanAlready", settings.ownerclan) as string;
            LogToFile("ClanTournament", $"Клан {name} зарегистрировались на турнир в {DateTime.Now.ToString(CultureInfo.InvariantCulture)}", this);
        }

        private static string HexToCuiColor(string hex) { if (string.IsNullOrEmpty(hex)) { hex = "#FFFFFFFF"; } var str = hex.Trim('#'); if (str.Length == 6) str += "FF"; if (str.Length != 8) { throw new Exception(hex); throw new InvalidOperationException("Cannot convert a wrong format."); } var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber); var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber); var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber); var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber); Color color = new Color32(r, g, b, a); return $"{color.r:F2} {color.g:F2} {color.b:F2} {color.a:F2}"; }

        private void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (player == null || entity == null) return;
            if (entity as BuildingPrivlidge)
            {
                CuiHelper.DestroyUi(player, CupLayer);
                if (entity.OwnerID != player.userID) return;
                CuiElementContainer container = new CuiElementContainer();
                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    RectTransform =
                        {AnchorMin = "0.425 0.475", AnchorMax = "0.575 0.525"},
                    Image = { Color = "0 0 0 0" }
                }, "Overlay", CupLayer);


                if (IsBlocked() == 0)
                {
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Button = { Color = "0.3 0.3 0.3 1", Command = $"" },
                        Text =
                        {
                            Text = $"ПРОШЛО 24 ЧАСА!", Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-regular.ttf", FontSize = 16
                        }
                    }, CupLayer);
                    CuiHelper.AddUi(player, container);
                    return;
                }

                object clans = Clans?.CallHook("ClanPoint", player.userID);
                object clan = Clans?.CallHook("ClanCount", player.userID);
                if (clan is bool && !(bool)clan)
                {
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Button = { Color = "0.3 0.3 0.3 1", Command = $"" },
                        Text =
                        {
                            Text =
                                $"Ошибка! Возможно вы не состоите в клане, или же не являетесь главой клана в котором вы состоите",
                            Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-regular.ttf", FontSize = 16
                        }
                    }, CupLayer);
                    CuiHelper.AddUi(player, container);
                    return;
                }

                var build = entity.GetBuildingPrivilege(entity.WorldSpaceBounds()).GetBuilding();
                var foundationlist = build.decayEntities.ToList().FindAll(p => p.PrefabName == "assets/prefabs/building core/foundation/foundation.prefab" || p.PrefabName == "assets/prefabs/building core/foundation.triangle/foundation.triangle.prefab");
                if (foundationlist.Count < 100)
                {
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Button = { Color = "0.3 0.3 0.3 1", Command = $"" },
                        Text =
                        {
                            Text = $"Необходимо иметь 100 фундаментов!", Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-regular.ttf", FontSize = 16
                        }
                    }, CupLayer);
                    CuiHelper.AddUi(player, container);
                    return;
                }

                if (clans is int && (int)clans < 1000)
                {
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Button = { Color = "0.3 0.3 0.3 1", Command = $"" },
                        Text =
                        {
                            Text = $"Нужно 1000 очков!",
                            Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-regular.ttf", FontSize = 16
                        }
                    }, CupLayer);
                    CuiHelper.AddUi(player, container);
                    return;
                }

                var find = ClanData.FirstOrDefault(p => p.ownerclan == player.userID);
                if (find != null)
                {
                    if (find.isremove == true)
                    {
                        container.Add(new CuiButton
                        {
                            RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                            Button = { Color = "0.3 0.3 0.3 1", Command = $"" },
                            Text =
                            {
                                Text = $"Ваш шкаф уже был уничтожен! вы вылетели с турнира!",
                                Align = TextAnchor.MiddleCenter,
                                Font = "robotocondensed-regular.ttf", FontSize = 16
                            }
                        }, CupLayer);
                        CuiHelper.AddUi(player, container);
                        return;
                    }

                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Button = { Color = "0.3 0.3 0.3 1", Command = $"" },
                        Text =
                        {
                            Text = $"Ошибка! Ваш клан уже зарегистрирован на турнир!", Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-regular.ttf", FontSize = 16
                        }
                    }, CupLayer);
                    CuiHelper.AddUi(player, container);
                    return;
                }

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { Color = "0.3 0.3 0.3 1", Command = $"registration.clanssss {entity.net.ID}" },
                    Text =
                    {
                        Text = $"ЗАРЕГИСТРИРОВАТЬСЯ НА ТУРНИР!", Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf", FontSize = 16
                    }
                }, CupLayer);
                CuiHelper.AddUi(player, container);
            }
        }

        private void OnLootEntityEnd(BasePlayer player, BaseCombatEntity entity) => CuiHelper.DestroyUi(player, CupLayer);


        object OnEntityGroundMissing(BaseEntity entity)
        {
            if (entity == null) return null;
            if (ClanData.Any(p => p.build == entity.net.ID))
            {
                return false;
            }
            return null;
        }
    }
}