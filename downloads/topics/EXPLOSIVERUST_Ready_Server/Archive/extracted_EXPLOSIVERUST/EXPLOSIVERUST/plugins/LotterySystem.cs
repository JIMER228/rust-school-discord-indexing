using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("LotterySystem", "https://topplugin.ru / https://discord.com/invite/5DPTsRmd3G", "1.0.3")]
    class LotterySystem : RustPlugin
    {
        #region Вар
        string Layer = "Lottery_UI";

        [PluginReference] Plugin ImageLibrary, InventorySystem;

        static System.Random Rnd = new System.Random();

        public Dictionary<ulong, DataBase> DB = new Dictionary<ulong, DataBase>();
        #endregion

        #region Класс
        public class Settings
        {
            public string DisplayName;
            public string ShortName;
            public string Command;
            public string Url;
            public string Color;
            public int Amount;
            public int DropChance;
        }

        public class DataBase
        {
            public int Amount;
        }
        #endregion

        #region Ресурсы
        List<Settings> settings = new List<Settings>()
        {
            new Settings
            {
                DisplayName = "Дерево",
                ShortName = "wood",
                Command = null,
                Url = null,
                Color = "0.39 0.39 0.39",
                Amount = 3000,
                DropChance = 100
            },
            new Settings
            {
                DisplayName = "Камень",
                ShortName = "stones",
                Command = null,
                Url = null,
                Color = "0.39 0.39 0.39",
                Amount = 3000,
                DropChance = 100
            },
            new Settings
            {
                DisplayName = "Ткань",
                ShortName = "cloth",
                Command = null,
                Url = null,
                Color = "0.39 0.39 0.39",
                Amount = 300,
                DropChance = 100
            },
            new Settings
            {
                DisplayName = "Кожа",
                ShortName = "leather",
                Command = null,
                Url = null,
                Color = "0.39 0.39 0.39",
                Amount = 200,
                DropChance = 100
            },
            new Settings
            {
                DisplayName = "Уголь",
                ShortName = "charcoal",
                Command = null,
                Url = null,
                Color = "0.39 0.39 0.39",
                Amount = 3000,
                DropChance = 100
            },
            new Settings
            {
                DisplayName = "Порох",
                ShortName = "gunpowder",
                Command = null,
                Url = null,
                Color = "0.34 0.77 0.60",
                Amount = 2000,
                DropChance = 50
            },
            new Settings
            {
                DisplayName = "Мвк",
                ShortName = "metal.refined",
                Command = null,
                Url = null,
                Color = "0.34 0.77 0.60",
                Amount = 100,
                DropChance = 50
            },
            new Settings
            {
                DisplayName = "Скрап",
                ShortName = "scrap",
                Command = null,
                Url = null,
                Color = "0.34 0.77 0.60",
                Amount = 150,
                DropChance = 50
            },
            new Settings
            {
                DisplayName = "Бур",
                ShortName = "jackhammer",
                Command = null,
                Url = null,
                Color = "0.34 0.77 0.60",
                Amount = 1,
                DropChance = 50
            },
            new Settings
            {
                DisplayName = "Хазмат",
                ShortName = "halloween.surgeonsuit",
                Command = null,
                Url = null,
                Color = "0.55 0.33 0.76",
                Amount = 1,
                DropChance = 25
            },
            new Settings
            {
                DisplayName = "Бомба",
                ShortName = "explosive.satchel",
                Command = null,
                Url = null,
                Color = "0.55 0.33 0.76",
                Amount = 2,
                DropChance = 25
            },
            new Settings
            {
                DisplayName = "Верстак 2 уровня",
                ShortName = "workbench2",
                Command = null,
                Url = null,
                Color = "0.55 0.33 0.76",
                Amount = 1,
                DropChance = 25
            },
            new Settings
            {
                DisplayName = "Огненная колба",
                ShortName = null,
                Command = "",
                Url = "https://imgur.com/ITJgzdK.png",
                Color = "0.93 0.24 0.38",
                Amount = 1,
                DropChance = 10
            },
            new Settings
            {
                DisplayName = "Метаболизм на 3 дня",
                ShortName = null,
                Command = "",
                Url = "https://gspics.org/images/2020/12/22/0ksTyZ.png",
                Color = "0.93 0.24 0.38",
                Amount = 1,
                DropChance = 10
            },
            new Settings
            {
                DisplayName = "Переработчик",
                ShortName = null,
                Command = "",
                Url = "https://imgur.com/Km6sIej.png",
                Color = "0.93 0.24 0.38",
                Amount = 1,
                DropChance = 10
            },
            new Settings
            {
                DisplayName = "Вип на 3 дня",
                ShortName = null,
                Command = "",
                Url = "https://gspics.org/images/2020/11/22/00lmde.png",
                Color = "1 0.97 0",
                Amount = 1,
                DropChance = 3
            },
        };
        #endregion

        #region Хуки
        void OnServerInitialized()
        {
            ImageLibrary?.Call("AddImage", "https://i.imgur.com/oyqWIt5.png", "SZMTqbc");
            foreach (var check in settings)
            {
                var image = check.Command != null ? check.Url : $"https://rustlabs.com/img/items180/{check.ShortName}.png";
                var name = check.Command != null ? check.Url : check.ShortName;
                ImageLibrary?.Call("AddImage", image, name);
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

        object OnDispenserBonus(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            var player = entity.ToPlayer();
            if (item.info.shortname.Contains("hq.metal.ore")) return false;
            DB[player.userID].Amount += 1;
            player.SendConsoleCommand($"note.inv 605467368 1 \"R Coins\"");
            return null;
        }
        #endregion

        #region Дата
        void CreateDataBase(BasePlayer player)
        {
            var Database = Interface.Oxide.DataFileSystem.ReadObject<DataBase>($"LotterySystem/{player.userID}");
            
            if (!DB.ContainsKey(player.userID))
                DB.Add(player.userID, new DataBase());
             
            DB[player.userID] = Database ?? new DataBase();
        }

        void SaveDataBase(ulong userId) => Interface.Oxide.DataFileSystem.WriteObject($"LotterySystem/{userId}", DB[userId]);
        #endregion

        #region Команда
        [ConsoleCommand("lottery")]
        void ConsoleLottery(ConsoleSystem.Arg args)
        {
            var player = args.Player();

            if (DB[player.userID].Amount >= 25)
            {
                PrizUI(player, int.Parse(args.Args[0]));

                DB[player.userID].Amount -= 25;
                ACoins(player);
            }
            else
            {
                player.SendConsoleCommand($"note.inv 605467368 -{25 - DB[player.userID].Amount} \"Недостаточно AC\"");
            }
        }
        #endregion

        #region  Интерфейс
        void LotteryUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, Layer);
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.09 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, "Menu", Layer);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.04 0.855", AnchorMax = "0.96 1", OffsetMax = "0 0" },
                Text = { Text = $"<size=25><b>Лотерея</b></size>\nЗдесь вы можете бесплатно получить предметы.", Color = "1 1 1 0.5", Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, Layer);

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.29 0.08", AnchorMax = "0.294 0.85", OffsetMax = "0 0" },
                Image = { Color = "0.10 0.13 0.19 1" }
            }, Layer);

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.29 0.08", AnchorMax = "0.33 0.086", OffsetMax = "0 0" },
                Image = { Color = "0.10 0.13 0.19 1" }
            }, Layer);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.33 0.05", AnchorMax = "0.69 0.12", OffsetMax = "0 0" },
                Text = { Text = $"Открытие стоит 25 R Coins.", Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, Layer);

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.08 0.09", AnchorMax = "0.23 0.14", OffsetMax = "0 0" },
                Image = { Color = "0.10 0.13 0.19 0" }
            }, Layer, "Coin");

            container.Add(new CuiElement
            {
                Parent = "Coin",
                Components =
                {
                    new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", "SZMTqbc"), Color = "1 1 1 0.9" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "0.35 1", OffsetMax = "0 0" }
                }
            });

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.315 0.16", AnchorMax = "0.96 0.2", OffsetMax = "0 0" },
                Text = { Text = $"Список возможных наград.", Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, Layer);

            float width = 0.23f, height = 0.23f, startxBox = 0.04f, startyBox = 0.85f - height, xmin = startxBox, ymin = startyBox;
            for (int z = 0; z < 3; z++)
            {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmin + width} {ymin + height * 1}", OffsetMin = "1 1", OffsetMax = "-1 -1" },
                    Button = { Color = "0.10 0.13 0.19 1", Command = $"lottery {z}" },
                    Text = { Text = $"<b><size=16>✔\nОТКРОЙ</size></b>\nИ получи приз", Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 10, Font = "robotocondensed-regular.ttf" }
                }, Layer, $"Priz.{z}");

                xmin += width;
                if (xmin + width >= 0)
                {
                    xmin = startxBox;
                    ymin -= height;
                }
            }

            float width1 = 0.16f, height1 = 0.16f, startxBox1 = 0.315f, startyBox1 = 0.85f - height1, xmin1 = startxBox1, ymin1 = startyBox1;
            foreach (var check in settings)
            {
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"{xmin1} {ymin1}", AnchorMax = $"{xmin1 + width1} {ymin1 + height1 * 1}", OffsetMin = "1 1", OffsetMax = "-1 -1" },
                    Image = { Color = "0.10 0.13 0.19 1" }
                }, Layer, "Settings");

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Image = { Color = $"{check.Color} 0.1" }
                }, "Settings");

                var image = check.Command != null ? check.Url : check.ShortName;
                container.Add(new CuiElement
                {
                    Parent = "Settings",
                    Components =
                    {
                        new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", image), Color = "1 1 1 0.8", FadeIn = 1f },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 6", OffsetMax = "-6 -6" }
                    }
                });


                xmin1 += width1;
                if (xmin1 + width1 >= 1)
                {
                    xmin1 = startxBox1;
                    ymin1 -= height1;
                }
            }

            CuiHelper.AddUi(player, container);
            ACoins(player);
        }

        void PrizUI(BasePlayer player, int z)
        {
            CuiHelper.DestroyUi(player, "Priz");
            var container = new CuiElementContainer();

            var check = settings.OrderBy(x => Rnd.Next()).FirstOrDefault();	

            var item = Rnd.Next(0, 1001)/10f <= check.DropChance;
            Effect c = new Effect("assets/prefabs/deployable/vendingmachine/effects/vending-machine-purchase-human.prefab", player, 0, new Vector3(), new Vector3());
            EffectNetwork.Send(c, player.Connection);

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0.10 0.13 0.19 1", FadeIn = 0.5f }
            }, $"Priz.{z}", "Priz");

			if (item)
            {
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Image = { Color = $"{check.Color} 0.1" }
                }, "Priz");

                var image = check.Command != null ? check.Url : check.ShortName;
                container.Add(new CuiElement
                {
                    Parent = "Priz",
                    Components =
                    {
                        new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", image), Color = "1 1 1 0.9", FadeIn = 1f },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "15 15", OffsetMax = "-15 -15" }
                    }
                });

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0.02", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Text = { Text = $"{check.Amount} шт.", Color = "1 1 1 0.5", Align = TextAnchor.LowerCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
                }, "Priz");

                InventorySystem.Call("AddItem", player, check.DisplayName, check.ShortName, check.Command, check.Url, check.Color, check.Amount);
            }
            else
            {
                DB[player.userID].Amount += 15;

                container.Add(new CuiElement
                {
                    Parent = "Priz",
                    Components =
                    {
                        new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", "SZMTqbc"), Color = "1 1 1 0.9", FadeIn = 1f },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "33 30", OffsetMax = "-33 -30" }
                    }
                });

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0.02", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Text = { Text = $"15 AC.", Color = "1 1 1 0.5", Align = TextAnchor.LowerCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
                }, "Priz");
            }

            CuiHelper.AddUi(player, container);
        }

        void ACoins(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "AC");
            var container = new CuiElementContainer();

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.36 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Text = { Text = $"{DB[player.userID].Amount}", Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "Coin", "AC");

            CuiHelper.AddUi(player, container);
        }
        #endregion
    }
}