using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("AutomaticWipeNotification", "Ryamkk", "1.0.0")]
    public class AutomaticWipeNotification : RustPlugin
    {
        private const string WebHook = "https://discord.com/api/webhooks/1036720349678686268/Oxmo9ler19tOWGbNHBdoHhgLfP3E344J5_RMVzz0QBpAnaREDTZuNIt7_OKqoSLo3PgE";
        
        [ConsoleCommand("awn")]
        void Test(ConsoleSystem.Arg arg)
        {
            string message = "";
            foreach (var check in arg.Args)
                message += $" {check}";
            
            List<Fields> fields = new List<Fields>
            {
                new Fields("Сообщение: ", message, true)
            };
            
            FancyMessage newMessage = new FancyMessage(null, false, new FancyMessage.Embeds[1]
            {
                new FancyMessage.Embeds("Новое сообщение!", 14761010, fields)
            });
            
            Request(WebHook, newMessage.toJSON());
        }
        
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
                }, this, Core.Libraries.RequestMethod.POST);
        }
    }
}