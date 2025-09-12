
/* Documentation =>
 * Commands:
 * quarry.give «SteamID» «SkinID» => give to player the quarry with specified skin from config
 * quarry.giveall «SteamID» => give all quarries from config to player
 * quarry.giveme => give all quarries from config to self
 * Permissions:
 * aquarry.give => allows to use commands from in-game console
 * 
 * Copyright © 2022 AvG Лаймон#0680 (alias.dev@yandex.ru) */

using System.Collections.Generic;
using Oxide.Core;
using Newtonsoft.Json;
using UnityEngine;
using System.Linq;
using System;

namespace Oxide.Plugins
{
    [Info("aQuarry", "AvG Лаймон", "1.0.2")]
    class aQuarry : RustPlugin
    {
        #region [Initialization]
        private const bool Rus = false;
        private const string giveperm = "aquarry.give";
        private void OnServerInitialized()
        {
            if (!permission.PermissionExists(giveperm))
                permission.RegisterPermission(giveperm, this);
            NextTick(() =>
            {
                foreach (MiningQuarry entity in BaseNetworkable.serverEntities.OfType<MiningQuarry>())
                    UpdateQuarry(entity);
            });
        }
        #endregion
        #region [Data]
        private Dictionary<ulong, CashData> data = new Dictionary<ulong, CashData>();
        private class CashData
        {
            internal int quarries = 0;
            internal int jacks = 0;
        }
        #endregion
        #region [Configuration]
        private static Configuration cfg;
        private static Configuration DefaultConfig()
        {
            return new Configuration
            {
                Quarries = new Dictionary<ulong, QuarrySetting>
                {
                    [2896961730] = new QuarrySetting
                    {
                        name = "Personal Quarry Lvl 1",
                        resources = new List<ResourceSettings>
                        {
                            new ResourceSettings
                            {
                                shortname = "stones",
                                workNeeded = 0.3f
                            },
                            new ResourceSettings
                            {
                                shortname = "metal.ore",
                                workNeeded = 5f
                            }
                        }
                    },
                    [2896961934] = new QuarrySetting
                    {
                        name = "Personal Quarry Lvl 2",
                        resources = new List<ResourceSettings>
                        {
                            new ResourceSettings
                            {
                                shortname = "stones",
                                workNeeded = 0.3f
                            },
                            new ResourceSettings
                            {
                                shortname = "metal.ore",
                                workNeeded = 5f
                            },
                            new ResourceSettings
                            {
                                shortname = "sulfur.ore",
                                workNeeded = 7.5f
                            }
                        }
                    },
                    [2896962234] = new QuarrySetting
                    {
                        name = "Personal Quarry Lvl 3",
                        resources = new List<ResourceSettings>
                        {
                            new ResourceSettings
                            {
                                shortname = "stones",
                                workNeeded = 0.3f
                            },
                            new ResourceSettings
                            {
                                shortname = "metal.ore",
                                workNeeded = 5f
                            },
                            new ResourceSettings
                            {
                                shortname = "sulfur.ore",
                                workNeeded = 7.5f
                            },
                            new ResourceSettings
                            {
                                shortname = "hq.metal.ore",
                                workNeeded = 75f
                            }
                        }
                    },
                    [2896959659] = new QuarrySetting
                    {
                        name = "Personal PumpJack",
                        quarry = false,
                        resources = new List<ResourceSettings>
                        {
                            new ResourceSettings
                            {
                                shortname = "crude.oil",
                                workNeeded = 16.6f
                            },
                            new ResourceSettings
                            {
                                shortname = "lowgradefuel",
                                workNeeded = 5.8f
                            }
                        }
                    }
                }
            };
        }
        protected override void LoadDefaultConfig()
        {
            PrintWarning(Rus ? "Создание базовой конфигурации..." : "Creating a default config...");
            cfg = DefaultConfig();
            cfg.PluginVersion = Version;
            SaveConfig();
            PrintWarning(Rus ? "Создание базовой конфигурации завершено!" : "Default config has been created!");
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            cfg = Config.ReadObject<Configuration>();
            if (cfg.PluginVersion < Version) UpdateConfigValues();
            SaveConfig();
        }
        private void UpdateConfigValues()
        {
            PrintWarning(Rus ? "Обнаружено обновление плагина! Автоматическое обновление конфигурации..." : "Config update detected! Updating config values...");
            if (cfg.PluginVersion < new VersionNumber(1, 0, 2))
            {
                foreach (QuarrySetting x in cfg.Quarries.Values)
                {
                    x.Protection.Toggle = x.OwnerToggle;
                    x.Protection.Loot = x.OwnerToggle;
                    x.Protection.Team = x.TeamToggle;
                    x.SubPrefab.Custom = x.ChangePrefab;
                    x.SubPrefab.prefab = x.prefab;
                    x.SubPrefab.distance1 = x.distance;
                    x.Fuel.Custom = x.changeFuel;
                    x.Fuel.ShortName = x.fuelShortname;
                    x.Fuel.amount = x.fuel;
                    x.Production.processRate = x.processRate;
                    x.Production.WorkPerFuel = x.workPerFuel;
                    x.Production.WorkToAdd = x.workToAdd;
                    x.Extraction.Custom = x.changeResourse;
                    x.Extraction.resources = new List<ResourceSettings>();
                    foreach (ResourceSettings res in x.resources)
                    {
                        x.Extraction.resources.Add(new ResourceSettings
                        {
                            shortname = res.shortname,
                            workNeeded = res.workNeeded,
                            WorkNeeded = res.workNeeded,
                        });
                    }
                }
            }
            cfg.PluginVersion = Version;
            PrintWarning(Rus ? "Обновление конфигурации успешно!" : "Config update completed!");
        }
        protected override void SaveConfig() => Config.WriteObject(cfg);
        private class Configuration
        {
            [JsonProperty(Rus ? "Оптимизация" : "Optimization")] internal Optimization optimization = new Optimization();
            [JsonProperty(Rus ? "SteamID аватарки для чата" : "Avatar SteamID for chat")] internal ulong chatid = 0;
            [JsonProperty(Rus ? "Логировать полученные игроками карьеры?" : "Log the quarries received by players?")] internal bool log = false;
            [JsonProperty(Rus ? "Максимальное количество карьеров на одного игрока" : "Maximum amount of quarries per player")] internal int quarries = 3;
            [JsonProperty(Rus ? "Максимальное количество нефтекачек на одного игрока" : "Maximum amount of pumpjacks per player")] internal int jacks = 1;
            [JsonProperty(Rus ? "Список кастомных карьеров (SkinID карьера : настройка)" : "List of custom quarries (skinid : settings)")] internal Dictionary<ulong, QuarrySetting> Quarries = new Dictionary<ulong, QuarrySetting>();
            [JsonProperty(Rus ? "Версия плагина" : "Plugin Version")] internal VersionNumber PluginVersion = new VersionNumber();
        }
        private class Optimization
        {
            [JsonProperty(Rus ? "Отключить функционал запрета включения/выключения карьеров другими игроками" : "Disable toggle protection from other players")] internal bool Toggle = false;
            [JsonProperty(Rus ? "Отключить функционал запрета лутания карьеров другими игроками" : "Disable loot protection from other players")] internal bool Loot = false;
            [JsonProperty(Rus ? "Отключить функционал замены топлива и потребляемого количества" : "Disable custom fuel and it consume amount")] internal bool Fuel = false;
            [JsonProperty(Rus ? "Отключить взаимодействие с плагином RemoverTool" : "Disable interactions with RemoverTool plugin")] internal bool Remove = false;
        }
        private class QuarrySetting
        {
            [JsonProperty(Rus ? "Только владелец карьера может его включать/выключать?" : "Only owner can toggle this quarry?")] internal bool OwnerToggle = false;
            [JsonProperty(Rus ? "Включая команду владельца?" : "Include his team?")] internal bool TeamToggle = false;
            [JsonProperty(Rus ? "Это карьер (true) или нефтекачка (false) ?" : "This is a quarry (true) or pumpjack (false) ?")] internal bool quarry = true;
            [JsonProperty(Rus ? "Название карьера" : "Quarry custom name")] internal string name = "Custom Name";
            [JsonProperty(Rus ? "Защита от других игроков" : "Protection from other players")] internal QProtection Protection = new QProtection();
            [JsonProperty(Rus ? "Подмена префаба" : "Prefab substitution")] internal QPrefab SubPrefab = new QPrefab();
            [JsonProperty(Rus ? "Топливо" : "Fuel")] internal QFuel Fuel = new QFuel();
            [JsonProperty(Rus ? "Производство" : "Production")] internal QProduction Production = new QProduction();
            [JsonProperty(Rus ? "Добыча" : "Extraction")] internal QExtraction Extraction = new QExtraction();
            [JsonProperty(Rus ? "Использовать подмену префаба?" : "Use prefab substitution?")] internal bool ChangePrefab = true;
            [JsonProperty(Rus ? "ShortName предмета из которого создаем карьер" : "ShortName of the item from which we create a quarry")] internal string prefab = "furnace.large";
            [JsonProperty(Rus ? "Минимальное расстояние от игрока до карьера (при установке)" : "Minimum distance from the player to the quarry (when installed)")] internal float distance = 3f;
            [JsonProperty(Rus ? "Заменить предмет (топливо) на котором работает карьер?" : "Change the item (fuel) on which the quarry is working?")] internal bool changeFuel = false;
            [JsonProperty(Rus ? "Новый предмет используемый в качестве топлива (шортнейм)" : "A new item used as fuel (shortname)")] internal string fuelShortname = "lowgradefuel";
            [JsonProperty(Rus ? "Потребление топлива" : "Amount of fuel to consume")] internal int fuel = 1;
            [JsonProperty(Rus ? "Заменять стандартное производство ресурсов?" : "Replace standard resource production?")] internal bool changeResourse = false;
            [JsonProperty(Rus ? "Интервал производства ресурсов (секунды)" : "Production interval (seconds)")] internal float processRate = 5;
            [JsonProperty(Rus ? "Добавляемый запас единиц прогресса карьера при поглощении топлива" : "Add progress points to reserve when consume fuel")] internal float workPerFuel = 1000;
            [JsonProperty(Rus ? "Выполняемый прогресс при производстве" : "Progress points per production tick")] internal float workToAdd = 40;
            [JsonProperty(Rus ? "Настройка добываемых ресурсов" : "Resource extraction settings")] internal List<ResourceSettings> resources = new List<ResourceSettings>();
        }
        private class QProtection
        {
            [JsonProperty(Rus ? "Только владелец карьера может его включать/выключать?" : "Only owner can toggle?")] internal bool Toggle = false;
            [JsonProperty(Rus ? "Только владелец карьера может его лутать?" : "Only owner can loot?")] internal bool Loot = false;
            [JsonProperty(Rus ? "Включая команду владельца?" : "Include his team?")] internal bool Team = false;
        }
        private class QPrefab
        {
            [JsonProperty(Rus ? "Использовать подмену префаба?" : "Use prefab substitution?")] internal bool Custom = true;
            [JsonProperty(Rus ? "ShortName предмета из которого создаем карьер" : "ShortName of the item from which we create a quarry")] internal string prefab = "furnace.large";
            [JsonProperty(Rus ? "Минимальное расстояние от игрока до карьера (при установке)" : "Minimum distance from player to quarry (when installed)")] internal float distance1 = 3f;
            //[JsonProperty(Rus ? "Минимальное расстояние от карьера до строений (при установке)" : "Minimum distance from quarry to constructions (when installed)")] internal float distance2 = 3f;
        }
        private class QFuel
        {
            [JsonProperty(Rus ? "Заменить стандартное топливо?" : "Change standart fuel?")] internal bool Custom = false;
            [JsonProperty(Rus ? "Используемое топливо (шортнейм)" : "Fuel used (shortname)")] internal string ShortName = "lowgradefuel";
            [JsonProperty(Rus ? "Потребление топлива" : "Amount of fuel to consume")] internal int amount = 1;
        }
        private class QProduction
        {
            [JsonProperty(Rus ? "Заменять стандартное производство?" : "Replace standard production?")] internal bool Custom = true;
            [JsonProperty(Rus ? "Интервал производства ресурсов (в секундах)" : "Production interval (in seconds)")] internal float processRate = 5;
            [JsonProperty(Rus ? "«Работа» на одном поглощении топлива (в очках) (aka «WorkPerFuel»)" : "«Work» per consume fuel (in points) (aka «WorkPerFuel»)")] internal float WorkPerFuel = 1000;
            [JsonProperty(Rus ? "Выполняемая «работа» при производстве (в очках) (aka «WorkToAdd»)" : "Progress «work» per production (in points) (aka «WorkToAdd»)")] internal float WorkToAdd = 40;
        }
        private class QExtraction
        {
            [JsonProperty(Rus ? "Заменять стандартные ресурсы?" : "Replace standard resources?")] internal bool Custom = true;
            [JsonProperty(Rus ? "Добываемые ресурсы" : "Gathering resources")] internal List<ResourceSettings> resources;
        }
        private class ResourceSettings
        {
            [JsonProperty("ShortName")] internal string shortname = "stones";
            [JsonProperty(Rus ? "Требуемый прогресс для добычи 1 единицы ресурса" : "Required progress for extraction 1 amount of resource")] internal float workNeeded = 0.3f;
            [JsonProperty(Rus ? "Требуется проделать «работы» на 1 единицу ресурса (в очках) (aka «WorkNeeded»)" : "Required «work» progress for extraction 1 amount of resource (in points) (aka «WorkNeeded»)")] internal float WorkNeeded = 0.3f;
        }
        #endregion
        #region [Oxide]
        private void OnQuarryToggled(MiningQuarry quarry, BasePlayer player)
        {
            if (quarry.skinID == 0 || quarry.OwnerID == 0 || !cfg.Quarries.ContainsKey(quarry.skinID)) return;
            QuarrySetting set = cfg.Quarries[quarry.skinID];
            if (!set.OwnerToggle) return;
            if (quarry.OwnerID != player.userID)
            {
                bool toggle = true;
                if (set.TeamToggle)
                {
                    RelationshipManager.PlayerTeam team = RelationshipManager.ServerInstance.FindPlayersTeam(quarry.OwnerID);
                    if (team != null && team.members.Contains(player.userID))
                        toggle = false;
                }
                if (toggle)
                {
                    if (!quarry.HasFlag(BaseEntity.Flags.On))
                        quarry.EngineSwitch(true);
                    else quarry.EngineSwitch(false);
                }
            }
        }
        private object CanBuild(Planner plan, Construction entity, Construction.Target target)
        {
            if (!cfg.Quarries.ContainsKey(plan.skinID)) return null;
            BasePlayer player = plan.GetOwnerPlayer();
            if (!data.ContainsKey(player.userID)) return null;
            if (cfg.Quarries[plan.skinID].quarry && data[player.userID].quarries >= cfg.quarries)
            {
                player.SendConsoleCommand("chat.add", 2, cfg.chatid, string.Format(GetLang("Chat.QLimit", player.UserIDString), cfg.quarries));
                return false;
            }
            if (!cfg.Quarries[plan.skinID].quarry && data[player.userID].jacks >= cfg.jacks)
            {
                player.SendConsoleCommand("chat.add", 2, cfg.chatid, string.Format(GetLang("Chat.JLimit", player.UserIDString), cfg.jacks));
                return false;
            }
            return null;
        }
        private void OnEntityBuilt(Planner plan, GameObject obj)
        {
            if (plan == null || obj == null) return;
            if (!cfg.Quarries.ContainsKey(plan.skinID)) return;
            MiningQuarry quarry;
            BasePlayer player = plan.GetOwnerPlayer();
            if (cfg.Quarries[plan.skinID].ChangePrefab)
            {
                BaseEntity ent = obj.ToBaseEntity();
                NextTick(() => { ent.Kill(); });
                if (Vector3.Distance(player.ServerPosition, ent.transform.position) < cfg.Quarries[plan.skinID].distance)
                {
                    player.SendConsoleCommand("chat.add", 2, cfg.chatid, GetLang("Chat.TooClose", player.UserIDString));
                    GiveQuarryToPlayer(player, plan.skinID);
                    return;
                }
                Vector3 transform = ent.transform.position + new Vector3(0f, -2f, 0f) + (ent.transform.position - player.transform.position).normalized * 2;
                string prefab = cfg.Quarries[plan.skinID].quarry ? "assets/prefabs/deployable/quarry/mining_quarry.prefab" : "assets/prefabs/deployable/oil jack/mining.pumpjack.prefab";
                BaseEntity entity = GameManager.server.CreateEntity(prefab, transform, ent.transform.rotation);
                if (entity == null) return;
                entity.skinID = plan.skinID;
                entity.OwnerID = player.userID;
                entity.Spawn();
                quarry = entity.GetComponent<MiningQuarry>();
            }
            else quarry = obj.ToBaseEntity().GetComponent<MiningQuarry>();
            if (quarry == null) return;
            UpdateQuarry(quarry, player);
        }
        private object OnQuarryConsumeFuel(MiningQuarry quarry, Item item)
        {
            if (quarry == null || quarry.OwnerID == 0 || quarry.skinID == 0 || !cfg.Quarries.ContainsKey(quarry.skinID)) return null;
            QuarrySetting set = cfg.Quarries[quarry.skinID];
            if (set.changeFuel)
                item = quarry.fuelStoragePrefab.instance.GetComponent<StorageContainer>().inventory.FindItemsByItemName(set.fuelShortname);
            if (item != null && item.amount >= set.fuel)
            {
                item.amount -= set.fuel - 1;
                return item;
            }
            else
            {
                quarry.EngineSwitch(false);
                return false;
            }
        }
        private void OnEntityDeath(MiningQuarry quarry, HitInfo info) => OnEntityKill(quarry);
        private void OnEntityKill(MiningQuarry quarry)
        {
            if (quarry == null || quarry.OwnerID == 0 || quarry.skinID == 0 || !cfg.Quarries.ContainsKey(quarry.skinID)) return;
            if (!data.ContainsKey(quarry.OwnerID)) return;
            BasePlayer player = BasePlayer.FindByID(quarry.OwnerID);
            if (cfg.Quarries[quarry.skinID].quarry)
            {
                data[quarry.OwnerID].quarries--;
                if (player != null)
                    player.SendConsoleCommand("chat.add", 2, cfg.chatid, string.Format(GetLang("Chat.QDestroyed", player.UserIDString), cfg.quarries - data[quarry.OwnerID].quarries));
            }
            else
            {
                data[quarry.OwnerID].jacks--;
                if (player != null)
                    player.SendConsoleCommand("chat.add", 2, cfg.chatid, string.Format(GetLang("Chat.JDestroyed", player.UserIDString), cfg.jacks - data[quarry.OwnerID].jacks));
            }
        }
        private void OnNormalRemovedEntity(BasePlayer player, MiningQuarry quarry)
        {
            if (quarry == null || quarry.OwnerID == 0 || quarry.skinID == 0) return;
            if (!cfg.Quarries.ContainsKey(quarry.skinID)) return;
            if (cfg.Quarries[quarry.skinID].ChangePrefab)
                GiveQuarryToPlayer(player, quarry.skinID, true);
        }
        #endregion
        #region [Functions]
        private string GetLang(string key, string id) => lang.GetMessage(key, this, id);
        private void UpdateQuarry(MiningQuarry quarry, BasePlayer player = null)
        {
            if (!cfg.Quarries.ContainsKey(quarry.skinID)) return;
            QuarrySetting set = cfg.Quarries[quarry.skinID];
            if (set.changeFuel)
                quarry.fuelStoragePrefab.instance.GetComponent<StorageContainer>().inventory.onlyAllowedItems = new ItemDefinition[1] { ItemManager.FindItemDefinition(set.fuelShortname) };
            if (set.changeResourse)
            {
                quarry.processRate = set.processRate;
                quarry.workToAdd = set.workToAdd;
                quarry.workPerFuel = set.workPerFuel;
                if (set.resources.Count > 0)
                {
                    quarry.canExtractLiquid = true;
                    quarry.canExtractSolid = true;
                    quarry._linkedDeposit = new ResourceDepositManager.ResourceDeposit();
                    foreach (ResourceSettings x in set.resources)
                        quarry._linkedDeposit.Add(ItemManager.FindItemDefinition(x.shortname), 1f, 1000, x.workNeeded, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM);
                }
            }
            quarry.SendNetworkUpdate();
            if (!data.ContainsKey(quarry.OwnerID))
                data.Add(quarry.OwnerID, new CashData());
            if (set.quarry)
            {
                data[quarry.OwnerID].quarries++;
                if (player != null)
                    player.SendConsoleCommand("chat.add", 2, cfg.chatid, string.Format(GetLang("Chat.QPlaced", player.UserIDString), data[quarry.OwnerID].quarries, cfg.quarries));
            }
            else
            {
                data[quarry.OwnerID].jacks++;
                if (player != null)
                    player.SendConsoleCommand("chat.add", 2, cfg.chatid, string.Format(GetLang("Chat.JPlaced", player.UserIDString), data[quarry.OwnerID].jacks, cfg.jacks));
            }
        }
        private void GiveQuarryToPlayer(BasePlayer player, ulong skin, bool remove = false, Item item = null)
        {
            if (item == null)
            {
                string shortname = cfg.Quarries[skin].ChangePrefab ? cfg.Quarries[skin].prefab : "mining.quarry";
                item = ItemManager.CreateByName(shortname, 1, skin);
                item.name = cfg.Quarries[skin].name;
            }
            if (player.inventory.containerMain.itemList.Count >= 24 && player.inventory.containerBelt.itemList.Count >= 6)
            {
                player.SendConsoleCommand("chat.add", 2, cfg.chatid, GetLang("Chat.NotRecieved", player.UserIDString));
                timer.In(30f, () => { GiveQuarryToPlayer(player, skin, false, item); });
                return;
            }
            player.GiveItem(item);
            Planner entity = item.GetHeldEntity() as Planner;
            if (entity != null)
            {
                entity.skinID = skin;
                entity.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
            }
            if (remove) return;
            string mes = Rus ? $"Игрок {player.displayName}({player.userID}) успешно получил «{cfg.Quarries[skin].name}»" : $"Player {player.displayName}({player.userID}) successfully recieved «{cfg.Quarries[skin].name}»";
            PrintWarning(mes);
            if (cfg.log)
                LogToFile("Log", $"[{DateTime.Now:dd-MM-yyyy HH:mm:ss}] " + mes, this, false);
        }
        #endregion
        #region [Lang]
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Chat.NotRecieved"] = "Your inventory is full. Anouther attempt to recieve the item will occur in 30 seconds.",
                ["Chat.TooClose"] = "You are too close to the placement position.",
                ["Chat.QLimit"] = "You are already placed maximum amount ({0}) of quarries.",
                ["Chat.JLimit"] = "You are already placed maximum amount ({0}) of pumpjacks.",
                ["Chat.QPlaced"] = "You are placed {0} from {1} quarries.",
                ["Chat.JPlaced"] = "You are placed {0} from {1} pumpjacks.",
                ["Chat.QDestroyed"] = "Your quarry has been destroyed. You can place {0} more.",
                ["Chat.JDestroyed"] = "Your pumpjack has been destroyed. You can place {0} more."
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Chat.NotRecieved"] = "Ваш инвентарь переполнен. Повторная попытка выдачи предмета произойдет через 30 секунд.",
                ["Chat.TooClose"] = "Вы слишком близко к месту установки.",
                ["Chat.QLimit"] = "У вас уже установлено максимальное количество ({0}) карьеров.",
                ["Chat.JLimit"] = "У вас уже установлено максимальное количество ({0}) нефтекачек.",
                ["Chat.QPlaced"] = "Вы установили {0} из {1} карьеров.",
                ["Chat.JPlaced"] = "Вы установили {0} из {1} нефтекачек.",
                ["Chat.QDestroyed"] = "Ваш карьер был уничтожен. Можете установить еще {0}",
                ["Chat.JDestroyed"] = "Ваша нефтекачка была уничтожена. Можете установить еще {0}"
            }, this, "ru");
        }
        #endregion
        #region [Commands]
        [ConsoleCommand("quarry.give")]
        private void CMD_GiveCustomQuarry(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null && !permission.UserHasPermission(arg.Player().UserIDString, giveperm)) return;
            ulong userid;
            ulong skin;
            if (arg.Args == null || arg.Args.Length < 2 || !ulong.TryParse(arg.Args[0], out userid) || !ulong.TryParse(arg.Args[1], out skin))
            {
                PrintError(Rus ? "Неверный формат. Формат:\nquarry.give «SteamID» «SkinID»" : "Invalid format. Format:\nquarry.give «SteamID» «SkinID»");
                return;
            }
            if (!cfg.Quarries.ContainsKey(skin))
            {
                PrintError(Rus ? "Конфигурация не содержит карьера с указанным «SkinID»" : "The configuration does not contain a quarry with this «SkinID»");
                return;
            }
            BasePlayer player = BasePlayer.FindByID(userid);
            if (player == null)
            {
                PrintError(Rus ? "Игрок с указанным «SteamID» не найден" : "Player with intered «SteamID» not found");
                return;
            }
            GiveQuarryToPlayer(player, skin);
        }
        [ConsoleCommand("quarry.giveall")]
        private void CMD_GiveAllCustomQuarries(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null && !permission.UserHasPermission(arg.Player().UserIDString, giveperm)) return;
            ulong userid;
            if (arg.Args == null || arg.Args.Length < 1 || !ulong.TryParse(arg.Args[0], out userid))
            {
                PrintError(Rus ? "Неверный формат. Формат:\nquarry.giveall «SteamID»" : "Invalid format. Format:\nquarry.giveall «SteamID»");
                return;
            }
            BasePlayer player = BasePlayer.FindByID(userid);
            if (player == null)
            {
                PrintError(Rus ? "Игрок с указанным «SteamID» не найден" : "Player with intered «SteamID» not found");
                return;
            }
            foreach (ulong x in cfg.Quarries.Keys)
                GiveQuarryToPlayer(player, x);
        }
        [ConsoleCommand("quarry.giveme")]
        private void CMD_GiveCustomQuarrySelf(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null || !permission.UserHasPermission(arg.Player().UserIDString, giveperm)) return;
            foreach (ulong x in cfg.Quarries.Keys)
                GiveQuarryToPlayer(arg.Player(), x);
        }
        #endregion
    }
}
