using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using Network;
using Network.Visibility;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("(Mears) Radar", "Crunchy", "1.0.1")]
    internal class MearsRadar : CovalencePlugin
    {
        private const bool USE_NETWORK_GROUPS = false;

        private const float MAX_DISTANCE = 250f;
        private const float TICK_RATE = 0.25f;
        private static bool HideAdmins = false;

        private const string PERMS_ALLOWED = "mearsradar.allowed";
        private const string PERMS_HIDDEN = "mearsradar.hidden";

        private const string ENABLED = "You have <color=green>enabled</color> admin radar.";
        private const string DISABLED = "You have <color=red>disabled</color> admin radar.";

        private static MearsRadar s_PluginInstance;
        private static readonly Vector3 s_DrawNameOffset = new Vector3(0f, 2f, 0f);

        private static readonly string[] s_Colors = new[]
        {
            "#aa9980",
            "#aa5544",
            "#ee1122",
            "#225500",
            "#116655",
            "#f4bbcc",
            "#f7fb4b",
            "#ee2288",
            "#ee4499",
            "#77aaaa",
            "#ab1219",
            "#5d7f32",
            "#844012",
            "#006661",
            "#71acaf",
        };

#region Commands

        [Command("radar")]
        private void RadarCommand(IPlayer iPlayer, string cmd, string[] args)
        {
            BasePlayer player = iPlayer.Object as BasePlayer;
            if (player == null) return;

            if (!permission.UserHasPermission(player.UserIDString, PERMS_ALLOWED))
            {
                player.ChatMessage("You do not have permission to use this command.");
                return;
            }

            Radar radar = player.GetComponent<Radar>();

            if (radar == null)
            {
                player.gameObject.AddComponent<Radar>();
                player.ChatMessage(ENABLED);
            }
            else
            {
                UnityEngine.Object.Destroy(radar);
                player.ChatMessage(DISABLED);
            }
        }


        private static Dictionary<ulong, bool> PlayerHideAdmins = new Dictionary<ulong, bool>();

[Command("ha")]
private void HideAdminCommand(IPlayer iPlayer, string cmd, string[] args)
{
    BasePlayer player = iPlayer.Object as BasePlayer;
    if (player == null) return;

    if (!permission.UserHasPermission(player.UserIDString, PERMS_ALLOWED))
    {
        player.ChatMessage("You do not have permission to use this command.");
        return;
    }

    ulong playerId = player.userID;

    // Toggle the hide admin setting for this player
    if (PlayerHideAdmins.ContainsKey(playerId))
    {
        PlayerHideAdmins[playerId] = !PlayerHideAdmins[playerId];
    }
    else
    {
        PlayerHideAdmins[playerId] = true;
    }

    string status = PlayerHideAdmins[playerId] ? "<color=green>enabled</color>" : "<color=red>disabled</color>";
    player.ChatMessage($"Hiding admins from radar is now {status}.");
}



#endregion

#region Hooks

        private void Init()
        {
            s_PluginInstance = this;

            permission.RegisterPermission(PERMS_ALLOWED, this);
            permission.RegisterPermission(PERMS_HIDDEN, this);
        }

        private void Unload()
        {
            s_PluginInstance = null;
            var radars = UnityEngine.Object.FindObjectsOfType<Radar>();

            foreach (var radar in radars)
                UnityEngine.Object.Destroy(radar);
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            Radar radar = player.GetComponent<Radar>();

            if (radar != null)
                UnityEngine.Object.Destroy(radar);
        }

#endregion

        private class Radar : FacepunchBehaviour
        {
            private bool m_HasHiddenPerms;
            private BasePlayer m_Owner;
            private List<BasePlayer> m_PlayerCloseBy;

            private void Awake()
            {
                m_Owner = GetComponent<BasePlayer>();
                m_PlayerCloseBy = Facepunch.Pool.GetList<BasePlayer>();

                if (IsPlayerNull()) return;

                m_HasHiddenPerms = s_PluginInstance.permission.UserHasPermission(m_Owner.UserIDString, PERMS_HIDDEN);
                InvokeRepeating(StartDrawingNames, 0, TICK_RATE);
            }

            private void OnDestroy()
            {
                Facepunch.Pool.FreeList(ref m_PlayerCloseBy);
            }

            private bool IsPlayerNull()
            {
                bool isNull = m_Owner == null;

                if (isNull)
                    Destroy(this);

                return isNull;
            }

            private void StartDrawingNames()
            {
                StartCoroutine(DrawNearbyPlayers());
            }

            private IEnumerator DrawNearbyPlayers()
{
    m_PlayerCloseBy.Clear();

    if (USE_NETWORK_GROUPS)
    {
        Group networkGroup = m_Owner.net.group;

        if (networkGroup != null)
        {
            foreach (Connection connection in networkGroup.subscribers)
            {
                BasePlayer player = connection.player as BasePlayer;

                if (player != null)
                    m_PlayerCloseBy.Add(player);
            }
        }
    }
    else
    {
        BaseNetworkable.GetCloseConnections(m_Owner.transform.position, MAX_DISTANCE, m_PlayerCloseBy);
    }

    for (int i = 0; i < m_PlayerCloseBy.Count; i++)
    {
        if (IsPlayerNull()) yield break;

        var target = m_PlayerCloseBy[i];
        if (target == null || target.userID == m_Owner.userID || !target.IsConnected) continue;

        uint targetAuthLevel = target.Connection.authLevel;

        // Check if this player has HideAdmins enabled
        bool hideAdmins = PlayerHideAdmins.TryGetValue(m_Owner.userID, out bool isHidden) && isHidden;

        if (hideAdmins && targetAuthLevel > 0) continue;
        if (!m_HasHiddenPerms && targetAuthLevel > 0 && s_PluginInstance.permission.UserHasPermission(target.UserIDString, PERMS_HIDDEN)) continue;

        int targetHealth = Mathf.RoundToInt(target.health);
        string healthColor = targetHealth == 0 ? "#000000" : targetHealth > 70 ? "#085407" : targetHealth > 35 ? "#D55200" : "#720E0B";

        float distance = Vector3.Distance(m_Owner.transform.position, target.transform.position);
        string distanceText = $"{distance:F1}m";

        string icon;
        var team = target.Team;
        string nameColor = team != null ? s_Colors[team.teamID % (ulong)s_Colors.Length] : null;

        if (targetAuthLevel > 0)
        {
            bool hasActiveRadar = target.GetComponent<Radar>() != null;
            string color = hasActiveRadar ? "#109906" : "#ff0000";
            icon = $"<color={color}>★</color>";
        }
        else
        {
            icon = nameColor == null ? string.Empty : $"<color={nameColor}><size=30>‣</size></color>";
        }

        Vector3 pos = target.transform.position + s_DrawNameOffset;
        string text = $"<size=20>{icon} {target.displayName}</size> <size=16><color={healthColor}>{targetHealth}HP</color> | <color=#ffffff>{distanceText}</color></size>";

        m_Owner.SendConsoleCommand("ddraw.text", TICK_RATE, Color.white, pos, text);

        if (i % 5 != 0) continue;

        yield return null;
    }



            }
        }
    }
}
