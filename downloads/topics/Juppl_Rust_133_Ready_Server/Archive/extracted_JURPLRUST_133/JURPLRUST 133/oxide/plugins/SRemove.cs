using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Oxide.Game.Rust.Cui;

using Facepunch;
namespace Oxide.Plugins
{
    [Info("SRemove", "DeRzKiU", "1.0.0")]
    public class SRemove : RustPlugin
    {
        [PluginReference]
        private Plugin Clans;

        [PluginReference]
        private Plugin SrvStat;

        [PluginReference]
        private Plugin Friends;

        [PluginReference]
        private Plugin NoEscape;

        private PluginConfig _config;

        private readonly int triggerLayer = LayerMask.GetMask("Trigger");
        private readonly int triggerMask = LayerMask.GetMask("Prevent_Building");
        private static Dictionary<string, int> deployedToItem = new Dictionary<string, int>();
        private Dictionary<ulong, DateTime> Removers = new Dictionary<ulong, DateTime>();
        private Dictionary<ulong, int> AmountEntities = new Dictionary<ulong, int>();
        static FieldInfo buildingPrivlidges;

        #region Classes

        class PluginConfig
        {
            public float RateOfReturn { get; set; }

            public bool UseNoEscape { get; set; }

            public bool RemoveViaCupboard { get; set; }
            public bool RemoveViaFriends { get; set; }
            public bool RemoveViaClans { get; set; }

            public bool NeedAccessCupboard { get; set; }
        }

        #endregion

        #region Oxide Hooks

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            Config.WriteObject(new PluginConfig
            {
                RateOfReturn = 1.0f,
                UseNoEscape = false,
                RemoveViaClans = false,
                RemoveViaFriends = false,
                RemoveViaCupboard = false,
                NeedAccessCupboard = true,
            }, true);
            PrintWarning("Default configuration file created.");
        }

        void OnServerInitialized()
        {
            _config = Config.ReadObject<PluginConfig>();

            foreach (BasePlayer player in BasePlayer.activePlayerList)
                player.SendConsoleCommand($"bind z remove");
            buildingPrivlidges = typeof(BasePlayer).GetField("buildingPrivilege", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));


            deployedToItem.Clear();

            List<ItemDefinition> ItemsDefinition = ItemManager.GetItemDefinitions() as List<ItemDefinition>;
            foreach (ItemDefinition itemdef in ItemsDefinition)
            {
                if (itemdef?.GetComponent<ItemModDeployable>() == null) continue;
                if (deployedToItem.ContainsKey(itemdef.GetComponent<ItemModDeployable>().entityPrefab.resourcePath)) continue;
                deployedToItem.Add(itemdef.GetComponent<ItemModDeployable>().entityPrefab.resourcePath, itemdef.itemid);
            }
        }

        void Unloaded()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(player, "removeui");
        }

        object OnStructureRepair(BaseCombatEntity entity, BasePlayer player)
        {
            if (Removers.ContainsKey(player.userID))
            {
                return false;
            }
            return null;
        }

        void OnHammerHit(BasePlayer player, HitInfo info)
        {
            if (!Removers.ContainsKey(player.userID))
                return;

            BaseEntity entity = info?.HitEntity;

            if (!entity)
            {
                SendReply(player, "Объект не найден");
                return;
            }

            var storage = entity as StorageContainer;
            if (storage != null && storage.inventory.itemList.Count != 0)
            {
                player.ChatMessage("Объект имеет содержимое. Ремув запрещен.");
                return;
            }

            if (_config.UseNoEscape)
            {
                var block = NoEscape?.Call<bool>("IsEscapeBlocked", player);
                if (block == true)
                {
                    Removers.Remove(player.userID);
                    SendReply(player, "Вы не можете использовать /remove во время рейд-блока");
                    return;
                }
            }

            if (_config.RemoveViaCupboard)
            {
                var hasAccess = hasTotalAccess(player, player.transform.position);
                if (hasAccess == null)
                {
                    AmountEntities[player.userID] += 1;
                    SrvStat?.Call("addremoved", 1);
                    Refund(player, entity);
                    Removers[player.userID] = timenow();
                    entity.KillMessage();
                    return;
                }
            }

            bool canRemove = false;
            if (_config.RemoveViaClans)
            {
                var clanTagPlayer = Clans?.Call<string>("GetClanOf", player);
                var clanTagOwner = Clans?.Call<string>("GetClanOf", entity.OwnerID);
                if (clanTagPlayer != null && clanTagOwner != null && clanTagPlayer == clanTagOwner)
                    canRemove = true;
            }

            if (_config.RemoveViaFriends)
            {
                var isFriend = Friends?.Call<bool>("IsFriend", player.userID, entity.OwnerID);

                if (isFriend == true)
                    canRemove = true;
            }

            if (entity.OwnerID != player.userID)
            {
                if (!canRemove)
                {
                    SendReply(player, "У вас нет прав удалить этот объект");
                    return;
                }
            }

            if (_config.NeedAccessCupboard)
            {
                var hasAccess = hasTotalAccess(player, player.transform.position);
                if (hasAccess != null)
                {
                    SendReply(player, hasTotalAccess(player, player.transform.position));
                    return;
                }
            }

            AmountEntities[player.userID] += 1;
            SrvStat?.Call("addremoved", 1);
            Refund(player, entity);
            Removers[player.userID] = timenow();
            entity.KillMessage();
            return;
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            var victim = entity as BasePlayer;
            if (victim)
            {
                if (Removers.ContainsKey(victim.userID))
                {
                    Removers.Remove(victim.userID);
                    SendReply(victim, $"{offremove}");
                    if (AmountEntities[victim.userID] > 0)
                        SendReply(victim, $"Вы заремували <color=#00FF7F>{AmountEntities[victim.userID]}</color> объ.");
                    AmountEntities.Remove(victim.userID);
                }
            }
        }

        void OnPlayerInit(BasePlayer player)
        {
            player.SendConsoleCommand($"bind z remove");
        }

        #endregion

        #region Config

        string hasnotool = "Установи шкаф для ремува";
        string hasnoacces = "Ты должен быть авторизован в шкафу";
        string offremove = "";
        string onremove = "";

        #endregion

        #region API

        string isremoveenabled(BasePlayer player)
        {
            if (Removers.ContainsKey(player.userID))
            {
                return "yes";
            }
            return "no";
        }

        #endregion

        #region USERCOMMANDS

        [ConsoleCommand("remove")]
        void ccmdremove(ConsoleSystem.Arg arg)
        {
            if (arg.connection?.player != null)
            {
                var player = arg.Player();
                if (Removers.ContainsKey(player.userID))
                {
                    Removers.Remove(player.userID);
                    CuiHelper.DestroyUi(player, "removeui");
                    SendReply(player, $"{offremove}");
                    if (AmountEntities[player.userID] > 0)
                        SendReply(player, $"Вы заремували <color=#00FF7F>{AmountEntities[player.userID]}</color> объ.");
                    return;
                }
                goact(player, 30);
            }
            else
                return;
        }

        [ChatCommand("remove")]
        void chatremove(BasePlayer player, string command, string[] arg)
        {
            if (Removers.ContainsKey(player.userID))
            {
                Removers.Remove(player.userID);
                CuiHelper.DestroyUi(player, "removeui");
                SendReply(player, $"{offremove}");
                if (AmountEntities[player.userID] > 0)
                    SendReply(player, $"Вы заремували <color=#00FF7F>{AmountEntities[player.userID]}</color> объ.");
                return;
            }
            string time = "30";
            if (arg.Length != 0)
            {
                int outtime;
                bool result = Int32.TryParse(arg[0], out outtime);
                if (result)
                {
                    time = arg[0];
                    if (arg.Length == 0)
                        time = "30";
                    if (Convert.ToInt32(time) < 0 || Convert.ToInt32(time) > 30)
                        time = "30";
                }
                else
                {
                    time = "30";
                }
            }

            SendReply(player, "Нажми на Z чтобы вкл/выкл ремув");
            goact(player, Convert.ToInt32(time));
        }

        #endregion

        #region MAINFUNCTIONS

        void goact(BasePlayer player, int time)
        {
            object can = NoEscape?.Call("IsEscapeBlocked", player);
            if (can != null)
                if ((bool)can == true)
                {
                    SendReply(player, "Вы не можете использовать /remove во время рейд-блока");
                    return;
                }
            Removers[player.userID] = timenow();
            AmountEntities[player.userID] = 0;
            RemoveGui(player, time);
            timer.Once(1, () => goactremovetoend(player, time));
            SendReply(player, $"{onremove}");
        }

        int checktime(BasePlayer player, int sec)
        {
            if (Removers.ContainsKey(player.userID))
            {
                DateTime dateTime = DateTime.UtcNow;
                var usertime = Removers[player.userID];
                DateTime timeplus = usertime.AddSeconds(sec);
                var timetoend = (timeplus - dateTime).Seconds;
                if (timetoend > 0)
                    return timetoend;
                else
                    return -1;
            }
            return -1;
        }

        void goactremovetoend(BasePlayer player, int sec)
        {
            if (!Removers.ContainsKey(player.userID)) return;

            if (checktime(player, sec) != -1 && Removers.ContainsKey(player.userID))
                timer.Once(0.5f, () => goactremovetoend(player, sec));
            else
            {
                Removers.Remove(player.userID);
                SendReply(player, $"{offremove}");
                if (AmountEntities[player.userID] > 0)
                    SendReply(player, $"Вы заремували <color=#00FF7F>{AmountEntities[player.userID]}</color> объ.");
                AmountEntities.Remove(player.userID);
                return;
            }
        }

        void GiveAndShowItem(BasePlayer player, int item, int amount)
        {
            player.inventory.GiveItem(ItemManager.CreateByItemID(item, amount), null);
            player.Command("note.inv", new object[] { item, amount });
        }

        void Refund(BasePlayer player, BaseEntity entity)
        {
            if (deployedToItem.ContainsKey(entity.PrefabName))
            {
                if (entity.PrefabName.Contains("furnace.large"))
                {
                    GiveAndShowItem(player, 3655341, 500); //дерево
                    GiveAndShowItem(player, -892070738, 500); // камни
                    GiveAndShowItem(player, 28178745, 75); // топливо
                    return;
                }
                if (entity.PrefabName.Contains("furnace"))
                {
                    GiveAndShowItem(player, 3655341, 100); //дерево
                    GiveAndShowItem(player, -892070738, 200); // камни
                    GiveAndShowItem(player, 28178745, 50); // топливо
                    return;
                }
                if (entity.PrefabName.Contains("campfire"))
                {
                    GiveAndShowItem(player, 3655341, 50); //дерево
                    return;
                }
                if (entity.PrefabName.Contains("ceilinglight"))
                {
                    GiveAndShowItem(player, 688032252, 50); //металл
                    GiveAndShowItem(player, 28178745, 15); // топливо
                    return;
                }
                if (entity.PrefabName.Contains("lantern.deployed"))
                {
                    GiveAndShowItem(player, 688032252, 50); //металл
                    GiveAndShowItem(player, 28178745, 10); // топливо
                    return;
                }
                if (entity.PrefabName.Contains("refinery_small"))
                {
                    GiveAndShowItem(player, 3655341, 100); //дерево
                    GiveAndShowItem(player, 28178745, 250); // топливо
                    GiveAndShowItem(player, 688032252, 500); //металл
                    return;
                }
                if (entity.PrefabName.Contains("tunalight"))
                {
                    GiveAndShowItem(player, 3655341, 20); //дерево
                    GiveAndShowItem(player, 28178745, 5); // топливо
                    GiveAndShowItem(player, 1050986417, 10); //банка
                    return;
                }
                GiveAndShowItem(player, deployedToItem[entity.PrefabName], 1);
            }
            if (entity is BuildingBlock)
            {
                BuildingBlock buildingblock = entity as BuildingBlock;
                if (buildingblock.blockDefinition == null) return;
                int buildingblockGrade = (int)buildingblock.grade;

                if (buildingblock.blockDefinition.grades[buildingblockGrade] != null)
                {
                    List<ItemAmount> currentCost = buildingblock.blockDefinition.grades[buildingblockGrade].costToBuild as List<ItemAmount>;
                    foreach (ItemAmount ia in currentCost)
                    {
                        var item = ItemManager.CreateByItemID(ia.itemid, Convert.ToInt32(ia.amount * _config.RateOfReturn));
                        player.inventory.GiveItem(item, player.inventory.containerMain);
                        player.Command("note.inv", new object[] { ia.itemid, Convert.ToInt32(ia.amount * _config.RateOfReturn) });
                    }
                }
            }
        }

        public static DateTime timenow()
        {
            DateTime dateTime = DateTime.UtcNow;
            return dateTime;
        }

        void RemoveGui(BasePlayer player, int sec)
        {
            if (!Removers.ContainsKey(player.userID) || !player.IsAlive() || player.IsDead())
            {
                CuiHelper.DestroyUi(player, "removeui");
                return;
            }
            if (checktime(player, sec) == -1)
            {
                timer.Once(0.1f, () => RemoveGui(player, sec));
                return;
            }
            CuiHelper.DestroyUi(player, "removeui");
            var elements = new CuiElementContainer();
            var panel = elements.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0.5" },
                RectTransform = { AnchorMin = "0.343 0.113", AnchorMax = "0.64 0.143", OffsetMin = "0 0", OffsetMax = "0 0" },
            }, "Hud", "removeui");
            elements.Add(new CuiLabel
            {
                Text = { Text = $"<b>Режим удаления выключится через <color=#ff0000>{checktime(player, sec) + 1}</color> сек.</b>", FontSize = 16, Font = "robotocondensed-Bold.ttf", Align = TextAnchor.MiddleCenter },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, panel);
            CuiHelper.AddUi(player, elements);
            if (Removers.ContainsKey(player.userID))
                timer.Once(0.5f, () => RemoveGui(player, sec));
        }

        string hasTotalAccess(BasePlayer player, Vector3 targetLocation)
        {
            var colliders = Pool.GetList<Collider>();
            Vis.Colliders(targetLocation, 0.1f, colliders, triggerLayer);
            var cups = false;
            foreach (var collider in colliders)
            {
                var cup = collider.GetComponentInParent<BuildingPrivlidge>();
                if (cup == null) continue;
                cups = true;
                if (cup.IsAuthed(player))
                    return null;
            }
            Pool.FreeList(ref colliders);
            if (cups)
                return hasnoacces;
            else
                return null;
        }

        #endregion
    }
}