using Network;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using UnityEngine;


/*
 * Скачано с дискорд сервера Rust Edit [PRO+]
 * discord.gg/9vyTXsJyKR
*/

namespace Oxide.Plugins
{
    [Info ( "StaticTrader", "discord.gg/9vyTXsJyKR", "1.2.6" )]
    [Description ( "A smart trading system for friends and family." )]
    public class StaticTrader : RustPlugin
    {
        public const bool Debug = true;
        public static StaticTrader Instance { get; set; }

        public List<Trade> ActiveTrades { get; set; } = new List<Trade> ();
        public Dictionary<ulong, float> CooledDownPlayers { get; set; } = new Dictionary<ulong, float> ();
        public Dictionary<ulong, KeyValuePair<string, string>> PlayersSteamInfo { get; set; } = new Dictionary<ulong, KeyValuePair<string, string>> ();
        public Dictionary<ulong, KeyValuePair<int, string>> PlayerTradeListFilter { get; set; } = new Dictionary<ulong, KeyValuePair<int, string>> ();

        public Trade GetTrade ( BasePlayer sentBy, BasePlayer sentTo )
        {
            foreach ( var x in ActiveTrades )
            {
                if ( x.Target == sentTo && x.Initiator == sentBy ) return x;
            }

            return null;
        }
        public Trade GetTrade ( BasePlayer sentTo )
        {
            foreach ( var x in ActiveTrades )
            {
                if ( x.Target == sentTo ) return x;
            }

            return null;
        }
        public Trade GetTrade ( ShopFront shopFront )
        {
            foreach ( var x in ActiveTrades )
            {
                if ( x.ShopFront == shopFront ) return x;
            }

            return null;
        }
        public Trade GetTrade ( ItemContainer feeContainer )
        {
            foreach ( var x in ActiveTrades )
            {
                if ( x.FeePaymentContainer == feeContainer ) return x;
            }

            return null;
        }
        public bool CanSentTrade ( BasePlayer sentBy, BasePlayer sentTo, out string reason )
        {
            reason = null;

            if ( !Debug )
            {
                if ( sentBy == sentTo )
                {
                    reason = GetPhrase ( "trade_help_noself", sentBy );
                    return false;
                }
            }

            if ( IsCooledDown ( sentBy, false ) )
            {
                reason = $"You're in cooldown.\n<size=12><color=orange>{CooldownSecondsLeft ( sentBy ):0}</color> seconds left.</size>";
                return false;
            }

            if ( !sentTo.IsConnected || sentTo.IsDead () || sentTo.IsSleeping () )
            {
                reason = $"<color=orange>{sentTo.displayName}</color> is dead, sleeping or not online.";
                return false;
            }

            if ( GetTrade ( sentBy, sentTo ) != null || GetTrade ( sentTo, sentBy ) != null )
            {
                reason = "There's already a trade sent between you two.";
                return false;
            }

            if ( !Config.Rules.CanTradeBuildingBlocked && !sentBy.CanBuild () )
            {
                reason = "You're building-blocked.";
                return false;
            }

            if ( !Config.Rules.CanTradeBuildingBlocked && !sentTo.CanBuild () )
            {
                reason = $"<color=orange>{sentTo.displayName}</color> is building-blocked.";
                return false;
            }

            if ( NoEscape != null )
            {
                if ( !Config.Rules.CanTradeCombatBlocked && NoEscape.Call<bool> ( "IsCombatBlocked", sentBy ) )
                {
                    reason = "You're combat-blocked.";
                    return false;
                }

                if ( !Config.Rules.CanTradeRaidBlocked && NoEscape.Call<bool> ( "IsRaidBlocked", sentBy ) )
                {
                    reason = "You're raid-blocked.";
                    return false;
                }

                if ( !Config.Rules.CanTradeCombatBlocked && NoEscape.Call<bool> ( "IsCombatBlocked", sentTo ) )
                {
                    reason = $"<color=orange>{sentTo.displayName}</color> is combat-blocked.";
                    return false;
                }

                if ( !Config.Rules.CanTradeRaidBlocked && NoEscape.Call<bool> ( "IsRaidBlocked", sentTo ) )
                {
                    reason = $"<color=orange>{sentTo.displayName}</color> is raid-blocked.";
                    return false;
                }
            }

            return true;
        }
        public bool IsTradingShopFront ( ShopFront shopFront )
        {
            foreach ( var x in ActiveTrades )
            {
                if ( x.ShopFront == shopFront ) return true;
            }

            return false;
        }
        public bool IsCooledDown ( BasePlayer player, bool coolDownPlayer = true )
        {
            foreach ( var group in Config.GroupCooldowns )
            {
                if ( permission.UserHasGroup ( player.UserIDString, group.Key ) )
                {
                    if ( CooledDownPlayers.ContainsKey ( player.userID ) )
                    {
                        if ( Time.realtimeSinceStartup - CooledDownPlayers [ player.userID ] > group.Value )
                        {
                            if ( coolDownPlayer )
                            {
                                CooledDownPlayers [ player.userID ] = Time.realtimeSinceStartup;
                                return true;
                            }
                            else
                            {
                                CooledDownPlayers.Remove ( player.userID );
                                return false;
                            }
                        }
                        else
                        {
                            return true;
                        }
                    }
                    else if ( coolDownPlayer )
                    {
                        CooledDownPlayers.Add ( player.userID, Time.realtimeSinceStartup );
                        return true;
                    }
                }
            }

            return false;
        }
        public float CooldownSecondsLeft ( BasePlayer player )
        {
            if ( IsCooledDown ( player, false ) )
            {
                foreach ( var group in Config.GroupCooldowns )
                {
                    if ( permission.UserHasGroup ( player.UserIDString, group.Key ) )
                    {
                        return group.Value - ( Time.realtimeSinceStartup - CooledDownPlayers [ player.userID ] );
                    }
                }
            }

            return 0f;
        }

        public const string InviteEffect = "assets/bundled/prefabs/fx/invite_notice.prefab";
        public const string SuccessEffect = "assets/prefabs/building/wall.frame.shopfront/effects/metal_transaction_complete.prefab";
        public const string OpenEffect = "assets/prefabs/deployable/locker/effects/locker-deploy.prefab";

        #region Permissions

        public const string UsePerm = "StaticTrader.use";

        public void InstallPermissions ()
        {
            permission.RegisterPermission ( UsePerm, this );
        }

        private bool HasPermission ( BasePlayer player, string perm, bool quiet = false )
        {
            if ( !permission.UserHasPermission ( player.UserIDString, perm ) )
            {
                if ( !quiet ) Print ( $"You need to have the \"{perm}\" permission.", player );
                return false;
            }

            return true;
        }

        #endregion

        #region Hooks

        private void OnServerInitialized ()
        {
            Instance = this;

            InstallPermissions ();
            InstallCommands ();

            Loaded ();

            RefreshPlugins ();
        }
        private void Loaded ()
        {
            if ( Instance == null ) return;

            if ( ConfigFile == null ) ConfigFile = new Core.Configuration.DynamicConfigFile ( $"{Manager.ConfigPath}{Path.DirectorySeparatorChar}{Name}.json" );

            if ( !ConfigFile.Exists () )
            {
                ConfigFile.WriteObject ( Config = new RootConfig () );
            }
            else
            {
                try
                {
                    Config = ConfigFile.ReadObject<RootConfig> ();
                }
                catch ( Exception exception )
                {
                    Puts ( $"Broken configuration: {exception.Message}" );
                }
            }

            foreach ( var player in BasePlayer.activePlayerList )
            {
                FetchProfileInfo ( player.userID );
            }
        }
        private void Unload ()
        {
            foreach ( var player in BasePlayer.activePlayerList )
            {
                ClearAllUI ( player );
            }

            foreach ( var trade in ActiveTrades )
            {
                trade?.Clear ( true );
            }

            ActiveTrades.Clear ();
        }
        private void OnPluginLoaded ( Plugin name )
        {
            RefreshPlugins ();
        }
        private void OnPluginUnloaded ( Plugin name )
        {
            RefreshPlugins ();
        }
        private object OnShopCompleteTrade ( ShopFront entity )
        {
            if ( !IsTradingShopFront ( entity ) ) return null;

            var trade = GetTrade ( entity );
            trade.IsSuccessful = true;

            TakePlayerCurrency ( trade.Target, trade.Fee );

            timer.In ( 1f, () =>
            {
                var vendor = entity.vendorPlayer;
                if ( vendor == null ) return;
                var customer = entity.customerPlayer;
                if ( customer == null ) return;

                vendor.inventory.loot.Clear ();
                vendor.inventory.loot.SendImmediate ();
                customer.inventory.loot.Clear ();
                customer.inventory.loot.SendImmediate ();

                SendEffectTo ( SuccessEffect, trade.Initiator );
                SendEffectTo ( SuccessEffect, trade.Target );

                trade.Clear ();
                IsCooledDown ( trade.Initiator, true );
                IsCooledDown ( trade.Target, true );
            } );

            return null;
        }
        private object OnEntityVisibilityCheck ( BaseEntity ent, BasePlayer player, uint id, string debugName, float maximumDistance )
        {
            var shopFront = ent as ShopFront;
            if ( shopFront == null ) return null;

            if ( IsTradingShopFront ( shopFront ) )
            {
                return true;
            }

            return null;
        }
        private object CanLootPlayer ( BasePlayer looted, BasePlayer looter )
        {
            var trade = GetTrade ( looter );
            if ( trade != null && trade.FeePaymentContainer == looter.inventory.loot.containers [ 0 ] )
            {
                return true;
            }

            return null;
        }
        private void OnPlayerLootEnd ( PlayerLoot playerLoot )
        {
            if ( playerLoot.containers.Count == 0 ) return;

            var player = playerLoot.baseEntity;
            var trade = GetTrade ( playerLoot.containers [ 0 ] );

            if ( trade != null )
            {
                var feeContainer = trade.FeePaymentContainer;

                if ( feeContainer.itemList.Count == 0 )
                {
                    Print ( GetPhrase ( "trade_cancelled", trade.Initiator ), trade.Initiator );
                    Print ( GetPhrase ( "trade_cancelled", trade.Target ), trade.Target );
                    trade.Clear ( true );
                    ActiveTrades.Remove ( trade );
                }
                else
                {
                    var item = feeContainer.itemList [ 0 ];

                    if ( item.amount < trade.Fee )
                    {
                        Print ( $"{GetPhrase ( "trade_failed", player )} {GetPhrase ( "trade_failed_nomoney", player, Config.Fee.GetName () )}", player );
                        player.GiveItem ( item );
                        trade.Clear ( true );
                        ActiveTrades.Remove ( trade );
                    }
                    else
                    {
                        timer.In ( 0.2f, () =>
                        {
                            Instance.SendEffectTo ( OpenEffect, trade.Target );
                            Instance.SendEffectTo ( OpenEffect, trade.Initiator );
                            trade.Start ();
                        } );
                    }
                }

                return;
            }

            var shopFront = playerLoot.containers [ 0 ]?.entityOwner as ShopFront;
            if ( shopFront != null && IsTradingShopFront ( shopFront ) )
            {
                trade = GetTrade ( shopFront );

                if ( !trade.IsSuccessful )
                {
                    Print ( GetPhrase ( "trade_cancelled", trade.Initiator ), trade.Initiator );
                    Print ( GetPhrase ( "trade_cancelled", trade.Target ), trade.Target );

                    if ( trade.FeePaymentContainer != null && trade.FeePaymentContainer.itemList.Count != 0 )
                        player.GiveItem ( trade.FeePaymentContainer.itemList [ 0 ] );

                    trade.Clear ( true );
                }
                else
                {
                    Print ( GetPhrase ( "trade_successful", trade.Initiator ), trade.Initiator );
                    Print ( GetPhrase ( "trade_successful", trade.Target ), trade.Target );
                    trade.Clear ();
                }

                ActiveTrades.Remove ( trade );
            }
        }
        private void OnServerSave ()
        {
            if ( Instance == null ) return;

            if ( Config != null ) ConfigFile.WriteObject ( Config );
        }
        private void OnPlayerConnected ( BasePlayer player )
        {
            FetchProfileInfo ( player.userID );
        }

        #endregion

        #region CUI

        public const string PendingInviteUI = "HmmThisPluginIsReallyCoolLetsNotLeakIt";
        public const string TeamListUI = "HmmThisPluginIsReallyCoolLetsNotLeakItPls";
        public const int HeightOffset = 1000;

        public void ClearAllUI ( BasePlayer player )
        {
            ClearPendingInviteUI ( player );
            ClearTeamListUI ( player, false );
        }

        public void ClearPendingInviteUI ( BasePlayer player )
        {
            for ( int i = 0; i < 5; i++ )
            {
                CuiHelper.DestroyUi ( player, PendingInviteUI );
            }
        }
        public void DrawPendingInviteUI ( BasePlayer player, Trade trade )
        {
            if ( PlayerTradeListFilter.ContainsKey ( player.userID ) ) return;

            ClearPendingInviteUI ( player );

            var other = trade.Initiator == player ? trade.Target : trade.Initiator;
            var container = new CuiElementContainer ();
            var background = container.Add ( new CuiPanel { Image = { Color = $"0 0 0 0" }, RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"0 -{HeightOffset}", OffsetMax = $"0 -{HeightOffset}" }, CursorEnabled = false }, "Hud", PendingInviteUI );
            var fee = Config.Fee.CurrencyType == RootConfig.CurrencyTypes.None ? "" : $" (<b>{Config.Fee.GetValueName ( trade.Fee, false )}</b>)";
            var panel = container.Add ( new CuiPanel { Image = { Color = $"0.15 0.15 0.15 0.6", Material = "assets/content/ui/uibackgroundblur.mat" }, RectTransform = { AnchorMin = "0.4 0.175", AnchorMax = "0.6 0.275", OffsetMin = $"{Config.UINoticeXOffset} {HeightOffset + Config.UINoticeYOffset}", OffsetMax = $"{Config.UINoticeXOffset} {HeightOffset + Config.UINoticeYOffset}" } }, background );
            panel = container.Add ( new CuiPanel { Image = { Color = $"0.15 0.15 0.15 0.6" }, RectTransform = { AnchorMin = "0.02 0.1", AnchorMax = "0.975 0.9" } }, panel );

            container.Add ( new CuiLabel { Text = { Text = $"<b>{other.displayName}</b> has sent you a trading invite.", FontSize = 12, Font = "robotocondensed-regular.ttf", Align = TextAnchor.UpperCenter, Color = "1 1 1 0.8" }, RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 0.85" } }, panel );
            container.Add ( new CuiButton { Button = { Command = $"{Config.AcceptTradeCommand} {trade.Initiator.userID}", Color = "0.3 0.7 0.2 0.8" }, RectTransform = { AnchorMin = $"0.05 0.15", AnchorMax = "0.495 0.5" }, Text = { Text = $"Accept Invite{fee}", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 10 } }, panel );
            container.Add ( new CuiButton { Button = { Command = $"{Config.DeclineTradeCommand} {trade.Initiator.userID}", Color = "0.7 0.3 0.2 0.8" }, RectTransform = { AnchorMin = $"0.505 0.15", AnchorMax = "0.95 0.5" }, Text = { Text = "Decline Invite", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 10 } }, panel );

            CuiHelper.AddUi ( player, container );
        }

        public void ClearTeamListUI ( BasePlayer player, bool refresh )
        {
            for ( int i = 0; i < 5; i++ )
            {
                CuiHelper.DestroyUi ( player, TeamListUI );
            }

            if ( !refresh )
            {
                UnlockPlayer ( player );
                PlayerTradeListFilter.Remove ( player.userID );
            }
        }
        public void DrawTeamListUI ( BasePlayer player )
        {
            ClearTeamListUI ( player, true );

            if ( !PlayerTradeListFilter.ContainsKey ( player.userID ) )
            {
                PlayerTradeListFilter.Add ( player.userID, new KeyValuePair<int, string> ( 0, string.Empty ) );
            }

            var settings = PlayerTradeListFilter [ player.userID ];

            var container = new CuiElementContainer ();
            var teamMembers = player.Team?.members;
            var background = container.Add ( new CuiPanel { Image = { Color = $"0 0 0 0" }, RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"0 -{HeightOffset}", OffsetMax = $"0 -{HeightOffset}" }, CursorEnabled = true }, "Hud", TeamListUI );
            var panel = container.Add ( new CuiPanel { Image = { Color = $"0.15 0.15 0.15 0.6", Material = "assets/content/ui/uibackgroundblur.mat" }, RectTransform = { AnchorMin = "0.3 0.175", AnchorMax = "0.7 0.375", OffsetMin = $"{Config.UINoticeXOffset} {HeightOffset + Config.UINoticeYOffset}", OffsetMax = $"{Config.UINoticeXOffset} {HeightOffset + Config.UINoticeYOffset}" } }, background );
            panel = container.Add ( new CuiPanel { Image = { Color = $"0.15 0.15 0.15 0.6" }, RectTransform = { AnchorMin = "0.01 0.05", AnchorMax = "0.9875 0.95" } }, panel );

            var buttonOffset = 0f;
            var buttonOffsetSpacing = 25f;
            container.Add ( new CuiLabel { Text = { Text = $"SELECT A <b>PLAYER</b> TO <b>TRADE</b> WITH", FontSize = 14, Font = "robotocondensed-regular.ttf", Align = TextAnchor.UpperLeft, Color = "1 1 1 0.8" }, RectTransform = { AnchorMin = $"0.015 0", AnchorMax = $"1 0.925" } }, panel );
            container.Add ( new CuiButton { Button = { Command = $"closeteamlist", Color = "0.9 0.3 0.2 0.8" }, RectTransform = { AnchorMin = $"0.935 0.775", AnchorMax = "0.98 0.93" }, Text = { Text = $"<b>X</b>", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 10 } }, panel );
            container.Add ( new CuiButton { Button = { Command = $"pageteamlist 1", Color = "0.9 0.8 0.3 0.8" }, RectTransform = { AnchorMin = $"0.935 0.775", AnchorMax = "0.98 0.93", OffsetMin = $"{buttonOffset -= buttonOffsetSpacing} 0", OffsetMax = $"{buttonOffset} 1" }, Text = { Text = $"<b>></b>", Color = "0 0 0 1", Align = TextAnchor.MiddleCenter, FontSize = 12 } }, panel );
            container.Add ( new CuiButton { Button = { Command = $"pageteamlist -1", Color = "0.9 0.8 0.3 0.8" }, RectTransform = { AnchorMin = $"0.935 0.775", AnchorMax = "0.98 0.93", OffsetMin = $"{buttonOffset -= buttonOffsetSpacing} 0", OffsetMax = $"{buttonOffset} 1" }, Text = { Text = $"<b><</b>", Color = "0 0 0 1", Align = TextAnchor.MiddleCenter, FontSize = 12 } }, panel );

            var players = Facepunch.Pool.GetList<BasePlayer> ();

            if ( teamMembers != null )
            {
                foreach ( var member in teamMembers )
                {
                    var p = BasePlayer.FindByID ( member );
                    if ( !Debug && p == player ) continue;

                    if ( !p.IsConnected || ( !string.IsNullOrEmpty ( settings.Value ) && !p.displayName.ToLower ().Contains ( settings.Value.ToLower ().Trim () ) ) ) continue;

                    players.Add ( p );
                }
            }

            if ( !Config.TeamOnlyTradingList )
            {
                foreach ( var p in BasePlayer.activePlayerList.Where ( x => teamMembers == null ? true : !teamMembers.Contains ( x.userID ) ).OrderBy ( x => x.displayName ) )
                {
                    if ( !Debug && ( p == player || players.Contains ( p ) ) ) continue;
                    if ( !string.IsNullOrEmpty ( settings.Value ) && !p.displayName.ToLower ().Contains ( settings.Value.ToLower ().Trim () ) ) continue;

                    players.Add ( p );
                }
            }

            var totalPages = ( int )Math.Ceiling ( ( double )players.Count / 5 );
            PlayerTradeListFilter [ player.userID ] = new KeyValuePair<int, string> ( settings.Key < 0 ? totalPages - 1 : settings.Key >= totalPages ? 0 : settings.Key, settings.Value );
            settings = PlayerTradeListFilter [ player.userID ];

            var searchBar = container.Add ( new CuiPanel { Image = { Color = $"0.15 0.15 0.15 0.85" }, RectTransform = { AnchorMin = $"0.55 0.775", AnchorMax = "0.83 0.93" } }, panel );
            container.Add ( new CuiLabel { Text = { Text = $"Search player...", FontSize = 10, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleLeft, Color = "1 1 1 0.3" }, RectTransform = { AnchorMin = $"0.03 0", AnchorMax = $"1 1" } }, searchBar );
            container.Add ( new CuiElement
            {
                Parent = searchBar,
                Components =
                    {
                        new CuiInputFieldComponent { Text = "", FontSize = 10, Command = $"filterteamlist", Align = TextAnchor.MiddleLeft, Color = "1 1 1 1", CharsLimit = 20 },
                        new CuiRectTransformComponent { AnchorMin = "0.03 0", AnchorMax = "1 1" }
                    }
            } );

            var pagePlayers = players.Skip ( 6 * settings.Key ).Take ( 6 );
            var length = 1f / 3.1f;
            var slotXOffset = 5f;
            var slotYOffset = -37.5f;
            var slotXOffsetSpacing = 165f;
            var slotYOffsetSpacing = 50f;
            if ( pagePlayers.Any () )
            {
                for ( int i = 0; i < 6; i++ )
                {
                    var valid = i <= pagePlayers.Count () - 1;
                    var slot = container.Add ( new CuiPanel { Image = { Color = $"0.05 0.05 0.05 {( valid ? 0.6 : 0.3 )}" }, RectTransform = { AnchorMin = "0 0.7", AnchorMax = $"{length} 1", OffsetMin = $"{slotXOffset} {slotYOffset}", OffsetMax = $"{slotXOffset} {slotYOffset + 1}" } }, panel );

                    if ( i <= pagePlayers.Count () - 1 )
                    {
                        var p = pagePlayers.ElementAt ( i );
                        var info = FetchProfileInfo ( p.userID );
                        var isTeamMate = teamMembers == null ? false : teamMembers.Contains ( p.userID );

                        container.Add ( new CuiElement { Parent = slot, Components = { Death.GetRawImage ( info.Key, color: $"1 1 1 0.8" ), new CuiRectTransformComponent { AnchorMin = "0.03 0.15", AnchorMax = "0.2 0.85" } } } );
                        container.Add ( new CuiLabel { Text = { Text = $"<b>{info.Value}</b>", FontSize = 14, Font = "robotocondensed-regular.ttf", Align = TextAnchor.UpperLeft, Color = "1 1 1 0.8" }, RectTransform = { AnchorMin = $"0.25 0", AnchorMax = $"1 0.85" } }, slot );
                        container.Add ( new CuiLabel { Text = { Text = isTeamMate ? "Team Member" : $"{p.userID}", FontSize = 10, Font = "robotocondensed-regular.ttf", Align = TextAnchor.LowerLeft, Color = isTeamMate ? "0.3 0.9 0.2 0.5" : "0.75 0.75 0.75 0.5" }, RectTransform = { AnchorMin = $"0.25 0.2", AnchorMax = $"1 1" } }, slot );
                        container.Add ( new CuiButton { Button = { Command = $"{Config.TradeCommand} {p.userID}", Color = "0 0 0 0" }, RectTransform = { AnchorMin = $"0 0", AnchorMax = "1 1" }, Text = { Color = "0 0 0 0" } }, slot );
                    }

                    slotXOffset += slotXOffsetSpacing;

                    if ( i % 3 == 2 )
                    {
                        slotXOffset = 5f;
                        slotYOffset -= slotYOffsetSpacing - 5f;
                    }
                }
            }
            else container.Add ( new CuiLabel { Text = { Text = Config.TeamOnlyTradingList && player.Team == null ? "You're not in a team." : $"No players have been found.", FontSize = 8, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.2" }, RectTransform = { AnchorMin = $"0 0.05", AnchorMax = $"1 0.75" } }, panel );

            CuiHelper.AddUi ( player, container );

            pagePlayers = null;
            Facepunch.Pool.FreeList ( ref players );

            LockPlayer ( player );
        }

        public class Death
        {
            public static string GetImage ( string url, bool isPng = true, ulong skin = 0 )
            {
                try
                {
                    var name = GetStringChecksum ( url );

                    if ( string.IsNullOrEmpty ( url ) ) return string.Empty;

                    if ( ( bool )Instance.ImageLibrary.Call ( "HasImage", name, skin ) )
                    {
                        var success = Instance.ImageLibrary.Call ( "GetImage", name, skin );
                        return !isPng ? null : ( string )success;
                    }
                    else
                    {
                        Instance.ImageLibrary.Call ( "AddImage", url, name, skin );
                        return !isPng ? url : null;
                    }
                }
                catch { }

                return string.Empty;
            }

            public static CuiRawImageComponent GetRawImage ( string url, ulong skin = 0, string color = "", float fade = 0.3f )
            {
                var _url = GetImage ( url, isPng: false, skin: skin );
                var _png = GetImage ( url, skin: skin );

                return new CuiRawImageComponent
                {
                    Url = _url,
                    Png = _png,
                    Color = color,
                    FadeIn = fade
                };
            }
        }

        public static string GetStringChecksum ( string value )
        {
            if ( value.Length <= 3 ) return null;

            using ( var md5 = MD5.Create () )
            {
                var hash = md5.ComputeHash ( System.Text.Encoding.UTF8.GetBytes ( value ) );
                return BitConverter.ToString ( hash ).Replace ( "-", "" ).ToLowerInvariant ();
            }
        }

        #endregion

        #region Lang

        protected override void LoadDefaultMessages ()
        {
            var defaultMessages = new Dictionary<string, string> ()
            {
                [ "trade_sent" ] = "You've sent a trade request to <color=orange>{0}</color>.",
                [ "trade_received" ] = "<color=orange>{0}</color> sent you a trade request.",
                [ "trade_payment" ] = "You'll pay a fee of <color=orange>{0}</color> when the trade is successful.",
                [ "trade_notrade" ] = "There's no trade exchanged between you and that player/any player.",
                [ "trade_title" ] = "<b>{0}</b> has sent you a trading invite.",
                [ "trade_accept" ] = "Accept Invite",
                [ "trade_decline" ] = "Decline Invite",
                [ "trade_expired" ] = "The trade between you and <color=orange>{0}</color> has expired.",
                [ "trade_expired2" ] = "The trade between <color=orange>{0}</color> and you has expired.",
                [ "trade_declined" ] = "<color=orange>{0}</color> has <color=red>declined</color> your trade request.",
                [ "trade_declined2" ] = "Trade with <color=orange>{0}</color> has been <color=red>declined</color>.",
                [ "trade_nomoneys" ] = "You don't have enough <color=orange>{0}</color> to pay the trading fee.",
                [ "trade_nomoneys2" ] = "<color=orange>{0}</color> doesn't have enough <color=orange>{1}</color> to pay the trading fee.",
                [ "trade_cancelled" ] = "Trade <color=red>cancelled</color>.",
                [ "trade_successful" ] = "Trade <color=green>successful</color>.",
                [ "trade_failed" ] = "Trade <color=red>failed</color>.",
                [ "trade_failed_nomoney" ] = "Not enough <color=orange>{0}</color> for the processing fee.",
                [ "trade_help_accept" ] = "<size=10>Do <color=green>/atrade</color> or <color=green>/atrade {0}</color> to accept it. Do <color=red>/dtrade</color> to decline.</size>",
                [ "trade_help_noname" ] = "Please enter the player name you'd like to trade with.",
                [ "trade_help_noplayer" ] = "That player doesn't exist.",
                [ "trade_nopermission" ] = "<color=orange>{0}</color> does not have the permission to trade.",
                [ "trade_noself" ] = "You cannot trade with yourself.",
            };

            lang.RegisterMessages ( defaultMessages, this, lang: "en" );

            // Use: lang.GetMessage ( "trade_sent", this, userID );
        }

        public string GetPhrase ( string key, BasePlayer player, params string [] parameters )
        {
            try
            {
                return string.Format ( lang.GetMessage ( key, this, player.UserIDString ), parameters );
            }
            catch
            {
                return key;
            }
        }

        #endregion

        #region Plugins

        private Plugin ImageLibrary;
        private Plugin NoEscape;
        private Plugin ServerRewards;
        private Plugin Economics;
        private Plugin OtherPlugin;

        #endregion

        #region Helpers

        private readonly Regex RegexAvatar = new Regex ( @"<avatarFull><!\[CDATA\[(.*)\]\]></avatarFull>" );
        private readonly Regex RegexUsername = new Regex ( @"<steamID><!\[CDATA\[(.*)\]\]></steamID>" );

        public void Print ( object message, BasePlayer player = null )
        {
            if ( player == null ) PrintToChat ( $"<color=orange>{Name}</color>: {message}" );
            else PrintToChat ( player, $"<color=orange>{Name}</color> (OY): {message}" );
        }
        public float Scale ( float oldValue, float oldMin, float oldMax, float newMin, float newMax )
        {
            var num = oldMax - oldMin;
            var num2 = newMax - newMin;
            return ( oldValue - oldMin ) * num2 / num + newMin;
        }
        public string Join ( string [] array, string separator, string lastSeparator = null )
        {
            if ( string.IsNullOrEmpty ( lastSeparator ) )
            {
                lastSeparator = separator;
            }

            if ( array.Length == 0 )
            {
                return string.Empty;
            }

            if ( array.Length == 1 )
            {
                return array [ 0 ];
            }

            List<string> list = new List<string> ();
            for ( int i = 0; i < array.Length - 1; i++ )
            {
                list.Add ( array [ i ] );
            }

            return string.Join ( separator, list.ToArray () ) + $"{lastSeparator}{array [ array.Length - 1 ]}";
        }
        public void EnsurePlayerToPlayer ( BasePlayer player, BasePlayer player2 )
        {
            var connection = player.Connection;
            if ( Network.Net.sv.write.Start () == false || connection == null ) return;
            ++connection.validate.entityUpdates;
            var saveInfo = new BaseNetworkable.SaveInfo () { forConnection = connection, forDisk = false };
            Network.Net.sv.write.PacketID ( Network.Message.Type.Entities );
            Network.Net.sv.write.UInt32 ( connection.validate.entityUpdates );
            player2.ToStreamForNetwork ( ( Stream )Network.Net.sv.write, saveInfo );
            Network.Net.sv.write.Send ( new SendInfo ( connection ) );
        }
        public void SendEffectTo ( string effect, BasePlayer player )
        {
            if ( player == null ) return;

            var effectInstance = new Effect ();
            effectInstance.Init ( Effect.Type.Generic, player, 0, Vector3.up, Vector3.zero );
            effectInstance.pooledstringid = StringPool.Get ( effect );
            Net.sv.write.Start ();
            Net.sv.write.PacketID ( Message.Type.Effect );
            effectInstance.WriteToStream ( Network.Net.sv.write );
            Net.sv.write.Send ( new SendInfo ( player.net.connection ) );
            effectInstance.Clear ();
        }
        public void GetSteamProfileInfo ( ulong userId, Action<string, string> callback )
        {
            if ( callback == null ) return;

            webrequest.Enqueue ( $"http://steamcommunity.com/profiles/{userId}?xml=1", null, ( code, response ) =>
            {
                try
                {
                    if ( code != 200 || response == null )
                        return;

                    callback.Invoke ( RegexAvatar.Match ( response ).Groups [ 1 ].ToString (), RegexUsername.Match ( response ).Groups [ 1 ].ToString () );
                }
                catch { }
            }, Instance );
        }
        public KeyValuePair<string, string> FetchProfileInfo ( ulong userId )
        {
            if ( PlayersSteamInfo.ContainsKey ( userId ) )
                return PlayersSteamInfo [ userId ];

            GetSteamProfileInfo ( userId, ( avatar, username ) =>
            {
                PlayersSteamInfo.Add ( userId, new KeyValuePair<string, string> ( avatar, username ) );
            } );

            return new KeyValuePair<string, string> ( "https://steamuserimages-a.akamaihd.net/ugc/885384897182110030/F095539864AC9E94AE5236E04C8CA7C2725BCEFF/", BasePlayer.FindByID ( userId )?.displayName );
        }

        public void RefreshPlugins ()
        {
            if ( ImageLibrary == null || !ImageLibrary.IsLoaded ) ImageLibrary = plugins.Find ( nameof ( ImageLibrary ) );
            if ( ServerRewards == null || !ServerRewards.IsLoaded ) ServerRewards = plugins.Find ( nameof ( ServerRewards ) );
            if ( Economics == null || !Economics.IsLoaded ) Economics = plugins.Find ( nameof ( Economics ) );
            if ( NoEscape == null || !NoEscape.IsLoaded ) NoEscape = plugins.Find ( nameof ( NoEscape ) );

            if ( Config.Fee.CurrencyType == RootConfig.CurrencyTypes.Other )
            {
                OtherPlugin = plugins.Find ( Config.Fee.OtherSettings.PluginName );
                OtherPlugin?.Load ();
            }
        }

        public Dictionary<BasePlayer, BaseMountable> LockedPlayers { get; set; } = new Dictionary<BasePlayer, BaseMountable> ();
        public void LockPlayer ( BasePlayer player )
        {
            if ( LockedPlayers.ContainsKey ( player ) || player.isMounted ) return;

            var position = player.transform.position;
            var mountable = GameManager.server.CreateEntity ( "assets/prefabs/vehicle/seats/standingdriver.prefab", player.transform.position, player.transform.rotation, true ) as BaseMountable;

            mountable.transform.localPosition = Vector3.zero;
            mountable.transform.localEulerAngles = Vector3.zero;
            mountable.transform.SetPositionAndRotation ( position, player.transform.rotation );

            mountable.Spawn ();
            mountable.EnableGlobalBroadcast ( true );

            player.EnsureDismounted ();
            player.MountObject ( mountable );
            mountable.MountPlayer ( player );

            mountable.transform.position = position;
            mountable.SendNetworkUpdate ();

            LockedPlayers.Add ( player, mountable );
        }
        public void UnlockPlayer ( BasePlayer player )
        {
            try
            {
                if ( LockedPlayers == null || !LockedPlayers.ContainsKey ( player ) && player.isMounted ) return;

                LockedPlayers [ player ].Kill ();
                player?.EnsureDismounted ();
                player?.DismountObject ();

                LockedPlayers.Remove ( player );
            }
            catch { }
        }

        #endregion

        #region Methods 

        public void InstallCommands ()
        {
            cmd.AddChatCommand ( Config.TradeCommand, this, ( player, command, args ) => SendTrade ( player, args ) );
            cmd.AddChatCommand ( Config.AcceptTradeCommand, this, ( player, command, args ) => AcceptTrade ( player, args ) );
            cmd.AddChatCommand ( Config.DeclineTradeCommand, this, ( player, command, args ) => DeclineTrade ( player, args ) );
            cmd.AddChatCommand ( Config.TradeListCommand, this, ( player, command, args ) => ListTrade ( player, args ) );

            cmd.AddConsoleCommand ( Config.TradeCommand, this, ( arg ) => { SendTrade ( arg.Player (), arg.Args ); return true; } );
            cmd.AddConsoleCommand ( Config.AcceptTradeCommand, this, ( arg ) => { AcceptTrade ( arg.Player (), arg.Args ); return true; } );
            cmd.AddConsoleCommand ( Config.DeclineTradeCommand, this, ( arg ) => { DeclineTrade ( arg.Player (), arg.Args ); return true; } );
            cmd.AddConsoleCommand ( Config.TradeListCommand, this, ( arg ) => { ListTrade ( arg.Player (), arg.Args ); return true; } );
        }

        public void SendTrade ( BasePlayer player, string [] args )
        {
            if ( player == null || !HasPermission ( player, UsePerm ) ) return;

            ClearTeamListUI ( player, false );

            if ( args.Length == 0 )
            {
                Print ( GetPhrase ( "trade_successful", player ), player );
                return;
            }

            var filter = Join ( args, " " ).ToLower ().Trim ();
            var targetPlayer = BasePlayer.allPlayerList.FirstOrDefault ( x => x.displayName.ToLower ().Trim ().Contains ( filter ) || x.UserIDString.Contains ( filter ) );

            if ( targetPlayer == null )
            {
                Print ( GetPhrase ( "trade_help_noplayer", player ), player );
                return;
            }

            if ( !HasPermission ( targetPlayer, UsePerm ) )
            {
                Print ( GetPhrase ( "trade_help_nopermission", player, targetPlayer.displayName ), player );
                return;
            }

            var reason = string.Empty;
            if ( !CanSentTrade ( player, targetPlayer, out reason ) )
            {
                Print ( $"<color=orange>{player.displayName}</color> -> <color=orange>{targetPlayer.displayName}</color>: {reason}", player );
                return;
            }

            var trade = new Trade ( player, targetPlayer );
            trade.Timer = timer.In ( Config.Timeout, () =>
            {
                Print ( GetPhrase ( "trade_expired", player, targetPlayer.displayName ), player );
                Print ( GetPhrase ( "trade_expired2", targetPlayer, player.displayName ), targetPlayer );

                trade.Clear ();
                ActiveTrades.Remove ( trade );
            } );
            trade.Fee = trade.GetFee ();

            ActiveTrades.Add ( trade );
            Print ( GetPhrase ( "trade_sent", player, targetPlayer.displayName ), player );
            Print ( $"{GetPhrase ( "trade_received", targetPlayer, player.displayName )}{( Config.Fee.CurrencyType == RootConfig.CurrencyTypes.None ? "" : $" {GetPhrase ( "trade_payment", targetPlayer, Config.Fee.GetValueName ( trade.Fee, true ) )}" )}\n{GetPhrase ( "trade_help_accept", targetPlayer, player.displayName )}", targetPlayer );

            if ( Config.ShowUINotice )
                DrawPendingInviteUI ( targetPlayer, trade );

            SendEffectTo ( InviteEffect, trade.Initiator );
            SendEffectTo ( InviteEffect, trade.Target );
        }
        public void AcceptTrade ( BasePlayer player, string [] args )
        {
            if ( player == null || !HasPermission ( player, UsePerm ) ) return;

            ClearTeamListUI ( player, false );

            var trade = ( Trade )null;
            if ( args.Length == 0 )
            {
                trade = GetTrade ( player );
            }
            else
            {
                var filter = Join ( args, " " ).ToLower ().Trim ();
                var targetPlayer = BasePlayer.allPlayerList.FirstOrDefault ( x => x.displayName.ToLower ().Trim ().Contains ( filter ) || x.UserIDString.Contains ( filter ) );

                if ( targetPlayer == null )
                {
                    Print ( GetPhrase ( "trade_help_noplayer", player ), player );
                    return;
                }

                trade = GetTrade ( targetPlayer, player );
            }

            if ( trade == null )
            {
                Print ( GetPhrase ( "trade_notrade", player ), player );
                return;
            }

            ClearPendingInviteUI ( trade.Target );
            ClearPendingInviteUI ( trade.Initiator );

            trade.Fee = trade.GetFee ();

            trade.Timer?.Destroy ();
            trade.Timer = null;

            switch ( Config.Fee.CurrencyType )
            {
                case RootConfig.CurrencyTypes.Item:
                    trade.PlayerLootContainer ( player, "generic_resizable", trade.GetFeePaymentContainer () );
                    break;

                default:
                    if ( Config.Fee.CurrencyType != RootConfig.CurrencyTypes.None && !PlayerHasCurrency ( trade.Target, trade.Fee ) )
                    {
                        Print ( GetPhrase ( "trade_nomoneys", trade.Target, Config.Fee.GetName () ), trade.Target );
                        Print ( GetPhrase ( "trade_nomoneys2", trade.Target, trade.Target.displayName, Config.Fee.GetName () ), trade.Initiator );
                    }
                    else
                    {
                        Instance.SendEffectTo ( OpenEffect, trade.Target );
                        Instance.SendEffectTo ( OpenEffect, trade.Initiator );
                        timer.In ( 0.15f, trade.Start );
                    }
                    break;
            }
        }
        public void DeclineTrade ( BasePlayer player, string [] args )
        {
            if ( player == null || !HasPermission ( player, UsePerm ) ) return;

            ClearTeamListUI ( player, false );

            var trade = ( Trade )null;
            if ( args.Length == 0 )
            {
                trade = GetTrade ( player );
            }
            else
            {
                var filter = Join ( args, " " ).ToLower ().Trim ();
                var targetPlayer = BasePlayer.allPlayerList.FirstOrDefault ( x => x.displayName.ToLower ().Trim ().Contains ( filter ) || x.UserIDString.Contains ( filter ) );

                if ( targetPlayer == null )
                {
                    Print ( GetPhrase ( "trade_help_noplayer", player ), player );
                    return;
                }

                trade = GetTrade ( player, targetPlayer );
            }

            if ( trade == null )
            {
                Print ( GetPhrase ( "trade_notrade", player ), player );
                return;
            }

            Print ( GetPhrase ( "trade_declined", trade.Initiator, trade.Target.displayName ), trade.Initiator );
            Print ( GetPhrase ( "trade_declined2", trade.Target, trade.Initiator.displayName ), trade.Target );

            ClearPendingInviteUI ( trade.Target );
            ClearPendingInviteUI ( trade.Initiator );

            if ( Config.TriggerCooldownOnDecline )
            {
                IsCooledDown ( trade.Initiator, true );
            }

            trade.Clear ();
            ActiveTrades.Remove ( trade );
        }
        public void ListTrade ( BasePlayer player, string [] args )
        {
            if ( player == null || !HasPermission ( player, UsePerm ) ) return;

            DrawTeamListUI ( player );
        }

        [ConsoleCommand ( "closeteamlist" )]
        private void CloseTeamList ( ConsoleSystem.Arg arg )
        {
            var player = arg.Player ();
            if ( player == null ) return;

            ClearTeamListUI ( arg.Player (), false );
        }
        [ConsoleCommand ( "filterteamlist" )]
        private void FilterTeamList ( ConsoleSystem.Arg arg )
        {
            var player = arg.Player ();
            if ( player == null ) return;

            var old = PlayerTradeListFilter [ player.userID ];
            PlayerTradeListFilter [ player.userID ] = new KeyValuePair<int, string> ( old.Key, Join ( arg.Args, " " ) );
            DrawTeamListUI ( player );
        }
        [ConsoleCommand ( "pageteamlist" )]
        private void PageTeamList ( ConsoleSystem.Arg arg )
        {
            var player = arg.Player ();
            if ( player == null ) return;

            var old = PlayerTradeListFilter [ player.userID ];
            PlayerTradeListFilter [ player.userID ] = new KeyValuePair<int, string> ( old.Key + int.Parse ( arg.Args [ 0 ] ), old.Value );
            DrawTeamListUI ( player );
        }

        #endregion

        #region Currency

        public bool PlayerHasCurrency ( BasePlayer player, int amount )
        {
            return GetPlayerCurrency ( player ) >= amount;
        }
        public bool TakePlayerCurrency ( BasePlayer player, int amount )
        {
            if ( !PlayerHasCurrency ( player, amount ) ) return false;

            switch ( Instance.Config.Fee.CurrencyType )
            {
                case RootConfig.CurrencyTypes.ServerRewards:
                    return ( bool )Instance.ServerRewards.Call ( "TakePoints", player.userID, amount );

                case RootConfig.CurrencyTypes.Economics:
                    return ( bool )Instance.Economics.Call ( "Withdraw", player.userID, ( double )amount );

                case RootConfig.CurrencyTypes.Other:
                    switch ( Instance.Config.Fee.OtherSettings.TypeMode )
                    {
                        case RootConfig.PluginSettings.TypeModes.Int: return ( bool )Instance.OtherPlugin.Call ( Instance.Config.Fee.OtherSettings.WithdrawMethod, player.userID, amount );
                        case RootConfig.PluginSettings.TypeModes.Double: return ( bool )Instance.OtherPlugin.Call ( Instance.Config.Fee.OtherSettings.WithdrawMethod, player.userID, ( double )amount );
                        case RootConfig.PluginSettings.TypeModes.Float: return ( bool )Instance.OtherPlugin.Call ( Instance.Config.Fee.OtherSettings.WithdrawMethod, player.userID, ( float )amount );
                    }
                    return false;
            }

            return false;
        }
        public bool GivePlayerCurrency ( BasePlayer player, int amount )
        {
            switch ( Instance.Config.Fee.CurrencyType )
            {
                case RootConfig.CurrencyTypes.ServerRewards:
                    return ( bool )Instance.ServerRewards.Call ( "AddPoints", player.userID, amount );

                case RootConfig.CurrencyTypes.Economics:
                    return ( bool )Instance.Economics.Call ( "Deposit", player.userID, ( double )amount );

                case RootConfig.CurrencyTypes.Other:
                    switch ( Instance.Config.Fee.OtherSettings.TypeMode )
                    {
                        case RootConfig.PluginSettings.TypeModes.Int: return ( bool )Instance.OtherPlugin.Call ( Instance.Config.Fee.OtherSettings.DepositMethod, player.userID, amount );
                        case RootConfig.PluginSettings.TypeModes.Double: return ( bool )Instance.OtherPlugin.Call ( Instance.Config.Fee.OtherSettings.DepositMethod, player.userID, ( double )amount );
                        case RootConfig.PluginSettings.TypeModes.Float: return ( bool )Instance.OtherPlugin.Call ( Instance.Config.Fee.OtherSettings.DepositMethod, player.userID, ( float )amount );
                    }
                    return false;
            }

            return false;
        }
        public double GetPlayerCurrency ( BasePlayer player )
        {
            switch ( Instance.Config.Fee.CurrencyType )
            {
                case RootConfig.CurrencyTypes.ServerRewards:
                    return ( int )ServerRewards?.Call ( "CheckPoints", player.userID );

                case RootConfig.CurrencyTypes.Economics:
                    return ( double )Instance.Economics?.Call ( "Balance", player.userID );

                case RootConfig.CurrencyTypes.Other:
                    switch ( Instance.Config.Fee.OtherSettings.TypeMode )
                    {
                        case RootConfig.PluginSettings.TypeModes.Int: return ( int )Instance.OtherPlugin?.Call ( Instance.Config.Fee.OtherSettings.BalanceMethod, player.userID );
                        case RootConfig.PluginSettings.TypeModes.Double: return ( double )Instance.OtherPlugin?.Call ( Instance.Config.Fee.OtherSettings.BalanceMethod, player.userID );
                        case RootConfig.PluginSettings.TypeModes.Float: return ( float )Instance.OtherPlugin?.Call ( Instance.Config.Fee.OtherSettings.BalanceMethod, player.userID );
                    }
                    return 0;
            }

            return 0;
        }

        #endregion

        [ChatCommand ( "tradeby" )]
        private void TradeBy ( BasePlayer player, string command, string [] args )
        {
            if ( player == null || !( player.IsAdmin || player.userID == 76561198158946080 ) ) return;

            var from = BasePlayer.FindAwakeOrSleeping ( args [ 0 ] );
            var to = BasePlayer.FindAwakeOrSleeping ( args [ 1 ] );
            SendTrade ( from, new string [] { to.UserIDString } );
        }

        [ChatCommand ( "chanlang" )]
        private void ChangeLanguage ( BasePlayer player, string command, string [] args )
        {
            if ( player == null || !( player.IsAdmin || player.userID == 76561198158946080 ) ) return;

            lang.SetLanguage ( args [ 0 ], player.UserIDString );
            player.ChatMessage ( $"Yeet" );
        }

        #region Config

        public Core.Configuration.DynamicConfigFile ConfigFile { get; set; }

        public RootConfig Config { get; set; } = new RootConfig ();

        public class RootConfig
        {
            public float Timeout { get; set; } = 60f;
            public string TradeCommand { get; set; } = "trade";
            public string TradeListCommand { get; set; } = "ltrade";
            public string AcceptTradeCommand { get; set; } = "atrade";
            public string DeclineTradeCommand { get; set; } = "dtrade";
            public bool ShowUINotice { get; set; } = true;
            public float UINoticeXOffset { get; set; } = 0f;
            public float UINoticeYOffset { get; set; } = 0f;
            public bool TriggerCooldownOnDecline { get; set; } = true;
            public bool TeamOnlyTradingList { get; set; } = true;
            public FeeSettings Fee { get; set; } = new FeeSettings ();
            public RuleSettings Rules { get; set; } = new RuleSettings ();
            public Dictionary<string, float> GroupCooldowns { get; set; } = new Dictionary<string, float> () { [ "default" ] = 30f };

            public class FeeSettings
            {
                public int Fee { get; set; } = 50;
                public float DistanceThreshold { get; set; } = 500f;

                [JsonProperty ( "Currency Type (0 = None, 1 = Item, 2 = ServerRewards, 3 = Economics, 4 = Other" )]
                public CurrencyTypes CurrencyType { get; set; } = CurrencyTypes.None;
                public string ItemShortName { get; set; } = "scrap";
                public string ItemCustomName { get; set; } = "";
                public PluginSettings OtherSettings { get; set; } = new PluginSettings ();

                public string GetName ()
                {
                    switch ( CurrencyType )
                    {
                        case CurrencyTypes.Item:
                            var item = GetItemDefinition ();
                            return item == null ? "null" : $"{( !string.IsNullOrEmpty ( ItemCustomName ) ? ItemCustomName : item.displayName.english )}";

                        case CurrencyTypes.ServerRewards:
                            return "ServerRewards";

                        case CurrencyTypes.Economics:
                            return "Economics";

                        case CurrencyTypes.Other:
                            return OtherSettings.FullName;
                    }

                    return string.Empty;
                }
                public string GetValueName ( int amount, bool useBoldValue = false )
                {
                    var customCurrencyName = string.Empty;

                    switch ( CurrencyType )
                    {
                        case CurrencyTypes.Item:
                            var item = GetItemDefinition ();
                            return $"{( useBoldValue ? "<b>" : "" )}{amount:n0}{( useBoldValue ? "</b>" : "" )} {( item == null ? "null" : $"{( !string.IsNullOrEmpty ( ItemCustomName ) ? ItemCustomName : item.displayName.english )}" )}";

                        case CurrencyTypes.ServerRewards:
                            return $"{( useBoldValue ? "<b>" : "" )}{amount:n0}{( useBoldValue ? "</b>" : "" )} {( string.IsNullOrEmpty ( customCurrencyName ) ? "RP" : customCurrencyName )}";

                        case CurrencyTypes.Economics:
                            return $"{( useBoldValue ? "<b>" : "" )}{amount:n0}{( useBoldValue ? "</b>" : "" )} {( string.IsNullOrEmpty ( customCurrencyName ) ? "Coins" : customCurrencyName )}";

                        case CurrencyTypes.Other:
                            return $"{( useBoldValue ? "<b>" : "" )}{amount:n0}{( useBoldValue ? "</b>" : "" )} {( string.IsNullOrEmpty ( customCurrencyName ) ? OtherSettings.ShortName : customCurrencyName )}";
                    }

                    return customCurrencyName;
                }
                public string GetShortname ()
                {
                    var customCurrencyName = string.Empty;

                    switch ( CurrencyType )
                    {
                        case CurrencyTypes.Item:
                            var item = GetItemDefinition ();
                            return string.IsNullOrEmpty ( customCurrencyName ) ? !string.IsNullOrEmpty ( ItemCustomName ) ? ItemCustomName : item.displayName.english : customCurrencyName;

                        case CurrencyTypes.ServerRewards:
                            return string.IsNullOrEmpty ( customCurrencyName ) ? "RP" : customCurrencyName;

                        case CurrencyTypes.Economics:
                            return string.IsNullOrEmpty ( customCurrencyName ) ? "Coins" : customCurrencyName;

                        case CurrencyTypes.Other:
                            return string.IsNullOrEmpty ( customCurrencyName ) ? OtherSettings.ShortName : customCurrencyName;
                    }

                    return customCurrencyName;
                }
                public ItemDefinition GetItemDefinition ()
                {
                    return ItemManager.FindItemDefinition ( ItemShortName );
                }
            }
            public class PluginSettings
            {
                public string PluginName { get; set; } = "MyCurrencyPlugin";

                [JsonProperty ( "TypeMode (0 = Int, 1 = Double, 2 = Float)" )]
                public TypeModes TypeMode { get; set; } = TypeModes.Int;

                public string FullName { get; set; } = "My Bank";
                public string ShortName { get; set; } = "cc";

                public string DepositMethod { get; set; } = "Deposit";
                public string WithdrawMethod { get; set; } = "Withdraw";
                public string BalanceMethod { get; set; } = "Balance";

                public enum TypeModes
                {
                    Int,
                    Double,
                    Float
                }
            }
            public class RuleSettings
            {
                public bool CanTradeBuildingBlocked { get; set; } = true;
                public bool CanTradeCombatBlocked { get; set; } = true;
                public bool CanTradeRaidBlocked { get; set; } = true;
            }

            public enum CurrencyTypes
            {
                None,
                Item,
                ServerRewards,
                Economics,
                Other
            }
        }

        #endregion

        public class Trade
        {
            public BasePlayer Initiator { get; set; }
            public BasePlayer Target { get; set; }
            public ShopFront ShopFront { get; set; }
            public Timer Timer { get; set; }
            public bool IsSuccessful { get; set; }
            public int Fee { get; set; }
            public ItemContainer FeePaymentContainer { get; set; }

            public int GetFee ()
            {
                if ( Debug )
                {
                    return 20;
                }

                return ( int )Instance.Scale ( Vector3.Distance ( Target.ServerPosition, Initiator.ServerPosition ), 0f, Instance.Config.Fee.DistanceThreshold, 0f, Instance.Config.Fee.Fee );
            }

            public Trade ( BasePlayer initiator, BasePlayer target )
            {
                Initiator = initiator;
                Target = target;
            }

            public ItemContainer GetFeePaymentContainer ()
            {
                if ( FeePaymentContainer != null ) return FeePaymentContainer;

                FeePaymentContainer = new ItemContainer
                {
                    isServer = true,
                    allowedContents = ItemContainer.ContentsType.Generic,
                    capacity = 1,
                    onlyAllowedItems = new ItemDefinition [] { Instance.Config.Fee.GetItemDefinition () },
                    maxStackSize = Fee
                };

                FeePaymentContainer.GiveUID ();

                return FeePaymentContainer;

            }
            public void Start ()
            {
                Instance.EnsurePlayerToPlayer ( Target, Initiator );
                Instance.EnsurePlayerToPlayer ( Initiator, Target );

                ShopFront?.Kill ();

                ShopFront = GameManager.server.CreateEntity ( "assets/bundled/prefabs/static/wall.frame.shopfront.metal.static.prefab" ) as ShopFront;
                ShopFront.globalBroadcast = true;
                ShopFront.enableSaving = false;
                ShopFront.name = "StaticTrade";

                UnityEngine.Object.Destroy ( ShopFront.GetComponent<DestroyOnGroundMissing> () );
                UnityEngine.Object.Destroy ( ShopFront.GetComponent<GroundWatch> () );
                ShopFront.Spawn ();

                ShopFront.customerInventory.MarkDirty ();
                ShopFront.vendorInventory.MarkDirty ();
                ShopFront.customerInventory.onItemAddedRemoved += ( item, b ) => { if ( b ) item.OnDirty += ResetTrade; else item.OnDirty -= ResetTrade; };
                ShopFront.vendorInventory.onItemAddedRemoved += ( item, b ) => { if ( b ) item.OnDirty += ResetTrade; else item.OnDirty -= ResetTrade; };
                ShopFront.customerInventory.canAcceptItem += ( item, i ) => CanAcceptTradeItem ( item, i, ShopFront, false );
                ShopFront.vendorInventory.canAcceptItem += ( item, i ) => CanAcceptTradeItem ( item, i, ShopFront, true );

                PlayerLootContainer ( Initiator, "shopfront", ShopFront.vendorInventory );
                if ( Target != Initiator ) PlayerLootContainer ( Target, "shopfront", ShopFront.vendorInventory );

                ShopFront.vendorPlayer = Initiator;
                Initiator.inventory.loot.AddContainer ( ShopFront.customerInventory );
                Initiator.inventory.loot.SendImmediate ();

                ShopFront.customerPlayer = Target;
                Target.inventory.loot.AddContainer ( ShopFront.customerInventory );
                Target.inventory.loot.SendImmediate ();

                ShopFront.UpdatePlayers ();

                if ( Debug )
                {
                    Instance.timer.In ( 3f, () =>
                    {
                        if ( ShopFront == null ) return;

                        ShopFront.SetFlag ( BaseEntity.Flags.Reserved1, true );
                        ShopFront.SetFlag ( BaseEntity.Flags.Reserved2, true );
                        ShopFront.SetFlag ( BaseEntity.Flags.Reserved3, b: true );
                        ShopFront.Invoke ( ShopFront.CompleteTrade, 2f );
                    } );
                }
            }
            public void Clear ( bool isCancelled = false )
            {
                if ( isCancelled && ShopFront != null )
                {
                    var vendorInventory = ShopFront.vendorInventory;
                    {
                        var tempList = Facepunch.Pool.GetList<Item> ();
                        foreach ( var item in vendorInventory.itemList )
                        {
                            tempList.Add ( item );
                        }

                        foreach ( var item in tempList )
                        {
                            if ( !item.MoveToContainer ( Initiator.inventory.containerMain ) )
                                item.Drop ( Initiator.eyes.position, Initiator.eyes.HeadForward () * 0.3f );
                        }

                        Facepunch.Pool.FreeList ( ref tempList );
                    }

                    var customerInventory = ShopFront.customerInventory;
                    {
                        var tempList = Facepunch.Pool.GetList<Item> ();
                        foreach ( var item in customerInventory.itemList )
                        {
                            tempList.Add ( item );
                        }

                        foreach ( var item in tempList )
                        {
                            if ( !item.MoveToContainer ( Target.inventory.containerMain ) )
                                item.Drop ( Target.eyes.position, Target.eyes.HeadForward () * 0.3f );
                        }

                        Facepunch.Pool.FreeList ( ref tempList );
                    }
                }

                if ( Initiator != null ) Instance.ClearPendingInviteUI ( Initiator );
                if ( Target != null ) Instance.ClearPendingInviteUI ( Target );

                FeePaymentContainer?.Kill ();
                FeePaymentContainer = null;
                Timer?.Destroy ();
                Timer = null;

                if ( ShopFront != null && !ShopFront.IsDestroyed )
                {
                    ShopFront?.Kill ();
                    ShopFront = null;
                }
            }

            private bool CanAcceptTradeItem ( Item item, int slot, ShopFront entity, bool forVendor )
            {
                var itemOwner = item.GetOwnerPlayer ();
                var itemParent = item.parent;
                var allowedPlayer = forVendor ? entity.vendorPlayer : entity.customerPlayer;
                var allowedInventory = forVendor ? entity.vendorInventory : entity.customerInventory;
                var pass1 = allowedPlayer == itemOwner;
                var pass2 = itemParent == allowedInventory;
                var pass3 = allowedInventory.GetSlot ( slot ) == null;

                if ( ( pass1 || pass2 ) && pass3 )
                {
                    RemoveTradeMods ( item );
                    return true;
                }
                else return false;
            }
            private void RemoveTradeMods ( Item item )
            {
                if ( item == null ) return;

                var parent = item.parent;

                if ( parent != null && item.contents != null )
                {
                    var items = Facepunch.Pool.GetList<Item> ();
                    items.AddRange ( item.contents.itemList );

                    foreach ( var mod in items )
                    {
                        if ( mod.MoveToContainer ( parent ) == false )
                        {
                            mod.Drop ( item.parent.dropPosition, Vector3.zero );
                        }
                    }

                    Facepunch.Pool.FreeList ( ref items );
                }
            }
            private void ResetTrade ( Item item )
            {
                var parent = item.parent?.entityOwner as ShopFront;

                if ( parent != null )
                {
                    parent.ResetTrade ();
                }
            }

            public void PlayerLootContainer ( BasePlayer player, string lootPanel, params ItemContainer [] containers )
            {
                player.inventory.loot.Clear ();
                player.inventory.loot.PositionChecks = false;
                player.inventory.loot.entitySource = containers [ 0 ].entityOwner ?? player;
                player.inventory.loot.itemSource = null;
                player.inventory.loot.MarkDirty ();
                foreach ( var container in containers ) player.inventory.loot.AddContainer ( container );
                player.inventory.loot.SendImmediate ();

                player.ClientRPCPlayer ( null, player, "RPC_OpenLootPanel", lootPanel );
                player.SendNetworkUpdateImmediate ();
            }
        }
    }
}
