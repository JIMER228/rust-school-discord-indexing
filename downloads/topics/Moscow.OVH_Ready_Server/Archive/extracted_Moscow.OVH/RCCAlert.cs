using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries;

namespace Oxide.Plugins
{
    [Info("RCC Alert", "Hougan", "0.0.1")]
    public class RCCAlert : RustPlugin
    {
        #region Classes

        private class Configuration
        {
            [JsonProperty("Discord WebHook URL")]
            public string DiscordHook = "https://discord.com/api/webhooks/539944300902088734/eEbJJdRcE5XWTeymOVbVMgVnpryt3svndDvOx-9e69k_EqaaoQIKIYhp96k9yxr-BH40";

            [JsonProperty("RCC Api Key")]
            public string RccApiKey = "fa829e09703eaf0674d2a1147d2e88ec";
        }

        private class BanInfo
        {
            public int banID;
            public string reason;
            public string serverName;
            public int OVHserverID;
            public long banDate;
            public long unbanDate;
        }

        #endregion

        #region Varaibles

        private static Dictionary<ulong, int> Checks = new Dictionary<ulong, int>();
        private static Configuration Settings = new Configuration();

        #endregion

        #region Hooks

        private void OnServerInitialized()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile(Name))
            {
                Checks = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, int>>(Name);
            }
            
            CheckUser("Хуган", 76561198307286954);

            timer.Every(60, SaveData);
        }
 
        private void OnPlayerConnected(BasePlayer player)
        {
            CheckUser(player);
        }  

        private void Unload() => SaveData();
        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, Checks);

        #endregion

        #region Methods

        private void CheckUser(BasePlayer player)
        {
            CheckUser(player.displayName, player.userID);
        }

        private void CheckUser(string name, ulong userId)
        {
            try
            {
                PrintWarning(userId.ToString()); 
                webrequest.Enqueue($"https://rustcheatcheck.ru/panel/api?action=getInfo&key={Settings.RccApiKey}&player={userId}", "",(c, r) =>
                {
                    try
                    {
                        var result = JsonConvert.DeserializeObject(r);
                        //PrintError(r);
                        var obj = result as JObject;

                        var bans = JsonConvert.DeserializeObject<List<BanInfo>>(obj["bans"].ToString());
                        if (bans == null)
                        {
                            return;
                        }

                        ApplyChanges(name, userId, bans);
                    }
                    catch
                    {
                        
                    }
                }, this, RequestMethod.GET);
            }
            catch
            {
                
            }
        }

        private void ApplyChanges(string name, ulong userId, List<BanInfo> bans)
        {
            if (!Checks.ContainsKey(userId))
            {
                Checks.Add(userId, -1);
            }

            if (Checks[userId] != bans.Count && bans.Count != 0)
            {
                NotifyOwners(name, userId, bans);
            }
            
            Checks[userId] = bans.Count;
        }

        private void NotifyOwners(string name, ulong userId, List<BanInfo> bans)
        {
            
            var newList = new List<Fields>();
                             
            //newList.Add(new Fields($"Зашёл игрок {name}",$"{userId}", false));
            
            bans.ForEach(v =>
            {
                var date = new DateTime(1970, 1, 1, 0, 0, 0, 0, 0);
                date = date.AddSeconds(v.banDate);

                var time = $"{date.ToShortDateString()} {date.ToShortTimeString()}";
                
                newList.Add(new Fields($"<a:br_rock:757224248916967456> {v.serverName}", $"{v.reason}\n`{time}`", true));
            });
                    
            FancyMessage apiMessage = new FancyMessage($"@everyone", false, new FancyMessage.Embeds[1] { new FancyMessage.Embeds($"Зашёл игрок {name} - {userId} его баны:", 3092790, newList) });
            Request(Settings.DiscordHook, apiMessage.toJSON());
        }

        #endregion
        
        #region Utils
        
        public class FancyMessage
        {
            public string content { get; set; }
            public bool tts { get; set; }
            public Embeds[] embeds { get; set; }

            public class Embeds
            {
                public string title { get; set; }
                public int color { get; set; }
                public List<Fields> fields { get; set; }
                public Embeds(string title, int color, List<Fields> fields)
                {
                    this.title = title;
                    this.color = color;
                    this.fields = fields;
                }
            } 

            public FancyMessage(string content, bool tts, Embeds[] embeds)
            {
                this.content = content;
                this.tts = tts;
                this.embeds = embeds;
            }

            public string toJSON() => JsonConvert.SerializeObject(this);
        }
        public class Fields
        {
            public string name { get; set; }
            public string value { get; set; }
            public bool inline { get; set; }
            public Fields(string name, string value, bool inline)
            {
                this.name = name;
                this.value = value;
                this.inline = inline;
            }
        }
        
        private void Request(string url, string payload, Action<int> callback = null)
        {
            webrequest.Enqueue(url, payload, (code, response) =>
                {
                    if (code != 200 && code != 204)
                    {
                        if (response != null)
                        {
                            try
                            {
                                JObject json = JObject.Parse(response);
                                if (code == 429)
                                {
                                    float seconds = float.Parse(Math.Ceiling((double)(int)json["retry_after"] / 1000).ToString());
                                }
                                else
                                {
                                    PrintWarning($"Discord rejected that payload! Responded with \"{json["message"].ToString()}\" Code: {code}");
                                }
                            }
                            catch
                            {
                                PrintWarning($"Failed to get a valid response from discord! Error: \"{response}\" Code: {code}");
                            }
                        }
                        else
                        {
                            PrintWarning($"Discord didn't respond (down?) Code: {code}");
                        }
                    }
                    try
                    {
                        callback?.Invoke(code);
                    }
                    catch (Exception ex)
                    {
                        Interface.Oxide.LogException("[DiscordMessages] Request callback raised an exception!", ex);
                    }
                }, this, Core.Libraries.RequestMethod.POST, new Dictionary<string, string> { ["Content-Type"] =  "application/json"});
        }

        private double CurrentTime() => DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;  

        #endregion
    }
}