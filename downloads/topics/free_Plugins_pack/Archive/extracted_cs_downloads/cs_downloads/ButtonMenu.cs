using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{

    [Info("ButtonMenu", "SARO", "1.0.0")]
    public class ButtonMenu : RustPlugin
    {
        [PluginReference] Plugin ImageLibrary;

        private string Layer = "UI_1";
        private string Layer1 = "UI_2";

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, Layer);
                CuiHelper.DestroyUi(player, Layer1);
            }

        }


        void OnServerInitialized()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                OnPlayerConnected(player);
            }
        }


        private void OnPlayerConnected(BasePlayer player)
        {
            if (player.IsReceivingSnapshot)
            {
                NextTick(() => OnPlayerConnected(player));
                return;
            }

            UI_1(player);
            UI_2(player);
        }

        private void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (player == null || info == null) return;

            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.DestroyUi(player, Layer1);
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            if (player == null)
            {

            }
            UI_1(player);
            UI_2(player);

        }

        private void UI_1(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                RectTransform = { AnchorMin = "0.5 0.0", AnchorMax = "0.5 0.0", OffsetMin = "447 81", OffsetMax = "458 48" },
                Image = { Color = "0 0 0 0", Sprite = "Assets/Content/UI/UI.Background.Tile.psd", Material = "assets/content/ui/uibackgroundblur.mat" }
            }, "Overlay", Layer);

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components = {
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Command = $"chat.say /upgrade", Color = "1 0.96 0.88 0.15" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "-264 -30", OffsetMax = "-204 30" },
                Text = { Text = "АПГРЕЙД", Align = TextAnchor.MiddleCenter, FontSize = 12 }
            }, Layer);

            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.AddUi(player, container);

        }

        private void UI_2(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                RectTransform = { AnchorMin = "0.5 0.0", AnchorMax = "0.5 0.0", OffsetMin = "447 48", OffsetMax = "458 18" },
                Image = { Color = "0 0 0 0", Sprite = "Assets/Content/UI/UI.Background.Tile.psd", Material = "assets/content/ui/uibackgroundblur.mat" }
            }, "Overlay", Layer1);


            container.Add(new CuiElement
            {
                Parent = Layer1,
                Components = {
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Command = $"backpack.open", Color = "1 0.96 0.88 0.15" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "-264 -30", OffsetMax = "-204 30" },
                Text = { Text = "РЮКЗАК", Align = TextAnchor.MiddleCenter, FontSize = 12 }
            }, Layer1);

            CuiHelper.DestroyUi(player, Layer1);
            CuiHelper.AddUi(player, container);
        }
    }
}
