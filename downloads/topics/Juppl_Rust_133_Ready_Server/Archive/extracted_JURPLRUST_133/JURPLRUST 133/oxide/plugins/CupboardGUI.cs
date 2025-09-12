using System;
using System.Collections.Generic;
using System.Reflection;
using Oxide.Core.Plugins;
using System.Linq;
using UnityEngine;
using Oxide.Core;
using System.Reflection;
using Oxide.Core;
using System.Linq;
using System.Globalization;
using Facepunch;
using System.IO;
using Oxide.Game.Rust.Cui;
using Network;
using Network.Visibility;
using System.Text.RegularExpressions;
using System;
using System.Collections;
using Rust;

namespace Oxide.Plugins
{
    [Info("CupboardGUI", "UberCode", "1.0.0")]
    [Description("CupboardGUI")]
    public class CupboardGUI : RustPlugin
    {

        ImageCache ImageAssets;
        GameObject CupObject;

        private void cacheImage()
        {
            CupObject = new GameObject();
            ImageAssets = CupObject.AddComponent<ImageCache>();
            ImageAssets.imageFiles.Clear();
        }

        class ImageCache : MonoBehaviour
        {
            public Dictionary<string, string> imageFiles = new Dictionary<string, string>();

            List<Queue> queued = new List<Queue>();

            class Queue
            {
                public string url { get; set; }
                public string name { get; set; }
            }

            public void OnDestroy()
            {
                foreach (var value in imageFiles.Values)
                {
                    FileStorage.server.RemoveEntityNum(uint.MaxValue, Convert.ToUInt32(value));
                }
            }

            IEnumerator WaitForRequest(Queue queue)
            {
                using (var www = new WWW(queue.url))
                {
                    yield return www;

                    if (string.IsNullOrEmpty(www.error))
                    {
                        var stream = new MemoryStream();
                        stream.Write(www.bytes, 0, www.bytes.Length);
                        imageFiles.Add(queue.name, FileStorage.server.Store(stream, FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID).ToString());
                    }
                }
            }

            public void process()
            {
                for (int i = 0; i < 1; i++)
                    StartCoroutine(WaitForRequest(queued[i]));
            }
        }

        public string fetchImage(string name)
        {
            string result;
            if (ImageAssets.imageFiles.TryGetValue(name, out result))
                return result;
            return string.Empty;
        }

        void OnServerInitialized()
        {
            cacheImage();
        }

        void Unloaded()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, "gui");
            }
            foreach (var player in BasePlayer.activePlayerList)
            {
                Destroy(player);
            }
            UnityEngine.Object.Destroy(CupObject);
        }

        List<ulong> activeGUI = new List<ulong>();

        void Gui(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();
            if (activeGUI.Contains(player.userID)) return;
            CuiHelper.DestroyUi(player, "CupboardGui");
            CuiElement element = new CuiElement
            {
                Name = "CupboardGui",
                Components = {
                        new CuiTextComponent
                        {
                            Text = "СТРОИТЕЛЬСТВО ЗАПРЕЩЕНО",
                            FontSize = 17,
						    Color = "140.0 0.0 0.0",
                            Align = TextAnchor.MiddleCenter
                        },
						
                        new CuiOutlineComponent
                        {
                            Color = "0.3 0.0 0.0 999.9"
                        },
						
                        new CuiRectTransformComponent {
                            AnchorMin = "0.0 -0.69",
                            AnchorMax = "0.984 0.943"
					    }
                    }
            };
            container.Add(element);
            CuiHelper.AddUi(player, container);
            activeGUI.Add(player.userID);
        }	
			
        float lastTick = UnityEngine.Time.realtimeSinceStartup;

        void OnTick()
        {
            if (UnityEngine.Time.realtimeSinceStartup - lastTick < 0.1f) return;
            lastTick = UnityEngine.Time.realtimeSinceStartup;
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player.HasPlayerFlag(global::BasePlayer.PlayerFlags.InBuildingPrivilege) && !player.HasPlayerFlag(global::BasePlayer.PlayerFlags.HasBuildingPrivilege))
                {
                    Gui(player);
                    continue;
                }
                if (!player.HasPlayerFlag(global::BasePlayer.PlayerFlags.InBuildingPrivilege))
                {
                    Destroy(player);
                    continue;
                }
                if (player.HasPlayerFlag(global::BasePlayer.PlayerFlags.HasBuildingPrivilege))
                {
                    Destroy(player);
                    continue;
                }
            }
        }

        void Destroy(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "CupboardGui");
            activeGUI.Remove(player.userID);
        }
        

    }
}