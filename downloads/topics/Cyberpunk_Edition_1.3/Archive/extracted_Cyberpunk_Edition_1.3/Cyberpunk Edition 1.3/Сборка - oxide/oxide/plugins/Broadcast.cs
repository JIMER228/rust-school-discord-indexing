using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Broadcast","Discord: Netrunner#0115","1.0")]
    public class Broadcast : RustPlugin
    {
        #region Fields

        [PluginReference] private Plugin ImageLibrary;

        private static Broadcast _;

        #endregion

        #region Config

        

        #endregion

        #region Hooks

        void OnServerInitialized()
        {
            _ = this;

            foreach (var player in BasePlayer.activePlayerList)
            {
                OnPlayerConnected(player);
            }
        }
        
        void Unload()
        {
            foreach (var miner in UnityEngine.Object.FindObjectsOfType<Broadcaster>()) UnityEngine.Object.DestroyImmediate(miner);
        }

        void OnPlayerConnected(BasePlayer player)
        {
            player.gameObject.AddComponent<Broadcaster>();
        }

        #endregion

        #region Broadcaster

        class Notification
        {
            [JsonProperty("Заголовок")]
            public string Title;
            [JsonProperty("Текст")]
            public string Text;
            [JsonProperty("Цвет")]
            public string Color;
            [JsonProperty("Картинка(название)")]
            public string Image;
            [JsonProperty("Длительность")]
            public float Duration;
            [JsonProperty("Звук")]
            public string Sound;
        }


        class Broadcaster : FacepunchBehaviour
        {
            public BasePlayer Player;
            
            private List<Notification> Query = new List<Notification>();

            private void Awake()
            {
                Player = GetComponent<BasePlayer>();
                InvokeRepeating(CheckNotification,3f,3f);
            }

            public void AddNotification(Notification notification)
            {
                Query.Add(notification);
                //CheckNotification();
            }
            
            

            public void CheckNotification()
            {
                if (Query.Count>0)
                {
                    Notification notification = Query.First();
                    _.DrawNotification(Player, notification);
                    Query.Remove(notification);
                }
                
            }

            
            
        }

        #endregion

        #region Methods

        void GetPlayerNotice(BasePlayer player, string tittle = "Test",string text = "test",string image = "", string sound = "",float duration = 3.5f)
        {
            Notification notification = new Notification
            {
                Title = tittle,
                Text = text,
                Duration = duration,
                Image = image,
                Sound = sound
            };
            player.GetComponent<Broadcaster>().AddNotification(notification);
        }

        #endregion

        #region UI
        private string _Notice = "BroadcastUI";

         void DrawNotification(BasePlayer player,Notification notification)
        {
            
            //CuiHelper.DestroyUi(player, "Notification");
            CuiHelper.DestroyUi(player, _Notice);
            var container = new CuiElementContainer();
            
            container.Add(new CuiPanel
            {
                Image = {Color = "0.3 0 0.2 0.7",FadeIn = 0.3f},
                RectTransform = {AnchorMin = "1 1",AnchorMax = "1 1",OffsetMin = "-304 -154", OffsetMax = "-10 -50"},
                FadeOut = 0.5f
            }, "Overlay", _Notice);

            /*container.Add(new CuiPanel
            {
                Image = {Color = "0 0 0 0"},
                RectTransform = {AnchorMin = "0 0",AnchorMax = "0 0",OffsetMin = "0 0",OffsetMax = "330 50"}
            }, _Notice, "Notification");*/
            /*container.Add(new CuiElement
            {
                Parent = "Notification",
                Components =
                {
                    new CuiRawImageComponent {Png = (string) ImageLibrary.Call("GetImage","")},
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",AnchorMax = "1 1"
                    }
                }
            });*/
            container.Add(new CuiLabel
            {
                Text = {Text = notification.Title,Align = TextAnchor.UpperCenter,FontSize = 16,Font = "robotocondensed-bold.ttf",FadeIn = 0.32f},
                RectTransform = {AnchorMin = "0 0",AnchorMax = "1 1"},
                FadeOut = 0.4f
            }, _Notice,_Notice+".Title");
            /*container.Add(new CuiLabel
            {
                Text = {Text = notification.Title,Align = TextAnchor.UpperCenter,FontSize = 16,Font = "robotocondensed-bold.ttf",FadeIn = 0.32f},
                RectTransform = {AnchorMin = "0 1",AnchorMax = "1 1",OffsetMin = " 0 -35",OffsetMax = "0 -1"},
                FadeOut = 0.4f
            }, _Notice,_Notice+".Title");*/
            
            container.Add(new CuiElement
            {
                Parent = _Notice,
                Components =
                {
                    new CuiRawImageComponent {Png = (string) ImageLibrary.Call("GetImage",notification.Image),FadeIn = 0.34f},
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.1 0.5",AnchorMax = "0.1 0.5",OffsetMin = "-30 -30",OffsetMax = "30 30"
                    }
                }
            });
            
            container.Add(new CuiLabel
            {
                Text = {Text = notification.Text,Align = TextAnchor.MiddleCenter,FontSize = 15,Font = "robotocondensed-regular.ttf",FadeIn = 0.34f},
                RectTransform = {AnchorMin = "0.2 0",AnchorMax = "1 1",OffsetMin = " 25 25",OffsetMax = "-5 0"},
                FadeOut = 0.35f
            }, _Notice,_Notice+".Text");
            if (notification.Sound != null && notification.Sound != "")
            {
                Effect x = new Effect(notification.Sound, player, 0, new Vector3(), new Vector3());
                EffectNetwork.Send(x, player.Connection);
            }

            CuiHelper.AddUi(player, container);

            timer.Once(notification.Duration, () =>
            {
                CuiHelper.DestroyUi(player, _Notice);
                player.GetComponent<Broadcaster>().CheckNotification();
            });
        }

        #endregion

        #region Helper

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

        #endregion
    }
}