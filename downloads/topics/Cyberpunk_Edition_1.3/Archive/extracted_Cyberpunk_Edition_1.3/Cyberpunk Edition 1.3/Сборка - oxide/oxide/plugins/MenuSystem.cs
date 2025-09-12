using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("MenuSystem", "Netrunner", "1.0.0")]
    class MenuSystem : RustPlugin
    {
        #region Вар
        private string Layer = "Menu_UI";

        [PluginReference] Plugin ImageLibrary,Teleportation;

        Dictionary<ulong, string> ActiveButton = new Dictionary<ulong, string>();

        Dictionary<string, string> Command = new Dictionary<string, string>()
        {
            ["teleport"] = "https://lc-crb.ru/CyberPunk/bqWYNvb.png",
        };
        #endregion

        #region Хуки
        void OnServerInitialized()
        {
            foreach (var check in Command)
                ImageLibrary?.Call("AddImage", check.Value, check.Value);
        }

        void Unload()
        {
            foreach (var check in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(check, Layer);
        }
        #endregion

        #region Команды

        [ChatCommand("tpmenu")]
        void ChatTpMenu(BasePlayer player) => MenuUI(player, "teleport");

        [ConsoleCommand("menu1")]
        void ConsoleMenu(ConsoleSystem.Arg args)
        {
            var player = args.Player();
            ActiveButton[player.userID] = args.Args[0];
            UI(player, args.Args[0]);
            ButtonUI(player);
        }
        #endregion

        #region Интерфейс
        void MenuUI(BasePlayer player, string name = "")
        {
            ActiveButton[player.userID] = name;
            CuiHelper.DestroyUi(player, Layer);
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" },
            }, "ContentUI", Layer);

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.3 0.2", AnchorMax = "0.7 0.8", OffsetMax = "0 0" },
                Image = { Color = "0.09 0.10 0.15 0" }
            }, Layer, "Menu");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0.09 1", OffsetMax = "0 0" },
                Image = { Color = "0.09 0.10 0.15 0" }
            }, "Menu", "Button");



            container.Add(new CuiElement
            {
                Parent = "Image",
                Components =
                {
                    new CuiImageComponent { Color = "1 1 1 0.0", Sprite = "assets/icons/exit.png", FadeIn = 0.5f },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "15 14", OffsetMax = "-15 -14" }
                }
            });

            CuiHelper.AddUi(player, container);
            ButtonUI(player);
            UI(player, name);
        }

        void UI(BasePlayer player, string name)
        {
            if (name == "teleport")
                TeleportUI(player);
        }

        void ButtonUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "Command");
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0.13", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0"  }
            }, "Button", "Command");

            float width = 1f, height = 0.115f, startxBox = 0f, startyBox = 0.992f - height, xmin = startxBox, ymin = startyBox;
            foreach (var check in Command)
            {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmin + width} {ymin + height * 1}", OffsetMin = "0 1", OffsetMax = "0 -1" },
                    Button = { Color = "0 0 0 0", Command = $"menu {check.Key}" },
                    Text = { Text = $"" }
                }, "Command", "Images");

                var color = ActiveButton[player.userID] == check.Key ? "1 1 1 1" : "1 1 1 0.5";
                container.Add(new CuiElement
                {
                    Parent = "Images",
                    Components =
                    {
                        new CuiRawImageComponent { Color = color, Png = (string) ImageLibrary.Call("GetImage", check.Value) },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "15 11", OffsetMax = "-10 -11" }
                    }
                });

                var active = ActiveButton[player.userID] == check.Key ? "0.93 0.24 0.38 1" : "0 0 0 0";
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"0 0.1", AnchorMax = $"0.08 0.9", OffsetMax = "0 0" },
                    Image = { Color = active, FadeIn = 0.5f }
                }, "Images");

                var actives = ActiveButton[player.userID] == check.Key ? "1 1 1 0.05" : "0 0 0 0";
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"0 0.1", AnchorMax = $"1 0.9", OffsetMax = "0 0" },
                    Image = { Color = actives }
                }, "Images");

                xmin += width;
                if (xmin + width >= 1)
                {
                    xmin = startxBox;
                    ymin -= height;
                }
            }

            CuiHelper.AddUi(player, container);
        }

        void InfoUI(BasePlayer player)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.09 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, "Menu", "Info");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.04 0.855", AnchorMax = "0.96 1", OffsetMax = "0 0" },
                Text = { Text = $"<size=25><b>Основные команды</b></size>\nЗдесь вы можете узнать об основных командах сервера.", Color = "1 1 1 0.5", Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "Info");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.04 0", AnchorMax = "0.96 0.83", OffsetMax = "0 0" },
                Text = { Text = $"Список команд, связанных с телепортом:\n<color=#e85151>/tpr [ник игрока]</color> - отправить запрос на телепорт к игроку.\n<color=#e85151>/tpc</color> - отменить запрос на телепорт.\n<color=#e85151>/tpa</color> - принять запрос на телепорт.\n<color=#e85151>/sethome [название дома]</color> - создать точку дома.\n<color=#e85151>/home [название дома]</color> - телепортироваться на точку дома с указанным названием.\n\nСписок команд, связанных с друзьями:\n<color=#e85151>/team add [ник игрока]</color> - добавить игрока в группу.\n<color=#e85151>/team remove [ник игрока]</color> - удалить игрока из группы.\n\nПрочие команды:\n<color=#e85151>/kit</color> - меню наборов.\n<color=#e85151>/remove</color> - режим удаления\n<color=#e85151>/up</color> - режим улучшения.\n<color=#e85151>/trade [ник игрока]</color> - отправить запрос на обмен игроку.\n\nСписок биндов, необходимые для удобной игры:\n<color=#e85151>bind k chat.say /kit</color> - меню наборов.\n<color=#e85151>bind l chat.say /remove</color> - режим удаления.\n\nНе обязательно указывать те кнопки, что приведены в списке выше.\nНа каждую команду можете назначить любую удобную для Вас кнопку.", Color = "1 1 1 0.5", Align = TextAnchor.UpperLeft, FontSize = 12, Font = "robotocondensed-regular.ttf", FadeIn = 1f }
            }, "Info");

            CuiHelper.AddUi(player, container);
        }

        void TeleportUI(BasePlayer player) => Teleportation?.Call("TeleportUI", player);

        #endregion

        #region Хелпер
        void DestroyUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "Tp");
        }
        #endregion
    }
}