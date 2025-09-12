using System;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Libraries;
using Oxide.Game.Rust.Cui;
using Rust;
using Network;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using ProtoBuf;

namespace Oxide.Plugins{

    [Info("RecoilViewer", "ninco90", "1.5.3")]
    [Description("Displays the players recoil on the screen.")]
    public class RecoilViewer : RustPlugin {

        #region Fields
        [PluginReference] Plugin ImageLibrary;
        private static RecoilViewer plugin;
        private Dictionary <string, List <Vector2>> patternData;
        private BasePlayer ultArkan;
        List<ulong> AutoArkan = new List<ulong>();
        List<ulong> NoSpectate = new List<ulong>();
        private Dictionary<BasePlayer, ShooterData> CacheInfo = new Dictionary<BasePlayer, ShooterData>();
        private static Dictionary<string, Vector2> Sensibility = new Dictionary<string, Vector2>(){
            {"rifle.ak", new Vector2(11, 14)},
            {"rifle.ak.ice", new Vector2(11, 14)},
            {"rifle.lr300", new Vector2(27, 30)},
            {"smg.mp5", new Vector2(27, 30)},
            {"smg.thompson", new Vector2(27, 30)},
            {"smg.2", new Vector2(27, 30)},
            {"lmg.m249", new Vector2(27, 30)}
        };

        private const string UI_player_shot = "player_shot.GUI";
        private const string UI_player_hits = "player_hits.GUI";
        private const string UI_weapon_pattern = "weapon_pattern.GUI";
        private const string UI_panel_show = "panel_show.GUI";
        #endregion

        #region Oxide Hooks
        private void OnServerInitialized(){
            if (!ImageLibrary){
                PrintWarning("Image Library not detected, unloading RecoilViewer");
                Interface.Oxide.UnloadPlugin(Name);
                return;
            }

            permission.RegisterPermission("RecoilViewer.player", this);
            permission.RegisterPermission("RecoilViewer.admin", this);
            permission.RegisterPermission("RecoilViewer.spectate", this);
            permission.RegisterPermission("RecoilViewer.training", this);
            permission.RegisterPermission("RecoilViewer.zonemanager", this);
            permission.RegisterPermission("RecoilViewer.autoarkan", this);
            ReadData();

            AddImageLibrary(config.info.shots.iconURL, config.info.shots.iconURL);
            AddImageLibrary(config.info.hit.iconURL, config.info.hit.iconURL);
            AddImageLibrary(config.info.headshot.iconURL, config.info.headshot.iconURL);
        }
        
        private void Unload(){
            foreach (var player in BasePlayer.activePlayerList){
                CloseAllGUI(player);
            }
        }

        object CanSpectateTarget(BasePlayer player, string filter){
            if (permission.UserHasPermission(player.UserIDString, "RecoilViewer.spectate") && config.spectate && !NoSpectate.Contains(player.userID)){
                DeleteActiveShooter(player);
                timer.Once(0.5f, () => {
                    if (player.IsSpectating()){
                        var parentEntity = player.GetParentEntity();
                        if (parentEntity as BasePlayer != null){
                            var PlayerActive = parentEntity as BasePlayer;
                            SpectateRecoil(player, PlayerActive);
                        }
                    }
                });
            }
            return null;
        }

        object OnReloadWeapon(BasePlayer player, BaseProjectile projectile){
            if (player == null){ return null; }
            if (!CacheInfo.ContainsKey(player) || !config.reloadClear){ return null; }
            if (CacheInfo.ContainsKey(player)){
                ClearCacheInfo(player);
                var item = player.GetActiveItem();
                foreach (BasePlayer Viewer in  CacheInfo[player].Viewers) {
                    RecoilShotsGUI(Viewer, player);
                    ShowPatternGUI(Viewer, item, -1);
                    InfoHitsGUI(Viewer, player);
                }
                return null;
            }
            return null;
        }

        private void OnPlayerAttack(BasePlayer attacker, HitInfo hitinfo){
            if (!config.showinfo) return;
            if (hitinfo == null) return;
            if (!CacheInfo.ContainsKey(attacker)){ return; }
            SendHit(attacker, hitinfo);
        }

        private void OnWeaponFired(BaseProjectile projectile, BasePlayer player){
            if (player == null){ return; }
            if (!CacheInfo.ContainsKey(player)){ return; }

            var item = player.GetActiveItem();
            var Weapon = item.info.shortname;
            if(!patternData.ContainsKey(Weapon)){ return; }

            if (projectile.primaryMagazine.contents == 0){
                timer.Once(1.1f, () =>{
                    if (CacheInfo.ContainsKey(player)){ ClearCacheInfo(player); }
                });
            }

            var InfoPlayer = CacheInfo[player];
            float difference = Time.time - InfoPlayer.Time;
            if (difference > 0.5 && difference < 300.5){
                ClearCacheInfo(player);
                InfoPlayer.Time = 0;
            }

            float rotationLastx = player?.eyes?.rotation.eulerAngles.x ?? 0;
            float rotationLasty = player?.eyes?.rotation.eulerAngles.y ?? 0;
            InfoPlayer.NewShot = new Vector2(rotationLastx, rotationLasty);

            if (InfoPlayer.OldShot.x == 0 && InfoPlayer.OldShot.y == 0){
                float x = -patternData[Weapon][0].x;
                float y = -patternData[Weapon][0].y;

                InfoPlayer.ShootDisplay = new Vector2(x, y);
                InfoPlayer.AllShootsDisplay.Add(InfoPlayer.ShootDisplay);

                InfoPlayer.OldShot = InfoPlayer.NewShot;
                InfoPlayer.Time = Time.time;
                InfoPlayer.ShotCounter++;

                foreach (BasePlayer Viewer in  InfoPlayer.Viewers) {
                    RecoilShotsGUI(Viewer, player);
                    ShowPatternGUI(Viewer, item, 0);
                    InfoHitsGUI(Viewer, player);
                }
                return;
            }
            
            float currentX = InfoPlayer.NewShot.x - InfoPlayer.OldShot.x;
            float currentY = InfoPlayer.NewShot.y - InfoPlayer.OldShot.y;
            InfoPlayer.Displacement = new Vector2(currentX, currentY);

            if (currentX < -180){ InfoPlayer.Displacement.x = currentX + 360; }
            if (currentX > 180){ InfoPlayer.Displacement.x = currentX - 360; }
            if (currentY < -180){ InfoPlayer.Displacement.y = currentY + 360; }
            if (currentY > 180){ InfoPlayer.Displacement.y = currentY - 360; }

            if(patternData.ContainsKey(Weapon)){
                float displayX = InfoPlayer.Displacement.x * Sensibility[Weapon].x - patternData[Weapon][InfoPlayer.ShotCounter].x;
                float displayY = InfoPlayer.Displacement.y * Sensibility[Weapon].y - patternData[Weapon][InfoPlayer.ShotCounter].y;
                InfoPlayer.ShootDisplay = new Vector2(displayX, displayY);
                
                InfoPlayer.AllShootsDisplay.Add(InfoPlayer.ShootDisplay);
                InfoPlayer.ShotCounter++;
                //Puts("x: " + -displayX + " y: " + displayY);
            }

            foreach (BasePlayer Viewer in InfoPlayer.Viewers){
                RecoilShotsGUI(Viewer, player);
                ShowPatternGUI(Viewer, item, InfoPlayer.ShotCounter-1);
                InfoHitsGUI(Viewer, player);
            }

            if (Weapon.Contains("smg.thompson") && InfoPlayer.ShotCounter == 20){
                ClearCacheInfo(player);
            } else 
            if (Weapon.Contains("smg.2") && InfoPlayer.ShotCounter == 24){
                ClearCacheInfo(player);
            } else
            if (Weapon.Contains("lmg.m249") && InfoPlayer.ShotCounter == 30){
                ClearCacheInfo(player);
            } else
            if (InfoPlayer.ShotCounter == 30){
                ClearCacheInfo(player);
            }
            InfoPlayer.Time = Time.time;
        }

        private void OnActiveItemChanged(BasePlayer player, Item oldItem, Item newItem){
            if (player == null){ return; }
            if (!CacheInfo.ContainsKey(player)){ return; }
            if (newItem == null){ return; }

            ClearCacheInfo(player);

            var mameActiveItem = (newItem != null ? newItem.info.displayName.english.ToUpper() : Lang("Empty"));
            //Puts("ITEM: " + player.displayName + " Name Item: " + mameActiveItem);

            foreach (BasePlayer Viewer in  CacheInfo[player].Viewers) {
                WindowsViewerGUI(Viewer, newItem, player.displayName);
                InfoHitsGUI(Viewer, player);
                RecoilShotsGUI(Viewer, player);
            }
        }

        //AimTrain Hooks
        void JoinedAimTrain(BasePlayer player){
            if (permission.UserHasPermission(player.UserIDString, "RecoilViewer.training") && config.training){
                SpectateRecoil(player, player);
            }
        }

        void LeftAimTrain(BasePlayer player){
            if (permission.UserHasPermission(player.UserIDString, "RecoilViewer.training") && config.training){
                CloseRecoil(player);
            }
        }

        //ZoneManager Hooks
        void OnEnterZone(string ZoneID, BasePlayer player){
            if (permission.UserHasPermission(player.UserIDString, "RecoilViewer.zonemanager") && config.zonemanager && config.zoneslist.Contains(ZoneID)){
                SpectateRecoil(player, player);
            }
        }

        void OnExitZone(string ZoneID, BasePlayer player){
            if (permission.UserHasPermission(player.UserIDString, "RecoilViewer.zonemanager") && config.zonemanager && config.zoneslist.Contains(ZoneID)){
                CloseRecoil(player);
            }
        }

        //Arkan Hooks
        private void API_ArkanOnNoRecoilViolation(BasePlayer player, int NRViolationsNum, string json){
            if (config.arkan && json != null){
                if(ultArkan != player){
                    ultArkan = player;
                    foreach (var spectate in AutoArkan){
                        BasePlayer spect = FindPlayerByID(spectate);
                        if(spect == null) return;
                        CloseRecoil(spect);
                        SpectateRecoil(spect, player);
                    }  
                }
            }
        }
        #endregion

        #region Commands
        [ConsoleCommand("btnclose")]
        void ConsoleCmdBtnClose(ConsoleSystem.Arg arg){
            var player = arg.Player();
            var action = arg.Args?.Length > 0 ? arg.Args[0] : "null";
            if (action == "RecoilShow"){
                CloseRecoil(player);
                PrintToChat(player, Lang("Disabled"));
                return;
            }
        }

        [ConsoleCommand("recoil")]
        void ConsoleCmdRecoil(ConsoleSystem.Arg arg){
            var player = arg.Player();
            if (!permission.UserHasPermission(player.UserIDString, "RecoilViewer.admin")){
                player.ChatMessage(Lang("WithoutPermission"));
                return;
            }
            
            if(arg.Args == null){
                if (CheckViewerPlayer(player)){
                    CloseRecoil(player);
                    PrintToChat(player, Lang("Disabled"));
                } else {
                    PrintToChat(player, Lang("Usage"));
                }
                return;
            } else {
                if(arg.Args[0] == "off"){
                    NoSpectate.Add(player.userID);
                    CloseRecoil(player);
                    PrintToChat(player, Lang("DisabledSpectate"));
                    return;
                }

                if(arg.Args[0] == "arkan" && permission.UserHasPermission(player.UserIDString, "RecoilViewer.autoarkan") && config.arkan){
                    if(!AutoArkan.Contains(player.userID)){
                        AutoArkan.Add(player.userID);
                        PrintToChat(player, Lang("AutoArkanON"));
                    } else {
                        AutoArkan.Remove(player.userID);
                        CloseRecoil(player);
                        PrintToChat(player, Lang("AutoArkanOFF"));
                    }
                    return;
                } else {
                    PrintToChat(player, Lang("AutoArkanPermission"));
                    return;
                }
            }
        }

        [ChatCommand("recoil")]
        private void RecoilCommand(BasePlayer player, string command, string[] args){
            if (args.Length == 0){
                if (!permission.UserHasPermission(player.UserIDString, "RecoilViewer.player")){
                    player.ChatMessage(Lang("WithoutPermission"));
                    return;
                }
                if (CheckViewerPlayer(player)){
                    CloseRecoil(player);
                    PrintToChat(player, Lang("Disabled"));
                } else {
                    if (!CacheInfo.ContainsKey(player)){ 
                        CacheInfo.Add(player, new ShooterData()); 
                    }
                    if (!CacheInfo[player].Viewers.Contains(player)){
                        CacheInfo[player].Viewers.Add(player);
                    }
                    LaunchRecoil(player, player);
                }
                return;
            }

            if (!permission.UserHasPermission(player.UserIDString, "RecoilViewer.admin")){
                player.ChatMessage(Lang("WithoutPermission"));
                return;
            }

            if(args[0] == "arkan" && config.arkan){
                if(permission.UserHasPermission(player.UserIDString, "RecoilViewer.autoarkan")){
                    if(!AutoArkan.Contains(player.userID)){
                        AutoArkan.Add(player.userID);
                        PrintToChat(player, Lang("AutoArkanON"));
                    } else {
                        AutoArkan.Remove(player.userID);
                        CloseRecoil(player);
                        PrintToChat(player, Lang("AutoArkanOFF"));
                    }
                    return;
                } else {
                    PrintToChat(player, Lang("AutoArkanPermission"));
                    return;
                }
            }

            var ID = 0ul;
            BasePlayer search = (ulong.TryParse(args[0], out ID)) ? FindPlayerByID(ID) : FindPlayerByPartialName(args[0]);
            if (search == null){
                PrintToChat(player, Lang("ErrorSearch", args[0]));
                return;
            }

            DeleteActiveShooter(player);

            if (!CacheInfo.ContainsKey(search)){ 
                CacheInfo.Add(search, new ShooterData()); 
            }
            if (!CacheInfo[search].Viewers.Contains(player)){
                CacheInfo[search].Viewers.Add(player);
            }

            if (NoSpectate.Contains(player.userID)){ 
                NoSpectate.Remove(player.userID);
            }
            LaunchRecoil(player, search);
        }
        #endregion

        #region Functions
        private void SendHit(BasePlayer attacker, HitInfo hitinfo){
            var InfoPlayer = CacheInfo[attacker];
            var npc = hitinfo.HitEntity as BaseNpc;
            if (npc != null) {
                InfoPlayer.Hit++;
                foreach (BasePlayer Viewer in  InfoPlayer.Viewers) {
                    InfoHitsGUI(Viewer, attacker);
                }
                return;
            }
            var victim = hitinfo.HitEntity as BasePlayer;
            if (victim == null) return;
            if (victim == attacker) return;

            if (hitinfo.isHeadshot){
                InfoPlayer.Head++;
                foreach (BasePlayer Viewer in  InfoPlayer.Viewers) {
                    InfoHitsGUI(Viewer, attacker);
                }
                return;
            }
            InfoPlayer.Hit++;

            foreach (BasePlayer Viewer in  InfoPlayer.Viewers) {
                InfoHitsGUI(Viewer, attacker);
            }
        }

        private void ClearCacheInfo(BasePlayer player){
            var InfoPlayer = CacheInfo[player];
            InfoPlayer.OldShot = new Vector2(0, 0);
            InfoPlayer.AllShootsDisplay.Clear();
            InfoPlayer.ShotCounter = 0;
            InfoPlayer.Hit = 0;
            InfoPlayer.Head = 0;
        }

        private void SpectateRecoil(BasePlayer player, BasePlayer PlayerActive){
            if (!CacheInfo.ContainsKey(PlayerActive)){ 
                CacheInfo.Add(PlayerActive, new ShooterData()); 
            }
            if (!CacheInfo[PlayerActive].Viewers.Contains(player)){
                CacheInfo[PlayerActive].Viewers.Add(player);
            }

            LaunchRecoil(player,PlayerActive);
        }

        private void LaunchRecoil(BasePlayer player, BasePlayer PlayerActive){
            PrintToChat(player, Lang("EnableViewer", PlayerActive.IPlayer.Name));
            var item = PlayerActive.GetActiveItem();
            WindowsViewerGUI(player, item, PlayerActive.displayName);
            InfoHitsGUI(player, PlayerActive);
            RecoilShotsGUI(player,PlayerActive);
            return;
        }

        private void CloseRecoil(BasePlayer player){
            DeleteActiveShooter(player);
            CloseAllGUI(player);
        }

        private void DeleteActiveShooter(BasePlayer player){
            foreach (KeyValuePair<BasePlayer, ShooterData> PlayerActive in CacheInfo.ToList()) {
                //Puts("PlayerActive: " + PlayerActive.Key.displayName);
                foreach (BasePlayer viewer in PlayerActive.Value.Viewers.ToList()) {
                    //Puts("Viewer: " + viewer.displayName);
                    var InfoPlayer = CacheInfo[PlayerActive.Key];
                    if (InfoPlayer.Viewers.Contains(player)){
                        InfoPlayer.Viewers.Remove(player);
                        if (InfoPlayer.Viewers.Count == 0){
                            //Puts("Nobody sees it, we remove: " + PlayerActive.Key.displayName);
                            CacheInfo.Remove(PlayerActive.Key);
                        }
                        break;
                    }
                }
            }
        }

        bool CheckViewerPlayer(BasePlayer player){
            foreach (KeyValuePair<BasePlayer, ShooterData> PlayerActive in CacheInfo.ToList()) {
                foreach (BasePlayer viewer in PlayerActive.Value.Viewers.ToList()) {
                    if (CacheInfo[PlayerActive.Key].Viewers.Contains(player)){
                        //Puts("IF YOU ARE ALREADY WATCHING " + viewer.displayName);
                        return true;
                    }
                }
            }
            //Puts("NOT SEEING ANYONE " + player.displayName);
            return false;
        }

        private void CloseAllGUI(BasePlayer Viewer){
            CuiHelper.DestroyUi(Viewer, UI_panel_show);
            CuiHelper.DestroyUi(Viewer, UI_player_shot);
            CuiHelper.DestroyUi(Viewer, UI_player_hits);
            CuiHelper.DestroyUi(Viewer, UI_weapon_pattern);
        }
        #endregion

        #region Helpers
        private BasePlayer FindPlayerByPartialName(string name, bool sleepers = false){
            if (string.IsNullOrEmpty(name)) return null;
            BasePlayer player = null;
            try{
                for (int i = 0; i < BasePlayer.activePlayerList.Count; i++){
                    var p = BasePlayer.activePlayerList[i];
                    if (p == null) continue;
                    var pName = p?.displayName ?? string.Empty;
                    if (string.Equals(pName, name, StringComparison.OrdinalIgnoreCase)){
                        if (player != null) return null;
                        player = p;
                        return player;
                    }
                    if (pName.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0){
                        if (player != null) return null;
                        player = p;
                        return player;
                    }
                }
                
                if (sleepers){
                    for (int i = 0; i < BasePlayer.sleepingPlayerList.Count; i++){
                        var p = BasePlayer.sleepingPlayerList[i];
                        if (p == null) continue;
                        var pName = p?.displayName ?? string.Empty;
                        if (string.Equals(pName, name, StringComparison.OrdinalIgnoreCase)){
                            if (player != null) return null;
                            player = p;
                            return player;
                        }
                        if (pName.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0){
                            if (player != null) return null;
                            player = p;
                            return player;
                        }
                    }
                }
            }
            catch (Exception ex){
                PrintError(ex.ToString());
                return null;
            }
            return player;
        }

        private BasePlayer FindPlayerByID(ulong userID) { return BasePlayer.FindByID(userID) ?? BasePlayer.FindSleeping(userID) ?? null; }

        private class ShooterData {
            public List<BasePlayer> Viewers = new List<BasePlayer>();
            public Vector2 OldShot = new Vector2(0, 0);
            public Vector2 NewShot = new Vector2(0, 0);
            public Vector2 Displacement = new Vector2(0, 0);
            public Vector2 ShootDisplay = new Vector2(0, 0);
            public List<Vector2> AllShootsDisplay = new List<Vector2>();
            public int ShotCounter = 0;
            public int Hit = 0;
            public int Head = 0;
            public float Time;
        }

        private string GetImage(string name){ return ImageLibrary?.Call<string>("GetImage", name);  }

        private void AddImageLibrary(string name, string url){
            if (ImageLibrary == null || !ImageLibrary.IsLoaded){
                timer.Once(1f, () => {
                    AddImageLibrary(name, url);
                });
                return;
            }
            ImageLibrary.CallHook("AddImage", url, name, (ulong) 0);
        }
        #endregion

        #region GUI
        private void RecoilShotsGUI(BasePlayer Viewer, BasePlayer PlayerActive){
            CuiElementContainer container = UI.CreateElementContainer(UI_player_shot, "0 0 0 0", "0.81 0.65", "0.992 0.86");
            foreach (Vector2 point in CacheInfo[PlayerActive].AllShootsDisplay){
                UI.Point(ref container, UI_player_shot, config.recoil.point, config.recoil.pointsize, point, config.recoil.pointcolor, true);
            }

            if(config.showinfo){
                UI.Label(ref container, UI_player_shot, CacheInfo[PlayerActive].ShotCounter.ToString(), config.info.shots.textsize, config.info.shots.textposAnchorMin, config.info.shots.textposAnchorMax, config.info.shots.textcolor, TextAnchor.MiddleLeft, true);
            }

            CuiHelper.DestroyUi(Viewer, UI_player_shot);
            CuiHelper.AddUi(Viewer, container);
        }

        private void InfoHitsGUI(BasePlayer Viewer, BasePlayer PlayerActive){
            if(config.showinfo){
                int shots = CacheInfo[PlayerActive].ShotCounter;
                int hits = CacheInfo[PlayerActive].Hit;
                int heads = CacheInfo[PlayerActive].Head;

                CuiElementContainer container = UI.CreateElementContainer(UI_player_hits, "0 0 0 0", "0.810 0.52", "0.992 0.860");
                UI.Label(ref container, UI_player_hits, hits.ToString(), config.info.hit.textsize, config.info.hit.textposAnchorMin, config.info.hit.textposAnchorMax, config.info.hit.textcolor, TextAnchor.MiddleLeft, true);
                UI.Label(ref container, UI_player_hits, heads.ToString(), config.info.headshot.textsize, config.info.headshot.textposAnchorMin, config.info.headshot.textposAnchorMax, config.info.headshot.textcolor, TextAnchor.MiddleLeft, true);

                if(config.info.accuracy.enabled){
                    int accuracy = 0;
                    float maxProgress = 0.0f;
                    string color = "0 0 0 0";
                    if(shots > 0){ 
                        accuracy = (int) Math.Round((double) (100 * (hits+heads)) / shots); 
                        maxProgress = 0.01f * accuracy;
                        if(accuracy > 30){ color = config.info.accuracy.bgcolor30; }
                        if(accuracy > 60){ color = config.info.accuracy.bgcolor60; } 
                        if(accuracy > 80){ color = config.info.accuracy.bgcolor80; }
                    }
                    UI.Panel(ref container, UI_player_hits, color, "0.0 0.008", $"{(float) maxProgress} 0.1205", false, "assets/content/ui/namefontmaterial.mat");
                    UI.Label(ref container, UI_player_hits, Lang("Accuracy", accuracy), 12, "0 0.025", "1 0.1", config.info.accuracy.textcolor, TextAnchor.MiddleCenter, true);
                }

                CuiHelper.DestroyUi(Viewer, UI_player_hits);
                CuiHelper.AddUi(Viewer, container);
            }
        }

        private void WindowsViewerGUI(BasePlayer player, Item item, string ViewerName = ""){
            var mameActiveItem = (item != null ? item.info.displayName.english.ToUpper() : Lang("Empty"));
            
            CuiElementContainer container = UI.CreateElementContainer(UI_panel_show, config.windows.bgcolor, config.windows.posAnchorMin, config.windows.posAnchorMax, "assets/content/ui/uibackgroundblur-ingamemenu.mat");
            UI.Button(ref container, UI_panel_show, config.windows.closebgcolor, Lang("Close"), 12, "0.80 0.89", "0.98 0.98", $"global.btnclose RecoilShow");
            UI.Label(ref container, UI_panel_show, $"{mameActiveItem}", config.text.weaponsize, config.text.weaponposAnchorMin, config.text.weaponposAnchorMax, config.text.weaponcolor, TextAnchor.MiddleLeft, true);
            UI.Label(ref container, UI_panel_show, $"{ViewerName}", config.text.playersize, config.text.playerposAnchorMin, config.text.playerposAnchorMax, config.text.playercolor, TextAnchor.MiddleLeft);
            
            if(config.showinfo){
                UI.Image(ref container, UI_panel_show, GetImage(config.info.shots.iconURL), config.info.shots.iconposAnchorMin, config.info.shots.iconposAnchorMax);
                UI.Image(ref container, UI_panel_show, GetImage(config.info.hit.iconURL), config.info.hit.iconposAnchorMin, config.info.hit.iconposAnchorMax);
                UI.Image(ref container, UI_panel_show, GetImage(config.info.headshot.iconURL), config.info.headshot.iconposAnchorMin, config.info.headshot.iconposAnchorMax);
            }

            if(config.info.accuracy.enabled){
                UI.Panel(ref container, UI_panel_show, config.windows.bgcolor, "0 -0.15", "1 -0.03", false);
            }
            CuiHelper.DestroyUi(player, UI_panel_show);
            CuiHelper.AddUi(player, container);
            ShowPatternGUI(player, item, -1);
        }

        private void ShowPatternGUI(BasePlayer player, Item item, int count){
            CuiElementContainer container = UI.CreateElementContainer(UI_weapon_pattern, "0 0 0 0", "0.810 0.557", "0.992 0.863");
            if(item == null) NoPattern(container);
            else {
                if(!patternData.ContainsKey(item.info.shortname)) NoPattern(container);
                else {
                    int pos = 0;
                    foreach (Vector2 point in patternData[item.info.shortname]){
                        UI.Point(ref container, UI_weapon_pattern, config.pattern.point, config.pattern.pointsize, point, count < pos ? config.pattern.pointcolor : config.pattern.pointfirecolor);
                        pos++;
                    }
                }
            }
            CuiHelper.DestroyUi(player, UI_weapon_pattern);
            CuiHelper.AddUi(player, container);
        }

        private void NoPattern(CuiElementContainer container){
            UI.Label(ref container, UI_weapon_pattern, Lang("NoPattern"), config.text.nopatternsize, "0 0.15", "1 1", config.text.nopatterncolor, TextAnchor.MiddleCenter);
        }
        #endregion

        #region CUI Helper
        public class UI {
            public static CuiElementContainer CreateElementContainer(string panelName, string color, string aMin, string aMax, string mat = "assets/content/ui/namefontmaterial.mat"){
                CuiElementContainer container = new CuiElementContainer() { 
                    {
                        new CuiPanel {
                            Image = {Color = color, Material = mat},
                            RectTransform = {AnchorMin = aMin, AnchorMax = aMax},
                            CursorEnabled = false
                        },
                        new CuiElement().Parent = "Overlay",
                        panelName
                    }
                };
                return container;
            }

            static public void Panel(ref CuiElementContainer container, string panel, string color, string aMin, string aMax, bool cursor = false, string material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"){
                container.Add(new CuiPanel{
                    Image = { Color = color, Material = material},
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax},
                    CursorEnabled = cursor
                },
                panel);
            }

            static public void Label(ref CuiElementContainer container, string panel, string text, int size, string aMin, string aMax, string color = "1 1 1 0.6", TextAnchor align = TextAnchor.MiddleCenter, bool font = false){
                container.Add(new CuiLabel{
                    Text = { FontSize = size, Font = font? "robotocondensed-bold.ttf" : "robotocondensed-regular.ttf", Color = color, Align = align, Text = text},
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax}
                },
                panel);
            }

            static public void Point(ref CuiElementContainer container, string panel, string text, int fontsize, Vector2 point, string color, bool negative = false){
                container.Add(new CuiElement {
                    Parent = panel,
                    Components = {
                        new CuiTextComponent {
                            Text = text,
                            Color = color,
                            Align = TextAnchor.MiddleCenter,
                            FontSize = fontsize,
                        },
                        new CuiRectTransformComponent {
                            AnchorMin = "0 0.95",
                            AnchorMax = "1 1",
                            OffsetMin = $"0 {(negative ? -point.x : point.x)}",
                            OffsetMax = $"{(negative ? point.y : -point.y)} 0"
                        },
                    }
                });
            }

            static public void Button(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, string command, TextAnchor align = TextAnchor.MiddleCenter){
                container.Add(new CuiButton{
                    Button = { Color = color, Material = "assets/content/ui/namefontmaterial.mat", Command = command, FadeIn = 0f},
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax},
                    Text = { Text = text, Font = "robotocondensed-regular.ttf", FontSize = size, Align = align}
                },
                panel);
            }

            static public void Image(ref CuiElementContainer container, string panel, string png, string aMin, string aMax){
                container.Add(new CuiElement{
                    Name = CuiHelper.GetGuid(),
                    Parent = panel,
                    Components = {
                        new CuiRawImageComponent {Png = png},
                        new CuiRectTransformComponent {AnchorMin = aMin, AnchorMax = aMax}
                    }
                });
            }
        }
        #endregion

        #region Data
        void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(this.Title, patternData);
        void ReadData() => patternData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary <string, List <Vector2>>>(this.Title);
        #endregion

        #region Configuration
        private static ConfigData config = new ConfigData();
        
        private class ConfigData {
            [JsonProperty(PropertyName = "Use Spectate")]
            public bool spectate;

            [JsonProperty(PropertyName = "Use Arkan (Mod External: Arkan)")]
            public bool arkan = true;

            [JsonProperty(PropertyName = "Use Training (Mod External: AimTrain)")]
            public bool training;

            [JsonProperty(PropertyName = "Use ZoneManager (Mod External: ZoneManager)")]
            public bool zonemanager = true;

            [JsonProperty(PropertyName = "Active Zones (ZoneID)")]
            public List<string> zoneslist;

            [JsonProperty(PropertyName = "Show Shooting Info")]
            public bool showinfo;

            [JsonProperty(PropertyName = "Clean when reloading weapon")]
            public bool reloadClear;

            [JsonProperty(PropertyName = "Windows Config")]
            public Windows windows;

            [JsonProperty(PropertyName = "Text Display")]
            public Text text;

            [JsonProperty(PropertyName = "Info Display")]
            public Info info;

            [JsonProperty(PropertyName = "Pattern Weapons")]
            public Pattern pattern;

            [JsonProperty(PropertyName = "Recoil Player")]
            public Recoil recoil;
        }
        
        private class Windows {
            [JsonProperty(PropertyName = "Position AnchorMin")]
            public string posAnchorMin;

            [JsonProperty(PropertyName = "Position AnchorMax")]
            public string posAnchorMax;
            
            [JsonProperty(PropertyName = "Background Color")]
            public string bgcolor;

            [JsonProperty(PropertyName = "Close Text Color")]
            public string closecolor;

            [JsonProperty(PropertyName = "Close Background Color")]
            public string closebgcolor;
        }

        private class Text {
            [JsonProperty(PropertyName = "Weapon - AnchorMin")]
            public string weaponposAnchorMin;

            [JsonProperty(PropertyName = "Weapon - AnchorMax")]
            public string weaponposAnchorMax;

            [JsonProperty(PropertyName = "Weapon - Size")]
            public int weaponsize;
            
            [JsonProperty(PropertyName = "Weapon - Color")]
            public string weaponcolor;

            [JsonProperty(PropertyName = "Player Name - AnchorMin")]
            public string playerposAnchorMin;

            [JsonProperty(PropertyName = "Player Name - AnchorMax")]
            public string playerposAnchorMax;

            [JsonProperty(PropertyName = "Player Name - Size")]
            public int playersize;
            
            [JsonProperty(PropertyName = "Player Name - Color")]
            public string playercolor;

            [JsonProperty(PropertyName = "No Pattern - Size")]
            public int nopatternsize;

            [JsonProperty(PropertyName = "No Pattern - Color")]
            public string nopatterncolor;
        }

        private class Info {
            [JsonProperty(PropertyName = "Shoots Counter")]
            public InfoDetails shots;

            [JsonProperty(PropertyName = "Hit Counter")]
            public InfoDetails hit;

            [JsonProperty(PropertyName = "HeadShoot Counter")]
            public InfoDetails headshot;

            [JsonProperty(PropertyName = "Accuracy Display")]
            public Accuracy accuracy;
        }

        private class InfoDetails {
            [JsonProperty(PropertyName = "Icon URL")]
            public string iconURL;

            [JsonProperty(PropertyName = "Icon AnchorMin")]
            public string iconposAnchorMin;

            [JsonProperty(PropertyName = "Icon AnchorMax")]
            public string iconposAnchorMax;

            [JsonProperty(PropertyName = "Text AnchorMin")]
            public string textposAnchorMin;

            [JsonProperty(PropertyName = "Text AnchorMax")]
            public string textposAnchorMax;

            [JsonProperty(PropertyName = "Font Size")]
            public int textsize;
            
            [JsonProperty(PropertyName = "Font Color")]
            public string textcolor;
        }

        private class Accuracy {
            [JsonProperty(PropertyName = "Show Bar")]
            public bool enabled = true;

            [JsonProperty(PropertyName = "BG Color 30%")]
            public string bgcolor30 = "0.00 0.52 0.29 0.72";

            [JsonProperty(PropertyName = "BG Color 60%")]
            public string bgcolor60 = "0.97 0.51 0.14 0.72";

            [JsonProperty(PropertyName = "BG Color 80%")]
            public string bgcolor80 = "0.97 0.06 0.20 0.72";

            [JsonProperty(PropertyName = "Font Color")]
            public string textcolor;
        }

        private class Pattern {
            [JsonProperty(PropertyName = "Sets the point of the pattern")]
            public string point;
            
            [JsonProperty(PropertyName = "Point Size")]
            public int pointsize;

            [JsonProperty(PropertyName = "Point Color")]
            public string pointcolor;

            [JsonProperty(PropertyName = "Point Fire Color")]
            public string pointfirecolor;
        }

        private class Recoil {
            [JsonProperty(PropertyName = "Sets the point of the recoil")]
            public string point;
            
            [JsonProperty(PropertyName = "Point Size")]
            public int pointsize;

            [JsonProperty(PropertyName = "Point Color")]
            public string pointcolor;
        }

        private ConfigData GetDefaultConfig() {
            return new ConfigData {
                spectate = true,
                arkan = true,
                training = true,
                zonemanager = true,
                zoneslist = new List<string> { "91109370", "1013570", "69696969" },
                showinfo = true,
                reloadClear = true,
                windows = new Windows {
                    posAnchorMin = "0.810 0.571",
                    posAnchorMax = "0.992 0.895",
                    bgcolor = "0.0 0.0 0.0 0.75",
                    closecolor = "1 1 1 1",
                    closebgcolor = "0.87 0.0 0.0 0.9"
                },
                text = new Text {
                    weaponposAnchorMin = "0.03 0.91",
                    weaponposAnchorMax = "0.5 0.98",
                    weaponsize = 12,
                    weaponcolor = "0.8 0.8 0.8 1",
                    playerposAnchorMin = "0.03 0.84",
                    playerposAnchorMax = "0.6 0.94",
                    playersize = 10,
                    playercolor = "1 1 1 0.8",
                    nopatternsize = 15,
                    nopatterncolor = "1 0 0.38 1"
                },
                info = new Info {
                    shots = new InfoDetails {
                        iconURL = "https://i.imgur.com/XhvNi3x.png",
                        iconposAnchorMin = "0.03 0.78",
                        iconposAnchorMax = "0.08 0.83",
                        textposAnchorMin = "0.1 0.820",
                        textposAnchorMax = "0.4 0.910",
                        textsize = 12,
                        textcolor = "0.8 0.8 0.8 0.7"
                    },
                    hit = new InfoDetails {
                        iconURL = "https://i.imgur.com/XtiT7fO.png",
                        iconposAnchorMin = "0.03 0.71",
                        iconposAnchorMax = "0.08 0.76",
                        textposAnchorMin = "0.1 0.815",
                        textposAnchorMax = "0.4 0.885",
                        textsize = 11,
                        textcolor = "0.8 0.8 0.8 0.7"
                    },
                    headshot = new InfoDetails {
                        iconURL = "https://i.imgur.com/VlMQT8F.png",
                        iconposAnchorMin = "0.03 0.64",
                        iconposAnchorMax = "0.08 0.69",
                        textposAnchorMin = "0.1 0.75",
                        textposAnchorMax = "0.4 0.82",
                        textsize = 11,
                        textcolor = "0.8 0.8 0.8 0.7"
                    },
                    accuracy = new Accuracy {
                        enabled = true,
                        bgcolor30 = "0.00 0.52 0.29 0.72",
                        bgcolor60 = "0.97 0.51 0.14 0.72",
                        bgcolor80 = "0.97 0.06 0.20 0.72",
                        textcolor = "0.9 0.9 0.9 0.7"
                    }
                },
                pattern = new Pattern {
                    point = "•",
                    pointsize = 14,
                    pointcolor = "1 0 0.38 1",
                    pointfirecolor = "0.63 0.98 0.63 1.0"
                },
                recoil = new Recoil {
                    point = "⦿",
                    pointsize = 14,
                    pointcolor = "1 1 1 1"
                },
            };
        }

        protected override void LoadConfig(){
            base.LoadConfig();
            try {
                config = Config.ReadObject<ConfigData>();
                if (config == null){
                    LoadDefaultConfig();
                }
            } catch{
                PrintError("Configuration file is corrupt! Check your config file at https://jsonlint.com/");
                Interface.Oxide.RootPluginManager.RemovePlugin(this);
                return;
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig(){
            config = GetDefaultConfig();
        }

        protected override void SaveConfig(){
            Config.WriteObject(config);
        }
        #endregion
   
        #region Language
        protected override void LoadDefaultMessages() {
            lang.RegisterMessages(new Dictionary<string, string> {
                ["Usage"] = "Usage: <color=#65C0DD>/recoil PlayerName/SteamID</color>",
                ["NoPattern"] = "Pattern not available.",
                ["DisableViewer"] = "Disabling Recoil from {0}",
                ["EnableViewer"] = "Checking the Recoil of <color=#65C0DD>{0}</color>",
                ["WithoutPermission"] = "You do not have permission to use this command!",
                ["ErrorSearch"] = "There was an error trying to find the specified player: <color=#FD6036>{0}</color>",
                ["Close"] = "Close",
                ["Disabled"] = "The Viewer is now disabled.",
                ["DisabledSpectate"] = "The Viewer is temporarily disabled in viewer mode. It will be automatically re-enabled when using <color=#65C0DD>/recoil PlayerName/SteamID</color> in Chat.",
                ["Empty"] = "EMPTY HANDS",
                ["Accuracy"] = "Hits Accuracy <color=#65C0DD>{0}%</color>",
                ["AutoArkanON"] = "Arkan's automatic activation by NoRecoil is now <color=#65C0DD>ON</color>.",
                ["AutoArkanOFF"] = "Arkan's automatic activation by NoRecoil is now <color=#65C0DD>OFF</color>.",
                ["AutoArkanPermission"] = "You do not have permissions to use this feature or it is disabled in the settings."
            }, this);
        }

        private string Lang(string key, params object[] args) => string.Format(lang.GetMessage(key, this), args);

        private void PrintToChat(BasePlayer player, string message) => Player.Message(player, "<color=#FD6036>Recoil Viewer:</color> " + message);
        #endregion
    }
}