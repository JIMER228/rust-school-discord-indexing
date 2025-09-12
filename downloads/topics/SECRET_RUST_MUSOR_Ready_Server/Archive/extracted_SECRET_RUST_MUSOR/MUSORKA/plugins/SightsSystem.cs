using System.Linq;
using System.Collections.Generic;
using Oxide.Core;
using UnityEngine;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using System;
////update 1.3.0///
  ///Пофикшен интерфейс кнопок изменения размеров прицела///

namespace Oxide.Plugins
{
    [Info("SightsSystem", "", "1.3.0")]
    class SightsSystem : RustPlugin
    {
        #region Вар
        string Layer = "Sights_UI";
       

        [PluginReference] Plugin ImageLibrary;
        public Dictionary<ulong, float> SZ = new Dictionary<ulong, float>();
        public Dictionary<ulong, string> DB = new Dictionary<ulong, string>();
        #endregion

        #region Хуки
        void OnServerInitialized()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile("SightsSystem/PlayerList"))
                DB = Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, string>>("SightsSystem/PlayerList");
            if (Interface.Oxide.DataFileSystem.ExistsDatafile("SightsSystem/PlayerSizes"))
                SZ = Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, float>>("SightsSystem/PlayerSizes");
            foreach (var check in Hair)
                ImageLibrary.Call("AddImage", check, check);

            foreach (var check in BasePlayer.activePlayerList)
                OnPlayerConnected(check);
            AddCovalenceCommand("SizePlus", nameof(CmdPlusSize));
            AddCovalenceCommand("SizeMinus", nameof(CmdMinusSize));

        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (!DB.ContainsKey(player.userID))
                DB.Add(player.userID, "https://imgur.com/EJsPEkn.png");
            if (!SZ.ContainsKey(player.userID))
                SZ.Add(player.userID, 10f);

            int x = 0;
            for (int z = 0; z < Hair.Count(); z++)
                x = z;

            if (DB[player.userID] != Hair.ElementAt(x))
                HairUI(player);
        }

        void OnPlayerDisconnected(BasePlayer player) => SaveDataBase();

        void Unload()
        {
            SaveDataBase();

            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, Layer);
            }
        }
      

        void SaveDataBase()
        {
            Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("SightsSystem/PlayerList", DB);
            Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("SightsSystem/PlayerSizes", SZ);
        }

       
        #endregion

        #region Картинки прицелов
        List<string> Hair = new List<string>()
        {
            "https://imgur.com/O1T5M2S.png",
            "https://imgur.com/udgZFcU.png",
            "https://imgur.com/7zs9aHt.png",
            "https://imgur.com/iCrNfVl.png",
            "https://imgur.com/lBZ2Khj.png",
	        "https://configs-csgo.ru/upload/000/u1/6/e/6e22fbf2.png",
	        "https://imgur.com/udgZFcU.png",
	        "https://media.discordapp.net/attachments/1006602859636346880/1044627746933964890/Untitled-2.png",
	        "https://i.imgur.com/XCSkVNk.png",
	        "https://i.imgur.com/mIbPpj3.png",
	        "https://i.imgur.com/RACMuqg.png",
	        "https://i.imgur.com/tqtF73m.png",
	        "https://cdn.discordapp.com/attachments/1060231324109119559/1069567043982086194/10dbace8c3d8593f.png",
            "https://cdn.discordapp.com/attachments/1060231324109119559/1061788652063178772/-2.png"
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

        private void CmdPlusSize(IPlayer cov, string command, string[] args)
        {
            var player = cov?.Object as BasePlayer;
            if (player == null) return;
            if (SZ[player.userID] == 10) return;
            SZ[player.userID] += 1;
            HairUI(player);
            SightsUI(player);
        }

        private void CmdMinusSize(IPlayer cov, string command, string[] args)
        {
            var player = cov?.Object as BasePlayer;
            if (player == null) return;
            if (SZ[player.userID] == 1) return;
            SZ[player.userID] -= 1;
            HairUI(player);
            SightsUI(player);
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
                Image = { Material = "assets/content/ui/uibackgroundblur.mat", Color = "0 0 0 0.5" }
            }, "Overlay", Layer);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0.6" },
                Text = { Text = "" }
            }, Layer);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 1", OffsetMax = "0 0" },
                Button = { Color = "1.00 1.00 1.00 0.025", Material = "assets/content/ui/scope_2.mat", Close = Layer },
                Text = { Text = "" }
            }, Layer);

            container.Add(new CuiPanel
            {
                Image = { Color = "0.36 0.34 0.32 0.72", Material = "assets/icons/greyout.mat" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-319 -37", OffsetMax = "322 176" },
            }, Layer, "Layer1");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0.85", AnchorMax = "1 1"},
                Button = { Color = "0 0 0 0" },
                Text = { Text = $"Список прицелов, которые вы можете выбрать", Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 24 }
            }, "Layer1");

            

            container.Add(new CuiPanel
            {
                Image = { Color = "0.36 0.34 0.32 0.72", Material = "assets/icons/greyout.mat" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-150 -60", OffsetMax = "150 -40" }
            }, Layer, "Resize");

            container.Add(new CuiPanel
            {
                Image = { Color = "0.16 0.16 0.16 1", Material = "assets/icons/greyout.mat" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "15 5", OffsetMax = "-15 -5" }
            }, "Resize", "LineBG");
            
            var size = SZ[player.userID];
            float result = size / 10;
            

            container.Add(new CuiPanel
            {
                Image = { Color = "248 255 0 1", Material = "assets/icons/greyout.mat" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = $"{result} 1", OffsetMin = "15 5", OffsetMax = "-15 -5" }
            }, "Resize", "LineWrapper");

            container.Add(new CuiPanel
            {
                Image = { Color = "0.36 0.34 0.32 0.72", Material = "assets/icons/greyout.mat" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "152 -60", OffsetMax = "172 -40" }
            }, Layer, "PlusParent");

            container.Add(new CuiPanel
            {
                Image = { Color = "0.36 0.34 0.32 0.72", Material = "assets/icons/greyout.mat" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "174 -60", OffsetMax = "194 -40" }
            }, Layer, "MinusParent");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Command = "SizePlus" },
                Text = { Text = "+", Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 17 }
            }, "PlusParent");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Command = "SizeMinus" },
                Text = { Text = "-", Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 17 }
            }, "MinusParent");



            CuiHelper.AddUi(player, container);
            InterfaceUI(player);
        }

        void InterfaceUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "Hairs");
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, "Layer1", "Hairs");

            float width = 0.13f, height = 0.38f, startxBox = 0.0425f, startyBox = 0.8575f - height, xmin = startxBox, ymin = startyBox;
            int z = 0;
            foreach(var check in Hair)
            {

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmin + width} {ymin + height * 1}", OffsetMin = "2 2", OffsetMax = "-2 -2" },
                    Button = { Color = "0 0 0 0.45", Command = $"hair {z}", Material = "assets/icons/greyout.mat" },
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

                var color = DB[player.userID] == check ? "0.00 0.84 0.47 1.00" : "0 0 0 0";
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
            var size = SZ[player.userID];
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
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"-{size} -{size}", OffsetMax = $"{size} {size}" }
                }
            });

            CuiHelper.AddUi(player, container);
        }
        #endregion
    }
}