using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Plugins;
using Newtonsoft.Json;
using Oxide.Game.Rust;

namespace Oxide.Plugins
{
    [Info("Log Logger", "Nimant", "1.1.10")]
    class Logger : RustPlugin
    {	        
		
		#region Variables				
								
		private static ILogHandler OldLogger = null;
								
		private static List<LogNfo> QueueBuffer = new List<LogNfo>();
		private static List<PlayerInfo> Online = new List<PlayerInfo>();		
		
		private static List<string> DefaultExcludeWords = new List<string>()
		{
			"Kinematic body only supports Speculative Continuous collision detection",
			"Skipped frame because GfxDevice",
			"Your current multi-scene setup has inconsistent Lighting",
			"HandleD3DDeviceLost",            
			"ResetD3DDevice",            
			"dev->Reset",            
			"D3Dwindow device not lost anymore",            
			"D3D device reset",            
			"group < 0xfff",            
			"Mesh can not have more than 65000 vert",
			"Trying to add (Layout Rebuilder for)",            
			"Coroutine continue failure",            
			"No texture data available to upload",            
			"Trying to reload asset from disk that is not",            
			"Unable to find shaders used for the terrain engine.",            
			"Canvas element contains more than 65535 vertices",            
			"RectTransform.set_anchorMin",            
			"FMOD failed to initialize the output device",            
			"Cannot create FMOD::Sound",            
			"invalid utf-16 sequence",            
			"missing surrogate tail",            
			"Failed to create agent because it is not close enough to the Nav",            
			"user-provided triangle mesh descriptor is invalid",            
			"Releasing render texture that is set as"            				
		};
		
		private class LogNfo
		{
			public string prefix;
			public string date;
			public string text;
			public LogNfo(string prefix, string date, string text)
			{
				this.prefix = prefix;
				this.date = date;
				this.text = text;
			}
		}
		
		private class PlayerInfo
		{
			public ulong userID;
			public string userName;
			public string userIP;
			public string serverDisconnectReason;
			public string clientDisconnectReason;
			public Timer disconnectTimer;
			public bool isOnline;
		}
		
		private class MyLogger : ILogHandler
		{
			ILogHandler oldLogger = null;
			
			public MyLogger() {}
			
			public MyLogger(ILogHandler oldLogger) 
			{
				this.oldLogger = oldLogger;
			}
			
			public void LogException(Exception exception, UnityEngine.Object context)
			{
				if (oldLogger != null)
					oldLogger.LogException(exception, context);
			}

			public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
			{
				if (oldLogger != null)
				{
					var msg = string.Format(format, args);					
					var needSkip = false;
					
					foreach (var word in configData.ExcludeWordsRCON)
					{
						if (msg.Contains(word))
						{
							needSkip = true;
							break;
						}
					}
					
					if (!needSkip)
						oldLogger.LogFormat(logType, context, format, args);					
				}
			}						
		}
		
		#endregion
		
		#region Hooks
		
		private void Loaded() // не переименовывать !
		{
			Online.Clear();
			LoadVariables();
			
			if (configData.ExcludeWordsRCON == null)
			{
				configData.ExcludeWordsRCON = new List<string>();
				SaveConfig(configData);
			}
			
			if (configData.ChatWords == null || configData.ChatWords.Count == 0)
			{
				configData.ChatWords = new List<string>() { "[ChatPlus] " };
				SaveConfig(configData);
			}						
			
			UnityEngine.Application.logMessageReceived += HandleLog;
			
			var filter = RustExtension.Filter.ToList();
			foreach (var word in configData.ExcludeWordsRCON)
				filter.Add(word);
				
            RustExtension.Filter = filter.ToArray();
								
			OldLogger = UnityEngine.Debug.unityLogger.logHandler;
			UnityEngine.Debug.unityLogger.logHandler = new MyLogger(OldLogger);
		}				
		
		private void OnServerInitialized() => timer.Once(2.1f, CheckOnline);
		
		private void OnServerSave() => Log("log", GetCurDate(), null);

		private void Unload() 
		{
			Log("log", GetCurDate(), null);
			UnityEngine.Application.logMessageReceived -= HandleLog;			
			UnityEngine.Debug.unityLogger.logHandler = OldLogger;						
		}
		
		private void OnServerShutdown()
		{
			foreach (var playerInfo in Online.Where(x=> x.isOnline))
				LogPlayerOut(playerInfo, "server shutdown");			
		}
		
		private void OnPlayerConnected(BasePlayer player) => PlayerConnected(player, false);		
		
		private void OnPlayerDisconnected(BasePlayer player, string reason) => PlayerDisconnected(player, reason, false);		
		
		private void OnLogProduced(string type, string msg)
		{
			if (string.IsNullOrEmpty(type) || string.IsNullOrEmpty(msg) || type == "connection" || !msg.Contains(" disconnecting: ")) return;
			
			string userIP, userID_, reason;
			ulong userID = 0;
			
			try
			{
				userIP = msg.Substring(0, msg.IndexOf("/"));
				userID_ = msg.Substring(msg.IndexOf("/")+1, msg.IndexOf("/", msg.IndexOf("/") + 1)-msg.IndexOf("/")-1);
				reason = msg.Substring(msg.IndexOf(" disconnecting: ")+16, msg.Length-msg.IndexOf(" disconnecting: ")-16);
				
				if (!userID_.IsSteamId())
				{
					PrintWarning($"Ошибка парсинга строки '{msg}'.\nНеверный Steam ID '{userID_}'.");
					return;
				}
				
				userID = (ulong)Convert.ToInt64(userID_);
			}
			catch 
			{
				PrintWarning($"Ошибка парсинга строки '{msg}'.");
				return;
			}
			
			if (userID == 0) return;
			
			var playerInfo = Online.FirstOrDefault(x=> x.userID == userID);
			
			if (playerInfo == null)
			{
				var player = BasePlayer.FindByID(userID);
				if (player == null) return;
				PlayerConnected(player, true);
			}
			
			if (!playerInfo.isOnline) return;
			
			playerInfo.clientDisconnectReason = reason;			
						
			if (playerInfo.disconnectTimer == null)
				playerInfo.disconnectTimer = timer.Once(1f, ()=> PrepareLogPlayerOut(playerInfo));
		}				
		
		#endregion				
		
		#region Main
		
		private PlayerInfo PlayerConnected(BasePlayer player, bool writeImmediate) 
		{
			if (player == null) return null;						
			
			var playerInfo = Online.FirstOrDefault(x=> x.userID == player.userID);
			
			if (playerInfo == null)
			{
				playerInfo = new PlayerInfo();
				Online.Add(playerInfo);
			}

			var address = player.net?.connection?.ipaddress;
			if (string.IsNullOrEmpty(address)) address = "0.0.0.0:0";
			
			playerInfo.userID = player.userID;
			playerInfo.userName = player.displayName;
			playerInfo.userIP = address;
			playerInfo.serverDisconnectReason = "";
			playerInfo.clientDisconnectReason = "";
			playerInfo.isOnline = true;
			
			if (playerInfo.disconnectTimer != null)
			{
				playerInfo.disconnectTimer.Destroy();
				playerInfo.disconnectTimer = null;
			}
			
			if (writeImmediate)
				LogPlayerIn(playerInfo);
			else
				timer.Once(1f, ()=> LogPlayerIn(playerInfo)); // ожидаем смены имени, если оно с запрещенными префиксами
			
			return playerInfo;
		}
		
		private void PlayerDisconnected(BasePlayer player, string reason, bool writeImmediate)
		{
			if (player == null) return;
			
			var playerInfo = Online.FirstOrDefault(x=> x.userID == player.userID);
			
			if (playerInfo == null)
				playerInfo = PlayerConnected(player, true);
			
			PlayerDisconnected(playerInfo, reason, writeImmediate);
		}
		
		private void PlayerDisconnected(PlayerInfo playerInfo, string reason, bool writeImmediate)
		{
			if (playerInfo == null || !playerInfo.isOnline) return;
			
			playerInfo.serverDisconnectReason = reason;			
			
			if (writeImmediate)
				PrepareLogPlayerOut(playerInfo);
			else
				if (playerInfo.disconnectTimer == null)
					playerInfo.disconnectTimer = timer.Once(1f, ()=> PrepareLogPlayerOut(playerInfo));
		}
		
		private void PrepareLogPlayerOut(PlayerInfo playerInfo)
		{
			if (playerInfo == null || !playerInfo.isOnline) return;
			
			var reason = "";
			
			if (!string.IsNullOrEmpty(playerInfo.clientDisconnectReason) && !string.IsNullOrEmpty(playerInfo.serverDisconnectReason) && playerInfo.clientDisconnectReason.Length >= playerInfo.serverDisconnectReason.Length)
				reason = playerInfo.clientDisconnectReason;
			else
				if (!string.IsNullOrEmpty(playerInfo.clientDisconnectReason) && !string.IsNullOrEmpty(playerInfo.serverDisconnectReason) && playerInfo.clientDisconnectReason.Length < playerInfo.serverDisconnectReason.Length)
					reason = playerInfo.serverDisconnectReason;
				else
					if (!string.IsNullOrEmpty(playerInfo.clientDisconnectReason) && string.IsNullOrEmpty(playerInfo.serverDisconnectReason))
						reason = playerInfo.clientDisconnectReason;
					else
						reason = playerInfo.serverDisconnectReason;
					
			if (string.IsNullOrEmpty(reason))
				reason = "N|A";
			
			LogPlayerOut(playerInfo, reason);
			
			if (playerInfo.disconnectTimer != null)
				playerInfo.disconnectTimer.Destroy();
			
			playerInfo.disconnectTimer = null;
			playerInfo.isOnline = false;
		}
		
		private void CheckOnline()
		{
			var activePlayers = BasePlayer.activePlayerList.Where(x=> x != null && x.userID.IsSteamId()).ToList();
			
			foreach (BasePlayer target in activePlayers)								
			{
				if (target == null) continue;
				
				var playerInfo = Online.FirstOrDefault(x=> x.userID == target.userID);
				
				if (playerInfo == null || !playerInfo.isOnline)
					PlayerConnected(target, true);
			}
			
			foreach (var playerInfo in Online.ToList())
			{
				if (playerInfo == null || !playerInfo.isOnline) continue;
				
				if (!activePlayers.Exists(x=> x != null && x.userID == playerInfo.userID))				
					PlayerDisconnected(playerInfo, "N|A", true);
			}
			
			timer.Once(60f, CheckOnline);
		}				
		
		#endregion
		
		#region Log In|Out
		
		private void LogPlayerIn(PlayerInfo playerInfo)
		{
			if (playerInfo == null) return;
			
			var msg = $"{playerInfo.userIP}/{playerInfo.userID}/{playerInfo.userName} connecting";			
			Log("connection", GetCurDate(), msg);
		}
		
		private void LogPlayerOut(PlayerInfo playerInfo, string reason)
		{
			if (playerInfo == null) return;
			
			var msg = $"{playerInfo.userIP}/{playerInfo.userID}/{playerInfo.userName} disconnecting: {reason}";
			Log("connection", GetCurDate(), msg);
		}
		
		#endregion
		
		#region Log
		
		private bool TryLog(string prefix, string date, string text)
		{
			try { LogToFile(prefix, date + text, this, false); }
			catch { return false; }
			
			return true;
		}
		
		private void Log(string prefix, string date, string text)
		{
			if (!string.IsNullOrEmpty(text))
			{
				var prefix_ = prefix;
				var logString_ = text;
				timer.Once(0.1f, ()=> Interface.Oxide.CallHook("OnLogProduced", prefix_, logString_));
			
				QueueBuffer.Add(new LogNfo(prefix, date, text));
			}
			
			while (QueueBuffer.Count > 0)
			{					
				if (TryLog(QueueBuffer[0].prefix, QueueBuffer[0].date, QueueBuffer[0].text))
					QueueBuffer.RemoveAt(0);
				else
					break;
			}				
		}					
		
		#endregion
		
		#region Handle Log
		
		private static bool IsDefaultExcludeWordExists(string log)
		{
			foreach (var word in DefaultExcludeWords)				
				if (log.StartsWith(word))
					return true;				                  
			
			return false;
		}
		
		private void HandleLog(string logString, string stackTrace, LogType type) 
		{
			if (IsDefaultExcludeWordExists(logString) || configData.ExcludeWords.Exists(x=> logString.Contains(x))) return;			
			
			var prefix = "exception";
			
			switch (type)
			{
				case LogType.Log:
					 prefix = "log";
					 break;
				case LogType.Warning:
					 prefix = "warning";
					 break;
				case LogType.Error:
					 prefix = "error";
					 break;				
			}												
			
			if (prefix == "log")
			{
				var flag = false;
				
				foreach (var chat in configData.ChatWords)
				{
					if (logString.StartsWith(chat))
					{
						flag = true;
						break;
					}
				}
				
				if (flag || logString.StartsWith("[CHAT] ") || logString.StartsWith("[TEAM CHAT] "))
				{
					prefix = "chat";
					
					if (flag)					
						foreach (var chat in configData.ChatWords)						
							if (logString.StartsWith(chat))
								logString = logString.Replace(chat, "[CHAT] ");
				}
			}	
			
			if (!string.IsNullOrEmpty(stackTrace))
				logString += "\n" + stackTrace;
			
			logString = logString.Replace("\r","").TrimEnd('\n', ' ');
			
			var newString = "";			
			var array = logString.Split('\n');
			
			if (array.Length > 1)
			{				
				foreach (var line in array)
				{
					if (string.IsNullOrEmpty(line.TrimEnd('\n', ' '))) continue;					
					newString += line + $"\n ";
				}
				
				newString = newString.TrimEnd('\n', ' ');
			}
			else
				newString = logString;
			
			Log(prefix, GetCurDate(), newString);
		}

		#endregion

		#region Common
		
		private static string GetCurDate() => "["+DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss")+"] ";		
		
		private string GetPlayerName(ulong userID)
		{
			var data = permission.GetUserData(userID.ToString());															
			return data.LastSeenNickname;
		}
		
		#endregion
		
		#region Config        
		
        private static ConfigData configData;
		
        private class ConfigData
        {			
			[JsonProperty(PropertyName = "Ключевые фразы, для исключения строк с ними из логов")]
			public List<string> ExcludeWords = new List<string>();
			[JsonProperty(PropertyName = "Ключевые фразы, для исключения строк с ними из консоли (из логов так же)")]
			public List<string> ExcludeWordsRCON = new List<string>();
			[JsonProperty(PropertyName = "Слова для фильтрации чата в чат группу")]
			public List<string> ChatWords = new List<string>();
        }
		
        private void LoadVariables() => configData = Config.ReadObject<ConfigData>();        
		
        protected override void LoadDefaultConfig()
        {
            configData = new ConfigData
            {
				ExcludeWords = new List<string>(),
				ExcludeWordsRCON = new List<string>(),
				ChatWords = new List<string>() { "[ChatPlus] " }
            };
            SaveConfig(configData);
			timer.Once(0.1f, ()=> SaveConfig(configData));
        }        
		
        private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);				
		
        #endregion	
		
    }
}