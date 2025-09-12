using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Oxide.Plugins.TournamentClanExtensionMethods;

namespace Oxide.Plugins
{
    [Info("TournamentClan", "XAVIER", "1.0.0")]
    public class TournamentClan : RustPlugin
    {
        #region Var

        [PluginReference] private Plugin RustMap, Clans;
        private static TournamentClan _ins;
        public string Layer = "UI_MaskLayer";

        public class CupboardData
        {
            public uint netID;

            public bool Remove;

            public string nameClan;
        }


        public List<CupboardData> _cupboard = new List<CupboardData>();
        public List<CupboardComponent> _component = new List<CupboardComponent>();


        #endregion


        #region UI && Hooks

        
        private void OnLootEntityEnd(BasePlayer player, BuildingPrivlidge entity) => CuiHelper.DestroyUi(player, Layer);

        private void OnLootEntity(BasePlayer player, BuildingPrivlidge entity)
        {
            if (player == null) return;
            if (entity == null) return;
            if (!player.IsHuman()) return;
            CuiHelper.DestroyUi(player, Layer);
            var container = new CuiElementContainer();
            
            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "193 491", OffsetMax = "572 555"},
                Image = {Color = "0 0 0 0"}
            }, "Overlay", Layer);
            
            


            string nameClan = GetTag(player);
            if (string.IsNullOrEmpty(nameClan))
            {
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin =
                            $"0 0",
                        AnchorMax =
                            $"1 1",
                    },
                    Button =
                    {
                        Color = HexToCuiColor($"#AA4734", 100),
                    },
                    Text =
                    {
                        Text = "ВЫ НЕ МОЖЕТЕ ПРИНЯТЬ УЧАСТИЕ В ТУРНИРЕ\nПРИЧИНА: ВЫ НЕ СОСТОИТЕ В КЛАНЕ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 14, Color = HexToCuiColor($"#C5BBB3")
                    }
                }, Layer);
                
                
                CuiHelper.AddUi(player, container);
                return;
            }
            bool isOwnerAndModerator = GetModerator(player);

            if (!isOwnerAndModerator)
            {
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin =
                            $"0 0",
                        AnchorMax =
                            $"1 1",
                    },
                    Button =
                    {
                        Color = HexToCuiColor($"#AA4734", 100),
                    },
                    Text =
                    {
                        Text = "ВЫ НЕ МОЖЕТЕ ПРИНЯТЬ УЧАСТИЕ В ТУРНИРЕ\nПРИЧИНА: ВЫ НЕ ЯВЛЯЕТЕСЬ ГЛАВОЙ / МОДЕРАТОРОМ КЛАНА", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 14, Color = HexToCuiColor($"#C5BBB3")
                    }
                }, Layer);
                
                
                CuiHelper.AddUi(player, container);
                return;
            }

            if (config._MainSettings.MinPoint > 0 && GetPoint(player) < config._MainSettings.MinPoint)
            {
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin =
                            $"0 0",
                        AnchorMax =
                            $"1 1",
                    },
                    Button =
                    {
                        Color = HexToCuiColor($"#AA4734", 100),
                    },
                    Text =
                    {
                        Text = $"ВЫ НЕ МОЖЕТЕ ПРИНЯТЬ УЧАСТИЕ В ТУРНИРЕ\nПРИЧИНА: ВАМ НЕОБХОДИМО {config._MainSettings.MinPoint} ОЧКОВ.", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 14, Color = HexToCuiColor($"#C5BBB3")
                    }
                }, Layer);
                
                
                CuiHelper.AddUi(player, container);
                return;
            }

            if (config._MainSettings.MinPlayer > 0 && GetMember(player) < config._MainSettings.MinPlayer)
            {
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin =
                            $"0 0",
                        AnchorMax =
                            $"1 1",
                    },
                    Button =
                    {
                        Color = HexToCuiColor($"#AA4734", 100),
                    },
                    Text =
                    {
                        Text = $"ВЫ НЕ МОЖЕТЕ ПРИНЯТЬ УЧАСТИЕ В ТУРНИРЕ\nПРИЧИНА: ВАМ НЕОБХОДИМО {config._MainSettings.MinPlayer} ИГРОКОВ В КЛАНЕ.", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 14, Color = HexToCuiColor($"#C5BBB3")
                    }
                }, Layer);
                
                
                CuiHelper.AddUi(player, container);
                return;
            }

            if (config._MainSettings.MinFoundation > 0 && CheckFoundation(entity) < config._MainSettings.MinFoundation)
            {
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin =
                            $"0 0",
                        AnchorMax =
                            $"1 1",
                    },
                    Button =
                    {
                        Color = HexToCuiColor($"#AA4734", 100),
                    },
                    Text =
                    {
                        Text = $"ВЫ НЕ МОЖЕТЕ ПРИНЯТЬ УЧАСТИЕ В ТУРНИРЕ\nПРИЧИНА: ВАМ НЕОБХОДИМО {config._MainSettings.MinFoundation} ФУНДАМЕНТОВ.", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 14, Color = HexToCuiColor($"#C5BBB3")
                    }
                }, Layer);
                
                
                CuiHelper.AddUi(player, container);
                return;
            }

            if (IsTimeExit())
            {
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin =
                            $"0 0",
                        AnchorMax =
                            $"1 1",
                    },
                    Button =
                    {
                        Color = HexToCuiColor($"#AA4734", 100),
                    },
                    Text =
                    {
                        Text = $"ВЫ НЕ МОЖЕТЕ ПРИНЯТЬ УЧАСТИЕ В ТУРНИРЕ\nПРИЧИНА: ПРОШЛО ВРЕМЯ РЕГИСТРАЦИИ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 14, Color = HexToCuiColor($"#C5BBB3")
                    }
                }, Layer);
                
                
                CuiHelper.AddUi(player, container);
                return;
            }

            if (GetActive(nameClan))
            {
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin =
                            $"0 0",
                        AnchorMax =
                            $"1 1",
                    },
                    Button =
                    {
                        Color = HexToCuiColor($"#AA4734", 100),
                    },
                    Text =
                    {
                        Text = $"ВЫ НЕ МОЖЕТЕ ПРИНЯТЬ УЧАСТИЕ В ТУРНИРЕ\nПРИЧИНА: ВЫ УЖЕ УЧАВСТВУЕТЕ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 14, Color = HexToCuiColor($"#C5BBB3")
                    }
                }, Layer);
                
                
                CuiHelper.AddUi(player, container);
                return;
            }

            if (GetRemove(nameClan))
            {
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin =
                            $"0 0",
                        AnchorMax =
                            $"1 1",
                    },
                    Button =
                    {
                        Color = HexToCuiColor($"#AA4734", 100),
                    },
                    Text =
                    {
                        Text = $"ВЫ НЕ МОЖЕТЕ ПРИНЯТЬ УЧАСТИЕ В ТУРНИРЕ\nПРИЧИНА: ВЫ ВЫБЫЛИ ИЗ ТУРНИРА", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 14, Color = HexToCuiColor($"#C5BBB3")
                    }
                }, Layer);
                
                
                CuiHelper.AddUi(player, container);
                return;
            }
            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin =
                        $"0 0",
                    AnchorMax =
                        $"1 1",
                },
                Button =
                {
                    Color = HexToCuiColor($"#5D6A36", 100),
                    Command = $"TournamentHandler {entity.net.ID}"
                },
                Text =
                {
                    Text = $"ВЫ МОЖЕТЕ ПРИНЯТЬ УЧАСТИЕ В ТУРНИРЕ\nНАЖМИТЕ ЧТО БЫ ПРИНЯТЬ УЧАСТИЕ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 14, Color = HexToCuiColor($"#BFF069")
                }
            }, Layer);
                
                
            CuiHelper.AddUi(player, container);
            
        }

        #endregion
        
        
        #region Hooks

        void OnServerInitialized()
        {
            _ins = this;
            try
            {
                _cupboard = Interface.GetMod().DataFileSystem.ReadObject<List<CupboardData>>(Name);
                if (_cupboard == null)
                    _cupboard = new List<CupboardData>();
            }
            catch
            {
                _cupboard = new List<CupboardData>();
            }

            AnyCupboardChecks();
        }

        void OnServerSave() => Interface.GetMod().DataFileSystem.WriteObject(Name, _cupboard);

        void Unload()
        {
            Interface.GetMod().DataFileSystem.WriteObject(Name, _cupboard);
            _component.ForEach(component =>
            {
                component.Kill();
            });
            _component = null;
            _ins = null;
            config = null;
            TournamentClanExtensionMethods.ExtensionMethods.p = null;
        }
        
        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (Clans == null)
                return null;
            if (entity == null || info == null) return null;
            if (info.InitiatorPlayer == null) return null;
            try
            {
                var build = entity as BuildingPrivlidge;
                if (build != null && build.GetComponent<CupboardComponent>() != null)
                {
                    var target = info.InitiatorPlayer;
                    var isMember = (bool)Clans.CallHook("HasFriend", build.OwnerID, target.userID);
                    if (isMember)
                    {
                        if (target.SecondsSinceAttacked > 5)
                        {
                            target.ChatMessage("Вы не можете наносить урон своему шкафу, который является турнирным");
                            target.lastAttackedTime = UnityEngine.Time.time;
                            return false;
                        }
                    }
                }
            }
            catch (NullReferenceException e)
            {
                
            }
            
            return null;
        }
        
        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (Clans == null)
                return;
            if (entity == null || info == null) return;
            try
            {
                var target = info.InitiatorPlayer;
                if (target == null) return;
                var build = entity as BuildingPrivlidge;
                if (build != null && build.GetComponent<CupboardComponent>() != null)
                {
                    var findData = _cupboard.FirstOrDefault(p => p.netID == build.net.ID);
                    if (findData != null)
                    {
                        var component = build.GetComponent<CupboardComponent>();
                        _component.Remove(component);
                        build.GetComponent<CupboardComponent>().Kill();
                        findData.Remove = true;
                        Clans?.CallHook("ScoreRemove", findData.nameClan, info.InitiatorPlayer.userID, config._MainSettings.PercentRemove); // изменил метод, теперь вместо ulong первый аргумент идет название клана, которого зарейдили.
                        string nameAttacker = Clans?.Call("GetClanTag", info.InitiatorPlayer.userID) as string;
                        
                        Server.Broadcast($"Клан {nameAttacker} зарейдил клан {findData.nameClan}");
                        
                        
                        // Логирование, непонятно только зачем, но пусть будет.
                        
                        LogToFile(Name, $"Клан {findData.nameClan} был зарейжен кланом {nameAttacker}. Клан вылетел из турнира в {DateTime.Now.ToString(CultureInfo.InvariantCulture)}", this);
                    }
                }
            }
            catch (NullReferenceException e)
            {
                
            }
            
        }


        #endregion


        #region ConsoleCommand


        [ChatCommand("regtc")]
        void TournamentHandlerCommand(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            if (args.Args.Length == 0) return;
            
            string nameClan = GetTag(player);
            if (string.IsNullOrEmpty(nameClan)) return;
            
            
            uint buildBlock = 0;
            
            if (uint.TryParse(args.Args[0], out buildBlock))
            {
                BuildingPrivlidge entity = BaseNetworkable.serverEntities.Find(buildBlock) as BuildingPrivlidge;
                if (entity == null) return;
                bool isOwnerAndModerator = GetModerator(player);
                if (!isOwnerAndModerator) return;
                if (config._MainSettings.MinPoint > 0 && GetPoint(player) < config._MainSettings.MinPoint) return;
                if (config._MainSettings.MinPlayer > 0 && GetMember(player) < config._MainSettings.MinPlayer) return;
                if (config._MainSettings.MinFoundation > 0 &&
                    CheckFoundation(entity) < config._MainSettings.MinFoundation) return;
                if (IsTimeExit()) return;
                if (GetActive(nameClan)) return;
                if (GetRemove(nameClan)) return;
                
                _cupboard.Add(new CupboardData
                {
                    nameClan = nameClan,
                    netID = entity.net.ID,
                    Remove = false,
                });
                
                if (entity.GetComponent<CupboardComponent>() == null)
                {
                    entity.gameObject.AddComponent<CupboardComponent>().Init(nameClan);
                }
                
                
                Effect z = new Effect("assets/bundled/prefabs/fx/invite_notice.prefab", player, 0, new Vector3(), new Vector3());
                EffectNetwork.Send(z, player.Connection);
                
                player.ChatMessage("Вы успешно зарегистрировали свой клан!");
                player.EndLooting();
            }
        }

        #endregion
        
        #region Functional

        public void AnyCupboardChecks()
        {
            if (_cupboard.Count > 0)
            {
                foreach (var ent in _cupboard.FindAll(p => p.Remove == false))
                {   // вы заебали уже менять автора, реально. Автор плагина: XAVIER.
                    var cupboard = BaseNetworkable.serverEntities.Find(ent.netID);
                    if (cupboard != null && !cupboard.IsDestroyed)
                    {
                        if (cupboard.GetComponent<CupboardComponent>() == null)
                        {
                            cupboard.gameObject.AddComponent<CupboardComponent>().Init(ent.nameClan);
                        }
                    }
                }
            }
        }


        public string GetTag(BasePlayer player)
        {
            return (string) Clans?.CallHook("GetClanTag", player.userID);
        }

        public bool GetModerator(BasePlayer player)
        {
            // ReSharper disable once PossibleNullReferenceException
            
            return (bool) Clans?.CallHook("IsModerator", player.userID);
        }

        public int GetMember(BasePlayer player)
        {
            // ReSharper disable once PossibleNullReferenceException
            
            return (int)Clans?.CallHook("GetMember", player.userID);
        }


        public int GetPoint(BasePlayer player)
        {
            // ReSharper disable once PossibleNullReferenceException
            
            return (int) Clans?.Call("GetPoint", player.userID);
        }


        public int CheckFoundation(BuildingPrivlidge entity)
        {
            var build = entity.GetBuildingPrivilege(entity.WorldSpaceBounds()).GetBuilding();
            var foundationlist = build.decayEntities.ToList().FindAll(p => p.PrefabName == "assets/prefabs/building core/foundation/foundation.prefab" || p.PrefabName == "assets/prefabs/building core/foundation.triangle/foundation.triangle.prefab");
            return foundationlist.Count;
        }

        public bool GetActive(string nameClan)
        {
            return _cupboard.FirstOrDefault(p => p.nameClan == nameClan && !p.Remove) != null;
        }


        public bool GetRemove(string nameClan)
        {
            return _cupboard.FirstOrDefault(p => p.nameClan == nameClan && p.Remove) != null;
        }
        
        static DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);

        public bool IsTimeExit()
        {
            if (config._MainSettings.ActiveRegister == 0)
                return false;
            var timeWipe = SaveRestore.SaveCreatedTime.ToUniversalTime().Subtract(epoch).TotalSeconds + config._MainSettings.ActiveRegister - Facepunch.Math.Epoch.Current;
            return timeWipe < 0;
        }
        
        public string HexToCuiColor(string HEX, float Alpha = 100)
        {
            if (string.IsNullOrEmpty(HEX)) HEX = "#FFFFFF";

            var str = HEX.Trim('#');
            if (str.Length != 6) throw new Exception(HEX);
            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

            return $"{(double) r / 255} {(double) g / 255} {(double) b / 255} {Alpha / 100}";
        }

        #endregion
        
        
        #region Component


        public class CupboardComponent : MonoBehaviour
        {
            private BuildingPrivlidge _privlidge;
            public string nameClan;
            VendingMachineMapMarker vendingMarker;
            private void Awake()
            {
                _privlidge = GetComponent<BuildingPrivlidge>();
            }

            public void Init(string name)
            {
                nameClan = name;
                _ins._component.Add(this);
                CheckAnyMaps(); // вы заебали уже менять автора, реально. Автор плагина: XAVIER.
            }

            public void Kill()
            {
                DestroyImmediate(this);
            }

            public void DestroyMarker()
            {
                if (vendingMarker != null && !vendingMarker.IsDestroyed) vendingMarker.Kill();
                if (_ins.RustMap && _ins.config._MapSettings.isRustMap)
                    _ins.RustMap.CallHook("ApiRemovePointUrl", $"clanTournament{_privlidge.net.ID}");
            }

            private void OnDestroy()
            {
                if (IsInvoking())
                    CancelInvoke();
                DestroyMarker();
            }

            public void CheckAnyMaps()
            {
                DestroyMarker();
                
                if (_ins.config._MapSettings.isRustMap)
                    RustMapActive();
                if (_ins.config._MapSettings.isMapGame)
                    MarkerActive();
            }

            public void MarkerActive()
            {
                var position = _privlidge.transform.position;
                position.x = _ins.config._MapSettings.RotateMarker ? position.x + UnityEngine.Random.Range(5f, 20f) : position.x;
                vendingMarker = GameManager.server.CreateEntity("assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab", position).GetComponent<VendingMachineMapMarker>();
                vendingMarker.markerShopName = nameClan.ToUpper();
                vendingMarker.Spawn();
                
            }

            public void RustMapActive()
            {
                _ins.RustMap?.Call("ApiAddPointUrl", _ins.config._MapSettings.IconRustMap, $"clanTournament{_privlidge.net.ID}", _privlidge.transform.position, nameClan.ToUpper(), 0.04f);
            }
        }

        #endregion
        

        #region Plugin Remove API

        string CanRemove(BasePlayer player, BaseEntity entity)
        {
            if (player == null || entity == null) return null;
            var build = entity as BuildingPrivlidge;
            if (build == null) return null;
            var find = build.GetComponent<CupboardComponent>();
            if (find != null)
            {
                return "Вы не можете удалить турнирный шкаф!";
            }
            return null;
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
                PrintWarning("Привет. Данный плагин написал XAVIER. А собственно, вот его группа вк: vk.com/xavierrust");
            }

            config.PluginVersion = Version;
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }


        public class MainSettings
        {
            [JsonProperty("Минимальное количество игроков в клане для регистрации")] 
            public int MinPlayer;
            
            [JsonProperty("Минимальное количество очков в клане для регистрации")] 
            public int MinPoint;

            [JsonProperty("Минимальное количество фундаментов для регистрации")]
            public int MinFoundation;
            
            [JsonProperty("Сколько будет открыта регистрация после вайпа ( Если установить 0, то регистрация будет вечна. ПРИМЕР: 86400 секунд - значит ровно через 1 день после вайпа регистрация будет закрыта )")]
            public int ActiveRegister;

            [JsonProperty("Сколько отнимать очков от общего количество ( в процентах )")]
            public int PercentRemove;
        }

        public class MapSettings
        {
            [JsonProperty("Добавить поддержку RustMap ? ( https://rustplugin.ru/resources/rust-map.91/ )")]
            public bool isRustMap;

            [JsonProperty("Иконка на карту RustMap")]
            public string IconRustMap;

            [JsonProperty("Добавить маркер на карту G ?")]
            public bool isMapGame;

            [JsonProperty("Сдвигать ли маркер на карте G от основной позиции шкафа ?")]
            public bool RotateMarker;
        }
        


        private class PluginConfig
        {
            [JsonProperty("Основные настройки")]
            public MainSettings _MainSettings = new MainSettings();

            [JsonProperty("Настройка карты")] 
            public MapSettings _MapSettings = new MapSettings();
            
            
            

            [JsonProperty("Версия конфигурации")] 
            public VersionNumber PluginVersion = new VersionNumber();

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    #region MainSettings

                    _MainSettings = new MainSettings()
                    {
                        MinPlayer = 1,
                        MinPoint = 1,
                        MinFoundation = 10,
                        ActiveRegister = 0,
                    },

                    #endregion

                    #region MapSettings

                    _MapSettings = new MapSettings()
                    {
                      isRustMap  = false,
                      IconRustMap = "",
                      isMapGame = true,
                      RotateMarker = true,
                    },

                    #endregion
                    
                    #region Verison

                    PluginVersion = new VersionNumber(),

                    #endregion

                };
            }
        }

        #endregion
    }
}

#region Linq

    namespace Oxide.Plugins.TournamentClanExtensionMethods
    {
        public static class ExtensionMethods
        {
            internal static Core.Libraries.Permission p;
            public static bool All<T>(this IList<T> a, Func<T, bool> b) { for (int i = 0; i < a.Count; i++) { if (!b(a[i])) { return false; } } return true; }
            public static int Average(this IList<int> a) { if (a.Count == 0) { return 0; } int b = 0; for (int i = 0; i < a.Count; i++) { b += a[i]; } return b / a.Count; }
            public static T ElementAt<T>(this IEnumerable<T> a, int b) { using (var c = a.GetEnumerator()) { while (c.MoveNext()) { if (b == 0) { return c.Current; } b--; } } return default(T); }
            public static bool Exists<T>(this HashSet<T> a) where T : BaseEntity { foreach (var b in a) { if (!b.IsKilled()) { return true; } } return false; }
            public static bool Exists<T>(this IEnumerable<T> a, Func<T, bool> b = null) { using (var c = a.GetEnumerator()) { while (c.MoveNext()) { if (b == null || b(c.Current)) { return true; } } } return false; }
            public static T FirstOrDefault<T>(this IEnumerable<T> a, Func<T, bool> b = null) { using (var c = a.GetEnumerator()) { while (c.MoveNext()) { if (b == null || b(c.Current)) { return c.Current; } } } return default(T); }
            public static int RemoveAll<TKey, TValue>(this IDictionary<TKey, TValue> c, Func<TKey, TValue, bool> d) { int a = 0; foreach (var b in c.ToList()) { if (d(b.Key, b.Value)) { c.Remove(b.Key); a++; } } return a; }
            public static IEnumerable<V> Select<T, V>(this IEnumerable<T> a, Func<T, V> b) { var c = new List<V>(); using (var d = a.GetEnumerator()) { while (d.MoveNext()) { c.Add(b(d.Current)); } } return c; }
            public static Dictionary<T, V> ToDictionary<S, T, V>(this IEnumerable<S> a, Func<S, T> b, Func<S, V> c) { var d = new Dictionary<T, V>(); using (var e = a.GetEnumerator()) { while (e.MoveNext()) { d[b(e.Current)] = c(e.Current); } } return d; }
            public static List<T> ToList<T>(this IEnumerable<T> a) { var b = new List<T>(); if (a == null) { return b; } using (var c = a.GetEnumerator()) { while (c.MoveNext()) { b.Add(c.Current); } } return b; }
            public static List<T> Where<T>(this IEnumerable<T> a, Func<T, bool> b) { var c = new List<T>(); using (var d = a.GetEnumerator()) { while (d.MoveNext()) { if (b(d.Current)) { c.Add(d.Current); } } } return c; }
            public static List<T> OfType<T>(this IEnumerable<BaseNetworkable> a) where T : BaseEntity { var b = new List<T>(); using (var c = a.GetEnumerator()) { while (c.MoveNext()) { if (c.Current is T) { b.Add(c.Current as T); } } } return b; }
            public static int Sum<T>(this IList<T> a, Func<T, int> b) { int c = 0; for (int i = 0; i < a.Count; i++) { var d = b(a[i]); if (!float.IsNaN(d)) { c += d; } } return c; }
            public static bool HasPermission(this string a, string b) { if (p == null) { p = Interface.Oxide.GetLibrary<Core.Libraries.Permission>(null); } return !string.IsNullOrEmpty(a) && p.UserHasPermission(a, b); }
            public static bool HasPermission(this BasePlayer a, string b) { return a.UserIDString.HasPermission(b); }
            public static bool HasPermission(this ulong a, string b) { return a.ToString().HasPermission(b); }
            public static bool IsReallyConnected(this BasePlayer a) { return a.IsReallyValid() && a.net.connection != null; }
            public static bool IsKilled(this BaseNetworkable a) { return (object)a == null || a.IsDestroyed; }
            public static bool IsNull<T>(this T a) where T : class { return a == null; }
            public static bool IsNull(this BasePlayer a) { return (object)a == null; }
            public static bool IsReallyValid(this BaseNetworkable a) { return !((object)a == null || a.IsDestroyed || (object)a.net == null); }
            public static void SafelyKill(this BaseNetworkable a) { if (a.IsKilled()) { return; } a.Kill(BaseNetworkable.DestroyMode.None); }
            public static bool CanCall(this Plugin o) { return (object)o != null && o.IsLoaded; }
            public static bool IsInBounds(this OBB o, Vector3 a) { return o.ClosestPoint(a) == a; }
            public static bool IsHuman(this BasePlayer a) { return !(a.IsNpc || !a.userID.IsSteamId()); }
            public static bool IsCheating(this BasePlayer a) { return a._limitedNetworking || a.IsFlying || a.UsedAdminCheat(30f) || a.IsGod() || a.metabolism?.calories?.min == 500; }
            public static void SetAiming(this BasePlayer a, bool f) { a.modelState.aiming = f; a.SendNetworkUpdate(); }
            public static BasePlayer ToPlayer(this IPlayer user) { return user.Object as BasePlayer; }
            public static string ObjectName(this Collider collider) { try { return collider.gameObject?.name ?? string.Empty; } catch { return string.Empty; } }
            public static Vector3 GetPosition(this Collider collider) { try { return collider.transform?.position ?? Vector3.zero; } catch { return Vector3.zero; } }
            public static string ObjectName(this BaseEntity entity) { try { return entity.name ?? string.Empty; } catch { return string.Empty; } }
            public static T GetRandom<T>(this HashSet<T> h, string check = "@PrOmOcOdE") { if (h == null || h.Count == 0) { return default(T); } return h.ElementAt(UnityEngine.Random.Range(0, h.Count)); }
            public static int InventorySlots(this StorageContainer a) { if (a.IsKilled() || a.inventory == null) return 0; return a.inventorySlots; }
            public static IEnumerable<V> Select<T, V>(this IEnumerable<T> a, Func<T, int, V> b) { int c = -1; foreach (T d in a) { yield return b(d, ++c); } }
            public static IEnumerable<T> Skip<T>(this IEnumerable<T> a, int b) { using (var e = a.GetEnumerator()) { while (b > 0 && e.MoveNext()) { b--; } if (b <= 0) { while (e.MoveNext()) { yield return e.Current; } } } }
            public static IEnumerable<T> Take<T>(this IEnumerable<T> a, int b) { if (b <= 0) { yield break; } foreach (T c in a) { yield return c; int d = b - 1; b = d; if (d == 0) { break; } } }
        }
    }

    #endregion