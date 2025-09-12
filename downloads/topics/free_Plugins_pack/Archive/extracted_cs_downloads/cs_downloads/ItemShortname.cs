using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("ItemShortname", "Sava", "0.0.0")]
    public class ItemShortname : RustPlugin
    {
        [ChatCommand("is")]
        void ItemShortName(BasePlayer player)
        {
            Item i = player.GetActiveItem();
            if (i != null)
            {
                player.ChatMessage(i.info.shortname);
                Ui(player, i.info.shortname, i.skin);
            }
                
                
        }

        void Ui(BasePlayer player, string text, ulong skin)
        {
            CuiHelper.DestroyUi(player, "UI_name");
            CuiHelper.DestroyUi(player, "UI_skin");
            CuiHelper.AddUi(player, new CuiElementContainer
            {
                new CuiElement
                {
                    Parent = "Overlay",
                    Name = "UI_name",
                    Components =
                    {
                        new CuiInputFieldComponent
                        {
                            Text = text,
                            ReadOnly = true,
                            FontSize = 16,
                            Align = TextAnchor.MiddleCenter,
                        }, 
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 0.5",
                            AnchorMax = "0.5 0.5",
                            OffsetMin = "-200 -200",
                            OffsetMax = "200 -170"
                        }
                    }
                }
            });
            CuiHelper.AddUi(player, new CuiElementContainer
            {
                new CuiElement
                {
                    Parent = "Overlay",
                    Name = "UI_skin",
                    Components =
                    {
                        new CuiInputFieldComponent
                        {
                            Text = skin.ToString(),
                            ReadOnly = true,
                            FontSize = 16,
                            Align = TextAnchor.MiddleCenter,
                        }, 
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 0.5",
                            AnchorMax = "0.5 0.5",
                            OffsetMin = "-200 -150",
                            OffsetMax = "200 -120"
                        }
                    }
                }
            });
            timer.Once(5f, () => Destroy(player));
        }
        [ChatCommand("is_d")]
        void Destroy(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "UI_name");
            CuiHelper.DestroyUi(player, "UI_skin");
        }
    }
}