using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Rust;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using Apex.AI.Components;
using Rust.Ai;
using UnityEngine.SceneManagement;
using Facepunch;
using VLB;

namespace Oxide.Plugins
{
    [Info("ChickensAndEggs", "https://discord.gg/9vyTXsJyKR", "1.0.0")] 
    class ChickensAndEggs : RustPlugin
    {
		// variables
		
		[PluginReference]
  		private Plugin Farmer;
		
		private static Dictionary<BaseNpc, List<BasePlayer>> ChickenPlayers = new Dictionary<BaseNpc, List<BasePlayer>>();
				
		static int layerPlcmnt = LayerMask.GetMask("Construction", "Default", "Deployed", "World", "Terrain");

        static ChickensAndEggs ins;
		
		private static System.Random Rnd = new System.Random(); 
		
		Dictionary<BasePlayer, bool> CreateLoot = new Dictionary<BasePlayer, bool>();
		
		private static List<ulong> LockOtherLooting = new List<ulong>();
		
		bool init = false;
		
		// config
		
        private PluginConfig config;

        protected override void LoadDefaultConfig()
        {            
            config = PluginConfig.DefaultConfig();
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();

            if (config.PluginVersion < Version)
                UpdateConfigValues();
			
			if (config.ChickenLifeHours == 0)
				config.ChickenLifeHours = 24;

            Config.WriteObject(config, true);
        }

        private void UpdateConfigValues()
        {
            PluginConfig baseConfig = PluginConfig.DefaultConfig();
            if (config.PluginVersion < new VersionNumber(0, 1, 0))
            {
                PrintWarning("Config update detected! Updating config values...");
                PrintWarning("Config update completed!");
            }
            config.PluginVersion = Version;
        }

        protected override void SaveConfig() => Config.WriteObject(config);        

        class PluginConfig
        {
            [JsonProperty("Configuration Version")]
            public VersionNumber PluginVersion = new VersionNumber();

            [JsonProperty("Вероятность что игрок при разделывании курицы получит специальное яйцо (До 100%)")]
            public int ChangeGiveSpecialEgg = 5;

            [JsonProperty("Вероятность что игрок при разделывании курицы получит обычное яйцо (До 100%)")]
            public int ChangeGiveNormalEgg = 5;

            [JsonProperty("Разрешать игрокам улучшать обычное яйцо в специальные (10 обычных в 1 специальное)")]
            public bool EnabledUpgradeEgg = true;

            [JsonProperty("Время за какие яйцо превратится в курицу (секунды)")]
            public float TimeToChicken = 3600f;

            [JsonProperty("Время за какое курица даёт яйца")]
            public float TimeToEggs = 3600f;

            [JsonProperty("Количество яиц какое дает курица за указаное в конфигурации время")]
            public int ChickenGiveEggsCount = 1;

            [JsonProperty("Сколько курица потребляет еды за время вынашивания яйца")]
            public int ChickenEatCount = 1;

            [JsonProperty("Вероятность что курица снесет специального яйцо а особое (До 100%)")]
            public int Change = 10;

            [JsonProperty("Настройки обычного яйца")]
            public NormalEgg Normal;

            [JsonProperty("Настройки специального яйца")]
            public SpecialEgg Special;

            [JsonProperty("Отображение текста-информации над курицей только для авторизованых в шкафу")]
            public bool EnabledAuthCupboard = true;

            [JsonProperty("Разрешить лутать курицу только для авторизованых в шкафу")]
            public bool EnabledAuthCupboardInLoot = true;

            [JsonProperty("Запретить курице вынашивать яйца если у неё уже есть N-количество яиц (Указанное в конфигурации)")]
            public bool DisableCreateEggs = true;

            [JsonProperty("Количество яиц у курицы чтобы она остановила нести их (Нужно включен запрет)")]
            public int EggsCount = 5;

            [JsonProperty("Максимальное количество куриц возле 1 кормушки")]
            public int MaxChickenCount = 10;

            [JsonProperty("При убийстве курицы выбрасывать все яйца на землю (Если false то лут просто пропадёт)")]
            public bool EnabledDropEggs = true;
			
			[JsonProperty("Время жизни курицы (часы)")]
            public int ChickenLifeHours = 24;

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    PluginVersion = new VersionNumber(),
                    ChickenEatCount = 1,
                    ChickenGiveEggsCount = 1,
                    TimeToChicken = 3600f,
                    TimeToEggs = 3600f,
                    Change = 10,
                    ChangeGiveNormalEgg = 50,
                    ChangeGiveSpecialEgg = 10,
                    EnabledAuthCupboard = true,
                    EnabledAuthCupboardInLoot = true,
                    EnabledDropEggs = true,
					ChickenLifeHours = 24,
                    EnabledUpgradeEgg = false,
                    DisableCreateEggs = true,
                    EggsCount = 5,
                    MaxChickenCount = 10,
                    Normal = new NormalEgg()
                    {
                        MaxCount = 4,
                        Name = "Обычное куриное яйцо",
                        SkinEgg = 1916792036,
                        ItemsList = new List<EggItemSettings>()
                        {
                            new EggItemSettings()
                            {
                                    ShortName = "stones",
                                    MinAmount = 300,
                                    MaxAmount = 1000,
                                    Change = 100,
                                    Name = "",
                                    SkinID = 0,
                                    IsBlueprnt = false
                            }
                        }
                    },
                    Special = new SpecialEgg()
                    {
                        MaxCount = 4,
                        Name = "Имя специального яйца",
                        SkinEgg = 1916791710, 
                        ItemsList = new List<EggItemSettings>()
                        {
                            new EggItemSettings()
                            {
                                    ShortName = "wood",
                                    MinAmount = 300,
                                    MaxAmount = 1000,
                                    Change = 100,
                                    Name = "",
                                    SkinID = 0,
                                    IsBlueprnt = false
                            }
                        }
                    }

                };
            }
        }
		
		public class SpecialEgg
        {
            [JsonProperty("SkinID специального яйца")]
            public ulong SkinEgg = 1916791710;
            [JsonProperty("Имя специального яйца")]
            public string Name = "Яйцо для выращивания курицы";
            [JsonProperty("Максимальное количество итемов какое может выпасть с яйца")]
            public int MaxCount = 4;
            [JsonProperty("Список предметов, и их настройка")]
            public List<EggItemSettings> ItemsList = new List<EggItemSettings>();
        }

        public class NormalEgg
        {
            [JsonProperty("SkinID обычного яйца")]
            public ulong SkinEgg = 1916792036;
            [JsonProperty("Имя простого яйца")]
            public string Name = "Обычное куриное яйцо";
            [JsonProperty("Максимальное количество итемов какое может выпасть с яйца")]
            public int MaxCount = 4;
            [JsonProperty("Список предметов, и их настройка")]
            public List<EggItemSettings> ItemsList = new List<EggItemSettings>();
        }

        public class EggItemSettings
        {
            [JsonProperty("Shortname предмета")]
            public string ShortName;
            [JsonProperty("Минимальное количество")]
            public int MinAmount;
            [JsonProperty("Максимальное количество")]
            public int MaxAmount;
            [JsonProperty("Шанс что предмет будет добавлен (максимально 100%)")]
            public int Change;
            [JsonProperty("SkinID предмета")]
            public ulong SkinID;
            [JsonProperty("Имя предмета при создании (Оставте поле постым чтобы использовать стандартное название итема)")]
            public string Name;
            [JsonProperty("Это чертеж")]
            public bool IsBlueprnt;
        }
		
		// data
		
		public Dictionary<ulong, PlayersData> PlayersEggs = new Dictionary<ulong, PlayersData>();

		public class PlayersData
        {
            public Dictionary<uint, PlayerEggs> Eggs = new Dictionary<uint, PlayerEggs>();
            public Dictionary<uint, PlayeChickens> Chickens = new Dictionary<uint, PlayeChickens>();
        }

        public class PlayerEggs
        {
            public Vector3 position;
            public float nextEatTime;
        }

        public class PlayeChickens
        {
            public Vector3 position;
            public uint TroughID;
            public float nextEatTime;
            public int HP;
            public int EggsCount = 0;
            public int SpecialEggs = 0;
            public bool NeedEat = true;            

            public bool ChickenEat()
            {
                if (hitch == null) HitchTrough();
                Item foodItem = hitch.GetFoodItem();
                if (foodItem != null && foodItem.amount >= ins.config.ChickenEatCount)
                {
                    ItemModConsumable component = foodItem.info.GetComponent<ItemModConsumable>();
                    if (component)
                    {
                        foodItem.UseItem(ins.config.ChickenEatCount);
                        return true;
                    }
                }
                return false;
            }

            public void HitchTrough()
            {
                List<HitchTrough> list = new List<HitchTrough>();
                Vis.Entities<HitchTrough>(position, 2f, list, LayerMask.GetMask(new string[] { "Deployed" }));
                if (list.Count > 0)
                    hitch = list.First();
            }

            [JsonIgnore] public HitchTrough hitch;

            public void AddBoxEggs(StorageContainer container, ulong userID)
            {
                if (UnityEngine.Random.Range(1, 100) < ins.config.Change)
                {
                    var item = ItemManager.CreateByName("easter.goldegg", 1, ins.config.Special.SkinEgg);
                    item.name = ins.config.Special.Name;
					item.text = userID.ToString();
                    item.MoveToContainer(container.inventory);
                }
                else
                {
                    var item = ItemManager.CreateByName("easter.bronzeegg", ins.config.ChickenGiveEggsCount, ins.config.Normal.SkinEgg);
                    item.name = ins.config.Normal.Name;
					item.text = userID.ToString();
                    item.MoveToContainer(container.inventory);
                }
            }

            public void ReplaceEggs(StorageContainer container, int specialCount, int normalCount, ulong userID)
            {
                if (specialCount > 0)
                {
                    var item = ItemManager.CreateByName("easter.goldegg", specialCount, ins.config.Special.SkinEgg);
                    item.name = ins.config.Special.Name;
					item.text = userID.ToString();
                    item.MoveToContainer(container.inventory);
                }
                if (normalCount > 0)
                {
                    var item = ItemManager.CreateByName("easter.bronzeegg", normalCount, ins.config.Normal.SkinEgg);
                    item.name = ins.config.Normal.Name;
					item.text = userID.ToString();
                    item.MoveToContainer(container.inventory);
                }
            }
        }
		
        void LoadData()
        {
            try
            {
                PlayersEggs = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<ulong, PlayersData>>($"{Title}_Players");
                if (PlayersEggs == null)
                    PlayersEggs = new Dictionary<ulong, PlayersData>();
            }
            catch
            {
                PlayersEggs = new Dictionary<ulong, PlayersData>();
            }
        }
		
		void SaveData()
        {
            if (PlayersEggs != null)
                Interface.Oxide.DataFileSystem.WriteObject($"{Title}_Players", PlayersEggs);
        }
		
		// messages
		
		public static Dictionary<string, string> Messages = new Dictionary<string, string>()
        {
            {"InfoText", "Устанавливать яйцо нужно только возле кормушек" },
            {"Error Place Chicken Count", "Возле данной кормушки уже максимальное количество куриц" },
            {"Error Place Egg Count", "Возле данной кормушки уже максимальное количество яиц" },
            {"Error Place Eggs end Chicken Count", "Возле данной кормушки уже максимальное количество яиц и куриц" },
            {"Error Place", "Яйца нужно устанавливать возле кормушек. В данный момент она не найдена" },
            {"EggsInfoText", "<size=25><b>Яйцо с цыплёнком</b></size>\n<size=17>\nСкоро с него вылупится курица\nОсталось времени: {0}</size>" },
            {"ChickenInfoText", "<size=25><b>Домашняя курица</b></size>\n<size=17>\n\n{0}</size>" },
            {"AddInfoChicken", "<size=17>Курица снесет яйцо через: {0}</size>" },
            {"AddInfoChicken Nofood", "<size=20>Курице нечего есть</size>" },
            {"AddInfoChicken EggsLimitCount", "<size=20>Курица снесла максимальное количество яиц</size>" },
            {"The chicken blew the egg", "<size=20>Курица снесла яйца</size>" },
            {"ChickenNotHitch", "<size=19>У курицы нету своей кормушки\nУстановите кормушку, чтобы она несла яйца</size>" },
            {"Permission", "У вас нет прав использовать данную команду!" },
            {"Invalid Command", "Вы не верно используете команду, используйте:\n/egg <normal:special> <count> - Чтобы выдать себе указанные яйца\n/egg <normal:special> <steamid:name> <count> - Чтобы выдать игроку указанные яйца" }
        };
		
		// hooks
		
		private void Init()
        {
            ins = this;
            LoadData();
			ChickenPlayers.Clear();
        }
						
		private void OnEntitySpawned(BaseCorpse entity) 
		{
			// отслеживаем спавн тел домашних куриц и добавляем их в исключения на фарм яиц
			if (entity != null && LastChickenKilledPos.ContainsKey(entity.transform.position) && LastChickenKilledPos[entity.transform.position] + 2f >= Time.realtimeSinceStartup)
			{
				var disp = entity.GetComponent<ResourceDispenser>();
				if (disp != null)
					ExcludeCorps.Add(disp);								
			}
		}
		
		private void OnEntitySpawned(Chicken chicken) 
		{
			if (init || chicken == null) return;
			
			ulong ownerID = chicken.OwnerID;			
			if (ownerID == 0)
			{
				foreach(var pair in PlayersEggs.Where(x=> x.Value != null && x.Value.Chickens != null))
				{
					if (pair.Value.Chickens.ContainsKey(chicken.net.ID))
					{
						ownerID = pair.Key;
						break;
					}
				}								
			}
			
			if (ownerID > 0)
			{		
				if (chicken.gameObject.GetComponent<Spawnable>() != null)
					UnityEngine.Object.DestroyImmediate(chicken.gameObject.GetComponent<Spawnable>());
								
				chicken.OwnerID = ownerID;
				chicken.EnableSaving(true);
			}
		}
		
		private void OnEntitySpawned(BuildingPrivlidge entity)
		{
			if (init == false || entity == null) return;			
			timer.Once(0.1f, ()=> CommunityEntity.ServerInstance.StartCoroutine(ReCheckBP()));
		}
		
		private bool? OnEntityKill(Chicken chicken)
		{
			if (!init) return false;			
			return null;
		}
		
		private void OnServerInitialized()
        {
            PrintWarning("\n-----------------------------\n" +
            "     Author - https://topplugin.ru/\n" +
            "     VK - https://vk.com/rustnastroika\n" +
            "     Discord - https://discord.com/invite/5DPTsRmd3G\n" +
            "-----------------------------");
			init = true;			
			
            lang.RegisterMessages(Messages, this, "en");
            Messages = lang.GetMessages("en", this);
			
            var Allchicken = BaseNetworkable.serverEntities.OfType<Chicken>().ToList();
            if (Allchicken != null)
                Allchicken.ToList().ForEach(chicken =>
                {
                    if (chicken.OwnerID != 0 && chicken.OwnerID.IsSteamId() && PlayersEggs.ContainsKey(chicken.OwnerID) 
							&& PlayersEggs[chicken.OwnerID].Chickens.ContainsKey(chicken.net.ID) && chicken.GetComponent<ChickenComponent>() == null)
					{
                        AddComponent(chicken, PlayersEggs[chicken.OwnerID].Chickens[chicken.net.ID]);
						chicken.EnableSaving(true);
					}
                });
            
            var allEggs = PlayersEggs.Values.Where(p => p.Eggs != null);
            foreach (var eggs in allEggs)
            {
                foreach (var egg in eggs.Eggs)
                {
                    var entity = BaseEntity.serverEntities.Find(egg.Key);
                    if (entity != null)
                    {
                        entity.gameObject.AddComponent<EggsConponent>();
                        entity.GetComponent<EggsConponent>().Init(egg.Value);
                    }
                }
            }
			
			var brokenBoxes = BaseNetworkable.serverEntities.OfType<StorageContainer>().Where(x=> x != null && x.PrefabName == "assets/content/vehicles/minicopter/subents/fuel_storage.prefab" && IsNearChicken(x, Allchicken)).ToList();
			foreach (var box in brokenBoxes)
				box?.Kill();
			
			RunLifeController(true);					
        }
		
		void Unload()
        {            		
            var Allchicken = BaseNetworkable.serverEntities.OfType<Chicken>();	 		
			
            if (Allchicken != null)
                Allchicken.ToList().ForEach(chicken =>
                {
                    if (chicken.GetComponent<ChickenComponent>() != null)
                    {
                        PlayersEggs[chicken.OwnerID].Chickens[chicken.net.ID] = chicken.GetComponent<ChickenComponent>().data;
                        if (PlayersEggs[chicken.OwnerID].Chickens[chicken.net.ID].nextEatTime < config.TimeToEggs)
                            PlayersEggs[chicken.OwnerID].Chickens[chicken.net.ID].NeedEat = false;
                        RemoveComponent(chicken, chicken.GetComponent<ChickenComponent>().box);
						chicken.EnableSaving(true);
                    }
                });
            var alleggs = GameObject.FindObjectsOfType<EggsConponent>();
            alleggs.ToList().ForEach(egg =>
            {
                if (egg.GetComponent<EggsConponent>() != null)
                    RemoveEggComponent(egg);
            });
            SaveData();
        }
		
		void OnServerSave() => SaveData();
		
		void OnNewSave()
        {
            PlayersEggs.Clear();
            PlayersEggs = new Dictionary<ulong, PlayersData>();
            SaveData();
            PrintWarning("Вайп куриц прошел успешно.");
        }

        object OnHammerHit(BasePlayer player, HitInfo info)
        {
            if (player == null || info == null) return null;
			if (!player.IsAdmin) return null;
            if (!CreateLoot.ContainsKey(player)) return null;
            var container = info.HitEntity.GetComponent<StorageContainer>();            
            if (container == null) return null;
            if (CreateLoot[player])
            {
                config.Normal.ItemsList.Clear();
                config.Special.ItemsList.Clear();

                var items = container.inventory.itemList;
                foreach (var item in items)
                {
                    config.Normal.ItemsList.Add(new EggItemSettings()
                    {
                        Change = 50,
                        IsBlueprnt = item.IsBlueprint(),
                        MaxAmount = item.amount,
                        MinAmount = item.amount == 1 ? item.amount : item.amount / 2,
                        Name = "",
                        ShortName = item.info.shortname,
                        SkinID = item.skin
                    });

                    config.Special.ItemsList.Add(new EggItemSettings()
                    {
                        Change = 50,
                        IsBlueprnt = item.IsBlueprint(),
                        MaxAmount = item.amount,
                        MinAmount = item.amount == 1 ? item.amount : item.amount / 2,
                        Name = "",
                        ShortName = item.info.shortname,
                        SkinID = item.skin
                    });
                }
                SendReply(player, $"Лут успешно обновлен. Добавлено новых предметов: {items.Count}");
            }
            else
            {
                var items = container.inventory.itemList;
                foreach (var item in items)
                {
                    config.Normal.ItemsList.Add(new EggItemSettings()
                    {
                        Change = 50,
                        IsBlueprnt = item.IsBlueprint(),
                        MaxAmount = item.amount,
                        MinAmount = item.amount == 1 ? item.amount : item.amount / 2,
                        Name = "",
                        ShortName = item.info.shortname,
                        SkinID = item.skin
                    });

                    config.Special.ItemsList.Add(new EggItemSettings()
                    {
                        Change = 50,
                        IsBlueprnt = item.IsBlueprint(),
                        MaxAmount = item.amount,
                        MinAmount = item.amount == 1 ? item.amount : item.amount / 2,
                        Name = "",
                        ShortName = item.info.shortname,
                        SkinID = item.skin
                    });
                }
                SendReply(player, $"Лут успешно обновлен. Добавлено новых предметов: {items.Count}");

            }
            SaveConfig();
            return null;
        }
		
		private static HashSet<ResourceDispenser> ExcludeCorps = new HashSet<ResourceDispenser>(); 
		private static HashSet<ResourceDispenser> ExcludeFarms = new HashSet<ResourceDispenser>();
		
		object OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (dispenser == null || item == null || player == null || ExcludeCorps.Contains(dispenser)) return null;            
			
            switch (item.info.shortname)
            {
                case "chicken.raw":
					if (!ExcludeFarms.Contains(dispenser))
					{
						ExcludeFarms.Add(dispenser);
					
						if (UnityEngine.Random.Range(0f, 100f) < config.ChangeGiveSpecialEgg)
						{
							AddEggs(player, 1, "special");
							return null;
						}
						if (UnityEngine.Random.Range(0f, 100f) < config.ChangeGiveNormalEgg)
						{
							AddEggs(player, 1, "normal");
							return null;
						}
					}

                    break;
            }
            return null;
        }
		
		private void OnActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)
        {
            if (player == null || newItem == null)
                return;

            if (newItem.info.itemid == -1002156085 && newItem.skin == config.Special.SkinEgg && !player.GetComponent<ItemPlacement>())
                player.GetOrAddComponent<ItemPlacement>();
        }

        object OnItemAction(Item item, string action, BasePlayer player)
        {			
            if (item == null || player == null || !player.userID.IsSteamId()) return null;
            if (action == "upgrade_item")
            {
                if (item.info.itemid == 844440409 && item.skin == config.Normal.SkinEgg)
                {
                    if (config.EnabledUpgradeEgg)
                    {
                        AddEggs(player, 1, "special");

                        if (item.amount > 10)
                        {
                            item.amount -= 10;
                            item.MarkDirty();
                        }
                        else item.RemoveFromContainer();
                    }
                    return true;
                }
                if (item.info.itemid == -1002156085 && item.skin == config.Special.SkinEgg) return false;
            }
            if (action == "unwrap")
            {
                if (item.info.itemid == -1002156085 && item.skin == config.Special.SkinEgg)
                {
                    int count = 0;
					
					if (item.text == player.UserIDString)				
						Farmer?.Call("API_AddPlayerScores", player.userID, "egg", 3);
					
                    if (config.Special.MaxCount == 1)
                    {
                        var itemConfig = config.Special.ItemsList.GetRandom();
                        var amount = UnityEngine.Random.Range(itemConfig.MinAmount, itemConfig.MaxAmount);
                        var newItem = itemConfig.IsBlueprnt ? ItemManager.CreateByName("blueprintbase") : ItemManager.CreateByName(itemConfig.ShortName, amount, itemConfig.SkinID);
                        if (newItem == null)
                        {
                            PrintError($"Предмет {itemConfig.ShortName} не найден!");
                            return false;
                        }

                        if (itemConfig.IsBlueprnt)
                        {
                            var bpItemDef = ItemManager.FindItemDefinition(ItemManager.CreateByName(itemConfig.ShortName, amount, itemConfig.SkinID).info.itemid);
                            if (bpItemDef == null)
                            {
                                PrintError($"Предмет {itemConfig.ShortName} для создания чертежа не найден!");
                                return false;
                            }

                            newItem.blueprintTarget = bpItemDef.itemid;
                        }

                        if (!string.IsNullOrEmpty(itemConfig.Name))
                            newItem.name = itemConfig.Name;
                        if (!player.inventory.containerMain.IsFull()) newItem.MoveToContainer(player.inventory.containerMain);
                        else player.GiveItem(newItem, BaseEntity.GiveItemReason.Generic);
                    }
                    else
                        for (int i = 0; i < config.Special.ItemsList.Count; i++)
                        {
                            if (count > config.Special.MaxCount) break;

                            var itemConfig = config.Special.ItemsList[i];
                            if (UnityEngine.Random.Range(0, 100) > itemConfig.Change) continue;
                            var amount = UnityEngine.Random.Range(itemConfig.MinAmount, itemConfig.MaxAmount);

                            var newItem = itemConfig.IsBlueprnt ? ItemManager.CreateByName("blueprintbase") : ItemManager.CreateByName(itemConfig.ShortName, amount, itemConfig.SkinID);
                            if (newItem == null)
                            {
                                PrintError($"Предмет {itemConfig.ShortName} не найден!");
                                continue;
                            }

                            if (itemConfig.IsBlueprnt)
                            {
                                var bpItemDef = ItemManager.FindItemDefinition(ItemManager.CreateByName(itemConfig.ShortName, amount, itemConfig.SkinID).info.itemid);
                                if (bpItemDef == null)
                                {
                                    PrintError($"Предмет {itemConfig.ShortName} для создания чертежа не найден!");
                                    continue;
                                }

                                newItem.blueprintTarget = bpItemDef.itemid;
                            }

                            if (!string.IsNullOrEmpty(itemConfig.Name))
                                newItem.name = itemConfig.Name;
                            if (!player.inventory.containerMain.IsFull()) newItem.MoveToContainer(player.inventory.containerMain);
                            else player.GiveItem(newItem, BaseEntity.GiveItemReason.Generic);
                            count++;

                        }

                    if (item.amount > 1)
                    {
                        item.amount--;
                        item.MarkDirty();
                    }
                    else item.RemoveFromContainer();
                    return true;
                }

                if (item.info.itemid == 844440409 && item.skin == config.Normal.SkinEgg)
                {
					
					if (item.text == player.UserIDString)				
						Farmer?.Call("API_AddPlayerScores", player.userID, "egg", 3);
					
                    int count = 0;
                    if (config.Normal.MaxCount == 1)
                    {
                        var itemConfig = config.Normal.ItemsList.GetRandom();
                        var amount = UnityEngine.Random.Range(itemConfig.MinAmount, itemConfig.MaxAmount);
                        var newItem = itemConfig.IsBlueprnt ? ItemManager.CreateByName("blueprintbase") : ItemManager.CreateByName(itemConfig.ShortName, amount, itemConfig.SkinID);
                        if (newItem == null)
                        {
                            PrintError($"Предмет {itemConfig.ShortName} не найден!");
                            return false;
                        }

                        if (itemConfig.IsBlueprnt)
                        {
                            var bpItemDef = ItemManager.FindItemDefinition(ItemManager.CreateByName(itemConfig.ShortName, amount, itemConfig.SkinID).info.itemid);
                            if (bpItemDef == null)
                            {
                                PrintError($"Предмет {itemConfig.ShortName} для создания чертежа не найден!");
                                return false;
                            }

                            newItem.blueprintTarget = bpItemDef.itemid;
                        }

                        if (!string.IsNullOrEmpty(itemConfig.Name))
                            newItem.name = itemConfig.Name;
                        if (!player.inventory.containerMain.IsFull()) newItem.MoveToContainer(player.inventory.containerMain);
                        else player.GiveItem(newItem, BaseEntity.GiveItemReason.Generic);
                    }
                    else
                        for (int i = 0; i < config.Normal.ItemsList.Count; i++)
                        {
                            if (count >= config.Normal.MaxCount) break;
                            var itemConfig = config.Normal.ItemsList[i];
                            if (UnityEngine.Random.Range(0, 100) > itemConfig.Change) continue;

                            var amount = UnityEngine.Random.Range(itemConfig.MinAmount, itemConfig.MaxAmount);
                            var newItem = itemConfig.IsBlueprnt ? ItemManager.CreateByName("blueprintbase") : ItemManager.CreateByName(itemConfig.ShortName, amount, itemConfig.SkinID);
                            if (newItem == null)
                            {
                                PrintError($"Предмет {itemConfig.ShortName} не найден!");
                                continue;
                            }

                            if (itemConfig.IsBlueprnt)
                            {
                                var bpItemDef = ItemManager.FindItemDefinition(ItemManager.CreateByName(itemConfig.ShortName, amount, itemConfig.SkinID).info.itemid);
                                if (bpItemDef == null)
                                {
                                    PrintError($"Предмет {itemConfig.ShortName} для создания чертежа не найден!");
                                    continue;
                                }

                                newItem.blueprintTarget = bpItemDef.itemid;
                            }

                            if (!string.IsNullOrEmpty(itemConfig.Name))
                                newItem.name = itemConfig.Name;
                            count++;
                            if (!player.inventory.containerMain.IsFull()) newItem.MoveToContainer(player.inventory.containerMain);
                            else player.GiveItem(newItem, BaseEntity.GiveItemReason.Generic);
                        }

                    if (item.amount > 1)
                    {
                        item.amount--;
                        item.MarkDirty();
                    }
                    else item.RemoveFromContainer();
                    return true;
                }
            }
            return null;
        }              

        private object CanLootEntity(BasePlayer player, StorageContainer container)
        {
            if (player == null || container == null) return null;

			if (LockOtherLooting.Contains(player.userID))
			{				
				LockOtherLooting.Remove(player.userID);
				if (container.skinID != 999)
					return false;
			}
			
            if (config.EnabledAuthCupboardInLoot && container.skinID == 999)
            {
				var comp = container.GetParentEntity()?.GetComponent<ChickenComponent>();				
				if (comp == null) return false;
				
				if (IsAuthed(player, comp.GetBP()))
					return null;
				                
                return false;
            }
            return null;
        }
		
		private static Dictionary<Vector3, float> LastChickenKilledPos = new Dictionary<Vector3, float>();

        private void OnEntityDeath(Chicken entity, HitInfo info)
        {
            if (entity == null || !config.EnabledDropEggs) return;

            try
            {
                if (entity.GetComponent<ChickenComponent>() != null && entity.GetComponent<ChickenComponent>().box != null)
                {
                    var container = entity.GetComponent<ChickenComponent>().box.GetComponent<StorageContainer>();
                    if (container != null)
                    {
                        DropUtil.DropItems(container.inventory, entity.transform.position, container.dropChance);
                        EffectNetwork.Send(new Effect("assets/bundled/prefabs/fx/item_break.prefab", entity, 0, Vector3.up, Vector3.zero)
                        {
                            scale = UnityEngine.Random.Range(0f, 1f)
                        });
                    }

                    if (PlayersEggs.ContainsKey(entity.OwnerID) && PlayersEggs[entity.OwnerID].Chickens.ContainsKey(entity.net.ID))
                        PlayersEggs[entity.OwnerID].Chickens.Remove(entity.net.ID);                    
										
					if (!LastChickenKilledPos.ContainsKey(entity.CenterPoint()))
						LastChickenKilledPos.Add(entity.CenterPoint(), Time.realtimeSinceStartup);
					else
						LastChickenKilledPos[entity.CenterPoint()] = Time.realtimeSinceStartup;										
                }                
            }
            catch (NullReferenceException){}
        }
		
		/*object OnItemSplit(Item item, int split_Amount)
        {
            if (item.info.itemid == -1002156085 && item.skin == config.Special.SkinEgg || item.info.itemid == 844440409 && item.skin == config.Normal.SkinEgg)
            {
                Item byItemId = ItemManager.CreateByItemID(item.info.itemid, 1, item.skin);
                item.amount -= split_Amount;
                byItemId.amount = split_Amount;
                byItemId.name = item.name;
				byItemId.text = item.text;
                item.MarkDirty();
                return byItemId;
            }
            return null;
        }

        object CanCombineDroppedItem(DroppedItem drItem, DroppedItem anotherDrItem)
        {
            if (drItem.item.info.itemid == -1002156085 && (drItem.item.skin != anotherDrItem.item.skin || !IsTextEqual(drItem.item.text, anotherDrItem.item.text))) return false;
            if (drItem.item.info.itemid == 844440409 && (drItem.item.skin != anotherDrItem.item.skin || !IsTextEqual(drItem.item.text, anotherDrItem.item.text))) return false;
			if (drItem.item.info.itemid == 844440409 && drItem.item.skin > 0 && drItem.item.amount + anotherDrItem.item.amount > 9) return false; // для исключения конфликта с пасхальными яйцами
			
            return null;
        }

        object CanStackItem(Item item, Item anotherItem)
        {
            if (item.info.itemid == -1002156085 && (item.skin != anotherItem.skin || !IsTextEqual(item.text, anotherItem.text))) return false;
            if (item.info.itemid == 844440409 && (item.skin != anotherItem.skin || !IsTextEqual(item.text, anotherItem.text))) return false;			
			if (item.info.itemid == 844440409 && item.skin > 0 && item.amount + anotherItem.amount > 9) return false; // для исключения конфликта с пасхальными яйцами
			
            return null;
        }*/
		
		private bool? OnNpcTarget(object obj, Chicken chicken)
		{
			if (chicken == null || chicken.OwnerID == 0) return null;
			return false;
		}								
		
		private void OnPlayerInput(BasePlayer player, InputState input)
        {
			if (player == null || input == null || !input.WasJustPressed(BUTTON.USE)) return;
			
			var chickens = new List<BaseNpc>();
			
			foreach (var pair in ChickenPlayers)
			{
				if (pair.Key == null) continue;
				if (pair.Value.Contains(player))
				{
					if (!chickens.Contains(pair.Key))
						chickens.Add(pair.Key);
				}
			}
			
			if (chickens.Count() == 0) return;
			
			var ray = new Ray(player.eyes.position, Quaternion.Euler(input.current.aimAngles) * Vector3.forward);
			
            var chicken = FindChicken(ray, 2.5f);
			if (chicken == null) 
			{
				LockOtherLooting.Remove(player.userID);
				return;
			}
			
			if (chickens.Contains(chicken))
			{
				var component = chicken.GetComponent<ChickenComponent>();
				if (component == null) return;
				
				component.DoOpenContainer(player);
			}            
        }
		
		// helpers
		
		private BaseNpc FindChicken(Ray ray, float distance) 
        {            
			var hits = UnityEngine.Physics.RaycastAll(ray, distance);
            BaseEntity closest = null;
			
            foreach (var hit in hits)
            {				
                BaseEntity ent = hit.GetEntity();				
				if (!(hit.collider is CapsuleCollider) && hit.collider?.ToString().Contains("Chicken Collider") == true) continue;
				if (hit.collider?.name?.Contains("preventBuilding") == true) continue;
				
				//Puts(hit.collider?.name?.ToString() + " " + ent?.ToString());
				
                if (ent != null && hit.distance < distance)
                {					
					if (ent.ShortPrefabName == "player" || ent.ShortPrefabName == "generic_world" || ent.ShortPrefabName == "autoturret_deployed") continue;
                    closest = ent;
                    distance = hit.distance;					
                }
            }
			
			return closest as BaseNpc;
        }
		
		private static bool IsTextEqual(string var1, string var2)
		{
			if (string.IsNullOrEmpty(var1) && string.IsNullOrEmpty(var2))
				return true;
			
			if (string.IsNullOrEmpty(var1) || string.IsNullOrEmpty(var2))
				return false;
			
			return var1 == var2;
		}				
		
		private static bool IsNearChicken(StorageContainer cont, List<Chicken> chickens)
		{
			if (cont == null || chickens == null || chickens.Count == 0) return false;
			
			foreach (var chicken in chickens)
			{
				if (chicken == null) continue;
				
				if ((chicken.transform.position - cont.transform.position).sqrMagnitude <= 1f)
					return true;
			}
				
			return false;	
		}
		
		private IEnumerator ReCheckBP()
		{
			var eggs = GameObject.FindObjectsOfType<EggsConponent>().ToList();
			//Puts("яиц: "+ eggs.Count()); 
			
			foreach (var egg in eggs)
			{
				if (egg == null) continue;
				egg?.TryUpdateBP();
				yield return new WaitForSeconds(0.015f);
			}
				
			var chickens = GameObject.FindObjectsOfType<ChickenComponent>().ToList();
			//Puts("куриц: "+ chickens.Count());
				
			foreach (var chicken in chickens)
			{
				if (chicken == null) continue;
				chicken?.TryUpdateBP();	
				yield return new WaitForSeconds(0.015f);
			}
		}
		
		private static bool IsAuthed(BasePlayer player, BuildingPrivlidge bp)
		{
			if (player == null || bp == null) return false;
			
			foreach(var player_ in bp.authorizedPlayers)				
				if (player_.userid == player.userID)
					return true;
				
			return false;	
		}
		
		private static float GetGroundPosition(Vector3 pos)
        {            
            RaycastHit hitInfo;
            if (Physics.Raycast(pos+new Vector3(0, 0.2f, 0), Vector3.down, out hitInfo, 100f, LayerMask.GetMask(new[] { "Terrain", "World", "Default", "Construction", "Deployed" })))
                return hitInfo.point.y;
            
			return pos.y;
        }
		
		/*private static float GetGroundPosition(Vector3 pos)
        {
            float y = 0;
            RaycastHit hitInfo;
            if (Physics.Raycast(pos+new Vector3(0, 0.2f, 0), Vector3.down, out hitInfo, 10f, Layers.Solid))
            {
                y = hitInfo.point.y;
            }

            RaycastHit hit;
            if (Physics.Raycast(new Vector3(pos.x, y, pos.z), Vector3.down, out hit, Mathf.Infinity, LayerMask.GetMask(new[] {
                "Terrain", "World", "Default", "Construction", "Deployed"
            }
            )) && !hit.collider.name.Contains("rock_cliff")) return Mathf.Max(hit.point.y, y);

            return y;
        }*/
		
		public static string FormatShortTime(TimeSpan time) 
		{
			string result = string.Empty;
			result += $"{time.Hours.ToString("00")}:";
			result += $"{time.Minutes.ToString("00")}:";
			result += $"{time.Seconds.ToString("00")}";
			return result;
		}				

		private static string FormatTime(int units, string form1, string form2, string form3)
		{
			var tmp = units % 10;

			if (tmp == 0 || units >= 5 && units <= 20 || tmp >= 5 && tmp <= 9)
				return $"{units} {form1}";

			if (tmp >= 2 && tmp <= 4)
				return $"{units} {form2}";

			return $"{units} {form3}";
		}
		
		// commands        

        [ChatCommand("chikenloot")]
        void cmdChickenLoot(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;
            if (CreateLoot.ContainsKey(player))
                CreateLoot.Remove(player);
            if (args.Length < 1) return;

            switch (args[0])
            {
                case "create":
                    CreateLoot.Add(player, true);
                    SendReply(player, "Режим создания лута включен, ударьте по ящику с лутом чтобы скопировать его.");
                    break;
                case "update":
                    CreateLoot.Add(player, false);
                    SendReply(player, "Режим обновления лута включен, ударьте по ящику чтобы добавить лут с ящика к существующему");
                    break;
            }
        }
		
		[ChatCommand("egg")]
        void GiveSeed(BasePlayer player, string command, string[] args)
        {
            if (player.IsAdmin)
            {
                var type = args[0];

                if (type != "normal" && type != "special")
                {
                    SendReply(player, Messages["Invalid Command"]);
                    return;
                }
                if (args.Length == 2)
                {
                    int amount;
                    if (!int.TryParse(args[1], out amount))
                    {
                        SendReply(player, "Вы не указали количество, используйте /seed AMOUNT");

                        return;
                    }
                    AddEggs(player, amount, type);
                    return;
                }
                if (args.Length > 0 && args.Length == 3)
                {
                    var target = BasePlayer.Find(args[1]);
                    if (target == null)
                    {
                        SendReply(player, "Данный игрок не найден, попробуйте уточнить имя или SteamID, используйте /seed TARGETNAME/ID AMOUNT");
                        return;
                    }

                    int amount;
                    if (!int.TryParse(args[2], out amount))
                    {
                        SendReply(player, "Вы не указали количество, используйте /seed TARGETNAME/ID AMOUNT");
                        return;
                    }

                    AddEggs(target, amount, type);
                }

            }
            else
            {
                SendReply(player, string.Format(Messages["Permission"]));
            }
        }
		
		[ConsoleCommand("cae_count")]
        private void cmdChickensRestore2(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null) return;
			
			var chickens = BaseNetworkable.serverEntities.OfType<Chicken>().Where(x=> x != null && x.OwnerID > 0).ToList();			
			
			var result = new List<PlayeChickens>();
			
			foreach(var pair in PlayersEggs.Where(x=> x.Value != null && x.Value.Chickens != null).ToDictionary(x=> x.Key, x=> x.Value))
			{
				foreach(var chickenInfo in pair.Value.Chickens.ToDictionary(x=> x.Key, x=> x.Value))
				{
					var next = false;
					foreach(var chicken in chickens)
					{					
						if (chicken.net.ID == chickenInfo.Key)
						{
							next = true;
							break;
						}
					}
					if (next) continue;
					
					var nearbyTargets = Pool.GetList<HitchTrough>();
					Vis.Entities<HitchTrough>(chickenInfo.Value.position, 1.5f, nearbyTargets);			
					var result2 = nearbyTargets.Count > 0;
					Pool.FreeList<HitchTrough>(ref nearbyTargets);			
					if (!result2) continue;
					
					result.Add(chickenInfo.Value);
				}
			}
			
			var exclude = new List<PlayeChickens>();
			for (int ii = 0; ii < result.Count; ii++)
			{
				for (int jj = ii+1; jj < result.Count; jj++)
				{
					if (Vector3.Distance(result[ii].position, result[jj].position) < 0.75f)					
						exclude.Add(result[jj]);					
				}
			}
			
			result = result.Where(x=> !exclude.Contains(x)).ToList();
			
			PrintWarning($"К восстановлению {result.Count} куриц.");
        }
		
		private class Ggg
		{
			public PlayeChickens c;
			public ulong u;
		}
		
		[ConsoleCommand("cae_restore")]
        private void cmdChickensRestore(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null) return;
            
			var chickens = BaseNetworkable.serverEntities.OfType<Chicken>().Where(x=> x != null && x.OwnerID > 0).ToList();			
			
			var result = new List<Ggg>();
			
			foreach(var pair in PlayersEggs.Where(x=> x.Value != null && x.Value.Chickens != null).ToDictionary(x=> x.Key, x=> x.Value))
			{
				foreach(var chickenInfo in pair.Value.Chickens.ToDictionary(x=> x.Key, x=> x.Value))
				{
					var next = false;
					foreach(var chicken in chickens)
					{					
						if (chicken.net.ID == chickenInfo.Key)
						{
							next = true;
							break;
						}
					}
					if (next) continue;
					
					var nearbyTargets = Pool.GetList<HitchTrough>();
					Vis.Entities<HitchTrough>(chickenInfo.Value.position, 1.5f, nearbyTargets);			
					var result2 = nearbyTargets.Count > 0;
					Pool.FreeList<HitchTrough>(ref nearbyTargets);			
					if (!result2) continue;
					
					result.Add(new Ggg() { c = chickenInfo.Value, u = pair.Key });
				}
			}
			
			var exclude = new List<PlayeChickens>();
			for (int ii = 0; ii < result.Count(); ii++)
			{
				for (int jj = ii+1; jj < result.Count(); jj++)
				{
					if (Vector3.Distance(result[ii].c.position, result[jj].c.position) < 0.75f)					
						exclude.Add(result[jj].c);					
				}
			}
			
			result = result.Where(x=> !exclude.Contains(x.c)).ToList();
			int cnt = 0;
			
			foreach (var n in result)
			{
				//(PlayersEggs[pair.Value]).Chickens.Remove(pair.Key);
				CreateChicken(n.c.position, n.u, 0);
				cnt++;
			}
			
			PrintWarning($"Восстановлено {cnt} куриц игроков.");
        }
		
		[ConsoleCommand("findeggs")]
        void cmdEggsList(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null) return;
			
			var AllWorldEggs = GameObject.FindObjectsOfType<WorldItem>().Where(x=> x != null && x.skinID == 1849161832 && x.GetComponent<EggsConponent>() == null);
			
			if (AllWorldEggs.Count() > 0)
				PrintWarning($"Найдено {AllWorldEggs.Count()} яиц без контроллера");
			else
				PrintWarning("Все яйца в порядке");		
		}	
		
		// main		        

		private void RunLifeController(bool skip)
		{
			if (!skip && config.ChickenLifeHours > 0)
			{				
				var chickens = BaseNetworkable.serverEntities.OfType<Chicken>().Where(x=> x != null && x.OwnerID > 0).ToList();
				
				foreach (var chicken in chickens)
				{
					var damage = chicken.MaxHealth()/config.ChickenLifeHours;
					
					if (chicken.OwnerID.IsSteamId() && PlayersEggs.ContainsKey(chicken.OwnerID) && PlayersEggs[chicken.OwnerID].Chickens.ContainsKey(chicken.net.ID) && chicken.GetComponent<ChickenComponent>() != null)										
						chicken.Hurt(damage, DamageType.Generic, chicken, false);											
				}
			}
			
			timer.Once(3600f, ()=> RunLifeController(false));
		}
		
		private static string GetChickenLifePass(BaseNpc chicken)
		{
			if (chicken == null) return "";			
			var damagePerHour = chicken.MaxHealth()/ins.config.ChickenLifeHours;			
			var passHours = (int)Math.Ceiling(chicken.health/damagePerHour);
			
			var result = "курице осталось жить: ";
			
			if (passHours <= 1)
				return result + "менее часа";
			
			return result + FormatTime(passHours, "часов", "часа", "час");
		}
		
        void AddComponent(Chicken Chicken, PlayeChickens chicken)
        {
            if (Chicken == null || chicken == null) return;
            Chicken.gameObject.AddComponent<ChickenComponent>();
			RemoveAI(Chicken);
            Chicken.GetComponent<ChickenComponent>().Init(chicken, false);
        }   
		void RemoveAI(BaseNpc Chicken){	
			Chicken.CancelInvoke(Chicken.TickAi);
            var script1 = Chicken.GetComponent<AiManagedAgent>();
            UnityEngine.Object.Destroy(script1);
            var script2 = Chicken.GetComponent<UtilityAIComponent>();
            UnityEngine.Object.Destroy(script2);
			
            var obj = Chicken as BaseAnimalNPC;
            if (obj != null)
            {
                AIThinkManager.RemoveAnimal(obj);
            }
		}

        void AddEggs(BasePlayer player, int amount, string type)
        {
            if (player == null) return;

            switch (type)
            {
                case "normal":
                    Item egg = ItemManager.CreateByItemID(844440409, amount, config.Normal.SkinEgg);
                    egg.name = config.Normal.Name;
                    player.GiveItem(egg, BaseEntity.GiveItemReason.PickedUp);
                    break;
                case "special":
                    egg = ItemManager.CreateByItemID(-1002156085, amount, config.Special.SkinEgg);
                    egg.name = config.Special.Name;
                    player.GiveItem(egg, BaseEntity.GiveItemReason.PickedUp);
                    break;
            }
        }
		
		private DroppedItem SpawnDroppedItem(BasePlayer player, string name, Vector3 position, bool canPickup = true)
        {
            BaseEntity worldEntity = CreateWorldObject(name, position, canPickup);
            UnityEngine.Object.Destroy(worldEntity.GetComponent<Rigidbody>());
            UnityEngine.Object.Destroy(worldEntity.GetComponent<EntityCollisionMessage>());
            UnityEngine.Object.Destroy(worldEntity.GetComponent<PhysicsEffects>());
            DroppedItem droppedItem = worldEntity.GetComponent<DroppedItem>();
            droppedItem.CancelInvoke(droppedItem.IdleDestroy);
            return droppedItem;
        }

        private BaseEntity CreateWorldObject(string name, Vector3 pos, bool canPickup)
        {
            Item item = ItemManager.CreateByItemID(-1002156085);
            item.name = config.Special.Name;
            BaseEntity worldEntity = GameManager.server.CreateEntity("assets/prefabs/misc/burlap sack/generic_world.prefab", pos);
            WorldItem worldItem = worldEntity as WorldItem;
            if (worldItem != null)
                worldItem.InitializeItem(item);
            worldItem.skinID = config.Special.SkinEgg;
            worldItem.allowPickup = false;
            worldItem.enableSaving = true;
            worldEntity.Spawn();
            item.SetWorldEntity(worldEntity);
            worldEntity.SendNetworkUpdateImmediate();
            return worldEntity;
        }        

        private void InitializeEgg(BasePlayer player, string name, Vector3 localPosition, Vector3 localRotation, HitchTrough hitch)
        {
            DroppedItem droppedItem = SpawnDroppedItem(player, name, localPosition);
            localPosition.y = GetGroundPosition(localPosition) + 0.0f;
            droppedItem.transform.localPosition = localPosition + (droppedItem.transform.up * 0.1f);
            droppedItem.transform.rotation = Quaternion.Euler(localRotation);
            droppedItem.OwnerID = player.userID;
            if (!PlayersEggs.ContainsKey(player.userID))
            {
                PlayersEggs.Add(player.userID, new PlayersData());
            }

            /*if (player.IsAdmin)
            {
                CreateChicken(droppedItem.transform.position, player.userID, droppedItem.net.ID);
                droppedItem.Kill();
            }
            else*/
            {
                PlayersEggs[player.userID].Eggs.Add(droppedItem.net.ID, new PlayerEggs() { nextEatTime = config.TimeToChicken, position = droppedItem.transform.position });
                droppedItem.GetOrAddComponent<EggsConponent>()?.Init(PlayersEggs[player.userID].Eggs[droppedItem.net.ID]);
            }
        }
		
		private BaseEntity InstantiateEntity(string type, Vector3 position, Quaternion rotation)
        {
            var gameObject = Instantiate.GameObject(GameManager.server.FindPrefab(type), position, rotation);
            gameObject.name = type;

            SceneManager.MoveGameObjectToScene(gameObject, Rust.Server.EntityScene);

            UnityEngine.Object.Destroy(gameObject.GetComponent<Spawnable>());

            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            BaseEntity component = gameObject.GetComponent<BaseEntity>();
            return component;
        }
		
        public void CreateChicken(Vector3 transform, ulong player, uint oldKey)
        {
            BaseEntity entity = InstantiateEntity("assets/rust.ai/agents/chicken/chicken.prefab", transform, new Quaternion());			
			entity.Spawn();			
			var npc = entity as BaseNpc;
			RemoveAI(npc);
            npc.OwnerID = player;
            PlayersEggs[player].Eggs.Remove(oldKey);
            PlayersEggs[player].Chickens.Add(npc.net.ID, new PlayeChickens() { nextEatTime = config.TimeToEggs, position = transform });
            npc.GetOrAddComponent<ChickenComponent>()?.Init(PlayersEggs[player].Chickens[npc.net.ID], true);
            npc.SendNetworkUpdateImmediate();
			npc.EnableSaving(true);
        }
		
		void RemoveEggComponent(EggsConponent eggs)
        {
            if (eggs == null) return;
            if (PlayersEggs.ContainsKey(eggs.egg.OwnerID) && PlayersEggs[eggs.egg.OwnerID].Eggs.ContainsKey(eggs.egg.net.ID))
                eggs.OnDestroyComponent();
        }

        void RemoveComponent(Chicken chicken, StorageContainer box)
        {
            if (box == null || chicken == null) return;
            if (PlayersEggs.ContainsKey(chicken.OwnerID) && PlayersEggs[chicken.OwnerID].Chickens.ContainsKey(chicken.net.ID))
            {
                var data = PlayersEggs[chicken.OwnerID].Chickens[chicken.net.ID];

                if (box.inventory.itemList.Count > 0)
                {
                    int amountNormal = 0;
                    int specialAmount = 0;
                    foreach (var eggs in box.inventory.itemList)
                    {
                        if (eggs.info.shortname == "easter.bronzeegg")
                            amountNormal = amountNormal + eggs.amount;
                        if (eggs.info.shortname == "easter.goldegg")
                            specialAmount = specialAmount + eggs.amount;
                    }
                    data.EggsCount = amountNormal;
                    data.SpecialEggs = specialAmount;
                }
                chicken.GetComponent<ChickenComponent>().DestroyComponent();
            }
        }

        // classes

        class ItemPlacement : MonoBehaviour
        {
            private BasePlayer player;
            private DroppedItem droppedItem;
            private bool isValidPlacement;
            private float placementDistance = 2f;
            private void Awake()
            {
                player = GetComponent<BasePlayer>();
                SpawnDroppedItem(player.GetActiveItem()?.name);
                player.SendConsoleCommand("gametip.showgametip", Messages["InfoText"]);
                ins.timer.Once(5f, () => player?.SendConsoleCommand("gametip.hidegametip"));
            }

            private void FixedUpdate()
            {
                Item activeItem = player.GetActiveItem();
                if (activeItem == null || activeItem.info.itemid != -1002156085)
                    CancelPlacement();

                isValidPlacement = false;

                InputState input = player.serverInput;
                Vector3 eyePosition = player.transform.position + (Vector3.up * 1.9f);

                RaycastHit hit;
                if (Physics.Raycast(new Ray(player.transform.position + (Vector3.up * 1.9f), Quaternion.Euler(input.current.aimAngles) * Vector3.forward), out hit, placementDistance, layerPlcmnt))
                {
                    droppedItem.transform.position = hit.point + (-droppedItem.transform.up * 0.1f);
                }
                else
                {
                    droppedItem.transform.position = new Ray(eyePosition, Quaternion.Euler(input.current.aimAngles) * Vector3.forward).GetPoint(1.85f);
                }

                if (input.WasJustPressed(BUTTON.FIRE_PRIMARY))
                {
                    List<HitchTrough> list = new List<HitchTrough>();
                    Vis.Entities<HitchTrough>(droppedItem.transform.position, 2f, list, LayerMask.GetMask(new string[] { "Deployed" }));

                    if (list.Count > 0) isValidPlacement = true;
                    if (!isValidPlacement)
                    {
                        player.ChatMessage(Messages["Error Place"]);
                        return;
                    }
                    else
                    {
                        List<Chicken> chickenList = new List<Chicken>();
                        Vis.Entities(list.First().transform.position, 2f, chickenList, -1, QueryTriggerInteraction.Ignore);
                        if (chickenList.Count >= ins.config.MaxChickenCount)
                        {
                            player.ChatMessage(Messages["Error Place Chicken Count"]);
                            return;
                        }

                        List<DroppedItem> eggsList = new List<DroppedItem>();
                        Vis.Entities(list.First().transform.position, 2f, eggsList, -1, QueryTriggerInteraction.Ignore);
                        eggsList.RemoveAll(p => p.GetComponent<EggsConponent>() == null || p.IsDestroyed);
                        if (chickenList.Count >= ins.config.MaxChickenCount)
                        {
                            player.ChatMessage(Messages["Error Place Egg Count"]);
                            return;
                        }
                        if (chickenList.Count + eggsList.Count >= ins.config.MaxChickenCount)
                        {
                            player.ChatMessage(Messages["Error Place Eggs end Chicken Count"]);
                            return;
                        }
                        PlaceEgg(activeItem, list.First());
                    }
                }
                else if (input.WasJustPressed(BUTTON.FIRE_SECONDARY))
                    CancelPlacement();
            }

            private void SpawnDroppedItem(string name)
            {
                droppedItem = ins.SpawnDroppedItem(player, name, player.transform.position, false);
            }

            public void CancelPlacement()
            {
                droppedItem.DestroyItem();
                droppedItem.Kill();
                Destroy(this);
            }

            private void PlaceEgg(Item activeItem, HitchTrough hitch)
            {

                if (activeItem.amount == 1)
                    activeItem.RemoveFromContainer();
                else
                {
                    activeItem.amount--;
                    activeItem.MarkDirty();
                }
                activeItem.MarkDirty();
                ins.InitializeEgg(player, droppedItem.item.name, droppedItem.transform.position, droppedItem.transform.eulerAngles, hitch);
                droppedItem.DestroyItem();
                droppedItem.Kill();
                Destroy(this);
            }

            public void OnPlayerDeath()
            {
                CancelPlacement();
                Destroy(this);
            }
        }

        class EggsConponent : BaseEntity
        {
            public BaseEntity egg;
            PlayerEggs data;
            public List<BasePlayer> ColliderPlayersList = new List<BasePlayer>();
            private SphereCollider sphereCollider;
			private BuildingPrivlidge bp;

            private void Awake()
            {
                egg = GetComponent<BaseEntity>();
                sphereCollider = gameObject.AddComponent<SphereCollider>();
                sphereCollider.gameObject.layer = (int)Rust.Layer.Reserved1;
                sphereCollider.isTrigger = true;
                sphereCollider.radius = 3f;
                InvokeRepeating(UpdateInfo, 1f, 1f);
				if (egg is WorldItem)
				{
					var item = egg as WorldItem;
					item.allowPickup = false;
					item.enableSaving = true;
					UnityEngine.Object.Destroy(item.GetComponent<Rigidbody>());
					UnityEngine.Object.Destroy(item.GetComponent<EntityCollisionMessage>());
					UnityEngine.Object.Destroy(item.GetComponent<PhysicsEffects>());
					DroppedItem droppedItem = item.GetComponent<DroppedItem>();
					droppedItem.CancelInvoke(droppedItem.IdleDestroy);
					bp = egg.GetBuildingPrivilege();
				}				
            }
			
			public void TryUpdateBP()
			{
				if (bp == null && egg != null)
					bp = egg.GetBuildingPrivilege();
			}

            private void OnTriggerEnter(Collider other)
            {
                var target = other.GetComponentInParent<BasePlayer>();
				if (target == null) return;								
				
                if (!ColliderPlayersList.Contains(target))
                    ColliderPlayersList.Add(target);
            }

            private void OnTriggerExit(Collider other)
            {
                var target = other.GetComponentInParent<BasePlayer>();
                if (target == null) return;
                
				ColliderPlayersList.Remove(target);
            }

            public bool ContainsAny(string value, params string[] args) => args.Any(value.Contains);            

            public void Init(PlayerEggs playerEggs)
            {
                data = playerEggs;
            }

			public HitchTrough GetHitchTrough(Vector3 position) 
            {
                List<HitchTrough> list = new List<HitchTrough>();
                Vis.Entities<HitchTrough>(position, 2f, list, LayerMask.GetMask(new string[] { "Deployed" }));
                if (list.Count > 0)
                    return list.First();
				
				return null;
            }
			
            void UpdateInfo()
            {
                if (data == null || egg == null) return; 
                data.nextEatTime--;
				if (bp != null)
				{
					foreach (var player in ColliderPlayersList)
					{
						if (player == null || !player.IsConnected || player.IsSleeping() || player.IsDead()) continue;
						var isAdmin = player.IsAdmin;	
						if (Vector3.Distance(egg.transform.position, player.transform.position) <= 2)
						{
							if (ins.config.EnabledAuthCupboard && !IsAuthed(player, bp)) continue;	
							if (!isAdmin) SetPlayerFlag(player, BasePlayer.PlayerFlags.IsAdmin, true);
							player.SendConsoleCommand("ddraw.text", 1.01f, Color.white, new Vector3(egg.transform.position.x, egg.transform.position.y + 0.5f, egg.transform.position.z), string.Format(Messages["EggsInfoText"], FormatShortTime(TimeSpan.FromSeconds(data.nextEatTime))));
						}
						if (!isAdmin) SetPlayerFlag(player, BasePlayer.PlayerFlags.IsAdmin, false);
					}
				}
                if (data.nextEatTime <= 0)
                {
                    data.nextEatTime = ins.config.TimeToEggs;
					if (GetHitchTrough(data.position) != null)
						ins.CreateChicken(data.position, egg.OwnerID, egg.net.ID);					
                    egg.Kill();
                }
            }

            void SetPlayerFlag(BasePlayer player, BasePlayer.PlayerFlags f, bool b)
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
                player.SendNetworkUpdateImmediate(false);
            }

            void OnDestroy() => Destroy(this);

            public void OnDestroyComponent() => OnDestroy();            
        }

        class ChickenComponent : BaseEntity
        {
            BaseNpc entity;
            public PlayeChickens data;
            public List<BasePlayer> ColliderPlayersList = new List<BasePlayer>();
            SphereCollider sphereCollider;
            public StorageContainer box;			
			private BuildingPrivlidge bp;
			private float lastCheckTime, CheckTime;
			
            private void Awake()
            {
				CheckTime = Rnd.Next(8, 20)/10f;
                entity = GetComponent<BaseNpc>();
                entity.GetComponent<Chicken>().AttackRange = 0;				
                sphereCollider = gameObject.AddComponent<SphereCollider>();
                sphereCollider.gameObject.layer = (int)Rust.Layer.Reserved1;
				sphereCollider.name = "Chicken Collider";
                sphereCollider.isTrigger = true;
                sphereCollider.radius = 1.2f;
                InvokeRepeating(UpdateInfo, 1f, 1f);
				bp = entity.GetBuildingPrivilege();
            }
			
			public void TryUpdateBP()
			{
				if (bp == null && entity != null)
					bp = entity.GetBuildingPrivilege();
			}
			
			public BuildingPrivlidge GetBP() => bp;

			public void DoOpenContainer(BasePlayer player)
			{
				if (box == null || player == null || !IsAuthed(player, bp)) return;
				
				if (!LockOtherLooting.Contains(player.userID))
					LockOtherLooting.Add(player.userID);								
				
				box.SetFlag(BaseEntity.Flags.Open, true, false);
				player.inventory.loot.StartLootingEntity(box, false);
				player.inventory.loot.AddContainer(box.inventory);
				player.inventory.loot.SendImmediate();				
				player.ClientRPCPlayer(null, player, "RPC_OpenLootPanel", box.panelName);				
				box.SendNetworkUpdate();
			}
			
            private void OnTriggerEnter(Collider other)
            {				
                var target = other.GetComponentInParent<BasePlayer>();				
				if (target == null) return;
									
                if (!ColliderPlayersList.Contains(target))				
					ColliderPlayersList.Add(target);										
				
				if (entity != null)
				{
					if (!ChickenPlayers.ContainsKey(entity))
						ChickenPlayers.Add(entity, new List<BasePlayer>());
					
					if (!ChickenPlayers[entity].Contains(target))
						ChickenPlayers[entity].Add(target);
				}
				
				if (box != null)
					box.SendNetworkUpdateImmediate();
            }

            private void OnTriggerExit(Collider other)
            {
                var target = other.GetComponentInParent<BasePlayer>();				
				if (target == null) return;					
				                
                ColliderPlayersList.Remove(target);
				
				if (entity != null)
				{
					if (ChickenPlayers.ContainsKey(entity) && ChickenPlayers[entity].Contains(target))					
						ChickenPlayers[entity].Remove(target);
				}
            }

            public bool ContainsAny(string value, params string[] args) => args.Any(value.Contains);

            void CreateBox(bool NewBox)
            {
                var box = GameManager.server.CreateEntity("assets/content/vehicles/boats/rowboat/subents/rowboat_storage.prefab", transform.position, transform.rotation) as StorageContainer;
                box.Spawn();
                UnityEngine.Object.Destroy(box.GetComponent<DestroyOnGroundMissing>());
                UnityEngine.Object.Destroy(box.GetComponent<GroundWatch>());
                box.enableSaving = false;
                this.box = box;
                box.SetParent(entity, false, true); 
                box.transform.localPosition = new Vector3(0f, -500f, 0f);
                box.transform.rotation = entity.transform.rotation;
                box.panelName = "crate";
                box._maxHealth = 10000;
                box.health = 10000;
				box.inventory.capacity = 6;
                box.inventory.SetFlag(ItemContainer.Flag.NoItemInput, true);
				box.inventory.onlyAllowedItems = null;
				box.skinID = 999; 
                if (!NewBox)
                {
                    data.ReplaceEggs(box, data.SpecialEggs, data.EggsCount, entity == null ? 0 : entity.OwnerID);
                    data.SpecialEggs = 0;
                    data.EggsCount = 0;
                }
				box.SendNetworkUpdateImmediate(); 
            }

            public void Init(PlayeChickens playerEggs, bool New)
            {
                data = playerEggs;
                CreateBox(New);
				
				entity.CancelInvoke(entity.TickAi);
                entity.CancelInvoke(entity.TickStuck);
                entity.CancelInvoke(entity.TickNavigation);
                entity.CancelInvoke(entity.TickNavigationWater);
				
                data.HitchTrough();
                if (data.hitch == null)
                {
					ins.PlayersEggs[entity.OwnerID].Chickens.Remove(entity.net.ID);
                    entity.Die();                    
                }                
            }
			
            void UpdateInfo()
            {							
				if (data == null || entity == null) return;				
				if (box == null) CreateBox(true);
				
				if (!data.NeedEat)
					data.nextEatTime--;
				
				if (data.hitch == null)
				{				
					if (bp != null)
					{				
						foreach (var player in ColliderPlayersList.ToList())
						{					
							if (entity == null || player == null || !player.IsConnected || player.IsSleeping() || player.IsDead()) continue;												
							var isAdmin = player.IsAdmin;													
							if (Vector3.Distance(entity.transform.position, player.transform.position) <= 2)
							{								
								if (ins.config.EnabledAuthCupboard && !IsAuthed(player, bp)) continue;															
								var messages = $"{string.Format(Messages["ChickenInfoText"], Messages["ChickenNotHitch"])}";								
								if (!isAdmin) SetPlayerFlag(player, BasePlayer.PlayerFlags.IsAdmin, true);								
								player.SendConsoleCommand("ddraw.text", 1.01f, Color.white, new Vector3(entity.transform.position.x, entity.transform.position.y + 0.5f, entity.transform.position.z), messages);
							}
							if (!isAdmin) SetPlayerFlag(player, BasePlayer.PlayerFlags.IsAdmin, false);							
						}						
					}					
					data.HitchTrough();					
					return;
				}
				
				if (box == null || box.inventory == null) return;				
				var countSpecial = box.inventory.GetAmount(-1002156085, true);				
				var countNormal = box.inventory.GetAmount(844440409, true);				
				var amount = countSpecial + countNormal;				
				
				if (ins.config.DisableCreateEggs && amount >= ins.config.EggsCount)
				{				
					if (bp != null)
					{				
						foreach (var player in ColliderPlayersList.ToList())
						{					
							if (box == null || entity == null || player == null || !player.IsConnected || player.IsSleeping() || player.IsDead()) continue;							
							var isAdmin = player.IsAdmin;
							
							if (Vector3.Distance(entity.transform.position, player.transform.position) <= 2)
							{							
								if (ins.config.EnabledAuthCupboard && !IsAuthed(player, bp)) continue;																
								var messages = $"{string.Format(Messages["ChickenInfoText"], Messages["AddInfoChicken EggsLimitCount"])}";
								
								if (box.inventory.itemList.Count > 0)
								{
									if (ins.config.DisableCreateEggs)
									{								
										if (amount < ins.config.EggsCount)																			
											messages = messages + $"\n<b>{Messages["The chicken blew the egg"]}</b>";										
									}
									else																			
										messages = messages + $"\n<b>{Messages["The chicken blew the egg"]}</b>";									
								}								
								if (!isAdmin) SetPlayerFlag(player, BasePlayer.PlayerFlags.IsAdmin, true);									
								player.SendConsoleCommand("ddraw.text", 1.01f, Color.white, new Vector3(entity.transform.position.x, entity.transform.position.y + 0.5f, entity.transform.position.z), messages);								
							}
							if (!isAdmin) SetPlayerFlag(player, BasePlayer.PlayerFlags.IsAdmin, false);							
						}						
					}					
					return;
				}
				
				if (entity != null && data.NeedEat)
				{
					if (data.ChickenEat())
					{				
						entity.SetFact(BaseNpc.Facts.IsEating, 3, true, true);					
						data.nextEatTime = ins.config.TimeToEggs;						
						data.NeedEat = false;						
					}
					else											
						data.NeedEat = true;					
				}
				
				if (bp != null)
				{
					foreach (var player in ColliderPlayersList.ToList())
					{				
						if (box == null || entity == null || player == null || !player.IsConnected || player.IsSleeping() || player.IsDead()) continue;					
						var isAdmin = player.IsAdmin;
						
						if (Vector3.Distance(entity.transform.position, player.transform.position) <= 2)
						{						
							if (ins.config.EnabledAuthCupboard && !IsAuthed(player, bp)) continue;		                     							          					
							var add = "";								
							if (ins.config.ChickenLifeHours != 999)															
								add = "\n"+GetChickenLifePass(entity);							
														
							var messages = $"{string.Format(Messages["ChickenInfoText"], !data.NeedEat ? string.Format(Messages["AddInfoChicken"], FormatShortTime(TimeSpan.FromSeconds(data.nextEatTime))) : Messages["AddInfoChicken Nofood"])}" + add;

							if (box.inventory.itemList.Count > 0)
							{
								if (ins.config.DisableCreateEggs)
								{							
									if (amount < ins.config.EggsCount)																	
										messages = messages + $"\n<b>{Messages["The chicken blew the egg"]}</b>";																		
								}
								else																	
									messages = messages + $"\n<b>{Messages["The chicken blew the egg"]}</b>";																	
							}
							if (!isAdmin) SetPlayerFlag(player, BasePlayer.PlayerFlags.IsAdmin, true);								
							player.SendConsoleCommand("ddraw.text", 1.01f, Color.white, new Vector3(entity.transform.position.x, entity.transform.position.y + 0.5f, entity.transform.position.z), messages);							
						}						
						if (!isAdmin) SetPlayerFlag(player, BasePlayer.PlayerFlags.IsAdmin, false);						
					}					
				}
				
				if (data.nextEatTime <= 0 && box != null)
				{
					data.nextEatTime = ins.config.TimeToEggs;				
					data.AddBoxEggs(box, entity == null ? 0 : entity.OwnerID);					
					data.NeedEat = true;					
				}								
            }

            void FixedUpdate()
            {
                if (data == null || entity == null) return;
				
				if ((Time.realtimeSinceStartup - lastCheckTime) >= CheckTime)
				{							
					var pos = GetGroundPosition(data.position);
					if (Math.Abs(data.position.y - pos) > 0.01f)											
						data.position.y = pos;
					
					if (entity.transform.position != data.position)
						entity.transform.position = data.position;
					
					lastCheckTime = Time.realtimeSinceStartup;		
				}
            }

            void SetPlayerFlag(BasePlayer player, BasePlayer.PlayerFlags f, bool b)
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
                player.SendNetworkUpdateImmediate(false);
            }

            private void OnDestroy() 
			{
				if (box != null && !box.IsDestroyed)
					box.Kill();								
				
				Destroy(sphereCollider);
				Destroy(this);
			}

            public void DestroyComponent() => OnDestroy();            
        }
                
    }
}