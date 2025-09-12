
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("PromoCode", "Barry_Allenn", "1.2.0")]
    [Description("Plugin for promo codes with an interface for entering and activating promo codes")]

    public class PromoCode : RustPlugin
    {
        [PluginReference]
        private Plugin ImageLibrary;

        private PluginConfig config;
        private Dictionary<string, int> promoCodeActivations = new Dictionary<string, int>();
        private Dictionary<string, List<string>> usedPromoCodesByPlayer = new Dictionary<string, List<string>>();

        private class PluginConfig
        {

            [JsonProperty("PromoCodes")]
            public Dictionary<string, PromoCodeData> PromoCodes { get; set; }

            [JsonProperty("QR code to join your Discord server")]
            public string QRDISCORD_BA = "https://i.ibb.co/Zd1XKwh/Untitled-3.png";

            [JsonProperty("Text in the first column")] public string textFPDEText = "Discord";

            [JsonProperty("Text in the second column")] public string texttkFPDEText = "TikTok";

            [JsonProperty("Link to the first column")] public string DSFPDEText = "https://discord.gg/qXkHDGPkmn";

            [JsonProperty("Link to the second column")] public string TKFPDEText = "https://www.tiktok.com/@freedom.ua.rust";

            [JsonProperty("QR code of your TikTok profile")]
            public string QRTITTTOK_BA = "https://i.ibb.co/K7pH2jH/Untitled-2.png";

            [JsonProperty("MessagePrefix")]
            public string MessagePrefix { get; set; } = "<color=#0057b8>[Your Prefix]</color> ";

            [JsonProperty("Webhook to send to discord")]
            public string DiscordWebhookUrl { get; set; } = "Enter_your_webhook_URL_here";

            [JsonProperty("true or false sending promo code messages to the global chat")]
            public bool EnablePromoCodeBroadcast { get; set; } = true;
            
        }

        

        private class PromoCodeData
        {
            public string RewardCommand { get; set; }
            public string ExpiryDate { get; set; }
            public int MaxActivations { get; set; } = 50;
        }

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(new PluginConfig
            {
                EnablePromoCodeBroadcast = true,

                DiscordWebhookUrl = "Enter_your_webhook_URL_here",

                PromoCodes = new Dictionary<string, PromoCodeData>
                {
                    { "WELCOME", new PromoCodeData { RewardCommand = "give {player} wood 1000", ExpiryDate = "2024-12-31", MaxActivations = 50 } },
                    { "BONUS", new PromoCodeData { RewardCommand = "give {player} stone 500", ExpiryDate = "2024-12-31", MaxActivations = 50 } }
                },

                MessagePrefix = "<color=#0057b8>[Your Prefix]</color> ",

            }, true);
        }

        private void SendPluginMessage(BasePlayer player, string messageKey, params object[] args)
        {
            string prefix = config.MessagePrefix;
            string message = string.Format(Msg(messageKey, player.UserIDString), args);
            SendReply(player, $"{prefix}{message}");
        }

        private Dictionary<string, string> tempPromoCodeNames = new Dictionary<string, string>();
        private Dictionary<string, string> tempPromoCodeCommands = new Dictionary<string, string>();
        private Dictionary<string, int> tempPromoCodeLimits = new Dictionary<string, int>();
        private Dictionary<string, string> tempPromoCodeDates = new Dictionary<string, string>();


        private void Init()
        {
            PrintWarning("\n-----------------------------\n" +
            "     Author - Barry_Allenn\n" +
            "     Discord - https://discord.gg/qXkHDGPkmn\n" +
            "-----------------------------");

            permission.RegisterPermission("promocode.adminpanel", this);
            config = Config.ReadObject<PluginConfig>();
            LoadData();
        }

        private void Unload()
        {
            SaveData();
        }

        private void LoadData()
        {

            usedPromoCodesByPlayer = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, List<string>>>(Name) ?? new Dictionary<string, List<string>>();


            promoCodeActivations = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, int>>(Name + "_Activations") ?? new Dictionary<string, int>();
        }

        private void SaveData()
        {

            Interface.Oxide.DataFileSystem.WriteObject(Name, usedPromoCodesByPlayer);


            Interface.Oxide.DataFileSystem.WriteObject(Name + "_Activations", promoCodeActivations);
        }

        void OnServerInitialized()
        {
            ImageLibrary.Call("AddImage", "https://i.ibb.co/vz4YqSP/image.png", "fonpromocode");
            ImageLibrary.Call("AddImage", "https://i.ibb.co/1fhWtL4/adminpanel.png", "fonadminpanel");
            ImageLibrary.Call("AddImage", "https://i.ibb.co/j5GpWm2/1.png", "S_back");
            ImageLibrary.Call("AddImage", "https://i.ibb.co/nswvsyf/1.png", "S_before");
            ImageLibrary.Call("AddImage", "https://i.ibb.co/b1ws1ZS/1.png", "admin_button");
            ImageLibrary.Call("AddImage", "https://i.ibb.co/JKrzSFM/image.png", "Ojdwnfwf");
            ImageLibrary.Call("AddImage", config.QRTITTTOK_BA, "QRTITTTOK_BA");
            ImageLibrary.Call("AddImage", config.QRDISCORD_BA, "QRDISCORD_BA");
        }

        [ChatCommand("promo")]
        private void ShowPromoCodeMenu(BasePlayer player, string command, string[] args)
        {
            ShowPromoCodeUI_V1(player);
        }

        [ChatCommand("open_ui2")]
        private void OpenUI2ChatCommand(BasePlayer player, string command, string[] args)
        {
            CuiHelper.DestroyUi(player, "PromoCodeMenuUI");
            ShowPromoCodeUI_V2(player);
        }

        private void ShowPromoCodeUI_V1(BasePlayer player)
        {

            CuiHelper.DestroyUi(player, "PromoCodeMenu");

            var elements = new CuiElementContainer();


            elements.Add(new CuiPanel
            {
                Image = { Color = "0.1 0.1 0.1 0" },
                RectTransform = { AnchorMin = "0.23 0.19", AnchorMax = "0.77 0.75" },
                CursorEnabled = true
            }, "Overlay", "PromoCodeMenu");

            elements.Add(new CuiElement
            {
                Parent = "PromoCodeMenu",
                Components = {
                    new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", "fonpromocode") },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" }
                }
            });

            elements.Add(new CuiElement
            {
                Parent = "PromoCodeMenu",
                Components = {
                    new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", "QRTITTTOK_BA") },
                    new CuiRectTransformComponent { AnchorMin = "0.363 0.2555", AnchorMax = "0.514 0.533", OffsetMax = "0 0" }
                }
            });

            elements.Add(new CuiElement
            {
                Parent = "PromoCodeMenu",
                Components = {
                    new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", "QRDISCORD_BA") },
                    new CuiRectTransformComponent { AnchorMin = "0.1034 0.2555", AnchorMax = "0.253 0.533", OffsetMax = "0 0" }
                }
            });

            elements.Add(new CuiElement
            {
                Parent = "PromoCodeMenu",
                Components = {
                    new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", "Ojdwnfwf") },
                    new CuiRectTransformComponent { AnchorMin = "0.0954 0.247", AnchorMax = "0.261 0.5417", OffsetMax = "0 0" }
                }
            });

            elements.Add(new CuiElement
            {
                Parent = "PromoCodeMenu",
                Components = {
                    new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", "Ojdwnfwf") },
                    new CuiRectTransformComponent { AnchorMin = "0.3555 0.247", AnchorMax = "0.5215 0.5417", OffsetMax = "0 0" }
                }
            });

            elements.Add(new CuiLabel
            {
                Text = { Text = Msg("EnterPromoCode", player.UserIDString), Color = "1.00 1.00 1.00 0.6", FontSize = 12, Align = TextAnchor.MiddleLeft },
                RectTransform = { AnchorMin = "0.117 0.745", AnchorMax = "0.354 0.81" }
            }, "PromoCodeMenu");

            elements.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.605 0.77", AnchorMax = "0.85 0.83" },
                Text = { Text = Msg("S_info", player.userID.ToString()), Color = "1.00 1.00 1.00 0.6", Align = TextAnchor.MiddleCenter, FontSize = 15 }
            }, "PromoCodeMenu");

            elements.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.595 0.65", AnchorMax = "0.91 0.7" },
                Text = { Text = Msg("S_infotllo_line_1", player.userID.ToString()), Color = "1.00 1.00 1.00 0.5", Align = TextAnchor.MiddleLeft, FontSize = 14 }
            }, "PromoCodeMenu");

            elements.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.595 0.58", AnchorMax = "0.91 0.63" },
                Text = { Text = Msg("S_infotllo_line_2", player.userID.ToString()), Color = "1.00 1.00 1.00 0.5", Align = TextAnchor.MiddleLeft, FontSize = 14 }
            }, "PromoCodeMenu");

            elements.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.595 0.5", AnchorMax = "0.91 0.55" },
                Text = { Text = Msg("S_infotllo_line_3", player.userID.ToString()), Color = "1.00 1.00 1.00 0.5", Align = TextAnchor.MiddleLeft, FontSize = 14 }
            }, "PromoCodeMenu");

            elements.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.595 0.43", AnchorMax = "0.91 0.48" },
                Text = { Text = Msg("S_infotllo_line_4", player.userID.ToString()), Color = "1.00 1.00 1.00 0.5", Align = TextAnchor.MiddleLeft, FontSize = 14 }
            }, "PromoCodeMenu");

            elements.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.595 0.36", AnchorMax = "0.91 0.41" },
                Text = { Text = Msg("S_infotllo_line_5", player.userID.ToString()), Color = "1.00 1.00 1.00 0.5", Align = TextAnchor.MiddleLeft, FontSize = 14 }
            }, "PromoCodeMenu");

            elements.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.125 0.185", AnchorMax = "0.23 0.225" },
                Text = { Text = config.textFPDEText, Color = "1.00 1.00 1.00 0.5", Align = TextAnchor.MiddleCenter, FontSize = 10 }
            }, "PromoCodeMenu");

            elements.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.385 0.185", AnchorMax = "0.49 0.225" },
                Text = { Text = config.texttkFPDEText, Color = "1.00 1.00 1.00 0.5", Align = TextAnchor.MiddleCenter, FontSize = 10 }
            }, "PromoCodeMenu");

            elements.Add(new CuiElement
            {
                Name = "PromoCodeInput",
                Parent = "PromoCodeMenu",
                Components = {
                    new CuiInputFieldComponent { Text = Msg("EnterPromoCodePlaceholder", player.UserIDString), Color = "1.00 1.00 1.00 0.6", 
                    FontSize = 13, Align = TextAnchor.MiddleLeft, Command = "promocode.submit", NeedsKeyboard = true },
                    new CuiRectTransformComponent { AnchorMin = "0.117 0.645", AnchorMax = "0.39 0.71" }
                }
            });

            elements.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.109 0.147", AnchorMax = "0.25 0.18", OffsetMax = "0 0" },
                Image = { Color = "1 1 1 0" }
            }, "PromoCodeMenu", "DSFPDEText");

            elements.Add(new CuiElement
            {
                Parent = "DSFPDEText",
                Components =
                {
                    new CuiInputFieldComponent { Text = config.DSFPDEText, Color = "1 1 1 0.3", Align = TextAnchor.MiddleLeft, FontSize = 9, Font = "robotocondensed-bold.ttf"},
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });

            elements.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.3685 0.147", AnchorMax = "0.509 0.18", OffsetMax = "0 0" },
                Image = { Color = "1 1 1 0" }
            }, "PromoCodeMenu", "TKFPDEText");

            elements.Add(new CuiElement
            {
                Parent = "TKFPDEText",
                Components =
                {
                    new CuiInputFieldComponent { Text = config.TKFPDEText, Color = "1 1 1 0.3", Align = TextAnchor.MiddleLeft, FontSize = 9, Font = "robotocondensed-bold.ttf"},
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });

            
            if (permission.UserHasPermission(player.UserIDString, "promocode.adminpanel"))
            {
                
                elements.Add(new CuiElement
                {
                   Parent = "PromoCodeMenu",
                    Components = 
                    {
                        new CuiRawImageComponent
                        {
                            Png = (string)ImageLibrary.Call("GetImage", "admin_button"),
                            Color = "1 1 1 1"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.575 0.17", AnchorMax = "0.878 0.24"
                        }
                    }
                });

               
               elements.Add(new CuiButton
                {
                    Button = { Color = "0 0 0 0", Command = "open_secondary_ui", FadeIn = 0.5f },
                    RectTransform = { AnchorMin = "0.575 0.17", AnchorMax = "0.878 0.24" },
                    Text = { Text = Msg("S_adminpanel", player.UserIDString), Color = "1.00 1.00 1.00 0.9", FontSize = 11, Align = TextAnchor.MiddleCenter }
                }, "PromoCodeMenu");
            }

            elements.Add(new CuiButton
            {
                Button = { Color = "0.2 0.8 0.2 0", Command = "promocode.submit", FadeIn = 0.5f },
                RectTransform = { AnchorMin = "0.405 0.645", AnchorMax = "0.4985 0.713" },
                Text = { Text = Msg("Activate", player.UserIDString), Color = "1.00 1.00 1.00 0.9", FontSize = 11, Align = TextAnchor.MiddleCenter }
            }, "PromoCodeMenu");

            elements.Add(new CuiButton
            {
                Button = { Color = "0.8 0.2 0.2 0", Command = "promocode.close", FadeIn = 0.5f },
                RectTransform = { AnchorMin = "0.97 0.952", AnchorMax = "0.995 0.99" },
                Text = { Text = Msg("Close", player.UserIDString), FontSize = 20, Align = TextAnchor.MiddleCenter }
            }, "PromoCodeMenu");

            CuiHelper.AddUi(player, elements);
        }

        private void ShowPromoCodeUI_V2(BasePlayer player, int page = 1)
        {
            

            const int itemsPerPage = 5; 
            var container = new CuiElementContainer();

           
            var panel = container.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0.3 0.25", AnchorMax = "0.7 0.69" },
                CursorEnabled = true
            }, "Overlay", "PromoCodeMenuUI");

            container.Add(new CuiElement
            {
                Parent = "PromoCodeMenuUI",
                Components = {
                    new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", "fonadminpanel") },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" }
                }
            });

            
            container.Add(new CuiPanel
            {
                Image = { Color = "0.2 0.2 0.2 0" },
                RectTransform = { AnchorMin = "0.5 0.2", AnchorMax = "0.95 0.8" }
            }, panel);

            
            container.Add(new CuiButton
            {
                Button = { Color = "0.8 0.2 0.2 0", Command = "ui_close_secondary", Close = "PromoCodeMenuUI" },
                RectTransform = { AnchorMin = "0.95 0.95", AnchorMax = "0.98 0.989" },
                Text = { Text = "", FontSize = 16, Align = TextAnchor.MiddleCenter }
            }, panel);

            
        
            
            container.Add(new CuiLabel
            {
                Text = {
                    Text = Msg("S_panelpromo", player.userID.ToString()),
                    Color = "1 1 1 0.6",
                    FontSize = 11,
                    Align = TextAnchor.MiddleCenter
                },
                RectTransform = { AnchorMin = "0.61 0.785", AnchorMax = "0.87 0.85" }
            }, panel);

           
            var promoCodes = new List<KeyValuePair<string, PromoCodeData>>(config.PromoCodes);
            int totalPages = Mathf.CeilToInt((float)promoCodes.Count / itemsPerPage);
            int start = (page - 1) * itemsPerPage;

            float y = 0.7f;
            for (int i = start; i < start + itemsPerPage && i < promoCodes.Count; i++)
            {
                var promoCode = promoCodes[i];
                string code = promoCode.Key;
                var data = promoCode.Value;

                int activations = promoCodeActivations.TryGetValue(code, out int count) ? count : 0;

                container.Add(new CuiLabel
                {
                    Text = {
                        Text = $"{code} ({string.Format(lang.GetMessage("Activations", this, player.UserIDString), activations, data.MaxActivations)})",
                        Color = "1 1 1 0.6",
                        FontSize = 8,
                        Align = TextAnchor.MiddleLeft
                    },
                    RectTransform = { AnchorMin = $"0.58 {y}", AnchorMax = $"0.9 {y + 0.05}" }
                }, panel);

                if (!string.IsNullOrEmpty(data.ExpiryDate))
                {
                    container.Add(new CuiLabel
                    {
                        Text = {
                           Text = string.Format(lang.GetMessage("ExpiryDate", this, player.UserIDString), data.ExpiryDate),
                            Color = "1 1 1 0.6",
                            FontSize = 8,
                            Align = TextAnchor.MiddleLeft
                        },
                        RectTransform = { AnchorMin = $"0.58 {y - 0.04}", AnchorMax = $"0.9 {y + 0.01}" }
                    }, panel);
                }


                if (permission.UserHasPermission(player.UserIDString, "promocode.adminpanel"))
                {
                    container.Add(new CuiButton
                    {
                        Button = {
                           Color = "0.8 0.2 0.2 0",
                            Command = $"promocode.remove {code}",
                            FadeIn = 0.5f
                        },
                        RectTransform = { AnchorMin = $"0.82 {y - 0.02}", AnchorMax = $"0.9 {y + 0.03}" },
                        Text = { Text = Msg("S_delete", player.UserIDString), Color = "1 1 1 0.6", FontSize = 10, Align = TextAnchor.MiddleCenter }
                    }, panel);
                }
        
                y -= 0.1f;
            }


            if (page < totalPages)
            {

                container.Add(new CuiElement
               {
                    Parent = panel,
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = (string)ImageLibrary.Call("GetImage", "S_before"),
                            Color = "1 1 1 1"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.82 0.14",
                            AnchorMax = "0.875 0.2"
                        }
                    }
                });


               container.Add(new CuiButton
                {
                    Button = { Color = "0 0 0 0", Command = $"ui_promo_page {page + 1}", FadeIn = 0.5f },
                    RectTransform = { AnchorMin = "0.82 0.14", AnchorMax = "0.875 0.2" },
                    Text = { Text = "", FontSize = 9, Align = TextAnchor.MiddleCenter }
                }, panel);
            }


            if (page > 1)
            {

                container.Add(new CuiElement
                {
                    Parent = panel,
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = (string)ImageLibrary.Call("GetImage", "S_back"),
                            Color = "1 1 1 1" 
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.61 0.14",
                            AnchorMax = "0.665 0.2"
                        }
                    }
                });


                container.Add(new CuiButton
                {
                    Button = { Color = "0 0 0 0", Command = $"ui_promo_page {page - 1}", FadeIn = 0.5f },
                    RectTransform = { AnchorMin = "0.61 0.14", AnchorMax = "0.665 0.2" },
                    Text = { Text = "", FontSize = 8, Align = TextAnchor.MiddleCenter }
                }, panel);
            }

            container.Add(new CuiElement
            {
                Name = "PromoCodeNameInput",
                Parent = panel,
                Components = {
                    new CuiInputFieldComponent
                    {
                        Text = "",
                        Color = "1 1 1 0.6",
                        FontSize = 9,
                        Align = TextAnchor.MiddleLeft,
                        Command = "promocode.setname",
                        NeedsKeyboard = true
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.135 0.725",
                        AnchorMax = "0.33 0.778"
                    }
                }
            });

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.125 0.835", AnchorMax = "0.41 0.874" },
                Text = { Text = Msg("S_VDR", player.userID.ToString()), Color = "1.00 1.00 1.00 0.4", Align = TextAnchor.MiddleLeft, FontSize = 9 }
            }, "PromoCodeMenuUI");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.125 0.79", AnchorMax = "0.41 0.83" },
                Text = { Text = Msg("S_VDRPR", player.userID.ToString()), Color = "1.00 1.00 1.00 0.2", Align = TextAnchor.MiddleLeft, FontSize = 8 }
            }, "PromoCodeMenuUI");


            container.Add(new CuiElement
            {
                Name = "PromoCodeCommandInput",
                Parent = panel,
                Components = {
                    new CuiInputFieldComponent
                    {
                        Text = "",
                        Color = "1 1 1 0.6",
                        FontSize = 9,
                        Align = TextAnchor.MiddleLeft,
                        Command = "promocode.setcommand",
                        NeedsKeyboard = true
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.135 0.49",
                        AnchorMax = "0.33 0.538"
                    }
                }
            });

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.125 0.595", AnchorMax = "0.41 0.64" },
                Text = { Text = Msg("S_CODR", player.userID.ToString()), Color = "1.00 1.00 1.00 0.4", Align = TextAnchor.MiddleLeft, FontSize = 9 }
            }, "PromoCodeMenuUI");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.125 0.555", AnchorMax = "0.41 0.6" },
                Text = { Text = Msg("S_CODRPR", player.userID.ToString()), Color = "1.00 1.00 1.00 0.2", Align = TextAnchor.MiddleLeft, FontSize = 8 }
            }, "PromoCodeMenuUI");

            

            container.Add(new CuiElement
            {
                Name = "PromoCodeLimitInput",
                Parent = panel,
                Components = {
                    new CuiInputFieldComponent
                   {
                        Text = "",
                        Color = "1 1 1 1",
                        FontSize = 9,
                        Align = TextAnchor.MiddleLeft,
                        Command = "promocode.setlimit",
                        NeedsKeyboard = true
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.3 0.255", 
                        AnchorMax = "0.37 0.3" 
                    }
                }
            });

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.28 0.355", AnchorMax = "0.41 0.4" },
                Text = { Text = Msg("S_CODRLIMIT", player.userID.ToString()), Color = "1.00 1.00 1.00 0.4", Align = TextAnchor.MiddleLeft, FontSize = 9 }
            }, "PromoCodeMenuUI");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.28 0.315", AnchorMax = "0.41 0.355" },
                Text = { Text = Msg("S_CODRLIMFFIT", player.userID.ToString()), Color = "1.00 1.00 1.00 0.2", Align = TextAnchor.MiddleLeft, FontSize = 8 }
            }, "PromoCodeMenuUI");

            container.Add(new CuiElement
            {
                Name = "PromoCodeExpiryDateInput",
                Parent = panel,
                Components = {
                    new CuiInputFieldComponent
                    {
                        Text = "",
                        Color = "1 1 1 0.6",
                        FontSize = 9,
                        Align = TextAnchor.MiddleLeft,
                        Command = "promocode.setdate",
                        NeedsKeyboard = true
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.135 0.255", 
                        AnchorMax = "0.235 0.3"
                    }
                }
            });

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.125 0.355", AnchorMax = "0.27 0.4" },
                Text = { Text = Msg("S_CODRdata", player.userID.ToString()), Color = "1.00 1.00 1.00 0.4", Align = TextAnchor.MiddleLeft, FontSize = 9 }
            }, "PromoCodeMenuUI");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.125 0.315", AnchorMax = "0.27 0.355" },
                Text = { Text = Msg("S_CODRFFdata", player.userID.ToString()), Color = "1.00 1.00 1.00 0.2", Align = TextAnchor.MiddleLeft, FontSize = 8 }
            }, "PromoCodeMenuUI");


            container.Add(new CuiButton
            {
                Button = {
                    Color = "0.2 0.8 0.2 0",
                    Command = "promocode.create",
                    FadeIn = 0.5f
                },
                RectTransform = {
                    AnchorMin = "0.21 0.12", 
                    AnchorMax = "0.41 0.18" 
                },
                Text = {
                    Text = "Создать промокод",
                    FontSize = 14,
                    Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                }
            }, panel);

            CuiHelper.AddUi(player, container);
            
        }


        [ConsoleCommand("promocode.setlimit")]
        private void SetPromoCodeLimit(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || arg.Args == null || arg.Args.Length < 1) return;


            if (int.TryParse(arg.Args[0], out int limit) && limit > 0)
            {

                tempPromoCodeLimits[player.UserIDString] = limit;
                player.ChatMessage(lang.GetMessage("LimitSaved", this, player.UserIDString).Replace("{limit}", limit.ToString()));
            }
            else
            {
                player.ChatMessage(lang.GetMessage("LimitError", this, player.UserIDString));
            }
        }


        [ConsoleCommand("promocode.setname")]
        private void SetPromoCodeName(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || arg.Args == null || arg.Args.Length < 1) return;
        
            string promoCodeName = string.Join(" ", arg.Args).Trim(); 
        
           if (string.IsNullOrEmpty(promoCodeName))
            {
                player.ChatMessage(lang.GetMessage("PromoCodeNameEmpty", this, player.UserIDString));
                return;
            }


            tempPromoCodeNames[player.UserIDString] = promoCodeName;
            player.ChatMessage(lang.GetMessage("PromoCodeNameSaved", this, player.UserIDString).Replace("{promoCodeName}", promoCodeName));
        }


        [ConsoleCommand("promocode.submit")]
        private void SubmitPromoCode(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || arg.Args == null || arg.Args.Length < 1) return;

            string promoCode = arg.Args[0].Trim();
            ActivatePromoCode(player, promoCode); 
        }





        [ConsoleCommand("promocode.setcommand")]
        private void SetPromoCodeCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || arg.Args == null || arg.Args.Length == 0) return;


            string promoCodeCommand = string.Join(" ", arg.Args).Trim();


            tempPromoCodeCommands[player.UserIDString] = promoCodeCommand;
            player.ChatMessage(lang.GetMessage("PromoCodeCommandSaved", this, player.UserIDString).Replace("{promoCodeCommand}", promoCodeCommand));
        }


        [ConsoleCommand("promocode.setdate")]
        private void SetPromoCodeDate(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || arg.Args == null || arg.Args.Length < 1) return;

            string dateInput = arg.Args[0].Trim();


            if (DateTime.TryParse(dateInput, out DateTime expiryDate))
            {
                tempPromoCodeDates[player.UserIDString] = expiryDate.ToString("yyyy-MM-dd");
                player.ChatMessage(lang.GetMessage("PromoCodeDateSaved", this, player.UserIDString).Replace("{expiryDate}", expiryDate.ToString("yyyy-MM-dd")));
            }
            else
            {
                player.ChatMessage(lang.GetMessage("PromoCodeDateError", this, player.UserIDString));
            }
        }





        [ConsoleCommand("promocode.create")]
        private void CreatePromoCode(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;


            tempPromoCodeNames.TryGetValue(player.UserIDString, out string promoCodeName);
            tempPromoCodeCommands.TryGetValue(player.UserIDString, out string promoCodeCommand);
            tempPromoCodeLimits.TryGetValue(player.UserIDString, out int promoCodeLimit);
            tempPromoCodeDates.TryGetValue(player.UserIDString, out string promoCodeExpiryDate);


            if (string.IsNullOrEmpty(promoCodeName))
            {
                string errorMessage = lang.GetMessage("PromoCodeNameEmpty", this, player.UserIDString);
                player.ChatMessage(errorMessage);
                return;
           }


            if (string.IsNullOrEmpty(promoCodeCommand))
            {
                string errorMessage = lang.GetMessage("PromoCodeCommandEmpty", this, player.UserIDString);
                player.ChatMessage(errorMessage);
                return;
           }


            if (promoCodeLimit <= 0)
            {
                string errorMessage = lang.GetMessage("PromoCodeLimitInvalid", this, player.UserIDString);
                player.ChatMessage(errorMessage);
                return;
            }


            if (string.IsNullOrEmpty(promoCodeExpiryDate))
            {
                string errorMessage = lang.GetMessage("PromoCodeExpiryDateEmpty", this, player.UserIDString);
                player.ChatMessage(errorMessage);
                return;
           }


            if (!config.PromoCodes.ContainsKey(promoCodeName))
            {
                config.PromoCodes.Add(promoCodeName, new PromoCodeData
                {
                    RewardCommand = promoCodeCommand,
                    ExpiryDate = promoCodeExpiryDate,
                    MaxActivations = promoCodeLimit
                });

                Config.WriteObject(config); 
                player.ChatMessage(lang.GetMessage("PromoCodeCreated", this, player.UserIDString)
                    .Replace("{promoCodeName}", promoCodeName)
                    .Replace("{promoCodeExpiryDate}", promoCodeExpiryDate));
        

                tempPromoCodeNames.Remove(player.UserIDString);
                tempPromoCodeCommands.Remove(player.UserIDString);
                tempPromoCodeLimits.Remove(player.UserIDString);
                tempPromoCodeDates.Remove(player.UserIDString);
            }
            else
            {
                player.ChatMessage(lang.GetMessage("PromoCodeExists", this, player.UserIDString)
                    .Replace("{promoCodeName}", promoCodeName));
            }
        }


        [ConsoleCommand("ui_promo_page")]
        private void ShowPromoCodePage(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || arg.Args == null || arg.Args.Length < 1) return;

            if (!int.TryParse(arg.Args[0], out int page)) return;

    
            CuiHelper.DestroyUi(player, "PromoCodeMenuUI");


            ShowPromoCodeUI_V2(player, page);
        }


        [ConsoleCommand("promocode.close")]
        private void ClosePromoCodeUI(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            CloseAllInterfaces(player);
            
        }

        [ConsoleCommand("open_secondary_ui")]
        private void OpenSecondaryUICommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null)
            {
                PrintError(lang.GetMessage("CommandPlayerOnly", this)); 
                return;
            }

            CuiHelper.DestroyUi(player, "PromoCodeMenuUI"); 
            ShowPromoCodeUI_V2(player);
        }

        
        [ConsoleCommand("promocode.remove")]
        private void RemovePromoCode(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || arg.Args == null || arg.Args.Length < 1)
            {
                PrintWarning(lang.GetMessage("RemoveInvalidCommand", this)); 
                return;
            }

            if (!permission.UserHasPermission(player.UserIDString, "promocode.adminpanel"))
            {
                player.ChatMessage(lang.GetMessage("RemoveNoPermission", this, player.UserIDString));
                return;
            }

            string promoCode = arg.Args[0];

            if (config.PromoCodes.ContainsKey(promoCode))

                config.PromoCodes.Remove(promoCode);
        

                Config.WriteObject(config);

                player.ChatMessage(lang.GetMessage("PromoCodeRemoved", this, player.UserIDString).Replace("{promoCode}", promoCode));
                PrintWarning(lang.GetMessage("PromoCodeRemovedLog", this).Replace("{promoCode}", promoCode).Replace("{playerName}", player.displayName));
            }
            else
            {
                player.ChatMessage(lang.GetMessage("PromoCodeNotFound", this, player.UserIDString).Replace("{promoCode}", promoCode));
            }
        }



        private string Msg(string key, string userId = null)
        {
            return lang.GetMessage(key, this, userId);
        }

        private void CloseAllInterfaces(BasePlayer player)
        {

            CuiHelper.DestroyUi(player, "PromoCodeMenu");
        

            CuiHelper.DestroyUi(player, "PromoCodeMenuUI");
        

        }


        private void ActivatePromoCode(BasePlayer player, string promoCode)
        {
            string playerId = player.UserIDString;

            if (usedPromoCodesByPlayer.TryGetValue(playerId, out List<string> usedCodes) && usedCodes.Contains(promoCode))
            {
                SendPluginMessage(player, "PromoCodeAlreadyUsed");
                return;
            }

            if (promoCodeActivations.TryGetValue(promoCode, out int activations) && activations >= config.PromoCodes[promoCode].MaxActivations)
            {
                SendPluginMessage(player, "PromoCodeUnavailable");
                return;
            }

            if (config.PromoCodes.TryGetValue(promoCode, out PromoCodeData promoData))
            {
                if (!DateTime.TryParse(promoData.ExpiryDate, out DateTime expiryDate) || DateTime.Now > expiryDate)
                {
                    SendPluginMessage(player, "PromoCodeExpired");
                    return;
                }

                string rewardCommand = promoData.RewardCommand.Replace("{player}", playerId);
                rust.RunServerCommand(rewardCommand);

                promoCodeActivations[promoCode] = activations + 1;
                if (!usedPromoCodesByPlayer.ContainsKey(playerId))
                {
                    usedPromoCodesByPlayer[playerId] = new List<string>();
                }
                usedPromoCodesByPlayer[playerId].Add(promoCode);
                SaveData();

                SendPluginMessage(player, "PromoCodeActivated");


                if (config.EnablePromoCodeBroadcast)
                {
                    string message = Msg("PromoCodeActivatedBroadcast", player.UserIDString)
                        .Replace("{player}", player.displayName)
                        .Replace("{promoCode}", promoCode);
                    PrintToChat($"{config.MessagePrefix}{message}");
                }
                if (!string.IsNullOrEmpty(config.DiscordWebhookUrl))
                {
                    int remainingActivations = promoData.MaxActivations - promoCodeActivations[promoCode];
                    SendDiscordWebhook(player, promoCode, remainingActivations);
                }
                else
                {
                    Puts("The Discord webhook URL is not set. Skipping webhook send.");
                }

                Puts($"The promo code '{promoCode}' has been activated {promoCodeActivations[promoCode]} time(s).");
            }
            else
            {
                SendPluginMessage(player, "InvalidPromoCode");
            }
        }

        private void SendDiscordWebhook(BasePlayer player, string promoCode, int remainingActivations)
        {
            if (string.IsNullOrEmpty(config.DiscordWebhookUrl))
            {
                Puts("The Discord webhook URL is not set. Skipping webhook send.");
                return;
            }

            string playerName = player.displayName;
            string steamId = player.UserIDString;

            var embedPayload = new
            {
                embeds = new[]
                {
                    new
                    {
                        title = "🎉 Promo code activated! 🎉",
                        description = $"**Player:** {playerName}\n**Steam ID:** {steamId}\n**Promo code:** {promoCode}\n**Activations remaining:** {remainingActivations}",
                        color = 0x00FF00,
                        author = new
                        {
                            name = "Author: Barry_Allenn",
                            icon_url = "https://i.ibb.co/xHHfwjj/image.png",
                        },
                        footer = new
                        {
                            text = "Plugin PromoCode",
                            icon_url = "https://i.ibb.co/pfMxT7h/images.jpg"
                        },
                        timestamp = DateTime.UtcNow.ToString("o")

                    }
                }
            };

            string payload = JsonConvert.SerializeObject(embedPayload);

            webrequest.Enqueue(config.DiscordWebhookUrl, payload, (code, response) =>
            {
                if (code != 200 || string.IsNullOrEmpty(response))
                {

                }
            }, this, Oxide.Core.Libraries.RequestMethod.POST, new Dictionary<string, string> { { "Content-Type", "application/json" } });
        }



        protected override void LoadDefaultMessages()
        {

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["EnterPromoCode"] = "Enter your promo code",
                ["EnterPromoCodePlaceholder"] = "",
                ["Activate"] = "Activate",
                ["Close"] = "X",
                ["S_info"] = "Information",
                ["S_infotllo_line_1"] = "New promo code is here!",
                ["S_infotllo_line_2"] = "Where to find? Easy:",
                ["S_infotllo_line_3"] = "Watch our latest videos on TikTok",
                ["S_infotllo_line_4"] = "Or catch him on our Discord channel!",
                ["S_infotllo_line_5"] = "Don't miss your chance to get bonuses!",
                ["PromoCodeAlreadyUsed"] = "You have already used this promo code!",
                ["PromoCodeUnavailable"] = "This promo code is no longer available.",
                ["PromoCodeExpired"] = "This promo code has expired.",
                ["PromoCodeActivated"] = "Promo code activated!",
                ["InvalidPromoCode"] = "Invalid promo code.",
                ["PromoCodeActivatedBroadcast"] = "<color=#0099FF>{player}</color> has activated the promo code {promoCode}!",
                ["ExpiryDate"] = "Expiration Date: {0}",
                ["Activations"] = "Activations: {0}/{1}",
                ["S_delete"] = "Delete",
                ["PromoCodeNameEmpty"] = "Error: Promo code name is not specified.",
                ["PromoCodeCommandEmpty"] = "Error: Promo code command is not specified.",
                ["PromoCodeLimitInvalid"] = "Error: Activation limit is not specified or invalid.",
                ["PromoCodeExpiryDateEmpty"] = "Error: Expiry date is not specified.",
                ["PromoCodeCreated"] = "Promo code '{promoCodeName}' has been created with an expiration date of {promoCodeExpiryDate}!",
                ["PromoCodeExists"] = "Error: Promo code '{promoCodeName}' already exists.",
                ["LimitSaved"] = "Activation limit saved: {limit}",
                ["LimitError"] = "Error: Enter a valid number greater than zero for the activation limit.",
                ["PromoCodeNameEmpty"] = "Error: Promo code name cannot be empty.",
                ["PromoCodeNameSaved"] = "Promo code name saved: {promoCodeName}",
                ["PromoCodeCommandSaved"] = "Promo code command saved: {promoCodeCommand}",
                ["PromoCodeDateSaved"] = "Expiry date saved: {expiryDate}",
                ["PromoCodeDateError"] = "Error: Enter a valid date in YYYY-MM-DD format.",
                ["CommandPlayerOnly"] = "This command can only be used by a player.",
                ["RemoveInvalidCommand"] = "Invalid promo code removal command.",
                ["RemoveNoPermission"] = "You do not have permission to remove promo codes.",
                ["PromoCodeRemoved"] = "Promo code {promoCode} has been successfully removed.",
                ["PromoCodeRemovedLog"] = "Promo code {promoCode} was removed by admin {playerName}.",
                ["PromoCodeNotFound"] = "Promo code {promoCode} not found.",
                ["S_panelpromo"] = "Available Promo Codes:",
                ["S_adminpanel"] = "Admin panel",
                ["S_VDR"] = "Enter the name",
                ["S_VDRPR"] = "Example name 2025",
                ["S_CODR"] = "Enter the command for the promo code",
                ["S_CODRPR"] = "Example addgroup {player} vip 2d",
                ["S_CODRdata"] = "Expiration date",
                ["S_CODRFFdata"] = "Example 2025-1-31",
                ["S_CODRLIMIT"] = "Activation limit",
                ["S_CODRLIMFFIT"] = "Example 100"
            }, this);


            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["EnterPromoCode"] = "Введите ваш промокод",
                ["EnterPromoCodePlaceholder"] = "",
                ["Activate"] = "Активировать",
                ["Close"] = "X",
                ["S_info"] = "Информация",
                ["S_infotllo_line_1"] = "Новый промокод уже здесь!",
                ["S_infotllo_line_2"] = "Где найти? Легко:",
                ["S_infotllo_line_3"] = "Смотри наши свежие видео на TikTok",
                ["S_infotllo_line_4"] = "Или лови его в нашем Discord-канале!",
                ["S_infotllo_line_5"] = "Не упусти свой шанс получить бонусы!",
                ["PromoCodeAlreadyUsed"] = "Вы уже использовали этот промокод!",
                ["PromoCodeUnavailable"] = "Этот промокод больше недоступен.",
                ["PromoCodeExpired"] = "Этот промокод истек.",
                ["PromoCodeActivated"] = "Промокод активирован!",
                ["InvalidPromoCode"] = "Недействительный промокод.",
                ["PromoCodeActivatedBroadcast"] = "<color=#0099FF>{player}</color> активировал промокод {promoCode}!",
                ["ExpiryDate"] = "Дата окончания: {0}",
                ["Activations"] = "Активации: {0}/{1}",
                ["S_delete"] = "Удалить",
                ["PromoCodeNameEmpty"] = "Ошибка: Имя промокода не указано.",
                ["PromoCodeCommandEmpty"] = "Ошибка: Команда для промокода не указана.",
                ["PromoCodeLimitInvalid"] = "Ошибка: Лимит активаций не указан или неверен.",
                ["PromoCodeExpiryDateEmpty"] = "Ошибка: Дата истечения не указана.",
                ["PromoCodeCreated"] = "Промокод '{promoCodeName}' создан с истечением {promoCodeExpiryDate}!",
                ["PromoCodeExists"] = "Ошибка: Промокод '{promoCodeName}' уже существует.",
                ["LimitSaved"] = "Лимит активаций сохранен: {limit}",
                ["LimitError"] = "Ошибка: Введите корректное число больше нуля для лимита активаций.",
                ["PromoCodeNameEmpty"] = "Ошибка: Имя промокода не может быть пустым.",
                ["PromoCodeNameSaved"] = "Имя промокода сохранено: {promoCodeName}",
                ["PromoCodeCommandSaved"] = "Команда для промокода сохранена: {promoCodeCommand}",
                ["PromoCodeDateSaved"] = "Дата истечения сохранена: {expiryDate}",
                ["PromoCodeDateError"] = "Ошибка: Введите корректную дату в формате YYYY-MM-DD.",
                ["CommandPlayerOnly"] = "Эта команда может быть вызвана только игроком.",
                ["RemoveInvalidCommand"] = "Некорректная команда удаления промокода.",
                ["RemoveNoPermission"] = "У вас нет прав для удаления промокодов.",
                ["PromoCodeRemoved"] = "Промокод {promoCode} успешно удалён.",
                ["PromoCodeRemovedLog"] = "Промокод {promoCode} удалён администратором {playerName}.",
                ["PromoCodeNotFound"] = "Промокод {promoCode} не найден.",
                ["S_panelpromo"] = "Доступные Промокоды:",
                ["S_adminpanel"] = "Панель администратора",
                ["S_VDR"] = "Введите название",
                ["S_VDRPR"] = "Пример названия 2025",
                ["S_CODR"] = "Введите команду для промокода",
                ["S_CODRPR"] = "Пример addgroup {player} vip 2d",
                ["S_CODRdata"] = "Срок действия",
                ["S_CODRFFdata"] = "Пример 2025-1-31",
                ["S_CODRLIMIT"] = "Лимит активаций",
                ["S_CODRLIMFFIT"] = "Пример 100"
            }, this, "ru");


            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["EnterPromoCode"] = "Введіть ваш промокод",
                ["EnterPromoCodePlaceholder"] = "",
                ["Activate"] = "Активувати",
                ["Close"] = "X",
                ["S_info"] = "Інформація",
                ["S_infotllo_line_1"] = "Новий промокод вже тут!",
                ["S_infotllo_line_2"] = "Де знайти? Легко:",
                ["S_infotllo_line_3"] = "Дивись наші свіжі відео в TikTok",
                ["S_infotllo_line_4"] = "Або лови його у нашому Discord-каналі!",
                ["S_infotllo_line_5"] = "Не проґав свій шанс отримати бонуси!",
                ["PromoCodeAlreadyUsed"] = "Ви вже використали цей промокод!",
                ["PromoCodeUnavailable"] = "Цей промокод більше недоступний.",
                ["PromoCodeExpired"] = "Термін дії цього промокоду минув.",
                ["PromoCodeActivated"] = "Промокод активовано!",
                ["InvalidPromoCode"] = "Недійсний промокод.",
                ["PromoCodeActivatedBroadcast"] = "<color=#0099FF>{player}</color> активував промокод {promoCode}!",
                ["ExpiryDate"] = "Дата закінчення: {0}",
                ["Activations"] = "Активації: {0}/{1}",
                ["S_delete"] = "Удалить",
                ["PromoCodeNameEmpty"] = "Помилка: Ім'я промокоду не вказано.",
                ["PromoCodeCommandEmpty"] = "Помилка: Команда для промокоду не вказана.",
                ["PromoCodeLimitInvalid"] = "Помилка: Ліміт активацій не вказаний або невірний.",
                ["PromoCodeExpiryDateEmpty"] = "Помилка: Дата закінчення не вказана.",
                ["PromoCodeCreated"] = "Промокод '{promoCodeName}' створено з терміном дії до {promoCodeExpiryDate}!",
                ["PromoCodeExists"] = "Помилка: Промокод '{promoCodeName}' вже існує.",
                ["LimitSaved"] = "Ліміт активацій збережено: {limit}",
                ["LimitError"] = "Помилка: Введіть коректне число більше нуля для ліміту активацій.",
                ["PromoCodeNameEmpty"] = "Помилка: Ім'я промокоду не може бути порожнім.",
                ["PromoCodeNameSaved"] = "Ім'я промокоду збережено: {promoCodeName}",
                ["PromoCodeCommandSaved"] = "Команда для промокоду збережена: {promoCodeCommand}",
                ["PromoCodeDateSaved"] = "Дата закінчення збережена: {expiryDate}",
                ["PromoCodeDateError"] = "Помилка: Введіть коректну дату у форматі YYYY-MM-DD.",
                ["CommandPlayerOnly"] = "Ця команда може бути викликана лише гравцем.",
                ["RemoveInvalidCommand"] = "Некоректна команда видалення промокоду.",
                ["RemoveNoPermission"] = "У вас немає прав для видалення промокодів.",
                ["PromoCodeRemoved"] = "Промокод {promoCode} успішно видалено.",
                ["PromoCodeRemovedLog"] = "Промокод {promoCode} видалено адміністратором {playerName}.",
                ["PromoCodeNotFound"] = "Промокод {promoCode} не знайдено.",
                ["S_panelpromo"] = "Доступні Промокоди:",
                ["S_adminpanel"] = "Панель адміністратора",
                ["S_VDR"] = "Введіть назву",
                ["S_VDRPR"] = "Приклад назви 2025",
                ["S_CODR"] = "Введіть команду для промокоду",
                ["S_CODRPR"] = "Приклад addgroup {player} vip 2d",
                ["S_CODRdata"] = "Термін дії",
                ["S_CODRFFdata"] = "Приклад 2025-1-31",
                ["S_CODRLIMIT"] = "Ліміт активацій",
                ["S_CODRLIMFFIT"] = "Приклад 100"
            }, this, "uk");
        }
    }
}