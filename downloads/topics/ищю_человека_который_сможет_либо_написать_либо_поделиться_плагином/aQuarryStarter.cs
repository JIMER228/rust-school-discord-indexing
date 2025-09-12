using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using System.IO;

namespace Oxide.Plugins
{
    [Info("aQuarryStarter", "chess - lox", "1.0.0")]
    [Description("Добавляет готовые карьеры и выдаёт их игрокам")]
    public class aQuarryStarter : RustPlugin
    {
        private const string PermUse = "aquarrystarter.use";

        #region Файлы конфигурации
        private readonly string _staticDir   = Interface.Oxide.DataFileSystem.GetFile("aQuarry/StaticQuarries").Directory;
        private readonly string _personalDir = Interface.Oxide.DataFileSystem.GetFile("aQuarry/PersonalQuarries").Directory;

        private DynamicConfigFile _personalQuarry;
        private DynamicConfigFile _advancedQuarry;
        private DynamicConfigFile _staticStone;
        private DynamicConfigFile _staticPump;
        #endregion

        #region Хуки
        private void Init()
        {
            permission.RegisterPermission(PermUse, this);
            cmd.AddChatCommand("kit", this, "CmdKit");
        }

        private void OnServerInitialized()
        {
            // Создаём папки, если их нет
            if (!Directory.Exists(_staticDir))   Directory.CreateDirectory(_staticDir);
            if (!Directory.Exists(_personalDir)) Directory.CreateDirectory(_personalDir);

            InitPersonalQuarries();
            InitStaticQuarries();
        }
        #endregion

        #region Команды
        private void CmdKit(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermUse))
            {
                SendReply(player, "У вас нет прав.");
                return;
            }

            if (args.Length == 0)
            {
                SendReply(player, "/kit quarry — обычный карьер\n/kit advancedquarry — продвинутый");
                return;
            }

            switch (args[0].ToLower())
            {
                case "quarry":
                    GiveQuarry(player, 3187637123UL); // SkinID обычного
                    break;

                case "advancedquarry":
                    GiveQuarry(player, 3185891808UL); // SkinID продвинутого
                    break;

                default:
                    SendReply(player, "Неизвестный набор.");
                    break;
            }
        }
        #endregion

        #region Методы выдачи
        private void GiveQuarry(BasePlayer player, ulong skinID)
        {
            if (!plugins.Exists("aQuarry"))
            {
                SendReply(player, "Плагин aQuarry не загружен.");
                return;
            }

            // Через вызов aQuarry API
            plugins.Call("GiveQuarry", player, skinID);
            SendReply(player, $"Вы получили карьер (skin {skinID})!");
        }
        #endregion

        #region Инициализация JSON-файлов
        private void InitPersonalQuarries()
        {
            _personalQuarry = Interface.Oxide.DataFileSystem.GetFile("aQuarry/PersonalQuarries/PersonalQuarry.json");
            if (!_personalQuarry.Exists())
            {
                _personalQuarry.WriteObject(GetPersonalQuarryCfg());
                Puts("Создан PersonalQuarry.json");
            }

            _advancedQuarry = Interface.Oxide.DataFileSystem.GetFile("aQuarry/PersonalQuarries/AdvancedQuarry.json");
            if (!_advancedQuarry.Exists())
            {
                _advancedQuarry.WriteObject(GetAdvancedQuarryCfg());
                Puts("Создан AdvancedQuarry.json");
            }
        }

        private void InitStaticQuarries()
        {
            _staticStone = Interface.Oxide.DataFileSystem.GetFile("aQuarry/StaticQuarries/StaticStone.json");
            if (!_staticStone.Exists())
            {
                _staticStone.WriteObject(GetStaticStoneCfg());
                Puts("Создан StaticStone.json");
            }

            _staticPump = Interface.Oxide.DataFileSystem.GetFile("aQuarry/StaticQuarries/StaticPump.json");
            if (!_staticPump.Exists())
            {
                _staticPump.WriteObject(GetStaticPumpCfg());
                Puts("Создан StaticPump.json");
            }
        }
        #endregion

        #region Сами конфиги
        private object GetPersonalQuarryCfg()
        {
            return new Dictionary<string, object>
            {
                ["Enable this quarry?"] = true,
                ["Quarry custom name"] = "Personal Quarry",
                ["Quarry SkinID"] = 3187637123UL,
                ["Prefab substitution"] = new Dictionary<string, object>
                {
                    ["Use prefab substitution?"] = true,
                    ["This is a quarry (true) or pumpjack (false) ?"] = true,
                    ["ShortName of the item from which we create a quarry"] = "furnace.large",
                    ["Minimum distance from player to quarry (when installed)"] = 3.0,
                    ["Minimum distance from quarry to constructions (when installed)"] = 17.0,
                    ["Additional height setting (when installed)"] = -2.0
                },
                ["Production"] = new Dictionary<string, object>
                {
                    ["List of the fuel used and its production settings"] = new List<object>
                    {
                        new Dictionary<string, object>
                        {
                            ["ShortName"] = "lowgradefuel",
                            ["Amount"] = 50,
                            ["How many seconds can quarry work on one consume of this fuel?"] = 60.0,
                            ["Resource production interval on this fuel (seconds)"] = 60.0,
                            ["Gathering resources on this fuel"] = new List<object>
                            {
                                new { ShortName = "stones", Amount = 100, "Amount max" = 100, Probability = 100.0 },
                                new { ShortName = "metal.ore", Amount = 70, "Amount max" = 70, Probability = 100.0 },
                                new { ShortName = "sulfur.ore", Amount = 50, "Amount max" = 50, Probability = 100.0 },
                                new { ShortName = "hq.metal.ore", Amount = 10, "Amount max" = 10, Probability = 10.0 }
                            }
                        }
                    }
                }
            };
        }

        private object GetAdvancedQuarryCfg()
        {
            return new Dictionary<string, object>
            {
                ["Enable this quarry?"] = true,
                ["Quarry custom name"] = "Advanced Quarry",
                ["Quarry SkinID"] = 3185891808UL,
                ["Prefab substitution"] = new Dictionary<string, object>
                {
                    ["Use prefab substitution?"] = true,
                    ["This is a quarry (true) or pumpjack (false) ?"] = true,
                    ["ShortName of the item from which we create a quarry"] = "furnace.large",
                    ["Minimum distance from player to quarry (when installed)"] = 3.0,
                    ["Minimum distance from quarry to constructions (when installed)"] = 17.0,
                    ["Additional height setting (when installed)"] = -2.0
                },
                ["Production"] = new Dictionary<string, object>
                {
                    ["List of the fuel used and its production settings"] = new List<object>
                    {
                        new Dictionary<string, object>
                        {
                            ["ShortName"] = "diesel_barrel",
                            ["Amount"] = 1,
                            ["How many seconds can quarry work on one consume of this fuel?"] = 600.0,
                            ["Resource production interval on this fuel (seconds)"] = 120.0,
                            ["Gathering resources on this fuel"] = new List<object>
                            {
                                new { ShortName = "stones", Amount = 400, "Amount max" = 400, Probability = 100.0 },
                                new { ShortName = "metal.ore", Amount = 280, "Amount max" = 280, Probability = 100.0 },
                                new { ShortName = "sulfur.ore", Amount = 200, "Amount max" = 200, Probability = 100.0 },
                                new { ShortName = "hq.metal.ore", Amount = 40, "Amount max" = 40, Probability = 20.0 }
                            }
                        }
                    }
                }
            };
        }

        private object GetStaticStoneCfg()
        {
            return new Dictionary<string, object>
            {
                ["Enable this quarry?"] = true,
                ["Inventory"] = new { "Resource container capacity" = 18, "Fuel container capacity" = 6 },
                ["Production"] = new Dictionary<string, object>
                {
                    ["List of the fuel used and its production settings"] = new List<object>
                    {
                        new Dictionary<string, object>
                        {
                            ["ShortName"] = "diesel_barrel",
                            ["Amount"] = 1,
                            ["How many seconds can quarry work on one consume of this fuel?"] = 300.0,
                            ["Resource production interval on this fuel (seconds)"] = 60.0,
                            ["Gathering resources on this fuel"] = new List<object>
                            {
                                new { ShortName = "stones", Amount = 5000, "Amount max" = 5000, Probability = 100.0 },
                                new { ShortName = "metal.ore", Amount = 2500, "Amount max" = 2500, Probability = 100.0 },
                                new { ShortName = "sulfur.ore", Amount = 1500, "Amount max" = 1500, Probability = 100.0 },
                                new { ShortName = "hq.metal.ore", Amount = 300, "Amount max" = 300, Probability = 100.0 }
                            }
                        }
                    }
                }
            };
        }

        private object GetStaticPumpCfg()
        {
            return new Dictionary<string, object>
            {
                ["Enable this quarry?"] = true,
                ["Inventory"] = new { "Resource container capacity" = 18, "Fuel container capacity" = 6 },
                ["Production"] = new Dictionary<string, object>
                {
                    ["List of the fuel used and its production settings"] = new List<object>
                    {
                        new Dictionary<string, object>
                        {
                            ["ShortName"] = "diesel_barrel",
                            ["Amount"] = 1,
                            ["How many seconds can quarry work on one consume of this fuel?"] = 300.0,
                            ["Resource production interval on this fuel (seconds)"] = 60.0,
                            ["Gathering resources on this fuel"] = new List<object>
                            {
                                new { ShortName = "crude.oil", Amount = 250, "Amount max" = 250, Probability = 100.0 },
                                new { ShortName = "lowgradefuel", Amount = 100, "Amount max" = 100, Probability = 100.0 }
                            }
                        }
                    }
                }
            };
        }
        #endregion
    }
}