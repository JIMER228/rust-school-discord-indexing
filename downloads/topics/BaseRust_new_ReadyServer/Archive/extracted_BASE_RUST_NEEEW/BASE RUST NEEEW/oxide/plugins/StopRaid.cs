using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Rust;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;
using ru = Oxide.Game.Rust;

namespace Oxide.Plugins
{
	[Info("Stop Raid", "molik", "1.0.0")]
	public class StopRaid : RustPlugin
	{
		[PluginReference] Plugin Clans;
		private readonly Dictionary<ulong, TeleportTimer> TeleportTimers = new Dictionary<ulong, TeleportTimer>();
		class Info
		{
			public List<string> Disabled = new List<string> { };
		}

		private Info data = new Info();

		private void OnServerInitialized()
		{
			if (Interface.Oxide.DataFileSystem.ExistsDatafile("StopRaid")) data = Interface.Oxide.DataFileSystem.ReadObject<Info>("StopRaid");
		}

		void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
		{
			if (hitInfo == null) return;
			var player = hitInfo.Initiator as BasePlayer;
			if (player == null) return;
			var clan = Clans?.Call<string>("GetClanOf", player.userID);
			if (data.Disabled.Contains(clan))
			{
				hitInfo.damageTypes.ScaleAll(0);
			}
		}

		[ChatCommand("stopraid")]
		void CmdStop(BasePlayer player, string command, string[] args)
        {
			if (!player.IsAdmin)
			{
				PrintToChat(player, "Нет прав!");
				return;
			}
			if (args == null || args.Length <= 0)
			{
				PrintToChat(player, "Используйте: /stopraid ник чела из клана");
				return;
			}
			string victim = FindPlayer(args[0]);
			string clan = Clans?.Call<string>("GetClanOf", victim.userID);
			StopRaid(player, victim, clan);
		}
		void StopRaid(string vict, string clan)
		{
			BasePlayer victim = FindPlayer(vict);
			if (data.Disabled.Contains(clan))
			{
				data.Disabled.Remove(clan);
				var clanmates = Clans.Call("GetClanMembers", victim.userID) as List<string>;
				foreach (var players in clanmates)
				{
					Vector3 position = players.GetNetworkPosition();
					TeleportTimers[players] = new TeleportTimer
					{
						Timer = timer.Every(1f, () =>
						{
							players.MovePosition(position);
						})
					};
				}
			}
			else
			{
				data.Disabled.Add(clan);
				var clanmates = Clans.Call("GetClanMembers", victim.userID) as List<string>;
				foreach (var players in clanmates)
				{
					TeleportTimers.Remove(players);
				}
			}
			Interface.Oxide.DataFileSystem.WriteObject("StopRaid", data);

		}
		public BasePlayer FindPlayer(string info)
		{
			foreach (var player in BasePlayer.activePlayerList)
			{
				if (player.displayName.ToLower().Contains(info.ToLower()) || player.UserIDString == info) return player;
			}
			return default(BasePlayer);
		}
	}
}