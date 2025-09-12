using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Facepunch;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("MiniCopterCount", "Nimant", "1.0.1")]
    class MiniCopterCount : RustPlugin
    {																																							
		
		private static int GetTotalCount() => BaseNetworkable.serverEntities.OfType<MiniCopter>().Count();	
		
		#region Commands
		
		[ConsoleCommand("transport")]
        private void cmdTransport(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null) return;

            var mini = BaseNetworkable.serverEntities.OfType<MiniCopter>().Count();
			var cow = BaseNetworkable.serverEntities.OfType<ScrapTransportHelicopter>().Count();
			var car = BaseNetworkable.serverEntities.OfType<ModularCar>().Count();
			var horse = BaseNetworkable.serverEntities.OfType<RidableHorse>().Count();
			var rhib = BaseNetworkable.serverEntities.OfType<RHIB>().Count();
			var row = BaseNetworkable.serverEntities.OfType<MotorRowboat>().Count();
			
            Puts($"Транспорт на сервере: \n * {mini} миникоптеров\n * {cow} больших коптеров\n * {car} автомобилей\n * {horse} лошадей\n * {rhib} катеров\n * {row-rhib} лодок");
        }
		
		[ConsoleCommand("minicopters.count")]
        private void ConsoleCount(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player != null && !player.IsAdmin) return;
			
			if (player != null)
				PrintToConsole(player, $"Количество миникоптеров на карте: {GetTotalCount()} шт.");
			else				
				Puts($"Количество миникоптеров на карте: {GetTotalCount()} шт.");
		}
		
		[ChatCommand("minicopters.tp")]
        private void TpCommand(BasePlayer player, string command, string[] args)
        {
			if (!player.IsAdmin) return;			
			var list = BaseNetworkable.serverEntities.OfType<MiniCopter>().ToList();						
			
			if (list.Count() > 0)
			{
				player.Teleport(list.GetRandom().transform.position);
				SendReply(player, "Вы телепортировались к рандомному миникоптеру.");
			}
			else
				SendReply(player, "Миникоптеры не найдены!");
        }
		
		#endregion
		
	}
	
}	