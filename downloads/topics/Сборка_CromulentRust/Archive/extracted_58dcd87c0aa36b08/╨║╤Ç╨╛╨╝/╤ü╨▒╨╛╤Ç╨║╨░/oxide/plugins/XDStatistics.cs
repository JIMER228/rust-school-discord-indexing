using Oxide.Core.Plugins;
using System.Collections;
using UnityEngine.Networking;
using Newtonsoft.Json.Linq;
using System.Linq;
using Oxide.Core;
using System.Globalization;
using UnityEngine;
using System;
using Rust;
using System.Text;
using System.Collections.Generic;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Libraries;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Oxide.Plugins.XDStatisticsExtensionMethods;

namespace System.Runtime.CompilerServices
{
    public class IsExternalInit { }
}      
namespace Oxide.Plugins.XDStatisticsExtensionMethods
{
    public static class ExtensionMethods
    {
        public static List<TSource> XDToList<TSource>(this IEnumerable<TSource> source)
        {
            List<TSource> result = new();
            using IEnumerator<TSource> enumerator = source.GetEnumerator();
            while (enumerator.MoveNext()) result.Add(enumerator.Current);
            return result;
        }
		
        public static TSource XDLast<TSource>(this IList<TSource> source) => source[^1];


        public static HashSet<TSource> XDWhere<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            HashSet<TSource> result = new();
            using IEnumerator<TSource> enumerator = source.GetEnumerator();
            while (enumerator.MoveNext())
                if (predicate(enumerator.Current))
                    result.Add(enumerator.Current);
            return result;
        }
    }
}
		   		 		  						  	   		  	 	 		  	  			  			 		  				
/// - Исправлена проблема с засчетом ракет
/// - Исправлена проблема с NRE при сбитии вертолета
/// - Исправлена NRE при Unsubscribe
/// - Небольшой рефакторинг и улучшения кода
/// - Это лишь некоторые исправления и улучшения перед следующим важным и большим обновлением.
/// - Если вы нашли какие то проблемы не стесняйтесь говорить о них.

namespace Oxide.Plugins
{
    [Info("XDStatistics", "DezLife", "2.8.2")]
    [Description("Multifunctional statistics for your server!")]
    
    class XDStatistics : RustPlugin
    {
        
                        private record ItemName(string ShortName, string ENDisplayName, string RUDisplayName);

        
        
        private void STCanReceiveYield(BasePlayer player, GrowableEntity entity, Item item)
        {
            if (player == null || item == null || item.info == null) return;

            if (_alowedSeedId.Contains(item.info.itemid))
            {
                NextTick(() =>
                {
                    PlayerInfo playerStat = PlayerInfo.Find(player.userID.Get());

                    if (!playerStat.harvesting.HarvestingList.TryGetValue(item.info.shortname, out int currentAmount))
                    {
                        playerStat.harvesting.HarvestingList[item.info.shortname] = item.amount;
                    }
                    else
                    {
                        playerStat.harvesting.HarvestingList[item.info.shortname] = currentAmount + item.amount;
                    }

                    playerStat.harvesting.AllHarvesting += item.amount;
                    playerStat.Score += _config.settingsScore.PlantScore;
                });
            }
        }


        
                private void OnItemCraftFinished(ItemCraftTask task, Item item, ItemCrafter crafter)
        {
            if(_config.settingsScore.blackListedCraft.Contains(item.info.shortname))
                return;
            PlayerInfo playerInfo = PlayerInfo.Find(crafter.owner.userID.Get());
            playerInfo.otherStat.AllCraft += item.amount;
            playerInfo.Score += _config.settingsScore.craftScore;
        }
        
        private void ExplosionProgressAdd(BasePlayer player, BaseEntity entity, string shortname = "")
        {
            string weaponName = string.IsNullOrWhiteSpace(shortname) ? string.Empty : shortname;

            if (entity != null)
            {
                if (!_prefabID2Item.TryGetValue(entity.prefabID, out weaponName))
                {
                    _prefabNameItem.TryGetValue(entity.ShortPrefabName, out weaponName);
                }
            } 

            if (!string.IsNullOrEmpty(weaponName))
            {
                PlayerInfo playerstat = PlayerInfo.Find(player.userID.Get());
        
                if (playerstat.explosion.ExplosionUsed.ContainsKey(weaponName) && !_config.settingsScore.blackListed.Contains(weaponName))
                {
                    playerstat.explosion.ExplosionUsed[weaponName]++;
                    playerstat.explosion.AllExplosionUsed++;
                    playerstat.Score += _config.settingsScore.ExplosionScore[weaponName];
                }
            }
        }
        
        private void OnEntityDeath(BradleyAPC entity, HitInfo info)
        {
            if (entity == null || info is null) return;
            
            BasePlayer player = info.InitiatorPlayer;

            if (player != null && player.userID.IsSteamId() && !player.IsNpc && entity.ToPlayer() != player)
            {
                PlayerInfo playerstat = PlayerInfo.Find(player.userID.Get());
                playerstat.pVP.BradleyKill++;
                playerstat.Score += _config.settingsScore.BradleyScore;
            }
        }
        
        private void OnEntityDeath(BaseAnimalNPC entity, HitInfo info)
        {
            if (entity == null || info is null) return;
            
            BasePlayer player = info.InitiatorPlayer;

            if (player != null && player.userID.IsSteamId() && !player.IsNpc && entity.ToPlayer() != player)
            {
                PlayerInfo playerstat = PlayerInfo.Find(player.userID.Get());
                playerstat.otherStat.AnimalsKill++;
                playerstat.Score += _config.settingsScore.AnimalScore;
            }
        }

        private void WeaponProgressAdd(PlayerInfo player, HitInfo hitinfo, bool kill = false)
        {
            if (hitinfo == null || hitinfo.WeaponPrefab == null) return;

            string weaponName = null;

            if (hitinfo.Weapon != null && hitinfo.Weapon.GetItem() != null && hitinfo.Weapon.GetItem().info != null)
            {
                weaponName = hitinfo.Weapon.GetItem().info.shortname;
            }

            if (weaponName is null 
                && !_prefabID2Item.TryGetValue(hitinfo.WeaponPrefab.prefabID, out weaponName) 
                && !_prefabNameItem.TryGetValue(hitinfo.WeaponPrefab.ShortPrefabName, out weaponName))
            {
                return;
            }

            if (string.IsNullOrEmpty(weaponName)) return;

            weaponName = weaponName switch 
            {
                "rifle.ak.ice" or "rifle.ak.diver" => "rifle.ak",
                _ => weaponName
            };

            if (!player.weapon.WeaponUsed.TryGetValue(weaponName, out PlayerInfo.Weapon.WeaponInfo weapon))
            {
                player.weapon.WeaponUsed[weaponName] = new PlayerInfo.Weapon.WeaponInfo
                {
                    Kills = kill ? 1 : 0,
                    Headshots = hitinfo.isHeadshot ? 1 : 0,
                    Shots = 1
                };
                return;
            }

            if (hitinfo.isHeadshot) weapon.Headshots++;
            weapon.Shots++;
            if (kill) weapon.Kills++;
        }
        private bool HasImage(string imageName, ulong imageId = 0) => (bool)ImageLibrary.Call("HasImage", imageName, imageId);

        
        
        private void OnZLevelDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item, int prevAmount, int newAmount, bool isPowerTool)
        {
            if (player == null || item == null || item.info == null) return;

            ProgressAdd(player, item.info.shortname, item.amount);
        }
        
        private void OnEntityDeath(PatrolHelicopter entity, HitInfo info)
        {
            if (entity == null || info is null) return;
            
            BasePlayer player = info.InitiatorPlayer != null ? info.InitiatorPlayer : (entity.myAI._targetList is { Count: > 0 } ? entity.myAI._targetList.XDLast().ply : null);

            if (player != null && !player.IsNpc && entity.ToPlayer() != player)
            {
                PlayerInfo playerstat = PlayerInfo.Find(player.userID.Get());
                playerstat.pVP.HeliKill++;
                playerstat.Score += _config.settingsScore.HeliScore;
            }
        }
        public static StringBuilder sb;

        private class Configuration
        {

            public class Settings
            {
                [JsonProperty(RU ? "Чат-команды для открытия статистики" : "Chat commands for opening statistics", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public List<string> chatCommandOpenStat = new List<string> { "stat", "top" };
                [JsonProperty(RU ? "Консольная команда для открытия статистики" : "Console command to open statistics")]
                public string consoleCommandOpenStat = "stat";
                [JsonProperty(RU ? "Отправлять в чат сообщения с топ-5 игроками по разным категориям" : "Send chat messages with top 5 players in various categories")]
                public bool chatSendTop = true;
                [JsonProperty(RU ? "Как часто (в секундах) отправлять сообщение?" : "How often (in seconds) should the message be sent?")]
                public int chatSendTopTime = 600;
                [JsonProperty(RU ? "Включить возможность сбросить свою статистику? (требуется XDStatistics.reset)" : "Enable the ability to reset your stats? (requires XDStatistics.reset)")]
                public bool dropStatUse = false;
                [JsonProperty(RU
                    ? "Включить возможность скрыть свою статистику от других пользователей? (требуется XDStatistics.availability)"
                    : "Enable the ability to hide your statistics from other users? (requires XDStatistics.availability)")]
                public bool availabilityUse = true;
                [JsonProperty(RU ? "Очищать данные при вайпе" : "Clear data when wiped")]
                public bool wipeData = true;
                [JsonProperty(RU ? "Как часто (в минутах) будут сохраняться данные?" : "How often (in minutes) will data be saved?")]
                public int dataSaveTime = 30;
                [JsonProperty(RU ? "Учитывать убийства NPC для выбора любимого оружия?" : "Consider NPC kills for determining a favorite weapon?")]
                public bool npsDeathUse = false;
                [JsonProperty(RU ? "У вас сервер в режиме PVE?" : "Is your server in PVE mode?")]
                public bool pveServerMode = false;
                [JsonProperty(RU ? "Список игроков (SteamID), которые не будут включены в статистику" : "List of players (SteamID) who will not be included in the statistics",
                    ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public List<ulong> ignoreList = new List<ulong>();
                [JsonProperty(RU ? "Используйте хуки коллекции из плагина SkillTree" : "Use the gathering hooks from SkillTree")]
                public bool UseSkillTreeHooks;
                [JsonProperty(RU ? "Используйте хуки коллекции из плагина ZLevels Remastered" : "Use the gathering hooks from ZLevels Remastered")]
                public bool UseZLevelsRemasteredHooks;
            }
            [JsonProperty(RU ? "Настройки Discord" : "Discord Settings")]
            public DiscordMessage discordMessage = new DiscordMessage();
            [JsonProperty(RU ? "Настройка начисления очков" : "Points Allocation Settings")]
            public SettingsScore settingsScore = new SettingsScore();
            public class SettingsInterface
            {
                [JsonProperty(RU ? "Цвет плашки заднего фона в топ 10 за 3 место" : "Background color in the top 10 for 3rd place")]
                public string ColorTop3 = "0.80392 0.498033900 0.1960784 0.49";
                [JsonProperty(RU ? "Цвет плашки заднего фона в топ 10 за 2 место" : "Background color in the top 10 for 2nd place")]
                public string ColorTop2 = "0.7529412 0.7529412 0.7529412 0.49";
                [JsonProperty(RU ? "Использовать свой задний фон? (указанный снизу)" : "Use your own background? (indicated at the bottom)")]
                public bool UsebackgroundImageUrl = false;
                [JsonProperty(RU ? "Цвет плашки заднего фона в топ 10 за 1 место" : "Background color in the top 10 for 1st place")]
                public string ColorTop1 = "1 0.8431373 0 0.49";
                [JsonProperty(RU ? "Ссылка на свой задний фон (Если нужно)" : "Link to your background (If necessary)")]
                public string backgroundImageUrl = "";
            }

            [JsonProperty(RU ? "Основные настройки плагина" : "Basic Plugin Settings")]
            public Settings settings = new Settings();
		   		 		  						  	   		  	 	 		  	  			  			 		  				
            public class SettingsPrize
            {
                [JsonProperty(RU ? "Использовать выдачу награды после вайпа?" : "Use award distribution after the wipe?")]
                public bool prizeUse = false;
                [JsonProperty(RU ? "Награда в категории SCORE" : "Award in the SCORE category")]
                public List<Prize> prizeScore = new List<Prize>();
                [JsonProperty(RU ? "Награда в категории Киллер" : "Award in the Killer category")]
                public List<Prize> prizeKiller = new List<Prize>();
                [JsonProperty(RU ? "Награда в категории Фармила" : "Award in the Gathering category")]
                public List<Prize> prizeFarm = new List<Prize>();
                [JsonProperty(RU ? "Награда в категории рейдер" : "Award in the Raider category")]
                public List<Prize> prizeRaid = new List<Prize>();
                [JsonProperty(RU ? "Награда в категории Большой онлайн" : "Award in the Big Online category")]
                public List<Prize> prizeTime = new List<Prize>();
                [JsonProperty(RU ? "Награда в категории Убийца НПС" : "Award in the NPC Killer category")]
                public List<Prize> prizeNPCKiller = new List<Prize>();
                [JsonProperty(RU ? "Награда в категории убийца животных" : "Award in the Animal Killer category")]
                public List<Prize> prizeAnimalKiller = new List<Prize>();
                [JsonProperty(RU ? "[GameStores] ID магазина" : "[GameStores] Shop ID")]
                public string ShopID = "";
                [JsonProperty(RU ? "[GameStores] ID сервера" : "[GameStores] Server ID")]
                public string ServerID = "";
                [JsonProperty(RU ? "[GameStores] Секретный ключ" : "[GameStores] Secret Key")]
                public string SecretKey = "";

                internal class Prize
                {
                    [JsonProperty(RU ? "Использовать команды как награду?" : "Use commands as a prize?")]
                    public bool commandPrizeUse = true;
                    [JsonProperty(RU ? "За какое место вручать награду? (от 1 до 3). Если данная категория наград не требуется, удалите все связанные награды." : "Which ranking to award? (from 1 to 3). If you don't need to award this category, remove all related rewards.")]
                    public int top = 1;
                    [JsonProperty(RU ? "Использовать магазин GameStore для выдачи награды?" : "Use GameStore for prize distribution?")]
                    public bool gamestorePrizeUse = false;
                    [JsonProperty(RU ? "Использовать магазин MoscowOVH для выдачи награды?" : "Use MoscowOVH store for prize distribution?")]
                    public bool ovhPrizeUse = false;
                    [JsonProperty(RU ? "Использовать [IQEconomic, Economics или ServerRewards] для выдачи награды" : "Use [IQEconomic, Economics, or ServerRewards] to distribute rewards?")]
                    public bool economicPrizeUse = false;
                    [JsonProperty(RU ? "Команды для приза" : "Command for the prize")]
                    public List<string> commandPrizeList = new List<string>();
                    [JsonProperty(RU ? "[GameStores] Сообщение для истории покупок в магазине" : "[GameStores] Store purchase history message")]
                    public string balancePlusMess = "За топ 1!!!";
                    [JsonProperty(RU ? "[GameStores или MoscowOVH] Количество начисляемых денег на баланс" : "[GameStores or MoscowOVH] Amount of money to be credited")]
                    public int balancePlus = 30;
                    [JsonProperty(RU ? "[IQEconomic, Economics или ServerRewards] Количество начисляемых денег на баланс" : "[IQEconomic, Economics, or ServerRewards] Amount of money to credit to the balance")]
                    public int balanceEconomicsPlus = 100;
		   		 		  						  	   		  	 	 		  	  			  			 		  				

                    public void GiftPrizePlayer(string player)
                    {
                        if (commandPrizeUse)
                        {
                            foreach (string cmd in commandPrizeList)
                                Instance.Server.Command(cmd.Replace("%STEAMID%", player));
                        }
                        if (gamestorePrizeUse && Instance?.GameStoresRUST)
                        {

                            string uri = $"https://gamestores.ru/api?shop_id={Instance._config.settingsPrize.ShopID}&secret={Instance._config.settingsPrize.SecretKey}&server={Instance._config.settingsPrize.ServerID}&action=moneys&type=plus&steam_id={player}&amount={balancePlus}&mess={balancePlusMess}";
                            Instance.webrequest.Enqueue(uri
                           ,
                           "", (code, response) =>
                           {
                               switch (code)
                               {
                                   case 0:
                                       {
                                           Instance.PrintError("Api does not responded to a request");
                                           break;
                                       }
                                   case 200:
                                       {
                                           break;
                                       }
                                   case 404:
                                       {
                                           Instance.PrintError($"Please check your configuration! {code}");
                                           break;
                                       }
                               }
                           }, Instance);
                        }
                        if (ovhPrizeUse)
                        {
                            if (Instance?.RustStore)
                            {
                                Instance.RustStore.CallHook("APIChangeUserBalance", ulong.Parse(player), balancePlus, new Action<string>(result =>
                                {
                                    if (result == "SUCCESS")
                                        return;
                                    Interface.Oxide.LogDebug($"Баланс игрока {ulong.Parse(player)} не был изменен, ошибка: {result}");
                                }));
                            }
                        }
                        if (economicPrizeUse)
                        {
                            if (Instance?.Economics)
                            {
                                Instance.Economics.Call("Deposit", ulong.Parse(player), (double)balanceEconomicsPlus);
                            }
                            else if (Instance?.IQEconomic)
                            {
                                Instance.IQEconomic.Call("API_SET_BALANCE", ulong.Parse(player), balanceEconomicsPlus);
                            }
                            else if (Instance?.ServerRewards)
                            {
                                Instance.ServerRewards.Call("AddPoints", ulong.Parse(player), balanceEconomicsPlus);
                            }
                        }
                    }

                }
            }
            [JsonProperty(RU ? "Настройки наград для лидеров по каждой категории" : "Top Player Rewards Settings by Category")]
            public SettingsPrize settingsPrize = new SettingsPrize();
            public class DiscordMessage
            {
                [JsonProperty(RU ? "Отправлять в Discord топ-5 лучших игроков по разным категориям?" : "Send top 5 best players in various categories to Discord?")]
                public bool discordTopFiveUse = false;
                [JsonProperty(RU ? "Как часто (в секундах) отправлять сообщение?" : "How often (in seconds) should the message be sent?")]
                public int discordSendTopTime = 600;
                [JsonProperty(RU ? "WebHook Discord" : "Discord WebHook")]
                public string weebHook = string.Empty;
                [JsonProperty(RU ? "Цвет линии в сообщении (или несколько цветов)" : "The color(s) of the line in the message", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public int[] colorLines = new int[] { 53380, 9359868, 11253955 };
                [JsonProperty(RU ? "Дополнительный текст к сообщению" : "Additional text for the message")]
                public string message = string.Empty;
            }
            [JsonProperty(RU ? "Настройки интерфейса" : "Interface Settings")]
            public SettingsInterface settingsInterface = new SettingsInterface();

            public class SettingsScore
            {
                [JsonProperty(RU ? "Очки за крафт" : "Points for crafting")]
                public float craftScore = 1;
                [JsonProperty(RU ? "Очки за разбивание бочек" : "Points for breaking barrels")]
                public float barrelScore = 1;
                [JsonProperty(RU ? "Очки за установку строительных блоков" : "Points for placing building blocks")]
                public float BuildingScore = 1;
                [JsonProperty(RU ? "Очки за использование взрывчатых предметов" : "Points for using explosive items")]
                public Dictionary<string, float> ExplosionScore = new Dictionary<string, float>();
                [JsonProperty(RU ? "Очки за добычу ресурсов" : "Points for gathering resources")]
                public Dictionary<string, float> GatherScore = new Dictionary<string, float>();
                [JsonProperty(RU ? "Очки за найденный скрап" : "Points for collected scrap")]
                public float ScrapScore = 0.5f;
                [JsonProperty(RU ? "Очки за сбор урожая (с плантации)" : "Points for harvesting (from plantation)")]
                public float PlantScore = 0.2f;
                [JsonProperty(RU ? "Очки за убийство животных" : "Points for killing animals")]
                public float AnimalScore = 1;
                [JsonProperty(RU ? "Очки за сбитие вертолета" : "Points for shooting down a helicopter")]
                public float HeliScore = 5;
                [JsonProperty(RU ? "Очки за взрыв танка" : "Points for blowing up a tank")]
                public float BradleyScore = 5;
                [JsonProperty(RU ? "Очки за убийство NPC" : "Points for killing NPCs")]
                public float NpcScore = 5;
                [JsonProperty(RU ? "Очки за убийство игроков" : "Points for killing players")]
                public float PlayerScore = 10;
                [JsonProperty(RU ? "Очки за проведенное время на сервере (за каждую минуту)" : "Points for time spent on the server (per minute)")]
                public float TimeScore = 0.2f;
                [JsonProperty(RU ? "Сколько очков отнять за самоубийство?" : "How many points to deduct for suicide?")]
                public float SuicideScore = 2;
                [JsonProperty(RU ? "Сколько очков отнять за смерть?" : "How many points to deduct for death?")]
                public float DeathScore = 1;
                [JsonProperty(RU ? "Черный список ресурсов и взрывчатых предметов" : "Blacklist of resources and explosive items", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public List<string> blackListed = new List<string> { "ammo.rifle.explosive" };
                [JsonProperty(RU ? "Черный список предметов для крафта" : "Blacklist of crafting items", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public List<string> blackListedCraft = new List<string> { "photo", "note" };
            }
        }
        private bool IsFriends(ulong userID, ulong targetID)
        {
            if (Friends is not null)
                return Friends.Call("HasFriend", userID, targetID) is true;
    
            return RelationshipManager.ServerInstance.playerToTeam.TryGetValue(userID, out var team) && team.members.Contains(targetID);
        }
       
        private void OnPlayerConnected(BasePlayer player)
        {
            if (!_ignoreReservedPlayer.ContainsKey(player.userID.Get()))
            {
                PlayerInfo dataPlayer = PlayerInfo.Find(player.userID.Get());
                dataPlayer.Name = CleanString(player.displayName);
                if (dataPlayer.playedTime.DayNumber != DateTime.Now.ToShortDateString())
                {
                    dataPlayer.playedTime.PlayedToday = 0;
                    dataPlayer.playedTime.DayNumber = DateTime.Now.ToShortDateString();
                }
            }
            if (_prizePlayerData.ContainsKey(player.userID.Get()))
                SendMsgRewardWipe(player);
            SteamAvatarAdd(player.UserIDString);
        }

               
        private void CategoryStatUser(BasePlayer player, ulong target = 0, int cat = 0)
        {
            var container = new CuiElementContainer();
            PlayerInfo statInfo = PlayerInfo.Find(target == 0 ? player.userID.Get() : target);
            Dictionary<string, int> list = GetCategory(statInfo, cat);
                        container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.5803922 0.572549 0.6117647 0.4313726" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"151.69 {181.046 - ((list.Count - 1) * 50.729)}", OffsetMax = $"153.21 225.49" }
            }, UI_USER_STAT_INFO, "STAT_LINE");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.5803922 0.572549 0.6117647 1" },
                RectTransform = { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = $"-0.761 {-23.343 - ((list.Count - 1) * 30.729)}", OffsetMax = "0.761 0.17" }
            }, "STAT_LINE", "STAT_LINE_CHILD");
            
                        container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-146.988 -71.4", OffsetMax = "146.388 -53.16" }
            }, UI_USER_STAT_INFO, "MENU_USER_STAT");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "0.312 -9.12", OffsetMax = "84.704 9.121" },
                Button = { Command = $"UI_HandlerStat changeCategory {target} 0", Color = "0 0 0 0" },
                Text = { Text = GetLang("STAT_UI_CATEGORY_GATHER", player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, }
            }, "MENU_USER_STAT", "Panel_5655");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = cat == 0 ? "0.2988604 0.6886792 0.120194 0.6431373" : "0 0 0 0" },
                RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-42.347 0", OffsetMax = "42.196 1.871" }
            }, "Panel_5655", "Panel_8052");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-61.983 -9.121", OffsetMax = "31.232 9.12" },
                Button = { Command = $"UI_HandlerStat changeCategory {target} 1", Color = "0 0 0 0" },
                Text = { Text = GetLang("STAT_UI_CATEGORY_EXPLOSED", player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, }
            }, "MENU_USER_STAT", "Panel_56551");
		   		 		  						  	   		  	 	 		  	  			  			 		  				
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = cat == 1 ? "0.2988604 0.6886792 0.120194 0.6431373" : "0 0 0 0" },
                RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-46.608 0", OffsetMax = "46.608 1.871" }
            }, "Panel_56551", "Panel_8052");
		   		 		  						  	   		  	 	 		  	  			  			 		  				
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-115.459 -9.12", OffsetMax = "0.001 9.121" },
                Button = { Command = $"UI_HandlerStat changeCategory {target} 2", Color = "0 0 0 0" },
                Text = { Text = GetLang("STAT_UI_CATEGORY_PLANT", player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, }
            }, "MENU_USER_STAT", "Panel_56553");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = cat == 2 ? "0.2988604 0.6886792 0.120194 0.6431373" : "0 0 0 0" },
                RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-57.73 0", OffsetMax = "57.73 1.871" }
            }, "Panel_56553", "Panel_8052");
            
                        container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-146.991 -124.121", OffsetMax = "146.389 226.031" }
            }, "USER_STAT_INFO", "MAIN_LIST_STAT_USER");

            int y = 0;
            string userLang = lang.GetLanguage(player.UserIDString);
            foreach (KeyValuePair<string, int> item in list)
            {
                if (_config.settingsScore.blackListed.Contains(item.Key))
                    continue;
                float fade = 0.15f * y;
                string itemName;
                if (_itemName.ContainsKey(item.Key))
                    itemName = userLang == "ru" ? _itemName[item.Key].Ru : _itemName[item.Key].En;
                else
                {
                    itemName = "";
                    ItemDefinition itemDefinition = ItemManager.FindItemDefinition(item.Key);
                    if (itemDefinition != null && itemDefinition.displayName is { english: not null })
                    {
                        itemName = itemDefinition.displayName.english;
                    }
                }

                container.Add(new CuiElement
                {
                    Name = "STAT_USER_LINE",
                    Parent = "MAIN_LIST_STAT_USER",
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 0.03", Png = item.Key == "all" ?  _imageUI.GetImage("10") :  _imageUI.GetImage("9"), FadeIn = fade },
                    new CuiRectTransformComponent { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = $"-146.118 {-44.977 - (y * 50.729)}", OffsetMax = $"146.692 {-0.765 - (y * 50.729)}" }
                }
                });
                string name = cat == 0 ? item.Value.ToString("0,0", CultureInfo.InvariantCulture) : item.Value.ToString();

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "26.03 -10.534", OffsetMax = "126.03 12.574" },
                    Text = { Text = name, Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleRight, Color = "0.8538514 0.8491456 0.8867924 1", FadeIn = fade }
                }, "STAT_USER_LINE", "STAT_USER_AMOUNT");

                if (item.Key == "all")
                {
                    string langGet = cat switch
                    {
                        0 => "STAT_USER_TOTAL_GATHERED",
                        1 => "STAT_USER_TOTAL_EXPLODED",
                        _ => "STAT_USER_TOTAL_GROWED"
                    };

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-128 -10.534", OffsetMax = "50 12.574" },
                        Text = { Text = GetLang(langGet, player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 11, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1", FadeIn = fade }
                    }, "STAT_USER_LINE", "ALL_TOTAL");
                }
                else
                {
                    container.Add(new CuiElement
                    {
                        Name = "IMAGE_ITEM",
                        Parent = "STAT_USER_LINE",
                        Components = {
                            new CuiImageComponent { ItemId = ItemManager.FindItemDefinition(item.Key).itemid, FadeIn = fade},
                            new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-128 -17.5", OffsetMax = "-93 17.5" }
                        }
                    });
                    
                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-73.17 -10.534", OffsetMax = "50 12.574" },
                        Text = { Text = itemName, Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleLeft, Color = "0.8538514 0.8491456 0.8867924 1" , FadeIn = fade}
                    }, "STAT_USER_LINE");
                }
                y++;
            }

            
            CuiHelper.DestroyUi(player, "MENU_USER_STAT");
            CuiHelper.DestroyUi(player, "MAIN_LIST_STAT_USER");
            CuiHelper.DestroyUi(player, "STAT_LINE");
            CuiHelper.AddUi(player, container);
        }

        private void SteamAvatarAdd(string userid)
        {
            if (ImageLibrary is null || HasImage(userid))
                return;
		   		 		  						  	   		  	 	 		  	  			  			 		  				
            webrequest.Enqueue($"https://steamcommunity.com/profiles/{userid}?xml=1", null, 
                (code, response) =>
                {
                    if (code != 200 || response is null) 
                        return;
            
                    string avatarUrl = _avatarRegex.Match(response).Groups[1].ToString();
                    if (!string.IsNullOrEmpty(avatarUrl))
                    {
                        AddImage(avatarUrl, userid);
                    }
                }, this);
        }
        public const string UI_TOP_TEN_USER = "TOP_TEN_USER";

        [ConsoleCommand("stat.ignore")]
        private void CmdIgnorePlayer(ConsoleSystem.Arg arg)
        {
            if (arg == null)
                return;

            BasePlayer player = arg.Player();
            if (player != null && !permission.UserHasPermission(player.UserIDString, PermAdmin))
            {
                PrintToConsole(player, GetLang("STAT_CMD_1", player.UserIDString));
                return;
            }

            if (arg.Args == null || arg.Args.Count() == 0)
            {
                arg.ReplyWith(GetLang("STAT_CMD_2"));
                return;
            }

            ulong steamid;
            if (!ulong.TryParse(arg.Args[1], out steamid))
            {
                List<KeyValuePair<ulong, PlayerInfo>> players = FindPlayers(arg.Args[1].ToLower());

                if (players.Count == 0)
                {
                    arg.ReplyWith(GetLang("STAT_CMD_3"));
                    return;
                }

                if (players.Count > 1)
                {
                    string playersMore = "";
                    foreach (KeyValuePair<ulong, PlayerInfo> plr in players)
                        playersMore = playersMore + "\n" + plr.Value.Name + " - " + plr.Key;
		   		 		  						  	   		  	 	 		  	  			  			 		  				
                    arg.ReplyWith(GetLang("STAT_CMD_4", null, playersMore));
                    return;
                }

                steamid = players[0].Key;
            }

            string name = "";
            for (int ii = 1; ii < arg.Args.Count(); ii++)
                name += arg.Args[ii] + " ";

            name = name.Trim(' ');

            if (string.IsNullOrEmpty(name))
            {
                arg.ReplyWith(GetLang("STAT_CMD_2"));
                return;
            }

            if (!_players.ContainsKey(steamid) && !_ignoreReservedPlayer.ContainsKey(steamid))
            {
                arg.ReplyWith(GetLang("STAT_CMD_5"));
                return;
            }

            PlayerInfo playerInfo = _players.TryGetValue(steamid, out PlayerInfo player1) ? player1 : _ignoreReservedPlayer[steamid];
		   		 		  						  	   		  	 	 		  	  			  			 		  				
            switch (arg.Args[0])
            {
                case "add":
                case "a":
                    {
                        if (_ignoreReservedPlayer.ContainsKey(steamid))
                        {
                            arg.ReplyWith(GetLang("STAT_CMD_6", null, playerInfo.Name));
                            break;
                        }
                        _ignoreReservedPlayer.Add(steamid, playerInfo);
                        _players.Remove(steamid);
                        arg.ReplyWith(GetLang("STAT_CMD_7", null, playerInfo.Name));
                        break;
                    }
                case "r":
                case "remove":
                    {
                        if (!_ignoreReservedPlayer.ContainsKey(steamid))
                        {
                            arg.ReplyWith(GetLang("STAT_CMD_8", null, playerInfo.Name));
                            break;
                        }
                        PlayerInfo info = _ignoreReservedPlayer[steamid];
                        _players.Add(steamid, info);
                        _ignoreReservedPlayer.Remove(steamid);
                        arg.ReplyWith(GetLang("STAT_CMD_9", null, playerInfo.Name));
                        break;
                    }
            }
        }
        private const string PermAvailability = "XDStatistics.availability";
        private Dictionary<string, int> API_GetGathered(ulong id) => PlayerInfo.Find(id)?.gather.GatheredTotal;

        private int GetTopScore(ulong userid)
        {
            if (_config.settings.ignoreList.Contains(userid))
                return -1;

            int top = 1;
            float userScore = _players[userid].Score;

            foreach (KeyValuePair<ulong, PlayerInfo> player in _players)
            {
                if (!_config.settings.ignoreList.Contains(player.Key) && player.Value.Score > userScore)
                {
                    top++;
                }
            }
		   		 		  						  	   		  	 	 		  	  			  			 		  				
            return top;
        }
          
        private void OnPluginLoaded(Plugin name)
        {
            if (IsObjectNull(Instance) || IsObjectNull(name) || name == Instance)
                return;

            if (name.ToString() == "ZLevelsRemastered" || name.ToString() == "SkillTree")
            {
                foreach (string hook in _gatherHooks)
                    Unsubscribe(hook);
            }
            NextTick(ToggleGatherHooks);
        }

        private void DiscordPrintTopFive()
        {
            (string, string) data = GetRandomTopPlayer();
            SendDiscordMessage(CleanUpString(lang.GetMessage(data.Item1, this)), new List<string> { data.Item2 }, false);
            timer.Once(_config.discordMessage.discordSendTopTime, DiscordPrintTopFive);
        }

        
                protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["STAT_TOP_FIVE_KILL"] = "Top 5 <color=#4286f4>Killers</color>\n{0}</size>",
                ["STAT_TOP_FIVE_KILL_NPC"] = "Top 5 <color=#4286f4>NPC killers</color>\n{0}</size>",
                ["STAT_TOP_FIVE_FARM"] = "Top 5 <color=#4286f4>Farmers</color>\n{0}</size>",
                ["STAT_TOP_FIVE_EXPLOSION"] = "Top 5 <color=#4286f4>Explosions</color>\n{0}</size>",
                ["STAT_TOP_FIVE_TIMEPLAYED"] = "Top 5 <color=#4286f4>Most Time Played</color>\n{0}</size>",
                ["STAT_TOP_FIVE_BUILDINGS"] = "Top 5 <color=#4286f4>Builders</color>\n{0}</size>",
                ["STAT_TOP_FIVE_SCORE"] = "Top 5 <color=#4286f4>Most points</color>\n{0}</size>",
                ["STAT_TOP_FIVE_FERMER"] = "Top 5 <color=#4286f4>Farmers</color>\n{0}</size>",
                ["STAT_USER_TOTAL_GATHERED"] = "Total Gathered:",
                ["STAT_USER_TOTAL_EXPLODED"] = "Total Explosions:",
                ["STAT_USER_TOTAL_GROWED"] = "Total Farmed:",
                ["STAT_UI_MY_STAT"] = "My Statistics",
                ["STAT_UI_TOP_TEN"] = "Top 10 Players",
                ["STAT_UI_SEARCH"] = "Search",
                ["STAT_UI_INFO"] = "Player Information {0}",
                ["STAT_UI_ACTIVITY"] = "Activity",
                ["STAT_UI_ACTIVITY_TODAY"] = "Today: {0}",
                ["STAT_UI_ACTIVITY_TOTAL"] = "All Time: {0}",
                ["STAT_UI_SETTINGS"] = "Settings",
                ["STAT_UI_PLACE_TOP"] = "Place On Top: {0}",
                ["STAT_UI_SCORE"] = "Score: {0}",
                ["STAT_UI_PVP"] = "PvP Statistics",
                ["STAT_UI_FAVORITE_WEAPON"] = "Favorite Weapon",
                ["STAT_UI_PVP_KILLS"] = "Kills",
                ["STAT_UI_PVP_KILLS_NPC"] = "Kills NPC",
                ["STAT_UI_PVP_DEATH"] = "Deaths",
                ["STAT_UI_PVP_KDR"] = "K/D",
                ["STAT_UI_FAVORITE_WEAPON_KILLS"] = "Kills: {0}\nHits: {1}",
                ["STAT_UI_FAVORITE_WEAPON_NOT_DATA"] = "Data is still being calculated..",
                ["STAT_UI_OTHER_STAT"] = "Other Statistics",
                ["STAT_UI_HIDE_STAT"] = "Public Profile",
                ["STAT_UI_CONFIRM"] = "Are You Sure?",
                ["STAT_UI_CONFIRM_YES"] = "Yes",
                ["STAT_UI_CONFIRM_NO"] = "No",
                ["STAT_UI_RESET_STAT"] = "Reset Statistics",
                ["STAT_UI_CRATE_OPEN"] = "Crates Opened: {0}",
                ["STAT_UI_BARREL_KILL"] = "Barrels Destroyed: {0}",
                ["STAT_UI_ANIMAL_KILL"] = "Animal Kills: {0}",
                ["STAT_UI_HELI_KILL"] = "Helicopter Kills: {0}",
                ["STAT_UI_BRADLEY_KILL"] = "Bradley Kills: {0}",
                ["STAT_UI_NPC_KILL"] = "NPC Kills: {0}",
                ["STAT_UI_BTN_MORE"] = "Show More",
                ["STAT_UI_CATEGORY_GATHER"] = "Gather",
                ["STAT_UI_CATEGORY_EXPLOSED"] = "Explosions",
                ["STAT_UI_CATEGORY_PLANT"] = "Farming",
                ["STAT_UI_CATEGORY_TOP_KILLER"] = "Top 10 Killers",
                ["STAT_UI_CATEGORY_TOP_KILLER_ANIMALS"] = "Top 10 Animal Killers",
                ["STAT_UI_CATEGORY_TOP_NPCKILLER"] = "Top 10 NPC Killers",
                ["STAT_UI_CATEGORY_TOP_TIME"] = "Top 10 Most Time Played",
                ["STAT_UI_CATEGORY_TOP_GATHER"] = "Top 10 Gatherers",
                ["STAT_UI_CATEGORY_TOP_SCORE"] = "Top 10 Most Score",
                ["STAT_UI_CATEGORY_TOP_EXPLOSED"] = "Top 10 Explosions",
                ["STAT_PRINT_WIPE"] = "Wipe Detected. Data was successfully cleared!",
                ["STAT_CMD_1"] = "No Permission!!",
                ["STAT_CMD_2"] = "Usage: stat.ignore <add/remove> <Steam ID|Name>",
                ["STAT_CMD_3"] = "The specified playername could not be found. Please use their SteamID.",
                ["STAT_CMD_4"] = "Found several players with similar names: {0}",
                ["STAT_CMD_5"] = "Player not found!",
                ["STAT_CMD_6"] = "Player {0} is already ignored",
                ["STAT_CMD_7"] = "You have successfully added a player {0} to the ignore list",
                ["STAT_CMD_8"] = "The player {0} is not in the ignore list",
                ["STAT_CMD_9"] = "You have successfully removed the player {0} from the ignore list",
                ["STAT_CMD_10"] = "Player {0} successfully credited {1} score",
                ["STAT_CMD_11"] = "Player {0} successfully removed {1} score",
                ["STAT_COMMAND_SYNTAX_ERROR"] = "Incorrect syntax! Use: {0}",
                ["STAT_INVALID_PLAYER_ID_INPUT"] = "Invalid input! Please enter a valid player ID.",
                ["STAT_NOT_A_STEAM_ID"] = "The entered ID is not a SteamID. Please check and try again.",
                ["STAT_PLAYER_NOT_FOUND_BY_STEAMID"] = "Player with the specified Steam ID not found.",
                ["STAT_ADMIN_HIDE_STAT"] = "You've been added to the ignore list. You will not have access to the statistics. If this is an error, Please contact the Administrator!",
                ["STAT_TOP_PLAYER_WIPE_SCORE"] = "Congratulations!\nYou successfully held the {0} position in the previous wipe in the category <color=#4286f4>HIGHEST SCORE</color>\nYou received a well deserved Reward!",
                ["STAT_TOP_PLAYER_WIPE_TIME"] = "Congratulations!\nYou successfully held the {0} position in the previous wipe in the category <color=#4286f4>MOST TIME PLAYED</color>\nYou received a well deserved Reward!",
                ["STAT_TOP_PLAYER_WIPE_EXP"] = "Congratulations!\nYou successfully held the {0} position in the previous wipe in the category <color=#4286f4>MOST EXPLOSIONS</color>\nYou received a well deserved Reward!",
                ["STAT_TOP_PLAYER_WIPE_FARM"] = "Congratulations!\nYou successfully held the {0} position in the previous wipe in the category <color=#4286f4>MOST CROPS FARMED</color>\nYou received a well deserved Reward!",
                ["STAT_TOP_PLAYER_WIPE_KILL"] = "Congratulations!\nYou successfully held the {0} position in the previous wipe in the category <color=#4286f4>MOST KILLS</color>\nYou received a well deserved Reward!",
                ["STAT_TOP_PLAYER_WIPE_KILL_NPC"] = "Congratulations!\nYou successfully held the {0} position in the previous wipe in the category <color=#4286f4>NPC Killer</color>\nYou received a well deserved Reward!",
                ["STAT_TOP_PLAYER_WIPE_KILL_ANIMAL"] = "Congratulations!\nYou successfully held the {0} position in the previous wipe in the category <color=#4286f4>ANIMAL Killer</color>\nYou received a well deserved Reward!",
                ["STAT_TOP_VK_SCORE"] = "Топ {0} игрока по очкам\n {1}",
                ["STAT_TOP_VK_KILLER"] = "Топ {0} игрока по убийствам\n {1}",
                ["STAT_TOP_VK_TIME"] = "Топ {0} игрока по онлайну\n {1}",
                ["STAT_TOP_VK_FARM"] = "Топ {0} игрока по фарму\n {1}",
                ["STAT_TOP_VK_RAID"] = "Топ {0} игрока по рейдам\n {1}",
                ["STAT_TOP_VK_KILLER_NPC"] = "Топ {0} игрока по убийствам NPC\n {1}",
                ["STAT_TOP_VK_KILLER_ANIMAL"] = "Топ {0} игрока по убийствам животных\n {1}",

            }, this);
		   		 		  						  	   		  	 	 		  	  			  			 		  				
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["STAT_TOP_FIVE_KILL"] = "Топ 5 <color=#4286f4>киллеры</color>\n{0}</size>",
                ["STAT_TOP_FIVE_KILL_NPC"] = "Топ 5 <color=#4286f4>убийцы NPC</color>\n{0}</size>",
                ["STAT_TOP_FIVE_FARM"] = "Топ 5 <color=#4286f4>фармеры</color>\n{0}</size>",
                ["STAT_TOP_FIVE_EXPLOSION"] = "Топ 5 <color=#4286f4>рейдеры</color>\n{0}</size>",
                ["STAT_TOP_FIVE_TIMEPLAYED"] = "Топ 5 <color=#4286f4>долгожителей</color>\n{0}</size>",
                ["STAT_TOP_FIVE_BUILDINGS"] = "Топ 5 <color=#4286f4>строители</color>\n{0}</size>",
                ["STAT_TOP_FIVE_SCORE"] = "Топ 5 по <color=#4286f4>очкам</color>\n{0}</size>",
                ["STAT_TOP_FIVE_FERMER"] = "Топ 5 <color=#4286f4>фермеров</color>\n{0}</size>",
                ["STAT_USER_TOTAL_GATHERED"] = "Всего добыто:",
                ["STAT_USER_TOTAL_EXPLODED"] = "Всего взорвано:",
                ["STAT_USER_TOTAL_GROWED"] = "Всего выращено:",
                ["STAT_UI_MY_STAT"] = "Моя Статистика",
                ["STAT_UI_TOP_TEN"] = "Топ 10 игроков",
                ["STAT_UI_SEARCH"] = "Поиск",
                ["STAT_UI_INFO"] = "Информация о профиле {0}",
                ["STAT_UI_ACTIVITY"] = "Активность",
                ["STAT_UI_ACTIVITY_TODAY"] = "Сегодня: {0}",
                ["STAT_UI_ACTIVITY_TOTAL"] = "За все время: {0}",
                ["STAT_UI_SETTINGS"] = "Настройки",
                ["STAT_UI_PLACE_TOP"] = "Место в топе: {0}",
                ["STAT_UI_SCORE"] = "SCORE: {0}",
                ["STAT_UI_PVP"] = "PVP статистика",
                ["STAT_UI_FAVORITE_WEAPON"] = "Фаворитное оружие",
                ["STAT_UI_PVP_KILLS"] = "Убийств",
                ["STAT_UI_PVP_KILLS_NPC"] = "Убийств NPC",
                ["STAT_UI_PVP_DEATH"] = "Смертей",
                ["STAT_UI_PVP_KDR"] = "K/D",
                ["STAT_UI_FAVORITE_WEAPON_KILLS"] = "Убийств: {0}\nПопаданий: {1}",
                ["STAT_UI_FAVORITE_WEAPON_NOT_DATA"] = "Данные еще собираются...",
                ["STAT_UI_OTHER_STAT"] = "Другая статистика",
                ["STAT_UI_HIDE_STAT"] = "Общедоступный профиль",
                ["STAT_UI_CONFIRM"] = "Вы уверены ?",
                ["STAT_UI_CONFIRM_YES"] = "Да",
                ["STAT_UI_CONFIRM_NO"] = "Нет",
                ["STAT_UI_RESET_STAT"] = "Обнулить статистику",
                ["STAT_UI_CRATE_OPEN"] = "Открыто ящиков: {0}",
                ["STAT_UI_BARREL_KILL"] = "Разбито бочек: {0}",
                ["STAT_UI_ANIMAL_KILL"] = "Убито животных: {0}",
                ["STAT_UI_HELI_KILL"] = "Сбито вертолетов: {0}",
                ["STAT_UI_BRADLEY_KILL"] = "Танков уничтожено: {0}",
                ["STAT_UI_NPC_KILL"] = "Убито NPC: {0}",
                ["STAT_UI_BTN_MORE"] = "Показать еще",
                ["STAT_UI_CATEGORY_GATHER"] = "Добыча",
                ["STAT_UI_CATEGORY_EXPLOSED"] = "Взрывчатка",
                ["STAT_UI_CATEGORY_PLANT"] = "Фермерство",
                ["STAT_UI_CATEGORY_TOP_KILLER"] = "Топ 10 киллеров",
                ["STAT_UI_CATEGORY_TOP_KILLER_ANIMALS"] = "Топ 10 убийц животных",
                ["STAT_UI_CATEGORY_TOP_NPCKILLER"] = "Топ 10 убийц npc",
                ["STAT_UI_CATEGORY_TOP_TIME"] = "Топ 10 по онлайну",
                ["STAT_UI_CATEGORY_TOP_GATHER"] = "Топ 10 фармил",
                ["STAT_UI_CATEGORY_TOP_SCORE"] = "Топ 10 по очкам",
                ["STAT_UI_CATEGORY_TOP_EXPLOSED"] = "Топ 10 рейдеров",
                ["STAT_PRINT_WIPE"] = "Произошел вайп. Данные успешно удалены!",
                ["STAT_CMD_1"] = "Недостаточно прав!",
                ["STAT_CMD_2"] = "Используйте: stat.ignore <add/remove> <Steam ID|Имя>",
                ["STAT_CMD_3"] = "Указанный игрок не найден. Для более точного поиска укажите его SteamID.",
                ["STAT_CMD_4"] = "Найдено несколько игроков с похожим именем: {0}",
                ["STAT_CMD_5"] = "Игрок не найден!",
                ["STAT_CMD_6"] = "Игрок {0} уже игнорируется",
                ["STAT_CMD_7"] = "Вы успешно добавили игрока {0} в игнор лист",
                ["STAT_CMD_8"] = "Игрока {0} нет в списке игнорируемых",
                ["STAT_CMD_9"] = "Вы успешно убрали игрока {0} из игнор листа",
                ["STAT_CMD_10"] = "Игроку {0} успешно зачислено {1} очков",
                ["STAT_CMD_11"] = "Игроку {0} успешно снято {1} очков",
                ["STAT_COMMAND_SYNTAX_ERROR"] = "Неверный синтаксис! Используйте: {0}",
                ["STAT_INVALID_PLAYER_ID_INPUT"] = "Неверный ввод! Пожалуйста, введите корректный ID игрока.",
                ["STAT_NOT_A_STEAM_ID"] = "Введенный ID не является SteamID. Пожалуйста, проверьте и попробуйте снова.",
                ["STAT_PLAYER_NOT_FOUND_BY_STEAMID"] = "Игрок с указанным Steam ID не найден.",
                ["STAT_ADMIN_HIDE_STAT"] = "Вы добавлены в игнор лист. У вас нет доступа к статистики, если это ошибка, свяжитесь с администратором!",
                ["STAT_TOP_PLAYER_WIPE_SCORE"] = "Поздравляю!\nВ прошлом вайпе вы успешно удерживали {0} позицию в категории <color=#4286f4>SCORE</color>\nВы заслужено получаете награду!",
                ["STAT_TOP_PLAYER_WIPE_TIME"] = "Поздравляю!\nВ прошлом вайпе вы успешно удерживали {0} позицию в категории <color=#4286f4>Долгожитель</color>\nВы заслужено получаете награду!",
                ["STAT_TOP_PLAYER_WIPE_EXP"] = "Поздравляю!\nВ прошлом вайпе вы успешно удерживали {0} позицию в категории <color=#4286f4>Рейдер</color>\nВы заслужено получаете награду!",
                ["STAT_TOP_PLAYER_WIPE_FARM"] = "Поздравляю!\nВ прошлом вайпе вы успешно удерживали {0} позицию в категории <color=#4286f4>Добытчик</color>\nВы заслужено получаете награду!",
                ["STAT_TOP_PLAYER_WIPE_KILL"] = "Поздравляю!\nВ прошлом вайпе вы успешно удерживали {0} позицию в категории <color=#4286f4>Киллер</color>\nВы заслужено получаете награду!",
                ["STAT_TOP_PLAYER_WIPE_KILL_NPC"] = "Поздравляю!\nВ прошлом вайпе вы успешно удерживали {0} позицию в категории <color=#4286f4>Убийца нпс</color>\nВы заслужено получаете награду!",
                ["STAT_TOP_PLAYER_WIPE_KILL_ANIMAL"] = "Поздравляю!\nВ прошлом вайпе вы успешно удерживали {0} позицию в категории <color=#4286f4>Убийца животных</color>\nВы заслужено получаете награду!",
                ["STAT_TOP_VK_SCORE"] = "Топ {0} игрока по очкам\n {1}",
                ["STAT_TOP_VK_KILLER"] = "Топ {0} игрока по убийствам\n {1}",
                ["STAT_TOP_VK_TIME"] = "Топ {0} игрока по онлайну\n {1}",
                ["STAT_TOP_VK_FARM"] = "Топ {0} игрока по фарму\n {1}",
                ["STAT_TOP_VK_RAID"] = "Топ {0} игрока по рейдам\n {1}",
                ["STAT_TOP_VK_KILLER_NPC"] = "Топ {0} игрока по убийствам NPC\n {1}",
                ["STAT_TOP_VK_KILLER_ANIMAL"] = "Топ {0} игрока по убийствам животных\n {1}",

            }, this, "ru");
        }
        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject("XDStatistics/StatsUser", _players);
                [PluginReference] Plugin ImageLibrary, Friends, Clans, Battles, Duel, Economics, IQEconomic, ServerRewards, GameStoresRUST, RustStore, Duelist, EventHelper, ArenaTournament, ZLevelsRemastered, SkillTree, IQFakeActive;


        private (string, string) GetRandomTopPlayer()
        {
            int i = Core.Random.Range(0, 7);
            string playerstat = string.Empty;
            string langmsg;

            Func<KeyValuePair<ulong, PlayerInfo>, float> selector;
            Func<PlayerInfo, string> formatter;

            switch (i)
            {
                case 0:
                    if (_config.settings.pveServerMode)
                    {
                        selector = x => x.Value.pVP.KillsNpc;
                        formatter = playerInfo => $"{playerInfo.pVP.KillsNpc}";
                        langmsg = "STAT_TOP_FIVE_KILL_NPC";
                    }
                    else
                    {
                        selector = x => x.Value.pVP.Kills;
                        formatter = playerInfo => $"{playerInfo.pVP.Kills}";
                        langmsg = "STAT_TOP_FIVE_KILL";
                    }

                    break;
                case 1:
                    selector = x => x.Value.gather.AllGathered;
                    formatter = playerInfo => $"{playerInfo.gather.AllGathered:0,0}";
                    langmsg = "STAT_TOP_FIVE_FARM";
                    break;
                case 2:
                    selector = x => x.Value.explosion.AllExplosionUsed;
                    formatter = playerInfo => $"{playerInfo.explosion.AllExplosionUsed}";
                    langmsg = "STAT_TOP_FIVE_EXPLOSION";
                    break;
                case 3:
                    selector = x => x.Value.playedTime.PlayedForWipe;
                    formatter = playerInfo => $"{TimeHelper.FormatTime(TimeSpan.FromMinutes(playerInfo.playedTime.PlayedForWipe), 5, lang.GetServerLanguage())}";
                    langmsg = "STAT_TOP_FIVE_TIMEPLAYED";
                    break;
                case 4:
                    selector = x => x.Value.otherStat.BuildingCrate;
                    formatter = playerInfo => $"{playerInfo.otherStat.BuildingCrate}";
                    langmsg = "STAT_TOP_FIVE_BUILDINGS";
                    break;
                case 5:
                    selector = x => x.Value.Score;
                    formatter = playerInfo => $"{playerInfo.Score:0.0}";
                    langmsg = "STAT_TOP_FIVE_SCORE";
                    break;
                case 6:
                    selector = x => x.Value.harvesting.AllHarvesting;
                    formatter = playerInfo => $"{playerInfo.harvesting.AllHarvesting}";
                    langmsg = "STAT_TOP_FIVE_FERMER";
                    break;
                default:
                    return (string.Empty, string.Empty);
            }

            List<KeyValuePair<ulong, PlayerInfo>> sortedPlayers = _players.OrderByDescending(selector)
                .XDWhere(x => !_config.settings.ignoreList.Contains(x.Key))
                .XDToList();

            int top = 1;

            foreach (KeyValuePair<ulong, PlayerInfo> player in sortedPlayers)
            {
                if (top > 5) break;
                

                playerstat += $"<color=#faec84>{top}</color>. {player.Value.Name} : {formatter(player.Value)}\n";
                top++;
            }

            return (langmsg, playerstat);
        }
        
        
                
        public static bool IsObjectNull(object obj) => ReferenceEquals(obj, null);
        
        private void OnGrowableGathered(GrowableEntity plant, Item item, BasePlayer player)
        {
            if (_alowedSeedId.Contains(item.info.itemid))
            {
                NextTick(() =>
                {
                    PlayerInfo playerStat = PlayerInfo.Find(player.userID.Get());

                    if (!playerStat.harvesting.HarvestingList.TryGetValue(item.info.shortname, out int currentAmount))
                    {
                        playerStat.harvesting.HarvestingList[item.info.shortname] = item.amount;
                    }
                    else
                    {
                        playerStat.harvesting.HarvestingList[item.info.shortname] = currentAmount + item.amount;
                    }

                    playerStat.harvesting.AllHarvesting += item.amount;
                    playerStat.Score += _config.settingsScore.PlantScore;
                });
            }
        }

        
        
        private JObject API_GetAllPlayerStat(ulong id) => JObject.FromObject(PlayerInfo.Find(id));
        
        [ConsoleCommand("stat.wipe")]
        void StatWipeStat(ConsoleSystem.Arg arg)
        {
            if (arg is null) return;

            BasePlayer player = arg.Player();
            if (player != null && !player.IsAdmin)
            {
                PrintToConsole(player, GetLang("STAT_CMD_1", player.UserIDString));
                return;
            }
            
            if (_config.settingsPrize.prizeUse)
            {
                RewardPlayerCoroutine = ServerMgr.Instance.StartCoroutine(ParseTopUserForPrize());
            }
            else if (_config.settings.wipeData)
            {
                PlayerInfo.ClearDataWipe();
                NextTick(() => {
                    SaveData();
                    SaveDataIgnoreList();
                });
                PrintWarning(GetLang("STAT_PRINT_WIPE"));
            }
        }

        private object OnPlayerDeath(BasePlayer player, HitInfo hitInfo)
        {
            if (player == null || hitInfo == null)
                return null;

            if (hitInfo.InitiatorPlayer != null)
            {
                BasePlayer initiator = hitInfo.InitiatorPlayer;
                if (!initiator.userID.IsSteamId())
                    return null;
                PlayerInfo playerStatInitiator = PlayerInfo.Find(initiator.userID.Get());

                if (!player.userID.IsSteamId() || player.IsBot)
                {
                    playerStatInitiator.pVP.KillsNpc++;
                    playerStatInitiator.Score += _config.settingsScore.NpcScore;
                    if (_config.settings.npsDeathUse)
                        WeaponProgressAdd(playerStatInitiator, hitInfo, true);
                    return null;
                }

                if (IsFriends(initiator.userID.Get(), player.userID.Get()) ||
                    IsClans(initiator.UserIDString, player.UserIDString) ||
                    IsDuel(initiator.userID.Get()))
                    return null;

                PlayerInfo playerStatVictim = PlayerInfo.Find(player.userID.Get());

                if (hitInfo.damageTypes.GetMajorityDamageType() == DamageType.Suicide)
                {
                    playerStatVictim.pVP.Suicides++;
                    playerStatVictim.pVP.Deaths++;
                    playerStatVictim.Score -= _config.settingsScore.SuicideScore;
                    return null;
                }
		   		 		  						  	   		  	 	 		  	  			  			 		  				
                if (initiator != player)
                {
                    WeaponProgressAdd(playerStatInitiator, hitInfo, true);
                    playerStatInitiator.pVP.Kills++;
                    playerStatInitiator.pVP.Shots++;
                    playerStatInitiator.pVP.Headshots += hitInfo.isHeadshot ? 1 : 0;
                    playerStatInitiator.Score += _config.settingsScore.PlayerScore;
                }
                else
                {

                    playerStatVictim.pVP.Suicides++;
                    playerStatVictim.Score -= _config.settingsScore.SuicideScore;
                }

                playerStatVictim.pVP.Deaths++;
                playerStatVictim.Score -= _config.settingsScore.DeathScore;
            }
            else
            {
                if (hitInfo.damageTypes.GetMajorityDamageType() != DamageType.Suicide)
                {
                    PlayerInfo playerStatVictim = PlayerInfo.Find(player.userID.Get());
                    playerStatVictim.pVP.Deaths++;
                    playerStatVictim.Score -= _config.settingsScore.DeathScore;
                }
            }

            return null;
        }
        private static Dictionary<ulong, List<PrizePlayer>> _prizePlayerData = new();
        
                private void UserStat(BasePlayer player, ulong target = 0)
        {
            PlayerInfo statInfo = PlayerInfo.Find(target == 0 ? player.userID.Get() : target);
            ulong userid = target == 0 ? player.userID.Get() : target;
            string color = BasePlayer.FindByID(userid) != null ? "0.55 0.78 0.24 1" : "0.8 0.28 0.2 1";
            int kills = _config.settings.pveServerMode ? statInfo.pVP.KillsNpc : statInfo.pVP.Kills;
            string titleKills = _config.settings.pveServerMode ? GetLang("STAT_UI_PVP_KILLS_NPC", player.UserIDString) : GetLang("STAT_UI_PVP_KILLS", player.UserIDString);
            string pageTitle =  "<color=white>" + statInfo.Name.ToString() + "</color>";

            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-400.254 -331.554", OffsetMax = "393.974 274.446" }
            }, UI_INTERFACE, UI_USER_STAT);
		   		 		  						  	   		  	 	 		  	  			  			 		  				
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-400.254 -331.554", OffsetMax = "393.974 274.446" }
            }, UI_USER_STAT, UI_USER_STAT_INFO);
		   		 		  						  	   		  	 	 		  	  			  			 		  				
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "14.908 -69.438", OffsetMax = "227.492 -53.162" },
                Text = { Text = GetLang("STAT_UI_INFO", player.UserIDString, pageTitle), Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" }
            }, UI_USER_STAT_INFO, "INFO_USER_NICK");
            
            container.Add(new CuiElement
            {
                Name = "USER_AVATAR_LAYER",
                Parent = UI_USER_STAT_INFO,
                Components = {
                    new CuiRawImageComponent { Color = color, Png =  _imageUI.GetImage("2") },
                    new CuiRectTransformComponent {AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "15 -130", OffsetMax = "68 -77" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "AVATAR_ON_STEAM",
                Parent = "USER_AVATAR_LAYER",
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1",  Png = GetImage(target == 0 ? player.UserIDString : target.ToString()) },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-25 -25", OffsetMax = "25 25" }
                }
            });

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "15.477 -179.883", OffsetMax = "87.323 -161.717" },
                Text = { Text = GetLang("STAT_UI_ACTIVITY", player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 11, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" }
            }, UI_USER_STAT_INFO, "USER_ACTIVE");

            if (target == 0 && (_config.settings.availabilityUse && (permission.UserHasPermission(player.UserIDString, PermAvailability) ||  _config.settings.dropStatUse && permission.UserHasPermission(player.UserIDString, PermReset))))
            {
                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "15.477 -290.083", OffsetMax = "87.323 -271.917" },
                    Text = { Text = GetLang("STAT_UI_SETTINGS", player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 11, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" }
                }, UI_USER_STAT_INFO, "USER_SETINGS");
            }

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "14.8 -151.281", OffsetMax = "227.38 -134.319" },
                Text = { Text = GetLang("STAT_UI_PLACE_TOP", player.UserIDString, GetTopScore(target == 0 ? player.userID.Get() : target)), Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleLeft, Color = "0.8538514 0.8491456 0.8867924 1" }
            }, UI_USER_STAT_INFO, "TOP_IN_USER");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "14.69 -201.181", OffsetMax = "227.27 -184.219" },
                Text = { Text = GetLang("STAT_UI_ACTIVITY_TODAY", player.UserIDString, TimeHelper.FormatTime(TimeSpan.FromMinutes(statInfo.playedTime.PlayedToday), 5, lang.GetLanguage(player.UserIDString))), Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleLeft, Color = "0.8538514 0.8491456 0.8867924 1" }
            }, UI_USER_STAT_INFO, "TODAY_ACTIVE_USER");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-382.31 79.219", OffsetMax = "-169.73 96.181" },
                Text = { Text = GetLang("STAT_UI_ACTIVITY_TOTAL", player.UserIDString, TimeHelper.FormatTime(TimeSpan.FromMinutes(statInfo.playedTime.PlayedForWipe), 5, lang.GetLanguage(player.UserIDString))), Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleLeft, Color = "0.8538514 0.8491456 0.8867924 1" }
            }, UI_USER_STAT_INFO, "ALLTIME_ACTIVE_USER");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-382.31 56.919", OffsetMax = "-169.73 73.881" },
                Text = { Text = GetLang("STAT_UI_SCORE", player.UserIDString, statInfo.Score.ToString("0.0")), Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleLeft, Color = "0.8538514 0.8491456 0.8867924 1" }
            }, UI_USER_STAT_INFO, "SCORE_USER");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-209.5 -67.825", OffsetMax = "-130.043 -53.66" },
                Text = { Text = GetLang("STAT_UI_PVP", player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 11, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" }
            }, UI_USER_STAT_INFO, "PVP_STAT_USER");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-209.505 36.618", OffsetMax = "-13.975 50.782" },
                Text = { Text = GetLang("STAT_UI_FAVORITE_WEAPON", player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 11, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" }
            }, UI_USER_STAT_INFO, "RIFLE_FAVORITE_USER");

                        container.Add(new CuiElement
            {
                Name = "KILL_STAT_PLAYER",
                Parent = UI_USER_STAT_INFO,
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png =  _imageUI.GetImage("7") },
                    new CuiRectTransformComponent { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-209.389 -123.243", OffsetMax = "-14.469 -78.4" }
                }
            });

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "19 -7.014", OffsetMax = "97 8.414" },
                Text = { Text = titleKills, Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" }
            }, "KILL_STAT_PLAYER", "LABEL_KILL_AMOUNT");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-98 -7.014", OffsetMax = "-16.845 8.414" },
                Text = { Text = kills.ToString(), Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleRight, Color = "0.8538514 0.8491456 0.8867924 1" }
            }, "KILL_STAT_PLAYER", "LABEL_KILL_AMOUNTTWO");
            
                        container.Add(new CuiElement
            {
                Name = "KILLSHOT_STAT_PLAYER",
                Parent = UI_USER_STAT_INFO,
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png =  _imageUI.GetImage("7") },
                    new CuiRectTransformComponent { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-209.389 -173.691", OffsetMax = "-14.469 -128.849" }
                }
            });

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "19 -7.014", OffsetMax = "97 8.414" },
                Text = { Text = GetLang("STAT_UI_PVP_DEATH", player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" }
            }, "KILLSHOT_STAT_PLAYER", "LABEL_KILL_AMOUNT");
		   		 		  						  	   		  	 	 		  	  			  			 		  				
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-98 -7.014", OffsetMax = "-16.845 8.414" },
                Text = { Text = statInfo.pVP.Deaths.ToString(), Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleRight, Color = "0.8538514 0.8491456 0.8867924 1" }
            }, "KILLSHOT_STAT_PLAYER", "LABEL_KILL_AMOUNTTWO");
            
                        container.Add(new CuiElement
            {
                Name = "DEATCH_STAT_PLAYER",
                Parent = UI_USER_STAT_INFO,
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1",Png =  _imageUI.GetImage("7") },
                    new CuiRectTransformComponent { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-209.39 -224.721", OffsetMax = "-14.47 -179.879" }
                }
            });
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "19 -7.014", OffsetMax = "97 8.414" },
                Text = { Text = GetLang("STAT_UI_PVP_KDR", player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" }
            }, "DEATCH_STAT_PLAYER", "LABEL_KILL_AMOUNT");
            float kdr = statInfo.pVP.Deaths == 0 ? kills : (float)Math.Round(((float)kills) / statInfo.pVP.Deaths, 2);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-98 -7.014", OffsetMax = "-16.845 8.414" },
                Text = { Text = kdr.ToString(), Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleRight, Color = "0.8538514 0.8491456 0.8867924 1" }
            }, "DEATCH_STAT_PLAYER", "LABEL_KILL_AMOUNTTWO");
            
                        container.Add(new CuiElement
            {
                Name = "FAVORITE_WEAPON_STAT_PLAYER",
                Parent = UI_USER_STAT_INFO,
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png =  _imageUI.GetImage("8") },
                    new CuiRectTransformComponent { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-210.15 -324.607", OffsetMax = "-13.977 -278.569" }
                }
            });

            var weaponTop = statInfo.weapon.WeaponUsed.OrderByDescending(x => x.Value.Kills).Take(1).FirstOrDefault();
            if (weaponTop.Key != null)
            {
                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "46.321 -16.5", OffsetMax = "176.509 16.5" },
                    Text = { Text = GetLang("STAT_UI_FAVORITE_WEAPON_KILLS", player.UserIDString, weaponTop.Value.Kills, weaponTop.Value.Shots), Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
                }, "FAVORITE_WEAPON_STAT_PLAYER", "LABEL_KILL_AMOUNT");
		   		 		  						  	   		  	 	 		  	  			  			 		  				
                container.Add(new CuiElement
                {
                    Name = "WEAPON_IMG_USER",
                    Parent = "FAVORITE_WEAPON_STAT_PLAYER",
                    Components = {
                    new CuiImageComponent { ItemId = ItemManager.FindItemDefinition(weaponTop.Key).itemid, },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-88.5 -16.5", OffsetMax = "-55.5 16.5" }
                }
                });
            }
            else
            {
                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Text = { Text = GetLang("STAT_UI_FAVORITE_WEAPON_NOT_DATA", player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
                }, "FAVORITE_WEAPON_STAT_PLAYER", "LABEL_KILL_AMOUNT");
            }
            
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-209.498 -63.383", OffsetMax = "-79.669 -49.219" },
                Text = { Text = GetLang("STAT_UI_OTHER_STAT", player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 11, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" }
            }, UI_USER_STAT_INFO, "OTHER_STAT_LABEL");

            
            CloseLayer(player);
            CuiHelper.DestroyUi(player, "USER_STAT");
            CuiHelper.AddUi(player, container);
            CategoryStatUser(player, target);
            OtherStatUser(player, target);
            if (target == 0)
            {
                if (_config.settings.dropStatUse && permission.UserHasPermission(player.UserIDString, PermReset))
                    ButtonDropStat(player, statInfo);
                if (_config.settings.availabilityUse && permission.UserHasPermission(player.UserIDString, PermAvailability))
                    ButtonHideStat(player, statInfo);
            }
        }
        
                private void SearchPageUser(BasePlayer player, string target = "")
        {
            string SearchName = "";
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "1 1 1 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-378.454 -264.835", OffsetMax = "381.998 266.939" }
            }, UI_INTERFACE, UI_SEARCH_USER);

            container.Add(new CuiElement
            {
                Name = "SEARCH_LINE",
                Parent = UI_SEARCH_USER,
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png =  _imageUI.GetImage("18") },
                    new CuiRectTransformComponent {AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-181.67 -31.1", OffsetMax = "149.27 -7.1" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "LOUPE_SEARCH_IMG",
                Parent = "SEARCH_LINE",
                Components = {
                    new CuiImageComponent { Color = "1 1 1 1", Sprite = "assets/icons/examine.png" },
                    new CuiRectTransformComponent { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "13.87 -10", OffsetMax = "33.87 10" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "INPUT_SEARCH",
                Parent = "SEARCH_LINE",
                Components = {
                    new CuiInputFieldComponent { Text = SearchName, Command = $"UI_HandlerStat listplayer {SearchName}", Color = "1 1 1 1", FontSize = 10, Align = TextAnchor.MiddleLeft, NeedsKeyboard = true, CharsLimit = 45 },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-119.86 -9.314", OffsetMax = "129.03 9.591" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-181.67 -1.95", OffsetMax = "149.27 212.35" }
            }, UI_SEARCH_USER, "LIST_USER_SEARCH");

            int y = 0, x = 0;
            foreach (var players in _players.Where(z => z.Value.Name.ToLower().Contains(target) && !_config.settings.ignoreList.Contains(z.Key)))
            {
                string LockStatus = players.Value.HidedStatistics == true ? "assets/icons/lock.png" : "assets/icons/unlock.png";
                string Command = players.Value.HidedStatistics == true ? "" : $"UI_HandlerStat GoStatPlayers {players.Key}";
                string nickName =  "<color=white>" + GetCorrectName(players.Value.GetPlayerName(player.userID.Get()), 14) + "</color>";
                if (permission.UserHasPermission(player.UserIDString, PermAdmin))
                    Command = $"UI_HandlerStat GoStatPlayers {players.Key}";

                container.Add(new CuiElement
                {
                    Name = "USER_IN_SEARCH",
                    Parent = "LIST_USER_SEARCH",
                    Components = {
                        new CuiRawImageComponent { Color = "1 1 1 1", Png =  _imageUI.GetImage("19") },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"{-164.971 + (x * 112.586)} {84.138 - (y * 26.281)}", OffsetMax = $"{-62.801 + (x * 112.586)} {105.623 - (y * 26.281)}" }
                    }
                });

                container.Add(new CuiElement
                {
                    Name = "USER_HIDE_PROFILE",
                    Parent = "USER_IN_SEARCH",
                    Components = {
                    new CuiImageComponent { Color = "1 1 1 1", Sprite = LockStatus },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-45.68 -7.5", OffsetMax = "-30.68 7.5" }
                }
                });
                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-26.832 -10.743", OffsetMax = "48.365 10.743" },
                    Text = { Text = nickName  ?? "UNKNOW" , Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" }
                }, "USER_IN_SEARCH");

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { Command = Command, Color = "0 0 0 0" },
                    Text = { Text = ""}
                }, "USER_IN_SEARCH");

                x++;
                if (x == 3)
                {
                    x = 0;
                    y++;
                    if (y == 8)
                        break;
                }
            }
            CloseLayer(player);
            CuiHelper.AddUi(player, container);
        }
        private Dictionary<string, string> _prefabNameItem = new()
        {
            ["40mm_grenade_he"] = "multiplegrenadelauncher",
            ["grenade.beancan.deployed"] = "grenade.beancan",
            ["grenade.f1.deployed"] = "grenade.f1",
            ["explosive.satchel.deployed"] = "explosive.satchel",
            ["explosive.timed.deployed"] = "explosive.timed",
            ["rocket_basic"] = "ammo.rocket.basic",
            ["rocket_hv"] = "ammo.rocket.hv",
            ["rocket_fire"] = "ammo.rocket.fire",
            ["survey_charge.deployed"] = "surveycharge"
        };

        private static readonly string[] ZlevelsGatherHooks =
        {
            "OnZLevelDispenserGather",
            "OnZLevelCollectiblePickup",
            "OnZLevelGrowableGathered",
        };

		   		 		  						  	   		  	 	 		  	  			  			 		  				
        private static List<KeyValuePair<ulong, PlayerInfo>> FindPlayers(string name)
        {
            List<KeyValuePair<ulong, PlayerInfo>> players = new();
            string searchTerm = name.ToLower();

            IEnumerable<KeyValuePair<ulong, PlayerInfo>> playersData = _players.Concat(_ignoreReservedPlayer);
            foreach (KeyValuePair<ulong, PlayerInfo> activePlayer in playersData)
            {
                string playerNameLower = activePlayer.Value.Name.ToLower();
                if (playerNameLower == searchTerm || playerNameLower.Contains(searchTerm))
                    players.Add(activePlayer);
            }

            return players;
        }

        private class PlayerInfo
        {
            public string Name = string.Empty;
            public bool HidedStatistics = false;
            internal string GetPlayerName(ulong steamId) => string.IsNullOrWhiteSpace(Name) ? 
                Instance.covalence.Players.FindPlayerById(steamId.ToString())?.Name ?? "UNKNOWN" : Name;
            public float Score = 0;
            public Harvesting harvesting = new Harvesting();
            public OtherStat otherStat = new OtherStat();
            public Explosion explosion = new Explosion();
            public Gather gather = new Gather();
            public Weapon weapon = new Weapon();
            public PVP pVP = new PVP();
            public PlayedTime playedTime = new PlayedTime();

            internal class PlayedTime
            {
                public string DayNumber = DateTime.Now.ToShortDateString();
                public int PlayedForWipe = 0;
                public int PlayedToday = 0;
            }
            internal class Harvesting
            {
                public Dictionary<string, int> HarvestingList = new Dictionary<string, int>();
                public int AllHarvesting = 0;
            }
            internal class OtherStat
            {
                public int CrateOpen = 0;
                public int BarrelDeath = 0;
                public int AllCraft = 0;
                public int BuildingCrate = 0;
                public int AnimalsKill = 0;
            }
            internal class Explosion
            {
                public Dictionary<string, int> ExplosionUsed = new Dictionary<string, int>()
                {
                   ["explosive.timed"] = 0,
                   ["explosive.satchel"] = 0,
                   ["grenade.beancan"] = 0,
                   ["grenade.f1"] = 0,
                   ["ammo.rocket.basic"] = 0,
                   ["ammo.rocket.hv"] = 0,
                   ["ammo.rocket.fire"] = 0,
                   ["ammo.rifle.explosive"] = 0
                };
                public int AllExplosionUsed = 0;
            }
            internal class Gather
            {
                public Dictionary<string, int> GatheredTotal = new Dictionary<string, int>()
                {
                    ["wood"] = 0,
                    ["stones"] = 0,
                    ["metal.ore"] = 0,
                    ["sulfur.ore"] = 0,
                    ["hq.metal.ore"] = 0,
                    ["scrap"] = 0,
                };
                public int AllGathered = 0; 
            }
            internal class Weapon
            {
                public Dictionary<string, WeaponInfo> WeaponUsed = new Dictionary<string, WeaponInfo>();
                internal class WeaponInfo
                {
                    public int Kills = 0;
                    public int Headshots = 0;
                    public int Shots = 0;
                }
            }
            internal class PVP
            {
                public int Kills = 0;
                public int KillsNpc = 0;
                public int Deaths = 0;
                public int Suicides = 0;
                public int Shots = 0;
                public int Headshots = 0;
                public int HeliKill = 0;
                public int BradleyKill = 0;
            }
            public static void AddPlayedTime(ulong id)
            {
                if (!_players.TryGetValue(id, out PlayerInfo player))
                    return;

                player.playedTime.PlayedToday++;
                player.playedTime.PlayedForWipe++;
                player.Score += Instance._config.settingsScore.TimeScore;

                string currentDate = DateTime.Now.ToShortDateString();
                if (player.playedTime.DayNumber != currentDate)
                {
                    player.playedTime.PlayedToday = 0;
                    player.playedTime.DayNumber = currentDate;
                }
            }

            public static void PlayerClearData(ulong id)
            {
                BasePlayer player = BasePlayer.FindByID(id);
                _players[id] = new PlayerInfo();
                _players[id].Name = player.displayName;
            }
            public static void ClearDataWipe()
            {
                _ignoreReservedPlayer?.Clear();
                _players?.Clear();
            }
            public static PlayerInfo Find(ulong id)
            {
                if (_players.TryGetValue(id, out PlayerInfo player))
                {
                    return player;
                }

                if (_ignoreReservedPlayer.TryGetValue(id, out PlayerInfo ignoredPlayer))
                {
                    return ignoredPlayer;
                }
                
                

                PlayerInfo newPlayer = new();
                _players[id] = newPlayer;

                return newPlayer;
            }
            
            public static PlayerInfo EXFind(ulong id)
            {
                if (_players.TryGetValue(id, out PlayerInfo player))
                {
                    return player;
                }
		   		 		  						  	   		  	 	 		  	  			  			 		  				
                if (_ignoreReservedPlayer.TryGetValue(id, out PlayerInfo ignoredPlayer))
                {
                    return ignoredPlayer;
                }

                return null;
            }

        }

        private void OnCollectiblePickup(CollectibleEntity collectible, BasePlayer player)
        {
            if (player == null || collectible == null || collectible.itemList == null)
                return;

            foreach (ItemAmount item in collectible.itemList)
            {
                NextTick(() =>
                {
                    int itemId = item.itemDef.itemid;
                    int itemAmount = (int)item.amount;
                    string itemShortName = item.itemDef.shortname;

                    if (_alowedSeedId.Contains(itemId))
                    {
                        PlayerInfo playerstat = PlayerInfo.Find(player.userID.Get());

                        if (playerstat.harvesting.HarvestingList.TryGetValue(itemShortName, out int currentAmount))
                        {
                            playerstat.harvesting.HarvestingList[itemShortName] = currentAmount + itemAmount;
                        }
                        else
                        {
                            playerstat.harvesting.HarvestingList.Add(itemShortName, itemAmount);
                        }

                        playerstat.harvesting.AllHarvesting += itemAmount;
                        playerstat.Score += _config.settingsScore.PlantScore;
                    }
                    else
                    {
                        ProgressAdd(player, itemShortName, itemAmount, true);
                    }
                });
            }
        }
        public const string UI_SEARCH_USER = "SEARCH_USER";

        private string GetImage(string shortname, ulong skin = 0) => (string)ImageLibrary?.Call("GetImage", shortname, skin);
        private int? API_GetAllGathered(ulong id) => PlayerInfo.Find(id)?.gather.AllGathered;
        
        private class PrizePlayer
        {
            public string Name = string.Empty;
            public CatType catType;
            public int value;
            public int top;
        }

        private void STCanReceiveYield(BasePlayer player, CollectibleEntity entity, ItemAmount ia)
        {
            if (player == null || ia == null || ia.itemDef == null) return;
            
            NextTick(() =>
            {
                int itemId = ia.itemDef.itemid;
                int itemAmount = (int)ia.amount;
                string itemShortName = ia.itemDef.shortname;

                if (_alowedSeedId.Contains(itemId))
                {
                    PlayerInfo playerstat = PlayerInfo.Find(player.userID.Get());

                    if (playerstat.harvesting.HarvestingList.TryGetValue(itemShortName, out int currentAmount))
                    {
                        playerstat.harvesting.HarvestingList[itemShortName] = currentAmount + itemAmount;
                    }
                    else
                    {
                        playerstat.harvesting.HarvestingList.Add(itemShortName, itemAmount);
                    }
		   		 		  						  	   		  	 	 		  	  			  			 		  				
                    playerstat.harvesting.AllHarvesting += itemAmount;
                    playerstat.Score += _config.settingsScore.PlantScore;
                }
                else
                {
                    ProgressAdd(player, itemShortName, itemAmount, true);
                }
            });
        }
        
        private void OnContainerDropItems(ItemContainer container)
        {
            if (container == null)
                return;
            BaseEntity entity = container.entityOwner;
            if (entity == null)
                return;
            if (!entity.ShortPrefabName.Contains("barrel"))
                return;
            foreach (Item lootitem in container.itemList)
            {
                if (lootitem.info.shortname == "scrap" && lootitem.skin == 0)
                    lootitem.SetFlag(global::Item.Flag.Placeholder, true);
            }
        }

        [ConsoleCommand("stat.score")]
        void StatScoreGive(ConsoleSystem.Arg arg)
        {
            if (arg is null) return;

            BasePlayer player = arg.Player();
            if (player != null && !player.IsAdmin)
            {
                PrintToConsole(player, GetLang("STAT_CMD_1", player.UserIDString));
                return;
            }
            
            if (!arg.HasArgs(3))
            {
                SendConsoleMessage(player, GetLang("STAT_COMMAND_SYNTAX_ERROR", player?.UserIDString, "stat.score [give/remove] STEAMID SCORE"));
                return;
            }
            
            if(!ulong.TryParse(arg.GetString(1), out ulong playerid))
            {
                SendConsoleMessage(player, GetLang("STAT_INVALID_PLAYER_ID_INPUT", player?.UserIDString));
                return;
            }
            
            if (!playerid.IsSteamId())
            {
                SendConsoleMessage(player, GetLang("STAT_NOT_A_STEAM_ID", player?.UserIDString));
                return;
            }

            if (!int.TryParse(arg.Args[2], out int score))
            {
                Puts("Invalid score format.");
                return;
            }

            PlayerInfo playerInfo = PlayerInfo.EXFind(playerid);
            if (playerInfo is null)
            {
                SendConsoleMessage(player, GetLang("STAT_PLAYER_NOT_FOUND_BY_STEAMID", player?.UserIDString));
                return;
            }
            
            switch (arg.Args[0])
            {
                case "give":
                {
                    playerInfo.Score += score;
                    Puts(GetLang("STAT_CMD_10", null, playerid, score));
                    break;
                }
                case "remove":
                {
                    playerInfo.Score -= score;
                    Puts(GetLang("STAT_CMD_11", null, playerid, score));
                    break;
                }
            }
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }
        
        
        private void OnLootEntity(BasePlayer player, LootContainer entity)
        {
            if (entity == null || player == null || !entity.OwnerID.IsSteamId() || entity.net?.ID == null)
                return;

            ulong netId = entity.net.ID.Value;

            if (!_lootersListCarte.TryGetValue(player, out List<ulong> playerLoots))
            {
                playerLoots = new List<ulong>();
                _lootersListCarte[player] = playerLoots;
            }

            if (playerLoots.Contains(netId))
                return;

            PlayerInfo playerstat = PlayerInfo.Find(player.userID.Get());
            playerstat.otherStat.CrateOpen++;

            foreach (Item item in entity.inventory.itemList)
            {
                if (item.info.shortname == "scrap")
                {
                    ProgressAdd(player, item.info.shortname, item.amount);
                }
            }
		   		 		  						  	   		  	 	 		  	  			  			 		  				
            playerLoots.Add(netId);
        }
        
        private void OnWeaponFired(BaseProjectile projectile, BasePlayer player)
        {
            if (projectile == null || player == null)
                return;
            if (projectile.primaryMagazine != null && projectile.primaryMagazine.ammoType != null)
            {
                ExplosionProgressAdd(player, null, projectile.primaryMagazine.ammoType.shortname);
            }
        }
        public string GetLang(string LangKey, string userID = null, params object[] args)
        {
            sb?.Clear();
            if (args != null)
            {
                sb?.AppendFormat(lang.GetMessage(LangKey, this, userID), args);
                return sb?.ToString();
            }
            return lang.GetMessage(LangKey, this, userID);
        }
        
        private void SendConsoleMessage(BasePlayer player, string message)
        {
            if(player != null)
                player.ConsoleMessage(message);
            else
                PrintWarning(message);
        }

        private enum CatType
        {
            Score,
            Killer,
            Time,
            Farm,
            Raid,
            KillerNpc,
            KillerAnimal
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null)
                    LoadDefaultConfig();
                ValidateConfig();
                SaveConfig();
            }
            catch (JsonException ex)
            {
                Debug.LogException(ex);
                LoadDefaultConfig();
            }
        }

        private static readonly string[] SkillTreeGatherHooks =
        {
            "STCanReceiveYield",
            "OnSkillTreeHandleDispenser",
        };
        private void SendImage(BasePlayer player, string imageName, ulong imageId = 0) => ImageLibrary?.Call("SendImage", player, imageName, imageId);

        
        private readonly Regex _avatarRegex = new(@"<avatarFull><!\[CDATA\[(.*)\]\]></avatarFull>", RegexOptions.Compiled);
        private void SaveDataIgnoreList() => Interface.Oxide.DataFileSystem.WriteObject("XDStatistics/IgnorePlayers", _ignoreReservedPlayer);
        private void ButtonHideStat(BasePlayer player, PlayerInfo info)
        {
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "13.865 -8.619", OffsetMax = "160.34 6.226" }
            }, UI_USER_STAT_INFO, "BUTTON_HIDE_STAT");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-120.779 -7.423", OffsetMax = "21.525 9.815" },
                Text = { Text = GetLang("STAT_UI_HIDE_STAT", player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleLeft, Color = "0.8538514 0.8491456 0.8867924 1" }
            }, "BUTTON_HIDE_STAT", "LABEL_HIDE_USER");


            if (!info.HidedStatistics)
            {
                container.Add(new CuiElement
                {
                    Name = "CHECK_BOX_HIDE",
                    Parent = "BUTTON_HIDE_STAT",
                    Components = {
                        new CuiRawImageComponent { Color = "1 1 1 1", Png =  _imageUI.GetImage("4")},
                        new CuiRectTransformComponent { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "1.381 -6.404", OffsetMax = "14.381 6.596" }
                    }
                });
            }
            else
            {
                container.Add(new CuiElement
                {
                    Name = "CHECK_BOX_HIDE",
                    Parent = "BUTTON_HIDE_STAT",
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png =  _imageUI.GetImage("3")},
                    new CuiRectTransformComponent { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "1.381 -6.404", OffsetMax = "14.381 6.596" }
                }
                });
            }
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Button = { Command = "UI_HandlerStat hidestat", Color = "0 0 0 0" },
                Text = { Text = "" }
            }, "BUTTON_HIDE_STAT");

            CuiHelper.DestroyUi(player, "BUTTON_HIDE_STAT");
            CuiHelper.AddUi(player, container);
        }
        private JObject API_GetPlayerPlayedTime(ulong id) => JObject.FromObject(PlayerInfo.Find(id).playedTime);
        private record ItemDisplayName(string En, string Ru);
        private void OnServerInitialized()
        {
            if (!ImageLibrary)
            {
                NextTick(() => {
                    PrintError($"ERROR! Plugin ImageLibrary not found!");
                    Interface.Oxide.UnloadPlugin(Name);
                });
                return;
            }
                        foreach (ItemDefinition itemDef in ItemManager.GetItemDefinitions())
            {
                Item newItem = ItemManager.CreateByName(itemDef.shortname, 1, 0);

                BaseEntity heldEntity = newItem.GetHeldEntity();
                if (heldEntity != null)
                {
                    _prefabID2Item[heldEntity.prefabID] = itemDef.shortname;
                }

                if (itemDef.TryGetComponent(out ItemModDeployable itemModDeployable) && itemModDeployable.entityPrefab != null)
                {
                    string deployablePrefab = itemModDeployable.entityPrefab.resourcePath;

                    if (!string.IsNullOrEmpty(deployablePrefab))
                    {
                        GameObject prefab = GameManager.server.FindPrefab(deployablePrefab);
                        if (prefab != null && prefab.TryGetComponent(out BaseEntity baseEntity))
                        {
                            string shortPrefabName = baseEntity.ShortPrefabName;

                            if (!string.IsNullOrEmpty(shortPrefabName))
                            {
                                _prefabNameItem.TryAdd(shortPrefabName, itemDef.shortname);
                            }
                        }
                    }
                }

                newItem.Remove();
            }
                        
            foreach (BasePlayer player in BasePlayer.activePlayerList)
                OnPlayerConnected(player);

            timer.Once(60f, CheckInMinute);

            if (_config.settings.chatSendTop)
                timer.Once(_config.settings.chatSendTopTime, ChatPrintTopFive);
            if (_config.discordMessage.discordTopFiveUse && !string.IsNullOrEmpty(_config.discordMessage.weebHook))
                timer.Once(_config.discordMessage.discordSendTopTime, DiscordPrintTopFive);
            
            if (_config.settingsInterface.UsebackgroundImageUrl)
                AddImage(_config.settingsInterface.backgroundImageUrl, "CustomBackgroundImage");
                
            _imageUI = new ImageUI();
            _imageUI.DownloadImage();
            
            AddDisplayName();
            foreach (string cmds in _config.settings.chatCommandOpenStat)
                cmd.AddChatCommand(cmds, this, nameof(MainMenuStat));
            cmd.AddConsoleCommand(_config.settings.consoleCommandOpenStat, this, nameof(ConsoleCommandOpenMenu));

                        RegisterPermissionIfNotExists(PermAdmin);
            RegisterPermissionIfNotExists(PermAvailability);
            RegisterPermissionIfNotExists(PermReset);
                        
            timer.Every(_config.settings.dataSaveTime * 60, () => { SaveData(); SaveDataIgnoreList(); SaveDataPrize(); });
        }

        private string CleanUpString(string input)
        {
            return CleanUpRegex.Replace(input, string.Empty);
        }

        private void ValidateConfig()
        {
            if (_config.settingsScore.GatherScore.Count == 0)
            {
                _config.settingsScore.GatherScore = new Dictionary<string, float>
                {
                    ["wood"] = 0.3f,
                    ["stones"] = 0.6f,
                    ["metal.ore"] = 1,
                    ["sulfur.ore"] = 1.5f,
                    ["hq.metal.ore"] = 2,
                };
            }
            if (_config.settingsScore.ExplosionScore.Count == 0)
            {
                _config.settingsScore.ExplosionScore = new Dictionary<string, float>
                {
                    ["explosive.timed"] = 2,
                    ["explosive.satchel"] = 0.7f,
                    ["grenade.beancan"] = 0.3f,
                    ["grenade.f1"] = 0.1f,
                    ["ammo.rocket.basic"] = 1,
                    ["ammo.rocket.hv"] = 0.5f,
                    ["ammo.rocket.fire"] = 0.7f,
                    ["ammo.rifle.explosive"] = 0.02f,
                };
            }
            if (!_config.settingsScore.ExplosionScore.ContainsKey("ammo.rifle.explosive"))
            {
                _config.settingsScore.ExplosionScore.Add("ammo.rifle.explosive", 0.02f);
            }

            if (_config.settingsPrize.prizeFarm.Count == 0)
            {
                if (RU)
                {
                    _config.settingsPrize.prizeFarm = new List<Configuration.SettingsPrize.Prize>
                    {
                        new() {commandPrizeList = new List<string>{ "say %STEAMID%" }, balancePlusMess = "За топ 1 в категории фармер" },
                        new() { top = 2, commandPrizeList = new List<string>{ "say %STEAMID%" }, balancePlusMess = "За топ 2 в категории фармер" },
                    };
                }
                else
                {
                    _config.settingsPrize.prizeFarm = new List<Configuration.SettingsPrize.Prize>
                    {
                        new() {commandPrizeList = new List<string>{ "say %STEAMID%" }, balancePlusMess = "For the top 1 in the farmer category" },
                        new() { top = 2, commandPrizeList = new List<string>{ "say %STEAMID%" }, balancePlusMess = "For the top 2 in the farmer category" },
                    };
                }
                
            }
            if (_config.settingsPrize.prizeKiller.Count == 0)
            {
                if (RU)
                {
                    _config.settingsPrize.prizeKiller = new List<Configuration.SettingsPrize.Prize>
                    {
                        new() {commandPrizeList = new List<string>{ "say %STEAMID%" }, balancePlusMess = "За топ 1 в категории киллер" },
                        new() { top = 2, commandPrizeList = new List<string>{ "say %STEAMID%" }, balancePlusMess = "За топ 2 в категории киллер" },
                    };
                }
                else
                {
                    _config.settingsPrize.prizeKiller = new List<Configuration.SettingsPrize.Prize>
                    {
                        new() {commandPrizeList = new List<string>{ "say %STEAMID%" }, balancePlusMess = "For the top 1 in the killer category" },
                        new() { top = 2, commandPrizeList = new List<string>{ "say %STEAMID%" }, balancePlusMess = "For the top 2 in the killer category" },
                    };
                }
                
            }
            if (_config.settingsPrize.prizeRaid.Count == 0)
            {
                if (RU)
                {
                    _config.settingsPrize.prizeRaid = new List<Configuration.SettingsPrize.Prize>
                    {
                        new() {commandPrizeList = new List<string>{ "say %STEAMID%" }, balancePlusMess = "За топ 1 в категории рейдер" },
                        new() { top = 2, commandPrizeList = new List<string>{ "say %STEAMID%" }, balancePlusMess = "За топ 2 в категории рейдер" },
                    };
                }
                else
                {
                    _config.settingsPrize.prizeRaid = new List<Configuration.SettingsPrize.Prize>
                    {
                        new() {commandPrizeList = new List<string>{ "say %STEAMID%" }, balancePlusMess = "For the top 1 in the raider category" },
                        new() { top = 2, commandPrizeList = new List<string>{ "say %STEAMID%" }, balancePlusMess = "For the top 2 in the raider category" },
                    };
                }
                
            }
            if (_config.settingsPrize.prizeScore.Count == 0)
            {
                if (RU)
                {
                    _config.settingsPrize.prizeScore = new List<Configuration.SettingsPrize.Prize>
                    {
                        new() {commandPrizeList = new List<string>{ "say %STEAMID%" }, balancePlusMess = "За топ 1 в категории больше всего очков" },
                        new() { top = 2, commandPrizeList = new List<string>{ "say %STEAMID%" }, balancePlusMess = "За топ 2 в категории больше всего очков"  },
                    };
                }
                else
                {
                    _config.settingsPrize.prizeScore = new List<Configuration.SettingsPrize.Prize>
                    {
                        new() {commandPrizeList = new List<string>{ "say %STEAMID%" }, balancePlusMess = "For the top 1 in the category the most points" },
                        new() { top = 2, commandPrizeList = new List<string>{ "say %STEAMID%" }, balancePlusMess = "For the top 2 in the category the most points"  },
                    };
                }
               
            }
            if (_config.settingsPrize.prizeTime.Count == 0)
            {
                if (RU)
                {
                    _config.settingsPrize.prizeTime = new List<Configuration.SettingsPrize.Prize>
                    {
                        new() {commandPrizeList = new List<string>{ "say %STEAMID%" }, balancePlusMess = "За топ 1 в категории большой онлайн"  },
                        new() { top = 2, commandPrizeList = new List<string>{ "say %STEAMID%" }, balancePlusMess = "За топ 2 в категории большой онлайн" },
                    };
                }
                else
                {
                    _config.settingsPrize.prizeTime = new List<Configuration.SettingsPrize.Prize>
                    {
                        new() {commandPrizeList = new List<string>{ "say %STEAMID%" }, balancePlusMess = "For the top 1 in the big online category"  },
                        new() { top = 2, commandPrizeList = new List<string>{ "say %STEAMID%" }, balancePlusMess = "For the top 2 in the big online category" },
                    };
                }
                
            }
            if (_config.settingsPrize.prizeNPCKiller.Count == 0)
            {
                if (RU)
                {
                    _config.settingsPrize.prizeNPCKiller = new List<Configuration.SettingsPrize.Prize>
                    {
                        new() {commandPrizeList = new List<string>{ "say %STEAMID%" }, balancePlusMess = "За топ 1 в категории убийца NPC" },
                        new() { top = 2, commandPrizeList = new List<string>{ "say %STEAMID%" }, balancePlusMess = "За топ 2 в категории убийца NPC" },
                    };
                }
                else
                {
                    _config.settingsPrize.prizeNPCKiller = new List<Configuration.SettingsPrize.Prize>
                    {
                        new() {commandPrizeList = new List<string>{ "say %STEAMID%" }, balancePlusMess = "For the top 1 in the NPC killer category" },
                        new() { top = 2, commandPrizeList = new List<string>{ "say %STEAMID%" }, balancePlusMess = "For the top 2 in the NPC killer category" },
                    };
                }
                
            }
            if (_config.settingsPrize.prizeAnimalKiller.Count == 0)
            {
                if (RU)
                {
                    _config.settingsPrize.prizeAnimalKiller = new List<Configuration.SettingsPrize.Prize>
                    {
                        new() {commandPrizeList = new List<string>{ "say %STEAMID%" }, balancePlusMess = "За топ 1 в категории убийца NPC" },
                        new() { top = 2, commandPrizeList = new List<string>{ "say %STEAMID%" }, balancePlusMess = "За топ 2 в категории убийца NPC" },
                    };
                }
                else
                {
                    _config.settingsPrize.prizeAnimalKiller = new List<Configuration.SettingsPrize.Prize>
                    {
                        new() {commandPrizeList = new List<string>{ "say %STEAMID%" }, balancePlusMess = "For the top 1 in the NPC killer category" },
                        new() { top = 2, commandPrizeList = new List<string>{ "say %STEAMID%" }, balancePlusMess = "For the top 2 in the NPC killer category" },
                    };
                }
               
            }
        }

        
        private class DiscordMessage
        {
            public DiscordMessage(string content, params Embed[] embeds)
            {
                Content = content;
                Embeds = embeds.ToList();
            }

            [JsonProperty("content")] public string Content { get; set; }
            [JsonProperty("embeds")] public List<Embed> Embeds { get; set; }

            public string ToJson()
            {
                return JsonConvert.SerializeObject(this);
            }
        }

        private void DialogConfirmationDropStat(BasePlayer player)
        {
            var container = new CuiElementContainer();
            container.Add(new CuiElement
            {
                Name = "CONFIRMATIONS",
                Parent = UI_USER_STAT_INFO,
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png =  _imageUI.GetImage("6") },
                    new CuiRectTransformComponent {AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "13.64 -104.387", OffsetMax = "160.12 -45.246" }
                }
            });
		   		 		  						  	   		  	 	 		  	  			  			 		  				
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-73.24 -30", OffsetMax = "73.24 -0.43" },
                Text = { Text = GetLang("STAT_UI_CONFIRM", player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
            }, "CONFIRMATIONS");

            container.Add(new CuiElement
            {
                Name = "CONFIRMATIONS_YES",
                Parent = "CONFIRMATIONS",
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png =  _imageUI.GetImage("5")},
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "11.28 6.7", OffsetMax = "46.28 23.7" }
                }
            });

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-22.5 -11", OffsetMax = "22.5 11" },
                Button = { Command = "UI_HandlerStat confirm_yes", Color = "0 0 0 0" },
                Text = { Text = GetLang("STAT_UI_CONFIRM_YES", player.UserIDString), Font = "robotocondensed-bold.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" }
            }, "CONFIRMATIONS_YES", "LABEL_YES");

            container.Add(new CuiElement
            {
                Name = "CONFIRMATIONS_NO",
                Parent = "CONFIRMATIONS",
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png =  _imageUI.GetImage("5")},
                    new CuiRectTransformComponent { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-46.8 6.7", OffsetMax = "-11.8 23.7" }
                }
            });

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-22.5 -11", OffsetMax = "22.5 11" },
                Button = { Close = "CONFIRMATIONS", Color = "0 0 0 0" },
                Text = { Text = GetLang("STAT_UI_CONFIRM_NO", player.UserIDString), Font = "robotocondensed-bold.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" }
            }, "CONFIRMATIONS_NO", "LABEL_NO");

            CuiHelper.DestroyUi(player, "CONFIRMATIONS");
            CuiHelper.AddUi(player, container);
        }
        public const string UI_MENU_BUTTON = "MENU_BUTTON";

        
                private const bool RU = true;
        private Dictionary<uint, string> _prefabID2Item = new();
        private static readonly Regex CleanUpRegex = new Regex("<.*?>|{.*?}", RegexOptions.Compiled);
        
        private void CanCombineDroppedItem(DroppedItem droppedItem, DroppedItem di)
        {
            if (droppedItem.item.HasFlag(global::Item.Flag.Placeholder))
            {
                droppedItem.item.SetFlag(global::Item.Flag.Placeholder, false);
                di.item.SetFlag(global::Item.Flag.Placeholder, false);
            }
        }
        
        public static string CleanString(string str, string sub = "")
        {
            if (str == null) return null;

            StringBuilder sb = null;
            for (int i = 0; i < str.Length; i++)
            {
                char ch = str[i];
                if (char.IsSurrogate(ch))
                {
                    if (sb == null)
                        sb = new StringBuilder(str, 0, i, str.Length);
                    sb.Append(sub);
                    if (i + 1 < str.Length && char.IsHighSurrogate(ch) && char.IsLowSurrogate(str[i + 1]))
                        i++;
                }
                else if (sb != null)
                    sb.Append(ch);
            }
            return sb == null ? str : sb.ToString();
        }
		   		 		  						  	   		  	 	 		  	  			  			 		  				
        private void ChatPrintTopFive()
        {
            (string, string) data = GetRandomTopPlayer();

            foreach (BasePlayer item in BasePlayer.activePlayerList)
            {
                item.ChatMessage(GetLang(data.Item1, item.UserIDString, data.Item2));
            }
            timer.Once(_config.settings.chatSendTopTime, ChatPrintTopFive);
        }

        
                private Configuration _config;
                
                
        private void TopTen(BasePlayer player)
        {
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "1 1 1 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-640 -360.001", OffsetMax = "640 266.939" }
            }, UI_INTERFACE, UI_TOP_TEN_USER);
		   		 		  						  	   		  	 	 		  	  			  			 		  				
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-599.3 -237.224", OffsetMax = "589.334 271.441" }
            }, UI_TOP_TEN_USER, "TOP_10_TABLE");
            
            if (_config.settings.pveServerMode)
            {
                                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "9.2 -20.655", OffsetMax = "176.343 -3.545" },
                    Text = { Text = GetLang("STAT_UI_CATEGORY_TOP_KILLER_ANIMALS", player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 11, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" }
                }, "TOP_10_TABLE");

                container.Add(new CuiElement
                {
                    Name = "TOP_TABLE_0",
                    Parent = "TOP_10_TABLE",
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 0.3",  Png =  _imageUI.GetImage("20") },
                    new CuiRectTransformComponent { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "9.575 -441.751", OffsetMax = "177.214 -26.861" }
                }
                });
                
                IEnumerable<KeyValuePair<ulong, PlayerInfo>> animalKillerList = ProcessPlayerList(x =>  x.otherStat.AnimalsKill);
                
                int y = 0;

                foreach (KeyValuePair<ulong, PlayerInfo> item in animalKillerList)
                {
                    (string LockStatus, string Command, string NickName, string Color) processedItem = ProcessItem(item, player, y);

                    container.Add(new CuiPanel
                    {
                        CursorEnabled = false,
                        Image = { Color = processedItem.Color },
                        RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"-83.82 {177.277 - (y * 42.863)}", OffsetMax = $"83.82 {207.448 - (y * 42.863)}" }
                    }, "TOP_TABLE_0", "USER_INFO");
                    container.Add(new CuiElement
                    {
                        Name = "USER_STAT_HIDE",
                        Parent = "USER_INFO",
                        Components =
                        {
                            new CuiImageComponent { Color = "1 1 1 1", Sprite = processedItem.LockStatus },
                            new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-80.2 -6.5", OffsetMax = "-67.2 6.5" }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = "USER_INFO",
                        Components =
                        {
                            new CuiTextComponent { Text = processedItem.NickName, Font = "robotocondensed-regular.ttf", FontSize = 9, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                            new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-64.822 -6.724", OffsetMax = "7.522 6.724" }
                        }
                    });

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "7.522 -6.724", OffsetMax = "76.934 6.724" },
                        Text = { Text = item.Value.otherStat.AnimalsKill.ToString(), Font = "robotocondensed-regular.ttf", FontSize = 9, Align = TextAnchor.MiddleRight, Color = "1 1 1 1" }
                    }, "USER_INFO");

                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Button = { Command = processedItem.Command, Color = "0 0 0 0" },
                        Text = { Text = "" }
                    }, "USER_INFO");
                    y++;
                }

                            }
            else
            {
                
                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "9.2 -20.655", OffsetMax = "176.343 -3.545" },
                    Text = { Text = GetLang("STAT_UI_CATEGORY_TOP_KILLER", player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 11, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" }
                }, "TOP_10_TABLE");

                container.Add(new CuiElement
                {
                    Name = "TOP_TABLE_0",
                    Parent = "TOP_10_TABLE",
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 0.3",  Png =  _imageUI.GetImage("20") },
                    new CuiRectTransformComponent { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "9.575 -441.751", OffsetMax = "177.214 -26.861" }
                }
                });

                IEnumerable<KeyValuePair<ulong, PlayerInfo>> killerList = ProcessPlayerList(x => x.pVP.Kills);

                int y = 0;

                foreach (KeyValuePair<ulong, PlayerInfo> item in killerList)
                {
                    (string LockStatus, string Command, string NickName, string Color) processedItem = ProcessItem(item, player, y);

                    container.Add(new CuiPanel
                    {
                        CursorEnabled = false,
                        Image = { Color = processedItem.Color },
                        RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"-83.82 {177.277 - (y * 42.863)}", OffsetMax = $"83.82 {207.448 - (y * 42.863)}" }
                    }, "TOP_TABLE_0", "USER_INFO");
                    container.Add(new CuiElement
                    {
                        Name = "USER_STAT_HIDE",
                        Parent = "USER_INFO",
                        Components =
                        {
                            new CuiImageComponent { Color = "1 1 1 1", Sprite = processedItem.LockStatus },
                            new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-80.2 -6.5", OffsetMax = "-67.2 6.5" }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = "USER_INFO",
                        Components =
                        {
                            new CuiTextComponent { Text = processedItem.NickName, Font = "robotocondensed-regular.ttf", FontSize = 9, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                            new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-64.822 -6.724", OffsetMax = "7.522 6.724" }
                        }
                    });

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "7.522 -6.724", OffsetMax = "76.934 6.724" },
                        Text = { Text = item.Value.pVP.Kills.ToString(), Font = "robotocondensed-regular.ttf", FontSize = 9, Align = TextAnchor.MiddleRight, Color = "1 1 1 1" }
                    }, "USER_INFO");

                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Button = { Command = processedItem.Command, Color = "0 0 0 0" },
                        Text = { Text = "" }
                    }, "USER_INFO");
                    y++;
                }

                            }

                        container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "209.129 -20.655", OffsetMax = "376.271 -3.545" },
                Text = { Text = GetLang("STAT_UI_CATEGORY_TOP_NPCKILLER", player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 11, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" }
            }, "TOP_10_TABLE");

            container.Add(new CuiElement
            {
                Name = "TOP_TABLE_1",
                Parent = "TOP_10_TABLE",
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 0.3", Png =  _imageUI.GetImage("20") },
                    new CuiRectTransformComponent { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "210.281 -441.751", OffsetMax = "377.919 -26.861" }
                }
            });
            IEnumerable<KeyValuePair<ulong, PlayerInfo>> killerNpcList = ProcessPlayerList(x =>  x.pVP.KillsNpc);
            
            int i = 0;
            foreach (KeyValuePair<ulong, PlayerInfo> item in killerNpcList)
            {
                (string LockStatus, string Command, string NickName, string Color) processedItem = ProcessItem(item, player, i);

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = processedItem.Color },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"-83.82 {177.277 - (i * 42.863)}", OffsetMax = $"83.82 {207.448 - (i * 42.863)}" }
                }, "TOP_TABLE_1", "USER_INFO");

                container.Add(new CuiElement
                {
                    Name = "USER_STAT_HIDE",
                    Parent = "USER_INFO",
                    Components =
                    {
                        new CuiImageComponent { Color = "1 1 1 1", Sprite = processedItem.LockStatus },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-80.2 -6.5", OffsetMax = "-67.2 6.5" }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = "USER_INFO",
                    Components =
                    {
                        new CuiTextComponent { Text = processedItem.NickName, Font = "robotocondensed-regular.ttf", FontSize = 9, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-64.822 -6.724", OffsetMax = "7.522 6.724" }
                    }
                });

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "7.522 -6.724", OffsetMax = "76.934 6.724" },
                    Text = { Text = item.Value.pVP.KillsNpc.ToString(), Font = "robotocondensed-regular.ttf", FontSize = 9, Align = TextAnchor.MiddleRight, Color = "1 1 1 1" }
                }, "USER_INFO");
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { Command = processedItem.Command, Color = "0 0 0 0" },
                    Text = { Text = "" }
                }, "USER_INFO");
                i++;
            }

            
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "409.229 -20.655", OffsetMax = "576.371 -3.545" },
                Text = { Text = GetLang("STAT_UI_CATEGORY_TOP_TIME", player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 11, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" }
            }, "TOP_10_TABLE");

            container.Add(new CuiElement
            {
                Name = "TOP_TABLE_2",
                Parent = "TOP_10_TABLE",
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 0.3", Png =  _imageUI.GetImage("20") },
                    new CuiRectTransformComponent { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "409.231 -441.751", OffsetMax = "576.869 -26.861" }
                }
            });
            IEnumerable<KeyValuePair<ulong, PlayerInfo>> timeList = ProcessPlayerList(x =>  x.playedTime.PlayedForWipe);
            
            int c = 0;
            foreach (KeyValuePair<ulong, PlayerInfo> item in timeList)
            {
                (string LockStatus, string Command, string NickName, string Color) processedItem = ProcessItem(item, player, c);
		   		 		  						  	   		  	 	 		  	  			  			 		  				
                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = processedItem.Color },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"-83.82 {177.277 - (c * 42.863)}", OffsetMax = $"83.82 {207.448 - (c * 42.863)}" }
                }, "TOP_TABLE_2", "USER_INFO");
                container.Add(new CuiElement
                {
                    Name = "USER_STAT_HIDE",
                    Parent = "USER_INFO",
                    Components =
                    {
                        new CuiImageComponent { Color = "1 1 1 1", Sprite = processedItem.LockStatus },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-80.2 -6.5", OffsetMax = "-67.2 6.5" }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = "USER_INFO",
                    Components =
                    {
                        new CuiTextComponent { Text = processedItem.NickName, Font = "robotocondensed-regular.ttf", FontSize = 9, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-64.822 -6.724", OffsetMax = "7.522 6.724" }
                    }
                });

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "7.522 -6.724", OffsetMax = "76.934 6.724" },
                    Text =
                    {
                        Text = TimeHelper.FormatTime(TimeSpan.FromMinutes(item.Value.playedTime.PlayedForWipe), 5, lang.GetLanguage(player.UserIDString)),
                        Font = "robotocondensed-regular.ttf", FontSize = 9, Align = TextAnchor.MiddleRight, Color = "1 1 1 1"
                    }
                }, "USER_INFO");
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { Command = processedItem.Command, Color = "0 0 0 0" },
                    Text = { Text = "" }
                }, "USER_INFO");
                c++;
            }
		   		 		  						  	   		  	 	 		  	  			  			 		  				
            
                        container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-580.571 -20.655", OffsetMax = "-413.429 -3.545" },
                Text = { Text = GetLang("STAT_UI_CATEGORY_TOP_GATHER", player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 11, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" }
            }, "TOP_10_TABLE");

            container.Add(new CuiElement
            {
                Name = "TOP_TABLE_3",
                Parent = "TOP_10_TABLE",
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 0.3", Png =  _imageUI.GetImage("20") },
                    new CuiRectTransformComponent { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-580.099 -441.751", OffsetMax = "-412.461 -26.861" }
                }
            });
            IEnumerable<KeyValuePair<ulong, PlayerInfo>> farmList = ProcessPlayerList(x =>  x.gather.AllGathered);

            int f = 0;
            foreach (KeyValuePair<ulong, PlayerInfo> item in farmList)
            {
                (string LockStatus, string Command, string NickName, string Color) processedItem = ProcessItem(item, player, f);

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = processedItem.Color },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"-83.82 {177.277 - (f * 42.863)}", OffsetMax = $"83.82 {207.448 - (f * 42.863)}" }
                }, "TOP_TABLE_3", "USER_INFO");

                container.Add(new CuiElement
                {
                    Name = "USER_STAT_HIDE",
                    Parent = "USER_INFO",
                    Components =
                    {
                        new CuiImageComponent { Color = "1 1 1 1", Sprite = processedItem.LockStatus },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-80.2 -6.5", OffsetMax = "-67.2 6.5" }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = "USER_INFO",
                    Components =
                    {
                        new CuiTextComponent { Text = processedItem.NickName, Font = "robotocondensed-regular.ttf", FontSize = 9, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-64.822 -6.724", OffsetMax = "7.522 6.724" }
                    }
                });

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "7.522 -6.724", OffsetMax = "76.934 6.724" },
                    Text =
                    {
                        Text = item.Value.gather.AllGathered.ToString("0,0", CultureInfo.InvariantCulture), Font = "robotocondensed-regular.ttf", FontSize = 9,
                        Align = TextAnchor.MiddleRight, Color = "1 1 1 1"
                    }
                }, "USER_INFO");
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { Command = processedItem.Command, Color = "0 0 0 0" },
                    Text = { Text = "" }
                }, "USER_INFO");
                f++;
            }

            
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-381.151 -20.655", OffsetMax = "-214.009 -3.545" },
                Text = { Text = GetLang("STAT_UI_CATEGORY_TOP_EXPLOSED", player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 11, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" }
            }, "TOP_10_TABLE");


            container.Add(new CuiElement
            {
                Name = "TOP_TABLE_5",
                Parent = "TOP_10_TABLE",
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 0.3", Png =  _imageUI.GetImage("20") },
                    new CuiRectTransformComponent { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-380.679 -441.751", OffsetMax = "-213.041 -26.861"  }
                }
            });
            
            IEnumerable<KeyValuePair<ulong, PlayerInfo>> expList = ProcessPlayerList(x =>  x.explosion.AllExplosionUsed);

            int z = 0;
            foreach (KeyValuePair<ulong, PlayerInfo> item in expList)
            {
                (string LockStatus, string Command, string NickName, string Color) processedItem = ProcessItem(item, player, z);

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = processedItem.Color },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"-83.82 {177.277 - (z * 42.863)}", OffsetMax = $"83.82 {207.448 - (z * 42.863)}" }
                }, "TOP_TABLE_5", "USER_INFO");

                container.Add(new CuiElement
                {
                    Name = "USER_STAT_HIDE",
                    Parent = "USER_INFO",
                    Components = {
                        new CuiImageComponent { Color = "1 1 1 1", Sprite = processedItem.LockStatus },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-80.2 -6.5", OffsetMax = "-67.2 6.5" }
                    }
                });
                
                container.Add(new CuiElement
                {
                    Parent = "USER_INFO",
                    Components = {
                        new CuiTextComponent {Text = processedItem.NickName, Font = "robotocondensed-regular.ttf", FontSize = 9, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1"},
                        new CuiRectTransformComponent {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-64.822 -6.724", OffsetMax = "7.522 6.724" }
                    }
                });

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "7.522 -6.724", OffsetMax = "76.934 6.724" },
                    Text = { Text = item.Value.explosion.AllExplosionUsed.ToString(), Font = "robotocondensed-regular.ttf", FontSize = 9, Align = TextAnchor.MiddleRight, Color = "1 1 1 1" }
                }, "USER_INFO");
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { Command = processedItem.Command, Color = "0 0 0 0" },
                    Text = { Text = "" }
                }, "USER_INFO");
                z++;
            }
            
                        container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-180.871 -20.655", OffsetMax = "-13.729 -3.545" },
                Text = { Text = GetLang("STAT_UI_CATEGORY_TOP_SCORE", player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 11, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" }
            }, "TOP_10_TABLE");

            container.Add(new CuiElement
            {
                Name = "TOP_TABLE_4",
                Parent = "TOP_10_TABLE",
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 0.3",  Png = _imageUI.GetImage("20") },
                    new CuiRectTransformComponent { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-180.419 -441.751", OffsetMax = "-12.781 -26.861"}
                }
            });
            IEnumerable<KeyValuePair<ulong, PlayerInfo>> scoreList = ProcessPlayerList(x =>  (int)x.Score);

            int s = 0;
            foreach (KeyValuePair<ulong, PlayerInfo> item in scoreList)
            {
                (string LockStatus, string Command, string NickName, string Color) processedItem = ProcessItem(item, player, s);
                
                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = processedItem.Color },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"-83.82 {177.277 - (s * 42.863)}", OffsetMax = $"83.82 {207.448 - (s * 42.863)}" }
                }, "TOP_TABLE_4", "USER_INFO");

                container.Add(new CuiElement
                {
                    Name = "USER_STAT_HIDE",
                    Parent = "USER_INFO",
                    Components = {
                        new CuiImageComponent { Color = "1 1 1 1", Sprite = processedItem.LockStatus },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-80.2 -6.5", OffsetMax = "-67.2 6.5" }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = "USER_INFO",
                    Components = {
                        new CuiTextComponent {Text = processedItem.NickName, Font = "robotocondensed-regular.ttf", FontSize = 9, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1"},
                        new CuiRectTransformComponent {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-64.822 -6.724", OffsetMax = "7.522 6.724" }
                    }
                });

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "7.522 -6.724", OffsetMax = "76.934 6.724" },
                    Text = { Text = item.Value.Score.ToString("0.00"), Font = "robotocondensed-regular.ttf", FontSize = 9, Align = TextAnchor.MiddleRight, Color = "1 1 1 1" }
                }, "USER_INFO");
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { Command = processedItem.Command, Color = "0 0 0 0" },
                    Text = { Text = "" }
                }, "USER_INFO");
                s++;
            }
            
            CloseLayer(player);
            CuiHelper.AddUi(player, container);
            return;

            (string LockStatus, string Command, string NickName, string Color) ProcessItem(KeyValuePair<ulong, PlayerInfo> item, BasePlayer player, int rank)
            {
                string lockStatus = item.Value.HidedStatistics ? "assets/icons/lock.png" : "assets/icons/unlock.png";
                string command = item.Value.HidedStatistics ? "" : $"UI_HandlerStat GoStatPlayers {item.Key}";
                if (permission.UserHasPermission(player.UserIDString, PermAdmin))
                    command = $"UI_HandlerStat GoStatPlayers {item.Key}";
                string nickName = $"<color=white>{GetCorrectName(item.Value.Name, 17)}</color>";
                string color = rank switch
                {
                    0 => _config.settingsInterface.ColorTop1,
                    1 => _config.settingsInterface.ColorTop2,
                    2 => _config.settingsInterface.ColorTop3,
                    _ => "0 0 0 0"
                };
    
                return (lockStatus, command, nickName, color);
            }

            IEnumerable<KeyValuePair<ulong, PlayerInfo>> ProcessPlayerList(Func<PlayerInfo, int> orderFunc)
            {
                return _players
                    .Where(x => !_config.settings.ignoreList.Contains(x.Key))
                    .OrderByDescending(x => orderFunc(x.Value))
                    .Take(10);
            }
        }
        void OnItemPickup(Item item, BasePlayer player)
        {
            if (item == null || !item.HasFlag(global::Item.Flag.Placeholder))
                return;
            if (item.info.shortname == "scrap")
            {
                ProgressAdd(player, item.info.shortname, item.amount);
                item.SetFlag(global::Item.Flag.Placeholder, false);
            }
        }

        private  void OnServerShutdown() => Unload();
        private List<int> _alowedSeedId = new(){ 1548091822, 1771755747, 1112162468, 1367190888,-1962971928, -2086926071, 44433072, -567909622, 1272194103, 854447607,1660145984, 1783512007, -858312878 };
        
        private bool IsDuel(ulong userID)
        {
            object playerId = ObjectCache.Get(userID);
            BasePlayer player = (Duel is not null || Duelist is not null) ? BasePlayer.FindByID(userID) : null;

            return EventAtEvent(playerId) 
                   || IsPlayerOnBattle(playerId) 
                   || IsPlayerOnActiveDuel(player)
                   || InEvent(player) 
                   || IsOnTournament(playerId);
		   		 		  						  	   		  	 	 		  	  			  			 		  				
            bool EventAtEvent(object id) => EventHelper?.Call("EMAtEvent", id) is true;
    
            bool IsPlayerOnBattle(object id) => Battles?.Call<bool>("IsPlayerOnBattle", id) == true;
    
            bool IsPlayerOnActiveDuel(BasePlayer pl) => Duel?.Call<bool>("IsPlayerOnActiveDuel", pl) == true;
    
            bool InEvent(BasePlayer pl) => Duelist?.Call<bool>("inEvent", pl) == true;
    
            bool IsOnTournament(object id) => ArenaTournament?.Call<bool>("IsOnTournament", id) == true;
        }

        private void OnZLevelGrowableGathered(GrowableEntity ge, Item item, BasePlayer player, int prevAmount, int newAmount)
        {
            if (player == null || item == null || item.info == null) return;

            if (_alowedSeedId.Contains(item.info.itemid))
            {
                NextTick(() =>
                {
                    PlayerInfo playerStat = PlayerInfo.Find(player.userID.Get());

                    if (!playerStat.harvesting.HarvestingList.TryGetValue(item.info.shortname, out int currentAmount))
                    {
                        playerStat.harvesting.HarvestingList[item.info.shortname] = item.amount;
                    }
                    else
                    {
                        playerStat.harvesting.HarvestingList[item.info.shortname] = currentAmount + item.amount;
                    }

                    playerStat.harvesting.AllHarvesting += item.amount;
                    playerStat.Score += _config.settingsScore.PlantScore;
                });
            }
        }

        private IEnumerator ParseTopUserForPrize()
        {
            _prizePlayerData.Clear();

            var categoryDetails = new[]
            {
                new { 
                    Cat = CatType.Score,
                    _config.settingsPrize.prizeScore.Count, 
                    PrizeSettings = _config.settingsPrize.prizeScore, 
                    Selector = (Func<PlayerInfo, int>)(p => (int)p.Score) 
                },
                new { 
                    Cat = CatType.Killer,
                    _config.settingsPrize.prizeKiller.Count, 
                    PrizeSettings = _config.settingsPrize.prizeKiller, 
                    Selector = (Func<PlayerInfo, int>)(p => p.pVP.Kills) 
                },
                new { 
                    Cat = CatType.Time,
                    _config.settingsPrize.prizeTime.Count, 
                    PrizeSettings = _config.settingsPrize.prizeTime, 
                    Selector = (Func<PlayerInfo, int>)(p => p.playedTime.PlayedForWipe) 
                },
                new { 
                    Cat = CatType.Farm,
                    _config.settingsPrize.prizeFarm.Count, 
                    PrizeSettings = _config.settingsPrize.prizeFarm, 
                    Selector = (Func<PlayerInfo, int>)(p => p.gather.AllGathered) 
                },
                new { 
                    Cat = CatType.Raid,
                    _config.settingsPrize.prizeRaid.Count, 
                    PrizeSettings = _config.settingsPrize.prizeRaid, 
                    Selector = (Func<PlayerInfo, int>)(p => p.explosion.AllExplosionUsed) 
                },
                new { 
                    Cat = CatType.KillerNpc,
                    _config.settingsPrize.prizeNPCKiller.Count, 
                    PrizeSettings = _config.settingsPrize.prizeNPCKiller, 
                    Selector = (Func<PlayerInfo, int>)(p => p.pVP.KillsNpc) 
                },
                new { 
                    Cat = CatType.KillerAnimal,
                    _config.settingsPrize.prizeAnimalKiller.Count, 
                    PrizeSettings = _config.settingsPrize.prizeAnimalKiller, 
                    Selector = (Func<PlayerInfo, int>)(p => p.otherStat.AnimalsKill) 
                }
            };


            foreach (var detail in categoryDetails)
            {
                if (detail.Count == 0) continue;
		   		 		  						  	   		  	 	 		  	  			  			 		  				
                IEnumerable<KeyValuePair<ulong, PlayerInfo>> players = _players
                    .XDWhere(p => !_config.settings.ignoreList.Contains(p.Key))
                    .OrderByDescending(p => detail.Selector(p.Value))
                    .Take(detail.Count);
                

                int top = 1;
                foreach (KeyValuePair<ulong, PlayerInfo> item in players)
                {
                    if (!_prizePlayerData.ContainsKey(item.Key))
                    {
                        _prizePlayerData[item.Key] = new List<PrizePlayer>();
                    }

                    _prizePlayerData[item.Key].Add(new PrizePlayer 
                    {
                        catType = detail.Cat,
                        Name = item.Value.Name,
                        value = detail.Selector(item.Value),
                        top = top
                    });

                    detail.PrizeSettings.FirstOrDefault(x => x.top == top)?.GiftPrizePlayer(item.Key.ToString());
                    top++;
                    yield return CoroutineEx.waitForSeconds(0.02f);
                }
            }

            RewardPlayerCoroutine = null;
            SaveDataPrize();
            if (_config.settings.wipeData)
            {
                PlayerInfo.ClearDataWipe();
                NextTick(() => {
                    SaveData();
                    SaveDataIgnoreList();
                });
                PrintWarning(GetLang("STAT_PRINT_WIPE"));
            }
        }
        private static XDStatistics Instance;
        private bool AddImage(string url, string shortname, ulong skin = 0) => (bool)ImageLibrary?.Call("AddImage", url, shortname, skin);
        
        private static Dictionary<BasePlayer, List<UInt64>> _lootersListCarte = new();
        public const string UI_USER_STAT_INFO = "USER_STAT_INFO";
        private int? API_GetGathered(ulong id, string shortname)
        {
            Dictionary<string, int> data = API_GetGathered(id);
            int amount;
            if (data?.TryGetValue(shortname, out amount) == true)
                return amount;
            return null;
        }
        private void MenuButton(BasePlayer player, int page = 0)
        {
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "1 1 1 0" },
                RectTransform = { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-132.238 -75.531", OffsetMax = "122.197 -48.564" }
            }, UI_INTERFACE, UI_MENU_BUTTON);
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "-0.001 -13.483", OffsetMax = "88.113 13.484" },
                Button = { Command = $"UI_HandlerStat Page_swap 0", Color = "0 0 0 0" },
                Text = { Text = GetLang("STAT_UI_MY_STAT", player.UserIDString), FontSize = 10, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter }
            }, UI_MENU_BUTTON, "BUTTON_MY_STAT");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = page == 0 ? "0.2988604 0.6886792 0.120194 0.6431373" : "1 1 1 0.3" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-44.057 -13.483", OffsetMax = "44.057 -11.642" }
            }, "BUTTON_MY_STAT", "Panel_8193");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-39.106 -13.483", OffsetMax = "65.745 13.484" },
                Button = { Command = $"UI_HandlerStat Page_swap 1", Color = "0 0 0 0" },
                Text = { Text = GetLang("STAT_UI_TOP_TEN", player.UserIDString), FontSize = 10, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter }
            }, UI_MENU_BUTTON, "BUTTON_TOPTEN_USER");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = page == 1 ? "0.2988604 0.6886792 0.120194 0.64313732" : "1 1 1 0.3" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-52.425 -13.484", OffsetMax = "52.425 -11.642" }
            }, "BUTTON_TOPTEN_USER", "Panel_8193");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-61.472 -13.483", OffsetMax = "0.216 13.484" }
            }, UI_MENU_BUTTON, "BUTTON_PAGE_SEARCH");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = page == 2 ? "0.2988604 0.6886792 0.120194 0.6431373" : "1 1 1 0.3" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.844 -13.484", OffsetMax = "30.845 -11.642" }
            }, "BUTTON_PAGE_SEARCH");
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-30.844 -8.36", OffsetMax = "12.862 8.361" },
                Text = { Text = GetLang("STAT_UI_SEARCH", player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
            }, "BUTTON_PAGE_SEARCH");

            container.Add(new CuiElement
            {
                Parent = "BUTTON_PAGE_SEARCH",
                Components = {
                    new CuiImageComponent { Color = "1 1 1 1", Sprite = "assets/icons/examine.png" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "12.862 -6.5", OffsetMax = "25.862 6.5" }
                }
            });
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Button = { Command = $"UI_HandlerStat Page_swap 2", Color = "0 0 0 0" },
                Text = { Text = "" }
            }, "BUTTON_PAGE_SEARCH");

            CuiHelper.DestroyUi(player, UI_MENU_BUTTON);
            CuiHelper.AddUi(player, container);
            if (page == 0)
                UserStat(player);
            else if (page == 1)
                TopTen(player);
            else
                SearchPageUser(player);
        }

        private void OnZLevelCollectiblePickup(ItemAmount ia, BasePlayer player, CollectibleEntity ce, int prevAmount, float newAmount)
        {
            if (player == null || ia == null || ia.itemDef == null) return;
            
            NextTick(() =>
            {
                int itemId = ia.itemDef.itemid;
                int itemAmount = (int)ia.amount;
                string itemShortName = ia.itemDef.shortname;

                if (_alowedSeedId.Contains(itemId))
                {
                    PlayerInfo playerstat = PlayerInfo.Find(player.userID.Get());
		   		 		  						  	   		  	 	 		  	  			  			 		  				
                    if (playerstat.harvesting.HarvestingList.TryGetValue(itemShortName, out int currentAmount))
                    {
                        playerstat.harvesting.HarvestingList[itemShortName] = currentAmount + itemAmount;
                    }
                    else
                    {
                        playerstat.harvesting.HarvestingList.Add(itemShortName, itemAmount);
                    }

                    playerstat.harvesting.AllHarvesting += itemAmount;
                    playerstat.Score += _config.settingsScore.PlantScore;
                }
                else
                {
                    ProgressAdd(player, itemShortName, itemAmount, true);
                }
            });
        }
        private void LoadDataIgnoreList() => _ignoreReservedPlayer = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerInfo>>("XDStatistics/IgnorePlayers");
        private void OnNewSave()
        {
            if (_config.settingsPrize.prizeUse)
            {
                RewardPlayerCoroutine = ServerMgr.Instance.StartCoroutine(ParseTopUserForPrize());
            }
            else if (_config.settings.wipeData)
            {
                PlayerInfo.ClearDataWipe();
                NextTick(() => {
                    SaveData();
                    SaveDataIgnoreList();
                });
                PrintWarning(GetLang("STAT_PRINT_WIPE"));
            }
        }

        private void ProgressAdd(BasePlayer player, string shortname, int count, bool scoreGive = false)
        {
            PlayerInfo playerstat = PlayerInfo.Find(player.userID.Get());
            if (_config.settingsScore.blackListed.Contains(shortname))
                return;

            Dictionary<string, string> mappings = new Dictionary<string, string>
            {
                { "stones", "stones" },
                { "wood", "wood" },
                { "metal.ore", "metal.ore" },
                { "metal.fragments", "metal.ore" },
                { "sulfur.ore", "sulfur.ore" },
                { "sulfur", "sulfur.ore" },
                { "hq.metal.ore", "hq.metal.ore" },
                { "metal.refined", "hq.metal.ore" },
                { "scrap", "scrap" }
            };
		   		 		  						  	   		  	 	 		  	  			  			 		  				
            if (!mappings.TryGetValue(shortname, out string mappedName)) return;
            playerstat.gather.GatheredTotal[mappedName] += count;
            playerstat.gather.AllGathered += count;

            if (!scoreGive) return;
            if (mappedName == "scrap")
            {
                playerstat.Score += _config.settingsScore.ScrapScore;
            }
            else
            {
                if (_config.settingsScore.GatherScore.TryGetValue(mappedName, out float scoreValue))
                {
                    playerstat.Score += scoreValue;
                }
            }
        }

        private void OtherStatUser(BasePlayer player, ulong target = 0, int statType = 0)
        {
            var container = new CuiElementContainer();
            PlayerInfo statInfo = PlayerInfo.Find(target == 0 ? player.userID.Get() : target);
            if (statType == 0)
            {
                                container.Add(new CuiElement
                {
                    Name = "CRATE_STAT",
                    Parent = UI_USER_STAT_INFO,
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png =  _imageUI.GetImage("8") },
                    new CuiRectTransformComponent { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-210.146 181.391", OffsetMax = "-13.974 227.429" }
                }
                });

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "48.359 -7.014", OffsetMax = "176.509 8.414" },
                    Text = { Text = GetLang("STAT_UI_CRATE_OPEN", player.UserIDString, statInfo.otherStat.CrateOpen), Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
                }, "CRATE_STAT");

                container.Add(new CuiElement
                {
                    Parent = "CRATE_STAT",
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png =  _imageUI.GetImage("13") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-88.5 -16.5", OffsetMax = "-55.5 16.5" }
                }
                });
                
                                container.Add(new CuiElement
                {
                    Name = "BARREL_STAT",
                    Parent = UI_USER_STAT_INFO,
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png =  _imageUI.GetImage("8") },
                    new CuiRectTransformComponent { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-210.146 129.171", OffsetMax = "-13.974 175.209" }
                }
                });

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "48.359 -7.014", OffsetMax = "176.509 8.414" },
                    Text = { Text = GetLang("STAT_UI_BARREL_KILL", player.UserIDString, statInfo.otherStat.BarrelDeath), Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
                }, "BARREL_STAT");

                container.Add(new CuiElement
                {
                    Parent = "BARREL_STAT",
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1",  Png =  _imageUI.GetImage("12") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-88.5 -16.5", OffsetMax = "-55.5 16.5" }
                }
                });
                
                                container.Add(new CuiElement
                {
                    Name = "ANIMALKILL_STAT",
                    Parent = UI_USER_STAT_INFO,
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png =  _imageUI.GetImage("8") },
                    new CuiRectTransformComponent { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-210.146 76.951", OffsetMax = "-13.974 122.989" }
                }
                });

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "48.359 -7.014", OffsetMax = "176.509 8.414" },
                    Text = { Text = GetLang("STAT_UI_ANIMAL_KILL", player.UserIDString, statInfo.otherStat.AnimalsKill), Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
                }, "ANIMALKILL_STAT");
		   		 		  						  	   		  	 	 		  	  			  			 		  				
                container.Add(new CuiElement
                {
                    Parent = "ANIMALKILL_STAT",
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png =  _imageUI.GetImage("17") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-88.5 -16.5", OffsetMax = "-55.5 16.5" }
                }
                });
                            }
            else
            {
                                container.Add(new CuiElement
                {
                    Name = "Heli_STAT",
                    Parent = UI_USER_STAT_INFO,
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png =  _imageUI.GetImage("8") },
                    new CuiRectTransformComponent { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-210.146 181.391", OffsetMax = "-13.974 227.429" }
                }
                });

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "48.359 -7.014", OffsetMax = "176.509 8.414" },
                    Text = { Text = GetLang("STAT_UI_HELI_KILL", player.UserIDString, statInfo.pVP.HeliKill), Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
                }, "Heli_STAT");

                container.Add(new CuiElement
                {
                    Parent = "Heli_STAT",
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png =  _imageUI.GetImage("15") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-88.5 -16.5", OffsetMax = "-55.5 16.5" }
                }
                });
                
                                container.Add(new CuiElement
                {
                    Name = "BRADLEY_STAT",
                    Parent = UI_USER_STAT_INFO,
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png =  _imageUI.GetImage("8") },
                    new CuiRectTransformComponent { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-210.146 129.171", OffsetMax = "-13.974 175.209" }
                }
                });

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "48.359 -7.014", OffsetMax = "176.509 8.414" },
                    Text = { Text = GetLang("STAT_UI_BRADLEY_KILL", player.UserIDString, statInfo.pVP.BradleyKill), Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
                }, "BRADLEY_STAT");

                container.Add(new CuiElement
                {
                    Parent = "BRADLEY_STAT",
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1",  Png =  _imageUI.GetImage("16") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-88.5 -16.5", OffsetMax = "-55.5 16.5" }
                }
                });
                
                                container.Add(new CuiElement
                {
                    Name = "NPCKILL_STAT",
                    Parent = UI_USER_STAT_INFO,
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png =  _imageUI.GetImage("8") },
                    new CuiRectTransformComponent { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-210.146 76.951", OffsetMax = "-13.974 122.989" }
                }
                });

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "48.359 -7.014", OffsetMax = "176.509 8.414" },
                    Text = { Text = GetLang("STAT_UI_NPC_KILL", player.UserIDString, statInfo.pVP.KillsNpc), Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
                }, "NPCKILL_STAT");

                container.Add(new CuiElement
                {
                    Name = "NPC_IMG_USER",
                    Parent = "NPCKILL_STAT",
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png =  _imageUI.GetImage("11") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-88.5 -16.5", OffsetMax = "-55.5 16.5" }
                }
                });
                            }

            container.Add(new CuiButton
            {
                Button = { Color = "1 1 1 0.1", Command = $"UI_HandlerStat ShowMoreStat {target} {statType}" },
                Text = { Text = GetLang("STAT_UI_BTN_MORE", player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "0.8538514 0.8491456 0.8867924 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "186.97 -254.682", OffsetMax = "383.15 -233.6" }
            }, UI_USER_STAT_INFO, "SHOW_MORE_STAT");

            CuiHelper.DestroyUi(player, "SHOW_MORE_STAT");
            CuiHelper.DestroyUi(player, "CRATE_STAT");
            CuiHelper.DestroyUi(player, "BARREL_STAT");
            CuiHelper.DestroyUi(player, "NPCKILL_STAT");
            CuiHelper.DestroyUi(player, "Heli_STAT");
            CuiHelper.DestroyUi(player, "BRADLEY_STAT");
            CuiHelper.DestroyUi(player, "ANIMALKILL_STAT");
            CuiHelper.AddUi(player, container);
        }

        
                
        private void RegisterPermissionIfNotExists(string perm)
        {
            if (!string.IsNullOrEmpty(perm) && !permission.PermissionExists(perm, this))
            {
                permission.RegisterPermission(perm, this);
            }
        }

        
                private void ConsoleCommandOpenMenu(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            if (_ignoreReservedPlayer.ContainsKey(arg.Player().userID.Get()))
            {
                PrintToChat(arg.Player(), GetLang("STAT_ADMIN_HIDE_STAT", arg.Player().UserIDString));
                return;
            }
            MainMenuStat(arg.Player());
        }
        public const string UI_USER_STAT = "USER_STAT";
                
        
        private static class ObjectCache
        {
            private static readonly object True = true;
            private static readonly object False = false;

            private static class StaticObjectCache<T>
            {
                private static readonly Dictionary<T, object> CacheByValue = new Dictionary<T, object>();

                public static object Get(T value)
                {
                    object cachedObject;
                    if (!CacheByValue.TryGetValue(value, out cachedObject))
                    {
                        cachedObject = value;
                        CacheByValue[value] = cachedObject;
                    }

                    return cachedObject;
                }
            }

            public static object Get<T>(T value)
            {
                return StaticObjectCache<T>.Get(value);
            }

            public static object Get(bool value)
            {
                return value ? True : False;
            }
        }
        private static class TimeHelper
        {
            public static string FormatTime(TimeSpan time, int maxSubstr = 5, string language = "ru")
            {
                return language == "ru" ? FormatTimeRussian(time, maxSubstr) : FormatTimeDefault(time);
            }

            private static string FormatTimeRussian(TimeSpan time, int maxSubstr)
            {
                List<string> substrings = new List<string>();

                if (time.Days != 0 && substrings.Count < maxSubstr)
                {
                    substrings.Add(Format(time.Days, "д"));
                }

                if (time.Hours != 0 && substrings.Count < maxSubstr)
                {
                    substrings.Add(Format(time.Hours, "ч"));
                }

                if (time.Minutes != 0 && substrings.Count < maxSubstr)
                {
                    substrings.Add(Format(time.Minutes, "м"));
                }

                if (time.Days == 0 && time.Seconds != 0 && substrings.Count < maxSubstr)
                {
                    substrings.Add(Format(time.Seconds, "с"));
                }

                if (substrings.Count == 0)
                {
                    substrings.Add("0с");
                }

                return string.Join(" ", substrings);
            }

            private static string FormatTimeDefault(TimeSpan time)
            {
                List<string> parts = new List<string>();

                if (time.Days > 0)
                {
                    parts.Add($"{time.Days} day{(time.Days == 1 ? string.Empty : "s")}");
                }

                if (time.Hours > 0)
                {
                    parts.Add($"{time.Hours} hour{(time.Hours == 1 ? string.Empty : "s")}");
                }

                if (time.Minutes > 0)
                {
                    parts.Add($"{time.Minutes} minute{(time.Minutes == 1 ? string.Empty : "s")}");
                }

                if (time.Seconds > 0)
                {
                    parts.Add($"{time.Seconds} second{(time.Seconds == 1 ? string.Empty : "s")}");
                }

                if (parts.Count == 0)
                {
                    parts.Add("0 seconds");
                }

                return string.Join(", ", parts);
            }

            private static string Format(int units, string form)
            {
                return $"{units}{form}";
            }
        }

                
        
        private static readonly string[] DefaultGatherHooks =
        {
            "OnDispenserGather",
            "OnDispenserBonus",
            "OnCollectiblePickup",
            "OnGrowableGathered",
        };

        protected override void LoadDefaultConfig()
        {
            _config = new Configuration();
        }
        
                private static Dictionary<ulong, PlayerInfo> _players = new();

        private void SendMsgRewardWipe(BasePlayer player)
        {
            if (!_prizePlayerData.TryGetValue(player.userID.Get(), out List<PrizePlayer> playerGrant))
                return;
		   		 		  						  	   		  	 	 		  	  			  			 		  				
            Dictionary<CatType, string> categoryMessages = new()
            {
                [CatType.Score] = "STAT_TOP_PLAYER_WIPE_SCORE",
                [CatType.Killer] = "STAT_TOP_PLAYER_WIPE_KILL",
                [CatType.Time] = "STAT_TOP_PLAYER_WIPE_TIME",
                [CatType.Farm] = "STAT_TOP_PLAYER_WIPE_FARM",
                [CatType.Raid] = "STAT_TOP_PLAYER_WIPE_EXP",
                [CatType.KillerNpc] = "STAT_TOP_PLAYER_WIPE_KILL_NPC",
                [CatType.KillerAnimal] = "STAT_TOP_PLAYER_WIPE_KILL_ANIMAL",
            };

            foreach (PrizePlayer item in playerGrant)
            {
                if (categoryMessages.TryGetValue(item.catType, out string messageKey))
                {
                    player.ChatMessage(GetLang(messageKey, player.UserIDString, item.top));
                }
            }

            NextTick(() => 
            { 
                _prizePlayerData.Remove(player.userID.Get()); 
                SaveDataPrize(); 
            });
        }
        private const string PermReset = "XDStatistics.reset";

        
        private void OnPlayerAttack(BasePlayer attacker, HitInfo hitinfo)
        {
            if (hitinfo == null || attacker == null || !attacker.IsConnected)
                return;

            if (hitinfo.HitEntity is ScientistNPC && !_config.settings.npsDeathUse)
                return;

            if (hitinfo.HitEntity is BasePlayer targetPlayer && !targetPlayer.userID.IsSteamId())
                return;

            if (hitinfo.HitEntity == attacker)
                return;

            PlayerInfo playerStatInitiator = PlayerInfo.Find(attacker.userID.Get());
            WeaponProgressAdd(playerStatInitiator, hitinfo);
		   		 		  						  	   		  	 	 		  	  			  			 		  				
            playerStatInitiator.pVP.Shots++;
            if (hitinfo.isHeadshot)
                playerStatInitiator.pVP.Headshots++;
        }

                
                
        
        private class ImageUI
        {
            private readonly string _paths;
            private readonly string _printPath;
            private readonly Dictionary<string, ImageData> _images;

            private enum ImageStatus
            {
                NotLoaded,
                Loaded,
                Failed
            }
		   		 		  						  	   		  	 	 		  	  			  			 		  				
            public ImageUI()
            {
                _paths = Instance.Name + "/Images/";
                _printPath = "data/" + _paths;
                _images = new Dictionary<string, ImageData>
                {
                    { "1", new ImageData() },
                    { "2", new ImageData() },
                    { "3", new ImageData() },
                    { "4", new ImageData() },
                    { "5", new ImageData() },
                    { "6", new ImageData() },
                    { "7", new ImageData() },
                    { "8", new ImageData() },
                    { "9", new ImageData() },
                    { "10", new ImageData() },
                    { "11", new ImageData() },
                    { "12", new ImageData() },
                    { "13", new ImageData() },
                    { "14", new ImageData() },
                    { "15", new ImageData() },
                    { "16", new ImageData() },
                    { "17", new ImageData() },
                    { "18", new ImageData() },
                    { "19", new ImageData() },
                    { "20", new ImageData() },
                };
            }

            private class ImageData
            {
                public ImageStatus Status = ImageStatus.NotLoaded;
                public string Id { get; set; }
            }

            public string GetImage(string name)
            {
                if (_images.TryGetValue(name, out ImageData image) && image.Status == ImageStatus.Loaded)
                    return image.Id;
                return null;
            }

            public void DownloadImage()
            {
                KeyValuePair<string, ImageData>? imageToDownload = null;
                foreach (KeyValuePair<string, ImageData> img in _images)
                {
                    if (img.Value.Status != ImageStatus.NotLoaded) continue;
                    imageToDownload = img;
                    break;
                }

                if (imageToDownload.HasValue)
                {
                    ServerMgr.Instance.StartCoroutine(ProcessDownloadImage(imageToDownload.Value));
                    return;
                }

                List<string> failedImages = new List<string>();
                foreach (KeyValuePair<string, ImageData> img in _images)
                {
                    if (img.Value.Status == ImageStatus.Failed)
                    {
                        failedImages.Add(img.Key);
                    }
                }

                if (failedImages.Count > 0)
                {
                    string images = string.Join(", ", failedImages);
                    Instance.PrintError(RU 
                        ? $"Не удалось загрузить следующие изображения: {images}. Возможно, вы не загрузили их в папку '{_printPath}'."
                        : $"Failed to load the following images: {images}. Perhaps you did not upload them to the '{_printPath}' folder.");
                    Interface.Oxide.UnloadPlugin(Instance.Name);
                    return;
                }

                Instance.Puts(RU 
                    ? $"{_images.Count} изображений успешно загружено!"
                    : $"{_images.Count} images downloaded successfully!");
            }
            
            public void UnloadImages()
            {
                foreach (KeyValuePair<string, ImageData> item in _images)
                {
                    if (item.Value.Status == ImageStatus.Loaded && item.Value?.Id != null)
                    {
                        FileStorage.server.Remove(uint.Parse(item.Value.Id), FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID);
                    }
                }

                _images?.Clear();
            }

            private IEnumerator ProcessDownloadImage(KeyValuePair<string, ImageData> image)
            {
                string url = $"file://{Interface.Oxide.DataDirectory}/{_paths}{image.Key}.png";

                using UnityWebRequest www = UnityWebRequestTexture.GetTexture(url);
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.ConnectionError)
                {
                    image.Value.Status = ImageStatus.Failed;
                }
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

        private IEnumerable<string> _gatherHooks = DefaultGatherHooks.Concat(SkillTreeGatherHooks).Concat(ZlevelsGatherHooks);
        
        private void OnEntityBuilt(Planner plan, GameObject go)
        {
            BasePlayer player = plan != null ? plan.GetOwnerPlayer() : null;
            if (player == null)
                return;

            BaseEntity entity = go != null ? go.ToBaseEntity() : null;
            if (entity == null)
                return;
            
            BuildingBlock bBlock = entity as BuildingBlock;
            
            NextTick(() =>
            {
                if (bBlock == null || bBlock.IsDestroyed) return;
                PlayerInfo playerstat = PlayerInfo.Find(player.userID.Get());
                playerstat.otherStat.BuildingCrate++;
                playerstat.Score += _config.settingsScore.BuildingScore;
            });
        }

        
                private void Unload()
        {
            if (IsObjectNull(Instance))
                return;
            
            SaveData();
            SaveDataIgnoreList();
            SaveDataPrize();
            if (RewardPlayerCoroutine != null)
                ServerMgr.Instance.StopCoroutine(RewardPlayerCoroutine);
            foreach (BasePlayer player in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(player, UI_INTERFACE);
            if (_imageUI != null)
            {
                _imageUI.UnloadImages();
                _imageUI = null;
            }
            sb = null;
            Instance = null;
        }
        private void LoadDataPrize() => _prizePlayerData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, List<PrizePlayer>>>("XDStatistics/PlayersReward");

        private void ToggleGatherHooks()
        {
            string[] hooksToSubscribe = DefaultGatherHooks;

            if (_config.settings.UseSkillTreeHooks && SkillTree != null)
            {
                hooksToSubscribe = SkillTreeGatherHooks;
            }
            else if (_config.settings.UseZLevelsRemasteredHooks && ZLevelsRemastered != null)
            {
                hooksToSubscribe = ZlevelsGatherHooks;
            }

            ToggleSubscription(hooksToSubscribe);
        }

        
                
                public const string UI_INTERFACE = "INTERFACE_STATS";

        private Coroutine RewardPlayerCoroutine { get; set; }
        
        private void SendDiscordMessage(string title, List<string> embeds, bool inline = false)
        {
            Embed embed = new Embed();
            foreach (string item in embeds)
            {
                embed.AddField(title, item, inline, _config.discordMessage.colorLines.GetRandom());
            }
            webrequest.Enqueue(_config.discordMessage.weebHook, new DiscordMessage(_config.discordMessage.message, embed).ToJson(), (code, response) => { },
                this,
                RequestMethod.POST, new Dictionary<string, string>
                {
                    { "Content-Type", "application/json" }
                });
        }

        
        
        
        
        private void OnDispenserBonus(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            BasePlayer player = entity.ToPlayer();
            if (player == null)
                return;
            ProgressAdd(player, item.info.shortname, item.amount, true);
        }

        private void ButtonDropStat(BasePlayer player, PlayerInfo info)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "13.75 -29.559", OffsetMax = "160.23 -15.727" }
            }, UI_USER_STAT_INFO, "BUTTON_REFRESH_STAT");

            container.Add(new CuiElement
            {
                Name = "USER_REFRESH_STAT",
                Parent = "BUTTON_REFRESH_STAT",
                Components = {
                    new CuiImageComponent { Color = "1 1 1 1", Sprite = "assets/icons/clear_list.png" },
                    new CuiRectTransformComponent { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "1.74 -6.5", OffsetMax = "14.74 6.5" }
                }
            });

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-120.55 -6.916", OffsetMax = "21.75 8.202" },
                Text = { Text = GetLang("STAT_UI_RESET_STAT", player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleLeft, Color = "0.8538514 0.8491456 0.8867924 1" }
            }, "BUTTON_REFRESH_STAT", "LABEL_REFRESH_USER");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Button = { Command = "UI_HandlerStat confirm", Color = "0 0 0 0" },
                Text = { Text = "" }
            }, "BUTTON_REFRESH_STAT");

            CuiHelper.DestroyUi(player, "BUTTON_REFRESH_STAT");
            CuiHelper.AddUi(player, container);
        }
		   		 		  						  	   		  	 	 		  	  			  			 		  				
        private void CheckInMinute()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (player == null || !player.userID.IsSteamId() || _ignoreReservedPlayer.ContainsKey(player.userID.Get()))
                    continue;
                PlayerInfo.AddPlayedTime(player.userID.Get());
            }

            timer.Once(60f, CheckInMinute);
        }

        private class Embed
        {
            public int color
            {
                get; set;
            }
            [JsonProperty("fields")] public List<Field> Fields { get; set; } = new List<Field>();

            public Embed AddField(string name, string value, bool inline, int colors)
            {
                Fields.Add(new Field(name, Regex.Replace(value, "<.*?>", string.Empty), inline));
                color = colors;
                return this;
            }
        }

        [ConsoleCommand("UI_HandlerStat")]
        private void CmdConsoleHandler(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            PlayerInfo playerInfo = PlayerInfo.Find(player.userID.Get());
            if (player != null && args.HasArgs(1))
            {
                switch (args.Args[0])
                {
                    case "hidestat":
                        {
                            if (_config.settings.availabilityUse && !permission.UserHasPermission(player.UserIDString, PermAvailability))
                                return;
                            playerInfo.HidedStatistics = !playerInfo.HidedStatistics;
                            ButtonHideStat(player, playerInfo);
                            break;
                        }
                    case "confirm":
                        {
                            DialogConfirmationDropStat(player);
                            break;
                        }
                    case "confirm_yes":
                        {
                            if (_config.settings.dropStatUse && !permission.UserHasPermission(player.UserIDString, PermReset))
                                return;
                            PlayerInfo.PlayerClearData(player.userID.Get());
                            UserStat(player);
                            break;
                        }
                    case "changeCategory":
                        {
                            ulong target = ulong.Parse(args.Args[1]);
                            int cat = int.Parse(args.Args[2]);
                            CategoryStatUser(player, target, cat);
                            break;
                        }
                    case "ShowMoreStat":
                        {
                            ulong target = ulong.Parse(args.Args[1]);
                            int cat = int.Parse(args.Args[2]);
                            OtherStatUser(player, target, cat == 0 ? 1 : 0);
                            break;
                        }
                    case "listplayer":
                        {
                            if (args.Args.Length > 1)
                            {
                                string seaecher = args.Args[1].ToLower();
                                SearchPageUser(player, seaecher);
                            }
                            else
                                SearchPageUser(player);
                            break;
                        }
                    case "GoStatPlayers":
                        {
                            ulong id = ulong.Parse(args.Args[1]);
                            UserStat(player, id);
                            break;
                        }
                    case "Page_swap":
                        {
                            int cat = int.Parse(args.Args[1]);
                            MenuButton(player, cat);
                            break;
                        }
                }
            }
        }
        private static Dictionary<ulong, PlayerInfo> _ignoreReservedPlayer = new();
        private Dictionary<string, ItemDisplayName> _itemName = new();
		   		 		  						  	   		  	 	 		  	  			  			 		  				
                private void AddDisplayName()
        {
            webrequest.Enqueue($"https://api.skyplugins.ru/api/getitemlist", "", (code, response) =>
            {
                if (code == 200)
                {
                    List<ItemName> itemList = JsonConvert.DeserializeObject<List<ItemName>>(response);
                    foreach (ItemName item in itemList)
                    {
                        _itemName[item.ShortName] = new ItemDisplayName(item.ENDisplayName, item.RUDisplayName);
                    }
                }
            }, this);
        }

        private void LoadData() => _players = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerInfo>>("XDStatistics/StatsUser");
        
        private void OnPluginUnloaded(Plugin name)
        {
            if (Instance == null)
                return;

            if (name.ToString() == "ZLevelsRemastered" || name.ToString() == "SkillTree")
            {
                foreach (string hook in _gatherHooks)
                    Unsubscribe(hook);
            }
            NextTick(ToggleGatherHooks);
        }

        private bool IsClans(string userID, string targetID)
        {
            if (Clans is null) return false;
    
            string tagUserID = Clans.Call("GetClanOf", userID) as string;
            string tagTargetID = Clans.Call("GetClanOf", targetID) as string;
    
            return tagUserID is not null && tagUserID == tagTargetID;
        }

                private void MainMenuStat(BasePlayer player)
        {
            if (_ignoreReservedPlayer.ContainsKey(player.userID.Get()))
            {
                PrintToChat(player, GetLang("STAT_ADMIN_HIDE_STAT", player.UserIDString));
                return;
            }
            string background = _config.settingsInterface.UsebackgroundImageUrl ? GetImage("CustomBackgroundImage") :  _imageUI.GetImage("1");
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, "OverlayNonScaled", UI_INTERFACE);

            container.Add(new CuiElement
            {
                Name = "BACKGROUND",
                Parent = UI_INTERFACE,
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = background },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1"}
                }
            });

            container.Add(new CuiElement
            {
                Name = UI_CLOSE_MENU,
                Parent = "BACKGROUND",
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png =  _imageUI.GetImage("14") },
                    new CuiRectTransformComponent { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-115.354 -36.798", OffsetMax = "-105.246 -27.942" }
                }
            });

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Button = { Close = UI_INTERFACE, Color = "0 0 0 0" },
                Text = { Text = "" }
            }, UI_CLOSE_MENU);
		   		 		  						  	   		  	 	 		  	  			  			 		  				
            CuiHelper.DestroyUi(player, UI_INTERFACE);
            CuiHelper.AddUi(player, container);
            MenuButton(player);
        }

        private class Field
        {
            public Field(string name, string value, bool inline)
            {
                Name = name;
                Value = value;
                Inline = inline;
            }

            [JsonProperty("name")] public string Name { get; set; }
            [JsonProperty("value")] public string Value { get; set; }
            [JsonProperty("inline")] public bool Inline { get; set; }
        }

                private void OnEntityDeath(LootContainer entity, HitInfo info)
        {
            if (entity == null || info == null)
                return;
            BasePlayer player = info.InitiatorPlayer;
            if (player == null)
                return;
            if (entity.ShortPrefabName.Contains("barrel"))
            {
                PlayerInfo playerstat = PlayerInfo.Find(player.userID.Get());
                playerstat.otherStat.BarrelDeath++;
                playerstat.Score += _config.settingsScore.barrelScore;
            }
        }
        public const string UI_CLOSE_MENU = "CLOSE_MENU";
        private ImageUI _imageUI;
        private Dictionary<string, int> GetCategory(PlayerInfo statInfo, int cat)
        {
            Dictionary<string, int> result = new();

            switch (cat)
            {
                case 0:
                    AddItems(statInfo.gather.GatheredTotal.ToList(), 8);
                    result.Add("all", statInfo.gather.AllGathered);
                    break;
                case 1:
                    AddItems(statInfo.explosion.ExplosionUsed.ToList(), 8);
                    result.Add("all", statInfo.explosion.AllExplosionUsed);
                    break;
                case 2:
                    List<KeyValuePair<string, int>> orderedHarvesting = statInfo.harvesting.HarvestingList.XDToList();
                    orderedHarvesting.Sort((a, b) => b.Value.CompareTo(a.Value));
                    AddItems(orderedHarvesting, 9);
                    result.Add("all", statInfo.harvesting.AllHarvesting);
                    break;
                default:
                    return null;
            }
		   		 		  						  	   		  	 	 		  	  			  			 		  				
            return result;

            void AddItems(IReadOnlyList<KeyValuePair<string, int>> source, int count)
            {
                for (int i = 0; i < Math.Min(count, source.Count); i++)
                {
                    result.Add(source[i].Key, source[i].Value);
                }
            }
        }
		   		 		  						  	   		  	 	 		  	  			  			 		  				
        private void OnTimedExplosiveExplode(TimedExplosive explosive, Vector3 explosionFxPos)
        {
            BaseEntity entity = explosive != null ? explosive.LookupPrefab() : null;
            if (entity != null && explosive.creatorEntity is BasePlayer player)
            {
                ExplosionProgressAdd(player, entity);
            }
        }
		   		 		  						  	   		  	 	 		  	  			  			 		  				
        private void ToggleSubscription(string[] hooks)
        {
            foreach (string hook in hooks)
            {
                Unsubscribe(hook);
                Subscribe(hook);
            }
        }
        private void SaveDataPrize() => Interface.Oxide.DataFileSystem.WriteObject("XDStatistics/PlayersReward", _prizePlayerData);
        
        private void Init()
        {
            Instance = this;
            sb = new StringBuilder();
            LoadData();
            LoadDataIgnoreList();
            LoadDataPrize();
            foreach (string hook in _gatherHooks)
                Unsubscribe(hook);
            
            NextTick(ToggleGatherHooks);
        }
        private string GetCorrectName(string name, int length) => name.ToPrintable(length).EscapeRichText().Trim();

        private void OnSkillTreeHandleDispenser(BasePlayer player, BaseEntity entity, Item item)
        {
            if (player == null || item == null || item.info == null) return;

            ProgressAdd(player, item.info.shortname, item.amount);
        }
        private const string PermAdmin = "XDStatistics.admin";

        private void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            BasePlayer player = entity.ToPlayer();
            if (player == null)
                return;
            ProgressAdd(player, item.info.shortname, item.amount);
        }

        private void CloseLayer(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, UI_USER_STAT);
            CuiHelper.DestroyUi(player, UI_SEARCH_USER);
            CuiHelper.DestroyUi(player, UI_TOP_TEN_USER);
        }
            }
}
