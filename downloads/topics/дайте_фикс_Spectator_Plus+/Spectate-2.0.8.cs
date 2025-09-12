using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;
using Network;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Configuration;

namespace Oxide.Plugins
{
	/* based on 0.4.2 version by Wolf */	
    [Info("Spectate", "Nimant", "2.0.8")]    

    class Spectate : RustPlugin
    {
        #region Initialization
     
		private bool debug = true;
	 
        private Dictionary<ulong, SpecInfo> Spectating = new Dictionary<ulong, SpecInfo>();
		
		private class SpecInfo
		{
			public ulong targetID;
			
			[NonSerialized]
			public BasePlayer target;
			
			public float prevPosX;
			public float prevPosY;
			public float prevPosZ;
			
			public float health;
            public float calories;
            public float hydration;
			
			[NonSerialized]
			public BasePlayer player;
			
			[NonSerialized]
			public ItemContainer main;
			[NonSerialized]
			public ItemContainer belt;
			[NonSerialized]
			public ItemContainer wear;
			
			public byte[] jsonMain;
			public byte[] jsonBelt;
			public byte[] jsonWear;
			
			public SpecInfo(ulong targetID, Vector3 pos)
			{
				this.targetID = targetID;
				prevPosX = pos.x;
				prevPosY = pos.y;
				prevPosZ = pos.z;
				
				main = new ItemContainer();
				main.ServerInitialize(null, 36);
				main.GiveUID();	
				belt = new ItemContainer();
				belt.ServerInitialize(null, 36);
				belt.GiveUID();	
				wear = new ItemContainer();
				wear.ServerInitialize(null, 36);
				wear.GiveUID();	
			}
		}
		
        private const string permModer = "spectate.moder";
		private const string permHide = "spectate.hide";		
		
		private Dictionary<string, string> Messages = new Dictionary<string, string>()
		{
			{ "NotAllowed", "Недостаточно прав!" },
			{ "NoParams", "Укажите цель: spectate <steamID>" },
			{ "NotAllowedTP", "Запрещено пользоватся телепортацией" },
			{ "NotAllowedCraft", "Сперва отмените крафт" },
			{ "NotAllowedChat", "Запрещено использовать команды чата" },
			{ "NoValidTargets", "Цель не найдена" },
			{ "TargetSleep", "Цель отключена или спит" },
			//{ "NoBuildingPriv", "Включать режим спектатора разрешено только в своей билдинг зоне" },
			{ "PlayersOnly", "Невозможно использовать данную команду в консоле" },
			{ "SpectateSelf", "Вы не можете наблюдать за собой" },
			{ "SpectateStart", "Вы стали наблюдать за {0} ({1})" },
			{ "SpectateStop", "Режим наблюдения отключен" },			
			{ "SpectateAbort", "Режим наблюдения отключен: цель отключилась" },
			{ "TargetIsSpectating", "Указанная цель находится в режиме спектатора" },
			{ "TargetIsAdmin", /*"Наблюдение за этим игроком запрещено"*/"Цель не найдена" },
			{ "NotAllowIfMove", "Запрещено наблюдать за игроком, если вы на транспорте" }
		};

        private void Init()
        {            
			permission.RegisterPermission(permModer, this);
			permission.RegisterPermission(permHide, this);			
        }
		
		private void OnServerInitialized()
		{
			LoadData();
			RunFullMetabolismControl();
		}	
		
		private void Unload()
		{
			foreach (BasePlayer player in BasePlayer.activePlayerList.Where(x=>Spectating.ContainsKey(x.userID)).ToList())
			{
				SpectateToggle(player);
				PrintToConsole(player, Messages["SpectateStop"]);  
			}
		}

        #endregion        

        #region Commands

		[ConsoleCommand("spectate")]
        private void ConsSpectate(ConsoleSystem.Arg arg)
        {
            if (arg == null) return;			
			var player = arg?.Player();
			if (player==null)
			{
				Puts(Messages["PlayersOnly"]);
                return;
			}	
			
			int mode = player.IsAdmin?2:(permission.UserHasPermission(player.UserIDString, permModer)?1:0);
			
			if (mode==0)
            {
                PrintToConsole(player, Messages["NotAllowed"]);
                return;
            }  
			
			if (!Spectating.ContainsKey(player.userID))
			{
				if (arg.Args==null || arg.Args?.Length==0)
				{
					PrintToConsole(player, Messages["NoParams"]);
                    return;
				}									
				
				/*if (mode==1 && !(player.GetBuildingPrivilege(player.WorldSpaceBounds()) != null && player.CanBuild(player.WorldSpaceBounds())))
				{
					PrintToConsole(player, Messages["NoBuildingPriv"]);
                    return;
				}*/	
				
				if (player.inventory.crafting.queue.Count>0)
				{
					PrintToConsole(player, Messages["NotAllowedCraft"]);
                    return;
				}								
				
				BasePlayer target = null;		
				
				if (!debug)
					target = BasePlayer.Find(string.Join(" ", arg.Args.Select(v => v.ToString()).ToArray()));
				else
					target = BaseNetworkable.serverEntities.OfType<BasePlayer>().Where(x=>x.UserIDString == string.Join(" ", arg.Args.Select(v => v.ToString()).ToArray())).FirstOrDefault();
				
                if (target == null || target.IsDead() || !IsConnected(target))
                {                    
					PrintToConsole(player, Messages["NoValidTargets"]);
                    return;
                }
				
				if (target.IsSleeping())
                {                    
					PrintToConsole(player, Messages["TargetSleep"]);
                    return;
                }
								
				if (ReferenceEquals(target, player))
                {                    
					PrintToConsole(player, Messages["SpectateSelf"]);
                    return;
                }
				
                if (target.IsSpectating())
                {                    
					PrintToConsole(player, Messages["TargetIsSpectating"]);
                    return;
                }
								
				if (target.IsAdmin || permission.UserHasPermission(target.UserIDString, permHide)) 
                {
					PrintToConsole(player, Messages["TargetIsAdmin"]);                    
                    return;
                }
								
				SpectateToggle(player, target);				
				PrintToConsole(player, string.Format(Messages["SpectateStart"], target.displayName, target.UserIDString));
			}	
			else
			{				
				SpectateToggle(player);
                PrintToConsole(player, Messages["SpectateStop"]);                
			}				
		}			        

        #endregion
		
		#region Main				
		
		private void RunFullMetabolismControl()
		{
			foreach (BasePlayer player in BasePlayer.activePlayerList.Where(x=>Spectating.ContainsKey(x.userID)).ToList())
			{
				if (Spectating[player.userID].target == null || !IsConnected(Spectating[player.userID].target))
				{	
					SpectateToggle(player);
					PrintToConsole(player, Messages["SpectateAbort"]); 
				}	
				else	
					SetMaxMetabolism(player);
			}	
			timer.Once(1f, ()=> RunFullMetabolismControl());
		}
		
		private void SetMaxMetabolism(BasePlayer player)
		{			
			player.InitializeHealth(player.StartMaxHealth(), player.StartMaxHealth());
			player.metabolism.calories.@value = player.metabolism.calories.max;
			player.metabolism.hydration.@value = player.metabolism.hydration.max;            
		}
		
		private void SendEntitySnapshot(BasePlayer player, BaseNetworkable ent)
		{
			using (TimeWarning timeWarning = TimeWarning.New("SendEntitySnapshot", 0))
			{
				if (ent != null)
				{
					if (ent.net != null)
					{
						if (ent.ShouldNetworkTo(player))
						{
							if (Net.sv.write.Start())
							{
								player.net.connection.validate.entityUpdates = player.net.connection.validate.entityUpdates + 1;
								BaseNetworkable.SaveInfo saveInfo = new BaseNetworkable.SaveInfo()
								{
									forConnection = player.net.connection,
									forDisk = false
								};
								BaseNetworkable.SaveInfo saveInfo1 = saveInfo;
								Net.sv.write.PacketID(Message.Type.Entities);
								Net.sv.write.UInt32(player.net.connection.validate.entityUpdates);
								ent.ToStreamForNetwork(Net.sv.write, saveInfo1);
								Net.sv.write.Send(new SendInfo(player.net.connection));
							}
						}
					}
				}
			}
		}
		
		private void SpectateToggle(BasePlayer player, BasePlayer target = null)
		{
			if (player == null || !IsConnected(player)) return;						
			
			if (!Spectating.ContainsKey(player.userID))
			{				
				if (target == null || !IsConnected(target))
				{
					PrintToConsole(player, Messages["NoValidTargets"]);
                    return;
				}					
				
				if (player.HasParent())
				{
					PrintToConsole(player, Messages["NotAllowIfMove"]);
                    return;
				}
				 
				var lastPos = player.transform.position;                
				Spectating.Add(player.userID, new SpecInfo(target.userID, lastPos) );
				Spectating[player.userID].target = target;
				Spectating[player.userID].player = player;
                SavePlayer(player, Spectating[player.userID]);
				player.spectateFilter = target.UserIDString;
                player.SetPlayerFlag(BasePlayer.PlayerFlags.Spectating, true);
                player.gameObject.SetLayerRecursive(10);                	
                player.CancelInvoke("InventoryUpdate");							
				SetMaxMetabolism(player);
                player.ClearEntityQueue();                				
				SendEntitySnapshot(player, target);				
                player.gameObject.Identity();				
				if (target == null || !IsConnected(target) || target.IsSleeping())
				{					
					SpectateToggle(player, null);					
					PrintToConsole(player, Messages["TargetSleep"]);
                    return;
				}					
                player.SetParent(target, false, false);
                player.SetPlayerFlag(BasePlayer.PlayerFlags.ThirdPersonViewmode, true);				
                player.IPlayer.Command("camoffset 0,1.3,0");
				player.IPlayer.Command("camfov 70");
				player.IPlayer.Command("camdist 2.5");
				Log("", string.Format("Наблюдатель {0} ({1}) в точке {2} стал наблюдать за игроком {3} ({4}).", player.displayName, player.userID, player.transform.position, target.displayName, target.userID));
			}
			else
			{				
				var pos = new Vector3(Spectating[player.userID].prevPosX, Spectating[player.userID].prevPosY, Spectating[player.userID].prevPosZ);
				player.IPlayer.Command("camoffset", "0,1,0");				
				//SetParentNull(player, pos);
				player.SetParent(null);
				player.SetPlayerFlag(BasePlayer.PlayerFlags.ThirdPersonViewmode, false);
                player.SetPlayerFlag(BasePlayer.PlayerFlags.Spectating, false);
				player.spectateFilter = "";                
                player.gameObject.SetLayerRecursive(17);                                
				player.InvokeRepeating("InventoryUpdate", 1f, 0.1f * UnityEngine.Random.Range(0.99f, 1.01f));       
                player.StartSleeping();				
                RestorePlayer(player, Spectating[player.userID]);                				                				
				Teleport(player, pos);									
				Spectating.Remove(player.userID);				
				Log("", string.Format("Наблюдатель {0} ({1}) прекратил наблюдать за игроком.", player.displayName, player.userID));				
			}	
			SaveData();
		}
		
		private void SetParentNull(BasePlayer player, Vector3 pos)
		{									
			//player.transform.position = pos;
			BaseEntity parentEntity = player.GetParentEntity();
			if (parentEntity)			
				parentEntity.RemoveChild(player);						            	
			player.OnParentChanging(parentEntity, null);
			player.parentEntity.Set(null);			
			player.transform.SetParent(null, false);
			player.parentBone = 0;
			player.UpdateNetworkGroup();			
			player.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
			//player.SendChildrenNetworkUpdate();								
		}
		
		private void SavePlayer(BasePlayer player, SpecInfo info)
		{			
			info.health = player.health;
			info.calories = player.metabolism.calories.value;
			info.hydration = player.metabolism.hydration.value;									
						
			foreach(var item in player.inventory.containerMain.itemList.ToList())
				item.MoveToContainer(info.main, item.position);
			foreach(var item in player.inventory.containerBelt.itemList.ToList())
				item.MoveToContainer(info.belt, item.position);
			foreach(var item in player.inventory.containerWear.itemList.ToList())
				item.MoveToContainer(info.wear, item.position);															
		}
		
		private void RestorePlayer(BasePlayer player, SpecInfo info)
		{
			player.InitializeHealth(info.health, player.StartMaxHealth());
            player.metabolism.calories.@value = info.calories;
            player.metabolism.hydration.@value = info.hydration;            
				
			foreach(var item in info.main.itemList.ToList())
				item.MoveToContainer(player.inventory.containerMain, item.position);
			foreach(var item in info.belt.itemList.ToList())
				item.MoveToContainer(player.inventory.containerBelt, item.position);
			foreach(var item in info.wear.itemList.ToList())
				item.MoveToContainer(player.inventory.containerWear, item.position);
		}								
		
		#endregion

        #region Hooks

		private object OnServerCommand(ConsoleSystem.Arg arg)
		{							
			string command = arg?.cmd?.FullName?.ToLower();
			if (string.IsNullOrEmpty(command)) return null;									
			if (!command.Contains("chat.say")) return null;						
			var player = arg?.Player();
			if (player == null) return null;
			if (!Spectating.ContainsKey(player.userID)) return null;
			
			var str = arg.GetString(0, "").Trim(' ','	');
			if (str.StartsWith("/"))
			{
				SendReply(player, Messages["NotAllowedChat"]);		
				return false;
			}	
							
			return null;				
		}
		
		private object CanTeleport(BasePlayer player)
        {	
			if (player == null) return null;			
			if (Spectating.ContainsKey(player.userID))
				return Messages["NotAllowedTP"];
			
			return null;			
        }	
		
		private object OnEntityTakeDamage(BaseCombatEntity victim, HitInfo info)
		{			
			if (victim == null) return null;
            BasePlayer vict = victim.ToPlayer();
            if (vict == null) return null;			
							
			if (Spectating.ContainsKey(vict.userID))							
				return false;				

			return null;
		}
		
		/*private void OnPlayerDeath(BasePlayer target, HitInfo info)
		{			
			foreach(var player in Spectating.Where(x=>x.Value.target == null || !IsConnected(x.Value.target) || (target != null && x.Value.target == target) ))				
			{
				if (player.Value.player != null)
				{	
					SpectateToggle(player.Value.player);			
					PrintToConsole(player.Value.player, Messages["SpectateAbort"]);    
				}
			}	
		}*/
		
		private void OnPlayerConnected(BasePlayer player)
        {
			if (player == null) return;
            if (!Spectating.ContainsKey(player.userID)) return;
			
			if (player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot))
            {
                timer.Once(0.1f, () => OnPlayerConnected(player));
                return;
            }

            SpectateToggle(player);
        }		        

		private void OnPlayerDisconnected(BasePlayer target, string reason)
        {         
			if (target == null)
			{
				foreach (BasePlayer player in BasePlayer.activePlayerList.Where(x=>Spectating.ContainsKey(x.userID)).ToList())
				{
					if (Spectating[player.userID].target == null || !IsConnected(Spectating[player.userID].target))				
					{	
						SpectateToggle(player);
						PrintToConsole(player, Messages["SpectateAbort"]); 
					}						
				}	
			}
			
            if (Spectating.Values.ToList().Exists(x=>x.targetID==target.userID))
			{
				foreach(var pair in Spectating.ToDictionary(x=>x.Key, x=>x.Value))
				{
					if (pair.Value.targetID == target.userID)
					{
						var player = pair.Value.player;
						if (player == null) continue;						
						if (Spectating.ContainsKey(player.userID))
						{
							SpectateToggle(player);
							PrintToConsole(player, Messages["SpectateAbort"]);    
						}	
					}	
				}				
			}								
            if (!Spectating.ContainsKey(target.userID)) return;            
			SpectateToggle(target);
			PrintToConsole(target, Messages["SpectateAbort"]);    
        }		        
			
        #endregion

        #region Common
		
		private bool IsConnected(BasePlayer player)
		{
			if (debug)
				return true;
			
			return player != null && player.IsConnected;
		}
		
		private void Log(string filename, string text) 
		{			
			try
			{		
				LogToFile("info", "["+DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss")+"] " + text, this, false);		
			}
			catch 
			{
				timer.Once(1f, ()=> LogToFile("info", "["+DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss")+"] " + text, this, false));
			}
		}	
		
		private void Teleport(BasePlayer player, Vector3 position)
        {
			if (player==null || position==Vector3.zero) return;            
            if (player.net?.connection != null)
                player.ClientRPCPlayer(null, player, "StartLoading");            			
			if (!player.IsSleeping())
			{	
				player.SetPlayerFlag(BasePlayer.PlayerFlags.Sleeping, true);
				if (!BasePlayer.sleepingPlayerList.Contains(player))
					BasePlayer.sleepingPlayerList.Add(player);
				player.CancelInvoke("InventoryUpdate");    
			}			
            player.MovePosition(position);
            if (player.net?.connection != null)
                player.ClientRPCPlayer(null, player, "ForcePositionTo", position);
            if (player.net?.connection != null)
                player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
            player.UpdateNetworkGroup();            
            player.SendNetworkUpdateImmediate(false);
            if (player.net?.connection == null) return;
         
            try { player.ClearEntityQueue(null); } catch { }
            player.SendFullSnapshot();
        }				              

        #endregion

		#region Data
		
		private void LoadData()
		{
			Spectating = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<ulong, SpecInfo>>("SpectateData");		
			var arr = Spectating.Values.ToArray();
			for(int ii=arr.Length-1;ii>=0;ii--)
			{				
				arr[0].main.Load(ProtoBuf.ItemContainer.Deserialize(arr[0].jsonMain));
				arr[0].belt.Load(ProtoBuf.ItemContainer.Deserialize(arr[0].jsonBelt));
				arr[0].wear.Load(ProtoBuf.ItemContainer.Deserialize(arr[0].jsonWear));
			}
		}	

		private void SaveData()
		{
			var arr = Spectating.Values.ToArray();
			for(int ii=arr.Length-1;ii>=0;ii--)
			{
				arr[0].jsonMain = arr[0].main.Save().ToProtoBytes();
				arr[0].jsonBelt = arr[0].belt.Save().ToProtoBytes();
				arr[0].jsonWear = arr[0].wear.Save().ToProtoBytes();
			}	
			Interface.GetMod().DataFileSystem.WriteObject("SpectateData", Spectating);										
		}	
		
		#endregion
		
    }
}
