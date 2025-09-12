using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Rust;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Facepunch;

namespace Oxide.Plugins
{
    [Info("Mush", "https://discord.gg/9vyTXsJyKR", "1.0.0")]
    public class Mush : RustPlugin
    {
        #region Classess

        private class Case
        {
            [JsonProperty("Количество грибов")]
            public int MushAmount;
            [JsonProperty("Ссылка на изображение")]
            public string ImageURL;
            [JsonProperty("СкинИД предмета")]
            public ulong SkinID;
            [JsonProperty("Предметы выпадающие (shortname:кол-во)")]
            public List<List<string>> Items = new List<List<string>>();
        }

        private class Configuration
        {
            [JsonProperty("Скин ИД гриба")]
            public ulong SkinID;
            [JsonProperty("Количество грибов на карте")]
            public int MushAmount = 1000;
            [JsonProperty("Доступные кейсы для получения")]
            public List<Case> Cases = new List<Case>();

            public static Configuration Generate()
            {
                return new Configuration
                {
                    Cases = new List<Case>
                    {
                        new Case
                        {
                            MushAmount = 10,
                            ImageURL = "https://i.imgur.com/aFicpws.png",
                            SkinID = 123,
                            Items = new List<List<string>>
                            {
                                new List<string> { "rifle.ak:1" },
                                new List<string> { "wood:1000" }
                            }
                        },
                        new Case
                        {
                            MushAmount = 20,
                            ImageURL   = "https://i.imgur.com/aFicpws.png",
                            SkinID = 123,
                            Items = new List<List<string>>
                            {
                                new List<string> { "rifle.ak:1" },
                                new List<string> { "wood:1000" }
                            }
                        },
                        new Case
                        {
                            MushAmount = 30,
                            ImageURL   = "https://i.imgur.com/aFicpws.png",
                            SkinID = 123,
                            Items = new List<List<string>>
                            {
                                new List<string> { "rifle.ak:1" },
                                new List<string> { "wood:1000" }
                            }
                        },
                        new Case
                        {
                            MushAmount = 40,
                            ImageURL   = "https://i.imgur.com/aFicpws.png",
                            SkinID = 123,
                            Items = new List<List<string>>
                            {
                                new List<string> { "rifle.ak:1" },
                                new List<string> { "wood:1000" }
                            }
                        },
                        new Case
                        {
                            MushAmount = 50,
                            ImageURL   = "https://i.imgur.com/aFicpws.png",
                            SkinID = 123,
                            Items = new List<List<string>>
                            {
                                new List<string> { "rifle.ak:1" },
                                new List<string> { "wood:1000" }
                            }
                        },
                    }
                };
            }
        }

        private class DataBase
        {
            public Dictionary<ulong, PlayerDataBase> Players = new Dictionary<ulong, PlayerDataBase>();
        }

        private class PlayerDataBase
        {
            public int FullCount;
            public int Count;
        }

        #endregion

        #region Variables

        [PluginReference] 
		private Plugin ImageLibrary;
		
		private static System.Random Rnd = new System.Random();
		private static List<ulong> NeedInitPlayers = new List<ulong>();
		
        private static DataBase Base = new DataBase();
        private static Configuration Settings;
		private static List<BaseEntity> MushList = new List<BaseEntity>();
		private bool init = false;
		
		public List<string> CaseItems = new List<string>()
        {
            "https://i.imgur.com/7Mi4uQj.png",
            "https://i.imgur.com/J7WAiqy.png",
            "https://i.imgur.com/CCl6cR0.png",
            "https://i.imgur.com/khDM9Tf.png",
            "https://i.imgur.com/Qn9BGET.png",
            "https://i.imgur.com/fANuZsi.png",
            "https://i.imgur.com/WpDKf0n.png",
            "https://i.imgur.com/IYHgljB.png",
            "https://i.imgur.com/RAHGm4t.png"
        };

        #endregion

        #region Initialization 

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                Settings = Config.ReadObject<Configuration>();
            }
            catch
            {
                PrintWarning($"Error reading config, creating one new config!");
                LoadDefaultConfig();
            }

            SaveConfig();
        }
		
		private void LoadData()
        {
            try
            {
                Base = Interface.Oxide.DataFileSystem.ReadObject<DataBase>(Name);
                if (Base == null)
                    Base = new DataBase();
            }
            catch
            {
                Base = new DataBase();
            }
            init = true;
        }
		
		private void SaveData()
        {
            if (Base != null)
            Interface.Oxide.DataFileSystem.WriteObject(Name, Base);
        }

        protected override void LoadDefaultConfig() => Settings = Configuration.Generate();
        protected override void SaveConfig() => Config.WriteObject(Settings);
		
		#endregion             

		#region Hooks
		
        private void OnNewSave()
        {
            if (!init)
            {
                timer.Once(1f, () => OnNewSave());
                return;
            }
            Base.Players.Clear();
            SaveData();

            PrintWarning("Wiped detected! Clear data players");
        }                
        
		private void OnPlayerConnected(BasePlayer player)
        {
            if (!Base.Players.ContainsKey(player.userID))
            {
                Base.Players.Add(player.userID, new PlayerDataBase() { Count = 0, FullCount = 0 });
            }
			
			if (!NeedInitPlayers.Contains(player.userID))
				NeedInitPlayers.Add(player.userID);
        }  
		
        private void OnServerInitialized()
        {
            PrintWarning("\n-----------------------------\n" +
            "     Author - https://topplugin.ru/\n" +
            "     VK - https://vk.com/rustnastroika\n" +
            "     Discord - https://discord.com/invite/5DPTsRmd3G\n" +
            "-----------------------------");
			MushList.Clear();
            LoadData();
			DisableDefaultMushes();
            
            CaseItems.ForEach(image => ImageLibrary.Call("AddImage", image, image));
            ImageLibrary.Call("AddImage", "https://i.imgur.com/r3Zp9Dq.png", "Mush_BG");                        
            ImageLibrary.Call("AddImage", "https://i.imgur.com/x9MrQGP.png", "Mush_IC");
            ImageLibrary.Call("AddImage", "https://i.imgur.com/8uu3MSz.png", "Mush_Close");
            ImageLibrary.Call("AddImage", "https://i.imgur.com/597k7Xs.png", "Mush_Left"); 
            ImageLibrary.Call("AddImage", "https://i.imgur.com/twY7dLp.png", "Mush_Right");

            foreach (var check in Settings.Cases)            
                ImageLibrary.Call("AddImage", check.ImageURL, $"Mush.{Settings.Cases.IndexOf(check)}");            

            BasePlayer.activePlayerList.ToList().ForEach(OnPlayerConnected);						            		
			CommunityEntity.ServerInstance.StartCoroutine(SpawnToads());			            
        }
		
		private void Unload()
        {
            MushList.ForEach(p => { if (p != null && !p.IsDestroyed) p.Kill(); });
            SaveData();
			
			foreach (var player in BasePlayer.activePlayerList)
			{
				CuiHelper.DestroyUi(player, LayerMain);
				CuiHelper.DestroyUi(player, InitPanel);
			}
        }

        object CanCombineDroppedItem(DroppedItem drItem, DroppedItem anotherDrItem)
        {
            if (drItem.item.info.itemid == -1002156085 && drItem.item.info.itemid == anotherDrItem.item.info.itemid && drItem.item.skin != anotherDrItem.item.skin) return false;
            if (drItem.item.info.itemid == 844440409 && drItem.item.info.itemid == anotherDrItem.item.info.itemid && drItem.item.skin != anotherDrItem.item.skin) return false;
            if (drItem.item.skin != anotherDrItem.item.skin) return false;

            return null;
        }

        object CanStackItem(Item item, Item anotherItem)
        {
            if (item.info.itemid == -1002156085 && item.skin != anotherItem.skin) return false;
            if (item.info.itemid == 844440409 && item.skin != anotherItem.skin) return false;
            if (item.skin != anotherItem.skin) return false;
            return null;
        }
		
		object OnItemAction(Item item, string action, BasePlayer player)
        {
            if (item.info.shortname != "easter.goldegg") return null;

            var enter = Settings.Cases.FirstOrDefault(p => (ulong)p.SkinID == item.skin);
            if (enter == null) return null;

            var result = enter.Items.GetRandom();
            if (result.Any(p => !p.Contains(":")))
            {
                if (Oxide.Core.Random.Range(0, 100) > 50)
                {
                    OnItemAction(item, action, player);
                    return false;
                }
            }

            foreach (var check in result)
            {
                if (check.Contains(":"))
                {
                    var items = ItemManager.CreateByPartialName(check.Split(':')[0], int.Parse(check.Split(':')[1]));
                    if (!items.MoveToContainer(player.inventory.containerMain))
                    {
                        items.Drop(player.transform.position, Vector3.zero);
                        player.ChatMessage($"У вас не хватило места, предметы выбрашены на пол!");
                    }
                }
                else
                {
                    player.ChatMessage($"Вы получили <color=orange>редкую</color> привилегию!");
                    Server.Command(check.Replace("%STEAMID%", player.UserIDString));
                }
            }

            if (item.amount > 1)
                item.amount--;
            else item.DoRemove();

            return false;
        }

        private object OnItemSplit(Item item, int amount)
        {
            if (Settings.Cases.Any(p => p.SkinID == item.skin))
            {
                Item x = ItemManager.CreateByPartialName(item.info.shortname, amount);
                x.name = item.name;
                x.skin = item.skin;
                item.amount -= amount;
                item.MarkDirty();
                x.MarkDirty();
                return x;
            }

            if (item.info.shortname == "mushroom" && item.skin == Settings.SkinID)
            {
                Item byItemId = ItemManager.CreateByItemID(item.info.itemid, 1, item.skin);
                item.amount -= amount;
                byItemId.amount = amount;
                byItemId.name = item.name;
                item.MarkDirty();
                return byItemId;
            }

            return null;
        }
     
        private void OnCollectiblePickup(Item item, BasePlayer player)
        {
            if (item.info.shortname != "mushroom") return;
            if (!Base.Players.ContainsKey(player.userID))
            {
                Base.Players.Add(player.userID, new PlayerDataBase() { Count = 0, FullCount = 0 });
            }								
            item.skin = Settings.SkinID;
            Base.Players[player.userID].Count+=item.amount;
            Base.Players[player.userID].FullCount+=item.amount;

			timer.Once(1.5f, ()=>
			{
				if (player != null)
				{
					string position = player.transform.position.ToString();
					var ent = CreateMush();
					var randomPos = GetRandomPosition();
					ent.transform.position = (Vector3)randomPos;
					ent.Spawn();
					MushList.Add(ent);
				}
			});
        }
		
		#endregion
		
		#region Fill
		
		private void DisableDefaultMushes()
		{
			var allSpawnPopulations = SpawnHandler.Instance.AllSpawnPopulations;
            SpawnHandler.Instance.StopCoroutine("SpawnTick");
                        			
			SpawnDistribution[] spawndists = SpawnHandler.Instance.SpawnDistributions;
			for (int i = 0; i < allSpawnPopulations.Length; i++)
			{				
				if (!(allSpawnPopulations[i] == null) && allSpawnPopulations[i].name.Contains("mushroom"))
				{										
					allSpawnPopulations[i]._targetDensity = 0;			
					SpawnHandler.Instance.SpawnInitial(allSpawnPopulations[i], spawndists[i]);					
				}
			}			
            
            SpawnHandler.Instance.StartCoroutine("SpawnTick");
						
			foreach (var mush in BaseNetworkable.serverEntities.Where(p => p != null && p.PrefabName.Contains("mushroom-cluster-")).ToList())
				if (mush != null && !mush.IsDestroyed)				
					mush.Kill();
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
		
        private BaseEntity CreateMush()
        {
            BaseEntity entity = InstantiateEntity($@"assets/bundled/prefabs/autospawn/collectable/mushrooms/mushroom-cluster-{Rnd.Next(1,3)==1?"5":"6"}.prefab", new Vector3(), new Quaternion());
			return entity;
        }
		
		private static Vector3 GetRandomPosition()
        {
            var pos = Vector3.zero + new Vector3(Oxide.Core.Random.Range(-World.Size / 2.2f, World.Size / 2.2f), 0, Oxide.Core.Random.Range(-World.Size / 2.2f, World.Size / 2.2f));
            pos.y = TerrainMeta.HeightMap.GetHeight(pos);

            if (pos.y < 0.2f)
                return GetRandomPosition();
			
            List<BaseEntity> entities = Facepunch.Pool.GetList<BaseEntity>();
            Vis.Entities(pos, 1.5f, entities);
            int count = entities.Count;
            if (count > 0)						
                return GetRandomPosition();

            return pos;
        }  
		
		private IEnumerator SpawnToads()
		{
			for (int i = 0; i < Settings.MushAmount; i++)
            {
                var ent = CreateMush();
                var randomPos = GetRandomPosition();
                if (randomPos == null) continue;
                ent.transform.position = randomPos;
                ent.Spawn();
                MushList.Add(ent);
				
				if (i % 3 == 0)
					yield return null;
            }
			
			PrintWarning($"The plugin Mush was loaded successfully, mushrooms on the server added: {Settings.MushAmount}, current {BaseNetworkable.serverEntities.Where(p => p.PrefabName.Contains("mushroom-cluster-")).ToList().Count}");
			timer.Every(310, SaveData);
		}        
        
        #endregion

        #region Player Commands

		[ConsoleCommand("mush.top")]
        private void cmdMushTop10(ConsoleSystem.Arg args)
        {
            if (args.Args == null) return;
            int amount;
            if (!int.TryParse(args.Args[0], out amount)) return;
            var top = Base.Players.ToList().OrderByDescending(p=> p.Value.FullCount).Select(p=> $"{covalence.Players.FindPlayerById(p.Key.ToString()).Name}: {p.Value.FullCount}").Take(amount);
            args.ReplyWith(string.Join("\n", top));
        }
		
		[ChatCommand("grib")]
        private void CmdChatMush(BasePlayer player, string command, string[] args)
        {
            if (args == null || args.Length <= 0)
            {
				if (NeedInitPlayers.Contains(player.userID))
				{
					DoInitImages(player);
					NeedInitPlayers.Remove(player.userID);
					timer.Once(0.1f, ()=> InitializeExperimentalInterface(player, -1, 0, true));
				}						
				else
					InitializeExperimentalInterface(player, -1, 0, true);
			                
                return;
            }
            if (args != null && args[0] == "top")
            {
                int i = 1;
                var top = Base.Players.ToList().OrderByDescending(p => p.Value.FullCount).Select(p => $"{i++} <color=#8ABB50>{covalence.Players.FindPlayerById(p.Key.ToString()).Name}</color>: {p.Value.FullCount}").Take(10);
                SendReply(player, $"ТОП 10 грибников за текущий вайп:\n{string.Join("\n", top)}" );
            }
        }
		
		#endregion
		
		#region Commands

        [ConsoleCommand("UI_Mush")]
        private void CmdConsoleHandler(ConsoleSystem.Arg args)
        {
            var player = args.Player();
            if (!player || !args.HasArgs(1)) return;

            switch (args.Args[0].ToLower())
            {
                case "open":
                    {
                        int index;
                        if (!args.HasArgs(2) || !int.TryParse(args.Args[1], out index)) return;

                        var enter = Settings.Cases.FirstOrDefault(p => p.MushAmount == index);
                        if (enter == null || enter.MushAmount > Base.Players[player.userID].Count) return;

                        if (player.inventory.containerMain.capacity <= player.inventory.containerMain.itemList.Count)
                        {
                            player.ChatMessage($"У вас недостаточно места в инвентаре!");
                            return;
                        }

                        Base.Players[player.userID].Count -= enter.MushAmount;
                        player.ChatMessage($"Вы получили подарок в инвентарь!");

                        var item = ItemManager.CreateByName("easter.goldegg", 1, (ulong)enter.SkinID);
                        item.name = "Ящик с подарком";
                        item.MoveToContainer(player.inventory.containerMain);

                        InitializeExperimentalInterface(player,-1, Settings.Cases.IndexOf(enter) / 4);
                        break;
                    }
                case "page":
                    {
						var pg = int.Parse(args.Args[1]);
						
                        if (pg < 0 || pg > (int)Math.Ceiling(Settings.Cases.Count/4f)-1)
							return;						                        

                        InitializeExperimentalInterface(player, -1, pg);
                        break;
                    }
            }
        }

		[ConsoleCommand("grib_selectcase")]
        private void cmdSelectGriCase(ConsoleSystem.Arg args)
        {
            var player = args.Player();
            int casse = int.Parse(args.Args[0]);
            int page = int.Parse(args.Args[1]);
            InitializeExperimentalInterface(player, casse, page, false, true);
        }   		        

        #endregion

        #region GUI
		
		private static string LayerMain = "UI_Main123";
		private static string Layer = "UI_MushLayer";
		private static string LayerCase = "UI_CaseLayer";
		private const string InitPanel = "MushInitPanel";
		
		private void InitImage(ref CuiElementContainer container, string png, string ipanel)
		{
			container.Add(new CuiElement
			{
				Name = CuiHelper.GetGuid(),
				Parent = ipanel,				
				Components =
				{
					new CuiRawImageComponent { Png = png },
					new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
				}
			});
		}
		
		private void DoInitImages(BasePlayer player)
		{
			if (player == null) return;									
			var container = new CuiElementContainer();			
			
			container.Add(new CuiPanel
			{				
				RectTransform = { AnchorMin = "1.1 1.1", AnchorMax = "1.2 1.2" },
				CursorEnabled = false
			}, "Overlay", InitPanel);
			
			InitImage(ref container, (string)ImageLibrary.Call("GetImage", "Mush_BG"), InitPanel);
			InitImage(ref container, (string)ImageLibrary.Call("GetImage", "Mush_IC"), InitPanel);
			InitImage(ref container, (string)ImageLibrary.Call("GetImage", "Mush_Close"), InitPanel);
			InitImage(ref container, (string)ImageLibrary.Call("GetImage", "Mush_Left"), InitPanel);
			InitImage(ref container, (string)ImageLibrary.Call("GetImage", "Mush_Right"), InitPanel);
			
			foreach (var check in Settings.Cases)
				InitImage(ref container, (string)ImageLibrary.Call("GetImage", $"Mush.{Settings.Cases.IndexOf(check)}"), InitPanel);
				
			CaseItems.ForEach(image => InitImage(ref container, (string)ImageLibrary.Call("GetImage", image), InitPanel));
			
			CuiHelper.DestroyUi(player, InitPanel);
			CuiHelper.AddUi(player, container);	
		}

        private void InitializeExperimentalInterface(BasePlayer player,int type = -1, int page = 0, bool isStart = false, bool isCase = false)
        {
			CuiElementContainer container = new CuiElementContainer();			
			var fadeIn = 0f;
			
			if (isStart)
			{
				fadeIn = 0.5f;
				timer.Once(2f, ()=> { if (player != null) CuiHelper.DestroyUi(player, InitPanel); });;
				CuiHelper.DestroyUi(player, LayerMain);												
				
				container.Add(new CuiPanel
				{
					CursorEnabled = true,
					RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
					Image = { Color = "0 0 0 0" }
				}, "Overlay", LayerMain);											
				
				container.Add(new CuiElement
				{
					Parent = LayerMain,
					Components =
					{
						new CuiImageComponent {Color = "0.5 0.5 0.5 0.4", Material = "", FadeIn = fadeIn},
						new CuiRectTransformComponent {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"-330 -150", OffsetMax = $"330 200"}
					}
				});
				container.Add(new CuiElement
				{
					Parent = LayerMain,
					Components =
					{
						new CuiImageComponent {Color             = "0.3 0.3 0.3 0.8", Material = "", FadeIn = fadeIn},
						new CuiRectTransformComponent {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"-320 -55", OffsetMax = $"320 140"}
					}
				});												

				container.Add(new CuiElement
				{
					Parent = LayerMain,
					Components =
					{
						new CuiRawImageComponent {Png             = (string) ImageLibrary.Call("GetImage", "Mush_BG"), FadeIn = fadeIn},
						new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"-250 140", OffsetMax = $"250 235" }
					}
				});	

				container.Add(new CuiElement
				{
					Name = LayerMain + "FFF",
					Parent = LayerMain,
					Components =
					{
						new CuiImageComponent {Color  = "0.3 0.3 0.3 0.8", Material = "", FadeIn = fadeIn},
						new CuiRectTransformComponent {AnchorMin = "0.5 0.5", AnchorMax  = "0.5 0.5", OffsetMin = $"-320 -140", OffsetMax = $"170 -60"}
					}
				});	
				
				container.Add(new CuiElement
				{
					Parent = LayerMain,
					Components =
					{
						new CuiImageComponent {Color = "0.3 0.3 0.3 0.8", Material = "", FadeIn = fadeIn},
						new CuiRectTransformComponent {AnchorMin = "0.5 0.5", AnchorMax        = "0.5 0.5", OffsetMin = $"230 -140", OffsetMax = $"320 -105"}
					}
				});
				
				container.Add(new CuiElement
				{
					Parent = LayerMain,
					Name = LayerMain + ".D",
					Components =
					{
						new CuiRawImageComponent {Png            = (string) ImageLibrary.Call("GetImage", $"Mush_IC"), FadeIn = fadeIn},
						new CuiRectTransformComponent {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"235 -138", OffsetMax = $"270 -108"}
					}
				});
				
				container.Add(new CuiElement
				{
					Name = LayerMain + ".L",
					Parent = LayerMain,
					Components =
					{
						new CuiRawImageComponent {Png             = (string) ImageLibrary.Call("GetImage", "Mush_Left"), FadeIn = fadeIn},
						new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"-315 30", OffsetMax = $"-290 60" }
					}
				});
				container.Add(new CuiElement
				{
					Name = LayerMain + ".R",
					Parent = LayerMain,
					Components =
					{
						new CuiRawImageComponent {Png             = (string) ImageLibrary.Call("GetImage", "Mush_Right"), FadeIn = fadeIn},
						new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"290 30", OffsetMax = $"315 60" }
					}
				});
				container.Add(new CuiElement
				{
					Name = LayerMain + ".C",
					Parent = LayerMain,
					Components =
					{
						new CuiRawImageComponent {Png             = (string) ImageLibrary.Call("GetImage", "Mush_Close"), FadeIn = fadeIn},
						new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"285 155", OffsetMax = $"320 190" }
					}
				});
				
				//CuiHelper.AddUi(player, container);
				//return;
			}
			
			if (!isCase)
			{
				CuiHelper.DestroyUi(player, LayerCase);
				
				container.Add(new CuiPanel
				{                				
					RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
					Image = { Color = "0 0 0 0" }
				}, LayerMain, LayerCase);
			}
			
            CuiHelper.DestroyUi(player, Layer);

            container.Add(new CuiPanel
            {                				
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, LayerMain, Layer);
			
			container.Add(new CuiButton
			{
				RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
				Button = { Color = "0 0 0 0", Material = "", Close = LayerMain },
				Text = { Text = "" }
			}, Layer);
			
			container.Add(new CuiElement
			{
				Parent = Layer,
				Components =
				{
					new CuiImageComponent {Color = "0 0 0 0", Material = ""},
					new CuiRectTransformComponent {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"-330 -150", OffsetMax = $"330 200"}
				}
			});

			container.Add(new CuiElement
			{
				Name = LayerMain + ".T",
				Parent = Layer,
				Components =
				{
					new CuiImageComponent {Color  = "0 0 0 0", Material = ""},
					new CuiRectTransformComponent {AnchorMin = "0.5 0.5", AnchorMax  = "0.5 0.5", OffsetMin = $"-320 -140", OffsetMax = $"170 -60"}
				}
			});			

            if (type < 0)
            {
                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 0", OffsetMax = "0 0" },
                    Text = { Text = "Собирайте грибы и покупайте за них ящики с предметами.\nЧем выше уровень, тем лучше предметы.", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-bold.ttf", FontSize = 19, FadeIn = fadeIn }
                }, LayerMain + ".T");
            }
            else
            {
                container.Add(new CuiElement
                {
                    Parent = LayerMain + ".T",
                    Components =
                {
                    new CuiRawImageComponent {Color = "1 1 1 1", Png = (string)ImageLibrary.Call("GetImage", CaseItems[type]), FadeIn = fadeIn},
                    new CuiRectTransformComponent {AnchorMin = "0 -0.01", AnchorMax = "1 0.98"}
                }
                });
            }            						            

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"285 155", OffsetMax = $"320 190" },
                Button = { Color = "0 0 0 0", Close = LayerMain },
                Text = { Text = "" }
            }, Layer);
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"-315 30", OffsetMax = $"-290 60" },
                Button = { Color = "0 0 0 0", Command = $"UI_Mush page {page - 1}" },
                Text = { Text = "" }
            }, Layer);
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"290 30", OffsetMax = $"315 60" },
                Button = { Color = "0 0 0 0", Command = $"UI_Mush page {page + 1}" },
                Text = { Text = "" }
            }, Layer);

            var list = Settings.Cases.Skip(page * 4).Take(4);
            if (!list.Any())
            {
                InitializeExperimentalInterface(player, type, page - 1);
                return;
            }
									
			foreach (var check in list.Select((i, t) => new { A = i, B = t - 2 }))
			{
				if (!isCase)
				{
					container.Add(new CuiElement
					{
						Name = LayerMain + check.B,
						Parent = LayerCase,
						Components =
						{
							new CuiRawImageComponent {Png = (string) ImageLibrary.Call("GetImage", $"Mush.{Settings.Cases.IndexOf(check.A)}"), FadeIn = fadeIn},
							new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"{check.B * 143} -25", OffsetMax = $"{150 + check.B * 143} 145" }
						}
					});
				}

				string text = "ПОКАЗАТЬ";
				string color = "0.33 0.52 0.71 1.00";

				if (type >= 0 && type == Settings.Cases.IndexOf(check.A))
				{
					text = Base.Players[player.userID].Count < check.A.MushAmount ? "ЗАКРЫТО" : "КУПИТЬ";
					color = Base.Players[player.userID].Count < check.A.MushAmount ? "0.74 0.51 0.38 1.00" : "0.54 0.73 0.31 1.00";
				}
				
				container.Add(new CuiButton
				{
					RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"{7 + check.B * 143 + 5} -50", OffsetMax = $"{150 + check.B * 143 - 5} -25" },
					Button = { Color = color, FadeIn = fadeIn, Command = type > -1 && type == Settings.Cases.IndexOf(check.A) ? $"UI_Mush open {check.A.MushAmount}": $"grib_selectcase {Settings.Cases.IndexOf(check.A)} {page}" },
					Text = { Text = text, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 20, FadeIn = fadeIn }
				}, Layer);
			}			

            var info = Base.Players[player.userID];
            container.Add(new CuiLabel()
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"250 -140", OffsetMax = $"315 -105" },
                Text = { Text = info.Count.ToString(), Align = TextAnchor.MiddleRight, Font = "robotocondensed-bold.ttf", FontSize = 22, FadeIn = fadeIn }
            }, Layer);

            CuiHelper.AddUi(player, container);
        }            
       
        #endregion
        
    }
}