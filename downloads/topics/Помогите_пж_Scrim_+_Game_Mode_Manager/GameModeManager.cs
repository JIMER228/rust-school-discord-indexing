// Reference: Facepunch.Sqlite
using CompanionServer;
using ConVar;
//using Facepunch.Sqlite;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Database;
using Oxide.Core.Plugins;
using Oxide.Core.SQLite.Libraries;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

namespace Oxide.Plugins
{
    [Info("Game Mode Manager", "discord.gg/9vyTXsJyKR", "1.1.3")]
    [Description("The core and backbone plugin of both Scrim and Recoil Training Ground minigames.")]
    public class GameModeManager : RustPlugin
    {
        [PluginReference] Plugin ZoneManager, InventoryLoadout;
        private static GameModeManager _instance;
        public const string PermissionAdmin = "gamemodemanager.admin";
        public const string PermissionVanishBypass = "gamemodemanager.vanish.bypass";
        public const string PermissionIgnore = "gamemodemanager.ignore";
        private readonly string _databasePath = $"{Interface.Oxide.DataDirectory}//{MainFolderName}//{nameof(GameModeManager).ToLower()}.db";
        private readonly string _globalStatisticsTableName = "Global_Statistics";
        public Configuration PluginConfig;
        private DynamicConfigFile _dataManager;
        private PluginData _pluginData;
        public const string MainFolderName = "GameModes";
        public LobbyData LobbyToEdit;
        public HashSet<ulong> TeleportingPlayers = new HashSet<ulong>();
        public List<BasePlayer> TransportedPlayers = new List<BasePlayer>();
        public ImageManagerBehaviour ImageManager;
        private const float TextRefreshRate = 2f;
        private readonly Regex _steamAvatarRegex =
            new Regex(@"(?<=<avatarMedium>[\w\W]+)https://.+\.jpg(?=[\w\W]+<\/avatarMedium>)", RegexOptions.Compiled);

        private Core.Database.Connection _statisticsDatabaseConnection;// = new Database();
        SQLite Sqlite = Interface.GetMod().GetLibrary<SQLite>();
        private readonly Dictionary<ulong, Timer> _freezeTimers = new Dictionary<ulong, Timer>();
        public Dictionary<ulong, string> PlayersAvatarUrls = new Dictionary<ulong, string>();
        public Dictionary<ulong, string> PlayerSearch = new Dictionary<ulong, string>();
        public Dictionary<ulong, List<SelectablePlayer>> PlayerSearchResults = new Dictionary<ulong, List<SelectablePlayer>>();
        public Dictionary<ulong, PlayerPageIndex> PlayerPageIndexes = new Dictionary<ulong, PlayerPageIndex>();

        public Dictionary<int, Lobby> Lobbies { get; set; } = new Dictionary<int, Lobby>();
        #region Classes                 

        #region Config

        public class Configuration
        {
            [JsonProperty(PropertyName = "Use Better Chat")]
            public bool UseBetterChat { get; set; }

            [JsonProperty(PropertyName = "Chat command to leave the arena")]
            public string ArenaLeaveCommand { get; set; } = "Leave";

            [JsonProperty(PropertyName = "Chat command to show lobby pause menu")]
            public string ShowLobbyPauseMenuCommand { get; set; } = "Menu";

            [JsonProperty(PropertyName = "Up Arrow Icon")]
            public string UpArrow { get; set; } = "https://i.ibb.co/f1QCfNG/up.png";

            [JsonProperty(PropertyName = "Down Arrow Icon")]
            public string DownArrow { get; set; } = "https://i.ibb.co/ZzkvpWX/down.png";

            [JsonProperty(PropertyName = "Left Arrow Icon")]
            public string LeftArrow { get; set; } = "https://i.ibb.co/Tr09rGF/left.png";

            [JsonProperty(PropertyName = "Right Arrow Icon")]
            public string RightArrow { get; set; } = "https://i.ibb.co/CvjHK1Z/right.png";

            [JsonProperty(PropertyName = "Search Button Icon")]
            public string SearchButton { get; set; } = "https://i.ibb.co/TL0J5s7/arrow-forward.png";

            [JsonProperty(PropertyName = "Check Mark Icon")]
            public string Checked { get; set; } = "https://i.ibb.co/CPCK3cs/check.png";

            [JsonProperty(PropertyName = "Clear Input Icon")]
            public string Clear { get; set; } = "https://i.ibb.co/Qm3zRY6/clear.png";

            [JsonProperty(PropertyName = "Close Button Icon")]
            public string Close { get; set; } = "https://i.ibb.co/fQnDjW6/close.png";

            [JsonProperty(PropertyName = "Search Icon")]
            public string Search { get; set; } = "https://i.ibb.co/LtzyY5F/search.png";

            [JsonProperty(PropertyName = "Info Icon")]
            public string Info { get; set; } = "https://i.ibb.co/1dFT9RL/info.png";

            [JsonProperty(PropertyName = "VIP Locked Icon")]
            public string VipLocked { get; set; } = "https://i.ibb.co/fr0YhNY/vip-locked.png";

            [JsonProperty(PropertyName = "Locked Icon")]
            public string Locked { get; set; } = "https://i.ibb.co/pWh2Gh8/locked.png";

            [JsonProperty(PropertyName = "UnLocked Icon")]
            public string UnLocked { get; set; } = "https://i.ibb.co/QCMYJvq/unlocked.png";

            [JsonProperty(PropertyName = "Match Leader Icon")]
            public string MatchLeader { get; set; } = "https://i.ibb.co/jHQmGT8/crown.png";
        }

        #endregion Config

        #region Data
        public class PluginData
        {
            [JsonProperty(PropertyName = "Lobbies")]
            public HashSet<LobbyData> Lobbies { get; set; } = new HashSet<LobbyData>();
        }
        public class LobbyData
        {
            [JsonProperty(PropertyName = "Lobby - Name", Order = 1)]
            public string Name { get; set; }

            [JsonProperty(PropertyName = "Lobby - Zone ID", Order = 2)]
            public string ZoneId { get; set; }

            [JsonProperty(PropertyName = "Lobby - ID", Order = 3)]
            public int Id { get; set; }

            [JsonProperty(PropertyName = "Lobby - Main - Enabled", Order = 4)]
            public bool IsMain { get; set; }

            [JsonProperty(PropertyName = "Lobby - Loadout", Order = 5)]
            public string LoadoutName { get; set; }

            [JsonProperty(PropertyName = "Whitelist Mode - Enabled", Order = 6)]
            public bool RestrictAccess { get; set; }

            [JsonProperty(PropertyName = "Whitelisted Players", Order = 7)]
            public HashSet<ulong> AllowedPlayers { get; set; } = new HashSet<ulong>();

            [JsonProperty(PropertyName = "Banned Players", Order = 8)]
            public HashSet<ulong> BannedPlayers { get; set; } = new HashSet<ulong>();

            [JsonProperty(PropertyName = "Spawn Points", Order = 9)]
            public HashSet<LobbySpawnData> SpawnLocations { get; set; } = new HashSet<LobbySpawnData>();

            public override bool Equals(object obj)
            {
                var match = obj as LobbyData;
                if (match == null)
                {
                    return false;
                }

                return match.Id == Id;
            }
            public override int GetHashCode()
            {
                return Id.GetHashCode();
            }
        }
        public class ArenaData
        {
            [JsonProperty(PropertyName = "Arena - Name", Order = 1)]
            public string Name { get; set; }

            [JsonProperty(PropertyName = "Arena - ID", Order = 2)]
            public int Id { get; set; }

            [JsonProperty(PropertyName = "Arena - Enabled", Order = 3)]
            public bool IsEnabled { get; set; }

            [JsonProperty(PropertyName = "Arena - Zone ID", Order = 4)]
            public string ZoneId { get; set; }

            [JsonProperty(PropertyName = "Arena - Lobby ID", Order = 5)]
            public int LobbyId { get; set; }

            [JsonProperty(PropertyName = "Match - Type", Order = 6)]
            public MatchType MatchType { get; set; }

            [JsonProperty(PropertyName = "Match - Capacity", Order = 7)]
            public int Capacity { get; set; }

            [JsonProperty(PropertyName = "Entrance Point - Enabled", Order = 12)]
            public bool IsEntranceTriggerActive { get; set; }

            [JsonProperty(PropertyName = "Entrance Point - Radius", Order = 13)]
            public float EntranceTriggerRadius { get; set; }

            [JsonProperty(PropertyName = "Entrance Point - Physically Enter - Enabled", Order = 14)]
            public bool AccessThroughLobbyOnly { get; set; }

            [JsonProperty(PropertyName = "Entrance Point - Text Visibility Range", Order = 15)]
            public float EntrancePointTextVisibilityRange { get; set; } = 100f;

            [JsonProperty(PropertyName = "Entrance Point - Location", Order = 18)]
            public Vector3 EntranceLocation { get; set; }

            [JsonProperty(PropertyName = "Whitelist Mode - Enabled", Order = 27)]
            public bool RestrictAccess { get; set; }

            [JsonProperty(PropertyName = "Whitelisted Players", Order = 28)]
            public HashSet<ulong> AllowedPlayers { get; set; } = new HashSet<ulong>();

            [JsonProperty(PropertyName = "Banned Players", Order = 29)]
            public HashSet<ulong> BannedPlayers { get; set; } = new HashSet<ulong>();

        }
        public abstract class BaseSpawnData
        {
            [JsonProperty(PropertyName = "Spawn - Enabled", Order = 3)]
            public bool IsEnabled { get; set; }

            [JsonProperty(PropertyName = "Spawn - Name", Order = 4)]
            public string Name { get; set; }

            [JsonProperty(PropertyName = "Spawn - Location", Order = 5)]
            public Vector3 SpawnPoint { get; set; }

            [JsonProperty(PropertyName = "Spawn - Radius", Order = 6)]
            public float Radius { get; set; }

            [JsonProperty(PropertyName = "Spawn - Chance", Order = 7)]
            public int SpawnChance { get; set; }
        }
        public class LobbySpawnData : BaseSpawnData
        {
            public override bool Equals(object obj)
            {
                var match = obj as LobbySpawnData;
                if (match == null)
                {
                    return false;
                }

                return match.Name.Equals(Name, StringComparison.OrdinalIgnoreCase);
            }
            public override int GetHashCode()
            {
                return Name.ToLower().GetHashCode();
            }
        }
        #endregion Data

        #region Behaviours

        public class ArenaBehaviour : MonoBehaviour
        {
            private bool _isInitialized;
            public Arena Arena { get; set; }
            float _updateTime = 0.5f;
            public static ArenaBehaviour CreateEntrance(string name, Vector3 entranceLocation, float triggerRadius, bool isTriggerActive)
            {
                var sphereObject = new GameObject(name);
                sphereObject.transform.position = entranceLocation;
                sphereObject.gameObject.layer = (int)Rust.Layer.Reserved2;
                if (isTriggerActive)
                {
                    var sphereCollider = sphereObject.AddComponent<SphereCollider>();
                    sphereCollider.radius = triggerRadius;
                    sphereCollider.isTrigger = true;
                }
                return sphereObject.AddComponent<ArenaBehaviour>();
            }
            public void Initialize(Arena arena)
            {
                Arena = arena;

                if (Arena == null)
                    return;

                InvokeHandler.InvokeRepeating(this, UpdateText, UnityEngine.Random.Range(0.1f, 2f), TextRefreshRate);

                if (!Arena.Data.IsEnabled)
                    return;

                _isInitialized = true;
            }

            public void Update()
            {
                if (!_isInitialized)
                {
                    return;
                }
                _updateTime -= UnityEngine.Time.deltaTime;
                if (_updateTime <= 0)
                {
                    _updateTime = 0.5f;

                    Arena?.TimerTick();
                }
            }

            public void DoDestroy()
            {
                Destroy(this);
            }

            private void OnDestroy()
            {
                InvokeHandler.CancelInvoke(this, UpdateText);
            }

            private void OnTriggerEnter(Collider collider)
            {
                if (!_isInitialized || !Arena.Data.IsEntranceTriggerActive || collider == null || collider.gameObject == null)
                    return;

                var player = collider.gameObject.ToBaseEntity()?.ToPlayer();
                if (player == null)
                    return;

                if (player.isMounted)
                    return;

                if (_instance.TeleportingPlayers.Contains(player.userID))
                    return;
                Interface.CallHook(nameof(IAdvancedLobby.HandleJoinArena), player, Arena.Data.Id);
            }

            private void OnTriggerExit(Collider collider)
            {
                if (!_isInitialized || !Arena.Data.IsEntranceTriggerActive || collider == null || collider.gameObject == null)
                    return;

                var player = collider.gameObject.ToBaseEntity()?.ToPlayer();
                if (player == null)
                    return;

                if (player.isMounted)
                    return;

                Ui.ClearAllMenus(player);
            }

            private void UpdateText()
            {
                var position = Vector3.up * 2 + Arena.Data.EntranceLocation;
                var formattedText = Arena.GetEntranceText();
                _instance.UpdateInGameText(formattedText, position, Arena.Data.EntrancePointTextVisibilityRange);
            }
        }
        public class ImageManagerBehaviour : MonoBehaviour
        {
            private readonly Dictionary<string, string> _images = new Dictionary<string, string>();

            public string GetImage(string key)
            {
                var imageId = string.Empty;
                if (_images.TryGetValue(key, out imageId))
                {
                    return imageId;
                }
                return null;
            }
            public IEnumerator DownloadImage(string key, string url)
            {
                var www = UnityWebRequest.Get(url);

                yield return www.SendWebRequest();
                if (www.isNetworkError || www.isHttpError)
                {
                    _instance.PrintError($"Image failed to download! Error: {www.error} - Image URL: {url}");
                    www.Dispose();
                    yield break;
                }

                var texture = new Texture2D(2, 2);
                texture.LoadImage(www.downloadHandler.data);
                if (texture != null)
                {
                    byte[] bytes = texture.EncodeToPNG();
                    DestroyImmediate(texture);
                    StoreByteArray(key, bytes);
                }
                www.Dispose();
            }

            private void StoreByteArray(string key, byte[] bytes)
            {
                if (bytes != null)
                {
                    _images[key] = FileStorage.server.Store(bytes, FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID).ToString();
                }
            }
        }
        #endregion Behaviours
        public static class Ui
        {
            public static float GetX(float amount, int width = 1920) => amount / width;
            public static float GetY(float amount, int height = 1080) => amount / height;
            public static float GetMinX(float left, int width = 1920) => left / width;
            public static float GetMaxX(float right, int width = 1920) => 1 - right / width;
            public static float GetMinY(float bottom, int height = 1080) => bottom / height;
            public static float GetMaxY(float top, int height = 1080) => 1 - top / height;
            public static string GetMin(float left, float bottom, int width = 1920, int height = 1080) => $"{GetMinX(left, width)} {GetMinY(bottom, height)}";
            public static string GetMax(float right, float top, int width = 1920, int height = 1080) => $"{GetMaxX(right, width)} {GetMaxY(top, height)}";
            public static string GetVerticalMin(float left, float bottom, int order, float amount, int width = 1920, int height = 1080) =>
                $"{GetMinX(left, width)} {GetMinY(bottom, height) - GetY(amount, height) * order}";
            public static string GetVerticalMax(float right, float top, int order, float amount, int width = 1920, int height = 1080) =>
                $"{GetMaxX(right, width)} {GetMaxY(top, height) - GetY(amount, height) * order}";
            public static string GetHorizontalMin(float left, float bottom, int order, float amount, int width = 1920, int height = 1080) =>
                $"{GetMinX(left, width) + GetX(amount, width) * order} {GetMinY(bottom, height)}";
            public static string GetHorizontalMax(float right, float top, int order, float amount, int width = 1920, int height = 1080) =>
                $"{GetMaxX(right, width) + GetX(amount, width) * order} {GetMaxY(top, height)}";
            public static string GetGridMin(float left, float bottom, int order, float xAmount, float yAmount, int columnCount, int rowCount,
                int width = 1920, int height = 1080)
            {
                if (columnCount > 0)
                {
                    return $"{GetMinX(left, width) + GetX(xAmount, width) * (order % columnCount)} {GetMinY(bottom, height) - GetY(yAmount, height) * (order / columnCount)}";
                }
                if (rowCount > 0)
                {
                    return $"{GetMinX(left, width) + GetX(xAmount, width) * (order % rowCount)} {GetMinY(bottom, height) - GetY(yAmount, height) * (order / rowCount)}";
                }
                return $"{GetMinX(left, width) + GetX(xAmount, width) * order} {GetMinY(bottom, height) - GetY(yAmount, height) * order}";
            }
            public static string GetGridMax(float right, float top, int order, float xAmount, float yAmount, int columnCount, int rowCount,
                int width = 1920, int height = 1080)
            {
                if (columnCount > 0)
                {
                    return $"{GetMaxX(right, width) + GetX(xAmount, width) * (order % columnCount)} {GetMaxY(top, height) - GetY(yAmount, height) * (order / columnCount)}";
                }
                if (rowCount > 0)
                {
                    return $"{GetMaxX(right, width) + GetX(xAmount, width) * (order % rowCount)} {GetMaxY(top, height) - GetY(yAmount, height) * (order / rowCount)}";
                }
                return $"{GetMaxX(right, width) + GetX(xAmount, width) * order} {GetMaxY(top, height) - GetY(yAmount, height) * order}";
            }

            public static CuiElementContainer Container(Panels panel, string color, string min, string max, bool useBlur = true, bool useCursor = true, Panels parent = Panels.Overlay)
            {
                CuiElementContainer container = new CuiElementContainer()
                {
                    {
                        new CuiPanel
                        {
                            Image = {Color = color, Material = useBlur ? "assets/content/ui/uibackgroundblur.mat" : "Assets/Icons/IconMaterial.mat"},
                            RectTransform = {AnchorMin = min, AnchorMax = max},
                            CursorEnabled = useCursor
                        },
                        new CuiElement().Parent = parent == Panels.HudMenu ? "Hud.Menu" : parent.ToString(),
                        panel.ToString()
                    }
                };
                return container;
            }
            public static void Panel(ref CuiElementContainer container, Panels panel, string color, string min, string max, bool cursor = false)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = color },
                    RectTransform = { AnchorMin = min, AnchorMax = max },
                    CursorEnabled = cursor
                },
                panel.ToString());
            }

            public static void Panel(ref CuiElementContainer container, Panels panel, string color, string text, string min, string max,
                int size, string textMin = null, string textMax = null, string font = "RobotoCondensed-Regular.ttf", string textColor = "1.0 1.0 1.0 1.0",
                TextAnchor align = TextAnchor.MiddleCenter, bool cursor = false)
            {
                var name = CuiHelper.GetGuid();
                var cuiElement = new CuiElement
                {
                    Name = name,
                    Parent = panel.ToString(),
                    FadeOut = 0
                };
                cuiElement.Components.Add(new CuiImageComponent { Color = color });
                cuiElement.Components.Add(new CuiRectTransformComponent { AnchorMin = min, AnchorMax = max });
                if (cursor)
                    cuiElement.Components.Add(new CuiNeedsCursorComponent());
                container.Add(cuiElement);
                var textName = CuiHelper.GetGuid();
                container.Add(new CuiElement
                {
                    Name = textName,
                    Parent = panel.ToString(),
                    FadeOut = 0,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Color = textColor,
                            FontSize = (int)(size * 0.7f),
                            Align = align,
                            Text = text,
                            Font = font
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = textMin ?? min,
                            AnchorMax = textMax ?? max
                        }
                    }
                });
            }
            public static void Label(ref CuiElementContainer container, Panels panel, string text, int size, string min, string max, bool isBold = true, string color = "1.0 1.0 1.0 1.0", TextAnchor align = TextAnchor.MiddleCenter, float fadeIn = 0)
            {
                container.Add(new CuiLabel
                {
                    Text =
                    {
                        FadeIn = fadeIn,
                        Color = color,
                        FontSize = (int)(size * 0.7f),
                        Align = align,
                        Text = text,
                        Font = isBold ? "RobotoCondensed-Bold.ttf" : "RobotoCondensed-Regular.ttf"
                    },
                    RectTransform = { AnchorMin = min, AnchorMax = max }
                },
                panel.ToString());
            }
            public static void OutlineLabel(ref CuiElementContainer container, Panels panel, string text, int size, string min, string max,
                string font = Fonts.RobotoCondensedRegular, string color = "1.0 1.0 1.0 1.0", TextAnchor align = TextAnchor.MiddleCenter, float fadeIn = 0,
                string outlineDistance = "1 1", string outlineColor = "0 0 0 1.0")
            {
                CuiElement textElement = new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = panel.ToString(),
                    FadeOut = fadeIn,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = text,
                            FontSize = (int)(size * 0.7f),
                            Align = align,
                            FadeIn = fadeIn,
                            Font = font,
                            Color = color
                        },
                        new CuiOutlineComponent
                        {
                            Distance = outlineDistance,
                            Color = outlineColor
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = min,
                            AnchorMax = max
                        }
                    }
                };
                container.Add(textElement);
            }
            public static void Label(ref CuiElementContainer container, Panels panel, string text, int size, string min, string max, string font = "RobotoCondensed-Regular.ttf", string color = "1.0 1.0 1.0 1.0", TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiLabel
                {
                    Text =
                    {
                        Color = color,
                        FontSize = (int)(size * 0.7f),
                        Align = align,
                        Text = text,
                        Font = font
                    },
                    RectTransform = { AnchorMin = min, AnchorMax = max }
                },
                panel.ToString());
            }
            public static void Input(ref CuiElementContainer container, Panels panel, string text, int size, string min, string max, string command = null,
                string font = "RobotoCondensed-Regular.ttf", string color = "1.0 1.0 1.0 1.0", int charsLimit = 100, bool isPassword = false, TextAnchor align = TextAnchor.MiddleLeft)
            {
                var name = CuiHelper.GetGuid();
                container.Add(new CuiElement
                {
                    Name = name,
                    Parent = panel.ToString(),
                    FadeOut = 0,
                    Components = {
                        new CuiInputFieldComponent
                        {
                            Color = color,
                            FontSize = (int)(size * 0.7f),
                            Align = align,
                            Text = text,
                            Font = font,
                            CharsLimit = charsLimit,
                            Command = command,
                            IsPassword = isPassword
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = min,
                            AnchorMax = max
                        }
                    }
                });
            }
            public static void Button(ref CuiElementContainer container, Panels panel, string color, string text, int size, string min, string max, string command, bool isBold = true, string textColor = "1.0 1.0 1.0 1.0", TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiButton
                {
                    Button = { Color = color, Command = command, FadeIn = 0f },
                    RectTransform = { AnchorMin = min, AnchorMax = max },
                    Text =
                    {
                        Color = textColor,
                        Text = text,
                        FontSize = (int)(size * 0.7f),
                        Font = isBold ? "RobotoCondensed-Bold.ttf" : "RobotoCondensed-Regular.ttf",
                        Align = align
                    }
                },
                panel.ToString());
            }
            public static void Button(ref CuiElementContainer container, Panels panel, string color, string text, int size, string min, string max, string command, string textMin = null, string textMax = null, bool isBold = true, string textColor = "1.0 1.0 1.0 1.0", TextAnchor align = TextAnchor.MiddleCenter)
            {
                var parent = container.Add(new CuiButton
                {
                    Button = { Color = color, Command = command, FadeIn = 0f },
                    RectTransform = { AnchorMin = min, AnchorMax = max },
                    Text =
                    {
                        Color = textColor,
                        Text = null,
                        FontSize = (int)(size * 0.7f),
                        Font = isBold ? "RobotoCondensed-Bold.ttf" : "RobotoCondensed-Regular.ttf",
                        Align = align
                    }
                },
                panel.ToString());

                var textName = CuiHelper.GetGuid();
                container.Add(new CuiElement
                {
                    Name = textName,
                    Parent = parent,
                    FadeOut = 0,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Color = textColor,
                            FontSize = (int)(size * 0.7f),
                            Align = align,
                            Text = text,
                            Font = isBold ? "RobotoCondensed-Bold.ttf" : "RobotoCondensed-Regular.ttf",
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = textMin ?? min,
                            AnchorMax = textMax ?? max
                        }
                    }
                });
            }
            public static void ImageButton(ref CuiElementContainer container, Panels panel, string color, string image, string min, string max, string command, string imageMin = null, string imageMax = null, bool isUrl = false)
            {
                var name = CuiHelper.GetGuid();
                container.Add(new CuiElement
                {
                    Name = name,
                    Parent = panel.ToString(),
                    FadeOut = 0,
                    Components =
                    {
                        new CuiButtonComponent
                        {
                            Color = color,
                            Command = command,
                            FadeIn = 0f
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = min,
                            AnchorMax = max
                        }
                    }
                });
                if (!string.IsNullOrWhiteSpace(image))
                {
                    var rawImage = new CuiRawImageComponent
                    {
                        Color = "1.0 1.0 1.0 1.0",
                        FadeIn = 0f
                    };
                    if (isUrl)
                    {
                        rawImage.Url = image;
                    }
                    else
                    {
                        rawImage.Png = image;
                    }
                    container.Add(new CuiElement
                    {
                        Parent = name,
                        FadeOut = 0,
                        Components =
                        {
                            rawImage,
                            new CuiRectTransformComponent
                            {
                                AnchorMin = string.IsNullOrWhiteSpace(imageMin) ? "0.0 0.0" : imageMin,
                                AnchorMax = string.IsNullOrWhiteSpace(imageMax) ? "1.0 1.0" : imageMax
                            }
                        }
                    });
                }
            }
            public static void Image(ref CuiElementContainer container, Panels panel, string image, string min, string max, bool isUrl = false, string color = "1 1 1 1")
            {
                var rawImage = new CuiRawImageComponent
                {
                    Color = color,
                    FadeIn = 0f
                };
                if (isUrl)
                {
                    rawImage.Url = image;
                }
                else
                {
                    rawImage.Png = image;
                }
                container.Add(new CuiElement
                {
                    Components =
                    {
                        rawImage,
                        new CuiRectTransformComponent{ AnchorMin = min, AnchorMax = max },
                    },
                    FadeOut = 0f,
                    Parent = panel.ToString()
                });
            }
            public static string Color(string hexColor, float alpha = 1)
            {
                if (hexColor.StartsWith("#"))
                    hexColor = hexColor.Substring(1);
                var red = int.Parse(hexColor.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                var green = int.Parse(hexColor.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                var blue = int.Parse(hexColor.Substring(4, 2), NumberStyles.AllowHexSpecifier);
                return $"{(double)red / 255} {(double)green / 255} {(double)blue / 255} {alpha}";
            }
            public class ColorCode
            {
                public const string Black = "#000000", White = "#FFFFFF";
                public const string Green = "#018447", Red = "#B1231E", Blue = "#3C7DB3", ButtonBlack = "#141414", EerieBlack = "#282726",
                    Gray = "#424242", ShadowBlack = "#0F0F14", TextGray = "#B3B3B3", DarkBlue = "#0A325A", QueenBlue = "#33689A",
                    MayaBlue = "#81C7F8", Gainsboro = "#DCDCDC", Xanthic = "#EEEE04";
            }
            public static void ClearAllMenus(BasePlayer player)
            {
                _instance.UnfreezePlayer(player.userID);
                foreach (var name in Enum.GetNames(typeof(Panels)))
                {
                    CuiHelper.DestroyUi(player, name);
                }
            }
            public enum Panels
            {
                Overall,
                Overlay,
                Hud,
                HudMenu,
                Under,
                Fullscreen,
                TeamDeathmatchJoin,
                ArenaList,
                PauseMenu,
                LoadoutMenu,
                SwitchTeamMenu,
                LeaderboardMenu,
                ControlMenu,
                QuickMenu,
                InGameInfo,
                MatchPlayers,
                Recoil,
                RecoilImage
            }
            public enum MenuTab
            {
                PauseMenu,
                Loadout,
                SwitchTeam,
                Leaderboard,
                Control
            }
            public enum ControlMenuTab
            {
                General,
                Players,
                Kits,
                Privacy
            }

            public class Fonts
            {
                public const string DroidSansMono = "DroidSansMono.ttf";
                public const string PermanentMarker = "PermanentMarker.ttf";
                public const string RobotoCondensedBold = "RobotoCondensed-Bold.ttf";
                public const string RobotoCondensedRegular = "RobotoCondensed-Regular.ttf";
            }
        }
        public class Lobby
        {
            public LobbyData Data { get; set; } = new LobbyData();
            public Zone Zone { get; set; } = new Zone();

            public HashSet<LobbyPlayer> ActivePlayers { get; set; } = new HashSet<LobbyPlayer>();

            public Vector3 GetRandomSpawnLocation()
            {
                var validSpawnPoints = Data.SpawnLocations.Where(x => x.IsEnabled).ToList();
                if (!validSpawnPoints.Any())
                {
                    return Zone.Location;
                }

                var tryCount = 0;
                var randomChance = UnityEngine.Random.Range(0.1f, 100);
                var spawnPoint = validSpawnPoints.Where(x => x.SpawnChance >= randomChance)
                    .ToList().GetRandom();
                Vector2? randomPos = null;
                if (spawnPoint.Radius > 0)
                {
                    while (tryCount < 20)
                    {
                        randomPos = UnityEngine.Random.insideUnitCircle * spawnPoint.Radius;
                        var entities = Facepunch.Pool.GetList<BaseEntity>();
                        Vis.Entities(randomPos.Value, 10, entities, LayerMask.GetMask("Construction", "Deployable"));
                        var count = entities.Count;
                        Facepunch.Pool.FreeList(ref entities);
                        if (count == 0)
                        {
                            break;
                        }
                        randomPos = null;
                        tryCount++;
                    }
                }
                else
                {
                    randomPos = Vector2.zero;
                }

                return randomPos.HasValue
                    ? new Vector3(spawnPoint.SpawnPoint.x + randomPos.Value.x, spawnPoint.SpawnPoint.y, spawnPoint.SpawnPoint.z + randomPos.Value.y)
                    : Vector3.zero;
            }
        }

        public class LobbyPlayer
        {
            private readonly BasePlayer _player;

            public BasePlayer Player => _player;

            public LobbyPlayer(BasePlayer basePlayer)
            {
                _player = basePlayer;
            }
            public override bool Equals(object obj)
            {
                var lobbyPlayer = obj as LobbyPlayer;
                if (lobbyPlayer == null)
                {
                    return false;
                }

                return lobbyPlayer.Player.userID == Player.userID;
            }
            public override int GetHashCode()
            {
                return _player.userID.GetHashCode();
            }
        }
        public abstract class Arena
        {
            protected Arena(Lobby lobby, ArenaData arenaData)
            {
                Lobby = lobby;
                Data = arenaData;
            }
            public Lobby Lobby { get; private set; }
            public virtual ArenaData Data { get; set; }
            public Zone Zone { get; set; } = new Zone();
            public virtual Match Match { get; set; }
            public ArenaBehaviour Behaviour { get; set; }

            public virtual string GetEntranceText()
            {
                return string.Empty;
            }
            public virtual void TimerTick()
            {

            }
        }
        public abstract class Match
        {
            protected Match(Arena arena)
            {
                _id = Guid.NewGuid();
                Arena = arena;
            }

            private readonly Guid _id;
            public Guid Id => _id;
            public virtual Arena Arena { get; private set; }

            public bool IsCountDownActivated { get; set; }
            public bool IsCountDownEnabled { get; set; } = true;
            public bool IsStarted { get; set; }
            public bool WillAutoStart { get; set; } = false;
            public virtual HashSet<MatchPlayer> Players { get; set; } = new HashSet<MatchPlayer>();
            public bool AllowIndividualKit { get; set; }
            public bool RestrictAccess { get; set; }
            public HashSet<SelectablePlayer> AllowedPlayers { get; set; } = new HashSet<SelectablePlayer>();
            public HashSet<SelectablePlayer> BannedPlayers { get; set; } = new HashSet<SelectablePlayer>();
            public override bool Equals(object obj)
            {
                var match = obj as Match;
                if (match == null)
                {
                    return false;
                }

                return match.Id == Id;
            }
            public override int GetHashCode()
            {
                return _id.GetHashCode();
            }
        }
        public class MatchPlayer
        {
            public MatchPlayer(BasePlayer player, Match match)
            {
                Player = player;
                _playerId = player.userID;
                PlayerName = player.displayName;
                Match = match;
                Statistics = new PlayerStatistics(player);
                AllowPlaying = true;
            }
            private readonly ulong _playerId;
            public ulong PlayerId => _playerId;
            public string PlayerName { get; set; }
            public BasePlayer Player { get; private set; }
            public virtual Match Match { get; private set; }
            public bool IsMatchLeader { get; set; }
            public bool AllowPlaying { get; set; }
            public PlayerStatistics Statistics { get; set; }
            public string BlueTeamSelectedLoadoutName { get; set; } = string.Empty;
            public string RedTeamSelectedLoadoutName { get; set; } = string.Empty;
            public HashSet<string> IndividualItems { get; set; } = new HashSet<string>();

            public void Reset()
            {
                AllowPlaying = true;
                ResetMetabolism();
            }

            public void ResetMetabolism()
            {
                _instance.ResetPlayer(Player);
            }
            public void ResetStats()
            {
                Statistics.Reset();
            }

            public void ClearInventory()
            {
                _instance.InventoryStrip(Player);
            }

            public override bool Equals(object obj)
            {
                var matchPlayer = obj as MatchPlayer;
                if (matchPlayer == null)
                {
                    return false;
                }

                return matchPlayer.PlayerId == PlayerId;
            }
            public override int GetHashCode()
            {
                return _playerId.GetHashCode();
            }
        }
        public class PlayerStatistics
        {
            public PlayerStatistics(BasePlayer player)
            {
                Player = player;
            }
            public BasePlayer Player { get; }
            public int Kills { get; set; }
            public int Deaths { get; set; }
            public float DamagesDone { get; set; }
            public int Score => Kills - Deaths;

            public virtual void Reset()
            {
                Kills = 0;
                Deaths = 0;
                DamagesDone = 0;
            }
        }
        public enum MatchType
        {
            Scrim = 10,
            TrainingGround = 20
        }
        public enum ListType
        {
            Leaderboard = 0,
            Match = 10,
            Search = 20,
            Ban = 30,
            Allow = 40
        }
        public class Zone
        {
            public string Name { get; set; }
            public float Radius { get; set; }
            public float Radiation { get; set; }
            public float Comfort { get; set; }
            public float Temperature { get; set; }
            public Vector3 Location { get; set; }
            public Vector3 Size { get; set; }
            public Vector3 Rotation { get; set; }
            public string Id { get; set; }
            public string EnterMessage { get; set; }
            public string LeaveMessage { get; set; }
            public string Permission { get; set; }
            public string EjectSpawns { get; set; }
        }
        public class PlayerRank : PlayerStatistics
        {
            public PlayerRank(BasePlayer player) : base(player)
            {
            }

            public int Rank { get; set; }
        }
        public class SelectablePlayer
        {
            public SelectablePlayer(ulong playerId, string playerName)
            {
                _playerId = playerId;
                PlayerName = playerName;
            }

            private readonly ulong _playerId;
            public ulong PlayerId => _playerId;
            public string PlayerName { get; set; }
            public bool IsSelected { get; set; }

            public override bool Equals(object obj)
            {
                var player = obj as SelectablePlayer;
                if (player == null)
                {
                    return false;
                }

                return player.PlayerId == PlayerId;
            }
            public override int GetHashCode()
            {
                return _playerId.GetHashCode();
            }
        }
        public class PlayerPageIndex
        {
            public int LeaderboardIndex { get; set; }
            public int SearchIndex { get; set; }
            public int MatchIndex { get; set; }
            public int BanIndex { get; set; }
            public int AllowIndex { get; set; }
        }
        public class ArenaDetail
        {
            public int ArenaId { get; set; }
            public string Name { get; set; }
            public MatchType MatchType { get; set; }
            public int BlueTeam { get; set; }
            public int RedTeam { get; set; }
            public int Capacity { get; set; }
            public bool IsVip { get; set; }
            public bool IsRestricted { get; set; }
        }
        public class TextTagHelper
        {
            const string TextColorRemovalPattern = @"(?<=(<color=.*>))(.*)(?=(<\/color>))";
            const string TextSizeRemovalPattern = @"(?<=(<size=\d*>))(.*)(?=(<\/size>))";
            public string Result { get; set; }
            public TextTagHelper RemoveColorTag(string text = null)
            {
                if (string.IsNullOrEmpty(text))
                    text = Result;
                else
                    Result = "";
                var match = Regex.Match(text, TextColorRemovalPattern);
                if (match.Success)
                    Result = match.Value.ToUpper();
                else
                    Result = text.ToUpper();
                return this;
            }
            public TextTagHelper RemoveSizeTag(string text = null)
            {
                if (string.IsNullOrEmpty(text))
                    text = Result;
                else
                    Result = "";
                var match = Regex.Match(text, TextSizeRemovalPattern);
                if (match.Success)
                    Result = match.Value.ToUpper();
                else
                    Result = text.ToUpper();
                return this;
            }
        }
        private static class Messages
        {
            public const string NoPermission = "No Permission";
            public const string WrongCommand = "Wrong Command";
            public const string LobbySetValueHelp = "LobbySetValueHelp";
            public const string LobbyCommandsList = "Lobby Commands List";
            public const string InvalidPlayerId = "Invalid Player Id";
            public const string PlayerNotFound = "Player Not Found";
            public const string LobbyNotFound = "Lobby Not Found";
            public const string ArenaNotFound = "Arena Not Found";
            public const string ChangesSaved = "Changes Saved";
            public const string NoEditingLobby = "No Editing Lobby";
            public const string LobbyParameterChanged = "Lobby Parameter Changed";
            public const string LobbySpawnPointAdded = "Lobby Spawn Point Added";
            public const string LobbyInvalidPosition = "Lobby Invalid Position";
            public const string LobbyDeleted = "Lobby Deleted";
            public const string LobbyEditingStarted = "Lobby Editing Started";
            public const string LobbyEditingDone = "Lobby Editing Done";
            public const string LobbyNoAccess = "Lobby No Access";
            public const string MainLobbyNotMain = "Change Main Lobby To Not Main";
            public const string LobbyZoneNotFound = "Lobby Zone Not Found";
            public const string KitNotFound = "Kit Not Found";
            public const string LobbyCreated = "Lobby Created";
            public const string SpawnPointNotFound = "Spawn Point Not Found";
            public const string ServerLeaveMessage = "Server Leave Message";
        }
        public static class ConsoleCommands
        {
            public const string LobbyHelp = "lobby.help";
            public const string LobbyEdit = "lobby.edit";
            public const string LobbyDone = "lobby.done";
            public const string LobbyDelete = "lobby.delete";
            public const string LobbyCreate = "lobby.create";
            public const string LobbySet = "lobby.set";
            public const string LobbySetHelp = "lobby.set.help";
            public const string LobbyDeleteSpawn = "lobby.delete.spawn";
            public const string LobbyShow = "lobby.show";
            public const string LobbyAllow = "lobby.allow";
            public const string LobbyBan = "lobby.ban";

            public const string LobbyJoin = "lobby.join";
            public const string LobbyMenuClear = "lobby.menu.clear";
            public const string LobbyArenaListShow = "lobby.arena.list.show";
            public const string LobbyLeaderboardShow = "lobby.leaderboard.show";
            public const string ArenaTryJoin = "arena.try.join";
            public const string ServerLeave = "server.leave";

            public const string NextPage = "page.next";
            public const string PreviousPage = "page.prev";

            public const string ArenaGeneralHelp = "arena.help";
        }

        #endregion Classes

        #region Methods

        #region Commands

        private void LeaveArenaChatCommand(BasePlayer player, string command, string[] args)
        {
            if (player == null)
            {
                return;
            }

            Interface.Call(nameof(IAdvancedLobby.HandleLeaveArena), player);
        }

        private void ShowLobbyPauseMenuCommand(BasePlayer player, string command, string[] args)
        {
            if (player == null)
            {
                return;
            }

            if (Interface.Call(nameof(IAdvancedLobby.HandleShowPauseMenu), player) == null)
                ShowLobbyMenu(player);
        }

        #region LobbyDataCommands

        //lobby.help
        [ConsoleCommand(ConsoleCommands.LobbyHelp)]
        void LobbyHelp(ConsoleSystem.Arg conArgs)
        {
            var player = conArgs?.Player();
            if (player != null && !permission.UserHasPermission(player.UserIDString, PermissionAdmin))
            {
                ShowMessage(player, Messages.NoPermission, this);
                return;
            }
            ShowMessage(player, Messages.LobbyCommandsList, this, GetLobbyCommands());
        }

        //arena.help
        [ConsoleCommand(ConsoleCommands.ArenaGeneralHelp)]
        void ArenaGeneralHelp(ConsoleSystem.Arg conArgs)
        {
            var player = conArgs?.Player();
            if (player != null && !permission.UserHasPermission(player.UserIDString, PermissionAdmin))
            {
                ShowMessage(player, Messages.NoPermission, this);
                return;
            }
            Interface.Call(nameof(IAdvancedLobby.HandleArenaHelp), player);
        }

        //lobby.edit <id>
        [ConsoleCommand(ConsoleCommands.LobbyEdit)]
        void EditLobby(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                ShowMessage(null, Messages.WrongCommand, this, GetLobbyCommands());
                return;
            }

            var player = conArgs.Player();
            if (player != null && !permission.UserHasPermission(player.UserIDString, PermissionAdmin))
            {
                ShowMessage(player, Messages.NoPermission, this);
                return;
            }

            var id = conArgs.GetInt(0, -1);
            if (id >= 0)
            {
                LobbyToEdit = _pluginData.Lobbies.FirstOrDefault(x => x.Id == id);
                if (LobbyToEdit == null)
                {
                    ShowMessage(player, Messages.LobbyNotFound, this);
                    return;
                }
                UnLockEntities(LobbyToEdit.ZoneId, player.userID);
                ShowMessage(player, Messages.LobbyEditingStarted, this, LobbyToEdit.Name, LobbyToEdit.Id);
            }
            else
            {
                ShowMessage(player, Messages.WrongCommand, this, GetLobbyCommands());
            }
        }

        //lobby.done
        [ConsoleCommand(ConsoleCommands.LobbyDone)]
        void DoneLobby(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                ShowMessage(null, Messages.WrongCommand, this, GetLobbyCommands());
                return;
            }

            var player = conArgs.Player();
            if (player != null && !permission.UserHasPermission(player.UserIDString, PermissionAdmin))
            {
                ShowMessage(player, Messages.NoPermission, this);
                return;
            }

            if (LobbyToEdit != null)
            {
                LockEntities(LobbyToEdit.ZoneId);
                var name = LobbyToEdit.Name;
                var id = LobbyToEdit.Id;
                LobbyToEdit = null;
                ShowMessage(player, Messages.LobbyEditingDone, this, name, id);
            }
        }

        //lobby.delete <id>
        [ConsoleCommand(ConsoleCommands.LobbyDelete)]
        void DeleteLobby(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                ShowMessage(null, Messages.WrongCommand, this, GetLobbyCommands());
                return;
            }

            var player = conArgs.Player();
            if (player != null && !permission.UserHasPermission(player.UserIDString, PermissionAdmin))
            {
                ShowMessage(player, Messages.NoPermission, this);
                return;
            }

            var id = conArgs.GetInt(0, -1);
            if (id >= 0)
            {
                var removedCount = _pluginData.Lobbies.RemoveWhere(x => x.Id == id);
                if (removedCount > 0)
                {
                    SaveData();
                    RemoveLobby(id);
                    ShowMessage(player, Messages.LobbyDeleted, this);
                }
                else
                {
                    ShowMessage(player, Messages.LobbyNotFound, this);
                }
            }
            else
            {
                ShowMessage(player, Messages.WrongCommand, this, GetLobbyCommands());
            }
        }

        //lobby.create <Name> <ZoneId>
        [ConsoleCommand(ConsoleCommands.LobbyCreate)]
        void CreateLobby(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                ShowMessage(null, Messages.WrongCommand, this, GetLobbyCommands());
                return;
            }

            var player = conArgs.Player();
            if (player != null && !permission.UserHasPermission(player.UserIDString, PermissionAdmin))
            {
                ShowMessage(player, Messages.NoPermission, this);
                return;
            }

            if (conArgs.Args.Length < 2)
            {
                ShowMessage(player, Messages.WrongCommand, this, GetLobbyCommands());
                return;
            }

            var name = conArgs.GetString(0);
            var zoneId = conArgs.GetString(1);
            var zone = GetZone(zoneId);
            if (zone == null)
            {
                ShowMessage(player, Messages.LobbyZoneNotFound, this);
                return;
            }
            var id = 1001;
            if (_pluginData.Lobbies.Any())
            {
                id = _pluginData.Lobbies.Max(x => x.Id) + 1;
            }
            var newLobby = new LobbyData
            {
                Id = id,
                Name = name,
                ZoneId = zoneId,
                IsMain = !_pluginData.Lobbies.Any(),
                LoadoutName = string.Empty,
                RestrictAccess = false,
                SpawnLocations = new HashSet<LobbySpawnData>()
            };
            _pluginData.Lobbies.Add(newLobby);
            LobbyToEdit = newLobby;
            SaveData();
            var lobby = new Lobby
            {
                Data = newLobby,
                Zone = zone,
                ActivePlayers = new HashSet<LobbyPlayer>(GetZonePlayers(newLobby.ZoneId))
            };
            Lobbies.Add(newLobby.Id, lobby);
            ShowMessage(player, Messages.LobbyCreated, this);
        }

        //lobby.set <key> <value> [<key> <value>] [<key> <value>] ...
        [ConsoleCommand(ConsoleCommands.LobbySet)]
        void SetLobbyValue(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                ShowMessage(null, Messages.WrongCommand, this, GetLobbyCommands());
                return;
            }

            var player = conArgs.Player();
            if (player != null && !permission.UserHasPermission(player.UserIDString, PermissionAdmin))
            {
                ShowMessage(player, Messages.NoPermission, this);
                return;
            }

            if (conArgs.Args.Length <= 1)
            {
                ShowMessage(player, Messages.WrongCommand, this, GetLobbyCommands());
                return;
            }

            if (LobbyToEdit == null)
            {
                ShowMessage(player, Messages.NoEditingLobby, this, ConsoleCommands.LobbyEdit);
                return;
            }

            var length = conArgs.Args.Length / 2;
            var index = 0;
            Zone zone = null;
            for (var i = 0; i < length; i++)
            {
                var key = conArgs.GetString(index).ToLower();
                index++;
                switch (key)
                {
                    case "name":
                        {
                            LobbyToEdit.Name = conArgs.GetString(index);
                            ShowMessage(player, Messages.LobbyParameterChanged, this, nameof(LobbyData.Name));
                            break;
                        }
                    case "zone":
                    case "zoneid":
                        {
                            var zoneId = conArgs.GetString(index);
                            zone = GetZone(zoneId);
                            if (zone == null)
                            {
                                ShowMessage(player, Messages.LobbyZoneNotFound, this);
                            }
                            else
                            {
                                LobbyToEdit.ZoneId = zoneId;
                                ShowMessage(player, Messages.LobbyParameterChanged, this, nameof(LobbyData.ZoneId));
                            }
                            break;
                        }
                    case "main":
                    case "ismain":
                        {
                            var isMain = conArgs.GetBool(index);
                            if (LobbyToEdit.IsMain && !isMain)
                            {
                                ShowMessage(player, Messages.MainLobbyNotMain, this);
                                return;
                            }
                            foreach (var lobby in Lobbies.Values)
                            {
                                lobby.Data.IsMain = false;
                            }
                            foreach (var lobby in _pluginData.Lobbies)
                            {
                                lobby.IsMain = false;
                            }

                            LobbyToEdit.IsMain = isMain;
                            ShowMessage(player, Messages.LobbyParameterChanged, this, nameof(LobbyData.IsMain));
                            break;
                        }
                    case "restrict":
                    case "restrictaccess":
                        {
                            LobbyToEdit.RestrictAccess = conArgs.GetBool(index);
                            ShowMessage(player, Messages.LobbyParameterChanged, this, nameof(LobbyData.RestrictAccess));
                            break;
                        }
                    case "kit":
                    case "loadout":
                    case "loadoutname":
                        {
                            var loadoutName = conArgs.GetString(index);
                            if (InventoryLoadout != null && !InventoryLoadout.Call<bool>("IsValidLoadout", loadoutName))
                            {
                                ShowMessage(player, Messages.KitNotFound, this);
                            }
                            else
                            {
                                LobbyToEdit.LoadoutName = loadoutName;
                                ShowMessage(player, Messages.LobbyParameterChanged, this, nameof(LobbyData.LoadoutName));
                            }
                            break;
                        }
                    case "spawn":
                    case "spawnlocation":
                        {
                            var number = 1;
                            var lastItem = LobbyToEdit.SpawnLocations.OrderBy(x => x.Name).LastOrDefault();
                            if (lastItem != null)
                            {
                                var numberString = lastItem.Name.Split('_').LastOrDefault();
                                if (!string.IsNullOrWhiteSpace(numberString))
                                {
                                    if (int.TryParse(numberString, out number))
                                    {
                                        number++;
                                    }
                                    else
                                    {
                                        number = 1;
                                    }
                                }
                            }
                            var pos = conArgs.GetVector3(index, Vector3.zero);
                            if (pos.Equals(Vector3.zero))
                            {
                                if (conArgs.GetString(index).ToLower().Equals("here") && player != null)
                                {
                                    pos = player.ServerPosition;
                                    var lobbyZone = GetZone(LobbyToEdit.ZoneId);
                                    if (lobbyZone == null)
                                    {
                                        ShowMessage(player, Messages.LobbyZoneNotFound, this);
                                        break;
                                    }
                                    if (Vector3.Distance(pos, lobbyZone.Location) >= lobbyZone.Radius)
                                    {
                                        ShowMessage(player, Messages.LobbyInvalidPosition, this, pos);
                                        break;
                                    }
                                    LobbyToEdit.SpawnLocations.Add(new LobbySpawnData
                                    {
                                        IsEnabled = true,
                                        Name = $"SP_{number:000}",
                                        Radius = 0,
                                        SpawnPoint = pos,
                                        SpawnChance = 100
                                    });
                                    ShowMessage(player, Messages.LobbySpawnPointAdded, this, pos);
                                }
                                else
                                {
                                    ShowMessage(player, Messages.WrongCommand, this, GetLobbyCommands());
                                }
                            }
                            else
                            {
                                var lobbyZone = GetZone(LobbyToEdit.ZoneId);
                                if (lobbyZone == null)
                                {
                                    ShowMessage(player, Messages.LobbyZoneNotFound, this);
                                    break;
                                }
                                if (Vector3.Distance(pos, lobbyZone.Location) >= lobbyZone.Radius)
                                {
                                    ShowMessage(player, Messages.LobbyInvalidPosition, this, pos);
                                    break;
                                }
                                LobbyToEdit.SpawnLocations.Add(new LobbySpawnData
                                {
                                    IsEnabled = true,
                                    Name = $"SP_{number:000}",
                                    Radius = 0,
                                    SpawnPoint = pos,
                                    SpawnChance = 100
                                });
                                ShowMessage(player, Messages.LobbySpawnPointAdded, this, pos);
                            }
                            break;
                        }
                    default:
                        break;
                }
                index++;

            }
            SaveData();
            Lobby targetLobby;
            if (Lobbies.TryGetValue(LobbyToEdit.Id, out targetLobby) && targetLobby != null)
            {
                targetLobby.Data = LobbyToEdit;
                if (zone != null)
                    targetLobby.Zone = zone;
                targetLobby.ActivePlayers = new HashSet<LobbyPlayer>(GetZonePlayers(LobbyToEdit.ZoneId));
            }
            ShowMessage(player, Messages.ChangesSaved, this);
        }

        //lobby.set.help
        [ConsoleCommand(ConsoleCommands.LobbySetHelp)]
        void SetLobbyValueHelp(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                ShowMessage(null, Messages.WrongCommand, this, GetLobbyCommands());
                return;
            }

            var player = conArgs.Player();
            if (player != null && !permission.UserHasPermission(player.UserIDString, PermissionAdmin))
            {
                ShowMessage(player, Messages.NoPermission, this);
                return;
            }
            ShowMessage(player, Messages.LobbySetValueHelp, this, GetLobbyEditingKeys());
        }

        //lobby.delete.spawn <name>
        [ConsoleCommand(ConsoleCommands.LobbyDeleteSpawn)]
        void DeleteLobbySpawnPoint(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                ShowMessage(null, Messages.WrongCommand, this, GetLobbyCommands());
                return;
            }

            var player = conArgs.Player();
            if (player != null && !permission.UserHasPermission(player.UserIDString, PermissionAdmin))
            {
                ShowMessage(player, Messages.NoPermission, this);
                return;
            }

            if (conArgs.Args.Length <= 0)
            {
                ShowMessage(player, Messages.WrongCommand, this, GetLobbyCommands());
                return;
            }

            if (LobbyToEdit == null)
            {
                ShowMessage(player, Messages.NoEditingLobby, this, ConsoleCommands.LobbyEdit);
                return;
            }

            Lobby lobby;
            if (!Lobbies.TryGetValue(LobbyToEdit.Id, out lobby) || lobby == null)
            {
                ShowMessage(player, Messages.LobbyNotFound, this);
                return;
            }

            var spawnPointName = conArgs.GetString(0).ToLower();
            var deleted = LobbyToEdit.SpawnLocations.RemoveWhere(x => x.Name.ToLower().Equals(spawnPointName));
            if (deleted == 0)
            {
                ShowMessage(player, Messages.SpawnPointNotFound, this);
                return;
            }

            lobby.Data = LobbyToEdit;
            SaveData();
            ShowMessage(player, Messages.ChangesSaved, this);
        }

        //lobby.ban <add|remove> <lobbyId> <playerId>
        [ConsoleCommand(ConsoleCommands.LobbyBan)]
        void LobbyBan(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                ShowMessage(null, Messages.WrongCommand, this, GetLobbyCommands());
                return;
            }

            var player = conArgs.Player();
            if (player != null && !permission.UserHasPermission(player.UserIDString, PermissionAdmin))
            {
                ShowMessage(player, Messages.NoPermission, this);
                return;
            }

            if (conArgs.Args.Length <= 2)
            {
                ShowMessage(player, Messages.WrongCommand, this, GetLobbyCommands());
                return;
            }

            var state = conArgs.GetString(0).ToLower();
            var lobbyId = conArgs.GetInt(1);
            var playerId = conArgs.GetULong(2);
            var lobbyToEdit = _pluginData.Lobbies.FirstOrDefault(x => x.Id == lobbyId);
            if (lobbyToEdit == null)
            {
                ShowMessage(player, Messages.LobbyNotFound, this);
                return;
            }

            if (!playerId.IsSteamId())
            {
                ShowMessage(player, Messages.InvalidPlayerId, this);
                return;
            }

            var targetPlayer = BasePlayer.FindAwakeOrSleeping(playerId.ToString());
            if (targetPlayer == null)
            {
                ShowMessage(player, Messages.PlayerNotFound, this);
                return;
            }

            if (state.Equals("add"))
            {
                lobbyToEdit.BannedPlayers.Add(playerId);
            }
            else if (state.Equals("remove"))
            {
                lobbyToEdit.BannedPlayers.Remove(playerId);
            }
            SaveData();
            Lobby targetLobby;
            if (Lobbies.TryGetValue(LobbyToEdit.Id, out targetLobby) && targetLobby != null)
            {
                targetLobby.Data = LobbyToEdit;
            }
            ShowMessage(player, Messages.ChangesSaved, this);
        }

        //lobby.allow <add|remove> <lobbyId> <playerId>
        [ConsoleCommand(ConsoleCommands.LobbyAllow)]
        void LobbyAccess(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                ShowMessage(null, Messages.WrongCommand, this, GetLobbyCommands());
                return;
            }

            var player = conArgs.Player();
            if (player != null && !permission.UserHasPermission(player.UserIDString, PermissionAdmin))
            {
                ShowMessage(player, Messages.NoPermission, this);
                return;
            }

            if (conArgs.Args.Length <= 2)
            {
                ShowMessage(player, Messages.WrongCommand, this, GetLobbyCommands());
                return;
            }

            var state = conArgs.GetString(0).ToLower();
            var lobbyId = conArgs.GetInt(1);
            var playerId = conArgs.GetULong(2);
            var lobbyToEdit = _pluginData.Lobbies.FirstOrDefault(x => x.Id == lobbyId);
            if (lobbyToEdit == null)
            {
                ShowMessage(player, Messages.LobbyNotFound, this);
                return;
            }

            if (!playerId.IsSteamId())
            {
                ShowMessage(player, Messages.InvalidPlayerId, this);
                return;
            }

            var targetPlayer = BasePlayer.FindAwakeOrSleeping(playerId.ToString());
            if (targetPlayer == null)
            {
                ShowMessage(player, Messages.PlayerNotFound, this);
                return;
            }

            if (state.Equals("add"))
            {
                lobbyToEdit.AllowedPlayers.Add(playerId);
            }
            else if (state.Equals("remove"))
            {
                lobbyToEdit.AllowedPlayers.Remove(playerId);
            }
            SaveData();
            Lobby targetLobby;
            if (Lobbies.TryGetValue(LobbyToEdit.Id, out targetLobby) && targetLobby != null)
            {
                targetLobby.Data = LobbyToEdit;
            }
            ShowMessage(player, Messages.ChangesSaved, this);
        }

        #endregion LobbyDataCommands

        #region GUICommands

        //lobby.join <lobbyId>
        [ConsoleCommand(ConsoleCommands.LobbyJoin)]
        void LobbyJoin(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                return;
            }

            var player = conArgs.Player();
            if (player == null)
            {
                ShowMessage(player, Messages.PlayerNotFound, this);
                return;
            }

            var lobbyId = conArgs.GetInt(0);
            Lobby lobbyToJoin;
            if (!Lobbies.TryGetValue(lobbyId, out lobbyToJoin) || lobbyToJoin == null)
            {
                ShowMessage(player, Messages.ArenaNotFound, this);
                return;
            }

            var spawnLocation = lobbyToJoin.GetRandomSpawnLocation();
            TeleportToLobby(player, lobbyId, spawnLocation);
        }

        //lobby.menu.clear
        [ConsoleCommand(ConsoleCommands.LobbyMenuClear)]
        void LobbyMenuClear(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                return;
            }
            var player = conArgs.Player();
            if (player == null)
            {
                ShowMessage(player, Messages.PlayerNotFound, this);
                return;
            }
            Ui.ClearAllMenus(player);
        }

        //lobby.leaderboard.show
        [ConsoleCommand(ConsoleCommands.LobbyLeaderboardShow)]
        void LobbyLeaderboardShow(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                return;
            }

            var player = conArgs.Player();
            if (player == null)
            {
                ShowMessage(player, Messages.PlayerNotFound, this);
                return;
            }

            PlayerPageIndex playerPageIndex;
            if (PlayerPageIndexes.TryGetValue(player.userID, out playerPageIndex) && playerPageIndex != null)
            {
                playerPageIndex.LeaderboardIndex = 0;
            }
            ShowLobbyLeaderboardMenu(player);
        }

        //lobby.arena.list.show
        [ConsoleCommand(ConsoleCommands.LobbyArenaListShow)]
        void LobbyArenaListShow(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                return;
            }

            var player = conArgs.Player();
            if (player == null)
            {
                ShowMessage(player, Messages.PlayerNotFound, this);
                return;
            }
            ShowLobbyArenas(player);
        }

        //page.next
        [ConsoleCommand(ConsoleCommands.NextPage)]
        void NextPage(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                return;
            }

            var player = conArgs.Player();
            if (player == null)
            {
                return;
            }
            PlayerPageIndex playerPageIndex;
            if (PlayerPageIndexes.TryGetValue(player.userID, out playerPageIndex) &&
                playerPageIndex != null)
            {
                playerPageIndex.LeaderboardIndex++;
            }

            ShowLobbyLeaderboardMenu(player);
        }

        //page.prev
        [ConsoleCommand(ConsoleCommands.PreviousPage)]
        void PreviousPage(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                return;
            }

            var player = conArgs.Player();
            if (player == null)
            {
                return;
            }
            PlayerPageIndex playerPageIndex;
            if (PlayerPageIndexes.TryGetValue(player.userID, out playerPageIndex) && playerPageIndex != null)
            {
                playerPageIndex.LeaderboardIndex--;
            }
            ShowLobbyLeaderboardMenu(player);
        }

        //arena.try.join <arenaId>
        [ConsoleCommand(ConsoleCommands.ArenaTryJoin)]
        void ArenaTryJoin(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
                return;
            var player = conArgs.Player();
            if (player == null)
            {
                return;
            }

            var arenaId = conArgs.GetInt(0);
            Interface.CallHook(nameof(IAdvancedLobby.HandleJoinArena), player, arenaId);
        }

        //server.leave
        [ConsoleCommand(ConsoleCommands.ServerLeave)]
        void ServerLeave(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
                return;
            var player = conArgs.Player();
            if (player == null)
            {
                return;
            }
            player.Kick(Lang(Messages.ServerLeaveMessage, player.UserIDString, this));
        }

        #endregion GUICommands

        //lobby.show
        [ConsoleCommand(ConsoleCommands.LobbyShow)]
        void ShowLobbies(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
                return;

            var player = conArgs.Player();
            if (player != null && !permission.UserHasPermission(player.UserIDString, PermissionAdmin))
            {
                ShowMessage(player, Messages.NoPermission, this);
                return;
            }
            DisplayBorders(player);
        }


        #endregion Commands

        #region GUI

        public void ShowLobbyArenas(BasePlayer player)
        {
            Ui.ClearAllMenus(player);
            var mainContainer = Ui.Container(Ui.Panels.Fullscreen, Ui.Color(Ui.ColorCode.Black, 0f), Ui.GetMin(0, 0), Ui.GetMax(0, 0), false);
            var arenaListSection = Ui.Container(Ui.Panels.ArenaList,
                Ui.Color(Ui.ColorCode.ShadowBlack, 0f), Ui.GetMin(521, 428), Ui.GetMax(520, 157), false, true, Ui.Panels.Fullscreen);
            var aimTrainsSection = Ui.Container(Ui.Panels.ArenaList,
                Ui.Color(Ui.ColorCode.ShadowBlack, 0f), Ui.GetMin(521, 215), Ui.GetMax(520, 730), false, true, Ui.Panels.Fullscreen);

            var closeImage = ImageManager.GetImage(nameof(Configuration.Close));
            var closeImageIsUrl = false;
            if (string.IsNullOrWhiteSpace(closeImage))
            {
                ServerMgr.Instance.StartCoroutine(ImageManager.DownloadImage(nameof(Configuration.Close), PluginConfig.Close));
                closeImage = PluginConfig.Close;
                closeImageIsUrl = true;
            }

            var vipLockImage = ImageManager.GetImage(nameof(Configuration.VipLocked));
            var vipLockImageIsUrl = false;
            if (string.IsNullOrWhiteSpace(vipLockImage))
            {
                ServerMgr.Instance.StartCoroutine(ImageManager.DownloadImage(nameof(Configuration.VipLocked), PluginConfig.VipLocked));
                vipLockImage = PluginConfig.VipLocked;
                vipLockImageIsUrl = true;
            }

            var lockImage = ImageManager.GetImage(nameof(Configuration.Locked));
            var lockImageIsUrl = false;
            if (string.IsNullOrWhiteSpace(lockImage))
            {
                ServerMgr.Instance.StartCoroutine(ImageManager.DownloadImage(nameof(Configuration.Locked), PluginConfig.Locked));
                lockImage = PluginConfig.Locked;
                lockImageIsUrl = true;
            }

            var unlockedImage = ImageManager.GetImage(nameof(Configuration.UnLocked));
            var unlockedImageIsUrl = false;
            if (string.IsNullOrWhiteSpace(unlockedImage))
            {
                ServerMgr.Instance.StartCoroutine(ImageManager.DownloadImage(nameof(Configuration.UnLocked), PluginConfig.UnLocked));
                unlockedImage = PluginConfig.UnLocked;
                unlockedImageIsUrl = true;
            }

            Ui.ImageButton(ref mainContainer, Ui.Panels.Fullscreen, Ui.Color(Ui.ColorCode.Red), closeImage,
                Ui.GetMin(1425, 893), Ui.GetMax(465, 157),
                $"{ConsoleCommands.LobbyMenuClear}", Ui.GetMin(8, 8, 30, 30),
                Ui.GetMax(8, 8, 30, 30), closeImageIsUrl);

            var currentLobby = Lobbies.Values.FirstOrDefault(x => x.ActivePlayers.Any(a => a.Player.userID == player.userID));
            var arenas = new List<ArenaDetail>();
            if (currentLobby != null)
            {
                arenas = GetArenasDetailsInLobby(currentLobby.Data.Id);
            }

            #region Team Deathmatch List
            Ui.Panel(ref arenaListSection, Ui.Panels.ArenaList, Ui.Color(Ui.ColorCode.Gray),
                Ui.GetMin(0, 455, 879, 495), Ui.GetMax(839, 0, 879, 495));
            Ui.Panel(ref arenaListSection, Ui.Panels.ArenaList, Ui.Color(Ui.ColorCode.Gray), "MATCHES",
                Ui.GetMin(45, 455, 879, 495), Ui.GetMax(344, 0, 879, 495), 18,
                Ui.GetMin(57, 455, 879, 495), align: TextAnchor.MiddleLeft);
            Ui.Panel(ref arenaListSection, Ui.Panels.ArenaList, Ui.Color(Ui.ColorCode.Gray), "TEAMS",
                Ui.GetMin(540, 455, 879, 495), Ui.GetMax(229, 0, 879, 495), 18);
            Ui.Panel(ref arenaListSection, Ui.Panels.ArenaList, Ui.Color(Ui.ColorCode.Gray), "PLAYERS",
                Ui.GetMin(654, 455, 879, 495), Ui.GetMax(115, 0, 879, 495), 18);
            Ui.Panel(ref arenaListSection, Ui.Panels.ArenaList, Ui.Color(Ui.ColorCode.Gray), "STATUS",
                Ui.GetMin(769, 455, 879, 495), Ui.GetMax(0, 0, 879, 495), 18);

            var tdmArenas = arenas.Where(x => x.MatchType == MatchType.Scrim).ToList();
            for (var i = 0; i < 10; i++)
            {
                Ui.Panel(ref arenaListSection, Ui.Panels.ArenaList, Ui.Color(Ui.ColorCode.Gray), $"{i + 1}",
                    Ui.GetVerticalMin(0, 405, i, 45, 879, 495),
                    Ui.GetVerticalMax(839, 50, i, 45, 879, 495),
                    18, textColor: Ui.Color(Ui.ColorCode.Gainsboro));

                var arenaDetail = tdmArenas.ElementAtOrDefault(i);
                if (arenaDetail != null)
                {
                    Ui.Button(ref arenaListSection, Ui.Panels.ArenaList, Ui.Color(Ui.ColorCode.ButtonBlack, 0.9f), arenaDetail.Name, 18,
                        Ui.GetVerticalMin(45, 405, i, 45, 879, 495),
                        Ui.GetVerticalMax(344, 50, i, 45, 879, 495),
                        $"{ConsoleCommands.ArenaTryJoin} {arenaDetail.ArenaId}",
                        Ui.GetMin(12, 0, 490, 40),
                        Ui.GetMax(0, 0, 490, 40),
                        false, align: TextAnchor.MiddleLeft);
                    Ui.Panel(ref arenaListSection, Ui.Panels.ArenaList, Ui.Color(Ui.ColorCode.ButtonBlack, 0.9f), $"{arenaDetail.BlueTeam}",
                        Ui.GetVerticalMin(540, 405, i, 45, 879, 495),
                        Ui.GetVerticalMax(287, 50, i, 45, 879, 495), 18);
                    Ui.Panel(ref arenaListSection, Ui.Panels.ArenaList, Ui.Color(Ui.ColorCode.ButtonBlack, 0.9f), $"{arenaDetail.RedTeam}",
                        Ui.GetVerticalMin(597, 405, i, 45, 879, 495),
                        Ui.GetVerticalMax(230, 50, i, 45, 879, 495), 18);
                    Ui.Panel(ref arenaListSection, Ui.Panels.ArenaList, Ui.Color(Ui.ColorCode.ButtonBlack, 0.9f), $"{arenaDetail.BlueTeam + arenaDetail.RedTeam} / {arenaDetail.Capacity}",
                        Ui.GetVerticalMin(654, 405, i, 45, 879, 495),
                        Ui.GetVerticalMax(115, 50, i, 45, 879, 495), 18);
                    Ui.Panel(ref arenaListSection, Ui.Panels.ArenaList, Ui.Color(Ui.ColorCode.ButtonBlack, 0.9f),
                        Ui.GetVerticalMin(769, 405, i, 45, 879, 495),
                        Ui.GetVerticalMax(0, 50, i, 45, 879, 495));

                    var statusImage = arenaDetail.IsRestricted ? lockImage : arenaDetail.IsVip ? vipLockImage : unlockedImage;
                    var isUrl = arenaDetail.IsRestricted ? lockImageIsUrl : arenaDetail.IsVip ? vipLockImageIsUrl : unlockedImageIsUrl;
                    Ui.Image(ref arenaListSection, Ui.Panels.ArenaList, statusImage,
                        Ui.GetVerticalMin(769, 405, i, 45, 879, 495),
                        Ui.GetVerticalMax(0, 50, i, 45, 879, 495), isUrl);
                }
                else
                {
                    Ui.Panel(ref arenaListSection, Ui.Panels.ArenaList, Ui.Color(Ui.ColorCode.ButtonBlack, 0.9f),
                        Ui.GetVerticalMin(45, 405, i, 45, 879, 495),
                        Ui.GetVerticalMax(344, 50, i, 45, 879, 495));
                    Ui.Panel(ref arenaListSection, Ui.Panels.ArenaList, Ui.Color(Ui.ColorCode.ButtonBlack, 0.9f),
                        Ui.GetVerticalMin(540, 405, i, 45, 879, 495),
                        Ui.GetVerticalMax(287, 50, i, 45, 879, 495));
                    Ui.Panel(ref arenaListSection, Ui.Panels.ArenaList, Ui.Color(Ui.ColorCode.ButtonBlack, 0.9f),
                        Ui.GetVerticalMin(597, 405, i, 45, 879, 495),
                        Ui.GetVerticalMax(230, 50, i, 45, 879, 495));
                    Ui.Panel(ref arenaListSection, Ui.Panels.ArenaList, Ui.Color(Ui.ColorCode.ButtonBlack, 0.9f),
                        Ui.GetVerticalMin(654, 405, i, 45, 879, 495),
                        Ui.GetVerticalMax(115, 50, i, 45, 879, 495));
                    Ui.Panel(ref arenaListSection, Ui.Panels.ArenaList, Ui.Color(Ui.ColorCode.ButtonBlack, 0.9f),
                        Ui.GetVerticalMin(769, 405, i, 45, 879, 495),
                        Ui.GetVerticalMax(0, 50, i, 45, 879, 495));
                }
            }

            #endregion Team Deathmatch List

            #region Advanced Aim Train

            Ui.Panel(ref aimTrainsSection, Ui.Panels.ArenaList, Ui.Color(Ui.ColorCode.Gray),
                Ui.GetMin(0, 95, 879, 135), Ui.GetMax(839, 0, 879, 135));
            Ui.Panel(ref aimTrainsSection, Ui.Panels.ArenaList, Ui.Color(Ui.ColorCode.Gray), "Recoil Training Ground",
                Ui.GetMin(45, 95, 879, 135), Ui.GetMax(0, 0, 879, 135), 18,
                Ui.GetMin(57, 95, 879, 135), align: TextAnchor.MiddleLeft);

            var atArenas = arenas.Where(x => x.MatchType == MatchType.TrainingGround).ToList();
            for (var i = 0; i < 2; i++)
            {
                Ui.Panel(ref aimTrainsSection, Ui.Panels.ArenaList, Ui.Color(Ui.ColorCode.Gray), $"{i + 1}",
                    Ui.GetVerticalMin(0, 45, i, 45, 879, 135),
                    Ui.GetVerticalMax(839, 50, i, 45, 879, 135),
                    18, textColor: Ui.Color(Ui.ColorCode.Gainsboro));

                var arenaDetail = atArenas.ElementAtOrDefault(i);
                if (arenaDetail != null)
                {
                    Ui.Button(ref aimTrainsSection, Ui.Panels.ArenaList, Ui.Color(Ui.ColorCode.ButtonBlack, 0.9f), arenaDetail.Name, 18,
                        Ui.GetVerticalMin(45, 45, i, 45, 879, 135),
                        Ui.GetVerticalMax(0, 50, i, 45, 879, 135),
                        $"{ConsoleCommands.ArenaTryJoin} {arenaDetail.ArenaId}",
                        Ui.GetMin(12, 0, 490, 40),
                        Ui.GetMax(0, 0, 490, 40),
                        false, align: TextAnchor.MiddleLeft);
                }
                else
                {
                    Ui.Panel(ref aimTrainsSection, Ui.Panels.ArenaList, Ui.Color(Ui.ColorCode.ButtonBlack, 0.9f),
                        Ui.GetVerticalMin(45, 45, i, 45, 879, 135),
                        Ui.GetVerticalMax(0, 50, i, 45, 879, 135));
                }
            }
            #endregion Advanced Aim Train

            mainContainer.AddRange(arenaListSection);
            mainContainer.AddRange(aimTrainsSection);
            CuiHelper.AddUi(player, mainContainer);
        }
        public void ShowLobbyMenu(BasePlayer player)
        {
            Ui.ClearAllMenus(player);
            var mainContainer = Ui.Container(Ui.Panels.PauseMenu, Ui.Color(Ui.ColorCode.Black, 0f), Ui.GetMin(0, 0), Ui.GetMax(0, 0), false);

            Ui.Button(ref mainContainer, Ui.Panels.PauseMenu, Ui.Color(Ui.ColorCode.Gray, 0.9f),
                "RESUME", 28,
                Ui.GetMin(760, 602), Ui.GetMax(760, 428),
                $"{ConsoleCommands.LobbyMenuClear}", false);

            Ui.Button(ref mainContainer, Ui.Panels.PauseMenu, Ui.Color(Ui.ColorCode.Blue, 0.9f),
                "PLAY", 28,
                Ui.GetMin(760, 544), Ui.GetMax(760, 486),
                $"{ConsoleCommands.LobbyArenaListShow}", false);

            Ui.Button(ref mainContainer, Ui.Panels.PauseMenu, Ui.Color(Ui.ColorCode.Blue, 0.9f),
                  "LEADERBOARD", 28,
                  Ui.GetMin(760, 486), Ui.GetMax(760, 544),
                  $"{ConsoleCommands.LobbyLeaderboardShow}", false);

            Ui.Button(ref mainContainer, Ui.Panels.PauseMenu, Ui.Color(Ui.ColorCode.Red, 0.9f),
                  "QUIT SERVER", 28,
                  Ui.GetMin(760, 428), Ui.GetMax(760, 602),
                  $"{ConsoleCommands.ServerLeave}", false);

            CuiHelper.AddUi(player, mainContainer);
        }
        public void ShowLobbyLeaderboardMenu(BasePlayer player)
        {
            Ui.ClearAllMenus(player);
            var mainContainer = Ui.Container(Ui.Panels.Fullscreen, Ui.Color(Ui.ColorCode.Black, 0f), Ui.GetMin(0, 0), Ui.GetMax(0, 0), false);
            var entities = GetGlobalStatistics();
            var players = Filter(player, entities, ListType.Leaderboard, 10);

            var leaderboardSelection = Ui.Container(Ui.Panels.LeaderboardMenu,
                Ui.Color(Ui.ColorCode.ShadowBlack, 0.5f), Ui.GetMin(750, 733), Ui.GetMax(750, 238), false, true, Ui.Panels.Fullscreen);
            var playersList = Ui.Container(Ui.Panels.LeaderboardMenu,
                Ui.Color(Ui.ColorCode.ShadowBlack, 0.5f), Ui.GetMin(468, 197), Ui.GetMax(468, 359), false, true, Ui.Panels.Fullscreen);

            var leftArrowImageId = ImageManager.GetImage(nameof(Configuration.LeftArrow));
            var rightArrowImageId = ImageManager.GetImage(nameof(Configuration.RightArrow));
            if (string.IsNullOrWhiteSpace(leftArrowImageId))
            {
                ServerMgr.Instance.StartCoroutine(ImageManager.DownloadImage(nameof(Configuration.LeftArrow), PluginConfig.LeftArrow));
                Ui.ImageButton(ref mainContainer, Ui.Panels.Fullscreen, Ui.Color(Ui.ColorCode.ButtonBlack, 0.9f),
                    PluginConfig.LeftArrow, Ui.GetMin(810, 156), Ui.GetMax(1084, 898),
                    $"{ConsoleCommands.PreviousPage}", isUrl: true);
            }
            else
            {
                Ui.ImageButton(ref mainContainer, Ui.Panels.Fullscreen, Ui.Color(Ui.ColorCode.ButtonBlack, 0.9f),
                    leftArrowImageId, Ui.GetMin(810, 156), Ui.GetMax(1084, 898),
                    $"{ConsoleCommands.PreviousPage}");
            }
            if (string.IsNullOrWhiteSpace(rightArrowImageId))
            {
                ServerMgr.Instance.StartCoroutine(ImageManager.DownloadImage(nameof(Configuration.RightArrow), PluginConfig.RightArrow));
                Ui.ImageButton(ref mainContainer, Ui.Panels.Fullscreen, Ui.Color(Ui.ColorCode.ButtonBlack, 0.9f),
                    PluginConfig.RightArrow, Ui.GetMin(1084, 156), Ui.GetMax(810, 898),
                    $"{ConsoleCommands.NextPage}", isUrl: true);
            }
            else
            {
                Ui.ImageButton(ref mainContainer, Ui.Panels.Fullscreen, Ui.Color(Ui.ColorCode.ButtonBlack, 0.9f),
                    rightArrowImageId, Ui.GetMin(1084, 156), Ui.GetMax(810, 898),
                    $"{ConsoleCommands.NextPage}");
            }
            Ui.Button(ref mainContainer, Ui.Panels.Fullscreen, Ui.Color(Ui.ColorCode.Blue, 0.9f), "BACK TO GAME",
                19, Ui.GetMin(840, 153), Ui.GetMax(840, 895), $"{ConsoleCommands.LobbyMenuClear}", false);

            Ui.Panel(ref leaderboardSelection, Ui.Panels.LeaderboardMenu, Ui.Color(Ui.ColorCode.Gray), "LEADERBOARD",
                Ui.GetMin(14, 57, 420, 109), Ui.GetMax(14, 12, 420, 109), 22);

            Ui.Button(ref leaderboardSelection, Ui.Panels.LeaderboardMenu,
                 Ui.Color(Ui.ColorCode.Blue),
                 "GLOBAL", 19, Ui.GetMin(14, 12, 420, 109), Ui.GetMax(14, 57, 420, 109),
                 $"{ConsoleCommands.LobbyLeaderboardShow}", false);

            Ui.Panel(ref playersList, Ui.Panels.LeaderboardMenu, Ui.Color(Ui.ColorCode.Gray), "RANK",
                Ui.GetMin(12, 472, 984, 524), Ui.GetMax(897, 12, 984, 524), 22);
            Ui.Panel(ref playersList, Ui.Panels.LeaderboardMenu, Ui.Color(Ui.ColorCode.Gray),
                Ui.GetMin(92, 472, 984, 524), Ui.GetMax(852, 12, 984, 524));
            Ui.Panel(ref playersList, Ui.Panels.LeaderboardMenu, Ui.Color(Ui.ColorCode.Gray), "NAME",
                Ui.GetMin(137, 472, 984, 524), Ui.GetMax(357, 12, 984, 524), 22,
                Ui.GetMin(149, 472, 984, 524), align: TextAnchor.MiddleLeft);
            Ui.Panel(ref playersList, Ui.Panels.LeaderboardMenu, Ui.Color(Ui.ColorCode.Gray), "KILLS",
                Ui.GetMin(632, 472, 984, 524), Ui.GetMax(242, 12, 984, 524), 22);
            Ui.Panel(ref playersList, Ui.Panels.LeaderboardMenu, Ui.Color(Ui.ColorCode.Gray), "DEATHS",
                Ui.GetMin(747, 472, 984, 524), Ui.GetMax(127, 12, 984, 524), 22);
            Ui.Panel(ref playersList, Ui.Panels.LeaderboardMenu, Ui.Color(Ui.ColorCode.Gray), "DAMAGE",
                Ui.GetMin(862, 472, 984, 524), Ui.GetMax(12, 12, 984, 524), 22);
            for (var i = 0; i < 10; i++)
            {
                var playerStats = players.ElementAtOrDefault(i);
                if (playerStats == null || playerStats.Player == null)
                {
                    Ui.Panel(ref playersList, Ui.Panels.LeaderboardMenu, Ui.Color(Ui.ColorCode.ButtonBlack),
                        Ui.GetVerticalMin(12, 422, i, 45, 984, 524), Ui.GetVerticalMax(897, 62, i, 45, 984, 524));
                    Ui.Panel(ref playersList, Ui.Panels.LeaderboardMenu, Ui.Color(Ui.ColorCode.ButtonBlack),
                        Ui.GetVerticalMin(92, 422, i, 45, 984, 524), Ui.GetVerticalMax(852, 62, i, 45, 984, 524));
                    Ui.Panel(ref playersList, Ui.Panels.LeaderboardMenu, Ui.Color(Ui.ColorCode.ButtonBlack),
                        Ui.GetVerticalMin(137, 422, i, 45, 984, 524), Ui.GetVerticalMax(357, 62, i, 45, 984, 524));
                    Ui.Panel(ref playersList, Ui.Panels.LeaderboardMenu, Ui.Color(Ui.ColorCode.ButtonBlack),
                        Ui.GetVerticalMin(632, 422, i, 45, 984, 524), Ui.GetVerticalMax(242, 62, i, 45, 984, 524));
                    Ui.Panel(ref playersList, Ui.Panels.LeaderboardMenu, Ui.Color(Ui.ColorCode.ButtonBlack),
                        Ui.GetVerticalMin(747, 422, i, 45, 984, 524), Ui.GetVerticalMax(127, 62, i, 45, 984, 524));
                    Ui.Panel(ref playersList, Ui.Panels.LeaderboardMenu, Ui.Color(Ui.ColorCode.ButtonBlack),
                        Ui.GetVerticalMin(862, 422, i, 45, 984, 524), Ui.GetVerticalMax(12, 62, i, 45, 984, 524));
                }
                else
                {
                    string avatarUrl;
                    var color = playerStats.Player.userID == player.userID
                        ? Ui.Color(Ui.ColorCode.DarkBlue)
                        : Ui.Color(Ui.ColorCode.ButtonBlack);
                    if (PlayersAvatarUrls.TryGetValue(playerStats.Player.userID, out avatarUrl) && !string.IsNullOrWhiteSpace(avatarUrl))
                    {
                        Ui.Image(ref playersList, Ui.Panels.LeaderboardMenu, avatarUrl,
                            Ui.GetVerticalMin(92, 422, i, 45, 984, 524), Ui.GetVerticalMax(852, 62, i, 45, 984, 524),
                            true);
                    }
                    else
                    {
                        Ui.Panel(ref playersList, Ui.Panels.LeaderboardMenu, color,
                            Ui.GetVerticalMin(92, 422, i, 45, 984, 524), Ui.GetVerticalMax(852, 62, i, 45, 984, 524));
                    }
                    Ui.Panel(ref playersList, Ui.Panels.LeaderboardMenu, color, playerStats.Rank.ToString(),
                        Ui.GetVerticalMin(12, 422, i, 45, 984, 524), Ui.GetVerticalMax(897, 62, i, 45, 984, 524), 19);
                    Ui.Panel(ref playersList, Ui.Panels.LeaderboardMenu, color, playerStats.Player.displayName,
                        Ui.GetVerticalMin(137, 422, i, 45, 984, 524), Ui.GetVerticalMax(357, 62, i, 45, 984, 524), 19,
                        Ui.GetVerticalMin(149, 422, i, 45, 984, 524), align: TextAnchor.MiddleLeft);
                    Ui.Panel(ref playersList, Ui.Panels.LeaderboardMenu, color, playerStats.Kills.ToString(),
                        Ui.GetVerticalMin(632, 422, i, 45, 984, 524), Ui.GetVerticalMax(242, 62, i, 45, 984, 524), 19);
                    Ui.Panel(ref playersList, Ui.Panels.LeaderboardMenu, color, playerStats.Deaths.ToString(),
                        Ui.GetVerticalMin(747, 422, i, 45, 984, 524), Ui.GetVerticalMax(127, 62, i, 45, 984, 524), 19);
                    Ui.Panel(ref playersList, Ui.Panels.LeaderboardMenu, color, $"{playerStats.DamagesDone:N0}",
                        Ui.GetVerticalMin(862, 422, i, 45, 984, 524), Ui.GetVerticalMax(12, 62, i, 45, 984, 524), 19);
                }
            }
            mainContainer.AddRange(leaderboardSelection);
            mainContainer.AddRange(playersList);
            CuiHelper.AddUi(player, mainContainer);
        }
        #endregion GUI
        public void FreezePlayer(BasePlayer player)
        {
            var pos = player.ServerPosition;
            _freezeTimers[player.userID] = timer.Every(0.1f, () =>
            {
                if (!player.IsConnected)
                {
                    _freezeTimers[player.userID]?.Destroy();
                    return;
                }

                player.Teleport(pos);
            });
        }
        public void UnfreezePlayer(ulong playerId)
        {
            if (_freezeTimers.ContainsKey(playerId))
            {
                _freezeTimers[playerId]?.Destroy();
            }
        }
        public List<int> GetArenaIdsInLobby(int lobbyId)
        {
            var counts = new List<int>();
            var teamDeathmatch = plugins.PluginManager.GetPlugin("Scrim");
            if (teamDeathmatch != null)
            {
                var hookResult = teamDeathmatch.Call(nameof(IAdvancedLobby.GetArenaIdsInLobby), lobbyId) as List<int>;
                if (hookResult != null)
                    counts.AddRange(hookResult);
            }
            var advanceTraining = plugins.PluginManager.GetPlugin("RecoilTrainingGround");
            if (advanceTraining != null)
            {
                var hookResult = advanceTraining.Call(nameof(IAdvancedLobby.GetArenaIdsInLobby), lobbyId) as List<int>;
                if (hookResult != null)
                    counts.AddRange(hookResult);
            }
            return counts;
        }
        public List<ArenaDetail> GetArenasDetailsInLobby(int lobbyId)
        {
            var counts = new List<ArenaDetail>();
            var teamDeathmatch = plugins.PluginManager.GetPlugin("Scrim");
            if (teamDeathmatch != null)
            {
                var hookResult = teamDeathmatch.Call(nameof(IAdvancedLobby.GetArenasDetailsInLobby), lobbyId) as List<ArenaDetail>;
                if (hookResult != null)
                    counts.AddRange(hookResult);
            }
            var advanceTraining = plugins.PluginManager.GetPlugin("RecoilTrainingGround");
            if (advanceTraining != null)
            {
                var hookResult = advanceTraining.Call(nameof(IAdvancedLobby.GetArenasDetailsInLobby), lobbyId) as List<ArenaDetail>;
                if (hookResult != null)
                    counts.AddRange(hookResult);
            }
            return counts;
        }
        private void CheckDatabase()
        {
            try
            {
                _statisticsDatabaseConnection = Sqlite?.OpenDb(_databasePath, this);
                if (_statisticsDatabaseConnection == null)
                {
                    PrintError("Couldn't open GameModeManager database");
                    _statisticsDatabaseConnection.Con?.Close();
                    return;
                }

                Sqlite?.ExecuteNonQuery(Sql.Builder.Append(
                    $"CREATE TABLE IF NOT EXISTS {_globalStatisticsTableName} (PlayerId TEXT NOT NULL PRIMARY KEY UNIQUE," +
                    " Kills INTEGER NOT NULL DEFAULT 0, Deaths INTEGER NOT NULL DEFAULT 0, DamagesDone REAL NOT NULL DEFAULT 0);"
                ), _statisticsDatabaseConnection);
            }
            catch (Exception exception)
            {
                //_statisticsDatabaseConnection.Close();
                _statisticsDatabaseConnection?.Con?.Close();
                PrintError(exception.Message);
            }
        }
        public void ClearEntities(string zoneId)
        {
            //TODO: Add Behaviour for players entities
            var entities = ZoneManager?.Call<List<BaseEntity>>("GetEntitiesInZone", zoneId);
            if (entities == null)
            {
                return;
            }
            foreach (var entity in entities)
            {
                entity.Kill();
            }
        }
        public void LockEntities(string zoneId)
        {
            var entities = ZoneManager?.Call<List<BaseEntity>>("GetEntitiesInZone", zoneId);
            if (entities == null)
            {
                return;
            }
            foreach (var entity in entities)
            {
                entity.OwnerID = 11111111111111111;
                entity.SetFlag(BaseEntity.Flags.Locked, true);
                entity.SendNetworkUpdate();
            }
        }
        public void UnLockEntities(string zoneId, ulong adminId)
        {
            var entities = ZoneManager?.Call<List<BaseEntity>>("GetEntitiesInZone", zoneId);
            if (entities == null)
            {
                return;
            }
            foreach (var entity in entities)
            {
                entity.OwnerID = adminId;
                entity.SetFlag(BaseEntity.Flags.Locked, false);
                entity.SendNetworkUpdate();
            }
        }
        public void UpdateScore(List<PlayerStatistics> playersStatistics)
        {
            try
            {
                //_statisticsDatabaseConnection.Open(_databasePath);
                foreach (var playerStatistics in playersStatistics)
                {
                    Sqlite.Query(Core.Database.Sql.Builder.Append(
                        $"SELECT PlayerId, Kills, Deaths, DamagesDone FROM {_globalStatisticsTableName} WHERE PlayerId = @0 ORDER BY Kills", playerStatistics.Player.userID),
                        _statisticsDatabaseConnection, list =>
                        {
                            if (list != null)
                            {
                                var row = list.FirstOrDefault();
                                if (row != null)
                                {
                                    playerStatistics.Kills += int.Parse(row["Kills"].ToString());
                                    playerStatistics.Deaths += int.Parse(row["Deaths"].ToString());
                                    playerStatistics.DamagesDone += float.Parse(row["DamagesDone"].ToString());
                                    Sqlite.ExecuteNonQuery(Sql.Builder.Append($"UPDATE {_globalStatisticsTableName} SET Kills = {playerStatistics.Kills}, Deaths ={playerStatistics.Deaths}, DamagesDone = {playerStatistics.DamagesDone} WHERE PlayerId = {playerStatistics.Player.userID}"),
                                        _statisticsDatabaseConnection);
                                }
                                else
                                {
                                    Sqlite.ExecuteNonQuery(Sql.Builder.Append($"INSERT INTO {_globalStatisticsTableName} (PlayerId, Kills, Deaths, DamagesDone) VALUES({playerStatistics.Player.userID},{playerStatistics.Kills},{playerStatistics.Deaths},{playerStatistics.DamagesDone})"),
                                        _statisticsDatabaseConnection);
                                }
                            }
                        });

                }
            }
            catch (Exception exception)
            {
                PrintError(exception.Message);
            }
            finally
            {
                //_statisticsDatabaseConnection.Close();
            }
        }
        public List<PlayerRank> GetGlobalStatistics()
        {
            var entities = new List<PlayerRank>();
            try
            {
                //_statisticsDatabaseConnection.Open(_databasePath);
                Sqlite.Query(Sql.Builder.Append($"SELECT PlayerId, Kills, Deaths, DamagesDone FROM {_globalStatisticsTableName}"), _statisticsDatabaseConnection,
                    list =>
                    {
                        foreach (var tableValue in list)
                        {
                            object idObj;
                            if (tableValue.TryGetValue("PlayerId", out idObj) && idObj != null)
                            {
                                var player = BasePlayer.FindAwakeOrSleeping(idObj.ToString());
                                if (player != null)
                                {
                                    var entity = new PlayerRank(player)
                                    {
                                        Kills = int.Parse(tableValue["Kills"].ToString()),
                                        Deaths = int.Parse(tableValue["Deaths"].ToString()),
                                        DamagesDone = float.Parse(tableValue["DamagesDone"].ToString())
                                    };
                                    entities.Add(entity);
                                }
                            }
                        }
                    });

            }
            catch (Exception exception)
            {
                PrintError(exception.Message);
            }
            finally
            {
                //_statisticsDatabaseConnection.Close();
            }

            return entities.OrderByDescending(x => x.Score).Select((x, i) => { x.Rank = i + 1; return x; }).ToList();
        }
        public List<T> Filter<T>(BasePlayer player, IEnumerable<T> players, ListType listType, int pageSize)
        {
            var index = 0;
            var count = players.Count();
            PlayerPageIndex playerPageIndex;
            if (!PlayerPageIndexes.TryGetValue(player.userID, out playerPageIndex) || playerPageIndex == null)
            {
                playerPageIndex = new PlayerPageIndex();
                PlayerPageIndexes[player.userID] = playerPageIndex;
            }

            switch (listType)
            {
                case ListType.Leaderboard:
                    {
                        if (playerPageIndex.LeaderboardIndex < 0)
                        {
                            playerPageIndex.LeaderboardIndex = 0;
                        }
                        else if (playerPageIndex.LeaderboardIndex * pageSize >= count)
                        {
                            playerPageIndex.LeaderboardIndex = Mathf.CeilToInt((float)count / pageSize) - 1;
                        }
                        index = playerPageIndex.LeaderboardIndex;
                        break;
                    }
                case ListType.Match:
                    {
                        if (playerPageIndex.MatchIndex < 0)
                        {
                            playerPageIndex.MatchIndex = 0;
                        }
                        else if (playerPageIndex.MatchIndex * pageSize >= count)
                        {
                            playerPageIndex.MatchIndex = Mathf.CeilToInt((float)count / pageSize) - 1;
                        }
                        index = playerPageIndex.MatchIndex;
                        break;
                    }
                case ListType.Search:
                    {
                        if (playerPageIndex.SearchIndex < 0)
                        {
                            playerPageIndex.SearchIndex = 0;
                        }
                        else if (playerPageIndex.SearchIndex * pageSize >= count)
                        {
                            playerPageIndex.SearchIndex = Mathf.CeilToInt((float)count / pageSize) - 1;
                        }
                        index = playerPageIndex.SearchIndex;
                        break;
                    }
                case ListType.Ban:
                    {
                        if (playerPageIndex.BanIndex < 0)
                        {
                            playerPageIndex.BanIndex = 0;
                        }
                        else if (playerPageIndex.BanIndex * pageSize >= count)
                        {
                            playerPageIndex.BanIndex = Mathf.CeilToInt((float)count / pageSize) - 1;
                        }
                        index = playerPageIndex.BanIndex;
                        break;
                    }
                case ListType.Allow:
                    {
                        if (playerPageIndex.AllowIndex < 0)
                        {
                            playerPageIndex.AllowIndex = 0;
                        }
                        else if (playerPageIndex.AllowIndex * pageSize >= count)
                        {
                            playerPageIndex.AllowIndex = Mathf.CeilToInt((float)count / pageSize) - 1;
                        }
                        index = playerPageIndex.AllowIndex;
                        break;
                    }
            }
            return players.Skip(index * pageSize).Take(pageSize).ToList();
        }
        public void UpdatePlayerProfileImage(ulong playerId)
        {
            webrequest.Enqueue($"https://steamcommunity.com/profiles/{playerId}?xml=1", string.Empty,
                (code, result) =>
                {
                    if (code >= 200 && code <= 204)
                    {
                        var url = _steamAvatarRegex.Match(result).Value;
                        if (!string.IsNullOrWhiteSpace(url))
                        {
                            PlayersAvatarUrls[playerId] = url;
                        }
                    }
                }, this);
        }
        public void ResetPlayer(BasePlayer player)
        {
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Wounded, false);
            player.InitializeHealth(player.MaxHealth(), player.MaxHealth());
            player.metabolism.calories.value = player.metabolism.calories.max;
            player.metabolism.hydration.value = player.metabolism.hydration.max;
            player.metabolism.bleeding.value = 0;
            player.metabolism.radiation_level.value = 0;
            player.metabolism.radiation_poison.value = 0;
            player.metabolism.SendChangesToClient();
            player.SendNetworkUpdateImmediate();
        }

        public void InventoryStrip(BasePlayer player)
        {
            player.inventory.containerMain?.Clear();
            player.inventory.containerWear?.Clear();
            player.inventory.containerBelt?.Clear();
            ItemManager.DoRemoves();
        }
        public bool GiveLoadout(BasePlayer player, string loadoutName)
        {
            if (InventoryLoadout == null || string.IsNullOrWhiteSpace(loadoutName))
                return false;
            return InventoryLoadout.Call<bool>("GiveLoadout", player, loadoutName);
        }
        private void RemoveFromLobbies(BasePlayer player)
        {
            var activeLobby = Lobbies.Values.FirstOrDefault(x => x.ActivePlayers.Any(a => a.Player.userID == player.userID));
            if (activeLobby != null)
            {
                activeLobby.ActivePlayers.RemoveWhere(x => x.Player.userID == player.userID);
                InventoryStrip(player);
            }
        }
        private Tuple<Vector3?, int> GetSpawnPoint(BasePlayer player)
        {
            var currentLobby = Lobbies.Values.FirstOrDefault(x => x.ActivePlayers.Any(a => a.Player.userID == player.userID));
            if (currentLobby != null)
            {
                var spawnLocation = currentLobby.GetRandomSpawnLocation();
                if (spawnLocation.Equals(Vector3.zero))
                {
                    return null;
                }

                return new Tuple<Vector3?, int>(spawnLocation, currentLobby.Data.Id);
            }

            var mainLobby = Lobbies.Values.FirstOrDefault(x => x.Data.IsMain);
            if (mainLobby != null)
            {
                var spawnLocation = mainLobby.GetRandomSpawnLocation();
                if (spawnLocation.Equals(Vector3.zero))
                {
                    return null;
                }
                return new Tuple<Vector3?, int>(spawnLocation, mainLobby.Data.Id);
            }

            return null;
        }
        private string GetLobbyCommands()
        {
            var commands = new StringBuilder(Environment.NewLine);
            commands.AppendLine("<color=#eb9534>Lobby Console Commands:</color>");
            commands.AppendLine($"<color=#5af>{ConsoleCommands.LobbyShow}</color> Show Lobbies, Arenas and their spawn points");
            commands.AppendLine($"<color=#5af>{ConsoleCommands.LobbyHelp}</color> Get Lobby management commands list");
            commands.AppendLine($"<color=#5af>{ConsoleCommands.LobbyEdit} <id></color> Start editing a Lobby");
            commands.AppendLine($"<color=#5af>{ConsoleCommands.LobbyDone}</color> Stop editing a Lobby");
            commands.AppendLine($"<color=#5af>{ConsoleCommands.LobbyDelete} <id></color> Delete a Lobby");
            commands.AppendLine($"<color=#5af>{ConsoleCommands.LobbyCreate} <Name> <ZoneId></color> Create a Lobby");
            commands.AppendLine($"<color=#5af>{ConsoleCommands.LobbySet} <key> <value> [<key> <value>] [<key> <value>] ...</color> Set values for an editing Lobby");
            commands.AppendLine($"<color=#5af>{ConsoleCommands.LobbySetHelp}</color> Get list of available keys for editing the Lobby");
            commands.AppendLine($"<color=#5af>{ConsoleCommands.LobbyDeleteSpawn} <name></color> Delete a spawn point in Lobby");
            commands.AppendLine($"<color=#5af>{ConsoleCommands.LobbyAllow} <add|remove> <lobbyId> <playerId></color> Add/Remove player to the lobby whitelist");
            commands.AppendLine($"<color=#5af>{ConsoleCommands.LobbyBan} <add|remove> <lobbyId> <playerId></color> Ban/Unban player from the lobby");
            commands.AppendLine("<color=#5582ff>For passing the position you can use 'here' to send your current position or use X,Y,Z coordinates</color>");
            return commands.ToString();
        }
        private string GetLobbyEditingKeys()
        {
            var keys = new StringBuilder(Environment.NewLine);
            keys.AppendLine("<color=#5af>name</color>");
            keys.AppendLine("<color=#5af>zone</color> or <color=#5af>zoneId</color>");
            keys.AppendLine("<color=#5af>main</color> or <color=#5af>isMain</color>");
            keys.AppendLine("<color=#5af>kit</color> or <color=#5af>loadout</color> or <color=#5af>loadoutName</color>");
            keys.AppendLine("<color=#5af>restrict</color> or <color=#5af>restrictAccess</color>");
            keys.AppendLine("<color=#5af>spawn</color> or <color=#5af>spawnLocation</color>");
            return keys.ToString();
        }
        public void ShowMessage(BasePlayer player, string messageKey, Plugin plugin, params object[] args)
        {
            if (player != null)
            {
                player.ChatMessage(Lang(messageKey, player.UserIDString, plugin, args));
            }
            else
            {
                PrintWarning(Lang(messageKey, null, plugin, args));
            }
        }
        private string Lang(string key, string id, Plugin plugin, params object[] args) => string.Format(lang.GetMessage(key, plugin, id), args);
        public Color GetColor(string hexColor)
        {
            if (string.IsNullOrEmpty(hexColor))
            {
                hexColor = "#FFFFFFFF";
            }

            var str = hexColor.Trim('#');

            if (str.Length == 3)
                str += str;

            if (str.Length == 6)
                str += "FF";

            if (str.Length != 8)
            {
                str = "FFFFFFFF";
            }

            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);
            var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);

            return new Color32(r, g, b, a);
        }
        private void DisplayBorders(BasePlayer player, int visibilityTime = 60)
        {
            if (player == null)
                return;

            foreach (var lobby in Lobbies.Values)
            {

                player.SendConsoleCommand("ddraw.text", visibilityTime, GetColor("#F2994A"),
                    lobby.Zone.Location + new Vector3(0, 1.5f, 0), $"<size=20>{lobby.Data.Name} ({lobby.Data.Id})</size>");
                ShowZone(player, lobby.Zone, true, true, visibilityTime);

                foreach (var spawnLocation in lobby.Data.SpawnLocations)
                {
                    player.SendConsoleCommand("ddraw.text", visibilityTime, GetColor("#F2994A"),
                        spawnLocation.SpawnPoint + new Vector3(0, 1.5f, 0), $"<size=20>{spawnLocation.Name}</size>");
                    player.SendConsoleCommand("ddraw.sphere", visibilityTime, GetColor("#BDBDBD"), spawnLocation.SpawnPoint, spawnLocation.Radius);
                    player.SendConsoleCommand("ddraw.sphere", visibilityTime, GetColor("#BDBDBD"), spawnLocation.SpawnPoint, 1);
                }

                Interface.Call(nameof(IAdvancedLobby.HandleShowArena), player, lobby.Data.Id, visibilityTime);
            }
        }
        public void ShowZone(BasePlayer player, Zone zone, bool isEnabled, bool isLobby, float visibilityTime = 60)
        {
            var color = isEnabled
                ? isLobby ? GetColor("#F2994A") : GetColor("#F2C94C")
                : GetColor("#333333");
            if (zone.Size != Vector3.zero)
            {
                var center = zone.Location;
                var rotation = Quaternion.Euler(zone.Rotation);
                var size = zone.Size / 2;
                var point1 = RotatePointAroundPivot(new Vector3(center.x + size.x, center.y + size.y, center.z + size.z), center, rotation);
                var point2 = RotatePointAroundPivot(new Vector3(center.x + size.x, center.y - size.y, center.z + size.z), center, rotation);
                var point3 = RotatePointAroundPivot(new Vector3(center.x + size.x, center.y + size.y, center.z - size.z), center, rotation);
                var point4 = RotatePointAroundPivot(new Vector3(center.x + size.x, center.y - size.y, center.z - size.z), center, rotation);
                var point5 = RotatePointAroundPivot(new Vector3(center.x - size.x, center.y + size.y, center.z + size.z), center, rotation);
                var point6 = RotatePointAroundPivot(new Vector3(center.x - size.x, center.y - size.y, center.z + size.z), center, rotation);
                var point7 = RotatePointAroundPivot(new Vector3(center.x - size.x, center.y + size.y, center.z - size.z), center, rotation);
                var point8 = RotatePointAroundPivot(new Vector3(center.x - size.x, center.y - size.y, center.z - size.z), center, rotation);

                player.SendConsoleCommand("ddraw.line", visibilityTime, color, point1, point2);
                player.SendConsoleCommand("ddraw.line", visibilityTime, color, point1, point3);
                player.SendConsoleCommand("ddraw.line", visibilityTime, color, point1, point5);
                player.SendConsoleCommand("ddraw.line", visibilityTime, color, point4, point2);
                player.SendConsoleCommand("ddraw.line", visibilityTime, color, point4, point3);
                player.SendConsoleCommand("ddraw.line", visibilityTime, color, point4, point8);
                player.SendConsoleCommand("ddraw.line", visibilityTime, color, point5, point6);
                player.SendConsoleCommand("ddraw.line", visibilityTime, color, point5, point7);
                player.SendConsoleCommand("ddraw.line", visibilityTime, color, point6, point2);
                player.SendConsoleCommand("ddraw.line", visibilityTime, color, point8, point6);
                player.SendConsoleCommand("ddraw.line", visibilityTime, color, point8, point7);
                player.SendConsoleCommand("ddraw.line", visibilityTime, color, point7, point3);
            }
            else player.SendConsoleCommand("ddraw.sphere", visibilityTime, color, zone.Location, zone.Radius);
        }
        private Vector3 RotatePointAroundPivot(Vector3 point, Vector3 pivot, Quaternion rotation) => rotation * (point - pivot) + pivot;
        public Zone GetZone(string id)
        {
            var zoneData = ZoneManager?.Call<Dictionary<string, string>>("ZoneFieldList", id);
            if (zoneData == null || zoneData.Count == 0)
                return null;

            try
            {
                return new Zone
                {
                    Id = zoneData["ID"],
                    Name = zoneData["name"],
                    Comfort = float.Parse(zoneData["comfort"]),
                    Temperature = float.Parse(zoneData["temperature"]),
                    Radiation = float.Parse(zoneData["radiation"]),
                    Radius = float.Parse(zoneData["radius"]),
                    Rotation = zoneData["rotation"].ToVector3(),
                    Size = zoneData["size"].ToVector3(),
                    Location = zoneData["Location"].ToVector3(),
                    EnterMessage = zoneData["enter_message"],
                    LeaveMessage = zoneData["leave_message"],
                    Permission = zoneData["permission"],
                    EjectSpawns = zoneData["ejectspawns"]
                };
            }
            catch (Exception)
            {
                return null;
            }
        }
        public List<LobbyPlayer> GetZonePlayers(string id)
        {
            var zonePlayers = ZoneManager?.Call("GetPlayersInZone", id) as List<BasePlayer>;
            if (zonePlayers != null)
            {
                return zonePlayers.Select(x => new LobbyPlayer(x)).ToList();
            }
            return new List<LobbyPlayer>();
        }
        public Vector3 GetGroundPosition(Vector3 sourcePos)
        {
            RaycastHit hitInfo;

            if (UnityEngine.Physics.Raycast(sourcePos, Vector3.down, out hitInfo, Mathf.Infinity, LayerMask.GetMask("Terrain", "World", "Construction")))
            {
                sourcePos.y = hitInfo.point.y;
            }
            sourcePos.y = Mathf.Max(sourcePos.y, TerrainMeta.HeightMap.GetHeight(sourcePos));
            return sourcePos;
        }
        public void TeleportToLobby(BasePlayer player, int lobbyId, Vector3 destination)
        {
            if (player == null || player.IsNpc || destination.Equals(Vector3.zero))
                return;

            Lobby destinationLobby;
            if (Lobbies.TryGetValue(lobbyId, out destinationLobby) && destinationLobby != null)
            {
                if ((!destinationLobby.Data.RestrictAccess || destinationLobby.Data.AllowedPlayers.Contains(player.userID))
                    && !destinationLobby.Data.BannedPlayers.Contains(player.userID))
                {
                    RemoveFromLobbies(player);
                    destinationLobby.ActivePlayers.Add(new LobbyPlayer(player));
                    GiveLoadout(player, destinationLobby.Data.LoadoutName);
                    ResetPlayer(player);
                    Teleport(player, destination);
                    Ui.ClearAllMenus(player);
                    return;
                }
                ShowMessage(player, Messages.LobbyNoAccess, this);
            }
            ShowMessage(player, Messages.LobbyNotFound, this);
        }
        public void Teleport(BasePlayer player, Vector3 destination, bool checkGround = false, bool sleep = true)
        {
            if (player.IsNpc || destination.Equals(Vector3.zero))
                return;

            if (player.isMounted)
                player.GetMounted().DismountPlayer(player, true);

            if (player.GetParentEntity() != null)
                player.SetParent(null);

            if (checkGround)
                destination = GetGroundPosition(destination);
            TeleportingPlayers.Add(player.userID);
            timer.Once(2, () =>
            {
                TeleportingPlayers.Remove(player.userID);
            });
            if (sleep)
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
                player.MovePosition(destination);
                player.UpdateNetworkGroup();
                if (player.inventory != null && player.inventory.containerMain != null && player.inventory.containerBelt != null && player.inventory.containerWear != null)
                    player.StartSleeping();
                player.SendNetworkUpdateImmediate(false);
                player.ClearEntityQueue(null);
                player.ClientRPCPlayer(null, player, "StartLoading");
                if (player.net?.subscriber?.subscribed != null)
                    player.SendFullSnapshot();
            }
            else
            {
                player.MovePosition(destination);
                player.ClientRPCPlayer(null, player, "ForcePositionTo", destination);
                player.SendNetworkUpdateImmediate();
                player.ClearEntityQueue(null);
            }
        }
        public void UpdateInGameText(string text, Vector3 position, float textVisibilityRange = 20f)
        {
            var players = Facepunch.Pool.GetList<BasePlayer>();
            Vis.Entities(position, textVisibilityRange, players);
            if (players.Count > 0)
            {
                foreach (var player in players)
                {
                    if (player == null || player.IsDead() || player.isMounted)
                        continue;

                    if (player.IsAdmin)
                        player.SendConsoleCommand("ddraw.text", TextRefreshRate, Color.white, position, text);
                    else
                    {
                        player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                        player.SendNetworkUpdateImmediate();
                        player.SendConsoleCommand("ddraw.text", TextRefreshRate, Color.white, position, text);
                        player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                        player.SendNetworkUpdateImmediate();
                    }
                }
            }
            Facepunch.Pool.FreeList(ref players);
        }
        private Configuration GetDefaultConfig()
        {
            return new Configuration();
        }
        private void SaveData()
        {
            _dataManager.WriteObject(_pluginData);
        }
        private void LoadData()
        {
            try
            {
                _pluginData = _dataManager.ReadObject<PluginData>();
            }
            catch (Exception exception)
            {
                PrintError("Data file is corrupt, error message:");
                PrintError(exception.Message);
                if (exception.InnerException != null)
                {
                    PrintError($"Inner exception message: {exception.InnerException.Message}");
                }
                PrintWarning("New data file created");
                _pluginData = new PluginData();
            }
        }
        private void RemoveLobby(int lobbyId)
        {
            Lobby lobby;
            if (Lobbies.TryGetValue(lobbyId, out lobby) && lobby != null)
            {
                Interface.Call(nameof(IAdvancedLobby.HandleRemoveArena), lobbyId);

                var mainLobby = Lobbies.Values.FirstOrDefault(x => x.Data.IsMain && x.Data.Id != lobbyId);
                if (mainLobby != null)
                {
                    foreach (var player in lobby.ActivePlayers)
                    {
                        var spawnLocation = mainLobby.GetRandomSpawnLocation();
                        TeleportToLobby(player.Player, mainLobby.Data.Id, spawnLocation);
                    }
                }

                Lobbies.Remove(lobbyId);
            }
        }
        private void GenerateLobbies()
        {
            TransportedPlayers = new List<BasePlayer>();
            foreach (var lobbyData in _pluginData.Lobbies)
            {
                var lobby = new Lobby
                {
                    Data = lobbyData,
                    Zone = GetZone(lobbyData.ZoneId),
                    ActivePlayers = new HashSet<LobbyPlayer>(GetZonePlayers(lobbyData.ZoneId))
                };
                Lobbies.Add(lobbyData.Id, lobby);
                TransportedPlayers.AddRange(lobby.ActivePlayers.Select(x => x.Player));
            }

            var mainLobby = Lobbies.Values.FirstOrDefault(x => x.Data.IsMain);
            if (mainLobby != null)
            {
                var remainingPlayers = BasePlayer.activePlayerList
                    .Where(x => TransportedPlayers.All(p => p.userID != x.userID)).ToList();

                foreach (var basePlayer in remainingPlayers)
                {
                    TeleportToLobby(basePlayer, mainLobby.Data.Id, mainLobby.GetRandomSpawnLocation());
                }
            }
            TransportedPlayers.Clear();
        }
        private bool ValidateData()
        {
            var duplicates = _pluginData.Lobbies.GroupBy(g => g.Id)
                .Where(x => x.Count() > 1)
                .Select(x => x.Key).ToList();
            if (duplicates.Count > 0)
            {
                PrintError($"These lobby Ids are duplicated: {string.Join(", ", duplicates)}");
                return false;
            }
            //TODO: Validate arena id duplication
            /*var arenas = _pluginData.Lobbies.SelectMany(x => x.Arenas).ToList();
            duplicates = arenas.GroupBy(g => g.Id)
                .Where(x => x.Count() > 1)
                .Select(x => x.Key).ToList();
            if (duplicates.Count > 0)
            {
                PrintError($"These arena Ids are duplicated: {string.Join(", ", duplicates)}");
                return false;
            }*/

            if (InventoryLoadout != null)
            {
                foreach (var lobbyData in _pluginData.Lobbies)
                {
                    if (!IsLoadoutValid(lobbyData.LoadoutName))
                    {
                        PrintWarning($"{lobbyData.LoadoutName} loadout name of the {lobbyData.Name} lobby is invalid.");
                    }

                    Interface.Call(nameof(IAdvancedLobby.HandleArenaLoadoutValidation), lobbyData.Id);
                }
            }


            //TODO: Validate Spawn Point/Entrance Locations / Zones

            return true;
        }

        public bool IsLoadoutValid(string loadoutName) => InventoryLoadout.Call<bool>("IsValidLoadout", loadoutName);

        #region RustHooks
        object OnTeamCreate(BasePlayer player)
        {
            return false;
        }
        protected override void SaveConfig() => Config.WriteObject(PluginConfig, true);
        protected override void LoadConfig()
        {
            base.LoadConfig();
            PluginConfig = Config.ReadObject<Configuration>();
            Config.Settings.Converters = new JsonConverter[] { new StringEnumConverter() };

            SaveConfig();
        }
        protected override void LoadDefaultConfig()
        {
            Config.Settings.Converters = new JsonConverter[] { new StringEnumConverter() };
            PluginConfig = GetDefaultConfig();
        }
        void Init()
        {
            Config.Settings.Converters = new JsonConverter[] { new StringEnumConverter() };
        }
        private void Loaded()
        {
            permission.RegisterPermission(PermissionAdmin, this);
            permission.RegisterPermission(PermissionVanishBypass, this);
            permission.RegisterPermission(PermissionIgnore, this);
            _dataManager = Interface.Oxide.DataFileSystem.GetFile($"{MainFolderName}/{nameof(GameModeManager)}");
            _dataManager.Settings.Converters = new JsonConverter[] { new StringEnumConverter() };
            CheckDatabase();
        }
        private void Unload()
        {
            foreach (var arenaBehaviour in UnityEngine.Object.FindObjectsOfType<ArenaBehaviour>())
            {
                if (arenaBehaviour != null)
                    arenaBehaviour.DoDestroy();
            }

            if (ImageManager != null && ImageManager.gameObject != null)
            {
                UnityEngine.Object.Destroy(ImageManager.gameObject);
            }
            foreach (var imageManager in UnityEngine.Object.FindObjectsOfType<ImageManagerBehaviour>())
            {
                if (imageManager != null)
                    UnityEngine.Object.Destroy(imageManager);
            }

            foreach (var player in BasePlayer.allPlayerList)
            {
                if (player != null && player.inventory != null)
                    player.inventory.containerWear?.SetLocked(false);
                Ui.ClearAllMenus(player);
            }
            _statisticsDatabaseConnection.Con.Close();
            _instance = null;
        }
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [Messages.NoPermission] = "You don't have permission to use this command",
                [Messages.WrongCommand] = "You entered the command in a wrong way, available commands are: {0}",
                [Messages.LobbyCommandsList] = "Available lobby commands are: {0}",
                [Messages.InvalidPlayerId] = "Invalid player steamID",
                [Messages.PlayerNotFound] = "No player found",
                [Messages.LobbyNotFound] = "No lobby found",
                [Messages.ArenaNotFound] = "No arena found",
                [Messages.ChangesSaved] = "Changes has been saved",
                [Messages.NoEditingLobby] = "There is no lobby to edit, use the {0} to start editing a lobby",
                [Messages.LobbyParameterChanged] = "The lobby {0} has been changed",
                [Messages.LobbySpawnPointAdded] = "New spawn point {0} has been added to the lobby",
                [Messages.LobbyInvalidPosition] = "The target position is an invalid position, it should be inside the lobby",
                [Messages.LobbyDeleted] = "The lobby has been deleted",
                [Messages.LobbyCreated] = "The lobby has been created",
                [Messages.LobbyEditingStarted] = "Lobby {0} ({1}) editing has been started",
                [Messages.LobbyEditingDone] = "Lobby {0} ({1}) editing has been stopped",
                [Messages.LobbyNoAccess] = "You don't have access to this lobby",
                [Messages.MainLobbyNotMain] = "You aren't able to change IsMain to false for the main lobby",
                [Messages.LobbyZoneNotFound] = "Lobby Zone not found",
                [Messages.KitNotFound] = "Kit not found",
                [Messages.SpawnPointNotFound] = "Spawn Point not found",
                [Messages.ServerLeaveMessage] = "You left the server!",
                [Messages.LobbySetValueHelp] = "Available keys for editing the lobby: {0}",
            }, this);
        }
        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null || player.IsNpc)
                return;
            if (!permission.UserHasPermission(player.UserIDString, PermissionIgnore))
                player.inventory.containerWear.SetLocked(true);
            var spawnPoint = GetSpawnPoint(player);
            if (spawnPoint?.Item1 != null)
            {
                TeleportToLobby(player, spawnPoint.Item2, spawnPoint.Item1.Value);
            }
            UpdatePlayerProfileImage(player.userID);
        }
        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            RemoveFromLobbies(player);
            if (!player.IsDestroyed)
                player.Kill();
        }
        object OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (player == null)
                return null;

            var lobby = Lobbies.Values.FirstOrDefault(currentLobby =>
                currentLobby.ActivePlayers.Any(activePlayer => activePlayer.Player.userID == player.userID));
            if (lobby != null)
            {
                return false;
            }
            return null;
        }
        object OnPlayerRespawn(BasePlayer player)
        {
            if (player == null || player.IsNpc)
                return null;
            Vector3? spawnPoint = Interface.Call(nameof(IAdvancedLobby.HandleOnPlayerRespawn), player) as Vector3?;
            if (!spawnPoint.HasValue)
            {
                var spawnPointData = GetSpawnPoint(player);
                if (spawnPointData == null)
                    return null;
                spawnPoint = spawnPointData.Item1;
            }

            if (!spawnPoint.HasValue)
            {
                return null;
            }

            return new BasePlayer.SpawnPoint { pos = spawnPoint.Value, rot = new Quaternion(0, 0, 0, 1) };
        }
        object OnDefaultItemsReceive(PlayerInventory inventory)
        {
            if (inventory == null || inventory._baseEntity == null)
            {
                return null;
            }
            var player = inventory._baseEntity;
            var currentLobby = Lobbies.Values.FirstOrDefault(x => x.ActivePlayers.Any(a => a.Player.userID == player.userID));
            if (currentLobby != null)
            {
                GiveLoadout(player, currentLobby.Data.LoadoutName);
            }
            return true;
        }
        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity.OwnerID == 11111111111111111 && entity.HasFlag(BaseEntity.Flags.Locked))
            {
                info?.damageTypes.Clear();
                return false;
            }
            var player = entity as BasePlayer;
            if (player != null)
            {
                var lobby = Lobbies.Values.FirstOrDefault(currentLobby =>
                    currentLobby.ActivePlayers.Any(activePlayer => activePlayer.Player.userID == player.userID));
                if (lobby != null)
                {
                    info?.damageTypes.Clear();
                    return false;
                }
            }
            return null;
        }
        void OnUserPermissionGranted(string id, string permName)
        {
            if (permName.Equals(PermissionIgnore, StringComparison.OrdinalIgnoreCase))
            {
                var player = BasePlayer.FindAwakeOrSleeping(id);
                if (player != null && player.IsConnected)
                {
                    player.inventory.containerWear?.SetLocked(false);
                }
            }
        }
        void OnUserPermissionRevoked(string id, string permName)
        {
            if (permName.Equals(PermissionIgnore, StringComparison.OrdinalIgnoreCase))
            {
                var player = BasePlayer.FindAwakeOrSleeping(id);
                if (player != null && player.IsConnected)
                {
                    player.inventory.containerWear?.SetLocked(true);
                }
            }
        }
        private void OnServerInitialized()
        {
            _instance = this;

            if (!plugins.Exists(nameof(ZoneManager)) || ZoneManager == null)
            {
                PrintError("ZoneManager Not Found");
                return;
            }

            LoadData();
            if (!ValidateData())
            {
                return;
            }
            GenerateLobbies();

            cmd.AddChatCommand(PluginConfig.ArenaLeaveCommand, this, LeaveArenaChatCommand);
            cmd.AddChatCommand(PluginConfig.ShowLobbyPauseMenuCommand, this, ShowLobbyPauseMenuCommand);
            var webObject = GameObject.Find("ProArenaWebObject");
            if (webObject != null)
            {
                ImageManager = webObject.GetComponent<ImageManagerBehaviour>();
            }
            if (ImageManager == null)
                ImageManager = new GameObject("ProArenaWebObject").AddComponent<ImageManagerBehaviour>();

            ServerMgr.Instance.StartCoroutine(ImageManager.DownloadImage(nameof(Configuration.LeftArrow), PluginConfig.LeftArrow));
            ServerMgr.Instance.StartCoroutine(ImageManager.DownloadImage(nameof(Configuration.RightArrow), PluginConfig.RightArrow));
            ServerMgr.Instance.StartCoroutine(ImageManager.DownloadImage(nameof(Configuration.UpArrow), PluginConfig.UpArrow));
            ServerMgr.Instance.StartCoroutine(ImageManager.DownloadImage(nameof(Configuration.DownArrow), PluginConfig.DownArrow));
            ServerMgr.Instance.StartCoroutine(ImageManager.DownloadImage(nameof(Configuration.SearchButton), PluginConfig.SearchButton));
            ServerMgr.Instance.StartCoroutine(ImageManager.DownloadImage(nameof(Configuration.Search), PluginConfig.Search));
            ServerMgr.Instance.StartCoroutine(ImageManager.DownloadImage(nameof(Configuration.Clear), PluginConfig.Clear));
            ServerMgr.Instance.StartCoroutine(ImageManager.DownloadImage(nameof(Configuration.Close), PluginConfig.Close));
            ServerMgr.Instance.StartCoroutine(ImageManager.DownloadImage(nameof(Configuration.Checked), PluginConfig.Checked));
            ServerMgr.Instance.StartCoroutine(ImageManager.DownloadImage(nameof(Configuration.Info), PluginConfig.Info));
            ServerMgr.Instance.StartCoroutine(ImageManager.DownloadImage(nameof(Configuration.Locked), PluginConfig.Locked));
            ServerMgr.Instance.StartCoroutine(ImageManager.DownloadImage(nameof(Configuration.UnLocked), PluginConfig.UnLocked));
            ServerMgr.Instance.StartCoroutine(ImageManager.DownloadImage(nameof(Configuration.VipLocked), PluginConfig.VipLocked));
            ServerMgr.Instance.StartCoroutine(ImageManager.DownloadImage(nameof(Configuration.MatchLeader), PluginConfig.MatchLeader));

            foreach (var player in BasePlayer.allPlayerList)
            {
                if (player != null && player.inventory != null && !permission.UserHasPermission(player.UserIDString, PermissionIgnore))
                    player.inventory.containerWear?.SetLocked(true);
                UpdatePlayerProfileImage(player.userID);
                if (!player.IsDestroyed && !player.IsConnected)
                    player.Kill();
            }
        }
        #endregion RustHooks

        #region CustomHooks

        [HookMethod("RegisterArena")]
        private void RegisterArena(string pluginName)
        {
            var targetPlugin = plugins.PluginManager.GetPlugin(pluginName);
            if (targetPlugin != null)
            {
                targetPlugin.Call(nameof(IAdvancedLobby.InitializeArena), this);
            }
        }

        #endregion CustomHooks
        #endregion

        public interface IAdvancedLobby
        {
            void InitializeArena(GameModeManager arena);
            List<int> GetArenaIdsInLobby(int lobbyId);
            List<ArenaDetail> GetArenasDetailsInLobby(int lobbyId);
            List<BasePlayer> GetPlayerMatchPlayers(ulong playerId);
            void HandleShowArena(BasePlayer player, int lobbyId, int visibilityTime);
            void HandleRemoveArena(int lobbyId);
            void HandleArenaLoadoutValidation(int lobbyId);
            object HandleJoinArena(BasePlayer player, int arenaId);
            void HandleLeaveArena(BasePlayer player);
            object HandleOnPlayerRespawn(BasePlayer player);
            object HandleShowPauseMenu(BasePlayer player);
            void HandleArenaHelp(BasePlayer player);
        }
    }
}