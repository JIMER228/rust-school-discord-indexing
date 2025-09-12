using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Oxide.Core.Plugins;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using ProtoBuf;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("TPSkillSystem", "/https://blackplugin.ru/", "11.0.1")]
    public class TPSkillSystem : RustPlugin
    {
        [PluginReference] Plugin ImageLibrary, ArenaTournament;
        private static ConfigData configData;
        private class ConfigData
        {
            [JsonProperty("ИСПОЛЬЗОВАТЬ ПЛАГИН ДЛЯ НАСТРОЙКИ СКОРОСТИ ПЕЧЕК (НЕ ИСПОЛЬЗОВАТЬ - 0, ДРУГОЙ - 1)")]
            public int pluginfurnace;
            [JsonProperty("ОСНОВНЫЕ НАСТРОЙКИ")]
            public BasicSetting OptionsBasic = new BasicSetting();
            [JsonProperty("НАСТРОЙКИ ПОЛУЧЕНИЯ ОПЫТА")]
            public ActionPoints ActionSettings = new ActionPoints();
            [JsonProperty("НАСТРОЙКИ ОПЫТА И ОПИСАНИЕ")]
            public Dictionary<string, Skill> SkillList = new Dictionary<string, Skill>();

            internal class BasicSetting 
            {
                [JsonProperty("Текст в шапке")]
                public string Descript = "Название сервера";
                [JsonProperty("Стартовое количество очков у игрока")]
                public int BasePoints = 100;
                [JsonProperty("Количество выдаваемого опыта каждую минуту")]
                public float TimePoints = 0.0f;
            }

            internal class ActionPoints
            {
                [JsonProperty("Количество выдаваемого опыта за добычу ресурсов отбойным молотком")]
                public float GatherHitChainsaw = 1.0f;
                [JsonProperty("Количество выдаваемого опыта за добычу ресурсов бензопилой")]
                public float GatherHitJackhammer = 1.0f;
                [JsonProperty("Количество выдаваемого опыта за попадание по вертолёту")]
                public float HeliDamage = 1.0f;
                [JsonProperty("Количество выдаваемого опыта за улучшение построек")]
                public float Upgrade = 1.0f;
                [JsonProperty("Количество выдаваемого опыта за попадание по танку")]
                public float TankDamage = 1.0f;
                [JsonProperty("Количество выдаваемого опыта за убийство животных")]
                public float AnimalKill = 1.0f;
                [JsonProperty("Количество выдаваемого опыта за поднятие предмета")]
                public float PickUp = 1.0f;
                [JsonProperty("Количество выдаваемого опыта за добычу ресурсов")]
                public float GatherHit = 1.0f;
                [JsonProperty("Количество выдаваемого опыта за убийство игрока")]
                public float PlayerKill = 1.0f;
                [JsonProperty("Количество выдаваемого опыта за разбитие бочек")]
                public float BarrelKill = 1.0f;
                [JsonProperty("Количество выдаваемого опыта за убийство NPC")]
                public float NPCKill = 1.0f;
            }

            internal class Skill
            {
                [JsonProperty("Отображаемое название опыта")]
                public string DisplayName;
                [JsonProperty("Описание опыта")]
                public string Description;
                [JsonProperty("Рейт опыта при повышении уровня |Каждая строка означает новый уровень| |Значение означает процент от урона, плавки и тд|")]
                public List<float> Increase = new List<float>();
            }

            public float GetIncreaseOf(string name, int level)
            {
                if (SkillList.ContainsKey(name))
                {
                    if (SkillList[name].Increase.Count >= level)
                        return SkillList[name].Increase.ElementAt(level);
                }
                Interface.Oxide.LogWarning($"Try to get {level}LVL of {name} that is not exists!");
                return 0f;
            }

            public ConfigData()
            {
                ActionSettings = new ActionPoints();
                OptionsBasic = new BasicSetting();
                pluginfurnace=0;
                SkillList = new Dictionary<string, Skill>
                {
                    ["ШАХТЁР"] = new Skill
                    {
                        DisplayName = "ШАХТЁР",
                        Description = "С каждым уровнем увеличивается количество добываемых ресурсов киркой, чем выше уровень, \nтем больше вы будете добывать ресурсов.",
                        Increase = new List<float>
                        {
                            1.0f, 1.05f, 1.1f, 1.15f, 1.2f, 1.25f, 1.3f, 1.35f, 1.4f, 1.45f, 1.5f, 1.55f, 1.6f, 1.65f, 1.7f,
                            1.75f, 1.8f, 1.85f, 1.9f, 1.95f, 2.0f, 2.05f, 2.1f, 2.15f, 2.2f, 2.25f, 2.3f, 2.35f, 2.4f, 2.45f,
                            2.5f, 2.55f, 2.6f, 2.65f, 2.7f, 2.75f, 2.8f, 2.85f, 2.9f, 2.95f, 3.0f, 3.05f, 3.1f, 3.15f, 3.2f,
                            3.25f, 3.3f, 3.35f, 3.4f, 3.45f, 3.5f, 3.55f, 3.6f, 3.65f, 3.7f, 3.75f, 3.8f, 3.85f, 3.9f, 3.95f,
                            4.0f, 4.05f, 4.1f, 4.15f, 4.2f, 4.25f, 4.3f, 4.35f, 4.4f, 4.45f, 4.5f, 4.55f, 4.6f, 4.65f, 4.7f,
                            4.75f, 4.8f, 4.85f, 4.9f, 4.95f, 5.0f
                        }
                    },
                    ["ДРОВОСЕК"] = new Skill
                    {
                        DisplayName = "ДРОВОСЕК",
                        Description = "Каждый уровень увеличивает количество добываемого дерева, \nчем выше ваш уровень, тем больше вы будете добывать дерева.",
                        Increase = new List<float>
                        {
                            1.0f, 1.05f, 1.1f, 1.15f, 1.2f, 1.25f, 1.3f, 1.35f, 1.4f, 1.45f, 1.5f, 1.55f, 1.6f, 1.65f, 1.7f,
                            1.75f, 1.8f, 1.85f, 1.9f, 1.95f, 2.0f, 2.05f, 2.1f, 2.15f, 2.2f, 2.25f, 2.3f, 2.35f, 2.4f, 2.45f,
                            2.5f, 2.55f, 2.6f, 2.65f, 2.7f, 2.75f, 2.8f, 2.85f, 2.9f, 2.95f, 3.0f, 3.05f, 3.1f, 3.15f, 3.2f,
                            3.25f, 3.3f, 3.35f, 3.4f, 3.45f, 3.5f, 3.55f, 3.6f, 3.65f, 3.7f, 3.75f, 3.8f, 3.85f, 3.9f, 3.95f,
                            4.0f, 4.05f, 4.1f, 4.15f, 4.2f, 4.25f, 4.3f, 4.35f, 4.4f, 4.45f, 4.5f, 4.55f, 4.6f, 4.65f, 4.7f,
                            4.75f, 4.8f, 4.85f, 4.9f, 4.95f, 5.0f
                        }
                    },
                    ["ПЕЧНИК"] = new Skill
                    {
                        DisplayName = "ПЕЧНИК",
                        Description = "Скаждым уровнем увеличивается скорость плавки в печах, \nчем выше ваш уровень, тем быстрее работает печь.",
                        Increase = new List<float>
                        {
                            1.0f, 1.05f, 1.1f, 1.15f, 1.2f, 1.25f, 1.3f, 1.35f, 1.4f, 1.45f, 1.5f, 1.55f, 1.6f, 1.65f, 1.7f,
                            1.75f, 1.8f, 1.85f, 1.9f, 1.95f, 2.0f, 2.05f, 2.1f, 2.15f, 2.2f, 2.25f, 2.3f, 2.35f, 2.4f, 2.45f,
                            2.5f, 2.55f, 2.6f, 2.65f, 2.7f, 2.75f, 2.8f, 2.85f, 2.9f, 2.95f, 3.0f, 3.05f, 3.1f, 3.15f, 3.2f,
                            3.25f, 3.3f, 3.35f, 3.4f, 3.45f, 3.5f, 3.55f, 3.6f, 3.65f, 3.7f, 3.75f, 3.8f, 3.85f, 3.9f, 3.95f,
                            4.0f, 4.05f, 4.1f, 4.15f, 4.2f, 4.25f, 4.3f, 4.35f, 4.4f, 4.45f, 4.5f, 4.55f, 4.6f, 4.65f, 4.7f,
                            4.75f, 4.8f, 4.85f, 4.9f, 4.95f, 5.0f
                        }
                    },
                    ["ФЕРМЕР"] = new Skill
                    {
                        DisplayName = "ФЕРМЕР",
                        Description = "С каждым следующем уровнем вы будете получать больше ресурсов \nпри подборе их с земли.",
                        Increase = new List<float>
                        {
                            1.0f, 1.05f, 1.1f, 1.15f, 1.2f, 1.25f, 1.3f, 1.35f, 1.4f, 1.45f, 1.5f, 1.55f, 1.6f, 1.65f, 1.7f,
                            1.75f, 1.8f, 1.85f, 1.9f, 1.95f, 2.0f, 2.05f, 2.1f, 2.15f, 2.2f, 2.25f, 2.3f, 2.35f, 2.4f, 2.45f,
                            2.5f, 2.55f, 2.6f, 2.65f, 2.7f, 2.75f, 2.8f, 2.85f, 2.9f, 2.95f, 3.0f, 3.05f, 3.1f, 3.15f, 3.2f,
                            3.25f, 3.3f, 3.35f, 3.4f, 3.45f, 3.5f, 3.55f, 3.6f, 3.65f, 3.7f, 3.75f, 3.8f, 3.85f, 3.9f, 3.95f,
                            4.0f, 4.05f, 4.1f, 4.15f, 4.2f, 4.25f, 4.3f, 4.35f, 4.4f, 4.45f, 4.5f, 4.55f, 4.6f, 4.65f, 4.7f,
                            4.75f, 4.8f, 4.85f, 4.9f, 4.95f, 5.0f
                        }
                    },
                    ["СТРЕЛОК"] = new Skill
                    {
                        DisplayName = "СТРЕЛОК",
                        Description = "Каждый новый уровень вам даст способность наносить \nбольше урона игрокам при перестрелке.",
                        Increase = new List<float>
                        {
                            1.0f, 1.03f, 1.06f, 1.09f, 1.12f, 1.15f,
                            1.18f, 1.21f, 1.24f, 1.27f, 1.3f, 1.33f,
                            1.36f, 1.39f, 1.42f, 1.45f, 1.48f, 1.51f,
                            1.54f, 1.57f, 1.6f
                        }
                    },
                    ["СИЛА"] = new Skill
                    {
                        DisplayName = "СИЛА",
                        Description = "С каждым новым уровнем вы будете получать меньше \nурона который вам наносят игроки и НПС.",
                        Increase = new List<float>
                        {
                            0.0f, 0.01f, 0.02f, 0.03f, 0.04f, 0.05f, 0.06f, 0.07f, 0.08f, 0.09f, 0.1f,
                            0.11f, 0.12f, 0.13f, 0.14f, 0.15f, 0.16f, 0.17f, 0.18f, 0.19f, 0.25f
                        }
                    },
                    ["МЕТАБОЛИЗМ"] = new Skill
                    {
                        DisplayName = "МЕТАБОЛИЗМ",
                        Description = "Улучшая эту способность вы получаете \nвозможность просыпаться с полным здоровьем.",
                        Increase = new List<float>
                        {
                            0.0f, 0.03f, 0.06f, 0.09f, 0.12f, 0.15f, 0.18f, 0.21f, 0.24f, 0.27f, 0.3f,
                            0.33f, 0.36f, 0.39f, 0.42f, 0.45f, 0.48f, 0.51f, 0.54f, 0.57f, 0.6f
                        }
                    },
                    ["ВАМПИРИЗМ"] = new Skill
                    {
                        DisplayName = "ВАМПИРИЗМ",
                        Description = "Каждый новый уровень вам даст способность регенирировать \nсвое здоровье при нанесении урона игрокам.",
                        Increase = new List<float>
                        {
                            0.0f, 1.0f, 1.5f, 2.0f, 2.5f, 3.0f, 3.5f, 4.0f, 4.5f, 5.0f, 5.5f,
                            6.0f, 6.5f, 7.0f, 7.5f, 8.0f, 8.5f, 9.0f, 9.5f, 10.0f
                        }
                    },
                };
            }
        }

     #region Variables

        private static Dictionary<ulong, PlayerInfo> PlayerInfos = new Dictionary<ulong, PlayerInfo>();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                configData = Config.ReadObject<ConfigData>();
                if (configData?.SkillList == null) LoadDefaultConfig();
            }
            catch
            {
                PrintWarning($"Ошибка чтения конфигурации 'oxide/configData/{Name}', создаём новую конфигурацию!!");
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }
        protected override void LoadDefaultConfig() => configData = new ConfigData();
        protected override void SaveConfig() => Config.WriteObject(configData);

     #endregion
     
     #region Classes

        private class PlayerInfo
        {
            internal class SkillInfo
            {
                public Dictionary<string, int> Skills = new Dictionary<string, int>();
                public float GetCurrentIncrease(string name) => configData.GetIncreaseOf(name, Skills[name]);
            }

            public float CurrentPoints;
            public SkillInfo SkillsInfo = new SkillInfo();

            public void AddPoints(BasePlayer player, float increase)
            {
                CurrentPoints += increase;
                if (player != null)
                {
                    //UI_DrawCurrentInfo(player, Math.Floor(CurrentPoints) > Math.Floor(CurrentPoints - increase));
                }
            }

            public bool AddLevel(BasePlayer player, string name)
            {
                if (CurrentPoints < 1)
                {
                    ins.UI_DrawResearch(player, "НЕ ХВАТАЕТ ОЧКОВ");
                    return false;
                }

                if (SkillsInfo.Skills[name] + 1 >= configData.SkillList[name].Increase.Count)
                {
                    ins.UI_DrawResearch(player, "НАВЫК МАКСИМАЛЬНОГО УРОВНЯ");
                    return false;
                }

                SkillsInfo.Skills[name]++;
                ins.UI_DrawResearch(player, $"НАВЫК {name} УЛУЧШЕН");
                CurrentPoints--;
                return true;
            }
        }

     #endregion
     #region Initialization

        static TPSkillSystem ins;
        private void OnServerInitialized()
        {
            ins = this;
            if (Interface.Oxide.DataFileSystem.ExistsDatafile("PluginDataBase/TPSkillSystem/TPSkillSystem_Player"))
                PlayerInfos = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerInfo>>("PluginDataBase/TPSkillSystem/TPSkillSystem_Player");

            InitFileManager();

            foreach (var check in BasePlayer.activePlayerList)
                OnPlayerConnected(check);

            if (configData.OptionsBasic.TimePoints > 0)
            {
                timer.Every(60, () =>
                {
                    foreach (var basePlayer in BasePlayer.activePlayerList.Where(p => !p.IsReceivingSnapshot))
                    {
                        PlayerInfos[basePlayer.userID].AddPoints(basePlayer, configData.OptionsBasic.TimePoints);
                    }
                });
            }

            ImageLibrary.Call("AddImage", "https://i.ibb.co/RzqVZz0/BeBP63b.png", "fonDescription");
            ImageLibrary.Call("AddImage", "https://gspics.org/images/2024/07/02/0zCuXh.png", "menu");
        }

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject("PluginDataBase/TPSkillSystem/TPSkillSystem_Player", PlayerInfos);
        void OnServerSave() => SaveData();

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, MenuContent);
                CuiHelper.DestroyUi(player, "GUI_TPSkillSystem_Lvl");
                CuiHelper.DestroyUi(player, "GUI_TPSkillSystem_Menu");
                CuiHelper.DestroyUi(player, "GUI_TPSkillSystem_Lvl" + ".CE");
                CuiHelper.DestroyUi(player, "GUI_TPSkillSystem_Lvl" + ".CL");
            }
            SaveData();
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player.IsReceivingSnapshot)
            {
                timer.In(1f, () => OnPlayerConnected(player));
                return;
            }

            if (!PlayerInfos.ContainsKey(player.userID))
            {
                PlayerInfos.Add(player.userID, new PlayerInfo { CurrentPoints = configData.OptionsBasic.BasePoints });
                PlayerInfos[player.userID].SkillsInfo.Skills = configData.SkillList.ToDictionary(p => p.Key, p => 0); // 1
            }

            CuiHelper.DestroyUi(player, LayerInfo);
            /*CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-235 16", OffsetMax = "-210 98" },
                Button = { Color = HexToRustFormat("#8E8E8E40"), Command = "chat.say /skill" },
                Text = { Text = "" }
            }, "Hud", LayerInfo);

            timer.Once(5f, () =>
            {
                CuiHelper.AddUi(player, container);
                UI_DrawCurrentInfo(player);
            });*/
        }

     #endregion
     #region Hooks
        private float GetFurnaceRate(BasePlayer player)
        {
            return PlayerInfos[player.userID].SkillsInfo.GetCurrentIncrease("ПЕЧНИК");
        }
        object OnOvenToggle(BaseOven oven, BasePlayer player)
        {
            if(configData.pluginfurnace != 0) return null;
            if (oven.HasFlag(BaseEntity.Flags.On)) return null;

            if (oven.ShortPrefabName.Contains("furnace"))
            {
                double rate = PlayerInfos[player.userID].SkillsInfo.GetCurrentIncrease("ПЕЧНИК");
                StartCooking(oven, oven.GetComponent<BaseEntity>(), rate);
                return false;
            }
            return null;
        }

        void StartCooking(BaseOven oven, BaseEntity entity, double ovenMultiplier)
        {
            if(!oven.ShortPrefabName.Contains("electric"))
            {
                if (FindBurnable(oven) == null)
                return;
            }
            
            oven.inventory.temperature = 1000f;
            oven.UpdateAttachmentTemperature();
            InvokeHandler.CancelInvoke(entity.GetComponent<MonoBehaviour>(), new Action(oven.Cook));
            InvokeHandler.InvokeRepeating(entity.GetComponent<MonoBehaviour>(), new Action(oven.Cook), (float)(1f / ovenMultiplier), (float)(1f / ovenMultiplier));
            entity.SetFlag(BaseEntity.Flags.On, true, false);
        }

        Item FindBurnable(BaseOven oven)
        {
            if (oven.inventory == null)
                    return null;
            foreach (Item current in oven.inventory.itemList)
            {
                ItemModBurnable component = current.info.GetComponent<ItemModBurnable>();
                if (component && (oven.fuelType == null || current.info == oven.fuelType))
                    return current;
            }
            return null;
        }

        List<LootContainer> handledContainers = new List<LootContainer>();
        private void CanAffordUpgrade(BasePlayer player, BuildingBlock block, BuildingGrade.Enum grade)
        {
            if (PlayerInfos.ContainsKey(player.userID))
            {
                PlayerInfos[player.userID].AddPoints(player, configData.ActionSettings.Upgrade);
            }
        }

        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            int aaa = 0;
           
                if (entity == null || info == null) return null;
                if (info.Initiator is BasePlayer && !info.Initiator.IsNpc)
                {
                    BasePlayer player = entity.GetComponent<BasePlayer>();
                    BasePlayer playerInitianotor = info.Initiator.GetComponent<BasePlayer>();
                    if(player == null) return null;
                    if(player == playerInitianotor) return null;
                    if (PlayerInfos.ContainsKey(player.userID))
                    {
                        if(playerInitianotor != null)
                            if((bool) ArenaTournament.Call("IsOnTournament", playerInitianotor.userID)) return null;

                        if((bool) ArenaTournament.Call("IsOnTournament", player.userID)) return null;

                        float strengthProtection = PlayerInfos[player.userID].SkillsInfo.GetCurrentIncrease("СИЛА");

                        if(!playerInitianotor.IsNpc)
                        {
                            float archerDamage = PlayerInfos[playerInitianotor.userID].SkillsInfo.GetCurrentIncrease("СТРЕЛОК");
                            info.damageTypes.ScaleAll((1 - strengthProtection/100) / (1 - archerDamage/100));
                            float increase = PlayerInfos[playerInitianotor.userID].SkillsInfo.GetCurrentIncrease("ВАМПИРИЗМ");
                            NextTick(() =>
                            {
                                var heal = info.damageTypes.Total() * (increase/100);
                                playerInitianotor.health += heal;
                            });
                        }
                        else
                        {
                            info.damageTypes.ScaleAll(1 - strengthProtection/100);
                        }
                        return null;
                    }
                    if (info.HitEntity.ShortPrefabName == "patrolhelicopter")
                    {
                        PlayerInfos[playerInitianotor.userID].AddPoints(playerInitianotor, configData.ActionSettings.HeliDamage);
                    }
                    if (entity is BradleyAPC)
                    {
                        PlayerInfos[playerInitianotor.userID].AddPoints(playerInitianotor, configData.ActionSettings.TankDamage);
                    }
                }
            
            return null;
        }

        private void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            if (entity != null && entity is BasePlayer)
            {
                BasePlayer player = entity as BasePlayer;
                if (PlayerInfos.ContainsKey(player.userID))
                {
                    float increase = 1f;

                    var activeitem = player.GetActiveItem();
                    if (activeitem != null && activeitem.info.shortname.Contains("jackhammer"))
                    {
                        PlayerInfos[player.userID].AddPoints(player, configData.ActionSettings.GatherHitJackhammer);
                        if (dispenser.gatherType == ResourceDispenser.GatherType.Tree)
                        {
                            increase = PlayerInfos[player.userID].SkillsInfo.GetCurrentIncrease("ДРОВОСЕК");
                        }
                        else if (dispenser.gatherType == ResourceDispenser.GatherType.Ore)
                        {
                            increase = PlayerInfos[player.userID].SkillsInfo.GetCurrentIncrease("ШАХТЁР");
                        }
                        item.amount = (int)Math.Floor(item.amount * increase);
                    }
                    if (activeitem != null && activeitem.info.shortname.Contains("chainsaw"))
                    {
                        PlayerInfos[player.userID].AddPoints(player, configData.ActionSettings.GatherHitChainsaw);
                        if (dispenser.gatherType == ResourceDispenser.GatherType.Tree)
                        {
                            increase = PlayerInfos[player.userID].SkillsInfo.GetCurrentIncrease("ДРОВОСЕК");
                        }
                        else if (dispenser.gatherType == ResourceDispenser.GatherType.Ore)
                        {
                            increase = PlayerInfos[player.userID].SkillsInfo.GetCurrentIncrease("ШАХТЁР");
                        }
                        item.amount = (int)Math.Floor(item.amount * increase);
                    }

                    PlayerInfos[player.userID].AddPoints(player, configData.ActionSettings.GatherHit);
                    if (dispenser.gatherType == ResourceDispenser.GatherType.Tree)
                    {
                        increase = PlayerInfos[player.userID].SkillsInfo.GetCurrentIncrease("ДРОВОСЕК");
                    }
                    else if (dispenser.gatherType == ResourceDispenser.GatherType.Ore)
                    {
                        increase = PlayerInfos[player.userID].SkillsInfo.GetCurrentIncrease("ШАХТЁР");
                    }
                    item.amount = (int)Math.Floor(item.amount * increase);
                }
            }
            return;
        }

        void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (PlayerInfos.ContainsKey(player.userID))
            {
                PlayerInfos[player.userID].AddPoints(player, configData.ActionSettings.GatherHit);

                float increase = 1f;
                if (dispenser.gatherType == ResourceDispenser.GatherType.Tree)
                {
                    increase = PlayerInfos[player.userID].SkillsInfo.GetCurrentIncrease("ДРОВОСЕК");
                }
                else if (dispenser.gatherType == ResourceDispenser.GatherType.Ore)
                {
                    increase = PlayerInfos[player.userID].SkillsInfo.GetCurrentIncrease("ШАХТЁР");
                }
                item.amount = (int)Math.Floor(item.amount * increase);
            }
            return;
        }   

        object OnCollectiblePickup(CollectibleEntity collectible, BasePlayer player)
        {
            var item = collectible.itemList[0];
            if(!Grows.Contains($"{collectible.itemList[0].itemDef.shortname}")) return null;
            PlayerInfos[player.userID].AddPoints(player, configData.ActionSettings.PickUp);
            float increase = PlayerInfos[player.userID].SkillsInfo.GetCurrentIncrease("ФЕРМЕР");
            item.amount = (int)Math.Floor(item.amount * increase);
            return null;
        }

        List<string> Grows = new List<string>
        {
            "black.berry",
            "blue.berry",
            "red.berry",
            "white.berry",
            "yellow.berry",
            "green.berry",
            "pumpkin",
            "corn",
            "cloth",
            "potato"
        };

        void OnQuarryGather(MiningQuarry quarry, Item item)
        {
            if (quarry.OwnerID == 0) return;
            int newAmount = (int)(item.amount * PlayerInfos[quarry.OwnerID].SkillsInfo.GetCurrentIncrease("ШАХТЁР"));
            item.amount = newAmount > 1 ? newAmount : 1;
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (info != null && info.Initiator is BasePlayer && !info.Initiator.IsNpc && info.Initiator.GetComponent<BasePlayer>().IsConnected)
            {
                BasePlayer initiator = info.Initiator as BasePlayer;
                if(info.InitiatorPlayer != null && info.InitiatorPlayer == entity)
                    return;
                if (entity is BasePlayer && !entity.IsNpc)
                    PlayerInfos[initiator.userID].AddPoints(initiator, configData.ActionSettings.PlayerKill);
                if (entity.GetComponent<BaseAnimalNPC>() != null)
                    PlayerInfos[initiator.userID].AddPoints(initiator, configData.ActionSettings.AnimalKill);
                if (entity.ShortPrefabName.Contains("barrel"))
                    PlayerInfos[initiator.userID].AddPoints(initiator, configData.ActionSettings.BarrelKill);
                if(entity.IsNpc)
                    PlayerInfos[initiator.userID].AddPoints(initiator, configData.ActionSettings.NPCKill);

            }
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            if (PlayerInfos.ContainsKey(player.userID))
            {
                float increase = PlayerInfos[player.userID].SkillsInfo.GetCurrentIncrease("МЕТАБОЛИЗМ");
                player.health += increase;
            }
            return;
        }

     #endregion
     #region Interface

        private const string LayerInfo = "GUI_TPSkillSystem_Lvl";
        private static void UI_DrawCurrentInfo(BasePlayer player, bool fadeIn = false)
        {
            PlayerInfo playerInfo = PlayerInfos[player.userID];
            CuiElementContainer container = new CuiElementContainer();
            float percents = (float)(playerInfo.CurrentPoints - Math.Floor(playerInfo.CurrentPoints));

            CuiHelper.DestroyUi(player, LayerInfo + ".CE");
            CuiHelper.DestroyUi(player, LayerInfo + ".CL");

            container.Add(new CuiElement
            {
                FadeOut = fadeIn ? 1f : 0f, Parent = LayerInfo, Name = LayerInfo + ".CE",
                Components =
                {
                    new CuiImageComponent { Color = HexToRustFormat("#A7330DE6") },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = $"1 {percents}", OffsetMin = "1 1", OffsetMax = "-1 -1" },
                }
            });
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0.458", AnchorMax = "1 0.645" },
                Text = { Text = $"{((percents) * 100).ToString("F0")}%", FontSize = 11, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.6" }
            }, LayerInfo, LayerInfo + ".CL");

            CuiHelper.AddUi(player, container);
        }

        public const string MenuContent = "TPSystem.Content";

        void UI_DrawResearch(BasePlayer player, string update = "")
        {
            CuiHelper.DestroyUi(player, MenuContent);
            CuiElementContainer container = new CuiElementContainer();
            PlayerInfo playerInfo = PlayerInfos[player.userID];

            
                container.Add(new CuiElement
                {
                    Name = MenuContent,
                    Parent = ".Mains",
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Color = "1 1 1 1",
                            Png = (string)ImageLibrary.Call("GetImage", "menu"),
                        },

                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-592 -333", OffsetMax = "581 335"
                        }
                    }
                });
     
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0.8 0.804", AnchorMax = "0.817 0.832" },
                    Button = { Color = "1 1 1 0", Close = "Menu_UI" },
                
                }, MenuContent);
                
                if (update.Length>0)
                {
                    CuiHelper.DestroyUi(player, "WarningMessUI");

                    container.Add(new CuiElement
                    {
                        Name = "WarningMessUI",
                        Parent = MenuContent,
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = update,
                                FontSize = 15,
                                Align = TextAnchor.MiddleCenter,
                                Color = "1 1 1 0.9",
                            },

                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.3 0.19",
                                AnchorMax = "0.7 0.25",
                            },
                        }
                    });

                    timer.Once(0.3f, () =>
                    {
                        CuiHelper.DestroyUi(player, "WarningMessUI");
                    });
                }
           
                container.Add(new CuiPanel
                {
                    Image =
                    {
                        Color = "1 1 1 0",

                    },

                    RectTransform =
                    {
                        AnchorMin = "0.415 0.276",
                        AnchorMax = "0.587 0.33",
                    },

                }, MenuContent, "Balance");

                container.Add(new CuiElement
                {
                    Parent = "Balance",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = "ОЧКИ ПРОКАЧКИ",
                            FontSize = 12,
                            Align = TextAnchor.MiddleCenter,
                            Color = "1 1 1 0.9",
                        },

                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "0.9 1",
                        },
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = "Balance",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = Math.Floor(playerInfo.CurrentPoints).ToString("F0"),
                            FontSize = 12,
                            Align = TextAnchor.MiddleCenter,
                            Color = "1 1 1 0.9",
                        },

                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.814 0",
                            AnchorMax = "1 1",
                        },
                    }
                });
             

                float x = 0f;
                float y = 0f;
                 
                foreach (var check in configData.SkillList)
                {
                    container.Add(new CuiPanel
                    {
                        Image =
                        {
                            Color = "1 1 1 0",

                        },

                        RectTransform =
                        {
                            AnchorMin = $"{0.232 + x} {0.623 - y}",
                            AnchorMax = $"{0.407 + x} {0.676 - y}",
                        },

                    }, MenuContent, MenuContent + check.Key);

                    

                    container.Add(new CuiElement
                    {
                        Parent = MenuContent + check.Key,
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = check.Value.DisplayName.ToUpper(),
                                FontSize = 12,
                                Align = TextAnchor.MiddleLeft,
                                Color = "1 1 1 0.9",
                            },

                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.28 0",
                                AnchorMax = "1 1",
                            },
                        }
                    });

                    container.Add(new CuiButton
                    {
                        Button =
                        {
                            Command = $"GUI_TPSkillSystem description {check.Key} {((x > 0) ? true : false)}",
                            Color = "1 1 1 0",
                        },

                        RectTransform =
                        {
                            AnchorMin = "0.03 0.15",
                            AnchorMax = "0.145 0.8",
                        }
                    }, MenuContent + check.Key);

                    container.Add(new CuiButton
                    {
                        Button =
                        {
                            Command = $"GUI_TPSkillSystem increase {check.Key}",
                            Color = "1 1 1 0",
                        },

                        RectTransform =
                        {
                            AnchorMin = "0.855 0.15",
                            AnchorMax = "0.97 0.81",
                        }
                    }, MenuContent + check.Key);

                    container.Add(new CuiElement
                    {
                        Parent = MenuContent + check.Key,
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = playerInfo.SkillsInfo.Skills[check.Key].ToString(),
                                FontSize = 12,
                                Align = TextAnchor.MiddleCenter,
                                Color = "1 1 1 0.9",
                            },

                            new CuiRectTransformComponent
                            {
                                AnchorMin = $"0.71 0.15",
                                AnchorMax = $"0.824 0.81",
                            },
                        }
                    });
                    y+=0.0805f;
                    if(y>0.3f)
                    {
                        y=0f;
                        x+=0.3627f;
                    }
                }
                CuiHelper.AddUi(player, container);
        }

        void InfoSkill(BasePlayer player, string name, ConfigData.Skill skill = null, bool RL=false)
        {
            var container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, MenuContent + ".Description");

            var AMin = RL ? "1.1 0" : "1 0";
            var AMax = RL ? "2.1 3" : "2 3";

            container.Add(new CuiElement
            {
                Name = MenuContent + ".Description",
                Parent = MenuContent + name,
                Components = {
                    new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", "fonDescription") },
                    new CuiRectTransformComponent { AnchorMin = AMin, AnchorMax = AMax },
                }

            });

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.91 0.83", AnchorMax = "0.98 0.96" },
                Button = { Close = MenuContent + ".Description", Color = "1 1 1 0" },
            }, MenuContent + ".Description");


            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.05 0.8", AnchorMax = "0.8 1"},
                Text = { Text = $"Описание скила {skill.DisplayName}", Color = "1 1 1 0.65",FontSize = 12, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleLeft }
            }, MenuContent + ".Description");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.1 0", AnchorMax = "0.9 0.7"},
                Text = { Text = $"{skill.Description}", Color = "1 1 1 0.65",FontSize = 12, Font = "robotocondensed-bold.ttf", Align = TextAnchor.UpperLeft }
            }, MenuContent + ".Description");


            CuiHelper.AddUi(player, container);
        }

     #endregion
     #region Commands

        [ConsoleCommand("system.points")]
        private void CmdAdminHandler(ConsoleSystem.Arg args)
        {
            if (args.Player() != null || !args.HasArgs(2))
                return;

            ulong targetID;
            int amount;
            if (ulong.TryParse(args.Args[0], out targetID))
            {
                BasePlayer target = BasePlayer.FindByID(targetID);
                if (int.TryParse(args.Args[1], out amount))
                {
                    if (PlayerInfos.ContainsKey(targetID))
                    {
                        PlayerInfos[targetID].AddPoints(target, amount);
                        PrintWarning($"Successful added {amount} to {targetID}");

                        if (target != null && target.IsConnected)
                            target.ChatMessage($"Вы получили {amount} очков,  потратить их в меню!");
                    }
                }
            }
        }
        [ConsoleCommand("GUI_TPSkillSystem")]
        private void CmdConsoleHandler(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            if (player != null && args.HasArgs(1))
            {
                switch (args.Args[0].ToLower())
                {
                    case "increase":
                    {
                        if (args.HasArgs(2) && configData.SkillList.ContainsKey(args.Args[1]))
                        {
                            PlayerInfos[player.userID].AddLevel(player, args.Args[1]);
                        }
                        break;
                    }
                    case "open":
                    {
                        UI_DrawResearch(player);
                        break;
                    }
                    case "description":
                    {
                        string codeName = args.Args[1];
                        InfoSkill(player, codeName, configData.SkillList[codeName], bool.Parse(args.Args[2]));
                        break;
                    }
                }
            }
        }

     #endregion
     #region Utils       

        private static string HexToRustFormat(string hex)
        {
            if (string.IsNullOrEmpty(hex))
            {
                hex = "#FFFFFFFF";
            }

            var str = hex.Trim('#');
            if (str.Length == 6)
                str += "FF";

            if (str.Length != 8)
            {
                throw new Exception(hex);
                throw new InvalidOperationException("Cannot convert a wrong format.");
            }

            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);
            var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);

            UnityEngine.Color color = new Color32(r, g, b, a);
            return string.Format("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a);
        }

     #endregion
     #region LoadImages

        bool init;
        private GameObject FileManagerObject;
        private FileManager m_FileManager;
        private string UrlImagesLeft;
        private string UrlImagesRight;

        void InitFileManager()
        {
            FileManagerObject = new GameObject("FileManagerObject");
            m_FileManager = FileManagerObject.AddComponent<FileManager>();
        }

        class FileManager : MonoBehaviour
        {
            int loaded = 0;
            int needed = 0;
            public bool IsFinished => needed == loaded;
            const ulong MaxActiveLoads = 10;
            Dictionary<string, FileInfo> files = new Dictionary<string, FileInfo>();

            private class FileInfo
            {
                public string Url;
                public string Png;
            }

            public string GetPng(string name)
            {
                if (files.ContainsKey(name))
                    return files[name].Png;
                return null;
            }

            public IEnumerator LoadFile(string name, string url, int size = -1)
            {
                if (files.ContainsKey(name) && files[name].Url == url && !string.IsNullOrEmpty(files[name].Png)) yield break;
                files[name] = new FileInfo() { Url = url };
                needed++;
                yield return StartCoroutine(LoadImageCoroutine(name, url, size));
            }
            IEnumerator LoadImageCoroutine(string name, string url, int size = -1)
            {
                using (WWW www = new WWW(url))
                {
                    yield return www;
                    if (string.IsNullOrEmpty(www.error))
                    {
                        var bytes = size == -1 ? www.bytes : Resize(www.bytes, size);
                        var entityId = CommunityEntity.ServerInstance.net.ID;
                        var crc32 = FileStorage.server.Store(bytes, FileStorage.Type.png, entityId).ToString();
                        files[name].Png = crc32;
                    }

                }
                loaded++;
                ins.init = true;
            }
            static byte[] Resize(byte[] bytes, int size)
            {
                Image img = (Bitmap)(new ImageConverter().ConvertFrom(bytes));
                Bitmap cutPiece = new Bitmap(size, size);
                System.Drawing.Graphics graphic = System.Drawing.Graphics.FromImage(cutPiece);
                graphic.DrawImage(img, new Rectangle(0, 0, size, size), 0, 0, img.Width, img.Height, GraphicsUnit.Pixel);
                graphic.Dispose();
                MemoryStream ms = new MemoryStream();
                cutPiece.Save(ms, ImageFormat.Jpeg);
                return ms.ToArray();
            }
        }
     #endregion
    }
}