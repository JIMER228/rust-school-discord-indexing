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
    [Info("Obmen", "Sempai#3239", "1.1")]
    class Obmen : RustPlugin
    {
        private bool loaded = false;
        
        [JsonProperty("https://gspics.org/images/2022/02/02/04Kz73.jpg")]
        private string Layer = "https://gspics.org/images/2022/02/02/04Kz73.jpg";
        
        string[] FirstWord = { "ОБМЕНЯТЬ\n100К КАМНЯ НА 200К ДЕРЕВА", "ОБМЕНЯТЬ\n20К ЖЕЛЕЗА НА ГРОБ", "ОБМЕНЯТЬ\n100К ЖЕЛЕЗА НА 50К СЕРЫ", "ОБМЕНЯТЬ\n25К СКРАПА НА 50К ТНК", "ОБМЕНЯТЬ\n125K СКРАПА НА БУР", "ОБМЕНЯТЬ\n100К ЖЕЛЕЗА НА 5К СКРАПА" };
        string[] Command = { "obmen 0", "obmen 1", "obmen 2", "obmen 3", "obmen 4", "obmen 5" };

        void OnServerInitialized()
        {
			PrintWarning("\n-----------------------------\n" +
            "     Author - Sempai#3239\n" +
            "     VK - https://vk.com/rustnastroika/n" +
            "     Discord - https://discord.gg/5DPTsRmd3G/n" +
            "-----------------------------");
            InitFileManager();
            ServerMgr.Instance.StartCoroutine(LoadImages());
        }
        
        private Dictionary<string, string> images = new Dictionary<string, string>()
        {
            ["Img0"] = "https://gspics.org/images/2024/03/21/0NHJuN.png",
            ["Img1"] = "https://gspics.org/images/2024/03/21/0NHcAv.png",
            ["Img2"] = "https://gspics.org/images/2024/03/21/0NHcAv.png",
            ["Img3"] = "https://gspics.org/images/2024/03/21/0NHhas.png",
            ["Img4"] = "https://gspics.org/images/2024/03/21/0NHhas.png",
            ["Img5"] = "https://gspics.org/images/2024/03/21/0NHcAv.png",
            
            ["Image0"] = "https://gspics.org/images/2024/03/21/0NHtxZ.png",
            ["Image1"] = "https://gspics.org/images/2024/03/21/0NHHtT.png",
            ["Image2"] = "https://gspics.org/images/2024/03/21/0NHQmK.png",
            ["Image3"] = "https://gspics.org/images/2024/03/21/0NH4e7.png",
            ["Image4"] = "https://gspics.org/images/2024/03/21/0NHArn.png",
            ["Image5"] = "https://gspics.org/images/2024/03/21/0NHhas.png",
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
                Image = { Color = "0 0 0 0.4", Sprite = "Assets/Content/UI/UI.Background.Tile.psd", Material = "assets/content/ui/uibackgroundblur.mat", FadeIn = 0.3f },
                FadeOut = 0.3f
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
                Text = { Text = "GORGONA RUST", Align = TextAnchor.MiddleCenter, FontSize = 40 }
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
                z1 = 100000;
                z2 = 200000;
            }
            if (cislo == "1")
            {
                check = "metal.fragments";
                newitem = "coffin.storage";
                z1 = 20000;
                z2 = 1;
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
                check = "scrap";
                newitem = "lowgradefuel";
                z1 = 25000;
                z2 = 50000;
            }
            if (cislo == "4")
            {
                check = "scrap";
                newitem = "jackhammer";
                z1 = 125000;
                z2 = 1;
            }
            if (cislo == "5")
            {
                check = "metal.fragments";
                newitem = "scrap";
                z1 = 100000;
                z2 = 5000;
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