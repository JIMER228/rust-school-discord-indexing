using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Steamworks.Data;
using UnityEngine;
using Color = UnityEngine.Color;

namespace Oxide.Plugins
{
    [Info("MBox", "XAVIER", "1.0.0")]
    public class MBox : RustPlugin
    {

        #region Var

        [PluginReference] private Plugin ImageLibrary;
        private static MBox _ins;
        public Timer SpawnBoxTime;
        public Dictionary<ulong, int> _openCrate = new Dictionary<ulong, int>();
        public static string Layer = "UI_MBox";
        



        #endregion

        #region Hooks
        
        


        
        void OnServerInitialized()
        {
            _ins = this;
            ImageLibrary?.CallHook("AddImage", "https://i.imgur.com/pYGyLH8.png", "close");
            ImageLibrary?.CallHook("AddImage", config.ImageBox, "box");

            try
            {
                _openCrate = Interface.GetMod().DataFileSystem
                    .ReadObject<Dictionary<ulong, int>>($"{Name}/ListOpenCrate");
            }
            catch
            {
                _openCrate = new Dictionary<ulong, int>();
            }
            
            GetMonumentPrefab();
            GenerateTime();
        }
        

        void OnServerSave() => Interface.GetMod().DataFileSystem.WriteObject($"{Name}/ListOpenCrate", _openCrate);


        void Unload()
        {
            var find = UnityEngine.Object.FindObjectOfType<BoxComponent>();
            if (find != null)
                UnityEngine.Object.Destroy(find);
            Interface.GetMod().DataFileSystem.WriteObject($"{Name}/ListOpenCrate", _openCrate);
            _ins = null;
        }
        
        
        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return null;
            if (entity.GetComponent<BoxComponent>())
            {
                info.damageTypes?.ScaleAll(0f);
                return false;
            }

            return null;
        }
        object CanLootEntity(BasePlayer player, StorageContainer container)
        {
            if (container == null || player.IsNpc || player == null) return null;

            if (container.GetComponent<BoxComponent>())
            {
                var component = container.GetComponent<BoxComponent>();
                if (component.Time > 0)
                {
                    player.ChatMessage($"<color=#888a8a>Мегаящик заперт! Он откроется через: {TimeSpan.FromSeconds(component.Time)}</color>");
                    return null;
                }
            }
            
            return null;
        }
        
        private void OnLootEntityEnd(BasePlayer player, LootContainer container)
        {
            if (container == null || player.IsNpc || player == null) return;
            if (container.GetComponent<BoxComponent>())
            {
                if (container.inventory.itemList.Count <= 0)
                {
                    Server.Broadcast($"<color=#849c4b>МЕГАЯЩИК</color> был залутан игроком <color=#849c4b>{player.displayName}</color>");
                    container.GetComponent<BoxComponent>().Kill();
                    if (_openCrate.ContainsKey(player.userID))
                        _openCrate[player.userID]++;
                    else _openCrate.Add(player.userID, 1);
                    
                }
            }
        }

        #endregion

        #region MonumentName

        
        void GetMonumentPrefab()
        {
            List<string> monumentInfos = TerrainMeta.Path.Monuments.Select(p => p.name).ToList();
            Interface.Oxide.DataFileSystem.WriteObject($"{Name}/MonumentName", monumentInfos);
        }

        #endregion


        #region Functional
        
        private static class TimeExtensions
        {
            public static string FormatShortTime(TimeSpan time)
            {
                string result = string.Empty;
                if (time.Days != 0) result += $"{time.Days}д ";
                if (time.Hours != 0) result += $"{time.Hours}ч ";
                if (time.Minutes != 0) result += $"{time.Minutes}м ";
                if (time.Seconds != 0) result += $"{time.Seconds}с ";
                return result;
            }
            private static string Format(int units, string form1, string form2, string form3)
            {
                var tmp = units % 10;
                if (units >= 5 && units <= 20 || tmp >= 5 && tmp <= 9) return $"{units} {form1}";
                if (tmp >= 2 && tmp <= 4) return $"{units} {form2}";
                return $"{units} {form3}";
            }
        }


        public Vector3 GetPosition(MonumentInfo info, MonumentSpawn spawn)
        {
            return  info.transform.position + info.transform.rotation * StringExtensions.ToVector3(spawn.Position);
        }
        


        #endregion

        
        #region GenerateSpawn


        void GenerateTime()
        {
            if (SpawnBoxTime != null)
                SpawnBoxTime.Destroy();

            SpawnBoxTime = timer.Once(UnityEngine.Random.Range(config.minSpawnIvent, config.maxSpawnIvent), () =>
            {
                GenerateBox();
            });
        }


        bool GenerateBox()
        {

            var findBox = UnityEngine.Object.FindObjectOfType<BoxComponent>();
            if (findBox != null)
            {
                PrintWarning("Ящик уже где то заспавнен!");
                GenerateTime();
                return false;
            }
            
            var spawnMonument = config._MonumentSpawns.GetRandom();
            if (spawnMonument == null)
            {
                PrintWarning("Не был найден монумент в конфиге для спавна");
                GenerateTime();
                return false;
                
            }

            var find = TerrainMeta.Path.Monuments.FirstOrDefault(p => p.name.ToLower() == spawnMonument.MonumentName.ToLower());
            if (find == null)
            {
                PrintWarning($"РТ {spawnMonument.MonumentName.ToLower()} не был найден на карте");
                GenerateTime();
                return false;
            }

            if (BasePlayer.activePlayerList.Count < _ins.config.minPlayedPlayers)
            {
                PrintWarning("Недостаточно игроков для проведения ивента");
                GenerateTime();
                return false;
            }


            Vector3 position = GetPosition(find, spawnMonument);
            
            BaseEntity Box = GameManager.server.CreateEntity("assets/bundled/prefabs/radtown/underwater_labs/crate_normal.prefab", position);
            Box.Spawn();
            Box.SetFlag(BaseEntity.Flags.Locked, true);
            Box.SendNetworkUpdateImmediate();
            Box.gameObject.AddComponent<BoxComponent>().Initialized(spawnMonument);
            if (SpawnBoxTime != null)
                SpawnBoxTime.Destroy();
            
            Server.Broadcast($"Начался ивент: Мега-Ящик\nВнутри лежит ценный лут\nЯщик появился на {spawnMonument.NameMonument}!");
            
            NotifyOpen("<color=#d3d3d3>МЕГАЯЩИК!</color>", $"<color=#8ebd2e>Мегаящик</color> заспавнился на <color=#8ebd2e>{spawnMonument.NameMonument}</color>. До открытия <color=#8ebd2e>{TimeExtensions.FormatShortTime(TimeSpan.FromSeconds(config.BoxToOpen))}</color>");
            
            foreach (var target in BasePlayer.activePlayerList)
            {
                Effect.server.Run("assets/bundled/prefabs/fx/invite_notice.prefab", target, 0, Vector3.zero, Vector3.forward);
            }
            return true;


        }

        #endregion

        #region UI

        public string HexToCuiColor(string HEX, float Alpha = 100)
            {
                if (string.IsNullOrEmpty(HEX)) HEX = "#FFFFFF";

                var str = HEX.Trim('#');
                var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
                var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
                var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

                return $"{(double) r / 255} {(double) g / 255} {(double) b / 255} {Alpha / 100}";
            }

            public void NotifyOpen(string content, string message)
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    CuiHelper.DestroyUi(player, Layer);
                    var container = new CuiElementContainer();
                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = $"-245 147",
                            OffsetMax = $"-2 200"
                        },
                        Image = { Color = HexToCuiColor("#000000", 40) }
                    }, "Overlay", Layer);

                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0.02316605 0.1151512", AnchorMax = "0.1879022 0.8909093"
                        },
                        Image =
                        {
                            Color = HexToCuiColor("#7b7774"),
                        }
                    },Layer, ".Image");
                    
                    

                    container.Add(new CuiElement
                    {
                        Parent = $".Image",
                        Components =
                        {
                            new CuiRawImageComponent
                                {FadeIn = 0.3f, Png = (string)_ins.ImageLibrary.Call("GetImage", "box")},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1"}
                        }
                    });
                    
                    container.Add(new CuiElement() // content
                    {
                        Parent = Layer,
                        Components =
                        {
                            new CuiTextComponent{Text = $"{content}", Align = TextAnchor.LowerLeft, FontSize = 16, Font = "robotocondensed-bold.ttf"},
                            new CuiRectTransformComponent{AnchorMin = "0.2084941 0.5757585", AnchorMax = "0.9060489 1"},
                            new CuiOutlineComponent {Color = "0 0 0 1", Distance = "0.3 0.3"},
                        }
                    });
                    
                    container.Add(new CuiElement() // message
                    {
                        Parent = Layer,
                        Components =
                        {
                            new CuiTextComponent{Text = $"{message}", Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf", Color = HexToCuiColor($"#c3c3c3")},
                            new CuiRectTransformComponent{AnchorMin = "0.2084941 0.05454618", AnchorMax = "0.9060489 0.5757581"},
                            new CuiOutlineComponent {Color = "0 0 0 1", Distance = "0.3 0.3"},
                        }
                    });
                    
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0.942085 0.7333335", AnchorMax = "0.995 0.990"},
                        Button =
                        {
                            Close = Layer,
                            Sprite = "assets/content/ui/ui.background.tile.psd",
                            Color = HexToCuiColor("#b1392c")
                        },
                        Text =
                        {
                            Text = "", Color = HexToCuiColor($"#e1d6cc"),
                            Align = TextAnchor.MiddleCenter, FontSize = 13, Font = "robotocondensed-bold.ttf"
                        }
                    }, Layer,$".Close");
                    
                    container.Add(new CuiElement
                    {
                        Parent = $".Close",
                        Components =
                        {
                            new CuiRawImageComponent
                                {FadeIn = 0.3f, Png = (string)_ins.ImageLibrary.Call("GetImage", "close")},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "3 3", OffsetMax = "-3 -3"},
                        }
                    });

                    CuiHelper.AddUi(player, container);

                    _ins.timer.Once(15, () =>
                    {
                        if (player != null)
                            CuiHelper.DestroyUi(player, Layer);
                    });
                }
            }

        #endregion


        #region Component


        public class BoxComponent : MonoBehaviour
        {
            public BaseEntity entity;
            public int Time = 0;
            public int BoxToClose = 0;
            public MonumentSpawn info;
            MapMarkerGenericRadius mapMarker;
            private void Awake()
            {
                entity = GetComponent<BaseEntity>();
            }

            public void Initialized(MonumentSpawn spawn)
            {
                info = spawn;
                Time = _ins.config.BoxToOpen;
                Invoke("InvokeTime", 1f);
                DestroyMarker();
                UpdateMarker();

            }

            #region Invoke

            public void InvokeTime()
            {
                if (Time > 0)
                {
                    Time--;
                    if (Time <= 0)
                    {
                        UnlockCrate();
                        return;
                    }
                    Invoke("InvokeTime", 1f);
                }
            }
            
            public void InvokeClose()
            {
                if (BoxToClose > 0)
                {
                    BoxToClose--;
                    if (BoxToClose <= 0)
                    {
                        Kill();
                        _ins.Server.Broadcast("Закончился ивент: Мега-Ящик! Ящик никем не был никем залутан");
                        return;
                    }
                    Invoke("InvokeClose", 1f);
                }
                
            }

            #endregion



            #region Functional
            
            public void UnlockCrate()
            {
                if (Time <= 0 && BoxToClose <= 0)
                {
                    CancelInvoke("InvokeTime");
                    _ins.NotifyOpen("<color=#d3d3d3>МЕГАЯЩИК!</color>", "<color=#8ebd2e>Мегаящик</color> был открыт, успей залутать его первым");
                    GenerateItem();
                    BoxToClose = 600;
                    entity.SetFlag(BaseEntity.Flags.Locked, false);
                    entity.SendChildrenNetworkUpdateImmediate();
                    Invoke("InvokeClose", 1f);   
                }
            }

            public void Kill()
            {
                _ins.GenerateTime();
                DestroyImmediate(this);
            }
            
            
            void DestroyMarker()
            {
                if (mapMarker != null && !mapMarker.IsDestroyed) mapMarker.Kill();
            }
            
            private Color ConvertToColor(string color)
            {
                if (color.StartsWith("#")) color = color.Substring(1);
                int red = int.Parse(color.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                int green = int.Parse(color.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                int blue = int.Parse(color.Substring(4, 2), NumberStyles.AllowHexSpecifier);
                return new Color((float)red / 255, (float)green / 255, (float)blue / 255);
            }

            public void UpdateMarker()
            {
                var position = entity.transform.position;
                mapMarker = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", position) as MapMarkerGenericRadius;
                mapMarker.enableSaving = false;
                mapMarker.Spawn();
                mapMarker.radius = 0.3f;
                mapMarker.alpha = 1f;
                mapMarker.color1 = ConvertToColor("#10c916");
                mapMarker.SendUpdate();
                mapMarker.SendNetworkUpdate();
            }



            public void GenerateItem()
            {
                var container = entity.GetComponent<LootContainer>();
                container.inventory.itemList.Clear();
                foreach (var key in info._listItem)
                {
                    var item = key.ToGive();
                    item.MoveToContainer(container.inventory, -1);
                }

                container.inventory.capacity = container.inventory.itemList.Count;
            }
            

            #endregion


            #region Destoy

            private void OnDestroy()
            {
                CancelInvoke();
                Time = 0;
                BoxToClose = 0;
                DestroyMarker();
                if (entity != null)
                    entity.Kill();
            }

            #endregion
            
            
        }

        #endregion


        #region ChatCommand


        [ChatCommand("mg")]
        void OpenTop(BasePlayer player, string command, string[] args)
        {
            string topPoint = "Топ 5 игроков по лутанию Мега-Ящика:\n";
            int iss = 1;
            foreach (var value in _openCrate.OrderByDescending(p => p.Value).Take(5))
            {
                string name = null;
                if (covalence.Players.FindPlayer(value.Key.ToString()) != null)
                {
                    name = covalence.Players.FindPlayer(value.Key.ToString()).Name;
                }
                if (name == null) name = "Неизвестный";
                int amounts = value.Value;
                topPoint = topPoint + $"{iss.ToString()}" + ". " + $"{name} : " + $"{amounts.ToString()}" + "\n";
                iss++;
                if (iss > 5) break;
            }
            player.ChatMessage(topPoint);
        }


        [ChatCommand("megacrate")]
        void CrateController(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;
            if (args.Length == 0)
            {
                player.ChatMessage("Помощь по командам:\n/megacrate start - Запустить ивент\n/megacrate stop - Остановить ивент\n/megacrate unlock - Разблокировать ящик");
                return;
            }

            switch (args[0])
            {
                case "start":
                {
                    if (GenerateBox())
                        player.ChatMessage("Вы успешно начали ивент Мега-Ящик");
                    else player.ChatMessage("Ивент не был запущен, ошибка в консоли");
                    break;
                }
                case "stop":
                {
                    var find = UnityEngine.Object.FindObjectOfType<BoxComponent>();
                    if (find != null)
                    {
                        find.Kill();
                        player.ChatMessage("Вы успешно остановили ивент Мега-Ящик");
                        return;
                    }
                    player.ChatMessage("Мега-Ящик не был найден!");
                    break;
                }
                case "unlock":
                {
                    var find = UnityEngine.Object.FindObjectOfType<BoxComponent>();
                    if (find != null)
                    {
                        find.Time = 0;
                        find.UnlockCrate();
                        player.ChatMessage("Вы успешно разблокировали Мега-Ящик");
                    } else player.ChatMessage("Мега-Ящик не был найден!");
                    break;
                }
                    
            }
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

            public class ItemGive
            {
                [JsonProperty("ShortName")] 
                public string ShortName;

                [JsonProperty("Amount")] 
                public int Amount;

                [JsonProperty("SkinID")]
                public ulong SkinID;

                [JsonProperty("DisplayName")] 
                public string DisplayName;


                public Item ToGive()
                {
                    var item = ItemManager.CreateByName(ShortName, Amount, SkinID);
                    if (!string.IsNullOrEmpty(DisplayName))
                        item.name = DisplayName;

                    return item;
                }
            }


            public class MonumentSpawn
            {
                [JsonProperty("Prefab Monument ( узнать префаб можно : data/Mbox/MonumentName")]
                public string MonumentName;

                [JsonProperty("Название РТ")] 
                public string NameMonument;

                [JsonProperty("Позиция относительно ширины и длины РТ")]
                public string Position;

                [JsonProperty("Лут, который может выпасть на данной рт")]
                public List<ItemGive> _listItem = new List<ItemGive>();
                
            }
            
            

            private class PluginConfig
            {
                [JsonProperty("Минимальное количество игроков для запуска ивента" )]
                public int minPlayedPlayers;
                
                [JsonProperty("Время до начала ивента (Минимальное в секундах)")]
                public int minSpawnIvent;
                
                [JsonProperty("Время до начала ивента (Максимальное в секундах)")]
                public int maxSpawnIvent;

                [JsonProperty("Время открытия ящика ( в секундах )")]
                public int BoxToOpen;

                [JsonProperty("Картинка ящика в оповещениях")]
                public string ImageBox;

                [JsonProperty("Настройка спавна на различных РТ")]
                public List<MonumentSpawn> _MonumentSpawns = new List<MonumentSpawn>();

                [JsonProperty("Версия конфигурации")] 
                public VersionNumber PluginVersion = new VersionNumber();

                public static PluginConfig DefaultConfig()
                {
                    return new PluginConfig()
                    {
                        minPlayedPlayers = 1,
                        minSpawnIvent = 3600,
                        maxSpawnIvent = 7200,
                        BoxToOpen = 600,
                        _MonumentSpawns = new List<MonumentSpawn>()
                        {
                            new MonumentSpawn()
                            {
                                MonumentName = "assets/bundled/prefabs/autospawn/monument/large/airfield_1.prefab", 
                                NameMonument = "Аэродром",
                                Position = "(0, 0, 0)",
                                _listItem = new List<ItemGive>()
                                {
                                    new ItemGive()
                                    {
                                        ShortName = "rifle.ak",
                                        Amount = 1,
                                        SkinID = 0,
                                        DisplayName = "Какой-то автомат",
                                    },
                                    new ItemGive()
                                    {
                                        ShortName = "battery.small",
                                        Amount = 100,
                                        SkinID = 0,
                                        DisplayName = "",
                                    },
                                    new ItemGive()
                                    {
                                        ShortName = "metal.fragments",
                                        Amount = 3500,
                                        SkinID = 0,
                                        DisplayName = ""
                                    }
                                }
                            },
                            new MonumentSpawn()
                            {
                                MonumentName = "assets/bundled/prefabs/autospawn/monument/small/satellite_dish.prefab", 
                                NameMonument = "Хуй знает че",
                                Position = "(0, 0, 0)",
                                _listItem = new List<ItemGive>()
                                {
                                    new ItemGive()
                                    {
                                        ShortName = "rifle.ak",
                                        Amount = 1,
                                        SkinID = 0,
                                        DisplayName = "Какой-то автомат",
                                    },
                                    new ItemGive()
                                    {
                                        ShortName = "battery.small",
                                        Amount = 100,
                                        SkinID = 0,
                                        DisplayName = "",
                                    },
                                    new ItemGive()
                                    {
                                        ShortName = "metal.fragments",
                                        Amount = 3500,
                                        SkinID = 0,
                                        DisplayName = ""
                                    }
                                }
                            }
                        },
                        PluginVersion = new VersionNumber(),
                        

                    };
                }
            }

            #endregion
    }
}