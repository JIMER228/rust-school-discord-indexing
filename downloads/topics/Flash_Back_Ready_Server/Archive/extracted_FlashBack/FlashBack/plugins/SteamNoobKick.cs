using System;
using Oxide.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Oxide.Plugins
{
    [Info("SteamNoobKick", "r3dapple", "1.0.0")]
	
	class SteamNoobKick : RustPlugin
    {
        void OnPlayerConnected(BasePlayer player)
        {
			webrequest.EnqueueGet($"http://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/?key=CF09B8FC6C9006E7C75679FFA95D4BE7&steamids={player.UserIDString}", (code, reply) =>
			{
				var respond = JObject.Parse(reply);
				long unixtime = Convert.ToInt64(respond["response"]["players"][0]["timecreated"].ToString());
				double hourssinceregistered = (DateTime.Now - ToDateTime(unixtime)).TotalHours;
				if (hourssinceregistered < config.Hours) player.Kick($"Для игры на сервере Вашему аккаунту должно быть не менее {config.Hours.ToString()} ч.");
			}, this);
        }
		
		private DateTime ToDateTime(long UnixTime)
		{
			DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0);
			return origin.AddSeconds(UnixTime);
		}
		
		private Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Сколько часов должно пройти с регистрации Steam аккаунта игрока, чтобы он смог подключиться к серверу")]
            public int Hours { get; set; } = 24;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Возникла ошибка при чтении конфига, он был пересоздан");
            config = new Configuration();
        }

        protected override void SaveConfig() => Config.WriteObject(config);
	}
}