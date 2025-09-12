using Facepunch;
using System;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using UnityEngine.AI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    //  For previous version changes, see end of file.  

    //To do
    //  Scale population to server pop @pookins.
    //  Kits pagination.
    //  Make kits in use a different colour - Easier to spot, at a glance.
    //  Make info show more info - Ui maybe? Proximity based?
    //  Map markers option.
    //  Make melee npcs should retreat if player is mounted.
    //  Detect when target is dead and loot corpse.
    //  Register permission for every profile created + ignore players with that permission @Venedas. 
    //  Add glowing eyes optional
    //  Offer GUIAnnouncements for spawn announcement.
    //  Make eokas great again. 
    //  Spawn X profile after Y kills of Z profile
    //  Add back in move/spawn/kill commands.
    //  Work on grenade aim where target is higher than npc.
    //  Make grenade and syringe limited to the number in the kit. ////////////////

    //  Option to re-read data + config from file, without reloading profiles/plugin.
    //  Make data prefix + reload option in UI.
    //  Stop heli/ch npcs attacking botrespawn npcs.
    //  Make rocket npcs fire smoke from time to time, if LOS is lost.
    //  Adjust HV rocket aim/drop
    //  Gunshot/sound senses
    //  Add Night_Profile option.


    //  Changes in V1.1.2
    //  Performance/responsiveness improvements
    //  Fixed collision for parachute npcs (arrows) @LizardMods
    //  Fixed for Rust update changes to BaseAIBrain @LizardMods
    //  Made npcs always safe from fire they created. @MooDDang
    //  NPCs will get involved in nearby fights. 
    //  Now throws c4 or satchels near players (like grenades)
    //  Fixed head worn lamps issue. @406_Gromit
    //  Added global Remove_Frankenstein_Parts option @MM617 
    //  Added DM crates to Rust_Loot_Source @damnpixel
    //  Added Global NPCs_Assist_NPCs - default true.
    //  Changed < > button colour for kits in use - Easier to see with many kits.

    //  Added per profile:
    //  XPerienceValue @beepssy & @Somescrub
    //  Immune_From_Damage_Beyond @Playerwtfa
    //  Fire_Safe (for fire the npc didn't create) @MooDDang
    //  Victim_Bleed_Amount_Per_Hit - default 1 @Covfefe
    //  Victim_Bleed_Amount_Max - default 100 @Covfefe
    //  Backpack_Duration - default 10 (minutes) @MooDDang


    [Info("BotReSpawn", "Steenamaroo", "1.1.2", ResourceId = 0)]
    [Description("Spawn tailored AI with kits at monuments, custom locations, or randomly.")]

    class BotReSpawn : RustPlugin
    {
        [PluginReference] Plugin NoSash, Kits, CustomLoot, RustRewards, XPerience, ChuteCounter;
        int halfish, no_of_AI;
        bool loaded;

        static BotReSpawn bs;
        static System.Random random = new System.Random();
        public static string Get(ulong v) => RandomUsernames.Get((int)(v % 2147483647uL));
        public static bool IsNight => bs.configData.Global.UseServerTime == true ? TOD_Sky.Instance.IsNight : TOD_Sky.Instance.Cycle.Hour > bs.configData.Global.NightStartHour || TOD_Sky.Instance.Cycle.Hour < bs.configData.Global.DayStartHour;

        int GetRand(int l, int h) => random.Next(l, h);
        bool HasPermission(string id, string perm) => permission.UserHasPermission(id, perm);

        const string permAllowed = "BotReSpawn.allowed";
        const string huff = "assets/prefabs/npc/murderer/sound/breathing.prefab";
        const string Parachute = "assets/prefabs/misc/parachute/parachute.prefab";
        const string RocketExplosion = "assets/prefabs/weapons/rocketlauncher/effects/rocket_explosion.prefab";
        const string LockedCrate = "assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab";

        ItemDefinition fuel = ItemManager.FindItemDefinition("lowgradefuel");

        public List<ulong> HumanNPCs = new List<ulong>();
        public List<Vector3> protect = new List<Vector3>();
        public Dictionary<string, Profile> Profiles = new Dictionary<string, Profile>();
        Dictionary<string, LootContainer> Containers = new Dictionary<string, LootContainer>() { { "Default NPC", null } };
        LootContainer.LootSpawnSlot[] sc;
        public Dictionary<ulong, ScientistNPC> NPCPlayers = new Dictionary<ulong, ScientistNPC>();

        #region Setup + TakeDown 
        void Init()
        {
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore, 
            };
            no_of_AI = 0;
        }

        void Loaded()
        {
            ConVar.AI.npc_families_no_hurt = false;
            lang.RegisterMessages(Messages, this);
            permission.RegisterPermission(permAllowed, this);
        }

        void Unload() 
        {
            DestroySpawnGroups();

            foreach (BasePlayer player in BasePlayer.activePlayerList)
                DestroyMenu(player, true);

            foreach (var obj in UnityEngine.Object.FindObjectsOfType(typeof(MonoBehaviour)).ToList()) 
                if (obj?.ToString() != null && obj.ToString().Contains("BotData") && !(obj is BotData))
                    UnityEngine.Object.Destroy(obj);
        }

        void OnServerInitialized()
        {
            sc = GameManager.server.FindPrefab("assets/prefabs/npc/scarecrow/scarecrow.prefab")?.GetComponent<ScarecrowNPC>()?.LootSpawnSlots;

            foreach (BasePlayer player in BaseNetworkable.serverEntities.OfType<BasePlayer>())
                ProcessHumanNPC(player);

            halfish = Convert.ToInt16((ConVar.Server.worldsize / 2) / 1.1f);
            timer.Once(1f, () =>
            {
                if (NoSash)
                {
                    PrintWarning("NoSash plugin is installed");
                    PrintWarning("Target_Noobs option is now disabled.");
                    PrintWarning("BotReSpawn NPCs will target noob players.");
                }
                bs = this;
                GetBiomePoints();
                CheckMonuments(false);
                LoadConfigVariables();
                ImportFiles();
                loaded = true;
                SaveData();
                SetupProfiles();
                CheckKits();
                SetupLootSources();
                newsave = false;
                timer.Once(0.1f, () => CreateSpawnGroups());

                foreach (BradleyAPC apc in BaseNetworkable.serverEntities.OfType<BradleyAPC>())
                    SetupAPC(apc);
            });

            // Remove in V1.1.0
            BaseEntity.saveList.RemoveWhere(p => !p);
            BaseEntity.saveList.RemoveWhere(p => p == null);
        }

        void ProcessHumanNPC(BasePlayer player)
        {
            foreach (var comp in player?.GetComponents<Component>())
                if (comp?.GetType()?.Name == "HumanPlayer")
                {
                    HumanNPCs.Add(player.userID);
                    break;
                }
        }
        #endregion

        #region Setup Methods
        void GetBiomePoints()
        {
            var trees = BaseNetworkable.serverEntities.OfType<ResourceEntity>().Where(x => x is TreeEntity || x.ShortPrefabName.Contains("cactus"));
            string biomename = "";
            List<string> names = new List<string>();
            int biome = -1;
            Vector3 point = Vector3.zero;
            foreach (var tree in trees.ToList())
            {
                biome = TerrainMeta.BiomeMap.GetBiomeMaxType(tree.transform.position, -1);
                point = CalculateGroundPos(tree.transform.position + Vector3.forward / 2f);
                if (point != Vector3.zero)
                {
                    biomename = $"Biome{Enum.GetName(typeof(TerrainBiome.Enum), biome)}";
                    if (BiomeSpawns.ContainsKey(biomename))
                        BiomeSpawns[biomename].Add(point);
                    else
                        BiomeSpawns.Add(biomename, new List<Vector3> { point });
                }
            }
        }

        void CheckMonuments(bool add)
        {
            GameObject gobject;
            Vector3 pos;
            float rot;

            foreach (var monumentInfo in TerrainMeta.Path.Monuments.OrderBy(x => x.displayPhrase.english)) 
            {
                var displayPhrase = monumentInfo.displayPhrase.english.Replace("\n", String.Empty);
                if (displayPhrase.Contains("Oil Rig") || displayPhrase.Contains("Water Well"))
                    continue;

                if (monumentInfo?.gameObject?.name != null)
                    displayPhrase = ProcessName(monumentInfo.gameObject.name, displayPhrase);

                gobject = monumentInfo.gameObject; 
                pos = gobject.transform.position;
                rot = gobject.transform.eulerAngles.y;
                int counter = 0;

                if (displayPhrase != String.Empty)
                {
                    if (add)
                    {
                        foreach (var entry in Profiles.Where(x => x.Key.Contains(displayPhrase) && x.Key.Length == displayPhrase.Length + 2))
                            counter++;
                        if (counter < 10)
                            AddProfile(gobject, $"{displayPhrase} {counter}", null, pos);
                    }
                    else
                    {
                        foreach (var entry in GotMonuments.Where(x => x.Key.Contains(displayPhrase) && x.Key.Length == displayPhrase.Length + 2))
                            counter++;
                        if (counter < 10)
                            GotMonuments.Add($"{displayPhrase} {counter}", new Profile(ProfileType.Monument));
                    }
                }
            }
        }

        List<string> numerical = new List<string>() { "mountain_", "power_sub_big_", "power_sub_small_", "ice_lake_" };
        string ProcessName(string name, string displayPhrase)
        {
            foreach (var n in numerical)
            {
                int num;
                if (name.Length > n.Length && int.TryParse(name.Substring(name.IndexOf(n) + n.Length, 1), out num))
                {
                    if (name.Contains("sub_big_"))
                        displayPhrase += "_Large";
                    if (name.Contains("sub_small_"))
                        displayPhrase += "_Small";

                    return displayPhrase + $"_{num}";
                }
            }
            if (displayPhrase == "Harbor")
            {
                if (name.Contains("_1"))
                    displayPhrase += "_Small";
                else
                    displayPhrase += "_Large";
            }
            else if (displayPhrase == "Fishing Village" || displayPhrase == "Wild Swamp")
            {
                if (name.Contains("_a"))
                    displayPhrase += "_A";
                else if (name.Contains("_b"))
                    displayPhrase += "_B";
                else
                    displayPhrase += "_C";
            }
            return displayPhrase;
        }

        void ImportFiles()
        {
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>($"BotReSpawn/{configData.DataPrefix}-CustomProfiles");
            defaultData = Interface.Oxide.DataFileSystem.ReadObject<DefaultData>($"BotReSpawn/{configData.DataPrefix}-DefaultProfiles");
            defaultData.Events = defaultData.Events.Where(x => x.Key != "CH47").ToDictionary(pair => pair.Key, pair => pair.Value);

            spawnsData = Interface.Oxide.DataFileSystem.ReadObject<SpawnsData>($"BotReSpawn/{configData.DataPrefix}-SpawnsData");
        }

        Dictionary<string, List<Vector3>> BiomeSpawns = new Dictionary<string, List<Vector3>>()
        {
            { "BiomeArid", new List<Vector3>() },
            { "BiomeTemperate", new List<Vector3>() },
            { "BiomeTundra", new List<Vector3>() },
            { "BiomeArctic", new List<Vector3>() }
        };

        private void SetupProfiles()
        {
            CheckMonuments(true);

            foreach (var biome in defaultData.Biomes)
                AddProfile(new GameObject(), biome.Key, biome.Value, new Vector3());

            foreach (var e in defaultData.Events)
                AddProfile(new GameObject(), e.Key, e.Value, new Vector3());

            foreach (var profile in storedData.Profiles)
                AddData(profile.Key, profile.Value);

            SaveData();
            SetupSpawnsFile();
            foreach (var profile in Profiles.Where(x => x.Value.type == ProfileType.Custom || x.Value.type == ProfileType.Monument))
                if (profile.Value.Spawn.Kit.Count > 0 && Kits == null)
                    PrintWarning(lang.GetMessage("nokits", this), profile.Key);
        }

        void CheckKits()
        {
            ValidKits.Clear();
            Kits?.Call("GetKitNames", new object[] { ValidKits });

            var names = Kits?.Call("GetAllKits");
            if (names != null)
                ValidKits.AddRange((string[])names);

            ValidKits = ValidKits.Distinct().ToList();
            foreach (var kit in ValidKits.ToList())
            {
                object checkKit = Kits?.CallHook("GetKitInfo", kit, true);
                bool weaponInKit = false;
                JObject kitContents = checkKit as JObject;
                if (kitContents != null)
                {
                    JArray items = kitContents["items"] as JArray;
                    foreach (var weap in items)
                    {
                        JObject item = weap as JObject;
                        if (Isweapon(item["itemid"].ToString(), null))
                        {
                            weaponInKit = true;
                            break;
                        }
                    }
                }
                if (!weaponInKit)
                    ValidKits.Remove(kit);
            }

            if (Kits)
                foreach (var profile in Profiles)
                    profile.Value.Spawn.Kit = profile.Value.Spawn.Kit.Where(x => ValidKits.Contains(x)).ToList();

            ValidKits = ValidKits.OrderBy(x => x.ToString()).ToList();
        }

        void SetupLootSources()
        {
            List<string> ignore = Pool.GetList<string>();
            ignore.AddRange("hidden,test,shelves,stocking,mission".Split(','));
            Containers.Add("ScarecrowNPC", null);
            foreach (var entry in Resources.FindObjectsOfTypeAll<LootContainer>().Where(x => x != null && !x.isSpawned).ToList())
            {
                bool skip = false;
                foreach (var i in ignore)
                    if (entry.PrefabName.Contains(i))
                        skip = true;

                if (skip)
                    continue;

                string name = GetLootName(entry.PrefabName, entry.ShortPrefabName);
                if (Containers.ContainsKey(name))
                    continue;
                Containers.Add(name, entry);
            }
            Pool.FreeList<string>(ref ignore);
        }
        #endregion

        #region Helpers
        public string GetLootName(string name, string name2) => name.Contains("underwater_labs") ? "underwater_labs_" + name2 : name2;

        public void PopulateLoot(LootContainer source, ItemContainer container)
        {
            LootContainer.LootSpawnSlot[] lootSpawnSlots = source == null ? sc : source.LootSpawnSlots;
            if (lootSpawnSlots.Length != 0)
            {
                for (int i = 0; i < lootSpawnSlots.Length; i++)
                {
                    LootContainer.LootSpawnSlot lootSpawnSlot = lootSpawnSlots[i];
                    for (int j = 0; j < lootSpawnSlot.numberToSpawn; j++)
                        if (UnityEngine.Random.Range(0f, 1f) <= lootSpawnSlot.probability)
                            lootSpawnSlot.definition.SpawnIntoContainer(container);
                }
            }
            else if (source?.lootDefinition != null)
                for (int k = 0; k < source.maxDefinitionsToSpawn; k++)
                    source.lootDefinition.SpawnIntoContainer(container);

            if (source?.SpawnType == LootContainer.spawnType.ROADSIDE || source?.SpawnType == LootContainer.spawnType.TOWN)
                foreach (Item item in container.itemList)
                    if (item.hasCondition)
                        item.condition = UnityEngine.Random.Range(item.info.condition.foundCondition.fractionMin, item.info.condition.foundCondition.fractionMax) * item.info.condition.max;
        }

        static BasePlayer FindPlayerByName(string name)
        {
            BasePlayer result = null;
            foreach (BasePlayer current in BasePlayer.activePlayerList)
            {
                if (current.displayName.Equals(name, StringComparison.OrdinalIgnoreCase)
                    || current.UserIDString.Contains(name, CompareOptions.OrdinalIgnoreCase)
                    || current.displayName.Contains(name, CompareOptions.OrdinalIgnoreCase))
                    result = current;
            }
            return result;
        }

        public bool Isweapon(string i, Item item) 
        {
            if (i != string.Empty)
                item = ItemManager.CreateByItemID(Convert.ToInt32(i), 1);
            var held = item?.GetHeldEntity();
            bool weapon = item?.info?.category == ItemCategory.Weapon || (held != null && (held as ThrownWeapon || held as BaseMelee || held as TorchWeapon));

            if (i != string.Empty && item != null)
                item.Remove();

            return weapon;
        }
        #endregion

        #region Hooks
        bool newsave = false;
        void OnNewSave(string filename) => newsave = true;

        object OnNpcKits(ulong userID) => NPCPlayers.ContainsKey(userID) ? true : (object)null;

        private object CanEntityBeHostile(ScientistNPC npc) => npc != null && NPCPlayers.ContainsKey(npc.userID) ? true : (object)null;

        private object CanBeTargeted(NPCPlayer npc, BaseEntity entity) => (npc != null && NPCPlayers.ContainsKey(npc.userID) && configData.Global.Turret_Safe) ? false : (object)null;

        private object CanBradleyApcTarget(BradleyAPC bradley, NPCPlayer npc) => npc == null ? null : NPCPlayers.ContainsKey(npc.userID) ? !configData.Global.APC_Safe : (object)null;

        private void OnEntitySpawned(BradleyAPC apc) => SetupAPC(apc);

        void SetupAPC(BradleyAPC apc) => apc.InvokeRepeating(() => UpdateTargetList(apc), 0f, 2f);

        void OnEntitySpawned(BasePlayer player) => ProcessHumanNPC(player);

        public void UpdateTargetList(BradleyAPC apc)
        {
            List<BasePlayer> list = Pool.GetList<BasePlayer>();
            Vis.Entities<BasePlayer>(apc.transform.position, apc.searchRange, list, 133120, QueryTriggerInteraction.Collide);
            foreach (var player in list)
            {
                if (!player.GetComponent<BotData>() || !apc.VisibilityTest(player))
                    continue;

                bool flag = false;
                foreach (BradleyAPC.TargetInfo targetInfo in apc.targetList)
                {
                    if (targetInfo.entity == player)
                    {
                        targetInfo.lastSeenTime = Time.time;
                        flag = true;
                        break;
                    }
                }

                if (flag)
                    continue;

                BradleyAPC.TargetInfo targetInfo1 = Pool.Get<BradleyAPC.TargetInfo>();
                targetInfo1.Setup(player, Time.time);
                apc.targetList.Add(targetInfo1);
            }
            Pool.FreeList<BasePlayer>(ref list);
        }

        object OnEntityTakeDamage(BasePlayer player, HitInfo info) 
        {
            if (player == null || info == null)
                return null;

            var bData1 = player.GetComponent<BotData>();
            if (bData1 != null)
            {
                if (info?.Initiator == player || info?.InitiatorPlayer == player)
                    return true;

                if (info.damageTypes.GetMajorityDamageType() == Rust.DamageType.Heat && bData1.profile.Other.Fire_Safe)
                    return true;

                if (info.InitiatorPlayer != null)
                {
                    if (info.InitiatorPlayer == player)
                        return true;

                    var brain = bData1.GetComponent<BaseAIBrain>();
                    if (brain != null)
                    {
                        if (info.InitiatorPlayer != bData1.CurrentTarget && Vector3.Distance(info.InitiatorPlayer.transform.position, player.transform.position) >= bData1.profile.Other.Immune_From_Damage_Beyond)
                            return true;

                        if (info.InitiatorPlayer == bData1.CurrentTarget || bData1.Targets.ContainsKey(info.InitiatorPlayer))
                            SetTarget(bData1, brain, info.InitiatorPlayer);
                        else if (bData1.WantsAttack(info.InitiatorPlayer, true))
                        {
                            SetTarget(bData1, brain, info.InitiatorPlayer);
                            if (configData.Global.NPCs_Assist_NPCs)
                            {
                                List<BotData> bDatas = Pool.GetList<BotData>();
                                Vis.Components<BotData>(player.transform.position, 30f, bDatas);
                                foreach (var bData in bDatas)
                                {
                                    brain = bData.GetComponent<BaseAIBrain>();
                                    if (brain == null || bData == bData1)
                                        continue;

                                    if (info.InitiatorPlayer != bData.CurrentTarget && Vector3.Distance(info.InitiatorPlayer.transform.position, player.transform.position) >= bData.profile.Other.Immune_From_Damage_Beyond)
                                        continue;

                                    if (info.InitiatorPlayer == bData.CurrentTarget || bData.Targets.ContainsKey(info.InitiatorPlayer) || bData.WantsAttack(info.InitiatorPlayer, true))
                                    {
                                        timer.Once(Vector3.Distance(bData.transform.position, bData1.transform.position) / 5f, () =>
                                        {
                                            if (bData?.profile != null && brain?.Senses != null && info.InitiatorPlayer != null && !info.InitiatorPlayer.IsDead())
                                                SetTarget(bData, brain, info.InitiatorPlayer);
                                        });
                                    }
                                    //else
                                    //{
                                    //    No LOS? SetDestination(...
                                    //}
                                }
                                Pool.FreeList<BotData>(ref bDatas);
                            }
                        }
                    }

                    if (info != null && bData1.profile.Other.Die_Instantly_From_Headshot && info.isHeadshot)
                    {
                        var weap = info?.Weapon?.GetItem()?.info?.shortname;
                        var weaps = bData1.profile.Other.Instant_Death_From_Headshot_Allowed_Weapons;

                        if (weaps.Count == 0 || weap != null && weaps.Contains(weap))
                        {
                            info.damageTypes.Set(0, player.health);
                            return null;
                        }
                    }
                }

                if (configData.Global.APC_Safe && info?.Initiator is BradleyAPC)
                    return true;

                if (info.damageTypes.GetMajorityDamageType() == Rust.DamageType.Heat) 
                    return null;

                if (configData.Global.Pve_Safe)
                {
                    if (info.Initiator?.ToString() == null && info.damageTypes.GetMajorityDamageType() == Rust.DamageType.Bullet)
                        return true;
                    if (info.Initiator?.ToString() == null || info.Initiator.ToString().Contains("cactus") || info.Initiator.ToString().Contains("barricade"))
                        return true;
                }
            }
            return null;
        }

        void SetTarget(BotData bData, BaseAIBrain brain, BasePlayer target)
        {
            bData.Addto(bData.Players, target);
            bData.Addto(bData.Targets, target);
            bData.CurrentTarget = target;
            brain.Navigator.SetFacingDirectionEntity(target);
        }

        void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (player == null)
                return;

            if (HumanNPCs.Contains(player.userID))
                HumanNPCs.Remove(player.userID);

            if (player?.net?.connection != null)
                DestroyMenu(player, true);

            ScientistNPC npc = player as ScientistNPC;
            if (npc != null)
                OnEntityKill(npc, info, info?.InitiatorPlayer != null);
        }

        List<uint> BSCrates = new List<uint>(); 
        object OnEntityKill(ScientistNPC npc)
        {
            if (npc == null || npc.IsDestroyed)
                return null;
            return npc?.gameObject?.name == "BotReSpawn" ? true : (object)null;
        }

        void OnEntityKill(ScientistNPC npc, HitInfo info, bool killed)
        {
            if (npc?.userID != null && NPCPlayers.ContainsKey(npc.userID) && !botInventories.ContainsKey(npc.userID))
            {
                foreach (var child in npc.children.Where(child => child.name.Contains("parachute")))
                {
                    child.SetParent(null);
                    child.Kill();
                    break;
                }

                var bData = npc.GetComponent<BotData>();
                if (bData == null || !Profiles.ContainsKey(bData?.profilename))
                    return;

                if (!bData.temporary)
                    timer.Once(Mathf.Max(1, bData.profile.Death.Respawn_Timer * 60), () => bData.sg?.SpawnBot(1, bData.profile.type == ProfileType.Biome || bData.profile.Spawn.ChangeCustomSpawnOnDeath ? null : bData.sp));

                if (killed)
                {
                    if (!info.InitiatorPlayer.IsNpc)
                    {
                        if (bData.profile.Death.RustRewardsValue > 0)
                        {
                            var weapon = info?.Weapon?.GetItem()?.info?.shortname ?? info?.WeaponPrefab?.ShortPrefabName;
                            RustRewards?.Call("GiveRustReward", info.InitiatorPlayer, 0, bData.profile.Death.RustRewardsValue, npc, weapon, Vector3.Distance(info.InitiatorPlayer.transform.position, npc.transform.position), null);
                        }
                        if (bData.profile.Death.XPerienceValue > 0)
                            XPerience?.Call("GiveXP", info.InitiatorPlayer, bData.profile.Death.XPerienceValue);
                    }
                    if (bData.profile.Death.Spawn_Hackable_Death_Crate_Percent > GetRand(1, 101) && npc.WaterFactor() < 0.1f)
                    {
                        var pos = npc.transform.position;
                        timer.Once(2f, () =>
                        {
                            if (bData?.profile == null)
                                return;
                            var Crate = GameManager.server.CreateEntity(LockedCrate, pos + new Vector3(1, 2, 0), Quaternion.Euler(0, 0, 0));
                            Crate.Spawn();
                            BSCrates.Add(Crate.net.ID);

                            (Crate as HackableLockedCrate).hackSeconds = HackableLockedCrate.requiredHackSeconds - bData.profile.Death.Death_Crate_LockDuration * 60;
                            timer.Once(1.4f, () =>
                            {
                                if (Crate == null || bData?.profile == null)
                                    return;
                                if (CustomLoot && bData.profile.Death.Death_Crate_CustomLoot_Profile != string.Empty)
                                {
                                    var container = Crate?.GetComponent<StorageContainer>();
                                    if (container != null)
                                    {
                                        container.inventory.capacity = 36;
                                        container.onlyAcceptCategory = ItemCategory.All;
                                        container.SendNetworkUpdateImmediate();
                                        container.inventory.Clear();

                                        List<Item> loot = (List<Item>)CustomLoot?.Call("MakeLoot", bData.profile.Death.Death_Crate_CustomLoot_Profile);
                                        if (loot != null)
                                            foreach (var item in loot)
                                                if (!item.MoveToContainer(container.inventory, -1, true))
                                                    item.Remove();
                                    }
                                }
                            });
                        });
                    }
                    Interface.CallHook("OnBotReSpawnNPCKilled", npc, bData.profilename, bData.group, info);
                }

                Item activeItem = npc.GetActiveItem();
                if (bData.profile.Death.Weapon_Drop_Percent >= GetRand(1, 101) && activeItem != null)
                {
                    var numb = GetRand(Mathf.Min(bData.profile.Death.Min_Weapon_Drop_Condition_Percent, bData.profile.Death.Max_Weapon_Drop_Condition_Percent), bData.profile.Death.Max_Weapon_Drop_Condition_Percent);
                    numb = Convert.ToInt16((numb / 100f) * activeItem.maxCondition);
                    activeItem.condition = numb;
                    activeItem.Drop(npc.eyes.position, new Vector3(), new Quaternion());
                    npc.svActiveItemID = 0;
                    npc.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                }
                ItemContainer[] source = { npc.inventory.containerMain, npc.inventory.containerWear, npc.inventory.containerBelt };
                Inv botInv = new Inv() { profile = bData.profile, name = npc.displayName, };
                botInventories.Add(npc.userID, botInv);
                for (int i = 0; i < source.Length; i++)
                    foreach (var item in source[i].itemList)
                    {
                        botInv.inventory[i].Add(new InvContents
                        {
                            ID = item.info.itemid,
                            amount = item.amount,
                            skinID = item.skin,
                        });
                    }

                DeadNPCPlayerIds.Add(npc.userID, bData.profile.Other.Backpack_Duration * 60); 
                no_of_AI--;
            }
        }
        #endregion

        #region Events
        void OnEntitySpawned(HackableLockedCrate crate)
        {
            NextTick(() =>
            {
                if (crate == null || !loaded || (configData.Global.Ignore_HackableCrates_With_OwnerID && crate.OwnerID != 0) || BSCrates.Contains(crate.net.ID) || Interface.CallHook("OnBotReSpawnCrateDropped", crate) != null)
                    return;
                if (configData.Global.Ignore_Parented_HackedCrates && crate.HasParent() || crate.transform?.parent != null)
                    return;
                DoEvent("LockedCrate_Spawn", crate.transform.position);
            });
        }

        void OnCrateHack(HackableLockedCrate crate)
        {
            NextTick(() =>
            {
                if (crate == null || (configData.Global.Ignore_HackableCrates_With_OwnerID && crate.OwnerID != 0))
                    return;
                if (configData.Global.Ignore_Parented_HackedCrates && crate.HasParent() || crate.transform?.parent != null)
                    return;
                if (Interface.CallHook("OnBotReSpawnCrateHackBegin", crate) != null)
                    return;
                DoEvent("LockedCrate_HackStart", crate.transform.position);
            });
        }

        void OnEntityDeath(BaseEntity entity)
        {
            if (entity?.transform?.position == null)
                return;

            string prof = string.Empty;
            if (entity is BradleyAPC)
            {
                if (Interface.CallHook("OnBotReSpawnAPCKill", entity) != null)
                    return;
                prof = "APC_Kill";
            }

            if (entity?.GetComponent<PatrolHelicopterAI>())
            {
                if (Interface.CallHook("OnBotReSpawnPatrolHeliKill", entity) != null)
                    return;
                prof = "PatrolHeli_Kill";
            }

            if (entity.GetComponent<CH47HelicopterAIController>())
            {
                if (Interface.CallHook("OnBotReSpawnCH47Kill", entity) != null)
                    return;
                prof = "CH47_Kill";
            }

            if (prof != string.Empty)
                DoEvent(prof, entity.transform.position);
        }

        void DoEvent(string name, Vector3 pos)
        {
            Profile profile = Profiles[name];
            if (profile.Spawn.AutoSpawn == true && GetPop(profile) > 0)
            {
                if (profile.Spawn.Announce_Spawn && profile.Spawn.Announcement_Text != String.Empty)
                    PrintToChat(profile.Spawn.Announcement_Text);
                int quantity = GetPop(profile);

                if (profile.Spawn.AutoSpawn == true && quantity > 0)
                {
                    profile.Other.Location = pos;
                    CreateTempSpawnGroup(pos, name, profile, string.Empty, quantity);
                }
            }
        }
        #endregion

        #region SpawningHooks
        void OnEntitySpawned(DroppedItemContainer container)
        {
            NextTick(() =>
            {
                if (!loaded || container == null || container.IsDestroyed || container.playerSteamID == 0)
                    return;

                if (DeadNPCPlayerIds.ContainsKey(container.playerSteamID))
                {
                    if (configData.Global.Remove_BackPacks_Percent >= GetRand(1, 101))
                        container.Kill();

                    container.CancelInvoke(container.RemoveMe);
                    timer.Once(DeadNPCPlayerIds[container.playerSteamID], () => container?.Kill());
                    DeadNPCPlayerIds.Remove(container.playerSteamID);
                }
            });
        }

        void OnEntitySpawned(SupplySignal signal)
        {
            if (configData.Global.Ignore_Skinned_Supply_Grenades && signal.skinID != 0)
                return;

            timer.Once(2.3f, () =>
            {
                if (!loaded || signal != null)
                    SmokeGrenades.Add(new Vector3(signal.transform.position.x, 0, signal.transform.position.z));
            });
        }

        void OnEntitySpawned(SupplyDrop drop)
        {
            if (!loaded || (!drop.name.Contains("supply_drop") && !drop.name.Contains("sleigh/presentdrop")))
                return;

            if (Interface.CallHook("OnBotReSpawnAirdrop", drop) != null)
                return;

            if (!configData.Global.Supply_Enabled)
            {
                foreach (var location in SmokeGrenades.Where(location => Vector3.Distance(location, new Vector3(drop.transform.position.x, 0, drop.transform.position.z)) < 35f))
                {
                    SmokeGrenades.Remove(location);
                    return;
                }
            }

            Profile profile = null;
            Profiles.TryGetValue("AirDrop", out profile);

            if (profile != null)
                DoEvent("AirDrop", drop.transform.position);
        }

        void OnEntitySpawned(NPCPlayerCorpse corpse)
        {
            if (!loaded || corpse == null)
                return;

            Inv botInv = new Inv();
            ulong id = corpse.playerSteamID;
            timer.Once(0.1f, () =>
            {
                if (corpse == null || corpse.IsDestroyed || !botInventories.ContainsKey(id))
                    return;

                botInv = botInventories[id];
                Profile profile = botInv.profile;

                timer.Once(profile.Death.Corpse_Duration * 60, () => { if (corpse != null && !corpse.IsDestroyed) corpse?.Kill(); });

                timer.Once(2, () => { if (profile != null) corpse?.ResetRemovalTime(profile.Death.Corpse_Duration * 60); });

                if (!(profile.Death.Allow_Rust_Loot_Percent >= GetRand(1, 101)))
                    corpse.containers[0].Clear();
                else
                {
                    if (profile.Death.Rust_Loot_Source != "Default NPC")
                    {
                        LootContainer container = null;
                        Containers.TryGetValue(profile.Death.Rust_Loot_Source, out container);
                        corpse.containers[0].Clear();
                        PopulateLoot(container, corpse.containers[0]);
                    }
                    foreach (var item in corpse.containers[0].itemList.ToList())
                        if ((item.info.shortname.Contains("keycard") && configData.Global.Remove_KeyCard) || (item.info.shortname.Contains("frankensteins.") && configData.Global.Remove_Frankenstein_Parts))
                                item.Remove();
                }

                Item playerSkull = ItemManager.CreateByName("skull.human", 1);
                playerSkull.name = string.Concat($"Skull of {botInv.name}");
                ItemAmount SkullInfo = new ItemAmount() { itemDef = playerSkull.info, amount = 1, startAmount = 1 };
                var dispenser = corpse.GetComponent<ResourceDispenser>();
                if (dispenser != null)
                {
                    dispenser.containedItems.Add(SkullInfo);
                    dispenser.Initialize();
                }

                for (int i = 0; i < botInv.inventory.Length; i++)
                    foreach (var item in botInv.inventory[i])
                    {
                        var giveItem = ItemManager.CreateByItemID(item.ID, item.amount, item.skinID);
                        if (!giveItem.MoveToContainer(corpse.containers[i], -1, true))
                            giveItem.Remove();
                    }

                timer.Once(5f, () => botInventories?.Remove(id));

                if (profile.Death.Wipe_Belt_Percent >= GetRand(1, 101))
                    corpse.containers[2].Clear();
                if (profile.Death.Wipe_Clothing_Percent >= GetRand(1, 101))
                    corpse.containers[1].Clear();
                ItemManager.DoRemoves();

                corpse._playerName = botInv.name;
                corpse.lootPanelName = botInv.name;
            });
        }
        #endregion

        #region WeaponSwitching
        void SelectWeapon(ScientistNPC npc, BotData bData, BaseAIBrain brain) 
        {
            if (bData == null || bData.throwing || bData.healing || brain?.Senses == null || npc?.inventory?.containerBelt == null)
                return;

            if (bData.CurrentTarget == null || !bData.Targets.ContainsKey(bData.CurrentTarget) || !brain.Senses.Memory.IsLOS(bData.CurrentTarget))
                bData.GetNearest();

            Range enemyrange = bData.CurrentTarget == null ? TargetRange(30) : TargetRange(Vector3.Distance(npc.transform.position, bData.CurrentTarget.transform.position));
            Range bestrange = BestRange(bData, enemyrange);
            bData.canFire = !(configData.Global.Limit_ShortRange_Weapon_Use && bestrange == Range.Close && enemyrange == Range.Long);

            if (bestrange == bData.currentRange)
            {
                SetLights(bData, npc.GetHeldEntity());
                return;
            }
            bData.currentRange = bestrange;
            UpdateActiveItem(npc, bData.Weaps[bestrange].GetRandom(), bData);
        }

        void SetLights(BotData bData, HeldEntity held)
        {
            BaseProjectile gun = held as BaseProjectile;
            if (gun != null)
                gun.SetLightsOn(bData.profile.Behaviour.AlwaysUseLights ? true : IsNight);
            if (bData.hasHeadLamp)
                HeadLampToggle(bData.npc, bData.profile.Behaviour.AlwaysUseLights ? true : IsNight);
        }

        void UpdateActiveItem(ScientistNPC npc, HeldEntity held, BotData bData)
        {
            if (held == null)
            {
                npc.CancelInvoke("SelectWeapon");
                return;
            }
            var activeItem = npc.GetHeldEntity();
            npc.svActiveItemID = 0U;
            if (activeItem != null)
                activeItem.SetHeld(false);

            npc.svActiveItemID = held.GetItem().uid;
            npc.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);

            SetRange(npc, held, bData);
            if (held != null)
                held.SetHeld(true);

            npc.inventory.UpdatedVisibleHolsteredItems();

            var cs = held as Chainsaw;
            if (cs != null)
            {
                if (cs.HasFlag(BaseEntity.Flags.On))
                    return;

                cs.SetEngineStatus(true);
                cs.SendNetworkUpdateImmediate(false);
            }
            var cs1 = held as Jackhammer;
            if (cs1 != null)
            {
                if (cs1.HasFlag(BaseEntity.Flags.On))
                    return;

                cs1.SetEngineStatus(true);
                cs1.SendNetworkUpdateImmediate(false);
            }
        }

        void SetRange(ScientistNPC npc, HeldEntity held, BotData bData)
        {
            var weapon = held as AttackEntity;
            if (bData != null && weapon != null)
                weapon.effectiveRange = bData.Weaps[Range.Melee].Contains(held) ? 2 : 410;
        }

        void HeadLampToggle(ScientistNPC npc, bool NewState)
        {
            foreach (var item in npc.inventory.containerWear.itemList.Where(item => item.info.shortname.Equals("hat.miner") || item.info.shortname.Equals("hat.candle")))
            {
                if ((NewState && !item.IsOn()) || (!NewState && item.IsOn()))
                {
                    item.SwitchOnOff(NewState);
                    npc.inventory.ServerUpdate(0f);
                    break;
                }
            }
        }
        #endregion  

        #region SetUpLocations
        public Dictionary<string, Profile> GotMonuments = new Dictionary<string, Profile>();
        public Dictionary<string, List<SpawnerInfo>> Spawners = new Dictionary<string, List<SpawnerInfo>>();

        public class SpawnerInfo
        {
            public bool Destroy = false;
            public GameObject go;
            public Profile profile;
        }

        void AddProfile(GameObject go, string name, Profile monument, Vector3 pos)
        {
            if (monument == null && defaultData.Monuments.ContainsKey(name))
                monument = defaultData.Monuments[name];
            else if (monument == null)
                monument = new Profile(ProfileType.Monument);

            Spawners[name] = new List<SpawnerInfo>() { new SpawnerInfo() { go = go, profile = monument } };
            Profiles[name] = monument;
            Profiles[name].Other.Location = pos;

            foreach (var custom in storedData.Profiles)
            {
                if (custom.Value.Other.Parent_Monument == name && storedData.MigrationDataDoNotEdit.ContainsKey(custom.Key))
                {
                    var path = storedData.MigrationDataDoNotEdit[custom.Key];
                    if (path.ParentMonument == new Vector3())
                    {
                        Puts($"Parent_Monument added for {custom.Key}. Removing any existing custom spawn points");
                        spawnsData.CustomSpawnLocations[custom.Key].Clear();
                        SaveSpawns();
                        path.ParentMonument = pos;
                        path.Offset = Spawners[name][0].go.transform.InverseTransformPoint(custom.Value.Other.Location);
                    } 
                }
                else 
                {
                    if (!storedData.MigrationDataDoNotEdit.ContainsKey(custom.Key))
                        storedData.MigrationDataDoNotEdit.Add(custom.Key, new ProfileRelocation());

                    if (custom.Value.Other.Parent_Monument == "" && storedData.MigrationDataDoNotEdit[custom.Key].ParentMonument != new Vector3())
                    {
                        Puts($"Parent_Monument removed for {custom.Key}. Removing any existing custom spawn points");
                        spawnsData.CustomSpawnLocations[custom.Key].Clear();
                        storedData.MigrationDataDoNotEdit[custom.Key] = new ProfileRelocation();
                        SaveSpawns();
                    }
                }
            }
        }

        void AddData(string name, Profile profile)
        {
            if (!storedData.MigrationDataDoNotEdit.ContainsKey(name))
                storedData.MigrationDataDoNotEdit.Add(name, new ProfileRelocation());

            var path = storedData.MigrationDataDoNotEdit[name];

            if (profile.Other.Parent_Monument != String.Empty)
            {
                if (Profiles.ContainsKey(profile.Other.Parent_Monument))
                {
                    if (path.ParentMonument != Profiles[profile.Other.Parent_Monument].Other.Location)
                    {
                        bool userChanged = false;
                        foreach (var monument in Profiles)
                            if (monument.Value.Other.Location == Profiles[profile.Other.Parent_Monument].Other.Location && monument.Key != profile.Other.Parent_Monument)
                            {
                                userChanged = true;
                                break;
                            }

                        profile.Other.Location = Spawners[profile.Other.Parent_Monument][0].go.transform.TransformPoint(path.Offset);

                        if (userChanged)
                        {
                            Puts($"Parent_Monument change detected for {name}. Removing any existing custom spawn points");
                            spawnsData.CustomSpawnLocations[name].Clear();
                            SaveSpawns();
                        }

                        path.ParentMonument = Profiles[profile.Other.Parent_Monument].Other.Location;
                        path.Offset = Spawners[profile.Other.Parent_Monument][0].go.transform.InverseTransformPoint(profile.Other.Location);
                    }
                }
                else if (profile.Spawn.AutoSpawn == true)
                    Puts($"Parent monument {profile.Other.Parent_Monument} does not exist for custom profile {name}");
            }
            else if (newsave && configData.Global.Disable_Non_Parented_Custom_Profiles_After_Wipe)
                profile.Spawn.AutoSpawn = false;

            SaveData();
            Profiles[name] = profile;
            GameObject obj = new GameObject();
            obj.transform.position = profile.Other.Location;
            List<SpawnerInfo> parent;
            Spawners.TryGetValue(profile.Other.Parent_Monument, out parent);
            if (parent?[0]?.go)
                obj.transform.rotation = parent[0].go.transform.rotation;

            Spawners[name] = new List<SpawnerInfo>() { new SpawnerInfo() { go = obj, profile = profile, Destroy = true } };
        }

        void SetupSpawnsFile()
        {
            bool flag = false;
            foreach (var entry in Profiles.Where(entry => entry.Value.type != ProfileType.Biome && entry.Value.type != ProfileType.Event))
            {
                if (!spawnsData.CustomSpawnLocations.ContainsKey(entry.Key))
                {
                    spawnsData.CustomSpawnLocations.Add(entry.Key, new List<SpawnData>());
                    flag = true;
                }

                if (entry.Value.Spawn.AutoSpawn && entry.Value.Spawn.UseCustomSpawns && spawnsData.CustomSpawnLocations[entry.Key].Count == 0)
                    PrintWarning(lang.GetMessage("nospawns", this), entry.Key);
            }
            if (flag)
                SaveSpawns();
        }
        #endregion

        #region SpawnGroups
        void DestroySpawnGroups(GameObject gameObject = null)
        {
            foreach (var entry in Spawners.ToList())
            {
                if (entry.Value == null)
                    continue;

                foreach (var go in Spawners[entry.Key].ToList())
                {
                    if (gameObject == null || go?.go == gameObject)
                    {
                        if (go?.go != null)
                        {
                            var sg = go.go.GetComponent<CustomGroup>();

                            if (sg == null)
                                continue;

                            SpawnHandler.Instance.SpawnGroups.Remove(sg);
                            UnityEngine.Object.Destroy(sg);

                            if (go.Destroy)
                                UnityEngine.Object.Destroy(go.go);
                        }
                    }
                }
            }
        }

        void RemoveTemp(GameObject gameObject = null)
        {
            foreach (var entry in Spawners.ToList())
            {
                if (entry.Value == null)
                    continue;

                foreach (var go in entry.Value)
                {
                    if (gameObject == null || go?.go == gameObject)
                    {
                        if (go?.go != null)
                        {
                            var sg = go.go.GetComponent<CustomGroup>();

                            if (sg == null)
                                continue;

                            SpawnHandler.Instance.SpawnGroups.Remove(sg);
                        }
                    }
                }
            }
        }

        void CreateSpawnGroups(string single = "")
        {
            int delay = 0;
            foreach (var entry in Profiles)
            {
                if (entry.Value.Other.Parent_Monument != string.Empty && !Spawners.ContainsKey(entry.Value.Other.Parent_Monument))
                    continue;

                if (single == string.Empty || entry.Key == single)
                {
                    if (IsSpawner(entry.Key) && entry.Value.Spawn.AutoSpawn && Mathf.Max(configData.Global.DayStartHour, configData.Global.NightStartHour) > 0)
                    {
                        delay++;
                        timer.Once(delay, () =>
                        {
                            if (!bs.Profiles.ContainsKey(entry.Key))
                                return;

                            foreach (var go in Spawners[entry.Key])
                                SetUpSpawnGroup(go.go, go.profile, entry.Key, false, 0, String.Empty);
                        });
                    }
                }
            }
        }

        void CreateTempSpawnGroup(Vector3 pos, string name, Profile profile, string group, int quantity)
        {
            GameObject gameObject = new GameObject();
            gameObject.transform.position = pos;
            if (!Spawners.ContainsKey(name))
                Spawners.Add(name, new List<SpawnerInfo>());
            Spawners[name].Add(new SpawnerInfo() { go = gameObject, profile = profile, Destroy = true });
            SetUpSpawnGroup(gameObject, profile, name, true, quantity, group);
        }

        public class SpawnInfo
        {
            public SpawnInfo(SpawnData d)
            {
                if (d == null)
                    return;
                Stationary = d.Stationary;
                Kits = d.Kits.ToArray();
                Health = d.Health;
                RoamRange = d.RoamRange;
                UseOverrides = d.UseOverrides;
            }
            public Vector3 loc;
            public float rot = 0;
            public bool Stationary;
            public string[] Kits = null;
            public int Health = 150;
            public int RoamRange = 100;
            public bool UseOverrides = false;
        }

        public void SetUpSpawnGroup(GameObject gameObject, Profile profile, string name, bool t, int q, string g)
        {
            List<SpawnInfo> Points = new List<SpawnInfo>();
            var maxPopulation = t ? q : Mathf.Max(profile.Spawn.Day_Time_Spawn_Amount, profile.Spawn.Night_Time_Spawn_Amount);

            var comp = gameObject.AddComponent<CustomGroup>();
            if (profile.Other.Use_Map_Marker)
            {
                comp.marker = (MapMarkerGenericRadius)GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", gameObject.transform.position);
                comp.marker.enableSaving = false;
                comp.marker.color1 = Color.white;
                comp.marker.color2 = Color.black;
                comp.marker.alpha = 0.9f;
                comp.marker.radius = 0.2f;
                comp.marker.Spawn();
                comp.marker.SendUpdate();
            }

            comp.maxPopulation = 0;

            if (!t && profile.type == ProfileType.Biome)
            {
                if (BiomeSpawns[name].Count < maxPopulation)
                {
                    PrintWarning($"Found {BiomeSpawns[name].Count} out of {maxPopulation} spawnpoints for {name}.");
                    if (BiomeSpawns[name].Count > 0)
                        Puts("Adust desired population and reload profile.");
                }
                else
                {
                    while (Points.Count < maxPopulation)
                        Points.Add(new SpawnInfo(null) { loc = BiomeSpawns[name].GetRandom(), rot = 0 });

                    FinaliseSpawnGroup(gameObject, profile, name, Points, t, q, g);
                }
            }
            else
            {
                if (!t && profile.Spawn.UseCustomSpawns)
                {
                    var SpawnsData = bs.spawnsData.CustomSpawnLocations[name];

                    foreach (var entry in SpawnsData)
                    {
                        var loc = entry.loc;
                        if (profile.Spawn.UseCustomSpawns)
                        {
                            var l = bs.Spawners.ContainsKey(profile.Other.Parent_Monument) ? bs.Spawners[profile.Other.Parent_Monument]?[0]?.go.transform : bs.Spawners.ContainsKey(name) ? bs.Spawners[name]?[0]?.go.transform : null;
                            if (l != null)
                                loc = l.transform.TransformPoint(loc);
                        }
                        Points.Add(new SpawnInfo(entry.Kits == null ? null : entry) { loc = loc, rot = entry.rot });
                    }
                }

                if (Points.Count < maxPopulation)
                {
                    int safety = 0;
                    while (Points.Count() < maxPopulation)
                    {
                        safety++;
                        if (safety > maxPopulation * 2)
                        {
                            if (profile.type != ProfileType.Event)
                                bs.Puts($"FAILED TO GET ENOUGH SPAWN POINTS FOR PROFILE {name}.");
                            return;
                        }
                        Vector3 loc = TryGetSpawn(gameObject.transform.position, profile.Spawn.Radius);
                        if (loc != Vector3.zero)
                            Points.Add(new SpawnInfo(null) { loc = loc, rot = 0f });
                    }
                }
            }

            gameObject.transform.position = profile.Other.Location;
            if (profile.type != ProfileType.Biome && Points.Count >= maxPopulation)
                FinaliseSpawnGroup(gameObject, profile, name, Points, t, q, g);
        }

        void FinaliseSpawnGroup(GameObject gameObject, Profile profile, string name, List<SpawnInfo> Points, bool t, int q, string g)
        {
            int counter = 0;
            List<GameObject> sps = new List<GameObject>();
            foreach (var entry in Points)
            {
                var NewGO = new GameObject();
                sps.Add(NewGO);
                NewGO.transform.SetParent(gameObject.transform);
                NewSpawnPoint point = NewGO.AddComponent<NewSpawnPoint>();
                var SO = NewGO.AddComponent<SpawnOverride>();
                SO.Point = entry;
                SO.Initialize();

                point.location = entry.loc;
                point.rotation = Quaternion.Euler(0, entry.rot, 0);
                point.dropToGround = false;
                counter++;
            }
            gameObject.GetComponent<CustomGroup>().Setup(profile, name, t, q, g, sps);
        }

        public class SpawnOverride : MonoBehaviour
        {
            public SpawnInfo Point;

            public void Initialize()
            {
                Stationary = Point.Stationary;
                RoamRange = Point.RoamRange;
                Kits = Point.Kits;
                Health = Point.Health;
                UseOverrides = Point.UseOverrides;
            }
            public bool Stationary;
            public int RoamRange;
            public string[] Kits;
            public int Health;
            public bool UseOverrides;
        }

        static int GetPop(Profile p) => IsNight ? p.Spawn.Night_Time_Spawn_Amount : p.Spawn.Day_Time_Spawn_Amount;

        public class CustomGroup : SpawnGroup
        {
            public MapMarkerGenericRadius marker;
            bool night = IsNight;
            void CheckNight()
            {
                if (temporary)
                    return;

                if (marker != null && currentPopulation == 0 && !profile.Other.Always_Show_Map_Marker)
                {
                    marker.alpha = 0f;
                    marker.SendUpdate();
                }

                if (night != IsNight)
                {
                    maxPopulation = GetPop(profile);
                    night = IsNight;
                    if (night && profile.Spawn.Night_Time_Spawn_Amount > profile.Spawn.Day_Time_Spawn_Amount)
                        Spawn(profile.Spawn.Night_Time_Spawn_Amount - profile.Spawn.Day_Time_Spawn_Amount);
                    else if (!night && profile.Spawn.Day_Time_Spawn_Amount > profile.Spawn.Night_Time_Spawn_Amount)
                        Spawn(profile.Spawn.Day_Time_Spawn_Amount - profile.Spawn.Night_Time_Spawn_Amount);
                }
            }

            List<GameObject> SpawnPoints = new List<GameObject>();
            public Profile profile;
            public string profilename = string.Empty, group = string.Empty;

            public bool ready = false;

            public void Setup(Profile p, string n, bool t, int q, string g, List<GameObject> sps)
            {
                SpawnPoints = sps;
                spawnPoints = GetComponentsInChildren<BaseSpawnPoint>();
                name = temporary ? gameObject.GetInstanceID().ToString() : name;
                group = g;
                temporary = t;
                profile = p;
                profilename = n;
                maxPopulation = t ? q : GetPop(p);

                if (!t && profile.Spawn.Day_Time_Spawn_Amount != profile.Spawn.Night_Time_Spawn_Amount)
                    InvokeRepeating("CheckNight", random.Next(10, 20), random.Next(10, 20));

                numToSpawnPerTickMax = 0;
                numToSpawnPerTickMin = 0;
                respawnDelayMax = float.MaxValue;
                respawnDelayMin = float.MaxValue;
                wantsInitialSpawn = true;

                prefabs = new List<SpawnEntry>();
                prefabs.Add(new SpawnEntry() { prefab = new GameObjectRef { guid = "adb1626eb0a3ab747aa5345479befccf" } });

                enabled = true;
                gameObject.SetActive(true);
                Fill();
            }

            Vector3 ProcessPoint(Vector3 pos, bool chute, bool airdrop, bool stationary)
            {
                if (!stationary || (stationary && chute))
                {
                    NavMeshHit hit;
                    if (!NavMesh.SamplePosition(pos, out hit, 5, -1))
                        return Vector3.zero;
                }
                if (chute)
                {
                    pos.y = airdrop ? (pos.y = gameObject.transform.position.y - 40f) : 200f;
                    if (!airdrop)
                        pos += new Vector3(random.Next(-50, 0), 0, random.Next(-50, 0));
                }
                return pos;
            }

            public void SpawnBot(int num, NewSpawnPoint sp)
            {
                repoint = sp;
                Spawn(1);
                repoint = null;
            }

            NewSpawnPoint repoint = null;
            protected override void Spawn(int numToSpawn)
            {
                if (profile != null && !temporary)
                    maxPopulation = GetPop(profile);

                Vector3 vector3;
                Quaternion quaternion;
                numToSpawn = Mathf.Min(numToSpawn, maxPopulation - currentPopulation);

                for (int i = 0; i < numToSpawn; i++)
                {
                    GameObjectRef prefab = GetPrefab();
                    if (prefab != null && !string.IsNullOrEmpty(prefab.guid))
                    {
                        NewSpawnPoint spawnpoint = (NewSpawnPoint)GetSpawnPoint(prefab, out vector3, out quaternion);
                        if (spawnpoint)
                        {
                            var or = spawnpoint.gameObject.GetComponent<SpawnOverride>();
                            bool UseOR = or != null && or.UseOverrides;
                            spawnpoint.transform.position = vector3;
                            var point = ProcessPoint(vector3, profile.Other.Chute, profilename == "AirDrop", UseOR ? or.Stationary : profile.Spawn.Stationary);
                            if (point == Vector3.zero)
                                continue;

                            var npc = (global::HumanNPC)GameManager.server.CreateEntity(prefab.resourcePath, point, quaternion, false);
                            if (npc)
                            {
                                npc.gameObject.AwakeFromInstantiate();
                                string name = npc.gameObject.name;
                                npc.gameObject.name = "BotReSpawn";
                                bs.NextTick(() => npc.gameObject.name = name);
                                npc.Spawn();

                                if (marker != null)
                                {
                                    marker.alpha = 0.9f;
                                    marker.SendUpdate();
                                }

                                PostSpawnProcess(npc, spawnpoint);
                                SpawnPointInstance ins = npc.gameObject.AddComponent<SpawnPointInstance>();
                                ins.parentSpawnPointUser = this;
                                ins.parentSpawnPoint = spawnpoint;
                                ins.Notify();

                                npc.eyes.rotation = Quaternion.Euler(0, bs.Spawners[profilename][0].go.transform.rotation.eulerAngles.y + spawnpoint.rotation.eulerAngles.y, 0);
                                npc.viewAngles = npc.eyes.rotation.eulerAngles;
                                npc.ServerRotation = npc.eyes.rotation;
                            }
                        }
                    }
                }
            }

            protected override BaseSpawnPoint GetSpawnPoint(GameObjectRef prefabRef, out Vector3 pos, out Quaternion rot)
            {
                spawnPoints = spawnPoints.Where(x => x.GetType() == typeof(NewSpawnPoint)).ToArray();
                BaseSpawnPoint baseSpawnPoint = null;
                pos = Vector3.zero;
                rot = Quaternion.identity;
                int num = UnityEngine.Random.Range(0, (int)spawnPoints.Length);
                int num1 = 0;
                if (repoint != null && repoint.IsAvailableTo(prefabRef))
                    baseSpawnPoint = repoint;
                else
                {
                    if (profile.type == ProfileType.Biome)
                    {
                        var available = spawnPoints.Where(x => x != null && x.IsAvailableTo(prefabRef)).ToList();
                        baseSpawnPoint = available.GetRandom();
                    }
                    else
                    {
                        while (num1 < (int)spawnPoints.Length)
                        {
                            BaseSpawnPoint baseSpawnPoint1 = this.spawnPoints[(num + num1) % (int)spawnPoints.Length];
                            if (baseSpawnPoint1 == null || !baseSpawnPoint1.IsAvailableTo(prefabRef))
                                num1++;
                            else
                            {
                                baseSpawnPoint = baseSpawnPoint1;
                                break;
                            }
                        }
                    }
                }
                if (baseSpawnPoint)
                    baseSpawnPoint.GetLocation(out pos, out rot);

                return baseSpawnPoint;
            }

            protected override void PostSpawnProcess(BaseEntity entity, BaseSpawnPoint spawnPoint)
            {
                if (!entity || !spawnPoint)
                    return;

                base.PostSpawnProcess(entity, spawnPoint);

                var npc = entity as ScientistNPC;
                if (npc == null)
                    return;
                var nav = npc.GetComponent<BaseNavigator>();
                if (nav == null)
                    return;

                npc.NavAgent.enabled = false;
                nav.CanUseNavMesh = false;
                nav.DefaultArea = "Walkable";
                npc.NavAgent.areaMask = 1;
                npc.NavAgent.agentTypeID = -1372625422;
                npc.NavAgent.autoTraverseOffMeshLink = true;
                npc.NavAgent.autoRepath = true;
                nav.CanUseCustomNav = true;
                npc.NavAgent.baseOffset = -0.1f;

                var bData = npc.gameObject.AddComponent<BotData>();
                bData.temporary = temporary;
                bData.profile = profile;
                bData.profilename = profilename;

                if (npc?.Brain?.Senses != null)
                    npc.Brain.Senses.nextKnownPlayersLOSUpdateTime = Time.time * Time.time;

                if (!bs.NPCPlayers.ContainsKey(npc.userID))
                    bs.NPCPlayers.Add(npc.userID, npc);
                else
                {
                    bs.timer.Once(0.1f, () => npc?.Kill());
                    return;
                }

                bs.timer.Once(1.0f, () =>
                {
                    if (npc == null || nav == null || bData?.profile == null)
                    {
                        if (npc != null && !npc.IsDestroyed)
                            npc.Kill();
                        return;
                    }
                    bs.no_of_AI++;
                    npc.EnablePlayerCollider();
                    var brain = npc.GetComponent<BaseAIBrain>();
                    var or = spawnPoint.gameObject.GetComponent<SpawnOverride>();

                    if (or == null || brain == null)
                    {
                        npc.Kill();
                        return;
                    }
                    brain.UseAIDesign = false;
                    bool UseOR = or != null && or.UseOverrides;
                    bData.sp = (NewSpawnPoint)spawnPoint;
                    npc.Brain.Senses.nextUpdateTime = float.MaxValue;
                    npc.Brain.Senses.nextKnownPlayersLOSUpdateTime = float.MaxValue;
                    bData.profilename = profilename;
                    bData.stationary = UseOR ? or.Stationary : profile.Spawn.Stationary;
                    bData.group = group;
                    bData.sg = this;
                    brain.AllowedToSleep = false; 
                    brain.sleeping = false;

                    if (brain.Events == null)
                    {
                        npc.Kill();
                        return;
                    }

                    npc.enableSaving = false;
                    if (!temporary && profile.Spawn.Announce_Spawn && profile.Spawn.Announcement_Text != String.Empty)
                        bs.PrintToChat(profile.Spawn.Announcement_Text);
                    if (temporary && currentPopulation == maxPopulation)
                        bs.RemoveTemp(gameObject);
                    if (!bData.stationary && !profile.Other.Chute)
                    {
                        brain.GetComponent<BaseNavigator>().CanUseNavMesh = true;
                        npc.NavAgent.enabled = true; ;
                    }

                    nav.Init(npc, npc.NavAgent);
                    brain.HostileTargetsOnly = bData.profile.Behaviour.Peace_Keeper; 
                    brain.IgnoreSafeZonePlayers = true;
                    brain.SenseRange = 400;
                    brain.Senses.Init(npc, bs.configData.Global.Deaggro_Memory_Duration, 400, 400, brain.VisionCone, brain.CheckVisionCone, brain.CheckLOS, brain.IgnoreNonVisionSneakers, brain.ListenRange, bData.profile.Behaviour.Peace_Keeper, brain.MaxGroupSize > 0, brain.IgnoreSafeZonePlayers, brain.SenseTypes, true);

                    npc._maxHealth = UseOR ? or.Health : bData.profile.Spawn.BotHealth;
                    npc.startHealth = npc._maxHealth;
                    npc.InitializeHealth(npc._maxHealth, npc._maxHealth);

                    if (profile.Other.Chute) 
                        bs.AddChute(npc, bData.sp.transform.position);

                    List<string> kits = UseOR ? or.Kits.ToList() : profile.Spawn.Kit;

                    if (kits.Count > 0 && kits.Count() == profile.Spawn.BotNames.Count())
                    {
                        string kit = kits.GetRandom();
                        bs.GiveKit(npc, kit);
                        bData.kit = kit;
                        bs.SetName(profile, npc, profile.Spawn.Kit.IndexOf(kit));
                    }
                    else 
                    {
                        bData.kit = kits.GetRandom(); 
                        bs.GiveKit(npc, bData.kit);
                        bs.SetName(profile, npc, -1);
                    }

                    bs.SortWeapons(npc, bData);
                    
                    SetupStates(brain, bData);

                    if (temporary)
                    {
                        int suicInt = random.Next(profile.Other.Suicide_Timer * 60, (profile.Other.Suicide_Timer * 60) + 10);
                        bs.RunSuicide(npc, suicInt);
                    }

                    if (bData.profile.Other.Disable_Radio == true)
                    {
                        npc.radioChatterType = ScientistNPC.RadioChatterType.NONE;
                        npc.DeathEffects = new GameObjectRef[0];
                        npc.RadioChatterEffects = new GameObjectRef[0];
                    }

                    Interface.CallHook("OnBotReSpawnNPCSpawned", npc, profilename, group);
                });
            }

            private void OnDestroy()
            {
                CancelInvoke("CheckNight"); 

                for (int i = spawnInstances.Count - 1; i >= 0; i--)
                {
                    SpawnPointInstance item = spawnInstances[i];
                    if (item == null || item.gameObject == null)
                        continue;
                    BaseEntity baseEntity = item.gameObject.ToBaseEntity();
                    if (baseEntity?.transform?.position == null || setFreeIfMovedBeyond != null && !setFreeIfMovedBeyond.bounds.Contains(baseEntity.transform.position))
                    {
                        item?.Retire();
                    }
                    else if (baseEntity)
                        baseEntity.Kill(BaseNetworkable.DestroyMode.None);
                }
                spawnInstances.Clear();

                if (marker != null)
                    marker.Kill();
                foreach (var sp in SpawnPoints)
                    if (sp != null)
                        Destroy(sp);
            }
        }

        public class NewSpawnPoint : GenericSpawnPoint
        {
            public Vector3 location = new Vector3();
            public Quaternion rotation = new Quaternion();
            public override void ObjectSpawned(SpawnPointInstance instance)
            {
                OnObjectSpawnedEvent.Invoke();
                gameObject.SetActive(false);
            }

            public override void GetLocation(out Vector3 pos, out Quaternion rot)
            {
                pos = location;
                rot = rotation;
            }
        }
        #endregion

        #region BrainStates
        static void SetupStates(BaseAIBrain brain, BotData bData)
        {
            brain.Navigator.MaxRoamDistanceFromHome = bData.profile.Behaviour.Roam_Range;
            brain.Navigator.BestRoamPointMaxDistance = bData.profile.Behaviour.Roam_Range;
            brain.Navigator.BestMovementPointMaxDistance = bData.profile.Behaviour.Roam_Range;
            brain.Navigator.FastSpeedFraction = bData.profile.Behaviour.Running_Speed_Booster / 10f;
            brain.Events.Memory.Position.Set(bData.sp.transform.position, 4);
            brain.MemoryDuration = bs.configData.Global.Deaggro_Memory_Duration;

            if (brain != null && bData != null)
            {
                ClearBrain(brain);
                bs.timer.Once(5f, () => { if (brain != null) ClearBrain(brain); });
            }
        }

        static void ClearBrain(BaseAIBrain brain)
        {
            for (int i = 0; i < brain.Events.events.Count(); i++)
                if (brain.Events.events[i].EventType == AIEventType.AttackTick || brain.Events.events[i].EventType == AIEventType.BestTargetDetected)
                {
                    brain.Events.events.RemoveAt(i);
                    brain.Events.Memory.Entity.Set(null, 0);
                    i--;
                }
        }
        #endregion

        #region PosHelpers
        NavMeshHit navMeshHit;
        public bool HasNav(Vector3 pos) => NavMesh.SamplePosition(pos, out navMeshHit, 2, 1);

        public static Vector3 CalculateGroundPos(Vector3 pos)
        {
            pos.y = TerrainMeta.HeightMap.GetHeight(pos);
            NavMeshHit navMeshHit;

            if (!NavMesh.SamplePosition(pos, out navMeshHit, 2, 1))
                pos = Vector3.zero;
            else if (WaterLevel.GetWaterDepth(pos, true) > 0)
                pos = Vector3.zero;
            else if (Physics.RaycastAll(navMeshHit.position + new Vector3(0, 100, 0), Vector3.down, 99f, 1235288065).Any())
                pos = Vector3.zero;
            else
                pos = navMeshHit.position;
            return pos;
        }

        Vector3 TryGetSpawn(Vector3 pos, int radius)
        {
            int attempts = 0;
            var spawnPoint = Vector3.zero;
            Vector2 rand;

            while (attempts < 50 && spawnPoint == Vector3.zero)
            {
                attempts++;
                rand = UnityEngine.Random.insideUnitCircle * radius;
                spawnPoint = CalculateGroundPos(pos + new Vector3(rand.x, 0, rand.y));
                if (spawnPoint != Vector3.zero)
                    return spawnPoint;
            }
            return spawnPoint;
        }
        #endregion

        #region BotSetup
        string aiheader = "CAEIAggDCAUIEggECAYIEwgUCBUIFggNEj0IABABGhkIABACGAAgACgAMACiBgoNAAAAABUAAIA";
        void AddChute(ScientistNPC npc, Vector3 loc)
        {
            float fall = random.Next(60, Mathf.Min(100, configData.Global.Max_Chute_Fall_Speed) + 60) / 20f;
            var rb = npc.gameObject.GetComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.useGravity = false;
            rb.drag = 0f;
            npc.gameObject.layer = 0;
            rb.velocity = (loc - npc.transform.position).normalized * fall;

            var Chute = GameManager.server.CreateEntity(Parachute, npc.transform.position, Quaternion.Euler(0, 0, 0));
            Chute.gameObject.Identity();
            Chute.SetParent(npc);
            Chute.transform.localPosition += new Vector3(0, 1.3f, 0);
            Chute.Spawn();
            ChuteCounter?.Call("AddChute", new object[] { Chute }); 
            Chute.enableSaving = false;
        }

        void SetName(Profile p, global::HumanNPC npc, int num)
        {
            if (p.Spawn.BotNames.Count == 0)
            {
                npc.displayName = Get(npc.userID);
                npc.displayName = char.ToUpper(npc.displayName[0]) + npc.displayName.Substring(1);
            }
            else
            {
                if (num != -1 && p.Spawn.BotNames.Count > num)
                    npc.displayName = p.Spawn.BotNames[num];
                else
                    npc.displayName = p.Spawn.BotNames.GetRandom();
            }

            if (p.Spawn.BotNamePrefix != String.Empty)
                npc.displayName = p.Spawn.BotNamePrefix + " " + npc.displayName;
        }

        void GiveKit(ScientistNPC npc, string kit)
        {
            if (npc?.inventory?.containerBelt == null)
                return;

            var bData = npc.GetComponent<BotData>();
            if (bData != null && !String.IsNullOrEmpty(kit))
            {
                if (Kits && bData.profile.Spawn.Keep_Default_Loadout == false)
                    npc?.inventory?.Strip();

                Kits?.Call($"GiveKit", npc, kit, true);

                NextTick(() =>
                {
                    if (bData?.profile != null && npc?.inventory?.containerMain?.itemList != null)
                        if (bData.profile.Death.Wipe_Main_Percent >= GetRand(1, 101))
                        {
                            npc.inventory.containerMain.Clear();
                            ItemManager.DoRemoves();
                        }
                });
            }
        }

        void SortWeapons(ScientistNPC npc, BotData bData)
        {
            foreach (var attire in npc.inventory.containerWear.itemList.Where(attire => attire.info.shortname.Equals("hat.miner") || attire.info.shortname.Equals("hat.candle")))
            {
                bData.hasHeadLamp = true;
                Item newItem = ItemManager.Create(fuel, 1);
                attire.contents.Clear();
                ItemManager.DoRemoves();
                if (!newItem.MoveToContainer(attire.contents))
                    newItem.Remove();
                else
                {
                    npc.SendNetworkUpdateImmediate();
                    npc.inventory.ServerUpdate(0f);
                }
            }

            for (int i = 0; i < 4; i++)
                bData.Weaps.Add((Range)i, new List<HeldEntity>());

            bool flag = false;
            foreach (Item item in npc.inventory.containerBelt.itemList)
            {
                var held = item.GetHeldEntity() as HeldEntity;

                if (!Isweapon(string.Empty, item))
                {
                    MedicalTool med = held as MedicalTool;
                    if (med != null)
                        bData.meds.Add(med);
                    else
                        item.Remove();
                    continue;
                }

                if (held is ThrownWeapon)
                {
                    bData.throwables.Add(held);
                    continue;
                }

                //var lw = held as LiquidWeapon;
                //if (lw != null)
                //{
                //    lw.AutoPump = true;
                //    lw.RequiresPumping = false;
                //}

                var gun = held as BaseProjectile;
                if (held as BaseMelee != null || held as TorchWeapon != null)
                    bData.Weaps[Range.Melee].Add(held);
                else if (held as FlameThrower)
                {
                    NextTick(() =>
                    {
                        if (bData?.Weaps == null || held == null)
                            return;
                        if (bData.Weaps[Range.Melee].Count == 0)
                            bData.Weaps[Range.Close].Add(held);

                        SetMelee(bData);
                    });
                }
                else if (gun != null)
                {
                    if (gun.ShortPrefabName == "smg.entity")
                    {
                        gun.attackLengthMin = 0.1f;
                        gun.attackLengthMax = 0.5f;
                    }
                    gun.primaryMagazine.contents = gun.primaryMagazine.capacity;
                    if (held.ShortPrefabName.Contains("pistol") || held.ShortPrefabName.Contains("shotgun") || held.ShortPrefabName.Contains("bow"))
                        bData.Weaps[Range.Close].Add(held);
                    else if (held.name.Contains("bolt") || held.name.Contains("l96"))
                        bData.Weaps[Range.Long].Add(held);
                    else bData.Weaps[Range.Mid].Add(held);
                }
                else
                    bData.Weaps[Range.Mid].Add(held);
                flag = true;
                SetMelee(bData);
                if (bData.melee && bData.profile.Other.MurdererSound)
                {
                    Timer huffTimer = timer.Once(1f, () => { });
                    huffTimer = timer.Repeat(8f, 0, () =>
                    {
                        if (npc != null)
                        {
                            if (bData?.CurrentTarget != null)
                                Effect.server.Run(huff, npc, StringPool.Get("head"), Vector3.zero, Vector3.zero, null, false);
                        }
                        else
                            huffTimer?.Destroy();
                    });
                }
            }

            if (!flag)
            {
                PrintWarning(lang.GetMessage("noWeapon", this), bData.profilename, bData.kit);
                bData.noweapon = true;
                return;
            }
            npc.CancelInvoke(npc.EquipTest);
        }

        void SetMelee(BotData bData) => bData.melee = bData.Weaps[Range.Melee].Count > 0 && bData.Weaps[Range.Close].Count == 0 && bData.Weaps[Range.Mid].Count == 0 && bData.Weaps[Range.Long].Count == 0;
        void RunSuicide(ScientistNPC npc, int suicInt)
        {
            if (!NPCPlayers.ContainsKey(npc.userID))
                return;
            timer.Once(suicInt, () =>
            {
                if (npc == null)
                    return;
                HitInfo nullHit = new HitInfo();

                if (configData.Global.Suicide_Boom)
                {
                    Effect.server.Run(RocketExplosion, npc.transform.position);
                    nullHit.damageTypes.Add(Rust.DamageType.Explosion, 10000);
                }
                else
                    nullHit.damageTypes.Add(Rust.DamageType.Suicide, 10000);
                npc.Die(nullHit);
            });
        }
        #endregion

        #region Commands
        [ConsoleCommand("bot.count")]
        void CmdBotCount()
        {
            string msg = (NPCPlayers.Count == 1) ? "numberOfBot" : "numberOfBots";
            PrintWarning(lang.GetMessage(msg, this), NPCPlayers.Count);
        }

        [ConsoleCommand("bots.count")]
        void CmdBotsCount()
        {
            var records = BotReSpawnBots();
            if (records.Count == 0)
            {
                PrintWarning("There are no spawned npcs");
                return;
            }
            bool none = true;
            foreach (var entry in records)
                if (entry.Value.Count > 0)
                    none = false;
            if (none)
            {
                PrintWarning("There are no spawned npcs");
                return;
            }

            foreach (var entry in BotReSpawnBots().Where(x => Profiles[x.Key].Spawn.AutoSpawn == true))
                PrintWarning(entry.Key + " - " + entry.Value.Count + "/" + GetPop(Profiles[entry.Key]));
        }

        public BotData GetBData(BasePlayer player)
        {
            Vector3 start = player.eyes.position;
            Ray ray = new Ray(start, Quaternion.Euler(player.eyes.rotation.eulerAngles) * Vector3.forward);
            var hits = Physics.RaycastAll(ray);
            foreach (var hit in hits)
            {
                var npc = hit.collider?.GetComponentInParent<global::HumanNPC>();
                if (hit.distance < 2f)
                {
                    var bData = npc?.GetComponent<BotData>();
                    if (bData != null)
                        return bData;
                }
            }
            return null;
        }

        string TitleText => "<color=orange>" + lang.GetMessage("Title", this) + "</color>";

        [ChatCommand("botrespawn")]
        void botrespawn(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player.UserIDString, permAllowed) && !player.IsAdmin)
                return;

            if (args == null || args.Length == 0)
            {
                CheckKits();
                BSBGUI(player);
                BSMainUI(player, "", "", "Spawn");
                return;
            }

            string pn = string.Empty;
            var sp = spawnsData.CustomSpawnLocations;

            if (args != null && args.Length == 1)
            {
                switch (args[0])
                {
                    case "info":
                        var bData = GetBData(player);
                        if (bData == null)
                            SendReply(player, TitleText + lang.GetMessage("nonpc", this));
                        else
                            SendReply(player, TitleText + "NPC from profile - " + bData.profilename + ", wearing " + (String.IsNullOrEmpty(bData.kit) ? "no kit" : "kit - " + bData.kit));
                        return;
                }
            }

            if (args != null && args.Length == 2)
            {
                switch (args[0])
                {
                    case "add":
                        args[1] = args[1].Replace(" ", "_").Replace("-", "_");

                        if (Profiles.ContainsKey(args[1]))
                        {
                            SendReply(player, TitleText + lang.GetMessage("alreadyexists", this), args[1]);
                            return;
                        }
                        var customSettings = new Profile(ProfileType.Custom);
                        customSettings.Other.Location = player.transform.position;

                        storedData.Profiles.Add(args[1], customSettings);
                        AddData(args[1], customSettings);
                        SetupSpawnsFile();
                        SaveData();
                        SendReply(player, TitleText + lang.GetMessage("customsaved", this), player.transform.position);
                        BSBGUI(player);
                        BSMainUI(player, "2", args[1], "Spawn");
                        break;

                    case "remove":
                        if (storedData.Profiles.ContainsKey(args[1]))
                        {
                            DestroySpawnGroups(Spawners[args[1]][0].go);
                            spawnsData.CustomSpawnLocations[args[1]].Clear();
                            SaveSpawns();
                            Profiles.Remove(args[1]);
                            storedData.Profiles.Remove(args[1]);
                            storedData.MigrationDataDoNotEdit.Remove(args[1]);
                            SaveData();
                            SendReply(player, TitleText + lang.GetMessage("customremoved", this), args[1]);
                        }
                        else
                            SendReply(player, TitleText + lang.GetMessage("noprofile", this));
                        break;
                }
            }
        }

        void ShowSpawn(BasePlayer player, Vector3 loc, int num, float duration) => player.SendConsoleCommand("ddraw.text", duration, HasNav(loc) ? Color.green : Color.red, loc, $"<size=80>{num}</size>");
        void ShowProfiles(BasePlayer player, Vector3 loc, string name, float duration) => player.SendConsoleCommand("ddraw.text", duration, Color.green, loc, $"<size=20>{name}</size>");

        #endregion

        public Dictionary<ulong, int> DeadNPCPlayerIds = new Dictionary<ulong, int>();
        public Dictionary<ulong, string> KitRemoveList = new Dictionary<ulong, string>();
        public List<Vector3> SmokeGrenades = new List<Vector3>();
        public Dictionary<ulong, Inv> botInventories = new Dictionary<ulong, Inv>();

        public class Inv
        {
            public Profile profile;
            public string name;
            public List<InvContents>[] inventory = { new List<InvContents>(), new List<InvContents>(), new List<InvContents>() };
        }

        public class InvContents
        {
            public int ID;
            public int amount;
            public ulong skinID;
        }

        public Dictionary<string, int> Biomes = new Dictionary<string, int> { { "BiomeArid", 1 }, { "BiomeTemperate", 2 }, { "BiomeTundra", 4 }, { "BiomeArctic", 8 } };

        bool IsSpawner(string name) => Biomes.ContainsKey(name) || defaultData.Monuments.ContainsKey(name) || storedData.Profiles.ContainsKey(name);

        public enum Range { Melee, Close, Mid, Long, None }
        public Range TargetRange(float distance) => distance > 40 ? Range.Long : distance > 12 ? Range.Mid : distance > 4 ? Range.Close : Range.Melee;

        public Range BestRange(BotData bdata, Range targetrange)
        {
            if (bdata.melee)
                return Range.Melee;

            switch (targetrange)
            {
                case Range.Melee: return bdata.Weaps[Range.Melee].Count > 0 ? Range.Melee : bdata.Weaps[Range.Close].Count > 0 ? Range.Close : bdata.Weaps[Range.Mid].Count > 0 ? Range.Mid : Range.Long;
                case Range.Long: return bdata.Weaps[Range.Long].Count > 0 ? Range.Long : bdata.Weaps[Range.Mid].Count > 0 ? Range.Mid : bdata.Weaps[Range.Close].Count > 0 ? Range.Close : Range.Melee;
                case Range.Close: return bdata.Weaps[Range.Close].Count > 0 ? Range.Close : bdata.Weaps[Range.Mid].Count > 0 ? Range.Mid : bdata.Weaps[Range.Long].Count > 0 ? Range.Long : Range.Melee;
                case Range.Mid: return bdata.Weaps[Range.Mid].Count > 0 ? Range.Mid : bdata.Weaps[Range.Long].Count > 0 ? Range.Long : bdata.Weaps[Range.Close].Count > 0 ? Range.Close : Range.Melee;
            }
            return Range.Mid;
        }

        #region BotMono
        public class BotData : MonoBehaviour
        {
            public string kit;
            public Timer deaggro;
            public CustomGroup sg;
            public NewSpawnPoint sp;
            public List<SpawnInfo> SpawnPoints = new List<SpawnInfo>();
            public Dictionary<BasePlayer, float> Players = new Dictionary<BasePlayer, float>();
            public Dictionary<BasePlayer, float> Targets = new Dictionary<BasePlayer, float>();
            public BasePlayer CurrentTarget;
            public ScientistNPC npc;
            public BaseAIBrain brain;
            public Profile profile;
            public Range currentRange = Range.None;
            public Dictionary<Range, List<HeldEntity>> Weaps = new Dictionary<Range, List<HeldEntity>>();
            public List<HeldEntity> throwables = new List<HeldEntity>();
            public List<MedicalTool> meds = new List<MedicalTool>();
            public float nextThrowTime = Time.time + 10f;
            public float nextHealTime = Time.time + 10f;
            public string profilename, group;
            public bool canFire = true, throwing, healing, melee, temporary, noweapon, hasHeadLamp, stationary, inAir;
            CapsuleCollider capcol;

            void Start()
            {
                npc = GetComponent<global::ScientistNPC>();
                brain = npc.GetComponent<BaseAIBrain>();
                roampoint = npc.transform.position;

                if (npc.WaterFactor() > 0.9f)
                {
                    bs.timer.Once(2f, () =>
                    {
                        if (npc != null && !npc.IsDestroyed)
                            npc.Kill();
                    });
                    return;
                }

                if (profile.Other.Chute)
                {
                    inAir = true;
                    capcol = npc.GetComponent<CapsuleCollider>();
                    if (capcol != null)
                    {
                        capcol.isTrigger = true;
                        npc.GetComponent<CapsuleCollider>().radius += 2f;
                    }
                }

                if (!noweapon)
                    InvokeRepeating("SelectWeapon", UnityEngine.Random.Range(1.8f, 2.2f), UnityEngine.Random.Range(2.8f, 3.2f));

                InvokeRepeating("DoThink", UnityEngine.Random.Range(1.5f, 2.0f), UnityEngine.Random.Range(1.5f, 2.0f));
                InvokeRepeating("Attack", 0.5f, 0.5f);
                if (!temporary)
                    InvokeRepeating("KILL", random.Next(3, 15), random.Next(3, 15));
            }

            public Timer delay;
            public float distance = 0;
            public float distance1 = 0;
            public AIInformationZone zone;
            public Vector3 roampoint = new Vector3();
            public DateTime set;
            public bool relaxing;

            public void Think()
            {
                if (inAir || npc.IsWounded())
                    return;

                if (CurrentTarget == null || !Targets.ContainsKey(CurrentTarget) || !brain.Senses.Memory.IsLOS(CurrentTarget))
                    GetNearest();

                if (CurrentTarget != null)
                {
                    if (profile.Behaviour.Peace_Keeper && !CurrentTarget.IsHostile())
                    {
                        CheckKnownTime(CurrentTarget);
                    }
                    else if (Targets.ContainsKey(CurrentTarget))
                    {
                        relaxing = false;
                        brain.Navigator.SetFacingDirectionEntity(CurrentTarget);
                    }
                }
                else
                {
                    if (!relaxing)
                    {
                        relaxing = true;
                        brain.Navigator.ClearFacingDirectionOverride();
                        npc.modelState.aiming = false;
                        npc.SetPlayerFlag(BasePlayer.PlayerFlags.Aiming, false);
                    }
                    if (GoHome())
                    {
                        roampoint = sp.transform.position;
                        return;
                    }
                }

                if (!npc.IsReloading())
                {
                    if (!throwing && nextHealTime < Time.time && npc.health < npc.MaxHealth() / 5 * 4 && meds.Count() > 0 && (CurrentTarget == null || !brain.Senses.Memory.IsLOS(CurrentTarget)))
                    {
                        TryHeal();
                        return;
                    }

                    if (!healing && CurrentTarget != null && nextThrowTime < Time.time && throwables.Count > 0 && !brain.Senses.Memory.IsLOS(CurrentTarget) && !CurrentTarget.isInAir && Vector3.Distance(CurrentTarget.transform.position, npc.transform.position) < 80)
                    {
                        TryThrow(CurrentTarget);
                        return;
                    }
                }
                 
                brain.Navigator.MaxRoamDistanceFromHome = CurrentTarget != null ? 400 : profile.Behaviour.Roam_Range;

                distance1 = Vector3.Distance(roampoint, npc.transform.position);

                if (distance1 < distance && distance1 > 3)
                {
                    distance = distance1;
                    brain.Navigator.SetDestination(roampoint, CurrentTarget ? BaseNavigator.NavigationSpeed.Fast : BaseNavigator.NavigationSpeed.Slow, 0f, 0f);
                }
                else
                {
                    var loc = GetNearNavPoint(profile.Other.Short_Roam_Vision ? 10 : 30);
                    if (brain.Navigator.SetDestination(loc, CurrentTarget ? BaseNavigator.NavigationSpeed.Fast : BaseNavigator.NavigationSpeed.Slow, 0f, 0f))
                    {
                        distance = Vector3.Distance(loc, npc.transform.position);
                        roampoint = loc;
                    }
                }
            }

            public bool GoHome()
            {
                brain.Navigator.MaxRoamDistanceFromHome = CurrentTarget == null ? profile.Behaviour.Roam_Range : 400;

                var dist = Vector3.Distance(sp.transform.position, brain.transform.position);
                if (dist > profile.Behaviour.Roam_Range && dist > 5)
                {
                    brain.Navigator.SetDestination(sp.transform.position, BaseNavigator.NavigationSpeed.Normal, 0f, 0f);
                    return true;
                }
                return false;
            }

            void TryThrow(BasePlayer t)
            {
                if (t == null)
                    return;
                Vector3 loc = t.transform.position;
                var active = npc.GetHeldEntity();
                var throwable = throwables.GetRandom();
                if (throwable == null || active == null || throwing)
                    return;

                throwing = true;

                throwable.GetItem().amount = 2;
                bs.UpdateActiveItem(npc, throwable, this);

                bs.timer.Once(1.5f, () =>
                {
                    if (npc != null && t != null)
                    {
                        npc.SetAimDirection((t == null ? loc : t.ServerPosition - npc.ServerPosition).normalized);
                        npc.SignalBroadcast(BaseEntity.Signal.Throw);
                        (npc.GetHeldEntity() as ThrownWeapon).ServerThrow(npc.transform.position + ((t == null ? loc : t.transform.position - npc.transform.position) / 4 * 3.2f));
                    }
                });

                bs.timer.Once(3f, () =>
                {
                    if (npc != null && active != null && this != null)
                    {
                        bs.UpdateActiveItem(npc, active, this);
                        throwing = false;
                        nextThrowTime = Time.time + 20f;
                    }
                });
            }

            public Vector3 GetNearNavPoint(int radius = 30)
            {
                var pos = bs.TryGetSpawn(CurrentTarget == null || currentRange == Range.Long ? npc.transform.position : CurrentTarget.transform.position, radius);
                return pos == Vector3.zero ? npc.transform.position : pos;
            }

            public void TryHeal()
            {
                HeldEntity active = npc.GetHeldEntity();
                if (active == null || healing)
                    return;

                healing = true;
                foreach (var item in meds)
                {
                    if (item.name.Contains("syringe_medical.entity")) //// Add bandage use
                    {
                        bs.UpdateActiveItem(npc, item, this);
                        bs.timer.Once(1.5f, () => npc?.SignalBroadcast(BasePlayer.Signal.Attack, "", null));
                        break;
                    }
                }
                bs.timer.Once(1f, () =>
                {
                    if (npc != null)
                    {
                        var newActive = npc.GetHeldEntity();
                        if (newActive?.name != null && newActive.name.Contains("syringe_medical.entity"))
                        {
                            ItemModConsumable component = newActive?.GetOwnerItemDefinition()?.GetComponent<ItemModConsumable>();
                            if (component == null)
                                return;
                            foreach (var effect in component.effects.Where(effect => effect.type == MetabolismAttribute.Type.Health || effect.type == MetabolismAttribute.Type.HealthOverTime))
                                npc.health = npc.health + (bs.configData.Global.Scale_Meds_To_Health ? effect.amount / 100 * npc.MaxHealth() : effect.amount);
                            npc.InitializeHealth(npc.health, npc.MaxHealth());
                        }
                    }
                });
                bs.timer.Once(4f, () =>
                {
                    if (npc != null)
                    {
                        bs.UpdateActiveItem(npc, active, this);
                        healing = false;
                        nextHealTime = Time.time + 15f;
                    }
                });
            }

            public void DoThink()
            {
                Think();
                if (bs.configData.Global.Allow_Ai_Dormant && (profile.type != ProfileType.Biome || !bs.configData.Global.Prevent_Biome_Ai_Dormant))
                {
                    int playersnear = BaseEntity.Query.Server.GetPlayersInSphere(npc.Brain.Senses.owner.transform.position, 300, AIBrainSenses.playerQueryResults, new Func<BasePlayer, bool>(IsPlayer));

                    if (playersnear == 0)
                        npc.IsDormant = Rust.Ai.AiManager.ai_dormant && CurrentTarget == null;
                    else
                        npc.IsDormant = false;
                }

                int playersInSphere = BaseEntity.Query.Server.GetPlayersInSphere(npc.Brain.Senses.owner.transform.position, profile.Behaviour.Aggro_Range, AIBrainSenses.playerQueryResults, new Func<BasePlayer, bool>(WantsToAttack));
                for (int i = 0; i < playersInSphere; i++)
                {
                    BasePlayer player = AIBrainSenses.playerQueryResults[i];
                    Addto(Players, player);
                    Addto(Targets, player);

                    npc.Brain.Senses.Memory.SetKnown(player, npc, npc.Brain.Senses);
                    npc.Brain.Senses.Memory.Targets = Players.Keys.ToList<BaseEntity>();
                }


                if (playersInSphere == 0)
                    npc.Brain.Senses.Memory.Targets.Clear();

                UpdateKnownPlayersLOS();
                CheckKnownTime(null);
            }

            public void Addto(Dictionary<BasePlayer, float> list, BasePlayer player) => list[player] = Time.time;

            public void UpdateKnownPlayersLOS()
            {
                foreach (var player in Players)
                {
                    if (player.Key == null)
                        continue;

                    if (player.Key.health <= 0 || player.Key.IsDead())
                        CheckKnownTime(player.Key);

                    bool flag = brain.Senses.ownerAttack.CanSeeTarget(player.Key);
                    brain.Senses.Memory.SetLOS(player.Key, flag);
                }
            }

            float single = float.PositiveInfinity;
            float dist = 0;
            BasePlayer nearest = null;
            public void GetNearest()
            {
                single = float.PositiveInfinity;
                nearest = null;
                foreach (var player in Targets)
                {
                    if (player.Key == null || player.Key.Health() <= 0f)
                        continue;

                    dist = Vector3.Distance(player.Key.transform.position, npc.transform.position);
                    if (brain.Senses.Memory.IsLOS(player.Key) && dist < single)
                    {
                        single = dist;
                        nearest = player.Key;
                    }
                }
                CurrentTarget = nearest == null ? CurrentTarget : nearest;
            }

            public void CheckKnownTime(BasePlayer player = null)
            {
                if (player != null && CurrentTarget == player)
                    CurrentTarget = null;
                foreach (var p in Targets.ToDictionary(pair => pair.Key, pair => pair.Value))
                    if ((player != null && p.Key == player) || Time.time - p.Value > bs.configData.Global.Deaggro_Memory_Duration)
                    {
                        if (CurrentTarget == p.Key)
                            CurrentTarget = null;
                        Targets.Remove(p.Key);
                    }
            }

            public bool IsPlayer(BasePlayer player) => !player.IsNpc && player?.net?.connection != null;
            public bool WantsToAttack(BasePlayer entity) => WantsAttack(entity, false);
            public bool WantsAttack(BasePlayer player, bool hurt)
            {
                if (npc?.Brain?.Senses == null || player == null || !player.isServer || player.EqualNetID(npc.Brain.Senses.owner) || player.Health() <= 0f)
                    return false;

                if (profile.Behaviour.Ignore_All_Players && IsPlayer(player))
                    return false;

                if (profile.Behaviour.Ignore_Sleepers && player.IsSleeping() || player.IsDead())
                    return false;

                if (profile.Behaviour.Respect_Safe_Zones && player.InSafeZone())
                    return false;

                if (!bs.NoSash && !profile.Behaviour.Target_Noobs && player.IsNoob())
                    return false;

                if ((profile.Behaviour.Target_ZombieHorde == ShouldAttack.Ignore || (profile.Behaviour.Target_ZombieHorde == ShouldAttack.Defend && !hurt)) && player.Categorize() == "Zombie")
                    return false;

                if ((profile.Behaviour.Target_HumanNPC == ShouldAttack.Ignore || (profile.Behaviour.Target_HumanNPC == ShouldAttack.Defend && !hurt)) && bs.HumanNPCs.Contains(player.userID))
                    return false;

                var bData = player.GetComponent<BotData>();
                if (bData != null)
                {
                    if (bData.profilename == profilename) 
                        return false;
                    if (!bs.configData.Global.Ignore_Factions && FactionAllies(profile.Behaviour.Faction, profile.Behaviour.SubFaction, bData.profile.Behaviour.Faction, bData.profile.Behaviour.SubFaction))
                        return false;
                }

                if (player.IsNpc && player.Categorize() != "Zombie" && bData == null)
                    if ((profile.Behaviour.Target_Other_Npcs == ShouldAttack.Ignore || (profile.Behaviour.Target_Other_Npcs == ShouldAttack.Defend && !hurt)))
                        return false;

                brain.Senses.Memory.SetLOS(player, brain.Senses.ownerAttack.CanSeeTarget(player));

                if (!hurt)
                {
                    if (profile.Behaviour.Peace_Keeper && !player.IsHostile())
                        return false;

                    if (!brain.Senses.Memory.IsLOS(player))
                        return false;

                    float dist = Vector3.Distance(npc.transform.position, player.transform.position);
                    if ((player == CurrentTarget && dist > profile.Behaviour.DeAggro_Range) || (player != CurrentTarget && dist > profile.Behaviour.Aggro_Range))
                        return false;
                }
                return true;
            } 

            bool FactionAllies(int aa, int ab, int ba, int bb) => (aa == 0 && bb == 0) || (ba == 0 && bb == 0) || (aa != 0 && aa == ba) || (aa != 0 && aa == bb) || (ab != 0 && ab == ba) || (ab != 0 && ab == bb);

            public void KILL()
            {
                if (npc == null || profile == null || sg?.currentPopulation == null)
                    return;
                if (sg.currentPopulation > GetPop(profile))
                {
                    DestroyImmediate(npc?.GetComponent<SpawnPointInstance>());
                    if (!npc.IsDestroyed)
                        npc.Kill();
                }
            }

            void SelectWeapon() => bs.SelectWeapon(npc, this, brain);
            public void OnDestroy()
            {
                CancelInvoke("KILL");
                CancelInvoke("SelectWeapon");
                CancelInvoke("DoThink");
                CancelInvoke("Attack"); 

                if (sg?.marker != null && sg.currentPopulation == 1 && !profile.Other.Always_Show_Map_Marker)
                {
                    sg.marker.alpha = 0f;
                    sg.marker.SendUpdate();
                }
                if (temporary && sg != null && sg.currentPopulation < 2)
                    Destroy(sg);

                if (npc?.Brain != null)
                    Destroy(npc.Brain);

                if (npc?.userID != null)
                    bs.NPCPlayers.Remove(npc.userID);
            }

            private void OnTriggerEnter(Collider col)
            {
                if (!inAir)
                    return;

                NavMeshHit hit;
                if (NavMesh.SamplePosition(npc.transform.position, out hit, 10, -1))
                {
                    if (npc.WaterFactor() > 0.9f)
                    {
                        npc.Kill();
                        return;
                    }
                    
                    if (capcol != null)
                    {
                        capcol.isTrigger = false;
                        capcol.radius -= 2f;
                    }

                    var rb = npc.gameObject.GetComponent<Rigidbody>();
                    rb.isKinematic = true;
                    rb.useGravity = false;
                    npc.gameObject.layer = 17;
                    npc.ServerPosition = hit.position;

                    foreach (var child in npc.children.Where(child => child.name.Contains("parachute")))
                    {
                        child.SetParent(null);
                        child.Kill();
                        npc.Brain.Navigator.CanUseNavMesh = true;
                        npc.NavAgent.enabled = true;
                        npc.Brain.Navigator.PlaceOnNavMesh();

                        if (!npc.NavAgent.isOnNavMesh)
                            npc.BecomeWounded();
                        break;
                    }
                    inAir = false;
                    npc.Resume();
                }
            }

            #region Attack
            public void Attack()
            {
                if (CurrentTarget == null || !Targets.ContainsKey(CurrentTarget) || !npc.Brain.Senses.Memory.IsLOS(CurrentTarget))
                    GetNearest();

                if (CurrentTarget != null && canFire)
                    AttackTarget(CurrentTarget, npc.Brain.Senses.Memory.IsLOS(CurrentTarget));
            }

            public void AttackTarget(BasePlayer target, bool targetIsLOS)
            {
                var heldEntity = npc.GetHeldEntity() as AttackEntity;
                if (heldEntity == null || target == null)
                    return;

                if (Targets.ContainsKey(target))
                    npc.Brain.Navigator.SetFacingDirectionEntity(target);

                var melee = heldEntity as BaseMelee;

                if (melee)
                {
                    npc.nextTriggerTime = Time.time + 1f;
                    melee.attackLengthMin = 10f;

                    if (target != null && bs.HasNav(target.transform.position))
                    {
                        MeleeAttack(target, melee);
                        if (!stationary)
                        npc.Brain.Navigator.SetDestination(target.transform.position + (target.transform.position - npc.transform.position).normalized, BaseNavigator.NavigationSpeed.Fast, 0f, 0f);
                    }
                    else
                        if (!stationary)
                            npc.Brain.Navigator.SetDestination(sp.transform.position, BaseNavigator.NavigationSpeed.Fast, 0f, 0f);
                    return;
                }

                if (!stationary)
                {
                    var ft = heldEntity as FlameThrower;
                    if (ft != null)
                    {
                        var loc = GetNearNavPoint(3);
                        if (bs.HasNav(loc))
                        {
                            FlameAttack(npc, target, ft);
                            npc.Brain.Navigator.SetDestination(loc, BaseNavigator.NavigationSpeed.Fast, 0f, 0f);
                            return;
                        }
                    }
                }

                float single = Vector3.Dot(npc.eyes.BodyForward(), (target.CenterPoint() - npc.eyes.position).normalized);
                if (!targetIsLOS)
                {
                    if (single < 0.5f)
                        npc.targetAimedDuration = 0f;
                    npc.CancelBurst(0.2f);
                }
                else if (single > 0.2f && !npc.IsReloading())
                    npc.targetAimedDuration += 0.5f;

                if (!(npc.targetAimedDuration >= 0.5f & targetIsLOS))
                    npc.CancelBurst(0.2f);
                else
                {
                    BaseProjectile baseProjectile = heldEntity as BaseProjectile;
                    if (baseProjectile)
                    {
                        if (baseProjectile.primaryMagazine.contents <= 0)
                        {
                            baseProjectile.ServerReload();
                            return;
                        }
                        if (baseProjectile.NextAttackTime > Time.time)
                            return;

                        if (currentRange == Range.Long || heldEntity is BowWeapon || heldEntity is BaseLauncher)
                        {
                            //// Work on better aim duration checks
                            npc.modelState.aiming = true;
                            npc.SetPlayerFlag(BasePlayer.PlayerFlags.Aiming, true);
                            if (npc.targetAimedDuration > 2f)
                                npc.targetAimedDuration = 0;
                            else
                                return;
                        }
                    }
                    if (Mathf.Approximately(heldEntity.attackLengthMin, -1f))
                    {
                        ServerUse(heldEntity, npc.damageScale);
                        npc.lastGunShotTime = Time.time;
                        return;
                    }
                    if (npc.IsInvoking(new Action(TriggerDown)))
                        return;

                    if (Time.time <= npc.nextTriggerTime || Time.time <= npc.triggerEndTime)
                        return;

                    npc.InvokeRepeating(new Action(TriggerDown), 0f, 0.01f);
                    npc.triggerEndTime = Time.time + UnityEngine.Random.Range(heldEntity.attackLengthMin / 2, Mathf.Min(4, heldEntity.attackLengthMax * 2));
                    TriggerDown();
                }
            }

            public void TriggerDown()
            {
                AttackEntity heldEntity = npc.GetHeldEntity() as AttackEntity;
                if (heldEntity != null)
                    ServerUse(heldEntity, npc.damageScale);

                npc.lastGunShotTime = Time.time;
                if (Time.time > npc.triggerEndTime)
                {
                    npc.CancelInvoke(new Action(TriggerDown));
                    npc.nextTriggerTime = Time.time + (heldEntity != null ? heldEntity.attackSpacing : 1f);
                }
            }

            public void LauncherUse(BaseLauncher launcher)
            {
                if (CurrentTarget == null || !Targets.ContainsKey(CurrentTarget) || !npc.Brain.Senses.Memory.IsLOS(CurrentTarget))
                    GetNearest();

                if (CurrentTarget == null)
                    return;
                var dist = Vector3.Distance(npc.transform.position, CurrentTarget.transform.position);
                if (launcher.primaryMagazine.ammoType.itemid != 1055319033 && (dist > 100 || dist < 7))
                    return;

                ItemModProjectile component = launcher.primaryMagazine.ammoType.GetComponent<ItemModProjectile>();
                if (!component)
                    return;

                if (launcher.primaryMagazine.contents <= 0)
                {
                    launcher.SignalBroadcast(BaseEntity.Signal.DryFire, null);
                    launcher.StartAttackCooldown(1f);
                    return;
                }
                if (!component.projectileObject.Get().GetComponent<ServerProjectile>())
                {
                    launcher.ServerUse(1f, null);
                    return;
                }
                launcher.primaryMagazine.contents--;
                if (launcher.primaryMagazine.contents < 0)
                    launcher.primaryMagazine.contents = 0;

                float distance = Vector3.Distance(npc.transform.position, CurrentTarget.transform.position);
                Vector3 muzzlePoint = (CurrentTarget.transform.position + Offset(component.projectileObject.resourcePath, distance) - npc.eyes.position).normalized;

                var baseEntity = GameManager.server.CreateEntity(component.projectileObject.resourcePath, npc.eyes.position + (npc.eyes.BodyForward() / 2f), new Quaternion(), true);

                baseEntity.creatorEntity = npc;
                ServerProjectile serverProjectile = baseEntity.GetComponent<ServerProjectile>();
                if (serverProjectile)
                    serverProjectile.InitializeVelocity(muzzlePoint * serverProjectile.speed);

                baseEntity.SendMessage("SetDamageScale", 1f);
                baseEntity.Spawn();
                launcher.StartAttackCooldown(launcher.ScaleRepeatDelay(launcher.repeatDelay));
                launcher.SignalBroadcast(BaseEntity.Signal.Attack, string.Empty, null);
            }

            public Vector3 Offset(string resource, float distance)
            {
                if (resource.Contains("_he")) return new Vector3(0, distance * (distance / 10) / 18f, 0);
                if (resource.Contains("_hv")) return new Vector3(0, distance * (distance / 18) / 18f, 0);
                return new Vector3(0, distance * (distance / 14) / 18f, 0);
            }

            int AccuracyScaled(int accuracy, float distance) => distance > 400 ? (int)(accuracy / 2f) : distance > 300 ? (int)(accuracy / 5f * 3f) : accuracy > 200 ? (int)(accuracy / 4f * 3f) : accuracy;

            public void ServerUse(HeldEntity held, float damageModifier)
            {
                var launcher = held as BaseLauncher;
                if (launcher)
                {
                    LauncherUse(launcher);
                    return;
                }

                if (held is FlameThrower)
                    return;

                var att = held as BaseProjectile;
                if (att == null || att.HasAttackCooldown())
                    return;
                damageModifier *= (held.name.Contains("bolt") || held.name.Contains("l96")) ? (float)profile.Behaviour.RangeWeapon_DamageScale : 1;

                if (att.primaryMagazine.contents <= 0)
                {
                    held.SignalBroadcast(BaseEntity.Signal.DryFire, null);
                    att.StartAttackCooldownRaw(1f);
                    return;
                }

                att.primaryMagazine.contents--;
                if (att.primaryMagazine.contents < 0)
                    att.primaryMagazine.contents = 0;

                NPCPlayer npc1 = held.GetOwnerPlayer() as NPCPlayer;
                npc1.SetAimDirection(npc1.GetAimDirection());
                att.StartAttackCooldownRaw(att.repeatDelay);
                att.SignalBroadcast(BaseEntity.Signal.Attack, string.Empty, null);

                ItemModProjectile component = att.primaryMagazine.ammoType.GetComponent<ItemModProjectile>();
                Projectile projectile = component?.projectileObject?.Get()?.GetComponent<Projectile>();
                var dir = npc1.eyes.BodyForward();
                BasePlayer target = null;
                for (int i = 0; i < component.numProjectiles; i++)
                {
                    List<RaycastHit> list = Pool.GetList<RaycastHit>();
                    GamePhysics.TraceAll(new Ray(npc1.eyes.position, npc1.eyes.BodyForward()), 0f, list, 400f, 1219701505, QueryTriggerInteraction.UseGlobal);

                    for (int j = 0; j < list.Count; j++)
                    {
                        RaycastHit item = list[j];
                        BaseEntity entity = item.GetEntity();

                        if (entity == null)
                            continue;
                        target = entity as BasePlayer;

                        if (target != null && !npc1.IsVisibleAndCanSee(target.eyes.position))
                            break;

                        if (entity != held && !entity.EqualNetID(held))
                        {
                            var distance = Vector3.Distance(npc.transform.position, entity.transform.position);
                            if (AccuracyScaled(profile.Behaviour.Bot_Accuracy_Percent, distance) < bs.GetRand(1, 101))
                                continue;

                            HitInfo hitInfo = new HitInfo();
                            if (bs.configData.Global.NPCs_Damage_Armour)
                                hitInfo.HitBone = (uint)(bs.GetRand(1, 100) < 97 ? 1031402764 : 698017942);

                            hitInfo.Initiator = att.GetOwnerPlayer();
                            if (hitInfo.Initiator == null)
                                hitInfo.Initiator = att.GetParentEntity();

                            hitInfo.Weapon = att;
                            hitInfo.WeaponPrefab = att.gameManager.FindPrefab(att.PrefabName).GetComponent<AttackEntity>();
                            hitInfo.IsPredicting = false;

                            if (projectile != null)
                                hitInfo.DoHitEffects = projectile.doDefaultHitEffects;

                            hitInfo.DidHit = true;
                            hitInfo.ProjectileVelocity = npc1.eyes.BodyForward() * 300f;

                            if (att.MuzzlePoint != null)
                                hitInfo.PointStart = att.MuzzlePoint.position;

                            hitInfo.PointEnd = item.point;
                            hitInfo.HitPositionWorld = item.point;
                            hitInfo.HitNormalWorld = item.normal;
                            hitInfo.HitEntity = entity;
                            hitInfo.UseProtection = true;

                            if (profile.Behaviour.Victim_Bleed_Amount_Per_Hit == 0)
                                hitInfo.damageTypes.types[6] = 1f;
                            else 
                            {
                                BasePlayer player = entity as BasePlayer;
                                if (player != null)
                                {
                                    if (profile.Behaviour.Victim_Bleed_Amount_Max == 100)
                                        player.metabolism?.bleeding.Add(profile.Behaviour.Victim_Bleed_Amount_Per_Hit);
                                    else
                                    {
                                        var max = profile.Behaviour.Victim_Bleed_Amount_Max - player.metabolism.bleeding.value;
                                        if (max > 0)
                                            player.metabolism?.bleeding.Add(Mathf.Min(max, profile.Behaviour.Victim_Bleed_Amount_Per_Hit));
                                    }
                                }
                            } 

                            if (projectile != null)
                                projectile.CalculateDamage(hitInfo, att.GetProjectileModifier(), 1f);

                            hitInfo.damageTypes.ScaleAll(att.GetDamageScale(false) * damageModifier * 0.2f * ((float)profile.Behaviour.Bot_Damage_Percent) / 50f);
                            if (bs.configData.Global.Reduce_Damage_Over_Distance)
                                hitInfo.damageTypes.ScaleAll(Reduction(distance, (int)currentRange));

                            entity.OnAttacked(hitInfo);

                            if (entity is BasePlayer || entity is BaseNpc)
                            {
                                hitInfo.HitPositionLocal = entity.transform.InverseTransformPoint(hitInfo.HitPositionWorld);
                                hitInfo.HitNormalLocal = entity.transform.InverseTransformDirection(hitInfo.HitNormalWorld);
                                hitInfo.HitMaterial = StringPool.Get("Flesh");
                                Effect.server.ImpactEffect(hitInfo);
                            }
                            if (entity.ShouldBlockProjectiles())
                                break;
                        }
                    }
                    Pool.FreeList<RaycastHit>(ref list);
                    var cone = AimConeUtil.GetModifiedAimConeDirection(component.projectileSpread + att.GetAimCone() + att.GetAIAimcone() * 1f, npc1.eyes.BodyForward(), true);
                    att.CreateProjectileEffectClientside(component.projectileObject.resourcePath, npc1.eyes.position + npc1.eyes.BodyForward() * 3f, cone * component.projectileVelocity, UnityEngine.Random.Range(1, 100), null, att.IsSilenced(), false);
                    att.CreateProjectileEffectClientside(component.projectileObject.resourcePath, npc1.eyes.position + npc1.eyes.BodyForward() * 3f, cone * component.projectileVelocity, UnityEngine.Random.Range(1, 100), target?.net?.connection, att.IsSilenced(), true);
                }
            }

            float Reduction(float distance, int range)
            {
                if (range == 1 && distance > 20)
                    return Mathf.Max(0.1f, Mathf.Min(1, 1 - (distance - 20) / 100));
                if (range == 2 && distance > 40)
                    return Mathf.Max(0.1f, Mathf.Min(1, 1 - (distance - 40) / 100));
                if (range == 3 && distance > 100)
                    return Mathf.Max(0.1f, Mathf.Min(1, 1 - (distance - 100) / 300));
                return 1;
            }

            void FlameAttack(global::HumanNPC npc, BasePlayer t, FlameThrower ft)
            {
                if (t == null || ft == null)
                    return;

                if (Vector3.Distance(npc.transform.position, t.transform.position) > 4 || ft.HasAttackCooldown())
                {
                    Flame(false, ft);
                    return;
                }
                if (ft.ammo < 1)
                {
                    Flame(false, ft);
                    ft.ServerReload();
                }
                else if (!npc.IsReloading())
                {
                    if (ft.IsOnFire())
                        return;
                    Flame(true, ft);
                }
            }

            void Flame(bool on, FlameThrower ft)
            {
                npc.modelState.aiming = on;
                npc.SetPlayerFlag(BasePlayer.PlayerFlags.Aiming, on);
                ft.SetFlameState(on);
            }

            void MeleeAttack(BasePlayer t, BaseMelee melee)
            {
                if (t == null || melee == null)
                    return;

                if (melee as Chainsaw)
                {
                    (melee as Chainsaw).SetAttackStatus(true);
                    melee.Invoke(() => (melee as Chainsaw).SetAttackStatus(false), melee.attackSpacing + 0.5f);
                }

                Vector3 serverPos = t.ServerPosition - npc.ServerPosition;
                if (serverPos.magnitude > 0.001f)
                    npc.ServerRotation = Quaternion.LookRotation(serverPos.normalized);

                if (melee.NextAttackTime > Time.time || Vector3.Distance(npc.transform.position, t.transform.position) > 1.5)
                    return;

                npc.SignalBroadcast(BaseEntity.Signal.Attack, string.Empty, null);
                if (melee.swingEffect.isValid)
                    Effect.server.Run(melee.swingEffect.resourcePath, melee.transform.position, Vector3.forward, npc.net.connection, false);

                delay = bs.timer.Once(0.2f, () =>
                {
                    if (npc == null || profile == null || melee == null)
                        return;

                    Vector3 position = npc.eyes.position;
                    Vector3 direction = npc.eyes.BodyForward();
                    for (int index1 = 0; index1 < 2; ++index1)
                    {
                        List<RaycastHit> list = Pool.GetList<RaycastHit>();
                        GamePhysics.TraceAll(new Ray(position - direction * (index1 == 0 ? 0.0f : 0.2f), direction), index1 == 0 ? 0.0f : melee.attackRadius, list, melee.effectiveRange + 0.2f, 1219701521, QueryTriggerInteraction.UseGlobal);
                        bool flag = false;
                        for (int index2 = 0; index2 < list.Count; ++index2)
                        {
                            RaycastHit hit = list[index2];
                            BaseEntity e = hit.GetEntity();
                            
                            if (e != null && npc != null && !e.EqualNetID(npc) && !npc.isClient)
                            {
                                BasePlayer p = e as BasePlayer;
                                float num = 0.0f;
                                foreach (Rust.DamageTypeEntry damageType in melee.damageTypes)
                                    if (damageType?.amount != null)
                                        num += damageType.amount;

                                var attinfo = new HitInfo(npc, e, Rust.DamageType.Slash, num * 0.2f * 0.75f * (float)profile.Behaviour.Melee_DamageScale);

                                if (profile.Behaviour.Victim_Bleed_Amount_Per_Hit == 0)
                                    attinfo.damageTypes.types[6] = 1f;
                                else if (p != null)
                                {
                                    if (profile.Behaviour.Victim_Bleed_Amount_Max == 100)
                                        p.metabolism?.bleeding.Add(profile.Behaviour.Victim_Bleed_Amount_Per_Hit);
                                    else
                                    {
                                        var max = profile.Behaviour.Victim_Bleed_Amount_Max - p.metabolism.bleeding.value;
                                        if (max > 0)
                                            p.metabolism?.bleeding.Add(Mathf.Min(max, profile.Behaviour.Victim_Bleed_Amount_Per_Hit));
                                    }
                                }

                                e.OnAttacked(attinfo);
                                HitInfo info = Pool.Get<HitInfo>();
                                info.HitEntity = e;
                                info.HitPositionWorld = hit.point;
                                info.HitNormalWorld = -direction;
                                info.HitMaterial = e is BaseNpc || p != null ? StringPool.Get("Flesh") : StringPool.Get(hit.GetCollider().sharedMaterial != null ? hit.GetCollider().sharedMaterial.GetName() : "generic");
                                info.damageTypes.ScaleAll(((float)profile.Behaviour.Bot_Damage_Percent) / 50f);

                                melee.ServerUse_OnHit(info);
                                Effect.server.ImpactEffect(info);
                                Pool.Free<HitInfo>(ref info);
                                flag = true;
                                if (!(e != null) || e.ShouldBlockProjectiles())
                                    break;
                            }
                        }
                        Pool.FreeList<RaycastHit>(ref list);
                        if (flag)
                            break;
                    }
                    melee.StartAttackCooldown(melee.repeatDelay * 2f);
                });
            }
            #endregion
        }
        #endregion 

        #region Config
        private ConfigData configData;

        public class UI
        {
            public string ButtonColour = "0.7 0.32 0.17 1";
            public string ButtonColour2 = "0.4 0.1 0.1 1";
        }

        public class Global
        {
            public bool NPCs_Assist_NPCs = true, Ignore_Parented_HackedCrates = true, Ignore_HackableCrates_With_OwnerID = true, Reduce_Damage_Over_Distance = false, Ignore_Factions = false, Scale_Meds_To_Health = false, Allow_Ai_Dormant = false, Prevent_Biome_Ai_Dormant = false, Limit_ShortRange_Weapon_Use = false, NPCs_Damage_Armour = true, RustRewards_Whole_Numbers = true, Announce_Toplayer = false, UseServerTime = false, Disable_Non_Parented_Custom_Profiles_After_Wipe = true;
            public int Deaggro_Memory_Duration = 20, DayStartHour = 8, NightStartHour = 20, Show_Profiles_Seconds = 10;
            public bool APC_Safe = true, Turret_Safe = true, Animal_Safe = true, Supply_Enabled, Ignore_Skinned_Supply_Grenades, Staggered_Despawn = false;
            public bool Suicide_Boom = true, Remove_Frankenstein_Parts = true, Remove_KeyCard = true, Pve_Safe = true;
            public int Remove_BackPacks_Percent = 100;
            public int Max_Chute_Fall_Speed = 100;
        }

        class ConfigData
        {
            public string DataPrefix = "default";
            public UI UI = new UI();
            public Global Global = new Global();
        }

        private void LoadConfigVariables()
        {
            configData = Config.ReadObject<ConfigData>();
            SaveConfig(configData);
        }

        protected override void LoadDefaultConfig()
        {
            LoadConfigVariables();
            Puts("Creating new config file.");
        }

        void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, true);
        }
        #endregion

        #region Data  
        class StoredData
        {
            public Dictionary<string, Profile> Profiles = new Dictionary<string, Profile>();
            public Dictionary<string, ProfileRelocation> MigrationDataDoNotEdit = new Dictionary<string, ProfileRelocation>();
        }

        public class ProfileRelocation
        {
            public Vector3 ParentMonument = new Vector3();
            public Vector3 Offset = new Vector3();
        }

        class DefaultData
        {
            public Dictionary<string, Profile> Events = new Dictionary<string, Profile>() { { "AirDrop", new Profile(ProfileType.Event) }, { "CH47_Kill", new Profile(ProfileType.Event) }, { "PatrolHeli_Kill", new Profile(ProfileType.Event) }, { "APC_Kill", new Profile(ProfileType.Event) }, { "LockedCrate_Spawn", new Profile(ProfileType.Event) }, { "LockedCrate_HackStart", new Profile(ProfileType.Event) } };
            public Dictionary<string, Profile> Monuments = bs.GotMonuments;
            public Dictionary<string, Profile> Biomes = new Dictionary<string, Profile>() { { "BiomeArid", new Profile(ProfileType.Biome) }, { "BiomeTemperate", new Profile(ProfileType.Biome) }, { "BiomeTundra", new Profile(ProfileType.Biome) }, { "BiomeArctic", new Profile(ProfileType.Biome) } };
        }

        class SpawnsData
        {
            public Dictionary<string, List<SpawnData>> CustomSpawnLocations = new Dictionary<string, List<SpawnData>>();
        }

        public class SpawnData
        {
            public SpawnData(Profile p)
            {
                if (p != null)
                {
                    Kits = p.Spawn.Kit.ToList();
                    Health = p.Spawn.BotHealth;
                    Stationary = p.Spawn.Stationary;
                    RoamRange = p.Behaviour.Roam_Range;
                }
            }
            public bool UseOverrides = false;
            public Vector3 loc;
            public float rot;
            public List<string> Kits;
            public int Health;
            public bool Stationary;
            public int RoamRange;
        }

        StoredData storedData = new StoredData();
        DefaultData defaultData;
        SpawnsData spawnsData = new SpawnsData();

        void SaveSpawns() => Interface.Oxide.DataFileSystem.WriteObject($"BotReSpawn/{configData.DataPrefix}-SpawnsData", spawnsData);
        void SaveData()
        {
            if (!loaded)
                return;

            storedData.Profiles = storedData.Profiles.OrderBy(x => x.Key).ToDictionary(pair => pair.Key, pair => pair.Value);
            defaultData.Monuments = defaultData.Monuments.OrderBy(x => x.Key).ToDictionary(pair => pair.Key, pair => pair.Value);
            Interface.Oxide.DataFileSystem.WriteObject($"BotReSpawn/{configData.DataPrefix}-CustomProfiles", storedData);
            Interface.Oxide.DataFileSystem.WriteObject($"BotReSpawn/{configData.DataPrefix}-DefaultProfiles", defaultData);
            Interface.Oxide.DataFileSystem.WriteObject($"BotReSpawn/{configData.DataPrefix}-SpawnsData", spawnsData);
        }

        void ReloadData(string profile, bool UI, object AutoSpawn)
        {
            if (UI)
                SaveData();

            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>($"BotReSpawn/{configData.DataPrefix}-CustomProfiles");
            defaultData = Interface.Oxide.DataFileSystem.ReadObject<DefaultData>($"BotReSpawn/{configData.DataPrefix}-DefaultProfiles");

            foreach (var spawner in Spawners[profile])
                DestroySpawnGroups(spawner.go);

            if (storedData.Profiles.ContainsKey(profile))
            {
                Profiles.Remove(profile);
                AddData(profile, storedData.Profiles[profile]);
            }

            Profile prof = null;
            if (defaultData.Monuments.ContainsKey(profile))
                prof = defaultData.Monuments[profile];
            if (defaultData.Biomes.ContainsKey(profile))
                prof = defaultData.Biomes[profile];
            if (defaultData.Events.ContainsKey(profile))
                prof = defaultData.Events[profile];

            if (prof != null)
            {

                if (AutoSpawn != null)
                    prof.Spawn.AutoSpawn = (bool)AutoSpawn;

                prof.Other.Location = Profiles[profile].Other.Location;
                Profiles[profile] = prof;
                GameObject go = Spawners[profile][0].go;
                AddProfile(go, profile, prof, go.transform.position);
            }

            timer.Once(1f, () => CreateSpawnGroups(profile));
        }

        public enum ShouldAttack { Ignore, Defend, Attack }
        public enum ProfileType { Monument, Custom, Biome, Event }

        public class Profile
        {
            public Profile Clone(Profile source)
            {
                var serialized = JsonConvert.SerializeObject(this);
                var deser = JsonConvert.DeserializeObject<Profile>(serialized);
                deser.Other.Parent_Monument = source.Other.Parent_Monument;
                deser.Other.Location = source.Other.Location;
                deser.type = source.type;
                return deser;
            }

            public Profile(ProfileType t)
            {
                type = t;
                Spawn = new _Spawn(t);
                Behaviour = new _Behaviour(t);
                Death = new _Death(t);
                Other = new _Other(t);
            }

            public ProfileType type;
            public _Spawn Spawn;
            public _Behaviour Behaviour;
            public _Death Death;
            public _Other Other;

            public bool ShouldSerializetype()
            {
                Spawn.type = type;
                Behaviour.type = type;
                Death.type = type;
                Other.type = type;
                return true;
            }

            public class Base
            {
                [JsonIgnore] public ProfileType type;
            }

            public class _Spawn : Base
            {
                public _Spawn(ProfileType t) { type = t; }
                public bool AutoSpawn;
                public int Radius = 100;
                public List<string> BotNames = new List<string>();
                public string BotNamePrefix = String.Empty;
                public bool Keep_Default_Loadout;
                public List<string> Kit = new List<string>();
                public int Day_Time_Spawn_Amount = 5;
                public int Night_Time_Spawn_Amount = 0;
                public bool Announce_Spawn;
                public string Announcement_Text = String.Empty;
                public int BotHealth = 100;
                public bool Stationary;
                public bool UseCustomSpawns;
                public bool ChangeCustomSpawnOnDeath;

                public bool ShouldSerializeRadius() => type != ProfileType.Biome;
                public bool ShouldSerializeStationary() => type != ProfileType.Event;
                public bool ShouldSerializeUseCustomSpawns() => type != ProfileType.Event && type != ProfileType.Biome;
                public bool ShouldSerializeChangeCustomSpawnOnDeath() => type != ProfileType.Event && type != ProfileType.Biome;
            }

            public class _Behaviour : Base
            {
                public _Behaviour(ProfileType t) { type = t; }
                public int Roam_Range = 40;
                public int Aggro_Range = 30;
                public int DeAggro_Range = 40;
                public bool Peace_Keeper = true;
                public int Bot_Accuracy_Percent = 100;
                public int Bot_Damage_Percent = 50;
                public int Running_Speed_Booster = 10;
                public bool AlwaysUseLights;
                public bool Ignore_All_Players = false;
                public bool Ignore_Sleepers = true;
                public bool Target_Noobs = false;
                public double Melee_DamageScale = 1.0;
                public int Victim_Bleed_Amount_Per_Hit = 1;
                public int Victim_Bleed_Amount_Max = 100;
                public double RangeWeapon_DamageScale = 1.0;
                public ShouldAttack Target_ZombieHorde = 0;
                public ShouldAttack Target_HumanNPC = 0;
                public ShouldAttack Target_Other_Npcs = 0;
                public bool Respect_Safe_Zones = true;
                public int Faction = 0;
                public int SubFaction = 0;
            }

            public class _Death : Base
            {
                public _Death(ProfileType t) { type = t; }
                public int Spawn_Hackable_Death_Crate_Percent;
                public string Death_Crate_CustomLoot_Profile = "";
                public int Death_Crate_LockDuration = 10;
                public int Corpse_Duration = 1;
                public int Weapon_Drop_Percent = 0;
                public int Min_Weapon_Drop_Condition_Percent = 50;
                public int Max_Weapon_Drop_Condition_Percent = 100;
                public int Wipe_Main_Percent = 0;
                public int Wipe_Belt_Percent = 100;
                public int Wipe_Clothing_Percent = 100;
                public int Allow_Rust_Loot_Percent = 100;
                public string Rust_Loot_Source = "Default NPC";
                public int Respawn_Timer = 1;
                public double RustRewardsValue = 0.0;
                public double XPerienceValue = 0.0;
                public bool ShouldSerializeRespawn_Timer() => type != ProfileType.Event;
            }

            public class _Other : Base
            {
                public _Other(ProfileType t) { type = t; }
                public bool Chute;
                public int Backpack_Duration = 10;
                public int Suicide_Timer = 5;
                public bool Die_Instantly_From_Headshot = false;
                public bool Fire_Safe = true;
                public List<string> Instant_Death_From_Headshot_Allowed_Weapons = new List<string>();
                public bool Disable_Radio = true;
                public Vector3 Location;
                public string Parent_Monument = String.Empty;
                public bool Use_Map_Marker = false;
                public bool Always_Show_Map_Marker = false;
                public bool MurdererSound = false;
                public int Immune_From_Damage_Beyond = 400;
                public bool Short_Roam_Vision = false;
                public bool ShouldSerializeLocation() => type == ProfileType.Custom;
                public bool ShouldSerializeParent_Monument() => type == ProfileType.Custom;
            }
        }
        #endregion

        #region Messages     
        readonly Dictionary<string, string> Messages = new Dictionary<string, string>()
        {
            {"Title", "BotReSpawn : " },
            {"customsaved", "Custom Location Saved @ {0}" },
            {"ProfileMoved", "Custom Location {0} has been moved to your current position." },
            {"ParentSelected", "Parent Monument {0} set for profile {1}." },
            {"nonpc", "No BotReSpawn npc found directly in front of you." },
            {"noNavHere", "No navmesh was found at this location.\nConsider removing this point or using Stationary : true." },
            {"nospawns", "No custom spawn points were found for profile - {0}." },
            {"removednum", "Removed point {0} from {1}." },
            {"movedspawn", "Moved point {0} in {1}." },
            {"notthatmany", "Number of spawn points in {0} is less than {1}." },
            {"alreadyexists", "Custom Location already exists with the name {0}." },
            {"customremoved", "Custom Location {0} Removed." },
            {"deployed", "'{0}' bots deployed to {1}." },
            {"noprofile", "There is no profile by that name in default or custom profiles jsons." },
            {"nokits", "Kits is not installed but you have declared custom kits at {0}." },
            {"noWeapon", "A bot at {0} has no weapon. Check your kit {1} for a valid bullet or melee weapon." },
            {"numberOfBot", "There is {0} spawned bot alive." },
            {"numberOfBots", "There are {0} spawned bots alive." },
            {"dupID", "Duplicate userID save attempted. Please notify author." },
            {"NoBiomeSpawn", "Failed to find spawnpoints at {0}. Consider reducing npc numbers, or using custom profiles." },
            {"ToPlayer", "{0} npcs  have been sent to {1}" }
        };
        #endregion

        #region CUI
        const string Font = "robotocondensed-regular.ttf";
        public List<string> ValidKits = new List<string>();
        void OnPlayerDisconnected(BasePlayer player) => DestroyMenu(player, true);

        void DestroyMenu(BasePlayer player, bool all)
        {
            if (all)
                CuiHelper.DestroyUi(player, "BSBGUI");
            CuiHelper.DestroyUi(player, "BSKitsUI");
            CuiHelper.DestroyUi(player, "BSSpawnsUI");
            CuiHelper.DestroyUi(player, "BSMainUI");
            CuiHelper.DestroyUi(player, "BSBSOverridesUI");
            CuiHelper.DestroyUi(player, "BSUIToPlayerSelect");
            CuiHelper.DestroyUi(player, "BSShowParentsUI");
            CuiHelper.DestroyUi(player, "BSShowLootUI");
        }

        void BSBGUI(BasePlayer player)
        {
            if (player == null || configData == null)
                return;
            DestroyMenu(player, true);
            string guiString = string.Format("0.1 0.1 0.1 0.94");
            var elements = new CuiElementContainer();
            var mainName = elements.Add(new CuiPanel { Image = { Color = guiString }, RectTransform = { AnchorMin = "0.1 0.05", AnchorMax = "0.9 0.95" }, CursorEnabled = true }, "Overlay", "BSBGUI");
            elements.Add(new CuiButton { Button = { Color = "0 0 0 1" }, RectTransform = { AnchorMin = $"0 0.95", AnchorMax = $"0.999 1" }, Text = { Text = string.Empty } }, mainName);
            elements.Add(new CuiButton { Button = { Color = "0 0 0 1" }, RectTransform = { AnchorMin = $"0 0", AnchorMax = $"0.999 0.05" }, Text = { Text = string.Empty } }, mainName);
            elements.Add(new CuiButton { Button = { Command = "CloseBS", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = "0.955 0.96", AnchorMax = "0.99 0.99" }, Text = { Text = "X", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
            elements.Add(new CuiLabel { Text = { Text = "BotReSpawn", FontSize = 20, Font = Font, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = "0.2 0.95", AnchorMax = "0.8 1" } }, mainName);
            CuiHelper.AddUi(player, elements);
        }

        void BSMainUI(BasePlayer player, string tab, string profile = "", string sub = "", int page = 1)
        {
            DestroyMenu(player, false);
            var elements = new CuiElementContainer();
            var mainName = elements.Add(new CuiPanel { Image = { Color = "0 0 0 0" }, RectTransform = { AnchorMin = "0.1 0", AnchorMax = "0.9 0.9" }, CursorEnabled = true }, "Overlay", "BSMainUI");
            elements.Add(new CuiElement { Parent = "BSMainUI", Components = { new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" } } });

            float top = 0.875f;
            float bottom = 0.9f;

            var data = tab == "1" ? defaultData.Events.Concat(defaultData.Biomes.Concat(defaultData.Monuments)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value) : storedData.Profiles;
            data = data.Where(x => Spawners.ContainsKey(x.Key)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            bool odd = true;
            double left = 0;

            if (tab == string.Empty)
            {
                elements.Add(new CuiButton { Button = { Command = "", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0 0.95", AnchorMax = $"0.999 0.99" }, Text = { Text = $"BotReSpawn Settings", FontSize = 18, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);

                elements.Add(new CuiButton { Button = { Command = $"BSUIMain 0", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.45 0.775", AnchorMax = $"0.55 0.8" }, Text = { Text = $"Global settings", FontSize = 14, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                elements.Add(new CuiButton { Button = { Command = $"BSUIMain 1", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.45 0.725", AnchorMax = $"0.55 0.750" }, Text = { Text = $"Default profiles", FontSize = 14, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                elements.Add(new CuiButton { Button = { Command = $"BSUIMain 2", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.45 0.675", AnchorMax = $"0.55 0.700" }, Text = { Text = $"Custom profiles", FontSize = 14, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                elements.Add(new CuiButton { Button = { Command = $"BSUIShowAll", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.45 0.575", AnchorMax = $"0.55 0.600" }, Text = { Text = $"Show all profiles", FontSize = 14, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);

                if (Editing.ContainsKey(player.userID))
                {
                    string name = Editing[player.userID];
                    if (Profiles.ContainsKey(name))
                    {
                        elements.Add(new CuiLabel { Text = { Text = $"Addspawn command is enabled for profile {name}", FontSize = 14, Font = Font, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = $"0.35 0.425", AnchorMax = $"0.65 0.450" } }, mainName);
                        elements.Add(new CuiButton { Button = { Command = $"BSUIStopEdit", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.35 0.375", AnchorMax = $"0.65 0.400" }, Text = { Text = $"Stop editing {name}", FontSize = 14, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                        elements.Add(new CuiButton { Button = { Command = $"BSGotoProfile {(Profiles[name].type == ProfileType.Custom ? "2" : "1")} {RS(name)}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.35 0.325", AnchorMax = $"0.65 0.350" }, Text = { Text = $"Go to settings for {name}", FontSize = 14, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                    }
                    else
                        Editing.Remove(player.userID);
                }
            }
            else if (profile == string.Empty)
            {
                if (tab == "0")
                {
                    var conf = configData.Global;
                    elements.Add(new CuiButton { Button = { Command = "", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0 0.95", AnchorMax = $"0.999 0.99" }, Text = { Text = $"Global settings", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);

                    foreach (var setting in typeof(Global).GetFields())
                    {
                        var cat = setting.GetValue(conf);

                        top -= 0.025f;
                        bottom -= 0.025f;

                        if (odd)
                            elements.Add(new CuiPanel { Image = { Color = $"0 0 0 0.8" }, RectTransform = { AnchorMin = $"0 {top}", AnchorMax = $"0.999 {bottom}" }, CursorEnabled = true }, mainName);

                        elements.Add(new CuiLabel { Text = { Text = $"{setting.Name}", FontSize = 11, Font = Font, Align = TextAnchor.MiddleLeft }, RectTransform = { AnchorMin = $"0.25 {top}", AnchorMax = $"0.5 {bottom}" } }, mainName);

                        if (setting.FieldType == typeof(int))
                        {
                            elements.Add(new CuiButton { Button = { Command = $"BSConfChangeNum {RS(setting.Name)} false", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.55 {top + 0.003}", AnchorMax = $"0.57 {bottom - 0.003}" }, Text = { Text = "<", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                            elements.Add(new CuiLabel { Text = { Text = $"{cat}", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = $"0.575 {top + 0.003}", AnchorMax = $"0.625 {bottom - 0.003}" } }, mainName);
                            elements.Add(new CuiButton { Button = { Command = $"BSConfChangeNum {RS(setting.Name)} true", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.63 {top + 0.003}", AnchorMax = $"0.65 {bottom - 0.003}" }, Text = { Text = ">", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);

                        }
                        else if (setting.FieldType == typeof(bool))
                            elements.Add(new CuiButton { Button = { Command = $"BSConfChangeBool {RS(setting.Name)}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.55 {top + 0.003}", AnchorMax = $"0.65 {bottom - 0.003}" }, Text = { Text = $"{cat}", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                        odd = !odd;
                    }
                }
                else
                {
                    elements.Add(new CuiButton { Button = { Command = "", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0 0.95", AnchorMax = $"0.999 0.99" }, Text = { Text = tab == "1" ? "Default Profiles" : "Custom Profiles", FontSize = 18, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);

                    int counter = -1;
                    foreach (var entry in data)
                    {
                        counter++;
                        if (counter >= page * 120 || counter < (page * 120) - 120)
                            continue;

                        if (counter > 0 && counter % 30 == 0)
                        {
                            top = 0.875f;
                            bottom = 0.9f;
                            left += 0.25;
                        }

                        if (odd && left == 0)
                            elements.Add(new CuiPanel { Image = { Color = $"0 0 0 0.8" }, RectTransform = { AnchorMin = $"0 {top}", AnchorMax = $"0.999 {bottom}" }, CursorEnabled = true }, mainName);

                        elements.Add(new CuiButton { Button = { Command = $"BSUI {tab} {RS(entry.Key)} 0 0", Color = "0 0 0 0" }, RectTransform = { AnchorMin = $"{left + 0.05} {top}", AnchorMax = $"{left + 0.3} {bottom}" }, Text = { Text = $"{entry.Key}", Color = entry.Value.Spawn.AutoSpawn ? "0 1 0 1" : "1 1 1 1", FontSize = 11, Font = Font, Align = TextAnchor.MiddleLeft } }, mainName);

                        top -= 0.025f;
                        bottom -= 0.025f;
                        odd = !odd;
                    }
                }
                elements.Add(new CuiButton { Button = { Command = "BSUIMain", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.4 0.065", AnchorMax = $"0.6 0.095" }, Text = { Text = $"Back", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);

                if (page > 1)
                    elements.Add(new CuiButton { Button = { Command = $"BSUIPage {tab} {page - 1}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.15 0.065", AnchorMax = $"0.3 0.095" }, Text = { Text = $"<-", FontSize = 18, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);

                if (data.Count > page * 120)
                    elements.Add(new CuiButton { Button = { Command = $"BSUIPage {tab} {page + 1}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.7 0.065", AnchorMax = $"0.85 0.095" }, Text = { Text = $"->", FontSize = 18, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
            }
            else
            {
                var entry = data[RD(profile)];
                elements.Add(new CuiButton { Button = { Command = "", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0 0.95", AnchorMax = $"0.999 0.99" }, Text = { Text = $"{RD(profile)}", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);

                float l = 0.12f;
                float r = 0.27f;

                foreach (var category in typeof(Profile).GetFields())
                {
                    if (category.Name == "type")
                        continue;
                    if (sub == "0")
                        sub = category.Name;
                    elements.Add(new CuiButton { Button = { Command = $"BSUI {tab} {RS(profile)} {category.Name} 0", Color = configData.UI.ButtonColour2 }, RectTransform = { AnchorMin = $"{l} 0.91", AnchorMax = $"{r} 0.935" }, Text = { Text = $"{category.Name}", FontSize = 14, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);

                    l += 0.2f;
                    r += 0.2f;


                    if (category.Name != sub)
                        continue;

                    foreach (var setting in category.FieldType.GetFields())
                    {
                        var cat = category.GetValue(entry);
                        if (setting.Name == "type" || setting.FieldType == typeof(List<string>))
                            continue;

                        if ((entry.type == ProfileType.Biome || entry.type == ProfileType.Event) && setting.Name.Contains("CustomSpawn"))
                            continue;

                        if (entry.type == ProfileType.Biome && setting.Name == "Radius")
                            continue;

                        if (entry.type == ProfileType.Event && setting.Name == "Respawn_Timer")
                            continue;

                        top -= 0.025f;
                        bottom -= 0.025f;

                        if (odd)
                            elements.Add(new CuiPanel { Image = { Color = $"0 0 0 0.8" }, RectTransform = { AnchorMin = $"0 {top}", AnchorMax = $"0.999 {bottom}" }, CursorEnabled = true }, mainName);

                        if (setting.FieldType == typeof(int))
                        {
                            elements.Add(new CuiLabel { Text = { Text = $"{setting.Name}", FontSize = 11, Font = Font, Align = TextAnchor.MiddleLeft }, RectTransform = { AnchorMin = $"0.02 {top}", AnchorMax = $"0.3 {bottom}" } }, mainName);

                            elements.Add(new CuiButton { Button = { Command = $"BSChangeNum {tab} {RS(profile)} {RS(category.Name)} {RS(setting.Name)} false", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.25 {top + 0.003}", AnchorMax = $"0.27 {bottom - 0.003}" }, Text = { Text = "<", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                            elements.Add(new CuiLabel { Text = { Text = $"{setting.GetValue(cat)}", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = $"0.275 {top + 0.003}", AnchorMax = $"0.325 {bottom - 0.003}" } }, mainName);
                            elements.Add(new CuiButton { Button = { Command = $"BSChangeNum {tab} {RS(profile)} {RS(category.Name)} {RS(setting.Name)} true", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.33 {top + 0.003}", AnchorMax = $"0.35 {bottom - 0.003}" }, Text = { Text = ">", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                        }
                        else if (setting.FieldType == typeof(ShouldAttack))
                        {
                            elements.Add(new CuiLabel { Text = { Text = $"{setting.Name}", FontSize = 11, Font = Font, Align = TextAnchor.MiddleLeft }, RectTransform = { AnchorMin = $"0.02 {top}", AnchorMax = $"0.3 {bottom}" } }, mainName);

                            elements.Add(new CuiButton { Button = { Command = $"BSChangeEnum {tab} {RS(profile)} {RS(category.Name)} {RS(setting.Name)} false", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.25 {top + 0.003}", AnchorMax = $"0.27 {bottom - 0.003}" }, Text = { Text = "<", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                            elements.Add(new CuiLabel { Text = { Text = $"{setting.GetValue(cat)}", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = $"0.275 {top + 0.003}", AnchorMax = $"0.325 {bottom - 0.003}" } }, mainName);
                            elements.Add(new CuiButton { Button = { Command = $"BSChangeEnum {tab} {RS(profile)} {RS(category.Name)} {RS(setting.Name)} true", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.33 {top + 0.003}", AnchorMax = $"0.35 {bottom - 0.003}" }, Text = { Text = ">", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                        }
                        else if (setting.FieldType == typeof(double))
                        {
                            elements.Add(new CuiLabel { Text = { Text = $"{setting.Name}", FontSize = 11, Font = Font, Align = TextAnchor.MiddleLeft }, RectTransform = { AnchorMin = $"0.02 {top}", AnchorMax = $"0.3 {bottom}" } }, mainName);

                            elements.Add(new CuiButton { Button = { Command = $"BSChangeDouble {tab} {RS(profile)} {RS(category.Name)} {RS(setting.Name)} false", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.25 {top + 0.003}", AnchorMax = $"0.27 {bottom - 0.003}" }, Text = { Text = "<", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                            elements.Add(new CuiLabel { Text = { Text = $"{setting.GetValue(cat)}", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = $"0.275 {top + 0.003}", AnchorMax = $"0.325 {bottom - 0.003}" } }, mainName);
                            elements.Add(new CuiButton { Button = { Command = $"BSChangeDouble {tab} {RS(profile)} {RS(category.Name)} {RS(setting.Name)} true", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.33 {top + 0.003}", AnchorMax = $"0.35 {bottom - 0.003}" }, Text = { Text = ">", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                        }
                        else if (setting.FieldType == typeof(bool))
                        {
                            elements.Add(new CuiLabel { Text = { Text = $"{setting.Name}", FontSize = 11, Font = Font, Align = TextAnchor.MiddleLeft }, RectTransform = { AnchorMin = $"0.02 {top}", AnchorMax = $"0.3 {bottom}" } }, mainName);
                            elements.Add(new CuiButton { Button = { Command = $"BSChangeBool {tab} {RS(profile)} {RS(category.Name)} {RS(setting.Name)}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.25 {top + 0.003}", AnchorMax = $"0.35 {bottom - 0.003}" }, Text = { Text = $"{setting.GetValue(cat)}", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                        }
                        else if (setting.FieldType == typeof(string))
                        {
                            if (setting.Name == "Parent_Monument" && entry.type == ProfileType.Custom)
                            {
                                elements.Add(new CuiLabel { Text = { Text = $"{setting.Name}", FontSize = 11, Font = Font, Align = TextAnchor.MiddleLeft }, RectTransform = { AnchorMin = $"0.02 {top}", AnchorMax = $"0.3 {bottom}" } }, mainName);
                                elements.Add(new CuiButton { Button = { Command = $"BSShowParents {tab} {RS(profile)} {RS(category.Name)} {RS(setting.Name)}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.25 {top + 0.003}", AnchorMax = $"0.35 {bottom - 0.003}" }, Text = { Text = $"{(setting.GetValue(cat).ToString() == string.Empty ? "Select" : setting.GetValue(cat))}", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                            }
                            else if (setting.Name == "Rust_Loot_Source")
                            {
                                elements.Add(new CuiLabel { Text = { Text = $"{setting.Name}", FontSize = 11, Font = Font, Align = TextAnchor.MiddleLeft }, RectTransform = { AnchorMin = $"0.02 {top}", AnchorMax = $"0.3 {bottom}" } }, mainName);
                                elements.Add(new CuiButton { Button = { Command = $"BSShowLoot {tab} {RS(profile)} {RS(category.Name)} {RS(setting.Name)}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.25 {top + 0.003}", AnchorMax = $"0.35 {bottom - 0.003}" }, Text = { Text = $"{(setting.GetValue(cat).ToString() == string.Empty ? "Select" : setting.GetValue(cat))}", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                            }
                            else
                            {
                                elements.Add(new CuiLabel { Text = { Text = $"{setting.Name}", FontSize = 11, Font = Font, Align = TextAnchor.MiddleLeft }, RectTransform = { AnchorMin = $"0.02 {top}", AnchorMax = $"0.3 {bottom}" } }, mainName);
                                elements.Add(new CuiLabel { Text = { Text = $"{(setting.Name == "Location" ? setting.GetValue(cat) : "Edit in json")}", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = $"0.25 {top + 0.003}", AnchorMax = $"0.35 {bottom - 0.003}" } }, mainName);
                            }
                        }
                        else
                        {
                            elements.Add(new CuiLabel { Text = { Text = $"{setting.Name}", FontSize = 11, Font = Font, Align = TextAnchor.MiddleLeft }, RectTransform = { AnchorMin = $"0.02 {top}", AnchorMax = $"0.3 {bottom}" } }, mainName);
                            elements.Add(new CuiLabel { Text = { Text = $"{(setting.Name == "Location" ? setting.GetValue(cat) : "Edit in json")}", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = $"0.25 {top + 0.003}", AnchorMax = $"0.35 {bottom - 0.003}" } }, mainName);
                        }
                        odd = !odd;
                    }
                }

                if (data[RD(profile)].type == ProfileType.Custom || data[RD(profile)].type == ProfileType.Monument)
                {
                    elements.Add(new CuiPanel { Image = { Color = $"0 0 0 0.8" }, RectTransform = { AnchorMin = $"0.06 0.12", AnchorMax = $"0.34 0.21" }, CursorEnabled = true }, mainName);
                    elements.Add(new CuiButton { Button = { Command = $"BSUIEditSpawns {RS(profile)} 0", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.1 0.17", AnchorMax = $"0.3 0.20" }, Text = { Text = "Edit Spawnpoints", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                    if (data[RD(profile)].type == ProfileType.Custom)
                        elements.Add(new CuiButton { Button = { Command = $"BSUIMoveProfile {RS(profile)}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.1 0.13", AnchorMax = $"0.3 0.16" }, Text = { Text = "Move profile here", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                }

                elements.Add(new CuiPanel { Image = { Color = $"0 0 0 0.8" }, RectTransform = { AnchorMin = $"0.36 0.12", AnchorMax = $"0.64 0.25" }, CursorEnabled = true }, mainName);

                elements.Add(new CuiButton { Button = { Command = $"BSUIReload {RS(profile)}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.4 0.21", AnchorMax = $"0.6 0.24" }, Text = { Text = "Reload profile", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                elements.Add(new CuiButton { Button = { Command = $"BSUIEditKits {RS(profile)}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.4 0.17", AnchorMax = $"0.6 0.20" }, Text = { Text = "Edit Kits", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                elements.Add(new CuiButton { Button = { Command = $"BSUIToPlayerSelect {RS(profile)}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.4 0.13", AnchorMax = $"0.6 0.16" }, Text = { Text = "Send to player", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);

                elements.Add(new CuiPanel { Image = { Color = $"0 0 0 0.8" }, RectTransform = { AnchorMin = $"0.66 0.12", AnchorMax = $"0.94 0.21" }, CursorEnabled = true }, mainName);

                elements.Add(new CuiButton { Button = { Command = $"BSCopy {tab} {RS(profile)} {sub}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.7 0.17", AnchorMax = $"0.9 0.20" }, Text = { Text = "Copy settings", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                if (Copy.ContainsKey(player.userID))
                    elements.Add(new CuiButton { Button = { Command = $"BSPaste {tab} {RS(profile)} {sub}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.7 0.13", AnchorMax = $"0.9 0.16" }, Text = { Text = $"Paste {Copy[player.userID]}", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);

                if (data[RD(profile)].type == ProfileType.Custom)
                    elements.Add(new CuiButton { Button = { Command = $"BSUIRemoveProfile {RS(profile)}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.01 0.065", AnchorMax = $"0.16 0.095" }, Text = { Text = "Delete Profile", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);

                elements.Add(new CuiButton { Button = { Command = $"BSUIMain {tab}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.4 0.065", AnchorMax = $"0.6 0.095" }, Text = { Text = "Back", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);

            }
            CuiHelper.AddUi(player, elements);
        }

        void SetProfile(string profile, Profile p)
        {
            switch (p.type)
            {
                case ProfileType.Monument:
                    defaultData.Monuments[profile] = p;
                    break;
                case ProfileType.Biome:
                    defaultData.Biomes[profile] = p;
                    break;
                case ProfileType.Event:
                    defaultData.Events[profile] = p;
                    break;
                case ProfileType.Custom:
                    storedData.Profiles[profile] = p;
                    break;
            }
        }

        void BSKitsUI(BasePlayer player, string profile = "", int spawnpoint = -1)
        {
            DestroyMenu(player, false);
            var elements = new CuiElementContainer();
            var mainName = elements.Add(new CuiPanel { Image = { Color = "0 0 0 0" }, RectTransform = { AnchorMin = "0.1 0", AnchorMax = "0.9 0.9" }, CursorEnabled = true }, "Overlay", "BSKitsUI");
            elements.Add(new CuiElement { Parent = "BSKitsUI", Components = { new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" } } });
            string text = $"Kits for {profile}";
            if (spawnpoint > -1)
                text += $" - spawnpoint {spawnpoint}";
            elements.Add(new CuiLabel { Text = { Text = text, FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = $"0 0.9", AnchorMax = $"1 1" } }, mainName);

            float top = 0.925f;
            float bottom = 0.95f;
            double left = 0;

            List<string> k = spawnpoint == -1 ? GetProfile(profile).Spawn.Kit : spawnsData.CustomSpawnLocations[profile][spawnpoint].Kits;
            int num = 0;

            for (int i = 0; i < ValidKits.Count; i++)
            {
                if (i > 0 && i % 35 == 0)
                {
                    top = 0.925f;
                    bottom = 0.95f;
                    left += 0.25;
                }
                if (i > 139)
                    break;

                top -= 0.023f;
                bottom -= 0.023f;
                if (spawnpoint > -1)
                {
                    elements.Add(new CuiLabel { Text = { Text = $"{ValidKits[i]}", FontSize = 11, Font = Font, Align = TextAnchor.MiddleLeft }, RectTransform = { AnchorMin = $"{left + 0.05} {top}", AnchorMax = $"{left + 0.15} {bottom}" } }, mainName);
                    elements.Add(new CuiButton { Button = { Command = $"BSChangeSPKit {RS(profile)} {i} {spawnpoint} false", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"{left + 0.15} {top + 0.003}", AnchorMax = $"{left + 0.17} {bottom - 0.003}" }, Text = { Text = "<", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                    elements.Add(new CuiLabel { Text = { Text = $"{k.Where(x => x == ValidKits[i]).Count()}", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = $"{left + 0.175} {top + 0.003}", AnchorMax = $"{left + 0.185} {bottom - 0.003}" } }, mainName);
                    elements.Add(new CuiButton { Button = { Command = $"BSChangeSPKit {RS(profile)} {i} {spawnpoint} true", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"{left + 0.19} {top + 0.003}", AnchorMax = $"{left + 0.21} {bottom - 0.003}" }, Text = { Text = ">", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                }
                else
                {
                    num = k.Where(x => x == ValidKits[i]).Count();
                    elements.Add(new CuiLabel { Text = { Text = $"{ValidKits[i]}", FontSize = 11, Font = Font, Align = TextAnchor.MiddleLeft }, RectTransform = { AnchorMin = $"{left + 0.05} {top}", AnchorMax = $"{left + 0.15} {bottom}" } }, mainName);
                    elements.Add(new CuiButton { Button = { Command = $"BSChangeKit {RS(profile)} {i} false", Color = num > 0 ? configData.UI.ButtonColour2 : configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"{left + 0.15} {top + 0.003}", AnchorMax = $"{left + 0.17} {bottom - 0.003}" }, Text = { Text = "<", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                    elements.Add(new CuiLabel { Text = { Text = $"{num}", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = $"{left + 0.175} {top + 0.003}", AnchorMax = $"{left + 0.185} {bottom - 0.003}" } }, mainName);
                    elements.Add(new CuiButton { Button = { Command = $"BSChangeKit {RS(profile)} {i} true", Color = num > 0 ? configData.UI.ButtonColour2 : configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"{left + 0.19} {top + 0.003}", AnchorMax = $"{left + 0.21} {bottom - 0.003}" }, Text = { Text = ">", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                }
            }
            elements.Add(new CuiButton { Button = { Command = $"CloseExtra BSKitsUI {RS(profile)} {spawnpoint}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.4 0.065", AnchorMax = $"0.6 0.095" }, Text = { Text = "Back", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
            CuiHelper.AddUi(player, elements);
        }

        void BSSpawnsUI(BasePlayer player, string profile = "", int page = 0)
        {
            DestroyMenu(player, false);
            var elements = new CuiElementContainer();
            var mainName = elements.Add(new CuiPanel { Image = { Color = "0 0 0 0" }, RectTransform = { AnchorMin = "0.1 0", AnchorMax = "0.9 0.9" }, CursorEnabled = true }, "Overlay", "BSSpawnsUI");
            elements.Add(new CuiElement { Parent = "BSSpawnsUI", Components = { new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" } } });
            elements.Add(new CuiLabel { Text = { Text = $"Spawn points for {profile}", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = $"0 0.9", AnchorMax = $"1 1" } }, mainName);

            float top = 0.875f;
            float bottom = 0.9f;
            double left = 0;

            var s = spawnsData.CustomSpawnLocations[profile];
            if (s.Count > 0)
            {
                s = s.GetRange(page * 54, Mathf.Min(s.Count() - page * 54, 54));

                if (s.Count() == 0)
                {
                    page--;
                    s = spawnsData.CustomSpawnLocations[profile];
                    s = s.GetRange(page * 54, Mathf.Min(s.Count() - page * 54, 54));
                }
            }

            if (s.Count < 27)
                left = 0.25;

            for (int i = 0; i < s.Count; i++)
            {
                if (i == 27)
                {
                    top = 0.875f;
                    bottom = 0.9f;
                    left = 0.5;
                }
                if (i > 53)
                    break;

                top -= 0.025f;
                bottom -= 0.025f;
                elements.Add(new CuiLabel { Text = { Text = $"{i + (page * 54)}", FontSize = 11, Font = Font, Align = TextAnchor.MiddleLeft }, RectTransform = { AnchorMin = $"{left + 0.08} {top}", AnchorMax = $"{left + 0.10} {bottom}" } }, mainName);
                elements.Add(new CuiButton { Button = { Command = $"BSUIMoveSpawn {RS(profile)} {i + (page * 54)} true {page}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"{left + 0.11} {top + 0.003}", AnchorMax = $"{left + 0.16} {bottom - 0.003}" }, Text = { Text = "Remove", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                elements.Add(new CuiButton { Button = { Command = $"BSUIMoveSpawn {RS(profile)} {i + (page * 54)} false {page}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"{left + 0.17} {top + 0.003}", AnchorMax = $"{left + 0.23} {bottom - 0.003}" }, Text = { Text = "Move here", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                elements.Add(new CuiButton { Button = { Command = $"BSEditOverRides {RS(profile)} {i + (page * 54)}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"{left + 0.24} {top + 0.003}", AnchorMax = $"{left + 0.31} {bottom - 0.003}" }, Text = { Text = "Edit overrides", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
            }

            elements.Add(new CuiButton { Button = { Command = $"BSUISetEditing {RS(profile)}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.3 0.16", AnchorMax = $"0.49 0.19" }, Text = { Text = "Add by command", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
            elements.Add(new CuiButton { Button = { Command = $"BSUIAddSpawn {RS(profile)} {page}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.3 0.12", AnchorMax = $"0.49 0.15" }, Text = { Text = "Add spawnpoint here", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
            elements.Add(new CuiButton { Button = { Command = $"BSUIShowSpawns {RS(profile)}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.51 0.16", AnchorMax = $"0.7 0.19" }, Text = { Text = "Show all spawnpoints", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
            elements.Add(new CuiButton { Button = { Command = $"BSUICheckNav", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.51 0.12", AnchorMax = $"0.7 0.15" }, Text = { Text = "Check for Navmesh", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);

            if (page > 0)
                elements.Add(new CuiButton { Button = { Command = $"BSUIEditSpawns {RS(profile)} {page - 1}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.2 0.065", AnchorMax = $"0.3 0.095" }, Text = { Text = "<-", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
            if (spawnsData.CustomSpawnLocations[profile].Count() > (page + 1) * 54)
                elements.Add(new CuiButton { Button = { Command = $"BSUIEditSpawns {RS(profile)} {page + 1}", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.7 0.065", AnchorMax = $"0.8 0.095" }, Text = { Text = "->", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);

            elements.Add(new CuiButton { Button = { Command = $"CloseExtra BSSpawnsUI {RS(profile)} -1", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.4 0.065", AnchorMax = $"0.6 0.095" }, Text = { Text = "Back", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);

            CuiHelper.AddUi(player, elements);
        }


        void BSOverridesUI(BasePlayer player, string profile = "", int spawnpoint = 0)
        {
            CuiHelper.DestroyUi(player, "BSBSOverridesUI");
            var elements = new CuiElementContainer();
            var mainName = elements.Add(new CuiPanel { Image = { Color = "0 0 0 0.96" }, RectTransform = { AnchorMin = "0.2 0.1", AnchorMax = "0.8 0.8" }, CursorEnabled = true }, "Overlay", "BSBSOverridesUI");
            elements.Add(new CuiElement { Parent = "BSBSOverridesUI", Components = { new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" } } });
            elements.Add(new CuiLabel { Text = { Text = $"Overrides for spawn point {spawnpoint}", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = $"0 0.9", AnchorMax = $"1 1" } }, mainName);

            var s = spawnsData.CustomSpawnLocations[profile][spawnpoint];
            elements.Add(new CuiLabel { Text = { Text = $"Enable overrides", FontSize = 11, Font = Font, Align = TextAnchor.MiddleLeft }, RectTransform = { AnchorMin = $"0.4 0.76", AnchorMax = $"0.5 0.8" } }, mainName);
            elements.Add(new CuiButton { Button = { Command = $"BSChangeOverRides {RS(profile)} {spawnpoint} 0 true", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.5 0.76", AnchorMax = $"0.6 0.8" }, Text = { Text = s.UseOverrides.ToString(), FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);

            if (s.UseOverrides)
            {
                elements.Add(new CuiLabel { Text = { Text = $"Stationary", FontSize = 11, Font = Font, Align = TextAnchor.MiddleLeft }, RectTransform = { AnchorMin = $"0.4 0.7", AnchorMax = $"0.5 0.74" } }, mainName);
                elements.Add(new CuiButton { Button = { Command = $"BSChangeOverRides {RS(profile)} {spawnpoint} 1 true", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.5 0.7", AnchorMax = $"0.6 0.74" }, Text = { Text = s.Stationary.ToString(), FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);

                elements.Add(new CuiLabel { Text = { Text = $"Health", FontSize = 11, Font = Font, Align = TextAnchor.MiddleLeft }, RectTransform = { AnchorMin = $"0.4 0.64", AnchorMax = $"0.5 0.68" } }, mainName);
                elements.Add(new CuiButton { Button = { Command = $"BSChangeOverRides {RS(profile)} {spawnpoint} 2 false", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.5 0.64", AnchorMax = $"0.53 0.68" }, Text = { Text = "<", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                elements.Add(new CuiLabel { Text = { Text = s.Health.ToString(), FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = $"0.54 0.64", AnchorMax = $"0.56 0.68" } }, mainName);
                elements.Add(new CuiButton { Button = { Command = $"BSChangeOverRides {RS(profile)} {spawnpoint} 2 true", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.57 0.64", AnchorMax = $"0.6 0.68" }, Text = { Text = ">", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);

                elements.Add(new CuiLabel { Text = { Text = $"Roam range", FontSize = 11, Font = Font, Align = TextAnchor.MiddleLeft }, RectTransform = { AnchorMin = $"0.4 0.58", AnchorMax = $"0.5 0.62" } }, mainName);
                elements.Add(new CuiButton { Button = { Command = $"BSChangeOverRides {RS(profile)} {spawnpoint} 3 false", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.5 0.58", AnchorMax = $"0.53 0.62" }, Text = { Text = "<", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
                elements.Add(new CuiLabel { Text = { Text = s.RoamRange.ToString(), FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = $"0.54 0.58", AnchorMax = $"0.56 0.62" } }, mainName);
                elements.Add(new CuiButton { Button = { Command = $"BSChangeOverRides {RS(profile)} {spawnpoint} 3 true", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.57 0.58", AnchorMax = $"0.6 0.62" }, Text = { Text = ">", FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);

                elements.Add(new CuiButton { Button = { Command = $"BSUIEditSPKits {RS(profile)} {spawnpoint} ", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.4 0.52", AnchorMax = $"0.6 0.56" }, Text = { Text = "Kits", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
            }

            elements.Add(new CuiButton { Button = { Command = $"BSCloseOverrides", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.4 0.015", AnchorMax = $"0.6 0.05" }, Text = { Text = "Save", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
            CuiHelper.AddUi(player, elements);
        }


        void BSToPlayerUI(BasePlayer player, string profile)
        {
            CuiHelper.DestroyUi(player, "BSUIToPlayerSelect");
            var elements = new CuiElementContainer();
            var mainName = elements.Add(new CuiPanel { Image = { Color = "0 0 0 0.98" }, RectTransform = { AnchorMin = "0.1 0.05", AnchorMax = "0.9 0.9" }, CursorEnabled = true }, "Overlay", "BSUIToPlayerSelect");
            elements.Add(new CuiElement { Parent = "BSUIToPlayerSelect", Components = { new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" } } });
            elements.Add(new CuiLabel { Text = { Text = $"Spawn {profile} npcs near a player.", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = $"0 0.9", AnchorMax = $"1 1" } }, mainName);

            float top = 0.925f;
            float bottom = 0.95f;
            double left = 0;
            bool odd = true;
            var players = BasePlayer.activePlayerList;

            if (players.Count < 34)
                left = 0.4;

            for (int i = 0; i < players.Count; i++)
            {
                if (i == 165)
                    break;

                if (i > 0 && i % 33 == 0 && left < 0.79)
                {
                    top = 0.925f;
                    bottom = 0.95f;
                    left += 0.2;
                }

                top -= 0.025f;
                bottom -= 0.025f;
                if (odd && left == 0)
                    elements.Add(new CuiPanel { Image = { Color = $"0 0 0 0.8" }, RectTransform = { AnchorMin = $"0 {top}", AnchorMax = $"0.999 {bottom - 0.005}" }, CursorEnabled = true }, mainName);

                elements.Add(new CuiButton { Button = { Command = $"BSUIToPlayer {RS(profile)} {players[i].userID} ", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"{left + 0.02} {top}", AnchorMax = $"{left + 0.18} {bottom - 0.005}" }, Text = { Text = players[i].displayName, FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
            }
            elements.Add(new CuiButton { Button = { Command = $"BSUICloseToPlayer", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.4 0.015", AnchorMax = $"0.6 0.05" }, Text = { Text = "Close", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
            CuiHelper.AddUi(player, elements);
        }

        void BSShowParentsUI(BasePlayer player, string tab, string profile, string category)
        {
            CuiHelper.DestroyUi(player, "BSShowParentsUI");
            var elements = new CuiElementContainer();
            var mainName = elements.Add(new CuiPanel { Image = { Color = "0 0 0 0.98" }, RectTransform = { AnchorMin = "0.1 0.05", AnchorMax = "0.9 0.9" }, CursorEnabled = true }, "Overlay", "BSShowParentsUI");
            elements.Add(new CuiElement { Parent = "BSShowParentsUI", Components = { new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" } } });
            elements.Add(new CuiLabel { Text = { Text = $"Select parent monument for {profile}.", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = $"0 0.9", AnchorMax = $"1 1" } }, mainName);

            float top = 0.925f;
            float bottom = 0.95f;
            double left = 0;
            bool odd = true;

            var mons = Spawners.Where(x => x.Value[0].profile.type == ProfileType.Monument).ToList();
            for (int i = 0; i < mons.Count(); i++)
            {
                if (i == 165)
                    break;

                if (i > 0 && i % 33 == 0 && left < 0.79)
                {
                    top = 0.925f;
                    bottom = 0.95f;
                    left += 0.2;
                }

                top -= 0.025f;
                bottom -= 0.025f;
                if (odd && left == 0)
                    elements.Add(new CuiPanel { Image = { Color = $"0 0 0 0.8" }, RectTransform = { AnchorMin = $"0 {top}", AnchorMax = $"0.999 {bottom - 0.005}" }, CursorEnabled = true }, mainName);

                elements.Add(new CuiButton { Button = { Command = $"BSSelectParent {tab} {RS(profile)} {RS(category)} {RS(mons[i].Key)} ", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"{left + 0.02} {top}", AnchorMax = $"{left + 0.18} {bottom - 0.005}" }, Text = { Text = mons[i].Key, FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
            }
            elements.Add(new CuiButton { Button = { Command = $"BSUICloseParentMonument", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.4 0.015", AnchorMax = $"0.6 0.05" }, Text = { Text = "Close", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
            CuiHelper.AddUi(player, elements);
        }

        void BSShowLootUI(BasePlayer player, string tab, string profile, string category)
        {
            CuiHelper.DestroyUi(player, "BSShowLootUI");
            var elements = new CuiElementContainer();
            var mainName = elements.Add(new CuiPanel { Image = { Color = "0 0 0 0.98" }, RectTransform = { AnchorMin = "0.1 0.05", AnchorMax = "0.9 0.9" }, CursorEnabled = true }, "Overlay", "BSShowLootUI");
            elements.Add(new CuiElement { Parent = "BSShowLootUI", Components = { new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" } } });
            elements.Add(new CuiLabel { Text = { Text = $"Select vanilla loot table for {profile}.", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = $"0 0.9", AnchorMax = $"1 1" } }, mainName);

            float top = 0.925f;
            float bottom = 0.95f;
            double left = 0;
            bool odd = true;

            var containers = Containers.Where(x => x.Key == "Default NPC" || x.Key == "ScarecrowNPC" || x.Value != null).ToList().OrderBy(x => x.Key).ToList();
            for (int i = 0; i < containers.Count(); i++)
            {
                if (i > 0 && i % 32 == 0)
                {
                    top = 0.925f;
                    bottom = 0.95f;
                    left += 0.2;
                }

                top -= 0.025f;
                bottom -= 0.025f;
                if (odd && left == 0)
                    elements.Add(new CuiPanel { Image = { Color = $"0 0 0 0.8" }, RectTransform = { AnchorMin = $"0 {top}", AnchorMax = $"0.999 {bottom - 0.005}" }, CursorEnabled = true }, mainName);

                elements.Add(new CuiButton { Button = { Command = $"BSSelectLoot {tab} {RS(profile)} {RS(category)} {RS(containers[i].Key)} ", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"{left + 0.02} {top}", AnchorMax = $"{left + 0.18} {bottom - 0.005}" }, Text = { Text = containers[i].Key, FontSize = 11, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
            }
            elements.Add(new CuiButton { Button = { Command = $"BSUICloseLoot", Color = configData.UI.ButtonColour }, RectTransform = { AnchorMin = $"0.4 0.015", AnchorMax = $"0.6 0.05" }, Text = { Text = "Close", FontSize = 16, Font = Font, Align = TextAnchor.MiddleCenter } }, mainName);
            CuiHelper.AddUi(player, elements);
        }
        #endregion

        #region UICommands
        void MsgUI(BasePlayer player, string message, float duration = 1.5f)
        {
            CuiHelper.DestroyUi(player, "msgui");
            timer.Once(duration, () =>
            {
                if (player != null)
                    CuiHelper.DestroyUi(player, "msgui");
            });
            var elements = new CuiElementContainer();
            var mainName = elements.Add(new CuiPanel { Image = { FadeIn = 0.3f, Color = $"0.1 0.1 0.1 0.9" }, RectTransform = { AnchorMin = "0.1 0.63", AnchorMax = "0.9 0.77" }, CursorEnabled = false, FadeOut = 0.3f }, "Overlay", "msgui");
            elements.Add(new CuiLabel { FadeOut = 0.5f, Text = { FadeIn = 0.5f, Text = message, Color = "1 1 1 1", FontSize = 26, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" } }, mainName);

            CuiHelper.AddUi(player, elements);
        }

        [ConsoleCommand("BSCopy")]
        private void BSCopy(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            DestroyMenu(arg.Player(), false);
            Copy[arg.Player().userID] = RD(arg.Args[1]);
            BSMainUI(arg.Player(), arg.Args[0], RD(arg.Args[1]), arg.Args[2], 0);
        }
        [ConsoleCommand("BSPaste")]
        private void BSPaste(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            DestroyMenu(arg.Player(), false);
            SetProfile(RD(arg.Args[1]), GetProfile(Copy[arg.Player().userID]).Clone(GetProfile(RD(arg.Args[1]))));
            BSMainUI(arg.Player(), arg.Args[0], RD(arg.Args[1]), arg.Args[2], 0);
        }

        [ConsoleCommand("BSUICloseToPlayer")]
        private void BSUICloseToPlayer(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null)
                CuiHelper.DestroyUi(arg.Player(), "BSUIToPlayerSelect");
        }

        [ConsoleCommand("BSUIToPlayerSelect")]
        private void BSUIToPlayerSelect(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null)
                BSToPlayerUI(arg.Player(), arg.Args[0]);
        }

        [ConsoleCommand("BSUIToPlayer")]
        private void BSUIToPlayer(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;

            BasePlayer target = GetPlayer(arg.Args[1]);
            if (target == null)
                return;

            if (!SpawnToPlayer(target, RD(arg.Args[0]), -1))
            {
                MsgUI(arg.Player(), lang.GetMessage("noprofile", this));
                SendReply(arg.Player(), TitleText + lang.GetMessage("noprofile", this));
            }
            CuiHelper.DestroyUi(arg.Player(), "BSUIToPlayerSelect");
        }

        [ConsoleCommand("botrespawn")]
        private void botrespawn(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null && !HasPermission(arg.Player().UserIDString, permAllowed) && !arg.Player().IsAdmin)
                return;
            if (arg?.Args == null)
                return;

            if (arg.Args.Length > 2 && arg.Args[0] == "toplayer")
            {
                int num = -1;
                if (arg.Args.Length == 4)
                    int.TryParse(arg.Args[3], out num);
                BasePlayer target = GetPlayer(arg.Args[1]);
                if (target == null)
                    return;

                if (!SpawnToPlayer(target, arg.Args[2], num))
                    Puts(lang.GetMessage("noprofile", this));
            }

            if (arg.Args.Length == 2)
            {
                if (!Profiles.ContainsKey(arg.Args[1]))
                    return;
                ReloadData(arg.Args[1], false, arg.Args[0] == "disable" ? false : true);
            }
        }

        BasePlayer GetPlayer(string name)
        {
            BasePlayer target = FindPlayerByName(name);
            if (target == null)
                target = BasePlayer.Find(name);
            if (target == null)
                Puts($"No player found for {name}");
            return target;
        }

        bool SpawnToPlayer(BasePlayer target, string profile, int num)
        {
            foreach (var entry in Profiles.Where(entry => entry.Key == profile))
            {
                CreateTempSpawnGroup(target.transform.position, entry.Key, entry.Value, null, num == -1 ? IsNight ? entry.Value.Spawn.Night_Time_Spawn_Amount : entry.Value.Spawn.Day_Time_Spawn_Amount : num);
                Puts(String.Format(lang.GetMessage("deployed", this), entry.Key, target.displayName));
                if (configData.Global.Announce_Toplayer)
                    bs.PrintToChat(string.Format(lang.GetMessage("ToPlayer", this), profile, target.displayName));
                return true;
            }
            return false;
        }

        [ConsoleCommand("BSUIEditSPKits")]
        private void BSUIEditSPKits(ConsoleSystem.Arg arg)
        {
            if (ValidKits.Count == 0)
            {
                MsgUI(arg.Player(), "There are no valid kits", 3f);
                return;
            }

            if (arg.Player() == null)
                return;
            BSKitsUI(arg.Player(), RD(arg.Args[0]), Convert.ToInt16(arg.Args[1]));
        }

        [ConsoleCommand("BSChangeSPKit")]
        private void BSChangeSPKit(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            int num = Convert.ToInt16(arg.Args[1]);
            if (Convert.ToBoolean(arg.Args[3]) == true)
                spawnsData.CustomSpawnLocations[RD(arg.Args[0])][Convert.ToInt16(arg.Args[2])].Kits.Add(ValidKits[num]);
            else
                spawnsData.CustomSpawnLocations[RD(arg.Args[0])][Convert.ToInt16(arg.Args[2])].Kits.Remove(ValidKits[num]);
            BSKitsUI(arg.Player(), RD(arg.Args[0]), Convert.ToInt16(arg.Args[2]));
        }

        [ConsoleCommand("BSChangeOverRides")]
        private void BSChangeOverRides(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            int option = Convert.ToInt16(arg.Args[2]);
            var sd = spawnsData.CustomSpawnLocations[RD(arg.Args[0])][Convert.ToInt16(arg.Args[1])];
            switch (option)
            {
                case 0:
                    sd.UseOverrides = !sd.UseOverrides;
                    if (sd.Kits == null)
                    {
                        var p = Profiles[RD(arg.Args[0])];
                        sd.Stationary = p.Spawn.Stationary;
                        sd.Kits = p.Spawn.Kit.ToList();
                        sd.Health = p.Spawn.BotHealth;
                        sd.RoamRange = p.Behaviour.Roam_Range;
                    }
                    break;
                case 1:
                    sd.Stationary = !sd.Stationary;
                    break;
                case 2:
                    sd.Health = Convert.ToBoolean(arg.Args[3]) == true ? sd.Health + 10 : sd.Health - 10;
                    sd.Health = Mathf.Max(sd.Health, 10);
                    break;
                case 3:
                    sd.RoamRange = Convert.ToBoolean(arg.Args[3]) == true ? sd.RoamRange + 10 : sd.RoamRange - 10;
                    sd.RoamRange = Mathf.Max(sd.RoamRange, 10);
                    break;
            }
            BSOverridesUI(arg.Player(), RD(arg.Args[0]), Convert.ToInt16(arg.Args[1]));
            SaveSpawns();
        }

        [ConsoleCommand("BSEditOverRides")]
        private void BSEditOverRides(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            BSOverridesUI(arg.Player(), RD(arg.Args[0]), Convert.ToInt16(arg.Args[1]));
            SaveSpawns();
        }

        [ConsoleCommand("BSCloseOverrides")]
        private void BSCloseOverrides(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            CuiHelper.DestroyUi(arg.Player(), "BSBSOverridesUI");
            SaveSpawns();
        }

        [ConsoleCommand("BSUIShowSpawns")]
        private void BSUIShowSpawns(ConsoleSystem.Arg arg)
        {
            var p = RD(arg.Args[0]);
            var s = spawnsData.CustomSpawnLocations[p];

            for (int i = 0; i < s.Count; i++)
            {
                var t = Spawners.ContainsKey(Profiles[p].Other.Parent_Monument) ? Spawners[Profiles[p].Other.Parent_Monument]?[0]?.go.transform : Spawners.ContainsKey(p) ? Spawners[p]?[0]?.go.transform : null;
                ShowSpawn(arg.Player(), t.TransformPoint(s[i].loc), i, 10);
            }
            BSSpawnsUI(arg.Player(), RD(arg.Args[0]), 0);
        }

        [ConsoleCommand("BSUICheckNav")]
        private void BSUICheckNav(ConsoleSystem.Arg arg) => MsgUI(arg.Player(), HasNav(arg.Player().transform.position) ? "Navmesh found" : "No Navmesh");

        [ConsoleCommand("checknav")]
        private void checknav(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null || (!HasPermission(arg.Player().UserIDString, permAllowed) && !arg.Player().IsAdmin))
                return;
            BSUICheckNav(arg);
        }

        [ConsoleCommand("BSUIMoveSpawn")]
        private void BSUIMoveSpawn(ConsoleSystem.Arg arg)
        {
            var p = RD(arg.Args[0]);
            var s = spawnsData.CustomSpawnLocations[p];
            var num = Convert.ToInt32(arg.Args[1]);
            var player = arg.Player();

            if (Convert.ToBoolean(arg.Args[2]) == true)
                s.RemoveAt(num);
            else
            {
                if (s.Count() >= num)
                {
                    var rot = player.viewAngles.y;
                    if (!HasNav(player.transform.position) && !Profiles[p].Spawn.Stationary && !s[num].UseOverrides && !s[num].Stationary)
                    {
                        s[num].UseOverrides = true;
                        s[num].Stationary = true;
                    }

                    var t = Spawners.ContainsKey(Profiles[p].Other.Parent_Monument) ? Spawners[Profiles[p].Other.Parent_Monument]?[0]?.go.transform : Spawners.ContainsKey(p) ? Spawners[p]?[0]?.go.transform : null;

                    if (t != null)
                    {
                        Vector3 loc = t.InverseTransformPoint(player.transform.position);
                        s[num].loc = loc;
                        s[num].rot = rot - t.transform.eulerAngles.y;
                        SaveSpawns();
                        ShowSpawn(player, player.transform.position, num, 10f);

                        MsgUI(player, String.Format(lang.GetMessage("movedspawn", this), num, p));
                        SendReply(player, TitleText + lang.GetMessage("movedspawn", this), num, p);
                        return;
                    }
                }
            }
            BSSpawnsUI(player, RD(arg.Args[0]), Convert.ToInt16(arg.Args[3]));
        }

        Dictionary<ulong, string> Editing = new Dictionary<ulong, string>();
        Dictionary<ulong, string> Copy = new Dictionary<ulong, string>();

        [ConsoleCommand("BSUISetEditing")]
        private void BSUISetEditing(ConsoleSystem.Arg arg)
        {
            var p = RD(arg.Args[0]);
            Editing[arg.Player().userID] = RD(arg.Args[0]);
            DestroyMenu(arg.Player(), true);
            MsgUI(arg.Player(), $"You can add spawnpoints to {RD(arg.Args[0])} by command 'addspawn'", 5);
        }

        [ConsoleCommand("BSUIStopEdit")]
        private void BSUIStopEdit(ConsoleSystem.Arg arg)
        {
            Editing.Remove(arg.Player().userID);
            BSMainUI(arg.Player(), "", "", "Spawn");
        }

        [ConsoleCommand("BSGotoProfile")]
        private void BSGotoProfile(ConsoleSystem.Arg arg)
        {
            BSMainUI(arg.Player(), arg.Args[0], RD(arg.Args[1]), "Spawn");
        }

        [ConsoleCommand("BSUIAddSpawn")]
        private void BSUIAddSpawn(ConsoleSystem.Arg arg)
        {
            var p = RD(arg.Args[0]);
            var s = spawnsData.CustomSpawnLocations[p];
            var rot = arg.Player().viewAngles.y;
            var t = Spawners.ContainsKey(Profiles[p].Other.Parent_Monument) ? Spawners[Profiles[p].Other.Parent_Monument][0]?.go?.transform : Spawners.ContainsKey(p) ? Spawners[p]?[0]?.go.transform : null;

            if (t != null)
            {
                Vector3 loc = t.InverseTransformPoint(arg.Player().transform.position);
                s.Add(new SpawnData(null) { loc = loc, rot = rot - t.eulerAngles.y, Stationary = HasNav(arg.Player().transform.position) && Profiles[p].Spawn.Stationary });
                SaveSpawns();
                ShowSpawn(arg.Player(), arg.Player().transform.position, s.Count - 1, 10f);
            }

            BSSpawnsUI(arg.Player(), p, Convert.ToInt16(arg.Args[1]));
        }


        [ConsoleCommand("addspawn")]
        private void addspawn(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null || (!HasPermission(arg.Player().UserIDString, permAllowed) && !arg.Player().IsAdmin))
                return;
            string p = string.Empty;
            Editing.TryGetValue(arg.Player().userID, out p);

            if (p != null && Profiles.ContainsKey(p))
            {
                var s = spawnsData.CustomSpawnLocations[Editing[arg.Player().userID]];
                var rot = arg.Player().viewAngles.y;
                var t = Spawners.ContainsKey(Profiles[p].Other.Parent_Monument) ? Spawners[Profiles[p].Other.Parent_Monument][0]?.go?.transform : Spawners.ContainsKey(p) ? Spawners[p]?[0]?.go.transform : null;

                if (t != null)
                {
                    Vector3 loc = t.InverseTransformPoint(arg.Player().transform.position);
                    s.Add(new SpawnData(Profiles[p]) { loc = loc, rot = rot - t.eulerAngles.y, Stationary = HasNav(arg.Player().transform.position) && Profiles[p].Spawn.Stationary });
                    SaveSpawns();
                    ShowSpawn(arg.Player(), arg.Player().transform.position, s.Count - 1, 10f);
                    MsgUI(arg.Player(), $"Added point {s.Count()}", 1.5f);
                }
            }
            else
            {
                MsgUI(arg.Player(), "You are not presently editing a valid profile.", 3);
            }
        }

        [ConsoleCommand("BSUIEditKits")]
        private void BSUIEditKits(ConsoleSystem.Arg arg)
        {
            if (ValidKits.Count == 0)
            {
                Puts("THERE ARE NO VALID KITS"); 
                return;
            }

            if (arg.Player() == null)
                return;
            BSKitsUI(arg.Player(), RD(arg.Args[0]));
        }

        [ConsoleCommand("BSChangeKit")]
        private void BSChangeKit(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            int num = Convert.ToInt16(arg.Args[1]);
            if (Convert.ToBoolean(arg.Args[2]) == true)
                GetProfile(RD(arg.Args[0])).Spawn.Kit.Add(ValidKits[num]);
            else
                GetProfile(RD(arg.Args[0])).Spawn.Kit.Remove(ValidKits[num]);
            BSKitsUI(arg.Player(), RD(arg.Args[0]));
        }

        [ConsoleCommand("BSUIEditSpawns")]
        private void BSUIEditSpawns(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            CuiHelper.DestroyUi(arg.Player(), "BSSpawnsUI");
            BSSpawnsUI(arg.Player(), RD(arg.Args[0]), Convert.ToInt16(arg.Args[1]));
        }

        [ConsoleCommand("CloseExtra")]
        private void CloseKitsBS(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            CuiHelper.DestroyUi(arg.Player(), arg.Args[0]);
            BSMainUI(arg.Player(), Profiles[RD(arg.Args[1])].type == ProfileType.Custom ? "2" : "1", RD(arg.Args[1]), "Spawn", 1);
            if (Convert.ToInt16(arg.Args[2]) > -1)
            {
                BSSpawnsUI(arg.Player(), RD(arg.Args[1]), 0);
                BSOverridesUI(arg.Player(), RD(arg.Args[1]), Convert.ToInt16(arg.Args[2]));
            }
            SaveSpawns();
        }

        [ConsoleCommand("BSUIReload")]
        private void BSUIReload(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            ReloadData(RD(arg.Args[0]), true, null);
        }

        [ConsoleCommand("BSUIShowAll")]
        private void BSUIShowAll(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            foreach (var profile in Profiles.Where(x => x.Value.type != ProfileType.Biome && x.Value.type != ProfileType.Event))
                ShowProfiles(arg.Player(), profile.Value.Other.Location, profile.Key, configData.Global.Show_Profiles_Seconds);
            DestroyMenu(arg.Player(), true);
        }

        [ConsoleCommand("BSUIMain")]
        private void BSUIMain(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            DestroyMenu(arg.Player(), false);
            BSMainUI(arg.Player(), arg?.Args?.Length == 1 ? arg.Args[0] : "");
        }

        [ConsoleCommand("BSUI")]
        private void BSUI(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            DestroyMenu(arg.Player(), false);
            BSMainUI(arg.Player(), arg.Args[0], RD(arg.Args[1]), arg.Args[2]);
        }

        [ConsoleCommand("BSUIPage")]
        private void BSUIPage(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            DestroyMenu(arg.Player(), false);
            BSMainUI(arg.Player(), arg.Args[0], string.Empty, string.Empty, Convert.ToInt16(arg.Args[1]));
        }

        [ConsoleCommand("BSChangeBool")]
        private void BSChangeBool(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            DestroyMenu(arg.Player(), false);

            var record = GetProfile(RD(arg.Args[1]));
            var sub = record.GetType().GetField(RD(arg.Args[2]));
            var subobj = sub.GetValue(record);
            var prop = subobj.GetType().GetField(RD(arg.Args[3]));
            var propobj = prop.GetValue(subobj);
            prop.SetValue(sub.GetValue(record), !(bool)propobj);
            sub.SetValue(record, subobj);

            BSMainUI(arg.Player(), arg.Args[0], RD(arg.Args[1]), arg.Args[2], 0);
        }

        [ConsoleCommand("BSUICloseParentMonument")]
        private void BSUICloseParentMonument(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            CuiHelper.DestroyUi(arg.Player(), "BSShowParentsUI");
        }

        [ConsoleCommand("BSUICloseLoot")]
        private void BSUICloseLoot(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            CuiHelper.DestroyUi(arg.Player(), "BSShowLootUI");
        }

        [ConsoleCommand("BSShowParents")]
        private void BSShowParents(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            BSShowParentsUI(arg.Player(), arg.Args[0], RD(arg.Args[1]), arg.Args[2]);
        }

        [ConsoleCommand("BSShowLoot")]
        private void BSShowLoot(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            BSShowLootUI(arg.Player(), arg.Args[0], RD(arg.Args[1]), arg.Args[2]);
        }

        [ConsoleCommand("BSSelectParent")]
        private void BSSelectParent(ConsoleSystem.Arg arg) 
        {
            if (arg.Player() == null)
                return;
            string cprofile = RD(arg.Args[1]);
            var record = GetProfile(cprofile);

            record.Other.Parent_Monument = RD(arg.Args[3]);

            var path = storedData.MigrationDataDoNotEdit[cprofile];
            if (path.ParentMonument == new Vector3())
                Puts($"Parent_Monument added for {cprofile}. Removing any existing custom spawn points");
            else
                Puts($"Parent_Monument changed for {cprofile}. Removing any existing custom spawn points");

            spawnsData.CustomSpawnLocations[cprofile].Clear();
            SaveSpawns();

            path.ParentMonument = Profiles[RD(arg.Args[3])].Other.Location;
            path.Offset = Spawners[RD(arg.Args[3])][0].go.transform.InverseTransformPoint(record.Other.Location);

            ReloadData(arg.Args[1], true, null);
            BSMainUI(arg.Player(), arg.Args[0], cprofile, arg.Args[2], 0);
            MsgUI(arg.Player(), String.Format(lang.GetMessage("ParentSelected", this), RD(arg.Args[3]), cprofile));
        }

        [ConsoleCommand("BSSelectLoot")]
        private void BSSelectLoot(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            string cprofile = RD(arg.Args[1]);
            var record = GetProfile(cprofile);

            record.Death.Rust_Loot_Source = RD(arg.Args[3]);
            ReloadData(cprofile, true, null);
            BSMainUI(arg.Player(), arg.Args[0], cprofile, arg.Args[2], 0);
        }

        [ConsoleCommand("BSUIMoveProfile")]
        private void BSUIMoveProfile(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            string cprofile = RD(arg.Args[0]);
            var record = GetProfile(cprofile);
            var path = storedData.MigrationDataDoNotEdit[cprofile];

            record.Other.Location = arg.Player().transform.position;

            if (Spawners.ContainsKey(record.Other.Parent_Monument))
            {
                path.ParentMonument = Profiles[cprofile].Other.Location;
                storedData.MigrationDataDoNotEdit[cprofile].Offset = Spawners[record.Other.Parent_Monument][0].go.transform.InverseTransformPoint(arg.Player().transform.position);
            }

            spawnsData.CustomSpawnLocations[arg.Args[0]].Clear();
            SaveSpawns();
            ReloadData(RD(arg.Args[0]), true, null);
            BSMainUI(arg.Player(), "2", cprofile, "Other");
            MsgUI(arg.Player(), String.Format(lang.GetMessage("ProfileMoved", this), cprofile));
        }

        string remove = string.Empty;
        [ConsoleCommand("BSUIRemoveProfile")]
        private void BSUIReoveProfile(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;

            string cprofile = RD(arg.Args[0]);
            if (remove == cprofile)
            {
                DestroySpawnGroups(Spawners[cprofile][0].go);
                spawnsData.CustomSpawnLocations[cprofile].Clear();
                SaveSpawns();
                Profiles.Remove(cprofile);
                storedData.Profiles.Remove(cprofile);
                storedData.MigrationDataDoNotEdit.Remove(cprofile);
                SaveData();
                BSMainUI(arg.Player(), "2", "", "Spawn");
            }
            else
            {
                MsgUI(arg.Player(), "Click again to confirm");
                remove = cprofile;
            }
        }

        Profile GetProfile(string name) => defaultData.Monuments.ContainsKey(name) ? defaultData.Monuments[name] : defaultData.Biomes.ContainsKey(name) ? defaultData.Biomes[name] : defaultData.Events.ContainsKey(name) ? defaultData.Events[name] : storedData.Profiles[name];

        [ConsoleCommand("BSChangeEnum")]
        private void BSChangeEnum(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            DestroyMenu(arg.Player(), false);
            bool up = Convert.ToBoolean(arg.Args[4]);
            var record = GetProfile(RD(arg.Args[1]));
            var sub = record.GetType().GetField(RD(arg.Args[2]));
            var subobj = sub.GetValue(record);
            var prop = sub.GetValue(record).GetType().GetField(RD(arg.Args[3]));
            var propobj = (int)prop.GetValue(subobj);
            propobj = up ? propobj + 1 : propobj - 1;
            propobj = Mathf.Max(Mathf.Min(propobj, 2), 0);
            prop.SetValue(sub.GetValue(record), propobj);
            sub.SetValue(record, subobj);
            BSMainUI(arg.Player(), arg.Args[0], RD(arg.Args[1]), arg.Args[2], 0);
        }

        [ConsoleCommand("BSChangeNum")]
        private void BSChangeNum(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            DestroyMenu(arg.Player(), false);
            bool up = Convert.ToBoolean(arg.Args[4]);
            var record = GetProfile(RD(arg.Args[1]));
            var sub = record.GetType().GetField(RD(arg.Args[2]));
            var subobj = sub.GetValue(record);
            var prop = sub.GetValue(record).GetType().GetField(RD(arg.Args[3]));
            var propobj = (int)prop.GetValue(subobj);

            int increment = tens.Contains(arg.Args[3]) ? ScaleIncrement(propobj, up) : 1;
            if ((arg.Args[3].Contains("Percent") || arg.Args[3].Contains("Chute_Fall")) && !arg.Args[3].Contains("Damage"))
                increment = 5;
            propobj = limitpercent(RD(arg.Args[3]), Mathf.Max(tens.Contains(arg.Args[3]) ? 10 : 0, up ? propobj + increment : propobj - increment));
            prop.SetValue(sub.GetValue(record), propobj);
            sub.SetValue(record, subobj);
            BSMainUI(arg.Player(), arg.Args[0], RD(arg.Args[1]), arg.Args[2], 0);
        }

        int ScaleIncrement(int val, bool up)
        {
            if (up)
            {
                if (val >= 5000) return 1000;
                if (val >= 500) return 100;
                if (val >= 300) return 50;
                if (val >= 200) return 20;
                return 10;
            }
            else
            {
                if (val <= 200) return 10;
                if (val <= 300) return 20;
                if (val <= 500) return 50;
                if (val <= 5000) return 100;
                return 1000;
            }
        }

        [ConsoleCommand("BSChangeDouble")]
        private void BSChangeDouble(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            DestroyMenu(arg.Player(), false);
            bool up = Convert.ToBoolean(arg.Args[4]);
            var record = GetProfile(RD(arg.Args[1]));
            var sub = record.GetType().GetField(RD(arg.Args[2]));
            var subobj = sub.GetValue(record);
            var prop = sub.GetValue(record).GetType().GetField(RD(arg.Args[3]));
            var propobj = Math.Round((double)prop.GetValue(subobj), 1);
            bool RR = arg.Args[3].Contains("RustRewards") && configData.Global.RustRewards_Whole_Numbers;
            double num = RR ? ScaleRR((int)propobj, up) : 0.1;
            propobj = up ? propobj + num : propobj - num;
            if (RR)
                propobj = Math.Round(propobj, 0);
            else
                propobj = Math.Round(Mathf.Max(0, (float)propobj), 1);

            prop.SetValue(sub.GetValue(record), propobj);
            sub.SetValue(record, subobj);
            BSMainUI(arg.Player(), arg.Args[0], RD(arg.Args[1]), arg.Args[2], 0);
        }

        int ScaleRR(int val, bool up)
        {
            if (up)
            {
                if (val >= 5000) return 1000;
                if (val >= 500) return 100;
                if (val >= 300) return 50;
                if (val >= 200) return 20;
                if (val >= 100) return 10;
                if (val >= 25) return 5;
                return 1;
            }
            else
            {
                if (val <= 25) return 1;
                if (val <= 100) return 5;
                if (val <= 200) return 10;
                if (val <= 300) return 20;
                if (val <= 500) return 50;
                if (val <= 5000) return 100;
                return 1000;
            }
        }

        [ConsoleCommand("BSConfChangeNum")]
        private void BSConfChangeNum(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            DestroyMenu(arg.Player(), false);
            bool up = Convert.ToBoolean(arg.Args[1]);
            var sub = configData.Global.GetType().GetField(RD(arg.Args[0]));
            var subobj = (int)sub.GetValue(configData.Global);

            int increment = tens.Contains(arg.Args[0]) ? 10 : 1;
            if (arg.Args[0].Contains("Percent") || arg.Args[0].Contains("Chute_Fall"))
                increment = 5;

            subobj = limitpercent(RD(arg.Args[0]), Mathf.Max(0, up ? subobj + increment : subobj - increment));
            sub.SetValue(configData.Global, subobj);
            BSMainUI(arg.Player(), "0", "", "", 1);
        }

        int limitpercent(string name, int number)
        {
            if (name.Contains("Damage"))
                return Mathf.Max(number, 0);
            if (name.Contains("Percent") || name.Contains("Chute_Fall"))
                return Mathf.Max(Mathf.Min(number, 100), 0);
            return number;
        }

        [ConsoleCommand("BSConfChangeBool")]
        private void BSConfChangeBool(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            DestroyMenu(arg.Player(), false);
            var sub = configData.Global.GetType().GetField(RD(arg.Args[0]));
            var subobj = (bool)sub.GetValue(configData.Global);
            sub.SetValue(configData.Global, !subobj);
            BSMainUI(arg.Player(), "0", "", "", 1);
        }

        public List<string> tens = new List<string>() { "Bot_Damage_Percent", "Radius", "BotHealth", "Roam_Range", "Aggro_Range", "DeAggro_Range" };

        [ConsoleCommand("CloseBS")]
        private void CloseBS(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
                return;
            DestroyMenu(arg.Player(), true);
            SaveData();
            SaveConfig(configData);
        }

        string RS(string input) => input.Replace(" ", "-");
        string RD(string input) => input.Replace("-", " ");
        #endregion

        #region API
        private Dictionary<string, List<ulong>> BotReSpawnBots()
        {
            var BotReSpawnBots = new Dictionary<string, List<ulong>>();
            foreach (var entry in Profiles)
                BotReSpawnBots.Add(entry.Key, new List<ulong>());

            foreach (var bot in NPCPlayers)
            {
                if (bot.Value == null)
                    continue;
                var bData = bot.Value.GetComponent<BotData>();
                if (bData == null)
                    continue;
                if (BotReSpawnBots.ContainsKey(bData.profilename))
                    BotReSpawnBots[bData.profilename].Add(bot.Key);
                else
                    BotReSpawnBots.Add(bData.profilename, new List<ulong> { bot.Key });
            }
            return BotReSpawnBots;
        }

        private string NPCProfile(NPCPlayer npc)
        {
            if (NPCPlayers.ContainsKey(npc.userID))
                return npc.GetComponent<BotData>().name;
            return "No Name";
        }

        private string[] AddGroupSpawn(Vector3 location, string profileName, string group, int quantity)
        {
            if (location == new Vector3() || profileName == null || group == null)
                return new string[] { "error", "null parameter" };
            string lowerProfile = profileName.ToLower();

            foreach (var entry in bs.Profiles.Where(entry => entry.Key.ToLower() == lowerProfile && IsSpawner(entry.Key)))
            {
                if (entry.Key.ToLower() == lowerProfile)
                {
                    if (quantity == 0)
                        quantity = GetPop(entry.Value);
                    CreateTempSpawnGroup(location, entry.Key, entry.Value, group.ToLower(), quantity);
                    return new string[] { "true", "Group successfully added" };
                }
            }
            return new string[] { "false", "Group add failed - Check profile name and try again" };
        }

        private string[] RemoveGroupSpawn(string group)
        {
            if (group == null)
                return new string[] { "error", "No group specified." };

            List<global::HumanNPC> toDestroy = Pool.GetList<global::HumanNPC>();
            bool flag = false;
            foreach (var bot in NPCPlayers.ToDictionary(pair => pair.Key, pair => pair.Value))
            {
                if (bot.Value == null)
                    continue;
                var bData = bot.Value.GetComponent<BotData>();
                if (bData.group == group.ToLower())
                {
                    flag = true;
                    NPCPlayers[bot.Key].Kill();
                }
            }
            Facepunch.Pool.FreeList(ref toDestroy);
            return flag ? new string[] { "true", $"Group {group} was destroyed." } : new string[] { "true", $"There are no bots belonging to {group}" };
        }

        #endregion
    }
}

//  Fixed in V1.0.3
//  Melee attack/hitinfo Error.
//  Properly fixed disable radio chatter.
//  Added RustRewardsValue per profile.
//  Automatically remove non-weapon items from npc belt.
//  Made day/night respawn times quicker - Not related to regular respawn timer.
//  Fixed the resetting of kits info where Kits plugin was not loaded.
//  Running speed booster fixed - 10 is default - bigger is faster.
//  Removed accidental 'edit spawnpoints' button in Biome profile view. 
//  Found and fixed conflict issue which resulted in fail to compile during server restarts. Thanks @Krungh Crow
//  New kits are found without the need to reload.
//  Kits with any melee weapon should now be accepted (hatchets etc).
//  Addspawn console command has been added, for keybinding.It is enabled in UI (spawnpoints page).
//  Kits page allows for four columns, allowing for approx 120 kits total.
//  Added option to use server time, ignoring day/night start hours.


//  Fixed in V1.0.4
//  Number adjustment increments are dynamic - Bigger increment for larger numbers  
//  ToPlayer showing 'does not exist' for some profile names. 
//  Profile page not having enough room on larger servers.
//  Issues relating to quantity of AirDrop/ToPlayer spawned npcs.
//  Removed non-applicable options from Airdop profile page.
//  Always use lights setting, and general use-lights behaviour
//  Respawn time/order - now more accurate/predictable.
//  Added Wipe_Main_Percent option - This is for people who want to add loot items to main via kit, but only want them available a % of the time.
//  Spaces and hyphens in profile names causing issues. They are now auto removed.
//  Day/night spawning times/delays.
//  APC_Safe should apply to npcs being 'run over' now - untested.
//  Reported death type for suicide is now 'Suicide', where explosion option is disabled.
//  Roam behaviour with melee-only npcs.


//  Fixed in V1.0.5
//  Rare issue where day night switching from zero to many npcs could throw errors. 
//  Altered initial save timing. @Sasquire
//  Made room for more kits, until pagination is added - Might stutter maxed out?
//  Botnames should sync with kit names, where possible.
//  Improvements to deaggro range / line of sight disengage.
//  Added kit name to /botrespawn info command
//  Added AddGroupSpawn API - BotReSpawn?.Call("AddGroupSpawn", location, "profilename", "MadeUpName", quantity); @bazz3l
//  Added RemoveGroupSpawn API.
//  Added hook OnBotReSpawnNPCSpawned(ScientistNPC npc, string profilename, string group, passing group name for API users.
//  BotReSpawn npcs should ignore Safezone players.
//  Hooked up ChangeCustomSpawnOnDeath option - TEST THIS
//  Prevent custom spawn point related options showing for Biome/Event


//  Changes in V1.0.6
//  Fixed UI issue changing overrides for default monument spawnpoints. 
//  Fixed issue where override kits for newly created spawnpoints would synchronise. 
//  Fixed issue where kit changes may not hold on first attempt.
//  Fixed issue where full list of bot names wasn't used.
//  Added "Go to settings for {profile}" button on main UI page, when 'addspawn' command is enabled for a profile.
//  Fixed incorrect title on 'Select Parent_Monument page.
//  Added Copy/Paste buttons for profiles - Does not copy location, Parent_Monument, or spawn points.
//  Added 'Show All Profiles' duration option (seconds) in global config.
//  Fixed accuracy and damage multipliers.
//  Added "Delete Profile" button in UI - Two clicks required.
//  Bot_Damage_Percent can now exceed 100.
//  Added global option "Ignore_Skinned_Supply_Grenades".
//  Default kits are only copied to custom spawn point overrides (as defaults) if/when UseOverrides is set true.
//  Added toplayer console command. "botrespawn toplayer NameOrID ProfileName amount(optional)".
//  Fixed late timing of wipe_main_percent, which resulted in wiping loot placed by other plugins.
//  Removed unused CH47 event profile.
//  Replaced Harbor profiles with Harbor_Small/Harbor_Large.
//  Replaced small Fishing Village profiles with Fishing Village_A/_B/_C. Large remains as before.
//  Added "Disable_Non_Parented_Custom_Profiles_After_Wipe" option. Set true if reusing the same map.
//  Removed 'Failed to get enough spawnpoints for...' message for event profiles, as this doesn't indicate a problem.

//  Added Events
//  LockedCrate_Spawn
//  LockedCrate_HackStart
//  APC_Kill  
//  PatrolHeli_Kill
//  CH47_Kill

//  Added API
//  object OnBotReSpawnCrateDropped(HackableLockedCrate crate)  
//  object OnBotReSpawnCrateHackBegin(HackableLockedCrate crate)
//  object OnBotReSpawnAPCKill(BradleyAPC apc)
//  object OnBotReSpawnPatrolHeliKill(PatrolHelicopterAI heli)
//  object OnBotReSpawnCH47Kill(CH47HelicopterAIController ch)
//  object OnBotReSpawnAirdrop(SupplyDrop drop)

//  Notes
//  If you use Parent_Monument for a custom profile, but without custom spawnpoints, please use "Move Profile Here" after installing and before your next wipe.
//  If you used the Harbor profiles, you'll need to set them up again. They're now called Harbor_Large/_Small
//  If you used the small fishing village profiles, you'll need to set them up again. They're now called Fishing Village_A/_B/_C.


//  Changes in V1.0.7
//  Fixed toplayer console command.
//  Fixed inconsequential null reference error during server boot.
//  Fixed log file spam regarding navagent with stationary npcs. 
//  Fixed GiveRustReward error.
//  Added Ignore Sleepers.
//  Added Ignore Noob players. (based on Rust sash)
//  Added Ignore/Defend/Attack ZombieHorde.
//  Added Ignore/Defend/Attack HumanNPC. 
//  Added Ignore/Defend/Attack Other NPCs.
//  Added number for profile 'Faction'.
//  Added number for profile 'SubFaction'.
//  Added attack/ignore eachother, using 'Faction/SubFaction' as identifier.
//  Added CheckNav console command and UI button.
//  Added global RustRewards whole number true/false.
//  Added pagination for spawn points page.
//  Added Optional map-markers per profile.
//  Added Murderer breathing sound (true/false) per profile

//  Made APCs target + hurt BotReSpawn npcs, where APC_Safe is false.
//  Added toplayer announcement global config option, and lang message.
//  Separated out variations of Harbor Fishing Villages Ice lakes, mountains, substations, and swamps - Old profiles are auto-removed.
//      Take a backup for reference, if needed. 
//  Show Spawnpoints shows as red for points with no navmesh.

//  NPCs no longer pursue players after player death.
//  NPCs can now heal with syringes when line of sight is broken. 
//  NPCs can now throw grenades when line of sight is broken.
//  NPCs can now use - Flamethrowers, bows, rocket launchers, MGL, chainsaws, jackhammers, nailguns.
//  NPCs can now damage the armour you are wearing. (global config option)
//  NPCs will now headshot you, from time to time.
//  Increased randomisation of length of automatic weapon fire bursts.
//  Evened out npc damage across the board, so nothing should be OP now.
//  Added Melee_DamageScale and RangeWeapon_DamageScale per profile incase you liked them OP.
//  Added Global option to prevent using shorter range weapons over long range
//  Server Ai_Dormant is now respected. Sniping an npc will override. Global config option enables/disables.
//  Removed unneeded 'Radius' option from biome profiles.
//  Fixed biome npc respawn position not being randomised. 
//  Added selectable vanilla loot source.
//  Added Scale_Meds_To_Health global option
//  Made RustRewards value up/down increments bigger/smaller, depending on current number. (whole numbers only)


//  Changes in V1.0.8
//  bug fix


//  Changes in V1.0.9
//  Readded missing corpse dispenser skulls.
//  Auto-Disabled Target_Noobs false if NoSash plugin is installed.
//  Fixed faction/subfaction issue.
//  Fixed parachute destroy savelist issue.
//  Added global option Ignore_Factions - All profiles fight all profiles.
//  Fixed NPCs failing to return fire when constantly being hit.
//  Fixed stuttery melee npc chasing.
//  Fixed missing projectile traces/bullet holes.
//  Added ScarecrowNPC as a vanilla loot source.
//  Added OnBotReSpawnNPCKilled API.
//  Fixed "Reload Profile" not working for biomes.
//  Fixed NPC pursuit logic.
//  Fixed unintentionally long fire bursts with m249.
//  Added global reduce damage over distance option.
//  Added aggro memory duration global option.

//  Changes in V1.1.0 
//  Fixed issue with loot source selection and profiles with spaces in names.
//  Fixed missing ScarecrowNPC loot source option.
//  Added global Ignore_HackableCrates_With_OwnerID
//  More biome spawnpoints are found now, and much faster.
//  Made AutoSpawn:true profiles show as green in UI.
//  Fixed ocassional fail to return home - CONFIRM...
//  Fixed Hapis monument name checking error.

//  Changes in V1.1.1 
//  Fixed toplayer button in UI sending to user, not target.
//  Fixed startup performance issue.