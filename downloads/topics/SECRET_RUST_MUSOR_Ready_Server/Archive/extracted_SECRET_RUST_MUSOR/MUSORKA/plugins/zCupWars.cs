using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using System.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Oxide.Core;
using System.Globalization;
using Newtonsoft.Json.Linq;
using ProtoBuf;
using Rust;

namespace Oxide.Plugins
{
    //Special ver Dezz
    [Info("CupWars", "Qbis", "2.1.1")]
    public class zCupWars : RustPlugin
    {
        #region [Vars]
        [PluginReference] Plugin ImageLibrary, Clans, ZoneManager, CopyPaste;
        private List<BuildingPrivlidge> Cups = new List<BuildingPrivlidge>();
        private List<ulong> CloseUI = new List<ulong>();
        private Dictionary<string, int> ClansPoint = new Dictionary<string, int>();

        public class cupData
        {
            public string OwnerName;
            public int lastCapture;
            public Vector3 position;
            public string Name;
            public string zoneID;
        }

        private static zCupWars plugin;
        private bool newSave = false;
        #endregion

        #region [Config]
        private PluginConfig config;

        protected override void LoadDefaultConfig()
        {
            config = PluginConfig.DefaultConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();

            if (config.PluginVersion < Version)
                UpdateConfigValues();

            Config.WriteObject(config, true);
        }

        private void UpdateConfigValues()
        {
            PluginConfig baseConfig = PluginConfig.DefaultConfig();
            if (config.PluginVersion < Version)
            {
                config.PluginVersion = Version;
                if (Version == new VersionNumber(2, 0, 5))
                {
                    config.settings.distanceCapt = 30;
                }

                if(Version == new VersionNumber(2, 1, 0))
                {
                    config.settings.blockStart = 24;
                    config.settings.blockStop = 12;
                    config.settings.showTopAmount = 5;
                }

                PrintWarning("Config checked completed!");
            }
            config.PluginVersion = Version;
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        private class PluginConfig
        {
            [JsonProperty("Настройки шкафа")]
            public Settings settings;

            [JsonProperty("Настройки маркера")]
            public MarkersSettings marker;

            [JsonProperty("Config version")]
            public VersionNumber PluginVersion = new VersionNumber();

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    settings = new Settings()
                    {
                        skinID = 2679214470,
                        rewardRadius = 500,
                        captSeconds = 300,
                        captDelay = 14400,
                        gatherPrecent = 15,
                        distanceCapt = 30,
                        showTopAmount = 5,
                        blockStart = 24,
                        blockStop = 12
                    },
                    marker = new MarkersSettings()
                    {
                        markerRadius = 0.5f,
                        markerAlpha = 0.4f,
                        markerColorCanCapture = "#10c916",
                        markerColorCantCapture = "#ffb700",
                        markerColorCapture = "#ed0707"
                    },
                    PluginVersion = new VersionNumber()

                };
            }
        }

        public class Settings
        {
            [JsonProperty("skinID выдаваемоего шкафа")]
            public ulong skinID;

            [JsonProperty("Радиус в котором выдаются налоги (в метрах)")]
            public int rewardRadius;

            [JsonProperty("Сколько нужно секунд для захвата")]
            public int captSeconds;

            [JsonProperty("Откат между захватами в секундах")]
            public int captDelay;

            [JsonProperty("Процент налога")]
            public int gatherPrecent;

            [JsonProperty("Расстояние что бы засчитывался захват")]
            public int distanceCapt;

            [JsonProperty("Час с которого начинается блок на захват")]
            public int blockStart;

            [JsonProperty("Час с которого заканчивается блок на захват")]
            public int blockStop;

            [JsonProperty("Какое количество кланов выводить в топе")]
            public int showTopAmount;
        }

        public class MarkersSettings
        {
            [JsonProperty("Радиус маркера")]
            public float markerRadius;

            [JsonProperty("Прозрачность маркера")]
            public float markerAlpha;

            [JsonProperty("Цвет маркера когда можно захватить")]
            public string markerColorCanCapture;

            [JsonProperty("Цвет маркера когда идет захват")]
            public string markerColorCapture;

            [JsonProperty("Цвет маркера когда нельзя захватить")]
            public string markerColorCantCapture;
        }
        #endregion

        #region [Localization⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠]
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CantCapt"] = "Захватить можно будет через {0}ч. {1}м. {2}с.",
                ["StartCapt"] = "Клан {0} начал захват территории {1}",
                ["AlreadyCapt"] = "Территория уже захвачена вашим кланом",
                ["OtherCapt"] = "Клан {0} перехватил вашу территорию",
                ["AlreadyCapturing"] = "Ваш клан уже захватывает территорию",
                ["StopCapt"] = "Клан {0} захватил территорию {1}",
                ["UI_rightPanelText"] = "До конца захвата:\n{0}мин {1}сек",
                ["UI_rightPanelText3"] = "Захват: ({0}:{1})",
                ["UI_rightPanelText4"] = "По окончанию времени вы захватите территорию.",
                ["UI_Header"] = "Захват зон",
                ["UI_Footer"] = "Налог составляет {0}%",
                ["UI_Name"] = "<size=24>Зона:\n {0}</size>",
                ["UI_Capt"] = "<size=20>Захвачена:</size>\n {0}",
                ["UI_Free"] = "<size=20>Свободна</size>",
                ["UI_CanCapt"] = "Можно\nзахватить",
                ["UI_NextCapt"] = "До повторного захвата\n {0}ч {1}мин"
            }, this);
        }
        string GetMsg(string key, BasePlayer player = null) => lang.GetMessage(key, this, player?.UserIDString);
        string GetMsg(string key) => lang.GetMessage(key, this);
        #endregion

        #region [Oxide]
        private void OnServerInitialized()
        {
            if (newSave)
            {
                var mapsize = TerrainMeta.Size / 2;
                var pos1 = new Vector3(mapsize.x, 0, -mapsize.z);
                var pos2 = new Vector3(-mapsize.x, 0, mapsize.z);
                var pos3 = new Vector3(mapsize.x, 0, mapsize.z);
                var pos4 = new Vector3(-mapsize.x, 0, -mapsize.z);

                CreateBuildngs(pos1, "A");
                CreateBuildngs(pos2, "B");
                CreateBuildngs(pos3, "C");
                CreateBuildngs(pos4, "D");
                SaveCups();
            }
            else
                LoadCupboards();

            LoadClanPoints();


            ImageLibrary?.Call("AddImage", "https://i.imgur.com/ZCGO8gC.png", "button_close_right"); //Флаг
            ImageLibrary?.Call("AddImage", "https://cdn.discordapp.com/attachments/872535153476505623/959393449268895804/close.png", "button_close"); ;
        }

        private void Init()
        {
            plugin = this;
        }

        private void Unload()
        {
            SaveCups(true);
            SaveClanPoints();
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, "CaptMenuMain");
                CuiHelper.DestroyUi(player, "CaptTableMain");
            }
        }

        private void OnEntitySpawned(BaseLock codeLock)
        {
            if (codeLock == null)
                return;

            var parent = codeLock.GetParentEntity();
            if (parent == null)
                return;

            var cup = parent.GetBuildingPrivilege();
            if (cup == null)
                return;

            if (Cups.Contains(cup))
            {
                NextTick(() => {
                    codeLock.Kill();
                });
            }
        }

        private void OnEntityBuilt(Planner plan, GameObject go)
        {
            if (plan == null)
                return;

            var player = plan?.GetOwnerPlayer();
            if (player == null)
                return;

            var codelock = go.GetComponent<CodeLock>();
            if (codelock != null)
            {
                var parent = codelock.GetParentEntity();
                if (parent == null)
                    return;

                var cup = parent as BuildingPrivlidge;
                if (cup != null)
                {
                    foreach (var c in Cups)
                    {
                        var comp = cup.gameObject.GetComponent<CupManager>();
                        if ((bool)ZoneManager?.Call("IsPlayerInZone", comp.ZoneID, player))
                        {
                            NextTick(() =>
                            {
                                if (codelock != null)
                                    codelock.Kill();
                            });
                        }
                    }
                }

                cup = parent.GetBuildingPrivilege();
                if (cup != null)
                {
                    if (Cups.Contains(cup))
                    {
                        NextTick(() =>
                        {
                            if (codelock != null)
                                codelock.Kill();
                        });
                    }
                }
                return;
            }


            if (!player.IsAdmin)
                return;

            var priv = go.GetComponent<BuildingPrivlidge>();
            if (priv != null)
            {
                var activItem = player.GetActiveItem();

                if (activItem.skin == config.settings.skinID)
                {
                    var ent = priv as BaseEntity;
                    ent.OwnerID = 0;
                    ent.GetComponent<BaseCombatEntity>().lifestate = BaseCombatEntity.LifeState.Dead;
                    UnityEngine.Object.Destroy(ent.GetComponent<DestroyOnGroundMissing>());
                    UnityEngine.Object.Destroy(ent.GetComponent<GroundWatch>());
                    var comp = priv.gameObject.AddComponent<CupManager>();
                    string zoneID = UnityEngine.Random.Range(10000, 99999).ToString();
                    string[] zoneOptions = new string[] { "nobuild", "true", "nodecay", "true", "noentitypickup", "true", "undestr", "true", "radius", "25", "nosignupdates", "true", "nocup", "true" };
                    ZoneManager?.Call("CreateOrUpdateZone", zoneID, zoneOptions, ent.transform.position);
                    comp.Initialize("-", Facepunch.Math.Epoch.Current - config.settings.captDelay, activItem.name, GetGrid(ent.transform.position), zoneID);
                    Cups.Add(priv);
                    SaveCups();
                    player.ChatMessage($"Территория с именем {activItem.name} создана");
                }
            }
        }

        private object OnCupboardAuthorize(BuildingPrivlidge privilege, BasePlayer player)
        {
            if (privilege == null || player == null)
                return null;

            if (Cups.Contains(privilege))
            {
                var clanName = Clans?.Call<string>("GetClanOf", player.userID);
                if (clanName == null)
                    return false;


                var comp = privilege.gameObject.GetComponent<CupManager>();
                if (comp == null)
                    return null;

                foreach (var cup in Cups)
                {
                    var comp2 = privilege.gameObject.GetComponent<CupManager>();
                    if (comp2 == null)
                        continue;

                    if (comp.ownerName == clanName)
                    {
                        if (comp.isCapture && comp.captClan != clanName)
                            break;

                        return null;
                    }

                    if (comp.captClan == clanName)
                    {
                        player.ChatMessage(GetMsg("AlreadyCapturing", player));
                        return false;
                    }

                }

                if(
                    (config.settings.blockStart <= config.settings.blockStop && config.settings.blockStart <= DateTime.Now.Hour && DateTime.Now.Hour <= config.settings.blockStop) 
                    || 
                    (config.settings.blockStart > config.settings.blockStop && (config.settings.blockStart <= DateTime.Now.Hour || DateTime.Now.Hour  <= config.settings.blockStop))
                  )
                {
                    
                    player.ChatMessage($"Захват недоступен с {config.settings.blockStart} до {config.settings.blockStop} часов");
                    return false;
                }

                var auth = comp.SetNewCaptClan(clanName, player);

                if (auth)
                {
                    privilege.authorizedPlayers.Clear();
                    privilege.inventory.Clear();
                }


                return false;
            }
            return null;
        }

        private object OnCupboardDeauthorize(BuildingPrivlidge privilege, BasePlayer player)
        {
            if (privilege == null || player == null)
                return null;

            if (Cups.Contains(privilege))
            {
                return false;
            }
            return null;
        }

        private object OnCupboardClearList(BuildingPrivlidge privilege, BasePlayer player)
        {
            if (privilege == null || player == null)
                return null;

            if (Cups.Contains(privilege))
            {
                return false;
            }
            return null;
        }

        private void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (player == null || entity == null)
                return;

            var privilege = entity as BuildingPrivlidge;
            if (privilege == null)
                return;

            if (!Cups.Contains(privilege))
                return;

            var comp = privilege.GetComponent<CupManager>();
            if (comp == null)
                return;

            var clanName = Clans?.Call<string>("GetClanOf", player.userID);
            if (clanName == null)
            {
                PlayerNameID playerNameID = privilege.authorizedPlayers.FirstOrDefault(p => p.userid == player.userID);
                if (playerNameID != null)
                    privilege.authorizedPlayers.Remove(playerNameID);


                timer.Once(0.01f, player.EndLooting);
                return;
            }

            if (comp.ownerName != clanName)
            {
                PlayerNameID playerNameID = privilege.authorizedPlayers.FirstOrDefault(p => p.userid == player.userID);
                if (playerNameID != null)
                    privilege.authorizedPlayers.Remove(playerNameID);
                timer.Once(0.01f, player.EndLooting);
            }
        }

        private void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            if (entity.ToPlayer() == null) return;
            if (item == null) return;

            foreach (var cup in Cups)
            {
                if (Vector3.Distance(cup.transform.position, entity.ToPlayer().transform.position) < config.settings.rewardRadius)
                {
                    NextTick(() =>
                    {
                        var amount = Convert.ToInt32(item.amount * (config.settings.gatherPrecent / 100f));
                        if (amount <= 0)
                            return;

                        var bonusItem = ItemManager.CreateByName(item.info.shortname, amount);
                        if (bonusItem.amount <= 0)
                            return;

                        bonusItem.MoveToContainer(cup.inventory);

                    });
                }
            }

        }

        private void OnDispenserBonus(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            OnDispenserGather(dispenser, entity, item);
            if (entity.ToPlayer() == null) return;
            if (item.info.shortname == "hq.metal.ore")
            {
                return;
            }
            else
            {
                if (entity.ToPlayer().SecondsSinceAttacked > 150)
                {
                    CheckZahvatt(entity.ToPlayer());
                    entity.ToPlayer().lastAttackedTime = UnityEngine.Time.time;
                }
            }
        }

        /*object CheckZahvat(ulong id)
        {
            var player = BasePlayer.FindByID(id);
            foreach (var cup in Cups)
            {
                if (Vector3.Distance(cup.transform.position, player.transform.position) < config.settings.rewardRadius)
                {
                    var comp = cup.gameObject.GetComponent<CupManager>();
                    if (comp == null)
                        return false;
                    return comp.ownerName;
                }
            }
        }*/

        bool CheckZahvatt(BasePlayer player)
        {
            var clanName = Clans?.Call<string>("GetClanOf", player.userID);
            foreach (var cup in Cups)
            {
                if (Vector3.Distance(cup.transform.position, player.transform.position) < config.settings.rewardRadius)
                {
                    var comp = cup.gameObject.GetComponent<CupManager>();
                    if (comp == null)
                        return false;
                    string zonename = comp.cupName;
                    string status;
                    if (comp.ownerName != "-")
                    {
                        if (clanName == comp.ownerName)
                        {
                            status = "данная зона под вашим контролем";
                        }
                        else
                        {
                            status = $"данная зона под контролем клана: <color=orange>{comp.ownerName}</color>";
                        }
                    }
                    else
                    {
                        status = "данная зона свободна";
                    }
                    player.ChatMessage($"Вы фармите в зоне: {zonename}, {status}.");
                }
            }
                return false;

        }

        bool CheckZahvat(ulong owner)
        {
            var player = BasePlayer.FindByID(owner);
            var clanName = Clans?.Call<string>("GetClanOf", player.userID);
            int abc = 0;
            foreach (var cup in Cups)
            {
                if (Vector3.Distance(cup.transform.position, player.transform.position) < config.settings.rewardRadius)
                {
                    var comp = cup.gameObject.GetComponent<CupManager>();
                    if (comp == null)
                        return false;
                    if (clanName == comp.ownerName)
                    {
                        abc++;
                    }
                }
            }
            if (abc == 1)
            {
                return true;
            }
            else
            {
                return false;
            }

        }
        #endregion

        #region [Func]
        private static string GetGrid(Vector3 pos)
        {
            var letter = 'A';
            var x = Mathf.Floor((pos.x + ConVar.Server.worldsize / 2f) / 146.3f) % 26;
            var z = Mathf.Floor(ConVar.Server.worldsize / 146.3f) -
                    Mathf.Floor((pos.z + ConVar.Server.worldsize / 2f) / 146.3f);
            letter = (char)(letter + x);
            return $"{letter}{z}";
        }

        private void KillCups()
        {
            foreach (var cup in Cups)
            {
                if (cup == null)
                    continue;


                var comp = cup.gameObject.GetComponent<CupManager>();
                if (comp == null)
                    continue;

                UnityEngine.Object.Destroy(comp);
            }
        }

        const float RaycastDistance = 500f;
        const float DefaultCupboardZoneRadius = 20f;

        private void CreateBuildngs(Vector3 pos, string name = "Зона захвата")
        {
            var center = pos / 2;
            Vector3 lastPosition = center;

            for (int i = 0; i < 300; i++)
            {
                lastPosition = RandomCircle(lastPosition);
                if (ValidPosition(ref lastPosition))
                {
                    //if (CheckGroundAround(lastPosition, 5f) && CheckGroundAround(lastPosition, 10f) && CheckGroundAround(lastPosition, 15f))
                    //{
                    PrintError("Валид");
                        string[] pasteOptions = new string[] { "autoheight", "true", "stability", "false", "auth", "false", "entityowner", "false" };
                        CopyPaste?.Call("TryPasteFromVector3", lastPosition, 0f, "zahvat", pasteOptions);
                        timer.Once(10f, () =>
                        {
                            var cups = new List<BaseEntity>();
                            Vis.Entities(lastPosition, 15f, cups);
                            var cup = cups.Where(e => e.ShortPrefabName.Contains("cupboard.tool")).ToList().FirstOrDefault();
                            if(cup == null)
                            {
                                PrintError($"Шкаф не найден, найдено объектов {cups.Count}");
                                return;
                            }

                            var priv = cup.gameObject.GetComponent<BuildingPrivlidge>();
                            cup.OwnerID = 0;
                            cup.GetComponent<BaseCombatEntity>().lifestate = BaseCombatEntity.LifeState.Dead;
                            UnityEngine.Object.Destroy(cup.GetComponent<DestroyOnGroundMissing>());
                            UnityEngine.Object.Destroy(cup.GetComponent<GroundWatch>());
                            string zoneID = UnityEngine.Random.Range(10000, 99999).ToString();
                            string[] zoneOptions = new string[] { "nobuild", "true", "nodecay", "true", "noentitypickup", "true", "undestr", "true", "radius", "75", "nosignupdates", "true", "nocup", "true" };
                            ZoneManager?.Call("CreateOrUpdateZone", zoneID, zoneOptions, cup.transform.position);
                            var comp = priv.gameObject.AddComponent<CupManager>();
                            comp.Initialize("-", Facepunch.Math.Epoch.Current - config.settings.captDelay, name, GetGrid(cup.transform.position), zoneID);
                            Cups.Add(priv);
                            SaveCups();
                        });
                        break;
                    //}
                }
            }
        }



        private bool CheckGroundAround(Vector3 center, float radius)
        {
            float maxY = 4f;
            List<Vector3> positions = new List<Vector3>();
            for (int i = 1; i < 10; i++)
            {
                Vector3 pos;

                pos.x = center.x + radius * Mathf.Sin(i * 40 * Mathf.Deg2Rad);
                pos.z = center.z + radius * Mathf.Cos(i * 40 * Mathf.Deg2Rad);
                pos.y = center.y;
                pos.y = GetGroundPosition(pos);
                positions.Add(pos);
            }

            foreach (var check in positions)
                if (center.y - check.y > maxY || check.y - center.y > maxY)
                    return false;


            return true;
        }


        static float GetGroundPosition(Vector3 pos)
        {
            float y = TerrainMeta.HeightMap.GetHeight(pos);
            RaycastHit hit;
            if (Physics.Raycast(new Vector3(pos.x, pos.y + 200f, pos.z), Vector3.down, out hit, Mathf.Infinity, LayerMask.GetMask(new[] {
                "Terrain", "World", "Default", "Construction", "Deployed"
            }
            )) && !hit.collider.name.Contains("rock_cliff")) return Mathf.Max(hit.point.y, y);
            return y;
        }


        Vector3 RandomCircle(Vector3 center)
        {
            float ang = UnityEngine.Random.value * 360;
            Vector3 pos;
            pos.x = center.x + 25f * Mathf.Sin(ang * Mathf.Deg2Rad);
            pos.z = center.z + 25f * Mathf.Cos(ang * Mathf.Deg2Rad);
            pos.y = center.y;
            pos.y = GetGroundPosition(pos);
            return pos;
        }

        private bool ValidPosition(ref Vector3 randomPos)
        {
            RaycastHit hitInfo;
            if (Physics.Raycast(randomPos, Vector3.down, out hitInfo, RaycastDistance, Layers.Solid)) randomPos.y = hitInfo.point.y;
            else return false;
            if (WaterLevel.Test(randomPos)) return false;
            var colliders = new List<Collider>();
            Vis.Colliders(randomPos, 15f, colliders);
            if (colliders.Where(col => col.name.ToLower().Contains("prevent") && col.name.ToLower().Contains("building")).Count() > 0) return false;
            var entities = new List<BaseEntity>();
            Vis.Entities(randomPos, 15f, entities);
            if (entities.Where(ent => ent is BaseVehicle || ent is CargoShip || ent is BaseHelicopter || ent is BradleyAPC || ent is TreeEntity || ent is OreResourceEntity).Count() > 0) return false;
            var cupboards = new List<BuildingPrivlidge>();
            Vis.Entities(randomPos, DefaultCupboardZoneRadius + 10f, cupboards);
            if (cupboards.Count > 0) return false;
            return true;
        }
        #endregion

        #region [Data]
        private void LoadCupboards()
        {
            List<cupData> data = new List<cupData>();
            data = Interface.Oxide.DataFileSystem.ReadObject<List<cupData>>($"CupWars/data");


            foreach (var cup in data)
            {
                var cupboards = new List<BaseEntity>();
                Vis.Entities(cup.position, 10f, cupboards);

                foreach (var c in cupboards)
                {
                    if (!c.ShortPrefabName.Contains("cupboard.tool"))
                        continue;

                    var findCup = c;
                    findCup.OwnerID = 0;

                    var combat = findCup.GetComponent<BaseCombatEntity>();
                    if (combat != null)
                        combat.lifestate = BaseCombatEntity.LifeState.Dead;

                    var DGM = findCup.GetComponent<DestroyOnGroundMissing>();
                    if (DGM != null)
                        UnityEngine.Object.Destroy(DGM);

                    var GW = findCup.GetComponent<GroundWatch>();
                    if (GW != null)
                        UnityEngine.Object.Destroy(GW);
                    var bp = findCup as BuildingPrivlidge;

                    var comp = bp.gameObject.AddComponent<CupManager>();
                    comp.Initialize(cup.OwnerName, cup.lastCapture, cup.Name, GetGrid(findCup.transform.position), cup.zoneID);
                    Cups.Add(bp);
                    break;
                }
            }
        }

        private void SaveCups(bool destoyComp = false)
        {
            List<cupData> data = new List<cupData>();
            foreach (var cup in Cups)
            {
                if (cup == null)
                    continue;

                var comp = cup.gameObject.GetComponent<CupManager>();
                if (comp == null)
                    continue;

                cupData addData = new cupData();
                addData.OwnerName = comp.ownerName;
                addData.Name = comp.cupName;
                addData.lastCapture = comp.lastCapture;
                addData.position = cup.transform.position;
                addData.zoneID = comp.ZoneID;
                data.Add(addData);
            }

            Interface.Oxide.DataFileSystem.WriteObject($"CupWars/data", data);

            if (destoyComp)
                KillCups();
        }

        void LoadClanPoints()
        {
            ClansPoint = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, int>>($"CupWars/points/data");
        }

        void SaveClanPoints()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"CupWars/points/data", ClansPoint);
        }


        void OnNewSave()
        {
            Cups.Clear();
            SaveCups();
            newSave = true;
            PrintWarning("Wipe detected! Data clearing");
        }
        #endregion

        #region [Comp]
        public class CupManager : MonoBehaviour
        {
            public string ownerName;
            public int lastCapture;
            public string cupName;

            public bool isCapture;
            public string captClan;
            public int capt;

            public string ZoneID;
            private MapMarkerGenericRadius mapMarker;
            private VendingMachineMapMarker vendingMarker;

            public void DestroyComp() => OnDestroy();
            private void OnDestroy()
            {
                RemoveMarker();
                Destroy(this);
            }

            public void Initialize(string owner, int last, string name, string grid, string zoneID)
            {
                ownerName = owner;
                lastCapture = last;
                cupName = name;
                ZoneID = zoneID;

                isCapture = false;
                captClan = "";
                capt = 0;

                if (Facepunch.Math.Epoch.Current - lastCapture < plugin.config.settings.captDelay)
                    CreateMarker(plugin.config.marker.markerColorCantCapture);
                else
                    CreateMarker(plugin.config.marker.markerColorCanCapture);

                InvokeRepeating("Timer", 1f, 1f);
            }

            private void Timer()
            {
                if (isCapture)
                {
                    var clan = plugin.Clans?.Call<JObject>("GetClan", captClan);
                    if (clan == null)
                        return;

                    var members = clan.GetValue("members") as JArray;
                    foreach (var member in members)
                    {
                        var id = Convert.ToUInt64(member);
                        var player = BasePlayer.FindByID(id);
                        if (player == null)
                            continue;

                        if (!player.IsConnected)
                            continue;

                        if (Vector3.Distance(player.transform.position, transform.position) < plugin.config.settings.distanceCapt)
                        {
                            capt++;
                            break;
                        }
                    }

                    

                    if (capt >= plugin.config.settings.captSeconds)
                    {
                        StopCapture();
                    }
                    else
                    {
                        DrawUiCapture();
                    }

                }

                if (Facepunch.Math.Epoch.Current - lastCapture == plugin.config.settings.captDelay && !isCapture)
                    mapMarker.color1 = ConvertToColor(plugin.config.marker.markerColorCanCapture);

                UpdateMarker();
            }

            private void DrawUiCapture()
            {
                var clan = plugin.Clans?.Call<JObject>("GetClan", captClan);
                if (clan == null)
                    return;

                var members = clan.GetValue("members") as JArray;
                foreach (var member in members)
                {
                    var id = Convert.ToUInt64(member);

                    var player = BasePlayer.FindByID(id);
                    if (player == null)
                        continue;

                    if (player.IsConnected)
                        plugin.CreateCaptureTable(player, this);
                }
            }

            public bool SetNewCaptClan(string clan, BasePlayer cPlayer)
            {
                if (clan == captClan)
                    return true;

                if (Facepunch.Math.Epoch.Current - lastCapture < plugin.config.settings.captDelay)
                {
                    var time = TimeSpan.FromSeconds(lastCapture + plugin.config.settings.captDelay - Facepunch.Math.Epoch.Current);
                    cPlayer.ChatMessage(String.Format(plugin.GetMsg("CantCapt", cPlayer), time.Hours, time.Minutes, time.Seconds));
                    return false;
                }

                if (isCapture)
                {
                    var Clan = plugin.Clans?.Call<JObject>("GetClan", captClan);
                    if (Clan == null)
                        return false;

                    var members = Clan.GetValue("members") as JArray;
                    foreach (var member in members)
                    {
                        var id = Convert.ToUInt64(member);
                        var player = BasePlayer.FindByID(id);
                        if (player == null)
                            continue;

                        if (player.IsConnected)
                        {
                            player.ChatMessage(String.Format(plugin.GetMsg("OtherCapt", player), clan));
                            CuiHelper.DestroyUi(player, "CaptTableMain");
                        }
                    }
                }
                else
                {
                    if (clan == ownerName)
                    {
                        cPlayer.ChatMessage(plugin.GetMsg("AlreadyCapt", cPlayer));
                        return false;
                    }

                    foreach (BasePlayer player in BasePlayer.activePlayerList.ToList())
                        player.ChatMessage($"<size=20>Начался захват территории <color=#B33A00>{cupName}</color></size>\nЗахватывается кланом <color=#B33A00>{clan}</color>");
                    mapMarker.color1 = ConvertToColor(plugin.config.marker.markerColorCapture);
                }

                captClan = clan;
                capt = 0;
                isCapture = true;
                DrawUiCapture();

                return true;
            }

            private void StopCapture()
            {
                capt = 0;
                isCapture = false;

                var Clan = plugin.Clans?.Call<JObject>("GetClan", captClan);
                if (Clan == null)
                {
                    captClan = "";
                    mapMarker.color1 = ConvertToColor(plugin.config.marker.markerColorCanCapture);
                    return;
                }

                var members = Clan.GetValue("members") as JArray;
                foreach (var member in members)
                {
                    var id = Convert.ToUInt64(member);
                    var player = BasePlayer.FindByID(id);
                    if (player == null)
                        continue;


                    if (player.IsConnected)
                        CuiHelper.DestroyUi(player, "CaptTableMain");
                }

                ownerName = captClan;
                vendingMarker.markerShopName = ownerName;
                vendingMarker.SendNetworkUpdate();
                var playerOwner = ownerName;
                lastCapture = Facepunch.Math.Epoch.Current;
                plugin.Clans?.Call("AddClanPoints", playerOwner, 300);
                captClan = "";
                mapMarker.color1 = ConvertToColor(plugin.config.marker.markerColorCantCapture);
                foreach (BasePlayer player in BasePlayer.activePlayerList.ToList())
                    player.ChatMessage($"<size=20>Клан <color=#B33A00>{ownerName}</color> захватил территорию <color=#B33A00>{cupName}</color></size>");
                plugin.SaveCups();

                if (!plugin.ClansPoint.ContainsKey(ownerName))
                    plugin.ClansPoint.Add(ownerName, 1);
                else
                    plugin.ClansPoint[ownerName]++;
                plugin.SaveClanPoints();

            }

            private void UpdateMarker()
            {
                mapMarker.SendUpdate();
            }

            private void RemoveMarker()
            {
                if (mapMarker != null && !mapMarker.IsDestroyed) mapMarker.Kill();
                if (vendingMarker != null && !vendingMarker.IsDestroyed) vendingMarker.Kill();
            }

            private void CreateMarker(string color)
            {
                RemoveMarker();

                mapMarker = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", transform.position).GetComponent<MapMarkerGenericRadius>();
                vendingMarker = GameManager.server.CreateEntity("assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab", transform.position).GetComponent<VendingMachineMapMarker>();

                mapMarker.radius = plugin.config.marker.markerRadius;
                mapMarker.color1 = ConvertToColor(color);
                var c = ConvertToColor(color);
                mapMarker.alpha = plugin.config.marker.markerAlpha;
                mapMarker.enabled = true;
                mapMarker.OwnerID = 0;
                mapMarker.Spawn();
                mapMarker.SendUpdate();

                vendingMarker.markerShopName = ownerName;
                vendingMarker.OwnerID = 0;
                vendingMarker.Spawn();
                vendingMarker.enabled = false;
            }

            private Color ConvertToColor(string color)
            {
                if (color.StartsWith("#")) color = color.Substring(1);
                int red = int.Parse(color.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                int green = int.Parse(color.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                int blue = int.Parse(color.Substring(4, 2), NumberStyles.AllowHexSpecifier);
                return new Color((float)red / 255, (float)green / 255, (float)blue / 255);
            }
        }
        #endregion

        #region [Command]
        [ChatCommand("stat")]
        private void cmd_ShowTop(BasePlayer player, string c, string[] args)
        {
            string text = "<size=18>Захвачено территорий:</size>";

            int i = 1;
            foreach(var top in ClansPoint.OrderByDescending(p => p.Value).Take(config.settings.showTopAmount))
            {
                text += $"\n{i}. {top.Key} : {top.Value}";
            }

            player.ChatMessage(text);
        }

        [ChatCommand("spawnCups")]
        private void cmd_spawnCup(BasePlayer player,string c, string[] args)
        {
            if (!player.IsAdmin)
                return;

            var mapsize = TerrainMeta.Size / 2;
            var pos1 = new Vector3(mapsize.x, 0, -mapsize.z);
            var pos2 = new Vector3(-mapsize.x, 0, mapsize.z);
            var pos3 = new Vector3(mapsize.x, 0, mapsize.z);
            var pos4 = new Vector3(-mapsize.x, 0, -mapsize.z);

            CreateBuildngs(pos1, "A");
            CreateBuildngs(pos2, "B");
            CreateBuildngs(pos3, "C");
            CreateBuildngs(pos4, "D");
            SaveCups();
        }

        [ChatCommand("getCup")]
        private void cmd_GetCup(BasePlayer player, string c, string[] args)
        {
            if (!player.IsAdmin)
                return;

            if (args.Length < 1)
            {
                player.ChatMessage("/getCup НазваниеШкафа");
                return;
            }

            var item = ItemManager.CreateByName("cupboard.tool", 1, config.settings.skinID);
            item.name = args[0];
            player.GiveItem(item);
            player.ChatMessage($"Вам выдан шкаф {args[0]}");
        }

        [ChatCommand("remCup")]
        private void cmd_remCup(BasePlayer player, string c, string[] args)
        {
            if (!player.IsAdmin)
                return;

            if (args.Length < 1)
            {
                player.ChatMessage("/remCup НазваниеШкафа");
                return;
            }

            foreach (var cup in Cups)
            {
                var comp = cup.gameObject.GetComponent<CupManager>();
                if (comp == null) continue;

                if (comp.cupName.Contains(args[0]))
                {
                    ZoneManager?.Call("EraseZone", comp.ZoneID);
                    Cups.Remove(cup);
                    UnityEngine.Object.Destroy(comp);
                    cup.Kill();
                    SaveCups();
                    player.ChatMessage($"Шкаф удален");
                    return;
                }
            }

            player.ChatMessage($"Шкаф {args[0]} не найден");
        }

        [ChatCommand("radCup")]
        private void cmd_radCup(BasePlayer player, string c, string[] args)
        {
            if (!player.IsAdmin)
                return;

            if (args.Length < 1)
            {
                player.ChatMessage("/radCup НазваниеШкафа");
                return;
            }

            foreach (var cup in Cups)
            {
                var comp = cup.gameObject.GetComponent<CupManager>();
                if (comp == null) continue;

                if (comp.cupName.Contains(args[0]))
                {
                    player.SendConsoleCommand("ddraw.box", 20f, Color.red, cup.transform.position, config.settings.rewardRadius);
                    return;
                }
            }

            player.ChatMessage($"Шкаф {args[0]} не найден");
        }

        [ChatCommand("cap")]
        private void cmd_ShowCups(BasePlayer player, string c, string[] a) => CreateCaptureMenu(player);

        #endregion

        #region [UI]
        private void CreateCaptureTable(BasePlayer player, CupManager comp)
        {
            CuiElementContainer container = new CuiElementContainer();
            container.Add(new CuiElement { Parent = "Overlay", Name = "CaptTableMain", Components = { new CuiImageComponent { Color = "1 1 1 0" }, new CuiRectTransformComponent { AnchorMin = "0.85 0.48", AnchorMax = "1 0.575" } } });
            UI.CreatePanel(ref container, "imageMask", "CaptTableMain", $"1 1 1 0.2", "0.8 0.1", "1 0.9");
            CreateImage(ref container, "button_close_right", "imageMask", "1 1 1 1", "button_close_right", "0.15 0.2", "0.85 0.8");
            UI.CreateButton(ref container, "button_close_right", "0 0 0 0.7", "", 0, "0 0", "1 1", "UI_CLOSE_TABLE");
            UI.CreatePanel(ref container, "PreBar", "imageMask", $"1 1 1 1", "0 0", $"{1.0f - (float)comp.capt / (float)config.settings.captSeconds} 0.05");

            if (!CloseUI.Contains(player.userID))
            {
                UI.CreatePanel(ref container, "CaptTablePanel", "CaptTableMain", $"1 1 1 0.2", "0 0.1", "0.78 0.9");
                var time = TimeSpan.FromSeconds(config.settings.captSeconds - comp.capt);
                UI.CreateTextOutLine(ref container, "CaptTablePanel", String.Format(GetMsg("UI_rightPanelText3", player), time.Minutes, time.Seconds, comp.cupName), "1 1 1 1", $"0.02 0", $"1 0.95", TextAnchor.UpperLeft, 15);
                UI.CreateTextOutLine(ref container, "CaptTablePanel", String.Format(GetMsg("UI_rightPanelText4", player)), "1 1 1 1", $"0.02 0.15", $"1 1", TextAnchor.LowerLeft, 12);

            }


            CuiHelper.DestroyUi(player, "CaptTableMain");
            CuiHelper.AddUi(player, container);
        }

        private void CreateCaptureMenu(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiElement { Parent = "Overlay", Name = "CaptMenuMain", Components = { new CuiImageComponent { Color = "0 0 0 0.1", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" }, new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }, new CuiNeedsCursorComponent() } });

            UI.CreateButton(ref container, "CaptMenuMain", "0 0 0 0", "", 0, "0 0", "1 1", "UI_CLOSE_MENU");

            UI.CreatePanel(ref container, "CaptMenuPanel", "CaptMenuMain", $"0.1 0.1 0.1 0.98", "0.25 0.25", "0.75 0.75");

            UI.CreateTextOutLine(ref container, "CaptMenuPanel", GetMsg("UI_Header", player), "1 1 1 1", "0.009821426 0.9006174", "0.9900553 0.9900553", TextAnchor.MiddleCenter, 28);

            int i = 0;
            foreach (var cup in Cups)
            {
                var comp = cup.GetComponent<CupManager>();
                if (comp == null) continue;

                UI.CreatePanel(ref container, "CaptCup" + i, "CaptMenuPanel", $"0 0 0 0.8", $"{0.02569436 + (i * 0.25) } 0.1028278", $"{0.2196413 + (i * 0.25)} 0.8971471");

                UI.CreateTextOutLine(ref container, "CaptCup" + i, String.Format(GetMsg("UI_Name", player), comp.cupName), "1 1 1 1", "0 0.7281784", "1 1", TextAnchor.UpperCenter, 20);


                if (comp.ownerName != "-")
                {
                    UI.CreateTextOutLine(ref container, "CaptCup" + i, String.Format(GetMsg("UI_Capt", player), comp.ownerName), "1 1 1 1", "0 0.4545436", "1 0.7087604", TextAnchor.MiddleCenter, 20);

                }
                else
                {
                    UI.CreateTextOutLine(ref container, "CaptCup" + i, GetMsg("UI_Free", player), "1 1 1 1", "0 0.4545436", "1 0.7087604", TextAnchor.MiddleCenter, 20);

                }

                if (Facepunch.Math.Epoch.Current - comp.lastCapture < config.settings.captDelay)
                {
                    var time = TimeSpan.FromSeconds(comp.lastCapture + plugin.config.settings.captDelay - Facepunch.Math.Epoch.Current);
                    UI.CreateTextOutLine(ref container, "CaptCup" + i, String.Format(GetMsg("UI_NextCapt", player), time.Hours, time.Minutes), "1 1 1 1", "0 0", "1 0.4559983", TextAnchor.LowerCenter, 18);
                }
                else
                {
                    UI.CreateTextOutLine(ref container, "CaptCup" + i, GetMsg("UI_CanCapt", player), "1 1 1 1", "0 0", "1 0.4059983", TextAnchor.LowerCenter, 18);

                }
                i++;
            }

            UI.CreateTextOutLine(ref container, "CaptMenuPanel", String.Format(GetMsg("UI_Footer", player), config.settings.gatherPrecent), "1 1 1 1", "0.04017222 0.01285341", "0.9784793 0.1131105", TextAnchor.LowerCenter, 18);


            CuiHelper.DestroyUi(player, "CaptMenuMain");
            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("UI_CLOSE_TABLE")]
        private void cmd_UI_CLOSE_TABLE(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;
            if (CloseUI.Contains(player.userID))
            {
                CloseUI.Remove(player.userID);
            }
            else
            {
                CloseUI.Add(player.userID);
                CuiHelper.DestroyUi(player, "CaptTablePanel");
            }
        }

        [ConsoleCommand("UI_CLOSE_MENU")]
        private void cmd_UI_CLOSE_MENU(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;

            CuiHelper.DestroyUi(player, "CaptMenuMain");
        }

        #region [UI generator]
        public class UI
        {
            public static void CreateOutLines(ref CuiElementContainer container, string parent, string color)
            {
                CreatePanel(ref container, "Line", parent, color, "0 0", "0.001 1");
                CreatePanel(ref container, "Line", parent, color, "0 0", "1 0.001");
                CreatePanel(ref container, "Line", parent, color, "0.999 0", "1 1");
                CreatePanel(ref container, "Line", parent, color, "0 0.999", "1 1");
            }

            public static void CreateButton(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, string command, TextAnchor align = TextAnchor.MiddleCenter, string name = "button", float FadeIn = 0f)
            {

                container.Add(new CuiButton
                {

                    Button = { Color = color, Command = command, FadeIn = FadeIn },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    Text = { Text = text, FontSize = size, Align = align }

                },
                panel, name);
            }

            public static void CreatePanel(ref CuiElementContainer container, string name, string parent, string color, string aMin, string aMax, float Fadeout = 0f, float Fadein = 0f)
            {

                container.Add(new CuiElement
                {
                    Name = name,
                    Parent = parent,
                    Components =
        {
            new CuiImageComponent { Color = color, FadeIn = Fadein },
            new CuiRectTransformComponent { AnchorMin = aMin, AnchorMax = aMax}
        },
                    FadeOut = Fadeout
                });
            }

            public static void CreatePanelBlur(ref CuiElementContainer container, string name, string parent, string color, string aMin, string aMax, float Fadeout = 0f, float Fadein = 0f)
            {
                container.Add(new CuiPanel()
                {
                    CursorEnabled = true,
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    Image = { Color = color, Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat", FadeIn = Fadein },
                    FadeOut = Fadeout
                }, parent, name);
            }

            public static void CreateText(ref CuiElementContainer container, string parent, string text, string color, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleLeft, int size = 14, string name = "name", float Fadein = 0f)
            {
                container.Add(new CuiElement
                {
                    Parent = parent,
                    Name = name,
                    Components =
        {
            new CuiTextComponent(){ Color = color, Text = text, FontSize = size, Align = align, FadeIn = Fadein },
            new CuiRectTransformComponent{ AnchorMin =  aMin ,AnchorMax = aMax }
        }
                });
            }

            public static void CreateTextOutLine(ref CuiElementContainer container, string parent, string text, string color, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleLeft, int size = 14, string name = "name", float Fadein = 0f)
            {
                container.Add(new CuiElement
                {
                    Parent = parent,
                    Name = name,
                    Components =
        {
            new CuiTextComponent(){ Color = color, Text = text, FontSize = size, Align = align, FadeIn = Fadein },
            new CuiRectTransformComponent{ AnchorMin =  aMin ,AnchorMax = aMax },
            new CuiOutlineComponent{ Color = "0 0 0 1" }
        }
                });
            }
        }

        public void CreateImage(ref CuiElementContainer container, string name, string panel, string color, string image, string aMin, string aMax, float Fadeout = 0f, float Fadein = 0f, ulong skin = 0)
        {
            container.Add(new CuiElement
            {
                Name = name,
                Parent = panel,
                Components =
        {
            new CuiRawImageComponent { Color = color, Png = (string)ImageLibrary.Call("GetImage", image, skin), FadeIn = Fadein },
            new CuiRectTransformComponent { AnchorMin = aMin, AnchorMax = aMax },

        },
                FadeOut = Fadeout
            });
        }
        #endregion
        #endregion
    }
}