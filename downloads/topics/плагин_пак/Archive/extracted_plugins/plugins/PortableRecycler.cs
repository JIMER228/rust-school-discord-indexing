using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using UnityEngine;
using Physics = UnityEngine.Physics;

namespace Oxide.Plugins
{
    [Info("Portable Recycler", "RustPlugin.ru / Sparkless / OxideBro", "2.1.6")]
    [Description("Позволяет выдавать карманные переработчики игрокам")]
    class PortableRecycler : RustPlugin
    {
        #region Dictionary

        private Dictionary<ulong, ulong> Recyclers = new Dictionary<ulong, ulong>();

        #endregion

        #region Config
        static PluginConfig config;

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Благодарим за скачивание плагина на сайте RustPlugin.ru.");
            config = PluginConfig.DefaultConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();

            if (config.PluginVersion < Version)
                UpdateConfigValues();
            Config.WriteObject(config, true);



            var defs = ItemManager.GetItemDefinitions();
            var ingridients = new List<ItemAmount>();
            foreach (var item in config.Price)
            {
                def = defs.FirstOrDefault(x =>
                    x.displayName.english == item.Key || x.shortname == item.Key || x.itemid.ToString() == item.Key);
                if (!def)
                {
                    PrintWarning(Messages["Nodef"], item.Key);
                    continue;
                }
                ingridients.Add(new ItemAmount(def, item.Value));
            }

            def = ItemManager.FindItemDefinition("lunar.firecrackers");
            if (!def)
            {
                PrintError("Unable to find the quarry defenition! The plugin can't work at all.\nPlease contact the developer - Vlad-00003 at oxide.");
                Interface.Oxide.UnloadPlugin(Title);
            }
            bp = def.Blueprint;
            if (bp == null)
            {
                bp = def.gameObject.AddComponent<ItemBlueprint>();
                bp.ingredients = ingridients;
                bp.defaultBlueprint = false;
                bp.userCraftable = true;
                bp.isResearchable = false;
                bp.workbenchLevelRequired = config.Workbench;
                bp.amountToCreate = Amount;
                bp.time = config.Time;
                bp.scrapRequired = 750;
                bp.blueprintStackSize = 1;
            }
        }

        private void UpdateConfigValues()
        {
            PluginConfig baseConfig = PluginConfig.DefaultConfig();
            if (config.PluginVersion < Version)
            {
                PrintWarning("Config update detected! Updating config values...");
                PrintWarning("Config update completed!");
            }
            config.PluginVersion = Version;
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        private class PluginConfig
        {
            [JsonProperty("Ресурсы для крафта")]
            public Dictionary<string, int> Price;
            [JsonProperty("Требуемый уровень верстака")]
            public int Workbench;
            [JsonProperty("Время крафта")]
            public float Time;
            [JsonProperty("Команда для крафта")]
            public string Command;
            [JsonProperty("Шанс что переработчик будет скрафчен")]
            public float Chance;
            [JsonProperty("Можно ли подбирать переработчик")]
            public bool Available;
            [JsonProperty("Подбор только владельцем?")]
            public bool Owner;
            [JsonProperty("Право на постройку для подбора")]
            public bool Privelege;
            [JsonProperty("Подбор переработчиков с РТ")]
            public bool Radtown;
            [JsonProperty("Установка на землю")]
            public bool Ground;
            [JsonProperty("Установка на строения")]
            public bool Structure;
            [JsonProperty("Логи выдачи")]
            public bool lGet;
            [JsonProperty("Логи поднятий")]
            public bool lPickup;
            [JsonProperty("Логи установки")]
            public bool lDeploy;

            [JsonProperty("Версия конфигурации")]
            public VersionNumber PluginVersion = new VersionNumber();
            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig
                {
                    Price = new Dictionary<string, int>()
                    {
                        ["scrap"] = 1000,
                        ["metal.refined"] = 100,
                        ["gears"] = 25,
                        ["sheetmetal"] = 25,
                        ["metalspring"] = 15
                    },
                    Workbench = 2,
                    Time = 20f,
                    Chance = 50f,
                    Available = true,
                    Command = "/craftrec",
                    Owner = true,
                    Privelege = true,
                    Radtown = true,
                    Ground = true,
                    Structure = true,
                    lGet = true,
                    lPickup = true,
                    lDeploy = true,
                    PluginVersion = new VersionNumber(),
                };
            }
        }




        public int Amount = 1;
        private ItemBlueprint bp;
        private ItemDefinition def;
        public string Permission = "PortableRecycler.craft";

        Dictionary<string, string> Messages = new Dictionary<string, string>()
        {
            {"Nodef", "Не найдено определение предмета {0}. Он не будет добавлен к цене крафта." },
            {"Noingridient", "Недостаточно ингридиентов. На крафт нужно:\n{0}" },
            {"EnoughtIngridient", "{0} - <color=#53f442>{1}</color>/{2}" },
            {"NotEnoughtIngridient", "{0} - <color=#f44141>{1}</color>/{2}" },
            {"Workbench", "Необходим верстак уровня {0}" },
            {"NoPermission", "У вас нет необходимой привилегии" },
            {"CraftDenied", "К сожалению у Вас не получилось скрафтить переработчик. \nШанс на создание {0}%\nМожет быть повезет в следующий раз." },
            {"P.PRIVELEGE", "Вам нужно право на строительство чтобы подобрать переработчик" },
            {"P.AVAILABLE", "Подбор переработчиков выключен" },
            {"P.RADTOWN", "Подбор переработчиков с радтауна запрещен" },
            {"P.OWNER", "Переработчики может подобрать только их владельцы" },
            {"D.GROUND", "Переработчики нельзя ставить на землю" },
            {"D.STRUCTURE", "Переработчики нельзя ставить на строения (фундамент, потолки и тд)" },
            {"R.GIVED", "Вам был успешно выдан переработчик" },
            {"R.PICKUP", "Вы успешно подобрали переработчик" }
        };
        
        #endregion

        #region Oxide Hooks

        void OnServerInitialized()
        {
            Recyclers = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, ulong>>("PortableRecyclers");
            lang.RegisterMessages(Messages, this, "en");
            cmd.AddChatCommand(config.Command.Replace("/", string.Empty), this, CmdCraft);
            permission.RegisterPermission(Permission, this);
        }

        void Unload()
        {
            UnityEngine.Object.Destroy(bp);
        }

        void OnEntityBuilt(Planner planner, GameObject gameobject)
        {
            BaseEntity entity = gameobject.ToBaseEntity();
            BasePlayer player = planner.GetOwnerPlayer();
            if (player == null || entity == null) return;
            if (entity.skinID != 1311956341) return;
            Vector3 ePos = entity.transform.position;
            BaseEntity Recycler = GameManager.server.CreateEntity("assets/bundled/prefabs/static/recycler_static.prefab", ePos, entity.transform.rotation, true);
            RaycastHit rHit;
            if (Physics.Raycast(new Vector3(ePos.x, ePos.y + 1, ePos.z), Vector3.down, out rHit, 2f, LayerMask.GetMask(new string[] { "Construction" })) && rHit.GetEntity() != null)
            {
                if (!config.Structure)
                {
                    SendReply(player, Messages["D.STRUCTURE"]);
                    GiveRecycler(player);
                    return;
                }
                var newentity = rHit.GetEntity();
                if (!Recyclers.ContainsKey(newentity.net.ID.Value))
                    Recyclers.Add(newentity.net.ID.Value, 0);
            }
            else
            {
                if (!config.Ground)
                {
                    SendReply(player, Messages["D.GROUND"]);
                    GiveRecycler(player);
                    return;
                }
            }
            Recycler.OwnerID = player.userID;
            Recycler.Spawn();
            if (entity is BuildingBlock) Recyclers[entity.net.ID.Value] = Recycler.net.ID.Value;
            Interface.Oxide.DataFileSystem.WriteObject("PortableRecyclers", Recyclers);
            if (config.lDeploy) LogToFile("Deploy", $"Переработчик был установлен игроком {player.userID}", this);
            NextTick(() => entity.Kill());

        }

        void OnHammerHit(BasePlayer player, HitInfo info)
        {
            BaseEntity entity = info.HitEntity;
            if (entity == null) return;

            if (!entity.ShortPrefabName.Contains("recycler")) return;

            if (!config.Available)
            {
                SendReply(player, Messages["P.AVAILABLE"]);
                return;
            }

            if (!config.Radtown && entity.OwnerID == 0)
            {
                SendReply(player, Messages["P.RADTOWN"]);
                return;
            }

            if (config.Privelege && !player.CanBuild())
            {
                SendReply(player, Messages["P.PRIVELEGE"]);
                return;
            }

            if (config.Owner && entity.OwnerID != player.userID && entity.OwnerID != 0)
            {

                SendReply(player, Messages["P.OWNER"]);
                return;
            }

            entity.Kill();
            GiveRecycler(player, true);
            SendReply(player, Messages["R.PICKUP"]);
            if (config.lPickup) LogToFile("Pickup", $"Переработчик был подобран игроком {player.userID} | Владелец {entity.OwnerID}", this);
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (info == null || entity == null) return;

            if (entity is BuildingBlock && Recyclers.ContainsKey(entity.net.ID.Value)) DestroyRecycler(entity);
        }

        void OnEntityKill(BaseNetworkable entity)
        {
            if (entity == null) return;

            if (entity is BuildingBlock && Recyclers.ContainsKey(entity.net.ID.Value)) DestroyRecycler(entity);
        }

        object CanStackItem(Item item, Item targetItem)
        {
            if (item.info.shortname.Contains("bench"))
                if (item.skin != 0 || targetItem.skin != 0)
                    return false;

            return null;
        }

        object CanCombineDroppedItem(DroppedItem item, DroppedItem targetItem)
        {
            if (item.item.info.shortname.Contains("bench"))
                if (item.skinID != 0 || targetItem.skinID != 0)
                    return false;

            return null;
        }

        #endregion

        #region Main
        private void CmdCraft(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, Permission))
            {
                SendReply(player, Messages["NoPermission"]);
                return;
            }
            if (player.currentCraftLevel < config.Workbench)
            {
                SendReply(player, Messages["Workbench"], config.Workbench);
                return;
            }
            foreach (var ingredient in bp.ingredients)
            {
                var playeram = player.inventory.GetAmount(ingredient.itemDef.itemid);
                if (playeram >= ingredient.amount) continue;
                var reply = bp.ingredients.Select(x =>
                    GetMsg(player.inventory.GetAmount(x.itemDef.itemid) >= x.amount
                            ? "EnoughtIngridient"
                            : "NotEnoughtIngridient"
                        , player, x.itemDef.displayName.translated, player.inventory.GetAmount(x.itemDef.itemid),
                        x.amount)).ToArray();
                SendReply(player, Messages["Noingridient"], string.Join("\n", reply));
                return;
            }
            ItemCrafter itemCrafter = player.inventory.crafting;
            if (!itemCrafter.CanCraft(bp))
                return;
            ++itemCrafter.taskUID;
            List<Item> items = new List<Item>();
            foreach (var ingridient in bp.ingredients)
            {
                var amount = (int)ingridient.amount;
                foreach (var container in itemCrafter.containers)
                {
                    amount -= container.Take(items, ingridient.itemid, amount);
                    if (amount > 0)
                        continue;
                    break;
                }
            }
            if (UnityEngine.Random.Range(0f, 100f) < 10)
            {
                Effect.server.Run("assets/prefabs/deployable/research table/effects/research-success.prefab", player, 2, Vector3.zero, Vector3.forward);
                GiveRecycler(player);
            }
            else
            {
                Effect.server.Run("assets/prefabs/deployable/research table/effects/research-fail.prefab", player, 0, Vector3.zero, Vector3.forward);
                SendReply(player, Messages["CraftDenied"], config.Chance);
                return;
            }
        }

        private string GetMsg(string langkey, BasePlayer player, params object[] args)
        {
            string msg = lang.GetMessage(langkey, this, player?.UserIDString);
            if (args.Length > 0)
                msg = string.Format(msg, args);
            return msg;
        }


        [ConsoleCommand("recycler.add")]
        private void AddRecycler(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin)
            {
                SendError(arg, "[Ошибка] У вас нет доступа к этой команде!");
                return;
            }

            if (!arg.HasArgs())
            {
                PrintError(":\n[Ошибка] Введите recycler.add steamid/nickname\n[Пример] recyler.add Lomarine\n[Пример] recyler.add 76560000000000001");
                return;
            }

            BasePlayer player = BasePlayer.Find(arg.Args[0]);
            if (player == null)
            {
                PrintError($"[Ошибка] Не удается найти игрока {arg.Args[0]}");
                return;
            }

            GiveRecycler(player);
        }

        private void GiveRecycler(BasePlayer player, bool enabled = false)
        {
            Item rec = ItemManager.CreateByName("workbench3", 1, 1311956341);
            rec.MoveToContainer(player.inventory.containerMain);
            if (!enabled)
                SendReply(player, Messages["R.GIVED"]);
            if (config.lGet) LogToFile("Get", $"Переработчик был выдан игроку {player.userID}", this);
        }

        private void DestroyRecycler(BaseNetworkable entity)
        {
            BaseNetworkable rEntity = BaseNetworkable.serverEntities.Find(new NetworkableId(Recyclers[entity.net.ID.Value]));
            if (rEntity != null) rEntity.Kill();
            Recyclers.Remove(entity.net.ID.Value);
            Interface.Oxide.DataFileSystem.WriteObject("PortableRecyclers", Recyclers);
        }
        #endregion
    }
}