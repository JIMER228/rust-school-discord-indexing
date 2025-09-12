using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("Teleportation", "Netrunner", "1.0.0")]
    class Teleportation : RustPlugin
    {
        [PluginReference] Plugin NoEscape, ImageLibrary;
        Dictionary<ulong, Vector3> lastPositions = new Dictionary<ulong, Vector3>();
        Dictionary<BasePlayer, int> spectatingPlayers = new Dictionary<BasePlayer, int>();
        class TP
        {
            public BasePlayer Player;
            public BasePlayer Player2;
            public Vector3 pos;
            public bool EnabledShip;
            public int seconds;
            public TP(BasePlayer player, Vector3 Pos, int Seconds, bool EnabledShip1, BasePlayer player2 = null)
            {
                Player = player;
                pos = Pos;
                seconds = Seconds;
                EnabledShip = EnabledShip1;
                Player2 = player2;
            }
        }
        const string TPADMIN = "teleportation.admin";
        const string SpecPerm = "teleportation.spec";
        int homelimitDefault;
        Dictionary<string, int> homelimitPerms;
        int tpkdDefault;
        Dictionary<string, int> tpkdPerms;
        int tpkdhomeDefault;
        Dictionary<string, int> tpkdhomePerms;
        int teleportSecsDefault;
        bool restrictCupboard;
        bool enabledTPR;
        bool homecupboard;
        bool adminsLogs;
        bool restrictTPRCupboard;
        bool foundationEx;
        bool createSleepingBug;
        string EffectPrefab1;
        string EffectPrefab;
        bool EnabledShipTP;
        bool EnabledBallonTP;
        bool CancelTPMetabolism;
        bool CancelTPCold;
        bool CancelTPRadiation;
        bool CancelTPWounded;

        static DynamicConfigFile config;
        Dictionary<string, int> teleportSecsPerms;
        void OnNewSave()
        {
            PrintWarning("Обнаружен вайп. Очищаем данные с data/Teleportation");
            WipeData();
        }
        void WipeData()
        {
            LoadData();
            homes = new Dictionary<ulong, Dictionary<string, Vector3>>();
            SaveData();
        }
        protected override void LoadDefaultConfig()
        {
            GetVariable(Config, "Запрещать отправлять запрос на телепортацию в зоне действия чужого шкафа", out homecupboard, true);
            GetVariable(Config, "Звук уведомления при получение запроса на телепорт (пустое поле = звук отключен)", out EffectPrefab1, "assets/prefabs/locks/keypad/effects/lock.code.unlock.prefab");
            GetVariable(Config, "Звук предупреждения (пустое поле = звук отключен)", out EffectPrefab, "assets/prefabs/locks/keypad/effects/lock.code.denied.prefab");
            GetVariable(Config, "Разрешать сохранять местоположение только на фундаменте", out foundationEx, true);
            GetVariable(Config, "Создавать объект при сохранении местоположения в виде Sleeping Bag", out createSleepingBug, true);
            GetVariable(Config, "Запрещать принимать запрос на телепортацию в зоне действия чужого шкафа", out restrictCupboard, true);
            GetVariable(Config, "Логировать использование команд для администраторов", out adminsLogs, true);
            GetVariable(Config, "Разрешить команду TPR игрокам (false = /tpr не будет работать)", out enabledTPR, true);
            GetVariable(Config, "Разрешить отправку и приём телепорта и телепорт домой на корабле", out EnabledShipTP, true);
            GetVariable(Config, "Разрешить отправку и приём телепорта на воздушном шаре", out EnabledBallonTP, true);
            GetVariable(Config, "Запрещать отправлять запрос на телепортацию в зоне действия чужого шкафа", out restrictTPRCupboard, true);
            GetVariable(Config, "Отмета телепорта игрока (Home/TP) если у него кровотечение", out CancelTPMetabolism, true);
            GetVariable(Config, "Отмета телепорта игрока (Home/TP) если игрок ранен", out CancelTPWounded, true);
            GetVariable(Config, "Отмета телепорта игрока (Home/TP) если ему холодно", out CancelTPCold, true);
            GetVariable(Config, "Отмета телепорта игрока (Home/TP) если он облучен радиацией", out CancelTPRadiation, true);
            GetVariable(Config, "Ограничение на количество сохранённых местоположений", out homelimitDefault, 3);
            Config["Ограничение на количество сохранённых местоположений с привилегией"] = homelimitPerms = GetConfig("Ограничение на количество сохранённых местоположений с привилегией", new Dictionary<string, object>() {
                    {
                    "teleportation.vip", 5
                }
            }
            ).ToDictionary(p => p.Key, p => Convert.ToInt32(p.Value));
            PermissionService.RegisterPermissions(this, homelimitPerms.Keys.ToList());
            GetVariable(Config, "Длительность задержки перед телепортацией (в секундах)", out teleportSecsDefault, 15);
            Config["Длительность задержки перед телепортацией с привилегией (в секундах)"] = teleportSecsPerms = GetConfig("Длительность задержки перед телепортацией с привилегией (в секундах)", new Dictionary<string, object>() {
                    {
                    "teleportation.vip", 10
                }
            }
            ).ToDictionary(p => p.Key, p => Convert.ToInt32(p.Value));
            PermissionService.RegisterPermissions(this, teleportSecsPerms.Keys.ToList());
            GetVariable(Config, "Длительность перезарядки телепорта (в секундах)", out tpkdDefault, 300);
            Config["Длительность перезарядки телепорта с привилегией (в секундах)"] = tpkdPerms = GetConfig("Длительность перезарядки телепорта с привилегией (в секундах)", new Dictionary<string, object>() {
                    {
                    "teleportation.vip", 150
                }
            }
            ).ToDictionary(p => p.Key, p => Convert.ToInt32(p.Value));
            PermissionService.RegisterPermissions(this, tpkdPerms.Keys.ToList());
            GetVariable(Config, "Длительность перезарядки телепорта домой (в секундах)", out tpkdhomeDefault, 300);
            Config["Длительность перезарядки телепорта домой с привилегией (в секундах)"] = tpkdhomePerms = GetConfig("Длительность перезарядки телепорта домой с привилегией (в секундах)", new Dictionary<string, object>() {
                    {
                    "teleportation.vip", 150
                }
            }
            ).ToDictionary(p => p.Key, p => Convert.ToInt32(p.Value));
            PermissionService.RegisterPermissions(this, tpkdhomePerms.Keys.ToList());
            SaveConfig();
        }
        T GetConfig<T>(string name, T defaultValue) => Config[name] == null ? defaultValue : (T)Convert.ChangeType(Config[name], typeof(T));
        public static void GetVariable<T>(DynamicConfigFile config, string name, out T value, T defaultValue)
        {
            config[name] = value = config[name] == null ? defaultValue : (T)Convert.ChangeType(config[name], typeof(T));
        }
        public static class PermissionService
        {
            public static Permission permission = Interface.GetMod().GetLibrary<Permission>();
            public static bool HasPermission(ulong uid, string permissionName)
            {
                return !string.IsNullOrEmpty(permissionName) && permission.UserHasPermission(uid.ToString(), permissionName);
            }
            public static void RegisterPermissions(Plugin owner, List<string> permissions)
            {
                if (owner == null) throw new ArgumentNullException("owner");
                if (permissions == null) throw new ArgumentNullException("commands");
                foreach (var permissionName in permissions.Where(permissionName => !permission.PermissionExists(permissionName)))
                {
                    permission.RegisterPermission(permissionName, owner);
                }
            }
        }
        public BasePlayer FindBasePlayer(string nameOrUserId)
        {
            nameOrUserId = nameOrUserId.ToLower();
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player.displayName.ToLower().Contains(nameOrUserId) || player.UserIDString == nameOrUserId) return player;
            }
            foreach (var player in BasePlayer.sleepingPlayerList)
            {
                if (player.displayName.ToLower().Contains(nameOrUserId) || player.UserIDString == nameOrUserId) return player;
            }
            return default(BasePlayer);
        }
        private readonly int groundLayer = LayerMask.GetMask("Terrain", "World");
        private readonly int buildingMask = Rust.Layers.Server.Buildings;
        Dictionary<ulong, Dictionary<string, Vector3>> homes;
        Dictionary<ulong, int> cooldownsTP = new Dictionary<ulong, int>();
        Dictionary<ulong, int> cooldownsHOME = new Dictionary<ulong, int>();
        List<TP> tpQueue = new List<TP>();
        List<TP> pendings = new List<TP>();
        List<ulong> sethomeBlock = new List<ulong>();

        [ChatCommand("sethome")]
        void cmdChatSetHome(BasePlayer player, string command, string[] args)
        {
            var pos = player.PivotPoint();
            var foundation = GetFoundation(pos);
            var bulds = GetBuldings(pos);
            if (foundationEx && foundation == null)
            {
                Effect.server.Run(EffectPrefab, player, 0, Vector3.zero, Vector3.forward);
                SendReply(player, "Фундамент не найден!");
                return;
            }
            if (!foundationEx && bulds == null)
            {
                Effect.server.Run(EffectPrefab, player, 0, Vector3.zero, Vector3.forward);
                SendReply(player, "Фундамент не найден!");
                return;
            }
            if (args.Length != 1)
            {
                Effect.server.Run(EffectPrefab, player, 0, Vector3.zero, Vector3.forward);
                SendReply(player, "Используйте <color=#ee3e61>/sethome название дома</color> чтобы сохранить местоположение дома");
                return;
            }
            if (sethomeBlock.Contains(player.userID))
            {
                Effect.server.Run(EffectPrefab, player, 0, Vector3.zero, Vector3.forward);
                SendReply(player, "Нельзя использовать /sethome слишком часто, попробуйте позже!");
                return;
            }
            var name = args[0];
            SetHome(player, name);
        }

        [ChatCommand("removehome")]
        void cmdChatRemoveHome(BasePlayer player, string command, string[] args)
        {
            if (args.Length != 1)
            {
                Effect.server.Run(EffectPrefab, player, 0, Vector3.zero, Vector3.forward);
                SendReply(player, "Используйте <color=#ee3e61>/removehome название дома</color> чтобы удолить местоположение дома");
                return;
            }
            if (!homes.ContainsKey(player.userID))
            {
                Effect.server.Run(EffectPrefab, player, 0, Vector3.zero, Vector3.forward);
                SendReply(player, "Список ваших домов пуст!");
                return;
            }
            var name = args[0];
            var playerHomes = homes[player.userID];
            if (!playerHomes.ContainsKey(name))
            {
                Effect.server.Run(EffectPrefab, player, 0, Vector3.zero, Vector3.forward);
                SendReply(player, "Местоположение с таким названием не найдено!");
                return;
            }
            foreach (var sleepingBag in SleepingBag.FindForPlayer(player.userID, true))
            {
                if (Vector3.Distance(sleepingBag.transform.position, playerHomes[name]) < 1)
                {
                    sleepingBag.Kill();
                    break;
                }
            }
            Effect.server.Run(EffectPrefab1, player, 0, Vector3.zero, Vector3.forward);
            playerHomes.Remove(name);
            SendReply(player, $"Местоположение {name} успешно удалено");
        }
        [ConsoleCommand("home")]
        void ConsoleHome(ConsoleSystem.Arg args)
        {
            var player = args.Player();
            if (player != null && args.HasArgs(1))
            {
                if (args.Args[0] == "tp")
                {
                    cmdChatHome(player, "", new[] { args.Args[1] });
                }
                if (args.Args[0] == "set")
                {
                    var local = GetGridLocation(player);
                    cmdChatSetHome(player, "", new[] { local });
                    TeleportUI(player);
                }
                if (args.Args[0] == "remove")
                {
                    cmdChatRemoveHome(player, "", new[] { args.Args[1] });
                    TeleportUI(player);
                }
            }
        }

        [ConsoleCommand("tpr")]
        void ConsoleTpr(ConsoleSystem.Arg args)
        {
            var player = args.Player();
            cmdChatTpr(player, "", new[] { args.Args[0] });
        }

        [ChatCommand("home")]
        void cmdChatHome(BasePlayer player, string command, string[] args)
        {
            if (args.Length != 1)
            {
                Effect.server.Run(EffectPrefab, player, 0, Vector3.zero, Vector3.forward);
                SendReply(player, "Используйте <color=#ee3e61>/home название дома</color> чтобы телепортироваться домой");
                return;
            }
            if(NoEscape != null)
            {
                var blockTime = (bool) NoEscape?.Call("IsBlocked", player);
                if (blockTime == true)
                {
                    SendReply(player, "Вы не можете использовать команду <color=#ee3e61>/home</color> во время рейда!");
                    return;
                }
            }
            if (!EnabledShipTP && player.GetParentEntity() is CargoShip)
            {
                SendReply(player, "Вы не можете телепортироваться на грузовом корабле.");
                return;
            }
            if (!EnabledBallonTP && player.GetParentEntity() is HotAirBalloon)
            {
                SendReply(player, "Вы не можете телепортироваться на воздушном шаре.");
                return;
            }
            if (player.IsWounded() && CancelTPWounded)
            {
                SendReply(player, "Вы ранены!");
                return;
            }
            if (player.metabolism.bleeding.value > 0 && CancelTPMetabolism)
            {
                SendReply(player, "Вы ранены!");
                return;
            }
            if (CancelTPRadiation && player.radiationLevel > 10)
            {
                SendReply(player, "Вы облучены радиацией!");
                return;
            }
            int seconds;
            if (cooldownsHOME.TryGetValue(player.userID, out seconds) && seconds > 0)
            {
                Effect.server.Run(EffectPrefab, player, 0, Vector3.zero, Vector3.forward);
                SendReply(player, string.Format("Телепортация на перезарядке!\nОсталось <color=#ee3e61>{0}</color>", TimeToString(seconds)));
                return;
            }
            if (homecupboard)
            {
                var privilege = player.GetBuildingPrivilege(player.WorldSpaceBounds());
                if (privilege != null && !player.IsBuildingAuthed())
                {
                    Effect.server.Run(EffectPrefab, player, 0, Vector3.zero, Vector3.forward);
                    SendReply(player, "Телепортация домой в зоне действия чужого шкафа запрещена!");
                    return;
                }
            }
            if (!homes.ContainsKey(player.userID))
            {
                Effect.server.Run(EffectPrefab, player, 0, Vector3.zero, Vector3.forward);
                SendReply(player, "Список ваших домов пуст!");
                return;
            }
            var name = args[0];
            var playerHomes = homes[player.userID];
            if (!playerHomes.ContainsKey(name))
            {
                Effect.server.Run(EffectPrefab, player, 0, Vector3.zero, Vector3.forward);
                SendReply(player, "Местоположение с таким названием не найдено!");
                return;
            }
            var time = GetTeleportTime(player.userID);
            var pos = playerHomes[name];
            SleepingBag bag = GetSleepingBag(name, pos);
            if (createSleepingBug)
            {
                if (bag == null)
                {
                    Effect.server.Run(EffectPrefab, player, 0, Vector3.zero, Vector3.forward);
                    SendReply(player, "Спальный мешок не найден, местоположение удалено!");
                    playerHomes.Remove(name);
                    return;
                }
            }
            if (!createSleepingBug)
            {
                var bulds = GetBuldings(pos);
                if (bulds == null)
                {
                    Effect.server.Run(EffectPrefab, player, 0, Vector3.zero, Vector3.forward);
                    SendReply(player, "Фундамент не найден, местоположение было удалено!");
                    playerHomes.Remove(name);
                    return;
                }
            }
            if (CancelTPCold && player.metabolism.temperature.value < 0)
            {
                Effect.server.Run(EffectPrefab, player, 0, Vector3.zero, Vector3.forward);
                SendReply(player, "Вам холодно!");
                return;
            }
            if (tpQueue.Any(p => p.Player == player) || pendings.Any(p => p.Player2 == player))
            {
                Effect.server.Run(EffectPrefab, player, 0, Vector3.zero, Vector3.forward);
                SendReply(player, "Телепортация запрещена!\n<color=#ee3e61>Причина:</color> Вы в очереди на телепортацию");
                return;
            }
            var lastTp = tpQueue.Find(p => p.Player == player);
            if (lastTp != null)
            {
                tpQueue.Remove(lastTp);
            }
            tpQueue.Add(new TP(player, pos, time, false, null));
            Effect.server.Run(EffectPrefab1, player, 0, Vector3.zero, Vector3.forward);
            SendReply(player, String.Format("Телепортация на дом <color=#ee3e61>{0}</color> будет через <color=#ee3e61>{1}</color>", name, TimeToString(time)));
        }
        [ChatCommand("tpr")]
        void cmdChatTpr(BasePlayer player, string command, string[] args)
        {
            if (!enabledTPR) return;
            if (args.Length != 1)
            {
                Effect.server.Run(EffectPrefab, player, 0, Vector3.zero, Vector3.forward);
                SendReply(player, "Доступные команды:\n<color=#ee3e61>/tpr ник или steam id</color> - отправить запрос на телепорт\n<color=#ee3e61>/tpa</color> - принять запрос на телепорт\n<color=#ee3e61>/tpc</color> - отменить телепортацию");
                return;
            }
            if(NoEscape != null)
            {
                var blockTime = (bool) NoEscape?.Call("IsBlocked", player);
                if (blockTime == true)
                {
                    SendReply(player, "Вы не можете использовать команду <color=#ee3e61>/tpr</color> во время рейда!");
                    return;
                }
            }
            if (!EnabledShipTP && player.GetParentEntity() is CargoShip)
            {
                SendReply(player, "Вы не можете телепортироваться на грузовом корабле.");
                return;
            }
            if (!EnabledBallonTP && player.GetParentEntity() is HotAirBalloon)
            {
                SendReply(player, "Вы не можете телепортироваться на воздушном шаре.");
                return;
            }
            if (player.IsWounded() && CancelTPWounded)
            {
                SendReply(player, "Вы ранены!");
                return;
            }
            if (player.metabolism.bleeding.value > 0 && CancelTPMetabolism)
            {
                SendReply(player, "Вы ранены!");
                return;
            }
            if (CancelTPRadiation && player.radiationLevel > 10)
            {
                Effect.server.Run(EffectPrefab, player, 0, Vector3.zero, Vector3.forward);
                SendReply(player, "Вы облучены радиацией!");
                return;
            }
            if (restrictTPRCupboard)
            {
                var privilege = player.GetBuildingPrivilege(player.WorldSpaceBounds());
                if (privilege != null && !player.IsBuildingAuthed())
                {
                    Effect.server.Run(EffectPrefab, player, 0, Vector3.zero, Vector3.forward);
                    SendReply(player, "Телепортация в зоне действия чужого шкафа запрещена!");
                    return;
                }
            }
            var name = args[0];
            var target = FindBasePlayer(name);
            if (target == null)
            {
                Effect.server.Run(EffectPrefab, player, 0, Vector3.zero, Vector3.forward);
                SendReply(player, "Игрок не найден");
                return;
            }
            if (target == player)
            {
                SendReply(player, "Нельзя отправлять запрос на телепорт самому себе!");
                return;
            }
            if(NoEscape != null)
            {
                var time = (bool) NoEscape?.Call("IsBlocked", target);
                if (time == true)
                {
                    SendReply(player, "У вашего тимейта рейд блок!");
                    return;
                }
            }
            int seconds = 0;
            if (restrictCupboard && player.GetBuildingPrivilege(player.WorldSpaceBounds()) != null && !player.GetBuildingPrivilege(player.WorldSpaceBounds()).authorizedPlayers.Select(p => p.userid).Contains(player.userID))
            {
                Effect.server.Run(EffectPrefab, player, 0, Vector3.zero, Vector3.forward);
                SendReply(player, "Телепортация в зоне действия чужого шкафа запрещена!");
                return;
            }

            if (cooldownsTP.TryGetValue(player.userID, out seconds) && seconds > 0)
            {
                Effect.server.Run(EffectPrefab, player, 0, Vector3.zero, Vector3.forward);
                SendReply(player, string.Format("Телепортация на перезарядке!\nОсталось <color=#ee3e61>{0}</color>", TimeToString(seconds)));
                return;
            }
            if (CancelTPCold && player.metabolism.temperature.value < 0)
            {
                Effect.server.Run(EffectPrefab, player, 0, Vector3.zero, Vector3.forward);
                SendReply(player, "Вам холодно!");
                return;
            }
            if (tpQueue.Any(p => p.Player == player) || pendings.Any(p => p.Player2 == player))
            {
                Effect.server.Run(EffectPrefab, player, 0, Vector3.zero, Vector3.forward);
                SendReply(player, "Телепортация запрещена!\n<color=#ee3e61>Причина:</color> Вы в очереди на телепортацию");
                return;
            }

            if (tpQueue.Any(p => p.Player == target) || pendings.Any(p => p.Player2 == target))
            {
                Effect.server.Run(EffectPrefab, player, 0, Vector3.zero, Vector3.forward);
                SendReply(player, "Телепортация запрещена!\n<color=#ee3e61>Причина:</color> Вы в очереди на телепортацию");
                return;
            }
            SendReply(player, string.Format("Запрос игроку <color=#ee3e61>{0}</color> успешно отправлен", target.displayName));
            SendReply(target, string.Format("Игрок <color=#ee3e61>{0}</color> отправил вам запрос на телепортацию\nИспользуйте <color=#ee3e61>/tpa</color> чтобы принять\nИспользуйте <color=#ee3e61>/tpc</color> чтобы отказаться", player.displayName));
            Effect.server.Run(EffectPrefab1, target, 0, Vector3.zero, Vector3.forward);

            pendings.Add(new TP(target, Vector3.zero, 15, false, player));
        }

        [ChatCommand("tpa")]
        void cmdChatTpa(BasePlayer player, string command, string[] args)
        {
            if (!enabledTPR) return;
            var tp = pendings.Find(p => p.Player == player);
            if (tp == null)
            {
                Effect.server.Run(EffectPrefab, player, 0, Vector3.zero, Vector3.forward);
                SendReply(player, "У вас нет активных запросов на телепортацию!");
                return;
            }
            BasePlayer pendingPlayer = tp.Player2;
            if (pendingPlayer == null)
            {
                Effect.server.Run(EffectPrefab, player, 0, Vector3.zero, Vector3.forward);
                SendReply(player, "У вас нет активных запросов на телепортацию!");
                return;
            }
            if (!EnabledBallonTP && player.GetParentEntity() is HotAirBalloon)
            {
                SendReply(player, "Вы не можете телепортироваться на воздушном шаре.");
                return;
            }
            if (!EnabledShipTP && player.GetParentEntity() is CargoShip)
            {
                SendReply(player, "Вы не можете телепортироваться на грузовом корабле.");
                return;
            }
            if (CancelTPCold && player.metabolism.temperature.value < 0)
            {
                Effect.server.Run(EffectPrefab, player, 0, Vector3.zero, Vector3.forward);
                SendReply(player,"Вам холодно!");
                return;
            }
            if (player.IsWounded() && CancelTPWounded)
            {
                SendReply(player, "Вы ранены!");
                return;
            }
            if (player.metabolism.bleeding.value > 0 && CancelTPMetabolism)
            {
                SendReply(player, "Вы ранены!");
                return;
            }
            if (CancelTPRadiation && player.radiationLevel > 10)
            {
                Effect.server.Run(EffectPrefab, player, 0, Vector3.zero, Vector3.forward);
                SendReply(player, "Вы облучены радиацией!");
                return;
            }
            if (restrictCupboard && player.GetBuildingPrivilege(player.WorldSpaceBounds()) != null && !player.GetBuildingPrivilege(player.WorldSpaceBounds()).authorizedPlayers.Select(p => p.userid).Contains(player.userID))
            {
                Effect.server.Run(EffectPrefab, player, 0, Vector3.zero, Vector3.forward);
                SendReply(player, "Принятие телепортации в зоне действия чужого шкафа запрещена!");
                return;
            }
            var ret = Interface.Call("CanTeleport", player) as string;
            if (ret != null)
            {
                SendReply(player, ret);
                return;
            }
            var time = GetTeleportTime(pendingPlayer.userID);
            pendings.Remove(tp);
            var lastTp = tpQueue.Find(p => p.Player == pendingPlayer);
            if (lastTp != null)
            {
                tpQueue.Remove(lastTp);
            }
            var Enabled = player.GetParentEntity() is CargoShip || player.GetParentEntity() is HotAirBalloon;
            tpQueue.Add(new TP(pendingPlayer, player.transform.position, time, Enabled, player));
            Effect.server.Run(EffectPrefab1, player, 0, Vector3.zero, Vector3.forward);
            SendReply(pendingPlayer, string.Format("Игрок <color=#ee3e61>{0}</color> принял ваш запрос на телепортацию\nВы будете телепортированы через <color=#ee3e61>{1}</color>", player.displayName, TimeToString(time)));
            if (args.Length <= 0) SendReply(player, String.Format("Вы приняли запрос телепортации от игрока <color=#ee3e61>{0}</color>\nОн будет телепортирован через <color=#ee3e61>{1}</color>", pendingPlayer.displayName, TimeToString(time)));
        }
        [ChatCommand("tpc")]
        void cmdChatTpc(BasePlayer player, string command, string[] args)
        {
            var tp = pendings.Find(p => p.Player == player);
            BasePlayer target = tp?.Player2;
            if (target != null)
            {
                pendings.Remove(tp);
                SendReply(player, "Телепортация успешно отменена!");
                SendReply(target, string.Format("Игрок <color=#ee3e61>{0}</color> отменил телепортацию!", player.displayName));
                return;
            }
            foreach (var pend in pendings)
            {
                if (pend.Player2 == player)
                {
                    SendReply(player, "Телепортация успешно отменена!");
                    SendReply(pend.Player, string.Format("Игрок <color=#ee3e61>{0}</color> отменил телепортацию!", player.displayName));
                    pendings.Remove(pend);
                    return;
                }
            }
            foreach (var tpQ in tpQueue)
            {
                if (tpQ.Player2 != null && tpQ.Player2 == player)
                {
                    SendReply(player, "Телепортация успешно отменена!");
                    SendReply(tpQ.Player, string.Format("Игрок <color=#ee3e61>{0}</color> отменил телепортацию!", player.displayName));
                    tpQueue.Remove(tpQ);
                    return;
                }
                if (tpQ.Player == player)
                {
                    SendReply(player, "Телепортация успешно отменена!");
                    if (tpQ.Player2 != null) SendReply(tpQ.Player2, string.Format("Игрок <color=#ee3e61>{0}</color> отменил телепортацию!", player.displayName));
                    tpQueue.Remove(tpQ);
                    return;
                }
            }
        }
        void SpectateFinish(BasePlayer player)
        {
            player.Command("camoffset", "0,1,0");
            player.StopSpectating();
            player.SetParent(null);
            player.gameObject.SetLayerRecursive(17);
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Spectating, false);
            player.SetPlayerFlag(BasePlayer.PlayerFlags.ThirdPersonViewmode, false);
            player.SendNetworkUpdateImmediate();
            player.metabolism.Reset();
            player.InvokeRepeating("InventoryUpdate", 1f, 0.1f * UnityEngine.Random.Range(0.99f, 1.01f));
            if (player.net?.connection != null) player.ClientRPCPlayer(null, player, "StartLoading");
            player.StartSleeping();
            if (lastPositions.ContainsKey(player.userID))
            {
                Vector3 lastPosition = lastPositions[player.userID] + Vector3.up;
                player.MovePosition(lastPosition);
                if (player.net?.connection != null) player.ClientRPCPlayer(null, player, "ForcePositionTo", lastPosition);
                lastPositions.Remove(player.userID);

            }

            if (player.net?.connection != null) player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
            player.UpdateNetworkGroup();
            player.SendNetworkUpdateImmediate(false);
            if (player.net?.connection == null) return;
            try
            {
                player.ClearEntityQueue(null);
            }
            catch { }
            player.SendFullSnapshot();

            SendReply(player, "Слежка закончена!");
        }


        private void OnUserConnected(IPlayer player) => ResetSpectate(player);

        private void OnUserDisconnected(IPlayer player) => ResetSpectate(player);

        private void ResetSpectate(IPlayer player)
        {
            player.Command("camoffset 0,1,0");

            if (lastPositions.ContainsKey(ulong.Parse(player.Id)))
            {
                lastPositions.Remove(ulong.Parse(player.Id));
            }
        }

        object OnPlayerSpectateEnd(BasePlayer player, string spectateFilter)
        {
            player.Command("camoffset", "0,1,0");
            return null;
        }

        [ChatCommand("tpspec")]
        void cmdTPSpec(BasePlayer player, string command, string[] args)
        {
            if (!PermissionService.HasPermission(player.userID, "teleportation.spec")) return;
            if (!player.IsSpectating())
            {
                if (args.Length == 0 || args.Length != 1)
                {
                    SendReply(player, "Не правильно введена команда. Используйте: /tpspec ник игрока");
                    return;
                }
                string name = args[0];
                BasePlayer target = FindBasePlayer(name);
                if (target == null)
                {
                    Effect.server.Run(EffectPrefab, player, 0, Vector3.zero, Vector3.forward);
                    SendReply(player, "Игрок не найден");
                    return;
                }
                switch (args.Length)
                {
                    case 1:
                        if (!target.IsConnected)
                        {
                            SendReply(player, "Игрок не в сети");
                            return;
                        }
                        if (target.IsDead())
                        {
                            SendReply(player, "Игрок не найден, или он мёртв");
                            return;
                        }
                        if (ReferenceEquals(target, player))
                        {
                            SendReply(player, "Нельзя следить за самым собой");
                            return;
                        }
                        if (target.IsSpectating())
                        {
                            SendReply(player, "Игрок уже за кем то наблюдает");
                            return;
                        }
                        spectatingPlayers.Remove(target);
                        lastPositions[player.userID] = player.transform.position;
                        HeldEntity heldEntity = player.GetActiveItem()?.GetHeldEntity() as HeldEntity;
                        heldEntity?.SetHeld(false);
                        player.SetPlayerFlag(BasePlayer.PlayerFlags.Spectating, true);
                        player.gameObject.SetLayerRecursive(10);
                        player.CancelInvoke("MetabolismUpdate");
                        player.CancelInvoke("InventoryUpdate");
                        player.ClearEntityQueue();
                        player.SendEntitySnapshot(target);
                        player.gameObject.Identity();
                        player.SetParent(target);
                        player.SetPlayerFlag(BasePlayer.PlayerFlags.ThirdPersonViewmode, true);
                        player.Command("camoffset", "0, 1.3, 0");
                        SendReply(player, $"Вы наблюдаете за игроком {target}! Что бы переключаться между игроками, нажимайте: Пробел\nЧтобы выйти с режима наблюдения, введите: /tpspec");
                        break;
                }
            }
            else SpectateFinish(player);
        }
        [ChatCommand("tp")]
        void cmdTP(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin && !PermissionService.HasPermission(player.userID, "teleportation.admin")) return;
            switch (args.Length)
            {
                case 1:
                    string name = args[0];
                    BasePlayer target = FindBasePlayer(name);
                    if (target == null)
                    {
                        Effect.server.Run(EffectPrefab, player, 0, Vector3.zero, Vector3.forward);
                        SendReply(player, "Игрок не найден");
                        return;
                    }
                    if (adminsLogs)
                    {
                        LogToFile("admin", $"[{DateTime.Now.ToShortTimeString()}] {player} телепортировался к {target}", this, true);
                    }
                    Teleport(player, target);
                    break;
                case 2:
                    string name1 = args[0];
                    string name2 = args[1];
                    BasePlayer target1 = FindBasePlayer(name1);
                    BasePlayer target2 = FindBasePlayer(name2);
                    if (target1 == null || target2 == null)
                    {
                        SendReply(player, "Игрок не найден");
                        return;
                    }
                    if (adminsLogs)
                    {
                        LogToFile("admin", $"[{DateTime.Now.ToShortTimeString()}] Игрок {player} телепортировал {target1} к {target2}", this, true);
                    }
                    Teleport(target1, target2);
                    break;
                case 3:
                    float x = float.Parse(args[0].Replace(",", ""));
                    float y = float.Parse(args[1].Replace(",", ""));
                    float z = float.Parse(args[2]);
                    if (adminsLogs)
                    {
                        LogToFile("admin", $"[{DateTime.Now.ToShortTimeString()}] Игрок {player} телепортировался на координаты: ({x} / {y} / {z})", this, true);
                    }
                    Teleport(player, x, y, z);
                    break;
            }
        }
        [ConsoleCommand("home.wipe")]
        private void CmdTest(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null) return;
            PrintWarning("Запущен ручной вайп. Очищаем данные с data/Teleportation");
            WipeData();
        }
        public string TimeToString(double time)
        {
            TimeSpan elapsedTime = TimeSpan.FromSeconds(time);
            int hours = elapsedTime.Hours;
            int minutes = elapsedTime.Minutes;
            int seconds = elapsedTime.Seconds;
            int days = Mathf.FloorToInt((float)elapsedTime.TotalDays);
            string s = "";
            if (days > 0) s += $"{days} дн.";
            if (hours > 0) s += $"{hours} ч. ";
            if (minutes > 0) s += $"{minutes} мин. ";
            if (seconds > 0) s += $"{seconds} сек.";
            else s = s.TrimEnd(' ');
            return s;
        }
        void OnPlayerDisconnected(BasePlayer player)
        {
            pendings.RemoveAll(p => p.Player == player || p.Player2 == player);
            tpQueue.RemoveAll(p => p.Player == player || p.Player2 == player);
        }
        void OnServerInitialized()
        {
            LoadData();
            LoadDefaultConfig();
            PermissionService.RegisterPermissions(this, new List<string>() { TPADMIN, SpecPerm });
            timer.Every(1f, TeleportationTimerHandle);
            timer.Every(300, SaveData);

            ImageLibrary?.Call("AddImage", "https://imgur.com/bqWYNvb.png", "ImageHome");
            ImageLibrary?.Call("AddImage", "https://imgur.com/CC9gXGx.png", "ImageDrug");
        }
        void Unload() => SaveData();
        void OnEntityBuilt(Planner planner, GameObject gameobject)
        {
            if (planner == null || gameobject == null) return;
            var player = planner.GetOwnerPlayer();
            BaseEntity entity = gameobject.ToBaseEntity();
            if (entity == null) return;
            if (gameobject.name.Contains("foundation"))
            {
                var pos = gameobject.transform.position;
                foreach (var pending in tpQueue)
                {
                    if (Vector3.Distance(pending.pos, pos) < 3)
                    {
                        entity.Kill();
                        SendReply(planner.GetOwnerPlayer(), "Нельзя, тут телепортируется игрок!");
                        return;
                    }
                }
            }
        }
        void TeleportationTimerHandle()
        {
            List<ulong> tpkdToRemove = new List<ulong>();
            foreach (var uid in cooldownsTP.Keys.ToList())
            {
                if (--cooldownsTP[uid] <= 0)
                {
                    tpkdToRemove.Add(uid);
                }
            }
            tpkdToRemove.ForEach(p => cooldownsTP.Remove(p));
            List<ulong> tpkdHomeToRemove = new List<ulong>();
            foreach (var uid in cooldownsHOME.Keys.ToList())
            {
                if (--cooldownsHOME[uid] <= 0) tpkdHomeToRemove.Add(uid);
            }
            tpkdHomeToRemove.ForEach(p => cooldownsHOME.Remove(p));
            for (int i = pendings.Count - 1;
            i >= 0;
            i--)
            {
                var pend = pendings[i];
                if (pend.Player != null && pend.Player.IsConnected && pend.Player.IsWounded())
                {
                    SendReply(pend.Player, "Телепортация отменена!\n<color=#ee3e61>Причина:</color> Вы получили ранение!");
                    pendings.RemoveAt(i);
                    continue;
                }
                if (--pend.seconds <= 0)
                {
                    pendings.RemoveAt(i);
                    if (pend.Player2 != null && pend.Player2.IsConnected) SendReply(pend.Player2, "Запрос телепортации отменён");
                    if (pend.Player != null && pend.Player.IsConnected) SendReply(pend.Player, "Вы не ответили на запрос.");
                }
            }
            for (int i = tpQueue.Count - 1;
            i >= 0;
            i--)
            {
                var reply = 1;
                if (reply == 0) { }
                var tp = tpQueue[i];
                if (tp.Player != null)
                {
                    if (tp.Player.IsConnected && (CancelTPWounded && tp.Player.IsWounded()) || (tp.Player.metabolism.bleeding.value > 0 && CancelTPMetabolism) || (CancelTPRadiation && tp.Player.radiationLevel > 10))
                    {
                        SendReply(tp.Player, "Телепортация отменена!\n<color=#ee3e61>Причина:</color> Вы получили ранение!");
                        if (tp.Player2 != null && tp.Player2.IsConnected) SendReply(tp.Player2, "Игрок ранен. Телепортация отменена!");
                        tpQueue.RemoveAt(i);
                        continue;
                    }
                    if (restrictTPRCupboard)
                    {
                        var privilege = tp.Player.GetBuildingPrivilege(tp.Player.WorldSpaceBounds());
                        if (privilege != null && !tp.Player.IsBuildingAuthed())
                        {
                            Effect.server.Run(EffectPrefab, tp.Player, 0, Vector3.zero, Vector3.forward);

                            SendReply(tp.Player, "Телепортация в зоне действия чужого шкафа запрещена!");
                            if (tp.Player2 != null && tp.Player2.IsConnected) SendReply(tp.Player2, "Вы или игрок находитесь в зоне действия чужого шкафа!");
                            tpQueue.RemoveAt(i);
                            return;
                        }
                    }
                }

                if (tp.Player2 != null)
                {
                    if (tp.Player2.IsConnected && (tp.Player2.IsWounded() && CancelTPWounded) || (tp.Player2.metabolism.bleeding.value > 0 && CancelTPMetabolism) || (CancelTPRadiation && tp.Player2.radiationLevel > 10))
                    {
                        SendReply(tp.Player2, "Телепортация отменена!\n<color=#ee3e61>Причина:</color> Вы получили ранение!");
                        if (tp.Player != null && tp.Player.IsConnected) SendReply(tp.Player, "Игрок ранен. Телепортация отменена!");
                        tpQueue.RemoveAt(i);
                        continue;
                    }
                    if (restrictTPRCupboard)
                    {
                        var privilege = tp.Player2.GetBuildingPrivilege(tp.Player2.WorldSpaceBounds());
                        if (privilege != null && !tp.Player2.IsBuildingAuthed())
                        {
                            Effect.server.Run(EffectPrefab, tp.Player2, 0, Vector3.zero, Vector3.forward);
                            if (tp.Player != null && tp.Player.IsConnected) SendReply(tp.Player, "Вы или игрок находитесь в зоне действия чужого шкафа!");

                            SendReply(tp.Player2, "Телепортация в зоне действия чужого шкафа запрещена!");
                            return;
                        }
                    }
                }
                if (--tp.seconds <= 0)
                {
                    tpQueue.RemoveAt(i);
                    var ret = Interface.CallHook("CanTeleport", tp.Player) as string;
                    if (ret != null)
                    {
                        SendReply(tp.Player, ret);
                        continue;
                    }
                    if (CheckInsideInFoundation(tp.pos))
                    {
                        SendReply(tp.Player, "Телепортация запрещена, местоположение находится в фундаменте");
                        continue;
                    }
                    if (tp.Player2 != null)
                    {
                        tp.Player.SetParent(tp.Player2.GetParentEntity());
                        if (tp.EnabledShip) tp.pos = tp.Player2.transform.position;
                    }
                    if (tp.Player2 != null && tp.Player != null && tp.Player.IsConnected && tp.Player2.IsConnected)
                    {
                        var seconds = GetKD(tp.Player.userID);
                        cooldownsTP[tp.Player.userID] = seconds;
                        SendReply(tp.Player, string.Format("Вы успешно телепортировались к игроку <color=#ee3e61>{0}</color>", tp.Player2.displayName));
                    }
                    else if (tp.Player != null && tp.Player.IsConnected)
                    {
                        tp.Player.SetParent(null);
                        var seconds = GetKDHome(tp.Player.userID);
                        cooldownsHOME[tp.Player.userID] = seconds;
                        SendReply(tp.Player, "Вы успешно телепортированы домой!");
                    }
                    Teleport(tp.Player, tp.pos);
                    NextTick(() => Interface.CallHook("OnPlayerTeleported", tp.Player));
                }
            }
        }
        void SetHome(BasePlayer player, string name)
        {
            var uid = player.userID;
            var pos = player.transform.position;
            if (player.GetBuildingPrivilege(player.WorldSpaceBounds()) != null && !player.GetBuildingPrivilege(player.WorldSpaceBounds()).authorizedPlayers.Select(p => p.userid).Contains(player.userID))
            {
                Effect.server.Run(EffectPrefab, player, 0, Vector3.zero, Vector3.forward);
                SendReply(player, "Установка местоположения в зоне действия чужого шкафа запрещена!");
                return;
            }
            Dictionary<string, Vector3> playerHomes;
            if (!homes.TryGetValue(uid, out playerHomes)) playerHomes = (homes[uid] = new Dictionary<string, Vector3>());
            if (GetHomeLimit(uid) == playerHomes.Count)
            {
                Effect.server.Run(EffectPrefab, player, 0, Vector3.zero, Vector3.forward);
                SendReply(player, "У вас максимальное кол-во местоположений!");
                return;
            }
            if (playerHomes.ContainsKey(name))
            {
                Effect.server.Run(EffectPrefab, player, 0, Vector3.zero, Vector3.forward);
                SendReply(player, "Такое местоположение уже существует!");
                return;
            }
            if (CheckInsideInFoundation(player.transform.position))
            {
                SendReply(player, "Вы не можете устанавливать местоположение находясь в фундаменте");
                return;
            }
            playerHomes.Add(name, pos);
            if (createSleepingBug)
            {
                CreateSleepingBag(player, pos, name);
            }
            Effect.server.Run(EffectPrefab1, player, 0, Vector3.zero, Vector3.forward);
            SendReply(player, $"Местоположение {name} успешно установлено!");
            sethomeBlock.Add(player.userID);
            timer.Once(10f, () => sethomeBlock.Remove(player.userID));
        }
        private bool CheckInsideInFoundation(Vector3 position)
        {
            foreach (var hit in Physics.RaycastAll(position, Vector3.up, 2f, LayerMask.GetMask("Terrain", "World", "Construction", "Deployed")))
            {
                if (hit.GetCollider().name.Contains("foundation")) return true;
            }
            foreach (var hit in Physics.RaycastAll(position + Vector3.up + Vector3.up + Vector3.up + Vector3.up, Vector3.down, 2f, LayerMask.GetMask("Terrain", "World", "Construction", "Deployed")))
            {
                if (hit.GetCollider().name.Contains("foundation")) return true;
            }
            return false;
        }
        int GetKDHome(ulong uid)
        {
            int min = tpkdhomeDefault;
            foreach (var privilege in tpkdhomePerms) if (PermissionService.HasPermission(uid, privilege.Key)) min = Mathf.Min(min, privilege.Value);
            return min;
        }
        int GetKD(ulong uid)
        {
            int min = tpkdDefault;
            foreach (var privilege in tpkdPerms) if (PermissionService.HasPermission(uid, privilege.Key)) min = Mathf.Min(min, privilege.Value);
            return min;
        }
        int GetHomeLimit(ulong uid)
        {
            int max = homelimitDefault;
            foreach (var privilege in homelimitPerms) if (PermissionService.HasPermission(uid, privilege.Key)) max = Mathf.Max(max, privilege.Value);
            return max;
        }
        int GetTeleportTime(ulong uid)
        {
            int min = teleportSecsDefault;
            foreach (var privilege in teleportSecsPerms) if (PermissionService.HasPermission(uid, privilege.Key)) min = Mathf.Min(min, privilege.Value);
            return min;
        }
        BaseEntity GetBuldings(Vector3 pos)
        {
            RaycastHit hit;
            if (Physics.Raycast(new Ray(pos, Vector3.down), out hit, 0.2f))
            {
                var entity = hit.GetEntity();
                if (entity != null) return entity;
                else return null;
            }
            return null;
        }
        private BaseEntity GetFoundation(Vector3 pos)
        {
            RaycastHit hit;
            if (Physics.Raycast(pos, Vector3.down, out hit, LayerMask.GetMask("Terrain", "World", "Construction", "Deployed")))
            {
                var entity = hit.GetEntity();
                if (entity != null) if (entity.PrefabName.Contains("foundation")) return entity;
            }
            return null;
        }
        SleepingBag GetSleepingBag(string name, Vector3 pos)
        {
            List<SleepingBag> sleepingBags = new List<SleepingBag>();
            Vis.Components(pos, .1f, sleepingBags);
            return sleepingBags.Count > 0 ? sleepingBags[0] : null;
        }
        void CreateSleepingBag(BasePlayer player, Vector3 pos, string name)
        {
            SleepingBag sleepingBag = GameManager.server.CreateEntity("assets/prefabs/deployable/sleeping bag/sleepingbag_leather_deployed.prefab", pos, Quaternion.identity) as SleepingBag;
            if (sleepingBag == null) return;
            sleepingBag.skinID = 1323108630;
            sleepingBag.deployerUserID = player.userID;
            sleepingBag.niceName = name;
            sleepingBag.OwnerID = player.userID;
            sleepingBag.Spawn();
            sleepingBag.SendNetworkUpdate();
        }
        Dictionary<string, Vector3> GetHomes(ulong uid)
        {
            Dictionary<string, Vector3> positions;
            if (!homes.TryGetValue(uid, out positions)) return null;
            return positions.ToDictionary(p => p.Key, p => p.Value);
        }
        public void Teleport(BasePlayer player, BasePlayer target) => Teleport(player, target.transform.position);
        public void Teleport(BasePlayer player, float x, float y, float z) => Teleport(player, new Vector3(x, y, z));
        public void Teleport(BasePlayer player, Vector3 position)
        {
            if (player.IsDead() && player.IsConnected)
            {
                player.RespawnAt(position, Quaternion.identity);
                return;
            }
            var ret = Interface.Call("CanTeleport", player) as string;
            if (ret != null)
            {
                SendReply(player, ret);
                return;
            }
            BaseMountable mount = player.GetMounted();
            if (mount != null) mount.DismountPlayer(player);
            if (player.net?.connection != null) player.ClientRPCPlayer(null, player, "StartLoading");
            player.StartSleeping();
            player.MovePosition(position);
            if (player.net?.connection != null) player.ClientRPCPlayer(null, player, "ForcePositionTo", position);
            if (player.net?.connection != null) player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
            player.UpdateNetworkGroup();
            player.SendNetworkUpdateImmediate(false);
            if (player.net?.connection == null) return;
            try
            {
                player.ClearEntityQueue(null);
            }
            catch { }
            player.SendFullSnapshot();
        }
        DynamicConfigFile homesFile = Interface.Oxide.DataFileSystem.GetFile("Teleportation/Homes");
        DynamicConfigFile tpsaveFile = Interface.Oxide.DataFileSystem.GetFile("Teleportation/AdminTpSave");

        void LoadData()
        {
            try
            {
                homes = homesFile.ReadObject<Dictionary<ulong, Dictionary<string, Vector3>>>();
                if (homes == null)
                {
                    homes = new Dictionary<ulong, Dictionary<string, Vector3>>();
                }

            }
            catch
            {
                homes = new Dictionary<ulong, Dictionary<string, Vector3>>();
            }
        }
        void SaveData()
        {
            if (homes != null) homesFile.WriteObject(homes);

        }

        void TeleportUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "Tp");
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.09 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, "Menu", "Tp");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.04 0.855", AnchorMax = "0.96 1", OffsetMax = "0 0" },
                Text = { Text = $"<size=25><b>Система телепортации</b></size>\nЗдесь вы можете создать точку телепортации домой и к другу.", Color = "1 1 1 0.5", Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "Tp");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.05 0.46", AnchorMax = "0.33 0.85", OffsetMax = "0 0" },
                Button = { Color = "0.5 0 0 0.5", Command = "chat.say /tpc" },
                Text = { Text = "<b><size=14>ОТМЕНИТЬ</size></b>\nТЕЛЕПОРТАЦИЮ", Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 10, Font = "robotocondensed-regular.ttf" }
            }, "Tp");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.358 0.46", AnchorMax = "0.635 0.85", OffsetMax = "0 0" },
                Button = { Color = "0.5 0 0 0.5", Command = "chat.say /tpa" },
                Text = { Text = "<b><size=14>ПРИНЯТЬ</size></b>\nТЕЛЕПОРТАЦИЮ", Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 10, Font = "robotocondensed-regular.ttf" }
            }, "Tp");

            RelationshipManager.PlayerTeam playerTeam = RelationshipManager.ServerInstance.FindTeam(player.currentTeam);
            if (player.currentTeam != 0 && playerTeam.members.Count >= 2)
            {
                for (int z = 0; z < playerTeam.members.Count(); z++)
                {
                    BasePlayer friend = FindBasePlayer(playerTeam.members.ElementAt(z).ToString());
                    if (friend.displayName != player.displayName)
                    {
                        string name = friend.displayName;
                        if (name.ToLower().Contains("#Sectrust".ToLower()))
					        name = RemoveBadWord(name, "#Sectrust".ToLower());
                        int hp = (int)friend.health;
                        int seconds = 0;

                        var text = cooldownsTP.TryGetValue(player.userID, out seconds) && seconds > 0 ? $"<b><size=12>{name}</size></b>\nПодождите: {TimeToString(seconds)}" : $"<b><size=12>{name}</size></b>\nХп друга: {hp}";
                        container.Add(new CuiButton
                        {
                            RectTransform = { AnchorMin = "0.664 0.46", AnchorMax = "0.94 0.85", OffsetMax = "0 0" },
                            Button = { Color = "0.5 0 0 0.5", Command = $"tpr {name}" },
                            Text = { Text = text, Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 10, Font = "robotocondensed-regular.ttf" }
                        }, "Tp");
                    }
                }
            }

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.05 0.04", AnchorMax = "0.33 0.437", OffsetMax = "0 0" },
                Button = { Color = "0.5 0 0 0.5", Command = "home set" },
                Text = { Text = "<b><size=12>СОЗДАТЬ</size></b>\nТОЧКУ ДОМА", Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 8, Font = "robotocondensed-regular.ttf" }
            }, "Tp");

            float width = 0.305f, height = 0.407f, startxBox = 0.345f, startyBox = 0.442f - height, xmin = startxBox, ymin = startyBox;
            if (homes.ContainsKey(player.userID))
            {
                foreach (var check in homes[player.userID])
                {
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = xmin + " " + ymin, AnchorMax = (xmin + width) + " " + (ymin + height * 1), OffsetMin = "6 2", OffsetMax = "-6 -2" },
                        Button = { Color = "0.10 0.13 0.19 0" },
                        Text = { Text = $"" }
                    }, "Tp", "Settings");

                    int sec = 0;
                    var time = cooldownsHOME.TryGetValue(player.userID, out sec) && sec > 0 ? $"\n\n\n\n\n<b><size=12>ДОМ</size></b>\nКвадрат: {check.Key}\nПодождите: {TimeToString(sec)}" : $"\n\n\n\n\n<b><size=12>ДОМ</size></b>\nКвадрат: {check.Key}";
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 0.15", AnchorMax = "1 1", OffsetMax = "0 0" },
                        Button = { Color = "0.5 0 0 0.5", Command = $"home tp {check.Key}" },
                        Text = { Text = time, Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 10, Font = "robotocondensed-regular.ttf" }
                    }, "Settings");

                    container.Add(new CuiElement
                    {
                        Parent = "Settings",
                        Components =
                        {
                            new CuiImageComponent { Sprite = "assets/icons/light_campfire.png", Color = "1 1 1 0.8", FadeIn = 0.5f },
                            new CuiRectTransformComponent { AnchorMin = "0 0.25", AnchorMax = "1 0.97", OffsetMin = "40 40", OffsetMax = "-40 -40" }
                        }
                    });

                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.15", OffsetMax = "0 0" },
                        Button = { Color = "0.93 0.24 0.38 0.8", Command = $"home remove {check.Key}" },
                        Text = { Text = $"УДАЛИТЬ", Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 10, Font = "robotocondensed-bold.ttf" }
                    }, "Settings");

                    xmin += width;
                    if (xmin + width >= 1)
                    {
                        xmin = startxBox;
                        ymin -= height;
                    }
                }
            }

            CuiHelper.AddUi(player, container);
        }

        private static string RemoveBadWord(string oldStr, string word)
		{
			word = word.ToLower();
			var oldStrLow = oldStr.ToLower();
			var result = "";
			int num = 0;
			
			for (int ii = 0; ii < oldStr.Length; ii++)
			{
				var ch = oldStr[ii];
				var foundWord = true;
				
				for (int jj = 0; jj < word.Length; jj++)
				{
					if (ii+jj >= oldStrLow.Length || word[jj] != oldStrLow[ii+jj])
					{
						foundWord = false;
						break;
					}
				}
				
				if (foundWord)
					num += word.Length;
				
				if (ii == num)
				{
					result += ch;
					num++;
				}
			}
			
			return result;
		}

        string GetGridLocation(BasePlayer player)
        {
            string gridLocation = "";
            int numx = Convert.ToInt32(player.transform.position.x);
            int numz = Convert.ToInt32(player.transform.position.z);

            float offset = (ConVar.Server.worldsize) / 2;
            float step = (ConVar.Server.worldsize) / (0.0066666666666667f * (ConVar.Server.worldsize));
            string start = "";

            int diff = Convert.ToInt32(step);
            int absoluteDifference = diff;

            char letter = 'A';
            int number = 0;
            for (float xx = -offset; xx < offset; xx += step)
            {
                for (float zz = offset; zz > -offset; zz -= step)
                {
                    if (Math.Abs(numx - xx) <= diff && Math.Abs(numz - zz) <= diff)
                    {
                        gridLocation = $"{start}{letter}{number}";
                        break;
                    }
                    number++;
                }
                number = 0;
                if (letter.ToString().ToUpper() == "Z")
                {
                    start = "A";
                    letter = 'A';
                }
                else
                {
                    letter = (char)(((int)letter) + 1);
                }
                if (Math.Abs(numx - xx) <= diff)
                {
                    break;
                }
            }
            return gridLocation;
        }
    }
}                                                                                                                              