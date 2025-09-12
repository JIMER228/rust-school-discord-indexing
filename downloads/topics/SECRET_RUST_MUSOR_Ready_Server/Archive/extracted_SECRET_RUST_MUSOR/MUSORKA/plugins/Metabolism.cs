using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Metabolism", "walkinrey", "1.0.1")]
    class Metabolism : RustPlugin
    {
        #region Переменные
        private Configuration config;
        private string[] permissions = {"metabolism.food_spawn", "metabolism.water_spawn", "metabolism.food_always", "metabolism.water_always"};
        #endregion
        #region Конфиг
        private class Configuration
        {
            [JsonProperty("Сколько еды восполнять?")] public float FoodSetFloat = 500f;
            [JsonProperty("Сколько воды восполнять?")] public float WaterSetFloat = 250f;
        }
        protected override void LoadDefaultConfig() => config = new Configuration();
        protected override void SaveConfig() => Config.WriteObject(config);
        protected override void LoadConfig()
        {
            base.LoadConfig(); 
            try 
            {
                config = Config.ReadObject<Configuration>();
            } 
            catch 
            {
                LoadDefaultConfig();
            }
            NextTick(SaveConfig);
        }
        #endregion
        #region Хуки
        void Loaded()
        {
            foreach(var perm in permissions) permission.RegisterPermission(perm, this);
        }
        object OnRunPlayerMetabolism(PlayerMetabolism metabolism, BasePlayer player, float delta)
        {
            if(player.IPlayer.HasPermission("metabolism.food_always")) metabolism.calories.value = 500f;
            if(player.IPlayer.HasPermission("metabolism.water_always")) metabolism.hydration.value = 250f; 
            return null;    
        }
        void OnPlayerRespawned(BasePlayer player)
        {
            PlayerMetabolism metabolism = player.metabolism;
            if(player.IPlayer.HasPermission("metabolism.food_spawn")) metabolism.calories.value = config.FoodSetFloat;
            if(player.IPlayer.HasPermission("metabolism.water_spawn")) metabolism.hydration.value = config.WaterSetFloat;
        }
        #endregion
    }
}