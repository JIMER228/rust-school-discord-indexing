using UnityEngine;
using Oxide.Game.Rust.Cui;
using Oxide.Core;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Logo", "ferq3ns", "1.0.0")]
    public class Logo : RustPlugin
    {

        private const string onlineImgUrl = "https://i.postimg.cc/Bn8hBrW6/online.png";

        private const string kitsImgUrl = "https://i.postimg.cc/PqKtpp9D/12-20250622213050.png";

        private const string storeImgUrl = "https://i.postimg.cc/mgmLh10H/13-20250622213400.png";

        private const string OnlineText = "onlinecount";

        [PluginReference]
        private Plugin ImageLibrary, NoEscape;

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, "storef");
                CuiHelper.DestroyUi(player, "kitsblur");
                CuiHelper.DestroyUi(player, "storeblur");
                CuiHelper.DestroyUi(player, "closeblur");
                CuiHelper.DestroyUi(player, "onlineblur");
                CuiHelper.DestroyUi(player, "kitsf");
                CuiHelper.DestroyUi(player, "closef");
                CuiHelper.DestroyUi(player, "onlinecounter");
                CuiHelper.DestroyUi(player, "online_icon");
                CuiHelper.DestroyUi(player, "onlinef");
                CuiHelper.DestroyUi(player, "fmain");
                CuiHelper.DestroyUi(player, "fmainicon");
                CuiHelper.DestroyUi(player, "onlineip");
            }
        } 

        private void OnServerInitialized()
        {
            if (plugins.Find("ImageLibrary") != null)
            {
                Interface.CallHook("AddImage", onlineImgUrl, "online", 0UL);
                Interface.CallHook("AddImage", kitsImgUrl, "kits", 0UL);
                Interface.CallHook("AddImage", storeImgUrl, "store", 0UL);
            }

            if (plugins.Find("ImageLibrary") == null)
            {
                PrintWarning("Плагин Image Library не найден! Установите Image Library - https://umod.org/plugins/image-library");
                return;
            }

            foreach (var player in BasePlayer.activePlayerList)
            {
                DrawUIF(player);
            }
        }

        void OnPlayerInit(BasePlayer player)
        {
            timer.Once(2f, () => DrawUIF(player));

            DrawUIF(player);
            RefreshOnline();
        }

        [ConsoleCommand("mclick")]
        private void OnMClick(ConsoleSystem.Arg arg)
        {
            var player = arg?.Connection?.player as BasePlayer;
            if (player == null) return;

            DrawUIM(player);
            DestroyUIF(player);
        }

        [ConsoleCommand("fclick")]
        private void OnFClick(ConsoleSystem.Arg arg)
        {
            var player = arg?.Connection?.player as BasePlayer;
            if (player == null) return;

            DrawUIF(player);
            DestroyUIM(player);
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            timer.Once(2f, () => DrawUIF(player));

            DrawUIF(player);

            if (player == null) return;

            DrawUIF(player);
            RefreshOnline();
        }

        private void DrawUIF(BasePlayer player)
        {
            DestroyUIF(player);

            var container = new CuiElementContainer();

            container.Add(new CuiButton
            {
                Button = {
                    Command = "chat.say /kit",
                    Color = "0 0 0 0.5",
                    Close = ""
                },
                RectTransform = {
                    AnchorMin = "0.004 0.957",
                    AnchorMax = "0.023 0.992"
                },
                Text = {
                    Text = "",
                    Color = "0 0 0 0"
                }
            }, "Hud", "kitsf");

            container.Add(new CuiElement
            {
                Name = "kitsicon",
                Parent = "kitsf",
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Color = "1 1 1 1",
                        Png = (string)ImageLibrary.Call("GetImage", "kits")
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.05 0.05",
                        AnchorMax = "0.899 0.899"
                    }
                }
            });

            container.Add(new CuiButton
            {
                Button = {
                    Command = "chat.say /store",
                    Color = "0 0 0 0.5",
                    Close = ""
                },
                RectTransform = {
                    AnchorMin = "0.026 0.957",
                    AnchorMax = "0.045 0.992"
                },
                Text = {
                    Text = "",
                    Color = "0 0 0 0"
                }
            }, "Hud", "storef");

            container.Add(new CuiElement
            {
                Name = "storeicon",
                Parent = "storef",
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Color = "1 1 1 1",
                        Png = (string)ImageLibrary.Call("GetImage", "store")
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.05 0.05",
                        AnchorMax = "0.899 0.899"
                    }
                }
            });

            container.Add(new CuiButton
            {
                Button = {
                    Command = "mclick",
                    Color = "0 0 0 0.5",
                    Close = ""
                },
                RectTransform = {
                    AnchorMin = "0.047 0.957",
                    AnchorMax = "0.051 0.992"
                }
            }, "Hud", "closef");

            container.Add(new CuiElement
            {
                Name = "closetext",
                Parent = "closef",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = "«",
                        FontSize = 13,
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 1"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    }
                }
            });

            container.Add(new CuiPanel
            {
                Image = {
                    Color = "0 0 0 0.5"
                },
                RectTransform = {
                    AnchorMin = "0.018 0.933",
                    AnchorMax = "0.051 0.954"
                },
                CursorEnabled = false
            }, "Hud", "onlinef");

            container.Add(new CuiPanel
            {
                Image = {
                    Color = "0 0 0 0.5"
                },
                RectTransform = {
                    AnchorMin = "0.004 0.933",
                    AnchorMax = "0.016 0.954"
                },
                CursorEnabled = false
            }, "Hud", "onlineip");

            container.Add(new CuiElement
            {
                Name = "online_icon",
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Color = "1 1 1 1",
                        Png = (string)ImageLibrary.Call("GetImage", "online")
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.004 0.933",
                        AnchorMax = "0.016 0.954"
                    }
                }
            });

            container.Add(new CuiElement
            {
                Name = "onlinecounter",
                Parent = "onlinef",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = $"{BasePlayer.activePlayerList.Count}/{ConVar.Server.maxplayers}",
                        FontSize = 12,
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 1"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    }
                }
            });

            CuiHelper.AddUi(player, container);
        }

        private void RefreshOnline()
        {
            var container = new CuiElementContainer();

            var OnlinePlayer = BasePlayer.activePlayerList.Count;

            container.Add(new CuiElement
            {
                Name = "onlinecounter",
                Parent = "onlinef",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = $"{BasePlayer.activePlayerList.Count}/{ConVar.Server.maxplayers}",
                        FontSize = 12,
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 1"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    }
                }
            });

            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, "onlinecounter");
                CuiHelper.AddUi(player, container);
            }
        }

        private void DestroyUIF(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "storef");
            CuiHelper.DestroyUi(player, "kitsblur");
            CuiHelper.DestroyUi(player, "storeblur");
            CuiHelper.DestroyUi(player, "closeblur");
            CuiHelper.DestroyUi(player, "onlineblur");
            CuiHelper.DestroyUi(player, "kitsf");
            CuiHelper.DestroyUi(player, "closef");
            CuiHelper.DestroyUi(player, "onlinecounter");
            CuiHelper.DestroyUi(player, "online_icon");
            CuiHelper.DestroyUi(player, "onlinef");
            CuiHelper.DestroyUi(player, "onlineip");
            CuiHelper.DestroyUi(player, "BlockMsg");
            CuiHelper.DestroyUi(player, "TimerPanel");
            CuiHelper.DestroyUi(player, "countDown");
            
        }

        private void DestroyUIM(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "fmain");
            CuiHelper.DestroyUi(player, "fmainicon");
        }


        private void DrawUIM(BasePlayer player)
        {
            DestroyUIM(player);

            var container = new CuiElementContainer();

            container.Add(new CuiButton
            {
                Button = {
                    Command = "fclick",
                    Color = "0 0 0 0.5",
                    Close = ""
                },
                RectTransform = {
                    AnchorMin = "0.004 0.957",
                    AnchorMax = "0.023 0.992"
                },
                Text = {
                    Text = "",
                    Color = "0 0 0 0"
                }
            }, "Hud", "fmain");

            container.Add(new CuiElement
            {
                Name = "fmainicon",
                Parent = "fmain",
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Color = "1 1 1 1",
                        Png = (string)ImageLibrary.Call("GetImage", "kits")
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.05 0.05",
                        AnchorMax = "0.899 0.899"
                    }
                }
            });

            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("max.click")]
        private void OnMaxClick(ConsoleSystem.Arg arg)
        {
            var player = arg?.Connection?.player as BasePlayer;
            if (player == null) return;

            DestroyUIM(player);
            DrawUIF(player);
            SendGUI(player);
        }
    }
}