using Oxide.Core;
using Oxide.Core.Plugins;
using System;
using UnityEngine;
using Oxide.Core.Configuration;

namespace Oxide.Plugins
{
    [Info("EventBox", "YourName", "4.0.0")]
    [Description("Spawns a loot box in front of admin with rewards")]
    class EventBox : RustPlugin
    {
        // Префаб ящика
        private const string BoxPrefab = "assets/prefabs/deployable/large wood storage/box.wooden.large.prefab";
        
        private Configuration config;
        
        private class Configuration
        {
            public string RewardCommand { get; set; }
            public float SpawnDistance { get; set; } // Дистанция перед игроком
        }

        private BaseEntity eventBox;

        private void Init()
        {
            permission.RegisterPermission("eventbox.admin", this);
            
            config = new Configuration
            {
                RewardCommand = "giveplayer {playerId} rifle.ak 1",
                SpawnDistance = 2f
            };
            
            LoadConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try 
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) throw new Exception();
                SaveConfig();
            }
            catch
            {
                LoadDefaultConfig();
                PrintError("Конфигурация повреждена, загружена по умолчанию");
            }
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        [ChatCommand("eventbox")]
        private void CmdSpawnEventBox(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "eventbox.admin"))
            {
                player.ChatMessage("<color=red>У вас нет прав на эту команду!</color>");
                return;
            }
            
            try
            {
                SpawnBoxInFrontOfPlayer(player);
                player.ChatMessage("<color=green>Ящик с наградой создан перед вами!</color>");
            }
            catch (Exception ex)
            {
                player.ChatMessage("<color=red>Ошибка при создании ящика!</color>");
                PrintError($"Ошибка: {ex}");
            }
        }

        private void SpawnBoxInFrontOfPlayer(BasePlayer player)
        {
            DestroyOldBox();

            // Позиция перед игроком
            Vector3 spawnPos = player.transform.position + player.transform.forward * config.SpawnDistance;
            spawnPos.y = TerrainMeta.HeightMap.GetHeight(spawnPos) + 0.5f;

            // Создаем ящик
            eventBox = GameManager.server.CreateEntity(BoxPrefab, spawnPos);
            if (eventBox == null)
            {
                throw new Exception("Не удалось создать ящик!");
            }
            
            eventBox.Spawn();
            Puts($"Ящик создан перед игроком {player.displayName} на позиции: {spawnPos}");
        }

        private void DestroyOldBox()
        {
            if (eventBox != null && !eventBox.IsDestroyed)
            {
                eventBox.Kill();
                Puts("Старый ящик уничтожен");
            }
        }

        private void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (entity == eventBox)
            {
                try
                {
                    string cmd = config.RewardCommand.Replace("{playerId}", player.UserIDString);
                    var parts = cmd.Split(' ');
                    if (parts.Length > 1)
                    {
                        player.SendConsoleCommand(parts[0], parts[1..]);
                    }
                    
                    player.ChatMessage($"<color=#00ff00>🎉 Вы получили награду!</color>");
                    Puts($"Игрок {player.displayName} получил награду: {cmd}");
                    DestroyOldBox();
                }
                catch (Exception ex)
                {
                    PrintError($"Ошибка при выдаче награды: {ex}");
                }
            }
        }
    }
}