using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static TowConfig;

namespace Oxide.Plugins
{
    [Info("FreeneticPyromid", "MaltrzD", "0.0.1")]
    public class FreeneticPyromid : RustPlugin
    {
        private RuntimeDoorData cachedDoorData;
        private ConfigData _config;
        private Timer waitOnlineTimer;

        [PluginReference] private Plugin Notify;
        private Timer addRadiationTimerRef;

        public class RuntimeDoorData
        {
            public SlidingProgressDoor SlidingDoor;
            public Vector3 Center = new Vector3();

            public void ChangeDoorState(bool state)
            {
                if (state)
                {
                    SlidingDoor.AddEnergy(1000);
                    SlidingDoor.secondsToClose = 1000000;
                }
                else
                {
                    SlidingDoor.secondsToClose = 3;
                    SlidingDoor.currentEnergy = 0;
                }

                SlidingDoor.SendNetworkUpdateImmediate();
            }
        }

        private void Loaded()
        {
            ReadConfig();
        }
        private void Unload()
        {
            DestroyAllUI();
            cachedDoorData?.ChangeDoorState(false);
        }
        private void OnServerInitialized()
        {
            LoadImages();
            PrintCCTVs();

            var camera = FindCCTVCamera(_config.CameraId);
            if(camera == null)
            {
                PrintError("Не найдена камера с припиской!");
                return;
            }

            SlidingProgressDoor door = null;
            var doors = BaseEntity.serverEntities.OfType<SlidingProgressDoor>();
            foreach (var d in doors)
            {
                if(Vector3.Distance(camera.transform.position, d.transform.position) < 50)
                {
                    door = d;
                    break;
                }
            }

            if(door == null)
            {
                PrintError("Не удалось найти дверь в радиусе 50 метров от центра!");
                return;
            }

            Puts($"{door.energyForOpen}/{door.secondsToClose}/{door.storedEnergy}");

            cachedDoorData = new RuntimeDoorData()
            {
                SlidingDoor = door,
                Center = camera.transform.position
            };
            cachedDoorData.ChangeDoorState(false);

            Puts($"Первая проверка на онлайн через: {_config.FirstCheckTime}");
            timer.Once(_config.FirstCheckTime, CheckFirstTime);
        }
        private void OnPlayerConnected(BasePlayer player)
        {
            if(countDownTime > 0)
                DrawUIFull(player);
        }

        private void CheckFirstTime()
        {
            if(BasePlayer.activePlayerList.Count >= _config.MinFirstTimeOnline)
            {
                Puts("Онлайн набран, Запуск ивента!");
                StartEvent();
            }
            else
            {
                waitOnlineTimer = timer.Every(_config.CheckOnlineFirstTime, () =>
                {
                    if(BasePlayer.activePlayerList.Count >= _config.MinFirstTimeOnline)
                    {
                        waitOnlineTimer.Destroy();
                        waitOnlineTimer = null;

                        StartEvent();
                    }
                });
            }
        }

        private void StartEvent()
        {
            Puts("Ивент начался, дверь открыта!");

            foreach (var item in BasePlayer.activePlayerList)
                Notify?.CallHook("SendNotify", item.userID.Get(), 0, "Ивент Пирамида начался двери открыты!");

            cachedDoorData.ChangeDoorState(true);

            StartCountDownTimer();

            timer.Once(_config.EventDuration, OnEventEnd);

            if(addRadiationTimerRef != null)
            {
                addRadiationTimerRef.Destroy();
                addRadiationTimerRef = null;
            }
        }
        private void OnEventEnd()
        {
            Puts("Ивент закончен, дверь закрыта!");

            cachedDoorData.ChangeDoorState(false);

            DestroyAllUI();
            
            timer.Once(_config.DefaultStartEventTime, () => StartEvent());

            foreach (var item in BasePlayer.activePlayerList)
                Notify?.CallHook("SendNotify", item.userID.Get(), 0, $"Ивент Пирамида закончился двери закрыты!\nПокиньте Пирамиду у вас есть {_config.RadiationStartTime} секунд!");

            timer.Once(_config.RadiationStartTime, StartRadiation);
        }
        
        private void StartRadiation()
        {
            AddRadiation();
            addRadiationTimerRef = timer.Every(5, AddRadiation);
        }
        private void AddRadiation()
        {
            foreach (var p in BasePlayer.activePlayerList.Where(x => Vector3.Distance(cachedDoorData.Center, x.transform.position) < _config.Radius))
                p.ApplyRadiation(_config.RadiationLevel, false);
        }

        private object CanTeleport(BasePlayer player)
        {
            if(cachedDoorData != null && countDownTime > 0)
            {
                if(Vector3.Distance(player.transform.position, cachedDoorData.Center) < _config.Radius)
                {
                    Notify?.CallHook("SendNotify", player.userID.Get(), 0, "Вы не можете телепортироватся в месте ивента!");
                    return "";
                }
            }

            return null;
        }
        private object OnTeleportRequested(BasePlayer player, BasePlayer requester)
        {
            if (cachedDoorData != null && countDownTime > 0)
            {
                if (
                    Vector3.Distance(player.transform.position, cachedDoorData.Center) < _config.Radius 
                    || Vector3.Distance(requester.transform.position, cachedDoorData.Center) < _config.Radius)
                {
                    Notify?.CallHook("SendNotify", player.userID.Get(), 0, "Телепорт запрещен в месте ивента или из него!");

                    return "";
                }
            }

            return true;
        }
        private void PrintCCTVs()
        {
            Puts(BaseEntity.serverEntities.OfType<CCTV_RC>().Select(x => x.rcIdentifier).ToSentence());
        }
        private CCTV_RC FindCCTVCamera(string sub)
            => BaseEntity.serverEntities.OfType<CCTV_RC>().FirstOrDefault(x => x.rcIdentifier.ToLower().StartsWith(sub.ToLower()));

        #region [ UI ]
        private int countDownTime = 0;
        private Dictionary<BasePlayer, bool> changedViews = new Dictionary<BasePlayer, bool>(); //true == full

        private void StartCountDownTimer()
        {
            changedViews = new Dictionary<BasePlayer, bool>();

            foreach (var p in BasePlayer.activePlayerList) DrawUIFull(p);

            countDownTime = Convert.ToInt32(_config.EventDuration);
            CountDownTimer(countDownTime);
        }
        private void CountDownTimer(int time)
        {
            countDownTime = time;

            List<BasePlayer> full = new List<BasePlayer>();
            List<BasePlayer> small = new List<BasePlayer>();

            foreach (var p in BasePlayer.activePlayerList)
            {
                if (changedViews.ContainsKey(p))
                {
                    if(changedViews[p]) full.Add(p);
                    else small.Add(p);
                }
                else full.Add(p);
            }

            SendUpdateFull(full, time);
            SendUpdateSmall(small, time);

            if (time <= 0)
            {
                DestroyAllUI();
                return;
            }

            timer.Once(1, () => CountDownTimer(time - 1));
        }

        private void DrawUIFull(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "BG");

            var container = new CuiElementContainer();

            container.Add(new CuiElement
            {
                Parent = $"Overlay",
                Name = $"BG",

                Components =
                {
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "1 0.5",
                        AnchorMax = "1 0.5",
                        OffsetMin = "-174.5 -25.5",
                        OffsetMax = "0 25.5"
                    },
                    new CuiRawImageComponent()
                    {
                        Png = GetImage("BG"),
                        Color = "1 1 1 1",
                    },
                }
            });

            container.Add(new CuiElement
            {
                Parent = $"BG",
                Name = $"Filler",

                Components =
                {
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "1 0",
                        AnchorMax = "1 0",
                        OffsetMin = "-111.3084 5.9",
                        OffsetMax = "-7.631599 8.5"
                    },
                    new CuiRawImageComponent()
                    {
                        Png = GetImage("Filler"),
                        Color = "1 1 1 1",
                    },
                }
            });

            container.Add(new CuiElement
            {
                Parent = $"BG",
                Name = $"TimerText",

                Components =
                {
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "-20.74615 -10.1",
                        OffsetMax = "79.86815 3.9"
                    },
                    new CuiTextComponent()
                    {
                        Text = "Осталось 10:00",
                        FontSize = 8,
                        FadeIn = 0f,
                        Color = "1 1 1 1",
                        Align = TextAnchor.MiddleRight,
                    },
                }
            });

            container.Add(new CuiButton()
            {
                Button =
                {
                    Color = "1 1 1 0",
                    Command = "pyramid.change.view",
                },
                Text =
                {
                    Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 0",
                    FontSize = 14,
                },
                RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 1",
                    OffsetMin = "0 0",
                    OffsetMax = "0 0",
                },
            }, $"BG", "Btn");

            CuiHelper.AddUi(player, container);

            SendUpdateFull(new BasePlayer[] { player }, countDownTime);
        }
        private void DrawUISmall(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "BG");

            var container = new CuiElementContainer();

            container.Add(new CuiElement
            {
                Parent = $"Overlay",
                Name = $"BG",

                Components =
                {
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "1 0.5",
                        AnchorMax = "1 0.5",
                        OffsetMin = "-64 -22.5",
                        OffsetMax = "0 22.5"
                    },
                    new CuiRawImageComponent()
                    {
                        Png = GetImage("BGSmall"),
                        Color = "1 1 1 1",
                    },
                }
            });

            container.Add(new CuiElement
            {
                Parent = $"BG",
                Name = $"Filler",

                Components =
                {
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "-22.75 -20",
                        OffsetMax = "22.75 -17.5"
                    },
                    new CuiRawImageComponent()
                    {
                        Png = GetImage("FillerV2"),
                        Color = "1 1 1 1",
                    },
                }
            });

            container.Add(new CuiButton()
            {
                Button =
                {
                    Color = "1 1 1 0",
                    Command = "pyramid.change.view",
                },
                Text =
                {
                    Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 0",
                    FontSize = 14,
                },
                RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 1",
                    OffsetMin = "0 0",
                    OffsetMax = "0 0",
                },
            }, $"BG", "Btn");

            CuiHelper.AddUi(player, container);

            SendUpdateSmall(new BasePlayer[] { player }, countDownTime);
        }

        private void SendUpdateFull(IEnumerable<BasePlayer> players, int time)
        {
            int minutes = time / 60;
            int seconds = time % 60;

            float fillAmount = Mathf.Clamp01(time / _config.EventDuration);

            float offsetMinX = -115f;
            float fullWidth = 115f - 7.631599f;
            float currentWidth = fullWidth * fillAmount;
            float offsetMaxX = offsetMinX + currentWidth;

            var container = new CuiElementContainer();
            container.Add(new CuiElement
            {
                Parent = $"BG",
                Name = $"TimerText",
                Update = true,

                Components =
                {
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "-20.74615 -10.1",
                        OffsetMax = "79.86815 3.9"
                    },
                    new CuiTextComponent()
                    {
                        Text = $"Осталось {minutes}:{seconds:D2}",
                        FontSize = 8,
                        FadeIn = 0f,
                        Color = "1 1 1 1",
                        Align = TextAnchor.MiddleRight,
                    },
                }
            });
            container.Add(new CuiElement
            {
                Parent = $"BG",
                Name = $"Filler",
                Update = true,

                Components =
                {
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "1 0",
                        AnchorMax = "1 0",
                        OffsetMin = $"{offsetMinX} 5.9",
                        OffsetMax = $"{offsetMaxX} 8.5"
                    },
                    new CuiRawImageComponent()
                    {
                        Png = GetImage("Filler"),
                        Color = "1 1 1 1",
                    },
                }
            });

            foreach (var player in players)
                CuiHelper.AddUi(player, container);
        }
        private void SendUpdateSmall(IEnumerable<BasePlayer> players, int time)
        {
            CuiElementContainer container = new CuiElementContainer();

            float fillAmount = Mathf.Clamp01(time / _config.EventDuration);
            float maxX = Mathf.Lerp(-22.75f, 22.75f, fillAmount);

            container.Add(new CuiElement
            {
                Parent = $"BG",
                Name = $"Filler",
                Update = true,

                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = $"-22.75 -20",
                        OffsetMax = $"{maxX} -17.5"
                    },
                    new CuiRawImageComponent
                    {
                        Png = GetImage("FillerV2"),
                        Color = "1 1 1 1"
                    }
                }
            });

            foreach (var player in players)
            {
                CuiHelper.AddUi(player, container);
            }
        }


        private void DestroyAllUI()
        {
            foreach (var p in BasePlayer.activePlayerList) CuiHelper.DestroyUi(p, "BG");
        }

        [ConsoleCommand("pyramid.change.view")]
        private void ChangeView(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            if (changedViews.ContainsKey(player))
            {
                bool view = changedViews[player] = !changedViews[player];

                if (view) DrawUIFull(player);
                else DrawUISmall(player);
            }
            else
            {
                changedViews.Add(player, false);
                DrawUISmall(player);
            }
        }


        [PluginReference] private Plugin ImageLibrary;
        public string GetImage(string shortname, ulong skin = 0) => (string)ImageLibrary.Call("GetImage", shortname, skin);
        public bool AddImage(string url, string shortname, ulong skin = 0) => (bool)ImageLibrary?.Call("AddImage", url, shortname, skin);
        private void LoadImages()
        {
            AddImage("https://i.imgur.com/ctDdj0B.png", "BGSmall");
            AddImage("https://i.imgur.com/IBt1OqI.png", "BG");

            AddImage("https://i.imgur.com/D5PMyiM.png", "Filler");
            AddImage("https://i.imgur.com/AV27xvb.png", "FillerV2");
        }
        #endregion

        #region [ CONFIG ]
        class ConfigData
        {
            [JsonProperty("Радиус поиска двери и зоны запрещенного ТП")]
            public float Radius = 50;
            [JsonProperty("Rc Id камеры для поиска двери")]
            public string CameraId = "aboba";

            [JsonProperty("Через какое время будет первая проверка на онлайн")]
            public float FirstCheckTime = 7200;
            [JsonProperty("Мин. онлайн первой проверки")]
            public int MinFirstTimeOnline = 30;
            [JsonProperty("Проверка в секундах на онлайн, если первый онлайн не набран")]
            public float CheckOnlineFirstTime = 600;

            [JsonProperty("Интвервал между ивентами")]
            public float DefaultStartEventTime = 14400;
            [JsonProperty("Длительность ивента")]
            public float EventDuration = 900;

            [JsonProperty("Время между добавленим радиации")]
            public float RadiationInterval = 5;
            [JsonProperty("Начало радиации после ивента")]
            public float RadiationStartTime = 5;
            [JsonProperty("Колво радиации")]
            public float RadiationLevel = 25;
        }
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData();
            SaveConfig(config);
        }
        void SaveConfig(object config)
        {
            Config.WriteObject(config, true);
        }
        void ReadConfig()
        {
            base.Config.Settings.ObjectCreationHandling = ObjectCreationHandling.Replace;
            _config = Config.ReadObject<ConfigData>();
            SaveConfig(_config);
        }
        #endregion
    }
}