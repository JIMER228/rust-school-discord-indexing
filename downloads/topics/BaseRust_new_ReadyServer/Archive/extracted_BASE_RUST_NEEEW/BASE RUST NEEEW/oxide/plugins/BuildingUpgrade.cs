using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Oxide.Plugins
{
    [Info("Building Upgrade", "OxideBro", "1.2.21")]
    class BuildingUpgrade : RustPlugin
    {
        [PluginReference] Plugin Remove, NoEscape, BuildingProtection;

        private void PayForUpgrade(ConstructionGrade g, BasePlayer player)
        {
            foreach (ItemAmount itemAmount in g.costToBuild)
            {
                var item = player.inventory.FindItemID(itemAmount.itemid);
                item.UseItem((int)itemAmount.amount);
                player.Command(string.Concat(new object[] {
                    "note.inv ", itemAmount.itemid, " ", itemAmount.amount * -1f
                }
                ), new object[0]);
            }
        }

        private ConstructionGrade GetGrade(BuildingBlock block, BuildingGrade.Enum iGrade)
        {
            if ((int)block.grade < (int)block.blockDefinition.grades.Length) return block.blockDefinition.grades[(int)iGrade];
            return block.blockDefinition.defaultGrade;
        }

        private bool CanAffordUpgrade(BuildingBlock block, BuildingGrade.Enum iGrade, BasePlayer player)
        {
            if (config.mainSettings.permissionAutoGradeAdmin && permission.UserHasPermission(player.UserIDString, config.mainSettings.permissionAutoGradeFree)) return true;
            foreach (var item in GetGrade(block, iGrade).costToBuild)
            {
                var invItem = player.inventory.FindItemID(item.itemid);
                if (invItem == null || invItem.amount < item.amount)
                    return false;
            }

            return true;
        }

        Dictionary<BuildingGrade.Enum, string> gradesString = new Dictionary<BuildingGrade.Enum, string>() {
                {
                BuildingGrade.Enum.Wood, "<color=orange>дерева</color>"
            }
            , {
                BuildingGrade.Enum.Stone, "<color=orange>камня</color>"
            }
            , {
                BuildingGrade.Enum.Metal, "<color=orange>метала</color>"
            }
            , {
                BuildingGrade.Enum.TopTier, "<color=orange>мвк</color>"
            }
        };

        Dictionary<BasePlayer, BuildingGrade.Enum> grades = new Dictionary<BasePlayer, BuildingGrade.Enum>();
        Dictionary<BasePlayer, int> timers = new Dictionary<BasePlayer, int>();



        private PluginConfig config;

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Благодарим за заказ плагина у разработчика OxideBro. Если вы передадите этот плагин сторонним лицам знайте - это лишает вас гарантированных обновлений!");
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
            if (config.PluginVersion < new VersionNumber(1, 2, 2))
            {
                PrintWarning("Config update detected! Updating config values...");
                config.commandSettings.ChatCMD = new List<string>()
                {
                    "up",
                    "bgrade",
                    "upgrade"
                };
                PrintWarning("Config update completed!");
            }
            config.PluginVersion = Version;
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }


        public class MainSettings
        {
            [JsonProperty("Через сколько секунд автоматически выключать улучшение строений")]
            public int resetTime = 40;
            [JsonProperty("Привилегия что бы позволить улучшать объекты при строительстве")]
            public string permissionAutoGrade = "buildingupgrade.build";
            [JsonProperty("Привилегия для улучшения при строительстве и ударе киянкой без траты ресурсов")]
            public string permissionAutoGradeFree = "buildingupgrade.free";
            [JsonProperty("Привилегия что бы позволить улучшать объекты ударом киянки")]
            public string permissionAutoGradeHammer = "buildingupgrade.hammer";
            [JsonProperty("Включить бесплатный Upgrade для администраторов?")]
            public bool permissionAutoGradeAdmin = true;
            [JsonProperty("Запретить Upgrade в Building Block?")]
            public bool getBuild = true;
            [JsonProperty("Включить доступ только по привилегиям?")]
            public bool permissionOn = true;
            [JsonProperty("Включить поддержку NoEscape (Запретить Upgrade в Raid Block)?")]
            public bool useNoEscape = true;
            [JsonProperty("Включить поддержку BuildingProtection (Запретить Upgrade в BuildingProtection)?")]
            public bool useBuildingProtection = false;
            [JsonProperty("Разрешить улучшать повреждённые постройки?")]
            public bool CanUpgradeDamaged = false;
            [JsonProperty("Включить выключение удаления построек при включении авто-улучшения (Поддержка плагина Remove с сайта RustPlugin.ru)")]
            public bool EnabledRemove = false;
            [JsonProperty("Включить переключение типов апгреда клавией E для игроков (При включенной функции может быть небольшая нагрузка из за хука)")]
            public bool EnabledInput = false;
        }

        public class MessagesSettings
        {
            [JsonProperty("No Permissions Hammer:")]
            public string MessageAutoGradePremHammer = "У вас нету доступа к улучшению киянкой!";
            [JsonProperty("No Permissions:")]
            public string MessageAutoGradePrem = "У вас нету доступа к данной команде!";
            [JsonProperty("No Resources:")]
            public string MessageAutoGradeNo = "<color=#ffcc00><size=16>Для улучшения нехватает ресурсов!!!</size></color>";
            [JsonProperty("Сообщение при включение Upgrade:")]
            public string MessageAutoGradeOn = "<size=14><color=#EC402C>Upgrade включен!</color> \nДля быстрого переключения используйте: <color=#EC402C>/upgrade 0-4</color></size>";
            [JsonProperty("Сообщение при выключение Upgrade:")]
            public string MessageAutoGradeOff = "<color=#ffcc00><size=14>Вы отключили <color=#EC402C>Upgrade!</color></size></color>";
        }

        public class GUISettings
        {
            [JsonProperty("Минимальный отступ:")]
            public string PanelAnchorMin = "0.0 0.908";
            [JsonProperty("Максимальный отступ:")]
            public string PanelAnchorMax = "1 0.958";
            [JsonProperty("Цвет фона:")]
            public string PanelColor = "0 0 0 0.50";
        }

        public class GUISettingsText
        {
            [JsonProperty("Размер текста в gui панели:")]
            public int TextFontSize = 16;
            [JsonProperty("Цвет текста в gui панели:")]
            public string TextСolor = "0 0 0 1";
            [JsonProperty("Минимальный отступ в gui панели:")]
            public string TextAnchorMin = "0.0 0.870";
            [JsonProperty("Максимальный отступ в gui панели:")]
            public string TextAnchorMax = "1 1";
        }


        public class InfoNotiseSettings
        {
            [JsonProperty("Включить GUI оповещение при использование плана постройки")]
            public bool InfoNotice = true;
            [JsonProperty("Размер текста GUI оповещения")]
            public int InfoNoticeSize = 18;
            [JsonProperty("Сообщение GUI")]
            public string InfoNoticeText = "Используйте <color=#EC402C>/upgrade</color> (Или нажмите <color=#EC402C>USE - Клавиша E</color>) для быстрого улучшения при постройке.";
            [JsonProperty("Время показа оповещения")]
            public int InfoNoticeTextTime = 5;
        }

        public class CommandSettings
        {
            [JsonProperty("Список чатовых и консольных команд переключения авто-улучшения")]
            public List<string> ChatCMD = new List<string>();

        }

        class PluginConfig
        {
            [JsonProperty("Configuration Version")]
            public VersionNumber PluginVersion = new VersionNumber();
            [JsonProperty("Основные настройки")]
            public MainSettings mainSettings;
            [JsonProperty("Сообщения")]
            public MessagesSettings messagesSettings;
            [JsonProperty("Настройки GUI Panel")]
            public GUISettings gUISettings;
            [JsonProperty("Настройки GUI Text")]
            public GUISettingsText gUISettingsText;

            [JsonProperty("Настройки GUI Оповещения")]
            public InfoNotiseSettings infoNotiseSettings;

            [JsonProperty("Команды")]
            public CommandSettings commandSettings;
            [JsonIgnore]
            [JsonProperty("Server Initialized‌‌‍‍‍​​")]
            public bool Init = false;

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    PluginVersion = new VersionNumber(),
                    commandSettings = new CommandSettings()
                    {
                        ChatCMD = new List<string>()
                        {
                           "up",
                            "upgrade"
                         },
                    },
                    gUISettings = new GUISettings(),
                    gUISettingsText = new GUISettingsText(),
                    infoNotiseSettings = new InfoNotiseSettings(),
                    mainSettings = new MainSettings(),
                    messagesSettings = new MessagesSettings()
                };
            }
        }

        public Timer mytimer;

        void cmdAutoGrade(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            if (config.mainSettings.permissionOn && !permission.UserHasPermission(player.UserIDString, config.mainSettings.permissionAutoGrade))
            {
                SendReply(player, config.messagesSettings.MessageAutoGradePrem);
                return;
            }
            int grade;
            timers[player] = config.mainSettings.resetTime;
            if (config.mainSettings.EnabledRemove)
            {
                var removeEnabled = (bool)Remove?.Call("OnRemoveActivate", player.userID);
                if (removeEnabled)
                {
                    Remove?.Call("RemoveDeativate", player.userID);
                }
            }
            if (args == null || args.Length <= 0 || args[0] != "1" && args[0] != "2" && args[0] != "3" && args[0] != "4" && args[0] != "0")
            {
                if (!grades.ContainsKey(player))
                {
                    grade = (int)(grades[player] = BuildingGrade.Enum.Wood);
                    SendReply(player, config.messagesSettings.MessageAutoGradeOn);
                }
                else
                {
                    grade = (int)grades[player];
                    grade++;
                    grades[player] = (BuildingGrade.Enum)Mathf.Clamp(grade, 1, 5);
                }
                if (grade > 4)
                {
                    grades.Remove(player);
                    timers.Remove(player);
                    DestroyUI(player);
                    SendReply(player, config.messagesSettings.MessageAutoGradeOff);
                    return;
                }
                timers[player] = config.mainSettings.resetTime;
                DrawUI(player, (BuildingGrade.Enum)grade, config.mainSettings.resetTime);
                return;
            }
            switch (args[0])
            {
                case "1":
                    grade = (int)(grades[player] = BuildingGrade.Enum.Wood);
                    timers[player] = config.mainSettings.resetTime;
                    DrawUI(player, BuildingGrade.Enum.Wood, config.mainSettings.resetTime);
                    return;
                case "2":
                    grade = (int)(grades[player] = BuildingGrade.Enum.Stone);
                    timers[player] = config.mainSettings.resetTime;
                    DrawUI(player, BuildingGrade.Enum.Stone, config.mainSettings.resetTime);
                    return;
                case "3":
                    grade = (int)(grades[player] = BuildingGrade.Enum.Metal);
                    timers[player] = config.mainSettings.resetTime;
                    DrawUI(player, BuildingGrade.Enum.Metal, config.mainSettings.resetTime);
                    return;
                case "4":
                    grade = (int)(grades[player] = BuildingGrade.Enum.TopTier);
                    timers[player] = config.mainSettings.resetTime;
                    DrawUI(player, BuildingGrade.Enum.TopTier, config.mainSettings.resetTime);
                    return;
                case "0":
                    grades.Remove(player);
                    timers.Remove(player);
                    DestroyUI(player);
                    SendReply(player, config.messagesSettings.MessageAutoGradeOff);
                    return;
            }
        }
        void consoleAutoGrade(ConsoleSystem.Arg arg, string[] args)
        {
            var player = arg.Player();
            if (config.mainSettings.permissionOn && !permission.UserHasPermission(player.UserIDString, config.mainSettings.permissionAutoGrade))
            {
                SendReply(player, config.messagesSettings.MessageAutoGradePrem);
                return;
            }
            int grade;
            if (config.mainSettings.EnabledRemove)
            {
                var removeEnabled = (bool)Remove.Call("OnRemoveActivate", player.userID);
                if (removeEnabled)
                {
                    Remove.Call("RemoveDeativate", player.userID);
                }
            }
            timers[player] = config.mainSettings.resetTime;
            if (player == null) return;
            if (args == null || args.Length <= 0)
            {
                if (!grades.ContainsKey(player))
                {
                    grade = (int)(grades[player] = BuildingGrade.Enum.Wood);
                    SendReply(player, config.messagesSettings.MessageAutoGradeOn);
                }
                else
                {
                    grade = (int)grades[player];
                    grade++;
                    grades[player] = (BuildingGrade.Enum)Mathf.Clamp(grade, 1, 5);
                }
                if (grade > 4)
                {
                    grades.Remove(player);
                    timers.Remove(player);
                    DestroyUI(player);
                    SendReply(player, config.messagesSettings.MessageAutoGradeOff);
                    return;
                }
                timers[player] = config.mainSettings.resetTime;
                DrawUI(player, (BuildingGrade.Enum)grade, config.mainSettings.resetTime);
            }
        }

        void Init()
        {
            permission.RegisterPermission(config.mainSettings.permissionAutoGrade, this);
            permission.RegisterPermission(config.mainSettings.permissionAutoGradeFree, this);
            permission.RegisterPermission(config.mainSettings.permissionAutoGradeHammer, this);
            config.commandSettings.ChatCMD.ForEach(c => cmd.AddChatCommand(c, this, cmdAutoGrade));
            config.commandSettings.ChatCMD.ForEach(c => cmd.AddConsoleCommand(c, this, nameof(consoleAutoGrade)));
        }
        void OnServerInitialized()
        {

            if (!config.infoNotiseSettings.InfoNotice)
                Unsubscribe(nameof(OnActiveItemChanged));
            else
                Subscribe(nameof(OnActiveItemChanged));

            if (!config.mainSettings.EnabledInput)
                Unsubscribe("OnPlayerInput");
            else
                Subscribe("OnPlayerInput");

            timer.Every(1f, GradeTimerHandler);
            config.Init = true;
        }

        private void OnActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)
        {
            if (grades.ContainsKey(player)) return;
            if (newItem == null || newItem.info.shortname != "building.planner") return;
            player.SendConsoleCommand("gametip.showgametip", $"<size={config.infoNotiseSettings.InfoNoticeSize}>{config.infoNotiseSettings.InfoNoticeText}</color>");
            timer.Once(config.infoNotiseSettings.InfoNoticeTextTime, () => player?.SendConsoleCommand("gametip.hidegametip"));
        }

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (player == null || input == null || !config.Init) return;
            Item activeItem = player.GetActiveItem();
            if (activeItem == null || activeItem.info.shortname != "building.planner") return;
            if (input.WasJustPressed(BUTTON.USE))
            {
                if (config.mainSettings.EnabledRemove)
                {
                    var removeEnabled = (bool)Remove?.Call("OnRemoveActivate", player.userID);
                    if (removeEnabled)
                        Remove?.Call("RemoveDeativate", player.userID);
                }
                if (config.mainSettings.permissionOn && !permission.UserHasPermission(player.UserIDString, config.mainSettings.permissionAutoGrade))
                {
                    SendReply(player, config.messagesSettings.MessageAutoGradePrem);
                    return;
                }
                int grade;
                timers[player] = config.mainSettings.resetTime;
                if (!grades.ContainsKey(player))
                {
                    grade = (int)(grades[player] = BuildingGrade.Enum.Wood);
                    SendReply(player, config.messagesSettings.MessageAutoGradeOn);
                }
                else
                {
                    grade = (int)grades[player];
                    grade++;
                    grades[player] = (BuildingGrade.Enum)Mathf.Clamp(grade, 1, 5);
                }
                if (grade > 4)
                {
                    grades.Remove(player);
                    timers.Remove(player);
                    DestroyUI(player);
                    SendReply(player, config.messagesSettings.MessageAutoGradeOff);
                    return;
                }
                timers[player] = config.mainSettings.resetTime;
                DrawUI(player, (BuildingGrade.Enum)grade, config.mainSettings.resetTime);
                return;
            }
        }

        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                player?.SendConsoleCommand("gametip.hidegametip");
                DestroyUI(player);
            }
        }

        object OnHammerHit(BasePlayer player, HitInfo info)
        {
            if (!config.Init) return null;

            if (player == null || info == null || info.HitEntity == null) return null;

            var buildingBlock = info?.HitEntity.GetComponent<BuildingBlock>();
            if (buildingBlock == null) return null;

            if (config.mainSettings.permissionOn && !permission.UserHasPermission(player.UserIDString, config.mainSettings.permissionAutoGradeHammer))
            {
                SendReply(player, config.messagesSettings.MessageAutoGradePremHammer);
                return null;
            }
            Grade(buildingBlock, player);
            return null;
        }

        void OnEntityBuilt(Planner planner, GameObject gameObject)
        {
            if (planner == null || gameObject == null || !config.Init) return;
            var player = planner.GetOwnerPlayer();
            if (player == null) return;
            if (gameObject.ToBaseEntity() == null) return;
            BuildingBlock entity = gameObject.ToBaseEntity() as BuildingBlock;
            if (entity == null || entity.IsDestroyed) return;
            Grade(entity, player);
        }


        void Grade(BuildingBlock block, BasePlayer player)
        {
            if (block == null) return;
            if (config.mainSettings.useNoEscape && NoEscape)
            {
                object can = NoEscape?.Call("IsRaidBlocked", player);
                if (can != null) if ((bool)can)
                    {
                        SendReply(player, "Вы не можете использовать Upgrade во время рейд-блока");
                        return;
                    }
            }

            if (config.mainSettings.useBuildingProtection && BuildingProtection && player.GetBuildingPrivilege() != null)
            {
                if ((bool)BuildingProtection?.Call("IsProtection", player.GetBuildingPrivilege().net.ID))
                {
                    SendReply(player, "Строительство при включенной защите запрещено.");
                    return;
                }
            }

            BuildingGrade.Enum grade;
            if (!grades.TryGetValue(player, out grade)) return;

            var reply = 0;
            if (reply == 0) { }
            if (config.mainSettings.getBuild && player.GetBuildingPrivilege() != null && !player.GetBuildingPrivilege().IsAuthed(player))
            {
                player.ChatMessage("<color=ffcc00><size=16><color=#EC402C>Upgrade</color> запрещен в билдинг блоке!!!</size></color>");
                return;
            }
            if (block.blockDefinition.checkVolumeOnUpgrade)
            {
                if (DeployVolume.Check(block.transform.position, block.transform.rotation, PrefabAttribute.server.FindAll<DeployVolume>(block.prefabID), ~(1 << block.gameObject.layer)))
                {
                    player.ChatMessage("Вы не можете улучшить постройку находясь в ней");
                    return;
                }
            }
            var ret = Interface.Call("CanUpgrade", player) as string;
            if (ret != null)
            {
                SendReply(player, ret);
                return;
            }
            if (block.grade > grade)
            {
                SendReply(player, "Нельзя понижать уровень строения!");
                return;
            }
            if (block.grade == grade)
            {
                SendReply(player, "Уровень строения соответствует выбранному.");
                return;
            }
            if (block.Health() != block.MaxHealth() && !config.mainSettings.CanUpgradeDamaged)
            {
                SendReply(player, "Нельзя улучшать повреждённые постройки!");
                return;
            }

            if (!CanAffordUpgrade(block, grade, player))
            {
                SendReply(player, config.messagesSettings.MessageAutoGradeNo);
                return;
            }

            if (config.mainSettings.permissionAutoGradeAdmin && !permission.UserHasPermission(player.UserIDString, config.mainSettings.permissionAutoGradeFree))
                PayForUpgrade(GetGrade(block, grade), player);

            block.SetGrade(grade);
            block.SetHealthToMax();
            block.UpdateSkin(false);
            Effect.server.Run(string.Concat("assets/bundled/prefabs/fx/build/promote_", grade.ToString().ToLower(), ".prefab"), block, 0, Vector3.zero, Vector3.zero, null, false);
            timers[player] = config.mainSettings.resetTime;
            DrawUI(player, grade, config.mainSettings.resetTime);
        }

        void GradeTimerHandler()
        {
            foreach (var player in timers.Keys.ToList())
            {
                var seconds = --timers[player];
                if (seconds <= 0)
                {
                    grades.Remove(player);
                    timers.Remove(player);
                    DestroyUI(player);
                    continue;
                }
                DrawUI(player, grades[player], seconds);
            }
        }

        void DrawUI(BasePlayer player, BuildingGrade.Enum grade, int seconds)
        {
            DestroyUI(player);
            CuiHelper.AddUi(player, GUI.Replace("{0}", gradesString[grade]).Replace("{1}", seconds.ToString()).Replace("{PanelColor}", config.gUISettings.PanelColor).Replace("{PanelAnchorMin}", config.gUISettings.PanelAnchorMin).Replace("{PanelAnchorMax}", config.gUISettings.PanelAnchorMax).Replace("{TextFontSize}", config.gUISettingsText.TextFontSize.ToString()).Replace("{TextСolor}", config.gUISettingsText.TextСolor.ToString()).Replace("{TextAnchorMin}", config.gUISettingsText.TextAnchorMin).Replace("{TextAnchorMax}", config.gUISettingsText.TextAnchorMax));
        }

        void DestroyUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "autograde.panel");
            CuiHelper.DestroyUi(player, "autogradetext");
        }

        private string GUI = @"[{""name"": ""autograde.panel"",""parent"": ""Hud"",""components"": [{""type"": ""UnityEngine.UI.Image"",""color"": ""{PanelColor}""},{""type"": ""RectTransform"",""anchormin"": ""{PanelAnchorMin}"",""anchormax"": ""{PanelAnchorMax}""}]}, {""name"": ""autogradetext"",""parent"": ""autograde.panel"",""components"": [{""type"": ""UnityEngine.UI.Text"",""text"": ""Авто-улучшение до {0} закончится через: " + @"{1} сек."",""fontSize"": ""{TextFontSize}"",""align"": ""MiddleCenter""}, {""type"": ""RectTransform"",""anchormin"": ""{TextAnchorMin}"",""anchormax"": ""{TextAnchorMax}""}]}]";
        void UpdateTimer(BasePlayer player, ulong playerid = 0)
        {
            timers[player] = config.mainSettings.resetTime;
            DrawUI(player, grades[player], timers[player]);
        }

        object BuildingUpgradeActivate(ulong id)
        {
            var player = BasePlayer.FindByID(id);
            if (player != null) if (grades.ContainsKey(player)) return true;
            return false;
        }

        void BuildingUpgradeDeactivate(ulong id)
        {
            var player = BasePlayer.FindByID(id);
            if (player != null)
            {
                grades.Remove(player);
                timers.Remove(player);
                DestroyUI(player);
            }
        }
    }
}