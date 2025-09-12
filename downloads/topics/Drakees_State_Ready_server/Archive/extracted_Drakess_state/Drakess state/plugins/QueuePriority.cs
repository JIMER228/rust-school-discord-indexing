using Network;
using System;
using System.Linq;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Configuration;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("QueuePriority", "Nimant", "3.0.1")]
    class QueuePriority : RustPlugin
    {                       						
		
		#region Variables
		
		private static List<Connection> Queue;		
		private static Dictionary<ulong, bool> IsPlayerVIP = new Dictionary<ulong, bool>();		
		private static List<string> AllowPermissions = new List<string>() { "queuepriority.access", "bypassqueue.allow" };
		private static int AttemptCount = 100;

		private bool ShopExists = false;
		private string Request;
		private static Dictionary<ulong, int> PlayerStoreResult = new Dictionary<ulong, int>();
		private static int CheckIndex;
		private static int DelayCount;

		#endregion
		
		#region Hooks
		
		private void Init() 
		{
			LoadVariables();
			
			if (!(string.IsNullOrEmpty(configData.SHOP_ID) || string.IsNullOrEmpty(configData.SECRET_KEY) || string.IsNullOrEmpty(configData.SERVER_ID)))
				ShopExists = true;
			
			Request = $"http://gamestores.ru/api/?shop_id={configData.SHOP_ID}&secret={configData.SECRET_KEY}&server={configData.SERVER_ID}";
			
			foreach(var perm in AllowPermissions)
				permission.RegisterPermission(perm, this);
		}		
		
		private void OnServerInitialized() => GetQueue();		
		
		#endregion
		
		#region Main
		
		private void GetQueue()
		{
			if (Queue == null && AttemptCount > 0)
			{	
				try
				{
					ServerMgr instance = SingletonComponent<ServerMgr>.Instance;	
					if (instance == null || instance?.connectionQueue == null) return;					
					Queue = instance?.connectionQueue.queue;
				}
				catch {}		
				
				AttemptCount = Queue == null ? AttemptCount - 1 : 0;								
			}
			
			if (Queue == null && AttemptCount > 0)
			{
				timer.Once(1f, ()=> GetQueue());
				return;
			}
			
			if (Queue == null)							
				PrintWarning("Ошибка получения доступа к очереди. Плагин неактивен.");				
			else				
				CheckTimer();
		}
		
		private void CheckTimer()
		{
			CheckVipPlayers();
			PrioritySort();
			
			timer.Once(0.5f, CheckTimer);
		}
		
		private void CheckVipPlayers()
		{
			if (!ShopExists || Queue == null || Queue?.Count <= 1) return;
			
			if (CheckIndex >= Queue?.Count)
				CheckIndex = 0;
			
			if (DelayCount >= configData.DelayCount)
			{
				if (!HasPermission(Queue[CheckIndex].userid))
				{
					CheckStoreCommands(Queue[CheckIndex].userid);					
					DelayCount = 0;
				}
				CheckIndex++;
			}
			else
				DelayCount++;
		}
		
		private void PrioritySort()
		{
			try
			{				
				if (Queue == null || Queue?.Count <= 1) return;				
				
				var commonIndex = GetFirstCommonIndex();
				if (commonIndex == -1 || commonIndex == int.MaxValue) return;
				
				var vipIndex = GetFirstVipIndexToMove();
				if (vipIndex == -1 || vipIndex == int.MaxValue) return;
				
				SwapQueueIndexes(commonIndex, vipIndex);								
			}
			catch (Exception ex)
			{
				PrintWarning("Ошибка замены игроков в очереди: "+ex.Message);										
			}				
		}
		
		private void SwapQueueIndexes(int commonIndex, int vipIndex)
        {
            var tmp = Queue[commonIndex]; // первый в очереди обычный
            Queue[commonIndex] = Queue[vipIndex]; // поставили на его место первого випера			

            for (int ii = vipIndex - 1; ii >= commonIndex + 1; ii--)                            
                Queue[ii+1] = Queue[ii];

            Queue[commonIndex + 1] = tmp;
        }
		
		private int GetFirstCommonIndex()
		{
			if (Queue == null || Queue.Count <= 1)
				return -1;
			
			for (int ii=0;ii<Queue.Count;ii++)			
				if (!HasPermission(Queue[ii].userid))
					return ii;
			
			return int.MaxValue;
		}
		
		private int GetFirstVipIndexToMove()
		{
			if (Queue == null || Queue.Count <= 1)
				return -1;
			
			var index = GetFirstCommonIndex();
			if (index == -1 || index == int.MaxValue)
				return -1;
			
			for (int ii=index+1;ii<Queue.Count;ii++)			
				if (HasPermission(Queue[ii].userid))
					return ii;
			
			return int.MaxValue;
		}
		
		#endregion

		#region Helpers
		
		private bool HasPermission(ulong steamId)
		{													
			foreach(var perm in AllowPermissions)
				if (permission.UserHasPermission(steamId.ToString(), perm))
					return true;
				
			return false;
		}                		
		
		#endregion
		
		#region Shop
		
		private int CheckExistsAndExecCommands(string response, int code, ulong userID)
        {
            switch (code)
            {
                case 0:                    
                    return 1; /* магазин временно не доступен */                   
                case 200:
                    Dictionary<string, object> Response = JsonConvert.DeserializeObject<Dictionary<string, object>>(response, new KeyValuesConverter());
                    if (Response != null && response != null && response != "null")
                    {    
						if (!Response.ContainsKey("code"))													
							break;
						
                        switch (System.Convert.ToInt32(Response["code"]))
                        {
                            case 100:
								if (!Response.ContainsKey("data"))																	
									break;									
								
                                List<object> data = Response["data"] as List<object>;                                
                                
                                foreach (object pair in data)
                                {
                                    Dictionary<string, object> iteminfo = pair as Dictionary<string, object>;

                                    if (iteminfo.ContainsKey("command"))
                                    {
                                        string command = iteminfo["command"].ToString().ToLower().Replace('\n', '|').Replace("%steamid%", userID.ToString());
                                        String[] CommandArray = command.Split('|');
                                        foreach (var substring in CommandArray)
                                        {                         											
											if (substring.ToLower().Contains(" "+configData.MagazGroup.ToLower()+" "))
											{
												ConsoleSystem.Run(ConsoleSystem.Option.Server, substring);
												Puts($"Была выполнена команда магазина: {substring}");
												SendResult(new Dictionary<string, string>() { { "gived", "true" }, { "id", $"{iteminfo["id"]}" } });																										
												IsPlayerVIP.Remove(userID);													
												return 1000; /* в товарах есть подходящее слово */
											}												
                                        }                                                                                
                                    }									
                                }
                                break;
                            case 104:                                                                                                
                                break;
                        }
						return 100; /* товар получен или его не было */	
                    }                    
					                    
                    return 2; /* неизвестная ошибка */
                case 404:                    
                    return 3; /* магазин не доступен */
            }
			
			return 4; /* неизвестный код ответа */
        }                
        
        private void CheckStoreCommands(ulong userID)
        {            			
			var Args = new Dictionary<string, string>() { { "items", "true" }, { "steam_id", userID.ToString() } };
			string Request = $"{this.Request}&{string.Join("&", Args.Select(x => x.Key + "=" + x.Value).ToArray())}";            												
			webrequest.EnqueueGet(Request, (code, res) => 
			{ 					
				CheckExistsAndExecCommands(res, code, userID); 																						
			}, this);
        }				       

		private void SendResult(Dictionary<string, string> Args)
        {
            string Request = $"{this.Request}&{string.Join("&", Args.Select(x => x.Key + "=" + x.Value).ToArray())}";
            webrequest.EnqueueGet(Request, (code, res) => {}, this);
        }
		
		#endregion
		
		#region Config        				
		
        private static ConfigData configData;
		
        private class ConfigData
        {            					
			[JsonProperty(PropertyName = "Задержка перед следующей проверкой корзины игрока (секунды)")]
			public int DelayCount;
			[JsonProperty(PropertyName = "SHOP.ID")]
			public string SHOP_ID;
			[JsonProperty(PropertyName = "SERVER.ID")]
			public string SERVER_ID;
			[JsonProperty(PropertyName = "SECRET.KEY")]
			public string SECRET_KEY;
			[JsonProperty(PropertyName = "Группа с привилегией для очереди (для магазина с мгновенной активацией)")]
			public string MagazGroup;
        }
		
        private void LoadVariables() => configData = Config.ReadObject<ConfigData>();        
		
        protected override void LoadDefaultConfig()
        {
            configData = new ConfigData
            {
				DelayCount = 3,
                SHOP_ID = "",
				SERVER_ID = "",
				SECRET_KEY = "",
				MagazGroup = "entrance"
            };
            SaveConfig(configData);
			timer.Once(0.1f, ()=> SaveConfig(configData));
        }        
		
        private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
		
        #endregion	

    }
}
