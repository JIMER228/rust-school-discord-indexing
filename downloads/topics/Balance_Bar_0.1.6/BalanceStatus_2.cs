using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Balance Status", "YaMang -w-", "1.0.4")]
    [Description("YaMang-w-")]
    internal class BalanceStatus : RustPlugin
    {
        private readonly string _HidePerm = $"BalanceStatus.hide";
#pragma warning disable CS0649 // 필드에는 할당되지 않으므로 항상 기본값을 사용합니다.
        [PluginReference] private Plugin SimpleStatus, ImageLibrary, Economics, ServerRewards;
#pragma warning restore CS0649 // 필드에는 할당되지 않으므로 항상 기본값을 사용합니다.
        private List<string> statusIds = new List<string>();
        #region Hook

        void OnServerInitialized()
        {
            if (ServerRewards == null)
            {
                _config.balanceSettings.balances["ServerRewards"].Enable = false;
                PrintWarning("ServerRewards can't be enabled !");
            }
            if (Economics == null)
            {
                _config.balanceSettings.balances["Economics"].Enable = false;
                PrintWarning("Economics can't be enabled !");
            }
            permission.RegisterPermission(_HidePerm, this);
            SaveConfig();

            foreach (var balance in _config.balanceSettings.balances)
            {
                if(!balance.Value.Enable) continue;

                string image = "";
                if(!string.IsNullOrEmpty(balance.Value.Image))
                {
                    if (!balance.Value.Image.StartsWith("item"))
                    {
                        ImageLibrary.Call<bool>("AddImage", balance.Value.Image, $"BS_{balance.Value.Title}", 0UL);
                        image = $"BS_{balance.Value.Title}";
                    }
                    else 
                        image = balance.Value.Image;
                }

                SimpleStatus.CallHook("CreateStatus", this, balance.Key, new Dictionary<string, object>
                {
                    ["color"] = HexToColor(balance.Value.BackgroundColor),
                    ["title"] = balance.Value.Title,
                    ["titleColor"] = HexToColor(balance.Value.TitleColor),
                    ["text"] = "0",
                    ["textColor"] = HexToColor(balance.Value.TextColor),
                    ["icon"] = image,
                    ["iconColor"] = "1 1 1 1",
                    ["rank"] = balance.Value.Rank
                });

                statusIds.Add(balance.Key);
            }

            foreach (var player in BasePlayer.activePlayerList)
            {
                if (permission.UserHasPermission(player.UserIDString, _HidePerm)) continue;
                foreach (var status in statusIds)
                {
                    SetStatus(player.UserIDString, status);
                }
            }

            

            timer.Every(_config.balanceSettings.RefreshTime, UpdateBalance);
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, _HidePerm)) return;

            foreach (var status in statusIds)
            {
                SetStatus(player.UserIDString, status);
            }

        }

        private void OnPlayerDiscconnected(BasePlayer player)
        {
            foreach (var status in statusIds)
            {
                SetStatus(player.UserIDString, status, 0);
            }
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                foreach (var status in statusIds)
                {
                    SetStatus(player.UserIDString, status, 0);
                }
            }
        }
        #endregion

        #region Commands
        [ChatCommand("bs")]
        private void BSCMD(BasePlayer player)
        {
            if(!permission.UserHasPermission(player.UserIDString, _HidePerm))
            {
                permission.GrantUserPermission(player.UserIDString, _HidePerm, this);
                PrintToChat(player, Lang("BS_Hide"));
            }
            else
            {
                permission.RevokeUserPermission(player.UserIDString, _HidePerm);
                PrintToChat(player, Lang("BS_Show"));
            }
        }
        #endregion

        #region Helper
        private void UpdateBalance()
        {
            if (statusIds.Count == 0) return;
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (permission.UserHasPermission(player.UserIDString, _HidePerm)) continue;
                foreach (var status in statusIds)
                {
                    object amount = GetBalances(player, status);

                    switch (status)
                    {
                        case "ServerRewards":
                            SetStatusProperty(player.UserIDString, status, new Dictionary<string, object>
                            {
                                ["text"] = $"{Lang("BS_ServerRewards", amount)}"
                            });
                            break;
                        case "Economics":
                            SetStatusProperty(player.UserIDString, status, new Dictionary<string, object>
                            {
                                ["text"] = $"{Lang("BS_Economics", amount)}"
                            });
                            break;
                        case "Scrap":
                            SetStatusProperty(player.UserIDString, status, new Dictionary<string, object>
                            {
                                ["text"] = $"{Lang("BS_Scrap", amount)}"
                            });
                            break;
                        case "CustomItem":
                            SetStatusProperty(player.UserIDString, status, new Dictionary<string, object>
                            {
                                ["text"] = $"{Lang("BS_CustomItem", amount)}"
                            });
                            break;

                    }
                }
            }
        }

        private object GetBalances(BasePlayer player, string status)
        {
            object amount = null;
            if (status == "Economics")
                amount = Economics.Call("Balance", player.UserIDString);
            else if (status == "ServerRewards")
                amount = ServerRewards.Call("CheckPoints", player.UserIDString);
            else if (status == "Scrap")
            {
                var items = Pool.Get<List<Item>>();
                player.inventory.GetAllItems(items);
                int scraps = 0;
                
                if (items.Count() == 0)
                    return 0;

                var s = items.Where(x => x.info.shortname == "scrap");
                foreach (var item in s)
                {
                    scraps += item.amount;
                }
                Pool.Free(ref items);
                amount = scraps;
            }
            //else if (status == "CustomItem")
            //{
            //    var items = Pool.Get<List<Item>>();
            //    player.inventory.GetAllItems(items);
            //    int customitem = 0;

            //    if (items.Count() == 0)
            //        return 0;

            //    var config = _config.balanceSettings.balances["CustomItem"];

            //    string[] splitString = config.Image.Substring(9).Split('-');
            //    string shortname = splitString[0].ToString();
            //    string skinid = splitString[1].ToString();
            //    var s = items.Where(x => x.info.shortname == shortname && x.skin == Convert.ToUInt64(skinid));
            //    foreach (var item in s)
            //    {
            //        customitem += item.amount;
            //    }
            //    Pool.Free(ref items);
            //    amount = customitem;
            //}
            return amount;
        }
        
        //MEVENT
        private static string HexToColor(string HEX, float Alpha = 100)
        {
            if (string.IsNullOrEmpty(HEX)) HEX = "#FFFFFF";

            var str = HEX.Trim('#');
            if (str.Length != 6) throw new Exception(HEX);
            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

            return $"{(double)r / 255} {(double)g / 255} {(double)b / 255} {Alpha / 100f}";
        }
        

        #endregion
        
        #region Config        
        private ConfigData _config;
        private class ConfigData
        {
            [JsonProperty(PropertyName = "Balance Settings")] public BalanceSettings balanceSettings { get; set; }
            public Core.VersionNumber Version { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<ConfigData>();

            if (_config.Version < Version)
                UpdateConfigValues();

            Config.WriteObject(_config, true);
        }

        protected override void LoadDefaultConfig() => _config = GetBaseConfig();

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                balanceSettings = new BalanceSettings()
                {
                    RefreshTime = 10f,
                    balances = new Dictionary<string, Balances>()
                    {
                        {
                            "Economics", new Balances()
                            {
                                Enable = true,
                                BackgroundColor = "4CAF50",
                                Title = "Economics",
                                TitleColor = "FFFF00",
                                Image = "https://i.imgur.com/jyTe69j.png",
                                ImageColor = "FFFF00",
                                Rank = 0
                            }
                        },
                        {
                            "ServerRewards", new Balances()
                            {
                                Enable = true,
                                BackgroundColor = "FFD700",
                                Title = "ServerRewards",
                                TitleColor = "FFFF00",
                                Image = "https://i.imgur.com/jyTe69j.png",
                                ImageColor = "FFFF00",
                                Rank = 0
                            }
                        },
                        {
                            "Scrap", new Balances()
                            {
                                Enable = true,
                                BackgroundColor = "FFD700",
                                Title = "Scraps",
                                TitleColor = "FFFF00",
                                Image = "itemid:-932201673",
                                ImageColor = "FFFFFF",
                                Rank = 0
                            }
                        }
                        //{
                        //    "CustomItem", new Balances()
                        //    {
                        //        Enable = false,
                        //        BackgroundColor = "FFD700",
                        //        Title = "Test",
                        //        TitleColor = "FFFF00",
                        //        Image = "itemskin:rifle.ak-3339500254",
                        //        ImageColor = "FFFFFF",
                        //        Rank = 0
                        //    }
                        //}
                    }
                },
                Version = Version
            };
        }

        public class BalanceSettings
        {
            public float RefreshTime { get; set; }
            public Dictionary<string, Balances> balances { get; set; }
        }

        public class Balances
        {
            public bool Enable { get; set; }
            public string BackgroundColor { get; set; }
            public string Title { get; set; }
            public string TitleColor { get; set; }
            public string TextColor { get; set; }
            public string Image { get; set; }
            public string ImageColor { get; set; }
            public int Rank { get; set; }
        }

        protected override void SaveConfig() => Config.WriteObject(_config, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");
            _config.Version = Version;
            PrintWarning("Config update completed!");
        }

        #endregion

        #region Lang

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "BS_Economics", "{0} Eco" },
                { "BS_Scrap", "{0} Scraps" },
                { "BS_CustomItem", "{0}" },
                { "BS_ServerRewards", "{0} RP" },
                { "BS_Hide", "Hide Balance Status" },
                { "BS_Show", "Show Balance Status" }

            }, this);
        }

        private string Lang(string key, params object[] args)
        {
            return string.Format(lang.GetMessage(key, this), args);
        }
        #endregion
        
        #region Simple Status 
        private void CreateStatus(string statusid, string backgroundColor = "1 1 1 1", string title = "Text", string titleColor = "1 1 1 1", string text = null, string textColor = "1 1 1 1", string imageName = null, string imageColor = "1 1 1 1") => SimpleStatus?.CallHook("CreateStatus", this, statusid, backgroundColor, title, titleColor, text, textColor, imageName, imageColor);
        private void SetStatus(string userId, string statusId, int duration = int.MaxValue, bool pauseOffline = false) => SimpleStatus?.CallHook("SetStatus", userId, statusId, duration, pauseOffline);
        private void SetStatusTitle(string userId, string statusId, string title = null) => SimpleStatus?.CallHook("SetStatusTitle", userId, statusId, title);
        private void SetStatusProperty(string userId, string statusId, Dictionary<string, object> properties) => SimpleStatus?.CallHook("SetStatusProperty", userId, statusId, properties);
        private int GetDuration(string userId, string statusId) => (int)SimpleStatus?.CallHook("GetDuration", userId, statusId);
        #endregion
    }
} 