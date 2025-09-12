using System;
using System.Collections.Generic;
using Facepunch.Extend;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("EconomicsEvo","Netrunner","1.0")]
    public class EconomicsEvo: RustPlugin
    {
        #region Fields

        static EconomicsEvo _;

        [PluginReference] private Plugin ImageLibrary,Broadcast;

        #endregion

        #region Config

        class Currency
        {
            [JsonProperty("Название валюты")]
            public string Name;
            [JsonProperty("Виртуальная/физическая(true/false)")]
            public bool Vitrual;
            [JsonProperty("Максимальное кол-во")]
            public float Max;
            [JsonProperty("Может быть отрицательным")]
            public bool Negative;
            [JsonProperty("Картинка")]
            public string Image = "";
            [JsonProperty("skinId(Если физическая)")]
            public ulong SkinId = 0;
            [JsonProperty("Начальный бонус")]
            public int StartAmount = 1000;
            [JsonProperty("Сбрасывать после вайпа?(true/false)")]
            public bool WipeOnNewSave = false;
        }

        class CurrencySettings
        {
            [JsonProperty("Настройка валюты за удар")]
            public HitFarm HitFarm = new HitFarm();
            [JsonProperty("Настройка валюты за бонус")]
            public BonusFarm BonusFarm = new BonusFarm();
            [JsonProperty("Настройка валюты за время")]
            public TimeBonus TimeBonus = new TimeBonus();
            [JsonProperty("Настройка валюты за добивание")]
            public KillPoints KillPoints = new KillPoints();
            [JsonProperty("Настройка валюты за собирательство")]
            public LootPoints LootPoints = new LootPoints();
            
        }

        class HitFarm
        {
            [JsonProperty("Начислять валюту за каждый удар по ресурсу?")]
            public bool EnableHitFarm;
            
            [JsonProperty("Валюта за добычу серы")]
            public double SulfurOreGather = 0.1;
            [JsonProperty("Валюта за добычу железа")]
            public double MetalOreGather = 0.1;
            [JsonProperty("Валюта за добычу камня")]
            public double StoneOreGather = 0.1;
            
            [JsonProperty("Валюта за добычу дерева")]
            public double WoodGather = 0.1;
        }

        class BonusFarm
        {
            [JsonProperty("Начислять валюту за последний удар по ресурсу?")]
            public bool EnableBonusFarm = false;
            
            [JsonProperty("Валюта за бонус серы")]
            public double SulfurOreBonus = 0.1;
            [JsonProperty("Валюта за бонус железа")]
            public double MetalOreBonus= 0.1;
            [JsonProperty("Валюта за бонус камня")]
            public double StoneOreBonus= 0.1;
            
            [JsonProperty("Валюта за бонус дерева")]
            public double WoodBonus= 0.1;
        }

        class TimeBonus
        {
            [JsonProperty("Начислять валюту за последний удар по ресурсу?")]
            public bool EnableTimeBonus = false;
            
            [JsonProperty("Настройки валюты за время")]
            public TimePoints TimePoint = new TimePoints();
        }
        
        class KillPoints
        {
            [JsonProperty("Начислять валюту за добивания?")]
            public bool EnableKillBonus = false;
            [JsonProperty("Валюта за убийство NPC")]
            public double Npc = 1;
            [JsonProperty("Валюта за убийство Животного")]
            public double Animal = 1;
            [JsonProperty("Валюта за убийство Игрока")]
            public double Player = 2;
            [JsonProperty("Валюта за убийство Танка")]
            public double Tank = 5;
            [JsonProperty("Валюта за убийство Вертолета")]
            public double Heli = 5;
            [JsonProperty("Валюта за убийство Чинука")]
            public double Ch47 = 5;
        }

        class TimePoints
        {
            [JsonProperty("Как часто давать валюту за время(секунды)")]
            public int Time = 60;
            [JsonProperty("Количество валюты за время")]
            public double Amount = 1;
            [JsonProperty("Умножать ли бонус спустя определенное время?")]
            public bool EnableTimeScale = false;
            [JsonProperty("Как часто увеличивать награду")]
            public float ScaleRate = 1;
            [JsonProperty("Множитель награды")]
            public double ScaleModiier = 1.2;
        }

        class LootPoints
        {
            [JsonProperty("Начислять валюту за Собирательство?")]
            public bool EnableLootBonus = false;
            [JsonProperty("Валюта за разрушение бочки")]
            public double Barrel = 0;
            [JsonProperty("Валюта за открытие залоченого контейнера")]
            public double LockedContainer = 0;
            [JsonProperty("Валюта за собирательство")]
            public double PickUp = 0;
        }
        
        static Configuration config = new Configuration();

        class Configuration
        {
            [JsonProperty("Валюты")] 
            public Dictionary<string,Currency> Currencies;
            [JsonProperty("Настройка  валют")]
            public Dictionary<string, CurrencySettings> Settings;

            [JsonProperty("Настройка  rates")]
            public Dictionary<string, double> Rates;

            
            [JsonProperty("Использовать GUI(true/false)")]
            public bool UseUI = true;
            
            public static Configuration GetNewConfiguration()
            {
                return new Configuration
                {
                    Currencies = new Dictionary<string, Currency>
                    {
                        ["basic"] =  new Currency
                        {
                            Name = "Basic",
                            Negative = false,
                            Max = 10000,
                            StartAmount = 1000,
                            Vitrual = true,
                            Image = "https://i.ibb.co/qJkjy4t/testLogo.png",
                            SkinId = 0,
                            WipeOnNewSave = true
                        },
                        ["secondary"] =  new Currency
                        {
                            Name = "Basic",
                            Negative = false,
                            Max = 10000,
                            StartAmount = 1000,
                            Vitrual = true,
                            Image = "https://i.ibb.co/qJkjy4t/testLogo.png",
                            SkinId = 0,
                            WipeOnNewSave = true
                        },
                    },
                    Rates = new Dictionary<string, double>
                    {
                        ["default"] = 1.0,
                        ["vip"] = 2.0
                    },
                    Settings = new Dictionary<string, CurrencySettings>
                    {
                        ["basic"] = new CurrencySettings(),
                        ["secondary"] = new CurrencySettings()
                    }
                };
            }
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) PrintWarning("NULL");
                if (config == null) LoadDefaultConfig();
            }
            catch
            {
                PrintError($"Не удалось найти конфигурацию 'oxide/config/{Name}', Создание конфига!!");
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }
        protected override void LoadDefaultConfig() => config = Configuration.GetNewConfiguration();
        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion

        #region Data

        private Dictionary<ulong,double>_playerRates = new Dictionary<ulong, double>();

        private static Dictionary<string,Dictionary<ulong,double>>_database = new Dictionary<string, Dictionary<ulong, double>>();
        void InitDataBase()
        {
            
            /*foreach (var currency in config.Currencies)
            {
                if (!_database.ContainsKey(currency.Key))
                {
                    _database.Add(currency.Key, new Dictionary<ulong, double>());
                    PrintWarning($"Валюта {currency.Value.Name} добавлена в базу данных");
                }
                else
                {
                    PrintWarning($"Данные валюты {currency.Value.Name} загружены в базу данных");
                }
                
            }*/
            LoadData();
            //SaveData();
        }
        
        
        void SaveData()
        {
            foreach (var data in _database)
            {
                PrintWarning($"Данные валюты {config.Currencies[data.Key].Name} сохранены с {data.Value.Count} записей");
                Interface.Oxide.DataFileSystem.WriteObject($"EconomicsEvo/{data.Key}",data.Value);
                PrintWarning($"Данные валюты \'{config.Currencies[data.Key].Name}\' сохранены");
            }
        }

        bool ExistCurrency(string key)
        {
            if (config.Currencies.ContainsKey(key))
            {
                PrintError($"Валюта с ключом {key} не найдена!");
                return false;
            }

            return true;
        }

        
        void LoadData()
        {
            foreach (var currency in config.Currencies)
            {
                PrintWarning($"Начата обработка валюты {currency.Value.Name}");
                if (Interface.Oxide.DataFileSystem.ExistsDatafile($"EconomicsEvo/{currency.Key}"))
                {
                    if (_database.ContainsKey(currency.Key))
                    {
                        _database[currency.Key] =
                            Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, double>>($"EconomicsEvo/{currency.Key}");
                        //PrintWarning($"Данные валюты {currency.Value.Name} загружены");
                        PrintWarning($"[{currency.Value.Name}] загружено {_database[currency.Key].Count} записей");
                    }
                    else
                    {
                        _database.Add(currency.Key,
                            Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, double>>(
                                $"EconomicsEvo/{currency.Key}"));
                    }
                }
                else
                {
                    PrintError($"Данные валюты {currency.Value.Name} не обнаружены. Будут созданы новые данные");
                    _database.Add(currency.Key,new Dictionary<ulong, double>());
                }
            }
            
        }
        
        void CheckPlayerData(BasePlayer player, string currency)
        {
            ulong playerid = player.userID;
            Currency cur = config.Currencies[currency];
            /*if (!_database.ContainsKey(currency))
            {
                PrintError("Not Contains");
                _database.Add(currency,new Dictionary<ulong, double>());
                PrintWarning($"Create {currency} data");
            }*/
            PrintWarning($"{cur.Name} Имеет {_database[currency].Count} записей");
            
            if (!_database[currency].ContainsKey(playerid))
            {
                _database[currency].Add(playerid,cur.StartAmount);
                PrintWarning($"Добавлен игрок в валюту {cur.Name} со стартовым значением {cur.StartAmount}");
            }
            
        }
        
        #endregion

        #region Hooks
        

        void OnServerInitialized()
        {
            _ = this;
            _.PrintWarning("Init");
            InitDataBase();
            
            foreach (var perm in config.Rates.Keys) permission.RegisterPermission("economicsevo."+perm,this);
            timer.Once(3f, InitImages);
            foreach (var player in BasePlayer.activePlayerList) OnPlayerConnected(player); 
           /*timer.Every(60f, SaveData);*/
        }

        void OnPlayerConnected(BasePlayer player)
        {
            foreach (var var in config.Currencies) CheckPlayerData(player,var.Key);
            CalcPlayerRates(player);
            if (player.GetComponent<TimeReward>() == null) player.gameObject.AddComponent<TimeReward>();
        }
        
        void DestroyTimeBonusObjects()
        {
            foreach (var obj in UnityEngine.Object.FindObjectsOfType<TimeReward>()) UnityEngine.Object.Destroy(obj);
        }
        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player.GetComponent<TimeReward>() != null) UnityEngine.Object.DestroyImmediate(player.GetComponent<TimeReward>());
        }
        void Unload()
        {
            DestroyTimeBonusObjects();
            SaveData();
        }
        
        void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            if (!(entity is BasePlayer)) return;
            BasePlayer player = entity as BasePlayer;
            switch (item.info.shortname)
            {
                case "stones":
                    foreach (var var in config.Settings)
                        if (var.Value.HitFarm.EnableHitFarm)
                            AddBalanceByID(player.userID, var.Key,var.Value.HitFarm.StoneOreGather,true);
                    break;
                case "sulfur.ore":
                    foreach (var var in config.Settings)
                        if (var.Value.HitFarm.EnableHitFarm)
                            AddBalanceByID(player.userID, var.Key,var.Value.HitFarm.SulfurOreGather,true);
                    break;
                case "metal.ore":
                    foreach (var var in config.Settings)
                        if (var.Value.HitFarm.EnableHitFarm)
                            AddBalanceByID(player.userID, var.Key,var.Value.HitFarm.MetalOreGather,true);
                    break;
                case "wood":
                    foreach (var var in config.Settings)
                        if (var.Value.HitFarm.EnableHitFarm)
                            AddBalanceByID(player.userID, var.Key,var.Value.HitFarm.WoodGather,true);
                    break;
            }
        }
        
        void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            switch (item.info.shortname)
            {
                case "stones":
                    foreach (var var in config.Settings)
                        if (var.Value.BonusFarm.EnableBonusFarm)
                            AddBalanceByID(player.userID, var.Key,var.Value.BonusFarm.StoneOreBonus,true);
                    break;
                case "sulfur.ore":
                    foreach (var var in config.Settings)
                        if (var.Value.BonusFarm.EnableBonusFarm)
                            AddBalanceByID(player.userID, var.Key,var.Value.BonusFarm.SulfurOreBonus,true);
                    break;
                case "hq.metal.ore":
                    foreach (var var in config.Settings)
                        if (var.Value.BonusFarm.EnableBonusFarm)
                            AddBalanceByID(player.userID, var.Key,var.Value.BonusFarm.MetalOreBonus,true);
                    break;
                case "wood":
                    foreach (var var in config.Settings)
                        if (var.Value.BonusFarm.EnableBonusFarm)
                            AddBalanceByID(player.userID, var.Key,var.Value.BonusFarm.WoodBonus,true);
                    break;
            }
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info== null) return;
            if (info.InitiatorPlayer == null || info.InitiatorPlayer.userID == 0) return;
                
            
            BasePlayer player = info.InitiatorPlayer;
            if ( entity is ScientistNPC)
                foreach (var money in config.Currencies)
                {
                    if (config.Settings[money.Key].KillPoints.EnableKillBonus)
                        AddBalanceByID(player.userID, money.Key, config.Settings[money.Key].KillPoints.Npc, true);
                    return;
                }
            if ( entity is BaseAnimalNPC)
                foreach (var money in config.Currencies)
                {
                    if (config.Settings[money.Key].KillPoints.EnableKillBonus)
                        AddBalanceByID(player.userID, money.Key, config.Settings[money.Key].KillPoints.Animal, true);
                    return;
                }

            if ( entity is BradleyAPC)
                foreach (var money in config.Currencies)
                {
                    if (config.Settings[money.Key].KillPoints.EnableKillBonus)
                        AddBalanceByID(player.userID, money.Key, config.Settings[money.Key].KillPoints.Tank, true);
                    return;
                }
            if ( entity is BaseHelicopter)
                foreach (var money in config.Currencies)
                {
                    if (config.Settings[money.Key].KillPoints.EnableKillBonus)
                        AddBalanceByID(player.userID, money.Key, config.Settings[money.Key].KillPoints.Heli, true);
                    return;
                }
            if ( entity is BasePlayer && !(entity is ScientistNPC)&& !(entity is NPCDwelling)&& !(entity is BanditGuard)&& !(entity is ScarecrowNPC) && !(info.InitiatorPlayer == info.HitEntity as BasePlayer))
                foreach (var money in config.Currencies)
                {
                    if (config.Settings[money.Key].KillPoints.EnableKillBonus)
                        AddBalanceByID(player.userID,money.Key, config.Settings[money.Key].KillPoints.Player, true);
                    return;
                }


        }
        
        #endregion

        #region TimeBonusComponent

        class TimeSettings
        {
            public double Amount = 0;

            public float Time = 60f;

            public bool UseMultiplier = false;

            public float MultiplierInterval = 60f;

            public double Multiplier = 1;
        }

        
        class TimeReward : FacepunchBehaviour
        {
            private BasePlayer Player;
            
            Dictionary<string,TimeSettings> CurrencyTimes = new Dictionary<string, TimeSettings>();

            private void Awake()
            {
                Player = GetComponent<BasePlayer>();
                _.PrintWarning($"Start init {Player}");
                foreach (var currency in config.Settings)
                    if (currency.Value.TimeBonus.EnableTimeBonus) FormatCurrency(currency.Key,currency.Value.TimeBonus);
                InitTimers();
            }

            void FormatCurrency(string key,TimeBonus timeBonus)
            {
                
                TimeSettings timeSettings = new TimeSettings();
                timeSettings.Amount = timeBonus.TimePoint.Amount;
                timeSettings.Time = timeBonus.TimePoint.Time;
                timeSettings.UseMultiplier = timeBonus.TimePoint.EnableTimeScale;
                timeSettings.MultiplierInterval = timeBonus.TimePoint.ScaleRate;
                timeSettings.Multiplier = timeBonus.TimePoint.ScaleModiier;
                CurrencyTimes.Add(key,timeSettings);
            }

            void InitTimers()
            {
                foreach (var time in CurrencyTimes)
                {
                    if (time.Value.UseMultiplier)
                        InvokeRepeating(() => { ScaleBonus(time.Key); },
                            time.Value.MultiplierInterval, time.Value.MultiplierInterval);
                    InvokeRepeating(() => {AddTimeBonus(time.Key,time.Value.Amount);},time.Value.Time,time.Value.Time);
                }
                
            }

            void ScaleBonus(string key)
            {
                if (CurrencyTimes[key].UseMultiplier) CurrencyTimes[key].Amount *= CurrencyTimes[key].Multiplier;
            }

            void AddTimeBonus(string key,double amount)
            {
                _.AddBalanceByID(Player.userID,key,amount,true);
            }
        }

        #endregion

        #region Methods

        
        void InitImages()
        {
            foreach (var currency in config.Currencies)
            {
                ImageLibrary.Call("AddImage", currency.Value.Image, currency.Key);
                PrintWarning($"Картинка валюты {currency.Key} загружена({currency.Value.Image})");
            }
        }

        void CalcPlayerRates(BasePlayer player)
        {
            if (_playerRates.ContainsKey(player.userID)) return;

            double rate = 1.0;
            foreach (var perm in config.Rates)
                if (permission.UserHasPermission(player.UserIDString, "economics." + perm) && perm.Value > rate)
                    rate = perm.Value;
            _playerRates.Add(player.userID,rate);
            PrintWarning($"игроку {player} рейты валюту установлены на {rate}");
        }


        #region Transaction

        void AddBalanceByID(ulong playerId,string currency,double amount, bool useMultiplier = false)
        {
            PrintWarning("AddBalanceByID");

            if (!_database.ContainsKey(currency))
            {
                PrintWarning("No currency");
                return;
            }

            if (!_database[currency].ContainsKey(playerId))
            {
                PrintWarning("No player");
                return;
            }
            if (useMultiplier) amount *= _playerRates[playerId];

            _database[currency][playerId] += amount;
            BasePlayer player = BasePlayer.FindByID(playerId);
            if (player != null)
            {
                if (BasePlayer.activePlayerList.Contains(player))
                {
                    Broadcast.Call("GetPlayerNotice", player, "Баланс", 
                        $"<b>ПОЛУЧЕНО: <color=#e999c4>{amount}</color> <color=#EBD4AE>{config.Currencies[currency].Name}</color></b>", currency,
                        "assets/bundled/prefabs/fx/invite_notice.prefab");
                }
            }
            LogDeposit(playerId.ToString(),amount,currency);
        }
        void AddBalanceByID(ulong playerId,string currency,int amount, bool useMultiplier = false)
        {
            AddBalanceByID(playerId,currency,(double)amount,useMultiplier);
        }

        double GetBalance(ulong playerId,string currency)
        {
            if (!_database.ContainsKey(currency))
            {
                PrintWarning("No currency");
                return 505;
            }

            if (!_database[currency].ContainsKey(playerId))
            {
                PrintWarning("No player");
                return 404;
            }
            
            return _database[currency][playerId];
        }
        
        private void RemoveBalanceByID(ulong playerId, string currency, double amount)
        {
           _database[currency][playerId] -= amount;
           LogWithdraw(playerId.ToString(),amount,currency);
        }
        private void RemoveBalanceByID(ulong playerId, string currency, int amount)
        {
            _database[currency][playerId] -= amount;
            LogWithdraw(playerId.ToString(),amount,currency);
        }

        void SetBalanceByID(ulong playerId, string currency, double amount)
        {
            _database[currency][playerId] = amount;
            LogSetBalance(playerId.ToString(),amount,currency);
        }

        #endregion

        #endregion

        #region Logs
        
        private string _logFile = "EconomicsEvo";
        
        void LogDeposit(string userid, double amount,string currency)
        {
            
            if (amount !=0)
            {
                LogToFile($"{currency}",$"Added {amount} for {userid}",this);
            }
        }

        void LogSetBalance(string userid, double amount, string currency)
        {
            LogToFile($"{currency}",$"Set Balnce for {userid} : {amount}",this);
        }
        
        void LogWithdraw(string userid, double amount, string currency)
        {
            LogToFile($"{currency}",$"Withdraw {amount} for {userid}",this);
        }
        
        void LogAdminCommand(BasePlayer player, string text)
        {
            LogToFile("admin",$"Admin[{player}]: {text}",this);
        }

        void LogServerCommand(string target, int amount,string currency)
        {
            LogToFile("server",$"SERVER:Add {amount}x{currency} to {target}",this);
        }

        #endregion

        #region Commands

        [ChatCommand("einfo")]
        void GetInfo(BasePlayer player, string command, string[] args)
        {
            foreach (var data in _database)
            {
                SendReply(player,data.Key);
                foreach (var eco in data.Value)
                {
                    SendReply(player,$"[{player.userID}] {eco.Key}:{eco.Value}");
                }
            }
            SaveData();
            
        }

        [ConsoleCommand("moneyadd")]
        void ServerAddBalanceToPlayer(ConsoleSystem.Arg arg)
        {
            if (!arg.IsServerside) return;
            if (arg.HasArgs(3))
            {
                ulong useid = Convert.ToUInt64(arg.Args[0]);
                int amount = arg.Args[1].ToInt();
                string currency = arg.Args[2];
                AddBalanceByID(useid, currency, amount);
                PrintWarning($"SERVER ADD BALANCE TO {useid}");
                return;
                
            }
        }

        [ChatCommand("eco")]
        void AdminCommands(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin || args == null) return;
            string text = $"Admin {player} ";
            if (args.Length < 2)
            {
                SendReply(player,$"Wrong Command\n" +
                                 $"Command list:" +
                                 $"/eco set (playerid/name) (amount) - to change default balance\n" +
                                 $"/eco set (playerid/name) (amount) (currency) - to change currency balance\n" +
                                 $"/eco setme (amount) (currency) - to change your currency balance\n" +
                                 $"/eco give (playerid/name) (amount) - give balance(default) for player\n" +
                                 $"/eco give (playerid/name) (amount) (currency) - give balance for player\n" +
                                 $"/eco giveme (amount) (currency) - give balance for player\n" +
                                 $"/eco withdraw (playerid/name) (amount) - withdraw player balance(default)\n" +
                                 $"/eco withdraw (playerid/name) (amount) (currency) - withdraw player balance\n" +
                                 $"/eco withdrawme (amount) (currency) - withdraw your balance \n");
            }

            BasePlayer target = BasePlayer.Find(args[1]);
            switch (args[0])
            {
                case "set":
                    if (args.Length<4)
                    {
                        SendReply(player,$"Use: {command} set player amount currency");
                        return;
                    }
                    if (args.Length >= 4)
                    {
                        double amount = Convert.ToDouble(args[2]);
                        text += "set ";
                        text += args[1];
                        text += " ";
                        text += args[2];
                        text += " ";
                        text += args[3];
                        LogAdminCommand(player,text);
                        SetBalanceByID(target.userID, args[3],Convert.ToDouble(args[2]));
                        return;
                    }
                    break;
                case "setme":
                    if (args.Length<3)
                    {
                        SendReply(player,$"Use: {command} setme amount currency");
                        return;
                    }

                    if (args.Length >= 3)
                    {
                        double amount = Convert.ToDouble(args[1]);
                        text += "setme ";
                        text += args[1];
                        text += " ";
                        text += args[2];
                        LogAdminCommand(player,text);
                        SetBalanceByID(player.userID, args[2], amount);
                        return;
                    }
                    break;
                case "withdraw":
                    
                    if (args.Length<4)
                    {
                        SendReply(player,$"Use: {command} withdraw player amount currency");
                        return;
                    }

                    if (args.Length >= 4)
                    {
                        double amount = Convert.ToDouble(args[2]);
                        text += "withdraw ";
                        text += args[1];
                        text += " ";
                        text += args[2];
                        text += " ";
                        text += args[3];
                        LogAdminCommand(player,text);
                        RemoveBalanceByID(target.userID, args[3],amount);
                        return;
                    }
                    break;
                case "withdrawme":
                    
                    if (args.Length<3)
                    {
                        SendReply(player,$"Use: {command} withdrawme amount currency");
                        return;
                    }

                    if (args.Length >= 3)
                    {
                        double amount = Convert.ToDouble(args[1]);
                        text += "withdrawme ";
                        text += args[1];
                        text += " ";
                        text += args[2];
                        LogAdminCommand(player,text);
                        RemoveBalanceByID(player.userID, args[2],amount);
                        return;
                    }
                    break;
                case "give":
                    if (args.Length<4)
                    {
                        SendReply(player,$"Use: {command} give player amount currency");
                        return;
                    }

                    if (args.Length >= 4)
                    {
                        double amount = Convert.ToDouble(args[2]);
                        text += "give ";
                        text += args[1];
                        text += " ";
                        text += args[2];
                        text += " ";
                        text += args[3];
                        LogAdminCommand(player,text);
                        AddBalanceByID(target.userID,args[3],amount);
                        return;
                    }
                    break;
                case "giveme":
                    
                    if (args.Length<3)
                    {
                        SendReply(player,$"Use: {command} giveme amount currency");
                        return;
                    }

                    if (args.Length >= 3)
                    {
                        double amount = Convert.ToDouble(args[1]);
                        text += "giveme ";
                        text += args[1];
                        text += " ";
                        text += args[2];
                        LogAdminCommand(player,text);
                        AddBalanceByID(player.userID,args[2],amount);
                        return;
                    }
                    break;
                case "giveall":
                    
                    if (args.Length<3)
                    {
                        SendReply(player,$"Use: {command} giveall amount currency");
                        return;
                    }

                    if (args.Length >= 3)
                    {
                        double amount = Convert.ToDouble(args[1]);
                        text += "giveme ";
                        text += args[1];
                        text += " ";
                        text += args[2];
                        LogAdminCommand(player,text);
                        foreach (var players in BasePlayer.activePlayerList)
                        {
                            AddBalanceByID(players.userID,args[2],amount);
                        }
                        
                        return;
                    }
                    break;
            }

        }

        #endregion
    }
}