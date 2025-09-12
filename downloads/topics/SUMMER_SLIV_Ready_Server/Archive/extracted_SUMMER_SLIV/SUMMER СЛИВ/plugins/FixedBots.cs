//Reference: I18N
//Reference: I18N.Other

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Facepunch;
using Network;
using Newtonsoft.Json;
using Oxide.Core.Libraries;
using WebSocketSharp;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("FixedBots", "-", "1.0.9")]
	class FixedBots : RustPlugin
	{
		Dictionary<string, int> ListConnectedIP = new Dictionary<string, int>();
		Dictionary<string, ulong> LastSteamidFromIp = new Dictionary<string, ulong>();
		HashSet<string> BannedIP = new HashSet<string>();

		void OnServerInitialized()
		{
			timer.Repeat(20f, 0, () =>
			{
				ListConnectedIP.Clear();
			});
		}
		
		string CanClientLogin(Network.Connection connection)
		{
			string ip = connection.ipaddress.Split(':')[0];
			int count = 0;
			if (ListConnectedIP.TryGetValue(ip, out count))
			{
				if (LastSteamidFromIp.ContainsKey(ip) == false || LastSteamidFromIp[ip] != connection.userid) {
					LastSteamidFromIp[ip] = connection.userid;
					ListConnectedIP[ip] = count + 1;
					if (count >= 2)
					{
						BannedIP.Add(ip);
					}
				}
			}
			else
			{
				ListConnectedIP[ip] = 1;
			}

			if (BannedIP.Contains(ip))
			{
				return "You banned from this server!";
			}
			return null;
		}
	}
}