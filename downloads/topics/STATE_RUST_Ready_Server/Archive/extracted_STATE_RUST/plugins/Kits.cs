using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
	/* Based on version 1.2.1 */
    [Info("Kits", "wazzzup|Nimant", "1.0.6")]	
	class Kits: RustPlugin
    {        		
        private static Dictionary < string, Dictionary < int, List < KitItem >>> RandomList = new Dictionary < string, Dictionary < int, List < KitItem >>> ();
        private static readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);
        private static double CurrentTime() => DateTime.UtcNow.Subtract(epoch).TotalSeconds;        		
        private static Dictionary < int, List < ulong >> skinCache = new Dictionary < int, List < ulong >> ();
        private static ConfigData configData;
        
		private class ConfigData
        {
			[JsonProperty(PropertyName = "Цвет фона")]
            public string background = "0 0 0 0.5";
			[JsonProperty(PropertyName = "Цвет фона доступного набора")]
            public string kitBackground = "0.4392 0.5215 0.2980 0.9";
			[JsonProperty(PropertyName = "Цвет фона недоступного набора")]
            public string kitDisabledBackground = "0.7882 0.2666 0.1882 0.9";
			[JsonProperty(PropertyName = "Количество наборов в строке")]
            public int kitsPerRow = 8;
			[JsonProperty(PropertyName = "Высота панельки набора")]
            public float kitHeight = 0.1f;
			[JsonProperty(PropertyName = "Ширина панельки набора")]
            public float kitWidth = 0.12f;
			[JsonProperty(PropertyName = "Расстояние между панельками наборов")]
            public float kitBetween = 0.01f;
			[JsonProperty(PropertyName = "Минимальная позиция углов картинки на наборе")]
            public string kitImageAnchorMin = "0.1 0.1";
			[JsonProperty(PropertyName = "Максимальная позиция углов картинки на наборе")]
            public string kitImageAnchorMax = "0.4 0.9";
			[JsonProperty(PropertyName = "Прозрачность картинки на наборе")]
            public string kitImageTransparency = "0.9";
			[JsonProperty(PropertyName = "Использовать логирование")]
            public bool logusage = false;
			[JsonProperty(PropertyName = "Соответствие количества патронов оружию")]
            public Dictionary < string, KeyValuePair < string, int >> weaponAmmunition = new Dictionary < string, KeyValuePair < string, int >> ()
            {
                { "bow.hunting", new KeyValuePair < string, int > ("arrow.wooden", 50) },
                { "crossbow", new KeyValuePair < string, int > ("arrow.wooden", 50) },
                { "pistol.eoka", new KeyValuePair < string, int > ("ammo.handmade.shell", 50) },
                { "pistol.revolver", new KeyValuePair < string, int > ("ammo.pistol", 50) },
                { "pistol.semiauto", new KeyValuePair < string, int > ("ammo.pistol", 50) },
                { "pistol.python", new KeyValuePair < string, int > ("ammo.pistol", 50) },
                { "pistol.m92", new KeyValuePair < string, int > ("ammo.pistol", 50) },
                { "shotgun.waterpipe", new KeyValuePair < string, int > ("ammo.shotgun.fire", 30) },
                { "shotgun.double", new KeyValuePair < string, int > ("ammo.shotgun.fire", 50) },
                { "shotgun.pump", new KeyValuePair < string, int > ("ammo.shotgun.fire", 50) },
                { "shotgun.spas12", new KeyValuePair < string, int > ("ammo.shotgun.fire", 50) },
                { "smg.thompson", new KeyValuePair < string, int > ("ammo.pistol", 60) },
                { "smg.2", new KeyValuePair < string, int > ("ammo.pistol", 60) },
                { "smg.mp5", new KeyValuePair < string, int > ("ammo.pistol", 60) },
                { "rifle.bolt", new KeyValuePair < string, int > ("ammo.rifle", 20) },
                { "rifle.semiauto", new KeyValuePair < string, int > ("ammo.rifle", 40) },
                { "lmg.m249", new KeyValuePair < string, int > ("ammo.rifle", 100) },
                { "rifle.ak", new KeyValuePair < string, int > ("ammo.rifle", 60) },
                { "rifle.lr300", new KeyValuePair < string, int > ("ammo.rifle", 60) }
            };
        }
		
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData(){};
            SaveConfig(config);
        }
		
        private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
		
        private class KitItemGroup
        {
			[JsonProperty(PropertyName = "Выдать данное число предметов из набора (0 - выдать все)")]
            public int pickCountFromGroup = 0;
			[JsonProperty(PropertyName = "Давать патроны к оружию")]
            public bool giveAmmoForWeapon = false;
			[JsonProperty(PropertyName = "Предметы набора")]
            public List < KitItem > items = new List < KitItem > ();
        }
		
        private class KitItem
        {
			[JsonProperty(PropertyName = "ИД предмета")]
            public int itemid;
			[JsonProperty(PropertyName = "Короткое имя предмета")]
            public string name;			
			[JsonProperty(PropertyName = "Контейнер")]
            public string container;
			[JsonProperty(PropertyName = "Шанс при случайном выборе")]
            public int randomChance = 100;
			[JsonProperty(PropertyName = "Минимальное количество (для случайного выпадения)")]
            public int minamount;
			[JsonProperty(PropertyName = "Максимальное количество (для случайного выпадения)")]
            public int maxamount;
			[JsonProperty(PropertyName = "Целостность предмета")]
            public float condition;
			[JsonProperty(PropertyName = "ИД скина")]
            public ulong skinid = 0;
			[JsonProperty(PropertyName = "Случайный скин")]
            public bool randomskin = false;
			[JsonProperty(PropertyName = "ИД предмета для чертежа")]
            public int blueprintTarget = 0;
			[JsonProperty(PropertyName = "Дополнительное содержимое предмета")]
            public List < ItemContent > contents;
			[JsonProperty(PropertyName = "Патроны для предмета")]
            public Weapon weapon;
        }
		
        private class Weapon
        {
			[JsonProperty(PropertyName = "Тип патронов")]
            public string ammoType;
			[JsonProperty(PropertyName = "Число патронов")]
            public int ammoAmount;
        }
		
        private class ItemContent
        {
			[JsonProperty(PropertyName = "Короткое имя предмета")]
            public string ShortName;
			[JsonProperty(PropertyName = "Целостность предмета")]
            public float Condition;
			[JsonProperty(PropertyName = "Число предметов")]
            public int Amount;
			
            public ItemContent(string ShortName, float Condition, int Amount)
            {
                this.ShortName = ShortName;
                this.Condition = Condition;
                this.Amount = Amount;
            }
        }
		
        private class Kit
        {
			[JsonProperty(PropertyName = "Приоритет сортировки")]
            public int priority;
			[JsonProperty(PropertyName = "Имя набора")]
            public string name;
			[JsonProperty(PropertyName = "Ссылка на картинку набора")]
            public string image = "";			
            [JsonIgnore] 
			public string imageID = "";
			[JsonProperty(PropertyName = "Это стартовый набор")]
            public bool isautokit = false;
			[JsonProperty(PropertyName = "Описание набора")]
            public string description;
			[JsonProperty(PropertyName = "Это набор со случайным выпадением предметов")]
            public bool isRandom = false;
			[JsonProperty(PropertyName = "Включить случайные скины")]
            public bool randomskin = false;
			[JsonProperty(PropertyName = "Максимальное доступное число наборов (0 - безлимит)")]
            public int max;
			[JsonProperty(PropertyName = "Задержка перед следующим получением набора (в секундах)")]
            public double cooldown;
			[JsonProperty(PropertyName = "Задержка на получение набора после вайпа (в секундах)")]
            public double cooldownFromWipe = 0;
			[JsonProperty(PropertyName = "Набор скрыт от игроков")]
            public bool hide = true;
			[JsonProperty(PropertyName = "Привилегия для набора")]
            public string permission = "";
			[JsonProperty(PropertyName = "Содержимое набора")]
            public List < KitItemGroup > itemgroups;			
            [JsonIgnore] 
			public int beltCount;
            [JsonIgnore] 
			public int mainCount;
            [JsonIgnore] 
			public int wearCount;
            [JsonIgnore] 
			public int totalCount;
        }
		
        private List < Kit > autokits = new List < Kit > ();
        private StoredData storedData = new StoredData();
		
        private class StoredData
        {
			[JsonProperty(PropertyName = "Языки")]
            public Dictionary < string, Dictionary < string, string >> lang = new Dictionary < string, Dictionary < string, string >> ();
			[JsonProperty(PropertyName = "Наборы")]
            public Dictionary < string, Kit > kits = new Dictionary < string, Kit > ();
        }
		
        private static Dictionary < ulong, Dictionary < string, KitData >> kitsData;
		
        private class KitData
        {
			[JsonProperty(PropertyName = "Получено наборов")]
            public int max;
			[JsonProperty(PropertyName = "Задержка после получения набора")]
            public double cooldown;
        }
		
        private void SaveKitsData() 
		{
			var data = new Dictionary < ulong, Dictionary < string, KitData >>();
			
			foreach(var pair in kitsData)
			{				
				foreach(var pair2 in pair.Value)
				{
					if (pair2.Value.max <= 0 && pair2.Value.cooldown <= 0) continue;
					
					if (!data.ContainsKey(pair.Key))
						data.Add(pair.Key, new Dictionary < string, KitData >());				
					
					data[pair.Key].Add(pair2.Key, pair2.Value);
				}
			}
			
			Interface.Oxide.DataFileSystem.WriteObject(this.Title + "_Data", data);
		}
        
        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(this.Title, storedData);
        
        private void Init()
        {
            permission.RegisterPermission("kits.ignorelimits", this);
            configData = Config.ReadObject < ConfigData > ();
            SaveConfig(configData);
            mainUI = mainUI.Replace("{background}", configData.background);
            try
            {
                kitsData = Interface.Oxide.DataFileSystem.ReadObject < Dictionary < ulong, Dictionary < string, KitData >>> (this.Title + "_Data");
            }
            catch
            {
                kitsData = new Dictionary < ulong, Dictionary < string, KitData >> ();
            }
            try
            {
                storedData = Interface.Oxide.DataFileSystem.ReadObject < StoredData > (this.Title);
            }
            catch
            {
                storedData = new StoredData();
            }
        }
		
        private void OnServerInitialized() 
		{
			InitFileManager();									
			timer.Once(1f, ()=> InitKits());
		}
        
        private void OnPlayerRespawned(BasePlayer player)
        {
			if (player == null) return;
            var thereturn = Interface.Oxide.CallHook("canRedeemKit", player);
            if (thereturn == null)
            {
                foreach(Kit kit in autokits)
                {
                    var success = CanRedeemKit(player, kit.name, true) as string;
                    if (success != null) continue;
                    player.inventory.Strip();
                    success = GiveKit(player, kit.name) as string;
                    if (success != null) continue;
                    proccessKitGiven(player, kit.name);
                    return;
                }
            }
        }
		
        private void Unload()
        {			
            SaveKitsData();
            SaveLogs();
            foreach(var player in BasePlayer.activePlayerList) CuiHelper.DestroyUi(player, "KitsUI");			
        }
		
        private void OnServerSave()
        {
            SaveKitsData();
            SaveLogs();
        }
		
        private void OnNewSave()
        {
            kitsData = new Dictionary < ulong, Dictionary < string, KitData >> ();
            SaveKitsData();
        }
		
        private void SaveLogs()
        {
            foreach(var x in logging)
            {
                if (x.Value != "")
                {
                    LogToFile(x.Key, $"[{DateTime.Now}] {x.Value}", this);
                }
            }
            logging.Clear();
        }
		
		[ConsoleCommand("kit")] 
		private void cmdConsoleGiveKit(ConsoleSystem.Arg arg)
        {
			try
			{
				if (arg == null || arg.Connection == null)
				{
					if (arg != null)
						SendReply(arg, "Данную команду нельзя выполнять из консоли");
					return;
				}
				var player = arg.Player();
				if (player == null || arg.Args?.Length == 0) return;
				TryGiveKit(player, arg.Args[0]);
				CuiHelper.DestroyUi(player, "KitsUI");
				ShowUI(player);
			}
			catch {}
        }
		
		[ConsoleCommand("kits.open")] 
		private void cmdConsoleKitGui(ConsoleSystem.Arg arg)
        {
            if (arg == null || arg.Connection == null)
            {
				if (arg != null)
					SendReply(arg, "Вы не можете использовать данную команду");
                return;
            }
            var player = arg.Player();
            if (player == null) return;
            ShowUI(player);
        }
		
		[ChatCommand("kit")] 
		private void CommandChatKit(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            if (args.Length == 0)
            {
                ShowUI(player);
                return;
            }
            if (player.net.connection.authLevel < 2)
            {
                TryGiveKit(player, args[0].ToLower());
                return;
            }
            switch (args[0].ToLower())
            {
                case "resetdata":
                    OnNewSave();
                    break;
                case "add":
                case "new":
                    if (args.Length < 2) SendReply(player, GetMsg("Help", player.userID));
                    else
                    {
                        if (storedData.kits.ContainsKey(args[1].ToLower()))
                        {
                            SendReply(player, GetMsg("Kit Already Exists", player.userID));
                            break;
                        }
                        var kit = new Kit();
                        kit.priority = storedData.kits.Count < 1 ? 1 : storedData.kits.OrderByDescending(k => k.Value.priority).FirstOrDefault().Value.priority + 1;
                        kit.name = args[1].ToLower();
                        storedData.kits.Add(args[1].ToLower(), kit);
                        kit.itemgroups = GetPlayerItems(player);
                        SendReply(player, GetMsg("Kit Added", player.userID));
                    }
                    break;
                case "replace":
                    if (args.Length < 2) SendReply(player, GetMsg("Help", player.userID));
                    else if (!storedData.kits.ContainsKey(args[1].ToLower()))
                    {
                        SendReply(player, GetMsg("No Kit Found", player.userID));
                        break;
                    }
                    storedData.kits[args[1].ToLower()].itemgroups = GetPlayerItems(player);
                    SendReply(player, GetMsg("Kit Replaced", player.userID));
                    break;
                case "remove":
                    if (args.Length < 2) SendReply(player, GetMsg("Help", player.userID));
                    else
                    {
                        if (!storedData.kits.Remove(args[1].ToLower()))
                        {
                            SendReply(player, GetMsg("No Kit Found", player.userID));
                            break;
                        }
                        SendReply(player, GetMsg("Kit Removed", player.userID));
                        break;
                    }
                    break;
                case "edit":
                    if (args.Length < 2 || args.Length % 2 != 0) SendReply(player, GetMsg("Help", player.userID));
                    else
                    {
                        if (!storedData.kits.ContainsKey(args[1].ToLower()))
                        {
                            SendReply(player, GetMsg("No Kit Found", player.userID));
                            break;
                        }
                        Kit kit = storedData.kits[args[1].ToLower()];
                        for (var i = 2; i < args.Length; i = i + 2)
                        {
                            switch (args[i].ToLower())
                            {
                                case "name":
                                    kit.name = args[i + 1];
                                    break;
                                case "permission":
                                    kit.permission = args[i + 1];
                                    break;
                                case "image":
                                    kit.image = args[i + 1];
                                    break;
                                case "priority":
                                    int prior = -1;
                                    if (!int.TryParse(args[i + 1], out prior)) break;
                                    foreach(var k in storedData.kits)
                                    {
                                        if (k.Value.priority >= prior) k.Value.priority += 1;
                                    }
                                    kit.priority = prior;
                                    break;
                                case "hide":
                                    kit.hide = GetBoolValue(args[i + 1]);
                                    break;
                                case "isautokit":
                                    kit.isautokit = GetBoolValue(args[i + 1]);
                                    break;
                                case "israndom":
                                    kit.isRandom = GetBoolValue(args[i + 1]);
                                    break;
                                case "randomskin":
                                    kit.isautokit = GetBoolValue(args[i + 1]);
                                    break;
                                case "max":
                                    kit.max = Convert.ToInt32(args[i + 1]);
                                    break;
                                case "cooldown":
                                    kit.cooldown = Convert.ToSingle(args[i + 1]);
                                    break;
                                case "wipe":
                                    kit.cooldownFromWipe = Convert.ToSingle(args[i + 1]);
                                    break;
                            }
                        }
                        SendReply(player, GetMsg("Kit edited", player.userID));
                    }
                    break;
                default:
                    TryGiveKit(player, args[0].ToLower());
                    return;
            }
            InitKits();
            SaveData();
        }
		
		[HookMethod("GetKitInfo")] 
		public object GetKitInfo(string kitname)
        {
            if (storedData.kits.ContainsKey(kitname.ToLower()))
            {
                Kit kit = storedData.kits[kitname.ToLower()];
                JObject obj = new JObject();
                obj["name"] = kit.name;
                obj["permission"] = kit.permission;
                obj["max"] = kit.max;
                obj["image"] = kit.image;
                obj["hide"] = kit.hide;
                obj["description"] = kit.description;
                obj["cooldown"] = kit.cooldown;
                JArray items = new JArray();
                foreach(var g in kit.itemgroups)
                {
                    foreach(var itemEntry in g.items)
                    {
                        JObject item = new JObject();
                        item["amount"] = itemEntry.minamount;
                        item["container"] = itemEntry.container;
                        item["itemid"] = itemEntry.itemid;
                        item["skinid"] = itemEntry.skinid;
                        item["weapon"] = itemEntry.weapon != null ? true : false;
                        item["blueprint"] = itemEntry.blueprintTarget > 0;
                        JArray mods = new JArray();
                        if (itemEntry.contents != null)
                        {
                            foreach(var mod in itemEntry.contents) mods.Add(mod.ShortName);
                            item["mods"] = mods;
                        }
                        items.Add(item);
                    }
                }
                obj["items"] = items;
                return obj;
            }
            return null;
        }
		
        private bool TryGiveKit(BasePlayer player, string kitname, bool force = false)
        {
			if (player == null) return false;
			
            var success = CanRedeemKit(player, kitname, force) as string;
            if (success != null)
            {
                OnScreen(player, success, "0.7882 0.2666 0.1882 0.9");
                return false;
            }
            success = GiveKit(player, kitname) as string;
            if (success != null)
            {
                OnScreen(player, success, "0.7882 0.2666 0.1882 0.9");
                return false;
            }
            OnScreen(player, GetMsg("KitRedeemed", player.userID), "0.4392 0.5215 0.2980 0.9");
            if (!force) proccessKitGiven(player, kitname);
            return true;
        }
		
        private void proccessKitGiven(BasePlayer player, string kitname)
        {
			if (player == null) return;
            if (string.IsNullOrEmpty(kitname)) return;
            Kit kit;
            if (!storedData.kits.TryGetValue(kitname, out kit)) return;
            var kitData = GetKitData(player.userID, kitname);
            if (kit.max > 0) kitData.max += 1;
            if (kit.cooldown > 0) kitData.cooldown = CurrentTime() + kit.cooldown;
            if (configData.logusage && !kit.isautokit) Log($"{player.displayName} {player.userID} получил набор {kit.name}"); 
        }
		
        private object GiveKit(BasePlayer player, string kitname, bool toInventory = true, ItemContainer inventory = null)
        {
            if (player == null && inventory != null) return GiveKit(inventory, kitname);
            if (string.IsNullOrEmpty(kitname)) return GetMsg("Empty kit name", player.userID);
            kitname = kitname.ToLower();
            Kit kit;
            if (!storedData.kits.TryGetValue(kitname, out kit)) return GetMsg("No Kit Found", player.userID);
            if (kit.isRandom)
            {
                foreach(var group in RandomList[kitname])
                {
                    foreach(KitItem item in group.Value)
                    {
                        if (kit.itemgroups[group.Key].pickCountFromGroup > 0)
                        {
                            for (int j = 1; j <= kit.itemgroups[group.Key].pickCountFromGroup; j++)
                            {
                                KitItem kitem = group.Value.GetRandom();
                                Item itemGive = BuildItem(kitem, kit.randomskin);
                                GiveItem(player.inventory, itemGive, kitem.container == "belt" ? player.inventory.containerBelt : kitem.container == "wear" ? player.inventory.containerWear : player.inventory.containerMain);
                                if (kit.itemgroups[group.Key].giveAmmoForWeapon && itemGive.info.category.ToString() == "Weapon")
                                {
                                    if (configData.weaponAmmunition.ContainsKey(kitem.name))
                                    {
                                        Item gitem = ItemManager.CreateByName(configData.weaponAmmunition[kitem.name].Key, configData.weaponAmmunition[kitem.name].Value);
                                        if (!gitem.MoveToContainer(player.inventory.containerMain)) gitem.Drop(player.inventory.containerMain.dropPosition, player.inventory.containerMain.dropVelocity, new Quaternion());
                                    }
                                }
                            }
                            break;
                        }
                        else
                        {
                            if (item.randomChance != 100)
                            {
                                if (UnityEngine.Random.Range(0, 100) <= item.randomChance)
                                {
                                    GiveItem(player.inventory, BuildItem(item, kit.randomskin), item.container == "belt" ? player.inventory.containerBelt : item.container == "wear" ? player.inventory.containerWear : player.inventory.containerMain);
                                }
                            }
                            else
                            {
                                GiveItem(player.inventory, BuildItem(item, kit.randomskin), item.container == "belt" ? player.inventory.containerBelt : item.container == "wear" ? player.inventory.containerWear : player.inventory.containerMain);
                            }
                        }
                    }
                }
            }
            else foreach(var g in kit.itemgroups)
            {
                foreach(var kitem in g.items)
                {
                    GiveItem(player.inventory, BuildItem(kitem, kit.randomskin), kitem.container == "belt" ? player.inventory.containerBelt : kitem.container == "wear" ? player.inventory.containerWear : player.inventory.containerMain);
                }
            }
            return true;
        }
		
        private object GiveKit(ItemContainer inventory, string kitname)
        {
			if (inventory == null) return false;
            if (string.IsNullOrEmpty(kitname)) return false;
            kitname = kitname.ToLower();
            Kit kit;
            if (!storedData.kits.TryGetValue(kitname, out kit)) return false;
            if (kit.isRandom)
            {
                foreach(var group in RandomList[kitname])
                {
                    foreach(KitItem item in group.Value)
                    {
                        if (kit.itemgroups[group.Key].pickCountFromGroup > 0)
                        {
                            for (int j = 1; j <= kit.itemgroups[group.Key].pickCountFromGroup; j++)
                            {
                                KitItem kitem = group.Value.GetRandom();
                                Item itemGive = BuildItem(kitem, kit.randomskin);
                                if (!GiveItem(inventory, itemGive)) itemGive.Drop(inventory.dropPosition, inventory.dropVelocity, new Quaternion());
                                if (kit.itemgroups[group.Key].giveAmmoForWeapon)
                                {
                                    if (configData.weaponAmmunition.ContainsKey(kitem.name) && itemGive.info.category.ToString() == "Weapon")
                                    {
                                        Item gitem = ItemManager.CreateByName(configData.weaponAmmunition[kitem.name].Key, configData.weaponAmmunition[kitem.name].Value);
                                        if (!gitem.MoveToContainer(inventory)) gitem.Drop(inventory.dropPosition, inventory.dropVelocity, new Quaternion());
                                    }
                                }
                            }
                            break;
                        }
                        else
                        {
                            Item gitem = BuildItem(item, kit.randomskin);
                            if (!GiveItem(inventory, gitem)) gitem.Drop(inventory.dropPosition, inventory.dropVelocity, new Quaternion());
                        }
                    }
                }
            }
            else foreach(var g in kit.itemgroups)
            {
                foreach(var kitem in g.items)
                {
                    Item gitem = BuildItem(kitem, kit.randomskin);
                    if (!GiveItem(inventory, gitem)) gitem.Drop(inventory.dropPosition, inventory.dropVelocity, new Quaternion());
                }
            }
            return true;
        }
		
        private ulong uzero = 0U;
		
        private void InitKits()
        {
            Dictionary < int, Kit > tempautokits = new Dictionary < int, Kit > ();            
            foreach(var k in storedData.kits)
            {
                if (!string.IsNullOrEmpty(k.Value.image))
                {
                    string imname = "kit" + k.Key;
					CommunityEntity.ServerInstance.StartCoroutine(m_FileManager.LoadFile(imname, k.Value.image));                    
                }
                if (!storedData.lang.ContainsKey("en")) storedData.lang.Add("en", new Dictionary < string, string > ());
                if (!storedData.lang.ContainsKey("ru")) storedData.lang.Add("ru", new Dictionary < string, string > ());
                if (!storedData.lang["en"].ContainsKey(k.Value.name))
                {
                    storedData.lang["en"].Add(k.Value.name, k.Value.name);
                    storedData.lang["ru"].Add(k.Value.name, k.Value.name);
                }
                if (k.Value.isautokit) tempautokits.Add(k.Value.priority, k.Value);
                if (!string.IsNullOrEmpty(k.Value.permission) && !permission.PermissionExists("kits." + k.Value.permission, this)) permission.RegisterPermission("kits." + k.Value.permission, this);
                foreach(var g in k.Value.itemgroups)
                {
                    k.Value.beltCount = g.items.Where(i => i.container == "belt").Count();
                    k.Value.wearCount = g.items.Where(i => i.container == "wear").Count();
                    k.Value.mainCount = g.items.Where(i => i.container == "main").Count();
                    if (g.pickCountFromGroup > 0)
                    {
                        if (k.Value.beltCount > g.pickCountFromGroup) k.Value.beltCount = g.pickCountFromGroup;
                        if (k.Value.wearCount > g.pickCountFromGroup) k.Value.wearCount = g.pickCountFromGroup;
                        if (k.Value.mainCount > g.pickCountFromGroup) k.Value.mainCount = g.pickCountFromGroup;
                    }
                    k.Value.totalCount = k.Value.beltCount + k.Value.wearCount + k.Value.mainCount;
                    foreach(var item in g.items)
                    {
                        if (item.randomskin || k.Value.randomskin)
                        {
                            if (!skinCache.ContainsKey(item.itemid))
                            {
                                var skins = ItemSkinDirectory.ForItem(ItemManager.FindItemDefinition(item.itemid));
                                if (skins.Length > 1)
                                {
                                    var list = new List < ulong > ();
                                    list.Add(0U);
                                    foreach(var s in skins)
                                    {
                                        list.Add((ulong) s.id);
                                    }
                                    skinCache.Add(item.itemid, list);
                                }
                            }
                        }
                    }
                }
                if (k.Value.isRandom && !RandomList.ContainsKey(k.Value.name))
                {
                    RandomList.Add(k.Value.name, new Dictionary < int, List < KitItem >> ());
                    int i = 0;
                    foreach(KitItemGroup itemgroup in k.Value.itemgroups)
                    {
                        RandomList[k.Value.name].Add(i, new List < KitItem > ());
                        foreach(KitItem item in itemgroup.items)
                        {
                            if (item.randomChance > 0)
                            {
                                if (itemgroup.pickCountFromGroup > 0)
                                {
                                    for (int j = 1; j <= item.randomChance; j++)
                                    {
                                        RandomList[k.Value.name][i].Add(item);
                                    }
                                }
                                else
                                {
                                    RandomList[k.Value.name][i].Add(item);
                                }
                            }
                        }
                        Shuffle(RandomList[k.Value.name][i]);
                        i++;
                    }
                }
            }            
            storedData.kits = storedData.kits.OrderBy(k => k.Value.priority).ToDictionary(x => x.Key, x => x.Value);
            autokits = tempautokits.OrderBy(k => k.Key).ToDictionary(x => x.Key, x => x.Value).Values.ToList();
            if (autokits.Count < 1) Unsubscribe(nameof(OnPlayerRespawned));
            GetKitImages();
            SaveData();
        }
		
        public System.Random rnd = new System.Random();
        
		private void Shuffle(List < KitItem > list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rnd.Next(n + 1);
                KitItem value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }
		
        private void GetKitImages()
        {
            if (!m_FileManager.IsFinished)
            {                
                timer.In(1f, GetKitImages);
                return;
            }
            foreach(var k in storedData.kits)
            {
                if (!string.IsNullOrEmpty(k.Value.image))
                {
                    string imname = "kit" + k.Key;
                    k.Value.imageID = m_FileManager.GetPng(imname);
                }
            }
        }
		
        private Dictionary < string, string > logging = new Dictionary < string, string > ();
		
        private void Log(string text, string filename = "log")
        {
            if (!logging.ContainsKey(filename)) logging.Add(filename, text + "\r\n");
            else logging[filename] += text + "\r\n";
        }
		
		private static string FormatShortTime(double time)
		{
			TimeSpan elapsedTime = TimeSpan.FromSeconds(time);
            int hours = elapsedTime.Hours;
            int minutes = elapsedTime.Minutes;
            int seconds = elapsedTime.Seconds;
            int days = Mathf.FloorToInt((float)elapsedTime.TotalDays);
            string s = "";
			int cnt = 0;
			
			char ch = ' ';
			
            if (days > 0) { s += $"{days} дн{ch}"; cnt+=1; }
            if (hours > 0) { s += $"{hours} ч{ch}"; cnt+=2; }
			if (cnt == 1 || cnt == 3) return s.TrimEnd(' ','\n');
			
            if (minutes > 0) { s += $"{minutes} мин{ch}"; cnt+=4; }			
			if (cnt == 2 || cnt == 6) return s.TrimEnd(' ','\n');
			
            if (seconds > 0) s += $"{seconds} сек";            						
			if (string.IsNullOrEmpty(s)) return null;
			
            return s.TrimEnd(' ','\n');
			
			if (string.IsNullOrEmpty(s))
				s = $"пара{ch}секунд";
			
            return s;
		}		        
		
        private bool CanRedeemKitUI(BasePlayer player, Kit kit)
        {
			if (player == null) return false;
            if (kit.hide || kit.isautokit) return false;
            if (!string.IsNullOrEmpty(kit.permission))
                if (!permission.UserHasPermission(player.UserIDString, "kits." + kit.permission)) return false;
            var kitData = GetKitData(player.userID, kit.name);
            if (kit.max > 0)
                if (kitData.max >= kit.max) return false;
            return true;
        }
		
        private object CanRedeemKit(BasePlayer player, string kitname, bool skipAuth = false)
        {
			if (player == null) return "null";
            if (string.IsNullOrEmpty(kitname)) return GetMsg("Empty kit name", player.userID);
            kitname = kitname.ToLower();
            Kit kit;
            if (!storedData.kits.TryGetValue(kitname, out kit)) return GetMsg("No Kit Found", player.userID);
            if (!skipAuth && (kit.hide || kit.isautokit) && player.net.connection.authLevel < 2) return GetMsg("No Kit Found", player.userID);
            object thereturn = Interface.Oxide.CallHook("canRedeemKit", player);
            if (thereturn != null)
            {
                if (thereturn is string) return thereturn;
                return GetMsg("CantRedeemNow", player.userID);
            }
            if (!skipAuth && !string.IsNullOrEmpty(kit.permission) && !permission.UserHasPermission(player.UserIDString, "kits." + kit.permission)) return GetMsg("NoPermKit", player.userID);
            var kitData = GetKitData(player.userID, kitname);
            if (!skipAuth && kit.max > 0 && !permission.UserHasPermission(player.UserIDString, "kits.ignorelimits"))
                if (kitData.max >= kit.max) return GetMsg("NoRemainingUses", player.userID);
            if (kit.cooldownFromWipe > 0 && !permission.UserHasPermission(player.UserIDString, "kits.ignorelimits"))
            {
                double secsFromWipe = DateTime.UtcNow.Subtract(SaveRestore.SaveCreatedTime).TotalSeconds;
                if (secsFromWipe < kit.cooldownFromWipe) return string.Format(GetMsg("CooldownMessage", player.userID), FormatShortTime(kit.cooldownFromWipe - secsFromWipe));
            }
            if (!skipAuth && kit.cooldown > 0 && !permission.UserHasPermission(player.UserIDString, "kits.ignorelimits"))
            {
                var ct = CurrentTime();
                if (kitData.cooldown > ct && kitData.cooldown != 0.0) return string.Format(GetMsg("CooldownMessage", player.userID), FormatShortTime(Math.Abs(Math.Ceiling(kitData.cooldown - ct))));
            }
            if ((player.inventory.containerBelt.capacity - player.inventory.containerBelt.itemList.Count) < kit.beltCount || (player.inventory.containerWear.capacity - player.inventory.containerWear.itemList.Count) < kit.wearCount || (player.inventory.containerMain.capacity - player.inventory.containerMain.itemList.Count) < kit.mainCount)
                if (kit.totalCount > (player.inventory.containerMain.capacity - player.inventory.containerMain.itemList.Count)) return GetMsg("NoInventorySpace", player.userID);
            return true;
        }
		
        private bool GiveItem(ItemContainer container, Item item)
        {
            if (container == null) return false;
            if (!item.MoveToContainer(container, -1, true))
				item.Drop(container.dropPosition, container.dropVelocity, new Quaternion());
			
			return true;
        }
		
        private bool GiveItem(PlayerInventory inv, Item item, ItemContainer container = null)
        {
            if (inv == null || item == null)
            {
                return false;
            }
            int position = -1;
			
			var cont = container != null ? container : inv.containerMain;
			
            if (!(((container != null) && item.MoveToContainer(container, position, true)) || (item.MoveToContainer(inv.containerMain, -1, true) || item.MoveToContainer(inv.containerBelt, -1, true))))
				item.Drop(cont.dropPosition, cont.dropVelocity, new Quaternion());
			
			return true;
        }
		
        private Item BuildItem(KitItem kitem, bool kitRandomSkin = false)
        {
            int amount = kitem.minamount;
            if (kitem.minamount < kitem.maxamount) amount = UnityEngine.Random.Range(kitem.minamount, kitem.maxamount);
            ulong skin = kitem.skinid;
            if (kitem.randomskin || kitRandomSkin)
            {
                if (skinCache.ContainsKey(kitem.itemid))
                {
                    skin = skinCache[kitem.itemid].GetRandom();
                }
            }
            Item item = ItemManager.CreateByName(kitem.name, amount, skin);            
            item.condition = kitem.condition;
            if (kitem.blueprintTarget != 0) item.blueprintTarget = kitem.blueprintTarget;
            if (kitem.weapon != null)
            {
                var weapon = item.GetHeldEntity() as BaseProjectile;
                if (weapon != null)
                {
                    (item.GetHeldEntity() as BaseProjectile).primaryMagazine.contents = kitem.weapon.ammoAmount;
                    (item.GetHeldEntity() as BaseProjectile).primaryMagazine.ammoType = ItemManager.FindItemDefinition(kitem.weapon.ammoType);
                }
            }
            if (kitem.contents != null)
            {
                foreach(var cont in kitem.contents)
                {
                    Item new_cont = ItemManager.CreateByName(cont.ShortName, cont.Amount);
                    new_cont.condition = cont.Condition;
                    new_cont.MoveToContainer(item.contents);
                }
            }
            return item;
        }
		
        private KitData GetKitData(ulong userID, string kitname)
        {
            if (!kitsData.ContainsKey(userID)) kitsData.Add(userID, new Dictionary < string, KitData > ());
            if (!kitsData[userID].ContainsKey(kitname)) kitsData[userID].Add(kitname, new KitData());
            return kitsData[userID][kitname];
        }
		
        private static List < KitItemGroup > GetPlayerItems(BasePlayer player)
        {
			if (player == null) return new List < KitItemGroup >();
            List < KitItemGroup > kitgroups = new List < KitItemGroup > ();
            var kitgroup = new KitItemGroup();
            var kititems = kitgroup.items;
            kitgroups.Add(kitgroup);
            foreach(Item item in player.inventory.containerWear.itemList.OrderBy(x=>x.position))
            {
                if (item != null)
                {
                    var iteminfo = ProcessItem(item, "wear");
                    kititems.Add(iteminfo);
                }
            }
            foreach(Item item in player.inventory.containerMain.itemList.OrderBy(x=>x.position))
            {
                if (item != null)
                {
                    var iteminfo = ProcessItem(item, "main");
                    kititems.Add(iteminfo);
                }
            }
            foreach(Item item in player.inventory.containerBelt.itemList.OrderBy(x=>x.position))
            {
                if (item != null)
                {
                    var iteminfo = ProcessItem(item, "belt");
                    kititems.Add(iteminfo);
                }
            }
            return kitgroups;
        }
		
        private static KitItem ProcessItem(Item item, string container)
        {
            KitItem iItem = new KitItem();
            iItem.minamount = item.amount;
            iItem.maxamount = item.amount;
            iItem.container = container;
            iItem.condition = item.condition;
            iItem.skinid = item.skin;
            iItem.itemid = item.info.itemid;
            iItem.name = item.info.shortname;
            iItem.blueprintTarget = item.blueprintTarget;
            if (item.info.category.ToString() == "Weapon")
            {
                BaseProjectile weapon = item.GetHeldEntity() as BaseProjectile;
                if (weapon != null)
                {
                    if (weapon.primaryMagazine != null)
                    {
                        iItem.weapon = new Weapon();
                        iItem.weapon.ammoType = weapon.primaryMagazine.ammoType.shortname;
                        iItem.weapon.ammoAmount = weapon.primaryMagazine.contents;
                    }
                }
            }
            if (item.contents != null)
            {
                iItem.contents = new List < ItemContent > ();
                foreach(var mod in item.contents.itemList)
                {
                    if (mod.info.itemid != 0)
                    {
                        iItem.contents.Add(new ItemContent(mod.info.shortname, mod.condition, mod.amount));
                    }
                }
            }
            return iItem;
        }
		
        private static bool GetBoolValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            value = value.Trim().ToLower();
            switch (value)
            {
                case "t":
                case "true":
                case "1":
                case "yes":
                case "y":
                case "on":
                    return true;
                default:
                    return false;
            }
        }
		
        private string GetMsg(string key, object steamid = null) => lang.GetMessage(key, this, steamid == null ? null : steamid.ToString());
        
        private string GetKitName(string key, string lang)
        {
            if (!storedData.lang.ContainsKey(lang)) lang = "en";
            if (!storedData.lang.ContainsKey(lang)) return key;
            if (storedData.lang[lang].ContainsKey(key)) return storedData.lang[lang][key];
            return key;
        }
		
        private static Dictionary < string, string > langEN = new Dictionary < string, string > ()
        {
            { "Help", "/kit new name - добавить набор с текущим инвентарём\n/kit replace name - заменить набор инветарём\n/kit remove name - удалить набор\n/kit edit name flags - редактировать набор, флаги: \nname newname\nisautokit true|false\nisrandom true|false\nrandomskin true|false\nmax <максимальное число наборов>\ncooldown <время в секундах>\nwipe <время в секундах>\nhide true|false" },
            { "Kit Already Exists", "Такой набор уже существует" },
            { "Kit Added", "Набор создан" },
            { "No Kit Found", "Такого набора не существует" },
            { "Kit Replaced", "Вещи в наборе успешно заменены" },
            { "Kit Removed", "Набор удалён" },
            { "KitRedeemed", "Набор получен" },
            { "Empty kit name ", "Не указан набор" },
            { "CantRedeemNow", "В данный момент нельзя получить набор" },
            { "NoPermKit", "У вас нет прав на этот набор" },
            { "NoRemainingUses", "Данный набор закончился" },
            { "CooldownMessage", "Осталось подождать {0}" },
            { "NoInventorySpace", "Недостаточно места в инвентаре" },
            { "No kits", "Нет доступных наборов" },
            { "Kit edited", "Набор успешно отредактирован" },
        };
		
        private static Dictionary < string, string > langRU = new Dictionary < string, string > ()
        {
            { "Help", "/kit new name - добавить набор с текущим инвентарём\n/kit replace name - заменить набор инветарём\n/kit remove name - удалить набор\n/kit edit name flags - редактировать набор, флаги: \nname newname\nisautokit true|false\nisrandom true|false\nrandomskin true|false\nmax <максимальное число наборов>\ncooldown <время в секундах>\nwipe <время в секундах>\nhide true|false" },
            { "Kit Already Exists", "Такой набор уже существует" },
            { "Kit Added", "Набор создан" },
            { "No Kit Found", "Такого набора не существует" },
            { "Kit Replaced", "Вещи в наборе успешно заменены" },
            { "Kit Removed", "Набор удалён" },
            { "KitRedeemed", "Набор получен" },
            { "Empty kit name ", "Не указан набор" },
            { "CantRedeemNow", "В данный момент нельзя получить набор" },
            { "NoPermKit", "У вас нет прав на этот набор" },
            { "NoRemainingUses", "Данный набор закончился" },
            { "CooldownMessage", "Осталось подождать {0}" },
            { "NoInventorySpace", "Недостаточно места в инвентаре" },
            { "No kits", "Нет доступных наборов" },
            { "Kit edited", "Набор успешно отредактирован" },
        };
		
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(langEN, this);
            lang.RegisterMessages(langRU, this, "ru");
        }
		
        private void OnScreen(BasePlayer player, string text, string color)
        { 
			if (player == null) return;
            CuiHelper.DestroyUi(player, "KitsAlert"); 
            CuiHelper.AddUi(player, alertUI.Replace("{text}", text).Replace("{color}", color));
            timer.Once(2, () => CuiHelper.DestroyUi(player, "KitsAlert"));
        }
		
		private string mainUI = @"[{""name"":""KitsUI"",""parent"":""Overlay"",""components"":[{""type"":""UnityEngine.UI.Image"",""color"":""0 0 0 0""},{""type"":""RectTransform"",""anchormin"":""0 0"",""anchormax"":""1 1""},{""type"":""NeedsCursor""}]},{""name"":""77946099d7304efa942e03f6bc1257ef"",""parent"":""KitsUI"",""components"":[{""type"":""UnityEngine.UI.Button"",""close"":""KitsUI"",""color"":""0 0 0 0""},{""type"":""RectTransform"",""anchormin"":""0 0"",""anchormax"":""1 1""}]},{""name"":""KitsUIPanel_"",""parent"":""KitsUI"",""components"":[{""type"":""UnityEngine.UI.Image"",""color"":""{background}""},{""type"":""RectTransform"",""anchormin"":""{positionMin}"",""anchormax"":""{positionMax}""},{""type"":""NeedsCursor""}]}]"; 
		private string panelUI = @"[{""name"":""KitsUIPanel{number}"",""parent"":""KitsUIPanel_"",""components"":[{""type"":""UnityEngine.UI.Image"",""color"":""0 0 0 0""},{""type"":""RectTransform"",""anchormin"":""{anchormin}"",""anchormax"":""{anchormax}""},{""type"":""NeedsCursor""}]}]"; 
		private string alertUI = @"[{""name"":""KitsAlert"",""parent"":""Overlay"",""components"":[{""type"":""UnityEngine.UI.Image"",""color"":""{color}""},{""type"":""RectTransform"",""anchormin"":""0.35 0.8"",""anchormax"":""0.65 0.85""}]},{""parent"":""KitsAlert"",""components"":[{""type"":""UnityEngine.UI.Text"",""text"":""{text}"",""fontSize"":18,""align"":""MiddleCenter"",""color"":""1 1 1 1""},{""type"":""UnityEngine.UI.Outline"",""color"":""0.0 0.0 0.0 1.0"",""distance"":""1 1""},{""type"":""RectTransform"",""anchormin"":""0 0"",""anchormax"":""1 1""}]}]";
				
        private void ShowUI(BasePlayer player)
        {
			if (player == null) return;
            CuiHelper.DestroyUi(player, "KitsUI"); 
            int kitCount = 0;
            var kitElements = new CuiElementContainer();
            int panelCount = 1;
            object thereturn = Interface.Oxide.CallHook("canRedeemKit", player);
            if (thereturn == null)
            {
                Dictionary < string, Dictionary < string, string >> userkits = new Dictionary < string, Dictionary < string, string >> ();
                foreach(var k in storedData.kits)
                {
                    if (k.Value.hide || !CanRedeemKitUI(player, k.Value)) continue;
                    string kittext = "";
                    double cooldown = 0;
                    double secsFromWipeLeft = 0;
                    int max = 0;
                    KitData kitData = null;
                    if ((k.Value.cooldown > 0 || k.Value.max > 0) && !permission.UserHasPermission(player.UserIDString, "kits.ignorelimits"))
                    {
                        kitData = GetKitData(player.userID, k.Value.name);
                    }
                    if (k.Value.max > 0)
                    {
                        kittext += $"осталось {k.Value.max - kitData.max}";
                    }
                    if (k.Value.cooldown > 0 && !permission.UserHasPermission(player.UserIDString, "kits.ignorelimits"))
                    {
                        var ct = CurrentTime();
                        if (kitData.cooldown > ct && kitData.cooldown != 0.0)
                        {
                            cooldown = Math.Abs(Math.Ceiling(kitData.cooldown - ct));
                            if (kittext != "") kittext += "\n";
                            kittext += FormatShortTime(cooldown);
                        }
                    }
                    if (k.Value.cooldownFromWipe > 0 && !permission.UserHasPermission(player.UserIDString, "kits.ignorelimits"))
                    {
                        double secsFromWipe = DateTime.UtcNow.Subtract(SaveRestore.SaveCreatedTime).TotalSeconds;
                        secsFromWipeLeft = k.Value.cooldownFromWipe - secsFromWipe;
                        if (secsFromWipeLeft > 0)
                        {
                            if (kittext != "") kittext += "\n";
                            kittext += FormatShortTime(k.Value.cooldownFromWipe - secsFromWipe);
                        }
                    }
                    userkits.Add(k.Value.name, new Dictionary < string, string > ()
                    {
                        {
                            "kittext",
                            kittext
                        },
                        {
                            "background",
                            (cooldown > 0 || secsFromWipeLeft > 0 ? configData.kitDisabledBackground : configData.kitBackground)
                        },
                        {
                            "cooldown",
                            (cooldown > 0 || secsFromWipeLeft > 0 ? "1" : "0")
                        },
                        {
                            "image",
                            k.Value.imageID
                        },
						{
                            "row",
                            k.Value.permission
                        },
                    });                    
                    kitCount++;
                }
                int curkitcount = GetKitCountInRow(userkits, 0);
                float uistart = (1f - (configData.kitWidth + configData.kitBetween) * curkitcount) / 2f;								
				
                int panel = 1;
                int kk = 0, allKk = 0;				
				
                foreach(var k in userkits)
                {
                    kitElements.Add(new CuiPanel()
                    {
                        Image = {
                            Color = k.Value["background"]
                        }, RectTransform = {
                            AnchorMin = $"{uistart+kk*(configData.kitWidth+configData.kitBetween)} 0.1",
                            AnchorMax = $"{uistart+kk*(configData.kitWidth+configData.kitBetween)+configData.kitWidth} 0.9"
                        }
                    }, $"KitsUIPanel{panel}", $"KitsUIkit_{k.Key}");
                    if (k.Value["image"] != "")
                    {
                        kitElements.Add(new CuiElement()
                        {
                            Parent = $"KitsUIkit_{k.Key}", Components = {
                                new CuiRawImageComponent
                                {
                                    Color = $"1 1 1 {configData.kitImageTransparency}", 
									Png = k.Value["image"], 
									Sprite = "assets/content/textures/generic/fulltransparent.tga"
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = configData.kitImageAnchorMin, 
									AnchorMax = configData.kitImageAnchorMax
                                }
                            }
                        });
                    }
                    kitElements.Add(new CuiElement
                    {
                        Parent = $"KitsUIkit_{k.Key}", Components = {
                            new CuiTextComponent
                            {
                                Color = "1 1 1 1", 
								Text = GetKitName(k.Key, lang.GetLanguage(player.UserIDString)) + (k.Value["kittext"] != "" ? $"\n<size=12><color=#d4d4d4>{k.Value["kittext"]}</color></size>" : ""), 
								FontSize = 16, 
								Align = TextAnchor.MiddleCenter
                            },
                            new CuiOutlineComponent
                            {
                                Distance = "1 1", 
								Color = "0.0 0.0 0.0 1.0"
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = $"0 0", 
								AnchorMax = $"1 1"
                            },
                        }
                    });
                    kitElements.Add(new CuiButton
                    {
                        Button = {
                            Command = (k.Value["cooldown"] == "1" ? "" : $"kit {k.Key}"),
                            Color = "0 0 0 0"
                        }, RectTransform = {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1"
                        }, Text = {
                            Text = "",
                            FontSize = 16,
                            Align = TextAnchor.MiddleCenter,
                        }
                    }, $"KitsUIkit_{k.Key}");
                    kk++;																				
					
					if (kk >= curkitcount)
					{
						panel++;
						panelCount = panel;
						allKk += kk;
						kk = 0;
						curkitcount = GetKitCountInRow(userkits, allKk);						
						uistart = (1f - (configData.kitWidth + configData.kitBetween) * curkitcount) / 2f;
					}					                    
                }				
				if (kk == 0) panelCount--;
            }            
            if (panelCount < 1) panelCount = 1; 
            CuiHelper.AddUi(player, mainUI.Replace("{positionMin}", $"0 {0.5f-configData.kitHeight*panelCount/2f}").Replace("{positionMax}", $"1 {0.5f + configData.kitHeight * panelCount / 2f}"));
            if (kitCount < 1)
            {
                var el = new CuiElementContainer();
                el.Add(new CuiElement
                {
                    Parent = "KitsUIPanel_", Components = {
                        new CuiTextComponent
                        {
                            Color = "1 1 1 1", 
							Text = GetMsg("No kits", player.userID), 
							FontSize = 16, 
							Align = TextAnchor.MiddleCenter
                        },
                        new CuiOutlineComponent
                        {
                            Distance = "1 1", 
							Color = "0.0 0.0 0.0 1.0"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = $"0 0", 
							AnchorMax = $"1 1"
                        },
                    }
                });
                CuiHelper.AddUi(player, el);
            }
            else
            {
                float maxYanchor = 1f;
                float perPanel = 1f / panelCount;                
                for (int j = 1; j <= panelCount; j++)
                {
                    CuiHelper.AddUi(player, panelUI.Replace("{number}", $"{j}").Replace("{anchormin}", $"0 {maxYanchor - perPanel}").Replace("{anchormax}", $"1 {maxYanchor}"));
                    maxYanchor -= perPanel;                    
                }
                CuiHelper.AddUi(player, kitElements);
            }
        }	

		private int GetKitCountInRow(Dictionary < string, Dictionary < string, string >> kits, int pass)
		{
			int count = 0, kCount = 0;
			string lastRow = "zero";			
			foreach(var k in kits)
			{
				count++;
				if (count > pass)
				{
					kCount++;					
					
					if ( (kCount > configData.kitsPerRow) || (k.Value["row"] != lastRow && lastRow != "zero") )
						return kCount-1;															
					
					lastRow = k.Value["row"];
				}												
			}
									
			return kCount;
		}
		
		#region File Manager
        
        private GameObject FileManagerObject;
        private FileManager m_FileManager;
        
        private void InitFileManager()
        {
            FileManagerObject = new GameObject("MAP_FileManagerObject");
            m_FileManager = FileManagerObject.AddComponent<FileManager>();
        }

        private class FileManager : MonoBehaviour
        {
            int loaded = 0;
            int needed = 0;

            public bool IsFinished => needed == loaded;
            const ulong MaxActiveLoads = 10;
            public Dictionary<string, FileInfo> files = new Dictionary<string, FileInfo>();

            public class FileInfo
            {
                public string Url;
                public string Png;
            }

            public string GetPng(string name) => files[name].Png;

            public IEnumerator LoadFile(string name, string url, int size = -1)
            {
                if (files.ContainsKey(name) && files[name].Url == url && !string.IsNullOrEmpty(files[name].Png)) yield break;
                files[name] = new FileInfo() { Url = url };
                needed++;
                yield return StartCoroutine(LoadImageCoroutine(name, url, size));
            }

            IEnumerator LoadImageCoroutine(string name, string url, int size = -1)
            {
                using (WWW www = new WWW(url))
                {
                    yield return www;
                    if (string.IsNullOrEmpty(www.error))
                    {
                        var bytes = size == -1 ? www.bytes : Resize(www.bytes, size);


                        var entityId = CommunityEntity.ServerInstance.net.ID;
                        var crc32 = FileStorage.server.Store(bytes, FileStorage.Type.png, entityId).ToString();
                        files[name].Png = crc32;
                    }
                }
                loaded++;                
            }

            static byte[] Resize(byte[] bytes, int size)
            {
                Image img = (Bitmap)(new ImageConverter().ConvertFrom(bytes));
                Bitmap cutPiece = new Bitmap(size, size);
                System.Drawing.Graphics graphic = System.Drawing.Graphics.FromImage(cutPiece);
                graphic.DrawImage(img, new Rectangle(0, 0, size, size), 0, 0, img.Width, img.Height, GraphicsUnit.Pixel);
                graphic.Dispose();
                MemoryStream ms = new MemoryStream();
                cutPiece.Save(ms, ImageFormat.Jpeg);
                return ms.ToArray();
            }
        }    
		
		#endregion
		
    }
	
}
