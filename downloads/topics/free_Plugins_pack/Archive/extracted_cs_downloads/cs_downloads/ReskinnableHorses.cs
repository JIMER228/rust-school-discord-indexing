using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Newtonsoft.Json.Linq;

namespace Oxide.Plugins
{   
    [Info("ReskinnableHorses", "rustmods.ru", "1.2.0")]
    [Description("Change horse breed by using spraycan")]
    class ReskinnableHorses : RustPlugin
    {
        [PluginReference]
        private Plugin ImageLibrary;

        #region Config
        private ReskinnableHorsesConfig config;

        private class ReskinnableHorsesConfig : SerializableConfiguration
        {
            [JsonProperty("(1) Use Permission")]
            public string Use_Permission_Name;
            [JsonProperty("(2) Unlock all breeds permission")]
            public string Allow_Everything_Permission_Name;
            [JsonProperty("(3) Individual permissions (leave breed names intact)")]
            public Dictionary<string, string> Individual_Permissions;
            [JsonProperty("(4) Image URLs (leave breed names intact)")]
            public Dictionary<string, string> imageURLs;
            [JsonProperty("(5) CUI container name")]
            public string UI_Name;
            [JsonProperty("(6) Allow players to reskin horses they don't own")]
            public bool Allow_Reskinning_Not_Owned_Horses;
        }

        private ReskinnableHorsesConfig GetDefaultConfig()
        {
            return new ReskinnableHorsesConfig 
            {
                Use_Permission_Name = "reskinnablehorses.use",
                Allow_Everything_Permission_Name = "reskinnablehorses.all",
                Individual_Permissions = new Dictionary<string, string>()
                {
                    { "Appaloosa", "reskinnablehorses.appaloosa"},
                    { "Bay", "reskinnablehorses.bay"},
                    { "Bucksin", "reskinnablehorses.bucksin"},
                    { "Chestnut", "reskinnablehorses.chestnut"},
                    { "DappleGrey", "reskinnablehorses.dapplegrey"},
                    { "PieBald", "reskinnablehorses.piebald"},
                    { "Pinto", "reskinnablehorses.pinto"},
                    { "RedRoan", "reskinnablehorses.redroan"},
                    { "White", "reskinnablehorses.white"},
                    { "Black", "reskinnablehorses.black"}
                },
                imageURLs = new Dictionary<string, string>()
                {
                    { "Appaloosa", "https://cdn.discordapp.com/attachments/1099598272886214676/1156613266332721182/ftqPOJM.png?ex=65159b8b&is=65144a0b&hm=93dc1ca8e6963d2adcec280ccfe3be8e8a4840df43ed53bb1a7c81dff8942f55&"},
                    { "Bay", "https://cdn.discordapp.com/attachments/1099598272886214676/1156613325879263332/DHLJOxF.png?ex=65159b99&is=65144a19&hm=26bf6f63af2edaab4a502dcef977fd356883309ef6e8a097dd0d8ff1d382c86e&"},
                    { "Bucksin", "https://cdn.discordapp.com/attachments/1099598272886214676/1156613397031419944/bBKIkr8.png?ex=65159baa&is=65144a2a&hm=bed0ab4cc6e6f3697c9b8d6020221582e36263ff106d060cfd62eaf748387d86&"},
                    { "Chestnut", "https://cdn.discordapp.com/attachments/1099598272886214676/1156613450693345441/dKJ6QyL.png?ex=65159bb7&is=65144a37&hm=4b9dd19531baffd08765b044543b6fbae34f67c20720ecba96e8c7df8691386f&"},
                    { "DappleGrey", "https://cdn.discordapp.com/attachments/1099598272886214676/1156613499695403068/UeChlgX.png?ex=65159bc3&is=65144a43&hm=6f7ae6732ab6cf4064a0024ea7da85ac76a603596d2c44305f1096f2211e4cb3&"},
                    { "PieBald", "https://cdn.discordapp.com/attachments/1099598272886214676/1156613558847668285/ByxM6gU.png?ex=65159bd1&is=65144a51&hm=89b6df859b26ffaa35fa1dc64502bcb3740331cc581aa974396139a172102960&"},
                    { "Pinto", "https://cdn.discordapp.com/attachments/1099598272886214676/1156613642700206161/62faazU.png?ex=65159be5&is=65144a65&hm=69f4e64d384f194d82cdef7d74c6f19e25f6d98c83e9fb0be56ed3a2b666d6e1&"},
                    { "RedRoan", "https://cdn.discordapp.com/attachments/1099598272886214676/1156613793737089136/d7YPDg8.png?ex=65159c09&is=65144a89&hm=9525b1672cd5e9ee50eebb76a3956f026dad40e8cc127ec5f6c3b616bcda7e18&"},
                    { "White", "https://cdn.discordapp.com/attachments/1099598272886214676/1156613842244227132/cNPx1lf.png?ex=65159c14&is=65144a94&hm=b6e5f2db05bb9e988f0a8b08028618189af56735c39b82f40d54b964c49f9f47&"},
                    { "Black", "https://cdn.discordapp.com/attachments/1099598272886214676/1156613896027766904/zdGJREe.png?ex=65159c21&is=65144aa1&hm=dead0f822eb32f0a13c88f617c716cd379c0837be1e7e4c4a689b7f949614d8d&"}
                },
                UI_Name = "reskinnablehorses",
                Allow_Reskinning_Not_Owned_Horses = true
            };
        }
        #endregion

        #region Configuration Boilerplate
        private class SerializableConfiguration
        {
            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonHelper.Deserialize(ToJson()) as Dictionary<string, object>;
        }

        private static class JsonHelper
        {
            public static object Deserialize(string json) => ToObject(JToken.Parse(json));

            private static object ToObject(JToken token)
            {
                switch (token.Type)
                {
                    case JTokenType.Object:
                        return token.Children<JProperty>()
                                    .ToDictionary(prop => prop.Name,
                                                  prop => ToObject(prop.Value));

                    case JTokenType.Array:
                        return token.Select(ToObject).ToList();

                    default:
                        return ((JValue)token).Value;
                }
            }
        }

        private bool MaybeUpdateConfig(SerializableConfiguration config)
        {
            var currentWithDefaults = config.ToDictionary();
            var currentRaw = Config.ToDictionary(x => x.Key, x => x.Value);
            return MaybeUpdateConfigDict(currentWithDefaults, currentRaw);
        }

        private bool MaybeUpdateConfigDict(Dictionary<string, object> currentWithDefaults, Dictionary<string, object> currentRaw)
        {
            bool changed = false;

            foreach (var key in currentWithDefaults.Keys)
            {
                object currentRawValue;
                if (currentRaw.TryGetValue(key, out currentRawValue))
                {
                    var defaultDictValue = currentWithDefaults[key] as Dictionary<string, object>;
                    var currentDictValue = currentRawValue as Dictionary<string, object>;

                    if (defaultDictValue != null)
                    {
                        if (currentDictValue == null)
                        {
                            currentRaw[key] = currentWithDefaults[key];
                            changed = true;
                        }
                        else if (MaybeUpdateConfigDict(defaultDictValue, currentDictValue))
                            changed = true;
                    }
                }
                else
                {
                    currentRaw[key] = currentWithDefaults[key];
                    changed = true;
                }
            }

            return changed;
        }

        protected override void LoadDefaultConfig() => config = GetDefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<ReskinnableHorsesConfig>();
                if (config == null)
                {
                    throw new JsonException();
                }

                if (MaybeUpdateConfig(config))
                {
                    PrintWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch (Exception e)
            {
                PrintError(e.Message);
                PrintWarning($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Puts($"Configuration changes saved to {Name}.json");
            Config.WriteObject(config, true);
        }
        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NotOwned"] = "You don't own that horse",
            }, this, "en");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NotOwned"] = "Вы не владеете этой лошадью",
            }, this, "ru");
        }

        #endregion

        #region Hooks
        void Init()
        {
            permission.RegisterPermission(config.Use_Permission_Name, this);
            permission.RegisterPermission(config.Allow_Everything_Permission_Name, this);
            foreach (var perm in config.Individual_Permissions.Values)
            {
                permission.RegisterPermission(perm, this);
            }
        }

        void Unload()
        {
            foreach(var player in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(player, config.UI_Name);
        }

        void OnServerInitialized()
        {
            if(ImageLibrary == null)
            {
                PrintError("ImageLibrary is not installed!");
                return;
            }

            foreach (var image in config.imageURLs.Keys)
            {
                ImageLibrary.Call("AddImage", config.imageURLs[image], image);
            }
        }

        void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (player == null || input == null) return;
            if (!input.WasJustReleased(BUTTON.FIRE_SECONDARY)) return;
            if (!(player.GetHeldEntity() is SprayCan)) return;

            var horse = GetHorse(player);
            if (horse == null) return;
            ShowReskinningUI(player, horse);
        }
        #endregion

        #region Methods
        bool UseSpray(BasePlayer player, BaseNetworkable target_ent)
        {
            if (player == null || target_ent == null) return false;

            var ent = player.GetHeldEntity();
            if (ent == null || !(ent is SprayCan)) return false;
            var spraycan = ent as SprayCan;
            spraycan.ClientRPC(null, "Client_ReskinResult", 1, target_ent.net.ID);
            var ownerItem = player.GetActiveItem();
            if (ownerItem == null) return false;
            ownerItem.LoseCondition(spraycan.ConditionLossPerReskin);
            spraycan.SetFlag(BaseEntity.Flags.Busy, true, false, true);
            spraycan.Invoke(new Action(spraycan.ClearBusy), spraycan.SprayCooldown);

            return true;
        }

        void ApplyBreed(BasePlayer player, RidableHorse horse, int breed)
        {
            if(player == null || horse == null || breed < 0 || breed > 9) return;
            if (breed == horse.currentBreed) return;
            if (!permission.UserHasPermission(player.UserIDString, config.Individual_Permissions[horse.breeds[breed].name]) &&
                !permission.UserHasPermission(player.UserIDString, config.Allow_Everything_Permission_Name)) return;
            if(horse.OwnerID != 0 && horse.OwnerID != player.userID && !config.Allow_Reskinning_Not_Owned_Horses)
            {
                PrintToChat(player, lang.GetMessage("NotOwned", this, player.UserIDString));
                return;
            }
            if (!UseSpray(player, horse)) return;
            horse.ApplyBreed(breed);
            horse.SendNetworkUpdate();
        }
        RidableHorse GetHorse(BasePlayer player)
        {
            if (player == null) return null;

            RaycastHit hit;
            if (Physics.Raycast(player.eyes.position, player.eyes.HeadForward(), out hit, 3f))
            {
                var ent = hit.GetEntity();
                if (ent is RidableHorse)
                {
                    return (RidableHorse)ent;
                }
            }

            return null;
        }
        #endregion

        #region UI
        void ShowReskinningUI(BasePlayer player, RidableHorse horse)
        {
            if (player == null || horse == null) return;
            if (!permission.UserHasPermission(player.UserIDString, config.Use_Permission_Name)) return;

            var hasAllPerm = permission.UserHasPermission(player.UserIDString, config.Allow_Everything_Permission_Name);

            var elements = new CuiElementContainer();

            elements.Add(new CuiElement
            {
                Name = config.UI_Name,
                Parent = "Overlay",
                Components =
                {
                    new CuiNeedsCursorComponent(),
                    new CuiButtonComponent
                    {
                        Close = config.UI_Name,
                        Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
                        Sprite = "assets/content/ui/ui.background.transparent.radial.psd",
                        Color = "0.4 0.4 0.4 1"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    }
                }
            });

            elements.Add(new CuiElement
            {
                Name = $"{config.UI_Name}_Header",
                Parent = config.UI_Name,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = "Select Skin",
                        Color = "0.776 0.74 0.708 1",
                        FontSize = 50,
                        Align = TextAnchor.UpperLeft
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = "50 -550",
                        OffsetMax = "550 -40"
                    }
                }
            });

            elements.Add(new CuiElement
            {
                Name = $"{config.UI_Name}_Panel",
                Parent = config.UI_Name,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0.8 0.8 0.8 0.3"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.195 0.222",
                        AnchorMax = "0.807 0.787"
                    }
                }
            });

            elements.Add(new CuiElement
            {
                Name = $"{config.UI_Name}_Panel_Label",
                Parent = $"{config.UI_Name}_Panel",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = "SKINS",
                        Color = "0.95 0.95 0.95 0.9",
                        FontSize = 30
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.01 0",
                        AnchorMax = "1 0.98"
                    }
                }
            });

            var i = -1;
            var current_breed = -1;


            foreach(var breed in horse.breeds) 
            {
                current_breed++;
                if (!permission.UserHasPermission(player.UserIDString, config.Individual_Permissions[breed.name]) && !hasAllPerm)
                    continue;
                i++;

                var btnelement = new CuiElement
                {
                    Name = $"{config.UI_Name}_Panel_Button",
                    Parent = $"{config.UI_Name}_Panel",
                    Components =
                    {
                        new CuiButtonComponent
                        {
                            Color = "0 0 0 0",
                            Command = $"reskinnablehorses.reskin {current_breed}"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = $"{0.18 * (i < 5 ? i : i - 5) + 0.07} {(i < 5 ? 0.58 : 0.18) - 0.02}",
                            AnchorMax = $"{0.18 * (i < 5 ? i + 1 : i - 4) + 0.05} {(i < 5 ? 0.9 : 0.5) - 0.02}"
                        }
                    }
                };

                var imageID = (string)ImageLibrary.Call("GetImage", breed.name);

                var imageelement = new CuiElement
                {
                    Name = $"{config.UI_Name}_Panel_Button_image",
                    Parent = $"{config.UI_Name}_Panel_Button",
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = imageID
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0.1",
                            AnchorMax = "1 1"
                        }
                    }
                };

                var textelement = new CuiElement
                {
                    Name = $"{config.UI_Name}_Panel_Button_text",
                    Parent = $"{config.UI_Name}_Panel_Button",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = breed.breedName.english,
                            FontSize = 20
                            ,
                            Align = TextAnchor.LowerCenter,
                            Font = "robotocondensed-regular.ttf",
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1"
                        }
                    }
                };

                elements.Add(btnelement);
                elements.Add(imageelement);
                elements.Add(textelement);
            }

            CuiHelper.DestroyUi(player, config.UI_Name);
            CuiHelper.AddUi(player, elements);
        }

        #endregion

        #region Commands
        [ConsoleCommand("reskinnablehorses.reskin")]
        private void ccmdreskinCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !arg.HasArgs(1)) return;
            if (!permission.UserHasPermission(player.UserIDString, config.Use_Permission_Name)) return;

            var horse = GetHorse(player);
            if (horse == null) return;

            if (horse.OwnerID != 0 && horse.OwnerID != player.userID && !config.Allow_Reskinning_Not_Owned_Horses) return;
            CuiHelper.DestroyUi(player, config.UI_Name);

            try
            {
                var breed = int.Parse(arg.Args[0]);
                ApplyBreed(player, horse, breed);
            }
            catch (Exception)
            {
                return;
            }
        }
        #endregion
    }
}