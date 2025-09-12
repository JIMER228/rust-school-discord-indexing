using System;
using Newtonsoft.Json;
using System.Linq;
		   		 		  						  	   		  	  			  	   		  	  			  				
namespace Oxide.Plugins
{
    [Info("RecyclerEfficiency", "Mercury", "1.0.1")]
    [Description("RecyclerEfficiency")]
    public class RecyclerEfficiency : RustPlugin
    {

        protected override void LoadDefaultConfig() => config = Configuration.GetNewConfiguration();
        
        
        private static Configuration config = new Configuration();
        
        private void OnEntitySpawned(Recycler recycler)
        {
            if (recycler == null)
                return;

            if (recycler.OwnerID != 0) return;
		   		 		  						  	   		  	  			  	   		  	  			  				
            SetPresetRecycler(recycler, true);
        }
        
        void OnRecyclerToggle(Recycler recycler, BasePlayer player)
        {
            if (recycler.OwnerID != 0) return;
            
            if (!recycler.IsOn())
            {
                NextTick(() =>
                {
                    if (!recycler.IsOn())
                        return;
                    
                    Int32 recyclerSpeed = config.GetSpeed(recycler.IsSafezoneRecycler());
                    recycler.InvokeRepeating(recycler.RecycleThink, recyclerSpeed, recyclerSpeed);
                });
            }
        }
        protected override void SaveConfig() => Config.WriteObject(config);

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();
            }
            catch
            {
                PrintWarning("Ошибка #385663" + $"чтения конфигурации 'oxide/config/{Name}', создаём новую конфигурацию!!");
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }

        private void Unload() => UpdateRecycler(true);
        private class Configuration
        {
            [JsonProperty(LanguageEn ? "Recycler Configuration at Monuments" : "Настройка переработчиков на монументах")]
            public RecyclerPreset monumentRecyclers;

            public static Configuration GetNewConfiguration()
            {
                return new Configuration
                {
                    safeZoneRecyclers = new RecyclerPreset
                    {
                        SpeedSecods = 8,
                        Efficiency = 40
                    },
                    monumentRecyclers = new RecyclerPreset
                    {
                        SpeedSecods = 5,
                        Efficiency = 60
                    }
                };
            }
            
            public Int32 GetSpeed(Boolean isSafeZone)
            {
                RecyclerPreset preset = isSafeZone ? safeZoneRecyclers : monumentRecyclers;
                return preset.SpeedSecods;
            }
            internal class RecyclerPreset
            {
                [JsonProperty(LanguageEn ? "Recycling Speed (in seconds)" : "Скорость переработки (в секундах)")]
                public Int32 SpeedSecods;
                [JsonProperty(LanguageEn ? "Recycler Efficiency (from 0 to 100)" : "Эффективность переработчика (от 0 до 100)")]
                public Int32 Efficiency;
            }

            public Single GetEfficiency(Boolean isSafeZone)
            {
                RecyclerPreset preset = isSafeZone ? safeZoneRecyclers : monumentRecyclers;
                return preset.Efficiency <= 0 ? 0.1f :
                    preset.Efficiency > 100 ? 1.0f : preset.Efficiency / 100.0f;
            }
            [JsonProperty(LanguageEn ? "Recycler Configuration from Safe Zone" : "Настройка переработчиков из безопасной зоны")]
            public RecyclerPreset safeZoneRecyclers;
        }

        
        
        private void Init() => Unsubscribe(nameof(OnEntitySpawned));

        private void SetDefaultPresets(Recycler recycler)
        {
            recycler.recycleEfficiency = 0.6f;
            recycler.safezoneRecycleEfficiency = 0.4f;
            recycler.radtownRecycleEfficiency = 0.6f;
        }

        
        
        private void UpdateRecycler(Boolean isDefault = false)
        {
            foreach(Recycler recycler in BaseNetworkable.serverEntities.entityList.Get().Values.Where(x => x != null && x is Recycler))
            {
                if(recycler.OwnerID != 0) continue;
                if (isDefault)
                    SetDefaultPresets(recycler);
                else SetPresetRecycler(recycler);
            }
        }
        private Int32 countRecyclerMonument = 0;
        
        private void OnServerInitialized()
        {
            UpdateRecycler();
            Puts($"Setup preset recycler :\nSafeZone recyclers : {countRecyclerSafeZone}\nSpeed - {config.safeZoneRecyclers.SpeedSecods}\nEfficiency - {config.safeZoneRecyclers.Efficiency}\n\nMonuments recyclers : {countRecyclerMonument}\nSpeed - {config.monumentRecyclers.SpeedSecods}\nEfficiency - {config.monumentRecyclers.Efficiency}");
            Subscribe(nameof(OnEntitySpawned));
        }
        private Int32 countRecyclerSafeZone = 0;
        private const Boolean LanguageEn = false;
        private void SetPresetRecycler(Recycler recycler, Boolean isSpawned = false)
        {
            Boolean isSafeZone = recycler.IsSafezoneRecycler();
            if (isSafeZone)
                countRecyclerSafeZone++;
            else countRecyclerMonument++;
            
            Single efficiency = config.GetEfficiency(isSafeZone);

            recycler.recycleEfficiency = efficiency;
            recycler.safezoneRecycleEfficiency = efficiency;
            recycler.radtownRecycleEfficiency = efficiency;
            
            if(isSpawned)
            {
                String typeRecycler = isSafeZone ? "safe zone" : "monument";
                Puts($"Finded spawned new recycler, setuped preset in {typeRecycler}");
            }
        }

            }
}
