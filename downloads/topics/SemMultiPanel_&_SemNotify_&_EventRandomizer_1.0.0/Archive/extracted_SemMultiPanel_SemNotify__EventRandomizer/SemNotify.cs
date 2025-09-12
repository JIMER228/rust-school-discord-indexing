using Facepunch;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;


namespace Oxide.Plugins
{
    [Info("SemNotify", "FreePlugins", "1.0.0")]
    public class SemNotify : RustPlugin
    {
        #region Vars

        [PluginReference] private Plugin ImageLibrary;
        private static SemNotify _ins;
        private const string Layer = "NotifyUI";

        #endregion

        #region Config

        private static Configuration _config;
        public class Configuration
        {
            [JsonProperty(PropertyName = "Notify Color")] public string NotifyColor;
            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    NotifyColor = "#C3C3C325"
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) LoadDefaultConfig();
                SaveConfig();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                PrintWarning("Creating new config file.");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig() => _config = Configuration.DefaultConfig();
        protected override void SaveConfig() => Config.WriteObject(_config);

        #endregion

        #region Class | Data

        public Dictionary<BasePlayer, NotifyComponent> _playerData = new Dictionary<BasePlayer, NotifyComponent>();

        public class NotifyData
        {
            public string Type;

            public string Message;

            public float StartTime;

            public int ShowTime;

            public string UID = CuiHelper.GetGuid();

        }
        #endregion

        #region Hooks

        void OnServerInitialized()
        {
            ImageLibrary?.CallHook("AddImage", "https://i.imgur.com/JWAjSK4.png", "Info");
            ImageLibrary?.CallHook("AddImage", "https://i.imgur.com/Tv8ZlHh.png", "Event");
            _ins = this;
        }

        void Unload()
        {
            _playerData.Values.ToList().ForEach(component =>
            {
                if (component != null)
                    component.Kill();
            });
            _ins = null;
        }

        #endregion

        #region Component | Functional

        private NotifyComponent GetComponent(BasePlayer player)
        {
            NotifyComponent component;
            return _playerData.TryGetValue(player, out component)
                ? component
                : player.gameObject.AddComponent<NotifyComponent>();

        }

        public class NotifyComponent : FacepunchBehaviour
        {
            private BasePlayer player;
            public List<NotifyData> notifyList = new List<NotifyData>();

            private void Awake()
            {
                player = GetComponent<BasePlayer>();
                _ins._playerData[player] = this;
                Invoke("NotificationUpdate", 1);
            }

            public void NotificationUpdate()
            {
                CancelInvoke(NotificationUpdate);
                if (notifyList.Count == 0)
                {
                    Kill();
                    return;
                }
                List<NotifyData> result = Pool.GetList<NotifyData>();

                foreach (var key in notifyList)
                {
                    if (Time.time - key.StartTime >= key.ShowTime)
                    {
                        result.Add(key);
                    }
                }

                if (result.Count > 0)
                {
                    RemoveNotify(result);
                }
                Pool.FreeList(ref result);
                Invoke(NotificationUpdate, 1);
            }

            public void RemoveNotify(List<NotifyData> data)
            {
                foreach (var key in data)
                {
                    notifyList.Remove(key);
                }

                if (notifyList.Count == 0)
                {
                    Kill();
                    return;
                }

                ShowNotification();

            }

            public void RemoveNotify(int index)
            {
                try
                {
                    notifyList.RemoveAt(index);

                    if (notifyList.Count == 0)
                    {
                        Kill();
                        return;
                    }

                    ShowNotification();
                }
                catch
                {
                }

            }

            public void Kill()
            {
                DestroyImmediate(this);
            }

            private void OnDestroy()
            {
                CancelInvoke();

                DestroyUI();

                _ins._playerData.Remove(player);
            }

            public void DestroyUI()
            {
                foreach (var check in notifyList)
                {
                    CuiHelper.DestroyUi(player, $"{check.UID}" + "Icon");
                    CuiHelper.DestroyUi(player, $"{check.UID}" + "Text");
                    CuiHelper.DestroyUi(player, $"{check.UID}");
                }
            }


            public void AddNotification(NotifyData data)
            {
                notifyList.Add(data);

                ShowNotification();
            }

            public void ShowNotification()
            {
                DestroyUI();

                var yMin = -0.7872353;
                var yMax = -0.02127671;
                var margin = 0.95744659;

                var container = new CuiElementContainer();

                foreach (var check in notifyList)
                {
                    switch (check.Type)
                    {
                        case "Info":
                            container.Add(new CuiPanel
                            {
                                CursorEnabled = false,
                                Image = { Color = "0 0 0 0", FadeIn = 1f},
                                FadeOut = 0.5f,
                                RectTransform = { AnchorMin = $"-0.01557624 {yMin}", AnchorMax = $"1.040498 {yMax}" }
                            }, "LeftPanelLogo", $"{check.UID}");

                            container.Add(new CuiElement
                            {
                                Name = $"{check.UID}" + "Icon",
                                Parent = $"{check.UID}",
                                FadeOut = 0.5f,
                                Components = 
                                {
                                    new CuiRawImageComponent { Color = "1 1 1 1", Png = _ins.ImageLibrary.Call<string>("GetImage", "Info"), FadeIn = 1f },
                                    new CuiRectTransformComponent { AnchorMin = "0.01718206 0.06481483", AnchorMax = "0.1271477 0.953703" }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Name = $"{check.UID}" + "Text",
                                Parent = $"{check.UID}",
                                FadeOut = 0.5f,
                                Components = 
                                {
                                    new CuiTextComponent { Text = $"{check.Message}", Font = "robotocondensed-bold.ttf", FontSize = 11, Align = TextAnchor.UpperLeft, Color = "1 1 1 0.8", FadeIn = 1f },

                                    new CuiRectTransformComponent { AnchorMin = "0.1580756 -0.2777756", AnchorMax = "0.9828178 1" }
                                }
                            });
                            break;
                        case "Event":
                            container.Add(new CuiPanel
                            {
                                CursorEnabled = false,
                                Image = { Color = "0 0 0 0", FadeIn = 1f },
                                FadeOut = 0.5f,
                                RectTransform = { AnchorMin = $"-0.01557624 {yMin}", AnchorMax = $"1.040498 {yMax}" }
                            }, "LeftPanelLogo", $"{check.UID}");

                            container.Add(new CuiElement
                            {
                                Name = $"{check.UID}" + "Icon",
                                Parent = $"{check.UID}",
                                FadeOut = 0.5f,
                                Components =
                                {
                                    new CuiRawImageComponent { Color = "1 1 1 1", Png = _ins.ImageLibrary.Call<string>("GetImage", "Event"), FadeIn = 1f },
                                    new CuiRectTransformComponent { AnchorMin = "0.01718206 0.06481483", AnchorMax = "0.1271477 0.953703" }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Name = $"{check.UID}" + "Text",
                                Parent = $"{check.UID}",
                                FadeOut = 0.5f,
                                Components =
                                {
                                    new CuiTextComponent { Text = $"{check.Message}", Font = "robotocondensed-bold.ttf", FontSize = 11, Align = TextAnchor.UpperLeft, Color = "1 1 1 0.8", FadeIn = 1f },

                                    new CuiRectTransformComponent { AnchorMin = "0.1580756 -0.2777756", AnchorMax = "0.9828178 1" }
                                }
                            });
                            break;

                        default:

                            break;
                    }
                    yMin -= margin;
                    yMax -= margin;
                    _ins.timer.Once(check.ShowTime, () => 
                    {

                        CuiHelper.DestroyUi(player, $"{check.UID}" + "Icon");
                        CuiHelper.DestroyUi(player, $"{check.UID}" + "Text");
                        CuiHelper.DestroyUi(player, $"{check.UID}");
                    });
                }
                CuiHelper.AddUi(player, container);
            }

            private static string HexToCuiColor(string hex)
            {
                if (string.IsNullOrEmpty(hex)) hex = "#FFFFFFFF";
                var str = hex.Trim('#');
                if (str.Length == 6) str += "FF";
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

        #endregion

        #region API

        private void SendNotify(string userId, string type, string message, int show)
        {
            SendNotify(BasePlayer.FindByID(ulong.Parse(userId)), type, message, show);
        }

        private void SendNotify(ulong userId, string type, string message, int show)
        {
            SendNotify(BasePlayer.FindByID(userId), type, message, show);
        }

        private void SendNotify(BasePlayer player, string type, string message, int show)
        {
            if (player == null) return;

            var component = player.GetComponent<NotifyComponent>();

            var notify = GetComponent(player);
            if (notify == null) return;

            var data = new NotifyData
            {
                Type = type,
                Message = message,
                StartTime = Time.time,
                ShowTime = show,
            };
            notify.AddNotification(data);
        }

        #endregion

    }
}