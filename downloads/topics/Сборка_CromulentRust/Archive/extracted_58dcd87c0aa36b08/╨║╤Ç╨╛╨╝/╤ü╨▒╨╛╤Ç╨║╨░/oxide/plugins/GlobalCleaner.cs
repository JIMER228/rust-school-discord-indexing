
namespace Oxide.Plugins
{
    [Info("GlobalCleaner", "FourTeen", "1.0.3")]
    internal class GlobalCleaner : RustPlugin
    {
        void OnServerInitialized()
        {
            timer.Every(3600, () => { GlobalClean(); }); //3600
        }
        void GlobalClean()
        {
            timer.Once(3000, () => //10min /3000
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    player.ChatMessage("[<color=#64FF33>ОЧИСТКА КАРТЫ</color>] Через <color=#FF9740>10</color> минут карта будет автоматически <color=#64FF33>очищена</color> от объектов, которые не подключены к шкафу.");
                }
                PrintWarning("Через 10 минут карта будет автоматически очищена от объектов, которые не подключены к шкафу.");
            });
            timer.Once(3300, () => //5min /3300
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    player.ChatMessage("[<color=#64FF33>ОЧИСТКА КАРТЫ</color>] Через <color=#FF9740>5</color> минут карта будет автоматически <color=#64FF33>очищена</color> от объектов, которые не подключены к шкафу.");
                }
                PrintWarning("Через 5 минут карта будет автоматически очищена от объектов, которые не подключены к шкафу.");
            });
            timer.Once(3480, () => //2min /3480
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    player.ChatMessage("[<color=#64FF33>ОЧИСТКА КАРТЫ</color>] Через <color=#FF9740>2</color> минуты карта будет автоматически <color=#64FF33>очищена</color> от объектов, которые не подключены к шкафу.");
                }
                PrintWarning("Через 2 минуты карта будет автоматически очищена от объектов, которые не подключены к шкафу.");
            });
            timer.Once(3570, () => //30sec /3570
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    player.ChatMessage("[<color=#64FF33>ОЧИСТКА КАРТЫ</color>] Через <color=#FF9740>30</color> секунд карта будет автоматически <color=#64FF33>очищена</color> от объектов, которые не подключены к шкафу.");
                }
                PrintWarning("Через 30 секунд карта будет автоматически очищена от объектов, которые не подключены к шкафу.");
            });
            timer.Once(3590, () => //10sec /3590
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    player.ChatMessage("[<color=#64FF33>ОЧИСТКА КАРТЫ</color>] Через <color=#FF9740>10</color> секунд карта будет автоматически <color=#64FF33>очищена</color> от объектов, которые не подключены к шкафу.");
                }
                PrintWarning("Через 10 секунд карта будет автоматически очищена от объектов, которые не подключены к шкафу.");
            });
            timer.Once(3597, () => //3sec /3597
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    player.ChatMessage("[<color=#64FF33>ОЧИСТКА КАРТЫ</color>] Через <color=#FF9740>3</color> секунд карта будет автоматически <color=#64FF33>очищена</color> от объектов, которые не подключены к шкафу.");
                }
                PrintWarning("Через 3 секунды карта будет автоматически очищена от объектов, которые не подключены к шкафу.");
            });
            timer.Once(3600, () => //клин 3600
            {
                int i = 0;
                foreach (var bentity in BaseEntity.serverEntities)
                {
                    var entity = bentity as BaseEntity;
                    if (entity != null)
                    {
                        var privilage = entity.GetBuildingPrivilege();
                        if (privilage == null && entity.OwnerID != 0 && !entity.IsDestroyed)
                        {
                            if (entity is BaseVehicle)
                            {
                                var vehicle = entity as BaseVehicle;
                                if (vehicle != null && !vehicle.AnyMounted())
                                {
                                    entity.Kill();
                                    i++;
                                }
                            }
                            else if (entity is BaseVehicleSeat)
                            {
                                var seat = entity as BaseVehicleSeat;
                                if (seat != null && !seat.AnyMounted())
                                {
                                    entity.Kill();
                                    i++;
                                }
                            }
                            else
                            {
                                entity.Kill();
                                i++;
                            }
                        }
                    }
                }
                foreach (var player in BasePlayer.activePlayerList)
                {
                    timer.Once(3, () =>
                    {
                        player.ChatMessage($"[<color=#64FF33>ОЧИСТКА КАРТЫ</color>] Карта автоматически <color=#64FF33>очищена</color> от объектов, которые не подключены к шкафу. \nОчищено обьектов: <color=#FF9740>{i}</color>.");
                        PrintWarning($"Карта автоматически очищена от объектов, которые не подключены к шкафу. Очищено обьектов: {i}");
                    });

                }
            });
        }
    }
}
