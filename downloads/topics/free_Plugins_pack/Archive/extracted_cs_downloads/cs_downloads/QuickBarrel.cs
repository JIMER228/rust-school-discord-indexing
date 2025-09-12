using System;
using Object = System.Object;
using Oxide.Core.Plugins;
using UnityEngine;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("QuickBarrel", "Mercury", "1.2.0")]
    [Description("QuickBarrel")]
    public class QuickBarrel : RustPlugin
    {
        protected override void SaveConfig() => Config.WriteObject(config);
        
        
                
        private void OnPluginLoaded(Plugin plugin)
        {
            if (plugin == null)
                return;
            
            NextTick(ToggleHooks);
        }
        public class Configuration
        {
            [JsonProperty("Допустимая дистанция между игроком и бочкой, для работы функций плагина")]
            public Single DistanceUse;
            
            public static Configuration GetNewConfiguration()
            {
                return new Configuration
                {
                    OneHitUse = false,
                    DistanceUse = 10f,
                };
            }
            [JsonProperty("Использовать функцию 1 удара (бочки и дорожные знаки будут ломаться с 1 удара)")]
            public Boolean OneHitUse;
        }

        void OnContainerDropItems(ItemContainer container)
        {
            if (container == null) return;

            LootContainer lootContainer = container.entityOwner as LootContainer;
            if (lootContainer == null) return;
            if (!IsValidContainer(lootContainer)) return;

            BasePlayer player = lootContainer.lastAttacker as BasePlayer;
            if (player == null) return;

            if (Vector3.Distance(player.transform.position, lootContainer.transform.position) >
                config.DistanceUse) return;

            List<Item> casheList = Facepunch.Pool.GetList<Item>();
            foreach (Item item in lootContainer.inventory.itemList)
                casheList.Add(item);

            foreach (Item item in casheList)
                player.GiveItem(item);
            
            Facepunch.Pool.FreeList(ref casheList);
        }
        
        private void OnBonusItemDropped(Item item, BasePlayer player)
        {
            if(item.GetRootContainer() != null) return;
            
            if (Vector3.Distance(player.transform.position, item.GetWorldEntity().transform.position) >
                config.DistanceUse) return;
            
            player.GiveItem(item);
        }
		   		 		  						  	   		  	  			  	   		  	  			  				
        private object OnEntityTakeDamage(LootContainer container, HitInfo hitInfo)
        {
            if (container == null) return null;
            BasePlayer player = hitInfo.InitiatorPlayer;
            if (player == null) return true;
            if (!IsValidContainer(container)) return null;
            hitInfo.damageTypes.ScaleAll(1000f);
            return null;
        }
        //- Исправлен перенос скрапа из бочек в инвентарь умноженный с помощью чая
        
                private static Configuration config = new Configuration();

        
                private Boolean IsValidContainer(LootContainer lootContainer) =>
            lootContainer.ShortPrefabName.Contains("barrel") ||
            lootContainer.ShortPrefabName.Contains("roadsign");

        protected override void LoadDefaultConfig() => config = Configuration.GetNewConfiguration();
        
        private void Init()
        {
            if(!config.OneHitUse)
                Unsubscribe(nameof(OnEntityTakeDamage));
        }
        
        private void ToggleHooks()
        {
            if (config.OneHitUse)
            {
                Unsubscribe(nameof(OnEntityTakeDamage));
                Subscribe(nameof(OnEntityTakeDamage));
            }
            
            Unsubscribe(nameof(OnContainerDropItems));
            Subscribe(nameof(OnContainerDropItems));
            
            Unsubscribe(nameof(OnBonusItemDropped));
            Subscribe(nameof(OnBonusItemDropped));
        }
		   		 		  						  	   		  	  			  	   		  	  			  				
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();

                if (config.DistanceUse == 0f)
                    config.DistanceUse = 10f;
            }
            catch
            {
                PrintWarning($"Ошибка чтения конфигурации #93 'oxide/config/{Name}', создаём новую конфигурацию!!");
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }
            }
}
