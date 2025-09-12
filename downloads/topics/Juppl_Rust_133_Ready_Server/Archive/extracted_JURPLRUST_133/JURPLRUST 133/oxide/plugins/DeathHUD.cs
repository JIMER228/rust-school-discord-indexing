using Oxide.Game.Rust.Cui;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("DeathHUD", "setfps", "0.1")]
    class DeathHUD : RustPlugin
    {
        float AnchorMaxY = 0.96f;

        List<string> killsCache = new List<string>();
        List<Timer> killsTimer = new List<Timer>();

        void AddDeath(string text)
        {
            killsCache.Add(text);
            killsTimer.Add(timer.Once(25f, () => removeLine()));

            if (killsCache.Count > 10)
            {
                killsTimer[0].Destroy();
                removeLine();
            }
            else
            {
                ShowDeathHud();
            }          
        }

        void ShowDeathHud()
        {            
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiElement()
            {
                Name = "DeathHUD",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = string.Join("\n", killsCache.ToArray()),
                        FontSize = 12,
                        Font = "robotocondensed-bold.ttf",
                        Align = UnityEngine.TextAnchor.UpperRight
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0.4 " + (AnchorMaxY - (0.03f * killsCache.Count)),
                        AnchorMax = "0.99 " + AnchorMaxY
                    },
                    new CuiOutlineComponent()
                    {
                        Color = "0 0 0 1",
                        Distance = "0.5 -0.5"
                    }
                }
            });

            foreach(BasePlayer player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, "DeathHUD");
                CuiHelper.AddUi(player, container);
            }
        }

        void removeLine()
        {
            killsCache.RemoveAt(0);
            killsTimer.RemoveAt(0);

            if (killsCache.Count < 1)
            {
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                {
                    CuiHelper.DestroyUi(player, "DeathHUD");
                }

                return;
            }

            ShowDeathHud();
        }
    }
}