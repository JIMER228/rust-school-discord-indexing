using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Globalization;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Portable Copter", "https://topplugin.ru/", "2.3.2")]

	class PortableCopter : RustPlugin
	{
		[PluginReference]
		Plugin ImageLibrary;

		int citemid = 573676040; //ID предмета который меняем
		ulong cskinid = 1663370375; //ID скина
		string name = "Миникоптер\n\n<size=16>   <color=#32CD32>Установите его на улице</color></size>"; //Новое название и описание предмета
		
		/////////////////////////////
		
		double otstup = 0.325; //Отступ с левой стороны (смещение ингредиентов для крафта, чем больше ингредиентов тем меньше отступ)
		bool craftenabled = true; //Включить (true) или выключить (false) возможность крафта
		
		/////////////////////////////
		
		private void OnEntityBuilt(Planner planner, GameObject gameobject)
		{
			var ent = gameobject.ToBaseEntity();
			if (ent == null) return;
			if (ent.skinID != cskinid) return;
			var player = planner.GetOwnerPlayer();
			if (player == null) return;

            NextTick(() => { ent.Kill(); });

			var Copter = GameManager.server.CreateEntity("assets/content/vehicles/minicopter/minicopter.entity.prefab", ent.transform.position, ent.transform.rotation);

			RaycastHit rHit;

			if (Physics.Raycast(new Vector3(ent.transform.position.x, ent.transform.position.y, ent.transform.position.z), Vector3.down, out rHit, 2f, LayerMask.GetMask("Construction", "Deployed")) && rHit.GetEntity() != null)
			{
				PrintToChat(player, "Создавайте миникоптер на улице!");
				ReceiveCopter(player);
				NextTick(() => { Copter.Kill(); });
				return;
			}
			var entlist = new List<BaseEntity>();			
			Vis.Entities(ent.transform.position, 1, entlist);
			int count = 0;
			foreach(BaseEntity ent1 in entlist) if (ent!=ent1) count++;
			if (count > 0)
			{
				PrintToChat(player, "Создавайте миникоптер вдали от объектов\nна достаточном от себя расстоянии");
				ReceiveCopter(player);
				NextTick(() => { Copter.Kill(); });
				return;
			}
			
			Copter.Spawn();
			PrintToChat(player, "<color=#32CD32>Миникоптер создан!</color>");
		}
		
		private void ReceiveCopter(BasePlayer player)
		{
			Item copter = ItemManager.CreateByItemID(citemid, 1, cskinid);
			copter.name = name;
			
			if (6 - player.inventory.containerBelt.itemList.Count > 0)
			{
				copter.MoveToContainer(player.inventory.containerBelt);
			}
			else if (24 - player.inventory.containerMain.itemList.Count > 0)
			{
				copter.MoveToContainer(player.inventory.containerMain);
			}
			else
			{
				copter.Drop(player.transform.position, Vector3.up);
				PrintToChat(player, "Спавнер миникоптера брошен Вам под ноги!");
			}
			PrintToChat(player, "<color=#32CD32>Вы получили миникоптер!</color>");
			return;
		}
		
		private object CanCombineDroppedItem(DroppedItem item, DroppedItem targetItem)
		{
			if (item.item.info.itemid == citemid)
				if (item.skinID == cskinid || targetItem.skinID == cskinid) return false;
			
			return null;
		}
		
		private object CanStackItem(Item item, Item targetItem)
		{
			if (item.info.itemid == citemid)
				if (item.skin == cskinid || targetItem.skin == cskinid) return false;
			
			return null;
		}
		
        private void OnServerInitialized()
        {
            LoadConfig();
            SaveConfig();
			
			if (!plugins.Find("ImageLibrary"))
			{
				PrintError("Please setup ImageLibrary plugin!");
				Interface.Oxide.UnloadPlugin(Title);
				return;
			}
			
			if (craftenabled)
			{
				ImageLibrary.Call("AddImage", "http://i.imgur.com/k9tiotx.png", "copter");
				
				foreach (var item in conf.Ingredients)
				{
					if (item.image != String.Empty) ImageLibrary.Call("AddImage", item.image, item.shortname);
					else ImageLibrary.Call("AddImage", $"https://rustlabs.com/img/items180/{item.shortname}.png", item.shortname);
				}
			}
        }
		
		[ConsoleCommand("givecopter")]
		private void GiveConsoleCopter(ConsoleSystem.Arg arg)
		{
			if (!arg.IsAdmin || !arg.HasArgs()) return;
			BasePlayer player = BasePlayer.Find(arg.Args[0]);
			string dt = "["+DateTime.Now.ToString("dd.MM - HH:mm")+"]";
			if (!player)
			{
				LogToFile("Receive", $"{dt} ERROR! Игрок не найден", this);
			}
			LogToFile("Receive", $"{dt} Коптер выдан игроку {player.displayName} ({player.userID})", this);
			PrintWarning($"{dt} Коптер выдан игроку {player.displayName} ({player.userID})");
			ReceiveCopter(player);
		}
			
		[ChatCommand("gco")]
		void GiveAdmCopter(BasePlayer player, string cmd, string[] args)
		{
			if (!player.IsAdmin) return;
			ReceiveCopter(player);
			return;
		}
		
		public object GetItem(BasePlayer player, int itemId, ulong skinID, int amount, bool check = false)
        {
            if (check)
            {
                List<Item> list = player.inventory.FindItemIDs(itemId);
				
				if (skinID == 0)
				{
					if (player.inventory.GetAmount(itemId) < amount) return false;
					return true;
				}
				
				foreach (var findItem in list)
                {
                    if (findItem.skin == skinID && findItem.amount >= amount)
                    {
                        return true;
                    }
					return false;
                }
				
                return false;
            }
            else
            {
                List<Item> customTake = player.inventory.FindItemIDs(itemId);

                for (int i = 0; i < customTake.Count; i++)
                {
                    if (customTake[i].amount <= amount && amount != 0)
                    {
                        amount = amount - customTake[i].amount;
                        customTake[i].RemoveFromContainer();
                    }
                    if (customTake[i].amount > amount && amount != 0)
                    {
                        int first = customTake[i].amount;
                        customTake[i].amount = customTake[i].amount - amount;
                        int second = first - customTake[i].amount;
                        amount = amount - second;
                        customTake[i].MarkDirty();
                    }
                }
				
                return null;
            }
        }
		
		[ChatCommand("copter")]
		void CraftCopter(BasePlayer player)
		{
			if (!craftenabled)
			{
				PrintToChat(player, "Крафт миникоптера отключен!");
				return;
			}
			
			var left = otstup;
			
			CuiHelper.DestroyUi(player, "CopterCraft");
			
			CuiElementContainer container = new CuiElementContainer();

			container.Add(new CuiPanel()
			{
				CursorEnabled = true,
				RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
				Image = { FadeIn = 0.5f, Color = "0 0 0 0.3" }
			}, "Overlay", "CopterCraft");
			
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Button = { FadeIn = 0.5f, Color = "0 0 0 0.5", Material = "assets/content/ui/uibackgroundblur.mat" },
                Text = { Text = "" }
            }, "CopterCraft");
			
			container.Add(new CuiLabel()
            {
                RectTransform = { AnchorMin = "0.5 0.85", AnchorMax = "0.5 0.85", OffsetMin = "-200 -20", OffsetMax = "200 20"}, 
                Text = { FadeIn = 0.5f, Align = TextAnchor.MiddleCenter, Text = "КРАФТ МИНИКОПТЕРА", FontSize = 34} 
            }, "CopterCraft"); 
			
			container.Add(new CuiElement()
			{
				Name = "CopterCraft" + ".CopterImage",
				Parent = "CopterCraft",
				Components =
				{
					new CuiRawImageComponent()
					{ 
						FadeIn = 1.0f,
						Color = "1 1 1 1",
						Png = (string) ImageLibrary.Call("GetImage", "copter")
					},
					new CuiRectTransformComponent()
					{
						AnchorMin = "0.5 0.71", AnchorMax = "0.5 0.71", OffsetMin = "-100 -100", OffsetMax = "100 100"
					},
				}
			});
			
			container.Add(new CuiLabel()
            {
                RectTransform = { AnchorMin = "0.5 0.6", AnchorMax = "0.5 0.6", OffsetMin = "-200 -20", OffsetMax = "200 20"}, 
                Text = { Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", Text = "Необходимые ресурсы:", FontSize = 28} 
            }, "CopterCraft"); 
			
			foreach (var item in conf.Ingredients)
			{
				Item newItem = ItemManager.CreateByItemID(item.itemID, item.amount, item.skin);
				
				if (newItem == null)
				{
					PrintError($"Cant create item with ID: {item.itemID}");
					continue;
				}

				container.Add(new CuiElement()
				{
					Parent = "CopterCraft",
					Name = "CopterCraft" + $".{newItem.info.shortname}",
					Components =
					{
						new CuiImageComponent()
						{
							FadeIn = 1.5f,
							Color = "0 0 0 0.2",
						},
                        new CuiOutlineComponent()
                        {
							Color = (bool) GetItem(player, item.itemID, item.skin, item.amount, true) == true ? "0 1 0 0.01" : "1 0 0 0.01",
                            Distance = "1 1",
                        },
						new CuiRectTransformComponent()
						{
							AnchorMin = $"{left} 0.4",
							AnchorMax = $"{left + 0.1} 0.56"
						},
					}
				});
				
                container.Add(new CuiElement()
                {
					Name = "CopterCraft" + $".{newItem.info.shortname}.Image",
                    Parent = "CopterCraft" + $".{newItem.info.shortname}",
                    Components =
                    {
                        new CuiRawImageComponent()
                        { 
							FadeIn = 1.5f,
                            Color = "1 1 1 1",
                            Png = (string) ImageLibrary.Call("GetImage", newItem.info.shortname)
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin = "0.15 0.15", AnchorMax = "0.85 0.85"
                        },
                    }
                });
				
				container.Add(new CuiLabel()
				{
					RectTransform = { AnchorMin = "0.8 0.09", AnchorMax = "0.8 0.09", OffsetMin = "-20 -15", OffsetMax = "20 15"}, 
					Text = { Align = TextAnchor.MiddleRight, Font = "robotocondensed-regular.ttf", Text = $"x{item.amount}", FontSize = 12} 
				}, "CopterCraft" + $".{newItem.info.shortname}"); 
				
				left += 0.12;
			}
			
			container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Button = { Color = "0 0 0 0", Close = "CopterCraft" },
                Text = { Text = "" }
            }, "CopterCraft", "CopterCraft" + ".Close");
			
			container.Add(new CuiButton()
            {
                RectTransform = { AnchorMin = $"0.4 0.18", AnchorMax = $"0.6 0.24" },
                Button = { Color = "0.9686275 0.9215686 0.8823529 0.03529412", Command = $"craftminicopter" },   
                Text = { Align = TextAnchor.MiddleCenter, Color = "1 1 1 1", Text = "СОЗДАТЬ", FontSize = 24 }
            }, "CopterCraft", "CopterCraft" + ".Button");
			
			CuiHelper.AddUi(player, container);
			return;
		}
		
		[ConsoleCommand("craftminicopter")]
		private void CraftCopterConsole(ConsoleSystem.Arg arg)
		{
			if (!craftenabled) return;
			
			if (arg.Player() == null) return;
			
			BasePlayer player = arg.Player();
			
			Dictionary<int, int> items = new Dictionary<int, int>();
			
			foreach (var item in conf.Ingredients)
			{
				if ((bool) GetItem(player, item.itemID, item.skin, item.amount, true) == false)
				{
					PrintToChat(player, "Не хватает ресурсов для крафта!");
					CuiHelper.DestroyUi(player, "CopterCraft");
					return;
				}
				
                items.Add(item.itemID, item.amount);
			}
			
			foreach (var item in items)
			{
				GetItem(player, item.Key, 0U, item.Value, false);
			}
			
			LogToFile("Receive", $"Игрок {player.displayName} ({player.userID}) скрафтил коптер", this);
			CuiHelper.DestroyUi(player, "CopterCraft");
			ReceiveCopter(player);
		}
		
		public class Configuration
		{
			[JsonProperty("Стоимость крафта")] public List<ItemIngredient> Ingredients = new List<ItemIngredient>();
			
			public static Configuration createdconfiguration()
            {
                Configuration createdconfig = new Configuration();
				
                createdconfig.Ingredients = new List<ItemIngredient>()
                {
                    new ItemIngredient
                    {
                        itemID = 479143914,
						shortname = "gears",
                        amount = 15,
						skin = 0,
                        image = String.Empty
                    },
                    new ItemIngredient
                    {
                        itemID = 69511070,
						shortname = "metal.fragments",
                        amount = 5000,
						skin = 0,
                        image = String.Empty
                    },
                    new ItemIngredient
                    {
                        itemID = 1199391518,
						shortname = "roadsigns",
                        amount = 15,
						skin = 0,
                        image = String.Empty
                    },
                };
                
                return createdconfig;
            }
		}
		
        public class ItemIngredient
        {
            [JsonProperty("ID предмета")] public int itemID = 0;
			[JsonProperty("Название предмета")] public string shortname = String.Empty;
            [JsonProperty("Количество")] public int amount = 0;
			[JsonProperty("Скин предмета (для кастом предметов)")] public ulong skin = 0;
			[JsonProperty("Ссылка на картинку (для кастом предметов)")] public string image = String.Empty;
        }
		
        protected override void LoadConfig()
        {
            base.LoadConfig();
            conf = Config.ReadObject<Configuration>();
			if (conf?.Ingredients == null) LoadDefaultConfig();
            NextTick(SaveConfig);
        }

		protected override void LoadDefaultConfig() => conf = Configuration.createdconfiguration();
		
		protected override void SaveConfig() => Config.WriteObject(conf);
		
		public Configuration conf;
	}
}