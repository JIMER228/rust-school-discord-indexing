using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Rust;

namespace Oxide.Plugins
{
	[Info("FixBalloons", "", "0.0.1")]
	public class FixBalloons : RustPlugin
	{
		
		private const int MultiplayDecayPerMinute = 10;
		
		private static Dictionary<uint, Vector3> LastPos = new Dictionary<uint, Vector3>();
		
		private void OnServerInitialized() => CheckBalloonsTimer();
		
		private void CheckBalloonsTimer()
		{
			var balloons = BaseNetworkable.serverEntities.OfType<HotAirBalloon>().Where(x=> x != null && !x.IsDestroyed).ToList();
			
			foreach (var balloon in balloons.Where(x=> !x.IsOn() && (x.HasFlag(BaseEntity.Flags.Reserved1) || x.HasFlag(BaseEntity.Flags.Reserved2))))
			{
				if (!LastPos.ContainsKey(balloon.net.ID))
				{
					LastPos.Add(balloon.net.ID, balloon.transform.position);
					continue;
				}
				
				if (Vector3.Distance(LastPos[balloon.net.ID], balloon.transform.position) <= 1f)
				{					
					PrintWarning("Ускоренно гниём багованый шар, координаты "+balloon.transform.position.ToString());
					Decay(balloon);
				}
				else
					LastPos[balloon.net.ID] = balloon.transform.position;
			}
			
			LastPos = LastPos.Where(x=> balloons.Exists(y=> y != null && !y.IsDestroyed && y.net.ID == x.Key)).ToDictionary(x=> x.Key, x=> x.Value);
			timer.Once(60f, CheckBalloonsTimer);
		}
		
		private static void Decay(HotAirBalloon balloon)
		{
			float single = MultiplayDecayPerMinute * (1f / HotAirBalloon.outsidedecayminutes);
			balloon.Hurt(balloon.MaxHealth() * single, DamageType.Decay, balloon, false);			
		}

	}
	
}