using Oxide.Core;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("PassSystem", "https://topplugin.ru / https://discord.com/invite/5DPTsRmd3G", "1.0.3")]
    class PassSystem : RustPlugin
    {
        #region Вар
        [PluginReference] Plugin ImageLibrary, InventorySystem;

        public Dictionary<ulong, DataBase> DB = new Dictionary<ulong, DataBase>();
        private readonly List<uint> crateInfo = new List<uint>();
        #endregion

        #region Класс
        public class Settings
        {
            public string DisplayName;
            public int Level;
            public string ShortName;
            public int Amount;
            public string Command;
            public string Url;
            public TaskSettings Tasks;
        }

        public class TaskSettings
        {
            public string DisplayName;
            public string ShortName;
            public int Amount;
        }

        public class DataBase
        {
            public int Level = 1;
            public int Amount;
            public bool Enabled;
        }
        #endregion

        #region Уровни
        List<Settings> settings = new List<Settings>()
        {
            new Settings
            {
                DisplayName = "Котелок",
                Level = 1,
                ShortName = "cursedcauldron",
                Amount = 1,
                Command = null,
                Url = null,
                Tasks = new TaskSettings
                {
                    DisplayName = "Соберите 200 ткани",
                    ShortName = "cloth",
                    Amount = 200
                }
            },
            new Settings
            {
                DisplayName = "Скрап x100",
                Level = 2,
                ShortName = "scrap",
                Amount = 100,
                Command = null,
                Url = null,
                Tasks = new TaskSettings
                {
                    DisplayName = "Сломайте 10 обычных бочек",
                    ShortName = "loot_barrel_2",
                    Amount = 10
                }
            },
            new Settings
            {
                DisplayName = "Настил",
                Level = 3,
                ShortName = "floor.triangle.grill",
                Amount = 1,
                Command = null,
                Url = null,
                Tasks = new TaskSettings
                {
                    DisplayName = "Добудьте 20 кактусов",
                    ShortName = "cactusflesh",
                    Amount = 20
                }
            },
            new Settings
            {
                DisplayName = "Гроб",
                Level = 4,
                ShortName = "coffin.storage",
                Amount = 1,
                Command = null,
                Url = null,
                Tasks = new TaskSettings
                {
                    DisplayName = "Нафармите 2400 серы",
                    ShortName = "sulfur.ore",
                    Amount = 2400
                }
            },
            new Settings
            {
                DisplayName = "Хазмат",
                Level = 5,
                ShortName = "hazmatsuit_scientist",
                Amount = 1,
                Command = null,
                Url = null,
                Tasks = new TaskSettings
                {
                    DisplayName = "Убить 3 медведя",
                    ShortName = "bear",
                    Amount = 3
                }
            },
            new Settings
            {
                DisplayName = "Револьвер",
                Level = 6,
                ShortName = "pistol.revolver",
                Amount = 1,
                Command = null,
                Url = null,
                Tasks = new TaskSettings
                {
                    DisplayName = "Залутайте 10 обычных ящиков",
                    ShortName = "crate_normal_2",
                    Amount = 10
                }
            },
            new Settings
            {
                DisplayName = "Сопля",
                Level = 7,
                ShortName = "supply.signal",
                Amount = 1,
                Command = null,
                Url = null,
                Tasks = new TaskSettings
                {
                    DisplayName = "Убейте 10 игроков",
                    ShortName = "player",
                    Amount = 10
                }
            },
            new Settings
            {
                DisplayName = "Гаражка",
                Level = 8,
                ShortName = "wall.frame.garagedoor",
                Amount = 1,
                Command = null,
                Url = null,
                Tasks = new TaskSettings
                {
                    DisplayName = "Залутайте 4 оружейных ящика",
                    ShortName = "crate_normal",
                    Amount = 4
                }
            },
            new Settings
            {
                DisplayName = "Беретта",
                Level = 9,
                ShortName = "pistol.m92",
                Amount = 1,
                Command = null,
                Url = null,
                Tasks = new TaskSettings
                {
                    DisplayName = "Убейте 3 нпс",
                    ShortName = "scientist_junkpile_pistol",
                    Amount = 3
                }
            },
            new Settings
            {
                DisplayName = "Огненная колба",
                Level = 10,
                ShortName = null,
                Amount = 1,
                Command = "flask_give %STEAMID%",
                Url = "https://imgur.com/ITJgzdK.png",
                Tasks = new TaskSettings
                {
                    DisplayName = "Скрафтите томпсон",
                    ShortName = "smg.thompson",
                    Amount = 1
                }
            },
            new Settings
            {
                DisplayName = "Метаболизм на 3 дня",
                Level = 11,
                ShortName = null,
                Amount = 1,
                Command = "grantperm %STEAMID% metabolism.axion 3d",
                Url = "https://gspics.org/images/2020/10/07/zjQju.png",
                Tasks = new TaskSettings
                {
                    DisplayName = "Залутайте чинук",
                    ShortName = "codelockedhackablecrate",
                    Amount = 1
                }
            },
            new Settings
            {
                DisplayName = "Вип на 3 дня",
                Level = 12,
                ShortName = null,
                Amount = 1,
                Command = "addgroup %STEAMID% vip 3d",
                Url = "https://gspics.org/images/2020/10/01/z2aiQ.png",
                Tasks = new TaskSettings
                {
                    DisplayName = "Сбейте вертолет",
                    ShortName = "patrolhelicopter",
                    Amount = 1
                }
            },
        };
        #endregion

        #region Хуки
        void OnServerInitialized()
        {
            foreach (var check in settings)
            {
                if (check.Url != null)
                    ImageLibrary?.Call("AddImage", check.Url, check.Url);
                else
                    ImageLibrary?.Call("AddImage", $"https://rustlabs.com/img/items180/{check.ShortName}.png", check.ShortName);
            }
            foreach (var check in BasePlayer.activePlayerList)
                OnPlayerConnected(check);
        }

        void OnPlayerConnected(BasePlayer player) => CreateDataBase(player);

        void OnPlayerDisconnected(BasePlayer player) => SaveDataBase(player.userID);

        void Unload()
        {
            foreach (var check in DB)
                SaveDataBase(check.Key);
        }

        object OnCollectiblePickup(Item item, BasePlayer player)
        {
            Progress(player, item.info.shortname, item.amount);
            return null;
        }

        object OnDispenserBonus(ResourceDispenser dispenser, BaseEntity entity, Item item) => OnDispenserGather(dispenser, entity, item);
        object OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            BasePlayer player = entity.ToPlayer();
            NextTick(() => {
                Progress(player, item.info.shortname, item.amount);
            });
            return null;
        }

        public ulong lastDamageName;
        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity is BradleyAPC && info.Initiator is BasePlayer)
                lastDamageName = info.Initiator.ToPlayer().userID;
            if (entity is BaseHelicopter && info.Initiator is BasePlayer)
                lastDamageName = info.Initiator.ToPlayer().userID;
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return;
            BasePlayer player = null;

            if (info.InitiatorPlayer != null) player = info.InitiatorPlayer;
            else if (entity is BradleyAPC || entity is BaseHelicopter) player = BasePlayer.FindByID(lastDamageName);

            if (player == null) return;
            if (entity.ToPlayer() != null && entity as BasePlayer == player) return;
            Progress(player, entity?.ShortPrefabName, 1);
        }

        void OnItemCraftFinished(ItemCraftTask task, Item item)
        {
            Progress(task.owner, item.info.shortname, item.amount);
        }

        void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (crateInfo.Contains(entity.net.ID))
                return;

            crateInfo.Add(entity.net.ID);

            if (entity.ShortPrefabName.Contains("crate_normal") || entity.ShortPrefabName.Contains("codelockedhackablecrate"))
                Progress(player, entity?.ShortPrefabName, 1);
        }
        #endregion

        #region Дата
        void CreateDataBase(BasePlayer player)
        {
            var Database = Interface.Oxide.DataFileSystem.ReadObject<DataBase>($"PassSystem/{player.userID}");
            
            if (!DB.ContainsKey(player.userID))
                DB.Add(player.userID, new DataBase());
             
            DB[player.userID] = Database ?? new DataBase();
        }

        void SaveDataBase(ulong userId) => Interface.Oxide.DataFileSystem.WriteObject($"PassSystem/{userId}", DB[userId]);
        #endregion

        #region Прогресс
        void Progress(BasePlayer player, string name, int amount)
        {
            foreach (var check in settings)
            {
                var db = DB[player.userID];
                if (db.Level == check.Level)
                {
                    if (check.Tasks.ShortName == name)
                    {
                        if (db.Enabled == false)
                        {
                            db.Amount += amount;
                            player.SendConsoleCommand($"note.inv 605467368 {amount} \"Задание\"");
                            if (db.Amount >= check.Tasks.Amount)
                            {
                                player.SendConsoleCommand($"note.inv 605467368 1 \"Задание выполнено\"");
                                db.Enabled = true;
                                db.Amount = check.Tasks.Amount;
                            }
                        }
                    }
                }
            }
        }
        #endregion

        #region Команды
        [ConsoleCommand("pass")]
        void ConsolePass(ConsoleSystem.Arg args)
        {
            var player = args.Player();
            if (player != null && args.HasArgs(1))
            {
                if (args.Args[0] == "skip")
                {
                    ItemUI(player, int.Parse(args.Args[1]));
                }
                if (args.Args[0] == "info")
                {
                    InfoItemUI(player, int.Parse(args.Args[1]));
                }
                if (args.Args[0] == "take")
                {
                    var check = settings.FirstOrDefault(z => z.Level == int.Parse(args.Args[1]));
                    DB[player.userID].Level += 1;
                    DB[player.userID].Amount = 0;
                    DB[player.userID].Enabled = false;
                    InventorySystem.Call("AddItem", player, check.DisplayName, check.ShortName, check.Command, check.Url, "0 0 0", check.Amount);
                    var level = DB[player.userID].Level == 13 ? 12 : DB[player.userID].Level;
                    InfoItemUI(player, level);
                    Effect x = new Effect("assets/bundled/prefabs/fx/notice/stack.world.fx.prefab", player, 0, new Vector3(), new Vector3());
                    EffectNetwork.Send(x, player.Connection);
                }
            }
        }
        #endregion
        
        #region Интерфейс
        void PassUI(BasePlayer player)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.09 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, "Menu", "Pass");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.04 0.855", AnchorMax = "0.96 1", OffsetMax = "0 0" },
                Text = { Text = $"<size=25><b>Пасс система</b></size>\nЗдесь вы можете выполнять задания и получать за это награду.", Color = "1 1 1 0.5", Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "Pass");

            CuiHelper.AddUi(player, container);
            var level = DB[player.userID].Level == 13 ? 12 : DB[player.userID].Level;
            InfoItemUI(player, level);
            ItemUI(player);
        }

        void ItemUI(BasePlayer player, int page = 0)
        {
            CuiHelper.DestroyUi(player, "Settings");
            var container = new CuiElementContainer();

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.439 0.03", AnchorMax = $"0.499 0.09", OffsetMin = "2 2", OffsetMax = "-2 -2" },
                Button = { Color = "0.10 0.13 0.19 1", Command = page != 0 ? $"pass skip {page - 1}" : "" },
                Text = { Text = $"<", Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "Pass", "Skip");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.501 0.03", AnchorMax = $"0.561 0.09", OffsetMin = "2 2", OffsetMax = "-2 -2" },
                Button = { Color = "0.10 0.13 0.19 1", Command = settings.Count() > (page + 1) * 4 ? $"pass skip {page + 1}" : "" },
                Text = { Text = $">", Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "Pass", "Skip");

            float width = 0.21f, height = 0.33f, startxBox = 0.08f, startyBox = 0.435f - height, xmin = startxBox, ymin = startyBox;
            foreach (var check in settings.Skip(page * 4).Take(4))
            {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmin + width} {ymin + height * 1}", OffsetMin = "2 2", OffsetMax = "-2 -2" },
                    Button = { Color = "0.10 0.13 0.19 1", Command = $"pass info {check.Level}" },
                    Text = { Text = $"" }
                }, "Pass", "Settings");
                xmin += width;
                if (xmin + width >= 1)
                {
                    xmin = startxBox;
                    ymin -= height;
                }

                var image = check.Command != null ? check.Url : check.ShortName;
                container.Add(new CuiElement
                {
                    Parent = "Settings",
                    Components =
                    {
                        new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", image) },
                        new CuiRectTransformComponent { AnchorMin = "0 0.4", AnchorMax = "1 1", OffsetMin = "14 11", OffsetMax = "-14 -11" }
                    }
                });

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.35", OffsetMax = "0 0" },
                    Text = { Text = $"<b>{check.DisplayName}</b>\nLevel {check.Level}", Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
                }, "Settings");
            }

            CuiHelper.AddUi(player, container);
        }

        void InfoItemUI(BasePlayer player, int Level)
        {
            CuiHelper.DestroyUi(player, "Info");
            CuiHelper.DestroyUi(player, "Task");
            var container = new CuiElementContainer();
            var check = settings.FirstOrDefault(z => z.Level == Level);

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.085 0.65", AnchorMax = "0.915 0.85", OffsetMax = "0 0" },
                Image = { Color = "0.10 0.13 0.19 1" }
            }, "Pass", "Info");

            var image = check.Command != null ? check.Url : check.ShortName;
            container.Add(new CuiElement
            {
                Parent = "Info",
                Components =
                {
                    new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", image) },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "0.26 1", OffsetMin = "20 16", OffsetMax = "-20 -16" }
                }
            });

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.28 0", AnchorMax = "0.96 1", OffsetMax = "0 0" },
                Text = { Text = $"<size=25>Награда за {check.Level} - уровень</size>\nНаграда: {check.DisplayName}", Color = "1 1 1 0.5", Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "Info");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.085 0.44", AnchorMax = "0.915 0.64", OffsetMax = "0 0" },
                Image = { Color = "0.10 0.13 0.19 1" }
            }, "Pass", "Task");

            if (DB[player.userID].Level == check.Level)
            {
                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.02 0", AnchorMax = "1 0.96", OffsetMax = "0 0" },
                    Text = { Text = $"Задание {check.Level} - уровня", Color = "1 1 1 0.5", Align = TextAnchor.UpperCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
                }, "Task");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.02 0", AnchorMax = "1 0.45", OffsetMax = "0 0" },
                    Text = { Text = check.Tasks.DisplayName, Color = "1 1 1 0.5", Align = TextAnchor.UpperLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" }
                }, "Task");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.02 0", AnchorMax = "0.98 0.45", OffsetMax = "0 0" },
                    Text = { Text = $"{DB[player.userID].Amount}/{check.Tasks.Amount}", Color = "1 1 1 0.5", Align = TextAnchor.UpperRight, FontSize = 12, Font = "robotocondensed-regular.ttf" }
                }, "Task");

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0.02 0.17", AnchorMax = "0.98 0.25", OffsetMax = "0 0" },
                    Image = { Color = "1 1 1 0.1" }
                }, "Task", "Progress");

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = $"{((float)DB[player.userID].Amount / check.Tasks.Amount)} 1", OffsetMax = "0 0" },
                    Image = { Color = "0.93 0.24 0.38 1" }
                }, "Progress");

                if (DB[player.userID].Enabled == true)
                {
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                        Button = { Color = "0.10 0.13 0.19 1", Command = $"pass take {check.Level}" },
                        Text = { Text = $"<b><size=20>ВЫПОЛНЕНО</size></b>\nНажмите чтобы получить награду", Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 10, Font = "robotocondensed-regular.ttf" }
                    }, "Task");
                }
            }
            
            if (DB[player.userID].Level > check.Level)
            {
                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Text = { Text = $"<b><size=20>Задание {check.Level}</size></b>\nВыполнено", Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 10, Font = "robotocondensed-regular.ttf" }
                }, "Task");
            }

            if (DB[player.userID].Level < check.Level)
            {
                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Text = { Text = $"<b><size=20>ИНФОРМАЦИЯ</size></b>\nОткройте предыдущее задание, чтобы увидеть новое!", Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
                }, "Task");
            }

            CuiHelper.AddUi(player, container);
        }
        #endregion
    }
}