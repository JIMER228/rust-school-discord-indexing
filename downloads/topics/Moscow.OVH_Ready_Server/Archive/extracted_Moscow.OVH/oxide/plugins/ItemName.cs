using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("ItemName", "Hougan", "0.0.1")]
    public class ItemName : RustPlugin
    {
        #region Classes

        private class CustomItem
        {
            [JsonProperty("SkinID предметa")]
            public ulong SkinID;
            [JsonProperty("Отображаемое имя предмета")]
            public string DisplayName;
            
            public CustomItem() {}
            public CustomItem(ulong skinId, string name)
            {
                SkinID = skinId;
                DisplayName = name;
            }
        }

        private class Configuration
        {
            [JsonProperty("Список предметов для отслеживания")]
            public List<CustomItem> Items = new List<CustomItem>();

            public static Configuration Generate()
            {
                return new Configuration()
                {
                    Items = new List<CustomItem>
                    {
                        new CustomItem(1492610636UL, "Золотой череп"),
                        new CustomItem(1492612801UL, "Золотая рыбка"),
                        new CustomItem(1500420411UL, "Золотая ловушка"),
                        new CustomItem(1552134674UL, "Волшебный саженец"),
                        new CustomItem(1492611813UL, "Сердце человека")
                    }
                };
            }
        }
        
        #endregion

        #region Variables

        private static Configuration Settings;

        #endregion

        #region Initialization
       
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                Settings = Config.ReadObject<Configuration>();
                if (Settings == null) LoadDefaultConfig();
            } 
            catch
            {
                PrintWarning($"Ошибка чтения конфигурации 'oxide/config/{Name}', создаём новую конфигурацию!!");
                LoadDefaultConfig();
            }
            
            NextTick(SaveConfig);
        }

        protected override void LoadDefaultConfig() => Settings = Configuration.Generate();
        protected override void SaveConfig()        => Config.WriteObject(Settings);

        #endregion

        #region Hooks

        private void CanAcceptItem(ItemContainer container, Item item, int targetPos)
        {
            var shouldName = Settings.Items.FirstOrDefault(p => p.SkinID == item.skin);
            if (shouldName == null) return;

            item.name = shouldName.DisplayName;
        } 

        #endregion
    }
}