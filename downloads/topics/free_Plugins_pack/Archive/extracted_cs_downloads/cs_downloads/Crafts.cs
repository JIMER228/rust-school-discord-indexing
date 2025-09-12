using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Rust;
using UnityEngine;
using VLB;
using WebSocketSharp;
using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
 [Info("Crafts", "Scrooge", "1.17.0")]
	public class Crafts : RustPlugin
	{
		#region Fields

		[PluginReference] private Plugin ImageLibrary, SpawnModularCar;

		private const string Layer = "UI.Crafts";

		private static Crafts _instance;

		private enum WorkbenchLevel
		{
			None = 0,
			One = 1,
			Two = 2,
			Three = 3
		}

		private enum CraftType
		{
			Command,
			Vehicle,
			Item,
			Recycler,
			ModularCar
		}

		#endregion

		#region Config

		private static Configuration _config;

		private class Configuration
		{
			[JsonProperty(PropertyName = "The color of the button when all the items are present")]
			public string GreenColor = "#80FF8080";

			[JsonProperty(PropertyName = "Command")]
			public string Command = "craft";

			[JsonProperty(PropertyName = "Enable debug?")]
			public bool useDebug = true;

			[JsonProperty(PropertyName = "Workbenches Setting",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<WorkbenchLevel, WorkbenchConfig> Workbenchs =
				new Dictionary<WorkbenchLevel, WorkbenchConfig>
				{
					[WorkbenchLevel.None] = new WorkbenchConfig("#00000080", "No Workbench Required"),
					[WorkbenchLevel.One] = new WorkbenchConfig("#80400080", "Workbench 1 LVL"),
					[WorkbenchLevel.Two] = new WorkbenchConfig("#0080FF80", "Workbench 2 LVL"),
					[WorkbenchLevel.Three] = new WorkbenchConfig("#FF000080", "Workbench 3 LVL")
				};

			[JsonProperty(PropertyName = "Craft Settings", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<CraftConfig> CraftsList = new List<CraftConfig>
			{
				new CraftConfig
				{
					Enabled = true,
					ImageURL = "https://i.imgur.com/YXjADeE.png",
					Description = new List<string>
					{
						"Craft requires:",
						"- Gears (5 pcs)",
						"- Road Signs (5 pcs)",
						"- Metal (2000 pcs)"
					},
					Command = "givecopter",
					Permission = "crafts.all",
					DisplayName = "Minicopter",
					ShortName = "electric.flasherlight",
					Amount = 1,
					SkinID = 2080145158,
					Type = CraftType.Vehicle,
					Prefab = "assets/content/vehicles/minicopter/minicopter.entity.prefab",
					Level = WorkbenchLevel.One,
					UseDistance = true,
					Distance = 1.5f,
					GiveCommand = string.Empty,
					Ground = true,
					Structure = true,
					Items = new List<ItemForCraft>
					{
						new ItemForCraft("gears", 5, 0),
						new ItemForCraft("roadsigns", 5, 0),
						new ItemForCraft("metal.fragments", 2000, 0)
					}
				},
				new CraftConfig
				{
					Enabled = true,
					ImageURL = "https://i.imgur.com/dmWQOm6.png",
					Description = new List<string>
					{
						"Craft requires:",
						"- Gears (5 pcs)",
						"- Road Signs (5 pcs)",
						"- Metal (2000 pcs)"
					},
					Command = "giverowboat",
					Permission = "crafts.all",
					DisplayName = "Row Boat",
					ShortName = "coffin.storage",
					Amount = 1,
					SkinID = 2080150023,
					Type = CraftType.Vehicle,
					Prefab = "assets/content/vehicles/boats/rowboat/rowboat.prefab",
					Level = WorkbenchLevel.Two,
					UseDistance = true,
					Distance = 1.5f,
					GiveCommand = string.Empty,
					Ground = true,
					Structure = true,
					Items = new List<ItemForCraft>
					{
						new ItemForCraft("gears", 5, 0),
						new ItemForCraft("roadsigns", 5, 0),
						new ItemForCraft("metal.fragments", 2000, 0)
					}
				},
				new CraftConfig
				{
					Enabled = true,
					ImageURL = "https://i.imgur.com/CgpVw2j.png",
					Description = new List<string>
					{
						"Craft requires:",
						"- Gears (5 pcs)",
						"- Road Signs (5 pcs)",
						"- Metal (2000 pcs)"
					},
					Command = "giverhibboat",
					Permission = "crafts.all",
					DisplayName = "RHIB",
					ShortName = "electric.sirenlight",
					Amount = 1,
					SkinID = 2080150770,
					Type = CraftType.Vehicle,
					GiveCommand = string.Empty,
					Prefab = "assets/content/vehicles/boats/rhib/rhib.prefab",
					Level = WorkbenchLevel.Three,
					UseDistance = true,
					Distance = 1.5f,
					Ground = true,
					Structure = true,
					Items = new List<ItemForCraft>
					{
						new ItemForCraft("gears", 5, 0),
						new ItemForCraft("roadsigns", 5, 0),
						new ItemForCraft("metal.fragments", 2000, 0)
					}
				},
				new CraftConfig
				{
					Enabled = true,
					ImageURL = "https://i.imgur.com/eioxlvK.png",
					Description = new List<string>
					{
						"Craft requires:",
						"- Gears (5 pcs)",
						"- Road Signs (5 pcs)",
						"- Metal (2000 pcs)"
					},
					Command = "givesedan",
					Permission = "crafts.all",
					DisplayName = "Car",
					ShortName = "woodcross",
					Amount = 1,
					SkinID = 2080151780,
					Type = CraftType.Vehicle,
					GiveCommand = string.Empty,
					Prefab = "assets/content/vehicles/sedan_a/sedantest.entity.prefab",
					Level = WorkbenchLevel.Two,
					UseDistance = true,
					Distance = 1.5f,
					Ground = true,
					Structure = true,
					Items = new List<ItemForCraft>
					{
						new ItemForCraft("gears", 5, 0),
						new ItemForCraft("roadsigns", 5, 0),
						new ItemForCraft("metal.fragments", 2000, 0)
					}
				},
				new CraftConfig
				{
					Enabled = true,
					ImageURL = "https://i.imgur.com/cp2Xx2A.png",
					Description = new List<string>
					{
						"Craft requires:",
						"- Gears (5 pcs)",
						"- Road Signs (5 pcs)",
						"- Metal (2000 pcs)"
					},
					Command = "givehotair",
					Permission = "crafts.all",
					DisplayName = "Hot Air Balloon",
					ShortName = "box.repair.bench",
					Amount = 1,
					SkinID = 2080152635,
					Type = CraftType.Vehicle,
					GiveCommand = string.Empty,
					Prefab = "assets/prefabs/deployable/hot air balloon/hotairballoon.prefab",
					Level = WorkbenchLevel.Three,
					UseDistance = true,
					Distance = 1.5f,
					Ground = true,
					Structure = true,
					Items = new List<ItemForCraft>
					{
						new ItemForCraft("gears", 5, 0),
						new ItemForCraft("roadsigns", 5, 0),
						new ItemForCraft("metal.fragments", 2000, 0)
					}
				},
				new CraftConfig
				{
					Enabled = true,
					ImageURL = "https://i.imgur.com/7JZE0Lr.png",
					Description = new List<string>
					{
						"Craft requires:",
						"- Gears (5 pcs)",
						"- Road Signs (5 pcs)",
						"- Metal (2000 pcs)"
					},
					Command = "givescrapheli",
					Permission = "crafts.all",
					DisplayName = "Transport Helicopter",
					ShortName = "lantern",
					Amount = 1,
					SkinID = 2080154394,
					Type = CraftType.Vehicle,
					GiveCommand = string.Empty,
					Prefab = "assets/content/vehicles/scrap heli carrier/scraptransporthelicopter.prefab",
					Level = WorkbenchLevel.Three,
					UseDistance = true,
					Distance = 1.5f,
					Ground = true,
					Structure = true,
					Items = new List<ItemForCraft>
					{
						new ItemForCraft("gears", 5, 0),
						new ItemForCraft("roadsigns", 5, 0),
						new ItemForCraft("metal.fragments", 2000, 0)
					}
				},
				new CraftConfig
				{
					Enabled = true,
					ImageURL = "https://i.imgur.com/LLB2AVi.png",
					Description = new List<string>
					{
						"Craft requires:",
						"- Gears (5 pcs)",
						"- Road Signs (5 pcs)",
						"- Metal (2000 pcs)"
					},
					Command = "giverecycler",
					Permission = "crafts.all",
					DisplayName = "Home Recycler",
					ShortName = "research.table",
					Amount = 1,
					SkinID = 2186833264,
					Type = CraftType.Recycler,
					Prefab = "assets/bundled/prefabs/static/recycler_static.prefab",
					GiveCommand = string.Empty,
					Level = WorkbenchLevel.Two,
					UseDistance = true,
					Distance = 1.5f,
					Ground = true,
					Structure = true,
					Items = new List<ItemForCraft>
					{
						new ItemForCraft("gears", 5, 0),
						new ItemForCraft("roadsigns", 5, 0),
						new ItemForCraft("metal.fragments", 2000, 0)
					}
				},
				new CraftConfig
				{
					Enabled = true,
					ImageURL = "https://i.imgur.com/mw1T17x.png",
					Description = new List<string>
					{
						"Craft requires:",
						"- Gears (5 pcs)",
						"- Road Signs (5 pcs)",
						"- Metal (2000 pcs)"
					},
					Command = "givelr300",
					Permission = "crafts.all",
					DisplayName = string.Empty,
					ShortName = "rifle.lr300",
					Amount = 1,
					SkinID = 0,
					Type = CraftType.Item,
					Prefab = string.Empty,
					GiveCommand = string.Empty,
					Level = WorkbenchLevel.None,
					UseDistance = true,
					Distance = 1.5f,
					Ground = true,
					Structure = true,
					Items = new List<ItemForCraft>
					{
						new ItemForCraft("gears", 5, 0),
						new ItemForCraft("roadsigns", 5, 0),
						new ItemForCraft("metal.fragments", 2000, 0)
					}
				},
				new CraftConfig
				{
					Enabled = true,
					ImageURL = "https://i.imgur.com/z7X5D5V.png",
					Description = new List<string>
					{
						"Craft requires:",
						"- Gears (5 pcs)",
						"- Road Signs (5 pcs)",
						"- Metal (2000 pcs)"
					},
					Command = "givemod1",
					Permission = "crafts.all",
					DisplayName = "Car",
					ShortName = "electric.flasherlight",
					Amount = 1,
					SkinID = 2244308598,
					Type = CraftType.ModularCar,
					Prefab = string.Empty,
					GiveCommand = string.Empty,
					Level = WorkbenchLevel.Two,
					UseDistance = true,
					Distance = 1.5f,
					Ground = true,
					Structure = true,
					Items = new List<ItemForCraft>
					{
						new ItemForCraft("gears", 5, 0),
						new ItemForCraft("roadsigns", 5, 0),
						new ItemForCraft("metal.fragments", 2000, 0)
					},
					Modular = new ModularCarConf
					{
						CodeLock = true,
						KeyLock = false,
						EnginePartsTier = 2,
						FreshWaterAmount = 0,
						FuelAmount = 140,
						Modules = new[]
						{
							"vehicle.1mod.engine",
							"vehicle.1mod.cockpit.armored",
							"vehicle.1mod.cockpit.armored"
						}
					}
				}
			};

			[JsonProperty(PropertyName = "Recycler Settings")]
			public RecyclerConfig Recycler = new RecyclerConfig
			{
				Speed = 5f,
				Radius = 7.5f,
				Text = "<size=19>RECYCLER</size>\n<size=15>{0}/{1}</size>",
				Color = "#C5D0E6",
				Delay = 0.75f,
				Available = true,
				Owner = true,
				Amounts = new[] {0.9f, 0, 0, 0, 0, 0.5f, 0, 0, 0, 0.9f, 0.5f, 0.5f, 0, 1, 1, 0.5f, 0, 0, 0, 0, 0, 1, 1},
				Scale = 0.5f,
				DDraw = true,
				Building = true
			};

			[JsonProperty(PropertyName = "Car Settings")]
			public CarConfig Car = new CarConfig
			{
				ActiveItems = new ActiveItemOptions
				{
					Disable = true,
					BlackList = new[]
					{
						"explosive.timed", "rocket.launcher", "surveycharge", "explosive.satchel"
					}
				},
				Radius = 7.5f,
				Text = "<size=15>{0}/{1}</size>",
				Color = "#C5D0E6",
				Delay = 0.75f
			};
		}

		private class CarConfig
		{
			[JsonProperty(PropertyName = "Active Items (in hand)")]
			public ActiveItemOptions ActiveItems;

			[JsonProperty(PropertyName = "DDraw Radius")]
			public float Radius;

			[JsonProperty(PropertyName = "DDraw Text")]
			public string Text;

			[JsonProperty(PropertyName = "DDraw Color")]
			public string Color;

			[JsonProperty(PropertyName = "DDraw Delay (sec)")]
			public float Delay;
		}

		public class ActiveItemOptions
		{
			[JsonProperty(PropertyName = "Forbid to hold all items")]
			public bool Disable;

			[JsonProperty(PropertyName = "List of blocked items (shortname)",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public string[] BlackList;
		}

		private class RecyclerConfig
		{
			[JsonProperty(PropertyName = "Recycling speed")]
			public float Speed;

			[JsonProperty(PropertyName = "Use DDraw? (showing damage on the recycler)")]
			public bool DDraw;

			[JsonProperty(PropertyName = "DDraw Radius")]
			public float Radius;

			[JsonProperty(PropertyName = "DDraw Text")]
			public string Text;

			[JsonProperty(PropertyName = "DDraw Color")]
			public string Color;

			[JsonProperty(PropertyName = "DDraw Delay (sec)")]
			public float Delay;

			[JsonProperty(PropertyName = "Enabled pickup?")]
			public bool Available;

			[JsonProperty(PropertyName = "Only owner can pickup")]
			public bool Owner;

			[JsonProperty(PropertyName = "Check ability to build for pickup")]
			public bool Building;

			[JsonProperty(PropertyName = "BaseProtection Settings",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public float[] Amounts;

			[JsonProperty(PropertyName = "Damage Scale")]
			public float Scale;
		}

		private class WorkbenchConfig
		{
			[JsonProperty(PropertyName = "Color")] public string Color;

			[JsonProperty(PropertyName = "Title")] public string Title;

			public WorkbenchConfig(string color, string title)
			{
				Color = color;
				Title = title;
			}
		}

		private class CraftConfig
		{
			[JsonProperty(PropertyName = "Enabled craft?")]
			public bool Enabled;

			[JsonProperty(PropertyName = "Image")] public string ImageURL;

			[JsonProperty(PropertyName = "Description")]
			public List<string> Description;

			[JsonProperty(PropertyName = "Command for give")]
			public string Command;

			[JsonProperty(PropertyName = "Permission for craft")]
			public string Permission;

			[JsonProperty(PropertyName = "DisplayName")]
			public string DisplayName;

			[JsonProperty(PropertyName = "Shortname")]
			public string ShortName;

			[JsonProperty(PropertyName = "Amount")]
			public int Amount;

			[JsonProperty(PropertyName = "Skin")] public ulong SkinID;

			[JsonProperty(PropertyName = "Type (Item/Command/Vehicle/Recycler)")]
			[JsonConverter(typeof(StringEnumConverter))]
			public CraftType Type;

			[JsonProperty(PropertyName = "Prefab (for Vehicle)")]
			public string Prefab;

			[JsonProperty(PropertyName = "Command on give")]
			public string GiveCommand;

			[JsonProperty(PropertyName = "Workbench Level")]
			public WorkbenchLevel Level;

			[JsonProperty(PropertyName = "Distance Check")]
			public bool UseDistance;

			[JsonProperty(PropertyName = "Distance")]
			public float Distance;

			[JsonProperty(PropertyName = "Place the ground")]
			public bool Ground;

			[JsonProperty(PropertyName = "Place the structure")]
			public bool Structure;

			[JsonProperty(PropertyName = "Items For Craft",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<ItemForCraft> Items;

			[JsonProperty(PropertyName = "For Modular Car")]
			public ModularCarConf Modular;

			public Item ToItem()
			{
				var newItem = ItemManager.CreateByName(ShortName, Amount, SkinID);
				if (newItem == null)
				{
					Debug.LogError($"Error creating item with ShortName: '{ShortName}'");
					return null;
				}

				if (!DisplayName.IsNullOrEmpty()) newItem.name = DisplayName;

				return newItem;
			}

			public void Give(BasePlayer player)
			{
				if (player == null) return;

				var item = ToItem();
				if (item == null) return;

				player.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
			}

			public void Spawn(BasePlayer player, Vector3 pos, Quaternion rot)
			{
				switch (Type)
				{
					case CraftType.ModularCar:
					{
						_instance?.SpawnModularCar?.Call("API_SpawnPresetCar", player, Modular.Get());
						break;
					}
					case CraftType.Vehicle:
					{
						var entity = GameManager.server.CreateEntity(Prefab, pos,
							Quaternion.Euler(0, player.GetNetworkRotation().eulerAngles.y - 90, 0));
						if (entity == null) return;

						entity.skinID = SkinID;
						entity.OwnerID = player.userID;
						entity.Spawn();
						break;
					}
					default:
					{
						var entity = GameManager.server.CreateEntity(Prefab, pos, rot);
						if (entity == null) return;

						entity.skinID = SkinID;
						entity.OwnerID = player.userID;
						entity.Spawn();
						break;
					}
				}
			}
		}

		private class ModularCarConf
		{
			[JsonProperty(PropertyName = "CodeLock")]
			public bool CodeLock;

			[JsonProperty(PropertyName = "KeyLock")]
			public bool KeyLock;

			[JsonProperty(PropertyName = "Engine Parts Tier")]
			public int EnginePartsTier;

			[JsonProperty(PropertyName = "Fresh Water Amount")]
			public int FreshWaterAmount;

			[JsonProperty(PropertyName = "Fuel Amount")]
			public int FuelAmount;

			[JsonProperty(PropertyName = "Modules", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public string[] Modules;

			public Dictionary<string, object> Get()
			{
				return new Dictionary<string, object>
				{
					["CodeLock"] = CodeLock,
					["KeyLock"] = KeyLock,
					["EnginePartsTier"] = EnginePartsTier,
					["FreshWaterAmount"] = FreshWaterAmount,
					["FuelAmount"] = FuelAmount,
					["Modules"] = Modules
				};
			}
		}

		private class ItemForCraft
		{
			[JsonProperty(PropertyName = "Shortname")]
			public string ShortName;

			[JsonProperty(PropertyName = "Amount")]
			public int Amount;

			[JsonProperty(PropertyName = "Skin")] public ulong SkinID;

			public ItemForCraft(string shortname, int amount, ulong skin)
			{
				ShortName = shortname;
				Amount = amount;
				SkinID = skin;
			}
		}

		protected override void LoadConfig()
		{
			base.LoadConfig();
			try
			{
				_config = Config.ReadObject<Configuration>();
				if (_config == null) throw new Exception();
				SaveConfig();
			}
			catch
			{
				PrintError("Your configuration file contains an error. Using default configuration values.");
				LoadDefaultConfig();
			}
		}

		protected override void SaveConfig()
		{
			Config.WriteObject(_config);
		}

		protected override void LoadDefaultConfig()
		{
			_config = new Configuration();
		}

		#endregion

		#region Hooks

		private void OnServerInitialized(bool initialized)
		{
			_instance = this;

			LoadImages();

			if (_config.CraftsList.Exists(x => x.Enabled && x.Type == CraftType.ModularCar) && !SpawnModularCar)
				PrintWarning("SpawnModularCar IS NOT INSTALLED.");

			_config.CraftsList.ForEach(item =>
			{
				if (!string.IsNullOrEmpty(item.Command))
					AddCovalenceCommand(item.Command, nameof(CmdGiveItem));

				if (!string.IsNullOrEmpty(item.Permission) && !permission.PermissionExists(item.Permission))
					permission.RegisterPermission(item.Permission, this);
			});

			if (initialized)
				foreach (var ent in BaseNetworkable.serverEntities.OfType<BaseEntity>())
					OnEntitySpawned(ent);

			AddCovalenceCommand(_config.Command, nameof(CmdChatOpenUI));
		}

		private void Unload()
		{
			foreach (var player in BasePlayer.activePlayerList)
				CuiHelper.DestroyUi(player, Layer);

			foreach (var component in UnityEngine.Object.FindObjectsOfType<RecyclerComponent>())
				if (component != null)
					component.Kill();

			foreach (var component in UnityEngine.Object.FindObjectsOfType<CarController>())
				if (component != null)
					component.Kill();

			_config = null;
			_instance = null;
		}

		private void OnEntityBuilt(Planner held, GameObject go)
		{
			if (held == null || go == null) return;

			var player = held.GetOwnerPlayer();
			if (player == null) return;

			var entity = go.ToBaseEntity();
			if (entity == null || entity.skinID == 0) return;

			var craft = _config.CraftsList.Find(x =>
				(x.Type == CraftType.Vehicle || x.Type == CraftType.Recycler || x.Type == CraftType.ModularCar) &&
				x.SkinID == entity.skinID);
			if (craft == null) return;

			var transform = entity.transform;

			var itemName = !string.IsNullOrEmpty(craft.DisplayName)
				? craft.DisplayName
				: ItemManager.FindItemDefinition(craft.ShortName)?.displayName.translated ?? "ITEM";

			NextTick(() =>
			{
				if (entity != null)
					entity.Kill();
			});

			RaycastHit rHit;
			if (Physics.Raycast(transform.position + new Vector3(0, 0.1f, 0), Vector3.down, out rHit, 4f,
				LayerMask.GetMask("Construction")) && rHit.GetEntity() != null)
			{
				if (!craft.Structure)
				{
					Reply(player, OnStruct, itemName);
					GiveCraft(player, craft);
					return;
				}
			}
			else
			{
				if (!craft.Ground)
				{
					Reply(player, OnGround, itemName);
					GiveCraft(player, craft);
					return;
				}
			}

			if (craft.UseDistance && Vector3.Distance(player.ServerPosition, transform.position) < craft.Distance)
			{
				Reply(player, BuildDistance, craft.Distance);
				GiveCraft(player, craft);
				return;
			}

			craft.Spawn(player, transform.position, transform.rotation);
		}

		private object CanResearchItem(BasePlayer player, Item item)
		{
			if (player == null || item == null ||
			    !_config.CraftsList.Exists(x => x.Type == CraftType.Vehicle && x.SkinID == item.skin)) return null;
			return false;
		}

		private void OnEntitySpawned(BaseEntity entity)
		{
			if (entity == null) return;

			if (entity is Recycler)
				entity.gameObject.AddComponent<RecyclerComponent>();

			if (entity is BasicCar)
				entity.gameObject.AddComponent<CarController>();
		}

		private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
		{
			if (entity == null || entity.OwnerID == 0) return;

			var recycler = entity.GetComponent<RecyclerComponent>();
			if (recycler != null)
			{
				info.damageTypes.ScaleAll(_config.Recycler.Scale);
				recycler.DDraw();
			}

			var car = entity.GetComponent<CarController>();
			if (car != null)
			{
				car.ManageDamage(info);
				car.DDraw();
			}
		}

		private object OnRecyclerToggle(Recycler recycler, BasePlayer player)
		{
			if (recycler == null || player == null) return null;

			var component = recycler.GetComponent<RecyclerComponent>();
			if (component == null) return null;

			if (!recycler.IsOn())
			{
				foreach (var obj in recycler.inventory.itemList)
					obj.CollectedForCrafting(player);

				component.StartRecycling();
			}
			else
			{
				component.StopRecycling();
			}

			return false;
		}

		private void OnHammerHit(BasePlayer player, HitInfo info)
		{
			if (player == null || info == null) return;

			var entity = info.HitEntity;
			if (entity == null) return;

			var component = entity.GetComponent<RecyclerComponent>();
			if (component == null) return;

			if (!_config.Recycler.Available)
			{
				Reply(player, NotTake);
				return;
			}

			component.TryPickup(player);
		}

		#endregion

		#region Commands

		[ConsoleCommand("UI_Crafts")]
		private void CmdConsoleCraft(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null) return;

			if (!arg.HasArgs())
			{
				DrawUI(player, isFirst: true);
				return;
			}

			switch (arg.Args[0].ToLower())
			{
				case "page":
				{
					int page;
					if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out page)) return;

					DrawUI(player, page);
					break;
				}
				case "craft":
				{
					int itemid;
					if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out itemid)
					                    || !(itemid >= 0 && _config.CraftsList.Count > itemid)) return;

					var craftItem = _config.CraftsList[itemid];
					if (craftItem == null) return;

					if (!HasWorkbench(player, craftItem.Level))
					{
						Reply(player, "NOT WORKBENCH");
						return;
					}

					var playerItems = player.inventory.containerMain.itemList.Concat(player.inventory.containerBelt.itemList).ToList();

					if (!HasAllItems(playerItems, craftItem))
					{
						Reply(player, "NOT.RESOURCES");
						return;
					}

					craftItem.Items.ForEach(item =>
					{
						if (item != null)
							Take(playerItems, item.ShortName, item.SkinID, item.Amount);
					});

					GiveCraft(player, craftItem);
					//CraftItem(player, craftItem);
					Reply(player, GIVECRAFT,
						!string.IsNullOrEmpty(craftItem.DisplayName)
							? craftItem.DisplayName
							: ItemManager.FindItemDefinition(craftItem.ShortName).displayName.translated);
					break;
				}
			}
		}

		private void CmdGiveItem(IPlayer iPlayer, string command, string[] args)
		{
			if (args.Length == 0) return;
			var player = BasePlayer.Find(args[0]);
			if (player == null)
			{
				Reply(iPlayer, "PLAYER NOT FOUND", args[0]);
				return;
			}

			var craftItem = _config.CraftsList.Find(x => x.Command == command);
			if (craftItem == null)
			{
				iPlayer.Reply("COMMAND NOT FOUND", command);
				return;
			}

			var item = craftItem.ToItem();
			if (item == null)
				return;

			var itemName = !string.IsNullOrEmpty(craftItem.DisplayName)
				? craftItem.DisplayName
				: item.info.displayName.translated;

			player.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
			Reply(player, GIVECRAFT, itemName);

			if (_config.useDebug) Reply(iPlayer, "GIVE.DEBUG", player.displayName, player.UserIDString, itemName);
		}

		private void CmdChatOpenUI(IPlayer cov, string command, string[] args)
		{
			var player = cov?.Object as BasePlayer;
			if (player == null) return;

			DrawUI(player, isFirst: true);
		}

		#endregion

		#region Interface

		private void DrawUI(BasePlayer player, int page = 0, bool isFirst = false)
		{
			var container = new CuiElementContainer();

			var playerItems = player.inventory.containerMain.itemList.Concat(player.inventory.containerBelt.itemList).ToList();

			#region First

			if (isFirst)
			{
				CuiHelper.DestroyUi(player, Layer);

				#region BG

				container.Add(new CuiPanel
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Image =
					{
						Color = "0.1 0.1 0.05 0.75",
						Sprite = "assets/content/ui/ui.background.transparent.radial.psd",
						Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
					},
					CursorEnabled = true
				}, "Overlay", Layer);

				#endregion

				#region Title

				container.Add(new CuiLabel
				{
					RectTransform =
						{AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-200 200", OffsetMax = "200 300"},
					Text =
					{
						Text = lang.GetMessage("Title", this, player.UserIDString), Align = TextAnchor.MiddleCenter,
						FontSize = 28
					}
				}, Layer);

				#endregion
			}

			#endregion

			#region Main

			container.Add(new CuiPanel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Image = {Color = "0 0 0 0"}
			}, Layer, Layer + ".Main");

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "1 1", AnchorMax = "1 1",
					OffsetMin = "-45 -45", OffsetMax = "-5 -5"
				},
				Text =
				{
					Text = "✕",
					Align = TextAnchor.MiddleCenter,
					FontSize = 28,
					Color = "1 1 1 1"
				},
				Button =
				{
					Color = "0 0 0 0",
					Close = Layer
				}
			}, Layer + ".Main");

			#region Items

			var list = GetPlayerCrafts(player, page);

			if (list.Count > 0)
			{
				var xSwitch = -(220 * list.Count + 40 * (list.Count - 1)) / 2;

				for (var i = 0; i < list.Count; i++)
				{
					var craft = list[i];

					container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"{xSwitch} -100",
							OffsetMax = $"{xSwitch + 220} 200"
						},
						Image = {Color = "0 0 0 0.5", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"}
					}, Layer + ".Main", Layer + $".Craft.{xSwitch}");

					container.Add(new CuiElement
					{
						Parent = Layer + $".Craft.{xSwitch}",
						Components =
						{
							new CuiRawImageComponent {Png = ImageLibrary.Call<string>("GetImage", craft.ImageURL)},
							new CuiRectTransformComponent
							{
								AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-70 0", OffsetMax = "70 140"
							}
						}
					});

					container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-85 -10", OffsetMax = "85 -5"
						},
						Image = {Color = "1 1 1 1", Sprite = "assets/content/ui/gameui/compass/alpha_mask.png"}
					}, Layer + $".Craft.{xSwitch}");

					container.Add(new CuiLabel
					{
						RectTransform =
							{AnchorMin = "0 0.5", AnchorMax = "1 0.5", OffsetMin = "0 -130", OffsetMax = "0 -25"},
						Text =
						{
							Text = string.Join("\n", craft.Description), Align = TextAnchor.UpperCenter,
							FontSize = 14
						}
					}, Layer + $".Craft.{xSwitch}");

					container.Add(new CuiPanel
					{
						RectTransform =
							{AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 -50", OffsetMax = "0 -5"},
						Image =
						{
							Color = HexToCuiColor(_config.Workbenchs[craft.Level].Color),
							Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
						}
					}, Layer + $".Craft.{xSwitch}", Layer + $".Craft.{xSwitch}.Workbench");

					container.Add(new CuiLabel
					{
						RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
						Text =
						{
							Text = _config.Workbenchs[craft.Level].Title, Align = TextAnchor.MiddleCenter,
							FontSize = 14
						}
					}, Layer + $".Craft.{xSwitch}.Workbench");

					var active = HasAllItems(playerItems, craft) && HasWorkbench(player, craft.Level);
					container.Add(new CuiButton
					{
						RectTransform =
							{AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 -105", OffsetMax = "0 -55"},
						Button =
						{
							Command = active ? $"UI_Crafts craft {_config.CraftsList.IndexOf(craft)}" : "",
							Color = active ? HexToCuiColor(_config.GreenColor) : "0 0 0 0.5",
							Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
							Close = active ? Layer : ""
						},
						Text =
						{
							Text = lang.GetMessage("Создать", this, player.UserIDString),
							Align = TextAnchor.MiddleCenter, FontSize = 24
						}
					}, Layer + $".Craft.{xSwitch}");

					xSwitch += 260;
				}
			}
			else
			{
				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-250 -150", OffsetMax = "250 150"
					},
					Text =
					{
						Text = lang.GetMessage("NOT CRAFTS", this, player.UserIDString),
						Align = TextAnchor.MiddleCenter, FontSize = 34
					}
				}, Layer + ".Main");
			}

			#endregion

			#region Pages

			if (list.Count > 0)
			{
				container.Add(new CuiButton
				{
					RectTransform =
						{AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "5 -25", OffsetMax = "55 25"},
					Button =
					{
						Command = page > 0 ? $"UI_Crafts page {page - 1}" : "",
						Color = "0 0 0 0"
					},
					Text =
					{
						Text = "<", Align = TextAnchor.MiddleCenter, FontSize = 40,
						Color = page > 0 ? "1 1 1 1" : "1 1 1 0.5"
					}
				}, Layer + ".Main");

				var count = _config.CraftsList.Count(craft => craft.Enabled &&
				                                              (string.IsNullOrEmpty(craft.Permission) ||
				                                               permission.UserHasPermission(player.UserIDString,
					                                               craft.Permission)));

				container.Add(new CuiButton
				{
					RectTransform =
						{AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-55 -25", OffsetMax = "-5 25"},
					Button =
					{
						Command = count > (page + 1) * 3 ? $"UI_Crafts page {page + 1}" : "",
						Color = "0 0 0 0"
					},
					Text =
					{
						Text = ">", Align = TextAnchor.MiddleCenter, FontSize = 40,
						Color = count > (page + 1) * 3 ? "1 1 1 1" : "1 1 1 0.5"
					}
				}, Layer + ".Main");
			}

			#endregion

			#endregion

			CuiHelper.DestroyUi(player, Layer + ".Main");
			CuiHelper.AddUi(player, container);
		}

		#endregion

		#region Utils

		private void GiveCraft(BasePlayer player, CraftConfig cfg)
		{
			switch (cfg.Type)
			{
				case CraftType.Command:
				{
					var command = cfg.GiveCommand.Replace("\n", "|")
						.Replace("%steamid%", player.UserIDString, StringComparison.OrdinalIgnoreCase).Replace(
							"%username%",
							player.displayName, StringComparison.OrdinalIgnoreCase);

					foreach (var check in command.Split('|')) Server.Command(check);
					break;
				}
				default:
				{
					cfg.Give(player);
					break;
				}
			}
		}

        private void CraftItem(BasePlayer player, CraftConfig item, ItemCrafter crafter)
        {
            var defenition = ItemManager.FindItemDefinition(item.ShortName);

            var task = Pool.Get<ItemCraftTask>();
            task.blueprint = defenition.Blueprint;
            task.endTime = 0.0f;
            task.taskUID = player.inventory.crafting.taskUID + 1;
            crafter.owner = player;
            task.instanceData = null;
            if (task.instanceData != null)
                task.instanceData.ShouldPool = false;
            task.amount = 1;
            task.skinID = (int) item.SkinID;

            player.inventory.crafting.queue.AddLast(task);
            if (crafter.owner != null)
                crafter.owner.Command("note.craft_add", task.taskUID, task.blueprint.targetItem.itemid,
                    1, task.skinID);
        }

		private static bool HasWorkbench(BasePlayer player, WorkbenchLevel level)
		{
			return level == WorkbenchLevel.Three ? player.HasPlayerFlag(BasePlayer.PlayerFlags.Workbench3)
				: level == WorkbenchLevel.Two ? player.HasPlayerFlag(BasePlayer.PlayerFlags.Workbench3) ||
				                                player.HasPlayerFlag(BasePlayer.PlayerFlags.Workbench2)
				: level == WorkbenchLevel.One ? player.HasPlayerFlag(BasePlayer.PlayerFlags.Workbench3) ||
				                                player.HasPlayerFlag(BasePlayer.PlayerFlags.Workbench2) ||
				                                player.HasPlayerFlag(BasePlayer.PlayerFlags.Workbench1)
				: level == WorkbenchLevel.None;
		}

		private static bool HasAllItems(IReadOnlyList<Item> items, CraftConfig craftConfig)
		{
			for (var i = 0; i < craftConfig.Items.Count; i++)
			{
				var itemForCraft = craftConfig.Items[i];

				if (ItemCount(items, itemForCraft.ShortName, itemForCraft.SkinID) < itemForCraft.Amount) return false;
			}

			return true;
		}

		private static int ItemCount(IReadOnlyList<Item> items, string shortname, ulong skin)
		{
			var result = 0;

			for (var i = 0; i < items.Count; i++)
			{
				var item = items[i];
				if (item.info.shortname == shortname && (skin == 0 || item.skin == skin))
					result += item.amount;
			}

			return result;
		}

		private void Take(IEnumerable<Item> itemList, string shortname, ulong skinId, int iAmount)
		{
			var num1 = 0;
			if (iAmount == 0) return;

			var list = Pool.GetList<Item>();

			foreach (var item in itemList)
			{
				if (item.info.shortname != shortname ||
				    skinId != 0 && item.skin != skinId) continue;

				var num2 = iAmount - num1;
				if (num2 <= 0) continue;
				if (item.amount > num2)
				{
					item.MarkDirty();
					item.amount -= num2;
					num1 += num2;
					break;
				}

				if (item.amount <= num2)
				{
					num1 += item.amount;
					list.Add(item);
				}

				if (num1 == iAmount)
					break;
			}

			foreach (var obj in list)
				obj.RemoveFromContainer();

			Pool.FreeList(ref list);
		}

		private static string HexToCuiColor(string hex)
		{
			if (string.IsNullOrEmpty(hex)) hex = "#FFFFFFFF";

			var str = hex.Trim('#');

			if (str.Length == 6)
				str += "FF";

			if (str.Length != 8) throw new Exception(hex);

			var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
			var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
			var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);
			var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);
			Color color = new Color32(r, g, b, a);
			return $"{color.r:F2} {color.g:F2} {color.b:F2} {color.a:F2}";
		}

		private List<CraftConfig> GetPlayerCrafts(BasePlayer player, int page, int count = 3)
		{
			return _config.CraftsList
				.FindAll(craft => craft.Enabled && (string.IsNullOrEmpty(craft.Permission) ||
				                                    permission.UserHasPermission(player.UserIDString,
					                                    craft.Permission)))
				.Skip(page * count).Take(count)
				.ToList();
		}

		private static Color HexToUnityColor(string hex)
		{
			if (string.IsNullOrEmpty(hex)) hex = "#FFFFFFFF";

			var str = hex.Trim('#');

			if (str.Length == 6)
				str += "FF";

			if (str.Length != 8) throw new Exception(hex);

			var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
			var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
			var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);
			var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);

			Color color = new Color32(r, g, b, a);

			return color;
		}

		private static void SetPlayerFlag(BasePlayer player, BasePlayer.PlayerFlags f, bool b)
		{
			if (b)
			{
				if (player.HasPlayerFlag(f)) return;
				player.playerFlags |= f;
			}
			else
			{
				if (!player.HasPlayerFlag(f)) return;
				player.playerFlags &= ~f;
			}

			player.SendNetworkUpdateImmediate();
		}

		private void LoadImages()
		{
			timer.In(5f, () =>
			{
				if (!ImageLibrary)
				{
					PrintWarning("IMAGE LIBRARY IS NOT INSTALLED.");
				}
				else
				{
					var imagesList = new Dictionary<string, string>();

					_config.CraftsList.ForEach(item =>
					{
						if (!string.IsNullOrEmpty(item.ImageURL) && !imagesList.ContainsKey(item.ImageURL))
							imagesList.Add(item.ImageURL, item.ImageURL);
					});

					ImageLibrary?.Call("ImportImageList", Title, imagesList, 0UL, true);
				}
			});
		}

		#endregion

		#region Recycler Component

		private class RecyclerComponent : FacepunchBehaviour
		{
			private Recycler recycler;

			private GroundWatch groundWatch;
			private DestroyOnGroundMissing groundMissing;

			[NonSerialized] private readonly BaseEntity[] SensesResults = new BaseEntity[64];

			private void Awake()
			{
				recycler = GetComponent<Recycler>();

				if (recycler.OwnerID != 0)
				{
					recycler.baseProtection = ScriptableObject.CreateInstance<ProtectionProperties>();
					recycler.baseProtection.amounts = _config.Recycler.Amounts;

					groundWatch = recycler.GetOrAddComponent<GroundWatch>();

					groundMissing = recycler.GetOrAddComponent<DestroyOnGroundMissing>();
				}
			}

			public void DDraw()
			{
				if (recycler == null)
				{
					Kill();
					return;
				}

				if (recycler.OwnerID == 0 || !_config.Recycler.DDraw)
					return;

				var inSphere = BaseEntity.Query.Server.GetInSphere(recycler.transform.position, _config.Recycler.Radius,
					SensesResults, entity => entity is BasePlayer);
				if (inSphere == 0)
					return;

				for (var i = 0; i < inSphere; i++)
				{
					var user = SensesResults[i] as BasePlayer;
					if (user == null || user.IsDestroyed || !user.IsConnected || user.IsNpc ||
					    !user.userID.IsSteamId()) continue;

					if (user.Connection.authLevel < 2) SetPlayerFlag(user, BasePlayer.PlayerFlags.IsAdmin, true);

					user.SendConsoleCommand("ddraw.text", _config.Recycler.Delay,
						HexToUnityColor(_config.Recycler.Color),
						recycler.transform.position + Vector3.up,
						string.Format(_config.Recycler.Text, recycler.health, recycler._maxHealth));

					if (user.Connection.authLevel < 2) SetPlayerFlag(user, BasePlayer.PlayerFlags.IsAdmin, false);
				}
			}

			#region Methods

			public void StartRecycling()
			{
				if (recycler.IsOn())
					return;

				InvokeRepeating(RecycleThink, _config.Recycler.Speed, _config.Recycler.Speed);
				Effect.server.Run(recycler.startSound.resourcePath, recycler, 0U, Vector3.zero, Vector3.zero);
				recycler.SetFlag(BaseEntity.Flags.On, true);

				recycler.SendNetworkUpdateImmediate();
			}

			public void StopRecycling()
			{
				CancelInvoke(RecycleThink);

				if (!recycler.IsOn())
					return;

				Effect.server.Run(recycler.stopSound.resourcePath, recycler, 0U, Vector3.zero, Vector3.zero);
				recycler.SetFlag(BaseEntity.Flags.On, false);
				recycler.SendNetworkUpdateImmediate();
			}

			public void RecycleThink()
			{
				var flag = false;
				var num1 = recycler.recycleEfficiency;
				for (var slot1 = 0; slot1 < 6; ++slot1)
				{
					var slot2 = recycler.inventory.GetSlot(slot1);
					if (slot2 != null)
					{
						if (Interface.CallHook("OnRecycleItem", recycler, slot2) != null)
						{
							if (HasRecyclable())
								return;
							StopRecycling();
							return;
						}

						if (slot2.info.Blueprint != null)
						{
							if (slot2.hasCondition)
								num1 = Mathf.Clamp01(
									num1 * Mathf.Clamp(slot2.conditionNormalized * slot2.maxConditionNormalized, 0.1f,
										1f));
							var num2 = 1;
							if (slot2.amount > 1)
								num2 = Mathf.CeilToInt(Mathf.Min(slot2.amount, slot2.info.stackable * 0.1f));
							if (slot2.info.Blueprint.scrapFromRecycle > 0)
							{
								var iAmount = slot2.info.Blueprint.scrapFromRecycle * num2;
								if (slot2.info.stackable == 1 && slot2.hasCondition)
									iAmount = Mathf.CeilToInt(iAmount * slot2.conditionNormalized);
								if (iAmount >= 1)
									recycler.MoveItemToOutput(ItemManager.CreateByName("scrap", iAmount));
							}

							if (!string.IsNullOrEmpty(slot2.info.Blueprint.RecycleStat))
							{
								var list = Pool.GetList<BasePlayer>();
								Vis.Entities(transform.position, 3f, list, 131072);
								foreach (var basePlayer in list)
									if (basePlayer.IsAlive() && !basePlayer.IsSleeping() &&
									    basePlayer.inventory.loot.entitySource == recycler)
									{
										basePlayer.stats.Add(slot2.info.Blueprint.RecycleStat, num2,
											Stats.Steam | Stats.Life);
										basePlayer.stats.Save();
									}

								Pool.FreeList(ref list);
							}

							slot2.UseItem(num2);
							using (var enumerator = slot2.info.Blueprint.ingredients.GetEnumerator())
							{
								while (enumerator.MoveNext())
								{
									var current = enumerator.Current;
									if (current != null && current.itemDef.shortname != "scrap")
									{
										var num3 = current.amount / slot2.info.Blueprint.amountToCreate;
										var num4 = 0;
										if (num3 <= 1.0)
										{
											for (var index = 0; index < num2; ++index)
												if (Random.Range(0.0f, 1f) <= num3 * (double) num1)
													++num4;
										}
										else
										{
											num4 = Mathf.CeilToInt(
												Mathf.Clamp(num3 * num1 * Random.Range(1f, 1f), 0.0f, current.amount) *
												num2);
										}

										if (num4 > 0)
										{
											var num5 = Mathf.CeilToInt(num4 / (float) current.itemDef.stackable);
											for (var index = 0; index < num5; ++index)
											{
												var iAmount = num4 > current.itemDef.stackable
													? current.itemDef.stackable
													: num4;
												if (!recycler.MoveItemToOutput(ItemManager.Create(current.itemDef,
													iAmount)))
													flag = true;
												num4 -= iAmount;
												if (num4 <= 0)
													break;
											}
										}
									}
								}

								break;
							}
						}
					}
				}

				if (!flag && HasRecyclable())
					return;
				StopRecycling();
			}

			public bool HasRecyclable()
			{
				for (var slot1 = 0; slot1 < 6; ++slot1)
				{
					var slot2 = recycler.inventory.GetSlot(slot1);
					if (slot2 != null)
					{
						var can = Interface.CallHook("CanRecycle", recycler, slot2);
						if (can is bool)
							return (bool) can;

						if (slot2.info.Blueprint != null)
							return true;
					}
				}

				return false;
			}

			#endregion

			#region Destroy

			public void TryPickup(BasePlayer player)
			{
				if (_config.Recycler.Building && !player.CanBuild())
				{
					_instance.Reply(player, CantBuild);
					return;
				}

				if (_config.Recycler.Owner && recycler.OwnerID != player.userID)
				{
					_instance.Reply(player, OnlyOwner);
					return;
				}

				if (recycler.SecondsSinceDealtDamage < 30f)
				{
					_instance.Reply(player, RecentlyDamaged);
					return;
				}

				recycler.Kill();

				var craft = _config.CraftsList.Find(x => x.Type == CraftType.Recycler);
				if (craft == null)
				{
					_instance.Reply(player, CannotGive);
					return;
				}

				_instance?.GiveCraft(player, craft);
			}

			private void OnDestroy()
			{
				CancelInvoke();

				Destroy(this);
			}

			public void Kill()
			{
				Destroy(this);
			}

			#endregion
		}

		#endregion

		#region Car Component

		public class CarController : FacepunchBehaviour
		{
			public BasicCar entity;
			public BasePlayer player;
			public bool isDieing;

			private bool allowHeldItems;
			private string[] disallowedItems;

			[NonSerialized] private readonly BaseEntity[] SensesResults = new BaseEntity[64];

			private void Awake()
			{
				entity = GetComponent<BasicCar>();

				allowHeldItems = !_config.Car.ActiveItems.Disable;
				disallowedItems = _config.Car.ActiveItems.BlackList;
			}

			private void Update()
			{
				UpdateHeldItems();
				CheckWaterLevel();
			}

			public void ManageDamage(HitInfo info)
			{
				if (isDieing)
				{
					NullifyDamage(info);
					return;
				}

				if (info.damageTypes.GetMajorityDamageType() == DamageType.Bullet)
					info.damageTypes.ScaleAll(200);

				if (info.damageTypes.Total() >= entity.health)
				{
					isDieing = true;
					NullifyDamage(info);
					OnDeath();
				}
			}

			public void DDraw()
			{
				if (entity == null)
				{
					Kill();
					return;
				}

				if (entity.OwnerID == 0)
					return;

				var inSphere = BaseEntity.Query.Server.GetInSphere(entity.transform.position, _config.Car.Radius,
					SensesResults, ent => ent is BasePlayer);
				if (inSphere == 0)
					return;

				for (var i = 0; i < inSphere; i++)
				{
					var user = SensesResults[i] as BasePlayer;
					if (user == null || user.IsDestroyed || !user.IsConnected || user.IsNpc ||
					    !user.userID.IsSteamId()) continue;

					if (user.Connection.authLevel < 2) SetPlayerFlag(user, BasePlayer.PlayerFlags.IsAdmin, true);

					user.SendConsoleCommand("ddraw.text", _config.Car.Delay, HexToUnityColor(_config.Car.Color),
						entity.transform.position + new Vector3(0.25f, 1, 0),
						string.Format(_config.Car.Text, entity.health, entity._maxHealth));

					if (user.Connection.authLevel < 2) SetPlayerFlag(user, BasePlayer.PlayerFlags.IsAdmin, false);
				}
			}

			private void NullifyDamage(HitInfo info)
			{
				info.damageTypes = new DamageTypeList();
				info.HitEntity = null;
				info.HitMaterial = 0;
				info.PointStart = Vector3.zero;
			}

			public void UpdateHeldItems()
			{
				if (player == null)
					return;

				var item = player.GetActiveItem();
				if (item == null || item.GetHeldEntity() == null)
					return;

				if (disallowedItems.Contains(item.info.shortname) || !allowHeldItems)
				{
					_instance?.Reply(player, ItemNotAllowed);

					var slot = item.position;
					item.SetParent(null);
					item.MarkDirty();

					Invoke(() =>
					{
						if (player == null) return;
						item.SetParent(player.inventory.containerBelt);
						item.position = slot;
						item.MarkDirty();
					}, 0.15f);
				}
			}

			public void CheckWaterLevel()
			{
				if (WaterLevel.Factor(entity.WorldSpaceBounds().ToBounds(), true, true) > 0.7f)
					StopToDie();
			}

			public void StopToDie(bool death = true)
			{
				if (entity != null)
				{
					entity.SetFlag(BaseEntity.Flags.Reserved1, false);

					foreach (var wheel in entity.wheels)
					{
						wheel.wheelCollider.motorTorque = 0;
						wheel.wheelCollider.brakeTorque = float.MaxValue;
					}

					entity.GetComponent<Rigidbody>().velocity = Vector3.zero;

					if (player != null)
						entity.DismountPlayer(player);
				}

				if (death) OnDeath();
			}

			private void OnDeath()
			{
				isDieing = true;

				if (player != null)
					player.EnsureDismounted();

				Invoke(() =>
				{
					Effect.server.Run("assets/prefabs/npc/patrol helicopter/effects/heli_explosion.prefab",
						transform.position);
					_instance.NextTick(() =>
					{
						if (entity != null && !entity.IsDestroyed)
							entity.DieInstantly();
						Destroy(this);
					});
				}, 5f);
			}

			public void Kill()
			{
				StopToDie(false);
				Destroy(this);
			}
		}

		#endregion

		#region Lang

		private const string
			NOTRESOURCES = "NOT.RESOURCES",
			PLAYERNOTFOUND = "PLAYER NOT FOUND",
			COMMANDNOTFOUND = "COMMAND NOT FOUND",
			GIVECRAFT = "GIVECRAFT",
			GIVEDEBUG = "GIVE.DEBUG",
			NOTWORKBENCH = "NOT WORKBENCH",
			NOTCRAFTS = "NOT CRAFTS",
			CREATE = "CREATE",
			TitleMain = "Title",
			OnGround = "OnGround",
			BuildDistance = "BuildDistance",
			OnStruct = "OnStruct",
			NotTake = "NotTake",
			ItemNotAllowed = "ItemNotAllowed",
			CantBuild = "CantBuild",
			OnlyOwner = "OnlyOwner",
			RecentlyDamaged = "RecentlyDamaged",
			CannotGive = "CannotGive";

		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
				[NOTRESOURCES] = "Not enough resources",
				[PLAYERNOTFOUND] = "Player {0} not found",
				[COMMANDNOTFOUND] = "Command {0} not found",
				[GIVECRAFT] = "Congratulations! You got a {0}",
				[GIVEDEBUG] = "Player {0} ({1}) is granted: {2}",
				[NOTWORKBENCH] = "Not enough workbench level for craft!",
				[NOTCRAFTS] = "There's no craft available for you",
				[CREATE] = "<b>Создать</b>",
				[TitleMain] = "<b>Меню крафта</b>",
				[OnGround] = "{0} can't put it on the ground!",
				[BuildDistance] = "Built closer than {0}m is blocked!",
				[OnStruct] = "{0} can't put on the buildings!",
				[NotTake] = "Pickup disabled!",
				[ItemNotAllowed] = "Item blocked!",
				[CantBuild] = "You must have the permission to build.",
				[OnlyOwner] = "Only the owner can pick up the recycler!",
				[RecentlyDamaged] = "The recycler has recently been damaged, you can take it in 30 seconds!",
				[CannotGive] = "Call the administrator. The recycler cannot be give"
			}, this);
		}

		private void Reply(BasePlayer player, string key, params object[] obj)
		{
			SendReply(player, string.Format(lang.GetMessage(key, this, player.UserIDString), obj));
		}

		private void Reply(IPlayer player, string key, params object[] obj)
		{
			player.Reply(string.Format(lang.GetMessage(key, this, player.Id), obj));
		}

		#endregion
	}
}