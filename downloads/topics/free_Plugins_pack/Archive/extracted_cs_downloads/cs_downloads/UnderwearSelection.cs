using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Plugins;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Underwear Selection", "Marat", "1.0.2")]
    [Description("Allows players to customize their underwear appearance")]
    class UnderwearSelection : RustPlugin
    {
        [PluginReference] private Plugin ImageLibrary;
        
        #region Field
        
        private const string InitialLayer = "UI_UnderwearLayer";
        private const string permissionUse = "underwearselection.use";
        
        #endregion
        
        #region Oxide Hooks
        
        private void OnServerInitialized()
        {
            if (ImageLibrary == null)
            {
                PrintError("[ImageLibrary] not found! Plugin is disabled!");
                Interface.Oxide.UnloadPlugin(Title);
                return;
            }
            
            foreach (var list in config.Underwear)
            {
                ImageLibrary.Call("AddImage", list.UrlMale, $"{list.Title}.Male");
                ImageLibrary.Call("AddImage", list.UrlFemale, $"{list.Title}.Female");
            }
            
            LoadData();
            AddCovalenceCommand(config.Commands, nameof(CmdChangeWear));
            permission.RegisterPermission(permissionUse, this);
            
            for (int i = 0; i < BasePlayer.activePlayerList.Count; i++)
            {
                OnPlayerConnected(BasePlayer.activePlayerList[i]);
            }
        }
        
        private void Unload()
        {
            for (int i = 0; i < BasePlayer.activePlayerList.Count; i++)
            {
                var player = BasePlayer.activePlayerList[i];
                CuiHelper.DestroyUi(player, InitialLayer);
                player.nextUnderwearValidationTime = Time.time + 0.2f;
                ServerMgr.Instance?.StopCoroutine(PreloadImages(player));
            }
            SaveData();
            config = null;
        }
        
        private void OnServerSave() => SaveData();
        
        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null || !player.IsConnected) return;
            if (player.IsReceivingSnapshot)
            {
                timer.Once(1f, () => OnPlayerConnected(player));
                return;
            }
            ServerMgr.Instance?.StartCoroutine(PreloadImages(player));
            
            if (!storedData.PlayerData.TryGetValue(player.userID, out var data))
            {
                data = new Data();
                storedData.PlayerData.Add(player.userID, data);
            }
            if (config.SkinConnection == 0 && !config.UseRandomSkin)
            {
                data.enable = false;
                player.nextUnderwearValidationTime = Time.time + 0.2f;
            }
            else if (config.SkinConnection != 0)
            {
                data.enable = true;
                UnderwearChange(player, config.SkinConnection);
                return;
            }
            if (!permission.UserHasPermission(player.UserIDString, permissionUse)) return;
            UnderwearChange(player, data.id);
        }
        
        private void OnPlayerDisconnected(BasePlayer player)
        {
            player.nextUnderwearValidationTime = Time.time + 0.2f;
            ServerMgr.Instance?.StopCoroutine(PreloadImages(player));
        }
        
        private void OnPlayerRespawned(BasePlayer player)
        {
            if (player == null || !player.IsConnected) return;
            if (!storedData.PlayerData.TryGetValue(player.userID, out var data))
            {
                data = new Data();
                storedData.PlayerData.Add(player.userID, data);
            }
            if (config.UseRandomSkin)
            {
                var random = new System.Random();
                var randomUnderwear = config.Underwear[random.Next(config.Underwear.Count)];
                data.enable = true;
                UnderwearChange(player, randomUnderwear.Id);
                return;
            }
            if (!permission.UserHasPermission(player.UserIDString, permissionUse)) return;
            UnderwearChange(player, player.lastValidUnderwearSkin);
        }
        
        #endregion
        
        #region Configuration
        
        private static PluginConfig config;
        
        private class PluginConfig
        {
            [JsonProperty("Commands")] public string[] Commands;
            [JsonProperty("Forced underwear skin when a player connects")] public uint SkinConnection;
            [JsonProperty("Random underwear skin upon respawn")] public bool UseRandomSkin;
            [JsonProperty("Image and description")] public List<ItemInfo> Underwear;
        }
        
        protected override void LoadDefaultConfig()
        {
            config = new PluginConfig
            {
                Commands = new string[] {"uw", "wear", "underwear"},
                SkinConnection = 0,
                UseRandomSkin = false,
                Underwear = new List<ItemInfo>()
                {
                    new("Scribble", 792014640, "https://i.ibb.co/cvFYv4B/scribble-male.png", "https://i.ibb.co/mHLj0FP/scribble-female.png"),
                    new("Gradient", 241501709, "https://i.ibb.co/MG5GMKh/gradient-male.png", "https://i.ibb.co/b70811b/gradient-female.png"),
                    new("PalmLeaves", 1756736103, "https://i.ibb.co/YQFnzZm/palmleaves-male.png", "https://i.ibb.co/z6dhs5R/palmleaves-female.png"),
                    new("Bikini/Rapido", 359039573, "https://i.ibb.co/42pYF1D/rapido-male.png", "https://i.ibb.co/j3wZXF4/pink-bikini.png"),
                    new("Coconut", 3797783720, "https://i.ibb.co/5RcSNsW/coconut-female-male.png", "https://i.ibb.co/5RcSNsW/coconut-female-male.png"),
                    new("MummyWraps", 1154108357, "https://i.ibb.co/3SWCyj2/mummywraps-male.png", "https://i.ibb.co/p15j3cD/mummywraps-female.png"),
                    new("Purple", 1967073602, "https://i.ibb.co/yVCBwSD/purple-male.png", "https://i.ibb.co/0QnjzKT/purple-female.png"),
                    new("GrassSkirt", 4122325535, "https://i.ibb.co/8XvwtzS/grassskirt-male.png", "https://i.ibb.co/T4Yg0Cm/grassskirt-top-female.png")
                }
            };
        }
        
        private class ItemInfo
        {
            public string Title;
            public uint Id;
            public string UrlMale;
            public string UrlFemale;
            
            public ItemInfo(string title, uint id, string urlMale, string urlFemale)
            {
                Title = title;
                Id = id;
                UrlMale = urlMale;
                UrlFemale = urlFemale;
            }
        }
        
        protected override void SaveConfig() => Config.WriteObject(config);
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<PluginConfig>();
                if (config == null) LoadDefaultConfig();
            }
            catch
            {
                PrintWarning("The config file contains an error and has been replaced with the default config.");
                LoadDefaultConfig();
            }
            SaveConfig();
        }
        
        #endregion
        
        #region Commands
        
        private void CmdChangeWear(IPlayer ipPlayer, string command, string[] arg)
        {
            var player = ipPlayer?.Object as BasePlayer;
            if (player == null) return;
            if (!permission.UserHasPermission(player.UserIDString, permissionUse))
            {
                PrintToChat(player, GetMessage("Lang_NoPermissions", player));
                return;
            }
            InitializeLayers(player, true);
        }
        
        [ConsoleCommand("UI_WearController")]
        private void CmdConsoleHandler(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !arg.HasArgs(1)) return;
            var data = storedData.PlayerData[player.userID];
            switch (arg.Args[0].ToLower())
            {
                case "choose":
                {
                    if (!uint.TryParse(arg.Args[1], out uint index)) return;
                    data.id = index;
                    data.enable = true;
                    UnderwearChange(player, index);
                    InitializeLayers(player, false);
                    EffectNetwork.Send(new Effect("assets/prefabs/npc/autoturret/effects/targetlost.prefab", player, 0, new Vector3(), new Vector3()), player.Connection);
                    break;
                }
                case "enabled":
                {
                    var enable = !data.enable;
                    data.enable = enable;
                    if (!data.enable) player.nextUnderwearValidationTime = Time.time + 0.2f;
                    InitializeLayers(player, false);
                    break;
                }
            }
        }
        
        #endregion
        
        #region Methods
        
        private void UnderwearChange(BasePlayer player, uint id)
        {
            if (!storedData.PlayerData[player.userID].enable) return;
            player.nextUnderwearValidationTime = float.PositiveInfinity;
            player.lastValidUnderwearSkin = id;
            player.SendNetworkUpdateImmediate();
        }
        
        private string GetMessage(string key, BasePlayer player)
        {
            return lang.GetMessage(key, this, player.UserIDString);
        }
        
        #endregion
        
        #region Interfaces
        
        private void InitializeLayers(BasePlayer player, bool update)
        {
            float fade = !update ? 0f : 0.25f;
            var data = storedData.PlayerData[player.userID];
            
            CuiElementContainer container = new CuiElementContainer();
            
            container.Add(new CuiElement()
            {
                Parent = "Overlay",
                Name = InitialLayer,
                DestroyUi = InitialLayer,
                Components =
                {
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                    new CuiImageComponent { Color = "0.235 0.227 0.2 0.9" },
                    new CuiNeedsCursorComponent()
                }
            });
            
            container.Add(new CuiButton()
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                Button = { Close = InitialLayer, Color = "0.141 0.137 0.096 0.98", Sprite = "assets/content/ui/ui.background.transparent.radial.psd" }
            }, InitialLayer);
            
            container.Add(new CuiPanel()
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"-440 -300", OffsetMax = $"440 300" },
                Image = { Color = "0.11 0.12 0.10 0.8", Material = "assets/content/ui/uibackgroundblur.mat", FadeIn = fade }
            }, InitialLayer, InitialLayer + ".Main");
            
            container.Add(new CuiPanel()
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"0 -50", OffsetMax = $"0 0" },
                Image = { Color = "0.08 0.08 0.08 0.8", Material = "assets/content/ui/uibackgroundblur.mat", FadeIn = fade }
            }, InitialLayer + ".Main", InitialLayer + ".Title");
            
            container.Add(new CuiLabel()
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                Text = { Text = GetMessage("Lang_Title", player).ToUpper(), Color = "0.78 0.74 0.70 1.0", FontSize = 20, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter, FadeIn = fade }
            }, InitialLayer + ".Title");
            
            container.Add(new CuiPanel()
            {
                RectTransform = { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = $"20 -13", OffsetMax = $"120 13" },
                Image = { Color = "0.11 0.12 0.10 0.8", Material = "assets/content/ui/uibackgroundblur.mat", FadeIn = fade }
            }, InitialLayer + ".Title", InitialLayer + ".Toggle");
            
            var text = GetMessage(data.enable ? "Lang_Enable" : "Lang_Disable", player);
            var offsetMin = data.enable ? "1 1" : "50 1";
            var offsetMax = data.enable ? "-50 -1" : "-1 -1";
            
            container.Add(new CuiButton()
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = offsetMin, OffsetMax = offsetMax },
                Button = { Command = $"UI_WearController enabled", Color = data.enable ? "0.36 0.44 0.22 1.0" : "0.71 0.22 0.15 1.0", Material = "assets/content/ui/uibackgroundblur.mat" },
                Text = { Text = text.ToUpper(), Color = data.enable ? "0.78 0.74 0.70 1.0" : "0.78 0.74 0.70 0.6", FontSize = 12, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter }
            }, InitialLayer + ".Toggle");
            
            container.Add(new CuiButton()
            {
                RectTransform = { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-25 -25", OffsetMax = "-5 -5" },
                Button = { Close = InitialLayer, Color = "0.71 0.22 0.15 1.0", Sprite = "assets/icons/close.png" }
            }, InitialLayer + ".Main");
            
            const int marginTop = 18, margin = 15, width = 200, height = 250;
            
            for (var i = 0; i < config.Underwear.Count; i++)
            {
                var offsetX = i % 4 * (width + margin) - (2 * width + 1.5 * margin);
                var offsetY = -i / 4 * (height + margin) - marginTop;
                
                container.Add(new CuiPanel()
                {
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"{offsetX} {offsetY}", OffsetMax = $"{offsetX + width} {offsetY + height}" },
                    Image = { Color = "0.19 0.23 0.14 1.0", Material = "assets/content/ui/uibackgroundblur.mat", FadeIn = fade }
                }, InitialLayer + ".Main", InitialLayer + $".Button.{i}");
                
                var isSelected = player.lastValidUnderwearSkin == config.Underwear[i].Id;
                var image = Underwear.IsFemale(player) ? $"{config.Underwear[i].Title}.Female" : $"{config.Underwear[i].Title}.Male";
                
                container.Add(new CuiElement()
                {
                    Parent = InitialLayer + $".Button.{i}",
                    Name = InitialLayer + $".Image.{i}",
                    DestroyUi = InitialLayer + $".Image.{i}",
                    Components =
                    {
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-85 -72", OffsetMax = "85 98" },
                        new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", image), Color = "1 1 1 1" }
                    }
                });
                
                if (isSelected)
                {
                    container.Add(new CuiPanel()
                    {
                        RectTransform = { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-30 -28", OffsetMax = "-10 -10" },
                        Image = { Color = "0.81 0.77 0.74 0.8", Sprite = "assets/icons/clothing.png" }
                    }, InitialLayer + $".Button.{i}");
                }
                
                container.Add(new CuiPanel()
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 0", OffsetMax = "0 26" },
                    Image = { Color = "0 0 0 0.6" }
                }, InitialLayer + $".Button.{i}", InitialLayer + $".Title.{i}");
                
                string[] parts = config.Underwear[i].Title.Split('/');
                string genderTitle = Underwear.IsFemale(player) ? parts[0] : (parts.Length > 1 ? parts[1] : parts[0]);
                
                container.Add(new CuiLabel()
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "10 0", OffsetMax = "125 26" },
                    Text = { Text = GetMessage($"{genderTitle}", player).ToUpper(), Color = "0.81 0.77 0.74 1.0", FontSize = 11, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleLeft }
                }, InitialLayer + $".Title.{i}");
                
                var textTitle = GetMessage(isSelected ? "Lang_Installed" : "Lang_Apply", player);
                var colorTitle = !isSelected ? "0.59 0.84 0.18 1.0" : "0.30 0.65 0.90 1.0";
                var colorButton = !isSelected ? "0.30 0.36 0.16 1.0" : "0.20 0.30 0.40 1.0";
                var command = !isSelected ? $"UI_WearController choose {config.Underwear[i].Id}" : "";
                
                container.Add(new CuiButton()
                {
                    RectTransform = { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-75 0", OffsetMax = "0 26" },
                    Button = { Command = command, Color = colorButton, Material = "assets/content/ui/uibackgroundblur.mat" },
                    Text = { Text = textTitle.ToUpper(), Color = colorTitle, FontSize = 11, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter }
                }, InitialLayer + $".Title.{i}");
            }
            
            CuiHelper.AddUi(player, container);
        }
        
        private IEnumerator PreloadImages(BasePlayer player)
        {
            if (player == null || !player.IsConnected) yield break;
            for (var i = 0; i < config.Underwear.Count; i++)
            {
                var image = $"{config.Underwear[i].Title}.{(Underwear.IsFemale(player) ? "Female" : "Male")}";
                CuiElementContainer temp = new CuiElementContainer();
                
                temp.Add(new CuiElement()
                {
                    Parent = "Hud",
                    Name = $".{i}",
                    Components =
                    {
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "0 0" },
                        new CuiRawImageComponent { Png = (string) ImageLibrary?.Call("GetImage", image) }
                    }
                });
                
                CuiHelper.AddUi(player, temp);
                yield return new WaitForSeconds(1.0f);
                CuiHelper.DestroyUi(player, $".{i}");
            }
        }
        
        #endregion
        
        #region Language
        
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Lang_NoPermissions"] = "You don't have permission to use this command.",
                ["Lang_Title"] = "Underwear Selection",
                ["Lang_Apply"] = "Apply",
                ["Lang_Installed"] = "Installed",
                ["Lang_Enable"] = "On",
                ["Lang_Disable"] = "Off",
                ["Scribble"] = "Scribble",
                ["Gradient"] = "Gradient",
                ["PalmLeaves"] = "Palm Leaves",
                ["Bikini"] = "Pink Bikini",
                ["Rapido"] = "Rapido",
                ["Coconut"] = "Coconut",
                ["MummyWraps"] = "Mummy Wraps",
                ["Purple"] = "Purple",
                ["GrassSkirt"] = "Grass Skirt"
            }, this);
            
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Lang_NoPermissions"] = "У вас нет разрешения на использование этой команды.",
                ["Lang_Title"] = "Выбор нижнего белья",
                ["Lang_Apply"] = "Применить",
                ["Lang_Installed"] = "Установлен",
                ["Lang_Enable"] = "Вкл",
                ["Lang_Disable"] = "Выкл",
                ["Scribble"] = "Каракули",
                ["Gradient"] = "Градиент",
                ["PalmLeaves"] = "Пальмовые листья",
                ["Bikini"] = "Розовое бикини",
                ["Rapido"] = "Rapido",
                ["Coconut"] = "Кокос",
                ["MummyWraps"] = "Mummy Wraps",
                ["Purple"] = "Фиолетовое белье",
                ["GrassSkirt"] = "Травяная юбка"
            }, this, "ru");
        }
        
        #endregion
        
        #region Data
        
        private StoredData storedData;
        
        private class StoredData
        {
            public Dictionary<ulong, Data> PlayerData = new Dictionary<ulong, Data>();
        }
        
        private class Data
        {
            public uint id;
            public bool enable;
        }
        
        private void SaveData()
        {
            if (storedData != null)
            {
                Interface.Oxide.DataFileSystem.WriteObject($"{Name}_Data", storedData, true);
            }
        }
        
        private void LoadData()
        {
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>($"{Name}_Data");
            if (storedData == null)
            {
                storedData = new StoredData();
                SaveData();
            }
        }
        
        #endregion
    }
}
