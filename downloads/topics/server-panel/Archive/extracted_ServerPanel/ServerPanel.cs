using Enumerable = System.Linq.Enumerable;
// #define TESTING

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Oxide.Plugins.ServerPanelExtensionMethods;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

#if CARBON
using Carbon.Base;
using Carbon.Modules;
#endif

namespace Oxide.Plugins
{
	/*ПЛАГИН БЫЛ ПОФИКШЕН С ПОМОЩЬЮ ПРОГРАММЫ СКАЧАНОЙ С https://discord.gg/dNGbxafuJn */ [Info("ServerPanel", "https://discord.gg/dNGbxafuJn", "1.0.3")]
	public class ServerPanel : RustPlugin
	{
		#region Fields

		[PluginReference] private Plugin
			ImageLibrary = null,
			NoEscape = null,
			Notify = null,
			UINotify = null,
			KillRecords = null,
			Statistics = null;

		private static ServerPanel Instance;

#if CARBON
		private ImageDatabaseModule imageDatabase;
#endif

		private bool _enabledImageLibrary;

		private Dictionary<int, int> _categoriesByID = new();

		private Dictionary<string, int> _categoriesByCommand = new();

		private Dictionary<string, Func<BasePlayer, string>> _headerUpdateFields = new();

		private const string
			Perm_Edit = "serverpanel.edit",
			CmdMainConsole = "UI_ServerPanel",
			Layer = "UI.Server.Panel",
			LayerHeader = "UI.Server.Panel.Header",
			LayerContent = "UI.Server.Panel.Content",
			LayerCategories = "UI.Server.Panel.Categories",
			EditingLayerPageEditor = "UI.Server.Panel.Editor.Page",
			EditingLayerElementEditor = "UI.Server.Panel.Editor.Element",
			EditingLayerModal = "UI.Server.Panel.Editor.Modal",
			EditingLayerModalArrayView = EditingLayerModal + ".Content.View",
			EditingLayerModalAnchorSelector = "UI.Server.Panel.Editor.Modal.Anchor.Selector",
			EditingLayerModalColorSelector = "UI.Server.Panel.Editor.Modal.Color.Selector",
			EditingLayerModalTextEditor = "UI.Server.Panel.Editor.Modal.Text.Editor";

		private Dictionary<(float AnchorMinX, float AnchorMinY, float AnchorMaxX, float AnchorMaxY), string>
			rectToImage = new()
			{
				[(0, 1, 0, 1)] =
					"https://github.com/TheMevent/PluginsStorage/blob/main/Images/Anchors/top-left.png?raw=true",
				[(0.5f, 1, 0.5f, 1)] =
					"https://github.com/TheMevent/PluginsStorage/blob/main/Images/Anchors/top-center.png?raw=true",
				[(1, 1, 1, 1)] =
					"https://github.com/TheMevent/PluginsStorage/blob/main/Images/Anchors/top-right.png?raw=true",

				[(0, 0.5f, 0, 0.5f)] =
					"https://github.com/TheMevent/PluginsStorage/blob/main/Images/Anchors/middle-left.png?raw=true",
				[(0.5f, 0.5f, 0.5f, 0.5f)] =
					"https://github.com/TheMevent/PluginsStorage/blob/main/Images/Anchors/middle-center.png?raw=true",
				[(1, 0.5f, 1, 0.5f)] =
					"https://github.com/TheMevent/PluginsStorage/blob/main/Images/Anchors/middle-right.png?raw=true",

				[(0, 0, 0, 0)] =
					"https://github.com/TheMevent/PluginsStorage/blob/main/Images/Anchors/bottom-left.png?raw=true",
				[(0.5f, 0, 0.5f, 0)] =
					"https://github.com/TheMevent/PluginsStorage/blob/main/Images/Anchors/bottom-center.png?raw=true",
				[(1, 0, 1, 0)] =
					"https://github.com/TheMevent/PluginsStorage/blob/main/Images/Anchors/bottom-right.png?raw=true",

				[(0, 1, 1, 1)] =
					"https://github.com/TheMevent/PluginsStorage/blob/main/Images/Anchors/top-stretch.png?raw=true",
				[(0, 0, 0, 1)] =
					"https://github.com/TheMevent/PluginsStorage/blob/main/Images/Anchors/stretch-left.png?raw=true",
				[(0.5f, 0, 0.5f, 1)] =
					"https://github.com/TheMevent/PluginsStorage/blob/main/Images/Anchors/stretch-center.png?raw=true",
				[(1, 0, 1, 1)] =
					"https://github.com/TheMevent/PluginsStorage/blob/main/Images/Anchors/stretch-right.png?raw=true",

				[(0, 0, 1, 1)] =
					"https://github.com/TheMevent/PluginsStorage/blob/main/Images/Anchors/stretch-stretch.png?raw=true",
			};

		#endregion

		#region Config

		private static Configuration _config;

		private class Configuration
		{
			#region Fields

			[JsonProperty(PropertyName = "Work with Notify?")]
			public bool UseNotify = true;

			[JsonProperty(PropertyName = "Economy Header Fields",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<EconomyHeadField> EconomyFields = new()
			{
				EconomyHeadField.Create(true, EconomyEntry.CreateEconomics(), "{economy_economics}"),
				EconomyHeadField.Create(true, EconomyEntry.CreateServerRewards(), "{economy_server_rewards}"),
				EconomyHeadField.Create(true, EconomyEntry.CreateBankSystem(), "{economy_bank_system}"),
			};

			[JsonProperty(PropertyName = "Block Settings")]
			public BlockSettings Block = new()
			{
				BlockWhenBuildingBlock = false,
				BlockWhenRaidBlock = false,
				BlockWhenCombatBlock = false
			};

			[JsonProperty(PropertyName = "Auto-Open Settings")]
			public AutoOpenSettings AutoOpen = new()
			{
				ShowMenuEveryTime = true
			};

			#endregion Fields

			#region Classes

			public class AutoOpenSettings
			{
				[JsonProperty(PropertyName = "Show menu every time player connects to server?")]
				public bool ShowMenuEveryTime = true;
			}

			public class BlockSettings
			{
				[JsonProperty(PropertyName = "Block the opening during a building block?")]
				public bool BlockWhenBuildingBlock;

				[JsonProperty(PropertyName = "Block the opening during a raid block?")]
				public bool BlockWhenRaidBlock;

				[JsonProperty(PropertyName = "Block the opening during a combat block?")]
				public bool BlockWhenCombatBlock;
			}

			public class EconomyHeadField
			{
				#region Fields

				[JsonProperty(PropertyName = "Enabled")]
				public bool Enabled;

				[JsonProperty(PropertyName = "Economy Settings")]
				public EconomyEntry Economy = new();

				[JsonProperty(PropertyName = "Update key (MUST BE UNIQUE)")]
				public string UpdateKey;

				#endregion

				#region Constructors

				public static EconomyHeadField Create(bool enabled, EconomyEntry economy, string updateKey)
				{
					return new EconomyHeadField
					{
						Enabled = enabled,
						Economy = economy,
						UpdateKey = updateKey
					};
				}

				#endregion
			}

			public enum EconomyType
			{
				Plugin,
				Item
			}

			public class EconomyEntry
			{
				#region Fields

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

				[JsonProperty(PropertyName = "Lang Key (for Title)")]
				public string TitleLangKey;

				[JsonProperty(PropertyName = "Lang Key (for Balance)")]
				public string BalanceLangKey;

				#endregion Fields

				#region Public Methods

				#region Titles

				public string GetTitle(BasePlayer player)
				{
					return Instance.Msg(player, TitleLangKey);
				}

				public string GetBalanceTitle(BasePlayer player)
				{
					return Instance.Msg(player, BalanceLangKey, ShowBalance(player).ToString());
				}

				#endregion Titles

				#region Economy

				public double ShowBalance(BasePlayer player)
				{
					switch (Type)
					{
						case EconomyType.Plugin:
						{
							var plugin = Instance?.plugins?.Find(Plug);
							if (plugin == null)
								return 0;

							return Convert.ToDouble(plugin.Call(BalanceHook, player.UserIDString));
						}
						case EconomyType.Item:
						{
							return ItemCount(Enumerable.ToArray(Enumerable.Concat(player.inventory.containerMain?.itemList ?? Enumerable.Empty<Item>(), Enumerable.Concat(player.inventory.containerBelt?.itemList ?? Enumerable.Empty<Item>(), player.inventory.containerWear?.itemList ?? Enumerable.Empty<Item>()))), ShortName, Skin);
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
							var plugin = Instance?.plugins?.Find(Plug);
							if (plugin == null) return;

							switch (Plug)
							{
								case "BankSystem":
								case "ServerRewards":
								case "IQEconomic":
									plugin.Call(AddHook, player.UserIDString, (int) amount);
									break;
								default:
									plugin.Call(AddHook, player.UserIDString, amount);
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

							var plugin = Instance?.plugins.Find(Plug);
							if (plugin == null) return false;

							switch (Plug)
							{
								case "BankSystem":
								case "ServerRewards":
								case "IQEconomic":
									plugin.Call(RemoveHook, player.UserIDString, (int) amount);
									break;
								default:
									plugin.Call(RemoveHook, player.UserIDString, amount);
									break;
							}

							return true;
						}
						case EconomyType.Item:
						{
							var playerItems = Enumerable.Concat(player.inventory.containerMain?.itemList ?? Enumerable.Empty<Item>(), Enumerable.Concat(player.inventory.containerBelt?.itemList ?? Enumerable.Empty<Item>(), player.inventory.containerWear?.itemList ?? Enumerable.Empty<Item>()));
							var am = (int) amount;

							if (ItemCount(Enumerable.ToArray(playerItems), ShortName, Skin) < am) return false;

							Take(playerItems, ShortName, Skin, am);
							return true;
						}
						default:
							return false;
					}
				}

				#endregion Economy

				#endregion

				#region Private Methods

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

				private static int ItemCount(Item[] items, string shortname, ulong skin)
				{
					return Array.FindAll(items,
							item => item.info.shortname == shortname && !item.isBroken &&
							        (skin == 0 || item.skin == skin))
						.Sum(item => item.amount);
				}

				private static void Take(IEnumerable<Item> itemList, string shortname, ulong skinId, int amountToTake)
				{
					if (amountToTake == 0) return;

					var takenAmount = 0;

					var itemsToTake = Pool.GetList<Item>();

					foreach (var item in itemList)
					{
						if (item.info.shortname != shortname ||
						    (skinId != 0 && item.skin != skinId) || item.isBroken) continue;

						var remainingAmount = amountToTake - takenAmount;
						if (remainingAmount <= 0) break;

						if (item.amount > remainingAmount)
						{
							item.MarkDirty();
							item.amount -= remainingAmount;
							break;
						}

						if (item.amount <= remainingAmount)
						{
							takenAmount += item.amount;
							itemsToTake.Add(item);
						}

						if (takenAmount == amountToTake)
							break;
					}

					foreach (var itemToTake in itemsToTake)
						itemToTake.RemoveFromContainer();

					Pool.FreeList(ref itemsToTake);
				}

				#endregion Private Methods

				#region Constructors

				public static EconomyEntry CreateEconomics()
				{
					return new EconomyEntry
					{
						Type = EconomyType.Plugin,
						Plug = "Economics",
						TitleLangKey = "Economy.Economics.Title",
						BalanceLangKey = "Economy.Economics.Balance",
						AddHook = "Deposit",
						BalanceHook = "Balance",
						RemoveHook = "Withdraw",
						ShortName = string.Empty,
						DisplayName = string.Empty,
						Skin = 0,
					};
				}

				public static EconomyEntry CreateServerRewards()
				{
					return new EconomyEntry
					{
						Type = EconomyType.Plugin,
						Plug = "ServerRewards",
						TitleLangKey = "Economy.ServerRewards.Title",
						BalanceLangKey = "Economy.ServerRewards.Balance",
						AddHook = "AddPoints",
						BalanceHook = "CheckPoints",
						RemoveHook = "TakePoints",
						ShortName = string.Empty,
						DisplayName = string.Empty,
						Skin = 0,
					};
				}

				public static EconomyEntry CreateBankSystem()
				{
					return new EconomyEntry
					{
						Type = EconomyType.Plugin,
						Plug = "BankSystem",
						TitleLangKey = "Economy.BankSystem.Title",
						BalanceLangKey = "Economy.BankSystem.Balance",
						AddHook = "API_BankSystemDeposit",
						BalanceHook = "API_BankSystemBalance",
						RemoveHook = "API_BankSystemWithdraw",
						ShortName = string.Empty,
						DisplayName = string.Empty,
						Skin = 0,
					};
				}

				public static EconomyEntry CreateIQEconomic()
				{
					return new EconomyEntry
					{
						Type = EconomyType.Plugin,
						Plug = "IQEconomic",
						TitleLangKey = "Economy.IQEconomic.Title",
						BalanceLangKey = "Economy.IQEconomic.Balance",
						AddHook = "API_SET_BALANCE",
						BalanceHook = "API_GET_BALANCE",
						RemoveHook = "API_REMOVE_BALANCE",
						ShortName = string.Empty,
						DisplayName = string.Empty,
						Skin = 0,
					};
				}

				public static EconomyEntry CreateScrap()
				{
					return new EconomyEntry
					{
						Type = EconomyType.Item,
						Plug = string.Empty,
						TitleLangKey = "Economy.Scrap.Title",
						BalanceLangKey = "Economy.Scrap.Balance",
						AddHook = string.Empty,
						BalanceHook = string.Empty,
						RemoveHook = string.Empty,
						ShortName = "scrap",
						DisplayName = string.Empty,
						Skin = 0,
					};
				}

				#endregion Constructors
			}

			#endregion
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

		#endregion Config

		#region Data

		#region Data.General

		public void SaveData()
		{
			SaveCategoriesData();

			SaveTemplateData();

			SaveLocalizationData();

			SaveHeaderFieldsData();
		}

		private void LoadData()
		{
			LoadCategoriesData();

			LoadTemplateData();

			LoadLocalizationData();

			LoadHeaderFieldsData();
		}

		private void LoadDataFromFile<T>(ref T data, string filePath)
		{
			try
			{
				data = Interface.Oxide.DataFileSystem.ReadObject<T>(filePath);
			}
			catch (Exception e)
			{
				PrintError(e.ToString());
			}

			data ??= Activator.CreateInstance<T>();
		}

		private void SaveDataToFile<T>(T data, string filePath)
		{
			Interface.Oxide.DataFileSystem.WriteObject(filePath, data);
		}

		#endregion Data.General

		#region Data.Categories

		private static CategoriesData _categoriesData;

		private class CategoriesData
		{
			[JsonProperty(PropertyName = "Categories", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<MenuCategory> Categories = new();
		}

		private void LoadCategoriesData() => LoadDataFromFile(ref _categoriesData, $"{Name}/Categories");

		private void SaveCategoriesData() => SaveDataToFile(_categoriesData, $"{Name}/Categories");

		#endregion Data.Categories

		#region Data.Template

		private static TemplateData _templateData;

		private void SaveTemplateData() => SaveDataToFile(_templateData, $"{Name}/Template");

		private void LoadTemplateData() => LoadDataFromFile(ref _templateData, $"{Name}/Template");

		private class TemplateData
		{
			#region Fields

			[JsonProperty(PropertyName = "Use an expert mod?")]
			public bool UseExpertMod = false;

			[JsonProperty(PropertyName = "UI Settings")]
			public UISettings UI = default;

			#endregion

			#region Public Methods

			public void ShowEditPageButtonsUI(BasePlayer player, CuiElementContainer container, string parent,
				string cmdAddPage = "",
				string cmdRemovePage = "")
			{
				#region Add Page

				container.Add(new CuiButton
				{
					Button =
					{
						Color = "0 0.372549 0.7176471 1",
						Command = cmdAddPage
					},
					Text =
					{
						Text = "+ NEW PAGE", Font = "robotocondensed-regular.ttf", FontSize = 14,
						Align = TextAnchor.MiddleCenter, Color = "0.8862745 0.8588235 0.827451 1"
					},
					RectTransform = {AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "22 10", OffsetMax = "148 40"}
				}, parent);

				#endregion

				#region Remove Page

				container.Add(new CuiButton
				{
					Button =
					{
						Color = "0.8117647 0.2627451 0.1764706 1",
						Command = cmdRemovePage
					},
					Text =
					{
						Text = "DELETE PAGE", Font = "robotocondensed-regular.ttf", FontSize = 14,
						Align = TextAnchor.MiddleCenter, Color = "0.8862745 0.8588235 0.827451 1"
					},
					RectTransform = {AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "158 10", OffsetMax = "284 40"}
				}, parent);

				#endregion
			}

			public void ShowContentUI(BasePlayer player, CuiElementContainer container,
				string cmdPage = "",
				Action<string> callback = null)
			{
				if (!TryGetOpenedMenu(player.userID, out var openedMenu))
					return;

				var menuCategory = Instance.GetCategoryById(openedMenu.SelectedCategory);

				var page = openedMenu.PageIndex;
				var maxPages = menuCategory.Pages.Count;

				UI.Content.Background.Get(ref container, player, Layer, LayerContent,
					destroy: LayerContent);

				var categoryPage = menuCategory?.Pages[page];
				if (categoryPage != null)
				{
					switch (categoryPage.Type)
					{
						case CategoryPage.PageType.Plugin:
						{
							var elements =
								categoryPage.Plugin?.Call<CuiElementContainer>(categoryPage.PluginHook, player);
							if (elements != null)
							{
								container.AddRange(elements);
							}

							break;
						}

						case CategoryPage.PageType.UI:
						{
							categoryPage.Elements?.ForEach(element =>
								element?.Get(ref container, player, LayerContent, element.Name));

							if (menuCategory.ShowPages)
								UI.Content.Pagination.ShowPagination(player, container, LayerContent, page,
									maxPages,
									LayerContent + ".Navigation", cmdPage);

							if (CanPlayerEdit(player))
							{
								UI.Content.EditButton.ShowEditButtonUI(player, container, LayerContent,
									cmdEdit: $"{CmdMainConsole} edit_page start {menuCategory.ID} {page}");

								ShowEditPageButtonsUI(player, container, LayerContent,
									cmdAddPage: $"{CmdMainConsole} edit_page add {menuCategory.ID} {page}",
									cmdRemovePage: $"{CmdMainConsole} edit_page remove {menuCategory.ID} {page}");
							}

							break;
						}
					}
				}

				callback?.Invoke(LayerContent);
			}

			public void ShowCloseButtonUI(BasePlayer player, CuiElementContainer container,
				string parent,
				string closeLayer = "",
				string command = "")
			{
				UI.CloseButton.ShowButtonUI(player, container, parent, parent + ".CloseButton", closeLayer: closeLayer,
					command: command);
			}

			public void ShowBackgroundUI(BasePlayer player, CuiElementContainer container,
				string cmdOnClick = "")
			{
				UI.Background.Background.Get(ref container, player, UI.Background.ParentLayer, Layer, destroy: Layer);

				if (UI.Background.CloseAfterClick)
				{
					container.Add(new CuiElement()
					{
						Parent = Layer,
						Components =
						{
							new CuiButtonComponent()
							{
								Color = "0 0 0 0",
								Close = Layer,
								Command = cmdOnClick
							},
							new CuiRectTransformComponent()
						}
					});
				}
			}

			public void ShowHeaderUI(BasePlayer player, CuiElementContainer container)
			{
				UI.Header.Background.Get(ref container, player, Layer, LayerHeader,
					destroy: LayerHeader);

				foreach (var headerField in _headerFieldsData.Fields)
					headerField.Get(ref container, player, LayerHeader,
						headerField.Name,
						destroy: headerField.Name,
						textFormatter: text => Instance.FormatUpdateField(player, text));
			}

			public void UpdateGlobalHeaderUI(BasePlayer player, CuiElementContainer container)
			{
				foreach (var headerField in _headerFieldsData.Fields)
					headerField.Get(ref container, player, LayerHeader,
						headerField.Name,
						textFormatter: text => Instance.FormatUpdateField(player, text),
						needUpdate: true);
			}

			public void UpdateHeaderUI(BasePlayer player, CuiElementContainer container)
			{
				foreach (var headerField in _headerFieldsData.elementsToUpdate)
					headerField.Get(ref container, player, LayerHeader,
						headerField.Name,
						destroy: headerField.Name,
						textFormatter: text => Instance.FormatUpdateField(player, text));
			}

			public void ShowUpdateHeaderUI(BasePlayer player)
			{
				UpdateUI(player, container => { UpdateHeaderUI(player, container); });
			}

			public void ShowCategories(BasePlayer player, CuiElementContainer container)
			{
				UI.Categories.Background.Get(ref container, player, Layer, LayerCategories,
					destroy: LayerCategories);

				container.Add(new CuiElement()
				{
					Name = Layer + ".Scroll.Panel",
					DestroyUi = Layer + ".Scroll.Panel",
					Parent = LayerCategories,
					Components =
					{
						new CuiImageComponent()
						{
							Color = "0 0 0 0"
						},
						UI.Categories.CategoriesScroll.GetRectTransform()
					}
				});
				
				ShowCategoriesScrollUI(player, container);
			}

			public void ShowCategoriesScrollUI(BasePlayer player, CuiElementContainer container, bool needUpdate = false)
			{
				var targetCategoriesCount = _categoriesData.Categories.Count;

				if (CanPlayerEdit(player))
					targetCategoriesCount += 2;

				var totalWidth = targetCategoriesCount * UI.Categories.CategoryWidth +
				                 (targetCategoriesCount - 1) * UI.Categories.CategoriesMargin;

				if (UI.Categories.UseScrolling)
				{
					container.Add(new CuiElement
					{
						Name = Layer + ".Scroll.View",
						DestroyUi = Layer + ".Scroll.View",
						Parent = Layer + ".Scroll.Panel",
						Update = needUpdate,
						Components =
						{
							UI.Categories.CategoriesScroll.GetScrollView(CalculateContentRectTransform(totalWidth)),
						}
					});
				}
				else
				{
					container.Add(new CuiElement
					{
						Name = Layer + ".Scroll.View",
						DestroyUi = Layer + ".Scroll.View",
						Parent = Layer + ".Scroll.Panel",
						Update = needUpdate,
						Components =
						{
							new CuiImageComponent()
							{
								Color = "0 0 0 0"
							},
							new CuiRectTransformComponent()
						}
					});
				}

				ShowCategoriesLoopUI(player, container);
			}

			public void ShowCategoriesLoopUI(BasePlayer player,
				CuiElementContainer container)
			{
				if (!TryGetOpenedMenu(player.userID, out var openedMenu))
					return;

				var selectedCategory = openedMenu.SelectedCategory;

				var mainOffset = UI.Categories.CategoriesIndent;

				foreach (var menuCategory in _categoriesData.Categories)
				{
					var menuCategoryTitle = menuCategory.Title;
					var isSelected = selectedCategory == menuCategory.ID;

					var categoryButton = Layer + ".Scroll.View" + $".Category.{menuCategory.ID}";

					container.Add(new CuiElement()
					{
						Name = categoryButton,
						DestroyUi = categoryButton,
						Parent = Layer + ".Scroll.View",
						Components =
						{
							GetCategoriesButtonComponent(isSelected, menuCategory),
							CalculateCategoriesPosition(mainOffset, UI.Categories.CategoryWidth,
								UI.Categories.CategoryHeight)
						}
					});

					UI.Categories.CategoryTitle.Get(player, container, categoryButton, text: menuCategoryTitle,
						isSelected: isSelected);

					if (UI.Categories.ShowSelectedElement && isSelected)
						UI.Categories.SelectedElement.Get(ref container, player, categoryButton);

					if (UI.Categories.CategoryTitle.UseIcon && !string.IsNullOrEmpty(menuCategory.Icon))
						UI.Categories.CategoryTitle.Icon.Get(ref container, player, categoryButton,
							textFormatter: menuIcon => menuCategory.Icon);

					if (UI.Categories.CategoryTitle.UseOutline)
						(isSelected ? UI.Categories.CategoryTitle.SelectedOutline : UI.Categories.CategoryTitle.Outline)
							.ShowOutlineUI(player, container, categoryButton);

					if (openedMenu.isEditMode)
						UI.Categories.CategoryEditPanel.GetCategoriesEditPanel(player,
							ref container,
							categoryButton,
							categoryButton + ".Panel.Settings",
							$"{CmdMainConsole} edit_menu category up {menuCategory.ID}",
							$"{CmdMainConsole} edit_menu category down {menuCategory.ID}",
							$"{CmdMainConsole} edit_category start {menuCategory.ID}");

					CategoriesLoopPosition(ref mainOffset);
				}

				if (CanPlayerEdit(player))
				{
					container.Add(new CuiElement()
					{
						Name = Layer + ".Scroll.View.Category.Create",
						DestroyUi = Layer + ".Scroll.View.Category.Create",
						Parent = Layer + ".Scroll.View",
						Components =
						{
							new CuiImageComponent() {Color = "0 0 0 0"},
							CalculateCategoriesPosition(mainOffset, UI.Categories.CategoryWidth,
								UI.Categories.CategoryHeight)
						}
					});

					UI.Categories.AdminCategory.AdminCheckbox.GetCheckbox(player, container,
						Layer + ".Scroll.View.Category.Create",
						Layer + ".Scroll.View.Category.Create.CheckBox.AdminMode",
						$"{CmdMainConsole} edit_menu change_mode",
						openedMenu.isEditMode);

					UI.Categories.AdminCategory.ButtonAddCategory.Get(ref container,
						player, Layer + ".Scroll.View.Category.Create",
						cmdFormatter: text => $"{CmdMainConsole} edit_category create");

					CategoriesLoopPosition(ref mainOffset);

					container.Add(new CuiElement()
					{
						Name = Layer + ".Scroll.View.Category.Admin.Settings",
						DestroyUi = Layer + ".Scroll.View.Category.Admin.Settings",
						Parent = Layer + ".Scroll.View",
						Components =
						{
							new CuiButtonComponent()
							{
								Command = $"{CmdMainConsole} edit_header_fields start",
								Color = "0 0 0 0"
							},
							CalculateCategoriesPosition(mainOffset, UI.Categories.CategoryWidth,
								UI.Categories.CategoryHeight)
						}
					});

					UI.Categories.AdminCategory.ButtonAdminSettings.Get(ref container,
						player, Layer + ".Scroll.View.Category.Admin.Settings",
						cmdFormatter: text => $"{CmdMainConsole} edit_header_fields start");
				}
			}

			private void CategoriesLoopPosition(ref float mainOffset)
			{
				if (UI.Categories.CategoriesScroll.ScrollType == ScrollType.Horizontal)
					mainOffset += UI.Categories.CategoriesMargin + UI.Categories.CategoryWidth;
				else
					mainOffset = mainOffset - UI.Categories.CategoriesMargin - UI.Categories.CategoryHeight;
			}

			private CuiButtonComponent GetCategoriesButtonComponent(bool isSelected, MenuCategory menuCategory)
			{
				var btnComponent = new CuiButtonComponent
				{
					Color = isSelected
						? UI.Categories.CategoryTitle.SelectedBackgroundColor.Get()
						: UI.Categories.CategoryTitle.BackgroundColor.Get(),
					Command = $"{CmdMainConsole} menu category {menuCategory.ID}",
				};

				if (!string.IsNullOrEmpty(UI.Categories.CategoryTitle.BackgroundSprite))
					btnComponent.Sprite = UI.Categories.CategoryTitle.BackgroundSprite;

				if (!string.IsNullOrEmpty(UI.Categories.CategoryTitle.BackgroundMaterial))
					btnComponent.Material = UI.Categories.CategoryTitle.BackgroundMaterial;
				return btnComponent;
			}

			private CuiRectTransform CalculateContentRectTransform(float totalWidth)
			{
				CuiRectTransform contentRect;
				if (UI.Categories.CategoriesScroll.ScrollType == ScrollType.Horizontal)
				{
					contentRect = new CuiRectTransform()
					{
						AnchorMin = "0 0", AnchorMax = "0 1",
						OffsetMin = "0 0",
						OffsetMax = $"{totalWidth} 0"
					};
				}
				else
				{
					contentRect = new CuiRectTransform()
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = $"0 -{totalWidth}",
						OffsetMax = "0 0"
					};
				}

				return contentRect;
			}

			private CuiRectTransformComponent CalculateCategoriesPosition(float offsetVal, float categoryWidth,
				float categoryHeight)
			{
				CuiRectTransformComponent cuiRect;
				if (UI.Categories.CategoriesScroll.ScrollType == ScrollType.Horizontal)
				{
					cuiRect = new CuiRectTransformComponent()
					{
						AnchorMin = "0 1", AnchorMax = "0 1",
						OffsetMin = $"{offsetVal} -{categoryHeight}",
						OffsetMax = $"{offsetVal + categoryWidth} 0"
					};
				}
				else
				{
					cuiRect = new CuiRectTransformComponent()
					{
						AnchorMin = "0 1", AnchorMax = "0 1",
						OffsetMin = $"0 {offsetVal - categoryHeight}",
						OffsetMax = $"{categoryWidth} {offsetVal}"
					};
				}

				return cuiRect;
			}

			#endregion
		}

		#endregion Data.Template

		#region Data.Localization

		private static LocalizationData _localizationData;

		private void SaveLocalizationData() => SaveDataToFile(_localizationData, $"{Name}/Localization");

		private void LoadLocalizationData() => LoadDataFromFile(ref _localizationData, $"{Name}/Localization");

		private class LocalizationData
		{
			[JsonProperty(PropertyName = "Localization Settings")]
			public LocalizationSettings Localization = new();
		}

		#endregion Data.Localization

		#region Data.Header

		private static HeaderFieldsData _headerFieldsData;

		private void SaveHeaderFieldsData() => SaveDataToFile(_headerFieldsData, $"{Name}/HeaderFields");

		private void LoadHeaderFieldsData()
		{
			LoadDataFromFile(ref _headerFieldsData, $"{Name}/HeaderFields");

			LoadHeaderFieldsDataCache();
		}

		private void LoadHeaderFieldsDataCache()
		{
			_headerFieldsData?.Load();
		}

		private class HeaderFieldsData
		{
			#region Fields

			[JsonProperty(PropertyName = "Fields", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<HeaderFieldUI> Fields = new();

			#endregion

			#region Cache

			[JsonIgnore] public bool needToUpdate;

			[JsonIgnore] public List<HeaderFieldUI> elementsToUpdate;

			public void Load()
			{
				elementsToUpdate?.Clear();

				elementsToUpdate = Fields.FindAll(x => x.NeedToUpdate);
				if (elementsToUpdate.Count > 0)
					needToUpdate = true;
			}

			#endregion
		}

		public class HeaderFieldUI : UiElement
		{
			#region Fields

			[JsonProperty(PropertyName = "Need to update?")]
			public bool NeedToUpdate;

			#endregion

			#region Constructors

			public HeaderFieldUI()
			{
			}

			public HeaderFieldUI(UiElement other) : base(other)
			{
				NeedToUpdate = false;
			}

			public HeaderFieldUI(UiElement other, bool needToUpdate) : base(other)
			{
				NeedToUpdate = needToUpdate;
			}

			#endregion
		}

		#endregion Data.Localization

		#region Classes

		#region Categories

		public class MenuCategory
		{
			#region Fields

			[JsonProperty(PropertyName = "ID")] public int ID;

			[JsonProperty(PropertyName = "Enabled")]
			public bool Enabled;

			[JsonProperty(PropertyName = "Permission")]
			public string Permission = string.Empty;

			[JsonProperty(PropertyName = "Title")] public string Title = string.Empty;

			[JsonProperty(PropertyName = "Chat Button")]
			public bool ChatBtn;

			[JsonProperty(PropertyName = "Show Pages?")]
			public bool ShowPages;

			[JsonProperty(PropertyName = "Commands", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public string[] Commands;

			[JsonProperty(PropertyName = "Icon")] public string Icon = string.Empty;

			[JsonProperty(PropertyName = "Pages", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<CategoryPage> Pages = new();

			#endregion

			#region Public Methods

			public void MoveUp()
			{
				var index = _categoriesData.Categories.IndexOf(this);
				if (index > 0 && index < _categoriesData.Categories.Count)
				{
					(_categoriesData.Categories[index], _categoriesData.Categories[index - 1]) = (
						_categoriesData.Categories[index - 1],
						_categoriesData.Categories[index]); // Swap
				}
			}

			public void MoveDown()
			{
				var index = _categoriesData.Categories.IndexOf(this);
				if (index >= 0 && index < _categoriesData.Categories.Count - 1)
					(_categoriesData.Categories[index], _categoriesData.Categories[index + 1]) = (
						_categoriesData.Categories[index + 1],
						_categoriesData.Categories[index]); // Swap
			}

			#endregion
		}

		public class MenuCategoryBuilder
		{
			private bool _enabled;
			private string _permission = string.Empty;
			private string _title = string.Empty;
			private bool _chatButton;
			private bool _showPages = true;
			private List<string> _commands = new();
			private List<CategoryPage> _pages;

			public MenuCategory Build()
			{
				var menuCategory = new MenuCategory
				{
					ID = Instance.GetUniqueCategoryID(),
					Enabled = _enabled,
					Permission = _permission,
					Title = _title,
					ChatBtn = _chatButton,
					ShowPages = _showPages,
					Commands = _commands?.ToArray(),
					Pages = _pages
				};

				return menuCategory;
			}

			public MenuCategoryBuilder WithEnabled(bool enabled)
			{
				_enabled = enabled;
				return this;
			}

			public MenuCategoryBuilder WithTitle(string title)
			{
				_title = title;
				return this;
			}

			public MenuCategoryBuilder WithPermission(string permission)
			{
				_permission = permission;
				return this;
			}

			public MenuCategoryBuilder WithChatButton(bool chatButton)
			{
				_chatButton = chatButton;
				return this;
			}

			public MenuCategoryBuilder WithShowPages(bool showPages)
			{
				_showPages = showPages;
				return this;
			}

			public MenuCategoryBuilder WithCommand(string command)
			{
				_commands.Add(command);
				return this;
			}

			public MenuCategoryBuilder WithPages(List<CategoryPage> pages)
			{
				_pages = pages;
				return this;
			}
		}

		public class CategoryPage
		{
			#region Fields

			[JsonProperty(PropertyName = "Title")] public string Title;

			[JsonProperty(PropertyName = "Command")]
			public string Command;

			[JsonProperty(PropertyName = "Type (Plugin/UI)")] [JsonConverter(typeof(StringEnumConverter))]
			public PageType Type;

			[JsonProperty(PropertyName = "Plugin Name")]
			public string PluginName;

			[JsonProperty(PropertyName = "Plugin Hook")]
			public string PluginHook;

			[JsonProperty(PropertyName = "UI Elements", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<UiElement> Elements = new();

			#endregion

			#region Cache

			[JsonIgnore] public Plugin Plugin => Instance?.plugins.Find(PluginName);

			#endregion

			#region Classes

			public enum PageType
			{
				Plugin,
				UI
			}

			#endregion
		}

		#endregion Categories

		#region UI

		public class UISettings
		{
			#region Fields

			[JsonProperty(PropertyName = "ID (DONT CHANGE)")]
			public string ID;

			[JsonProperty(PropertyName = "Background")]
			public BackgroundUI Background = new();

			[JsonProperty(PropertyName = "Content")]
			public ContentUI Content = new();

			[JsonProperty(PropertyName = "Header")]
			public HeaderUI Header = new();

			[JsonProperty(PropertyName = "Categories")]
			public CategoriesUI Categories = new();

			[JsonProperty(PropertyName = "Close Button")]
			public CloseButtonUI CloseButton = new();

			#endregion

			#region Classes

			public class OutlineUI
			{
				#region Fields

				[JsonProperty(PropertyName = "Color")] public IColor Color = IColor.CreateTransparent();

				[JsonProperty(PropertyName = "Size")] public float Size = 0f;

				[JsonProperty(PropertyName = "Sprite")]
				public string Sprite = string.Empty;

				[JsonProperty(PropertyName = "Material")]
				public string Material = string.Empty;

				#endregion

				#region Public Methods

				public void ShowOutlineUI(BasePlayer player, CuiElementContainer container,
					string outlineParent,
					string name = "")
				{
					if (string.IsNullOrEmpty(name)) name = CuiHelper.GetGuid();

					var imageComponent = new CuiImageComponent
					{
						Color = Color.Get()
					};

					if (!string.IsNullOrWhiteSpace(Sprite))
						imageComponent.Sprite = Sprite;

					if (!string.IsNullOrWhiteSpace(Material))
						imageComponent.Material = Material;

					container.Add(new CuiElement()
					{
						Parent = outlineParent,
						Name = name + ".1",
						DestroyUi = name + ".1",
						Components =
						{
							imageComponent,
							new CuiRectTransformComponent
								{AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"0 -{Size}", OffsetMax = "0 0"}
						}
					});

					container.Add(new CuiElement()
					{
						Parent = outlineParent,
						Name = name + ".2",
						DestroyUi = name + ".2",
						Components =
						{
							imageComponent,
							new CuiRectTransformComponent
								{AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 0", OffsetMax = $"0 {Size}"}
						}
					});

					container.Add(new CuiElement()
					{
						Parent = outlineParent,
						Name = name + ".3",
						DestroyUi = name + ".3",
						Components =
						{
							imageComponent,
							new CuiRectTransformComponent
							{
								AnchorMin = "0 0", AnchorMax = "0 1", OffsetMin = $"0 {Size}",
								OffsetMax = $"{Size} -{Size}"
							}
						}
					});

					container.Add(new CuiElement()
					{
						Parent = outlineParent,
						Name = name + ".4",
						DestroyUi = name + ".4",
						Components =
						{
							imageComponent,
							new CuiRectTransformComponent
							{
								AnchorMin = "1 0", AnchorMax = "1 1", OffsetMin = $"-{Size} {Size}",
								OffsetMax = $"0 -{Size}"
							}
						}
					});
				}

				#endregion
			}

			public class CloseButtonUI
			{
				#region Fields

				[JsonProperty(PropertyName = "Background")]
				public UiElement Background = new();

				[JsonProperty(PropertyName = "Title")] public UiElement Title = new();

				#endregion

				#region Public Methods

				public void ShowButtonUI(BasePlayer player, CuiElementContainer container,
					string parent,
					string name = "",
					string closeLayer = "",
					string command = "")
				{
					if (string.IsNullOrEmpty(name))
						name = CuiHelper.GetGuid();

					Background.Get(ref container, player, parent, name, name);

					Title.Get(ref container, player, name, name + ".Title");

					container.Add(new CuiElement()
					{
						Parent = name,
						Components =
						{
							new CuiButtonComponent()
							{
								Color = "0 0 0 0",
								Command = command,
								Close = closeLayer
							}
						}
					});
				}

				#endregion
			}

			public class EditButtonUI
			{
				#region Fields

				[JsonProperty(PropertyName = "Background")]
				public UiElement Background = new();

				[JsonProperty(PropertyName = "Title")] public UiElement Title = new();

				[JsonProperty(PropertyName = "Description Background")]
				public UiElement DescriptionBackground = new();

				[JsonProperty(PropertyName = "Description Title")]
				public UiElement DescriptionTitle = new();

				#endregion

				#region Public Methods

				public void ShowEditButtonUI(BasePlayer player, CuiElementContainer container,
					string parent,
					string name = "",
					string closeLayer = "",
					string cmdEdit = "")
				{
					if (string.IsNullOrEmpty(name))
						name = CuiHelper.GetGuid();

					Background.Get(ref container, player, parent, name);
					Title.Get(ref container, player, name);
					DescriptionBackground.Get(ref container, player, name, name + ".Description");
					DescriptionTitle.Get(ref container, player, name + ".Description");

					container.Add(new CuiElement()
					{
						Parent = name,
						Components =
						{
							new CuiButtonComponent()
							{
								Color = "0 0 0 0",
								Command = cmdEdit,
								Close = closeLayer
							}
						}
					});
				}

				#endregion
			}

			public class ContentUI
			{
				[JsonProperty(PropertyName = "Background")]
				public UiElement Background = new();

				[JsonProperty(PropertyName = "Pagination")]
				public PaginationUI Pagination = new();

				[JsonProperty(PropertyName = "Edit Button")]
				public EditButtonUI EditButton = new();
			}

			public class PaginationUI
			{
				#region Fields

				[JsonProperty(PropertyName = "Type")] [JsonConverter(typeof(StringEnumConverter))]
				public PaginationType Type = PaginationType.Text;

				[JsonProperty(PropertyName = "Text Pagination Settings")]
				public TextPaginationUI TextPagination = new();

				[JsonProperty(PropertyName = "Multiple Buttons Settings")]
				public MultipleButtonsPagination MultipleButtons = new();

				#endregion

				#region Public Methods

				public void ShowPagination(BasePlayer player, CuiElementContainer container,
					string parent,
					int page,
					int maxPages,
					string name = "",
					string cmdPage = "")
				{
					switch (Type)
					{
						case PaginationType.Text:
							ShowTextPaginationUI(player, container, parent, page, maxPages, name, cmdPage);
							break;

						case PaginationType.MultipleButtons:
							CreateMultipleButtonsPaginationUI(player, container, parent, page, maxPages, name, cmdPage);
							break;

						default:
							throw new ArgumentOutOfRangeException();
					}
				}

				#endregion

				#region Private Methods

				private void ShowTextPaginationUI(BasePlayer player, CuiElementContainer container,
					string parent,
					int page,
					int maxPages,
					string name = "",
					string cmdPage = "")
				{
					if (string.IsNullOrEmpty(name)) name = CuiHelper.GetGuid();

					TextPagination.TextLabel.Get(ref container, player, parent, name, name,
						textFormatter: paginationText => paginationText.Replace("{page}",
							(page + 1).ToString()).Replace("{maxPages}", maxPages.ToString()));

					TextPagination.ButtonBack.ShowButtonUI(player, container, name,
						command: $"{cmdPage} {Mathf.Max(page - 1, 0)}");

					TextPagination.ButtonNext.ShowButtonUI(player, container, name,
						command: $"{cmdPage} {Mathf.Min(page + 1, maxPages - 1)}");
				}

				private void CreateMultipleButtonsPaginationUI(BasePlayer player, CuiElementContainer container,
					string parent,
					int page,
					int maxPages,
					string name = "",
					string cmdPage = "")
				{
					if (string.IsNullOrEmpty(name)) name = CuiHelper.GetGuid();

					container.Add(new CuiElement()
					{
						Parent = parent,
						Name = name,
						DestroyUi = name,
						Components =
						{
							new CuiImageComponent() {Color = "0 0 0 0"},
							MultipleButtons.GetRectTransform()
						}
					});

					var totalWidth = maxPages * MultipleButtons.PageTitle.Width +
					                 (maxPages - 1) * MultipleButtons.Margin;

					string offsetMin, offsetMax;
					switch (MultipleButtons.Type)
					{
						case MultipleButtonsPagination.SortingType.Left:
							offsetMin = $"{-totalWidth} 0";
							offsetMax = "0 0";
							break;
						case MultipleButtonsPagination.SortingType.Center:
							var halfOfWidth = (float) Math.Round(totalWidth / 2f, 2);

							offsetMin = $"-{halfOfWidth} 0";
							offsetMax = $"{halfOfWidth} 0";
							break;
						case MultipleButtonsPagination.SortingType.Right:
							offsetMin = $"0 0";
							offsetMax = $"{totalWidth} 0";
							break;
						default:
							throw new ArgumentOutOfRangeException();
					}

					var pagesBackground = container.Add(new CuiPanel()
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = offsetMin, OffsetMax = offsetMax
						},
						Image =
						{
							Color = "0 0 0 0"
						}
					}, name);

					MultipleButtons.ButtonBack.ShowButtonUI(player, container, pagesBackground,
						command: $"{cmdPage} {Mathf.Max(page - 1, 0)}");

					MultipleButtons.ButtonNext.ShowButtonUI(player, container, pagesBackground,
						command: $"{cmdPage} {Mathf.Min(page + 1, maxPages - 1)}");

					var offsetX = 0f;
					for (var targetPage = 1; targetPage <= maxPages; targetPage++)
					{
						container.Add(new CuiPanel()
							{
								RectTransform =
								{
									AnchorMin = "0 1", AnchorMax = "0 1",
									OffsetMin = $"{offsetX} 0",
									OffsetMax =
										$"{offsetX + MultipleButtons.PageTitle.Width} {MultipleButtons.PageTitle.Height}",
								},
								Image =
								{
									Color = MultipleButtons.PageTitle.BackgroundColor.Get()
								}
							}, pagesBackground, name + $".Page.{targetPage}", name + $".Page.{targetPage}");

						MultipleButtons.PageTitle.Title.Get(ref container, player, name + $".Page.{targetPage}",
							textFormatter: paginationText => paginationText.Replace("{page}", targetPage.ToString()));

						#region Selected

						if ((targetPage - 1) == page)
							MultipleButtons.SelectedLine.Get(ref container, player, name + $".Page.{targetPage}");

						#endregion

						container.Add(new CuiElement()
						{
							Parent = name + $".Page.{targetPage}",
							Components =
							{
								new CuiButtonComponent()
								{
									Color = "0 0 0 0",
									Command = $"{cmdPage} {targetPage - 1}"
								},
								new CuiRectTransformComponent()
							}
						});

						offsetX += MultipleButtons.PageTitle.Width + MultipleButtons.Margin;
					}
				}

				#endregion

				#region Classes

				public class TextPaginationUI
				{
					[JsonProperty(PropertyName = "Button Back")]
					public CloseButtonUI ButtonBack = new();

					[JsonProperty(PropertyName = "Button Next")]
					public CloseButtonUI ButtonNext = new();

					[JsonProperty(PropertyName = "Label Settings")]
					public UiElement TextLabel = new();
				}

				public class MultipleButtonsPagination : InterfacePosition
				{
					#region Fields

					[JsonProperty(PropertyName = "Margin")]
					public float Margin;

					[JsonProperty(PropertyName = "Sorting Type")]
					public SortingType Type;

					[JsonProperty(PropertyName = "Page Title")]
					public PageButton PageTitle = new();

					[JsonProperty(PropertyName = "Selected Line")]
					public UiElement SelectedLine = new();

					[JsonProperty(PropertyName = "Button Back")]
					public CloseButtonUI ButtonBack = new();

					[JsonProperty(PropertyName = "Button Next")]
					public CloseButtonUI ButtonNext = new();

					#endregion

					#region Classes

					public enum SortingType
					{
						Left,
						Center,
						Right
					}

					public class PageButton
					{
						[JsonProperty(PropertyName = "Width")] public float Width;

						[JsonProperty(PropertyName = "Height")]
						public float Height;

						[JsonProperty(PropertyName = "Background Color")]
						public IColor BackgroundColor;

						[JsonProperty(PropertyName = "Title")] public UiElement Title = new();
					}

					#endregion
				}

				public enum PaginationType
				{
					Text,
					MultipleButtons
				}

				#endregion
			}

			public class BackgroundUI
			{
				#region Fields

				[JsonProperty(PropertyName = "Parent (Overlay/Hud)")]
				public string ParentLayer = "Overlay";

				[JsonProperty(PropertyName = "Background")]
				public UiElement Background = new();

				[JsonProperty(PropertyName = "Close after click?")]
				public bool CloseAfterClick;

				#endregion
			}

			public class HeaderUI
			{
				#region Fields

				[JsonProperty(PropertyName = "Background")]
				public UiElement Background = new();

				#endregion
			}

			public class CategoriesUI
			{
				#region Fields

				[JsonProperty(PropertyName = "Background")]
				public UiElement Background = new();

				[JsonProperty(PropertyName = "Use scrolling?")]
				public bool UseScrolling;

				[JsonProperty(PropertyName = "Categories Scroll")]
				public ScrollUIElement CategoriesScroll = new();

				[JsonProperty(PropertyName = "Categories Indent")]
				public float CategoriesIndent;

				[JsonProperty(PropertyName = "Category Width")]
				public float CategoryWidth;

				[JsonProperty(PropertyName = "Category Height")]
				public float CategoryHeight;

				[JsonProperty(PropertyName = "Categories Margin")]
				public float CategoriesMargin;

				[JsonProperty(PropertyName = "Show selected element?")]
				public bool ShowSelectedElement;

				[JsonProperty(PropertyName = "Selected Element")]
				public UiElement SelectedElement = new();

				[JsonProperty(PropertyName = "Category Title")]
				public CategoryTitleUI CategoryTitle = new();

				[JsonProperty(PropertyName = "Category Edit Panel")]
				public CategoryEditPanelUI CategoryEditPanel = new();

				[JsonProperty(PropertyName = "Admin Category")]
				public CategoriesAdminCategoryUI AdminCategory = new();

				#endregion
			}

			public class CategoriesAdminCategoryUI
			{
				[JsonProperty(PropertyName = "Admin Mode Checkbox")]
				public CheckboxElement AdminCheckbox = new();

				[JsonProperty(PropertyName = "Add Category Button")]
				public UiElement ButtonAddCategory = new();

				[JsonProperty(PropertyName = "Admin Settings Button")]
				public UiElement ButtonAdminSettings = new();
			}

			public class CategoryEditPanelUI
			{
				#region Fields

				[JsonProperty(PropertyName = "Background")]
				public UiElement Background = new();

				[JsonProperty(PropertyName = "Up Button")]
				public UiElement ButtonUp = new();

				[JsonProperty(PropertyName = "Down Button")]
				public UiElement ButtonDown = new();

				[JsonProperty(PropertyName = "Edit Button")]
				public UiElement ButtonEdit = new();

				#endregion

				#region Public Methods

				public void GetCategoriesEditPanel(BasePlayer player, ref CuiElementContainer container,
					string parent,
					string name = "",
					string cmdUp = "",
					string cmdDown = "",
					string cmdEdit = "")
				{
					Background?.Get(ref container, player, parent, name);

					ButtonUp?.Get(ref container, player, name, name + ".UpButton", cmdFormatter: cmd => cmdUp);
					ButtonDown?.Get(ref container, player, name, name + ".DownButton", cmdFormatter: cmd => cmdDown);
					ButtonEdit?.Get(ref container, player, name, name + ".EditButton", cmdFormatter: cmd => cmdEdit);
				}

				#endregion
			}

			public class CategoryTitleUI : InterfacePosition
			{
				#region Fields

				[JsonProperty(PropertyName = "Enabled")]
				public bool Enabled;

				[JsonProperty(PropertyName = "Font Size")]
				public int FontSize;

				[JsonProperty(PropertyName = "Font")] public string Font;

				[JsonProperty(PropertyName = "Align")] [JsonConverter(typeof(StringEnumConverter))]
				public TextAnchor Align;

				[JsonProperty(PropertyName = "Text Color")]
				public IColor TextColor = IColor.CreateTransparent();

				[JsonProperty(PropertyName = "Selected Text Color")]
				public IColor SelectedTextColor = IColor.CreateTransparent();

				[JsonProperty(PropertyName = "Background Color")]
				public IColor BackgroundColor = IColor.CreateTransparent();

				[JsonProperty(PropertyName = "Selected Background Color")]
				public IColor SelectedBackgroundColor = IColor.CreateTransparent();

				[JsonProperty(PropertyName = "Background Sprite")]
				public string BackgroundSprite = string.Empty;

				[JsonProperty(PropertyName = "Background Material")]
				public string BackgroundMaterial = string.Empty;

				[JsonProperty(PropertyName = "Show icon?")]
				public bool UseIcon;

				[JsonProperty(PropertyName = "Icon")] public UiElement Icon = new();

				[JsonProperty(PropertyName = "Show outline?")]
				public bool UseOutline;

				[JsonProperty(PropertyName = "Selected Outline")]
				public OutlineUI SelectedOutline = new();

				[JsonProperty(PropertyName = "Outline")]
				public OutlineUI Outline = new();

				#endregion

				#region Public Methods

				public CuiTextComponent GetText(string text, bool isSelected)
				{
					return new CuiTextComponent()
					{
						Text = text,
						Font = Font ?? "robotocondensed-bold.ttf",
						FontSize = FontSize,
						Align = Align,
						Color = isSelected ? SelectedTextColor.Get() : TextColor.Get()
					};
				}

				public void Get(BasePlayer player, CuiElementContainer container, string parent, string name = "",
					string text = "", bool isSelected = false)
				{
					if (string.IsNullOrEmpty(name)) name = CuiHelper.GetGuid();

					container.Add(new CuiElement()
					{
						Name = name,
						Parent = parent,
						Components =
						{
							GetText(text, isSelected),
							GetRectTransform()
						}
					});
				}

				#endregion
			}

			#endregion

			#region Public Methods

			public List<UiElement> GetAllUiElements()
			{
				var allUiElements = new List<UiElement>();

				GetAllUiElementsRecursive(this, ref allUiElements);

				return allUiElements;
			}

			#endregion

			#region Private Methods

			private void GetAllUiElementsRecursive(object obj, ref List<UiElement> allUiElements)
			{
				if (obj is UiElement uiElement)
				{
					allUiElements.Add(uiElement);
				}

				if (obj is UISettings uiSettings)
				{
					GetAllUiElementsRecursive(uiSettings.Background, ref allUiElements);
					GetAllUiElementsRecursive(uiSettings.Content, ref allUiElements);
					GetAllUiElementsRecursive(uiSettings.Header, ref allUiElements);
					GetAllUiElementsRecursive(uiSettings.Categories, ref allUiElements);
					GetAllUiElementsRecursive(uiSettings.CloseButton, ref allUiElements);
				}

				if (obj is BackgroundUI backgroundUI)
				{
					GetAllUiElementsRecursive(backgroundUI.Background, ref allUiElements);
				}

				if (obj is HeaderUI headerUI)
				{
					GetAllUiElementsRecursive(headerUI.Background, ref allUiElements);

					/*
					foreach (var headerUIField in headerUI.Fields)
						GetAllUiElementsRecursive(headerUIField, ref allUiElements);*/
				}

				if (obj is ContentUI contentUI)
				{
					GetAllUiElementsRecursive(contentUI.Background, ref allUiElements);
					GetAllUiElementsRecursive(contentUI.Pagination, ref allUiElements);
					GetAllUiElementsRecursive(contentUI.EditButton, ref allUiElements);
				}

				if (obj is PaginationUI paginationUI)
				{
					GetAllUiElementsRecursive(paginationUI.TextPagination, ref allUiElements);
					GetAllUiElementsRecursive(paginationUI.MultipleButtons, ref allUiElements);
				}

				if (obj is CloseButtonUI closeButtonUI)
				{
					GetAllUiElementsRecursive(closeButtonUI.Background, ref allUiElements);
					GetAllUiElementsRecursive(closeButtonUI.Title, ref allUiElements);
				}

				if (obj is PaginationUI.MultipleButtonsPagination multipleButtonsPagination)
				{
					GetAllUiElementsRecursive(multipleButtonsPagination.PageTitle.Title, ref allUiElements);
					GetAllUiElementsRecursive(multipleButtonsPagination.SelectedLine, ref allUiElements);
					GetAllUiElementsRecursive(multipleButtonsPagination.ButtonBack, ref allUiElements);
					GetAllUiElementsRecursive(multipleButtonsPagination.ButtonNext, ref allUiElements);
				}

				if (obj is PaginationUI.TextPaginationUI textPagination)
				{
					GetAllUiElementsRecursive(textPagination.ButtonBack, ref allUiElements);
					GetAllUiElementsRecursive(textPagination.ButtonNext, ref allUiElements);
					GetAllUiElementsRecursive(textPagination.TextLabel, ref allUiElements);
				}

				if (obj is EditButtonUI editButton)
				{
					GetAllUiElementsRecursive(editButton.Background, ref allUiElements);
					GetAllUiElementsRecursive(editButton.Title, ref allUiElements);
					GetAllUiElementsRecursive(editButton.DescriptionBackground, ref allUiElements);
					GetAllUiElementsRecursive(editButton.DescriptionTitle, ref allUiElements);
				}
			}

			#endregion
		}

		public class InterfacePosition
		{
			#region Fields

			[JsonProperty(PropertyName = "AnchorMin (X)")]
			public float AnchorMinX;

			[JsonProperty(PropertyName = "AnchorMin (Y)")]
			public float AnchorMinY;

			[JsonProperty(PropertyName = "AnchorMax (X)")]
			public float AnchorMaxX;

			[JsonProperty(PropertyName = "AnchorMax (Y)")]
			public float AnchorMaxY;

			[JsonProperty(PropertyName = "OffsetMin (X)")]
			public float OffsetMinX;

			[JsonProperty(PropertyName = "OffsetMin (Y)")]
			public float OffsetMinY;

			[JsonProperty(PropertyName = "OffsetMax (X)")]
			public float OffsetMaxX;

			[JsonProperty(PropertyName = "OffsetMax (Y)")]
			public float OffsetMaxY;

			#endregion Fields

			#region Public Methods

			public string GetAnchorImage()
			{
				if (Instance?.rectToImage.TryGetValue(
					    new ValueTuple<float, float, float, float>(AnchorMinX, AnchorMinY, AnchorMaxX, AnchorMaxY),
					    out var image) == true)
					return image;

				return string.Empty;
			}

			public CuiRectTransformComponent GetRectTransform()
			{
				return new CuiRectTransformComponent
				{
					AnchorMin = $"{AnchorMinX} {AnchorMinY}",
					AnchorMax = $"{AnchorMaxX} {AnchorMaxY}",
					OffsetMin = $"{OffsetMinX} {OffsetMinY}",
					OffsetMax = $"{OffsetMaxX} {OffsetMaxY}"
				};
			}

			public CuiRectTransformComponent GetRectTransform(Func<float, float> formatterOffMaxX,
				Func<float, float> formatterOffMaxY)
			{
				var oMaxX = OffsetMaxX;
				if (formatterOffMaxX != null) oMaxX = formatterOffMaxX(OffsetMaxX);

				var oMaxY = OffsetMaxY;
				if (formatterOffMaxY != null) oMaxY = formatterOffMaxY(OffsetMaxY);

				return new CuiRectTransformComponent
				{
					AnchorMin = $"{AnchorMinX} {AnchorMinY}",
					AnchorMax = $"{AnchorMaxX} {AnchorMaxY}",
					OffsetMin = $"{OffsetMinX} {OffsetMinY}",
					OffsetMax = $"{oMaxX} {oMaxY}"
				};
			}

			public override string ToString()
			{
				return JsonConvert.SerializeObject(GetRectTransform(), 0, new JsonSerializerSettings()
				{
					DefaultValueHandling = DefaultValueHandling.Ignore
				}).Replace("\\n", "\n");
			}

			#endregion

			#region Constructors

			public static InterfacePosition CreatePosition(float aMinX, float aMinY, float aMaxX, float aMaxY,
				float oMinX, float oMinY, float oMaxX, float oMaxY)
			{
				return new InterfacePosition
				{
					AnchorMinX = aMinX,
					AnchorMinY = aMinY,
					AnchorMaxX = aMaxX,
					AnchorMaxY = aMaxY,
					OffsetMinX = oMinX,
					OffsetMinY = oMinY,
					OffsetMaxX = oMaxX,
					OffsetMaxY = oMaxY
				};
			}

			public static InterfacePosition CreatePosition(
				string anchorMin = "0 0",
				string anchorMax = "1 1",
				string offsetMin = "0 0",
				string offsetMax = "0 0")
			{
				var aMinX = float.Parse(anchorMin.Split(' ')[0]);
				var aMinY = float.Parse(anchorMin.Split(' ')[1]);
				var aMaxX = float.Parse(anchorMax.Split(' ')[0]);
				var aMaxY = float.Parse(anchorMax.Split(' ')[1]);
				var oMinX = float.Parse(offsetMin.Split(' ')[0]);
				var oMinY = float.Parse(offsetMin.Split(' ')[1]);
				var oMaxX = float.Parse(offsetMax.Split(' ')[0]);
				var oMaxY = float.Parse(offsetMax.Split(' ')[1]);

				return new InterfacePosition
				{
					AnchorMinX = aMinX,
					AnchorMinY = aMinY,
					AnchorMaxX = aMaxX,
					AnchorMaxY = aMaxY,
					OffsetMinX = oMinX,
					OffsetMinY = oMinY,
					OffsetMaxX = oMaxX,
					OffsetMaxY = oMaxY
				};
			}

			public static InterfacePosition CreatePosition(CuiRectTransform rectTransform)
			{
				var aMinX = float.Parse(rectTransform.AnchorMin.Split(' ')[0]);
				var aMinY = float.Parse(rectTransform.AnchorMin.Split(' ')[1]);
				var aMaxX = float.Parse(rectTransform.AnchorMax.Split(' ')[0]);
				var aMaxY = float.Parse(rectTransform.AnchorMax.Split(' ')[1]);
				var oMinX = float.Parse(rectTransform.OffsetMin.Split(' ')[0]);
				var oMinY = float.Parse(rectTransform.OffsetMin.Split(' ')[1]);
				var oMaxX = float.Parse(rectTransform.OffsetMax.Split(' ')[0]);
				var oMaxY = float.Parse(rectTransform.OffsetMax.Split(' ')[1]);

				return new InterfacePosition
				{
					AnchorMinX = aMinX,
					AnchorMinY = aMinY,
					AnchorMaxX = aMaxX,
					AnchorMaxY = aMaxY,
					OffsetMinX = oMinX,
					OffsetMinY = oMinY,
					OffsetMaxX = oMaxX,
					OffsetMaxY = oMaxY
				};
			}

			#endregion Constructors
		}

		public enum CuiElementType
		{
			Label,
			Panel,
			Button,
			Image,
			InputField
		}

		public enum ScrollType
		{
			Horizontal,
			Vertical
		}

		public class UiElement : InterfacePosition
		{
			#region Fields

			[JsonProperty(PropertyName = "Enabled?")]
			public bool Enabled;

			[JsonProperty(PropertyName = "Name")] public string Name = string.Empty;

			[JsonProperty(PropertyName = "Type (Label/Panel/Button/Image)")]
			[JsonConverter(typeof(StringEnumConverter))]
			public CuiElementType Type;

			[JsonProperty(PropertyName = "Color")] public IColor Color = new("#FFFFFF", 100);

			[JsonProperty(PropertyName = "Text", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<string> Text = new();

			[JsonProperty(PropertyName = "Font Size")]
			public int FontSize;

			[JsonProperty(PropertyName = "Font")] public CuiElementFont Font = CuiElementFont.RobotoCondensedBold;

			[JsonProperty(PropertyName = "Align")] [JsonConverter(typeof(StringEnumConverter))]
			public TextAnchor Align;

			[JsonProperty(PropertyName = "Text Color")]
			public IColor TextColor = new("#FFFFFF", 100);

			[JsonProperty(PropertyName = "Command ({user} - user steamid)")]
			public string Command = string.Empty;

			[JsonProperty(PropertyName = "Image")] public string Image = string.Empty;

			[JsonProperty(PropertyName = "Cursor Enabled")]
			public bool CursorEnabled;

			[JsonProperty(PropertyName = "Keyboard Enabled")]
			public bool KeyboardEnabled;

			[JsonProperty(PropertyName = "Sprite")]
			public string Sprite = string.Empty;

			[JsonProperty(PropertyName = "Material")]
			public string Material = string.Empty;

			#endregion Fields

			#region Public Methods

			public bool TryGetImage(out string image)
			{
				if (Type == CuiElementType.Image)
				{
					if (!string.IsNullOrEmpty(Image))
					{
						if (Image.IsURL())
						{
							image = Image;
							return true;
						}
					}
				}

				image = null;
				return false;
			}

			public void Get(ref CuiElementContainer container, BasePlayer player,
				string parent,
				string name = null,
				string destroy = "",
				string close = "",
				Func<string, string> textFormatter = null,
				Func<string, string> cmdFormatter = null,
				bool needUpdate = false)
			{
				if (!Enabled) return;

				if (string.IsNullOrEmpty(name))
					name = CuiHelper.GetGuid();

				switch (Type)
				{
					case CuiElementType.Label:
					{
						var targetText = GetLocalizedText(player);

						var text = string.Join("\n", targetText);

						if (textFormatter != null)
							text = textFormatter(text);

						container.Add(new CuiElement
						{
							Name = name,
							Parent = parent,
							DestroyUi = destroy,
							Update = needUpdate,
							Components =
							{
								new CuiTextComponent
								{
									Text = text,
									Align = Align,
									Font = GetFontByType(Font),
									FontSize = FontSize,
									Color = TextColor.Get()
								},
								GetRectTransform()
							}
						});
						break;
					}

					case CuiElementType.InputField:
					{
						var targetText = GetLocalizedText(player);

						var text = $"{string.Join("\n", targetText)}";

						if (textFormatter != null)
							text = textFormatter(text);

						container.Add(new CuiElement
						{
							Name = name,
							Parent = parent,
							DestroyUi = destroy,
							Update = needUpdate,
							Components =
							{
								new CuiInputFieldComponent
								{
									Text = text,
									Align = Align,
									Font = GetFontByType(Font),
									FontSize = FontSize,
									Color = TextColor.Get(),
									HudMenuInput = true,
									ReadOnly = true
								},
								GetRectTransform()
							}
						});
						break;
					}

					case CuiElementType.Panel:
					{
						var imageElement = new CuiImageComponent
						{
							Color = Color.Get()
						};

						if (!string.IsNullOrEmpty(Sprite)) imageElement.Sprite = Sprite;
						if (!string.IsNullOrEmpty(Material)) imageElement.Material = Material;

						var cuiElement = new CuiElement
						{
							Name = name,
							Parent = parent,
							DestroyUi = destroy,
							Update = needUpdate,
							Components =
							{
								imageElement,
								GetRectTransform()
							}
						};

						if (CursorEnabled)
							cuiElement.Components.Add(new CuiNeedsCursorComponent());

						if (KeyboardEnabled)
							cuiElement.Components.Add(new CuiNeedsKeyboardComponent());

						container.Add(cuiElement);
						break;
					}

					case CuiElementType.Button:
					{
						var targetCommand = $"{Command}".Replace("{user}", player.UserIDString);

						if (cmdFormatter != null)
							targetCommand = cmdFormatter(targetCommand);

						var btnElement = new CuiButtonComponent()
						{
							Command = targetCommand,
							Color = Color.Get(),
							Close = close
						};

						if (!string.IsNullOrEmpty(Sprite)) btnElement.Sprite = Sprite;
						if (!string.IsNullOrEmpty(Material)) btnElement.Material = Material;

						container.Add(new CuiElement
						{
							Name = name,
							Parent = parent,
							DestroyUi = destroy,
							Update = needUpdate,
							Components =
							{
								btnElement,
								GetRectTransform()
							}
						});

						var targetText = GetLocalizedText(player);
						var message = $"{string.Join("\n", targetText)}";

						if (textFormatter != null)
							message = textFormatter(message);

						if (!string.IsNullOrEmpty(message))
						{
							container.Add(new CuiElement
							{
								Parent = name,
								Components =
								{
									new CuiTextComponent()
									{
										Text = message,
										Align = Align,
										Font = GetFontByType(Font),
										FontSize = FontSize,
										Color = TextColor.Get()
									},
									new CuiRectTransformComponent()
								}
							});
						}

						break;
					}

					case CuiElementType.Image:
					{
						if (string.IsNullOrEmpty(Image)) return;

						ICuiComponent imageElement;
						if (Image.StartsWith("assets/"))
						{
							if (Image.Contains("Linear"))
							{
								imageElement = new CuiRawImageComponent
								{
									Color = Color.Get(),
									Sprite = Image
								};
							}
							else
							{
								imageElement = new CuiImageComponent
								{
									Color = Color.Get(),
									Sprite = Image
								};
							}
						}
						else if (Image.IsURL())
						{
							imageElement = new CuiRawImageComponent
							{
								Png = Instance?.GetImage(Image),
								Color = Color.Get()
							};
						}
						else
						{
							var image = Image;
							if (textFormatter != null)
								image = textFormatter(image);

							imageElement = new CuiRawImageComponent
							{
								Png = Instance?.GetImage(image),
								Color = Color.Get()
							};
						}

						container.Add(new CuiElement
						{
							Name = name,
							Parent = parent,
							DestroyUi = destroy,
							Update = needUpdate,
							Components =
							{
								imageElement,
								GetRectTransform()
							}
						});
						break;
					}
				}
			}

			private List<string> GetLocalizedText(BasePlayer player)
			{
				List<string> targetText;

				var playerLang = Instance?.lang?.GetLanguage(player.UserIDString);
				if (!string.IsNullOrWhiteSpace(playerLang) &&
				    _localizationData.Localization.Elements.TryGetValue(Name, out var elementLocalization) &&
				    elementLocalization.Messages.TryGetValue(playerLang, out var textLocalization))
				{
					targetText = textLocalization.Text;
				}
				else
				{
					targetText = Text;
				}

				return targetText;
			}

			private static string GenerateElementGUID(CuiElementType elementType)
			{
				return $"{elementType}_{CuiHelper.GetGuid().Substring(0, 10)}";
			}

			#endregion Public Methods

			#region Constructors

			public UiElement()
			{
			}

			public UiElement(UiElement other)
			{
				AnchorMinX = other.AnchorMinX;
				AnchorMinY = other.AnchorMinY;
				AnchorMaxX = other.AnchorMaxX;
				AnchorMaxY = other.AnchorMaxY;
				OffsetMinX = other.OffsetMinX;
				OffsetMinY = other.OffsetMinY;
				OffsetMaxX = other.OffsetMaxX;
				OffsetMaxY = other.OffsetMaxY;
				Enabled = other.Enabled;
				Name = other.Name;
				Type = other.Type;
				Color = other.Color;
				Text = other.Text;
				FontSize = other.FontSize;
				Font = other.Font;
				Align = other.Align;
				TextColor = other.TextColor;
				Command = other.Command;
				Image = other.Image;
				CursorEnabled = other.CursorEnabled;
				KeyboardEnabled = other.KeyboardEnabled;
			}

			public static UiElement CreatePanel(
				InterfacePosition position,
				IColor color,
				bool cursorEnabled = false,
				bool keyboardEnabled = false,
				string sprite = "",
				string material = "",
				string randomName = "")
			{
				if (string.IsNullOrWhiteSpace(randomName)) randomName = GenerateElementGUID(CuiElementType.Panel);

				return new UiElement
				{
					Name = randomName,
					AnchorMinX = position.AnchorMinX,
					AnchorMinY = position.AnchorMinY,
					AnchorMaxX = position.AnchorMaxX,
					AnchorMaxY = position.AnchorMaxY,
					OffsetMinX = position.OffsetMinX,
					OffsetMinY = position.OffsetMinY,
					OffsetMaxX = position.OffsetMaxX,
					OffsetMaxY = position.OffsetMaxY,
					Enabled = true,
					Type = CuiElementType.Panel,
					Color = color,
					Text = new List<string>(),
					FontSize = 14,
					Font = CuiElementFont.RobotoCondensedBold,
					Align = TextAnchor.UpperLeft,
					TextColor = new IColor("#FFFFFF", 100),
					Command = string.Empty,
					Image = string.Empty,
					CursorEnabled = cursorEnabled,
					KeyboardEnabled = keyboardEnabled,
					Sprite = sprite,
					Material = material
				};
			}

			public static UiElement CreateImage(
				InterfacePosition position,
				string image,
				IColor color = null,
				string randomName = "")
			{
				if (string.IsNullOrWhiteSpace(randomName)) randomName = GenerateElementGUID(CuiElementType.Image);

				color ??= new IColor("#FFFFFF", 100);

				return new UiElement
				{
					Name = randomName,
					AnchorMinX = position.AnchorMinX,
					AnchorMinY = position.AnchorMinY,
					AnchorMaxX = position.AnchorMaxX,
					AnchorMaxY = position.AnchorMaxY,
					OffsetMinX = position.OffsetMinX,
					OffsetMinY = position.OffsetMinY,
					OffsetMaxX = position.OffsetMaxX,
					OffsetMaxY = position.OffsetMaxY,
					Enabled = true,
					Type = CuiElementType.Image,
					Color = color,
					Text = new List<string>(),
					FontSize = 14,
					Font = CuiElementFont.RobotoCondensedBold,
					Align = TextAnchor.UpperLeft,
					TextColor = new IColor("#FFFFFF", 100),
					Command = string.Empty,
					Image = image
				};
			}

			public static UiElement CreateLabel(
				InterfacePosition position,
				IColor textColor,
				List<string> text,
				int fontSize = 14,
				string font = "robotocondensed-bold.ttf",
				TextAnchor align = TextAnchor.UpperLeft,
				string randomName = "")
			{
				if (string.IsNullOrWhiteSpace(randomName)) randomName = GenerateElementGUID(CuiElementType.Label);

				return new UiElement
				{
					Name = randomName,
					AnchorMinX = position.AnchorMinX,
					AnchorMinY = position.AnchorMinY,
					AnchorMaxX = position.AnchorMaxX,
					AnchorMaxY = position.AnchorMaxY,
					OffsetMinX = position.OffsetMinX,
					OffsetMinY = position.OffsetMinY,
					OffsetMaxX = position.OffsetMaxX,
					OffsetMaxY = position.OffsetMaxY,
					Enabled = true,
					Type = CuiElementType.Label,
					Color = new IColor("#FFFFFF", 100),
					Text = text,
					FontSize = fontSize,
					Font = GetFontTypeByFont(font),
					Align = align,
					TextColor = textColor,
					Command = string.Empty,
					Image = string.Empty
				};
			}

			public static UiElement CreateLabel(
				InterfacePosition position,
				IColor textColor,
				string text,
				int fontSize = 14,
				string font = "robotocondensed-bold.ttf",
				TextAnchor align = TextAnchor.UpperLeft,
				string randomName = "")
			{
				if (string.IsNullOrWhiteSpace(randomName)) randomName = GenerateElementGUID(CuiElementType.Label);

				return new UiElement
				{
					Name = randomName,
					AnchorMinX = position.AnchorMinX,
					AnchorMinY = position.AnchorMinY,
					AnchorMaxX = position.AnchorMaxX,
					AnchorMaxY = position.AnchorMaxY,
					OffsetMinX = position.OffsetMinX,
					OffsetMinY = position.OffsetMinY,
					OffsetMaxX = position.OffsetMaxX,
					OffsetMaxY = position.OffsetMaxY,
					Enabled = true,
					Type = CuiElementType.Label,
					Color = new IColor("#FFFFFF", 100),
					Text = new List<string> {text},
					FontSize = fontSize,
					Font = GetFontTypeByFont(font),
					Align = align,
					TextColor = textColor,
					Command = string.Empty,
					Image = string.Empty
				};
			}

			public static UiElement CreateButton(
				InterfacePosition position,
				IColor color,
				IColor textColor,
				string text = "",
				bool cursorEnabled = false,
				bool keyboardEnabled = false,
				string sprite = "",
				string material = "",
				int fontSize = 14,
				string font = "robotocondensed-bold.ttf",
				TextAnchor align = TextAnchor.UpperLeft,
				string command = "",
				string randomName = "")
			{
				if (string.IsNullOrWhiteSpace(randomName)) randomName = GenerateElementGUID(CuiElementType.Button);
				
				return new UiElement
				{
					Name = randomName,
					AnchorMinX = position.AnchorMinX,
					AnchorMinY = position.AnchorMinY,
					AnchorMaxX = position.AnchorMaxX,
					AnchorMaxY = position.AnchorMaxY,
					OffsetMinX = position.OffsetMinX,
					OffsetMinY = position.OffsetMinY,
					OffsetMaxX = position.OffsetMaxX,
					OffsetMaxY = position.OffsetMaxY,
					Enabled = true,
					Type = CuiElementType.Button,
					Color = color,
					Text = new List<string> {text},
					FontSize = fontSize,
					Font = GetFontTypeByFont(font),
					Align = align,
					TextColor = textColor,
					Command = command ?? string.Empty,
					Image = string.Empty,
					CursorEnabled = cursorEnabled,
					KeyboardEnabled = keyboardEnabled,
					Sprite = sprite,
					Material = material
				};
			}

			public static UiElement CreateInputField(
				InterfacePosition position,
				IColor textColor,
				string text,
				int fontSize = 14,
				string font = "robotocondensed-bold.ttf",
				TextAnchor align = TextAnchor.UpperLeft,
				string randomName = "")
			{
				if (string.IsNullOrWhiteSpace(randomName)) randomName = GenerateElementGUID(CuiElementType.InputField);

				return new UiElement
				{
					Name = randomName,
					AnchorMinX = position.AnchorMinX,
					AnchorMinY = position.AnchorMinY,
					AnchorMaxX = position.AnchorMaxX,
					AnchorMaxY = position.AnchorMaxY,
					OffsetMinX = position.OffsetMinX,
					OffsetMinY = position.OffsetMinY,
					OffsetMaxX = position.OffsetMaxX,
					OffsetMaxY = position.OffsetMaxY,
					Enabled = true,
					Type = CuiElementType.InputField,
					Color = new IColor("#FFFFFF", 100),
					Text = new List<string> {text},
					FontSize = fontSize,
					Font = GetFontTypeByFont(font),
					Align = align,
					TextColor = textColor,
					Command = string.Empty,
					Image = string.Empty
				};
			}

			#endregion Constructors
		}

		public class ScrollUIElement : InterfacePosition
		{
			#region Fields

			[JsonProperty(PropertyName = "Scroll Type")] [JsonConverter(typeof(StringEnumConverter))]
			public ScrollType ScrollType;

			[JsonProperty(PropertyName = "Movement Type")] [JsonConverter(typeof(StringEnumConverter))]
			public ScrollRect.MovementType MovementType;

			[JsonProperty(PropertyName = "Elasticity")]
			public float Elasticity;

			[JsonProperty(PropertyName = "Deceleration Rate")]
			public float DecelerationRate;

			[JsonProperty(PropertyName = "Scroll Sensitivity")]
			public float ScrollSensitivity;

			[JsonProperty(PropertyName = "Scrollbar Settings")]
			public ScrollBarSettings Scrollbar = new();

			#endregion

			#region Public Methods

			public CuiScrollViewComponent GetScrollView(CuiRectTransform contentTransform)
			{
				var cuiScrollView = new CuiScrollViewComponent
				{
					MovementType = MovementType,
					Elasticity = Elasticity,
					DecelerationRate = DecelerationRate,
					ScrollSensitivity = ScrollSensitivity,
					ContentTransform = contentTransform,
					Inertia = true
				};

				switch (ScrollType)
				{
					case ScrollType.Vertical:
					{
						cuiScrollView.Vertical = true;
						cuiScrollView.Horizontal = false;

						cuiScrollView.VerticalScrollbar = Scrollbar.Get();
						break;
					}

					case ScrollType.Horizontal:
					{
						cuiScrollView.Horizontal = true;
						cuiScrollView.Vertical = false;

						cuiScrollView.HorizontalScrollbar = Scrollbar.Get();
						break;
					}
				}

				return cuiScrollView;
			}

			#endregion

			#region Classes

			public class ScrollBarSettings
			{
				#region Fields

				[JsonProperty(PropertyName = "Invert")]
				public bool Invert;

				[JsonProperty(PropertyName = "Auto Hide")]
				public bool AutoHide;

				[JsonProperty(PropertyName = "Handle Sprite")]
				public string HandleSprite;

				[JsonProperty(PropertyName = "Size")] public float Size;

				[JsonProperty(PropertyName = "Handle Color")]
				public IColor HandleColor;

				[JsonProperty(PropertyName = "Highlight Color")]
				public IColor HighlightColor;

				[JsonProperty(PropertyName = "Pressed Color")]
				public IColor PressedColor;

				[JsonProperty(PropertyName = "Track Sprite")]
				public string TrackSprite;

				[JsonProperty(PropertyName = "Track Color")]
				public IColor TrackColor;

				#endregion

				#region Public Methods

				public CuiScrollbar Get()
				{
					var cuiScrollbar = new CuiScrollbar()
					{
						Size = Size
					};

					if (Invert) cuiScrollbar.Invert = Invert;
					if (AutoHide) cuiScrollbar.AutoHide = AutoHide;
					if (!string.IsNullOrEmpty(HandleSprite)) cuiScrollbar.HandleSprite = HandleSprite;
					if (!string.IsNullOrEmpty(TrackSprite)) cuiScrollbar.TrackSprite = TrackSprite;

					if (HandleColor != null) cuiScrollbar.HandleColor = HandleColor.Get();
					if (HighlightColor != null) cuiScrollbar.HighlightColor = HighlightColor.Get();
					if (PressedColor != null) cuiScrollbar.PressedColor = PressedColor.Get();
					if (TrackColor != null) cuiScrollbar.TrackColor = TrackColor.Get();

					return cuiScrollbar;
				}

				#endregion
			}

			#endregion
		}

		public class IColor
		{
			#region Fields

			[JsonProperty(PropertyName = "HEX")] public string Hex;

			[JsonProperty(PropertyName = "Opacity (0 - 100)")]
			public float Alpha;

			#endregion

			#region Public Methods

			public string Get()
			{
				if (string.IsNullOrEmpty(Hex)) Hex = "#FFFFFF";

				var str = Hex.Trim('#');
				if (str.Length != 6) throw new Exception(Hex);
				var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
				var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
				var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

				return $"{(double) r / 255} {(double) g / 255} {(double) b / 255} {Alpha / 100}";
			}

			#endregion

			#region Constructors

			public IColor(string hex, float alpha)
			{
				Hex = hex;
				Alpha = alpha;
			}

			public static IColor Create(string hex, float alpha = 100)
			{
				return new IColor(hex, alpha);
			}

			public static IColor CreateTransparent()
			{
				return new IColor("#000000", 0);
			}

			public static IColor CreateWhite()
			{
				return new IColor("#FFFFFF", 100);
			}

			public static IColor CreateBlack()
			{
				return new IColor("#000000", 100);
			}

			#endregion
		}

		public class CheckboxElement
		{
			#region Fields

			[JsonProperty(PropertyName = "Checkbox")]
			public UiElement Checkbox;

			[JsonProperty(PropertyName = "Title")] public UiElement Title;

			#endregion

			#region Public Methods

			public void GetCheckbox(BasePlayer player,
				CuiElementContainer container,
				string parent,
				string name,
				string cmd,
				bool isChecked)
			{
				Checkbox?.Get(ref container, player, parent, name, name, cmdFormatter: text => cmd,
					textFormatter: text => isChecked ? text : string.Empty);

				Title?.Get(ref container, player, name);
			}

			#endregion
		}

		#region Font

		public enum CuiElementFont
		{
			RobotoCondensedBold,
			RobotoCondensedRegular,
			DroidSansMono,
			PermanentMarker,
		}

		public static string GetFontByType(CuiElementFont fontType)
		{
			switch (fontType)
			{
				case CuiElementFont.RobotoCondensedBold:
					return "robotocondensed-bold.ttf";
				case CuiElementFont.RobotoCondensedRegular:
					return "robotocondensed-regular.ttf";
				case CuiElementFont.DroidSansMono:
					return "droidsansmono.ttf";
				case CuiElementFont.PermanentMarker:
					return "permanentmarker.ttf";
				default:
					throw new ArgumentOutOfRangeException(nameof(fontType), fontType, null);
			}
		}

		public static CuiElementFont GetFontTypeByFont(string font)
		{
			switch (font)
			{
				case "robotocondensed-bold.ttf":
					return CuiElementFont.RobotoCondensedBold;
				case "robotocondensed-regular.ttf":
					return CuiElementFont.RobotoCondensedRegular;
				case "droidsansmono.ttf":
					return CuiElementFont.DroidSansMono;
				case "permanentmarker.ttf":
					return CuiElementFont.PermanentMarker;
				default:
					throw new ArgumentOutOfRangeException(nameof(font), font, null);
			}
		}

		#endregion

		#endregion

		#region Localization

		private class LocalizationSettings
		{
			#region Fields

			[JsonProperty(PropertyName = "UI Elements", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public Dictionary<string, ElementLocalization> Elements = new();

			#endregion

			#region Classes

			public class ElementLocalization
			{
				[JsonProperty(PropertyName = "Messages", ObjectCreationHandling = ObjectCreationHandling.Replace)]
				public Dictionary<string, LocalizationInfo> Messages = new();
			}

			public class LocalizationInfo
			{
				[JsonProperty(PropertyName = "Text", ObjectCreationHandling = ObjectCreationHandling.Replace)]
				public List<string> Text = new();
			}

			#endregion
		}

		#endregion

		#endregion

		#endregion Data

		#region Hooks

		private void Init()
		{
			Instance = this;

			if (!_config.AutoOpen.ShowMenuEveryTime)
				Unsubscribe(nameof(OnPlayerConnected));
		}

		private void OnServerInitialized()
		{
			LoadData();

			LoadCategories();

			LoadImages();

			RegisterCommands();

			RegisterPermissions();

			LoadUpdateFields();
		}

		private void Unload()
		{
			foreach (var player in BasePlayer.activePlayerList)
			{
				API_OnServerPanelDestroyUI(player);
			}

			_config = null;
			Instance = null;
			_templateData = null;
			_categoriesData = null;
			_headerFieldsData = null;
			_localizationData = null;
		}

		#region Player Hooks

		private void OnPlayerConnected(BasePlayer player)
		{
			if (player == null || !player.userID.IsSteamId()) return;

			var targetCategory = GetAvailableCategories(player.userID)?.FirstOrDefault();
			if (targetCategory == null) return;

			NextTick(() => { StartShowMenu(player, targetCategory); });
		}

		private void OnPlayerDisconnected(BasePlayer player)
		{
			API_OnServerPanelClosed(player);
		}

		#endregion

		#endregion Hooks

		#region Commands

		private void CmdOpenMenu(IPlayer covPlayer, string command, string[] args)
		{
			var player = covPlayer.Object as BasePlayer;
			if (player == null) return;

			if (_categoriesData?.Categories == null || _templateData?.UI == null)
			{
				if (covPlayer.IsAdmin)
				{
					SendReply(player, "Plugin is not initialized! Please, contact admin");
				}
				else
				{
					SendReply(player, "Plugin is not initialized! Please, contact admin");
				}

				return;
			}

			if (_enabledImageLibrary == false)
			{
				SendNotify(player, NoILError, 1);

				BroadcastILNotInstalled();
				return;
			}

			var category = GetCategoryByCommand(command);
			if (category == null)
			{
				Reply(player, MsgCantOpenMenuInvalidCommand);
				return;
			}

			if (_config.Block.BlockWhenBuildingBlock && player.IsBuildingBlocked())
			{
				Reply(player, MsgCantOpenMenuBuildingBlock);
				return;
			}

			if (_config.Block.BlockWhenRaidBlock && IsServerPanelPlayerRaidBlocked(player))
			{
				Reply(player, MsgCantOpenMenuRaidBlock);
				return;
			}

			if (_config.Block.BlockWhenCombatBlock && IsServerPanelPlayerCombatBlocked(player))
			{
				Reply(player, MsgCantOpenMenuCombatBlock);
				return;
			}

			StartShowMenu(player, category);
		}

		[ConsoleCommand(CmdMainConsole)]
		private void CmdServerPanel(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || !arg.HasArgs()) return;
			
			switch (arg.GetString(0))
			{
				case "close":
				{
					API_OnServerPanelClosed(player);
					break;
				}

				case "menu":
				{
					if (!TryGetOpenedMenu(player.userID, out var openedMenu))
						return;

					switch (arg.GetString(1))
					{
						case "category":
						{
							var nextCategory = arg.GetInt(2);

							var menuCategory = GetCategoryById(nextCategory);
							if (menuCategory == null) return;

							if (Interface.CallHook("OnServerPanelCategoryPage", player, nextCategory, 0) != null)
								return;

							openedMenu.OnSelectCategory(nextCategory);

							UpdateUI(player, container =>
							{
								_templateData?.ShowCategoriesLoopUI(player, container);

								ShowContent(player, container);

								ShowCloseButton(player, container);
							});
							break;
						}

						case "page":
						{
							var targetPage = arg.GetInt(2);

							if (Interface.CallHook("OnServerPanelCategoryPage", player, openedMenu.SelectedCategory,
								    targetPage) != null)
								return;

							openedMenu.OnSelectPage(targetPage);

							openedMenu.UpdateContent();
							break;
						}
					}

					break;
				}

				case "edit_page":
				{
					if (!CanPlayerEdit(player)) return;

					switch (arg.GetString(1))
					{
						case "start":
						{
							var categoryID = arg.GetInt(2);
							var pageID = arg.GetInt(3);

							EditPageData.Create(player, categoryID, pageID);

							ShowPageEditorPanel(player);
							break;
						}

						case "save":
						{
							var editData = EditPageData.Get(player.userID);

							editData?.Save();
							break;
						}

						case "add": // add page
						{
							if (!TryGetOpenedMenu(player.userID, out var openedMenu))
								return;

							var categoryID = arg.GetInt(2);
							var pageID = arg.GetInt(3);

							GetCategoryById(categoryID)?.Pages.Add(new CategoryPage
							{
								Title = string.Empty,
								Command = string.Empty,
								Type = CategoryPage.PageType.UI,
								PluginName = string.Empty,
								PluginHook = string.Empty,
								Elements = new List<UiElement>
								{
									UiElement.CreatePanel(
										InterfacePosition.CreatePosition("0.5 0.5", "0.5 0.5", "-100 -100", "100 100"),
										IColor.CreateBlack()
									),
									UiElement.CreateLabel(
										InterfacePosition.CreatePosition("0.5 0.5", "0.5 0.5", "-100 -100", "100 100"),
										IColor.Create("#E2DBD3"), "TEST ELEMENT",
										fontSize: 14, align: TextAnchor.MiddleCenter),
								}
							});

							openedMenu.OnSelectPage(openedMenu.GetLastPage());

							ShowMenuUI(player);
							break;
						}

						case "remove": // remove page
						{
							if (!TryGetOpenedMenu(player.userID, out var openedMenu))
								return;

							var categoryID = arg.GetInt(2);
							var targetCategory = GetCategoryById(categoryID);
							if (targetCategory == null) return;

							var pageID = arg.GetInt(3);
							if (pageID <= 0) return;

							targetCategory.Pages.RemoveAt(pageID);

							openedMenu.OnSelectPage(openedMenu.GetLastPage());

							ShowMenuUI(player);
							break;
						}

						case "element":
						{
							var editData = EditPageData.Get(player.userID);

							switch (arg.GetString(2))
							{
								case "edit":
								{
									var elementIndex = arg.GetInt(3);
									if (!editData.StartEditElement(elementIndex, LayerContent))
										return;

									EditUiElementData.Create(player,
										editData.elementIndex,
										editData.OnEditElementSave,
										editData.OnEditElementStartEdit,
										editData.OnEditElementStopEdit,
										editData.OnStartTextEditing,
										editData.OnStopTextEditing);

									ShowElementEditorPanel(player);
									break;
								}
								case "add":
								{
									editData.categoryPage.Elements.Add(UiElement.CreatePanel(
										InterfacePosition.CreatePosition(0.5f, 0.5f, 0.5f, 0.5f, -50, -50, 50, 50),
										new IColor("#FFFFFF", 100)));

									SaveData();

									if (TryGetOpenedMenu(player.userID, out var openedMenu))
										openedMenu.UpdateContent();

									ShowPageEditorPanel(player);
									break;
								}

								case "remove":
								{
									if (!arg.HasArgs(3)) return;

									editData.categoryPage.Elements.RemoveAt(arg.GetInt(3));

									SaveData();

									if (TryGetOpenedMenu(player.userID, out var openedMenu))
										openedMenu.UpdateContent();

									ShowPageEditorPanel(player);
									break;
								}

								case "move":
								{
									if (!arg.HasArgs(4)) return;

									var targetElement = editData.categoryPage.Elements[arg.GetInt(4)];

									switch (arg.GetString(3))
									{
										case "up":
										{
											editData.categoryPage.Elements.MoveUp(targetElement);
											break;
										}

										case "down":
										{
											editData.categoryPage.Elements.MoveDown(targetElement);
											break;
										}
									}

									SaveData();

									if (TryGetOpenedMenu(player.userID, out var openedMenu))
										openedMenu.UpdateContent();

									ShowPageEditorPanel(player);
									break;
								}
							}

							break;
						}
					}

					break;
				}

				case "edit_element":
				{
					if (!CanPlayerEdit(player)) return;

					switch (arg.GetString(1))
					{
						case "cancel":
						{
							var editPageData = EditUiElementData.Get(player.userID);
							editPageData.EndEditElement(true);
							break;
						}

						case "save":
						{
							var editPageData = EditUiElementData.Get(player.userID);

							UpdateUI(player, container =>
							{
								UpdateTitlePageEditorFieldUI(container, editPageData.elementIndex,
									editPageData.editingElement, true);
							});

							editPageData.EndEditElement();
							break;
						}

						case "field":
						{
							var editPageData = EditUiElementData.Get(player.userID);

							var fieldName = arg.GetString(2);

							var targetField = editPageData.editingElement.GetType().GetField(fieldName);
							if (targetField == null)
								return;

							var parent = arg.GetString(3);
							if (string.IsNullOrEmpty(parent)) return;

							if (targetField.FieldType.IsEnum)
							{
								if (targetField.GetValue(editPageData.editingElement) is not Enum nowEnum) return;

								Enum targetEnum = null;
								switch (arg.GetString(4))
								{
									case "prev":
									{
										targetEnum = nowEnum.Previous();
										break;
									}

									case "next":
									{
										targetEnum = nowEnum.Next();
										break;
									}
								}

								if (targetEnum == null) return;

								targetField.SetValue(editPageData.editingElement, targetEnum);
							}
							else if (targetField.FieldType == typeof(List<string>))
							{
								var val = string.Join(" ", arg.Args.Skip(4));
								if (!string.IsNullOrEmpty(val))
								{
									var text = new List<string>();
									foreach (var line in val.Split('\n'))
									{
										text.Add(line);
									}

									targetField.SetValue(editPageData.editingElement, text);
								}
							}
							else if (targetField.FieldType == typeof(string))
							{
								var val = string.Join(" ", arg.Args.Skip(4));
								if (!string.IsNullOrEmpty(val))
									targetField.SetValue(editPageData.editingElement, val);
							}
							else
							{
								var newValue = string.Join(" ", arg.Args.Skip(4));

								try
								{
									var convertedValue = Convert.ChangeType(newValue, targetField.FieldType);
									targetField.SetValue(editPageData.editingElement, convertedValue);
								}
								catch (Exception ex)
								{
									Puts($"Error setting property '{fieldName}': {ex.Message}");
									player.SendMessage($"Error setting property '{fieldName}': {ex.Message}");
									return;
								}
							}

							if (targetField.Name == nameof(UiElement.Type))
							{
								UpdateUI(player, container =>
								{
									if (editPageData.isTextEditing)
									{
										ShowTextEditorLinesUI(player, ref container);
									}
									else
									{
										editPageData.UpdateEditElement(ref container, player,
											needAddImage: targetField.Name == nameof(UiElement.Image));
									}
								});

								ShowElementEditorPanel(player);
							}
							else
								UpdateUI(player, container =>
								{
									if (editPageData.isTextEditing)
									{
										ShowTextEditorLinesUI(player, ref container);

										FieldElementUI(container, parent, targetField,
											targetField.GetValue(editPageData.editingElement));
									}
									else
									{
										editPageData.UpdateEditElement(ref container, player,
											needAddImage: targetField.Name == nameof(UiElement.Image));

										FieldElementUI(container, parent, targetField,
											targetField.GetValue(editPageData.editingElement));
									}
								});

							break;
						}

						case "color":
						{
							var editPageData = EditUiElementData.Get(player.userID);

							switch (arg.GetString(2))
							{
								case "start":
								{
									var fieldName = arg.GetString(3);
									if (string.IsNullOrEmpty(fieldName)) return;

									var targetField = editPageData.editingElement.GetType().GetField(fieldName);
									if (targetField == null)
										return;

									var parent = arg.GetString(4);
									if (string.IsNullOrEmpty(parent)) return;

									ShowColorSelectionPanel(player, fieldName, parent);
									break;
								}

								case "close":
								{
									break;
								}

								case "set":
								{
									var fieldName = arg.GetString(3);
									if (string.IsNullOrEmpty(fieldName)) return;

									var targetField = editPageData.editingElement.GetType().GetField(fieldName);
									if (targetField == null)
										return;

									var parent = arg.GetString(4);
									if (string.IsNullOrEmpty(parent)) return;

									switch (arg.GetString(5))
									{
										case "hex":
										{
											var hex = string.Join(" ", arg.Args.Skip(6));
											if (string.IsNullOrEmpty(hex)) return;

											var str = hex.Trim('#');
											if (!str.IsHex())
												return;

											if (targetField.GetValue(editPageData.editingElement) is not IColor
											    targetValue) return;

											targetValue.Hex = str;

											targetField.SetValue(editPageData.editingElement, targetValue);
											break;
										}

										case "opacity":
										{
											var opacity = arg.GetFloat(6);
											if (opacity is < 0 or > 100)
												return;

											opacity = (float) Math.Round(opacity, 2);

											if (targetField.GetValue(editPageData.editingElement) is not IColor
											    targetValue) return;

											targetValue.Alpha = opacity;

											targetField.SetValue(editPageData.editingElement, targetValue);
											break;
										}
									}

									UpdateUI(player, container =>
									{
										if (editPageData.isTextEditing)
											ShowTextEditorLinesUI(player, ref container);
										else
											editPageData.UpdateEditElement(ref container, player);

										FieldElementUI(container, parent, targetField,
											targetField.GetValue(editPageData.editingElement));
									});

									ShowColorSelectionPanel(player, fieldName, parent);
									break;
								}
							}

							break;
						}

						case "text":
						{
							var editPageData = EditUiElementData.Get(player.userID);

							switch (arg.GetString(2))
							{
								case "start":
								{
									editPageData.StartTextEditing();

									ShowTextEditorPanel(player);
									break;
								}

								case "close":
								{
									editPageData.StopTextEditing();

									SaveData();
									break;
								}

								case "lang":
								{
									switch (arg.GetString(3))
									{
										case "select":
										{
											var targetLang = arg.GetString(4);
											if (string.IsNullOrEmpty(targetLang)) return;

											editPageData.SelectLang(targetLang);

											UpdateUI(player, container =>
											{
												ShowTextEditorLangsUI(player, container);

												ShowTextEditorLinesUI(player, ref container);
											});

											break;
										}

										case "remove":
										{
											var targetLang = arg.GetString(4);
											if (string.IsNullOrEmpty(targetLang)) return;

											editPageData.RemoveLang(targetLang);

											UpdateUI(player, container =>
											{
												ShowTextEditorLangsUI(player, container);

												ShowTextEditorLinesUI(player, ref container);
											});
											break;
										}
									}

									break;
								}

								case "line":
								{
									var textAction = arg.GetString(3);

									var textIndex = arg.GetInt(4);

									var text = editPageData.GetText().ToList();

									if (textAction != "add")
										if (textIndex < 0 || textIndex >= text.Count)
											return;

									switch (textAction)
									{
										case "set":
										{
											var val = string.Join(" ", arg.Args.Skip(5));
											if (string.IsNullOrEmpty(val)) return;

											text[textIndex] = val;

											editPageData.SaveTextForLang(text);

											UpdateUI(player, container => ShowTextEditorLinesUI(player, ref container));
											break;
										}

										case "remove":
										{
											text.RemoveAt(textIndex);

											editPageData.SaveTextForLang(text);

											CuiHelper.DestroyUi(player,
												EditingLayerModalTextEditor + $".Main.Edit.Text.Line.{text.Count + 1}");

											UpdateUI(player, container => ShowTextEditorLinesUI(player, ref container));
											break;
										}

										case "add":
										{
											text.Add(string.Empty);

											editPageData.SaveTextForLang(text);

											UpdateUI(player,
												container =>
												{
													var fontSize = Convert.ToInt32(editPageData.editingElement.GetType()
														.GetField("FontSize")?.GetValue(editPageData.editingElement));
													var textLineHeight = fontSize * 1.5f;

													var totalHeight = text.Count * textLineHeight +
													                  (text.Count - 1) * UI_TextEditor_Lines_Margin_Y;

													totalHeight += 34 + 20;

													ShowTextEditorScrollLinesUI(player, ref container, totalHeight);

													// ShowTextEditorLinesUI(ref container, text, font, fontSize, align, textColor, textLineHeight);
												});
											break;
										}
									}

									break;
								}
							}

							break;
						}

						case "rect_transform":
						{
							switch (arg.GetString(2))
							{
								case "start":
								{
									ShowPositionSelectionPanel(player);
									break;
								}

								case "close":
								{
									break;
								}

								case "select":
								{
									var anchors = string.Join(" ", arg.Args.Skip(3));
									if (string.IsNullOrEmpty(anchors)) return;

									var anchorsValues = anchors.Trim('(', ')').Split(',');
									if (anchorsValues.Length != 4)
									{
										return;
									}

									var anchorMinX = float.Parse(anchorsValues[0].Trim());
									var anchorMinY = float.Parse(anchorsValues[1].Trim());
									var anchorMaxX = float.Parse(anchorsValues[2].Trim());
									var anchorMaxY = float.Parse(anchorsValues[3].Trim());

									var editPageData = EditUiElementData.Get(player.userID);

									editPageData.editingElement.AnchorMinY = anchorMinY;
									editPageData.editingElement.AnchorMinX = anchorMinX;
									editPageData.editingElement.AnchorMaxY = anchorMaxY;
									editPageData.editingElement.AnchorMaxX = anchorMaxX;

									UpdateUI(player, container =>
									{
										editPageData.UpdateEditElement(ref container, player);

										PositionFieldElementUI(container, editPageData.editingElement);
									});
									break;
								}
							}

							break;
						}
					}

					break;
				}

				case "edit_category":
				{
					if (!CanPlayerEdit(player) ||
					    !TryGetOpenedMenu(player.userID, out var openedMenu))
						return;
					
					switch (arg.GetString(1))
					{
						case "start":
						{
							var categoryID = arg.GetInt(2);

							var targetCategory = GetCategoryById(categoryID);
							if (targetCategory == null) return;
							
							EditCategoryData.Create(player, categoryID);

							ShowCategoryEditorPanel(player);
							break;
						}

						case "create":
						{
							EditCategoryData.Create(player, -1, needCreate: true);

							ShowCategoryEditorPanel(player);
							break;
						}

						case "close":
						{
							EditCategoryData.Remove(player.userID);
							break;
						}

						case "save":
						{
							var editCategoryData = EditCategoryData.Get(player.userID);
							if (editCategoryData == null) return;
							
							editCategoryData.Save();
							
							UpdateUI(player, container =>
							{
								_templateData?.ShowCategoriesScrollUI(player, container, false);
							});
							// UpdateUI(player, container => { ShowNavigation(player, container); });
							break;
						}
 
						case "remove": // remove category
						{
							var editCategoryData = EditCategoryData.Get(player.userID);
							if (editCategoryData == null) return;

							editCategoryData.Remove();

							UpdateUI(player, container =>
							{
								_templateData?.ShowCategoriesScrollUI(player, container, false);
							});
							// UpdateUI(player, container => { ShowNavigation(player, container); });
							break;
						}

						case "field":
						{
							var editCategoryData = EditCategoryData.Get(player.userID);
							if (editCategoryData == null) return;

							var fieldName = arg.GetString(2);

							var targetField = editCategoryData.menuCategory.GetType().GetField(fieldName);
							if (targetField == null)
								return;

							var parent = arg.GetString(3);
							if (string.IsNullOrEmpty(parent)) return;

							if (targetField.FieldType.IsEnum)
							{
								if (targetField.GetValue(editCategoryData.menuCategory) is not Enum nowEnum) return;

								Enum targetEnum = null;
								switch (arg.GetString(4))
								{
									case "prev":
									{
										targetEnum = nowEnum.Previous();
										break;
									}

									case "next":
									{
										targetEnum = nowEnum.Next();
										break;
									}
								}

								if (targetEnum == null) return;

								targetField.SetValue(editCategoryData.menuCategory, targetEnum);
							}
							else if (targetField.FieldType == typeof(List<string>))
							{
								var val = string.Join(" ", arg.Args.Skip(4));
								if (!string.IsNullOrEmpty(val))
								{
									var text = new List<string>();
									foreach (var line in val.Split('\n'))
									{
										text.Add(line);
									}

									targetField.SetValue(editCategoryData.menuCategory, text);
								}
							}
							else if (targetField.FieldType == typeof(string))
							{
								var val = string.Join(" ", arg.Args.Skip(4));
								if (!string.IsNullOrEmpty(val))
									targetField.SetValue(editCategoryData.menuCategory, val);
							}
							else
							{
								var newValue = string.Join(" ", arg.Args.Skip(4));

								try
								{
									var convertedValue = Convert.ChangeType(newValue, targetField.FieldType);
									targetField.SetValue(editCategoryData.menuCategory, convertedValue);
								}
								catch (Exception ex)
								{
									Puts($"Error setting property '{fieldName}': {ex.Message}");
									player.SendMessage($"Error setting property '{fieldName}': {ex.Message}");
									return;
								}
							}

							UpdateUI(player, container =>
							{
								CategoryEditorFieldUI(player, container, parent, targetField,
									targetField?.GetValue(editCategoryData.menuCategory));
							});
							break;
						}

						case "page":
						{
							switch (arg.GetString(2))
							{
								case "field":
								{
									var editCategoryData = EditCategoryData.Get(player.userID);
									if (editCategoryData == null) return;

									var pageIndex = arg.GetInt(3);

									var fieldName = arg.GetString(4);

									var categoryPage = editCategoryData.menuCategory.Pages[pageIndex];
									if (categoryPage == null) return;

									var targetField = categoryPage.GetType().GetField(fieldName);
									if (targetField == null)
										return;

									var parent = arg.GetString(5);
									if (string.IsNullOrEmpty(parent)) return;

									if (targetField.FieldType.IsEnum)
									{
										if (targetField.GetValue(categoryPage) is not Enum nowEnum)
											return;

										Enum targetEnum = null;
										switch (arg.GetString(6))
										{
											case "prev":
											{
												targetEnum = nowEnum.Previous();
												break;
											}

											case "next":
											{
												targetEnum = nowEnum.Next();
												break;
											}
										}

										if (targetEnum == null) return;

										targetField.SetValue(categoryPage, targetEnum);
									}
									else if (targetField.FieldType == typeof(List<string>))
									{
										var val = string.Join(" ", arg.Args.Skip(6));
										if (!string.IsNullOrEmpty(val))
										{
											var text = new List<string>();
											foreach (var line in val.Split('\n'))
											{
												text.Add(line);
											}

											targetField.SetValue(categoryPage, text);
										}
									}
									else if (targetField.FieldType == typeof(string))
									{
										var val = string.Join(" ", arg.Args.Skip(6));

										targetField.SetValue(categoryPage, val);
									}
									else
									{
										var newValue = string.Join(" ", arg.Args.Skip(6));

										try
										{
											var convertedValue = Convert.ChangeType(newValue, targetField.FieldType);
											targetField.SetValue(categoryPage, convertedValue);
										}
										catch (Exception ex)
										{
											Puts($"Error setting property '{fieldName}': {ex.Message}");
											player.SendMessage($"Error setting property '{fieldName}': {ex.Message}");
											return;
										}
									}

									UpdateUI(player, container =>
									{
										CategoryEditorPagesFieldUI(player, container, parent, targetField,
											targetField?.GetValue(categoryPage), pageIndex);
									});
									break;
								}
							}

							break;
						}

						case "array":
						{
							var editCategoryData = EditCategoryData.Get(player.userID);
							if (editCategoryData == null) return;

							switch (arg.GetString(2))
							{
								case "start":
								{
									var fieldName = arg.GetString(3);

									var targetField = editCategoryData.menuCategory.GetType().GetField(fieldName);
									if (targetField == null)
										return;

									editCategoryData.StartEditArray(
										targetField.GetValue(editCategoryData.menuCategory) as object[],
										targetField.Name);

									ShowCategoryArrayEditorModal(player);
									break;
								}

								case "close":
								{
									editCategoryData.StopEditArray();
									break;
								}

								case "add":
								{
									var targetField = editCategoryData.menuCategory.GetType()
										.GetField(editCategoryData.editableArrayName);
									if (targetField == null)
										return;

									var currentArray = editCategoryData.editableArray;
									var elementType = currentArray?.GetType().GetElementType();
									if (elementType == null) return;

									var newElementValue = (object) arg.GetString(3);

									var newLength = currentArray.Length + 1;
									var newArray = Array.CreateInstance(elementType, newLength);

									newArray.SetValue(newElementValue, 0);

									for (var i = 0; i < currentArray.Length; i++)
										newArray.SetValue(currentArray.GetValue(i), i + 1);

									targetField.SetValue(editCategoryData.menuCategory, newArray);

									editCategoryData.editableArray = newArray as object[];

									UpdateUI(player,
										container =>
										{
											CategoryArrayEditorLoopUI(editCategoryData.GetEditableArrayValues(),
												container);
										});
									break;
								}

								case "remove":
								{
									var targetField = editCategoryData.menuCategory.GetType()
										.GetField(editCategoryData.editableArrayName);
									if (targetField == null)
										return;

									var currentArray = editCategoryData.editableArray;
									var elementType = currentArray?.GetType().GetElementType();
									if (elementType == null) return;

									var indexToRemove = arg.GetInt(3, -1);
									if (indexToRemove < 0) return;

									var newLength = currentArray.Length - 1;

									var newArray = Array.CreateInstance(elementType, newLength);

									var j = 0;
									for (var i = 0; i < currentArray.Length; i++)
									{
										if (i != indexToRemove)
											newArray.SetValue(currentArray.GetValue(i), j++);
									}

									targetField.SetValue(editCategoryData.menuCategory, newArray);

									editCategoryData.editableArray = newArray as object[];

									CuiHelper.DestroyUi(player,
										EditingLayerModalArrayView + $".Command.{currentArray.Length - 1}");

									UpdateUI(player,
										container =>
										{
											CategoryArrayEditorLoopUI(editCategoryData.GetEditableArrayValues(),
												container);
										});
									break;
								}

								case "edit":
								{
									var targetField = editCategoryData.menuCategory.GetType()
										.GetField(editCategoryData.editableArrayName);
									if (targetField == null)
										return;

									var currentArray = editCategoryData.editableArray;
									var elementType = currentArray?.GetType().GetElementType();
									if (elementType == null) return;

									var indexToChange = arg.GetInt(3, -1);
									if (indexToChange < 0) return;

									var newElementValue = (object) arg.GetString(4);

									currentArray.SetValue(newElementValue, indexToChange);

									targetField.SetValue(editCategoryData.menuCategory, currentArray);

									editCategoryData.editableArray = currentArray;

									UpdateUI(player,
										container =>
										{
											CategoryArrayEditorLoopUI(editCategoryData.GetEditableArrayValues(),
												container);
										});
									break;
								}
							}

							break;
						}
					}

					break;
				}

				case "edit_header_fields":
				{
					if (!CanPlayerEdit(player))
						return;

					switch (arg.GetString(1))
					{
						case "start":
						{
							EditHeaderFieldsData.Create(player);

							ShowHeaderFieldsEditorPanel(player);
							break;
						}

						case "save":
						{
							var editData = EditHeaderFieldsData.Get(player.userID);

							editData?.Save();
							break;
						}

						case "element":
						{
							var editData = EditHeaderFieldsData.Get(player.userID);

							switch (arg.GetString(2))
							{
								case "edit":
								{
									var elementIndex = arg.GetInt(3);

									if (!editData.StartEditElement(elementIndex, LayerHeader))
										return;

									EditUiElementData.Create(player,
										editData.elementIndex,
										editData.OnEditElementSave,
										editData.OnEditElementStartEdit,
										editData.OnEditElementStopEdit,
										editData.OnStartTextEditing,
										editData.OnStopTextEditing);

									ShowElementEditorPanel(player);
									break;
								}

								case "add":
								{
									editData.HeaderFields.Add(new HeaderFieldUI(UiElement.CreatePanel(
										InterfacePosition.CreatePosition(0.5f, 0.5f, 0.5f, 0.5f, -50, -50, 50, 50),
										new IColor("#FFFFFF", 100))));

									SaveData();

									if (TryGetOpenedMenu(player.userID, out var openedMenu))
										openedMenu.UpdateContent();

									ShowPageEditorPanel(player);
									break;
								}

								case "remove":
								{
									if (!arg.HasArgs(3)) return;

									editData.HeaderFields.RemoveAt(arg.GetInt(3));

									SaveData();

									if (TryGetOpenedMenu(player.userID, out var openedMenu))
										openedMenu.UpdateContent();

									ShowPageEditorPanel(player);
									break;
								}

								case "move":
								{
									if (!arg.HasArgs(4)) return;

									switch (arg.GetString(3))
									{
										case "up":
										{
											editData.HeaderFields.MoveUp(arg.GetInt(4));
											break;
										}

										case "down":
										{
											editData.HeaderFields.MoveDown(arg.GetInt(4));
											break;
										}
									}

									SaveData();

									if (TryGetOpenedMenu(player.userID, out var openedMenu))
										openedMenu.UpdateContent();

									ShowPageEditorPanel(player);
									break;
								}
							}

							break;
						}
					}

					break;
				}

				case "edit_menu":
				{
					if (!CanPlayerEdit(player) ||
					    !TryGetOpenedMenu(player.userID, out var openedMenu))
						return;

					switch (arg.GetString(1))
					{
						case "change_mode":
						{
							openedMenu.OnChangeEditMode();

							ShowMenuUI(player);
							break;
						}

						case "category_create":
						{
							break;
						}

						case "category":
						{
							var targetCategoryID = arg.GetInt(3);
							var menuCategory = GetCategoryById(targetCategoryID);
							if (menuCategory == null) return;

							switch (arg.GetString(2))
							{
								case "up":
								{
									menuCategory.MoveUp();
									break;
								}

								case "down":
								{
									menuCategory.MoveDown();
									break;
								}
							}

							LoadCategories();

							UpdateUI(player, container => { _templateData?.ShowCategoriesLoopUI(player, container); });
							break;
						}
					}

					break;
				}
			}
		}

		#endregion Commands

		#region Interface

		#region Main Panel

		private void ShowMenuUI(BasePlayer player)
		{
			UpdateUI(player, container =>
			{
				ShowBackground(player, container);

				ShowNavigation(player, container);

				ShowHeader(player, container);

				ShowContent(player, container);

				ShowCloseButton(player, container);
			});
		}

		private void ShowBackground(BasePlayer player, CuiElementContainer container)
		{
			_templateData.ShowBackgroundUI(player, container,
				cmdOnClick: $"{CmdMainConsole} close");
		}

		private void ShowNavigation(BasePlayer player, CuiElementContainer container)
		{
			_templateData.ShowCategories(player, container);
		}

		private void ShowHeader(BasePlayer player, CuiElementContainer container)
		{
			_templateData.ShowHeaderUI(player, container);
		}

		private void ShowContent(BasePlayer player, CuiElementContainer container)
		{
			_templateData.ShowContentUI(player, container, $"{CmdMainConsole} menu page");
		}

		private void ShowCloseButton(BasePlayer player, CuiElementContainer container)
		{
			_templateData.ShowCloseButtonUI(player, container, Layer, Layer, $"{CmdMainConsole} close");
		}

		#endregion Main Panel

		#region Editor Panel

		private void ShowPageEditorPanel(BasePlayer player)
		{
			var container = new CuiElementContainer();

			var editData = EditPageData.Get(player.userID);

			#region Background

			container.Add(new CuiPanel()
			{
				RectTransform =
				{
					AnchorMin = "0 0",
					AnchorMax = "0 1",
					OffsetMin = "0 0",
					OffsetMax = "360 0"
				},
				Image =
				{
					Color = "0 0 0 0"
				}
			}, Layer, EditingLayerPageEditor, EditingLayerPageEditor);

			container.Add(new CuiPanel()
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Image =
				{
					Color = "0 0 0 0.9",
					Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
				},
			}, EditingLayerPageEditor);

			#region Title

			container.Add(new CuiElement
			{
				Parent = EditingLayerPageEditor,
				Components =
				{
					new CuiTextComponent
					{
						Text = "CUI ELEMENT\nSELECTION",
						Font = "robotocondensed-bold.ttf",
						FontSize = 30,
						Align = TextAnchor.MiddleLeft,
						Color = HexToCuiColor("#E2DBD3", 90)
					},
					new CuiRectTransformComponent
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = "40 -145", OffsetMax = "0 0"
					}
				}
			});

			#endregion Title

			ShowCloseButtonUI(container, EditingLayerPageEditor, EditingLayerPageEditor + ".CloseButton",
				EditingLayerPageEditor,
				$"{CmdMainConsole} edit_page save");

			#endregion Background

			#region Selection

			#region Scroll View

			var offsetY = 0f;
			var fieldHeight = 40f;
			var fieldMarginY = 10f;

			var elements = editData.categoryPage.Elements;

			var totalHeight = elements.Count * fieldHeight + (elements.Count - 1) * fieldMarginY;

			totalHeight += 2f + fieldMarginY + fieldHeight;

			totalHeight = Mathf.Max(510, totalHeight);

			container.Add(new CuiElement()
			{
				Parent = EditingLayerPageEditor,
				Name = EditingLayerPageEditor + ".Selection",
				DestroyUi = EditingLayerPageEditor + ".Selection",
				Components =
				{
					new CuiScrollViewComponent
					{
						MovementType = ScrollRect.MovementType.Elastic,
						Vertical = true,
						Inertia = true,
						Horizontal = false,
						Elasticity = 0.25f,
						DecelerationRate = 0.3f,
						ScrollSensitivity = 24f,
						ContentTransform = new CuiRectTransform
						{
							AnchorMin = "0 1",
							AnchorMax = "1 1",
							OffsetMin = $"0 -{totalHeight}",
							OffsetMax = "0 0"
						},
						VerticalScrollbar = new CuiScrollbar()
						{
							Size = 5f, AutoHide = false,
							HandleColor = HexToCuiColor("#D74933"),
						},
					},
					new CuiRectTransformComponent()
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "10 55", OffsetMax = "-10 -145"
					}
				}
			});

			#endregion Scroll View

			#region UI Elements

			foreach (var cuiElement in elements)
			{
				var elementIndex = editData.categoryPage.Elements.IndexOf(cuiElement);

				container.Add(new CuiPanel
					{
						Image = {Color = "0.3019608 0.3019608 0.3019608 0.4"},
						RectTransform =
						{
							AnchorMin = "0 1", AnchorMax = "1 1",
							OffsetMin = $"10 {offsetY - fieldHeight}",
							OffsetMax = $"-20 {offsetY}"
						}
					}, EditingLayerPageEditor + ".Selection",
					EditingLayerPageEditor + $".Selection.Element.{elementIndex}",
					EditingLayerPageEditor + $".Selection.Element.{elementIndex}");

				PageEditorFieldUI(container, elementIndex, cuiElement);

				offsetY = offsetY - fieldHeight - fieldMarginY;
			}

			if (elements.Count > 0)
			{
				container.Add(new CuiPanel()
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = $"10 {offsetY - 2f}",
						OffsetMax = $"-20 {offsetY}"
					},
					Image =
					{
						Color = HexToCuiColor("#E2DBD3", 20)
					}
				}, EditingLayerPageEditor + ".Selection");

				offsetY = offsetY - 2f - fieldMarginY;
			}

			container.Add(new CuiButton()
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = $"10 {offsetY - fieldHeight}",
					OffsetMax = $"-20 {offsetY}"
				},
				Text =
				{
					Text = "+ ADD NEW LAYER",
					Font = "robotocondensed-bold.ttf", FontSize = 22,
					Align = TextAnchor.MiddleLeft, Color = HexToCuiColor("#CF432D", 90)
				},
				Button =
				{
					Color = "0 0 0 0",
					Command = $"{CmdMainConsole} edit_page element add"
				}
			}, EditingLayerPageEditor + ".Selection");

			#endregion UI Elements

			#endregion Selection

			CuiHelper.AddUi(player, container);
		}

		private void ShowHeaderFieldsEditorPanel(BasePlayer player)
		{
			var container = new CuiElementContainer();

			var editData = EditHeaderFieldsData.Get(player.userID);

			#region Background

			container.Add(new CuiPanel()
			{
				RectTransform =
				{
					AnchorMin = "0 0",
					AnchorMax = "0 1",
					OffsetMin = "0 0",
					OffsetMax = "360 0"
				},
				Image =
				{
					Color = "0 0 0 0"
				}
			}, Layer, EditingLayerPageEditor, EditingLayerPageEditor);

			container.Add(new CuiPanel()
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Image =
				{
					Color = "0 0 0 0.9",
					Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
				},
			}, EditingLayerPageEditor);

			#region Title

			container.Add(new CuiElement
			{
				Parent = EditingLayerPageEditor,
				Components =
				{
					new CuiTextComponent
					{
						Text = "CUI ELEMENT\nSELECTION",
						Font = "robotocondensed-bold.ttf",
						FontSize = 30,
						Align = TextAnchor.MiddleLeft,
						Color = HexToCuiColor("#E2DBD3", 90)
					},
					new CuiRectTransformComponent
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = "40 -145", OffsetMax = "0 0"
					}
				}
			});

			#endregion Title

			ShowCloseButtonUI(container, EditingLayerPageEditor, EditingLayerPageEditor + ".CloseButton",
				EditingLayerPageEditor,
				$"{CmdMainConsole} edit_header_fields save");

			#endregion Background

			#region Selection

			#region Scroll View

			container.Add(new CuiElement()
			{
				Parent = EditingLayerPageEditor,
				Name = EditingLayerPageEditor + ".Selection",
				DestroyUi = EditingLayerPageEditor + ".Selection",
				Components =
				{
					new CuiScrollViewComponent
					{
						MovementType = ScrollRect.MovementType.Elastic,
						Vertical = true,
						Inertia = true,
						Horizontal = false,
						Elasticity = 0.25f,
						DecelerationRate = 0.3f,
						ScrollSensitivity = 24f,
						ContentTransform = new CuiRectTransform
						{
							AnchorMin = "0 1",
							AnchorMax = "1 1",
							OffsetMin = "0 -{PAGE_EDITOR_SCROLL_SIZE}",
							OffsetMax = "0 0"
						},
						VerticalScrollbar = new CuiScrollbar()
						{
							Size = 5f, AutoHide = false,
							HandleColor = HexToCuiColor("#D74933"),
						},
					},
					new CuiRectTransformComponent()
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "10 55", OffsetMax = "-10 -145"
					}
				}
			});

			#endregion Scroll View

			#region UI Elements

			var offsetY = 0f;
			var fieldHeight = 40f;
			var fieldMarginY = 10f;

			var elements = editData.HeaderFields;

			TitleEditorUI(container, EditingLayerPageEditor + ".Selection", ref offsetY, "HEADER FIELDS",
				margin: fieldMarginY);

			foreach (var cuiElement in elements)
			{
				var elementIndex = editData.HeaderFields.IndexOf(cuiElement);

				container.Add(new CuiPanel
					{
						Image = {Color = "0.3019608 0.3019608 0.3019608 0.4"},
						RectTransform =
						{
							AnchorMin = "0 1", AnchorMax = "1 1",
							OffsetMin = $"10 {offsetY - fieldHeight}",
							OffsetMax = $"-20 {offsetY}"
						}
					}, EditingLayerPageEditor + ".Selection",
					EditingLayerPageEditor + $".Selection.Element.{elementIndex}",
					EditingLayerPageEditor + $".Selection.Element.{elementIndex}");

				PageEditorFieldUI(container, elementIndex, cuiElement,
					cmdRemove: "edit_header_fields element remove",
					cmdEdit: "edit_header_fields element edit",
					cmdMove: "edit_header_fields element move");

				offsetY = offsetY - fieldHeight - fieldMarginY;
			}

			if (elements.Count > 0)
			{
				container.Add(new CuiPanel()
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = $"10 {offsetY - 2f}",
						OffsetMax = $"-20 {offsetY}"
					},
					Image =
					{
						Color = HexToCuiColor("#E2DBD3", 20)
					}
				}, EditingLayerPageEditor + ".Selection");

				offsetY = offsetY - 2f - fieldMarginY;
			}

			container.Add(new CuiButton()
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = $"10 {offsetY - fieldHeight}",
					OffsetMax = $"-20 {offsetY}"
				},
				Text =
				{
					Text = "+ ADD NEW LAYER",
					Font = "robotocondensed-bold.ttf", FontSize = 22,
					Align = TextAnchor.MiddleLeft, Color = HexToCuiColor("#CF432D", 90)
				},
				Button =
				{
					Color = "0 0 0 0",
					Command = $"{CmdMainConsole} edit_header_fields element add"
				}
			}, EditingLayerPageEditor + ".Selection");

			#endregion UI Elements

			#endregion Selection

			CuiHelper.AddUi(player,
				container.ToJson().Replace("{PAGE_EDITOR_SCROLL_SIZE}", (Mathf.Abs(offsetY) + 100).ToString("N")));
		}

		private void ShowElementEditorPanel(BasePlayer player)
		{
			var container = new CuiElementContainer();

			var editData = EditUiElementData.Get(player.userID);

			var targetElement = editData.editingElement;

			#region Background

			container.Add(new CuiPanel()
			{
				RectTransform =
				{
					AnchorMin = "0 0",
					AnchorMax = "0 1",
					OffsetMin = "0 0",
					OffsetMax = "360 0"
				},
				Image =
				{
					Color = "0 0 0 0"
				}
			}, Layer, EditingLayerElementEditor, EditingLayerElementEditor);

			container.Add(new CuiPanel()
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Image =
				{
					Color = "0 0 0 0.95",
					Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
				},
			}, EditingLayerElementEditor);

			#region Title

			container.Add(new CuiElement
			{
				Parent = EditingLayerElementEditor,
				Components =
				{
					new CuiTextComponent
					{
						Text = "UI EDITOR",
						Font = "robotocondensed-bold.ttf",
						FontSize = 30,
						Align = TextAnchor.MiddleLeft,
						Color = HexToCuiColor("#E2DBD3", 90)
					},
					new CuiRectTransformComponent
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = "40 -145", OffsetMax = "0 0"
					}
				}
			});

			#endregion Title

			ShowCloseButtonUI(container, EditingLayerElementEditor,
				EditingLayerElementEditor + ".CloseButton", EditingLayerElementEditor,
				commandOnClose: $"{CmdMainConsole} edit_element save");

			#endregion Background

			#region Selection

			#region Scroll View

			var offsetY = 0f;
			var fieldHeight = 85f;
			var fieldMarginY = 10f;

			container.Add(new CuiElement()
			{
				Parent = EditingLayerElementEditor,
				Name = EditingLayerElementEditor + ".Selection",
				DestroyUi = EditingLayerElementEditor + ".Selection",
				Components =
				{
					new CuiScrollViewComponent
					{
						MovementType = ScrollRect.MovementType.Elastic,
						Vertical = true,
						Inertia = true,
						Horizontal = false,
						Elasticity = 0.25f,
						DecelerationRate = 0.3f,
						ScrollSensitivity = 24f,
						ContentTransform = new CuiRectTransform
						{
							AnchorMin = "0 1",
							AnchorMax = "1 1",
							OffsetMin = "0 %TOTAL_SCROLL_HEIGHT%",
							OffsetMax = "0 0"
						},
						VerticalScrollbar = new CuiScrollbar()
						{
							Size = 5f, AutoHide = false,
							HandleColor = HexToCuiColor("#D74933"),
						},
					},
					new CuiRectTransformComponent()
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "10 55", OffsetMax = "-10 -145"
					}
				}
			});

			#endregion Scroll View

			#region Enabled Section

			var enabledField = targetElement.GetType().GetField("Enabled");
			var enabledLayer = CuiHelper.GetGuid();

			container.Add(new CuiPanel
			{
				Image = {Color = "0 0 0 0"},
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = $"20 {offsetY - fieldHeight}",
					OffsetMax = $"-20 {offsetY}"
				}
			}, EditingLayerElementEditor + ".Selection", enabledLayer + ".Background", enabledLayer + ".Background");

			FieldElementUI(container, enabledLayer, enabledField, enabledField?.GetValue(targetElement));

			offsetY = offsetY - fieldHeight - fieldMarginY;

			#endregion Enabled Section

			#region Type Section

			var typeField = targetElement.GetType().GetField("Type");
			var typeLayer = CuiHelper.GetGuid();

			container.Add(new CuiPanel
			{
				Image = {Color = "0 0 0 0"},
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = $"20 {offsetY - fieldHeight}",
					OffsetMax = $"-20 {offsetY}"
				}
			}, EditingLayerElementEditor + ".Selection", typeLayer + ".Background", typeLayer + ".Background");

			FieldElementUI(container, typeLayer, typeField, typeField?.GetValue(targetElement));

			offsetY = offsetY - fieldHeight - fieldMarginY;

			#endregion Type Section

			#region Name Section

			var nameField = targetElement.GetType().GetField("Name");
			var nameLayer = CuiHelper.GetGuid();

			container.Add(new CuiPanel
			{
				Image = {Color = "0 0 0 0"},
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = $"20 {offsetY - fieldHeight}",
					OffsetMax = $"-20 {offsetY}"
				}
			}, EditingLayerElementEditor + ".Selection", nameLayer + ".Background", nameLayer + ".Background");

			FieldElementUI(container, nameLayer, nameField, nameField?.GetValue(targetElement));

			offsetY = offsetY - fieldHeight - fieldMarginY;

			#endregion Name Section

			#region Rect Transform Section

			TitleEditorUI(container, EditingLayerElementEditor + ".Selection", ref offsetY, "RECT TRANSFORM",
				margin: fieldMarginY);

			if (targetElement is InterfacePosition interfacePos)
			{
				#region Position Section

				container.Add(new CuiPanel
					{
						Image = {Color = "0 0 0 0"},
						RectTransform =
						{
							AnchorMin = "0 1", AnchorMax = "1 1",
							OffsetMin = $"20 {offsetY - fieldHeight}",
							OffsetMax = $"-20 {offsetY}"
						}
					}, EditingLayerElementEditor + ".Selection",
					EditingLayerElementEditor + ".Selection.RectTransform.Position");

				PositionFieldElementUI(container, interfacePos);

				offsetY = offsetY - fieldHeight - fieldMarginY;

				#endregion

				foreach (var positionField in typeof(InterfacePosition).GetFields())
				{
					var targetFieldLayer = CuiHelper.GetGuid();

					container.Add(new CuiPanel
						{
							Image = {Color = "0 0 0 0"},
							RectTransform =
							{
								AnchorMin = "0 1", AnchorMax = "1 1",
								OffsetMin = $"20 {offsetY - fieldHeight}",
								OffsetMax = $"-20 {offsetY}"
							}
						}, EditingLayerElementEditor + ".Selection", targetFieldLayer + ".Background",
						targetFieldLayer + ".Background");

					FieldElementUI(container, targetFieldLayer, positionField, positionField.GetValue(interfacePos));

					offsetY = offsetY - fieldHeight - fieldMarginY;
				}
			}

			#endregion Rect Transform Section

			#region Label Section

			if (targetElement.Type is CuiElementType.Label or CuiElementType.InputField)
			{
				TitleEditorUI(container, EditingLayerElementEditor + ".Selection", ref offsetY, "TEXT STYLE",
					margin: fieldMarginY);

				container.Add(new CuiButton()
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = $"20 {offsetY - 40f}",
						OffsetMax = $"-20 {offsetY}"
					},
					Text =
					{
						Text = "EDIT TEXT",
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 20,
						Color = HexToCuiColor("#E2DBD3")
					},
					Button =
					{
						Color = HexToCuiColor("#4D4D4D", 50),
						Command = $"{CmdMainConsole} edit_element text start"
					}
				}, EditingLayerElementEditor + ".Selection");

				offsetY = offsetY - 40f;
			}

			#endregion Label Section

			#region Panel Section

			if (targetElement.Type == CuiElementType.Panel)
			{
				TitleEditorUI(container, EditingLayerElementEditor + ".Selection", ref offsetY, "PANEL STYLE",
					margin: fieldMarginY);

				var targetFields = new List<FieldInfo>
				{
					targetElement.GetType().GetField("Color")
				};

				foreach (var panelField in targetFields)
				{
					var targetFieldLayer = CuiHelper.GetGuid();

					container.Add(new CuiPanel
						{
							Image = {Color = "0 0 0 0"},
							RectTransform =
							{
								AnchorMin = "0 1", AnchorMax = "1 1",
								OffsetMin = $"20 {offsetY - fieldHeight}",
								OffsetMax = $"-20 {offsetY}"
							}
						}, EditingLayerElementEditor + ".Selection", targetFieldLayer + ".Background",
						targetFieldLayer + ".Background");

					FieldElementUI(container, targetFieldLayer, panelField, panelField.GetValue(targetElement));

					offsetY = offsetY - fieldHeight - fieldMarginY;
				}
			}

			#endregion Panel Section

			#region Image Section

			if (targetElement.Type == CuiElementType.Image)
			{
				TitleEditorUI(container, EditingLayerElementEditor + ".Selection", ref offsetY, "IMAGE STYLE",
					margin: fieldMarginY);

				var targetFields = new List<FieldInfo>
				{
					targetElement.GetType().GetField("Color"),
					targetElement.GetType().GetField("Image")
				};

				foreach (var textField in targetFields)
				{
					var targetFieldLayer = CuiHelper.GetGuid();

					container.Add(new CuiPanel
						{
							Image = {Color = "0 0 0 0"},
							RectTransform =
							{
								AnchorMin = "0 1", AnchorMax = "1 1",
								OffsetMin = $"20 {offsetY - fieldHeight}",
								OffsetMax = $"-20 {offsetY}"
							}
						}, EditingLayerElementEditor + ".Selection", targetFieldLayer + ".Background",
						targetFieldLayer + ".Background");

					FieldElementUI(container, targetFieldLayer, textField, textField.GetValue(targetElement));

					offsetY = offsetY - fieldHeight - fieldMarginY;
				}
			}

			#endregion Image Section

			#region Button Section

			if (targetElement.Type == CuiElementType.Button)
			{
				#region Text

				TitleEditorUI(container, EditingLayerElementEditor + ".Selection", ref offsetY, "TEXT SETTINGS",
					margin: fieldMarginY);

				container.Add(new CuiButton()
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = $"20 {offsetY - 40f}",
						OffsetMax = $"-20 {offsetY}"
					},
					Text =
					{
						Text = "EDIT TEXT",
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 20,
						Color = HexToCuiColor("#E2DBD3")
					},
					Button =
					{
						Color = HexToCuiColor("#4D4D4D", 50),
						Command = $"{CmdMainConsole} edit_element text start"
					}
				}, EditingLayerElementEditor + ".Selection");

				offsetY = offsetY - 40f;

				#endregion

				offsetY = offsetY - 40f;
                
				#region Panel

				TitleEditorUI(container, EditingLayerElementEditor + ".Selection", ref offsetY, "BUTTON STYLE",
					margin: fieldMarginY);

				var targetFields = new List<FieldInfo>
				{
					targetElement.GetType().GetField("Command"),
					targetElement.GetType().GetField("Color"),
					targetElement.GetType().GetField("Sprite"),
					targetElement.GetType().GetField("Material"),
				};

				foreach (var textField in targetFields)
				{
					var targetFieldLayer = CuiHelper.GetGuid();

					container.Add(new CuiPanel
						{
							Image = {Color = "0 0 0 0"},
							RectTransform =
							{
								AnchorMin = "0 1", AnchorMax = "1 1",
								OffsetMin = $"20 {offsetY - fieldHeight}",
								OffsetMax = $"-20 {offsetY}"
							}
						}, EditingLayerElementEditor + ".Selection", targetFieldLayer + ".Background",
						targetFieldLayer + ".Background");

					FieldElementUI(container, targetFieldLayer, textField, textField.GetValue(targetElement));

					offsetY = offsetY - fieldHeight - fieldMarginY;
				}

				#endregion
			}

			#endregion
            
			#endregion Selection

			CuiHelper.AddUi(player, container.ToJson().Replace("%TOTAL_SCROLL_HEIGHT%", offsetY.ToString("N")));
		}

		#region Editor Selection Panels

		private void ShowPositionSelectionPanel(BasePlayer player)
		{
			var editPageData = EditUiElementData.Get(player.userID);
			if (editPageData is not {editingElement: InterfacePosition interfacePos})
				return;

			var container = new CuiElementContainer();

			var itemsOnLine = 4;
			var margin = 10f;
			var width = 130f;
			var height = 29f;

			var constOffsetX = -((itemsOnLine * width) + (itemsOnLine - 1) * margin) / 2f;
			var offsetX = constOffsetX;
			var offsetY = -25f;
			var index = 1;

			var bgLayer = container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1"
					},
					Image =
					{
						Color = HexToCuiColor("#000000", 98)
					}
				}, "Overlay",
				EditingLayerModalAnchorSelector,
				EditingLayerModalAnchorSelector);

			var mainLayer = container.Add(new CuiPanel
				{
					RectTransform =
					{
						AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
						OffsetMin = "-300 -100",
						OffsetMax = "300 100"
					},
					Image =
					{
						Color = HexToCuiColor("#202224")
					}
				}, EditingLayerModalAnchorSelector, EditingLayerModalAnchorSelector + ".Main",
				EditingLayerModalAnchorSelector + ".Main");

			#region Header

			#region Title

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = "0 0",
					OffsetMax = "0 40"
				},
				Text =
				{
					Text = Msg(player, "SELECT POSITION"),
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-bold.ttf",
					FontSize = 22,
					Color = HexToCuiColor("#DCDCDC")
				}
			}, mainLayer);

			#endregion

			#region Close

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "1 1", AnchorMax = "1 1",
					OffsetMin = "-30 5",
					OffsetMax = "0 35"
				},
				Text =
				{
					Text = Msg(player, "X"),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 22,
					Color = HexToCuiColor("#EF5125")
				},
				Button =
				{
					Color = "0 0 0 0",
					Close = bgLayer,
					Command = $"{CmdMainConsole} edit_element rect_transform close",
				}
			}, mainLayer, mainLayer + ".BTN.Close.Edit");

			#endregion

			#endregion

			foreach (var (rect, image) in rectToImage)
			{
				var isSelectedRect = interfacePos.GetAnchorImage() == image;

				var rectLayer = CuiHelper.GetGuid();

				container.Add(new CuiElement()
				{
					Name = rectLayer,
					Parent = EditingLayerModalAnchorSelector + ".Main",
					Components =
					{
						new CuiImageComponent()
						{
							Color = isSelectedRect ? HexToCuiColor("#A5EA32", 20) : HexToCuiColor("#4D4D4D", 40)
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "0.5 1",
							AnchorMax = "0.5 1",
							OffsetMin = $"{offsetX} {offsetY - height}",
							OffsetMax = $"{offsetX + width} {offsetY}"
						}
					}
				});

				container.Add(new CuiElement()
				{
					Parent = rectLayer,
					Components =
					{
						new CuiRawImageComponent()
						{
							Png = GetImage(image)
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "0 0",
							AnchorMax = "1 1",
							OffsetMin = "5 5",
							OffsetMax = "-5 -5"
						}
					}
				});

				container.Add(new CuiElement
				{
					Parent = rectLayer,
					Components =
					{
						new CuiButtonComponent()
						{
							Color = "0 0 0 0",
							Close = bgLayer,
							Command = $"{CmdMainConsole} edit_element rect_transform select {rect}",
						},
						new CuiRectTransformComponent()
					}
				});

				if (index % itemsOnLine == 0)
				{
					offsetX = constOffsetX;
					offsetY = offsetY - height - margin;
				}
				else
				{
					offsetX += width + margin;
				}

				index++;
			}

			CuiHelper.AddUi(player, container);
		}

		private void ShowColorSelectionPanel(BasePlayer player, string fieldName, string parentLayer)
		{
			var editPageData = EditUiElementData.Get(player.userID);

			var targetField = editPageData.editingElement.GetType().GetField(fieldName);
			if (targetField == null || targetField.GetValue(editPageData.editingElement) is not IColor selectedColor)
				return;

			var container = new CuiElementContainer();

			#region Background

			var bgLayer = container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 1"
				},
				Image =
				{
					Color = HexToCuiColor("#000000", 98)
				}
			}, "Overlay", EditingLayerModalColorSelector, EditingLayerModalColorSelector);

			#endregion

			#region Main

			var mainLayer = container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
					OffsetMin = "-240 -260",
					OffsetMax = "240 260"
				},
				Image =
				{
					Color = HexToCuiColor("#202224")
				}
			}, bgLayer, EditingLayerModalColorSelector + ".Main", EditingLayerModalColorSelector + ".Main");

			#region Header

			#region Title

			container.Add(new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = "0 0",
					OffsetMax = "0 40"
				},
				Text =
				{
					Text = Msg(player, "COLOR PICKER"),
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-bold.ttf",
					FontSize = 22,
					Color = HexToCuiColor("#DCDCDC")
				}
			}, mainLayer);

			#endregion

			#region Close

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "1 1", AnchorMax = "1 1",
					OffsetMin = "-30 5",
					OffsetMax = "0 35"
				},
				Text =
				{
					Text = Msg(player, "X"),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 22,
					Color = HexToCuiColor("#EF5125")
				},
				Button =
				{
					Color = "0 0 0 0",
					Close = bgLayer,
					Command = $"{CmdMainConsole} edit_element color close",
				}
			}, mainLayer, mainLayer + ".BTN.Close.Edit");

			#endregion

			#endregion

			#region Colors

			var topRightColor = Color.blue;
			var bottomRightColor = Color.green;
			var topLeftColor = Color.red;
			var bottomLeftColor = Color.yellow;

			var scale = 20f;
			var total = scale * 2 - 8f;

			var width = 20f;
			var height = 20f;

			var constSwitchX = -((int) scale * width) / 2f;
			var xSwitch = constSwitchX;
			var ySwitch = -20f;

			for (var y = 0f; y < scale; y += 1f)
			{
				var heightColor = Color.Lerp(topRightColor, bottomRightColor, y.Scale(0f, scale, 0f, 1f));

				for (float x = 0; x < scale; x += 1f)
				{
					var widthColor = Color.Lerp(topLeftColor, bottomLeftColor, (x + y).Scale(0f, total, 0f, 1f));
					var targetColor = Color.Lerp(widthColor, heightColor, x.Scale(0f, scale, 0f, 1f)) * 1f;

					container.Add(new CuiButton
					{
						RectTransform =
						{
							AnchorMin = "0.5 1", AnchorMax = "0.5 1",
							OffsetMin = $"{xSwitch} {ySwitch - height}",
							OffsetMax = $"{xSwitch + width} {ySwitch}"
						},
						Text = {Text = string.Empty},
						Button =
						{
							Color = $"{targetColor.r} {targetColor.g} {targetColor.b} 1",
							Command =
								$"{CmdMainConsole} edit_element color set {fieldName} {parentLayer} hex {ColorUtility.ToHtmlStringRGB(targetColor)}"
						}
					}, mainLayer);

					xSwitch += width;
				}

				xSwitch = constSwitchX;
				ySwitch -= height;
			}

			#endregion

			#region Selected Color

			if (selectedColor != null)
			{
				#region Show Color

				container.Add(new CuiElement
				{
					Name = mainLayer + ".Selected.Color",
					Parent = mainLayer,
					Components =
					{
						new CuiImageComponent
						{
							Color = selectedColor.Get()
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "0.5 0",
							AnchorMax = "0.5 0",
							OffsetMin = $"{constSwitchX} 30",
							OffsetMax = $"{constSwitchX + 100f} 60"
						},
						new CuiOutlineComponent
						{
							Color = HexToCuiColor("#575757"),
							Distance = "3 -3",
							UseGraphicAlpha = true
						}
					}
				});

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 1",
						AnchorMax = "1 1",
						OffsetMin = "0 0",
						OffsetMax = "0 25"
					},
					Text =
					{
						Text = Msg(player, "Selected color:"),
						Align = TextAnchor.UpperLeft,
						Font = "robotocondensed-regular.ttf",
						FontSize = 14,
						Color = "1 1 1 1"
					}
				}, mainLayer + ".Selected.Color");

				#endregion

				#region Input

				#region HEX

				container.Add(new CuiElement
				{
					Name = mainLayer + ".Selected.Color.Input.HEX",
					Parent = mainLayer,
					Components =
					{
						new CuiImageComponent
						{
							Color = HexToCuiColor("#2F3134")
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "0.5 0", AnchorMax = "0.5 0",
							OffsetMin = $"{Mathf.Abs(constSwitchX) - 180} 30",
							OffsetMax = $"{Mathf.Abs(constSwitchX) - 100} 60"
						},
						new CuiOutlineComponent
						{
							Color = HexToCuiColor("#575757"),
							Distance = "1 -1"
						}
					}
				});

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 1",
						AnchorMax = "1 1",
						OffsetMin = "0 0",
						OffsetMax = "0 20"
					},
					Text =
					{
						Text = Msg(player, "HEX"),
						Align = TextAnchor.UpperLeft,
						Font = "robotocondensed-regular.ttf",
						FontSize = 12,
						Color = HexToCuiColor("#FFFFFF")
					}
				}, mainLayer + ".Selected.Color.Input.HEX");

				container.Add(new CuiElement
				{
					Parent = mainLayer + ".Selected.Color.Input.HEX",
					Components =
					{
						new CuiInputFieldComponent
						{
							FontSize = 10,
							Align = TextAnchor.MiddleCenter,
							Command = $"{CmdMainConsole} edit_element color set {fieldName} {parentLayer} hex",
							Color = HexToCuiColor("#575757"),
							CharsLimit = 150,
							Text = $"{selectedColor.Hex}",
							NeedsKeyboard = true
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = "0 0", OffsetMax = "0 0"
						}
					}
				});

				#endregion

				#region Opacity

				container.Add(new CuiElement
				{
					Name = mainLayer + ".Selected.Color.Input.Opacity",
					Parent = mainLayer,
					Components =
					{
						new CuiImageComponent
						{
							Color = HexToCuiColor("#2F3134")
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "0.5 0", AnchorMax = "0.5 0",
							OffsetMin = $"{Mathf.Abs(constSwitchX) - 90} 30",
							OffsetMax = $"{Mathf.Abs(constSwitchX)} 60"
						},
						new CuiOutlineComponent
						{
							Color = HexToCuiColor("#575757"),
							Distance = "1 -1"
						}
					}
				});

				container.Add(new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0 1",
						AnchorMax = "1 1",
						OffsetMin = "0 0",
						OffsetMax = "0 20"
					},
					Text =
					{
						Text = Msg(player, "Opacity (0-100)"),
						Align = TextAnchor.UpperLeft,
						Font = "robotocondensed-regular.ttf",
						FontSize = 12,
						Color = HexToCuiColor("#FFFFFF")
					}
				}, mainLayer + ".Selected.Color.Input.Opacity");

				container.Add(new CuiElement
				{
					Parent = mainLayer + ".Selected.Color.Input.Opacity",
					Components =
					{
						new CuiInputFieldComponent
						{
							FontSize = 10,
							Align = TextAnchor.MiddleCenter,
							Command = $"{CmdMainConsole} edit_element color set {fieldName} {parentLayer} opacity",
							Color = HexToCuiColor("#575757"),
							CharsLimit = 150,
							Text = $"{selectedColor.Alpha}",
							NeedsKeyboard = true
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "0 0", AnchorMax = "1 1",
							OffsetMin = "0 0", OffsetMax = "0 0"
						}
					}
				});

				#endregion

				#endregion
			}

			#endregion

			#endregion

			CuiHelper.AddUi(player, container);
		}

		#region Text Editor

		private const float
			UI_TextEditor_TextStyle_Height = 85f,
			UI_TextEditor_TextStyle_Margin_Y = 5f,
			UI_TextEditor_Lines_Margin_Y = 0f,
			UI_TextEditor_Lang_Margin_Y = 4f,
			UI_TextEditor_Lang_Height = 26f;

		private void ShowTextEditorPanel(BasePlayer player)
		{
			var elementData = EditUiElementData.Get(player.userID);

			var text = elementData.GetText();

			var container = new CuiElementContainer();

			#region Background

			var bgLayer = container.Add(new CuiPanel
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 1"
				},
				Image =
				{
					Color = HexToCuiColor("#000000", 98)
				},
				CursorEnabled = true
			}, "Overlay", EditingLayerModalTextEditor, EditingLayerModalTextEditor);

			#endregion

			#region Main

			var mainLayer = container.Add(new CuiPanel
			{
				RectTransform =
					{AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-640 -360", OffsetMax = "640 360"},
				Image =
				{
					Color = HexToCuiColor("#202224")
				}
			}, bgLayer, EditingLayerModalTextEditor + ".Main", EditingLayerModalTextEditor + ".Main");

			#region Header

			#region Title

			container.Add(new CuiLabel
			{
				RectTransform =
					{AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "40 -75", OffsetMax = "0 -25"},
				Text =
				{
					Text = Msg(player, "TEXT EDITOR"),
					Align = TextAnchor.MiddleLeft,
					Font = "robotocondensed-bold.ttf",
					FontSize = 32,
					Color = HexToCuiColor("#E2DBD3", 90)
				}
			}, mainLayer);

			#endregion

			#region Close

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "1 1", AnchorMax = "1 1",
					OffsetMin = "-30 -30",
					OffsetMax = "0 0"
				},
				Text =
				{
					Text = Msg(player, "X"),
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 22,
					Color = HexToCuiColor("#EF5125")
				},
				Button =
				{
					Color = "0 0 0 0",
					Close = bgLayer,
					Command = $"{CmdMainConsole} edit_element text close",
				}
			}, mainLayer, mainLayer + ".BTN.Close.Edit");

			#endregion

			#endregion

			#region Select Lang

			#region Fields

			var totalHeight = _langList.Count * UI_TextEditor_Lang_Height +
			                  (_langList.Count - 1) * UI_TextEditor_Lang_Margin_Y;

			#endregion

			container.Add(new CuiPanel
				{
					RectTransform =
						{AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "20 -585", OffsetMax = "300 -135"},
					Image =
					{
						Color = "0 0 0 0"
					}
				}, EditingLayerModalTextEditor + ".Main", EditingLayerModalTextEditor + ".Main.Select.Lang",
				EditingLayerModalTextEditor + ".Main.Select.Lang");

			#region Title

			container.Add(new CuiElement
			{
				Parent = EditingLayerModalTextEditor + ".Main.Select.Lang",
				Components =
				{
					new CuiTextComponent
					{
						Text = "SELECT LANG", Font = "robotocondensed-regular.ttf", FontSize = 32,
						Align = TextAnchor.UpperLeft, Color = "0.8117647 0.2627451 0.1764706 0.9019608"
					},
					new CuiRectTransformComponent
						{AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -50", OffsetMax = "0 0"}
				}
			});

			#endregion Title

			#region Scroll

			container.Add(new CuiPanel
				{
					RectTransform =
						{AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 -50"},
					Image =
					{
						Color = "0 0 0 0"
					}
				}, EditingLayerModalTextEditor + ".Main.Select.Lang",
				EditingLayerModalTextEditor + ".Main.Edit.Select.Lang.Panel",
				EditingLayerModalTextEditor + ".Main.Edit.Select.Lang.Panel");

			container.Add(new CuiElement
			{
				Name = EditingLayerModalTextEditor + ".Main.Select.Lang.Scroll.View",
				Parent = EditingLayerModalTextEditor + ".Main.Edit.Select.Lang.Panel",
				Components =
				{
					new CuiScrollViewComponent()
					{
						MovementType = ScrollRect.MovementType.Elastic,
						Vertical = true,
						Inertia = true,
						Horizontal = false,
						Elasticity = 0.25f,
						DecelerationRate = 0.3f,
						ScrollSensitivity = 24f,
						ContentTransform = new CuiRectTransform
						{
							AnchorMin = "0 1", AnchorMax = "1 1",
							OffsetMin = $"0 {-totalHeight}",
							OffsetMax = "0 0"
						},
						VerticalScrollbar = new CuiScrollbar
						{
							Size = 3,
							AutoHide = false,
							HighlightColor = HexToCuiColor("#CE412B"),
							HandleColor = HexToCuiColor("#CE412B"),
							PressedColor = HexToCuiColor("#CE412B"),
							TrackColor = HexToCuiColor("#2C2F31")
						}
					}
				}
			});

			#endregion

			#region Loop

			ShowTextEditorLangsUI(player, container);

			#endregion

			#endregion

			#region Text Lines

			#region Fields

			var fontSize = Convert.ToInt32(elementData.editingElement.GetType().GetField("FontSize")
				?.GetValue(elementData.editingElement));
			Enum.TryParse<TextAnchor>(
				elementData.editingElement.GetType().GetField("Align")?.GetValue(elementData.editingElement)
					?.ToString(), out var align);

			var textLineHeight = fontSize * 1.5f;

			totalHeight = text.Count * textLineHeight + (text.Count - 1) * UI_TextEditor_Lines_Margin_Y;

			totalHeight += 34 + 20;

			totalHeight = Mathf.Min(totalHeight + 300, 1000);

			#endregion

			container.Add(new CuiPanel
				{
					RectTransform =
						{AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-320 -585", OffsetMax = "320 -135"},
					Image =
					{
						Color = "0 0 0 0"
					}
				}, EditingLayerModalTextEditor + ".Main", EditingLayerModalTextEditor + ".Main.Edit.Text",
				EditingLayerModalTextEditor + ".Main.Edit.Text");

			#region Title

			container.Add(new CuiElement
			{
				Parent = EditingLayerModalTextEditor + ".Main.Edit.Text",
				Components =
				{
					new CuiTextComponent
					{
						Text = "EDIT TEXT LINES", Font = "robotocondensed-regular.ttf", FontSize = 32,
						Align = TextAnchor.UpperLeft, Color = "0.8117647 0.2627451 0.1764706 0.9019608"
					},
					new CuiRectTransformComponent
						{AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -50", OffsetMax = "0 0"}
				}
			});

			#endregion Title

			ShowTextEditorScrollLinesUI(player, ref container, totalHeight);

			#endregion

			#region Text Style

			#region Fields

			var targetFields = new List<FieldInfo>
			{
				elementData.editingElement.GetType().GetField("Font"),
				elementData.editingElement.GetType().GetField("FontSize"),
				elementData.editingElement.GetType().GetField("Align"),
				elementData.editingElement.GetType().GetField("TextColor")
			};

			totalHeight = targetFields.Count * UI_TextEditor_TextStyle_Height +
			              (targetFields.Count - 1) * UI_TextEditor_TextStyle_Margin_Y;

			#endregion

			container.Add(new CuiPanel
				{
					RectTransform =
						{AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-300 -585", OffsetMax = "-20 -135"},
					Image =
					{
						Color = "0 0 0 0"
					}
				}, EditingLayerModalTextEditor + ".Main", EditingLayerModalTextEditor + ".Main.Text.Style",
				EditingLayerModalTextEditor + ".Main.Text.Style");

			#region Title

			container.Add(new CuiElement
			{
				Parent = EditingLayerModalTextEditor + ".Main.Text.Style",
				Components =
				{
					new CuiTextComponent
					{
						Text = "TEXT STYLE", Font = "robotocondensed-regular.ttf", FontSize = 32,
						Align = TextAnchor.UpperLeft, Color = "0.8117647 0.2627451 0.1764706 0.9019608"
					},
					new CuiRectTransformComponent
						{AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -50", OffsetMax = "0 0"}
				}
			});

			#endregion Title

			#region Scroll

			container.Add(new CuiPanel
				{
					RectTransform =
						{AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 -50"},
					Image =
					{
						Color = "0 0 0 0"
					}
				}, EditingLayerModalTextEditor + ".Main.Text.Style",
				EditingLayerModalTextEditor + ".Main.Edit.Text.Style.Panel",
				EditingLayerModalTextEditor + ".Main.Edit.Text.Style.Panel");

			container.Add(new CuiElement
			{
				Name = EditingLayerModalTextEditor + ".Main.Text.Style.Scroll.View",
				Parent = EditingLayerModalTextEditor + ".Main.Edit.Text.Style.Panel",
				Components =
				{
					new CuiScrollViewComponent()
					{
						MovementType = ScrollRect.MovementType.Elastic,
						Vertical = true,
						Inertia = true,
						Horizontal = false,
						Elasticity = 0.25f,
						DecelerationRate = 0.3f,
						ScrollSensitivity = 24f,
						ContentTransform = new CuiRectTransform
						{
							AnchorMin = "0 1", AnchorMax = "1 1",
							OffsetMin = $"0 {-totalHeight}",
							OffsetMax = "0 0"
						},
						VerticalScrollbar = new CuiScrollbar
						{
							Size = 3,
							AutoHide = false,
							HighlightColor = HexToCuiColor("#CE412B"),
							HandleColor = HexToCuiColor("#CE412B"),
							PressedColor = HexToCuiColor("#CE412B"),
							TrackColor = HexToCuiColor("#2C2F31")
						}
					}
				}
			});

			#endregion

			#region Loop

			var offsetY = 0f;

			foreach (var targetField in targetFields)
			{
				container.Add(new CuiPanel
					{
						Image = {Color = "0 0 0 0"},
						RectTransform =
						{
							AnchorMin = "0 1", AnchorMax = "1 1",
							OffsetMin = $"0 {offsetY - UI_TextEditor_TextStyle_Height}", OffsetMax = $"-10 {offsetY}"
						}
					}, EditingLayerModalTextEditor + ".Main.Text.Style.Scroll.View",
					EditingLayerModalTextEditor + $".Main.Text.Style.Field.{targetField.Name}.Background",
					EditingLayerModalTextEditor + $".Main.Text.Style.Field.{targetField.Name}.Background");

				FieldElementUI(container, EditingLayerModalTextEditor + $".Main.Text.Style.Field.{targetField.Name}",
					targetField,
					targetField.GetValue(elementData.editingElement));

				offsetY = offsetY - UI_TextEditor_TextStyle_Height - UI_TextEditor_TextStyle_Margin_Y;
			}

			#endregion

			#endregion

			#endregion

			#region Save

			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-84 30", OffsetMax = "84 70"
				},
				Text =
				{
					Text = "SAVE", Font = "robotocondensed-bold.ttf", FontSize = 24, Align = TextAnchor.MiddleCenter,
					Color = "0.8862745 0.8588235 0.827451 1"
				},
				Button =
				{
					Color = "0 0.372549 0.7176471 1",
					Close = bgLayer,
					Command = $"{CmdMainConsole} edit_element text close",
				}
			}, EditingLayerModalTextEditor + ".Main");

			#endregion

			CuiHelper.AddUi(player, container);
		}

		private void ShowTextEditorScrollLinesUI(BasePlayer player,
			ref CuiElementContainer container,
			float totalHeight)
		{
			#region Scroll

			container.Add(new CuiPanel
				{
					RectTransform =
						{AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 -50"},
					Image =
					{
						Color = "0 0 0 0"
					}
				}, EditingLayerModalTextEditor + ".Main.Edit.Text",
				EditingLayerModalTextEditor + ".Main.Edit.Text.Scroll.Panel",
				EditingLayerModalTextEditor + ".Main.Edit.Text.Scroll.Panel");

			container.Add(new CuiElement
			{
				Name = EditingLayerModalTextEditor + ".Main.Edit.Text.Scroll.View",
				Parent = EditingLayerModalTextEditor + ".Main.Edit.Text.Scroll.Panel",
				Components =
				{
					new CuiScrollViewComponent()
					{
						MovementType = ScrollRect.MovementType.Elastic,
						Vertical = true,
						Inertia = true,
						Horizontal = false,
						Elasticity = 0.25f,
						DecelerationRate = 0.3f,
						ScrollSensitivity = 24f,
						ContentTransform = new CuiRectTransform
						{
							AnchorMin = "0 1", AnchorMax = "1 1",
							OffsetMin = $"0 {-totalHeight}",
							OffsetMax = "0 0"
						},
						VerticalScrollbar = new CuiScrollbar
						{
							Size = 3,
							AutoHide = false,
							HighlightColor = HexToCuiColor("#CE412B"),
							HandleColor = HexToCuiColor("#CE412B"),
							PressedColor = HexToCuiColor("#CE412B"),
							TrackColor = HexToCuiColor("#2C2F31")
						}
					}
				}
			});

			#endregion

			ShowTextEditorLinesUI(player, ref container);
		}

		private void ShowTextEditorLangsUI(BasePlayer player, CuiElementContainer container)
		{
			var editPageData = EditUiElementData.Get(player.userID);

			var offsetY = 0f;
			foreach (var (flagPath, langKey, langName) in _langList)
			{
				var selectedLang = editPageData.IsSelectedLang(langKey);

				container.Add(new CuiPanel
					{
						Image =
						{
							Color = selectedLang ? HexToCuiColor("#A5EA32", 20) : HexToCuiColor("#4D4D4D", 40)
						},
						RectTransform =
						{
							AnchorMin = "0 1", AnchorMax = "1 1",
							OffsetMin = $"0 {offsetY - UI_TextEditor_Lang_Height}", OffsetMax = $"-10 {offsetY}"
						}
					}, EditingLayerModalTextEditor + ".Main.Select.Lang.Scroll.View",
					EditingLayerModalTextEditor + $".Main.Select.Lang.Item.{langKey}",
					EditingLayerModalTextEditor + $".Main.Select.Lang.Item.{langKey}");

				#region flag

				container.Add(new CuiElement
				{
					Parent = EditingLayerModalTextEditor + $".Main.Select.Lang.Item.{langKey}",
					Components =
					{
						new CuiImageComponent
						{
							Sprite = flagPath,
							Material = flagPath,
						},
						new CuiRectTransformComponent
							{AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "4 -7", OffsetMax = "18 7"}
					}
				});

				#endregion flag

				#region country

				container.Add(new CuiElement
				{
					Parent = EditingLayerModalTextEditor + $".Main.Select.Lang.Item.{langKey}",
					Components =
					{
						new CuiTextComponent
						{
							Text = langName, Font = "robotocondensed-bold.ttf", FontSize = 14,
							Align = TextAnchor.MiddleLeft, Color = "0.8862745 0.8588235 0.827451 0.9019608"
						},
						new CuiRectTransformComponent
							{AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "25 0", OffsetMax = "0 0"}
					}
				});

				#endregion country

				#region btn

				container.Add(new CuiElement
				{
					Parent = EditingLayerModalTextEditor + $".Main.Select.Lang.Item.{langKey}",
					Components =
					{
						new CuiButtonComponent()
						{
							Color = "0 0 0 0",
							Command = $"{CmdMainConsole} edit_element text lang select {langKey}",
						},
						new CuiRectTransformComponent()
					}
				});

				#endregion

				#region clear action

				if (editPageData.HasLang(langKey))
				{
					container.Add(new CuiElement
					{
						Name = EditingLayerModalTextEditor + $".Main.Select.Lang.Item.{langKey}.Clear",
						Parent = EditingLayerModalTextEditor + $".Main.Select.Lang.Item.{langKey}",
						Components =
						{
							new CuiImageComponent
							{
								Color = HexToCuiColor("#E2DBD3"),
								Sprite = "assets/icons/close.png"
							},
							new CuiRectTransformComponent
								{AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-18 -7", OffsetMax = "-4 7"}
						}
					});

					container.Add(new CuiElement
					{
						Parent = EditingLayerModalTextEditor + $".Main.Select.Lang.Item.{langKey}.Clear",
						Components =
						{
							new CuiButtonComponent()
							{
								Color = "0 0 0 0",
								Command = $"{CmdMainConsole} edit_element text lang remove {langKey}",
							},
							new CuiRectTransformComponent()
						}
					});
				}

				#endregion clear action

				offsetY = offsetY - UI_TextEditor_Lang_Height - UI_TextEditor_Lang_Margin_Y;
			}
		}

		private void ShowTextEditorLinesUI(BasePlayer player, ref CuiElementContainer container)
		{
			var editPageData = EditUiElementData.Get(player.userID);

			var text = editPageData.GetText();

			Enum.TryParse<CuiElementFont>(
				editPageData.editingElement.GetType().GetField("Font")
					?.GetValue(editPageData.editingElement)?.ToString(), out var fontType);
			var font = GetFontByType(fontType);

			var fontSize = Convert.ToInt32(editPageData.editingElement.GetType()
				.GetField("FontSize")?.GetValue(editPageData.editingElement));
			var textColor = editPageData.editingElement.GetType().GetField("TextColor")
				?.GetValue(editPageData.editingElement) as IColor;
			Enum.TryParse<TextAnchor>(
				editPageData.editingElement.GetType().GetField("Align")
					?.GetValue(editPageData.editingElement)?.ToString(), out var align);

			var textLineHeight = fontSize * 1.5f;

			var totalWidth = 620f;

			#region Loop

			var offsetY = 0f;
			
			for (var index = 0; index < text.Count; index++)
			{
				var textLine = text[index];

				var targetHeight = textLineHeight;

				var textSize = CalcSize(textLine);
				if (textSize.x > totalWidth)
				{
					var xSize = Mathf.CeilToInt(textSize.x / totalWidth);

					targetHeight = textLineHeight * xSize;
				}
				
				container.Add(new CuiPanel
					{
						Image = {Color = "0.3019608 0.3019608 0.3019608 0.4"},
						RectTransform =
						{
							AnchorMin = "0 1", AnchorMax = "1 1",
							OffsetMin = $"0 {offsetY - targetHeight}",
							OffsetMax = $"-40 {offsetY}"
						}
					}, EditingLayerModalTextEditor + ".Main.Edit.Text.Scroll.View",
					EditingLayerModalTextEditor + $".Main.Edit.Text.Line.{index}",
					EditingLayerModalTextEditor + $".Main.Edit.Text.Line.{index}");

				#region title

				container.Add(new CuiElement
				{
					Parent = EditingLayerModalTextEditor + $".Main.Edit.Text.Line.{index}",
					Components =
					{
						new CuiInputFieldComponent
						{
							Text = textLine,
							Font = font ?? "robotocondensed-bold.ttf",
							FontSize = fontSize,
							Align = align,
							Color = textColor?.Get() ?? "1 1 1 1",
							Command = $"{CmdMainConsole} edit_element text line set {index}",
							NeedsKeyboard = true,
							LineType = InputField.LineType.MultiLineNewline,
							HudMenuInput = true,

						},
						new CuiRectTransformComponent
							{AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 0", OffsetMax = "-5 0"}
					}
				});

				#endregion title

				#region clear

				container.Add(new CuiElement
				{
					Name = EditingLayerModalTextEditor + $".Main.Edit.Text.Line.{index}.Clear",
					Parent = EditingLayerModalTextEditor + $".Main.Edit.Text.Line.{index}",
					Components =
					{
						new CuiImageComponent
						{
							Color = HexToCuiColor("#E2DBD3"),
							Sprite = "assets/icons/close.png"
						},
						new CuiRectTransformComponent
							{AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "12 -8", OffsetMax = "28 8"}
					}
				});

				container.Add(new CuiElement
				{
					Parent = EditingLayerModalTextEditor + $".Main.Edit.Text.Line.{index}.Clear",
					Components =
					{
						new CuiButtonComponent()
						{
							Color = "0 0 0 0",
							Command = $"{CmdMainConsole} edit_element text line remove {index}"
						},
						new CuiRectTransformComponent()
					}
				});

				#endregion clear

				if (index == text.Count - 1)
					offsetY -= targetHeight;
				else
					offsetY = offsetY - targetHeight - UI_TextEditor_Lines_Margin_Y;
			}

			#endregion

			#region Add New Line

			offsetY -= 20;

			container.Add(new CuiPanel
				{
					Image = {Color = "0 0 0 0"},
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"0 {offsetY - 34}",
						OffsetMax = $"-40 {offsetY}"
					}
				}, EditingLayerModalTextEditor + ".Main.Edit.Text.Scroll.View",
				EditingLayerModalTextEditor + $".Main.Edit.Text.Line.{text.Count}",
				EditingLayerModalTextEditor + $".Main.Edit.Text.Line.{text.Count}");

			#region line

			container.Add(new CuiPanel
				{
					Image = {Color = "0.8862745 0.8588235 0.827451 0.1490196"},
					RectTransform = {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -2", OffsetMax = "0 0"}
				}, EditingLayerModalTextEditor + $".Main.Edit.Text.Line.{text.Count}");

			#endregion

			#region title

			container.Add(new CuiElement
			{
				Parent = EditingLayerModalTextEditor + $".Main.Edit.Text.Line.{text.Count}",
				Components =
				{
					new CuiTextComponent
					{
						Text = "+ ADD NEW LINE", Font = "robotocondensed-bold.ttf", FontSize = 14,
						Align = TextAnchor.MiddleLeft, Color = "0.8117647 0.2627451 0.1764706 0.9019608"
					},
					new CuiRectTransformComponent
						{AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 0", OffsetMax = "-5 0"}
				}
			});

			#endregion title

			container.Add(new CuiElement()
			{
				Parent = EditingLayerModalTextEditor + $".Main.Edit.Text.Line.{text.Count}",
				Components =
				{
					new CuiButtonComponent()
					{
						Color = "0 0 0 0",
						Command = $"{CmdMainConsole} edit_element text line add {text.Count}"
					},
					new CuiRectTransformComponent()
				}
			});

			#endregion
			
			Vector2 CalcSize(string line)
			{
				var width = line.Length * fontSize * 0.225f;
				var height = fontSize;

				return new Vector2(width, height);
			}
		}

		#endregion

		#endregion

		#region Category Editor

		private const int
			UI_CategoryEditor_EditField_Left_Indent = 0,
			UI_CategoryEditor_EditField_Width = 164,
			UI_CategoryEditor_EditField_Height = 50,
			UI_CategoryEditor_EditField_MarginX = 10,
			UI_CategoryEditor_EditField_MarginY = 6,
			UI_CategoryEditor_EditField_OnLine = 4,
			UI_CategoryEditor_EditArrayField_Left_Indent = 30,
			UI_CategoryEditor_EditArrayField_Width = 164,
			UI_CategoryEditor_EditArrayField_Height = 50,
			UI_CategoryEditor_EditArrayField_MarginX = 10,
			UI_CategoryEditor_EditArrayField_MarginY = 6,
			UI_CategoryEditor_EditArrayField_OnLine = 4,
			UI_CategoryEditor_CommandField_Height = 26,
			UI_CategoryEditor_CommandField_Margin = 2;

		private void ShowCategoryEditorPanel(BasePlayer player)
		{
			var editCategoryData = EditCategoryData.Get(player.userID);
			if (editCategoryData == null) return;

			var container = new CuiElementContainer();

			#region Background

			container.Add(new CuiPanel()
			{
				RectTransform =
				{
					AnchorMin = "0 0",
					AnchorMax = "1 1"
				},
				Image =
				{
					Color = "0 0 0 0.9",
					Material = "assets/content/ui/uibackgroundblur.mat",
					Sprite = "assets/content/ui/ui.background.transparent.radial.psd"
				}
			}, Layer, EditingLayerPageEditor, EditingLayerPageEditor);

			#endregion

			#region Main

			container.Add(new CuiPanel
			{
				Image = {Color = HexToCuiColor("#000000")},
				RectTransform =
				{
					AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
					OffsetMin = "-400 -300", OffsetMax = "400 300"
				}
			}, EditingLayerPageEditor, EditingLayerPageEditor + ".Main", EditingLayerPageEditor + ".Main");

			#endregion

			#region Header

			container.Add(new CuiElement
			{
				Parent = EditingLayerPageEditor + ".Main",
				Components =
				{
					new CuiRawImageComponent
					{
						Png = GetImage("ServerPanel_Editor_EditCategory")
					},
					new CuiRectTransformComponent
						{AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "16 -50", OffsetMax = "44 -22"}
				}
			});

			container.Add(new CuiElement
			{
				Parent = EditingLayerPageEditor + ".Main",
				Components =
				{
					new CuiTextComponent
					{
						Text = "EDIT CATEGORY", Font = "robotocondensed-regular.ttf", FontSize = 22,
						Align = TextAnchor.MiddleLeft, Color = HexToCuiColor("#CF432D", 90)
					},
					new CuiRectTransformComponent
						{AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "54 -54", OffsetMax = "0 -18"}
				}
			});

			#endregion

			#region Close Button

			container.Add(new CuiElement
			{
				Name = EditingLayerPageEditor + ".Button.Close",
				Parent = EditingLayerPageEditor + ".Main",
				Components =
				{
					new CuiButtonComponent
					{
						Color = "0 0 0 0",
						Command = $"{CmdMainConsole} edit_category close",
						Close = EditingLayerPageEditor
					},
					new CuiRectTransformComponent
						{AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-50 -50", OffsetMax = "-10 -10"}
				}
			});

			container.Add(new CuiElement
			{
				Parent = EditingLayerPageEditor + ".Button.Close",
				Components =
				{
					new CuiImageComponent {Sprite = "assets/icons/close.png"},
					new CuiRectTransformComponent
						{AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-10 -10", OffsetMax = "10 10"}
				}
			});

			#endregion

			#region Outline

			CreateOutline(container, HexToCuiColor("#CF432D"), 4, EditingLayerPageEditor + ".Main");

			#endregion

			#region Content

			var targetFields =
				Array.FindAll(editCategoryData.menuCategory.GetType().GetFields(),
					field => field.FieldType.IsPrimitive || field.FieldType == typeof(string));

			var maxLines = Mathf.CeilToInt((float) targetFields.Length / UI_CategoryEditor_EditField_OnLine);

			var totalHeight = maxLines * UI_CategoryEditor_EditField_Height +
			                  (maxLines - 1) * UI_CategoryEditor_EditField_MarginY;

			totalHeight = Mathf.Max(475, totalHeight);

			container.Add(new CuiPanel
				{
					Image = {Color = HexToCuiColor("#000000", 0)},
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "32 55", OffsetMax = "-32 -70"
					}
				}, EditingLayerPageEditor + ".Main", EditingLayerPageEditor + ".Content",
				EditingLayerPageEditor + ".Content");

			container.Add(new CuiElement
			{
				Parent = EditingLayerPageEditor + ".Content",
				Name = EditingLayerPageEditor + ".Content.View",
				DestroyUi = EditingLayerPageEditor + ".Content.View",
				Components =
				{
					new CuiScrollViewComponent
					{
						MovementType = ScrollRect.MovementType.Elastic,
						Vertical = true,
						Inertia = true,
						Horizontal = false,
						Elasticity = 0.25f,
						DecelerationRate = 0.3f,
						ScrollSensitivity = 24f,
						ContentTransform = new CuiRectTransform
						{
							AnchorMin = "0 1",
							AnchorMax = "1 1",
							OffsetMin = $"0 -{totalHeight}",
							OffsetMax = "0 0"
						},
						VerticalScrollbar = new CuiScrollbar
						{
							Size = 4,
							AutoHide = false,
							Invert = true,
							HighlightColor = HexToCuiColor("#D74933"),
							HandleColor = HexToCuiColor("#D74933"),
							PressedColor = HexToCuiColor("#D74933"),
							TrackColor = HexToCuiColor("#373737")
						}
					}
				}
			});

			#region Loop

			var offsetY = 0f;

			CategoryEditorCategoriesLoopUI(player, targetFields, container, editCategoryData, ref offsetY);

			#endregion

			#region Pages

			var pagesField = editCategoryData.menuCategory.GetType().GetField("Pages");
			if (pagesField?.GetValue(editCategoryData.menuCategory) is List<CategoryPage> categoryPages)
			{
				var pageTargetFields =
					Array.FindAll(typeof(CategoryPage).GetFields(),
						field => field.Name != nameof(CategoryPage.Elements));

				offsetY = offsetY - 20;

				var pagesHeader = container.Add(new CuiPanel
				{
					Image = {Color = HexToCuiColor("#E44028", 0)},
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "0 1",
						OffsetMin = $"0 {offsetY - 30}",
						OffsetMax = $"700 {offsetY}"
					}
				}, EditingLayerPageEditor + ".Content.View");

				container.Add(new CuiLabel()
				{
					Text =
					{
						Text = "PAGES SECTION", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-bold.ttf",
						FontSize = 24, Color = HexToCuiColor("#CF432D", 90)
					}
				}, pagesHeader);

				offsetY = offsetY - 30 - 20;

				for (var pageIndex = 0; pageIndex < categoryPages.Count; pageIndex++)
				{
					var categoryPage = categoryPages[pageIndex];

					var pageLayer = CuiHelper.GetGuid();

					#region Header

					container.Add(new CuiPanel
					{
						Image = {Color = HexToCuiColor("#FFFFFF", 20)},
						RectTransform =
						{
							AnchorMin = "0 1", AnchorMax = "0 1",
							OffsetMin = $"0 {offsetY - 30f}",
							OffsetMax = $"700 {offsetY}"
						}
					}, EditingLayerPageEditor + ".Content.View", pageLayer, pageLayer);

					container.Add(new CuiElement
					{
						Parent = pageLayer,
						Components =
						{
							new CuiTextComponent
							{
								Text = $"{pageIndex + 1}", Font = "robotocondensed-bold.ttf", FontSize = 14,
								Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#E2DBD3")
							},
							new CuiRectTransformComponent
								{AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"}
						}
					});

					#endregion

					offsetY = offsetY - 30f - 20f;

					#region Page Fields

					var offsetX = UI_CategoryEditor_EditField_Left_Indent;
					for (var fieldIndex = 0; fieldIndex < pageTargetFields.Length; fieldIndex++)
					{
						var targetField = pageTargetFields[fieldIndex];
						var fieldLayer = CuiHelper.GetGuid();

						container.Add(new CuiPanel
							{
								Image = {Color = "0 0 0 0"},
								RectTransform =
								{
									AnchorMin = "0 1",
									AnchorMax = "0 1",
									OffsetMin = $"{offsetX} {offsetY - UI_CategoryEditor_EditField_Height}",
									OffsetMax = $"{offsetX + UI_CategoryEditor_EditField_Width} {offsetY}"
								}
							}, EditingLayerPageEditor + ".Content.View", fieldLayer + ".Background",
							fieldLayer + ".Background");

						CategoryEditorPagesFieldUI(player, container, fieldLayer, targetField,
							targetField.GetValue(categoryPage), pageIndex);

						#region Calculate Position

						if (fieldIndex + 1 != pageTargetFields.Length)
						{
							if ((fieldIndex + 1) % UI_CategoryEditor_EditField_OnLine == 0)
							{
								offsetX = UI_CategoryEditor_EditField_Left_Indent;
								offsetY = offsetY - UI_CategoryEditor_EditField_Height -
								          UI_CategoryEditor_EditField_MarginY;
							}
							else
							{
								offsetX = offsetX + UI_CategoryEditor_EditField_Width +
								          UI_CategoryEditor_EditField_MarginX;
							}
						}

						#endregion
					}

					#endregion
				}

				#endregion
			}

			#endregion

			#region Buttons

			if (editCategoryData.NeedCreate)
			{
				#region Create

				container.Add(new CuiButton
				{
					Text =
					{
						Text = "CREATE", Font = "robotocondensed-regular.ttf", FontSize = 14,
						Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#E2DBD3")
					},
					Button =
					{
						Color = HexToCuiColor("#005FB7"),
						Command = $"{CmdMainConsole} edit_category save",
						Close = EditingLayerPageEditor
					},
					RectTransform =
					{
						AnchorMin = "0.5 0", AnchorMax = "0.5 0",
						OffsetMin = "-65 15", OffsetMax = "65 45"
					}
				}, EditingLayerPageEditor + ".Main");

				#endregion Create
			}
			else
			{
				#region Save

				container.Add(new CuiButton
				{
					Text =
					{
						Text = "SAVE", Font = "robotocondensed-regular.ttf", FontSize = 14,
						Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#E2DBD3")
					},
					Button =
					{
						Color = HexToCuiColor("#005FB7"),
						Command = $"{CmdMainConsole} edit_category save",
						Close = EditingLayerPageEditor
					},
					RectTransform =
					{
						AnchorMin = "0.5 0", AnchorMax = "0.5 0",
						OffsetMin = "-140 15", OffsetMax = "-10 45"
					}
				}, EditingLayerPageEditor + ".Main");

				#endregion Save

				#region Remove

				container.Add(new CuiButton
				{
					Text =
					{
						Text = "REMOVE", Font = "robotocondensed-regular.ttf", FontSize = 14,
						Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#E2DBD3")
					},
					Button =
					{
						Color = HexToCuiColor("#CF432D"),
						Command = $"{CmdMainConsole} edit_category remove",
						Close = EditingLayerPageEditor
					},
					RectTransform =
					{
						AnchorMin = "0.5 0", AnchorMax = "0.5 0",
						OffsetMin = "10 15", OffsetMax = "140 45"
					}
				}, EditingLayerPageEditor + ".Main");

				#endregion Remove
			}

			#endregion

			CuiHelper.AddUi(player, container);
		}

		private void ShowCategoryArrayEditorModal(BasePlayer player)
		{
			var editCategoryData = EditCategoryData.Get(player.userID);
			if (editCategoryData == null) return;

			var container = new CuiElementContainer();

			#region Background

			container.Add(new CuiPanel()
			{
				RectTransform =
				{
					AnchorMin = "0 0",
					AnchorMax = "1 1"
				},
				Image =
				{
					Color = "0 0 0 0.9",
					Material = "assets/content/ui/uibackgroundblur.mat",
					Sprite = "assets/content/ui/ui.background.transparent.radial.psd"
				}
			}, Layer, EditingLayerModal, EditingLayerModal);

			#endregion

			#region Main

			container.Add(new CuiPanel
			{
				Image = {Color = HexToCuiColor("#000000")},
				RectTransform =
				{
					AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
					OffsetMin = "-200 -150", OffsetMax = "200 150"
				}
			}, EditingLayerModal, EditingLayerModal + ".Main", EditingLayerModal + ".Main");

			#endregion

			#region Header

			container.Add(new CuiElement
			{
				Parent = EditingLayerModal + ".Main",
				Components =
				{
					new CuiTextComponent
					{
						Text = editCategoryData.editableArrayName ?? string.Empty, Font = "robotocondensed-regular.ttf",
						FontSize = 22, Align = TextAnchor.MiddleLeft, Color = HexToCuiColor("#CF432D", 90)
					},
					new CuiRectTransformComponent
						{AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "20 -47", OffsetMax = "-50 -13"}
				}
			});

			#endregion

			#region Close Button

			container.Add(new CuiElement
			{
				Name = EditingLayerModal + ".Button.Close",
				Parent = EditingLayerModal + ".Main",
				Components =
				{
					new CuiButtonComponent
					{
						Color = "0 0 0 0",
						Command = $"{CmdMainConsole} edit_category array close",
						Close = EditingLayerModal
					},
					new CuiRectTransformComponent
						{AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-50 -50", OffsetMax = "-10 -10"}
				}
			});

			container.Add(new CuiElement
			{
				Parent = EditingLayerModal + ".Button.Close",
				Components =
				{
					new CuiImageComponent {Color = HexToCuiColor("#FFFFFF"), Sprite = "assets/icons/close.png"},
					new CuiRectTransformComponent
						{AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-10 -10", OffsetMax = "10 10"}
				}
			});

			#endregion

			#region Add Button

			container.Add(new CuiElement
			{
				Name = EditingLayerModal + ".Button.Add",
				Parent = EditingLayerModal + ".Main",
				Components =
				{
					new CuiButtonComponent
					{
						Color = "0 0 0 0",
						Command = $"{CmdMainConsole} edit_category array add",
					},
					new CuiRectTransformComponent
						{AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-100 -50", OffsetMax = "-60 -10"}
				}
			});

			container.Add(new CuiElement
			{
				Parent = EditingLayerModal + ".Button.Add",
				Components =
				{
					new CuiImageComponent {Color = HexToCuiColor("#FFFFFF"), Sprite = "assets/icons/add.png"},
					new CuiRectTransformComponent
						{AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-10 -10", OffsetMax = "10 10"}
				}
			});

			#endregion

			#region Outline

			CreateOutline(container, HexToCuiColor("#CF432D"), 4, EditingLayerModal + ".Main");

			#endregion

			#region Content

			var arrayValues = editCategoryData.GetEditableArrayValues();

			var maxLines = Mathf.CeilToInt((float) arrayValues.Length / UI_CategoryEditor_EditField_OnLine);

			var totalHeight = maxLines * UI_CategoryEditor_EditField_Height +
			                  (maxLines - 1) * UI_CategoryEditor_EditField_MarginY;

			totalHeight = Mathf.Max(475, totalHeight);

			#region Scroll View

			container.Add(new CuiPanel
				{
					Image = {Color = HexToCuiColor("#000000", 0)},
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "32 55", OffsetMax = "-32 -70"
					}
				}, EditingLayerModal + ".Main",
				EditingLayerModal + ".Content",
				EditingLayerModal + ".Content");

			container.Add(new CuiElement
			{
				Parent = EditingLayerModal + ".Content",
				Name = EditingLayerModalArrayView,
				DestroyUi = EditingLayerModalArrayView,
				Components =
				{
					new CuiScrollViewComponent
					{
						MovementType = ScrollRect.MovementType.Elastic,
						Vertical = true,
						Inertia = true,
						Horizontal = false,
						Elasticity = 0.25f,
						DecelerationRate = 0.3f,
						ScrollSensitivity = 24f,
						ContentTransform = new CuiRectTransform
						{
							AnchorMin = "0 1",
							AnchorMax = "1 1",
							OffsetMin = $"0 -{totalHeight}",
							OffsetMax = "0 0"
						},
						VerticalScrollbar = new CuiScrollbar
						{
							Size = 4,
							AutoHide = false,
							Invert = true,
							HighlightColor = HexToCuiColor("#D74933"),
							HandleColor = HexToCuiColor("#D74933"),
							PressedColor = HexToCuiColor("#D74933"),
							TrackColor = HexToCuiColor("#373737")
						}
					}
				}
			});

			#endregion

			#region Loop

			CategoryArrayEditorLoopUI(arrayValues, container);

			#endregion

			#endregion

			CuiHelper.AddUi(player, container);
		}

		private static void CategoryArrayEditorLoopUI(object[] targetFields, CuiElementContainer container)
		{
			var offsetY = 0f;
			for (var cmdIndex = 0; cmdIndex < targetFields.Length; cmdIndex++)
			{
				var targetCMD = targetFields[cmdIndex];

				container.Add(new CuiPanel
					{
						Image =
						{
							Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
							Color = HexToCuiColor("#38393F", 40)
						},
						RectTransform =
						{
							AnchorMin = "0 1", AnchorMax = "1 1",
							OffsetMin = $"0 {offsetY - UI_CategoryEditor_CommandField_Height}",
							OffsetMax = $"-20 {offsetY}"
						}
					}, EditingLayerModalArrayView,
					EditingLayerModalArrayView + $".Command.{cmdIndex}",
					EditingLayerModalArrayView + $".Command.{cmdIndex}");

				container.Add(new CuiElement
				{
					Parent = EditingLayerModalArrayView + $".Command.{cmdIndex}",
					Components =
					{
						new CuiInputFieldComponent
						{
							Text = targetCMD?.ToString() ?? string.Empty,
							Font = "robotocondensed-bold.ttf", FontSize = 12, Align = TextAnchor.MiddleLeft,
							Color = HexToCuiColor("#E2DBD3"),
							NeedsKeyboard = true,
							Command = $"{CmdMainConsole} edit_category array edit {cmdIndex}"
						},
						new CuiRectTransformComponent
							{AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 0", OffsetMax = "-5 0"}
					}
				});

				container.Add(new CuiElement
				{
					Parent = EditingLayerModalArrayView + $".Command.{cmdIndex}",
					Components =
					{
						new CuiButtonComponent()
						{
							Color = HexToCuiColor("#E2DBD3"),
							Sprite = "assets/icons/close.png",
							Command = $"{CmdMainConsole} edit_category array remove {cmdIndex}"
						},
						new CuiRectTransformComponent
							{AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-20 -6", OffsetMax = "-8 6"}
					}
				});

				#region Calculate Position

				offsetY = offsetY - UI_CategoryEditor_CommandField_Height - UI_CategoryEditor_CommandField_Margin;

				#endregion
			}
		}

		#endregion

		#endregion Editor Panel

		#region UI.Components

		private static void TitleEditorUI(CuiElementContainer container,
			string parent,
			ref float offsetY,
			string textTitle,
			float size = 40f,
			float margin = 10f,
			int fontSize = 32)
		{
			var textStyleLayer = container.Add(new CuiPanel
			{
				Image = {Color = "0 0 0 0"},
				RectTransform =
				{
					AnchorMin = "0 1", AnchorMax = "1 1",
					OffsetMin = $"20 {offsetY - size}",
					OffsetMax = $"-20 {offsetY}"
				}
			}, parent);

			container.Add(new CuiLabel()
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 1"
				},
				Text =
				{
					Text = textTitle,
					Font = "robotocondensed-bold.ttf",
					FontSize = fontSize,
					Align = TextAnchor.MiddleLeft,
					Color = HexToCuiColor("#CF432D", 90)
				}
			}, textStyleLayer);

			offsetY = offsetY - size - margin;
		}

		private static void CategoryEditorCategoriesLoopUI(BasePlayer player, FieldInfo[] targetFields,
			CuiElementContainer container,
			EditCategoryData editCategoryData, ref float offsetY)
		{
			var offsetX = UI_CategoryEditor_EditField_Left_Indent;
			for (var fieldIndex = 0; fieldIndex < targetFields.Length; fieldIndex++)
			{
				var targetField = targetFields[fieldIndex];
				var fieldLayer = CuiHelper.GetGuid();

				container.Add(new CuiPanel
				{
					Image = {Color = "0 0 0 0"},
					RectTransform =
					{
						AnchorMin = "0 1",
						AnchorMax = "0 1",
						OffsetMin = $"{offsetX} {offsetY - UI_CategoryEditor_EditField_Height}",
						OffsetMax = $"{offsetX + UI_CategoryEditor_EditField_Width} {offsetY}"
					}
				}, EditingLayerPageEditor + ".Content.View", fieldLayer + ".Background", fieldLayer + ".Background");

				CategoryEditorFieldUI(player, container, fieldLayer, targetField,
					targetField.GetValue(editCategoryData.menuCategory));

				#region Calculate Position

				if (fieldIndex + 1 != targetFields.Length)
				{
					if ((fieldIndex + 1) % UI_CategoryEditor_EditField_OnLine == 0)
					{
						offsetX = UI_CategoryEditor_EditField_Left_Indent;
						offsetY = offsetY - UI_CategoryEditor_EditField_Height - UI_CategoryEditor_EditField_MarginY;
					}
					else
					{
						offsetX = offsetX + UI_CategoryEditor_EditField_Width + UI_CategoryEditor_EditField_MarginX;
					}
				}

				#endregion
			}

			offsetY = offsetY - UI_CategoryEditor_EditField_Height - UI_CategoryEditor_EditField_MarginY;

			#region Commands

			var commandsField = editCategoryData.menuCategory.GetType().GetField("Commands");
			if (commandsField?.GetValue(editCategoryData.menuCategory) is string[] commandsList)
			{
				offsetX = UI_CategoryEditor_EditField_Left_Indent;

				var targetFieldLayer = CuiHelper.GetGuid();

				container.Add(new CuiPanel
				{
					Image = {Color = "0 0 0 0"},
					RectTransform =
					{
						AnchorMin = "0 1",
						AnchorMax = "0 1",
						OffsetMin = $"{offsetX} {offsetY - UI_CategoryEditor_EditField_Height}",
						OffsetMax = $"{offsetX + UI_CategoryEditor_EditField_Width} {offsetY}"
					}
				}, EditingLayerPageEditor + ".Content.View", targetFieldLayer, targetFieldLayer);

				container.Add(new CuiLabel()
				{
					RectTransform = {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -24", OffsetMax = "0 0"},
					Text =
					{
						Text = $"{commandsField.GetFieldTitle()}",
						Font = "robotocondensed-bold.ttf", FontSize = 10, Align = TextAnchor.MiddleLeft,
						Color = HexToCuiColor("#E2DBD3")
					}
				}, targetFieldLayer);

				container.Add(new CuiButton()
				{
					Text =
					{
						Text = "OPEN",
						Font = "robotocondensed-bold.ttf",
						FontSize = 12,
						Align = TextAnchor.MiddleCenter,
						Color = HexToCuiColor("#E2DBD3"),
					},
					Button =
					{
						Color = HexToCuiColor("#38393F", 40),
						Command = $"{CmdMainConsole} edit_category array start {commandsField.Name}"
					},
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 0", OffsetMax = "0 26"
					}
				}, targetFieldLayer);
			}

			#endregion

			offsetY = offsetY - UI_CategoryEditor_EditField_Height;
		}

		private static void CategoryEditorLoopFieldUI(CuiElementContainer container,
			MenuCategory targetCategory,
			string commandsFieldLayer,
			FieldInfo commandsField)
		{
			if (commandsField?.GetValue(targetCategory) is not string[] commandsList)
				return;

			container.Add(new CuiPanel()
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Image = {Color = "0 0 0 0"}
				}, commandsFieldLayer + ".Background",
				commandsFieldLayer, commandsFieldLayer);

			container.Add(new CuiElement
			{
				Parent = commandsFieldLayer,
				Components =
				{
					new CuiTextComponent
					{
						Text =
							$"{commandsField.GetFieldTitle()}",
						Font = "robotocondensed-bold.ttf", FontSize = 10,
						Align = TextAnchor.MiddleLeft, Color = HexToCuiColor("#E2DBD3")
					},
					new CuiRectTransformComponent
						{AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -24", OffsetMax = "0 0"}
				}
			});

			container.Add(new CuiButton
			{
				RectTransform = {AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-24 -24", OffsetMax = "0 0"},
				Text =
				{
					Text = "+",
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-bold.ttf",
					FontSize = 18,
					Color = HexToCuiColor("#E2DBD3")
				},
				Button =
				{
					Color = "0 0 0 0",
					Command = $"{CmdMainConsole} edit_category array {commandsField.Name} {commandsFieldLayer} add"
				}
			}, commandsFieldLayer);

			var offsetY = -24f;
			for (var cmdIndex = 0; cmdIndex < commandsList.Length; cmdIndex++)
			{
				var targetCMD = commandsList[cmdIndex];

				container.Add(new CuiPanel
					{
						Image =
						{
							Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
							Color = HexToCuiColor("#38393F", 40)
						},
						RectTransform =
						{
							AnchorMin = "0 1", AnchorMax = "1 1",
							OffsetMin = $"0 {offsetY - UI_CategoryEditor_CommandField_Height}",
							OffsetMax = $"0 {offsetY}"
						}
					}, commandsFieldLayer, commandsFieldLayer + $".Command.{cmdIndex}",
					commandsFieldLayer + $".Command.{cmdIndex}");

				container.Add(new CuiElement
				{
					Parent = commandsFieldLayer + $".Command.{cmdIndex}",
					Components =
					{
						new CuiInputFieldComponent
						{
							Text = targetCMD ?? string.Empty,
							Font = "robotocondensed-bold.ttf", FontSize = 12, Align = TextAnchor.MiddleLeft,
							Color = HexToCuiColor("#E2DBD3"),
							NeedsKeyboard = true,
							Command =
								$"{CmdMainConsole} edit_category array {commandsField.Name} {commandsFieldLayer} edit {cmdIndex}"
						},
						new CuiRectTransformComponent
							{AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 0", OffsetMax = "-5 0"}
					}
				});

				container.Add(new CuiElement
				{
					Parent = commandsFieldLayer + $".Command.{cmdIndex}",
					Components =
					{
						new CuiButtonComponent()
						{
							Color = HexToCuiColor("#E2DBD3"),
							Sprite = "assets/icons/close.png",
							Command =
								$"{CmdMainConsole} edit_category array {commandsField.Name} {commandsFieldLayer} remove {cmdIndex}"
						},
						new CuiRectTransformComponent
							{AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-16 -6", OffsetMax = "-4 6"}
					}
				});

				#region Calculate Position

				offsetY = offsetY - UI_CategoryEditor_CommandField_Height - UI_CategoryEditor_CommandField_Margin;

				#endregion
			}
		}

		private static void CategoryEditorFieldUI(BasePlayer player,
			CuiElementContainer container,
			string targetFieldLayer,
			FieldInfo targetField,
			object fieldValue)
		{
			container.Add(new CuiPanel()
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Image = {Color = "0 0 0 0"}
			}, targetFieldLayer + ".Background", targetFieldLayer, targetFieldLayer);

			if (fieldValue is bool boolValue)
			{
				container.Add(new CuiLabel()
				{
					RectTransform = {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -24", OffsetMax = "0 0"},
					Text =
					{
						Text = $"{targetField.GetFieldTitle()}",
						Font = "robotocondensed-bold.ttf", FontSize = 10, Align = TextAnchor.MiddleLeft,
						Color = HexToCuiColor("#E2DBD3")
					}
				}, targetFieldLayer);

				container.Add(new CuiButton()
				{
					Text =
					{
						Text = boolValue ? "ON" : "OFF",
						Font = "robotocondensed-bold.ttf",
						FontSize = 12,
						Align = TextAnchor.MiddleCenter,
						Color = HexToCuiColor("#E2DBD3")
					},
					Button =
					{
						Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
						Color = boolValue ? HexToCuiColor("#D74933", 90) : HexToCuiColor("#71B8ED", 20),
						Command =
							$"{CmdMainConsole} edit_category field {targetField.Name} {targetFieldLayer} {!boolValue}",
					},
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 0", OffsetMax = "0 26"}
				}, targetFieldLayer);
			}
			else
			{
				container.Add(new CuiLabel()
				{
					RectTransform = {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -24", OffsetMax = "0 0"},
					Text =
					{
						Text = $"{targetField.GetFieldTitle()}",
						Font = "robotocondensed-bold.ttf", FontSize = 10, Align = TextAnchor.MiddleLeft,
						Color = HexToCuiColor("#E2DBD3")
					}
				}, targetFieldLayer);

				#region Value

				var fieldValueStr = fieldValue?.ToString();

				container.Add(new CuiPanel
				{
					Image = {Color = HexToCuiColor("#38393F", 40)},
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 0", OffsetMax = "0 26"}
				}, targetFieldLayer, targetFieldLayer + ".Value");

				container.Add(new CuiElement()
				{
					Parent = targetFieldLayer + ".Value",
					Components =
					{
						new CuiInputFieldComponent()
						{
							Font = "robotocondensed-bold.ttf",
							FontSize = 12,
							Align = TextAnchor.MiddleLeft,
							Color = HexToCuiColor("#E2DBD3"),
							Text = $"{fieldValueStr}",
							NeedsKeyboard = true,
							Command = $"{CmdMainConsole} edit_category field {targetField.Name} {targetFieldLayer}"
						},
						new CuiRectTransformComponent() {AnchorMin = "0 0", AnchorMax = "1 1"}
					}
				});

				#endregion
			}
		}

		private static void CategoryEditorPagesFieldUI(BasePlayer player,
			CuiElementContainer container,
			string targetFieldLayer,
			FieldInfo targetField,
			object fieldValue,
			int pageIndex)
		{
			container.Add(new CuiPanel()
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Image = {Color = "0 0 0 0"}
			}, targetFieldLayer + ".Background", targetFieldLayer, targetFieldLayer);

			container.Add(new CuiLabel()
			{
				RectTransform = {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -24", OffsetMax = "0 0"},
				Text =
				{
					Text = $"{targetField.GetFieldTitle()}",
					Font = "robotocondensed-bold.ttf", FontSize = 10, Align = TextAnchor.MiddleLeft,
					Color = HexToCuiColor("#E2DBD3")
				}
			}, targetFieldLayer);

			if (targetField.FieldType.IsEnum)
			{
				#region Value

				var fieldValueStr = fieldValue?.ToString();

				container.Add(new CuiPanel
				{
					Image = {Color = HexToCuiColor("#38393F", 40)},
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 0", OffsetMax = "0 26"}
				}, targetFieldLayer, targetFieldLayer + ".Value");

				container.Add(new CuiElement()
				{
					Parent = targetFieldLayer + ".Value",
					Components =
					{
						new CuiTextComponent()
						{
							Font = "robotocondensed-bold.ttf",
							FontSize = 12,
							Align = TextAnchor.MiddleCenter,
							Color = HexToCuiColor("#E2DBD3"),
							Text = $"{fieldValueStr}",
						},
						new CuiRectTransformComponent() {AnchorMin = "0 0", AnchorMax = "1 1"}
					}
				});

				#endregion

				#region Buttons

				container.Add(new CuiButton()
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "0 1",
						OffsetMin = "0 0", OffsetMax = "18 0"
					},
					Text =
					{
						Text = "<",
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 14,
						Color = HexToCuiColor("#E2DBD3", 90)
					},
					Button =
					{
						Command =
							$"{CmdMainConsole} edit_category page field {pageIndex} {targetField.Name} {targetFieldLayer} prev",
						Color = HexToCuiColor("#4D4D4D", 50)
					}
				}, targetFieldLayer + ".Value");

				container.Add(new CuiButton()
				{
					RectTransform =
					{
						AnchorMin = "1 0", AnchorMax = "1 1",
						OffsetMin = "-18 0", OffsetMax = "0 0"
					},
					Text =
					{
						Text = ">",
						Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf",
						FontSize = 14,
						Color = HexToCuiColor("#E2DBD3", 90)
					},
					Button =
					{
						Command =
							$"{CmdMainConsole} edit_category page field {pageIndex} {targetField.Name} {targetFieldLayer} next",
						Color = HexToCuiColor("#4D4D4D", 50)
					}
				}, targetFieldLayer + ".Value");

				#endregion
			}
			else if (fieldValue is bool boolValue)
			{
				container.Add(new CuiButton()
				{
					Text =
					{
						Text = boolValue ? "ON" : "OFF",
						Font = "robotocondensed-bold.ttf",
						FontSize = 12,
						Align = TextAnchor.MiddleCenter,
						Color = HexToCuiColor("#E2DBD3")
					},
					Button =
					{
						Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
						Color = boolValue ? HexToCuiColor("#D74933", 90) : HexToCuiColor("#71B8ED", 20),
						Command =
							$"{CmdMainConsole} edit_category page field {pageIndex} {targetField.Name} {targetFieldLayer} {!boolValue}",
					},
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 0", OffsetMax = "0 26"}
				}, targetFieldLayer);
			}
			else
			{
				#region Value

				var fieldValueStr = fieldValue?.ToString();

				container.Add(new CuiPanel
				{
					Image = {Color = HexToCuiColor("#38393F", 40)},
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 0", OffsetMax = "0 26"}
				}, targetFieldLayer, targetFieldLayer + ".Value");

				container.Add(new CuiElement()
				{
					Parent = targetFieldLayer + ".Value",
					Components =
					{
						new CuiInputFieldComponent()
						{
							Font = "robotocondensed-bold.ttf",
							FontSize = 12,
							Align = TextAnchor.MiddleLeft,
							Color = HexToCuiColor("#E2DBD3"),
							Text = $"{fieldValueStr}",
							NeedsKeyboard = true,
							Command =
								$"{CmdMainConsole} edit_category page field {pageIndex} {targetField.Name} {targetFieldLayer}"
						},
						new CuiRectTransformComponent() {AnchorMin = "0 0", AnchorMax = "1 1"}
					}
				});

				#endregion
			}
		}

		private void PageEditorFieldUI(CuiElementContainer container,
			int elementIndex,
			UiElement cuiElement,
			string cmdRemove = "edit_page element remove",
			string cmdEdit = "edit_page element edit",
			string cmdMove = "edit_page element move")
		{
			container.Add(new CuiPanel()
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
					Image = {Color = "0 0 0 0"}
				}, EditingLayerPageEditor + $".Selection.Element.{elementIndex}",
				EditingLayerPageEditor + $".Selection.Element.{elementIndex}.Panel",
				EditingLayerPageEditor + $".Selection.Element.{elementIndex}.Panel");

			#region Point

			container.Add(new CuiPanel()
				{
					RectTransform =
					{
						AnchorMin = "0 0.5",
						AnchorMax = "0 0.5",
						OffsetMin = "5 -4", OffsetMax = "13 4"
					},
					Image =
					{
						Color = HexToCuiColor("#D9D9D9")
					}
				}, EditingLayerPageEditor + $".Selection.Element.{elementIndex}.Panel");

			#endregion

			#region Title

			UpdateTitlePageEditorFieldUI(container, elementIndex, cuiElement);

			#endregion

			#region Buttons

			#region Remove

			container.Add(new CuiElement()
			{
				Name = EditingLayerPageEditor + $".Selection.Element.{elementIndex}.Panel" + ".Button.Remove",
				Parent = EditingLayerPageEditor + $".Selection.Element.{elementIndex}.Panel",
				Components =
				{
					new CuiRawImageComponent()
					{
						Png = GetImage("ServerPanel_Editor_Btn_Remove")
					},
					new CuiRectTransformComponent()
					{
						AnchorMin = "1 0.5", AnchorMax = "1 0.5",
						OffsetMin = "-81 -8",
						OffsetMax = "-65 8"
					}
				}
			});

			container.Add(new CuiElement()
			{
				Parent = EditingLayerPageEditor + $".Selection.Element.{elementIndex}.Panel" + ".Button.Remove",
				Components =
				{
					new CuiButtonComponent()
					{
						Color = "0 0 0 0",
						Command = $"{CmdMainConsole} {cmdRemove} {elementIndex}",
					},
					new CuiRectTransformComponent()
					{
						AnchorMin = "0 0", AnchorMax = "1 1"
					}
				}
			});

			#endregion Remove

			#region Edit

			container.Add(new CuiElement()
			{
				Name = EditingLayerPageEditor + $".Selection.Element.{elementIndex}.Panel" + ".Button.Edit",
				Parent = EditingLayerPageEditor + $".Selection.Element.{elementIndex}.Panel",
				Components =
				{
					new CuiRawImageComponent()
					{
						Png = GetImage("ServerPanel_Editor_Btn_Edit")
					},
					new CuiRectTransformComponent()
					{
						AnchorMin = "1 0.5", AnchorMax = "1 0.5",
						OffsetMin = "-56 -8",
						OffsetMax = "-40 8"
					}
				}
			});

			container.Add(new CuiElement()
			{
				Parent = EditingLayerPageEditor + $".Selection.Element.{elementIndex}.Panel" + ".Button.Edit",
				Components =
				{
					new CuiButtonComponent()
					{
						Color = "0 0 0 0",
						Command = $"{CmdMainConsole} {cmdEdit} {elementIndex}",
					},
					new CuiRectTransformComponent()
					{
						AnchorMin = "0 0", AnchorMax = "1 1"
					}
				}
			});

			#endregion Edit

			#region Move.Up

			container.Add(new CuiElement()
			{
				Name = EditingLayerPageEditor + $".Selection.Element.{elementIndex}.Panel" + ".Button.Move.Up",
				Parent = EditingLayerPageEditor + $".Selection.Element.{elementIndex}.Panel",
				Components =
				{
					new CuiRawImageComponent()
					{
						Png = GetImage("ServerPanel_Editor_Btn_Up")
					},
					new CuiRectTransformComponent()
					{
						AnchorMin = "1 0.5", AnchorMax = "1 0.5",
						OffsetMin = "-28.5 1.5",
						OffsetMax = "-20 8"
					}
				}
			});

			container.Add(new CuiElement()
			{
				Parent = EditingLayerPageEditor + $".Selection.Element.{elementIndex}.Panel" + ".Button.Move.Up",
				Components =
				{
					new CuiButtonComponent()
					{
						Color = "0 0 0 0",
						Command = $"{CmdMainConsole} {cmdMove} up {elementIndex}",
					},
					new CuiRectTransformComponent()
					{
						AnchorMin = "0 0", AnchorMax = "1 1"
					}
				}
			});

			#endregion Move.Up

			#region Move.Down

			container.Add(new CuiElement()
			{
				Name = EditingLayerPageEditor + $".Selection.Element.{elementIndex}.Panel" + ".Button.Move.Down",
				Parent = EditingLayerPageEditor + $".Selection.Element.{elementIndex}.Panel",
				Components =
				{
					new CuiRawImageComponent()
					{
						Png = GetImage("ServerPanel_Editor_Btn_Down")
					},
					new CuiRectTransformComponent()
					{
						AnchorMin = "1 0.5", AnchorMax = "1 0.5",
						OffsetMin = "-28.5 -8",
						OffsetMax = "-20 -1.5"
					}
				}
			});

			container.Add(new CuiElement()
			{
				Parent = EditingLayerPageEditor + $".Selection.Element.{elementIndex}.Panel" + ".Button.Move.Down",
				Components =
				{
					new CuiButtonComponent()
					{
						Color = "0 0 0 0",
						Command = $"{CmdMainConsole} {cmdMove} down {elementIndex}",
					},
					new CuiRectTransformComponent()
					{
						AnchorMin = "0 0", AnchorMax = "1 1"
					}
				}
			});

			#endregion Move.Down

			#endregion Buttons
		}

		private static void UpdateTitlePageEditorFieldUI(CuiElementContainer container, int elementIndex,
			UiElement cuiElement, bool needUpdate = false)
		{
			var element = new CuiElement()
			{
				Name = EditingLayerPageEditor + $".Selection.Element.{elementIndex}.Panel" + ".Title",
				Parent = EditingLayerPageEditor + $".Selection.Element.{elementIndex}.Panel",
				Components =
				{
					new CuiInputFieldComponent()
					{
						Text = $"{cuiElement.Name}",
						Align = TextAnchor.MiddleLeft,
						Font = "robotocondensed-bold.ttf",
						FontSize = 12,
						Color = HexToCuiColor("#E2DBD3", 90),
						ReadOnly = true
					},
					new CuiRectTransformComponent()
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "20 0", OffsetMax = "-85 0"
					}
				}
			};

			if (needUpdate) element.Update = true;

			container.Add(element);
		}

		private void PositionFieldElementUI(CuiElementContainer container, InterfacePosition interfacePos)
		{
			container.Add(new CuiPanel
				{
					Image = {Color = "0 0 0 0"},
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1"
					}
				}, EditingLayerElementEditor + ".Selection.RectTransform.Position",
				EditingLayerElementEditor + ".Selection.RectTransform.Position.Content",
				EditingLayerElementEditor + ".Selection.RectTransform.Position.Content");

			container.Add(new CuiLabel()
			{
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "1 1",
					OffsetMin = "0 58", OffsetMax = "0 0"
				},
				Text =
				{
					Text = "POSITION",
					Align = TextAnchor.MiddleLeft,
					FontSize = 20,
					Font = "robotocondensed-bold.ttf",
					Color = HexToCuiColor("#E2DBD3", 90)
				}
			}, EditingLayerElementEditor + ".Selection.RectTransform.Position.Content");

			#region Position.Value

			container.Add(new CuiElement()
			{
				Parent = EditingLayerElementEditor + ".Selection.RectTransform.Position.Content",
				Name = EditingLayerElementEditor + ".Selection.RectTransform.Position.Content.Value",
				Components =
				{
					new CuiRawImageComponent()
					{
						Png = GetImage(interfacePos.GetAnchorImage())
					},
					new CuiRectTransformComponent()
					{
						AnchorMin = "0 0", AnchorMax = "1 0",
						OffsetMin = "0 0", OffsetMax = "0 58"
					}
				}
			});

			container.Add(new CuiElement()
			{
				Parent = EditingLayerElementEditor + ".Selection.RectTransform.Position.Content.Value",
				Components =
				{
					new CuiButtonComponent()
					{
						Command = $"{CmdMainConsole} edit_element rect_transform start",
						Color = "0 0 0 0",
					},
					new CuiRectTransformComponent() {AnchorMin = "0 0", AnchorMax = "1 1"}
				}
			});

			#endregion
		}

		private static void FieldElementUI(CuiElementContainer container,
			string targetFieldLayer,
			FieldInfo targetField,
			object fieldValue)
		{
			container.Add(new CuiPanel()
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
				Image = {Color = "0 0 0 0"}
			}, targetFieldLayer + ".Background", targetFieldLayer, targetFieldLayer);

			if (fieldValue is bool boolValue)
			{
				container.Add(new CuiLabel()
				{
					RectTransform =
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "5 0", OffsetMax = "0 0"
					},
					Text =
					{
						Text = $"{targetField.GetFieldTitle()}",
						Align = TextAnchor.MiddleLeft,
						FontSize = 20,
						Font = "robotocondensed-bold.ttf",
						Color = HexToCuiColor("#E2DBD3", 90)
					}
				}, targetFieldLayer);

				container.Add(new CuiButton()
				{
					RectTransform =
					{
						AnchorMin = "1 0.5",
						AnchorMax = "1 0.5",
						OffsetMin = "-50 -25", OffsetMax = "0 25"
					},
					Text =
					{
						Text = boolValue ? "✔" : string.Empty,
						Font = "robotocondensed-bold.ttf",
						FontSize = 14,
						Align = TextAnchor.MiddleCenter,
						Color = HexToCuiColor("#E2DBD3", 90)
					},
					Button =
					{
						Color = HexToCuiColor("#4D4D4D", 50),
						Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
						Command =
							$"{CmdMainConsole} edit_element field {targetField.Name} {targetFieldLayer} {!boolValue}",
					},
				}, targetFieldLayer);
			}
			else
			{
				container.Add(new CuiLabel()
				{
					RectTransform =
					{
						AnchorMin = "0 1", AnchorMax = "1 1",
						OffsetMin = "0 -40", OffsetMax = "0 0"
					},
					Text =
					{
						Text = $"{targetField.GetFieldTitle()}",
						Align = TextAnchor.MiddleLeft,
						FontSize = 20,
						Font = "robotocondensed-bold.ttf",
						Color = HexToCuiColor("#E2DBD3", 90)
					}
				}, targetFieldLayer);

				#region Value

				if (fieldValue is IColor colorValue)
				{
					container.Add(new CuiPanel()
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 0",
							OffsetMin = "0 0", OffsetMax = "0 42"
						},
						Image =
						{
							Color = colorValue.Get()
						}
					}, targetFieldLayer, targetFieldLayer + ".Value");

					container.Add(new CuiPanel()
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "0.6 1",
							OffsetMin = "0 0", OffsetMax = "0 0"
						},
						Image =
						{
							Color = HexToCuiColor("#000000", 75),
							Material = "assets/content/ui/uibackgroundblur.mat"
						}
					}, targetFieldLayer + ".Value", targetFieldLayer + ".Value.Color");

					container.Add(new CuiElement()
					{
						Parent = targetFieldLayer + ".Value.Color",
						Components =
						{
							new CuiRawImageComponent()
							{
								Png = Instance.GetImage("ServerPanel_Editor_Select")
							},
							new CuiRectTransformComponent()
							{
								AnchorMin = "0 0.5", AnchorMax = "0 0.5",
								OffsetMin = "12 -9", OffsetMax = "30 9"
							}
						}
					});
					container.Add(new CuiElement()
					{
						Parent = targetFieldLayer + ".Value.Color",
						Components =
						{
							new CuiTextComponent()
							{
								Text = $"{colorValue.Hex}",
								Align = TextAnchor.MiddleLeft,
								Font = "robotocondensed-bold.ttf",
								FontSize = 16,
								Color = HexToCuiColor("#E2DBD3", 90)
							},
							new CuiRectTransformComponent()
							{
								AnchorMin = "0 0", AnchorMax = "1 1",
								OffsetMin = "35 0", OffsetMax = "0 0"
							}
						}
					});

					container.Add(new CuiElement()
					{
						Parent = targetFieldLayer + ".Value.Color",
						Components =
						{
							new CuiButtonComponent()
							{
								Color = "0 0 0 0",
								Command =
									$"{CmdMainConsole} edit_element color start {targetField.Name} {targetFieldLayer}"
							},
							new CuiRectTransformComponent()
							{
								AnchorMin = "0 0", AnchorMax = "1 1"
							}
						}
					});
				}
				else if (targetField.FieldType.IsEnum)
				{
					var fieldValueStr = fieldValue?.ToString();

					container.Add(new CuiPanel()
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 0",
							OffsetMin = "0 0", OffsetMax = "0 42"
						},
						Image =
						{
							Color = HexToCuiColor("#000000", 80)
						}
					}, targetFieldLayer, targetFieldLayer + ".Value");

					container.Add(new CuiElement()
					{
						Parent = targetFieldLayer + ".Value",
						Components =
						{
							new CuiTextComponent()
							{
								FontSize = 16,
								Font = "robotocondensed-bold.ttf",
								Align = TextAnchor.MiddleCenter,
								Color = HexToCuiColor("#E2DBD3", 90),
								Text = $"{fieldValueStr}",
							},
							new CuiRectTransformComponent() {AnchorMin = "0 0", AnchorMax = "1 1"}
						}
					});

					#region Prev

					container.Add(new CuiButton()
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "0 1",
							OffsetMin = "0 0", OffsetMax = "42 0"
						},
						Text =
						{
							Text = "<",
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-bold.ttf",
							FontSize = 20,
							Color = HexToCuiColor("#E2DBD3", 90)
						},
						Button =
						{
							Command = $"{CmdMainConsole} edit_element field {targetField.Name} {targetFieldLayer} prev",
							Color = HexToCuiColor("#4D4D4D", 50)
						}
					}, targetFieldLayer + ".Value");

					#endregion

					#region Next

					container.Add(new CuiButton()
					{
						RectTransform =
						{
							AnchorMin = "1 0", AnchorMax = "1 1",
							OffsetMin = "-42 0", OffsetMax = "0 0"
						},
						Text =
						{
							Text = ">",
							Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-bold.ttf",
							FontSize = 20,
							Color = HexToCuiColor("#E2DBD3", 90)
						},
						Button =
						{
							Command = $"{CmdMainConsole} edit_element field {targetField.Name} {targetFieldLayer} next",
							Color = HexToCuiColor("#4D4D4D", 50)
						}
					}, targetFieldLayer + ".Value");

					#endregion
				}
				else
				{
					var fieldValueStr = fieldValue?.ToString();

					container.Add(new CuiPanel()
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 0",
							OffsetMin = "0 0", OffsetMax = "0 42"
						},
						Image =
						{
							Color = HexToCuiColor("#000000", 80)
						}
					}, targetFieldLayer, targetFieldLayer + ".Value");

					container.Add(new CuiElement()
					{
						Parent = targetFieldLayer + ".Value",
						Components =
						{
							new CuiInputFieldComponent()
							{
								FontSize = 16,
								Font = "robotocondensed-bold.ttf",
								Align = TextAnchor.MiddleCenter,
								Command = $"{CmdMainConsole} edit_element field {targetField.Name} {targetFieldLayer}",
								Color = HexToCuiColor("#E2DBD3", 90),
								Text = $"{fieldValueStr}",
								NeedsKeyboard = true
							},
							new CuiRectTransformComponent() {AnchorMin = "0 0", AnchorMax = "1 1"}
						}
					});

					#region Number

					if (double.TryParse(fieldValueStr, out var numberValue))
					{
						var stepSize = 1f;
						if (targetField.Name.Contains("Anchor"))
							stepSize = 0.1f;

						#region Minus

						container.Add(new CuiButton()
						{
							RectTransform =
							{
								AnchorMin = "0 0", AnchorMax = "0 1",
								OffsetMin = "0 0", OffsetMax = "42 0"
							},
							Text =
							{
								Text = "-",
								Align = TextAnchor.MiddleCenter,
								Font = "robotocondensed-bold.ttf",
								FontSize = 20,
								Color = HexToCuiColor("#E2DBD3", 90)
							},
							Button =
							{
								Command =
									$"{CmdMainConsole} edit_element field {targetField.Name} {targetFieldLayer} {numberValue - stepSize}",
								Color = HexToCuiColor("#4D4D4D", 50)
							}
						}, targetFieldLayer + ".Value");

						#endregion

						#region Add

						container.Add(new CuiButton()
						{
							RectTransform =
							{
								AnchorMin = "1 0", AnchorMax = "1 1",
								OffsetMin = "-42 0", OffsetMax = "0 0"
							},
							Text =
							{
								Text = "+",
								Align = TextAnchor.MiddleCenter,
								Font = "robotocondensed-bold.ttf",
								FontSize = 20,
								Color = HexToCuiColor("#E2DBD3", 90)
							},
							Button =
							{
								Command =
									$"{CmdMainConsole} edit_element field {targetField.Name} {targetFieldLayer} {numberValue + stepSize}",
								Color = HexToCuiColor("#4D4D4D", 50)
							}
						}, targetFieldLayer + ".Value");

						#endregion
					}

					#endregion
				}

				#endregion
			}
		}

		private static void CreateOutline(CuiElementContainer container, string outlineColor, int outlineSize,
			string outlineParent)
		{
			#region Outline (1)

			container.Add(new CuiPanel
			{
				Image = {Color = outlineColor},
				RectTransform =
					{AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"0 -{outlineSize}", OffsetMax = "0 0"}
			}, outlineParent);

			#endregion Outline (1)

			#region Outline (2)

			container.Add(new CuiPanel
			{
				Image = {Color = outlineColor},
				RectTransform =
					{AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 0", OffsetMax = $"0 {outlineSize}"}
			}, outlineParent);

			#endregion Outline (2)

			#region Outline (3)

			container.Add(new CuiPanel
			{
				Image = {Color = outlineColor},
				RectTransform =
				{
					AnchorMin = "0 0", AnchorMax = "0 1", OffsetMin = "0 4", OffsetMax = $"{outlineSize} -{outlineSize}"
				}
			}, outlineParent);

			#endregion Outline (3)

			#region Outline (4)

			container.Add(new CuiPanel
			{
				Image = {Color = outlineColor},
				RectTransform =
				{
					AnchorMin = "1 0", AnchorMax = "1 1", OffsetMin = $"-{outlineSize} {outlineSize}",
					OffsetMax = $"0 -{outlineSize}"
				}
			}, outlineParent);

			#endregion Outline (4)
		}

		private static void ShowCloseButtonUI(CuiElementContainer container,
			string closeButtonParent,
			string closeButtonName,
			string closeLayer = "",
			string commandOnClose = "",
			string closeButtonAnchorMin = "1 1",
			string closeButtonAnchorMax = "1 1",
			string closeButtonOffsetMin = "-40 -40",
			string closeButtonOffsetMax = "0 0")
		{
			container.Add(new CuiPanel
			{
				Image =
				{
					Color = HexToCuiColor("#E44028")
				},
				RectTransform =
				{
					AnchorMin = closeButtonAnchorMin,
					AnchorMax = closeButtonAnchorMax,
					OffsetMin = closeButtonOffsetMin,
					OffsetMax = closeButtonOffsetMax
				}
			}, closeButtonParent, closeButtonName);

			container.Add(new CuiElement()
			{
				Parent = closeButtonName,
				Components =
				{
					new CuiImageComponent()
					{
						Sprite = "assets/icons/close.png",
						Color = HexToCuiColor("#E2DBD3"),
					},
					new CuiRectTransformComponent()
					{
						AnchorMin = "0 0", AnchorMax = "1 1",
						OffsetMin = "10 10", OffsetMax = "-10 -10"
					}
				}
			});

			container.Add(new CuiElement()
			{
				Parent = closeButtonName,
				Components =
				{
					new CuiButtonComponent()
					{
						Color = "0 0 0 0",
						Close = closeLayer,
						Command = commandOnClose
					},
					new CuiRectTransformComponent()
					{
						AnchorMin = "0 0", AnchorMax = "1 1"
					}
				}
			});
		}

		private static void UpdateUI(BasePlayer player, Action<CuiElementContainer> callback = null)
		{
			var container = new CuiElementContainer();
			callback?.Invoke(container);
			CuiHelper.AddUi(player, container);
		}

		#endregion UI.Components

		#endregion Interface

		#region Utils

		private void StartShowMenu(BasePlayer player, MenuCategory category)
		{
			if (!_openedMenus.ContainsKey(player.userID))
				_openedMenus.TryAdd(player.userID, new OpenedMenu(player, category));

			ShowMenuUI(player);
		}

		#region Opened Menu

		private Dictionary<ulong, OpenedMenu> _openedMenus = new();

		private class OpenedMenu
		{
			#region Fields

			public BasePlayer Player;

			public int SelectedCategory;

			public int PageIndex;

			private Timer updateTimer = null;

			#endregion

			#region Initialization

			public OpenedMenu(BasePlayer player, MenuCategory targetCategory)
			{
				Player = player;

				SelectedCategory = targetCategory.ID;

				PageIndex = 0;

				if (_headerFieldsData.needToUpdate)
					updateTimer = Instance?.timer.Every(1f, UpdateHeader);
			}

			#endregion

			#region Public Methods

			public bool isEditMode = false;

			public void OnChangeEditMode()
			{
				if (!CanPlayerEdit(Player)) return;

				isEditMode = !isEditMode;
			}

			public void OnSelectCategory(int category)
			{
				SelectedCategory = category;

				PageIndex = 0;
			}

			public void OnSelectPage(int pageIndex)
			{
				PageIndex = pageIndex;
			}

			public int GetLastPage()
			{
				var category = Instance.GetCategoryById(SelectedCategory);
				if (category.Pages.Count == 0) return 0;

				return category.Pages.Count - 1;
			}

			public void UpdateContent()
			{
				UpdateUI(Player,
					container =>
						_templateData.ShowContentUI(Player, container, $"{CmdMainConsole} menu page"));
			}

			#endregion

			#region Private Methods

			private void UpdateHeader()
			{
				if (Player != null)
				{
					_templateData?.ShowUpdateHeaderUI(Player);
				}
			}

			#endregion

			#region Destroy

			public void OnDestroy()
			{
				updateTimer?.Destroy();
			}

			#endregion
		}

		private static OpenedMenu GetOpenedMenu(ulong player)
		{
			return Instance._openedMenus.GetValueOrDefault(player);
		}

		private static bool TryGetOpenedMenu(ulong player, out OpenedMenu openedMenu)
		{
			return Instance._openedMenus.TryGetValue(player, out openedMenu);
		}

		private void RemoveOpenedMenu(ulong player)
		{
			if (_openedMenus.TryGetValue(player, out var menu))
				menu.OnDestroy();

			_openedMenus.Remove(player);
		}

		#endregion

		#region Update Fields

		private void LoadUpdateFields()
		{
			var dict = new Dictionary<string, Func<BasePlayer, string>>
			{
				{"{online_players}", GetOnlinePlayers},
				{"{max_players}", GetMaxPlayers},
				{"{player_kills}", GetPlayerKills},
				{"{player_deaths}", GetPlayerDeaths},
				{"{player_username}", GetPlayerUsername},
				{"{player_avatar}", GetPlayerAvatar}
			};

			_config.EconomyFields.ForEach(economyField =>
			{
				if (!economyField.Enabled)
					return;

				if (dict.ContainsKey(economyField.UpdateKey))
				{
					PrintError($"{economyField.UpdateKey} already defined!");
					return;
				}

				dict.Add(economyField.UpdateKey, player => economyField.Economy.ShowBalance(player).ToString());
			});

			_headerUpdateFields = dict;
		}

		private string FormatUpdateField(BasePlayer player, string updateField)
		{
			foreach (var updateInfo in _headerUpdateFields)
				updateField = updateField.Replace(updateInfo.Key, updateInfo.Value(player));

			return updateField;
		}

		#region Actions

		private string GetOnlinePlayers(BasePlayer player)
		{
			return BasePlayer.activePlayerList.Count.ToString();
		}

		private string GetMaxPlayers(BasePlayer player)
		{
			return ConVar.Server.maxplayers.ToString();
		}

		private string GetPlayerUsername(BasePlayer player)
		{
			return player.displayName;
		}

		private string GetPlayerAvatar(BasePlayer player)
		{
			return $"avatar_{player.UserIDString}";
		}

		private string GetPlayerKills(BasePlayer player)
		{
			if (KillRecords != null) return Convert.ToString(KillRecords.Call("GetKillRecord", player.UserIDString, "baseplayer"));
			if (Statistics != null) return Convert.ToString(Statistics.Call("GetStatsValue", player.userID.Get(), "kills"));

			return 0.ToString();
		}

		private string GetPlayerDeaths(BasePlayer player)
		{
			if (KillRecords != null) return Convert.ToString(KillRecords.Call("GetKillRecord", player.UserIDString, "death"));

			if (Statistics != null) return Convert.ToString(Statistics.Call("GetStatsValue", player.userID.Get(), "deaths"));

			return 0.ToString();
		}

		#endregion

		#endregion

		#region Editing

		private List<(string FlagPath, string LangKey, string LangName)> _langList = new()
		{
			("assets/icons/flags/af.png", "af", "Afrikaans"),
			("assets/icons/flags/ar.png", "ar", "العربية"),
			("assets/icons/flags/ca.png", "ca", "Català"),
			("assets/icons/flags/cs.png", "cs", "Čeština"),
			("assets/icons/flags/da.png", "da", "Dansk"),
			("assets/icons/flags/de.png", "de", "Deutsch"),
			("assets/icons/flags/el.png", "el", "Ελληνικά"),
			("assets/icons/flags/en-pt.png", "en-PT", "Portuguese (Portugal)"),
			("assets/icons/flags/en.png", "en", "English"),
			("assets/icons/flags/es-es.png", "es-ES", "Español (España)"),
			("assets/icons/flags/fi.png", "fi", "Suomi"),
			("assets/icons/flags/fr.png", "fr", "Français"),
			("assets/icons/flags/he.png", "he", "עברית"),
			("assets/icons/flags/hu.png", "hu", "Magyar"),
			("assets/icons/flags/it.png", "it", "Italiano"),
			("assets/icons/flags/ja.png", "ja", "日本語"),
			("assets/icons/flags/ko.png", "ko", "한국어"),
			("assets/icons/flags/nl.png", "nl", "Nederlands"),
			("assets/icons/flags/no.png", "no", "Norsk"),
			("assets/icons/flags/pl.png", "pl", "Polski"),
			("assets/icons/flags/pt-br.png", "pt-BR", "Português (Brasil)"),
			("assets/icons/flags/pt-pt.png", "pt-PT", "Português (Portugal)"),
			("assets/icons/flags/ro.png", "ro", "Română"),
			("assets/icons/flags/ru.png", "ru", "Русский"),
			("assets/icons/flags/sr.png", "sr", "Српски"),
			("assets/icons/flags/sv-se.png", "sv-SE", "Svenska"),
			("assets/icons/flags/tr.png", "tr", "Türkçe"),
			("assets/icons/flags/uk.png", "uk", "Українська"),
			("assets/icons/flags/vi.png", "vi", "Tiếng Việt"),
			("assets/icons/flags/zh-cn.png", "zh-CN", "中文 (简体)"),
			("assets/icons/flags/zh-tw.png", "zh-TW", "中文 (繁體)")
		};

		#region Edit Page

		private Dictionary<ulong, EditPageData> editPages = new();

		private class EditPageData
		{
			#region Page

			public ulong playerID;

			public int Category;

			public int Page;

			public CategoryPage categoryPage;

			public static EditPageData Create(BasePlayer player, int categoryID, int page)
			{
				var data = new EditPageData()
				{
					playerID = player.userID,
					Category = categoryID,
					Page = page,
					categoryPage = Instance.GetCategoryById(categoryID).Pages[page]
				};

				Instance?.editPages.TryAdd(player.userID, data);

				return data;
			}

			public static EditPageData Get(ulong playerID)
			{
				return Instance?.editPages.TryGetValue(playerID, out var data) == true ? data : null;
			}

			public void Save()
			{
				var category = Instance?.GetCategoryById(Category);
				if (category != null)
					category.Pages[Page] = categoryPage;

				Instance?.editPages?.Remove(playerID);

				Instance?.SaveData();
			}

			#endregion

			#region Edit Element

			public UiElement editingElement = null;

			public string editingElementParent, editingElementName;

			public int elementIndex;

			public bool StartEditElement(int elementID, string parent)
			{
				elementIndex = elementID;

				editingElementParent = parent;

				if (elementID >= 0 && elementID < categoryPage.Elements.Count)
				{
					editingElement = categoryPage.Elements[elementID];

					editingElementName = editingElement.Name;
				}

				return editingElement != null;
			}

			public void EndEditElement(bool cancel = false)
			{
				if (cancel)
				{
					editingElement = null;
					return;
				}

				categoryPage.Elements[elementIndex] = editingElement;
			}

			public void UpdateEditElement(ref CuiElementContainer container, BasePlayer player, bool isRename = false)
			{
				if (isRename)
					editingElement.Get(ref container, player, editingElementParent, editingElement.Name,
						editingElementName);
				else
					editingElement.Get(ref container, player, editingElementParent, editingElement.Name,
						needUpdate: true);
			}


			public void OnEditElementSave()
			{
				Instance?.SaveCategoriesData();
			}

			public (UiElement uiElement, string parent) OnEditElementStartEdit()
			{
				return (editingElement, editingElementParent);
			}

			public void OnEditElementStopEdit(UiElement uiElement)
			{
				categoryPage.Elements[elementIndex] = uiElement;
			}

			#endregion

			#region Edit Text

			public void OnStartTextEditing()
			{
			}

			public void OnStopTextEditing()
			{
				if (BasePlayer.TryFindByID(playerID, out var player))
					UpdateUI(player, container => UpdateEditElement(ref container, player));
			}

			#endregion Edit Text
		}

		#endregion

		#region Edit Header Fields

		private Dictionary<ulong, EditHeaderFieldsData> editHeaderFields = new();

		private class EditHeaderFieldsData
		{
			#region Page

			public ulong playerID;

			public List<HeaderFieldUI> HeaderFields = new();

			public static void Create(BasePlayer player)
			{
				var data = new EditHeaderFieldsData()
				{
					playerID = player.userID,
					HeaderFields = _headerFieldsData.Fields
				};

				Instance?.editHeaderFields.TryAdd(player.userID, data);
			}

			public static EditHeaderFieldsData Get(ulong playerID)
			{
				return Instance?.editHeaderFields.TryGetValue(playerID, out var data) == true ? data : null;
			}

			public void Save()
			{
				Instance?.editHeaderFields?.Remove(playerID);

				Instance?.SaveHeaderFieldsData();

				Instance?.LoadHeaderFieldsDataCache();
			}

			#endregion

			#region Edit Element

			public HeaderFieldUI editingElement = null;

			public string editingElementParent, editingElementName;

			public int elementIndex;

			public bool StartEditElement(int elementID, string parent)
			{
				editingElement = null;
				editingElementName = null;

				elementIndex = elementID;

				editingElementParent = parent;

				if (elementID >= 0 && elementID < HeaderFields.Count)
				{
					editingElement = HeaderFields[elementID];

					editingElementName = editingElement.Name;
				}

				return editingElement != null;
			}

			public void EndEditElement(bool cancel = false)
			{
				if (cancel)
				{
					editingElement = null;
					return;
				}

				HeaderFields[elementIndex] = editingElement;

				editingElement = null;
				editingElementParent = null;
				editingElementName = null;
				elementIndex = default;
			}

			public void UpdateEditElement(ref CuiElementContainer container, BasePlayer player, bool isRename = false)
			{
				if (isRename)
					editingElement.Get(ref container, player, editingElementParent, editingElement.Name,
						editingElementName);
				else
					editingElement.Get(ref container, player, editingElementParent, editingElement.Name,
						needUpdate: true);
			}

			public void OnEditElementSave()
			{
				Instance?.SaveHeaderFieldsData();
			}

			public (UiElement uiElement, string parent) OnEditElementStartEdit()
			{
				return (editingElement, editingElementParent);
			}

			public void OnEditElementStopEdit(UiElement uiElement)
			{
				HeaderFields[elementIndex] = new HeaderFieldUI(uiElement, editingElement.NeedToUpdate);
			}

			#endregion

			#region Edit Text

			public void OnStartTextEditing()
			{
			}

			public void OnStopTextEditing()
			{
				Instance?.SaveHeaderFieldsData();

				Instance?.LoadHeaderFieldsDataCache();

				if (BasePlayer.TryFindByID(playerID, out var player))
					UpdateUI(player, container => _templateData?.UpdateGlobalHeaderUI(player, container));
			}

			#endregion Edit Text
		}

		#endregion

		#region Edit Category

		private Dictionary<ulong, EditCategoryData> editMenuCategories = new();

		private class EditCategoryData
		{
			#region Fields

			public ulong playerID;

			public int MenuCategoryID;

			public MenuCategory menuCategory;

			public bool NeedCreate = false;

			#endregion

			#region Public Methods

			public static void Create(BasePlayer player, int menuCategoryID, bool needCreate = false)
			{
				var targetCategory = needCreate
					? new MenuCategory
					{
						ID = Instance.GetUniqueCategoryID(),
						Enabled = true,
						Permission = string.Empty,
						Title = "New Category",
						ChatBtn = false,
						ShowPages = false,
						Commands = new[]
						{
							"test_command_123"
						},
						Icon = string.Empty,
						Pages = new List<CategoryPage>
						{
							new()
							{
								Title = string.Empty,
								Command = string.Empty,
								Type = CategoryPage.PageType.UI,
								PluginName = string.Empty,
								PluginHook = string.Empty,
								Elements = new List<UiElement>
								{
									UiElement.CreatePanel(
										InterfacePosition.CreatePosition(0.5f, 0.5f, 0.5f, 0.5f, -50, -50, 50, 50),
										IColor.CreateWhite())
								}
							}
						}
					}
					: Instance?.GetCategoryById(menuCategoryID);

				if (targetCategory == null)
				{
					Instance?.PrintError($"Error: Can't find category with id {menuCategoryID}");
					return;
				}

				var data = new EditCategoryData()
				{
					playerID = player.userID,
					MenuCategoryID = menuCategoryID,
					menuCategory = targetCategory,
					NeedCreate = needCreate
				};

				Instance?.editMenuCategories?.TryAdd(player.userID, data);
			}

			public static EditCategoryData Get(ulong playerID)
			{
				return Instance?.editMenuCategories?.TryGetValue(playerID, out var data) == true ? data : null;
			}

			public static bool Remove(ulong playerID)
			{
				return Instance?.editMenuCategories?.Remove(playerID) ?? true;
			}

			public void Save()
			{
				if (NeedCreate)
				{
					_categoriesData.Categories.Add(menuCategory);
				}
				else
				{
					var targetIndex = _categoriesData.Categories.FindIndex(x => x.ID == MenuCategoryID);
					_categoriesData.Categories[targetIndex] = menuCategory;
				}
				
				Remove(playerID);

				Instance?.LoadCategories();
				
				Instance?.SaveData();
			}

			public void Remove()
			{
				if (!NeedCreate)
					_categoriesData?.Categories?.RemoveAll(x => x.ID == MenuCategoryID);

				Remove(playerID);
				
				Instance?.LoadCategories();
				
				Instance?.SaveData();
			}

			#endregion Public Methods

			#region Array

			public object[] editableArray;

			public string editableArrayName;

			public void StartEditArray(object[] targetArray, string fieldName)
			{
				editableArray = targetArray;
				editableArrayName = fieldName;
			}

			public void StopEditArray()
			{
				editableArray = null;
				editableArrayName = null;
			}

			public object[] GetEditableArrayValues()
			{
				return editableArray;
			}

			#endregion
		}

		#endregion Edit Category

		#region Edit UI Element

		private Dictionary<ulong, EditUiElementData> editUiElement = new();

		private class EditUiElementData
		{
			#region Fields

			public ulong playerID;

			public int elementIndex;

			public Action OnSave, OnStartTextEditing, OnStopTextEditing;

			public Func<(UiElement uiElement, string parent)> startEditElement;

			public Action<UiElement> onStopEditElement;

			public static void Create(BasePlayer player,
				int elementIndex,
				Action onSave,
				Func<(UiElement uiElement, string parent)> startEditElement,
				Action<UiElement> stopEditElement,
				Action onStartTextEditing = null,
				Action onStopTextEditing = null)
			{
				var data = new EditUiElementData()
				{
					playerID = player.userID,
					elementIndex = elementIndex,
					OnSave = onSave,
					startEditElement = startEditElement,
					onStopEditElement = stopEditElement,
					OnStartTextEditing = onStartTextEditing,
					OnStopTextEditing = onStopTextEditing,
				};

				data.StartEditElement();

				Instance?.editUiElement.TryAdd(player.userID, data);
			}

			public static EditUiElementData Get(ulong playerID)
			{
				return Instance?.editUiElement.TryGetValue(playerID, out var data) == true ? data : null;
			}
			
			public static void Remove(ulong playerID)
			{
				Instance?.editUiElement?.Remove(playerID);
			}

			public void Save()
			{
				OnSave?.Invoke();

				Remove(playerID);
			}

			#endregion

			#region Edit Element

			public UiElement editingElement = null;

			public string editingElementParent, editingElementName;

			public void StartEditElement()
			{
				var targetElement = startEditElement?.Invoke();
				if (targetElement == null)
				{
					return;
				}

				editingElement = targetElement.Value.uiElement;
				editingElementParent = targetElement.Value.parent;

				editingElementName = editingElement.Name;
			}

			public void EndEditElement(bool cancel = false)
			{
				if (cancel)
				{
					editingElement = null;
					return;
				}

				onStopEditElement?.Invoke(editingElement);

				Save();
			}
			
			public void UpdateEditElement(ref CuiElementContainer container, BasePlayer player, bool isRename = false, 
				bool needAddImage = false)
			{
				if (needAddImage && editingElement.Type == CuiElementType.Image && editingElement.TryGetImage(out var image))
					Instance?.AddImage(image, image);

				editingElement.Get(ref container, player, editingElementParent, editingElement.Name,
					editingElementName);
			}

			#endregion

			#region Edit Text

			public bool isTextEditing = false;

			public void StartTextEditing()
			{
				isTextEditing = true;

				OnStartTextEditing?.Invoke();
			}

			public void StopTextEditing()
			{
				isTextEditing = false;

				OnStopTextEditing?.Invoke();
			}

			private string _targetLang;

			public void SelectLang(string langKey)
			{
				_targetLang = langKey;
			}

			public bool IsSelectedLang(string langKey)
			{
				if (string.IsNullOrWhiteSpace(_targetLang) || _targetLang == "en")
					return langKey == "en";

				return langKey == _targetLang;
			}

			public List<string> GetText()
			{
				if (string.IsNullOrWhiteSpace(_targetLang) || _targetLang == "en")
				{
					return editingElement.Text;
				}

				if (_localizationData.Localization.Elements.TryGetValue(editingElement.Name,
					    out var elementLocalization) &&
				    elementLocalization.Messages.TryGetValue(_targetLang, out var langValue))
				{
					return langValue.Text;
				}

				return editingElement.Text;
			}

			public void SaveTextForLang(List<string> text)
			{
				if (string.IsNullOrWhiteSpace(_targetLang) || _targetLang == "en")
				{
					editingElement.Text = text;
				}
				else
				{
					if (_localizationData.Localization.Elements.TryGetValue(editingElement.Name,
						    out var elementLocalization))
					{
						elementLocalization.Messages[_targetLang] = new LocalizationSettings.LocalizationInfo()
						{
							Text = text
						};
					}
					else
					{
						_localizationData.Localization.Elements[editingElement.Name] =
							new LocalizationSettings.ElementLocalization
							{
								Messages = new Dictionary<string, LocalizationSettings.LocalizationInfo>
								{
									[_targetLang] = new()
									{
										Text = text
									}
								}
							};
					}
				}
			}

			public bool HasLang(string langKey)
			{
				if (string.IsNullOrWhiteSpace(langKey) || langKey == "en")
					return true;

				return _localizationData.Localization.Elements.TryGetValue(editingElement.Name,
					       out var elementLocalization) &&
				       elementLocalization.Messages.ContainsKey(langKey);
			}

			public void RemoveLang(string langKey)
			{
				if (string.IsNullOrWhiteSpace(langKey) || langKey == "en")
				{
					editingElement.Text = new List<string>();
				}
				else
				{
					if (_localizationData.Localization.Elements.TryGetValue(editingElement.Name,
						    out var elementLocalization))
						elementLocalization.Messages?.Remove(langKey);
				}

				if (_targetLang == langKey)
					SelectLang(default);
			}

			#endregion Edit Text
		}

		#endregion Edit UI Element

		#endregion

		#region Working with Images

		private void AddImage(string url, string fileName, ulong imageId = 0)
		{
#if CARBON
			imageDatabase.Queue(true, new Dictionary<string, string>
			{
				[fileName] = url
			});
#else
			ImageLibrary?.Call("AddImage", url, fileName, imageId);
#endif
		}

		private string GetImage(string name)
		{
#if CARBON
			return imageDatabase.GetImageString(name);
#else
			return Convert.ToString(ImageLibrary?.Call("GetImage", name));
#endif
		}

		private bool HasImage(string name)
		{
#if CARBON
			return Convert.ToBoolean(imageDatabase.HasImage(name));
#else
			return Convert.ToBoolean(ImageLibrary?.Call("HasImage", name));
#endif
		}

		private void LoadImages()
		{
#if CARBON
			imageDatabase = BaseModule.GetModule<ImageDatabaseModule>();
#endif
			_enabledImageLibrary = true;

			var imagesList = new Dictionary<string, string>
			{
				["ServerPanel_Editor_Btn_Remove"] =
					"https://github.com/TheMevent/PluginsStorage/blob/main/Images/ServerPanel/serverpanel-editor-icon-remove.png?raw=true",
				["ServerPanel_Editor_Btn_Edit"] =
					"https://github.com/TheMevent/PluginsStorage/blob/main/Images/ServerPanel/serverpanel-editor-icon-edit.png?raw=true",
				["ServerPanel_Editor_Btn_Up"] =
					"https://github.com/TheMevent/PluginsStorage/blob/main/Images/ServerPanel/serverpanel-editor-icon-up.png?raw=true",
				["ServerPanel_Editor_Btn_Down"] =
					"https://github.com/TheMevent/PluginsStorage/blob/main/Images/ServerPanel/serverpanel-editor-icon-down.png?raw=true",
				["ServerPanel_Editor_Select"] =
					"https://github.com/TheMevent/PluginsStorage/blob/main/Images/ServerPanel/serverpanel-editor-icon-select.png?raw=true",
				["ServerPanel_Editor_EditCategory"] =
					"https://github.com/TheMevent/PluginsStorage/blob/main/Images/ServerPanel/serverpanel-editor-icon-category.png?raw=true",
			};

			foreach (var rectImage in rectToImage.Values)
				RegisterImage(ref imagesList, rectImage, rectImage);

			_templateData.UI?.GetAllUiElements()?.ForEach(uiElement =>
			{
				if (uiElement.TryGetImage(out var img))
				{
					RegisterImage(ref imagesList, img, img);
				}
			});

			_headerFieldsData.Fields?.ForEach(uiElement =>
			{
				if (uiElement.TryGetImage(out var img))
				{
					RegisterImage(ref imagesList, img, img);
				}
			});

			_categoriesData.Categories?.ForEach(category =>
			{
				category.Pages?.ForEach(page =>
				{
					page.Elements?.ForEach(uiElement =>
					{
						if (uiElement.TryGetImage(out var img))
						{
							RegisterImage(ref imagesList, img, img);
						}
					});
				});
			});

#if CARBON
            imageDatabase.Queue(false, imagesList);
#else
			timer.In(1f, () =>
			{
				if (ImageLibrary is not {IsLoaded: true})
				{
					_enabledImageLibrary = false;

					BroadcastILNotInstalled();
					return;
				}

				ImageLibrary?.Call("ImportImageList", Title, imagesList, 0UL, true);
			});
#endif
		}

		private void RegisterImage(ref Dictionary<string, string> images, string name, string image)
		{
			if (string.IsNullOrEmpty(image) || string.IsNullOrEmpty(name)) return;

			images.TryAdd(name, image);
		}

		private void BroadcastILNotInstalled()
		{
			for (var i = 0; i < 5; i++) PrintError("IMAGE LIBRARY IS NOT INSTALLED.");
		}

		#endregion

		#region Server Loading

		private void LoadCategories()
		{
			_categoriesByID.Clear();
			_categoriesByCommand.Clear();

			for (var index = 0; index < _categoriesData.Categories.Count; index++)
			{
				var menuCategory = _categoriesData.Categories[index];

				_categoriesByID[menuCategory.ID] = index;

				foreach (var menuCommand in menuCategory.Commands)
					_categoriesByCommand[menuCommand] = index;
			}
		}

		private void RegisterCommands()
		{
			var openMenuCommands = new HashSet<string>();

			_categoriesData?.Categories?.FindAll(menuCategory => menuCategory.Enabled)?.ForEach(menuCategory =>
			{
				foreach (var menuCommand in menuCategory.Commands)
					openMenuCommands.Add(menuCommand);
			});

			if (openMenuCommands.Count > 0)
				AddCovalenceCommand(openMenuCommands.ToArray(), nameof(CmdOpenMenu));
		}

		private void RegisterPermissions()
		{
			var menuPermissions = new HashSet<string>
			{
				Perm_Edit
			};

			_categoriesData?.Categories?.FindAll(menuCategory => menuCategory.Enabled)
				?.ForEach(menuCategory => menuPermissions.Add(menuCategory.Permission));

			foreach (var perm in menuPermissions)
				if (!permission.PermissionExists(perm))
					permission.RegisterPermission(perm, this);
		}

		#endregion

		#region Categories

		private List<MenuCategory> GetAvailableCategories(ulong player)
		{
			return _categoriesData.Categories.FindAll(category =>
			{
				if (!category.Enabled)
					return false;

				if (!string.IsNullOrEmpty(category.Permission) &&
				    !permission.UserHasPermission(player.ToString(), category.Permission))
					return false;

				return true;
			});
		}

		private MenuCategory GetCategoryById(int categoryID)
		{
			return _categoriesByID.TryGetValue(categoryID, out var categoryIndex)
				? _categoriesData.Categories[categoryIndex]
				: null;
		}

		private MenuCategory GetCategoryByCommand(string categoryName)
		{
			return _categoriesByCommand.TryGetValue(categoryName, out var categoryIndex)
				? _categoriesData.Categories[categoryIndex]
				: null;
		}

		private int GetUniqueCategoryID()
		{
			int categoryID;
			do
			{
				categoryID = Random.Range(int.MinValue, int.MaxValue);
			} while (_categoriesByID.ContainsKey(categoryID));

			return categoryID;
		}

		#endregion

		#region Other Plugins

		private bool IsServerPanelPlayerRaidBlocked(BasePlayer player)
		{
			return Convert.ToBoolean(NoEscape?.Call("IsRaidBlocked", player) ?? false);
		}

		private bool IsServerPanelPlayerCombatBlocked(BasePlayer player)
		{
			return Convert.ToBoolean(NoEscape?.Call("IsCombatBlocked", player) ?? false);
		}

		#endregion

		private static bool CanPlayerEdit(BasePlayer player)
		{
			return player.HasPermission(Perm_Edit);
		}

		private static string HexToCuiColor(string hex, float alpha = 100)
		{
			if (string.IsNullOrEmpty(hex)) hex = "#FFFFFF";

			var str = hex.Trim('#');
			if (str.Length != 6) throw new Exception(hex);
			var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
			var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
			var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

			return $"{(double) r / 255} {(double) g / 255} {(double) b / 255} {alpha / 100f}";
		}

		#endregion Utils

		#region API

		private void API_OnServerPanelCallClose(BasePlayer player)
		{
			if (player == null) return;

			API_OnServerPanelDestroyUI(player);

			API_OnServerPanelClosed(player);
		}

		private void API_OnServerPanelClosed(BasePlayer player)
		{
			if (player == null) return;

			Interface.CallHook("OnServerPanelClosed", player);

			RemoveOpenedMenu(player.userID);
		}

		private static void API_OnServerPanelDestroyUI(BasePlayer player)
		{
			CuiHelper.DestroyUi(player, Layer);
			CuiHelper.DestroyUi(player, EditingLayerModal);
			CuiHelper.DestroyUi(player, EditingLayerModalColorSelector);
			CuiHelper.DestroyUi(player, EditingLayerModalTextEditor);
		}

		public void API_OnServerPanelSetHeaderFields(List<HeaderFieldUI> targetHeaderFields)
		{
			_headerFieldsData.Fields = targetHeaderFields?.ToList();
		}

		public void API_OnServerPanelSetTemplate(UISettings targetUI)
		{
			_templateData.UI = targetUI;
		}

		public void API_OnServerPanelSetCategories(List<MenuCategory> targetCategories)
		{
			if (targetCategories is null) return;

			_categoriesData.Categories = targetCategories?.ToList();
		}

		public void API_OnServerPanelUpdateText(Dictionary<string, string> targetUpdateFields)
		{
			if (targetUpdateFields == null) return;

			_templateData?.UI?.GetAllUiElements()?.ForEach(uiElement =>
			{
				foreach (var (key, val) in targetUpdateFields)
				{
					if (uiElement.Text?.Count > 0)
					{
						var newText = new List<string>();

						uiElement.Text?.ForEach(targetText => newText.Add(targetText.Replace(key, val)));

						uiElement.Text = newText;
					}

					if (!string.IsNullOrWhiteSpace(uiElement.Image))
						uiElement.Image = uiElement.Image.Replace(key, val);
				}
			});

			_headerFieldsData?.Fields?.ForEach(uiElement =>
			{
				foreach (var (key, val) in targetUpdateFields)
				{
					if (uiElement.Text?.Count > 0)
					{
						var newText = new List<string>();

						uiElement.Text?.ForEach(targetText => newText.Add(targetText.Replace(key, val)));

						uiElement.Text = newText;
					}

					if (!string.IsNullOrWhiteSpace(uiElement.Image))
						uiElement.Image = uiElement.Image.Replace(key, val);
				}
			});

			_categoriesData?.Categories?.ForEach(menuCategory =>
			{
				menuCategory?.Pages?.ForEach(page =>
				{
					if (page.Type == CategoryPage.PageType.UI)
					{
						page.Elements?.ForEach(uiElement =>
						{
							foreach (var (key, val) in targetUpdateFields)
							{
								if (uiElement.Text?.Count > 0)
								{
									var newText = new List<string>();

									uiElement.Text?.ForEach(targetText => newText.Add(targetText.Replace(key, val)));

									uiElement.Text = newText;
								}

								if (!string.IsNullOrWhiteSpace(uiElement.Image))
									uiElement.Image = uiElement.Image.Replace(key, val);
							}
						});
					}
				});
			});
		}

		#endregion

		#region Lang

		private const string
			NoILError = "NoILError",
			MsgCantOpenMenuInvalidCommand = "MsgCantOpenMenuInvalidCommand",
			MsgCantOpenMenuBuildingBlock = "MsgCantOpenMenuBuildingBlock",
			MsgCantOpenMenuRaidBlock = "MsgCantOpenMenuRaidBlock",
			MsgCantOpenMenuCombatBlock = "MsgCantOpenMenuCombatBlock";

		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["Economy.Economics.Title"] = "Economics",
				["Economy.Economics.Balance"] = "{0} $",

				["Economy.ServerRewards.Title"] = "Server Rewards",
				["Economy.ServerRewards.Balance"] = "{0} RP",

				["Economy.BankSystem.Title"] = "Bank System",
				["Economy.BankSystem.Balance"] = "{0} $",

				["Economy.IQEconomic.Title"] = "IQEconomic",
				["Economy.IQEconomic.Balance"] = "{0} $",

				["Economy.Scrap.Title"] = "Scrap",
				["Economy.Scrap.Balance"] = "{0} scrap",

				[MsgCantOpenMenuInvalidCommand] =
					"Sorry, you typed the wrong command. Please check the spelling and try again.",
				[MsgCantOpenMenuBuildingBlock] = "You cannot open the menu: you are in a building zone!",
				[MsgCantOpenMenuRaidBlock] = "You can't open the menu: you are raid blocked!",
				[MsgCantOpenMenuCombatBlock] = "You can't open the menu: you are combat blocked!",
				[NoILError] = "The plugin does not work correctly, contact the administrator!",
			}, this);
		}

		private string Msg(string key, string userid = null, params object[] obj) =>
			string.Format(lang.GetMessage(key, this, userid), obj);

		private string Msg(BasePlayer player, string key, params object[] obj)
		{
			return Msg(key, player.UserIDString, obj);
		}

		private void Reply(BasePlayer player, string key, params object[] obj)
		{
			SendReply(player, Msg(key, player.UserIDString, obj));
		}

		private void SendNotify(BasePlayer player, string key, int type, params object[] obj)
		{
			if (_config.UseNotify && (Notify != null || UINotify != null))
				Interface.Oxide.CallHook("SendNotify", player, type, Msg(player, key, obj));
			else
				Reply(player, key, obj);
		}

		#endregion

		#region Testing Functions

#if TESTING
		private static void SayDebug(BasePlayer player, string hook, string message)
		{
			Debug.Log($"[ServerPanel | {hook} | {player.UserIDString}] {message}");
		}

		private static void SayDebug(ulong player, string hook, string message)
		{
			Debug.Log($"[ServerPanel | {hook} | {player}] {message}");
		}

		private static void SayDebug(string message)
		{
			Debug.Log($"[ServerPanel] {message}");
		}
#endif

		#endregion
	}
}

#region Extension Methods

namespace Oxide.Plugins.ServerPanelExtensionMethods
{
	// ReSharper disable ForCanBeConvertedToForeach
	// ReSharper disable LoopCanBeConvertedToQuery
	public static class ExtensionMethods
	{
		internal static Permission perm;

		public static bool IsURL(this string uriName)
		{
			return Uri.TryCreate(uriName, UriKind.Absolute, out var uriResult) &&
			       (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
		}

		public static Enum Next(this Enum input)
		{
			var values = Enum.GetValues(input.GetType());
			var j = Array.IndexOf(values, input) + 1;
			return values.Length == j ? (Enum) values.GetValue(0) : (Enum) values.GetValue(j);
		}

		public static Enum Previous(this Enum input)
		{
			var values = Enum.GetValues(input.GetType());
			var j = Array.IndexOf(values, input) - 1;
			return j == -1 ? (Enum) values.GetValue(values.Length - 1) : (Enum) values.GetValue(j);
		}

		public static float Scale(this float oldValue, float oldMin, float oldMax, float newMin, float newMax)
		{
			var oldRange = oldMax - oldMin;
			var newRange = newMax - newMin;
			var newValue = (oldValue - oldMin) * newRange / oldRange + newMin;

			return newValue;
		}

		public static int Scale(this int oldValue, int oldMin, int oldMax, int newMin, int newMax)
		{
			var oldRange = oldMax - oldMin;
			var newRange = newMax - newMin;
			var newValue = (oldValue - oldMin) * newRange / oldRange + newMin;

			return newValue;
		}

		public static long Scale(this long oldValue, long oldMin, long oldMax, long newMin, long newMax)
		{
			var oldRange = oldMax - oldMin;
			var newRange = newMax - newMin;
			var newValue = (oldValue - oldMin) * newRange / oldRange + newMin;

			return newValue;
		}

		public static bool IsHex(this string s)
		{
			return s.Length == 6 && Regex.IsMatch(s, "^[0-9A-Fa-f]+$");
		}


		public static bool All<T>(this IList<T> a, Func<T, bool> b)
		{
			for (var i = 0; i < a.Count; i++)
				if (!b(a[i]))
					return false;
			return true;
		}

		public static int Average(this IList<int> a)
		{
			if (a.Count == 0) return 0;
			var b = 0;
			for (var i = 0; i < a.Count; i++) b += a[i];
			return b / a.Count;
		}

		public static T ElementAt<T>(this IEnumerable<T> a, int b)
		{
			using var c = a.GetEnumerator();
			while (c.MoveNext())
			{
				if (b == 0) return c.Current;
				b--;
			}

			return default;
		}

		public static bool Exists<T>(this IEnumerable<T> a, Func<T, bool> b = null)
		{
			using var c = a.GetEnumerator();
			while (c.MoveNext())
				if (b == null || b(c.Current))
					return true;

			return false;
		}

		public static T FirstOrDefault<T>(this IEnumerable<T> a, Func<T, bool> b = null)
		{
			using (var c = a.GetEnumerator())
			{
				while (c.MoveNext())
					if (b == null || b(c.Current))
						return c.Current;
			}

			return default;
		}

		public static int RemoveAll<T, V>(this IDictionary<T, V> a, Func<T, V, bool> b)
		{
			var c = new List<T>();
			using (var d = a.GetEnumerator())
			{
				while (d.MoveNext())
					if (b(d.Current.Key, d.Current.Value))
						c.Add(d.Current.Key);
			}

			c.ForEach(e => a.Remove(e));
			return c.Count;
		}

		public static IEnumerable<V> Select<T, V>(this IEnumerable<T> a, Func<T, V> b)
		{
			var c = new List<V>();
			using var d = a.GetEnumerator();
			while (d.MoveNext()) c.Add(b(d.Current));

			return c;
		}

		public static List<TResult> Select<T, TResult>(this List<T> source, Func<T, TResult> selector)
		{
			if (source == null || selector == null) return new List<TResult>();

			var r = new List<TResult>(source.Count);
			for (var i = 0; i < source.Count; i++) r.Add(selector(source[i]));

			return r;
		}

		public static List<T> SkipAndTake<T>(this List<T> source, int skip, int take)
		{
			var index = Mathf.Min(Mathf.Max(skip, 0), source.Count);
			return source.GetRange(index, Mathf.Min(take, source.Count - index));
		}

		public static string[] Skip(this string[] a, int count)
		{
			if (a.Length == 0) return Array.Empty<string>();
			var c = new string[a.Length - count];
			var n = 0;
			for (var i = 0; i < a.Length; i++)
			{
				if (i < count) continue;
				c[n] = a[i];
				n++;
			}

			return c;
		}

		public static List<T> Skip<T>(this IList<T> source, int count)
		{
			if (count < 0)
				count = 0;

			if (source == null || count > source.Count)
				return new List<T>();

			var result = new List<T>(source.Count - count);
			for (var i = count; i < source.Count; i++)
				result.Add(source[i]);
			return result;
		}

		public static Dictionary<T, V> Skip<T, V>(
			this IDictionary<T, V> source,
			int count)
		{
			var result = new Dictionary<T, V>();
			using var iterator = source.GetEnumerator();
			for (var i = 0; i < count; i++)
				if (!iterator.MoveNext())
					break;

			while (iterator.MoveNext()) result.Add(iterator.Current.Key, iterator.Current.Value);

			return result;
		}

		public static List<T> Take<T>(this IList<T> a, int b)
		{
			var c = new List<T>();
			for (var i = 0; i < a.Count; i++)
			{
				if (c.Count == b) break;
				c.Add(a[i]);
			}

			return c;
		}

		public static Dictionary<T, V> Take<T, V>(this IDictionary<T, V> a, int b)
		{
			var c = new Dictionary<T, V>();
			foreach (var f in a)
			{
				if (c.Count == b) break;
				c.Add(f.Key, f.Value);
			}

			return c;
		}

		public static Dictionary<T, V> ToDictionary<S, T, V>(this IEnumerable<S> a, Func<S, T> b, Func<S, V> c)
		{
			var d = new Dictionary<T, V>();
			using var e = a.GetEnumerator();
			while (e.MoveNext()) d[b(e.Current)] = c(e.Current);

			return d;
		}

		public static List<T> ToList<T>(this IEnumerable<T> a)
		{
			var b = new List<T>();

			using var c = a.GetEnumerator();
			while (c.MoveNext()) b.Add(c.Current);

			return b;
		}

		public static T[] ToArray<T>(this IEnumerable<T> a)
		{
			var b = new List<T>();

			using (var c = a.GetEnumerator())
				while (c.MoveNext())
					b.Add(c.Current);

			return b.ToArray();
		}

		public static T[] ToArray<T>(this HashSet<T> source)
		{
			var array = new T[source.Count];

			var index = 0;
			foreach (var item in source)
				array[index++] = item;

			return array;
		}

		public static HashSet<T> ToHashSet<T>(this IEnumerable<T> a)
		{
			return new HashSet<T>(a);
		}

		public static List<T> Where<T>(this List<T> source, Predicate<T> predicate)
		{
			if (source == null)
				return new List<T>();

			if (predicate == null)
				return new List<T>();

			return source.FindAll(predicate);
		}

		public static List<T> Where<T>(this List<T> source, Func<T, int, bool> predicate)
		{
			if (source == null)
				return new List<T>();

			if (predicate == null)
				return new List<T>();

			var r = new List<T>();
			for (var i = 0; i < source.Count; i++)
				if (predicate(source[i], i))
					r.Add(source[i]);
			return r;
		}

		public static List<T> Where<T>(this IEnumerable<T> source, Func<T, bool> predicate)
		{
			var c = new List<T>();

			using (var d = source.GetEnumerator())
			{
				while (d.MoveNext())
					if (predicate(d.Current))
						c.Add(d.Current);
			}

			return c;
		}

		public static List<T> OfType<T>(this IEnumerable<BaseNetworkable> a) where T : BaseEntity
		{
			var b = new List<T>();
			using var c = a.GetEnumerator();
			while (c.MoveNext())
				if (c.Current is T entity)
					b.Add(entity);

			return b;
		}

		public static int Sum<T>(this IList<T> a, Func<T, int> b)
		{
			var c = 0;
			for (var i = 0; i < a.Count; i++)
			{
				var d = b(a[i]);
				if (!float.IsNaN(d)) c += d;
			}

			return c;
		}

		public static T LastOrDefault<T>(this List<T> source)
		{
			if (source == null || source.Count == 0)
				return default;

			return source[^1];
		}

		public static int Count<T>(this List<T> source, Func<T, bool> predicate)
		{
			if (source == null)
				return 0;

			if (predicate == null)
				return 0;

			var count = 0;
			for (var i = 0; i < source.Count; i++)
				checked
				{
					if (predicate(source[i])) count++;
				}

			return count;
		}

		public static TAccumulate Aggregate<TSource, TAccumulate>(this List<TSource> source, TAccumulate seed,
			Func<TAccumulate, TSource, TAccumulate> func)
		{
			if (source == null) throw new Exception("Aggregate: source is null");

			if (func == null) throw new Exception("Aggregate: func is null");

			var result = seed;
			for (var i = 0; i < source.Count; i++) result = func(result, source[i]);
			return result;
		}

		public static int Sum(this IList<int> a)
		{
			var c = 0;
			for (var i = 0; i < a.Count; i++)
			{
				var d = a[i];
				if (!float.IsNaN(d)) c += d;
			}

			return c;
		}

		public static bool HasPermission(this string userID, string b)
		{
			perm ??= Interface.Oxide.GetLibrary<Permission>();
			return !string.IsNullOrEmpty(userID) && (string.IsNullOrEmpty(b) || perm.UserHasPermission(userID, b));
		}

		public static bool HasPermission(this BasePlayer a, string b)
		{
			return a.UserIDString.HasPermission(b);
		}

		public static bool HasPermission(this ulong a, string b)
		{
			return a.ToString().HasPermission(b);
		}

		public static bool IsReallyConnected(this BasePlayer a)
		{
			return a.IsReallyValid() && a.net.connection != null;
		}

		public static bool IsKilled(this BaseNetworkable a)
		{
			return (object) a == null || a.IsDestroyed;
		}

		public static bool IsNull<T>(this T a) where T : class
		{
			return a == null;
		}

		public static bool IsNull(this BasePlayer a)
		{
			return (object) a == null;
		}

		public static bool IsReallyValid(this BaseNetworkable a)
		{
			return !((object) a == null || a.IsDestroyed || a.net == null);
		}

		public static void SafelyKill(this BaseNetworkable a)
		{
			if (a.IsKilled()) return;
			a.Kill();
		}

		public static bool CanCall(this Plugin o)
		{
			return o is {IsLoaded: true};
		}

		public static bool IsInBounds(this OBB o, Vector3 a)
		{
			return o.ClosestPoint(a) == a;
		}

		public static bool IsHuman(this BasePlayer a)
		{
			return !(a.IsNpc || !a.userID.IsSteamId());
		}

		public static BasePlayer ToPlayer(this IPlayer user)
		{
			return user.Object as BasePlayer;
		}

		public static List<TResult> SelectMany<TSource, TResult>(this List<TSource> source,
			Func<TSource, List<TResult>> selector)
		{
			if (source == null || selector == null)
				return new List<TResult>();

			var result = new List<TResult>(source.Count);
			source.ForEach(i => selector(i).ForEach(j => result.Add(j)));
			return result;
		}

		public static IEnumerable<TResult> SelectMany<TSource, TResult>(
			this IEnumerable<TSource> source,
			Func<TSource, IEnumerable<TResult>> selector)
		{
			using var item = source.GetEnumerator();
			while (item.MoveNext())
			{
				using var result = selector(item.Current).GetEnumerator();
				while (result.MoveNext()) yield return result.Current;
			}
		}

		public static int Sum<TSource>(this IEnumerable<TSource> source, Func<TSource, int> selector)
		{
			var sum = 0;

			using var element = source.GetEnumerator();
			while (element.MoveNext()) sum += selector(element.Current);

			return sum;
		}

		public static double Sum<TSource>(this IEnumerable<TSource> source, Func<TSource, double> selector)
		{
			var sum = 0.0;

			using var element = source.GetEnumerator();
			while (element.MoveNext()) sum += selector(element.Current);

			return sum;
		}

		public static bool Any<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
		{
			if (source == null) return false;

			using var element = source.GetEnumerator();
			while (element.MoveNext())
				if (predicate(element.Current))
					return true;

			return false;
		}

		public static string GetFieldTitle<T>(this string field)
		{
			var fieldInfo = typeof(T).GetField(field);
			return fieldInfo == null ? field : GetFieldTitle(fieldInfo);
		}

		public static string GetFieldTitle(this FieldInfo fieldInfo)
		{
			var jsonAttribute = fieldInfo.GetCustomAttribute<JsonPropertyAttribute>();
			return jsonAttribute == null ? string.Empty : jsonAttribute.PropertyName;
		}


		public static bool MoveDown<T>(this List<T> source, T target)
		{
			if (source == null) return false;

			var index = source.LastIndexOf(target);
			if (index > 0 && index < source.Count)
			{
				(source[index], source[index - 1]) = (source[index - 1], source[index]); // Swap

				return true;
			}

			return false;
		}

		public static bool MoveUp<T>(this List<T> source, T target)
		{
			if (source == null) return false;

			var index = source.LastIndexOf(target);
			if (index >= 0 && index < source.Count - 1)
			{
				(source[index], source[index + 1]) = (
					source[index + 1], source[index]); // Swap

				return true;
			}

			return false;
		}

		public static bool MoveDown<T>(this List<T> source, int index)
		{
			if (source == null) return false;

			if (index > 0 && index < source.Count)
			{
				(source[index], source[index - 1]) = (source[index - 1], source[index]); // Swap

				return true;
			}

			return false;
		}

		public static bool MoveUp<T>(this List<T> source, int index)
		{
			if (source == null) return false;

			if (index >= 0 && index < source.Count - 1)
			{
				(source[index], source[index + 1]) = (
					source[index + 1], source[index]); // Swap

				return true;
			}

			return false;
		}
	}
}

#endregion Extension Methods
/* Boosty - https://boosty.to/skulidropek 
Discord - https://discord.gg/k3hXsVua7Q 
Discord The Rust Bay - https://discord.gg/Zq3TVjxKWk  */