using Oxide.Core.Plugins;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Globalization;
using Newtonsoft.Json;
using System.Linq;
using Oxide.Core;


namespace Oxide.Plugins
{
    [Info("VkReports", "Vokidu", "1.3.9")]
    public class VkReports : RustPlugin
    {						
		private static ConfigData cfg;
		private static List<ulong> BlockedPlayers = new List<ulong>();
		private static Hash<ulong, int> PlayersCooldowns = new Hash<ulong, int>();
		private static Hash<ulong, int> PlayersLimits = new Hash<ulong, int>();	    
		
		class ConfigData
        {
            public string access_token;
            public int chat_id;
            public string command;
            public string[] permission;
            public int day_limit;
            public int cooldown;
            public int cd_collect;
			public string server;
        }
		
		private void Init() => cfg = Config.ReadObject<ConfigData>();									   	    
		
		protected override void LoadDefaultConfig()
        {
			var config = new ConfigData
            {
                access_token = "access_token аккаунта вк",
                chat_id = 1,
                command = "report",
                permission = new string[] {"chat.moder2prefix", "chat.moder3prefix", "chatplus.moderator", "chatplus.bossmoder"},
                day_limit = 10,
                cooldown = 60,
                cd_collect = 180,
				server = "Test Server"
                
            };
            Config.WriteObject(config, true);
        }						
		
		private object CanSendReport(BasePlayer player)
		{
			if(cfg.day_limit > 0 && PlayersLimits[player.userID] >= cfg.day_limit) {
				return "Вы достигли лимита отправки жалоб в день. Следующие жалобы можно будет отправить только после перезагрузки сервера.";
			}
			if(cfg.cd_collect == 0 ) {
				if(PlayersCooldowns[player.userID] > CurTime())
					return "Вы недавно отправляли жалобу. Подождите немного.";
				PlayersCooldowns[player.userID] = CurTime()+cfg.cooldown;
				PlayersLimits[player.userID] += 1;
				return null;
			} else {
				if(BlockedPlayers.Contains(player.userID)) {
					if(PlayersCooldowns[player.userID] > CurTime())
						return "Вы отправляли жалобы слишком часто. Отправка временно недоступна.";
					else
						BlockedPlayers.Remove(player.userID);
				}
				if(PlayersCooldowns[player.userID] < CurTime()) {
					PlayersCooldowns[player.userID] = CurTime()+cfg.cooldown;
					PlayersLimits[player.userID] += 1;
					return null;
				} else if(PlayersCooldowns[player.userID] > (CurTime()+cfg.cd_collect)) {
					BlockedPlayers.Add(player.userID);
					return "Вы отправляли жалобы слишком часто. Отправка временно заблокированна.";
				}
			}
			PlayersCooldowns[player.userID] += cfg.cooldown;
			PlayersLimits[player.userID] += 1;
			return null;
		}	    	   
		
		[ChatCommand("report")]
		private void SendReport(BasePlayer player, string command, string[] args)
		{
			var canSendReport = CanSendReport(player);
			if (canSendReport is string) {
				SendReply(player, canSendReport.ToString());
				return;
			}
			
			if(args.Length < 1){
				SendReply(player, "Пример использования:\n/report <имя или steamid> <текст жалобы>");
				return;
			}
			var report = "Жалоба от игрока "+FixName(player.displayName)+":\n";
			
			string cheater = args[0];
			
			List<BasePlayer> targetList = FindPlayer(cheater);
			if (targetList.Count == 0)
			{
				SendReply(player, "Игрок не найден");
				return;
			}

			if (targetList.Count > 1)
			{
				SendReply(player, "Было найдено несколько игроков!");
				string returnString = "";
				targetList.ForEach(p => returnString += $"\n{p}");
				SendReply(player, returnString);
				return;
			}
			
			BasePlayer target = targetList[0];			
			
			string complaint = "";
			for(int ii=1;ii<args.Length;ii++) complaint += args[ii] + " ";
			
			complaint = complaint.Trim(' ');
			if (string.IsNullOrEmpty(complaint))
				complaint = "не указана";
			
			report = $"Сервер: {cfg.server}\nЖалоба от игрока "+player.displayName+" на игрока "+target.displayName+":\nhttps://steamcommunity.com/profiles/"+target.userID+$"\nТекст жалобы: {complaint}\n";
							
			VkLog(FixName(report));						
			
			SendReportToOnlineModers(report);
			SendReply(player, "Ваша жалоба отправлена.");
		}
		
		private void SendReportToOnlineModers(string text)
		{
			foreach(var ply in BasePlayer.activePlayerList){
				var flag = false;
				foreach(var priv in cfg.permission){
					if(permission.UserHasPermission(ply.userID.ToString(), priv))
						flag = true;
				}
				if(flag){
					if (!ply.IsAdmin)
						SendReply(ply, text);
					ply.ConsoleMessage("<color=#FFFFFF>[ЖАЛОБА] "+text+"</color>");
				}
			}
		}
		
		private static int CurTime() => (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
		
		private static string FixName(string name) => name.Replace("&","_").Replace("#","_").Replace("?","_");	

	    private List<BasePlayer> FindPlayer(string nameOrId)
	    {
		    List<BasePlayer> playerList = new List<BasePlayer>();
		    foreach (var check in BasePlayer.activePlayerList)
		    {
			    if (check.UserIDString == nameOrId)
				    return new List<BasePlayer> {check};
			    if (check.displayName.Contains(nameOrId, CompareOptions.IgnoreCase))
				    playerList.Add(check);
		    }
		    foreach (var check in BasePlayer.sleepingPlayerList)
		    {
			    if (check.UserIDString == nameOrId)
				    return new List<BasePlayer> {check};
			    if (check.displayName.Contains(nameOrId, CompareOptions.IgnoreCase))
				    playerList.Add(check);
		    }

		    return playerList;
	    }
		
		private void VkLog(string text)
		{
			webrequest.EnqueuePost("https://api.vk.com/method/messages.send", "peer_id="+cfg.chat_id+"&message="+text+"&v=5.80&access_token="+cfg.access_token, (code, response) => {}/*(code, response, player)*/, this);
		}
		
		private string API_GetServerName() => cfg.server;
    }
	
}