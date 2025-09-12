using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
	[Info("Cases", "Netrunner", "1.0.0")]
	public class Cases : RustPlugin
	{
		#region Fields

		[PluginReference] private Plugin
			ImageLibrary = null,
			PlaytimeTracker = null,
			UINotify = null,
			Notify = null;

		private const string Layer = "UI.Cases";

		private const string ModalLayer = "UI.Cases.Modal";

		private static Cases _instance;

		private const BindingFlags bindingFlags = BindingFlags.Instance |
		                                          BindingFlags.NonPublic |
		                                          BindingFlags.Public;

		private const string PERM_EDIT = "Cases.edit";

		private readonly Dictionary<int, CaseInfo> _casesByIDs = new Dictionary<int, CaseInfo>();

		private readonly Dictionary<string, List<KeyValuePair<int, string>>> _itemsCategories =
			new Dictionary<string, List<KeyValuePair<int, string>>>();

		private readonly Dictionary<BasePlayer, PlayerItemsData> _playersItems =
			new Dictionary<BasePlayer, PlayerItemsData>();

		private class PlayerItemsData
		{
			public readonly int CaseId;

			public readonly List<ItemInfo> Items;

			public PlayerItemsData(int caseId, List<ItemInfo> items)
			{
				CaseId = caseId;
				Items = items;
			}
		}

		private readonly Dictionary<int, ItemInfo> _itemByIDs = new Dictionary<int, ItemInfo>();

		private readonly Dictionary<BasePlayer, float> TimePlayers = new Dictionary<BasePlayer, float>();

		#endregion

		#region Config

		private Configuration _config;

		private class Configuration
		{
			[JsonProperty(PropertyName = "Enable item scrolling?")]
			public readonly bool Scrolling = true;

			[JsonProperty(PropertyName = "Work with Notify?")]
			public readonly bool UseNotify = true;

			[JsonProperty(PropertyName = "Permission (ex: cases.use)")]
			public string Permission = string.Empty;

			[JsonProperty(PropertyName = "Amount of items in the roulette")]
			public readonly int AmountItems = 26;

			[JsonProperty(PropertyName = "Commands", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public readonly string[] Commands = {"cases", "opencase"};

			[JsonProperty(PropertyName = "Economy")]
			public EconomyConf Economy = new EconomyConf
			{
				Type = EconomyType.Plugin,
				AddHook = "Deposit",
				BalanceHook = "Balance",
				RemoveHook = "Withdraw",
				Plug = "Economics",
				ShortName = "scrap",
				DisplayName = string.Empty,
				Skin = 0,
				Show = true
			};

			[JsonProperty(PropertyName = "Rarity Colors (chance - color)",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public readonly Dictionary<float, IColor> Rarity = new Dictionary<float, IColor>
			{
				[70] = new IColor("#AFAFAF", 75),
				[65] = new IColor("#6496E1", 75),
				[55] = new IColor("#4B69CD", 75),
				[50] = new IColor("#8847FF", 75),
				[45] = new IColor("#8847FF", 75),
				[40] = new IColor("#8847FF", 75),
				[35] = new IColor("#8847FF", 75),
				[30] = new IColor("#8847FF", 75),
				[25] = new IColor("#8847FF", 75),
				[20] = new IColor("#D32CE6", 75),
				[15] = new IColor("#D32CE6", 75),
				[10] = new IColor("#D32CE6", 75),
				[5] = new IColor("#EB4B4B", 75)
			};

			[JsonProperty(PropertyName = "Playtime Tracker Settings")]
			public PlaytimeTrackerSettings PlaytimeTrackerConf = new PlaytimeTrackerSettings
			{
				Enabled = true,
				Cases = new Dictionary<int, int>
				{
					[3600] = 1,
					[14400] = 2,
					[28800] = 3,
					[86400] = 4
				}
			};

			[JsonProperty(PropertyName = "Cases for time settings")]
			public TimeCasesSettings TimeCasesSettings = new TimeCasesSettings
			{
				Enable = false,
				Cooldown = 14400,
				Cases = new List<int>
				{
					1,
					2,
					3,
					4,
					5
				}
			};

			[JsonProperty(PropertyName = "Bonus Case")]
			public BonusCaseInfo BonusCaseInfo = new BonusCaseInfo
			{
				Enabled = true,
				ID = -1,
				Image = "https://i.imgur.com/n4I3vI0.png",
				Permission = string.Empty,
				CooldownTime = 86400,
				Cost = 0,
				CustomCurrency = new CustomCurrency
				{
					Enabled = false,
					CostFormat = "{0} scrap",
					Type = EconomyType.Item,
					Plug = string.Empty,
					AddHook = string.Empty,
					RemoveHook = string.Empty,
					BalanceHook = string.Empty,
					ShortName = "scrap",
					DisplayName = string.Empty,
					Skin = 0
				},
				Items = new List<ItemInfo>
				{
					new ItemInfo
					{
						Type = ItemType.Item,
						ID = 1,
						Image = string.Empty,
						Title = string.Empty,
						Command = string.Empty,
						Plugin = new PluginItem(),
						DisplayName = string.Empty,
						ShortName = "wood",
						Skin = 0,
						Amount = 3500,
						Chance = 70
					},
					new ItemInfo
					{
						Type = ItemType.Item,
						ID = 2,
						Image = string.Empty,
						Title = string.Empty,
						Command = string.Empty,
						Plugin = new PluginItem(),
						DisplayName = string.Empty,
						ShortName = "stones",
						Skin = 0,
						Amount = 2500,
						Chance = 70
					},
					new ItemInfo
					{
						Type = ItemType.Item,
						ID = 3,
						Image = string.Empty,
						Title = string.Empty,
						Command = string.Empty,
						Plugin = new PluginItem(),
						DisplayName = string.Empty,
						ShortName = "leather",
						Skin = 0,
						Amount = 1000,
						Chance = 55
					},
					new ItemInfo
					{
						Type = ItemType.Item,
						ID = 4,
						Image = string.Empty,
						Title = string.Empty,
						Command = string.Empty,
						Plugin = new PluginItem(),
						DisplayName = string.Empty,
						ShortName = "cloth",
						Skin = 0,
						Amount = 1000,
						Chance = 55
					},
					new ItemInfo
					{
						Type = ItemType.Item,
						ID = 5,
						Image = string.Empty,
						Title = string.Empty,
						Command = string.Empty,
						Plugin = new PluginItem(),
						DisplayName = string.Empty,
						ShortName = "lowgradefuel",
						Skin = 0,
						Amount = 500,
						Chance = 50
					},
					new ItemInfo
					{
						Type = ItemType.Item,
						ID = 6,
						Image = string.Empty,
						Title = string.Empty,
						Command = string.Empty,
						Plugin = new PluginItem(),
						DisplayName = string.Empty,
						ShortName = "metal.fragments",
						Skin = 0,
						Amount = 1500,
						Chance = 65
					},
					new ItemInfo
					{
						Type = ItemType.Item,
						ID = 7,
						Image = string.Empty,
						Title = string.Empty,
						Command = string.Empty,
						Plugin = new PluginItem(),
						DisplayName = string.Empty,
						ShortName = "metal.refined",
						Skin = 0,
						Amount = 150,
						Chance = 65
					},
					new ItemInfo
					{
						Type = ItemType.Item,
						ID = 8,
						Image = string.Empty,
						Title = string.Empty,
						Command = string.Empty,
						Plugin = new PluginItem(),
						DisplayName = string.Empty,
						ShortName = "sulfur",
						Skin = 0,
						Amount = 2500,
						Chance = 55
					},
					new ItemInfo
					{
						Type = ItemType.Item,
						ID = 9,
						Image = string.Empty,
						Title = string.Empty,
						Command = string.Empty,
						Plugin = new PluginItem(),
						DisplayName = string.Empty,
						ShortName = "gunpowder",
						Skin = 0,
						Amount = 1500,
						Chance = 45
					},
					new ItemInfo
					{
						Type = ItemType.Item,
						ID = 10,
						Image = string.Empty,
						Title = string.Empty,
						Command = string.Empty,
						Plugin = new PluginItem(),
						DisplayName = string.Empty,
						ShortName = "explosive.timed",
						Skin = 0,
						Amount = 1,
						Chance = 5
					}
				}
			};

			[JsonProperty(PropertyName = "Cases", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<CaseInfo> Cases = new List<CaseInfo>
			{
				new CaseInfo
				{
					ID = 1,
					Image = "https://i.imgur.com/0p9qwot.png",
					Permission = string.Empty,
					Cost = 100,
					CustomCurrency = new CustomCurrency
					{
						Enabled = false,
						CostFormat = "{0} scrap",
						Type = EconomyType.Item,
						Plug = string.Empty,
						AddHook = string.Empty,
						RemoveHook = string.Empty,
						BalanceHook = string.Empty,
						ShortName = "scrap",
						DisplayName = string.Empty,
						Skin = 0
					},
					Items = new List<ItemInfo>
					{
						new ItemInfo
						{
							Type = ItemType.Item,
							ID = 11,
							Image = string.Empty,
							Title = string.Empty,
							Command = string.Empty,
							Plugin = new PluginItem(),
							DisplayName = string.Empty,
							ShortName = "metalblade",
							Skin = 0,
							Amount = 40,
							Chance = 50
						},
						new ItemInfo
						{
							Type = ItemType.Item,
							ID = 12,
							Image = string.Empty,
							Title = string.Empty,
							Command = string.Empty,
							Plugin = new PluginItem(),
							DisplayName = string.Empty,
							ShortName = "sewingkit",
							Skin = 0,
							Amount = 30,
							Chance = 50
						},
						new ItemInfo
						{
							Type = ItemType.Item,
							ID = 14,
							Image = string.Empty,
							Title = string.Empty,
							Command = string.Empty,
							Plugin = new PluginItem(),
							DisplayName = string.Empty,
							ShortName = "roadsigns",
							Skin = 0,
							Amount = 15,
							Chance = 50
						},
						new ItemInfo
						{
							Type = ItemType.Item,
							ID = 15,
							Image = string.Empty,
							Title = string.Empty,
							Command = string.Empty,
							Plugin = new PluginItem(),
							DisplayName = string.Empty,
							ShortName = "metalpipe",
							Skin = 0,
							Amount = 20,
							Chance = 50
						},
						new ItemInfo
						{
							Type = ItemType.Item,
							ID = 16,
							Image = string.Empty,
							Title = string.Empty,
							Command = string.Empty,
							Plugin = new PluginItem(),
							DisplayName = string.Empty,
							ShortName = "gears",
							Skin = 0,
							Amount = 15,
							Chance = 50
						},
						new ItemInfo
						{
							Type = ItemType.Item,
							ID = 17,
							Image = string.Empty,
							Title = string.Empty,
							Command = string.Empty,
							Plugin = new PluginItem(),
							DisplayName = string.Empty,
							ShortName = "smgbody",
							Skin = 0,
							Amount = 5,
							Chance = 50
						},
						new ItemInfo
						{
							Type = ItemType.Item,
							ID = 18,
							Image = string.Empty,
							Title = string.Empty,
							Command = string.Empty,
							Plugin = new PluginItem(),
							DisplayName = string.Empty,
							ShortName = "smgbody",
							Skin = 0,
							Amount = 5,
							Chance = 20
						},
						new ItemInfo
						{
							Type = ItemType.Item,
							ID = 19,
							Image = string.Empty,
							Title = string.Empty,
							Command = string.Empty,
							Plugin = new PluginItem(),
							DisplayName = string.Empty,
							ShortName = "metalspring",
							Skin = 0,
							Amount = 20,
							Chance = 50
						},
						new ItemInfo
						{
							Type = ItemType.Item,
							ID = 20,
							Image = string.Empty,
							Title = string.Empty,
							Command = string.Empty,
							Plugin = new PluginItem(),
							DisplayName = string.Empty,
							ShortName = "semibody",
							Skin = 0,
							Amount = 3,
							Chance = 15
						},
						new ItemInfo
						{
							Type = ItemType.Item,
							ID = 21,
							Image = string.Empty,
							Title = string.Empty,
							Command = string.Empty,
							Plugin = new PluginItem(),
							DisplayName = string.Empty,
							ShortName = "techparts",
							Skin = 0,
							Amount = 10,
							Chance = 25
						},
						new ItemInfo
						{
							Type = ItemType.Item,
							ID = 22,
							Image = string.Empty,
							Title = string.Empty,
							Command = string.Empty,
							Plugin = new PluginItem(),
							DisplayName = string.Empty,
							ShortName = "riflebody",
							Skin = 0,
							Amount = 10,
							Chance = 5
						}
					}
				},
				new CaseInfo
				{
					ID = 2,
					Image = "https://i.imgur.com/rADqKVZ.png",
					Permission = string.Empty,
					Cost = 150,
					CustomCurrency = new CustomCurrency
					{
						Enabled = false,
						CostFormat = "{0} scrap",
						Type = EconomyType.Item,
						Plug = string.Empty,
						AddHook = string.Empty,
						RemoveHook = string.Empty,
						BalanceHook = string.Empty,
						ShortName = "scrap",
						DisplayName = string.Empty,
						Skin = 0
					},
					Items = new List<ItemInfo>
					{
						new ItemInfo
						{
							Type = ItemType.Item,
							ID = 43,
							Image = string.Empty,
							Title = string.Empty,
							Command = string.Empty,
							Plugin = new PluginItem(),
							DisplayName = string.Empty,
							ShortName = "scrap",
							Skin = 0,
							Amount = 500,
							Chance = 50
						},
						new ItemInfo
						{
							Type = ItemType.Item,
							ID = 44,
							Image = string.Empty,
							Title = string.Empty,
							Command = string.Empty,
							Plugin = new PluginItem(),
							DisplayName = string.Empty,
							ShortName = "gunpowder",
							Skin = 0,
							Amount = 3500,
							Chance = 50
						},
						new ItemInfo
						{
							Type = ItemType.Item,
							ID = 45,
							Image = string.Empty,
							Title = string.Empty,
							Command = string.Empty,
							Plugin = new PluginItem(),
							DisplayName = string.Empty,
							ShortName = "rifle.ak",
							Skin = 0,
							Amount = 1,
							Chance = 15
						},
						new ItemInfo
						{
							Type = ItemType.Item,
							ID = 46,
							Image = string.Empty,
							Title = string.Empty,
							Command = string.Empty,
							Plugin = new PluginItem(),
							DisplayName = string.Empty,
							ShortName = "rifle.semiauto",
							Skin = 0,
							Amount = 1,
							Chance = 20
						},
						new ItemInfo
						{
							Type = ItemType.Item,
							ID = 47,
							Image = string.Empty,
							Title = string.Empty,
							Command = string.Empty,
							Plugin = new PluginItem(),
							DisplayName = string.Empty,
							ShortName = "explosive.timed",
							Skin = 0,
							Amount = 5,
							Chance = 20
						},
						new ItemInfo
						{
							Type = ItemType.Item,
							ID = 48,
							Image = string.Empty,
							Title = string.Empty,
							Command = string.Empty,
							Plugin = new PluginItem(),
							DisplayName = string.Empty,
							ShortName = "sulfur",
							Skin = 0,
							Amount = 5000,
							Chance = 50
						},
						new ItemInfo
						{
							Type = ItemType.Item,
							ID = 49,
							Image = string.Empty,
							Title = string.Empty,
							Command = string.Empty,
							Plugin = new PluginItem(),
							DisplayName = string.Empty,
							ShortName = "metal.refined",
							Skin = 0,
							Amount = 300,
							Chance = 50
						},
						new ItemInfo
						{
							Type = ItemType.Item,
							ID = 50,
							Image = string.Empty,
							Title = string.Empty,
							Command = string.Empty,
							Plugin = new PluginItem(),
							DisplayName = string.Empty,
							ShortName = "stones",
							Skin = 0,
							Amount = 8000,
							Chance = 60
						},
						new ItemInfo
						{
							Type = ItemType.Item,
							ID = 51,
							Image = string.Empty,
							Title = string.Empty,
							Command = string.Empty,
							Plugin = new PluginItem(),
							DisplayName = string.Empty,
							ShortName = "wood",
							Skin = 0,
							Amount = 8000,
							Chance = 60
						},
						new ItemInfo
						{
							Type = ItemType.Item,
							ID = 52,
							Image = string.Empty,
							Title = string.Empty,
							Command = string.Empty,
							Plugin = new PluginItem(),
							DisplayName = string.Empty,
							ShortName = "lmg.m249",
							Skin = 0,
							Amount = 1,
							Chance = 5
						},
						new ItemInfo
						{
							Type = ItemType.Item,
							ID = 53,
							Image = string.Empty,
							Title = string.Empty,
							Command = string.Empty,
							Plugin = new PluginItem(),
							DisplayName = string.Empty,
							ShortName = "ammo.rocket.basic",
							Skin = 0,
							Amount = 4,
							Chance = 50
						}
					}
				},
				new CaseInfo
				{
					ID = 3,
					Image = "https://i.imgur.com/ojg7Sn5.png",
					Permission = string.Empty,
					Cost = 200,
					CustomCurrency = new CustomCurrency
					{
						Enabled = false,
						CostFormat = "{0} scrap",
						Type = EconomyType.Item,
						Plug = string.Empty,
						AddHook = string.Empty,
						RemoveHook = string.Empty,
						BalanceHook = string.Empty,
						ShortName = "scrap",
						DisplayName = string.Empty,
						Skin = 0
					},
					Items = new List<ItemInfo>
					{
						new ItemInfo
						{
							Type = ItemType.Item,
							ID = 23,
							Image = string.Empty,
							Title = string.Empty,
							Command = string.Empty,
							Plugin = new PluginItem(),
							DisplayName = string.Empty,
							ShortName = "pistol.semiauto",
							Skin = 0,
							Amount = 1,
							Chance = 50
						},
						new ItemInfo
						{
							Type = ItemType.Item,
							ID = 24,
							Image = string.Empty,
							Title = string.Empty,
							Command = string.Empty,
							Plugin = new PluginItem(),
							DisplayName = string.Empty,
							ShortName = "pistol.python",
							Skin = 0,
							Amount = 1,
							Chance = 50
						},
						new ItemInfo
						{
							Type = ItemType.Item,
							ID = 25,
							Image = string.Empty,
							Title = string.Empty,
							Command = string.Empty,
							Plugin = new PluginItem(),
							DisplayName = string.Empty,
							ShortName = "pistol.m92",
							Skin = 0,
							Amount = 1,
							Chance = 50
						},
						new ItemInfo
						{
							Type = ItemType.Item,
							ID = 26,
							Image = string.Empty,
							Title = string.Empty,
							Command = string.Empty,
							Plugin = new PluginItem(),
							DisplayName = string.Empty,
							ShortName = "smg.2",
							Skin = 0,
							Amount = 1,
							Chance = 40
						},
						new ItemInfo
						{
							Type = ItemType.Item,
							ID = 27,
							Image = string.Empty,
							Title = string.Empty,
							Command = string.Empty,
							Plugin = new PluginItem(),
							DisplayName = string.Empty,
							ShortName = "rifle.m39",
							Skin = 0,
							Amount = 1,
							Chance = 20
						},
						new ItemInfo
						{
							Type = ItemType.Item,
							ID = 28,
							Image = string.Empty,
							Title = string.Empty,
							Command = string.Empty,
							Plugin = new PluginItem(),
							DisplayName = string.Empty,
							ShortName = "smg.thompson",
							Skin = 0,
							Amount = 1,
							Chance = 40
						},
						new ItemInfo
						{
							Type = ItemType.Item,
							ID = 29,
							Image = string.Empty,
							Title = string.Empty,
							Command = string.Empty,
							Plugin = new PluginItem(),
							DisplayName = string.Empty,
							ShortName = "rifle.semiauto",
							Skin = 0,
							Amount = 1,
							Chance = 20
						},
						new ItemInfo
						{
							Type = ItemType.Item,
							ID = 30,
							Image = string.Empty,
							Title = string.Empty,
							Command = string.Empty,
							Plugin = new PluginItem(),
							DisplayName = string.Empty,
							ShortName = "rifle.lr300",
							Skin = 0,
							Amount = 1,
							Chance = 15
						},
						new ItemInfo
						{
							Type = ItemType.Item,
							ID = 31,
							Image = string.Empty,
							Title = string.Empty,
							Command = string.Empty,
							Plugin = new PluginItem(),
							DisplayName = string.Empty,
							ShortName = "rifle.bolt",
							Skin = 0,
							Amount = 1,
							Chance = 15
						},
						new ItemInfo
						{
							Type = ItemType.Item,
							ID = 54,
							Image = string.Empty,
							Title = string.Empty,
							Command = string.Empty,
							Plugin = new PluginItem(),
							DisplayName = string.Empty,
							ShortName = "rifle.ak",
							Skin = 0,
							Amount = 1,
							Chance = 15
						}
					}
				},
				new CaseInfo
				{
					ID = 4,
					Image = "https://i.imgur.com/1ZttHs8.png",
					Permission = string.Empty,
					Cost = 200,
					CustomCurrency = new CustomCurrency
					{
						Enabled = false,
						CostFormat = "{0} scrap",
						Type = EconomyType.Item,
						Plug = string.Empty,
						AddHook = string.Empty,
						RemoveHook = string.Empty,
						BalanceHook = string.Empty,
						ShortName = "scrap",
						DisplayName = string.Empty,
						Skin = 0
					},
					Items = new List<ItemInfo>
					{
						new ItemInfo
						{
							Type = ItemType.Item,
							ID = 32,
							Image = string.Empty,
							Title = string.Empty,
							Command = string.Empty,
							Plugin = new PluginItem(),
							DisplayName = string.Empty,
							ShortName = "explosive.timed",
							Skin = 0,
							Amount = 5,
							Chance = 50
						},
						new ItemInfo
						{
							Type = ItemType.Item,
							ID = 33,
							Image = string.Empty,
							Title = string.Empty,
							Command = string.Empty,
							Plugin = new PluginItem(),
							DisplayName = string.Empty,
							ShortName = "ammo.rocket.basic",
							Skin = 0,
							Amount = 8,
							Chance = 50
						},
						new ItemInfo
						{
							Type = ItemType.Item,
							ID = 34,
							Image = string.Empty,
							Title = string.Empty,
							Command = string.Empty,
							Plugin = new PluginItem(),
							DisplayName = string.Empty,
							ShortName = "lmg.m249",
							Skin = 0,
							Amount = 1,
							Chance = 50
						},
						new ItemInfo
						{
							Type = ItemType.Item,
							ID = 35,
							Image = string.Empty,
							Title = string.Empty,
							Command = string.Empty,
							Plugin = new PluginItem(),
							DisplayName = string.Empty,
							ShortName = "gunpowder",
							Skin = 0,
							Amount = 8000,
							Chance = 50
						},
						new ItemInfo
						{
							Type = ItemType.Item,
							ID = 36,
							Image = string.Empty,
							Title = string.Empty,
							Command = string.Empty,
							Plugin = new PluginItem(),
							DisplayName = string.Empty,
							ShortName = "rifle.l96",
							Skin = 0,
							Amount = 1,
							Chance = 50
						},
						new ItemInfo
						{
							Type = ItemType.Item,
							ID = 37,
							Image = string.Empty,
							Title = string.Empty,
							Command = string.Empty,
							Plugin = new PluginItem(),
							DisplayName = string.Empty,
							ShortName = "supply.signal",
							Skin = 0,
							Amount = 6,
							Chance = 50
						},
						new ItemInfo
						{
							Type = ItemType.Item,
							ID = 38,
							Image = string.Empty,
							Title = string.Empty,
							Command = string.Empty,
							Plugin = new PluginItem(),
							DisplayName = string.Empty,
							ShortName = "explosive.satchel",
							Skin = 0,
							Amount = 6,
							Chance = 50
						},
						new ItemInfo
						{
							Type = ItemType.Item,
							ID = 39,
							Image = string.Empty,
							Title = string.Empty,
							Command = string.Empty,
							Plugin = new PluginItem(),
							DisplayName = string.Empty,
							ShortName = "ammo.rifle.explosive",
							Skin = 0,
							Amount = 250,
							Chance = 50
						},
						new ItemInfo
						{
							Type = ItemType.Item,
							ID = 40,
							Image = string.Empty,
							Title = string.Empty,
							Command = string.Empty,
							Plugin = new PluginItem(),
							DisplayName = string.Empty,
							ShortName = "grenade.f1",
							Skin = 0,
							Amount = 5,
							Chance = 50
						},
						new ItemInfo
						{
							Type = ItemType.Item,
							ID = 41,
							Image = string.Empty,
							Title = string.Empty,
							Command = string.Empty,
							Plugin = new PluginItem(),
							DisplayName = string.Empty,
							ShortName = "workbench3",
							Skin = 0,
							Amount = 1,
							Chance = 60
						},
						new ItemInfo
						{
							Type = ItemType.Item,
							ID = 42,
							Image = string.Empty,
							Title = string.Empty,
							Command = string.Empty,
							Plugin = new PluginItem(),
							DisplayName = string.Empty,
							ShortName = "scrap",
							Skin = 0,
							Amount = 1000,
							Chance = 50
						}
					}
				},
				new CaseInfo
				{
					ID = 5,
					Image = "https://i.imgur.com/wIPGCGM.png",
					Permission = string.Empty,
					Cost = 500,
					CustomCurrency = new CustomCurrency
					{
						Enabled = false,
						CostFormat = "{0} scrap",
						Type = EconomyType.Item,
						Plug = string.Empty,
						AddHook = string.Empty,
						RemoveHook = string.Empty,
						BalanceHook = string.Empty,
						ShortName = "scrap",
						DisplayName = string.Empty,
						Skin = 0
					},
					Items = new List<ItemInfo>
					{
						new ItemInfo
						{
							Type = ItemType.Item,
							ID = 55,
							Image = string.Empty,
							Title = string.Empty,
							Command = string.Empty,
							Plugin = new PluginItem(),
							DisplayName = string.Empty,
							ShortName = "wood",
							Skin = 0,
							Amount = 20000,
							Chance = 70
						},
						new ItemInfo
						{
							Type = ItemType.Item,
							ID = 56,
							Image = string.Empty,
							Title = string.Empty,
							Command = string.Empty,
							Plugin = new PluginItem(),
							DisplayName = string.Empty,
							ShortName = "stones",
							Skin = 0,
							Amount = 15000,
							Chance = 70
						},
						new ItemInfo
						{
							Type = ItemType.Item,
							ID = 57,
							Image = string.Empty,
							Title = string.Empty,
							Command = string.Empty,
							Plugin = new PluginItem(),
							DisplayName = string.Empty,
							ShortName = "leather",
							Skin = 0,
							Amount = 2400,
							Chance = 55
						},
						new ItemInfo
						{
							Type = ItemType.Item,
							ID = 58,
							Image = string.Empty,
							Title = string.Empty,
							Command = string.Empty,
							Plugin = new PluginItem(),
							DisplayName = string.Empty,
							ShortName = "cloth",
							Skin = 0,
							Amount = 2300,
							Chance = 55
						},
						new ItemInfo
						{
							Type = ItemType.Item,
							ID = 59,
							Image = string.Empty,
							Title = string.Empty,
							Command = string.Empty,
							Plugin = new PluginItem(),
							DisplayName = string.Empty,
							ShortName = "lowgradefuel",
							Skin = 0,
							Amount = 1500,
							Chance = 50
						},
						new ItemInfo
						{
							Type = ItemType.Item,
							ID = 60,
							Image = string.Empty,
							Title = string.Empty,
							Command = string.Empty,
							Plugin = new PluginItem(),
							DisplayName = string.Empty,
							ShortName = "roadsigns",
							Skin = 0,
							Amount = 35,
							Chance = 50
						},
						new ItemInfo
						{
							Type = ItemType.Item,
							ID = 61,
							Image = string.Empty,
							Title = string.Empty,
							Command = string.Empty,
							Plugin = new PluginItem(),
							DisplayName = string.Empty,
							ShortName = "metalpipe",
							Skin = 0,
							Amount = 40,
							Chance = 50
						},
						new ItemInfo
						{
							Type = ItemType.Item,
							ID = 62,
							Image = string.Empty,
							Title = string.Empty,
							Command = string.Empty,
							Plugin = new PluginItem(),
							DisplayName = string.Empty,
							ShortName = "gears",
							Skin = 0,
							Amount = 30,
							Chance = 50
						}
					}
				}
			};

			[JsonProperty(PropertyName = "Wipe Settings")]
			public WipeSettings Wipe = new WipeSettings
			{
				Players = false,
				Cooldowns = true
			};

			[JsonProperty(PropertyName = "UI Settings")]
			public InterfaceSettings UI = new InterfaceSettings
			{
				Colors = new InterfaceSettings.ColorsSettings
				{
					Color1 = new IColor("#0E0E10", 100),
					Color2 = new IColor("#161617", 100),
					Color3 = new IColor("#FFFFFF", 100),
					Color4 = new IColor("#4B68FF", 100),
					Color5 = new IColor("#BFBFBF", 100),
					Color6 = new IColor("#4B68FF", 33),
					Color7 = new IColor("##324192", 100),
					Color8 = new IColor("#FFFFFF", 50),
					Color9 = new IColor("#161617", 99),
					Color10 = new IColor("#161617", 85),
					Color11 = new IColor("#FF4B4B", 100),
					Color12 = new IColor("#CD3838", 100),
					Color13 = new IColor("#50965F", 100)
				}
			};
		}

		private class InterfaceSettings
		{
			[JsonProperty(PropertyName = "Colors")]
			public ColorsSettings Colors;

			public class ColorsSettings
			{
				[JsonProperty(PropertyName = "Color 1")]
				public IColor Color1;

				[JsonProperty(PropertyName = "Color 2")]
				public IColor Color2;

				[JsonProperty(PropertyName = "Color 3")]
				public IColor Color3;

				[JsonProperty(PropertyName = "Color 4")]
				public IColor Color4;

				[JsonProperty(PropertyName = "Color 5")]
				public IColor Color5;

				[JsonProperty(PropertyName = "Color 6")]
				public IColor Color6;

				[JsonProperty(PropertyName = "Color 7")]
				public IColor Color7;

				[JsonProperty(PropertyName = "Color 8")]
				public IColor Color8;

				[JsonProperty(PropertyName = "Color 9")]
				public IColor Color9;

				[JsonProperty(PropertyName = "Color 10")]
				public IColor Color10;

				[JsonProperty(PropertyName = "Color 11")]
				public IColor Color11;

				[JsonProperty(PropertyName = "Color 12")]
				public IColor Color12;

				[JsonProperty(PropertyName = "Color 13")]
				public IColor Color13;
			}
		}

		private class WipeSettings
		{
			[JsonProperty(PropertyName = "Wipe Players?")]
			public bool Players;

			[JsonProperty(PropertyName = "Wipe Cooldowns?")]
			public bool Cooldowns;
		}

		private class TimeCasesSettings
		{
			[JsonProperty(PropertyName = "Enabled")]
			public bool Enable;

			[JsonProperty(PropertyName = "Cooldown (seconds)")]
			public float Cooldown;

			[JsonProperty(PropertyName = "Cases (IDs)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<int> Cases;
		}

		private class PlaytimeTrackerSettings
		{
			[JsonProperty(PropertyName = "Enabled")]
			public bool Enabled;

			[JsonProperty(PropertyName = "Playtime (seconds) - CaseID",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<int, int> Cases;
		}

		private class IColor
		{
			[JsonProperty(PropertyName = "HEX")] public string HEX;

			[JsonProperty(PropertyName = "Opacity (0 - 100)")]
			public readonly float Alpha;

			public string Get()
			{
				if (string.IsNullOrEmpty(HEX)) HEX = "#FFFFFF";

				var str = HEX.Trim('#');
				if (str.Length != 6) throw new Exception(HEX);
				var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
				var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
				var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

				return $"{(double) r / 255} {(double) g / 255} {(double) b / 255} {Alpha / 100}";
			}

			public IColor(string hex, float alpha)
			{
				HEX = hex;
				Alpha = alpha;
			}
		}

		private enum EconomyType
		{
			Plugin,
			Item
		}

		private abstract class EconomyTemplate
		{
			[JsonProperty(PropertyName = "Type (Plugin/Item)")] [JsonConverter(typeof(StringEnumConverter))]
			public EconomyType Type;

			[JsonProperty(PropertyName = "Plugin name")]
			public string Plug;

			[JsonProperty(PropertyName = "Balance add hook")]
			public string AddHook;

			[JsonProperty(PropertyName = "Balance remove hook")]
			public string RemoveHook;

			[JsonProperty(PropertyName = "Balance show hook")]
			public string BalanceHook;

			[JsonProperty(PropertyName = "ShortName")]
			public string ShortName;

			[JsonProperty(PropertyName = "Display Name (empty - default)")]
			public string DisplayName;

			[JsonProperty(PropertyName = "Skin")] public ulong Skin;

			public double ShowBalance(BasePlayer player)
			{
				switch (Type)
				{
					case EconomyType.Plugin:
					{
						var plugin = _instance?.plugins?.Find(Plug);
						if (plugin == null) return 0;

						return Math.Round(Convert.ToDouble((double) plugin.Call(BalanceHook, player.userID,"repute")));
					}
					case EconomyType.Item:
					{
						return ItemCount(player.inventory.AllItems(), ShortName, Skin);
					}
					default:
						return 0;
				}
			}

			public void AddBalance(BasePlayer player, double amount)
			{
				switch (Type)
				{
					case EconomyType.Plugin:
					{
						var plugin = _instance?.plugins?.Find(Plug);
						if (plugin == null) return;

						switch (Plug)
						{
							case "BankSystem":
							case "ServerRewards":
								plugin.Call(AddHook, player.userID, (int) amount);
								break;
							default:
								plugin.Call(AddHook, player.userID,"repute", amount);
								break;
						}

						break;
					}
					case EconomyType.Item:
					{
						var am = (int) amount;

						var item = ToItem(am);
						if (item == null) return;

						player.GiveItem(item);
						break;
					}
				}
			}

			public bool RemoveBalance(BasePlayer player, double amount)
			{
				switch (Type)
				{
					case EconomyType.Plugin:
					{
						if (ShowBalance(player) < amount) return false;

						var plugin = _instance?.plugins.Find(Plug);
						if (plugin == null) return false;

						switch (Plug)
						{
							case "BankSystem":
							case "ServerRewards":
								plugin.Call(RemoveHook, player.userID, (int) amount);
								break;
							default:
								plugin.Call(RemoveHook, player.userID,"repute",amount);
								break;
						}

						return true;
					}
					case EconomyType.Item:
					{
						var playerItems = player.inventory.AllItems();
						var am = (int) amount;

						if (ItemCount(playerItems, ShortName, Skin) < am) return false;

						Take(playerItems, ShortName, Skin, am);
						return true;
					}
					default:
						return false;
				}
			}

			private Item ToItem(int amount)
			{
				var item = ItemManager.CreateByName(ShortName, amount, Skin);
				if (item == null)
				{
					Debug.LogError($"Error creating item with ShortName: '{ShortName}'");
					return null;
				}

				if (!string.IsNullOrEmpty(DisplayName)) item.name = DisplayName;

				return item;
			}
		}

		private class EconomyConf : EconomyTemplate
		{
			[JsonProperty(PropertyName = "Show Balance")]
			public bool Show;
		}

		private class CustomCurrency : EconomyTemplate
		{
			[JsonProperty(PropertyName = "Enabled")]
			public bool Enabled;

			[JsonProperty(PropertyName = "Cost Format")]
			public string CostFormat;
		}

		private abstract class CaseTemplate
		{
			[JsonProperty(PropertyName = "ID")] public int ID;

			[JsonProperty(PropertyName = "Image")] public string Image;

			[JsonProperty(PropertyName = "Permission")]
			public string Permission;

			[JsonProperty(PropertyName = "Cooldown")]
			public float CooldownTime;

			[JsonProperty(PropertyName = "Items", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<ItemInfo> Items;

			public bool HasCase(BasePlayer player, int caseId)
			{
				return _instance.GetPlayerData(player).Cases.ContainsKey(caseId);
			}

			public int CaseAmount(BasePlayer player, int caseId)
			{
				return _instance.GetPlayerData(player).Cases[caseId];
			}
		}

		private class BonusCaseInfo : CaseInfo
		{
			[JsonProperty(PropertyName = "Enabled")]
			public bool Enabled;
		}

		private class CaseInfo : CaseTemplate, ICloneable, IDisposable
		{
			[JsonProperty(PropertyName = "Cost")] public int Cost;

			[JsonProperty(PropertyName = "Custom Currency")]
			public CustomCurrency CustomCurrency;

			public object Clone()
			{
				return MemberwiseClone();
			}

			public void Dispose()
			{
				//null
			}
		}

		private enum ItemType
		{
			Item,
			Command,
			Plugin
		}

		private class ItemInfo
		{
			[JsonProperty(PropertyName = "Type")] [JsonConverter(typeof(StringEnumConverter))]
			public ItemType Type;

			[JsonProperty(PropertyName = "ID")] public int ID;

			[JsonProperty(PropertyName = "Image")] public string Image;

			[JsonProperty(PropertyName = "Title")] public string Title;

			[JsonProperty(PropertyName = "Command (%steamid%)")]
			public string Command;

			[JsonProperty(PropertyName = "Plugin")]
			public PluginItem Plugin;

			[JsonProperty(PropertyName = "Display Name (empty - default)")]
			public string DisplayName;

			[JsonProperty(PropertyName = "ShortName")]
			public string ShortName;

			[JsonProperty(PropertyName = "Skin")] public ulong Skin;

			[JsonProperty(PropertyName = "Amount")]
			public int Amount;

			[JsonProperty(PropertyName = "Chance")]
			public float Chance;

			#region Definition

			[JsonIgnore] private ItemDefinition _definition;

			[JsonIgnore]
			public ItemDefinition ItemDefinition
			{
				get
				{
					if (_definition == null)
						_definition = ItemManager.FindItemDefinition(ShortName);

					return _definition;
				}
			}

			public void UpdateDefinition()
			{
				_definition = ItemManager.FindItemDefinition(ShortName);
			}

			#endregion

			public string GetName()
			{
				if (!string.IsNullOrEmpty(Title))
					return Title;

				if (!string.IsNullOrEmpty(DisplayName))
					return DisplayName;

				var def = ItemManager.FindItemDefinition(ShortName);
				if (!string.IsNullOrEmpty(ShortName) && def != null)
					return def.displayName.translated;

				return string.Empty;
			}

			public void Get(BasePlayer player, int count = 1)
			{
				switch (Type)
				{
					case ItemType.Item:
						ToItem(player, count);
						break;
					case ItemType.Command:
						ToCommand(player, count);
						break;
					case ItemType.Plugin:
						Plugin.Get(player, count);
						break;
				}
			}

			private void ToItem(BasePlayer player, int count)
			{
				if (ItemDefinition == null)
				{
					Debug.LogError($"Error creating item with ShortName '{ShortName}'");
					return;
				}

				GetStacks(ItemDefinition, Amount * count)?.ForEach(stack =>
				{
					var newItem = ItemManager.Create(ItemDefinition, stack, Skin);
					if (newItem == null)
					{
						_instance?.PrintError($"Error creating item with ShortName '{ShortName}'");
						return;
					}

					if (!string.IsNullOrEmpty(DisplayName)) newItem.name = DisplayName;

					player.GiveItem(newItem, BaseEntity.GiveItemReason.PickedUp);
				});
			}

			private void ToCommand(BasePlayer player, int count)
			{
				for (var i = 0; i < count; i++)
				{
					var command = Command.Replace("\n", "|")
						.Replace("%steamid%", player.UserIDString, StringComparison.OrdinalIgnoreCase).Replace(
							"%username%",
							player.displayName, StringComparison.OrdinalIgnoreCase);

					foreach (var check in command.Split('|')) _instance?.Server.Command(check);
				}
			}

			private static List<int> GetStacks(ItemDefinition item, int amount)
			{
				var list = Pool.GetList<int>();
				var maxStack = item.stackable;

				if (maxStack == 0) maxStack = 1;

				while (amount > maxStack)
				{
					amount -= maxStack;
					list.Add(maxStack);
				}

				list.Add(amount);

				return list;
			}
		}

		private class PluginItem
		{
			[JsonProperty(PropertyName = "Hook")] public string Hook;

			[JsonProperty(PropertyName = "Plugin name")]
			public string Plugin;

			[JsonProperty(PropertyName = "Amount")]
			public int Amount;

			public void Get(BasePlayer player, int count = 1)
			{
				var plug = _instance?.plugins.Find(Plugin);
				if (plug == null)
				{
					_instance?.PrintError($"Plugin '{Plugin}' not found !!! ");
					return;
				}

				switch (Plugin)
				{
					case "Economics":
					{
						plug.Call(Hook, player.userID, (double) Amount * count);
						break;
					}
					default:
					{
						plug.Call(Hook, player.userID, Amount * count);
						break;
					}
				}
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

		#region Data

		private PluginData _data;

		private void SaveData()
		{
			SavePlayers();

			SaveCooldowns();
		}

		private void SavePlayers()
		{
			Interface.Oxide.DataFileSystem.WriteObject(Name, _data);
		}

		private void SaveCooldowns()
		{
			Interface.Oxide.DataFileSystem.WriteObject($"{Name}_Cooldowns", Cooldowns);
		}

		private void LoadData()
		{
			try
			{
				_data = Interface.Oxide.DataFileSystem.ReadObject<PluginData>(Name);
				Cooldowns = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, Cooldown>>($"{Name}_Cooldowns");
			}
			catch (Exception e)
			{
				PrintError(e.ToString());
			}

			if (_data == null) _data = new PluginData();
			if (Cooldowns == null) Cooldowns = new Dictionary<ulong, Cooldown>();
		}

		private class PluginData
		{
			[JsonProperty(PropertyName = "Players", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public readonly Dictionary<ulong, PlayerData> Players = new Dictionary<ulong, PlayerData>();
		}

		private class PlayerData
		{
			[JsonProperty(PropertyName = "Last Bonus Time")]
			public DateTime LastBonus = new DateTime(1970, 1, 1);

			[JsonProperty(PropertyName = "Cases", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<int, int> Cases = new Dictionary<int, int>();

			[JsonProperty(PropertyName = "Last PlayTime")]
			public double LastPlayTime;
		}

		private PlayerData GetPlayerData(BasePlayer player)
		{
			return GetPlayerData(player.userID);
		}

		private PlayerData GetPlayerData(ulong member)
		{
			if (!_data.Players.ContainsKey(member))
				_data.Players.Add(member, new PlayerData());

			return _data.Players[member];
		}

		#endregion

		#region Hooks

		private void Init()
		{
			_instance = this;
			LoadData();

			if (!_config.TimeCasesSettings.Enable)
				Unsubscribe(nameof(OnPlayerConnected));
		}

		private void OnServerInitialized()
		{
			LoadItems();

			LoadImages();

			CheckOnDuplicates();

			FillItems();

			AddCovalenceCommand(_config.Commands, nameof(CmdCases));
			AddCovalenceCommand("givecase", nameof(CmdGiveCase));

			permission.RegisterPermission(PERM_EDIT, this);

			if (_config.TimeCasesSettings.Enable || _config.PlaytimeTrackerConf.Enabled)
			{
				if (_config.TimeCasesSettings.Enable)
					foreach (var player in BasePlayer.activePlayerList)
						OnPlayerConnected(player);

				timer.Every(1, TimeHandler);
			}

			if (_config.Cases.Exists(x => x.CooldownTime > 0)) timer.Every(1, CooldownController);
		}

		private void OnServerSave()
		{
			timer.In(Random.Range(2, 7), SavePlayers);
		}

		private void Unload()
		{
			foreach (var player in BasePlayer.activePlayerList)
			{
				CuiHelper.DestroyUi(player, Layer);
				CuiHelper.DestroyUi(player, Layer + ".Modal");

				OnPlayerDisconnected(player, string.Empty);
			}

			SaveData();

			_instance = null;
		}

		private void OnPlayerDisconnected(BasePlayer player, string reason)
		{
			if (player == null) return;

			_editByPlayer.Remove(player);

			player.GetComponent<OpenCase>()?.Finish(true);
		}

		private void OnPlayerConnected(BasePlayer player)
		{
			if (player == null) return;

			TimePlayers[player] = Time.time;
		}

		#region Wipe

		private void OnNewSave(string filename)
		{
			if (_config.Wipe.Players)
			{
				_data.Players.Clear();

				SavePlayers();
			}

			if (_config.Wipe.Cooldowns)
			{
				Cooldowns.Clear();

				SaveCooldowns();
			}
		}

		#endregion

		#endregion

		#region Commands

		private void CmdCases(IPlayer cov, string command, string[] args)
		{
			var player = cov?.Object as BasePlayer;
			if (player == null) return;

			if (!string.IsNullOrEmpty(_config.Permission) &&
			    !permission.UserHasPermission(player.UserIDString, _config.Permission))
			{
				SendNotify(player, NoPermissions, 1);
				return;
			}

			OpenCase openCase;
			if (player.TryGetComponent(out openCase) && openCase.Started) return;

			MainUI(player, first: true);
		}

		private void CmdGiveCase(IPlayer cov, string command, string[] args)
		{
			if (args.Length < 3)
			{
				cov.Reply($"Error syntax! Use: {command} <steamid> <caseid> <amount>");
				return;
			}

			var isAll = false;
			ulong target;
			if (!ulong.TryParse(args[0], out target))
			{
				if (args[0] == "*")
					isAll = true;
				else
					return;
			}

			int caseId, amount;
			if (!int.TryParse(args[1], out caseId) || !int.TryParse(args[2], out amount)) return;

			if (isAll)
			{
				foreach (var player in BasePlayer.activePlayerList) GiveCase(player, caseId, amount);

				cov.Reply($"{BasePlayer.activePlayerList.Count} players received case {caseId} ({amount} pcs.)");
			}
			else
			{
				GiveCase(target, caseId, amount);

				cov.Reply($"Player `{target}` received case {caseId} ({amount} pcs.)");
			}
		}

		[ConsoleCommand("UI_Cases")]
		private void CmdConsoleCases(ConsoleSystem.Arg arg)
		{
			var player = arg?.Player();
			if (player == null || !arg.HasArgs()) return;

			switch (arg.Args[0])
			{
				case "stopedit":
				{
					_editByPlayer.Remove(player);
					break;
				}

				case "edit":
				{
					int caseId;
					if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out caseId)) return;

					_editByPlayer[player] = new EditData(caseId);

					EditCaseUI(player);
					break;
				}

				case "saveedit":
				{
					var editData = _editByPlayer[player];

					if (editData.Generated)
					{
						_config.Cases.Add(editData.CaseInfo);
					}
					else
					{
						var oldCase = FindCaseById(editData.CaseInfo.ID);
						var newCase = editData.CaseInfo;

						var index = _config.Cases.IndexOf(oldCase);
						if (index != -1)
							_config.Cases[index] = newCase;

						oldCase.Dispose();
					}

					FillItems();

					SaveConfig();

					CaseUi(player, editData.CaseInfo.ID);
					break;
				}

				case "changefield":
				{
					if (!arg.HasArgs(3)) return;

					var fieldName = arg.Args[1];
					if (string.IsNullOrEmpty(fieldName)) return;

					var editData = _editByPlayer[player];

					var field = editData.Fields.Find(x => x.Name == fieldName);
					if (field == null)
						return;

					var newValue = arg.Args[2];

					object resultValue = null;
					switch (field.FieldType.Name)
					{
						case "String":
						{
							resultValue = newValue;
							break;
						}
						case "Int32":
						{
							int result;
							if (int.TryParse(newValue, out result))
								resultValue = result;
							break;
						}
						case "Single":
						{
							float result;
							if (float.TryParse(newValue, out result))
								resultValue = result;
							break;
						}
						case "Double":
						{
							double result;
							if (double.TryParse(newValue, out result))
								resultValue = result;
							break;
						}
						case "Boolean":
						{
							bool result;
							if (bool.TryParse(newValue, out result))
								resultValue = result;
							break;
						}
					}

					if (resultValue != null && field.GetValue(editData.CaseInfo)?.Equals(resultValue) != true)
					{
						field.SetValue(editData.CaseInfo, resultValue);

						EditCaseUI(player);
					}

					break;
				}

				case "start_editiem":
				{
					int itemId;
					if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out itemId)) return;

					var editData = _editByPlayer[player];

					ItemInfo itemInfo;
					if (itemId == -1)
					{
						itemInfo = new ItemInfo
						{
							Type = ItemType.Item,
							ID = _instance.GenerateItemID(),
							Image = string.Empty,
							Title = string.Empty,
							Command = string.Empty,
							Plugin = new PluginItem
							{
								Hook = string.Empty,
								Plugin = string.Empty,
								Amount = 0
							},
							DisplayName = string.Empty,
							ShortName = string.Empty,
							Skin = 0,
							Amount = 1,
							Chance = 100
						};

						editData.GeneratedItem = true;
					}
					else
					{
						itemInfo = editData.CaseInfo.Items.Find(x => x.ID == itemId);
					}

					editData.EditableItem = itemInfo;

					EditItemUi(player);
					break;
				}

				case "edititem_field":
				{
					if (!arg.HasArgs(3)) return;

					var editData = _editByPlayer[player];
					var item = editData.EditableItem;

					var fieldName = arg.Args[1];
					if (string.IsNullOrEmpty(fieldName)) return;

					var field = item.GetType().GetField(fieldName);
					if (field == null)
						return;

					var newValue = arg.Args[2];

					object resultValue = null;
					switch (field.FieldType.Name)
					{
						case "String":
						{
							resultValue = newValue;
							break;
						}
						case "Int32":
						{
							int result;
							if (int.TryParse(newValue, out result))
								resultValue = result;
							break;
						}
						case "Single":
						{
							float result;
							if (float.TryParse(newValue, out result))
								resultValue = result;
							break;
						}
						case "Double":
						{
							double result;
							if (double.TryParse(newValue, out result))
								resultValue = result;
							break;
						}
						case "Boolean":
						{
							bool result;
							if (bool.TryParse(newValue, out result))
								resultValue = result;
							break;
						}
					}

					if (resultValue != null && field.GetValue(item)?.Equals(resultValue) != true)
					{
						field.SetValue(item, resultValue);

						if (field.Name == "ShortName")
							item.UpdateDefinition();

						EditItemUi(player);
					}

					break;
				}

				case "edititem_close":
				{
					var editData = _editByPlayer[player];

					if (editData.GeneratedItem)
						editData.CaseInfo.Items.Add(editData.EditableItem);

					editData.ClearEditableItem();

					EditCaseUI(player);
					break;
				}

				case "start_selectitem":
				{
					_editByPlayer[player]?.ClearSelect();

					SelectItemUi(player);
					break;
				}

				case "selectitem":
				{
					if (!arg.HasArgs(3)) return;

					var editData = _editByPlayer[player];

					var param = arg.Args[1];
					switch (param)
					{
						case "page":
						{
							int page;
							if (!int.TryParse(arg.Args[2], out page)) return;

							editData.SelectPage = page;
							break;
						}
						case "search":
						{
							var search = string.Join(" ", arg.Args.Skip(2));
							if (string.IsNullOrEmpty(search) || editData.SelectInput.Equals(search)) return;

							editData.SelectInput = search;
							break;
						}

						case "category":
						{
							var category = string.Join(" ", arg.Args.Skip(2));
							if (string.IsNullOrEmpty(category) || editData.SelectedCategory.Equals(category)) return;

							editData.SelectedCategory = category;
							break;
						}
					}

					SelectItemUi(player);
					break;
				}

				case "selectitem_close":
				{
					_editByPlayer[player]?.ClearSelect();
					break;
				}

				case "takeitem":
				{
					if (!arg.HasArgs(2)) return;

					var shortName = arg.Args[1];
					if (string.IsNullOrEmpty(shortName)) return;

					var editData = _editByPlayer[player];

					editData.EditableItem.ShortName = shortName;

					editData.EditableItem.UpdateDefinition();

					editData.ClearSelect();

					EditItemUi(player);
					break;
				}

				case "changeclassfield_case":
				{
					if (!arg.HasArgs(4)) return;

					var editData = _editByPlayer[player];

					var classFieldName = arg.Args[1];
					if (string.IsNullOrEmpty(classFieldName)) return;

					var fieldName = arg.Args[2];
					if (string.IsNullOrEmpty(fieldName)) return;

					var classField = editData.CaseInfo.GetType().GetField(classFieldName);
					if (classField == null) return;

					var classValue = classField.GetValue(editData.CaseInfo);
					if (classValue == null) return;

					var field = classValue.GetType().GetField(fieldName);
					if (field == null) return;

					var newValue = arg.Args[3];

					object resultValue = null;
					switch (field.FieldType.Name)
					{
						case "String":
						{
							resultValue = newValue;
							break;
						}
						case "Int32":
						{
							int result;
							if (int.TryParse(newValue, out result))
								resultValue = result;
							break;
						}
						case "Single":
						{
							float result;
							if (float.TryParse(newValue, out result))
								resultValue = result;
							break;
						}
						case "Double":
						{
							double result;
							if (double.TryParse(newValue, out result))
								resultValue = result;
							break;
						}
						case "Boolean":
						{
							bool result;
							if (bool.TryParse(newValue, out result))
								resultValue = result;
							break;
						}
					}

					if (resultValue != null && field.GetValue(classValue)?.Equals(resultValue) != true)
					{
						field.SetValue(classValue, resultValue);

						EditCaseUI(player);
					}

					break;
				}

				case "changeclassfield_item":
				{
					if (!arg.HasArgs(4)) return;

					var editData = _editByPlayer[player];

					if (editData.EditableItem == null) return;

					var classFieldName = arg.Args[1];
					if (string.IsNullOrEmpty(classFieldName)) return;

					var fieldName = arg.Args[2];
					if (string.IsNullOrEmpty(fieldName)) return;

					var classField = editData.EditableItem.GetType().GetField(classFieldName);
					if (classField == null) return;

					var classValue = classField.GetValue(editData.EditableItem);
					if (classValue == null) return;

					var field = classValue.GetType().GetField(fieldName);
					if (field == null) return;

					var newValue = arg.Args[3];

					object resultValue = null;
					switch (field.FieldType.Name)
					{
						case "String":
						{
							resultValue = newValue;
							break;
						}
						case "Int32":
						{
							int result;
							if (int.TryParse(newValue, out result))
								resultValue = result;
							break;
						}
						case "Single":
						{
							float result;
							if (float.TryParse(newValue, out result))
								resultValue = result;
							break;
						}
						case "Double":
						{
							double result;
							if (double.TryParse(newValue, out result))
								resultValue = result;
							break;
						}
						case "Boolean":
						{
							bool result;
							if (bool.TryParse(newValue, out result))
								resultValue = result;
							break;
						}
					}

					if (resultValue != null && field.GetValue(classValue)?.Equals(resultValue) != true)
					{
						field.SetValue(classValue, resultValue);

						EditItemUi(player);
					}

					break;
				}

				case "listchangepage":
				{
					int newPage;
					if (!arg.HasArgs(3) || !int.TryParse(arg.Args[2], out newPage)) return;

					var fieldName = arg.Args[1];
					if (string.IsNullOrEmpty(fieldName)) return;

					var editData = _editByPlayer[player];

					editData.ListPages[fieldName] = newPage;

					EditCaseUI(player);
					break;
				}

				case "close":
				{
					OpenCase openCase;
					if (player.TryGetComponent(out openCase) && openCase.Started) return;

					CuiHelper.DestroyUi(player, Layer);
					CasesToUpdate.Remove(player);
					break;
				}

				case "cpage":
				{
					int page;
					if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out page)) return;

					MainUI(player, page);
					break;
				}
				case "showcase":
				{
					int caseId;
					if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out caseId)) return;

					OpenCase openCase;
					if (player.TryGetComponent(out openCase) && openCase.Started) return;

					CasesToUpdate.Remove(player);

					var page = 0;
					if (arg.HasArgs(3))
						int.TryParse(arg.Args[2], out page);

					CaseUi(player, caseId, page);
					break;
				}
				case "tryopencase":
				{
					int caseId;
					if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out caseId)) return;

					CaseAcceptUi(player, caseId);
					break;
				}
				case "opencase":
				{
					int caseId;
					if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out caseId)) return;

					PlayerItemsData itemsData;
					if (!_playersItems.TryGetValue(player, out itemsData)) return;

					var data = GetPlayerData(player);
					if (data == null) return;

					var caseInfo = FindCaseById(caseId);
					if (caseInfo == null) return;

					var cooldownTime = GetCooldownTime(player.userID, caseInfo);
					if (cooldownTime > 0) return;

					if (data.Cases.ContainsKey(caseId))
					{
						if (data.Cases[caseId] < 1) return;

						data.Cases[caseId]--;

						if (data.Cases[caseId] <= 0)
							data.Cases.Remove(caseId);
					}
					else
					{
						if (caseId != -1 &&
						    (caseInfo.CustomCurrency.Enabled
							    ? !caseInfo.CustomCurrency.RemoveBalance(player, caseInfo.Cost)
							    : !_config.Economy.RemoveBalance(player, caseInfo.Cost)))
						{
							ErrorUi(player, Msg(player, NotEnoughMoney));
							return;
						}
					}

					SetCooldown(player, caseInfo);

					if (_config.Scrolling)
					{
						player.gameObject.AddComponent<OpenCase>().StartOpen(caseId, itemsData.Items);
					}
					else
					{
						var award = itemsData.Items.GetRandom();
						if (award == null) return;

						SendEffect(player,
							"assets/prefabs/deployable/vendingmachine/effects/vending-machine-purchase-human.prefab");

						GiveItem(player, award.ID);

						AwardUi(player, caseId, award);
					}

					break;
				}
			}
		}

		#endregion

		#region Interface

		private const int CasesOnPage = 3;
		private const float CaseItemSize = 185f;
		private const int CasesOnString = 3;

		private const float Margin = (575f - CasesOnString * CaseItemSize) / (CasesOnString - 1);

		private void MainUI(BasePlayer player, int page = 0, bool first = false)
		{
			#region Fields

			var constYSwitch = _config.BonusCaseInfo.Enabled ? -210f : -70f;

			var cases = _config.Cases
				.FindAll(x =>
					string.IsNullOrEmpty(x.Permission) ||
					permission.UserHasPermission(player.UserIDString, x.Permission));

			var array = cases
				.Skip(CasesOnPage * page)
				.Take(CasesOnPage)
				.ToList();

			var casesToCooldown = array.FindAll(x => GetCooldownTime(player.userID, x) > 0);
			if (casesToCooldown.Count > 0)
				CasesToUpdate[player] = casesToCooldown;

			#endregion

			var container = new CuiElementContainer();

			#region Background

			if (first)
			{
				CuiHelper.DestroyUi(player, Layer);

				container.Add(new CuiPanel
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Image =
					{
						Color = "0 0 0 0.0",
						Material = ""
					},
					CursorEnabled = true
				}, "ContentUI", Layer);

				/*container.Add(new CuiButton
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Text = {Text = ""},
					Button =
					{
						Color = "0 0 0 0",
						Command = "UI_Cases close"
					}
				}, Layer);*/
			}

			#endregion

			#region Main

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
					OffsetMin = $"-300 {(_config.BonusCaseInfo.Enabled ? -260 : -260)}",
					OffsetMax = $"300 {(_config.BonusCaseInfo.Enabled ? 265 : 265)}"
				},
				Image =
				{
					Color = "0 0 0 0"
				}
			}, Layer, Layer + ".Main");

			#region Header

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = "0 -50",
					OffsetMax = "0 0"
				},
				Image = {Color = "0.88 0.83 0.63 0.8"}
			}, Layer + ".Main", Layer + ".Header");

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 1",
					OffsetMin = "30 0",
					OffsetMax = "0 0"
				},
				Text =
				{
					Text = Msg(player, TitleMenu),
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-bold.ttf",
					FontSize = 16,
					Color = "0 0 0 1"
				}
			}, Layer + ".Header");

			var xSwitch = -25f;
			float width = 25;



			#region Balance

			if (_config.Economy.Show)
			{
				width = 90;
				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "1 1", AnchorMax = "1 1",
						OffsetMin = $"{xSwitch - width} -37.5",
						OffsetMax = $"{xSwitch} -12.5"
					},
					Image =
					{
						Color = "0.27 0.27 0.27 0.9"
					}
				}, Layer + ".Header", Layer + ".Header.Balance");

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1"
					},
					Text =
					{
						Text = Msg(player, BalanceTitle, _config.Economy.ShowBalance(player)),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 10,
						Color = _config.UI.Colors.Color3.Get()
					}
				}, Layer + ".Header.Balance");

				xSwitch = xSwitch - width - 5;
			}

			#endregion

			#region Add Case

			if (CanEditCase(player))
			{
				width = 90;
				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "1 1", AnchorMax = "1 1",
						OffsetMin = $"{xSwitch - width} -37.5",
						OffsetMax = $"{xSwitch} -12.5"
					},
					Text =
					{
						Text = Msg(player, AddCaseTitle),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 10,
						Color = _config.UI.Colors.Color3.Get()
					},
					Button =
					{
						Command = "UI_Cases edit -2",
						Color = _config.UI.Colors.Color4.Get()
					}
				}, Layer + ".Header", Layer + ".Header.AddCase");

				xSwitch = xSwitch - width - 5;
			}

			#endregion

			#endregion

			#region Bonus Case

			if (_config.BonusCaseInfo.Enabled)
			{
				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = "12.5 -190",
						OffsetMax = "-12.5 -70"
					},
					Image = {Color = "0.88 0.83 0.63 0.3"}
				}, Layer + ".Main", Layer + ".Bonus.Case");

				container.Add(new CuiElement
				{
					Parent = Layer + ".Bonus.Case",
					Components =
					{
						new CuiRawImageComponent
							{Png = ImageLibrary.Call<string>("GetImage", _config.BonusCaseInfo.Image)},
						new CuiRectTransformComponent
						{
							AnchorMin = "0 0.5", AnchorMax = "0 0.5",
							OffsetMin = "5 -55", OffsetMax = "115 55"
						}
					}
				});

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0.5", AnchorMax = "0 0.5",
						OffsetMin = "125 0", OffsetMax = "250 55"
					},
					Text =
					{
						Text = Msg(player, TitleBonusCase),
						Align = TextAnchor.MiddleLeft,
						Font = "robotocondensed-bold.ttf",
						FontSize = 17,
						Color = _config.UI.Colors.Color3.Get()
					}
				}, Layer + ".Bonus.Case");

				var cd = GetCooldownTime(player.userID, _config.BonusCaseInfo);
				if (cd > 0)
				{
					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0.5", AnchorMax = "0 0.5",
							OffsetMin = "125 -15",
							OffsetMax = "250 0"
						},
						Text =
						{
							Text = Msg(player, DelayAvailable),
							Align = TextAnchor.MiddleLeft,
							Font = "robotocondensed-regular.ttf",
							FontSize = 10,
							Color = "0.88 0.83 0.63 0.8"
						}
					}, Layer + ".Bonus.Case");

					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0.5", AnchorMax = "0 0.5",
							OffsetMin = "125 -45",
							OffsetMax = "350 -15"
						},
						Text =
						{
							Text = $"{FormatShortTime(player, TimeSpan.FromSeconds(cd))}",
							Align = TextAnchor.MiddleLeft,
							Font = "robotocondensed-bold.ttf",
							FontSize = 18,
							Color = _config.UI.Colors.Color3.Get()
						}
					}, Layer + ".Bonus.Case");
				}
				else
				{
					container.Add(new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0.5", AnchorMax = "0 0.5",
							OffsetMin = "125 -35",
							OffsetMax = "250 0"
						},
						Text =
						{
							Text = Msg(player, Availabe),
							Align = TextAnchor.MiddleLeft,
							Font = "robotocondensed-bold.ttf",
							FontSize = 18,
							Color = _config.UI.Colors.Color3.Get()
						}
					}, Layer + ".Bonus.Case");
				}

				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "1 0.5", AnchorMax = "1 0.5",
						OffsetMin = "-160 -15",
						OffsetMax = "-25 17.5"
					},
					Text =
					{
						Text = Msg(player, Open),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 14,
						Color = "0 0 0 1"
					},
					Button =
					{
						Color = "0.88 0.83 0.63 0.9",
						Command = cd <= 0 ? "UI_Cases showcase -1" : ""
					}
				}, Layer + ".Bonus.Case");
			}

			#endregion

			#region Cases

			xSwitch = -(CasesOnString * CaseItemSize + (CasesOnString - 1) * Margin) / 2f;
			var ySwitch = constYSwitch;

			for (var i = 0; i < array.Count; i++)
			{
				var caseData = array[i];

				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0.5 1", AnchorMax = "0.5 1",
						OffsetMin = $"{xSwitch} {ySwitch - CaseItemSize}",
						OffsetMax = $"{xSwitch + CaseItemSize} {ySwitch}"
					},
					Image =
					{
						Color = "0.88 0.83 0.63 0.2"
					}
				}, Layer + ".Main", Layer + $".Cases.Background.{caseData.ID}");

				container.Add(new CuiElement
				{
					Parent = Layer + $".Cases.Background.{caseData.ID}",
					Components =
					{
						new CuiRawImageComponent {Png = ImageLibrary.Call<string>("GetImage", caseData.Image)},
						new CuiRectTransformComponent
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1",
							OffsetMin = "-70 -150", OffsetMax = "70 -10"
						}
					}
				});

				RefCaseUi(player, ref container, caseData);

				if ((i + 1) % CasesOnString == 0)
				{
					ySwitch = ySwitch - CaseItemSize - 10f;
					xSwitch = -(CasesOnString * CaseItemSize + (CasesOnString - 1) * Margin) / 2f;
				}
				else
				{
					xSwitch += CaseItemSize + Margin;
				}
			}

			#endregion

			#region Pages

			var pages = Math.Ceiling((double) cases.Count / CasesOnPage);
			if (pages > 1)
			{
				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0.5 0", AnchorMax = "0.5 0",
						OffsetMin = "-175 10",
						OffsetMax = "-30 40"
					},
					Text =
					{
						Text = Msg(player, BackButton),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 12,
						Color = _config.UI.Colors.Color3.Get()
					},
					Button =
					{
						Color = "0.88 0.83 0.63 0.3",
						Command = page != 0 ? $"UI_Cases cpage {page - 1}" : ""
					}
				}, Layer + ".Main");

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0.5 0", AnchorMax = "0.5 0",
						OffsetMin = "-30 10",
						OffsetMax = "30 40"
					},
					Text =
					{
						Text = Msg(player, PagesFormat, page + 1, pages),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 12,
						Color = _config.UI.Colors.Color3.Get()
					}
				}, Layer + ".Main");

				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0.5 0", AnchorMax = "0.5 0",
						OffsetMin = "30 10",
						OffsetMax = "175 40"
					},
					Text =
					{
						Text = Msg(player, NextButton),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 12,
						Color = "0 0 0 1"
					},
					Button =
					{
						Color = "0.88 0.83 0.63 0.9",
						Command = cases.Count > (page + 1) * CasesOnPage ? $"UI_Cases cpage {page + 1}" : ""
					}
				}, Layer + ".Main");
			}

			#endregion

			#endregion

			CuiHelper.DestroyUi(player, Layer + ".Main");
			CuiHelper.AddUi(player, container);
		}

		private void RefCaseUi(BasePlayer player, ref CuiElementContainer container, CaseInfo caseInfoData)
		{
			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 1"
				},
				Image =
				{
					Color = "0 0 0 0"
				}
			}, Layer + $".Cases.Background.{caseInfoData.ID}", Layer + $".Cases.{caseInfoData.ID}");

			var cd = GetCooldownTime(player.userID, caseInfoData);
			if (cd > 0)
			{
				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0.5 0", AnchorMax = "0.5 0",
						OffsetMin = "-70 10",
						OffsetMax = "70 27.5"
					},
					Image =
					{
						Color = _config.UI.Colors.Color1.Get()
					}
				}, Layer + $".Cases.{caseInfoData.ID}", Layer + $".Cases.{caseInfoData.ID}.Cooldown");

				container.Add(new CuiLabel
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Text =
					{
						Text = $"{FormatShortTime(player, TimeSpan.FromSeconds(cd))}",
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 10,
						Color = _config.UI.Colors.Color3.Get()
					}
				}, Layer + $".Cases.{caseInfoData.ID}.Cooldown");
			}
			else
			{
				if (caseInfoData.HasCase(player, caseInfoData.ID))
				{
					container.Add(new CuiButton
					{
						RectTransform =
						{
							AnchorMin = "0.5 0", AnchorMax = "0.5 0",
							OffsetMin = "-70 10",
							OffsetMax = "-12.5 27.5"
						},
						Text =
						{
							Text = $"{caseInfoData.CaseAmount(player, caseInfoData.ID)}",
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-bold.ttf",
							FontSize = 10,
							Color = _config.UI.Colors.Color3.Get()
						},
						Button =
						{
							Color = _config.UI.Colors.Color7.Get(),
							Command = $"UI_Cases showcase {caseInfoData.ID}"
						}
					}, Layer + $".Cases.{caseInfoData.ID}");

					container.Add(new CuiButton
					{
						RectTransform =
						{
							AnchorMin = "0.5 0", AnchorMax = "0.5 0",
							OffsetMin = "-7.5 10",
							OffsetMax = "70 27.5"
						},
						Text =
						{
							Text = Msg(player, Open),
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-regular.ttf",
							FontSize = 10,
							Color = _config.UI.Colors.Color3.Get()
						},
						Button =
						{
							Color = _config.UI.Colors.Color4.Get(),
							Command = $"UI_Cases showcase {caseInfoData.ID}"
						}
					}, Layer + $".Cases.{caseInfoData.ID}");
				}
				else
				{
					if (caseInfoData.Cost > 0)
					{
						var custom = caseInfoData.CustomCurrency.Enabled;

						container.Add(new CuiLabel
						{
							RectTransform =
							{
								AnchorMin = "0.5 0", AnchorMax = "0.5 0",
								OffsetMin = "-70 10",
								OffsetMax = "20 27.5"
							},
							Text =
							{
								Text = Msg(player, CaseCost),
								Align = TextAnchor.MiddleLeft,
								Font = "robotocondensed-regular.ttf",
								FontSize = 9,
								Color = _config.UI.Colors.Color5.Get()
							}
						}, Layer + $".Cases.{caseInfoData.ID}");

						container.Add(new CuiButton
						{
							RectTransform =
							{
								AnchorMin = "0.5 0", AnchorMax = "0.5 0",
								OffsetMin = "12.5 10",
								OffsetMax = "70 27.5"
							},
							Text =
							{
								Text =
									custom
										? string.Format(caseInfoData.CustomCurrency.CostFormat, caseInfoData.Cost)
										: Msg(player, CaseCurrency, caseInfoData.Cost),
								Align = TextAnchor.MiddleCenter,
								Font = "robotocondensed-bold.ttf",
								FontSize = 10,
								Color = _config.UI.Colors.Color3.Get()
							},
							Button =
							{
								Color = _config.UI.Colors.Color1.Get(),
								Command = $"UI_Cases showcase {caseInfoData.ID}"
							}
						}, Layer + $".Cases.{caseInfoData.ID}");
					}
				}
			}

			CuiHelper.DestroyUi(player, Layer + $".Cases.{caseInfoData.ID}");
		}

		private void CaseUi(BasePlayer player, int caseId, int page = 0)
		{
			var caseInfo = FindCaseById(caseId);
			if (caseInfo == null) return;

			if (page == 0)
				if (!_playersItems.ContainsKey(player) || _playersItems[player].CaseId != caseId)
					_playersItems[player] = new PlayerItemsData(caseId, GetItems(caseInfo));

			var ItemSize = 134f;
			var ItemsOnString = 4;
			var Lines = 2;
			var ItemsOnPage = ItemsOnString * Lines;

			var container = new CuiElementContainer();

			#region Main

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
					OffsetMin = "-300 -300",
					OffsetMax = "300 307.5"
				},
				Image =
				{
					Color = "0 0 0 1"
				}
			}, Layer, Layer + ".Main");

			#region Header

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = "0 -50",
					OffsetMax = "0 0"
				},
				Image = {Color = "0.36 0.34 0.26 1"}
			}, Layer + ".Main", Layer + ".Header");


			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 1",
					OffsetMin = "30 0",
					OffsetMax = "0 0"
				},
				Text =
				{
					Text = Msg(player, TitleMenu),
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-bold.ttf",
					FontSize = 14,
					Color = _config.UI.Colors.Color3.Get()
				}
			}, Layer + ".Header");

			var xSwitch = -25f;
			var width = 110f;

			#region MyRegion

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "1 1", AnchorMax = "1 1",
					OffsetMin = $"{xSwitch - width} -37.5",
					OffsetMax = $"{xSwitch} -12.5"
				},
				Text =
				{
					Text = Msg(player, GoBack),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 11,
					Color = "0 0 0 1"
				},
				Button =
				{
					Color = "0.88 0.83 0.63 0.9",
					Command = $"{_config.Commands.GetRandom()}"
				}
			}, Layer + ".Header");

			xSwitch = xSwitch - width - 5;

			#endregion

			#region Balance

			if (_config.Economy.Show)
			{
				width = 90;
				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "1 1", AnchorMax = "1 1",
						OffsetMin = $"{xSwitch - width} -37.5",
						OffsetMax = $"{xSwitch} -12.5"
					},
					Image =
					{
						Color = "0.88 0.83 0.63 0.4"
					}
				}, Layer + ".Header", Layer + ".Header.Balance");

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1"
					},
					Text =
					{
						Text = Msg(player, BalanceTitle, _config.Economy.ShowBalance(player)),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 10,
						Color = "0.88 0.83 0.63 1"
					}
				}, Layer + ".Header.Balance");

				xSwitch = xSwitch - width - 5;
			}

			#endregion

			#region Edit Case

			if (CanEditCase(player))
			{
				width = 90;
				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "1 1", AnchorMax = "1 1",
						OffsetMin = $"{xSwitch - width} -37.5",
						OffsetMax = $"{xSwitch} -12.5"
					},
					Text =
					{
						Text = Msg(player, EditCaseTitle),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 10,
						Color = _config.UI.Colors.Color3.Get()
					},
					Button =
					{
						Command = $"UI_Cases edit {caseInfo.ID}",
						Color = _config.UI.Colors.Color4.Get()
					}
				}, Layer + ".Header", Layer + ".Header.AddCase");

				xSwitch = xSwitch - width - 5;
			}

			#endregion

			#endregion

			#region Roulette

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = "12.5 -215",
					OffsetMax = "-12.5 -80"
				},
				Image = {Color = "0.88 0.83 0.63 0.3"}
			}, Layer + ".Main", Layer + ".Roulette");

			RouletteUi(ref container, player, _playersItems[player].Items);

			RouletteButton(ref container, player, caseInfo.ID, false);

			#endregion

			#region Items

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "0 1",
					OffsetMin = "15 -270",
					OffsetMax = "150 -235"
				},
				Text =
				{
					Text = Msg(player, ItemList),
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-regular.ttf",
					FontSize = 12,
					Color = _config.UI.Colors.Color3.Get()
				}
			}, Layer + ".Main");

			var Margin = (575f - ItemsOnString * ItemSize) / (ItemsOnString - 1);

			xSwitch = -(ItemsOnString * ItemSize + (ItemsOnString - 1) * Margin) / 2f;
			var ySwitch = -275f;

			var i = 1;
			foreach (var item in caseInfo.Items.Skip(page * ItemsOnPage).Take(ItemsOnPage))
			{
				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0.5 1", AnchorMax = "0.5 1",
						OffsetMin = $"{xSwitch} {ySwitch - ItemSize}",
						OffsetMax = $"{xSwitch + ItemSize} {ySwitch}"
					},
					Image = {Color = "0.88 0.83 0.63 0.2"}
				}, Layer + ".Main", Layer + $".Item.{i}");

				if (string.IsNullOrEmpty(item.Image))
					container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
							OffsetMin = "-45 -45", OffsetMax = "45 45"
						},
						Image =
						{
							ItemId = item.ItemDefinition != null ? item.ItemDefinition.itemid : 0,
							SkinId = item.Skin
						}
					}, Layer + $".Item.{i}");
				else
					container.Add(new CuiElement
					{
						Parent = Layer + $".Item.{i}",
						Components =
						{
							new CuiRawImageComponent
							{
								Png = ImageLibrary.Call<string>("GetImage", item.Image)
							},
							new CuiRectTransformComponent
							{
								AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
								OffsetMin = "-45 -45", OffsetMax = "45 45"
							}
						}
					});

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = "0 -20",
						OffsetMax = "0 -5"
					},
					Text =
					{
						Text = $"{item.GetName()}",
						Align = TextAnchor.LowerCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 10,
						Color = _config.UI.Colors.Color8.Get()
					}
				}, Layer + $".Item.{i}");

				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "1 0", AnchorMax = "1 0",
						OffsetMin = "-25 10",
						OffsetMax = "7.5 25"
					},
					Image = {Color = "0.88 0.83 0.63 0.8"}
				}, Layer + $".Item.{i}", Layer + $".Item.{i}.Amount");

				container.Add(new CuiLabel
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Text =
					{
						Text = $"{item.Amount}",
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 10,
						Color = "0 0 0 1"
					}
				}, Layer + $".Item.{i}.Amount");

				if (_config.Rarity.ContainsKey(item.Chance))
					container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0 1", AnchorMax = "1 1",
							OffsetMin = "0 -5", OffsetMax = "0 0"
						},
						Image =
						{
							Color = _config.Rarity[item.Chance].Get()
						}
					}, Layer + $".Item.{i}");

				if (i % ItemsOnString == 0)
				{
					ySwitch = ySwitch - ItemSize - Margin;
					xSwitch = -(ItemsOnString * ItemSize + (ItemsOnString - 1) * Margin) / 2f;
				}
				else
				{
					xSwitch += ItemSize + Margin;
				}

				i++;
			}

			#endregion

			#region Pages

			var pages = Math.Ceiling((double) caseInfo.Items.Count / ItemsOnPage);
			if (pages > 1)
			{
				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0.5 0", AnchorMax = "0.5 0",
						OffsetMin = "-175 10",
						OffsetMax = "-30 40"
					},
					Text =
					{
						Text = Msg(player, BackButton),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 12,
						Color = _config.UI.Colors.Color3.Get()
					},
					Button =
					{
						Color = "0.88 0.83 0.63 0.4",
						Command = page != 0 ? $"UI_Cases showcase {caseId} {page - 1}" : ""
					}
				}, Layer + ".Main");

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0.5 0", AnchorMax = "0.5 0",
						OffsetMin = "-30 10",
						OffsetMax = "30 40"
					},
					Text =
					{
						Text = Msg(player, PagesFormat, page + 1, pages),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 12,
						Color = _config.UI.Colors.Color3.Get()
					}
				}, Layer + ".Main");

				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0.5 0", AnchorMax = "0.5 0",
						OffsetMin = "30 10",
						OffsetMax = "175 40"
					},
					Text =
					{
						Text = Msg(player, NextButton),
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 12,
						Color = "0 0 0 1"
					},
					Button =
					{
						Color = "0.88 0.83 0.63 0.9",
						Command = caseInfo.Items.Count > (page + 1) * ItemsOnPage
							? $"UI_Cases showcase {caseId} {page + 1}"
							: ""
					}
				}, Layer + ".Main");
			}

			#endregion

			#endregion

			CuiHelper.DestroyUi(player, Layer + ".Main");
			CuiHelper.AddUi(player, container);
		}

		private void RouletteUi(ref CuiElementContainer container, BasePlayer player, List<ItemInfo> items)
		{
			#region Items

			var ItemSize = 100f;
			var amountOnString = 5;
			var margin = 10f;

			var xSwitch = -(amountOnString * ItemSize + (amountOnString - 1) * margin) / 2f;

			var i = 0;
			foreach (var item in items.Take(5))
			{
				CuiHelper.DestroyUi(player, Layer + $".Roulette.Case.{i}");

				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0.5 1", AnchorMax = "0.5 1",
						OffsetMin = $"{xSwitch} {-10 - ItemSize}",
						OffsetMax = $"{xSwitch + ItemSize} -10"
					},
					Image = {Color = "0.88 0.83 0.63 0.3"}
				}, Layer + ".Roulette", Layer + $".Roulette.Case.{i}");

				if (string.IsNullOrEmpty(item.Image))
					container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
							OffsetMin = "-35 -35", OffsetMax = "35 35"
						},
						Image =
						{
							ItemId = item.ItemDefinition != null ? item.ItemDefinition.itemid : 0,
							SkinId = item.Skin
						}
					}, Layer + $".Roulette.Case.{i}");
				else
					container.Add(new CuiElement
					{
						Parent = Layer + $".Roulette.Case.{i}",
						Components =
						{
							new CuiRawImageComponent
							{
								Png = ImageLibrary.Call<string>("GetImage", item.Image)
							},
							new CuiRectTransformComponent
							{
								AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
								OffsetMin = "-35 -35", OffsetMax = "35 35"
							}
						}
					});

				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "1 0", AnchorMax = "1 0",
						OffsetMin = "-20 5",
						OffsetMax = "5 17"
					},
					Image = {Color = "0.88 0.83 0.63 0.9"}
				}, Layer + $".Roulette.Case.{i}", Layer + $".Roulette.Case.{i}.Amount");

				container.Add(new CuiLabel
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Text =
					{
						Text = $"{item.Amount}",
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 8,
						Color = "0 0 0 1"
					}
				}, Layer + $".Roulette.Case.{i}.Amount");

				if (_config.Rarity.ContainsKey(item.Chance))
					container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0 1", AnchorMax = "1 1",
							OffsetMin = "0 -2.5", OffsetMax = "0 0"
						},
						Image =
						{
							Color = _config.Rarity[item.Chance].Get()
						}
					}, Layer + $".Roulette.Case.{i}");

				xSwitch += ItemSize + margin;
				i++;
			}

			#endregion

			#region Arrow

			CuiHelper.DestroyUi(player, Layer + ".Roulette.Arrow");

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 1", AnchorMax = "0.5 1",
					OffsetMin = "-2 -25",
					OffsetMax = "2 0"
				},
				Image =
				{
					Color = "0.88 0.83 0.63 1"
				}
			}, Layer + ".Roulette", Layer + ".Roulette.Arrow");

			#endregion
		}

		private void RouletteButton(ref CuiElementContainer container, BasePlayer player, int caseId, bool started)
		{
			CuiHelper.DestroyUi(player, Layer + ".Roulette.Button");

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0.5 0", AnchorMax = "0.5 0",
					OffsetMin = "-50 -12",
					OffsetMax = "50 12"
				},
				Text =
				{
					Text = Msg(player, started ? WaitButton : OpenButton),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 12,
					Color = "0 0 0 1"
				},
				Button =
				{
					Color = started ? "0.88 0.83 0.63 0.3" : "0.88 0.83 0.63 0.7",
					Command = started ? "" : $"UI_Cases tryopencase {caseId}"
				}
			}, Layer + ".Roulette", Layer + ".Roulette.Button");
		}

		private void CaseAcceptUi(BasePlayer player, int caseId)
		{
			var container = new CuiElementContainer();

			container.Add(new CuiButton
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Text = {Text = ""},
				Button =
				{
					Color = "0.88 0.83 0.63 0.2",
					Close = Layer + ".Modal"
				}
			}, "ContentUI", Layer + ".Modal");

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
					OffsetMin = "-150 -25", OffsetMax = "150 25"
				},
				Text = {Text = ""},
				Button =
				{
					Color = "0 0 0 0.6",
					Close = Layer + ".Modal"
				}
			}, Layer + ".Modal", Layer + ".Modal.Main");

			container.Add(new CuiLabel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "0.5 1"},
				Text =
				{
					Text = Msg(player, AcceptOpenQuestion),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 12,
					Color = _config.UI.Colors.Color3.Get()
				}
			}, Layer + ".Modal.Main");

			container.Add(new CuiButton
			{
				RectTransform = {AnchorMin = "0.5 0", AnchorMax = "1 1"},
				Text =
				{
					Text = Msg(player, AcceptOpen),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 12,
					Color = "0 0 0 1"
				},
				Button =
				{
					Color = "0.88 0.83 0.63 0.8",
					Command = $"UI_Cases opencase {caseId}",
					Close = Layer + ".Modal"
				}
			}, Layer + ".Modal.Main");

			CuiHelper.DestroyUi(player, Layer + ".Modal");
			CuiHelper.AddUi(player, container);
		}

		private void ErrorUi(BasePlayer player, string msg)
		{
			var container = new CuiElementContainer();

			container.Add(new CuiPanel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Image = {Color = _config.UI.Colors.Color10.Get()}
			}, "ContentUI", Layer + ".Modal");

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
					OffsetMin = "-127.5 -75",
					OffsetMax = "127.5 140"
				},
				Image = {Color = _config.UI.Colors.Color11.Get()}
			}, Layer + ".Modal", Layer + ".Modal.Main");

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = "0 -165",
					OffsetMax = "0 0"
				},
				Text =
				{
					Text = Msg(player, ErrorTitle),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 120,
					Color = _config.UI.Colors.Color3.Get()
				}
			}, Layer + ".Modal.Main");

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = "0 -175",
					OffsetMax = "0 -155"
				},
				Text =
				{
					Text = $"{msg}",
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 12,
					Color = _config.UI.Colors.Color3.Get()
				}
			}, Layer + ".Modal.Main");

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 0",
					OffsetMin = "0 0", OffsetMax = "0 30"
				},
				Text =
				{
					Text = Msg(player, CloseModal),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 12,
					Color = _config.UI.Colors.Color3.Get()
				},
				Button =
				{
					Color = _config.UI.Colors.Color12.Get(),
					Command = $"{_config.Commands.GetRandom()}",
					Close = Layer + ".Modal"
				}
			}, Layer + ".Modal.Main");

			CuiHelper.DestroyUi(player, Layer + ".Modal");
			CuiHelper.AddUi(player, container);
		}

		private void AwardUi(BasePlayer player, int caseId, ItemInfo item) //награды
		{
			var container = new CuiElementContainer();

			container.Add(new CuiPanel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Image = {Color = _config.UI.Colors.Color10.Get()}
			}, "ContentUI", Layer + ".Modal");

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
					OffsetMin = "-150 -100", OffsetMax = "150 100"
				},
				Image = {Color = _config.UI.Colors.Color1.Get()}
			}, Layer + ".Modal", Layer + ".Modal.Main");

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = "0 -50",
					OffsetMax = "0 0"
				},
				Text =
				{
					Text = Msg(player, YourWinnings),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 12,
					Color = _config.UI.Colors.Color3.Get()
				}
			}, Layer + ".Modal.Main");

			#region Image

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = "10 -150",
					OffsetMax = "-10 -50"
				},
				Image =
				{
					Color = _config.UI.Colors.Color2.Get()
				}
			}, Layer + ".Modal.Main", Layer + ".Modal.Image");

			if (string.IsNullOrEmpty(item.Image))
				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0.5 1", AnchorMax = "0.5 1",
						OffsetMin = "-35 -75", OffsetMax = "35 -5"
					},
					Image =
					{
						ItemId = item.ItemDefinition != null ? item.ItemDefinition.itemid : 0,
						SkinId = item.Skin
					}
				}, Layer + ".Modal.Image");
			else
				container.Add(new CuiElement
				{
					Parent = Layer + ".Modal.Image",
					Components =
					{
						new CuiRawImageComponent
						{
							Png = ImageLibrary.Call<string>("GetImage", item.Image)
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1",
							OffsetMin = "-35 -75", OffsetMax = "35 -5"
						}
					}
				});

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 0",
					OffsetMin = "0 0",
					OffsetMax = "0 20"
				},
				Text =
				{
					Text = $"{item.GetName()} ({item.Amount})",
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 12,
					Color = _config.UI.Colors.Color3.Get()
				}
			}, Layer + ".Modal.Image");

			#endregion

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 0",
					OffsetMin = "0 0",
					OffsetMax = "0 35"
				},
				Text =
				{
					Text = Msg(player, GiveNow),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 12,
					Color = _config.UI.Colors.Color3.Get()
				},
				Button =
				{
					Color = _config.UI.Colors.Color4.Get(),
					Command = "UI_Cases cpage 0",
					Close = Layer + ".Modal"
				}
			}, Layer + ".Modal.Main");

			CuiHelper.DestroyUi(player, Layer + ".Modal");
			CuiHelper.AddUi(player, container);
		}

		#endregion

		#region Editing

		#region Data

		private Dictionary<BasePlayer, EditData> _editByPlayer = new Dictionary<BasePlayer, EditData>();

		private class EditData
		{
			public CaseInfo CaseInfo;

			public List<FieldInfo> Fields = new List<FieldInfo>();

			public bool Generated;

			public Dictionary<string, int> ListPages = new Dictionary<string, int>();

			public bool GeneratedItem;

			public ItemInfo EditableItem;

			public string SelectInput;

			public string SelectedCategory;

			public int SelectPage;

			public void ClearSelect()
			{
				SelectInput = string.Empty;
				SelectedCategory = string.Empty;
				SelectPage = 0;
			}

			public void ClearEditableItem()
			{
				GeneratedItem = false;
				EditableItem = null;
			}

			public EditData(int caseId)
			{
				if (caseId == -2)
				{
					Generated = true;

					CaseInfo = new CaseInfo
					{
						ID = _instance.GenerateID(),
						Image = string.Empty,
						Permission = string.Empty,
						CooldownTime = 0,
						Items = new List<ItemInfo>(),
						Cost = 100,
						CustomCurrency = new CustomCurrency
						{
							Enabled = false,
							CostFormat = "{0} scrap",
							Type = EconomyType.Item,
							Plug = string.Empty,
							AddHook = string.Empty,
							RemoveHook = string.Empty,
							BalanceHook = string.Empty,
							ShortName = "scrap",
							DisplayName = string.Empty,
							Skin = 0
						}
					};
				}
				else
				{
					CaseInfo = (CaseInfo) _instance.FindCaseById(caseId).Clone();
				}
			}

			public int GetPage(string fieldName)
			{
				int result;
				return ListPages.TryGetValue(fieldName, out result) ? result : 0;
			}
		}

		#endregion

		#region Interface

		private void EditCaseUI(BasePlayer player)
		{
			var editData = _editByPlayer[player];

			if (editData.Fields.Count == 0)
				editData.Fields = typeof(CaseInfo).GetFields(bindingFlags).ToList()
					.FindAll(field => field.GetCustomAttribute<JsonIgnoreAttribute>() == null);

			#region Background

			var container = new CuiElementContainer
			{
				{
					new CuiPanel
					{
						RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
						Image =
						{
							Color = _config.UI.Colors.Color9.Get()
						}
					},
					Layer, ModalLayer
				}
			};

			#endregion

			#region Main

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
					OffsetMin = "-240 -275",
					OffsetMax = "240 275"
				},
				Image =
				{
					Color = _config.UI.Colors.Color1.Get()
				}
			}, ModalLayer, ModalLayer + ".Main");

			#region Header

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = "0 -50",
					OffsetMax = "0 0"
				},
				Image = {Color = "0.88 0.83 0.63 0.9"}
			}, ModalLayer + ".Main", ModalLayer + ".Header");

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 1",
					OffsetMin = "30 0",
					OffsetMax = "0 0"
				},
				Text =
				{
					Text = Msg(player, CaseEditTitle),
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-bold.ttf",
					FontSize = 14,
					Color = _config.UI.Colors.Color3.Get()
				}
			}, ModalLayer + ".Header");

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "1 1", AnchorMax = "1 1",
					OffsetMin = "-50 -37.5",
					OffsetMax = "-25 -12.5"
				},
				Text =
				{
					Text = Msg(player, CloseButton),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 10,
					Color = _config.UI.Colors.Color3.Get()
				},
				Button =
				{
					Close = ModalLayer,
					Color = _config.UI.Colors.Color11.Get(),
					Command = "UI_Cases stopedit"
				}
			}, ModalLayer + ".Header");

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "1 1", AnchorMax = "1 1",
					OffsetMin = "-105 -37.5",
					OffsetMax = "-55 -12.5"
				},
				Text =
				{
					Text = Msg(player, SaveEditCase),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 10,
					Color = _config.UI.Colors.Color3.Get()
				},
				Button =
				{
					Close = ModalLayer,
					Color = _config.UI.Colors.Color13.Get(),
					Command = "UI_Cases saveedit"
				}
			}, ModalLayer + ".Header");

			#endregion

			#region Fields

			var constXSwitch = 10f;
			var xSwitch = constXSwitch;
			var ySwitch = -60f;

			var width = 150f;
			var height = 50f;
			var margin = 5f;

			var itemsOnString = 3;

			#region Strings

			var element = 0;
			var textFields = editData.Fields.FindAll(x => x.FieldType == typeof(string) ||
			                                              x.FieldType == typeof(double) ||
			                                              x.FieldType == typeof(float) ||
			                                              x.FieldType == typeof(int));
			textFields.ForEach(field =>
			{
				EditTextField(ref container, editData.CaseInfo, field,
					ModalLayer + ".Main", CuiHelper.GetGuid(),
					"0 1", "0 1",
					$"{xSwitch} {ySwitch - height}",
					$"{xSwitch + width} {ySwitch}",
					$"UI_Cases changefield {field.Name} "
				);

				if (++element % itemsOnString == 0)
				{
					xSwitch = constXSwitch;
					ySwitch = ySwitch - height - margin;
				}
				else
				{
					xSwitch += width + margin;
				}
			});

			margin = 10f;

			if (textFields.Count % itemsOnString != 0) ySwitch = ySwitch - height - margin;

			#endregion

			#region Lists

			editData.Fields.FindAll(x => x.FieldType == typeof(List<ItemInfo>)).ForEach(field =>
			{
				EditItemsListField(ref container, ModalLayer + ".Main", player, editData.CaseInfo, editData, field,
					ref ySwitch);
			});

			ySwitch -= margin;

			#endregion

			#region Classes

			editData.Fields
				.FindAll(field =>
					field.FieldType.IsClass &&
					(field.FieldType.Namespace == null || !field.FieldType.Namespace.StartsWith("System"))).ForEach(
					field =>
					{
						EditClassField(ref container,
							ref field,
							field.GetValue(editData.CaseInfo),
							ModalLayer + ".Main", null,
							$"UI_Cases changeclassfield_case {field.Name}",
							ref ySwitch
						);
					});

			ySwitch -= margin;

			#endregion

			#endregion

			#endregion

			CuiHelper.DestroyUi(player, ModalLayer);
			CuiHelper.AddUi(player, container);
		}

		private void EditItemUi(BasePlayer player)
		{
			var editData = _editByPlayer[player];

			var item = editData.EditableItem;
			if (item == null) return;

			var fields = item.GetType().GetFields(bindingFlags).ToList()
				.FindAll(field => field.GetCustomAttribute<JsonIgnoreAttribute>() == null);

			var container = new CuiElementContainer();

			#region Background

			container.Add(new CuiPanel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Image =
				{
					Color = _config.UI.Colors.Color9.Get()
				}
			}, ModalLayer, ModalLayer + ".Edit.Item");

			#endregion

			#region Main

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
					OffsetMin = "-240 -220",
					OffsetMax = "240 220"
				},
				Image =
				{
					Color = _config.UI.Colors.Color1.Get()
				}
			}, ModalLayer + ".Edit.Item", ModalLayer + ".Edit.Item.Main");

			#region Header

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = "0 -50",
					OffsetMax = "0 0"
				},
				Image = {Color = _config.UI.Colors.Color2.Get()}
			}, ModalLayer + ".Edit.Item.Main", ModalLayer + ".Edit.Item.Main.Header");

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 1",
					OffsetMin = "30 0",
					OffsetMax = "0 0"
				},
				Text =
				{
					Text = Msg(player, ItemEditingTitle),
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-bold.ttf",
					FontSize = 14,
					Color = _config.UI.Colors.Color3.Get()
				}
			}, ModalLayer + ".Edit.Item.Main.Header");

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "1 1", AnchorMax = "1 1",
					OffsetMin = "-50 -37.5",
					OffsetMax = "-25 -12.5"
				},
				Text =
				{
					Text = Msg(player, CloseButton),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 10,
					Color = _config.UI.Colors.Color3.Get()
				},
				Button =
				{
					Color = _config.UI.Colors.Color4.Get(),
					Close = ModalLayer + ".Edit.Item",
					Command =
						"UI_Cases edititem_close"
				}
			}, ModalLayer + ".Edit.Item.Main.Header");

			#endregion

			#region Image

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "0 1",
					OffsetMin = "10 -200",
					OffsetMax = "145 -65"
				},
				Image = {Color = _config.UI.Colors.Color2.Get()}
			}, ModalLayer + ".Edit.Item.Main", ModalLayer + ".Edit.Item.Main.Image");

			#region Image

			if (!string.IsNullOrEmpty(item.Image))
			{
				container.Add(new CuiElement
				{
					Parent = ModalLayer + ".Edit.Item.Main.Image",
					Components =
					{
						new CuiRawImageComponent
						{
							Png = ImageLibrary.Call<string>("GetImage", item.Image)
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = "5 5", OffsetMax = "-5 -5"
						}
					}
				});
			}
			else
			{
				if (item.ItemDefinition != null)
					container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = "5 5", OffsetMax = "-5 -5"
						},
						Image =
						{
							ItemId = item.ItemDefinition.itemid,
							SkinId = item.Skin
						}
					}, ModalLayer + ".Edit.Item.Main.Image");
			}

			#endregion

			#endregion

			#region Fields

			var constXSwitch = 155f;
			var xSwitch = constXSwitch;
			var width = 150f;
			var height = 45f;
			var margin = 5f;
			var ySwitch = -65f;

			var itemsOnString = 2f;

			var element = 0;
			fields.FindAll(field => field.Name != "Image" && (field.FieldType == typeof(string) ||
			                                                  field.FieldType == typeof(double) ||
			                                                  field.FieldType == typeof(float) ||
			                                                  field.FieldType == typeof(int))).ForEach(field =>
			{
				var name = CuiHelper.GetGuid();

				EditTextField(ref container, item, field,
					ModalLayer + ".Edit.Item.Main", name,
					"0 1", "0 1",
					$"{xSwitch} {ySwitch - height}",
					$"{xSwitch + width} {ySwitch}",
					$"UI_Cases edititem_field {field.Name} "
				);

				if (field.Name == "ShortName")
					container.Add(new CuiButton
					{
						RectTransform =
						{
							AnchorMin = "1 0", AnchorMax = "1 0",
							OffsetMin = "-25 0",
							OffsetMax = "0 25"
						},
						Text =
						{
							Text = Msg(player, EditBtn),
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-regular.ttf",
							FontSize = 16,
							Color = "1 1 1 1"
						},
						Button =
						{
							Color = _config.UI.Colors.Color4.Get(),
							Command = "UI_Cases start_selectitem"
						}
					}, name);

				if (ySwitch - height < -200)
				{
					itemsOnString = 3;
					constXSwitch = 10f;
				}

				if (++element % itemsOnString == 0)
				{
					xSwitch = constXSwitch;
					ySwitch = ySwitch - height - margin;
				}
				else
				{
					xSwitch += width + margin;
				}
			});

			#endregion

			#region Classes

			ySwitch = ySwitch - height - margin;

			fields
				.FindAll(field =>
					field.FieldType.IsClass &&
					(field.FieldType.Namespace == null || !field.FieldType.Namespace.StartsWith("System"))
					&& field.GetCustomAttribute<JsonIgnoreAttribute>() == null).ForEach(field =>
				{
					EditClassField(ref container,
						ref field,
						field.GetValue(item),
						ModalLayer + ".Edit.Item.Main", null,
						$"UI_Cases changeclassfield_item {field.Name}",
						ref ySwitch
					);
				});

			#endregion

			#endregion

			CuiHelper.DestroyUi(player, ModalLayer + ".Edit.Item");
			CuiHelper.AddUi(player, container);
		}

		private void SelectItemUi(BasePlayer player)
		{
			var container = new CuiElementContainer();

			var editData = _editByPlayer[player];

			if (string.IsNullOrEmpty(editData.SelectedCategory))
				editData.SelectedCategory = _itemsCategories.FirstOrDefault().Key;

			#region Background

			container.Add(new CuiPanel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Image =
				{
					Color = _config.UI.Colors.Color9.Get()
				}
			}, ModalLayer, ModalLayer + ".Select.Item");

			container.Add(new CuiButton
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Text = {Text = ""},
				Button =
				{
					Color = "0 0 0 0",
					Close = ModalLayer + ".Select.Item",
					Command = "UI_Cases selectitem_close"
				}
			}, ModalLayer + ".Select.Item");

			#endregion

			#region Main

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
					OffsetMin = "-260 -270",
					OffsetMax = "260 280"
				},
				Image =
				{
					Color = _config.UI.Colors.Color1.Get()
				}
			}, ModalLayer + ".Select.Item", ModalLayer + ".Select.Main");

			#region Categories

			var amountOnString = 4;
			var Width = 120f;
			var Height = 25f;
			var xMargin = 5f;
			var yMargin = 5f;

			var constSwitch = -(amountOnString * Width + (amountOnString - 1) * xMargin) / 2f;
			var xSwitch = constSwitch;
			var ySwitch = -15f;

			var i = 1;
			foreach (var cat in _itemsCategories)
			{
				container.Add(new CuiButton
				{
					RectTransform =
					{
						AnchorMin = "0.5 1", AnchorMax = "0.5 1",
						OffsetMin = $"{xSwitch} {ySwitch - Height}",
						OffsetMax = $"{xSwitch + Width} {ySwitch}"
					},
					Text =
					{
						Text = $"{cat.Key}",
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf",
						FontSize = 10,
						Color = "1 1 1 1"
					},
					Button =
					{
						Color = editData.SelectedCategory == cat.Key
							? _config.UI.Colors.Color4.Get()
							: _config.UI.Colors.Color2.Get(),
						Command = $"UI_Cases selectitem category {cat.Key}"
					}
				}, ModalLayer + ".Select.Main");

				if (i % amountOnString == 0)
				{
					ySwitch = ySwitch - Height - yMargin;
					xSwitch = constSwitch;
				}
				else
				{
					xSwitch += xMargin + Width;
				}

				i++;
			}

			#endregion

			#region Items

			amountOnString = 5;

			var strings = 4;
			var totalAmount = amountOnString * strings;

			ySwitch = ySwitch - yMargin - Height - 10f;

			Width = 85f;
			Height = 85f;
			xMargin = 15f;
			yMargin = 5f;

			constSwitch = -(amountOnString * Width + (amountOnString - 1) * xMargin) / 2f;
			xSwitch = constSwitch;

			i = 1;

			var canSearch = !string.IsNullOrEmpty(editData.SelectInput) && editData.SelectInput.Length > 2;

			var temp = canSearch
				? _itemsCategories
					.SelectMany(x => x.Value)
					.Where(x => x.Value.StartsWith(editData.SelectInput) || x.Value.Contains(editData.SelectInput) ||
					            x.Value.EndsWith(editData.SelectInput))
					.ToList()
				: _itemsCategories[editData.SelectedCategory];

			var itemsAmount = temp.Count;
			var Items = temp.Skip(editData.SelectPage * totalAmount).Take(totalAmount).ToList();

			Items.ForEach(item =>
			{
				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0.5 1", AnchorMax = "0.5 1",
						OffsetMin = $"{xSwitch} {ySwitch - Height}",
						OffsetMax = $"{xSwitch + Width} {ySwitch}"
					},
					Image = {Color = _config.UI.Colors.Color2.Get()}
				}, ModalLayer + ".Select.Main", ModalLayer + $".Select.Main.Item.{item.Key}");

				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "5 5", OffsetMax = "-5 -5"
					},
					Image =
					{
						ItemId = item.Key
					}
				}, ModalLayer + $".Select.Main.Item.{item.Key}");

				container.Add(new CuiButton
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Text = {Text = ""},
					Button =
					{
						Color = "0 0 0 0",
						Command = $"UI_Cases takeitem {item.Value}",
						Close = ModalLayer + ".Select.Item"
					}
				}, ModalLayer + $".Select.Main.Item.{item.Key}");

				if (i % amountOnString == 0)
				{
					xSwitch = constSwitch;
					ySwitch = ySwitch - yMargin - Height;
				}
				else
				{
					xSwitch += xMargin + Width;
				}

				i++;
			});

			#endregion

			#region Search

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 0", AnchorMax = "0.5 0",
					OffsetMin = "-90 10", OffsetMax = "90 35"
				},
				Image = {Color = _config.UI.Colors.Color4.Get()}
			}, ModalLayer + ".Select.Main", ModalLayer + ".Select.Main.Search");

			container.Add(new CuiElement
			{
				Parent = ModalLayer + ".Select.Main.Search",
				Components =
				{
					new CuiInputFieldComponent
					{
						FontSize = 10,
						Align = TextAnchor.MiddleLeft,
						Command = "UI_Cases selectitem search ",
						Color = "1 1 1 0.95",
						CharsLimit = 150,
						Text = canSearch ? $"{editData.SelectInput}" : Msg(player, ItemSearch)
					},
					new CuiRectTransformComponent
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "10 0", OffsetMax = "0 0"
					}
				}
			});

			#endregion

			#region Pages

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "0 0",
					OffsetMin = "10 10",
					OffsetMax = "80 35"
				},
				Text =
				{
					Text = Msg(player, BackButton),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = "1 1 1 1"
				},
				Button =
				{
					Color = _config.UI.Colors.Color2.Get(),
					Command =
						editData.SelectPage != 0
							? $"UI_Cases selectitem page {editData.SelectPage - 1}"
							: ""
				}
			}, ModalLayer + ".Select.Main");

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "1 0", AnchorMax = "1 0",
					OffsetMin = "-80 10",
					OffsetMax = "-10 35"
				},
				Text =
				{
					Text = Msg(player, NextButton),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = "1 1 1 1"
				},
				Button =
				{
					Color = _config.UI.Colors.Color4.Get(),
					Command = itemsAmount > (editData.SelectPage + 1) * totalAmount
						? $"UI_Cases selectitem page {editData.SelectPage + 1}"
						: ""
				}
			}, ModalLayer + ".Select.Main");

			#endregion

			#endregion

			CuiHelper.DestroyUi(player, ModalLayer + ".Select.Item");
			CuiHelper.AddUi(player, container);
		}

		#region Components

		private void EditTextField(ref CuiElementContainer container,
			object objectInfi,
			FieldInfo field,
			string parent, string name,
			string aMin, string aMax, string oMin, string oMax,
			string command)
		{
			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = aMin,
					AnchorMax = aMax,
					OffsetMin = oMin,
					OffsetMax = oMax
				},
				Image =
				{
					Color = "0 0 0 0"
				}
			}, parent, name);

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = "0 -20", OffsetMax = "0 0"
				},
				Text =
				{
					Text = $"{field.GetCustomAttribute<JsonPropertyAttribute>()?.PropertyName ?? "UKNOWN"}",
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = "1 1 1 1"
				}
			}, name);

			container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 1",
					OffsetMin = "0 0", OffsetMax = "0 -20"
				},
				Image = {Color = "0 0 0 0"}
			}, name, $"{name}.Value");

			CreateOutLine(ref container, $"{name}.Value", _config.UI.Colors.Color2.Get());

			container.Add(new CuiElement
			{
				Parent = $"{name}.Value",
				Components =
				{
					new CuiInputFieldComponent
					{
						FontSize = 12,
						Align = TextAnchor.MiddleLeft,
						Command = $"{command}",
						Color = "1 1 1 0.65",
						CharsLimit = 150,
						NeedsKeyboard = true,
						Text = $"{field.GetValue(objectInfi)}"
					},
					new CuiRectTransformComponent
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "10 0", OffsetMax = "0 0"
					}
				}
			});
		}

		private void EditItemsListField(ref CuiElementContainer container,
			string parent,
			BasePlayer player,
			CaseInfo caseInfo,
			EditData editData,
			FieldInfo field,
			ref float ySwitch)
		{
			var list = (List<ItemInfo>) field.GetValue(caseInfo);
			if (list == null) return;

			var amountOnString = 7;

			#region Title

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "0 1",
					OffsetMin = $"10 {ySwitch - 20f}",
					OffsetMax = $"100 {ySwitch}"
				},
				Text =
				{
					Text = $"{field.GetCustomAttribute<JsonPropertyAttribute>()?.PropertyName ?? "UKNOWN"}",
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-regular.ttf",
					FontSize = 12,
					Color = "1 1 1 1"
				}
			}, parent);

			#endregion

			#region Buttons

			#region Add

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "0 1",
					OffsetMin = $"45 {ySwitch - 20f}",
					OffsetMax = $"65 {ySwitch}"
				},
				Text =
				{
					Text = Msg(player, CaseItemsAdd),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = "1 1 1 1"
				},
				Button =
				{
					Color = _config.UI.Colors.Color4.Get(),
					Command = "UI_Cases start_editiem -1"
				}
			}, parent);

			#endregion

			#region Back

			var nowPage = editData.GetPage(field.Name);
			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "0 1",
					OffsetMin = $"70 {ySwitch - 20f}",
					OffsetMax = $"90 {ySwitch}"
				},
				Text =
				{
					Text = Msg(player, BtnBack),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = "1 1 1 1"
				},
				Button =
				{
					Color = _config.UI.Colors.Color4.Get(),
					Command = nowPage != 0
						? $"UI_Cases listchangepage {field.Name} {nowPage - 1}"
						: ""
				}
			}, parent);

			#endregion

			#region Next

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "0 1",
					OffsetMin = $"95 {ySwitch - 20f}",
					OffsetMax = $"115 {ySwitch}"
				},
				Text =
				{
					Text = Msg(player, BtnNext),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 10,
					Color = "1 1 1 1"
				},
				Button =
				{
					Color = _config.UI.Colors.Color4.Get(),
					Command = list.Count > (nowPage + 1) * amountOnString
						? $"UI_Cases listchangepage {field.Name} {nowPage + 1}"
						: ""
				}
			}, parent);

			#endregion

			#endregion

			ySwitch -= 25f;

			#region Items

			var xSwitch = 10f;
			var width = 60f;
			var height = 60f;
			var margin = 5f;

			foreach (var item in list.Skip(nowPage * amountOnString).Take(amountOnString))
			{
				container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "0 1",
						OffsetMin = $"{xSwitch} {ySwitch - height}",
						OffsetMax = $"{xSwitch + width} {ySwitch}"
					},
					Image =
					{
						Color = _config.UI.Colors.Color2.Get()
					}
				}, parent, ModalLayer + $".Items.{xSwitch}");

				#region Image

				if (!string.IsNullOrEmpty(item.Image))
					container.Add(new CuiElement
					{
						Parent = ModalLayer + $".Items.{xSwitch}",
						Components =
						{
							new CuiRawImageComponent
							{
								Png = ImageLibrary.Call<string>("GetImage", item.Image)
							},
							new CuiRectTransformComponent
							{
								AnchorMin = "0 0", AnchorMax = "1 1",
								OffsetMin = "5 5", OffsetMax = "-5 -5"
							}
						}
					});
				else
					container.Add(new CuiPanel
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = "5 5", OffsetMax = "-5 -5"
						},
						Image =
						{
							ItemId = item.ItemDefinition.itemid,
							SkinId = item.Skin
						}
					}, ModalLayer + $".Items.{xSwitch}");

				#endregion

				#region Amount

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "0 2",
						OffsetMax = "-2 0"
					},
					Text =
					{
						Text = $"{item.Amount}",
						Align = TextAnchor.LowerRight,
						Font = "robotocondensed-regular.ttf",
						FontSize = 10,
						Color = "1 1 1 0.9"
					}
				}, ModalLayer + $".Items.{xSwitch}");

				#endregion

				#region Edit

				container.Add(new CuiButton
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Text = {Text = ""},
					Button =
					{
						Color = "0 0 0 0",
						Command =
							$"UI_Cases start_editiem {item.ID}"
					}
				}, ModalLayer + $".Items.{xSwitch}");

				#endregion

				xSwitch += width + margin;
			}

			ySwitch -= height;

			#endregion
		}

		private void EditClassField(ref CuiElementContainer container,
			ref FieldInfo field,
			object fieldObject,
			string parent, string name,
			string command,
			ref float ySwitch)
		{
			if (string.IsNullOrEmpty(name))
				name = CuiHelper.GetGuid();

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "0 1",
					OffsetMin = $"10 {ySwitch - 20f}",
					OffsetMax = $"100 {ySwitch}"
				},
				Text =
				{
					Text = $"{field.GetCustomAttribute<JsonPropertyAttribute>()?.PropertyName ?? "UKNOWN"}",
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-regular.ttf",
					FontSize = 12,
					Color = "1 1 1 1"
				}
			}, parent);

			ySwitch -= 25f;

			#region Fields

			var constXSwitch = 10f;
			var xSwitch = constXSwitch;

			var width = 150f;
			var height = 50f;
			var margin = 5f;

			var itemsOnString = 3;

			var fields = fieldObject.GetType().GetFields(bindingFlags).ToList();

			var element = 0;

			#region Text Fields

			var textFields = fields.FindAll(x => x.FieldType == typeof(string) ||
			                                     x.FieldType == typeof(double) ||
			                                     x.FieldType == typeof(float) ||
			                                     x.FieldType == typeof(int));

			foreach (var textField in textFields)
			{
				EditTextField(ref container,
					fieldObject,
					textField,
					parent,
					CuiHelper.GetGuid(),
					"0 1", "0 1",
					$"{xSwitch} {ySwitch - height}",
					$"{xSwitch + width} {ySwitch}",
					$"{command} {textField.Name} "
				);

				if (++element % itemsOnString == 0)
				{
					xSwitch = constXSwitch;
					ySwitch = ySwitch - height - margin;
				}
				else
				{
					xSwitch += width + margin;
				}
			}

			#endregion

			#endregion
		}

		#endregion

		#endregion

		#endregion

		#region Utils

		private double GetPlayTime(BasePlayer player)
		{
			return Convert.ToDouble(PlaytimeTracker?.Call("GetPlayTime", player.UserIDString));
		}

		private bool CanEditCase(BasePlayer player)
		{
			return permission.UserHasPermission(player.UserIDString, PERM_EDIT);
		}

		private static void CreateOutLine(ref CuiElementContainer container, string parent, string color,
			float size = 2)
		{
			container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 0",
						AnchorMax = "1 0",
						OffsetMin = $"{size} 0",
						OffsetMax = $"-{size} {size}"
					},
					Image = {Color = color}
				},
				parent);
			container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 1",
						AnchorMax = "1 1",
						OffsetMin = $"{size} -{size}",
						OffsetMax = $"-{size} 0"
					},
					Image = {Color = color}
				},
				parent);
			container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "0 1",
						OffsetMin = "0 0",
						OffsetMax = $"{size} 0"
					},
					Image = {Color = color}
				},
				parent);
			container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "1 0",
						AnchorMax = "1 1",
						OffsetMin = $"-{size} 0",
						OffsetMax = "0 0"
					},
					Image = {Color = color}
				},
				parent);
		}

		private void TimeHandler()
		{
			if (_config.PlaytimeTrackerConf.Enabled) TimeHandlingPlaytime();

			if (_config.TimeCasesSettings.Enable) TimeHandlingTimeCases();
		}

		private void TimeHandlingTimeCases()
		{
			var toRemove = Pool.GetList<BasePlayer>();

			var givedPlayers = Pool.GetList<BasePlayer>();

			TimePlayers.ToList().ForEach(check =>
			{
				var player = check.Key;
				if (player == null || !player.IsConnected)
				{
					toRemove.Add(player);
					return;
				}

				if (Time.time - check.Value >= _config.TimeCasesSettings.Cooldown)
				{
					GiveCase(player, _config.TimeCasesSettings.Cases.GetRandom());

					givedPlayers.Add(player);
				}
			});

			givedPlayers.ForEach(player => TimePlayers[player] = Time.time);
			Pool.FreeList(ref givedPlayers);

			toRemove.ForEach(player => TimePlayers.Remove(player));
			Pool.FreeList(ref toRemove);
		}

		private void TimeHandlingPlaytime()
		{
			foreach (var player in BasePlayer.activePlayerList)
			{
				var playTime = GetPlayTime(player);

				foreach (var caseData in _config.PlaytimeTrackerConf.Cases)
				{
					var time = caseData.Key;
					if (playTime >= time)
					{
						var data = GetPlayerData(player);
						if (time > data.LastPlayTime)
						{
							data.LastPlayTime = time;

							GiveCase(player, caseData.Value);
							break;
						}
					}
				}
			}
		}

		private void GiveCase(BasePlayer target, int caseId, int amount = 1)
		{
			Reply(target, GiveBonusCase);

			GiveCase(target.userID, caseId, amount);
		}

		private void GiveCase(ulong target, int caseId, int amount = 1)
		{
			var data = GetPlayerData(target);
			if (data == null) return;

			if (data.Cases.ContainsKey(caseId))
				data.Cases[caseId] += amount;
			else
				data.Cases.Add(caseId, amount);
		}

		private static void SendEffect(BasePlayer player, string effect)
		{
			EffectNetwork.Send(new Effect(effect, player, 0, new Vector3(), new Vector3()), player.Connection);
		}

		private string FormatShortTime(BasePlayer player, TimeSpan time)
		{
			var result = string.Empty;

			if (time.Days != 0)
				result += Msg(player, DaysFormat, time.Days);

			if (time.Hours != 0)
				result += Msg(player, HoursFormat, time.Hours);

			if (time.Minutes != 0)
				result += Msg(player, MinutesFormat, time.Minutes);

			if (time.Seconds != 0)
				result += Msg(player, SecondsFormat, time.Seconds);

			return result;
		}

		private void CheckOnDuplicates()
		{
			var items = _config.Cases.SelectMany(x => x.Items)
				.GroupBy(x => x.ID)
				.Where(group => group.Count() > 1)
				.Select(group => group.Key).ToArray();

			if (items.Length > 0)
				PrintError(
					$"Matching item IDs found (Cases): {string.Join(", ", items.Select(x => x.ToString()))}");
		}

		private void CaseInit(CaseInfo caseInfo)
		{
			if (!string.IsNullOrEmpty(caseInfo.Permission) && !permission.PermissionExists(caseInfo.Permission))
				permission.RegisterPermission(caseInfo.Permission, this);

			caseInfo.Items.Sort((x, y) => x.Chance.CompareTo(y.Chance));
		}

		private static List<ItemInfo> GetItems(CaseInfo caseInfo)
		{
			var result = new List<ItemInfo>();

			for (var i = 0; i < _instance._config.AmountItems; i++)
			{
				ItemInfo item = null;
				var iteration = 0;
				do
				{
					iteration++;

					var randomItem = caseInfo.Items.GetRandom();
					if (randomItem.Chance < 1 || randomItem.Chance > 100)
						continue;

					if (Random.Range(0f, 100f) <= randomItem.Chance)
						item = randomItem;
				} while (item == null && iteration < 1000);

				if (item != null)
					result.Add(item);
			}

			return result;
		}

		private void FillItems()
		{
			_casesByIDs.Clear();
			_itemByIDs.Clear();

			_config.BonusCaseInfo.Items.ForEach(item =>
			{
				if (_itemByIDs.ContainsKey(item.ID))
					PrintError($"Items with the same ID found {item.ID}");
				else
					_itemByIDs.Add(item.ID, item);
			});

			_config.Cases.ForEach(@case =>
			{
				if (!_casesByIDs.ContainsKey(@case.ID))
					_casesByIDs.Add(@case.ID, @case);

				@case.Items.ForEach(item =>
				{
					if (_itemByIDs.ContainsKey(item.ID))
						PrintError($"Items with the same ID found {item.ID}");
					else
						_itemByIDs.Add(item.ID, item);
				});
			});
		}

		private int GenerateID()
		{
			var result = -1;

			do
			{
				var val = Random.Range(0, int.MaxValue);

				if (!_casesByIDs.ContainsKey(val) && val != result)
					result = val;
			} while (result == -1);

			return result;
		}

		private int GenerateItemID()
		{
			var result = -1;

			do
			{
				var val = Random.Range(0, int.MaxValue);

				if (!_itemByIDs.ContainsKey(val) && val != result)
					result = val;
			} while (result == -1);

			return result;
		}

		private CaseInfo FindCaseById(int caseId)
		{
			CaseInfo caseInfo;
			return caseId == -1 ? _config.BonusCaseInfo :
				_casesByIDs.TryGetValue(caseId, out caseInfo) ? caseInfo : null;
		}

		private ItemInfo FindItemById(int id)
		{
			ItemInfo item;
			return _itemByIDs.TryGetValue(id, out item) ? item : null;
		}

		private void GiveItem(BasePlayer player, int itemId)
		{
			var item = FindItemById(itemId);
			if (item == null) return;

			item.Get(player);
			SendNotify(player, GiveItemMsg, 0, item.GetName());

			_playersItems.Remove(player);
		}

		private IEnumerable<CaseInfo> AllCases
		{
			get
			{
				if (_config.BonusCaseInfo.Enabled)
					yield return _config.BonusCaseInfo;

				foreach (var @case in _config.Cases)
					yield return @case;
			}
		}

		private void LoadItems()
		{
			ItemManager.itemList.ForEach(item =>
			{
				var itemCategory = item.category.ToString();

				var kvp = new KeyValuePair<int, string>(item.itemid, item.shortname);

				if (_itemsCategories.ContainsKey(itemCategory))
				{
					if (!_itemsCategories[itemCategory].Contains(kvp))
						_itemsCategories[itemCategory].Add(kvp);
				}
				else
				{
					_itemsCategories.Add(itemCategory, new List<KeyValuePair<int, string>> {kvp});
				}
			});
		}

		private void LoadImages()
		{
			if (!ImageLibrary)
			{
				PrintWarning("IMAGE LIBRARY IS NOT INSTALLED");
			}
			else
			{
				var imagesList = new Dictionary<string, string>();

				var itemIcons = new List<KeyValuePair<string, ulong>>();

				foreach (var caseInfo in AllCases)
				{
					CaseInit(caseInfo);

					if (!string.IsNullOrEmpty(caseInfo.Image) && !imagesList.ContainsKey(caseInfo.Image))
						imagesList.Add(caseInfo.Image, caseInfo.Image);

					caseInfo.Items.ForEach(item =>
					{
						if (!string.IsNullOrEmpty(item.Image) && !imagesList.ContainsKey(item.Image))
							imagesList.Add(item.Image, item.Image);

						itemIcons.Add(new KeyValuePair<string, ulong>(item.ShortName, item.Skin));
					});
				}

				if (itemIcons.Count > 0) ImageLibrary?.Call("LoadImageList", Title, itemIcons, null);

				ImageLibrary?.Call("ImportImageList", Title, imagesList, 0UL, true);
			}
		}

		private static int ItemCount(Item[] items, string shortname, ulong skin)
		{
			return items.Where(item =>
					item.info.shortname == shortname && !item.isBroken && (skin == 0 || item.skin == skin))
				.Sum(item => item.amount);
		}

		private static void Take(IEnumerable<Item> itemList, string shortname, ulong skinId, int iAmount)
		{
			var num1 = 0;
			if (iAmount == 0) return;

			var list = Pool.GetList<Item>();

			foreach (var item in itemList)
			{
				if (item.info.shortname != shortname ||
				    (skinId != 0 && item.skin != skinId) || item.isBroken) continue;

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

		#region Cooldown

		private readonly Dictionary<BasePlayer, List<CaseInfo>> CasesToUpdate =
			new Dictionary<BasePlayer, List<CaseInfo>>();

		private Dictionary<ulong, Cooldown> Cooldowns = new Dictionary<ulong, Cooldown>();

		private void CooldownController()
		{
			var toRemove = Pool.GetList<BasePlayer>();

			CasesToUpdate.ToList().ForEach(check =>
			{
				if (check.Key == null)
				{
					toRemove.Add(check.Key);
					return;
				}

				var container = new CuiElementContainer();

				check.Value.ToList().ForEach(caseData =>
				{
					RefCaseUi(check.Key, ref container, caseData);

					var cooldownTime = GetCooldownTime(check.Key.userID, caseData);
					if (cooldownTime <= 0) RemoveCooldown(check.Key, caseData);
				});

				CuiHelper.AddUi(check.Key, container);
			});

			toRemove.ForEach(x => CasesToUpdate.Remove(x));
			Pool.FreeList(ref toRemove);
		}

		private Cooldown GetCooldown(ulong player)
		{
			Cooldown cooldown;
			return Cooldowns.TryGetValue(player, out cooldown) ? cooldown : null;
		}

		private CooldownData GetCooldown(ulong player, CaseInfo caseInfoData)
		{
			return GetCooldown(player)?.GetCooldown(caseInfoData);
		}

		private int GetCooldownTime(ulong player, CaseInfo caseInfoData)
		{
			return GetCooldown(player)?.GetCooldownTime(caseInfoData) ?? -1;
		}

		private void SetCooldown(BasePlayer player, CaseInfo caseInfoData)
		{
			if (Cooldowns.ContainsKey(player.userID))
				Cooldowns[player.userID].SetCooldown(caseInfoData);
			else
				Cooldowns.Add(player.userID, new Cooldown().SetCooldown(caseInfoData));
		}

		private void RemoveCooldown(BasePlayer player, CaseInfo caseInfoData)
		{
			if (!Cooldowns.ContainsKey(player.userID)) return;

			CasesToUpdate[player].Remove(caseInfoData);

			Cooldowns[player.userID].RemoveCooldown(caseInfoData);

			if (Cooldowns[player.userID].Data.Count == 0)
			{
				Cooldowns.Remove(player.userID);

				CasesToUpdate.Remove(player);
			}
		}

		private class Cooldown
		{
			#region Fields

			[JsonProperty(PropertyName = "Cooldowns", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public readonly Dictionary<int, CooldownData> Data = new Dictionary<int, CooldownData>();

			#endregion

			#region Utils

			public bool Any(CaseInfo caseInfoData)
			{
				var data = GetCooldown(caseInfoData);
				return data != null && (DateTime.Now - data.LastTime).Seconds > caseInfoData.CooldownTime;
			}

			public CooldownData GetCooldown(CaseInfo caseInfoData)
			{
				CooldownData data;
				return Data.TryGetValue(caseInfoData.ID, out data) ? data : null;
			}

			public int GetCooldownTime(CaseInfo caseInfoData)
			{
				var data = GetCooldown(caseInfoData);
				if (data == null) return -1;

				return (int) (data.LastTime.AddSeconds(caseInfoData.CooldownTime) - DateTime.Now).TotalSeconds;
			}

			public void RemoveCooldown(CaseInfo caseInfoData)
			{
				Data.Remove(caseInfoData.ID);
			}

			public Cooldown SetCooldown(CaseInfo caseInfoData)
			{
				if (Data.ContainsKey(caseInfoData.ID))
					Data[caseInfoData.ID].LastTime = DateTime.Now;
				else
					Data.Add(caseInfoData.ID, new CooldownData {LastTime = DateTime.Now});

				return this;
			}

			#endregion
		}

		private class CooldownData
		{
			public DateTime LastTime;
		}

		#endregion

		#endregion

		#region Component

		private class OpenCase : FacepunchBehaviour
		{
			private BasePlayer _player;

			private int index;

			private int Count;

			private int CaseId;

			private List<ItemInfo> Items;

			public bool Started;

			private void Awake()
			{
				_player = GetComponent<BasePlayer>();
			}

			public void StartOpen(int caseId, List<ItemInfo> items)
			{
				Started = true;

				Count = items.Count;

				Items = items;

				index = 0;

				CaseId = caseId;

				var container = new CuiElementContainer();
				_instance.RouletteButton(ref container, _player, caseId, true);
				CuiHelper.AddUi(_player, container);

				Handle();
			}

			private void Handle()
			{
				CancelInvoke(Handle);

				if (!Started)
					return;

				#region Finish

				if (index < 0 || index >= Count - 5)
				{
					Finish();
					return;
				}

				#endregion

				#region Roulette

				Items.RemoveAt(0);

				var container = new CuiElementContainer();
				_instance.RouletteUi(ref container, _player, Items);
				CuiHelper.AddUi(_player, container);

				index++;

				SendEffect(_player, "assets/bundled/prefabs/fx/notice/stack.world.fx.prefab");

				#endregion

				Invoke(Handle, GetTime());
			}

			private float GetTime()
			{
				float time;

				var percent = (1f - (float) (index + 1) / Count) * 100f;
				if (percent < 10)
					time = 1.25f;
				else if (percent < 20)
					time = 1.5f;
				else if (percent < 30)
					time = 0.75f;
				else
					time = 0.2f;

				return time;
			}

			public void Finish(bool unload = false)
			{
				Started = false;

				if (Items.Count > 2)
				{
					var award = Items[2];
					if (award != null)
					{
						SendEffect(_player,
							"assets/prefabs/deployable/vendingmachine/effects/vending-machine-purchase-human.prefab");

						_instance.GiveItem(_player, award.ID);

						_instance.AwardUi(_player, CaseId, award);
					}
				}

				Kill();
			}

			private void OnDestroy()
			{
				CancelInvoke();

				Destroy(this);
			}

			public void Kill()
			{
				DestroyImmediate(this);
			}
		}

		#endregion

		#region Lang

		private const string
			GiveBonusCase = "GiveBonusCase",
			ItemSearch = "ItemSearch",
			EditBtn = "EditBtn",
			EditCaseTitle = "EditCaseTitle",
			AddCaseTitle = "AddCaseTitle",
			SaveEditCase = "SaveEditCase",
			ItemEditingTitle = "ItemEditingTitle",
			CaseEditTitle = "CaseEditTitle",
			BtnNext = "BtnNext",
			BtnBack = "BtnBack",
			CaseItemsAdd = "CaseItemsAdd",
			ErrorTitle = "ErrorTitle",
			PagesFormat = "PagesFormat",
			BalanceTitle = "BalanceTitle",
			NoPermissions = "NoPermissions",
			DaysFormat = "DaysFormat",
			HoursFormat = "HoursFormat",
			MinutesFormat = "MinutesFormat",
			SecondsFormat = "SecondsFormat",
			CaseCost = "CaseCost",
			CaseCurrency = "CaseCurrency",
			Open = "Open",
			Availabe = "Availabe",
			DelayAvailable = "DelayAvailable",
			TitleBonusCase = "TitleBonusCase",
			CloseButton = "CloseButton",
			TitleMenu = "TitleMenu",
			BackButton = "BackButton",
			NextButton = "NextButton",
			GoBack = "GoBack",
			ItemList = "ItemList",
			OpenButton = "OpenButton",
			WaitButton = "WaitButton",
			AcceptOpenQuestion = "AcceptOpenQuestion",
			AcceptOpen = "AcceptOpen",
			CloseModal = "CloseModal",
			YourWinnings = "YourWinnings",
			GiveNow = "GiveNow",
			NotEnoughMoney = "NotEnoughMoney",
			GiveItemMsg = "GiveItemMsg";

		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
				[DaysFormat] = " {0} d. ",
				[HoursFormat] = " {0} h. ",
				[MinutesFormat] = " {0} m. ",
				[SecondsFormat] = " {0} s. ",
				[CaseCost] = "Case cost",
				[CaseCurrency] = "{0} $",
				[Open] = "Open",
				[Availabe] = "AVAILABE",
				[DelayAvailable] = "Will be available in",
				[TitleBonusCase] = "Bonus case",
				[CloseButton] = "✕",
				[TitleMenu] = "Case Menu",
				[BackButton] = "Back",
				[NextButton] = "Next",
				[GoBack] = "Go back",
				[ItemList] = "List of items",
				[OpenButton] = "Open case",
				[WaitButton] = "Wait",
				[AcceptOpenQuestion] = "Open the case?",
				[AcceptOpen] = "Open case",
				[CloseModal] = "CLOSE",
				[YourWinnings] = "Your winnings",
				[GiveNow] = "Pick up now",
				[NotEnoughMoney] = "You don't have enough money!",
				[GiveItemMsg] = "You got the '{0}'",
				[NoPermissions] = "You don't have permissions!",
				[BalanceTitle] = "{0} RP",
				[PagesFormat] = "{0} / {1}",
				[ErrorTitle] = "XXX",
				[CaseItemsAdd] = "+",
				[BtnBack] = "◀",
				[BtnNext] = "▶",
				[CaseEditTitle] = "Creating/editing case",
				[ItemEditingTitle] = "Creating/editing item",
				[SaveEditCase] = "SAVE",
				[AddCaseTitle] = "Add Case",
				[EditCaseTitle] = "Edit Case",
				[EditBtn] = "✎",
				[ItemSearch] = "Item search",
				[GiveBonusCase] = "You got the case! Try your luck: <color=#4286f4>/cases</color>"
			}, this);
		}

		private string Msg(BasePlayer player, string key, params object[] obj)
		{
			return string.Format(lang.GetMessage(key, this, player.UserIDString), obj);
		}

		private void Reply(BasePlayer player, string key, params object[] obj)
		{
			SendReply(player, Msg(player, key, obj));
		}

		private void SendNotify(BasePlayer player, string key, int type, params object[] obj)
		{
			if (_config.UseNotify && (Notify != null || UINotify != null))
				Interface.Oxide.CallHook("SendNotify", player, type, Msg(player, key, obj));
			else
				Reply(player, key, obj);
		}

		#endregion
	}
}