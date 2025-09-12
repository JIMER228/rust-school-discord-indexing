using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
using Rust;

namespace Oxide.Plugins
{
    [Info("BloodBoost", "r3dapple", "1.0.82")] //Проверки: cutted

    class BloodBoost : RustPlugin
    {
		[PluginReference] Plugin ImageLibrary;
		
		private void OnServerInitialized()
        {
			LoadConfig();
			if (!ImageLibrary) PrintWarning("Для использования картинок необходим плагин ImageLibrary");
			foreach (var priv in config.Boost)
			{
				ImageLibrary?.Call("AddImage", priv.image, priv.boostname);
				continue;
			}
            timer.Every(1f, CheckTimer);
			SaveConfig();
        }
		
		[ConsoleCommand("giveboost")]
		private void giveboost(ConsoleSystem.Arg arg)
		{
			//
			if (!arg.IsAdmin) return;

			var steamid = arg.GetString(0);
			if (string.IsNullOrEmpty(steamid))
			{
				arg.ReplyWith("Не указан steamid! -> giveboost {steamid} {boostname} {time}");
				return;
			}
			
			var boostname = arg.GetString(1);
			if (string.IsNullOrEmpty(boostname) || config.Boost.FindIndex(x => x.boostname == boostname) == -1)
			{
				arg.ReplyWith("Не указано или неверно указано название способности! -> giveboost {steamid} {boostname} {time}");
				return;
			}
			var privid = config.Boost.FindIndex(x => x.boostname == boostname);
			
			var countfirst = arg.GetString(2);
			if (string.IsNullOrEmpty(countfirst)) countfirst = "30";
			
			//
			
			BasePlayer player2 = null;
			
			foreach (BasePlayer activePlayer in BasePlayer.activePlayerList)
			{
				if (activePlayer.UserIDString == steamid) player2 = activePlayer;
			}
			
			if (player2 == null)
			{
				arg.ReplyWith("Игрок не найден");
				return;
			}
			
			int count;
			bool ifcount = Int32.TryParse(countfirst, out count);
			
			if (!ifcount) count = 30;
			
			Item item = ItemManager.CreateByItemID(config.Settings.itemid, 1, config.Settings.skin);
			
			if (item == null)
			{
				arg.ReplyWith("Неверный shortname предмета");
				return;
			}
			
			item.busyTime = privid+1;
			item._maxCondition = count;
			item.name = $"{config.Boost[privid].name} на {count.ToString()} сек.";
			
			player2.GiveItem(item);
			
			arg.ReplyWith("Способность выдана");
		}
		
		[ChatCommand("getblood")]
		private void GetBlood(BasePlayer player, string command, string[] args)
		{
			if (!player.IsAdmin) return;
			int amount = 1;
			if (args.Count() > 0 && args[0] != null)
			{
				bool isgood = Int32.TryParse(args[0], out amount);
			}
			Item item = ItemManager.CreateByItemID(config.Settings.itemid, amount, config.Settings.skin);
			item.name = config.Settings.name;
			player.GiveItem(item);
			PrintToChat(player, $"Вы получили <color=#32cd32>{config.Settings.name}</color> x <color=#32cd32>{amount}</color>");
			return;
		}
		
		[ChatCommand("blood")]
		private void DrawInfo(BasePlayer player)
		{
			CuiHelper.DestroyUi(player, Title + ".Help");
            CuiElementContainer container = new CuiElementContainer();
			
			container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, "Overlay", Title + ".Help");
			
			container.Add(new CuiElement
			{
				Name = Title + ".Help.Main",
				Parent = Title + ".Help",
				Components =
				{
					new CuiImageComponent { Color = "0 0 0 0.85" },
					new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-500 -200", OffsetMax = "500 200" }
				}
			});
			
			container.Add(new CuiElement
			{
				Name = Title + ".Help.Main.Title",
				Parent = Title + ".Help.Main",
				Components =
				{
					new CuiImageComponent { Color = "0 0 0 0.85" },
					new CuiRectTransformComponent { AnchorMin = "0.095 1.038", AnchorMax = "0.095 1.038", OffsetMin = "-95 -15", OffsetMax = "95 15" }
				}
			});
			
			container.Add(new CuiLabel()
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0"},
                Text = { FadeIn = 0.5f, Align = TextAnchor.MiddleCenter, Text = "СПОСОБНОСТИ:", Font = "robotocondensed-regular.ttf", FontSize = 20, Color = "1 1 1 0.7" }
            }, Title + ".Help.Main.Title", Title + ".Help.Main.Title.Text");
			
			container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.95 0.93", AnchorMax = "1 1", OffsetMax = "0 0" },
                Text = { Text = "☓", FontSize = 18, Font = "robotocondensed-regular.ttf", Color = "1 0 0 1", Align = TextAnchor.MiddleCenter },
                Button = { Close = Title + ".Help", Color = "0 0 0 0.3" }
            }, Title + ".Help.Main", Title + ".Help.Main.CloseButton");
			
			//
			
			for (int i = 0; i < config.Boost.Count; i++)
            {
				if (i > 4)
				{
					container.Add(new CuiButton
					{
						RectTransform = { AnchorMin = "0.9 0", AnchorMax = "1 0.1", OffsetMax = "0 0" },
						Text = { Text = "ДАЛЕЕ", FontSize = 20, Font = "robotocondensed-regular.ttf", Color = "1 1 1 0.7", Align = TextAnchor.MiddleCenter },
						Button = { Command = "bbnextpage", Color = "0 0 0 0.3" }
					}, Title + ".Help.Main");
					continue;
				}
				
				container.Add(new CuiElement
				{
					Name = Title + $".Help.Main.Boost{i}",
					Parent = Title + ".Help.Main",
					Components =
					{
						new CuiImageComponent { Color = "0 0 0 0.7" },
						new CuiRectTransformComponent { AnchorMin = $"{0.1+(0.2*i)} 0.5", AnchorMax = $"{0.1+(0.2*i)} 0.5", OffsetMin = "-75 -150", OffsetMax = "75 150" }
					}
				});
				
				container.Add(new CuiLabel()
				{
					RectTransform = { AnchorMin = "0.5 0.9", AnchorMax = "0.5 0.9", OffsetMin = "-60 -25", OffsetMax = "60 25" },
					Text = { Text = config.Boost[i].name, FontSize = 22, Font = "robotocondensed-regular.ttf", Color = "0.827 0.827 0.827", Align = TextAnchor.MiddleCenter }
				}, Title + $".Help.Main.Boost{i}");
				
				container.Add(new CuiElement
                {
                    Parent = Title + $".Help.Main.Boost{i}",
                    Components =
                    {
                        new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", config.Boost[i].boostname) },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.6", AnchorMax = "0.5 0.6", OffsetMin = "-45 -45", OffsetMax = "45 45" }
                    }
                });
				
				container.Add(new CuiButton
				{
					RectTransform = { AnchorMin = "0.5 0.15", AnchorMax = "0.5 0.15", OffsetMin = "-60 -25", OffsetMax = "60 25" },
					Button = { Color = "0.7 0.7 0.7 0", Command = $"boostinfo {config.Boost[i].boostname}" },
					Text = { Text = "ПОДРОБНЕЕ", FontSize = 22, Color = "0.827 0.827 0.827", Align = TextAnchor.MiddleCenter }
				}, Title + $".Help.Main.Boost{i}");
				
                /*container.Add(new CuiElement
                {
                    Parent = Title + ".Main",
                    Name = Title + ".Main" + $".{i}.Handler",
                    Components =
                    {
                        new CuiImageComponent { FadeIn = 0.5f, Color = "0.7 0.7 0.7 0.5" },
                        new CuiRectTransformComponent { AnchorMin = $"{(i*0.19)+x1} 0.3", AnchorMax = $"{(i*0.19)+x2} 0.8", OffsetMax = "0 0" }
                    }
                });
                
                container.Add(new CuiElement
                {
                    Parent = Title + ".Main" + $".{i}.Handler",
                    Components =
                    {
                        new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", config.Boost[i].name) },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-65 -65", OffsetMax = "65 65" }
                    }
                });
				
				container.Add(new CuiLabel()
				{
					RectTransform = { AnchorMin = "0 0.1", AnchorMax = "1 0.15" },
					Text = { Text = $"Собрано:", Font = "robotocondensed-regular.ttf", FontSize = 14, Color = "1 1 1 1", Align = TextAnchor.MiddleCenter }
				}, Title + ".Main" + $".{i}.Handler");
				
				container.Add(new CuiLabel()
				{
					RectTransform = { AnchorMin = "0 0.0", AnchorMax = "1 0.125" },
					Text = { Text = $"{playerstat} из {config.Boost[i].toreceive}", Font = "robotocondensed-regular.ttf", FontSize = 24, Color = "1 1 1 1", Align = TextAnchor.MiddleCenter }
				}, Title + ".Main" + $".{i}.Handler");
				
				container.Add(new CuiButton
				{
					RectTransform = { AnchorMin = "0 -0.15", AnchorMax = "1 -0.015", OffsetMin = "0 0", OffsetMax = "0 0" },
					Button = { Color = "0.7 0.7 0.7 0.5", Command = playerstat < config.Boost[i].toreceive ? "" : $"activategroup {i}" },
					Text = { Text = playerstat < config.Boost[i].toreceive ? "НЕДОСТУПНО" : "ПОЛУЧИТЬ", FontSize = 24, Color = "0.827 0.827 0.827", Align = TextAnchor.MiddleCenter }
				}, Title + ".Main" + $".{i}.Handler");
                
                container.Add(new CuiElement
                {
                    Parent = Title + ".Main" + $".{i}.Handler",
                    Components =
                    {
                        new CuiTextComponent { Text = $"{config.Boost[i].name}", Font = "robotocondensed-regular.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter},
                        new CuiRectTransformComponent { AnchorMin = "0 1", AnchorMax = "1 1.1" }
                    }
                });
				
				container.Add(new CuiElement
                {
                    Parent = Title + ".Main" + $".{i}.Handler",
                    Components =
                    {
                        new CuiImageComponent { Color = "0.564 0.933 0.564 0.5" },
                        new CuiRectTransformComponent { AnchorMin = $"0 0", AnchorMax = $"1 {playerstat >= config.Boost[i].toreceive ? 1 : (float)playerstat/config.Boost[i].toreceive}", OffsetMax = "0 0" }
                    }
                });*/
            }
			
			CuiHelper.AddUi(player, container);
		}
		
		[ConsoleCommand("boostinfo")]
		private void boostinfo(ConsoleSystem.Arg arg)
		{
			if (!arg.Player()) return;
			BasePlayer player = arg.Player();
			var bname = arg.GetString(0);
			if (string.IsNullOrEmpty(bname)) return;
			var boost = config.Boost.First(x => x.boostname == bname);
			//var privid = config.Boost.FindIndex(x => x.boostname == boostname);
			
			CuiHelper.DestroyUi(player, Title + ".Help");
            CuiElementContainer container = new CuiElementContainer();
			
			container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, "Overlay", Title + ".Help");
			
			container.Add(new CuiElement
			{
				Name = Title + ".Help.Main",
				Parent = Title + ".Help",
				Components =
				{
					new CuiImageComponent { Color = "0 0 0 0.85" },
					new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-500 -200", OffsetMax = "500 200" }
				}
			});
			
			container.Add(new CuiElement
			{
				Name = Title + ".Help.Main.Title",
				Parent = Title + ".Help.Main",
				Components =
				{
					new CuiImageComponent { Color = "0 0 0 0.85" },
					new CuiRectTransformComponent { AnchorMin = "0.095 1.038", AnchorMax = "0.095 1.038", OffsetMin = "-95 -15", OffsetMax = "95 15" }
				}
			});
			
			container.Add(new CuiLabel()
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0"},
                Text = { FadeIn = 0.5f, Align = TextAnchor.MiddleCenter, Text = "ПОДРОБНЕЕ", Font = "robotocondensed-regular.ttf", FontSize = 20, Color = "1 1 1 0.7" }
            }, Title + ".Help.Main.Title", Title + ".Help.Main.Title.Text");
			
			container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.95 0.93", AnchorMax = "1 1", OffsetMax = "0 0" },
                Text = { Text = "←", FontSize = 18, Font = "robotocondensed-regular.ttf", Color = "1 0 0 1", Align = TextAnchor.MiddleCenter },
                Button = { Command = "chat.say /blood", Color = "0 0 0 0.3" }
            }, Title + ".Help.Main", Title + ".Help.Main.CloseButton");
			
			container.Add(new CuiLabel()
            {
                RectTransform = { AnchorMin = "0.5 0.85", AnchorMax = "0.5 0.85", OffsetMin = "-200 -50", OffsetMax = "200 50"},
                Text = { FadeIn = 0.5f, Align = TextAnchor.MiddleCenter, Text = $"<b>{boost.name}</b>", Font = "robotocondensed-regular.ttf", FontSize = 22, Color = "1 1 1 1" }
            }, Title + ".Help.Main", Title + ".Help.Main.BigTitle");
			
			container.Add(new CuiLabel()
            {
                RectTransform = { AnchorMin = "0.5 0.4", AnchorMax = "0.5 0.4", OffsetMin = "-300 -125", OffsetMax = "300 125"},
                Text = { FadeIn = 0.5f, Align = TextAnchor.UpperCenter, Text = boost.description, Font = "robotocondensed-regular.ttf", FontSize = 16, Color = "1 1 1 1" }
            }, Title + ".Help.Main", Title + ".Help.Main.Text");
			
			CuiHelper.AddUi(player, container);
			
			return;
		}
		
		private void CheckTimer()
        {
            foreach (var player in actives.Keys.ToList())
            {
                var time = --actives[player];
				var boost = config.Boost.First(x => x.boostname == boostname[player]);
				if (time == 5 || time == 10 || time == 15) PrintToChat(player, $"Через {time} секунд у Вас закончится действие способности <color=#32cd32>{boost.name}</color>");
                if (time <= 0)
                {
					PrintToChat(player, $"У Вас закончилось действие способности <color=#32cd32>{boost.name}</color>");
                    KillBoosts(player);
                    CuiHelper.DestroyUi(player, Title);
                    continue;
                }
				var type = boostname[player];
                DrawUI(player, time, type);
            }
        }
		
		private void OnPlayerDeath(BasePlayer player, HitInfo info)
		{
			if (player == null) return;
			KillBoosts(player);
			CuiHelper.DestroyUi(player, Title);
			return;
		}
		
		void DrawUI(BasePlayer player, int time, string type)
		{
			CuiHelper.DestroyUi(player, Title);
            CuiElementContainer container = new CuiElementContainer();

			container.Add(new CuiPanel
            {
                CursorEnabled = false,
                RectTransform = { AnchorMin = "0.317 0.0665", AnchorMax = "0.317 0.0665", OffsetMin = "-30 -30", OffsetMax = "30 30" },
                Image = { Color = "1 1 1 0.15" }
            }, "Overlay", Title);
			
			container.Add(new CuiElement
			{
				Name = Title + ".Image",
				Parent = Title,
				Components =
				{
					new CuiRawImageComponent { Png = (string) ImageLibrary?.Call("GetImage", type), Color = "1 1 1 0.5" },
					new CuiRectTransformComponent { AnchorMin = "0.05 0.05", AnchorMax = "0.95 0.95", OffsetMax = "0 0"}
				}
			});
			
			container.Add(new CuiElement
			{
				Name = Title + ".Scale",
				Parent = Title,
				Components =
				{
					new CuiImageComponent { Color = "0.196 0.803 0.196 0.5" },
					new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = $"1 {Math.Min(0.01 + 1 * ((float) time / config.Settings.togive), 1)}", OffsetMin = "0 0", OffsetMax = "0 0"}//r3d
				}
			});
			
			
			
			container.Add(new CuiElement
			{
				Name = Title + ".Timer",
				Parent = Title,
				Components =
				{
					new CuiTextComponent { Text = time.ToString(), FontSize = 16, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter, Color = "1 1 1 1"},
					new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 0.4", OffsetMax = "0 0"},
					new CuiOutlineComponent { Color = "0 0 0 0.9", Distance = "0.7 0.7" }
				}
			});
			
			CuiHelper.AddUi(player, container);
		}
		
		private void Unload()
		{
			foreach (var player in BasePlayer.activePlayerList)
			{
				CuiHelper.DestroyUi(player, Title);
				CuiHelper.DestroyUi(player, Title + ".Help");
			}
		}
		
		Dictionary<BasePlayer, string> boostname = new Dictionary<BasePlayer, string>();
		
		Dictionary<BasePlayer, int> actives = new Dictionary<BasePlayer, int>();
		
		private void KillBoosts(BasePlayer player)
		{
			if (player == null) return;
			if (actives.ContainsKey(player))
			{	
				actives.Remove(player);
				boostname.Remove(player);
			}
			return;
		}
		
		private System.Random random = new System.Random();
		
		private object CanMoveItem(Item item, PlayerInventory playerLoot, uint targetContainer, int targetSlot, int amount)
		{
			if (item == null) return null;
			
			if (item.skin != config.Settings.skin) return null;
			
			BasePlayer player = playerLoot.GetComponent<BasePlayer>();
			
			if (player == null) return null;
			
			ItemContainer container = playerLoot.FindContainer(targetContainer);
			
			if (container == null) return null;
			
			if (container == playerLoot.containerBelt)
			{
				if (item.busyTime != 0)
				{
					if (actives.ContainsKey(player))
					{
						var boostid = (int)item.busyTime-1;
						if (boostname[player] != config.Boost[boostid].boostname)
						{
							PrintToChat(player, "На данный момент у вас активна другая способность");
							return null;
						}
						item.UseItem(1);
						actives[player] += (int)item._maxCondition;
						PrintToChat(player, $"Вы продлили действие способности <color=#32cd32>{config.Boost.First(x => x.boostname == boostname[player]).name}</color> до <color=#32CD32>{actives[player]} сек.</color>");
						return null;
					}
					else
					{
						var boostid = (int)item.busyTime-1;
						item.UseItem(1);
						int secs = (int)item._maxCondition;
						actives[player] = secs;
						boostname.Add(player, config.Boost[boostid].boostname);
						PrintToChat(player, $"Вы активировали способность <color=#32cd32>{config.Boost[boostid].name}</color> на <color=#32cd32>{secs.ToString()} сек.</color>");
						return null;
					}
				}
				
				item.UseItem(1);
				
				if (actives.ContainsKey(player))
				{
					actives[player] += config.Settings.togive;
					PrintToChat(player, $"Вы продлили действие способности <color=#32cd32>{config.Boost.First(x => x.boostname == boostname[player]).name}</color> до <color=#32CD32>{actives[player]} сек.</color>");
					return null;
				}
				
				var privid = random.Next(0, config.Boost.Count);
				boostname.Add(player, config.Boost[privid].boostname);
				PrintToChat(player, $"Вы активировали способность <color=#32cd32>{config.Boost[privid].name}</color> на <color=#32cd32>{config.Settings.togive} сек.</color>");
				actives[player] = config.Settings.togive;
			}
			
			return null;
		}
		
		private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            try
            {
                if (info != null && entity != null) //here
                {
					if ((info.damageTypes.Get(DamageType.Explosion) > 0) && !(entity is BasePlayer))
					{
						BasePlayer raider = null;
						if (info.Initiator is BasePlayer && info.Initiator.GetComponent<NPCPlayer>() == null) //cutted
						{
							raider = info.Initiator as BasePlayer;
							if (actives.ContainsKey(raider))
							{
								if (boostname[raider] == "raider")
								{
									//PrintToChat($"> TEST | Должен был нанести {info.damageTypes.Get(DamageType.Explosion).ToString()} урона");
									var amount = config.Boost.First(x => x.boostname == "raider").amount;
									info.damageTypes.Scale(DamageType.Explosion, 1+amount/100);
									//PrintToChat($"> TEST | Нанёс {info.damageTypes.Get(DamageType.Explosion).ToString()} урона взрывами");
								}
							}
						}
					}
					if (entity is BasePlayer && entity.GetComponent<NPCPlayer>() == null) //cutted
					{
						BasePlayer victim = entity as BasePlayer;
						BasePlayer attacker = null;

						if (info.Initiator is BasePlayer && info.Initiator.GetComponent<NPCPlayer>() == null) //cutted
						{
							attacker = info.Initiator as BasePlayer;
						
							if (actives.ContainsKey(victim))
							{
								if (boostname[victim] == "shield")
								{
									//PrintToChat($"> TEST | {victim.displayName} должен был получить {info.damageTypes.Total()} урона");
									var amount = config.Boost.First(x => x.boostname == "shield").amount;
									info.damageTypes.ScaleAll((float)1-amount/100);
									//PrintToChat($"> TEST | Получил {info.damageTypes.Total().ToString()} урона ???");
								}
							}
							
							if (actives.ContainsKey(attacker))
							{
								if (boostname[attacker] == "regen")
								{
									//PrintToChat($"> TEST | {attacker.displayName} [{attacker.health} HP] получит {info.damageTypes.Total()*(config.Settings.regenlevel/100)} HP от регена");
									var amount = config.Boost.First(x => x.boostname == "regen").amount;
									attacker.Heal(info.damageTypes.Total()*(amount/100));
									//PrintToChat($"> TEST | У {attacker.displayName} теперь {attacker.health} HP");
								}
								if (boostname[attacker] == "moredamage")
								{
									//PrintToChat($"> TEST | {victim.displayName} должен был получить {info.damageTypes.Total()} урона");
									var amount = config.Boost.First(x => x.boostname == "moredamage").amount;
									info.damageTypes.ScaleAll(1+amount/100);
									//PrintToChat($"> TEST | Получил {info.damageTypes.Total().ToString()} урона ???");
								}
								if (boostname[attacker] == "radiation")
								{
									//PrintToChat($"> TEST | {victim.displayName} должен был получить {info.damageTypes.Total()} урона");
									var amount = config.Boost.First(x => x.boostname == "radiation").amount;
									info.damageTypes.ScaleAll(1+5/100);
									victim.metabolism.radiation_poison.value += amount;
									//PrintToChat($"> TEST | Получил {info.damageTypes.Total().ToString()} урона + радиацию???");
								}
							}
						}
					}
                }
            }
            catch (NullReferenceException)
            {
                
            }
        }
		
		private object CanStackItem(Item item, Item targetItem)
		{
			if (item.skin == config.Settings.skin || targetItem.skin == config.Settings.skin) return false;
			return null;
		}
		
		List <LootContainer> looted = new List <LootContainer> ();
		
		void OnLootEntity(BasePlayer player, BaseEntity entity, Item item)
		{
			if (!(entity is LootContainer)) return;
			var container = (LootContainer)entity;
			if (looted.Contains(container)) return;
			looted.Add(container);
		    List <int> il = new List <int> ();
		    if (containers.Contains(container.ShortPrefabName))
			{
				if (UnityEngine.Random.Range(0f, 100f) < config.Settings.chance) 
				{
					var itemContainer = container.inventory;
					foreach(var i1 in itemContainer.itemList)
					{
						il.Add(i1.info.itemid);
					}
					if (!il.Contains(config.Settings.itemid))
					{
						if (container.inventory.itemList.Count == container.inventory.capacity) container.inventory.capacity++;
						item = ItemManager.CreateByItemID(config.Settings.itemid, 1, config.Settings.skin);
						item.name = config.Settings.name;
						item.MoveToContainer(itemContainer);
					}
				}
		   }
		}
		
		List <string> containers = new List <string> ()
		{
			{"crate_basic"},
			{"crate_elite"},
			{"crate_normal"},
			{"crate_normal_2"},
			{"crate_tools"},
			{"crate_underwater_basic"},
			{"crate_underwater_advanced"},
			{"supply_drop"},
			{"codelockedhackablecrate"},
			{"bradley_crate"}
		};
		
		//
		
		string GetMsg(string key, BasePlayer player = null) => lang.GetMessage(key, this, player == null ? null : player.UserIDString);
		
		private _Conf config; 
		
        class _Conf
        {          
            [JsonProperty(PropertyName = "Настройки")]
            public Options Settings { get; set; }
			
			[JsonProperty(PropertyName = "Настройки способностей")]
            public List<Boosts> Boost { get; set; }

			public class Boosts
			{
				[JsonProperty(PropertyName = "Системное название (не менять)")]
				public string boostname { get; set; }
				[JsonProperty(PropertyName = "Название")]
				public string name { get; set; }
				[JsonProperty(PropertyName = "Описание")]
				public string description { get; set; }
				[JsonProperty(PropertyName = "Картинка")]
				public string image { get; set; }
				[JsonProperty(PropertyName = "Величина (на сколько % увеличивать или уменьшать урон, сколько давать радиации и тд)")]
				public float amount { get; set; }
			}
			
            public class Options
            {
				[JsonProperty(PropertyName = "На сколько секунд активировать способность")]
                public int togive { get; set; }
                [JsonProperty(PropertyName = "Шанс выпадения предмета")]
                public int chance { get; set; }
                [JsonProperty(PropertyName = "ID предмета")]
                public int itemid { get; set; }
				[JsonProperty(PropertyName = "Название предмета")]
                public string name { get; set; }
				[JsonProperty(PropertyName = "ID скина кейса")]
                public uint skin { get; set; }
            }
        }
		
        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<_Conf>();

            Config.WriteObject(config, true);
        }

        protected override void LoadDefaultConfig()
		{
			config = SetDefaultConfig();
			PrintWarning("Создаём конфиг-файл");
		}

        private _Conf SetDefaultConfig()
        {
            return new _Conf
            {
				Boost = new List<_Conf.Boosts>()
                {
					new _Conf.Boosts
					{
						boostname = "regen",
						name = "Регенерация",
						description = "Восстанавливает урон в процентах от урона, нанесённого другим игрокам",
						image = "https://rustlabs.com/img/items180/largemedkit.png",
						amount = 10,
					},
					new _Conf.Boosts
					{
						boostname = "shield",
						name = "Щит",
						description = "Уменьшает наносимый по вам урон в процентах",
						image = "https://rustlabs.com/img/items180/metal.plate.torso.png",
						amount = 10,
					},
					new _Conf.Boosts
					{
						boostname = "raider",
						name = "Рейдер",
						description = "Увеличивает урон по постройкам от взрывчатки",
						image = "https://rustlabs.com/img/items180/explosive.timed.png",
						amount = 10,
					},
					new _Conf.Boosts
					{
						boostname = "radiation",
						name = "Заражённый",
						description = "Заражает игрока радиацией и увеличивает на 5% наносимый ему урон",
						image = "https://rustlabs.com/img/items180/antiradpills.png",
						amount = 10,
					},
					new _Conf.Boosts
					{
						boostname = "moredamage",
						name = "Меткость",
						description = "Увеличивает наносимый игрокам урон",
						image = "https://rustlabs.com/img/items180/rifle.ak.png",
						amount = 10,
					},
                },
                Settings = new _Conf.Options
                {
                    chance = 15,
					togive = 30,
                    itemid = 1776460938,
					name = "Радиоактивная жижа",
					skin = 666,
                }              
            };
        }

        protected override void SaveConfig() => Config.WriteObject(config, true);
		
        private void UpdateConfigValues()
        {
            PrintWarning("Обновляем конфиг-файл");

            _Conf baseConfig = SetDefaultConfig();
        }
    }
}
