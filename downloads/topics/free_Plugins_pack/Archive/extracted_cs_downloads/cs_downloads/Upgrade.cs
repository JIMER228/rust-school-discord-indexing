using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Upgrade", "b1xbyy", "1.0.0")]
    class Upgrade : RustPlugin
    {
        private PluginConfig config;
        protected override void LoadDefaultConfig()
        {
            config = PluginConfig.DefaultConfig();
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();
        }
        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }
        private class PluginConfig
        {
            [JsonProperty("Выключить GUI?")]
            public bool disablegui;
            [JsonProperty("Время действия апгрейда")]
            public int time;
            [JsonProperty("Расположение GUI - AnchorMin")]
            public string AnchorMin;
            [JsonProperty("Расположение GUI - AnchorMax")]
            public string AnchorMax;
            [JsonProperty("GUI - Цвет фона")]
            public string color;
            [JsonProperty("Сообщения")]
            public List<string> messages;
            [JsonProperty("Сообщения - Названия")]
            public List<string> messages2;
            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    time = 60,
                    AnchorMin = "0.3447913 0.8135",
                    AnchorMax = "0.640625 0.8435",
                    color = "0.97 0.92 0.88 0.18",
                    messages = new List<string>
                    {
                        "АВТОУЛУЧШЕНИЕ ДО <color=#4682B4>{grade}</color> ВЫКЛЮЧИТСЯ ЧЕРЕЗ <color=#4682B4>{count} СЕК</color>",
                        "Режим улучшения выключен.",
                        "Этот объект не нуждается в улучшении до <color=#4682B4>{grade}</color>!",
                        "Вы не можете авто-улучшать постройки <color=#4682B4>во время рейда</color>!",
                        "Вы находитесь на чужой территории!",
                        "Этот объект можно будет улучшить через <color=#4682B4>{count} секунд</color>",
                        "У вас не хватает ресурсов для улучшения до <color=#4682B4>{grade}</color>!",
                        "Используйте <color=#4682B4>план постройки</color> для улучшения объектов.\nКоманда <color=#4682B4>/up</color> - сменить ресурс улучшения"
                    },
                    messages2 = new List<string>()
                    {
                        { "None" }, { "ДЕРЕВО" }, { "КАМЕНЬ" }, { "МЕТАЛ" }, { "МВК" }
                    },
                    disablegui = false
                };
            }
        }

        static string GUIjson = "";
        static Upgrade _ins;
        string helpstring;

        void Init()
        {
        }

        void OnServerInitialized()
        {
            SaveConfig();
            if (config.disablegui) Unsubscribe(nameof(OnActiveItemChanged));

            GUIjson = "[{\"name\":\"UpgradeGUIBackground\", \"parent\":\"Hud\", \"components\":[{\"type\":\"UnityEngine.UI.Image\", \"color\":\"{color}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0.5 0.8\",\"anchormax\":\"0.5 0.8\",\"offsetmin\":\"-199.5 80\",\"offsetmax\":\"180.5 {max}\"}]},{\"name\":\"UpgradeGUIText\",\"parent\":\"UpgradeGUIBackground\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{text}\",\"font\":\"robotocondensed-regular.ttf\",\"align\":\"MiddleCenter\"},{\"type\":\"UnityEngine.UI.Outline\",\"color\":\"0 0 0 0.5\",\"distance\":\"0.5 -0.5\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\"}]}]".Replace("{max}", "100").Replace("{color}", config.color);

            float number;
            string[] ar = config.AnchorMax.Split(' ');
            if (float.TryParse(ar[1], out number))
            {
                helpstring = ar[0] + " " + (number + 0.03f).ToString();
            }
            else Debug.LogError("Конфиг поврежден!");

            _ins = this;

            permission.RegisterPermission("Upgrade.use", this);
            permission.RegisterPermission("Upgrade.admin", this);
        }

        void unloadbehavior(BasePlayer player)
        {
            player.GetComponent<UpgradeConstruction>()?.DoDestroy();
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            unloadbehavior(player);
        }

        private void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList) unloadbehavior(player);
        }

        Dictionary<ulong, Timer> activegui = new Dictionary<ulong, Timer>();
        private void OnActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)
        {
            if (newItem != null)
            {
                if (newItem.info.itemid.Equals(1525520776))
                {
                    if (player.GetComponent<UpgradeConstruction>()) return;
                    destroynotif(player);
                    CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", GUIjson.Replace("{text}", config.messages[7]).Replace("{max}", "100"));
                    activegui.Add(player.userID, timer.Once(7f, () =>
                    {
                        destroynotif(player);
                    }));
                }
                else if (activegui.ContainsKey(player.userID))
                {
                    destroynotif(player);
                }
            }
            else if (activegui.ContainsKey(player.userID))
            {
                destroynotif(player);
            }
        }

        void destroynotif(BasePlayer player)
        {
            if (activegui.ContainsKey(player.userID))
            {
                activegui[player.userID]?.Destroy();
                activegui.Remove(player.userID);
            }
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "UpgradeGUIBackground");
        }

        [ChatCommand("up")]
        void cmdChatUpgrade(BasePlayer player, string command, string[] args)
        {
            upgradecommand(player, args);
        }

        [ChatCommand("deletemenu")]
        void cmdChatRemove(BasePlayer player, string command, string[] args)
        {
            player.GetComponent<UpgradeConstruction>()?.DoDestroy();
        }

        [ConsoleCommand("upgrade.use")]
        void ConsoleUP(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;
            upgradecommand(player, arg.Args);
        }

        [ConsoleCommand("upgrade.off")]
        void ConsoleUpgradeOff(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;
            player.GetComponent<UpgradeConstruction>()?.DoDestroy();
        }

        void upgradecommand(BasePlayer player, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "Upgrade.use"))
            {
                SendReply(player, "У тебя нету прав на использование этой команды");
                return;
            }
            int current = 0;
            if (args != null && args.Length > 0) 
            {
                if (!int.TryParse(args[0], out current) || current < 1 || current > 4)
                {
                    current = 2; // Устанавливаем КАМЕНЬ по умолчанию
                }
            }
            else
            {
                current = 2; // Устанавливаем КАМЕНЬ по умолчанию
            }

            UpgradeConstruction tool = player.GetComponent<UpgradeConstruction>();
            if (tool != null)
            {
                tool.changegrade(current);
                return;
            }

            if (activegui != null && activegui.ContainsKey(player.userID))
            {
                activegui[player.userID]?.Destroy();
                activegui.Remove(player.userID);
            }
            tool = player.gameObject.AddComponent<UpgradeConstruction>();
            tool.currentgrade = current;
            PrintToChat(player, config.messages[7]);
        }

        class UpgradeConstruction : MonoBehaviour
        {
            BasePlayer player;
            public int count = _ins.config.time;
            public int currentgrade = 2;

            void Awake()
            {
                player = GetComponent<BasePlayer>();
                if (player == null) Destroy(this);
                InvokeRepeating("UpdateGUI", 0, 1f);
            }

            public void changegrade(int change)
            {
                if (change > 4) currentgrade = 1;
                else if (change <= 0) currentgrade = 2;
                else currentgrade = change;
                count = _ins.config.time;
                UpdateGUI();
                EffectNetwork.Send(new Effect("assets/bundled/prefabs/fx/notice/loot.drag.grab.fx.prefab", player, 0, Vector3.up, Vector3.zero) { scale = UnityEngine.Random.Range(0f, 1f) }, player.net.connection);
            }

            void UpdateGUI()
            {
                if (count.Equals(0))
                {
                    DoDestroy();
                    return;
                }
                if (!_ins.config.disablegui)
                {
                    CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "UpgradeGUIBackground");
                    CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", GUIjson.Replace("{text}", _ins.config.messages[0].Replace("{grade}", _ins.config.messages2[currentgrade]).Replace("{count}", count.ToString())).Replace("{max}", "100"));
                }
                count--;
            }

            public void DoDestroy()
            {
                Destroy(this);
            }

            void OnDestroy()
            {
                if (!_ins.config.disablegui) CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "UpgradeGUIBackground");
                _ins.PrintToChat(player, _ins.config.messages[1]);
            }
        }

        [PluginReference] Plugin NoEscape = null;

        private void OnEntityBuilt(Planner plan, GameObject go)
        {
            if (plan == null || go == null) return;
            BasePlayer player = plan.GetOwnerPlayer();
            if (player == null) return;
            UpgradeConstruction gr = player.GetComponent<UpgradeConstruction>();
            if (gr == null) return;
            BuildingBlock block = go.GetComponent<BuildingBlock>();
            if (block == null) return;
            
            // Обновляем счетчик до начального значения
            gr.count = _ins.config.time;
            
            TryUpgrade(player, block, gr);
        }

        object OnHammerHit(BasePlayer player, HitInfo info)
        {
            if (player == null || info == null) return null;
            if (info.HitEntity is BuildingBlock)
            {
                var block = info.HitEntity as BuildingBlock;
                if (block == null) return null;

                var grade = player.GetComponent<UpgradeConstruction>();
                if (grade == null) return null;

                TryUpgrade(player, block, grade);
                return true;
            }
            return null;
        }

        private void TryUpgrade(BasePlayer player, BuildingBlock block, UpgradeConstruction gr)
        {
            if (block == null) return;

            if ((int)block.grade >= gr.currentgrade)
            {
                _ins.PrintToChat(player, config.messages[2].Replace("{grade}", config.messages2[gr.currentgrade]));
                return;
            }

            if (NoEscape != null)
            {
                object can = NoEscape?.Call("IsRaidBlocked", player);
                if (can != null && (bool)can)
                {
                    gr.DoDestroy();
                    _ins.PrintToChat(player, config.messages[3]);
                    return;
                }
            }

            if (!player.CanBuild())
            {
                gr.DoDestroy();
                _ins.PrintToChat(player, config.messages[4]);
                return;
            }

            if (block.SecondsSinceAttacked < 30)
            {
                _ins.PrintToChat(player, config.messages[5].Replace("{count}", (30 - block.SecondsSinceAttacked).ToString()));
                return;
            }

            BuildingGrade.Enum grade = (BuildingGrade.Enum)gr.currentgrade;

            if (!CanAffordUpgrade(block, grade, player))
            {
                _ins.PrintToChat(player, config.messages[6].Replace("{grade}", config.messages2[gr.currentgrade]));
                return;
            }

            PayForUpgrade(block, grade, player);
            UpgradeBuildingBlock(block, grade);
        }

        private bool CanAffordUpgrade(BuildingBlock block, BuildingGrade.Enum grade, BasePlayer player)
        {
            var dict = new Dictionary<int, int>();

            foreach (var itemAmount in block.blockDefinition.GetGrade(grade, block.skinID).CostToBuild())
            {
                int amount;
                if (!dict.TryGetValue(itemAmount.itemid, out amount))
                    amount = player.inventory.GetAmount(itemAmount.itemid);
                if (amount < itemAmount.amount)
                    return false;

                dict[itemAmount.itemid] = amount - Mathf.RoundToInt(itemAmount.amount);
            }

            return true;
        }

        private void PayForUpgrade(BuildingBlock block, BuildingGrade.Enum grade, BasePlayer player)
        {
            var collect = new List<Item>();

            foreach (var itemAmount in block.blockDefinition.GetGrade(grade, block.skinID).CostToBuild())
            {
                player.inventory.Take(collect, itemAmount.itemid, (int)itemAmount.amount);
                player.Command("note.inv " + itemAmount.itemid + " " + (float)((int)itemAmount.amount * -1.0));
            }
        }

        private static void UpgradeBuildingBlock(BuildingBlock block, BuildingGrade.Enum grade)
        {
            if (block == null || block.IsDestroyed) return;

            block.SetGrade(grade);
            block.SetHealthToMax();
            block.StartBeingRotatable();
            block.SendNetworkUpdate();
            block.UpdateSkin();
            block.ResetUpkeepTime();
            block.UpdateSurroundingEntities();
            block.GetBuilding()?.Dirty();
        }
    }
}