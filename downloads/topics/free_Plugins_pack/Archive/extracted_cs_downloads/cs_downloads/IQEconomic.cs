using ConVar;
using Pool = Facepunch.Pool;
using Object = System.Object;
using System;
using System.Collections;
using UnityEngine.Networking;
using Oxide.Game.Rust.Cui;
using Newtonsoft.Json;
using Oxide.Core.Database;
using System.Linq;
using Oxide.Core;
using System.Text;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("IQEconomic", "Mercury", "2.10.13")]
    [Description("Экономика на ваш сервер")]    
    public class IQEconomic : RustPlugin
    {
        private Boolean isUsedSQL = false;
        private const String arrowLogRemoved = "=======>";

        private void OnPlayerSleep(BasePlayer player)
        {
            if (_interface == null) return;
            InterfaceBuilder.DestroyPlayerUI(player);
        }
        
        private void DisconnectedPlayer(BasePlayer player)
        {
            if (isUsedSQL)
            {
                PlayerInfo pInfo = PlayerInfo.Get(player.UserIDString);
                if (pInfo == null) return;
                ImportPlayerSQL(player.UserIDString, pInfo.Balance, pInfo.LimitBalance, pInfo.Time, pInfo.DateTime, pInfo.IsHide);
                return;
            }
            
            PlayerInfo.Save(player.UserIDString);
            PlayerInfo.Remove(player.UserIDString);
        }
		   		 		  						   					  						  						   		 		  		 	
        private void OnPlayerRespawned(BasePlayer player)
        {
            if (!saveAfterDeath.TryGetValue(player, out Int32 balance)) return;
            if (balance == 0) return;
            AddBalance(player, balance, TypeLog.ReturnedSaveAfterDeath);
        }
        private List<Item> GetPhysicsItems(BasePlayer player)
        {
            Configuration.GeneralSetting.PhysicsItem physicConfig = config.generalSetting.physicsItem;
            
            List<Item> acceptedItems = Pool.Get<List<Item>>();
            List<Item> playerInventory = GetAllItems(player);

            foreach (Item item in playerInventory)
            {
                if (physicConfig.IsPhysicMoney(item))
                    acceptedItems.Add(item);
            }
            
            Pool.FreeUnmanaged(ref playerInventory);

            return acceptedItems;
        }

        public String FormatTime(TimeSpan time, string userId)
        {
            if (time.Days > 0)
                return Format(time.Days, GetLang("EXCHANGER_COURSE_TITLE_FORMAT_TIME_DAYS", userId));

            if (time.Hours > 0)
                return Format(time.Hours, GetLang("EXCHANGER_COURSE_TITLE_FORMAT_TIME_HOURSE", userId));

            if (time.Minutes > 0)
                return Format(time.Minutes, GetLang("EXCHANGER_COURSE_TITLE_FORMAT_TIME_MINUTES", userId));

            return time.Seconds > 0 ? Format(time.Seconds, GetLang("EXCHANGER_COURSE_TITLE_FORMAT_TIME_SECONDS", userId)) : String.Empty; 
        }
        
        private (BasePlayer, String) FindPlayerNameOrID(BasePlayer finder, String nameOrID)
        {
            List<BasePlayer> players = Pool.Get<List<BasePlayer>>();
            players.AddRange(BasePlayer.activePlayerList);
            
            if (nameOrID.IsSteamId() && ulong.TryParse(nameOrID, out UInt64 userID))
            {
                BasePlayer player = BasePlayer.FindByID(userID);
                Pool.FreeUnmanaged(ref players);
                return (player, null);
            }

            List<BasePlayer> matchingPlayers = Pool.Get<List<BasePlayer>>();

            try
            {
                matchingPlayers.AddRange(players.Where(player => player.displayName != null && player.displayName.IndexOf(nameOrID, StringComparison.OrdinalIgnoreCase) >= 0));
                
                switch (matchingPlayers.Count)
                {
                    case 1:
                    {
                        BasePlayer foundPlayer = matchingPlayers[0];
                        return (foundPlayer, null);
                    }
                    case > 1:
                    {
                        String nicknameList = String.Join(", ", matchingPlayers.Take(3).Select(p => p.displayName));
                        return (null, GetLang("CHAT_ALERT_TRANSFER_MATCHES_PLAYERS", finder.UserIDString, nameOrID, nicknameList));
                    }
                    default:
                        return (null, null);
                }
            }
            finally
            {
                Pool.FreeUnmanaged(ref players);
                Pool.FreeUnmanaged(ref matchingPlayers);
            }
        }

                
                
        private void OnPlayerDeath(BasePlayer target, HitInfo hitInfo)
        {
            if(!target) return;
            Boolean isRealPlayerTarget = IsRealPlayer(target);
            Configuration.EarningCoins.PresetEarning earningPlayer = config.earningCoins.killedPlayer;
            Configuration.EarningCoins.PresetEarning earningNpc = config.earningCoins.killedNpcs;

            if (isRealPlayerTarget)
            {
                if (isUsedUI)
                    InterfaceBuilder.DestroyPlayerUI(target);

                if (config.generalSetting.physicsItem.useSaveDeath && config.generalSetting.typeCoins == TypeCoins.Physics)
                {
                    Int32 balance = GetBalancePlayer(target);
                    RemoveBalance(target, balance, TypeLog.SaveAfterDeath);
                    saveAfterDeath[target] = balance;
                }
            }

            if (!earningPlayer.useEarning && !earningNpc.useEarning) return;
            if (hitInfo == null) return;
            BasePlayer attacker = hitInfo.InitiatorPlayer;
            if (!attacker) return;
            if (!IsRealPlayer(attacker)) return;
            if (attacker == target) return;
           
            if (isRealPlayerTarget)
            {
                if (IsBlockedEarningKillPlayer(attacker, target)) return;

                if (!IsOnlyTakeEarning(attacker, earningPlayer)) return;
                
                AddBalance(attacker, earningPlayer.countMoney, TypeLog.Action, earningPlayer.useChatAlert ? "CHAT_ALERT_EARNING_KILLED_PLAYER" : default);
                return;
            }
            
            if (!IsOnlyTakeEarning(attacker, earningNpc)) return;
            
            AddBalance(attacker, earningNpc.countMoney, TypeLog.Action, earningNpc.useChatAlert ? "CHAT_ALERT_EARNING_KILLED_NPC" : default);
        }

        private void ImportPlayerSQL(String userID, Int32 balance, Int32 limitBalance, Int32 time, Double dateTime, Boolean isHide, Boolean isMigrate = false)
        {
            if (sqlConnection == null) return;
            
            Configuration.GeneralSetting.MySQLConnection sqlInfo = config.generalSetting.mySQLConnectionSettings;
            String hideString = isHide.ToString();
            String sqlQuery = $"UPDATE {sqlInfo.dbTableName} SET `steamid` = @0, `balance` = @1,`limit_balance` = @2,`time` = @3,`last_connection` = @4, `is_hide` = @5 WHERE `steamid` = @0";
            Sql updateCommand = Sql.Builder.Append(sqlQuery, userID, balance, limitBalance, time, dateTime, hideString);
            
            sqlLibrary.Update(updateCommand, sqlConnection, rowsAffected =>
            {
                if (rowsAffected > 0) return;
                String Query = String.Format(SQL_Query_InsertUser(), userID, balance, limitBalance, time, dateTime, hideString);
                Sql sql = Sql.Builder.Append(Query);
                
                sqlLibrary.Insert(sql, sqlConnection, rowsAffecteds =>
                {
                    if (!isMigrate)
                        Puts(LanguageEn? $"" : $"Данные игрока {userID} были внесены в базу-данных");
                });
            });
        }
        
        private String SQL_Query_CreatedDatabase()
        {
            String CreatedDB = $"CREATE TABLE IF NOT EXISTS `{config.generalSetting.mySQLConnectionSettings.dbTableName}`(" +
                               "`id` INT(11) NOT NULL AUTO_INCREMENT," +
                               "`steamid` VARCHAR(255) NOT NULL," +
                               "`balance` VARCHAR(255) NOT NULL," +
                               "`limit_balance` VARCHAR(255) NOT NULL," +
                               "`time` VARCHAR(255) NOT NULL," +
                               "`last_connection` VARCHAR(255) NOT NULL," +
                               "`is_hide` VARCHAR(6) NOT NULL," +
                               " PRIMARY KEY(`id`))";
            
            return CreatedDB;
        }   

        
        
        private void TransferToPlayer(BasePlayer player, BasePlayer targetPlayer, Int32 amount)
        {
            if (!IsValidTransfer(player, targetPlayer, amount))
                return;
		   		 		  						   					  						  						   		 		  		 	
            ExecuteTransfer(player, targetPlayer, amount);
        }
        
        private List<String> GetOrSetCacheUI(String keyCache, String interfaceJson = null)
        {
            if (cachedUI.TryGetValue(keyCache, out List<String> ui))
                return ui;

            if (interfaceJson == null) return null;

            List<String> newUI = new() { interfaceJson };
            cachedUI[keyCache] = newUI;
            return newUI;
        }
        
        private void ConsoleCommandBalance(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            ChatCommandBalance(player);
        }
        
        
                
        private void OnEntityDeath(LootContainer entity, HitInfo info)
        {
            if (!entity.PrefabName.Contains("barrel") || entity.PrefabName.Contains("hobobarrel")) return;

            Configuration.EarningCoins.PresetEarning earning = config.earningCoins.killedBarrels;
            if (!earning.useEarning) return;
            
            if (!entity || info == null) return;
            
            BasePlayer player = info.InitiatorPlayer;
            
            if(!IsTakeEarning(player, earning)) return;
            AddBalance(player, earning.countMoney, TypeLog.Action, earning.useChatAlert ? "CHAT_ALERT_EARNING_DESTROY_BARREL" : default);
        }

        
        
        private void RemoveBalance(String userID, Int32 amount, TypeLog typeLog)
        {
            if (config.generalSetting.typeCoins == TypeCoins.Physics)
            {
                PrintWarning(LanguageEn ? "" : $"Нельзя забрать валюту у игрока {userID} пока он оффлайн");
                return;
            }
            
            if (isUsedSQL)
                GetAndFunctionalBalancePlayerSQL(userID, false, amount);
            else
            {
                PlayerInfo pInfo = PlayerInfo.GetOffline(userID);
                if (pInfo == null)
                    return;

                if (!DoesHaveEnoughBalance(pInfo.Balance))
                {
                    pInfo.Balance = 0;
                    LogAction(userID, pInfo.Balance, typeLog, false);
                }
                else
                {
                    pInfo.Balance -= amount;
                    LogAction(userID, amount, typeLog, false);
                }
                
                PlayerInfo.SaveOffline(userID, pInfo);
                Interface.Oxide.CallHook("OnRemovedBalance", UInt64.Parse(userID), amount, null, pInfo.Balance);
            }
        }
        private static Boolean IsUnblockExchangerCourse(Int32 timeExchange) =>
            ((SaveRestore.SaveCreatedTime.ToUniversalTime().Subtract(epoch).TotalSeconds + timeExchange) - CurrentTime) <= 0;
		   		 		  						   					  						  						   		 		  		 	
        void API_TRANSFERS(BasePlayer player, BasePlayer transferUserID, Int32 balance) => TransferToPlayer(player, transferUserID, balance);

        private String Format(Int32 units, String form)
        {
            Int32 lastDigit = units % 10;
            Int32 lastTwoDigits = units % 100;

            if (lastTwoDigits is >= 11 and <= 19)
                return $"{units}{form}";

            return lastDigit switch
            {
                1 => $"{units}{form}",
                2 or 3 or 4 => $"{units}{form}",
                _ => $"{units}{form}"
            };
        }
        
        private Int32 GetLimiteExchanged(PlayerInfo pInfo)
        {
            Int32 limitExchange = config.exchangerSetting.limitStoreMoney;
            if (limitExchange <= 0) return -1;

            Int32 leftLimite = limitExchange - pInfo.LimitBalance;
            return leftLimite <= 0 ? 0 : leftLimite;
        }
        
        private void AddBalance(BasePlayer player, PlayerInfo pInfo, Int32 amount, TypeLog typeLog, String alertMessage = default)
        {
            if (!player || amount <= 0) return;

            if (config.generalSetting.typeCoins == TypeCoins.Physics)
            {
                Item physicMoney = config.generalSetting.physicsItem.CreatePhysicMoney(amount);
                if (physicMoney == null)
                    return;
                
                if (!TryMoveItem(player, physicMoney))
                {
                    physicMoney.Remove();
                    return;
                }
		   		 		  						   					  						  						   		 		  		 	
                if (!String.IsNullOrWhiteSpace(alertMessage))
                    SendChat(GetLang("CHAT_ALERT_EARNING_TITLE", player.UserIDString, amount, GetLang(alertMessage, player.UserIDString)), player);
                
                Int32 balancePlayer = GetBalancePlayer(player);
                UpdateExchangedUI(player, balancePlayer, GetLimiteExchanged(pInfo));
                LogAction(player, amount, typeLog, true); 
                
                Interface.Oxide.CallHook("OnAddedBalance", player.userID.Get(), amount, player, balancePlayer);
                return;
            }

            pInfo.Balance += amount;
            
            if (!String.IsNullOrWhiteSpace(alertMessage))
                SendChat(GetLang("CHAT_ALERT_EARNING_TITLE", player.UserIDString, amount, GetLang(alertMessage, player.UserIDString)), player);
            
            LogAction(player, amount, typeLog, true);
            
            DrawUI_UpdateTitleBalance(player, pInfo);
            
            UpdateExchangedUI(player, pInfo.Balance, GetLimiteExchanged(pInfo));
            
            Interface.Oxide.CallHook("OnAddedBalance", player.userID.Get(), amount, player, pInfo.Balance);
        }
        
        
        private void MoscovOVHBalanceSet(BasePlayer player, (Int32 exchangeCoins, Int32 storeMoney) exchangeDetail, UInt64 userID = 0)
        {
            if (!config.exchangerSetting.storeSetting.useMoscovOvh) return;
            if (!RustStore)
            {
                PrintWarning(LanguageEn ? "" : "У вас не установлен магазин MoscovOVH");
                return;
            }

            if (exchangeDetail.storeMoney <= 0)
                return;
            
            Boolean isSetOffline = !player || exchangeDetail.exchangeCoins <= 0;
            userID = !isSetOffline ? player.userID : userID;

            RustStore.CallHook("APIChangeUserBalance", userID, exchangeDetail.storeMoney, new Action<String>((result) =>
            {
                PlayerInfo pInfo = PlayerInfo.Get(userID.ToString());

                if (result == "SUCCESS")
                {
                    if (isSetOffline) return;
                    RemoveBalance(player, exchangeDetail.exchangeCoins, TypeLog.Exchanger);
                    SendChat(GetLang("CHAT_ALERT_EXCHANGED_SUCCES", player.UserIDString), player);

                    if (config.exchangerSetting.limitStoreMoney <= 0) return;
                    if (pInfo != null)
                        pInfo.LimitBalance += exchangeDetail.storeMoney;
                    return;
                }

                if (isSetOffline) return;
                SendChat(GetLang("CHAT_ALERT_EXCHANGED_FAIL", player.UserIDString), player);
            }));
        }

                
        
        private List<Item> GetAllItems(BasePlayer player)
        {
            List<Item> itemList = Pool.Get<List<Item>>();
            player.inventory.GetAllItems(itemList);
            player.inventory.AddBackpackContentsToList(itemList);
            return itemList;
        }

        
        private enum TypeCoins
        {
            Virtual,
            Physics
        }
		void API_REMOVE_BALANCE(BasePlayer player, Int32 balance) => RemoveBalance(player, balance, TypeLog.API);
        
        private void DrawUI_UpdateTitleBalance(BasePlayer player, PlayerInfo pInfo)
        {
            if (!isUsedUI) return;
            if (player.IsSleeping()) return;   
            if (_interface == null)
                return;
            
            if (pInfo == null) return;
            if (pInfo.IsHide) return;
            
            String Interface = InterfaceBuilder.GetInterface("UI_Balance_Title_Update");
            if (Interface == null)
                return;
		   		 		  						   					  						  						   		 		  		 	
            Int32 balance = GetBalancePlayer(pInfo);
            String balanceLang = balance >= 99999999
                ? GetLang("BALANCE_TITLE_SHORT", player.UserIDString, balance)
                : GetLang("BALANCE_TITLE", player.UserIDString, balance);
            
            Interface = Interface.Replace("%BALANCE_TITLE%", balanceLang);
            
            AddUI(player, Interface);
        }
        private void OnPlayerConnected(BasePlayer player) => ConnectedPlayer(player);
        private Timer timerTrackerTimes;        
        private Boolean isUsedStores = false;
        
        private void GetPlayerSQL(String userID, BasePlayer player = null)
        {
            if (sqlConnection == null)
                return;
            
            Sql sql = Sql.Builder.Append(SQL_Query_SelectedDatabase(userID));

            sqlLibrary.Query(sql, sqlConnection, list =>
            {
                if (list == null)
                    return;

                if (list.Count <= 0)
                {
                    PlayerInfo.Import(userID, new PlayerInfo
                    {
                        Balance = 0,
                        LimitBalance = 0,
                        Time = 0,
                        DateTime = CurrentTime,
                        IsHide = false,
                    });

                    if (player)
                        DrawUI_HudPanel(player);
                    return;
                }

                foreach (Dictionary<String, Object> entry in list)
                {
                    String steamID = (String)entry["steamid"];
                    if (!steamID.IsSteamId()) return;

                    if (!Int32.TryParse((String)entry["balance"], out Int32 balance))
                        return;

                    if (!Int32.TryParse((String)entry["limit_balance"], out Int32 limitBalance))
                        return;

                    if (!Int32.TryParse((String)entry["time"], out Int32 time))
                        return;

                    if (!Double.TryParse((String)entry["last_connection"], out Double dateTime))
                        return;
                    
                    if (!Boolean.TryParse((String)entry["is_hide"], out Boolean isHideStatus))
                        return;

                    PlayerInfo.Import(userID, new PlayerInfo
                    {
                        Balance = balance,
                        LimitBalance = limitBalance,
                        Time = time,
                        DateTime = CurrentTime,
                        IsHide = isHideStatus,
                    });
                    
                    if (player)
                        DrawUI_HudPanel(player);
                }
            });
        }
        private Int32 GetBalancePlayer(PlayerInfo pInfo) => pInfo?.Balance ?? 0;

        void API_TRANSFERS(UInt64 userID, String transferUserID, Int32 balance)
        {
            BasePlayer player = BasePlayer.FindByID(userID);
            if (!player) return;

            TransferToPlayer(player, transferUserID, balance);
        }

        private void RemovePhysicMoney(BasePlayer player, Int32 amount)
        {
            List<Item> physicMoney = GetPhysicsItems(player);
		   		 		  						   					  						  						   		 		  		 	
            foreach (Item pItem in physicMoney)
            {
                if (amount <= 0)
                    break;

                if (pItem.amount >= amount)
                {
                    pItem.amount -= amount;
		   		 		  						   					  						  						   		 		  		 	
                    if (pItem.amount == 0)
                    {
                        pItem.RemoveFromContainer();
                        pItem.Remove();
                    }
		   		 		  						   					  						  						   		 		  		 	
                    amount = 0;
                }
                else
                {
                    amount -= pItem.amount;
                    pItem.RemoveFromContainer();
                    pItem.Remove();
                }
                
                pItem.MarkDirty();
            }
            
            Pool.FreeUnmanaged(ref physicMoney);
        }
        
        private void GetAndFunctionalBalancePlayerSQL(String userID, Boolean addOrRemove, Int32 amount = 0)
        {
            if (sqlConnection == null)
                return;
            
            Sql sql = Sql.Builder.Append(SQL_Query_SelectedDatabase(userID));
		   		 		  						   					  						  						   		 		  		 	
            sqlLibrary.Query(sql, sqlConnection, list =>
            {
                if (list == null)
                    return;

                if (list.Count <= 0)
                {
                    PlayerInfo.Import(userID, new PlayerInfo
                    {
                        Balance = addOrRemove ? amount : 0,
                        LimitBalance = 0,
                        Time = 0,
                        DateTime = CurrentTime,
                        IsHide = false,
                    });
                    
                    return;
                }
		   		 		  						   					  						  						   		 		  		 	
                foreach (Dictionary<String, Object> entry in list)
                {
                    String steamID = (String)entry["steamid"];
                    if (!steamID.IsSteamId()) return;

                    if (!Int32.TryParse((String)entry["balance"], out Int32 balance))
                        return;

                    if (!Int32.TryParse((String)entry["limit_balance"], out Int32 limitBalance))
                        return;

                    if (!Int32.TryParse((String)entry["time"], out Int32 time))
                        return;

                    if (!Double.TryParse((String)entry["last_connection"], out Double dateTime))
                        return;
                    
                    if (!Boolean.TryParse((String)entry["is_hide"], out Boolean isHideStatus))
                        return;
                    
                    PlayerInfo.Import(userID, new PlayerInfo
                    {
                        Balance = addOrRemove ? balance + amount : Math.Max(balance - amount, 0),
                        LimitBalance = limitBalance,
                        Time = time,
                        DateTime = dateTime,
                        IsHide = isHideStatus,
                    });
                }
                
                PlayerInfo pInfo = PlayerInfo.Get(userID);
                ImportPlayerSQL(userID, pInfo.Balance, pInfo.LimitBalance, pInfo.Time, pInfo.DateTime, pInfo.IsHide);
		   		 		  						   					  						  						   		 		  		 	
                Interface.Oxide.CallHook(addOrRemove ? "OnAddedBalance" : "OnRemovedBalance", UInt64.Parse(userID), amount, null, pInfo.Balance);
            });
        }
        /// <summary>
        /// Plans :
        /// - Исправлена возможная ошибка при Unload с закрытием SQL соединения
        /// - Добавлена возможность полной очистки дата-файла при вайпе сервера, включается отдельно в конфигурации
        /// - Убрана лишняя обработка игроков при загрузке плагина, если используется MySQL
        /// - Добавлены дополнительные проверки в OnPlayerDeath на случай ошибки в конфигурации или в случае возможных конфликтов
        /// - Добавлена возможность запретить использовать команду для обмена валюты пока игрок не находится в безопасной зоне
        /// - Добавлено сообщение о некорректном синтаксисе если включен обмен между игроками, но не включен обмен на магазин
        /// </summary>
        
                [PluginReference] Plugin IQChat, Friends, Clans, Battles, Duel, RustStore, GameStoresRUST, Duelist, ArenaTournament, XFarmRoom;
        
        private Configuration.ExchangerSetting.CourseController GetActualyCourse()
        {
            foreach (Configuration.ExchangerSetting.CourseController course in orderedListExchanged)
            {
                if (IsUnblockExchangerCourse(course.secondsAfterWipe))
                    return course;
            }

            return null;
        }
        
        private void UpdateMultiplier(BasePlayer player, Int32 balancePlayer, Int32 limitExchanged, Int32 multiplier)
        {
            exchangerDataPlayer[player] += multiplier;
            DrawUI_UpdateExchangeOne_Menu(player, balancePlayer, limitExchanged, exchangerDataPlayer[player]);
        }
        
        private void UpdateExchangerCourse()
        {
            Configuration.ExchangerSetting.CourseController courseUpdated = GetActualyCourse();
            if (actualyCourse != null && courseUpdated == actualyCourse) return;
            
            actualyCourse = courseUpdated;

            if (courseData.keyCourse == courseUpdated.secondsAfterWipe) return;
            
            courseData.keyCourse = actualyCourse.secondsAfterWipe;
            courseData.lastUpdateCourseTime = CurrentTime;

            if (!config.exchangerSetting.useMessageUpdateCourse) return;
            foreach (BasePlayer basePlayer in BasePlayer.activePlayerList)
                SendChat(GetLang("CHAT_ALERT_COURSE_UPDATE", basePlayer.UserIDString, config.commandExchanger), basePlayer);
        }
        
        
        
        void API_TRANSFERS(UInt64 userID, UInt64 transferUserID, Int32 balance)
        {
            BasePlayer player = BasePlayer.FindByID(userID);
            if (!player) return;
            
            TransferToPlayer(player, transferUserID.ToString(), balance);
        }

        void API_TRANSFERS(BasePlayer player, UInt64 transferUserID, Int32 balance)
        {
            if (!player) return;
            
            BasePlayer targetPlayer = BasePlayer.FindByID(transferUserID);
            if (!targetPlayer) return;
            
            TransferToPlayer(player, targetPlayer, balance);
        }
        private Dictionary<String, List<String>> cachedUI = new();

        void API_REMOVE_BALANCE(UInt64 userID, Int32 balance)
        {
            String idString = userID.ToString();
            API_REMOVE_BALANCE(idString, balance);
        }
        
        private String SQL_Query_CheckColumn()
        {
            String checkColumnQuery = $@"SELECT COUNT(*) 
                                        FROM INFORMATION_SCHEMA.COLUMNS 
                                        WHERE table_schema = DATABASE()
                                        AND table_name = '{config.generalSetting.mySQLConnectionSettings.dbTableName}' 
                                        AND column_name = 'is_hide';";
            
            return checkColumnQuery;
        }   
        Int32 API_GET_BALANCE(BasePlayer player) => GetBalancePlayer(player);
        
        private String SQL_Query_InsertUser()
        {
            String InserUser = $"INSERT INTO `{config.generalSetting.mySQLConnectionSettings.dbTableName}`" + "(`steamid`, `balance`, `limit_balance`, `time`, `last_connection`, `is_hide`) VALUES ('{0}','{1}','{2}','{3}','{4}','{5}')";
            return InserUser;
        }
        private Dictionary<BasePlayer, Int32> saveAfterDeath = new();
        private static void AddUI(BasePlayer player, String json)
        {
            if (!player || player.net?.connection == null) return;
            CommunityEntity.ServerInstance.ClientRPC<String>(RpcTarget.Player("AddUI", player.net.connection), json);
        }
        
        private void ReadData() => courseData = Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<CourseInfo>("IQSystem/IQEconomic/Exchanger/UpdateCourseTime");

        private static IQEconomic _;

                
                
        private void GameStoreBalanceSet(BasePlayer player, (Int32 exchangeCoins, Int32 storeMoney) exchangeDetail, UInt64 userID = 0)
        {
            if (!config.exchangerSetting.storeSetting.useGameStores) return;
            if (!GameStoresRUST)
            {
                PrintWarning(LanguageEn ? "" : "У вас не установлен магазин GameStores");
                return;
            }
            
            if (exchangeDetail.storeMoney <= 0)
                return;
            
            Boolean isSetOffline = !player || exchangeDetail.exchangeCoins <= 0;
            userID = !isSetOffline ? player.userID : userID;
            
            PlayerInfo pInfo = PlayerInfo.Get(userID.ToString());

            if (!isSetOffline)
            {
                RemoveBalance(player, exchangeDetail.exchangeCoins, TypeLog.Exchanger);
                SendChat(GetLang("CHAT_ALERT_EXCHANGED_SUCCES", player.UserIDString), player);

                if (config.exchangerSetting.limitStoreMoney > 0)
                {
                    if (pInfo != null)
                        pInfo.LimitBalance += exchangeDetail.storeMoney;
                }
            }
            
            GameStoresRUST.CallHook("API_ChangePlayerBalance", userID, exchangeDetail.storeMoney, "plus", null, new Action<Boolean, String>((code, result) =>
            {
                LogToFile("DebugGS", $"GameStores DebugFile : isSetOffline : {isSetOffline}, exchangeCoins : {exchangeDetail.exchangeCoins}, code : {code}, result : {result}", this);
                //Cloud temp...
                
                // PlayerInfo pInfo = PlayerInfo.Get(userID.ToString());
                //
                if (code)
                {
                //     if (isSetOffline) return;
                //     RemoveBalance(player, exchangeDetail.exchangeCoins, TypeLog.Exchanger);
                //     SendChat(GetLang("CHAT_ALERT_EXCHANGED_SUCCES", player.UserIDString), player);
                //
                //     if (config.exchangerSetting.limitStoreMoney <= 0) return;
                //     if (pInfo != null)
                //         pInfo.LimitBalance += exchangeDetail.storeMoney;
                //     
                    return;
                }
                
                if (isSetOffline) return;
                SendChat(GetLang("CHAT_ALERT_EXCHANGED_FAIL", player.UserIDString), player);
            }));
        }
        
        private Int32 GetLimiteExchanged(BasePlayer player)
        {
            Int32 limitExchange = config.exchangerSetting.limitStoreMoney;
            if (limitExchange <= 0) return -1;
            
            PlayerInfo pInfo = PlayerInfo.Get(player.UserIDString);
            if (pInfo == null) return -1;
            
            Int32 leftLimite = limitExchange - pInfo.LimitBalance;
            return leftLimite <= 0 ? 0 : leftLimite;
        }

        Boolean API_IS_REMOVED_BALANCE(UInt64 userID, Int32 amount)
        {
            BasePlayer player = BasePlayer.FindByID(userID);

            return player && DoesHaveEnoughBalance(player, amount);
        }
        
        private Boolean IsLimiteBalance(PlayerInfo pInfo)
        {
            Int32 limitExchange = config.exchangerSetting.limitStoreMoney;
            if (limitExchange <= 0) return false;
            if (pInfo == null) return false;
            return pInfo.LimitBalance >= limitExchange;
        }
        
        private Configuration.ExchangerSetting.CourseController actualyCourse = null;

        private static Double CurrentTime => DateTime.UtcNow.Subtract(epoch).TotalSeconds;

        
        
                
                private const String transferPrivilage = "iqeconomic.transferuse";
        private static readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);

                
                
		Boolean API_MONEY_TYPE() => config.generalSetting.typeCoins == TypeCoins.Virtual;
        
        private const Boolean LanguageEn = false;
		   		 		  						   					  						  						   		 		  		 	
        private Boolean IsBlockedEarningKillPlayer(BasePlayer player, BasePlayer targetPlayer) =>
            IsFriends(player, targetPlayer.userID) || IsDuel(player.userID);
        
        private String SQL_Query_AddNewColumn()
        {
            String addColumnQuery = $@"
            ALTER TABLE `{config.generalSetting.mySQLConnectionSettings.dbTableName}`
            ADD COLUMN `is_hide` VARCHAR(6) NOT NULL DEFAULT 'false';";
            
            return addColumnQuery;
        }

        void API_SET_BALANCE(UInt64 userID, Int32 balance, ItemContainer itemContainer = null)
        {
            String idString = userID.ToString();
            API_SET_BALANCE(idString, balance, itemContainer);
        }
        
        private void DrawUI_StaticIconBalance(BasePlayer player)
        {
            if (_interface == null)
                return;
            
            DestroyUI(player, InterfaceBuilder.UI_PANEL_STATIC_BALANCE);
            
            List<String> cashedStaticPanel = GetOrSetCacheUI("UI_Balance_Panel_Icon_Static");
            if (cashedStaticPanel != null)
            {
                foreach (String uiCached in cashedStaticPanel)
                    AddUI(player, uiCached);
            }
            else
            {
                String Interface = InterfaceBuilder.GetInterface("UI_Balance_Panel_Icon_Static");
                if (Interface == null)
                    return;

                List<String> newUI = GetOrSetCacheUI("UI_Balance_Panel_Icon_Static", Interface);
                cashedStaticPanel = newUI;

                foreach (String uiCached in cashedStaticPanel)  
                    AddUI(player, uiCached);
            }
        }
        
                
        
        private Boolean DoesHaveEnoughBalance(Int32 balancePlayer, Int32 amount = 0)
        {
            if (balancePlayer == 0) return false;
            return balancePlayer >= amount;
        }
        
                
        
                
        void API_SET_BALANCE(String userID, Int32 balance, ItemContainer itemContainer = null)
        {
            if (!UInt64.TryParse(userID, out UInt64 id)) return;
            BasePlayer player = BasePlayer.FindByID(id);

            if(!player)
                AddBalance(userID, balance, TypeLog.API);
            else AddBalance(player, balance, TypeLog.API);
        }
        
                
        private Object CanUserLogin(String name, String id)
        {
            if (sqlConnection == null)
            {
                PrintError(LanguageEn ? "" : "Невозможно сохранить или получить данные игрока! SQL соединение недоступно....");
                return null;
            }
            
            GetPlayerSQL(id);
            return null;
        }
        
        private IEnumerator MigrateProcess()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
                DisconnectedPlayer(player);
            
            InterfaceBuilder.DestroyAll();
            
            String[] playersData = PlayerInfo.GetFiles();
            Int32 countFiles = playersData.Length;
            PrintWarning(LanguageEn ? $"[MIGRATION] The process of transferring data from the local storage to MySQL -> {countFiles} records has started, it may take some time, do not turn off the server and do not restart the plugin, after closing you will see a message. Approximate waiting time : {Math.Round((countFiles * 0.5) / 60)} minutes" : $"[MIGRATION] Запущен процесс переноса данных с локального хранилища в MySQL -> {countFiles} записей, это может занять какое-то время, не выключайте сервер и не перезагружайте плагин, после заверешения вы увидите сообщение. Примерное время ожидания : {Math.Round((countFiles * 0.5) / 60)} минут(ы)");
            yield return CoroutineEx.waitForSeconds(0.25f);

            PrintWarning(LanguageEn ? "[MIGRATION] Trying to connect to MySQL" : "[MIGRATION] Попытка подключения к MySQL");
            SQL_OpenConnection(true);
            yield return CoroutineEx.waitForSeconds(10f);

            if (sqlConnection != null)
                PrintWarning(LanguageEn ? "[MIGRATION] Successfully connected to MySQL" : "[MIGRATION] Успешно подключились к MySQL");
            else
            {
                PrintWarning(LanguageEn ? "[MIGRATION] Failed to connect to MySQL" : "[MIGRATION] Не удалось подключиться к MySQL");
                
                if (coroutineMigrate != null)
                {
                    ServerMgr.Instance.StopCoroutine(coroutineMigrate);
                    coroutineMigrate = null;
                }
            }
            
            yield return CoroutineEx.waitForSeconds(1f);
            PrintWarning(LanguageEn ? "[MIGRATION] The process of transferring player data to MySQL has been started" : "[MIGRATION] Запущен процесс переноса данных игроков в MySQL");

            Int32 iterationCount = 0; 
            foreach (String playerIDs in playersData)
            {
                PlayerInfo pInfo = PlayerInfo.GetOffline(playerIDs);
                if(pInfo == null) continue;
                
                ImportPlayerSQL(playerIDs, pInfo.Balance, pInfo.LimitBalance, pInfo.Time, pInfo.DateTime, pInfo.IsHide, true);
                iterationCount++;

                if (iterationCount % 100 == 0 || iterationCount == countFiles)
                {
                    Int32 remaining = countFiles - iterationCount;
                    PrintWarning(LanguageEn ? $"[MIGRATION] The transfer is in progress... There are {remaining} players left" : $"[MIGRATION] Перенос в процессе... Осталось {remaining} игроков");
                }
                
                yield return CoroutineEx.waitForSeconds(0.25f);
            }
            
            yield return CoroutineEx.waitForSeconds(10f);
            
            PrintWarning(LanguageEn ? "[MIGRATION] The transfer is complete! The plugin has been unloaded, now you can delete all the data files" : "[MIGRATION] Перенос завершен! Плагин выгружен, теперь вы можете удалить все дата-файлы");
            
            config.generalSetting.mySQLConnectionSettings.useMySQL = true;
            SaveConfig();
            
            NextTick(() => Interface.Oxide.UnloadPlugin(Name));
        }           
        
        private class PlayerInfo : DataManager<PlayerInfo>
        {
            public static void Import(String id, PlayerInfo data) => ImportPlayer(id, data);
            public static void Save(String id) => SavePlayer(id);
            public static void SaveOffline(String id, PlayerInfo data) => SaveOfflinePlayer(id, data);
            public static void Remove(String id) => RemovePlayer(id);
            public static void Delete(String id) => DeletePlayer(id);
            public static PlayerInfo Load(String id) => LoadPlayer(id);
            public static PlayerInfo Get(String id) => GetPlayer(id);
            public static PlayerInfo GetOffline(String id) => GetOfflinePlayer(id);
            public static String[] GetFiles() => GetFilesPlayers();

            
            [JsonProperty(LanguageEn ? "Player Balance" : "Баланс игрока")]
            public Int32 Balance;
            [JsonProperty(LanguageEn ? "Player's balance withdrawal limit" : "Лимит вывода баланса игрока")]
            public Int32 LimitBalance;
            [JsonProperty(LanguageEn ? "Time counter" : "Счетчик времени")]
            public Int32 Time;
            [JsonProperty(LanguageEn ? "The player's last login to the server" : "Последний вход игрока на сервер")]
            public Double DateTime;
            [JsonProperty(LanguageEn ? "Is the player's balance menu hidden" : "Скрыта ли меню баланса у игрока")]
            public Boolean IsHide;
        }

        
        
        private void OnLootSpawn(LootContainer container)
        {
            if (!container) return;
            String shortPrefabName = container.ShortPrefabName;
            Item droppedItem = config.generalSetting.physicsItem.GetDroppedItem(shortPrefabName);
            if (droppedItem == null) return;

            container.Invoke(() =>
            {
                if (container.inventory == null) return;
                if(!droppedItem.MoveToContainer(container.inventory))
                    droppedItem.Remove();
                
            }, 0.25f);
        }

        private void ExecuteTransfer(BasePlayer player, BasePlayer targetPlayer, Int32 amount)
        {
            RemoveBalance(player, amount, TypeLog.Transfer);
            SendChat(GetLang("CHAT_ALERT_TRANSFERED_SUCCES", player.UserIDString, amount, targetPlayer.displayName), player);
            
            AddBalance(targetPlayer, amount, TypeLog.Transfer);
            SendChat(GetLang("CHAT_ALERT_TRANSFERED_SUCCES", targetPlayer.UserIDString, amount, player.displayName), targetPlayer);
        }

        
        
        private void OnEntityDeath(BradleyAPC entity, HitInfo info)
        {
            Configuration.EarningCoins.PresetEarning earning = config.earningCoins.killedBradley;
            if (!earning.useEarning) return;
            
            if (!entity || info == null) return;
            
            BasePlayer player = info.InitiatorPlayer;
            
            if(!IsTakeEarning(player, earning)) return;
            AddBalance(player, earning.countMoney, TypeLog.Action, earning.useChatAlert ? "CHAT_ALERT_EARNING_DESTROY_BRADLEY" : default);
        }
        private const String arrowLogReceived = "=====>";
        
        
                
        private void DrawUI_HudPanel(BasePlayer player)
        {
            if (!isUsedUI) return;
            if (player.IsSleeping()) return;
            
            PlayerInfo pInfo = PlayerInfo.Get(player.UserIDString);
            if (pInfo == null) return;
            Configuration.GeneralSetting.InterfaceSetting.PresetUI presetUI = config.generalSetting.interfaceSetting.presetUI;
		   		 		  						   					  						  						   		 		  		 	
            if (pInfo.IsHide)
                DrawUI_StaticIconBalance(player);
            else DrawUI_StaticPanelBalance(player);

            if (presetUI.useTransferButton && config.exchangerSetting.useExchanger)
                DrawUI_StaticExchangerPanel(player);

            if (presetUI.useHideUI)
                DrawUI_StaticHider(player, pInfo.IsHide);
        }
        private const String actionLogRemoved = (LanguageEn ? "written off" : "списано");
        Boolean API_IS_USER(UInt64 userID) => API_IS_USER(userID.ToString());
        
                
                
        private String SQL_Query_SelectedDatabase(String steamId)
        {
            String SelectUser = $"SELECT * FROM `{config.generalSetting.mySQLConnectionSettings.dbTableName}` WHERE steamid = '{steamId}'";
		   		 		  						   					  						  						   		 		  		 	
            return SelectUser;
        }

        private const String actionLogReceived = (LanguageEn ? "received" : "получил");
        private void AddBalance(BasePlayer player, Int32 amount, TypeLog typeLog, String alertMessage = default)
        {
            if (!player) return;
            PlayerInfo pInfo = PlayerInfo.Get(player.UserIDString);
            if (pInfo == null)
                return;
            
            AddBalance(player, pInfo, amount, typeLog, alertMessage);
        }

        void API_SET_BALANCE(BasePlayer player, Int32 balance, ItemContainer itemContainer = null) => AddBalance(player, balance, TypeLog.API);

        private void HookController()
        {
            Configuration.EarningCoins earningCoins = config.earningCoins;

            if (!isUsedUI)
            {
                Unsubscribe(nameof(OnPlayerSleepEnded));
                Unsubscribe(nameof(OnPlayerSleep));
            }

            if (!isUsedSQL)
                Unsubscribe(nameof(CanUserLogin));
            else
            {
                Unsubscribe(nameof(OnNewSave));
                Unsubscribe(nameof(OnPlayerConnected));
            }

            if (config.generalSetting.typeCoins == TypeCoins.Virtual)
            {
                Unsubscribe(nameof(OnLootSpawn));
                Unsubscribe(nameof(OnItemSplit));
            }
            else
            {
                if (!config.generalSetting.physicsItem.droppedItem.isUseDropped)
                    Unsubscribe(nameof(OnLootSpawn));
               
                if (config.generalSetting.physicsItem.skinID == 0 || plugins.Find("Stacks") ||
                    plugins.Find("Loottable") || plugins.Find("CustomSkinsStacksFix") || plugins.Find("SkinBox") ||
                    plugins.Find("StackModifier"))
                {
                    Unsubscribe(nameof(OnPluginLoaded));
                    Unsubscribe(nameof(OnItemSplit));
                }
            }
            
            Boolean anyGather = earningCoins.resourceGatherEarning.Count == 0 || earningCoins.resourceGatherEarning.Any(x => x.Value.useEarning);
            if(!anyGather)
                Unsubscribe(nameof(OnDispenserBonusReceived));
		   		 		  						   					  						  						   		 		  		 	
            Boolean anyCollectable = earningCoins.resourceCollectable.Count == 0 || earningCoins.resourceCollectable.Any(x => x.Value.useEarning);
            if(!anyCollectable)
                Unsubscribe(nameof(OnCollectiblePickedup));
            
            if (!isUsedUI && !earningCoins.killedPlayer.useEarning && !earningCoins.killedNpcs.useEarning && !config.generalSetting.physicsItem.useSaveDeath)
                Unsubscribe(nameof(OnPlayerDeath));
            
            if(!config.generalSetting.physicsItem.useSaveDeath || config.generalSetting.typeCoins == TypeCoins.Virtual)
                Unsubscribe(nameof(OnPlayerRespawned));
            
            if (!earningCoins.killedBradley.useEarning && !earningCoins.killedAnimal.useEarning && !earningCoins.killedBarrels.useEarning)
                Unsubscribe(nameof(OnEntityDeath));

            if (!earningCoins.killedHelicopter.useEarning)
            {
                Unsubscribe(nameof(OnPatrolHelicopterKill));
                Unsubscribe(nameof(OnEntityKill));
            }
        }

		   		 		  						   					  						  						   		 		  		 	
                
        
        
        
                
        private void ChatCommandBalance(BasePlayer player)
        {
            if (!player) return;
            Int32 balancePlayer = GetBalancePlayer(player);
            SendChat(GetLang("CHAT_ALERT_BALANCE", player.UserIDString, balancePlayer), player);
        }
        
                
        
        private class ImageUI
        {
            private const String _path = "IQSystem/IQEconomic/Images/";
            private const String _printPath = "data/" + _path;
            private readonly Dictionary<String, ImageData> _images = new()
            {
                { "PANEL_BALANCE", new ImageData() },
                { "PANEL_EXCAHNGER", new ImageData() },
                { "ICON_BALANCE", new ImageData() },
                { "ICON_EXCHANGER", new ImageData() },
                { "ICON_UNHIDE", new ImageData() },
                { "ICON_HIDE", new ImageData() },
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
                        
                        if(!_.isUsedSQL)
                            foreach (BasePlayer player in BasePlayer.activePlayerList)
                            {
                                _.OnPlayerConnected(player);
                                _.DrawUI_HudPanel(player);
                            }
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
        
        private void DrawUI_UpdateExchangeFull_Menu(BasePlayer player, Int32 balance, Int32 limiteExchanged)
        {
            if (_interface == null)
                return;
            
            String Interface = InterfaceBuilder.GetInterface("UI_Exchanger_Update_FullExchange_Button");
            if (Interface == null)
                return;
            
            Interface = Interface.Replace("%FULL_COURSE_TITLE%", actualyCourse.GetFullBalanceCourseString(player, balance, limiteExchanged));
            
            AddUI(player, Interface);
        }   
        
        private void OnDispenserBonusReceived(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (dispenser == null || !player || item == null) return;
            Dictionary<String, Configuration.EarningCoins.PresetEarning> resourceGather = config.earningCoins.resourceGatherEarning;

            String shortname = item.info.shortname;
            if(!resourceGather.TryGetValue(shortname, out Configuration.EarningCoins.PresetEarning resourcePreset)) return;
            if(!resourcePreset.useEarning) return;
            if(!resourcePreset.IsEarning(player)) return;
                
            AddBalance(player, resourcePreset.countMoney, TypeLog.Action, resourcePreset.useChatAlert ? "CHAT_ALERT_EARNING_GATHER_RESOURCE" : default);
        }

        private enum TypeLog
        {
            API,
            Command,
            Exchanger,
            ExchangerReturned,
            Transfer,
            Action,
            SaveAfterDeath,
            ReturnedSaveAfterDeath,
        }
        
        private Item OnItemSplited(Item item, Int32 amount) //Other plugin hook
        {
            switch (item.skin)
            {
                case 47328465:
                case 57834532:
                case 23156768:
                case 31213265:
                    return null;
            }

            Item x = config.generalSetting.physicsItem.CreatePhysicMoney(amount);
            item.amount -= amount;
            item.MarkDirty();
            return x;
        }
        
        
                
        Boolean API_IS_USER(String userID)
        {
            PlayerInfo playerData = PlayerInfo.Get(userID);
            return playerData != null;
        }
        
        private void DrawUI_StaticPanelBalance(BasePlayer player)
        {
            if (_interface == null)
                return;
            
            DestroyUI(player, InterfaceBuilder.UI_PANEL_STATIC_BALANCE);
            
            List<String> cashedStaticPanel = GetOrSetCacheUI("UI_Balance_Panel_Static");
            if (cashedStaticPanel != null)
            {
                foreach (String uiCached in cashedStaticPanel)
                    AddUI(player, uiCached);
            }
            else
            {
                String Interface = InterfaceBuilder.GetInterface("UI_Balance_Panel_Static");
                if (Interface == null)
                    return;

                List<String> newUI = GetOrSetCacheUI("UI_Balance_Panel_Static", Interface);
                cashedStaticPanel = newUI;

                foreach (String uiCached in cashedStaticPanel)  
                    AddUI(player, uiCached);
            }
            
            DrawUI_StaticTitleBalance(player);
        }
        private void WriteData() => Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("IQSystem/IQEconomic/Exchanger/UpdateCourseTime", courseData);
        private const Int32 timeUpdateCourse = 600;
        private Boolean IsTakeEarning(BasePlayer player, Configuration.EarningCoins.PresetEarning earning) => IsRealPlayer(player) && earning.IsEarning(player);

        private void TransferToPlayer(BasePlayer player, String nameOrID, String amountString)
        {
            if (String.IsNullOrWhiteSpace(nameOrID))
            {
                SendChat(GetLang("CHAT_ALERT_TRANSFER_NO_NAME", player.UserIDString), player);
                return;
            }

            if (String.IsNullOrWhiteSpace(amountString) || !Int32.TryParse(amountString, out Int32 amount))
            {
                SendChat(GetLang("CHAT_ALERT_TRANSFER_INCORRECT_AMOUNT", player.UserIDString), player);
                return;
            }

            (BasePlayer, String) targetPlayer = FindPlayerNameOrID(player, nameOrID);
            if (!targetPlayer.Item1)
            {
                if (targetPlayer.Item2 != null && !String.IsNullOrWhiteSpace(targetPlayer.Item2))
                {
                    SendChat(targetPlayer.Item2, player);
                    return;
                }
                SendChat(GetLang("CHAT_ALERT_TRANSFER_PLAYER_OFFLINE", player.UserIDString), player);
                return;
            }

            if (!IsValidTransfer(player, targetPlayer.Item1, amount))
                return;

            ExecuteTransfer(player, targetPlayer.Item1, amount);
        }
		   		 		  						   					  						  						   		 		  		 	
                
        
                
        private void UpdateExchangedUI(BasePlayer player, Int32 balancePlayer, Int32 limitExchanged)
        {
            if (!exchangerDataPlayer.ContainsKey(player)) return;

            DrawUI_UpdateExchangeBalance_Menu(player, balancePlayer);
            DrawUI_UpdateExchangeOne_Menu(player, balancePlayer, limitExchanged, exchangerDataPlayer[player]);
            DrawUI_UpdateExchangeFull_Menu(player, balancePlayer, limitExchanged);

            if (limitExchanged >= 0)
                DrawUI_UpdateExchangeLimite_Menu(player, limitExchanged);
        }
        
        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<String, String>
            {
                ["BALANCE_TITLE"] = "Balance: {0}",
                ["BALANCE_TITLE_SHORT"] = "${0}",
                ["EXCHANGER_ACTUALY_BALANCE"] = "Your current balance: {0}",
                ["EXCHANGER_ACTUALY_LIMITE"] = "Available withdrawal limit: {0}",
                ["EXCHANGER_STORE_INFO"] = "<color=#CD412B>*</color>You must log in to our store at RustStore.com",
                ["EXCHANGER_COURSE_TITLE_NOT_BALANCE"] = "Insufficient\nfunds",
                ["EXCHANGER_COURSE_TITLE_IS_LIMIT"] = "Limit\nreached",
                ["EXCHANGER_COURSE_TITLE"] = "{0} COIN\n~{1} USD",
                ["EXCHANGER_COURSE_TITLE_SHORT"] = "{0}\n~{1}",
                ["EXCHANGER_COURSE_COIN"] = "{0} COIN",
                ["EXCHANGER_COURSE_STORE_MONEY"] = "~{0} USD",
                ["EXCHANGER_COURSE_NO_UPDATE"] = "∞",
                ["EXCHANGER_COURSE_TITLE_FORMAT_TIME_DAYS"] = "D",
                ["EXCHANGER_COURSE_TITLE_FORMAT_TIME_HOURSE"] = "H",
                ["EXCHANGER_COURSE_TITLE_FORMAT_TIME_MINUTES"] = "M",
                ["EXCHANGER_COURSE_TITLE_FORMAT_TIME_SECONDS"] = "S",

                ["CHAT_ALERT_BALANCE"] = "Your balance: {0}",
                ["CHAT_ALERT_COURSE_INITIALIZE"] = "Exchange rate is not yet ready",
                ["CHAT_ALERT_COURSE_UPDATE"] = "Attention!\nThe exchange rate has been updated. You can check it using the /{0} command",
                ["CHAT_ALERT_EXCHANGED_FAIL"] = "Funds were not exchanged!\nYou may not be logged into the store",
                ["CHAT_ALERT_EXCHANGED_SUCCES"] = "Currency exchange request sent. Your funds will be credited to the store account",

                ["CHAT_ALERT_ONLY_SAFE_ZONE"] = "You can only use this command in the safe zone",
                
                ["CHAT_ALERT_TRANSFER_NO_NAME"] = "You did not specify the player’s nickname or SteamID to whom you wish to send currency",
                ["CHAT_ALERT_TRANSFER_NO_NAME_OR_AMOUNT"] = "You did not specify a nickname and/or quantity to send currency to another player",
                ["CHAT_ALERT_TRANSFER_INCORRECT_AMOUNT"] = "The amount entered for sending to the player is incorrect",
                ["CHAT_ALERT_TRANSFER_PLAYER_OFFLINE"] = "The player is not on the server",
                ["CHAT_ALERT_TRANSFER_NO_ME"] = "You cannot transfer currency to yourself",
                ["CHAT_ALERT_TRANSFER_NO_DOES_BALANCE"] = "You do not have enough funds to send this amount",
                ["CHAT_ALERT_TRANSFERED_SUCCES"] = "You have successfully sent {0} currency to player {1}",
                ["CHAT_ALERT_TRANSFER_ACCEPT_SUCCES"] = "You have successfully received {0} currency from player {1}",
                ["CHAT_ALERT_TRANSFER_MATCHES_PLAYERS"] = "According to your search with the nickname '{0}', several players were found : {1}",

                ["CHAT_ALERT_EARNING_TITLE"] = "You have successfully received {0} currency for <color=#1F6BA0>{1}</color>",
                ["CHAT_ALERT_EARNING_KILLED_PLAYER"] = "killing a player",
                ["CHAT_ALERT_EARNING_KILLED_NPC"] = "killing an NPC",
                ["CHAT_ALERT_EARNING_KILLED_ANIMAL"] = "killing an animal",
                ["CHAT_ALERT_EARNING_DESTROY_HELICOPTER"] = "destroying a helicopter",
                ["CHAT_ALERT_EARNING_DESTROY_BRADLEY"] = "destroying a tank",
                ["CHAT_ALERT_EARNING_DESTROY_BARREL"] = "destroying a barrel",
                ["CHAT_ALERT_EARNING_TIME_TRACKED"] = "time spent on the server",
                ["CHAT_ALERT_EARNING_GATHER_RESOURCE"] = "gathering a resource",
                ["CHAT_ALERT_EARNING_PICK_UP_RESOURCE"] = "picking up a resource",

            }, this);

            lang.RegisterMessages(new Dictionary<String, String>
            {
                ["BALANCE_TITLE"] = "Баланс: {0}",
                ["BALANCE_TITLE_SHORT"] = "${0}",
                ["EXCHANGER_ACTUALY_BALANCE"] = "Ваш актуальный баланс: {0}",
                ["EXCHANGER_ACTUALY_LIMITE"] = "Доступный лимит на вывод средств : {0}",
                ["EXCHANGER_STORE_INFO"] = "<color=#CD412B>*</color>Вы должны авторизоваться в нашем магазине RustStore.com",
                ["EXCHANGER_COURSE_TITLE_NOT_BALANCE"] = "Недостаточно\nсредств",
                ["EXCHANGER_COURSE_TITLE_IS_LIMIT"] = "Достигнут\nлимит",
                ["EXCHANGER_COURSE_TITLE"] = "{0} COIN\n~{1} USD",
                ["EXCHANGER_COURSE_TITLE_SHORT"] = "{0}\n~{1}",
                ["EXCHANGER_COURSE_COIN"] = "{0} COIN",
                ["EXCHANGER_COURSE_STORE_MONEY"] = "~{0} USD",
                ["EXCHANGER_COURSE_NO_UPDATE"] = "∞",
                ["EXCHANGER_COURSE_TITLE_FORMAT_TIME_DAYS"] = "D",
                ["EXCHANGER_COURSE_TITLE_FORMAT_TIME_HOURSE"] = "H",
                ["EXCHANGER_COURSE_TITLE_FORMAT_TIME_MINUTES"] = "M",
                ["EXCHANGER_COURSE_TITLE_FORMAT_TIME_SECONDS"] = "S",
                
                ["CHAT_ALERT_BALANCE"] = "Ваш баланс: {0}",
                ["CHAT_ALERT_COURSE_INITIALIZE"] = "Курс для обмена еще не готов",
                ["CHAT_ALERT_COURSE_UPDATE"] = "Внимание!\nКурс для обмена валюты был обновлен, вы можете посмотреть его использовав команду /{0}",
                ["CHAT_ALERT_EXCHANGED_FAIL"] = "Средства не были обменены!\nВозможно вы не авторизованы в магазине",
                ["CHAT_ALERT_EXCHANGED_SUCCES"] = "Отправлен запрос на обмен валюты, ваши средства поступят на счет магазина",
                
                ["CHAT_ALERT_ONLY_SAFE_ZONE"] = "Вы можете использовать эту команду только в безопасной зоне",
                
                ["CHAT_ALERT_TRANSFER_NO_NAME"] = "Вы не указали ник или SteamID игрока, которому хотите прислать валюту",
                ["CHAT_ALERT_TRANSFER_NO_NAME_OR_AMOUNT"] = "Вы не указали ник и/или количество для отправки валюты другому игроку",
                ["CHAT_ALERT_TRANSFER_INCORRECT_AMOUNT"] = "Вы некорректно указали сумму для отправки игроку",
                ["CHAT_ALERT_TRANSFER_PLAYER_OFFLINE"] = "Данного игрока нет на сервере",
                ["CHAT_ALERT_TRANSFER_NO_ME"] = "Вы не можете передать валюту самому себе",
                ["CHAT_ALERT_TRANSFER_NO_DOES_BALANCE"] = "У вас недостаточно средств для передачи данной суммы",
                ["CHAT_ALERT_TRANSFERED_SUCCES"] = "Вы успешно передали {0} валюты, игроку {1}",
                ["CHAT_ALERT_TRANSFER_ACCEPT_SUCCES"] = "Вы успешно получили {0} валюты, от игрока {1}",
                ["CHAT_ALERT_TRANSFER_MATCHES_PLAYERS"] = "По вашему поиску с ником '{0}', было найдено несколько игроков : {1}",
                
                ["CHAT_ALERT_EARNING_TITLE"] = "Вы успешно получили {0} валюты за <color=#1F6BA0>{1}</color>",
                ["CHAT_ALERT_EARNING_KILLED_PLAYER"] = "убийство игрока",
                ["CHAT_ALERT_EARNING_KILLED_NPC"] = "убийство NPC",
                ["CHAT_ALERT_EARNING_KILLED_ANIMAL"] = "убийство животного",
                ["CHAT_ALERT_EARNING_DESTROY_HELICOPTER"] = "сбитый вертолет",
                ["CHAT_ALERT_EARNING_DESTROY_BRADLEY"] = "уничтоженный танк",
                ["CHAT_ALERT_EARNING_DESTROY_BARREL"] = "уничтожение бочки",
                ["CHAT_ALERT_EARNING_TIME_TRACKED"] = "проведенное время на сервере",
                ["CHAT_ALERT_EARNING_GATHER_RESOURCE"] = "добычу ресурса",
                ["CHAT_ALERT_EARNING_PICK_UP_RESOURCE"] = "поднятие ресурса",
            }, this, "ru");
            
            PrintWarning(LanguageEn ? "Language file loaded successfully" : "Языковой файл загружен успешно");
        }
        
        private Dictionary<UInt64, BasePlayer> heliKilleds = new ();
        private List<Configuration.ExchangerSetting.CourseController> orderedListExchanged;
		   		 		  						   					  						  						   		 		  		 	
                
        
                
        
        private void OnPlayerSleepEnded(BasePlayer player) => DrawUI_HudPanel(player);

        private void DrawUI_UpdateExchangeOne_Menu(BasePlayer player, Int32 balancePlayer, Int32 limiteExchanged, Int32 multiplier = 1)
        {
            if (_interface == null)
                return;
            
            String Interface = InterfaceBuilder.GetInterface("UI_Exchanger_Update_OneExchange_Button");
            if (Interface == null)
                return;
            
            Interface = Interface.Replace("%ONE_COURSE_TITLE%", actualyCourse.GetMultipliedCourseString(player, balancePlayer, multiplier, limiteExchanged));
            
            AddUI(player, Interface);
        }      
        Item API_GET_ITEM(Int32 amount) => config.generalSetting.typeCoins == TypeCoins.Virtual ? null : config.generalSetting.physicsItem.CreatePhysicMoney(amount);
        private Timer timerUpdateCourse = null;

                
        private Boolean IsRealPlayer(BasePlayer player) => player && player.userID.IsSteamId();
        
        
        
        private void SendChat(String Message, BasePlayer player, Chat.ChatChannel channel = Chat.ChatChannel.Global)
        {
            Configuration.OtherPlugins.IQChatSetting chatContoller = config.otherPlugins.settingIQChat;
            if (IQChat)
            {
                if (chatContoller.useUiAlert)
                    IQChat.Call("API_ALERT_PLAYER_UI", player, Message);
                else IQChat.Call("API_ALERT_PLAYER", player, Message, chatContoller.customPrefix, chatContoller.customAvatar);
            }
            else player.SendConsoleCommand("chat.add", channel, 0, Message);
        }
        
        
        
        private void TrackedPlayers()
        {
            Configuration.EarningCoins.PresetEarning timerEarning = config.earningCoins.timeTracked.timeTrackerPreset;
        
            List<BasePlayer> pList = Pool.Get<List<BasePlayer>>();
            pList.AddRange(BasePlayer.activePlayerList);

            foreach (BasePlayer player in pList)
            {
                PlayerInfo pInfo = PlayerInfo.Get(player.UserIDString);
                if (pInfo == null) continue;
                if(!IsOnlyTakeEarning(player, timerEarning)) continue;

                pInfo.Time += timeTracker / 60;
                if (pInfo.Time >= config.earningCoins.timeTracked.timeMinuteGame)
                {
                    AddBalance(player, pInfo, timerEarning.countMoney, TypeLog.Action, timerEarning.useChatAlert ? "CHAT_ALERT_EARNING_TIME_TRACKED" : default);
                    pInfo.Time = 0;
                }
            }
            
            Pool.FreeUnmanaged(ref pList);
        }
        
        private void DrawUI_UpdateExchangeBalance_Menu(BasePlayer player, Int32 balance)
        {
            if (!exchangerDataPlayer.ContainsKey(player)) return;
            if (player.IsSleeping()) return;
            if (_interface == null)
                return;
            
            String Interface = InterfaceBuilder.GetInterface("UI_Exchanger_Update_Balance_Label");
            if (Interface == null)
                return;
            
            Interface = Interface.Replace("%EXCHANGER_ACTUALY_BALANCE%", GetLang("EXCHANGER_ACTUALY_BALANCE", player.UserIDString, balance));
            
            AddUI(player, Interface);
        }   
        private class Configuration
        {
            internal class ExchangerSetting
            {
                [JsonProperty(LanguageEn ? "" : "Разрешить игрокам использовать обмен только в безопасной зоне")]
                public Boolean useEchangeOnlySafeZone;
                [JsonProperty(LanguageEn ? "" : "Отправлять сообщение игрокам об обновлении курса обмена")]
                public Boolean useMessageUpdateCourse;
                [JsonProperty(LanguageEn ? "" : "Разрешить игрокам передавать валюту через команду")]
                public Boolean useTransferP2P;
                [JsonProperty(LanguageEn ? "" : "Использовать возможности вывода валюты на баланс магазина (true - да/false - нет)")]
                public Boolean useExchanger;
                [JsonProperty(LanguageEn ? "" : "Лимит вывода средств игрока (валюты магазина). 0 - отключает лимит")]
                public Int32 limitStoreMoney;
                [JsonProperty(LanguageEn ? "" : "Укажите первый множитель (Минимальный)")]
                public Int32 oneMultiplaier;
                [JsonProperty(LanguageEn ? "" : "Укажите второй множитель (Максимальный)")]
                public Int32 twoMultiplaier;
                [JsonProperty(LanguageEn ? "" : "Настройка курса валюты для обмена в ваш магазин (если вам не нужен сменный, просто оставьте один курс)")]
                public List<CourseController> courseController = new List<CourseController>();
                [JsonProperty(LanguageEn ? "" : "Настройка магазинов")]
                public StoreSettings storeSetting = new();
                
                internal class StoreSettings
                {
                    [JsonProperty(LanguageEn ? "" : "Использовать MoscovOVH (true - да/false - нет)")]
                    public Boolean useMoscovOvh;
                    //public Boolean useSurvivalShop;
                    [JsonProperty(LanguageEn ? "" : "Использовать GameStores (true - да/false - нет)")]
                    public Boolean useGameStores;
                }
                
                internal class CourseController
                {
                    [JsonProperty(LanguageEn ? "" : "Сколько времени должно пройти с смены карты до обновления курса (секунды)")]
                    public Int32 secondsAfterWipe;
                    [JsonProperty(LanguageEn ? "" : "Сколько требуется валюты для обмена")]
                    public Int32 coin;
                    [JsonProperty(LanguageEn ? "" : "Сколько баланса получит игрок за обмен валюты ")]
                    public Int32 storeMoney;

                    public String GetCourseString(BasePlayer player, Int32 playerBalance, Int32 limiteExchanged)
                    {
                        if (limiteExchanged > 0)
                            playerBalance = Math.Min(playerBalance, limiteExchanged); 

                        return playerBalance < coin
                            ? _.GetLang("EXCHANGER_COURSE_TITLE_NOT_BALANCE", player.UserIDString)
                            : _.IsLimiteBalance(player)
                                ? _.GetLang("EXCHANGER_COURSE_TITLE_IS_LIMIT", player.UserIDString)
                                : _.GetLang("EXCHANGER_COURSE_TITLE", player.UserIDString, coin, storeMoney,
                                    limiteExchanged);
                    }
		   		 		  						   					  						  						   		 		  		 	
                    public String GetFullBalanceCourseString(BasePlayer player, Int32 playerBalance, Int32 limiteExchanged)
                    {
                        if (limiteExchanged > 0)
                        {
                            Int32 maxCoinsForLimit = limiteExchanged / storeMoney;
                            playerBalance = Math.Min(playerBalance, maxCoinsForLimit);
                        }

                        (Int32 exchangeCoins, Int32 totalStoreMoney) = GetFullExchangeDetails(playerBalance);
                        return FormatCourseString(exchangeCoins, totalStoreMoney, player, limiteExchanged);
                    }

                    public String GetMultipliedCourseString(BasePlayer player, Int32 playerBalance, Int32 multiplier, Int32 limiteExchanged)
                    {
                        if (limiteExchanged > 0)
                        {
                            Int32 maxCoinsForLimit = limiteExchanged / storeMoney;
                            playerBalance = Math.Min(playerBalance, maxCoinsForLimit);
                        }

                        (Int32 exchangeCoins, Int32 totalStoreMoney) = GetExchangeDetails(playerBalance, multiplier);
                        return FormatCourseString(exchangeCoins, totalStoreMoney, player, limiteExchanged);
                    }
                    
                    private String FormatCourseString(Int32 exchangeCoins, Int32 totalStoreMoney, BasePlayer player,
                        Int32 limiteExchanged)
                    {
                        String langKey =
                            _.IsLimiteBalance(player) ? "EXCHANGER_COURSE_TITLE_IS_LIMIT"
                            : exchangeCoins <= 0 ? "EXCHANGER_COURSE_TITLE_NOT_BALANCE"
                            : exchangeCoins >= 9999999 ? "EXCHANGER_COURSE_TITLE_SHORT"
                            : "EXCHANGER_COURSE_TITLE";

                        return limiteExchanged > 0
                            ? _.GetLang(langKey, player.UserIDString, exchangeCoins, totalStoreMoney, limiteExchanged)
                            : _.GetLang(langKey, player.UserIDString, exchangeCoins, totalStoreMoney);
                    }

                    public (Int32 exchangeCoins, Int32 totalStoreMoney) GetExchangeDetails(Int32 playerBalance,
                        Int32 multiplier)
                    {
                        Int32 exchangeCoins = GetExchangeMoney(playerBalance, multiplier);
                        Int32 totalStoreMoney = (exchangeCoins * storeMoney) / coin; 
		   		 		  						   					  						  						   		 		  		 	
                        return (exchangeCoins, totalStoreMoney);
                    }

                    public (Int32 exchangeCoins, Int32 totalStoreMoney) GetFullExchangeDetails(Int32 playerBalance)
                    {
                        Int32 exchangeCoins = (playerBalance / coin) * coin;
                        Int32 totalStoreMoney = (exchangeCoins * storeMoney) / coin; 
		   		 		  						   					  						  						   		 		  		 	
                        return (exchangeCoins, totalStoreMoney);
                    }

                    public Int32 GetExchangeMoney(Int32 playerBalance, Int32 multiplier)
                    {
                        Int32 desiredExchangeCoins = coin * multiplier;
                        return Math.Min((playerBalance / coin) * coin, desiredExchangeCoins);
                    }
                }

                public String GetLastTimeUpdateCourse(BasePlayer player)
                {
                    if (_.orderedListExchanged is not { Count: > 1 })
                        return _.GetLang("EXCHANGER_COURSE_NO_UPDATE", player.UserIDString);
                    
                    return _.FormatTime(TimeSpan.FromSeconds(CurrentTime - _.courseData.lastUpdateCourseTime), player.UserIDString);
                }
		   		 		  						   					  						  						   		 		  		 	
                public List<CourseController> GetOrderedList() => config.exchangerSetting.courseController.Count == 1 ? config.exchangerSetting.courseController : config.exchangerSetting.courseController.OrderByDescending(x => x.secondsAfterWipe).ToList();
            }
            [JsonProperty(LanguageEn ? "" : "Настройки обменника и перевода валюты между игроками")]
            public ExchangerSetting exchangerSetting = new();
            [JsonProperty(LanguageEn ? "" : "Основные настройки плагина")]
            public GeneralSetting generalSetting = new();
            [JsonProperty(LanguageEn ? "" : "Настройки вознаграждений игроков за действия")]
            public EarningCoins earningCoins = new();

            internal class GeneralSetting
            {
                internal class PhysicsItem
                {
                    [JsonProperty("Shortname")]
                    public String shortname;
                    [JsonProperty("SkinID")]
                    public UInt64 skinID;
                    [JsonProperty(LanguageEn ? "Display Name" : "Отображаемое имя")]
                    public String displayName;
		   		 		  						   					  						  						   		 		  		 	
                    [JsonProperty(LanguageEn ? "" : "Сохранять валюту игрока при его смерти")]
                    public Boolean useSaveDeath;
                    [JsonProperty(LanguageEn ? "" : "Настройка выпадения физической валюты")]
                    public DroppedItem droppedItem = new();
                    
                    internal class DroppedItem
                    {
                        [JsonProperty(LanguageEn ? "" : "Включить выпадение валюты в ящиках")]
                        public Boolean isUseDropped;
                        [JsonProperty(LanguageEn ? "" : "Выпадениее физической валюты в ящиках [ShortPrefabName] = Настройка")]
                        public Dictionary<String, DropSetting> dropItemPresets = new();
                        
                        internal class DropSetting
                        {
                            [JsonProperty(LanguageEn ? "" : "Шанс выпадения (0-100)")]
                            public Int32 rare;
                            [JsonProperty(LanguageEn ? "" : "Минимальное количество")]
                            public Int32 amountMin;
                            [JsonProperty(LanguageEn ? "" : "Максимальное количество")]
                            public Int32 amountMax;

                            public Boolean IsDrooped() => rare >= Core.Random.Range(0, 100);
                            public Int32 GetAmount() => Core.Random.Range(amountMin, amountMax);
                        }
                    }
                    
                    public Item GetDroppedItem(String shortPrefabName)
                    {
                        if (!droppedItem.dropItemPresets.TryGetValue(shortPrefabName, out DroppedItem.DropSetting dropSetting)) return null;
                        return !dropSetting.IsDrooped() ? null : CreatePhysicMoney(dropSetting.GetAmount());
                    }
                    
                    public Item CreatePhysicMoney(Int32 amount = 1)
                    {
                        Item item = ItemManager.CreateByName(shortname, amount, skinID);
                        item.name = displayName;
		   		 		  						   					  						  						   		 		  		 	
                        return item;
                    }

                    public Boolean IsPhysicMoney(Item item) => item.skin == skinID && item.info.shortname == shortname && item.name == displayName;
                }
                [JsonProperty(LanguageEn ? "" : "Удалять все данные игроков при вайпе сервера? (иначе будет автоматическая система очистки встроенная в плагин)")]
                public Boolean clearFullDataFile;
                [JsonProperty(LanguageEn ? "" : "Настройка физической валюты (Тип 1)")]
                public PhysicsItem physicsItem = new PhysicsItem();
                [JsonProperty(LanguageEn ? "MySQL connection settings" : "Настройки соединения с MySQL")]
                public MySQLConnection mySQLConnectionSettings = new MySQLConnection();
                [JsonProperty(LanguageEn ? "Settings UI" : "Настройки интерфейса")]
                public InterfaceSetting interfaceSetting = new InterfaceSetting();
                [JsonProperty(LanguageEn ? "" : "Использовать логирование о получении/списании баланса в файле")]
                public Boolean useLogger;
                [JsonProperty(LanguageEn ? "" : "Укажите тип валюты : 0 - Виртуальная, 1 - Физическая (в виде предмета)")]
                public TypeCoins typeCoins;
                internal class InterfaceSetting
                {
                    [JsonProperty(LanguageEn ? "" : "Настройка дополнений UI")]
                    public PresetUI presetUI;    
                    [JsonProperty(LanguageEn ? "" : "Включить поддержку UI")]
                    public Boolean useUI;
                    [JsonProperty(LanguageEn ? "" : "Настройка цветов UI")]
                    public ColorsSetting colorsUI;     

                    internal class ColorsSetting
                    {
                        [JsonProperty(LanguageEn ? "" : "Цвет текста")]
                        public String colorText; 
                        [JsonProperty(LanguageEn ? "" : "Цвет панелей")]
                        public String colorPanel;
                    }
                    [JsonProperty(LanguageEn ? "" : "Настройка позиций UI")]
                    public PositionsSetting positionUI;

                    internal class PositionsSetting
                    {
                        [JsonProperty(LanguageEn ? "" : "Позиции панели баланса")]
                        public PositionPreset balancePanel;
                        [JsonProperty(LanguageEn ? "" : "Позиции иконки от панели баланса (при скрытии баланса)")]
                        public PositionPreset balanceIconHided;
                        [JsonProperty(LanguageEn ? "" : "Позиции первой дополнительной кнопки")]
                        public PositionPreset firstButton;
                        [JsonProperty(LanguageEn ? "" : "Позиции второй дополнительной кнопки")]
                        public PositionPreset twoButton;
                        internal class PositionPreset
                        {
                            [JsonProperty(LanguageEn ? "" : "AnchorMin")]
                            public String anchorMin;
                            [JsonProperty(LanguageEn ? "" : "AnchorMax")]
                            public String anchorMax;
                            [JsonProperty(LanguageEn ? "" : "OffsetMin")]
                            public String offsetMin;
                            [JsonProperty(LanguageEn ? "" : "OffsetMax")]
                            public String offsetMax;
                        }
                    }
                    internal class PresetUI
                    {
                        [JsonProperty(LanguageEn ? "" : "Включить поддержку UI кнопки для открытия обменника")]
                        public Boolean useTransferButton;
                        [JsonProperty(LanguageEn ? "" : "Включить поддержку скрытия UI")]
                        public Boolean useHideUI;
                    }
                }
                
                internal class MySQLConnection
                {
                    [JsonProperty(LanguageEn ? "Use MySQL instead of a data file (true - yes/false - no)" : "Использовать MySQL вместо дата-файла (true - да/false - нет)")]
                    public Boolean useMySQL;
                    [JsonProperty(LanguageEn ? "Host (IP-Address)" : "Хост (IP-Address)")]
                    public String dbIP;
                    [JsonProperty(LanguageEn ? "Port (default 3306)" : "Порт (стандартно 3306)")]
                    public String dbPort;
                    [JsonProperty(LanguageEn ? "Database name" : "Имя базы данных")]
                    public String dbName;
                    [JsonProperty(LanguageEn ? "Username" : "Имя пользователя")]
                    public String dbUser;
                    [JsonProperty(LanguageEn ? "Password" : "Пароль")]
                    public String dbPassword;
                    [JsonProperty(LanguageEn ? "Table name" : "Название таблицы")]
                    public String dbTableName;

                    public Boolean IsUsedMySQL() => useMySQL && IsFilledMySqlData();

                    public Boolean IsFilledMySqlData() => !String.IsNullOrWhiteSpace(dbIP) &&
                                                          !String.IsNullOrWhiteSpace(dbPort) &&
                                                          !String.IsNullOrWhiteSpace(dbName) &&
                                                          !String.IsNullOrWhiteSpace(dbUser) &&
                                                          !String.IsNullOrWhiteSpace(dbTableName) &&
                                                          !String.IsNullOrWhiteSpace(dbPassword);
                }
            }
            internal class OtherPlugins
            {
                [JsonProperty(LanguageEn ? "" : "Настройка IQChat")]
                public IQChatSetting settingIQChat = new IQChatSetting();
                internal class IQChatSetting
                {
                    [JsonProperty(LanguageEn ? "IQChat : Custom prefix in the chat" : "IQChat : Кастомный префикс в чате")]
                    public String customPrefix;
                    [JsonProperty(LanguageEn ? "IQChat : A custom avatar in the chat. Specify the Steam profile ID (if required)" : "IQChat : Кастомный аватар в чате. Укажите SteamID профиля (если требуется)")]
                    public String customAvatar;
                    [JsonProperty(LanguageEn ? "IQChat : Use UI notifications" : "IQChat : Использовать UI уведомления")]
                    public Boolean useUiAlert;
                }
            }
            [JsonProperty(LanguageEn ? "" : "Команда для открытия обменника и для перевода валюты между игроками")]
            public String commandExchanger;
            
            public static Configuration GetNewConfiguration()
            {
                return new Configuration
                {
                    commandBalance = "balance",
                    commandExchanger = "transfer",
                    
                    generalSetting = new GeneralSetting
                    {
                        clearFullDataFile = false,
                        useLogger = false,
                        typeCoins = TypeCoins.Virtual,
                        interfaceSetting = new GeneralSetting.InterfaceSetting
                        {
                            useUI = true,
                            presetUI = new GeneralSetting.InterfaceSetting.PresetUI
                            {
                                useHideUI = true,
                                useTransferButton = true
                            },
                            colorsUI = new GeneralSetting.InterfaceSetting.ColorsSetting
                            {
                                colorText = "0.969 0.922 0.882 1",
                                colorPanel = "0.969 0.922 0.882 0.03137255" 
                            },
                            positionUI = new GeneralSetting.InterfaceSetting.PositionsSetting
                            {
                                balancePanel = new GeneralSetting.InterfaceSetting.PositionsSetting.PositionPreset
                                {
                                    anchorMin = "1 0",
                                    anchorMax = "1 0",
                                    offsetMin = "-340.067 15.867",
                                    offsetMax = "-238.067 41.867"
                                },
                                balanceIconHided = new GeneralSetting.InterfaceSetting.PositionsSetting.PositionPreset
                                {
                                    anchorMin = "1 0",
                                    anchorMax = "1 0",
                                    offsetMin = "-235.733 15.867",
                                    offsetMax = "-209.733 41.867"
                                },
                                firstButton = new GeneralSetting.InterfaceSetting.PositionsSetting.PositionPreset
                                {
                                    anchorMin = "1 0",
                                    anchorMax = "1 0",
                                    offsetMin = "-235.733765 43.667567",
                                    offsetMax = "-209.73345 69.66776"
                                },
                                twoButton = new GeneralSetting.InterfaceSetting.PositionsSetting.PositionPreset
                                {
                                    anchorMin = "1 0",
                                    anchorMax = "1 0",
                                    offsetMin = "-235.73356 71.84754",
                                    offsetMax = "-209.73365 97.84776"
                                }
                            }
                        },
                        physicsItem = new GeneralSetting.PhysicsItem
                        {
                            shortname = "bleach",
                            skinID = 3351226447,
                            displayName = "COINS",
                            useSaveDeath = false,
                            
                            droppedItem = new GeneralSetting.PhysicsItem.DroppedItem
                            {
                                isUseDropped = false,
                                dropItemPresets = new Dictionary<String, GeneralSetting.PhysicsItem.DroppedItem.DropSetting>()
                                {
                                    ["heli_crate"] = new GeneralSetting.PhysicsItem.DroppedItem.DropSetting
                                    {
                                        rare = 100,
                                        amountMin = 5,
                                        amountMax = 10
                                    },
                                    ["bradley_crate"] = new GeneralSetting.PhysicsItem.DroppedItem.DropSetting
                                    {
                                        rare = 80,
                                        amountMin = 10,
                                        amountMax = 15
                                    },
                                    ["crate_elite"] = new GeneralSetting.PhysicsItem.DroppedItem.DropSetting
                                    {
                                        rare = 70,
                                        amountMin = 2,
                                        amountMax = 3
                                    },
                                    ["crate_normal"] = new GeneralSetting.PhysicsItem.DroppedItem.DropSetting
                                    {
                                        rare = 50,
                                        amountMin = 1,
                                        amountMax = 2
                                    },
                                    ["crate_normal_2"] = new GeneralSetting.PhysicsItem.DroppedItem.DropSetting
                                    {
                                        rare = 50,
                                        amountMin = 1,
                                        amountMax = 2
                                    },
                                    ["crate_basic"] = new GeneralSetting.PhysicsItem.DroppedItem.DropSetting
                                    {
                                        rare = 20,
                                        amountMin = 1,
                                        amountMax = 1
                                    },
                                    ["crate_tools"] = new GeneralSetting.PhysicsItem.DroppedItem.DropSetting
                                    {
                                        rare = 20,
                                        amountMin = 1,
                                        amountMax = 1
                                    },
                                }
                            }
                        },
                        mySQLConnectionSettings = new GeneralSetting.MySQLConnection
                        {
                            useMySQL = false,
                            dbUser = String.Empty,
                            dbPassword = String.Empty,
                            dbIP = String.Empty,
                            dbPort = "3306",
                            dbName = String.Empty,
                            dbTableName = "IQEconomic_Db"
                        }
                    },
                    exchangerSetting = new ExchangerSetting
                    {
                        useMessageUpdateCourse = true,
                        useTransferP2P = true,
                        useExchanger = false,
                        limitStoreMoney = 0,
                        oneMultiplaier = 2,
                        twoMultiplaier = 5,
                        storeSetting = new ExchangerSetting.StoreSettings
                        {
                            useMoscovOvh = false,
                            useGameStores = false,
                        },
                        courseController = new List<ExchangerSetting.CourseController>()
                        {
                            new ExchangerSetting.CourseController()
                            {
                                secondsAfterWipe = 0,
                                coin = 1,
                                storeMoney = 10,
                            }
                        }
                    },
                    earningCoins = new EarningCoins
                    {
                        killedPlayer = new EarningCoins.PresetEarning
                        {
                            useEarning = true,
                            useChatAlert = false,
                            permissionEarning = String.Empty,
                            rare = 100,
                            countMoney = 3
                        },
                        killedAnimal = new EarningCoins.PresetEarning
                        {
                            useEarning = true,
                            useChatAlert = false,
                            permissionEarning = String.Empty,
                            rare = 100,
                            countMoney = 1
                        },
                        killedNpcs = new EarningCoins.PresetEarning
                        {
                            useEarning = true,
                            useChatAlert = false,
                            permissionEarning = String.Empty,
                            rare = 100,
                            countMoney = 2
                        },
                        killedBarrels = new EarningCoins.PresetEarning
                        {
                            useEarning = false,
                            useChatAlert = false,
                            permissionEarning = String.Empty,
                            rare = 20,
                            countMoney = 3
                        },
                        killedBradley = new EarningCoins.PresetEarning
                        {
                            useEarning = true,
                            useChatAlert = true,
                            permissionEarning = String.Empty,
                            rare = 100,
                            countMoney = 10
                        },
                        killedHelicopter = new EarningCoins.PresetEarning
                        {
                            useEarning = true,
                            useChatAlert = true,
                            permissionEarning = String.Empty,
                            rare = 100,
                            countMoney = 15
                        },
                        timeTracked = new EarningCoins.TimeTracker
                        {
                            timeTrackerPreset = new EarningCoins.PresetEarning
                            {
                                useEarning = true,
                                useChatAlert = true,
                                permissionEarning = String.Empty,
                                rare = 100,
                                countMoney = 2
                            },
                            timeMinuteGame = 10
                        },
                        resourceGatherEarning = new Dictionary<String, EarningCoins.PresetEarning>()
                        {
                            ["wood"] = new()
                            {
                                useEarning = true,
                                useChatAlert = false,
                                permissionEarning = String.Empty,
                                rare = 10,
                                countMoney = 1
                            },
                            ["stones"] = new()
                            {
                                useEarning = true,
                                useChatAlert = false,
                                permissionEarning = String.Empty,
                                rare = 15,
                                countMoney = 1
                            },
                        },
                        resourceCollectable = new Dictionary<String, EarningCoins.PresetEarning>
                        {
                            ["wood"] = new()
                            {
                                useEarning = true,
                                useChatAlert = false,
                                permissionEarning = String.Empty,
                                rare = 5,
                                countMoney = 1
                            },
                            ["stones"] = new()
                            {
                                useEarning = true,
                                useChatAlert = false,
                                permissionEarning = String.Empty,
                                rare = 10,
                                countMoney = 1
                            },
                        }
                    },
                    otherPlugins = new OtherPlugins
                    {
                        settingIQChat = new OtherPlugins.IQChatSetting
                        {
                            customPrefix = "[<color=#738D45>IQEconomic</color>] ",
                            customAvatar = "0",
                            useUiAlert = false
                        }
                    }
                };
            }
            [JsonProperty(LanguageEn ? "" : "Настройки взаимодействия с другими плагинами")]
            public OtherPlugins otherPlugins = new();
            [JsonProperty(LanguageEn ? "" : "Команда для просмотра баланса")]
            public String commandBalance;
            internal class EarningCoins
            {
                [JsonProperty(LanguageEn ? "" : "Награда за убийство игроков")]
                public PresetEarning killedPlayer;
                [JsonProperty(LanguageEn ? "" : "Награда за убийство животных")]
                public PresetEarning killedAnimal;
                [JsonProperty(LanguageEn ? "" : "Награда за убийство NPC")]
                public PresetEarning killedNpcs;
                [JsonProperty(LanguageEn ? "" : "Награда за уничтожение танка")]
                public PresetEarning killedBradley;
                [JsonProperty(LanguageEn ? "" : "Награда за уничтожение вертолета")]
                public PresetEarning killedHelicopter;
                [JsonProperty(LanguageEn ? "" : "Награда за уничтожение бочек")]
                public PresetEarning killedBarrels;
                [JsonProperty(LanguageEn ? "" : "Награда за добычу ресурсов")]
                public Dictionary<String, PresetEarning> resourceGatherEarning = new();
                [JsonProperty(LanguageEn ? "" : "Награда за поднятие ресурсов с земли")]
                public Dictionary<String, PresetEarning> resourceCollectable = new();
                [JsonProperty(LanguageEn ? "" : "Награда за проведенное время на сервере")]
                public TimeTracker timeTracked = new();
                
                internal class TimeTracker
                {
                    [JsonProperty(LanguageEn ? "" : "Настройка вознаграждения за время игры на сервере")]
                    public PresetEarning timeTrackerPreset;
                    [JsonProperty(LanguageEn ? "" : "Сколько минут нужно отыграть, чтобы получить награду")]
                    public Int32 timeMinuteGame;
                }

                internal class PresetEarning
                {
                    [JsonProperty(LanguageEn ? "" : "Использовать данный способ получения награды (true - да/false - нет)")]
                    public Boolean useEarning;
                    [JsonProperty(LanguageEn ? "" : "Использовать уведомление о получении валюты в чат игрока (true - да/false - нет)")]
                    public Boolean useChatAlert;
                    [JsonProperty(LanguageEn ? "" : "Разрешения с которым возможно получение награды (если разрешено всем - оставьте поле пустым)")]
                    public String permissionEarning;
                    [JsonProperty(LanguageEn ? "" : "Шанс получения награды (0-100)")]
                    public Int32 rare;
                    [JsonProperty(LanguageEn ? "" : "Количество валюты, которое получит игрок")]
                    public Int32 countMoney;
                    
                    public Boolean IsEarning(BasePlayer player) => (String.IsNullOrWhiteSpace(permissionEarning) || _.permission.UserHasPermission(player.UserIDString, permissionEarning)) && rare >= Core.Random.Range(0, 100);
                }
            }
        }

        private Boolean TryMoveItem(BasePlayer player, Item physicMoney) => physicMoney.MoveToContainer(player.inventory.containerMain) || physicMoney.MoveToContainer(player.inventory.containerBelt);

        private void OnPluginLoaded(Plugin name)
        {
            if (plugins.Find("Stacks") || plugins.Find("Loottable") || plugins.Find("CustomSkinsStacksFix") || plugins.Find("SkinBox") || plugins.Find("StackModifier"))
                Unsubscribe(nameof(OnItemSplit));
        }
        
        private void DrawUI_StaticHider(BasePlayer player, Boolean hideStatus)
        {
            if (_interface == null)
                return;

            String panelType = !hideStatus ? "UI_Hide_Panel_Static" : "UI_UnHide_Panel_Static";
            
            DestroyUI(player, InterfaceBuilder.UI_PANEL_STATIC_HIDER);
            
            List<String> cashedStaticPanel = GetOrSetCacheUI(panelType);
            if (cashedStaticPanel != null)
            {
                foreach (String uiCached in cashedStaticPanel)
                    AddUI(player, uiCached);
            }
            else
            {
                String Interface = InterfaceBuilder.GetInterface(panelType);
                if (Interface == null)
                    return;
                
                List<String> newUI = GetOrSetCacheUI(panelType, Interface);
                cashedStaticPanel = newUI;
		   		 		  						   					  						  						   		 		  		 	
                foreach (String uiCached in cashedStaticPanel)  
                    AddUI(player, uiCached);
            }
        }

        
        
        private Item OnItemSplit(Item item, Int32 amount)
        {
            if (item.skin != config.generalSetting.physicsItem.skinID) return null;

            Item x = config.generalSetting.physicsItem.CreatePhysicMoney(amount);
            item.amount -= amount;
            item.MarkDirty();
            return x;
        }
        public class CourseInfo
        {
            public Double lastUpdateCourseTime = 0;
            public Int32 keyCourse;
        }

                
                
        private void AddBalance(String userID, Int32 amount, TypeLog typeLog)
        {
            if (config.generalSetting.typeCoins == TypeCoins.Physics)
            {
                PrintWarning(LanguageEn ? "" : $"Игрок {userID} не может получить физическую валюту в оффлайне");
                return;
            }
            
            LogAction(userID, amount, typeLog, true);

            if (isUsedSQL)
                GetAndFunctionalBalancePlayerSQL(userID, true, amount);
            else
            {
                PlayerInfo pInfo = PlayerInfo.GetOffline(userID);
                if (pInfo == null)
                    return;
                
                pInfo.Balance += amount;
                
                PlayerInfo.SaveOffline(userID, pInfo);
                
                Interface.Oxide.CallHook("OnAddedBalance", UInt64.Parse(userID), amount, null, pInfo.Balance);
            }
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

        void API_TRANSFERS(String userID, UInt64 transferUserID, Int32 balance)
        {
            if (!UInt64.TryParse(userID, out UInt64 playerID)) return;
            BasePlayer player = BasePlayer.FindByID(playerID);
            if (!player) return;
            
            BasePlayer targetPlayer = BasePlayer.FindByID(transferUserID);
            if (!targetPlayer) return;
            
            TransferToPlayer(player, targetPlayer, balance);
        }

        private void OnPlayerDisconnected(BasePlayer player, String reason) => DisconnectedPlayer(player);

        private Int32 GetBalancePlayer(String userID)
        {
            if (config.generalSetting.typeCoins == TypeCoins.Physics)
                return 0;
            
            PlayerInfo pInfo = PlayerInfo.GetOffline(userID);
            return pInfo?.Balance ?? 0;
        }
                
                
        private void OnPatrolHelicopterKill(PatrolHelicopter patrolHelicopter, HitInfo info)
        {
            UInt64 patrolNetID = patrolHelicopter.net.ID.Value;

            BasePlayer player = info.InitiatorPlayer;
            if (!IsRealPlayer(player)) return;
            heliKilleds[patrolNetID] = player;
        }

        private readonly Core.MySql.Libraries.MySql sqlLibrary = Interface.Oxide.GetLibrary<Core.MySql.Libraries.MySql>();
		void API_TRANSFERS(String userID, String transferUserID, Int32 balance)
        {
            if (!UInt64.TryParse(userID, out UInt64 id)) return;
            BasePlayer player = BasePlayer.FindByID(id);
            if (!player) return;
            
            TransferToPlayer(player, transferUserID, balance);
        }

        
                
        Boolean API_IS_REMOVED_BALANCE(String userID, Int32 amount)
        {
            if (!UInt64.TryParse(userID, out UInt64 id)) return false;
            BasePlayer player = BasePlayer.FindByID(id);
            return player && DoesHaveEnoughBalance(player, amount);
        }
        private static InterfaceBuilder _interface;
        
        private Boolean DoesHaveEnoughBalance(BasePlayer player, Int32 amount = 0)
        {
            Int32 balancePlayer = GetBalancePlayer(player);
            if (balancePlayer == 0) return false;
            return balancePlayer >= amount;
        }
        
        private Boolean IsValidTransfer(BasePlayer player, BasePlayer targetPlayer, Int32 amount)
        {
            if (amount <= 0)
            {
                SendChat(GetLang("CHAT_ALERT_TRANSFER_INCORRECT_AMOUNT", player.UserIDString), player);
                return false;
            }

            if (player.UserIDString.Equals(targetPlayer.UserIDString))
            {
                SendChat(GetLang("CHAT_ALERT_TRANSFER_NO_ME", player.UserIDString), player);
                return false;
            }
		   		 		  						   					  						  						   		 		  		 	
            if (DoesHaveEnoughBalance(player, amount)) return true;
            SendChat(GetLang("CHAT_ALERT_TRANSFER_NO_DOES_BALANCE", player.UserIDString), player);
            return false;
        }

        void API_TRANSFERS(UInt64 userID, BasePlayer transferUser, Int32 balance)
        {
            BasePlayer player = BasePlayer.FindByID(userID);
            if (!player) return;
            
            if (!transferUser) return;
            
            TransferToPlayer(player, transferUser, balance);
        }

                
        
        private static void DestroyUI(BasePlayer player, String elementName)
        {
            if (!player || player.net?.connection == null) return;
            CommunityEntity.ServerInstance.ClientRPC<String>(RpcTarget.Player("DestroyUI", player.net.connection), elementName);
        }
        protected override void SaveConfig() => Config.WriteObject(config);
        
        private void DrawUI_StaticTitleBalance(BasePlayer player)
        {
            if (_interface == null)
                return;
            
            String Interface = InterfaceBuilder.GetInterface("UI_Balance_Title_Static");
            if (Interface == null)
                return;
		   		 		  						   					  						  						   		 		  		 	
            Int32 balance = GetBalancePlayer(player);
            String balanceLang = balance >= 99999999
                ? GetLang("BALANCE_TITLE_SHORT", player.UserIDString, balance)
                : GetLang("BALANCE_TITLE", player.UserIDString, balance);
            Interface = Interface.Replace("%BALANCE_TITLE%", balanceLang);
            
            AddUI(player, Interface);
        }
        
        
        
        Int32 API_GET_BALANCE(String userID)
        {
            if (!UInt64.TryParse(userID, out UInt64 id)) return 0;
            BasePlayer player = BasePlayer.FindByID(id);

            return !player ? GetBalancePlayer(userID) : GetBalancePlayer(player);
        }
        
        private void Init()
        {
            _ = this;
            
            isUsedSQL = config.generalSetting.mySQLConnectionSettings.IsUsedMySQL();
            isUsedUI = config.generalSetting.typeCoins == TypeCoins.Virtual &&
                       config.generalSetting.interfaceSetting.useUI;
            isUsedExchanger = !String.IsNullOrWhiteSpace(config.commandExchanger) && config.exchangerSetting.useExchanger;
            isUsedStores = config.exchangerSetting.storeSetting.useGameStores ||
                           config.exchangerSetting.storeSetting.useMoscovOvh;
            
            if(isUsedExchanger)
                ReadData();
            
            HookController();
        }
        private Connection sqlConnection = null;
        Boolean API_IS_REMOVED_BALANCE(BasePlayer player, Int32 amount) => DoesHaveEnoughBalance(player, amount);
        
        private void DrawUI_UpdateExchangeLimite_Menu(BasePlayer player, Int32 limitExchanged)
        {
            if (!exchangerDataPlayer.ContainsKey(player)) return;
            if (player.IsSleeping()) return;
            if (_interface == null)
                return;
            
            String Interface = InterfaceBuilder.GetInterface("UI_Exchanger_Update_Limite_Label");
            if (Interface == null)
                return;
            
            Interface = Interface.Replace("%EXCHANGER_ACTUALY_LIMITE%", GetLang("EXCHANGER_ACTUALY_LIMITE", player.UserIDString, limitExchanged));   
            
            AddUI(player, Interface);
        }
        private Boolean IsOnlyTakeEarning(BasePlayer player, Configuration.EarningCoins.PresetEarning earning) => earning.IsEarning(player);
        Boolean API_IS_USER(BasePlayer player) => API_IS_USER(player.UserIDString);
        
        private void RemoveBalance(BasePlayer player, Int32 amount, TypeLog typeLog)
        {
            if (!player) return;
            PlayerInfo pInfo = PlayerInfo.Get(player.UserIDString);
            if (pInfo == null) return;
            RemoveBalance(player, pInfo, amount, typeLog);
        }

                
                
        void API_REMOVE_BALANCE(String userID, Int32 balance)
        {
            if (!UInt64.TryParse(userID, out UInt64 id)) return;
            BasePlayer player = BasePlayer.FindByID(id);

            if(!player)
                RemoveBalance(userID, balance, TypeLog.API);
            else RemoveBalance(player, balance, TypeLog.API);
        }
        private Boolean isUsedExchanger = false;
        
        private void DrawUI_UpdateHider(BasePlayer player, Boolean typeHide)
        {
            if (!isUsedUI) return;
            if (player.IsSleeping()) return;   
            if (_interface == null)
                return;

            String panelType = String.Empty;

            if (typeHide)
            {
                DrawUI_StaticIconBalance(player);
                panelType = "UI_UnHide_Panel_Update";
            }
            else
            {
                panelType = "UI_Hide_Panel_Update";
                DrawUI_StaticPanelBalance(player);
            }
            
            List<String> cashedStaticPanel = GetOrSetCacheUI(panelType);
            if (cashedStaticPanel != null)
            {
                foreach (String uiCached in cashedStaticPanel)
                    AddUI(player, uiCached);
            }
            else
            {
                String Interface = InterfaceBuilder.GetInterface(panelType);
                if (Interface == null)
                    return;
                
                List<String> newUI = GetOrSetCacheUI(panelType, Interface);
                cashedStaticPanel = newUI;

                foreach (String uiCached in cashedStaticPanel)  
                    AddUI(player, uiCached);
            }
        }

        
        
        private void SQL_OpenConnection(Boolean migration = false)
        {
            Configuration.GeneralSetting.MySQLConnection sqlInfo = config.generalSetting.mySQLConnectionSettings;
            if(!migration)
                if (!sqlInfo.useMySQL) return;
            
            if (!sqlInfo.IsFilledMySqlData())
            {
                PrintError(LanguageEn ? "You have MySQL support enabled but fixed to make SQL work correctly!" : "У вас включена поддержка MySQL но некоторые поля пустые, исправьте чтобы SQL работал корректно!");
                return;
            }

            sqlConnection = sqlLibrary.OpenDb(sqlInfo.dbIP, Convert.ToInt32(sqlInfo.dbPort), sqlInfo.dbName,
                sqlInfo.dbUser, sqlInfo.dbPassword, this);
            if (sqlConnection == null) return;
            
            Sql checkColumnSql = Sql.Builder.Append(SQL_Query_CheckColumn());
            sqlLibrary.Query(checkColumnSql, sqlConnection, (result) =>
            {
                if (result != null && result.Count > 0 && Convert.ToInt32(result[0]["COUNT(*)"]) == 0)
                {
                    Sql addColumnSql = Sql.Builder.Append(SQL_Query_AddNewColumn());
                    sqlLibrary.Insert(addColumnSql, sqlConnection);
                }
            });
            
            Sql sql = Sql.Builder.Append(SQL_Query_CreatedDatabase());
            sqlLibrary.Insert(sql, sqlConnection);
            
            timerWaitSQL = timer.Once(5f, () =>
            {
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                {
                    if (sqlConnection == null)
                    {
                        PrintError(LanguageEn
                            ? ""
                            : "Невозможно сохранить или получить данные игрока! SQL соединение недоступно....");
                        return;
                    }

                    GetPlayerSQL(player.UserIDString, player);
                }

                if (!migration)
                    Puts(LanguageEn ? "" : "MySQL соединение открыто");
            });
        }
        
        private Boolean IsLimiteBalance(BasePlayer player)
        {
            Int32 limitExchange = config.exchangerSetting.limitStoreMoney;
            if (limitExchange <= 0) return false;
            PlayerInfo pInfo = PlayerInfo.Get(player.UserIDString);
            if (pInfo == null) return false;

            return pInfo.LimitBalance >= limitExchange;
        }
        
                
                
        [ConsoleCommand("iq.eco")]
        private void Funcinal_Rcon_Command(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player)
                if (!player.IsAdmin)
                    return;
            
            if (!arg.HasArgs()) return;

            String type = arg.GetString(0);
            
            String userID = arg.GetString(1);
            if (String.IsNullOrWhiteSpace(userID) || !userID.IsSteamId())
            {
                PrintWarning(LanguageEn ? "" : "Вы некорректно указали SteamID игрока");
                return;
            }
            
            if (!UInt64.TryParse(userID, out UInt64 uintUserID))
            {
                PrintWarning(LanguageEn ? "" : "Вы некорректно указали SteamID игрока");
                return;
            }
            
            BasePlayer targetPlayer = BasePlayer.FindByID(uintUserID);
            Boolean isOfflinePlayer = targetPlayer == null || !targetPlayer.IsConnected;
            
            if (type.Equals("balance"))
            {
                Int32 balance = isOfflinePlayer ? GetBalancePlayer(userID) : GetBalancePlayer(targetPlayer);
                Puts(LanguageEn ? "" : $"[Command] Баланс игрока {userID} составляет : {balance}");
                return;
            }

            if (type.Equals("limit.reset"))
            {
                PlayerInfo pInfo = PlayerInfo.Get(player.UserIDString);
                if (pInfo == null)
                {
                    PrintWarning(LanguageEn ? "" : "Такого игрока нет в базе данных");
                    return;
                }

                pInfo.LimitBalance = 0;
                Puts(LanguageEn ? "" : $"[Command] Лимит игрока {userID} сброшен");
                return;
            }
            
            String amountArg = arg.GetString(2);
            if (String.IsNullOrWhiteSpace(amountArg))
            {
                PrintWarning(LanguageEn ? "" : "Вы не указали количество");
                return;
            }
            
            if(!Int32.TryParse(amountArg, out Int32 amount))
            {
                PrintWarning(LanguageEn ? "" : "Вы некорректно указали количество, оно должно состоять из цифр");
                return;
            }
            
            switch (type)
            {
                case "give":
                {
                    if (isOfflinePlayer)
                        AddBalance(userID, amount, TypeLog.Command);
                    else AddBalance(targetPlayer, amount, TypeLog.Command);
                    
                    Puts(LanguageEn ? "" : $"[Command] Игрок {userID} успешно получил {amount} валюты");
                    break;
                }
		   		 		  						   					  						  						   		 		  		 	
                case "remove":
                {
                    if (isOfflinePlayer)
                        RemoveBalance(userID, amount, TypeLog.Command);
                    else RemoveBalance(targetPlayer, amount, TypeLog.Command);
                    
                    Puts(LanguageEn ? "" : $"[Command] У игрока {userID} успешно списано {amount} валюты");
                    break;
                }
		   		 		  						   					  						  						   		 		  		 	
                case "give.store":
                {
                    if (!isUsedStores)
                    {
                        PrintWarning(LanguageEn ? "" : "У вас не включен никакой магазин, включите в конфигурации настройку для использования данного функционала");
                        return;
                    }
                    
                    StoreAddedBalance(targetPlayer, new ValueTuple<Int32, Int32>(0, amount), uintUserID);
                    Puts(LanguageEn ? "" : $"Отправлен запрос в магазин сервера для зачисления {amount} баланса для игрока {userID}");
                    break;
                }
            }
        }
        
        
                
        private void DrawUI_Exchanger_Menu(BasePlayer player)
        {
            if (!config.exchangerSetting.useExchanger || orderedListExchanged == null || orderedListExchanged.Count == 0) return;
            if (player.IsSleeping()) return;

            if (_interface == null)
                return;

            if (actualyCourse == null)
            {
                SendChat(GetLang("CHAT_ALERT_COURSE_INITIALIZE", player.UserIDString), player);
                return;
            }

            exchangerDataPlayer.TryAdd(player, 1);
            
            String Interface = InterfaceBuilder.GetInterface("UI_Exchanger_Menu_Panel_Static");
            if (Interface == null)
                return;

            Configuration.ExchangerSetting exchangerSetting = config.exchangerSetting;
            Int32 balance = GetBalancePlayer(player);
            Int32 limitExchanged = GetLimiteExchanged(player);
            String limitExchangedTitle = limitExchanged == -1 ? String.Empty : GetLang("EXCHANGER_ACTUALY_LIMITE", player.UserIDString, limitExchanged);
            
            Interface = Interface.Replace("%EXCHANGER_ACTUALY_LIMITE%", limitExchangedTitle);
            Interface = Interface.Replace("%EXCHANGER_ACTUALY_BALANCE%", GetLang("EXCHANGER_ACTUALY_BALANCE", player.UserIDString, balance));
            Interface = Interface.Replace("%EXCHANGER_STORE_INFO%", GetLang("EXCHANGER_STORE_INFO", player.UserIDString, balance));
            Interface = Interface.Replace("%MULTIPLIER_ONE%", $"{exchangerSetting.oneMultiplaier}");
            Interface = Interface.Replace("%MULTIPLIER_TWO%", $"{exchangerSetting.twoMultiplaier}");
            Interface = Interface.Replace("%ONE_COURSE_TITLE%", actualyCourse.GetCourseString(player, balance, limitExchanged));
            Interface = Interface.Replace("%FULL_COURSE_TITLE%", actualyCourse.GetFullBalanceCourseString(player, balance, limitExchanged));
            Interface = Interface.Replace("%EXCHANGER_COURSE_COIN%", GetLang("EXCHANGER_COURSE_COIN", player.UserIDString, actualyCourse.coin));
            Interface = Interface.Replace("%EXCHANGER_COURSE_STORE_MONEY%", GetLang("EXCHANGER_COURSE_STORE_MONEY", player.UserIDString, actualyCourse.storeMoney));
            Interface = Interface.Replace("%LAST_TIME_UPDATE_COURSE%", exchangerSetting.GetLastTimeUpdateCourse(player));

            AddUI(player, Interface);
        }     

        void API_TRANSFERS(BasePlayer player, String transferUserID, Int32 balance)
        {
            if (!player) return;
            TransferToPlayer(player, transferUserID, balance);
        }
        
        private void DrawUI_StaticExchangerPanel(BasePlayer player)
        {
            if (_interface == null)
                return;
            
            DestroyUI(player, InterfaceBuilder.UI_PANEL_STATIC_EXCHANGER);
            
            List<String> cashedStaticPanel = GetOrSetCacheUI("UI_Exchanger_Panel_Static");
            if (cashedStaticPanel != null)
            {
                foreach (String uiCached in cashedStaticPanel)
                    AddUI(player, uiCached);
            }
            else
            {
                String Interface = InterfaceBuilder.GetInterface("UI_Exchanger_Panel_Static");
                if (Interface == null)
                    return;
		   		 		  						   					  						  						   		 		  		 	
                List<String> newUI = GetOrSetCacheUI("UI_Exchanger_Panel_Static", Interface);
                cashedStaticPanel = newUI;

                foreach (String uiCached in cashedStaticPanel)  
                    AddUI(player, uiCached);
            }
        }
        
        
        
        private void ChatCommandExchanger(BasePlayer player, String cmd, String[] arg)
        {
            if (!player) return;
            if (!permission.UserHasPermission(player.UserIDString, transferPrivilage)) return;

            if (config.exchangerSetting.useEchangeOnlySafeZone && !player.InSafeZone())
            {
                SendChat(GetLang("CHAT_ALERT_ONLY_SAFE_ZONE", player.UserIDString), player);
                return;
            }

            if (arg == null || arg.Length < 2 || !config.exchangerSetting.useTransferP2P)
            {
                if (config.exchangerSetting.useExchanger)
                    DrawUI_Exchanger_Menu(player);
                else SendChat(GetLang("CHAT_ALERT_TRANSFER_NO_NAME_OR_AMOUNT", player.UserIDString), player);
                return;
            }

            String playerOrID = arg[0];
            String amountString = arg[1];

            TransferToPlayer(player, playerOrID, amountString);
        }

        void API_TRANSFERS(String userID, BasePlayer transferUser, Int32 balance)
        {
            if (!UInt64.TryParse(userID, out UInt64 playerID)) return;
            BasePlayer player = BasePlayer.FindByID(playerID);
            if (!player) return;
            
            if (!transferUser) return;
            
            TransferToPlayer(player, transferUser, balance);
        }
        
        private void RemoveBalance(BasePlayer player, PlayerInfo pInfo, Int32 amount, TypeLog typeLog)
        {
            if (!player) return;

            if (config.generalSetting.typeCoins == TypeCoins.Physics)
            {
                Int32 balancePlayer = GetBalancePlayer(player);

                if (!DoesHaveEnoughBalance(balancePlayer))
                {
                    RemovePhysicMoney(player, balancePlayer);
                    LogAction(player, balancePlayer, typeLog, false);
                }
                else
                {
                    RemovePhysicMoney(player, amount);
                    LogAction(player, amount, typeLog, false);
                }
                
                UpdateExchangedUI(player, balancePlayer, GetLimiteExchanged(pInfo));
                Interface.Oxide.CallHook("OnRemovedBalance", player.userID.Get(), amount, player, pInfo.Balance);
                return;
            }
            
            if (!DoesHaveEnoughBalance(pInfo.Balance))
            {
                pInfo.Balance = 0;
                LogAction(player, pInfo.Balance, typeLog, false);
            }
            else
            {
                pInfo.Balance -= amount;
                LogAction(player, amount, typeLog, false);
            }
            
            DrawUI_UpdateTitleBalance(player, pInfo);
            UpdateExchangedUI(player, pInfo.Balance, GetLimiteExchanged(pInfo));
            Interface.Oxide.CallHook("OnRemovedBalance", player.userID.Get(), amount, player, pInfo.Balance);
        }
        private ImageUI _imageUI;

        private void OnServerInitialized()
        {
            RegisteredPermission();
            
            if (isUsedSQL)
                SQL_OpenConnection();
            
            if (config.earningCoins.timeTracked.timeTrackerPreset.useEarning)
                timerTrackerTimes = timer.Every(timeTracker, TrackedPlayers);
            
            if (config.generalSetting.interfaceSetting.useUI)
            {
                _imageUI = new ImageUI();
                _imageUI.DownloadImage();
            }
            else
            {
                if (!_.isUsedSQL)
                    foreach (BasePlayer player in BasePlayer.activePlayerList)
                        OnPlayerConnected(player);
            }

            cmd.AddChatCommand(config.commandBalance, this, nameof(ChatCommandBalance));
            cmd.AddConsoleCommand(config.commandBalance, this, nameof(ConsoleCommandBalance));
            
            if (isUsedExchanger)
            {
                orderedListExchanged = config.exchangerSetting.GetOrderedList();
                
                UpdateExchangerCourse();
                timerUpdateCourse = timer.Every(timeUpdateCourse, UpdateExchangerCourse);
                
                cmd.AddChatCommand(config.commandExchanger, this, nameof(ChatCommandExchanger));
                cmd.AddConsoleCommand(config.commandExchanger, this, nameof(ConsoleCommandExchanger));
            }
            else if (config.exchangerSetting.useTransferP2P)
            {
                cmd.AddChatCommand(config.commandExchanger, this, nameof(ChatCommandExchanger));
                cmd.AddConsoleCommand(config.commandExchanger, this, nameof(ConsoleCommandExchanger));
            }
        }
        
        private void ExchangedProcess(BasePlayer player, (Int32 exchangeCoins, Int32 storeMoney) exchangeDetail)
        {
            PlayerInfo pInfo = PlayerInfo.Get(player.UserIDString);
            if (pInfo == null) return;
            if (IsLimiteBalance(pInfo)) return;
            
            exchangerDataPlayer[player] = 1;
            StoreAddedBalance(player, exchangeDetail);
        }

        private void OnEntityKill(PatrolHelicopter patrolHelicopter)
        {
            Configuration.EarningCoins.PresetEarning earning = config.earningCoins.killedHelicopter;
            if (!earning.useEarning) return;
            
            UInt64 patrolNetID = patrolHelicopter.net.ID.Value;

            heliKilleds.Remove(patrolNetID, out BasePlayer player);
     
            if (!player)
            {
                player = patrolHelicopter.myAI._targetList is { Count: > 0 } targetList
                    ? targetList[targetList.Count - 1].ply
                    : null;
            }
            
            if(!IsTakeEarning(player, earning)) return;
            AddBalance(player, earning.countMoney, TypeLog.Action, earning.useChatAlert ? "CHAT_ALERT_EARNING_DESTROY_HELICOPTER" : default);
        }
        
        private void Unload()
        {
            if (_ == null) return;

            if (coroutineMigrate != null)
            {
                ServerMgr.Instance.StopCoroutine(coroutineMigrate);
                coroutineMigrate = null;
            }
            else
            {
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                    DisconnectedPlayer(player);
            }

            if (!String.IsNullOrWhiteSpace(config.commandExchanger) && config.exchangerSetting.useExchanger)
                WriteData();

            if (saveAfterDeath != null)
            {
                saveAfterDeath.Clear();
                saveAfterDeath = null;
            }
            
            if (heliKilleds != null)
            {
                heliKilleds.Clear();
                heliKilleds = null;
            }

            if (timerTrackerTimes is { Destroyed: false })
            {
                timerTrackerTimes.Destroy();
                timerTrackerTimes = null;
            }       
            
            if (timerWaitSQL is { Destroyed: false })
            {
                timerWaitSQL.Destroy();
                timerWaitSQL = null;
            }
            
            if (timerUpdateCourse is { Destroyed: false })
            {
                timerUpdateCourse.Destroy();
                timerUpdateCourse = null;
            }
            
            if (sqlConnection != null && sqlLibrary != null)
            {
                sqlLibrary.CloseDb(sqlConnection);
                sqlConnection = null;
            }
            
            InterfaceBuilder.DestroyAll();
            
            if (_imageUI != null)
            {
                _imageUI.UnloadImages();
                _imageUI = null;
            }
            
            if (cachedUI != null)
            {
                cachedUI.Clear();
                cachedUI = null;
            }

            if (exchangerDataPlayer != null)
            {
                exchangerDataPlayer.Clear();
                exchangerDataPlayer = null;
            }
            
            _ = null;
        }
        
        private void ConnectedPlayer(BasePlayer player)
        {
            PlayerInfo pInfo = PlayerInfo.Load(player.UserIDString);
            
            if (pInfo != null)
            {
                pInfo.DateTime = CurrentTime;
                return;
            }
            
            PlayerInfo.Import(player.UserIDString, new PlayerInfo
            {
                Balance = 0,
                LimitBalance = 0,
                Time = 0,
                DateTime = CurrentTime,
                IsHide = false,
            });
            
            PlayerInfo.Save(player.UserIDString);
        }
        private Boolean isUsedUI = false;

        private List<UInt64> GetFriendList(BasePlayer targetPlayer)
        {
            List<UInt64> friendList = Pool.Get<List<UInt64>>();

            if (Friends)
            {
                if (Friends.Call("GetFriends", targetPlayer.userID.Get()) is UInt64[] friends)
                    friendList.AddRange(friends);
            }

            if (Clans)
            {
                if (Clans.Call("GetClanMembers", targetPlayer.UserIDString) is UInt64[] clanMembers)
                    friendList.AddRange(clanMembers);
            }

            if (targetPlayer.Team != null)
                friendList.AddRange(targetPlayer.Team.members);

            return friendList;
        }

                
        
        
                
        [ConsoleCommand("migrate.data")] 
        private void MigrateCommand(ConsoleSystem.Arg arg)
        {
            if(arg.Player() != null)
                if (!arg.Player().IsAdmin)
                    return;
            
            if (!config.generalSetting.mySQLConnectionSettings.IsFilledMySqlData())
            {
                PrintError(LanguageEn ? "You have not filled in the data from MySQL" : "Вы не заполнили данные от MySQL");
                return;
            }
        
            coroutineMigrate = ServerMgr.Instance.StartCoroutine(MigrateProcess());
        }

                
        private Boolean IsFriends(BasePlayer player, UInt64 targetID)
        {
            List<UInt64> friendList = GetFriendList(player);
            Boolean isFriend = friendList != null && friendList.Contains(targetID);
    
            Pool.FreeUnmanaged(ref friendList);
            
            return isFriend;
        }
        private Dictionary<BasePlayer, Int32> exchangerDataPlayer = new();
        
                
        private Int32 GetBalancePlayer(BasePlayer player)
        {
            if (!player) return 0;
            if (config.generalSetting.typeCoins == TypeCoins.Physics)
                return GetPlayerPhysicMoney(player);
            
            PlayerInfo pInfo = PlayerInfo.Get(player.UserIDString);
            return pInfo?.Balance ?? 0;
        }

        
        
        private void StoreAddedBalance(BasePlayer player, (Int32 exchangeCoins, Int32 storeMoney) exchangeDetail, UInt64 userID = 0)
        {
            MoscovOVHBalanceSet(player, exchangeDetail, userID);
            GameStoreBalanceSet(player, exchangeDetail, userID);
        }

        private void TransferToPlayer(BasePlayer player, String nameOrID, Int32 amount)
        {
            if (String.IsNullOrWhiteSpace(nameOrID))
            {
                SendChat(GetLang("CHAT_ALERT_TRANSFER_NO_NAME", player.UserIDString), player);
                return;
            }

            BasePlayer targetPlayer = BasePlayer.Find(nameOrID);
            if (targetPlayer == null)
            {
                SendChat(GetLang("CHAT_ALERT_TRANSFER_PLAYER_OFFLINE", player.UserIDString), player);
                return;
            }

            if (!IsValidTransfer(player, targetPlayer, amount))
                return;

            ExecuteTransfer(player, targetPlayer, amount);
        }
        
        private void ConsoleCommandExchanger(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if(!player) return;
            if (!permission.UserHasPermission(player.UserIDString, transferPrivilage)) return;

            if (config.exchangerSetting.useEchangeOnlySafeZone && !player.InSafeZone())
            {
                SendChat(GetLang("CHAT_ALERT_ONLY_SAFE_ZONE", player.UserIDString), player);
                return;
            }
            
            if (arg?.Args == null || !arg.HasArgs(2) || !config.exchangerSetting.useTransferP2P)
            {
                if (config.exchangerSetting.useExchanger)
                    DrawUI_Exchanger_Menu(player);
                else SendChat(GetLang("CHAT_ALERT_TRANSFER_NO_NAME_OR_AMOUNT", player.UserIDString), player);
                return;
            }

            String playerOrID = arg.GetString(0);
            String amountString = arg.GetString(1);

            TransferToPlayer(player, playerOrID, amountString);
        }
        
                
                
        private static Configuration config = new Configuration();
        
        
                
        private Boolean IsDuel(UInt64 userID)
        {
            Object obj = Interface.Oxide.RootPluginManager.GetPlugin("AimTraining")?.CallHook("IsArenaPlayer", userID);
            if (obj is Boolean) return (Boolean)obj;
            if (Battles) return (Boolean)Battles?.Call("IsPlayerOnBattle", userID);
            if (Duel) return (Boolean)Duel?.Call("IsPlayerOnActiveDuel", BasePlayer.FindByID(userID));
            if (Duelist) return (Boolean)Duelist?.Call("inEvent", BasePlayer.FindByID(userID));
            if (ArenaTournament) return ArenaTournament.Call<Boolean>("IsOnTournament", userID);
            if (XFarmRoom) return XFarmRoom.Call<Boolean>("API_PlayerInRoom", userID);
            return false;
        }

        private const Int32 timeTracker = 60;

        protected override void LoadDefaultConfig() => config = Configuration.GetNewConfiguration();

        Int32 API_GET_BALANCE(UInt64 userID)
        {
            BasePlayer player = BasePlayer.FindByID(userID);
            return !player ? GetBalancePlayer(userID.ToString()) : GetBalancePlayer(player);
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();

                if (config.earningCoins.killedBarrels == null)
                {
                    config.earningCoins.killedBarrels = new Configuration.EarningCoins.PresetEarning()
                    {
                        useEarning = false,
                        permissionEarning = String.Empty,
                        rare = 20,
                        countMoney = 3
                    };
                }

                if (config.generalSetting.interfaceSetting.colorsUI == null)
                {
                    config.generalSetting.interfaceSetting.colorsUI =
                        new Configuration.GeneralSetting.InterfaceSetting.ColorsSetting()
                        {
                            colorText = "0.969 0.922 0.882 1",
                            colorPanel = "0.969 0.922 0.882 0.03137255"
                        };
                }

                if (config.generalSetting.interfaceSetting.positionUI == null)
                {
                    config.generalSetting.interfaceSetting.positionUI =
                        new Configuration.GeneralSetting.InterfaceSetting.PositionsSetting()
                        {
                            balanceIconHided = new Configuration.GeneralSetting.InterfaceSetting.PositionsSetting.PositionPreset()
                            {
                                anchorMin = "1 0",
                                anchorMax = "1 0",
                                offsetMin = "-235.733 15.867",
                                offsetMax = "-209.733 41.867"
                            },
                            balancePanel = new Configuration.GeneralSetting.InterfaceSetting.PositionsSetting.PositionPreset
                            {
                                anchorMin = "1 0",
                                anchorMax = "1 0",
                                offsetMin = "-340.067 15.867",
                                offsetMax = "-238.067 41.867"
                            },
                            firstButton = new Configuration.GeneralSetting.InterfaceSetting.PositionsSetting.PositionPreset
                            {
                                anchorMin = "1 0",
                                anchorMax = "1 0",
                                offsetMin = "-235.733765 43.667567",
                                offsetMax = "-209.73345 69.66776"
                            },
                            twoButton = new Configuration.GeneralSetting.InterfaceSetting.PositionsSetting.PositionPreset
                            {
                                anchorMin = "1 0",
                                anchorMax = "1 0",
                                offsetMin = "-235.73356 71.84754",
                                offsetMax = "-209.73365 97.84776"
                            }
                        };
                }
            }
            catch
            {
                PrintWarning(LanguageEn ? "Error #49" + $"reading the configuration 'oxide/config/{Name}', creating a new configuration! #33" : "Ошибка #49" + $"чтения конфигурации 'oxide/config/{Name}', создаём новую конфигурацию! #33");
                LoadDefaultConfig();
            }
            NextTick(SaveConfig);
        }

                
                
        
                
        private class InterfaceBuilder
        {
            
            public static InterfaceBuilder Instance;
            public const String UI_PANEL_STATIC_BALANCE = "UI_PANEL_STATIC_BALANCE";
            public const String UI_PANEL_STATIC_EXCHANGER = "UI_PANEL_STATIC_EXCHANGER";
            public const String UI_PANEL_STATIC_EXCHANGER_MENU = "UI_PANEL_STATIC_EXCHANGER_MENU";
            public const String UI_PANEL_STATIC_HIDER = "UI_PANEL_STATIC_HIDER";
            public Dictionary<String, String> Interfaces;
            
            private const String FontMaterial = "assets/content/ui/namefontmaterial.mat";
            
            private readonly String colorPanel = config.generalSetting.interfaceSetting.colorsUI.colorPanel; 
            private readonly String colorText = config.generalSetting.interfaceSetting.colorsUI.colorText; 
            
            private readonly String OffsetMinExchangerDefault = config.generalSetting.interfaceSetting.positionUI.firstButton.offsetMin;
            private readonly String OffsetMaxExchangerDefault = config.generalSetting.interfaceSetting.positionUI.firstButton.offsetMax;
            private readonly String OffsetMinHiderDefault = config.generalSetting.interfaceSetting.positionUI.twoButton.offsetMin;
            private readonly String OffsetMaxHiderDefault = config.generalSetting.interfaceSetting.positionUI.twoButton.offsetMax;

            private readonly String AnchorMinExchanger = config.generalSetting.interfaceSetting.positionUI.firstButton.anchorMin;
            private readonly String AnchorMaxExchanger = config.generalSetting.interfaceSetting.positionUI.firstButton.anchorMax;
            private readonly String AnchorMinHider = config.generalSetting.interfaceSetting.positionUI.twoButton.anchorMin;
            private readonly String AnchorMaxHider = config.generalSetting.interfaceSetting.positionUI.twoButton.anchorMax;
            
            private String anchorMinExchanger = String.Empty;
            private String anchorMaxExchanger = String.Empty;
            private String anchorMinHider = String.Empty;
            private String anchorMaxHider = String.Empty;
            
            private String offsetMinExchanger = String.Empty;
            private String offsetMaxExchanger = String.Empty;
            private String offsetMinHider = String.Empty;
            private String offsetMaxHider = String.Empty;
            
            
            
            public InterfaceBuilder()
            {
                Instance = this;
                Interfaces = new Dictionary<String, String>();

                Configuration.GeneralSetting.InterfaceSetting.PresetUI presetUI = config.generalSetting.interfaceSetting.presetUI;
                SetOffsets(presetUI.useTransferButton && config.exchangerSetting.useExchanger, presetUI.useHideUI);
                
                Building_StaticMenuBalance();
                Building_StaticIconBalance();
                
                Building_StaticTitleBalance();
                Building_UpdateTitleBalance();
                
                Building_StaticHider("UI_Hide_Panel_Static", "ICON_HIDE");
                Building_UpdateHider("UI_Hide_Panel_Update", "ICON_HIDE");
                
                Building_StaticHider("UI_UnHide_Panel_Static", "ICON_UNHIDE");
                Building_UpdateHider("UI_UnHide_Panel_Update", "ICON_UNHIDE");

                Building_StaticButtonExchanger();
                Building_StaticMenuExchanger();
                Building_UpdateBalance();
                Building_UpdateLimite();
                Building_UpdateCourseCoin();
                Building_UpdateCourseStoreMoney();
                Building_UpdateCourseTime();
                Building_UpdateButtonExchangeAll();
                Building_UpdateButtonExchangeOne();
            }

            private void SetOffsets(Boolean useExchangerButton, Boolean useHideUI)
            {
                if (useExchangerButton && useHideUI)
                {
                    anchorMinExchanger = AnchorMinExchanger;
                    anchorMaxExchanger = AnchorMaxExchanger;

                    anchorMinHider = AnchorMinHider;
                    anchorMaxHider = AnchorMaxHider;
                    
                    offsetMinExchanger = OffsetMinExchangerDefault;
                    offsetMaxExchanger = OffsetMaxExchangerDefault;

                    offsetMinHider = OffsetMinHiderDefault;
                    offsetMaxHider = OffsetMaxHiderDefault;
                }
                else if (useExchangerButton || useHideUI)
                {
                    anchorMinExchanger = AnchorMinExchanger;
                    anchorMaxExchanger = AnchorMaxExchanger;

                    anchorMinHider = anchorMinExchanger;
                    anchorMaxHider = anchorMaxExchanger;
                    
                    offsetMinExchanger = OffsetMinExchangerDefault;
                    offsetMaxExchanger = OffsetMaxExchangerDefault;

                    offsetMinHider = offsetMinExchanger;
                    offsetMaxHider = offsetMaxExchanger;
                }
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
                    DestroyPlayerUI(player);
                }
            }

            public static void DestroyPlayerUI(BasePlayer player)
            {
                DestroyUI(player, UI_PANEL_STATIC_BALANCE);
                DestroyUI(player, UI_PANEL_STATIC_HIDER);
                DestroyUI(player, UI_PANEL_STATIC_EXCHANGER);
                DestroyUI(player, UI_PANEL_STATIC_EXCHANGER_MENU);
            }
            
            
                        
            private void Building_StaticMenuBalance()
            {
                CuiElementContainer container = new CuiElementContainer();
                
                container.Add(new CuiElement
                {
                    Name = UI_PANEL_STATIC_BALANCE,
                    Parent = "Overlay",
                    DestroyUi = UI_PANEL_STATIC_BALANCE,
                    Components = {
                        new CuiRawImageComponent { Color = colorPanel, Png = _._imageUI.GetImage("PANEL_BALANCE"), Material = FontMaterial },
                        new CuiRectTransformComponent { AnchorMin = config.generalSetting.interfaceSetting.positionUI.balancePanel.anchorMin, AnchorMax = config.generalSetting.interfaceSetting.positionUI.balancePanel.anchorMax, OffsetMin = config.generalSetting.interfaceSetting.positionUI.balancePanel.offsetMin, OffsetMax = config.generalSetting.interfaceSetting.positionUI.balancePanel.offsetMax }
                    }
                });

                container.Add(new CuiElement
                {
                    Name = "MINI_PANEL_ICON_BALANCE",
                    Parent = UI_PANEL_STATIC_BALANCE,
                    DestroyUi = "MINI_PANEL_ICON_BALANCE",
                    Components = {
                        new CuiImageComponent { Color = colorPanel, Material = FontMaterial },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "53.333 -13", OffsetMax = "79.333 13" }
                    }
                });
                
                container.Add(new CuiElement
                {
                    Name = "ICON_BALANCE",
                    Parent = "MINI_PANEL_ICON_BALANCE",
                    DestroyUi = "ICON_BALANCE",
                    Components = {
                        new CuiRawImageComponent { Color = "1 1 1 1", Png = _._imageUI.GetImage("ICON_BALANCE") },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" }
                    }
                });
                
                AddInterface("UI_Balance_Panel_Static", container.ToJson());
            }
            
            private void Building_StaticIconBalance()
            {
                CuiElementContainer container = new CuiElementContainer();
                
                container.Add(new CuiElement
                {
                    Name = UI_PANEL_STATIC_BALANCE,
                    Parent = "Overlay",
                    DestroyUi = UI_PANEL_STATIC_BALANCE,
                    Components = {
                        new CuiImageComponent { Color = colorPanel, Material = FontMaterial },
                        new CuiRectTransformComponent { AnchorMin = config.generalSetting.interfaceSetting.positionUI.balanceIconHided.anchorMin, AnchorMax = config.generalSetting.interfaceSetting.positionUI.balanceIconHided.anchorMax, OffsetMin = config.generalSetting.interfaceSetting.positionUI.balanceIconHided.offsetMin, OffsetMax = config.generalSetting.interfaceSetting.positionUI.balanceIconHided.offsetMax }
                    }
                });
                
                container.Add(new CuiElement
                {
                    Name = "ICON_BALANCE",
                    Parent = UI_PANEL_STATIC_BALANCE,
                    DestroyUi = "ICON_BALANCE",
                    Components = {
                        new CuiRawImageComponent { Color = "1 1 1 1", Png = _._imageUI.GetImage("ICON_BALANCE") },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" }
                    }
                });
                
                AddInterface("UI_Balance_Panel_Icon_Static", container.ToJson());
            }

                        
            private void Building_StaticTitleBalance()
            {
                CuiElementContainer container = new CuiElementContainer();
                
                container.Add(new CuiElement
                {
                    Name = "BALANCE_TITLE",
                    Parent = UI_PANEL_STATIC_BALANCE,
                    DestroyUi = "BALANCE_TITLE",
                    Components = {
                        new CuiTextComponent { Text = "%BALANCE_TITLE%", Font = "robotocondensed-bold.ttf", FontSize = 12, Align = TextAnchor.MiddleLeft, Color = colorText },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6.794 0.333", OffsetMax = "-1.807 -0.333" }
                    }
                });
                
                AddInterface("UI_Balance_Title_Static", container.ToJson());
            }
            
            private void Building_UpdateTitleBalance()
            {
                CuiElementContainer container = new CuiElementContainer();
                
                container.Add(new CuiElement
                {
                    Name = "BALANCE_TITLE",
                    Parent = UI_PANEL_STATIC_BALANCE,
                    Update = true,
                    Components = {
                        new CuiTextComponent { Text = "%BALANCE_TITLE%" },
                    }
                });
                
                AddInterface("UI_Balance_Title_Update", container.ToJson());
            }
            
                        
            
            
                        
            private void Building_StaticButtonExchanger()
            {
                CuiElementContainer container = new CuiElementContainer();
                
                container.Add(new CuiElement
                {
                    Name = UI_PANEL_STATIC_EXCHANGER,
                    Parent = "Overlay",
                    DestroyUi = UI_PANEL_STATIC_EXCHANGER,
                    Components = {
                        new CuiImageComponent { Color = colorPanel, Material = FontMaterial },
                        new CuiRectTransformComponent { AnchorMin = anchorMinExchanger, AnchorMax = anchorMaxExchanger, OffsetMin = offsetMinExchanger, OffsetMax = offsetMaxExchanger }
                    }
                });
                
                container.Add(new CuiElement
                {
                    Name = "ICON_EXCHANGER",
                    Parent = UI_PANEL_STATIC_EXCHANGER,
                    DestroyUi = "ICON_EXCHANGER",
                    Components = {
                        new CuiRawImageComponent { Color = "1 1 1 1", Png = _._imageUI.GetImage("ICON_EXCHANGER") },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" }
                    }
                });
                
                container.Add(new CuiButton
                {
                    Button = { Color = "0 0 0 0", Command = config.commandExchanger },
                    Text = { Text = "", Font = "robotocondensed-bold.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "0 0 0 0" },
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" }
                },"ICON_EXCHANGER","EXCHANGER_BUTTON", "EXCHANGER_BUTTON");
                
                AddInterface("UI_Exchanger_Panel_Static", container.ToJson());
            }
            
            
            private void Building_StaticMenuExchanger()
            {
                CuiElementContainer container = new CuiElementContainer();
                
                container.Add(new CuiElement
                {
                    Name = UI_PANEL_STATIC_EXCHANGER_MENU,
                    Parent = "Overlay",
                    DestroyUi = UI_PANEL_STATIC_EXCHANGER_MENU,
                    Components = {
                        new CuiRawImageComponent { Color = "1 1 1 1", Png = _._imageUI.GetImage("PANEL_EXCAHNGER")},
                        new CuiRectTransformComponent { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-204 79.067", OffsetMax = "189.333 128.4" }
                    }
                });
                
                container.Add(new CuiElement
                {
                    Name = "BALANCE_EXCHANGER",
                    Parent = UI_PANEL_STATIC_EXCHANGER_MENU,
                    DestroyUi = "BALANCE_EXCHANGER",
                    Components = {
                        new CuiTextComponent { Text = "%EXCHANGER_ACTUALY_BALANCE%", Font = "robotocondensed-bold.ttf", FontSize = 8, Align = TextAnchor.UpperLeft, Color = colorText },
                        new CuiRectTransformComponent { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "3.687 -1.053", OffsetMax = "230.98 11.453" }
                    }
                });
                
                container.Add(new CuiElement
                {
                    Name = "LIMITE_LABEL_EXCHANGER",
                    Parent = UI_PANEL_STATIC_EXCHANGER_MENU,
                    DestroyUi = "LIMITE_LABEL_EXCHANGER",
                    Components = {
                        new CuiTextComponent { Text = "%EXCHANGER_ACTUALY_LIMITE%", Font = "robotocondensed-bold.ttf", FontSize = 8, Align = TextAnchor.UpperLeft, Color = colorText },
                        new CuiRectTransformComponent { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "3.687 7.674", OffsetMax = "230.98 20.18" }
                    }
                });

                container.Add(new CuiElement
                {
                    Name = "STORE_LABEL",
                    Parent = UI_PANEL_STATIC_EXCHANGER_MENU,
                    DestroyUi = "STORE_LABEL",
                    Components = {
                        new CuiTextComponent { Text = "%EXCHANGER_STORE_INFO%", Font = "robotocondensed-regular.ttf", FontSize = 8, Align = TextAnchor.UpperLeft, Color = colorText },
                        new CuiRectTransformComponent { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "3.687 -9.786", OffsetMax = "359.841 2.72" }
                    }
                });
                
                container.Add(new CuiElement
                {
                    Name = "COURSE_UPDATE_TIME",
                    Parent = UI_PANEL_STATIC_EXCHANGER_MENU,
                    DestroyUi = "COURSE_UPDATE_TIME",
                    Components = { 
                        new CuiTextComponent { Text = "%LAST_TIME_UPDATE_COURSE%", Font = "robotocondensed-bold.ttf", FontSize = 10, Align = TextAnchor.MiddleRight, Color = colorText },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-50.294 -18.708", OffsetMax = "-13.274 -5.958" }
                    }
                });

                container.Add(new CuiElement
                {
                    Name = "COURSE_COIN",
                    Parent = UI_PANEL_STATIC_EXCHANGER_MENU,
                    DestroyUi = "COURSE_COIN",
                    Components = {
                        new CuiTextComponent { Text = "%EXCHANGER_COURSE_COIN%", Font = "robotocondensed-bold.ttf", FontSize = 12, Align = TextAnchor.MiddleRight, Color = colorText },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-52.26 -0.32", OffsetMax = "-5.918 17.253" }
                    }
                });
                
                container.Add(new CuiElement
                {
                    Name = "COURSE_STORE_MONEY",
                    Parent = UI_PANEL_STATIC_EXCHANGER_MENU,
                    DestroyUi = "COURSE_STORE_MONEY",
                    Components = {
                        new CuiTextComponent { Text = "%EXCHANGER_COURSE_STORE_MONEY%", Font = "robotocondensed-bold.ttf", FontSize = 10, Align = TextAnchor.MiddleRight, Color = colorText },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-52.26 -11.12", OffsetMax = "-5.919 6.453" }
                    }
                });
                
                container.Add(new CuiElement
                {
                    Name = "ALL_EXCHANGE_COURSE_LABEL",
                    Parent = UI_PANEL_STATIC_EXCHANGER_MENU,
                    DestroyUi = "ALL_EXCHANGE_COURSE_LABEL",
                    Components = {
                        new CuiTextComponent { Text = "%FULL_COURSE_TITLE%", Font = "robotocondensed-bold.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = colorText  },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "114 -15.791", OffsetMax = "184.329 12.779" }
                    }
                });
                
                container.Add(new CuiElement
                {
                    Name = "ALL_EXCHANGE_COURSE_BUTTON",
                    Parent = "ALL_EXCHANGE_COURSE_LABEL",
                    DestroyUi = "ALL_EXCHANGE_COURSE_BUTTON",
                    Components = {
                        new CuiButtonComponent { Color = "0 0 0 0", Command = "UI_Economic_Command exchanger.run.all" },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                    }
                });
                
                container.Add(new CuiElement
                {
                    Name = "ONE_EXCHANGE_COURSE_LABEL",
                    Parent = UI_PANEL_STATIC_EXCHANGER_MENU,
                    DestroyUi = "ONE_EXCHANGE_COURSE_LABEL",
                    Components = {
                        new CuiTextComponent { Text = "%ONE_COURSE_TITLE%", Font = "robotocondensed-bold.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = colorText },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "40.436 -15.791", OffsetMax = "110.764 12.779" }
                    }
                });
                
                container.Add(new CuiElement
                {
                    Name = "ONE_EXCHANGE_COURSE_BUTTON",
                    Parent = "ONE_EXCHANGE_COURSE_LABEL",
                    DestroyUi = "ONE_EXCHANGE_COURSE_BUTTON",
                    Components = {
                        new CuiButtonComponent { Color = "0 0 0 0", Command = "UI_Economic_Command exchanger.run.one" },
                       new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                    }
                });
                
                container.Add(new CuiButton
                {
                    Button = { Color = "0 0 0 0", Command = "UI_Economic_Command exchanger.multiplier %MULTIPLIER_ONE%" },
                    Text = { Text = "X%MULTIPLIER_ONE%", Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = colorText },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "8.621 -0.32", OffsetMax = "37.512 12.779" }
                },UI_PANEL_STATIC_EXCHANGER_MENU,"UP_MULTIPLIER_XONE", "UP_MULTIPLIER_XONE");

                container.Add(new CuiButton
                {
                    Button = { Color = "0 0 0 0", Command = "UI_Economic_Command exchanger.multiplier %MULTIPLIER_TWO%" },
                    Text = { Text = "X%MULTIPLIER_TWO%", Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = colorText },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "8.621 -15.791", OffsetMax = "37.512 -2.693" }
                },UI_PANEL_STATIC_EXCHANGER_MENU,"UP_MULTIPLIER_XTWO", "UP_MULTIPLIER_XTWO");
                
                container.Add(new CuiButton
                {
                    Button = { Color = "0 0 0 0", Command = "UI_Economic_Command exchanger.close" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "182.053 10.107", OffsetMax = "194.053 22.107" }
                },UI_PANEL_STATIC_EXCHANGER_MENU,"CLOSE_BUTTON_EXCANGER", "CLOSE_BUTTON_EXCANGER");

                
                AddInterface("UI_Exchanger_Menu_Panel_Static", container.ToJson());
            }
            
            private void Building_UpdateBalance()
            {
                CuiElementContainer container = new CuiElementContainer();
                
                container.Add(new CuiElement
                {
                    Name = "BALANCE_EXCHANGER",
                    Parent = UI_PANEL_STATIC_EXCHANGER_MENU,
                    Update = true,
                    Components = { new CuiTextComponent { Text = "%EXCHANGER_ACTUALY_BALANCE%" } }
                });
                
                AddInterface("UI_Exchanger_Update_Balance_Label", container.ToJson());
            }  
            
            private void Building_UpdateLimite()
            {
                CuiElementContainer container = new CuiElementContainer();
                
                container.Add(new CuiElement
                {
                    Name = "LIMITE_LABEL_EXCHANGER",
                    Parent = UI_PANEL_STATIC_EXCHANGER_MENU,
                    Update = true,
                    Components = {
                        new CuiTextComponent { Text = "%EXCHANGER_ACTUALY_LIMITE%" },
                    }
                });
                
                AddInterface("UI_Exchanger_Update_Limite_Label", container.ToJson());
            }
            
            private void Building_UpdateCourseTime()
            {
                CuiElementContainer container = new CuiElementContainer();
                
                container.Add(new CuiElement
                {
                    Name = "COURSE_UPDATE_TIME",
                    Parent = UI_PANEL_STATIC_EXCHANGER_MENU,
                    Update = true,
                    Components = {
                        new CuiTextComponent { Text = "4h"  },
                    }
                });
                
                AddInterface("UI_Exchanger_Update_CourseTime_Label", container.ToJson());
            }
            
            private void Building_UpdateCourseCoin()
            {
                CuiElementContainer container = new CuiElementContainer();
                
                container.Add(new CuiElement
                {
                    Name = "COURSE_COIN",
                    Parent = UI_PANEL_STATIC_EXCHANGER_MENU,
                    Update = true,
                    Components = {
                        new CuiTextComponent { Text = "100 COIN" },
                    }
                });
                
                AddInterface("UI_Exchanger_Update_CourseCoin_Label", container.ToJson());
            } 
            
            private void Building_UpdateCourseStoreMoney()
            {
                CuiElementContainer container = new CuiElementContainer();
                
                container.Add(new CuiElement
                {
                    Name = "COURSE_STORE_MONEY",
                    Parent = UI_PANEL_STATIC_EXCHANGER_MENU,
                    Update = true,
                    Components = {
                        new CuiTextComponent { Text = "~10 RUB" }
                    }
                });
                
                AddInterface("UI_Exchanger_Update_CourseStoreMoney_Label", container.ToJson());
            }
            
            private void Building_UpdateButtonExchangeAll()
            {
                CuiElementContainer container = new CuiElementContainer();
                
                container.Add(new CuiElement
                {
                    Name = "ALL_EXCHANGE_COURSE_LABEL",
                    Parent = UI_PANEL_STATIC_EXCHANGER_MENU,
                    Update = true,
                    Components = {
                        new CuiTextComponent { Text = "%FULL_COURSE_TITLE%" },
                    }
                });
                
                AddInterface("UI_Exchanger_Update_FullExchange_Button", container.ToJson());
            }   
            
            private void Building_UpdateButtonExchangeOne()
            {
                CuiElementContainer container = new CuiElementContainer();
                
                container.Add(new CuiElement
                {
                    Name = "ONE_EXCHANGE_COURSE_LABEL",
                    Parent = UI_PANEL_STATIC_EXCHANGER_MENU,
                    Update = true,
                    Components = {
                        new CuiTextComponent { Text = "%ONE_COURSE_TITLE%" }
                    }
                });
                
                AddInterface("UI_Exchanger_Update_OneExchange_Button", container.ToJson());
            }
            
                        
                        
            private void Building_StaticHider(String nameElement, String iconElement)
            {
                CuiElementContainer container = new CuiElementContainer();
                
                container.Add(new CuiElement
                {
                    Name = UI_PANEL_STATIC_HIDER,
                    Parent = "Overlay",
                    DestroyUi = UI_PANEL_STATIC_HIDER,
                    Components = {
                        new CuiImageComponent { Color = colorPanel, Material = FontMaterial },
                        new CuiRectTransformComponent { AnchorMin = anchorMinHider, AnchorMax = anchorMaxHider, OffsetMin = offsetMinHider, OffsetMax = offsetMaxHider }
                    }
                });
                
                container.Add(new CuiElement
                {
                    Name = "ICON_HIDE",
                    Parent = UI_PANEL_STATIC_HIDER,
                    DestroyUi = "ICON_HIDE",
                    Components = {
                        new CuiRawImageComponent { Color = "1 1 1 1", Png = _._imageUI.GetImage(iconElement) },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" }
                    }
                });
                
                container.Add(new CuiElement
                {
                    Name = "HIDE_BUTTON",
                    Parent = "ICON_HIDE",
                    DestroyUi = "HIDE_BUTTON",
                    Components = {
                        new CuiButtonComponent { Color = "0 0 0 0", Command = "UI_Economic_Command hide.controller" },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" }
                    }
                });
                
                AddInterface(nameElement, container.ToJson());
            }
            
            private void Building_UpdateHider(String nameElement, String iconElement)
            {
                CuiElementContainer container = new CuiElementContainer();
                
                container.Add(new CuiElement
                {
                    Name = "ICON_HIDE",
                    Parent = UI_PANEL_STATIC_HIDER,
                    Update = true,
                    Components =
                    {
                        new CuiRawImageComponent { Png = _._imageUI.GetImage(iconElement) }
                    }
                });
                
                AddInterface(nameElement, container.ToJson());
            }
            
                    }
        
        private Int32 GetPlayerPhysicMoney(BasePlayer player)
        {
            Configuration.GeneralSetting.PhysicsItem physicConfig = config.generalSetting.physicsItem;
            Int32 balance = 0;

            List<Item> allItems = GetAllItems(player);

            foreach (Item itemInInventory in allItems)
            {
                if (physicConfig.IsPhysicMoney(itemInInventory))
                    balance += itemInInventory.amount;
            }
            
            Pool.FreeUnmanaged(ref allItems);
            
            return balance;
        }
        
                
        
        
        

        public CourseInfo courseData = new CourseInfo();

        
                
        private void OnCollectiblePickedup(CollectibleEntity collectible, BasePlayer player, Item component)
        {
            if (!collectible || !player || collectible.itemList == null) return;
            Dictionary<String, Configuration.EarningCoins.PresetEarning> resourceCollectable = config.earningCoins.resourceCollectable;

            String shortname = component.info.shortname;
            if(!resourceCollectable.TryGetValue(shortname, out Configuration.EarningCoins.PresetEarning resourcePreset)) return;
            if(!resourcePreset.useEarning) return;
            if(!resourcePreset.IsEarning(player)) return;

            AddBalance(player, resourcePreset.countMoney, TypeLog.Action, resourcePreset.useChatAlert ? "CHAT_ALERT_EARNING_PICK_UP_RESOURCE" : default);
        }

        
        
                
        
        private abstract class DataManager<T> where T : DataManager<T>, new()
        {
            private const String baseFolder =  "IQSystem/IQEconomic/";
            public static Dictionary<String, T> _players = new();
            
            protected static void ImportPlayer(String userId, T data) => _players[userId] = data;
            protected static void SavePlayer(String userId)
            {
                if (!_players.TryGetValue(userId, out T data)) return;

                Interface.Oxide.DataFileSystem.WriteObject(baseFolder + userId, data);
            }
            protected static void SaveOfflinePlayer(String userId, T data) => Interface.Oxide.DataFileSystem.WriteObject(baseFolder + userId, data);

            protected static void RemovePlayer(String userId)
            {
                if (!_players.ContainsKey(userId)) return;
                _players.Remove(userId);
            }

            protected static void DeletePlayer(String userId)
            {
                RemovePlayer(userId);
                Interface.Oxide.DataFileSystem.DeleteDataFile(baseFolder + userId);
            }
            protected static T GetPlayer(String userId) => _players.GetValueOrDefault(userId);

            protected static T GetOfflinePlayer(String userId)
            {
                T data = null;
		   		 		  						   					  						  						   		 		  		 	
                try
                {
                    data = Interface.Oxide.DataFileSystem.ReadObject<T>(baseFolder + userId);
                }
                catch (Exception e)
                {
                    Interface.Oxide.LogError(e.ToString());
                }

                return data;
            }

            protected static T LoadPlayer(String userId)
            {
                T data = null;

                try
                {
                    data = Interface.Oxide.DataFileSystem.ReadObject<T>(baseFolder + userId);
                }
                catch (Exception e)
                {
                    Interface.Oxide.LogError(e.ToString());
                }

                return _players[userId] = data;
            }
            
            protected static String[] GetFilesPlayers()
            {
                try
                {
                    Int32 json = ".json".Length;
                    String[] paths = Interface.Oxide.DataFileSystem.GetFiles(baseFolder, "*.json");
                    for (Int32 i = 0; i < paths.Length; i++)
                    {
                        String path = paths[i];
                        Int32 separatorIndex = path.LastIndexOf("/", StringComparison.Ordinal);
                        paths[i] = path.Substring(separatorIndex + 1, path.Length - separatorIndex - 1 - json);
                    }

                    return paths;
                }
                catch
                {
                    return Array.Empty<String>();
                }
            }
        }

                
        private void CleanerDataFiles() 
        {
            List<String> filesPlayers = Pool.Get<List<String>>();
            filesPlayers.AddRange(PlayerInfo.GetFiles());
            
            if (config.generalSetting.clearFullDataFile)
            {
                foreach (String filesPlayer in filesPlayers)
                    PlayerInfo.Delete(filesPlayer);
                
                Pool.FreeUnmanaged(ref filesPlayers);
                return;
            }
            
            foreach (String filesPlayer in filesPlayers)
            {
                PlayerInfo pInfo = PlayerInfo.GetOffline(filesPlayer);
                if(pInfo == null) continue;

                pInfo.LimitBalance = 0;
                
                Int32 dayLeftConnection = (Int32)Math.Round((CurrentTime - pInfo.DateTime) / 86400.0f);
                
                if (pInfo.Balance <= 50 && dayLeftConnection >= 14)
                    PlayerInfo.Delete(filesPlayer);
                
                if (dayLeftConnection >= 30)
                    PlayerInfo.Delete(filesPlayer);
            }
            
            Pool.FreeUnmanaged(ref filesPlayers);
        }
		   		 		  						   					  						  						   		 		  		 	
        
        
                
        private void LogAction(Object playerOrUserID, Int32 amount, TypeLog log, Boolean addOrRemove)
        {
            if (!config.generalSetting.useLogger) return;

            String logArrow = arrowLogRemoved;
            String logAction = actionLogRemoved;
            if (addOrRemove)
            {
                logArrow = arrowLogReceived;
                logAction = actionLogReceived;
            }

            String playerInfo = playerOrUserID switch
            {
                BasePlayer player => $"{player.displayName} ({player.UserIDString})",
                String userID => $"OfflinePlayer ({userID})",
                _ => "Unknown"
            };

            String logInfo = $"{logArrow} [{log}] {playerInfo} {logAction} {amount}";

            LogToFile("IQEconomic", logInfo, _, true, true);
        }
        
        
        [ConsoleCommand("UI_Economic_Command")] 
        private void UI_Economic_Command(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (!player) return;
            if (!arg.HasArgs()) return;

            String type = arg.GetString(0);
            
            Int32 balancePlayer = GetBalancePlayer(player);

            switch (type)
            {
                case "hide.controller":
                {
                    PlayerInfo pInfo = PlayerInfo.Get(player.UserIDString);
                    if (pInfo == null) return;

                    pInfo.IsHide = !pInfo.IsHide;
                    DrawUI_UpdateHider(player, pInfo.IsHide);
                    break;
                }
                
                case "exchanger.multiplier":
                {
                    if (!isUsedExchanger) return;
                    String multiplierGet = arg.GetString(1);
                    if (!Int32.TryParse(multiplierGet, out Int32 multiplier)) return;
                    
                    PlayerInfo pInfo = PlayerInfo.Get(player.UserIDString);
                    if (pInfo == null) return;

                    Int32 limitExchanged = GetLimiteExchanged(pInfo);
                    
                    UpdateMultiplier(player, balancePlayer, limitExchanged, multiplier);
                    break;
                } 
                
                case "exchanger.run.all":
                {
                    if (!isUsedExchanger) return;
                    (Int32 exchangeCoins, Int32 storeMoney) exchangeDetail = actualyCourse.GetFullExchangeDetails(balancePlayer);
                    ExchangedProcess(player, exchangeDetail);
                    break;
                }
                
                case "exchanger.run.one":
                {
                    if (!isUsedExchanger) return;
                    Int32 multiplier = exchangerDataPlayer.GetValueOrDefault(player, 1);
                    (Int32 exchangeCoins, Int32 storeMoney) exchangeDetail = actualyCourse.GetExchangeDetails(balancePlayer, multiplier);
                    ExchangedProcess(player, exchangeDetail);
                    break;
                }

                case "exchanger.close":
                {
                    if (!isUsedExchanger) return;
                    if (exchangerDataPlayer.ContainsKey(player))
                        exchangerDataPlayer.Remove(player);

                    DestroyUI(player, InterfaceBuilder.UI_PANEL_STATIC_EXCHANGER_MENU);
                    break;
                }
            }
        }
        private Coroutine coroutineMigrate = null;

        
        private void RegisteredPermission()
        {
            permission.RegisterPermission(transferPrivilage, this);

            if (!String.IsNullOrWhiteSpace(config.earningCoins.killedAnimal.permissionEarning))
                permission.RegisterPermission(config.earningCoins.killedAnimal.permissionEarning, this);

            if (!String.IsNullOrWhiteSpace(config.earningCoins.killedPlayer.permissionEarning))
                permission.RegisterPermission(config.earningCoins.killedPlayer.permissionEarning, this);

            if (!String.IsNullOrWhiteSpace(config.earningCoins.killedBradley.permissionEarning))
                permission.RegisterPermission(config.earningCoins.killedBradley.permissionEarning, this);
		   		 		  						   					  						  						   		 		  		 	
            if (!String.IsNullOrWhiteSpace(config.earningCoins.killedHelicopter.permissionEarning))
                permission.RegisterPermission(config.earningCoins.killedHelicopter.permissionEarning, this);
		   		 		  						   					  						  						   		 		  		 	
            if (!String.IsNullOrWhiteSpace(config.earningCoins.killedNpcs.permissionEarning))
                permission.RegisterPermission(config.earningCoins.killedNpcs.permissionEarning, this);

            if (!String.IsNullOrWhiteSpace(config.earningCoins.timeTracked.timeTrackerPreset.permissionEarning))
                permission.RegisterPermission(config.earningCoins.timeTracked.timeTrackerPreset.permissionEarning, this);
		   		 		  						   					  						  						   		 		  		 	
            foreach (KeyValuePair<String, Configuration.EarningCoins.PresetEarning> presetEarning in config.earningCoins.resourceGatherEarning)
            {
                if (!String.IsNullOrWhiteSpace(presetEarning.Value.permissionEarning))
                    permission.RegisterPermission(presetEarning.Value.permissionEarning, this);
            }
		   		 		  						   					  						  						   		 		  		 	
            foreach (KeyValuePair<String, Configuration.EarningCoins.PresetEarning> presetEarning in config.earningCoins.resourceCollectable)
            {
                if (!String.IsNullOrWhiteSpace(presetEarning.Value.permissionEarning))
                    permission.RegisterPermission(presetEarning.Value.permissionEarning, this);
            }
        }

        
        
        private void OnEntityDeath(BaseAnimalNPC entity, HitInfo info)
        {
            Configuration.EarningCoins.PresetEarning earning = config.earningCoins.killedAnimal;
            if (!earning.useEarning) return;
            
            if (!entity || info == null) return;
            
            BasePlayer player = info.InitiatorPlayer;
            if(!IsTakeEarning(player, earning)) return;
            AddBalance(player, earning.countMoney, TypeLog.Action, earning.useChatAlert ? "CHAT_ALERT_EARNING_KILLED_ANIMAL" : default);
        }

        private void OnNewSave(String filename) => CleanerDataFiles();   
        private Timer timerWaitSQL = null;
        
        
		    }
}
