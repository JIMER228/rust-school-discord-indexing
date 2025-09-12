using System.Linq;
using System.Collections.Generic;
using Oxide.Core;
using UnityEngine;
using Oxide.Game.Rust.Cui;
using System;
using Newtonsoft.Json.Linq;

namespace Oxide.Plugins
{
    [Info("CheckNames", "Drop Dead", "1.0")]
    class CheckNames : RustPlugin
    {
        public Dictionary<ulong, string> Players = new Dictionary<ulong, string>();

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            /*if (entity == null || info == null) return;
            BasePlayer player = null;
            if (info.InitiatorPlayer != null) player = info.InitiatorPlayer;
            if (player == null) return;
            webrequest.EnqueueGet($"https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/?key=12150A57912B93A00734AA6C9E165F19&steamids={player.userID}&personalname&format=json", (code, response) =>
            {
                JObject keyValues = JObject.Parse(response);
                foreach (var item in keyValues["response"]["players"])
                {
                        if (player.displayName != (string)item["personaname"])
                        {
                            VKMessage($"Steam аккаунт подозревается в подмене nickname: {(string)item["personaname"]} (Исходный ник: {player.displayName}) - {player.userID}");
                            ConsoleSystem.Run(ConsoleSystem.Option.Server, "ban " + player.userID + " SpoofNickname");
                            return;
                        }
                    }
                    

                }, this);*/
            if (entity == null || info == null) return;
            if (info.InitiatorPlayer == null || info.Initiator == null) return;

            if (Players.ContainsKey(info.InitiatorPlayer.userID) && Players[info.InitiatorPlayer.userID] != info.InitiatorPlayer.displayName) 
            { 
                if (Players.ContainsValue(info.InitiatorPlayer.displayName))
                {
                    VKMessage($"Steam аккаунт подозревается в подмене nickname: {info.InitiatorPlayer.displayName} (Исходный ник: {Players[info.InitiatorPlayer.userID]}) - {info.InitiatorPlayer.userID}");
                    ConsoleSystem.Run(ConsoleSystem.Option.Server, "ban " + info.InitiatorPlayer.userID + " SpoofNickname");
                }
            } 

            var player = entity.ToPlayer();
            if (player != null)
            {
                if (Players.ContainsKey(player.userID) && Players[player.userID] != player.displayName) 
                { 
                    if (Players.ContainsValue(player.displayName))
                    {
                        VKMessage($"Steam аккаунт подозревается в подмене nickname: {player.displayName} (Исходный ник: {Players[player.userID]}) - {player.userID}");
                        ConsoleSystem.Run(ConsoleSystem.Option.Server, "ban " + player.userID + " SpoofNickname");
                    }
                } 
            }
        }
        private void VKMessage(string text)
        {
            int RandomID = UnityEngine.Random.Range(0, 9999);
            while (text.Contains("#"))
                text = text.Replace("#", "%23");
            string newUrl = "https://api.vk.com/method/messages.send?chat_id=" + "6" + $"&random_id={RandomID}" + $"&message={text}" + "&v=5.85&access_token=" + "c6b62a00a473b620aa7b9bb4bb7f52cd856722684ff793623525fdc19708fcfee4d993f9c4277fde6af24";
            webrequest.Enqueue(newUrl, null, (c, r) => { }, this);
        }
        [ChatCommand("testforme")]
        void testforme(BasePlayer player)
        {
            SendReply(player, player.displayName);
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"{Title}/Nicknames", Players);
        }

        private void LoadData()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile($"{Title}/Nicknames")) Players = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, string>>($"{Title}/Nicknames");
        }

        private void OnServerInitialized()
        {
            LoadData();
            foreach (var p in BasePlayer.activePlayerList) OnPlayerInit(p);
        }

        private void OnPlayerInit(BasePlayer player)
        {
            if (player.IsReceivingSnapshot)
            {
                NextTick(() => OnPlayerInit(player));
                return;
            }
            if (!Players.ContainsKey(player.userID)) Players.Add(player.userID, player.displayName);
            if (Players.ContainsKey(player.userID) && Players[player.userID] != player.displayName) { Players[player.userID] = player.displayName; } 
        }
    }
}