using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("TechnicalPause","Тест","0.0")]
    public class TechnicalPause : RustPlugin
    {
        void UI(BasePlayer player)
        {
            CuiElementContainer container =new CuiElementContainer();
            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0 0",AnchorMax = "1 1"},
                Text = {Align = TextAnchor.MiddleCenter,Text = "Временно закрыто на техработы. Скоро здесь что-то появится :3",FontSize = 32}
            }, "ContentUI");

            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("standby")]
        void StandByCommand(ConsoleSystem.Arg arg)
        {
            UI(arg.Player());
        }
    }
}