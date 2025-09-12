using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Libraries.Covalence;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Rust Gestures", "Beast_", "1.0.2")]
    [Description("Unlock Rust DLC Gestures For Everyone")]
    public class RustGestures : RustPlugin
    {
        #region Fields
        [PluginReference]
        private Plugin ImageLibrary;

        private static RustGestures instance;
        private Configuration config;

        private const string PERMS = "rustgestures.use";
        private const string MAINPANELNAME = "RustGesturesPanel";

        private static Dictionary<ulong, int> playerPages = new Dictionary<ulong, int>();
        #endregion

        #region Configuration
        private class Configuration
        {
            [JsonProperty("Rust Gestures Commands")]
            public string[] commands = new string[]
            {
                "gestures",
				"dance",
                "rg"
            };

            [JsonProperty("Show all gestures in a single page")]
            public bool disablePages = false;

            [JsonProperty("Gestures list", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<GestureInfo> gesturesList = new List<GestureInfo>
            {
                new GestureInfo { Id = 478760625, Name = "Raise the Roof", Image = "https://i.ibb.co/WPqCxYG/Raise-the-Roof.png" },
                new GestureInfo { Id = 1855420636, Name = "Cabbage Patch", Image = "https://i.ibb.co/tDtJg8S/Cabbage-Patch.png" },
                new GestureInfo { Id = 1702547860, Name = "The Twist", Image = "https://i.ibb.co/LNPKvK9/The-Twist.png" },
                new GestureInfo { Id = 2662905952, Name = "Loser", Image = "https://i.ibb.co/SB14sXC/Loser.png" },
                new GestureInfo { Id = 452952148, Name = "Cut Throat", Image = "https://i.ibb.co/XkBjJtP/Cut-Throat.png" },
                new GestureInfo { Id = 933114077, Name = "Finger Gun", Image = "https://i.ibb.co/6mJxgMp/Finger-Gun.png" },
                new GestureInfo { Id = 2299615665, Name = "Shush!", Image = "https://i.ibb.co/h8yRSL5/Shush.png" },
                new GestureInfo { Id = 995421278, Name = "Watching You", Image = "https://i.ibb.co/kykZPwB/Watching-You.png"},
                new GestureInfo { Id = 3823341693, Name = "Rock Paper Scissors", Image = "https://i.ibb.co/pzXLhQ3/Rock-Paper-Scissors.png" },
                new GestureInfo { Id = 722391486, Name = "Shush! (Vocal)", Image = "https://i.ibb.co/FwRqby9/Shush-Vocal.png" },
                new GestureInfo { Id = 3712618926, Name = "No-no!", Image = "https://i.ibb.co/4s1SSKH/No-No.png" },
                new GestureInfo { Id = 1161511754, Name = "Knuckle Crack", Image = "https://i.ibb.co/TqqfyMy/Knuckle-Crack.png" }
            };

            public string ToJson() => JsonConvert.SerializeObject(this);
            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }

        public class GestureInfo
        {
            public uint Id { get; set; }
            public string Name { get; set; }
            public string Image { get; set; }
        }

        protected override void LoadDefaultConfig() => config = new Configuration();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) throw new JsonException();

                SaveConfig();
            }
            catch
            {
                PrintWarning($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion

        #region Hooks
        private void Init()
        {
            instance = this;
            permission.RegisterPermission(PERMS, this);
        }

        private void OnServerInitialized()
        {
            if (ImageLibrary == null || !ImageLibrary.IsLoaded)
            {
                PrintError("ImageLibrary is not loaded! Plugin has been disabled!");
                Interface.Oxide.UnloadPlugin(Name);
                return;
            }

            AddCovalenceCommand(config.commands, nameof(RustGesturesCommand));

            foreach (var gesture in config.gesturesList)
            {
                AddImage(gesture.Image, $"image_{gesture.Id}");
            }
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player != null && player.IsConnected)
                {
                    UIHelper.ClearUI(player);
                }
            }
            playerPages.Clear();
        }

        #endregion

        #region Commands
        private void RustGesturesCommand(IPlayer iplayer, string command, string[] args)
        {
            var player = iplayer.Object as BasePlayer;
            if (player == null) return;

            if (!permission.UserHasPermission(player.UserIDString, PERMS))
            {
                PrintToChat(player, "You don't have permissions to use this command!");
                return;
            }

            if (!playerPages.ContainsKey(player.userID))
                playerPages[player.userID] = 0;

            UIHelper.CreateUI(player);
        }

        [ConsoleCommand("rustgestures.previous")]
        private void PreviousPage(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;

            if (playerPages[player.userID] > 0)
            {
                playerPages[player.userID]--;
                UIHelper.CreateUI(player);
            }
        }

        [ConsoleCommand("rustgestures.next")]
        private void NextPage(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;

            int currentPage = playerPages[player.userID];
            int totalPages = Mathf.CeilToInt((float)config.gesturesList.Count / 3);

            if (currentPage < totalPages - 1)
            {
                playerPages[player.userID]++;
                UIHelper.CreateUI(player);
            }
        }

        [ConsoleCommand("rustgestures.exit")]
        private void ExitMenu(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;

            UIHelper.ClearUI(player);
            playerPages.Remove(player.userID);
        }

        [ConsoleCommand("rustgestures.select")]
        private void SelectGestureCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null || arg.Args == null || arg.Args.Length < 1) return;

            string selected = arg.Args[0];
            if (string.IsNullOrEmpty(selected)) return;

            if (uint.TryParse(selected, out uint gestureID))
            {
                var gesture = config.gesturesList.FirstOrDefault(g => g.Id == gestureID);
                if (gesture == null) return;

                var gestureConfig = GestureCollection.Instance.AllGestures.FirstOrDefault((gs) => gs.gestureId == gesture.Id);
                if (gestureConfig == null) return;

                UIHelper.ClearUI(player);
                player.Server_StartGesture(gestureConfig, BasePlayer.GestureStartSource.Player, true);
            }
        }
        #endregion

        #region Helper

        #region UI
        public static class UIHelper
        {
            public static void CreateUI(BasePlayer player)
            {
                var container = new CuiElementContainer();
                container.Add(new CuiPanel
                {
                    CursorEnabled = true,
                    Image = { Color = "0.1125 0.1125 0.1125 1" },
                    RectTransform =
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "-275 -190",
                        OffsetMax = "275 190"
                    }
                }, "Overlay", MAINPANELNAME);

                container.Add(new CuiElement
                {
                    Name = "PanelTitle",
                    Parent = MAINPANELNAME,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = "Rust Gestures",
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 30,
                            Align = TextAnchor.MiddleCenter,
                            Color = "1 1 1 1"
                        },
                        new CuiOutlineComponent
                        {
                            Color = "0 0 0 1",
                            Distance = "1 1"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0.85",
                            AnchorMax = "1 1",
                        }
                    }
                });

                if (instance.config.disablePages)
                {
                    int gesturesPerRow = 4;
                    float buttonWidth = 48f;
                    float buttonHeight = 52f;
                    float spacingX = 5f;
                    float spacingY = 5f;

                    float startX = -((gesturesPerRow * (buttonWidth + spacingX)) - spacingX) / 2f;
                    float startY = 0.63f;

                    for (int i = 0; i < instance.config.gesturesList.Count; i++)
                    {
                        int row = i / gesturesPerRow;
                        int col = i % gesturesPerRow;

                        float positionX = startX + col * (buttonWidth + spacingX);
                        float positionY = startY - row * (buttonHeight + spacingY) / 250f;

                        var gesture = instance.config.gesturesList[i];
                        AddButton(container, gesture, positionX, positionY, buttonWidth, buttonHeight);
                    }
                }
                else
                {
                    int currentPage = playerPages[player.userID];
                    int totalPages = Mathf.CeilToInt((float)instance.config.gesturesList.Count / 3);

                    for (int i = 0; i < 3; i++)
                    {
                        int gestureIndex = currentPage * 3 + i;
                        if (gestureIndex >= instance.config.gesturesList.Count)
                            break;

                        var gesture = instance.config.gesturesList[gestureIndex];
                        AddButton(container, gesture, i);
                    }

                    if (currentPage > 0)
                    {
                        container.Add(new CuiButton
                        {
                            Button =
                            {
                                Color = "0 0 0 0",
                                Command = $"rustgestures.previous"
                            },
                            Text =
                            {
                                Text = "<",
                                Font = "robotocondensed-regular.ttf",
                                FontSize = 18,
                                Align = TextAnchor.MiddleCenter,
                                Color = "1 1 1 1"
                            },
                            RectTransform =
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "0 0",
                                OffsetMin = "0 18",
                                OffsetMax = "60 58"
                            }
                        }, MAINPANELNAME, "PreviousButton");
                    }

                    if (currentPage < totalPages - 1)
                    {
                        container.Add(new CuiButton
                        {
                            Button =
                            {
                                Color = "0 0 0 0",
                                Command = $"rustgestures.next"
                            },
                            Text =
                            {
                                Text = ">",
                                Font = "robotocondensed-regular.ttf",
                                FontSize = 18,
                                Align = TextAnchor.MiddleCenter,
                                Color = "1 1 1 1"
                            },
                            RectTransform =
                            {
                                AnchorMin = "1 0",
                                AnchorMax = "1 0",
                                OffsetMin = "-60 18",
                                OffsetMax = "0 58"
                            }
                        }, MAINPANELNAME, "NextButton");
                    }
                }

                container.Add(new CuiButton
                {
                    Button =
                    {
                        Color = "0.8 0 0 1",
                        Command = $"rustgestures.exit"
                    },
                    Text =
                    {
                        Text = "Exit",
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 15,
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 1"
                    },
                    RectTransform =
                    {
                        AnchorMin = $"0.5 {(instance.config.disablePages ? "-0.03" : "0")}",
                        AnchorMax = $"0.5 {(instance.config.disablePages ? "-0.03" : "0")}",
                        OffsetMin = "-45 18",
                        OffsetMax = "45 58"
                    }
                }, MAINPANELNAME, "ExitButton");

                CuiHelper.DestroyUi(player, MAINPANELNAME);
                CuiHelper.AddUi(player, container);
            }

            public static void AddButton(CuiElementContainer container, GestureInfo gesture, float posX, float posY, float width, float height)
            {
                container.Add(new CuiButton
                {
                    Button =
                    {
                        Color = "0.1886792 0.1886792 0.1886792 1",
                        Command = $"rustgestures.select {gesture.Id}"
                    },
                    Text =
                    {
                        Text = gesture.Name,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 12,
                        Align = TextAnchor.LowerCenter,
                        Color = "1 1 1 1"
                    },
                    RectTransform =
                    {
                        AnchorMin = $"{0.5f + posX / 275f} {posY}",
                        AnchorMax = $"{0.5f + (posX + width) / 275f} {posY + height / 250f}"
                    }
                }, MAINPANELNAME, $"Button_{gesture.Image}");

                container.Add(new CuiElement
                {
                    Parent = $"Button_{gesture.Image}",
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = instance.GetImage($"image_{gesture.Id}")
                        },
                        new CuiOutlineComponent
                        {
                            Color = "0 0 0 0.5",
                            Distance = "1 -1"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.1 0.1",
                            AnchorMax = "0.9 0.9"
                        }
                    }
                });
            }

            public static void AddButton(CuiElementContainer container, GestureInfo gesture, int index)
            {
                float buttonWidth = 120f;
                float spacing = 20f;
                float startX = -buttonWidth * 1.5f - spacing;
                float positionX = startX + index * (buttonWidth + spacing);

                container.Add(new CuiButton
                {
                    Button =
                    {
                        Color = "0.1886792 0.1886792 0.1886792 1",
                        Command = $"rustgestures.select {gesture.Id}"
                    },
                    Text =
                    {
                        Text = gesture.Name,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 14,
                        Align = TextAnchor.LowerCenter,
                        Color = "1 1 1 1"
                    },
                    RectTransform =
                    {
                        AnchorMin = $"{0.5f + positionX / 550f} 0.34",
                        AnchorMax = $"{0.5f + (positionX + buttonWidth) / 550f} 0.7"
                    }
                }, MAINPANELNAME, $"Button_{gesture.Image}");

                container.Add(new CuiElement
                {
                    Parent = $"Button_{gesture.Image}",
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = instance.GetImage($"image_{gesture.Id}")
                        },
                        new CuiOutlineComponent
                        {
                            Color = "0 0 0 0.5",
                            Distance = "1 -1"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0.2",
                            AnchorMax = "1 0.8"
                        }
                    }
                });
            }

            public static void ClearUI(BasePlayer player)
            {
                CuiHelper.DestroyUi(player, MAINPANELNAME);
                playerPages.Remove(player.userID);
            }
        }
        #endregion

        public string GetImage(string shortname, ulong skin = 0)
        {
            if (ImageLibrary == null || !ImageLibrary.IsLoaded)
            {
                PrintWarning("ImageLibrary is not loaded or has been unloaded.");
                return null;
            }

            return (string)ImageLibrary.Call("GetImage", shortname, skin);
        }

        public bool AddImage(string url, string shortname, ulong skin = 0)
        {
            if (ImageLibrary == null || !ImageLibrary.IsLoaded)
            {
                PrintWarning("ImageLibrary is not loaded or has been unloaded.");
                return false;
            }

            return (bool)ImageLibrary.Call("AddImage", url, shortname, skin);
        }
        #endregion
    }
}