using Oxide.Core;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Newtonsoft.Json.Converters;
using System.Linq;
using System.Globalization;
using UnityEngine;
using Facepunch;

namespace Oxide.Plugins
{
    [Info("ServerCoins", "xkrystalll", "1.2.0")]
    public class ServerCoins : RustPlugin
    {





        // This is config language setting (true - russian, false - english)
        private const bool ConfigLang = false;






        [PluginReference]
        private Plugin ServerRewards;
        private const string PERM_CGIVE = "ServerCoins.cgive";

        #region Cfg

        public enum ShopType : byte
        {
            GameStores = 0,
            OVH = 1,
            ServerRewards = 2
        }

        public class Shop
        {
            [JsonConverter(typeof(StringEnumConverter))]
            [JsonProperty(ConfigLang ? "Какой магазин использовать? (GameStores, OVH, ServerRewards)" : "Which store should use? (GameStores, OVH, ServerRewards)", Order = 0)]
            public ShopType Type;
            [JsonProperty(ConfigLang ? "Настройки GameStores" : "GameStores settings", Order = 1)]
            public GameStoresSettings GameStoresSettings;
        }
        public class GameStoresSettings
        {
            [JsonProperty(ConfigLang ? "Секретный ключ магазина" : "Store's secret key", Order = 0)]
            public string SecretKey;
            [JsonProperty(ConfigLang ? "ID магазина в сервисе" : "Store ID in the service", Order = 1)]
            public string ShopID;
            [JsonProperty(ConfigLang ? "Сообщение при выводе (отображается в истории пополнений игрока)" : "Message on withdraw (displays in players deposit story)", Order = 2)]
            public string Message;
        }
        public class RewardItem 
        {
            [JsonProperty(ConfigLang ? "Shortname предмета" : "Item shortname", Order = 0)]
            public string Shortname;
            [JsonProperty(ConfigLang ? "Type of action to activate the item (unwrap, consume)" : "", Order = 1)]
            public string Action;
            [JsonProperty(ConfigLang ? "Имя предмета" : "Item name", Order = 2)]
            public string Name;
            [JsonProperty(ConfigLang ? "SkinID предмета" : "Item SkinID", Order = 3)]
            public ulong SkinID;
            // [JsonProperty(ConfigLang ? "Размер стака" : "Stack size", Order = 4)]
            // public int StackSize;
            // [JsonProperty(ConfigLang ? "Сколько даёт монеток? (min, max)" : "How many coins does it give? (min, max)", Order = 5)]
            // public KeyValuePair<int, int> Coins;
            [JsonProperty(ConfigLang ? "Минимальное кол-во монеток для вывода" : "Minimum number of coins for withdrawal", Order = 4)]
            public int MinAmountToWithdraw;
            // [JsonProperty(ConfigLang ? "Сколько будет спавнится в ящиках? (min, max)" : "How many will spawn in the boxes? (min, max)", Order = 7)]
            // public KeyValuePair<int, int> Amount;
            [JsonProperty(ConfigLang ? "Ящики, в которых будет спавниться и шанс спавна" : "Boxes in which to spawn and the chance to spawn", Order = 5)] // %id%
            public Dictionary<string, AmountAndChance> Containers;
        }
        public class AmountAndChance 
        {
            [JsonProperty(ConfigLang ? "Сколько будет спавнится в ящиках? (min, max)" : "How many will spawn in the boxes? (min, max)", Order = 0)]
            public KeyValuePair<int, int> Amount;
            [JsonProperty(ConfigLang ? "Шанс спавна" : "Spawn chance", Order = 1)]
            public float SpawnChance;
        }
        public class DataConfig
        {
            [JsonProperty(ConfigLang ? "Соотношение к выдаче баланса на магазины (например 10 монеток = 5 руб на баланс)" : "The ratio to the issuance of the balance to stores (for example, 10 coins = 5 rubles per balance)", Order = 0)]
            public float Ratio;
            [JsonProperty(ConfigLang ? "Логировать выдачу баланса игрокам?" : "Log the balance changes by players?", Order = 1)]
            public bool LogRewardActions;
            [JsonProperty(ConfigLang ? "Магазин" : "Shop type", Order = 2)]
            public Shop Shop;
            [JsonProperty(ConfigLang ? "Настройки предметов (SkinID у каждого должен быть уникален)" : "Item settings (each SkinID must be unique)", Order = 3)]
            public List<RewardItem> RewardItems;
            public RewardItem GetRandomItem() => RewardItems.GetRandom();
        }

        void Init()
        {
            LoadConfig();
        }

        public DataConfig cfg;
        protected override void LoadConfig()
        {
            base.LoadConfig();
            cfg = Config.ReadObject<DataConfig>();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(cfg);
        }

        protected override void LoadDefaultConfig()
        {
            cfg = new DataConfig()
            {
                Shop = new Shop()
                {
                    Type = ShopType.GameStores,
                    GameStoresSettings = new GameStoresSettings()
                    {
                        SecretKey = "secretkey",
                        ShopID = "shopid",
                        Message = ConfigLang ? "Обмен монеток на баланс" : "Change coins for rubles"
                    }
                },
                Ratio = 1.0f,
                LogRewardActions = true,
                RewardItems = new List<RewardItem>()
                {
                    new RewardItem()
                    {
                        Shortname = "xmas.present.small",
                        Action = "unwrap",
                        Name = "MysteryCoin",
                        // StackSize = 40,
                        SkinID = 2401812064,
                        MinAmountToWithdraw = 2,
                        Containers = new Dictionary<string, AmountAndChance>()
                        {
                            ["crate_basic"] = new AmountAndChance() 
                            {
                                Amount = new KeyValuePair<int, int>(2, 5),
                                SpawnChance = 20.0f
                            },
                            ["crate_normal"] = new AmountAndChance() 
                            {
                                Amount = new KeyValuePair<int, int>(20, 50),
                                SpawnChance = 40.0f
                            },
                            ["crate_normal_2"] = new AmountAndChance() 
                            {
                                Amount = new KeyValuePair<int, int>(1000, 1000),
                                SpawnChance = 60.0f
                            }
                        }
                        // Amount = new KeyValuePair<int, int>(1, 5),
                        // Containers = new Dictionary<string, float> 
                        // {
                        //     ["crate_basic"] = 10.0f,
                        //     ["crate_normal"] = 40.0f,
                        //     ["crate_normal_2"] = 100.0f
                        // }
                    },
                    new RewardItem()
                    {
                        Shortname = "xmas.present.large",
                        Action = "unwrap",
                        Name = "RareCoin",
                        MinAmountToWithdraw = 5,
                        // StackSize = 5,
                        SkinID = 2917345108,
                        Containers = new Dictionary<string, AmountAndChance>()
                        {
                            ["crate_basic"] = new AmountAndChance() 
                            {
                                Amount = new KeyValuePair<int, int>(2, 5),
                                SpawnChance = 10.0f
                            },
                            ["crate_normal"] = new AmountAndChance() 
                            {
                                Amount = new KeyValuePair<int, int>(20, 50),
                                SpawnChance = 20.0f
                            },
                            ["crate_normal_2"] = new AmountAndChance() 
                            {
                                Amount = new KeyValuePair<int, int>(500, 600),
                                SpawnChance = 40.0f
                            }
                        }
                    }
                }
            };
        }

        #endregion

        #region OxideHooks
        // ItemContainer.CanAcceptResult? CanAcceptItem(ItemContainer container, Item item, int targetPos)
        // {
        //     var item1 = cfg.RewardItems.FirstOrDefault(x =>
        //         x.Shortname == item.info.shortname && item.skin == x.SkinID);
        //     if (item1 == null)
        //         return null;
        //
        //     if (container.GetOwnerPlayer() == null)
        //         return ItemContainer.CanAcceptResult.CannotAccept;
        //     
        //     return null;
        // }
        private void Loaded()
        {
            Unsubscribe(nameof(OnItemSplit));
            Unsubscribe(nameof(OnLootSpawn));
        }
        void OnServerInitialized()
        {
            Subscribe(nameof(OnLootSpawn));

            if (cfg.Shop.Type == ShopType.GameStores && !plugins.Exists("GameStoresRUST"))
            {
                PrintError("The store plugin is not loaded, further operation of the plugin is impossible!");
                return;
            }
            if (cfg.Shop.Type == ShopType.OVH && !plugins.Exists("RustStore"))
            {
                PrintError("The store plugin is not loaded, further operation of the plugin is impossible!");
                return;
            }
            if (cfg.Shop.Type == ShopType.ServerRewards && !plugins.Exists("ServerRewards"))
            {
                PrintError("The 'ServerRewards' plugin is not loaded, further operation of the plugin is impossible!");
                return;
            }
            permission.RegisterPermission(PERM_CGIVE, this);

            if (!plugins.Find("StackModifier") && !plugins.Find("Loottable"))
                Subscribe(nameof(OnItemSplit));
        }

        void OnLootSpawn(LootContainer container)
        {
            if (container == null || container.inventory == null) return;
            NextTick(() =>
            {
                // Puts(container?.ShortPrefabName);
                // if (container.GetComponent<HackableLockedCrate>())
                //     {
                //         container.GetComponent<HackableLockedCrate>().hackSeconds += 1000;
                //     }
                if (container?.ShortPrefabName == "stocking_large_deployed" ||
                container?.ShortPrefabName == "stocking_small_deployed") 
                    return;

                RewardItem rewardItem = null;
                if (cfg.RewardItems.Count <= 1)
                    rewardItem = cfg.RewardItems[0];
                else if (cfg.RewardItems.Count > 1)
                    rewardItem = cfg.RewardItems.GetRandom();
                else
                {
                    return;
                }

                float randomValue = UnityEngine.Random.Range(0f, 100f);
                
                if (rewardItem.Containers.ContainsKey(container.ShortPrefabName) && randomValue < rewardItem.Containers[container.ShortPrefabName].SpawnChance)
                {
                    // if (container.inventory.itemList.Count == container.inventory.capacity)

                    var chanceAndAmount = rewardItem.Containers[container.ShortPrefabName];
                    int amount = new System.Random().Next(chanceAndAmount.Amount.Key, chanceAndAmount.Amount.Value);

                    Item i = ItemManager.CreateByName(rewardItem.Shortname, amount, rewardItem.SkinID);
                    if (i == null)
                    {
                        PrintError("Error on create a coin item (wrong shortname?)");
                        return;
                    }
                    container.inventory.capacity++;

                    if (!string.IsNullOrEmpty(rewardItem.Name))
                        i.name = rewardItem.Name;

                    i.MoveToContainer(container.inventory);
                }
            });
        }
        object OnItemAction(Item item, string action, BasePlayer player)
        {
            if (player == null || !player.userID.IsSteamId() || string.IsNullOrEmpty(action))
                return null;

            
            if (action == "upgrade_item" && cfg.RewardItems.Any(x => x.SkinID == item.skin))
                return false;
                
            if (!cfg.RewardItems.Any(x => x.SkinID == item.skin && x.Action == action))
                return null;
            
            // if (item.GetOwnerPlayer() == null || item?.GetOwnerPlayer() != player)
            // {
            //     player.ChatMessage(GetMsg("error.moveToInventory", player.userID));
            //     return false;
            // }
            
            RewardItem rewardItem = cfg.RewardItems.FirstOrDefault(x => x.SkinID == item.skin && x.Action == action);


            AddingFunds(player, rewardItem, item);
            return false;
        }

        private Item OnItemSplit(Item item, int amount)
        {
            if (item != null && cfg.RewardItems.Any(x => x.SkinID == item.skin))
            {
                Item byItemId = ItemManager.CreateByItemID(item.info.itemid, 1, item.skin);
                if (!string.IsNullOrEmpty(item.name))
                    byItemId.name = item.name;
                
                item.amount -= amount;
                byItemId.amount = amount;
                item.MarkDirty();
                return byItemId;
            }

            return null;
        }

        private bool? CanStackItem(Item item, Item targetItem)
        {
            if (!cfg.RewardItems.Any(x => x.Shortname == item.info.shortname && x.SkinID == item.skin)) { return null; }
            if (item.skin != targetItem.skin)
                return false;

            return null;
        }

        private bool? CanCombineDroppedItem(DroppedItem item, DroppedItem targetItem)
        {
            if (item.GetItem() == null || targetItem.GetItem() == null)
                return null;

            if (item.GetItem().skin != targetItem.GetItem().skin)
                return false;

            return null;
        }

        #endregion
        private List<BasePlayer> FindPlayerDefinition(string IdOrIpOrName)
        {
            List<BasePlayer> playersFinded = new List<BasePlayer>();
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player.UserIDString.Equals(IdOrIpOrName))
                    playersFinded.Add(player);
                else if (!string.IsNullOrEmpty(player.displayName) && player.displayName.Contains(IdOrIpOrName, CompareOptions.IgnoreCase))
                    playersFinded.Add(player);
                else if (player.net?.connection != null && player.net.connection.ipaddress.Equals(IdOrIpOrName))
                    playersFinded.Add(player);
            }
            return playersFinded;
        }
        [ChatCommand("coins")]
        private void cmdCoins(BasePlayer p)
        {
            if (p == null)
                return;
            if (!p.IsAdmin && !permission.UserHasPermission(p.UserIDString, PERM_CGIVE))
                return;
            
            foreach (var x in cfg.RewardItems)
            {
                p.ChatMessage($"{x.Name} - {x.SkinID}");
            }
        }
        [ChatCommand("cgive")]
        private void GiveHandler(BasePlayer player, string command, string[] args)
        {
            if (player == null) 
                return;
            if (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, PERM_CGIVE))
                return;
            if (args.IsNullOrEmpty())
            {
                player.ChatMessage(GetMsg("give.argsIsNull", player.userID));
                return;
            }

            ulong skinid = 0ul;
            if (!ulong.TryParse(args[0], out skinid))
            {
                player.ChatMessage(GetMsg("give.argsIsNull", player.userID));
                return;
            }

            int amount = 0;
            if (!int.TryParse(args[1], out amount) || amount <= 0)
            {
                player.ChatMessage(GetMsg("give.argsIsNull", player.userID));
                return;
            }

            if (!cfg.RewardItems.Any(x => x.SkinID == skinid))
            {
                player.ChatMessage(GetMsg("give.notFound", player.userID));
                return;
            }

            var playersFounded = FindPlayerDefinition(args[2]);
            if (playersFounded.Count > 1)
            {
                player.ChatMessage($"Finded some players: {string.Join(", ", playersFounded.ConvertAll(x => x.displayName))}");
                return;
            }
            else if (playersFounded.Count <= 0)
            {
                player.ChatMessage($"Player '{args[2]}' not found.");
                return;
            }
            BasePlayer targetRaw = playersFounded[0];
            BasePlayer target = null;   
            if (args.Length > 1)
            {
                if (targetRaw != null && targetRaw.userID.IsSteamId() && targetRaw.IsConnected)
                    target = targetRaw;
                else
                {
                    player.ChatMessage($"Player '{target}' not found.");
                    return;
                }
            }
            RewardItem rewardItem = cfg.RewardItems.FirstOrDefault(x => x.SkinID == skinid);
            Item i = ItemManager.CreateByName(rewardItem.Shortname, amount, rewardItem.SkinID);
            i.name = rewardItem.Name;
            target.GiveItem(i);
            player.ChatMessage(string.Format(GetMsg("give.success", player.userID), amount, target == null ? player.displayName : target.displayName));
        }
        private int GetCoinsAmount(BasePlayer player, RewardItem rewardItem)
        {
            var sum = 0;
            foreach (var coins in GetAllPlayerItems(player))
            {
                if (coins.skin == rewardItem.SkinID) 
                    sum += coins.amount;
            }
            return sum;
        }
        public void TakeCoins(BasePlayer player, int amount, RewardItem rewardItem)
        {
            foreach (var item in GetAllPlayerItems(player))
            {
                if (item.skin != rewardItem.SkinID) 
                    continue;
                if (item.amount > amount)
                {
                    item.UseItem(amount);
                    break;
                }
                amount -= item.amount;
                item.UseItem(item.amount);
            }
        }
        private void AddingFunds(BasePlayer player, RewardItem rewardItem, Item item)
        {
            int amount = item.amount;
            if (rewardItem.MinAmountToWithdraw > amount)
            {
                player.ChatMessage(string.Format(GetMsg("withdraw.notenoughcoins", player.userID), rewardItem.MinAmountToWithdraw));
                return;
            }
            switch (cfg.Shop.Type)
            {
                case ShopType.GameStores:
                    MoneyPlus(player.userID, (int)(amount / cfg.Ratio), (code) => 
                    {
                        if (code == CallbackCode.Ok)
                        {
                            if (cfg.LogRewardActions)
                            {
                                Interface.Oxide.LogDebug($"Player [{player.displayName}/{player.UserIDString}] get {amount / cfg.Ratio} rub on balance in shop"); // %id%
                            }

                            TakeCoins(player, amount, rewardItem);
                            player.ChatMessage(string.Format(string.Format(GetMsg("withdraw.success", player.userID), amount / cfg.Ratio, amount), amount / cfg.Ratio, amount));
                        }
                        else
                        {
                            Interface.Oxide.LogDebug(string.Format(GetMsg("weblogger.error", player.userID), player.userID));
                            player.ChatMessage(GetMsg("error.wrongWebRequest", player.userID));
                            return;
                        }
                    });
                    break;
                case ShopType.OVH:
                    var plugin = plugins.Find("RustStore");
                    plugin?.Call("APIChangeUserBalance", player.userID.Get(), amount / cfg.Ratio, new Action<string>((result) =>
                    {
                        if (result == "SUCCESS")
                        {
                            // 9945
                            if (cfg.LogRewardActions)
                            {
                                Interface.Oxide.LogDebug(string.Format(GetMsg("weblogger.success", player.userID), player.displayName, player.UserIDString, amount / cfg.Ratio));
                                LogToFile("withdraws",
                                    $"({DateTime.Now.ToShortTimeString()}): "
                                + "Adding funds to account:"
                                + $"{player.userID}: Added {amount / cfg.Ratio} balance.",
                                    this);
                            }
                            TakeCoins(player, amount, rewardItem);
                            player.ChatMessage(string.Format(GetMsg("withdraw.success", player.userID), amount / cfg.Ratio, amount));
                        }
                        if (cfg.LogRewardActions)
                        {
                            Interface.Oxide.LogDebug($"Balance not changed: {result}");
                        }
                    }));
                    break;
                case ShopType.ServerRewards:
                    AddPoints(player.userID, (int)(amount / cfg.Ratio));
                    item.Remove();
                    item.RemoveFromContainer();
                    item.RemoveFromWorld();
                    // TakeCoins(player, amount, rewardItem);
                    LogToFile("withdraws",
                        $"({DateTime.Now.ToShortTimeString()}): "
                    + "Adding funds to account:"
                    + $"{player.userID}: Added {(int)(amount / cfg.Ratio)} balance.",
                        this);
                    player.ChatMessage(string.Format(GetMsg("withdraw.success", player.userID), amount / cfg.Ratio, amount));
                    break;
                default:
                    PrintError("Unknown shop type. Check config");
                    return;
            }
        }

        #region Helpers

        private List<Item> GetAllPlayerItems(BasePlayer player)
        {
            List<Item> items = Pool.Get<List<Item>>();

            if (player.inventory.containerMain != null)
            {
                items.AddRange(player.inventory.containerMain.itemList);
            }

            if (player.inventory.containerBelt != null)
            {
                items.AddRange(player.inventory.containerBelt.itemList);
            }

            if (player.inventory.containerWear != null)
            {
                items.AddRange(player.inventory.containerWear.itemList);
            }

            return items;
        }

        void AddPoints(ulong playerID, int amount) => ServerRewards?.Call("AddPoints", playerID, amount);
        void MoneyPlus(ulong userId, int amount, Action<CallbackCode> callback) 
        {
            ApiRequestBalance(userId.ToString(), amount.ToString(), callback);
        } 
        void ApiRequestBalance(string playerId, string amount, Action<CallbackCode> callback)
        {
            string request = $"http://gamestores.ru/api?shop_id={cfg.Shop.GameStoresSettings.ShopID}&secret={cfg.Shop.GameStoresSettings.SecretKey}&action=moneys&type=plus&steam_id={playerId}&amount={amount}&mess={cfg.Shop.GameStoresSettings.Message}";
                webrequest.Enqueue(request, null, (code, response) =>
                {
                    // PrintWarning($"Code - {code}, resp - {response}");
                    var responseParsed = JsonConvert.DeserializeObject<Dictionary<string, string>>(response);
                    if (responseParsed["result"] != "success")
                    {
                        PrintError($"{request}\nCODE {code}: {response}");

                        if (cfg.LogRewardActions)
                        {
                            LogToFile("logError", $"({DateTime.Now.ToShortTimeString()}): {request}\nCODE {code}: {request}", this);
                        }
                        callback.Invoke(CallbackCode.Error);
                        return;
                    }
                    if (code != 200 && code != 201)
                    {
                        PrintError($"{request}\nCODE {code}: {response}");
                        callback.Invoke(CallbackCode.Error);
                        if (cfg.LogRewardActions)
                        {
                            LogToFile("logError", $"({DateTime.Now.ToShortTimeString()}): {request}\nCODE {code}: {request}", this);
                        }
                    }
                    else
                    {
                        if (cfg.LogRewardActions)
                        {
                            LogToFile("withdraws",
                                $"({DateTime.Now.ToShortTimeString()}): "
                            + "Adding funds to account:"
                            + $"{playerId}: Added {amount} balance.",
                                this);
                        }
                        callback?.Invoke(CallbackCode.Ok);
                        // success = true;
                    }

                    // if (code == 201)
                    // {
                    //     PrintWarning($"code - {code}, resp - {response}");
                    //     PrintWarning("Plugin not working. Add Gamestores shop info into config!");
                    //     Interface.Oxide.UnloadPlugin(Title);
                    // }
                }, this, Core.Libraries.RequestMethod.GET);
        }
        #endregion

        public enum CallbackCode : byte
        {
            Ok = 0,
            Error = 1
        }

        #region Lang
        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["give.argsIsNull"] = "Использование - cgive 'skinid монеты из конфига' 'кол-во' (опционально - userid)",
                ["give.notFound"] = "Монета не найдена. Используйте /coins что бы узнать все монеты",
                ["give.success"] = "Вы выдали себе {0} монет в инвентарь {1}!",
                ["error.wrongWebRequest"] = "Внутренняя ошибка сервера. Ваши монеты в безопасности! Сообщите администрации.",
                ["weblogger.error"] = "Произошла внутренняя ошибка. Баланс игрока {0} не изменён",
                ["weblogger.success"] = "Игрок [{0}/{1}] получил {2} руб на баланс в магазине",
                ["withdraw.notenoughcoins"] = "Вам не хватает монет. Нужно минимум {0}.",
                ["withdraw.success"] =  "Вы получили {0} руб. на баланс. Вы потратили {1} монет.",
                ["error.moveToInventory"] = "Переместите монеты в инвентарь и попробуйте снова."
            }, this, "ru");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["give.argsIsNull"] = "Usage - cgive 'skinid coins from config' 'amount' (optional - userid)",
                ["give.notFound"] = "The coin was not found. Use /coins for know all coins",
                ["give.success"] = "You have given yourself {0} coins in your inventory {1}!",
                ["error.wrongWebRequest"] = "Internal server error. Your coins are safe! Inform the administration.",
                ["weblogger.error"] = "An internal error has occurred. The player's balance {0} has not been changed",
                ["weblogger.success"] = "The player [{0}/{1}] received {2} rubles to the balance in the store",
                ["withdraw.notenoughcoins"] = "You don't have enough coins. You need at least {0}.",
                ["withdraw.success"] =  "You have received {0} rubles to the balance. You have spent {1} coins.",
                ["error.moveToInventory"] = "Move coins to inventory and try again."
            }, this);
        }
        string GetMsg(string key, ulong id) => lang.GetMessage(key, this, id.ToString());
        #endregion
    }
}