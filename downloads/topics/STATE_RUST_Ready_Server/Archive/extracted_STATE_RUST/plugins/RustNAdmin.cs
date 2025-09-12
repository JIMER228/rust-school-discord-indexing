using System;
using System.Linq;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using Network;

namespace Oxide.Plugins
{
    [Info("RustNAdmin", "Nimant", "1.0.6")]

    class RustNAdmin : RustPlugin
    {								
		
		#region Variables
		
		[PluginReference]
        private Plugin Logger;
		
		private const int HugePing = 100;
		
		private const int MinOnlineToEqualZero = 3;
		
		private const int WarningStateMin = 6;		
		private const int ErrorStateMin = 12;		
		
		private const int WarningDelayTime = 15;
		private const int LongWarningDelayTime = 45;
		private const int ErrorDelayTime = 120;
		
		private const int SkipCount = 4;
		private const string Prefix = "#RNA#";		
		private const string Message = "f{fps} o{online} e{entities} n{networkout} s{state} p{ping}";
		
		private static int SkipCounter = 0;
		private static int LastOnline = -1;
		
		private static float EndTimeWarning = 0;
		private static float EndTimeLongWarning = 0;
		private static float EndTimeError = 0;
		
		#endregion
		
		#region Init
		
        private void Init()
        {
			SkipCounter = 0;
			LastOnline = -1;
			
            var filter = Game.Rust.RustExtension.Filter.ToList();
            filter.Add(Prefix);
			filter.Add("Error sending rcon reply:");
			filter.Add("[RCON] Auth:");
            Game.Rust.RustExtension.Filter = filter.ToArray();                        
					
			TrySetExcludeWord();			
			SendSystemState();
        }
		
		private void Unload()
		{
			var buf = "";
			foreach (var item in LogBuffer)
				buf += item + "\n";
			
			buf = buf.TrimEnd('\n');				
			Log(buf);
			LogBuffer.Clear();
		}
		
		#endregion
		
		#region Main
		
		private void SendSystemState()
		{						
			string fps = "";
			string online = "";
			string entities = "";
			string networkout = "";
			string state = "";
			string ping = "";
			
			var onlineCnt = BasePlayer.activePlayerList.Count();
			
			if (Message.Contains("{fps}"))
				fps = Performance.report.frameRate.ToString();
			
			if (Message.Contains("{online}"))
				online = onlineCnt.ToString();
			
			if (Message.Contains("{entities}"))
				entities = BaseNetworkable.serverEntities.Count().ToString();
			
			if (Message.Contains("{networkout}"))
				networkout = GetNetworkOut().ToString();
			
			if (Message.Contains("{state}"))
				state = GetState(onlineCnt).ToString();
			
			if (Message.Contains("{ping}"))
				ping = GetHugePing().ToString();
			
			var text = Message.Replace("{fps}", fps).Replace("{online}", online).Replace("{entities}", entities).Replace("{networkout}", networkout).Replace("{state}", state).Replace("{ping}", ping);
			Debug.Log(Prefix+" "+text);
			
			if (SkipCounter >= SkipCount)
			{
				LogBuf(GetCurDate()+text);
				SkipCounter = 0;
			}	
			else
				SkipCounter++;
			
			timer.Once(1f, ()=> SendSystemState());
		}

		private void TrySetExcludeWord(int attempt = 50)
		{
			if (Logger != null)
				Logger.CallHook("SetExcludeWord", Prefix);
			else
			{					
				if (attempt <= 0) return;				
				int attempt_ = attempt - 1;
				timer.Once(0.1f, ()=> TrySetExcludeWord(attempt_));
			}	
		}
		
		private int GetNetworkOut() => Net.sv != null ? (int)Net.sv.GetStat(null, BaseNetwork.StatTypeLong.BytesSent_LastSecond) : 0;
		
		private static string GetCurDate() => "["+DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss")+"] ";
		
		private static bool IsZeroOnline(int online) => online <= MinOnlineToEqualZero;		
		
		private static int GetState(int online)
		{
			int result = 0;
			var realTime = Time.realtimeSinceStartup;
			
			if (EndTimeError >= realTime)
			{
				LastOnline = online;
				return 3;
			}
			
			if (EndTimeLongWarning >= realTime)
			{
				LastOnline = online;
				return 2;
			}
			
			if (EndTimeWarning >= realTime)
			{
				LastOnline = online;
				return 1;
			}
			
			if (LastOnline >= 0)
			{
				var diff = LastOnline - online;
				if (diff >= WarningStateMin)
				{
					if (diff >= ErrorStateMin)
					{
						if (!IsZeroOnline(online))
						{
							result = 2;
							EndTimeLongWarning = realTime + LongWarningDelayTime;
						}
						else
						{
							result = 3;
							EndTimeError = realTime + ErrorDelayTime;
						}
					}
					else
					{
						result = 1;
						EndTimeWarning = realTime + WarningDelayTime;
					}
				}
			}
			
			LastOnline = online;						
			return result;
		}
		
		private static int GetHugePing()
		{
			int result = 0;
			foreach (var player in BasePlayer.activePlayerList)			
				if (player.IPlayer.Ping > HugePing)
					result++;
			
			return result;
		}
		
		#endregion
		
		#region Log
		
		private static List<string> QueueBuffer = new List<string>();
		private static List<string> LogBuffer = new List<string>();
		
		private void LogBuf(string message)
		{
			LogBuffer.Add(message);
			
			if (LogBuffer.Count > 50)
			{
				var buf = "";
				foreach (var item in LogBuffer)
					buf += item + "\n";
				
				buf = buf.TrimEnd('\n');				
				Log(buf);
				LogBuffer.Clear();
			}
		}
		
		private bool TryLog(string message)
		{
			if (string.IsNullOrEmpty(message)) return true;
			
			try { LogToFile("status", $"{message}", this, false); }
			catch { return false; }
			
			return true;
		}
		
		private void Log(string message)
		{						
			QueueBuffer.Add(message);	
			while(QueueBuffer.Count > 0)
			{					
				if (TryLog(QueueBuffer[0]))
					QueueBuffer.RemoveAt(0);
				else
					break;
			}
		}
		
		#endregion
		
    }
}
