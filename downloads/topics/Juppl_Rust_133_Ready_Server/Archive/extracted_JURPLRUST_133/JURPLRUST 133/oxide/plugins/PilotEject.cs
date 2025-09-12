using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using UnityEngine;
using Random = UnityEngine.Random;

// Possible Future Features:
// - Pilot eject when the heli gets destroyed (monitoring OnEntityTakeDamage maybe?)
// - Option to spawn heli with no crates if the pilot has an inventory
// - Scientist?
// - Popup notifications


namespace Oxide.Plugins
{
    [Info("PilotEject", "redBDGR", "1.0.27")]
    [Description("A special helicopter event")]

    class PilotEject : RustPlugin
    {
        [PluginReference] Plugin GUIAnnouncements;
        private bool useGUIAnnouncements;

        private const string heliprefab = "assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab";
        private const string permissionName = "piloteject.admin";
        public static LayerMask collLayers = LayerMask.GetMask("Construction", "Clutter", "Deployed", "Tree", "Terrain", "World", "Water", "Default");

        private float arrowHeight = 200f;
        private float arrowLength = 120f;
        private float chanceOfOccuring = 0.20f;
        private bool changed;
        private bool inboundMessage = true;
        private bool killMSG = true;
        private float maxEventTime = 3600f;
        private float maxTimeToEvent = 600.0f;
        private float minEventTime = 1800f;
        private float minimumSpawnHeight = 20f;
        private bool minimumSpawnHeightEnabled;
        private float minTimeToEvent = 300.0f;
        private bool pilotArrow = true;
        private float pilotLifeLength = 600f;
        private string pilotName = "Helicopter Pilot";
        private bool destroyChuteOnLand = false;
        private int customCratesToDrop = 3;
        private bool randomEventEnabled = true;
        private int minHelicoptersBeforeNextEvent = 0;

        private Timer repeat;
        private Timer rp;
        private StoredData storedData;
        private DynamicConfigFile InventoryData;
        private float time;
        private bool timedEventEnabled = true;
        private bool truePVEPilotDamageable;
        private int heliCount = 0;

        private List<ItemInfo> beltContainer = new List<ItemInfo>();
        private List<ItemInfo> clothesContainer = new List<ItemInfo>();
        private List<ItemInfo> mainContiner = new List<ItemInfo>();
        public List<BaseHelicopter> helis = new List<BaseHelicopter>();
        public List<BasePlayer> pilots = new List<BasePlayer>();

        private void Loaded()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                //chat
                ["Heli Malfunctioned"] = "Вертолет неисправен! Пилоту пришлось спастись, и в настоящее время он спускается с парашютом. Найди его и укради его добычу",
                ["No Permission"] = "Вы не можете использовать эту команду!",
                ["Pilot Killed"] = "Пилот был убит {0}",
                ["Pilot Inventory Set"] = "Инвентарь пилотов успешно настроен!",
                ["Malfunction Timer Warning (Console)"] = "Вертолет выйдет из строя через {0} секунды",
                ["Broken Heli Inbound"] = "Прибывает сильно поврежденный вертолет"
            }, this);

            InventoryData = Interface.Oxide.DataFileSystem.GetFile("PilotEject");
        }

        private void OnServerInitialized()
        {
            LoadData();
            permission.RegisterPermission(permissionName, this);
        }

        private void OnServerSave()
        {
            SaveData();
        }

        private void Unload()
        {
            SaveData();
        }

        private void SaveData()
        {
            storedData.mainContiner = mainContiner;
            storedData.beltContainer = beltContainer;
            storedData.clothesContainer = clothesContainer;
            InventoryData.WriteObject(storedData);
        }

        private void LoadData()
        {
            try
            {
                storedData = InventoryData.ReadObject<StoredData>();
                mainContiner = storedData.mainContiner;
                beltContainer = storedData.beltContainer;
                clothesContainer = storedData.clothesContainer;
            }
            catch
            {
                Puts("Failed to load data, creating new file");
                storedData = new StoredData();
            }
        }

        private void LoadVariables()
        {
            minTimeToEvent = Convert.ToSingle(GetConfig("Settings", "Min Time Until Pilot Eject", 300.0f));
            maxTimeToEvent = Convert.ToSingle(GetConfig("Settings", "Max Time Until Pilot Eject", 600.0f));
            killMSG = Convert.ToBoolean(GetConfig("Settings", "Pilot Death Message Enabled", true));
            pilotLifeLength = Convert.ToSingle(GetConfig("Settings", "Pilot Life Length", 600f));
            minimumSpawnHeight = Convert.ToSingle(GetConfig("Settings", "Pilot Minimum Spawn Height", 20f));
            minimumSpawnHeightEnabled = Convert.ToBoolean(GetConfig("Settings", "Min Spawn Height Enabled", false));
            inboundMessage = Convert.ToBoolean(GetConfig("Settings", "Inbound Message", true));
            pilotName = Convert.ToString(GetConfig("Settings", "Pilot Name", "Helicopter Pilot"));
            destroyChuteOnLand = Convert.ToBoolean(GetConfig("Settings", "Destroy Chute On Land", false));
            customCratesToDrop = Convert.ToInt32(GetConfig("Settings", "Number of Crates to Spawn", 3));
            truePVEPilotDamageable = Convert.ToBoolean(GetConfig("Settings", "(TruePVP) Pilot Is Damageable", true));
            useGUIAnnouncements = Convert.ToBoolean(GetConfig("Settings", "Use GUIAnnouncements", false));

            // Arrow Settings
            pilotArrow = Convert.ToBoolean(GetConfig("Arrow Settings", "Arrow Above Pilot", true));
            arrowHeight = Convert.ToSingle(GetConfig("Arrow Settings", "Arrow Height", 200f));
            arrowLength = Convert.ToSingle(GetConfig("Arrow Settings", "Arrow Length (seconds)", 120f));

            // Timed Event Settings
            timedEventEnabled = Convert.ToBoolean(GetConfig("Timed Event Settings", "Timed Helicopter Enabled", true));
            minEventTime = Convert.ToSingle(GetConfig("Timed Event Settings", "Min Time Until Next Event", 1800f));
            maxEventTime = Convert.ToSingle(GetConfig("Timed Event Settings", "Max Time Until Next Event", 3600f));

            // Random Event Settings
            randomEventEnabled = Convert.ToBoolean(GetConfig("Random Event Settings", "Chance of helicopter being event heli (external helicopters only)", true));
            chanceOfOccuring = Convert.ToSingle(GetConfig("Random Event Settings", "Chance Of Occuring", 0.20f));
            minHelicoptersBeforeNextEvent = Convert.ToInt32(GetConfig("Random Event Settings", "Min Helicopters Between Random Event", 0));

            if (!changed) return;
            SaveConfig();
            changed = false;
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        private void Init()
        {
            LoadVariables();
            if (timedEventEnabled)
                timer.Repeat(Random.Range(minEventTime, maxEventTime), 0, CallBrokenHeli);
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity.name == heliprefab)
            {
                if (helis.Contains(entity.GetComponent<BaseHelicopter>()))
                    helis.Remove(entity.GetComponent<BaseHelicopter>());
                return;
            }
            BasePlayer pilot = entity.GetComponent<BasePlayer>();
            if (pilot == null) return;
            if (!pilots.Contains(pilot)) return;
            //if (info.Initiator.GetComponent<BasePlayer>() == null) return;
            if (info.InitiatorPlayer == null) return;
            if (killMSG)
            {
                if (useGUIAnnouncements)
                    SendGlobaGUIAnnouncement(string.Format(msg("Pilot Killed"), info.InitiatorPlayer?.displayName));
                else
                    rust.BroadcastChat(null, string.Format(msg("Pilot Killed"), info.InitiatorPlayer?.displayName));
            }
            pilots.Remove(pilot);
        }

        private void OnEntitySpawned(BaseNetworkable entity)
        {
            BaseHelicopter heli = entity.GetComponent<BaseHelicopter>();
            if (entity.name != heliprefab)
                return;
            if (heliCount != minHelicoptersBeforeNextEvent)
            {
                heliCount++;
                return;
            }
            timer.Once(0.1f, () =>
            {
                if (heli == null) return;
                if (helis.Contains(heli))
                    return;
                if (randomEventEnabled)
                {
                    float rng = Random.Range(0f, 1f);
                    Puts($"{rng.ToString()} || {chanceOfOccuring.ToString()}");
                    if (rng > chanceOfOccuring)
                        return;
                    DoEvent(entity);
                    helis.Add(heli);
                    heliCount = 0;
                    return;
                }
                DoEvent(entity);
                helis.Add(heli);
            });
        }

        private object CanEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (!truePVEPilotDamageable)
                return null;
            BasePlayer player = entity as BasePlayer;
            if (player == null) return null;
            if (pilots.Contains(player))
                return true;
            return null;
        }

        [ChatCommand("callbrokenheli")]
        private void CallBrokenHeliCMD(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionName))
            {
                player.ChatMessage(msg("No Permission", player.UserIDString));
                return;
            }
            CallBrokenHeli();
        }

        [ConsoleCommand("callbrokenheli")]
        private void CallBrokeHeliCONSOLECMD(ConsoleSystem.Arg args)
        {
            if (args.connection != null) return;
            CallBrokenHeli();
            Puts("Broken heli inbound");
        }

        [ChatCommand("setpilotinventory")]
        private void SetpilotinventoryCMD(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionName))
            {
                player.ChatMessage(msg("No Permission", player.UserIDString));
                return;
            }

            mainContiner.Clear();
            beltContainer.Clear();
            clothesContainer.Clear();

            foreach (var item in player.inventory.containerMain.itemList)
                mainContiner.Add(new ItemInfo { shortname = item.info.shortname, amount = item.amount, chance = 1f });
            foreach (var item in player.inventory.containerBelt.itemList)
                beltContainer.Add(new ItemInfo { shortname = item.info.shortname, amount = item.amount, chance = 1f });
            foreach (var item in player.inventory.containerWear.itemList)
                clothesContainer.Add(new ItemInfo { shortname = item.info.shortname, amount = item.amount, chance = 1f });

            SaveData();
            LoadData();

            player.ChatMessage(msg("Pilot Inventory Set", player.UserIDString));
        }

        private void DoEvent(BaseNetworkable entity)
        {
            var x = Random.Range(minTimeToEvent, maxTimeToEvent);
            Puts(string.Format(msg("Malfunction Timer Warning (Console)"), x));
            if (inboundMessage)
            {
                if (useGUIAnnouncements)
                    SendGlobaGUIAnnouncement(msg("Broken Heli Inbound"));
                else
                    rust.BroadcastChat(null, msg("Broken Heli Inbound"));
            }

            timer.Once(x, () =>
            {
                if (entity == null)
                    return;
                var heli = entity as BaseHelicopter;
                if (heli == null)
                    return;
                if (heli.IsDead())
                    return;

                if (useGUIAnnouncements)
                    SendGlobaGUIAnnouncement(msg("Heli Malfunctioned"));
                else
                    rust.BroadcastChat(null, msg("Heli Malfunctioned"));

                var helipos = heli.transform.position;
                heli.Hurt(heli.health - 10.0f);
                var pilot = HandlePlayer(helipos);
                var chute = CreateParachute();
                heli.Hurt(heli.health);
                heli.maxCratesToSpawn = customCratesToDrop;
                if (pilot == null)
                    PrintError("Pilot error");
                else
                {
                    pilot.displayName = pilotName;
                    pilots.Add(pilot);
                    pilot.SendNetworkUpdateImmediate();
                    AddItems(pilot);
                    chute.SetParent(pilot);
                    chute.Spawn();
                    MovePlayerDown(pilot);
                }
                timer.Once(time, () =>
                {
                    timer.Once(pilotLifeLength, () =>
                    {
                        if (pilot != null) pilot.Kill();
                    });
                    if (pilot == null)
                        return;
                    if (pilot.IsDead() || pilot.isDestroyed)
                        return;
                    pilot.Heal(100.0f);
                    pilot.StartWounded();
                    if (destroyChuteOnLand)
                        if (chute != null)
                            chute.Kill();
                    if (pilotArrow)
                        foreach (var player in BasePlayer.activePlayerList)
                            DoDraws(player, pilot.transform.position);

                    repeat = timer.Repeat(5.0f, 0, () =>
                    {
                        if (!pilot.IsDead() || !pilot.isDestroyed)
                        {
                            pilot.StopWounded();
                            pilot.Heal(100.0f);
                            pilot.StartWounded();
                        }
                        else
                        {
                            if (!chute.isDestroyed)
                                chute.Kill();
                            repeat.Destroy();
                        }
                    });
                });
            });
        }

        private void MovePlayerDown(BasePlayer player)
        {
            Vector3 ground = GetGroundPosition(player.transform.position);
            float dist = player.transform.position.y - ground.y;
            float repeattimes = dist / 0.4f;
            time = repeattimes * 0.1f;

            rp = timer.Repeat(0.1f, Convert.ToInt32(repeattimes), () =>
            {
                if (player == null || player.isDestroyed)
                {
                    rp.Destroy();
                    return;
                }
                var downpos = new Vector3(player.transform.position.x, player.transform.position.y - 0.4f, player.transform.position.z);
                player.MovePosition(downpos);

                foreach (var people in BasePlayer.activePlayerList)
                    people.SendEntityUpdate();
            });
        }

        private static Vector3 GetGroundPosition(Vector3 sourcePos)
        {
            RaycastHit hitInfo;
            if (!Physics.Raycast(sourcePos, Vector3.down, out hitInfo, collLayers)) return sourcePos;
            if (hitInfo.collider.name == "prevent_building" || hitInfo.collider.name == "prevent_building_sphere")
                sourcePos = GetGroundPosition(new Vector3(hitInfo.point.x, hitInfo.point.y - 0.5f, hitInfo.point.z));
            else
                sourcePos = hitInfo.point;
            return sourcePos;
        }

        private static BaseEntity CreateParachute()
        {
            var ent = GameManager.server.CreateEntity("assets/prefabs/misc/parachute/parachute.prefab", new Vector3(0, 0, 0));
            return ent;
        }

        private void SendGlobaGUIAnnouncement(string msg)
        {
            foreach(BasePlayer player in BasePlayer.activePlayerList)
                if (player.isConnected)
                    GUIAnnouncements?.Call("CreateAnnouncement", msg, "Grey", "White", player);
        }

        private BasePlayer HandlePlayer(Vector3 pos)
        {
            if (minimumSpawnHeightEnabled)
                if (pos.y < minimumSpawnHeight)
                    pos.y = minimumSpawnHeight;
            var ent = GameManager.server.CreateEntity("assets/prefabs/player/player.prefab", pos);
            ent.Spawn();
            return ent as BasePlayer;
        }

        private void DoDraws(BasePlayer player, Vector3 startPos)
        {
            if (player.IsAdmin())
            {
                player.SendConsoleCommand("ddraw.arrow", arrowLength, Color.red, startPos + new Vector3(0f, 25f + arrowHeight, 0f), startPos + new Vector3(0f, 25f, 0f), 4.0f);
            }
            else
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                player.SendNetworkUpdateImmediate();
                player.SendConsoleCommand("ddraw.arrow", arrowLength, Color.red, startPos + new Vector3(0f, 25f + arrowHeight, 0f), startPos + new Vector3(0f, 25f, 0f), 4.0f);
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                player.SendNetworkUpdateImmediate();
            }
        }

        private void CallBrokenHeli()
        {
            BaseEntity ent = GameManager.server.CreateEntity(heliprefab, new Vector3(Convert.ToSingle(ConVar.Server.worldsize), 50f, Convert.ToSingle(ConVar.Server.worldsize)));
            if (!ent) return;
            helis.Add((BaseHelicopter)ent);
            ent.Spawn();
            DoEvent(ent);
        }

        private void AddItems(BasePlayer player)
        {
            foreach (var item in mainContiner)
                if (Random.Range(0f, 1f) < item.chance)
                {
                    var newitem = ItemManager.CreateByName(item.shortname, item.amount);
                    if (newitem == null) continue;
                    newitem.MoveToContainer(player.inventory.containerMain);
                }
            foreach (var item in beltContainer)
                if (Random.Range(0f, 1f) < item.chance)
                {
                    var newitem = ItemManager.CreateByName(item.shortname, item.amount);
                    if (newitem == null) continue;
                    newitem.MoveToContainer(player.inventory.containerBelt);
                }
            foreach (var item in clothesContainer)
                if (Random.Range(0f, 1f) < item.chance)
                {
                    var newitem = ItemManager.CreateByName(item.shortname, item.amount);
                    if (newitem == null) continue;
                    newitem.MoveToContainer(player.inventory.containerWear);
                }
        }

        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                changed = true;
            }
            object value;
            if (data.TryGetValue(datavalue, out value)) return value;
            value = defaultValue;
            data[datavalue] = value;
            changed = true;
            return value;
        }

        private string msg(string key, string id = null)
        {
            return lang.GetMessage(key, this, id);
        }

        private class StoredData
        {
            public List<ItemInfo> beltContainer = new List<ItemInfo>();
            public List<ItemInfo> clothesContainer = new List<ItemInfo>();
            public List<ItemInfo> mainContiner = new List<ItemInfo>();
        }

        private class ItemInfo
        {
            public int amount;
            public float chance;
            public string shortname;
            public ulong skinId;
        }

        private bool IsDamagedHeli(BaseHelicopter heli)
        {
            return helis.Contains(heli);
        }
    }
}