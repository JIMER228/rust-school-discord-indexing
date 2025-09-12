using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Obmen", "Molik", "1.1.1")]
    class Obmen : RustPlugin
    {
        private bool loaded = false;

        [JsonProperty("Основной слой интерфейса")]
        private string Layer = "Obmen";

        string[] FirstWord = { "200К КАМНЯ = 600К ДЕРЕВА", "200К ДЕРЕВА = 100К УГЛЯ", "100К ЖЕЛЕЗА = 25К СЕРЫ", "10К ЖЕЛЕЗА = БУР", "100К ЖЕЛЕЗА = 10К ПОРОХА", "100К ЖЕЛЕЗА = 5К СКРАПА" };
        string[] Command = { "obmen 0", "obmen 1", "obmen 2", "obmen 3", "obmen 4", "obmen 5" };

        void OnServerInitialized()
        {
            InitFileManager();
            ServerMgr.Instance.StartCoroutine(LoadImages());
        }

        private Dictionary<string, string> images = new Dictionary<string, string>()
        {
            ["Img0"] = "https://imgur.com/8eTRs6p.png",
            ["Img1"] = "https://imgur.com/Z4vaStO.png",
            ["Img2"] = "https://imgur.com/b993ZWx.png",
            ["Img3"] = "https://imgur.com/b993ZWx.png",
            ["Img4"] = "https://imgur.com/b993ZWx.png",
            ["Img5"] = "https://imgur.com/b993ZWx.png",

            ["Image0"] = "https://imgur.com/Z4vaStO.png",
            ["Image1"] = "https://rustlabs.com/img/items180/charcoal.png",
            ["Image2"] = "https://imgur.com/gwRK5qo.png",
            ["Image3"] = "https://rustlabs.com/img/items180/jackhammer.png",
            ["Image4"] = "https://www.rust-items.com/icons/4277665d6b86d1c95a473f001c8b5161.png",
            ["Image5"] = "https://www.rust-items.com/icons/c0fa952d07bbf02f55ed344b3260763b.png",
            ["ImPng"] = "https://cdn.discordapp.com/attachments/903350610932416602/937281106540109885/111111.png",
        };

        IEnumerator LoadImages()
        {
            foreach (var name in images.Keys.ToList())
            {
                yield return m_FileManager.StartCoroutine(m_FileManager.LoadFile(name, images[name]));
                images[name] = m_FileManager.GetPng(name);
            }
            loaded = true;
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, "ggg");
            }
        }

        private void DrawInterface(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();
            string ggg = "ggg";
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0.2 0.2", AnchorMax = "0.8 0.8" },
                Image = { Color = "0.5 0 0 0", Sprite = "Assets/Content/UI/UI.Background.Tile.psd", FadeIn = 0.3f },
                FadeOut = 0.3f
            }, "Overlay", Layer);

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                    {
                        new CuiImageComponent {Color =  "0.2 0.2 0.2 1"},
                        new CuiRectTransformComponent {AnchorMin = $"0.275 0.175", AnchorMax = $"0.725 0.875"}
                    }
            });

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                    {
                        new CuiImageComponent {Color =  "0.2 0.2 0.2 1"},
                        new CuiRectTransformComponent {AnchorMin = $"0.4 0.05", AnchorMax = $"0.6 0.155"}
                    }
            });

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.275 0.9", AnchorMax = $"0.725 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                Button = { Color = "0.2 0.2 0.2 1", Command = $"" },
                Text = { Text = "ОБМЕН РЕСУРСОВ", Align = TextAnchor.MiddleCenter, FontSize = 15 }
            }, Layer);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.41 0.06", AnchorMax = "0.59 0.145", OffsetMin = "0 0", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0.8", Close = Layer },
                Text = { Text = "ЗАКРЫТЬ", Align = TextAnchor.MiddleCenter, FontSize = 16, Font = "robotocondensed-bold.ttf", Color = "1 1 1 1" }
            }, Layer);

            double anchor1 = 0.75;
            double anchor2 = 0.85;
            double anchor3 = 0.76;
            double anchor4 = 0.84;
            double anchor1_1 = 0.755;
            double anchor1_2 = 0.845;


            for (int i = 0; i < 6; i++)
            {
                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiImageComponent {Color =  "0 0 0 0.8"},
                        new CuiRectTransformComponent {AnchorMin = $"0.3 {anchor1}", AnchorMax = $"0.7 {anchor2}"}
                    }
                });

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"0.526 {anchor3}", AnchorMax = $"0.697 {anchor4}", OffsetMin = "0 0", OffsetMax = "0 0" },
                    Button = { Color = "0 0 0 0.5", Command = $"{Command[i]}", Close = Layer },
                    Text = { Text = $"{FirstWord[i]}", Align = TextAnchor.MiddleCenter, FontSize = 12 }
                }, Layer);

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"0.3583 {anchor1_1}", AnchorMax = $"0.4661 {anchor1_2}", OffsetMin = "0 0", OffsetMax = "0 0" },
                    Button = { Color = "0 0 0 0", Command = $"{Command[i]}" },
                    Text = { Text = "ВЗАМЕН НА", Align = TextAnchor.MiddleCenter, FontSize = 15 }
                }, Layer);

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Name = Layer + $"Img{i}",
                    Components =
                    {
                        new CuiRawImageComponent() { Png = images[$"Img{i}"], Color = "1 1 1 1", },
                        new CuiRectTransformComponent(){  AnchorMin = $"0.303 {anchor1_1}", AnchorMax = $"0.3536 {anchor1_2}", OffsetMin = "0 0", OffsetMax = "0 0" }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Name = Layer + $"Image{i}",
                    Components =
                    {
                        new CuiRawImageComponent() { Png = images[$"Image{i}"], Color = "1 1 1 1", },
                        new CuiRectTransformComponent(){  AnchorMin = $"0.4708 {anchor1_1}", AnchorMax = $"0.5217 {anchor1_2}", OffsetMin = "0 0", OffsetMax = "0 0" }
                    }
                });

                anchor1 -= 0.11;
                anchor2 -= 0.11;
                anchor1_1 -= 0.11;
                anchor1_2 -= 0.11;
                anchor3 -= 0.11;
                anchor4 -= 0.11;

                CuiHelper.DestroyUi(player, Layer + $"Img{i}");
                CuiHelper.DestroyUi(player, Layer + $"Image{i}");
            }

            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.AddUi(player, container);
        }


        [ChatCommand("obmen")]
        void Gui(BasePlayer player)
        {
            DrawInterface(player);
        }

        [ConsoleCommand("obmen")]
        void Obmen2(ConsoleSystem.Arg args)
        {
            var player = args.Player();
            var cislo = args.Args[0];
            string newitem = "";
            string check = "";
            int z1 = 0;
            int z2 = 0;

            if (cislo == "0")
            {
                check = "stones";
                newitem = "wood";
                z1 = 200000;
                z2 = 600000;
            }
            if (cislo == "1")
            {
                check = "wood";
                newitem = "charcoal";
                z1 = 200000;
                z2 = 100000;
            }
            if (cislo == "2")
            {
                check = "metal.fragments";
                newitem = "sulfur";
                z1 = 100000;
                z2 = 50000;
            }
            if (cislo == "3")
            {
                check = "metal.fragments";
                newitem = "jackhammer";
                z1 = 10000;
                z2 = 1;
            }
            if (cislo == "4")
            {
                check = "metal.fragments";
                newitem = "gunpowder";
                z1 = 100000;
                z2 = 25000;
            }
            if (cislo == "5")
            {
                check = "metal.fragments";
                newitem = "scrap";
                z1 = 100000;
                z2 = 5000;
            }

            var count = player.inventory.GetAmount(ItemManager.FindItemDefinition(check).itemid);
            if (count >= z1)
            {
                player.inventory.Take(null, ItemManager.FindItemDefinition(check).itemid, z1);
                Item item;
                item = ItemManager.CreateByName(newitem, z2);
                player.GiveItem(item);
            }
            else
            {
                SendReply(player, "Не достаточно ресурсов");
            }
        }

        private GameObject FileManagerObject;
        private FileManager m_FileManager;

        void InitFileManager()
        {
            FileManagerObject = new GameObject("MAP_FileManagerObject");
            m_FileManager = FileManagerObject.AddComponent<FileManager>();
        }

        class FileManager : MonoBehaviour
        {
            int loaded = 0;
            int needed = 0;

            public bool IsFinished => needed == loaded;
            const ulong MaxActiveLoads = 10;
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
                needed++;
                yield return StartCoroutine(LoadImageCoroutine(name, url));
            }

            IEnumerator LoadImageCoroutine(string name, string url)
            {
                using (WWW www = new WWW(url))
                {
                    yield return www;
                    using (MemoryStream stream = new MemoryStream())
                    {
                        if (string.IsNullOrEmpty(www.error))
                        {
                            stream.Position = 0;
                            stream.SetLength(0);

                            var bytes = www.bytes;

                            stream.Write(bytes, 0, bytes.Length);

                            var entityId = CommunityEntity.ServerInstance.net.ID;
                            var crc32 = FileStorage.server.Store(stream.ToArray(), FileStorage.Type.png, entityId).ToString();
                            files[name].Png = crc32;
                        }
                    }
                }
                loaded++;
            }
        }
    }
}