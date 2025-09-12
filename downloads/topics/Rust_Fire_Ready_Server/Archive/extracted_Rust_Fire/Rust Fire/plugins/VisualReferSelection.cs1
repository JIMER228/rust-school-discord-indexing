using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Visual Refer Selection", "Orange", "1.0.0")]
    [Description("https://rustworkshop.space/")]
    public class VisualReferSelection : RustPlugin
    {
        #region Vars

        private const string elemMain = "VisualReferSelection.Main";
        private CuiElementContainer container = new CuiElementContainer();

        #endregion
        
        #region Oxide Hooks

        private void Init()
        {
            cmd.AddConsoleCommand("refer.select", this, nameof(cmdControlConsole));
        }

        private void OnServerInitialized()
        {
            foreach (var value in config.refers)
            {
                AddImage(value.url, value.url);
            }
            
            timer.Once(5f, () =>
            {
                BuildUI();
                
                foreach (var player in BasePlayer.activePlayerList)
                {
                    OnPlayerInit(player);
                }
            });
        }
        
        private void OnPlayerInit(BasePlayer player)
        {
            if (player.IsConnected == false)
            {
                return;
            }
            
            if (player.IsReceivingSnapshot == true || player.IsSleeping())
            {
                timer.Once(3f, () => { OnPlayerInit(player); });
                return;
            }
            
            if (Interface.CallHook("CanBeReferred", player) != null)
            {
                return;
            }
            
            CreateUI(player);
        }

        #endregion

        #region Commands

        private void cmdControlConsole(ConsoleSystem.Arg arg)
        {
            if (arg.Args == null || arg.Args?.Length == 0)
            {
                return;
            }

            Interface.CallHook("SetPlayerRefer", arg.Player(), arg.Args[0]);
        }

        #endregion

        #region Core

        private void BuildUI()
        {
            container.Add(new CuiElement
            {
                Name = elemMain,
                Parent = "Hud.Menu",
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = config.backgroundColor,
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    },
                    new CuiNeedsCursorComponent()
                }
            });

            container.Add(new CuiElement
            {
                Parent = elemMain,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = config.textHeader,
                        Color = "1 1 1 1",
                        Align = TextAnchor.UpperCenter
                    },
                    new CuiOutlineComponent
                    {
                        Color = "0 0 0 1",
                        Distance = config.outlineDistance
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "0.95 0.95"
                    }
                }
            });

            var i = 0;
            var startX = 100;
            var startY = -150;
            var offsetX = 5;
            var offsetY = 5;
            var size = 150;
            var x = startX;
            var y = startY;

            foreach (var value in config.refers)
            {
                var name = elemMain + "." + value.shortname;
                container.Add(new CuiElement
                {
                    Name = name,
                    Parent = elemMain,
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Color = "1 1 1 1",
                            Png = GetImage(value.url)
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1",
                            AnchorMax = "0 1",
                            OffsetMin = $"{x} {y - size}",
                            OffsetMax = $"{x + size} {y}"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = name,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = value.displayName,
                            Align = TextAnchor.LowerCenter,
                            FontSize = config.sizeDisplayName,
                            Color = "1 1 1 1"
                        },
                        new CuiOutlineComponent
                        {
                            Color = "0 0 0 1",
                            Distance = config.outlineDistance
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.05 0.05",
                            AnchorMax = "0.95 0.95"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = name,
                    Components =
                    {
                        new CuiButtonComponent
                        {
                            Command = "refer.select " + value.shortname,
                            Close = elemMain,
                            Color = "1 1 1 0"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1"
                        }
                    }
                });

                x += size + offsetX;

                if (++i % 7 == 0)
                {
                    x = startX;
                    y -= size + offsetY;
                }
            }
        }

        private void CreateUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, elemMain);
            CuiHelper.AddUi(player, container);
        }

        #endregion
        
        #region Configuration

        private static ConfigData config = GetDefaultConfig();

        private class ConfigData
        {
            public string backgroundColor = "0.2 0.2 0.2 0.8";

            public string textHeader = "\n\n\n\n<color=#dedede><size=25>Как вы узнали о нас? Выберите нужный вариант,чтобы продолжить игру!\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n</size></color>";

            public int sizeDisplayName = 15;

            public string outlineDistance = "1.0 -1.0";
            
            [JsonProperty(PropertyName = "Refers list")]
            public List<ReferEntry> refers = new List<ReferEntry>();
        }

        private class ReferEntry
        {
            [JsonProperty(PropertyName = "Shortname")]
            public string shortname;
            
            [JsonProperty(PropertyName = "Display name")]
            public string displayName;
            
            [JsonProperty(PropertyName = "Url")]
            public string url;
        }

        private static ConfigData GetDefaultConfig()
        {
            return new ConfigData
            {
                refers = new List<ReferEntry>
                {
					new ReferEntry
                    {
                        shortname = "alkad",
                        displayName = "В Alkad",
                        url = "https://i.imgur.com/9mIuJQV.png"
                    },
                    new ReferEntry
                    {
                        shortname = "friend",
                        displayName = "Пригласил друг",
                        url = "https://i.imgur.com/ccUQFfo.png"
                    },
                    new ReferEntry
                    {
                        shortname = "search",
                        displayName = "В поиске",
                        url = "https://i.imgur.com/iqDfXLg.png",
                    },
					new ReferEntry
                    {
                        shortname = "warkey",
                        displayName = "Warkey",
                        url = "https://i.imgur.com/PnuWrN5.png",
                    },
                    new ReferEntry
                    {
                        shortname  = "youtube",
                        displayName = "YouTube",
                        url = "https://i.imgur.com/ERcKhxs.png"
                    },
					new ReferEntry
					{
                        shortname  = "retine",
                        displayName = "Retine",
                        url = "https://i.imgur.com/0COQ1AG.png"
                    },
                    new ReferEntry
                    {
                        shortname = "other",
                        displayName = "Другое",
                        url = "https://i.imgur.com/60SaMuU.png"
                    },
                }
            };
        }

        #endregion
        
        #region Image Library Support

        [PluginReference] private Plugin ImageLibrary;

        private void AddImage(string name, string url)
        { 
            if (ImageLibrary == null || ImageLibrary?.IsLoaded == false)
            {
                PrintWarning("Image Library is not loaded or not exist!");
                timer.Once(5f, () => { AddImage(name, url); });
                return;
            }

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(url))
            {
                return;
            }

            ImageLibrary.CallHook("AddImage", url, name, 0UL);
        }

        private string GetImage(string name)
        {
            return ImageLibrary?.Call<string>("GetImage", name);
        }

        #endregion
    }
}