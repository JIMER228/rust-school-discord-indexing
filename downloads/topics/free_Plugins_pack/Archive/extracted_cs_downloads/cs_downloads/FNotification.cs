using System;
using System.Collections.Generic;
using System.Globalization;
using Oxide.Plugins.FNotificationExtensionMethods;
using Facepunch;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("FNotification", "Sempai#3239", "1.0.0")]
    public class FNotification : RustPlugin
    {
        #region Var

        
        [PluginReference] private Plugin ImageLibrary, FPanel;
        private static FNotification _ins;
        private const string Layer = "UI_NotifyUI";
        
        #endregion


        #region Class | Data

        public Dictionary<BasePlayer, NotifyComponent> _playerData = new Dictionary<BasePlayer, NotifyComponent>();
        
        public class NotifyData
        {
            public string Content;

            public string Message;

            public string Image;
            
            public float StartTime;

            public int ShowTime;


            public string OffsetMin;

            public string OffsetMax;


        }
        

        #endregion


        #region Hooks

        void OnServerInitialized()
        {
            ImageLibrary?.CallHook("AddImage", "https://gspics.org/images/2024/03/23/0NTEbv.png", "close");
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
            Oxide.Plugins.FNotificationExtensionMethods.ExtensionMethods.p = null;
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

        [ConsoleCommand("close.notify")]
        void CloseNotify(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();

            var component = GetComponent(player);
            if (component == null) return;
            int index = int.Parse(args.Args[0]);
            component.RemoveNotify(index);
        }
        
        public class NotifyComponent : FacepunchBehaviour
        {

            private BasePlayer player;
            public List<NotifyData> notifyList = new List<NotifyData>();
            public bool openPanel = false;

            private void Awake()
            {
                player = GetComponent<BasePlayer>();
                _ins._playerData[player] = this;
                openPanel = isOpen();
                Invoke("NotificationUpdate", 1);
            }


            public bool isOpen()
            {
                if (!_ins.plugins.Find("FPanel"))
                    return false;
                if ((bool)_ins.FPanel?.CallHook("IsOpen", player.userID))
                    return true;
                return false;
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
                for (int i = 0; i < 5; i++)
                {
                    CuiHelper.DestroyUi(player, Layer + $"{i}.FNotify");
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
                
                if (openPanel) return;
                
                var ySwitchMin = -111;
                var ySwitchMax = -56;
                
                var container = new CuiElementContainer();

                foreach (var check in notifyList.Select((i, t) => new { A = i, B = t }).Take(5))
                {
                    
                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = $"4 {ySwitchMin}",
                            OffsetMax = $"263 {ySwitchMax}"
                        },
                        Image = { Color = HexToCuiColor("#fffdfc", 20), Sprite = "assets/content/ui/ui.background.tile.psd" }
                    }, "Overlay", Layer + $"{check.B}.FNotify");
                    
                    
                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0.02316605 0.1151512", AnchorMax = "0.1879022 0.8909093"
                        },
                        Image =
                        {
                            Color = HexToCuiColor("#7b7774"),
                        }
                    },Layer + $"{check.B}.FNotify", $"{check.B}.Image");
                    
                    

                    container.Add(new CuiElement
                    {
                        Parent = $"{check.B}.Image",
                        Components =
                        {
                            new CuiRawImageComponent
                                {FadeIn = 0.3f, Png = (string)_ins.ImageLibrary.Call("GetImage", check.A.Image)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = check.A.OffsetMin, OffsetMax = check.A.OffsetMax}
                        }
                    });
                    
                    container.Add(new CuiElement() // content
                    {
                        Parent = Layer + $"{check.B}.FNotify",
                        Components =
                        {
                            new CuiTextComponent{Color = HexToCuiColor("#fcf7f6"),Text = $"{check.A.Content}", Align = TextAnchor.LowerLeft, FontSize = 17, Font = "robotocondensed-bold.ttf"},
                            new CuiRectTransformComponent{AnchorMin = "0.2084941 0.5757585", AnchorMax = "0.9060489 1"},
                            new CuiOutlineComponent {Color = "0 0 0 1", Distance = "0.3 0.3"},
                        }
                    });
                    
                    container.Add(new CuiElement() // message
                    {
                        Parent = Layer + $"{check.B}.FNotify",
                        Components =
                        {
                            new CuiTextComponent{Color = HexToCuiColor("#fcf7f6"),Text = $"{check.A.Message}", Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf"},
                            new CuiRectTransformComponent{AnchorMin = "0.2084941 0.05454618", AnchorMax = "0.9060489 0.5757581"},
                            new CuiOutlineComponent {Color = "0 0 0 1", Distance = "0.3 0.3"},
                        }
                    });
                    
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0.942085 0.7333335", AnchorMax = "0.995 0.990"},
                        Button =
                        {
                            Command = $"close.notify {notifyList.IndexOf(check.A)}",
                            Sprite = "assets/content/ui/ui.background.tile.psd",
                            Color = HexToCuiColor("#ac555c")
                        },
                        Text =
                        {
                            Text = "", Color = HexToCuiColor($"#e1d6cc"),
                            Align = TextAnchor.MiddleCenter, FontSize = 13, Font = "robotocondensed-bold.ttf"
                        }
                    }, Layer + $"{check.B}.FNotify", $"{check.B}.Close");
                    
                    container.Add(new CuiElement
                    {
                        Parent = $"{check.B}.Close",
                        Components =
                        {
                            new CuiRawImageComponent
                                {FadeIn = 0.3f, Png = (string)_ins.ImageLibrary.Call("GetImage", "close")},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "3 3", OffsetMax = "-3 -3"}
                        }
                    });
                    
                    
                    ySwitchMin += -59;
                    ySwitchMax += -59;
                }
                


                CuiHelper.AddUi(player, container);
            }
            
            public string HexToCuiColor(string HEX, float Alpha = 100)
            {
                if (string.IsNullOrEmpty(HEX)) HEX = "#FFFFFF";

                var str = HEX.Trim('#');
                var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
                var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
                var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

                return $"{(double) r / 255} {(double) g / 255} {(double) b / 255} {Alpha / 100}";
            }
        }
        

        #endregion

        #region API

        
        private void SendNotify(string userId, string content, string message, string image, string offmin, string offmax, int show)
        {
            SendNotify(BasePlayer.FindByID(ulong.Parse(userId)), content, message, image, offmin, offmax, show);
        }

        private void SendNotify(ulong userId, string content, string message, string image, string offmin, string offmax, int show)
        {
            SendNotify(BasePlayer.FindByID(userId), content, message, image, offmin, offmax, show);
        }

        private void SendNotify(BasePlayer player, string content, string message, string image, string offmin, string offmax, int show)
        {
            if (player == null) return;

            var component = player.GetComponent<NotifyComponent>();
            if (component != null)
            {
               if (component.notifyList.FirstOrDefault(p => p.Image == image) != null)
                   return;
            }


            var notify = GetComponent(player);
            if (notify == null) return;
            
            // ReSharper disable once PossibleInvalidOperationException
            if (ImageLibrary?.Call<bool>("HasImage", image) == false)
            {
                
                ImageLibrary?.CallHook("AddImage", image, image);
            }

            var find = notify.notifyList.FirstOrDefault(p => p.Image == image);
            if (find != null) return;
                
            
            var data = new NotifyData
            {
                Content = content,
                Message = message,
                Image = image,
                StartTime = Time.time,
                ShowTime = show,
                OffsetMin = offmin,
                OffsetMax = offmax
            };
            notify.AddNotification(data);
        }
        
        

        #endregion

        #region FPanel


        public void OpenPanel(BasePlayer player)
        {
            var component = GetComponent(player);
            if (component == null) return;
            component.openPanel = true;
            component.ShowNotification();
        }

        public void DestroyPanel(BasePlayer player)
        {
            var component = GetComponent(player);
            if (component == null) return;
            component.openPanel = false;
            component.ShowNotification();
        }

        #endregion
       
    }
}

#region Linq

namespace Oxide.Plugins.FNotificationExtensionMethods
{
    public static class ExtensionMethods
    {
        internal static Core.Libraries.Permission p;
        public static bool All<T>(this IList<T> a, Func<T, bool> b) { for (int i = 0; i < a.Count; i++) { if (!b(a[i])) { return false; } } return true; }
        public static int Average(this IList<int> a) { if (a.Count == 0) { return 0; } int b = 0; for (int i = 0; i < a.Count; i++) { b += a[i]; } return b / a.Count; }
        public static T ElementAt<T>(this IEnumerable<T> a, int b) { using (var c = a.GetEnumerator()) { while (c.MoveNext()) { if (b == 0) { return c.Current; } b--; } } return default(T); }
        public static bool Exists<T>(this HashSet<T> a) where T : BaseEntity { foreach (var b in a) { if (!b.IsKilled()) { return true; } } return false; }
        public static bool Exists<T>(this IEnumerable<T> a, Func<T, bool> b = null) { using (var c = a.GetEnumerator()) { while (c.MoveNext()) { if (b == null || b(c.Current)) { return true; } } } return false; }
        public static T FirstOrDefault<T>(this IEnumerable<T> a, Func<T, bool> b = null) { using (var c = a.GetEnumerator()) { while (c.MoveNext()) { if (b == null || b(c.Current)) { return c.Current; } } } return default(T); }
        public static int RemoveAll<TKey, TValue>(this IDictionary<TKey, TValue> c, Func<TKey, TValue, bool> d) { int a = 0; foreach (var b in c.ToList()) { if (d(b.Key, b.Value)) { c.Remove(b.Key); a++; } } return a; }
        public static IEnumerable<V> Select<T, V>(this IEnumerable<T> a, Func<T, V> b) { var c = new List<V>(); using (var d = a.GetEnumerator()) { while (d.MoveNext()) { c.Add(b(d.Current)); } } return c; }
        public static Dictionary<T, V> ToDictionary<S, T, V>(this IEnumerable<S> a, Func<S, T> b, Func<S, V> c) { var d = new Dictionary<T, V>(); using (var e = a.GetEnumerator()) { while (e.MoveNext()) { d[b(e.Current)] = c(e.Current); } } return d; }
        public static List<T> ToList<T>(this IEnumerable<T> a) { var b = new List<T>(); if (a == null) { return b; } using (var c = a.GetEnumerator()) { while (c.MoveNext()) { b.Add(c.Current); } } return b; }
        public static List<T> Where<T>(this IEnumerable<T> a, Func<T, bool> b) { var c = new List<T>(); using (var d = a.GetEnumerator()) { while (d.MoveNext()) { if (b(d.Current)) { c.Add(d.Current); } } } return c; }
        public static List<T> OfType<T>(this IEnumerable<BaseNetworkable> a) where T : BaseEntity { var b = new List<T>(); using (var c = a.GetEnumerator()) { while (c.MoveNext()) { if (c.Current is T) { b.Add(c.Current as T); } } } return b; }
        public static int Sum<T>(this IList<T> a, Func<T, int> b) { int c = 0; for (int i = 0; i < a.Count; i++) { var d = b(a[i]); if (!float.IsNaN(d)) { c += d; } } return c; }
        public static bool HasPermission(this string a, string b) { if (p == null) { p = Interface.Oxide.GetLibrary<Core.Libraries.Permission>(null); } return !string.IsNullOrEmpty(a) && p.UserHasPermission(a, b); }
        public static bool HasPermission(this BasePlayer a, string b) { return a.UserIDString.HasPermission(b); }
        public static bool HasPermission(this ulong a, string b) { return a.ToString().HasPermission(b); }
        public static bool IsReallyConnected(this BasePlayer a) { return a.IsReallyValid() && a.net.connection != null; }
        public static bool IsKilled(this BaseNetworkable a) { return (object)a == null || a.IsDestroyed; }
        public static bool IsNull<T>(this T a) where T : class { return a == null; }
        public static bool IsNull(this BasePlayer a) { return (object)a == null; }
        public static bool IsReallyValid(this BaseNetworkable a) { return !((object)a == null || a.IsDestroyed || (object)a.net == null); }
        public static void SafelyKill(this BaseNetworkable a) { if (a.IsKilled()) { return; } a.Kill(BaseNetworkable.DestroyMode.None); }
        public static bool CanCall(this Plugin o) { return (object)o != null && o.IsLoaded; }
        public static bool IsInBounds(this OBB o, Vector3 a) { return o.ClosestPoint(a) == a; }
        public static bool IsHuman(this BasePlayer a) { return !(a.IsNpc || !a.userID.IsSteamId()); }
        public static bool IsCheating(this BasePlayer a) { return a._limitedNetworking || a.IsFlying || a.UsedAdminCheat(30f) || a.IsGod() || a.metabolism?.calories?.min == 500; }
        public static void SetAiming(this BasePlayer a, bool f) { a.modelState.aiming = f; a.SendNetworkUpdate(); }
        public static BasePlayer ToPlayer(this IPlayer user) { return user.Object as BasePlayer; }
        public static string ObjectName(this Collider collider) { try { return collider.gameObject?.name ?? string.Empty; } catch { return string.Empty; } }
        public static Vector3 GetPosition(this Collider collider) { try { return collider.transform?.position ?? Vector3.zero; } catch { return Vector3.zero; } }
        public static string ObjectName(this BaseEntity entity) { try { return entity.name ?? string.Empty; } catch { return string.Empty; } }
        public static T GetRandom<T>(this HashSet<T> h) { if (h == null || h.Count == 0) { return default(T); } return h.ElementAt(UnityEngine.Random.Range(0, h.Count)); }
        public static int InventorySlots(this StorageContainer a) { if (a.IsKilled() || a.inventory == null) return 0; return a.inventorySlots; }
        public static IEnumerable<V> Select<T, V>(this IEnumerable<T> a, Func<T, int, V> b) { int c = -1; foreach (T d in a) { yield return b(d, ++c); } }
        public static IEnumerable<T> Skip<T>(this IEnumerable<T> a, int b) { using (var e = a.GetEnumerator()) { while (b > 0 && e.MoveNext()) { b--; } if (b <= 0) { while (e.MoveNext()) { yield return e.Current; } } } }
        public static IEnumerable<T> Take<T>(this IEnumerable<T> a, int b) { if (b <= 0) { yield break; } foreach (T c in a) { yield return c; int d = b - 1; b = d; if (d == 0) { break; } } }
    }
}

#endregion