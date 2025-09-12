using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("MyMiniSwitch", ".h1pex", "1.0.0")]
    [Description("Switches between /mymini and /nomini commands")]
    public class MyMiniSwitch : RustPlugin
    {
        private bool isMyMini = true;

        [ChatCommand("myminiswitch")]
        private void MyMiniSwitchCommand(BasePlayer player, string command, string[] args)
        {
            if (isMyMini)
            {
                player.SendConsoleCommand("chat.say", "/mymini");
            }
            else
            {
                player.SendConsoleCommand("chat.say", "/nomini");
            }

            // Переключаем состояние
            isMyMini = !isMyMini;
        }
    }
}