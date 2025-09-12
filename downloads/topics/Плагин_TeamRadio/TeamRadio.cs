using System;
using System.Collections.Generic;
using UnityEngine;
using Network;

namespace Oxide.Plugins
{
    [Info("TeamRadio", "Ks1vik", "1.0.0")]
    [Description("Provides voice communication between team members via radio, regardless of distance")]
    public class TeamRadio : RustPlugin
    {
        private readonly HashSet<ulong> activeRadios = new HashSet<ulong>();
        private readonly Dictionary<ulong, ulong> playerTeamCache = new Dictionary<ulong, ulong>();
        private const string RADIO_TOGGLE_KEY = "N";
        
        private void Init()
        {
            Puts("TeamRadio plugin loaded successfully!");
            Puts($"Press {RADIO_TOGGLE_KEY} to toggle your radio on/off");
        }
        
        private void OnServerInitialized()
        {
            playerTeamCache.Clear();
            activeRadios.Clear();
        }
        
        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (player == null || input == null) return;
            
            if (input.WasJustPressed(BUTTON.RELOAD))
            {
                ToggleRadio(player);
            }
        }
        
        private void ToggleRadio(BasePlayer player)
        {
            if (player == null) return;
            
            ulong playerID = player.userID;
            bool wasActive = activeRadios.Contains(playerID);
            
            if (wasActive)
            {
                activeRadios.Remove(playerID);
                player.ChatMessage("Radio turned OFF");
            }
            else
            {
                if (player.currentTeam == 0UL)
                {
                    player.ChatMessage("You need to be in a team to use radio");
                    return;
                }
                
                activeRadios.Add(playerID);
                player.ChatMessage("Radio turned ON");
            }
        }
        
        private object OnPlayerVoice(BasePlayer player, byte[] data)
        {
            if (player == null || data == null) return null;
            
            if (!activeRadios.Contains(player.userID))
                return null;
            
            if (player.currentTeam == 0UL)
                return null;
            
            var team = RelationshipManager.ServerInstance?.FindTeam(player.currentTeam);
            if (team == null || team.members.Count == 0)
                return null;
            
            SendVoiceToTeamMembers(player, data, team);
            
            return false;
        }
        
        private void SendVoiceToTeamMembers(BasePlayer sender, byte[] voiceData, RelationshipManager.PlayerTeam team)
        {
            if (sender == null || voiceData == null || team == null) return;
            
            var recipients = new List<Network.Connection>();
            
            foreach (ulong memberID in team.members)
            {
                if (memberID == sender.userID) continue;
                
                var member = BasePlayer.FindByID(memberID);
                if (member == null || !member.IsConnected) continue;
                
                if (!activeRadios.Contains(memberID)) continue;
                
                if (member.net?.connection != null)
                {
                    recipients.Add(member.net.connection);
                }
            }
            
            if (recipients.Count > 0)
            {
                SendVoiceData(sender, voiceData, recipients);
            }
        }
        
        private void SendVoiceData(BasePlayer sender, byte[] voiceData, List<Network.Connection> recipients)
        {
            if (sender == null || voiceData == null || recipients == null || recipients.Count == 0) return;
            
            try
            {
                NetWrite netWrite = Net.sv.StartWrite();
                netWrite.PacketID(Message.Type.VoiceData);
                netWrite.EntityID(sender.net.ID);
                netWrite.BytesWithSize(voiceData, false);
                
                foreach (var connection in recipients)
                {
                    if (connection != null && connection.connected)
                    {
                        netWrite.Send(new SendInfo(connection) { priority = Priority.Immediate });
                    }
                }
            }
            catch (Exception ex)
            {
                Puts($"Error sending voice data: {ex.Message}");
            }
        }
        
        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player == null) return;
            
            activeRadios.Remove(player.userID);
            playerTeamCache.Remove(player.userID);
        }
        
        private void OnTeamUpdate(ulong oldTeam, ulong newTeam, BasePlayer player)
        {
            if (player == null) return;
            
            if (newTeam == 0UL)
            {
                playerTeamCache.Remove(player.userID);
                if (activeRadios.Contains(player.userID))
                {
                    activeRadios.Remove(player.userID);
                    player.ChatMessage("Radio turned OFF - you left the team");
                }
            }
            else
            {
                playerTeamCache[player.userID] = newTeam;
            }
        }
        
        private void Unload()
        {
            activeRadios.Clear();
            playerTeamCache.Clear();
            Puts("TeamRadio plugin unloaded");
        }
        
        public bool HasActiveRadio(ulong playerID)
        {
            return activeRadios.Contains(playerID);
        }
        
        public void SetRadioState(ulong playerID, bool enabled)
        {
            if (enabled)
            {
                activeRadios.Add(playerID);
            }
            else
            {
                activeRadios.Remove(playerID);
            }
        }
    }
} 