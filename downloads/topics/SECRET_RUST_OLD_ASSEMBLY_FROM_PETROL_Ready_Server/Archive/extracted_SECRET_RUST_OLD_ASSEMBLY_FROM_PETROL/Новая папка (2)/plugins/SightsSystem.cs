using System.Linq;
using System.Collections.Generic;
using Oxide.Core;
using UnityEngine;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("SightsSystem", "Chibubrik", "1.1.0")]
    class SightsSystem : RustPlugin
    {
        #region Вар
        string Layer = "Sights_UI";

        [PluginReference] Plugin ImageLibrary;

        public Dictionary<ulong, string> DB = new Dictionary<ulong, string>();
        #endregion

        #region Хуки
        void OnServerInitialized()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile("SightsSystem/PlayerList"))
                DB = Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, string>>("SightsSystem/PlayerList");
            foreach (var check in Hair)
                ImageLibrary.Call("AddImage", check, check);

            foreach (var check in BasePlayer.activePlayerList)
                OnPlayerConnected(check);
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (!DB.ContainsKey(player.userID))
                DB.Add(player.userID, "https://imgur.com/EJsPEkn.png");

            int x = 0;
            for (int z = 0; z < Hair.Count(); z++)
                x = z;

            if (DB[player.userID] != Hair.ElementAt(x))
                HairUI(player);
        }

        void OnPlayerDisconnected(BasePlayer player) => SaveDataBase();

        void Unload() => SaveDataBase();

        void SaveDataBase() => Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("SightsSystem/PlayerList", DB);
        #endregion

        #region Картинки прицелов
        List<string> Hair = new List<string>()
        {
            "https://imgur.com/O1T5M2S.png",
            "https://imgur.com/udgZFcU.png",
            "https://imgur.com/7zs9aHt.png",
            "https://imgur.com/iCrNfVl.png",
            "https://imgur.com/lBZ2Khj.png",
	    "https://cdn.discordapp.com/attachments/903350610932416602/964233950287495278/icons8---96.png",
	    "https://cdn.discordapp.com/attachments/903350610932416602/964233950065217576/icons8--96.png",
	    "https://i.imgur.com/mIbPpj3.png",
	    "https://i.imgur.com/XCSkVNk.png",
	    "https://i.imgur.com/RACMuqg.png",
	    "https://i.imgur.com/tqtF73m.png",
	    "https://i.imgur.com/uIHaR7Q.png",
	    "https://i.imgur.com/Dbxnsm1.png",
	    "https://i.imgur.com/bzsU7kE.png",
	    "https://i.imgur.com/2Wke9lp.png",
            "https://imgur.com/EJsPEkn.png"
        };
        #endregion

        #region Команды
        [ChatCommand("hair")]
        void ChatHair(BasePlayer player) => SightsUI(player);

        [ConsoleCommand("hair")]
        void ConsoleHair(ConsoleSystem.Arg args)
        {
            var player = args.Player();

            int id = int.Parse(args.Args[0]);
            DB[player.userID] = Hair.ElementAt(id);
            InterfaceUI(player);

            int x = 0;
            for (int z = 0; z < Hair.Count(); z++)
                x = z;

            if (id == x)
                CuiHelper.DestroyUi(player, "Hair");
            else
                HairUI(player);
        }
        #endregion

        #region Интерфейс
        void SightsUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, Layer);
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0.9", Material = "assets/content/ui/uibackgroundblur.mat" }
            }, "Overlay", Layer);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Close = Layer },
                Text = { Text = "" }
            }, Layer);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0.54", AnchorMax = "1 0.61", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0" },
                Text = { Text = $"<size=20>ПРИЦЕЛЫ</size>\nЗдесь, вы можете выбрать прицел!", Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 12 }
            }, Layer);

            CuiHelper.AddUi(player, container);
            InterfaceUI(player);
        }

        void InterfaceUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "Hairs");
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = $"0.255 0.395", AnchorMax = $".745 0.61", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, Layer, "Hairs");

            float width = 0.1666f, height = 0.62f, startxBox = 0f, startyBox = 0.68f - height, xmin = startxBox, ymin = startyBox;
            int z = 0;
            foreach(var check in Hair)
            {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = xmin + " " + ymin, AnchorMax = (xmin + width) + " " + (ymin + height * 1), OffsetMin = "3 0", OffsetMax = "-3 0" },
                    Button = { Color = "1 1 1 0.1", Command = $"hair {z}" },
                    Text = { Text = "" }
                }, "Hairs", "Image");

                container.Add(new CuiElement
                {
                    Parent = "Image",
                    Components =
                    {
                        new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", Hair.ElementAt(z)), Color = "1 1 1 0.3" },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "20 20", OffsetMax = "-20 -20" }
                    }
                });

                var color = DB[player.userID] == check ? "0.71 0.24 0.24 1" : "0.28 0.28 0.28 1";
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.03", OffsetMax = "0 0" },
                    Image = { Color = color }
                }, "Image");

                xmin += width;
                if (xmin + width >= 1)
                {
                    xmin = startxBox;
                    ymin -= height;
                }
                z++;
            }

            CuiHelper.AddUi(player, container);
        }

        void HairUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "Hair");
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, "Hud", "Hair");

            container.Add(new CuiElement
            {
                Parent = "Hair",
                Components =
                {
                    new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", DB[player.userID]), Color = "1 1 1 0.8" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "-10 -10", OffsetMax = "10 10" }
                }
            });

            CuiHelper.AddUi(player, container);
        }
        #endregion
    }
}