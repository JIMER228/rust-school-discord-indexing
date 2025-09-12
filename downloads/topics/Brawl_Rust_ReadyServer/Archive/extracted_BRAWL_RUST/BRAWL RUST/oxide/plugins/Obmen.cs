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
    [Info("Obmen", "Woks & Drop Dead", "1.1.1")]
    class Obmen : RustPlugin
    {
        private bool loaded = false;
        
        [JsonProperty("https://gspics.org/images/2022/02/02/04Kz73.jpg")]
        private string Layer = "https://gspics.org/images/2022/02/02/04Kz73.jpg";
        
        string[] FirstWord = { "ОБМЕНЯТЬ\n200К КАМНЯ НА 600К ДЕРЕВА", "ОБМЕНЯТЬ\n25К СКРАПА НА 50К ТНК", "ОБМЕНЯТЬ\n1000К ЖЕЛЕЗА НА 500К СЕРЫ", "ОБМЕНЯТЬ\n20К ЖЕЛЕЗА НА ГРОБ", "ОБМЕНЯТЬ\n125 СКРАПА НА БУР", "ОБМЕНЯТЬ\n100К СКРАПА НА 10К МВК", "ОБМЕНЯТЬ\n100К СКРАПА НА 10К МВК" };
        string[] Command = { "obmen 0", "obmen 1", "obmen 2", "obmen 3", "obmen 4", "obmen 5", "obmen 6" };

        void OnServerInitialized()
        {
            InitFileManager();
            ServerMgr.Instance.StartCoroutine(LoadImages());
        }
        
        private Dictionary<string, string> images = new Dictionary<string, string>()
        {
            ["Img0"] = "https://imgur.com/8eTRs6p.png",
            ["Img1"] = "https://www.rust-items.com/icons/c0fa952d07bbf02f55ed344b3260763b.png",
            ["Img2"] = "https://imgur.com/b993ZWx.png",
            ["Img3"] = "https://imgur.com/b993ZWx.png",
            ["Img4"] = "https://www.rust-items.com/icons/c0fa952d07bbf02f55ed344b3260763b.png",
            ["Img5"] = "https://www.rust-items.com/icons/c0fa952d07bbf02f55ed344b3260763b.png",
			["Img6"] = "https://www.rust-items.com/icons/c0fa952d07bbf02f55ed344b3260763b.png",
            
            ["Image0"] = "https://i.imgur.com/UXbst9Q.png",
            ["Image1"] = "",
            ["Image2"] = "",
            ["Image3"] = "",
            ["Image4"] = "",
            ["Image5"] = "https://www.rust-items.com/icons/95f7bc50d1458203a0924a572269bd1f.png",
			["Image6"] = "https://www.rust-items.com/icons/77388b11e55bb2fa47923d3295bd9dc3.png",
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
                CuiHelper.DestroyUi(player, Layer);
            }
        }
        
        private void DrawInterface(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();
            
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = {Color = "0 0 0 0.5", Sprite = "assets/content/materials/highlight.png", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
            }, "Overlay", Layer);
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.9781 0.9611", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Close = Layer },
                Text = { Text = "✖", Align = TextAnchor.MiddleCenter, FontSize = 20,Color = "1 0 0 1" }
            }, Layer);
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.3 0.9111", AnchorMax = "0.7 0.9944", OffsetMin = "0 0", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0",},
                Text = { Text = "RUSSIAN RUST", Align = TextAnchor.MiddleCenter, FontSize = 40 }
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
                        new CuiImageComponent {Color =  "0 0 0 0.5"},
                        new CuiRectTransformComponent {AnchorMin = $"0.3 {anchor1}", AnchorMax = $"0.7 {anchor2}"}
                    }
                });
                
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"0.526 {anchor3}", AnchorMax = $"0.697 {anchor4}", OffsetMin = "0 0", OffsetMax = "0 0" },
                    Button = { Color = "0 0 0 0.6", Command = $"{Command[i]}",Close = Layer},
                    Text = { Text = $"{FirstWord[i]}", Align = TextAnchor.MiddleCenter, FontSize = 14 }
                }, Layer);
                
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"0.3583 {anchor1_1}", AnchorMax = $"0.4661 {anchor1_2}", OffsetMin = "0 0", OffsetMax = "0 0" },
                    Button = { Color = "0 0 0 0", Command = $"{Command[i]}" },
                    Text = { Text = "ВЗАМЕН НА", Align = TextAnchor.MiddleCenter, FontSize = 25 }
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
                check = "scrap";
                newitem = "lowgradefuel";
                z1 = 25000;
                z2 = 50000;
            }
            if (cislo == "2")
            {
                check = "metal.fragments";
                newitem = "sulfur";
                z1 = 1000000;
                z2 = 500000;
            }
            if (cislo == "3")
            {
                check = "metal.fragments";
                newitem = "coffin.storage";
                z1 = 20000;
                z2 = 1;
            }
            if (cislo == "4")
            {
                check = "scrap";
                newitem = "jackhammer";
                z1 = 125;
                z2 = 1;
            }
            if (cislo == "5")
            {
                check = "scrap";
                newitem = "metal.refined";
                z1 = 100000;
                z2 = 10000;
            }
			if (cislo == "6")
            {
                check = "scrap";
                newitem = "hq.metal.ore";
                z1 = 100000;
                z2 = 10000;
            }
            
            var count = player.inventory.GetAmount(ItemManager.FindItemDefinition(check).itemid);
            if(count>=z1)
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

            IEnumerator LoadImageCoroutine( string name, string url)
            {
                using (WWW www = new WWW( url ))
                {
                    yield return www;
                    using (MemoryStream stream = new MemoryStream())
                    {
                        if (string.IsNullOrEmpty( www.error ))
                        {
                            stream.Position = 0;
                            stream.SetLength( 0 );

                            var bytes = www.bytes;

                            stream.Write( bytes, 0, bytes.Length );

                            var entityId = CommunityEntity.ServerInstance.net.ID;
                            var crc32 = FileStorage.server.Store(stream.ToArray(), FileStorage.Type.png, entityId).ToString();
                            files[ name ].Png = crc32;
                        }
                    }
                }
                loaded++;
            }
        }
    }
}