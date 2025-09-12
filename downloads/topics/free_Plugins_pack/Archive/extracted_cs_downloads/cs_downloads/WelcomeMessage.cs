using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("WelcomeMessage", "tututu123", "1.0.0")]
    [Description("Sends a welcome message to players when they join the server")]

    public class WelcomeMessage : CovalencePlugin
    {
        // Настройка текста приветствия
        private string welcomeMessage = "Welcome to the server, {player}! Have fun and good luck!";

        // Вызывается при входе игрока на сервер
        private void OnUserConnected(IPlayer player)
        {
            // Отправляем персонализированное сообщение
            player.Message(welcomeMessage.Replace("{player}", player.Name));
        }

        // Создание или загрузка конфигурации
        protected override void LoadDefaultConfig()
        {
            Config["WelcomeMessage"] = welcomeMessage;
            SaveConfig();
        }

        // Загрузка данных из конфигурации
        private void Init()
        {
            welcomeMessage = Config["WelcomeMessage"]?.ToString() ?? welcomeMessage;
        }
    }
}