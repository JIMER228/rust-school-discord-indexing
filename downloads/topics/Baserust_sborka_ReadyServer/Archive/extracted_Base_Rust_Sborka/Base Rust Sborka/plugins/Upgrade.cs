using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Upgrade", "thouxanbanfauni", "1.0.1")]
    public class Upgrade : RustPlugin
    {
        #region Vars
        
		[PluginReference] Plugin NoEscape;
		bool useNoEscape = true;
		
        private static Upgrade plugin;
        
        private const string permUse = "upgrade.use";
        private const string permFree = "upgrade.free";
        private const string elem = "Upgrade.Text";

        #endregion
        
        #region Oxide Hooks

        private void Init()
        {
            permission.RegisterPermission(permUse, this);
            permission.RegisterPermission(permFree, this);
            
            lang.RegisterMessages(EN, this);
            
            plugin = this;
            
            foreach (var command in config.commands)
            {
                cmd.AddChatCommand(command, this, "Command");
                cmd.AddConsoleCommand(command, this, "Command");
            }
        }

        private void Unload()
        {
            foreach (var script in UnityEngine.Object.FindObjectsOfType<Upgrader>().ToList())
            {
                script.Kill();
            }
        }

        private void OnHammerHit(BasePlayer player, HitInfo info)
        {
			if (player == null || info == null) return;
            var entity = info?.HitEntity;
            if (entity == null) return;
			var component = player.GetComponent<Upgrader>();
			if (component == null) return;
            component.Upgrade(entity);
        }
        
        private void OnEntityBuilt(Planner plan, GameObject go)
        {
            var entity = go?.ToBaseEntity();
            var player = plan?.GetOwnerPlayer();
            if (entity == null || player == null) {return;}
			var component = player.GetComponent<Upgrader>();
			if (component == null) return;
            component.Upgrade(entity);
        }

        #endregion
        
        #region Localization

        private Dictionary<string, string> EN = new Dictionary<string, string>
        {
            {"Permission", "You don't have permission to use that!"},
            {"Cost", "У вас не хватает ресурсов для апгрейда!"},
            {"Enabled", "Апгрейд включен!"},
            {"Disabled", "Апгрейд был выключен!"},
            {"Text", "Режим улучшения строения до <color=green>{0}</color> выключится через {1} секунд"},
            {"1", "Дерева"},
            {"2", "Камня"},
            {"3", "Металла"},
            {"4", "МВК"}
        };

        private void message(BasePlayer player, string key, params object[] args)
        {
            var message = string.Format(lang.GetMessage(key, this, player.UserIDString), args);
            player.ChatMessage(message);
        }

        #endregion
        
        #region Configuration
        
        private static ConfigData config;
        
        private class ConfigData
        {
            [JsonProperty(PropertyName = "Команды")]
            public List<string> commands;

            [JsonProperty(PropertyName = "1. Проверка прав на строительство")]
            public bool checkPrivilege;

            [JsonProperty(PropertyName = "2. Проверка на полное здоровье")]
            public bool checkHealth;
            
            [JsonProperty(PropertyName = "3. Время с последнего использования")]
            public int attackTime;

            [JsonProperty(PropertyName = "GUI-настройки:")]
            public GUISettings settings;
        }

        private class GUISettings
        {
            [JsonProperty(PropertyName = "Продолжительно")]
            public int disableTime;
            
            [JsonProperty(PropertyName = "Цвет панели")]
            public string panelColor;

            [JsonProperty(PropertyName = "Цвет текста")]
            public string textColor;

            [JsonProperty(PropertyName = "Размер текста")]
            public int textSize;

            [JsonProperty(PropertyName = "Anchor min")]
            public string anchorMin;
            
            [JsonProperty(PropertyName = "Anchor max")]
            public string anchorMax;
        }
        
        private ConfigData GetDefaultConfig() 
        {
            return new ConfigData 
            {
                checkPrivilege = false,
                checkHealth = false,
                attackTime = 40,
                settings = new GUISettings
                {
                    disableTime = 40,
                    textSize = 13,
                    textColor = "1 1 0.9",
                    panelColor = "0.0 0.0 0.0 0",
                    anchorMin = "0 0 0 0 1",
                    anchorMax = "0.987 0.24"
                },
                commands = new List<string>
                {
                    "up",
                    "upgrade",
                    "grade",
                    "bgrade",
                }
            };
        }
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
   
            try
            {
                config = Config.ReadObject<ConfigData>();
        
                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            PrintError("Configuration file is corrupt(or not exists), creating new one!");
            config = GetDefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }
        
        #endregion

        #region Commands

        private void Command(BasePlayer player, string command, string[] args)
        {
					if (useNoEscape)
                    {
                        if (plugins.Exists("NoEscape"))
                        {
                            var isRaid = (bool)NoEscape?.Call("IsRaidBlock", player.userID);
                            if (isRaid)
                            {
                                SendReply(player, "<color=#CF1E1E>•</color> Ошибка:\nВы не можете использовать улучшение построек во время рейда");
                                return;
                            }
                        }
                    }
            Command(player, args);
        }

        private void Command(ConsoleSystem.Arg arg)
        {
					if (useNoEscape)
                    {
                        if (plugins.Exists("NoEscape"))
                        {
                            var isRaid = (bool)NoEscape?.Call("IsRaidBlock", arg.Player().userID);
                            if (isRaid)
                            {
                                SendReply(arg.Player(), "<color=#CF1E1E>•</color> Ошибка:\nВы не можете использовать улучшение построек во время рейда");
                                return;
                            }
                        }
                    }
			
            Command(arg.Player(), arg.Args);
        }

        private void Command(BasePlayer player, string[] args)
        {
            if (player == null) {return;}

            if (!permission.UserHasPermission(player.UserIDString, permUse))
            {
                message(player, "Permission");
                return;
            }

					if (useNoEscape)
                    {
                        if (plugins.Exists("NoEscape"))
                        {
                            var isRaid = (bool)NoEscape?.Call("IsRaidBlock", player.userID);
                            if (isRaid)
                            {
                                SendReply(player, "<color=#CF1E1E>•</color> Ошибка:\nВы не можете использовать улучшение построек во время рейда");
                                return;
                            }
                        }
                    }

            var script = player.GetComponent<Upgrader>();
            if (script == null)
            {
                script = player.gameObject.AddComponent<Upgrader>();
            }

            try
            {
                script.SetGrade(Convert.ToInt32(args[0]));
            }
            catch
            {
                script.MoveGrade();
            }
        }

        #endregion

        #region Helpers

        private string GetGrade(int grade, string id)
        {
            return lang.GetMessage(grade.ToString(), this, id);
        }

        #endregion

        #region Script

        private class Upgrader : MonoBehaviour
        {
            private BasePlayer player;
            private BuildingGrade.Enum gradeEnum;
            private int gradeInt;
            private string gradeString;
            private bool freeUpgrades;
            private string message;
            private int timeLeft = config.settings.disableTime;
            private GUISettings settings = config.settings;

            #region Hooks

            private void Awake()
            {
                player = GetComponent<BasePlayer>();
                freeUpgrades = plugin.permission.UserHasPermission(player.UserIDString, permFree);
                message = plugin.lang.GetMessage("Text", plugin, player.UserIDString);
            }

            private void Start()
            {
                CreateGUI();
                plugin.message(player, "Enabled");
            }

            private void OnDestroy()
            {
                CuiHelper.DestroyUi(player, elem);
                plugin.message(player, "Disabled");
            }

            #endregion

            #region Public

            public void Kill()
            {
                Destroy(this);
            }

            public void MoveGrade()
            {
                SetGrade(gradeInt + 1);
            }

            public void SetGrade(int value)
            {
                if (value == gradeInt || value < 1 || value > 4)
                {
                    Kill();
                    return;
                }
                
                gradeEnum = (BuildingGrade.Enum) value;
                gradeInt = value;
                gradeString = plugin.GetGrade(gradeInt, player.UserIDString);
                Refresh();
            }

            public void Upgrade(BaseEntity entity)
            {
				if (entity == null) return;
                var block = entity?.GetComponent<BuildingBlock>();
                if (block == null) return;
                Upgrade(block);
            }

            #endregion

            #region GUI

            private void CreateGUI()
            {
                CancelInvoke("CreateGUI");
                
                if (timeLeft < 1)
                {
                    Kill();
                    return;
                }
                
                var container = new CuiElementContainer
                {
                    new CuiElement
                    {
                        Name = elem,
                        Components =
                        {
                            new CuiImageComponent
                            {
                                Color = settings.panelColor
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = settings.anchorMin, 
                                AnchorMax = settings.anchorMax, 
                            }
                        }
                    },
                    new CuiElement
                    {
                        Parent = elem,
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Color = settings.textColor,
                                Text = string.Format(message, gradeString, timeLeft),
                                FontSize = settings.textSize,
                                Align = TextAnchor.MiddleCenter
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0", 
                                AnchorMax = "1 1"
                            }
                        }
                    }
                };

                CuiHelper.DestroyUi(player, elem);
                CuiHelper.AddUi(player, container);
                timeLeft--;
                
                Invoke("CreateGUI", 1);
            }

            #endregion

            private static bool UpgradeBlocked(BuildingBlock block)
            {
                if (!block.blockDefinition.checkVolumeOnUpgrade) return false;
                return DeployVolume.Check(block.transform.position, block.transform.rotation, PrefabAttribute.server.FindAll<DeployVolume>(block.prefabID), ~(1 << block.gameObject.layer));
            }

            #region Core

            private void Upgrade(BuildingBlock block)
            {
				if (block == null) return;
				
                if (Interface.Oxide.CallHook("OnStructureUpgrade", block, player, gradeEnum) != null)
                {
                    Destroy(this);
                    return;
                }

                Refresh();

                if (UpgradeBlocked(block)) return;
                if (block.grade >= gradeEnum) return;
                if (block.SecondsSinceAttacked < config.attackTime) return;
                if (config.checkPrivilege && !player.CanBuild()) return;
                
                if (config.checkHealth && Math.Abs(block.Health() - block.MaxHealth()) > 1) return;

                try
                {
                    var resources = block.blockDefinition.grades[gradeInt].costToBuild;
                    if (!TakeResources(resources))
                    {
                        return;
                    }
                }
                catch
                {
                    return;
                }

                block.SetGrade(gradeEnum);
                block.SetHealthToMax();
                block.StartBeingRotatable();
                block.SendNetworkUpdate();
                block.UpdateSkin();
                block.ResetUpkeepTime();
                Effect.server.Run("assets/bundled/prefabs/fx/build/promote_" + gradeEnum.ToString().ToLower() + ".prefab", block.transform.position);
            }

            private bool TakeResources(List<ItemAmount> items)
            {
                if (freeUpgrades)
                {
                    return true;
                }
                
                foreach (var item in items)
                {
                    if (player.inventory.GetAmount(item.itemid) < item.amount)
                    {
                        plugin.message(player, "Cost");
                        return false;
                    }
                }
                
                foreach (var item in items)
                {
                    player.inventory.Take(null, item.itemid, Convert.ToInt32(item.amount));
                    player.Command("note.inv", item.itemid, -item.amount);
                }
                
                return true;
            }
            
            private void Refresh()
            {
                CancelInvoke("Kill");
                Invoke("Kill", settings.disableTime);
                timeLeft = settings.disableTime;
                CreateGUI();
            }

            #endregion
        }

        #endregion
    }
}