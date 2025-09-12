using Newtonsoft.Json;
using Rust;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;
namespace Oxide.Plugins
{
    [Info("OreBonus", "r3dapple", "69.4.17")] //for Toxa
    class OreBonus : RustPlugin
    {
		private const uint SkinSlitok = 1893418312;
		private const uint SkinWood = 2001371285;
		
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
			
			[JsonProperty(PropertyName = "Шанс выпадения радиационной руды при добычи эксаватором")]
			public float ChanceExcavator { get; set; }
			
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
				[JsonProperty(PropertyName = "Радиус созданой радиации (для слитка)")]
				public float RadiationRadiusSlitok { get; set; }
				[JsonProperty(PropertyName = "Интенсивность созданой радиации (для слитка)")]
				public float IntensityRadiationSlitok { get; set; }
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
			PrintWarning("Спасибо за покупку плагина на сайте RustPlugin.ru! Приобретение в ином месте лишает Вас обновлений и подвергает Ваш сервер опасности.");
		}

        private _Conf SetDefaultConfig()
        {
            return new _Conf
            {
				Chance = 30,
				ChanceExcavator = 1.5f,
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
					new _Conf.OreConfig
					{
						orename = "Урановый слиток",
						itemlist = new List<_Conf.ItemConfig>()
						{
							new _Conf.ItemConfig { shortname = "stones", fixedcount = 1000, min = 1000, max = 5000 },
							new _Conf.ItemConfig { shortname = "metal.fragments", fixedcount = 1000, min = 1000, max = 5000 },
							new _Conf.ItemConfig { shortname = "metal.refined", fixedcount = 50, min = 25, max = 100 },
							new _Conf.ItemConfig { shortname = "sulfur", fixedcount = 1000, min = 1000, max = 5000 },
						},
					},
					new _Conf.OreConfig
					{
						orename = "Радиоактивная древесина",
						itemlist = new List<_Conf.ItemConfig>()
						{
							new _Conf.ItemConfig { shortname = "wood", fixedcount = 10000, min = 1000, max = 10000 },
						},
					},
				},
				
                RadiationSetting = new _Conf.Options
                {
                    DisableDefaultRadiation = false,
					EnabledRadiation = true,
					IntensityRadiation = 10f,
					RadiationRadius = 10f,
					IntensityRadiationSlitok = 15f,
					RadiationRadiusSlitok = 15f,
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

        object OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (player == null) return null;
            if (dispenser.gatherType != ResourceDispenser.GatherType.Ore && dispenser.gatherType != ResourceDispenser.GatherType.Tree) return null;
			if (dispenser.gatherType == ResourceDispenser.GatherType.Ore)
			{
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
			}
			else if (dispenser.gatherType == ResourceDispenser.GatherType.Tree)
			{
				if (Random.Range(100f, 200f) < config.Chance+100)
				{
					item = ItemManager.CreateByItemID(642482233, 1, SkinWood);
					item.name = "Радиоактивная древесина";
					player.GiveItem(item);
					PrintToChat(player, $"Вы нашли предмет <color=#00FF00>Радиоактивная древесина</color>!");
				}
			}
            return null;
        }
		
		object CanWearItem(PlayerInventory inventory, Item item, int targetSlot)
		{
			BasePlayer player = inventory?.GetComponent<BasePlayer>();
			if (player == null) return null;
			if (player.userID < 76560000000000000) return null;
			if (item.info.shortname == "hazmatsuit_scientist" || item.info.shortname == "hazmatsuit_scientist_peacekeeper" || item.info.shortname == "santabeard") return false;
			return null;
		}
		
		void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            if (item == null || item.info == null) return;
			
			if (item.info.shortname.ToLower() == "hazmatsuit_scientist_peacekeeper")
			{
				if (item.name != "Урановый слиток" || item.skin != SkinSlitok)
				{
					item.name = "Урановый слиток";
					item.skin = SkinSlitok;
				}
			}
			
			else if (item.info.shortname.ToLower() == "sticks")
			{
				if (item.name != "Радиоактивная древесина" || item.skin != SkinWood)
				{
					item.name = "Радиоактивная древесина";
					item.skin = SkinWood;
				}
			}
			
			else if (item.info.shortname.ToLower() == "ducttape")
			{
				if (item.name != "Радиоактивный камень" || item.skin != 1499303078)
				{
					item.name = "Радиоактивный камень";
					item.skin = 1499303078;
				}
			}
			
			else if (item.info.shortname.ToLower() == "hazmatsuit_scientist")
			{
				if (item.name != "Радиоактивный металл" || item.skin != 1499311722)
				{
					item.name = "Радиоактивный металл";
					item.skin = 1499311722;
				}
			}
			
			else if (item.info.shortname.ToLower() == "coal")
			{
				if (item.name != "Радиоактивный МВК" || item.skin != 1499301592)
				{
					item.name = "Радиоактивный МВК";
					item.skin = 1499301592;
				}
			}
			
			else if (item.info.shortname.ToLower() == "santabeard")
			{
				if (item.name != "Радиоактивная сера" || item.skin != 1499310834)
				{
					item.name = "Радиоактивная сера";
					item.skin = 1499310834;
				}
			}
        }
		
		void OnExcavatorGather(ExcavatorArm excavator, Item item)
		{
			if (UnityEngine.Random.Range(0f, 500f) < config.ChanceExcavator)
			{
				ExcavatorOutputPile excavatorOutputPileGeneric = excavator.outputPiles.GetRandom();
				Item slitok = ItemManager.CreateByItemID(-1958316066, 1, SkinSlitok);
				slitok.name = "Урановый слиток";
				slitok.MoveToContainer(excavatorOutputPileGeneric.inventory, -1, true);
				return;
			}
		}

        private void GiveOre(BasePlayer player, int type)
        {
            ulong skinid = 0U;
			int sitemid = 0;
            string newname = String.Empty;
            switch (type)
            {
                case 1:
					sitemid = 1401987718; //ducttape
                    skinid = 1499303078;
                    newname = "Радиоактивный камень";
                    break;
                case 2:
					sitemid = -253079493; //hazmatsuit_scientist
                    skinid = 1499311722;
                    newname = "Радиоактивный металл";
                    break;
                case 3:
					sitemid = 204391461; //coal
                    skinid = 1499301592;
                    newname = "Радиоактивный МВК";
                    break;
                case 4:
					sitemid = 2126889441; //santabeard
                    skinid = 1499310834;
                    newname = "Радиоактивная сера";
                    break;
				case 5:
					sitemid = 642482233; //sticks
                    newname = "Радиоактивная древесина";
                    break;
				case 6:
					sitemid = -1958316066; //hazmatsuit_scientist_peacekeeper
                    newname = "Урановый слиток";
                    break;
            }

            Item ore = ItemManager.CreateByItemID(sitemid, 1, skinid);
            ore.name = newname;
            player.GiveItem(ore, BaseEntity.GiveItemReason.PickedUp);
            PrintToChat(player, $"Вы нашли предмет <color=#00FF00>{newname}</color>!");
            return;
        }

        private Timer mytimer;

        object OnRecyclerToggle(Recycler recycler, BasePlayer player)
        {
            if (recycler.IsOn() || !config.RadiationSetting.EnabledRadiation) return null;

            var items1 = recycler.inventory.FindItemsByItemID(1401987718);
			var items2 = recycler.inventory.FindItemsByItemID(-253079493);
			var items3 = recycler.inventory.FindItemsByItemID(204391461);
			var items4 = recycler.inventory.FindItemsByItemID(2126889441);
			var items5 = recycler.inventory.FindItemsByItemID(-1958316066);
			var items6 = recycler.inventory.FindItemsByItemID(642482233);
            if (RadiationZones.ContainsKey(recycler.GetInstanceID()))
                DestroyZone(recycler.GetInstanceID());
            if (DestroyZones.ContainsKey(recycler.GetInstanceID()))
                DestroyZones.Remove(recycler.GetInstanceID());
			if (items5 != null && (items5.Where(i => i.skin == SkinSlitok) != null))
			{
				if (RadiationZones.ContainsKey(recycler.GetInstanceID()))
                DestroyZone(recycler.GetInstanceID());
				if (DestroyZones.ContainsKey(recycler.GetInstanceID()))
                DestroyZones.Remove(recycler.GetInstanceID());
				InitializeZone(recycler.transform.position, config.RadiationSetting.IntensityRadiationSlitok, config.RadiationSetting.RadiationRadiusSlitok, recycler.GetInstanceID());
                DestroyZones.Add(recycler.GetInstanceID(), timer.Once(config.RadiationSetting.TimeToDestroy, () => DestroyZone(recycler.GetInstanceID())));
			}
            if ((items1 != null || items2 != null || items3 != null || items4 != null || items6 != null) && (items1.Where(i => i.skin == 1499303078) != null || items2.Where(i => i.skin == 1499311722) != null || items3.Where(i => i.skin == 1499301592) != null || items4.Where(i => i.skin == 1499310834) != null || items6.Where(i => i.skin == SkinWood) != null))
            {
				if (RadiationZones.ContainsKey(recycler.GetInstanceID()))
                DestroyZone(recycler.GetInstanceID());
				if (DestroyZones.ContainsKey(recycler.GetInstanceID()))
                DestroyZones.Remove(recycler.GetInstanceID());
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
            if (item.info.itemid == 1401987718 || item.info.itemid == -253079493 || item.info.itemid == 204391461 || item.info.itemid == 2126889441)
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
			else if (item.info.shortname == "hazmatsuit_scientist_peacekeeper" && item.skin == SkinSlitok)
			{
				foreach (var cs in config.Ore.Where(x => x.orename == "Урановый слиток"))
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
			}
			else if (item.info.shortname == "sticks" && item.skin == SkinWood)
			{
				foreach (var cs in config.Ore.Where(x => x.orename == "Радиоактивная древесина"))
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
			}
			else return null;
            return null;
        }

        /*private object CanCombineDroppedItem(DroppedItem item, DroppedItem targetItem)
        {
            if (item.item.info.itemid == itemid) return false;

            return null;
        }

        private object CanStackItem(Item item, Item targetItem)
        {
            if (item.info.itemid == itemid) return false;

            return null;
        }*/

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
                PrintToChat(player, $"Используйте: /getore [номер руды]\n1 - камень\n2 - метал\n3 - МВК\n4 - сера\n5 - дерево\n6 - слиток");
                return;
            }
            int ruda = 0;
            if (!int.TryParse(args[0], out ruda))
            {
                PrintToChat(player, $"Используйте: /getore [номер руды]\n1 - камень\n2 - метал\n3 - МВК\n4 - сера\n5 - дерево\n6 - слиток");
                return;
            }
            if (ruda > 6 || ruda < 1)
            {
                PrintToChat(player, $"Используйте: /getore [номер руды]\n1 - камень\n2 - метал\n3 - МВК\n4 - сера\n5 - дерево\n6 - слиток");
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