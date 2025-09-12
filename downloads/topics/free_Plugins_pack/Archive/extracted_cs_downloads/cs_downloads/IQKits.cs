using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using Object = System.Object;
using Oxide.Core.Plugins;
using System;
using System.Text;
using Pool = Facepunch.Pool;
using UnityEngine.UI;
using Oxide.Game.Rust.Cui;
using System.Linq;
using System.Collections;
using UnityEngine.Networking;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("IQKits", "Mercury", "2.0.0")]
    [Description("Лучшие наборы из всех,которые есть")]
    public class IQKits : RustPlugin
    {
        
        private void PresetRemove(BasePlayer player, String presetName)
        {
	        if (!config.presetsItems.ContainsKey(presetName))
	        {
		        SendChat(GetLang("CHAT_ALERT_REMOVE_PRESET_ERROR_NO_ITEMS", player.UserIDString), player);   
		        return;
	        }
	        
	        config.presetsItems.Remove(presetName);
	        
	        SendChat(GetLang("CHAT_ALERT_REMOVE_PRESET_SUCCESS", player.UserIDString, presetName), player);
	        SaveConfig();
        }
        
        
        
        
        
        private void RemoveKit(BasePlayer player, String categoryName, String presetName)
        {
	        if (!config.categoryKits.TryGetValue(categoryName, out Configuration.CategoryInfo categoryInfo))
	        {
		        SendChat(GetLang("CHAT_ADMIN_COMMAND_KIT_NO_CATEGORY", player.UserIDString), player);
		        return;
	        }
		   		 		  						   					  						  						  		 			  	 	 
	        if (String.IsNullOrWhiteSpace(presetName))
	        {
		        config.categoryKits.Remove(categoryName);
		        SendChat(GetLang("CHAT_ADMIN_COMMAND_KIT_CATEGORY_REMOVE", player.UserIDString, categoryName), player);
		        return;
	        }
	        
	        if (!categoryInfo.kitList.ContainsKey(presetName))
	        {
		        SendChat(GetLang("CHAT_ADMIN_COMMAND_KIT_TRY_REMOVE_NO_PRESET_KIT", player.UserIDString, presetName), player);
		        return;
	        }
		   		 		  						   					  						  						  		 			  	 	 
	        if (config.categoryKits[categoryName].kitList.Count == 1)
	        {
		        config.categoryKits.Remove(categoryName);
		        SendChat(GetLang("CHAT_ADMIN_COMMAND_KIT_CATEGORY_REMOVE", player.UserIDString, categoryName), player);
		        return;
	        }

	        config.categoryKits[categoryName].kitList.Remove(presetName);
	        SendChat(GetLang("CHAT_ADMIN_COMMAND_KIT_REMOVE_PRESET_IN_CATEGORY", player.UserIDString, presetName, categoryName), player);
        }

	    
	    
	    private Boolean IsRank(BasePlayer player, String rankKey)
	    {
		    if (!IQRankSystem) return true;
		    if (String.IsNullOrWhiteSpace(rankKey)) return true;
		    return (Boolean)IQRankSystem?.Call("API_GET_AVAILABILITY_RANK_USER", player.userID.Get(), rankKey);
	    }

        
        private Boolean TryGetKitInfo(BasePlayer player, String categoryKey, String kitName, out CategoryInfo.KitInfo kitValue)
        {
	        kitValue = null;

	        if (!config.presetsItems.ContainsKey(kitName))
		        return false;
	        
	        if (!config.categoryKits.TryGetValue(categoryKey, out Configuration.CategoryInfo categoryConfig) || !categoryConfig.kitList.ContainsKey(kitName))
		        return false;
	        

	        if (!repositoryKits.TryGetValue(player.UserIDString, out Dictionary<String, CategoryInfo> categoryInfo) || 
	            !categoryInfo.TryGetValue(categoryKey, out CategoryInfo categoryValue))
		        return true;

	        categoryValue.playerKitsTaked.TryGetValue(kitName, out kitValue);
	        
	        return true;
        }
        
        private void DrawUI_InfoKit_Panel_InfoKit(BasePlayer player, Configuration.CategoryInfo categoryInfo, List<Configuration.ItemKits> presetItems, String categoryKey, String kitKey)
        {
	        if (_interface == null) return;
            
	        String Interface = InterfaceBuilder.GetInterface("UI_KIT_ITEM_PANEL_INFO_KIT");
	        if (Interface == null) return;

	        if (!categoryInfo.kitList.TryGetValue(kitKey, out Configuration.KitInfo kitInfo))
		        return;
	        
	        String getStatusKit = GetStatusAvaliable(player, categoryKey, kitKey);
	        String statusKit = String.IsNullOrWhiteSpace(getStatusKit) ? GetLang("UI_KIT_AVALIABLE_OPEN", player.UserIDString) : getStatusKit;

	        String descriptionKit = kitInfo.type switch
	        {
		        TypeKit.OnlyAmount => GetLang("UI_KIT_DESCRIPTION", player.UserIDString, "00:00:00", kitInfo.amount,
			        presetItems.Count),
		        TypeKit.CooldownAndAmount => GetLang("UI_KIT_DESCRIPTION", player.UserIDString,
			        ConvertMinutesToTimeFormat(kitInfo.cooldown), kitInfo.amount, presetItems.Count),
		        TypeKit.OnlyCooldown => GetLang("UI_KIT_DESCRIPTION", player.UserIDString,
			        ConvertMinutesToTimeFormat(kitInfo.cooldown),
			        GetLang("UI_KIT_DESCRIPTION_ONLY_COOLDOWN", player.UserIDString), presetItems.Count),
		        _ => String.Empty
	        };

	        Interface = Interface.Replace("%NAME_KIT%", kitInfo.title.GetLanguageText(player));
	        Interface = Interface.Replace("%KIT_ICON%", _imageUI.GetImage(kitInfo.iconKey));
	        Interface = Interface.Replace("%DESCRIPTION_KIT%", descriptionKit);
	        Interface = Interface.Replace("%AVALIABLE_STATUS_KIT%", statusKit);

	        AddUI(player, Interface);
	        return;
		   		 		  						   					  						  						  		 			  	 	 
	        String ConvertMinutesToTimeFormat(Int32 totalMinutes)
	        {
		        Int32 hours = totalMinutes / 60;
		        Int32 minutes = totalMinutes % 60;

		        return $"{hours:00}:{minutes:00}:{0:00}";
	        }
        }      


        private Int32 GetLeftAmountKit(BasePlayer player, String categoryKey, String kitName, Int32 configKitAmount)
		{
		    return TryGetKitInfo(player, categoryKey, kitName, out CategoryInfo.KitInfo kitValue) 
		        ? configKitAmount - (kitValue?.amount ?? 0) <= 0 ? 0 : configKitAmount - (kitValue?.amount ?? 0)
		        : configKitAmount;
		}
	    
	    private String GetRankName(String rankKey)
	    {
		    if (!IQRankSystem) return String.Empty;
		    return (String)IQRankSystem?.Call("API_GET_RANK_NAME", rankKey);
	    }

                
        private void DrawUI_InfoKit_Panel(BasePlayer player, String categoryKey, String kitKey)
        {
	        if (!config.presetsItems.TryGetValue(kitKey, out List<Configuration.ItemKits> presetItems))
		        return;
	        
	        if (!config.categoryKits.TryGetValue(categoryKey, out Configuration.CategoryInfo categoryInfo)) 
		        return;
	        
	        if (_interface == null) return;
            
	        String Interface = InterfaceBuilder.GetInterface("UI_KIT_ITEM_PANEL");
	        if (Interface == null) return;
	        
	        AddUI(player, Interface);
	        
	        DrawUI_InfoKit_Panel_InfoKit(player, categoryInfo, presetItems, categoryKey, kitKey);
	        DrawUI_InfoKit_Panel_ScrollBar(player, presetItems);
        } 
		   		 		  						   					  						  						  		 			  	 	 
        public class Configuration
        {
	        [JsonProperty(LanguageEn ? "Auto kits settings on respawn" : "Настройка автоматических наборов при возрождении")]
	        public AutoKitController autoKitController;
	        
	        public class ItemKits
	        {
		        [JsonProperty(LanguageEn ? "Item type: 0 - Physical item, 1 - Command" : "Тип предмета : 0 - Физический предмет, 1 - Команда")]
		        public TypeKitItem typeItem;
		        [JsonProperty(LanguageEn ? "Physical item settings" : "Настройка физического предмета")]
		        public PhysicItem physicItem;
		        [JsonProperty(LanguageEn ? "Command settings" : "Настройка команды")]
		        public Command commandItem;

		        public class PhysicItem
		        { 
			        [JsonProperty(LanguageEn ? "Container where the item will be placed [0 - Clothing, 1 - Belt, 2 - Main]" : "Контейнер в котором будет предмет [0 - Одежда, 1 - Пояс, 2 - Основной]")]
			        public ContainerItem containerItem;
			        [JsonProperty("Shortname")]
			        public String shortname;
			        [JsonProperty(LanguageEn ? "Quantity" : "Количество")]
			        public Int32 amount;
			        [JsonProperty("SkinID")]
			        public UInt64 skinID;
			        [JsonProperty(LanguageEn ? "Displayed name [Leave empty for in-game name]" : "Отображаемое имя [Оставьте пустым - тогда будет название из игры]")]
			        public String displayName;
			        [JsonProperty(LanguageEn ? "Container slot where the item will be placed" : "Слот контейнера в котором будет расположен предмет")]
			        public Int32 slotIndex;
			        [JsonProperty(LanguageEn ? "List of item contents" : "Список контента предмета")]
			        public List<ContentItem> contentsItem;

			        public class ContentItem
			        {
				        [JsonProperty(LanguageEn ? "Ammunition [true], other content [false]" : "Патроны [true], другой контент [false]")]
				        public Boolean ammoOrContent;
				        [JsonProperty("Shortname")]
				        public String shortname;
				        [JsonProperty(LanguageEn ? "Quantity" : "Количество")]
				        public Int32 amount;

			        }

			        public Item BuildItem()
			        {
				        Item itemBuild = ItemManager.CreateByName(shortname, amount, skinID);
				        if (itemBuild == null) return null;
				        
				        if(!String.IsNullOrWhiteSpace(displayName) && _.ItemIdCorrecteds.Contains(itemBuild.skin))
					        itemBuild.name = displayName;

				        if (contentsItem.Count == 0) return itemBuild;
				        
				        foreach (ContentItem contentItem in contentsItem)
				        {
					        if (contentItem.ammoOrContent)
					        {
						        BaseProjectile weapon = itemBuild.GetHeldEntity() as BaseProjectile;
						        if(!weapon) continue;
						        weapon.primaryMagazine.contents = contentItem.amount;
						        weapon.primaryMagazine.ammoType = ItemManager.FindItemDefinition(contentItem.shortname);
					        }
					        else
					        {
						        Item itemContent = ItemManager.CreateByName(contentItem.shortname, contentItem.amount);
						        itemContent?.MoveToContainer(itemBuild.contents);
					        }
				        }

				        return itemBuild;
			        }
		        }

		        public class Command
		        {
			        [JsonProperty(LanguageEn ? "Icon name from the folder [data/IQSystem/IQKits/Images]. Without file extension" : "Название иконки из папки [data/IQSystem/IQKits/Images]. Без расширения файла")]
			        public String iconKey;
			        [JsonProperty(LanguageEn ? "Console command [%USERID% - will be replaced with player's Steam64ID]" : "Консольная команда [%USERID% - заменится на Steam64ID игрока]")]
			        public String command;

			        public String BuildCommand(String userID) => command.Replace("%USERID%", userID);
		        }
	        }

	        public class IQChatSetting
	        {
		        [JsonProperty(LanguageEn ? "IQChat: Custom prefix in chat" : "IQChat : Кастомный префикс в чате")]
		        public String customPrefix;
		        [JsonProperty(LanguageEn ? "IQChat: Custom chat avatar [SteamID] (If required)" : "IQChat : Кастомный аватар в чате [SteamID]")]
		        public String customAvatar;
	        }

	        public class KitInfo
	        {
		        [JsonProperty(LanguageEn ? "Kit restriction type: 0 - Cooldown, 1 - Amount, 2 - Cooldown + Amount" : "Тип ограничения набора : 0 - Перезарядка, 1 - Количество, 2 - Перезарядка + Количество")]
		        public TypeKit type;
		        [JsonProperty(LanguageEn ? "Kit name" : "Название для набора")]
		        public LanguageText title;
		        [JsonProperty(LanguageEn ? "Icon name from the folder [data/IQSystem/IQKits/Images]. Without file extension" : "Название иконки из папки [data/IQSystem/IQKits/Images]. Без расширения файла")]
		        public String iconKey;
		        [JsonProperty(LanguageEn ? "Kit cooldown time in minutes [For types 0, 2]" : "Время перезарядки набора в минутах [Для типов 0, 2]")]
		        public Int32 cooldown;
		        [JsonProperty(LanguageEn ? "Number of available kits [For types 1, 2]" : "Количество доступных наборов [Для типов 1, 2]")]
		        public Int32 amount;
		        [JsonProperty(LanguageEn ? "How many minutes after a server wipe the kit becomes available" : "Через сколько минут разблокируется набор после вайпа сервера")]
		        public Int32 unlockedAfterMinutes;
		        [JsonProperty(LanguageEn ? "Can this kit be taken during a raid block? [true - yes/false - no]" : "Можно ли взять данный набор во время рейд-блока [true - да/false - нет]")]
		        public Boolean takeIsRaidblock;

		        public Double GetUnblockTime() => unlockedAfterMinutes > 0 ? (unlockedAfterMinutes - DateTime.UtcNow.Subtract(SaveRestore.SaveCreatedTime).TotalMinutes) * 60.0 : 0;
	        }
	        public class CategoryInfo
	        {
		        [JsonProperty(LanguageEn ? "Hide the category if the player does not have permissions for it [true], otherwise, it will be displayed to the player [false].\n\n\n\n\n\n\n\n\n" : "Скрыть категорию, если у игрока нет разрешений на нее [true], иначе она будет отображаться у игрока [false]")]
		        public Boolean hideCategoryPermission;
		        [JsonProperty(LanguageEn ? "Category name" : "Название категории")]
		        public LanguageText title;
		        [JsonProperty(LanguageEn ? "Category description" : "Описание категории")]
		        public LanguageText description;
		        [JsonProperty(LanguageEn ? "Icon name from the folder [data/IQSystem/IQKits/Images]. Without file extension" : "Название иконки из папки [data/IQSystem/IQKits/Images]. Без расширения файла")]
		        public String iconKey;
		        [JsonProperty(LanguageEn ? "Permission required to access this category and its kits [Leave empty to make it available to everyone]" : "Разрешение с которым будет доступна данная категория и наборы в ней [Оставьте пустым - будет доступен всем]")]
		        public String permission;
		        [JsonProperty(LanguageEn ? "IQRankSystem: Rank key required to access this kit [Leave empty to ignore]" : "IQRankSystem : Ключ ранга с которым будет доступен этот набор [Оставьте пустым - не будет учитываться]")]
		        public String rankKey;
		        [JsonProperty(LanguageEn ? "List of item presets for this category [Preset key (Must be unique within the category)] = Settings" : "Список пресетов с предметами для данной категории [Ключ из списка пресетов (Не должны повторяться в одной категории)] = Настройка")]
		        public Dictionary<String, KitInfo> kitList = new Dictionary<String, KitInfo>();
	        }
		        
	        public class LanguageText
	        {
		        [JsonProperty(LanguageEn ? "Text in Russian" : "Текст на русском")]
		        public String russianText;
		        [JsonProperty(LanguageEn ? "Text in English" : "Текст на английском")]
		        public String englishText;


		        public String GetLanguageText(BasePlayer player) => _.lang.GetLanguage(player.UserIDString).Equals("ru") ? russianText : englishText;
	        }
	        [JsonProperty(LanguageEn ? "IQChat Settings" : "Настройки IQChat")]
	        public IQChatSetting iqchatSetting = new IQChatSetting();
	        [JsonProperty(LanguageEn ? "Preset item settings [Unique preset name] = List of items" : "Настройка пресетов с предметами [Уникальное название пресета] = Список предметов")]
	        public Dictionary<String, List<ItemKits>> presetsItems = new Dictionary<String, List<ItemKits>>();

	        public static Configuration GetNewConfiguration()
	        {
		        return new Configuration
		        {
			        iqchatSetting = new IQChatSetting
			        {
				        customPrefix = "[<color=#5A9FDD>IQKits</color>]\n",
				        customAvatar = "0"
			        },
			        sortedPermissions = true,
			        autoKitController = new AutoKitController
			        {
				        useAutoKit = false,
				        stripInventoryDefaultItems = false,
				        settingAutoKits = new AutoKitController.AutoKits
				        {
					        useRandomAutoKit = false,
					        listAutoKits = new Dictionary<String, AutoKitController.AutoKits.AutoKit>()
					        {
						        ["iqkits.default"] = new AutoKitController.AutoKits.AutoKit
						        {
							        biomeType = BiomeType.None,
							        itemPresetKey = "AUTO_KIT_DEFAULT", 
						        },
						        ["iqkits.vip"] = new AutoKitController.AutoKits.AutoKit
						        {
							        biomeType = BiomeType.None,
							        itemPresetKey = "AUTO_KIT_VIP", 
						        },
					        }
				        }
			        },
			        categoryKits = new Dictionary<String, CategoryInfo>()
			        {
				        ["DEFAULT_CATEGORY"] = new CategoryInfo
				        {
					        title = new LanguageText
					        {
						        russianText = "НАЧАЛЬНЫЙ",
						        englishText = "STARTING",
					        },
					        description = new LanguageText
					        {
						        russianText = "Наборы для всех игроков",
						        englishText = "Kits for all players"
					        },
					        iconKey = "START_CATEGORY",
					        permission = String.Empty,
					        rankKey = String.Empty,
					        hideCategoryPermission = false,
					        kitList = new Dictionary<String, KitInfo>()
					        {
						        ["START_PRESET_KIT"] = new KitInfo
						        {
							        type = TypeKit.OnlyCooldown,
							        title = new LanguageText
							        {
								        russianText = "Стартовый",
								        englishText = "Starting",
							        },
							        iconKey = "START_ICON",
							        cooldown = 30,
							        amount = 0,
							        unlockedAfterMinutes = 0,
							        takeIsRaidblock = false,
						        },
						        ["TOOL_PRESET_KIT"] = new KitInfo
						        {
							        type = TypeKit.OnlyCooldown,
							        title = new LanguageText
							        {
								        russianText = "Инструменты",
								        englishText = "Tools",
							        },
							        iconKey = "TOOL_ICON",
							        cooldown = 30,
							        amount = 0,
							        unlockedAfterMinutes = 0,
							        takeIsRaidblock = false,
						        },
						        ["MED_PRESET_KIT"] = new KitInfo
						        {
							        type = TypeKit.OnlyCooldown,
							        title = new LanguageText
							        {
								        russianText = "Медицина",
								        englishText = "Medicine",
							        },
							        iconKey = "MED_ICON",
							        cooldown = 10,
							        amount = 0,
							        unlockedAfterMinutes = 0,
							        takeIsRaidblock = false,
						        },
						        ["FOOD_PRESET_KIT"] = new KitInfo
						        {
							        type = TypeKit.OnlyCooldown,
							        title = new LanguageText
							        {
								        russianText = "Еда",
								        englishText = "Food",
							        },
							        iconKey = "FOOD_ICON",
							        cooldown = 10,
							        amount = 0,
							        unlockedAfterMinutes = 0,
							        takeIsRaidblock = false,
						        },
						        ["DIVER_PRESET_KIT"] =  new KitInfo
						        {
							        type = TypeKit.CooldownAndAmount,
							        title = new LanguageText
							        {
								        russianText = "ДАЙВЕР",
								        englishText = "DIVER",
							        },
							        iconKey = "DIVER_ICON",
							        cooldown = 60,
							        amount = 3,
							        unlockedAfterMinutes = 0,
							        takeIsRaidblock = false,
						        },
						        ["HOME_PRESET_KIT"] = new KitInfo
						        {
							        type = TypeKit.OnlyAmount,
							        title = new LanguageText
							        {
								        russianText = "ДОМ",
								        englishText = "HOME",
							        },
							        iconKey = "HOME_ICON",
							        cooldown = 0,
							        amount = 1,
							        unlockedAfterMinutes = 0,
							        takeIsRaidblock = false,
						        },
					        }
				        },
				        ["VIP_CATEGORY"] = new CategoryInfo
				        {
					        title = new LanguageText
					        {
						        russianText = "VIP",
						        englishText = "VIP",
					        },
					        description = new LanguageText
					        {
						        russianText = "Улучшенные наборы",
						        englishText = "Enhanced kits"
					        },
					        hideCategoryPermission = false,
					        iconKey = "VIP_CATEGORY",
					        rankKey = String.Empty,
					        permission = String.Empty,
					        kitList = new Dictionary<String, KitInfo>()
					        {
						        ["VIP_START_PRESET_KIT"] = new KitInfo
						        {
							        type = TypeKit.OnlyCooldown,
							        title = new LanguageText
							        {
								        russianText = "Стартовый",
								        englishText = "Starting",
							        },
							        iconKey = "VIP_START_ICON",
							        cooldown = 120,
							        amount = 0,
							        unlockedAfterMinutes = 0,
							        takeIsRaidblock = false,
						        },
						        ["VIP_MED_PRESET_KIT"] = new KitInfo
						        {
							        type = TypeKit.OnlyCooldown,
							        title = new LanguageText
							        {
								        russianText = "Медицина",
								        englishText = "Medicine",
							        },
							        iconKey = "UPGRADE_MED_ICON",
							        cooldown = 25,
							        amount = 0,
							        unlockedAfterMinutes = 0,
							        takeIsRaidblock = false,
						        },
						        ["VIP_RESOURCE_PRESET_KIT"] = new KitInfo
						        {
							        type = TypeKit.OnlyCooldown,
							        title = new LanguageText
							        {
								        russianText = "Ресурсы",
								        englishText = "Resource",
							        },
							        iconKey = "RESOURCE_ICON",
							        cooldown = 60,
							        amount = 0,
							        unlockedAfterMinutes = 0,
							        takeIsRaidblock = false,
						        },
						        ["VIP_COMPONENT_PRESET_KIT"] = new KitInfo
						        {
							        type = TypeKit.OnlyCooldown,
							        title = new LanguageText
							        {
								        russianText = "Компоненты",
								        englishText = "Components",
							        },
							        iconKey = "COMPONENT_ICON",
							        cooldown = 300,
							        amount = 0,
							        unlockedAfterMinutes = 0,
							        takeIsRaidblock = false,
						        },
						        ["VIP_BOOM_PRESET_KIT"] = new KitInfo
						        {
							        type = TypeKit.CooldownAndAmount,
							        title = new LanguageText
							        {
								        russianText = "Взрывчатка",
								        englishText = "Explosives",
							        },
							        iconKey = "BOOM_ICON",
							        cooldown = 1440,
							        amount = 5,
							        unlockedAfterMinutes = 0,
							        takeIsRaidblock = false,
						        },
						        ["VIP_ONLY_ONE_PRESET_KIT"] = new KitInfo
						        {
							        type = TypeKit.OnlyAmount,
							        title = new LanguageText
							        {
								        russianText = "VIP",
								        englishText = "VIP",
							        },
							        iconKey = "PRIVILAGE_ICON",
							        cooldown = 0,
							        amount = 1,
							        unlockedAfterMinutes = 0,
							        takeIsRaidblock = false,
						        },
					        }
				        },
				        ["PREMIUM_CATEGORY"] = new CategoryInfo
				        {
					        title = new LanguageText
					        {
						        russianText = "PREMIUM",
						        englishText = "PREMIUM",
					        },
					        description = new LanguageText
					        {
						        russianText = "Продвинутые наборы",
						        englishText = "Advanced kits"
					        },
					        hideCategoryPermission = false,
					        iconKey = "PREMIUM_CATEGORY",
					        permission = String.Empty,
					        rankKey = String.Empty,
					        kitList = new Dictionary<String, KitInfo>()
					        {
						        ["PREMIUM_START_PRESET_KIT"] = new KitInfo
						        {
							        type = TypeKit.OnlyCooldown,
							        title = new LanguageText
							        {
								        russianText = "Стартовый",
								        englishText = "Starting",
							        },
							        iconKey = "PREMIUM_START_ICON",
							        cooldown = 120,
							        amount = 0,
							        unlockedAfterMinutes = 0,
							        takeIsRaidblock = false,
						        },
						        ["PREMIUM_MED_PRESET_KIT"] = new KitInfo
						        {
							        type = TypeKit.OnlyCooldown,
							        title = new LanguageText
							        {
								        russianText = "Медицина",
								        englishText = "Medicine",
							        },
							        iconKey = "UPGRADE_MED_ICON",
							        cooldown = 25,
							        amount = 0,
							        unlockedAfterMinutes = 0,
							        takeIsRaidblock = false,
						        },
						        ["PREMIUM_RESOURCE_PRESET_KIT"] = new KitInfo
						        {
							        type = TypeKit.OnlyCooldown,
							        title = new LanguageText
							        {
								        russianText = "Ресурсы",
								        englishText = "Resource",
							        },
							        iconKey = "UPGRADE_RESOURCE_ICON",
							        cooldown = 60,
							        amount = 0,
							        unlockedAfterMinutes = 0,
							        takeIsRaidblock = false,
						        },
						        ["PREMIUM_COMPONENT_PRESET_KIT"] = new KitInfo
						        {
							        type = TypeKit.OnlyCooldown,
							        title = new LanguageText
							        {
								        russianText = "Компоненты",
								        englishText = "Components",
							        },
							        iconKey = "UPGRADE_COMPONENT_ICON",
							        cooldown = 300,
							        amount = 0,
							        unlockedAfterMinutes = 0,
							        takeIsRaidblock = false,
						        },
						        ["PREMIUM_BOOM_PRESET_KIT"] = new KitInfo
						        {
							        type = TypeKit.CooldownAndAmount,
							        title = new LanguageText
							        {
								        russianText = "Взрывчатка",
								        englishText = "Explosives",
							        },
							        iconKey = "UPGRADE_BOOM_ICON",
							        cooldown = 1440,
							        amount = 5,
							        unlockedAfterMinutes = 0,
							        takeIsRaidblock = false,
						        },
						        ["PREMIUM_ONLY_ONE_PRESET_KIT"] = new KitInfo
						        {
							        type = TypeKit.OnlyAmount,
							        title = new LanguageText
							        {
								        russianText = "PREMIUM",
								        englishText = "PREMIUM",
							        },
							        iconKey = "PRIVILAGE_ICON",
							        cooldown = 0,
							        amount = 1,
							        unlockedAfterMinutes = 0,
							        takeIsRaidblock = false,
						        },
					        }
				        },
				        ["ELITE_CATEGORY"] = new CategoryInfo
				        {
					        title = new LanguageText
					        {
						        russianText = "ELITE",
						        englishText = "ELITE",
					        },
					        description = new LanguageText
					        {
						        russianText = "Элитные наборы",
						        englishText = "Elite kits"
					        },
					        hideCategoryPermission = false,
					        iconKey = "ELITE_CATEGORY",
					        permission = String.Empty,
					        rankKey = String.Empty,
					        kitList = new Dictionary<String, KitInfo>()
					        {
						        ["ELITE_START_PRESET_KIT"] = new KitInfo
						        {
							        type = TypeKit.OnlyCooldown,
							        title = new LanguageText
							        {
								        russianText = "Стартовый",
								        englishText = "Starting",
							        },
							        iconKey = "ELITE_START_ICON",
							        cooldown = 120,
							        amount = 0,
							        unlockedAfterMinutes = 0,
							        takeIsRaidblock = false,
						        },
						        ["ELITE_MED_PRESET_KIT"] = new KitInfo
						        {
							        type = TypeKit.OnlyCooldown,
							        title = new LanguageText
							        {
								        russianText = "Медицина",
								        englishText = "Medicine",
							        },
							        iconKey = "UPGRADE_MED_ICON",
							        cooldown = 25,
							        amount = 0,
							        unlockedAfterMinutes = 0,
							        takeIsRaidblock = false,
						        },
						        ["ELITE_RESOURCE_PRESET_KIT"] = new KitInfo
						        {
							        type = TypeKit.OnlyCooldown,
							        title = new LanguageText
							        {
								        russianText = "Ресурсы",
								        englishText = "Resource",
							        },
							        iconKey = "ADVANCED_RESOURCE_ICON",
							        cooldown = 60,
							        amount = 0,
							        unlockedAfterMinutes = 0,
							        takeIsRaidblock = false,
						        },
						        ["ELITE_COMPONENT_PRESET_KIT"] = new KitInfo
						        {
							        type = TypeKit.OnlyCooldown,
							        title = new LanguageText
							        {
								        russianText = "Компоненты",
								        englishText = "Components",
							        },
							        iconKey = "ADVANCED_COMPONENT_ICON",
							        cooldown = 300,
							        amount = 0,
							        unlockedAfterMinutes = 0,
							        takeIsRaidblock = false,
						        },
						        ["ELITE_BOOM_PRESET_KIT"] = new KitInfo
						        {
							        type = TypeKit.CooldownAndAmount,
							        title = new LanguageText
							        {
								        russianText = "Взрывчатка",
								        englishText = "Explosives",
							        },
							        iconKey = "ADVANCED_BOOM_ICON",
							        cooldown = 1440,
							        amount = 5,
							        unlockedAfterMinutes = 0,
							        takeIsRaidblock = false,
						        },
						        ["ELITE_ONLY_ONE_PRESET_KIT"] = new KitInfo
						        {
							        type = TypeKit.OnlyAmount,
							        title = new LanguageText
							        {
								        russianText = "ELITE",
								        englishText = "ELITE",
							        },
							        iconKey = "PRIVILAGE_ICON",
							        cooldown = 0,
							        amount = 1,
							        unlockedAfterMinutes = 0,
							        takeIsRaidblock = false,
						        },
					        }
				        },
				        ["BONUS_CATEGORY"] = new CategoryInfo
				        {
					        title = new LanguageText
					        {
						        russianText = "БОНУС",
						        englishText = "BONUS",
					        },
					        description = new LanguageText
					        {
						        russianText = "Бонусные наборы",
						        englishText = "Bonus kits"
					        },
					        hideCategoryPermission = false,
					        iconKey = "BONUS_CATEGORY",
					        permission = String.Empty,
					        rankKey = String.Empty,
					        kitList = new Dictionary<String, KitInfo>()
					        {
						        ["BONUS_ONE_PRESET_KIT"] = new KitInfo
						        {
							        type = TypeKit.OnlyAmount,
							        title = new LanguageText
							        {
								        russianText = "Подарок",
								        englishText = "Present",
							        },
							        iconKey = "BONUS_PRESENTS",
							        cooldown = 0,
							        amount = 1,
							        unlockedAfterMinutes = 0,
							        takeIsRaidblock = false,
						        },
						        ["BONUS_TWO_PRESET_KIT"] = new KitInfo
						        {
							        type = TypeKit.OnlyAmount,
							        title = new LanguageText
							        {
								        russianText = "Комфорт",
								        englishText = "Comfort",
							        },
							        iconKey = "BONUS_PLUSHY",
							        cooldown = 0,
							        amount = 1,
							        unlockedAfterMinutes = 0,
							        takeIsRaidblock = false,
						        },
						        ["BONUS_THREE_PRESET_KIT"] = new KitInfo
						        {
							        type = TypeKit.OnlyAmount,
							        title = new LanguageText
							        {
								        russianText = "Карты",
								        englishText = "Cards",
							        },
							        iconKey = "BONUS_CARDS",
							        cooldown = 0,
							        amount = 1,
							        unlockedAfterMinutes = 0,
							        takeIsRaidblock = false,
						        },
					        }
				        },
			        },
			        presetsItems = new Dictionary<String, List<ItemKits>>()
			        {
				        ["START_PRESET_KIT"] = new List<ItemKits>()
				        {
					        new ItemKits()
					        {
						        physicItem = new ItemKits.PhysicItem
						        {
							        containerItem = ContainerItem.containerMain,
							        shortname = "rock",
							        amount = 1,
							        skinID = 0,
							        displayName = "",
							        slotIndex = 0,
							        contentsItem = new List<ItemKits.PhysicItem.ContentItem>()
						        }
					        }
				        }
			        },
		        };
	        }
	        [JsonProperty(LanguageEn ? "Kit category settings [Unique category name] = Settings" : "Настройка категорий с наборами [Уникальное название категории] = Настройка")]
	        public Dictionary<String, CategoryInfo> categoryKits = new Dictionary<String, CategoryInfo>();
	        [JsonProperty(LanguageEn ? "Sort categories in UI by permissions [if a player has access to a category, it will appear earlier in the list] [true - yes/false - no]" : "Сортировать категории в UI по разрешениям [если у игрока есть доступ к категории - она будет первее в списке] [true - да/false - нет]")]
	        public Boolean sortedPermissions;
	        public class AutoKitController
	        {
		        [JsonProperty(LanguageEn ? "Use automatic kits [true - yes/false - no]" : "Использовать автоматические наборов [true - да/false - нет]")]
		        public Boolean useAutoKit;
		        [JsonProperty(LanguageEn ? "Clear player's inventory on respawn [true - yes/false - no]" : "Очищать инвентарь игрока при возрождении [true - да/false - нет]")]
		        public Boolean stripInventoryDefaultItems;
		        [JsonProperty(LanguageEn ? "Automatic kits settings" : "Настройка автоматических наборов")]
		        public AutoKits settingAutoKits;

		        
		        public class AutoKits
		        {
			        [JsonProperty(LanguageEn ? "Use a random kit after respawn (if multiple are available) [true - yes/false - no]" : "Использовать случайный набор после возрождения (если доступно сразу несколько) [true - да/false - нет]")]
			        public Boolean useRandomAutoKit;
			        [JsonProperty(LanguageEn ? "List of automatic kits and their settings [Permission = Settings]" : "Список автоматических наборов и их настройка [Разрешение = Настройка]")]
			        public Dictionary<String, AutoKit> listAutoKits = new Dictionary<String, AutoKit>();

			        public class AutoKit
			        {
				        [JsonProperty(LanguageEn ? "Biome type where this kit will be given [0 - Not dependent on biome, 1 - Arid, 2 - Temperate, 3 - Tundra, 4 - Arctic]" : "Тип биома в котором будет выдаваться данный набор [0 - Не зависит от биома, 1 - Arid, 2 - Temperate, 3 - Tundra, 4 - Arctic]")]
				        public BiomeType biomeType;
				        [JsonProperty(LanguageEn ? "Preset key from the list for item distribution" : "Ключ из списка пресетов для выдачи предметов")]
				        public String itemPresetKey;
			        }


			        public String GetAutoKitKey(BasePlayer player)
			        {
				        List<String> avaliableKeys = Pool.Get<List<String>>();
				        
				        foreach (KeyValuePair<String, AutoKit> listAutoKit in listAutoKits)
				        {
					        if (_.permission.UserHasPermission(player.UserIDString, listAutoKit.Key))
					        {
						        if(listAutoKit.Value.biomeType == BiomeType.None)
							        avaliableKeys.Add(listAutoKit.Value.itemPresetKey);
						        else if(listAutoKit.Value.biomeType == GetBiome(player))
							        avaliableKeys.Add(listAutoKit.Value.itemPresetKey);
					        }
				        }

				        try
				        {
					        if (avaliableKeys.Count == 0) return String.Empty;
					        return useRandomAutoKit ? avaliableKeys.GetRandom() : avaliableKeys[0];
				        }
				        finally
				        {
					        Pool.FreeUnmanaged(ref avaliableKeys);
				        }
			        }
			    
			        private BiomeType GetBiome(BasePlayer player)
			        {
				        if (TerrainMeta.BiomeMap.GetBiome(player.transform.position, 1) > 0.5) return BiomeType.Arid;
				        if (TerrainMeta.BiomeMap.GetBiome(player.transform.position, 2) > 0.5) return BiomeType.Temperate;
				        if (TerrainMeta.BiomeMap.GetBiome(player.transform.position, 4) > 0.5) return BiomeType.Tundra;
				        return TerrainMeta.BiomeMap.GetBiome(player.transform.position, 8) > 0.5 ? BiomeType.Arctic : BiomeType.None;
			        }
		        }
	        }
        }

                
        
        [ConsoleCommand("kit")]
        private void ConsoleCommandKit(ConsoleSystem.Arg arg)
        {
	        BasePlayer player = arg.Player();
	        if (!player)
	        {
		        if (!arg.HasArgs(1)) return;
		        String action = arg.Args[0];
		        switch (action)
		        {
			        case "give":
			        {
				        if (!arg.HasArgs(3))
				        {
					        PrintWarning(LanguageEn ? "" : "Попытка выдать кит игроку - неуспешна, не полностью заполнены аргументы команды!");
					        return;
				        }
				        String nameOrID = arg.Args[1];
				        String presetName = arg.Args[2];
				        BasePlayer targetPlayer = BasePlayer.Find(nameOrID);
				        if (!targetPlayer) return;
				        
				        MovedItemsInPreset(targetPlayer, presetName);
				        break;
			        }
		        }
		        return;
	        }
	        
	        DrawUI_Kit_Overlay(player);
        }
        
        private void DrawUI_Kit_Panel(BasePlayer player, Int32 kitIndex, String indexName, Int32 page, String categoryName, KeyValuePair<String, Configuration.KitInfo> kitInfo)
        {
            if (_interface == null) return;
            
            String Interface = InterfaceBuilder.GetInterface("UI_KIT_PANEL");
            if (Interface == null) return;

            String getStatusKit = GetStatusAvaliable(player, categoryName, kitInfo.Key);
            String statusKit = getStatusKit;
            String panelImg = "UI_BLOCKED_PANEL_KIT";
            String commandKit = String.Empty;
		   		 		  						   					  						  						  		 			  	 	 
            if (String.IsNullOrWhiteSpace(getStatusKit))
            {
	            panelImg = "UI_PANEL_KIT";
	            statusKit = GetLang("UI_KIT_AVALIABLE_OPEN", player.UserIDString);
	            commandKit = $"ui_kit_func take.kit {categoryName} {kitInfo.Key} {page}";
            }
            
            Interface = Interface.Replace("%CATEGORY_INDEX%", $"{categoryName}");
            Interface = Interface.Replace("%INDEX%", $"{indexName}");
            Interface = Interface.Replace("%OFFSET_MIN%", $"-101.729 {-64.037 - (kitIndex * 54)}");
            Interface = Interface.Replace("%OFFSET_MAX%", $"102.938 {-8.037 - (kitIndex * 54)}");
            
            Interface = Interface.Replace("%COMMAND_TAKE_KIT%", commandKit);
            Interface = Interface.Replace("%COMMAND_INFO_KIT%", $"ui_kit_func open.info.kit {categoryName} {kitInfo.Key}");
            
            Interface = Interface.Replace("%KIT_TITLE%", kitInfo.Value.title.GetLanguageText(player));
            Interface = Interface.Replace("%PANEL_KIT_PNG%", _imageUI.GetImage(panelImg));
            Interface = Interface.Replace("%IS_AVALIABLE_STATUS%", statusKit); 
            Interface = Interface.Replace("%ICON_KIT%", _imageUI.GetImage(kitInfo.Value.iconKey));
		   		 		  						   					  						  						  		 			  	 	 
            AddUI(player, Interface);
            
            if(kitInfo.Value.type is TypeKit.OnlyAmount or TypeKit.CooldownAndAmount)
	            DrawUI_Kit_Panel_Amount(player, indexName, GetLeftAmountKit(player, categoryName, kitInfo.Key, kitInfo.Value.amount));
        } 
	    
	    	    
                
        private static Configuration config = new Configuration();

		private static List<Configuration.ItemKits.PhysicItem.ContentItem> ParseContentInfo(Item item)
		{
		    List<Configuration.ItemKits.PhysicItem.ContentItem> contentsItem = new();

		    if (item.contents != null)
		    {
		        foreach (Item content in item.contents.itemList)
		        {
		            Configuration.ItemKits.PhysicItem.ContentItem contentItem = new()
		            {
		                ammoOrContent = false,
		                shortname = content.info.shortname,
		                amount = content.amount
		            };

		            contentsItem.Add(contentItem);
		        }
		    }

		    if (item.info.category != ItemCategory.Weapon) return contentsItem;
		    BaseProjectile weapon = item.GetHeldEntity() as BaseProjectile;
		    if (!weapon) return contentsItem;
		    
		    Configuration.ItemKits.PhysicItem.ContentItem ammoItem = new()
		    {
			    ammoOrContent = true,
			    shortname = weapon.primaryMagazine.ammoType.shortname,
			    amount = weapon.primaryMagazine.contents == 0 ? 1 : weapon.primaryMagazine.contents
		    };

		    contentsItem.Add(ammoItem);

		    return contentsItem;
		}
		   		 		  						   					  						  						  		 			  	 	 
                
        
        private class ImageUI
        {
            private const String _path = "IQSystem/IQKits/Images/";
            private const String _printPath = "data/" + _path;
            private readonly Dictionary<String, ImageData> _images = new()
            {
                { "UI_BLOCKED_PANEL_KIT", new ImageData() },
                { "UI_PANEL_KIT", new ImageData() },
                { "UI_CATEGORY_PANEL", new ImageData() },
                { "UI_EMPTY_CATEGORY", new ImageData() },
                { "UI_BLOCK_AMOUNT", new ImageData() },
                { "UI_PAGE_LEFT", new ImageData() },
                { "UI_PAGE_RIGHT", new ImageData() },
                { "UI_INFO_PANEL_NO_ITEMS", new ImageData() },
                { "UI_INFO_PANEL_ITEM_BACKGROUND", new ImageData() },
                { "UI_INFO_PANEL", new ImageData() },
            };

            private enum ImageStatus
            {
                NotLoaded,
                Loaded,
                Failed
            }

            private class ImageData
            {
                public ImageStatus Status = ImageStatus.NotLoaded;
                public String Id { get; set; }
            }
		   		 		  						   					  						  						  		 			  	 	 
            public String GetImage(String name)
            {
                if (_images.TryGetValue(name, out ImageData image) && image.Status == ImageStatus.Loaded)
                    return image.Id;
                return null;
            }

            public void DownloadImage()
            {
                if (_ == null) return;
                KeyValuePair<String, ImageData>? image = null;

                foreach (KeyValuePair<String, Configuration.CategoryInfo> category in config.categoryKits)
                {
	                _images.TryAdd(category.Value.iconKey, new ImageData());
	                foreach (KeyValuePair<String, Configuration.KitInfo> kitInfo in category.Value.kitList)
		                _images.TryAdd(kitInfo.Value.iconKey, new ImageData());

	                foreach (List<Configuration.ItemKits> itemList in config.presetsItems.Values)
	                {
		                foreach (Configuration.ItemKits itemKits in itemList)
			                if (itemKits.typeItem == TypeKitItem.command)
				                _images.TryAdd(itemKits.commandItem.iconKey, new ImageData());
	                }
                }

                foreach (KeyValuePair<String, ImageData> img in _images)
                {
                    if (img.Value.Status == ImageStatus.NotLoaded)
                    {
                        image = img;
                        break;
                    }
                }

                if (image != null)
                {
                    ServerMgr.Instance.StartCoroutine(ProcessDownloadImage(image.Value));
                }
                else
                {
                    List<String> failedImages = new List<String>();
		   		 		  						   					  						  						  		 			  	 	 
                    foreach (KeyValuePair<String, ImageData> img in _images)
                    {
                        if (img.Value.Status == ImageStatus.Failed)
                        {
                            failedImages.Add(img.Key);
                        }
                    }

                    if (failedImages.Count > 0)
                    {
                        String images = string.Join(", ", failedImages);
                        _.PrintError(LanguageEn
                            ? $"Failed to load the following images: {images}. Perhaps you did not upload them to the '{_printPath}' folder."
                            : $"Не удалось загрузить следующие изображения: {images}. Возможно, вы не загрузили их в папку '{_printPath}'.");
                        Interface.Oxide.UnloadPlugin(_.Name);
                    }
                    else
                    {
                        _.Puts(LanguageEn
                            ? $"{_images.Count} images downloaded successfully!"
                            : $"{_images.Count} изображений успешно загружено!");
                        
                        _interface = new InterfaceBuilder();
                    }
                }
            }
            
            public void UnloadImages()
            {
                foreach (KeyValuePair<string, ImageData> item in _images)
                    if(item.Value.Status == ImageStatus.Loaded)
                        if (item.Value?.Id != null)
                            FileStorage.server.Remove(uint.Parse(item.Value.Id), FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID);

                _images?.Clear();
            }

            private IEnumerator ProcessDownloadImage(KeyValuePair<String, ImageData> image)
            {
                String url = $"file://{Interface.Oxide.DataDirectory}/{_path}{image.Key}.png";

                using UnityWebRequest www = UnityWebRequestTexture.GetTexture(url);
                yield return www.SendWebRequest();

                if (www.result is UnityWebRequest.Result.ConnectionError or UnityWebRequest.Result.ProtocolError)
                    image.Value.Status = ImageStatus.Failed;
                else
                {
                    Texture2D tex = DownloadHandlerTexture.GetContent(www);
                    image.Value.Id = FileStorage.server.Store(tex.EncodeToPNG(), FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID).ToString();
                    image.Value.Status = ImageStatus.Loaded;
                    UnityEngine.Object.DestroyImmediate(tex);
                }

                DownloadImage();
            }
        }

	    	    
	    	    
	    
	    private const Boolean LanguageEn = false;
        
        private void DrawUI_Kit_Category(BasePlayer player, Int32 indexCategory, Int32 page, KeyValuePair<String, Configuration.CategoryInfo> categoryInfo)
        {
            if (_interface == null) return;
            
            String Interface = InterfaceBuilder.GetInterface("UI_KIT_CATEGORY");
            if (Interface == null) return;

            Interface = Interface.Replace("%INDEX%", $"{categoryInfo.Key}");

            Single scrollHeight = 0;
            if (categoryInfo.Value.kitList.Count > 4)
	            scrollHeight = (categoryInfo.Value.kitList.Count * 55) - 265 + 12;
            
            Interface = Interface.Replace("%SCROLL_AMOUNT%", $"{scrollHeight}");
            Interface = Interface.Replace("%OFFSET_MIN%", $"{-468.467 + (indexCategory * 234)} -171");
            Interface = Interface.Replace("%OFFSET_MAX%", $"{-231.8 + (indexCategory * 234)} 171");
            Interface = Interface.Replace("%ICON_CATEGORY%", _imageUI.GetImage(categoryInfo.Value.iconKey));
            Interface = Interface.Replace("%TITLE_CATEGORY%", categoryInfo.Value.title.GetLanguageText(player));
            Interface = Interface.Replace("%DESCRIPTION_CATEGORY%", categoryInfo.Value.description.GetLanguageText(player));
   
            AddUI(player, Interface);

            Int32 kitCount = 0;
            String nameIndexKit = String.Empty;
            foreach (KeyValuePair<String, Configuration.KitInfo> kitList in categoryInfo.Value.kitList)
            {
	            nameIndexKit = $"{kitCount}_{categoryInfo.Key}";
	            DrawUI_Kit_Panel(player, kitCount, nameIndexKit, page, categoryInfo.Key, kitList);
	            kitCount++;
            }
        }   

	    	    private void SendChat(String Message, BasePlayer player, ConVar.Chat.ChatChannel channel = ConVar.Chat.ChatChannel.Global)
	    {
		    if (IQChat) 
			    IQChat?.Call("API_ALERT_PLAYER", player, Message, config.iqchatSetting.customPrefix, config.iqchatSetting.customAvatar);
		    else player.SendConsoleCommand("chat.add", channel, 0, Message); 
	    }
	            
	    [PluginReference] Plugin IQChat, RaidBlock, NoEscape, IQRankSystem;
        
        private void DrawUI_InfoKit_Panel_ScrollBar_Item(BasePlayer player, Configuration.ItemKits itemKit, String offsetMin, String offsetMax)
        {
	        if (_interface == null) return;
	     
	        String Interface = InterfaceBuilder.GetInterface("UI_KIT_ITEM_PANEL_INFO_KIT_ITEM");
	        if (Interface == null) return;
	        
	        Int32 amountItem = itemKit.typeItem == TypeKitItem.command ? 1 : itemKit.physicItem.amount;

	        Interface = Interface.Replace("%OFFSET_MIN%", offsetMin);
	        Interface = Interface.Replace("%OFFSET_MAX%", offsetMax);
	        Interface = Interface.Replace("%AMOUNT%", $"{amountItem}");
	        
	        AddUI(player, Interface);

	        DrawUI_InfoKit_Panel_ScrollBar_Item_Icon(player, itemKit);
        }   
		
		private String GetStatusAvaliable(BasePlayer player, String categoryKey, String kitName)
		{
			if (!CheckTakeKitOtherPlugins(player))
				return GetLang("UI_KIT_AVALIABLE_ERROR", player.UserIDString);
			
			Configuration.CategoryInfo categoryInfo = config.categoryKits[categoryKey];
			if (!String.IsNullOrWhiteSpace(categoryInfo.permission) && !permission.UserHasPermission(player.UserIDString, categoryInfo.permission))
				return GetLang("UI_KIT_AVALIABLE_ERROR_NO_PERMISSIONS", player.UserIDString);

			if (!IsRank(player, categoryInfo.rankKey))
			{
				String rankName = GetRankName(categoryInfo.rankKey);
				return GetLang("UI_KIT_AVALIABLE_ERROR_NO_RANK_PLAYER", player.UserIDString, rankName);
			}

			if (!TryGetKitInfo(player, categoryKey, kitName, out CategoryInfo.KitInfo kitValue))
				return GetLang("UI_KIT_AVALIABLE_ERROR", player.UserIDString);
			
			Configuration.KitInfo kitConfig = categoryInfo.kitList[kitName];
			
			Double unblockTime = kitConfig.GetUnblockTime();
			if (unblockTime > 0)
				return GetLang("UI_KIT_AVALIABLE_COOLDOWN", player.UserIDString, FormatCooldown(unblockTime));

			if (IsRaidBlocked(player, kitConfig.takeIsRaidblock))
				return GetLang("UI_KIT_AVALIABLE_ERROR_IS_RAIDBLOCK", player.UserIDString);
			
			if (kitValue != null)
			{
				String infoKit = kitConfig.type switch
				{
					TypeKit.OnlyAmount when kitValue.amount >= kitConfig.amount
						=> GetLang("UI_KIT_AVALIABLE_NO_AMOUNT", player.UserIDString),
		   		 		  						   					  						  						  		 			  	 	 
					TypeKit.CooldownAndAmount when kitValue.amount >= kitConfig.amount
						=> GetLang("UI_KIT_AVALIABLE_NO_AMOUNT", player.UserIDString),

					TypeKit.CooldownAndAmount when kitValue.cooldown - CurrentTime() >= kitConfig.cooldown / 60.0f
						=> GetLang("UI_KIT_AVALIABLE_COOLDOWN", player.UserIDString,
							FormatCooldown(kitValue.cooldown - CurrentTime())), 

					TypeKit.OnlyCooldown when kitValue.cooldown - CurrentTime() >= kitConfig.cooldown / 60.0f
						=> GetLang("UI_KIT_AVALIABLE_COOLDOWN", player.UserIDString,
							FormatCooldown(kitValue.cooldown - CurrentTime())),

					_ => null
				};
				
				if (infoKit != null)
					return infoKit;
			}
			
			if (!IsAvaliableInventory(player, kitName))
				return GetLang("UI_KIT_AVALIABLE_NO_INVENTORY", player.UserIDString);
			
			return String.Empty;
			
			String FormatCooldown(Double seconds)
			{
				TimeSpan time = TimeSpan.FromSeconds(seconds);
				return time.TotalHours >= 24 ? $"{(Int32)time.TotalHours:D2}:{time.Minutes:D2}:{time.Seconds:D2}" : time.ToString(@"hh\:mm\:ss");
			}
		}
        
        private void DrawUI_InfoKit_Panel_ScrollBar_Item_Icon(BasePlayer player, Configuration.ItemKits itemKit)
        {
	        if (_interface == null) return;

	        if (itemKit.typeItem == TypeKitItem.command)
	        {
		        String Interface = InterfaceBuilder.GetInterface("UI_KIT_ITEM_PANEL_INFO_KIT_ITEM_COMMAND_ICON");
		        if (Interface == null) return;
		        
		        Interface = Interface.Replace("%ICON_ITEM%", _imageUI.GetImage(itemKit.commandItem.iconKey));

		        AddUI(player, Interface);
	        }
	        else
	        {
		        CuiElementContainer container = new CuiElementContainer();
		        
		        container.Add(new CuiElement
		        {
			        Name = "ITEM_ICON",
			        Parent = "BACKGROUN_ITEM",
			        Components =
			        {
				        new CuiImageComponent() { Color = "1 1 1 1", ItemId = ItemManager.FindItemDefinition(itemKit.physicItem.shortname).itemid, SkinId = itemKit.physicItem.skinID },
				        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-13.333 -13.333", OffsetMax = "13.333 13.333" }
			        }
		        });
		        
		        AddUI(player, container.ToJson());
	        }
        }
        private static void AddUI(BasePlayer player, String json)
        {
            if (!player || player.net?.connection == null) return;
            CommunityEntity.ServerInstance.ClientRPC<String>(RpcTarget.Player("AddUI", player.net.connection), json);
        }

	    private static IQKits _;
        
        private void DrawUI_LoadedKits(BasePlayer player, Int32 page = 0)
        {
            if (_interface == null) return;
            
            String Interface = InterfaceBuilder.GetInterface("UI_KIT_OVERLAY_CATEGORY_PANEL");
            if (Interface == null) return;
            
            AddUI(player, Interface);

            Tuple<Int32, IEnumerable<KeyValuePair<String, Configuration.CategoryInfo>>> sortedKits = GetSortedCategory(player, page);
            
            DrawUI_LoadedCategory(player, sortedKits.Item2, page);

            if (sortedKits.Item1 > 4)
	            DrawUI_Kit_PagePanel(player, page);
        }     
        
        
        private void DrawUI_InfoKit_Panel_ScrollBar_Empty(BasePlayer player, String offsetMin, String offsetMax)
        {
	        if (_interface == null) return;
	     
	        String Interface = InterfaceBuilder.GetInterface("UI_KIT_ITEM_PANEL_INFO_KIT_EMPTY_ITEM");
	        if (Interface == null) return;
	        
	        Interface = Interface.Replace("%OFFSET_MIN%", offsetMin);
	        Interface = Interface.Replace("%OFFSET_MAX%", offsetMax);
	        
	        AddUI(player, Interface);
        }   

		
		private static Configuration.ItemKits ParseItemInfo(Item item, ContainerItem containerItem)
		{
		    Configuration.ItemKits itemKitPreset = new()
		    {
		        typeItem = TypeKitItem.physicItem,
		        commandItem = new Configuration.ItemKits.Command { command = String.Empty, iconKey = String.Empty},
		        physicItem = new Configuration.ItemKits.PhysicItem
		        {
		            containerItem = containerItem,
		            shortname = item.info.shortname,
		            amount = item.amount,
		            skinID = item.skin,
		            slotIndex = item.position,
		            displayName = item.skin != 0 ? item.name : String.Empty,
		            contentsItem = ParseContentInfo(item),
		        },
		    };

		    return itemKitPreset;
		}

        private void DrawUI_LoadedCategory(BasePlayer player, IEnumerable<KeyValuePair<String, Configuration.CategoryInfo>> sortedCategory, Int32 page = 0)
        {
	        Int32 countKit = 0;
	        foreach (KeyValuePair<String, Configuration.CategoryInfo> categoryKits in sortedCategory)
	        {
		        if (countKit >= 4) break;
		        DrawUI_Kit_Category(player, countKit, page, categoryKits);
		        countKit++;
	        }

	        for (Int32 i = countKit; i < 4; i++)
	        {
		        DrawUI_Empty_Kit_Category(player, countKit);
		        countKit++;
	        }
        }
        private void CreateKit(BasePlayer player, String categoryName, String presetName)
        {
	        if (!config.presetsItems.ContainsKey(presetName))
	        {
		        SendChat(GetLang("CHAT_ADMIN_COMMAND_KIT_TRY_REMOVE_NO_PRESET_KIT", player.UserIDString, presetName), player);
		        return;
	        }
	        
	        if(!config.categoryKits.ContainsKey(categoryName))
		        config.categoryKits.Add(categoryName, new Configuration.CategoryInfo
		        {
			        title = new Configuration.LanguageText()
			        {
				        russianText = "NAME CATEGORY",
				        englishText = "NAME CATEGORY"
			        },
			        description = new Configuration.LanguageText()
			        {
				        russianText = "DESCRIPTION CATEGORY",
				        englishText = "DESCRIPTION CATEGORY"
			        },
			        iconKey = "START_CATEGORY",
			        permission = "iqkits.setting",
			        rankKey = String.Empty,
			        kitList = new Dictionary<String, Configuration.KitInfo>() { }
		        });

	        if (config.categoryKits[categoryName].kitList.ContainsKey(presetName))
	        {
		        SendChat(GetLang("CHAT_ADMIN_COMMAND_KIT_CONTAINS_PRESET", player.UserIDString, presetName, categoryName), player);
		        return;
	        }
	        
	        config.categoryKits[categoryName].kitList.Add(presetName, new Configuration.KitInfo
	        {
		        type = TypeKit.OnlyCooldown,
		        title = new Configuration.LanguageText()
		        {
			        russianText = presetName,
			        englishText = presetName
		        },
		        iconKey = "START_CATEGORY",
		        cooldown = 10,
		        amount = 0,
		        unlockedAfterMinutes = 0,
		        takeIsRaidblock = false
	        });
	        
	        SendChat(GetLang("CHAT_ADMIN_COMMAND_KIT_ADDED_PRESET", player.UserIDString, presetName, categoryName), player);
	        SaveConfig();
        }
        
        private void DrawUI_InfoKit_Panel_ScrollBar(BasePlayer player, List<Configuration.ItemKits> itemsKit)
        {
	        if (_interface == null) return;
	        
	        String Interface = InterfaceBuilder.GetInterface("UI_KIT_ITEM_PANEL_SCROLL");
	        if (Interface == null) return;
	        
	        Int32 itemCount = itemsKit.Count;
	        Double scrollAmount = itemCount <= 18 ? 450f : itemCount * (53f / 2f) - 101f + 53f;
	        
	        Interface = Interface.Replace("%SCROLL_AMOUNT%", $"{scrollAmount}");
            
	        AddUI(player, Interface);
	        
	        Single currentOffset = 0.171f;
	        Int32 y = 0;
	        Int32 i = 0;
	        
	        foreach (Configuration.ItemKits itemKits in itemsKit)
	        {
		        String offsetMin = $"{currentOffset} {-2.939 - (y * 48)}";
		        String offsetMax = $"{currentOffset + elementWidth} {50.394 - (y * 48)}";
		        
		        DrawUI_InfoKit_Panel_ScrollBar_Item(player, itemKits, offsetMin, offsetMax);
		        
		        if (y == 1)
		        {
			        currentOffset += elementWidth - 4;
			        y = 0;
		        }
		        else y++;

		        i++;
	        }

	        for (Int32 j = i; j < 18; j++)
	        {
		        String offsetMin = $"{currentOffset} {-2.939 - (y * 48)}";
		        String offsetMax = $"{currentOffset + elementWidth} {50.394 - (y * 48)}";

		        DrawUI_InfoKit_Panel_ScrollBar_Empty(player, offsetMin, offsetMax);
		        
		        if (y == 1)
		        {
			        currentOffset += elementWidth - 4;
			        y = 0;
		        }
		        else y++;
	        }
        }
		
		private void MovedItemsInPreset(BasePlayer player, String kitKey)
		{
			if (!config.presetsItems.TryGetValue(kitKey, out List<Configuration.ItemKits> presets))
			{
				PrintError(LanguageEn ? "" : $"Ошибка при выдаче набора игроку : {player.UserIDString}. Не существует пресет предметов с названием : {kitKey}");
				return;
			}

			Dictionary<ItemContainer, List<Item>> noSlotMovedItems = new();
			foreach (Configuration.ItemKits presetItems in presets)
			{
				if (presetItems.typeItem == TypeKitItem.physicItem)
				{
					Item buildItem = presetItems.physicItem.BuildItem();
					if (buildItem == null)
					{
						PrintError(LanguageEn ? "" : $"Ошибка при создании предмета для выдачи в пресете : {kitKey}.\nShortname предмета : {presetItems.physicItem.shortname}\nПроверьте корректность данных.");
						continue;
					}
		   		 		  						   					  						  						  		 			  	 	 
					ItemContainer containerItem = presetItems.physicItem.containerItem switch
					{
						ContainerItem.containerWear => player.inventory.containerWear,
						ContainerItem.containerBelt => player.inventory.containerBelt,
						ContainerItem.containerMain => player.inventory.containerMain,
						_ => null
					};

					if (containerItem == null)
					{
						PrintError(LanguageEn ? "" : $"Ошибка при выдачи предмета в пресете : {kitKey}.\nShortname предмета : {presetItems.physicItem.shortname}\nНеизвестный контейнер");
						continue;
					}

					Boolean moved = buildItem.MoveToContainer(containerItem, presetItems.physicItem.containerItem == ContainerItem.containerWear ? -1 : presetItems.physicItem.slotIndex);
					if (!moved)
					{
						noSlotMovedItems.TryAdd(containerItem, new List<Item>());
						noSlotMovedItems[containerItem].Add(buildItem);
					}
				}
				else
				{
					String command = presetItems.commandItem.BuildCommand(player.UserIDString);
					if (String.IsNullOrWhiteSpace(command))
					{
						PrintError(LanguageEn ? "" : $"Ошибка при выдачи команды в пресете : {kitKey}. Команда не указана!");
						continue;
					}
					
					rust.RunServerCommand(command);
				}
			}

			if (noSlotMovedItems.Count == 0) return;
			foreach (KeyValuePair<ItemContainer, List<Item>> noSlotMovedItem in noSlotMovedItems)
			{
				foreach (Item noMovedItem in noSlotMovedItem.Value)
				{
					if (!noMovedItem.MoveToContainer(noSlotMovedItem.Key))
						player.GiveItem(noMovedItem);
				}
			}
					
			noSlotMovedItems.Clear();
			noSlotMovedItems = null;
		}
        
        
        private Boolean IsAvaliableInventory(BasePlayer player, String kitName)
        {
	        Int32 slotWearFree = 8 - player.inventory.containerWear.itemList.Count;
	        Int32 slotOtherFree = 30 - (
		        player.inventory.containerBelt.itemList.Count +
		        player.inventory.containerMain.itemList.Count
	        );

	        Int32 needWear = 0;
	        Int32 needOther = 0;

	        foreach (Configuration.ItemKits item in config.presetsItems[kitName])
	        {
		        if (item.typeItem == TypeKitItem.physicItem && item.physicItem.containerItem == ContainerItem.containerWear)
			        needWear++;
		        else needOther++;
	        }

	        if (slotWearFree >= needWear && slotOtherFree >= needOther)
		        return true;
		   		 		  						   					  						  						  		 			  	 	 
	        Int32 wearOverflow = needWear - slotWearFree;
	        if (wearOverflow < 0)
		        wearOverflow = 0;
		   		 		  						   					  						  						  		 			  	 	 
	        return slotOtherFree >= (needOther + wearOverflow);
        }
        
                
        private List<Configuration.ItemKits> ParseItemInInventory(BasePlayer player)
		{
		    Int32 totalCount = player.inventory.containerMain.itemList.Count +
		                       player.inventory.containerBelt.itemList.Count +
		                       player.inventory.containerWear.itemList.Count;
		                     
		    List<Configuration.ItemKits> parseItems = new(totalCount);

		    ProcessContainer(player.inventory.containerBelt.itemList, ContainerItem.containerBelt, parseItems);
		    ProcessContainer(player.inventory.containerWear.itemList, ContainerItem.containerWear, parseItems);
		    ProcessContainer(player.inventory.containerMain.itemList, ContainerItem.containerMain, parseItems);
		    return parseItems;
		}

		   		 		  						   					  						  						  		 			  	 	 
        private void ClearDataFiles()
        {
	        repositoryKits.Clear();
	        WriteData();
        }
	    private const String pageFormat = "{0}/<size=10>{1}</size>";

        private void OnNewSave(String filename) => ClearDataFiles();
	    public enum TypeKitItem
	    {
		    physicItem,
		    command
	    }
	    private static Double CurrentTime() => Facepunch.Math.Epoch.Current;
  
        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<String, String>
            {
	            ["CHAT_ADMIN_COMMAND_KIT_NO_SYNTAX"] = "Incorrect syntax:\nWorking with presets:\n- <color=#5A9FDD>/kit preset add NAME_PRESET</color> - creates a preset from the items in the inventory and saves it to the configuration\n- <color=#5A9FDD>/kit preset edit NAME_PRESET</color> - modifies an existing preset in the configuration with the items in your inventory\n- <color=#5A9FDD>/kit preset remove NAME_PRESET</color> - removes a preset from the preset list\n\nWorking with kits:\n- <color=#5A9FDD>/kit add NAME_CATEGORY PRESET_NAME</color> - adds a preset to a category; if the category does not exist, it creates one with the specified preset\n- <color=#5A9FDD>/kit remove NAME_CATEGORY PRESET_NAME</color> - removes a preset from a category; if it's the only preset, the entire category is deleted. If no preset name is specified, the whole category will be removed",
				["CHAT_ADMIN_COMMAND_KIT_NO_CATEGORY"] = "The specified category does not exist in the configuration",
				["CHAT_ADMIN_COMMAND_KIT_CATEGORY_REMOVE"] = "Category <color=#5A9FDD>'{0}'</color> successfully removed",
				["CHAT_ADMIN_COMMAND_KIT_TRY_REMOVE_NO_PRESET_KIT"] = "Preset <color=#5A9FDD>'{0}'</color> does not exist in the configuration.",
				["CHAT_ADMIN_COMMAND_KIT_REMOVE_PRESET_IN_CATEGORY"] = "Preset <color=#5A9FDD>'{0}'</color> removed from category <color=#5A9FDD>'{1}'</color>",
				["CHAT_ADMIN_COMMAND_KIT_CONTAINS_PRESET"] = "Preset <color=#5A9FDD>'{0}'</color> already exists in category <color=#5A9FDD>'{1}'</color>",
				["CHAT_ADMIN_COMMAND_KIT_ADDED_PRESET"] = "Preset <color=#5A9FDD>'{0}'</color> successfully added to category <color=#5A9FDD>'{1}'</color>",

				["UI_KIT_AVALIABLE_ERROR"] = "Unavailable",
				["UI_KIT_AVALIABLE_ERROR_NO_PERMISSIONS"] = "Insufficient permissions",
				["UI_KIT_AVALIABLE_ERROR_IS_RAIDBLOCK"] = "Raid block is active",
				["UI_KIT_AVALIABLE_ERROR_NO_RANK_PLAYER"] = "Rank: {0}",
				["UI_KIT_AVALIABLE_NO_AMOUNT"] = "Out of stock",
				["UI_KIT_AVALIABLE_COOLDOWN"] = "{0}",
				["UI_KIT_AVALIABLE_NO_INVENTORY"] = "Inventory is full",
				["UI_KIT_AVALIABLE_OPEN"] = "Available to claim",
				["UI_KIT_DESCRIPTION"] = "Cooldown time: {0}\nTotal claims allowed: {1}\nItems: {2} pcs",
				["UI_KIT_DESCRIPTION_ONLY_COOLDOWN"] = "Unlimited",

				["TITLE_KIT_OVERLAY"] = "GAME KITS",
				["DESCRIPTION_KIT_OVERLAY"] = "Besides free kits, you can purchase access to kits at freneticrust.shop",

				["CHAT_ALERT_CREATE_PRESET_SUCCESS"] = "You have successfully created a preset with items.\nConfiguration key: <color=#5A9FDD>'{0}'</color>\nItem count: <color=#5A9FDD>'{1}'</color>",
				["CHAT_ALERT_CREATE_PRESET_ERROR_NO_ITEMS"] = "Unable to create a preset.\nYou have 0 items in your inventory",
				["CHAT_ALERT_CREATE_PRESET_ERROR_CONTAINS_KEY"] = "Unable to create a preset.\nA preset with this key already exists in the configuration",

				["CHAT_ALERT_EDIT_PRESET_SUCCESS"] = "You have successfully modified an existing preset with items.\nConfiguration key: <color=#5A9FDD>'{0}'</color>\nItem count: <color=#5A9FDD>'{1}'</color>",
				["CHAT_ALERT_EDIT_PRESET_ERROR_NO_ITEMS"] = "Unable to modify the preset.\nYou have 0 items in your inventory",
				["CHAT_ALERT_EDIT_PRESET_ERROR_NO_CONTAINS_KEY"] = "Unable to modify the preset.\nA preset with this key does not exist in the configuration",
		   		 		  						   					  						  						  		 			  	 	 
				["CHAT_ALERT_REMOVE_PRESET_SUCCESS"] = "You have successfully removed an existing preset with items.\nConfiguration key: <color=#5A9FDD>'{0}'</color>",
				["CHAT_ALERT_REMOVE_PRESET_ERROR_NO_ITEMS"] = "Unable to remove the preset.\nA preset with this key does not exist in the configuration",
            }, this);

            lang.RegisterMessages(new Dictionary<String, String>
            {
	            ["CHAT_ADMIN_COMMAND_KIT_NO_SYNTAX"] = "Некорректный синтаксис :\nРабота с пресетами :\n- <color=#5A9FDD>/kit preset add NAME_PRESET</color> - создает пресет из предметов в инвентаре в конфигурацию\n- <color=#5A9FDD>/kit preset edit NAME_PRESET</color> - меняет уже существующий пресет из конфигурации на предметы в вашем инвентаре\n- <color=#5A9FDD>/kit preset remove NAME_PRESET</color> - удаляет пресет из списка пресетов\n\nРабота с китами :\n- <color=#5A9FDD>/kit add NAME_CATEGORY PRESET_NAME</color> - добавляет пресет в категорию, если категории не существует - создаст ее с указанным пресетом\n- <color=#5A9FDD>/kit remove NAME_CATEGORY PRESET_NAME</color> - удаляет пресет из категории, если пресет всего один - удаляет всю категорию. Если не указать название пресета - удалит всю категорию",
	            ["CHAT_ADMIN_COMMAND_KIT_NO_CATEGORY"] = "Указанной категории не существует в конфигурации",
	            ["CHAT_ADMIN_COMMAND_KIT_CATEGORY_REMOVE"] = "Категория <color=#5A9FDD>'{0}'</color> успешно удалена",
	            ["CHAT_ADMIN_COMMAND_KIT_TRY_REMOVE_NO_PRESET_KIT"] = "Пресета <color=#5A9FDD>'{0}'</color> не существует в конфигурации.",
	            ["CHAT_ADMIN_COMMAND_KIT_REMOVE_PRESET_IN_CATEGORY"] = "Пресет <color=#5A9FDD>'{0}'</color> удален из категории <color=#5A9FDD>'{1}'</color>",
	            ["CHAT_ADMIN_COMMAND_KIT_CONTAINS_PRESET"] = "Пресет <color=#5A9FDD>'{0}'</color> уже существует в категории <color=#5A9FDD>'{1}'</color>",
	            ["CHAT_ADMIN_COMMAND_KIT_ADDED_PRESET"] = "Пресет <color=#5A9FDD>'{0}'</color> успешно добавлен в категорию <color=#5A9FDD>'{1}'</color>",
	            
	            ["UI_KIT_AVALIABLE_ERROR"] = "Недоступен",
	            ["UI_KIT_AVALIABLE_ERROR_NO_PERMISSIONS"] = "Недостаточно прав",
	            ["UI_KIT_AVALIABLE_ERROR_IS_RAIDBLOCK"] = "Активен рейдблок",
	            ["UI_KIT_AVALIABLE_ERROR_NO_RANK_PLAYER"] = "Ранг : {0}",
	            ["UI_KIT_AVALIABLE_NO_AMOUNT"] = "Кончился",
	            ["UI_KIT_AVALIABLE_COOLDOWN"] = "{0}",
	            ["UI_KIT_AVALIABLE_NO_INVENTORY"] = "Инвентарь полон",
	            ["UI_KIT_AVALIABLE_OPEN"] = "Можно получить",
	            ["UI_KIT_DESCRIPTION"] = "Время перезарядки : {0}\nВсего раз можно взять : {1}\nПредметов : {2} штук",
	            ["UI_KIT_DESCRIPTION_ONLY_COOLDOWN"] = "Бесконечно",
	            
	            ["TITLE_KIT_OVERLAY"] = "ИГРОВЫЕ НАБОРЫ",
	            ["DESCRIPTION_KIT_OVERLAY"] = "Помимо бесплатных, вы можете купить доступ к наборам на freneticrust.shop",
	            
	            ["CHAT_ALERT_CREATE_PRESET_SUCCESS"] = "Вы успешно создали пресет с предметами.\nКлюч в конфигурации : <color=#5A9FDD>'{0}'</color>\nКоличество предметов : <color=#5A9FDD>'{1}'</color>",
	            ["CHAT_ALERT_CREATE_PRESET_ERROR_NO_ITEMS"] = "Невозможно создать пресет.\nУ вас 0 предметов в инвентаре",
	            ["CHAT_ALERT_CREATE_PRESET_ERROR_CONTAINS_KEY"] = "Невозможно создать пресет.\nТакой ключ для пресета уже есть в конфигурации",
	            
	            ["CHAT_ALERT_EDIT_PRESET_SUCCESS"] = "Вы успешно изменили существующий пресет пресет с предметами.\nКлюч в конфигурации : <color=#5A9FDD>'{0}'</color>\nКоличество предметов : <color=#5A9FDD>'{1}'</color>",
	            ["CHAT_ALERT_EDIT_PRESET_ERROR_NO_ITEMS"] = "Невозможно изменить пресет.\nУ вас 0 предметов в инвентаре",
	            ["CHAT_ALERT_EDIT_PRESET_ERROR_NO_CONTAINS_KEY"] = "Невозможно изменить пресет.\nТакой ключ для пресета не существует есть в конфигурации",
	            
	            ["CHAT_ALERT_REMOVE_PRESET_SUCCESS"] = "Вы успешно удалили существующий пресет пресет с предметами.\nКлюч в конфигурации : <color=#5A9FDD>'{0}'</color>",
	            ["CHAT_ALERT_REMOVE_PRESET_ERROR_NO_ITEMS"] = "Невозможно удалить пресет.\nТакой ключ для пресета не существует есть в конфигурации",
            }, this, "ru");
        }
        protected override void SaveConfig() => Config.WriteObject(config);

		private CategoryInfo.KitInfo GetOrCreateKitInfo(CategoryInfo categoryInfo, String kitKey, Configuration.KitInfo kitConfig)
		{
			if (categoryInfo.playerKitsTaked.TryGetValue(kitKey, out CategoryInfo.KitInfo kitInfo)) return kitInfo;
			kitInfo = new CategoryInfo.KitInfo
			{
				amount = 0,
				cooldown = kitConfig.type is TypeKit.OnlyCooldown or TypeKit.CooldownAndAmount ? CurrentTime() + (kitConfig.cooldown * 60) : 0
			};
			categoryInfo.playerKitsTaked[kitKey] = kitInfo;
			return kitInfo;
		}

        private void DrawUI_Update_KitPanel(BasePlayer player, String indexName, Int32 page, String categoryName, KeyValuePair<String, Configuration.KitInfo> kitInfo)
        {
            if (_interface == null) return;
            
            String Interface = InterfaceBuilder.GetInterface("UI_KIT_UPDATE_INFO");
            if (Interface == null) return;
            
            String getStatusKit = GetStatusAvaliable(player, categoryName, kitInfo.Key);
            String statusKit = getStatusKit;
            String commandKit = String.Empty;
            String panelImg = "UI_BLOCKED_PANEL_KIT";
		   		 		  						   					  						  						  		 			  	 	 
            if (String.IsNullOrWhiteSpace(getStatusKit))
            {
	            panelImg = "UI_PANEL_KIT";
	            statusKit = GetLang("UI_KIT_AVALIABLE_OPEN", player.UserIDString);
	            commandKit = $"ui_kit_func take.kit {categoryName} {kitInfo.Key} {page}";
            }
            
            Interface = Interface.Replace("%CATEGORY_INDEX%", $"{categoryName}");
            Interface = Interface.Replace("%INDEX%", $"{indexName}");
            Interface = Interface.Replace("%PANEL_KIT_PNG%", _imageUI.GetImage(panelImg));
            Interface = Interface.Replace("%COMMAND_TAKE_KIT%", commandKit);
            Interface = Interface.Replace("%IS_AVALIABLE_STATUS%", statusKit); 

            AddUI(player, Interface);
        }

                
        
        
        private class InterfaceBuilder
        {
            
            public static InterfaceBuilder Instance;
            public const String UI_KIT_OVERLAY = "UI_KIT_OVERLAY";
            public Dictionary<String, String> Interfaces;

            
            
            public InterfaceBuilder()
            {
                Instance = this;
                Interfaces = new Dictionary<String, String>();
                
                Building_KitOverlay();
                Building_KitCategory();
                Building_EmptyKitCategory();
                Building_KitPanel();
                Building_KitPanel_Amount();
                Building_KitPagePanel();
                Building_CategoryPanel();
                
                Building_InfoPanel_ItemsKit();
                Building_InfoPanel_ItemsKit_InfoKit();
                Building_InfoPanel_ItemsKit_Scroll();
                Building_InfoPanel_ItemsKit_Item();
                Building_InfoPanel_ItemsKit_Item_Command();
                Building_InfoPanel_ItemsKit_EmptyItem();
                
                Building_KitUpdateInfo();
                Building_KitUpdateInfoAmount();
            }

            public static void AddInterface(String name, String json)
            {
                if (!Instance.Interfaces.TryAdd(name, json))
                {
                    _.PrintError($"Error! Tried to add existing cui elements! -> {name}");
                    return;
                }
            }

            public static String GetInterface(String name)
            {
                if (Instance.Interfaces.TryGetValue(name, out String json) == false)
                {
                    _.PrintWarning($"Warning! UI elements not found by name! -> {name}");
                }
		   		 		  						   					  						  						  		 			  	 	 
                return json;
            }

            public static void DestroyAll()
            {
                for (Int32 i = 0; i < BasePlayer.activePlayerList.Count; i++)
                {
                    BasePlayer player = BasePlayer.activePlayerList[i];
                    DestroyUI(player, UI_KIT_OVERLAY);
                }
            }
		   		 		  						   					  						  						  		 			  	 	 
            
                        
            private void Building_KitOverlay()
            {
                CuiElementContainer container = new CuiElementContainer();

                container.Add(new CuiPanel
                {
	                CursorEnabled = true,
	                Image = { Color = "0 0 0 0.8", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat", Sprite = "assets/content/ui/ui.background.transparent.radial.psd" },
	                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" }
                }, "OverlayNonScaled", UI_KIT_OVERLAY, UI_KIT_OVERLAY);

                container.Add(new CuiElement
                {
	                Name = "TITLE_KIT_OVERLAY",
	                Parent = UI_KIT_OVERLAY,
	                DestroyUi = "TITLE_KIT_OVERLAY",
	                Components = {
		                new CuiTextComponent { Text = "%TITLE_KIT_OVERLAY%", Font = "robotocondensed-bold.ttf", FontSize = 24, Align = TextAnchor.LowerLeft, Color = "0.8941177 0.854902 0.8196079 1" },
		                new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-458.697 224.981", OffsetMax = "-243.17 265.286" }
	                }
                });

                container.Add(new CuiElement
                {
	                Name = "DESCRIPTION_KIT_OVERLAY",
	                Parent = UI_KIT_OVERLAY,
	                DestroyUi = "DESCRIPTION_KIT_OVERLAY",
	                Components = {
		                new CuiTextComponent { Text = "%DESCRIPTION_KIT_OVERLAY%", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.UpperLeft, Color = "0.8941177 0.854902 0.8196079 1" },
		                new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-458.695 180.693", OffsetMax = "-119.933 228.907" }
	                }
                }); 
                
                container.Add(new CuiButton
                {
	                Button = { Color = "0 0 0 0", Close = UI_KIT_OVERLAY},
	                Text =
	                {
		                Text = "", Font = "robotocondensed-regular.ttf", FontSize = 8,
		                Align = TextAnchor.MiddleCenter, Color = "0 0 0 0"
	                },
	                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
                }, UI_KIT_OVERLAY, "CLOSE_OVERLAY_BUTTON", "CLOSE_OVERLAY_BUTTON");
                
                AddInterface("UI_KIT_OVERLAY", container.ToJson());
            }

            private void Building_CategoryPanel()
            {
	            CuiElementContainer container = new CuiElementContainer();
	     
	            container.Add(new CuiPanel
	            {
		            CursorEnabled = false,
		            Image = { Color = "0 0 0 0" },
		            RectTransform =
		            {
			            AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-468.803 -170.992", OffsetMax = "468.131 171.008"
		            }
	            }, UI_KIT_OVERLAY, "CATEGORY_PANEL", "CATEGORY_PANEL");
	            
	            AddInterface("UI_KIT_OVERLAY_CATEGORY_PANEL", container.ToJson());
            }
            
            private void Building_KitPagePanel()
            {
                CuiElementContainer container = new CuiElementContainer();

                container.Add(new CuiPanel
                {
	                CursorEnabled = false,
	                Image = { Color = "0 0 0 0" },
	                RectTransform ={ AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-243.167 224.982", OffsetMax = "-159.553 258.498" }
                },UI_KIT_OVERLAY,"PAGE_PANEL", "PAGE_PANEL");

                container.Add(new CuiElement
                {
	                Name = "PAGE_COUNT_TITLE",
	                Parent = "PAGE_PANEL",
	                DestroyUi = "PAGE_COUNT_TITLE",
	                Components = {
		                new CuiTextComponent { Text = "%PAGE_TITLE%", Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = "0.8941177 0.854902 0.8196079 1" },
		                new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-18.16 -15.491", OffsetMax = "18.16 12.558" }
	                }
                });

                container.Add(new CuiElement
                {
	                Name = "PAGE_LEFT_PNG",
	                Parent = "PAGE_PANEL",
	                DestroyUi = "PAGE_LEFT_PNG",
	                Components = {
		                new CuiRawImageComponent { Color = "%PAGE_LEFT_COLOR%", Png = _imageUI.GetImage("UI_PAGE_LEFT") },
		                new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-39.173 -14.8", OffsetMax = "-12.507 11.867" }
	                }
                });
                
                container.Add(new CuiButton
                {
	                Button = { Color = "0 0 0 0", Command = "%PAGE_LEFT_COMMAND%"},
	                Text =
	                {
		                Text = "", Font = "robotocondensed-regular.ttf", FontSize = 8,
		                Align = TextAnchor.MiddleCenter, Color = "0 0 0 0"
	                },
	                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
                }, "PAGE_LEFT_PNG", "PAGE_LEFT_BUTTON", "PAGE_LEFT_BUTTON");

                container.Add(new CuiElement
                {
	                Name = "PAGE_RIGHT_PNG",
	                Parent = "PAGE_PANEL",
	                DestroyUi = "PAGE_RIGHT_PNG",
	                Components = {
		                new CuiRawImageComponent { Color = "%PAGE_RIGHT_COLOR%", Png = _imageUI.GetImage("UI_PAGE_RIGHT")  },
		                new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "13 -14.8", OffsetMax = "39.667 11.867" }
	                }
                });

                container.Add(new CuiButton
                {
	                Button = { Color = "0 0 0 0", Command = "%PAGE_RIGHT_COMMAND%"},
	                Text =
	                {
		                Text = "", Font = "robotocondensed-regular.ttf", FontSize = 8,
		                Align = TextAnchor.MiddleCenter, Color = "0 0 0 0"
	                },
	                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
                }, "PAGE_RIGHT_PNG", "PAGE_RIGHT_BUTTON", "PAGE_RIGHT_BUTTON");
		   		 		  						   					  						  						  		 			  	 	 
                AddInterface("UI_KIT_OVERLAY_PAGE_PANEL", container.ToJson());
            }

            private void Building_KitCategory()
            {
	            CuiElementContainer container = new CuiElementContainer();

	            container.Add(new CuiElement
	            {
		            Name = "CATEGORY_KIT_%INDEX%",
		            Parent = "CATEGORY_PANEL",
		            DestroyUi = "CATEGORY_KIT_%INDEX%",
		            Components =
		            {
			            new CuiRawImageComponent { Color = "1 1 1 1", Png = _imageUI.GetImage("UI_CATEGORY_PANEL") },
			            new CuiRectTransformComponent
			            {
				            AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "%OFFSET_MIN%", OffsetMax = "%OFFSET_MAX%"
			            }
		            }
	            });
		   		 		  						   					  						  						  		 			  	 	 
	            container.Add(new CuiElement
	            {
		            Name = "DESCRIPTION_CATEGORY_%INDEX%",
		            Parent = "CATEGORY_KIT_%INDEX%",
		            DestroyUi = "DESCRIPTION_CATEGORY_%INDEX%",
		            Components =
		            {
			            new CuiTextComponent
			            {
				            Text = "%DESCRIPTION_CATEGORY%", Font = "robotocondensed-regular.ttf", FontSize = 12,
				            Align = TextAnchor.UpperLeft, Color = "0.8941177 0.854902 0.8196079 1"
			            },
			            new CuiRectTransformComponent
			            {
				            AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-48.751 113.326",
				            OffsetMax = "109.551 132.807"
			            }
		            }
	            });
	         
	            container.Add(new CuiElement
	            {
		            Name = "TITLE_CATEGORY_%INDEX%",
		            Parent = "CATEGORY_KIT_%INDEX%",
		            DestroyUi = "TITLE_CATEGORY_%INDEX%",
		            Components =
		            {
			            new CuiTextComponent
			            {
				            Text = "%TITLE_CATEGORY%", Font = "robotocondensed-bold.ttf", FontSize = 18,
				            Align = TextAnchor.UpperLeft, Color = "0.8941177 0.854902 0.8196079 1"
			            },
			            new CuiRectTransformComponent
			            {
				            AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-48.751 131.34",
				            OffsetMax = "115.551 153.593"
			            }
		            }
	            });

	            container.Add(new CuiElement
	            {
		            Name = "ICON_CATEGORY_%INDEX%",
		            Parent = "CATEGORY_KIT_%INDEX%",
		            DestroyUi = "ICON_CATEGORY_%INDEX%",
		            Components =
		            {
			            new CuiRawImageComponent { Color = "1 1 1 1", Png = "%ICON_CATEGORY%" },
			            new CuiRectTransformComponent
			            {
				            AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-102.933 116.8",
				            OffsetMax = "-62.933 156.8"
			            }
		            }
	            });

	            container.Add(new CuiElement
	            {
		            Name = "SCROLL_PANEL_%INDEX%",
		            Parent = "CATEGORY_KIT_%INDEX%",
		            DestroyUi = "SCROLL_PANEL_%INDEX%",
		            Components =
		            {
			            new CuiScrollViewComponent
			            {
				            MovementType = ScrollRect.MovementType.Elastic,
				            Vertical = true,
				            Elasticity = 0.2f,
				            Inertia = true,
				            ScrollSensitivity = 13f,
				            ContentTransform = new CuiRectTransform
				            {
					            AnchorMin = "0 0",
					            AnchorMax = "1 1",
					            OffsetMin = $"0 -%SCROLL_AMOUNT%"
				            },
			            },
			            new CuiRawImageComponent { Color = "0 0 0 0" },
			            new CuiRectTransformComponent
			            {
				            AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-109.294 -333.641",
				            OffsetMax = "109.818 -68.019"
			            }
		            }
	            });

	            AddInterface("UI_KIT_CATEGORY", container.ToJson());
            }

            private void Building_KitPanel()
            {
	            CuiElementContainer container = new CuiElementContainer();

	            container.Add(new CuiElement
	            {
		            Name = "KIT_PANEL_%INDEX%",
		            Parent = "SCROLL_PANEL_%CATEGORY_INDEX%",
		            DestroyUi = "KIT_PANEL_%INDEX%",
		            Components =
		            {   
			            new CuiRawImageComponent { Color = "1 1 1 1", Png = "%PANEL_KIT_PNG%" },
			            new CuiRectTransformComponent { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "%OFFSET_MIN%", OffsetMax = "%OFFSET_MAX%" }
		            }
	            });

	            container.Add(new CuiElement
	            {
		            Name = "KIT_ICON_%INDEX%",
		            Parent = "KIT_PANEL_%INDEX%",
		            DestroyUi = "KIT_ICON_%INDEX%",
		            Components =
		            {
			            new CuiRawImageComponent { Color = "1 1 1 1", Png = "%ICON_KIT%" },
			            new CuiRectTransformComponent
			            {
				            AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-86.933 -13.333",
				            OffsetMax = "-60.267 13.333"
			            }
		            }
	            });

	            container.Add(new CuiElement
	            {
		            Name = "KIT_NAME_%INDEX%",
		            Parent = "KIT_PANEL_%INDEX%",
		            DestroyUi = "KIT_NAME_%INDEX%",
		            Components =
		            {
			            new CuiTextComponent
			            {
				            Text = "%KIT_TITLE%", Font = "robotocondensed-bold.ttf", FontSize = 12,
				            Align = TextAnchor.UpperLeft, Color = "0.8941177 0.854902 0.8196079 1"
			            },
			            new CuiRectTransformComponent
			            {
				            AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-47.173 -3.102",
				            OffsetMax = "27.867 11.196"
			            }
		            }
	            });

	            container.Add(new CuiElement
	            {
		            Name = "COOLDOWN_TITLE_%INDEX%",
		            Parent = "KIT_PANEL_%INDEX%",
		            DestroyUi = "COOLDOWN_TITLE_%INDEX%",
		            Components =
		            {
			            new CuiTextComponent
			            {
				            Text = "%IS_AVALIABLE_STATUS%", Font = "robotocondensed-regular.ttf", FontSize = 10,
				            Align = TextAnchor.MiddleLeft, Color = "0.8941177 0.854902 0.8196079 1"
			            },
			            new CuiRectTransformComponent
			            {
				            AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-47.173 -14.729", OffsetMax = "38.345 -0.431"
			            }
		            }
	            });
	            
	            container.Add(new CuiElement
	            {
		            Name = "BUTTON_COMMAND_TAKE_%INDEX%",
		            Parent = "KIT_PANEL_%INDEX%",
		            DestroyUi = "BUTTON_COMMAND_TAKE_%INDEX%",
		            Components =
		            {
			            new CuiButtonComponent { Color = "0 0 0 0", Command = "%COMMAND_TAKE_KIT%" },
			            new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-96.425 -22.074", OffsetMax = "41.557 22.193" }
		            }
	            });
	            
	            container.Add(new CuiButton
	            {
		            Button = { Color = "0 0 0 0", Command = "%COMMAND_INFO_KIT%"},
		            Text = { Text = "", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0 0 0 0" },
		            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "50.414 -21.225", OffsetMax = "94.955 22.193" }
	            },"KIT_PANEL_%INDEX%","BUTTON_COMMAND_INFO_%INDEX%", "BUTTON_COMMAND_INFO_%INDEX%");
	            
	            AddInterface("UI_KIT_PANEL", container.ToJson());
            }
            
            private void Building_KitUpdateInfo()
            {
	            CuiElementContainer container = new CuiElementContainer();
	            
	            container.Add(new CuiElement
	            {
		            Name = "KIT_PANEL_%INDEX%",
		            Parent = "SCROLL_PANEL_%CATEGORY_INDEX%",
		            Update = true,
		            Components = { new CuiRawImageComponent { Png = "%PANEL_KIT_PNG%" }, }
	            });
	            
	            container.Add(new CuiElement
	            {
		            Name = "COOLDOWN_TITLE_%INDEX%",
		            Parent = "KIT_PANEL_%INDEX%",
		            Update = true,
		            Components = { new CuiTextComponent { Text = "%IS_AVALIABLE_STATUS%" }, }
	            });
	            
	            container.Add(new CuiElement
	            {
		            Name = "BUTTON_COMMAND_TAKE_%INDEX%",
		            Parent = "KIT_PANEL_%INDEX%",
		            Update = true,
		            Components = { new CuiButtonComponent { Command = "%COMMAND_TAKE_KIT%" }, }
	            });
	            
	            AddInterface("UI_KIT_UPDATE_INFO", container.ToJson());
            }
            
            private void Building_KitUpdateInfoAmount()
            {
	            CuiElementContainer container = new CuiElementContainer();
	            
	            container.Add(new CuiElement
	            {
		            Name = "AMOUNT_KIT_%INDEX%",
		            Parent = "KIT_PANEL_%INDEX%",
		            Update = true,
		            Components = { new CuiTextComponent { Text = "x%AMOUNT%" }, }
	            });
	            
	            AddInterface("UI_KIT_UPDATE_INFO_AMOUNT", container.ToJson());
            }
            
            private void Building_KitPanel_Amount()
            {
	            CuiElementContainer container = new CuiElementContainer();

	            container.Add(new CuiElement
	            {
		            Name = "AMOUNT_ICON_%INDEX%",
		            Parent = "KIT_PANEL_%INDEX%",
		            DestroyUi = "AMOUNT_ICON_%INDEX%",
		            Components =
		            {
			            new CuiRawImageComponent { Color = "1 1 1 1", Png = _imageUI.GetImage("UI_BLOCK_AMOUNT") },
			            new CuiRectTransformComponent
			            {
				            AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "10.327 3.007", OffsetMax = "26.327 19.007"
			            }
		            }
	            });

	            container.Add(new CuiElement
	            {
		            Name = "AMOUNT_KIT_%INDEX%",
		            Parent = "KIT_PANEL_%INDEX%",
		            DestroyUi = "AMOUNT_KIT_%INDEX%",
		            Components =
		            {
			            new CuiTextComponent
			            {
				            Text = "x%AMOUNT%", Font = "robotocondensed-regular.ttf", FontSize = 10,
				            Align = TextAnchor.MiddleLeft,
				            Color = "0.8941177 0.854902 0.8196079 1"
			            },
			            new CuiRectTransformComponent
			            {
				            AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "26.327 3.007",
				            OffsetMax = "42.327 19.007"
			            }
		            }
	            });

	            AddInterface("UI_KIT_PANEL_AMOUNT_BLOCK", container.ToJson());
            }
            
            private void Building_EmptyKitCategory()
            {
	            CuiElementContainer container = new CuiElementContainer();

	            container.Add(new CuiElement
	            {
		            Name = "EMPTY_CATEGORY_%INDEX%",
		            Parent = "CATEGORY_PANEL",
		            DestroyUi = "EMPTY_CATEGORY_%INDEX%",
		            Components = {
			            new CuiRawImageComponent { Color = "1 1 1 1", Png = _imageUI.GetImage("UI_EMPTY_CATEGORY") },
			            new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "%OFFSET_MIN%", OffsetMax = "%OFFSET_MAX%"}
		            }
	            });

	            container.Add(new CuiElement
	            {
		            Name = "EMPTY_NAME_%INDEX%",
		            Parent = "EMPTY_CATEGORY_%INDEX%",
		            DestroyUi = "EMPTY_NAME_%INDEX%",
		            Components = {
			            new CuiTextComponent { Text = "ТУТ ПУСТО", Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.UpperCenter, Color = "0.8941177 0.854902 0.8196079 1" },
			            new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-64.779 -50.412", OffsetMax = "64.779 -24.888" }
		            }
	            });

	            container.Add(new CuiElement
	            {
		            Name = "EMPTY_DESCRIPTION_%INDEX%",
		            Parent = "EMPTY_CATEGORY_%INDEX%",
		            DestroyUi = "EMPTY_DESCRIPTION_%INDEX%",
		            Components = {
			            new CuiTextComponent { Text = "Извините, пока что тут ничего нету, возможно в будущем мы добавим наборы", Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.UpperCenter, Color = "0.8941177 0.854902 0.8196079 1" },
			            new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-89.249 -113.256", OffsetMax = "89.249 -50.411" }
		            }
	            });


	            AddInterface("UI_KIT_EMPTY_CATEGORY", container.ToJson());
            }
            
            
            
            private void Building_InfoPanel_ItemsKit()
            {
	            CuiElementContainer container = new CuiElementContainer();
		   		 		  						   					  						  						  		 			  	 	 
	            container.Add(new CuiElement
	            {
		            Name = "INFO_KIT_ITEMS",
		            Parent = UI_KIT_OVERLAY,
		            DestroyUi = "INFO_KIT_ITEMS",
		            Components =
		            {
			            new CuiRawImageComponent { Color = "1 1 1 1", Png = _imageUI.GetImage("UI_INFO_PANEL")},
			            new CuiRectTransformComponent
			            {
				            AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-474.067 -310.2", OffsetMax = "241.267 -176.867"
			            }
		            }
	            });
	            
	            container.Add(new CuiButton
	            {
		            Button = { Color = "0 0 0 0", Close = "INFO_KIT_ITEMS" },
		            Text =
		            {
			            Text = "", Font = "robotocondensed-regular.ttf", FontSize = 14,
			            Align = TextAnchor.MiddleCenter, Color = "0 0 0 0"
		            },
		            RectTransform =
		            {
			            AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-139.22 36.867",
			            OffsetMax = "-126.38 49.212"
		            }
	            }, "INFO_KIT_ITEMS", "CLOSE_BUTTON_INFO_KIT", "CLOSE_BUTTON_INFO_KIT");
	            
	            AddInterface("UI_KIT_ITEM_PANEL", container.ToJson());
            } 
            
            private void Building_InfoPanel_ItemsKit_Scroll()
            {
	            CuiElementContainer container = new CuiElementContainer();

	            container.Add(new CuiElement
	            {
		            Name = "INFO_KIT_ITEMS_SCROLL",
		            Parent = "INFO_KIT_ITEMS",
		            DestroyUi = "INFO_KIT_ITEMS_SCROLL",
		            Components =
		            {
			            new CuiScrollViewComponent
			            {
				            MovementType = ScrollRect.MovementType.Elastic,
				            Horizontal = true,
				            Elasticity = 0.2f,
				            Inertia = true,
				            ScrollSensitivity = 13f,
				            ContentTransform = new CuiRectTransform
				            {
					            AnchorMax = "0 1",
					            OffsetMax = "%SCROLL_AMOUNT% 0"
				            },
			            },
			            new CuiImageComponent() { Color = "0 0 0 0" },
			            new CuiRectTransformComponent
			            {
				            AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "251.959 -117.254", OffsetMax = "-17.618 -16.466"
			            }
		            }
	            });

	            AddInterface("UI_KIT_ITEM_PANEL_SCROLL", container.ToJson());
            } 
            
            private void Building_InfoPanel_ItemsKit_InfoKit()
            {
	            CuiElementContainer container = new CuiElementContainer();
	            
	            container.Add(new CuiElement
	            {
		            Name = "ICON_KIT_INFO",
		            Parent = "INFO_KIT_ITEMS",
		            DestroyUi = "ICON_KIT_INFO",
		            Components =
		            {
			            new CuiRawImageComponent { Color = "1 1 1 1", Png = "%KIT_ICON%" },
			            new CuiRectTransformComponent
			            {
				            AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-327.933 0",
				            OffsetMax = "-287.933 40"
			            }
		            }
	            });

	            container.Add(new CuiElement
	            {
		            Name = "NAME_KIT_INFO",
		            Parent = "INFO_KIT_ITEMS",
		            DestroyUi = "NAME_KIT_INFO",
		            Components =
		            {
			            new CuiTextComponent
			            {
				            Text = "%NAME_KIT%", Font = "robotocondensed-bold.ttf", FontSize = 18,
				            Align = TextAnchor.UpperLeft, Color = "0.8941177 0.854902 0.8196079 1"
			            },
			            new CuiRectTransformComponent
			            {
				            AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-275.567 11.707",
				            OffsetMax = "-137.367 36.867"
			            }
		            }
	            });

	            container.Add(new CuiElement
	            {
		            Name = "STATUS_KIT_INFO",
		            Parent = "INFO_KIT_ITEMS",
		            DestroyUi = "STATUS_KIT_INFO",
		            Components =
		            {
			            new CuiTextComponent
			            {
				            Text = "%AVALIABLE_STATUS_KIT%", Font = "robotocondensed-regular.ttf", FontSize = 12,
				            Align = TextAnchor.UpperLeft, Color = "0.8941177 0.854902 0.8196079 1"
			            },
			            new CuiRectTransformComponent
			            {
				            AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-275.567 -3.747",
				            OffsetMax = "-137.367 17.747"
			            }
		            }
	            });

	            container.Add(new CuiElement
	            {
		            Name = "DESCRIPTION_KIT_INFO",
		            Parent = "INFO_KIT_ITEMS",
		            DestroyUi = "DESCRIPTION_KIT_INFO",
		            Components =
		            {
			            new CuiTextComponent
			            {
				            Text = "%DESCRIPTION_KIT%",
				            Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.UpperLeft,
				            Color = "0.8941177 0.854902 0.8196079 1"
			            },
			            new CuiRectTransformComponent
			            {
				            AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-327.93 -49.564",
				            OffsetMax = "-137.363 -5.769"
			            }
		            }
	            });

	            AddInterface("UI_KIT_ITEM_PANEL_INFO_KIT", container.ToJson());
            }

            private void Building_InfoPanel_ItemsKit_Item()
            {
	            CuiElementContainer container = new CuiElementContainer();
	           
	            container.Add(new CuiElement
	            {
		            Name = "BACKGROUN_ITEM",
		            Parent = "INFO_KIT_ITEMS_SCROLL",
		            Components = {
			            new CuiRawImageComponent { Color = "1 1 1 1", Png = _imageUI.GetImage("UI_INFO_PANEL_ITEM_BACKGROUND")},
			            new CuiRectTransformComponent { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "%OFFSET_MIN%", OffsetMax = "%OFFSET_MAX%" }
		            }
	            });

	            container.Add(new CuiElement
	            {
		            Name = "ITEM_AMOUNT",
		            Parent = "BACKGROUN_ITEM",
		            Components = {
			            new CuiTextComponent { Text = "x%AMOUNT%", Font = "robotocondensed-regular.ttf", FontSize = 8, Align = TextAnchor.LowerRight, Color = "0.8941177 0.854902 0.8196079 1" },
			            new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-18.564 -17.698", OffsetMax = "18.564 -6.435" }
		            }
	            });


	            AddInterface("UI_KIT_ITEM_PANEL_INFO_KIT_ITEM", container.ToJson());
            }
            
            private void Building_InfoPanel_ItemsKit_Item_Command()
            {
	            CuiElementContainer container = new CuiElementContainer();
	           
	            container.Add(new CuiElement
	            {
		            Name = "ITEM_ICON",
		            Parent = "BACKGROUN_ITEM",
		            Components = {
			            new CuiRawImageComponent { Color = "1 1 1 1", Png = "%ICON_ITEM%" },
			            new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-13.333 -13.333", OffsetMax = "13.333 13.333" }
		            }
	            });
	            
	            AddInterface("UI_KIT_ITEM_PANEL_INFO_KIT_ITEM_COMMAND_ICON", container.ToJson());
            }
            
            
            private void Building_InfoPanel_ItemsKit_EmptyItem()
            {
	            CuiElementContainer container = new CuiElementContainer();
	           
	            container.Add(new CuiElement
	            {
		            Name = "BACKGROUN_ITEM",
		            Parent = "INFO_KIT_ITEMS_SCROLL",
		            Components = {
			            new CuiRawImageComponent { Color = "1 1 1 1", Png = _imageUI.GetImage("UI_INFO_PANEL_NO_ITEMS")},
			            new CuiRectTransformComponent { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "%OFFSET_MIN%", OffsetMax = "%OFFSET_MAX%" }
		            }
	            });
	            
	            AddInterface("UI_KIT_ITEM_PANEL_INFO_KIT_EMPTY_ITEM", container.ToJson());
            }
            
                    }

		private static void ProcessContainer(List<Item> items, ContainerItem container, List<Configuration.ItemKits> parseItems)
		{
		    foreach (Item item in items)
		    {
		        Configuration.ItemKits itemKit = ParseItemInfo(item, container);
		        parseItems.Add(itemKit);
		    }
		}
	    public enum TypeKit
	    {
		    OnlyCooldown,
		    OnlyAmount,
		    CooldownAndAmount,
	    }
        
        [ChatCommand("kit")]
        private void KitCommandChat(BasePlayer player, String cmd, String[] args)
        {
	        if (!player.IsAdmin || args.Length == 0)
	        { 
		        DrawUI_Kit_Overlay(player);
		        return;
	        }

	        if (args.Length <= 1)
	        {
		        SendChat(GetLang("CHAT_ADMIN_COMMAND_KIT_NO_SYNTAX", player.UserIDString), player);
		        return;
	        }

	        String action = args[0];
	        switch (action)
	        {
		        case "add":
		        {
			        if (args.Length < 3)
			        {
				        SendChat(GetLang("CHAT_ADMIN_COMMAND_KIT_NO_SYNTAX", player.UserIDString), player);
				        return;
			        }
			        String categoryName = args[1].ToUpper();
			        String presetName = args[2].ToUpper();
			        
			        CreateKit(player, categoryName, presetName);
			        break;
		        }
		        case "remove":
		        {
			        if (args.Length < 2)
			        {
				        SendChat(GetLang("CHAT_ADMIN_COMMAND_KIT_NO_SYNTAX", player.UserIDString), player);
				        return;
			        }
			        String categoryName = args[1].ToUpper();
			        String presetName = args.Length > 2 ? args[2].ToUpper() : String.Empty;
			        
			        RemoveKit(player, categoryName, presetName);
			        break;
		        }
		        case "preset":
		        {
			        if (args.Length < 3)
			        {
				        SendChat(GetLang("CHAT_ADMIN_COMMAND_KIT_NO_SYNTAX", player.UserIDString), player);
				        return;
			        }
			        
			        String actionPreset = args[1];
			        String presetName = args[2].ToUpper();
			        switch (actionPreset)
			        {
				        case "add":
				        {
					        PresetCreate(player, presetName);
					        break;
				        }
				        case "edit":
				        {
					        PresetEdit(player, presetName);
					        break;
				        }
				        case "remove":
				        {
					        PresetRemove(player, presetName);
					        break;
				        }
			        }
			        break;
		        }
	        }
        }
	    private static InterfaceBuilder _interface;
		   		 		  						   					  						  						  		 			  	 	 
        
                
        private void PresetCreate(BasePlayer player, String presetName)
        {
	        List<Configuration.ItemKits> presetItems = ParseItemInInventory(player);
	        if (presetItems == null || presetItems.Count == 0)
	        {
				SendChat(GetLang("CHAT_ALERT_CREATE_PRESET_ERROR_NO_ITEMS", player.UserIDString), player);   
		        return;
	        }

	        if (!config.presetsItems.TryAdd(presetName, presetItems))
	        {
		        SendChat(GetLang("CHAT_ALERT_CREATE_PRESET_ERROR_CONTAINS_KEY", player.UserIDString), player);   
		        return;
	        }

	        SendChat(GetLang("CHAT_ALERT_CREATE_PRESET_SUCCESS", player.UserIDString, presetName, presetItems.Count), player);
	        SaveConfig();
        }
        
        private void OnServerInitialized()
        {
	        _imageUI = new ImageUI();
	        _imageUI.DownloadImage();

	        foreach (Configuration.CategoryInfo configCategoryKit in config.categoryKits.Values)
	        {
		        if (!permission.PermissionExists(configCategoryKit.permission))
			        permission.RegisterPermission(configCategoryKit.permission, this);
	        }

	        foreach (String autoKitPermission in config.autoKitController.settingAutoKits.listAutoKits.Keys)
	        {
		        if (!permission.PermissionExists(autoKitPermission))
			        permission.RegisterPermission(autoKitPermission, this);
	        }
        }
		   		 		  						   					  						  						  		 			  	 	 
        private void PresetEdit(BasePlayer player, String presetName)
        {
	        List<Configuration.ItemKits> presetItems = ParseItemInInventory(player);
	        if (presetItems == null || presetItems.Count == 0)
	        {
		        SendChat(GetLang("CHAT_ALERT_EDIT_PRESET_ERROR_NO_ITEMS", player.UserIDString), player);   
		        return;
	        }

	        if (!config.presetsItems.ContainsKey(presetName))
	        {
		        SendChat(GetLang("CHAT_ALERT_EDIT_PRESET_ERROR_NO_CONTAINS_KEY", player.UserIDString), player);   
		        return;
	        }

	        config.presetsItems[presetName] = presetItems;
	        
	        SendChat(GetLang("CHAT_ALERT_EDIT_PRESET_SUCCESS", player.UserIDString, presetName, presetItems.Count), player);
	        SaveConfig();
        }
	    
	    public enum ContainerItem
	    {
		    containerWear,
		    containerBelt,
		    containerMain
	    }



		
		
		private void AutoKitMoved(BasePlayer player)
		{
			if (!CheckTakeAutoKitOtherPlugins(player)) return;
			
			Configuration.AutoKitController autoKitConfig = config.autoKitController;
			if(autoKitConfig.stripInventoryDefaultItems)
				player.inventory.Strip();

			String kitKey = autoKitConfig.settingAutoKits.GetAutoKitKey(player);
			if (String.IsNullOrWhiteSpace(kitKey)) return;
			MovedItemsInPreset(player, kitKey);
		}
        
        
        
        private void OnPlayerRespawned(BasePlayer player) => AutoKitMoved(player);
        
                
        private void DrawUI_Kit_Overlay(BasePlayer player)
        {
            if (_interface == null) return;

            String Interface = InterfaceBuilder.GetInterface("UI_KIT_OVERLAY");
            if (Interface == null) return;

            Interface = Interface.Replace("%TITLE_KIT_OVERLAY%", GetLang("TITLE_KIT_OVERLAY", player.UserIDString));
            Interface = Interface.Replace("%DESCRIPTION_KIT_OVERLAY%", GetLang("DESCRIPTION_KIT_OVERLAY", player.UserIDString));
     
            AddUI(player, Interface);
            
            DrawUI_LoadedKits(player);
        }

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
                PrintWarning(LanguageEn
                    ? $"Error reading #54327 configuration 'oxide/config/{Name}', creating a new configuration!!"
                    : $"Ошибка чтения #54327 конфигурации 'oxide/config/{Name}', создаём новую конфигурацию!!");
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }
        
		private Boolean CheckTakeKitOtherPlugins(BasePlayer player)
		{
			Object CanReedeemKit = Interface.CallHook("CanRedeemKit", player);
			if (CanReedeemKit != null)
			{
				switch (CanReedeemKit)
				{
					case Boolean hookStatus:
						return hookStatus;
					case String hookStatus:
						return String.IsNullOrWhiteSpace(hookStatus);
				}
			}
			
			Object canReedeemKit = Interface.CallHook("canRedeemKit", player);
			if (canReedeemKit != null)
			{
				switch (canReedeemKit)
				{
					case Boolean hookStatus:
						return hookStatus;
					case String hookStatus:
						return String.IsNullOrWhiteSpace(hookStatus);
				}
			}

			return true;
		}
        
        private Boolean CheckTakeAutoKitOtherPlugins(BasePlayer player)
        {
	        Object CanRedeemAutoKit = Interface.CallHook("CanRedeemAutoKit", player);
	        if (CanRedeemAutoKit != null)
	        {
		        switch (CanRedeemAutoKit)
		        {
			        case Boolean hookStatus:
				        return hookStatus;
			        case String hookStatus:
				        return String.IsNullOrWhiteSpace(hookStatus);
		        }
	        }
	        
	        Object canRedeemAutoKit = Interface.CallHook("canRedeemAutoKit", player);
	        if (canRedeemAutoKit != null)
	        {
		        switch (canRedeemAutoKit)
		        {
			        case Boolean hookStatus:
				        return hookStatus;
			        case String hookStatus:
				        return String.IsNullOrWhiteSpace(hookStatus);
		        }
	        }

	        return true;
        }
	    private readonly List<UInt64> ItemIdCorrecteds = new List<UInt64> { 1074866732, 2004072627, 218363552, 2006957888, 930560607, 1123047824, 1130765085, 442289265, 090353317, 1383638, 118372687 }; 
	    
	    
	    private Boolean IsRaidBlocked(BasePlayer player, Boolean skip)
	    {
		    if (skip) return false;

		    if (RaidBlock)
			    return RaidBlock.Call<Boolean>("IsRaidBlocked", player);
		    return NoEscape && NoEscape.Call<Boolean>("IsRaidBlocked", player);
	    }

				
                
        
        private String GetLang(in String LangKey, in String userID = null, params Object[] args)
        {
	        if (args == null)
		        return lang.GetMessage(LangKey, this, userID);

	        StringBuilder sb = Pool.Get<StringBuilder>();

	        try
	        {
		        sb.AppendFormat(lang.GetMessage(LangKey, this, userID), args);
		        return sb.ToString();
	        }
	        finally
	        {
		        sb.Clear();
		        Pool.FreeUnmanaged(ref sb);
	        }
        }
	    private const Int32 elementWidth = 53;
		   		 		  						   					  						  						  		 			  	 	 
				
				
		
		
		private Tuple<Int32, IEnumerable<KeyValuePair<String, Configuration.CategoryInfo>>> GetSortedCategory(
			BasePlayer player, Int32 page)
		{
			Int32 countCategory = 0;
			IEnumerable<KeyValuePair<String, Configuration.CategoryInfo>> categoryKits;

			List<KeyValuePair<String, Configuration.CategoryInfo>> filtered = new();
			foreach (KeyValuePair<String, Configuration.CategoryInfo> entry in config.categoryKits)
			{
				if (entry.Value.hideCategoryPermission)
				{
					if (String.IsNullOrWhiteSpace(entry.Value.permission) ||
					    permission.UserHasPermission(player.UserIDString, entry.Value.permission))
						filtered.Add(entry);
				}
				else filtered.Add(entry);
			}

			categoryKits = filtered;
			countCategory = filtered.Count;


			categoryKits = config.sortedPermissions
				? categoryKits.OrderByDescending(x =>
					String.IsNullOrWhiteSpace(x.Value.permission) ||
					permission.UserHasPermission(player.UserIDString, x.Value.permission)).Skip(page * 4)
				: categoryKits.Skip(page * 4);

			return new Tuple<Int32, IEnumerable<KeyValuePair<String, Configuration.CategoryInfo>>>(countCategory,
				categoryKits);
		}
        public class CategoryInfo
        {
			public Dictionary<String, KitInfo> playerKitsTaked = new Dictionary<String, KitInfo>();
	        public class KitInfo
	        {
		        public Int32 amount;
		        public Double cooldown;
	        }
        }

        
        
        private Hash<String, Dictionary<String, CategoryInfo>> repositoryKits = new Hash<String, Dictionary<String, CategoryInfo>>();

                
        
        private static void DestroyUI(BasePlayer player, String elementName)
        {
            if (!player || player.net?.connection == null) return;
            CommunityEntity.ServerInstance.ClientRPC<String>(RpcTarget.Player("DestroyUI", player.net.connection), elementName);
        }
        
        private void DrawUI_Empty_Kit_Category(BasePlayer player, Int32 indexCategory)
        {
            if (_interface == null) return;
            
            String Interface = InterfaceBuilder.GetInterface("UI_KIT_EMPTY_CATEGORY");
            if (Interface == null) return;
		   		 		  						   					  						  						  		 			  	 	 
            Interface = Interface.Replace("%INDEX%", $"{indexCategory}");
            Interface = Interface.Replace("%OFFSET_MIN%", $"{-468.467 + (indexCategory * 234)} -171");
            Interface = Interface.Replace("%OFFSET_MAX%", $"{-231.8 + (indexCategory * 234)} 171");
            
            AddUI(player, Interface);
        }

		private void TakeKit(BasePlayer player, String categoryName, String kitKey, Int32 page)
		{
			if (!config.categoryKits.TryGetValue(categoryName, out Configuration.CategoryInfo categoryConfig) ||
			    !categoryConfig.kitList.TryGetValue(kitKey, out Configuration.KitInfo kitConfig))
				return;
		   		 		  						   					  						  						  		 			  	 	 
			CategoryInfo categoryInfo = GetOrCreateCategoryInfo(player, categoryName);
			CategoryInfo.KitInfo kitInfo = GetOrCreateKitInfo(categoryInfo, kitKey, kitConfig);

			if (kitConfig.type is TypeKit.OnlyAmount or TypeKit.CooldownAndAmount)
				kitInfo.amount++;

			if (kitConfig.type is TypeKit.OnlyCooldown or TypeKit.CooldownAndAmount)
				kitInfo.cooldown = CurrentTime() + (kitConfig.cooldown * 60);

			MovedItemsInPreset(player, kitKey);
			
			Int32 categoryCount = 0;
			String nameIndexKit = String.Empty;
			Tuple<Int32, IEnumerable<KeyValuePair<String, Configuration.CategoryInfo>>> sortedCategory = GetSortedCategory(player, page);
			
			foreach (KeyValuePair<String, Configuration.CategoryInfo> categorySorted in sortedCategory.Item2)
			{
				Int32 kitCount = 0;
				
				if(categoryCount >= 4) break;
				
				foreach (KeyValuePair<String, Configuration.KitInfo> kitList in categorySorted.Value.kitList)
				{
					nameIndexKit = $"{kitCount}_{categorySorted.Key}";
		   		 		  						   					  						  						  		 			  	 	 
					DrawUI_Update_KitPanel(player, nameIndexKit, page, categorySorted.Key, kitList);
					if (kitList.Value.type is TypeKit.OnlyAmount or TypeKit.CooldownAndAmount)
						DrawUI_Update_KitAmount(player, nameIndexKit, categorySorted.Key, kitList);
					
					kitCount++;
				}
		   		 		  						   					  						  						  		 			  	 	 
				categoryCount++;
			}
			
			Interface.CallHook("OnKitRedeemed", player, kitKey);
		}
        
        private void DrawUI_Kit_PagePanel(BasePlayer player, Int32 page = 0)
        {
            if (_interface == null) return;
            
            String Interface = InterfaceBuilder.GetInterface("UI_KIT_OVERLAY_PAGE_PANEL");
            if (Interface == null) return;

            Int32 currentPage = page + 1;
            Int32 maxPage = (Int32)Math.Ceiling(config.categoryKits.Count / 4.0);
            
            String nextPageCommand = $"ui_kit_func page.controller {page + 1}";  
            String nextColorPage = "1 1 1 1";

            String backPageCommand = $"ui_kit_func page.controller {page - 1}";   
            String backColorPage = "1 1 1 1";
            
            if (page >= maxPage - 1)
            {
	            nextPageCommand = String.Empty;
	            nextColorPage = "1 1 1 0.5";
            }

            if (page <= 0)
            {
	            backPageCommand = String.Empty;
	            backColorPage = "1 1 1 0.5";
            }
            
            Interface = Interface.Replace("%PAGE_TITLE%", String.Format(pageFormat, currentPage, maxPage));
            Interface = Interface.Replace("%PAGE_RIGHT_COMMAND%", nextPageCommand);
            Interface = Interface.Replace("%PAGE_LEFT_COMMAND%", backPageCommand);
            Interface = Interface.Replace("%PAGE_LEFT_COLOR%", backColorPage);
            Interface = Interface.Replace("%PAGE_RIGHT_COLOR%", nextColorPage);
            
            AddUI(player, Interface);
        }
        private void WriteData() => Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("IQSystem/IQKits/InfoPlayers", repositoryKits);

		private CategoryInfo GetOrCreateCategoryInfo(BasePlayer player, String categoryName)
		{
			if (!repositoryKits.TryGetValue(player.UserIDString, out Dictionary<String, CategoryInfo> playerCategories))
				repositoryKits[player.UserIDString] = playerCategories = new Dictionary<String, CategoryInfo>();

			if (!playerCategories.TryGetValue(categoryName, out CategoryInfo categoryInfo))
				playerCategories[categoryName] = categoryInfo = new CategoryInfo { playerKitsTaked = new Dictionary<String, CategoryInfo.KitInfo>() };

			return categoryInfo;
		}
        
        private void DrawUI_Kit_Panel_Amount(BasePlayer player, String indexName, Int32 leftAmount)
        {
            if (_interface == null) return;
            
            String Interface = InterfaceBuilder.GetInterface("UI_KIT_PANEL_AMOUNT_BLOCK");
            if (Interface == null) return;

            Interface = Interface.Replace("%INDEX%", $"{indexName}");
            Interface = Interface.Replace("%AMOUNT%", $"{leftAmount}");

            AddUI(player, Interface);
        }     
	    public enum BiomeType
	    {
		    None,
		    Arid,
		    Temperate,
		    Tundra,
		    Arctic
	    }

        private void Unload()
        {
	        if (_ == null) return;
	        
	        WriteData();
	        
	        InterfaceBuilder.DestroyAll();
                        
	        if (_imageUI != null)
	        {
		        _imageUI.UnloadImages();
		        _imageUI = null;
	        }
	        
	        _ = null;
        }
	    private static ImageUI _imageUI;
        
        private void DrawUI_Update_KitAmount(BasePlayer player, String indexName, String categoryName, KeyValuePair<String, Configuration.KitInfo> kitInfo)
        {
            if (_interface == null) return;
            
            String Interface = InterfaceBuilder.GetInterface("UI_KIT_UPDATE_INFO_AMOUNT");
            if (Interface == null) return;
            
            Interface = Interface.Replace("%INDEX%", $"{indexName}");
            Interface = Interface.Replace("%AMOUNT%", $"{GetLeftAmountKit(player, categoryName, kitInfo.Key, kitInfo.Value.amount)}"); 

            AddUI(player, Interface);
        }
        
        [ConsoleCommand("ui_kit_func")]
        private void ConsoleCommandUIKitFunc(ConsoleSystem.Arg arg)
        {
	        BasePlayer player = arg.Player(); 
	        if (!player) return;
	        if (!arg.HasArgs()) return;
	        
	        String action = arg.Args[0];

	        switch (action)
	        { 
		        case "take.kit":
		        {
			        String categoryName = arg.Args[1];
			        String itemKey = arg.Args[2];
			        Int32 page = Int32.Parse(arg.Args[3]);

			        String getStatusKit = GetStatusAvaliable(player, categoryName, itemKey);
			        if (!String.IsNullOrWhiteSpace(getStatusKit))
				        return;
			        
			        TakeKit(player, categoryName, itemKey, page);
			        break;
		        }
		        case "open.info.kit":
		        {
			        String categoryKey = arg.Args[1];
			        String kitKey = arg.Args[2];
			        DrawUI_InfoKit_Panel(player, categoryKey, kitKey);
			        break;
		        }
		        case "page.controller":
		        {
			        Int32 page = Int32.Parse(arg.Args[1]);
			        if (page < 0) return;
			        
			        DrawUI_LoadedKits(player, page);
			        break;
		        }
	        }
        }

                
        private void ValidateDataFromConfig() //TODO: Потестить очень-очень
        {
	        List<String> repositoriesToRemove = new();

	        foreach (var repository in repositoryKits)
	        {
		        List<String> categoriesToRemove = new();
		        foreach (var category in repository.Value)
		        {
			        if (!config.categoryKits.ContainsKey(category.Key))
			        {
				        categoriesToRemove.Add(category.Key);
				        continue; 
			        }

			        List<String> kitsToRemove = new();

			        foreach (KeyValuePair<String, CategoryInfo.KitInfo> kitInfo in category.Value.playerKitsTaked)
			        {
				        if (!config.presetsItems.ContainsKey(kitInfo.Key))
					        kitsToRemove.Add(kitInfo.Key);
			        }

			        foreach (String kitKey in kitsToRemove)
				        repositoryKits[repository.Key][category.Key].playerKitsTaked.Remove(kitKey);
		        }

		        foreach (String categoryKey in categoriesToRemove)
			        repositoryKits[repository.Key].Remove(categoryKey);

		        if (repositoryKits[repository.Key].Count == 0)
			        repositoriesToRemove.Add(repository.Key);
	        }

	        foreach (String repositoryKey in repositoriesToRemove)
		        repositoryKits.Remove(repositoryKey);
        }

        protected override void LoadDefaultConfig() => config = Configuration.GetNewConfiguration();


	    private void ReadData() => repositoryKits = Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<Hash<String, Dictionary<String, CategoryInfo>>>("IQSystem/IQKits/InfoPlayers");
        
        private void Init()
        {
	        _ = this;
	        
	        ReadData();
	        
	        if(!config.autoKitController.useAutoKit)
		        Unsubscribe(nameof(OnPlayerRespawned));
	        
	        ValidateDataFromConfig();
        }
            }
}
