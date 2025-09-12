using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("StaticButtons","Тест","0.1")]
    public class StaticButtons : RustPlugin
    {
        #region Fields

        [PluginReference] private Plugin ImageLibrary;

        #endregion

        #region Config

        

        #endregion

        #region Hooks

        void OnPlayerConnected(BasePlayer player)
        {
            StaticeButtonUI(player);
        }

        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyLayers(player);
            }
            
        }

        void OnServerInitialized()
        {

            ImageLibrary.Call("AddImage", "https://lc-crb.ru/CyberPunk/DadQ9QH.png", "menuimg"); 
            ImageLibrary.Call("AddImage", "https://lc-crb.ru/CyberPunk/gby1cxy.png", "backpackimg");
            foreach (var player in BasePlayer.activePlayerList)
            {
                StaticeButtonUI(player);
            }
        }

        #endregion

        #region UI

        void DestroyLayers(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "CTaxiBtn");
            CuiHelper.DestroyUi(player, "CFreindBtn");
            CuiHelper.DestroyUi(player, "CAlertBtn");
            CuiHelper.DestroyUi(player, "CMenuBtn");
            CuiHelper.DestroyUi(player, "CBackpackBtn");
            
        }

        void StaticeButtonUI(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();
            
            container.Add(new CuiButton
            {
                Button = {Color = "0.38 0.51 0.16 0.85",Command = "chat.say /taxi"},
                Text = {Text = "ДЕЛАМЕЙН",Align = TextAnchor.MiddleCenter},
                RectTransform = {AnchorMin = "0.5 0",AnchorMax = "0.5 0",OffsetMin = "-200 0",OffsetMax = "-75 16"}
            }, "Overlay", "CTaxiBtn");
            container.Add(new CuiButton
            {
                Button = {Color = "0.14 0.21 0.29 0.85",Command = "chat.say /fmenu"},
                Text = {Text = "КОНТАКТЫ",Align = TextAnchor.MiddleCenter},
                RectTransform = {AnchorMin = "0.5 0",AnchorMax = "0.5 0",OffsetMin = "-71 0",OffsetMax = "53 16"}
            }, "Overlay", "CFreindBtn");
            container.Add(new CuiButton
            {
                Button = {Color = "0.42 0.16 0.14 0.85",Command = "chat.say /raid"},
                Text = {Text = "ОПОВЕЩЕНИЯ",Align = TextAnchor.MiddleCenter},
                RectTransform = {AnchorMin = "0.5 0",AnchorMax = "0.5 0",OffsetMin = "57 0",OffsetMax = "181 16"}
            }, "Overlay", "CAlertBtn");

            /*container.Add(new CuiPanel
            {
                RawImage = {Png = (string) ImageLibrary.Call("GetImage","backpackimg")},
                RectTransform = {AnchorMin = "0.5 0",AnchorMax = "0.5 0",OffsetMin = "-265 16",OffsetMax = "-202 79"}
            }, "Overlay","CMenuBtn");*/
            container.Add(new CuiElement
            {
                Parent = "Overlay",
                Name = "CMenuBtn",
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Png = (string) ImageLibrary.Call("GetImage","menuimg")
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0",AnchorMax = "0.5 0",OffsetMin = "-265 17",OffsetMax = "-202 78"
                    }
                }
            });
            container.Add(new CuiButton
            {
                Button = {Command = "chat.say /menu",Color = "0.48 0.45 0.43 0.5"},
                Text = {Text =  "",Align = TextAnchor.MiddleCenter},
                RectTransform = {AnchorMin = "0 0",AnchorMax = "1 1"}
            }, "CMenuBtn");
            container.Add(new CuiElement
            {
                Parent = "Overlay",
                Name = "CBackpackBtn",
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Png = (string) ImageLibrary.Call("GetImage","backpackimg")
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0",AnchorMax = "0.5 0",OffsetMin = "182 17",OffsetMax = "245 78"
                    }
                }
            });
            container.Add(new CuiButton
            {
                Button = {Command = "chat.say /bp",Color = "0.48 0.45 0.43 0.5"},
                Text = {Text =  "",Align = TextAnchor.MiddleCenter},
                RectTransform = {AnchorMin = "0 0",AnchorMax = "1 1"}
            }, "CBackpackBtn");
            /*container.Add(new CuiPanel
            {
                RawImage = {},
                RectTransform = {AnchorMin = "0.5 0",AnchorMax = "0.5 0",OffsetMin = "182 16",OffsetMax = "245 79"}
            }, "Overlay","CBackpackBtn");*/

            CuiHelper.AddUi(player, container);
        }

        

        #endregion
    }
}