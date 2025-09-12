using Oxide.Core;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;

namespace Oxide.Plugins
{
    [Info("GChill", "h1pex", "1.8.1")]
    [Description("Beach chair that perfectly matches player's Y-axis rotation")]
    class GChill : RustPlugin
    {
        private Dictionary<ulong, BaseEntity> playerChairs = new Dictionary<ulong, BaseEntity>();
        private Dictionary<ulong, float> lastRotationChecks = new Dictionary<ulong, float>();
        private Dictionary<BuildingPrivlidge, ulong> tcPlacedBy = new Dictionary<BuildingPrivlidge, ulong>();
        private Dictionary<ulong, float> lastSecurityChecks = new Dictionary<ulong, float>();
        
        private const float ROTATION_SPEED = 6.5f;
        private const float VIEW_THRESHOLD = 35f;
        private const float LERP_SPEED = 0.15f;
        private const float ROTATION_CHECK_INTERVAL = 0.033f;
        private const float SECURITY_CHECK_INTERVAL = 0.5f;

        void Init()
        {
            playerChairs.Clear();
            tcPlacedBy.Clear();
            lastRotationChecks.Clear();
            lastSecurityChecks.Clear();
            
            permission.RegisterPermission("gchill.use", this);
            timer.Every(SECURITY_CHECK_INTERVAL, CheckAllPlayerChairs);
            Subscribe("OnEntityBuilt");
        }

        void OnPlayerDisconnected(BasePlayer player) => CleanupChair(player?.userID ?? 0);
        void OnPlayerDie(BasePlayer player) => CleanupChair(player?.userID ?? 0);

        // Заменяем OnFrame на более эффективный таймер
        void OnTick()
        {
            float currentTime = Time.realtimeSinceStartup;

            foreach (var kvp in new Dictionary<ulong, BaseEntity>(playerChairs))
            {
                var playerId = kvp.Key;
                var chair = kvp.Value;

                // Проверяем время последней проверки поворота
                if (!lastRotationChecks.TryGetValue(playerId, out float lastCheck) || 
                    (currentTime - lastCheck) >= ROTATION_CHECK_INTERVAL)
                {
                    var player = BasePlayer.FindByID(playerId);
                    if (!IsValidPlayerAndChair(player, chair)) continue;
                    if (!(chair is BaseMountable mountable) || !mountable.IsMounted()) continue;

                    // Проверка безопасности с интервалом
                    if (!lastSecurityChecks.TryGetValue(playerId, out float lastSecurityCheck) || 
                        (currentTime - lastSecurityCheck) >= SECURITY_CHECK_INTERVAL)
                    {
                        if (IsPlayerOnForeignTerritory(player))
                        {
                            SendReply(player, "Вы находитесь на чужой территории. Лежак убран.");
                            DismountAndCleanupChair(playerId);
                            continue;
                        }
                        lastSecurityChecks[playerId] = currentTime;
                    }

                    // Обновляем поворот
                    UpdateChairRotation(player, chair);
                    lastRotationChecks[playerId] = currentTime;
                }
            }
        }

        private void UpdateChairRotation(BasePlayer player, BaseEntity chair)
        {
            float playerViewAngle = player.eyes.rotation.eulerAngles.y;
            float chairAngle = chair.transform.rotation.eulerAngles.y;
            float relativeAngle = Mathf.DeltaAngle(chairAngle, playerViewAngle);

            if (Mathf.Abs(relativeAngle) > VIEW_THRESHOLD)
            {
                float rotationDirection = Mathf.Sign(relativeAngle);
                
                // Убираем зависимость от Time.deltaTime для более стабильной скорости
                float targetRotation = ROTATION_SPEED * rotationDirection;
                
                // Применяем более прямой расчет нового угла
                float newAngle = Mathf.LerpAngle(chairAngle, playerViewAngle, LERP_SPEED);
                
                // Ограничиваем максимальное изменение угла за одно обновление
                float maxDelta = ROTATION_SPEED;
                float actualDelta = Mathf.DeltaAngle(chairAngle, newAngle);
                if (Mathf.Abs(actualDelta) > maxDelta)
                {
                    newAngle = chairAngle + (maxDelta * Mathf.Sign(actualDelta));
                }

                chair.transform.rotation = Quaternion.Euler(0f, newAngle, 0f);
                chair.TransformChanged();
                chair.SendNetworkUpdate();
            }
        }

        // Оптимизированная проверка территории
        private bool IsPlayerOnForeignTerritory(BasePlayer player)
        {
            if (player == null) return true;

            // Кэшируем результат проверки TC
            var nearestPrivilege = GetNearestBuildingPrivilege(player.transform.position);
            if (nearestPrivilege != null)
            {
                if (!nearestPrivilege.IsAuthed(player))
                {
                    // Проверяем, установил ли игрок этот TC
                    if (tcPlacedBy.TryGetValue(nearestPrivilege, out ulong placerId) && placerId != player.userID)
                    {
                        return true;
                    }
                }
            }

            // Проверка постройки под ногами
            RaycastHit hit;
            if (Physics.Raycast(player.transform.position + Vector3.up * 0.1f, Vector3.down, out hit, 3f, 
                LayerMask.GetMask("Construction", "Deployed")))
            {
                BaseEntity entity = hit.GetEntity();
                if (entity != null && entity.OwnerID != 0 && entity.OwnerID != player.userID)
                {
                    if (!AreInSameTeam(player.userID, entity.OwnerID))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // Кэширующий метод получения ближайшего TC
        private BuildingPrivlidge GetNearestBuildingPrivilege(Vector3 position)
        {
            BuildingPrivlidge nearest = null;
            float nearestDistance = float.MaxValue;

            var privileges = UnityEngine.Object.FindObjectsOfType<BuildingPrivlidge>();
            foreach (var privilege in privileges)
            {
                if (privilege == null || privilege.IsDestroyed) continue;

                float distance = Vector3.Distance(position, privilege.transform.position);
                if (distance <= 30f && distance < nearestDistance)
                {
                    nearest = privilege;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        // Новый метод для проверки всех активных лежаков
        private void CheckAllPlayerChairs()
        {
            // Копируем словарь для безопасного перебора
            foreach (var kvp in new Dictionary<ulong, BaseEntity>(playerChairs))
            {
                var playerId = kvp.Key;
                var chair = kvp.Value;
                var player = BasePlayer.FindByID(playerId);
                
                if (player == null || chair == null || chair.IsDestroyed) 
                {
                    CleanupChair(playerId);
                    continue;
                }
                
                // Проверяем нахождение на чужой территории
                if (IsPlayerOnForeignTerritory(player))
                {
                    // Принудительно убираем лежак
                    DismountAndCleanupChair(playerId);
                    SendReply(player, "Серверная проверка: вы находитесь на чужой территории. Лежак убран.");
                }
            }
        }

        // Отслеживаем установку шкафов
        void OnEntityBuilt(Planner plan, GameObject gameObject)
        {
            if (plan == null || gameObject == null) return;
            
            BasePlayer player = plan.GetOwnerPlayer();
            if (player == null) return;
            
            BaseEntity entity = gameObject.GetComponent<BaseEntity>();
            if (entity == null) return;
            
            // Если был установлен TC, записываем кто его установил
            if (entity is BuildingPrivlidge)
            {
                BuildingPrivlidge tc = entity as BuildingPrivlidge;
                tcPlacedBy[tc] = player.userID;
                
                // Сохраняем данные в хранилище для восстановления после перезагрузки сервера
                SaveTCData();
            }
        }
        
        // Сохраняем данные о TC в хранилище
        private void SaveTCData()
        {
            var data = new Dictionary<string, ulong>();
            foreach (var kvp in tcPlacedBy)
            {
                if (kvp.Key != null && !kvp.Key.IsDestroyed)
                {
                    // Используем сетевой ID TC как ключ
                    data[kvp.Key.net.ID.ToString()] = kvp.Value;
                }
            }
            
            // Сохраняем данные
            Interface.Oxide.DataFileSystem.WriteObject("GChill_TCData", data);
        }
        
        // Загружаем данные о TC при загрузке плагина
        void LoadTCData()
        {
            var data = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, ulong>>("GChill_TCData");
            if (data == null) return;
            
            // Очищаем текущие данные
            tcPlacedBy.Clear();
            
            // Загружаем данные из хранилища
            foreach (var kvp in data)
            {
                try
                {
                    uint netID;
                    if (uint.TryParse(kvp.Key, out netID))
                    {
                        // Находим TC по его сетевому ID используя правильный класс
                        BaseNetworkable entity = null;
                        
                        // Пробуем найти напрямую по ID
                        foreach (var netEntity in BaseNetworkable.serverEntities)
                        {
                            if (netEntity.net.ID.Value == netID)
                            {
                                entity = netEntity;
                                break;
                            }
                        }
                        
                        if (entity != null && entity is BuildingPrivlidge)
                        {
                            tcPlacedBy[entity as BuildingPrivlidge] = kvp.Value;
                        }
                    }
                }
                catch {}
            }
        }
        
        // Загружаем данные при загрузке плагина
        void OnServerInitialized()
        {
            LoadTCData();
        }
        
        // Сохраняем данные при выгрузке плагина
        void Unload()
        {
            SaveTCData();
        }

        // Проверка на членство в команде
        private bool AreInSameTeam(ulong playerID, ulong otherID)
        {
            if (playerID == otherID) return true;
            
            BasePlayer player = BasePlayer.FindByID(playerID);
            if (player == null) return false;
            
            RelationshipManager.PlayerTeam team = RelationshipManager.ServerInstance.FindPlayersTeam(playerID);
            if (team == null) return false;
            
            return team.members.Contains(otherID);
        }

        // Заменяем старый метод проверки позиции на новый
        private bool IsPositionSafe(Vector3 position, ulong playerID)
        {
            BasePlayer player = BasePlayer.FindByID(playerID);
            if (player == null) return false;
            
            // Временно перемещаем игрока на проверяемую позицию (только для проверки)
            Vector3 originalPosition = player.transform.position;
            player.transform.position = position;
            
            bool result = !IsPlayerOnForeignTerritory(player);
            
            // Возвращаем игрока на исходную позицию
            player.transform.position = originalPosition;
            
            return result;
        }

        // Заменяем старый метод проверки на новый
        private bool IsSafeToPlaceChair(BasePlayer player)
        {
            if (player == null) return false;
            return !IsPlayerOnForeignTerritory(player);
        }

        // Метод для принудительного снятия игрока с лежака и удаления
        private void DismountAndCleanupChair(ulong userId)
        {
            if (playerChairs.TryGetValue(userId, out var chair))
            {
                if (chair != null && !chair.IsDestroyed)
                {
                    var mountable = chair as BaseMountable;
                    if (mountable != null && mountable.IsMounted())
                    {
                        // Снимаем игрока
                        var player = BasePlayer.FindByID(userId);
                        if (player != null)
                        {
                            mountable.DismountPlayer(player);
                        }
                    }
                    
                    // Убиваем стул
                    chair.Kill();
                }
                playerChairs.Remove(userId);
            }
        }

        // Защита игрока на стуле от получения урона
        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return null;

            // Проверяем, является ли цель игроком на стуле
            var targetPlayer = entity as BasePlayer;
            if (targetPlayer != null && playerChairs.ContainsKey(targetPlayer.userID))
            {
                var chair = playerChairs[targetPlayer.userID];
                if (chair != null && !chair.IsDestroyed && (chair as BaseMountable)?.IsMounted() == true)
                {
                    // Отменяем урон
                    return true;
                }
            }

            // Проверяем, является ли атакующий игроком на стуле
            var attackerPlayer = info.InitiatorPlayer;
            if (attackerPlayer != null && playerChairs.ContainsKey(attackerPlayer.userID))
            {
                var chair = playerChairs[attackerPlayer.userID];
                if (chair != null && !chair.IsDestroyed && (chair as BaseMountable)?.IsMounted() == true)
                {
                    // Отменяем урон от игрока на стуле
                    return true;
                }
            }

            return null;
        }

        // Предотвращение нанесения урона игроком на стуле
        object OnWeaponAttack(BaseProjectile projectile, HitInfo info)
        {
            if (projectile == null || info == null) return null;

            // Проверяем, является ли стреляющий игроком на стуле
            var attackerPlayer = info.InitiatorPlayer;
            if (attackerPlayer != null && playerChairs.ContainsKey(attackerPlayer.userID))
            {
                var chair = playerChairs[attackerPlayer.userID];
                if (chair != null && !chair.IsDestroyed && (chair as BaseMountable)?.IsMounted() == true)
                {
                    // Отменяем выстрел
                    return true;
                }
            }

            return null;
        }

        [ChatCommand("gc")]
        void cmdGChill(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;

            // Проверяем нахождение на чужой территории
            if (IsPlayerOnForeignTerritory(player))
            {
                SendReply(player, "Вы не можете использовать лежак на чужой территории!");
                return;
            }

            if (!permission.UserHasPermission(player.UserIDString, "gchill.use"))
            {
                SendReply(player, "У вас нет разрешения на использование этой команды!");
                return;
            }

            // Проверка и очистка старых лежаков
            if (playerChairs.TryGetValue(player.userID, out var existingChair))
            {
                if (existingChair != null && !existingChair.IsDestroyed)
                {
                    SendReply(player, "У вас уже есть лежак!");
                    return;
                }
                playerChairs.Remove(player.userID);
            }

            // Получаем чистый Y-ротационный вектор игрока
            Quaternion playerRotation = Quaternion.Euler(0, player.transform.eulerAngles.y, 0);
            
            // Создаем лежак
            CreateChairForPlayer(player, playerRotation);
        }

        void CreateChairForPlayer(BasePlayer player, Quaternion rotation)
        {
            // Проверяем нахождение на чужой территории
            if (IsPlayerOnForeignTerritory(player))
            {
                SendReply(player, "Вы не можете использовать лежак на чужой территории!");
                return;
            }

            // УПРОЩЕННОЕ РАЗМЕЩЕНИЕ - без сложных проверок коллизий
            Vector3 chairPosition = Vector3.zero; // Инициализируем переменную
            RaycastHit hit;
            
            // Выполняем рейкаст чтобы проверить препятствия впереди игрока
            bool hasObstacle = Physics.Raycast(player.eyes.position, player.eyes.HeadForward(), out hit, 3f, LayerMask.GetMask("Construction", "World", "Deployed"));
            
            if (hasObstacle)
            {
                // Если на пути есть препятствие, проверяем расстояние до него
                float distToObstacle = Vector3.Distance(player.eyes.position, hit.point);
                
                if (distToObstacle < 1.5f)
                {
                    SendReply(player, "Недостаточно места для размещения лежака. Отойдите от препятствия!");
                    return;
                }
                
                // Размещаем лежак перед препятствием
                chairPosition = hit.point - player.eyes.HeadForward() * 0.5f;
                
                // Убеждаемся, что лежак не пройдет сквозь препятствие
                Vector3 finalPos = chairPosition;
                finalPos.y += 0.2f; // Небольшая подстраховка по высоте
                
                // Проверяем, не будет ли стул пересекать стену/препятствие
                Collider[] colliders = Physics.OverlapBox(
                    finalPos,
                    new Vector3(1f, 0.3f, 0.4f), // Половина размеров лежака
                    rotation,
                    LayerMask.GetMask("Construction", "World", "Deployed")
                );
                
                if (colliders.Length > 0)
                {
                    SendReply(player, "Лежак будет пересекать препятствие. Измените позицию!");
                    return;
                }
                
                // Если прошли все проверки, спавним лежак
                SpawnChair(player, chairPosition, player.transform.rotation);
                return;
            }
            else
            {
                // Проверяем, на чем стоит игрок
                bool isOnBuildingOrFoundation = false;
                if (Physics.Raycast(player.transform.position, Vector3.down, out hit, 3f, LayerMask.GetMask("Construction", "World", "Deployed")))
                {
                    BaseEntity entity = hit.GetEntity();
                    if (entity != null)
                    {
                        // Проверяем, является ли это фундаментом или частью постройки
                        BuildingBlock block = entity as BuildingBlock;
                        if (block != null)
                        {
                            // Это фундамент или часть постройки
                            isOnBuildingOrFoundation = true;
                            
                            // Проверяем принадлежность или доступ
                            if (block.OwnerID == player.userID || HasBuildingAccess(player, block))
                            {
                                // Размещаем лежак перед игроком с проверкой на коллизии
                                Vector3 potentialPos = new Vector3(
                                    player.transform.position.x + player.transform.forward.x * 1.8f,
                                    hit.point.y + 0.4f, // Немного выше поверхности
                                    player.transform.position.z + player.transform.forward.z * 1.8f
                                );
                                
                                // Проверяем линию от игрока до потенциальной позиции лежака
                                if (!Physics.Linecast(player.eyes.position, potentialPos, LayerMask.GetMask("Construction", "World", "Deployed")))
                                {
                                    // Проверяем, не будет ли стул пересекать стену/препятствие
                                    Collider[] colliders = Physics.OverlapBox(
                                        potentialPos,
                                        new Vector3(1f, 0.3f, 0.4f), // Половина размеров лежака
                                        rotation,
                                        LayerMask.GetMask("Construction", "World", "Deployed")
                                    );
                                    
                                    if (colliders.Length == 0)
                                    {
                                        chairPosition = potentialPos;
                                        SpawnChair(player, chairPosition, player.transform.rotation);
                                        return;
                                    }
                                    else
                                    {
                                        SendReply(player, "Лежак будет пересекать препятствие. Измените позицию!");
                                        return;
                                    }
                                }
                                else
                                {
                                    SendReply(player, "На пути к месту размещения есть препятствие!");
                                    return;
                                }
                            }
                        }
                    }
                }
                
                // Если игрок не на фундаменте или у него нет доступа, пробуем обычное размещение
                if (!isOnBuildingOrFoundation)
                {
                    // Пробуем разные позиции рядом с игроком
                    bool foundValidPos = false;
                    
                    // Используем круговой поиск для проверки разных позиций
                    for (float angle = 0; angle < 360; angle += 45)
                    {
                        float radians = angle * Mathf.Deg2Rad;
                        Vector3 direction = new Vector3(Mathf.Sin(radians), 0, Mathf.Cos(radians));
                        
                        for (float distance = 1.5f; distance <= 2.5f; distance += 0.5f)
                        {
                            Vector3 testPos = player.transform.position + direction * distance;
                            
                            // Проверка высоты поверхности
                            if (Physics.Raycast(testPos + Vector3.up * 2f, Vector3.down, out hit, 4f, LayerMask.GetMask("Terrain", "World", "Construction")))
                            {
                                Vector3 potentialPos = new Vector3(testPos.x, hit.point.y + 0.4f, testPos.z);
                                
                                // Проверяем линию видимости от игрока до позиции
                                if (!Physics.Linecast(player.eyes.position, potentialPos, LayerMask.GetMask("Construction", "World", "Deployed")))
                                {
                                    // Проверяем, не будет ли стул пересекать препятствие
                                    Collider[] colliders = Physics.OverlapBox(
                                        potentialPos,
                                        new Vector3(1f, 0.3f, 0.4f), // Половина размеров лежака
                                        rotation,
                                        LayerMask.GetMask("Construction", "World", "Deployed")
                                    );
                                    
                                    if (colliders.Length == 0)
                                    {
                                        chairPosition = potentialPos;
                                        foundValidPos = true;
                                        break;
                                    }
                                }
                            }
                        }
                        
                        if (foundValidPos) break;
                    }
                    
                    if (foundValidPos)
                    {
                        SpawnChair(player, chairPosition, player.transform.rotation);
                        return;
                    }
                    else
                    {
                        SendReply(player, "Не удалось найти подходящее место для лежака!");
                        return;
                    }
                }
            }
            
            // Если все проверки не прошли, сообщаем об ошибке
            SendReply(player, "Не удалось создать лежак. Попробуйте в другом месте.");
        }

        // Новый метод для прямого создания стула без лишних проверок
        private void SpawnChair(BasePlayer player, Vector3 position, Quaternion rotation)
        {
            // Создаем сущность лежака
            var beachChair = GameManager.server.CreateEntity(
                "assets/prefabs/misc/summer_dlc/beach_chair/beachchair.deployed.prefab", 
                position, 
                rotation);

            if (beachChair == null)
            {
                SendReply(player, "Не удалось создать лежак.");
                return;
            }

            // Принудительно устанавливаем позицию и вращение
            beachChair.transform.position = position;
            beachChair.transform.rotation = rotation;
            
            // Спавним и регистрируем
            beachChair.Spawn();
            playerChairs[player.userID] = beachChair;

            // Сажаем игрока с задержкой
            timer.Once(0.2f, () =>
            {
                if (IsValidPlayerAndChair(player, beachChair))
                {
                    try
                    {
                        (beachChair as BaseMountable).MountPlayer(player);
                        SendReply(player, "Лежак создан!");
                    }
                    catch
                    {
                        // Если не удалось посадить игрока, удаляем лежак и пробуем еще раз с увеличенной высотой
                        Vector3 newPosition = position;
                        newPosition.y += 0.3f;
                        
                        beachChair.Kill();
                        playerChairs.Remove(player.userID);
                        
                        var newChair = GameManager.server.CreateEntity(
                            "assets/prefabs/misc/summer_dlc/beach_chair/beachchair.deployed.prefab", 
                            newPosition, 
                            rotation);
                            
                        if (newChair != null)
                        {
                            newChair.Spawn();
                            playerChairs[player.userID] = newChair;
                            
                            timer.Once(0.2f, () =>
                            {
                                if (IsValidPlayerAndChair(player, newChair))
                                {
                                    try
                                    {
                                        (newChair as BaseMountable).MountPlayer(player);
                                        SendReply(player, "Лежак создан!");
                                    }
                                    catch
                                    {
                                        newChair.Kill();
                                        playerChairs.Remove(player.userID);
                                        SendReply(player, "Не удалось сесть на лежак.");
                                    }
                                }
                            });
                        }
                        else
                        {
                            SendReply(player, "Не удалось создать лежак.");
                        }
                    }
                }
            });
        }

        bool IsValidPlayerAndChair(BasePlayer player, BaseEntity chair)
        {
            return player != null && !player.IsDead() && 
                   chair != null && !chair.IsDestroyed &&
                   playerChairs.ContainsKey(player.userID) && 
                   playerChairs[player.userID] == chair;
        }

        private void CleanupChair(ulong userId)
        {
            if (playerChairs.TryGetValue(userId, out var chair))
            {
                if (chair != null && !chair.IsDestroyed)
                    chair.Kill();
                playerChairs.Remove(userId);
            }
        }

        void OnEntityDismounted(BaseMountable entity, BasePlayer player)
        {
            if (player == null || !playerChairs.TryGetValue(player.userID, out var chair))
                return;

            if (chair == entity)
            {
                player.SendConsoleCommand("cursor.visible", false);
                player.SendConsoleCommand("cursor.lock", true);
                timer.Once(0.5f, () => CleanupChair(player.userID));
            }
        }

        // Метод для проверки доступа к постройке
        private bool HasBuildingAccess(BasePlayer player, BaseEntity entity)
        {
            // Проверка прямого владения объектом
            if (entity.OwnerID == player.userID)
            {
                return true;
            }
            
            // Проверяем является ли объект частью здания
            BuildingBlock block = entity as BuildingBlock;
            if (block != null)
            {
                // Проверяем владельца блока
                if (block.OwnerID == player.userID)
                {
                    return true;
                }
                
                // Проверяем авторизацию в ближайших TC
                var cupboards = UnityEngine.Object.FindObjectsOfType<BuildingPrivlidge>();
                foreach (var cupboard in cupboards)
                {
                    if (cupboard == null || cupboard.IsDestroyed) continue;
                    
                    float distance = Vector3.Distance(block.transform.position, cupboard.transform.position);
                    if (distance <= 30f && cupboard.IsAuthed(player))
                    {
                        return true;
                    }
                }
            }
            
            // Проверка авторизации в шкафе привилегий напрямую
            BuildingPrivlidge privilege = entity as BuildingPrivlidge;
            if (privilege != null)
            {
                return privilege.IsAuthed(player);
            }
            
            // Проверка доступа через кодовый замок
            var lockable = entity.GetComponent<BaseLock>();
            if (lockable != null)
            {
                if (!lockable.IsLocked())
                {
                    return true; // Если замок не заперт, считаем что есть доступ
                }
                
                CodeLock codeLock = lockable as CodeLock;
                if (codeLock != null)
                {
                    // Проверяем авторизацию в кодовом замке
                    return codeLock.whitelistPlayers.Contains(player.userID) || 
                           codeLock.guestPlayers.Contains(player.userID);
                }
            }
            
            // Проверяем наличие шкафа привилегий в радиусе
            var cupboardsNearby = UnityEngine.Object.FindObjectsOfType<BuildingPrivlidge>();
            foreach (var cupboard in cupboardsNearby)
            {
                if (Vector3.Distance(entity.transform.position, cupboard.transform.position) <= 30f)
                {
                    if (cupboard.IsAuthed(player))
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }
        
        // Метод для проверки привилегий игрока к постройке
        private bool HasBuildingPrivilege(BasePlayer player, BuildingBlock block)
        {
            if (block == null) return false;
            
            // Проверяем все шкафы привилегий вокруг блока
            var cupboards = UnityEngine.Object.FindObjectsOfType<BuildingPrivlidge>();
            foreach (var cupboard in cupboards)
            {
                if (cupboard == null || cupboard.IsDestroyed) continue;
                
                float distance = Vector3.Distance(block.transform.position, cupboard.transform.position);
                if (distance <= 30f && cupboard.IsAuthed(player))
                {
                    return true;
                }
            }
            
            return false;
        }
    }
}