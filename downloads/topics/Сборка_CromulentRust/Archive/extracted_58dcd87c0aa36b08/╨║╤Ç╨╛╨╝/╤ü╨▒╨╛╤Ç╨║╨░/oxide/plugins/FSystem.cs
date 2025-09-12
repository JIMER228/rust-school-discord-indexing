using Oxide.Core;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("FSystem", "FourTeen", "1.0.4")]
    internal class FSystem : RustPlugin
    {
        [PluginReference] private Plugin RustApp;
        private const string PERM = "rustpanel.ignorereport";
        //private Dictionary<string, string> CHECK = new Dictionary<string, string>();

        private void OnServerInitialized()
        {
            //LoadData();
            permission.RegisterPermission(PERM, this);
            Subscribe(nameof(RustApp_CanIgnoreBanByCreatorID));
            Subscribe(nameof(RustApp_CanIgnoreBan));
            Subscribe(nameof(RustApp_CanIgnoreReport));
            Subscribe(nameof(RustApp_CanIgnoreCheck));
            //Subscribe(nameof(RustApp_CanOpenReportUI));
            //Subscribe(nameof(RustApp_OnCheckNoticeShowed));
        }

        object RustApp_CanIgnoreBanByCreatorID(string steamid, string id)
        {
            if (id == "10506" || id == "10083")
            {
                return false;
            }
            return null;
        }

        object RustApp_CanIgnoreCheck(BasePlayer player)
        {
            if (player != null && permission.UserHasPermission(player.UserIDString, PERM))
                return false;
            return null;
        }

        object RustApp_CanIgnoreReport(string target_steam_id, string initiator_steam_id)
        {
            if (string.IsNullOrEmpty(target_steam_id) && string.IsNullOrEmpty(initiator_steam_id))
                return null;
            var initiator = BasePlayer.Find(initiator_steam_id);
            var target = BasePlayer.Find(target_steam_id);
            //if (initiator != null && !permission.UserHasPermission(initiator_steam_id, PERM))
            //{
            //    RustApp.Call("SendMessage", initiator_steam_id, "Система репортов доступна игрокам, которые <color=#64FF33>приобрели</color> товар в магазине <color=#6b53f5>rust-crom.ru</color> на сумму от <color=#64FF33>500</color> рублей.");
            //    return false;
            //}
            if (permission.UserHasPermission(target_steam_id, PERM))
            {
                if (initiator != null && target != null)
                {
                    RustApp.CallHook("SoundToast", initiator, $"Игрок {target.displayName} является донатером на него нельзя кинуть репорт!", 2);
                }
                return false;
            }
            return null;
        }

        object RustApp_CanIgnoreBan(string steam_id)
        {
            if (string.IsNullOrEmpty(steam_id))
                return null;
            if (permission.UserHasPermission(steam_id, PERM))
            {
                RustApp.Call("SendMessage", steam_id, "Вас хотели выгнать или заблокировать, но <color=#FF9740>fourteen</color> о вас позаботится <3");
                return false;
            }
            //if (!CHECK.ContainsKey(steam_id))
            //{
            //    var player = BasePlayer.Find(steam_id);
            //    if (player != null && !CHECK.ContainsValue(player.Connection.IPAddressWithoutPort()))
            //        return false;
            //    else
            //    {
            //        var connection = ConnectionAuth.m_AuthConnection.Find(v => v.userid.ToString() == steam_id);
            //        if (connection != null && !CHECK.ContainsValue(connection.IPAddressWithoutPort()))
            //            return false;
            //    }
            //}
            return null;
        }

        //object RustApp_CanOpenReportUI(BasePlayer player)
        //{
        //    if (player == null)
        //        return null;
        //    if (!permission.UserHasPermission(player.UserIDString, PERM))
        //    {
        //        RustApp.Call("SendMessage", player, "Система репортов доступна игрокам, которые <color=#64FF33>приобрели</color> товар в магазине <color=#6b53f5>rust-crom.ru</color> на сумму от <color=#64FF33>500</color> рублей.");
        //        return false;
        //    }
        //    return null;
        //}

        //void RustApp_OnCheckNoticeShowed(BasePlayer player)
        //{
        //    if (player != null && !CHECK.ContainsKey(player.UserIDString))
        //        CHECK.Add(player.UserIDString, player.Connection.IPAddressWithoutPort());
        //    SaveData();
        //}

        //void OnPlayerConnected(BasePlayer player)
        //{
        //    if (player != null && CHECK.ContainsKey(player.UserIDString))
        //        CHECK.Remove(player.UserIDString);
        //    SaveData();
        //}

        //void SaveData()
        //{
        //    Interface.Oxide.DataFileSystem.WriteObject(Title + "/CHECK", CHECK);
        //}

        //void LoadData()
        //{
        //    try
        //    {
        //        CHECK = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, string>>(Title + "/CHECK");
        //    }
        //    catch (Exception e)
        //    {
        //        PrintError(e.ToString());
        //    }
        //}
    }
}
