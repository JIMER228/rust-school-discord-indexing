using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Oxide.Plugins {
    [Info("QuarryPro", "Egor Blagov", "1.2.4")]
    [Description("Replaces some of survey crates with oil crate")]
    class QuarryPro : RustPlugin {
        const string perm = "quarrypro.adm";
        const string permInfo = "quarrypro.info";
        const string permCraftAll = "quarrypro.craft.all";
        const string permCraftQuarry = "quarrypro.craft.quarry";
        const string permCraftPumpjack = "quarrypro.craft.pumpjack";
        const string permCraftFormat = "quarrypro.craft.{0}";
        const string oilCraterPrefab = "assets/prefabs/tools/surveycharge/survey_crater_oil.prefab";
        const string standardCraterPrefab = "assets/prefabs/tools/surveycharge/survey_crater.prefab";
        const string pumpjackPrefab = "mining.pumpjack";
        const float quarryWork = 15;
        const float liquidWork = 10;

        static QuarryPro Instance;

        class PluginConfig {
            public enum QuarryType {
                Quarry,
                Pumpjack
            };

            public class ChargeResource {
                public string Shortname;
                public float ResourcePerMinuteMin;
                public float ResourcePerMinuteMax;
                public float SpawnChance;

                public float GetWorkNeeded(QuarryType type) {
                    return GenerateWorkNeeded(type, this.ResourcePerMinuteMin, this.ResourcePerMinuteMax);
                }

                public string ToDescription(string userIdString) {
                    var msg = string.Format(Instance.msg("Description", userIdString),
                        $"<color=#ff6b0f>{ItemManager.FindItemDefinition(this.Shortname).displayName.english}</color>",
                        this.SpawnChance * 100,
                        this.ResourcePerMinuteMin,
                        this.ResourcePerMinuteMax
                    );
                    return $"<size=15>{msg}</size>";
                }
            }

            public class Recipe {
                public RecipeIngredient[] Ingredients = new RecipeIngredient[] {
                    new RecipeIngredient {
                        shortname="scrap",
                        amount=10
                    },
                    new RecipeIngredient {
                        shortname="grenade.beancan",
                        amount=1
                    },
                    new RecipeIngredient {
                        shortname="metal.fragments",
                        amount=120
                    }
                };
                public uint CraftAmount = 10;

                public string GetDescription(string userIdString) {
                    var result = new StringBuilder($"<color=#6bbcff>{string.Format(Instance.msg("CraftAmount", userIdString), CraftAmount)}</color>");
                    result.AppendLine();
                    foreach (var i in Ingredients) {
                        result.AppendLine(i.GetDescription(userIdString));
                    }
                    return result.ToString();
                }

                public string GetNotMeetItems(BasePlayer player) {
                    var result = new List<string>();
                    foreach( var i in Ingredients) {
                        if (!i.Meets(player)) {
                            result.Add(Instance.msg(ItemManager.FindItemDefinition(i.shortname).displayName.english, player.UserIDString));
                        }
                    }
                    return string.Join(", ", result);
                }

                public bool Meets(BasePlayer player) {
                    foreach (var i in Ingredients) {
                        if (!i.Meets(player)) {
                            return false;
                        }
                    }
                    return true;
                }

                public void Take(BasePlayer player) {
                    foreach (var i in Ingredients) {
                        i.Take(player);
                    }
                }
            }

            public class RecipeIngredient {
                public string shortname;
                public int amount;

                public string GetDescription(string userIdString) => $"{amount} {Instance.msg(ItemManager.FindItemDefinition(shortname).displayName.english, userIdString)}";

                public bool Meets(BasePlayer player) {
                    int itemId = ItemManager.FindItemDefinition(shortname).itemid;
                    if (shortname != "surveycharge") {
                        return player.inventory.GetAmount(itemId) >= amount;
                    }

                    int count = 0;
                    foreach (var item in player.inventory.FindItemIDs(itemId)) {
                        if (item.skin == 0) {
                            count += item.amount;
                        }
                    }
                    return count >= amount;
                }
                class ItemTaken {
                    public Item item;
                    public ItemContainer container;

                    public void Hide() {
                        item.RemoveFromContainer();
                    }

                    public void Unhide() {
                        item.MoveToContainer(container);
                    }
                }

                public void Take(BasePlayer player) {
                    var itemId = ItemManager.FindItemDefinition(shortname).itemid;
                    
                    var toIgnore = Facepunch.Pool.GetList<ItemTaken>();
                    if (shortname == "surveycharge") {
                        foreach (var currentContainer in new List<ItemContainer> {player.inventory.containerBelt,
                            player.inventory.containerMain, player.inventory.containerWear}) {

                            var allItems = currentContainer.itemList;
                            for (int i = allItems.Count - 1; i >= 0; i--) {
                                if (allItems[i].info.shortname == "surveycharge" && allItems[i].skin != 0) {
                                    toIgnore.Add(new ItemTaken {
                                        item = allItems[i],
                                        container = currentContainer
                                    });
                                }
                            }
                        }
                        foreach (var ignored in toIgnore) {
                            ignored.Hide();
                        }
                    }

                    player.inventory.Take(null, itemId, amount);
                    player.Command("note.inv", (object)itemId, (object)-amount);

                    if (shortname == "surveycharge") {
                        foreach (var ignored in toIgnore) {
                            ignored.Unhide();
                        }
                    }
                    Facepunch.Pool.FreeList(ref toIgnore);
                }
            }

            public class ChargeDescription {
                public Recipe Recipe = new Recipe();
                public ulong SkinId;
                public float CraterSpawnChance;
                public ChargeResource[] Resources;
                [JsonConverter(typeof(StringEnumConverter))]
                public QuarryType Type;
                public string NameLocalizedMessage;
                [JsonIgnore]
                public bool isLiquid {
                    get {
                        return Type == QuarryType.Pumpjack;
                    }
                }

                public string GetItemName(string userIDString) {
                    var name = Instance.msg(this.NameLocalizedMessage, userIDString);
                    if (!Instance.config.ShowChanceInChargeName) {
                        return name;
                    }

                    return name + string.Format(" {0:0}%", this.CraterSpawnChance * 100);
                }

                public string GetChargeDescription(string userIdString) {
                    var result = new StringBuilder($"{GetChargeName(userIdString)}\n");
                    foreach (var resource in this.Resources) {
                        result.AppendLine(resource.ToDescription(userIdString));
                    }

                    return result.ToString();
                }

                public string GetChargeName(string userIdString) => $"<color=#6bbcff><size=18>{GetItemName(userIdString)}</size></color>";
            }

            public float DefaultCrudeOilSpawnChance = 0.3f;
            public float DefaultCrudeOilRatePerMinuteMin = 1.0f;
            public float DefaultCrudeOilRatePerMinuteMax = 10.0f;
            public bool ShowChanceInChargeName = true;
            public Recipe QuarryRecipe = new Recipe {
                CraftAmount = 1
            };
            public Recipe PumpjackRecipe = new Recipe {
                CraftAmount = 1
            };
            public Dictionary<string, ChargeDescription> Charges = new Dictionary<string, ChargeDescription> {
                ["stone"] = new ChargeDescription {
                    SkinId = 1668263237,
                    CraterSpawnChance = 1.0f,
                    Resources = new ChargeResource[] {
                        new ChargeResource {
                            Shortname = "stones",
                            ResourcePerMinuteMax = 10,
                            ResourcePerMinuteMin = 5,
                            SpawnChance = 1.0f
                        }
                    },
                    Type = QuarryType.Quarry,
                    NameLocalizedMessage = "StonesCharge"
                },
                ["crudeoil"] = new ChargeDescription {
                    SkinId = 1668267050,
                    CraterSpawnChance = 1.0f,
                    Resources = new ChargeResource[] {
                        new ChargeResource {
                            Shortname = "crude.oil",
                            ResourcePerMinuteMax = 10,
                            ResourcePerMinuteMin = 5,
                            SpawnChance = 1.0f
                        }
                    },
                    Type = QuarryType.Pumpjack,
                    NameLocalizedMessage = "CrudeOilCharge"
                },
                ["metal"] = new ChargeDescription {
                    SkinId = 1668261972,
                    CraterSpawnChance = 1.0f,
                    Resources = new ChargeResource[] {
                        new ChargeResource {
                            Shortname = "metal.ore",
                            ResourcePerMinuteMax = 10,
                            ResourcePerMinuteMin = 5,
                            SpawnChance = 1.0f,
                        }
                    },
                    Type = QuarryType.Quarry,
                    NameLocalizedMessage = "MetalOreCharge"
                },
                ["hqmetal"] = new ChargeDescription {
                    SkinId = 1668259252,
                    CraterSpawnChance = 1.0f,
                    Resources = new ChargeResource[] {
                        new ChargeResource {
                            Shortname = "hq.metal.ore",
                            ResourcePerMinuteMax = 10,
                            ResourcePerMinuteMin = 5,
                            SpawnChance = 1.0f,
                        }
                    },
                    Type = QuarryType.Quarry,
                    NameLocalizedMessage = "HQMetalCharge"
                },
                ["sulfur"] = new ChargeDescription {
                    SkinId = 1667974140,
                    CraterSpawnChance = 1.0f,
                    Resources = new ChargeResource[] {
                        new ChargeResource {
                            Shortname = "sulfur.ore",
                            ResourcePerMinuteMax = 10,
                            ResourcePerMinuteMin = 5,
                            SpawnChance = 1.0f,
                        }
                    },
                    Type = QuarryType.Quarry,
                    NameLocalizedMessage = "SulfurOreCharge"
                },
                ["scrap"] = new ChargeDescription {
                    SkinId = 1667965490,
                    CraterSpawnChance = 1.0f,
                    Resources = new ChargeResource[] {
                        new ChargeResource {
                            Shortname = "scrap",
                            ResourcePerMinuteMax = 10,
                            ResourcePerMinuteMin = 5,
                            SpawnChance = 1.0f,
                        }
                    },
                    Type = QuarryType.Quarry,
                    NameLocalizedMessage = "ScrapCharge"
                },

                ["combo"] = new ChargeDescription {
                    SkinId = 1677537255,
                    CraterSpawnChance = 0.5f,
                    Resources = new ChargeResource[] {
                        new ChargeResource {
                            Shortname = "sulfur.ore",
                            ResourcePerMinuteMax = 10,
                            ResourcePerMinuteMin = 5,
                            SpawnChance = 0.5f
                        },
                        new ChargeResource {
                            Shortname = "metal.ore",
                            ResourcePerMinuteMax = 10,
                            ResourcePerMinuteMin = 5,
                            SpawnChance = 0.5f
                        },
                        new ChargeResource {
                            Shortname = "stones",
                            ResourcePerMinuteMax = 10,
                            ResourcePerMinuteMin = 5,
                            SpawnChance = 0.5f
                        }
                    },
                    NameLocalizedMessage = "ComboCharge",
                    Type = QuarryType.Quarry
                },

                ["component"] = new ChargeDescription {
                    SkinId = 1677904953,
                    CraterSpawnChance = 0.5f,
                    Resources = new ChargeResource[] {
                        new ChargeResource {
                            Shortname = "metalspring",
                            ResourcePerMinuteMax = 0.5f,
                            ResourcePerMinuteMin = 0.2f,
                            SpawnChance = 0.2f
                        },
                        new ChargeResource {
                            Shortname = "gears",
                            ResourcePerMinuteMax = 0.5f,
                            ResourcePerMinuteMin = 0.2f,
                            SpawnChance = 0.2f
                        },
                        new ChargeResource {
                            Shortname = "riflebody",
                            ResourcePerMinuteMax = 0.5f,
                            ResourcePerMinuteMin = 0.2f,
                            SpawnChance = 0.1f
                        },
                        new ChargeResource {
                            Shortname = "sheetmetal",
                            ResourcePerMinuteMax = 0.5f,
                            ResourcePerMinuteMin = 0.2f,
                            SpawnChance = 0.2f
                        },
                        new ChargeResource {
                            Shortname = "metalpipe",
                            ResourcePerMinuteMax = 0.5f,
                            ResourcePerMinuteMin = 0.2f,
                            SpawnChance = 0.2f
                        },
                        new ChargeResource {
                            Shortname = "smgbody",
                            ResourcePerMinuteMax = 0.5f,
                            ResourcePerMinuteMin = 0.2f,
                            SpawnChance = 0.1f
                        }
                    },
                    NameLocalizedMessage = "ComponentCharge",
                    Type = QuarryType.Quarry
                }
            };

            public List<string> GetCustomNames() {
                return Charges.Values.Select(ch => ch.NameLocalizedMessage).ToList();
            }

            public string GetChargeName(ulong skin) {
                foreach (var entry in this.Charges) {
                    if (entry.Value.SkinId == skin) {
                        return entry.Key;
                    }
                }

                return null;
            }

            public float GetDefaultCrudeOilWork() {
                return GenerateWorkNeeded(QuarryType.Pumpjack, DefaultCrudeOilRatePerMinuteMin, DefaultCrudeOilRatePerMinuteMax);
            }

            private static float GenerateWorkNeeded(QuarryType type, float minRatePerMinute, float maxRatePerMinute) {
                float rate = UnityEngine.Random.Range(minRatePerMinute, maxRatePerMinute);
                var res = GetWorkNeeded(type, rate);
                return res;
            }

            private static float GetWorkNeeded(QuarryType type, float resourcePerMinute) {
                float ws = (type == QuarryType.Quarry ? quarryWork : liquidWork ) / 15 * 10;
                return 60.0f * 7.5f / ws / resourcePerMinute; // Rust formula 4730.0f * 35.0f / 7.5f * 10.0f
            }
        }

        class StoredInfo {
            public class ResourceInfo {
                public Vector3 position;
                public string shortname;
                public float workNeeded;
                public bool isLiquid;
            }

            public List<ResourceInfo> resourcesSaved = new List<ResourceInfo>();
        }

        private PluginConfig config;
        private StoredInfo storedInfo;
        Dictionary<BaseEntity, string> enqueuedToRemove = new Dictionary<BaseEntity, string>();

        protected override void LoadDefaultConfig() {
            Config.WriteObject(new PluginConfig(), true);
        }

        protected override void LoadDefaultMessages() {
            // WARNING: Config loading is set here, since we have to get custom MSG names from config and register them

            config = Config.ReadObject<PluginConfig>();
            Config.WriteObject(config);

            var customNames = config.GetCustomNames();
            var enLang = new Dictionary<string, string> {
                ["StonesCharge"] = "Stones Charge",
                ["CrudeOilCharge"] = "Crude Oil Charge",
                ["MetalOreCharge"] = "Metal Ore Charge",
                ["HQMetalCharge"] = "HQ Metal Ore Charge",
                ["SulfurOreCharge"] = "Sulfur Ore Charge",
                ["ScrapCharge"] = "Scrap Charge",
                ["ComboCharge"] = "Multi Charge",
                ["ComponentCharge"] = "Components Charge",
                ["Description"] = "{0} - {1:0}% (speed {2}-{3} per minute)",
                ["TakeCharge"] = "You should take a PRO Survey Charge into hands, in order to use this command",
                ["NoPermission"] = "You have no permission to use this command",
                ["NoPermissionCraft"] = "You don't have any craft permissions",
                ["CraftAmount"] = "{0} items will be crafted",
                ["Available"] = "Available to craft:",
                ["Help"] = "Shows charge craft recipe",
                ["NotEnough"] = "Not enough items",
                ["InvalidIngredients"] = "Missing item names",
                ["Quarry"] = "Quarry",
                ["Pumpjack"] = "Pumpjack"
            };

            var ruLang = new Dictionary<string, string> {
                ["StonesCharge"] = "Заряд для камней",
                ["CrudeOilCharge"] = "Заряд для нефти",
                ["MetalOreCharge"] = "Заряд для железной руды",
                ["HQMetalCharge"] = "Заряд для МВК",
                ["SulfurOreCharge"] = "Заряд для серной руды",
                ["ScrapCharge"] = "Заряд для металлолома",
                ["ComboCharge"] = "Заряд для базовых ресурсов",
                ["ComponentCharge"] = "Заряд для компонентов",
                ["Description"] = "{0} - {1:0}% (скорость {2}-{3} ед/мин)",
                ["TakeCharge"] = "Вам нужно взять в руки ПРО геологический заряд, чтобы использовать эту команду",
                ["NoPermission"] = "У вас нет разрешения на использование этой команды",
                ["NoPermissionCraft"] = "У вас нет разрешения на крафт каких-либо зарядов",
                ["CraftAmount"] = "При крафте выдается {0}",
                ["Available"] = "Доступно для крафта:",
                ["Help"] = "Отображает рецепт для крафта",
                ["NotEnough"] = "Не хватает ресурсов",
                ["InvalidIngredients"] = "Недостающие ресурсы",
                ["Quarry"] = "Карьер",
                ["Pumpjack"] = "Нефтекачка"
            };

            foreach (var name in customNames) {
                if (!enLang.ContainsKey(name)) {
                    enLang[name] = name;
                }
                if (!ruLang.ContainsKey(name)) {
                    ruLang[name] = name;
                }
            }
            Action<PluginConfig.Recipe> registerIngridients = (PluginConfig.Recipe recipe) => {
                foreach (var item in recipe.Ingredients) {
                    var info = ItemManager.FindItemDefinition(item.shortname);
                    if (info != null) {
                        enLang[info.displayName.english] = info.displayName.english;
                        ruLang[info.displayName.english] = info.displayName.english;
                    } else {
                        PrintError($"Unknown shortname: {item.shortname}");
                    }
                }
            };

            foreach (var charge in config.Charges) {
                registerIngridients(charge.Value.Recipe);
            }
            registerIngridients(config.QuarryRecipe);
            registerIngridients(config.PumpjackRecipe);

            lang.RegisterMessages(enLang, this);
            lang.RegisterMessages(ruLang, this, "ru");
        }

        private string msg(string key, string userId) {
            return lang.GetMessage(key, this, userId);
        }

        private void Init() {
            Instance = this;
            permission.RegisterPermission(perm, this);
            permission.RegisterPermission(permInfo, this);
            permission.RegisterPermission(permCraftAll, this);
            permission.RegisterPermission(permCraftPumpjack, this);
            permission.RegisterPermission(permCraftQuarry, this);
            foreach (var name in config.Charges.Keys) {
                permission.RegisterPermission(string.Format(permCraftFormat, name), this);
            }

            storedInfo = Interface.Oxide.DataFileSystem.ReadObject<StoredInfo>(this.Name);
        }

        private void Unload() {
            SaveData();
        }

        private void SaveData() {
            if (this.storedInfo != null) {
                Interface.Oxide.DataFileSystem.WriteObject<StoredInfo>(this.Name, this.storedInfo, true);
            }
        }

        void OnExplosiveThrown(BasePlayer player, BaseEntity entity) {
            if (entity is SurveyCharge) {
                var chargeName = this.config.GetChargeName(player.GetHeldEntity().GetItem().skin);
                if (chargeName != null)
                    this.enqueuedToRemove[entity] = chargeName;
            }
        }

        bool locked = false;
        Item OnItemSplit(Item item, int amount) {
            if (locked) {
                return null;
            }

            if (item.info.shortname == "surveycharge" && this.config.GetChargeName(item.skin) != null) {
                locked = true;
                var newItem = item.SplitItem(amount);
                newItem.skin = item.skin;
                locked = false;
                return newItem;
            }

            return null;
        }

        object CanStackItem(Item item, Item targetItem) {
            if (item.info.shortname == "surveycharge" && targetItem.info.shortname == "surveycharge" &&
                (this.config.GetChargeName(item.skin) != null || this.config.GetChargeName(targetItem.skin) != null)) {
                return item.skin == targetItem.skin;
            }

            return null;
        }

        private void OnEntityKill(BaseEntity ent) {
            if (ent is SurveyCharge) {
                if (this.enqueuedToRemove.ContainsKey(ent)) {
                    this.handleCustom(ent);
                    this.enqueuedToRemove.Remove(ent);
                } else {
                    this.handleDefault(ent);
                }

                this.enqueuedToRemove.Keys.ToList().ForEach(key => {
                    if (key == null || !key.IsValid()) {
                        this.enqueuedToRemove.Remove(key);
                    }
                });
            }
        }

        private void handleDefault(BaseEntity ent) {
            RaycastHit hitOut;
            if (!TransformUtil.GetGroundInfo(ent.transform.position, out hitOut, 0.3f, (LayerMask)8388608, (Transform)null))
                return;

            Vector3 point = hitOut.point;
            Vector3 normal = hitOut.normal;

            List<SurveyCrater> list = Facepunch.Pool.GetList<SurveyCrater>();
            Vis.Entities(ent.transform.position, 10f, list, 1, QueryTriggerInteraction.Collide);
            bool anotherCrater = list.Count > 0;
            Facepunch.Pool.FreeList(ref list);
            if (anotherCrater)
                return;

            if (!getRandom(this.config.DefaultCrudeOilSpawnChance)) {
                return;
            }

            SpawnCrater(point, Quaternion.LookRotation(normal) * Quaternion.Euler(90, 0, 0), true);
            cleanupResourceAt(point);
            createResourceAt(point, "crude.oil", true, this.config.GetDefaultCrudeOilWork());
        }

        private void handleCustom(BaseEntity ent) {
            var chargeName = this.enqueuedToRemove[ent];
            var charge = this.config.Charges[chargeName];
           

            RaycastHit hitOut;
            if (!TransformUtil.GetGroundInfo(ent.transform.position, out hitOut, 0.3f, (LayerMask)8388608, (Transform)null))
                return;

            Vector3 point = hitOut.point;
            Vector3 normal = hitOut.normal;

            List<SurveyCrater> list = Facepunch.Pool.GetList<SurveyCrater>();
            Vis.Entities(ent.transform.position, 10f, list, 1, QueryTriggerInteraction.Collide);
            bool anotherCrater = list.Count > 0;
            Facepunch.Pool.FreeList(ref list);
            if (anotherCrater)
                return;


            var resourcesBackup = getResourceBackup(point);
            cleanupResourceAt(point);
            if (!getRandom(charge.CraterSpawnChance)) {
                NextTick(() => {
                    foreach (var resource in resourcesBackup) {
                        ResourceDepositManager.GetOrCreate(point)._resources.Add(resource);
                    }
                    Facepunch.Pool.FreeList(ref resourcesBackup);
                    resourcesBackup = null;
                });
                return;
            }
            bool spawnedResource = false;
            
            foreach (var resource in charge.Resources) {
                if (getRandom(resource.SpawnChance)) {
                    createResourceAt(point, resource.Shortname, charge.isLiquid, resource.GetWorkNeeded(charge.Type));
                    spawnedResource = true;
                }
            }

            if (spawnedResource) {
                SpawnCrater(point, Quaternion.LookRotation(normal) * Quaternion.Euler(90, 0, 0), charge.isLiquid);
            }
        }

        private void SpawnCrater(Vector3 position, Quaternion rotation, bool isLiquid) {
            var crater = GameManager.server.CreateEntity(isLiquid ? oilCraterPrefab : standardCraterPrefab, position, rotation, true) as SurveyCrater;
            crater.Spawn();
        }

        private void OnItemAddedToContainer(ItemContainer container, Item item) {
            if (container == null || item == null) {
                return;
            }

            if (container.playerOwner == null) {
                return;
            }

            this.AdjustSurveyChargeName(item, container.playerOwner);
        }

        private void AdjustSurveyChargeName(Item item, BasePlayer playerOwner) {
            if (item.info.shortname != "surveycharge" || item.skin == 0) {
                return;
            }
            string chargeName = this.config.GetChargeName(item.skin);
            if (chargeName == null) {
                return;
            }

            item.name = this.config.Charges[chargeName].GetItemName(playerOwner.UserIDString);
       }


        private void OnServerInitialized() {
            var quarries = UnityEngine.Object.FindObjectsOfType<MiningQuarry>();
            List<StoredInfo.ResourceInfo> resourcesFiltered = new List<StoredInfo.ResourceInfo>();
            foreach (var quarry in quarries) {
                var matched = this.storedInfo.resourcesSaved.Where(info => (info.position - quarry.transform.position).magnitude < 2);
                if (matched.Count() > 0) {
                    this.cleanupResourceAt(quarry.transform.position);
                    foreach (var resource in matched.ToList()) {
                        resourcesFiltered.Add(resource);
                        this.createResourceAt(
                            resource.position,
                            resource.shortname,
                            resource.isLiquid,
                            resource.workNeeded
                        );
                    }

                } else { // fall back for not handled quarries
                    if (quarry.canExtractLiquid) {
                        cleanupResourceAt(quarry.transform.position);
                        this.createResourceAt(quarry.transform.position, "crude.oil", true, this.config.GetDefaultCrudeOilWork());
                    } else {
                        ResourceDepositManager.ResourceDeposit resourceDeposit = ResourceDepositManager.GetOrCreate(quarry.transform.position);
                        if (resourceDeposit != null && resourceDeposit._resources.Count > 0) {
                            continue;
                        }
                        this.createResourceAt(quarry.transform.position, "stones", false, UnityEngine.Random.Range(0.3f, 0.5f)); // From default Rust spawn
                    }
                }
            }
            this.storedInfo.resourcesSaved = resourcesFiltered;

            foreach (var player in BasePlayer.activePlayerList) {
                foreach (var item in player.inventory.AllItems()) {
                    this.AdjustSurveyChargeName(item, player);
                }
            }
        }

        private void OnPlayerInit(BasePlayer player) {
            player.inventory.AllItems().ToList().ForEach(it => this.AdjustSurveyChargeName(it, player));
        }

        private void OnServerSave() {
            SaveData();
        }

        [ChatCommand("chargeinfo")]
        private void ChargeInfo(BasePlayer player, string cmd, string[] argv) {
            if (!permission.UserHasPermission(player.UserIDString, permInfo)) {
                SendReply(player, msg("NoPermission", player.UserIDString));
                return;
            }

            if (player.GetHeldEntity() != null) {
                var heldItem = player.GetHeldEntity().GetItem();
                if (heldItem.info.shortname == "surveycharge" && heldItem.skin != 0) {
                    var chargename = this.config.GetChargeName(heldItem.skin);
                    if (chargename != null) {
                        SendReply(player, this.config.Charges[chargename].GetChargeDescription(player.UserIDString));
                        return;
                    }
                }
            }

            SendReply(player, msg("TakeCharge", player.UserIDString));
        }

        class CraftEntry {
            public string permission;
            public string name;
            public string key;
            public PluginConfig.Recipe recipe;
            public string color;
            public string item;
            public ulong skinId = 0;
        }
        [ChatCommand("chargecraft")]
        private void ChargeCraft(BasePlayer player, string cmd, string[] argv) {
            bool hasAnyCraft = false;
            foreach (var perm in permission.GetUserPermissions(player.UserIDString)) {
                if (perm.StartsWith("quarrypro.craft")) {
                    hasAnyCraft = true;
                    break;
                }
            }

            if (!hasAnyCraft) {
                SendReply(player, msg("NoPermissionCraft", player.UserIDString));
                return;
            }

            List<CraftEntry> craftEntries = getCraftEntries(player);
            if (argv.Length == 0 || craftEntries.Where(x => x.key == argv[0]).Count() == 0 || argv.Length == 2 && argv[1] != "help") {
                var help = new StringBuilder(Instance.msg("Available", player.UserIDString));
                help.AppendLine();
                foreach (var entry in craftEntries) {
                    if (permission.UserHasPermission(player.UserIDString, permCraftAll) ||
                        permission.UserHasPermission(player.UserIDString, entry.permission)) {
                        help.AppendLine($"<color=#{entry.color}>/chargecraft {entry.key}</color> - {entry.name}");
                    }
                }
                help.AppendLine();
                help.AppendLine($"<color=#ff6b0f>/chargecraft <color=white><key></color> help</color> - {Instance.msg("Help", player.UserIDString)}");
                SendReply(player, help.ToString());
                return;
            }

            var entryToCraft = craftEntries.Where(x => x.key == argv[0]).First();
            var recipeToCraft = entryToCraft.recipe;
            bool hasItems = recipeToCraft.Meets(player);
            if (argv.Length == 2 && argv[1] == "help" || !hasItems) {
                var help = new StringBuilder();
                if (!(argv.Length == 2 && argv[1] == "help")) {
                    help.AppendLine($"<color=red>{Instance.msg("NotEnough", player.UserIDString)}</color>");
                    help.AppendLine($"{Instance.msg("InvalidIngredients", player.UserIDString)}: {recipeToCraft.GetNotMeetItems(player)}");
                }
                help.AppendLine(recipeToCraft.GetDescription(player.UserIDString));
                SendReply(player, help.ToString());
                return;
            }
            
            recipeToCraft.Take(player);
            var item = ItemManager.CreateByName(entryToCraft.item, (int) recipeToCraft.CraftAmount, entryToCraft.skinId);
            player.GiveItem(item, BaseEntity.GiveItemReason.Crafted);
        }

        private List<CraftEntry> getCraftEntries(BasePlayer player) {
            List<CraftEntry> result = new List<CraftEntry>();
            foreach (var entry in config.Charges) {
                result.Add(new CraftEntry {
                    name = entry.Value.GetItemName(player.UserIDString),
                    permission = string.Format(permCraftFormat, entry.Key),
                    key = entry.Key,
                    recipe = entry.Value.Recipe,
                    color = "ff6b0f",
                    item = "surveycharge",
                    skinId = entry.Value.SkinId
                });
            }
            result.Add(new CraftEntry {
                name = msg("Quarry", player.UserIDString),
                permission = permCraftQuarry,
                key = "quarry",
                recipe = config.QuarryRecipe,
                color = "6bff0f",
                item = "mining.quarry",
            });
            result.Add(new CraftEntry {
                name = msg("Pumpjack", player.UserIDString),
                permission = permCraftPumpjack,
                key = "pumpjack",
                recipe = config.PumpjackRecipe,
                color = "6bff0f",
                item = "mining.pumpjack"
            });
            return result;
        }

        [ConsoleCommand("chargecache")]
        private void ChargeCache(ConsoleSystem.Arg arg) {
            if (arg.Player() != null && !permission.UserHasPermission(arg.Player().UserIDString, perm)) {
                PrintToConsole(arg.Player(), "You have no permission to use this command");
                return;
            }

            arg.ReplyWith($"count is {this.enqueuedToRemove.Count}");
        }

        [ConsoleCommand("chargegive")]
        private void GiveCharge(ConsoleSystem.Arg arg) {
            if (arg.Player() != null && !permission.UserHasPermission(arg.Player().UserIDString, perm)) {
                PrintToConsole(arg.Player(), "You have no permission to use this command");
                return;
            }

            Action<string> printToConsole = (str) => {
                Puts(str);
                LogToFile("Shop", string.Format("{0}: {1}", DateTime.Now, str), this);
            };

            string args = arg.HasArgs() ? string.Join(", ", arg.Args.Select(s => s.ToString())) : "none";
            printToConsole($"trying for execute player: {arg.Player() != null} args: {args}");
            if (arg.Player() != null) {
                printToConsole = (str) => {
                    PrintToConsole(arg.Player(), str);
                    LogToFile("Shop", string.Format("{0}: {1}", DateTime.Now, str), this);
                };
            }

            if (!arg.HasArgs() || arg.Args.Length < 3) {
                printToConsole("Unable to give item, not enough args");
                throw new Exception("Not enough args");
            }

            if (!arg.Args[0].IsSteamId()) {
                printToConsole($"{arg.Args[0]} is not valid Steam ID");
                throw new Exception($"{arg.Args[0]} invalid Steam ID");
            }

            if (!this.config.Charges.ContainsKey(arg.Args[1])) {
                printToConsole($"charge with name {arg.Args[1]} doesn't exist");
                throw new Exception($"charge with name {arg.Args[1]} doesn't exist");
            }

            int amount;
            if (!int.TryParse(arg.Args[2], out amount)) {
                printToConsole($"{arg.Args[2]} is not valid amount");
                throw new Exception($"{arg.Args[2]} invalid amount");
            }

            BasePlayer target = BasePlayer.Find(arg.Args[0]);
            if (target == null) {
                printToConsole($"Warning: player with Steam ID {arg.Args[0]} was not found within active players");
                target = BasePlayer.FindSleeping(arg.Args[0]);
                if (target == null) {
                    printToConsole($"Error: plyaer with Steam ID {arg.Args[0]} was not found neither within active nor sleeping players");
                    throw new Exception($"Unable to find player with Steam ID {arg.Args[0]}");
                }
            }

            Item item = ItemManager.CreateByName("surveycharge", amount, this.config.Charges[arg.Args[1]].SkinId);
            target.GiveItem(item);
            printToConsole($"Success {arg.Args[0]} {arg.Args[1]} {arg.Args[2]}");
        }


        private void cleanupResourceAt(Vector3 position) {
            ResourceDepositManager.ResourceDeposit resourceDeposit = ResourceDepositManager.GetOrCreate(position);
            resourceDeposit._resources.Clear();
        }

        private List<ResourceDepositManager.ResourceDeposit.ResourceDepositEntry> getResourceBackup(Vector3 position) {
            ResourceDepositManager.ResourceDeposit resourceDeposit = ResourceDepositManager.GetOrCreate(position);
            var list = Facepunch.Pool.GetList<ResourceDepositManager.ResourceDeposit.ResourceDepositEntry>();
            foreach (var entry in resourceDeposit._resources) {
                list.Add(entry);
            }
            return list;
        }

        private void createResourceAt(Vector3 position, string shortname, bool isLiquid, float workNeeded) {
            ResourceDepositManager.ResourceDeposit resourceDeposit = ResourceDepositManager.GetOrCreate(position);

            if (resourceDeposit == null)
                return; // ! check if unable to create resource then don't spawn crater

            resourceDeposit._resources.Add(new ResourceDepositManager.ResourceDeposit.ResourceDepositEntry {
                type = ItemManager.FindItemDefinition(shortname),
                spawnType = isLiquid ? ResourceDepositManager.ResourceDeposit.surveySpawnType.OIL : ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM,
                isLiquid = isLiquid,
                workNeeded = workNeeded
            });

            this.storedInfo.resourcesSaved.Add(new StoredInfo.ResourceInfo {
                shortname = shortname,
                isLiquid = isLiquid,
                workNeeded = workNeeded,
                position = position
            });
        }

        private static bool getRandom(float probability = 0.5f) {
            return UnityEngine.Random.Range(0f, 1f) < probability;
        }
    }
}
