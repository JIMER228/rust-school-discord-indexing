using Oxide.Core;
using Oxide.Core.Plugins;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("BarricadeToHighWallReplacer", "YourName", "1.1.2")]
    [Description("Replaces double wooden barricades with high external wooden walls for players with permission.")]
    class BarricadeToHighWallReplacer : RustPlugin
    {
        // Название разрешения
        private const string PermissionUse = "barricadetohighwallreplacer.use";

        void Init()
        {
            // Регистрируем разрешение при загрузке плагина
            permission.RegisterPermission(PermissionUse, this);
        }

        void OnEntityBuilt(Planner planner, GameObject gameObject)
        {
            // Проверяем, что игрок построил объект
            BaseEntity entity = gameObject.GetComponent<BaseEntity>();
            if (entity == null || entity.PrefabName != "assets/prefabs/deployable/barricades/barricade.cover.wood_double.prefab")
                return;

            // Получаем игрока, который построил баррикаду
            BasePlayer player = planner.GetOwnerPlayer();
            if (player == null || !permission.UserHasPermission(player.UserIDString, PermissionUse))
                return;

            // Убираем баррикаду
            entity.Kill();

            // Создаём высокую внешнюю деревянную стену (High External Wooden Wall)
            Quaternion rotation = gameObject.transform.rotation;
            Vector3 position = gameObject.transform.position;
            BaseEntity wallEntity = GameManager.server.CreateEntity("assets/prefabs/building/wall.external.high.wood/wall.external.high.wood.prefab", position, rotation);

            if (wallEntity == null)
                return;

            // Устанавливаем владельца стены (чтобы можно было удалить)
            wallEntity.OwnerID = player.userID;

            // Игнорируем долгую установку (устанавливаем сразу)
            wallEntity.skinID = 0; // Убедимся, что используется стандартный скин
            wallEntity.SendNetworkUpdateImmediate();

            // Размещаем стену
            wallEntity.Spawn();

            // Воспроизводим звук удара молотка по металлу
            Effect.server.Run("assets/bundled/prefabs/fx/build/hammer_metal.prefab", wallEntity.transform.position);
        }
    }
}