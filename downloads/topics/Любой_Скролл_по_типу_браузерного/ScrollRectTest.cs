using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Game.Rust.Cui;
using System.Collections.Generic;
using System.Numerics;

namespace Oxide.Plugins
{
    [Info("ScrollRectTest", "MaltrzD", "0.0.1")]
    class ScrollRectTest : RustPlugin
    {
        [ChatCommand("test")]
        private void ScrollTestCommand(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "Scroller");

            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0.3130209 0.1944444",
                    AnchorMax = "0.7140625 0.8166667"
                }
            }, "Hud", "Scroller");
            container.Add(new CuiElement
            {
                Parent = "Scroller",
                Name = "ScrollRect",
                Components =
                {
                    new CuiScrollViewComponent
                    {
                        Horizontal = false,
                        Vertical = true,
                        MovementType = UnityEngine.UI.ScrollRect.MovementType.Elastic,
                        ScrollSensitivity = 24,
                        Inertia = true,
                        DecelerationRate = 0.24f,


                        ContentTransform = new CuiRectTransform
                        {
                            AnchorMin = "0 1",
                            AnchorMax = "1 1",
                            OffsetMin = "0 -2000",
                            OffsetMax = "0 0"
                        },
                        VerticalScrollbar = new CuiScrollbar
                        {
                            Size = 10,
                            AutoHide = true,
                            HandleColor = "1 1 1 0.25",
                            HighlightColor = "1 1 1 0.6",
                            PressedColor = "1 1 1 0.6",
                        }
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    },
                    new CuiImageComponent
                    {
                        Color = "0.49 0.49 0.49 1"
                    },
                    new CuiNeedsCursorComponent {}
                }
            });

            int spawnCount = 100;
            int labelsPerLine = 5;

            int aMinX = 10;
            int aMaxX = 100;

            int aMinY = 1900;
            int aMaxY = 1990;
            for (int i = 0; i < spawnCount; i++)
            {
                if (i % (labelsPerLine) == 0 && i != 0)
                {
                    aMinY -= 100;
                    aMaxY -= 100;

                    aMinX = 10;
                    aMaxX = 100;
                }

                container.Add(new CuiPanel
                {
                    Image =
                    {
                        Color = "0.239 0.239 0.239 1"
                    },
                    RectTransform =
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "0 0",

                        OffsetMin = $"{aMinX} {aMinY}",
                        OffsetMax = $"{aMaxX} {aMaxY}",
                    }
                }, "ScrollRect", $"ScrollRect.{i}");
                container.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = $"{i}",
                        FontSize = 35,
                        Align = UnityEngine.TextAnchor.MiddleCenter,
                        Color = "1 1 1 0.7",
                    },
                    RectTransform =
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    }
                }, $"ScrollRect.{i}", $"ScrollRect.{i}.Label");

                aMinX += 100;
                aMaxX += 100;
            }

            CuiHelper.AddUi(player, container);

            timer.Once(25f, () => CuiHelper.DestroyUi(player, "Scroller"));
        }
	}
}
