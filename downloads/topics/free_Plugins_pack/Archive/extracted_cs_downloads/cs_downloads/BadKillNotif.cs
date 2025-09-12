using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("BadKillNotif", "Termin", "1.2.1")]
    public class BadKillNotif : RustPlugin
    {
        private void OnPlayerDeath(BasePlayer victim, HitInfo hitInfo)
        {
            if (victim == null || hitInfo == null)
                return;

            BasePlayer attacker = hitInfo.InitiatorPlayer;
            if (attacker == null || attacker == victim)
                return;

            string weaponName = GetWeaponName(hitInfo);
            float distance = Vector3.Distance(attacker.transform.position, victim.transform.position);
            int roundedDistance = Mathf.RoundToInt(distance);

            string message = $"<color=#FFE400>[PersianToxic]</color> <color=#A0A0A0><color=#FFA500>{victim.displayName}</color> Killed By <color=#FFA500>{attacker.displayName}</color> Using <color=#FFA500>{weaponName}</color> From A <color=#FFA500>{roundedDistance}M</color> Distance</color>";

            if (victim.currentTeam != 0)
            {
                foreach (BasePlayer teamMember in BasePlayer.activePlayerList)
                {
                    if (teamMember.currentTeam == victim.currentTeam)
                    {
                        SendReply(teamMember, message);
                    }
                }
            }
            else
            {
                SendReply(victim, message);
            }

            if (attacker.currentTeam != 0)
            {
                foreach (BasePlayer teamMember in BasePlayer.activePlayerList)
                {
                    if (teamMember.currentTeam == attacker.currentTeam)
                    {
                        SendReply(teamMember, message);
                    }
                }
            }
            else
            {
                SendReply(attacker, message);
            }
        }

        private string GetWeaponName(HitInfo hitInfo)
        {
            if (hitInfo?.WeaponPrefab == null)
                return "Unknown";

            string weaponName = hitInfo.WeaponPrefab.ShortPrefabName;
            weaponName = weaponName.Replace(".entity", "")
                                  .Replace(".prefab", "")
                                  .Replace("_", " ")
                                  .Trim();

            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(weaponName);
        }
    }
}