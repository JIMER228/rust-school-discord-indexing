using Newtonsoft.Json;
using System.Collections.Generic;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using System;
using Oxide.Core.Plugins;
using System.Collections;
using Oxide.Core;

namespace Oxide.Plugins 
{
    [Info("PersonalAnimal", "walkinrey", "1.3.7")]
    public class PersonalAnimal : RustPlugin 
    {
        public static PersonalAnimal Instance;

        [PluginReference] private Plugin ImageLibrary, PersonalNPC, Friends, Clans;

        private Dictionary<ulong, PlayerAnimalController> _existsControllers = new Dictionary<ulong, PlayerAnimalController>();
        private Dictionary<ulong, AnimalOwnerComponent> _existsOwnerComponents = new Dictionary<ulong, AnimalOwnerComponent>();

        private List<string> _permissionKeys = new List<string>();
        private Dictionary<ulong, DateTime> _cooldownInfo = new Dictionary<ulong, DateTime>();

        private BUTTON controlButton = BUTTON.FIRE_THIRD, inventoryButton = BUTTON.RELOAD, mountButton = BUTTON.USE;

        #region Config

        private Configuration _config;

        public class Configuration 
        {
            [JsonProperty("Controls setup")]
            public ControlsSetup controlsSetup = new ControlsSetup();
            
            [JsonProperty("GUI setup")]
            public GUISetup gui = new GUISetup();

            [JsonProperty("Protect from Rust anti-cheat kick when player riding an animal")]
            public ProtectInfo protect = new ProtectInfo();

            [JsonProperty("Spawn settings")]
            public SpawnInfo spawnInfo = new SpawnInfo();

            [JsonProperty("How frequent animal will update the information? (affects the performance and operation of the animal)")]
            public float mainProcessTimer = 0.01f;

            [JsonProperty("Setting up personal animals by permission")]
            public Dictionary<string, AnimalInfo> animalInfoPerm = new Dictionary<string, AnimalInfo>();

            [JsonProperty("Damage scale from animals to other entities")]
            public Dictionary<string, float> damageScale = new Dictionary<string, float>();

            [JsonProperty("Animal install by item")]
            public List<ItemInfo> installItem = new List<ItemInfo>();

            [JsonProperty("List of prefabs that the animal can loot (useful if the animal attacks loot instead of looting it)")]
            public List<string> lootEntities = new List<string>();

            public class SpawnInfo 
            {
                [JsonProperty("Can player spawn animal in cupboard range?")]
                public bool canSpawnInCupboard = true;

                [JsonProperty("Can player spawn animal on construction? (foundations, floors, walls, etc.)")]
                public bool canSpawnOnConstruction = false;

                [JsonProperty("Can player spawn animal on deployed entities? (wood boxes, tables, workbenches, etc.)")]
                public bool canSpawnOnDeployed = false;
            }

            public class ProtectInfo 
            {
                [JsonProperty("Kick if detected noclip?")]
                public bool noClip = true;
                
                [JsonProperty("Kick if detected speedhack?")]
                public bool speedHack = true;

                [JsonProperty("Kick if detected flyhack?")]
                public bool flyHack = true;

                [JsonProperty("Kick if detected insideterrain?")]
                public bool insideTerrain = true;
            }

            public struct ItemInfo 
            {
                [JsonProperty("Item name")]
                public string name;

                [JsonProperty("Item shortname")]
                public string shortname;

                [JsonProperty("Item skin")]
                public ulong skin;

                [JsonProperty("Return item back if player have despawned the animal via command or GUI?")]
                public bool returnDespawn;

                [JsonProperty("Bot info")]
                public AnimalInfo animal;
            }

            public class GUISetup 
            {
                [JsonProperty("How many seconds to update the GUI?")]
                public int refreshTime = 6;

                [JsonProperty("Panel layer (Hud, Overlay, Overall, Hud.Menu, Under)")]
                public string layer = "Overlay";

                [JsonProperty("Panel position")]
                public CuiRectTransformComponent panelPosition = new CuiRectTransformComponent();

                [JsonProperty("Second position of the panel (used if the player has a personal bot)")]
                public CuiRectTransformComponent secondPanelPosition = new CuiRectTransformComponent();

                [JsonProperty("1 panel color")]
                public string panelColor1 = "#7f8c8d";

                [JsonProperty("2 panel color")]
                public string panelColor2 = "#bdc3c7";

                [JsonProperty("Health bar color")]
                public string panelHealthColor = "#2ecc71";

                [JsonProperty("Shortcut buttons")]
                public List<AccessButton> accessButtons = new List<AccessButton>();

                public class AccessButton
                {
                    [JsonProperty("Text on button")]
                    public string text = "";

                    [JsonProperty("Executable chat commands")]
                    public string[] commands = new string[] {};

                    public AccessButton(string btnText, string[] btnCommand)
                    {
                        text = btnText;
                        commands = btnCommand;
                    }
                }
            }

            public class ControlsSetup 
            {
                [JsonProperty("Which button will assign tasks to the animal, attack / collect, etc. (MIDDLE_MOUSE, SECOND_MOUSE, E, RELOAD, SPRINT)")]
                public string key = "MIDDLE_MOUSE";

                [JsonProperty("Button to open animal inventory (MIDDLE_MOUSE, SECOND_MOUSE, E, RELOAD, SPRINT)")]
                public string inventoryKey = "RELOAD";

                [JsonProperty("Button to mount animal (MIDDLE_MOUSE, SECOND_MOUSE, E, RELOAD, SPRINT)")]
                public string mountKey = "E";

                [JsonProperty("Range of action of the assignment button")]
                public float rayLength = 25f;

                [JsonProperty("Input tick (affects performance and feedback of the inputs, 0.1 for best performance, 0.01 for best feedback)")]
                public float inputTick = 0.05f;

                [JsonProperty("Display 3D arrows over a target?")]
                public bool showArrow = true;

                [JsonProperty("Arrow display duration")]
                public int arrowDuration = 2;

                [JsonProperty("Distance between player and animal to move animal into idle state")]
                public float idleDistance = 1f;

                [JsonProperty("Control animal by mouse direction (true) of by WASD buttons (false) when mounted?")]
                public bool byDirection = true;
            }

            public class AnimalInfo 
            {
                [JsonProperty("The name of the animal to be selected through the command when spawning")]
                public string spawnName = "wolf1";

                [JsonProperty("Animal type (bear, boar, chicken, stag, wolf, polar-bear)")]
                public string type = "wolf";

                [JsonProperty("Animal speed (slowest, slow, normal, fast)")]
                public string speed = "normal";

                [JsonProperty("Maximum health")]
                public int maxHealth = 200;

                [JsonProperty("Animal spawn cooldown")]
                public float cooldown = 300f;

                [JsonProperty("AI Stopping Distance")]
                public float stoppingDistance = 0.1f;

                [JsonProperty("Addons setup")]
                public AddonsSetup addonsSetup = new AddonsSetup();

                [JsonProperty("Functions setup")]
                public FunctionsSetup functionsSetup = new FunctionsSetup();

                [JsonProperty("Damage, interactions and loot setup")]
                public InteractionSetup interactionSetup = new InteractionSetup();

                [JsonProperty("Death Marker (marker will be only visible for owner)")]
                public DeathMarkerSetup deathMarker = new DeathMarkerSetup();

                [JsonProperty("Nutrition setup")]
                public FoodSetup nutritionSetup = new FoodSetup();

                public class DeathMarkerSetup 
                {
                    [JsonProperty("Show marker on bot's death position?")]
                    public bool enableMarker = false;

                    [JsonProperty("Display name on map")]
                    public string displayName = "Bot's death marker";

                    [JsonProperty("Marker radius")]
                    public float radius = 0.35f;
                    
                    [JsonProperty("Outline color (hex)")]
                    public string outline = "00FFFFFF";

                    [JsonProperty("Main color (hex)")]
                    public string main = "00FFFF";

                    [JsonProperty("Alpha")]
                    public float alpha = 0.5f;

                    [JsonProperty("Duration")]
                    public int duration = 20;
                }

                public class FoodSetup 
                {
                    [JsonProperty("Turn on the animal feeding system?")]
                    public bool enableFeeding = true;

                    [JsonProperty("Setting health for food eaten")]
                    public Dictionary<string, float> foodInfo = new Dictionary<string, float>();
                }

                public class AddonsSetup 
                {
                    [JsonProperty("Enable the ability to ride an animal?")]
                    public bool needChair = false;

                    [JsonProperty(
                        "Dismount owner when entering RaidableBase?")]
                    public bool dismountOnRaidableBase = false;

                    [JsonProperty("Add a bag to an animal to store resources?")]
                    public bool needBag = true;

                    [JsonProperty("Number of available slots in the bag (maximum 36)")]
                    public int slotsAmount = 12;
                }

                public class InteractionSetup 
                {
                    [JsonProperty("Animal damage rate")]
                    public float botDamageRate = 2f;

                    [JsonProperty("Damage rate receive for an animal")]
                    public float botReceiveDamageRate = 1f;

                    [JsonProperty("Can the animal damage players?")]
                    public bool canDamagePlayers = true;

                    [JsonProperty("Can players damage the animal?")]
                    public bool canBeDamagedByPlayers = true;

                    [JsonProperty("Despawn animal corpse after death?")]
                    public bool despawnCorpse = false;
                    
                    [JsonProperty("Distance between animal and loot entity (collectible resources, loot boxes etc.)")] 
                    public float collectibleDistance = 3f;

                    [JsonProperty("Distance between animal and enemy")]
                    public float enemyDistance = 5f;

                    [JsonProperty("Setting up resource pickup rates")]
                    public Dictionary<string, float> collectRates = new Dictionary<string, float>();

                    [JsonProperty("Setting up loot rates")]
                    public Dictionary<string, float> lootRates = new Dictionary<string, float>();

                    [JsonProperty("Black list of items that cannot be put in the bag")]
                    public List<string> bagBlacklist = new List<string>();
                }

                public class FunctionsSetup 
                {
                    [JsonProperty("Can the animal attack objects?")]
                    public bool canAttackEntities = true;

                    [JsonProperty("Can the animal attack players?")]
                    public bool canAttackPlayers = true;

                    [JsonProperty("Can the animal attack NPCs?")]
                    public bool canAttackNPC = true;

                    [JsonProperty("Can an animal loot boxes?")]
                    public bool canLootBoxes = true;

                    [JsonProperty("Can the animal pick up resources?")]
                    public bool canCollectResources = true;

                    [JsonProperty("Does the animal have to defend itself?")]
                    public bool canProtectSelf = true;

                    [JsonProperty("Should the animal protect the owner?")]
                    public bool canProtectOwner = true;

                    [JsonProperty("Can an animal collect resources within a specified radius? (/panimal auto-collect)")]
                    public bool canAutoCollect = false;

                    [JsonProperty("Collect resources radius (/panimal auto-collect)")]
                    public float collectRadius = 50f;

                    [JsonProperty("Only owner can open animal's bag after death?")]
                    public bool onlyOwnerLootDeadBag = true;

                    [JsonProperty("Can attack team/clan members or friends?")]
                    public bool canAttackTeam = true;
                }

                public string TypeToEntity()
                {
                    switch(type)
                    {
                        case "bear": return "assets/rust.ai/agents/bear/bear.prefab";
                        case "boar": return "assets/rust.ai/agents/boar/boar.prefab";
                        case "chicken": return "assets/rust.ai/agents/chicken/chicken.prefab";
                        case "stag": return "assets/rust.ai/agents/stag/stag.prefab";
                        case "wolf": return "assets/rust.ai/agents/wolf/wolf.prefab";
                        case "polar-bear": return "assets/rust.ai/agents/bear/polarbear.prefab";

                        default: return string.Empty;
                    }
                }

                public Vector3 GetChairOffset()
                {
                    switch(type)
                    {
                        case "bear": return new Vector3(0, 0.65f, 0);
                        case "boar": return new Vector3(0, 0.35f, 0);
                        case "stag": return new Vector3(0, 0.5f, 0);
                        case "polar-bear": return new Vector3(0, 0.7f, 0);

                        default: return Vector3.zero;
                    }
                }

                public Vector3 GetFeedingOffset()
                {
                    switch(type)
                    {
                        case "wolf": return new Vector3(0, 0.15f, 0.9f);
                        case "bear": return new Vector3(0, 0.15f, 1.25f);
                        case "boar": return new Vector3(0, 0.15f, 1.1f);
                        case "stag": return new Vector3(0, 0.15f, 1f);
                        case "polar-bear": return new Vector3(0, 0.15f, 1.3f);
                        case "chicken": return new Vector3(0, 0.2f, 0.5f);
                        
                        default: return Vector3.zero;
                    }
                }
            }
        }

        protected override void LoadDefaultConfig()
        {
            _config = new Configuration();

            _config.gui.panelPosition = new CuiRectTransformComponent
            {
                AnchorMin = "1 1", AnchorMax = "1 1",
                OffsetMin = "-170 -104", OffsetMax = "-10 -10"
            };

            _config.gui.secondPanelPosition = new CuiRectTransformComponent
            {
                AnchorMin = "1 1", AnchorMax = "1 1",
                OffsetMin = "-360 -104", OffsetMax = "-200 -10"
            };

            _config.damageScale.Add("bradleyapc", 0f);
            _config.damageScale.Add("bradley", 0f);

            _config.animalInfoPerm.Add("personalanimal.wolf", new Configuration.AnimalInfo
            {
                interactionSetup = new Configuration.AnimalInfo.InteractionSetup
                {
                    collectRates = new Dictionary<string, float>
                    {
                        ["stones"] = 5f
                    },
                    bagBlacklist = new List<string>
                    {
                        "rocket.launcher"
                    }
                },
                nutritionSetup = new Configuration.AnimalInfo.FoodSetup()
                {
                    foodInfo = new Dictionary<string, float>
                    {
                        ["pumpkin"] = 10f,
                        ["corn"] = 5f,
                        ["apple"] = 5f,
                    }
                }
            });

            _config.installItem.Add(new Configuration.ItemInfo 
            {
                name = "Wolf",
                skin = 1234,
                shortname = "furnace",
                animal = new Configuration.AnimalInfo
                {
                    interactionSetup = new Configuration.AnimalInfo.InteractionSetup
                    {
                        collectRates = new Dictionary<string, float>
                        {
                            ["stones"] = 5f
                        },
                        bagBlacklist = new List<string>
                        {
                            "rocket.launcher"
                        }
                    },
                    nutritionSetup = new Configuration.AnimalInfo.FoodSetup()
                    {
                        foodInfo = new Dictionary<string, float>
                        {
                            ["pumpkin"] = 10f,
                            ["corn"] = 5f,
                            ["apple"] = 5f,
                        }
                    }
                }
            });

            _config.gui.accessButtons.Add(new Configuration.GUISetup.AccessButton("Stay on Position", new string[] {"panimal idle"}));

            _config.lootEntities.Add("foodbox");
            _config.lootEntities.Add("vehicle_parts");
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
            catch (Exception ex)
            {
                PrintError("{0}", ex);
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        #endregion

        #region Loc

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ChatCommand_Success_Despawn"] = "Your personal animal has been successfully despawned!",
                ["ChatCommand_Success_Spawn"] = "Your personal animal has been successfully spawned!",

                ["ChatCommand_Notice_Cooldown"] = "You need to wait {0} seconds before re-spawning the animal!",
                ["ChatCommand_Notice_Location"] = "Your animal is in the grid: {0}, distance to it: {1}",
                ["ChatCommand_Notice_Health"] = "Your animal's health: {0}/{1}",
                ["ChatCommand_Notice_Follow"] = "The animal is now following you!",
                ["ChatCommand_Notice_AvailableBots"] = "<size=16>Available animals:</size>\n{BOTS}\n\nEnter /panimal [short name of the animal], to spawn!",

                ["ChatCommand_Error_AutoPickup"] = "<size=16>Auto-pickup resources</size>\n\nThe animal will begin to collect all resources within a radius of 50 meters from its original point.\n\nAvailable mods: all, wood, stone, metal, sulfur\nDisable - /panimal auto-pickup disable",
                ["ChatCommand_Error_NoPermission"] = "You do not have permission to spawn a personal animal",
                ["ChatCommand_Error_CannotUse"] = "Your animal does not have this function, you cannot use it!",
                ["ChatCommand_Error_NoBot"] = "You don't have a personal animal!",
                ["ChatCommand_Error_NotFounded"] = "Animal not found!",
                ["ChatCommand_Error_Blacklist"] = "This item has been blacklisted, you cannot give it to an animal!",
                ["ChatCommand_Error_CannotSpawn"] = "You can't spawn a personal animal here!",

                ["ChatCommand_Notice_AutoPickup_Status"] = "Auto-pickup resources: {0}\nResources to collect: {1}",

                ["ChatCommand_AutoMode_Resources_All"] = "all",
                ["ChatCommand_AutoMode_Resources_Wood"] = "wood",
                ["ChatCommand_AutoMode_Resources_Stone"] = "stone",
                ["ChatCommand_AutoMode_Resources_Sulfur"] = "sulfur",
                ["ChatCommand_AutoMode_Resources_Metal"] = "metal",
                ["ChatCommand_AutoMode_Resources_Hemp"] = "hemp",
                ["ChatCommand_AutoMode_Resources_Berries"] = "berries",
                ["ChatCommand_AutoMode_Resources_Corn"] = "corn",
                ["ChatCommand_AutoMode_Resources_Mushroom"] = "mushroom",
                ["ChatCommand_AutoMode_Resources_Pumpkin"] = "pumpkin",

                ["ChatCommand_AutoMode_Status_Disabled"] = "disabled",
                ["ChatCommand_AutoMode_Status_Enabled"] = "enabled",

                ["Bot_Notice_MissionCompleted"] = "Mission completed, backing to you!",
                ["Bot_Notice_GoingCollect"] = "Going to collect the resource!",
                ["Bot_Notice_GoingLootBox"] = "Going to loot the box!",
                ["Bot_Notice_Following"] = "Following you!",
                ["Bot_Notice_Staying"] = "Staying on the position.",
                ["Bot_Notice_StartedAttack"] = "Starting attack!",
                ["Bot_Notice_GoingPosition"] = "Going to the position.",

                ["Bot_Error_NoResourcesAround"] = "No resources around!",
                ["Bot_Error_Dead_NotOwner"] = "You can't loot this bag because you aren't the owner!",

                ["GUI_Header"] = "Animal Control",
                ["GUI_Follow"] = "Follow",
                ["GUI_Kill"] = "Kill",

                ["Text_Inventory"] = "Open Inventory - R",
                ["Text_Drive"] = "Saddle the animal - E"
            }, this, "en");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ChatCommand_Success_Despawn"] = "Ваше персональное животное успешно задеспавнено!",
                ["ChatCommand_Success_Spawn"] = "Ваше персональное животное успешно заспавнено!",

                ["ChatCommand_Notice_Cooldown"] = "Вам нужно подождать {0} секунд, прежде чем повторно заспавнить животное!",
                ["ChatCommand_Notice_Location"] = "Ваше животное находится в квадрате: {0}, расстояние до него: {1}",
                ["ChatCommand_Notice_Health"] = "Здоровье вашего животного: {0}/{1}",
                ["ChatCommand_Notice_Follow"] = "Животное теперь следует за вами!",
                ["ChatCommand_Notice_AvailableBots"] = "<size=16>Доступные животные:</size>\n{BOTS}\n\nВведите /panimal [короткое название животного], чтобы заспавнить!",

                ["ChatCommand_Error_AutoPickup"] = "<size=16>Авто-подбор ресурсов</size>\n\nЖивотное начнет собирать все ресурсы в радиусе 50 метров от его первоначальной точки.\n\nДоступные режимы: all, wood, stone, metal, sulfur\nОтключить - /panimal auto-pickup disable",
                ["ChatCommand_Error_NoPermission"] = "У вас нет разрешения на спавн персонального животного",
                ["ChatCommand_Error_CannotUse"] = "Ваше животное не обладает такой функцией, вы не можете ее использовать!",
                ["ChatCommand_Error_NoBot"] = "У вас нет персонального животного!",
                ["ChatCommand_Error_NotFounded"] = "Животное не найдено!",
                ["ChatCommand_Error_Blacklist"] = "Этот предмет добавлен в черный список, вы не можете дать его животному!",
                ["ChatCommand_Error_CannotSpawn"] = "Вы не можете заспавнить персональное животное здесь!",

                ["ChatCommand_Notice_AutoPickup_Status"] = "Авто-подбор ресурсов: {0}\nРесурсы для сбора: {1}",

                ["ChatCommand_AutoMode_Resources_All"] = "все",
                ["ChatCommand_AutoMode_Resources_Wood"] = "дерево",
                ["ChatCommand_AutoMode_Resources_Stone"] = "камень",
                ["ChatCommand_AutoMode_Resources_Sulfur"] = "сера",
                ["ChatCommand_AutoMode_Resources_Metal"] = "металл",
                ["ChatCommand_AutoMode_Resources_Hemp"] = "ткань",
                ["ChatCommand_AutoMode_Resources_Berries"] = "ягоды",
                ["ChatCommand_AutoMode_Resources_Corn"] = "кукуруза",
                ["ChatCommand_AutoMode_Resources_Mushroom"] = "гриб",
                ["ChatCommand_AutoMode_Resources_Pumpkin"] = "тыква",

                ["ChatCommand_AutoMode_Status_Disabled"] = "отключён",
                ["ChatCommand_AutoMode_Status_Enabled"] = "включён",

                ["Bot_Notice_MissionCompleted"] = "Цель выполнена, возвращаюсь к вам!",
                ["Bot_Notice_GoingCollect"] = "Иду собирать ресурс!",
                ["Bot_Notice_GoingLootBox"] = "Иду лутать ящик!",
                ["Bot_Notice_Following"] = "Следую за вами!",
                ["Bot_Notice_Staying"] = "Стою на позиции.",
                ["Bot_Notice_StartedAttack"] = "Начинаю атаку!",
                ["Bot_Notice_GoingPosition"] = "Иду на позицию.",

                ["Bot_Error_NoResourcesAround"] = "Нет ресурсов поблизости!",
                ["Bot_Error_Dead_NotOwner"] = "Вы не можете залутать эту сумку потому что вы не ее владелец!",

                ["GUI_Header"] = "Управление животным",
                ["GUI_Follow"] = "Следовать",
                ["GUI_Kill"] = "Убить",

                ["Text_Inventory"] = "Открыть инвентарь - R",
                ["Text_Drive"] = "Оседлать животное - E"
            }, this, "ru");
        }

        #endregion

        #region Hooks

        private void OnServerInitialized()
        {
            if(ImageLibrary == null)
            {
                PrintError("Install ImageLibrary / Установите ImageLibrary!");
                NextTick(() => Interface.Oxide.UnloadPlugin(Title));

                return;
            }

            ImageLibrary.CallHook("AddImage", "https://api.rustyplugin.ru/pnpc/arrow.png", "PersonalAnimal_Close");
            ImageLibrary.CallHook("AddImage", "https://api.rustyplugin.ru/pnpc/arrow2.png", "PersonalAnimal_Open");

            switch(_config.controlsSetup.key)
            {
                case "E":
                    controlButton = BUTTON.USE;
                    break;

                case "MIDDLE_MOUSE":
                    controlButton = BUTTON.FIRE_THIRD;
                    break;

                case "RELOAD":
                    controlButton = BUTTON.RELOAD;
                    break;
                
                case "SPRINT":
                    controlButton = BUTTON.SPRINT;
                    break;

                case "SECOND_MOUSE":
                    controlButton = BUTTON.FIRE_SECONDARY;
                    break;
            }

            switch(_config.controlsSetup.inventoryKey)
            {
                case "E":
                    inventoryButton = BUTTON.USE;
                    break;

                case "MIDDLE_MOUSE":
                    inventoryButton = BUTTON.FIRE_THIRD;
                    break;

                case "RELOAD":
                    inventoryButton = BUTTON.RELOAD;
                    break;
                
                case "SPRINT":
                    inventoryButton = BUTTON.SPRINT;
                    break;

                case "SECOND_MOUSE":
                    inventoryButton = BUTTON.FIRE_SECONDARY;
                    break;
            }

            switch(_config.controlsSetup.mountKey)
            {
                case "E":
                    mountButton = BUTTON.USE;
                    break;

                case "MIDDLE_MOUSE":
                    mountButton = BUTTON.FIRE_THIRD;
                    break;

                case "RELOAD":
                    mountButton = BUTTON.RELOAD;
                    break;
                
                case "SPRINT":
                    mountButton = BUTTON.SPRINT;
                    break;

                case "SECOND_MOUSE":
                    mountButton = BUTTON.FIRE_SECONDARY;
                    break;
            }
        }

        private object OnPlayerWantsMount(BasePlayer player, BaseMountable entity)
        {    
            if(entity == null || player == null) return null;
            if(!_existsOwnerComponents.ContainsKey(entity.net.ID.Value)) return null;
           
            AnimalOwnerComponent ownerComponent = _existsOwnerComponents[entity.net.ID.Value];

            if(ownerComponent == null)
            {
                _existsOwnerComponents.Remove(entity.net.ID.Value);

                return null;
            }

            if(ownerComponent.controller == null) return null;

            entity.AttemptMount(player, false);
            ownerComponent.controller.IsDriving = true;

            return false;
        }

        private object OnNpcTarget(BaseEntity npc, BaseEntity entity)
        {
            if(entity == null || npc == null) return null;
            if(!_existsOwnerComponents.ContainsKey(npc.net.ID.Value)) return null;
           
            AnimalOwnerComponent ownerComponent = _existsOwnerComponents[npc.net.ID.Value];

            if(ownerComponent == null)
            {
                _existsOwnerComponents.Remove(npc.net.ID.Value);

                return null;
            }

            if(entity == ownerComponent.controller.owner) return false;
           
            return null;
        }

        private void OnEntityDeath(BaseAnimalNPC entity, HitInfo info)
        {
            if(entity == null) return;

            var ownerComponent = entity.GetComponent<AnimalOwnerComponent>();

            if(ownerComponent)
            {
                _existsOwnerComponents.Remove(entity.net.ID.Value);
            
                if(ownerComponent.controller && !ownerComponent.isLootSpawned)
                {
                    var controller = ownerComponent.controller;
                    var source = controller.owner.inventory.loot.entitySource;

                    _existsControllers.Remove(controller.GetComponent<BaseNetworkable>().net.ID.Value);
                    
                    if(source != null)
                    {
                        var storage = source as StorageContainer;

                        if(storage)
                        {
                            if(storage.name == $"panimal_{controller.owner.userID}")
                            {
                                controller.owner.EndLooting();
                            }
                        }
                    }

                    if(controller.bagItems.Count != 0)
                    {
                        var backpack = GameManager.server.CreateEntity("assets/prefabs/misc/item drop/item_drop_backpack.prefab", entity.transform.position, Quaternion.identity) as DroppedItemContainer;

                        backpack.inventory = new ItemContainer();
                        backpack.inventory.ServerInitialize((Item) null, controller.bagItems.Count);
                        backpack.inventory.GiveUID();
                        backpack.inventory.SetFlag(ItemContainer.Flag.NoItemInput, true);

                        foreach(var bagItem in controller.bagItems) 
                        {
                            var obj = bagItem.ToItem();

                            if (!obj.MoveToContainer(backpack.inventory))
                                obj.DropAndTossUpwards(backpack.transform.position);
                        }

                        backpack.ResetRemovalTime();
                        backpack.Spawn();

                        if(controller.animalSetup.functionsSetup.onlyOwnerLootDeadBag)
                        {
                            backpack.name = $"panimal_{controller.owner.userID}";
                            backpack.OwnerID = controller.owner.userID;
                        }
                    }
                }
                
                if(ownerComponent.controller != null) 
                {
                    if(ownerComponent.controller.animalSetup.interactionSetup.despawnCorpse)
                    {
                        entity.transform.position = new Vector3(0, -1000, 0);
                    }
                }

                if(ownerComponent.controller) UnityEngine.Object.Destroy(ownerComponent.controller);

                ownerComponent.isLootSpawned = true;
            }
        }

        private object OnPlayerViolation(BasePlayer player, AntiHackType type, float amount)
        {
            if(player == null || type == AntiHackType.None) return null;
            if(!_existsControllers.ContainsKey(player.net.ID.Value)) return null;
            
            var controller = _existsControllers[player.net.ID.Value];
            
            if(controller == null)
            {
                _existsControllers.Remove(player.net.ID.Value);
                return null;
            }

            if(controller.IsDriving)
            {
                if(!_config.protect.flyHack && type == AntiHackType.FlyHack) return false;
                if(!_config.protect.insideTerrain && type == AntiHackType.InsideTerrain) return false;
                if(!_config.protect.noClip && type == AntiHackType.NoClip) return false;
                if(!_config.protect.speedHack && type == AntiHackType.SpeedHack) return false;
            }
           
            return null;
        }

        private void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if(player == null || entity == null) return;

            if(entity is DroppedItemContainer || entity is LootableCorpse)
            {
                if(entity.name.Contains("panimal"))
                {
                    if(entity.OwnerID != player.userID)
                    {
                        player.ChatMessage(GetMsg("Bot_Error_Dead_NotOwner", player.UserIDString));
                        NextTick(() => player.EndLooting());

                        return;
                    }
                }
            }
        }

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if(entity == null || info == null || info?.Initiator == null || entity.net == null || info?.Initiator?.net == null) return null;

            BaseEntity target = info.Initiator;
            if(!(target is BaseCombatEntity)) return null;

            if(_existsOwnerComponents.ContainsKey(target.net.ID.Value))
            {
                var initiatorBotOwner = _existsOwnerComponents[target.net.ID.Value];

                if(initiatorBotOwner != null && initiatorBotOwner?.controller != null)
                {
                    info.damageTypes.ScaleAll(initiatorBotOwner.controller.animalSetup.interactionSetup.botDamageRate);
                    if(_config.damageScale.ContainsKey(entity.ShortPrefabName)) info.damageTypes.ScaleAll(_config.damageScale[entity.ShortPrefabName]);

                    if(!initiatorBotOwner.controller.animalSetup.interactionSetup.canDamagePlayers)
                    {
                        var victim = entity.ToPlayer();

                        if(victim != null)
                        {
                            if(!victim.IsNpc && victim.userID.IsSteamId())
                            {
                                return false;
                            }
                        }
                    }
                }
            }

            if(_existsControllers.ContainsKey(entity.net.ID.Value))
            {
                var controller = _existsControllers[entity.net.ID.Value];

                if(controller != null) 
                {
                    if(target == controller?.bot) return false;

                    controller.OnAttacked(target, info, true);
                }

                return null;
            }
        
            if(_existsOwnerComponents.ContainsKey(entity.net.ID.Value))
            {
                var ownerComponent = _existsOwnerComponents[entity.net.ID.Value];
                
                if(ownerComponent != null && ownerComponent?.controller != null) 
                {
                    if(entity == ownerComponent.controller?.owner) return false;

                    info.damageTypes?.ScaleAll(ownerComponent.controller.animalSetup.interactionSetup.botReceiveDamageRate);

                    if(!ownerComponent.controller.animalSetup.interactionSetup.canBeDamagedByPlayers)
                    {
                        var player = target.ToPlayer();

                        if(player != null)
                        {
                            if(player.IsNpc == false && player.userID.IsSteamId())
                            {
                                return false;
                            }
                        }
                    }
                    
                    ownerComponent.controller.OnAttacked(target, info);
                }
            }

            return null;
        }

        private void Unload() 
        {
            var finded = _existsControllers;

            for (int i = finded.Count - 1; i >= 0; i--)
            {
                var obj = new List<PlayerAnimalController>(_existsControllers.Values)[i];

                if(obj.bot != null) obj.bot.Kill();
                if(obj) UnityEngine.Object.Destroy(obj);
            }

            var cachedPlayers = Facepunch.Pool.GetList<BasePlayer>();
            cachedPlayers.AddRange(BasePlayer.activePlayerList);

            cachedPlayers.ForEach(x => CuiHelper.DestroyUi(x, "PersonalAnimal_ControlPanel"));
            Facepunch.Pool.FreeList(ref cachedPlayers);
        }

        private object CanAcceptItem(ItemContainer container, Item item, int targetPos)
        {
            var player = item?.GetOwnerPlayer();

            if(player == null) return null;
            if(!_existsControllers.ContainsKey(player.net.ID.Value)) return null;

            var controller = _existsControllers[player.net.ID.Value];

            if(controller != null)
            {
                var owner = container.GetEntityOwner();
                if(owner == null) return null;

                if(controller.IsBlacklisted(item) && owner.name == $"panimal_{player.userID}")
                {
                    SendMsg(player, "ChatCommand_Error_Blacklist");
                    return ItemContainer.CanAcceptResult.CannotAcceptRightNow;
                }
            }
            else 
            {
                _existsControllers.Remove(player.net.ID.Value);
            }

            return null;
        }

        private void Loaded() 
        {
            Instance = this;

            _permissionKeys = new List<string>(_config.animalInfoPerm.Keys);
            _permissionKeys.ForEach(x => permission.RegisterPermission(x, this));
        }

        private void OnLootEntityEnd(BasePlayer player, BaseCombatEntity entity)
        {
            if(!_existsControllers.ContainsKey(player.net.ID.Value)) return;

            var controller = _existsControllers[player.net.ID.Value];
            if(controller == null) 
            {
                _existsControllers.Remove(player.net.ID.Value);

                return;
            }

            if(!controller.animalSetup.addonsSetup.needBag) return;
            
            var storage = entity as StorageContainer;

            if(storage == null) return;
            if(storage.name != $"panimal_{player.userID}") return;
        
            controller.UpdateItems(storage.inventory);
        }

        private void OnEntityBuilt(Planner plan, GameObject go)
        {
            if(_config.installItem.Count == 0) return;

            var player = plan.GetOwnerPlayer();
            if(player == null) return;

            var item = player.GetActiveItem();
            if(item == null) return;

            foreach(var loopInfo in _config.installItem)
            {
                if(loopInfo.skin == item.skin)
                {
                    PlayerAnimalController comp;

                    if(player.TryGetComponent<PlayerAnimalController>(out comp)) 
                    {
                        if(comp.ItemInfo.returnDespawn)
                        {
                            Item panimal = ItemManager.CreateByName(comp.ItemInfo.shortname, 1, comp.ItemInfo.skin);
                            if(!string.IsNullOrEmpty(comp.ItemInfo.name)) panimal.name = comp.ItemInfo.name;

                            player.GiveItem(panimal);
                        }

                        comp.bot.Kill();
                        //OnEntityDeath(comp.bot, null);
                    }

                    NextTick(() =>
                    {
                        var controller = player.gameObject.AddComponent<PlayerAnimalController>();
                        
                        controller.bot = CreateBot(player, loopInfo.animal, go.transform.position);
                        controller.owner = player;

                        controller.mountButton = mountButton;
                        controller.controlButton = controlButton;
                        controller.inventoryButton = inventoryButton;

                        controller.ItemInfo = loopInfo;

                        _existsControllers.Remove(player.net.ID.Value);
                        _existsControllers.Add(player.net.ID.Value, controller);

                        SendMsg(player, "ChatCommand_Success_Spawn");
                        go.ToBaseEntity().Kill();
                    });

                    break;
                }
            }
        }

        #endregion

        #region Methods

        [ChatCommand("panimal")]
        private void chatCommand(BasePlayer player, string command, string[] args) 
        {
            if(args == null || args?.Length == 0) 
            {
                PlayerAnimalController playerAnimalController;

                if(player.TryGetComponent<PlayerAnimalController>(out playerAnimalController)) 
                {
                    if(playerAnimalController.ItemInfo.returnDespawn)
                    {
                        Item panimal = ItemManager.CreateByName(playerAnimalController.ItemInfo.shortname, 1, playerAnimalController.ItemInfo.skin);
                        if(!string.IsNullOrEmpty(playerAnimalController.ItemInfo.name)) panimal.name = playerAnimalController.ItemInfo.name;

                        player.GiveItem(panimal);
                    }

                    if(playerAnimalController.bot) 
                    {
                        player.EndLooting();
                        playerAnimalController.bot.Kill();
                    }

                    SendMsg(player, "ChatCommand_Success_Despawn");
                }
                else 
                {
                    var botSetup = GetBotSetup(player);

                    if(botSetup != null)
                    {
                        if(botSetup.Count == 0) SendMsg(player, "ChatCommand_Error_NoPermission");
                        else 
                        {
                            if(botSetup.Count == 1)
                            {
                                chatCommand(player, command, new string[] {botSetup[0].spawnName});

                                return;
                            }

                            string msg = lang.GetMessage("ChatCommand_Notice_AvailableBots", this, player.UserIDString);
                            string availableBots = "";

                            foreach(var bot in botSetup)
                            {
                                availableBots = availableBots + $"\n{botSetup.IndexOf(bot) + 1}. {bot.spawnName}";
                            }

                            msg = msg.Replace("{BOTS}", availableBots);

                            player.ChatMessage(msg);
                        }
                    }
                }

                return;
            }

            if(args[0] == "idle")
            {
                var controller = player.GetComponent<PlayerAnimalController>();

                if(controller == null)
                {
                    SendMsg(player, "ChatCommand_Error_NoBot");

                    return;
                }
                else 
                {
                    controller.Idle();
                    SendMsg(player, "Bot_Notice_Staying");

                    return;
                }
            }

            if(args[0] == "where")
            {
                var controller = player.GetComponent<PlayerAnimalController>();

                if(controller == null) 
                {
                    SendMsg(player, "ChatCommand_Error_NoBot");

                    return;
                }
                else 
                {
                    SendMsg(player, "ChatCommand_Notice_Location", new string[] {PersonalAnimal.GetGrid(controller.bot.transform.position), Vector3.Distance(controller.bot.transform.position, player.transform.position).ToString()} );

                    return;
                }
            }

            if(args[0] == "health")
            {
                var controller = player.GetComponent<PlayerAnimalController>();

                if(controller == null) 
                {
                    SendMsg(player, "ChatCommand_Error_NoBot");

                    return;
                }
                else 
                {
                    SendMsg(player, "ChatCommand_Notice_Health", new string[] {Mathf.RoundToInt(controller.bot.Health()).ToString(), Mathf.RoundToInt(controller.bot.MaxHealth()).ToString()} );

                    return;
                }
            }

            if(args[0] == "follow")
            {
                var controller = player.GetComponent<PlayerAnimalController>();

                if(controller == null) 
                {
                    SendMsg(player, "ChatCommand_Error_NoBot");
                    
                    return;
                }
                else 
                {
                    controller.FollowPlayer();
                    SendMsg(player, "Bot_Notice_Following");

                    return;
                }
            }

            if(args[0] == "auto-pickup")
            {
                if(args.Length == 1)
                {
                    SendMsg(player, "ChatCommand_Error_AutoPickup");

                    return;
                }
                else 
                {
                    var controller = player.GetComponent<PlayerAnimalController>();

                    if(controller == null)
                    {
                        SendMsg(player, "ChatCommand_Error_NoBot");

                        return;
                    }

                    var compMode = controller.mode;

                    if(compMode == null)
                    {
                        SendMsg(player, "ChatCommand_Error_CannotUse");

                        return;
                    }
                    else if(!controller.animalSetup.functionsSetup.canAutoCollect)
                    {
                        SendMsg(player, "ChatCommand_Error_CannotUse");

                        return;
                    }

                    if(args[1] == "disable")
                    {
                        compMode.Disable();

                        SendMsg(player, "ChatCommand_Notice_AutoPickup_Status");

                        return;
                    }

                    if(args[1] == "enable")
                    {
                        compMode.SetMode(AnimalAutoMode.AutoMode.Pickup);
                        compMode.EnableMode();

                        SendMsg(player, "ChatCommand_Notice_AutoPickup_Status");

                        return;
                    }

                    if(args[1] == "all")
                    {
                        compMode.AddResource(AnimalAutoMode.Resources.All);

                        SendMsg(player, "ChatCommand_Notice_AutoPickup_Status");
                    
                        return;
                    }

                    if(args[1] == "stone")
                    {
                        compMode.AddResource(AnimalAutoMode.Resources.Stone);

                        SendMsg(player, "ChatCommand_Notice_AutoPickup_Status");
                    
                        return;
                    }

                    if(args[1] == "metal")
                    {
                        compMode.AddResource(AnimalAutoMode.Resources.Metal);

                        SendMsg(player, "ChatCommand_Notice_AutoPickup_Status");
                    
                        return;
                    }

                    if(args[1] == "sulfur")
                    {
                        compMode.AddResource(AnimalAutoMode.Resources.Sulfur);

                        SendMsg(player, "ChatCommand_Notice_AutoPickup_Status");
                    
                        return;
                    }

                    if(args[1] == "wood")
                    {
                        compMode.AddResource(AnimalAutoMode.Resources.Wood);

                        SendMsg(player, "ChatCommand_Notice_AutoPickup_Status");

                        return;
                    }

                    if(args[1] == "hemp")
                    {
                        compMode.AddResource(AnimalAutoMode.Resources.Hemp);

                        SendMsg(player, "ChatCommand_Notice_AutoPickup_Status");

                        return;
                    }

                    if(args[1] == "corn")
                    {
                        compMode.AddResource(AnimalAutoMode.Resources.Corn);

                        SendMsg(player, "ChatCommand_Notice_AutoPickup_Status");

                        return;
                    }

                    if(args[1] == "mushroom")
                    {
                        compMode.AddResource(AnimalAutoMode.Resources.Mushroom);

                        SendMsg(player, "ChatCommand_Notice_AutoPickup_Status");

                        return;
                    }

                    if(args[1] == "pumpkin")
                    {
                        compMode.AddResource(AnimalAutoMode.Resources.Pumpkin);

                        SendMsg(player, "ChatCommand_Notice_AutoPickup_Status");

                        return;
                    }

                    if(args[1] == "berries")
                    {
                        compMode.AddResource(AnimalAutoMode.Resources.Berries);

                        SendMsg(player, "ChatCommand_Notice_AutoPickup_Status");

                        return;
                    }

                    compMode.Disable();

                    SendMsg(player, "ChatCommand_Notice_AutoPickup_Status");

                    return;
                }
            }

            var bots = GetBotSetup(player);

            if(bots.Count != 0)
            {
                List<Configuration.AnimalInfo> botsFinded = new List<Configuration.AnimalInfo>();

                foreach(var botSetup in bots)
                {
                    if(botSetup.spawnName == args[0]) botsFinded.Add(botSetup);
                }

                Configuration.AnimalInfo bot;

                if(botsFinded.Count != 0) bot = botsFinded[0];
                else 
                {
                    SendMsg(player, "ChatCommand_Error_NotFounded");

                    return;
                }

                if(bot != null)
                {
                    string perm = string.Empty;

                    foreach(var pair in _config.animalInfoPerm)
                    {
                        if(pair.Value == bot)
                        {
                            perm = pair.Key;

                            break;
                        }
                    }

                    if(permission.UserHasPermission(player.UserIDString, perm))
                    {
                        PlayerAnimalController comp;

                        if(player.TryGetComponent<PlayerAnimalController>(out comp)) 
                        {
                            if(comp.ItemInfo.returnDespawn)
                            {
                                Item panimal = ItemManager.CreateByName(comp.ItemInfo.shortname, 1, comp.ItemInfo.skin);
                                if(!string.IsNullOrEmpty(comp.ItemInfo.name)) panimal.name = comp.ItemInfo.name;

                                player.GiveItem(panimal);
                            }

                            if(comp.bot) comp.bot.Kill();
                            SendMsg(player, "ChatCommand_Success_Despawn");

                            return;
                        }

                        if(_config.spawnInfo.canSpawnInCupboard == false && player.GetBuildingPrivilege() != null)
                        {
                            SendMsg(player, "ChatCommand_Error_CannotSpawn");
                            return;
                        }

                        if(!_config.spawnInfo.canSpawnOnConstruction)
                        {
                            RaycastHit hit;

                            if(Physics.Raycast(player.transform.position + Vector3.up * 0.5f, Vector3.down, out hit, 100f, LayerMask.GetMask("Construction")))
                            {
                                SendMsg(player, "ChatCommand_Error_CannotSpawn");
                                return;
                            }
                        }

                        if(!_config.spawnInfo.canSpawnOnDeployed)
                        {
                            RaycastHit hit;

                            if(Physics.Raycast(player.transform.position + Vector3.up * 0.5f, Vector3.down, out hit, 100f, LayerMask.GetMask("Deployed")))
                            {
                                SendMsg(player, "ChatCommand_Error_CannotSpawn");
                                return;
                            }
                        }

                        if(_cooldownInfo.ContainsKey(player.userID))
                        {
                            var lastTimeSpawn = _cooldownInfo[player.userID];

                            if(DateTime.Now > lastTimeSpawn.AddSeconds(bot.cooldown))
                            {
                                _cooldownInfo.Remove(player.userID);
                                _cooldownInfo.Add(player.userID, DateTime.Now);
                            }
                            else 
                            {
                                SendMsg(player, "ChatCommand_Notice_Cooldown", new string[] { Mathf.RoundToInt((float)(lastTimeSpawn.AddSeconds(bot.cooldown) - DateTime.Now).TotalSeconds).ToString() });
                                return;
                            }
                        }
                        else 
                        {
                            _cooldownInfo.Add(player.userID, DateTime.Now);
                        }

                        var controller = player.gameObject.AddComponent<PlayerAnimalController>();
                        
                        controller.bot = CreateBot(player, bot, player.transform.position, true);
                        controller.owner = player;

                        controller.mountButton = mountButton;
                        controller.controlButton = controlButton;
                        controller.inventoryButton = inventoryButton;

                        _existsControllers.Remove(player.net.ID.Value);
                        _existsControllers.Add(player.net.ID.Value, controller);

                        SendMsg(player, "ChatCommand_Success_Spawn");
                    }
                    else SendMsg(player, "ChatCommand_Error_NoPermission");
                }
                else 
                {
                    SendMsg(player, "ChatCommand_Error_NotFounded");
                }
            }
            else SendMsg(player, "ChatCommand_Error_NoPermission");
        }

        public static string GetGrid(Vector3 position) // Credit: Jake_Rich
        {
            var roundedPos = new Vector2(World.Size / 2 + position.x, World.Size / 2 - position.z);
            var grid = $"{NumberToLetter((int)(roundedPos.x / 150))}{(int)(roundedPos.y / 150)}";

            return grid;
        }

        public static string NumberToLetter(int num) // Credit: Jake_Rich
        {
            var num2 = Mathf.FloorToInt((float)(num / 26));
            var num3 = num % 26;
            var text = string.Empty;
            if (num2 > 0)
            {
                for (var i = 0; i < num2; i++)
                {
                    text += Convert.ToChar(65 + i);
                }
            }

            return text + Convert.ToChar(65 + num3);
        }

        [ConsoleCommand("panimal")]
        private void cnslCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();

            if(player == null) return;

            var controller = player.GetComponent<PlayerAnimalController>();

            if(controller == null) return;

            if(arg.HasArgs())
            {
                if(arg.Args[0] == "follow")
                {
                    controller.FollowPlayer();
                    SendMsg(player, "Bot_Notice_Following");

                    return;
                }

                if(arg.Args[0] == "command")
                {
                    if(arg.HasArgs(2))
                    {
                        string args = "";

                        if(arg.HasArgs(3))
                        {
                            for(int i = 2; i < arg.Args.Length; i++)
                            {
                                args = args + $"\"{arg.Args[i]}\"";
                            }
                        }

                        rust.RunClientCommand(player, $"chat.say", new string[] { $"/{arg.Args[1]} {args}" });
                    }

                    return;
                }

                if(arg.Args[0] == "hierarchy")
                {
                    controller.RenderHierarchy();

                    return;
                }

                if(arg.Args[0] == "hide_panel")
                {
                    controller.IsGUIHidden = !controller.IsGUIHidden;
                    controller.RenderMenu(true);

                    return;
                }

                int index = 0;

                if(int.TryParse(arg.Args[0], out index))
                {
                    foreach(var command in _config.gui.accessButtons[index].commands)
                    {
                        player.SendConsoleCommand($"panimal command {command}");
                    }
                }
            }
        }

        [ConsoleCommand("panimal.item")]
        private void cnslCommandItem(ConsoleSystem.Arg arg)
        {
            if(arg.Player() != null) return;

            if(!arg.HasArgs(2))
            {
                PrintError("Введите Steam ID и скин предмета! / Please enter Steam ID and item skin!");
                return;
            }

            ulong id, skin;

            if(!ulong.TryParse(arg.Args[0], out id))
            {
                PrintError("Steam ID указан неверно / Steam ID is incorrect");
                return;
            }

            if(!ulong.TryParse(arg.Args[1], out skin))
            {
                PrintError("Скин указан неверно / Skin is incorrect");
                return;
            }

            BasePlayer reciver = BasePlayer.FindByID(id);
            
            if(reciver == null)
            {
                PrintError("Игрок не найден / Player not found");
                return;
            }

            Configuration.ItemInfo info = new Configuration.ItemInfo();

            foreach(var loopInfo in _config.installItem)
            {
                if(loopInfo.skin == skin)
                {
                    info = loopInfo;
                    break;
                }
            }

            if(info.animal == null)
            {
                PrintError("Предмет не найден / Item is not found");
                return;
            }

            Item pnpc = ItemManager.CreateByName(info.shortname, 1, info.skin);
            if(!string.IsNullOrEmpty(info.name)) pnpc.name = info.name;

            reciver.GiveItem(pnpc);
            Puts($"Item was successfully given to player {reciver.displayName}");
        }

        public static string HexToRustFormat(string hex)
        {
            Color color;
            ColorUtility.TryParseHtmlString(hex, out color);

            var sb = new System.Text.StringBuilder();
            return sb.AppendFormat("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a).ToString();
        }

        private List<Configuration.AnimalInfo> GetBotSetup(BasePlayer player)
        {
            List<Configuration.AnimalInfo> setups = new List<Configuration.AnimalInfo>();

            foreach(var key in _permissionKeys)
            {
                if(permission.UserHasPermission(player.UserIDString, key))
                {
                    setups.Add(_config.animalInfoPerm[key]);
                }
            }

            return setups;
        }

        private string GetMsg(string key, string id) => lang.GetMessage(key, this, id);

        private void SendMsg(BasePlayer player, string key, string[] args = null) 
        {
            if(args != null) player.ChatMessage(string.Format(lang.GetMessage(key, this, player.UserIDString), args));
            else 
            {
                if(key == "ChatCommand_Notice_AutoPickup_Status" || key == "ChatCommand_Notice_AutoFarm_Status")
                {
                    if(_existsControllers.ContainsKey(player.net.ID.Value))
                    {
                        var controller = _existsControllers[player.net.ID.Value];

                        if(controller != null)
                        {
                            string msg = "";
                            string status = "";

                            if(controller.mode.IsDisabled()) status = lang.GetMessage("ChatCommand_AutoMode_Status_Disabled", this, player.UserIDString);
                            else status = lang.GetMessage("ChatCommand_AutoMode_Status_Enabled", this, player.UserIDString);

                            var resources = controller.mode.GetResources();

                            for(int i = 0; i < resources.Length; i++)
                            {
                                msg += $"{lang.GetMessage($"ChatCommand_AutoMode_Resources_{resources[i]}", this, player.UserIDString)}, ";
                            }

                            if(msg.Length - 2 >= 0) msg = msg.Remove(msg.Length - 2);

                            SendMsg(player, key, new string[] 
                            {
                                status,
                                msg
                            });

                            return;
                        }
                    }
                }

                player.ChatMessage(lang.GetMessage(key, this, player.UserIDString));
            }
        }

        private BaseAnimalNPC CreateBot(BasePlayer player, Configuration.AnimalInfo animalInfo, Vector3 pos, bool offset = false)
        {
            BaseAnimalNPC bot = GameManager.server.CreateEntity(animalInfo.TypeToEntity(), pos + new Vector3((offset ? 0.3f : 0f), 0, 0)) as BaseAnimalNPC;

            bot.Spawn();
            bot.InitializeHealth(animalInfo.maxHealth, animalInfo.maxHealth);

            var controller = player.GetComponent<PlayerAnimalController>();

            controller.pluginInstance = this;
            controller.animalSetup = animalInfo;
            controller.plugin = this;
            controller.npcPlugin = PersonalNPC;

            controller.cachedImages.Add("open", ImageLibrary.Call<string>("GetImage", $"PersonalAnimal_Open"));
            controller.cachedImages.Add("close", ImageLibrary.Call<string>("GetImage", $"PersonalAnimal_Close"));

            var ownerComponent = bot.gameObject.AddComponent<AnimalOwnerComponent>();
            ownerComponent.controller = controller;

            _existsOwnerComponents.Add(bot.net.ID.Value, ownerComponent);

            return bot;
        }

        #endregion

        #region Behaviour

        private class AnimalOwnerComponent : MonoBehaviour
        {
            public PlayerAnimalController controller; 
            public bool isLootSpawned;

            private void OnTriggerEnter(Collider other)
            {
                if (!controller.animalSetup.addonsSetup.dismountOnRaidableBase) return;
                
                if (other == null) return;

                if (!controller.owner.isMounted) return;
                if (controller.Chair == null) return;

                if (other.name == "RaidableBases")
                {
                    controller.StopDriving();

                    controller.owner.transform.position = controller.owner.transform.position +
                                                          (controller.owner.transform.forward * -1.5f);
                    
                    controller.bot.transform.position = controller.bot.transform.position +
                                                          (controller.bot.transform.forward * -2f);
                }
            }
        }

        private class AnimalAutoMode : MonoBehaviour
        {
            public enum AutoMode {None, Pickup}; 
            public enum Resources {All, Wood, Stone, Metal, Sulfur, Hemp, Berries, Corn, Pumpkin, Mushroom};

            private AutoMode _mode = AutoMode.None;
            private List<string> _resources = new List<string>();
            private PlayerAnimalController _controller;

            private Coroutine _autoModeCoroutine;

            public float lastTimeStarted {get; private set;}
            public float lastTimePickup;

            public Vector3 StartPos {get; private set;}

            private List<ulong> _ignoreList = new List<ulong>();

            private void Start() => _controller = GetComponent<AnimalOwnerComponent>().controller;
            
            public void Disable() 
            {
                _mode = AutoMode.None;
                EnableMode(true);
            }

            public bool IsDisabled() => _mode == AutoMode.None;
            
            public AutoMode GetMode() => _mode;

            public void SetMode(AutoMode newMode) => _mode = newMode;

            public void AddResource(Resources resource) 
            {
                if(resource == Resources.All) 
                {
                    _resources = new List<string>() {"Wood", "Stone", "Metal", "Sulfur", "Hemp", "Corn", "Berries", "Pumpkin", "Mushroom"};
                }
                else
                {
                    _resources.RemoveAll(x => x == "All");

                    if(!_resources.Contains(resource.ToString()))
                    {
                        _resources.Add(resource.ToString());
                    }
                    else _resources.RemoveAll(x => x == resource.ToString());
                }
            }

            public void AddIgnore(ulong net) => _ignoreList.Add(net);

            public void EnableMode(bool disable = false)
            {
                lastTimePickup = Time.realtimeSinceStartup + 10;

                if(disable)
                {
                    if(_autoModeCoroutine != null) _controller.StopCoroutine(_autoModeCoroutine);
                    _ignoreList = new List<ulong>();
                }
                else
                {
                    if(_mode != AutoMode.None && _controller) 
                    {
                        if(GetResources() == new string[] {})
                        {
                            EnableMode(true);

                            return;
                        }

                        StartPos = transform.position;
                        lastTimeStarted = UnityEngine.Time.realtimeSinceStartup;

                        _autoModeCoroutine = _controller.StartCoroutine(_controller.UpdateAutoMode());
                        _controller.StartAutoMode(); 
                    }
                }
            }

            public CollectibleEntity[] GetPickupResourcesInRadius(bool wood = false, bool metal = false, bool sulfur = false, bool stone = false, bool hemp = false, bool corn = false, bool mushroom = false, bool pumpkin = false, bool berries = false)
            {
                var colliders = Physics.OverlapSphere(StartPos, _controller.animalSetup.functionsSetup.collectRadius);
                List<CollectibleEntity> finded = new List<CollectibleEntity>();

                if(colliders.Length != 0)
                {
                    foreach(var collider in colliders)
                    {
                        var ent = collider.ToBaseEntity();

                        if(ent != null)
                        {
                            if(ent is CollectibleEntity)
                            {
                                var collectible = ent as CollectibleEntity;

                                if(_ignoreList.Contains(ent.net.ID.Value)) continue;

                                bool hasWood = false, hasStones = false, hasMetal = false, hasSulfur = false, hasCloth = false, hasCorn = false, hasMushroom = false, hasPumpkin = false, hasBerries = false;

                                foreach(var item in collectible.itemList)
                                {
                                    if(wood && !hasWood) hasWood = item.itemDef.shortname == "wood";
                                    if(stone && !hasStones) hasStones = item.itemDef.shortname == "stones";
                                    if(metal && !hasMetal) hasMetal = item.itemDef.shortname == "metal.ore";
                                    if(sulfur && !hasSulfur) hasSulfur = item.itemDef.shortname == "sulfur.ore";
                                    if(hemp && !hasCloth) hasCloth = item.itemDef.shortname == "cloth";
                                    if(corn && !hasCorn) hasCorn = item.itemDef.shortname == "corn";
                                    if(mushroom && !hasMushroom) hasMushroom = item.itemDef.shortname == "mushroom";
                                    if(pumpkin && !hasPumpkin) hasPumpkin = item.itemDef.shortname == "pumpkin";
                                    if(berries && !hasBerries) hasBerries = (item.itemDef.shortname == "black.berry" || item.itemDef.shortname == "blue.berry" || item.itemDef.shortname == "green.berry" || item.itemDef.shortname == "red.berry" || item.itemDef.shortname == "white.berry" || item.itemDef.shortname == "yellow.berry");
                                }

                                if((wood && hasWood) || (stone && hasStones) || (metal && hasMetal) || (sulfur && hasSulfur) || (hemp && hasCloth) || (corn && hasCorn) || (mushroom && hasMushroom) || (pumpkin && hasPumpkin) || (berries && hasBerries)) 
                                {
                                    finded.Add(collectible);
                                    continue;
                                }
                            }
                        }
                    }
                }

                return finded.ToArray();
            }

            public string[] GetResources() => _resources?.ToArray() ?? new string[] {};
        }

        private class AnimalFeedingTrigger : FacepunchBehaviour
        {
            private PlayerAnimalController _controller;
            private SphereCollider _collider;

            private void Start()
            {
                _controller = GetComponentInParent<AnimalOwnerComponent>().controller;
                _collider = GetComponent<SphereCollider>();
                
                _collider.radius = 1f;
                _collider.isTrigger = true;
            }

            private void OnTriggerEnter(Collider other)
            {
                Invoke(() => 
                {
                    if(other != null)
                    {
                        var ent = other.ToBaseEntity();

                        if(ent != null)
                        {
                            CheckItem(ent);
                        }
                    }
                }, 0.1f);
            }

            private void CheckItem(BaseEntity ent)
            {
                if(ent == null) return;
                if((ent is DroppedItem) == false) return;

                var dropped = ent as DroppedItem;

                if(dropped != null)
                {
                    if(dropped.item?.info.category == ItemCategory.Food)
                    {
                        if(_controller.animalSetup.nutritionSetup.foodInfo.ContainsKey(dropped.item.info.shortname))
                        {
                            _controller.bot.ClientRPC<Vector3>(null, "Eat", ent.transform.position);
                            Invoke(() => Eat(dropped), 3f);
                        }
                    }
                }
            }

            private void Eat(DroppedItem item)
            {
                if(item == null) return;

                item.item.UseItem();
                _controller.bot.Heal(_controller.animalSetup.nutritionSetup.foodInfo[item.item.info.shortname]);

                if(item.item?.amount != 0) CheckItem(item);
            }
        }

        private class PlayerAnimalController : FacepunchBehaviour
        {
            public PersonalAnimal pluginInstance;
            public Configuration.AnimalInfo animalSetup;

            public Plugin plugin, npcPlugin;

            public Dictionary<string, string> cachedImages = new Dictionary<string, string>();

            public AnimalAutoMode mode;
            public BaseAnimalNPC bot;
            public BasePlayer owner;

            private BaseMountable _chair;

            public bool IsDriving;
            public Vector3 DriveDestination;

            private Vector3 _currentDestination;
            private bool _isFollowPlayer, _isIdle, _isViewingHierarchy, _firstTimeRenderOnSecond;

            private float _lastTimeGUI, _lastTimeText;
            private string _tipText;

            private Configuration _config;

            private BaseCombatEntity _lastTarget;
            private LootContainer _containerTarget;
            private CollectibleEntity _collectibleTarget;
            private BaseNavigator _navigator;

            private StorageContainer _storage;
            private BaseNavigator.NavigationSpeed _navigationSpeed = BaseNavigator.NavigationSpeed.Normal;

            public List<ItemData> bagItems = new List<ItemData>();

            private float _lastTimeAttack;
            private CuiRectTransformComponent _lastGUIPosition;

            public bool IsGUIHidden;
            public Configuration.ItemInfo ItemInfo;

            public BaseMountable Chair => _chair;

            public BUTTON mountButton, controlButton, inventoryButton;

            public struct ItemData
            {
                public bool IsBlueprint;

                public ulong Skin;

                public float Condition, MaxCondition, Fuel;

                public int Ammo, AmmoType, FlameFuel, BlueprintTarget, Amount;

                public string Name, Text, Shortname;

                public List<ItemData> Contents;

                public Item ToItem()
                {
                    if(Amount == 0) Amount = 1;

                    Item item = ItemManager.CreateByName(Shortname, Amount, Skin);

                    if (IsBlueprint)
                    {
                        item.blueprintTarget = BlueprintTarget;
                        return item;
                    }

                    item.fuel = Fuel;
                    item.condition = Condition;

                    if (MaxCondition != -1)
                        item.maxCondition = MaxCondition;

                    if (Contents != null)
                    {
                        if (Contents.Count > 0)
                        {
                            if (item.contents == null)
                            {
                                item.contents = new ItemContainer();
                                item.contents.ServerInitialize(null, Contents.Count);
                                item.contents.GiveUID();
                                item.contents.parent = item;
                            }
                            foreach (var contentItem in Contents)
                                contentItem.ToItem()?.MoveToContainer(item.contents);
                        }
                    }
                    else
                        item.contents = null;

                    BaseProjectile.Magazine magazine = item.GetHeldEntity()?.GetComponent<BaseProjectile>()?.primaryMagazine;
                    FlameThrower flameThrower = item.GetHeldEntity()?.GetComponent<FlameThrower>();

                    if (magazine != null)
                    {
                        magazine.contents = Ammo;
                        magazine.ammoType = ItemManager.FindItemDefinition(AmmoType);
                    }

                    if (flameThrower != null)
                        flameThrower.ammo = FlameFuel;

                    item.text = Text;

                    if (Name != null && !string.IsNullOrEmpty(Name))
                        item.name = Name;

                    return item;
                }

                public static ItemData FromItem(Item item) => new ItemData
                {
                    Shortname = item.info.shortname,
                    Ammo = item.GetHeldEntity()?.GetComponent<BaseProjectile>()?.primaryMagazine?.contents ?? 0,
                    AmmoType = item.GetHeldEntity()?.GetComponent<BaseProjectile>()?.primaryMagazine?.ammoType?.itemid ?? 0,
                    Amount = item.amount,
                    Condition = item.condition,
                    MaxCondition = item.maxCondition,
                    Fuel = item.fuel,
                    Skin = item.skin,
                    Contents = ItemData.GetContents(item),
                    FlameFuel = item.GetHeldEntity()?.GetComponent<FlameThrower>()?.ammo ?? 0,
                    IsBlueprint = item.IsBlueprint(),
                    BlueprintTarget = item.blueprintTarget,
                    Name = item.name,
                    Text = item.text
                };

                public static List<ItemData> GetContents(Item item)
                {
                    List<ItemData> items = new List<ItemData>();

                    if(item.contents == null || item.contents?.itemList == null) return items;

                    foreach(var contentItem in item.contents.itemList)
                    {
                        items.Add(ItemData.FromItem(contentItem));
                    }

                    return items;
                }
            }

            private void Start()
            {
                if(animalSetup.functionsSetup.canAutoCollect) mode = bot.gameObject.AddComponent<AnimalAutoMode>();
                if(animalSetup.nutritionSetup.enableFeeding) SpawnFeedingTrigger();

                var brain = bot.GetComponent<BaseAIBrain>();

                _config = plugin.Config.ReadObject<Configuration>();
                
                _lastTimeGUI = Time.realtimeSinceStartup;
                _lastTimeText = Time.realtimeSinceStartup;

                _isFollowPlayer = true;
                _navigationSpeed = (BaseNavigator.NavigationSpeed)Enum.Parse(typeof(BaseNavigator.NavigationSpeed), System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(animalSetup.speed));

                if(animalSetup.addonsSetup.needBag) _tipText += GetMsg("Text_Inventory") + "\n";
                if(animalSetup.addonsSetup.needChair) _tipText += GetMsg("Text_Drive") + "\n";

                RenderMenu(true);

                StartCoroutine(Timer(() =>
                {
                    _navigator = brain.Navigator;

                    brain.CancelInvoke(new Action(brain.TickMovement));
                    brain.states = new Dictionary<AIState, BaseAIBrain.BasicAIState>();

                    _navigator.StoppingDistance = animalSetup.stoppingDistance;
                    _navigator.Agent.stoppingDistance = animalSetup.stoppingDistance;
                }, 0.1f, true));

                StartCoroutine(Timer(() =>
                {
                    if(_storage)
                    {
                        if(owner?.inventory.loot.containers.Count != 0)
                        {
                            var container = owner.inventory.loot.containers[0];

                            if(_storage.inventory == container)
                            {
                                if(Vector3.Distance(bot.transform.position, owner.transform.position) > 5)
                                {
                                    owner.EndLooting();
                                }
                            }
                        }
                    }
                }, 1f, false));

                StartCoroutine(Timer(() =>
                {
                    if(owner.serverInput != null) OnPlayerInput(owner, owner.serverInput);
                }, _config.controlsSetup.inputTick, false));

                StartCoroutine(Timer(() =>
                {
                    RefreshMenu();
                }, _config.gui.refreshTime, false));

                StartCoroutine(Timer(() =>
                {
                    bot.CurrentBehaviour = BaseNpc.Behaviour.Sleep;

                    if(IsDriving)
                    {
                        if(_chair)
                        {
                            if(_chair.GetMounted() == null)
                            {
                                _chair.Kill();
                                IsDriving = false;
                            }
                            else 
                            {
                                if(owner.IsWounded())
                                {
                                    _chair.Kill();
                                    IsDriving = false;
                                }
                            }
                        }
                    }
                    else 
                    {
                        if(Vector3.Distance(owner.transform.position, bot.transform.position) < 2.5f)
                        {
                            ShowText();
                        }
                    }

                    if(_collectibleTarget != null)
                    {
                        if(!_collectibleTarget.IsDestroyed)
                        {
                            SetDestination(_collectibleTarget.transform.position);

                            if(Vector3.Distance(bot.transform.position, _collectibleTarget.transform.position) < animalSetup.interactionSetup.collectibleDistance)
                            {
                                if (_collectibleTarget.itemList != null)
                                {
                                    foreach (ItemAmount itemAmount in _collectibleTarget.itemList)
                                    {
                                        if(itemAmount.amount <= 0) 
                                        {
                                            itemAmount.amount = 1;
                                            Debug.LogWarning("[PersonalAnimal] Item amount was forced from 0 to 1!");
                                        }

                                        Item obj = ItemManager.Create(itemAmount.itemDef, (int) itemAmount.amount);

                                        if(animalSetup.interactionSetup.collectRates.ContainsKey(obj.info.shortname))
                                        {
                                            obj.amount = (int)(obj.amount * animalSetup.interactionSetup.collectRates[obj.info.shortname]);
                                        }

                                        if (obj != null) CollectItem(obj);
                                    }

                                    if (_collectibleTarget.pickupEffect.isValid) Effect.server.Run(_collectibleTarget.pickupEffect.resourcePath, this.transform.position, this.transform.up);
                                    _collectibleTarget.Kill();

                                    if(!mode.IsDisabled()) mode.lastTimePickup = Time.realtimeSinceStartup;
                                }
                            }
                            else if(mode.lastTimePickup + 10 < Time.realtimeSinceStartup && !mode.IsDisabled()) 
                            {
                                mode.lastTimePickup = Time.realtimeSinceStartup;
                                mode.AddIgnore(_collectibleTarget.net.ID.Value);
                            }
                        }
                        else _collectibleTarget = null;
                    }

                    if(_containerTarget != null)
                    {
                        if(!_containerTarget.IsDestroyed)
                        {
                            if(Vector3.Distance(bot.transform.position, _containerTarget.transform.position) < animalSetup.interactionSetup.collectibleDistance)
                            {
                                if(_containerTarget.LootSpawnSlots.Length != 0 || _config.lootEntities.Contains(_containerTarget.ShortPrefabName))
                                {
                                    SetDestination(bot.transform.position);
                                                                
                                    for (int i = _containerTarget.inventory.itemList.Count - 1; i >= 0; i--)
                                    {
                                        var item = _containerTarget.inventory.itemList[i];

                                        CollectItem(item, animalSetup.interactionSetup.lootRates);
                                    }
                                    
                                    _containerTarget.Kill();
                                }
                            }
                        }
                        else _containerTarget = null;
                    }

                    if(_lastTarget) 
                    {
                        StartAttack();
                        SetDestination(_lastTarget.transform.position);
                    }

                    if(_lastTarget == null && _collectibleTarget == null && !_isFollowPlayer && _containerTarget == null && !_isIdle && !IsDriving)
                    {
                        if(mode != null && !mode.IsDisabled()) StartAutoMode();
                        else 
                        {
                            _isFollowPlayer = true;
                            _currentDestination = owner.transform.position;
            
                            SetDestination(_currentDestination);
                            SendMsg("Bot_Notice_MissionCompleted");
                        }
                    }
                }, _config.mainProcessTimer, false));
            }

            private void OnPlayerInput(BasePlayer player, InputState input)
            {
                if(input.IsDown(controlButton)) OnInput();

                if(IsDriving)
                {
                    if(input.IsDown(BUTTON.JUMP)) 
                    {
                        StopDriving();
                        return;
                    }

                    if(_config.controlsSetup.byDirection)
                    {
                        if(input.IsDown(BUTTON.FORWARD))
                        {
                            if(bot == null) return;
                            if(bot.transform == null) return;

                            Vector3 direction = (player.eyes.HeadRay().direction).normalized * 10;
                            Vector3 destination = bot.transform.position + new Vector3(direction.x, 0, direction.z);

                            DriveDestination = destination;

                            return;
                        }
                    }
                    else 
                    {
                        bool isClickedFwd = input.IsDown(BUTTON.FORWARD);

                        if(isClickedFwd)
                        {
                            if(bot == null) return;
                            if(bot.transform == null) return;

                            Vector3 direction = (bot.transform.forward).normalized * 10;
                            Vector3 destination = bot.transform.position + new Vector3(direction.x, 0, direction.z);

                            DriveDestination = destination;

                            if(input.IsDown(BUTTON.LEFT))
                            {
                                DriveDestination += (-transform.right) * 2f;
                            }

                            if(input.IsDown(BUTTON.RIGHT))
                            {
                                DriveDestination += (transform.right) * 2f;
                            }
                        }

                        return;
                    }
                }

                if(input.IsDown(inventoryButton)) 
                {
                    OpenBag();
                    return;
                }

                if(input.IsDown(mountButton)) 
                {
                    Drive();
                    return;
                }
            }

            private void SpawnFeedingTrigger()
            {
                GameObject feedingTrigger = new GameObject("Feeding Trigger", new Type[] 
                {
                    typeof(SphereCollider), typeof(AnimalFeedingTrigger)
                });
                
                feedingTrigger.transform.position = transform.position;
                feedingTrigger.transform.SetParent(bot.transform, false);

                feedingTrigger.transform.localPosition = animalSetup.GetFeedingOffset();
            }

            public virtual void Update()
            {
                if(bot == null || owner == null)
                {
                    Destroy(this);
                    return;
                }

                if(!owner.IsConnected)
                {
                    Destroy(this);
                    return;
                }

                if(IsDriving) SetDestination(DriveDestination);
                else 
                {
                    if(_isIdle) SetDestination(_currentDestination);
                    else 
                    {
                        if(_isFollowPlayer) 
                        {
                            if(Vector3.Distance(owner.transform.position, bot.transform.position) > _config.controlsSetup.idleDistance)
                            {
                                SetDestination(new Vector3(owner.transform.position.x - 1f, owner.transform.position.y, owner.transform.position.z));
                            }
                        }
                        else if(bot.Destination != _currentDestination && _currentDestination != new Vector3() && Vector3.Distance(_currentDestination, bot.transform.position) > 1f) SetDestination(_currentDestination);
                    }
                }
            }

            private void OnDestroy() 
            {
                if(owner != null) 
                {
                    CuiHelper.DestroyUi(owner, "PersonalAnimal_ControlPanel");
                
                    if(_storage)
                    {
                        if(owner?.inventory.loot.containers.Count != 0)
                        {
                            var container = owner.inventory.loot.containers[0];

                            if(_storage.inventory == container) owner.EndLooting();
                        }
                    }

                    if(animalSetup.deathMarker.enableMarker && bot != null)
                    {
                        DeathMarker marker = new GameObject("Animal Death Marker", typeof(DeathMarker)).GetComponent<DeathMarker>();

                        marker.displayName = animalSetup.deathMarker.displayName;
                        marker.radius = animalSetup.deathMarker.radius;
                        marker.alpha = animalSetup.deathMarker.alpha;
                        marker.refreshRate = 3f;
                        marker.position = bot.transform.position;
                        marker.duration = animalSetup.deathMarker.duration;
                        marker.player = owner;

                        ColorUtility.TryParseHtmlString($"#{animalSetup.deathMarker.main}", out marker.color1);
                        ColorUtility.TryParseHtmlString($"#{animalSetup.deathMarker.outline}", out marker.color2);
                    }
                }

                if(_chair != null) 
                {
                    var mounted = _chair.GetMounted();

                    if(mounted) _chair.DismountPlayer(mounted, true);

                    _chair.Kill();
                }

                if(mode != null) Destroy(mode);

                if(bot != null)
                {
                    if(!bot.IsDestroyed) 
                    {
                        plugin.CallHook("OnEntityDeath", bot.ToPlayer(), null);
                    }
                }
            }

            public void RenderHierarchy()
            {
                if(_isViewingHierarchy || IsGUIHidden)
                {
                    if(!IsGUIHidden) _isViewingHierarchy = false;

                    CuiHelper.DestroyUi(owner, "CPA_Open_Icon");
                    CuiHelper.DestroyUi(owner, "CPA_Hierarchy");

                    CuiHelper.AddUi(owner, new List<CuiElement>
                    {
                        new CuiElement 
                        {
                            Name = "CPA_Open_Icon", Parent = "CPA_Open",
                            Components = 
                            {
                                new CuiRawImageComponent
                                {
                                    Png = cachedImages["open"]
                                },

                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0", AnchorMax = "1 1",
                                    OffsetMin = "5 2", OffsetMax = "-5 -2"
                                }
                            }
                        }
                    });

                    return;
                }

                CuiElementContainer container = new CuiElementContainer();

                container.Add(new CuiElement 
                {
                    Name = "CPA_Hierarchy", Parent = "PersonalAnimal_ControlPanel",

                    Components = 
                    {
                        new CuiImageComponent {Color = "0 0 0 0"},
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 0", AnchorMax = "0.5 0",
                            OffsetMin = "-80 0", OffsetMax = "80 0"
                        }
                    }
                });

                for(int i = 0; i < _config.gui.accessButtons.Count; i++)
                {
                    var button = _config.gui.accessButtons[i];
                    
                    container.Add(new CuiElement 
                    {
                        Name = $"CPA_Hierarchy_Element{i}", Parent = "CPA_Hierarchy",
                        Components = 
                        {
                            new CuiButtonComponent 
                            {
                                Command = $"panimal {i}",
                                Color = HexToRustFormat(_config.gui.panelColor2),
                                Sprite = "assets/content/ui/ui.background.tile.psd",
                            },

                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                                OffsetMin = $"-80 {-25 * (i + 1)}", OffsetMax = $"80 {-5 - (25 * i)}"
                            }
                        }
                    });

                    container.Add(new CuiElement 
                    {
                        Name = $"CPA_Hierarchy_Element{i}_Text", Parent = $"CPA_Hierarchy_Element{i}",
                        Components = 
                        {
                            new CuiTextComponent 
                            {
                                Text = button.text,
                                Align = TextAnchor.MiddleCenter,
                                FontSize = 13
                            },

                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0", AnchorMax = "1 1"
                            }
                        }
                    });
                }

                _isViewingHierarchy = true;

                container.Add(new CuiElement 
                {
                    Name = "CPA_Open_Icon", Parent = "CPA_Open",
                    Components = 
                    {
                        new CuiRawImageComponent
                        {
                            Png = cachedImages["close"]
                        },

                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "1 1",
                            OffsetMin = "5 2", OffsetMax = "-5 -2"
                        }
                    }
                });

                CuiHelper.DestroyUi(owner, "CPA_Open_Icon");
                CuiHelper.AddUi(owner, container);
            }

            public void RenderMenu(bool ignoreLastTime = false, CuiRectTransformComponent nextPosition = null)
            {
                if(_lastTimeGUI + 1 > UnityEngine.Time.realtimeSinceStartup && !ignoreLastTime) return;
                else _lastTimeGUI = UnityEngine.Time.realtimeSinceStartup;

                CuiHelper.DestroyUi(owner, "PersonalAnimal_ControlPanel");

                CuiElementContainer container = new CuiElementContainer();
                CuiRectTransformComponent panelPos = _config.gui.panelPosition;

                if(nextPosition != null) 
                {
                    panelPos = nextPosition;
                    _lastGUIPosition = panelPos;
                }
                else 
                {
                    if(npcPlugin && npcPlugin.Call<bool>("HasBot", owner)) 
                    {
                        panelPos = _config.gui.secondPanelPosition;
                        _lastGUIPosition = panelPos;
                    }
                    
                    _lastGUIPosition = panelPos;
                }

                container.Add(new CuiElement 
                {
                    Name = "PersonalAnimal_ControlPanel",
                    Parent = _config.gui.layer,

                    Components = 
                    {
                        new CuiImageComponent
                        {
                            Sprite = "assets/content/ui/ui.background.tile.psd",
                            Color = HexToRustFormat(_config.gui.panelColor1)
                        },

                        IsGUIHidden ? new CuiRectTransformComponent
                        {
                            AnchorMin = panelPos.AnchorMin, AnchorMax = panelPos.AnchorMax,

                            OffsetMin = $"{panelPos.OffsetMax.Split(' ')[0]} {panelPos.OffsetMin.Split(' ')[1]}",
                            OffsetMax = panelPos.OffsetMax
                        } : panelPos
                    }
                });

                container.Add(new CuiElement()
                {
                    Name = "CP_HideButton",
                    Parent = "PersonalAnimal_ControlPanel",

                    Components = 
                    {
                        new CuiButtonComponent
                        {
                            Color = HexToRustFormat(_config.gui.panelColor2),
                            Command = "panimal hide_panel",
                            Material = "assets/content/ui/ui.background.tile.psd",
                        },

                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = "-25 -25", OffsetMax = "-5 0"
                        }
                    }
                });

                container.Add(new CuiElement()
                {
                    Name = "CP_HideButton_Text",
                    Parent = "CP_HideButton",

                    Components = 
                    {
                        new CuiTextComponent
                        {
                            FontSize = 20,
                            Text = IsGUIHidden ? "<" : ">",
                            Align = TextAnchor.MiddleCenter
                        },

                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "1 1"
                        }
                    }
                });
                
                if(IsGUIHidden)
                {
                    CuiHelper.AddUi(owner, container);

                    return;
                }

                container.Add(new CuiElement 
                {
                    Name = "CPA_Header",
                    Parent = "PersonalAnimal_ControlPanel",

                    Components = 
                    {
                        new CuiImageComponent 
                        {
                            Color = HexToRustFormat(_config.gui.panelColor2),
                            Sprite = "assets/content/ui/ui.background.tile.psd"
                        },

                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                            OffsetMin = "-78 -20", OffsetMax = "78 -2"
                        }
                    }
                });

                container.Add(new CuiElement 
                {
                    Name = "CPA_Header_Text",
                    Parent = "CPA_Header",

                    Components = 
                    {
                        new CuiTextComponent
                        {
                            FontSize = 14,
                            Text = GetMsg("GUI_Header"),
                            Align = TextAnchor.MiddleCenter
                        },

                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "1 1"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Name = "CPA_HealthBar",
                    Parent = "PersonalAnimal_ControlPanel",

                    Components = 
                    {
                        new CuiImageComponent
                        {
                            Color = HexToRustFormat("#C5C5C5FF"), Sprite = "assets/content/ui/ui.background.tile.psd"
                        },

                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                            OffsetMin = "-53 -48", OffsetMax = "78 -22"  
                        }
                    } 
                });

                container.Add(new CuiElement 
                {
                    Name = "CPA_HealthBar_Fill",
                    Parent = "CPA_HealthBar",

                    Components = 
                    {
                        new CuiImageComponent
                        {
                            Color = HexToRustFormat(_config.gui.panelHealthColor),
                            Sprite = "assets/content/ui/ui.background.tile.psd"
                        },

                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = $"{bot.Health() / bot.MaxHealth()} 1",
                            OffsetMax = "0 -0.001"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Name = "CPA_HealthBar_Text",
                    Parent = "CPA_HealthBar",
                    
                    Components = 
                    {
                        new CuiTextComponent
                        {
                            Text = $"{Mathf.RoundToInt(bot.Health())}",
                            Align = TextAnchor.MiddleCenter
                        },

                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "1 1",
                            OffsetMin = "-15 0", OffsetMax = "0 0"
                        }
                    }
                });

                container.Add(new CuiElement 
                {
                    Name = "CPA_Status_Bg",
                    Parent = "PersonalAnimal_ControlPanel",

                    Components = 
                    {
                        new CuiImageComponent
                        {
                            Sprite = "assets/content/ui/ui.background.tile.psd",
                            Color = HexToRustFormat(_config.gui.panelColor2)
                        },

                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                            OffsetMin = "-78 -48", OffsetMax = "-53 -22"
                        }
                    }
                });

                container.Add(new CuiElement 
                {
                    Name = "CPA_Status_Icon",
                    Parent = "CPA_Status_Bg",

                    Components = 
                    {
                        new CuiImageComponent 
                        {
                            Color = "1 1 1 1",
                            Sprite = "assets/icons/health.png"
                        },

                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "1 1",
                            OffsetMin = "3 3", OffsetMax = "-3 -3"
                        }
                    }
                });

                container.Add(new CuiElement 
                {
                    Name = "CPA_InfoPosition",
                    Parent = "PersonalAnimal_ControlPanel",
                    
                    Components = 
                    {
                        new CuiImageComponent
                        {
                            Color = HexToRustFormat(_config.gui.panelColor2), Sprite = "assets/content/ui/ui.background.tile.psd",
                        },

                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                            OffsetMin = "-78 -70", OffsetMax = "78 -50"
                        }
                    }
                });

                container.Add(new CuiElement 
                {
                    Name = "CPA_Location_Text",
                    Parent = "CPA_InfoPosition",

                    Components = 
                    {
                        new CuiTextComponent 
                        {
                            Text = $"{PersonalAnimal.GetGrid(bot.transform.position)}: {Mathf.RoundToInt(Vector3.Distance(bot.transform.position, owner.transform.position))}m",
                            FontSize = 14,
                            Align = TextAnchor.MiddleCenter
                        },

                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "1 1"
                        }
                    }
                });

                container.Add(new CuiElement 
                {
                    Name = "CPA_Follow", Parent = "PersonalAnimal_ControlPanel",

                    Components = 
                    {
                        new CuiButtonComponent
                        {
                            Color = HexToRustFormat(_config.gui.panelColor2),
                            Command = "panimal follow",
                            Sprite = "assets/content/ui/ui.background.tile.psd",
                        },

                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 0", AnchorMax = "0.5 0",
                            OffsetMin = "-78 2", OffsetMax = "-5 22"
                        }
                    }
                });

                container.Add(new CuiElement 
                {
                    Name = "CPA_Follow_Text", Parent = "CPA_Follow",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = GetMsg("GUI_Follow"),
                            Align = TextAnchor.MiddleCenter,
                            FontSize = 12
                        },

                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "1 1"
                        }
                    }
                });

                container.Add(new CuiElement 
                {
                    Name = "CPA_Kill", Parent = "PersonalAnimal_ControlPanel",
                    Components = 
                    {
                        new CuiButtonComponent
                        {
                            Color = HexToRustFormat("#F02424FF"),
                            Command = "chat.say /panimal",
                            Sprite = "assets/content/ui/ui.background.tile.psd",
                        },

                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 0", AnchorMax = "0.5 0",
                            OffsetMin = "-3 2", OffsetMax = "40 22"
                        }
                    }
                });

                container.Add(new CuiElement 
                {
                    Name = "CPA_Kill_Text", Parent = "CPA_Kill",
                    Components = 
                    {
                        new CuiTextComponent
                        {
                            Text = GetMsg("GUI_Kill"),
                            Align = TextAnchor.MiddleCenter,
                            FontSize = 12,
                        },

                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "1 1"
                        }
                    }
                });

                container.Add(new CuiElement 
                {
                    Name = "CPA_Open", Parent = "PersonalAnimal_ControlPanel",
                    Components = 
                    {
                        new CuiButtonComponent
                        {
                            Color = HexToRustFormat(_config.gui.panelColor2),
                            Sprite = "assets/content/ui/ui.background.tile.psd",
                            Command = "panimal hierarchy"
                        },

                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 0", AnchorMax = "0.5 0",
                            OffsetMin = "42 2", OffsetMax = "78 22"
                        }
                    }
                });

                container.Add(new CuiElement 
                {
                    Name = "CPA_Open_Icon", Parent = "CPA_Open",
                    Components = 
                    {
                        new CuiRawImageComponent
                        {
                            Png = cachedImages["open"]
                        },

                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "1 1",
                            OffsetMin = "5 2", OffsetMax = "-5 -2"
                        }
                    }
                });

                CuiHelper.AddUi(owner, container);
            }

            private void RefreshMenu()
            {
                if(npcPlugin)
                {
                    bool hasBot = npcPlugin.Call<bool>("HasBot", owner);
                    CuiRectTransformComponent nextGUIPosition = null;

                    if(_lastGUIPosition == _config.gui.secondPanelPosition && !hasBot) nextGUIPosition = _config.gui.panelPosition;
                    else if(_lastGUIPosition == _config.gui.panelPosition && hasBot) nextGUIPosition = _config.gui.secondPanelPosition;

                    if(nextGUIPosition != _lastGUIPosition)
                    {
                        RenderMenu(true, nextGUIPosition);

                        return;
                    }
                }

                if(IsGUIHidden) return;

                CuiElementContainer container = new CuiElementContainer();

                CuiHelper.DestroyUi(owner, "CPA_HealthBar");
                CuiHelper.DestroyUi(owner, "CPA_Location_Text");

                container.Add(new CuiElement 
                {
                    Name = "CPA_Location_Text",
                    Parent = "CPA_InfoPosition",

                    Components = 
                    {
                        new CuiTextComponent 
                        {
                            Text = $"{PersonalAnimal.GetGrid(bot.transform.position)}: {Mathf.RoundToInt(Vector3.Distance(bot.transform.position, owner.transform.position))}m",
                            FontSize = 14,
                            Align = TextAnchor.MiddleCenter
                        },

                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "1 1"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Name = "CPA_HealthBar",
                    Parent = "PersonalAnimal_ControlPanel",

                    Components = 
                    {
                        new CuiImageComponent
                        {
                            Color = HexToRustFormat("#C5C5C5FF"), Sprite = "assets/content/ui/ui.background.tile.psd"
                        },

                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                            OffsetMin = "-53 -48", OffsetMax = "78 -22"  
                        }
                    } 
                });

                container.Add(new CuiElement 
                {
                    Name = "CPA_HealthBar_Fill",
                    Parent = "CPA_HealthBar",

                    Components = 
                    {
                        new CuiImageComponent
                        {
                            Color = HexToRustFormat(_config.gui.panelHealthColor),
                            Sprite = "assets/content/ui/ui.background.tile.psd"
                        },

                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = $"{bot.Health() / bot.MaxHealth()} 1",
                            OffsetMax = "0 -0.001"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Name = "CPA_HealthBar_Text",
                    Parent = "CPA_HealthBar",
                    
                    Components = 
                    {
                        new CuiTextComponent
                        {
                            Text = $"{Mathf.RoundToInt(bot.Health())}",
                            Align = TextAnchor.MiddleCenter
                        },

                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "1 1",
                            OffsetMin = "-15 0", OffsetMax = "0 0"
                        }
                    }
                });

                CuiHelper.AddUi(owner, container);
            }

            public void Idle()
            {
                _isFollowPlayer = false;
                _isIdle = true;
            }

            private string GetMsg(string key) => plugin.Call<string>("GetMsg", key, owner.UserIDString);

            public void OnAttacked(BaseEntity attacker, HitInfo info, bool ownerAttacked = false)
            {
                if(!(attacker is BaseCombatEntity)) return;
                if(IsDriving) return;

                if(attacker == owner) return;
                if(attacker == bot) return;

                if(ownerAttacked) 
                {
                    if(!animalSetup.functionsSetup.canProtectOwner)
                    {
                        return;
                    }
                }
                else 
                {
                    if(!animalSetup.functionsSetup.canProtectSelf)
                    {
                        return;
                    }
                }

                Nullify();

                _lastTarget = attacker as BaseCombatEntity;
                _currentDestination = _lastTarget.transform.position;
            }

            public virtual bool IsBlacklisted(Item item)
            {
                if(animalSetup.interactionSetup.bagBlacklist.Contains(item.info.shortname)) return true;

                return false;
            }

            private void CollectItem(Item item, Dictionary<string, float> rates = null)
            {
                if(rates != null)
                {
                    if(rates.ContainsKey(item.info.shortname))
                    {
                        item.amount = (int)(item.amount * rates[item.info.shortname]);
                    }
                }

                if(animalSetup.addonsSetup.needBag) 
                {
                    if(bagItems.Count >= animalSetup.addonsSetup.slotsAmount)
                    {
                        if(CheckSlots(item))
                        {
                            bagItems.Add(ItemData.FromItem(item));

                            return;
                        }
                    }
                    else 
                    {
                        bagItems.Add(ItemData.FromItem(item));

                        return;
                    }
                }
                
                item.Drop(bot.transform.position, Vector3.zero);
            }

            public void FollowPlayer()
            {
                Nullify();

                if(mode) mode.Disable();
                _isFollowPlayer = true;

                _currentDestination = owner.transform.position;
                SetDestination(_currentDestination);
            }

            public void SetDestination(Vector3 destination)
            {
                if(!_navigator) return;

                _currentDestination = destination;
                
                _navigator.Think(0.9f);
                _navigator.SetDestination(destination, _navigationSpeed);

                var direction = destination - bot.transform.position;
                if(direction == Vector3.zero) direction = new Vector3(0, 0, 1);

                bot.ServerRotation = Quaternion.LookRotation(direction);
            }

            private void Nullify()
            {
                _currentDestination = Vector3.zero;

                _collectibleTarget = null;
                _containerTarget = null;
                _lastTarget = null;

                _isIdle = false;
                _isFollowPlayer = false;
            }

            private void SendMsg(string key, string[] args = null) => plugin.Call<string>("SendMsg", owner, key, args);

            private bool StartAttack()
            {
                if (_lastTarget != null)
                {
                    if (Vector3.Distance(bot.transform.position, _lastTarget.transform.position) < animalSetup.interactionSetup.enemyDistance && _lastTimeAttack < Time.realtimeSinceStartup && !bot.InSafeZone() && !_lastTarget.InSafeZone())
                    {  
                        _lastTimeAttack = Time.realtimeSinceStartup + 1.75f;
                        bot.Attack(_lastTarget);
                        _lastTarget.Hurt(bot.AttackDamage, bot.AttackDamageType, bot, false);
                    }

                    return true;
                }

                return false;
            }

            public void UpdateItems(ItemContainer container)
            {
                bagItems = new List<ItemData>();

                foreach(var item in container.itemList) bagItems.Add(ItemData.FromItem(item));
                if(_storage) _storage.Kill();
            }

            public void OpenBag()
            {
                RaycastHit hit;

                if(IsDriving || !animalSetup.addonsSetup.needBag) return;

                if(Physics.Raycast(owner.eyes.HeadRay(), out hit, _config.controlsSetup.rayLength))
                {
                    var hitEnt = hit.GetEntity();

                    if(hitEnt == null) return;
                    if(hitEnt != bot) return;
                }
                else return;

                if(owner.inventory.loot != null)
                {
                    if(owner.inventory.loot.entitySource != null)
                    {
                        if(owner.inventory.loot.entitySource.name == $"panimal_{owner.userID}") return;
                    }
                }

                owner.EndLooting();
                
                StorageContainer storage = GameManager.server.CreateEntity("assets/prefabs/deployable/large wood storage/box.wooden.large.prefab", new Vector3(0f, -10f, 0f), Quaternion.identity) as StorageContainer;
                storage.name = $"panimal_{owner.userID}";

                storage.CreateInventory(true);
                storage.inventory.capacity = animalSetup.addonsSetup.slotsAmount;

                foreach(var bagItem in bagItems) 
                {
                    var bagItemConvert = bagItem.ToItem();
                    if(bagItemConvert == null) continue;

                    if(!bagItemConvert.MoveToContainer(storage.inventory)) storage.inventory.Insert(bagItemConvert);
                }

                storage.inventory.MarkDirty();
                storage.inventory.Save();

                storage.SendMessage("SetDeployedBy", owner, (SendMessageOptions)1);
                storage.Spawn();

                owner.inventory.loot.StartLootingEntity(storage, false);
                owner.inventory.loot.AddContainer(storage.inventory);
                owner.inventory.loot.SendImmediate();

                owner.ClientRPCPlayer(null, owner, "RPC_OpenLootPanel", "generic");

                storage.DecayTouch();
                storage.SendNetworkUpdate();

                _storage = storage;
            }

            public bool CheckSlots(Item item)
            {
                StorageContainer storage = GameManager.server.CreateEntity("assets/prefabs/deployable/large wood storage/box.wooden.large.prefab", new Vector3(0f, -10f, 0f), Quaternion.identity) as StorageContainer;

                storage.CreateInventory(true);
                storage.inventory.capacity = animalSetup.addonsSetup.slotsAmount;

                foreach(var bagItem in bagItems) 
                {
                    var convertedItem = bagItem.ToItem();

                    if(convertedItem != null)
                    {
                        convertedItem.MoveToContainer(storage.inventory);
                    }
                }

                bool result = item.MoveToContainer(storage.inventory);

                Destroy(storage.gameObject);

                return result;
            }

            public void Drive()
            {
                if(!animalSetup.addonsSetup.needChair) return;

                RaycastHit hit;

                if(Physics.Raycast(owner.eyes.HeadRay(), out hit, _config.controlsSetup.rayLength))
                {
                    var hitEnt = hit.GetEntity();

                    if(hitEnt == null) return;
                    if(hitEnt != bot) return;
                }
                else return;

                if(!IsDriving)
                {
                    var entityChair = GameManager.server.CreateEntity("assets/prefabs/deployable/chair/chair.deployed.prefab", Vector3.zero, Quaternion.identity, false) as BaseMountable;
                    
                    entityChair.enableSaving = false;
                    entityChair.isMobile = true;
                    entityChair.pickup.enabled = false;
                    entityChair.maxMountDistance = 1.5f;
                    entityChair.skinID = 1169930802;

                    entityChair.SetParent(bot, false, true);
                    entityChair.transform.localPosition = animalSetup.GetChairOffset();

                    entityChair.Spawn();
                    entityChair.isMobile = true;

                    Destroy(entityChair.GetComponent<DestroyOnGroundMissing>());
                    Destroy(entityChair.GetComponent<GroundWatch>());

                    entityChair.gameObject.AddComponent<AnimalOwnerComponent>();

                    entityChair._limitedNetworking = true;
                    entityChair.syncPosition = false;

                    _chair = entityChair;

                    entityChair.MountPlayer(owner);

                    IsDriving = true;
                }
            }

            public void StopDriving() 
            {
                if(_chair)
                {
                    _chair.Kill();
                }

                IsDriving = false;
                FollowPlayer();
            }

            public void OnInput()
            {
                RaycastHit hit;

                if(IsDriving || owner.inventory.loot.entitySource != null) return;

                if(Physics.Raycast(owner.eyes.HeadRay(), out hit, _config.controlsSetup.rayLength))
                {
                    var hitEnt = hit.GetEntity();
                    
                    if(hitEnt != null) 
                    {
                        if(hitEnt.ShortPrefabName.Contains("junkpile")) return;

                        if(hitEnt is CollectibleEntity)
                        {
                            if(!animalSetup.functionsSetup.canCollectResources)
                            {
                                SendMsg("ChatCommand_Error_CannotUse");

                                return;
                            }

                            ShowArrow(hit.point);
                            Nullify();

                            _currentDestination = hitEnt.transform.position;
                            SetDestination(_currentDestination);
                            _collectibleTarget = hitEnt as CollectibleEntity;

                            SendMsg("Bot_Notice_GoingCollect");

                            return;
                        }

                        if(hitEnt is LootContainer)
                        {
                            if(!animalSetup.functionsSetup.canLootBoxes)
                            {
                                SendMsg("ChatCommand_Error_CannotUse");

                                return;
                            }

                            var container = hitEnt as LootContainer;

                            if(container.isLootable || _config.lootEntities.Contains(container.ShortPrefabName))
                            {
                                ShowArrow(hitEnt.transform.position);
                                Nullify();

                                _currentDestination = hitEnt.transform.position;
                                SetDestination(_currentDestination);

                                _containerTarget = container;

                                SendMsg("Bot_Notice_GoingLootBox");

                                return;
                            }
                        }

                        if(hitEnt is BaseCombatEntity)
                        {
                            if(!hitEnt.IsDestroyed && hitEnt.Health() > 1f)
                            {
                                if(hitEnt == bot)
                                {
                                    if(!_isFollowPlayer)
                                    {
                                        FollowPlayer();
                                        SendMsg("Bot_Notice_Following");
                                    }
                                    else 
                                    {
                                        Idle();
                                        SendMsg("Bot_Notice_Staying");
                                    }

                                    return;
                                }
                                
                                if(animalSetup.functionsSetup.canAttackEntities)
                                {
                                    if(!animalSetup.functionsSetup.canAttackNPC || !animalSetup.functionsSetup.canAttackPlayers)
                                    {
                                        if(hitEnt is BasePlayer)
                                        {
                                            var hitPlayer = hitEnt.ToPlayer();

                                            if(hitPlayer != null)
                                            {
                                                if(!hitPlayer.userID.IsSteamId() && !animalSetup.functionsSetup.canAttackNPC) return;
                                                if(hitPlayer.userID.IsSteamId() && !animalSetup.functionsSetup.canAttackPlayers) return;
                                            }
                                        }
                                    }

                                    if(!animalSetup.functionsSetup.canAttackTeam)
                                    {
                                        if(hitEnt is BasePlayer)
                                        {
                                            var hitPlayer = hitEnt.ToPlayer();

                                            if(hitPlayer != null)
                                            {
                                                if(owner.Team != null)
                                                {
                                                    if(owner.Team.members.Contains(hitPlayer.userID))
                                                    {
                                                        return;
                                                    }
                                                }

                                                if(pluginInstance.Friends != null) 
                                                {
                                                    var req = pluginInstance.Friends?.Call("IsFriend", hitPlayer.userID, owner.userID);

                                                    if(req is bool) 
                                                    {
                                                        if(((bool)req) == true) return;
                                                    }
                                                }

                                                if(pluginInstance.Clans != null) 
                                                {
                                                    var req = pluginInstance.Clans?.Call("IsClanMember", hitPlayer.userID.ToString(), owner.userID.ToString());

                                                    if(req is bool) 
                                                    {
                                                        if(((bool)req) == true) return;
                                                    }
                                                }
                                            }
                                        }
                                    }

                                    ShowArrow(hitEnt.transform.position);
                                    Nullify();

                                    _lastTarget = hitEnt as BaseCombatEntity;

                                    _isFollowPlayer = false;
                                    _isIdle = false;

                                    SetDestination(hitEnt.transform.position);

                                    if(StartAttack() == false)
                                    {
                                        _lastTarget = null;
                                        _isFollowPlayer = true;

                                        return;
                                    }

                                    SendMsg("Bot_Notice_StartedAttack");
                                }
                            }
                        }
                    }
                    else 
                    {
                        ShowArrow(hit.point);
                        Nullify();

                        _currentDestination = hit.point;
                        SetDestination(_currentDestination);

                        _isIdle = true;

                        SendMsg("Bot_Notice_GoingPosition");
                    }
                }
            }

            public void ShowArrow(Vector3 pos, Color color = new Color())
            {
                if(!_config.controlsSetup.showArrow) return;

                if(color == new Color()) color = Color.white;

                if(!owner.IsAdmin) 
                {
                    owner.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                    owner.SendNetworkUpdateImmediate();

                    owner.SendConsoleCommand("ddraw.arrow", _config.controlsSetup.arrowDuration, Color.black, pos + new Vector3(0f, pos.y + 5), pos, 1.5f);
                   
                    owner.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                    owner.SendNetworkUpdateImmediate();
                }
                else 
                {
                    owner.SendConsoleCommand("ddraw.arrow", _config.controlsSetup.arrowDuration, Color.black, pos + new Vector3(0f, pos.y + 5), pos, 1.5f);
                }
            }

            public void ShowText()
            {
                if(!animalSetup.addonsSetup.needBag && !animalSetup.addonsSetup.needChair) return;
                if(_lastTimeText + 1 > Time.realtimeSinceStartup) return;

                _lastTimeText = Time.realtimeSinceStartup;

                if(!owner.IsAdmin) 
                {
                    owner.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                    owner.SendNetworkUpdateImmediate();

                    owner.SendConsoleCommand("ddraw.text", 1f, Color.white, bot.transform.position, _tipText);
                   
                    owner.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                    owner.SendNetworkUpdateImmediate();
                }
                else 
                {
                    owner.SendConsoleCommand("ddraw.text", 1f, Color.white, bot.transform.position, _tipText);
                }
            }

            private void StartCollect(CollectibleEntity[] array)
            {
                Nullify();

                SendMsg("Bot_Notice_GoingCollect");
                var resource = array[0];

                _currentDestination = resource.transform.position;
                SetDestination(_currentDestination);
                _collectibleTarget = resource;
            }

            public bool StartAutoMode()
            {
                if(mode.IsDisabled()) return false;

                var modeType = mode.GetMode();
                    var resources = mode.GetResources();

                    var woodCollectibles = mode.GetPickupResourcesInRadius(true);
                    var stoneCollectibles = (CollectibleEntity[])null;
                    var sulfurCollectibles = (CollectibleEntity[])null;
                    var metalCollectibles = (CollectibleEntity[])null;
                    var hempCollectibles = (CollectibleEntity[])null;
                    var cornCollectibles = (CollectibleEntity[])null;
                    var mushroomCollectibles = (CollectibleEntity[])null;
                    var pumpkinCollectibles = (CollectibleEntity[])null;
                    var berriesCollectibles = (CollectibleEntity[])null;

                    if(_collectibleTarget != null)
                    {
                        stoneCollectibles = mode.GetPickupResourcesInRadius(false, false, false, true);
                        sulfurCollectibles = mode.GetPickupResourcesInRadius(false, false, true);
                        metalCollectibles = mode.GetPickupResourcesInRadius(false, true);
                        hempCollectibles = mode.GetPickupResourcesInRadius(false, false, false, false, true);
                        cornCollectibles = mode.GetPickupResourcesInRadius(false, false, false, false, false, true);
                        mushroomCollectibles = mode.GetPickupResourcesInRadius(false, false, false, false, false, false, true);
                        pumpkinCollectibles = mode.GetPickupResourcesInRadius(false, false, false, false, false, false, false, true);
                        berriesCollectibles = mode.GetPickupResourcesInRadius(false, false, false, false, false, false, false, false, true);

                        if(woodCollectibles.Contains(_collectibleTarget) 
                            || stoneCollectibles.Contains(_collectibleTarget) 
                                || sulfurCollectibles.Contains(_collectibleTarget) 
                                    || metalCollectibles.Contains(_collectibleTarget) 
                                        || hempCollectibles.Contains(_collectibleTarget) 
                                            || cornCollectibles.Contains(_collectibleTarget)
                                                || mushroomCollectibles.Contains(_collectibleTarget)
                                                    || pumpkinCollectibles.Contains(_collectibleTarget)
                                                        || berriesCollectibles.Contains(_collectibleTarget)) return true;
                    }

                    if(resources.Contains("Wood") && woodCollectibles.Length != 0)
                    {
                        StartCollect(woodCollectibles);
                        return true;
                    }

                    stoneCollectibles = mode.GetPickupResourcesInRadius(false, false, false, true);

                    if(resources.Contains("Stone") && stoneCollectibles.Length != 0)
                    {
                        StartCollect(stoneCollectibles);
                        return true;
                    }

                    sulfurCollectibles = mode.GetPickupResourcesInRadius(false, false, true);

                    if(resources.Contains("Sulfur") && sulfurCollectibles.Length != 0)
                    {
                        StartCollect(sulfurCollectibles);
                        return true;
                    }

                    metalCollectibles = mode.GetPickupResourcesInRadius(false, true);

                    if(resources.Contains("Metal") && metalCollectibles.Length != 0)
                    {
                        StartCollect(metalCollectibles);
                        return true;
                    }

                    hempCollectibles = mode.GetPickupResourcesInRadius(false, false, false, false, true);

                    if(resources.Contains("Hemp") && hempCollectibles.Length != 0)
                    {
                        StartCollect(hempCollectibles);
                        return true;
                    }

                    cornCollectibles = mode.GetPickupResourcesInRadius(false, false, false, false, false, true);

                    if(resources.Contains("Corn") && cornCollectibles.Length != 0)
                    {
                        StartCollect(cornCollectibles);
                        return true;
                    }

                    mushroomCollectibles = mode.GetPickupResourcesInRadius(false, false, false, false, false, false, true);

                    if(resources.Contains("Mushroom") && mushroomCollectibles.Length != 0)
                    {
                        StartCollect(mushroomCollectibles);
                        return true;
                    }

                    pumpkinCollectibles = mode.GetPickupResourcesInRadius(false, false, false, false, false, false, false, true);

                    if(resources.Contains("Pumpkin") && pumpkinCollectibles.Length != 0)
                    {
                        StartCollect(pumpkinCollectibles);
                        return true;
                    }

                    berriesCollectibles = mode.GetPickupResourcesInRadius(false, false, false, false, false, false, false, false, true);

                    if(resources.Contains("Berries") && berriesCollectibles.Length != 0)
                    {
                        StartCollect(berriesCollectibles);
                        return true;
                    }

                if(mode.lastTimeStarted + 2 > UnityEngine.Time.realtimeSinceStartup)
                {                    
                    StartCoroutine(NextTick(delegate()
                    {
                        SendMsg("Bot_Error_NoResourcesAround");
                    }));

                    mode.Disable();
                }
                else 
                {
                    mode.Disable();
                }

                _isFollowPlayer = true;

                return false;
            }

            private IEnumerator NextTick(Action action)
            {
                for(;;)
                {
                    yield return CoroutineEx.waitForEndOfFrame;
                    action();

                    break;
                }
            }

            private IEnumerator Timer(Action action, float time, bool once)
            {
                for(;;)
                {
                    yield return CoroutineEx.waitForSeconds(time);
                    action();

                    if(once) break;
                }
            }

            public IEnumerator UpdateAutoMode()
            {
                for(;;)
                {
                    yield return CoroutineEx.waitForSeconds(5f);
                    
                    if(!StartAutoMode())
                    {
                        break;
                    }
                }
            }

        }

        public class DeathMarker : MonoBehaviour
        {
            private const string genericPrefab = "assets/prefabs/tools/map/genericradiusmarker.prefab";
            private const string vendingPrefab = "assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab";
        
            private VendingMachineMapMarker vending;
            private MapMarkerGenericRadius generic;

            public float radius, alpha, refreshRate;
            public Color color1, color2;
            public string displayName;
            public Vector3 position;
            public int duration;

            public BasePlayer player;

            private void Start()
            {
                transform.position = position;

                vending = GameManager.server.CreateEntity(vendingPrefab, position).GetComponent<VendingMachineMapMarker>();
                vending.markerShopName = displayName;
                vending.enableSaving = false;
                vending.limitNetworking = true;
                vending.Spawn();

                generic = GameManager.server.CreateEntity(genericPrefab).GetComponent<MapMarkerGenericRadius>();
                generic.color1 = color1;
                generic.color2 = color2;
                generic.radius = radius;
                generic.alpha = alpha;
                generic.enableSaving = false;
                generic.limitNetworking = true;
                generic.SetParent(vending);
                generic.Spawn();

                if (duration != 0) Invoke(nameof(DestroyMakers), duration);
                if (refreshRate > 0f) InvokeRepeating(nameof(UpdateMarkers), refreshRate, refreshRate);

                vending.SendAsSnapshot(player.Connection, true);
                generic.SendAsSnapshot(player.Connection, true);

                UpdateMarkers();
            }

            public void UpdateMarkers()
            {
                vending.SendNetworkUpdate();
                generic.SendUpdate();
            }

            private void DestroyMakers()
            {
                if (vending.IsValid()) vending.Kill();
                if (generic.IsValid()) generic.Kill();

                Destroy(gameObject);
            }

            private void OnDestroy() 
            {
                if (vending.IsValid()) vending.Kill();
                if (generic.IsValid()) generic.Kill();
            }
        }

        #endregion

        #region API

        private bool IsPersonalAnimal(BaseEntity entity) => _existsOwnerComponents.ContainsKey(entity.net.ID.Value); 
        private bool HasBot(BasePlayer player) => _existsControllers.ContainsKey(player.net.ID.Value) ? _existsControllers[player.net.ID.Value] != null : false;

        private bool CreatePersonalAnimal(BasePlayer player, string spawnName, bool checkForCooldown, bool enableSpawnChecks)
        {
            Configuration.AnimalInfo info = null;

            foreach(var value in _config.animalInfoPerm.Values)
            {
                if(value.spawnName == spawnName)
                {
                    info = value;
                    break;
                }
            }

            if(info == null) return false;

            PlayerAnimalController comp;

            if(player.TryGetComponent<PlayerAnimalController>(out comp)) 
            {
                if(comp.ItemInfo.returnDespawn)
                {
                    Item panimal = ItemManager.CreateByName(comp.ItemInfo.shortname, 1, comp.ItemInfo.skin);
                    if(!string.IsNullOrEmpty(comp.ItemInfo.name)) panimal.name = comp.ItemInfo.name;

                    player.GiveItem(panimal);
                }

                if(comp.bot) comp.bot.Kill();
                SendMsg(player, "ChatCommand_Success_Despawn");

                return false;
            }

            if(enableSpawnChecks)
            {
                if(_config.spawnInfo.canSpawnInCupboard == false && player.GetBuildingPrivilege() != null)
                {
                    SendMsg(player, "ChatCommand_Error_CannotSpawn");
                    return false;
                }

                if(!_config.spawnInfo.canSpawnOnConstruction)
                {
                    RaycastHit hit;

                    if(Physics.Raycast(player.transform.position + Vector3.up * 0.5f, Vector3.down, out hit, 100f, LayerMask.GetMask("Construction")))
                    {
                        SendMsg(player, "ChatCommand_Error_CannotSpawn");
                        return false;
                    }
                }

                if(!_config.spawnInfo.canSpawnOnDeployed)
                {
                    RaycastHit hit;

                    if(Physics.Raycast(player.transform.position + Vector3.up * 0.5f, Vector3.down, out hit, 100f, LayerMask.GetMask("Deployed")))
                    {
                        SendMsg(player, "ChatCommand_Error_CannotSpawn");
                        return false;
                    }
                }
            }

            if(checkForCooldown)
            {
                if(_cooldownInfo.ContainsKey(player.userID))
                {
                    var lastTimeSpawn = _cooldownInfo[player.userID];

                    if(DateTime.Now > lastTimeSpawn.AddSeconds(info.cooldown))
                    {
                        _cooldownInfo.Remove(player.userID);
                        _cooldownInfo.Add(player.userID, DateTime.Now);
                    }
                    else 
                    {
                        SendMsg(player, "ChatCommand_Notice_Cooldown", new string[] { Mathf.RoundToInt((float)(lastTimeSpawn.AddSeconds(info.cooldown) - DateTime.Now).TotalSeconds).ToString() });
                        return false;
                    }
                }
                else 
                {
                    _cooldownInfo.Add(player.userID, DateTime.Now);
                }
            }

            var controller = player.gameObject.AddComponent<PlayerAnimalController>();
            
            controller.bot = CreateBot(player, info, player.transform.position, true);
            controller.owner = player;

            controller.mountButton = mountButton;
            controller.controlButton = controlButton;
            controller.inventoryButton = inventoryButton;

            _existsControllers.Remove(player.net.ID.Value);
            _existsControllers.Add(player.net.ID.Value, controller);

            SendMsg(player, "ChatCommand_Success_Spawn");

            return true;
        }

        #endregion
    }
} 