using Oxide.Core;
using Oxide.Plugins;

namespace Oxide.Plugins
{
    [Info("SocialLinks", "wyir", "1.1.0")]
    [Description("Отправляет ссылки на социальные сети проекта при вводе команды /link")]
    public class SocialLinks : RustPlugin
    {
        // Социальные сети проекта с цветами
        private string socialLinks = "Подпишись на нас:\n"
                                     + "<color=#75c3ff>Telegram</color>: https://t.me/yourproject\n"
                                     + "<color=#7289DA>Discord</color>: https://discord.gg/yourproject\n" //тут менять ссылки
                                     + "<color=#4C75A3>ВКонтакте</color>: https://vk.com/yourproject";

        // Обработчик команды /link
        [ChatCommand("link")] // тут можно изменить комманду
        private void LinkCommand(BasePlayer player, string command, string[] args)
        {
            // Отправляем сообщение в чат
            SendReply(player, socialLinks);
        }
    }
}
