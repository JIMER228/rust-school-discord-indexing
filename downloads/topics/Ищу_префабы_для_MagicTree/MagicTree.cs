using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    // fix путей префабов 3.0.5
    
    [Info("MagicTree", "byKomander & KirillRnD", "3.0.5")]
    public class MagicTree : RustPlugin
    {
        #region Классы данных и конфигурация

        public class Seed
        {
            public string shortname;
            public string name;
            public ulong skinId;
        }

        public class Wood
        {
            [JsonProperty("UID Дерева")] public NetworkableId woodId;
            [JsonProperty("Осталось времени")] public int NeedTime;
            [JsonProperty("Осталось времени до разрушения")] public int NeedTimeToDestroy = -1;
            [JsonProperty("Текущая стадия (0=A,1=B,2=C,3=D,4=E)")] public int CurrentStage;
            [JsonProperty("Позиция")] public Vector3 woodPos;
            [JsonProperty("Ящики (ID)")] public List<NetworkableId> BoxListed = new List<NetworkableId>();

            [JsonIgnore] public List<BaseEntity> boxes = new List<BaseEntity>();
            [JsonIgnore] public bool finalBoxSpawned = false;
            [JsonIgnore] public bool FinalNotified = false;
        }

        public class BoxItemsList
        {
            [JsonProperty("Shortname предмета")] public string ShortName;
            [JsonProperty("Мин. кол-во")] public int MinAmount;
            [JsonProperty("Макс. кол-во")] public int MaxAmount;
            [JsonProperty("Шанс что предмет будет добавлен (максимально 100%)")] public int Change;
            [JsonProperty("SkinID предмета")] public ulong SkinID;
            [JsonProperty("Имя предмета при создании (Оставьте поле пустым чтобы использовать стандартное название итема)")] public string Name;
            [JsonProperty("Это чертеж")] public bool IsBlueprnt;
        }

        public static PluginConfig config;

        public class PluginConfig
        {
            [JsonProperty("Time (сек на все 5 стадии)")]
            public int Time = 600;

            [JsonProperty("TimetoDestroy (сколько живёт после финала)")]
            public int TimetoDestroy = 1800;

            [JsonProperty("Посадка деревьев разрешена только в земле (запрещены плантации и прочее)")]
            public bool PlanterBoxDisable = false;

            [JsonProperty("Множитель добычи при финальной срубке магического дерева")]
            public int Bonus = 1;

            [JsonProperty("Количество вещей в ящике")]
            public int ItemsCount = 3;

            [JsonProperty("Кол-во ящиков на дереве")]
            public int BoxCount = 3;

            [JsonProperty("Шанс выпадения зерна с дерева (макс-100)")]
            public int Chance = 5;

            [JsonProperty("Префаб ящика")]
            public string CrateBasic = "assets/bundled/prefabs/radtown/crate_underwater_basic.prefab";

            [JsonProperty("Permission (/seed)")]
            public string Permission = "MagicTree.perm";

            [JsonProperty("Список лута (BoxItemsList)")]
            public List<BoxItemsList> casesItems = new List<BoxItemsList>();

            [JsonProperty("Эффект успеха")]
            public string SucEffect = "";

            [JsonProperty("Эффект ошибки")]
            public string ErrorEffect = "";

            [JsonProperty("Настройки семени")]
            public Seed seed = new Seed { shortname = "seed.hemp", name = "Семена магического дерева", skinId = 1787823357 };

            [JsonProperty("BiomeStages (5 префаба на биом)")]
            public Dictionary<string, List<string>> BiomeStages = new Dictionary<string, List<string>>();

            [JsonProperty("Версия конфигурации")]
            public VersionNumber PluginVersion = new VersionNumber(2, 0, 3);

            [JsonProperty("Настройки уведомлений")]
            public Notifications notifications = new Notifications();

            public class Notifications
            {
                [JsonProperty("Ошибка команды")]
                public string CmdError = "Неправильно ввели команду.";
                [JsonProperty("Семена можно сажать только в землю")]
                public string DisablePlantSeed = "Семена разрешено садить только в землю";
                [JsonProperty("Семена нельзя сажать в чужих постройках")]
                public string DisableAuthSeed = "Семена нельзя садить в зоне чужого шкафа";
                [JsonProperty("Ошибка количества")]
                public string CountError = "Неверное кол-во!";
                [JsonProperty("Нет прав")]
                public string Permission = "У вас нет прав!";
                [JsonProperty("Дерево посажено")]
                public string Wood = "<size=14><b>Вы посадили магическое дерево!</b></size>\n<size=9>Скоро оно вырастет и даст плоды!</size>";
                [JsonProperty("Дерево созрело (финальный текст)")]
                public string InfoTextFull = "Магическое дерево выросло - руби и собирай лут!";
                [JsonProperty("Информация о стадии созревания")]
                public string InfoDdraw = "<size=25><b>Магическое дерево</b></size>\n<size=17>Этап созревания дерева: {0}/5\n\nВремя до полного созревания: {1}</size>";
                [JsonProperty("Неверное количество (пример)")]
                public string WrongAmount = "Неверное количество. Пример: /seed 5";
                [JsonProperty("Игрок не найден")]
                public string PlayerNotFound = "Игрок не найден. Пример: /seed PlayerName 5";
                [JsonProperty("Использование команды seed")]
                public string SeedUsage = "Использование:\n/seed AMOUNT или /seed PLAYER AMOUNT";
                [JsonProperty("Уведомление о получении семени (всплывающее)")]
                public string SeedNotification = "<size=14>Вам выпало магическое семечко!</size>^<size=9>Посадите его, оно вырастет в дерево с лутом</size>";
            }

            public static PluginConfig DefaultConfig()
            {
                var defCfg = new PluginConfig
                {
                    Time = 600,
                    TimetoDestroy = 1800,
                    PlanterBoxDisable = false,
                    Bonus = 1,
                    ItemsCount = 3,
                    BoxCount = 3,
                    Chance = 5,
                    CrateBasic = "assets/bundled/prefabs/radtown/crate_underwater_basic.prefab",
                    Permission = "MagicTree.perm",
                    casesItems = new List<BoxItemsList>
                    {
                        new BoxItemsList { ShortName = "scrap", MinAmount = 40, MaxAmount = 80, Change = 80, SkinID = 0, Name = "", IsBlueprnt = false },
                        new BoxItemsList { ShortName = "gunpowder", MinAmount = 100, MaxAmount = 200, Change = 50, SkinID = 0, Name = "", IsBlueprnt = false },
                        new BoxItemsList { ShortName = "metal.refined", MinAmount = 10, MaxAmount = 50, Change = 30, SkinID = 0, Name = "", IsBlueprnt = false }
                    },
                    BiomeStages = new Dictionary<string, List<string>>
                    {
                        ["Arid"] = new List<string>
                        {
                            "assets/bundled/prefabs/autospawn/resource/v3_arid_forest/palm_tree_short_c.prefab",
                            "assets/bundled/prefabs/autospawn/resource/v3_arid_forest/palm_tree_short_a.prefab",
                            "assets/bundled/prefabs/autospawn/resource/v3_arid_forest/palm_tree_med_a.prefab",
                            "assets/bundled/prefabs/autospawn/resource/v3_arid_forest/palm_tree_tall_a.prefab",
                            "assets/bundled/prefabs/autospawn/resource/v3_arid_forest/palm_tree_tall_b.prefab"
                        },
                        ["Temperate1"] = new List<string>
                        {
                            "assets/bundled/prefabs/autospawn/resource/v3_temp_forest/pine_a.prefab",
                            "assets/bundled/prefabs/autospawn/resource/v3_temp_forest/pine_b.prefab",
                            "assets/bundled/prefabs/autospawn/resource/v3_temp_forest/pine_c.prefab",
                            "assets/bundled/prefabs/autospawn/resource/v3_temp_forest/pine_d.prefab",
                            "assets/bundled/prefabs/autospawn/resource/v3_temp_forest/pine_dead_a.prefab"
                        },
                        ["Temperate2"] = new List<string>
                        {
                            "assets/bundled/prefabs/autospawn/resource/v3_temp_forest/birch_small_temp.prefab",
                            "assets/bundled/prefabs/autospawn/resource/v3_temp_forest/birch_medium_temp.prefab",
                            "assets/bundled/prefabs/autospawn/resource/v3_temp_forest/birch_large_temp.prefab",
                            "assets/bundled/prefabs/autospawn/resource/v3_temp_forest/birch_xl_temp.prefab",
                            "assets/bundled/prefabs/autospawn/resource/v3_temp_forest/birch_xl_temp.prefab"
                        },
                        ["Tundra"] = new List<string>
                        {
                            "assets/bundled/prefabs/autospawn/resource/v3_temp_forest/pine_a.prefab",
                            "assets/bundled/prefabs/autospawn/resource/v3_temp_forest/pine_b.prefab",
                            "assets/bundled/prefabs/autospawn/resource/v3_temp_forest/pine_c.prefab",
                            "assets/bundled/prefabs/autospawn/resource/v3_temp_forest/pine_d.prefab",
                            "assets/bundled/prefabs/autospawn/resource/v3_temp_forest/pine_dead_a.prefab"
                        },
                        ["Arctic"] = new List<string>
                        {
                            "assets/bundled/prefabs/autospawn/resource/v3_arctic_forest/pine_a_snow.prefab",
                            "assets/bundled/prefabs/autospawn/resource/v3_arctic_forest/pine_b_snow.prefab",
                            "assets/bundled/prefabs/autospawn/resource/v3_arctic_forest/pine_c_snow.prefab",
                            "assets/bundled/prefabs/autospawn/resource/v3_arctic_forest/pine_d_snow.prefab",
                            "assets/bundled/prefabs/autospawn/resource/v3_arctic_forest/pine_dead_snow_a.prefab"
                        },
                        ["Swamp"] = new List<string>
                        {
                            "assets/bundled/prefabs/autospawn/resource/v3_swamp_forest/birch_a.prefab",
                            "assets/bundled/prefabs/autospawn/resource/v3_swamp_forest/birch_b.prefab",
                            "assets/bundled/prefabs/autospawn/resource/v3_swamp_forest/birch_c.prefab",
                            "assets/bundled/prefabs/autospawn/resource/v3_swamp_forest/birch_d.prefab",
                            "assets/bundled/prefabs/autospawn/resource/v3_swamp_forest/birch_dead.prefab"
                        }
                    },
                    PluginVersion = new VersionNumber(2, 0, 3)
                };
                defCfg.seed = new Seed { shortname = "seed.hemp", name = "Семена магического дерева", skinId = 1787823357 };
                return defCfg;
            }
        }

        #endregion

        #region Поля и Lifecycle

        private Dictionary<ulong, Dictionary<NetworkableId, Wood>> WoodsList = new Dictionary<ulong, Dictionary<NetworkableId, Wood>>();
        public static MagicTree ins;
        private List<TreeComponent> treeComponents = new List<TreeComponent>();
        private Dictionary<NetworkableId, bool> generatedBoxes = new Dictionary<NetworkableId, bool>();
        private Dictionary<NetworkableId, Dictionary<string, int>> intendedLoot = new Dictionary<NetworkableId, Dictionary<string, int>>();
        private Dictionary<ulong, float> lastSeedTime = new Dictionary<ulong, float>();
        private HashSet<NetworkableId> usedTrees = new HashSet<NetworkableId>();
        private const float SEED_COOLDOWN = 1f;

        protected override void LoadDefaultConfig()
        {
            PrintWarning("MagicTree: Создаём шаблонный конфиг по умолчанию...");
            config = PluginConfig.DefaultConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<PluginConfig>();
                if (config == null)
                    throw new Exception("Конфигурация вернула null");
            }
            catch (Exception ex)
            {
                PrintWarning("MagicTree: Ошибка чтения конфига: " + ex.Message);
                LoadDefaultConfig();
                SaveConfig();
                return;
            }
            // Если конфиг существует, оставляем его без обновления, чтобы не перезаписывать ваши правки.
        }

        protected override void SaveConfig() => Config.WriteObject(config, true);

        void Loaded()
        {
            ins = this;
            permission.RegisterPermission(config.Permission, this);
            lastSeedTime.Clear();
            usedTrees.Clear();
            PrintWarning("MagicTree Loaded()");
        }

        #endregion

        #region Load/Save Data

        void LoadData()
        {
            try
            {
                WoodsList = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<ulong, Dictionary<NetworkableId, Wood>>>("MagicTree_Data");
                if (WoodsList == null)
                    WoodsList = new Dictionary<ulong, Dictionary<NetworkableId, Wood>>();
            }
            catch
            {
                WoodsList = new Dictionary<ulong, Dictionary<NetworkableId, Wood>>();
            }
        }
        void SaveData() => Interface.GetMod().DataFileSystem.WriteObject("MagicTree_Data", WoodsList);

        #endregion

        #region AddOrRemoveComponent

        void AddOrRemoveComponent(string mode, TreeComponent component, BaseEntity tree, ulong ownerId)
        {
            if (!WoodsList.ContainsKey(ownerId)) return;
            switch (mode)
            {
                case "add":
                    if (!WoodsList[ownerId].ContainsKey(tree.net.ID)) return;
                    var data = WoodsList[ownerId][tree.net.ID];
                    if (data == null) return;
                    var go = new GameObject("TreeCompObj_Restore");
                    go.transform.position = tree.transform.position;
                    var newComp = go.AddComponent<TreeComponent>();
                    newComp.Init(data, tree);
                    treeComponents.Add(newComp);
                    break;
                case "remove":
                    if (component == null || tree == null) return;
                    if (!WoodsList[ownerId].ContainsKey(tree.net.ID)) return;
                    var w = WoodsList[ownerId][tree.net.ID];
                    if (w.boxes.Count > 0)
                    {
                        foreach (var b in w.boxes)
                            b?.Kill();
                        w.boxes.Clear();
                    }
                    component.DestroyComponent();
                    if (treeComponents.Contains(component))
                        treeComponents.Remove(component);
                    break;
            }
        }

        #endregion

        #region OnEntityBuilt

        void OnEntityBuilt(Planner planner, GameObject go, Vector3 pos)
        {
            if (planner == null || go == null) return;
            var player = planner.GetOwnerPlayer();
            if (player == null) return;
            var ent = go.ToBaseEntity();
            if (ent == null) return;
            if (ent.skinID == config.seed.skinId)
            {
                NextTick(() =>
                {
                    if (ent == null || ent.IsDestroyed) return;
                    if (config.PlanterBoxDisable && ent.GetParentEntity() != null)
                    {
                        Server.Command($"gametipsapi.showforplayer {player.userID} 0 \"{config.notifications.DisablePlantSeed}\"");
                        AddSeed(player, 1, false);
                        ent.Kill();
                        return;
                    }
                    if (player.GetBuildingPrivilege()?.IsAuthed(player) == false)
                    {
                        Server.Command($"gametipsapi.showforplayer {player.userID} 0 \"{config.notifications.DisableAuthSeed}\"");
                        AddSeed(player, 1, false);
                        ent.Kill();
                        return;
                    }
                    SpawnWood(player.userID, ent.transform.position, null, ent, null);
                    Server.Command($"gametipsapi.showforplayer {player.userID} 0 \"{config.notifications.Wood}\"");
                });
            }
        }

        #endregion

        #region SpawnWood (5 стадий)

        public void SpawnWood(ulong ownerId, Vector3 pos, BaseEntity oldTree, BaseEntity seedEntity, TreeComponent oldComp)
        {
            string biome = GetDominantBiome(pos);
            string foundKey = config.BiomeStages.Keys.FirstOrDefault(k => k.Equals(biome, StringComparison.OrdinalIgnoreCase));
            if (foundKey == null)
            {
                PrintWarning($"[MagicTree] Биом '{biome}' не найден. Используем 'Temperate1'.");
                foundKey = "Temperate1";
            }

            var prefabs = config.BiomeStages[foundKey];
            if (prefabs.Count < 5)
            {
                PrintWarning($"[MagicTree] Для биома '{foundKey}' требуется 5 префабов, а есть {prefabs.Count}.");
                return;
            }

            int stageIndex = oldTree == null ? 0 : WoodsList[ownerId][oldTree.net.ID].CurrentStage + 1;
            if (stageIndex > 4) return;

            string prefabPath = prefabs[stageIndex];
            
            // Проверяем существование префаба перед созданием
            var prefab = GameManager.server.FindPrefab(prefabPath);
            if (prefab == null)
            {
                PrintWarning($"[MagicTree] Префаб не найден: {prefabPath}. Используем запасной вариант.");
                prefabPath = "assets/bundled/prefabs/autospawn/resource/v3_temp_forest/pine_a.prefab";
                prefab = GameManager.server.FindPrefab(prefabPath);
                
                if (prefab == null)
                {
                    PrintError($"[MagicTree] Не удалось создать дерево: запасной префаб тоже не найден.");
                    return;
                }
            }

            if (oldTree == null)
            {
                seedEntity?.Kill();
            }

            var newTree = GameManager.server.CreateEntity(prefabPath, pos);
            if (!newTree)
            {
                PrintError($"[MagicTree] Не удалось создать дерево из префаба: {prefabPath}");
                return;
            }

            newTree.OwnerID = ownerId;
            newTree.Spawn();

            if (oldTree == null)
            {
                if (!WoodsList.ContainsKey(ownerId))
                    WoodsList[ownerId] = new Dictionary<NetworkableId, Wood>();
                WoodsList[ownerId][newTree.net.ID] = new Wood { woodId = newTree.net.ID, CurrentStage = 0, NeedTime = config.Time / 5, woodPos = pos };
            }
            else
            {
                WoodsList[ownerId].Remove(oldTree.net.ID);
                WoodsList[ownerId][newTree.net.ID] = new Wood { woodId = newTree.net.ID, CurrentStage = stageIndex, NeedTime = config.Time / 5, woodPos = pos };
                oldTree.Kill();
            }

            var go = new GameObject($"TreeStage{stageIndex}Obj");
            go.transform.position = pos;
            var comp = go.AddComponent<TreeComponent>();
            comp.Init(WoodsList[ownerId][newTree.net.ID], newTree);
            treeComponents.Add(comp);

            if (oldComp != null && treeComponents.Contains(oldComp))
                treeComponents.Remove(oldComp);
        }

        #endregion

        #region TreeComponent

        public class TreeComponent : BaseEntity
        {
            public BaseEntity tree;
            public Wood data;
            private SphereCollider sphereCollider;
            public List<BasePlayer> PlayersTrigger = new List<BasePlayer>();

            void Awake()
            {
                sphereCollider = gameObject.GetComponent<SphereCollider>() ?? gameObject.AddComponent<SphereCollider>();
                sphereCollider.gameObject.layer = (int)Rust.Layer.Reserved1;
                sphereCollider.isTrigger = true;
                sphereCollider.radius = 4f;
            }

            public void Init(Wood w, BaseEntity entity)
            {
                if (entity == null) { Destroy(this); return; }
                tree = entity;
                data = w;
                InvokeRepeating(nameof(DrawInfo), 1f, 1f);
            }

            void OnTriggerEnter(Collider other)
            {
                var pl = other.GetComponentInParent<BasePlayer>();
                if (pl != null)
                {
                    PlayersTrigger.RemoveAll(x => x == pl);
                    PlayersTrigger.Add(pl);
                }
            }

            void OnTriggerExit(Collider other)
            {
                var pl = other.GetComponentInParent<BasePlayer>();
                if (pl != null)
                    PlayersTrigger.RemoveAll(x => x == pl);
            }

            void DrawInfo()
            {
                if (data == null || tree == null || tree.IsDestroyed) { Destroy(this); return; }
                if (data.NeedTime <= 0 && data.CurrentStage == 4 && !data.finalBoxSpawned && data.BoxListed.Count == 0)
                {
                    data.finalBoxSpawned = true;
                    // Создаём adaptiveCounts для данного дерева не передавая – каждый вызов SpawnBox будет накапливать статистику
                    Dictionary<string, int> adaptiveCounts = new Dictionary<string, int>();
                    MagicTree.ins.SpawnBox(data, config.BoxCount, tree, tree.OwnerID, 0, adaptiveCounts);
                    data.CurrentStage = 5;
                }
                if (data.NeedTime <= 0 && data.CurrentStage < 4)
                {
                    MagicTree.ins.SpawnWood(tree.OwnerID, tree.transform.position, tree, null, this);
                }
                if (data.CurrentStage >= 5 && data.BoxListed.Count > 0)
                {
                    foreach (var pl in PlayersTrigger)
                    {
                        SendNotification(pl, config.notifications.InfoTextFull, 1.1f);
                        if (!data.FinalNotified)
                        {
                            MagicTree.ins.Server.Command($"gametipsapi.showforplayer {pl.userID} 0 \"{config.notifications.InfoTextFull}\"");
                        }
                    }
                    data.FinalNotified = true;
                    data.NeedTimeToDestroy++;
                    if (data.NeedTimeToDestroy > config.TimetoDestroy)
                    {
                        if (MagicTree.ins.WoodsList.ContainsKey(tree.OwnerID) &&
                            MagicTree.ins.WoodsList[tree.OwnerID].ContainsKey(tree.net.ID))
                            MagicTree.ins.WoodsList[tree.OwnerID].Remove(tree.net.ID);
                        var hitInfo = new HitInfo(null, tree, Rust.DamageType.Generic, tree.Health(), tree.transform.position);
                        tree.OnAttacked(hitInfo);
                        Destroy(this);
                    }
                }
                data.NeedTime--;
                PlayersTrigger.RemoveAll(p => p == null || !p.IsConnected || Vector3.Distance(p.transform.position, tree.transform.position) > 4f);
                foreach (var pl in PlayersTrigger)
                {
                    if (data.CurrentStage < 5 && data.NeedTime > 0)
                    {
                        string tStr = FormatTime(data.NeedTime);
                        string msg = string.Format(config.notifications.InfoDdraw, data.CurrentStage + 1, tStr);
                        SendNotification(pl, msg, 1.1f);
                    }
                }
            }

            void SendNotification(BasePlayer player, string msg, float duration)
            {
                if (player == null) return;
                player.SendConsoleCommand("ddraw.text", duration, "1 1 1 1", tree.transform.position + Vector3.up * 2f, msg);
            }

            void OnDestroy()
            {
                if (data != null && data.boxes.Count > 0)
                {
                    foreach (var bx in data.boxes)
                    {
                        if (!bx) continue;
                        bx.SetFlag(BaseEntity.Flags.Busy, false, true);
                        bx.gameObject.AddComponent<MagicTreeHelpers.MagicTreeRBC>();
                    }
                }
            }

            public void DestroyComponent() => Destroy(this);
        }

        #endregion

        #region SpawnBox & AddMagicLoot

        // Изменённый SpawnBox – создаём adaptiveCounts для данного дерева и передаём его в каждый вызов AddMagicLoot
        public void SpawnBox(Wood data, int i, BaseEntity tree, ulong ownerID, int countBox = 0, Dictionary<string, int> adaptiveCounts = null)
        {
            if (data == null || tree == null) return;
            data.BoxListed.Clear();
            data.boxes.Clear();
            if (countBox == 0) countBox = config.BoxCount;
            if (adaptiveCounts == null)
                adaptiveCounts = new Dictionary<string, int>();

            for (int k = 0; k < countBox; k++)
            {
                Vector3 offset = new Vector3(UnityEngine.Random.Range(-5f, 5f),
                                             UnityEngine.Random.Range(5f, 9f),
                                             UnityEngine.Random.Range(-5f, 5f));
                var boxEnt = GameManager.server.CreateEntity(config.CrateBasic, tree.transform.position + offset, Quaternion.identity);
                if (!boxEnt) continue;
                var container = boxEnt.GetComponent<LootContainer>();
                if (container != null)
                {
                    container.initialLootSpawn = false;
                    container.lootDefinition = null;
                }
                boxEnt.enableSaving = false;
                boxEnt.Spawn();
                if (container != null && container.inventory != null)
                {
                    container.inventory.capacity = 12;
                    container.inventory.itemList.Clear();
                }
                // Передаём adaptiveCounts в модифицированный метод AddMagicLoot
                AddMagicLoot(boxEnt, adaptiveCounts);
                boxEnt.SetFlag(BaseEntity.Flags.Reserved8, false, true);
                boxEnt.SetFlag(BaseEntity.Flags.Busy, true, true);
                data.BoxListed.Add(boxEnt.net.ID);
                data.boxes.Add(boxEnt);
            }
        }

        // Изменённый метод AddMagicLoot с адаптивным снижением шансов и взвешенным выбором.
        void AddMagicLoot(BaseEntity box, Dictionary<string, int> adaptiveCounts)
        {
            if (box == null) return;
            var container = box.GetComponent<LootContainer>();
            if (container == null) return;
            Puts($"[MagicTree Debug] AddMagicLoot для ящика {box.net.ID}");
            if (generatedBoxes.ContainsKey(box.net.ID))
            {
                Puts($"[MagicTree Debug] Ящик {box.net.ID} уже генерировался, пропускаем.");
                return;
            }
            generatedBoxes[box.net.ID] = true;
            if (container.inventory.itemList != null && container.inventory.itemList.Count > 0)
                container.inventory.itemList.Clear();
            box.gameObject.AddComponent<LootFiller>();
            NetworkableId boxId = box.net.ID;
            intendedLoot[boxId] = new Dictionary<string, int>();

            Puts($"[MagicTree Loot] Генерация лута для ящика {box.net.ID}");
            // Получаем список уникальных предметов из конфига
            var allItems = config.casesItems.GroupBy(x => x.ShortName).Select(g => g.First()).ToList();
            int requiredSlots = Math.Min(config.ItemsCount, allItems.Count);
            Puts($"[MagicTree Loot] Требуется добавить {requiredSlots} уникальных предметов (из {allItems.Count} доступных).");

            // Используем взвешенный выбор с учетом адаптивного снижения шансов
            for (int slot = 0; slot < requiredSlots; slot++)
            {
                var weightedList = new List<Tuple<BoxItemsList, float>>();
                foreach (var item in allItems)
                {
                    float effectiveChance = item.Change;
                    if (adaptiveCounts.TryGetValue(item.ShortName, out int count))
                    {
                        effectiveChance = item.Change * (1 - 0.2f * count);
                    }
                    if (effectiveChance < 0) effectiveChance = 0;
                    weightedList.Add(Tuple.Create(item, effectiveChance));
                }
                float totalWeight = weightedList.Sum(x => x.Item2);
                BoxItemsList selected = null;
                if (totalWeight > 0)
                {
                    float r = UnityEngine.Random.Range(0f, totalWeight);
                    float cumulative = 0;
                    foreach (var tuple in weightedList)
                    {
                        cumulative += tuple.Item2;
                        if (r <= cumulative)
                        {
                            selected = tuple.Item1;
                            break;
                        }
                    }
                }
                // Если не удалось выбрать по весам, берем с наивысшим базовым шансом
                if (selected == null)
                {
                    selected = allItems.OrderByDescending(x => x.Change).First();
                }
                float currentEffective = selected.Change;
                if (adaptiveCounts.TryGetValue(selected.ShortName, out int cnt))
                    currentEffective = selected.Change * (1 - 0.2f * cnt);
                Puts($"[MagicTree Loot] Выбран предмет: {selected.ShortName} (Базовый Chance={selected.Change}, адаптивный effective chance={currentEffective})");

                allItems.Remove(selected);
                if (adaptiveCounts.ContainsKey(selected.ShortName))
                    adaptiveCounts[selected.ShortName]++;
                else
                    adaptiveCounts[selected.ShortName] = 1;

                int minVal = Math.Min(selected.MinAmount, selected.MaxAmount);
                int maxVal = Math.Max(selected.MinAmount, selected.MaxAmount);
                if (maxVal == 0)
                {
                    Puts($"[MagicTree Loot] {selected.ShortName}: min=0, max=0, пропускаем");
                    slot--;
                    continue;
                }
                int amount = UnityEngine.Random.Range(minVal, maxVal + 1);
                Puts($"[MagicTree Loot] Сгенерировано для {selected.ShortName}: {amount} (диапазон [{minVal}..{maxVal}])");
                intendedLoot[boxId][selected.ShortName] = amount;
                var defItem = ItemManager.FindItemDefinition(selected.ShortName);
                if (defItem == null)
                {
                    PrintError($"[MagicTree] Предмет {selected.ShortName} не найден!");
                    continue;
                }
                if (selected.IsBlueprnt)
                    CreateBlueprintItems(container.inventory, defItem, selected, amount);
                else
                    CreateStackableItems(container.inventory, defItem, selected, amount);
            }
            timer.Once(0.5f, () => AdjustLoot(box));
        }

        void AdjustLoot(BaseEntity box)
        {
            var container = box.GetComponent<LootContainer>();
            if (container == null || container.inventory == null) return;
            NetworkableId boxId = box.net.ID;
            if (!intendedLoot.ContainsKey(boxId)) return;
            var intended = intendedLoot[boxId];
            foreach (var item in container.inventory.itemList)
            {
                if (intended.ContainsKey(item.info.shortname))
                {
                    int expected = intended[item.info.shortname];
                    if (item.amount > expected)
                    {
                        Puts($"[MagicTree AdjustLoot] Корректирую {item.info.shortname}: {item.amount} -> {expected}");
                        item.amount = expected;
                    }
                }
            }
        }

        void CreateBlueprintItems(ItemContainer invContainer, ItemDefinition defItem, BoxItemsList selected, int amount)
        {
            for (int i = 0; i < amount; i++)
            {
                var bp = ItemManager.CreateByName("blueprintbase", 1);
                if (bp == null)
                {
                    PrintError($"[MagicTree] Не удалось создать blueprintbase для {selected.ShortName}!");
                    continue;
                }
                bp.blueprintTarget = defItem.itemid;
                if (!string.IsNullOrEmpty(selected.Name))
                    bp.name = selected.Name;
                bool moved = bp.MoveToContainer(invContainer);
                Puts($"[MagicTree Debug] Blueprint {selected.ShortName} (x1) moved: {moved}");
            }
        }

        void CreateStackableItems(ItemContainer invContainer, ItemDefinition defItem, BoxItemsList selected, int amount)
        {
            int maxStack = defItem.stackable;
            if (maxStack < 1) maxStack = 1;
            int left = amount;
            while (left > 0)
            {
                int spawnAmount = Math.Min(left, maxStack);
                var newItem = ItemManager.CreateByName(selected.ShortName, spawnAmount, selected.SkinID);
                if (newItem == null)
                {
                    PrintError($"[MagicTree] Предмет {selected.ShortName} не найден или не удалось создать!");
                    break;
                }
                if (!string.IsNullOrEmpty(selected.Name))
                    newItem.name = selected.Name;
                bool moved = newItem.MoveToContainer(invContainer);
                Puts($"[MagicTree Debug] Stackable {selected.ShortName} (x{spawnAmount}) moved: {moved}");
                left -= spawnAmount;
            }
        }

        public class LootFiller : MonoBehaviour { }

        #endregion

        #region /seed Command

        [ChatCommand("seed")]
        void CmdSeed(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, config.Permission))
            {
                Effect.server.Run(config.ErrorEffect, player, 0, Vector3.zero, Vector3.forward);
                return;
            }
            if (args.Length == 1)
            {
                if (!int.TryParse(args[0], out int amount))
                {
                    Server.Command($"gametipsapi.showforplayer {player.userID} 0 \"{config.notifications.WrongAmount}\"");
                    return;
                }
                AddSeed(player, amount, false);
                return;
            }
            if (args.Length == 2)
            {
                var target = BasePlayer.Find(args[0]);
                if (!target)
                {
                    Server.Command($"gametipsapi.showforplayer {player.userID} 0 \"{config.notifications.PlayerNotFound}\"");
                    return;
                }
                if (!int.TryParse(args[1], out int amt2))
                {
                    Server.Command($"gametipsapi.showforplayer {player.userID} 0 \"{config.notifications.WrongAmount}\"");
                    return;
                }
                AddSeed(target, amt2);
                return;
            }
            Server.Command($"gametipsapi.showforplayer {player.userID} 0 \"{config.notifications.SeedUsage}\"");
        }

        void AddSeed(BasePlayer pl, int amount, bool showMsg = true)
        {
            if (pl == null) return;

            var item = ItemManager.CreateByName(config.seed.shortname, amount, config.seed.skinId);
            if (item == null)
            {
                PrintError($"[MagicTree Error] Failed to create seed item: {config.seed.shortname}");
                return;
            }

            item.name = config.seed.name;

            // Пробуем добавить предмет в основной инвентарь
            if (item.MoveToContainer(pl.inventory.containerMain))
            {
                Puts($"[MagicTree Debug] Successfully gave seed to {pl.displayName}");
                if (showMsg)
                {
                    // Используем встроенное уведомление вместо GameTipsAPI
                    pl.ChatMessage(config.notifications.SeedNotification.Replace("^", "\n"));
                    
                    if (!string.IsNullOrEmpty(config.SucEffect))
                    {
                        Effect.server.Run(config.SucEffect, pl, 0, Vector3.zero, Vector3.forward);
                    }
                }
            }
            else if (item.MoveToContainer(pl.inventory.containerBelt)) // Пробуем пояс
            {
                Puts($"[MagicTree Debug] Gave seed to belt for {pl.displayName}");
                if (showMsg)
                {
                    pl.ChatMessage(config.notifications.SeedNotification.Replace("^", "\n"));
                }
            }
            else // Если инвентарь полон, дропаем на землю
            {
                Puts($"[MagicTree Debug] Inventory full, dropping seed on ground for {pl.displayName}");
                item.Drop(pl.transform.position + new Vector3(0, 1, 0), Vector3.zero);
                if (showMsg)
                {
                    pl.ChatMessage("Ваш инвентарь полон! Семя упало на землю.");
                }
            }
        }

        #endregion

        #region Helpers

        public static string FormatTime(int sec)
        {
            if (sec < 0) sec = 0;
            var t = TimeSpan.FromSeconds(sec);
            return $"{t.Hours:00}:{t.Minutes:00}:{t.Seconds:00}";
        }

        public void ShowGameTip(BasePlayer player, string msg)
        {
            if (player == null) return;
            Server.Command($"gametipsapi.showforplayer {player.userID} 0 \"{msg}\"");
        }

        #endregion

        #region Биом

        private string GetDominantBiome(Vector3 pos)
        {
            if ((TerrainMeta.TopologyMap.GetTopology(pos) & (int)TerrainTopology.Enum.Swamp) != 0)
                return "Swamp";
            float arid = TerrainMeta.BiomeMap.GetBiome(pos, 1);
            float temperate = TerrainMeta.BiomeMap.GetBiome(pos, 2);
            float tundra = TerrainMeta.BiomeMap.GetBiome(pos, 4);
            float arctic = TerrainMeta.BiomeMap.GetBiome(pos, 8);
            string biome = "Arid";
            float maxVal = arid;
            if (temperate > maxVal) { biome = "Temperate1"; maxVal = temperate; }
            if (tundra > maxVal) { biome = "Tundra"; maxVal = tundra; }
            if (arctic > maxVal) biome = "Arctic";
            if (config.BiomeStages.ContainsKey("Temperate2") && biome == "Arid")
                biome = "Temperate2";
            return biome;
        }

        #endregion

        #region Gathering & Damage

        void OnEntityTakeDamage(BaseEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return;
            
            var player = info.InitiatorPlayer;
            if (player == null) return;

            // Проверяем, является ли сущность деревом
            if (!entity.PrefabName.Contains("tree")) return;

            // Проверяем, не было ли уже семени с этого дерева
            if (usedTrees.Contains(entity.net.ID))
            {
                return;
            }

            // Проверяем кулдаун игрока
            if (lastSeedTime.ContainsKey(player.userID))
            {
                if (Time.time - lastSeedTime[player.userID] < SEED_COOLDOWN) return;
            }

            // Проверяем, не является ли это магическим деревом
            foreach (var ownerList in WoodsList.Values)
            {
                if (ownerList.ContainsKey(entity.net.ID)) return;
            }

            // Проверяем шанс выпадения семени
            if (UnityEngine.Random.Range(0, 100) < config.Chance)
            {
                lastSeedTime[player.userID] = Time.time;
                usedTrees.Add(entity.net.ID); // Отмечаем дерево как использованное
                AddSeed(player, 1, true);
                Puts($"[MagicTree Debug] Seed dropped from tree {entity.net.ID} to player {player.displayName}");
            }
        }

        // Добавляем очистку использованных деревьев при их уничтожении
        void OnEntityDeath(BaseEntity entity, HitInfo info)
        {
            if (entity == null) return;
            
            if (usedTrees.Contains(entity.net.ID))
            {
                usedTrees.Remove(entity.net.ID);
                Puts($"[MagicTree Debug] Removed dead tree {entity.net.ID} from used trees list");
            }
        }

        // Периодическая очистка списка использованных деревьев (на случай, если дерево было уничтожено без события OnEntityDeath)
        void OnServerSave()
        {
            HashSet<NetworkableId> treesToRemove = new HashSet<NetworkableId>();
            
            foreach (var treeId in usedTrees)
            {
                var entity = BaseNetworkable.serverEntities.Find(treeId) as BaseEntity;
                if (entity == null || entity.IsDestroyed)
                {
                    treesToRemove.Add(treeId);
                }
            }

            foreach (var treeId in treesToRemove)
            {
                usedTrees.Remove(treeId);
                Puts($"[MagicTree Debug] Cleaned up destroyed tree {treeId} from used trees list");
            }
        }

        void OnGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            if (dispenser == null || entity == null || item == null) return;
            
            var player = entity.ToPlayer();
            if (player == null) return;

            var tree = dispenser.GetComponent<BaseEntity>();
            if (tree == null) return;

            // Проверяем, является ли это магическим деревом
            bool isMagicTree = false;
            foreach (var ownerList in WoodsList.Values)
            {
                if (ownerList.ContainsKey(tree.net.ID))
                {
                    isMagicTree = true;
                    var wood = ownerList[tree.net.ID];
                    if (wood.CurrentStage >= 4)
                    {
                        // Для магического дерева только увеличиваем количество ресурсов
                        int originalAmount = item.amount;
                        item.amount *= config.Bonus;
                        Puts($"[MagicTree Debug] Magic tree resource bonus: {originalAmount} -> {item.amount}");
                    }
                    break;
                }
            }

            // Если это обычное дерево, проверяем шанс выпадения семени
            if (!isMagicTree && item.info.shortname.Contains("wood"))
            {
                if (UnityEngine.Random.Range(0, 100) < config.Chance)
                {
                    Puts($"[MagicTree Debug] Seed drop from normal tree gathering for {player.displayName}");
                    AddSeed(player, 1, true);
                }
            }
        }

        // Добавляем метод для очистки словаря кулдаунов
        void OnPlayerDisconnected(BasePlayer player)
        {
            if (player == null) return;
            lastSeedTime.Remove(player.userID);
        }

        #endregion
    }
}

namespace Oxide.Plugins.MagicTreeHelpers
{
    using UnityEngine;
    public class MagicTreeRBC : MonoBehaviour
    {
        BaseEntity check;
        void Awake()
        {
            check = GetComponent<BaseEntity>();
            InvokeRepeating(nameof(Untie), 0f, 2f);
        }
        void Untie()
        {
            if (check == null || check.IsDestroyed)
            {
                Destroy(this);
                return;
            }
            check.SetFlag(BaseEntity.Flags.Reserved8, false, false);
            var body = check.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                if (!IsOnGround(check.transform.position) && body.detectCollisions)
                    body.isKinematic = true;
                else
                {
                    Destroy(this);
                    return;
                }
                body.isKinematic = false;
                body.mass = 10f;
            }
        }
        bool IsOnGround(Vector3 pos)
        {
            float y = TerrainMeta.HeightMap.GetHeight(pos);
            return (pos.y - y) < 0.1f;
        }
    }
}
