using System.Collections;
using System.Collections.Generic;
using Oxide.Game.Rust.Cui;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Configuration;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("MapBags", "Ryamkk", "2.0.1")]
    class MapBags : RustPlugin
    {
        Dictionary<ulong, string> playerDic = new Dictionary<ulong, string>();
        
        int ImageSize = 300;

        void OnServerInitialized()
        {
            InitFileManager();
            ServerMgr.Instance.StartCoroutine(LoadImages());
        }
        
        private Dictionary<string, string> images = new Dictionary<string, string>()
        {
            ["Map"] = "https://i.imgur.com/0hNI31m.png",
            ["SbagCD"] = "https://cdn.discordapp.com/attachments/845688011597021244/845688089967984680/SbagCD.png",
            ["Sbag"] = "https://cdn.discordapp.com/attachments/845688011597021244/845688091002798090/Sbag.png",
            ["Death"] = "https://cdn.discordapp.com/attachments/845688011597021244/845688113346510868/Death.png",
        };
        
        IEnumerator LoadImages()
        {
            foreach (var name in images.Keys.ToList())
            {
                yield return m_FileManager.StartCoroutine(m_FileManager.LoadFile(name, images[name]));
                images[name] = m_FileManager.GetPng(name);
            }
        }
        
        void Unload()
        {
            UnityEngine.Object.Destroy(FileManagerObject);
            foreach (BasePlayer player in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(player, Layer);
        }

        private void OnPlayerRespawned(BasePlayer player)
        { 
            if (player.IsReceivingSnapshot)
                CuiHelper.DestroyUi(player, Layer);
        }

        private void OnPlayerInit(BasePlayer player)
        {
            if (player.IsReceivingSnapshot)
                if (player.IsDead())
                    DDrawMapBags(player);
        }

        private void OnEntityDeath(BasePlayer player, HitInfo info)
        {
            if (!player.IsConnected) return;
            
            if (playerDic.ContainsKey(player.userID)) playerDic.Remove(player.userID);
            playerDic.Add(player.userID, player.transform.position.ToString());

            timer.Once(0.5f, () => { DDrawMapBags(player); });     
        }

        string Layer = "MapBags";
        private void DDrawMapBags(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, Layer); 
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { FadeIn = 2.5f, Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0.4 0.4", AnchorMax = "0.4 0.4", OffsetMin = $"{-ImageSize} {-ImageSize}", OffsetMax = $"{ImageSize} {ImageSize}" },
            }, "Overlay", Layer);
            
            container.Add(new CuiElement
            {
                Parent = Layer,
                Name = Layer + ".BG",
                Components =
                {
                    new CuiRawImageComponent { FadeIn = 2.5f, Color = "1 1 1 1", Png = images["Map"] },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });

            if (SleepingBag.FindForPlayer(player.userID, true) != null)
            {
                int bcount = 0;
                foreach (var cBag in SleepingBag.FindForPlayer(player.userID, true))
                {
                    var anchors = ToAnchors(cBag.transform.position, 0.05f);
                    if (cBag.unlockSeconds > 0)
                    {
                        container.Add(new CuiElement
                        {
                            Parent = Layer + ".BG",
                            Name = Layer + ".BG" + ".Button",
                            Components =
                            {
                                new CuiRawImageComponent { FadeIn = 2.5f, Color = "1 1 1 1", Png = images["SbagCD"] },
                                new CuiRectTransformComponent {AnchorMin = anchors[0], AnchorMax = anchors[1]}
                            }
                        });
                    }
                    else
                    {
                        container.Add(new CuiElement
                        {
                            Parent = Layer + ".BG",
                            Name = Layer + ".BG" + ".Button",
                            Components =
                            {
                                new CuiRawImageComponent { FadeIn = 2.5f, Color = "1 1 1 1", Png = images["Sbag"] },
                                new CuiRectTransformComponent {AnchorMin = anchors[0], AnchorMax = anchors[1]}
                            }
                        });
                    }

                    container.Add(new CuiButton
                    {
                        Button = { Color = "0 0 0 0", Command = $"respawn_sleepingbag {cBag.net.ID}" },
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Text = { Text = "" }
                    }, Layer + ".BG" + ".Button");
                    bcount++;
                }
            }

            if (playerDic.ContainsKey(player.userID))
            {
                var anchorsDic = ToAnchors(playerDic[player.userID].ToVector3(), 0.03f);
                
                container.Add(new CuiElement
                {
                    Parent = Layer + ".BG",
                    Components =
                    {
                        new CuiRawImageComponent { FadeIn = 2.5f, Color = "1 1 1 1", Png = images["Death"] },
                        new CuiRectTransformComponent { AnchorMin = anchorsDic[0], AnchorMax = anchorsDic[1] }
                    }
                });
            }
            
            CuiHelper.AddUi(player, container);
        }

        string[] ToAnchors(Vector3 position, float size)
        {
            Vector2 center = ToScreenCoords(position);
            center.y = center.y + 0.02f;
            size *= 0.5f;
            return new[]
            {
                $"{center.x - size} {center.y - size}",
                $"{center.x + size} {center.y + size}",
                $"{center.x - 0.1} {center.y - size-0.04f}",
                $"{center.x + 0.1} {center.y - size+0.02}"
            };
        }
        
        Vector2 ToScreenCoords(Vector3 pos)
        {
            float pad = 2048 * 0.01f;
            pos*=0.85f;
            pos+=new Vector3(World.Size * 0.5f, 0, World.Size * 0.5f);
            pos+=new Vector3(pad * 0.5f, 0, pad * 0.5f);
            pos/=(World.Size+pad);
        
            return new Vector2(pos.x, pos.z);
        }

        private GameObject FileManagerObject;
        private FileManager m_FileManager;

        void InitFileManager()
        {
            FileManagerObject = new GameObject("FileManagerObject");
            m_FileManager = FileManagerObject.AddComponent<FileManager>();
        }

        class FileManager : MonoBehaviour
        {
            Dictionary<string, FileInfo> files = new Dictionary<string, FileInfo>();

            DynamicConfigFile dataFile = Interface.Oxide.DataFileSystem.GetFile("Images");

            private class FileInfo
            {
                public string Url;
                public string Png;
            }

            public void SaveData()
            {
                dataFile.WriteObject(files);
            }

            public string GetPng(string name) => files[name].Png;

            private void Awake()
            {
                files = dataFile.ReadObject<Dictionary<string, FileInfo>>() ?? new Dictionary<string, FileInfo>();
            }

            public IEnumerator LoadFile(string name, string url)
            {
                if (files.ContainsKey(name) && files[name].Url == url && !string.IsNullOrEmpty(files[name].Png)) yield break;
                files[name] = new FileInfo() { Url = url };
                yield return StartCoroutine(LoadImageCoroutine(name, url));
            } 

            IEnumerator LoadImageCoroutine(string name, string url)
            {
                using (WWW www = new WWW(url))
                {
                    yield return www;
                    {
                        if (string.IsNullOrEmpty(www.error))
                        {
                            var entityId = CommunityEntity.ServerInstance.net.ID;
                            var crc32 = FileStorage.server.Store(www.bytes, FileStorage.Type.png, entityId).ToString();
                            files[name].Png = crc32;
                        }
                    }
                }
            }
        }
    }
}