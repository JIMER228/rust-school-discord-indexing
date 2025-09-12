using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Oxide.Plugins
{
    [Info("UraniumTools", "Mercury", "0.0.2")]
    [Description("Урановые инструменты,добывают сразу готовые ресурсы с возможностью увеличения выпадения,нанося урон радиацией")]
    class UraniumTools : RustPlugin
    {
        #region Configuration

        private static Configuration config = new Configuration();
        private class Configuration
        {
            [JsonProperty("Настройка урановых инструментов")]
            public List<Tools> UraniumTools = new List<Tools>();

            internal class Tools
            {
                [JsonProperty("Shortname инструмента")]
                public string Shortname;
                [JsonProperty("Название инструмента")]
                public string Name;
                [JsonProperty("SkinID инструмента")]
                public ulong SkinID;
                [JsonProperty("Мутация (При добыче будет перерабатывать ресурс) [true/false]")]
                public bool MutationUse;
                [JsonProperty("Радиация (При ударе инструментом будет добавлять радиацию игроку) [true/false]")]
                public bool RadiationUse;
                [JsonProperty("Умножение ресурсов (При ударе инструментом будет увеличивать добычу в Х раз) [true/false]")]
                public bool RateGatherUse;
                [JsonProperty("Радиация за удар инструментом")]
                public float Radiation;
                [JsonProperty("Увеличивать добычу в Х раз за удар")]
                public float RateGather;
            }

            public static Configuration GetNewConfiguration()
            {
                return new Configuration
                {
                    UraniumTools = new List<Tools>
                    {
                        new Tools
                        {
                            Shortname = "pickaxe",
                            Name = "Урановая кирка",
                            SkinID = 859006499,
                            MutationUse = true,
                            RadiationUse = true,
                            RateGatherUse = true,
                            Radiation = 3,
                            RateGather = 2
                        },
                        new Tools
                        {
                            Shortname = "hatchet",
                            Name = "Урановый топор",
                            SkinID = 860588662,
                            MutationUse = false,
                            RadiationUse = true,
                            RateGatherUse = true,
                            Radiation = 3,
                            RateGather = 5
                        },
                    }
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();
            }
            catch
            {
                PrintWarning("Ошибка #183" + $"чтения конфигурации 'oxide/config/{Name}', создаём новую конфигурацию!!");
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }

        protected override void LoadDefaultConfig() => config = Configuration.GetNewConfiguration();
        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion

        #region Hooks
        void OnServerInitialized()
        {
            MutationRegistered();
        }

        void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            Item weapon = entity.ToPlayer()?.GetActiveItem();
            if (weapon == null) return;
            UseTools(item, entity.ToPlayer(), weapon.info.shortname, weapon.skin);
        }
        void OnDispenserBonus(ResourceDispenser disp, BasePlayer player, Item item)
        {
            Item weapon = player?.GetActiveItem();
            if (weapon == null) return;
            UseTools(item, player, weapon.info.shortname, weapon.skin);
        }
        #endregion

        #region Commands
        [ConsoleCommand("ut_give")]
        void UraniumToolGive(ConsoleSystem.Arg args)
        {
            ulong SteamID = ulong.Parse(args.Args[0]);
            string Shortname = args.Args[1];
            BasePlayer player = BasePlayer.FindByID(SteamID);
            CreateItem(player, Shortname);
        }
        #endregion

        #region Metods

        #region Mutations
        private Dictionary<ItemDefinition, ItemDefinition> Transmutations;
        public List<string> MutationItemList = new List<string>
        {
            "chicken.raw",
            "humanmeat.raw",
            "bearmeat",
            "deermeat.raw",
            "meat.boar",
            "wolfmeat.raw",
            "hq.metal.ore",
            "metal.ore",
            "sulfur.ore"
        };
        void MutationRegistered()
        {
            Transmutations = ItemManager.GetItemDefinitions().Where(p => MutationItemList.Contains(p.shortname)).ToDictionary(p => p, p => p.GetComponent<ItemModCookable>()?.becomeOnCooked);
            ItemDefinition wood = ItemManager.FindItemDefinition(-151838493);
            ItemDefinition charcoal = ItemManager.FindItemDefinition(-1938052175);
            Transmutations.Add(wood, charcoal);
        }
        #endregion

        void UseTools(Item item,BasePlayer player, string Shortname, ulong SkinID)
        {
            for (int i = 0; i < config.UraniumTools.Count; i++)
            {
                var UraniumTool = config.UraniumTools[i];
                if (UraniumTool.SkinID == SkinID)
                {
                    if (UraniumTool.MutationUse && Transmutations.ContainsKey(item.info))
                        item.info = Transmutations[item.info];
                    if (UraniumTool.RadiationUse)
                        player.metabolism.radiation_poison.value += UraniumTool.Radiation;
                    if (UraniumTool.RateGatherUse)
                        item.amount = (int)(item.amount * UraniumTool.RateGather * 1);
                }
            }
        }

        void CreateItem(BasePlayer player,string Shortname)
        {
            var UraniumTool = config.UraniumTools.FirstOrDefault(x => x.Shortname == Shortname);
            Item item = ItemManager.CreateByName(Shortname, 1, UraniumTool.SkinID);
            item.name = UraniumTool.Name;
            player.GiveItem(item);
        }

        #endregion
    }
}
