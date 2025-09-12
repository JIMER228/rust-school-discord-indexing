using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using UnityEngine.Networking;

namespace Oxide.Plugins;

[Info("CMenu", "Baks", "1.0")]
public class CMenu : RustPlugin
{
    #region Fields

    private static CMenu _;
    private static ImageUI _imageUI;

    #endregion

    #region Config

    class MenuButton
    {
        public string Command;
    }

    static Configuration config = new Configuration();

    class Configuration
    {
        [JsonProperty("Настройка")] public string[] Settings;

        public static Configuration GetNewConfiguration()
        {
            return new Configuration
            {
                Settings = new []
                {
                    "/block",
                    "/report",
                    "/list",
                    "/f",
                    "/bskin",
                }
            };
        }
    }

    protected override void LoadConfig()
    {
        base.LoadConfig();
        try
        {
            config = Config.ReadObject<Configuration>();
            if (config == null) PrintWarning("NULL");
            if (config == null) LoadDefaultConfig();
        }
        catch
        {
            PrintError($"Не удалось найти конфигурацию 'oxide/config/{Name}', Создание конфига!!");
            LoadDefaultConfig();
        }

        NextTick(SaveConfig);
    }

    protected override void LoadDefaultConfig() => config = Configuration.GetNewConfiguration();
    protected override void SaveConfig() => Config.WriteObject(config);

    #endregion
    
    #region Hooks

    void OnServerInitialized()
    {
        _ = this;
        _imageUI = new ImageUI();
        _imageUI.DownloadImage();
    }
    
    void Unload()
    {
        if (_imageUI != null)
        {
            _imageUI.UnloadImages();
            _imageUI = null;
        }
        _ = null;
    }

    #endregion

    #region Methods

    

    #endregion

    #region UI

    #region Images

    private class ImageUI
        {
            private const String _path = "CromulentMenu/";
            private const String _printPath = "data/" + _path;
            private readonly Dictionary<String, ImageData> _images = new()
            {
                { "main", new ImageData() },
                { "btn_1", new ImageData() },
                { "icon_1", new ImageData() },
                { "btn_2", new ImageData() },
                { "icon_2", new ImageData() },
                { "btn_3", new ImageData() },
                { "icon_3", new ImageData() },
                { "btn_4", new ImageData() },
                { "icon_4", new ImageData() },
                { "btn_5", new ImageData() },
                { "icon_5", new ImageData() },
            };

            private enum ImageStatus
            {
                NotLoaded,
                Loaded,
                Failed
            }

            private class ImageData
            {
                public ImageStatus Status = ImageStatus.NotLoaded;
                public string Id { get; set; }
            }

            public string GetImage(string name)
            {
                ImageData image;
                if (_images.TryGetValue(name, out image) && image.Status == ImageStatus.Loaded)
                    return image.Id;
                return null;
            }

            public void DownloadImage()
            {
                KeyValuePair<string, ImageData>? image = null;
                foreach (KeyValuePair<string, ImageData> img in _images)
                {
                    if (img.Value.Status == ImageStatus.NotLoaded)
                    {
                        image = img;
                        break;
                    }
                }

                if (image != null)
                {
                    ServerMgr.Instance.StartCoroutine(ProcessDownloadImage(image.Value));
                }
                else
                {
                    List<String> failedImages = new List<string>();

                    foreach (KeyValuePair<String, ImageData> img in _images)
                    {
                        if (img.Value.Status == ImageStatus.Failed)
                        {
                            failedImages.Add(img.Key);
                        }
                    }

                    if (failedImages.Count > 0)
                    {
                        String images = String.Join(", ", failedImages);
                        _.PrintError( $"Не удалось загрузить изображения: {images}.");
                        Interface.Oxide.UnloadPlugin(_.Name);
                    }
                    else
                    {
                        _.Puts($"{_images.Count} изображений успешно загружено!");
                    }
                }
            }
            
            public void UnloadImages()
            {
                foreach (KeyValuePair<string, ImageData> item in _images)
                    if(item.Value.Status == ImageStatus.Loaded)
                        if (item.Value?.Id != null)
                            FileStorage.server.Remove(uint.Parse(item.Value.Id), FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID);

                _images?.Clear();
            }

            private IEnumerator ProcessDownloadImage(KeyValuePair<string, ImageData> image)
            {
                string url = "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + _path + image.Key + ".png";
		   		 		  						  	   		  	 	 		  	  			  			 		  				
                using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(url))
                {
                    yield return www.SendWebRequest();

                    if (www.isNetworkError || www.isHttpError)
                    {
                        image.Value.Status = ImageStatus.Failed;
                    }
                    else
                    {
                        Texture2D tex = DownloadHandlerTexture.GetContent(www);
                        image.Value.Id = FileStorage.server.Store(tex.EncodeToPNG(), FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID).ToString();
                        image.Value.Status = ImageStatus.Loaded;
                        UnityEngine.Object.DestroyImmediate(tex);
                    }

                    DownloadImage();
                }
            }
        }

    #endregion

    private string _layer = "MenuUI";
    private string _content = "ContentUI";
    
    void InitMenu(BasePlayer player)
    {
        CuiHelper.DestroyUi(player, _layer);

        CuiElementContainer container = new CuiElementContainer();

        container.Add(new CuiPanel
        {
            Image = {Color = HexToRustFormat("#171025f2")},
            RectTransform = { AnchorMin = "0 0",AnchorMax = "1 1"},
            CursorEnabled = true
        }, "Overlay", _layer);
        container.Add(new CuiButton
        {
            RectTransform = { AnchorMin = "0 0",AnchorMax = "1 1"},
            Button = { Close = _layer,Color = "0 0 0 0"},
            Text = { Text = ""}
        }, _layer);
        container.Add(new CuiPanel
        {
            RectTransform = { AnchorMin = "0.5 0.5",AnchorMax = "0.5 0.5",OffsetMin = "-178.5 -252",OffsetMax = "178.5 252"},
            Image = {Color = "0 0 0 0"}
        }, _layer,_content);
        
        
        container.Add(new CuiElement
        {
            Parent = _content,
            Name = "CMenuCloseBtn",
            Components =
            {
                new CuiRawImageComponent
                {
                   Url = "https://i.ibb.co/nn3Mq3g/2024-01-20-231758304.png"
                },
                new CuiRectTransformComponent
                {
                    AnchorMin = "1 1",AnchorMax = "1 1",OffsetMin = "9 -55",OffsetMax = "64 0"
                }
            }
        });
        container.Add(new CuiButton
        {
            RectTransform = { AnchorMin = "0 0",AnchorMax = "1 1"},
            Button = { Close = _layer,Color = "0 0 0 0"},
            Text = { Text = ""}
        }, "CMenuCloseBtn");

        container.Add(new CuiElement
        {
            Parent = _content,
            Components =
            {
                new CuiRawImageComponent
                {
                    Png = _imageUI.GetImage("main")
                },
                new CuiRectTransformComponent
                {
                    AnchorMin = "0 0",AnchorMax = "1 1"
                }
            }
        });

        container.Add(new CuiButton
        {
            RectTransform = { AnchorMin = "0 0",AnchorMax = "0 0",OffsetMin = "10 14",OffsetMax = "138 53"},
            Button = { Color = "0 0 0 0",Close = _layer,Command = "tpm.comaand /trade yes"},
            Text = { Text = ""}
        }, _content);
        
        container.Add(new CuiButton
        {
            RectTransform = { AnchorMin = "0 0",AnchorMax = "0 0",OffsetMin = "36 57",OffsetMax = "107 131"},
            Button = { Color = "0 0 0 0",Close = _layer,Command = "tpm.comaand /trade yes"},
            Text = { Text = ""}
        }, _content);
        container.Add(new CuiButton
        {
            RectTransform = { AnchorMin = "0 0",AnchorMax = "0 0",OffsetMin = "221 14",OffsetMax = "348 53"},
            Button = { Color = "0 0 0 0",Close = _layer,Command = "tpm.comaand /duel a"},
            Text = { Text = ""}
        }, _content);
        container.Add(new CuiButton
        {
            RectTransform = { AnchorMin = "0 0",AnchorMax = "0 0",OffsetMin = "247 57",OffsetMax = "321 131"},
            Button = { Color = "0 0 0 0",Close = _layer,Command = "tpm.comaand /duel a"},
            Text = { Text = ""}
        }, _content);
        container.Add(new CuiButton
        {
            RectTransform = { AnchorMin = "0 0",AnchorMax = "0 0",OffsetMin = "115 90",OffsetMax = "242 128"},
            Button = { Color = "0 0 0 0",Close = _layer,Command = "tpm.comaand /help"},
            Text = { Text = ""}
        }, _content);
        container.Add(new CuiButton
        {
            RectTransform = { AnchorMin = "0 0",AnchorMax = "0 0",OffsetMin = "77 144",OffsetMax = "279 182"},
            Button = { Color = "0 0 0 0",Close = _layer,Command = "tpm.comaand /rank"},
            Text = { Text = ""}
        }, _content);
        container.Add(new CuiButton
        {
            RectTransform = { AnchorMin = "0 0",AnchorMax = "0 0",OffsetMin = "7 220",OffsetMax = "168 256"},
            Button = { Color = "0 0 0 0",Close = _layer,Command = "tpm.comaand /chat"},
            Text = { Text = ""}
        }, _content);
        container.Add(new CuiButton
        {
            RectTransform = { AnchorMin = "0 0",AnchorMax = "0 0",OffsetMin = "7 263",OffsetMax = "168 299"},
            Button = { Color = "0 0 0 0",Close = _layer,Command = "tpm.comaand /raid"},
            Text = { Text = ""}
        }, _content);

        float height = 38, icn_lenght = 34.5f, btn_l = 108.8f,padding = 4,starty = -113;
        for (int i = 0; i < 5; i++)
        {
            container.Add(new CuiElement
            {
                Parent = _content,
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Png = _imageUI.GetImage($"icon_{i+1}")
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1",AnchorMax = "0 1", OffsetMin = $"191 {starty}",OffsetMax = $"{194.7+icn_lenght} {height+starty}"
                    }
                }
            });
            container.Add(new CuiElement
            {
                Parent = _content,
                Name = _content+$".{i}",
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Png = _imageUI.GetImage($"btn_{i+1}")
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1",AnchorMax = "0 1", OffsetMin = $"232 {starty}",OffsetMax = $"{235.76+btn_l} {height+starty}"
                    }
                }
            });
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0",AnchorMax = "1 1"},
                Button = { Close = _layer,Color = "0 0 0 0",Command = $"tpm.comaand {config.Settings[i]}"},
            }, _content+$".{i}");

            starty -= padding + height;
        }

        CuiHelper.AddUi(player, container);
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

    #endregion

    #region Commands

    [ChatCommand("menu")]
    void MenuChatCmd(BasePlayer player, string command, string[] args)
    {
        InitMenu(player);
    }

    #endregion
}