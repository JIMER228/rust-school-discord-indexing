using System;
using System.Globalization;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("PNPCUI","Netrunner","1.0")]
    public class PNPCUI: RustPlugin
    {
        #region Fields

        [PluginReference] private Plugin ImageLibrary,PersonalNPC;

        #endregion


        #region Hooks

        void OnServerInitialized()
        {
            ImageLibrary.Call("AddImage", "https://i.ibb.co/5xwrrdP/ico.png","pnpc");
        }

        #endregion

        #region UI

        private string _layer = "PNPCLayer";
        void ShowUI(BasePlayer player)
        {
            bool spawned = (bool) PersonalNPC.Call("HasBot", player);
            CuiHelper.DestroyUi(player, _layer);
            CuiElementContainer container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                Image = {Color = "0 0 0 0.3"},
                RectTransform = {AnchorMin = "0.5 0.5",AnchorMax = "0.5 0.5",OffsetMin = "-80 -280",OffsetMax = "80 120"}
            }, "ContentUI", _layer);
            container.Add(new CuiElement
            {
                Parent = _layer,
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Png = (string) ImageLibrary.Call("GetImage","pnpc")
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 1",AnchorMax = "0.5 1",OffsetMin = "-60 -130",OffsetMax = "60 -10"
                    }
                }
            });
            container.Add(new CuiPanel
            {
                Image = {Color = "1 0 0 0.4"},
                RectTransform = {AnchorMin = "0 1",AnchorMax = "1 1",OffsetMin = "0 -200",OffsetMax = "0 -140"}
            }, _layer, "PNPCDescription");
            container.Add(new CuiLabel
            {
                Text = {Text = "ВАШ ТЕЛОХРАНИТЕЛЬ",Align = TextAnchor.MiddleCenter},
                RectTransform = {AnchorMin = "0 0",AnchorMax = "1 1"}
            }, "PNPCDescription");
            if (spawned)
            {
                container.Add(new CuiButton
                {
                    RectTransform = {AnchorMin = "0 1",AnchorMax = "0.5 1",OffsetMin = "0 -250",OffsetMax = "0 -200"},
                    Button = {Command = "pnpc command /pnpc follow",Color = HexToRustFormat("#FADE87")},
                    Text = {Text = "ПОДОЗВАТЬ",Align = TextAnchor.MiddleCenter,Color = "0 0 0 1"}
                }, _layer);
                container.Add(new CuiButton
                {
                    RectTransform = {AnchorMin = "0.5 1",AnchorMax = "1 1",OffsetMin = "0 -250",OffsetMax = "0 -200"},
                    Button = {Command = "pnpc command /pnpc combat",Color = HexToRustFormat("#FADE87")},
                    Text = {Text = "АВТОАТАКА",Align = TextAnchor.MiddleCenter,Color = "0 0 0 1"}
                }, _layer);
                container.Add(new CuiButton
                {
                    RectTransform = {AnchorMin = "0 1",AnchorMax = "0.5 1",OffsetMin = "0 -300",OffsetMax = "0 -250"},
                    Button = {Command = "pnpc command /pnpc loot-all",Color = HexToRustFormat("#FADE87")},
                    Text = {Text = "АВТОЛУТ",Align = TextAnchor.MiddleCenter,Color = "0 0 0 1"}
                }, _layer);
                container.Add(new CuiButton
                {
                    RectTransform = {AnchorMin = "0.5 1",AnchorMax = "1 1",OffsetMin = "0 -300",OffsetMax = "0 -250"},
                    Button = {Command = "pnpc command /pnpc inventory",Color = HexToRustFormat("#FADE87")},
                    Text = {Text = "ИНВЕНТАРЬ",Align = TextAnchor.MiddleCenter,Color = "0 0 0 1"}
                }, _layer);
            }
            container.Add(new CuiButton
            {
                Button = {Command = "pnbtn",Color = HexToRustFormat("#FADE87")},
                Text = {Text = spawned?"УБИТЬ":"ПРИЗВАТЬ",Align = TextAnchor.MiddleCenter,FontSize = 32,Color = "0 0 0 1"},
                RectTransform = {AnchorMin = "0 0",AnchorMax = "1 0",OffsetMin = "0 10",OffsetMax = "0 60"}
            }, _layer);
            CuiHelper.AddUi(player, container);
        }

        #endregion

        [ConsoleCommand("pncpc")]
        void PNPCCombat(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
            {
                return;
            }
            arg.Player().SendConsoleCommand("chat.say /pnpc","combat");
        }

        [ConsoleCommand("openpncui")]
        void OpenUICommand(ConsoleSystem.Arg arg)
        {
            ShowUI(arg.Player());
        }

        [ConsoleCommand("pnbtn")]
        void BtnPressed(ConsoleSystem.Arg arg)
        {
            arg.Player().SendConsoleCommand("chat.say /pnpc");
            arg.Player().SendConsoleCommand("chat.say /mynpc");
            //NextFrame(() => ShowUI(arg.Player()));
        }
        private static string HexToRustFormat(string hex)
        {
            if (string.IsNullOrEmpty(hex))
            {
                hex = "#FFFFFFFF";
            }

            var str = hex.Trim('#');

            if (str.Length == 6)
                str += "FF";

            if (str.Length != 8)
            {
                throw new Exception(hex);
                throw new InvalidOperationException("Cannot convert a wrong format.");
            }

            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);
            var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);

            Color color = new Color32(r, g, b, a);

            return string.Format("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a);
        }
    }
}