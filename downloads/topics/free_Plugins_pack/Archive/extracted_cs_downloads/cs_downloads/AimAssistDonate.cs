using Facepunch.Extend;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using ProtoBuf;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("AimAssistDonate", "FourTeen & Koks", "2.0.0")]
    class AimAssistDonate : RustPlugin
    {
        [PluginReference] private Plugin ImageLibrary;

        private Dictionary<ulong, PlayerSettings> playerSettings = new Dictionary<ulong, PlayerSettings>();

        private class PlayerSettings
        {
            public bool On { get; set; } = false;
            public bool BetaAim { get; set; } = false;
            public bool TeamFF { get; set; } = false;
            public bool BotsFF { get; set; } = false;
            public int Distance { get; set; } = 300;
        }

        private PlayerSettings GetPlayerSettings(BasePlayer player)
        {
            ulong userID = player.userID;
            if (!playerSettings.ContainsKey(userID))
            {
                playerSettings[userID] = new PlayerSettings();
            }
            return playerSettings[userID];
        }
        private void SavePlayerSettings(BasePlayer player, PlayerSettings settings)
        {
            ulong userID = player.userID;
            playerSettings[userID] = settings;
        }

        private const string Main = "MainAIMUI";
        private const string ON = "https://gspics.org/images/2024/04/09/0Y1XOZ.png";
        private const string OFF = "https://gspics.org/images/2024/04/09/0Y1WXs.png";
        private const string MAIN = "https://i.postimg.cc/g2dZFY65/hsehf9sfh.png";

        private void OnServerInitialized()
        {
            LoadData();
            ImageLibrary.Call("AddImage", ON, ON);
            ImageLibrary.Call("AddImage", OFF, OFF);
            ImageLibrary.Call("AddImage", MAIN, MAIN);
            permission.RegisterPermission("aimassist.use", this);
            cmd.AddChatCommand("aim", this, "CmdAimAssist");
        }
        void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, Main);
            }
        }
        private void CmdAimAssist(BasePlayer player, string command, string[] args)
        {
            if (permission.UserHasPermission(player.UserIDString, "aimassist.use"))
            {
                GuiAim(player);
            }
            else
            {
                SendReply(player, "Вы не имеете доступа к использованию аима!");
            }
        }
        private void OnWeaponFired(BaseProjectile projectile, BasePlayer attacker, ItemModProjectile mod, ProjectileShoot projectileShoot)
        {
            if (attacker != null && permission.UserHasPermission(attacker.UserIDString, "aimassist.use"))
            {
                PlayerSettings settings = GetPlayerSettings(attacker);
                if (settings.On == false) return;
                Vector3 attackerPosition = attacker.transform.position;
                Collider[] hitColliders = Physics.OverlapSphere(attackerPosition, settings.Distance, LayerMask.GetMask("Player (Server)"));
                BasePlayer closestPlayer = null;
                float closestDistance = float.MaxValue;
                RelationshipManager.PlayerTeam team = RelationshipManager.ServerInstance.FindTeam(attacker.currentTeam);

                foreach (Collider collider in hitColliders)
                {
                    BasePlayer targetPlayer = collider.GetComponent<BasePlayer>();
                    bool teameit = true;
                    bool botff = true;
                    if (targetPlayer != null)
                    {
                        if (targetPlayer.name == "assets/rust.ai/agents/npcplayer/humannpc/tunneldweller/npc_tunneldweller.prefab") continue;
                        if (targetPlayer.name == "assets/rust.ai/agents/npcplayer/humannpc/banditguard/npc_bandit_guard.prefab") continue;
                        if (targetPlayer.name == "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_peacekeeper.prefab") continue;
                        if (targetPlayer.name == "assets/prefabs/npc/bandit/missionproviders/missionprovider_outpost_a.prefab") continue;
                        if (targetPlayer.name == "assets/prefabs/npc/bandit/missionproviders/missionprovider_outpost_b.prefab") continue;
                        if (targetPlayer.name == "assets/prefabs/npc/bandit/missionproviders/missionprovider_bandit_b.prefab") continue;
                        if (targetPlayer.name == "assets/prefabs/npc/bandit/missionproviders/missionprovider_fishing_a.prefab") continue;
                        if (targetPlayer.name == "assets/prefabs/npc/bandit/missionproviders/missionprovider_fishing_b.prefab") continue;
                        if (targetPlayer.name == "assets/prefabs/npc/bandit/shopkeepers/bandit_shopkeeper.prefab") continue;
                        if (targetPlayer.name == "assets/prefabs/npc/bandit/shopkeepers/boat_shopkeeper.prefab") continue;
                        if (targetPlayer.IsBot)
                        {
                            if (settings.BotsFF) botff = true;
                            else botff = false;
                        }
                    }
if (team != null && team.members.Contains(targetPlayer.userID))
{
    if (settings.TeamFF)
        teameit = true;
    else
        teameit = false;
}

                    if (targetPlayer != null && targetPlayer != attacker && teameit && !targetPlayer.IsSleeping() && !targetPlayer.IsWounded() && botff)
                    {
                        float distance = Vector3.Distance(attackerPosition, targetPlayer.transform.position);
                        if (settings.BetaAim)
                        {
                            Quaternion q = GetForPlayer(attacker, targetPlayer);
                            float xq = q.eulerAngles.x;
                            float yq = q.eulerAngles.y;
                            float x;
                            float y;
                            float xp = attacker.eyes.rotation.eulerAngles.x;
                            float yp = attacker.eyes.rotation.eulerAngles.y;
                            if (xq + 30 >= 360) xq = xq - 360;
                            if (yq + 30 >= 360) yq = yq - 360;
                            if (xp + 30 >= 360) xp = xp - 360;
                            if (yp + 30 >= 360) yp = yp - 360;
                            x = Math.Abs(xq - xp);
                            y = Math.Abs(yq - yp);
                            if (x < 30 && y < 30)
                            {
                                if (distance < closestDistance && permission.UserHasPermission(attacker.UserIDString, "aimassist.use"))
                                {
                                    closestDistance = distance;
                                    closestPlayer = targetPlayer;
                                }
                            }
                        }
                        else
                        {
                            if (distance < closestDistance && permission.UserHasPermission(attacker.UserIDString, "aimassist.use"))
                            {
                                closestDistance = distance;
                                closestPlayer = targetPlayer;
                            }
                        }
                    }
                }
                if (closestPlayer != null)
                {
                    Vector3 posseat = attacker.transform.position;
                    posseat.y = posseat.y + 0.5f;
                    posseat.y = posseat.y - 0.5f;
                    BaseMountable seat = GameManager.server.CreateEntity("assets/prefabs/vehicle/seats/passengerchair.prefab", posseat, GetForPlayerAim(attacker, closestPlayer)) as BaseMountable;
                    seat.Spawn();
                    attacker.SetMounted(seat);
                    attacker.SendNetworkUpdate();
                    attacker.UpdateNetworkGroup();
                    attacker.SendEntityUpdate();
                    seat.AdminKill();
                    projectile.primaryMagazine.contents = projectile.primaryMagazine.capacity;
                    projectile.SendNetworkUpdateImmediate();

                }
            }
        }
        public Quaternion GetForPlayer(BasePlayer attacker, BasePlayer closestPlayer)
        {
            Vector3 target = closestPlayer.ServerPosition;
            Vector3 posseat = attacker.transform.position;
            posseat.y = posseat.y + 0.5f;
            Vector3 dir = (target - posseat).normalized;
            posseat.y = posseat.y - 0.5f;
            Quaternion q = Quaternion.Euler(Quaternion.LookRotation(dir).eulerAngles.x, Quaternion.LookRotation(dir).eulerAngles.y, Quaternion.LookRotation(dir).eulerAngles.z);
            return q;
        }
        public Quaternion GetForPlayerAim(BasePlayer attacker, BasePlayer closetplayer)
        {
            Vector3 target = closetplayer.GetBounds(closetplayer.IsDucked()).center;
            Vector3 posseat = attacker.transform.position;
            int distance = (int)Vector2.Distance(attacker.transform.position, closetplayer.transform.position);
            if (distance < 170)
            {
                if (!attacker.IsDucked()) posseat.y += 1f;
                if (attacker.IsDucked()) posseat.y += 0.5f;
            }
            else
            {
                if (!attacker.IsDucked()) posseat.y -= 0.2f;
                if (attacker.IsDucked()) posseat.y -= 1.2f;
            }
            Vector3 dir = (target - posseat).normalized;
            Quaternion q = Quaternion.Euler(Quaternion.LookRotation(dir).eulerAngles.x, Quaternion.LookRotation(dir).eulerAngles.y, Quaternion.LookRotation(dir).eulerAngles.z);
            return q;
        }
        #region GUI
        void GuiAim(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, Main);
            string OffsetMinON = "192.36 176";
            string OffsetMaxON = "245 196";

            string OffsetMinBA = "192.36 131";
            string OffsetMaxBA = "245 153";

            string OffsetMinTFF = "192.36 86";
            string OffsetMaxTFF = "245 107.5";

            string OffsetMinBFF = "192.36 39.7";
            string OffsetMaxBFF = "245 63";

            string OffsetMinDIST = "192.36 -5";
            string OffsetMaxDIST = "245 20";

            PlayerSettings settings = GetPlayerSettings(player);

            string OnText = settings.On ? "OFF" : "ON";
            string teamFFText = settings.TeamFF ? "OFF" : "ON";
            string betaaimtext = settings.BetaAim ? "OFF" : "ON";
            string botFFText = settings.BotsFF ? "OFF" : "ON";

            CuiElementContainer container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                KeyboardEnabled = true,
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-350 -268", OffsetMax = "355 265" },
                Image = { Png = (string)ImageLibrary.Call("GetImage", MAIN), Material = "assets/icons/greyout.mat", }
            }, "Hud", Main);
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "296 215", OffsetMax = "341 258" },
                Button = { Color = "1 1 1 0", Material = "assets/icons/greyout.mat", Close = Main }
            }, Main, ".close");
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = OffsetMinON, OffsetMax = OffsetMaxON },
                Button = { Color = "1 1 1 0", Material = "assets/icons/greyout.mat", Command = $"aimmenuguicommand aim {OnText.ToLower()}" },
                Text = { Color = "1 1 1 1", Text = OnText, FontSize = 12, Align = TextAnchor.MiddleCenter }
            }, Main, ".1");
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = OffsetMinBA, OffsetMax = OffsetMaxBA },
                Button = { Color = "1 1 1 0", Material = "assets/icons/greyout.mat", Command = $"aimmenuguicommand betaaim {betaaimtext.ToLower()}" },
                Text = { Color = "1 1 1 1", Text = betaaimtext, FontSize = 12, Align = TextAnchor.MiddleCenter }
            }, Main, ".2");
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = OffsetMinTFF, OffsetMax = OffsetMaxTFF },
                Button = { Color = "1 1 1 0", Material = "assets/icons/greyout.mat", Command = $"aimmenuguicommand teamff {teamFFText.ToLower()}" },
                Text = { Color = "1 1 1 1", Text = teamFFText, FontSize = 12, Align = TextAnchor.MiddleCenter }
            }, Main, ".3");
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = OffsetMinBFF, OffsetMax = OffsetMaxBFF },
                Button = { Color = "1 1 1 0", Material = "assets/icons/greyout.mat", Command = $"aimmenuguicommand botff {botFFText.ToLower()}" },
                Text = { Color = "1 1 1 1", Text = botFFText, FontSize = 12, Align = TextAnchor.MiddleCenter }
            }, Main, ".4");
            container.Add(new CuiElement
            {
                Parent = Main,
                Name = ".5",
                Components =
                {
                    new CuiInputFieldComponent { Text = settings.Distance.ToString() + " / 300", Command  = "aimmenuguicommand aimradius ", ReadOnly = false, FontSize = 12, Align =  TextAnchor.MiddleCenter },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = OffsetMinDIST, OffsetMax = OffsetMaxDIST },
                }
            });
            CuiHelper.AddUi(player, container);
        }
        #endregion
        #region GUICOMMAND
        [ConsoleCommand("aimmenuguicommand")]
        private void ccmdESPGUI(ConsoleSystem.Arg arg)
        {
            PlayerSettings settings = GetPlayerSettings(arg.Player());
            switch (arg.Args[0])
            {
                case "aim":
                    {
                        if (arg.Args[1] == "off") settings.On = false;
                        if (arg.Args[1] == "on") settings.On = true;
                        break;
                    }
                case "betaaim":
                    {
                        if (arg.Args[1] == "off") settings.BetaAim = false;
                        if (arg.Args[1] == "on") settings.BetaAim = true;
                        break;
                    }
                case "teamff":
                    {
                        if (arg.Args[1] == "off") settings.TeamFF = false;
                        if (arg.Args[1] == "on") settings.TeamFF = true;
                        break;
                    }
                case "botff":
                    {
                        if (arg.Args[1] == "off") settings.BotsFF = false;
                        if (arg.Args[1] == "on") settings.BotsFF = true;
                        break;
                    }
                case "aimradius":
                    {
                        if (arg.Args[1].ToInt() <= 300) settings.Distance = arg.Args[1].ToInt();
                        else settings.Distance = 300;
                        break;
                    }
            }
            SavePlayerSettings(arg.Player(), settings);
            CuiHelper.DestroyUi(arg.Player(), Main);
            GuiAim(arg.Player());
            SaveData();
        }
        #endregion
        void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Title + "/playerSettings", playerSettings);
        }
        void LoadData()
        {
            try
            {
                playerSettings = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerSettings>>(Title + $"/playerSettings");
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
            }
        }
    }
}