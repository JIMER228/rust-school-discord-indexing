using System;
using System.Collections.Generic;
using System.Linq;
using ConVar;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
    [Info("PrisonBitch", "fermens", "0.0.61")]
    public class PrisonBitch : RustPlugin
    {
        #region Config
        private static PluginConfig config;

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

        class wear
        {
            [JsonProperty("Название")]
            public string name;

            [JsonProperty("Скин")]
            public ulong skin;
        }

        class belt
        {
            [JsonProperty("Название")]
            public string name;

            [JsonProperty("Скин")]
            public ulong skin;

            [JsonProperty("Количество")]
            public int amount;
        }

        private class PluginConfig
        {
            [JsonProperty("Одежда заключеного")]
            public wear[] clothes;

            [JsonProperty("Название постройки в CopyPaste")]
            public string name;

            [JsonProperty("Позиции для спавна (не трогать!)")]
            public List<string> positions;

            [JsonProperty("Координаты для постройки тюрьмы")]
            public string position;

            [JsonProperty("Запретить писать в общий чат")]
            public bool blockchat;

            [JsonProperty("Разрешенные команды")]
            public string[] whitelist;

            [JsonProperty("Вещи в поясе")]
            public belt[] belts;

            [JsonProperty("Сообщения")]
            public Dictionary<string, string> messages;

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    clothes = new wear[]
                    {
                        new wear{ name = "jacket.snow", skin = 673718814 },
                        new wear{ name = "burlap.trousers", skin = 681294648 }
                    },
                    blockchat = true,
                    name = "prisonbitch",
                    positions = new List<string>(),
                    position = "(-1000.0, 800.0, 1000.0)",
                    whitelist = new string[] { "report", "kill", "respawn", "inventory.endloot" },
                    messages = new Dictionary<string, string>
                    {
                        { "COMMAND.BLOCKED", "<color=yellow>В тюрьме нельзя иcпользовать эту команду!</color>" },
                        { "CHAT.BLOCKED", "<color=yellow>В тюрьме общий чат не работает!</color>" },
                        { "GUI.TIMER", "ВАМ ОСТАЛОСЬ ПРОВЕСТИ ЕЩЕ {time} В ТЮРЬМЕ"},
                        { "PRISON.ADD", "Игрок <color=yellow>{name}</color> отправлен в тюрьму на <color=yellow>{time}</color>\nПричина: <color=yellow>{reason}</color>"},
                        { "PRISON.REMOVE", "Игрок <color=yellow>{name}</color> вышел на волю."}
                    },
                    belts = new belt[]
                    {
                    //    new belt{ name = "knife.bone", amount = 1 , skin = 835461890 }
                    }
                };
            }
        }
        #endregion

        #region ВРЕМЯ
        private static string m0 = "МИНУТ";
        private static string m1 = "МИНУТЫ";
        private static string m2 = "МИНУТУ";

        private static string s0 = "СЕКУНД";
        private static string s1 = "СЕКУНДЫ";
        private static string s2 = "СЕКУНДУ";

        private static string h0 = "ЧАСОВ";
        private static string h1 = "ЧАСА";
        private static string h2 = "ЧАС";

        private static string d0 = "ДНЕЙ";
        private static string d1 = "ДНЯ";
        private static string d2 = "ДЕНЬ";

        private static string FormatTime(TimeSpan time)
        => (time.Days == 0 ? string.Empty : FormatDays(time.Days)) + (time.Hours == 0 ? string.Empty : FormatHours(time.Hours)) + (time.Minutes == 0 ? string.Empty : FormatMinutes(time.Minutes)) + ((time.Seconds == 0) ? string.Empty : FormatSeconds(time.Seconds));

        private static string FormatDays(int days) => FormatUnits(days, d0, d1, d2);

        private static string FormatHours(int hours) => FormatUnits(hours, h0, h1, h2);

        private static string FormatMinutes(int minutes) => FormatUnits(minutes, m0, m1, m2);

        private static string FormatSeconds(int seconds) => FormatUnits(seconds, s0, s1, s2);

        private static string FormatUnits(int units, string form1, string form2, string form3)
        {
            var tmp = units % 10;

            if (units >= 5 && units <= 20 || tmp >= 5 && tmp <= 9 || tmp == 0)
                return $"{units} {form1} ";

            if (tmp >= 2 && tmp <= 4)
                return $"{units} {form2} ";

            return $"{units} {form3} ";
        }
        #endregion

        #region ЗЭК ГУИ & ПРОЧЕЕ
        const string PRISONGUI = "[{\"name\":\"PRISONGUI\",\"parent\":\"Overlay\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0 0 0 0.7878113\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0.82\",\"anchormax\":\"1 0.9\",\"offsetmax\":\"0 0\"}]},{\"name\":\"LINEG\",\"parent\":\"PRISONGUI\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"1 0 0 0.3921569\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 0.05\",\"offsetmax\":\"0 0\"}]},{\"name\":\"LINEG2\",\"parent\":\"PRISONGUI\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"1 0 0 0.3921569\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0.95\",\"anchormax\":\"1 1\",\"offsetmax\":\"0 0\"}]},{\"name\":\"TEXTG\",\"parent\":\"PRISONGUI\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{text}\",\"fontSize\":30,\"align\":\"MiddleCenter\",\"color\":\"1 1 1 0.7878122\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmax\":\"0 0\"}]}]";
        const string PRISONGUITICK = "[{\"name\":\"TEXTG\",\"parent\":\"PRISONGUI\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{text}\",\"fontSize\":30,\"align\":\"MiddleCenter\",\"color\":\"1 1 1 0.7878122\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmax\":\"0 0\"}]}]";

        class PRISON : MonoBehaviour
        {
            public int seconds;
            public DateTime lastmsg;
            private BasePlayer player;

            public void START(int time)
            {
                player = GetComponent<BasePlayer>();
                if (player == null)
                {
                    Destroy(this);
                    return;
                }
                seconds = time;
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "PRISONGUI");
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", PRISONGUI.Replace("{text}", config.messages["GUI.TIMER"].Replace("{time}", FormatTime(TimeSpan.FromSeconds(seconds)))));
                InvokeRepeating(nameof(TICK), 1f, 1f);
            }

            private void TICK()
            {
                seconds--;
                if (seconds <= 0)
                {
                    string text = config.messages["PRISON.REMOVE"].Replace("{name}", player.displayName);
                    ConsoleNetwork.SendClientCommand(Network.Net.sv.connections, "chat.add", 0, player.UserIDString, text);
                    UnPrison();
                    DoDestroy();
                    return;
                }
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "TEXTG");
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", PRISONGUITICK.Replace("{text}", config.messages["GUI.TIMER"].Replace("{time}", FormatTime(TimeSpan.FromSeconds(seconds)))));
            }

            public void UnPrison()
            {
                if (prisoners.ContainsKey(player.userID)) prisoners.Remove(player.userID);
                UNPRISON(player);
                Destroy(this);
            }

            public void DoDestroy() => Destroy(this);

            private void OnDestroy()
            {
                if (IsInvoking(nameof(TICK))) CancelInvoke(nameof(TICK));
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "PRISONGUI");
            }
        }
        #endregion

        #region ТЮРЬМА!!!
        [PluginReference] Plugin CopyPaste;
        static PrisonBitch ins;
        private static List<Vector3> positions = new List<Vector3>();
        private void Init()
        {
            ins = this;
            Unsubscribe(nameof(OnPlayerChat));
            Unsubscribe(nameof(OnPasteFinished));
        }

        private void CREATEPRISON()
        {
            Subscribe(nameof(OnPasteFinished));
            if (CopyPaste == null)
            {
                Debug.LogError("Установите копипаст!");
                return;
            }
            var options = new List<string> { "Deployables", "true", "Inventories", "true", "height", position.y.ToString() };
            var successPaste = CopyPaste.Call("TryPasteFromVector3", position, 0f, config.name, options.ToArray());
            if (successPaste is string)
            {
                PrintError(successPaste.ToString());
                Unsubscribe(nameof(OnPasteFinished));
            }
            INITIALIZE();
        }

        private void OnPasteFinished(List<BaseEntity> entitys)
        {
            config.positions.Clear();
            positions.Clear();

            foreach (var entity in entitys)
            {
                if (entity is SirenLight)
                {
                    Vector3 pos = entity.transform.position + Vector3.up;
                    positions.Add(pos);
                    config.positions.Add(pos.ToString());
                    entity.Kill();
                }
            }
            Debug.Log("Тюрьма построена!");
            SaveConfig();
            Unsubscribe(nameof(OnPasteFinished));
        }
        #endregion

        #region БЛОКИРУЕМ КОМАНДЫ & ОБЩИЙ CHAT
        private object OnPlayerChat(BasePlayer player, string message, Chat.ChatChannel channel)
        {
            if (channel == Chat.ChatChannel.Global)
            {
                PRISON pRISON;
                if (player.TryGetComponent<PRISON>(out pRISON))
                {
                    if (DateTime.Now > pRISON.lastmsg)
                    {
                        player.ChatMessage(config.messages["CHAT.BLOCKED"]);
                        pRISON.lastmsg = DateTime.Now.AddSeconds(3);
                    }
                    return true;
                }
            }
            return null;
        }

        private object OnUserCommand(IPlayer player, string com, string[] args) => blocker(BasePlayer.Find(player.Id), com.TrimStart('/').Substring(com.IndexOf(".", StringComparison.Ordinal) + 1).ToLower());

        private object OnServerCommand(ConsoleSystem.Arg arg) => blocker(arg.Player(), arg.cmd.FullName.ToLower());

        private object blocker(BasePlayer player, string com)
        {
            if (player == null) return null;
            com = com.Replace("global.", "");
            if (config.whitelist.Any(x => com.Contains(x))) return null;
            PRISON pRISON;
            if (player.TryGetComponent<PRISON>(out pRISON))
            {
                if (DateTime.Now > pRISON.lastmsg)
                {
                    player.ChatMessage(config.messages["COMMAND.BLOCKED"]);
                    pRISON.lastmsg = DateTime.Now.AddSeconds(3);
                }
                return true;
            }
            return null;
        }
        #endregion

        #region КОМАНДЫ
        const string adminpermission = "prisonbitch.admin";
        [ChatCommand("prison")]
        private void CMDCHATPRISON(BasePlayer player, string cmd, string[] args)
        {
            if (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, adminpermission))
            {
                player.ChatMessage("В ДОСТУПЕ ОТКАЗАНО!");
                return;
            }

            if (args == null || args.Length == 0)
            {
                player.ChatMessage("<color=yellow>/prison add STEAMID МИНУТ ПРИЧИНА</color> - отправить в тюрьму\n<color=yellow>/prison remove STEAMID</color> - освободить с тюрьмы\n<color=yellow>/prison tp</color> - телепорт в тюрьму\nSTEAMID не обязательно, можно и через ник, если игрок находиться на сервере");
            }
            else if (args.Length == 1)
            {
                if (args[0] == "tp")
                {
                    Teleport(player, positions[Random.Range(0, positions.Count)]);
                }
            }
            else if (args.Length > 1)
            {
                if (args[0] == "add")
                {
                    if (args.Length < 3)
                    {
                        player.ChatMessage("<color=yellow>/prison add STEAMID МИНУТ ПРИЧИНА</color>\nSTEAMID не обязательно, можно и через ник, если игрок находиться на сервере");
                        return;
                    }

                    BasePlayer target = BasePlayer.Find(args[1]);
                    ulong STEAMID;
                    if (target != null) STEAMID = target.userID;
                    else if (!ulong.TryParse(args[1], out STEAMID))
                    {
                        player.ChatMessage("<color=yellow>НЕ КОРЕКТНО УКАЗАН STEAMID ИЛИ НИК!</color>");
                        return;
                    }

                    int MINUTES;
                    if (!int.TryParse(args[2], out MINUTES) || MINUTES <= 0)
                    {
                        player.ChatMessage("<color=yellow>НЕ КОРЕКТНО УКАЗАНО ВРЕМЯ!</color>");
                        return;
                    }

                    string reason = "-";
                    if (args.Length > 3) reason = string.Join(" ", args.Skip(3).ToArray());

                    string name;
                    string time;
                    ADDPRISON(STEAMID, MINUTES, reason, out name, out time);
                    Debug.Log($"{name} отправлен в тюрьму на {time}. (Причина: {reason})");
                }
                else if (args[0] == "remove")
                {
                    if (args.Length < 2)
                    {
                        player.ChatMessage("<color=yellow>/prison remove STEAMID</color>\nSTEAMID не обязательно, можно и через ник, если игрок находиться на сервере");
                        return;
                    }

                    BasePlayer target = BasePlayer.Find(args[1]);
                    ulong STEAMID;
                    if (target != null) STEAMID = target.userID;
                    else if (!ulong.TryParse(args[1], out STEAMID))
                    {
                        player.ChatMessage("<color=yellow>НЕ КОРЕКТНО УКАЗАН STEAMID ИЛИ НИК!</color>");
                        return;
                    }

                    if (!prisoners.ContainsKey(STEAMID))
                    {
                        player.ChatMessage("<color=yellow>В ТЮРЬМЕ ТАКОЙ ИГРОК НЕ ОБНАРУЖЕН!</color>");
                        return;
                    }
                    string name;
                    REMOVEPRISON(STEAMID, out name);
                    Debug.Log($"{name} успешно особожден с тюрьмы.");
                }
            }
        }

        [ConsoleCommand("prison.add")]
        private void CMDNCOMMAND(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            if (!arg.HasArgs(2))
            {
                arg.ReplyWith("prison.add STEAMID МИНУТ ПРИЧИНА\nSTEAMID не обязательно, можно и через ник, если игрок находиться на сервере");
                return;
            }

            BasePlayer target = BasePlayer.Find(arg.Args[0]);
            ulong STEAMID;
            if (target != null) STEAMID = target.userID;
            else if (!ulong.TryParse(arg.Args[0], out STEAMID))
            {
                arg.ReplyWith("НЕ КОРЕКТНО УКАЗАН STEAMID ИЛИ НИК!");
                return;
            }

            int MINUTES;
            if (!int.TryParse(arg.Args[1], out MINUTES) || MINUTES <= 0)
            {
                arg.ReplyWith("НЕ КОРЕКТНО УКАЗАНО ВРЕМЯ!");
                return;
            }

            string reason = "-";
            if (arg.HasArgs(3)) reason = string.Join(" ", arg.Args.Skip(2).ToArray());

            string name;
            string time;
            ADDPRISON(STEAMID, MINUTES, reason, out name, out time);
            //fullmute
            arg.ReplyWith($"{name} отправлен в тюрьму на {time}. (Причина: {reason})");
        }

        private void ADDPRISON(ulong STEAMID, int MINUTES, string reason, out string name, out string time)
        {
            prisoners[STEAMID] = DateTime.Now.AddMinutes(MINUTES);
            BasePlayer player = BasePlayer.FindByID(STEAMID);
            if (player != null)
            {
                name = player.displayName;
                IsPrison(player);
            }
            else name = STEAMID.ToString();

            time = FormatTime(TimeSpan.FromMinutes(MINUTES)).ToLower();
            Server.Broadcast(config.messages["PRISON.ADD"].Replace("{name}", name).Replace("{time}", FormatTime(TimeSpan.FromMinutes(MINUTES)).ToLower()).Replace("{reason}", reason));
        }

        private void REMOVEPRISON(ulong STEAMID, out string name)
        {
            prisoners.Remove(STEAMID);
            Save();
            BasePlayer player = BasePlayer.FindByID(STEAMID);
            if (player != null)
            {
                name = player.displayName;
                PRISON pRISON;
                if (player.TryGetComponent<PRISON>(out pRISON))
                {
                    pRISON.UnPrison();
                }
            }
            else
            {
                BasePlayer sleeper = BasePlayer.FindSleeping(STEAMID);
                if (sleeper != null)
                {
                    name = sleeper.displayName;
                    sleeper.Kill();
                }
                else name = STEAMID.ToString();
            }
            Server.Broadcast(config.messages["PRISON.REMOVE"].Replace("{name}", name));
        }

        [ConsoleCommand("prison.remove")]
        private void CMDREMOVECOMMAND(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            if (!arg.HasArgs(1))
            {
                arg.ReplyWith("prison.remove STEAMID\nSTEAMID не обязательно, можно и через ник, если игрок находиться на сервере");
                return;
            }

            BasePlayer target = BasePlayer.Find(arg.Args[0]);
            ulong STEAMID;
            if (target != null) STEAMID = target.userID;
            else if (!ulong.TryParse(arg.Args[0], out STEAMID))
            {
                arg.ReplyWith("НЕ КОРЕКТНО УКАЗАН STEAMID ИЛИ НИК!");
                return;
            }

            if (!prisoners.ContainsKey(STEAMID))
            {
                arg.ReplyWith("В ТЮРЬМЕ ТАКОЙ ИГРОК НЕ ОБНАРУЖЕН!");
                return;
            }
            string name;
            REMOVEPRISON(STEAMID, out name);
            arg.ReplyWith($"{name} успешно особожден.");
        }
        #endregion

        private static Dictionary<ulong, DateTime> prisoners = new Dictionary<ulong, DateTime>();
        private static Vector3 position;

        private void OnServerInitialized()
        {
            SaveConfig();
            if (config.belts == null)
            {
                config.belts = new belt[]
                {
                    new belt{ name = "knife.bone", amount = 1 , skin = 835461890 }
                };
                SaveConfig();
            }

            prisoners.Clear();
            Dictionary<ulong, string> prisonerssave = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, string>>("prisoners");
            foreach (var z in prisonerssave)
            {
                DateTime dateTime = Convert.ToDateTime(z.Value);
                if (dateTime <= DateTime.Now) continue;
                prisoners.Add(z.Key, dateTime);
            }
            if (!permission.PermissionExists(adminpermission)) permission.RegisterPermission(adminpermission, this);
            Debug.Log($"Обнаружили {prisoners.Count} заключённых");
            position = config.position.ToVector3();
            Debug.Log("Проверяем построена ли тюрьма.");
            RaycastHit hitInfo;
            if (!UnityEngine.Physics.Raycast(position, Vector3.up, out hitInfo, LayerMask.GetMask("Construction")))
            {
                Debug.Log("Тюрьма не построена...строим тюрьму...");
                CREATEPRISON();
            }
            else
            {
                if (config.positions.Count == 0)
                {
                    Debug.LogError("Не указаны координаты для заключенных!");
                    return;
                }

                foreach (var z in config.positions)
                {
                    positions.Add(z.ToVector3());
                }
                INITIALIZE();
            }
            if (config.blockchat) Subscribe(nameof(OnPlayerChat));
        }

        private void INITIALIZE()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                IsPrison(player);
            }
        }

        private bool ISPRISON(BasePlayer player)
        {
            DateTime dateTime;
            if (prisoners.TryGetValue(player.userID, out dateTime) && dateTime > DateTime.Now) return true;
            return false;
        }

        private bool IsPrison(BasePlayer player)
        {
            DateTime dateTime;
            if (!prisoners.TryGetValue(player.userID, out dateTime)) return false;
            int time = (int)(dateTime - DateTime.Now).TotalSeconds;
            if (time < 0) time = 0;
            if (player.GetComponent<PRISON>() == null) player.gameObject.AddComponent<PRISON>().START(time);

            if (positions.Count == 0)
            {
                Debug.LogError("Не указаны координаты для заключенных или не построена тюрьма!");
                return true;
            }

            if (player.IsWounded() || player.GetComponentInParent<CargoShip>() || player.GetComponentInParent<HotAirBalloon>() || player.GetComponentInParent<Lift>()) player.Kill();
            else
            {
                if (player.isMounted) player.GetMounted().DismountPlayer(player, false);
                Teleport(player, positions[Random.Range(0, positions.Count)]);
                player.inventory.Strip();
                timer.Once(0.5f, () =>
                {
                    if (!player.IsConnected) return;
                    player.inventory.Strip();
                    foreach (var z in config.clothes)
                    {
                        Item item = CREATEITEM(z.name, z.skin);
                        if (item == null)
                        {
                            Debug.LogError(z.name + " предмет не существует! Исправьте в конфиге!");
                            continue;
                        }
                        item.MoveToContainer(player.inventory.containerWear);
                    }
                    foreach (var z in config.belts)
                    {
                        Item item = CREATEITEM(z.name, z.skin, z.amount);
                        if (item == null)
                        {
                            Debug.LogError(z.name + " предмет не существует! Исправьте в конфиге!");
                            continue;
                        }
                        item.MoveToContainer(player.inventory.containerBelt);
                    }
                    player.inventory.containerWear.SetFlag(ItemContainer.Flag.IsLocked, true);
                    player.SendNetworkUpdate();
                });
            }


            return true;
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            PRISON pRISON;
            if (player.TryGetComponent<PRISON>(out pRISON))
            {
                pRISON.DoDestroy();
            }
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (!player.IsConnected) return;

            if (player.IsReceivingSnapshot)
            {
                timer.Once(1f, () => OnPlayerConnected(player));
                return;
            }

            if (!IsPrison(player)) UNPRISON(player);
        }

        private static void UNPRISON(BasePlayer player)
        {
            if (player.inventory.containerWear.HasFlag(ItemContainer.Flag.IsLocked))
            {
                player.inventory.containerWear.Clear();
                player.inventory.containerWear.SetFlag(ItemContainer.Flag.IsLocked, false);
                BasePlayer.SpawnPoint spawnPoint = ServerMgr.FindSpawnPoint();
                Teleport(player, spawnPoint.pos);
            }
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            IsPrison(player);
        }

        private void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                PRISON pRISON;
                if (player.TryGetComponent<PRISON>(out pRISON)) pRISON.DoDestroy();
            }
            if (prisoners.Count > 0) Save();
        }

        private static void Teleport(BasePlayer player, Vector3 position)
        {
            if (!player.IsConnected) return;
            player.ClientRPCPlayer(null, player, "StartLoading");
            StartSleeping(player);
            player.MovePosition(position);
            player.ClientRPCPlayer(null, player, "ForcePositionTo", position);
            player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
            player.UpdateNetworkGroup();
            player.SendNetworkUpdateImmediate(false);
            try { player.ClearEntityQueue(null); } catch { }
            player.SendFullSnapshot();
        }

        private static void StartSleeping(BasePlayer player)
        {
            if (player.IsSleeping()) return;
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Sleeping, true);
            if (!BasePlayer.sleepingPlayerList.Contains(player)) BasePlayer.sleepingPlayerList.Add(player);
            player.CancelInvoke("InventoryUpdate");
        }

        private void Save() => Interface.Oxide.DataFileSystem.WriteObject("prisoners", prisoners);

        private static Item CREATEITEM(string name, ulong skin, int amount = 1) => ItemManager.CreateByName(name, amount, skin);

        #region ЧАТЫ
        private object OnXChat(ulong userid)
        {
            if (prisoners.ContainsKey(userid)) return true;
            return null;
        }

        private void OnBetterChat(Dictionary<string, object> messageData)
        {
            Chat.ChatChannel chatChannel = (ConVar.Chat.ChatChannel)messageData["ChatChannel"];
            bool isPublicMessage = chatChannel == ConVar.Chat.ChatChannel.Global;

            if (chatChannel == Chat.ChatChannel.Global)
            {
                IPlayer player = (IPlayer)messageData["Player"];
                ulong ID = Convert.ToUInt64(player.Id);
                if (prisoners.ContainsKey(ID))
                {
                    messageData["CancelOption"] = 2;
                }
            }
        }
        #endregion
    }
}