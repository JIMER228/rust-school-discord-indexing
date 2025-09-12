using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("CopyURL", "David", "1.0.0")]

    public class CopyUrl : RustPlugin
    {
        string ui_cached;

        void OnServerInitialized()
        {
            foreach (string command in config.commands.Keys)
                cmd.AddChatCommand(command, this, "ChatCommands");

            CacheUi();
        }

        void CacheUi()
        {
            var ui = new CuiElementContainer();
            //background
            ui.Add(new CuiPanel
            {
                Image = { Color = "0.70 0.67 0.65 0.3", Material = "assets/content/ui/uibackgroundblur.mat", FadeIn = 0.3f },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                FadeOut = 0f,
                CursorEnabled = true,
                KeyboardEnabled = true
            },
                "Overlay",
                "copyUrl.main");
            //background radial blurr
            ui.Add(new CuiElement
            {
                Parent = "copyUrl.main",
                Name = "copyUrl.radialblurr",
                Components =
                 {
                    new CuiImageComponent { Material = "assets/icons/iconmaterial.mat", Sprite = "assets/content/ui/ui.background.transparent.radial.psd", Color = "0 0 0 0.93", FadeIn = 0.3f},
                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"}
                 },
                FadeOut = 0f
            });
            //close button
            ui.Add(new CuiButton
            {
                Button = { Close = "copyUrl.main", Color = "0 0 0 0", Material = "assets/icons/iconmaterial.mat", FadeIn = 0.3f },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
            },
                "copyUrl.main",
                "copyUrl.closebtn");
            //container
            ui.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0", Material = "assets/icons/iconmaterial.mat", FadeIn = 0.3f },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-200 -150", OffsetMax = "200 150" },
                FadeOut = 0f,
                CursorEnabled = true,
                KeyboardEnabled = true
            },
                "copyUrl.main",
                "copyUrl.offset");
            //title
            ui.Add(new CuiElement
            {
                Parent = "copyUrl.offset",
                Name = "copyUrl.title",
                Components =
                    {
                        new CuiTextComponent
                        {
                            Text = "{title}",
                            FontSize = 32,
                            Font = "robotocondensed-bold.ttf",
                            Align = TextAnchor.UpperCenter,
                            Color = "1 1 1 0.6",
                            FadeIn = 0.3f,
                        },

                        new CuiRectTransformComponent
                        {
                             AnchorMin = "0 0.6",
                             AnchorMax = "1 0.75"
                        }
                    },
                FadeOut = 0f
            });
            //copy button panel
            ui.Add(new CuiPanel
            {
                Image = { Color = "0.57 0.65 0.42 1", Material = "assets/content/ui/uibackgroundblur.mat", FadeIn = 0.3f },
                RectTransform = { AnchorMin = "0.2 0.38", AnchorMax = "0.8 0.48" },
                FadeOut = 0f,
                CursorEnabled = true,
                KeyboardEnabled = true
            },
                "copyUrl.offset",
                "copyUrl.inputcopybutton");
            //button text
            ui.Add(new CuiElement
            {
                Parent = "copyUrl.inputcopybutton",
                Name = "copyUrl.inputcopybutton_text",
                Components =
                    {
                        new CuiTextComponent
                        {
                            Text = "COPY URL",
                            FontSize = 12,
                            Font = "robotocondensed-bold.ttf",
                            Align = TextAnchor.MiddleCenter,
                            Color = "1 1 1 0.6",
                            FadeIn = 0.3f,
                        },

                        new CuiRectTransformComponent
                        {
                             AnchorMin = "0 0",
                             AnchorMax = "1 1"
                        }
                    },
                FadeOut = 0f
            });
            //input container
            ui.Add(new CuiPanel
            {
                Image = { Color = "0.71 0.68 0.67 0.45", Material = "assets/content/ui/uibackgroundblur.mat", FadeIn = 0.3f },
                RectTransform = { AnchorMin = "0.2 0.5", AnchorMax = "0.8 0.6" },
                FadeOut = 0f,
                CursorEnabled = true,
                KeyboardEnabled = true
            },
                "copyUrl.offset",
                "copyUrl.inputbox");

            //inputfield
            ui.Add(new CuiElement
            {
                Parent = "copyUrl.inputbox",
                Name = "copyUrl.inputfield",

                Components =
                    {
                        new CuiInputFieldComponent
                        {

                            Text = "{link}",
                            CharsLimit = 250,
                            Color = "1 1 1 0.75",
                            IsPassword = false,
                            Command = "",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 14,
                            Align = TextAnchor.UpperCenter
                        },

                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 -1.3",
                            AnchorMax = "1 0.75"

                        }

                    },
            });
            //description
            ui.Add(new CuiElement
            {
                Parent = "copyUrl.offset",
                Name = "copyUrl.description_text",
                Components =
                    {
                        new CuiTextComponent
                        {
                            Text = "{description}",
                            FontSize = 11,
                            Font = "robotocondensed-regular.ttf",
                            Align = TextAnchor.UpperCenter,
                            Color = "1 1 1 0.6",
                            FadeIn = 0.3f,
                        },

                        new CuiRectTransformComponent
                        {
                             AnchorMin = "0.1 0",
                             AnchorMax = "0.9 0.34"
                        }
                    },
                FadeOut = 0f
            });

            ui_cached = ui.ToString();
        }

        void ChatCommands(BasePlayer player, string command, string[] args)
        {
            if (!config.commands.ContainsKey(command.ToLower()))
                return;

            OpenUi(player, command);
        }

        void OpenUi(BasePlayer player, string command)
        {
            CuiHelper.DestroyUi(player, "copyUrl.main");
            CuiHelper.AddUi(
                player,
                ui_cached.ToString().Replace("{title}", config.commands[command.ToLower()].title)
                .Replace("{description}", config.commands[command.ToLower()].description)
                .Replace("{link}", config.commands[command.ToLower()].link)
            );
        }

        #region [Config] 

        private Configuration config;
        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<Configuration>();
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = Configuration.CreateConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        class Configuration
        {
            [JsonProperty("Commands")]
            public Dictionary<string, Command> commands { get; set; }

            public class Command
            {
                [JsonProperty("Title")]
                public string title { get; set; }

                [JsonProperty("Link")]
                public string link { get; set; }

                [JsonProperty("Description")]
                public string description { get; set; }
            }
            public static Configuration CreateConfig()
            {
                return new Configuration
                {
                    commands = new Dictionary<string, CopyUrl.Configuration.Command>(StringComparer.InvariantCultureIgnoreCase)
                    {
                        {
                            "discord", new CopyUrl.Configuration.Command
                            {
                                title = "DISCORD INVITE LINK",
                                link = "https://discord.gg/rustplugins",
                                description = "Join our discord to get more free plugins!"
                            }
                        },
                        {
                            "vip", new CopyUrl.Configuration.Command
                            {
                                title = "GET VIP PACKAGE AT",
                                link = "www.rustplugins.net",
                                description = "Support our server by purchasing VIP."
                            }
                        },
                    },
                };
            }
        }
        #endregion
    }
}