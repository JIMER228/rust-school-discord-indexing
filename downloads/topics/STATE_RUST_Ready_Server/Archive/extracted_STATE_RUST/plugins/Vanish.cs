using System.Collections.Generic;
using System.Linq;
using Network;
using Newtonsoft.Json;
using Rust;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Facepunch;

namespace Oxide.Plugins
{
	/* Переделан на базе версии 0.4.2 от Wulf/lukespragg (maintained by Jake_Rich) */
    [Info("Vanish", "Nimant", "1.0.4")]    
    public class Vanish : RustPlugin
    {
		
		#region Variables
		
		private const string PermUse = "vanish.access";
		private const string GuiName = "VanishIcon";
		private static List<ulong> SaveVanish = new List<ulong>();
		private static Dictionary<BasePlayer, bool> OnlinePlayers = new Dictionary<BasePlayer, bool>();
		
		#endregion
		              
        #region Hooks
						        
        private void Init()
        {            
            permission.RegisterPermission(PermUse, this);
			LoadDefaultMessages();			
			LoadSavedData();		

            AddCommandAliases("ChatCommandVanish", "VanishCommand");
			AddCommandAliases("ConsoleCommandVanish", "VanishCommand");

            Unsubscribe(nameof(CanNetworkTo));
            Unsubscribe(nameof(CanBeTargeted));
            Unsubscribe(nameof(CanBradleyApcTarget));
			Unsubscribe(nameof(CanHelicopterTarget));
            Unsubscribe(nameof(OnNpcTarget));
            Unsubscribe(nameof(OnEntityTakeDamage));
			Unsubscribe(nameof(OnSensorDetect));
        }

		private void OnServerInitialized()
		{
			foreach (var player in BasePlayer.activePlayerList.Where(x=> x != null && x.userID.IsSteamId()))
				OnPlayerConnected(player);
		}
		
		private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList.Where(x=> x != null && x.userID.IsSteamId()))
				CuiHelper.DestroyUi(player, GuiName);
        }
		
		private void OnPlayerConnected(BasePlayer player) 
		{
			if (player == null) return;
			
			if (!OnlinePlayers.ContainsKey(player))
				OnlinePlayers.Add(player, false);
			
			if (!player.IsAdmin && !HasPerm(player.UserIDString, PermUse)) return;
			
            if (player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot))
            {
                timer.Once(0.1f, () => OnPlayerConnected(player));
                return;
            }
			
			if (SaveVanish.Contains(player.userID))			
				Disappear(player);				
		}
		
		private void OnPlayerDisconnected(BasePlayer player)
		{
			if (player == null) return;
			OnlinePlayers.Remove(player);
		}
				
		private object OnEntityTakeDamage(BasePlayer player, HitInfo info)
        {
			if (player == null || info == null) return null;            
			
            if (OnlinePlayers.ContainsKey(player) && OnlinePlayers[player])
            {
				if (info != null)
				{
					info.damageTypes = new DamageTypeList();
					info.HitMaterial = 0;
					info.PointStart = Vector3.zero;
				}
                return false;
            }			            

            return null;
        }		
				
        private object CanNetworkTo(BasePlayer player, BasePlayer target)
        {            
            if (player == null || target == null || player == target) return null;            
            if (IsInvisible(player)) return false;

            return null;
        }
				
		private object CanNetworkTo(HeldEntity entity, BasePlayer target)
        {
			if (entity == null || target == null) return null;
            var player = entity.GetOwnerPlayer();
            if (player == null || player == target) return null;            
            if (IsInvisible(player)) return false;

            return null;
        }
        
        private object CanBeTargeted(BasePlayer player)
        {            
            if (player != null && IsInvisible(player)) return false;
            return null;
        }
		
		private object OnNpcTarget(BaseEntity npc, BasePlayer player)
        {            
            if (player != null && IsInvisible(player)) return false;
            return null;
        }

        private object CanBradleyApcTarget(BradleyAPC bradleyApc, BasePlayer player)
        {            
            if (player != null && IsInvisible(player)) return false;
            return null;
        }

        private object CanHelicopterTarget(PatrolHelicopterAI heli, BasePlayer player)
        {
            if (player != null && IsInvisible(player)) return false;
            return null;
        }
		
		private object OnSensorDetect(HBHFSensor sensor, BasePlayer player)
		{
			if (player != null && IsInvisible(player)) return false;
            return null;
		}
		
        #endregion                

        #region Vanish

        private void Disappear(BasePlayer player)
        {
            var connections = new List<Connection>();
            foreach (var basePlayer in BasePlayer.activePlayerList.Where(x=> x != null && x.userID.IsSteamId()))
            {
                if (player == basePlayer || !basePlayer.IsConnected) continue;                
                connections.Add(basePlayer.net.connection);
            }

            if (Net.sv.write.Start())
            {
                Net.sv.write.PacketID(Network.Message.Type.EntityDestroy);
                Net.sv.write.EntityID(player.net.ID);
                Net.sv.write.UInt8((byte)BaseNetworkable.DestroyMode.None);
                Net.sv.write.Send(new SendInfo(connections));
            }

            var item = player.GetActiveItem();
			var activeHeldEntity = item?.GetHeldEntity();
            if (activeHeldEntity != null && Net.sv.write.Start())
            {				
                Net.sv.write.PacketID(Network.Message.Type.EntityDestroy);
                Net.sv.write.EntityID(activeHeldEntity.net.ID);
                Net.sv.write.UInt8((byte)BaseNetworkable.DestroyMode.None);
                Net.sv.write.Send(new SendInfo(connections));
            }
			
			foreach(var heldEntity in GetAllHolsteredHeldEntity(player))
			{
				if (activeHeldEntity != null && heldEntity == activeHeldEntity) continue;				
				if (heldEntity != null && Net.sv.write.Start())
				{				
					Net.sv.write.PacketID(Network.Message.Type.EntityDestroy);
					Net.sv.write.EntityID(heldEntity.net.ID);
					Net.sv.write.UInt8((byte)BaseNetworkable.DestroyMode.None);
					Net.sv.write.Send(new SendInfo(connections));
				}	
			}
			
            VanishGui(player);           

            player.ChatMessage(Lang("VanishEnabled", player.UserIDString));
			
			if (!OnlinePlayers.ContainsKey(player))
				OnlinePlayers.Add(player, true);
			
            OnlinePlayers[player] = true;
			
			if (!SaveVanish.Contains(player.userID))
				SaveVanish.Add(player.userID);
			
			SaveData();

            //Remove player from Grid so animals can't target it (HACKY SOLUTION)
            //Is good for now, as the only thing that uses this grid is AI, so removing it only prevents AI from finding player
            //Player is added back to grid when reappearing
            BaseEntity.Query.Server.RemovePlayer(player);

            Subscribe(nameof(CanNetworkTo));
            Subscribe(nameof(CanBeTargeted));
			Subscribe(nameof(OnNpcTarget));
            Subscribe(nameof(CanBradleyApcTarget));
			Subscribe(nameof(CanHelicopterTarget));            
            Subscribe(nameof(OnEntityTakeDamage));
			Subscribe(nameof(OnSensorDetect));
        }
		
		private List<HeldEntity> GetAllHolsteredHeldEntity(BasePlayer player)
		{
			List<HeldEntity> list = Pool.GetList<HeldEntity>();
			List<HeldEntity> resultList = new List<HeldEntity>();
			Item[] itemArray = player.inventory.AllItems();	
			
			for (int i = 0; i < (int)itemArray.Length; i++)
			{
				Item item = itemArray[i];
				if (item.info.isHoldable)
				{
					if (item.GetHeldEntity() != null)
					{
						HeldEntity component = item.GetHeldEntity().GetComponent<HeldEntity>();
						if (component != null)
						{
							list.Add(component);
						}
					}
				}
			}
			
			IOrderedEnumerable<HeldEntity> heldEntities = 
				from x in list
				orderby x.hostileScore descending
				select x;
			bool flag = true;
			bool flag1 = true;
			bool flag2 = true;
			
			IEnumerator<HeldEntity> enumerator = heldEntities.GetEnumerator();
			
			try
			{
				while (enumerator.MoveNext())
				{
					HeldEntity current = enumerator.Current;
					if (current != null)
					{
						if (current.holsterInfo.displayWhenHolstered)
						{
							if (flag2 && !current.IsDeployed() && current.holsterInfo.slot == HeldEntity.HolsterInfo.HolsterSlot.BACK)
							{								
								if (!resultList.Contains(current))
									resultList.Add(current);
								flag2 = false;
							}
							else if (flag1 && !current.IsDeployed() && current.holsterInfo.slot == HeldEntity.HolsterInfo.HolsterSlot.RIGHT_THIGH)
							{
								if (!resultList.Contains(current))
									resultList.Add(current);
								flag1 = false;
							}
							else if (!flag || current.IsDeployed() || current.holsterInfo.slot != HeldEntity.HolsterInfo.HolsterSlot.LEFT_THIGH)
							{
								// nothing
							}
							else
							{
								if (!resultList.Contains(current))
									resultList.Add(current);
								flag = false;
							}
						}
					}
				}
			}
			finally
			{				
				enumerator.Dispose();
			}
			Pool.FreeList<HeldEntity>(ref list);
			
			return resultList;
		}
		
		private void Reappear(BasePlayer player)
        {
			if (!OnlinePlayers.ContainsKey(player))
				OnlinePlayers.Add(player, false);
			
            OnlinePlayers[player] = false;    

			if (SaveVanish.Contains(player.userID))
				SaveVanish.Remove(player.userID);
			
			SaveData();			
			
			player.SendNetworkUpdate();         
            player.GetActiveItem()?.GetHeldEntity()?.SendNetworkUpdate();
			
			var heldEntity = player.GetHeldEntity();			
			if (heldEntity != null)				
				heldEntity.UpdateHeldItemVisibility();		
			
            CuiHelper.DestroyUi(player, GuiName);

            //Add player back to Grid so AI can find it
            BaseEntity.Query.Server.AddPlayer(player);

            player.ChatMessage(Lang("VanishDisabled", player.UserIDString));
            if (OnlinePlayers.Where(x=> x.Key != null && x.Value).Count() <= 0) 
			{
				Unsubscribe(nameof(CanNetworkTo));
				Unsubscribe(nameof(CanBeTargeted));
				Unsubscribe(nameof(CanBradleyApcTarget));
				Unsubscribe(nameof(CanHelicopterTarget));
				Unsubscribe(nameof(OnNpcTarget));
				Unsubscribe(nameof(OnEntityTakeDamage));
				Unsubscribe(nameof(OnSensorDetect));
			}
        }

        #endregion
        
        #region GUI		

        private void VanishGui(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, GuiName);
            var elements = new CuiElementContainer();            

            if (!string.IsNullOrEmpty(config.IconURL))
            {
                elements.Add(new CuiElement
                {
                    Name = GuiName,
                    Components =
                    {
                        new CuiRawImageComponent { Color = "1 1 1 1", Sprite = "assets/content/textures/generic/fulltransparent.tga", Url = config.IconURL },						
                        new CuiRectTransformComponent { AnchorMin = $"{config.IconPosX} {1-config.IconPosY-config.IconHeight}",  AnchorMax = $"{config.IconPosX+config.IconWidth} {1-config.IconPosY}" },
						new CuiOutlineComponent { Distance = "0.25 0.25", Color = "1 1 1 1" }
                    }
                });
            }            

            CuiHelper.AddUi(player, elements);
        }

        #endregion        

        #region Helpers

        private void AddCommandAliases(string key, string command)
        {
            foreach (var language in lang.GetLanguages(this))
            {
                var messages = lang.GetMessages(language, this);
                foreach (var message in messages.Where(m => m.Key.Equals(key))) AddCovalenceCommand(message.Value, command);
            }
        }

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private bool HasPerm(string id, string perm) => permission.UserHasPermission(id, perm);        

        private bool IsInvisible(BasePlayer player) => player != null && OnlinePlayers.ContainsKey(player) && OnlinePlayers[player];

        #endregion
		
		#region Commands

        private void VanishCommand(IPlayer player, string command, string[] args)
        {
			var basePlayer = player?.Object as BasePlayer;
            if (basePlayer == null) return;  
			
            if (!basePlayer.IsAdmin && !player.HasPermission(PermUse))
            {
                player.Reply(Lang("NotAllowedPerm", player.Id, PermUse));
                return;
            }                     
            
            if (IsInvisible(basePlayer)) Reappear(basePlayer);
            else Disappear(basePlayer);
        }		
		
        #endregion
		
		#region Localization

        private new void LoadDefaultMessages()
        {         
            lang.RegisterMessages(new Dictionary<string, string>
            {
				["ChatCommandVanish"] = "vanish",                
				["ConsoleCommandVanish"] = "vanish.toggle",
                ["VanishEnabled"] = "Вы вошли в режим невидимости",                
				["VanishDisabled"] = "Вы вышли из режима невидимости",
                ["NotAllowedPerm"] = "У вас недостаточно прав",
            }, this);
        }

        #endregion
		
		#region Configuration

        private static Configuration config;

        public class Configuration
        {
			[JsonProperty(PropertyName = "Иконка")] 
            public string IconURL;
			[JsonProperty(PropertyName = "Позиция иконки по вертикали (0.0 - 1.0)")]
			public float IconPosY;
			[JsonProperty(PropertyName = "Высота иконки (0.0 - 1.0)")]
			public float IconHeight;
			[JsonProperty(PropertyName = "Позиция иконки по горизонтали (0.0 - 1.0)")]
			public float IconPosX;
			[JsonProperty(PropertyName = "Ширина иконки (0.0 - 1.0)")]
			public float IconWidth;

            public static Configuration DefaultConfig() 
            {
                return new Configuration
                {
					IconURL = "https://i.imgur.com/Iau9yua.png",
					IconPosY = 0.917f,
					IconHeight = 0.062f,
					IconPosX = 0.161f,
					IconWidth = 0.045f
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config?.IconPosY == null) LoadDefaultConfig();
            }
            catch
            {                
                LoadDefaultConfig();
            }
            SaveConfig();
			timer.Once(0.1f, ()=> SaveConfig());
        }

        protected override void LoadDefaultConfig() => config = Configuration.DefaultConfig();

        protected override void SaveConfig() => Config.WriteObject(config);
		
		private void LoadSavedData() => SaveVanish = Interface.GetMod().DataFileSystem.ReadObject<List<ulong>>("VanishData");                    
                
        private void SaveData() => Interface.GetMod().DataFileSystem.WriteObject("VanishData", SaveVanish);    

        #endregion
		
    }
}
