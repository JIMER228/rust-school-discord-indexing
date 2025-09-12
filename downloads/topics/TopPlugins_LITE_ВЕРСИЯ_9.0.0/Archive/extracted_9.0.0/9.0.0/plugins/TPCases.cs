using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using System.Text;
using ConVar;
using UnityEngine.Networking;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Object = System.Object;

namespace Oxide.Plugins
{ 
    [Info("TPCases", "ds:alone_sempai / https://topplugin.ru/", "9.0.0")]
    [Description("Кейсы на ваш сервер")]
    public class TPCases : RustPlugin
    {
	    
	    #region Reference

	    [PluginReference] Plugin ImageLibrary, IQEconomic, Economics, ServerRewards, IQChat, Battles, Duel, Duelist, ArenaTournament, AimTraining, XFarmRoom, IQRankSystem, TPEconomic;

	    #region ImageLibrary

	    private String GetImage(String fileName, UInt64 skin = 0)
	    {
		    String imageId = (String)plugins.Find("ImageLibrary").CallHook("ImageUi.GetImage", fileName, skin);
		    return !String.IsNullOrEmpty(imageId) ? imageId : String.Empty;
	    }

	    #endregion

	    [ConsoleCommand("CloseTPCasesMenu423")]
	    void casf(ConsoleSystem.Arg args)
	    {
	    	if (args.Player() == null)
                return;

            BasePlayer player = args.Player();

	    	if (LocalRepositoryPlayer.ContainsKey(player))
			    LocalRepositoryPlayer.Remove(player);
			CuiHelper.DestroyUi(player, "Menu_UI");
	    }

	    #region IQEconomic / Economics / ServerRewards / TPEconomic
	    
	    private void SetBalance(UInt64 userID, Int32 Balance)
	    {
		    if (IQEconomic != null)
			    IQEconomic?.Call("API_SET_BALANCE", userID, Balance);
			else if (TPEconomic != null)
				TPEconomic?.Call("API_PUT_BALANCE_PLUS", userID, (float)Balance);
		    else if (Economics != null)
			    Economics?.Call("Deposit", userID, (Double)Balance);
		    else if (ServerRewards != null)
			    ServerRewards?.Call("AddPoints", userID, Balance);
	    }
	    private Int32 GetBalance(UInt64 userID)
	    {
		    if (IQEconomic != null)
			    return (Int32)IQEconomic?.Call("API_GET_BALANCE", userID);
			else if (TPEconomic != null)
			    return Convert.ToInt32((float)TPEconomic?.Call("API_GET_BALANCE", userID));
		    else if (Economics != null)
			    return Convert.ToInt32((Double)Economics?.Call("Balance", userID));
		    else if (ServerRewards != null)
		    {
			    Object Coins = ServerRewards.Call("CheckPoints", userID);
			    return Coins == null ? 0 : Convert.ToInt32(Coins);
		    }

		    return 0;
	    }

	    private Boolean IsRemovedBalance(UInt64 userID, Int32 Amount)
	    {
		    if (IQEconomic != null)
			    return (Boolean)IQEconomic?.Call("API_IS_REMOVED_BALANCE", userID, Amount);
		    else if (TPEconomic != null)
			    return GetBalance(userID) >= Amount;    
		    else if (Economics != null || ServerRewards)
			    return GetBalance(userID) >= Amount;
		    return false;
	    }

	    private void RemoveBalance(UInt64 userID, Int32 Balance)
	    {
		    if (IQEconomic != null)
			    IQEconomic?.Call("API_REMOVE_BALANCE", userID, Balance);
			else if (TPEconomic != null)
			    TPEconomic?.Call("API_PUT_BALANCE_MINUS", userID, (float)Balance);
		    else if (Economics != null)
			    Economics?.Call("Withdraw", userID, Convert.ToDouble(Balance));
		    else if (ServerRewards != null)
			    ServerRewards?.Call("TakePoints", userID, Balance);
	    }

	    #endregion

	    #region IQChat

	    private void SendChat(String Message, BasePlayer player, Chat.ChatChannel channel = Chat.ChatChannel.Global)
	    {
		    Configuration.ReferencePlugins.IQChatPreset ChatPrest = config.ReferencePluginsPreset.IQChatSettings;
		    if (IQChat)
			    if (ChatPrest.UIAlertUse)
				    IQChat?.Call("API_ALERT_PLAYER_UI", player, Message);
			    else IQChat?.Call("API_ALERT_PLAYER", player, Message, ChatPrest.CustomPrefix, ChatPrest.CustomAvatar);
		    else player.SendConsoleCommand("chat.add", channel, 0, Message);
	    }

	    #endregion

	    #region Duels

	    public Boolean IsDuel(UInt64 userID)
	    {
		    Object obj = Interface.Oxide.RootPluginManager.GetPlugin("AimTraining")?.CallHook("IsArenaPlayer", userID);
		    if (obj is Boolean)
			    return (Boolean)obj;
		    else if (Battles)
			    return (Boolean)Battles?.Call("IsPlayerOnBattle", userID);
		    else if (Duel) return (Boolean)Duel?.Call("IsPlayerOnActiveDuel", BasePlayer.FindByID(userID));
		    else if (Duelist) return (Boolean)Duelist?.Call("inEvent", BasePlayer.FindByID(userID));
		    else if (ArenaTournament) return ArenaTournament.Call<Boolean>("IsOnTournament", userID);
		    else if (XFarmRoom) return XFarmRoom.Call<Boolean>("API_PlayerInRoom", userID);
		    else return false;
	    }

	    #endregion

	    #region IQRankSystem

	    private Boolean IsRank(UInt64 userID, String Key)
	    {
		    if (!IQRankSystem) return false;
		    return (Boolean)IQRankSystem?.Call("API_GET_AVAILABILITY_RANK_USER", userID, Key);
	    }
	    private String GetRankName(String Key)
	    {
		    String Rank = String.Empty;
		    if (!IQRankSystem) return Rank;
		    return (String)IQRankSystem?.Call("API_GET_RANK_NAME", Key); 
	    }

	    #endregion

	    #endregion
	    
	    #region Vars

	    private Boolean FullInit = false;
	    private const Boolean LanguageEn = true;

	    private static TPCases _;
	    private static InterfaceBuilder _interface;
	    
	    private List<String> CaseKeys = new List<String>();
	    private Dictionary<BasePlayer, LocalRepository> LocalRepositoryPlayer = new Dictionary<BasePlayer, LocalRepository>();
	    private List<UInt64> LootersListCrateID = new List<UInt64>();

	    private class LocalRepository
	    {
		    private Double CooldownTakeItem;
		    private Double CooldownEffects;
		    public Boolean IsOpenedCase;
		    
		    public Boolean IsCooldownTake => CooldownTakeItem >= CurrentTime;
		    public void SetCooldown() => CooldownTakeItem = CurrentTime + 1f;
		    
		    public Boolean IsCooldownEffect => CooldownEffects >= CurrentTime;
		    public void SetCooldownEffect() => CooldownEffects = CurrentTime + 1f;
	    }

	    static Double CurrentTime => Facepunch.Math.Epoch.Current;

	    public enum TypeReward
	    {
		    Item,
		    Command
	    }
	    
	    #endregion

	    #region Configuration

	    private static Configuration config = new Configuration();

	    private class Configuration
	    {
		    [JsonProperty(LanguageEn ? "Using logging for player case openings." : "Использовать логирование открытия кейсов игроком")]
		    public Boolean UseLogs;
		    [JsonProperty(LanguageEn ? "Plugin interface customization" : "Настройка интерфейса плагина")]
		    public Interface InterfaceSettings = new Interface();
		    [JsonProperty(LanguageEn ? "Settings cases" : "Настройка кейсов")]
		    public Dictionary<String, CasesPreset> CasesPresets = new Dictionary<String, CasesPreset>();
		    [JsonProperty(LanguageEn ? "Setting up compatible plugins" : "Настройка совместимых плагинов")]
		    public ReferencePlugins ReferencePluginsPreset = new ReferencePlugins();
		    [JsonProperty(LanguageEn ? "Setting the starting number of cases for the player" : "Настройка стартового количества кейсов для игрока")]
		    public List<DefaultCasePlayer> DefaultCasePlayers = new List<DefaultCasePlayer>();
		    [JsonProperty(LanguageEn ? "Case drop setting (supports looting and destroying objects) : [ShortPrefabName] = Setting" : "Настройка выпадения кейсов (поддерживает лутание и уничтожение объектов) : [ShortPrefabName] = Настройка")]
		    public Dictionary<String, List<DropCases>> DropCasesList = new Dictionary<String, List<DropCases>>();
		    internal class DropCases
		    {
			    [JsonProperty(LanguageEn ? "Case drop chance" : "Шанс выпадения кейса")]
			    public Int32 Rare;
			    [JsonProperty(LanguageEn ? "Number of cases to issue" : "Количество кейсов для выдачи")]
			    public Int32 Count;
			    [JsonProperty(LanguageEn ? "Permission to receive a case (environment empty - will be available to everyone)" : "Права для получения кейса (оставьте пустым - будет доступно всем)")]
			    public String Permissions;
			    [JsonProperty(LanguageEn ? "Case key to issue (from the list of your cases)" : "Ключ кейса для выдачи (из списка ваших кейсов)")]
			    public String CaseKey;
		    }
		    internal class DefaultCasePlayer
		    {
			    [JsonProperty(LanguageEn ? "Case key to issue (from the list of your cases)" : "Ключ кейса для выдачи (из списка ваших кейсов)")]
			    public String CaseKey;
			    [JsonProperty(LanguageEn ? "Permissions for obtaining cases (it will work if the player receives these rights or enters the server with them) (leave blank - will be available to everyone)" : "Права для получения кейсов (сработает если игрок получит эти права или зайдет с ними на сервер) (остаьвте пустым - будет доступно всем)")]
			    public String Permissions;
			    [JsonProperty(LanguageEn ? "Number of cases to issue" : "Количество кейсов для выдачи")]
			    public Int32 Count;
		    }

		    internal class ReferencePlugins
		    {
			    [JsonProperty(LanguageEn ? "Setting up IQChat" : "Настройка IQChat'a")]
			    public IQChatPreset IQChatSettings = new IQChatPreset();
			    internal class IQChatPreset
			    {
				    [JsonProperty(LanguageEn ? "IQChat : Custom chat prefix" : "IQChat : Кастомный префикс в чате")]
				    public String CustomPrefix;
				    [JsonProperty(LanguageEn ? "IQChat : Custom chat avatar (If required)" : "IQChat : Кастомный аватар в чате(Если требуется)")]
				    public String CustomAvatar;
				    [JsonProperty(LanguageEn ? "IQChat : Use UI Notifications" : "IQChat : Использовать UI уведомления")]
				    public Boolean UIAlertUse;
			    }
		    }
		    internal class CasesPreset
		    {
			    [JsonProperty(LanguageEn ? "Link to case picture (350x350)" : "Ссылка на картинку кейса (350x350)")]
			    public String PNGCase;
			    [JsonProperty(LanguageEn ? "Setting up case names (if you don't need them, leave the fields blank)" : "Настройка названий кейсов (если они вам не нужны - оставьте поля пустыми)")]
			    public NameCases caseName = new NameCases();
			    
			    internal class NameCases
			    {
				    [JsonProperty(LanguageEn ? "Case name in Russia" : "Название кейса на русском")]
				    public String NameRussia;
				    [JsonProperty(LanguageEn ? "Case name in English" : "Название кейса на английской")]
				    public String NameEnglish;

				    public String GetCaseName(BasePlayer player)
				    {
					    String NameResult = String.Empty;
					    if (_.lang.GetLanguage(player.UserIDString).Equals("ru") &&
					        !String.IsNullOrWhiteSpace(NameRussia))
						    NameResult = NameRussia;
					    else if (!String.IsNullOrWhiteSpace(NameEnglish))
						    NameResult = NameEnglish;

					    return NameResult;
				    }
			    }

			    [JsonProperty(LanguageEn ? "Setting up the sale and purchase of cases (IQEconomic/Economics/TPEconomic)" : "Настройка продажи и покупки кейсов (IQEconomic/Economics/TPEconomic)")]
			    public EconomicSetting EconomicSettings = new EconomicSetting();
			    [JsonProperty(LanguageEn ? "Setting Restrictions" : "Настройка ограничений")]
			    public RestrictionsPreset RestrictionsSettings = new RestrictionsPreset();
			    [JsonProperty(LanguageEn ? "Setting rewards from a case" : "Настройка наград из кейса")]
			    public List<RewardPreset> RewardPresets = new List<RewardPreset>();
				
			    public RewardPreset GetRandomReward()
			    {
				    Int32 RandomPlayerRare = Oxide.Core.Random.Range(0, 100);
				    RewardPreset RandomReward = null;
				    
				    foreach (RewardPreset rewardPreset in RewardPresets)
				    {					    
					    RewardPreset RewardTry = RewardPresets.GetRandom();
					    if (RandomPlayerRare >= RewardTry.Rare) continue;
					    
					    RandomReward = RewardTry;
					    break;
				    }

				    return RandomReward ?? (RandomReward = GetRandomReward());
			    }

			    internal class RestrictionsPreset
			    {
				    [JsonProperty(LanguageEn ? "Permission that give the opportunity to open a case (leave the field empty - it will be available to everyone)" : "Права дающие возможность открыть кейс (оставьте поле пустым - будет доступен для всех)")]
				    public String Permissions;
				    [JsonProperty(LanguageEn ? "IQRankSystem : Rank giving the opportunity to open a case (leave the field blank - it will be available to everyone)" : "IQRankSystem : Ранг дающий возможность открыть кейс (оставьте поле пустым - будет доступен для всех)")]
				    public String IQRankKey;
			    }
			    internal class RewardPreset
			    {
				    [JsonProperty(LanguageEn ? "Select reward type: 0 - Item, 1 - Command" : "Выберите тип награды : 0 - Предмет, 1 - Команда")]
				    public TypeReward Type;
				    [JsonProperty(LanguageEn ? "Chance of dropping this reward" : "Шанс выпадения данной награды")]
				    public Int32 Rare;
				    [JsonProperty(LanguageEn ? "The displayed chance of getting this reward" : "Отображаемый шанс выпадения данной награды")]
				    public Int32 VisualRare;
				    [JsonProperty(LanguageEn ? "PNG for reward (85x85) (if not required - leave blank) (not required for regular items)" : "PNG для награды (85x85) (если не требуется - оставляйте пустым) (не требуется для обычных предметов)")]
				    public String PNGReward;
				    
				    [JsonProperty(LanguageEn ? "Item setting (if item type is selected)" : "Настройка предмета (если выбран тип Предмет)")]
				    public ItemPreset ItemSetting = new ItemPreset();
				    [JsonProperty(LanguageEn ? "Command setting (if Command type is selected)" : "Настройка команды (если выбран тип Команда)")]
				    public CommandPreset CommandSetting = new CommandPreset();

				    #region CommandPreset

				    internal class CommandPreset
				    {
					    [JsonProperty(LanguageEn
						    ? "Item name in UI (if left blank, values from lang will be used)"
						    : "Название предмета в UI (если оставить пустым будут браться значения из lang)")]
					    public String DisplayName;
					    [JsonProperty(LanguageEn ? "Console command (%STEAMID% - will be replaced by the player's Steam64ID)" : "Консольная команда (%STEAMID% - заменится на Steam64ID игрока)")]
					    public String Command;
					    [JsonProperty(LanguageEn ? "Seconds. Time for which the reward will be issued - leave 0 if this award is not issued for a while and fill in the field below (only affects the visual display)" : "Секунды. Время на какое будет выдана награда - оставьте 0 если данная награда не выдается на время и заполните поле ниже(влияет только на визуальное отображение)")]
					    public Int32 SecondCommand;
					    [JsonProperty(LanguageEn ? "Amount. If it is a custom item or a reward that is issued in N quantity (affects visual display only)" : "Количество. Если это кастомный предмет или награда, которая выдается в N количестве (влияет только на визуальное отображение)")]
					    public Int32 ItemAmount;

					    public String GetAmountLabel(BasePlayer player) => SecondCommand == 0 ? $"x{ItemAmount}" : _.FormatTime(TimeSpan.FromSeconds(SecondCommand), player.UserIDString);
				    }

				    #endregion

				    #region ItemPreset

				    internal class ItemPreset
				    {
					    [JsonProperty(LanguageEn
						    ? "Item name in UI (if left blank, values from lang will be used)"
						    : "Название предмета в UI (если оставить пустым будут браться значения из lang)")]
					    public String DisplayName;
					    [JsonProperty(LanguageEn ? "Is this a blueprint? true - yes/false - no" : "Это чертеж? true - да/false - нет")]
					    public Boolean IsBlueprint;
					    [JsonProperty(LanguageEn ? "Shortname item" : "Shortname предмета")]
					    public String Shortname;
					    [JsonProperty(LanguageEn ? "SkinID item" : "SkinID предмета")]
					    public UInt64 SkinID;
					    [JsonProperty(LanguageEn ? "Quantity setting (if you need a static value - set both fields to the same value)" : "Настройка количества (если вам нужно статичное значение - установите в обоих полях одинаковое значение)")]
					    public AmountPreset Amounts = new AmountPreset();

					    internal class AmountPreset
					    {
						    [JsonProperty(LanguageEn ? "Min amount" : "Минимальное количество")]
						    public Int32 MinAmount;
						    [JsonProperty(LanguageEn ? "Max amount" : "Максимальное количество")]
						    public Int32 MaxAmount;

						    [JsonIgnore]
						    public String GetAmountLabel => MinAmount == MaxAmount ? $"x{MinAmount}" : $"x{MinAmount}-{MaxAmount}";
						    [JsonIgnore]
						    public Int32 GetAmount => MinAmount == MaxAmount ? MinAmount : Oxide.Core.Random.Range(MinAmount, MaxAmount);
					    }
				    }

				    #endregion
			    }

			    internal class EconomicSetting
			    {
				    [JsonProperty(LanguageEn ? "Case purchase price (If set to 0 - the case cannot be bought)" : "Цена покупки кейса (Если установить 0 - кейс нельзя будет купить)")]
				    public Int32 PriceBuyCase;

				    [JsonProperty(LanguageEn ? "Case sale price (If set to 0 - the case cannot be sold)" : "Цена продажи кейса (Если установить 0 - кейс нельзя будет продать)")]
				    public Int32 PriceSellCase;

				    [JsonIgnore]
				    public Boolean IsSell => PriceSellCase > 0;
				    [JsonIgnore]
				    public Boolean IsBuy => PriceBuyCase > 0;
			    }
		    }

		    internal class Interface
		    {
			    [JsonProperty(LanguageEn ? "Use sound effects in conjunction with the interface" : "Использовать совместно с интерфейсом звуковые эффекты")]
			    public Boolean UseEffects;
			    [JsonProperty(LanguageEn ? "PNG: Toggle switch turned left (35х20)" : "PNG: Тумблер включенный влево (35х20)")]
			    public String TumblerLeft;
			    [JsonProperty(LanguageEn ? "PNG: Toggle switch turned right (35х20)" : "PNG: Тумблер включенный вправо (35х20)")]
			    public String TumblerRight;
			    [JsonProperty(LanguageEn ? "PNG: Back panel of information about the number of cases (277x46)" : "PNG: Задняя панель информации о количестве кейсов (277x46)")]
			    public String PanelCasesInfoAmount;
			    [JsonProperty(LanguageEn ? "PNG: Back panel of inventory button (178x46)" : "PNG: Задняя панель кнопки инвентаря (178x46)")]
			    public String PanelCasesInventoryPanel;
			    [JsonProperty(LanguageEn ? "PNG: Back panel of balance information (122x46)" : "PNG: Задняя панель информации о балансе (122x46)")]
			    public String PanelCasesBalancePanel;		
			    [JsonProperty(LanguageEn ? "PNG: Glow effect for cases (909x377)" : "PNG: Эффект свечения для кейсов (909x377)")]
			    public String EffectCases;
			    [JsonProperty(LanguageEn ? "PNG: End cap (if there is no item in the case) (143x155)" : "PNG: Заглушка (если нет предмета в кейсе) (143x155)")]
			    public String NoRareBackground;
			    [JsonProperty(LanguageEn ? "PNG: Back page (60x60)" : "PNG: Страница назад (60x60)")]
			    public String BackPage;
			    [JsonProperty(LanguageEn ? "PNG: Next page (60x60)" : "PNG: Страница вперед (60x60)")]
			    public String NextPage;
			    [JsonProperty(LanguageEn ? "PNG: Button 'Open Case ' (350x46)" : "PNG: Кнопка 'Открыть Кейс' (350x46)")]
			    public String OpenCaseButton;
			    [JsonProperty(LanguageEn ? "PNG: 'Buy' button (172x46)" : "PNG: Кнопка 'Купить' (172x46)")]
			    public String BuyCaseButton;
			    [JsonProperty(LanguageEn ? "PNG: 'Sell' button (172x46)" : "PNG: Кнопка 'Продать (172x46)")]
			    public String SellCaseButton;
			    [JsonProperty(LanguageEn ? "PNG: Button Lock Picture (172x46)" : "PNG: Картинка для блокировки кнопок (172x46)")]
			    public String BlockPanelButton;			
			    [JsonProperty(LanguageEn ? "PNG: The picture next to the displayed chance (13x13)" : "PNG: Картинка возле отображаемого шанса (13x13)")]
			    public String RareMiniIcon;
			    [JsonProperty(LanguageEn ? "PNG: Images that will be displayed at the reward depending on the DISPLAYED CHANCE of falling out [Chance(0-100)] - PNG (143x155) (The order of filling: From greater to lesser chance)" : "PNG: Картинки которые будут отображаться у награды зависимо от ОТОБРАЖАЕМОГО ШАНСА выпадения [Шанс(0-100)] - PNG (143x155) (Порядок заполнения : От большего к меньшему шансу)")]
			    public Dictionary<Int32, String> RareBackground = new Dictionary<Int32, String>();

		    }
		    public static Configuration GetNewConfiguration()
		    {
			    return new Configuration
			    {
				    UseLogs = false,
					InterfaceSettings = new Interface()
					{
						UseEffects = true,
						TumblerLeft = "https://i.ibb.co/TYFkDHP/1222.png",
						TumblerRight = "https://i.ibb.co/2SqX63m/2-1.png", 
						PanelCasesInfoAmount = "https://i.postimg.cc/sgP6msKt/CVwUO8y.png",
						PanelCasesInventoryPanel = "https://i.postimg.cc/MZCJrZbT/MmB7paC.png",
						PanelCasesBalancePanel = "https://i.postimg.cc/BvS0ch5B/61im43h.png",
						EffectCases = "https://i.postimg.cc/tJWWbdS7/ZURvF4x.png",
						NoRareBackground = "https://i.postimg.cc/J0VHpDr8/uHTdwjY.png",
						BackPage = "https://i.postimg.cc/9FBwBQv3/5iHnDXC.png",
						NextPage = "https://i.postimg.cc/sx4xKTHP/LR2j88f.png",
						OpenCaseButton = "https://i.postimg.cc/jdqStQk9/zvtUYCS.png",
						BuyCaseButton = "https://i.postimg.cc/dVkvVWRW/hV4Xksf.png",
						SellCaseButton = "https://i.postimg.cc/k4cPpL95/OFjBLhn.png",
						BlockPanelButton = "https://i.postimg.cc/QMZLxjdt/8KOy2pU.png",
						RareMiniIcon = "https://i.ibb.co/gSrn52J/434.png",
						RareBackground = new Dictionary<Int32, String>()
						{
							[50] = "https://i.postimg.cc/ZYMM5JfS/CR545Ie.png",
							[25] = "https://i.postimg.cc/SNYtXH5z/VIM8jSa.png",
							[10] = "https://i.postimg.cc/J0fxc4gH/fz3Foqr.png",
							[0] = "https://i.postimg.cc/J0fxc4gH/fz3Foqr.png"
						}
					},
					ReferencePluginsPreset = new ReferencePlugins
					{
						IQChatSettings = new ReferencePlugins.IQChatPreset
						{
							CustomPrefix = "<color=#F6C0DC>[TPCases]</color>\n",
							CustomAvatar = "0",
							UIAlertUse = false
						}
					},
					DefaultCasePlayers = new List<DefaultCasePlayer>()
					{
						new DefaultCasePlayer()
						{
							CaseKey = "free_case",
							Permissions = "",
							Count = 3,
						},
						new DefaultCasePlayer()
						{
							CaseKey = "raiders_case",
							Permissions = "tpcases.raider",
							Count = 1,
						},
					},
					DropCasesList = new Dictionary<String, List<DropCases>>()
					{
						["crate_elite"] = new List<DropCases>()
						{
							new DropCases
							{
								Rare = 10,
								Count = 1,
								Permissions = "",
								CaseKey = "components_case"
							},
							new DropCases
							{
								Rare = 5,
								Count = 1,
								Permissions = "",
								CaseKey = "blueprints_case"
							},
							new DropCases
							{
								Rare = 5,
								Count = 1,
								Permissions = "tpcases.vip",
								CaseKey = "raiders_case"
							},
						}, 
						["loot-barrel-1"] = new List<DropCases>()
						{
							new DropCases
							{
								Rare = 10,
								Count = 1,
								Permissions = "",
								CaseKey = "components_case"
							},
						}
					},
					CasesPresets = new Dictionary<String, CasesPreset>()
					{
						#region FreeCase
						
						["free_case"] = new CasesPreset
						{
							caseName = new CasesPreset.NameCases()
							{
								NameRussia = "",
								NameEnglish = ""
							},
							PNGCase = "https://i.postimg.cc/Dzv9bB1k/fz3-Foffffffqr.png",
							EconomicSettings = new CasesPreset.EconomicSetting
							{
								PriceBuyCase = 0,
								PriceSellCase = 0
							},
							RestrictionsSettings = new CasesPreset.RestrictionsPreset
							{
								Permissions = "",
								IQRankKey = ""
							},
							RewardPresets = new List<CasesPreset.RewardPreset>
							{
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Item,
									Rare = 79,
									VisualRare = 70,
									PNGReward = "",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = false,
										Shortname = "wood",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 3000,
											MaxAmount = 5000,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "",
										SecondCommand = 0,
										ItemAmount = 0
									}
								},
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Item,
									Rare = 60,
									VisualRare = 60,
									PNGReward = "",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = false,
										Shortname = "stones",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 1000,
											MaxAmount = 3000,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "",
										SecondCommand = 0,
										ItemAmount = 0
									}
								},
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Item,
									Rare = 30,
									VisualRare = 30,
									PNGReward = "",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = false,
										Shortname = "metal.fragments",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 500,
											MaxAmount = 2000,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "",
										SecondCommand = 0,
										ItemAmount = 0
									}
								},
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Item,
									Rare = 15,
									VisualRare = 30,
									PNGReward = "",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = false,
										Shortname = "icepick.salvaged",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 1,
											MaxAmount = 1,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "",
										SecondCommand = 0,
										ItemAmount = 0
									}
								},
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Item,
									Rare = 50,
									VisualRare = 30,
									PNGReward = "",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = false,
										Shortname = "grenade.f1",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 1,
											MaxAmount = 3,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "",
										SecondCommand = 0,
										ItemAmount = 0
									}
								},
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Item,
									Rare = 30,
									VisualRare = 35,
									PNGReward = "",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = false,
										Shortname = "flamethrower",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 1,
											MaxAmount = 1,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "",
										SecondCommand = 0,
										ItemAmount = 0
									}
								},
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Item,
									Rare = 60,
									VisualRare = 45,
									PNGReward = "",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = false,
										Shortname = "pistol.revolver",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 1,
											MaxAmount = 1,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "",
										SecondCommand = 0,
										ItemAmount = 0
									}
								},
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Item,
									Rare = 40,
									VisualRare = 45,
									PNGReward = "",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = false,
										Shortname = "pistol.python",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 1,
											MaxAmount = 1,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "",
										SecondCommand = 0,
										ItemAmount = 0
									}
								},
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Item,
									Rare = 4,
									VisualRare = 8,
									PNGReward = "",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = false,
										Shortname = "rifle.ak",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 1,
											MaxAmount = 1,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "",
										SecondCommand = 0,
										ItemAmount = 0
									}
								},
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Item,
									Rare = 3,
									VisualRare = 6,
									PNGReward = "",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = false,
										Shortname = "rifle.lr300",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 1,
											MaxAmount = 1,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "",
										SecondCommand = 0,
										ItemAmount = 0
									}
								},
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Item,
									Rare = 1,
									VisualRare = 3,
									PNGReward = "",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = false,
										Shortname = "supply.signal",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 1,
											MaxAmount = 1,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "",
										SecondCommand = 0,
										ItemAmount = 0
									}
								},
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Item,
									Rare = 10,
									VisualRare = 20,
									PNGReward = "",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = false,
										Shortname = "scrap",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 50,
											MaxAmount = 200,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "",
										SecondCommand = 0,
										ItemAmount = 0
									}
								},
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Item,
									Rare = 5,
									VisualRare = 20,
									PNGReward = "",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = false,
										Shortname = "door.hinged.toptier",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 1,
											MaxAmount = 1,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "",
										SecondCommand = 0,
										ItemAmount = 0
									}
								},
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Command,
									Rare = 10,
									VisualRare = 15,
									PNGReward = "https://i.postimg.cc/xjJWHkNC/hGXoUms.png",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = false,
										Shortname = "",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 1,
											MaxAmount = 1,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "command your plugin %STEAMID%",
										SecondCommand = 0,
										ItemAmount = 1
									}
								},
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Command,
									Rare = 10,
									VisualRare = 15,
									PNGReward = "https://i.postimg.cc/kg9Zkf16/zzsL6qZ.png",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = false,
										Shortname = "",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 1,
											MaxAmount = 1,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "command your plugin %STEAMID%",
										SecondCommand = 0,
										ItemAmount = 1
									}
								},
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Command,
									Rare = 5,
									VisualRare = 10,
									PNGReward = "https://i.postimg.cc/nztQk9cJ/X0VAteL.png",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = false,
										Shortname = "",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 1,
											MaxAmount = 1,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "addgroup %STEAMID% vip 3d",
										SecondCommand = 259200,
										ItemAmount = 0
									}
								},
							},
						},
						#endregion
						
						#region Components Case
						
						["components_case"] = new CasesPreset
						{
							caseName = new CasesPreset.NameCases()
							{
								NameRussia = "",
								NameEnglish = ""
							},
							PNGCase = "https://i.postimg.cc/s25MvfP0/j7pA2qD.png",
							EconomicSettings = new CasesPreset.EconomicSetting
							{
								PriceBuyCase = 20,
								PriceSellCase = 10
							},
							RestrictionsSettings = new CasesPreset.RestrictionsPreset
							{
								Permissions = "",
								IQRankKey = ""
							},
							RewardPresets = new List<CasesPreset.RewardPreset>
							{
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Item,
									Rare = 10,
									VisualRare = 15,
									PNGReward = "",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = false,
										Shortname = "riflebody",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 1,
											MaxAmount = 3,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "",
										SecondCommand = 0,
										ItemAmount = 0
									}
								},
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Item,
									Rare = 15,
									VisualRare = 20,
									PNGReward = "",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = false,
										Shortname = "smgbody",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 1,
											MaxAmount = 5,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "",
										SecondCommand = 0,
										ItemAmount = 0
									}
								},
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Item,
									Rare = 15,
									VisualRare = 20,
									PNGReward = "",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = false,
										Shortname = "semibody",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 1,
											MaxAmount = 3,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "",
										SecondCommand = 0,
										ItemAmount = 0
									}
								},
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Item,
									Rare = 20,
									VisualRare = 30,
									PNGReward = "",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = false,
										Shortname = "metalpipe",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 3,
											MaxAmount = 10,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "",
										SecondCommand = 0,
										ItemAmount = 0
									}
								},
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Item,
									Rare = 15,
									VisualRare = 25,
									PNGReward = "",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = false,
										Shortname = "techparts",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 1,
											MaxAmount = 3,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "",
										SecondCommand = 0,
										ItemAmount = 0
									}
								},
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Item,
									Rare = 40,
									VisualRare = 50,
									PNGReward = "",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = false,
										Shortname = "roadsigns",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 5,
											MaxAmount = 30,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "",
										SecondCommand = 0,
										ItemAmount = 0
									}
								},
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Item,
									Rare = 25,
									VisualRare = 40,
									PNGReward = "",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = false,
										Shortname = "metalspring",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 3,
											MaxAmount = 15,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "",
										SecondCommand = 0,
										ItemAmount = 0
									}
								},
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Item,
									Rare = 40,
									VisualRare = 45,
									PNGReward = "",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = false,
										Shortname = "sewingkit",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 10,
											MaxAmount = 25,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "",
										SecondCommand = 0,
										ItemAmount = 0
									}
								},
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Item,
									Rare = 50,
									VisualRare = 75,
									PNGReward = "",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = false,
										Shortname = "sheetmetal",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 3,
											MaxAmount = 5,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "",
										SecondCommand = 0,
										ItemAmount = 0
									}
								},
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Item,
									Rare = 70,
									VisualRare = 80,
									PNGReward = "",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = false,
										Shortname = "tarp",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 3,
											MaxAmount = 5,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "",
										SecondCommand = 0,
										ItemAmount = 0
									}
								},
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Item,
									Rare = 25,
									VisualRare = 35,
									PNGReward = "",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = false,
										Shortname = "gears",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 3,
											MaxAmount = 8,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "",
										SecondCommand = 0,
										ItemAmount = 0
									}
								},
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Item,
									Rare = 40,
									VisualRare = 50,
									PNGReward = "",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = false,
										Shortname = "propanetank",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 3,
											MaxAmount = 10,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "",
										SecondCommand = 0,
										ItemAmount = 0
									}
								},
							},
						},
						#endregion
						
						#region Blueprints Case
						
						["blueprints_case"] = new CasesPreset
						{
							caseName = new CasesPreset.NameCases()
							{
								NameRussia = "",
								NameEnglish = ""
							},
							PNGCase = "https://i.postimg.cc/HxpknwkV/2jTQUla.png",
							EconomicSettings = new CasesPreset.EconomicSetting
							{
								PriceBuyCase = 70,
								PriceSellCase = 25
							},
							RestrictionsSettings = new CasesPreset.RestrictionsPreset
							{
								Permissions = "",
								IQRankKey = ""
							},
							RewardPresets = new List<CasesPreset.RewardPreset>
							{
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Item,
									Rare = 1,
									VisualRare = 3,
									PNGReward = "",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = true,
										Shortname = "rocket.launcher",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 1,
											MaxAmount = 1,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "",
										SecondCommand = 0,
										ItemAmount = 0
									}
								},
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Item,
									Rare = 3,
									VisualRare = 5,
									PNGReward = "",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = true,
										Shortname = "rifle.ak",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 1,
											MaxAmount = 1,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "",
										SecondCommand = 0,
										ItemAmount = 0
									}
								},
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Item,
									Rare = 3,
									VisualRare = 6,
									PNGReward = "",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = true,
										Shortname = "rifle.bolt",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 1,
											MaxAmount = 1,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "",
										SecondCommand = 0,
										ItemAmount = 0
									}
								},
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Item,
									Rare = 5,
									VisualRare = 10,
									PNGReward = "",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = true,
										Shortname = "smg.thompson",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 1,
											MaxAmount = 1,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "",
										SecondCommand = 0,
										ItemAmount = 0
									}
								},
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Item,
									Rare = 8,
									VisualRare = 15,
									PNGReward = "",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = true,
										Shortname = "shotgun.pump",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 1,
											MaxAmount = 1,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "",
										SecondCommand = 0,
										ItemAmount = 0
									}
								},
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Item,
									Rare = 14,
									VisualRare = 20,
									PNGReward = "",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = true,
										Shortname = "pistol.python",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 1,
											MaxAmount = 1,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "",
										SecondCommand = 0,
										ItemAmount = 0
									}
								},
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Item,
									Rare = 40,
									VisualRare = 40,
									PNGReward = "",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = true,
										Shortname = "pistol.revolver",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 1,
											MaxAmount = 1,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "",
										SecondCommand = 0,
										ItemAmount = 0
									}
								},
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Item,
									Rare = 40,
									VisualRare = 45,
									PNGReward = "",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = true,
										Shortname = "weapon.mod.lasersight",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 1,
											MaxAmount = 1,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "",
										SecondCommand = 0,
										ItemAmount = 0
									}
								},
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Item,
									Rare = 40,
									VisualRare = 45,
									PNGReward = "",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = true,
										Shortname = "weapon.mod.silencer",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 1,
											MaxAmount = 1,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "",
										SecondCommand = 0,
										ItemAmount = 0
									}
								},
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Item,
									Rare = 40,
									VisualRare = 45,
									PNGReward = "",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = true,
										Shortname = "weapon.mod.flashlight",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 1,
											MaxAmount = 1,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "",
										SecondCommand = 0,
										ItemAmount = 0
									}
								},
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Item,
									Rare = 3,
									VisualRare = 10,
									PNGReward = "",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = true,
										Shortname = "door.double.hinged.toptier",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 1,
											MaxAmount = 1,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "",
										SecondCommand = 0,
										ItemAmount = 0
									}
								},
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Item,
									Rare = 4,
									VisualRare = 15,
									PNGReward = "",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = true,
										Shortname = "door.hinged.toptier",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 1,
											MaxAmount = 1,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "",
										SecondCommand = 0,
										ItemAmount = 0
									}
								},
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Item,
									Rare = 70,
									VisualRare = 60,
									PNGReward = "",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = true,
										Shortname = "ladder.wooden.wall",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 1,
											MaxAmount = 1,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "",
										SecondCommand = 0,
										ItemAmount = 0
									}
								},
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Item,
									Rare = 35,
									VisualRare = 45,
									PNGReward = "",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = true,
										Shortname = "wall.external.high.stone",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 1,
											MaxAmount = 1,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "",
										SecondCommand = 0,
										ItemAmount = 0
									}
								},
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Item,
									Rare = 30,
									VisualRare = 45,
									PNGReward = "",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = true,
										Shortname = "gates.external.high.stone",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 1,
											MaxAmount = 1,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "",
										SecondCommand = 0,
										ItemAmount = 0
									}
								},
							},
						},
						#endregion
						
						#region Raider Case
						
						["raiders_case"] = new CasesPreset
						{
							caseName = new CasesPreset.NameCases()
							{
								NameRussia = "",
								NameEnglish = ""
							},
							PNGCase = "https://i.postimg.cc/pLbxyf0R/COSxi3t.png",
							EconomicSettings = new CasesPreset.EconomicSetting
							{
								PriceBuyCase = 70,
								PriceSellCase = 0
							},
							RestrictionsSettings = new CasesPreset.RestrictionsPreset
							{
								Permissions = "",
								IQRankKey = ""
							},
							RewardPresets = new List<CasesPreset.RewardPreset>
							{
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Item,
									Rare = 7,
									VisualRare = 13,
									PNGReward = "",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = false,
										Shortname = "multiplegrenadelauncher",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 1,
											MaxAmount = 1,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "",
										SecondCommand = 0,
										ItemAmount = 0
									}
								},
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Item,
									Rare = 3,
									VisualRare = 5,
									PNGReward = "",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = false,
										Shortname = "rocket.launcher",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 1,
											MaxAmount = 1,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "",
										SecondCommand = 0,
										ItemAmount = 0
									}
								},
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Item,
									Rare = 3,
									VisualRare = 6,
									PNGReward = "",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = false,
										Shortname = "rifle.ak",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 1,
											MaxAmount = 1,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "",
										SecondCommand = 0,
										ItemAmount = 0
									}
								},
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Item,
									Rare = 25,
									VisualRare = 40,
									PNGReward = "",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = false,
										Shortname = "grenade.beancan",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 5,
											MaxAmount = 15,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "",
										SecondCommand = 0,
										ItemAmount = 0
									}
								},
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Item,
									Rare = 1,
									VisualRare = 2,
									PNGReward = "",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = false,
										Shortname = "lmg.m249",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 1,
											MaxAmount = 1,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "",
										SecondCommand = 0,
										ItemAmount = 0
									}
								},
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Item,
									Rare = 3,
									VisualRare = 8,
									PNGReward = "",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = false,
										Shortname = "explosive.timed",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 1,
											MaxAmount = 3,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "",
										SecondCommand = 0,
										ItemAmount = 0
									}
								},
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Item,
									Rare = 13,
									VisualRare = 30,
									PNGReward = "",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = false,
										Shortname = "explosive.satchel",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 3,
											MaxAmount = 8,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "",
										SecondCommand = 0,
										ItemAmount = 0
									}
								},
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Item,
									Rare = 35,
									VisualRare = 45,
									PNGReward = "",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = false,
										Shortname = "sulfur",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 1500,
											MaxAmount = 3500,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "",
										SecondCommand = 0,
										ItemAmount = 0
									}
								},
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Item,
									Rare = 15,
									VisualRare = 25,
									PNGReward = "",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = false,
										Shortname = "gunpowder",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 1000,
											MaxAmount = 2500,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "",
										SecondCommand = 0,
										ItemAmount = 0
									}
								},
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Item,
									Rare = 25,
									VisualRare = 45,
									PNGReward = "",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = false,
										Shortname = "ammo.rifle.explosive",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 128,
											MaxAmount = 128,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "",
										SecondCommand = 0,
										ItemAmount = 0
									}
								},
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Item,
									Rare = 3,
									VisualRare = 10,
									PNGReward = "",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = false,
										Shortname = "ammo.rocket.basic",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 1,
											MaxAmount = 3,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "",
										SecondCommand = 0,
										ItemAmount = 0
									}
								},
								new CasesPreset.RewardPreset
								{
									Type = TypeReward.Command,
									Rare = 10,
									VisualRare = 15,
									PNGReward = "https://i.postimg.cc/xjJWHkNC/hGXoUms.png",
									ItemSetting = new CasesPreset.RewardPreset.ItemPreset
									{
										DisplayName = "",
										IsBlueprint = false,
										Shortname = "",
										SkinID = 0,
										Amounts = new CasesPreset.RewardPreset.ItemPreset.AmountPreset
										{
											MinAmount = 1,
											MaxAmount = 1,
										}
									},
									CommandSetting = new CasesPreset.RewardPreset.CommandPreset
									{
										DisplayName = "",
										Command = "command your plugin %STEAMID%",
										SecondCommand = 0,
										ItemAmount = 1
									}
								},
							},
						},
						#endregion
					},
			    };
		    }
	    }

	    protected override void LoadConfig()
	    {
		    base.LoadConfig();
		    try
		    {
			    config = Config.ReadObject<Configuration>();
			    if (config == null) LoadDefaultConfig();

			    foreach (KeyValuePair<string,Configuration.CasesPreset> configCasesPreset in config.CasesPresets)
			    {
				    if (configCasesPreset.Value.caseName == null)
				    {
					    configCasesPreset.Value.caseName = new Configuration.CasesPreset.NameCases()
					    {
						    NameRussia = "",
						    NameEnglish = "",
					    };
				    }

				    if (configCasesPreset.Value.caseName.NameEnglish == null)
					    configCasesPreset.Value.caseName.NameEnglish = "";
				    
				    if (configCasesPreset.Value.caseName.NameRussia == null)
					    configCasesPreset.Value.caseName.NameRussia = "";
			    }
		    }
		    catch
		    {
			    PrintWarning(LanguageEn ? "Error #58 reading configuration 'oxide/config/{Name}', creating a new configuration!!" : $"Ошибка #58 чтения конфигурации 'oxide/config/{Name}', создаём новую конфигурацию!!");
			    LoadDefaultConfig();
		    }

		    NextTick(SaveConfig);
	    }

	    protected override void LoadDefaultConfig() => config = Configuration.GetNewConfiguration();
	    protected override void SaveConfig() => Config.WriteObject(config);

	    #endregion
	    
	    #region Data

	    private Dictionary<UInt64, InformationPlayer> DataPlayer = new Dictionary<UInt64, InformationPlayer>();
	    private Dictionary<UInt64, List<String>> DataPlayerTakeGiftCase = new Dictionary<UInt64, List<String>>();

	    private class InformationPlayer
	    {
		    [JsonProperty(LanguageEn ? "Toggle switch for issuing items (true - plugin inventory / false - player inventory)" : "Тумблер выдачи предметов (true - инвентарь плагина/false - инвентарь игрока)")]
		    public Boolean SwitchTakeInventory = false;
		    
		    [JsonProperty(LanguageEn ? "Cases [Key] - Amount" : "Кейсы [Key] - Amount")]
		    public Dictionary<String, Int32> CasesPlayer = new Dictionary<String, Int32>();

		    [JsonProperty(LanguageEn ? "Inventory player" : "Инвентарь игрока")]
		    public List<RewardPreset> InventoryPlayer =
			    new List<RewardPreset>();

		    internal class RewardPreset
		    {
			    [JsonProperty(LanguageEn
				    ? "Select reward type: 0 - Item, 1 - Command"
				    : "Выберите тип награды : 0 - Предмет, 1 - Команда")]
			    public TypeReward Type;

			    [JsonProperty(LanguageEn
				    ? "The displayed chance of getting this reward"
				    : "Отображаемый шанс выпадения данной награды")]
			    public Int32 VisualRare;

			    [JsonProperty(LanguageEn ? "PNG for reward" : "PNG для награды")]
			    public String PNGReward;

			    [JsonProperty(LanguageEn ? "Infromation Item" : "Данные о предмете")]
			    public ItemPreset ItemSetting = new ItemPreset();

			    [JsonProperty(LanguageEn ? "Information Command" : "Данные о команде")]
			    public CommandPreset CommandSetting = new CommandPreset();

			    #region CommandPreset

			    internal class CommandPreset
			    {
				    [JsonProperty(LanguageEn
					    ? "Item name in UI (if left blank, values from lang will be used)"
					    : "Название предмета в UI (если оставить пустым будут браться значения из lang)")]
				    public String DisplayName;
				    
				    [JsonProperty(LanguageEn
					    ? "Console command (%STEAMID% - will be replaced by the player's Steam64ID)"
					    : "Консольная команда (%STEAMID% - заменится на Steam64ID игрока)")]
				    public String Command;

				    [JsonProperty(LanguageEn
					    ? "Seconds (only affects the visual display)"
					    : "Секунды (влияет только на визуальное отображение)")]
				    public Int32 SecondCommand;

				    [JsonProperty(LanguageEn
					    ? "Amount (affects visual display only)"
					    : "Количество (влияет только на визуальное отображение)")]
				    public Int32 ItemAmount;
				    
				    public String GetAmountLabel(BasePlayer player) => SecondCommand == 0 ? $"x{ItemAmount}" : _.FormatTime(TimeSpan.FromSeconds(SecondCommand), player.UserIDString);

			    }

			    #endregion

			    #region ItemPreset

			    internal class ItemPreset
			    {
				    [JsonProperty(LanguageEn
					    ? "Item name in UI (if left blank, values from lang will be used)"
					    : "Название предмета в UI (если оставить пустым будут браться значения из lang)")]
				    public String DisplayName;
				    
				    [JsonProperty(LanguageEn
					    ? "Is this a blueprint? true - yes/false - no"
					    : "Это чертеж? true - да/false - нет")]
				    public Boolean IsBlueprint;

				    [JsonProperty(LanguageEn ? "Shortname item" : "Shortname предмета")]
				    public String Shortname;

				    [JsonProperty(LanguageEn ? "SkinID item" : "SkinID предмета")]
				    public UInt64 SkinID;

				    [JsonProperty(LanguageEn ? "Amount" : "Количество")]
				    public Int32 Amount;

				    [JsonIgnore] 
				    public String GetAmountLabel => $"x{Amount}";
			    }

			    #endregion
		    }
		    
		    public RewardPreset GenericRewardPreset(Configuration.CasesPreset.RewardPreset Reward)
		    {
			    RewardPreset ResultReward = new RewardPreset
			    {
				    Type = Reward.Type,
				    VisualRare = Reward.VisualRare,
				    PNGReward = Reward.PNGReward,
				    ItemSetting = new RewardPreset.ItemPreset
				    {
					    DisplayName = Reward.ItemSetting.DisplayName,
					    IsBlueprint = Reward.ItemSetting.IsBlueprint,
					    Shortname = Reward.ItemSetting.Shortname,
					    SkinID = Reward.ItemSetting.SkinID,
					    Amount = Reward.ItemSetting.Amounts.GetAmount
				    },
				    CommandSetting = new RewardPreset.CommandPreset
				    {
					    DisplayName = Reward.CommandSetting.DisplayName,
					    Command = Reward.CommandSetting.Command,
					    SecondCommand = Reward.CommandSetting.SecondCommand,
					    ItemAmount = Reward.CommandSetting.ItemAmount
				    }
			    };

			    return ResultReward;
		    }

		    public Int32 GetAmountCase(String CaseKey)
		    {
			    Int32 Amount = 0;

			    if (CasesPlayer.ContainsKey(CaseKey))
				    Amount = CasesPlayer[CaseKey];
			    
			    return Amount;
		    }
	    }

	    #region Data Func
	    
	    private void ReadData()
	    {
		    DataPlayer = Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<Dictionary<UInt64, InformationPlayer>>("IQSystem/TPCases/DataPlayer");
		    DataPlayerTakeGiftCase = Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<Dictionary<UInt64, List<String>>>("IQSystem/TPCases/DataPlayerTakeGiftCase");
	    }
	    private void WriteData()
	    {
		    Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("IQSystem/TPCases/DataPlayer", DataPlayer);
		    Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("IQSystem/TPCases/DataPlayerTakeGiftCase", DataPlayerTakeGiftCase);
	    }
	    
	    private void RegisteredDataUser(UInt64 playerID)
	    {
		    if (!playerID.IsSteamId()) return; 
		    
		    if (!DataPlayerTakeGiftCase.ContainsKey(playerID))
			    DataPlayerTakeGiftCase.Add(playerID, new List<String>());
		    
		    if (!DataPlayer.ContainsKey(playerID))
			    DataPlayer.Add(playerID, new InformationPlayer
			    {
				    SwitchTakeInventory = false,
				    CasesPlayer = new Dictionary<String, Int32>(),
				    InventoryPlayer = new List<InformationPlayer.RewardPreset>()
			    });
			    
		    GiftDefaultCase(playerID);
	    }
	    
	    #endregion
	    
	    #endregion

	    #region Hooks

	    private void Init() => ReadData();
	    
	    private void OnServerInitialized()
	    {
		    _ = this;
		    //Load your images here
		    ImageUi.Initialize();
		    ImageUi.DownloadImages();

		    foreach (KeyValuePair<String, Configuration.CasesPreset> Cases in config.CasesPresets)
		    {
			    if(!CaseKeys.Contains(Cases.Key))
				    CaseKeys.Add(Cases.Key);
			    
			    if(!String.IsNullOrWhiteSpace(Cases.Value.RestrictionsSettings.Permissions) && !permission.PermissionExists(Cases.Value.RestrictionsSettings.Permissions, this))
				    permission.RegisterPermission(Cases.Value.RestrictionsSettings.Permissions, this);
		    }

		    foreach (Configuration.DefaultCasePlayer defaultCasePlayer in config.DefaultCasePlayers)
		    {
			    if(!String.IsNullOrWhiteSpace(defaultCasePlayer.Permissions) && !permission.PermissionExists(defaultCasePlayer.Permissions, this))
				    permission.RegisterPermission(defaultCasePlayer.Permissions, this);
		    }
		    
		    foreach (List<Configuration.DropCases> DropCase in config.DropCasesList.Values)
		    {
			    foreach (Configuration.DropCases cases in DropCase)
			    {
				    if (!String.IsNullOrWhiteSpace(cases.Permissions) &&
				        !permission.PermissionExists(cases.Permissions, this))
					    permission.RegisterPermission(cases.Permissions, this);
			    }
		    }

		    if (config.DropCasesList.Count == 0)
		    {
			    Unsubscribe("OnLootEntity");
			    Unsubscribe("OnEntityDeath");
		    }

		    Puts(LanguageEn ? $"Loaded {CaseKeys.Count} cases" : $"Загружено {CaseKeys.Count} кейсов");
		    
		    foreach (BasePlayer player in BasePlayer.activePlayerList)
			    OnPlayerConnected(player);


            ImageLibrary?.Call("AddImage", "https://i.ibb.co/pLvByMc/3454435.png", "background3");
	    }
	    
	    private void Unload()
	    {
		    WriteData();
		    InterfaceBuilder.DestroyAll();
		    ImageUi.Unload();
		    _ = null;
	    }

	    private void OnPlayerConnected(BasePlayer player) => RegisteredDataUser(player.userID);
	    private void OnPlayerDisconnected(BasePlayer player, String reason)
	    {
		    if (LocalRepositoryPlayer.ContainsKey(player))
			    LocalRepositoryPlayer.Remove(player);
	    }
	    
	    private void OnLootEntity(BasePlayer player, BaseEntity entity)
	    {
		    if (entity == null || player == null) return;
		    if (entity as StorageContainer == null) return;
		    if (entity.OwnerID >= 7656000000) return;
		    
		    if (entity.net == null) return;
		    UInt64 NetID = entity.net.ID.Value;
		    if (LootersListCrateID.Contains(NetID)) return;

		    SearchCaseLootable(player, entity.ShortPrefabName);
		    LootersListCrateID.Add(NetID);
	    }
	    
	    private object OnLootEntityNetworking(BasePlayer player, BaseEntity entity)
	    {
		    if (entity == null || player == null) return null;
		    if (entity as StorageContainer == null) return null;
		    if (entity.OwnerID >= 7656000000) return null;
		    
		    if (entity.net == null) return null;
		    UInt64 NetID = entity.net.ID.Value;
		    if(NetID == entity.skinID || NetID >= 75532324340) return null;
		    if (LootersListCrateID.Contains(NetID)) return false;

		    return null;
	    }
	    
	    private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
	    {
		    if (entity == null || info == null) return;
		    BasePlayer player = info.InitiatorPlayer;
		    if (player == null) return;

		    if (entity.PrefabName.Contains("barrel") && !entity.PrefabName.Contains("hobobarrel"))
			    SearchCaseLootable(player, entity.ShortPrefabName);
	    }

	    #endregion

	    #region Command
/*
	    [ChatCommand("сфыу")]
	    void ChatCommandOpenUI_RuCommand(BasePlayer player)
	    {
		    OpenMenuCases(player);
	    }
	    
	    [ChatCommand("case")]
	    void ChatCommandOpenUI(BasePlayer player)
	    {
		    OpenMenuCases(player);
	    }*/

	    [ConsoleCommand("case.give.all")]
	    private void AdminCaseCommandGiveAll(ConsoleSystem.Arg arg)
	    {
		    if (!FullInit) return;
		    BasePlayer player = arg.Player();
		    
		    if(player != null)
			    if (!player.IsAdmin)
				    return;

		    if (!arg.HasArgs(2))
			    return;
		    
		    String CaseKey = arg.GetString(0);
		    if (String.IsNullOrWhiteSpace(CaseKey) || !CaseKeys.Contains(CaseKey))
		    {
			    PrintWarning(LanguageEn ? "Case with such key does not exist" : "Кейса с таким ключем не существует");
			    return;
		    }
		    
		    Int32 Amount = arg.GetInt(1, -1);
		    if (Amount == -1)
		    {
			    PrintWarning(LanguageEn ? "The second argument is not a number!" : "Второй аргумент не является числом!");
			    return;
		    }

		    foreach (BasePlayer playerList in BasePlayer.activePlayerList)
			    GiveCases(playerList.userID, CaseKey, Amount);
		    
		    Puts(LanguageEn ? $"All players have been given a {CaseKey} case in the amount of {Amount}" : $"Всем игрокам выдан кейс {CaseKey} в количестве {Amount}");
	    }
	    
	    [ConsoleCommand("case")] 
	    private void AdminCaseCommand(ConsoleSystem.Arg arg)
	    {
		    if (!FullInit) return;
		    BasePlayer player = arg.Player();
		    
		    if(player != null)
			    if (!player.IsAdmin)
				    return;

		    if (!arg.HasArgs(3))
			    return;
		    
		    String NamaOrID = arg.GetString(1);
		    if(String.IsNullOrWhiteSpace(NamaOrID))
		    {
			    PrintWarning(LanguageEn ? "Enter Nickname or SteamID" : "Введите ник или SteamID");
			    return;
		    }

		    IPlayer iPlayer = covalence.Players.FindPlayer(NamaOrID);
		    if(iPlayer == null)
		    {
			    PrintWarning(LanguageEn ? $"This player does not exist" : $"Такого игрока не существует");
			    return;
		    }

		    String CaseKey = arg.GetString(2);
		    if (String.IsNullOrWhiteSpace(CaseKey) || !CaseKeys.Contains(CaseKey))
		    {
			    PrintWarning(LanguageEn ? "Case with such key does not exist" : "Кейса с таким ключем не существует");
			    return;
		    }
		    
		    Int32 Amount = arg.GetInt(3, -1);
		    if (Amount == -1)
		    {
			    PrintWarning(LanguageEn ? "The second argument is not a number!" : "Второй аргумент не является числом!");
			    return;
		    }

		    UInt64 PlayerID = UInt64.Parse(iPlayer.Id);
		    
		    switch (arg.GetString(0))
		    {
			    case "give":
			    {
				    GiveCases(PlayerID, CaseKey, Amount);
				    Puts(LanguageEn ? $"The player successfully received a case {CaseKey} in the amount of {Amount}" : $"Игрок успешно получил кейс {CaseKey} в количестве {Amount}");
				    break;
			    }
			    case "remove":
			    {
				    if (!RemoveCases(PlayerID, CaseKey, Amount))
				    {
					    Puts(LanguageEn ? $"Player has no cases with key {CaseKey}" : $"У игрока нет кейсов с ключем {CaseKey}");
					    return;
				    }
				    Puts(LanguageEn ? $"You have successfully removed the {Amount} of the {CaseKey} case from the player" : $"Вы успешно удалили {Amount} кейсов {CaseKey} у игрока");
				    break;
			    }
		    }
	    }

	    [ConsoleCommand("case.func")]
	    void ConsoleFuncCommandUI(ConsoleSystem.Arg arg)
	    {
		    if (!FullInit) return;
		    
		    BasePlayer player = arg.Player();
		    if (player == null) return;

		    if(LocalRepositoryPlayer.ContainsKey(player))
			    if (LocalRepositoryPlayer[player].IsOpenedCase)
				    return;
		    
		    InformationPlayer Data = DataPlayer[player.userID];
		    if (Data == null) return;

		    String Action = arg.Args[0];

		    switch (Action)
		    {
			    case "switch.inventory":
			    {
				    Data.SwitchTakeInventory = !Data.SwitchTakeInventory;

				    DrawUI_Tumbler(player);
				    break;
			    }
			    case "page.controller":
			    {
				    Int32 Page = Int32.Parse(arg.Args[1]);
				    if (Page >= CaseKeys.Count)
					    Page = 0;

				    if (Page < 0)
					    Page = CaseKeys.Count - 1;

				    DrawUI_ShowCases(player, Page);
				    break;
			    }
			    case "close.case.menu": 
			    {
				    CuiHelper.DestroyUi(player, InterfaceBuilder.UI_CASES_MENU);
				    
				    if (LocalRepositoryPlayer.ContainsKey(player))
					    LocalRepositoryPlayer.Remove(player);
				    break;
			    }
			    case "open.case":
			    {
				    LocalRepositoryPlayer[player].IsOpenedCase = true;

				    String CaseKey = arg.Args[1];
				    player.StartCoroutine(OpenCase(player, CaseKey));
				    break;
			    }
			    case "open.inventory":
			    {
				    DrawUI_InventoryPlayer(player);
				    break;
			    }
			    case "open.case.menu":
			    {
				    DrawUI_Case(player);
				    break;
			    }
			    case "sell.case.economic": 
			    {
				    String CaseKey = arg.Args[1];
				    BuyAndSellCases(player, CaseKey, true);
				    break;
			    }
			    case "buy.case.economic":
			    {
				    String CaseKey = arg.Args[1];
				    BuyAndSellCases(player, CaseKey, false);
				    break;
			    }
			    case "inventory.take.reward": 
			    {
				    if (LocalRepositoryPlayer[player].IsCooldownTake) return;
				    Int32 IndexReward = Int32.Parse(arg.Args[1]);
				    InformationPlayer.RewardPreset rewardPreset = DataPlayer[player.userID].InventoryPlayer[IndexReward];
				    
				    TakeReward(player, rewardPreset);
				    
				    LocalRepositoryPlayer[player].SetCooldown();
				    break;
			    }
			    case "inventory.page.controller": 
			    {
				    Int32 Page = Int32.Parse(arg.Args[1]);
				    DrawUI_ShowItems_Inventory(player, Page);
				    break;
			    }
		    }
	    }

	    #endregion

	    #region Metods

	    #region Search Case
	    
	    private Boolean GetRandomDrop(Int32 RandomInt)
	    {
		    Int32 Random = Oxide.Core.Random.Range(0, 100);
		    return RandomInt >= Random;
	    }

	    private void SearchCaseLootable(BasePlayer player, String ShortPrefabName)
	    {
		    if (!config.DropCasesList.ContainsKey(ShortPrefabName)) return;

		    foreach (Configuration.DropCases dropCases in config.DropCasesList[ShortPrefabName])
		    {
			    if (String.IsNullOrWhiteSpace(dropCases.Permissions) ||
			        permission.UserHasPermission(player.UserIDString, dropCases.Permissions))
			    {
				    if (GetRandomDrop(dropCases.Rare))
				    {
					    GiveCases(player.userID, dropCases.CaseKey, dropCases.Count);
					    break;
				    }
			    }
		    }
	    }

	    #endregion

	    private void GiveCases(UInt64 playerID, String CaseKey, Int32 Amount = 1)
	    {
		    if (!CaseKeys.Contains(CaseKey)) return;

		    RegisteredDataUser(playerID);
		    
		    if (DataPlayer[playerID].CasesPlayer.ContainsKey(CaseKey))
			    DataPlayer[playerID].CasesPlayer[CaseKey] += Amount;
		    else DataPlayer[playerID].CasesPlayer.Add(CaseKey, Amount);
		    
		    BasePlayer player = BasePlayer.FindByID(playerID);
		    if (player == null) return;
		    
		    SendChat(GetLang("CASE_MESSAGE_FIND_CASE", player.UserIDString), player);
	    }
	    
	    private Boolean RemoveCases(UInt64 playerID, String CaseKey, Int32 Amount = 1)
	    {
		    if (!CaseKeys.Contains(CaseKey)) return false;
		    if (!DataPlayer[playerID].CasesPlayer.ContainsKey(CaseKey)) return false;
		    
		    DataPlayer[playerID].CasesPlayer[CaseKey] -= Amount;
		    if (DataPlayer[playerID].CasesPlayer[CaseKey] == 0)
			    DataPlayer[playerID].CasesPlayer.Remove(CaseKey);

		    return true;
	    }

	    #region Gift Default Case

	    private void GiftDefaultCase(UInt64 playerID)
	    {
		    if (!DataPlayerTakeGiftCase.ContainsKey(playerID))
			    DataPlayerTakeGiftCase.Add(playerID, new List<String>());

		    foreach (Configuration.DefaultCasePlayer defaultCasePlayer in config.DefaultCasePlayers)
		    {
			    String CaseKey = defaultCasePlayer.CaseKey;
			    if (!CaseKeys.Contains(CaseKey)) continue;
			    if (DataPlayerTakeGiftCase[playerID].Contains(CaseKey)) continue;

			    if (!String.IsNullOrWhiteSpace(defaultCasePlayer.Permissions) &&
			        !permission.UserHasPermission(playerID.ToString(), defaultCasePlayer.Permissions)) continue;
			    
			    DataPlayerTakeGiftCase[playerID].Add(CaseKey);

			    GiveCases(playerID, CaseKey, defaultCasePlayer.Count);
			    PrintError(LanguageEn ? $"Player {playerID} has been given case {CaseKey}" : $"Игроку {playerID} выдан кейс {CaseKey}");
		    }
	    }

	    #endregion

	    #region OpenMenuCase
	    
	    private void OpenMenuCases(BasePlayer player)
	    {
		    if (!FullInit)
		    {
			    SendChat(GetLang("UI_CASE_INITIALIZE", player.UserIDString), player);
			    return;
		    }

		    if (IsDuel(player.userID))
		    {
			    SendChat(GetLang("CASE_MESSAGE_NO_DUEL", player.UserIDString), player);
			    return;
		    }
			    
		    DrawUI_Case(player);
	    }
	    
	    #endregion

	    #region Blocked Func

	    private Boolean IsPermissionBlocked(BasePlayer player, String Permission) => String.IsNullOrWhiteSpace(Permission) || permission.UserHasPermission(player.UserIDString, Permission);
	    private String IsRankBlocked(BasePlayer player, String Rank)
	    {
		    if(!IQRankSystem) return String.Empty;
		    if (String.IsNullOrWhiteSpace(Rank)) return String.Empty;

		    return !IsRank(player.userID, Rank) ? GetRankName(Rank) : String.Empty;
	    }

	    #endregion

	    #region Buy&Sell Cases

	    private void BuyAndSellCases(BasePlayer player, String CaseKey, Boolean BuyOrSale)
	    {
		    Configuration.CasesPreset.EconomicSetting EconomicInfo = config.CasesPresets[CaseKey].EconomicSettings;

		    if (!BuyOrSale)
		    {
			    if (!IsRemovedBalance(player.userID, EconomicInfo.PriceBuyCase)) return;
			    
			    GiveCases(player.userID, CaseKey);

			    RemoveBalance(player.userID, EconomicInfo.PriceBuyCase);
			    
			    if(!LocalRepositoryPlayer[player].IsCooldownEffect)
			    {
					RunEffect(player, "assets/prefabs/deployable/vendingmachine/effects/vending-machine-purchase-human.prefab");
					LocalRepositoryPlayer[player].SetCooldownEffect();
			    }
			    
			    Interface.Oxide.CallHook("OnBuyCase", player, CaseKey);
		    }
		    else
		    {
			    if (!RemoveCases(player.userID, CaseKey)) return;
			    SetBalance(player.userID, EconomicInfo.PriceSellCase);
			    
			    if(!LocalRepositoryPlayer[player].IsCooldownEffect)
			    {
				    RunEffect(player, "assets/prefabs/deployable/vendingmachine/effects/vending-machine-purchase-human.prefab");
				    LocalRepositoryPlayer[player].SetCooldownEffect();
			    }
			    
			    Interface.Oxide.CallHook("OnSellCase", player, CaseKey);
		    }

		    DrawUI_UpdateLabels(player, CaseKey);
	    }

	    #endregion

	    private IEnumerator OpenCase(BasePlayer player, String CaseKey)
	    {
		    if (!RemoveCases(player.userID, CaseKey)) yield break;
		    DrawUI_UpdateLabels(player, CaseKey);
		    
		    Configuration.CasesPreset Case = config.CasesPresets[CaseKey];
		    Configuration.CasesPreset.RewardPreset RandomReward = Case.GetRandomReward();

		    RunEffect(player, "assets/prefabs/npc/autoturret/effects/reload.prefab");
		    yield return CoroutineEx.waitForSeconds(1.5f);
		    
		    List<Int32> VisualRandomIndexes = new List<Int32>();
		    List<Configuration.CasesPreset.RewardPreset> SortedRewardsCase = Case.RewardPresets.OrderByDescending(x => x.VisualRare).ToList();
		    
		    for (Int32 Index = 0; Index < SortedRewardsCase.Count; Index++)
		    {
			    Configuration.CasesPreset.RewardPreset Reward = SortedRewardsCase[Index];
			    if (Reward != RandomReward)
				    VisualRandomIndexes.Add(Index);
		    }

		    Int32 VisualRandomCount = VisualRandomIndexes.Count;
		    for (Int32 RandomIndex = 0; RandomIndex < VisualRandomCount; RandomIndex++)
		    { 
			    RunEffect(player, "assets/prefabs/npc/autoturret/effects/targetacquired.prefab");
			    Int32 VisualIndex = VisualRandomIndexes.GetRandom();
			    DrawUI_NoRare_Panel(player, $"RARE_{VisualIndex}", "0 0", "1 1", "0 0", "0 0");
			    VisualRandomIndexes.Remove(VisualIndex);
			    yield return CoroutineEx.waitForSeconds(0.6f);
		    }
		    
		    RunEffect(player, "assets/prefabs/misc/casino/slotmachine/effects/payout_jackpot.prefab");
		    yield return CoroutineEx.waitForSeconds(0.6f);

		    DrawUI_ShowItems(player, CaseKey);
		    LocalRepositoryPlayer[player].IsOpenedCase = false;
		    
		    if (!DataPlayer[player.userID].SwitchTakeInventory)
			    GiveReward(player, DataPlayer[player.userID].GenericRewardPreset(RandomReward));
		    else MoveToInventory(player, RandomReward);
		    
		    Log(LanguageEn ? $"Player {player.displayName} ({player.userID}) opened case {CaseKey} and received:" +
		                     $"\nType: {RandomReward.Type}" +
		                     $"\nItem Information:" +
		                     $"\nName - {RandomReward.ItemSetting.DisplayName}" +
		                     $"\nShortname - {RandomReward.ItemSetting.Shortname}" +
		                     $"\nSkinID - {RandomReward.ItemSetting.SkinID}" +
		                     $"\nCommand Information:" +
		                     $"\nName - {RandomReward.CommandSetting.DisplayName}" +
		                     $"\nCommand - {RandomReward.CommandSetting.Command}" +
		                     $"\n" :
							$"Игрок {player.displayName}({player.userID}) открыл кейс {CaseKey} и получил :" +
							$"\nТип : {RandomReward.Type}" +
							$"\nИнформация о предмете :" +
							$"\nНазвание - {RandomReward.ItemSetting.DisplayName}" +
							$"\nShortname - {RandomReward.ItemSetting.Shortname}" +
							$"\nSkinID - {RandomReward.ItemSetting.SkinID}" +
							$"\nИнформация о команде :" +
							$"\nНазвание - {RandomReward.CommandSetting.DisplayName}" +
							$"\nКоманда - {RandomReward.CommandSetting.Command}" +
							$"\n");
		    
		    Interface.Oxide.CallHook("OnOpenedCase", player, CaseKey);
	    }

	    #region Move To Inventory

	    private void MoveToInventory(BasePlayer player, Configuration.CasesPreset.RewardPreset Reward)
	    {
		    List<InformationPlayer.RewardPreset> Inventory = DataPlayer[player.userID].InventoryPlayer;

		    if (Reward.Type == TypeReward.Item)
		    {
			    ItemDefinition ItemDef = ItemManager.FindItemDefinition(Reward.ItemSetting.Shortname);
			    if (ItemDef != null)
				    if (ItemDef.category == ItemCategory.Resources || ItemDef.category == ItemCategory.Medical ||
				        ItemDef.category == ItemCategory.Food || ItemDef.category == ItemCategory.Ammunition ||
				        ItemDef.category == ItemCategory.Component)
				    {
					    InformationPlayer.RewardPreset InventoryItem = Inventory.FirstOrDefault(x =>
						    x.ItemSetting.Shortname.Equals(Reward.ItemSetting.Shortname) &&
						    x.ItemSetting.SkinID == Reward.ItemSetting.SkinID);

					    if (InventoryItem != null)
						    InventoryItem.ItemSetting.Amount += Reward.ItemSetting.Amounts.GetAmount;
					    else DataPlayer[player.userID].InventoryPlayer.Add(DataPlayer[player.userID].GenericRewardPreset(Reward));
					    return;
				    }
		    }

		    DataPlayer[player.userID].InventoryPlayer.Add(DataPlayer[player.userID].GenericRewardPreset(Reward));
	    }

	    #endregion

	    #region Player Take Reward

	    private void TakeReward(BasePlayer player, InformationPlayer.RewardPreset rewardPreset)
	    {
		    if (!DataPlayer[player.userID].InventoryPlayer.Contains(rewardPreset))
		    {
			    PrintError(LanguageEn
				    ? "Could not determine the item that arose with the developer, and the settings and data file came."
				    : "Не удалось определить предмет, свяжитесь с разработчиком и пришлите конфигурацию и дата-файл");
			    return;
		    }

		    DataPlayer[player.userID].InventoryPlayer.Remove(rewardPreset);
		    GiveReward(player, rewardPreset);
		    
		    DrawUI_ShowItems_Inventory(player);
	    }

	    #endregion
	    
	    #region Player Give Reward Item

	    private void GiveReward(BasePlayer player, InformationPlayer.RewardPreset rewardPreset)
	    {
		    if (rewardPreset.Type == TypeReward.Item)
		    {
			    if (rewardPreset.ItemSetting.IsBlueprint)
			    {
				    ItemDefinition definition = ItemManager.FindItemDefinition(rewardPreset.ItemSetting.Shortname);

				    Item itemBp = ItemManager.CreateByItemID(-996920608, rewardPreset.ItemSetting.Amount);
				    if (itemBp.instanceData == null)
					    itemBp.instanceData = new ProtoBuf.Item.InstanceData();
				    itemBp.instanceData.ShouldPool = false;
				    itemBp.instanceData.blueprintAmount = 1;
				    itemBp.instanceData.blueprintTarget = definition.itemid;
				    itemBp.MarkDirty();
				    
				    if(!itemBp.MoveToContainer(player.inventory.containerMain))
					    if (!itemBp.MoveToContainer(player.inventory.containerBelt))
					    {
						    SendChat(GetLang("CASE_MESSAGE_FULL_INVENTORY", player.UserIDString), player);
						    itemBp.DropAndTossUpwards(player.transform.position, 2f);
					    }
			    }
			    else
			    {
				    Item item = ItemManager.CreateByName(rewardPreset.ItemSetting.Shortname, rewardPreset.ItemSetting.Amount, rewardPreset.ItemSetting.SkinID);
				    
				    if(!item.MoveToContainer(player.inventory.containerMain))
					    if (!item.MoveToContainer(player.inventory.containerBelt))
					    {
						    SendChat(GetLang("CASE_MESSAGE_FULL_INVENTORY", player.UserIDString), player);
						    item.DropAndTossUpwards(player.transform.position, 2f);
					    }
			    }
		    }
		    else rust.RunServerCommand(rewardPreset.CommandSetting.Command.Replace("%STEAMID%", player.UserIDString));
	    }

	    #endregion

	    #endregion
	    
	    #region Interface

	    #region Inventory
	    
	    private void DrawUI_InventoryPlayer(BasePlayer player)
		{
			String Interface = InterfaceBuilder.GetInterface("UI_CASE_TEMPLATE_INVENTORY_PLAYER");
            if (Interface == null) return;

            Interface = Interface.Replace("%UI_CASE_DESCRIPTION_INVENTORY%", GetLang("UI_CASE_DESCRIPTION_INVENTORY", player.UserIDString));
            Interface = Interface.Replace("%UI_CASE_TITLE_INVENTORY%", GetLang("UI_CASE_TITLE_INVENTORY", player.UserIDString));
            Interface = Interface.Replace("%UI_CASE_BUTTON_BACK_CASE%", GetLang("UI_CASE_BUTTON_BACK_CASE", player.UserIDString));
            Interface = Interface.Replace("%UI_PROFILE_DESCRIPTION_YOUR_INVENTORY%", GetLang("UI_PROFILE_DESCRIPTION_YOUR_INVENTORY", player.UserIDString));
            
            CuiHelper.DestroyUi(player, InterfaceBuilder.UI_CASES_MENU);
            CuiHelper.DestroyUi(player, InterfaceBuilder.UI_CASES_MENU_INVENTORY);
            CuiHelper.AddUi(player, Interface);

            DrawUI_InventoryPlayer_Pages(player);
            DrawUI_ShowItems_Inventory(player);
		}	
	    
	    private void DrawUI_InventoryPlayer_Pages(BasePlayer player, Int32 Page = 0)
		{
			String Interface = InterfaceBuilder.GetInterface("UI_CASE_TEMPLATE_INVENTORY_PLAYER_PAGE");
            if (Interface == null) return;
            
            Interface = Interface.Replace("%BACK_PAGE_INVENTORY%", Page == 0 ? "" : $"case.func inventory.page.controller {Page - 1}");
            Interface = Interface.Replace("%NEXT_PAGE_INVENTORY%", DataPlayer[player.userID].InventoryPlayer.Count <= (Page + 1) * 40  ? "" :$"case.func inventory.page.controller {Page + 1}");

            CuiHelper.DestroyUi(player, "BackPageInventory");
            CuiHelper.DestroyUi(player, "NextPageInventory");
            CuiHelper.AddUi(player, Interface);
		}	
	    
	    private void DrawUI_ShowItems_Inventory(BasePlayer player, Int32 Page = 0) 
	    {
		    String Interface = InterfaceBuilder.GetInterface("UI_CASE_TEMPLATE_INVENTORY_ITEM_PANEL");
		    if (Interface == null) return;
		    
		    CuiHelper.DestroyUi(player, "InventoryItemPanel");
		    CuiHelper.AddUi(player, Interface);
		    
		    Int32 X = 0, Y = 0;
		    for (Int32 i = 40 * Page; i < 40 * (Page + 1); i++)
		    {
			    InformationPlayer.RewardPreset Reward = DataPlayer[player.userID].InventoryPlayer.Count - 1 >= i ? DataPlayer[player.userID].InventoryPlayer[i] : null;
			    DrawUI_InventoryItem(player, Reward, X, Y, i);
		    
			    X++;
			    if (X != 10) continue;
			    
			    X = 0;
			    Y += 1;
		    }
		    
		    DrawUI_InventoryPlayer_Pages(player, Page);
	    }	 	
	    
	    private void DrawUI_InventoryItem(BasePlayer player, InformationPlayer.RewardPreset rewardPreset, Int32 X, Int32 Y, Int32 I)
		{
			if (rewardPreset != null)
			{
				String Interface = InterfaceBuilder.GetInterface("UI_CASE_TEMPLATE_INVENTORY_ITEM");
				if (Interface == null) return;

				Interface = Interface.Replace("%PARENT_ITEM_INVENTOY%", $"ImageRareItem_{I}");
				Interface = Interface.Replace("%OFFSET_MIN%", $"{-506.467 + (X * 102)} {113.333 - (Y * 110)}");
				Interface = Interface.Replace("%OFFSET_MAX%", $"{-411.133 + (X * 102)} {216.667 - (Y * 110)}");
				
				Interface = Interface.Replace("%BACKGROUND_RARE%", ImageUi.GetImage(ImageUi.GetRareImage(rewardPreset.VisualRare)));
				Interface = Interface.Replace("%TITLE_ITEM%", rewardPreset.Type == TypeReward.Item ? (rewardPreset.ItemSetting.DisplayName != null && !String.IsNullOrWhiteSpace(rewardPreset.ItemSetting.DisplayName)) ? rewardPreset.ItemSetting.DisplayName : GetLang("UI_ITEM_LIST_ITEM_NAME", player.UserIDString) : (rewardPreset.CommandSetting.DisplayName != null && !String.IsNullOrWhiteSpace(rewardPreset.CommandSetting.DisplayName)) ? rewardPreset.CommandSetting.DisplayName : GetLang("UI_ITEM_LIST_COMMAND_NAME", player.UserIDString));
				Interface = Interface.Replace("%ITEM_AMOUNT%", rewardPreset.Type == TypeReward.Item ? rewardPreset.ItemSetting.GetAmountLabel : rewardPreset.CommandSetting.GetAmountLabel(player));

				CuiHelper.AddUi(player, Interface);
				
				if (rewardPreset.Type == TypeReward.Item && rewardPreset.ItemSetting.IsBlueprint)
					_interface.Item_Blueprint(player, $"ImageRareItem_{I}");

				if (!String.IsNullOrWhiteSpace(rewardPreset.PNGReward))
					DrawUI_ItemPNG(player, $"ImageRareItem_{I}",ImageUi.GetImage(rewardPreset.PNGReward), $"case.func inventory.take.reward {I}");
				else _interface.CaseItemsShortnameOrSkinID(player, ItemManager.FindItemDefinition(rewardPreset.ItemSetting.Shortname).itemid, UInt32.Parse(rewardPreset.ItemSetting.SkinID.ToString()), $"case.func inventory.take.reward {I}", $"ImageRareItem_{I}");
			}
			else DrawUI_NoRare_Panel(player, "InventoryItemPanel", "0.5 0.5", "0.5 0.5", $"{-506.467 + (X * 102)} {113.333 - (Y * 110)}", $"{-411.133 + (X * 102)} {216.667 - (Y * 110)}");
			
		}
	    
	    #endregion

	    private void DrawUI_Case(BasePlayer player)
		{
			if (!LocalRepositoryPlayer.ContainsKey(player))
				LocalRepositoryPlayer.Add(player, new LocalRepository());
			
			String Interface = InterfaceBuilder.GetInterface("UI_CASE_TEMPLATE_MENU");
            if (Interface == null) return;
            
            Interface = Interface.Replace("%UI_TITLE_CASE_INFO_ITEMS%", GetLang("UI_TITLE_CASE_INFO_ITEMS", player.UserIDString));
            Interface = Interface.Replace("%UI_TUBLER_INVENTORY_PLUGIN%", GetLang("UI_TUBLER_INVENTORY_PLUGIN", player.UserIDString));
            Interface = Interface.Replace("%UI_TUBLER_INVENTORY_PLAYER%", GetLang("UI_TUBLER_INVENTORY_PLAYER", player.UserIDString));
            Interface = Interface.Replace("%UI_PROFILE_TITLE_YOUR_AMOUNT_CASE%", GetLang("UI_PROFILE_TITLE_YOUR_AMOUNT_CASE", player.UserIDString));
            Interface = Interface.Replace("%UI_PROFILE_TITLE_YOUR_INVENTORY%", GetLang("UI_PROFILE_TITLE_YOUR_INVENTORY", player.UserIDString));
            Interface = Interface.Replace("%UI_PROFILE_DESCRIPTION_YOUR_INVENTORY%", GetLang("UI_PROFILE_DESCRIPTION_YOUR_INVENTORY", player.UserIDString));
            Interface = Interface.Replace("%UI_PROFILE_TITLE_IQECONOMIC_BALANCE%", GetLang("UI_PROFILE_TITLE_IQECONOMIC_BALANCE", player.UserIDString));

            CuiHelper.DestroyUi(player, InterfaceBuilder.UI_CASES_MENU_INVENTORY);
            CuiHelper.DestroyUi(player, InterfaceBuilder.UI_CASES_MENU);
            CuiHelper.AddUi(player, Interface);

            DrawUI_Tumbler(player);
            DrawUI_ShowCases(player, 0);
		}

	    private void DrawUI_UpdateLabels(BasePlayer player, String CaseKey)
	    {
		    Int32 AmountCase = DataPlayer[player.userID].GetAmountCase(CaseKey);

		    String Interface = InterfaceBuilder.GetInterface("UI_CASE_TEMPLATE_CASE_LABELS_AMOUNT");
		    if (Interface == null) return;
		    
		    Interface = Interface.Replace("%UI_PROFILE_DESCRIPTION_AMOUNT_IQECONOMIC_BALANCE%", GetLang("UI_PROFILE_DESCRIPTION_AMOUNT_IQECONOMIC_BALANCE", player.UserIDString, GetBalance(player.userID)));
		    if (_.IQEconomic || _.Economics || _.TPEconomic)
		    	Interface = Interface.Replace("%UI_PROFILE_DESCRIPTION_YOUR_AMOUNT_CASE%", GetLang("UI_PROFILE_DESCRIPTION_YOUR_AMOUNT_CASE", player.UserIDString, AmountCase).Replace(" штук", ""));
		    else
		    	Interface = Interface.Replace("%UI_PROFILE_DESCRIPTION_YOUR_AMOUNT_CASE%", GetLang("UI_PROFILE_DESCRIPTION_YOUR_AMOUNT_CASE", player.UserIDString, AmountCase));

		    CuiHelper.DestroyUi(player, "AmountThisCase");
		    CuiHelper.DestroyUi(player, "AmountBalanceUser");
		    CuiHelper.AddUi(player, Interface);

		    DrawUI_OpenedCase(player, AmountCase, CaseKey);
	    }		
	    
	    private void DrawUI_Tumbler(BasePlayer player)
	    {
		    String Interface = InterfaceBuilder.GetInterface("UI_CASE_TEMPLATE_TUMBLER");
		    if (Interface == null) return;

		    Configuration.Interface @interface = config.InterfaceSettings;
		    
		    Interface = Interface.Replace("%SWITCH_STATUS_TUMBLER%", DataPlayer[player.userID].SwitchTakeInventory ? ImageUi.GetImage(@interface.TumblerLeft) : ImageUi.GetImage(@interface.TumblerRight));

		    CuiHelper.DestroyUi(player, "SwitchInventory");
		    CuiHelper.AddUi(player, Interface);
	    }	
	    
	    private void DrawUI_ShowItems(BasePlayer player, String CaseKey)
	    {
		    String Interface = InterfaceBuilder.GetInterface("UI_CASE_TEMPLATE_ITEMS_PANEL");
		    if (Interface == null) return;
		    
		    CuiHelper.DestroyUi(player, "PanelItemCase");
		    CuiHelper.AddUi(player, Interface);

		    List<Configuration.CasesPreset.RewardPreset> OrderItems = config.CasesPresets[CaseKey].RewardPresets.OrderByDescending(x => x.VisualRare).ToList();
		    
		    Int32 X = 0, Y = 0;
		    for (Int32 i = 0; i < 16; i++)
		    { 
			    Configuration.CasesPreset.RewardPreset Reward = OrderItems.Count - 1 >= i ? OrderItems[i] : null;
			    DrawUI_ShowItem(player, i, X, Y, Reward);

			    X++;
			    if (X != 8) continue;
			    
			    X = 0;
			    Y++;
		    }
	    }	 	
	    
	    private void DrawUI_ShowItem(BasePlayer player, Int32 I, Int32 X, Int32 Y, Configuration.CasesPreset.RewardPreset Reward = null)
	    {
		    if (Reward != null)
		    {
			    String Interface = InterfaceBuilder.GetInterface("UI_CASE_TEMPLATE_ITEM");
			    if (Interface == null) return;

			    Interface = Interface.Replace("%PARENT_RARE%", $"RARE_{I}");
			    Interface = Interface.Replace("%OFFSET_MIN%", $"{-308 + (X * 79.5)} {80 - (Y * 79.5)}");
			    Interface = Interface.Replace("%OFFSET_MAX%", $"{-234 + (X * 79.5)} {149.8 - (Y * 79.5)}");
			    Interface = Interface.Replace("%ITEM_RARE_PNG%", ImageUi.GetImage(ImageUi.GetRareImage(Reward.VisualRare)));
			    //Interface = Interface.Replace("%RARE%", $"{Reward.VisualRare}%");
			    //Interface = Interface.Replace("%ITEM_NAME%", Reward.Type == TypeReward.Item ? (Reward.ItemSetting.DisplayName != null && !String.IsNullOrWhiteSpace(Reward.ItemSetting.DisplayName)) ? Reward.ItemSetting.DisplayName : GetLang("UI_ITEM_LIST_ITEM_NAME", player.UserIDString) : (Reward.CommandSetting.DisplayName != null && !String.IsNullOrWhiteSpace(Reward.CommandSetting.DisplayName)) ? Reward.CommandSetting.DisplayName : GetLang("UI_ITEM_LIST_COMMAND_NAME", player.UserIDString));
			    Interface = Interface.Replace("%AMOUNT_ITEM%", Reward.Type == TypeReward.Item ? Reward.ItemSetting.Amounts.GetAmountLabel : Reward.CommandSetting.GetAmountLabel(player));
	
			    CuiHelper.AddUi(player, Interface);
			    if (Reward.Type == TypeReward.Item && Reward.ItemSetting.IsBlueprint)
				    _interface.Item_Blueprint(player, $"RARE_{I}");
			    
			    if (!String.IsNullOrWhiteSpace(Reward.PNGReward))
				    DrawUI_ItemPNG(player, $"RARE_{I}",ImageUi.GetImage(Reward.PNGReward));
			    else _interface.CaseItemsShortnameOrSkinID(player, ItemManager.FindItemDefinition(Reward.ItemSetting.Shortname).itemid, UInt32.Parse(Reward.ItemSetting.SkinID.ToString()), "", $"RARE_{I}");
		   		

			    CuiElementContainer container = new CuiElementContainer();
			    container.Add(new CuiElement
			    {
				    Name = "PngRare_Mini",
				    Parent = $"RARE_{I}",
				    Components =
				    {
					    new CuiRawImageComponent { Color = "1 1 1 1", Png = ImageUi.GetImage(config.InterfaceSettings.RareMiniIcon) },
					    new CuiRectTransformComponent
					    {
						    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "13 -30", OffsetMax = "21 -22"
					    }
				    }
			    });
			    
			    container.Add(new CuiElement
			    {
				    Name = "RareAmount",
				    Parent = $"RARE_{I}",
				    Components =
				    {
					    new CuiTextComponent
					    {
						    Text = $"{Reward.VisualRare}%", Font = "robotocondensed-regular.ttf", FontSize = 9,
						    Align = TextAnchor.MiddleLeft, Color = "1 1 1 1",
					    },
					    new CuiRectTransformComponent
					    {
						    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "22 -34", OffsetMax = "42 -18"
					    }
				    }
			    });
			    CuiHelper.AddUi(player, container);
		    }
		    else DrawUI_NoRare_Panel(player, "PanelItemCase", "0.5 0.5", "0.5 0.5", $"{-308 + (X * 79.5)} {80 - (Y * 79.5)}", $"{-234 + (X * 79.5)} {149 - (Y * 79.5)}");
	    }

	    private void DrawUI_NoRare_Panel(BasePlayer player, String Parent, String AnchorMin, String AnchorMax, String OffsetMin, String OffsetMax, String Color = "1 1 1 1")
	    {
		    String Interface = InterfaceBuilder.GetInterface("UI_CASE_TEMPLATE_ITEM_EMPTY");
		    if (Interface == null) return;
			   
		    Interface = Interface.Replace("%COLOR%", Color);
		    Interface = Interface.Replace("%PARENT_RARE%", Parent);
		    Interface = Interface.Replace("%ANCHOR_MIN%", AnchorMin);
		    Interface = Interface.Replace("%ANCHOR_MAX%", AnchorMax);
		    Interface = Interface.Replace("%OFFSET_MIN%", OffsetMin);
		    Interface = Interface.Replace("%OFFSET_MAX%", OffsetMax);
			    
		    CuiHelper.AddUi(player, Interface);
	    }
	    
	    private void DrawUI_ShowCases(BasePlayer player, Int32 Page)
	    {
		    String Interface = InterfaceBuilder.GetInterface("UI_CASE_TEMPLATE_CASES");
		    if (Interface == null) return;
		  
		    String SelectCaseKey = CaseKeys[Page];
		    
		    String BackCaseKey = Page - 1 >= 0 ? CaseKeys[Page - 1] : CaseKeys[CaseKeys.Count - 1];
		    String NextCaseKey = Page + 1 >= CaseKeys.Count ? CaseKeys[0] : CaseKeys[Page + 1];

		    Configuration.CasesPreset CaseCentral = config.CasesPresets[SelectCaseKey];
		    Configuration.CasesPreset CaseBack = config.CasesPresets[BackCaseKey];
		    Configuration.CasesPreset CaseNext = config.CasesPresets[NextCaseKey];

		    Boolean IsBuy = CaseCentral.EconomicSettings.IsBuy &&
		                    GetBalance(player.userID) >= CaseCentral.EconomicSettings.PriceBuyCase;
		    
		    Boolean IsSell = CaseCentral.EconomicSettings.IsSell &&
		                     DataPlayer[player.userID].GetAmountCase(SelectCaseKey) > 0;
		    Interface = Interface.Replace("%THREE_CASE_PNG%", ImageUi.GetImage(CaseBack.PNGCase));
		    Interface = Interface.Replace("%TWO_CASE_PNG%", ImageUi.GetImage(CaseNext.PNGCase));
		    Interface = Interface.Replace("%CENTRAL_CASE_PNG%", ImageUi.GetImage(CaseCentral.PNGCase));
		    Interface = Interface.Replace("%CASENAME%", CaseCentral.caseName.GetCaseName(player)); 
		    Interface = Interface.Replace("%UI_CASE_BUTTON_SELL_BUTTON%", GetLang("UI_CASE_BUTTON_SELL_BUTTON", player.UserIDString));
		    Interface = Interface.Replace("%COMMAND_SELL_CASE%", !IsSell ? "" : $"case.func sell.case.economic {SelectCaseKey}"); 
		    Interface = Interface.Replace("%UI_CASE_BUTTON_BUY_BUTTON%", GetLang("UI_CASE_BUTTON_BUY_BUTTON", player.UserIDString));
		    Interface = Interface.Replace("%COMMAND_BUY_CASE%", !IsBuy ? "" : $"case.func buy.case.economic {SelectCaseKey}");
		    Interface = Interface.Replace("%PRICE_BUY%", !IsBuy ? "" : $"<b>-{CaseCentral.EconomicSettings.PriceBuyCase}</b>");
		    Interface = Interface.Replace("%PRICE_SELL%", !IsSell ? "" : $"<b>+{CaseCentral.EconomicSettings.PriceSellCase}</b>");
		    Interface = Interface.Replace("%BACK_PAGE%", $"case.func page.controller {Page - 1}");
		    Interface = Interface.Replace("%NEXT_PAGE%", $"case.func page.controller {Page + 1}");
		    
		    CuiHelper.DestroyUi(player, "BackgroundPanelCasesMain");
		    CuiHelper.AddUi(player, Interface);

		    DrawUI_UpdateLabels(player, SelectCaseKey);

		    if (_.IQEconomic || _.Economics || _.TPEconomic)
		    {
			    if (!IsBuy)
				    DrawUI_BlockButton(player, "BuyCase");

			    if (!IsSell)
				    DrawUI_BlockButton(player, "SellCase");
		    }

		    DrawUI_ShowItems(player, SelectCaseKey);
	    }
	    private void DrawUI_BlockButton(BasePlayer player, String Parent)
	    {
		    String Interface = InterfaceBuilder.GetInterface("UI_CASE_TEMPLATE_CASE_BUTTON_BLOCK");
		    if (Interface == null) return;

		    Interface = Interface.Replace("%PARENT%", Parent);

		    CuiHelper.AddUi(player, Interface);
	    }

	    private void DrawUI_ItemPNG(BasePlayer player, String Parent, String PNG, String Command = "")
	    {
		    String Interface = InterfaceBuilder.GetInterface("UI_CASE_TEMPLATE_ITEM_PNG");
		    if (Interface == null) return;
		    
		    Interface = Interface.Replace("%PARENT_RARE%", Parent);
		    Interface = Interface.Replace("%PNG_ITEM%", PNG);
		    Interface = Interface.Replace("%COMMAND_TAKE%", Command);
		    
		    CuiHelper.AddUi(player, Interface);
	    }

	    private void DrawUI_OpenedCase(BasePlayer player, Int32 AmountCase, String CaseKey)
	    {
		    String Interface = InterfaceBuilder.GetInterface("UI_CASE_TEMPLATE_CASE_BUTTON_OPENED");
		    if (Interface == null) return;

		    Configuration.CasesPreset.RestrictionsPreset CaseRestriction = config.CasesPresets[CaseKey].RestrictionsSettings;
		    String RankName = IsRankBlocked(player, CaseRestriction.IQRankKey);

		    Interface = Interface.Replace("%UI_CASE_BUTTON_OPEN_CASE%", !IsPermissionBlocked(player, CaseRestriction.Permissions) ? GetLang("UI_CASE_PERMISSION_BLOCKED", player.UserIDString) : !String.IsNullOrWhiteSpace(RankName) ? GetLang("UI_CASE_IQRANK_BLOCKED", player.UserIDString, RankName) : AmountCase > 0 ? GetLang("UI_CASE_BUTTON_OPEN_CASE", player.UserIDString) : GetLang("UI_CASE_BUTTON_NO_OPEN_AMOUNT_CASE", player.UserIDString));
		    Interface = Interface.Replace("%COMMAND_OPEN_CASE%", !IsPermissionBlocked(player, CaseRestriction.Permissions) ? "" :  !String.IsNullOrWhiteSpace(RankName) ? "" : AmountCase > 0 ? $"case.func open.case {CaseKey}" : "");

		    CuiHelper.DestroyUi(player, "OpenCaseBTN");
		    CuiHelper.AddUi(player, Interface);
	    }
	    
	    private class InterfaceBuilder
	    {
		    #region Vars

		    public static InterfaceBuilder Instance;
		    public const String UI_CASES_MENU = "UI_CASES_MENU";
		    public const String UI_CASES_MENU_INVENTORY = "UI_CASES_MENU_INVENTORY";
		    public Dictionary<String, String> Interfaces;

		    #endregion

		    #region Main

		    public InterfaceBuilder()
		    {
			    Instance = this;
			    Interfaces = new Dictionary<String, String>();

			    Building_CaseMenu();
			    Building_Tumbler();
			    Building_Cases();
			    
			    Building_Cases_Label_Amounts();
			    Building_CaseItemsPNG();
			    
			    Building_CaseItemsPanel();
			    Building_CaseItem();
			    Building_CaseItem_Empty();
			    
			    Building_Cases_ButtonClose();
			    Building_Cases_Opened();
			    
			    Building_Inventory_Player();
			    Building_Inventory_Items_Panel();
			    Building_Inventory_Item();
			    Building_Inventory_Player_Pages();
		    }

		    public static void AddInterface(String name, String json)
		    {
			    if (Instance.Interfaces.ContainsKey(name))
			    {
				    _.PrintError($"Error! Tried to add existing cui elements! -> {name}");
				    return;
			    }

			    Instance.Interfaces.Add(name, json);
		    }

		    public static String GetInterface(String name)
		    {
			    String json = String.Empty;
			    if (Instance.Interfaces.TryGetValue(name, out json) == false)
			    {
				    _.PrintWarning($"Warning! UI elements not found by name! -> {name}");
			    }

			    return json;
		    }

		    public static void DestroyAll()
		    {
			    foreach (BasePlayer player in BasePlayer.activePlayerList)
			    {
				    CuiHelper.DestroyUi(player, UI_CASES_MENU);
				    CuiHelper.DestroyUi(player, UI_CASES_MENU_INVENTORY);
			    }
		    }

		    #endregion

		    #region Building CaseMenu

		    private void Building_CaseMenu()
		    {
			    CuiElementContainer container = new CuiElementContainer();

			    container.Add(new CuiPanel
			    {
				    CursorEnabled = true,
				    Image = { Color = "0 0 0 0.75", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
				    RectTransform =
					    { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0.009", OffsetMax = "0 0.001" }
			    }, ".Mains", UI_CASES_MENU);
			    
			    container.Add(new CuiElement
	            {
	                Parent = UI_CASES_MENU,
	                Components ={
	                    new CuiRawImageComponent{Png = (string) _.ImageLibrary.Call("GetImage", "background3"),},
	                    new CuiRectTransformComponent{AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-592 -333", OffsetMax = "581 335"}}
	            });

			    container.Add(new CuiButton
			    {
				    Button = { Color = "1 1 1 0", Command = "CloseTPCasesMenu423" },
				    Text = { Text = "", Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "0 0 0 0" },
				    RectTransform =
				    {
					    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "347 204", OffsetMax = "367 223"
				    }
			    }, UI_CASES_MENU);
			    
			  /*  container.Add(new CuiPanel
			    {
				    CursorEnabled = false,
				    Image = { Color = "1 1 1 0.4" },
				    RectTransform =
				    {
					    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-506.733 -77.133",
					    OffsetMax = "-486.733 -75.8"
				    }
			    }, UI_CASES_MENU, "StaticLineOne");

			    container.Add(new CuiPanel
			    {
				    CursorEnabled = false,
				    Image = { Color = "1 1 1 0.4" },
				    RectTransform =
				    {
					    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-245.333 -77.133",
					    OffsetMax = "274 -75.8"
				    }
			    }, UI_CASES_MENU, "StaticLineTwo");*/


			    container.Add(new CuiElement
			    {
				    Name = "StaticLabelItemsCase",
				    Parent = UI_CASES_MENU,
				    Components =
				    {
					    new CuiTextComponent
					    {
						    Text = "%UI_TITLE_CASE_INFO_ITEMS%",
						    Font = "robotocondensed-regular.ttf", FontSize = 11, Align = TextAnchor.MiddleLeft,
						    Color = "1 1 1 0.6"
					    },
					    new CuiRectTransformComponent
					    {
						    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-300 -40", OffsetMax = "-74 -22"
					    }
				    }
			    });
			    container.Add(new CuiElement
			    {
				    Name = "StaticLabelDropItem_Inventory",
				    Parent = UI_CASES_MENU,
				    Components =
				    {
					    new CuiTextComponent
					    {
						    Text = "%UI_TUBLER_INVENTORY_PLUGIN%", Font = "robotocondensed-regular.ttf", FontSize = 11,
						    Align = TextAnchor.MiddleLeft, Color = "1 1 1 0.6"
					    },
					    new CuiRectTransformComponent
					    {
						    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "112 -40", OffsetMax = "211 -22"
					    }
				    }
			    });

			    container.Add(new CuiElement
			    {
				    Name = "StaticLabelDropItem_MyInventory",
				    Parent = UI_CASES_MENU,
				    Components =
				    {
					    new CuiTextComponent
					    {
						    Text = "%UI_TUBLER_INVENTORY_PLAYER%", Font = "robotocondensed-regular.ttf", FontSize = 11,
						    Align = TextAnchor.MiddleLeft, Color = "1 1 1 0.6"
					    },
					    new CuiRectTransformComponent
					    {
						    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "238 -40", OffsetMax = "336 -22"
					    }
				    }
			    });

			 /*   container.Add(new CuiElement
			    {
				    Name = "StaticInfoCaseAmountPanel",
				    Parent = UI_CASES_MENU,
				    Components =
				    {
					    new CuiRawImageComponent { Color = "1 1 1 1", Png = ImageUi.GetImage(config.InterfaceSettings.PanelCasesInfoAmount) },
					    new CuiRectTransformComponent
					    {
						    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "355.127 293.867",
						    OffsetMax = "506.46 324.533"
					    }
				    }
			    });*/

			    container.Add(new CuiPanel
			    {
				    CursorEnabled = false,
				    Image = { Color = "1 1 1 0" },
				    RectTransform =
				    {
					    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-290 150", OffsetMax = "-196 175"
				    }
			    }, UI_CASES_MENU, "StaticInfoCaseAmountPanel");
				

			   /* container.Add(new CuiElement
			    {
				    Name = "StaticInfoCaseInventoryPanel",
				    Parent = UI_CASES_MENU,
				    Components =
				    {
					    new CuiRawImageComponent { Color = "1 1 1 1", Png = ImageUi.GetImage(config.InterfaceSettings.PanelCasesInventoryPanel)},
					    new CuiRectTransformComponent
					    {
						    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "387.793 257.067", OffsetMax = "506.46 287.733"
					    }
				    }
			    });*/


			  /*  container.Add(new CuiElement
			    {
				    Name = "DescriptionInfo",
				    Parent = "StaticInfoCaseInventoryPanel",
				    Components =
				    {
					    new CuiTextComponent
					    {
						    Text = "%UI_PROFILE_DESCRIPTION_YOUR_INVENTORY%", Font = "robotocondensed-regular.ttf", FontSize = 11,
						    Align = TextAnchor.UpperLeft, Color = "1 1 1 1"
					    },
					    new CuiRectTransformComponent
					    {
						    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-24.057 -13.083",
						    OffsetMax = "51.133 0"
					    }
				    }
			    });*/

			    if (_.IQEconomic || _.Economics || _.TPEconomic)
			    {
				    container.Add(new CuiPanel
				    {
					    CursorEnabled = false,
					    Image = { Color = "1 1 1 0" },
					    RectTransform =
					    {
						    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-290 135", OffsetMax = "-196 155"
					    }
				    }, UI_CASES_MENU, "StaticInfoCaseBalance");

				    container.Add(new CuiElement
				    {
					    Name = "TitleBalanceInfo",
					    Parent = "StaticInfoCaseBalance",
					    Components =
					    {
						    new CuiTextComponent
						    {
							    Text = "%UI_PROFILE_TITLE_IQECONOMIC_BALANCE%", Font = "robotocondensed-regular.ttf", FontSize = 11,
							    Align = TextAnchor.MiddleLeft, Color = "1 1 1 0.6"
						    },
						    new CuiRectTransformComponent
						    {
							    AnchorMin = "0.1 0.1", AnchorMax = "1 0.7"
						    }
					    }
				    });

				    container.Add(new CuiElement
				    {
					    Name = "TitleCaseInfo",
					    Parent = "StaticInfoCaseAmountPanel",
					    Components =
					    {
						    new CuiTextComponent
						    {
							    Text = "%UI_PROFILE_TITLE_YOUR_AMOUNT_CASE%", Font = "robotocondensed-regular.ttf", FontSize = 9,
							    Align = TextAnchor.UpperLeft, Color = "1 1 1 0.6"
						    },
						    new CuiRectTransformComponent
						    {
							    AnchorMin = "0.09 0", AnchorMax = "1 0.7", OffsetMin = "0 0", OffsetMax = "0 0"
						    }
					    }
				    });
			    }
			    else
			    {
			    	container.Add(new CuiElement
				    {
					    Name = "TitleCaseInfo",
					    Parent = "StaticInfoCaseAmountPanel",
					    Components =
					    {
						    new CuiTextComponent
						    {
							    Text = "%UI_PROFILE_TITLE_YOUR_AMOUNT_CASE%", Font = "robotocondensed-regular.ttf", FontSize = 11,
							    Align = TextAnchor.MiddleLeft, Color = "1 1 1 0.6"
						    },
						    new CuiRectTransformComponent
						    {
							    AnchorMin = "0.1 0", AnchorMax = "1 0.75", OffsetMin = "0 0", OffsetMax = "0 0"
						    }
					    }
				    });
			    }

			    
			    container.Add(new CuiPanel
			    {
				    CursorEnabled = false,
				    Image = { Color = "1 1 1 0" },
				    RectTransform =
				    {
					    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "190 150", OffsetMax = "290 173"
				    }
			    }, UI_CASES_MENU, "StaticInfoCaseInventoryPanel");

			    container.Add(new CuiElement
			    {
				    Name = "TitleButtonInfo",
				    Parent = "StaticInfoCaseInventoryPanel",
				    Components =
				    {
					    new CuiTextComponent
					    {
						    Text = "%UI_PROFILE_TITLE_YOUR_INVENTORY%", Font = "robotocondensed-regular.ttf", FontSize = 11,
						    Align = TextAnchor.UpperLeft, Color = "1 1 1 0.6"
					    },
					    new CuiRectTransformComponent
					    {
						    AnchorMin = "0.2 0", AnchorMax = "1 0.8"
					    }
				    }
			    });

			    container.Add(new CuiButton
			    {
				    Button = { Color = "1 1 1 0", Command = "case.func open.inventory"},
				    Text =
				    {
					    Text = "", Font = "robotocondensed-regular.ttf", FontSize = 14,
					    Align = TextAnchor.MiddleCenter, Color = "0 0 0 1"
				    },
				    RectTransform =
				    {
					    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-59.333 -15.333",
					    OffsetMax = "59.333 15.333"
				    }
			    }, "StaticInfoCaseInventoryPanel", "СlickButtonInventory");
			    AddInterface("UI_CASE_TEMPLATE_MENU", container.ToJson());
		    }

		    #endregion

		    #region Building Case Items

		    private Single FadeIn = 0f;
		    private Single FadeOut = 0f;
		    private void Building_CaseItemsPNG()
		    {
			    CuiElementContainer container = new CuiElementContainer();
			    
			    container.Add(new CuiElement
			    {
				    FadeOut = FadeOut,
				    Name = "PngItem",
				    Parent = "%PARENT_RARE%",
				    Components =
				    {
					    new CuiRawImageComponent { FadeIn = FadeIn, Color = "1 1 1 1", Png = "%PNG_ITEM%" },
					    new CuiRectTransformComponent
					    {
						    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-26 -30",
						    OffsetMax = "26 22"
					    }
				    }
			    });
			    
			    container.Add(new CuiButton
			    {
				    FadeOut = FadeOut,
				    Button = { FadeIn = FadeIn, Color = "1 1 1 0", Command = "%COMMAND_TAKE%"},
				    Text =
				    {
					    Text = "", Font = "robotocondensed-regular.ttf", FontSize = 14,
					    Align = TextAnchor.MiddleCenter, Color = "0 0 0 0"
				    },
				    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" }
			    }, "%PARENT_RARE%", "TakeButton");

			     AddInterface("UI_CASE_TEMPLATE_ITEM_PNG", container.ToJson());
		    }	
		    
		    public void CaseItemsShortnameOrSkinID(BasePlayer player, Int32 ItemID, UInt32 SkinID, String Command, String Parent)
		    {
			    CuiElementContainer container = new CuiElementContainer();
			    
			    container.Add(new CuiElement
			    {
				    FadeOut = FadeOut,
				    Name = "PngItem",
				    Parent = Parent,
				    Components =
				    {
					    new CuiImageComponent() { FadeIn = FadeIn, Color = "1 1 1 1", ItemId = ItemID, SkinId = SkinID },
					    new CuiRectTransformComponent
					    {
						    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-26 -30",
						    OffsetMax = "26 22"
					    }
				    }
			    });
			    
			    container.Add(new CuiButton
			    {
				    FadeOut = FadeOut,
				    Button = { FadeIn = FadeIn, Color = "1 1 1 0", Command = Command },
				    Text =
				    {
					    Text = "", Font = "robotocondensed-regular.ttf", FontSize = 14,
					    Align = TextAnchor.MiddleCenter, Color = "0 0 0 0"
				    },
				    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" }
			    }, Parent, "TakeButton");
			    
			    CuiHelper.AddUi(player, container);
		    }	
		    
		    private void Building_CaseItemsPanel()
		    {
			    CuiElementContainer container = new CuiElementContainer();

			     container.Add(new CuiPanel
			    {
				    FadeOut = FadeOut,
				    CursorEnabled = false,
				    Image = { FadeIn = FadeIn, Color = "1 1 1 0" },
				    RectTransform =
				    {
					    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-509.564 -310.109", OffsetMax = "506.462 -92.667"
				    }
			    }, UI_CASES_MENU, "PanelItemCase");

			     AddInterface("UI_CASE_TEMPLATE_ITEMS_PANEL", container.ToJson());
		    }
		    
		    private void Building_CaseItem()
		    {
			    CuiElementContainer container = new CuiElementContainer();
			    
			    container.Add(new CuiElement
			    {
				    FadeOut = FadeOut,
				    Name = "%PARENT_RARE%",
				    Parent = "PanelItemCase",
				    Components =
				    {
					    new CuiRawImageComponent { FadeIn = FadeIn, Color = "1 1 1 1", Png = "%ITEM_RARE_PNG%" },
					    new CuiRectTransformComponent
					    { 
						    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "%OFFSET_MIN%", OffsetMax = "%OFFSET_MAX%" 
					    }
				    }
			    });
			 /*   
			    container.Add(new CuiElement
			    {
				    FadeOut = FadeOut,
				    Name = "NameItem",
				    Parent = "%PARENT_RARE%",
				    Components =
				    {
					    new CuiTextComponent
					    {
						    Text = "%ITEM_NAME%", Font = "robotocondensed-regular.ttf", FontSize = 10,
						    Align = TextAnchor.LowerLeft, Color = "1 1 1 1",
						    FadeIn = FadeIn,
					    },
					    new CuiRectTransformComponent
					    {
						    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-41.099 33.159", OffsetMax = "12.915 47.908" 
					    }
				    }
			    });*/
			    
			    container.Add(new CuiElement
			    {
				    FadeOut = FadeOut,
				    Name = "AmountItem",
				    Parent = "%PARENT_RARE%",
				    Components =
				    {
					    new CuiTextComponent
					    {
						    Text = "%AMOUNT_ITEM%", Font = "robotocondensed-regular.ttf", FontSize = 8,
						    Align = TextAnchor.MiddleLeft, Color = "0.6603774 0.6603774 0.6603774 1",
						    FadeIn = FadeIn,
					    },
					    new CuiRectTransformComponent
					    {
						    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-33 22",
						    OffsetMax = "41 32"
					    }
				    }
			    });

			    AddInterface("UI_CASE_TEMPLATE_ITEM", container.ToJson());
		    }
		    
		    private void Building_CaseItem_Empty()
		    {
			    CuiElementContainer container = new CuiElementContainer();

			    container.Add(new CuiElement
			    {
				    FadeOut = FadeOut,
				    Name = "ItemNoRareCase",
				    Parent = "%PARENT_RARE%",
				    Components =
				    {
					    new CuiRawImageComponent { FadeIn = FadeIn, Color = "%COLOR%", Png = ImageUi.GetImage(config.InterfaceSettings.NoRareBackground) },
					    new CuiRectTransformComponent
					    { 
						    AnchorMin = "%ANCHOR_MIN%", AnchorMax = "%ANCHOR_MAX%", OffsetMin = "%OFFSET_MIN%", OffsetMax = "%OFFSET_MAX%"
					    }
				    }
			    });

			     AddInterface("UI_CASE_TEMPLATE_ITEM_EMPTY", container.ToJson());
		    }

		    #endregion
		    
		    #region Building Tumbler

		    private void Building_Tumbler()
		    {
			    CuiElementContainer container = new CuiElementContainer();
				
			    container.Add(new CuiElement
			    {
				    FadeOut = FadeOut,
				    Name = "SwitchInventory",
				    Parent = UI_CASES_MENU,
				    Components =
				    {
					    new CuiRawImageComponent { FadeIn = FadeIn, Color = "1 1 1 1", Png = "%SWITCH_STATUS_TUMBLER%"},
					    new CuiRectTransformComponent
					    {
						    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "209.8 -38.133", OffsetMax = "233.133 -24.8"
					    }
				    }
			    });
			    
			    container.Add(new CuiButton
			    {
				    FadeOut = FadeOut,
				    Button = { FadeIn = FadeIn, Color = "0 0 0 0", Command = "case.func switch.inventory"},
				    Text = { Text = "", Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "0 0 0 0" },
				    RectTransform =
				    {
					    AnchorMin = "0 0", AnchorMax = "1 1"
				    }
			    }, "SwitchInventory");
			    
			    AddInterface("UI_CASE_TEMPLATE_TUMBLER", container.ToJson());
		    }

		    #endregion

		    #region Buiding_Cases

		    private void Building_Cases()
		    {
			    CuiElementContainer container = new CuiElementContainer();

			    container.Add(new CuiPanel
			    {
				    FadeOut = FadeOut,
				    CursorEnabled = false,
				    Image = { FadeIn = FadeIn, Color = "1 1 1 0" },
				    RectTransform =
				    {
					    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-208 -47",
					    OffsetMax = "205 287"
				    }
			    }, UI_CASES_MENU, "BackgroundPanelCasesMain");

			    container.Add(new CuiElement
			    {
				    FadeOut = FadeOut,
				    Name = "ThreeCase",
				    Parent = "BackgroundPanelCasesMain",
				    Components =
				    {
					    new CuiRawImageComponent { FadeIn = FadeIn, Color = "1 1 1 0.3", Png = "%THREE_CASE_PNG%" },
					    new CuiRectTransformComponent
					    {
						    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-177 -34", OffsetMax = "-83 59"
					    }
				    }
			    });

			    container.Add(new CuiElement
			    {
				    FadeOut = FadeOut,
				    Name = "TwoCase",
				    Parent = "BackgroundPanelCasesMain",
				    Components =
				    {
					    new CuiRawImageComponent { FadeIn = FadeIn, Color = "1 1 1 0.3", Png = "%TWO_CASE_PNG%" },
					    new CuiRectTransformComponent
					    {
						    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "89 -34", OffsetMax = "183 59"
					    }
				    }
			    });

			/*    container.Add(new CuiElement
			    {
				    FadeOut = FadeOut,
				    Name = "Effect",
				    Parent = "BackgroundPanelCasesMain",
				    Components =
				    {
					    new CuiRawImageComponent { FadeIn = FadeIn, Color = "1 1 1 1", Png = ImageUi.GetImage(config.InterfaceSettings.EffectCases) },
					    new CuiRectTransformComponent
					    {
						    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-300 -93.258",
						    OffsetMax = "306 158.075"
					    }
				    }
			    });*/

			    container.Add(new CuiElement
			    {
				    FadeOut = FadeOut,
				    Name = "MainCase",
				    Parent = "BackgroundPanelCasesMain",
				    Components =
				    {
					    new CuiRawImageComponent { FadeIn = FadeIn, Color = "1 1 1 1", Png = "%CENTRAL_CASE_PNG%"},
					    new CuiRectTransformComponent
					    {
						    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-93 -94", OffsetMax = "99 99"
					    }
				    }
			    });
			    
			    container.Add(new CuiElement
			    {
				    Name = "NameMainCase",
				    Parent = "MainCase",
				    Components = {
					    new CuiTextComponent { Text = "<b>%CASENAME%</b>", Font = "robotocondensed-regular.ttf", FontSize = 25, Align = TextAnchor.LowerCenter, Color = "1 1 1 1" },
					    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-126.61 -123.869", OffsetMax = "126.61 -79.998" }				    }
			    });
			    
		/*	    container.Add(new CuiElement
			    {
				    FadeOut = FadeOut,
				    Name = "ButtonRightPanel",
				    Parent = "BackgroundPanelCasesMain",
				    Components =
				    {
					    new CuiRawImageComponent { FadeIn = FadeIn, Color = "1 1 1 1",  Png = ImageUi.GetImage(config.InterfaceSettings.NextPage) },
					    new CuiRectTransformComponent
					    {
						    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "271.8 12.408",
						    OffsetMax = "311.8 52.408"
					    }
				    }
			    });*/
			    
			    container.Add(new CuiButton
			    {
				    FadeOut = FadeOut,
				    Button = { FadeIn = FadeIn, Color = "1 1 1 0", Command = "%NEXT_PAGE%"},
				    Text =
				    {
					    Text = "", Font = "robotocondensed-regular.ttf", FontSize = 14,
					    Align = TextAnchor.MiddleCenter, Color = "0 0 0 0"
				    },
				    RectTransform =
					    { 
					    	AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "185 -10", OffsetMax = "210 15"
						}
			    }, "BackgroundPanelCasesMain", "ButtonRight");
			    
			   /* container.Add(new CuiElement
			    {
				    FadeOut = FadeOut,
				    Name = "ButtonLeftPanel",
				    Parent = "BackgroundPanelCasesMain",
				    Components =
				    {
					    new CuiRawImageComponent { FadeIn = FadeIn, Color = "1 1 1 1", Png = ImageUi.GetImage(config.InterfaceSettings.BackPage) },
					    new CuiRectTransformComponent
					    {
						    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-311.8 12.408",
						    OffsetMax = "-271.8 52.408"
					    }
				    }
			    });*/
			    
			    container.Add(new CuiButton
			    {
				    FadeOut = FadeOut,
				    Button = { FadeIn = FadeIn, Color = "1 1 1 0", Command = "%BACK_PAGE%"},
				    Text =
				    {
					    Text = "", Font = "robotocondensed-regular.ttf", FontSize = 14,
					    Align = TextAnchor.MiddleCenter, Color = "0 0 0 0"
				    },
				    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-205 -10", OffsetMax = "-180 15" }
			    }, "BackgroundPanelCasesMain", "ButtonLeft");

			   /* container.Add(new CuiElement
			    {
				    FadeOut = FadeOut,
				    Name = "OpenCaseButtonPanel",
				    Parent = "BackgroundPanelCasesMain",
				    Components =
				    {
					    new CuiRawImageComponent { FadeIn = FadeIn, Color = "1 1 1 1", Png = ImageUi.GetImage(config.InterfaceSettings.OpenCaseButton) },
					    new CuiRectTransformComponent
					    {
						    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-83 -124", OffsetMax = "89 -63"
					    }
				    }
			    });*/

			    container.Add(new CuiPanel
			    {
				    FadeOut = FadeOut,
				    CursorEnabled = false,
				    Image = { FadeIn = FadeIn, Color = "1 1 1 0" },
				    RectTransform =
				    {
					    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-80 -100", OffsetMax = "89 -73"
				    }
			    }, "BackgroundPanelCasesMain", "OpenCaseButtonPanel");

			    if (_.IQEconomic || _.Economics || _.TPEconomic)
			    {
				    container.Add(new CuiElement
				    {
					    FadeOut = FadeOut,
					    Name = "SellButtonCase",
					    Parent = "BackgroundPanelCasesMain",
					    Components =
					    {
						    new CuiRawImageComponent
							    { FadeIn = FadeOut, Color = "1 1 1 1", Png = ImageUi.GetImage(config.InterfaceSettings.SellCaseButton) },
						    new CuiRectTransformComponent
						    {
							    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-77 -122", OffsetMax = "0 -102"
						    }
					    }
				    });

				    container.Add(new CuiElement
				    {
					    FadeOut = FadeOut,
					    Name = "SellLabel",
					    Parent = "SellButtonCase",
					    Components =
					    {
						    new CuiTextComponent
						    {
							    Text = "%UI_CASE_BUTTON_SELL_BUTTON%", Font = "robotocondensed-regular.ttf",
							    FontSize = 11, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1",
							    FadeIn = FadeIn,
						    },
						    new CuiRectTransformComponent
						    {
							    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-16.003 -15.333", OffsetMax = "54.177 15.333"
						    }
					    }
				    });

				    container.Add(new CuiButton
				    {
					    FadeOut = FadeOut,
					    Button = { Color = "1 1 1 0", Command = "%COMMAND_SELL_CASE%" },
					    Text =
					    {
						    Text = "", Font = "robotocondensed-regular.ttf", FontSize = 14,
						    Align = TextAnchor.MiddleCenter, Color = "1 1 1 0",
						    FadeIn = FadeIn,
					    },
					    RectTransform =
					    {
						    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-57.333 -15.333",
						    OffsetMax = "57.333 15.333"
					    }
				    }, "SellButtonCase", "SellCase");

				    container.Add(new CuiElement
				    {
					    FadeOut = FadeOut,
					    Name = "BuyButtonCase",
					    Parent = "BackgroundPanelCasesMain",
					    Components =
					    {
						    new CuiRawImageComponent
							    { FadeIn = FadeIn, Color = "1 1 1 1", Png = ImageUi.GetImage(config.InterfaceSettings.BuyCaseButton) },
						    new CuiRectTransformComponent
						    {
							    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "10 -122", OffsetMax = "87 -102"
						    }
					    }
				    });

				    container.Add(new CuiElement
				    {
					    FadeOut = FadeOut,
					    Name = "BuyLabel",
					    Parent = "BuyButtonCase",
					    Components =
					    {
						    new CuiTextComponent
						    {
							    Text = "%UI_CASE_BUTTON_BUY_BUTTON%", Font = "robotocondensed-regular.ttf",
							    FontSize = 11, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1",
							    FadeIn = FadeIn,
						    },
						    new CuiRectTransformComponent
						    {
							    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-16.49 -15.333", OffsetMax = "55.69 15.333"
						    }
					    }
				    });

				    container.Add(new CuiButton
				    {
					    FadeOut = FadeOut,
					    Button = { FadeIn = FadeIn,Color = "1 1 1 0", Command = "%COMMAND_BUY_CASE%" },
					    Text =
					    {
						    Text = "", Font = "robotocondensed-regular.ttf", FontSize = 14,
						    Align = TextAnchor.MiddleCenter, Color = "1 1 1 0"
					    },
					    RectTransform =
					    {
						    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-57.333 -15.333",
						    OffsetMax = "57.333 15.333"
					    }
				    }, "BuyButtonCase", "BuyCase");
				    
				    container.Add(new CuiElement 
				    {
					    FadeOut = FadeOut,
					    Name = "PriceSell",
					    Parent = "SellButtonCase",
					    Components = {
						    new CuiTextComponent { FadeIn = FadeIn, Text = "%PRICE_SELL%", Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleRight, Color = "1 1 1 1" },
						    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-134.479 -15.333", OffsetMax = "-50.188 15.333" }
					    }
				    });
				    
				    container.Add(new CuiElement
				    {
					    FadeOut = FadeOut,
					    Name = "PriceBuy",
					    Parent = "SellButtonCase",
					    Components = {
						    new CuiTextComponent { FadeIn = FadeIn, Text = "%PRICE_BUY%", Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
						    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "136.588 -15.333", OffsetMax = "252.879 15.333" }
					    }
				    });
			    }

			    AddInterface("UI_CASE_TEMPLATE_CASES", container.ToJson());
		    }

		    private void Building_Cases_ButtonClose()
		    {
			    CuiElementContainer container = new CuiElementContainer();

			    container.Add(new CuiElement
			    {
				    FadeOut = FadeOut,
				    Name = "BlockBtn",
				    Parent = "%PARENT%",
				    Components = {
					    new CuiRawImageComponent { FadeIn = FadeIn, Color = "1 1 1 1", Png = ImageUi.GetImage(config.InterfaceSettings.BlockPanelButton) },
					    new CuiRectTransformComponent { AnchorMin = "0.2 0.2", AnchorMax = "0.8 0.8", OffsetMin = "0 0", OffsetMax = "0 0" }
				    }
			    });

			    AddInterface("UI_CASE_TEMPLATE_CASE_BUTTON_BLOCK", container.ToJson());
		    }
		    private void Building_Cases_Opened()
		    {
			    CuiElementContainer container = new CuiElementContainer();

			    container.Add(new CuiButton
			    {
				    FadeOut = FadeOut,
				    Button = { FadeIn = FadeIn, Color = "1 1 1 0", Command = "%COMMAND_OPEN_CASE%"},
				    Text =
				    {
					    Text = "%UI_CASE_BUTTON_OPEN_CASE%", Font = "robotocondensed-regular.ttf", FontSize = 12,
					    Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.6"
				    },
				    RectTransform =
				    {
					    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-116.667 -15.333",
					    OffsetMax = "116.667 15.333"
				    }
			    }, "OpenCaseButtonPanel", "OpenCaseBTN");

			    AddInterface("UI_CASE_TEMPLATE_CASE_BUTTON_OPENED", container.ToJson());
		    }
		    
		    private void Building_Cases_Label_Amounts()
		    {
			    CuiElementContainer container = new CuiElementContainer();

			    if (_.IQEconomic || _.Economics || _.TPEconomic)
			    {
				    container.Add(new CuiElement
				    {
					    FadeOut = FadeOut,
					    Name = "AmountBalanceUser",
					    Parent = "StaticInfoCaseBalance",
					    Components =
					    {
						    new CuiTextComponent
						    {
							    Text = "%UI_PROFILE_DESCRIPTION_AMOUNT_IQECONOMIC_BALANCE%",
							    Font = "robotocondensed-regular.ttf", FontSize = 10,
							    Align = TextAnchor.UpperLeft, Color = "1 1 1 0.6",
							    FadeIn = FadeIn,
						    },
						    new CuiRectTransformComponent
						    {
							    AnchorMin = "0.48 0", AnchorMax = "1 0.60"
						    }
					    }
				    });

				    container.Add(new CuiElement
				    {
					    FadeOut = FadeOut,
					    Name = "AmountThisCase",
					    Parent = "StaticInfoCaseAmountPanel",
					    Components =
					    {
						    new CuiTextComponent
						    {
							    Text = "%UI_PROFILE_DESCRIPTION_YOUR_AMOUNT_CASE%", Font = "robotocondensed-regular.ttf", FontSize = 10,
							    Align = TextAnchor.LowerLeft, Color = "1 1 1 0.6",
							    FadeIn = FadeIn,
						    },
						    new CuiRectTransformComponent
						    {
							    AnchorMin = "0.875 0.22", AnchorMax = "1 1"
						    }
					    }
				    });
			    }
			    else
			    {
			    	container.Add(new CuiElement
				    {
					    FadeOut = FadeOut,
					    Name = "AmountThisCase",
					    Parent = "StaticInfoCaseAmountPanel",
					    Components =
					    {
						    new CuiTextComponent
						    {
							    Text = "%UI_PROFILE_DESCRIPTION_YOUR_AMOUNT_CASE%", Font = "robotocondensed-regular.ttf", FontSize = 11,
							    Align = TextAnchor.LowerLeft, Color = "1 1 1 0.6",
							    FadeIn = FadeIn,
						    },
						    new CuiRectTransformComponent
						    {
							    AnchorMin = "0.35 -0.8", AnchorMax = "1 0"
						    }
					    }
				    });
			    }

			    AddInterface("UI_CASE_TEMPLATE_CASE_LABELS_AMOUNT", container.ToJson());
		    }
		    
		    #endregion
		    
		    public void Item_Blueprint(BasePlayer player, String Parent)
		    {
			    CuiElementContainer container = new CuiElementContainer();

			    container.Add(new CuiElement
			    {
				    FadeOut = FadeOut,
				    Name = "PngBlueprint",
				    Parent = Parent,
				    Components = {
					    new CuiImageComponent { FadeIn = FadeIn, Color = "1 1 1 1", ItemId = ItemManager.FindItemDefinition("blueprintbase").itemid},
					    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31.663 -35.533", OffsetMax = "31.671 27.8" }
				    }
			    });

			    CuiHelper.AddUi(player, container);
		    }

		    #region Building Inventory

		    private void Building_Inventory_Player()
		    {
			    CuiElementContainer container = new CuiElementContainer();

			    container.Add(new CuiPanel
			    {
				    FadeOut = FadeOut,
				    CursorEnabled = true,
				    Image = { FadeIn = FadeIn, Color = "0 0 0 0.75", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
				    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" }
			    }, "Overlay", UI_CASES_MENU_INVENTORY);

			    container.Add(new CuiElement
			    {
				    FadeOut = FadeOut,
				    Name = "InventoryLabel",
				    Parent = UI_CASES_MENU_INVENTORY,
				    Components =
				    {
					    new CuiTextComponent
					    {
						    Text = "%UI_CASE_TITLE_INVENTORY%", Font = "robotocondensed-regular.ttf", FontSize = 24,
						    Align = TextAnchor.UpperLeft, Color = "1 1 1 1",
						    FadeIn = FadeIn,
					    },
					    new CuiRectTransformComponent
					    {
						    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-507.485 262.795",
						    OffsetMax = "-355.622 292.123"
					    }
				    }
			    });

			    container.Add(new CuiElement
			    {
				    FadeOut = FadeOut,
				    Name = "InventoryDescription",
				    Parent = UI_CASES_MENU_INVENTORY,
				    Components =
				    {
					    new CuiTextComponent
					    {
						    Text = "%UI_CASE_DESCRIPTION_INVENTORY%",
						    Font = "robotocondensed-regular.ttf", FontSize = 11, Align = TextAnchor.UpperLeft,
						    Color = "1 1 1 1",
						    FadeIn = FadeIn,
					    },
					    new CuiRectTransformComponent
					    {
						    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
						    OffsetMin = "-507.485 201.775", OffsetMax = "-263.447 265.942"
					    }
				    }
			    });

			    container.Add(new CuiElement
			    {
				    FadeOut = FadeOut,
				    Name = "PanelGoHome",
				    Parent = UI_CASES_MENU_INVENTORY,
				    Components =
				    {
					    new CuiRawImageComponent { FadeIn = FadeIn, Color = "1 1 1 1", Png = ImageUi.GetImage(config.InterfaceSettings.PanelCasesInventoryPanel) },
					    new CuiRectTransformComponent
					    {
						    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "387.837 256.211",
						    OffsetMax = "506.503 288.211"
					    }
				    }
			    });

			    container.Add(new CuiElement
			    {
				    FadeOut = FadeOut,
				    Name = "BackToCase",
				    Parent = "PanelGoHome",
				    Components =
				    {
					    new CuiTextComponent
					    {
						    Text = "%UI_CASE_BUTTON_BACK_CASE%", Font = "robotocondensed-regular.ttf", FontSize = 11,
						    Align = TextAnchor.UpperLeft, Color = "0.627451 0.2196078 0.8196079 1",
						    FadeIn = FadeIn,
					    },
					    new CuiRectTransformComponent
					    {
						    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-24.057 0", OffsetMax = "51.13300 13.0833232"
					    }
				    }
			    });

			    container.Add(new CuiElement
			    {
				    FadeOut = FadeOut,
				    Name = "ClicableInfo",
				    Parent = "PanelGoHome",
				    Components =
				    {
					    new CuiTextComponent
					    {
						    Text = "%UI_PROFILE_DESCRIPTION_YOUR_INVENTORY%", Font = "robotocondensed-regular.ttf", FontSize = 11,
						    Align = TextAnchor.UpperLeft, Color = "1 1 1 1",
						    FadeIn = FadeIn,
					    },
					    new CuiRectTransformComponent
					    {
						    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-24.057 -13.083", OffsetMax = "51.133 0"
					    }
				    }
			    });

			    container.Add(new CuiButton
			    {
				    FadeOut = FadeOut,
				    Button = { FadeIn = FadeIn, Color = "1 1 1 0", Command = "case.func open.case.menu"},
				    Text =
				    {
					    Text = "", Font = "robotocondensed-regular.ttf", FontSize = 14,
					    Align = TextAnchor.MiddleCenter, Color = "0 0 0 0"
				    },
				    RectTransform =
				    {
					    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-59.333 -16", OffsetMax = "59.333 16"
				    }
			    }, "PanelGoHome", "ClickButtonToCase");

			    AddInterface("UI_CASE_TEMPLATE_INVENTORY_PLAYER", container.ToJson());
		    }
		    
		    private void Building_Inventory_Player_Pages()
		    {
			    CuiElementContainer container = new CuiElementContainer();

			    container.Add(new CuiElement
			    {
				    FadeOut = FadeOut,
				    Name = "NextPageInventory",
				    Parent = UI_CASES_MENU_INVENTORY,
				    Components =
				    {
					    new CuiRawImageComponent { FadeIn = FadeIn, Color = "1 1 1 1", Png = ImageUi.GetImage(config.InterfaceSettings.NextPage) },
					    new CuiRectTransformComponent
					    {
						    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "466.507 -308.333",
						    OffsetMax = "506.5073 -268.333" 
					    }
				    }
			    }); 

			    container.Add(new CuiButton
			    {
				    FadeOut = FadeOut,
				    Button = { FadeIn = FadeIn, Color = "0 0 0 0", Command = "%NEXT_PAGE_INVENTORY%" },
				    Text =
				    {
					    Text = "", Font = "robotocondensed-regular.ttf", FontSize = 14,
					    Align = TextAnchor.MiddleCenter, Color = "0 0 0 0"
				    },
				    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" }
			    }, "NextPageInventory", "Button_538");

			    container.Add(new CuiElement
			    {
				    FadeOut = FadeOut,
				    Name = "BackPageInventory",
				    Parent = UI_CASES_MENU_INVENTORY,
				    Components =
				    {
					    new CuiRawImageComponent { FadeIn = FadeIn, Color = "1 1 1 1", Png = ImageUi.GetImage(config.InterfaceSettings.BackPage) },
					    new CuiRectTransformComponent
					    {
						    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "412.867 -308.333",
						    OffsetMax = "452.8627 -268.33322"
					    }
				    }
			    });

			    container.Add(new CuiButton
			    {
				    FadeOut = FadeOut,
				    Button = { FadeIn = FadeIn, Color = "0 0 0 0", Command = "%BACK_PAGE_INVENTORY%"},
				    Text =
				    {
					    Text = "Rust UI Button", Font = "robotocondensed-regular.ttf", FontSize = 14,
					    Align = TextAnchor.MiddleCenter, Color = "0 0 0 0"
				    },
				    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" }
			    }, "BackPageInventory", "Button_538");

			    AddInterface("UI_CASE_TEMPLATE_INVENTORY_PLAYER_PAGE", container.ToJson());

		    }

		    #endregion

		    #region Building Inventory Item

		    private void Building_Inventory_Items_Panel()
		    {
			    CuiElementContainer container = new CuiElementContainer();

			    container.Add(new CuiPanel
			    {
				    FadeOut = FadeOut,
				    CursorEnabled = false,
				    Image = { FadeIn = FadeIn, Color = "1 1 1 0" },
				    RectTransform =
				    {
					    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-509.067 -257.724",
					    OffsetMax = "509.119 181.067"
				    }
			    }, UI_CASES_MENU_INVENTORY, "InventoryItemPanel");
			    
			    AddInterface("UI_CASE_TEMPLATE_INVENTORY_ITEM_PANEL", container.ToJson());
		    }

		    private void Building_Inventory_Item()
		    {
			    CuiElementContainer container = new CuiElementContainer();

			    container.Add(new CuiElement
			    {
				    FadeOut = FadeOut,
				    Name = "%PARENT_ITEM_INVENTOY%",
				    Parent = "InventoryItemPanel",
				    Components =
				    {
					    new CuiRawImageComponent { FadeIn = FadeIn, Color = "1 1 1 1", Png = "%BACKGROUND_RARE%" },
					    new CuiRectTransformComponent
					    {
						    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "%OFFSET_MIN%",
						    OffsetMax = "%OFFSET_MAX%"
					    }
				    }
			    });

			    container.Add(new CuiElement
			    {
				    FadeOut = FadeOut,
				    Name = "NameItem",
				    Parent = "%PARENT_ITEM_INVENTOY%",
				    Components =
				    {
					    new CuiTextComponent
					    {
						    Text = "%TITLE_ITEM%", Font = "robotocondensed-regular.ttf", FontSize = 10,
						    Align = TextAnchor.UpperLeft, Color = "1 1 1 1",
						    FadeIn = FadeIn,
					    },
					    new CuiRectTransformComponent
					    {
						    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-41.923 32.151",
						    OffsetMax = "17.938 44.528"
					    }
				    }
			    });

			    container.Add(new CuiElement
			    {
				    FadeOut = FadeOut,
				    Name = "DescriptionItem",
				    Parent = "%PARENT_ITEM_INVENTOY%",
				    Components =
				    {
					    new CuiTextComponent
					    {
						    Text = "%ITEM_AMOUNT%", Font = "robotocondensed-regular.ttf", FontSize = 8,
						    Align = TextAnchor.UpperLeft, Color = "0.3568628 0.3490196 0.3490196 1",
						    FadeIn = FadeIn, 
					    },
					    new CuiRectTransformComponent
					    {
						    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-41.923 22.014",
						    OffsetMax = "17.938 32.151"
					    }
				    }
			    });

			    AddInterface("UI_CASE_TEMPLATE_INVENTORY_ITEM", container.ToJson());
		    }

		    #endregion
	    }

	    #endregion
	    
        #region Utilites

        #region FormatTime

        public String FormatTime(TimeSpan time, String UserID)
        {
	        String Result = String.Empty;

	        String Days = GetLang("UI_ITEM_TIME_DAYS", UserID);
	        String Hourse = GetLang("UI_ITEM_TIME_HOURSE", UserID);
	        String Minutes = GetLang("UI_ITEM_TIME_MINUTES", UserID);
	        String Seconds = GetLang("UI_ITEM_TIME_SECONDS", UserID);

	        if (time.Seconds != 0)
		        Result = $"{Format(time.Seconds, Seconds, Seconds, Seconds)}";

	        if (time.Minutes != 0)
		        Result = $"{Format(time.Minutes, Minutes, Minutes, Minutes)}";

	        if (time.Hours != 0)
		        Result = $"{Format(time.Hours, Hourse, Hourse, Hourse)}";

	        if (time.Days != 0)
		        Result = $"{Format(time.Days, Days, Days, Days)}";

	        return Result;
        }

        private String Format(Int32 units, String form1, String form2, String form3)
        {
            var tmp = units % 10;

            if (units >= 5 && units <= 20 || tmp >= 5 && tmp <= 9)
                return $"{units}{form1}";

            if (tmp >= 2 && tmp <= 4)
                return $"{units}{form2}";

            return $"{units}{form3}";
        }

        #endregion

        #region Effects

        private void RunEffect(BasePlayer player, String Path)
        {
	        if (!config.InterfaceSettings.UseEffects) return;
	        Effect effect = new Effect(Path, player, 0, new Vector3(), new Vector3());
	        EffectNetwork.Send(effect, player.Connection);
        }

        #endregion

        private void Log(String LoggedMessage)
        {
	        if (!config.UseLogs) return;
	        LogToFile("TPCasesLog", LoggedMessage, this);
        }
        
        private class ImageUi
        {
            private static Coroutine coroutineImg = null;
			private static Dictionary<String, String> Images = new Dictionary<String, String>();

			private static List<String> KeyImages = new List<String>();
			public static void DownloadImages() { coroutineImg = ServerMgr.Instance.StartCoroutine(AddImage()); }

            private static IEnumerator AddImage()
            {
	            if (_ == null)
					yield break;
				_.PrintWarning(LanguageEn ? "We generate the interface, wait ~10-15 seconds!" : "Генерируем интерфейс, ожидайте ~10-15 секунд!");
				foreach (String URL in KeyImages)
				{
					String KeyName = URL;
					if (KeyName == null) throw new ArgumentNullException(nameof(KeyName));

					UnityWebRequest www = UnityWebRequestTexture.GetTexture(URL);
					yield return www.SendWebRequest();

					if (www.isNetworkError || www.isHttpError)
					{
						_.PrintWarning($"Image download error! Error: {www.error}, Image name: {KeyName}");
						www.Dispose();
						coroutineImg = null;
						yield break;
					}

					Texture2D texture = DownloadHandlerTexture.GetContent(www);
					if (texture != null)
					{
						Byte[] bytes = texture.EncodeToPNG();

						String image = FileStorage.server.Store(bytes, FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID).ToString();
						if (!Images.ContainsKey(KeyName))
							Images.Add(KeyName, image);
						else
							Images[KeyName] = image;

						UnityEngine.Object.DestroyImmediate(texture);
					}

					www.Dispose();
					yield return CoroutineEx.waitForSeconds(0.02f);
				}

				yield return CoroutineEx.waitForSeconds(0.02f);
                coroutineImg = null;

                _interface = new InterfaceBuilder();
                _.PrintWarning(LanguageEn ? "Interface loaded successfully!" : "Интерфейс успешно загружен!");
                _.FullInit = true;
            }

            public static String GetImage(String ImgKey) => Images.ContainsKey(ImgKey) ? Images[ImgKey] : _.GetImage("LOADING");

            public static String GetRareImage(Int32 Rare)
            {
	            String Image = String.Empty;
	            Configuration.Interface @interface = config.InterfaceSettings;
	            
	            foreach (KeyValuePair<Int32,String> RareImages in @interface.RareBackground)
	            {
		            if (Rare < RareImages.Key) continue;
		            Image = RareImages.Value;
		            break;
	            }

	            if (String.IsNullOrWhiteSpace(Image))
		            Image = @interface.RareBackground.OrderBy(x => x.Key).First().Value;
	            
	            return Image;
            }
            public static void Initialize()
			{
				KeyImages = new List<String>();
				Images = new Dictionary<String, String>();

				Configuration.Interface @interface = config.InterfaceSettings;

				#region Tumbler PNG

				if (!KeyImages.Contains(@interface.TumblerLeft))
					KeyImages.Add(@interface.TumblerLeft);

				if (!KeyImages.Contains(@interface.TumblerRight))
					KeyImages.Add(@interface.TumblerRight);

				#endregion

				#region Profile PNG
				
				if (!KeyImages.Contains(@interface.PanelCasesInfoAmount))
					KeyImages.Add(@interface.PanelCasesInfoAmount);
				
				if (!KeyImages.Contains(@interface.PanelCasesInventoryPanel))
					KeyImages.Add(@interface.PanelCasesInventoryPanel);
				
				if (!KeyImages.Contains(@interface.PanelCasesBalancePanel))
					KeyImages.Add(@interface.PanelCasesBalancePanel);
				
				#endregion
				
				if (!KeyImages.Contains(@interface.EffectCases))
					KeyImages.Add(@interface.EffectCases);

				#region Case Images

				foreach (KeyValuePair<String, Configuration.CasesPreset> preset in config.CasesPresets)
				{
					if(!KeyImages.Contains(preset.Value.PNGCase))
						KeyImages.Add(preset.Value.PNGCase);

					foreach (Configuration.CasesPreset.RewardPreset rewardPreset in preset.Value.RewardPresets)
					{
						if(!String.IsNullOrWhiteSpace(rewardPreset.PNGReward) && !KeyImages.Contains(rewardPreset.PNGReward))
							KeyImages.Add(rewardPreset.PNGReward);
					}
				}

				#endregion

				#region Pages

				if (!KeyImages.Contains(@interface.BackPage))
					KeyImages.Add(@interface.BackPage);
				
				if (!KeyImages.Contains(@interface.NextPage))
					KeyImages.Add(@interface.NextPage);

				#endregion

				#region Buttons 

				if (!KeyImages.Contains(@interface.OpenCaseButton))
					KeyImages.Add(@interface.OpenCaseButton);
				
				if (!KeyImages.Contains(@interface.BuyCaseButton))
					KeyImages.Add(@interface.BuyCaseButton);
				
				if (!KeyImages.Contains(@interface.SellCaseButton))
					KeyImages.Add(@interface.SellCaseButton);
				
				if (!KeyImages.Contains(@interface.BlockPanelButton))
					KeyImages.Add(@interface.BlockPanelButton);

				#endregion
				
				#region CaseItems Rare

				if (!KeyImages.Contains(@interface.RareMiniIcon))
					KeyImages.Add(@interface.RareMiniIcon);
				
				if (!KeyImages.Contains(@interface.NoRareBackground))
					KeyImages.Add(@interface.NoRareBackground);

				foreach (String PNGRares in @interface.RareBackground.Values)
				{
					if (!KeyImages.Contains(PNGRares))
						KeyImages.Add(PNGRares);
				}
				#endregion
			}
            public static void Unload()
            {
	            coroutineImg = null;
                foreach (KeyValuePair<String, String> item in Images)
                    FileStorage.server.RemoveExact(UInt32.Parse(item.Value), FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID, 0U);

				KeyImages.Clear();
				KeyImages = null;
				Images.Clear();
				Images = null;
			}
        }

        #endregion
        
        #region Lang

        private static StringBuilder sb = new StringBuilder();
        public string GetLang(string LangKey, string userID = null, params object[] args)
        {
	        sb.Clear();
	        if (args != null)
	        {
		        sb.AppendFormat(lang.GetMessage(LangKey, this, userID), args);
		        return sb.ToString();
	        }
	        return lang.GetMessage(LangKey, this, userID);
        }
        private new void LoadDefaultMessages()
        {
	        lang.RegisterMessages(new Dictionary<string, string>
	        {
		        ["UI_TITLE_CASE_INFO_ITEMS"] = "<b>Possible items and their chances in this case</b>",
		        ["UI_TUBLER_INVENTORY_PLUGIN"] = "To plugin inventory",
		        ["UI_TUBLER_INVENTORY_PLAYER"] = "Your inventory",
		        ["UI_PROFILE_TITLE_YOUR_AMOUNT_CASE"] = "Quantity cases",
		        ["UI_PROFILE_DESCRIPTION_YOUR_AMOUNT_CASE"] = "{0} piece",
		        ["UI_PROFILE_TITLE_YOUR_INVENTORY"] = "Your inventory",
		        ["UI_PROFILE_DESCRIPTION_YOUR_INVENTORY"] = "clickable",
		        ["UI_PROFILE_TITLE_IQECONOMIC_BALANCE"] = "Balance:",
		        ["UI_PROFILE_DESCRIPTION_AMOUNT_IQECONOMIC_BALANCE"] = "{0}",
		        ["UI_ITEM_LIST_ITEM_NAME"] = "<b>Item</b>",
		        ["UI_ITEM_LIST_COMMAND_NAME"] = "<b>Reward</b>",
		        ["UI_CASE_BUTTON_OPEN_CASE"] = "<b>OPEN CASE</b>",
		        ["UI_CASE_BUTTON_NO_OPEN_AMOUNT_CASE"] = "<b><size=17>NOT ENOUGH CASES</size></b>",
		        ["UI_CASE_BUTTON_SELL_BUTTON"] = "<b>SELL</b>",
		        ["UI_CASE_BUTTON_BUY_BUTTON"] = "<b>BUY</b>",
		        ["UI_CASE_TITLE_INVENTORY"] = "<b>INVENTORY</b>",
		        ["UI_CASE_DESCRIPTION_INVENTORY"] = "This is where all your belongings knocked out of cases go.\nYou can pick them up at any time!",
		        ["UI_CASE_BUTTON_BACK_CASE"] = "Back",
		        ["UI_CASE_INITIALIZE"] = "Cases are currently being initialized, possibly later",
		        ["UI_CASE_IQRANK_BLOCKED"] = "Rank required {0}",
		        ["UI_CASE_PERMISSION_BLOCKED"] = "Not permissions",
		        ["CASE_MESSAGE_FIND_CASE"] = "You have successfully found a case, check your cases!",
		        ["CASE_MESSAGE_NO_DUEL"] = "You cannot use cases during a duel",
		        ["CASE_MESSAGE_FULL_INVENTORY"] = "Your inventory is full!\nItem has been dropped on the ground!",
		        ["UI_ITEM_TIME_DAYS"] = "D",
		        ["UI_ITEM_TIME_HOURSE"] = "H",
		        ["UI_ITEM_TIME_MINUTES"] = "M",
		        ["UI_ITEM_TIME_SECONDS"] = "S",

	        }, this);
	        lang.RegisterMessages(new Dictionary<string, string>
	        {
		        ["UI_TITLE_CASE_INFO_ITEMS"] = "<b>Возможные предметы и их шансы в этом кейсе</b>",
		        ["UI_TUBLER_INVENTORY_PLUGIN"] = "В инвентарь плагина",
		        ["UI_TUBLER_INVENTORY_PLAYER"] = "В ваш инвентарь",
		        ["UI_PROFILE_TITLE_YOUR_AMOUNT_CASE"] = "Количество кейсов",
		        ["UI_PROFILE_DESCRIPTION_YOUR_AMOUNT_CASE"] = "{0} штук",
		        ["UI_PROFILE_TITLE_YOUR_INVENTORY"] = "Ваш инвентарь",
		        ["UI_PROFILE_DESCRIPTION_YOUR_INVENTORY"] = "кликабельно",
		        ["UI_PROFILE_TITLE_IQECONOMIC_BALANCE"] = "Баланс:",
		        ["UI_PROFILE_DESCRIPTION_AMOUNT_IQECONOMIC_BALANCE"] = "{0}",
		        ["UI_ITEM_LIST_ITEM_NAME"] = "<b>Предмет</b>",
		        ["UI_ITEM_LIST_COMMAND_NAME"] = "<b>Награда</b>",
		        ["UI_CASE_BUTTON_OPEN_CASE"] = "<b>ОТКРЫТЬ КЕЙС</b>",
		        ["UI_CASE_BUTTON_NO_OPEN_AMOUNT_CASE"] = "<b><size=12>НЕДОСТАТОЧНО КЕЙСОВ</size></b>",
		        ["UI_CASE_BUTTON_SELL_BUTTON"] = "<b>ПРОДАТЬ</b>",
		        ["UI_CASE_BUTTON_BUY_BUTTON"] = "<b>КУПИТЬ</b>",
		        ["UI_CASE_TITLE_INVENTORY"] = "<b>ИНВЕНТАРЬ</b>",
		        ["UI_CASE_DESCRIPTION_INVENTORY"] = "Сюда попадают все ваши вещи выбитые с кейсов.\nВы можете забрать их в любой момент!",
		        ["UI_CASE_BUTTON_BACK_CASE"] = "Вернуться",
		        ["UI_CASE_INITIALIZE"] = "Кейсы в данный момент инициализируются, попробуйте позже",
		        ["UI_CASE_IQRANK_BLOCKED"] = "Требуется ранг {0}",
		        ["UI_CASE_PERMISSION_BLOCKED"] = "Недостаточно прав",
		        ["CASE_MESSAGE_FIND_CASE"] = "Вы успешно нашли кейс, проверьте свои кейсы!",
		        ["CASE_MESSAGE_NO_DUEL"] = "Вы не можете ипользовать кейсы во время дуэли",
		        ["CASE_MESSAGE_FULL_INVENTORY"] = "Ваш инвентарь переполнен!\nПредмет выброшен на землю!",
		        ["UI_ITEM_TIME_DAYS"] = "Д",
		        ["UI_ITEM_TIME_HOURSE"] = "Ч",
		        ["UI_ITEM_TIME_MINUTES"] = "М",
		        ["UI_ITEM_TIME_SECONDS"] = "С",
	        }, this, "ru");
        }
        #endregion

        #region API

        void API_GIVE_CASE(UInt64 userID, String CaseKey, Int32 Amount) => GiveCases(userID, CaseKey, Amount);
        void API_REMOVE_CASE(UInt64 userID, String CaseKey, Int32 Amount) => RemoveCases(userID, CaseKey, Amount);
        Boolean API_IS_CASE_EXIST(String CaseKey) => config.CasesPresets.ContainsKey(CaseKey);

        Int32 API_GET_AMOUNT_CASE(BasePlayer player, String CaseKey)
        {
	        if (!DataPlayer.ContainsKey(player.userID)) return 0;
	        return DataPlayer[player.userID].GetAmountCase(CaseKey);
        }

        Boolean API_IS_CASE_PLAYER(BasePlayer player, String CaseKey) => API_GET_AMOUNT_CASE(player, CaseKey) != 0;


        #endregion
    }
}