using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("ManageZone", "tofurahie & FourTeen", "1.1.4")]
    internal class ManageZone : RustPlugin
    {
        #region Static

        private const string Layer = "UI_ManageZone";
        private static ManageZone _ins;
        private Configuration _config;
        private List<ZoneObject> Zones = new List<ZoneObject>();
        private Dictionary<ulong, int> PlayersInfo = new Dictionary<ulong, int>();
        private const string perm = "managezone.use";
        private Dictionary<ulong, int> ZoneCreator = new Dictionary<ulong, int>();
        private Dictionary<ulong, float> cooldown = new Dictionary<ulong, float>();

        #region Image


        #endregion

        #region Classes

        private class ZoneSettings
        {
            [JsonProperty(PropertyName = "ZoneID(Uniq)")]
            public int ZoneID;

            [JsonProperty(PropertyName = "Zone Name")]
            public string ZoneName = "ZONE NAME";

            [JsonProperty(PropertyName = "Zone Type")]
            public string ZoneType = "ZONE TYPE";

            [JsonProperty(PropertyName = "Zone Type Color(HEX FORMAT)")]
            public string ZoneTypeColor = "white";

            [JsonProperty(PropertyName = "Zone Position")]
            public Vector3 ZonePosition = Vector3.zero;

            [JsonProperty(PropertyName = "Zone Radius")]
            public float ZoneRadius = 10;

            [JsonProperty(PropertyName = "Visible sphere?")]
            public bool CreateVisibleZone = true;

            [JsonProperty(PropertyName = "Can damage other players in Zone")]
            public bool CanDamagePlayers = false;

            [JsonProperty(PropertyName = "Can damage other player structures")]
            public bool CanDamagePlayerStructures = false;

            [JsonProperty(PropertyName = "Can damage NPC")]
            public bool CanDamageNPC = false;

            [JsonProperty(PropertyName = "Can damage Helicopter")]
            public bool CanDamageHelicopter = false;
            [JsonProperty(PropertyName = "Permission")]
            public string permission = "";
        }

        private class Configuration
        {
            [JsonProperty(PropertyName = "Show Notification")]
            public bool ShowNotification = true;

            [JsonProperty(PropertyName = "Show None zone type")]
            public bool ShowNone = true;

            [JsonProperty(PropertyName = "Notification message when enter in zone(%ZONENAME% - Zone name, %ZONETYPE% - Zone type)")]
            public string EnterMessage = "You have entered the zone %ZONENAME%\nZone type: %ZONETYPE%";

            [JsonProperty(PropertyName = "Notification message when leave from zone")]
            public string LeaveMessage = "You have left the Zone, now you are in the open world";

            [JsonProperty(PropertyName = "Zones", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ZoneSettings> ZoneSettingsList = new List<ZoneSettings>();
        }

        #endregion

        void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Title + "/ZoneCreator", ZoneCreator);
            Interface.Oxide.DataFileSystem.WriteObject(Title + "/CoolDown", cooldown);
        }
        void LoadData()
        {
            try
            {
                ZoneCreator = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, int>>(Title + "/ZoneCreator");
                cooldown = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, float>>(Title + "/CoolDown");
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
            }
        }

        #endregion

        #region Config

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        protected override void LoadDefaultConfig() => _config = new Configuration();

        #endregion

        #region OxideHooks

        private void OnServerInitialized()
        {
            LoadData();
            _ins = this;
            List<KeyValuePair<ulong, float>> changes = new List<KeyValuePair<ulong, float>>();

            foreach (var kvp in cooldown)
            {
                Puts($"{kvp.Key}, {kvp.Value}, {kvp.Value + Time.time}");
                var cd = kvp.Value + Time.time;
                var userid = kvp.Key;
                changes.Add(new KeyValuePair<ulong, float>(userid, cd));
            }

            foreach (var change in changes)
            {
                cooldown.Remove(change.Key);
                cooldown.Add(change.Key, change.Value);
                timer.Once(change.Value, () => cooldown.Remove(change.Key));
            }
            SaveData();
            foreach (var check in BasePlayer.activePlayerList) OnPlayerConnected(check);
            foreach (var check in _config.ZoneSettingsList)
            {
                SpawnZone(check.ZoneID);
                if (!permission.PermissionExists(check.permission)) permission.RegisterPermission(check.permission, this);
            }
            if (!permission.PermissionExists(perm)) permission.RegisterPermission(perm, this);
        }

        void OnNewSave(string filename)
        {
            foreach (var check in _config.ZoneSettingsList)
            {
                DestroyZone(check.ZoneID);
            }
            foreach (var kvp in ZoneCreator)
            {
                if (permission.PermissionExists($"managezone.{kvp.Value}"))
                {
                    if (permission.UserHasPermission(kvp.Key.ToString(), $"managezone.{kvp.Value}")) permission.RevokeUserPermission(kvp.Key.ToString(), $"managezone.{kvp.Value}");
                }
                ZoneCreator.Remove(kvp.Key);
            }
        }
        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null) return;
            UpdateZone(player, 0);
        }

        private void OnPlayerDeath(BasePlayer player, HitInfo info) =>
            NextTick(() =>
            {
                if (player != null && player.IsDead()) UpdateZone(player, 0);
            });

        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (player == null || !PlayersInfo.ContainsKey(player.userID)) return;
            PlayersInfo.Remove(player.userID);
            UpdateZone(player, 0);
        }

        private void Unload()
        {
            List<KeyValuePair<ulong, float>> changes = new List<KeyValuePair<ulong, float>>();

            foreach (var kvp in cooldown)
            {
                var cd = kvp.Value - Time.time;
                var userid = kvp.Key;
                changes.Add(new KeyValuePair<ulong, float>(userid, cd));
            }

            foreach (var change in changes)
            {
                cooldown.Remove(change.Key);
                cooldown.Add(change.Key, change.Value);
            }

            foreach (var check in Zones.ToArray()) DestroyZone(check.ZoneID);
            foreach (var check in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(check, Layer);
                CuiHelper.DestroyUi(check, Layer + ".ZONE");
                DestroyAlert(check);
            }

            SaveData();
            _ins = null;
        }


        //private void OnEntityTakeDamage(BasePlayer target, HitInfo info)
        //{
        //    if (target == null || !target.userID.IsSteamId() || !PlayersInfo.ContainsKey(target.userID)) return;
        //    if (InitiatorIsHelicopter(info))
        //    {
        //        var zS = GetZoneSettings(PlayersInfo[target.userID]);
        //        if (zS != null && !zS.CanDamageHelicopter)
        //        {
        //            info?.damageTypes?.ScaleAll(0);
        //            return;
        //        }
        //    }

        //    var attacker = info?.InitiatorPlayer;
        //    var zoneSettings = GetZoneSettings(PlayersInfo[target.userID]);
        //    if (zoneSettings == null || attacker == null) return;
        //    if (attacker.userID.IsSteamId())
        //    {
        //        if (zoneSettings.CanDamagePlayers) return;
        //        info.damageTypes?.ScaleAll(0);
        //        info.DoHitEffects = false;
        //    }
        //    else if (!zoneSettings.CanDamageNPC)
        //    {
        //        info.damageTypes?.ScaleAll(0);
        //        info.DoHitEffects = false;
        //    }
        //}

        private void OnEntityTakeDamage(BuildingBlock block, HitInfo info)
        {
            if (block == null || info == null) return;
            var attacker = info.InitiatorPlayer;
            if (InitiatorIsHelicopter(info))
            {
                foreach (var check in _config.ZoneSettingsList)
                {
                    if (check.CanDamageHelicopter || !(Vector3.Distance(check.ZonePosition, block.transform.position) <= check.ZoneRadius)) continue;
                    info.damageTypes?.ScaleAll(0);
                    return;
                }
            }
            if (attacker == null || !attacker.userID.IsSteamId() || attacker.userID == block.OwnerID || GetZoneSettings(PlayersInfo[attacker.userID]) == null) return;
            if (!GetZoneSettings(PlayersInfo[attacker.userID]).CanDamagePlayerStructures) info.damageTypes?.ScaleAll(0);
        }

        private void OnEntityTakeDamage(Door block, HitInfo info)
        {
            if (block == null || info == null) return;
            var attacker = info.InitiatorPlayer;
            if (InitiatorIsHelicopter(info))
            {
                foreach (var check in _config.ZoneSettingsList)
                {
                    if (!check.CanDamageHelicopter && Vector3.Distance(check.ZonePosition, block.transform.position) <= check.ZoneRadius)
                    {
                        info?.damageTypes?.ScaleAll(0);
                        return;
                    }
                }
            }
            if (attacker == null || !attacker.userID.IsSteamId() || attacker.userID == block.OwnerID || GetZoneSettings(PlayersInfo[attacker.userID]) == null) return;
            if (!GetZoneSettings(PlayersInfo[attacker.userID]).CanDamagePlayerStructures) info.damageTypes?.ScaleAll(0);
        }

        private void OnEntityTakeDamage(IOEntity block, HitInfo info)
        {
            if (block == null || info == null) return;
            var attacker = info?.InitiatorPlayer;
            if (InitiatorIsHelicopter(info))
            {
                foreach (var check in _config.ZoneSettingsList)
                {
                    if (!check.CanDamageHelicopter && Vector3.Distance(check.ZonePosition, block.transform.position) <= check.ZoneRadius)
                    {
                        info?.damageTypes?.ScaleAll(0);
                        return;
                    }
                }
            }
            if (attacker == null || !attacker.userID.IsSteamId() || attacker.userID == block.OwnerID || GetZoneSettings(PlayersInfo[attacker.userID]) == null) return;
            if (!GetZoneSettings(PlayersInfo[attacker.userID]).CanDamagePlayerStructures) info.damageTypes?.ScaleAll(0);
        }

        private void OnEntityTakeDamage(StorageContainer block, HitInfo info)
        {
            if (block == null || info == null) return;
            var attacker = info?.InitiatorPlayer;
            if (InitiatorIsHelicopter(info))
            {
                foreach (var check in _config.ZoneSettingsList)
                {
                    if (!check.CanDamageHelicopter && Vector3.Distance(check.ZonePosition, block.transform.position) <= check.ZoneRadius)
                    {
                        info?.damageTypes?.ScaleAll(0);
                        return;
                    }
                }
            }
            if (attacker == null || !attacker.userID.IsSteamId() || attacker.userID == block.OwnerID || GetZoneSettings(PlayersInfo[attacker.userID]) == null) return;
            if (!GetZoneSettings(PlayersInfo[attacker.userID]).CanDamagePlayerStructures) info.damageTypes?.ScaleAll(0);
        }

        private bool InitiatorIsHelicopter(HitInfo hitInfo)
        {
            if (hitInfo.Initiator != null && (hitInfo.Initiator is BaseHelicopter || hitInfo.Initiator.ShortPrefabName.Equals("oilfireballsmall") || hitInfo.Initiator.ShortPrefabName.Equals("napalm"))) return true;
            return hitInfo.WeaponPrefab != null && (hitInfo.WeaponPrefab.ShortPrefabName.Equals("rocket_heli") || hitInfo.WeaponPrefab.ShortPrefabName.Equals("rocket_heli_napalm"));
        }

        #endregion
        private void SetGlobalCooldown(BasePlayer player)
        {
            if (player == null) return;
            float rb = 11400;
            ulong userid = player.userID;
            cooldown[userid] = Time.time + rb;
            timer.Once(rb, () => cooldown.Remove(userid));
        }
        private float GetGlobalCooldown(BasePlayer player)
        {
            if (player == null) return 0f;
            float cd;
            if (!cooldown.TryGetValue(player.userID, out cd))
            {
                return 0f;
            }
            return cd - Time.time;
        }

        #region Commands

        [ChatCommand("zm")]
        private void cmdChatzm(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, perm)) return;
            ShowUIZones(player);
        }

        [ConsoleCommand("UI_ZONE")]
        private void cmdConsoleUI_ZONE(ConsoleSystem.Arg arg)
        {
            if (arg == null || !arg.HasArgs()) return;
            var player = arg.Player();
            ZoneSettings zone;
            switch (arg.GetString(0))
            {
                case "CLOSE":
                    DestroyAlert(player);
                    break;
                case "ADDNEWZONE":
                    zone = new ZoneSettings();
                    _config.ZoneSettingsList.Add(zone);
                    zone.ZoneID = UnityEngine.Random.Range(0, int.MaxValue);
                    zone.ZonePosition = player.transform.position;
                    ShowUIZoneSettings(player, zone.ZoneID);
                    SpawnZone(zone.ZoneID);
                    ZoneCreator.Add(player.userID, zone.ZoneID);
                    zone.permission = $"managezone.{zone.ZoneID}";
                    if (!permission.PermissionExists($"managezone.{zone.ZoneID}")) permission.RegisterPermission($"managezone.{zone.ZoneID}", this);
                    permission.GrantUserPermission(player.UserIDString, $"managezone.{zone.ZoneID}", this);
                    SetGlobalCooldown(player);
                    SaveData();
                    SaveConfig();
                    break;
                case "OPENZONESETTINGS":
                    ;
                    ShowUIZoneSettings(player, arg.GetInt(1));
                    break;
                case "OPENZONES":
                    ShowUIZones(player, arg.GetInt(1));
                    break;
                case "REMOVEZONE":
                    float time = GetGlobalCooldown(player);
                    if (time > 0f)
                    {
                        int hours = (int)(time / 3600);
                        if (hours < 0) hours = 0;

                        time %= 3600;

                        int minutes = (int)(time / 60);
                        if (minutes < 0) minutes = 0;

                        time %= 60;

                        int seconds = (int)time;
                        if (seconds < 0) seconds = 0;
                        if (hours == 0 && minutes == 0 && seconds != 0) player.ChatMessage($"Чтобы удалить текущую зону и установить новую, необходимо подождать - <color=#FF9740>{seconds}</color> секунд(у).");
                        else if (hours == 0 && minutes != 0) player.ChatMessage($"Чтобы удалить текущую зону и установить новую, необходимо подождать - <color=#FF9740>{minutes}</color> минут(у).");
                        else if (hours != 0) player.ChatMessage($"Чтобы удалить текущую зону и установить новую, необходимо подождать - <color=#FF9740>{hours}</color> час(а).");
                        return;
                    }
                    var z = GetZoneSettings(arg.GetInt(1));
                    _config.ZoneSettingsList.Remove(GetZoneSettings(arg.GetInt(1)));
                    DestroyZone(arg.GetInt(1));
                    ShowUIZones(player);
                    SaveConfig();
                    if (!permission.PermissionExists($"managezone.{z.ZoneName}")) permission.RevokeUserPermission(player.UserIDString, $"managezone.{z.ZoneName}");
                    ZoneCreator.Remove(player.userID);
                    SaveData();
                    break;
                case "NAME":
                    if (arg.Args.Length < 3) return;
                    zone = GetZoneSettings(arg.GetInt(1));
                    zone.ZoneName = string.Join(" ", arg.Args.Skip(2));
                    ShowUIZoneSettings(player, zone.ZoneID);
                    SaveConfig();
                    break;
                case "TYPE":
                    if (arg.Args.Length < 3) return;
                    zone = GetZoneSettings(arg.GetInt(1));
                    zone.ZoneType = string.Join(" ", arg.Args.Skip(2));
                    ShowUIZoneSettings(player, zone.ZoneID);
                    SaveConfig();
                    break;
                case "COLOR":
                    if (arg.Args.Length < 3) return;
                    zone = GetZoneSettings(arg.GetInt(1));
                    zone.ZoneTypeColor = string.Join(" ", arg.Args.Skip(2));
                    ShowUIZoneSettings(player, zone.ZoneID);
                    SaveConfig();
                    break;
                case "RADIUS":
                    if (arg.Args.Length < 3) return;
                    zone = GetZoneSettings(arg.GetInt(1));
                    if (arg.GetFloat(2) <= 200)
                        zone.ZoneRadius = arg.GetFloat(2);
                    else zone.ZoneRadius = 200;
                    UpdateZone(zone.ZoneID);
                    ShowUIZoneSettings(player, zone.ZoneID);
                    SaveConfig();
                    break;
            }
        }

        #endregion

        #region Functions

        private bool TargetInZone(ulong userID, ulong attackerUserID) => GetZoneSettings(PlayersInfo[userID]) != null;

        private void UpdateZone(BasePlayer player, int zoneID)
        {
            if (!PlayersInfo.TryAdd(player.userID, zoneID)) PlayersInfo[player.userID] = zoneID;
            var zone = GetZoneSettings(zoneID);
            if (zone == null)
            {
                if (_config.ShowNone) ShowUICurrentZone(player, "");
                else CuiHelper.DestroyUi(player, Layer + ".ZONE");
                if (_config.ShowNotification) ShowUIAlert(player, _config.LeaveMessage);
                return;
            }
            ShowUICurrentZone(player, $"<color={zone.ZoneTypeColor}>{zone.ZoneType}</color>");
            if (_config.ShowNotification) ShowUIAlert(player, _config.EnterMessage.Replace("%ZONENAME%", $"{zone.ZoneName}").Replace("%ZONETYPE%", $"<color={zone.ZoneTypeColor}>{zone.ZoneType}</color>"));
            if (!string.IsNullOrEmpty(zone.permission))
            {
                RelationshipManager.PlayerTeam team = RelationshipManager.ServerInstance.FindTeam(player.currentTeam);
                if (!permission.UserHasPermission(player.UserIDString, zone.permission))
                {
                    bool flag = false;
                    if (team != null)
                    {
                        foreach (var pl in team.members)
                        {
                            if (permission.UserHasPermission(pl.ToString(), zone.permission))
                            {
                                flag = true;
                                break;
                            }
                        }
                    }
                    if (flag == false)
                    {
                        //player.Kick("ПРИВАТНАЯ КАСТОМКА ВХОД ЗАПРЕЩЁН!");
                        //player.AdminKill();
                        player.Hurt(999999999999999f, Rust.DamageType.Generic);
                    }
                }
            }
        }

        private ZoneSettings GetZoneSettings(int zoneID)
        {
            foreach (var check in _config.ZoneSettingsList) if (check.ZoneID == zoneID) return check;
            return null;
        }
        private object GetZoneSettingsByPos(Vector3 vector)
        {
            foreach (var check in _config.ZoneSettingsList)
            {
                if (check.ZonePosition == vector) return check.ZoneID;
            }
            return null;
        }
        private ZoneObject GetZone(int zoneID)
        {
            foreach (var check in Zones) if (check.ZoneID == zoneID) return check;
            return null;
        }

        private void SpawnZone(int zoneID)
        {
            var zone = new GameObject().AddComponent<ZoneObject>();
            zone.SpawnZone(GetZoneSettings(zoneID));
            Zones.Add(zone);
        }

        private void UpdateZone(int zoneID)
        {
            DestroyZone(zoneID);
            SpawnZone(zoneID);
        }

        private void DestroyZone(int zoneID)
        {
            var zone = GetZone(zoneID);
            Zones.Remove(zone);
            zone?.Kill();
        }

        private class ZoneObject : FacepunchBehaviour
        {
            private SphereEntity _sphere;
            private SphereCollider _collider;
            public int ZoneID;

            public void SpawnZone(ZoneSettings settings)
            {
                ZoneID = settings.ZoneID;
                var radius = settings.ZoneRadius;
                if (settings.CreateVisibleZone)
                {
                    _sphere = (SphereEntity)GameManager.server.CreateEntity("assets/prefabs/visualization/sphere.prefab", settings.ZonePosition);
                    _sphere.currentRadius = radius;
                    _sphere.lerpRadius = radius;
                    _sphere.lerpSpeed = 3f;
                    _sphere.Spawn();
                    _sphere.SendNetworkUpdateImmediate();
                }
                _collider = gameObject.AddComponent<SphereCollider>();
                _collider.gameObject.layer = (int)Rust.Layer.Reserved1;
                _collider.transform.position = settings.ZonePosition;
                _collider.isTrigger = true;
                _collider.radius = radius / 2;
                _collider.SetActive(true);
            }

            private void OnTriggerEnter(Collider col)
            {
                if (!col.name.Contains("/player/player.prefab")) return;
                var player = col.GetComponentInParent<BasePlayer>();
                if (player == null) return;
                _ins.UpdateZone(player, ZoneID);
            }

            private void OnTriggerExit(Collider col)
            {
                if (!col.name.Contains("/player/player.prefab")) return;
                var player = col.GetComponentInParent<BasePlayer>();
                if (player == null) return;
                _ins.UpdateZone(player, 0);
            }

            public void Kill()
            {
                if (_sphere != null) _sphere.Kill();
                if (_collider != null) Destroy(_collider);
                Destroy(gameObject);
            }
        }

        #endregion

        #region UI

        private void ShowUIZones(BasePlayer player, int page = 0)
        {

            var container = new CuiElementContainer();
            var y = -35;

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                KeyboardEnabled = true,
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-250 -180", OffsetMax = "250 275" },
                Image = { Color = "0.2 0.2 0.2 1" }
            }, "Overlay", Layer);

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "0 -30", OffsetMax = "500 0" },
                Image = { Color = "0.9 0.2 0.2 1" }
            }, Layer);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "0 -30", OffsetMax = "500 0" },
                Text =
                {
                    Text = "ZONE PLUGIN SETTINGS", Font = "robotocondensed-bold.ttf", FontSize = 20, Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                }
            }, Layer);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-29 -29", OffsetMax = "-1 -1" },
                Button = { Color = "1 1 1 1", Close = Layer, Sprite = "assets/icons/vote_down.png" },
                Text = { Text = "" }
            }, Layer);

            foreach (var check in _config.ZoneSettingsList.Skip(13 * page).Take(13))
            {
                int i;
                if (ZoneCreator.TryGetValue(player.userID, out i))
                {
                    if (i == check.ZoneID)
                    {
                        container.Add(new CuiButton
                        {
                            RectTransform =
                    {
                        AnchorMin = "0.05 1", AnchorMax = "0.95 1",
                        OffsetMin = $"0 {y - 26}", OffsetMax = $"0 {y}"
                    },
                            Button =
                    {
                        Color = "0.25 0.25 0.25 1",
                        Command = $"UI_ZONE OPENZONESETTINGS {check.ZoneID}"
                    },
                            Text =
                    {
                        Text = check.ZoneName,
                        Color = "1 1 1 1",
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 15,
                        Align = TextAnchor.MiddleCenter
                    }
                        }, Layer);
                        container.Add(new CuiButton
                        {
                            RectTransform =
                    {
                        AnchorMin = "0.95 1", AnchorMax = "0.95 1",
                        OffsetMin = $"-27 {y - 26}", OffsetMax = $"-1 {y}"
                    },
                            Button =
                    {
                        Color = "0.6 0.2 0.2 1",
                        Sprite = "assets/icons/close.png",
                        Command = $"UI_ZONE REMOVEZONE {check.ZoneID}"
                    },
                            Text = { Text = "" }
                        }, Layer);
                        y -= 30;
                    }
                }
            }
            if (!ZoneCreator.ContainsKey(player.userID))
            {
                container.Add(new CuiButton
                {

                    RectTransform =
                {
                    AnchorMin = "0.05 1", AnchorMax = "0.95 1",
                    OffsetMin = $"0 {y - 26}", OffsetMax = $"0 {y}"
                },
                    Button =
                {
                    Color = "0.25 0.25 0.25 1",
                    Command = "UI_ZONE ADDNEWZONE"
                },
                    Text =
                {
                    Text = "+",
                    Color = "1 1 1 1",
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 15,
                    Align = TextAnchor.MiddleCenter
                }
                }, Layer);
            }

            if (page > 0)
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                        OffsetMin = $"-56 {y - 26}", OffsetMax = $"-30 {y}"
                    },
                    Button =
                    {
                        Color = "0 0 0 0",
                        Command = $"UI_ZONE OPENZONES {page - 1}"
                    },
                    Text =
                    {
                        Text = "<<",
                        Color = "1 1 1 1",
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 15,
                        Align = TextAnchor.MiddleCenter
                    }
                }, Layer);

            if (Zones.Count - 13 * (page + 1) > 0)
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                        OffsetMin = $"30 {y - 26}", OffsetMax = $"56 {y}"
                    },
                    Button =
                    {
                        Color = "0 0 0 0",
                        Command = $"UI_ZONE OPENZONES {page + 1}"
                    },
                    Text =
                    {
                        Text = ">>",
                        Color = "1 1 1 1",
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 15,
                        Align = TextAnchor.MiddleCenter
                    }
                }, Layer);
            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.AddUi(player, container);
        }

        private void ShowUIZoneSettings(BasePlayer player, int zoneID)
        {
            var zoneSettings = GetZoneSettings(zoneID);
            var container = new CuiElementContainer();
            var y = -35;

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-250 -180", OffsetMax = "250 275" },
                Image = { Color = "0.2 0.2 0.2 1" }
            }, "Overlay", Layer);

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "0 -30", OffsetMax = "500 0" },
                Image = { Color = "0.9 0.2 0.2 1" }
            }, Layer);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "0 -30", OffsetMax = "500 0" },
                Text =
                {
                    Text = "ZONE PLUGIN SETTINGS", Font = "robotocondensed-bold.ttf", FontSize = 20, Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                }
            }, Layer);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-29 -29", OffsetMax = "-1 -1" },
                Button = { Color = "1 1 1 1", Close = Layer, Sprite = "assets/icons/vote_down.png" },
                Text = { Text = "" }
            }, Layer);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "1 -29", OffsetMax = "29 -1" },
                Button = { Color = "1 1 1 1", Command = "UI_ZONE OPENZONES", Sprite = "assets/icons/community_servers.png" },
                Text = { Text = "" }
            }, Layer);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.02 1", AnchorMax = "1 1", OffsetMin = $"0 {y - 25}", OffsetMax = $"0 {y}" },
                Text =
                {
                    Text = "Zone Name:", Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                }
            }, Layer);

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = $"-200 {y - 25}", OffsetMax = $"-10 {y}" },
                Image = { Color = "0.4 0.4 0.4 0.9" }
            }, Layer);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = $"-200 {y - 25}", OffsetMax = $"-10 {y}" },
                Text =
                {
                    Text = zoneSettings.ZoneName, Font = "robotocondensed-regular.ttf", FontSize = 15, Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 0.2"
                }
            }, Layer);

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Align = TextAnchor.MiddleCenter, CharsLimit = 8, FontSize = 17,
                        Command = $"UI_ZONE NAME {zoneID}", Font = "robotocondensed-regular.ttf"
                    },
                    new CuiRectTransformComponent {AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = $"-200 {y - 25}", OffsetMax = $"-10 {y}"}
                }
            });

            y -= 30;

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.02 1", AnchorMax = "1 1", OffsetMin = $"0 {y - 25}", OffsetMax = $"0 {y}" },
                Text =
                {
                    Text = "Zone Type:", Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                }
            }, Layer);

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = $"-200 {y - 25}", OffsetMax = $"-10 {y}" },
                Image = { Color = "0.4 0.4 0.4 0.9" }
            }, Layer);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = $"-200 {y - 25}", OffsetMax = $"-10 {y}" },
                Text =
                {
                    Text = zoneSettings.ZoneType, Font = "robotocondensed-regular.ttf", FontSize = 15, Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 0.2"
                }
            }, Layer);

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Align = TextAnchor.MiddleCenter, CharsLimit = 8, FontSize = 17,
                        Command = $"UI_ZONE TYPE {zoneID}", Font = "robotocondensed-regular.ttf"
                    },
                    new CuiRectTransformComponent {AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = $"-200 {y - 25}", OffsetMax = $"-10 {y}"}
                }
            });

            y -= 30;

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.02 1", AnchorMax = "1 1", OffsetMin = $"0 {y - 25}", OffsetMax = $"0 {y}" },
                Text =
                {
                    Text = "Zone Type Color(HEX FORMAT):", Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                }
            }, Layer);

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = $"-200 {y - 25}", OffsetMax = $"-10 {y}" },
                Image = { Color = "0.4 0.4 0.4 0.9" }
            }, Layer);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = $"-200 {y - 25}", OffsetMax = $"-10 {y}" },
                Text =
                {
                    Text = $"<color={zoneSettings.ZoneTypeColor}>{zoneSettings.ZoneTypeColor}</color>", Font = "robotocondensed-regular.ttf", FontSize = 15, Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 0.2"
                }
            }, Layer);

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Align = TextAnchor.MiddleCenter, CharsLimit = 8, FontSize = 17,
                        Command = $"UI_ZONE COLOR {zoneID}", Font = "robotocondensed-regular.ttf"
                    },
                    new CuiRectTransformComponent {AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = $"-200 {y - 25}", OffsetMax = $"-10 {y}"}
                }
            });

            y -= 30;

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.02 1", AnchorMax = "1 1", OffsetMin = $"0 {y - 25}", OffsetMax = $"0 {y}" },
                Text =
                {
                    Text = "Zone Radius:", Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                }
            }, Layer);

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = $"-200 {y - 25}", OffsetMax = $"-10 {y}" },
                Image = { Color = "0.4 0.4 0.4 0.9" }
            }, Layer);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = $"-200 {y - 25}", OffsetMax = $"-10 {y}" },
                Text =
                {
                    Text = zoneSettings.ZoneRadius.ToString(CultureInfo.InvariantCulture) + " / 200", Font = "robotocondensed-regular.ttf", FontSize = 15, Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 0.2"
                }
            }, Layer);

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Align = TextAnchor.MiddleCenter, CharsLimit = 8, FontSize = 17,
                        Command = $"UI_ZONE RADIUS {zoneID}", Font = "robotocondensed-regular.ttf"
                    },
                    new CuiRectTransformComponent {AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = $"-200 {y - 25}", OffsetMax = $"-10 {y}"}
                }
            });
            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.AddUi(player, container);
        }

        private void ShowUICurrentZone(BasePlayer player, string text)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiElement
            {
                Parent = "Hud",
                Name = Layer + ".ZONE",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = text,
                        Color = "1 1 1 1",
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 20,
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.236 0.024", AnchorMax = "0.344 0.108"
                    }
                }
            });

            CuiHelper.DestroyUi(player, Layer + ".ZONE");
            CuiHelper.AddUi(player, container);
        }

        private void ShowUIAlert(BasePlayer player, string text)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-150 -150", OffsetMax = "150 -35" },
                Image = { Color = "0.2 0.2 0.2 1", FadeIn = 2 },
                FadeOut = 2f
            }, "Hud", Layer + ".alert");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "0 -25", OffsetMax = "300 0" },
                Image = { Color = "0.9 0.2 0.2 1", FadeIn = 2 },
                FadeOut = 2f
            }, Layer + ".alert", Layer + ".alert1");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "108 -23", OffsetMax = "129 -2" },
                Image = { Color = "1 1 1 1", FadeIn = 2, Sprite = "assets/icons/warning.png" },
                FadeOut = 2f
            }, Layer + ".alert", Layer + ".alert2");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "20 -25", OffsetMax = "300 0" },
                Text =
                {
                    Text = "ALERT", Font = "robotocondensed-bold.ttf", FontSize = 20, Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1", FadeIn = 2
                },
                FadeOut = 2f
            }, Layer + ".alert", Layer + ".alert3");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-24 -24", OffsetMax = "-1 -1" },
                Button = { Color = "1 1 1 1", Command = "UI_ZONE CLOSE", Sprite = "assets/icons/vote_down.png", FadeIn = 2f },
                Text = { Text = "" },
                FadeOut = 2f
            }, Layer + ".alert", Layer + ".alert4");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "0 -115", OffsetMax = "300 -25" },
                Text =
                {
                    Text = text, Font = "robotocondensed-regular.ttf", FontSize = 15, Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1", FadeIn = 2
                },
                FadeOut = 2f
            }, Layer + ".alert", Layer + ".alert5");

            DestroyAlert(player);
            CuiHelper.AddUi(player, container);

            timer.In(0, () =>
            {
                if (player == null) return;
                DestroyAlert(player);
            });
        }

        private void DestroyAlert(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, Layer + ".alert1");
            CuiHelper.DestroyUi(player, Layer + ".alert2");
            CuiHelper.DestroyUi(player, Layer + ".alert3");
            CuiHelper.DestroyUi(player, Layer + ".alert4");
            CuiHelper.DestroyUi(player, Layer + ".alert5");
            CuiHelper.DestroyUi(player, Layer + ".alert");
        }

        #endregion
    }
}