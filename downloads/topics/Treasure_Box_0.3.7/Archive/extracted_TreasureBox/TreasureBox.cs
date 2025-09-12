using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Oxide.Ext.ChaosNPC;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("TreasureBox", "k1lly0u", "0.3.7")]
    [Description("A random treasure chest mini event")]
    class TreasureBox : RustPlugin, IChaosNPCPlugin
    {
        #region Fields
        [PluginReference] Plugin LustyMap, Spawns, RandomSpawns, ZoneManager;
        
        private Timer nextEvent;
        private Timer timeToUnlock;
        private Timer timeToDestroy;
        private Timer uiTimer;

        private MapMarkerGenericRadius mapMarker;
        private BaseEntity container;
        private ConfigData.LootTables lootTable;

        private double nextTrigger;

        private bool isUnlocked;
        private bool canBeUnlocked;
        private bool isLooted;
        private bool isBlocked;

        private bool isLoaded;
        private string reason = "Loading Plugin!";

        private static string treasureIcon;

        private static TreasureBox Instance;

        private Dictionary<string, int> itemNameToId = new Dictionary<string, int>();

        private readonly List<CustomScientistNPC> customNpcs = new List<CustomScientistNPC>();

        private const string SMOKE_PREFAB = "assets/bundled/prefabs/fx/smoke_signal_full.prefab";
        private const string CONTAINER_PREFAB = "assets/prefabs/deployable/large wood storage/box.wooden.large.prefab";
        private const string MARKER_PREFAB = "assets/prefabs/tools/map/genericradiusmarker.prefab";
        #endregion

        #region Oxide Hooks 
        private void OnServerInitialized()
        {
            Instance = this;
            lang.RegisterMessages(Messages, this);

            SubscribeToMethods(false);

            itemNameToId = ItemManager.itemList.ToDictionary(x => x.shortname, y => y.itemid);

            VerifySettings();
            DestroyZone();
        }

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {            
            if (entity == null || info == null)
                return;

            if (entity == container)
            {
                info.damageTypes.ScaleAll(0);
                return;
            }
        }

        private void OnEntityDeath(CustomScientistNPC customNpc)
        {
            if (customNpc != null && customNpcs.Contains(customNpc))
            {
                customNpcs.Remove(customNpc);
                if (customNpcs.Count == 0 && configData.EventOptions.BotSettings.NPCLock)
                {
                    if (container != null && canBeUnlocked)
                    {
                        container.SetFlag(BaseEntity.Flags.Locked, false);
                        isUnlocked = true;
                    }
                }
            }
        }
        private void OnEntityKill(CustomScientistNPC customNpc) => customNpcs.Remove(customNpc);

        private void OnItemAddedToContainer(ItemContainer itemContainer, Item item)
        {
            if (!isLoaded || container == null) return;
            if (itemContainer.entityOwner == null) return;
            if (container == itemContainer.entityOwner)
            {
                if (isBlocked)
                    item.Drop(itemContainer.entityOwner.transform.position, Vector3.up);
            }
        }

        private void OnItemDeployed(Deployer deployer, BaseEntity entity)
        {
            BasePlayer player = deployer.GetOwnerPlayer();

            if (player == null || entity == null)
                return;
           
            if (entity == container)
            {
                Deployable deployable = deployer.GetDeployable();
                if (deployable != null)
                {                   
                    if (deployable.prefabID is 3518824735 or 2106860026)
                    {                        
                        entity.GetSlot(deployable.slot)?.Kill();

                        if (deployable.prefabID == 3518824735)
                            player.GiveItem(ItemManager.CreateByItemID(1159991980, 1, 0));
                        else player.GiveItem(ItemManager.CreateByItemID(-850982208, 1, 0));

                        SendReply(player, msg("noLockDeploy", player.UserIDString));
                    }
                }
            }
        }

        private void OnPlayerLootEnd(PlayerLoot loot)
        {
            if (!isLoaded || container == null) return;
            if (loot.entitySource != null && loot.entitySource == container)
            {
                ItemContainer itemContainer = container.GetComponent<StorageContainer>()?.inventory;
                if (itemContainer != null)
                {
                    if (!isLooted)
                    {
                        isLooted = true;
                        string playerName = loot.GetComponentInParent<BasePlayer>()?.displayName;
                        
                        if (!string.IsNullOrEmpty(playerName))
                            PrintToChat(string.Format(msg("eventWin"), playerName));
                    }

                    if (itemContainer.itemList.Count == 0)
                    {
                        DestroyContainer();
                    }                    
                }
            }
        }

        private object CanBuild(Planner planner, Construction prefab, Construction.Target target)
        {
            if (!isLoaded || container == null) return null;
            if (configData.EventOptions.ZoneSettings.DisableBuilding)
            {
                if (Vector3.Distance(target.position, container.transform.position) < configData.EventOptions.ZoneSettings.ZoneRadius)
                {
                    BasePlayer player = planner?.GetOwnerPlayer();
                    if (player != null)
                        SendReply(player, msg("nobuild", player.UserIDString));
                    return false;
                }
            }
            return null;
        }

        private void Unload()
        {            
            treasureIcon = string.Empty;
            DestroyEvent(true);
            DestroyZone();

            configData = null;
            Instance = null;
        }

        private void DestroyEvent(bool killNpcs)
        {
            nextEvent?.Destroy();
            timeToUnlock?.Destroy();
            timeToDestroy?.Destroy();
            uiTimer?.Destroy();

            foreach (BasePlayer player in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(player, Main);

            for (int i = customNpcs.Count - 1; i >= 0; i--)
            {                
                CustomScientistNPC npcPlayer = customNpcs[i];
                if (npcPlayer == null || npcPlayer.IsDestroyed)
                    continue;

                if (configData.EventOptions.BotSettings.DestroyOnFinish || killNpcs)
                {
                    if (npcPlayer != null && !npcPlayer.IsDestroyed)
                        npcPlayer.Die(new HitInfo(npcPlayer, npcPlayer, Rust.DamageType.Explosion, 1000f));
                }
            }

            customNpcs.Clear();

            if (container != null)
            {
                ItemContainer loot = container.GetComponent<StorageContainer>()?.inventory;
                if (loot != null)
                    ClearContainer(loot);
                (container as BaseCombatEntity)?.DieInstantly();
            }

            if (mapMarker != null && !mapMarker.IsDestroyed)
                mapMarker.Kill();

            if (configData.MapOptions.Lusty.Enabled)
                RemoveMapMarker();

        }
        #endregion

        #region Functions
        private void SubscribeToMethods(bool isSubscribing)
        {
            if (isSubscribing)
            {
                Subscribe(nameof(OnPlayerLootEnd));
                Subscribe(nameof(OnItemAddedToContainer));
                Subscribe(nameof(OnEntityTakeDamage));
                Subscribe(nameof(CanBuild));
            }
            else
            {
                Unsubscribe(nameof(OnPlayerLootEnd));
                Unsubscribe(nameof(OnItemAddedToContainer));
                Unsubscribe(nameof(OnEntityTakeDamage));
                Unsubscribe(nameof(CanBuild));
            }
        }

        private void VerifySettings()
        {
            if (!configData.EventOptions.RandomSpawnPoints)
            {
                if (!Spawns)
                {
                    reason = "Spawns Database not found!";
                    PrintError("Spawns Database not found! Can not continue");
                    return;
                }
                if (string.IsNullOrEmpty(configData.EventOptions.Spawnfile))
                {
                    reason = "No spawnfile has been set in the config!";
                    PrintError("No spawnfile has been set in the config! Can not continue");
                    return;
                }
                object success = Spawns.Call("GetSpawnsCount", configData.EventOptions.Spawnfile);
                if (success is string)
                {
                    reason = (string)success;
                    PrintError((string)success);
                    return;
                }
            }
            else
            {
                if (!RandomSpawns)
                {
                    reason = "RandomSpawns not found!";
                    PrintError("RandomSpawns can not be found! Can not continue");
                    return;
                }
            }
            if (configData.EventOptions.UISettings.Enabled && !string.IsNullOrEmpty(configData.EventOptions.UISettings.Icon))
                Add(configData.EventOptions.UISettings.Icon);          
            isLoaded = true;
            StartTimers();
        }

        private void StartTimers()
        {
            int time = UnityEngine.Random.Range(configData.Timers.MinInterval, configData.Timers.MaxInterval);
            nextTrigger = GrabCurrentTime() + time;
            nextEvent = timer.In((float)time, ()=> SpawnContainer());
        }

        private void SpawnContainer(object spawnLoc = null, bool countOverride = false)
        {
            if (!countOverride && BasePlayer.activePlayerList.Count < configData.EventOptions.MinPlayers)
            {
                StartTimers();
                return;
            }

            if (BasePlayer.activePlayerList.Count >= configData.EventOptions.MinPlayers || countOverride)
            {
                Vector3 location = Vector3.zero;
                
                if (spawnLoc == null)
                {
                    if (configData.EventOptions.RandomSpawnPoints)
                    {
                        object success = RandomSpawns.Call("GetSpawnPoint");
                        if (success != null)
                            location = (Vector3)success;
                    }
                    else
                    {
                        object success = Spawns.Call("GetRandomSpawn", configData.EventOptions.Spawnfile);
                        if (success is string)
                        {
                            PrintError((string)success);
                            goto START_TIMER;
                        }
                        location = (Vector3)success;
                    }                    
                }
                else location = (Vector3)spawnLoc;

                if (location == Vector3.zero)
                {
                    PrintError("There was a error retrieving a spawn location for the treasure box!");
                    goto START_TIMER;
                }                

                lootTable = configData.LootTable.GetRandom();
                if (lootTable == null)
                {
                    PrintError("Null loot table in config!");
                    return;
                }

                SubscribeToMethods(true);

                container = GameManager.server.CreateEntity(CONTAINER_PREFAB, location, Quaternion.identity, true);
                container.skinID = lootTable.SkinID;
                container.enableSaving = false;
                container.Spawn();

                UnityEngine.Object.Destroy(container.GetComponent<GroundWatch>());
                UnityEngine.Object.Destroy(container.GetComponent<DestroyOnGroundMissing>());
                          
                isBlocked = false;
                
                timer.In(1, () =>
                {
                    FillLootContainer(container);                    
                    isUnlocked = false;
                    canBeUnlocked = false;
                    isLooted = false;
                    NotifyEventStarted();
                    CreateNewZone();

                    ServerMgr.Instance.StartCoroutine(CreateNPCs(location));
                });

                return;
            }

            START_TIMER:
            StartTimers();
        }

        private void CreateNewZone()
        {
            if (ZoneManager && configData.EventOptions.ZoneSettings.TempZone)
            {
                string[] flags = new string[configData.EventOptions.ZoneSettings.Flags.Length + 2];

                flags[0] = "radius";
                flags[1] = configData.EventOptions.ZoneSettings.ZoneRadius.ToString();

                for (int i = 0; i < configData.EventOptions.ZoneSettings.Flags.Length; i++)
                {
                    flags[i + 2] = configData.EventOptions.ZoneSettings.Flags[i];
                }

                ZoneManager.Call("CreateOrUpdateZone", "TreasureBox Zone", flags, container.transform.position);
            }
        }

        private void DestroyZone()
        {
            if (ZoneManager && configData.EventOptions.ZoneSettings.TempZone)
            {
                ZoneManager.Call("EraseZone", "TreasureBox Zone");
            }
        }

        private void ClearContainer(ItemContainer itemContainer)
        {
            if (itemContainer == null || itemContainer.itemList == null)
                return;

            while (itemContainer.itemList.Count > 0)
            {
                Item item = itemContainer.itemList[0];
                item.RemoveFromContainer();
                item.Remove(0f);
            }
        }

        private void FillLootContainer(BaseEntity entity)
        {
            if (entity == null) return;
            ItemContainer itemContainer = entity.GetComponent<StorageContainer>()?.inventory;
            if (itemContainer == null)
                return;

            int count = UnityEngine.Random.Range(lootTable.Minimum, lootTable.Maximum);

            if (itemContainer.capacity < count)            
                itemContainer.capacity = count;

            List<ConfigData.LootTables.LootItem> Items = new List<ConfigData.LootTables.LootItem>(lootTable.Items);
            for (int i = 0; i < count; i++)
            {
                if (Items.Count == 0)
                    Items = new List<ConfigData.LootTables.LootItem>(lootTable.Items);

                ConfigData.LootTables.LootItem lootItem = Items.GetRandom();
                if (lootItem == null)
                {
                    count--;
                    continue;
                }

                bool isBlueprint = lootItem.Name.EndsWith(".bp");
                string shortname = isBlueprint ? lootItem.Name.Substring(0, lootItem.Name.Length - 3) : lootItem.Name;

                if (!itemNameToId.ContainsKey(shortname))
                {
                    PrintError($"Invalid item shortname set in loot list : {shortname}");
                    count--;
                    continue;
                }

                Item item = null;
                if (isBlueprint)
                {
                    item = ItemManager.CreateByItemID(-996920608, 1, 0);
                    item.blueprintTarget = itemNameToId[shortname];
                    item.amount = UnityEngine.Random.Range(lootItem.Minimum, lootItem.Maximum);
                }
                else item = ItemManager.CreateByName(shortname, 1, lootItem.SkinID);

                if (item != null)
                {
                    item.amount = UnityEngine.Random.Range(lootItem.Minimum, lootItem.Maximum);
                    item.MoveToContainer(itemContainer, -1, false);                    
                }
                Items.Remove(lootItem);
            }
            isBlocked = true;
            container.SetFlag(BaseEntity.Flags.Locked, true);
            if (configData.EventOptions.SmokeEnabled)
                Effect.server.Run(SMOKE_PREFAB, entity, 0, new Vector3(), new Vector3(), null, true);
        }
        
        private void NotifyEventStarted()
        {
            nextTrigger = GrabCurrentTime() + configData.Timers.Unlock;
            timeToUnlock = timer.In(configData.Timers.Unlock, UnlockContainer);

            if (configData.EventOptions.UISettings.Enabled)
                RefreshAllUI();

            PrintToChat(string.Format(msg("eventStart1"), FormatTime(configData.Timers.Unlock)));

            if (configData.EventOptions.BroadcastCoords)
                PrintToChat(string.Format(msg("broadcastCoords"), configData.EventOptions.UISettings.GridReference ? MapHelper.PositionToString(container.transform.position) : $"X: {Math.Round(container.transform.position.x, 1)}, Z: {Math.Round(container.transform.position.z, 1)}"));

            if (configData.MapOptions.Lusty.Enabled && !string.IsNullOrEmpty(configData.MapOptions.Lusty.Marker))            
                AddMapMarker(); 
            
            if (configData.MapOptions.Map.Enabled)
            {
                mapMarker = (MapMarkerGenericRadius)GameManager.server.CreateEntity(MARKER_PREFAB, container.transform.position + (UnityEngine.Random.onUnitSphere * configData.MapOptions.Map.Offset), new Quaternion());
                mapMarker.enableSaving = false;
                mapMarker.Spawn();

                mapMarker.radius = configData.MapOptions.Map.Radius;
                mapMarker.alpha = configData.MapOptions.Map.Alpha;

                Color color = string.IsNullOrEmpty(lootTable.MarkerColor) ? ConvertToColor(configData.MapOptions.Map.Color) : ConvertToColor(lootTable.MarkerColor);
                Color color2 = new Color(0, 0, 0, 0);

                mapMarker.color1 = color;
                mapMarker.color2 = color2;

                mapMarker.SendUpdate();

                if (configData.MapOptions.Map.BroadcastMarker)
                    PrintToChat(msg("broadcastMap"));  
            }                     
        }

        private void UnlockContainer()
        {
            if (container != null)
            {                
                nextTrigger = GrabCurrentTime() + configData.Timers.Loot;
                timeToDestroy = timer.In(configData.Timers.Loot, DestroyContainer);
               
                if (configData.EventOptions.UISettings.Enabled)
                    RefreshAllUI();

                PrintToChat(string.Format(msg("containerUnlock"), FormatTime(configData.Timers.Loot)));

                if (configData.EventOptions.BotSettings.NPCLock && customNpcs.Count > 0)
                {
                    canBeUnlocked = true;
                    return;
                }

                if (configData.EventOptions.EnableFirework)
                {
                    MortarFirework firework = GameManager.server.CreateEntity(configData.EventOptions.FireworkPrefab, container.transform.position) as MortarFirework;
                    if (firework != null)
                    {
                        firework.enableSaving = false;
                        firework.Spawn();
                        firework.Ignite(firework.transform.position);
                    }
                }

                container.SetFlag(BaseEntity.Flags.Locked, false);
                isUnlocked = true;
            }
            else StartTimers();            
        } 
        
        private void DestroyContainer()
        {            
            if (container != null)
            {                
                DestroyEvent(false);
                SubscribeToMethods(false);
                if (!isLooted)                
                    PrintToChat(msg("eventLose"));
                timer.In(configData.EventOptions.ZoneSettings.ActiveTime, DestroyZone);

                if (configData.MapOptions.Lusty.Enabled)
                    RemoveMapMarker();

                if (configData.MapOptions.Map.Enabled && mapMarker != null && !mapMarker.IsDestroyed)                
                    mapMarker.Kill();
                
            }
            StartTimers();
        }

        private string FormatTime(double time)
        {
            TimeSpan dateDifference = TimeSpan.FromSeconds((float)time);
            int days = dateDifference.Days;
            int hours = dateDifference.Hours;
            hours += (days * 24);
            int mins = dateDifference.Minutes;
            int secs = dateDifference.Seconds;
            if (hours > 0)
                return string.Format("{0:00}:{1:00}:{2:00}", hours, mins, secs);
            else return string.Format("{0:00}:{1:00}", mins, secs);
        }

        private double GrabCurrentTime() => DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;

        private Color ConvertToColor(string color)
        {
            if (color.StartsWith("#"))
                color = color.Substring(1);
            int red = int.Parse(color.Substring(0, 2), NumberStyles.AllowHexSpecifier);
            int green = int.Parse(color.Substring(2, 2), NumberStyles.AllowHexSpecifier);
            int blue = int.Parse(color.Substring(4, 2), NumberStyles.AllowHexSpecifier);

            return new Color((float)red / 255, (float)green / 255, (float)blue / 255);            
        }        
        #endregion

        #region NPC Controller   
        private static bool IsInSafeZone(Vector3 position)
        {
            int count = Physics.OverlapSphereNonAlloc(position, 1f, Vis.colBuffer, 1 << 18, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                Collider collider = Vis.colBuffer[i];
                if (collider.GetComponent<TriggerSafeZone>())
                    return true;
            }

            return false;
        }

        private static IEnumerator CreateNPCs(Vector3 position)
        {
            for (int i = 0; i < configData.EventOptions.BotSettings.Amount; i++)
            {                
                Vector2 onCirle = UnityEngine.Random.insideUnitCircle.normalized * 10f;

                CustomScientistNPC customNpc = ChaosNPC.SpawnNPC(Instance, position + new Vector3(onCirle.x, 0f, onCirle.y), configData.EventOptions.BotSettings.CustomNPCSettings);
                if (customNpc != null)
                    Instance.customNpcs.Add(customNpc);

                yield return CoroutineEx.waitForSeconds(0.5f);
            }            
        }
        #endregion

        #region CustomNPC
        public bool InitializeStates(BaseAIBrain customNPCBrain) => false;

        public bool WantsToPopulateLoot(CustomScientistNPC customNpc, NPCPlayerCorpse npcplayerCorpse) => false;

        public byte[] GetCustomDesign() => null;
        #endregion

        #region UI
        public class UI
        {
            static public CuiElementContainer CreateElementContainer(string panelName, string color, string aMin, string aMax, bool useCursor = false)
            {
                CuiElementContainer NewElement = new CuiElementContainer()
                {
                    {
                        new CuiPanel
                        {
                            Image = {Color = color},
                            RectTransform = {AnchorMin = aMin, AnchorMax = aMax},
                            CursorEnabled = useCursor
                        },
                        new CuiElement().Parent = "Hud", 
                        panelName
                    }
                };
                return NewElement;
            }
            static public void AddImage(ref CuiElementContainer container, string panel, string png, string aMin, string aMax, float fadeOut = 0f)
            {
                container.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = panel,
                    Components =
                    {
                        new CuiRawImageComponent {Png = png, Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                        new CuiRectTransformComponent {AnchorMin = aMin, AnchorMax = aMax }
                    }
                });
            }
            static public void CreateLabel(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiLabel
                {
                    Text = { Color = color, FontSize = size, Align = align, Text = text },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax }
                },
                panel);

            }
            public static string Color(string hexColor, float alpha)
            {
                if (hexColor.StartsWith("#"))
                    hexColor = hexColor.TrimStart('#');
                int red = int.Parse(hexColor.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                int green = int.Parse(hexColor.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                int blue = int.Parse(hexColor.Substring(4, 2), NumberStyles.AllowHexSpecifier);
                return $"{(double)red / 255} {(double)green / 255} {(double)blue / 255} {alpha}";
            }
        }
        #endregion
              
        #region UI Creation
        private const string Main = "TreasureUIMain";
        private void CreateTreasureUI(BasePlayer player)
        {
            CuiElementContainer MainCont = UI.CreateElementContainer(Main, UI.Color(configData.EventOptions.UISettings.UIBackgroundColor, configData.EventOptions.UISettings.UIOpacity), $"{configData.EventOptions.UISettings.XPosition} {configData.EventOptions.UISettings.YPosition}", $"{configData.EventOptions.UISettings.XPosition + configData.EventOptions.UISettings.XDimension} {configData.EventOptions.UISettings.YPosition + configData.EventOptions.UISettings.YDimension}");

            if (!string.IsNullOrEmpty(treasureIcon))
                UI.AddImage(ref MainCont, Main, treasureIcon, "0.01 0.05", "0.12 0.95");
            UI.CreateLabel(ref MainCont, Main, "", string.Format( isUnlocked ? msg("despawnsIn", player.UserIDString) : msg("unlocksIn", player.UserIDString), configData.EventOptions.UISettings.GridReference ? MapHelper.PositionToString(container.transform.position) : $"X: {Math.Round(container.transform.position.x, 1)}, Z: {Math.Round(container.transform.position.z, 1)}", GetFormatTime()), 15, "0.14 0", "1 1", TextAnchor.MiddleLeft);

            CuiHelper.DestroyUi(player, Main);
            CuiHelper.AddUi(player, MainCont);
        }

        private string GetFormatTime()
        {
            double time = nextTrigger - GrabCurrentTime();
            double minutes = Math.Floor((double)(time / 60));
            time -= (int)(minutes * 60);
            return string.Format("{0:00}:{1:00}", minutes, time);
        }

        private void RefreshAllUI()
        {
            uiTimer = timer.Repeat(1, (int)(nextTrigger - GrabCurrentTime()) - 1, () =>
            {
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                {
                    if (container == null) return;
                    if (!isLooted)
                        CreateTreasureUI(player);
                    else CuiHelper.DestroyUi(player, Main);
                }
            });
        }
        #endregion

        #region External Hooks
        private void AddMapMarker() => LustyMap?.Call("AddMarker", container.transform.position.x, container.transform.position.z, msg("iconName"), configData.MapOptions.Lusty.Marker);              
        
        private void RemoveMapMarker() => LustyMap?.Call("RemoveMarker", msg("iconName"));        
        #endregion

        #region Commands
        [ChatCommand("th")]
        void cmdTH(BasePlayer player, string command, string[] args)
        {
            if (!isLoaded)
            {
                SendReply(player, "The plugin is not loaded because : " + reason);
                return;
            }
            if (args.Length == 0)
            {
                SendReply(player, $"<color=#ce422b>{Title}</color>  <color=#939393>v.</color><color=#ce422b>{Version}</color>");
                SendReply(player, "<color=#ce422b>/th info</color><color=#939393> - Shows the time remaining before the next event step and the position of the treasure box (if applicable)</color>");
                if (player.IsAdmin)
                {
                    SendReply(player, "<color=#ce422b>/th start <opt:forced></color><color=#939393> - Start the event, add the 'forced' argument to ignore player counts</color>");
                    SendReply(player, "<color=#ce422b>/th starthere <opt:forced></color><color=#939393> - Start a new event on your position, add the 'forced' argument to ignore player counts</color>");
                    SendReply(player, "<color=#ce422b>/th cancel</color><color=#939393> - Cancels the current event</color>");
                    SendReply(player, "<color=#ce422b>/th unlock</color><color=#939393> - Pre-maturely unlock the treasure box</color>");
                }
            }
            else
            {
                switch (args[0].ToLower())
                {
                    case "info":
                        string time = FormatTime(nextTrigger - GrabCurrentTime());
                        if (container != null)
                        {
                            if (isUnlocked)
                                SendReply(player, msg("isUnlocked", player.UserIDString));
                            else SendReply(player, string.Format(msg("nextUnlock", player.UserIDString), time));
                            SendReply(player, string.Format(msg("currentPos", player.UserIDString), configData.EventOptions.UISettings.GridReference ? MapHelper.PositionToString(container.transform.position) : $"X: {Math.Round(container.transform.position.x, 1)}, Z: {Math.Round(container.transform.position.z, 1)}"));
                        }
                        else SendReply(player, string.Format(msg("nextDrop", player.UserIDString), time));
                        return;
                    case "start":
                        if (!player.IsAdmin)
                            return;

                        if (container != null)
                        {
                            SendReply(player, "<color=#939393>There is already a event in progress!</color>");
                            return;
                        }
                        else
                        {
                            nextEvent.Destroy();
                            SpawnContainer(null, args.Length == 2 && args[1].ToLower() == "forced");
                            SendReply(player, "<color=#939393>You have force started the event!</color>");
                        }
                        return;
                    case "starthere":
                        if (!player.IsAdmin)
                            return;

                        if (container != null)
                        {
                            SendReply(player, "<color=#939393>There is already a event in progress!</color>");
                            return;
                        }
                        else
                        {
                            nextEvent.Destroy();
                            SpawnContainer(player.transform.position, args.Length == 2 && args[1].ToLower() == "forced");
                            SendReply(player, "<color=#939393>You have started a event on your position!</color>");
                        }
                        return;
                    case "cancel":
                        if (!player.IsAdmin) return;
                        if (container == null)
                        {
                            SendReply(player, "<color=#939393>There is not currently a event in progress!</color>");
                            return;
                        }
                        else
                        {
                            DestroyEvent(true);
                            DestroyZone();
                            StartTimers();
                            SendReply(player, "<color=#939393>You have cancelled the current event!</color>");
                        }
                        return;
                    case "unlock":
                        if (!player.IsAdmin) return;
                        if (container != null)
                        {
                            if (isUnlocked)
                                SendReply(player, "<color=#939393>The treasure box is already unlocked!</color>");
                            else
                            {
                                timeToUnlock.Destroy();
                                UnlockContainer();
                                SendReply(player, "<color=#939393>You have unlocked the treasure box!</color>");
                            }
                        }
                        return;
                    default:
                        SendReply(player, "<color=#939393>Invalid command</color>");
                        break;
                }
            }
        }
        [ConsoleCommand("th")]
        void ccmdTH(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null) return;
            if (!isLoaded)
            {
                SendReply(arg, "The plugin is not loaded because : " + reason);
                return;
            }
            if (arg.Args == null || arg.Args.Length == 0)
            {
                SendReply(arg, $"- {Title}  v.{Version}   - {Author} @ chaoscode.io -");
                SendReply(arg, "th start <forced> - Force start the event");
                SendReply(arg, "th cancel - Cancels the current event");
                SendReply(arg, "th unlock - Manually unlock the treasure box");
                SendReply(arg, "th clearicon - Clears any TH related icons from the map");
            }
            else
            {
                switch (arg.Args[0].ToLower())
                {                   
                    case "start":
                        if (container != null)                        
                            SendReply(arg, "There is already a event in progress!");                        
                        else
                        {
                            nextEvent.Destroy();
                            SpawnContainer(null, arg.Args.Length == 2 && arg.Args[1].ToLower() == "forced");
                            SendReply(arg, "You have force started the event!");
                        }
                        return;                    
                    case "cancel":
                        if (container == null)                        
                            SendReply(arg, "There is not currently a event in progress!");                        
                        else
                        {
                            DestroyEvent(true);
                            DestroyZone();
                            StartTimers();
                            SendReply(arg, "You have cancelled the current event!");
                        }
                        return;
                    case "unlock":
                        if (container == null)                        
                            SendReply(arg, "There is not currently a event in progress!");
                        else
                        {
                            if (isUnlocked)
                                SendReply(arg, "The treasure box is already unlocked!");
                            else
                            {
                                timeToUnlock.Destroy();
                                UnlockContainer();
                                SendReply(arg, "You have unlocked the treasure box!");
                            }
                        }
                        return;
                    case "clearicon":
                        RemoveMapMarker();
                        return;
                    default:
                        SendReply(arg, "Invalid command");
                        break;
                }
            }
        }
        #endregion
        
        #region Imagery
        private WWW info;
        public void Add(string url)
        {
            info = new WWW(url);
            TryDownloadImage();
        }
        void TryDownloadImage()
        {
            if (!info.isDone)
            {
                timer.In(1, TryDownloadImage);
                return;
            }
            if (!string.IsNullOrEmpty(info.error))
            {
                PrintError(string.Format("Failed to load the Treasure Icon! Error: {0}", info.error));
                return;
            }
            else treasureIcon = FileStorage.server.Store(info.bytes, FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID).ToString();              
        }
        #endregion

        #region Config        
        private static ConfigData configData;

        private class ConfigData
        {          
            [JsonProperty(PropertyName = "Event Timers")]  
            public EventTimers Timers { get; set; }

            [JsonProperty(PropertyName = "Event Options")]
            public Options EventOptions { get; set; }

            [JsonProperty(PropertyName = "Loot Containers (Chosen at random)")]
            public List<LootTables> LootTable { get; set; }

            [JsonProperty(PropertyName = "Map Options")]
            public MapIntegration MapOptions { get; set; }

            public class LootTables
            {
                [JsonProperty(PropertyName = "Container skin ID")]
                public ulong SkinID { get; set; }

                [JsonProperty(PropertyName = "Minimum amount of items to spawn")]
                public int Minimum { get; set; }

                [JsonProperty(PropertyName = "Maximum amount of items to spawn")]
                public int Maximum { get; set; }

                [JsonProperty(PropertyName = "Map marker color override (hex)")]
                public string MarkerColor { get; set; }

                [JsonProperty(PropertyName = "Loot list")]
                public List<LootItem> Items { get; set; }

                public class LootItem
                {
                    [JsonProperty(PropertyName = "Item shortname")]
                    public string Name { get; set; }

                    [JsonProperty(PropertyName = "Minimum amount of item")]
                    public int Minimum { get; set; }

                    [JsonProperty(PropertyName = "Maximum amount of item")]
                    public int Maximum { get; set; }

                    [JsonProperty(PropertyName = "Skin ID")]
                    public ulong SkinID { get; set; }
                }
            }
            public class EventTimers
            {
                [JsonProperty(PropertyName = "Minimum time between events (seconds)")]
                public int MinInterval { get; set; }

                [JsonProperty(PropertyName = "Maximum time between events (seconds)")]
                public int MaxInterval { get; set; }

                [JsonProperty(PropertyName = "Amount of time before the box unlocks (seconds)")]
                public int Unlock { get; set; }

                [JsonProperty(PropertyName = "Amount of time the box will remain on the map (seconds)")]
                public int Loot { get; set; }
            }
            public class MapIntegration
            {
                [JsonProperty(PropertyName = "Ingame map integration")]
                public MapOptions Map { get; set; }

                [JsonProperty(PropertyName = "LustyMap intergration")]
                public LustyOptions Lusty { get; set; }

                public class MapOptions
                {                    
                    [JsonProperty(PropertyName = "Show a radius marker on the map")]
                    public bool Enabled { get; set; }

                    [JsonProperty(PropertyName = "The radius of the marker")]
                    public float Radius { get; set; }

                    [JsonProperty(PropertyName = "Maximum amount to offset the marker position from the box position")]
                    public float Offset { get; set; }

                    [JsonProperty(PropertyName = "Marker color (hex)")]
                    public string Color { get; set; }

                    [JsonProperty(PropertyName = "Marker transparency (0.0 - 1.0)")]
                    public float Alpha { get; set; }

                    [JsonProperty(PropertyName = "Broadcast information about the marker to chat")]
                    public bool BroadcastMarker { get; set; }
                }
                public class LustyOptions
                {
                    [JsonProperty(PropertyName = "Show a icon on LustyMap")]
                    public bool Enabled { get; set; }

                    [JsonProperty(PropertyName = "URL of the icon to be used")]
                    public string Marker { get; set; }
                }
            }
            public class Options
            {
                [JsonProperty(PropertyName = "Use RandomSpawns for box spawn points")]
                public bool RandomSpawnPoints { get; set; }

                [JsonProperty(PropertyName = "Use a spawn file for box spawn points")]
                public string Spawnfile { get; set; }

                [JsonProperty(PropertyName = "Minimum players required online to trigger the event")]
                public int MinPlayers { get; set; }

                [JsonProperty(PropertyName = "Show a smoke signal on the box location")]
                public bool SmokeEnabled { get; set; }

                [JsonProperty(PropertyName = "Broadcast the box co-ordinates to chat")]
                public bool BroadcastCoords { get; set; }

                [JsonProperty(PropertyName = "Shoot a firework into the sky when the box has been unlocked")]
                public bool EnableFirework { get; set; }

                [JsonProperty(PropertyName = "Firework prefab path")]
                public string FireworkPrefab { get; set; }

                [JsonProperty(PropertyName = "UI Options")]
                public UIOptions UISettings { get; set; }

                [JsonProperty(PropertyName = "Event Zone Options")]
                public ZoneOptions ZoneSettings { get; set; }

                [JsonProperty(PropertyName = "NPC Options")]
                public BotOptions BotSettings { get; set; }

                public class UIOptions
                {
                    [JsonProperty(PropertyName = "Enabled the UI display")]
                    public bool Enabled { get; set; }

                    [JsonProperty(PropertyName = "Display grid reference instead of coordinates")]
                    public bool GridReference { get; set; }

                    [JsonProperty(PropertyName = "URL of the icon to be used in the UI")]
                    public string Icon { get; set; }

                    [JsonProperty(PropertyName = "Position - X Start")]
                    public float XPosition { get; set; }

                    [JsonProperty(PropertyName = "Position - Y Start")]
                    public float YPosition { get; set; }

                    [JsonProperty(PropertyName = "Position - X Dimensions")]
                    public float XDimension { get; set; }

                    [JsonProperty(PropertyName = "Position - Y Dimensions")]
                    public float YDimension { get; set; }

                    [JsonProperty(PropertyName = "UI background color (hex)")]
                    public string UIBackgroundColor { get; set; }

                    [JsonProperty(PropertyName = "UI transparency (0.0 - 1.0)")]
                    public float UIOpacity { get; set; }
                }
                public class ZoneOptions
                {
                    [JsonProperty(PropertyName = "Disable building and deployable placement within the set radius of the box")]
                    public bool DisableBuilding { get; set; }   
                    
                    [JsonProperty(PropertyName = "Radius of the event zone")]
                    public float ZoneRadius { get; set; }

                    [JsonProperty(PropertyName = "Create a temporary zone around the box when it spawns using ZoneManager")]
                    public bool TempZone { get; set; }

                    [JsonProperty(PropertyName = "The amount of time the Zone Manager zone will remain active after the event is over (seconds)")]
                    public int ActiveTime { get; set; }

                    [JsonProperty(PropertyName = "Flags to be applied to the temporary Zone Manager zone")]
                    public string[] Flags { get; set; }
                }
                public class BotOptions
                {
                    [JsonProperty(PropertyName = "Spawn NPCs at the box location")]
                    public bool Enabled { get; set; }

                    [JsonProperty(PropertyName = "Kill any surviving bots when the event has finished")]
                    public bool DestroyOnFinish { get; set; }

                    [JsonProperty(PropertyName = "Amount of NPCs to spawn")]
                    public int Amount { get; set; }                   

                    [JsonProperty(PropertyName = "Keep box locked until all NPC's have been killed")]
                    public bool NPCLock { get; set; }

                    [JsonProperty(PropertyName = "Custom NPC Settings")]
                    public NPCSettings CustomNPCSettings { get; set; }
                }
            }
            public Oxide.Core.VersionNumber Version { get; set; }
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            configData = Config.ReadObject<ConfigData>();

            if (configData.Version < Version)
                UpdateConfigValues();

            Config.WriteObject(configData, true);
        }

        protected override void LoadDefaultConfig() => configData = GetBaseConfig();

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                MapOptions = new ConfigData.MapIntegration
                {
                    Lusty = new ConfigData.MapIntegration.LustyOptions
                    {
                        Marker = "http://www.rustedit.io/images/treasureicon.png",
                        Enabled = false
                    },
                    Map = new ConfigData.MapIntegration.MapOptions
                    {
                        Alpha = 0.5f,
                        Color = "#ce422b",
                        Enabled = true,
                        Radius = 4,
                        Offset = 25,
                        BroadcastMarker = true
                    }
                },
                LootTable = new List<ConfigData.LootTables>
                {
                    new ConfigData.LootTables
                    {
                        SkinID = 882223700,
                        Maximum = 4,
                        Minimum = 1,
                        Items = new List<ConfigData.LootTables.LootItem>
                        {
                            new ConfigData.LootTables.LootItem {Name = "syringe.medical", Maximum = 6, Minimum = 2 },
                            new ConfigData.LootTables.LootItem {Name = "largemedkit", Maximum = 2, Minimum = 1 },
                            new ConfigData.LootTables.LootItem {Name = "bandage", Maximum = 4, Minimum = 1 },
                            new ConfigData.LootTables.LootItem {Name = "antiradpills", Maximum = 3, Minimum = 1 }
                        }
                    },
                    new ConfigData.LootTables
                    {
                        SkinID = 10141,
                        Maximum = 4,
                        Minimum = 1,
                        Items = new List<ConfigData.LootTables.LootItem>
                        {
                            new ConfigData.LootTables.LootItem {Name = "ammo.rifle", Maximum = 100, Minimum = 10 },
                            new ConfigData.LootTables.LootItem {Name = "ammo.pistol", Maximum = 100, Minimum = 10 },
                            new ConfigData.LootTables.LootItem {Name = "ammo.rocket.basic", Maximum = 3, Minimum = 1 },
                            new ConfigData.LootTables.LootItem {Name = "ammo.shotgun.slug", Maximum = 20, Minimum = 10 },
                            new ConfigData.LootTables.LootItem {Name = "pistol.m92", Maximum = 1, Minimum = 1 },
                            new ConfigData.LootTables.LootItem {Name = "rifle.ak", Maximum = 1, Minimum = 1 },
                            new ConfigData.LootTables.LootItem {Name = "rifle.bolt", Maximum = 1, Minimum = 1 },
                            new ConfigData.LootTables.LootItem {Name = "rocket.launcher", Maximum = 1, Minimum = 1 },
                            new ConfigData.LootTables.LootItem {Name = "pistol.revolver", Maximum = 1, Minimum = 1 }
                        }
                    },
                    new ConfigData.LootTables
                    {
                        SkinID = 809975811,
                        Maximum = 4,
                        Minimum = 1,
                        Items = new List<ConfigData.LootTables.LootItem>
                        {
                            new ConfigData.LootTables.LootItem {Name = "apple", Maximum = 6, Minimum = 2 },
                            new ConfigData.LootTables.LootItem {Name = "bearmeat.cooked", Maximum = 4, Minimum = 2 },
                            new ConfigData.LootTables.LootItem {Name = "blueberries", Maximum = 8, Minimum = 4 },
                            new ConfigData.LootTables.LootItem {Name = "corn", Maximum = 8, Minimum = 4 },
                            new ConfigData.LootTables.LootItem {Name = "fish.raw", Maximum = 4, Minimum = 2 },
                            new ConfigData.LootTables.LootItem {Name = "granolabar", Maximum = 4, Minimum = 1 },
                            new ConfigData.LootTables.LootItem {Name = "meat.pork.cooked", Maximum = 8, Minimum = 4 },
                            new ConfigData.LootTables.LootItem {Name = "candycane", Maximum = 2, Minimum = 1 }                           
                        }
                    }
                },                
                EventOptions = new ConfigData.Options
                {
                    Spawnfile = "",
                    MinPlayers = 1,
                    SmokeEnabled = true,
                    BroadcastCoords = false,
                    EnableFirework = false,
                    FireworkPrefab = "assets/prefabs/deployable/fireworks/mortarchampagne.prefab",
                    UISettings = new ConfigData.Options.UIOptions
                    {
                        Icon = "http://www.rustedit.io/images/treasureicon.png",
                        UIBackgroundColor = "#4C4C4C",
                        GridReference = true,
                        Enabled = true,
                        XDimension = 0.275f,
                        XPosition = 0.625f,
                        YDimension = 0.05f,
                        YPosition = 0.93f,
                        UIOpacity = 0.7f
                    },
                    RandomSpawnPoints = false,
                    ZoneSettings = new ConfigData.Options.ZoneOptions
                    {
                        DisableBuilding = true,
                        ZoneRadius = 75,
                        TempZone = true,
                        ActiveTime = 300,
                        Flags = new string[] { "notp", "true", "notrade", "true" }
                    },                    
                    BotSettings = new ConfigData.Options.BotOptions
                    {
                        Amount = 3,
                        Enabled = true,
                        NPCLock = false,
                        CustomNPCSettings = new NPCSettings
                        {
                            Types = new NPCType[] { NPCType.BanditGuard, NPCType.TunnelDweller },
                            RoamRange = 30f,
                            ChaseRange = 90f                            
                        }
                    }
                },
                Timers = new ConfigData.EventTimers
                {
                    MaxInterval = 1200,
                    MinInterval = 600,
                    Loot = 300,
                    Unlock = 180
                },
                Version = Version                
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();
            if (configData.Version < new Core.VersionNumber(0, 2, 45))            
                configData.EventOptions.ZoneSettings = baseConfig.EventOptions.ZoneSettings;            

            if (configData.Version < new Core.VersionNumber(0, 2, 55))
            {
                configData.LootTable = baseConfig.LootTable;
                configData.EventOptions.UISettings.GridReference = baseConfig.EventOptions.UISettings.GridReference;
            }

            if (configData.Version < new Core.VersionNumber(0, 2, 62))
            {
                configData.EventOptions.BotSettings.NPCLock = baseConfig.EventOptions.BotSettings.NPCLock;
            }

            if (configData.Version < new Core.VersionNumber(0, 2, 70))
            {
                configData.EventOptions.BotSettings.DestroyOnFinish = true;
            }

            if (configData.Version < new Core.VersionNumber(0, 2, 73))
            {
                configData.EventOptions.EnableFirework = false;
                configData.EventOptions.FireworkPrefab = "assets/prefabs/deployable/fireworks/mortarchampagne.prefab";                
            }

            if (configData.Version < new Core.VersionNumber(0, 3, 2))
                configData.EventOptions.BotSettings.CustomNPCSettings = new NPCSettings();

            configData.Version = Version;
            PrintWarning("Config update completed!");
        }        
        #endregion

        #region Localization
        string msg(string key, string playerId = "") => lang.GetMessage(key, this, playerId);
        Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            {"eventStart1", "<color=#ce422b>[TreasureHunt] </color><color=#939393>: The Rust gods are pleased and have sent us a Treasure Box. It will unlock in</color><color=#ce422b> {0} </color><color=#939393>to unveil it's epic treasure!</color>" },
            {"nextDrop", "<color=#ce422b>[TreasureHunt]</color><color=#939393> : Time until next drop :</color><color=#ce422b> {0}</color>" },
            {"nextUnlock", "<color=#939393>Time until unlocked :</color><color=#ce422b> {0}</color>" },
            {"isUnlocked", "<color=#939393>Chest is currently unlocked!</color>" },
            {"currentPos", "<color=#939393>Can be found at :</color><color=#ce422b> {0}</color>" },
            {"eventWin", "<color=#ce422b>[TreasureHunt] </color><color=#939393>:</color><color=#ce422b> {0} </color><color=#939393>was able to loot the treasure box!</color>" },
            {"eventLose", "<color=#ce422b>[TreasureHunt] </color><color=#939393>: The treasure box was not found in time, the Rust gods have reclaimed it in dissapointment.</color>" },
            {"containerUnlock", "<color=#ce422b>[TreasureHunt] </color><color=#939393>: The treasure box has been unlocked! You have<color=#ce422b> {0} </color>to find it and claim the loot</color>" },
            {"broadcastMap", "<color=#939393>The treasure box is somewhere inside the<color=#ce422b> red </color>circle on your map!</color>" },
            {"broadcastCoords", "<color=#939393>The box can be found at</color><color=#ce422b> {0} </color>" },
            {"unlocksIn", "<color=#ce422b>{0}</color><color=#939393> - The chest unlocks in :</color><color=#ce422b> {1}</color>" },
            {"despawnsIn", "<color=#ce422b>{0}</color><color=#939393> - The chest despawns in :</color><color=#ce422b> {1}</color>" },
            {"iconName", "treasure box" },
            {"nobuild", "<color=#ce422b>You can not build or deploy item near the treasure box!</color>" },
            {"noLockDeploy", "<color=#ce422b>You are not allowed to place a lock on the treasure box</color>" },
            {"gridRef", "Grid Ref: {0}" }
        };
        #endregion

    }
}
