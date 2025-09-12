using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("WipeBlock", "Hougan|Nimant", "1.0.1")]
    public class WipeBlock : RustPlugin
    {
        #region Variables

        [PluginReference] 
        private Plugin ImageLibrary;
		
		private static Dictionary<ulong, Timer> PlayerTimers = new Dictionary<ulong, Timer>();
        private const string Layer = "UI_InstanceBlock";        
        private const string LayerBlock = "UI_Block";                        
        
        #endregion

        #region Initialization 

		private void Init() => LoadVariables();
		
        private void OnServerInitialized()
        {
			PlayerTimers.Clear();
			
			ImageLibrary?.Call("AddImage", "https://i.imgur.com/pipCniL.png", "WB_lock");
			ImageLibrary?.Call("AddImage", "https://i.imgur.com/3p2yyNw.png", "WB_grid");
			
			foreach (BasePlayer target in BasePlayer.activePlayerList.Where(x=> x != null && x.userID.IsSteamId()))
			{							
				for(int ii = target.inventory.containerBelt.itemList.Count - 1; ii >= 0; ii--)
				{				
					var item = target.inventory.containerBelt.itemList[ii];
					if (IsBlocked(item.info) > 0)
					{						
						if (!item.MoveToContainer(target.inventory.containerMain, -1, true))
							item.Drop(target.inventory.containerBelt.dropPosition, target.inventory.containerBelt.dropVelocity, new Quaternion());
					}						
				}
				
				for(int ii = target.inventory.containerWear.itemList.Count - 1; ii >= 0; ii--)
				{		
					var item = target.inventory.containerWear.itemList[ii];
					if (IsBlocked(item.info) > 0)
					{						
						if (!item.MoveToContainer(target.inventory.containerMain, -1, true))
							item.Drop(target.inventory.containerWear.dropPosition, target.inventory.containerWear.dropVelocity, new Quaternion());
					}						
				}
			}
        }

		private void Unload()
        {
			foreach (BasePlayer player in BasePlayer.activePlayerList)
			{
				CuiHelper.DestroyUi(player, Layer);
				CuiHelper.DestroyUi(player, LayerBlock);
			}
        }
		
        #endregion

        #region Hooks
        
        private bool? CanWearItem(PlayerInventory inventory, Item item)
        {
			if (inventory == null || item == null) return null;			
            var player = inventory.gameObject.ToBaseEntity() as BasePlayer;
			if (!player.UserIDString.IsSteamId()) return null;
			
            var isBlocked = IsBlocked(item.info) > 0 ? false : (bool?) null;
            if (isBlocked == false)            
                DrawInstanceBlock(player, item.info);                
            
            return isBlocked;
        }

        private bool? CanEquipItem(PlayerInventory inventory, Item item)
        {
			if (inventory == null || item == null) return null;
			
            var player = inventory.gameObject.ToBaseEntity() as BasePlayer;
            if (player == null) return null;
			if (!player.UserIDString.IsSteamId()) return null;
            
            var isBlocked = IsBlocked(item.info) > 0 ? false : (bool?) null;
            if (isBlocked == false)            
                DrawInstanceBlock(player, item.info);               
            
            return isBlocked;
        }

        private object OnReloadWeapon(BasePlayer player, BaseProjectile projectile)
        {
			if (player == null || projectile == null) return null;			
            if (!player.UserIDString.IsSteamId()) return null;

            var isBlocked = IsBlocked(projectile.primaryMagazine.ammoType) > 0 ? false : (bool?) null;
            if (isBlocked == false)            
                DrawInstanceBlock(player, projectile.primaryMagazine.ammoType);
			
            return isBlocked;
        }
        
        object OnReloadMagazine(BasePlayer player, BaseProjectile projectile)
        {
            if (player == null || projectile == null) return null;			
            if (!player.UserIDString.IsSteamId()) return null;

            var isBlocked = IsBlocked(projectile.primaryMagazine.ammoType) > 0 ? false : (bool?) null;
            if (isBlocked == false)
			{
                DrawInstanceBlock(player, projectile.primaryMagazine.ammoType);
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
                if (player == null) return null;                

                var isBlocked = IsBlocked(item.info.shortname) > 0 ? false : (bool?) null;
                if (isBlocked == false)
                {
                    DrawInstanceBlock(player, item.info);
                    return ItemContainer.CanAcceptResult.CannotAcceptRightNow;
                }
            }

            return null;
        }

        #endregion

        #region GUI

        [ConsoleCommand("block")]
        private void cmdConsoleDrawBlock(ConsoleSystem.Arg args) => DrawBlockGUI(args?.Player());

        [ChatCommand("block")]
        private void cmdChatDrawBlock(BasePlayer player) => DrawBlockGUI(player);
        
        private void DrawBlockGUI(BasePlayer player)
        {
			if (player == null) return;
			
            CuiHelper.DestroyUi(player, LayerBlock);
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0.1554687 0.1030325", AnchorMax = "0.1554687 0.1030325", OffsetMax = "883 596" },
                Image = { Color = "0 0 0 0" }
            }, "Hud", LayerBlock);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "-100 -100", AnchorMax = "100 100", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Close = LayerBlock },
                Text = { Text = "" }
            }, LayerBlock);            

            container.Add(new CuiElement
            {
                Parent = LayerBlock,
                Name = LayerBlock + ".Header",
                Components =
                {
                    new CuiImageComponent { Color = HexToCuiColor("#aae9f2FF") },
                    new CuiRectTransformComponent { AnchorMin = "0 0.9286154", AnchorMax = "1.015 0.9998464", OffsetMax = "0 0" }					
                }
            });

            container.Add(new CuiElement
            {
                Parent = LayerBlock + ".Header",
                Components =
                {
                    new CuiTextComponent { Color = HexToCuiColor("#333333"), Text = "Блокировка предметов после вайпа", FontSize = 30, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    new CuiOutlineComponent { Distance = "0.155 0.155", Color = HexToCuiColor("#515151FF")}					
                }
            });
            
            Dictionary<string, Dictionary<int, string>> blockedItemsGroups = new Dictionary<string, Dictionary<int, string>>();
            FillBlockedItems(blockedItemsGroups);
            var blockedItemsNew = blockedItemsGroups.OrderByDescending(p => p.Value.Count);

            int newString = 0;
            for (int t = 0; t < blockedItemsNew.Count(); t++)
            {
                var blockedCategory = blockedItemsNew.ElementAt(t).Value.OrderBy(p => IsBlocked(p.Value));                				
				
                container.Add(new CuiElement
                {
                    Parent = LayerBlock,
                    Name = LayerBlock + ".Category", 
                    Components =
                    {
                        new CuiImageComponent { Color = HexToCuiColor("#717171FF") },
                        new CuiRectTransformComponent { AnchorMin = $"0 {0.879  - (t) * 0.18 - newString * 0.12}", AnchorMax = $"1.015 {0.915  - (t) * 0.18 - newString * 0.12}", OffsetMax = "0 0" }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = LayerBlock + ".Category",
                    Components =
                    {
                        new CuiTextComponent { Color = HexToCuiColor("#EEEEEEFF"), Text = $"{blockedItemsNew.ElementAt(t).Key}", FontSize = 16, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" }						
                    }
                });
				
                for (int i = 0; i < blockedCategory.Count(); i++)
                {
                    if (i == 12)
                        newString++;
                    
                    var blockedItem = blockedCategory.ElementAt(i);
					
					var shortname = ItemManager.FindItemDefinition(blockedItem.Key)?.shortname;
					
                    container.Add(new CuiElement
                    {
                        Parent = LayerBlock,
                        Name = LayerBlock + $".{shortname}",
                        Components =
                        {
                            new CuiImageComponent { FadeIn = 0.5f, Color = HexToCuiColor((blockedItem.Value + "99")) },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = $"{0.00868246 + i * 0.0837714 - (Math.Floor((double) i / 12) * 12 * 0.0837714)}" +
                                            $" {0.7518223 - (t) * 0.18 - newString * 0.12}", 
                                
                                AnchorMax = $"{0.08415613 + i * 0.0837714 - (Math.Floor((double) i / 12) * 12 * 0.0837714)}" +
                                            $" {0.8636619  - (t) * 0.18 - newString * 0.12}", OffsetMax = "0 0"
                            },
                            new CuiOutlineComponent { Distance = "2 2", Color = HexToCuiColor("#000000FF")}
                        }
                    });
                    
                    var ID = (string) ImageLibrary?.Call("GetImage", shortname);
                    
                    container.Add(new CuiElement
                    {
                        Parent = LayerBlock + $".{shortname}",
                        Components =
                        {
                            new CuiRawImageComponent { FadeIn = 0.5f,  Png = ID },
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                        }
                    });

                    string text = IsBlocked(shortname) > 0
                        ? $@"<size=13></size>\n<size=14>{TimeSpan.FromSeconds((int) IsBlocked(shortname)).ToString("hh\\:mm")}</size>"
                        : "<size=13></size>";
                    
					if (IsBlocked(shortname) > 0)
					{
						var str = (string) plugins.Find("ImageLibrary")?.Call("GetImage", "WB_lock");
						if (!string.IsNullOrEmpty(str))
						{
							container.Add(new CuiElement
							{
								Parent = LayerBlock + $".{shortname}",
								Components =
								{
									new CuiRawImageComponent { FadeIn = 0.5f,  Png = str, Color = "1 1 1 0.5" },
									new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
								}
							});
						}
					}
					
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 0.1", AnchorMax = "1 1", OffsetMax = "0 0" },
                        Text = { FadeIn = 0.5f,Text = text, FontSize = 10, Font = "robotocondensed-regular.ttf", Align = TextAnchor.LowerCenter},
                        Button = { Color = "0 0 0 0" },
                    }, LayerBlock + $".{shortname}");
                }
				
				var grid = (string) plugins.Find("ImageLibrary")?.Call("GetImage", "WB_grid");
				
				if (!string.IsNullOrEmpty(grid))
				{
					for (int i = blockedCategory.Count()%12 == 0 ? 12 : blockedCategory.Count()%12; i < 12; i++)
					{
						container.Add(new CuiElement
						{
							Parent = LayerBlock,
							Name = LayerBlock + $".{t}.{i}",
							Components =
							{
								new CuiImageComponent { FadeIn = 0.5f, Color = "1 1 1 0.6", Png = grid },
								new CuiRectTransformComponent
								{
									AnchorMin = $"{0.00868246 + i * 0.0837714 - (Math.Floor((double) i / 12) * 12 * 0.0837714)}" +
												$" {0.7518223 - (t) * 0.18 - newString * 0.12}", 
									
									AnchorMax = $"{0.08415613 + i * 0.0837714 - (Math.Floor((double) i / 12) * 12 * 0.0837714)}" +
												$" {0.8636619  - (t) * 0.18 - newString * 0.12}", OffsetMax = "0 0"
								},
								new CuiOutlineComponent { Distance = "2 2", Color = HexToCuiColor("#000000FF")}
							}
						});
					}
				}
                        
            }

            CuiHelper.AddUi(player, container);
        }

        private void DrawInstanceBlock(BasePlayer player, ItemDefinition info)
        {
			if (player == null || info == null) return;
			
			var flag = false;
			
			if (!PlayerTimers.ContainsKey(player.userID))
				PlayerTimers.Add(player.userID, null);
			else
				if (PlayerTimers[player.userID] != null)
				{
					PlayerTimers[player.userID].Destroy();
					flag = true;
				}
			
			PlayerTimers[player.userID] = timer.Once(3f, ()=>
			{
				if (player != null)
				{
					CuiHelper.DestroyUi(player, Layer + ".Destroy1");
					CuiHelper.DestroyUi(player, Layer + ".Destroy2");
                    CuiHelper.DestroyUi(player, Layer + ".Destroy3");
                    CuiHelper.DestroyUi(player, Layer + ".Destroy5");
				}				    
                PlayerTimers[player.userID] = timer.Once(1f, ()=> 
				{
					if (player != null)
						CuiHelper.DestroyUi(player, Layer);
				});
			});
			
			if (!flag)
				CuiHelper.DestroyUi(player, Layer);
			else
			{				
				CuiHelper.DestroyUi(player, Layer + ".Destroy1");
				CuiHelper.DestroyUi(player, Layer + ".Destroy2");
				CuiHelper.DestroyUi(player, Layer + ".Destroy3");
				CuiHelper.DestroyUi(player, Layer + ".Destroy5");
				
				PlayerTimers[player.userID] = timer.Once(1f, ()=> 
				{
					if (player != null)											
						CuiHelper.DestroyUi(player, Layer);
				});
			}
			
            CuiElementContainer container = new CuiElementContainer();
            string inputText = "Предмет <b>{name}</b> временно заблокирован,\nподождите {1}".Replace("{name}", info.displayName.english).Replace("{1}", $"{TimeToString((long)Math.Round(TimeSpan.FromSeconds(IsBlocked(info)).TotalSeconds))}");
            
            container.Add(new CuiPanel
            {
                FadeOut = 0.5f,
                Image = { FadeIn = 0.5f, Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0.3 0.75", AnchorMax = "0.7 0.95" },
                CursorEnabled = false
            }, "Overlay", Layer);
            
            container.Add(new CuiElement
            {
                FadeOut = 0.5f,
                Parent = Layer,
                Name = Layer + ".Hide",
                Components =
                {
                    new CuiImageComponent { Color = "0 0 0 0", FadeIn = 0.5f },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });
            container.Add(new CuiElement
            {
                Parent = Layer + ".Hide",
                Name = Layer + ".Destroy1",
                FadeOut = 0.5f,
                Components =
                {
                    new CuiImageComponent { Color = HexToCuiColor("#aae9f2FF"), FadeIn = 0.5f},
                    new CuiRectTransformComponent { AnchorMin = "0 0.62", AnchorMax = "1 0.85" }
                }               
            });
			container.Add(new CuiElement
            {
                Parent = Layer + ".Hide",
                Name = Layer + ".Destroy2",
                FadeOut = 0.5f,
                Components =
                {
                    new CuiImageComponent { Color = "0.4 0.4 0.4 0.6", FadeIn = 0.5f},
                    new CuiRectTransformComponent { AnchorMin = "0 0.2", AnchorMax = "1 0.6" }
                }               
            });
            container.Add(new CuiLabel
            {
                FadeOut = 0.5f,
                Text = {FadeIn = 0.5f, Color = HexToCuiColor("#333333"), Text = "ПРЕДМЕТ ЗАБЛОКИРОВАН", FontSize = 22, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, Layer + ".Destroy1", Layer + ".Destroy5");            
            container.Add(new CuiLabel
            {
                FadeOut = 0.5f,
                Text = {FadeIn = 0.5f, Text = inputText, FontSize = 18, Align = TextAnchor.MiddleCenter, Color = "0.85 0.85 0.85 1" , Font = "robotocondensed-regular.ttf"},
                RectTransform = { AnchorMin = "0.04 0", AnchorMax = "0.96 0.85" }
            }, Layer + ".Hide", Layer + ".Destroy3");
            
			CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Functions        
        
        private double IsBlocked(string shortname)
        {
            if (!configData.BlockedItems.SelectMany(p => p.Value).Contains(shortname))
                return 0;

            var blockTime = configData.BlockedItems.FirstOrDefault(p => p.Value.Contains(shortname)).Key;
            var lefTime = (UnBlockTime(blockTime)) - CurrentTime();
            
            return lefTime > 0 ? lefTime : 0;
        }

        private double UnBlockTime(int amount) => SaveRestore.SaveCreatedTime.ToUniversalTime().Subtract(epoch).TotalSeconds + amount;

        private double IsBlocked(ItemDefinition itemDefinition) => IsBlocked(itemDefinition.shortname);
		
		private double IsBlocked(int itemID) => IsBlocked(ItemManager.FindItemDefinition(itemID)?.shortname);
		
		private void FillBlockedItems(Dictionary<string, Dictionary<int, string>> fillDictionary)
        {
            foreach (var items in configData.BlockedItems)
            {
				foreach (var bItem in items.Value)
				{
					var category = configData.NiceCategories.FirstOrDefault(x=> x.Value.Contains(bItem)).Key;
					if (string.IsNullOrEmpty(category))
						category = "N|A";
					
					var info = ItemManager.FindItemDefinition(bItem);
					if (info == null) continue;
					
					if (!fillDictionary.ContainsKey(category))
						fillDictionary.Add(category, new Dictionary<int, string>());

					if (!fillDictionary[category].ContainsKey(info.itemid))
						fillDictionary[category].Add(info.itemid, "#777777");
				}
            }
        }

        #endregion

        #region Utils

        private static readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);
        private static double CurrentTime() { return DateTime.UtcNow.Subtract(epoch).TotalSeconds; }
        
		private static string HexToCuiColor(string hex)
        {
            if (string.IsNullOrEmpty(hex))            
                hex = "#FFFFFFFF";            
 
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
            return $"{color.r:F2} {color.g:F2} {color.b:F2} {color.a:F2}";
        }
		
		private static bool IsElementExists(int val, int[] mas) {
			foreach (int elem in mas)
				if (elem==val)
					return true;
			return false;
		}
		
		private static string GetStringDays(int days)
		{
			if (IsElementExists(days, (new int[] {1,21})))
				return days.ToString()+" день";
			else if (IsElementExists(days, (new int[] {2,3,4,22,23,24})))
				return days.ToString()+" дня";
			else return days.ToString()+" дней";						
		}
		
		private static string GetStringHours(int hours)
		{
			if (IsElementExists(hours, (new int[] {1,21})))
				return hours.ToString()+" час";
			else if (IsElementExists(hours, (new int[] {2,3,4,22,23,24})))
				return hours.ToString()+" часа";
			else return hours.ToString()+" часов";						
		}
		
		private static string GetStringMinutes(int minutes)
		{
			if (IsElementExists(minutes, (new int[] {1,21,31,41,51})))
				return minutes.ToString()+" минуту";
			else if (IsElementExists(minutes, (new int[] {2,3,4,22,23,24,32,33,34,42,43,44,52,53,54})))
				return minutes.ToString()+" минуты";
			else return minutes.ToString()+" минут";						
		}
		
		private static string GetStringSeconds(int seconds)
		{
			if (IsElementExists(seconds, (new int[] {1,21,31,41,51})))
				return seconds.ToString()+" секунду";
			else if (IsElementExists(seconds, (new int[] {2,3,4,22,23,24,32,33,34,42,43,44,52,53,54})))
				return seconds.ToString()+" секунды";
			else return seconds.ToString()+" секунд";						
		}
		
		private string TimeToString(long time)
        {
            TimeSpan elapsedTime = TimeSpan.FromSeconds(time);
            int hours = elapsedTime.Hours;
            int minutes = elapsedTime.Minutes;
            int seconds = elapsedTime.Seconds;
            int days = Mathf.FloorToInt((float)elapsedTime.Days);
            string s = "";
			int count = 0;

            if (days > 0) 
			{	
				s += GetStringDays(days) + ", ";
				if (++count==2) return s.Trim(' ',',').Replace(","," и");
			}	
            if (hours > 0) 
			{					
				s += GetStringHours(hours) + ", ";
				if (++count==2) return s.Trim(' ',',').Replace(","," и");
			}	
            if (minutes > 0) 
			{
				s += GetStringMinutes(minutes) + ", ";
				if (++count==2) return s.Trim(' ',',').Replace(","," и");
			}	
            if (seconds > 0)
			{
				s += GetStringSeconds(seconds) + ", ";
				if (++count==2) return s.Trim(' ',',').Replace(","," и");
			}	            					
			
            return s.Trim(' ',',').Replace(","," и"); 
        }
		
        #endregion
		
		#region Config
		
        private static ConfigData configData;
		
        private class ConfigData
        {            
			[JsonProperty("Заблокированные предметы")]
			public Dictionary<int,List<string>> BlockedItems = new Dictionary<int,List<string>>();
			[JsonProperty("Красивые названия категорий")]
			public Dictionary<string, List<string>> NiceCategories = new Dictionary<string, List<string>>();
        }
		
        private void LoadVariables() => configData = Config.ReadObject<ConfigData>();        
		
        protected override void LoadDefaultConfig()
        {
            configData = new ConfigData
            {
				BlockedItems = new Dictionary<int,List<string>>
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
				},
				NiceCategories = new Dictionary<string, List<string>>()
				{
					{ "Оружие", new List<string>() { "pistol.revolver",	"shotgun.double", "flamethrower", "pistol.python",
													 "pistol.semiauto", "shotgun.pump", "shotgun.spas12", "pistol.m92", 
													 "smg.mp5", "smg.2", "smg.thompson", "rifle.semiauto", "rifle.bolt",
													 "rifle.ak",	"rifle.lr300", "rocket.launcher", "lmg.m249" } },
					{ "Взрывчатка", new List<string>() { "explosive.satchel", "grenade.f1",	"grenade.beancan", "surveycharge", 
														 "ammo.rifle.explosive", "ammo.rocket.basic", "ammo.rocket.fire",
														 "ammo.rocket.hv", "explosive.timed", } },
					{ "Броня", new List<string>() { "metal.facemask", "metal.plate.torso", "heavy.plate.helmet",
													"heavy.plate.jacket", "heavy.plate.pants" } }
				}
            };
            SaveConfig(configData);
			timer.Once(0.1f, ()=> SaveConfig(configData));
        }        
		
        private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
		
        #endregion
    }
}