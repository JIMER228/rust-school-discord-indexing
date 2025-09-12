using System;
using System.Collections.Generic;
using System.Globalization;
using Network;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Achievement System", "supreme", "2.7.9")]
    [Description("An achievement system where players are rewarded")]
    public class AchievementSystem : RustPlugin
    {
        #region Class Fields

        [PluginReference]
        private Plugin ImageLibrary, ServerRewards, Economics, Friends, Clans;
        
        private static AchievementSystem _plugin;
        private const string UsePermission = "achievementsystem.use";
        private const string UsePermissionVIP = "achievementsystem.vip";
        private const string AchievementFX = "assets/prefabs/tools/pager/effects/vibrate.prefab";
        private const string RewardsFX = "assets/prefabs/misc/easter/painted eggs/effects/gold_open.prefab";

        #endregion

        #region Hooks

        private void Init()
        {
            _plugin = this;
            LoadData();
            permission.RegisterPermission(UsePermission, this);
            permission.RegisterPermission(UsePermissionVIP, this);
        }

        private void OnServerInitialized()
        {
            ImageLibrary.Call("AddImage", "https://i.postimg.cc/1X5JcdrP/Achievement-System.png", "AchievementSystemUI"); //Popup
            ImageLibrary.Call("AddImage", "https://i.postimg.cc/Ghytk6vh/noun-Lock-3041637.png", "Locked"); //Locked
            ImageLibrary.Call("AddImage", "https://i.postimg.cc/DwJ9gTJG/unlock-512.png", "Redeemed"); //Redeemed icon
            ImageLibrary.Call("AddImage", _config.AchievementsImg.imageHemps, "Cannabis"); //Hemp
            ImageLibrary.Call("AddImage", _config.AchievementsImg.imagePumpkins, "Pies"); //Pumpkin
            ImageLibrary.Call("AddImage", _config.AchievementsImg.imageCorns, "Popcorn"); //Corn
            ImageLibrary.Call("AddImage", _config.AchievementsImg.imageWoodPicked, "WoodLurker"); //Wood Pick up
            ImageLibrary.Call("AddImage", _config.AchievementsImg.imageWoodGathered, "AxeMan"); // Wood Gathered
            ImageLibrary.Call("AddImage", _config.AchievementsImg.imageHeadshots, "HeadHunter"); //Headshots
            ImageLibrary.Call("AddImage", _config.AchievementsImg.imageSleepersKills, "Nightmare"); //Sleepers Killed
            ImageLibrary.Call("AddImage", _config.AchievementsImg.imagePVPKills, "Killer"); //Kills
            ImageLibrary.Call("AddImage", _config.AchievementsImg.imageBulletsFired, "Maniac"); //Bullets Fired
            ImageLibrary.Call("AddImage", _config.AchievementsImg.imageStonesPicked, "StoneAge"); //Stone Pick up
            ImageLibrary.Call("AddImage", _config.AchievementsImg.imageMetalsPicked, "Metal"); //Metal Pick up
            ImageLibrary.Call("AddImage", _config.AchievementsImg.imageSulfursPicked, "Raider"); //Sulfur Pick up
            ImageLibrary.Call("AddImage", _config.AchievementsImg.imageBarrelsKills, "GoldenBarrel"); //Barrels Destroyed
            ImageLibrary.Call("AddImage", _config.AchievementsImg.imageRocketsLaunched, "RPG"); //Rockets Launched
            ImageLibrary.Call("AddImage", _config.AchievementsImg.imageExplosivesThrown, "Explosion"); //Explosives Thrown
            ImageLibrary.Call("AddImage", _config.AchievementsImg.imageStonesGathered, "Miner"); //Stone Gathered
            ImageLibrary.Call("AddImage", _config.AchievementsImg.imageMetalsGathered, "MetalGear"); //Metal Gathered
            ImageLibrary.Call("AddImage", _config.AchievementsImg.imageSulfursGathered, "EvilMiner"); //Sulfur Gathered
            ImageLibrary.Call("AddImage", _config.AchievementsImg.imageScientistsKills, "Outsmarted"); //Scientists Killed
            ImageLibrary.Call("AddImage", _config.AchievementsImg.imageHeaviesKills, "Juggernaut"); //Heavy Scientists Killed
            ImageLibrary.Call("AddImage", _config.AchievementsImg.imageBoarsKills, "Piggy"); //Boars Killed
            ImageLibrary.Call("AddImage", _config.AchievementsImg.imageBearsKills, "Pookie"); //Bears Killed
            ImageLibrary.Call("AddImage", _config.AchievementsImg.imageChickensKills, "ChickenDinner"); //Chickens Klled
            ImageLibrary.Call("AddImage", _config.AchievementsImg.imageStagsKills, "Bambi"); //Stags Killed
            ImageLibrary.Call("AddImage", _config.AchievementsImg.imageWolvesKills, "LoneWolf"); //Wolves Killed
            ImageLibrary.Call("AddImage", _config.AchievementsImg.imageBradleysKills, "TankHunter"); //Bradleys Destroyed
            ImageLibrary.Call("AddImage", _config.AchievementsImg.imageChoppersKills, "ChopperHunter"); //Choppers Destroyed
            ImageLibrary.Call("AddImage", _config.AchievementsImg.imageClothSkinned, "Skinner"); // Cloth Skinned

            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                PlayerData data = GetPlayerData(player.userID);
                data.Name = player.displayName;
            }
        }

        private void Unload()
        {
            SaveData();
            
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                DestroyAllUi(player);
            }
            
            _plugin = null;
        }

        private void OnNewSave(string filename)
        {
            _data.PlayerDatas.Clear();
        }
        
        private void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (player == null)
            {
                return;
            }
            
            CuiHelper.DestroyUi(player, UiPanelName);
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            PlayerData data = GetPlayerData(player.userID);
            data.Name = player.displayName;
            NextTick(() => SaveData());
        }

        private void AddPoints(BasePlayer player, int amount)
        {
            ServerRewards?.Call("AddPoints", player.userID, amount);
        }

        private void AddEconomics(BasePlayer player, double amount)
        {
            Economics?.Call("Deposit", player.userID, amount);
        }

        private void OnCollectiblePickup(CollectibleEntity collectible, BasePlayer player)
        {
            if (player == null || player.IsNpc)
            {
                return;
            }
            
            if (!HasPermission(player, UsePermission) && !HasPermission(player, UsePermissionVIP))
            {
                return;
            }

            PlayerData data = GetPlayerData(player.userID);
            data.Name = player.displayName;
            
            if (collectible.ShortPrefabName == "hemp-collectable")
            {
                data.HempPickedUp++;
                
                int minHemp = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minHempsVIP : _config.AchievementsSet.minHemps;
            
                if (_data.PlayerDatas[player.userID].HempPickedUp == minHemp)
                {
                    DisplayUI(player, "<color=#14ad00>CANNABIS</color>", "Cannabis");
                    SendEffectTo(AchievementFX, player);
                    if (_config.AchievementsImg.achievementBroadcast)
                    {
                        Server.Broadcast($"<color=#e3e3e3>[<color=#ACFA58>Achievement System</color>] <color=#54a3f2>{player.displayName}</color> unlocked an Achievement: <color=#14ad00>CANNABIS</color>");
                    }
                }
            }
            
            if (collectible.ShortPrefabName == "pumpkin-collectable")
            {
                data.PumpkinPickedUp++;
                
                int minPumpkin = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minPumpkinsVIP : _config.AchievementsSet.minPumpkins;
            
                if (_data.PlayerDatas[player.userID].PumpkinPickedUp == minPumpkin)
                {
                    DisplayUI(player, "<color=#ffa40a>PIES</color>", "Pies");
                    SendEffectTo(AchievementFX, player);
                    if (_config.AchievementsImg.achievementBroadcast)
                    {
                        Server.Broadcast($"<color=#e3e3e3>[<color=#ACFA58>Achievement System</color>] <color=#54a3f2>{player.displayName}</color> unlocked an Achievement: <color=#ffa40a>PIES</color>");
                    }
                }
            }
            
            if (collectible.ShortPrefabName == "corn-collectable")
            {
                data.CornPickedUp++;
                
                int minCorn = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minCornsVIP : _config.AchievementsSet.minCorns;
            
                if (_data.PlayerDatas[player.userID].CornPickedUp == minCorn)
                {
                    DisplayUI(player, "<color=#fff133>POPCORN</color>", "Popcorn");
                    SendEffectTo(AchievementFX, player);
                    if (_config.AchievementsImg.achievementBroadcast)
                    {
                        Server.Broadcast($"<color=#e3e3e3>[<color=#ACFA58>Achievement System</color>] <color=#54a3f2>{player.displayName}</color> unlocked an Achievement: <color=#fff133>POPCORN</color>");
                    }
                }
            }

            if (collectible.ShortPrefabName == "stone-collectable")
            {
                data.StonePickedUp++;
                
                int minStone = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minStonesVIP : _config.AchievementsSet.minStones;
            
                if (_data.PlayerDatas[player.userID].StonePickedUp == minStone)
                {
                    DisplayUI(player, "<color=#d6d6d6>STONE AGE</color>", "StoneAge");
                    SendEffectTo(AchievementFX, player);
                    if (_config.AchievementsImg.achievementBroadcast)
                    {
                        Server.Broadcast($"<color=#e3e3e3>[<color=#ACFA58>Achievement System</color>] <color=#54a3f2>{player.displayName}</color> unlocked an Achievement: <color=#d6d6d6>STONE AGE</color>");
                    }
                }
            }

            if (collectible.ShortPrefabName == "wood-collectable")
            {
                data.WoodPickedUp++;
                
                int minWood = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minWoodVIP : _config.AchievementsSet.minWood;
            
                if (_data.PlayerDatas[player.userID].WoodPickedUp == minWood)
                {
                    DisplayUI(player, "<color=#acfa58>WOOD LURKER</color>", "WoodLurker");
                    SendEffectTo(AchievementFX, player);
                    if (_config.AchievementsImg.achievementBroadcast)
                    {
                        Server.Broadcast($"<color=#e3e3e3>[<color=#ACFA58>Achievement System</color>] <color=#54a3f2>{player.displayName}</color> unlocked an Achievement: <color=#acfa58>WOOD LURKER</color>");
                    }
                }
            }
            
            if (collectible.ShortPrefabName == "metal-collectable")
            {
                data.MetalPickedUp++;
                
                int minMetal = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minMetalsVIP : _config.AchievementsSet.minMetals;
            
                if (_data.PlayerDatas[player.userID].MetalPickedUp == minMetal)
                {
                    DisplayUI(player, "<color=#bdbdbd>METAL MAN</color>", "Metal");
                    SendEffectTo(AchievementFX, player);
                    if (_config.AchievementsImg.achievementBroadcast)
                    {
                        Server.Broadcast($"<color=#e3e3e3>[<color=#ACFA58>Achievement System</color>] <color=#54a3f2>{player.displayName}</color> unlocked an Achievement: <color=#bdbdbd>METAL MAN</color>");
                    }
                }
            }
            
            if (collectible.ShortPrefabName == "sulfur-collectable")
            {
                data.SulfurPickedUp++;
                
                int minSulfur = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minSulfursVIP : _config.AchievementsSet.minSulfurs;
            
                if (_data.PlayerDatas[player.userID].SulfurPickedUp == minSulfur)
                {
                    DisplayUI(player, "<color=#eb0f00>RAIDER</color>", "Raider");
                    SendEffectTo(AchievementFX, player);
                    if (_config.AchievementsImg.achievementBroadcast)
                    {
                        Server.Broadcast($"<color=#e3e3e3>[<color=#ACFA58>Achievement System</color>] <color=#54a3f2>{player.displayName}</color> unlocked an Achievement: <color=#eb0f00>RAIDER</color>");
                    }
                }
            }
        }
        
        private void OnEntityTakeDamage(BasePlayer victim, HitInfo info)
        {
            if (victim == null || info == null || info.InitiatorPlayer == null || !victim.userID.IsSteamId() || !info.InitiatorPlayer.userID.IsSteamId() || victim == info.InitiatorPlayer)
            {
                return;
            }

            BasePlayer player = info.InitiatorPlayer;
            
            if (!HasPermission(player, UsePermission) && !HasPermission(player, UsePermissionVIP))
            {
                return;
            }
            
            if (IsFriend(player.userID, victim.userID) || IsClanMember(player.userID, victim.userID))
            {
                return;
            }
            
            if (info.isHeadshot)
            {
                PlayerData data = GetPlayerData(player.userID);
                data.Name = player.displayName;

                data.HeadShots++;
                
                int minHeadShots = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minHeadshotsVIP : _config.AchievementsSet.minHeadshots;
            
                if (_data.PlayerDatas[player.userID].HeadShots == minHeadShots)
                {
                    DisplayUI(player, "<color=#4da6ff>HEAD HUNTER</color>", "HeadHunter");
                    SendEffectTo(AchievementFX, player);
                    if (_config.AchievementsImg.achievementBroadcast)
                    {
                        Server.Broadcast($"<color=#e3e3e3>[<color=#ACFA58>Achievement System</color>] <color=#54a3f2>{player.displayName}</color> unlocked an Achievement: <color=#4da6ff>HEAD HUNTER</color>");
                    }
                }
            }
        }
        
        private void OnEntityDeath(BasePlayer victim, HitInfo info)
        {
            if (victim == null || info == null || info.InitiatorPlayer == null || !victim.userID.IsSteamId() || !info.InitiatorPlayer.userID.IsSteamId() || victim == info.InitiatorPlayer)
            {
                return;
            }

            BasePlayer attacker = info.InitiatorPlayer;
            if (!HasPermission(attacker, UsePermission) && !HasPermission(attacker, UsePermissionVIP))
            {
                return;
            }
            
            ProcessPVPKills(attacker, victim);
        }

        private void OnEntityDeath(BaseEntity entity, HitInfo info)
        {
            if (entity == null || info == null || info.InitiatorPlayer == null || entity == info.InitiatorPlayer || info.Initiator.IsNpc || !info.InitiatorPlayer.userID.IsSteamId())
            {
                return;
            }

            BasePlayer player = info.InitiatorPlayer;
            if (!HasPermission(player, UsePermission) && !HasPermission(player, UsePermissionVIP))
            {
                return;
            }

            PlayerData data = GetPlayerData(player.userID);
            data.Name = player.displayName;
            
            if (entity.name.Contains("barrel"))
            {
                data.BarrelsDestroyed++;
                
                int minBarrels = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minBarrelsDestroyedVIP : _config.AchievementsSet.minBarrelsDestroyed;
            
                if (_data.PlayerDatas[player.userID].BarrelsDestroyed == minBarrels)
                {
                    DisplayUI(player, "<color=#ffdb2e>GOLDEN BARREL</color>", "GoldenBarrel");
                    SendEffectTo(AchievementFX, player);
                    if (_config.AchievementsImg.achievementBroadcast)
                    {
                        Server.Broadcast($"<color=#e3e3e3>[<color=#ACFA58>Achievement System</color>] <color=#54a3f2>{player.displayName}</color> unlocked an Achievement: <color=#ffdb2e>GOLDEN BARREL</color>");
                    }
                }
            }

            if (entity.ShortPrefabName == "boar")
            {
                data.BoarsKilled++; 
                
                int minBoars = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minBoarsKillsVIP : _config.AchievementsSet.minBoarsKills;
            
                if (_data.PlayerDatas[player.userID].BoarsKilled == minBoars)
                {
                    DisplayUI(player, "<color=#ff9ee9>PIGGY</color>", "Piggy");
                    SendEffectTo(AchievementFX, player);
                    if (_config.AchievementsImg.achievementBroadcast)
                    {
                        Server.Broadcast($"<color=#e3e3e3>[<color=#ACFA58>Achievement System</color>] <color=#54a3f2>{player.displayName}</color> unlocked an Achievement: <color=#ff9ee9>PIGGY</color>");
                    }
                }
            }
            
            if (entity.ShortPrefabName == "bear")
            {
                data.BearsKilled++; 
                
                int minBears = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minBearsKillsVIP : _config.AchievementsSet.minBearsKills;
            
                if (_data.PlayerDatas[player.userID].BearsKilled == minBears)
                {
                    DisplayUI(player, "<color=#c8ff9e>POOKIE BEAR</color>", "Pookie");
                    SendEffectTo(AchievementFX, player);
                    if (_config.AchievementsImg.achievementBroadcast)
                    {
                        Server.Broadcast($"<color=#e3e3e3>[<color=#ACFA58>Achievement System</color>] <color=#54a3f2>{player.displayName}</color> unlocked an Achievement: <color=#c8ff9e>POOKIE BEAR</color>");
                    }
                }
            }
            
            if (entity.ShortPrefabName == "chicken")
            {
                data.ChickensKilled++;
                
                int minChickens = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minChickensKillsVIP : _config.AchievementsSet.minChickensKills;
            
                if (_data.PlayerDatas[player.userID].ChickensKilled == minChickens)
                {
                    DisplayUI(player, "<color=#ba9eff>CHICKEN DINNER</color>", "ChickenDinner");
                    SendEffectTo(AchievementFX, player);
                    if (_config.AchievementsImg.achievementBroadcast)
                    {
                        Server.Broadcast($"<color=#e3e3e3>[<color=#ACFA58>Achievement System</color>] <color=#54a3f2>{player.displayName}</color> unlocked an Achievement: <color=#ba9eff>CHICKEN DINNER</color>");
                    }
                }
            }
            
            if (entity.ShortPrefabName == "stag")
            {
                data.StagsKilled++;
                
                int minStags = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minStagsKillsVIP : _config.AchievementsSet.minStagsKills;
            
                if (_data.PlayerDatas[player.userID].StagsKilled == minStags)
                {
                    DisplayUI(player, "<color=#dc567c>BAMBI</color>", "Bambi");
                    SendEffectTo(AchievementFX, player);
                    if (_config.AchievementsImg.achievementBroadcast)
                    {
                        Server.Broadcast($"<color=#e3e3e3>[<color=#ACFA58>Achievement System</color>] <color=#54a3f2>{player.displayName}</color> unlocked an Achievement: <color=#dc567c>BAMBI</color>");
                    }
                }
            }
            
            if (entity.ShortPrefabName == "wolf")
            {
                data.WolvesKilled++;
                
                int minWolves = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minWolvesKillsVIP : _config.AchievementsSet.minWolvesKills;
            
                if (_data.PlayerDatas[player.userID].WolvesKilled == minWolves)
                {
                    DisplayUI(player, "<color=#9f284a>LONE WOLF</color>", "LoneWolf");
                    SendEffectTo(AchievementFX, player);
                    if (_config.AchievementsImg.achievementBroadcast)
                    {
                        Server.Broadcast($"<color=#e3e3e3>[<color=#ACFA58>Achievement System</color>] <color=#54a3f2>{player.displayName}</color> unlocked an Achievement: <color=#9f284a>LONE WOLF</color>");
                    }
                }
            }

            if (entity.ShortPrefabName == "bradleyapc")
            {
                data.BradleyDestroyed++;
                
                int minBradleys = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minBradleysKillsVIP : _config.AchievementsSet.minBradleysKills;
            
                if (_data.PlayerDatas[player.userID].BradleyDestroyed == minBradleys)
                {
                    DisplayUI(player, "<color=#28539f>TANK HUNTER</color>", "TankHunter");
                    SendEffectTo(AchievementFX, player);
                    if (_config.AchievementsImg.achievementBroadcast)
                    {
                        Server.Broadcast($"<color=#e3e3e3>[<color=#ACFA58>Achievement System</color>] <color=#54a3f2>{player.displayName}</color> unlocked an Achievement: <color=#28539f>TANK HUNTER</color>");
                    }
                }
            }
        }
        
        private void OnEntityDeath(PatrolHelicopterAI heli, HitInfo info)
        {
            BasePlayer player = (BasePlayer)heli.helicopterBase.lastAttacker;
            if (!player)
            {
                return;
            }
            
            PlayerData data = GetPlayerData(player.userID);
            data.HelicopterDestroyed++;
            
            int minHelicopters = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minChoppersKillsVIP : _config.AchievementsSet.minChoppersKills;
            if (_data.PlayerDatas[player.userID].HelicopterDestroyed == minHelicopters)
            {
                DisplayUI(player, "<color=#cb6910>HELI HUNTER</color>", "ChopperHunter");
                SendEffectTo(AchievementFX, player);
                if (_config.AchievementsImg.achievementBroadcast)
                {
                    Server.Broadcast($"<color=#e3e3e3>[<color=#ACFA58>Achievement System</color>] <color=#54a3f2>{player.displayName}</color> unlocked an Achievement: <color=#cb6910>HELI HUNTER</color>");
                }
            }
        }

        private void OnEntityDeath(HumanNPC npc, HitInfo info)
        {
            if (npc == null || info == null || info.InitiatorPlayer == null || npc == info.InitiatorPlayer || info.Initiator.IsNpc || !info.InitiatorPlayer.userID.IsSteamId())
            {
                return;
            }
            
            BasePlayer player = info.InitiatorPlayer;
            if (!HasPermission(player, UsePermission) && !HasPermission(player, UsePermissionVIP))
            {
                return;
            }
            
            PlayerData data = GetPlayerData(player.userID);
            data.Name = player.displayName;
            
            if (npc.name.Contains("scientist"))
            {
                data.ScientistsKilled++;
                
                int minScientists = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minScientistsKillsVIP : _config.AchievementsSet.minScientistsKills;
            
                if (_data.PlayerDatas[player.userID].ScientistsKilled == minScientists)
                {
                    DisplayUI(player, "<color=#73a6e8>OUTSMARTED</color>", "Outsmarted");
                    SendEffectTo(AchievementFX, player);
                    if (_config.AchievementsImg.achievementBroadcast)
                    {
                        Server.Broadcast($"<color=#e3e3e3>[<color=#ACFA58>Achievement System</color>] <color=#54a3f2>{player.displayName}</color> unlocked an Achievement: <color=#73a6e8>OUTSMARTED</color>");
                    }
                }
            }
        }

        private void OnEntityDeath(ScientistNPC scientist, HitInfo info)
        {
            if (scientist == null || info == null || info.InitiatorPlayer == null || scientist == info.InitiatorPlayer || info.Initiator.IsNpc || !info.InitiatorPlayer.userID.IsSteamId())
            {
                return;
            }
            
            BasePlayer player = info.InitiatorPlayer;
            if (!HasPermission(player, UsePermission) && !HasPermission(player, UsePermissionVIP))
            {
                return;
            }
            
            PlayerData data = GetPlayerData(player.userID);
            data.Name = player.displayName;
            
            if (scientist.name.Contains("heavy"))
            {
                data.HeaviesKilled++;
                
                int minHeavies = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minHeaviesKillsVIP : _config.AchievementsSet.minHeaviesKills;
            
                if (_data.PlayerDatas[player.userID].HeaviesKilled == minHeavies)
                {
                    DisplayUI(player, "<color=#e8d473>JUGGERNAUT</color>", "Juggernaut");
                    SendEffectTo(AchievementFX, player);
                    if (_config.AchievementsImg.achievementBroadcast)
                    {
                        Server.Broadcast($"<color=#e3e3e3>[<color=#ACFA58>Achievement System</color>] <color=#54a3f2>{player.displayName}</color> unlocked an Achievement: <color=#e8d473>JUGGERNAUT</color>");
                    }
                }
            }
            else if (scientist.name.Contains("scientist"))
            {
                data.ScientistsKilled++;
                
                int minScientists = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minScientistsKillsVIP : _config.AchievementsSet.minScientistsKills;
            
                if (_data.PlayerDatas[player.userID].ScientistsKilled == minScientists)
                {
                    DisplayUI(player, "<color=#73a6e8>OUTSMARTED</color>", "Outsmarted");
                    SendEffectTo(AchievementFX, player);
                    if (_config.AchievementsImg.achievementBroadcast)
                    {
                        Server.Broadcast($"<color=#e3e3e3>[<color=#ACFA58>Achievement System</color>] <color=#54a3f2>{player.displayName}</color> unlocked an Achievement: <color=#73a6e8>OUTSMARTED</color>");
                    }
                }
            }
        }

        private void OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (!HasPermission(player, UsePermission) && !HasPermission(player, UsePermissionVIP))
            {
                return;
            }
            
            if (dispenser == null || player == null || item == null)
            {
                return;
            }
            
            PlayerData data = GetPlayerData(player.userID);
            data.Name = player.displayName;

            if (item.info.shortname == "wood")
            {
                data.WoodGathered += item.amount;
                
                int minWood = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minWoodGatheredVIP : _config.AchievementsSet.minWoodGathered;
            
                if (_data.PlayerDatas[player.userID].WoodGathered >= minWood && !_data.PlayerDatas[player.userID].DisplayWood)
                {
                    DisplayUI(player, "<color=#d66f00>AXE MAN</color>", "AxeMan");
                    SendEffectTo(AchievementFX, player);
                    _data.PlayerDatas[player.userID].DisplayWood = true;
                    if (_config.AchievementsImg.achievementBroadcast)
                    {
                        Server.Broadcast($"<color=#e3e3e3>[<color=#ACFA58>Achievement System</color>] <color=#54a3f2>{player.displayName}</color> unlocked an Achievement: <color=#d66f00>AXE MAN</color>");
                    }
                }
            }
            
            if (item.info.shortname == "stones")
            {
                data.StoneGathered += item.amount;
                
                int minStones = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minStonesGatheredVIP : _config.AchievementsSet.minStonesGathered;
            
                if (_data.PlayerDatas[player.userID].StoneGathered >= minStones && !_data.PlayerDatas[player.userID].DisplayStone)
                {
                    DisplayUI(player, "<color=#c4b382>MINER</color>", "Miner");
                    SendEffectTo(AchievementFX, player);
                    _data.PlayerDatas[player.userID].DisplayStone = true;
                    if (_config.AchievementsImg.achievementBroadcast)
                    {
                        Server.Broadcast($"<color=#e3e3e3>[<color=#ACFA58>Achievement System</color>] <color=#54a3f2>{player.displayName}</color> unlocked an Achievement: <color=#c4b382>MINER</color>");
                    }
                }
            }
            
            if (item.info.shortname == "metal.ore")
            {
                data.MetalGathered += item.amount;
                
                int minMetal = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minMetalsGatheredVIP : _config.AchievementsSet.minMetalsGathered;
            
                if (_data.PlayerDatas[player.userID].MetalGathered >= minMetal && !_data.PlayerDatas[player.userID].DisplayMetal)
                {
                    DisplayUI(player, "<color=#adadad>METAL GEAR</color>", "MetalGear");
                    SendEffectTo(AchievementFX, player);
                    _data.PlayerDatas[player.userID].DisplayMetal = true;
                    if (_config.AchievementsImg.achievementBroadcast)
                    {
                        Server.Broadcast($"<color=#e3e3e3>[<color=#ACFA58>Achievement System</color>] <color=#54a3f2>{player.displayName}</color> unlocked an Achievement: <color=#adadad>METAL GEAR</color>");
                    }
                }
            }
            
            if (item.info.shortname == "sulfur.ore")
            {
                data.SulfurGathered += item.amount;
                
                int minSulfur = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minSulfursGatheredVIP : _config.AchievementsSet.minSulfursGathered;
            
                if (_data.PlayerDatas[player.userID].SulfurGathered >= minSulfur && !_data.PlayerDatas[player.userID].DisplaySulfur)
                {
                    DisplayUI(player, "<color=#eb3e00>EVIL MINER</color>", "EvilMiner");
                    SendEffectTo(AchievementFX, player);
                    _data.PlayerDatas[player.userID].DisplaySulfur = true;
                    if (_config.AchievementsImg.achievementBroadcast)
                    {
                        Server.Broadcast($"<color=#e3e3e3>[<color=#ACFA58>Achievement System</color>] <color=#54a3f2>{player.displayName}</color> unlocked an Achievement: <color=#eb3e00>EVIL MINER</color>");
                    }
                }
            }

            if (item.info.shortname == "cloth")
            {
                data.ClothSkinned += item.amount;
                
                int minCloth = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minClothSkinnedVIP : _config.AchievementsSet.minClothSkinned;
            
                if (_data.PlayerDatas[player.userID].ClothSkinned >= minCloth && !_data.PlayerDatas[player.userID].DisplayCloth)
                {
                    DisplayUI(player, "<color=#95283b>SKINNER</color>", "Skinner");
                    SendEffectTo(AchievementFX, player);
                    _data.PlayerDatas[player.userID].DisplayCloth = true;
                    if (_config.AchievementsImg.achievementBroadcast)
                    {
                        Server.Broadcast($"<color=#e3e3e3>[<color=#ACFA58>Achievement System</color>] <color=#54a3f2>{player.displayName}</color> unlocked an Achievement: <color=#95283b>SKINNER</color>");
                    }
                }
            }
        }

        private void OnWeaponFired(BaseProjectile projectile, BasePlayer player, ItemModProjectile mod)
        {
            if (!HasPermission(player, UsePermission) && !HasPermission(player, UsePermissionVIP))
            {
                return;
            }
            
            if (player != null)
            {
                if (mod != null)
                {
                    if (mod.ToString().Contains("ammo"))
                    {
                        PlayerData data = GetPlayerData(player.userID);
                        data.Name = player.displayName;
                        
                        data.BulletsFired++;
                        
                        int minBullets = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minBulletsVIP : _config.AchievementsSet.minBullets;
            
                        if (_data.PlayerDatas[player.userID].BulletsFired == minBullets)
                        {
                            DisplayUI(player, "<color=#ffea80>MANIAC</color>", "Maniac");
                            SendEffectTo(AchievementFX, player);
                            if (_config.AchievementsImg.achievementBroadcast)
                            {
                                Server.Broadcast($"<color=#e3e3e3>[<color=#ACFA58>Achievement System</color>] <color=#54a3f2>{player.displayName}</color> unlocked an Achievement: <color=#ffea80>MANIAC</color>");
                            }
                        }
                    }
                }
            }
        }

        private void OnExplosiveThrown(BasePlayer player, BaseEntity entity, ThrownWeapon item)
        {
            if (!HasPermission(player, UsePermission) && !HasPermission(player, UsePermissionVIP))
            {
                return;
            }
            
            if (player != null)
            {
                if (item != null)
                {
                    if (item.name.Contains("explosive.timed") || item.name.Contains("explosive.satchel") || item.name.Contains("grenade.beancan") || item.name.Contains("grenade.f1"))
                    {
                        PlayerData data = GetPlayerData(player.userID);
                        data.Name = player.displayName;

                        data.ExplosivesThrown++;
                        
                        int minExplosives = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minExplosivesVIP : _config.AchievementsSet.minExplosives;
            
                        if (_data.PlayerDatas[player.userID].ExplosivesThrown == minExplosives)
                        {
                            DisplayUI(player, "<color=#ff9c42>BOOM</color>", "Explosion");
                            SendEffectTo(AchievementFX, player);
                            if (_config.AchievementsImg.achievementBroadcast)
                            {
                                Server.Broadcast($"<color=#e3e3e3>[<color=#ACFA58>Achievement System</color>] <color=#54a3f2>{player.displayName}</color> unlocked an Achievement: <color=#ff9c42>BOOM</color>");
                            }
                        }
                    }
                }
            }
        }

        private void OnRocketLaunched(BasePlayer player, BaseEntity entity)
        {
            if (!HasPermission(player, UsePermission) && !HasPermission(player, UsePermissionVIP))
            {
                return;
            }
            
            if (player != null)
            {
                PlayerData data = GetPlayerData(player.userID);
                data.Name = player.displayName;
                
                data.RocketsLaunched++;
                
                int minRockets = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minRocketsVIP : _config.AchievementsSet.minRockets;
            
                if (_data.PlayerDatas[player.userID].RocketsLaunched == minRockets)
                {
                    DisplayUI(player, "<color=#ff712e>RPG</color>", "RPG");
                    SendEffectTo(AchievementFX, player);
                    if (_config.AchievementsImg.achievementBroadcast)
                    {
                        Server.Broadcast($"<color=#e3e3e3>[<color=#ACFA58>Achievement System</color>] <color=#54a3f2>{player.displayName}</color> unlocked an Achievement: <color=#ff712e>RPG</color>");
                    }
                }
            }
        }

        #endregion

        #region Core Methods

        private void ProcessPVPKills(BasePlayer player, BasePlayer victim)
        {
            PlayerData data = GetPlayerData(player.userID);
            data.Name = player.displayName;
            
            if (victim.IsSleeping())
            {
                data.SleepersKilled++;
                
                int minSleepers = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minSleepersKillsVIP : _config.AchievementsSet.minSleepersKills;
            
                if (_data.PlayerDatas[player.userID].SleepersKilled == minSleepers)
                {
                    DisplayUI(player, "<color=#785959>NIGHTMARE</color>", "Nightmare");
                    SendEffectTo(AchievementFX, player);
                    if (_config.AchievementsImg.achievementBroadcast)
                    {
                        Server.Broadcast($"<color=#e3e3e3>[<color=#ACFA58>Achievement System</color>] <color=#54a3f2>{player.displayName}</color> unlocked an Achievement: <color=#785959>NIGHTMARE</color>");
                    }
                }

                return;
            }

            data.PVPKills++;

            int minKills = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minPVPKillsVIP : _config.AchievementsSet.minPVPKills;
            
            if (_data.PlayerDatas[player.userID].PVPKills == minKills)
            {
                DisplayUI(player, "<color=#ff5633>KILLER</color>", "Killer");
                SendEffectTo(AchievementFX, player);
                if (_config.AchievementsImg.achievementBroadcast)
                {
                    Server.Broadcast($"<color=#e3e3e3>[<color=#ACFA58>Achievement System</color>] <color=#54a3f2>{player.displayName}</color> unlocked an Achievement: <color=#ff5633>KILLER</color>");
                }
            }
        }

        #endregion

        #region Commands

        [ChatCommand("achievements")]
        private void AchievementsCommand(BasePlayer player)
        {
            if (!HasPermission(player, UsePermission) && !HasPermission(player, UsePermissionVIP))
            {
                player.ChatMessage(Lang(LangKeys.NoPermission, player.UserIDString));
                return;
            }
            
            DisplayAchievements(player);
        }
        
        [ChatCommand("checkachievements")]
        private void AchievementsCheckCommand(BasePlayer player)
        {
            if (!HasPermission(player, UsePermission) && !HasPermission(player, UsePermissionVIP))
            {
                player.ChatMessage(Lang(LangKeys.NoPermission, player.UserIDString));
                return;
            }

            player.ChatMessage($"<color=#e3e3e3>[<color=#ACFA58>Achievement System</color>] Your achievement stats are:\n" +
                               $"PVP Kills: {_data.PlayerDatas[player.userID].PVPKills} / {(HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minPVPKillsVIP : _config.AchievementsSet.minPVPKills)}\n" +
                               $"Scientists Killed: {_data.PlayerDatas[player.userID].ScientistsKilled} / {(HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minScientistsKillsVIP : _config.AchievementsSet.minScientistsKills)}\n" +
                               $"Heavy Scientists Killed: {_data.PlayerDatas[player.userID].HeaviesKilled} / {(HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minHeaviesKillsVIP : _config.AchievementsSet.minHeaviesKills)}\n" +
                               $"Boars Killed: {_data.PlayerDatas[player.userID].BoarsKilled} / {(HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minBoarsKillsVIP : _config.AchievementsSet.minBoarsKills)}\n" +
                               $"Bears Killed: {_data.PlayerDatas[player.userID].BearsKilled} / {(HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minBearsKillsVIP : _config.AchievementsSet.minBearsKills)}\n" +
                               $"Chickens Killed: {_data.PlayerDatas[player.userID].ChickensKilled} / {(HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minChickensKillsVIP : _config.AchievementsSet.minChickensKills)}\n" +
                               $"Stags Killed: {_data.PlayerDatas[player.userID].StagsKilled} / {(HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minStagsKillsVIP : _config.AchievementsSet.minStagsKills)}\n" +
                               $"Wolves Killed: {_data.PlayerDatas[player.userID].WolvesKilled} / {(HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minWolvesKillsVIP : _config.AchievementsSet.minWolvesKills)}\n" +
                               $"Bradleys Destroyed: {_data.PlayerDatas[player.userID].BradleyDestroyed} / {(HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minBradleysKillsVIP : _config.AchievementsSet.minBradleysKills)}\n" +
                               $"Choppers Destroyed: {_data.PlayerDatas[player.userID].HelicopterDestroyed} / {(HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minChoppersKillsVIP : _config.AchievementsSet.minChoppersKills)}\n" +
                               $"Sleepers Killed: {_data.PlayerDatas[player.userID].SleepersKilled} / {(HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minSleepersKillsVIP : _config.AchievementsSet.minSleepersKills)}\n" +
                               $"Headshots: {_data.PlayerDatas[player.userID].HeadShots} / {(HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minHeadshotsVIP : _config.AchievementsSet.minHeadshots)}\n" +
                               $"Barrels Destroyed: {_data.PlayerDatas[player.userID].BarrelsDestroyed} / {(HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minBarrelsDestroyedVIP : _config.AchievementsSet.minBarrelsDestroyed)}\n" +
                               $"Explosives Thrown: {_data.PlayerDatas[player.userID].ExplosivesThrown} / {(HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minExplosivesVIP : _config.AchievementsSet.minExplosives)}\n" +
                               $"Bullets Fired: {_data.PlayerDatas[player.userID].BulletsFired} / {(HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minBulletsVIP : _config.AchievementsSet.minBullets)}\n" +
                               $"Rockets Launched: {_data.PlayerDatas[player.userID].RocketsLaunched} / {(HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minRocketsVIP : _config.AchievementsSet.minRockets)}\n" +
                               $"Hemps Picked up: {_data.PlayerDatas[player.userID].HempPickedUp} / {(HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minHempsVIP : _config.AchievementsSet.minHemps)}\n" +
                               $"Pumpkins Picked up: {_data.PlayerDatas[player.userID].PumpkinPickedUp} / {(HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minPumpkinsVIP : _config.AchievementsSet.minPumpkins)}\n" +
                               $"Corns Picked up: {_data.PlayerDatas[player.userID].CornPickedUp} / {(HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minCornsVIP : _config.AchievementsSet.minCorns)}\n" +
                               $"Wood Picked up: {_data.PlayerDatas[player.userID].WoodPickedUp} / {(HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minWoodVIP : _config.AchievementsSet.minWood)}\n" +
                               $"Stones Picked up: {_data.PlayerDatas[player.userID].StonePickedUp} / {(HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minStonesVIP : _config.AchievementsSet.minStones)}\n" +
                               $"Metals Picked up: {_data.PlayerDatas[player.userID].MetalPickedUp} / {(HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minMetalsVIP : _config.AchievementsSet.minMetals)}\n" +
                               $"Sulfurs Picked up: {_data.PlayerDatas[player.userID].SulfurPickedUp} / {(HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minSulfursVIP : _config.AchievementsSet.minSulfurs)}\n" +
                               $"Stones Gathered: {_data.PlayerDatas[player.userID].StoneGathered} / {(HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minStonesGatheredVIP : _config.AchievementsSet.minStonesGathered)}\n" +
                               $"Wood Gathered: {_data.PlayerDatas[player.userID].WoodGathered} / {(HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minWoodGatheredVIP : _config.AchievementsSet.minWoodGathered)}\n" +
                               $"Metals Gathered: {_data.PlayerDatas[player.userID].MetalGathered} / {(HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minMetalsGatheredVIP : _config.AchievementsSet.minMetalsGathered)}\n" +
                               $"Sulfurs Gathered: {_data.PlayerDatas[player.userID].SulfurGathered} / {(HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minSulfursGatheredVIP : _config.AchievementsSet.minSulfursGathered)}\n" +
                               $"Cloth Skinned: {_data.PlayerDatas[player.userID].ClothSkinned} / {(HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minClothSkinnedVIP : _config.AchievementsSet.minClothSkinned)}</color>");
        }
        
        #endregion

        #region Configuration
        
        private Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Achievements for Regular Players")]
            public AchievementsSettings AchievementsSet { get; set; }
            
            [JsonProperty(PropertyName = "Achievements for VIP Players")]
            public AchievementsSettingsVIP AchievementsSetVIP { get; set; }
            
            [JsonProperty(PropertyName = "Reward Settings for Regular Players")]
            public RewardSettings RewardSet { get; set; }
            
            [JsonProperty(PropertyName = "Reward Settings for VIP Players")]
            public RewardSettingsVIP RewardSetVIP { get; set; }
            
            [JsonProperty(PropertyName = "Achievements Image URL")]
            public AchievementsImage AchievementsImg { get; set; }
        }

        private class AchievementsSettings
        {
            [JsonProperty("PVP Kills Amount for Achievement")]
            public int minPVPKills { get; set; }
            
            [JsonProperty("Scientists Kills Amount for Achievement")]
            public int minScientistsKills { get; set; }
            
            [JsonProperty("Heavy Scientists Kills Amount for Achievement")]
            public int minHeaviesKills { get; set; }
            
            [JsonProperty("Boars Kills Amount for Achievement")]
            public int minBoarsKills { get; set; }
            
            [JsonProperty("Bears Kills Amount for Achievement")]
            public int minBearsKills { get; set; }
            
            [JsonProperty("Chickens Kills Amount for Achievement")]
            public int minChickensKills { get; set; }
            
            [JsonProperty("Stags Kills Amount for Achievement")]
            public int minStagsKills { get; set; }
            
            [JsonProperty("Wolves Kills Amount for Achievement")]
            public int minWolvesKills { get; set; }
            
            [JsonProperty("Bradleys Destroyed Amount for Achievement")]
            public int minBradleysKills { get; set; }
            
            [JsonProperty("Choppers Destroyed Amount for Achievement")]
            public int minChoppersKills { get; set; }
            
            [JsonProperty("Sleepers Kills Amount for Achievement")]
            public int minSleepersKills { get; set; }
            
            [JsonProperty("Headshots Amount for Achievement")]
            public int minHeadshots { get; set; }
            
            [JsonProperty("Barrels Destroyed Amount for Achievement")]
            public int minBarrelsDestroyed { get; set; }
            
            [JsonProperty("Explosives Thrown Amount for Achievement")]
            public int minExplosives { get; set; }
            
            [JsonProperty("Bullets Fired Amount for Achievement")]
            public int minBullets { get; set; }
            
            [JsonProperty("Rockets Launched Amount for Achievement")]
            public int minRockets { get; set; }
            
            [JsonProperty("Hemps Picked Up Amount for Achievement")]
            public int minHemps { get; set; }
            
            [JsonProperty("Pumpkins Picked Up Amount for Achievement")]
            public int minPumpkins { get; set; }
            
            [JsonProperty("Corns Picked Up Amount for Achievement")]
            public int minCorns { get; set; }
            
            [JsonProperty("Stones Picked Up Amount for Achievement")]
            public int minStones { get; set; }
            
            [JsonProperty("Wood Picked Up Amount for Achievement")]
            public int minWood { get; set; }
            
            [JsonProperty("Metals Picked Up Amount for Achievement")]
            public int minMetals { get; set; }
            
            [JsonProperty("Sulfurs Picked Up Amount for Achievement")]
            public int minSulfurs { get; set; }
            
            [JsonProperty("Stones Gathered Amount for Achievement")]
            public int minStonesGathered { get; set; }
            
            [JsonProperty("Wood Gathered Amount for Achievement")]
            public int minWoodGathered { get; set; }
            
            [JsonProperty("Metals Gathered Amount for Achievement")]
            public int minMetalsGathered { get; set; }
            
            [JsonProperty("Sulfurs Gathered Amount for Achievement")]
            public int minSulfursGathered { get; set; }
            
            [JsonProperty("Cloth Skinned Amount for Achievement")]
            public int minClothSkinned { get; set; }
        }

        private class AchievementsSettingsVIP
        {
            [JsonProperty("PVP Kills Amount for Achievement")]
            public int minPVPKillsVIP { get; set; }
            
            [JsonProperty("Scientists Kills Amount for Achievement")]
            public int minScientistsKillsVIP { get; set; }
            
            [JsonProperty("Heavy Scientists Kills Amount for Achievement")]
            public int minHeaviesKillsVIP { get; set; }
            
            [JsonProperty("Boars Kills Amount for Achievement")]
            public int minBoarsKillsVIP { get; set; }
            
            [JsonProperty("Bears Kills Amount for Achievement")]
            public int minBearsKillsVIP { get; set; }
            
            [JsonProperty("Chickens Kills Amount for Achievement")]
            public int minChickensKillsVIP { get; set; }
            
            [JsonProperty("Stags Kills Amount for Achievement")]
            public int minStagsKillsVIP { get; set; }
            
            [JsonProperty("Wolves Kills Amount for Achievement")]
            public int minWolvesKillsVIP { get; set; }
            
            [JsonProperty("Bradleys Destroyed Amount for Achievement")]
            public int minBradleysKillsVIP { get; set; }
            
            [JsonProperty("Choppers Destroyed Amount for Achievement")]
            public int minChoppersKillsVIP { get; set; }
            
            [JsonProperty("Sleepers Kills Amount for Achievement")]
            public int minSleepersKillsVIP { get; set; }
            
            [JsonProperty("Headshots Amount for Achievement")]
            public int minHeadshotsVIP { get; set; }
            
            [JsonProperty("Barrels Destroyed Amount for Achievement")]
            public int minBarrelsDestroyedVIP { get; set; }
            
            [JsonProperty("Explosives Thrown Amount for Achievement")]
            public int minExplosivesVIP { get; set; }
            
            [JsonProperty("Bullets Fired Amount for Achievement")]
            public int minBulletsVIP { get; set; }
            
            [JsonProperty("Rockets Launched Amount for Achievement")]
            public int minRocketsVIP { get; set; }
            
            [JsonProperty("Hemps Picked Up Amount for Achievement")]
            public int minHempsVIP { get; set; }
            
            [JsonProperty("Pumpkins Picked Up Amount for Achievement")]
            public int minPumpkinsVIP { get; set; }
            
            [JsonProperty("Corns Picked Up Amount for Achievement")]
            public int minCornsVIP { get; set; }
            
            [JsonProperty("Stones Picked Up Amount for Achievement")]
            public int minStonesVIP { get; set; }
            
            [JsonProperty("Wood Picked Up Amount for Achievement")]
            public int minWoodVIP { get; set; }
            
            [JsonProperty("Metals Picked Up Amount for Achievement")]
            public int minMetalsVIP { get; set; }
            
            [JsonProperty("Sulfurs Picked Up Amount for Achievement")]
            public int minSulfursVIP { get; set; }
            
            [JsonProperty("Stones Gathered Amount for Achievement")]
            public int minStonesGatheredVIP { get; set; }
            
            [JsonProperty("Wood Gathered Amount for Achievement")]
            public int minWoodGatheredVIP { get; set; }
            
            [JsonProperty("Metals Gathered Amount for Achievement")]
            public int minMetalsGatheredVIP { get; set; }
            
            [JsonProperty("Sulfurs Gathered Amount for Achievement")]
            public int minSulfursGatheredVIP { get; set; }
            
            [JsonProperty("Cloth Skinned Amount for Achievement")]
            public int minClothSkinnedVIP { get; set; }
        }

        private class RewardSettings
        {
            [JsonProperty(PropertyName = "Rewards for PVP Kills Achievement Economics")]
            public int rewardsPVPKillsEconomics { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Scientists Kills Achievement Economics")]
            public int rewardsScientistsKillsEconomics { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Heavy Scientists Kills Achievement Economics")]
            public int rewardsHeaviesKillsEconomics { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Boars Kills Achievement Economics")]
            public int rewardsBoarsKillsEconomics { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Bears Kills Achievement Economics")]
            public int rewardsBearsKillsEconomics { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Chickens Kills Achievement Economics")]
            public int rewardsChickensKillsEconomics { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Stags Kills Achievement Economics")]
            public int rewardsStagsKillsEconomics { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Wolves Kills Achievement Economics")]
            public int rewardsWolvesKillsEconomics { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Bradleys Destroyed Achievement Economics")]
            public int rewardsBradleysKillsEconomics { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Choppers Destroyed Achievement Economics")]
            public int rewardsChoppersKillsEconomics { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Sleepers Kills Achievement Economics")]
            public int rewardsSleepersKillsEconomics { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Headshots Achievement Economics")]
            public int rewardsHeadshotsEconomics { get; set; }

            [JsonProperty(PropertyName = "Rewards for Barrels Destroyed Achievement Economics")]
            public int rewardsBarrelsEconomics { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Explosives Thrown Achievement Economics")]
            public int rewardsExplosivesEconomics { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Bullets Fired Achievement Economics")]
            public int rewardsBulletsEconomics { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Rockets Launched Achievement Economics")]
            public int rewardsRocketsEconomics { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Hemps Picked Up Achievement Economics")]
            public int rewardsHempsEconomics { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Pumpkins Picked Up Achievement Economics")]
            public int rewardsPumpkinsEconomics { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Corns Picked Up Achievement Economics")]
            public int rewardsCornsEconomics { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Stones Picked Up Achievement Economics")]
            public int rewardsStonesEconomics { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Wood Picked Up Achievement Economics")]
            public int rewardsWoodEconomics { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Metals Picked Up Achievement Economics")]
            public int rewardsMetalsEconomics { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Sulfurs Picked Up Achievement Economics")]
            public int rewardsSulfursEconomics { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Stones Gathered Achievement Economics")]
            public int rewardsStonesGatheredEconomics { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Wood Gathered Achievement Economics")]
            public int rewardsWoodGatheredEconomics { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Metals Gathered Achievement Economics")]
            public int rewardsMetalsGatheredEconomics { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Sulfurs Gathered Achievement Economics")]
            public int rewardsSulfursGatheredEconomics { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Cloth Skinned Achievement Economics")]
            public int rewardsClothSkinnedEconomics { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for PVP Kills Achievement RP")]
            public int rewardsPVPKillsRP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Scientists Kills Achievement RP")]
            public int rewardsScientistsKillsRP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Heavy Scientists Kills Achievement RP")]
            public int rewardsHeaviesKillsRP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Boars Kills Achievement RP")]
            public int rewardsBoarsKillsRP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Bears Kills Achievement RP")]
            public int rewardsBearsKillsRP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Chickens Kills Achievement RP")]
            public int rewardsChickensKillsRP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Stags Kills Achievement RP")]
            public int rewardsStagsKillsRP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Wolves Kills Achievement RP")]
            public int rewardsWolvesKillsRP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Bradleys Destroyed Achievement RP")]
            public int rewardsBradleysKillsRP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Choppers Destroyed Achievement RP")]
            public int rewardsChoppersKillsRP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Sleepers Kills Achievement RP")]
            public int rewardsSleepersKillsRP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Headshots Achievement RP")]
            public int rewardsHeadshotsRP { get; set; }

            [JsonProperty(PropertyName = "Rewards for Barrels Destroyed Achievement RP")]
            public int rewardsBarrelsRP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Explosives Thrown Achievement RP")]
            public int rewardsExplosivesRP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Bullets Fired Achievement RP")]
            public int rewardsBulletsRP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Rockets Launched Achievement RP")]
            public int rewardsRocketsRP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Hemps Picked Up Achievement RP")]
            public int rewardsHempsRP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Pumpkins Picked Up Achievement RP")]
            public int rewardsPumpkinsRP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Corns Picked Up Achievement RP")]
            public int rewardsCornsRP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Stones Picked Up Achievement RP")]
            public int rewardsStonesRP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Wood Picked Up Achievement RP")]
            public int rewardsWoodRP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Metals Picked Up Achievement RP")]
            public int rewardsMetalsRP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Sulfurs Picked Up Achievement RP")]
            public int rewardsSulfursRP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Stones Gathered Achievement RP")]
            public int rewardsStonesGatheredRP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Wood Gathered Achievement RP")]
            public int rewardsWoodGatheredRP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Metals Gathered Achievement RP")]
            public int rewardsMetalsGatheredRP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Sulfurs Gathered Achievement RP")]
            public int rewardsSulfursGatheredRP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Cloth Skinned Achievement RP")]
            public int rewardsClothSkinnedRP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for PVP Kills Achievement")]
            public Dictionary<string, int> rewardsPVPKills { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Scientists Kills Achievement")]
            public Dictionary<string, int> rewardsScientistsKills { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Heavy Scientists Kills Achievement")]
            public Dictionary<string, int> rewardsHeaviesKills { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Boars Kills Achievement")]
            public Dictionary<string, int> rewardsBoarsKills { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Bears Kills Achievement")]
            public Dictionary<string, int> rewardsBearsKills { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Chickens Kills Achievement")]
            public Dictionary<string, int> rewardsChickensKills { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Stags Kills Achievement")]
            public Dictionary<string, int> rewardsStagsKills { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Wolves Kills Achievement")]
            public Dictionary<string, int> rewardsWolvesKills { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Bradleys Destroyed Achievement")]
            public Dictionary<string, int> rewardsBradleysKills { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Choppers Destroyed Achievement")]
            public Dictionary<string, int> rewardsChoppersKills { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Sleepers Kills Achievement")]
            public Dictionary<string, int> rewardsSleepersKills { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Headshots Achievement")]
            public Dictionary<string, int> rewardsHeadshots { get; set; }

            [JsonProperty(PropertyName = "Rewards for Barrels Destroyed Achievement")]
            public Dictionary<string, int> rewardsBarrels { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Explosives Thrown Achievement")]
            public Dictionary<string, int> rewardsExplosives { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Bullets Fired Achievement")]
            public Dictionary<string, int> rewardsBullets { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Rockets Launched Achievement")]
            public Dictionary<string, int> rewardsRockets { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Hemps Picked Up Achievement")]
            public Dictionary<string, int> rewardsHemps { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Pumpkins Picked Up Achievement")]
            public Dictionary<string, int> rewardsPumpkins { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Corns Picked Up Achievement")]
            public Dictionary<string, int> rewardsCorns { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Stones Picked Up Achievement")]
            public Dictionary<string, int> rewardsStones { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Wood Picked Up Achievement")]
            public Dictionary<string, int> rewardsWood { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Metals Picked Up Achievement")]
            public Dictionary<string, int> rewardsMetals { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Sulfurs Picked Up Achievement")]
            public Dictionary<string, int> rewardsSulfurs { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Stones Gathered Achievement")]
            public Dictionary<string, int> rewardsStonesGathered { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Wood Gathered Achievement")]
            public Dictionary<string, int> rewardsWoodGathered { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Metals Gathered Achievement")]
            public Dictionary<string, int> rewardsMetalsGathered { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Sulfurs Gathered Achievement")]
            public Dictionary<string, int> rewardsSulfursGathered { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Cloth Skinned Achievement")]
            public Dictionary<string, int> rewardsClothSkinned { get; set; }
        }

        private class RewardSettingsVIP
        {
            [JsonProperty(PropertyName = "Rewards for PVP Kills Achievement Economics")]
            public int rewardsPVPKillsEconomicsVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Scientists Kills Achievement Economics")]
            public int rewardsScientistsKillsEconomicsVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Heavy Scientists Kills Achievement Economics")]
            public int rewardsHeaviesKillsEconomicsVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Boars Kills Achievement Economics")]
            public int rewardsBoarsKillsEconomicsVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Bears Kills Achievement Economics")]
            public int rewardsBearsKillsEconomicsVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Chickens Kills Achievement Economics")]
            public int rewardsChickensKillsEconomicsVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Stags Kills Achievement Economics")]
            public int rewardsStagsKillsEconomicsVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Wolves Kills Achievement Economics")]
            public int rewardsWolvesKillsEconomicsVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Bradleys Destroyed Achievement Economics")]
            public int rewardsBradleysKillsEconomicsVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Choppers Destroyed Achievement Economics")]
            public int rewardsChoppersKillsEconomicsVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Sleepers Kills Achievement Economics")]
            public int rewardsSleepersKillsEconomicsVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Headshots Achievement Economics")]
            public int rewardsHeadshotsEconomicsVIP { get; set; }

            [JsonProperty(PropertyName = "Rewards for Barrels Destroyed Achievement Economics")]
            public int rewardsBarrelsEconomicsVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Explosives Thrown Achievement Economics")]
            public int rewardsExplosivesEconomicsVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Bullets Fired Achievement Economics")]
            public int rewardsBulletsEconomicsVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Rockets Launched Achievement Economics")]
            public int rewardsRocketsEconomicsVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Hemps Picked Up Achievement Economics")]
            public int rewardsHempsEconomicsVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Pumpkins Picked Up Achievement Economics")]
            public int rewardsPumpkinsEconomicsVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Corns Picked Up Achievement Economics")]
            public int rewardsCornsEconomicsVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Stones Picked Up Achievement Economics")]
            public int rewardsStonesEconomicsVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Wood Picked Up Achievement Economics")]
            public int rewardsWoodEconomicsVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Metals Picked Up Achievement Economics")]
            public int rewardsMetalsEconomicsVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Sulfurs Picked Up Achievement Economics")]
            public int rewardsSulfursEconomicsVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Stones Gathered Achievement Economics")]
            public int rewardsStonesGatheredEconomicsVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Wood Gathered Achievement Economics")]
            public int rewardsWoodGatheredEconomicsVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Metals Gathered Achievement Economics")]
            public int rewardsMetalsGatheredEconomicsVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Sulfurs Gathered Achievement Economics")]
            public int rewardsSulfursGatheredEconomicsVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Cloth Skinned Achievement Economics")]
            public int rewardsClothSkinnedEconomicsVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for PVP Kills Achievement RP")]
            public int rewardsPVPKillsRPVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Scientists Kills Achievement RP")]
            public int rewardsScientistsKillsRPVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Heavy Scientists Kills Achievement RP")]
            public int rewardsHeaviesKillsRPVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Boars Kills Achievement RP")]
            public int rewardsBoarsKillsRPVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Bears Kills Achievement RP")]
            public int rewardsBearsKillsRPVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Chickens Kills Achievement RP")]
            public int rewardsChickensKillsRPVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Stags Kills Achievement RP")]
            public int rewardsStagsKillsRPVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Wolves Kills Achievement RP")]
            public int rewardsWolvesKillsRPVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Bradleys Destroyed Achievement RP")]
            public int rewardsBradleysKillsRPVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Choppers Destroyed Achievement RP")]
            public int rewardsChoppersKillsRPVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Sleepers Kills Achievement RP")]
            public int rewardsSleepersKillsRPVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Headshots Achievement RP")]
            public int rewardsHeadshotsRPVIP { get; set; }

            [JsonProperty(PropertyName = "Rewards for Barrels Destroyed Achievement RP")]
            public int rewardsBarrelsRPVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Explosives Thrown Achievement RP")]
            public int rewardsExplosivesRPVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Bullets Fired Achievement RP")]
            public int rewardsBulletsRPVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Rockets Launched Achievement RP")]
            public int rewardsRocketsRPVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Hemps Picked Up Achievement RP")]
            public int rewardsHempsRPVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Pumpkins Picked Up Achievement RP")]
            public int rewardsPumpkinsRPVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Corns Picked Up Achievement RP")]
            public int rewardsCornsRPVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Stones Picked Up Achievement RP")]
            public int rewardsStonesRPVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Wood Picked Up Achievement RP")]
            public int rewardsWoodRPVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Metals Picked Up Achievement RP")]
            public int rewardsMetalsRPVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Sulfurs Picked Up Achievement RP")]
            public int rewardsSulfursRPVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Stones Gathered Achievement RP")]
            public int rewardsStonesGatheredRPVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Wood Gathered Achievement RP")]
            public int rewardsWoodGatheredRPVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Metals Gathered Achievement RP")]
            public int rewardsMetalsGatheredRPVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Sulfurs Gathered Achievement RP")]
            public int rewardsSulfursGatheredRPVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Cloth Skinned Achievement RP")]
            public int rewardsClothSkinnedRPVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for PVP Kills Achievement")]
            public Dictionary<string, int> rewardsPVPKillsVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Scientists Kills Achievement")]
            public Dictionary<string, int> rewardsScientistsKillsVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Heavy Scientists Kills Achievement")]
            public Dictionary<string, int> rewardsHeaviesKillsVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Boars Kills Achievement")]
            public Dictionary<string, int> rewardsBoarsKillsVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Bears Kills Achievement")]
            public Dictionary<string, int> rewardsBearsKillsVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Chickens Kills Achievement")]
            public Dictionary<string, int> rewardsChickensKillsVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Stags Kills Achievement")]
            public Dictionary<string, int> rewardsStagsKillsVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Wolves Kills Achievement")]
            public Dictionary<string, int> rewardsWolvesKillsVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Bradleys Destroyed Achievement")]
            public Dictionary<string, int> rewardsBradleysKillsVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Choppers Destroyed Achievement")]
            public Dictionary<string, int> rewardsChoppersKillsVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Sleepers Kills Achievement")]
            public Dictionary<string, int> rewardsSleepersKillsVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Headshots Achievement")]
            public Dictionary<string, int> rewardsHeadshotsVIP { get; set; }

            [JsonProperty(PropertyName = "Rewards for Barrels Destroyed Achievement")]
            public Dictionary<string, int> rewardsBarrelsVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Explosives Thrown Achievement")]
            public Dictionary<string, int> rewardsExplosivesVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Bullets Fired Achievement")]
            public Dictionary<string, int> rewardsBulletsVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Rockets Launched Achievement")]
            public Dictionary<string, int> rewardsRocketsVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Hemps Picked Up Achievement")]
            public Dictionary<string, int> rewardsHempsVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Pumpkins Picked Up Achievement")]
            public Dictionary<string, int> rewardsPumpkinsVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Corns Picked Up Achievement")]
            public Dictionary<string, int> rewardsCornsVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Stones Picked Up Achievement")]
            public Dictionary<string, int> rewardsStonesVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Wood Picked Up Achievement")]
            public Dictionary<string, int> rewardsWoodVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Metals Picked Up Achievement")]
            public Dictionary<string, int> rewardsMetalsVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Sulfurs Picked Up Achievement")]
            public Dictionary<string, int> rewardsSulfursVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Stones Gathered Achievement")]
            public Dictionary<string, int> rewardsStonesGatheredVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Wood Gathered Achievement")]
            public Dictionary<string, int> rewardsWoodGatheredVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Metals Gathered Achievement")]
            public Dictionary<string, int> rewardsMetalsGatheredVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Sulfurs Gathered Achievement")]
            public Dictionary<string, int> rewardsSulfursGatheredVIP { get; set; }
            
            [JsonProperty(PropertyName = "Rewards for Cloth Skinned Achievement")]
            public Dictionary<string, int> rewardsClothSkinnedVIP { get; set; }
        }

        private class AchievementsImage
        {
            [JsonProperty("Enable Items Rewards")]
            public bool enableItems { get; set; }
            
            [JsonProperty("Enable Server Rewards RP Rewards")]
            public bool enableRP { get; set; }
            
            [JsonProperty("Enable Economics Rewards")]
            public bool enableEconomics { get; set; }
            
            [JsonProperty("Enable Global Broadcasting when unlocking an Achievement")]
            public bool achievementBroadcast { get; set; }
            
            [JsonProperty("PVP Kills Achievement Image")]
            public string imagePVPKills { get; set; }
            
            [JsonProperty("Scientists Kills Achievement Image")]
            public string imageScientistsKills { get; set; }
            
            [JsonProperty("Heavy Scientists Kills Achievement Image")]
            public string imageHeaviesKills { get; set; }
            
            [JsonProperty("Boars Kills Achievement Image")]
            public string imageBoarsKills { get; set; }
            
            [JsonProperty("Bears Kills Achievement Image")]
            public string imageBearsKills { get; set; }
            
            [JsonProperty("Chickens Kills Achievement Image")]
            public string imageChickensKills { get; set; }
            
            [JsonProperty("Stags Kills Achievement Image")]
            public string imageStagsKills { get; set; }
            
            [JsonProperty("Wolves Kills Achievement Image")]
            public string imageWolvesKills { get; set; }
            
            [JsonProperty("Bradleys Destroyed Achievement Image")]
            public string imageBradleysKills { get; set; }
            
            [JsonProperty("Choppers Destroyed Achievement Image")]
            public string imageChoppersKills { get; set; }
            
            [JsonProperty("Sleepers Kills Achievement Image")]
            public string imageSleepersKills { get; set; }
            
            [JsonProperty("Headshots Achievement Image")]
            public string imageHeadshots { get; set; }
            
            [JsonProperty("Barrels Destroyed Achievement Image")]
            public string imageBarrelsKills { get; set; }
            
            [JsonProperty("Explosives Thrown Achievement Image")]
            public string imageExplosivesThrown { get; set; }
            
            [JsonProperty("Bullets Fired Achievement Image")]
            public string imageBulletsFired { get; set; }
            
            [JsonProperty("Rockets Launched Achievement Image")]
            public string imageRocketsLaunched { get; set; }
            
            [JsonProperty("Hemps Picked Up Achievement Image")]
            public string imageHemps { get; set; }
            
            [JsonProperty("Pumpkins Picked Up Achievement Image")]
            public string imagePumpkins { get; set; }
            
            [JsonProperty("Corns Picked Up Achievement Image")]
            public string imageCorns { get; set; }
            
            [JsonProperty("Stones Picked Up Achievement Image")]
            public string imageStonesPicked { get; set; }
            
            [JsonProperty("Wood Picked Up Achievement Image")]
            public string imageWoodPicked { get; set; }
            
            [JsonProperty("Metals Picked Up Achievement Image")]
            public string imageMetalsPicked { get; set; }
            
            [JsonProperty("Sulfurs Picked Up Achievement Image")]
            public string imageSulfursPicked { get; set; }
            
            [JsonProperty("Stones Gathered Achievement Image")]
            public string imageStonesGathered { get; set; }
            
            [JsonProperty("Wood Gathered Achievement Image")]
            public string imageWoodGathered { get; set; }
            
            [JsonProperty("Metals Gathered Achievement Image")]
            public string imageMetalsGathered { get; set; }
            
            [JsonProperty("Sulfurs Gathered Achievement Image")]
            public string imageSulfursGathered { get; set; }
            
            [JsonProperty("Cloth Skinned Achievement Image")]
            public string imageClothSkinned { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                
                if (_config == null)
                {
                    throw new Exception();
                }
                
                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        protected override void LoadDefaultConfig()
        {
            _config = new Configuration
            {
                AchievementsSet = new AchievementsSettings
                {
                    minPVPKills = 40,
                    minScientistsKills = 50,
                    minHeaviesKills = 25,
                    minBoarsKills = 60,
                    minBearsKills = 30,
                    minChickensKills = 100,
                    minStagsKills = 45,
                    minWolvesKills = 30,
                    minBradleysKills = 15,
                    minChoppersKills = 20,
                    minSleepersKills = 30,
                    minHeadshots = 50,
                    minBarrelsDestroyed = 100,
                    minExplosives = 70,
                    minBullets = 1000,
                    minRockets = 20,
                    minHemps = 100,
                    minPumpkins = 100,
                    minCorns = 100,
                    minStones = 80,
                    minWood = 100,
                    minMetals = 60,
                    minSulfurs = 50,
                    minStonesGathered = 50000,
                    minWoodGathered = 80000,
                    minMetalsGathered = 40000,
                    minSulfursGathered = 30000,
                    minClothSkinned = 2000,
                },
                AchievementsSetVIP = new AchievementsSettingsVIP
                {
                    minPVPKillsVIP = 20,
                    minScientistsKillsVIP = 20,
                    minHeaviesKillsVIP = 15,
                    minBoarsKillsVIP = 30,
                    minBearsKillsVIP = 15,
                    minChickensKillsVIP = 50,
                    minStagsKillsVIP = 22,
                    minWolvesKillsVIP = 15,
                    minBradleysKillsVIP = 7,
                    minChoppersKillsVIP = 10,
                    minSleepersKillsVIP = 15,
                    minHeadshotsVIP = 25,
                    minBarrelsDestroyedVIP = 50,
                    minExplosivesVIP = 35,
                    minBulletsVIP = 500,
                    minRocketsVIP = 10,
                    minHempsVIP = 50,
                    minPumpkinsVIP = 100,
                    minCornsVIP = 100,
                    minStonesVIP = 40,
                    minWoodVIP = 50,
                    minMetalsVIP = 30,
                    minSulfursVIP = 25,
                    minStonesGatheredVIP = 25000,
                    minWoodGatheredVIP = 40000,
                    minMetalsGatheredVIP = 20000,
                    minSulfursGatheredVIP = 15000,
                    minClothSkinnedVIP = 1000,
                },
                RewardSet = new RewardSettings
                {
                    rewardsPVPKillsEconomics = 100,
                    rewardsScientistsKillsEconomics = 100,
                    rewardsHeaviesKillsEconomics = 100,
                    rewardsBoarsKillsEconomics = 100,
                    rewardsBearsKillsEconomics = 100,
                    rewardsChickensKillsEconomics = 100,
                    rewardsStagsKillsEconomics = 100,
                    rewardsWolvesKillsEconomics = 100,
                    rewardsBradleysKillsEconomics = 100,
                    rewardsChoppersKillsEconomics = 100,
                    rewardsSleepersKillsEconomics = 100,
                    rewardsHeadshotsEconomics = 100,
                    rewardsBarrelsEconomics = 100,
                    rewardsExplosivesEconomics = 100,
                    rewardsBulletsEconomics = 100,
                    rewardsRocketsEconomics = 100,
                    rewardsHempsEconomics = 100,
                    rewardsPumpkinsEconomics = 100,
                    rewardsCornsEconomics = 100,
                    rewardsStonesEconomics = 100,
                    rewardsWoodEconomics = 100,
                    rewardsMetalsEconomics = 100,
                    rewardsSulfursEconomics = 100,
                    rewardsStonesGatheredEconomics = 100,
                    rewardsWoodGatheredEconomics = 100,
                    rewardsMetalsGatheredEconomics = 100,
                    rewardsSulfursGatheredEconomics = 100,
                    rewardsClothSkinnedEconomics = 100,
                    rewardsPVPKillsRP = 100,
                    rewardsScientistsKillsRP = 100,
                    rewardsHeaviesKillsRP = 100,
                    rewardsBoarsKillsRP = 100,
                    rewardsBearsKillsRP = 100,
                    rewardsChickensKillsRP = 100,
                    rewardsStagsKillsRP = 100,
                    rewardsWolvesKillsRP = 100,
                    rewardsBradleysKillsRP = 100,
                    rewardsChoppersKillsRP = 100,
                    rewardsSleepersKillsRP = 100,
                    rewardsHeadshotsRP = 100,
                    rewardsBarrelsRP = 100,
                    rewardsExplosivesRP = 100,
                    rewardsBulletsRP = 100,
                    rewardsRocketsRP = 100,
                    rewardsHempsRP = 100,
                    rewardsPumpkinsRP = 100,
                    rewardsCornsRP = 100,
                    rewardsStonesRP = 100,
                    rewardsWoodRP = 100,
                    rewardsMetalsRP = 100,
                    rewardsSulfursRP = 100,
                    rewardsStonesGatheredRP = 100,
                    rewardsWoodGatheredRP = 100,
                    rewardsMetalsGatheredRP = 100,
                    rewardsSulfursGatheredRP = 100,
                    rewardsClothSkinnedRP = 100,
                    
                    rewardsPVPKills = new Dictionary<string, int>
                    {
                        {"rock", 1},
                        {"torch", 1},
                        {"stones", 500},
                    },
                    rewardsScientistsKills = new Dictionary<string, int>
                    {
                        {"rock", 1},
                        {"torch", 1},
                        {"stones", 500},
                    },
                    rewardsHeaviesKills = new Dictionary<string, int>
                    {
                        {"rock", 1},
                        {"torch", 1},
                        {"stones", 500},
                    },
                    rewardsBoarsKills = new Dictionary<string, int>
                    {
                        {"rock", 1},
                        {"torch", 1},
                        {"stones", 500},
                    },
                    rewardsBearsKills = new Dictionary<string, int>
                    {
                        {"rock", 1},
                        {"torch", 1},
                        {"stones", 500},
                    },
                    rewardsChickensKills = new Dictionary<string, int>
                    {
                        {"rock", 1},
                        {"torch", 1},
                        {"stones", 500},
                    },
                    rewardsStagsKills = new Dictionary<string, int>
                    {
                        {"rock", 1},
                        {"torch", 1},
                        {"stones", 500},
                    },
                    rewardsWolvesKills = new Dictionary<string, int>
                    {
                        {"rock", 1},
                        {"torch", 1},
                        {"stones", 500},
                    },
                    rewardsBradleysKills = new Dictionary<string, int>
                    {
                        {"rock", 1},
                        {"torch", 1},
                        {"stones", 500},
                    },
                    rewardsChoppersKills = new Dictionary<string, int>
                    {
                        {"rock", 1},
                        {"torch", 1},
                        {"stones", 500},
                    },
                    rewardsSleepersKills = new Dictionary<string, int>
                    {
                        {"rock", 1},
                        {"torch", 1},
                        {"stones", 500},
                    },
                    rewardsHeadshots = new Dictionary<string, int>
                    {
                        {"rock", 1},
                        {"torch", 1},
                        {"stones", 500},
                    },
                    rewardsBarrels = new Dictionary<string, int>
                    {
                        {"rock", 1},
                        {"torch", 1},
                        {"stones", 500},
                    },
                    rewardsExplosives = new Dictionary<string, int>
                    {
                        {"rock", 1},
                        {"torch", 1},
                        {"stones", 500},
                    },
                    rewardsBullets = new Dictionary<string, int>
                    {
                        {"rock", 1},
                        {"torch", 1},
                        {"stones", 500},
                    },
                    rewardsRockets = new Dictionary<string, int>
                    {
                        {"rock", 1},
                        {"torch", 1},
                        {"stones", 500},
                    },
                    rewardsHemps = new Dictionary<string, int>
                    {
                        {"rock", 1},
                        {"torch", 1},
                        {"stones", 500},
                    },
                    rewardsPumpkins = new Dictionary<string, int>
                    {
                        {"rock", 1},
                        {"torch", 1},
                        {"stones", 500},
                    },
                    rewardsCorns = new Dictionary<string, int>
                    {
                        {"rock", 1},
                        {"torch", 1},
                        {"stones", 500},
                    },
                    rewardsStones = new Dictionary<string, int>
                    {
                        {"rock", 1},
                        {"torch", 1},
                        {"stones", 500},
                    },
                    rewardsWood = new Dictionary<string, int>
                    {
                        {"rock", 1},
                        {"torch", 1},
                        {"stones", 500},
                    },
                    rewardsMetals = new Dictionary<string, int>
                    {
                        {"rock", 1},
                        {"torch", 1},
                        {"stones", 500},
                    },
                    rewardsSulfurs = new Dictionary<string, int>
                    {
                        {"rock", 1},
                        {"torch", 1},
                        {"stones", 500},
                    },
                    rewardsStonesGathered = new Dictionary<string, int>
                    {
                        {"rock", 1},
                        {"torch", 1},
                        {"stones", 500},
                    },
                    rewardsWoodGathered = new Dictionary<string, int>
                    {
                        {"rock", 1},
                        {"torch", 1},
                        {"stones", 500},
                    },
                    rewardsMetalsGathered = new Dictionary<string, int>
                    {
                        {"rock", 1},
                        {"torch", 1},
                        {"stones", 500},
                    },
                    rewardsSulfursGathered = new Dictionary<string, int>
                    {
                        {"rock", 1},
                        {"torch", 1},
                        {"stones", 500},
                    },
                    rewardsClothSkinned = new Dictionary<string, int>
                    {
                        {"rock", 1},
                        {"torch", 1},
                        {"stones", 500},
                    },
                },
                RewardSetVIP = new RewardSettingsVIP
                {
                    rewardsPVPKillsEconomicsVIP = 100,
                    rewardsScientistsKillsEconomicsVIP = 100,
                    rewardsHeaviesKillsEconomicsVIP = 100,
                    rewardsBoarsKillsEconomicsVIP = 100,
                    rewardsBearsKillsEconomicsVIP = 100,
                    rewardsChickensKillsEconomicsVIP = 100,
                    rewardsStagsKillsEconomicsVIP = 100,
                    rewardsWolvesKillsEconomicsVIP = 100,
                    rewardsBradleysKillsEconomicsVIP = 100,
                    rewardsChoppersKillsEconomicsVIP = 100,
                    rewardsSleepersKillsEconomicsVIP = 100,
                    rewardsHeadshotsEconomicsVIP = 100,
                    rewardsBarrelsEconomicsVIP = 100,
                    rewardsExplosivesEconomicsVIP = 100,
                    rewardsBulletsEconomicsVIP = 100,
                    rewardsRocketsEconomicsVIP = 100,
                    rewardsHempsEconomicsVIP = 100,
                    rewardsPumpkinsEconomicsVIP = 100,
                    rewardsCornsEconomicsVIP = 100,
                    rewardsStonesEconomicsVIP = 100,
                    rewardsWoodEconomicsVIP = 100,
                    rewardsMetalsEconomicsVIP = 100,
                    rewardsSulfursEconomicsVIP = 100,
                    rewardsStonesGatheredEconomicsVIP = 100,
                    rewardsWoodGatheredEconomicsVIP = 100,
                    rewardsMetalsGatheredEconomicsVIP = 100,
                    rewardsSulfursGatheredEconomicsVIP = 100,
                    rewardsClothSkinnedEconomicsVIP = 100,
                    rewardsPVPKillsRPVIP = 100,
                    rewardsScientistsKillsRPVIP = 100,
                    rewardsHeaviesKillsRPVIP = 100,
                    rewardsBoarsKillsRPVIP = 100,
                    rewardsBearsKillsRPVIP = 100,
                    rewardsChickensKillsRPVIP = 100,
                    rewardsStagsKillsRPVIP = 100,
                    rewardsWolvesKillsRPVIP = 100,
                    rewardsBradleysKillsRPVIP = 100,
                    rewardsChoppersKillsRPVIP = 100,
                    rewardsSleepersKillsRPVIP = 100,
                    rewardsHeadshotsRPVIP = 100,
                    rewardsBarrelsRPVIP = 100,
                    rewardsExplosivesRPVIP = 100,
                    rewardsBulletsRPVIP = 100,
                    rewardsRocketsRPVIP = 100,
                    rewardsHempsRPVIP = 100,
                    rewardsPumpkinsRPVIP = 100,
                    rewardsCornsRPVIP = 100,
                    rewardsStonesRPVIP = 100,
                    rewardsWoodRPVIP = 100,
                    rewardsMetalsRPVIP = 100,
                    rewardsSulfursRPVIP = 100,
                    rewardsStonesGatheredRPVIP = 100,
                    rewardsWoodGatheredRPVIP = 100,
                    rewardsMetalsGatheredRPVIP = 100,
                    rewardsSulfursGatheredRPVIP = 100,
                    rewardsClothSkinnedRPVIP = 100,
                    
                    rewardsPVPKillsVIP = new Dictionary<string, int>
                    {
                        {"rock", 3},
                        {"torch", 2},
                        {"stones", 1000},
                    },
                    rewardsScientistsKillsVIP = new Dictionary<string, int>
                    {
                        {"rock", 1},
                        {"torch", 1},
                        {"stones", 500},
                    },
                    rewardsHeaviesKillsVIP = new Dictionary<string, int>
                    {
                        {"rock", 1},
                        {"torch", 1},
                        {"stones", 500},
                    },
                    rewardsBoarsKillsVIP = new Dictionary<string, int>
                    {
                        {"rock", 1},
                        {"torch", 1},
                        {"stones", 500},
                    },
                    rewardsBearsKillsVIP = new Dictionary<string, int>
                    {
                        {"rock", 1},
                        {"torch", 1},
                        {"stones", 500},
                    },
                    rewardsChickensKillsVIP = new Dictionary<string, int>
                    {
                        {"rock", 1},
                        {"torch", 1},
                        {"stones", 500},
                    },
                    rewardsStagsKillsVIP = new Dictionary<string, int>
                    {
                        {"rock", 1},
                        {"torch", 1},
                        {"stones", 500},
                    },
                    rewardsWolvesKillsVIP = new Dictionary<string, int>
                    {
                        {"rock", 1},
                        {"torch", 1},
                        {"stones", 500},
                    },
                    rewardsBradleysKillsVIP = new Dictionary<string, int>
                    {
                        {"rock", 1},
                        {"torch", 1},
                        {"stones", 500},
                    },
                    rewardsChoppersKillsVIP = new Dictionary<string, int>
                    {
                        {"rock", 1},
                        {"torch", 1},
                        {"stones", 500},
                    },
                    rewardsSleepersKillsVIP = new Dictionary<string, int>
                    {
                        {"rock", 3},
                        {"torch", 2},
                        {"stones", 1000},
                    },
                    rewardsHeadshotsVIP = new Dictionary<string, int>
                    {
                        {"rock", 3},
                        {"torch", 2},
                        {"stones", 1000},
                    },
                    rewardsBarrelsVIP = new Dictionary<string, int>
                    {
                        {"rock", 3},
                        {"torch", 2},
                        {"stones", 1000},
                    },
                    rewardsExplosivesVIP = new Dictionary<string, int>
                    {
                        {"rock", 3},
                        {"torch", 2},
                        {"stones", 1000},
                    },
                    rewardsBulletsVIP = new Dictionary<string, int>
                    {
                        {"rock", 3},
                        {"torch", 2},
                        {"stones", 1000},
                    },
                    rewardsRocketsVIP = new Dictionary<string, int>
                    {
                        {"rock", 1},
                        {"torch", 1},
                        {"stones", 500},
                    },
                    rewardsHempsVIP = new Dictionary<string, int>
                    {
                        {"rock", 3},
                        {"torch", 2},
                        {"stones", 1000},
                    },
                    rewardsPumpkinsVIP = new Dictionary<string, int>
                    {
                        {"rock", 1},
                        {"torch", 1},
                        {"stones", 500},
                    },
                    rewardsCornsVIP = new Dictionary<string, int>
                    {
                        {"rock", 1},
                        {"torch", 1},
                        {"stones", 500},
                    },
                    rewardsStonesVIP = new Dictionary<string, int>
                    {
                        {"rock", 3},
                        {"torch", 2},
                        {"stones", 1000},
                    },
                    rewardsWoodVIP = new Dictionary<string, int>
                    {
                        {"rock", 3},
                        {"torch", 2},
                        {"stones", 1000},
                    },
                    rewardsMetalsVIP = new Dictionary<string, int>
                    {
                        {"rock", 3},
                        {"torch", 2},
                        {"stones", 1000},
                    },
                    rewardsSulfursVIP = new Dictionary<string, int>
                    {
                        {"rock", 3},
                        {"torch", 2},
                        {"stones", 1000},
                    },
                    rewardsStonesGatheredVIP = new Dictionary<string, int>
                    {
                        {"rock", 1},
                        {"torch", 1},
                        {"stones", 500},
                    },
                    rewardsWoodGatheredVIP = new Dictionary<string, int>
                    {
                        {"rock", 1},
                        {"torch", 1},
                        {"stones", 500},
                    },
                    rewardsMetalsGatheredVIP = new Dictionary<string, int>
                    {
                        {"rock", 1},
                        {"torch", 1},
                        {"stones", 500},
                    },
                    rewardsSulfursGatheredVIP = new Dictionary<string, int>
                    {
                        {"rock", 1},
                        {"torch", 1},
                        {"stones", 500},
                    },
                    rewardsClothSkinnedVIP = new Dictionary<string, int>
                    {
                        {"rock", 1},
                        {"torch", 1},
                        {"stones", 500},
                    },
                },
                AchievementsImg = new AchievementsImage
                {
                    enableItems = false,
                    enableRP = true,
                    achievementBroadcast = false,
                    imagePVPKills = "https://i.postimg.cc/Yq817CHv/Killer.png",
                    imageScientistsKills = "https://i.postimg.cc/vTvX1h8W/Scientist-Hazmat-Suit-icon.png",
                    imageHeaviesKills = "https://i.postimg.cc/sgffZgmS/juggernaut.png",
                    imageBoarsKills = "https://i.postimg.cc/QCsPFHDW/Piggy.png",
                    imageBearsKills = "https://i.postimg.cc/9XBVGqMM/Pookie.png",
                    imageChickensKills = "https://i.postimg.cc/5tCcRXSZ/Chicken-Dinner.png",
                    imageStagsKills = "https://i.postimg.cc/VLJcCZbf/Bambi.png",
                    imageWolvesKills = "https://i.postimg.cc/fLWbn7qV/Wolf.png",
                    imageBradleysKills = "https://i.postimg.cc/6p2wkWVx/tank-PNG1320.png",
                    imageChoppersKills = "https://i.postimg.cc/pVhwVGhK/Chopper.png",
                    imageSleepersKills = "https://i.postimg.cc/W4xGyYPS/Nightmare.png",
                    imageHeadshots = "https://i.postimg.cc/d31x9SR6/headshot.png",
                    imageBarrelsKills = "https://i.postimg.cc/9XNtvftJ/Golden-Barrel.png",
                    imageExplosivesThrown = "https://i.postimg.cc/FFyRJ397/Timed-Explosive-Charge-icon.png",
                    imageBulletsFired = "https://i.postimg.cc/KYjkPz0C/5-56-Rifle-Ammo-icon.png",
                    imageRocketsLaunched = "https://i.postimg.cc/wvKq1TZX/Rocket-icon.png",
                    imageHemps = "https://i.postimg.cc/WpWK4TjH/cannabis.png",
                    imagePumpkins = "https://i.postimg.cc/ydzKvBWP/pies.png",
                    imageCorns = "https://i.postimg.cc/RhtTnKG4/Corn-icon.png",
                    imageStonesPicked = "https://i.postimg.cc/HkcjNYsD/StoneAge.png",
                    imageWoodPicked = "https://i.postimg.cc/sXbk4HR5/Wood-icon.png",
                    imageMetalsPicked = "https://i.postimg.cc/cLR6WDYX/Metal.png",
                    imageSulfursPicked = "https://i.postimg.cc/k57Ggxv9/Raider.png",
                    imageStonesGathered = "https://i.postimg.cc/CLtrbBMf/Jackhammer-icon.png",
                    imageWoodGathered = "https://i.postimg.cc/m2ZbznX5/Hatchet-icon.png",
                    imageMetalsGathered = "https://i.postimg.cc/FRDDz1nz/Pick-Axe-icon.png",
                    imageSulfursGathered = "https://i.postimg.cc/HsVT9h5v/Evil-Miner.png",
                    imageClothSkinned = "https://i.postimg.cc/rp99yYq4/Skinner.png"
                }
            };
        }

        #endregion

        #region Lang

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [LangKeys.NoPermission] = "<color=#e3e3e3>You do not have permission to use this.</color>",
                [LangKeys.AchievementUnlocked] = "<color=#e3e3e3>[<color=#ACFA58>Achievement System</color>] Achievement <color=#ACFA58>unlocked</color>, type <color=#ff9329>/achievements</color> to redeem it or <color=#ff9329>/checkachievements</color> to check your stats!</color>"
            }, this);
        }
        
        private class LangKeys
        {
            public const string NoPermission = nameof(NoPermission);
            public const string AchievementUnlocked = nameof(AchievementUnlocked);
        }

        #endregion

        #region Data

        private PluginData _data;

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name, _data);
        }

        private void LoadData()
        {
            try
            {
                _data = Interface.Oxide.DataFileSystem.ReadObject<PluginData>(Name);
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
            }

            if (_data == null)
            {
                _data = new PluginData();
            }
        }

        private class PluginData
        {
            public Hash<ulong, PlayerData> PlayerDatas { get; set; } = new Hash<ulong, PlayerData>();
        }

        private class PlayerData
        {
            public string Name { get; set; }
            public int PVPKills { get; set; }
            public int ScientistsKilled { get; set; }
            public int HeaviesKilled { get; set; }
            public int BoarsKilled { get; set; }
            public int BearsKilled { get; set; }
            public int ChickensKilled { get; set; }
            public int StagsKilled { get; set; }
            public int WolvesKilled { get; set; }
            public int BradleyDestroyed { get; set; }
            public int HelicopterDestroyed { get; set; }
            public int SleepersKilled { get; set; }
            public int HeadShots { get; set; }
            public int BarrelsDestroyed { get; set; }
            public int ExplosivesThrown { get; set; }
            public int BulletsFired { get; set; }
            public int RocketsLaunched { get; set; }
            public int HempPickedUp { get; set; }
            public int PumpkinPickedUp { get; set; }
            public int CornPickedUp { get; set; }
            public int StonePickedUp { get; set; }
            public int WoodPickedUp { get; set; }
            public int MetalPickedUp { get; set; }
            public int SulfurPickedUp { get; set; }
            public int StoneGathered { get; set; }
            public int WoodGathered { get; set; }
            public int MetalGathered { get; set; }
            public int SulfurGathered { get; set; }
            public int ClothSkinned { get; set; }
            public List<string> AchievementsRedeemed { get; set; } = new List<string>();
            public bool DisplayWood { get; set; }
            public bool DisplayStone { get; set; }
            public bool DisplayMetal { get; set; }
            public bool DisplaySulfur { get; set; }
            public bool DisplayCloth { get; set; }
        }
        
        private PlayerData GetPlayerData(ulong playerId)
        {
            PlayerData data = _data.PlayerDatas[playerId];
            if (data == null)
            {
                data = new PlayerData();
                _data.PlayerDatas[playerId] = data;
            }
            
            return data;
        }

        #endregion

        #region Helpers

        private bool IsFriend(ulong playerId, ulong targetId)
        {
            if (!Friends)
            {
                return false;
            }
            
            return Friends.Call<bool>("AreFriends", playerId, targetId);
        }

        private bool IsClanMember(ulong playerId, ulong targetId)
        {
            if (!Clans)
            {
                return false;
            }

            return Clans.Call<bool>("IsClanMember", playerId, targetId);
        }

        private string Lang(string key, string id, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        
        private bool HasPermission(BasePlayer player, string perm) => permission.UserHasPermission(player.UserIDString, perm);
        
        private void SendEffectTo(string effect, BasePlayer player)
        {
            if (player == null)
            {
                return;
            }
            
            Effect effectInstance = new Effect();
            effectInstance.Init(Effect.Type.Generic, player, 0, Vector3.up, Vector3.zero);
            effectInstance.pooledstringid = StringPool.Get(effect);
            NetWrite write = Net.sv.StartWrite();
            write.PacketID(Message.Type.Effect);
            effectInstance.WriteToStream(write);
            write.Send(new SendInfo(player.net.connection));
            effectInstance.Clear();
        }

        #endregion
        
        #region UI Helpers
        
        private const string UiPanelName = "AchievementSystem";
        
        private static class Ui
        {
            private static string UiPanel { get; set; }

            public static CuiElementContainer Container(string color, float alpha, float fadeIn, float fadeOut, UiPosition pos, bool useCursor, string panel, string parent = "Hud")
            {
                UiPanel = panel;
                return new CuiElementContainer
                {
                    {
                        new CuiPanel
                        {
                            Image = { Color = Color(color, alpha), FadeIn = fadeIn},
                            RectTransform = { AnchorMin = pos.GetMin(), AnchorMax = pos.GetMax() },
                            CursorEnabled = useCursor,
                            FadeOut = fadeOut
                        },
                        new CuiElement().Parent = parent,
                        panel
                    }
                };
            }
            
            [Flags]
            public enum BorderEnum : byte
            {
                Top = 1,
                Left = 2,
                Bottom = 4,
                Right = 8,
                All = 15
            }

            public static void Outline(CuiElementContainer container, UiPosition pos, string color, float alpha, float fadeIn, float fadeOut, int size = 1, BorderEnum border = BorderEnum.All)
            {
                if ((border & BorderEnum.Top) == BorderEnum.Top)
                {
                    container.Add(new CuiPanel
                    {
                        RectTransform = {AnchorMin = $"{pos.XMin} {pos.YMax}", AnchorMax = $"{pos.XMax} {pos.YMax}", OffsetMin = $"0 -{size}"},
                        Image = { Color = Color(color, alpha), FadeIn = fadeIn},
                        FadeOut = fadeOut
                    }, UiPanel);
                }
                
                if ((border & BorderEnum.Left) == BorderEnum.Left)
                {
                    container.Add(new CuiPanel
                    {
                        RectTransform = {AnchorMin = $"{pos.XMin} {pos.YMin}", AnchorMax = $"{pos.XMin} {pos.YMax}", OffsetMin = $"-{size} -{size}", OffsetMax = $"1 {size}"},
                        Image = { Color = Color(color, alpha), FadeIn = fadeIn},
                        FadeOut = fadeOut
                    }, UiPanel);
                }
                
                if ((border & BorderEnum.Bottom) == BorderEnum.Bottom)
                {
                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = $"{pos.XMin} {pos.YMin}", AnchorMax = $"{pos.XMax} {pos.YMin}", OffsetMin = $"0 -{size}" },
                        Image = { Color = Color(color, alpha), FadeIn = fadeIn},
                        FadeOut = fadeOut
                    }, UiPanel);
                }

                if ((border & BorderEnum.Right) == BorderEnum.Right)
                {
                    container.Add(new CuiPanel
                    {
                        RectTransform = {AnchorMin = $"{pos.XMax} {pos.YMin}", AnchorMax = $"{pos.XMax} {pos.YMax}", OffsetMin = $"0 -{size}", OffsetMax = $"{size * 2} {size}"},
                        Image = { Color = Color(color, alpha), FadeIn = fadeIn},
                        FadeOut = fadeOut
                    }, UiPanel);
                }
            }
            
            public static void TextOutline(CuiElementContainer container, string text, string tcolor, float talpha, string ocolor, float oalpha, int size, UiPosition pos, float fadeIn, float fadeOut, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiElement
                {
                    Components =
                    {
                        new CuiTextComponent { Color = Color(tcolor, talpha), FontSize = size, Align = align, Text = text, Font = "robotocondensed-regular.ttf", FadeIn = fadeIn},
                        new CuiOutlineComponent { Distance = "1.5 1.5", Color = Color(ocolor, oalpha) },
                        new CuiRectTransformComponent { AnchorMin = pos.GetMin(), AnchorMax = pos.GetMax() }
                    },
                    FadeOut = fadeOut,
                    Parent = UiPanel
                });
            }

            public static void Label(CuiElementContainer container, string text, float alpha, string color, float fadeIn, float fadeOut, int size, UiPosition pos, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiLabel
                {
                    Text = { FontSize = size, Align = align, Text = text, Font = "robotocondensed-regular.ttf", Color = Color(color, alpha), FadeIn = fadeIn},
                    RectTransform = { AnchorMin = pos.GetMin(), AnchorMax = pos.GetMax() },
                    FadeOut = fadeOut
                },
                UiPanel);
            }
            
            public static void Panel(CuiElementContainer container, string color, float alpha, float fadeIn, float fadeOut, UiPosition pos, bool useCursor)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = Color(color, alpha), FadeIn = fadeIn },
                    RectTransform = { AnchorMin = pos.GetMin(), AnchorMax = pos.GetMax() },
                    CursorEnabled = useCursor,
                    FadeOut = fadeOut
                },
                    UiPanel);
            }

            public static void Button(CuiElementContainer container, string color, float alpha, string text, string tcolor, float talpha, float fadeIn, float fadeOut, int size, UiPosition pos, string command, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiButton
                {
                    Button = { Color = Color(color, alpha), Command = command, FadeIn = fadeIn },
                    RectTransform = { AnchorMin = pos.GetMin(), AnchorMax = pos.GetMax() },
                    Text = { Text = text, Color = Color(tcolor, talpha), FontSize = size, Font = "robotocondensed-regular.ttf", Align = align, FadeIn = fadeIn },
                    FadeOut = fadeOut
                },
                UiPanel);
            }
            
            public static void Image(CuiElementContainer container, string url, UiPosition pos, string color, float alpha, float fadeIn, float fadeOut)
            {
                container.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = UiPanel,
                    Components =
                    {
                        new CuiRawImageComponent { Png = !url?.StartsWith("http") ?? false ? url : null, Url = url?.StartsWith("http") ?? false ? url : null, FadeIn = fadeIn, Color = Color(color, alpha) },
                        new CuiRectTransformComponent { AnchorMin = pos.GetMin(), AnchorMax = pos.GetMax() }
                    },
                    FadeOut = fadeOut
                });
            }

            private static string Color(string hexColor, float alpha)
            {
                hexColor = hexColor.TrimStart('#');
                int red = int.Parse(hexColor.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                int green = int.Parse(hexColor.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                int blue = int.Parse(hexColor.Substring(4, 2), NumberStyles.AllowHexSpecifier);
                return $"{red / 255.0} {green / 255.0} {blue / 255.0} {alpha / 255}";
            }
        }

        private void DestroyAllUi(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, UiPanelName);
        }

        private class UiPosition
        {
            public float XMin { get; set; }
            public float YMin { get; set; }
            public float XMax { get; set; }
            public float YMax { get; set; }
            private bool Validate { get; }

            public UiPosition(float xMin, float yMin, float xMax, float yMax, bool val = true)
            {
                XMin = xMin;
                YMin = yMin;
                XMax = xMax;
                YMax = yMax;
                Validate = val;
            }

            public string GetMin() => $"{XMin} {YMin}";
            public string GetMax() => $"{XMax} {YMax}";

            public void SetX(float xPos, float xMax)
            {
                XMin = xPos;
                XMax = xMax;
            }

            public void SetY(float yMin, float yMax)
            {
                YMin = yMin;
                YMax = yMax;
            }

            public void ModifyX(float delta)
            {
                XMin += delta;
                XMax += delta;
            }

            public void ModifyXPad(float padding)
            {
                float spacing = (XMax - XMin + Math.Abs(padding)) * (padding < 0 ? -1 : 1);
                XMin += spacing;
                XMax += spacing;
            }

            public UiPosition CopyX(float yPos, float yMax)
            {
                return new UiPosition(XMin, yPos, XMax, yMax);
            }

            public void ModifyY(float delta)
            {
                YMin += delta;
                YMax += delta;
            }

            public UiPosition CopyY(float xPos, float yMax)
            {
                return new UiPosition(xPos, YMin, yMax, YMax);
            }

            public void ModifyYPad(float padding)
            {
                float spacing = (YMax - YMin + Math.Abs(padding)) * (padding < 0 ? -1 : 1);
                YMin += spacing;
                YMax += spacing;
            }

            public override string ToString()
            {
                return $"{XMin} {YMin} {XMax} {YMax}";
            }
        }

        #endregion
        
        #region UI Creation & Display

        private void DisplayUI(BasePlayer player, string text, string imageName)
        {
            UiPosition containerPos = new UiPosition(0.65f, 0.022f, 0.831f, 0.134f);
            CuiElementContainer container = Ui.Container("#ffffff", 0, 1f, 1f, containerPos, false, UiPanelName,"Overlay");
            
            UiPosition imagePos = new UiPosition(0f, 0.25f, 1f, 0.85f);
            Ui.Image(container, ImageLibrary.Call("GetImage", "AchievementSystemUI").ToString(), imagePos, "#ffffff", 255, 1f, 1f); //Achievement Template
            
            UiPosition logoPos = new UiPosition(0.02f, 0.35f, 0.25f, 0.70f);
            Ui.Image(container, ImageLibrary.Call("GetImage", imageName).ToString(), logoPos, "#ffffff", 255f, 1f, 1f); //Achievement Logo

            UiPosition labelPos = new UiPosition(0.1f, 0.40f, 0.99f, 0.99f);
            Ui.Label(container, "<b>ACHIEVEMENT UNLOCKED</b>", 150f, "#ffffff", 1f, 1f, 12, labelPos); //Achievement Text
            
            UiPosition textPos = new UiPosition(0.1f, -0.12f, 1f, 1f);
            Ui.Label(container, $"<b>[ {text} ]</b>", 150f, "#ffffff", 1f, 1f, 18, textPos); //Achievement Type

            timer.Once(4f, () => DestroyAllUi(player));
            CuiHelper.DestroyUi(player, UiPanelName);
            CuiHelper.AddUi(player, container);
            player.ChatMessage(Lang(LangKeys.AchievementUnlocked, player.UserIDString));
        }
        
        //TODO: After revisiting this plugin, given that a year has passed, I realise that it needs a proper clean re-write, using Enum types for achievements and way less repetitive code, whole plugin is developed in a dirty way. Should come eventually.

        private void DisplayAchievements(BasePlayer player)
        {
            UiPosition containerPos = new UiPosition(0f, 0f, 1f, 1f);
            CuiElementContainer container = Ui.Container("#000000", 230f, 0f, 0f, containerPos, true, UiPanelName,"Overlay");

            UiPosition labelPos = new UiPosition(0.25f, 0.85f, 0.75f, 0.98f);
            Ui.TextOutline(container, "<b>ACHIEVEMENTS</b>", "#ffffff", 255f, "#000000", 255f, 42, labelPos, 0f, 0f);
            
            UiPosition closePos = new UiPosition(0.97f, 0.96f, 0.998f, 0.998f);
            Ui.Button(container, "#e81123", 0f, "<b>Ｘ</b>", "#e81123", 255f, 0f, 0f, 20, closePos, "AchievementSystemUI close");

            // Achievements list
            UiPosition panelPos = new UiPosition(0.01f, 0.75f, 0.09f, 0.85f);
            panelPos.ModifyXPad(0.01f);
            Ui.Panel(container, "#ffffff", 50, 0f, 0f, panelPos, true);
            
            Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Nightmare").ToString(), panelPos, "#ffffff", 255f, 0f, 0f);
            
            int minSleepers = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minSleepersKillsVIP : _config.AchievementsSet.minSleepersKills;

            GetPlayerData(player.userID);
            if (_data.PlayerDatas[player.userID].SleepersKilled < minSleepers)
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Locked").ToString(), panelPos, "#000000", 250f, 0f, 0f);
            }
            else if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("Nightmare"))
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Redeemed").ToString(), panelPos, "#ffffff", 200f, 0f, 0f);
            }
            else
            {
                Ui.Button(container, "#ffffff", 0f, "", "#ffffff", 0f, 0f, 0f, 16, panelPos, "AchievementSystemUI redeem_nightmare");
            }
            
            panelPos.ModifyXPad(0.01f);
            Ui.Panel(container, "#ffffff", 80, 0f, 0f, panelPos, true);
            
            Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "HeadHunter").ToString(), panelPos, "#8c8c8c", 255f, 0f, 0f);
            
            int minHeadShots = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minHeadshotsVIP : _config.AchievementsSet.minHeadshots;
            
            if (_data.PlayerDatas[player.userID].HeadShots < minHeadShots)
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Locked").ToString(), panelPos, "#000000", 250f, 0f, 0f);
            }
            else if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("HeadHunter"))
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Redeemed").ToString(), panelPos, "#ffffff", 200f, 0f, 0f);
            }
            else
            {
                Ui.Button(container, "#ffffff", 0f, "", "#ffffff", 0f, 0f, 0f, 16, panelPos, "AchievementSystemUI redeem_headhunter");
            }
            
            panelPos.ModifyXPad(0.01f);
            Ui.Panel(container, "#ffffff", 80, 0f, 0f, panelPos, true);
            
            Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "GoldenBarrel").ToString(), panelPos, "#ffffff", 255f, 0f, 0f);
            
            int minBarrels = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minBarrelsDestroyedVIP : _config.AchievementsSet.minBarrelsDestroyed;
            
            if (_data.PlayerDatas[player.userID].BarrelsDestroyed < minBarrels)
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Locked").ToString(), panelPos, "#000000", 250f, 0f, 0f);
            }
            else if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("GoldenBarrel"))
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Redeemed").ToString(), panelPos, "#ffffff", 200f, 0f, 0f);
            }
            else
            {
                Ui.Button(container, "#ffffff", 0f, "", "#ffffff", 0f, 0f, 0f, 16, panelPos, "AchievementSystemUI redeem_goldenbarrel");
            }
            
            panelPos.ModifyXPad(0.01f);
            Ui.Panel(container, "#ffffff", 80, 0f, 0f, panelPos, true);
            
            Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Explosion").ToString(), panelPos, "#ffffff", 255f, 0f, 0f);
            
            int minExplosives = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minExplosivesVIP : _config.AchievementsSet.minExplosives;
            
            if (_data.PlayerDatas[player.userID].ExplosivesThrown < minExplosives)
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Locked").ToString(), panelPos, "#000000", 250f, 0f, 0f);
            }
            else if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("BOOM"))
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Redeemed").ToString(), panelPos, "#ffffff", 200f, 0f, 0f);
            }
            else
            {
                Ui.Button(container, "#ffffff", 0f, "", "#ffffff", 0f, 0f, 0f, 16, panelPos, "AchievementSystemUI redeem_boom");
            }
            
            panelPos.ModifyXPad(0.01f);
            Ui.Panel(container, "#ffffff", 80, 0f, 0f, panelPos, true);
            
            Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Maniac").ToString(), panelPos, "#ffffff", 255f, 0f, 0f);
            
            int minBullets = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minBulletsVIP : _config.AchievementsSet.minBullets;
            
            if (_data.PlayerDatas[player.userID].BulletsFired < minBullets)
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Locked").ToString(), panelPos, "#000000", 250f, 0f, 0f);
            }
            else if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("Maniac"))
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Redeemed").ToString(), panelPos, "#ffffff", 200f, 0f, 0f);
            }
            else
            {
                Ui.Button(container, "#ffffff", 0f, "", "#ffffff", 0f, 0f, 0f, 16, panelPos, "AchievementSystemUI redeem_maniac");
            }
            
            panelPos.ModifyXPad(0.01f);
            Ui.Panel(container, "#ffffff", 80, 0f, 0f, panelPos, true);
            
            Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "RPG").ToString(), panelPos, "#ffffff", 255f, 0f, 0f);
            
            int minRockets = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minRocketsVIP : _config.AchievementsSet.minRockets;
            
            if (_data.PlayerDatas[player.userID].RocketsLaunched < minRockets)
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Locked").ToString(), panelPos, "#000000", 250f, 0f, 0f);
            }
            else if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("RPG"))
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Redeemed").ToString(), panelPos, "#ffffff", 200f, 0f, 0f);
            }
            else
            {
                Ui.Button(container, "#ffffff", 0f, "", "#ffffff", 0f, 0f, 0f, 16, panelPos, "AchievementSystemUI redeem_rpg");
            }
            
            panelPos.ModifyXPad(0.01f);
            Ui.Panel(container, "#ffffff", 80, 0f, 0f, panelPos, true);
            
            Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Cannabis").ToString(), panelPos, "#ffffff", 255f, 0f, 0f);
            
            int minHemp = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minHempsVIP : _config.AchievementsSet.minHemps;
            
            if (_data.PlayerDatas[player.userID].HempPickedUp < minHemp)
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Locked").ToString(), panelPos, "#000000", 250f, 0f, 0f);
            }
            else if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("Cannabis"))
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Redeemed").ToString(), panelPos, "#ffffff", 200f, 0f, 0f);
            }
            else
            {
                Ui.Button(container, "#ffffff", 0f, "", "#ffffff", 0f, 0f, 0f, 16, panelPos, "AchievementSystemUI redeem_cannabis");
            }
            
            panelPos.ModifyXPad(0.01f);
            Ui.Panel(container, "#ffffff", 80, 0f, 0f, panelPos, true);
            
            Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Pies").ToString(), panelPos, "#ffffff", 255f, 0f, 0f);
            
            int minPumpkin = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minPumpkinsVIP : _config.AchievementsSet.minPumpkins;

            if (_data.PlayerDatas[player.userID].PumpkinPickedUp < minPumpkin)
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Locked").ToString(), panelPos, "#000000", 250f, 0f, 0f);
            }
            else if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("Pies"))
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Redeemed").ToString(), panelPos, "#ffffff", 200f, 0f, 0f);
            }
            else
            {
                Ui.Button(container, "#ffffff", 0f, "", "#ffffff", 0f, 0f, 0f, 16, panelPos, "AchievementSystemUI redeem_pies");
            }
            
            panelPos.ModifyXPad(0.01f);
            Ui.Panel(container, "#ffffff", 80, 0f, 0f, panelPos, true);
            
            Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Popcorn").ToString(), panelPos, "#ffffff", 255f, 0f, 0f);
            
            int minCorn = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minCornsVIP : _config.AchievementsSet.minCorns;
            
            if (_data.PlayerDatas[player.userID].CornPickedUp < minCorn)
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Locked").ToString(), panelPos, "#000000", 250f, 0f, 0f);
            }
            else if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("Popcorn"))
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Redeemed").ToString(), panelPos, "#ffffff", 200f, 0f, 0f);
            }
            else
            {
                Ui.Button(container, "#ffffff", 0f, "", "#ffffff", 0f, 0f, 0f, 16, panelPos, "AchievementSystemUI redeem_popcorn");
            }
            
            panelPos.ModifyXPad(0.01f);
            panelPos.ModifyXPad(-0.82f);
            panelPos.ModifyYPad(-0.02f);
            panelPos.ModifyXPad(0.01f);
            Ui.Panel(container, "#ffffff", 80, 0f, 0f, panelPos, true);
            
            Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Metal").ToString(), panelPos, "#ffffff", 255f, 0f, 0f);
            
            int minMetal = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minMetalsVIP : _config.AchievementsSet.minMetals;
            
            if (_data.PlayerDatas[player.userID].MetalPickedUp < minMetal)
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Locked").ToString(), panelPos, "#000000", 250f, 0f, 0f);
            }
            else if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("Metal"))
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Redeemed").ToString(), panelPos, "#ffffff", 200f, 0f, 0f);
            }
            else
            {
                Ui.Button(container, "#ffffff", 0f, "", "#ffffff", 0f, 0f, 0f, 16, panelPos, "AchievementSystemUI redeem_metal");
            }
            
            panelPos.ModifyXPad(0.01f);
            Ui.Panel(container, "#ffffff", 80, 0f, 0f, panelPos, true);
            
            Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Raider").ToString(), panelPos, "#ffffff", 255f, 0f, 0f);
            
            int minSulfur = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minSulfursVIP : _config.AchievementsSet.minSulfurs;
            
            if (_data.PlayerDatas[player.userID].SulfurPickedUp < minSulfur)
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Locked").ToString(), panelPos, "#000000", 250f, 0f, 0f);
            }
            else if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("Raider"))
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Redeemed").ToString(), panelPos, "#ffffff", 200f, 0f, 0f);
            }
            else
            {
                Ui.Button(container, "#ffffff", 0f, "", "#ffffff", 0f, 0f, 0f, 16, panelPos, "AchievementSystemUI redeem_raider");
            }
            
            panelPos.ModifyXPad(0.01f);
            Ui.Panel(container, "#ffffff", 80, 0f, 0f, panelPos, true);
            
            Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "WoodLurker").ToString(), panelPos, "#ffffff", 255f, 0f, 0f);
            
            int minWood = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minWoodVIP : _config.AchievementsSet.minWood;
            
            if (_data.PlayerDatas[player.userID].WoodPickedUp < minWood)
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Locked").ToString(), panelPos, "#000000", 250f, 0f, 0f);
            }
            else if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("WoodLurker"))
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Redeemed").ToString(), panelPos, "#ffffff", 200f, 0f, 0f);
            }
            else
            {
                Ui.Button(container, "#ffffff", 0f, "", "#ffffff", 0f, 0f, 0f, 16, panelPos, "AchievementSystemUI redeem_woodlurker");
            }
            
            panelPos.ModifyXPad(0.01f);
            Ui.Panel(container, "#ffffff", 80, 0f, 0f, panelPos, true);
            
            Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Killer").ToString(), panelPos, "#ffffff", 255f, 0f, 0f);

            int minKills = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minPVPKillsVIP : _config.AchievementsSet.minPVPKills;
            
            if (_data.PlayerDatas[player.userID].PVPKills < minKills)
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Locked").ToString(), panelPos, "#000000", 250f, 0f, 0f);
            }
            else if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("Killer"))
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Redeemed").ToString(), panelPos, "#ffffff", 200f, 0f, 0f);
            }
            else
            {
                Ui.Button(container, "#ffffff", 0f, "", "#ffffff", 0f, 0f, 0f, 16, panelPos, "AchievementSystemUI redeem_killer");
            }
            
            panelPos.ModifyXPad(0.01f);
            Ui.Panel(container, "#ffffff", 80, 0f, 0f, panelPos, true);
            
            Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "StoneAge").ToString(), panelPos, "#ffffff", 255f, 0f, 0f);
            
            int minStone = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minStonesVIP : _config.AchievementsSet.minStones;
            
            if (_data.PlayerDatas[player.userID].StonePickedUp < minStone)
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Locked").ToString(), panelPos, "#000000", 250f, 0f, 0f);
            }
            else if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("StoneAge"))
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Redeemed").ToString(), panelPos, "#ffffff", 200f, 0f, 0f);
            }
            else
            {
                Ui.Button(container, "#ffffff", 0f, "", "#ffffff", 0f, 0f, 0f, 16, panelPos, "AchievementSystemUI redeem_stoneage");
            }
            
            panelPos.ModifyXPad(0.01f);
            Ui.Panel(container, "#ffffff", 80, 0f, 0f, panelPos, true);
            
            Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "AxeMan").ToString(), panelPos, "#ffffff", 255f, 0f, 0f);
            
            int minWoodGathered = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minWoodGatheredVIP : _config.AchievementsSet.minWoodGathered;
            
            if (_data.PlayerDatas[player.userID].WoodGathered < minWoodGathered)
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Locked").ToString(), panelPos, "#000000", 250f, 0f, 0f);
            }
            else if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("AxeMan"))
            { 
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Redeemed").ToString(), panelPos, "#ffffff", 200f, 0f, 0f);
            }
            else
            {
                Ui.Button(container, "#ffffff", 0f, "", "#ffffff", 0f, 0f, 0f, 16, panelPos, "AchievementSystemUI redeem_axeman");
            }

            panelPos.ModifyXPad(0.01f);
            Ui.Panel(container, "#ffffff", 80, 0f, 0f, panelPos, true);
            
            Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Miner").ToString(), panelPos, "#ffffff", 255f, 0f, 0f);
            
            int minStonesGathered = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minStonesGatheredVIP : _config.AchievementsSet.minStonesGathered;
            
            if (_data.PlayerDatas[player.userID].StoneGathered < minStonesGathered)
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Locked").ToString(), panelPos, "#000000", 250f, 0f, 0f);
            }
            else if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("Miner"))
            {
               Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Redeemed").ToString(), panelPos, "#ffffff", 200f, 0f, 0f);
            }
            else
            {
                Ui.Button(container, "#ffffff", 0f, "", "#ffffff", 0f, 0f, 0f, 16, panelPos, "AchievementSystemUI redeem_miner");
            }
            
            panelPos.ModifyXPad(0.01f);
            Ui.Panel(container, "#ffffff", 80, 0f, 0f, panelPos, true);
            
            Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "MetalGear").ToString(), panelPos, "#ffffff", 255f, 0f, 0f);
            
            int minMetalGathered = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minMetalsGatheredVIP : _config.AchievementsSet.minMetalsGathered;
            
            if (_data.PlayerDatas[player.userID].MetalGathered < minMetalGathered)
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Locked").ToString(), panelPos, "#000000", 250f, 0f, 0f);
            }
            else if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("MetalGear"))
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Redeemed").ToString(), panelPos, "#ffffff", 200f, 0f, 0f);
            }
            else
            {
                Ui.Button(container, "#ffffff", 0f, "", "#ffffff", 0f, 0f, 0f, 16, panelPos, "AchievementSystemUI redeem_metalgear");
            }

            panelPos.ModifyXPad(0.01f);
            Ui.Panel(container, "#ffffff", 80, 0f, 0f, panelPos, true);
            
            Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "EvilMiner").ToString(), panelPos, "#ffffff", 255f, 0f, 0f);
            
            int minSulfurGathered = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minSulfursGatheredVIP : _config.AchievementsSet.minSulfursGathered;
            
            if (_data.PlayerDatas[player.userID].SulfurGathered < minSulfurGathered)
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Locked").ToString(), panelPos, "#000000", 250f, 0f, 0f);
            }
            else if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("EvilMiner"))
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Redeemed").ToString(), panelPos, "#ffffff", 200f, 0f, 0f);
            }
            else
            {
                Ui.Button(container, "#ffffff", 0f, "", "#ffffff", 0f, 0f, 0f, 16, panelPos, "AchievementSystemUI redeem_evilminer");
            }

            panelPos.ModifyXPad(0.01f);
            panelPos.ModifyXPad(-0.82f);
            panelPos.ModifyYPad(-0.02f);
            panelPos.ModifyXPad(0.01f);
            Ui.Panel(container, "#ffffff", 80, 0f, 0f, panelPos, true);
            
            Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Outsmarted").ToString(), panelPos, "#ffffff", 255f, 0f, 0f);
            
            int minScientistsKills = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minScientistsKillsVIP : _config.AchievementsSet.minScientistsKills;
            
            if (_data.PlayerDatas[player.userID].ScientistsKilled < minScientistsKills)
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Locked").ToString(), panelPos, "#000000", 250f, 0f, 0f);
            }
            else if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("Outsmarted"))
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Redeemed").ToString(), panelPos, "#ffffff", 200f, 0f, 0f);
            }
            else
            {
                Ui.Button(container, "#ffffff", 0f, "", "#ffffff", 0f, 0f, 0f, 16, panelPos, "AchievementSystemUI redeem_outsmarted");
            }
            
            panelPos.ModifyXPad(0.01f);
            Ui.Panel(container, "#ffffff", 80, 0f, 0f, panelPos, true);
            
            Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Juggernaut").ToString(), panelPos, "#ffffff", 255f, 0f, 0f);
            
            int minHeavies = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minHeaviesKillsVIP : _config.AchievementsSet.minHeaviesKills;
            
            if (_data.PlayerDatas[player.userID].HeaviesKilled < minHeavies)
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Locked").ToString(), panelPos, "#000000", 250f, 0f, 0f);
            }
            else if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("Juggernaut"))
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Redeemed").ToString(), panelPos, "#ffffff", 200f, 0f, 0f);
            }
            else
            {
                Ui.Button(container, "#ffffff", 0f, "", "#ffffff", 0f, 0f, 0f, 16, panelPos, "AchievementSystemUI redeem_juggernaut");
            }
            
            panelPos.ModifyXPad(0.01f);
            Ui.Panel(container, "#ffffff", 80, 0f, 0f, panelPos, true);
            
            Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Piggy").ToString(), panelPos, "#ffffff", 255f, 0f, 0f);
            
            int minBoars = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minBoarsKillsVIP : _config.AchievementsSet.minBoarsKills;
            
            if (_data.PlayerDatas[player.userID].BoarsKilled < minBoars)
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Locked").ToString(), panelPos, "#000000", 250f, 0f, 0f);
            }
            else if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("Piggy"))
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Redeemed").ToString(), panelPos, "#ffffff", 200f, 0f, 0f);
            }
            else
            {
                Ui.Button(container, "#ffffff", 0f, "", "#ffffff", 0f, 0f, 0f, 16, panelPos, "AchievementSystemUI redeem_piggy");
            }
            
            panelPos.ModifyXPad(0.01f);
            Ui.Panel(container, "#ffffff", 80, 0f, 0f, panelPos, true);
            
            Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Pookie").ToString(), panelPos, "#ffffff", 255f, 0f, 0f);
            
            int minBears = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minBearsKillsVIP : _config.AchievementsSet.minBearsKills;
            
            if (_data.PlayerDatas[player.userID].BearsKilled < minBears)
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Locked").ToString(), panelPos, "#000000", 250f, 0f, 0f);
            }
            else if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("Pookie"))
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Redeemed").ToString(), panelPos, "#ffffff", 200f, 0f, 0f);
            }
            else
            {
                Ui.Button(container, "#ffffff", 0f, "", "#ffffff", 0f, 0f, 0f, 16, panelPos, "AchievementSystemUI redeem_pookie");
            }
            
            panelPos.ModifyXPad(0.01f);
            Ui.Panel(container, "#ffffff", 80, 0f, 0f, panelPos, true);
            
            Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "ChickenDinner").ToString(), panelPos, "#ffffff", 255f, 0f, 0f);
            
            int minChickens = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minChickensKillsVIP : _config.AchievementsSet.minChickensKills;
            
            if (_data.PlayerDatas[player.userID].ChickensKilled < minChickens)
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Locked").ToString(), panelPos, "#000000", 250f, 0f, 0f);
            }
            else if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("ChickenDinner"))
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Redeemed").ToString(), panelPos, "#ffffff", 200f, 0f, 0f);
            }
            else
            {
                Ui.Button(container, "#ffffff", 0f, "", "#ffffff", 0f, 0f, 0f, 16, panelPos, "AchievementSystemUI redeem_chickendinner");
            }
            
            panelPos.ModifyXPad(0.01f);
            Ui.Panel(container, "#ffffff", 80, 0f, 0f, panelPos, true);
            
            Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Bambi").ToString(), panelPos, "#ffffff", 255f, 0f, 0f);
            
            int minStags = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minStagsKillsVIP : _config.AchievementsSet.minStagsKills;
            
            if (_data.PlayerDatas[player.userID].StagsKilled < minStags)
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Locked").ToString(), panelPos, "#000000", 250f, 0f, 0f);
            }
            else if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("Bambi"))
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Redeemed").ToString(), panelPos, "#ffffff", 200f, 0f, 0f);
            }
            else
            {
                Ui.Button(container, "#ffffff", 0f, "", "#ffffff", 0f, 0f, 0f, 16, panelPos, "AchievementSystemUI redeem_bambi");
            }
            
            panelPos.ModifyXPad(0.01f);
            Ui.Panel(container, "#ffffff", 80, 0f, 0f, panelPos, true);
            
            Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "LoneWolf").ToString(), panelPos, "#ffffff", 255f, 0f, 0f);
            
            int minWolves = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minWolvesKillsVIP : _config.AchievementsSet.minWolvesKills;
            
            if (_data.PlayerDatas[player.userID].WolvesKilled < minWolves)
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Locked").ToString(), panelPos, "#000000", 250f, 0f, 0f);
            }
            else if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("LoneWolf"))
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Redeemed").ToString(), panelPos, "#ffffff", 200f, 0f, 0f);
            }
            else
            {
                Ui.Button(container, "#ffffff", 0f, "", "#ffffff", 0f, 0f, 0f, 16, panelPos, "AchievementSystemUI redeem_lonewolf");
            }
            
            panelPos.ModifyXPad(0.01f);
            Ui.Panel(container, "#ffffff", 80, 0f, 0f, panelPos, true);
            
            Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "TankHunter").ToString(), panelPos, "#ffffff", 255f, 0f, 0f);
            
            int minBradleys = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minBradleysKillsVIP : _config.AchievementsSet.minBradleysKills;
            
            if (_data.PlayerDatas[player.userID].BradleyDestroyed < minBradleys)
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Locked").ToString(), panelPos, "#000000", 250f, 0f, 0f);
            }
            else if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("TankHunter"))
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Redeemed").ToString(), panelPos, "#ffffff", 200f, 0f, 0f);
            }
            else
            {
                Ui.Button(container, "#ffffff", 0f, "", "#ffffff", 0f, 0f, 0f, 16, panelPos, "AchievementSystemUI redeem_tankhunter");
            }

            panelPos.ModifyXPad(0.01f);
            Ui.Panel(container, "#ffffff", 80, 0f, 0f, panelPos, true);

            Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "ChopperHunter").ToString(), panelPos, "#ffffff", 255f, 0f, 0f);
            
            int minChoppers = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minChoppersKillsVIP : _config.AchievementsSet.minChoppersKills;
            
            if (_data.PlayerDatas[player.userID].HelicopterDestroyed < minChoppers)
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Locked").ToString(), panelPos, "#000000", 250f, 0f, 0f);
            }
            else if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("ChopperHunter"))
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Redeemed").ToString(), panelPos, "#ffffff", 200f, 0f, 0f);
            }
            else
            {
                Ui.Button(container, "#ffffff", 0f, "", "#ffffff", 0f, 0f, 0f, 16, panelPos, "AchievementSystemUI redeem_chopperhunter");
            }
            
            panelPos.ModifyXPad(0.01f);
            panelPos.ModifyXPad(-0.82f);
            panelPos.ModifyYPad(-0.02f);
            panelPos.ModifyXPad(0.01f);
            Ui.Panel(container, "#ffffff", 80, 0f, 0f, panelPos, true);
            
            Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Skinner").ToString(), panelPos, "#ffffff", 255f, 0f, 0f);
            
            int minCloth = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minClothSkinnedVIP : _config.AchievementsSet.minClothSkinned;
            
            if (_data.PlayerDatas[player.userID].ClothSkinned < minCloth)
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Locked").ToString(), panelPos, "#000000", 250f, 0f, 0f);
            }
            else if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("Skinner"))
            {
                Ui.Image(container, _plugin.ImageLibrary.Call("GetImage", "Redeemed").ToString(), panelPos, "#ffffff", 200f, 0f, 0f);
            }
            else
            {
                Ui.Button(container, "#ffffff", 0f, "", "#ffffff", 0f, 0f, 0f, 16, panelPos, "AchievementSystemUI redeem_skinner");
            }
            
            panelPos.ModifyXPad(0.01f);
            Ui.Panel(container, "#ffffff", 80, 0f, 0f, panelPos, true);
            
            
            
            panelPos.ModifyXPad(0.01f);
            Ui.Panel(container, "#ffffff", 80, 0f, 0f, panelPos, true);
            
            
            
            panelPos.ModifyXPad(0.01f);
            Ui.Panel(container, "#ffffff", 80, 0f, 0f, panelPos, true);
            
            
            
            panelPos.ModifyXPad(0.01f);
            Ui.Panel(container, "#ffffff", 80, 0f, 0f, panelPos, true);
            
            
            
            panelPos.ModifyXPad(0.01f);
            Ui.Panel(container, "#ffffff", 80, 0f, 0f, panelPos, true);
            
            
            
            panelPos.ModifyXPad(0.01f);
            Ui.Panel(container, "#ffffff", 80, 0f, 0f, panelPos, true);
            
            
            
            panelPos.ModifyXPad(0.01f);
            Ui.Panel(container, "#ffffff", 80, 0f, 0f, panelPos, true);
            
            

            panelPos.ModifyXPad(0.01f);
            Ui.Panel(container, "#ffffff", 80, 0f, 0f, panelPos, true);
            
            
            
            panelPos.ModifyXPad(0.01f);
            panelPos.ModifyXPad(-0.82f);
            panelPos.ModifyYPad(-0.02f);
            panelPos.ModifyXPad(0.01f);
            Ui.Panel(container, "#ffffff", 80, 0f, 0f, panelPos, true);
            
            
            
            panelPos.ModifyXPad(0.01f);
            Ui.Panel(container, "#ffffff", 80, 0f, 0f, panelPos, true);
            
            
            
            panelPos.ModifyXPad(0.01f);
            Ui.Panel(container, "#ffffff", 80, 0f, 0f, panelPos, true);
            
            
            
            panelPos.ModifyXPad(0.01f);
            Ui.Panel(container, "#ffffff", 80, 0f, 0f, panelPos, true);
            
            
            
            panelPos.ModifyXPad(0.01f);
            Ui.Panel(container, "#ffffff", 80, 0f, 0f, panelPos, true);
            
            
            
            panelPos.ModifyXPad(0.01f);
            Ui.Panel(container, "#ffffff", 80, 0f, 0f, panelPos, true);
            
            
            
            panelPos.ModifyXPad(0.01f);
            Ui.Panel(container, "#ffffff", 80, 0f, 0f, panelPos, true);
            
            
            
            panelPos.ModifyXPad(0.01f);
            Ui.Panel(container, "#ffffff", 80, 0f, 0f, panelPos, true);

            
            
            panelPos.ModifyXPad(0.01f);
            Ui.Panel(container, "#ffffff", 80, 0f, 0f, panelPos, true);
            
            

            CuiHelper.DestroyUi(player, UiPanelName);
            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region UI Commands

        [ConsoleCommand("AchievementSystemUI")]
        private void UiCommandHandler(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            if (player != null && args.HasArgs(1))
            {
                switch (args.Args[0].ToLower())
                {
                    case "close":
                    {
                        CuiHelper.DestroyUi(player, UiPanelName);
                        break;
                    }
                    case "redeem_killer":
                    {
                        if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("Killer"))
                        {
                            return;
                        }
                        
                        int minKills = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minPVPKillsVIP : _config.AchievementsSet.minPVPKills;

                        if (_data.PlayerDatas[player.userID].PVPKills >= minKills)
                        {
                            Dictionary<string, int> items = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsPVPKillsVIP : _config.RewardSet.rewardsPVPKills;

                            if (_config.AchievementsImg.enableItems)
                            {
                                foreach (KeyValuePair<string, int> item in items)
                                {
                                    Item give = ItemManager.CreateByName(item.Key, item.Value);

                                    if (give == null)
                                    {
                                        continue;
                                    }
                                
                                    player.GiveItem(give);
                                }
                            }

                            _data.PlayerDatas[player.userID].AchievementsRedeemed.Add("Killer");
                            SendEffectTo(RewardsFX, player);
                            DisplayAchievements(player);
                            if (_config.AchievementsImg.enableRP)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsPVPKillsRPVIP : _config.RewardSet.rewardsPVPKillsRP;
                                AddPoints(player, rp);
                            }
                            
                            if (_config.AchievementsImg.enableEconomics)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsPVPKillsEconomicsVIP : _config.RewardSet.rewardsPVPKillsEconomics;
                                AddEconomics(player, rp);
                            }
                        }

                        break;
                    }
                    case "redeem_nightmare":
                    {
                        if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("Nightmare"))
                        {
                            return;
                        }
                        
                        int minSleepers = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minSleepersKillsVIP : _config.AchievementsSet.minSleepersKills;

                        if (_data.PlayerDatas[player.userID].SleepersKilled >= minSleepers)
                        {
                            Dictionary<string, int> items = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsSleepersKillsVIP : _config.RewardSet.rewardsSleepersKills;

                            if (_config.AchievementsImg.enableItems)
                            {
                                foreach (KeyValuePair<string, int> item in items)
                                {
                                    Item give = ItemManager.CreateByName(item.Key, item.Value);

                                    if (give == null)
                                    {
                                        continue;
                                    }
                                
                                    player.GiveItem(give);
                                }
                            }

                            _data.PlayerDatas[player.userID].AchievementsRedeemed.Add("Nightmare");
                            SendEffectTo(RewardsFX, player);
                            DisplayAchievements(player);
                            if (_config.AchievementsImg.enableRP)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsSleepersKillsRPVIP : _config.RewardSet.rewardsSleepersKillsRP;
                                AddPoints(player, rp);
                            }
                            
                            if (_config.AchievementsImg.enableEconomics)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsSleepersKillsEconomicsVIP : _config.RewardSet.rewardsSleepersKillsEconomics;
                                AddEconomics(player, rp);
                            }
                        }
                        
                        break;
                    }
                    case "redeem_headhunter":
                    {
                        if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("HeadHunter"))
                        {
                            return;
                        }
                        
                        int minHs = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minHeadshotsVIP : _config.AchievementsSet.minHeadshots;

                        if (_data.PlayerDatas[player.userID].HeadShots >= minHs)
                        {
                            Dictionary<string, int> items = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsHeadshotsVIP : _config.RewardSet.rewardsHeadshots;

                            if (_config.AchievementsImg.enableItems)
                            {
                                foreach (KeyValuePair<string, int> item in items)
                                {
                                    Item give = ItemManager.CreateByName(item.Key, item.Value);

                                    if (give == null)
                                    {
                                        continue;
                                    }
                                
                                    player.GiveItem(give);
                                }
                            }

                            _data.PlayerDatas[player.userID].AchievementsRedeemed.Add("HeadHunter");
                            SendEffectTo(RewardsFX, player);
                            DisplayAchievements(player);
                            if (_config.AchievementsImg.enableRP)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsHeadshotsRPVIP : _config.RewardSet.rewardsHeadshotsRP;
                                AddPoints(player, rp);
                            }
                            
                            if (_config.AchievementsImg.enableEconomics)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsHeadshotsEconomicsVIP : _config.RewardSet.rewardsHeadshotsEconomics;
                                AddEconomics(player, rp);
                            }
                        }

                        break;
                    }
                    case "redeem_woodlurker":
                    {
                        if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("WoodLurker"))
                        {
                            return;
                        }
                        
                        int minWood = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minWoodVIP : _config.AchievementsSet.minWood;

                        if (_data.PlayerDatas[player.userID].WoodPickedUp >= minWood)
                        {
                            Dictionary<string, int> items = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsWoodVIP : _config.RewardSet.rewardsWood;

                            if (_config.AchievementsImg.enableItems)
                            {
                                foreach (KeyValuePair<string, int> item in items)
                                {
                                    Item give = ItemManager.CreateByName(item.Key, item.Value);

                                    if (give == null)
                                    {
                                        continue;
                                    }
                                
                                    player.GiveItem(give);
                                }
                            }

                            _data.PlayerDatas[player.userID].AchievementsRedeemed.Add("WoodLurker");
                            SendEffectTo(RewardsFX, player);
                            DisplayAchievements(player);
                            if (_config.AchievementsImg.enableRP)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsWoodRPVIP : _config.RewardSet.rewardsWoodRP;
                                AddPoints(player, rp);
                            }
                            
                            if (_config.AchievementsImg.enableEconomics)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsWoodEconomicsVIP : _config.RewardSet.rewardsWoodEconomics;
                                AddEconomics(player, rp);
                            }
                        }

                        break;
                    }
                    case "redeem_pies":
                    {
                        if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("Pies"))
                        {
                            return;
                        }
                        
                        int minPies = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minPumpkinsVIP : _config.AchievementsSet.minPumpkins;

                        if (_data.PlayerDatas[player.userID].PumpkinPickedUp >= minPies)
                        {
                            Dictionary<string, int> items = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsPumpkinsVIP : _config.RewardSet.rewardsPumpkins;

                            if (_config.AchievementsImg.enableItems)
                            {
                                foreach (KeyValuePair<string, int> item in items)
                                {
                                    Item give = ItemManager.CreateByName(item.Key, item.Value);

                                    if (give == null)
                                    {
                                        continue;
                                    }
                                
                                    player.GiveItem(give);
                                }
                            }

                            _data.PlayerDatas[player.userID].AchievementsRedeemed.Add("Pies");
                            SendEffectTo(RewardsFX, player);
                            DisplayAchievements(player);
                            if (_config.AchievementsImg.enableRP)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsPumpkinsRPVIP : _config.RewardSet.rewardsPumpkinsRP;
                                AddPoints(player, rp);
                            }
                            
                            if (_config.AchievementsImg.enableEconomics)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsPumpkinsEconomicsVIP : _config.RewardSet.rewardsPumpkinsEconomics;
                                AddEconomics(player, rp);
                            }
                        }

                        break;
                    }
                    case "redeem_popcorn":
                    {
                        if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("Popcorn"))
                        {
                            return;
                        }
                        
                        int minPopcorn = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minCornsVIP : _config.AchievementsSet.minCorns;

                        if (_data.PlayerDatas[player.userID].CornPickedUp >= minPopcorn)
                        {
                            Dictionary<string, int> items = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsCornsVIP : _config.RewardSet.rewardsCorns;

                            if (_config.AchievementsImg.enableItems)
                            {
                                foreach (KeyValuePair<string, int> item in items)
                                {
                                    Item give = ItemManager.CreateByName(item.Key, item.Value);

                                    if (give == null)
                                    {
                                        continue;
                                    }
                                
                                    player.GiveItem(give);
                                }
                            }

                            _data.PlayerDatas[player.userID].AchievementsRedeemed.Add("Popcorn");
                            SendEffectTo(RewardsFX, player);
                            DisplayAchievements(player);
                            if (_config.AchievementsImg.enableRP)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsCornsRPVIP : _config.RewardSet.rewardsCornsRP;
                                AddPoints(player, rp);
                            }
                            
                            if (_config.AchievementsImg.enableEconomics)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsCornsEconomicsVIP : _config.RewardSet.rewardsCornsEconomics;
                                AddEconomics(player, rp);
                            }
                        }

                        break;
                    }
                    case "redeem_cannabis":
                    {
                        if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("Cannabis"))
                        {
                            return;
                        }
                        
                        int minHemp = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minHempsVIP : _config.AchievementsSet.minHemps;

                        if (_data.PlayerDatas[player.userID].HempPickedUp >= minHemp)
                        {
                            Dictionary<string, int> items = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsHempsVIP : _config.RewardSet.rewardsHemps;

                            if (_config.AchievementsImg.enableItems)
                            {
                                foreach (KeyValuePair<string, int> item in items)
                                {
                                    Item give = ItemManager.CreateByName(item.Key, item.Value);

                                    if (give == null)
                                    {
                                        continue;
                                    }
                                
                                    player.GiveItem(give);
                                } 
                            }

                            _data.PlayerDatas[player.userID].AchievementsRedeemed.Add("Cannabis");
                            SendEffectTo(RewardsFX, player);
                            DisplayAchievements(player);
                            if (_config.AchievementsImg.enableRP)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsHempsRPVIP : _config.RewardSet.rewardsHempsRP;
                                AddPoints(player, rp);
                            }
                            
                            if (_config.AchievementsImg.enableEconomics)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsHempsEconomicsVIP : _config.RewardSet.rewardsHempsEconomics;
                                AddEconomics(player, rp);
                            }
                        }

                        break;
                    }
                    case "redeem_maniac":
                    {
                        if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("Maniac"))
                        {
                            return;
                        }
                        
                        int minBullets = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minBulletsVIP : _config.AchievementsSet.minBullets;

                        if (_data.PlayerDatas[player.userID].BulletsFired >= minBullets)
                        {
                            Dictionary<string, int> items = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsBulletsVIP : _config.RewardSet.rewardsBullets;

                            if (_config.AchievementsImg.enableItems)
                            {
                                foreach (KeyValuePair<string, int> item in items)
                                {
                                    Item give = ItemManager.CreateByName(item.Key, item.Value);

                                    if (give == null)
                                    {
                                        continue;
                                    }

                                    player.GiveItem(give);
                                }
                            }

                            _data.PlayerDatas[player.userID].AchievementsRedeemed.Add("Maniac");
                            SendEffectTo(RewardsFX, player);
                            DisplayAchievements(player);
                            if (_config.AchievementsImg.enableRP)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsBulletsRPVIP : _config.RewardSet.rewardsBulletsRP;
                                AddPoints(player, rp);
                            }
                            
                            if (_config.AchievementsImg.enableEconomics)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsBulletsEconomicsVIP : _config.RewardSet.rewardsBulletsEconomics;
                                AddEconomics(player, rp);
                            }
                        }

                        break;
                    }
                    case "redeem_stoneage":
                    {
                        if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("StoneAge"))
                        {
                            return;
                        }
                        
                        int minStone = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minStonesVIP : _config.AchievementsSet.minStones;

                        if (_data.PlayerDatas[player.userID].StonePickedUp >= minStone)
                        {
                            Dictionary<string, int> items = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsStonesVIP : _config.RewardSet.rewardsStones;

                            if (_config.AchievementsImg.enableItems)
                            {
                                foreach (KeyValuePair<string, int> item in items)
                                {
                                    Item give = ItemManager.CreateByName(item.Key, item.Value);

                                    if (give == null)
                                    {
                                        continue;
                                    }

                                    player.GiveItem(give);
                                } 
                            }

                            _data.PlayerDatas[player.userID].AchievementsRedeemed.Add("StoneAge");
                            SendEffectTo(RewardsFX, player);
                            DisplayAchievements(player);
                            if (_config.AchievementsImg.enableRP)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsStonesRPVIP : _config.RewardSet.rewardsStonesRP;
                                AddPoints(player, rp);
                            }
                            
                            if (_config.AchievementsImg.enableEconomics)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsStonesEconomicsVIP : _config.RewardSet.rewardsStonesEconomics;
                                AddEconomics(player, rp);
                            }
                        }

                        break;
                    }
                    case "redeem_metal":
                    {
                        if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("Metal"))
                        {
                            return;
                        }
                        
                        int minMetal = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minMetalsVIP : _config.AchievementsSet.minMetals;

                        if (_data.PlayerDatas[player.userID].MetalPickedUp >= minMetal)
                        {
                            Dictionary<string, int> items = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsMetalsVIP : _config.RewardSet.rewardsMetals;

                            if (_config.AchievementsImg.enableItems)
                            {
                                foreach (KeyValuePair<string, int> item in items)
                                {
                                    Item give = ItemManager.CreateByName(item.Key, item.Value);

                                    if (give == null)
                                    {
                                        continue;
                                    }
                                
                                    player.GiveItem(give);
                                }
                            }

                            _data.PlayerDatas[player.userID].AchievementsRedeemed.Add("Metal");
                            SendEffectTo(RewardsFX, player);
                            DisplayAchievements(player);
                            if (_config.AchievementsImg.enableRP)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsMetalsRPVIP : _config.RewardSet.rewardsMetalsRP;
                                AddPoints(player, rp);
                            }
                            
                            if (_config.AchievementsImg.enableEconomics)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsMetalsEconomicsVIP : _config.RewardSet.rewardsMetalsEconomics;
                                AddEconomics(player, rp);
                            }
                        }

                        break;
                    }
                    case "redeem_raider":
                    {
                        if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("Raider"))
                        {
                            return;
                        }
                        
                        int minSulfur = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minSulfursVIP : _config.AchievementsSet.minSulfurs;

                        if (_data.PlayerDatas[player.userID].SulfurPickedUp >= minSulfur)
                        {
                            Dictionary<string, int> items = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsSulfursVIP : _config.RewardSet.rewardsSulfurs;

                            if (_config.AchievementsImg.enableItems)
                            {
                                foreach (KeyValuePair<string, int> item in items)
                                {
                                    Item give = ItemManager.CreateByName(item.Key, item.Value);

                                    if (give == null)
                                    {
                                        continue;
                                    }

                                    player.GiveItem(give);
                                }
                            }

                            _data.PlayerDatas[player.userID].AchievementsRedeemed.Add("Raider");
                            SendEffectTo(RewardsFX, player);
                            DisplayAchievements(player);
                            if (_config.AchievementsImg.enableRP)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsSulfursRPVIP : _config.RewardSet.rewardsSulfursRP;
                                AddPoints(player, rp);
                            }
                            
                            if (_config.AchievementsImg.enableEconomics)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsSulfursEconomicsVIP : _config.RewardSet.rewardsSulfursEconomics;
                                AddEconomics(player, rp);
                            }
                        }

                        break;
                    }
                    case "redeem_goldenbarrel":
                    {
                        if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("GoldenBarrel"))
                        {
                            return;
                        }
                        
                        int minBarrel = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minBarrelsDestroyedVIP : _config.AchievementsSet.minBarrelsDestroyed;

                        if (_data.PlayerDatas[player.userID].BarrelsDestroyed >= minBarrel)
                        {
                            Dictionary<string, int> items = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsBarrelsVIP : _config.RewardSet.rewardsBarrels;

                            if (_config.AchievementsImg.enableItems)
                            {
                                foreach (KeyValuePair<string, int> item in items)
                                {
                                    Item give = ItemManager.CreateByName(item.Key, item.Value);

                                    if (give == null)
                                    {
                                        continue;
                                    }

                                    player.GiveItem(give);
                                } 
                            }

                            _data.PlayerDatas[player.userID].AchievementsRedeemed.Add("GoldenBarrel");
                            SendEffectTo(RewardsFX, player);
                            DisplayAchievements(player);
                            if (_config.AchievementsImg.enableRP)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsBarrelsRPVIP : _config.RewardSet.rewardsBarrelsRP;
                                AddPoints(player, rp);
                            }
                            
                            if (_config.AchievementsImg.enableEconomics)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsBarrelsEconomicsVIP : _config.RewardSet.rewardsBarrelsEconomics;
                                AddEconomics(player, rp);
                            }
                        }

                        break;
                    }
                    case "redeem_rpg":
                    {
                        if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("RPG"))
                        {
                            return;
                        }
                        
                        int minRockets = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minRocketsVIP : _config.AchievementsSet.minRockets;

                        if (_data.PlayerDatas[player.userID].RocketsLaunched >= minRockets)
                        {
                            Dictionary<string, int> items = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsRocketsVIP : _config.RewardSet.rewardsRockets;

                            if (_config.AchievementsImg.enableItems)
                            {
                                foreach (KeyValuePair<string, int> item in items)
                                {
                                    Item give = ItemManager.CreateByName(item.Key, item.Value);

                                    if (give == null)
                                    {
                                        continue;
                                    }

                                    player.GiveItem(give);
                                }
                            }

                            _data.PlayerDatas[player.userID].AchievementsRedeemed.Add("RPG");
                            SendEffectTo(RewardsFX, player);
                            DisplayAchievements(player);
                            if (_config.AchievementsImg.enableRP)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsRocketsRPVIP : _config.RewardSet.rewardsRocketsRP;
                                AddPoints(player, rp);
                            }
                            
                            if (_config.AchievementsImg.enableEconomics)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsRocketsEconomicsVIP : _config.RewardSet.rewardsRocketsEconomics;
                                AddEconomics(player, rp);
                            }
                        }

                        break;
                    }
                    case "redeem_boom":
                    {
                        if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("BOOM"))
                        {
                            return;
                        }
                        
                        int minExplosives = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minExplosivesVIP : _config.AchievementsSet.minExplosives;

                        if (_data.PlayerDatas[player.userID].ExplosivesThrown >= minExplosives)
                        {
                            Dictionary<string, int> items = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsExplosivesVIP : _config.RewardSet.rewardsExplosives;

                            if (_config.AchievementsImg.enableItems)
                            {
                                foreach (KeyValuePair<string, int> item in items)
                                {
                                    Item give = ItemManager.CreateByName(item.Key, item.Value);

                                    if (give == null)
                                    {
                                        continue;
                                    }
                                
                                    player.GiveItem(give);
                                }
                            }

                            _data.PlayerDatas[player.userID].AchievementsRedeemed.Add("BOOM");
                            SendEffectTo(RewardsFX, player);
                            DisplayAchievements(player);
                            if (_config.AchievementsImg.enableRP)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsExplosivesRPVIP : _config.RewardSet.rewardsExplosivesRP;
                                AddPoints(player, rp);
                            }
                            
                            if (_config.AchievementsImg.enableEconomics)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsExplosivesEconomicsVIP : _config.RewardSet.rewardsExplosivesEconomics;
                                AddEconomics(player, rp);
                            }
                        }

                        break;
                    }
                    case "redeem_axeman":
                    {
                        if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("AxeMan"))
                        {
                            return;
                        }
                        
                        int minWood = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minWoodGatheredVIP : _config.AchievementsSet.minWoodGathered;

                        if (_data.PlayerDatas[player.userID].WoodGathered >= minWood)
                        {
                            Dictionary<string, int> items = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsWoodGatheredVIP : _config.RewardSet.rewardsWoodGathered;

                            if (_config.AchievementsImg.enableItems)
                            {
                                foreach (KeyValuePair<string, int> item in items)
                                {
                                    Item give = ItemManager.CreateByName(item.Key, item.Value);

                                    if (give == null)
                                    {
                                        continue;
                                    }

                                    player.GiveItem(give);
                                }
                            }

                            _data.PlayerDatas[player.userID].AchievementsRedeemed.Add("AxeMan");
                            SendEffectTo(RewardsFX, player);
                            DisplayAchievements(player);
                            if (_config.AchievementsImg.enableRP)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsWoodGatheredRPVIP : _config.RewardSet.rewardsWoodGatheredRP;
                                AddPoints(player, rp);
                            }
                            
                            if (_config.AchievementsImg.enableEconomics)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsWoodGatheredEconomicsVIP : _config.RewardSet.rewardsWoodGatheredEconomics;
                                AddEconomics(player, rp);
                            }
                        }
                        
                        break;
                    }
                    case "redeem_miner":
                    {
                        if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("Miner"))
                        {
                            return;
                        }
                        
                        int minStones = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minStonesGatheredVIP : _config.AchievementsSet.minStonesGathered;

                        if (_data.PlayerDatas[player.userID].StoneGathered >= minStones)
                        {
                            Dictionary<string, int> items = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsStonesGatheredVIP : _config.RewardSet.rewardsStonesGathered;

                            if (_config.AchievementsImg.enableItems)
                            {
                                foreach (KeyValuePair<string, int> item in items)
                                {
                                    Item give = ItemManager.CreateByName(item.Key, item.Value);

                                    if (give == null)
                                    {
                                        continue;
                                    }

                                    player.GiveItem(give);
                                }
                            }

                            _data.PlayerDatas[player.userID].AchievementsRedeemed.Add("Miner");
                            SendEffectTo(RewardsFX, player);
                            DisplayAchievements(player);
                            if (_config.AchievementsImg.enableRP)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsStonesGatheredRPVIP : _config.RewardSet.rewardsStonesGatheredRP;
                                AddPoints(player, rp);
                            }
                            
                            if (_config.AchievementsImg.enableEconomics)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsStonesGatheredEconomicsVIP : _config.RewardSet.rewardsStonesGatheredEconomics;
                                AddEconomics(player, rp);
                            }
                        }
                        
                        break;
                    }
                    case "redeem_metalgear":
                    {
                        if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("MetalGear"))
                        {
                            return;
                        }
                        
                        int minMetals = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minMetalsGatheredVIP : _config.AchievementsSet.minMetalsGathered;

                        if (_data.PlayerDatas[player.userID].MetalGathered >= minMetals)
                        {
                            Dictionary<string, int> items = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsMetalsGatheredVIP : _config.RewardSet.rewardsMetalsGathered;

                            if (_config.AchievementsImg.enableItems)
                            {
                                foreach (KeyValuePair<string, int> item in items)
                                {
                                    Item give = ItemManager.CreateByName(item.Key, item.Value);

                                    if (give == null)
                                    {
                                        continue;
                                    }

                                    player.GiveItem(give);
                                }
                            }

                            _data.PlayerDatas[player.userID].AchievementsRedeemed.Add("MetalGear");
                            SendEffectTo(RewardsFX, player);
                            DisplayAchievements(player);
                            if (_config.AchievementsImg.enableRP)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsMetalsGatheredRPVIP : _config.RewardSet.rewardsMetalsGatheredRP;
                                AddPoints(player, rp);
                            }
                            
                            if (_config.AchievementsImg.enableEconomics)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsMetalsGatheredEconomicsVIP : _config.RewardSet.rewardsMetalsGatheredEconomics;
                                AddEconomics(player, rp);
                            }
                        }
                        
                        break;
                    }
                    case "redeem_evilminer":
                    {
                        if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("EvilMiner"))
                        {
                            return;
                        }
                        
                        int minSulfurs = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minSulfursGatheredVIP : _config.AchievementsSet.minSulfursGathered;

                        if (_data.PlayerDatas[player.userID].SulfurGathered >= minSulfurs)
                        {
                            Dictionary<string, int> items = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsSulfursGatheredVIP : _config.RewardSet.rewardsSulfursGathered;

                            if (_config.AchievementsImg.enableItems)
                            {
                                foreach (KeyValuePair<string, int> item in items)
                                {
                                    Item give = ItemManager.CreateByName(item.Key, item.Value);

                                    if (give == null)
                                    {
                                        continue;
                                    }

                                    player.GiveItem(give);
                                }
                            }

                            _data.PlayerDatas[player.userID].AchievementsRedeemed.Add("EvilMiner");
                            SendEffectTo(RewardsFX, player);
                            DisplayAchievements(player);
                            if (_config.AchievementsImg.enableRP)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsSulfursGatheredRPVIP : _config.RewardSet.rewardsSulfursGatheredRP;
                                AddPoints(player, rp);
                            }
                            
                            if (_config.AchievementsImg.enableEconomics)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsSulfursGatheredEconomicsVIP : _config.RewardSet.rewardsSulfursGatheredEconomics;
                                AddEconomics(player, rp);
                            }
                        }
                        
                        break;
                    }
                    case "redeem_outsmarted":
                    {
                        if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("Outsmarted"))
                        {
                            return;
                        }
                        
                        int minScientists = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minScientistsKillsVIP : _config.AchievementsSet.minScientistsKills;

                        if (_data.PlayerDatas[player.userID].ScientistsKilled >= minScientists)
                        {
                            Dictionary<string, int> items = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsScientistsKillsVIP : _config.RewardSet.rewardsScientistsKills;

                            if (_config.AchievementsImg.enableItems)
                            {
                                foreach (KeyValuePair<string, int> item in items)
                                {
                                    Item give = ItemManager.CreateByName(item.Key, item.Value);

                                    if (give == null)
                                    {
                                        continue;
                                    }

                                    player.GiveItem(give);
                                }
                            }

                            _data.PlayerDatas[player.userID].AchievementsRedeemed.Add("Outsmarted");
                            SendEffectTo(RewardsFX, player);
                            DisplayAchievements(player);
                            if (_config.AchievementsImg.enableRP)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsScientistsKillsRPVIP : _config.RewardSet.rewardsScientistsKillsRP;
                                AddPoints(player, rp);
                            }
                            
                            if (_config.AchievementsImg.enableEconomics)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsScientistsKillsEconomicsVIP : _config.RewardSet.rewardsScientistsKillsEconomics;
                                AddEconomics(player, rp);
                            }
                        }
                        
                        break;
                    }
                    case "redeem_juggernaut":
                    {
                        if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("Juggernaut"))
                        {
                            return;
                        }
                        
                        int minHeavies = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minHeaviesKillsVIP : _config.AchievementsSet.minHeaviesKills;

                        if (_data.PlayerDatas[player.userID].HeaviesKilled >= minHeavies)
                        {
                            Dictionary<string, int> items = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsHeaviesKillsVIP : _config.RewardSet.rewardsHeaviesKills;

                            if (_config.AchievementsImg.enableItems)
                            {
                                foreach (KeyValuePair<string, int> item in items)
                                {
                                    Item give = ItemManager.CreateByName(item.Key, item.Value);

                                    if (give == null)
                                    {
                                        continue;
                                    }

                                    player.GiveItem(give);
                                }
                            }

                            _data.PlayerDatas[player.userID].AchievementsRedeemed.Add("Juggernaut");
                            SendEffectTo(RewardsFX, player);
                            DisplayAchievements(player);
                            if (_config.AchievementsImg.enableRP)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsHeaviesKillsRPVIP : _config.RewardSet.rewardsHeaviesKillsRP;
                                AddPoints(player, rp);
                            }
                            
                            if (_config.AchievementsImg.enableEconomics)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsHeaviesKillsEconomicsVIP : _config.RewardSet.rewardsHeaviesKillsEconomics;
                                AddEconomics(player, rp);
                            }
                        }
                        
                        break;
                    }
                    case "redeem_piggy":
                    {
                        if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("Piggy"))
                        {
                            return;
                        }
                        
                        int minBoars = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minBoarsKillsVIP : _config.AchievementsSet.minBoarsKills;

                        if (_data.PlayerDatas[player.userID].BoarsKilled >= minBoars)
                        {
                            Dictionary<string, int> items = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsBoarsKillsVIP : _config.RewardSet.rewardsBoarsKills;

                            if (_config.AchievementsImg.enableItems)
                            {
                                foreach (KeyValuePair<string, int> item in items)
                                {
                                    Item give = ItemManager.CreateByName(item.Key, item.Value);

                                    if (give == null)
                                    {
                                        continue;
                                    }

                                    player.GiveItem(give);
                                }
                            }

                            _data.PlayerDatas[player.userID].AchievementsRedeemed.Add("Piggy");
                            SendEffectTo(RewardsFX, player);
                            DisplayAchievements(player);
                            if (_config.AchievementsImg.enableRP)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsBoarsKillsRPVIP : _config.RewardSet.rewardsBoarsKillsRP;
                                AddPoints(player, rp);
                            }
                            
                            if (_config.AchievementsImg.enableEconomics)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsBoarsKillsEconomicsVIP : _config.RewardSet.rewardsBoarsKillsEconomics;
                                AddEconomics(player, rp);
                            }
                        }
                        
                        break;
                    }
                    case "redeem_pookie":
                    {
                        if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("Pookie"))
                        {
                            return;
                        }
                        
                        int minBears = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minBearsKillsVIP : _config.AchievementsSet.minBearsKills;

                        if (_data.PlayerDatas[player.userID].BearsKilled >= minBears)
                        {
                            Dictionary<string, int> items = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsBearsKillsVIP : _config.RewardSet.rewardsBearsKills;

                            if (_config.AchievementsImg.enableItems)
                            {
                                foreach (KeyValuePair<string, int> item in items)
                                {
                                    Item give = ItemManager.CreateByName(item.Key, item.Value);

                                    if (give == null)
                                    {
                                        continue;
                                    }

                                    player.GiveItem(give);
                                }
                            }

                            _data.PlayerDatas[player.userID].AchievementsRedeemed.Add("Pookie");
                            SendEffectTo(RewardsFX, player);
                            DisplayAchievements(player);
                            if (_config.AchievementsImg.enableRP)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsBearsKillsRPVIP : _config.RewardSet.rewardsBearsKillsRP;
                                AddPoints(player, rp);
                            }
                            
                            if (_config.AchievementsImg.enableEconomics)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsBearsKillsEconomicsVIP : _config.RewardSet.rewardsBearsKillsEconomics;
                                AddEconomics(player, rp);
                            }
                        }

                        break;
                    }
                    case "redeem_chickendinner":
                    {
                        if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("ChickenDinner"))
                        {
                            return;
                        }
                        
                        int minChickens = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minChickensKillsVIP : _config.AchievementsSet.minChickensKills;

                        if (_data.PlayerDatas[player.userID].ChickensKilled >= minChickens)
                        {
                            Dictionary<string, int> items = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsChickensKillsVIP : _config.RewardSet.rewardsChickensKills;

                            if (_config.AchievementsImg.enableItems)
                            {
                                foreach (KeyValuePair<string, int> item in items)
                                {
                                    Item give = ItemManager.CreateByName(item.Key, item.Value);

                                    if (give == null)
                                    {
                                        continue;
                                    }

                                    player.GiveItem(give);
                                }
                            }

                            _data.PlayerDatas[player.userID].AchievementsRedeemed.Add("ChickenDinner");
                            SendEffectTo(RewardsFX, player);
                            DisplayAchievements(player);
                            if (_config.AchievementsImg.enableRP)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsChickensKillsRPVIP : _config.RewardSet.rewardsChickensKillsRP;
                                AddPoints(player, rp);
                            }
                            
                            if (_config.AchievementsImg.enableEconomics)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsChickensKillsEconomicsVIP : _config.RewardSet.rewardsChickensKillsEconomics;
                                AddEconomics(player, rp);
                            }
                        }

                        break;
                    }
                    case "redeem_bambi":
                    {
                        if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("Bambi"))
                        {
                            return;
                        }
                        
                        int minStags = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minStagsKillsVIP : _config.AchievementsSet.minStagsKills;

                        if (_data.PlayerDatas[player.userID].StagsKilled >= minStags)
                        {
                            Dictionary<string, int> items = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsStagsKillsVIP : _config.RewardSet.rewardsStagsKills;

                            if (_config.AchievementsImg.enableItems)
                            {
                                foreach (KeyValuePair<string, int> item in items)
                                {
                                    Item give = ItemManager.CreateByName(item.Key, item.Value);

                                    if (give == null)
                                    {
                                        continue;
                                    }

                                    player.GiveItem(give);
                                }
                            }

                            _data.PlayerDatas[player.userID].AchievementsRedeemed.Add("Bambi");
                            SendEffectTo(RewardsFX, player);
                            DisplayAchievements(player);
                            if (_config.AchievementsImg.enableRP)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsStagsKillsRPVIP : _config.RewardSet.rewardsStagsKillsRP;
                                AddPoints(player, rp);
                            }
                            
                            if (_config.AchievementsImg.enableEconomics)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsStagsKillsEconomicsVIP : _config.RewardSet.rewardsStagsKillsEconomics;
                                AddEconomics(player, rp);
                            }
                        }

                        break;
                    }
                    case "redeem_lonewolf":
                    {
                        if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("LoneWolf"))
                        {
                            return;
                        }
                        
                        int minWolves = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minWolvesKillsVIP : _config.AchievementsSet.minWolvesKills;

                        if (_data.PlayerDatas[player.userID].WolvesKilled >= minWolves)
                        {
                            Dictionary<string, int> items = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsWolvesKillsVIP : _config.RewardSet.rewardsWolvesKills;

                            if (_config.AchievementsImg.enableItems)
                            {
                                foreach (KeyValuePair<string, int> item in items)
                                {
                                    Item give = ItemManager.CreateByName(item.Key, item.Value);

                                    if (give == null)
                                    {
                                        continue;
                                    }

                                    player.GiveItem(give);
                                }
                            }

                            _data.PlayerDatas[player.userID].AchievementsRedeemed.Add("LoneWolf");
                            SendEffectTo(RewardsFX, player);
                            DisplayAchievements(player);
                            if (_config.AchievementsImg.enableRP)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsWolvesKillsRPVIP : _config.RewardSet.rewardsWolvesKillsRP;
                                AddPoints(player, rp);
                            }
                            
                            if (_config.AchievementsImg.enableEconomics)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsWolvesKillsEconomicsVIP : _config.RewardSet.rewardsWolvesKillsEconomics;
                                AddEconomics(player, rp);
                            }
                        }

                        break;
                    }
                    case "redeem_tankhunter":
                    {
                        if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("TankHunter"))
                        {
                            return;
                        }
                        
                        int minBradleys = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minBradleysKillsVIP : _config.AchievementsSet.minBradleysKills;

                        if (_data.PlayerDatas[player.userID].BradleyDestroyed >= minBradleys)
                        {
                            Dictionary<string, int> items = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsBradleysKillsVIP : _config.RewardSet.rewardsBradleysKills;

                            if (_config.AchievementsImg.enableItems)
                            {
                                foreach (KeyValuePair<string, int> item in items)
                                {
                                    Item give = ItemManager.CreateByName(item.Key, item.Value);

                                    if (give == null)
                                    {
                                        continue;
                                    }

                                    player.GiveItem(give);
                                }
                            }

                            _data.PlayerDatas[player.userID].AchievementsRedeemed.Add("TankHunter");
                            SendEffectTo(RewardsFX, player);
                            DisplayAchievements(player);
                            if (_config.AchievementsImg.enableRP)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsBradleysKillsRPVIP : _config.RewardSet.rewardsBradleysKillsRP;
                                AddPoints(player, rp);
                            }
                            
                            if (_config.AchievementsImg.enableEconomics)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsBradleysKillsEconomicsVIP : _config.RewardSet.rewardsBradleysKillsEconomics;
                                AddEconomics(player, rp);
                            }
                        }

                        break;
                    }
                    case "redeem_chopperhunter":
                    {
                        if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("ChopperHunter"))
                        {
                            return;
                        }
                        
                        int minChoppers = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minChoppersKillsVIP : _config.AchievementsSet.minChoppersKills;

                        if (_data.PlayerDatas[player.userID].HelicopterDestroyed >= minChoppers)
                        {
                            Dictionary<string, int> items = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsChoppersKillsVIP : _config.RewardSet.rewardsChoppersKills;

                            if (_config.AchievementsImg.enableItems)
                            {
                                foreach (KeyValuePair<string, int> item in items)
                                {
                                    Item give = ItemManager.CreateByName(item.Key, item.Value);

                                    if (give == null)
                                    {
                                        continue;
                                    }
                                
                                    player.GiveItem(give);
                                }
                            }

                            _data.PlayerDatas[player.userID].AchievementsRedeemed.Add("ChopperHunter");
                            SendEffectTo(RewardsFX, player);
                            DisplayAchievements(player);
                            if (_config.AchievementsImg.enableRP)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsChoppersKillsRPVIP : _config.RewardSet.rewardsChoppersKillsRP;
                                AddPoints(player, rp);
                            }
                            
                            if (_config.AchievementsImg.enableEconomics)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsChoppersKillsEconomicsVIP : _config.RewardSet.rewardsChoppersKillsEconomics;
                                AddEconomics(player, rp);
                            }

                        }

                        break;
                    }
                    case "redeem_skinner":
                    {
                        if (_data.PlayerDatas[player.userID].AchievementsRedeemed.Contains("Skinner"))
                        {
                            return;
                        }
                        
                        int minCloth = HasPermission(player, UsePermissionVIP) ? _config.AchievementsSetVIP.minClothSkinnedVIP : _config.AchievementsSet.minClothSkinned;

                        if (_data.PlayerDatas[player.userID].ClothSkinned >= minCloth)
                        {
                            Dictionary<string, int> items = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsClothSkinnedVIP : _config.RewardSet.rewardsClothSkinned;

                            if (_config.AchievementsImg.enableItems)
                            {
                                foreach (KeyValuePair<string, int> item in items)
                                {
                                    Item give = ItemManager.CreateByName(item.Key, item.Value);

                                    if (give == null)
                                    {
                                        continue;
                                    }
                                
                                    player.GiveItem(give);
                                }
                            }

                            _data.PlayerDatas[player.userID].AchievementsRedeemed.Add("Skinner");
                            SendEffectTo(RewardsFX, player);
                            DisplayAchievements(player);
                            if (_config.AchievementsImg.enableRP)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsClothSkinnedRPVIP : _config.RewardSet.rewardsClothSkinnedRP;
                                AddPoints(player, rp);
                            }
                            
                            if (_config.AchievementsImg.enableEconomics)
                            {
                                int rp = HasPermission(player, UsePermissionVIP) ? _config.RewardSetVIP.rewardsClothSkinnedEconomicsVIP : _config.RewardSet.rewardsClothSkinnedEconomics;
                                AddEconomics(player, rp);
                            }
                        }
                        
                        break;
                    }
                }
            }
        }

        #endregion
    }
}