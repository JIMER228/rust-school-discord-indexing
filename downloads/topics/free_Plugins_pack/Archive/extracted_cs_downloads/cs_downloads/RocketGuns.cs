using Oxide.Core.Plugins;
using UnityEngine;
using System.Collections.Generic;
using Newtonsoft.Json;
using System;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Rocket Guns", "Amphetaminov", "1.6.3")]
    [Description("Ракеты из любого оружия с ограниченным использованием")]
    public class RocketGuns : RustPlugin
    {
        #region Fields
        private const string PERMISSION_USE = "rocketguns.use";
        private const string PERMISSION_ADMIN = "rocketguns.admin";
        private const string PERMISSION_WEAPON = "rocketguns.weapon";
        private const string PERMISSION_ROCKET = "rocketguns.rocket";
        private const string PERMISSION_SPEED = "rocketguns.speed";
        private const string PERMISSION_COOLDOWN = "rocketguns.cooldown";
        private const string PERMISSION_GETWEAPON = "rocketguns.getweapon";
        private const string PERMISSION_GETLIMITED = "rocketguns.getlimited";

        private const string PERMISSION_ROCKET_BASIC = "rocketguns.rocket.basic";
        private const string PERMISSION_ROCKET_FIRE = "rocketguns.rocket.fire";
        private const string PERMISSION_ROCKET_HV = "rocketguns.rocket.hv";
        private const string PERMISSION_ROCKET_SMOKE = "rocketguns.rocket.smoke";
        private const string PERMISSION_ROCKET_SHARK = "rocketguns.rocket.shark";

        private readonly Dictionary<ulong, bool> playerStates = new Dictionary<ulong, bool>();
        private readonly Dictionary<ulong, DateTime> fireCooldownData = new Dictionary<ulong, DateTime>();
        private readonly Dictionary<ulong, Timer> sharkTimers = new Dictionary<ulong, Timer>();
        private readonly Dictionary<ulong, int> limitedUsesLeft = new Dictionary<ulong, int>();

        private const string SHARK_PREFAB = "assets/rust.ai/agents/fish/simpleshark.prefab";
        private const string WATER_SPLASH = "assets/bundled/prefabs/fx/explosions/water_bomb.prefab";
        private const string AIRBURST_EFFECT = "assets/content/vehicles/mlrs/effects/pfx_airburst.prefab";

        private readonly Dictionary<string, string> availableWeapons = new Dictionary<string, string>
        {
            { "rifle.lr300", "LR-300" },
            { "rifle.ak", "AK-47" },
            { "rifle.bolt", "Болтовка" },
            { "rifle.l96", "L96" },
            { "rifle.m39", "M39" },
            { "smg.mp5", "MP5A4" },
            { "smg.2", "Custom SMG" },
            { "pistol.python", "Python" },
            { "pistol.revolver", "Револьвер" },
            { "pistol.semiauto", "Semi Auto Pistol" },
            { "pistol.m92", "M92" },
            { "rocket.launcher", "Ракетница" },
            { "rifle.semiauto", "SAR" },
            { "shotgun.pump", "Помповый дробовик" },
            { "shotgun.spas12", "SPAS-12" },
            { "shotgun.double", "Двустволка" },
            { "smg.thompson", "Thompson" },
            { "crossbow", "Арбалет" },
            { "bow.hunting", "Лук" }
        };

        private readonly Dictionary<string, string> availableRockets = new Dictionary<string, string>
        {
            { "rocket_basic", "Обычная ракета" },
            { "rocket_fire", "Зажигательная ракета" },
            { "rocket_hv", "Высокоскоростная ракета" },
            { "rocket_smoke", "Дымовая ракета" },
            { "rocket_shark", "Ракета-Акула" }
        };

        private Configuration config;

        class LimitedUseConfig
        {
            [JsonProperty("Количество использований для ограниченного оружия")]
            public int UsesCount = 3;
            
            [JsonProperty("Разрешить повторное получение ограниченного оружия")]
            public bool AllowMultiple = false;
        }

        class Configuration
        {
            [JsonProperty("Скорость ракеты")]
            public float RocketSpeed = 50f;

            [JsonProperty("Задержка между выстрелами (в секундах)")]
            public float FireCooldown = 1f;

            [JsonProperty("Язык по умолчанию (ru/en)")]
            public string DefaultLanguage = "ru";

            [JsonProperty("Тип ракеты")]
            public string RocketType = "rocket_hv";

            [JsonProperty("Оружие (shortname)")]
            public string WeaponShortname = "rifle.lr300";

            [JsonProperty("Настройки ограниченного использования")]
            public LimitedUseConfig LimitedUseSettings = new LimitedUseConfig();
        }
        #endregion

        #region Configuration
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try 
            { 
                config = Config.ReadObject<Configuration>();
                if (config.LimitedUseSettings == null)
                    config.LimitedUseSettings = new LimitedUseConfig();
            }
            catch 
            { 
                LoadDefaultConfig(); 
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(config, true);
        #endregion

        #region Localization
        private string GetMessage(string key, string userId, params object[] args)
        {
            var message = lang.GetMessage(key, this, userId);
            var prefix = lang.GetMessage("PluginPrefix", this, userId);
            return $"{prefix} {string.Format(message, args)}";
        }

        private string GetMessage(string key, string userId)
        {
            var message = lang.GetMessage(key, this, userId);
            var prefix = lang.GetMessage("PluginPrefix", this, userId);
            return $"{prefix} {message}";
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPermission"] = "У вас нет прав для использования этой команды!",
                ["HelpText"] = "<color=#87ceeb>Доступные команды:</color>\n/rocketguns on - Включить режим ракет\n/rocketguns off - Выключить режим ракет\n/rocketguns info - Показать текущие настройки\n/rocketguns weapon [тип] - Изменить оружие\n/rocketguns rocket [тип] - Изменить тип ракеты\n/rocketguns speed [значение] - Изменить скорость ракеты\n/rocketguns cooldown [секунды] - Изменить задержку между выстрелами\n/rocketguns weapons - Показать список доступного оружия\n/rocketguns reset - Сбросить настройки на стандартные\n/getweapon - Получить выбранное оружие\n/getlimited - Получить оружие с ограниченным использованием",
                ["ShortCommands"] = "<color=#87ceeb>Короткие команды:</color>\n/rg on - Включить режим\n/rg off - Выключить режим\n/rg info - Настройки\n/rg weapon [тип] - Изменить оружие\n/rg rocket [тип] - Изменить тип ракеты\n/rg speed [значение] - Изменить скорость\n/rg cooldown [секунды] - Изменить задержку\n/rg weapons - Список оружия\n/rg reset - Сбросить настройки",
                ["RocketModeEnabled"] = "Режим ракет включен!",
                ["RocketModeDisabled"] = "Режим ракет выключен!",
                ["InvalidWeapon"] = "Неверное оружие! Доступные варианты: {0}",
                ["InvalidRocket"] = "Неверный тип ракеты! Доступные варианты: {0}",
                ["NoRocketsAvailable"] = "У вас нет доступных типов ракет!",
                ["InvalidNumber"] = "Пожалуйста, введите корректное число!",
                ["WeaponChanged"] = "Оружие изменено на: {0}",
                ["RocketChanged"] = "Тип ракеты изменен на: {0}",
                ["SpeedChanged"] = "Скорость изменена на: {0}",
                ["CooldownChanged"] = "Задержка изменена на: {0} секунд",
                ["AvailableWeapons"] = "Доступное оружие: {0}",
                ["AvailableRockets"] = "Доступные типы ракет: {0}",
                ["CurrentSettings"] = "Текущие настройки:\nСкорость: {0}\nЗадержка: {1} сек\nТип ракеты: {2}\nОружие: {3}",
                ["SettingsReset"] = "Настройки сброшены до значений по умолчанию!",
                ["WeaponReceived"] = "Вы получили оружие: {0}",
                ["LimitedReceived"] = "Вы получили оружие {0} с {1} использованиями режима ракет!",
                ["LimitedUsesLeft"] = "Осталось использований: {0}",
                ["LimitedUsesExhausted"] = "Все использования израсходованы! Режим ракет отключен.",
                ["LimitedAlreadyUsed"] = "Вы уже использовали свое ограниченное оружие!",
                ["PluginPrefix"] = "<color=#ffd700>[Rocket Guns]</color>",
                ["LimitedPrefix"] = "<color=#ff0000>[Ограниченное оружие]</color>"
            }, this, "ru");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPermission"] = "You don't have permission to use this command!",
                ["HelpText"] = "<color=#87ceeb>Available commands:</color>\n/rocketguns on - Enable rocket mode\n/rocketguns off - Disable rocket mode\n/rocketguns info - Show current settings\n/rocketguns weapon [type] - Change weapon\n/rocketguns rocket [type] - Change rocket type\n/rocketguns speed [value] - Change rocket speed\n/rocketguns cooldown [seconds] - Change fire cooldown\n/rocketguns weapons - Show available weapons\n/rocketguns reset - Reset settings to default\n/getweapon - Get selected weapon\n/getlimited - Get weapon with limited uses",
                ["ShortCommands"] = "<color=#87ceeb>Short commands:</color>\n/rg on - Enable mode\n/rg off - Disable mode\n/rg info - Settings\n/rg weapon [type] - Change weapon\n/rg rocket [type] - Change rocket type\n/rg speed [value] - Change speed\n/rg cooldown [seconds] - Change cooldown\n/rg weapons - List weapons\n/rg reset - Reset settings",
                ["RocketModeEnabled"] = "Rocket mode enabled!",
                ["RocketModeDisabled"] = "Rocket mode disabled!",
                ["InvalidWeapon"] = "Invalid weapon! Available options: {0}",
                ["InvalidRocket"] = "Invalid rocket type! Available options: {0}",
                ["NoRocketsAvailable"] = "You don't have any available rocket types!",
                ["InvalidNumber"] = "Please enter a valid number!",
                ["WeaponChanged"] = "Weapon changed to: {0}",
                ["RocketChanged"] = "Rocket type changed to: {0}",
                ["SpeedChanged"] = "Speed changed to: {0}",
                ["CooldownChanged"] = "Cooldown changed to: {0} seconds",
                ["AvailableWeapons"] = "Available weapons: {0}",
                ["AvailableRockets"] = "Available rocket types: {0}",
                ["CurrentSettings"] = "Current settings:\nSpeed: {0}\nCooldown: {1} sec\nRocket type: {2}\nWeapon: {3}",
                ["SettingsReset"] = "Settings have been reset to default!",
                ["WeaponReceived"] = "You received weapon: {0}",
                ["LimitedReceived"] = "You received {0} with {1} rocket mode uses!",
                ["LimitedUsesLeft"] = "Uses left: {0}",
                ["LimitedUsesExhausted"] = "All uses exhausted! Rocket mode disabled.",
                ["LimitedAlreadyUsed"] = "You've already used your limited weapon!",
                ["PluginPrefix"] = "<color=#ffd700>[Rocket Guns]</color>",
                ["LimitedPrefix"] = "<color=#ff0000>[Limited Weapon]</color>"
            }, this, "en");
        }
        #endregion

        #region Hooks
        private void Init()
        {
            permission.RegisterPermission(PERMISSION_USE, this);
            permission.RegisterPermission(PERMISSION_ADMIN, this);
            permission.RegisterPermission(PERMISSION_WEAPON, this);
            permission.RegisterPermission(PERMISSION_ROCKET, this);
            permission.RegisterPermission(PERMISSION_SPEED, this);
            permission.RegisterPermission(PERMISSION_COOLDOWN, this);
            permission.RegisterPermission(PERMISSION_GETWEAPON, this);
            permission.RegisterPermission(PERMISSION_GETLIMITED, this);
            
            permission.RegisterPermission(PERMISSION_ROCKET_BASIC, this);
            permission.RegisterPermission(PERMISSION_ROCKET_FIRE, this);
            permission.RegisterPermission(PERMISSION_ROCKET_HV, this);
            permission.RegisterPermission(PERMISSION_ROCKET_SMOKE, this);
            permission.RegisterPermission(PERMISSION_ROCKET_SHARK, this);

            LoadConfig();
        }

        private void Unload()
        {
            foreach(var timer in sharkTimers.Values)
                timer?.Destroy();
                
            playerStates.Clear();
            fireCooldownData.Clear();
            sharkTimers.Clear();
            limitedUsesLeft.Clear();
        }

        void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (player == null || input == null) return;

            if (limitedUsesLeft.ContainsKey(player.userID))
            {
                if (!permission.UserHasPermission(player.UserIDString, PERMISSION_GETLIMITED))
                    return;

                if (!playerStates.ContainsKey(player.userID) || !playerStates[player.userID])
                    return;

                if (input.IsDown(BUTTON.FIRE_PRIMARY))
                {
                    // Отменяем стандартное поведение оружия (расход боеприпасов)
                    input.WasJustPressed(BUTTON.FIRE_PRIMARY);

                    if (CheckisCooldown(player.userID))
                        return;

                    Item activeItem = player.GetActiveItem();
                    if (activeItem == null || activeItem.info.shortname != config.WeaponShortname)
                        return;

                    limitedUsesLeft[player.userID]--;

                    if (limitedUsesLeft[player.userID] <= 0)
                    {
                        playerStates[player.userID] = false;
                        limitedUsesLeft.Remove(player.userID);
                        permission.RevokeUserPermission(player.UserIDString, PERMISSION_GETLIMITED);
                        SendReply(player, GetMessage("LimitedUsesExhausted", player.UserIDString));
                    }
                    else
                    {
                        SendReply(player, GetMessage("LimitedUsesLeft", player.UserIDString, limitedUsesLeft[player.userID]));
                    }

                    FireRocket(player);
                }
            }
            else
            {
                if (!permission.UserHasPermission(player.UserIDString, PERMISSION_USE))
                    return;

                if (!playerStates.ContainsKey(player.userID) || !playerStates[player.userID])
                    return;

                if (input.IsDown(BUTTON.FIRE_PRIMARY))
                {
                    // Отменяем стандартное поведение оружия (расход боеприпасов)
                    input.WasJustPressed(BUTTON.FIRE_PRIMARY);

                    if (CheckisCooldown(player.userID))
                        return;

                    Item activeItem = player.GetActiveItem();
                    if (activeItem == null || activeItem.info.shortname != config.WeaponShortname)
                        return;

                    FireRocket(player);
                }
            }
        }

        private void FireRocket(BasePlayer player)
        {
            Vector3 position = player.eyes.position;

            if (config.RocketType == "rocket_shark")
            {
                CreateSharkRocket(player, position);
            }
            else
            {
                var rocket = GameManager.server.CreateEntity($"assets/prefabs/ammo/rocket/{config.RocketType}.prefab", 
                    position + player.eyes.HeadForward(), player.transform.rotation);
                
                if (rocket == null) return;

                rocket.creatorEntity = player;
                rocket.OwnerID = player.userID;

                var serverProjectile = rocket.GetComponent<ServerProjectile>();
                if (serverProjectile != null)
                {
                    serverProjectile.InitializeVelocity(Quaternion.Euler(player.serverInput.current.aimAngles) * rocket.transform.forward * config.RocketSpeed);
                }

                rocket.Spawn();

                Effect.server.Run("assets/bundled/prefabs/fx/weapons/rocket_launch.prefab", position);
                Effect.server.Run("assets/prefabs/weapons/rocket_launcher/effects/pfx_rocket_launch.prefab", position);
            }

            if (!fireCooldownData.ContainsKey(player.userID))
            {
                fireCooldownData.Add(player.userID, DateTime.Now.AddSeconds(config.FireCooldown));
            }
            else
            {
                fireCooldownData[player.userID] = DateTime.Now.AddSeconds(config.FireCooldown);
            }
        }

        private void CreateSharkRocket(BasePlayer player, Vector3 position)
        {
            var rocket = GameManager.server.CreateEntity("assets/prefabs/ammo/rocket/rocket_basic.prefab",
                position + player.eyes.HeadForward(), player.transform.rotation);

            if (rocket == null) return;

            rocket.creatorEntity = player;
            rocket.OwnerID = player.userID;
            rocket._name = "SLRocket";
            rocket.limitNetworking = true;

            var serverProjectile = rocket.GetComponent<ServerProjectile>();
            if (serverProjectile != null)
            {
                serverProjectile.InitializeVelocity(Quaternion.Euler(player.serverInput.current.aimAngles) * rocket.transform.forward * config.RocketSpeed);
            }

            rocket.Spawn();

            BaseEntity entity = GameManager.server.CreateEntity(SHARK_PREFAB, rocket.transform.position);
            if (entity == null) return;

            var shark = entity as SimpleShark;
            if (shark == null) return;

            shark.enabled = false;
            shark.limitNetworking = true;
            shark._name = "launchershark";
            shark.transform.position = rocket.transform.position;
            shark.transform.rotation = rocket.transform.rotation;
            entity.Spawn();

            timer.Once(0.05f, () =>
            {
                if (shark == null || shark.IsDestroyed) return;
                Effect.server.Run(AIRBURST_EFFECT, shark.transform.position, Vector3.forward, null, false);
            });

            shark.limitNetworking = false;

            if (sharkTimers.ContainsKey(player.userID))
            {
                sharkTimers[player.userID]?.Destroy();
                sharkTimers.Remove(player.userID);
            }

            Timer sharkTimer = timer.Every(0.01f, () =>
            {
                if (shark == null || !shark.IsAlive() || rocket == null || rocket.IsDestroyed)
                {
                    if (sharkTimers.ContainsKey(player.userID))
                    {
                        sharkTimers[player.userID]?.Destroy();
                        sharkTimers.Remove(player.userID);
                    }
                    return;
                }

                if (shark.transform != null && rocket.transform != null)
                {
                    shark.transform.position = rocket.transform.position;
                    shark.transform.rotation = rocket.transform.rotation;
                }
            });

            sharkTimers[player.userID] = sharkTimer;
        }

        private bool CheckisCooldown(ulong userID)
        {
            if (!fireCooldownData.ContainsKey(userID)) return false;
            if (fireCooldownData[userID] > DateTime.Now) return true;
            return false;
        }
        #endregion

        #region Commands
        [ChatCommand("rocketguns")]
        void RocketGunsCommand(BasePlayer player, string command, string[] args)
        {
            HandleCommand(player, args, false);
        }

        [ChatCommand("rg")]
        void RocketGunsShortCommand(BasePlayer player, string command, string[] args)
        {
            HandleCommand(player, args, true);
        }

        [ChatCommand("getlimited")]
        void GetLimitedCommand(BasePlayer player, string command, string[] args)
        {
            if (player == null)
            {
                if (args.Length < 1)
                {
                    Puts("Использование: getlimited <имя/steamid> [количество использований]");
                    return;
                }

                var target = covalence.Players.FindPlayer(args[0]);
                if (target == null)
                {
                    Puts($"Игрок {args[0]} не найден!");
                    return;
                }

                var basePlayer = target.Object as BasePlayer;
                if (basePlayer == null)
                {
                    Puts("Не удалось получить BasePlayer!");
                    return;
                }

                float uses = config.LimitedUseSettings.UsesCount;
                if (args.Length > 1 && float.TryParse(args[1], out float customUses))
                {
                    uses = customUses;
                }

                GiveLimitedWeapon(basePlayer, (int)uses);
                Puts($"Выдано ограниченное оружие игроку {target.Name} с {uses} использованиями");
                return;
            }

            if (!permission.UserHasPermission(player.UserIDString, PERMISSION_GETLIMITED))
            {
                SendReply(player, GetMessage("NoPermission", player.UserIDString));
                return;
            }

            if (!config.LimitedUseSettings.AllowMultiple && limitedUsesLeft.ContainsKey(player.userID))
            {
                SendReply(player, GetMessage("LimitedAlreadyUsed", player.UserIDString));
                return;
            }

            GiveLimitedWeapon(player, config.LimitedUseSettings.UsesCount);
            SendReply(player, GetMessage("LimitedReceived", player.UserIDString, 
                config.WeaponShortname, config.LimitedUseSettings.UsesCount));
        }

        [ConsoleCommand("getlimited")]
        void ConsoleGetLimited(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null) return;

            if (arg.Args == null || arg.Args.Length < 1)
            {
                Puts("Использование: getlimited <имя/steamid> [количество использований]");
                return;
            }

            var target = covalence.Players.FindPlayer(arg.Args[0]);
            if (target == null)
            {
                Puts($"Игрок {arg.Args[0]} не найден!");
                return;
            }

            var basePlayer = target.Object as BasePlayer;
            if (basePlayer == null)
            {
                Puts("Не удалось получить BasePlayer!");
                return;
            }

            float uses = config.LimitedUseSettings.UsesCount;
            if (arg.Args.Length > 1 && float.TryParse(arg.Args[1], out float customUses))
            {
                uses = customUses;
            }

            GiveLimitedWeapon(basePlayer, (int)uses);
            Puts($"Выдано ограниченное оружие игроку {target.Name} с {uses} использованиями");
        }

        private void GiveLimitedWeapon(BasePlayer player, int uses)
        {
            var definition = ItemManager.FindItemDefinition(config.WeaponShortname);
            if (definition != null)
            {
                Item item = ItemManager.Create(definition, 1);
                if (item != null)
                {
                    var baseProjectile = item.GetHeldEntity() as BaseProjectile;
                    if (baseProjectile != null)
                    {
                        baseProjectile.primaryMagazine.contents = baseProjectile.primaryMagazine.capacity;
                    }
                    player.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
                }
            }

            limitedUsesLeft[player.userID] = uses;
            playerStates[player.userID] = true;
            permission.GrantUserPermission(player.UserIDString, PERMISSION_GETLIMITED, this);
        }

        [ChatCommand("getweapon")]
        void GetWeaponCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PERMISSION_GETWEAPON))
            {
                SendReply(player, GetMessage("NoPermission", player.UserIDString));
                return;
            }

            var definition = ItemManager.FindItemDefinition(config.WeaponShortname);
            if (definition != null)
            {
                Item item = ItemManager.Create(definition, 1);
                if (item != null)
                {
                    var baseProjectile = item.GetHeldEntity() as BaseProjectile;
                    if (baseProjectile != null)
                    {
                        baseProjectile.primaryMagazine.contents = baseProjectile.primaryMagazine.capacity;
                    }
                    player.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
                    SendReply(player, GetMessage("WeaponReceived", player.UserIDString, config.WeaponShortname));
                }
            }
        }

        private void HandleCommand(BasePlayer player, string[] args, bool shortFormat)
        {
            if (!HasUsePermission(player) && !permission.UserHasPermission(player.UserIDString, PERMISSION_GETLIMITED))
            {
                SendReply(player, GetMessage("NoPermission", player.UserIDString));
                return;
            }

            if (args.Length == 0)
            {
                SendReply(player, GetMessage(shortFormat ? "ShortCommands" : "HelpText", player.UserIDString));
                return;
            }

            switch (args[0].ToLower())
            {
                case "on":
                    if (permission.UserHasPermission(player.UserIDString, PERMISSION_GETLIMITED))
                    {
                        playerStates[player.userID] = true;
                        SendReply(player, GetMessage("RocketModeEnabled", player.UserIDString));
                        return;
                    }
                    
                    if (!HasUsePermission(player))
                    {
                        SendReply(player, GetMessage("NoPermission", player.UserIDString));
                        return;
                    }
                    
                    playerStates[player.userID] = true;
                    SendReply(player, GetMessage("RocketModeEnabled", player.UserIDString));
                    break;

                case "off":
                    playerStates[player.userID] = false;
                    SendReply(player, GetMessage("RocketModeDisabled", player.UserIDString));
                    break;

                case "info":
                    SendReply(player, GetMessage("CurrentSettings", player.UserIDString, 
                        config.RocketSpeed, config.FireCooldown, config.RocketType, config.WeaponShortname));
                    break;

                case "weapon":
                    if (!HasPermission(player, PERMISSION_WEAPON))
                    {
                        SendReply(player, GetMessage("NoPermission", player.UserIDString));
                        return;
                    }

                    if (args.Length < 2)
                    {
                        SendReply(player, GetMessage("InvalidWeapon", player.UserIDString, string.Join(", ", availableWeapons.Keys)));
                        return;
                    }

                    string newWeapon = args[1].ToLower();
                    if (!availableWeapons.ContainsKey(newWeapon))
                    {
                        SendReply(player, GetMessage("InvalidWeapon", player.UserIDString, string.Join(", ", availableWeapons.Keys)));
                        return;
                    }

                    config.WeaponShortname = newWeapon;
                    SaveConfig();
                    SendReply(player, GetMessage("WeaponChanged", player.UserIDString, newWeapon));
                    break;

                case "rocket":
                    if (args.Length < 2)
                    {
                        SendReply(player, GetMessage("InvalidRocket", player.UserIDString, string.Join(", ", availableRockets.Keys)));
                        return;
                    }

                    string newRocket = args[1].ToLower();
                    if (!availableRockets.ContainsKey(newRocket))
                    {
                        SendReply(player, GetMessage("InvalidRocket", player.UserIDString, string.Join(", ", availableRockets.Keys)));
                        return;
                    }

                    config.RocketType = newRocket;
                    SaveConfig();
                    SendReply(player, GetMessage("RocketChanged", player.UserIDString, newRocket));
                    break;

                case "speed":
                    if (args.Length < 2 || !float.TryParse(args[1], out float newSpeed))
                    {
                        SendReply(player, GetMessage("InvalidNumber", player.UserIDString));
                        return;
                    }
                    config.RocketSpeed = newSpeed;
                    SaveConfig();
                    SendReply(player, GetMessage("SpeedChanged", player.UserIDString, newSpeed));
                    break;

                case "cooldown":
                    if (args.Length < 2 || !float.TryParse(args[1], out float newCooldown))
                    {
                        SendReply(player, GetMessage("InvalidNumber", player.UserIDString));
                        return;
                    }
                    config.FireCooldown = newCooldown;
                    SaveConfig();
                    SendReply(player, GetMessage("CooldownChanged", player.UserIDString, newCooldown));
                    break;

                case "weapons":
                    SendReply(player, GetMessage("AvailableWeapons", player.UserIDString, string.Join(", ", availableWeapons.Keys)));
                    break;

                case "reset":
                    if (!HasPermission(player, PERMISSION_ADMIN))
                    {
                        SendReply(player, GetMessage("NoPermission", player.UserIDString));
                        return;
                    }
                    config = new Configuration();
                    SaveConfig();
                    SendReply(player, GetMessage("SettingsReset", player.UserIDString));
                    break;

                default:
                    SendReply(player, GetMessage(shortFormat ? "ShortCommands" : "HelpText", player.UserIDString));
                    break;
            }
        }
        #endregion

        #region Helpers
        private bool HasPermission(BasePlayer player, string permissionName)
        {
            if (player == null) return false;
            return permission.UserHasPermission(player.UserIDString, PERMISSION_ADMIN) || 
                   permission.UserHasPermission(player.UserIDString, permissionName);
        }

        private bool HasUsePermission(BasePlayer player)
        {
            if (player == null) return false;
            return permission.UserHasPermission(player.UserIDString, PERMISSION_ADMIN) || 
                   permission.UserHasPermission(player.UserIDString, PERMISSION_USE);
        }
        #endregion
    }
}