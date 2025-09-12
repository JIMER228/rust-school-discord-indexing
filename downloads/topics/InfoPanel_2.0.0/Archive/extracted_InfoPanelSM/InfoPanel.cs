using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("InfoPanel", "Sempai", "1.0.0")]
    public class InfoPanel : RustPlugin
    {
        #region хуйня
        private bool IsAir, IsHeli, IsBreadly, IsCargo, IsCh;

        string colorActive = config.MainSetting.colorActive;
        string colorDeactive = config.MainSetting.colorDeactive;
        void EventInit(BasePlayer player, string type)
        {
            var container = new CuiElementContainer();
            switch(type)
            {
                case "plane":
                    {
                        CuiHelper.DestroyUi(player, "plane");
                        if(IsAir)
                        {
                            container.Add(new CuiElement
                            {
                                Name = "plane",
                                Parent = "panel",
                                Components =
                                {
                                    new CuiRawImageComponent
                                    {
                                        Color = colorActive,
                                        Png = GetImage("plane"),
                                        FadeIn = 1f
                                    },

                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.72 0.225",
                                        AnchorMax = "0.8065 0.45",
                                    }
                                }
                            });
                        }
                        else
                        {
                            container.Add(new CuiElement
                            {
                                Name = "plane",
                                Parent = "panel",
                                Components =
                                {
                                    new CuiRawImageComponent
                                    {
                                        Color = colorDeactive,
                                        Png = GetImage("plane"),
                                    },

                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.72 0.225",
                                        AnchorMax = "0.8065 0.45",
                                    }
                                }
                            });
                        }
                        CuiHelper.AddUi(player, container);
                        break;
                    }
                case "heli":
                    {
                        CuiHelper.DestroyUi(player, "heli");
                        if(IsHeli)
                        {
                            container.Add(new CuiElement
                            {
                                Name = "heli",
                                Parent = "panel",
                                Components =
                                {
                                    new CuiRawImageComponent
                                    {
                                        Color = colorActive,
                                        Png = GetImage("heli"),
                                        FadeIn = 1f
                                    },

                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.5 0.225",
                                        AnchorMax = "0.5865 0.45",
                                    }
                                }
                            });
                        }
                        else
                        {
                            container.Add(new CuiElement
                            {
                                Name = "heli",
                                Parent = "panel",
                                Components =
                                {
                                    new CuiRawImageComponent
                                    {
                                        Color = colorDeactive,
                                        Png = GetImage("heli"),
                                    },

                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.5 0.225",
                                        AnchorMax = "0.5865 0.45",
                                    }
                                }
                            });
                        }
                        CuiHelper.AddUi(player, container);
                        break;   
                    }
                case "cargo":
                    {
                        CuiHelper.DestroyUi(player, "cargo");
                        if(IsCargo)
                        {
                            container.Add(new CuiElement
                            {
                                Name = "cargo",
                                Parent = "panel",
                                Components =
                                {
                                    new CuiRawImageComponent
                                    {
                                        Color = colorActive,
                                        Png = GetImage("cargo"),
                                        FadeIn = 1f
                                    },

                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.39 0.225",
                                        AnchorMax = "0.4765 0.45",
                                    }
                                }
                            });
                        }
                        else
                        {
                            container.Add(new CuiElement
                            {
                                Name = "cargo",
                                Parent = "panel",
                                Components =
                                {
                                    new CuiRawImageComponent
                                    {
                                        Color = colorDeactive,
                                        Png = GetImage("cargo"),
                                    },

                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.39 0.225",
                                        AnchorMax = "0.4765 0.45",
                                    }
                                }
                            });
                        }
                        CuiHelper.AddUi(player, container);
                        break;   
                    }
                case "chelnok":
                    {
                        CuiHelper.DestroyUi(player, "chelnok");
                        if(IsCh)
                        {
                            container.Add(new CuiElement
                            {
                                Name = "chelnok",
                                Parent = "panel",
                                Components =
                                {
                                    new CuiRawImageComponent
                                    {
                                        Color = colorActive,
                                        Png = GetImage("chelnok"),
                                        FadeIn = 1f
                                    },

                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.61 0.225",
                                        AnchorMax = "0.6965 0.45",
                                    }
                                }
                            });
                        }
                        else
                        {
                            container.Add(new CuiElement
                            {
                                Name = "chelnok",
                                Parent = "panel",
                                Components =
                                {
                                    new CuiRawImageComponent
                                    {
                                        Color = colorDeactive,
                                        Png = GetImage("chelnok"),
                                    },

                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.61 0.225",
                                        AnchorMax = "0.6965 0.45",
                                    }
                                }
                            });
                        }
                        CuiHelper.AddUi(player, container);
                        break;   
                    }
                case "tank":
                    {
                        CuiHelper.DestroyUi(player, "tank");
                        if(IsBreadly)
                        {
                            container.Add(new CuiElement
                            {
                                Name = "tank",
                                Parent = "panel",
                                Components =
                                {
                                    new CuiRawImageComponent
                                    {                    
                                        Color = colorActive,
                                        Png = GetImage("tank"),
                                        FadeIn = 1f
                                    },

                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.835 0.225",
                                        AnchorMax = "0.9215 0.45",
                                    }
                                }
                            });
                        }
                        else
                        {
                            container.Add(new CuiElement
                            {
                                Name = "tank",
                                Parent = "panel",
                                Components =
                                {
                                    new CuiRawImageComponent
                                    {                    
                                        Color = colorDeactive,
                                        Png = GetImage("tank"),
                                    },

                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.835 0.225",
                                        AnchorMax = "0.9215 0.45",
                                    }
                                }
                            });
                        }
                        CuiHelper.AddUi(player, container);
                        break;   
                    }
            }
        }
        void OnEntityKill(BaseNetworkable entity)
        {
            if (entity is CargoPlane)
            {
                if(!IsAir) return;
                IsAir = false;
                foreach (var basePlayer in BasePlayer.activePlayerList)
                    EventInit(basePlayer, "plane");
            }
            else if (entity is CargoShip)
            {
                if(!IsCargo) return;
                IsCargo = false;
                foreach (var basePlayer in BasePlayer.activePlayerList)
                    EventInit(basePlayer, "cargo");
            }
            else if (entity is BaseHelicopter)
            {
                if(!IsHeli) return;
                IsHeli = false;
                foreach (var basePlayer in BasePlayer.activePlayerList)
                    EventInit(basePlayer, "heli");
            }
            else if (entity is BradleyAPC)
            {
                if(!IsBreadly) return;
                IsBreadly = false;
                foreach (var basePlayer in BasePlayer.activePlayerList)
                    EventInit(basePlayer, "tank");
            }
            else if (entity is CH47Helicopter)
            {   
                if(!IsCh) return;
                IsCh = false;
                foreach (var basePlayer in BasePlayer.activePlayerList)
                    EventInit(basePlayer, "chelnok");
            }
        } 
        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity is BaseHelicopter)
            {
                if(IsHeli) return;
                IsHeli = true;
                foreach (var basePlayer in BasePlayer.activePlayerList)
                    EventInit(basePlayer, "heli");
            }
            if (entity is BradleyAPC)
            {
                if(IsBreadly) return;
                IsBreadly = true;
                foreach (var basePlayer in BasePlayer.activePlayerList)
                    EventInit(basePlayer, "tank");
            }
            if (entity is CargoPlane)          
            {
                if(IsAir) return;
                IsAir = true;
                foreach (var basePlayer in BasePlayer.activePlayerList)
                    EventInit(basePlayer, "plane");
            }
            if (entity is CargoShip)
            {
                if(IsCargo) return;
                IsCargo = true;
                foreach (var basePlayer in BasePlayer.activePlayerList)
                    EventInit(basePlayer, "cargo");
            }
            if (entity is CH47Helicopter)
            { 
                if(IsCh) return;
                IsCh = true;
                foreach (var basePlayer in BasePlayer.activePlayerList)
                    EventInit(basePlayer, "chelnok");
            }
        }
        private void Check(BasePlayer player)
        {
            foreach (var entity in BaseNetworkable.serverEntities.Where(p => p is CargoPlane || p is BradleyAPC || p is BaseHelicopter || p is BaseHelicopter || p is CargoShip ||p is CH47Helicopter))
            {
                if (entity is CargoPlane)
                {
                    IsAir = true;
                    EventInit(player, "plane");
                }    
                if (entity is BradleyAPC)
                {
                    IsBreadly = true;
                    EventInit(player, "tank");
                }
                if (entity is BaseHelicopter)
                {
                    IsHeli = true;
                    EventInit(player, "heli");
                }
                if (entity is CargoShip)
                {
                    IsCargo = true;
                    EventInit(player, "cargo");
                }
                if (entity is CH47Helicopter)
                {
                    IsCh = true;
                    EventInit(player, "chelnok");
                }
            }
        }
        #endregion

        [PluginReference] Plugin ImageLibrary;

        private string GetImage(string fileName, ulong skin = 0)
        {
            var imageId = (string)plugins.Find("ImageLibrary").CallHook("GetImage", fileName, skin);
            if (!string.IsNullOrEmpty(imageId))
                return imageId;
            return string.Empty;
        }
        public bool AddImage(string url, string shortname, ulong skin = 0) => (bool)ImageLibrary?.Call("AddImage", url, shortname, skin);
        void Unload()
        {
            foreach(BasePlayer player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, "panel");
            }
        }
        void OnServerInitialized()
        {
            PrintWarning("\n-----------------------------\n " +" Author - Sempai#3239\n " +" VK - https://vk.com/rustnastroika\n " +" Forum - https://topplugin.ru\n " +" Discord - https://discord.gg/5DPTsRmd3G\n" +"-----------------------------");
            foreach(BasePlayer player in BasePlayer.activePlayerList)
            {
                OnPlayerConnected(player);
            }

            AddImage("https://i.ibb.co/K66v23d/unknown-2.png", "ico");
            AddImage("https://i.ibb.co/28t4XF0/MENU-3.png", "panel");

            AddImage($"https://i.ibb.co/WVf9vRk/mUSE7.png", $"heli");
            AddImage($"https://i.ibb.co/RyxsYyd/mUaUn.png", $"chelnok");
            AddImage($"https://i.ibb.co/2nLsxBx/mUR8K.png", $"plane");
            AddImage($"https://i.ibb.co/TTf479q/mUOGE.png", $"cargo");
            AddImage($"https://i.ibb.co/6ByLvVW/UCU8o.png", $"tank");
        }

        void OnPlayerConnected(BasePlayer player)
        {   
            UI_Panel(player);
            UI_Online(player);
            timer.Every(60f, () =>
            {
                UI_Online(player);
            });
        }

        [ChatCommand("test")]
        void test(BasePlayer player)
        {

        }


        void UI_Online(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();
            container.Add(new CuiElement
            {
                Name = "Online",
                Parent = "panel",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = $"{BasePlayer.activePlayerList.Count}",
                        FontSize = 10,
                        Align = TextAnchor.MiddleCenter,
                    },

                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.14 0.3",
                        AnchorMax = "1 1",
                    },
                }
            });
            
            container.Add(new CuiElement
            {
                Name = "Offline",
                Parent = "panel",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = $"{BasePlayer.sleepingPlayerList.Count}",
                        FontSize = 10,
                        Align = TextAnchor.MiddleCenter,
                    },

                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.67 0.3",
                        AnchorMax = "1 1",
                    },
                }
            });

            CuiHelper.DestroyUi(player, "Online");
            CuiHelper.DestroyUi(player, "Offline");
            CuiHelper.AddUi(player, container);
        }
        public static string MENU_PARENT = "MENU_PARENT_LAYER";

        void UI_Panel(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "0 -60", OffsetMax = "300 0" },
                Image = { Color = "0 0 0 0" }
            }, "Overlay", "Main");
            container.Add(new CuiElement
            {
                Name = "panel",
                Parent = "Main",
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Color = "1 1 1 1",
                        Png = GetImage("panel"),
                    },

                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.01999996 -0.1777785",
                        AnchorMax = "0.5766667 0.8333326",
                    }
                }
            });
            
            container.Add(new CuiButton
            {
                Button =
                {
                    Command = config.MainSetting.btncommand,
                    Color = "1 1 1 0",
                },

                RectTransform =
                {
                    AnchorMin = "0.05 -0.1",
                    AnchorMax = "0.2 0.73",
                }
            }, "Main", "btn");

            container.Add(new CuiElement
            {
                Name = "Cargo",
                Parent = "panel",
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Color = colorDeactive,
                        Png = GetImage("cargo"),
                    },

                    new CuiRectTransformComponent
                    {
                     
                        AnchorMin = "0.39 0.225",
                        AnchorMax = "0.4765 0.45",
                    }
                }
            });

            container.Add(new CuiElement
            {
                Name = "Heli",
                Parent = "panel",
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Color = colorDeactive,
                        Png = GetImage("heli"),
                    },

                    new CuiRectTransformComponent
                    {
                     
                        AnchorMin = "0.5 0.225",
                        AnchorMax = "0.5865 0.45",
                    }
                }
            });

            container.Add(new CuiElement
            {
                Name = "chelnok",
                Parent = "panel",
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Color = colorDeactive,
                        Png = GetImage("chelnok"),
                    },

                    new CuiRectTransformComponent
                    {
                     
                        AnchorMin = "0.61 0.225",
                        AnchorMax = "0.6965 0.45",
                    }
                }
            });

            container.Add(new CuiElement
            {
                Name = "plane",
                Parent = "panel",
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Color = colorDeactive,
                        Png = GetImage("plane"),
                    },

                    new CuiRectTransformComponent
                    {
                     
                        AnchorMin = "0.72 0.225",
                        AnchorMax = "0.8065 0.45",
                    }
                }
            });

            container.Add(new CuiElement
            {
                Name = "tank",
                Parent = "panel",
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Color = colorDeactive,
                        Png = GetImage("tank"),
                    },

                    new CuiRectTransformComponent
                    {
                     
                        AnchorMin = "0.835 0.225",
                        AnchorMax = "0.9215 0.45",
                    }
                }
            });
            CuiHelper.DestroyUi(player, "Main");
            CuiHelper.AddUi(player, container);

            Check(player);
        }

        #region Configuration 
        private static ConfigData config = new ConfigData();
        private class ConfigData
        {
            [JsonProperty("Основная настройка")]
            public MainSettings MainSetting = new MainSettings();

            internal class MainSettings
            {
                [JsonProperty("Команда на кнопку")]
                public string btncommand = "chat.say /menu";
                [JsonProperty("Цвет активного ивента")]
                public string colorActive = "1 1 1 1";
                [JsonProperty("Цвет не активного ивента")]
                public string colorDeactive = "0.29 0.29 0.29 1";
            }
            public static ConfigData GetNewConfiguration() 
            {
                return new ConfigData
                {
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<ConfigData>();
                if (config == null) LoadDefaultConfig();
            }
            catch
            {
                PrintWarning($"Ошибка чтения #57 конфигурации 'oxide/config/{Name}', создаём новую конфигурацию!!");
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }

        protected override void LoadDefaultConfig() => config = ConfigData.GetNewConfiguration();
        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion
    }   
}