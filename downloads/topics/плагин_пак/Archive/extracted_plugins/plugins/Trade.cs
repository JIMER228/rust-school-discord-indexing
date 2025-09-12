using System;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Plugins;
using Newtonsoft.Json;
using System.Linq;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info ("Trade", "Calytic", "1.2.45")]
    class Trade : RustPlugin
    {
        // Константы для меню выбора игрока
        private const string PlayerSelectMenu = "TradePlayerSelect";
        private const string PlayerSelectBackground = "TradePlayerSelectBackground";
        private const string PlayerSelectTitle = "TradePlayerSelectTitle";
        private const string PlayerSelectCloseButton = "TradePlayerSelectCloseButton";
        private const string PlayerSelectScrollView = "TradePlayerSelectScrollView";
        private const string PlayerSelectContent = "TradePlayerSelectContent";
        #region Configuration

        string box;
        int slots;
        float cooldownMinutes;
        float maxRadius;
        float pendingSeconds;
        float radiationMax;
        bool allowSafeZone;

        [PluginReference]
        Plugin Ignore, Clans, Friends;

        Dictionary<string, DateTime> tradeCooldowns = new Dictionary<string, DateTime> ();

        // Переменные для постраничной навигации меню выбора игроков
        private const int PlayersPerPage = 8; // Количество игроков на странице
        private Dictionary<string, int> playerMenuPages = new Dictionary<string, int>(); // Текущая страница для каждого игрока

        #endregion

        #region Trade State

        class OnlinePlayer
        {
            public BasePlayer Player;
            public StorageContainer View;
            public OpenTrade Trade;

            public PlayerInventory inventory {
                get {
                    return Player.inventory;
                }
            }

            public ItemContainer containerMain {
                get {
                    return Player.inventory.containerMain;
                }
            }

            public OnlinePlayer (BasePlayer player)
            {
            }

            public void Clear ()
            {
                View = null;
                Trade = null;
            }
        }

        [OnlinePlayers]
        Hash<BasePlayer, OnlinePlayer> onlinePlayers = new Hash<BasePlayer, OnlinePlayer> ();

        class OpenTrade
        {
            public OnlinePlayer source;
            public OnlinePlayer target;

            public BasePlayer sourcePlayer {
                get {
                    return source.Player;
                }
            }

            public BasePlayer targetPlayer {
                get {
                    return target.Player;
                }
            }

            public bool complete = false;
            public bool closing = false;

            public bool sourceAccept = false;
            public bool targetAccept = false;

            public OpenTrade (OnlinePlayer source, OnlinePlayer target)
            {
                this.source = source;
                this.target = target;
            }

            public OnlinePlayer GetOther (OnlinePlayer onlinePlayer)
            {
                if (source == onlinePlayer) {
                    return target;
                }

                return source;
            }

            public BasePlayer GetOther (BasePlayer player)
            {
                if (sourcePlayer == player) {
                    return targetPlayer;
                }

                return sourcePlayer;
            }

            public void ResetAcceptance ()
            {
                sourceAccept = false;
                targetAccept = false;
            }

            public bool IsInventorySufficient ()
            {
                if (target == null || source == null) {
                    return false;
                }

                if (target.containerMain == null || source.containerMain == null) {
                    return false;
                }

                if ((target.containerMain.capacity - target.containerMain.itemList.Count) < source.View.inventory.itemList.Count ||
                       (source.containerMain.capacity - source.containerMain.itemList.Count) < target.View.inventory.itemList.Count) {
                    return true;
                }

                return false;
            }

            public bool IsValid ()
            {
                if (IsSourceValid () && IsTargetValid ())
                    return true;

                return false;
            }

            public bool IsSourceValid ()
            {
                if (sourcePlayer != null && sourcePlayer.IsConnected)
                    return true;

                return false;
            }

            public bool IsTargetValid ()
            {
                if (targetPlayer != null && targetPlayer.IsConnected)
                    return true;

                return false;
            }
        }

        class PendingTrade
        {
            public BasePlayer Target;
            public Timer Timer;

            public PendingTrade (BasePlayer target)
            {
                Target = target;
            }

            public void Destroy ()
            {
                if (Timer != null && !Timer.Destroyed) {
                    Timer.Destroy ();
                }
            }
        }

        List<OpenTrade> openTrades = new List<OpenTrade> ();
        Dictionary<BasePlayer, PendingTrade> pendingTrades = new Dictionary<BasePlayer, PendingTrade> ();
        #endregion

        #region Initialization

        void Init ()
        {

            UnsubscribeAll ();
        }

        void UnsubscribeAll ()
        {
            //Unsubscribe(nameof(CanNetworkTo));
            Unsubscribe (nameof (OnItemAction));
            Unsubscribe (nameof (OnItemAddedToContainer));
            Unsubscribe (nameof (CanMoveItem));
            Unsubscribe (nameof (OnItemRemovedFromContainer));
        }

        void SubscribeAll ()
        {
            //Subscribe(nameof(CanNetworkTo));
            Subscribe (nameof (OnItemAction));
            Subscribe (nameof (OnItemAddedToContainer));
            Subscribe (nameof (CanMoveItem));
            Subscribe (nameof (OnItemRemovedFromContainer));
        }

        void Loaded ()
        {
            permission.RegisterPermission ("trade.use", this);
            permission.RegisterPermission ("trade.confirm", this);

            LoadMessages ();

            CheckConfig ();

            box = GetConfig ("Settings", "box", "assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab");
            slots = GetConfig ("Settings", "slots", 30);
            cooldownMinutes = GetConfig ("Settings", "cooldownMinutes", 5f);
            maxRadius = GetConfig ("Settings", "maxRadius", 5000f);
            pendingSeconds = GetConfig ("Settings", "pendingSeconds", 25f);
            radiationMax = GetConfig ("Settings", "radiationMax", 1f);
            allowSafeZone = GetConfig ("Settings", "allowSafeZone", true);
        }

        void Unloaded ()
        {
            foreach (var player in BasePlayer.activePlayerList) {
                OnlinePlayer onlinePlayer;
                if (onlinePlayers.TryGetValue (player, out onlinePlayer)) {
                    if (onlinePlayer.Trade != null) {
                        TradeCloseBoxes (onlinePlayer.Trade);
                    } else if (onlinePlayer.View != null) {
                        CloseBoxView (player, onlinePlayer.View);
                    }
                }
                
                // Очищаем UI от IQSorter при выгрузке плагина
                try
                {
                    CuiHelper.DestroyUi(player, "IQ_HEADER_PLAYER");
                    CuiHelper.DestroyUi(player, "IQ_HEADER_CONTAINER");
                }
                catch (Exception ex)
                {
                    Puts(string.Format("Error clearing IQSorter UI in Unloaded: {0}", ex.Message));
                }
                
                // Скрываем кнопку принятия торговли при выгрузке плагина
                HideTradeAcceptButton(player);
                
                // Дополнительная очистка лута при выгрузке плагина
                try
                {
                    if (player.inventory.loot.entitySource != null)
                    {
                        // Проверяем, не является ли это активной торговлей
                        bool isActiveTrade = false;
                        if (onlinePlayers.ContainsKey(player))
                        {
                            var playerData = onlinePlayers[player];
                            if (playerData.Trade != null && !playerData.Trade.complete)
                            {
                                isActiveTrade = true;
                            }
                        }
                        
                        // Очищаем лут только если это не активная торговля
                        if (!isActiveTrade)
                        {
                            player.EndLooting();
                            player.inventory.loot.entitySource = null;
                            player.inventory.loot.itemSource = null;
                            player.inventory.loot.containers.Clear();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Puts(string.Format("Error clearing loot in Unloaded: {0}", ex.Message));
                }
            }
        }

        protected new void LoadDefaultConfig ()
        {
            Config ["Settings", "box"] = "assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab";
            Config ["Settings", "slots"] = 30;
            Config ["Settings", "cooldownMinutes"] = 5;
            Config ["Settings", "maxRadius"] = 5000f;
            Config ["Settings", "pendingSeconds"] = 25f;
            Config ["Settings", "radiationMax"] = 1;
            Config ["Settings", "allowSafeZone"] = true;
            Config ["VERSION"] = Version.ToString ();
        }

        void CheckConfig ()
        {
            if (Config ["VERSION"] == null) {
                // FOR COMPATIBILITY WITH INITIAL VERSIONS WITHOUT VERSIONED CONFIG
                ReloadConfig ();
            } else if (GetConfig<string> ("VERSION", "") != Version.ToString ()) {
                // ADDS NEW, IF ANY, CONFIGURATION OPTIONS
                ReloadConfig ();
            }
        }

        protected void ReloadConfig ()
        {
            Config ["VERSION"] = Version.ToString ();

            // NEW CONFIGURATION OPTIONS HERE
            Config ["Settings", "radiationMax"] = GetConfig ("Settings", "radiationMax", 1f);
            // END NEW CONFIGURATION OPTIONS

            PrintToConsole ("Upgrading configuration file");
            SaveConfig ();
        }

        void LoadMessages ()
        {
            lang.RegisterMessages (new Dictionary<string, string>
            {
                {"Inventory: You", "You do not have enough room in your inventory"},
                {"Inventory: Them", "Their inventory does not have enough room"},
                {"Inventory: Generic", "Insufficient inventory space"},

                {"Player: Not Found", "No player found by that name"},
                {"Player: Unknown", "Unknown"},
                {"Player: Yourself", "You cannot trade with yourself"},

                {"Status: Completing", "Completing trade.."},
                {"Status: No Pending", "You have no pending trade requests"},
                {"Status: Pending", "They already have a pending trade request"},
                {"Status: Received", "You have received a trade request from {0}. Type <color=#00FF00>/trade accept</color> to begin trading"},
                {"Status: They Interrupted", "They moved or closed the trade"},
                {"Status: You Interrupted", "You moved or closed the trade"},

                {"Trade: Sent", "Trade request sent"},
                {"Trade: They Declined", "They declined your trade request"},
                {"Trade: You Declined", "You declined their trade request"},
                {"Trade: They Accepted", "{0} accepted."},
                {"Trade: You Accepted", "You accepted."},
                {"Trade: Pending", "Trade pending."},

                {"Denied: Permission", "You lack permission to do that"},
                {"Denied: Privilege", "You do no have building privilege"},
                {"Denied: Swimming", "You cannot do that while swimming"},
                {"Denied: Falling", "You cannot do that while falling"},
                {"Denied: Mounted", "You cannot do that while mounted"},
                {"Denied: Wounded", "You cannot do that while wounded"},
                {"Denied: Irradiated", "You cannot do that while irradiated"},
                {"Denied: Generic", "You cannot do that right now"},
                {"Denied: They Busy", "That player is busy"},
                {"Denied: They Ignored You", "They ignored you"},
                {"Denied: Distance", "Too far away"},
                {"Denied: Ship", "You cannot do that while on a ship"},
                {"Denied: Lift", "You cannot do that while on a lift"},
                {"Denied: Balloon", "You cannot do that while on a balloon"},
                {"Denied: Safe Zone", "You cannot do that while in a safe zone"},

                {"Item: BP", "BP"},

                {"Syntax: Trade Accept", "Invalid syntax. /trade accept"},
                {"Syntax: Trade", "Invalid syntax. /trade \"Player Name\""},

                {"Cooldown: Seconds", "You are doing that too often, try again in a {0} seconds(s)."},
                {"Cooldown: Minutes", "You are doing that too often, try again in a {0} minute(s)."},
            }, this);
        }

        #endregion

        #region Oxide Hooks

        //object CanNetworkTo(BaseNetworkable entity, BasePlayer target)
        //{
        //    if (entity == null || target == null || entity == target) return null;
        //    if (target.IsAdmin) return null;

        //    OnlinePlayer onlinePlayer;
        //    bool IsMyBox = false;
        //    if (onlinePlayers.TryGetValue(target, out onlinePlayer))
        //    {
        //        if (onlinePlayer.View != null && onlinePlayer.View.net.ID == entity.net.ID)
        //        {
        //            IsMyBox = true;
        //        }
        //    }

        //    if (IsTradeBox(entity) && !IsMyBox) return false;

        //    return null;
        //}

        void OnPlayerConnected (BasePlayer player)
        {
            onlinePlayers [player].View = null;
            onlinePlayers [player].Trade = null;
        }

        void OnPlayerDisconnected (BasePlayer player)
        {
            OnlinePlayer onlinePlayer;
            if (onlinePlayers.TryGetValue (player, out onlinePlayer)) {
                if (onlinePlayer.Trade != null) {
                    TradeCloseBoxes (onlinePlayer.Trade);
                } else if (onlinePlayer.View != null) {
                    CloseBoxView (player, onlinePlayer.View);
                }
            }
            
            // Очищаем UI от IQSorter при отключении игрока
            try
            {
                CuiHelper.DestroyUi(player, "IQ_HEADER_PLAYER");
                CuiHelper.DestroyUi(player, "IQ_HEADER_CONTAINER");
            }
            catch (Exception ex)
            {
                Puts(string.Format("Error clearing IQSorter UI in OnPlayerDisconnected: {0}", ex.Message));
            }
            
            // Скрываем кнопку принятия торговли при отключении игрока
            HideTradeAcceptButton(player);
            
            // Дополнительная очистка лута при отключении игрока
            try
            {
                if (player.inventory.loot.entitySource != null)
                {
                    // Проверяем, не является ли это активной торговлей
                    bool isActiveTrade = false;
                    if (onlinePlayers.ContainsKey(player))
                    {
                        var playerData = onlinePlayers[player];
                        if (playerData.Trade != null && !playerData.Trade.complete)
                        {
                            isActiveTrade = true;
                        }
                    }
                    
                    // Очищаем лут только если это не активная торговля
                    if (!isActiveTrade)
                    {
                        player.EndLooting();
                        player.inventory.loot.entitySource = null;
                        player.inventory.loot.itemSource = null;
                        player.inventory.loot.containers.Clear();
                    }
                }
            }
            catch (Exception ex)
            {
                Puts(string.Format("Error clearing loot in OnPlayerDisconnected: {0}", ex.Message));
            }
        }

        void OnPlayerLootEnd (PlayerLoot inventory)
        {
            var player = inventory.GetComponent<BasePlayer> ();
            if (player == null)
                return;

            OnlinePlayer onlinePlayer;
            if (onlinePlayers.TryGetValue (player, out onlinePlayer) && onlinePlayer.View != null) {
                if (onlinePlayer.View == inventory.entitySource && onlinePlayer.Trade != null) {
                    OpenTrade t = onlinePlayer.Trade;

                    if (!t.closing) {
                        t.closing = true;
                        if (!onlinePlayer.Trade.complete) {
                            if (onlinePlayer.Trade.sourcePlayer == player) {
                                TradeReply (t, "Status: They Interrupted", "Status: You Interrupted");
                            } else {
                                TradeReply (t, "Status: You Interrupted", "Status: They Interrupted");
                            }
                        }
                        CloseBoxView (player, (StorageContainer)inventory.entitySource);
                        
                        // Дополнительная принудительная очистка при закрытии лута
                        try
                        {
                            if (inventory.entitySource is StorageContainer container)
                            {
                                if (container is BaseEntity entity)
                                {
                                    entity.Kill(BaseNetworkable.DestroyMode.Gib);
                                }
                                if (container.net != null)
                                {
                                    container.net.Destroy();
                                }
                                UnityEngine.Object.Destroy(container);
                            }
                        }
                        catch (Exception ex)
                        {
                            Puts(string.Format("Error in OnPlayerLootEnd cleanup: {0}", ex.Message));
                        }
                    }
                }
            }
        }

        void OnItemAction (Item item, string cmd)
        {
            if (cmd == "drop") {
                BasePlayer player = item.GetOwnerPlayer ();

                if (player is BasePlayer) {
                    OnlinePlayer onlinePlayer;
                    if (onlinePlayers.TryGetValue (player, out onlinePlayer) && onlinePlayer.Trade != null && player.inventory != null) {
                        if (item.parent == player.inventory.containerMain && !onlinePlayer.Trade.IsInventorySufficient ()) {
                            ShowTrades (onlinePlayer.Trade, "Trade: Pending");
                        }
                    }
                }
            }
        }

        void OnItemAddedToContainer (ItemContainer container, Item item)
        {
            if (container.playerOwner is BasePlayer) {
                OnlinePlayer onlinePlayer;
                if (onlinePlayers.TryGetValue (container.playerOwner, out onlinePlayer) && onlinePlayer.Trade != null) {
                    OpenTrade t = onlinePlayers [container.playerOwner].Trade;

                    if (!t.complete) {
                        t.ResetAcceptance ();

                        if (t.IsValid ()) {
                            ShowTrades (t, "Trade: Pending");
                        } else {
                            TradeCloseBoxes (t);
                        }
                    }
                }
            }
        }

        object CanMoveItem (Item item, PlayerInventory playerLoot, uint targetContainer, int targetSlot)
        {
            var player = playerLoot.GetComponent<BasePlayer> ();
            if (player == null) {
                return null;
            }

            OnlinePlayer onlinePlayer;
            if (onlinePlayers.TryGetValue (player, out onlinePlayer) && onlinePlayer.Trade != null) {
                OpenTrade t = onlinePlayers [player].Trade;
                if (t.closing) {
                    return false;
                }
            }

            return null;
        }

        void OnItemRemovedFromContainer (ItemContainer container, Item item)
        {
            if (container.playerOwner is BasePlayer) {
                OnlinePlayer onlinePlayer;
                if (onlinePlayers.TryGetValue (container.playerOwner, out onlinePlayer) && onlinePlayer.Trade != null) {
                    OpenTrade t = onlinePlayers [container.playerOwner].Trade;
                    if (!t.complete) {
                        t.ResetAcceptance ();

                        if (t.IsValid ()) {
                            ShowTrades (t, "Trade: Pending");
                        } else {
                            TradeCloseBoxes (t);
                        }
                    }
                }
            }
        }

        #endregion

        #region Commands

        [ChatCommand ("trade")]
        void cmdTrade (BasePlayer player, string command, string [] args)
        {
            // Отладочная информация
            Puts(string.Format("cmdTrade called for player: {0}, args.Length: {1}", player.displayName, args.Length));
            if (args.Length > 0)
                Puts(string.Format("First arg: '{0}'", args[0]));
            
            if (args.Length == 1) {
                if (args [0] == "accept") {
                    Puts(string.Format("Player {0} is accepting trade", player.displayName));
                    if (!CanPlayerTrade (player, "trade.confirm"))
                        return;

                    AcceptTrade (player);
                    return;
                }
            }

            if (args.Length == 0) {
                // Показываем меню выбора игроков
                ShowPlayerSelectMenu(player);
                return;
            }

            if (args.Length != 1) {
                if (pendingTrades.ContainsKey (player)) {
                    SendReply (player, GetMsg ("Syntax: Trade Accept", player));
                } else {
                    SendReply (player, GetMsg ("Syntax: Trade", player));
                }

                return;
            }

            var targetPlayer = FindPlayerByPartialName (args [0]);
            if (targetPlayer == null) {
                SendReply (player, GetMsg ("Player: Not Found", player));
                return;
            }

            if (targetPlayer == player) {
                SendReply (player, GetMsg ("Player: Yourself", player));
                return;
            }

            if (!CheckCooldown (player)) {
                return;
            }

            if (Ignore != null) {
                var IsIgnored = Ignore.Call ("IsIgnored", player.UserIDString, targetPlayer.UserIDString);
                if ((bool)IsIgnored == true) {
                    SendReply (player, GetMsg ("Denied: They Ignored You", player));
                    return;
                }
            }

            OnlinePlayer onlineTargetPlayer;
            if (onlinePlayers.TryGetValue (targetPlayer, out onlineTargetPlayer) && onlineTargetPlayer.Trade != null) {
                SendReply (player, GetMsg ("Denied: They Busy", player));
                return;
            }

            if (maxRadius > 0) {
                if (targetPlayer.Distance (player) > maxRadius) {
                    SendReply (player, GetMsg ("Denied: Distance", player));
                    return;
                }
            }

            if (!CanPlayerTrade (player, "trade.use"))
                return;

            if (pendingTrades.ContainsKey (player)) {
                SendReply (player, GetMsg ("Status: Pending", player));
            } else {
                SendReply (targetPlayer, GetMsg ("Status: Received", targetPlayer), player.displayName);
                SendReply (player, GetMsg ("Trade: Sent", player));
                
                // Показываем UI кнопку принятия торговли
                ShowTradeAcceptButton(targetPlayer, player.displayName);
                
                var pendingTrade = new PendingTrade (targetPlayer);
                pendingTrades.Add (player, pendingTrade);

                pendingTrade.Timer = timer.In (pendingSeconds, delegate () {
                    if (pendingTrades.ContainsKey (player)) {
                        pendingTrades.Remove (player);
                        SendReply (player, GetMsg ("Trade: They Declined", player));
                        SendReply (targetPlayer, GetMsg ("Trade: You Declined", targetPlayer));
                        
                        // Скрываем UI кнопку принятия торговли
                        HideTradeAcceptButton(targetPlayer);
                    }
                });
            }
        }

        [ConsoleCommand ("trade")]
        void ccTrade (ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null)
                return;
            if (arg.Connection.player == null)
                return;
            cmdTrade (arg.Connection.player as BasePlayer, arg.cmd.Name, arg.Args);
        }

        [ConsoleCommand ("trade.decline")]
        void ccTradeDecline (ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null)
                return;
            if (arg.Connection.player == null)
                return;
            var player = arg.Connection.player as BasePlayer;

            OnlinePlayer onlinePlayer;
            if (onlinePlayers.TryGetValue (player, out onlinePlayer) && onlinePlayer.Trade != null) {
                onlinePlayer.Trade.closing = true;
                var target = onlinePlayer.Trade.GetOther (player);
                SendReply (player, GetMsg ("Trade: You Declined", player));
                SendReply (target, GetMsg ("Trade: They Declined", target));

                TradeCloseBoxes (onlinePlayer.Trade);
            } else if (player is BasePlayer) {
                HideTrade (player);
            }
        }



        [ConsoleCommand ("trade.confirm")]
        void ccTradeConfirm (ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null)
                return;
            if (arg.Connection.player == null)
                return;
            var player = arg.Connection.player as BasePlayer;

            // Отладочная информация
            Puts(string.Format("Trade confirm command called by player: {0}", player.displayName));

            TradeAccept (player);
        }

        [ConsoleCommand ("trade.accept.request")]
        void ccTradeAcceptRequest (ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null)
                return;
            if (arg.Connection.player == null)
                return;
            var player = arg.Connection.player as BasePlayer;

            // Отладочная информация
            Puts(string.Format("Trade accept request command called by player: {0}", player.displayName));

            AcceptTrade (player);
        }

        void TradeAccept (BasePlayer player)
        {
            // Отладочная информация
            Puts(string.Format("TradeAccept called for player: {0}", player.displayName));
            
            OnlinePlayer onlinePlayer;
            if (onlinePlayers.TryGetValue (player, out onlinePlayer) && onlinePlayer.Trade != null) {
                Puts(string.Format("Player {0} has active trade", player.displayName));
                
                var t = onlinePlayers [player].Trade;
                if (t.sourcePlayer == player) {
                    Puts(string.Format("Player {0} is source player", player.displayName));
                    if (!CheckSourceInventory (t)) {
                        Puts(string.Format("Source inventory check failed for {0}", player.displayName));
                        return;
                    }

                    t.sourceAccept = true;
                    Puts(string.Format("Source accept set to true for {0}", player.displayName));
                } else if (t.targetPlayer == player) {
                    Puts(string.Format("Player {0} is target player", player.displayName));
                    if (!CheckTargetInventory (t)) {
                        Puts(string.Format("Target inventory check failed for {0}", player.displayName));
                        return;
                    }

                    t.targetAccept = true;
                    Puts(string.Format("Target accept set to true for {0}", player.displayName));
                }

                Puts(string.Format("Trade status - sourceAccept: {0}, targetAccept: {1}", t.sourceAccept, t.targetAccept));

                if (t.targetAccept == true && t.sourceAccept == true) {
                    Puts(string.Format("Both players accepted, completing trade for {0}", player.displayName));
                    CompleteTrade (t);
                } else {
                    Puts(string.Format("Trade pending, showing trades for {0}", player.displayName));
                    ShowTrades (t, "Trade: Pending");
                }
            } else {
                Puts(string.Format("Player {0} has no active trade or onlinePlayer is null", player.displayName));
                if (player is BasePlayer) {
                HideTrade (player);
                }
            }
        }

        void CompleteTrade (OpenTrade t)
        {
            if (t.IsInventorySufficient ()) {
                t.ResetAcceptance ();
                ShowTrades (t, "Inventory: Generic");
                return;
            }
            if (t.complete) {
                return;
            }
            t.complete = true;
            t.closing = true;

            TradeCooldown (t);

            TradeReply (t, "Status: Completing");
            Interface.Oxide.NextTick (() => FinishTrade (t));
        }

        bool CheckSourceInventory (OpenTrade t)
        {
            var i = t.target.View.inventory.itemList.Count;
            var f = t.source.containerMain.capacity - t.source.containerMain.itemList.Count;
            if (i > f) {

                TradeReply (t, "Inventory: Them", "Inventory: You");

                t.sourceAccept = false;
                ShowTrades (t, "Inventory: Generic");
                return false;
            }

            return true;
        }

        bool CheckTargetInventory (OpenTrade t)
        {
            var i = t.source.View.inventory.itemList.Count;
            var f = t.target.containerMain.capacity - t.target.containerMain.itemList.Count;
            if (i > f) {
                TradeReply (t, "Inventory: You", "Inventory: Them");
                t.targetAccept = false;
                ShowTrades (t, "Inventory: Generic");
                return false;
            }

            return true;
        }

        #endregion

        #region GUI

        public string jsonTrade = @"[{""name"":""TradeMsg"",""parent"":""Overlay"",""components"":[{""type"":""UnityEngine.UI.Image"",""color"":""0.05 0.05 0.08 0.98"",""imagetype"":""Filled""},{""type"":""RectTransform"",""anchormax"":""0.75 0.99"",""anchormin"":""0.25 0.65""}]},{""name"":""TradeHeader"",""parent"":""TradeMsg"",""components"":[{""type"":""UnityEngine.UI.Image"",""color"":""0.1 0.1 0.15 0.95"",""imagetype"":""Filled""},{""type"":""RectTransform"",""anchormax"":""1 1"",""anchormin"":""0 0.85""}]},{""name"":""TradeTitle"",""parent"":""TradeHeader"",""components"":[{""type"":""UnityEngine.UI.Text"",""text"":""МЕНЮ ОБМЕНА"",""fontSize"":""18"",""align"":""MiddleCenter"",""color"":""1 1 1 1"",""font"":""robotocondensed-bold.ttf""},{""type"":""RectTransform"",""anchormax"":""1 1"",""anchormin"":""0 0""}]},{""name"":""SourceLabel{1}"",""parent"":""TradeMsg"",""components"":[{""type"":""UnityEngine.UI.Text"",""text"":""{sourcename}"",""fontSize"":""14"",""align"":""MiddleCenter"",""color"":""0.9 0.9 0.9 1"",""font"":""robotocondensed-bold.ttf""},{""type"":""RectTransform"",""anchormax"":""0.48 0.88"",""anchormin"":""0.02 0.85""}]},{""name"":""TargetLabel{2}"",""parent"":""TradeMsg"",""components"":[{""type"":""UnityEngine.UI.Text"",""text"":""{targetname}"",""fontSize"":""14"",""align"":""MiddleCenter"",""color"":""0.9 0.9 0.9 1"",""font"":""robotocondensed-bold.ttf""},{""type"":""RectTransform"",""anchormax"":""0.98 0.88"",""anchormin"":""0.52 0.85""}]},{""name"":""SourceItemsPanel{3}"",""parent"":""TradeMsg"",""components"":[{""type"":""UnityEngine.UI.Image"",""color"":""0.12 0.12 0.15 0.9"",""imagetype"":""Filled""},{""type"":""RectTransform"",""anchormax"":""0.47 0.83"",""anchormin"":""0.02 0.12""}]},{""name"":""SourceItemsText"",""parent"":""SourceItemsPanel{3}"",""components"":[{""type"":""UnityEngine.UI.Text"",""text"":""{sourceitems}"",""fontSize"":""12"",""align"":""UpperLeft"",""color"":""0.9 0.9 0.9 1""},{""type"":""RectTransform"",""anchormax"":""0.98 0.98"",""anchormin"":""0.02 0.02""}]},{""name"":""TargetItemsPanel{4}"",""parent"":""TradeMsg"",""components"":[{""type"":""UnityEngine.UI.Image"",""color"":""0.12 0.12 0.15 0.9"",""imagetype"":""Filled""},{""type"":""RectTransform"",""anchormax"":""0.98 0.83"",""anchormin"":""0.52 0.12""}]},{""name"":""TargetItemsText"",""parent"":""TargetItemsPanel{4}"",""components"":[{""type"":""UnityEngine.UI.Text"",""text"":""{targetitems}"",""fontSize"":""12"",""align"":""UpperLeft"",""color"":""0.9 0.9 0.9 1""},{""type"":""RectTransform"",""anchormax"":""0.98 0.98"",""anchormin"":""0.02 0.02""}]},{""name"":""AcceptTradeButton{5}"",""parent"":""TradeMsg"",""components"":[{""type"":""UnityEngine.UI.Button"",""color"":""0.2 0.8 0.3 0.9"",""command"":""trade.confirm""},{""type"":""RectTransform"",""anchormax"":""0.48 0.08"",""anchormin"":""0.35 0.02""}]},{""name"":""AcceptTradeLabel"",""parent"":""AcceptTradeButton{5}"",""components"":[{""type"":""UnityEngine.UI.Text"",""text"":""✅ ПОДТВЕРДИТЬ"",""fontSize"":""12"",""align"":""MiddleCenter"",""color"":""1 1 1 1"",""font"":""robotocondensed-bold.ttf""},{""type"":""RectTransform"",""anchormax"":""1 1"",""anchormin"":""0 0""}]},{""name"":""DeclineTradeButton{6}"",""parent"":""TradeMsg"",""components"":[{""type"":""UnityEngine.UI.Button"",""color"":""0.8 0.2 0.2 0.9"",""command"":""trade.decline""},{""type"":""RectTransform"",""anchormax"":""0.15 0.08"",""anchormin"":""0.02 0.02""}]},{""name"":""DeclineTradeLabel"",""parent"":""DeclineTradeButton{6}"",""components"":[{""type"":""UnityEngine.UI.Text"",""text"":""❌ ОТМЕНИТЬ"",""color"":""1 1 1 1"",""fontSize"":""12"",""align"":""MiddleCenter"",""font"":""robotocondensed-bold.ttf""},{""type"":""RectTransform"",""anchormax"":""1 1"",""anchormin"":""0 0""}]},{""name"":""TargetStatusLabel{7}"",""parent"":""TradeMsg"",""components"":[{""type"":""UnityEngine.UI.Text"",""text"":""{targetstatus}"",""fontSize"":""11"",""align"":""MiddleCenter"",""color"":""0.8 0.8 0.8 1"",""font"":""robotocondensed-bold.ttf""},{""type"":""RectTransform"",""anchormax"":""0.98 0.1"",""anchormin"":""0.52 0.02""}]},{""name"":""CloseButton"",""parent"":""TradeMsg"",""components"":[{""type"":""UnityEngine.UI.Button"",""color"":""0.8 0.2 0.2 0.9"",""command"":""trade.decline""},{""type"":""RectTransform"",""anchormax"":""0.98 0.98"",""anchormin"":""0.9 0.9""}]},{""name"":""CloseText"",""parent"":""CloseButton"",""components"":[{""type"":""UnityEngine.UI.Text"",""text"":""✕"",""fontSize"":""20"",""align"":""MiddleCenter"",""color"":""1 1 1 1"",""font"":""robotocondensed-bold.ttf""},{""type"":""RectTransform"",""anchormax"":""1 1"",""anchormin"":""0 0""}]}]
";
        private void ShowTrade (BasePlayer player, OpenTrade trade, string status)
        {
            HideTrade (player);

            OnlinePlayer onlinePlayer;
            if (!onlinePlayers.TryGetValue (player, out onlinePlayer)) {
                return;
            }

            if (onlinePlayer.View == null) {
                return;
            }

            StorageContainer sourceContainer = onlinePlayer.View;
            StorageContainer targetContainer = null;
            BasePlayer target = null;

            if (trade.sourcePlayer == player && trade.target.View != null) {
                targetContainer = trade.target.View;
                target = trade.targetPlayer;
                if (target is BasePlayer) {
                    if (trade.targetAccept) {
                        status += string.Format (GetMsg ("Trade: They Accepted", player), CleanName (target.displayName));
                    } else if (trade.sourceAccept) {
                        status += GetMsg ("Trade: You Accepted", player);
                    }
                } else {
                    return;
                }
            } else if (trade.targetPlayer == player && trade.source.View != null) {
                targetContainer = trade.source.View;
                target = trade.sourcePlayer;
                if (target is BasePlayer) {
                    if (trade.sourceAccept) {
                        status += string.Format (GetMsg ("Trade: They Accepted", player), CleanName (target.displayName));
                    } else if (trade.targetAccept) {
                        status += GetMsg ("Trade: You Accepted", player);
                    }
                } else {
                    return;
                }
            }

            if (targetContainer == null || target == null) {
                return;
            }

            string send = jsonTrade;
            for (int i = 1; i < 100; i++) {
                send = send.Replace ("{" + i + "}", Oxide.Core.Random.Range (9999, 99999).ToString ());
            }

            send = send.Replace ("{sourcename}", CleanName (player.displayName));
            if (target != null) {
                send = send.Replace ("{targetname}", CleanName (target.displayName));
            } else {
                send = send.Replace ("{targetname}", GetMsg ("Player: Unknown", player));
            }
            send = send.Replace ("{targetstatus}", status);

            var slotsAvailable = target.inventory.containerMain.capacity - (target.inventory.containerMain.itemList.Count);
            List<string> sourceItems = new List<string> ();
            var x = 1;
            foreach (Item i in sourceContainer.inventory.itemList) {
                string n = "";
                if (i.IsBlueprint ()) {
                    n = i.amount + " x <color=lightblue>" + i.blueprintTargetDef.displayName.english + " [" + GetMsg ("Item: BP", player) + "]</color>";
                } else {
                    n = i.amount + " x " + i.info.displayName.english;
                    if(i.info.condition.enabled)
                    {
                        var conditionPercent = System.Math.Round(i.condition * 100 / i.info.condition.max, 0);
                        if(conditionPercent <= 25f)
                        {
                            n += " [<color=red>" + conditionPercent + "%</color>]";
                        }
                        else if(conditionPercent <= 75f)
                        {
                            n += " [<color=yellow>" + conditionPercent + "%</color>]";
                        }
                        else if(conditionPercent <= 99f)
                        {
                            n += " [<color=green>" + conditionPercent + "%</color>]";
                        }
                    }
                }

                if (x > slotsAvailable) {
                    n = "<color=red>" + n + "</color>";
                }
                x++;

                sourceItems.Add (n);
            }

            send = send.Replace ("{sourceitems}", string.Join ("\n", sourceItems.ToArray ()));

            if (player != target) {
                slotsAvailable = player.inventory.containerMain.capacity - (player.inventory.containerMain.itemList.Count);
                List<string> targetItems = new List<string> ();
                x = 1;
                if (targetContainer != null) {
                    foreach (Item i in targetContainer.inventory.itemList) {
                        string n2 = "";
                        if (i.IsBlueprint ()) {
                            n2 = i.amount + " x <color=lightblue>" + i.blueprintTargetDef.displayName.english + " [" + GetMsg ("Item: BP", player) + "]</color>";
                        } else {
                            n2 = i.amount + " x " + i.info.displayName.english;
                            if (i.info.condition.enabled)
                            {
                                var conditionPercent = System.Math.Round(i.condition * 100 / i.info.condition.max, 0);
                                if (conditionPercent <= 25f)
                                {
                                    n2 += " [<color=red>" + conditionPercent + "%</color>]";
                                }
                                else if (conditionPercent <= 75f)
                                {
                                    n2 += " [<color=yellow>" + conditionPercent + "%</color>]";
                                }
                                else if (conditionPercent <= 99f)
                                {
                                    n2 += " [<color=green>" + conditionPercent + "%</color>]";
                                }
                            }
                        }
                        if (x > slotsAvailable) {
                            n2 = "<color=red>" + n2 + "</color>";
                        }
                        x++;
                        targetItems.Add (n2);
                    }
                }

                send = send.Replace ("{targetitems}", string.Join ("\n", targetItems.ToArray ()));
            } else {
                send = send.Replace ("{targetitems}", "");
            }

            CommunityEntity.ServerInstance.ClientRPCEx (new Network.SendInfo { connection = player.net.connection }, null, "AddUI", send);
        }

        private void HideTrade (BasePlayer player)
        {
            if (player.IsConnected) {
                // Очищаем основное меню торговли
                CommunityEntity.ServerInstance.ClientRPCEx (new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "TradeMsg");
                
                // Очищаем UI от IQSorter
                try
                {
                    // Используем CuiHelper для очистки UI от других плагинов
                    CuiHelper.DestroyUi(player, "IQ_HEADER_PLAYER");
                    CuiHelper.DestroyUi(player, "IQ_HEADER_CONTAINER");
                }
                catch (Exception ex)
                {
                    Puts(string.Format("Error clearing IQSorter UI: {0}", ex.Message));
                }
                
                // НЕ очищаем лут здесь - это может прервать активную торговлю
                // Очистка лута происходит только при завершении торговли
            }
        }

        #endregion

        #region Core Methods

        bool CheckCooldown (BasePlayer player)
        {
            if (cooldownMinutes > 0) {
                DateTime startTime;
                if (tradeCooldowns.TryGetValue (player.UserIDString, out startTime)) {
                    var endTime = DateTime.Now;

                    var span = endTime.Subtract (startTime);
                    if (span.TotalMinutes > 0 && span.TotalMinutes < Convert.ToDouble (cooldownMinutes)) {
                        double timeleft = System.Math.Round (Convert.ToDouble (cooldownMinutes) - span.TotalMinutes, 2);
                        if (timeleft < 1) {
                            double timelefts = System.Math.Round ((Convert.ToDouble (cooldownMinutes) * 60) - span.TotalSeconds);
                            SendReply (player, string.Format (GetMsg ("Cooldown: Seconds", player), timelefts.ToString ()));
                        } else {
                            SendReply (player, string.Format (GetMsg ("Cooldown: Minutes", player), System.Math.Round (timeleft).ToString ()));
                        }
                        return false;
                    } else {
                        tradeCooldowns.Remove (player.UserIDString);
                    }
                }
            }

            return true;
        }

        void TradeCloseBoxes (OpenTrade trade)
        {
            try
            {
                // Принудительно уничтожаем ящики
                if (trade.IsSourceValid () && trade.source.View != null) {
                    ForceDestroyBox(trade.source.View);
                CloseBoxView (trade.sourcePlayer, trade.source.View);
                    
                    // Дополнительная принудительная очистка
                    try
                    {
                        if (trade.source.View is BaseEntity entity)
                        {
                            entity.Kill(BaseNetworkable.DestroyMode.Gib);
                        }
                        if (trade.source.View.net != null)
                        {
                            trade.source.View.net.Destroy();
                        }
                        UnityEngine.Object.Destroy(trade.source.View);
                    }
                    catch (Exception ex)
                    {
                        Puts(string.Format("Error in TradeCloseBoxes source cleanup: {0}", ex.Message));
                    }
                }

                                if (trade.IsTargetValid () && trade.targetPlayer != trade.sourcePlayer && trade.target.View != null) {
                    Puts(string.Format("TradeCloseBoxes: Cleaning target box for player: {0}", trade.targetPlayer?.displayName));
                    
                    ForceDestroyBox(trade.target.View);
                    CloseBoxView (trade.targetPlayer, trade.target.View);
                    
                    // Дополнительная принудительная очистка - БОЛЕЕ АГРЕССИВНАЯ для цели
                    try
                    {
                        if (trade.target.View is BaseEntity entity)
                        {
                            entity.Kill(BaseNetworkable.DestroyMode.Gib);
                        }
                        if (trade.target.View.net != null)
                        {
                            trade.target.View.net.Destroy();
                        }
                        UnityEngine.Object.Destroy(trade.target.View);
                        
                        // Дополнительная очистка через BaseNetworkable
                        if (trade.target.View != null && trade.target.View.net != null)
                        {
                            trade.target.View.net.Destroy();
                        }
                        
                        // Финальная очистка - принудительно уничтожаем через BaseEntity
                        if (trade.target.View is BaseEntity entity2)
                        {
                            entity2.Kill(BaseNetworkable.DestroyMode.Gib);
                        }
                        
                        // Принудительно удаляем из мира через Unity
                        UnityEngine.Object.Destroy(trade.target.View);
                    }
                    catch (Exception ex)
                    {
                        Puts(string.Format("Error in TradeCloseBoxes target cleanup: {0}", ex.Message));
                    }
                }
            
            // Скрываем UI кнопки принятия торговли
            if (trade.sourcePlayer != null && trade.sourcePlayer.IsConnected)
                HideTradeAcceptButton(trade.sourcePlayer);
            if (trade.targetPlayer != null && trade.targetPlayer.IsConnected)
                HideTradeAcceptButton(trade.targetPlayer);
                    
                // Очищаем ссылки
                if (trade.source != null)
                {
                    trade.source.View = null;
                    trade.source.Trade = null;
                }
                if (trade.target != null)
                {
                    trade.target.View = null;
                    trade.target.Trade = null;
                }
            }
            catch (Exception ex)
            {
                Puts(string.Format("Error in TradeCloseBoxes: {0}", ex.Message));
            }
        }

        void TradeReply (OpenTrade trade, string msg, string msg2 = null)
        {
            if (msg2 == null) {
                msg2 = msg;
            }

            if (trade.targetPlayer != null) {
                SendReply (trade.targetPlayer, GetMsg (msg, trade.targetPlayer));
            }

            if (trade.sourcePlayer != null) {
                SendReply (trade.sourcePlayer, GetMsg (msg2, trade.sourcePlayer));
            }

        }

        void ShowTrades (OpenTrade trade, string msg)
        {
            Puts(string.Format("ShowTrades called with message: {0}", msg));
            Puts(string.Format("Source player: {0}, Target player: {1}", trade.sourcePlayer?.displayName, trade.targetPlayer?.displayName));
            
            ShowTrade (trade.sourcePlayer, trade, GetMsg (msg, trade.sourcePlayer));
            ShowTrade (trade.targetPlayer, trade, GetMsg (msg, trade.targetPlayer));
        }

        void TradeCooldown (OpenTrade trade)
        {
            PlayerCooldown (trade.targetPlayer);
            PlayerCooldown (trade.sourcePlayer);
        }

        void PlayerCooldown (BasePlayer player)
        {
            if (player.IsAdmin) {
                return;
            }
            if (tradeCooldowns.ContainsKey (player.UserIDString)) {
                tradeCooldowns.Remove (player.UserIDString);
            }

            tradeCooldowns.Add (player.UserIDString, DateTime.Now);
        }

        void FinishTrade (OpenTrade t)
        {
            try
            {
                // Перемещаем предметы от источника к цели
                foreach (var item in t.source.View.inventory.itemList.ToArray())
                {
                    if (item != null)
                    {
                        item.MoveToContainer(t.target.containerMain);
                    }
                }

                // Перемещаем предметы от цели к источнику
                foreach (var item in t.target.View.inventory.itemList.ToArray())
                {
                    if (item != null)
                    {
                        item.MoveToContainer(t.source.containerMain);
                    }
                }

                // Принудительно удаляем ящики и очищаем торговлю
                ForceCleanupTrade(t);
                
                // Дополнительная принудительная очистка в FinishTrade - ОДИНАКОВАЯ для обоих
                try
                {
                    // Очищаем ящик источника (того, кто отправлял запрос)
                    if (t.source != null && t.source.View != null)
                    {
                        Puts(string.Format("FinishTrade: Cleaning up source box for player: {0} at position: {1}", t.sourcePlayer?.displayName, t.source.View.transform.position));
                        
                        // Дополнительная очистка источника
                        if (t.source.View is BaseEntity entity)
                        {
                            entity.Kill(BaseNetworkable.DestroyMode.Gib);
                            Puts("FinishTrade: Source box BaseEntity kill completed");
                        }
                        if (t.source.View.net != null)
                        {
                            t.source.View.net.Destroy();
                            Puts("FinishTrade: Source box network destroy completed");
                        }
                        UnityEngine.Object.Destroy(t.source.View);
                        Puts("FinishTrade: Source box Unity destroy completed");
                        
                        // Дополнительная очистка через BaseNetworkable
                        if (t.source.View != null && t.source.View.net != null)
                        {
                            t.source.View.net.Destroy();
                            Puts("FinishTrade: Source box additional network destroy completed");
                        }
                        
                        // Финальная очистка источника
                        if (t.source.View is BaseEntity entity2)
                        {
                            entity2.Kill(BaseNetworkable.DestroyMode.Gib);
                            Puts("FinishTrade: Source box final BaseEntity kill completed");
                        }
                        UnityEngine.Object.Destroy(t.source.View);
                        Puts("FinishTrade: Source box final Unity destroy completed");
                    }
                    
                    // Очищаем ящик цели (того, кто принимал торговлю) - ОСОБЕННО ВНИМАТЕЛЬНО
                    if (t.target != null && t.target.View != null)
                    {
                        Puts(string.Format("FinishTrade: Cleaning up target box for player: {0} at position: {1}", t.targetPlayer?.displayName, t.target.View.transform.position));
                        
                        // Дополнительная очистка цели - БОЛЕЕ АГРЕССИВНАЯ
                        if (t.target.View is BaseEntity entity)
                        {
                            entity.Kill(BaseNetworkable.DestroyMode.Gib);
                            Puts("FinishTrade: Target box BaseEntity kill completed");
                        }
                        if (t.target.View.net != null)
                        {
                            t.target.View.net.Destroy();
                            Puts("FinishTrade: Target box network destroy completed");
                        }
                        UnityEngine.Object.Destroy(t.target.View);
                        Puts("FinishTrade: Target box Unity destroy completed");
                        
                        // Дополнительная очистка через BaseNetworkable
                        if (t.target.View != null && t.target.View.net != null)
                        {
                            t.target.View.net.Destroy();
                            Puts("FinishTrade: Target box additional network destroy completed");
                        }
                        
                        // Финальная очистка - принудительно уничтожаем через BaseEntity
                        if (t.target.View is BaseEntity entity2)
                        {
                            entity2.Kill(BaseNetworkable.DestroyMode.Gib);
                            Puts("FinishTrade: Target box final BaseEntity kill completed");
                        }
                        
                        // Принудительно удаляем из мира через Unity
                        UnityEngine.Object.Destroy(t.target.View);
                        Puts("FinishTrade: Target box final Unity destroy completed");
                        
                        // Дополнительная финальная очистка цели
                        if (t.target.View is BaseEntity entity3)
                        {
                            entity3.Kill(BaseNetworkable.DestroyMode.Gib);
                            Puts("FinishTrade: Target box additional final BaseEntity kill completed");
                        }
                        UnityEngine.Object.Destroy(t.target.View);
                        Puts("FinishTrade: Target box additional final Unity destroy completed");
                    }
                }
                catch (Exception ex)
                {
                    Puts(string.Format("Error in FinishTrade additional cleanup: {0}", ex.Message));
                }
            }
            catch (Exception ex)
            {
                Puts(string.Format("Error in FinishTrade: {0}", ex.Message));
                ForceCleanupTrade(t);
            }
        }

        void ForceCleanupTrade (OpenTrade t)
        {
            try
            {
                // Принудительно закрываем все UI
                if (t.sourcePlayer != null && t.sourcePlayer.IsConnected)
                {
                    HideTrade(t.sourcePlayer);
                    HideTradeAcceptButton(t.sourcePlayer);
                }
                if (t.targetPlayer != null && t.targetPlayer.IsConnected)
                {
                    HideTrade(t.targetPlayer);
                    HideTradeAcceptButton(t.targetPlayer);
                }

                // Принудительно удаляем ящики
                if (t.source != null && t.source.View != null)
                {
                    ForceDestroyBox(t.source.View);
                    // Дополнительная очистка
                    if (t.source.View.net != null)
                    {
                        t.source.View.net.Destroy();
                    }
                    
                    // Дополнительная принудительная очистка
                    try
                    {
                        if (t.source.View is BaseEntity entity)
                        {
                            entity.Kill(BaseNetworkable.DestroyMode.Gib);
                        }
                        if (t.source.View.net != null)
                        {
                            t.source.View.net.Destroy();
                        }
                        UnityEngine.Object.Destroy(t.source.View);
                    }
                    catch (Exception ex)
                    {
                        Puts(string.Format("Error in additional cleanup source: {0}", ex.Message));
                    }
                }
                if (t.target != null && t.target.View != null)
                {
                    Puts(string.Format("ForceCleanupTrade: Cleaning target box for player: {0}", t.targetPlayer?.displayName));
                    
                    ForceDestroyBox(t.target.View);
                    // Дополнительная очистка
                    if (t.target.View.net != null)
                    {
                        t.target.View.net.Destroy();
                    }
                    
                    // Дополнительная принудительная очистка - БОЛЕЕ АГРЕССИВНАЯ для цели
                    try
                    {
                        if (t.target.View is BaseEntity entity)
                        {
                            entity.Kill(BaseNetworkable.DestroyMode.Gib);
                        }
                        if (t.target.View.net != null)
                        {
                            t.target.View.net.Destroy();
                        }
                        UnityEngine.Object.Destroy(t.target.View);
                        
                        // Дополнительная очистка через BaseNetworkable
                        if (t.target.View != null && t.target.View.net != null)
                        {
                            t.target.View.net.Destroy();
                        }
                        
                        // Финальная очистка - принудительно уничтожаем через BaseEntity
                        if (t.target.View is BaseEntity entity2)
                        {
                            entity2.Kill(BaseNetworkable.DestroyMode.Gib);
                        }
                        
                        // Принудительно удаляем из мира через Unity
                        UnityEngine.Object.Destroy(t.target.View);
                    }
                    catch (Exception ex)
                    {
                        Puts(string.Format("Error in additional cleanup target: {0}", ex.Message));
                    }
                }
                
                // Дополнительная очистка лута для всех игроков
                foreach (var player in BasePlayer.activePlayerList)
                {
                    if (player.inventory.loot.entitySource != null)
                    {
                        // Проверяем, не связан ли лут с торговыми ящиками
                        bool isTradeBox = false;
                        if (t.source != null && t.source.View != null && player.inventory.loot.entitySource == t.source.View)
                            isTradeBox = true;
                        if (t.target != null && t.target.View != null && player.inventory.loot.entitySource == t.target.View)
                            isTradeBox = true;
                            
                        if (isTradeBox)
                        {
                            Puts(string.Format("ForceCleanupTrade: Cleaning loot for player: {0}", player.displayName));
                            
                            // Принудительно закрываем лут и очищаем ссылки
                            player.EndLooting();
                            player.inventory.loot.entitySource = null;
                            player.inventory.loot.itemSource = null;
                            player.inventory.loot.containers.Clear();
                            
                            // Дополнительно очищаем UI
                            try
                            {
                                player.SendConsoleCommand("inventory.endloot");
                                Puts(string.Format("ForceCleanupTrade: Sent endloot command to player: {0}", player.displayName));
                            }
                            catch (Exception ex)
                            {
                                Puts(string.Format("Error sending endloot command: {0}", ex.Message));
                            }
                        }
                    }
                }
                
                // Финальная принудительная очистка всех торговых ящиков
                try
                {
                    if (t.source != null && t.source.View != null)
                    {
                        Puts(string.Format("ForceCleanupTrade: Final cleanup for source box at position: {0}", t.source.View.transform.position));
                        
                        // Принудительно уничтожаем через BaseEntity
                        if (t.source.View is BaseEntity entity)
                        {
                            entity.Kill(BaseNetworkable.DestroyMode.Gib);
                            Puts("ForceCleanupTrade: Source box BaseEntity kill completed");
                        }
                        
                        // Принудительно уничтожаем через сеть
                        if (t.source.View.net != null)
                        {
                            t.source.View.net.Destroy();
                            Puts("ForceCleanupTrade: Source box network destroy completed");
                        }
                        
                        // Принудительно уничтожаем через Unity
                        UnityEngine.Object.Destroy(t.source.View);
                        Puts("ForceCleanupTrade: Source box Unity destroy completed");
                    }
                    
                    if (t.target != null && t.target.View != null)
                    {
                        Puts(string.Format("ForceCleanupTrade: Final cleanup for target box at position: {0}", t.target.View.transform.position));
                        
                        // Принудительно уничтожаем через BaseEntity
                        if (t.target.View is BaseEntity entity)
                        {
                            entity.Kill(BaseNetworkable.DestroyMode.Gib);
                            Puts("ForceCleanupTrade: Target box BaseEntity kill completed");
                        }
                        
                        // Принудительно уничтожаем через сеть
                        if (t.target.View.net != null)
                        {
                            t.target.View.net.Destroy();
                            Puts("ForceCleanupTrade: Target box network destroy completed");
                        }
                        
                        // Принудительно уничтожаем через Unity
                        UnityEngine.Object.Destroy(t.target.View);
                        Puts("ForceCleanupTrade: Target box Unity destroy completed");
                    }
                }
                catch (Exception ex)
                {
                    Puts(string.Format("Error in ForceCleanupTrade final cleanup: {0}", ex.Message));
                }

                // Очищаем ссылки
                if (t.source != null)
                {
                    t.source.View = null;
                    t.source.Trade = null;
                }
                if (t.target != null)
                {
                    t.target.View = null;
                    t.target.Trade = null;
                }

                // Удаляем из списка открытых торгов
                if (openTrades.Contains(t))
                {
                    openTrades.Remove(t);
                }
            }
            catch (Exception ex)
            {
                Puts(string.Format("Error in ForceCleanupTrade: {0}", ex.Message));
            }
        }

                void ForceDestroyBox (StorageContainer box)
        {
            try
            {
                if (box == null) return;

                Puts(string.Format("ForceDestroyBox: Starting aggressive cleanup for box at position: {0}", box.transform.position));

                // Принудительно закрываем лут для всех игроков
                foreach (var player in BasePlayer.activePlayerList)
                {
                    if (player.inventory.loot.entitySource == box)
                    {
                        // Проверяем, не является ли это активной торговлей
                        bool isActiveTrade = false;
                        if (onlinePlayers.ContainsKey(player))
                        {
                            var playerData = onlinePlayers[player];
                            if (playerData.Trade != null && !playerData.Trade.complete)
                            {
                                isActiveTrade = true;
                            }
                        }
                        
                        // Закрываем лут только если это не активная торговля
                        if (!isActiveTrade)
                        {
                            player.EndLooting();
                            // Принудительно очищаем ссылки на лут
                            player.inventory.loot.entitySource = null;
                            player.inventory.loot.itemSource = null;
                            player.inventory.loot.containers.Clear();
                        }
                    }
                }

                // Очищаем инвентарь ящика
                if (box.inventory != null)
                {
                    foreach (var item in box.inventory.itemList.ToArray())
                    {
                        if (item != null)
                        {
                            item.Remove();
                        }
                    }
                    // Принудительно очищаем инвентарь
                    box.inventory.itemList.Clear();
                }

                // Агрессивное уничтожение ящика - используем несколько методов
                if (box.IsDestroyed == false)
                {
                    Puts(string.Format("ForceDestroyBox: Box not destroyed, starting aggressive cleanup"));
                    
                    // 1. Убиваем объект
                    box.Kill(BaseNetworkable.DestroyMode.Gib);
                    Puts("ForceDestroyBox: Step 1 - Kill completed");
                    
                    // 2. Очищаем сеть
                    if (box.net != null)
                    {
                        box.net.Destroy();
                        Puts("ForceDestroyBox: Step 2 - Network destroy completed");
                    }
                    
                    // 3. Принудительно удаляем из мира через Unity
                    UnityEngine.Object.Destroy(box);
                    Puts("ForceDestroyBox: Step 3 - Unity destroy completed");
                    
                    // 4. Дополнительная очистка через BaseNetworkable
                    if (box.net != null)
                    {
                        box.net.Destroy();
                        Puts("ForceDestroyBox: Step 4 - Additional network destroy completed");
                    }
                    
                    // 5. Финальная очистка - принудительно уничтожаем через BaseEntity
                    if (box is BaseEntity entity)
                    {
                        entity.Kill(BaseNetworkable.DestroyMode.Gib);
                        Puts("ForceDestroyBox: Step 5 - BaseEntity kill completed");
                    }
                    
                    // 6. Дополнительная очистка через BaseNetworkable
                    if (box.net != null)
                    {
                        box.net.Destroy();
                        Puts("ForceDestroyBox: Step 6 - Additional network destroy completed");
                    }
                    
                    // 7. Финальная очистка - принудительно уничтожаем через BaseEntity
                    if (box is BaseEntity entity2)
                    {
                        entity2.Kill(BaseNetworkable.DestroyMode.Gib);
                        Puts("ForceDestroyBox: Step 7 - Additional BaseEntity kill completed");
                    }
                    
                    // 8. Принудительно удаляем из мира через Unity
                    UnityEngine.Object.Destroy(box);
                    Puts("ForceDestroyBox: Step 8 - Final Unity destroy completed");
                    
                    // 9. Дополнительная очистка через BaseNetworkable
                    if (box.net != null)
                    {
                        box.net.Destroy();
                        Puts("ForceDestroyBox: Step 9 - Final network destroy completed");
                    }
                    
                    // 10. Финальная очистка - принудительно уничтожаем через BaseEntity
                    if (box is BaseEntity entity3)
                    {
                        entity3.Kill(BaseNetworkable.DestroyMode.Gib);
                        Puts("ForceDestroyBox: Step 10 - Final BaseEntity kill completed");
                    }
                    
                    // 11. Принудительно удаляем из мира через Unity
                    UnityEngine.Object.Destroy(box);
                    Puts("ForceDestroyBox: Step 11 - Final Unity destroy completed");
                }
                else
                {
                    Puts("ForceDestroyBox: Box already destroyed, skipping cleanup");
                }
                
                Puts(string.Format("ForceDestroyBox: Aggressive cleanup completed for box"));
            }
            catch (Exception ex)
            {
                Puts(string.Format("Error in ForceDestroyBox: {0}", ex.Message));
            }
        }

        void AcceptTrade (BasePlayer player)
        {
            // Отладочная информация
            Puts(string.Format("AcceptTrade called for player: {0}", player.displayName));
            Puts(string.Format("Pending trades count: {0}", pendingTrades.Count));
            
            BasePlayer source = null;

            PendingTrade pendingTrade = null;

            foreach (KeyValuePair<BasePlayer, PendingTrade> kvp in pendingTrades) {
                Puts(string.Format("Checking pending trade: source={0}, target={1}", kvp.Key?.displayName, kvp.Value?.Target?.displayName));
                if (kvp.Value.Target == player) {
                    pendingTrade = kvp.Value;
                    source = kvp.Key;
                    Puts(string.Format("Found pending trade: source={0}, target={1}", source?.displayName, pendingTrade?.Target?.displayName));
                    break;
                }
            }

            if (source != null && pendingTrade != null) {
                Puts(string.Format("Starting trade between {0} and {1}", source.displayName, player.displayName));
                pendingTrade.Destroy ();
                pendingTrades.Remove (source);
                
                // Скрываем UI кнопку принятия торговли
                HideTradeAcceptButton(player);
                
                StartTrades (source, player);
            } else {
                Puts(string.Format("No pending trade found for {0}", player.displayName));
                SendReply (player, GetMsg ("Status: No Pending", player));
            }
        }

        void StartTrades (BasePlayer source, BasePlayer target)
        {
            var trade = new OpenTrade (onlinePlayers [source], onlinePlayers [target]);
            StartTrade (source, target, trade);
            if (source != target) {
                StartTrade (target, source, trade);
            }

        }

        void StartTrade (BasePlayer source, BasePlayer target, OpenTrade trade)
        {
            OpenBox (source, source);

            if (!openTrades.Contains (trade)) {
                openTrades.Add (trade);
            }
            onlinePlayers [source].Trade = trade;

            timer.In (0.1f, () => ShowTrade (source, trade, GetMsg ("Trade: Pending", source)));
        }

        void OpenBox (BasePlayer player, BaseEntity target)
        {
            SubscribeAll ();
            var ply = onlinePlayers [player];
            if (ply.View == null) {
                OpenBoxView (player, target);
                return;
            }

            CloseBoxView (player, ply.View);
            timer.In (1f, () => OpenBoxView (player, target));
        }

        void OpenBoxView (BasePlayer player, BaseEntity targArg)
        {
            try
            {
                var pos = new Vector3(player.transform.position.x, player.transform.position.y - 1, player.transform.position.z);
                var boxContainer = GameManager.server.CreateEntity(box, pos) as StorageContainer;
                
                if (boxContainer == null) return;
                
                boxContainer.GetComponent<DestroyOnGroundMissing>().enabled = false;
                boxContainer.GetComponent<GroundWatch>().enabled = false;
                boxContainer.transform.position = pos;

            StorageContainer view = boxContainer as StorageContainer;
            view.limitNetworking = true;
                player.EndLooting();

                if (targArg is BasePlayer)
                {
                BasePlayer target = targArg as BasePlayer;
                view.CreateInventory(true);
                view.inventory.playerOwner = player;
                    view.inventory.ServerInitialize(null, slots);

                view.enableSaving = false;
                    view.Spawn();

                    onlinePlayers[player].View = view;
                    timer.In(0.1f, () => 
                    {
                        try
                        {
                            if (view != null)
                            {
                                view.PlayerOpenLoot(player);
                            }
                        }
                        catch (Exception ex)
                        {
                            Puts(string.Format("Error opening loot: {0}", ex.Message));
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Puts(string.Format("Error in OpenBoxView: {0}", ex.Message));
            }
        }

        void CloseBoxView (BasePlayer player, StorageContainer view)
        {
            try
            {
            OnlinePlayer onlinePlayer;
                if (!onlinePlayers.TryGetValue(player, out onlinePlayer)) return;
            if (onlinePlayer.View == null) return;

                HideTrade(player);
                
                // Дополнительно очищаем UI от IQSorter при закрытии ящиков
                try
                {
                    CuiHelper.DestroyUi(player, "IQ_HEADER_PLAYER");
                    CuiHelper.DestroyUi(player, "IQ_HEADER_CONTAINER");
                }
                catch (Exception ex)
                {
                                            Puts(string.Format("Error clearing IQSorter UI in CloseBoxView: {0}", ex.Message));
                }
                if (onlinePlayer.Trade != null)
                {
                OpenTrade t = onlinePlayer.Trade;
                t.closing = true;

                    if (t.sourcePlayer == player && t.targetPlayer != player && t.target.View != null)
                    {
                    t.target.Trade = null;
                        CloseBoxView(t.targetPlayer, t.target.View);
                    }
                    else if (t.targetPlayer == player && t.sourcePlayer != player && t.source.View != null)
                    {
                    t.source.Trade = null;
                        CloseBoxView(t.sourcePlayer, t.source.View);
                    }

                    if (openTrades.Contains(t))
                    {
                        openTrades.Remove(t);
                    }
                }

                // Возвращаем предметы игроку
                if (view.inventory.itemList.Count > 0)
                {
                    foreach (Item item in view.inventory.itemList.ToArray())
                    {
                        if (item != null && item.position != -1)
                        {
                            try
                            {
                                item.MoveToContainer(player.inventory.containerMain);
                            }
                            catch
                            {
                                // Если не помещается в основной инвентарь, пробуем в пояс
                                try
                                {
                                    item.MoveToContainer(player.inventory.containerBelt);
                                }
                                catch
                                {
                                    // Если и в пояс не помещается, дропаем на землю
                                    item.Drop(player.transform.position, Vector3.zero);
                                }
                            }
                        }
                    }
                }

                // Закрываем лут
                if (player.inventory.loot.entitySource != null)
                {
                    try
                    {
                        player.inventory.loot.Invoke("SendUpdate", 0.1f);
                        view.SendMessage("PlayerStoppedLooting", player, SendMessageOptions.DontRequireReceiver);
                        player.SendConsoleCommand("inventory.endloot", null);
                    }
                    catch (Exception ex)
                    {
                        Puts(string.Format("Error closing loot: {0}", ex.Message));
                    }
            }

            player.inventory.loot.entitySource = null;
            player.inventory.loot.itemSource = null;
                player.inventory.loot.containers = new List<ItemContainer>();

                onlinePlayer.Clear();

                if (view != null)
                {
                    // Принудительно уничтожаем ящик полностью
                    try
                    {
                        // Сначала закрываем лут для всех игроков
                        foreach (var p in BasePlayer.activePlayerList)
                        {
                            if (p.inventory.loot.entitySource == view)
                            {
                                // Проверяем, не является ли это активной торговлей
                                bool isActiveTrade = false;
                                if (onlinePlayers.ContainsKey(p))
                                {
                                    var playerData = onlinePlayers[p];
                                    if (playerData.Trade != null && !playerData.Trade.complete)
                                    {
                                        isActiveTrade = true;
                                    }
                                }
                                
                                // Закрываем лут только если это не активная торговля
                                if (!isActiveTrade)
                                {
                                    p.EndLooting();
                                    // Принудительно очищаем ссылки на лут
                                    p.inventory.loot.entitySource = null;
                                    p.inventory.loot.itemSource = null;
                                    p.inventory.loot.containers.Clear();
                                }
                            }
                        }
                        
                        // Очищаем инвентарь ящика
                        if (view.inventory != null)
                        {
                            foreach (var item in view.inventory.itemList.ToArray())
                            {
                                if (item != null)
                                {
                                    item.Remove();
                                }
                            }
                            // Принудительно очищаем инвентарь
                            view.inventory.itemList.Clear();
                        }
                        
                        // Уничтожаем ящик полностью
                        view.Kill(BaseNetworkable.DestroyMode.Gib);
                        
                        // Дополнительная очистка сети
                        if (view.net != null)
                        {
                            view.net.Destroy();
                        }
                        
                        // Принудительное удаление из мира - используем несколько методов для гарантии
                        if (view.IsDestroyed == false)
                        {
                            // 1. Убиваем объект
                            view.Kill(BaseNetworkable.DestroyMode.Gib);
                            
                            // 2. Принудительно удаляем через Unity
                            UnityEngine.Object.Destroy(view);
                            
                            // 3. Дополнительная очистка через BaseNetworkable
                            if (view.net != null)
                            {
                                view.net.Destroy();
                            }
                            
                            // 4. Финальная очистка - принудительно уничтожаем через BaseEntity
                            if (view is BaseEntity entity)
                            {
                                entity.Kill(BaseNetworkable.DestroyMode.Gib);
                            }
                        }
                        
                        // Дополнительная принудительная очистка
                        try
                        {
                            if (view is BaseEntity entity2)
                            {
                                entity2.Kill(BaseNetworkable.DestroyMode.Gib);
                            }
                            if (view.net != null)
                            {
                                view.net.Destroy();
                            }
                            UnityEngine.Object.Destroy(view);
                            
                            // Дополнительная очистка через BaseNetworkable
                            if (view != null && view.net != null)
                            {
                                view.net.Destroy();
                            }
                            
                            // Финальная очистка - принудительно уничтожаем через BaseEntity
                            if (view is BaseEntity entity3)
                            {
                                entity3.Kill(BaseNetworkable.DestroyMode.Gib);
                            }
                            
                            // Принудительно удаляем из мира через Unity
                            UnityEngine.Object.Destroy(view);
                        }
                        catch (Exception ex)
                        {
                            Puts(string.Format("Error in additional cleanup view: {0}", ex.Message));
                        }
                    }
                    catch (Exception ex)
                    {
                        Puts(string.Format("Error destroying view: {0}", ex.Message));
                    }
                }
            }
            catch (Exception ex)
            {
                Puts(string.Format("Error in CloseBoxView: {0}", ex.Message));
            }
        }

        bool CanPlayerTrade (BasePlayer player, string perm)
        {
            if (!permission.UserHasPermission (player.UserIDString, perm)) {
                SendReply (player, GetMsg ("Denied: Permission", player));
                return false;
            }

            if (!player.CanBuild ()) {
                SendReply (player, GetMsg ("Denied: Privilege", player));
                return false;
            }

            if (radiationMax > 0 && player.radiationLevel > radiationMax) {
                SendReply (player, GetMsg ("Denied: Irradiated", player));
                return false;
            }

            if (player.IsSwimming ()) {
                SendReply (player, GetMsg ("Denied: Swimming", player));
                return false;
            }

            // Убираем проверку на падение для удобства использования
            // if (!player.IsOnGround () || player.IsFlying || player.isInAir) {
            //     SendReply (player, GetMsg ("Denied: Falling", player));
            //     return false;
            // }

            if (player.isMounted) {
                SendReply (player, GetMsg ("Denied: Mounted", player));
                return false;
            }

            if (player.IsWounded ()) {
                SendReply (player, GetMsg ("Denied: Wounded", player));
                return false;
            }

            if (player.GetComponentInParent<CargoShip> ()) {
                SendReply (player, GetMsg ("Denied: Ship", player));
                return false;
            }

            if (player.GetComponentInParent<HotAirBalloon> ()) {
                SendReply (player, GetMsg ("Denied: Balloon", player));
                return false;
            }

            if (player.GetComponentInParent<Lift> ()) {
                SendReply (player, GetMsg ("Denied: Lift", player));
                return false;
            }

            if (!allowSafeZone && player.InSafeZone ()) {
                SendReply (player, GetMsg ("Denied: Safe Zone", player));
                return false;
            }

            var canTrade = Interface.Call ("CanTrade", player);
            if (canTrade != null) {
                if (canTrade is string) {
                    SendReply (player, Convert.ToString (canTrade));
                } else {
                    SendReply (player, GetMsg ("Denied: Generic", player));
                }
                return false;
            }

            return true;
        }

        #endregion

        #region HelpText
        private void SendHelpText (BasePlayer player)
        {
            var sb = new StringBuilder ()
               .Append ("Trade by <color=#ce422b>http://rustservers.io</color>\n")
               .Append ("  ").Append ("<color=\"#ffd479\">/trade \"Player Name\"</color> - Send trade request").Append ("\n")
               .Append ("  ").Append ("<color=\"#ffd479\">/trade accept</color> - Accept trade request").Append ("\n");
            player.ChatMessage (sb.ToString ());
        }
        #endregion

        #region Helper methods

        private bool IsTradeBox (BaseNetworkable entity)
        {
            foreach (KeyValuePair<BasePlayer, OnlinePlayer> kvp in onlinePlayers) {
                if (kvp.Value.View != null && kvp.Value.View.net != null && entity.net != null && kvp.Value.View.net.ID == entity.net.ID) {
                    return true;
                }
            }

            return false;
        }

        bool hasAccess (BasePlayer player, string permissionname)
        {
            if (player.IsAdmin) return true;
            return permission.UserHasPermission (player.UserIDString, permissionname);
        }

        private BasePlayer FindPlayerByPartialName (string name)
        {
            if (string.IsNullOrEmpty (name))
                return null;
            BasePlayer player = null;
            name = name.ToLower ();
            var awakePlayers = BasePlayer.activePlayerList.ToArray ();
            foreach (var p in awakePlayers) {
                if (p.net == null || p.net.connection == null)
                    continue;

                if (p.displayName == name) {
                    if (player != null)
                        return null;
                    player = p;
                }
            }

            if (player != null)
                return player;
            foreach (var p in awakePlayers) {
                if (p.net == null || p.net.connection == null)
                    continue;

                if (p.displayName.ToLower ().IndexOf (name) >= 0) {
                    if (player != null)
                        return null;
                    player = p;
                }
            }

            return player;
        }

        private T GetConfig<T> (string name, T defaultValue)
        {
            if (Config [name] == null) {
                return defaultValue;
            }

            return (T)Convert.ChangeType (Config [name], typeof (T));
        }

        private T GetConfig<T> (string name, string name2, T defaultValue)
        {
            if (Config [name, name2] == null) {
                return defaultValue;
            }

            return (T)Convert.ChangeType (Config [name, name2], typeof (T));
        }

        string GetMsg (string key, BasePlayer player = null)
        {
            return lang.GetMessage (key, this, player == null ? null : player.UserIDString);
        }

        private string CleanName (string name)
        {
            return JsonConvert.ToString (name.Trim ()).Replace ("\"", "");
        }

        #endregion

        #region Player Select Menu

        private void ShowPlayerSelectMenu(BasePlayer player)
        {
            if (player == null || !player.IsConnected) return;

            try
            {
                // Сначала очищаем меню
                HidePlayerSelectMenu(player);
                
                // Сбрасываем страницу для игрока
                playerMenuPages[player.UserIDString] = 0;
                
                // Получаем ТОЛЬКО онлайн игроков, исключая самого игрока
                var players = new List<BasePlayer>();
                foreach (var p in BasePlayer.activePlayerList)
                {
                    if (p != null && p.IsConnected && p != player && !p.IsSleeping())
                    {
                        players.Add(p);
                    }
                }

                if (players.Count == 0)
                {
                    SendReply(player, "Нет доступных игроков для торговли");
                    return;
                }

                // Создаем JSON для UI с постраничной навигацией
                var menuJson = CreatePlayerSelectMenuJson(players, 0);
                
                // Показываем UI
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", menuJson);
            }
            catch (Exception ex)
            {
                Puts(string.Format("Error showing player select menu: {0}", ex.Message));
                SendReply(player, "Ошибка при открытии меню выбора игроков");
            }
        }

        private string CreatePlayerSelectMenuJson(List<BasePlayer> players, int currentPage)
        {
            try
            {
                int totalPages = (int)Math.Ceiling((double)players.Count / PlayersPerPage);
                int startIndex = currentPage * PlayersPerPage;
                int endIndex = Math.Min(startIndex + PlayersPerPage, players.Count);
                
                // Полностью новый простой JSON для меню
                var json = @"[{""name"":""PlayerMenu"",""parent"":""Overlay"",""components"":[{""type"":""UnityEngine.UI.Image"",""color"":""0 0 0 0.8"",""imagetype"":""Filled""},{""type"":""RectTransform"",""anchormax"":""0.6 0.8"",""anchormin"":""0.4 0.2""}]},{""name"":""MenuTitle"",""parent"":""PlayerMenu"",""components"":[{""type"":""UnityEngine.UI.Text"",""text"":""Онлайн игроки"",""fontSize"":""18"",""align"":""MiddleCenter"",""color"":""1 1 1 1""},{""type"":""RectTransform"",""anchormax"":""1 0.95"",""anchormin"":""0 0.85""}]},{""name"":""CloseBtn"",""parent"":""PlayerMenu"",""components"":[{""type"":""UnityEngine.UI.Button"",""color"":""0.8 0.2 0.2 0.9"",""command"":""trade.close""},{""type"":""RectTransform"",""anchormax"":""0.95 0.95"",""anchormin"":""0.85 0.85""}]},{""name"":""CloseText"",""parent"":""CloseBtn"",""components"":[{""type"":""UnityEngine.UI.Text"",""text"":""X"",""fontSize"":""16"",""align"":""MiddleCenter"",""color"":""1 1 1 1""},{""type"":""RectTransform"",""anchormax"":""1 1"",""anchormin"":""0 0""}]}";

                // Добавляем информацию о странице
                json += string.Format(",{{\"name\":\"PageInfo\",\"parent\":\"PlayerMenu\",\"components\":[{{\"type\":\"UnityEngine.UI.Text\",\"text\":\"Страница {0} из {1}\",\"fontSize\":\"12\",\"align\":\"MiddleCenter\",\"color\":\"0.8 0.8 0.8 1\"}},{{\"type\":\"RectTransform\",\"anchormax\":\"1 0.82\",\"anchormin\":\"0 0.78\"}}]}}", currentPage + 1, totalPages);

                // Добавляем кнопки навигации
                if (totalPages > 1)
                {
                    // Кнопка "Предыдущая страница"
                    if (currentPage > 0)
                    {
                        json += string.Format(",{{\"name\":\"PrevPageBtn\",\"parent\":\"PlayerMenu\",\"components\":[{{\"type\":\"UnityEngine.UI.Button\",\"color\":\"0.3 0.6 0.9 0.9\",\"command\":\"trade.prevpage\"}},{{\"type\":\"RectTransform\",\"anchormax\":\"0.25 0.15\",\"anchormin\":\"0.05 0.05\"}}]}},{{\"name\":\"PrevPageText\",\"parent\":\"PrevPageBtn\",\"components\":[{{\"type\":\"UnityEngine.UI.Text\",\"text\":\"← Назад\",\"fontSize\":\"12\",\"align\":\"MiddleCenter\",\"color\":\"1 1 1 1\"}},{{\"type\":\"RectTransform\",\"anchormax\":\"1 1\",\"anchormin\":\"0 0\"}}]}}");
                    }

                    // Кнопка "Следующая страница"
                    if (currentPage < totalPages - 1)
                    {
                        json += string.Format(",{{\"name\":\"NextPageBtn\",\"parent\":\"PlayerMenu\",\"components\":[{{\"type\":\"UnityEngine.UI.Button\",\"color\":\"0.3 0.6 0.9 0.9\",\"command\":\"trade.nextpage\"}},{{\"type\":\"RectTransform\",\"anchormax\":\"0.95 0.15\",\"anchormin\":\"0.75 0.05\"}}]}},{{\"name\":\"NextPageText\",\"parent\":\"NextPageBtn\",\"components\":[{{\"type\":\"UnityEngine.UI.Text\",\"text\":\"Вперед →\",\"fontSize\":\"12\",\"align\":\"MiddleCenter\",\"color\":\"1 1 1 1\"}},{{\"type\":\"RectTransform\",\"anchormax\":\"1 1\",\"anchormin\":\"0 0\"}}]}}");
                    }
                }

                // Добавляем игроков для текущей страницы
                for (int i = startIndex; i < endIndex; i++)
                {
                    var playerButton = players[i];
                    var buttonName = string.Format("Btn{0}", i - startIndex);
                    var yPos = 0.75 - ((i - startIndex) * 0.07);
                    
                    json += string.Format(",{{\"name\":\"{0}\",\"parent\":\"PlayerMenu\",\"components\":[{{\"type\":\"UnityEngine.UI.Button\",\"color\":\"0.3 0.3 0.3 0.9\",\"command\":\"trade.selectplayer {1}\"}},{{\"type\":\"RectTransform\",\"anchormax\":\"0.9 {2}\",\"anchormin\":\"0.1 {3}\"}}]}},{{\"name\":\"{0}Text\",\"parent\":\"{0}\",\"components\":[{{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{4}\",\"fontSize\":\"14\",\"align\":\"MiddleCenter\",\"color\":\"1 1 1 1\"}},{{\"type\":\"RectTransform\",\"anchormax\":\"1 1\",\"anchormin\":\"0 0\"}}]}}", buttonName, playerButton.UserIDString, yPos, yPos - 0.06, playerButton.displayName);
                }

                json += "]";
                return json;
            }
            catch (Exception ex)
            {
                Puts(string.Format("Error creating player select menu JSON: {0}", ex.Message));
                return "[]";
            }
        }

        private void HidePlayerSelectMenu(BasePlayer player)
        {
            if (player?.IsConnected == true)
            {
                try
                {
                    // Очищаем новое меню
                    CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "PlayerMenu");
                    
                    // Очищаем UI от IQSorter
                    CuiHelper.DestroyUi(player, "IQ_HEADER_PLAYER");
                    CuiHelper.DestroyUi(player, "IQ_HEADER_CONTAINER");
                }
                catch (Exception ex)
                {
                    Puts(string.Format("Error hiding player select menu: {0}", ex.Message));
                }
            }
        }

        [ConsoleCommand("trade.selectplayer")]
        void ccTradeSelectPlayer(ConsoleSystem.Arg arg)
        {
            if (arg?.Connection?.player == null) return;
            
            var player = arg.Connection.player as BasePlayer;
            if (player == null) return;

            try
            {
                if (arg.Args == null || arg.Args.Length == 0)
                {
                    SendReply(player, "Ошибка: не указан ID игрока");
                    HidePlayerSelectMenu(player);
                    return;
                }

                var targetUserId = arg.Args[0];
                var targetPlayer = BasePlayer.activePlayerList.FirstOrDefault(p => p.UserIDString == targetUserId && p.IsConnected);

                if (targetPlayer == null)
                {
                    SendReply(player, GetMsg("Player: Not Found", player));
                    HidePlayerSelectMenu(player);
                    return;
                }

                // Закрываем меню и начинаем торговлю
                HidePlayerSelectMenu(player);
                
                // Вызываем команду торговли с выбранным игроком
                var args = new string[] { targetPlayer.displayName };
                cmdTrade(player, "trade", args);
            }
            catch (Exception ex)
            {
                Puts(string.Format("Error in trade select player command: {0}", ex.Message));
                if (player != null)
                {
                    SendReply(player, "Ошибка при выборе игрока");
                }
            }
        }

        [ConsoleCommand("trade.close")]
        void ccTradeClose(ConsoleSystem.Arg arg)
        {
            if (arg?.Connection?.player == null) return;
            
            var player = arg.Connection.player as BasePlayer;
            if (player == null) return;
            
            // Отладочная информация
            Puts(string.Format("Trade close command called by player: {0}", player.displayName));
            
            try
            {
                HidePlayerSelectMenu(player);
            }
            catch (Exception ex)
            {
                Puts(string.Format("Error closing menu: {0}", ex.Message));
            }
        }

        private void RefreshPlayerSelectMenu(BasePlayer player)
        {
            if (player == null || !player.IsConnected) return;

            try
            {
                // Получаем текущую страницу для игрока
                int currentPage = 0;
                if (playerMenuPages.ContainsKey(player.UserIDString))
                    currentPage = playerMenuPages[player.UserIDString];

                // Получаем список игроков
                var players = new List<BasePlayer>();
                foreach (var p in BasePlayer.activePlayerList)
                {
                    if (p != null && p.IsConnected && p != player && !p.IsSleeping())
                    {
                        players.Add(p);
                    }
                }

                if (players.Count == 0)
                {
                    HidePlayerSelectMenu(player);
                    SendReply(player, "Нет доступных игроков для торговли");
                    return;
                }

                // Создаем JSON для UI с текущей страницей
                var menuJson = CreatePlayerSelectMenuJson(players, currentPage);
                
                // Сначала удаляем старое меню
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "PlayerMenu");
                
                // Показываем обновленное меню
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", menuJson);
            }
            catch (Exception ex)
            {
                Puts(string.Format("Error refreshing player select menu: {0}", ex.Message));
                SendReply(player, "Ошибка при обновлении меню");
            }
        }

        [ConsoleCommand("trade.prevpage")]
        void ccTradePrevPage(ConsoleSystem.Arg arg)
        {
            if (arg?.Connection?.player == null) return;
            
            var player = arg.Connection.player as BasePlayer;
            if (player == null) return;
            
            try
            {
                if (!playerMenuPages.ContainsKey(player.UserIDString))
                    playerMenuPages[player.UserIDString] = 0;
                
                if (playerMenuPages[player.UserIDString] > 0)
                {
                    playerMenuPages[player.UserIDString]--;
                    RefreshPlayerSelectMenu(player);
                }
            }
            catch (Exception ex)
            {
                Puts(string.Format("Error in prev page command: {0}", ex.Message));
            }
        }

        [ConsoleCommand("trade.nextpage")]
        void ccTradeNextPage(ConsoleSystem.Arg arg)
        {
            if (arg?.Connection?.player == null) return;
            
            var player = arg.Connection.player as BasePlayer;
            if (player == null) return;
            
            try
            {
                if (!playerMenuPages.ContainsKey(player.UserIDString))
                    playerMenuPages[player.UserIDString] = 0;
                
                // Получаем список игроков для проверки общего количества страниц
                var players = new List<BasePlayer>();
                foreach (var p in BasePlayer.activePlayerList)
                {
                    if (p != null && p.IsConnected && p != player && !p.IsSleeping())
                    {
                        players.Add(p);
                    }
                }
                
                int totalPages = (int)Math.Ceiling((double)players.Count / PlayersPerPage);
                
                if (playerMenuPages[player.UserIDString] < totalPages - 1)
                {
                    playerMenuPages[player.UserIDString]++;
                    RefreshPlayerSelectMenu(player);
                }
            }
            catch (Exception ex)
            {
                Puts(string.Format("Error in next page command: {0}", ex.Message));
            }
        }

        #endregion

        #region Trade Accept UI Button

        private void ShowTradeAcceptButton(BasePlayer player, string sourcePlayerName)
        {
            if (player == null || !player.IsConnected) return;

            try
            {
                // Отладочная информация
                Puts(string.Format("Showing trade accept button for player: {0}, source: {1}", player.displayName, sourcePlayerName));
                
                var cui = new CuiElementContainer();
                
                // Основная панель кнопки - серый полупрозрачный фон
                cui.Add(new CuiPanel
                {
                    Image = { Color = "0.3 0.3 0.3 0.8" },
                    RectTransform = { AnchorMin = "0.02 0.02", AnchorMax = "0.2 0.08" }
                }, "Overlay", "TradeAcceptUI");
                
                // Убираем верхнюю синюю границу - оставляем только кнопку
                
                // Левая зеленая полоска
                cui.Add(new CuiPanel
                {
                    Image = { Color = "0 0.8 0.4 1" },
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "0.01 1" }
                }, "TradeAcceptUI", "TradeAcceptLeftBorder");
                
                // Правая зеленая полоска
                cui.Add(new CuiPanel
                {
                    Image = { Color = "0 0.8 0.4 1" },
                    RectTransform = { AnchorMin = "0.99 0", AnchorMax = "1 1" }
                }, "TradeAcceptUI", "TradeAcceptRightBorder");
                
                // Кликабельная кнопка
                cui.Add(new CuiButton
                {
                    Button = { Color = "0 0 0 0", Command = "trade.accept.request" },
                    RectTransform = { AnchorMin = "0.02 0", AnchorMax = "0.98 1" },
                    Text = { Text = "" }
                }, "TradeAcceptUI", "TradeAcceptButton");
                
                // Иконка галочки
                cui.Add(new CuiLabel
                {
                    Text = { Text = "✅", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1", Font = "robotocondensed-bold.ttf" },
                    RectTransform = { AnchorMin = "0.05 0.2", AnchorMax = "0.25 0.8" }
                }, "TradeAcceptButton", "TradeAcceptIcon");
                
                // Основной текст в одну строку
                cui.Add(new CuiLabel
                {
                    Text = { Text = "ПРИНЯТЬ ТОРГОВЛЮ", FontSize = 10, Align = TextAnchor.MiddleLeft, Color = "0.95 0.95 0.95 1", Font = "robotocondensed-bold.ttf" },
                    RectTransform = { AnchorMin = "0.32 0.25", AnchorMax = "0.92 0.75" }
                }, "TradeAcceptButton", "TradeAcceptText");

                CuiHelper.AddUi(player, cui);
            }
            catch (Exception ex)
            {
                Puts(string.Format("Error showing trade accept button: {0}", ex.Message));
            }
        }

        private void HideTradeAcceptButton(BasePlayer player)
        {
            if (player?.IsConnected == true)
            {
                try
                {
                    CuiHelper.DestroyUi(player, "TradeAcceptUI");
                }
                catch (Exception ex)
                {
                    Puts(string.Format("Error hiding trade accept button: {0}", ex.Message));
                }
            }
        }

        #endregion
    }
}
