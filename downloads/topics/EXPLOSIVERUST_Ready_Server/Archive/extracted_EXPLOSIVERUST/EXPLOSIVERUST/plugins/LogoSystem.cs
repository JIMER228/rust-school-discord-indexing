using System.Collections.Generic;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("LogoSystem", "https://topplugin.ru / https://discord.com/invite/5DPTsRmd3G", "1.0.3")]
    class LogoSystem : RustPlugin
    {
        #region Вар
        string Layer = "Button_UI";

        Dictionary<string, string> Button = new Dictionary<string, string>()
        {
            ["R"] = "",
            ["store"] = "assets/icons/cart.png",
            ["menu"] = "assets/icons/community_servers.png"
        };

        [PluginReference] Plugin ChatSystem;
        #endregion

        #region Хуки
        void OnServerInitialized()
        {
            foreach (var target in BasePlayer.activePlayerList)
                OnPlayerConnected(target);
        }

        void OnPlayerConnected(BasePlayer player) 
        {
            if (player.IsReceivingSnapshot)
            {
                NextTick(() => OnPlayerConnected(player));
                return;
            }
            if ((bool)ChatSystem?.Call("ApiLogo", player))
                ButtonUI(player);
        }

        void Unload()
        {
            foreach (BasePlayer target in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(target, Layer);
        }
        #endregion

        #region Команда
        [ConsoleCommand("logo_enable")]
        void ConsoleLogo(ConsoleSystem.Arg args)
        {
            var player = args.Player();

            if (!(bool)ChatSystem?.Call("ApiLogo", player))
            {
                CuiHelper.DestroyUi(player, Layer);
            }
            else
            {
                ButtonUI(player);
            }
        }
        #endregion

        #region Интерфейс
        void ButtonUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, Layer);
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "0 -25", OffsetMax = "80.4 0" },
                Image = { Color = "1 1 1 0" }
            }, "Overlay", Layer);

            float width = 0.34f, height = 1f, startxBox = 0f, startyBox = 1f - height, xmin = startxBox, ymin = startyBox;
            foreach (var check in Button)
            {
                var text = check.Key == "R" ? check.Key : "";
                var command = check.Key != "R" ? $"chat.say /{check.Key}" : "";
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = xmin + " " + ymin, AnchorMax = (xmin + width) + " " + (ymin + height * 1), OffsetMin = "0 0", OffsetMax = "-2 0" },
                    Button = { Color = "1 1 1 0.1", Command = command },
                    Text = { Text = text, Color = "1 1 1 0.8", Align = TextAnchor.MiddleCenter, FontSize = 16, Font = "robotocondensed-bold.ttf" }
                }, Layer, "Image");

                if (check.Value != "")
                {
                    container.Add(new CuiElement
                    {
                        Parent = "Image",
                        Components =
                        {
                            new CuiImageComponent { Color = "1 1 1 0.8", Sprite = check.Value, FadeIn = 0.5f },
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "3 3", OffsetMax = "-3 -3" }
                        }
                    });
                }

                xmin += width;
            }

            CuiHelper.AddUi(player, container);
        }
        #endregion
    }
}