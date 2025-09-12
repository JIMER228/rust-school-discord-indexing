using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Player Coordinates", "REIN", "1.0.0")]
    [Description("Показывает координаты игрока по команде в чате (только для админов)")]
    public class PlayerCoordinates : RustPlugin
    {
        private void Init()
        {
            // Регистрируем команду
            cmd.AddChatCommand("coords", this, "CoordsCommand");
        }

        private void CoordsCommand(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;

            // Проверяем, является ли игрок администратором
            if (!player.IsAdmin)
            {
                player.ChatMessage("У вас нет прав для использования этой команды!");
                return;
            }

            // Получаем позицию игрока
            Vector3 position = player.transform.position;
            
            // Форматируем сообщение с координатами
            string message = $"Ваши координаты: X: {position.x:F1}, Y: {position.y:F1}, Z: {position.z:F1}";
            
            // Отправляем сообщение игроку
            player.ChatMessage(message);
        }
    }
}
