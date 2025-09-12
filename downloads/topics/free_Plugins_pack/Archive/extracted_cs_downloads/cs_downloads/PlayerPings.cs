using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using ProtoBuf;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Oxide.Game.Rust.Cui;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Facepunch.Models.Database;
using Rust;
using VLB;
using System.Reflection;

namespace Oxide.Plugins
{
    [Info("PlayerPings", "digital.inc", "1.1.3")]
    [Description("Send quick pings for yourself and your teammates")]

    class PlayerPings : RustPlugin
    {
        private const Boolean LanguageEn = true;

        private static PlayerPings _;
        [PluginReference] Plugin ImageLibrary;
        private Dictionary<ulong, UserData> userData = new Dictionary<ulong, UserData>();
        private static Double CurrentTime => Facepunch.Math.Epoch.Current;
        private Dictionary<ulong, float> lastPingTime = new Dictionary<ulong, float>();
        string[] exclusions = { "props", "misc", "decor" };
        string[] lootableObjects = new string[]
        {
            "crate",
            "barrel",
            "trash",
            "supply_drop",
            "backpack",
            "box",
            "minecart",
            "vehicle_parts",
            "refinery",
        };

        private static FieldInfo serverInputField;

        private static Oxide.Core.Libraries.Permission permissionEx;

        #region Oxide Hooks
        private void Init()
        {
            _ = this;
            permission.RegisterPermission("playerpings.use", this);

            LoadData();
            lang.RegisterMessages(MessagesEn, this);
            lang.RegisterMessages(MessagesRu, this, "ru");
            foreach (var player in BasePlayer.activePlayerList)
            {
                CheckPlayerData(player);
            }

            if (config.poinOption.inputPings || config.poinOption.inputCenterMouseWheel)
            {
                serverInputField = typeof(BasePlayer).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                                         .FirstOrDefault(f => f.FieldType == typeof(InputState));
                timer.Once(1f, AddInPlayerComponent);
            }

            ConVar.Server.maximumPings = config.poinOption.maximumPings;

            cmd.AddConsoleCommand(config.poinOption.pingCommand, this, nameof(BindCreatePoint));
            cmd.AddChatCommand(config.poinOption.menuCommand, this, nameof(PingMenuChatOpenCommand));
        }

        private void OnServerInitialized()
        {
            LoadImages();
        }

        void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (playerUiContainers.TryGetValue(player.UserIDString, out string containerName))
                {
                    CuiHelper.DestroyUi(player, containerName);
                    playerUiContainers.Remove(player.UserIDString);
                }
            }

            RemoveInPlayerComponent();

            _ = null;
            config = null;
            userData = null;
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, "playerpings.use"))
            {
                player.gameObject.GetOrAddComponent<PlayerPingController>();
            }
            else
            {
                var comp = player.gameObject.GetComponent<PlayerPingController>();
                if (comp != null)
                    UnityEngine.Object.Destroy(comp);
            }
            CheckPlayerData(player);
            SendImages(player);
        }


        void OnPlayerDisconnected(BasePlayer player)
        {
            if (playerUiContainers.TryGetValue(player.UserIDString, out string containerName))
            {
                CuiHelper.DestroyUi(player, containerName);
                playerUiContainers.Remove(player.UserIDString);
            }
        }

        private void AddInPlayerComponent()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                OnPlayerConnected(player);
            }
        }

        private void RemoveInPlayerComponent()
        {
            foreach (var obj in UnityEngine.Object.FindObjectsOfType<PlayerPingController>())
            {
                UnityEngine.Object.Destroy(obj);
            }
        }

        private class PlayerPingController : MonoBehaviour
        {
            private BasePlayer player;
            private InputState input;
            private float nextKeysCheck;
            private float nextAllowedPingTime = 0f;
            private float currentTime => Time.realtimeSinceStartup;
            private const float keysCheckDelay = 0.01f;

            void Awake()
            {
                player = GetComponent<BasePlayer>();
                if (player == null)
                {
                    enabled = false;
                    return;
                }

                if (_ == null) return;

                if (serverInputField == null)
                {
                    enabled = false;
                    return;
                }

                input = (InputState)serverInputField.GetValue(player);
            }
            void LateUpdate()
            {
                if (!enabled || player == null)
                    return;

                if (input == null)
                    return;

                if (nextKeysCheck > currentTime)
                    return;

                nextKeysCheck = currentTime + keysCheckDelay;

                Invoke(nameof(TrySet), 0.01f);
            }

            private void TrySet()
            {
                if (!_.permission.UserHasPermission(player.UserIDString, "playerpings.use"))
                {
                    Destroy(this);
                    return;
                }

                if ((config.poinOption.inputPings && input.WasJustPressed(BUTTON.USE) && input.IsDown(BUTTON.FIRE_SECONDARY)) || (config.poinOption.inputCenterMouseWheel && input.WasJustPressed(BUTTON.FIRE_THIRD)))
                {
                    if (player.inventory.loot.IsLooting())
                        return;

                    if (UnityEngine.Time.realtimeSinceStartup < nextAllowedPingTime)
                        return;

                    Ray ray = player.eyes.HeadRay();
                    RaycastHit hit;
                    if (Physics.Raycast(ray, out hit, 300, LayerMask.GetMask("Default", "Terrain", "Construction", "Water", "Deployed", "Tree", "Debris", "World", "Player (Server)", "AI", "Ragdoll")))
                    {
                        _.CreatePing(player, hit.point, hit.collider.gameObject);
                        nextAllowedPingTime = UnityEngine.Time.realtimeSinceStartup + config.poinOption.pingCooldown;
                    }
                }
            }
        }

        void OnUserPermissionGranted(string userId, string perm)
        {
            if (perm != "playerpings.use")
                return;

            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player.UserIDString == userId)
                {
                    player.gameObject.GetOrAddComponent<PlayerPingController>();
                    break;
                }
            }
        }

        void OnUserPermissionRevoked(string userId, string perm)
        {
            if (perm != "playerpings.use")
                return;

            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player.UserIDString == userId)
                {
                    var comp = player.gameObject.GetComponent<PlayerPingController>();
                    if (comp != null)
                        UnityEngine.Object.Destroy(comp);
                    break;
                }
            }
        }

        #endregion

        #region Core
        void LoadImages()
        {
            if (!plugins.Exists("ImageLibrary"))
            {
                PrintError("The required ImageLibrary plugin was not found.");
                Interface.Oxide.UnloadPlugin(Title);
                return;
            }

            if (!ImageLibrary.Call<bool>("HasImage", "SelectYellow"))
                ImageLibrary.Call("AddImage", "https://i.imgur.com/4b7bMZ9.png", "SelectYellow");

            if (!ImageLibrary.Call<bool>("HasImage", "SelectRed"))
                ImageLibrary.Call("AddImage", "https://i.imgur.com/nrlaW6S.png", "SelectRed");

            if (!ImageLibrary.Call<bool>("HasImage", "SelectPurpl"))
                ImageLibrary.Call("AddImage", "https://i.imgur.com/gQrF6ft.png", "SelectPurpl");

            if (!ImageLibrary.Call<bool>("HasImage", "SelectGreen"))
                ImageLibrary.Call("AddImage", "https://i.imgur.com/zsldcP8.png", "SelectGreen");

            if (!ImageLibrary.Call<bool>("HasImage", "SelectBlue"))
                ImageLibrary.Call("AddImage", "https://i.imgur.com/WFf57U4.png", "SelectBlue");

            if (!ImageLibrary.Call<bool>("HasImage", "Toggle-Off"))
                ImageLibrary.Call("AddImage", "https://i.imgur.com/82mH3ob.png", "Toggle-Off");

            if (!ImageLibrary.Call<bool>("HasImage", "Toggle-On"))
                ImageLibrary.Call("AddImage", "https://i.imgur.com/vXDwwjD.png", "Toggle-On");
        }

        public void SendImages(BasePlayer player)
        {
            ImageLibrary?.Call("SendImage", player, "SelectYellow");
            ImageLibrary?.Call("SendImage", player, "SelectRed");
            ImageLibrary?.Call("SendImage", player, "SelectPurpl");
            ImageLibrary?.Call("SendImage", player, "SelectGreen");
            ImageLibrary?.Call("SendImage", player, "SelectBlue");
            ImageLibrary?.Call("SendImage", player, "Toggle-Off");
            ImageLibrary?.Call("SendImage", player, "Toggle-On");
        }

        string GetGameObjectPath(GameObject obj)
        {
            string path = obj.name;
            Transform parent = obj.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }

        private int CheckItem(GameObject obj)
        {
            string path = GetGameObjectPath(obj);
            bool isLootable = Array.Exists(lootableObjects, obj => path.ToLower().Contains(obj) && !exclusions.Any(ex => path.ToLower().Contains(ex)));
            bool isBuilding = path.ToLower().Contains("assets/prefabs/building core/") && !exclusions.Any(ex => path.ToLower().Contains(ex));

            if (isLootable || (path.ToLower().Contains("furnace") && !path.ToLower().Contains("props")))
            {
                return 11;
            }
            if (path.ToLower().Contains("node") || path.ToLower().Contains("resource") && !exclusions.Any(ex => path.ToLower().Contains(ex)))
            {
                return 10;
            }
            if (isBuilding)
            {
                return 12;
            }
            return 4;
        }

        private void CreatePing(BasePlayer player, Vector3 point, GameObject obj)
        {
            var activeItem = player.GetActiveItem();
            if (activeItem != null)
            {
                if (activeItem.info.shortname == "tool.binoculars")
                {
                    return;
                }
            }

            float currentTime = Time.time;

            if (lastPingTime.ContainsKey(player.userID) && currentTime - lastPingTime[player.userID] < config.poinOption.pingCooldown) return;

            lastPingTime[player.userID] = currentTime;

            UserData userData = GetUserData(player.userID);
            int colorIndex = userData != null ? userData.colorIndex : config.poinOption.colorIndex;
            int icon = userData.getTypeAuto ? CheckItem(obj) : 4;

            MapNote note = new MapNote()
            {
                isPing = true,
                timeRemaining = config.poinOption.duration,
                totalDuration = 0,
                colourIndex = colorIndex,
                icon = icon,
                noteType = 1,
                worldPosition = point,
                ShouldPool = true
            };

            if (player.State.pings == null)
            {
                player.State.pings = new List<MapNote>();
            }

            if (player.State.pings.Count >= ConVar.Server.maximumPings)
                player.State.pings.RemoveAt(0);

            player.State.pings.Add(note);
            player.DirtyPlayerState();
            SendPingWithPlayer(player);
            player.TeamUpdate(true);
        }

        void CheckPlayerData(BasePlayer player)
        {
            ulong userID = player.userID;
            if (!userData.ContainsKey(userID))
            {
                userData[userID] = new UserData
                {
                    colorIndex = config.poinOption.colorIndex,
                    getTypeAuto = true
                };
                SaveData();
            }
        }

        void SendPingWithPlayer(BasePlayer player)
        {
            if (player != null && player.IsConnected)
            {
                using (MapNoteList mapNoteList = Facepunch.Pool.Get<MapNoteList>())
                {
                    mapNoteList.notes = Facepunch.Pool.GetList<MapNote>();
                    mapNoteList.notes.AddRange((IEnumerable<MapNote>)player.State.pings);
                    player.ClientRPCPlayer<MapNoteList>((Network.Connection)null, player, "Client_ReceivePings", mapNoteList);
                    mapNoteList.notes.Clear();
                }
            }
        }

        private UserData GetUserData(ulong userID)
        {
            if (userData.ContainsKey(userID))
            {
                return userData[userID];
            }
            return null;
        }

        void SetUserColorIndex(ulong userID, int colorIndex)
        {
            if (userData.ContainsKey(userID))
            {
                userData[userID].colorIndex = colorIndex;
                SaveData();
            }
        }

        void SetUserTypeAuto(ulong userID, bool autoType)
        {
            if (userData.ContainsKey(userID))
            {
                userData[userID].getTypeAuto = autoType;
                SaveData();
            }
        }

        #endregion

        #region UI
        private Dictionary<string, string> playerUiContainers = new Dictionary<string, string>();

        private void PingMenuChatOpenCommand(BasePlayer player)
        {
            if (player == null)
                return;

            if (!permission.UserHasPermission(player.UserIDString, "playerpings.use")) return;

            string containerName = $"pingmenu_{player.UserIDString}";

            CuiHelper.DestroyUi(player, containerName);
            CuiHelper.DestroyUi(player, $"{containerName}_overlay");
            var container = new CuiElementContainer();

            UserData userData = GetUserData(player.userID);
            int colorIndex = userData.colorIndex;
            bool getTypeAuto = userData.getTypeAuto;

            container.Add(new CuiButton
            {
                Button = { Command = "pingmenu.close", Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
            }, "Overlay", $"{containerName}_overlay");

            container.Add(new CuiPanel
            {
                Image = { Color = "0.00 0.00 0.00 0.00" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-235 -113.5", OffsetMax = "235 113.5" },
                CursorEnabled = true,
            }, "Overlay", containerName);

            container.Add(new CuiElement
            {
                Parent = containerName,
                Name = "Header",
                Components =
                {
                    new CuiImageComponent {FadeIn = 0.2f, Color = "0.15 0.15 0.15 0.80", Material = "assets/content/ui/uibackgroundblur.mat"},
                    new CuiRectTransformComponent {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -50", OffsetMax = "0 0"}
                }
            });

            container.Add(new CuiElement
            {
                Parent = "Header",
                Components = {
                    new CuiTextComponent() { Color = HexToCuiColor("#D5CEC7"), FadeIn = 0.5f, Text = $"{lang.GetMessage("headTitle", this, player.UserIDString)}", FontSize = 32, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" },
                }
            });

            container.Add(new CuiElement
            {
                Parent = containerName,
                Name = $"{containerName}_ColorSelector",
                Components =
                {
                    new CuiImageComponent {FadeIn = 0.5f, Color = "0 0 0 0"},
                    new CuiRectTransformComponent {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -145", OffsetMax = "0 -55"}
                }
            });

            string[] parentAndImageNames = { "SelectPurpl", "SelectRed", "SelectYellow", "SelectGreen", "SelectBlue" };
            int[] colorIndexs = { 4, 3, 0, 2, 5 };
            int[] offsets = { 0, 95, 190, 285, 380 };

            float fadeValue = 0.3f;
            for (int i = 0; i < parentAndImageNames.Length; i++)
            {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = $"{offsets[i]} -45", OffsetMax = $"{offsets[i] + 90} 45" },
                    Button = { FadeIn = fadeValue, Command = $"pingmenu.selectcolor {colorIndexs[i]}", Color = colorIndex == colorIndexs[i] ? "0.15 0.15 0.15 0.99" : "0.15 0.15 0.15 0.80", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                    Text = { Text = "" }
                }, $"{containerName}_ColorSelector", parentAndImageNames[i]);

                fadeValue += 0.2f;
            }

            float ImageFadeValue = 0.3f;
            for (int i = 0; i < parentAndImageNames.Length; i++)
            {
                container.Add(new CuiElement
                {
                    Parent = parentAndImageNames[i],
                    Components =
                    {
                        new CuiRawImageComponent()
                        {
                            FadeIn = ImageFadeValue,
                            Png = (string) ImageLibrary.Call("GetImage", parentAndImageNames[i]),
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin = "0.5 0.5",
                            AnchorMax = "0.5 0.5",
                            OffsetMin = "-35 -35",
                            OffsetMax = "35 35"
                        },
                    }
                });
                ImageFadeValue += 0.2f;
            }

            container.Add(new CuiElement
            {
                Parent = containerName,
                Name = "Automatically_Toggle",
                Components =
                {
                    new CuiImageComponent {FadeIn = 0.2f, Color = "0.15 0.15 0.15 0.80", Material = "assets/content/ui/uibackgroundblur.mat"},
                    new CuiRectTransformComponent {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "45 -190", OffsetMax = "0 -150"}
                }
            });

            container.Add(new CuiElement
            {
                Parent = "Automatically_Toggle",
                Components = {
                    new CuiTextComponent() { Color = HexToCuiColor("#D5CEC7"), FadeIn = 0.5f, Text = $"{lang.GetMessage("getTypeAuto", this, player.UserIDString)}", FontSize = 16, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" },
                }
            });

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"0 -190", OffsetMax = $"-430 -150" },
                Button = { FadeIn = 0.3f, Command = $"pingmenu.toggletypeauto", Color = getTypeAuto == false ? "1.00 0.40 0.40 0.50" : "0.77 1.00 0.40 0.50", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                Text = { Text = "" }
            }, containerName, $"{containerName}_ToggleBtn");

            container.Add(new CuiElement
            {
                Parent = $"{containerName}_ToggleBtn",
                Components =
                    {
                        new CuiRawImageComponent()
                        {
                            FadeIn = 0.35f,
                            Png = (string) ImageLibrary.Call("GetImage", getTypeAuto == false ? "Toggle-Off" : "Toggle-On"),
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin = "0.5 0.5",
                            AnchorMax = "0.5 0.5",
                            OffsetMin = getTypeAuto == false ? "-12 -12" : "-12.75 -9.5",
                            OffsetMax = getTypeAuto == false ? "12 12" : "12.75 9.5"
                        },
                    }
            });

            container.Add(new CuiElement
            {
                Parent = containerName,
                Name = "BindInputBox",
                Components =
                {
                    new CuiImageComponent {FadeIn = 0.2f, Color = "0.15 0.15 0.15 0.80", Material = "assets/content/ui/uibackgroundblur.mat"},
                    new CuiRectTransformComponent {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -227", OffsetMax = "0 -195"}
                }
            });

            container.Add(new CuiElement
            {
                Parent = "BindInputBox",
                Components = {
                    new CuiInputFieldComponent() { Color = HexToCuiColor("#D5CEC7"), ReadOnly = true, Text = $"bind key {config.poinOption.pingCommand}", FontSize = 14, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" },
                }
            });

            CuiHelper.AddUi(player, container);

            playerUiContainers[player.UserIDString] = containerName;
        }

        [ConsoleCommand("pingmenu.close")]
        void CmdCloseMenu(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null)
                return;

            if (playerUiContainers.TryGetValue(player.UserIDString, out string containerName))
            {
                CuiHelper.DestroyUi(player, containerName);
                CuiHelper.DestroyUi(player, $"{containerName}_overlay");
                playerUiContainers.Remove(player.UserIDString);
            }
        }

        [ConsoleCommand("pingmenu.selectcolor")]
        void CmdSetColor(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null)
                return;

            if (!permission.UserHasPermission(player.UserIDString, "playerpings.use")) return;

            int colorIndex;
            if (!int.TryParse(arg.Args[0], out colorIndex))
            {
                PrintWarning($"Invalid color index. User: {player.userID}");
                return;
            }

            string containerName;
            if (!playerUiContainers.TryGetValue(player.UserIDString, out containerName))
            {
                PrintWarning($"Container not found for player: {player.UserIDString}");
                return;
            }

            CuiHelper.DestroyUi(player, $"{containerName}_ColorSelector");

            var container = new CuiElementContainer();

            container.Add(new CuiElement
            {
                Parent = containerName,
                Name = $"{containerName}_ColorSelector",
                Components =
                {
                    new CuiImageComponent {FadeIn = 0.5f, Color = "0 0 0 0"},
                    new CuiRectTransformComponent {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -145", OffsetMax = "0 -55"}
                }
            });

            string[] parentAndImageNames = { "SelectPurpl", "SelectRed", "SelectYellow", "SelectGreen", "SelectBlue" };
            int[] colorIndexs = { 4, 3, 0, 2, 5 };
            int[] offsets = { 0, 95, 190, 285, 380 };

            for (int i = 0; i < parentAndImageNames.Length; i++)
            {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = $"{offsets[i]} -45", OffsetMax = $"{offsets[i] + 90} 45" },
                    Button = { Command = $"pingmenu.selectcolor {colorIndexs[i]}", Color = colorIndex == colorIndexs[i] ? "0.15 0.15 0.15 0.97" : "0.15 0.15 0.15 0.80", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                    Text = { Text = "" }
                }, $"{containerName}_ColorSelector", parentAndImageNames[i]);
            }

            for (int i = 0; i < parentAndImageNames.Length; i++)
            {
                container.Add(new CuiElement
                {
                    Parent = parentAndImageNames[i],
                    Components =
                    {
                        new CuiRawImageComponent()
                        {
                            Png = (string) ImageLibrary.Call("GetImage", parentAndImageNames[i]),
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin = "0.5 0.5",
                            AnchorMax = "0.5 0.5",
                            OffsetMin = "-35 -35",
                            OffsetMax = "35 35"
                        },
                    }
                });
            }

            SetUserColorIndex(player.userID, colorIndex);
            PlayUIButtonClickSound(player);
            CuiHelper.AddUi(player, container);
        }

        private void PlayUIButtonClickSound(BasePlayer player)
        {
            if (player != null)
            {
                EffectNetwork.Send(new Effect("assets/prefabs/misc/xmas/candy cane club/effects/tap.prefab", player, 0, new Vector3(), new Vector3()), player.Connection);
            }
        }

        [ConsoleCommand("pingmenu.toggletypeauto")]
        void CmdToggleTypeAuto(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null)
                return;

            if (!permission.UserHasPermission(player.UserIDString, "playerpings.use")) return;

            UserData userData = GetUserData(player.userID);
            bool getTypeAuto = userData.getTypeAuto;

            getTypeAuto = !getTypeAuto;

            string containerName;
            if (!playerUiContainers.TryGetValue(player.UserIDString, out containerName))
            {
                PrintWarning($"Container not found for player: {player.UserIDString}");
                return;
            }

            CuiHelper.DestroyUi(player, $"{containerName}_ToggleBtn");

            var container = new CuiElementContainer();

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"0 -190", OffsetMax = $"-430 -150" },
                Button = { Command = $"pingmenu.toggletypeauto", Color = getTypeAuto == false ? "1.00 0.40 0.40 0.50" : "0.77 1.00 0.40 0.50", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                Text = { Text = "" }
            }, containerName, $"{containerName}_ToggleBtn");

            container.Add(new CuiElement
            {
                Parent = $"{containerName}_ToggleBtn",
                Components =
                    {
                        new CuiRawImageComponent()
                        {
                            Png = (string) ImageLibrary.Call("GetImage", getTypeAuto == false ? "Toggle-Off" : "Toggle-On"),
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin = "0.5 0.5",
                            AnchorMax = "0.5 0.5",
                            OffsetMin = getTypeAuto == false ? "-12 -12" : "-12.75 -9.5",
                            OffsetMax = getTypeAuto == false ? "12 12" : "12.75 9.5"
                        },
                    }
            });

            SetUserTypeAuto(player.userID, getTypeAuto);
            CuiHelper.AddUi(player, container);
        }

        private void BindCreatePoint(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null)
                return;

            if (!permission.UserHasPermission(player.UserIDString, "playerpings.use")) return;

            if (player.eyes == null) return;
            var ray = player.eyes.HeadRay();
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 300, LayerMask.GetMask("Default", "Terrain", "Construction", "Water", "Deployed", "Tree", "Debris", "World", "Player (Server)", "AI", "Ragdoll")))
            {
                CreatePing(player, hit.point, hit.collider.gameObject);
            }
        }

        #endregion

        #region Utility

        private static string HexToCuiColor(string hex)
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

            return $"{color.r:F2} {color.g:F2} {color.b:F2} {color.a:F2}";
        }

        #endregion

        #region Config
        private static ConfigData config;

        private class ConfigData
        {
            [JsonProperty(PropertyName = LanguageEn ? "Basic setup" : "Базовая настройка")]
            public poinOptions poinOption { get; set; }
            public class poinOptions
            {
                [JsonProperty(PropertyName = LanguageEn ? "Color(0-yellow,1-blue,2-green,3-red,4-purple,5-blue)" : "Цвет(0-желтый,1-синий,2-зеленый,3-красный,4-фиолетовый,5-голубой)")]
                public int colorIndex { get; set; }

                [JsonProperty(PropertyName = LanguageEn ? "Maximum number of pings per player" : "Максимальное количество меток для одного игрока")]
                public int maximumPings { get; set; } = 5;

                [JsonProperty(PropertyName = LanguageEn ? "Maximum distance" : "Максимальная дистанция")]
                public float maxDistance { get; set; }

                [JsonProperty(PropertyName = LanguageEn ? "Ping display time" : "Время отображения метки")]
                public float duration { get; set; }

                [JsonProperty(PropertyName = LanguageEn ? "Enable ping metod Aiming + key use (E)" : "Включить способ установки метки (Прицеливание + E)")]
                public bool inputPings { get; set; }

                [JsonProperty(PropertyName = LanguageEn ? "Enable ping metod Center mouse wheel" : "Включить способ установки метки (Нажатие колесика мыши)")]
                public bool inputCenterMouseWheel { get; set; }

                [JsonProperty(PropertyName = LanguageEn ? "Delay between pings (seconds): 0.2 seconds as standard" : "Задержка между метками (секунды): По стандарту 0.2 секунды")]
                public float pingCooldown { get; set; }

                [JsonProperty(PropertyName = LanguageEn ? "Command to set ping" : "Команда для установки метки")]
                public string pingCommand { get; set; } = "playerping.set";

                [JsonProperty(PropertyName = LanguageEn ? "Menu Open Command" : "Команда для открытия меню")]
                public string menuCommand { get; set; } = "pingmenu";
            }

            public Oxide.Core.VersionNumber Version { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<ConfigData>();

            if (config.Version < Version)
                UpdateConfigValues();

            Config.WriteObject(config, true);
        }

        protected override void LoadDefaultConfig() => config = GetBaseConfig();

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                poinOption = new ConfigData.poinOptions
                {
                    colorIndex = 3,
                    maximumPings = 5,
                    maxDistance = 300,
                    duration = 3.0f,
                    inputPings = true,
                    inputCenterMouseWheel = true,
                    pingCooldown = 0.2f,
                    pingCommand = "playerping.set",
                    menuCommand = "pingmenu"
                },

                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(config, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Plugin update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();

            if (config.Version < new VersionNumber(1, 0, 0))
                config = baseConfig;

            config.Version = Version;
            PrintWarning("Config update completed!");
        }

        #endregion

        #region Data

        private class UserData
        {
            public int colorIndex;
            public bool getTypeAuto;
        }

        void LoadData()
        {
            userData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, UserData>>(this.Name);
            if (userData == null)
            {
                LoadDefaultData();
            }
        }

        void LoadDefaultData()
        {
            userData = new Dictionary<ulong, UserData>();
        }

        void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(this.Name, userData);
        }

        #endregion

        #region Localization

        private string getUserLang(string userId)
        {
            string langCode = lang.GetLanguage(userId);
            if (langCode != "ru" && langCode != "en")
                langCode = "en";
            return langCode;
        }

        private string Message(string key, string playerId = null) => lang.GetMessage(key, this, playerId);

        private readonly Dictionary<string, string> MessagesEn = new Dictionary<string, string>
        {
            ["headTitle"] = "MARKER SETTINGS",
            ["getTypeAuto"] = "Automatically define the type of object",
        };

        private readonly Dictionary<string, string> MessagesRu = new Dictionary<string, string>
        {
            ["headTitle"] = "НАСТРОЙКИ МАРКЕРА",
            ["getTypeAuto"] = "Автоматически определять тип объектов",
        };

        #endregion
    }
} 