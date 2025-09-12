using System.Linq;
using UnityEngine;
using Oxide.Core.Plugins;
using Rust;

namespace Oxide.Plugins
{
    [Info("GoldFish", "wazzzup", "1.1.1")]
    [Description("GoldFish")]
    public class GoldFish : RustPlugin
    {
        [PluginReference] Plugin LootItems;

        class FishTrap : MonoBehaviour
        {
            private StorageContainer Container;

            public void Awake()
            {
                Container = GetComponent<StorageContainer>();
                InvokeRepeating(nameof(Idle), configData.waitAmount, configData.waitAmount); 
            }

            public void Idle()
            {
                if (Container.inventory.itemList.Any(p => p.info.category == ItemCategory.Food))
                {
                    var item = Container.inventory.itemList.FirstOrDefault(p => p.info.category == ItemCategory.Food);
                    if (item != null)
                    {
                        if (item.amount == 1)
                        {
                            item.DoRemove();
                        }
                        else item.amount--;
                    }

                    if (Oxide.Core.Random.Range(0, 100) > configData.DropChance) return;
                    
                    Item itemd = ItemManager.CreateByName("fish.troutsmall", 1, 1492612801UL);
                    itemd.name = "TEST"; 
                    
                    itemd.MoveToContainer(Container.inventory);
                }
            }

        }

        private void Unload()
        {
            UnityEngine.Object.FindObjectsOfType<FishTrap>().ToList().ForEach(UnityEngine.Object.Destroy); 
        }

        void OnEntityBuilt(Planner plan, GameObject obj)
        {
            var entity = obj.GetComponent<BaseEntity>();
            if (entity is WildlifeTrap)
            {
                entity.gameObject.AddComponent<FishTrap>();
            }
        }

        static ConfigData configData;

        class ConfigData
        {
            public int    spawnChance = 10;
            public int waitAmount = 20;
            public int DropChance = 20;
            public string fishName    = "Золотая рыбка<size=24>\n</size><size=12> Загадай три желания и выпотроши меня по-полной:-P</size>";
        }

        protected override void LoadDefaultConfig()
        {
            configData = new ConfigData();
            SaveConfig(configData);
        }

        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);

        void Init()
        {
            configData = Config.ReadObject<ConfigData>();
            SaveConfig(configData);
        }

        void OnServerInitialized()
        {
            cmd.AddConsoleCommand("givefish", this, "cmdGive");
            foreach (var check in UnityEngine.Object.FindObjectsOfType<WildlifeTrap>())
                check.gameObject.AddComponent<FishTrap>();
        }

        void cmdGive(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player() ?? null;
            if (player?.net.connection.authLevel < 2) return;
            if (arg.Args == null || arg.Args.Length < 1)
            {
                SendReply(arg, "bad syntax");
                return;
            }

            BasePlayer targetPlayer = BasePlayer.Find(arg.Args[0]);
            if (targetPlayer == null)
            {
                SendReply(arg, "error player not found for give");
                return;
            }

            Item item = ItemManager.CreateByName("fish.troutsmall", 1, 1492612801UL);
            item.name = configData.fishName;
            if (!targetPlayer.inventory.GiveItem(item))
            {
                item.Drop(targetPlayer.inventory.containerMain.dropPosition, targetPlayer.inventory.containerMain.dropVelocity, new Quaternion());
            }
        }

        object OnItemAction(Item item, string action, BasePlayer player)
        {
            if (action == "Gut" && item.skin == 1492612801UL)
            {
                plugins.Find("Quest")?.Call("AddEXP", player, 0.002f, "За разделку рыбки");  
                LootItems?.Call("ManualSpawn", player.inventory.containerMain, "goldfish");
                item.UseItem(1);
                return true;
            }

            return null;
        }
    }
}