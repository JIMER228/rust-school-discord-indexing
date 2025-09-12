using Newtonsoft.Json;
using Rust;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;
namespace Oxide.Plugins
{
    [Info("OreBonus", "https://topplugin.ru/", "1.2.11")]
    class OreBonus : RustPlugin
    {
		private void Init()
		{
			LoadConfig();
			
			SaveConfig();
		}
		
		private _Conf config; 
		
        class _Conf
        {
			[JsonProperty(PropertyName = "Шанс, что после добычи обычной руды игрок получит радиационную (в процентах)")]
			public int Chance { get; set; }
			
            [JsonProperty(PropertyName = "Настройки радиации")]
            public Options RadiationSetting { get; set; }
			
			[JsonProperty(PropertyName = "Настройки переработки")]
            public List<OreConfig> Ore { get; set; }

			public class OreConfig
			{
				[JsonProperty(PropertyName = "Название руды (не менять)")]
				public string orename { get; set; }
				[JsonProperty(PropertyName = "Выдаваемый при переработке лут")]
				public List<ItemConfig> itemlist { get; set; }
			}
			
			public class ItemConfig
			{
				[JsonProperty(PropertyName = "Shortname предмета")]
				public string shortname { get; set; }
				[JsonProperty(PropertyName = "Фиксированное количество")]
				public int fixedcount { get; set; }
				[JsonProperty(PropertyName = "Минимальное рандомное количество")]
				public int min { get; set; }
				[JsonProperty(PropertyName = "Максимальное рандомное количество")]
				public int max { get; set; }
			}
			
			public class Options
            {
				[JsonProperty(PropertyName = "Создавать ли радиацию при начале переработки")]
				public bool EnabledRadiation { get; set; }
				[JsonProperty(PropertyName = "Радиус созданой радиации")]
				public float RadiationRadius { get; set; }
				[JsonProperty(PropertyName = "Интенсивность созданой радиации")]
				public float IntensityRadiation { get; set; }
				[JsonProperty(PropertyName = "Отключить стандартную радиацию на РТ (это нужно в случае если у Вас отключена радиация, плагин включит её обратно но уберёт на РТ)")]
				public bool DisableDefaultRadiation { get; set; }
				[JsonProperty(PropertyName = "Длительность созданной радиации (через сколько пропадёт зона, в секундах)")]
				public float TimeToDestroy { get; set; }
            }
			
           
		}
		
		protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<_Conf>();

            Config.WriteObject(config, true);
        }

        protected override void LoadDefaultConfig()
		{
			config = SetDefaultConfig();
			PrintWarning("Создаём конфиг-файл...");
			PrintWarning("Спасибо за покупку плагина на сайте TopPlugin.ru! Приобретение в ином месте лишает Вас обновлений и подвергает Ваш сервер опасности.");
		}

        private _Conf SetDefaultConfig()
        {
            return new _Conf
            {
				
				Chance = 30,
				Ore = new List<_Conf.OreConfig>()
				{
					new _Conf.OreConfig
					{
						orename = "Камень",
						itemlist = new List<_Conf.ItemConfig>()
						{
							new _Conf.ItemConfig { shortname = "stones", fixedcount = 10000, min = 1000, max = 10000 },
						},
					},
					new _Conf.OreConfig
					{
						orename = "Метал",
						itemlist = new List<_Conf.ItemConfig>()
						{
							new _Conf.ItemConfig { shortname = "metal.fragments", fixedcount = 10000, min = 1000, max = 10000 },
						},
					},
					new _Conf.OreConfig
					{
						orename = "МВК",
						itemlist = new List<_Conf.ItemConfig>()
						{
							new _Conf.ItemConfig { shortname = "metal.refined", fixedcount = 750, min = 100, max = 500 },
						},
					},
					new _Conf.OreConfig
					{
						orename = "Сера",
						itemlist = new List<_Conf.ItemConfig>()
						{
							new _Conf.ItemConfig { shortname = "sulfur", fixedcount = 1000, min = 1000, max = 5000 },
						},
					},
				},
				
                RadiationSetting = new _Conf.Options
                {
                    DisableDefaultRadiation = false,
					EnabledRadiation = true,
					IntensityRadiation = 10f,
					RadiationRadius = 10f,
					TimeToDestroy = 10f
                }              
            };
        }

        protected override void SaveConfig() => Config.WriteObject(config, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Обновляем конфиг-файл...");

            _Conf baseConfig = SetDefaultConfig();
        }

        private static int itemid = 204391461;

        object OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (player == null) return null;
            if (dispenser.gatherType != ResourceDispenser.GatherType.Ore) return null;
            if (Random.Range(0f, 100f) < config.Chance)
            {
                switch (item.info.shortname)
                {
                    case "stones":
                        GiveOre(player, 1);
                        break;
                    case "metal.ore":
                        GiveOre(player, 2);
                        break;
                    case "hq.metal.ore":
                        GiveOre(player, 3);
                        break;
                    case "sulfur.ore":
                        GiveOre(player, 4);
                        break;
                }
            }
            return null;
        }

        private void GiveOre(BasePlayer player, int type)
        {
            ulong skinid = 0U;
            string newname = String.Empty;
            switch (type)
            {
                case 1:
                    skinid = 1499303078;
                    newname = "Радиоактивный камень";
                    break;
                case 2:
                    skinid = 1499311722;
                    newname = "Радиоактивный металл";
                    break;
                case 3:
                    skinid = 1499301592;
                    newname = "Радиоактивный МВК";
                    break;
                case 4:
                    skinid = 1499310834;
                    newname = "Радиоактивная сера";
                    break;
            }

            Item ore = ItemManager.CreateByItemID(itemid, 1, skinid);
            ore.name = newname;
            player.GiveItem(ore, BaseEntity.GiveItemReason.PickedUp);
            PrintToChat(player, $"> Вы нашли предмет <color=#32CD32>{newname}</color>!");
            return;
        }

        private Timer mytimer;

        object OnRecyclerToggle(Recycler recycler, BasePlayer player)
        {
            if (recycler.IsOn() || !config.RadiationSetting.EnabledRadiation) return null;

            var items = recycler.inventory.FindItemsByItemID(itemid);
            if (RadiationZones.ContainsKey(recycler.GetInstanceID()))
                DestroyZone(recycler.GetInstanceID());
            if (DestroyZones.ContainsKey(recycler.GetInstanceID()))
                DestroyZones.Remove(recycler.GetInstanceID());
            if (items != null && items.Where(i => i.skin == 1499303078 || i.skin == 1499311722 || i.skin == 1499301592 || i.skin == 1499310834).FirstOrDefault() != null)
            {
                InitializeZone(recycler.transform.position, config.RadiationSetting.IntensityRadiation, config.RadiationSetting.RadiationRadius, recycler.GetInstanceID());
                DestroyZones.Add(recycler.GetInstanceID(), timer.Once(config.RadiationSetting.TimeToDestroy, () => DestroyZone(recycler.GetInstanceID())));
            }
            return null;
        }

        private void DestroyZone(int zone)
        {
            if (RadiationZones.ContainsKey(zone))
            {
                UnityEngine.Object.Destroy(RadiationZones[zone].zone);
                RadiationZones.Remove(zone);
            }
        }

        Dictionary<int, Timer> DestroyZones = new Dictionary<int, Timer>();

        private object OnRecycleItem(Recycler recycler, Item item)
        {
            if (item.info.itemid == itemid)
            {
                item.UseItem(1);
                switch (item.skin)
                {
                    case 1499303078:
						foreach (var cs in config.Ore.Where(x => x.orename == "Камень"))
						{
							for (int i = 0; i < cs.itemlist.Count; i++)
							{
								if (i > 5) continue;
								Item recycled = ItemManager.CreateByName(cs.itemlist[i].shortname, cs.itemlist[i].fixedcount + UnityEngine.Random.Range(cs.itemlist[i].min, cs.itemlist[i].max+1));
								if (recycled == null)
								{
									PrintError($"Shortname error: {cs.itemlist[i].shortname}");
									return null;
								}
								recycler.MoveItemToOutput(recycled);
							}
						}
                        break;
                    case 1499311722:
                        foreach (var cs in config.Ore.Where(x => x.orename == "Метал"))
						{
							for (int i = 0; i < cs.itemlist.Count; i++)
							{
								if (i > 5) continue;
								Item recycled = ItemManager.CreateByName(cs.itemlist[i].shortname, cs.itemlist[i].fixedcount + UnityEngine.Random.Range(cs.itemlist[i].min, cs.itemlist[i].max+1));
								if (recycled == null)
								{
									PrintError($"Shortname error: {cs.itemlist[i].shortname}");
									return null;
								}
								recycler.MoveItemToOutput(recycled);
							}
						}
                        break;
                    case 1499301592:
                        foreach (var cs in config.Ore.Where(x => x.orename == "МВК"))
						{
							for (int i = 0; i < cs.itemlist.Count; i++)
							{
								if (i > 5) continue;
								Item recycled = ItemManager.CreateByName(cs.itemlist[i].shortname, cs.itemlist[i].fixedcount + UnityEngine.Random.Range(cs.itemlist[i].min, cs.itemlist[i].max+1));
								if (recycled == null)
								{
									PrintError($"Shortname error: {cs.itemlist[i].shortname}");
									return null;
								}
								recycler.MoveItemToOutput(recycled);
							}
						}
                        break;
                    case 1499310834:
                        foreach (var cs in config.Ore.Where(x => x.orename == "Сера"))
						{
							for (int i = 0; i < cs.itemlist.Count; i++)
							{
								if (i > 5) continue;
								Item recycled = ItemManager.CreateByName(cs.itemlist[i].shortname, cs.itemlist[i].fixedcount + UnityEngine.Random.Range(cs.itemlist[i].min, cs.itemlist[i].max+1));
								if (recycled == null)
								{
									PrintError($"Shortname error: {cs.itemlist[i].shortname}");
									return null;
								}
								recycler.MoveItemToOutput(recycled);
							}
						}
                        break;
                    default:
                        return null;
                }
                return true;
            }
            return null;
        }

        private object CanCombineDroppedItem(DroppedItem item, DroppedItem targetItem)
        {
            if (item.item.info.itemid == itemid) return false;

            return null;
        }

        private object CanStackItem(Item item, Item targetItem)
        {
            if (item.info.itemid == itemid) return false;

            return null;
        }

        [ChatCommand("getore")]
        private void cmdgetore(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
            {
                PrintToChat(player, "Нет прав!");
                return;
            }
            if (args.Length < 1)
            {
                PrintToChat(player, $"Используйте: /getore [номер руды]\n1 - камень\n2 - метал\n3 - МВК\n4 - сера");
                return;
            }
            int ruda = 0;
            if (!int.TryParse(args[0], out ruda))
            {
                PrintToChat(player, $"Используйте: /getore [номер руды]\n1 - камень\n2 - метал\n3 - МВК\n4 - сера");
                return;
            }
            if (ruda > 4 || ruda < 1)
            {
                PrintToChat(player, $"Используйте: /getore [номер руды]\n1 - камень\n2 - метал\n3 - МВК\n4 - сера");
                return;
            }
            GiveOre(player, ruda);
            return;
        }

        [ChatCommand("orec")]
        private void cmdoretest(BasePlayer player)
        {
            if (!player.IsAdmin)
            {
                PrintToChat(player, "Нет прав!");
                return;
            }
            int count = 0;
            for (int i = 0; i < 50; i++)
            {
                if (Random.Range(0f, 100f) < config.Chance) count++;
            }
            PrintToChat(player, $"Из 50 камней выпадет примерно {count.ToString()} радиационной руды (шанс - {config.Chance}%)");
        }

        public class ZoneList
        {
            public RadZones zone;
        }

        private void OnServerRadiation()
        {
            var allobjects = UnityEngine.Object.FindObjectsOfType<TriggerRadiation>();
            for (int i = 0;
            i < allobjects.Length;
            i++)
            {
                UnityEngine.Object.Destroy(allobjects[i]);
            }
        }

        private ZoneList Zone;
        private Dictionary<int, ZoneList> RadiationZones = new Dictionary<int, ZoneList>();
        private static readonly int playerLayer = LayerMask.GetMask("Player (Server)");
        private static readonly Collider[] colBuffer = Vis.colBuffer;


        private void InitializeZone(Vector3 Location, float intensity, float radius, int ZoneID)
        {
            if (!ConVar.Server.radiation) ConVar.Server.radiation = true;
            if (config.RadiationSetting.DisableDefaultRadiation)
                OnServerRadiation();
            var newZone = new GameObject().AddComponent<RadZones>();
            newZone.Activate(Location, radius, intensity, ZoneID);
            ZoneList listEntry = new ZoneList
            {
                zone = newZone
            }
            ;
            RadiationZones.Add(ZoneID, listEntry);
        }

        public class RadZones : MonoBehaviour
        {
            private int ID;
            private Vector3 Position;
            private float ZoneRadius;
            private float RadiationAmount;
            private List<BasePlayer> InZone;
            private void Awake()
            {
                gameObject.layer = (int)Layer.Reserved1;
                gameObject.name = "NukeZone";
                var rigidbody = gameObject.AddComponent<Rigidbody>();
                rigidbody.useGravity = false;
                rigidbody.isKinematic = true;
            }
            public void Activate(Vector3 pos, float radius, float amount, int ZoneID)
            {
                ID = ZoneID;
                Position = pos;
                ZoneRadius = radius;
                RadiationAmount = amount;
                gameObject.name = $"OreBonus{ID}";
                transform.position = Position;
                transform.rotation = new Quaternion();
                UpdateCollider();
                gameObject.SetActive(true);
                enabled = true;
                var Rads = gameObject.GetComponent<TriggerRadiation>();
                Rads = Rads ?? gameObject.AddComponent<TriggerRadiation>();
                Rads.RadiationAmountOverride = RadiationAmount;
                Rads.interestLayers = playerLayer;
                Rads.enabled = true;
                if (IsInvoking("UpdateTrigger")) CancelInvoke("UpdateTrigger");
                InvokeRepeating("UpdateTrigger", 5f, 5f);
            }
            private void OnDestroy()
            {
                CancelInvoke("UpdateTrigger");
                Destroy(gameObject);
            }
            private void UpdateCollider()
            {
                var sphereCollider = gameObject.GetComponent<SphereCollider>();
                {
                    if (sphereCollider == null)
                    {
                        sphereCollider = gameObject.AddComponent<SphereCollider>();
                        sphereCollider.isTrigger = true;
                    }
                    sphereCollider.radius = ZoneRadius;
                }
            }
            private void UpdateTrigger()
            {
                InZone = new List<BasePlayer>();
                int entities = Physics.OverlapSphereNonAlloc(Position, ZoneRadius, colBuffer, playerLayer);
                for (var i = 0;
                i < entities;
                i++)
                {
                    var player = colBuffer[i].GetComponentInParent<BasePlayer>();
                    if (player != null) InZone.Add(player);
                }
            }
        }
    }
}