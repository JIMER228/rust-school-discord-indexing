using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("FSocial","Discord: Netrunner#0115","1.0")]
    public class FSocial : RustPlugin
    {
        #region Fields

        [PluginReference] private Plugin ImageLibrary,Broadcast;
        List<ulong> _socialCD = new List<ulong>();

        #endregion

        #region Config

        class SocialSettings
        {
            [JsonProperty("Название")]
            public string Name;
            [JsonProperty("Описание")]
            public string Text;
            [JsonProperty("Ссылка")]
            public string URL;
            [JsonProperty("Картинка")]
            public string Image;
        }
        
        static Configuration config = new Configuration();

        class Configuration
        {
            [JsonProperty("Настройка ссылок")] 
            public Dictionary<string, SocialSettings> Settings;

            [JsonProperty("Размер картинки")] 
            public int ImageSide = 100;
            [JsonProperty("Описание промо")]
            public string PromoText="HAXYN KOD";
            [JsonProperty("Промокод")]
            public string Promo = "XYN";
            public static Configuration GetNewConfiguration()
            {
                return new Configuration
                {
                    Settings = new Dictionary<string, SocialSettings>
                    {
                        ["vk"] = new SocialSettings
                        {
                            Name = "VK",
                            Text = "Our VK Group: vk.com/myserver",
                            URL = "vk.com/myserver",
                            Image = "https://forums.nexusmods.com/uploads/profile/photo-thumb-16432309.png"
                        },
                        ["discord"] = new SocialSettings{
                            Name = "Discord",
                            Text = "Our Discord server: vk.com/myserver",
                            URL = "vk.com/myserver",
                            Image = "https://forums.nexusmods.com/uploads/profile/photo-thumb-16432309.png"
                        },
                        ["store"] = new SocialSettings
                        {
                            Name = "Sotre",
                            Text = "Our Store: vk.com/myserver",
                            URL = "vk.com/myserver",
                            Image = "https://forums.nexusmods.com/uploads/profile/photo-thumb-16432309.png"
                        },
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
            DownloadImages();
        }

        #endregion

        #region Methods

        void DownloadImages()
        {
            ImageLibrary.Call("AddImage", "https://lc-crb.ru/CyberPunk/8s1jLEv.png", "prombg");//https://lc-crb.ru/CyberPunk/z8HUcIz.png
            ImageLibrary.Call("AddImage", "https://lc-crb.ru/CyberPunk/8s1jLEv.png", "socbg");
            ImageLibrary.Call("AddImage", "https://lc-crb.ru/CyberPunk/8s1jLEv.png", "infoicn");
            foreach (var setting in config.Settings) ImageLibrary.Call("AddImage", setting.Value.Image, setting.Key);
        }

        void GiveURL(BasePlayer player, string key)
        {
            if (_socialCD.Contains(player.userID))
            {
                Broadcast.Call("GetPlayerNotice", player, "Перезарядка","Пожулйста подождите","infoicn","assets/bundled/prefabs/fx/invite_notice.prefab");
                return;
            }
            if (config.Settings.ContainsKey(key))
            {
                SocialSettings social = config.Settings[key];
                Item note = ItemManager.CreateByItemID(1414245162);
                note.text = social.URL;
                note.name = social.Name;
                player.GiveItem(note,BaseEntity.GiveItemReason.PickedUp);
            }
            else
            {
                
                Item note = ItemManager.CreateByItemID(1414245162);
                note.text = config.Promo;
                note.name = "ПРОМОКОД";
                player.GiveItem(note,BaseEntity.GiveItemReason.PickedUp);
            }
            
            _socialCD.Add(player.userID);
            timer.Once(1f, () =>
            {
                _socialCD.Remove(player.userID);
            });
        }

        #endregion

        #region UI

        private string _layer = "SocialUI";
        void SocialUI(BasePlayer player)
        {
            
            CuiElementContainer container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                Image = {Color = "0 0 0 0"},
                RectTransform = {AnchorMin = "0.5 0.5",AnchorMax = "0.5 0.5",OffsetMin = $"-{config.Settings.Count*config.ImageSide*2.1/2} -{(90+config.ImageSide)/2}",OffsetMax = $"{(config.Settings.Count*config.ImageSide*2.1-config.ImageSide*2.1)/2} {(90+config.ImageSide)/2}"}
            }, "ContentUI", _layer);

            double startpos = 0,padding = config.ImageSide*1.1;
            foreach (var socialSetting in config.Settings)
            {
                container.Add(new CuiPanel
                {
                    Image = {Color = "0 0 0 0"},
                    RectTransform = {AnchorMin = "0 0.5",AnchorMax = "0 0.5",OffsetMin = $"{startpos} -{(90+config.ImageSide)/2}",OffsetMax = $"{startpos+config.ImageSide} {(90+config.ImageSide)/2}"}
                }, _layer, _layer + socialSetting.Key);
                container.Add(new CuiElement
                {
                    Parent = _layer + socialSetting.Key,
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",AnchorMax = "1 1"
                        },
                        new CuiRawImageComponent
                        {
                            Png = (string) ImageLibrary.Call("GetImage","socbg")
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = _layer + socialSetting.Key,
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1",AnchorMax = "0 1",OffsetMin = $"0 -{config.ImageSide}",OffsetMax = $"{config.ImageSide} 0"
                        },
                        new CuiRawImageComponent
                        {
                            Png = (string) ImageLibrary.Call("GetImage",socialSetting.Key)
                        }
                    }
                });
                container.Add(new CuiLabel
                {
                    Text = {Align = TextAnchor.MiddleCenter,Text = socialSetting.Value.Name,FontSize = 21},
                    RectTransform = {AnchorMin = "0 1",AnchorMax = "1 1",OffsetMin = $"0 -{config.ImageSide+28}",OffsetMax = $"0 -{config.ImageSide}"}
                }, _layer + socialSetting.Key);
                container.Add(new CuiLabel
                {
                    Text = {Align = TextAnchor.MiddleCenter,Text = socialSetting.Value.Text,FontSize = 16},
                    RectTransform = {AnchorMin = "0 1",AnchorMax = "1 1",OffsetMin = $"0 -{config.ImageSide+28+40}",OffsetMax = $"0 -{config.ImageSide+28}"}
                }, _layer + socialSetting.Key);
                container.Add(new CuiButton
                {
                    Text = {Align = TextAnchor.MiddleCenter,Text ="ПОЛУЧИТЬ ССЫЛКУ",FontSize = 18},
                    RectTransform = {AnchorMin = "0 1",AnchorMax = "1 1",OffsetMin = $"0 -{config.ImageSide+28+40+22}",OffsetMax = $"2 -{config.ImageSide+28+40}"},
                    Button = {Color = "0 0 0 0.9",Command = $"socialurl {socialSetting.Key}"}
                }, _layer + socialSetting.Key);
                startpos += config.ImageSide + padding;
            }

            container.Add(new CuiPanel
            {
                Image = {Color = "0 0 0 0.8"},
                RectTransform = {AnchorMin = "0.5 1",AnchorMax= "0.5 1",OffsetMin = $"-150 20",OffsetMax = "150 90"}
            }, _layer,_layer+"Promo");
            
            container.Add(new CuiElement
            {
                Parent = _layer+"Promo",
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",AnchorMax = "1 1"
                    },
                    new CuiRawImageComponent
                    {
                        Png = (string) ImageLibrary.Call("GetImage","prombg")
                    }
                }
            });

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0 0",AnchorMax = "1 1"},
                Text = {Text = config.PromoText,Align = TextAnchor.UpperCenter,FontSize = 16}
            }, _layer + "Promo");

            container.Add(new CuiButton
            {
                Button = {Color = "0 0 0 0.9",Command = $"socialurl promo"},
                Text = {Text = "Получить промокод",Align = TextAnchor.MiddleCenter,FontSize = 16},
                RectTransform = {AnchorMin = "0 0",AnchorMax = "1 0",OffsetMin = "0 0",OffsetMax = "2 20"}
            }, _layer + "Promo");

            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Command

        [ConsoleCommand("socialbuttons")]
        void OpenUICommand(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null) return;
            SocialUI(arg.Player());
        }

        [ConsoleCommand("socialurl")]
        void GiveURLPaper(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null || !arg.HasArgs()) return;
            GiveURL(arg.Player(),arg.Args[0]);
        }

        #endregion
    }
}