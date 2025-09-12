using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using System.Globalization;

namespace Oxide.Plugins
{
    [Info("WipePaper", "https://discord.gg/9vyTXsJyKR", "1.0.0")]
    public class WipePaper : RustPlugin
    {        

        #region Variables
		
		[PluginReference]
        private Plugin WipeInfo;		
		
		private const int DaysToDisablePromo = 3;
		
        private static string PaperMessage = "Промокод {0}\n\nАктивируй в магазине";
		
        private static WipeHandler Handler;
		private static DateTime WipeDt = default(DateTime);
		
		
		private class WipeHandler
        {
            [JsonProperty("Дата вайпа")]
            public string DateWipe = "";
            [JsonProperty("Список получивших игроков")]
            public HashSet<ulong> TakeOnWipe = new HashSet<ulong>();
			[JsonProperty("Промокод")]
            public string Promo = "";
        }

        #endregion

        #region Hooks
		
		private void Init()
		{
			if (Interface.Oxide.DataFileSystem.ExistsDatafile(Name))            
                Handler = Interface.Oxide.DataFileSystem.ReadObject<WipeHandler>(Name);            
            else            
                Handler = new WipeHandler();
		}

        private void OnServerInitialized()
        {   
            PrintWarning("\n-----------------------------\n" +
            "     Author - https://topplugin.ru/\n" +
            "     VK - https://vk.com/rustnastroika\n" +
            "     Discord - https://discord.com/invite/5DPTsRmd3G\n" +
            "-----------------------------");    
			WipeDt = default(DateTime);
			
            if (string.IsNullOrEmpty(Handler.DateWipe) && IsGlobaWipeNow())
            {
                Handler.DateWipe = SaveRestore.SaveCreatedTime.ToLocalTime().ToString("dd/MM/yyyy H:mm");
                Handler.TakeOnWipe.Clear();
            }

			if (IsGlobaWipeNow() || !string.IsNullOrEmpty(Handler.Promo))
				WipeDt = SaveRestore.SaveCreatedTime.ToLocalTime();
			
            BasePlayer.activePlayerList.ToList().ForEach(OnPlayerSleepEnded);
            Interface.Oxide.DataFileSystem.WriteObject(Name, Handler);
        }

        private void OnNewSave() 
		{
			Handler.DateWipe = "";
			Handler.Promo = "";
		}

        private void Unload() => Interface.Oxide.DataFileSystem.WriteObject(Name, Handler);

        private void OnServerSave() 
		{
            if (Handler != null)
				Interface.Oxide.DataFileSystem.WriteObject(Name, Handler); 
		}

        private void OnPlayerSleepEnded(BasePlayer player)
        {			
			if (player == null || Handler.TakeOnWipe.Contains(player.userID) || !IsPromoActive()) return;
			
            if (player.IsReceivingSnapshot)
            {
                timer.Once(1f, () => OnPlayerSleepEnded(player));
                return;
            }
            
            GivePaper(player);
        }

        #endregion
		
		#region Commands
		
		[ConsoleCommand("wp.setpromo")]
        private void CommandSetPromo(ConsoleSystem.Arg arg)
        {
            if (arg == null || arg.Player() != null) return;
            
			if (arg.Args == null || arg.Args.Length == 0)
			{
				Puts("Использование: wp.setpromo <ПРОМОКОД>");
				return;
			}
			
			Handler.Promo = arg.Args[0];												
            Handler.DateWipe = SaveRestore.SaveCreatedTime.ToLocalTime().ToString("dd/MM/yyyy H:mm");
            Handler.TakeOnWipe.Clear();
            
			WipeDt = SaveRestore.SaveCreatedTime.ToLocalTime();			
			
            BasePlayer.activePlayerList.ToList().ForEach(OnPlayerSleepEnded);
			
			Interface.Oxide.DataFileSystem.WriteObject(Name, Handler);
			Puts($"Установлен промокод '{Handler.Promo}'. Он будет действовать только в этом вайпе");
        }
		
		#endregion

        #region Functions 

		private static bool IsPromoActive() => (DateTime.Now-WipeDt).TotalMinutes < DaysToDisablePromo*24*60;
		
		private bool IsGlobaWipeNow()
		{
			if (WipeInfo == null) return false;
			
			var lastWipeStr = WipeInfo.Call("API_GetLastWipe") as string;
			var lastGlobalWipeStr = WipeInfo.Call("API_GetLastGlobalWipe") as string;
			
			var lW = DateTime.ParseExact(lastWipeStr, "d MMMM", CultureInfo.CreateSpecificCulture("ru-RU"));
			var lGW = DateTime.ParseExact(lastGlobalWipeStr, "d MMMM", CultureInfo.CreateSpecificCulture("ru-RU"));						
			
			if (string.IsNullOrEmpty(lastWipeStr) || string.IsNullOrEmpty(lastGlobalWipeStr))
				return false;
			
			return lW <= lGW;
		}
		
        private static void GivePaper(BasePlayer player)
        {
            var item = ItemManager.CreateByName("note", 1, 1916797805);
            item.text = string.Format(PaperMessage, !string.IsNullOrEmpty(Handler.Promo) ? Handler.Promo : "GLOBAL");
            item.name = "Записка с промокодом";
            player.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
            Handler.TakeOnWipe.Add(player.userID);
            player.ChatMessage($"Вы получили записку с промокодом в инвентарь!\n" +
                $"Активируйте промокод на <color=orange>TraveleRust.ru</color>, чтобы получить бонусные деньги на счёт, либо скидку в магазине.");
        }				
      
        #endregion
    }
}