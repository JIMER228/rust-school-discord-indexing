//added image library
//fixed default text
//added input block

using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("CustomButtons", "WhitePlugins.ru", "2.0.6")]
    [Description("Custom UI Buttons")]

    public class CustomButtons : RustPlugin
    {   
        #region Create Cui
            
        private void CreateCui(BasePlayer player)
        {   
            if (!_playerData.ContainsKey(player.userID)) return;
            var _createCui = CUIClass.CreateOverlay("empty*_", "0 0 0 0", "0 0", "0 0", false, 0.0f, $"assets/icons/iconmaterial.mat");
           
            //overlay
            //CUIClass.CreatePanel(ref _createCui, "overlay_main", "Hud", "0 0 0 0", "0 0", "1 1", false, 0f, "assets/icons/iconmaterial.mat");
            //CUIClass.CreatePanel(ref _createCui, "overlay_offset", "overlay_main", "0 0 0 0.0", "0.5 0.5", "0.5 0.5", false, 0f, "assets/icons/iconmaterial.mat", "-680 -360", "680 360");
            foreach (var element in _cuiData)
            {       
                string elementName = element.Key;
                string panelColor = _cuiData[$"{element.Key}"].panelColor;
                string panelText = _cuiData[$"{element.Key}"].text;
                string anchorMin = _cuiData[$"{element.Key}"].OffsetMin;
                string anchorMax = _cuiData[$"{element.Key}"].OffsetMax;
                string _imgUrl = Img(_cuiData[$"{element.Key}"].imageUrl);
                string _imgAMin = $"{_cuiData[$"{element.Key}"].imgAnchorMin}"; 
                if (_cuiData[$"{element.Key}"].imgAnchorMin == null) _imgAMin = "0 0";
                string _imgAMax = $"{_cuiData[$"{element.Key}"].imgAnchorMax}";
                if (_cuiData[$"{element.Key}"].imgAnchorMax == null) _imgAMax = "1 1";
                if (_playerData[player.userID].hidden)
                {   
                    if (_cuiData[$"{element.Key}"].onCmdHide == "true")
                    {
                        continue;
                    }
                }
                if (permission.UserHasPermission(player.UserIDString, $"custombuttons.{element.Key}") || 
                    permission.UserHasPermission(player.UserIDString, $"custombuttons.admin"))
                {
                    if (_cuiData[$"{element.Key}"].uiType == "Overlay")
                    {   
                        
                        CUIClass.CreatePanel(ref _createCui, elementName, "Overlay", $"{panelColor}", config.anchSet.anchorMin, config.anchSet.anchorMax, false, 0f, "assets/icons/iconmaterial.mat", anchorMin, anchorMax);
                        if (_cuiData[$"{element.Key}"].imageUrl != null)
                        {
                            CUIClass.CreateImage(ref _createCui, elementName, _imgUrl, _imgAMin, _imgAMax);
                        
                        }
                        if (_cuiData[$"{element.Key}"].text != null)
                        {
                            CUIClass.CreateText(ref _createCui, "text" + elementName, elementName, config.fontSet.fontColor, $"{panelText}", config.fontSet.fontSize, "0 0", "1 1", TextAnchor.MiddleCenter, config.fontSet.fontStyle, config.fontSet.fontOutColor, config.fontSet.fontOutThic);       
                        }
                        if (_cuiData[$"{element.Key}"].panelType == "button")
                        {   
                            string _command = "";
                            if (_cuiData[$"{element.Key}"].panelChatCmd != null) _command = $"custombuttons_runbtncmd {elementName}";
                            CUIClass.CreateButton(ref _createCui, "btn" + elementName, elementName, "0 0 0 0", "", 13, "0 0", $"1 1", _command, "", "1 1 1 0.7", 0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf");
                        }
                    }
                    if (_cuiData[$"{element.Key}"].uiType == "Hud")
                    {   
                        CUIClass.CreatePanel(ref _createCui, elementName, "Hud", $"{panelColor}", config.anchSet.anchorMin, config.anchSet.anchorMax, false, 0f, "assets/icons/iconmaterial.mat", anchorMin, anchorMax);
                        if (_cuiData[$"{element.Key}"].imageUrl != null)
                        {
                            CUIClass.CreateImage(ref _createCui, elementName, _imgUrl, _imgAMin, _imgAMax);
                        }
                        if (_cuiData[$"{element.Key}"].text != null)
                        {
                            CUIClass.CreateText(ref _createCui, "text" + elementName, elementName, config.fontSet.fontColor, $"{panelText}", config.fontSet.fontSize, "0 0", "1 1", TextAnchor.MiddleCenter, config.fontSet.fontStyle, config.fontSet.fontOutColor, config.fontSet.fontOutThic);       
                        }
                        if (_cuiData[$"{element.Key}"].panelType == "button")
                        {   
                            string _command = $"";
                            if (_cuiData[$"{element.Key}"].panelChatCmd != null) _command = $"custombuttons_runbtncmd {elementName}";
                            CUIClass.CreateButton(ref _createCui, "btn" + elementName, elementName, "0 0 0 0", "", 13, "0 0", $"1 1", _command, "", "1 1 1 0.7", 0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf");
                        }
                    }
                }
            }
           
            DestroyCui(player); 
            CuiHelper.AddUi(player, _createCui); 
            //if (_playerData[player.userID].hidden) DestroyHidden(player);
            
        }

        private void DestroyCui(BasePlayer player)
        {   
            CuiHelper.DestroyUi(player, "empty*_"); 
            CuiHelper.DestroyUi(player, "overlay_main");
            foreach (var element in _cuiData)
            {   
                CuiHelper.DestroyUi(player, $"{element.Key}"); 
            }
        }

        private void CreatePartialCui(BasePlayer player, List<string> list)
        {   
            if (!_playerData.ContainsKey(player.userID)) return;
            var _createPartialCui = CUIClass.CreateOverlay("empty*_", "0 0 0 0", "0 0", "0 0", false, 0.0f, $"assets/icons/iconmaterial.mat");

            //overlay
            //CUIClass.CreatePanel(ref _createCui, "overlay_main", "Hud", "0 0 0 0", "0 0", "1 1", false, 0f, "assets/icons/iconmaterial.mat");
            //CUIClass.CreatePanel(ref _createCui, "overlay_offset", "overlay_main", "0 0 0 0.0", "0.5 0.5", "0.5 0.5", false, 0f, "assets/icons/iconmaterial.mat", "-680 -360", "680 360");
            foreach (var element in _cuiData)
            {       
                string elementName = element.Key;
                string panelColor = _cuiData[$"{element.Key}"].panelColor;
                string panelText = _cuiData[$"{element.Key}"].text;
                string anchorMin = _cuiData[$"{element.Key}"].OffsetMin;
                string anchorMax = _cuiData[$"{element.Key}"].OffsetMax;
                string _imgUrl = _cuiData[$"{element.Key}"].imageUrl;
                string _imgAMin = $"{_cuiData[$"{element.Key}"].imgAnchorMin}";
                string _imgAMax = $"{_cuiData[$"{element.Key}"].imgAnchorMax}";
                if (_playerData[player.userID].hidden)
                {   
                    if (_cuiData[$"{element.Key}"].onCmdHide == "true")
                    {
                        continue;
                    }
                }
                if (_playerData[player.userID].hidden)
                {   
                    if (_cuiData[$"{element.Key}"].onCmdHide == "true")
                    {
                        continue;
                    }
                }
                if (list.Contains("*"))
                { // ignore 
                } else { if (!list.Contains($"{element.Key}")) { continue; } }

                if (permission.UserHasPermission(player.UserIDString, $"custombuttons.{element.Key}") || 
                    permission.UserHasPermission(player.UserIDString, $"custombuttons.admin"))
                {
                    if (_cuiData[$"{element.Key}"].uiType == "Overlay")
                    {   
                        CUIClass.CreatePanel(ref _createPartialCui, elementName, "Overlay", $"{panelColor}", config.anchSet.anchorMin, config.anchSet.anchorMax, false, 0f, "assets/icons/iconmaterial.mat", anchorMin, anchorMax);
                        if (_cuiData[$"{element.Key}"].imageUrl != null)
                        {
                            CUIClass.CreateImage(ref _createPartialCui, elementName, _imgUrl, _imgAMin, _imgAMax);
                        }
                        if (_cuiData[$"{element.Key}"].text != null)
                        {
                            CUIClass.CreateText(ref _createPartialCui, "text" + elementName, elementName, config.fontSet.fontColor, $"{panelText}", config.fontSet.fontSize, "0 0", "1 1", TextAnchor.MiddleCenter, config.fontSet.fontStyle, config.fontSet.fontOutColor, config.fontSet.fontOutThic);       
                        }
                        if (_cuiData[$"{element.Key}"].panelType == "button")
                        {   
                            string _command = "";
                            if (_cuiData[$"{element.Key}"].panelChatCmd != null) _command = $"custombuttons_runbtncmd {elementName}";
                            CUIClass.CreateButton(ref _createPartialCui, "btn" + elementName, elementName, "0 0 0 0", "", 13, "0 0", $"1 1", _command, "", "1 1 1 0.7", 0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf");
                        }
                    }
                    if (_cuiData[$"{element.Key}"].uiType == "Hud")
                    {   
                        CUIClass.CreatePanel(ref _createPartialCui, elementName, "Hud", $"{panelColor}", config.anchSet.anchorMin, config.anchSet.anchorMax, false, 0f, "assets/icons/iconmaterial.mat", anchorMin, anchorMax);
                        if (_cuiData[$"{element.Key}"].imageUrl != null)
                        {
                            CUIClass.CreateImage(ref _createPartialCui, elementName, _imgUrl, _imgAMin, _imgAMax);
                        }
                        if (_cuiData[$"{element.Key}"].text != null)
                        {
                            CUIClass.CreateText(ref _createPartialCui, "text" + elementName, elementName, config.fontSet.fontColor, $"{panelText}", config.fontSet.fontSize, "0 0", "1 1", TextAnchor.MiddleCenter, config.fontSet.fontStyle, config.fontSet.fontOutColor, config.fontSet.fontOutThic);       
                        }
                        if (_cuiData[$"{element.Key}"].panelType == "button")
                        {   
                            string _command = $"";
                            if (_cuiData[$"{element.Key}"].panelChatCmd != null) _command = $"custombuttons_runbtncmd {elementName}";
                            CUIClass.CreateButton(ref _createPartialCui, "btn" + elementName, elementName, "0 0 0 0", "", 13, "0 0", $"1 1", _command, "", "1 1 1 0.7", 0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf");
                        }
                    }
                }
            }
           
            CloseWhen(player, list);
            CuiHelper.AddUi(player, _createPartialCui); 
            //if (_playerData[player.userID].hidden) DestroyHidden(player);
            
        }
        
        #endregion

        #region [Permissions]

        
        private void RegisterPerms()
        {
            foreach (var entry in _cuiData)
            {
                permission.RegisterPermission($"custombuttons.{entry.Key}", this);   
            }
        }
        

        #endregion
 
        #region [CuiData]
        

        private void SaveData()
        {
            if (_cuiData != null)
                Interface.Oxide.DataFileSystem.WriteObject($"{Name}/CuiData", _cuiData);
        }

        private Dictionary<string, CuiData> _cuiData; 

        private class CuiData
        {
            public string uiType;
            public string panelType;
            public string OffsetMin;
            public string OffsetMax;
            public string text;
            public string panelColor;
            public string panelChatCmd;
            public string imageUrl;
            public string imgAnchorMin;
            public string imgAnchorMax;
            public string onCmdHide;
        }

        private void LoadData()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile($"{Name}/CuiData"))
            {
                _cuiData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, CuiData>>($"{Name}/CuiData");
            }
            else
            {
                _cuiData = new Dictionary<string, CuiData>();
                SaveData();
            }
        }

        #endregion

        #region [PlayerData]

        private void SavePlayerData()
        {
            if (_playerData != null)
                Interface.Oxide.DataFileSystem.WriteObject($"{Name}/PlayerData", _playerData);
        }

        private Dictionary<ulong, PlayerData> _playerData;
        private class PlayerData
        {
            public bool hidden;
        }

        private void LoadPlayerData()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile($"{Name}/PlayerData"))
            {
                _playerData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerData>>($"{Name}/PlayerData");
            }
            else
            {
                _playerData = new Dictionary<ulong, PlayerData>();
                SavePlayerData();
            }
        }

        private void WritePlayerEntry(BasePlayer player)
        {
            if (!_playerData.ContainsKey(player.userID))
            {
                _playerData.Add(player.userID, new PlayerData());
                _playerData[player.userID].hidden = false;
                SavePlayerData();
            }
        }
        //Added this for testing
        object CustomCommands(BasePlayer player, string command, string[] args)
        {
            if (command == config.commSet.togglePanels)
            {
                custombuttonstoggle(player);
            }
            else if (command == config.commSet.openCmd)
            {
                chatcmdOpenMenu(player);
            }
            else if (command == config.commSet.listPanels)
            {
                custombuttons_list(player);
            }
            return null;
        }

        private void DestroyHidden(BasePlayer player)
        {
            foreach (var element in _cuiData)
            {       
                if (_cuiData[$"{element.Key}"].onCmdHide != null)
                {
                    if (_cuiData[$"{element.Key}"].onCmdHide == "true") CuiHelper.DestroyUi(player, $"{element.Key}");
                }
            }
        }

        

        #endregion

        #region [Hooks]
        
        private void CloseWhen(BasePlayer player, List<string> list)
        {
            if (list.Contains("*"))
            {
                DestroyCui(player);
            } else {
                foreach (string element in list)
                {
                    CuiHelper.DestroyUi(player, element); 
                }
            }  
        }
        void OnPlayerConnected(BasePlayer player) => WritePlayerEntry(player);

        void OnPlayerDeath(BasePlayer player, HitInfo info) => CloseWhen(player, config.addSet.whenDead); 
        void OnPlayerSleepEnded(BasePlayer player) => CreateCui(player);
         
        void OnLootEntity(BasePlayer player, BaseEntity entity) => CloseWhen(player, config.addSet.whenLooting);
        void OnLootEntityEnd(BasePlayer player, BaseEntity entity) => CreatePartialCui(player, config.addSet.whenLooting);
       
        void OnEntityMounted(ComputerStation entity, BasePlayer player) => CloseWhen(player, config.addSet.whenComputer);
        void OnEntityDismounted(ComputerStation entity, BasePlayer player) => CreatePartialCui(player, config.addSet.whenComputer);

        [PluginReference] Plugin ImageLibrary;

        private void OnServerInitialized()
        {
            LoadData();
            LoadPlayerData();
            LoadConfig();
            
            cmd.AddChatCommand(config.commSet.listPanels, this, "CustomCommands");
            cmd.AddChatCommand(config.commSet.openCmd, this, "CustomCommands");
            cmd.AddChatCommand(config.commSet.togglePanels, this, "CustomCommands");
            
            foreach (var _player in BasePlayer.activePlayerList) WritePlayerEntry(_player);
            foreach (var _player in BasePlayer.activePlayerList) CreateCui(_player);
            
            permission.RegisterPermission($"custombuttons.admin", this);
            RegisterPerms();

            if (ImageLibrary != null)
            {
                foreach (string element in _cuiData.Keys)
                {
                    if (_cuiData[element].imageUrl != null && _cuiData[element].imageUrl != "")
                    {
                        ImageLibrary.Call("AddImage", _cuiData[element].imageUrl, _cuiData[element].imageUrl);
                    }
                    
                }   
            }
            
        }

        private string Img(string url)
        {
            if (ImageLibrary != null) 
            {   
                if (!(bool) ImageLibrary.Call("HasImage", url))
                    return url;
                else
                    return (string) ImageLibrary?.Call("GetImage", url);
            }
            else return url;
        }

        private void OnServerSave()
        {
            SaveData();
            SavePlayerData();
        }

        private void Unload()
        {
            foreach (var _player in BasePlayer.activePlayerList)
            { 
               DestroyButtonMenuUI(_player);
               DestroyEnterName(_player);
               DestroyEditMenu(_player);
               DestroyEditPosMain(_player);
               DestroyEditType(_player);
               DestroyCui(_player);
               
            }
        }

        void OnPluginLoaded(Plugin name)
        {   
            if(name is CustomButtons)
            {   
                
            }     
        } 

        

        #endregion
       
        private Dictionary<string, string> storedCuiFields = new Dictionary<string, string>();
        
        #region [Commands]

        private void ClearSpecificField(string[] fieldNames)
        {
            foreach (string field in fieldNames)
            {
               if (storedCuiFields.ContainsKey(field))
                { storedCuiFields.Remove(field); } 
            } 
        }

        private void ClearStoredFields()
        {   
            string[] preSet = { "panelName", "uiType", "panelType", "AnchorMin",
            "AnchorMax", "text", "panelColor", "imageUrl", "imgAnchorMin", "imgAnchorMax", "panelChatCmd", "onCmdHide" }; 
            foreach (string s in preSet)
            {
               if (storedCuiFields.ContainsKey($"{s}"))
                { storedCuiFields.Remove($"{s}"); } 
            }
        }

        private void CheckStoredFields()
        {   
            string[] preSet = { "panelName", "uiType", "panelType", "AnchorMin",
            "AnchorMax", "text", "panelColor", "imageUrl", "imgAnchorMin", "imgAnchorMax", "panelChatCmd", "onCmdHide" }; 
            foreach (string s in preSet)
            {
                if (!storedCuiFields.ContainsKey($"{s}"))
                {   
                    Puts($"{s} not found");
                    break;
                } else { Puts($"{s} found"); }
            }
        }

        [ChatCommand("custombuttons_list")]
        private void custombuttons_list(BasePlayer player)
        {
            if (player == null) return;
            if (!permission.UserHasPermission(player.UserIDString, $"custombuttons.admin")) 
            { SendReply(player, "You don't have permission to do that (custombuttons.admin)."); return; }
            string list = "<size=16><color=#FF9300>List of CUI Elements</color></size> \n\n";
            foreach (var element in _cuiData)
            {list = list + $" <color=#FF9300>•</color> {element.Key}<size=11><color=#CDCDCD>({_cuiData[$"{element.Key}"].uiType})</color></size>";}
            SendReply(player, list);
        }

        [ChatCommand("custombuttonstoggle")]
        private void custombuttonstoggle(BasePlayer player)
        {
            if (player == null) return;
            WritePlayerEntry(player);
            if (_playerData[player.userID].hidden)
            {
                _playerData[player.userID].hidden = false;
            }
            else { _playerData[player.userID].hidden = true; }
            SavePlayerData();
            CreateCui(player);
        }

        [ConsoleCommand("custombuttons_runbtncmd")]
        private void custombuttons_runbtncmd(ConsoleSystem.Arg arg) 
        {
            var player = arg?.Player();
            var args = arg.Args;
            if (arg.Player() == null) return;
            string chatcmd = _cuiData[$"{args[0]}"].panelChatCmd;
            string replacedValues = chatcmd.Replace("{player.name}", $"{player.displayName}")
            .Replace("{player.steam}", $"{player.userID}");
            arg.Player().SendConsoleCommand($"chat.say \"/{replacedValues}\" ");
        }

        [ConsoleCommand("custombuttons_storechanges")]
        private void custombuttons_storechanges(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            var args = arg.Args;
            if (arg.Player() == null) return;
            string[] preSet = { "uiType", "panelType", "AnchorMin",
            "AnchorMax", "text", "panelColor", "imageUrl", "imgAnchorMin", "imgAnchorMax", "panelChatCmd", "onCmdHide" }; 
            
            if(args[0] == "textcolor")
            {
                if (storedCuiFields.ContainsKey("text"))
                {_cuiData[args[1]].text = storedCuiFields["text"];}
                if (storedCuiFields.ContainsKey("panelColor"))
                {_cuiData[args[1]].panelColor = storedCuiFields["panelColor"];}
                SaveData(); DestroyAllSubs(player); //save and destroy text edit menu
                CreateCui(player); 
                EditMenu(player, args[1]); PopUpWindow(player, "small", GetLang("changesSaved")); //open main edit menu + notification
                ClearSpecificField(preSet);
                return;
            }
            if(args[0] == "image")
            {
                if (storedCuiFields.ContainsKey("imageUrl"))
                {_cuiData[args[1]].imageUrl = storedCuiFields["imageUrl"];}
                if (storedCuiFields.ContainsKey("imgAnchorMin"))
                {_cuiData[args[1]].imgAnchorMin = storedCuiFields["imgAnchorMin"];}
                if (storedCuiFields.ContainsKey("imgAnchorMax"))
                {_cuiData[args[1]].imgAnchorMax = storedCuiFields["imgAnchorMax"];}
                SaveData(); DestroyAllSubs(player); //save and destroy text edit menu
                CreateCui(player); 
                EditMenu(player, args[1]); PopUpWindow(player, "small", GetLang("changesSaved")); //open main edit menu + notification
                ClearSpecificField(preSet);
                return;
            }

            if(args[0] == "cui")
            {
                if (storedCuiFields.ContainsKey("uiType"))
                {_cuiData[args[1]].uiType = storedCuiFields["uiType"];}
                if (storedCuiFields.ContainsKey("panelType"))
                {_cuiData[args[1]].panelType = storedCuiFields["panelType"];}
                if (storedCuiFields.ContainsKey("panelChatCmd"))
                {_cuiData[args[1]].panelChatCmd = storedCuiFields["panelChatCmd"];}
                SaveData(); DestroyAllSubs(player); //save and destroy text edit menu
                CreateCui(player); 
                EditMenu(player, args[1]); PopUpWindow(player, "small", GetLang("changesSaved")); //open main edit menu + notification
                ClearSpecificField(preSet);
                return;
            }
            
        }

        [ConsoleCommand("custombuttons_discardchanges")]
        private void custombuttons_discardchanges(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            var args = arg.Args;
            if (arg.Player() == null) return;
            //if (args.Length < 2) return;
            string[] preSet = { "uiType", "panelType", "AnchorMin",
            "AnchorMax", "text", "panelColor", "imageUrl", "imgAnchorMin", "imgAnchorMax", "panelChatCmd", "onCmdHide" }; 
            ClearSpecificField(preSet);
            EditMenu(player, $"{args[0]}");   
        }

        [ChatCommand("custombuttons")]
        private void chatcmdOpenMenu(BasePlayer player)
        {   
            if (player == null) return;
            if (!permission.UserHasPermission(player.UserIDString, $"custombuttons.admin")) 
            { SendReply(player, "You don't have permission to do that (custombuttons.admin)."); return; }
            
            MainMenuUI(player);
        }

        [ConsoleCommand("storemenuvalue")]
        private void storemenuvalue(ConsoleSystem.Arg arg)
        {   
                var player = arg?.Player(); 
                var args = arg.Args;
                if (arg.Player() == null) return;
                //if (args.Length > 5) {Puts($"Too many arguments"); return;}
                if (args.Length < 2) return;
                if (!storedCuiFields.ContainsKey($"{args[0]}"))
                {
                    storedCuiFields.Add($"{args[0]}", $"");
                    _debug($"{args[0]} Not found, adding to dictionary");
                } 
                if(args[0] == "panelColor") 
                { 
                    if (args.Length != 5) { PopUpWindow(player, "small", GetLang("wrongColorUsage")); return;}
                    storedCuiFields[$"{args[0]}"] = $"{args[1]} {args[2]} {args[3]} {args[4]}"; 
                    _debug($"{args[0]}:{storedCuiFields[$"{args[0]}"]}");
                    ColorDisplay(player);
                    return;
                }
                if(args[0] == "AnchorMin" || args[0] == "AnchorMax" || args[0] == "imgAnchorMin"
                || args[0] == "imgAnchorMax") 
                {   
                    if (args.Length != 3) {PopUpWindow(player, "small", GetLang("wrongAnchorUsage")); return;}
                    storedCuiFields[$"{args[0]}"] = $"{args[1]} {args[2]}"; 
                    _debug($"{args[0]}:{storedCuiFields[$"{args[0]}"]}");
                    return;
                }

            #region editing spec.
                
                if(args[0] == "panelNameEdit") 
                { 
                    if (args.Length != 2) { EditBtn(player, false, ""); PopUpWindow(player, "small", GetLang("tooManyArguments")); return;}
                    if (!_cuiData.ContainsKey($"{args[1]}")) { EditBtn(player, false, ""); PopUpWindow(player, "small", GetLang("panelNameNotFound")); return;}
                    storedCuiFields[$"{args[0]}"] = $"{args[1]}";
                    
                    _debug($"{args[0]}:{storedCuiFields[$"{args[0]}"]}");
                    EditBtn(player, true, $"{storedCuiFields[$"{args[0]}"]}");
                    
                    return;
                }

                if(args[0] == "imageUrlEdit")
                {
                    if (args.Length > 3) { PopUpWindow(player, "small", GetLang("tooManyArguments")); return;}
                    storedCuiFields["imageUrl"] = args[2]; 
                    _debug($"{args[0]}:{storedCuiFields[$"imageUrl"]}");
                    EditImagePreview(player, args[1]);
                    return;

                }

                if(args[0] == "panelColorEdit") 
                { 
                    if (args.Length != 6) { PopUpWindow(player, "small", GetLang("wrongColorUsage")); return;}
                    storedCuiFields["panelColor"] = $"{args[2]} {args[3]} {args[4]} {args[5]}"; 
                    _debug($"{args[0]}:{storedCuiFields[$"{args[0]}"]}");
                    EditColor(player, args[1]);
                    return;
                }

                if(args[0] == "uiTypeEdit")
                {
                    if (args.Length > 3) { PopUpWindow(player, "small", GetLang("tooManyArguments")); return;}
                    _cuiData[$"{args[1]}"].uiType = args[2]; 
                    _debug($"{args[0]}:{_cuiData[$"{args[1]}"].uiType}");
                    SaveData();
                    EditType(player, args[1]);
                    return;
                }

                if(args[0] == "panelTypeEdit")
                {
                    if (args.Length > 3) { PopUpWindow(player, "small", GetLang("tooManyArguments")); return;}
                    _cuiData[$"{args[1]}"].panelType = args[2]; 
                    _debug($"{args[0]}:{_cuiData[$"{args[1]}"].panelType}");
                    SaveData();
                    EditType(player, args[1]);
                    return;
                }

                if(args[0] == "onCmdHideEdit")
                {
                    if (args.Length > 3) { PopUpWindow(player, "small", GetLang("tooManyArguments")); return;}
                    _cuiData[$"{args[1]}"].onCmdHide = args[2]; 
                    _debug($"{args[0]}:{_cuiData[$"{args[1]}"].onCmdHide}");
                    SaveData();
                    EditType(player, args[1]);
                    return;
                }

                
                if(args[0] == "panelChatCmdEdit")
                {
                    if (args.Length > 3) { PopUpWindow(player, "small", GetLang("tooManyArguments")); return;}
                    if (!storedCuiFields.ContainsKey("panelChatCmd")) { PopUpWindow(player, "small", GetLang("nothingToSave")); return;}
                    _cuiData[$"{args[1]}"].panelChatCmd = storedCuiFields["panelChatCmd"]; 
                    _debug($"{args[0]}:{_cuiData[$"{args[1]}"].panelChatCmd}");
                    SaveData();
                    EditType(player, args[1]);
                    return;
                }
                

            #endregion

                if (args.Length > 2) { PopUpWindow(player, "small", GetLang("tooManyArguments")); return;}
                storedCuiFields[$"{args[0]}"] = $"{args[1]}";
                _debug($"{args[0]}:{storedCuiFields[$"{args[0]}"]}");

                if(args[0] == "onCmdHide") {OpenCloseOption(player);}
                if(args[0] == "uiType") {UiTypeButtons(player);}
                if(args[0] == "panelType") {PanelTypeButtons(player);}
                if(args[0] == "imageUrl") {ImagePreview(player);}

                

                
        }

        [ConsoleCommand("custombuttons_save")]
        private void custombuttons_save(ConsoleSystem.Arg arg)
        {   
            var player = arg?.Player(); 
            var args = arg.Args;
            //if (arg.Player() == null) return;

                if (!storedCuiFields.ContainsKey("uiType") || !storedCuiFields.ContainsKey("panelType")
                || !storedCuiFields.ContainsKey("AnchorMin") || !storedCuiFields.ContainsKey("AnchorMax")
                || !storedCuiFields.ContainsKey("panelName"))
                {   
                    PopUpWindow(player, "small", GetLang("missingCoreValues"));
                    _debug("Missing core values");
                    return;
                }
            
                if (_cuiData.ContainsKey(storedCuiFields["panelName"]))
                {
                    _debug("Duplicate panel name");
                    return;
                }
                string pN = storedCuiFields["panelName"];

                _cuiData.Add($"{storedCuiFields["panelName"]}", new CuiData());
                SaveData();
                //MUST HAVE
                    _cuiData[pN].uiType = storedCuiFields["uiType"]; 
                    _cuiData[pN].panelType = storedCuiFields["panelType"];
                    _cuiData[pN].OffsetMin = storedCuiFields["AnchorMin"];
                    _cuiData[pN].OffsetMax = storedCuiFields["AnchorMax"];
                //OPTIONAL REPLACED WITH DEFAULT VALUES
                if (storedCuiFields.ContainsKey("panelColor"))
                {_cuiData[pN].panelColor = storedCuiFields["panelColor"];}
                if (storedCuiFields.ContainsKey("text"))
                {_cuiData[pN].text = storedCuiFields["text"];}
                if (storedCuiFields.ContainsKey("panelChatCmd"))
                {_cuiData[pN].panelChatCmd = storedCuiFields["panelChatCmd"];}
                if (storedCuiFields.ContainsKey("imageUrl"))
                {_cuiData[pN].imageUrl = storedCuiFields["imageUrl"];}
                if (storedCuiFields.ContainsKey("imgAnchorMin"))
                {_cuiData[pN].imgAnchorMin = storedCuiFields["imgAnchorMin"];}
                if (storedCuiFields.ContainsKey("imgAnchorMax"))
                {_cuiData[pN].imgAnchorMax = storedCuiFields["imgAnchorMax"];}
                if (storedCuiFields.ContainsKey("onCmdHide"))
                {_cuiData[pN].onCmdHide = storedCuiFields["onCmdHide"];}
                SaveData();
                DestroyButtonMenuUI(player);
                CreateCui(player);
                MainMenuUI(player);
                PopUpWindow(player, "small", $"<size=45>✔</size>\n<size=18>{pN} has been created!</size>\n");
                

        }

        [ConsoleCommand("custombuttons_edit")]
        private void custombuttons_edit(ConsoleSystem.Arg arg)
        { 
            var player = arg?.Player(); 
            var args = arg.Args;
            if (args[0] == "backtomain") {EditMenu(player, $"{args[1]}"); return;}
            if (args[0] == "backtostart") {DestroyAllSubs(player); MainMenuUI(player); return;}
            if (!storedCuiFields.ContainsKey("panelNameEdit")) { PopUpWindow(player, "small", GetLang("panelNameNotFound")); return; }
            
            DestroyEnterName(player);
            EditMenu(player, $"{args[0]}");
        }

        [ConsoleCommand("custombuttons_editmenu")]
        private void custombuttons_editmenu(ConsoleSystem.Arg arg)
        { 
            var player = arg?.Player(); 
            var args = arg.Args;
            if (!_cuiData.ContainsKey(args[1])) { PopUpWindow(player, "small", GetLang("panelNameNotFound")); return; }
            //if (!storedCuiFields.ContainsKey("panelNameEdit")) { PopUpWindow(player, "small", GetLang("panelNameNotFound")); return; }
            
            if(args[0] == "color")
            {EditText(player, args[1]); return;}
            if(args[0] == "image")
            {EditImage(player, args[1]); return;}
            if(args[0] == "cui")
            {EditType(player, args[1]); return;}
            if(args[0] == "anchor")
            {DestroyAllSubs(player); EditPosMain(player, args[1]); return;}
            /*
            color
            image
            cui
            anchor
            */
        }

        [ConsoleCommand("custombuttons_open")]
        private void custombuttons_open(ConsoleSystem.Arg arg) 
        {   
            var player = arg?.Player(); 
            var args = arg.Args;
            if (arg.Player() == null) return;
            //if (args.Length < 1) 
            //DestroyMainUI(player);
            if(args[0] == "mainmenu")
            {DestroyEnterName(player); DestroyButtonMenuUI(player); MainMenuUI(player); return;}
            
            if(args[0] == "tooltip-offset")
            {PopUpWindow(player, "tooltip-offset", "");}

            if(args[0] == "closemain")
            {DestroyMainUI(player); return;}

            if(args[0] == "new")
            {DestroyMainUI(player); NewButtonMenuUI(player); return;}

            if(args[0] == "edit")
            {DestroyMainUI(player); EnterName(player); return;}

            if(args[0] == "closepp")
            {DestroyPopUpWindow(player); return;}

            if(args[0] == "discard")
            { DestroyButtonMenuUI(player); NewButtonMenuUI(player); return;}
        }

        #endregion 

        #region [CUI Panels]
            #region Main Menu
                private void MainMenuUI(BasePlayer player)
                {                
                    string editor_Text = $"<size=23>CUI EDITOR</size>\n<color=#D1D1D1>Cui elements are saved into data file where they can be edited. If you don't like working with json files you can use the in-game editor menu to create ui panels.</color>";
                    string support_Text = $"<size=23>SUPPORT</size>\n<color=#D1D1D1>If you find a bug or have any questions related to my plugins, feel free to reach out.</color>\n\n<size=23>CONTACT</size>\n<color=#D1D1D1>The best way to get in touch is to message me on my discord.</color>";
                    //Background
                    var _baseUI = CUIClass.CreateOverlay("MainMenu", "0 0 0 0.4", "0 0", "1 1", true, 0.0f, $"assets/content/ui/uibackgroundblur.mat");
                    //Main_Div
                    CUIClass.CreatePanel(ref _baseUI, "main_div", "MainMenu", "0.25 0.23 0.22 0.95", "0.28 0.2", "0.72 0.75", false, 0f, "assets/content/ui/uibackgroundblur.mat");
                    CUIClass.CreatePanel(ref _baseUI, "title_div", "main_div", "0.11 0.11 0.11 0.95", "-0.005 0.9", "1.005 1", false, 0f, "assets/content/ui/uibackgroundblur.mat");
                    CUIClass.CreateText(ref _baseUI, "title_text", "title_div", "1 1 1 1", $"Custom Buttons <size=13><color=#D1D1D1>{Version}</color></size>", 18, "0.02 0", "1 1", TextAnchor.MiddleLeft, $"robotocondensed-bold.ttf", "0 0 0 1", $"1.5 1.5");
                    //CUIClass.CreateImage(ref _baseUI, "BasePanel", $"image-URL", $"0 0", $"1 1");
                    //CUIClass.CreatePanel(ref _baseUI, "BasePanel", "BaseUI", "0.16 0.34 0.49 1.0", "0.2 0.2", "0.8 0.8", false, 0f, "assets/content/ui/uibackgroundblur.mat");
                    //Middle_Div
                    CUIClass.CreatePanel(ref _baseUI, "splitter_div", "main_div", "1 1 1 0.4", "0.4995 0.20", "0.50 0.75", false, 0f, "assets/icons/iconmaterial.mat");
                    //Text_
                    CUIClass.CreateText(ref _baseUI, "editor_text", "main_div", "1 1 1 1", editor_Text, 14, "0.05 0", "0.45 0.79", TextAnchor.UpperLeft, $"robotocondensed-bold.ttf", "0 0 0 0.7", $"0.0 0.0");
                    CUIClass.CreateText(ref _baseUI, "support_text", "main_div", "1 1 1 1", support_Text, 14, "0.55 0", "0.95 0.79", TextAnchor.UpperLeft, $"robotocondensed-bold.ttf", "0 0 0 0.7", $"0.0 0.0");
                    //CUIClass.CreateInput(ref _baseUI, "_amountInput", "BasePanel", "1 1 1 1", 15, "0.2 0.3", "0.8 0.8", "robotocondensed-bold.ttf", $"storemenuvalue panelName", TextAnchor.MiddleLeft);
                    CUIClass.CreateButton(ref _baseUI, "btn_create", "main_div", "0.38 0.51 0.16 0.85", $"Create New", 16, "0.12 0.37", $"0.38 0.48", $"custombuttons_open new", "", "1 1 1 0.5", 0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf");
                    CUIClass.CreateButton(ref _baseUI, "btn_edit", "main_div", "0.16 0.34 0.49 0.85", $"Edit Existing", 16, "0.12 0.20", $"0.38 0.31", $"custombuttons_open edit", "", "1 1 1 0.5", 0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf");
                    CUIClass.CreateButton(ref _baseUI, "btn_close", "title_div", "0.56 0.20 0.15 0.75", $"<size=13><color=#FFB8AA>CLOSE</color></size>", 19, "0.9 0.15", $"0.99 0.85", $"custombuttons_open closemain", "", "1 1 1 0.5", 0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf");
                    CUIClass.CreateButton(ref _baseUI, "btn_discord", "main_div", "0.45 0.54 0.85 0.35", $"!David#1337", 16, "0.62 0.20", $"0.88 0.31", $"custombuttons_open discord", "", "1 1 1 0.7", 0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf");
                    DestroyMainUI(player);
                    CuiHelper.AddUi(player, _baseUI); 
                }

                /*
                    
                    public string imageUrl;
                    public string imgAnchorMin;
                    public string imgAnchorMax;
                    public bool onCmdHide;
                    public bool onCmdOpen;
                */

                private void DestroyMainUI(BasePlayer player)
                {
                    CuiHelper.DestroyUi(player, "MainMenu"); 
                }
            #endregion
            #region Create New Menu
                private void NewButtonMenuUI(BasePlayer player)
                {                
                    string editor_Text = $"<size=25>CUI EDITOR</size>\n<color=#D1D1D1>Cui elements are saved into data file where they can be edited. If you don't like working with json files you can use the in-game editor menu to create ui panels.</color>";
                    string support_Text = $"<size=25>SUPPORT</size>\n<color=#D1D1D1>If you find a bug or have any questions related to my plugins, feel free to reach out.</color>\n\n<size=25>CONTACT</size>\n<color=#D1D1D1>The best way to get in touch is to message me in my discord channel for verified customers.</color>";
                    //Background
                    var _createNewMenu = CUIClass.CreateOverlay("NewMenu", "0 0 0 0.4", "0 0", "1 1", true, 0.0f, $"assets/content/ui/uibackgroundblur.mat");
                    //Main_Div

                    CUIClass.CreatePanel(ref _createNewMenu, "main_div", "NewMenu", "0.25 0.23 0.22 0.0", "0.25 0.15", "0.75 0.9", false, 0f, "assets/icons/iconmaterial.mat");
                    CUIClass.CreatePanel(ref _createNewMenu, "main_div2", "main_div", "0.25 0.23 0.22 0.95", "0 0", "1 0.95", false, 0f, "assets/content/ui/uibackgroundblur.mat");
                    CUIClass.CreatePanel(ref _createNewMenu, "title_div", "main_div2", "0.11 0.11 0.11 0.95", "-0.005 0.92", "1.002 1", false, 0f, "assets/content/ui/uibackgroundblur.mat");
                    CUIClass.CreateText(ref _createNewMenu, "title_text", "title_div", "1 1 1 1", $"Create New Cui Element <size=13><color=#D1D1D1></color></size>", 18, "0.02 0", "1 1", TextAnchor.MiddleLeft, $"robotocondensed-bold.ttf", "0 0 0 1", $"1.5 1.5");
                    CUIClass.CreateButton(ref _createNewMenu, "title_btn", "title_div", "0.56 0.20 0.15 0.65", $"✕", 11, "0.945 0.2", $"0.99 0.80", $"custombuttons_open mainmenu", "", "1 1 1 0.7", 0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf");

                    //LEFT SIDE
                        //Panel NAME
                        CUIClass.CreatePanel(ref _createNewMenu, "field_div", "main_div", "0.11 0.11 0.11 0.95", "0.04 0.72", $"0.45 0.78", false, 0f, "assets/icons/iconmaterial.mat");
                            CUIClass.CreateText(ref _createNewMenu, "panelType_text", "field_div", "1 1 1 0.3", "PANEL NAME:", 11, "0.03 0", "0.95 0.9", TextAnchor.MiddleLeft, $"robotocondensed-bold.ttf", "0 0 0 0.7", $"0.0 0.0");
                            CUIClass.CreateInput(ref _createNewMenu, "_ammountInput", "field_div", "1 1 1 1", 17, "0.30 0.0", "1 0.95", "robotocondensed-bold.ttf", $"storemenuvalue panelName", TextAnchor.MiddleLeft);
                    
                        //UI TYPE
                        CUIClass.CreatePanel(ref _createNewMenu, "field_div2", "main_div", "0.11 0.11 0.11 0.95", "0.04 0.633", $"0.45 0.71", false, 0f, "assets/icons/iconmaterial.mat");
                        CUIClass.CreateText(ref _createNewMenu, "uiType_text", "field_div2", "1 1 1 0.3", "UI TYPE", 11, "0.05 0", "0.95 1", TextAnchor.MiddleLeft, $"robotocondensed-bold.ttf", "0 0 0 0.7", $"0.0 0.0");
                            //UI TYPE BUTTONS 
                        //Panel Type
                        CUIClass.CreatePanel(ref _createNewMenu, "field_div3", "main_div", "0.11 0.11 0.11 0.95", "0.04 0.545", $"0.45 0.622", false, 0f, "assets/icons/iconmaterial.mat");
                        CUIClass.CreateText(ref _createNewMenu, "panelType_text", "field_div3", "1 1 1 0.3", "PANEL TYPE", 11, "0.05 0", "0.95 1", TextAnchor.MiddleLeft, $"robotocondensed-bold.ttf", "0 0 0 0.7", $"0.0 0.0");
                            //PANEL TYPE BUTTONS
                            //PANEL COLOR
                        CUIClass.CreatePanel(ref _createNewMenu, "field_div4", "main_div", "0.11 0.11 0.11 0.95", "0.04 0.475", $"0.45 0.535", false, 0f, "assets/icons/iconmaterial.mat");
                            CUIClass.CreateText(ref _createNewMenu, "panelType_text", "field_div4", "1 1 1 0.3", "PANEL COLOR:", 11, "0.03 0", "0.95 0.9", TextAnchor.MiddleLeft, $"robotocondensed-bold.ttf", "0 0 0 0.7", $"0.0 0.0");
                            CUIClass.CreateInput(ref _createNewMenu, "_ammountInput", "field_div4", "1 1 1 1", 14, "0.32 0.0", "1 0.95", "robotocondensed-bold.ttf", $"storemenuvalue panelColor", TextAnchor.MiddleLeft);
                            //CUIClass.CreatePanel(ref _createNewMenu, "field_color4", "field_div4", "0.16 0.34 0.49 1.0", "0.9 0.2", $"0.975 0.8", false, 0f, "assets/icons/iconmaterial.mat");
                        //PANEL AnchorMin
                        CUIClass.CreatePanel(ref _createNewMenu, "field_div5", "main_div", "0.11 0.11 0.11 0.95", "0.04 0.4", $"0.45 0.46", false, 0f, "assets/icons/iconmaterial.mat");
                            CUIClass.CreateText(ref _createNewMenu, "panelType_text", "field_div5", "1 1 1 0.3", "OFFSET MIN:", 11, "0.03 0", "0.95 0.9", TextAnchor.MiddleLeft, $"robotocondensed-bold.ttf", "0 0 0 0.7", $"0.0 0.0");
                            CUIClass.CreateInput(ref _createNewMenu, "_ammountInput", "field_div5", "1 1 1 1", 14, "0.32 0.0", "1 0.95", "robotocondensed-bold.ttf", $"storemenuvalue AnchorMin", TextAnchor.MiddleLeft);
                            CUIClass.CreateButton(ref _createNewMenu, "btn_discord", "field_div5", "0.33 0.33 0.33 0.55", $"?", 11, "0.9 0.2", $"0.975 0.8", $"custombuttons_open tooltip-offset", "", "1 1 1 0.7", 0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf");
                        //PANEL AnchorMax
                        CUIClass.CreatePanel(ref _createNewMenu, "field_div6", "main_div", "0.11 0.11 0.11 0.95", "0.04 0.325", $"0.45 0.385", false, 0f, "assets/icons/iconmaterial.mat");
                            CUIClass.CreateText(ref _createNewMenu, "panelType_text", "field_div6", "1 1 1 0.3", "OFFSET MAX:", 11, "0.03 0", "0.95 0.9", TextAnchor.MiddleLeft, $"robotocondensed-bold.ttf", "0 0 0 0.7", $"0.0 0.0");
                            CUIClass.CreateInput(ref _createNewMenu, "_ammountInput", "field_div6", "1 1 1 1", 14, "0.32 0.0", "1 0.95", "robotocondensed-bold.ttf", $"storemenuvalue AnchorMax", TextAnchor.MiddleLeft);
                            CUIClass.CreateButton(ref _createNewMenu, "btn_discord", "field_div6", "0.33 0.33 0.33 0.55", $"?", 11, "0.9 0.2", $"0.975 0.8", $"custombuttons_open tooltip-offset", "", "1 1 1 0.7", 0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf");
                        //PANEL TEXT
                        CUIClass.CreatePanel(ref _createNewMenu, "field_div7", "main_div", "0.11 0.11 0.11 0.95", "0.04 0.25", $"0.45 0.31", false, 0f, "assets/icons/iconmaterial.mat");
                            CUIClass.CreateText(ref _createNewMenu, "panelType_text", "field_div7", "1 1 1 0.3", "TEXT:", 11, "0.03 0", "0.95 0.9", TextAnchor.MiddleLeft, $"robotocondensed-bold.ttf", "0 0 0 0.7", $"0.0 0.0");
                            CUIClass.CreateInput(ref _createNewMenu, "_ammountInput", "field_div7", "1 1 1 1", 14, "0.20 0.0", "1 0.95", "robotocondensed-bold.ttf", $"storemenuvalue text", TextAnchor.MiddleLeft);
                        //COMMAND TEXT
                        CUIClass.CreatePanel(ref _createNewMenu, "field_div8", "main_div", "0.11 0.11 0.11 0.95", "0.04 0.17", $"0.45 0.235", false, 0f, "assets/icons/iconmaterial.mat");
                            CUIClass.CreateText(ref _createNewMenu, "panelType_text", "field_div8", "1 1 1 0.3", "CHAT COMMAND:  <size=14>/</size>", 11, "0.03 0", "0.95 0.9", TextAnchor.MiddleLeft, $"robotocondensed-bold.ttf", "0 0 0 0.7", $"0.0 0.0");
                            CUIClass.CreateInput(ref _createNewMenu, "_ammountInput", "field_div8", "1 1 1 1", 14, "0.40 0.0", "1 0.95", "robotocondensed-bold.ttf", $"storemenuvalue panelChatCmd", TextAnchor.MiddleLeft);
                    
                        CUIClass.CreateText(ref _createNewMenu, "panelType_text", "main_div", "1 1 1 0.3", "Use quote marks while typing multiple words into text and command field.", 13, "0.06 0.10", "0.44 0.16", TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf", "0 0 0 0.7", $"0.0 0.0");

                    // RIGHT SIDE      
                        //ICON URL
                            CUIClass.CreatePanel(ref _createNewMenu, "field_div9", "main_div", "0.11 0.11 0.11 0.95", "0.545 0.72", $"0.95 0.78", false, 0f, "assets/icons/iconmaterial.mat");
                                CUIClass.CreateText(ref _createNewMenu, "panelType_text", "field_div9", "1 1 1 0.3", "IMAGE URL:", 11, "0.03 0", "0.95 0.9", TextAnchor.MiddleLeft, $"robotocondensed-bold.ttf", "0 0 0 0.7", $"0.0 0.0");
                                CUIClass.CreateInput(ref _createNewMenu, "_ammountInput", "field_div9", "1 1 1 1", 14, "0.26 0.0", "1 0.90", "robotocondensed-bold.ttf", $"storemenuvalue imageUrl", TextAnchor.MiddleLeft);
                        //ICON ANCHOR MIN
                            CUIClass.CreatePanel(ref _createNewMenu, "field_div9", "main_div", "0.11 0.11 0.11 0.95", "0.545 0.645", $"0.75 0.705", false, 0f, "assets/icons/iconmaterial.mat");
                                CUIClass.CreateText(ref _createNewMenu, "panelType_text", "field_div9", "1 1 1 0.3", " ANCHOR MIN:", 11, "0.03 0", "0.95 0.9", TextAnchor.MiddleLeft, $"robotocondensed-bold.ttf", "0 0 0 0.7", $"0.0 0.0");
                                CUIClass.CreateInput(ref _createNewMenu, "_ammountInput", "field_div9", "1 1 1 1", 11, "0.55 0.0", "1 0.95", "robotocondensed-bold.ttf", $"storemenuvalue imgAnchorMin", TextAnchor.MiddleLeft);
                        //ICON ANCHOR MAX
                            CUIClass.CreatePanel(ref _createNewMenu, "field_div9", "main_div", "0.11 0.11 0.11 0.95", "0.545 0.57", $"0.75 0.63", false, 0f, "assets/icons/iconmaterial.mat");
                                CUIClass.CreateText(ref _createNewMenu, "panelType_text", "field_div9", "1 1 1 0.3", " ANCHOR MAX:", 11, "0.03 0", "0.95 0.9", TextAnchor.MiddleLeft, $"robotocondensed-bold.ttf", "0 0 0 0.7", $"0.0 0.0");
                                CUIClass.CreateInput(ref _createNewMenu, "_ammountInput", "field_div9", "1 1 1 1", 11, "0.55 0.0", "1 0.95", "robotocondensed-bold.ttf", $"storemenuvalue imgAnchorMax", TextAnchor.MiddleLeft);
                        //OPEN TRUE FALSE
                        CUIClass.CreatePanel(ref _createNewMenu, "field_div10", "main_div", "0.11 0.11 0.11 0.95", "0.545 0.45", $"0.75 0.555", false, 0f, "assets/icons/iconmaterial.mat");
                            CUIClass.CreateText(ref _createNewMenu, "uiType_text", "field_div10", "1 1 1 0.3", "OPEN/CLOSE FUNCTION", 11, "0.05 0", "0.95 0.9", TextAnchor.UpperLeft, $"robotocondensed-bold.ttf", "0 0 0 0.7", $"0.0 0.0");
                        //function description
                        CUIClass.CreateText(ref _createNewMenu, "panelType_text", "main_div", "1 1 1 0.3", "'Open/Close Function' When set to 'Enabled', it allows the user to toggle the button on or off with a chat command.", 11, "0.555 0.3", $"0.94 0.425", TextAnchor.UpperCenter, $"robotocondensed-bold.ttf", "0 0 0 0.7", $"0.0 0.0");
                        //buttons create or discard

                        CUIClass.CreateButton(ref _createNewMenu, "btn_discord", "main_div", "0.38 0.51 0.16 0.85", $"CREATE BUTTON", 17, "0.585 0.23", $"0.91 0.32", $"custombuttons_save", "", "1 1 1 0.7", 0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf");
                        CUIClass.CreateButton(ref _createNewMenu, "btn_discord", "main_div", "0.56 0.20 0.15 0.85", $"DISCARD SETTINGS", 13, "0.595 0.145", $"0.90 0.21", $"custombuttons_open discard", "", "1 1 1 0.7", 0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf");

                    CUIClass.CreatePanel(ref _createNewMenu, "splitter_div", "main_div", "1 1 1 0.4", "0.4995 0.20", "0.50 0.75", false, 0f, "assets/icons/iconmaterial.mat");
                    DestroyMainUI(player);
                    CuiHelper.AddUi(player, _createNewMenu); 
                   
                    UiTypeButtons(player);
                    PanelTypeButtons(player);
        
                    ImagePreview(player);
                    OpenCloseOption(player);
                }

                private void DestroyButtonMenuUI(BasePlayer player)
                {
                    CuiHelper.DestroyUi(player, "NewMenu"); 
                }
            #endregion
            #region Enter Name Menu
            
                private void EnterName(BasePlayer player)
                {   
                    string text = "You can find all panel names in the data file or by using the chat command <b>'/custombuttons_list'</b>.";
                    var _enterName = CUIClass.CreateOverlay("entername_main", "0 0 0 0.5", "0 0", "1 1", true, 0.0f, $"assets/content/ui/uibackgroundblur.mat");
                    CUIClass.CreateButton(ref _enterName, "editmenu_closebg", "entername_main", "0 0 0 0", "", 13, "0 0", "1 1", "custombuttons_open mainmenu", "", "1 1 1 0.7", 0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf");
                   
                    CUIClass.CreatePanel(ref _enterName, "entername_div", "entername_main", "0.25 0.23 0.22 0.95", "0.4 0.45", "0.60 0.65", false, 0f, "assets/content/ui/uibackgroundblur.mat");
                    CUIClass.CreatePanel(ref _enterName, "input_div", "entername_div", "0.11 0.11 0.11 0.95", "0.05 0.4", "0.95 0.65", false, 0f, "assets/content/ui/uibackgroundblur.mat");
                    CUIClass.CreateText(ref _enterName, "input_title", "input_div", "1 1 1 0.4", "NAME:", 12, "0.05 0", "0.45 1", TextAnchor.MiddleLeft, $"robotocondensed-bold.ttf", "0 0 0 0.7", $"0.0 0.0");
                    CUIClass.CreateText(ref _enterName, "entername_title", "entername_div", "1 1 1 1", "ENTER PANEL NAME", 20, "0.05 0.7", "0.95 0.9", TextAnchor.UpperCenter, $"robotocondensed-bold.ttf", "0 0 0 0.7", $"0.0 0.0");
                    CUIClass.CreateText(ref _enterName, "entername_info", "entername_div", "1 1 1 1", text, 9, "0.07 0.10", "0.58 0.35", TextAnchor.UpperLeft, $"robotocondensed-regular.ttf", "0 0 0 0.7", $"0.0 0.0");

                    CUIClass.CreateInput(ref _enterName, "entername_input", "input_div", "1 1 1 1", 17, "0.20 0.05", "1 0.95", "robotocondensed-bold.ttf", $"storemenuvalue panelNameEdit", TextAnchor.MiddleLeft);
                    //CUIClass.CreatePanel(ref _createNewMenu, "title_div", "main_div2", "0.11 0.11 0.11 0.95", "-0.005 0.92", "1.002 1", false, 0f, "assets/content/ui/uibackgroundblur.mat");
                    //CUIClass.CreateImage(ref _imagePreview, "field_div11", "", $"0.05 0.03", $"0.95 0.85");
                    
                    //CUIClass.CreateButton(ref _ppWindow, "_ppWindow_btn", "_ppWindow", "0.22 0.22 0.22 1.0", $"{message}", 13, anchorMin, anchorMax, "custombuttons_open closepp", "", "1 1 1 0.7", 0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf");
                    DestroyEnterName(player);
                    CuiHelper.AddUi(player, _enterName);
                    EditBtn(player, false, "");
                }

                private void DestroyEnterName(BasePlayer player)
                {
                    CuiHelper.DestroyUi(player, "entername_main"); 
                    DestroyEditBtn(player); 
                }
                
                private void EditBtn(BasePlayer player, bool active, string panelName)
                {   
                    string btn_color = "0.23 0.23 0.23 0.55";
                    string btn_command = "";
                    if (active) {btn_color = "0.38 0.51 0.16 0.85"; btn_command = $"custombuttons_edit {panelName}";}
                    var _editBtn = CUIClass.CreateOverlay("empty", "0 0 0 0.0", "0 0", "0 0", true, 0.0f, $"assets/content/ui/uibackgroundblur.mat");
                    CUIClass.CreateButton(ref _editBtn, "editmenu_btn", "entername_div", btn_color, $"EDIT", 13, "0.65 0.10", $"0.95 0.35", btn_command, "", "1 1 1 0.7", 0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf");
                    DestroyEditBtn(player);
                    CuiHelper.AddUi(player, _editBtn);
                }

                private void DestroyEditBtn(BasePlayer player)
                {
                    CuiHelper.DestroyUi(player, "empty"); 
                    CuiHelper.DestroyUi(player, "editmenu_btn"); 
                }
                
            #endregion
            #region Edit Panel Menu
                #region Main
                    private void EditMenu(BasePlayer player, string panelName)
                    {   

                        var _editMenu = CUIClass.CreateOverlay("editmenu_main", "0 0 0 0.5", "0 0", "1 1", true, 0.0f, $"assets/content/ui/uibackgroundblur.mat");
                        //BASE
                        CUIClass.CreatePanel(ref _editMenu, "editmenu_div", "editmenu_main", "0.25 0.23 0.22 0.95", "0.35 0.35", "0.65 0.7", false, 0f, "assets/content/ui/uibackgroundblur.mat");
                        CUIClass.CreatePanel(ref _editMenu, "editmenu_title_div", "editmenu_main", "0.11 0.11 0.11 0.95", "0.3491 0.70", "0.6509 0.74", false, 0f, "assets/content/ui/uibackgroundblur.mat");
                        CUIClass.CreateText(ref _editMenu, "editmenu_title_text", "editmenu_title_div", "1 1 1 0.4", "Editor", 12, "0.02 0", "0.9 0.95", TextAnchor.MiddleLeft, $"robotocondensed-bold.ttf", "0 0 0 0.7", $"0.0 0.0");
                        CUIClass.CreateButton(ref _editMenu, "editmenu_close_btn", "editmenu_title_div", "0.56 0.20 0.15 0.65", $"✕", 11, "0.945 0.2", $"0.99 0.80", $"custombuttons_edit backtostart", "", "1 1 1 0.7", 0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf");
                        //TITLE
                        CUIClass.CreateText(ref _editMenu, "editmenu_div_text", "editmenu_div", "1 1 1 0.4", $"EDITING <b><size=20>'<color=#FF5733>{panelName}</color>'</size></b>", 12, "0.09 0.85", "0.9 0.95", TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf", "0 0 0 0.7", $"0.0 0.0");
                        //MENU BUTTONS
                        CUIClass.CreateButton(ref _editMenu, "editmenu_color_btn", "editmenu_div", "0.55 0.55 0.55 0.3", $"<size=20><b>Color & Text</b></size> \nPanel Color and Text", 11, "0.05 0.45", $"0.48 0.75", $"custombuttons_editmenu color {panelName}", "", "1 1 1 0.7", 0f, TextAnchor.MiddleCenter, $"robotocondensed-regular.ttf");
                        CUIClass.CreateButton(ref _editMenu, "editmenu_pos_btn", "editmenu_div", "0.55 0.55 0.55 0.3", $"<size=20><b>Panel Position</b></size> \nAnchor Settings", 11, "0.52 0.45", $"0.95 0.75", $"custombuttons_editmenu anchor {panelName}", "", "1 1 1 0.7", 0f, TextAnchor.MiddleCenter, $"robotocondensed-regular.ttf");  
                        CUIClass.CreateButton(ref _editMenu, "editmenu_img_btn", "editmenu_div", "0.55 0.55 0.55 0.3", $"<size=20><b>Image Settings</b></size> \nPosition and Image link", 11, "0.05 0.1", $"0.48 0.4", $"custombuttons_editmenu image {panelName}", "", "1 1 1 0.7", 0f, TextAnchor.MiddleCenter, $"robotocondensed-regular.ttf");
                        CUIClass.CreateButton(ref _editMenu, "editmenu_cui_btn", "editmenu_div", "0.55 0.55 0.55 0.3", $"<size=20><b>Cui Settings</b></size> \n CUI Type and Command", 11, "0.52 0.1", $"0.95 0.4", $"custombuttons_editmenu cui {panelName}", "", "1 1 1 0.7", 0f, TextAnchor.MiddleCenter, $"robotocondensed-regular.ttf");  
                        //END
                        DestroyEditMenu(player);
                        //destroy sub sections
                        DestroyAllSubs(player); 
                        //
                        CuiHelper.AddUi(player, _editMenu);
                               
                        
                        //EditText(player);
                        //EditImage(player);
                        //EditType(player);
                    }

                    private void DestroyEditMenu(BasePlayer player)
                    {
                        CuiHelper.DestroyUi(player, "editmenu_main"); 
                        
                    }

                    private void DestroyEditMenuCont(BasePlayer player)
                    {
                        
                        CuiHelper.DestroyUi(player, "editmenu_div_text"); 
                        CuiHelper.DestroyUi(player, "editmenu_color_btn"); 
                        CuiHelper.DestroyUi(player, "editmenu_pos_btn"); 
                        CuiHelper.DestroyUi(player, "editmenu_img_btn"); 
                        CuiHelper.DestroyUi(player, "editmenu_cui_btn"); 
                        
                    }

                    private void DestroyAllSubs(BasePlayer player)
                    {
                        
                        DestroyEditMenuCont(player);
                        DestroyEditMenu(player);
                        DestroyEditText(player);
                        DestroyEditColor(player);
                        DestroyEditPosMain(player);
                        DestroyEditImagePreview(player);
                        
                    }
                #endregion
                #region Text and Color
                    private void EditText(BasePlayer player, string panelName)
                    {   
                        string text = "";
                        if (_cuiData[panelName].text != null) text = _cuiData[panelName].text;
                        var _editText = CUIClass.CreateOverlay("empty", "0 0 0 0.0", "0 0", "0 0", true, 0.0f, $"assets/content/ui/uibackgroundblur.mat");
                        //CURRENT TEXT
                        CUIClass.CreatePanel(ref _editText, "editText_currentPanel", "editmenu_div", "0.11 0.11 0.11 0.75", "0.05 0.75", $"0.95 0.95", false, 0f, "assets/icons/iconmaterial.mat");
                        CUIClass.CreateText(ref _editText, "editText_currentText", "editText_currentPanel", "1 1 1 0.3", $"CURRENT TEXT:  <size=13>{text}</size>", 11, "0.05 0", "0.95 0.95", TextAnchor.MiddleLeft, $"robotocondensed-bold.ttf", "0 0 0 0.7", $"0.0 0.0");
                        //NEW TEXT
                        CUIClass.CreatePanel(ref _editText, "editText_panelNew", "editmenu_div", "0.11 0.11 0.11 0.95", "0.05 0.60", $"0.95 0.73", false, 0f, "assets/icons/iconmaterial.mat");
                            CUIClass.CreateText(ref _editText, "panelType_text", "editText_panelNew", "1 1 1 0.3", "NEW TEXT:", 11, "0.05 0", "0.95 1", TextAnchor.MiddleLeft, $"robotocondensed-bold.ttf", "0 0 0 0.7", $"0.0 0.0");
                            CUIClass.CreateInput(ref _editText, "_ammountInput", "editText_panelNew", "1 1 1 1", 14, "0.20 0", "1 1", "robotocondensed-bold.ttf", $"storemenuvalue text", TextAnchor.MiddleLeft);
                        //NEW COLOR 
                        CUIClass.CreatePanel(ref _editText, "editColor_newPanel", "editmenu_div", "0.11 0.11 0.11 0.95", "0.05 0.30", $"0.95 0.43", false, 0f, "assets/icons/iconmaterial.mat");
                            CUIClass.CreateText(ref _editText, "new_color", "editColor_newPanel", "1 1 1 0.3", "NEW COLOR:", 11, "0.05 0", "0.95 1", TextAnchor.MiddleLeft, $"robotocondensed-bold.ttf", "0 0 0 0.7", $"0.0 0.0");
                            CUIClass.CreateInput(ref _editText, "new_color_input", "editColor_newPanel", "1 1 1 1", 14, "0.3 0", "1 1", "robotocondensed-bold.ttf", $"storemenuvalue panelColorEdit {panelName}", TextAnchor.MiddleLeft);
                        //BUTTONS
                        CUIClass.CreateButton(ref _editText, "editText_saveBtn", "editmenu_div", "0.38 0.51 0.16 0.85", $"SAVE CHANGES", 13, "0.52 0.05","0.95 0.15", $"custombuttons_storechanges textcolor {panelName}", "", "1 1 1 0.7", 0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf");
                        CUIClass.CreateButton(ref _editText, "editText_backBtn", "editmenu_div", "0.56 0.20 0.15 0.85", $"DISCARD CHANGES", 13, "0.05 0.05","0.48 0.15", $"custombuttons_discardchanges {panelName}", "", "1 1 1 0.7", 0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf");
                        DestroyEditMenuCont(player);
                        DestroyEditText(player);
                        CuiHelper.AddUi(player, _editText); 
                        EditColor(player, panelName);            
                    }


                    private void DestroyEditText(BasePlayer player)
                    {   
                        CuiHelper.DestroyUi(player, "empty"); 
                        CuiHelper.DestroyUi(player, "editText_currentPanel"); 
                        CuiHelper.DestroyUi(player, "editText_panelNew"); 
                        CuiHelper.DestroyUi(player, "editText_saveBtn"); 
                        CuiHelper.DestroyUi(player, "editText_backBtn");
                    }

                    
                    private void EditColor(BasePlayer player, string panelName)
                    {   
                        var _editColor = CUIClass.CreateOverlay("empty", "0 0 0 0.0", "0 0", "0 0", true, 0.0f, $"assets/content/ui/uibackgroundblur.mat");
                        string cColor = "0.11 0.11 0.11 0.95";
                        string nColor = "0.11 0.11 0.11 0.95";
                        if (_cuiData[panelName].panelColor != null) cColor = _cuiData[panelName].panelColor;
                        if (storedCuiFields.ContainsKey("panelColor")) nColor = storedCuiFields["panelColor"];
                        //CURRENT COLOR
                        CUIClass.CreatePanel(ref _editColor, "editColor_currentPanel", "editmenu_div", "0.11 0.11 0.11 0.75", "0.05 0.45", $"0.95 0.58", false, 0f, "assets/icons/iconmaterial.mat");
                            CUIClass.CreateText(ref _editColor, "editColor_currentText", "editColor_currentPanel", "1 1 1 0.3", $"CURRENT COLOR:   <size=13>{cColor}</size>", 11, "0.05 0", "0.95 0.95", TextAnchor.MiddleLeft, $"robotocondensed-bold.ttf", "0 0 0 0.7", $"0.0 0.0");
                        //color display
                        CUIClass.CreatePanel(ref _editColor, "newcolorpreview", "editColor_newPanel", nColor, "0.9 0.15", $"0.97 0.85", false, 0f, "assets/icons/iconmaterial.mat");
                            CUIClass.CreatePanel(ref _editColor, "currentcolorpreview", "editColor_currentPanel", cColor, "0.9 0.15", $"0.97 0.85", false, 0f, "assets/icons/iconmaterial.mat");

                        DestroyEditColor(player);
                        CuiHelper.AddUi(player, _editColor);
                        
                        
                    }
                    
                    private void DestroyEditColor(BasePlayer player)
                    {   
                        CuiHelper.DestroyUi(player, "empty"); 
                        CuiHelper.DestroyUi(player, "editColor_currentPanel"); 
                        CuiHelper.DestroyUi(player, "editColor_currentText"); 
                        CuiHelper.DestroyUi(player, "editColor_colorPa"); 
                    }
                #endregion
                #region Position
                    private Dictionary<string, float> storedPosFields = new Dictionary<string, float>();

                    private string GetSingleAnchor(string anchor, int index)
                    {
                        if (anchor == null) return null;
                        string[] splitter = anchor.Split(' ');
                        return splitter[index];
                    }
                    
                    private void StoreAnchors(string anchor, string valueString)
                    {   
                        if (valueString == null) return;
                        float value = Convert.ToSingle(valueString);
                        if (!storedPosFields.ContainsKey(anchor))
                        {
                            storedPosFields.Add(anchor, value);
                            _debug($"{anchor} Not found, adding to dictionary");
                            return;
                        } else {storedPosFields[anchor] = value; return;}
                    }

                    private float ChangeAnchor(string op, string savedAnchor, string incValue)
                    {
                        float currentValue = Convert.ToSingle(savedAnchor);
                        float incrementValue = Convert.ToSingle(incValue);
                        if(op == "+") return currentValue + incrementValue;
                        if(op == "-") return currentValue - incrementValue;
                        return currentValue;
                    }   

                    private string SaveAnchor(string value1, string value2)
                    {
                        return $"{value1} {value2}";
                    }

                    [ConsoleCommand("pos_editing")]
                    private void pos_editing(ConsoleSystem.Arg arg)
                    {
                        var player = arg?.Player();
                        var args = arg.Args;
                        if (arg.Player() == null) return;
    
                        /*
                        0 panel name
                        1 value type 
                        2 operator type
                        3 inc value
                        */
                    
                        if(args[1] == "posLeft") 
                        {   
                            string left = GetSingleAnchor(_cuiData[$"{args[0]}"].OffsetMin, 0);
                            string newLeft = Convert.ToString(ChangeAnchor(args[2], left, args[3]));
                            _cuiData[$"{args[0]}"].OffsetMin = SaveAnchor(newLeft, GetSingleAnchor(_cuiData[$"{args[0]}"].OffsetMin, 1));
                            SaveData();
                            CreateCui(player); 
                            PosValues(player, args[0]);
                            //EditPosMain(player, args[0]);
                        } 
                        if(args[1] == "posBottom") 
                        {   
                            string bottom = GetSingleAnchor(_cuiData[$"{args[0]}"].OffsetMin, 1);
                            string newBottom = Convert.ToString(ChangeAnchor(args[2], bottom, args[3]));
                            _cuiData[$"{args[0]}"].OffsetMin = SaveAnchor(GetSingleAnchor(_cuiData[$"{args[0]}"].OffsetMin, 0), newBottom);
                            SaveData();
                            CreateCui(player); 
                            PosValues(player, args[0]);
                           // EditPosMain(player, args[0]);
                        } 
                        if(args[1] == "posRight") 
                        {   
                            string right = GetSingleAnchor(_cuiData[$"{args[0]}"].OffsetMax, 0);
                            string newRight = Convert.ToString(ChangeAnchor(args[2], right, args[3]));
                            _cuiData[$"{args[0]}"].OffsetMax = SaveAnchor(newRight, GetSingleAnchor(_cuiData[$"{args[0]}"].OffsetMax, 1));
                            SaveData();
                            CreateCui(player); 
                            PosValues(player, args[0]);
                           // EditPosMain(player, args[0]);
                        } 
                        if(args[1] == "posTop") 
                        {   
                            string top = GetSingleAnchor(_cuiData[$"{args[0]}"].OffsetMax, 1);
                            string newTop = Convert.ToString(ChangeAnchor(args[2], top, args[3]));
                            _cuiData[$"{args[0]}"].OffsetMax = SaveAnchor(GetSingleAnchor(_cuiData[$"{args[0]}"].OffsetMax, 0), newTop);
                            SaveData();
                            CreateCui(player); 
                            PosValues(player, args[0]);
                            //EditPosMain(player, args[0]);
                        } 
                                                
                    }

                    private string moveValue = "10";

                    [ConsoleCommand("custombuttons_movevalue")]
                    private void custombuttons_movevalue(ConsoleSystem.Arg arg)
                    {
                        var player = arg?.Player();
                        var args = arg.Args;
                        if (arg.Player() == null) return;
                        if (args.Length < 2) return;
                        moveValue = args[1];
                        EditPosMain(player, args[0]);

                    }

                    private void EditPosMain(BasePlayer player, string panelName)
                    {   
                        StoreAnchors("posLeft", GetSingleAnchor(_cuiData[panelName].OffsetMin, 0));
                        StoreAnchors("posBottom", GetSingleAnchor(_cuiData[panelName].OffsetMin, 1));
                        StoreAnchors("posRight", GetSingleAnchor(_cuiData[panelName].OffsetMax, 0));
                        StoreAnchors("posTop", GetSingleAnchor(_cuiData[panelName].OffsetMax, 1));
                        string pos_left = GetSingleAnchor(_cuiData[panelName].OffsetMin, 0);
                        string pos_bottom = GetSingleAnchor(_cuiData[panelName].OffsetMin, 1);
                        string pos_right = GetSingleAnchor(_cuiData[panelName].OffsetMax, 0);
                        string pos_top = GetSingleAnchor(_cuiData[panelName].OffsetMax, 1);
                        
                        var _editPos = CUIClass.CreateOverlay("editPos_main", "0 0 0 0.0", "0 0", "1 1", true, 0.0f, $"assets/content/ui/uibackgroundblur.mat");
                        CUIClass.CreatePanel(ref _editPos, "editPos_div", "editPos_main", "0.25 0.23 0.22 0.65", "0.35 0.35", $"0.65 0.65", false, 0f, "assets/content/ui/uibackgroundblur.mat");
                        //AnchorMin Title
                        CUIClass.CreateText(ref _editPos, "anch_min_title", "editPos_div", "1 1 1 0.3", $"OFFSET MIN", 20, "0.05 0.65", "0.45 0.85", TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf", "0 0 0 0.7", $"0.0 0.0");
                        //SQ1
                        CUIClass.CreatePanel(ref _editPos, "editPos_sq1", "editPos_div", "0.11 0.11 0.11 0.95", "0.05 0.35", $"0.245 0.55", false, 0f, "assets/content/ui/uibackgroundblur.mat");
                        //SQ2
                        CUIClass.CreatePanel(ref _editPos, "editPos_sq2", "editPos_div", "0.11 0.11 0.11 0.95", "0.255 0.35", $"0.455 0.55", false, 0f, "assets/content/ui/uibackgroundblur.mat");
                        //AnchorMax Title
                        CUIClass.CreateText(ref _editPos, "anch_min_title", "editPos_div", "1 1 1 0.3", $"OFFSET MAX", 20, "0.55 0.65", "0.95 0.85", TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf", "0 0 0 0.7", $"0.0 0.0");
                        //SQ3
                        CUIClass.CreatePanel(ref _editPos, "editPos_sq3", "editPos_div", "0.11 0.11 0.11 0.95", "0.55 0.35", $"0.745 0.55", false, 0f, "assets/content/ui/uibackgroundblur.mat");
                        //SQ4
                        CUIClass.CreatePanel(ref _editPos, "editPos_sq4", "editPos_div", "0.11 0.11 0.11 0.95", "0.755 0.35", $"0.955 0.55", false, 0f, "assets/content/ui/uibackgroundblur.mat");
                        //MOVING VALUE
                        CUIClass.CreatePanel(ref _editPos, "editPos_moveby_div", "editPos_div", "0.11 0.11 0.11 0.75", "0.05 0.05", $"0.455 0.18", false, 0f, "assets/icons/iconmaterial.mat");
                        CUIClass.CreateText(ref _editPos, "editPos_input_text", "editPos_moveby_div", "1 1 1 0.3", $"MOVING VALUE:   {moveValue}", 11, "0.05 0", "0.95 1", TextAnchor.MiddleLeft, $"robotocondensed-bold.ttf", "0 0 0 0.7", $"0.0 0.0");
                        //END BUTTOn
                        CUIClass.CreateButton(ref _editPos, "editPos_discard", "editPos_div", "0.11 0.11 0.11 0.85", $"BACK TO EDIT MENU", 13, "0.55 0.05", $"0.955 0.18", $"custombuttons_discardchanges {panelName}", "", "1 1 1 0.7", 0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf");
                        
                        DestroyEditPosMain(player);
                        DestroyEditMenuCont(player);
                        CuiHelper.AddUi(player, _editPos); 
                        PosValues(player, panelName); 
                    }

                    private void PosValues(BasePlayer player, string panelName)
                    {   
                        string pos_left = GetSingleAnchor(_cuiData[panelName].OffsetMin, 0);
                        string pos_bottom = GetSingleAnchor(_cuiData[panelName].OffsetMin, 1);
                        string pos_right = GetSingleAnchor(_cuiData[panelName].OffsetMax, 0);
                        string pos_top = GetSingleAnchor(_cuiData[panelName].OffsetMax, 1);
                        
                        var _posValues = CUIClass.CreateOverlay("empty", "0 0 0 0.0", "0 0", "0 0", true, 0.0f, $"assets/content/ui/uibackgroundblur.mat");
                        //CUIClass.CreatePanel(ref _posValues, "button1", "Overlay", "0.38 0.51 0.16 0.85", _cuiData[panelName].OffsetMin, _cuiData[panelName].OffsetMax, false, 0f, "assets/icons/iconmaterial.mat");
                        //SQ1
                        CUIClass.CreateText(ref _posValues, "sq1_value", "editPos_sq1", "1 1 1 0.3", $"LEFT POINT\n<size=13><color=#FFFFFF><b>{pos_left}</b></color></size>", 10, "0.05 0", "0.95 1", TextAnchor.MiddleCenter, $"robotocondensed-regular.ttf", "0 0 0 0.7", $"0.0 0.0");
                        CUIClass.CreateButton(ref _posValues, "sq1_sq_+", "editPos_div", "0.11 0.11 0.11 0.75", $"+", 13, "0.05 0.56", $"0.245 0.65", $"pos_editing {panelName} posLeft + {moveValue}", "", "1 1 1 0.7", 0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf");
                        CUIClass.CreateButton(ref _posValues, "sq1_sq_-", "editPos_div", "0.11 0.11 0.11 0.75", $"-", 13, "0.05 0.24", $"0.245 0.34", $"pos_editing {panelName} posLeft - {moveValue}", "", "1 1 1 0.7", 0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf");  
                        //SQ2
                        CUIClass.CreateText(ref _posValues, "sq2_value", "editPos_sq2", "1 1 1 0.3", $"BOTTOM POINT\n<size=13><color=#FFFFFF><b>{pos_bottom}</b></color></size>", 10, "0.05 0", "0.95 1", TextAnchor.MiddleCenter, $"robotocondensed-regular.ttf", "0 0 0 0.7", $"0.0 0.0");
                        CUIClass.CreateButton(ref _posValues, "sq2_sq_+", "editPos_div", "0.11 0.11 0.11 0.75", $"+", 13, "0.255 0.56", $"0.455 0.65", $"pos_editing {panelName} posBottom + {moveValue}", "", "1 1 1 0.7", 0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf");
                        CUIClass.CreateButton(ref _posValues, "sq2_sq_-", "editPos_div", "0.11 0.11 0.11 0.75", $"-", 13, "0.255 0.24", $"0.455 0.34", $"pos_editing {panelName} posBottom - {moveValue}", "", "1 1 1 0.7", 0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf");
                        //SQ3
                        CUIClass.CreateText(ref _posValues, "sq3_value", "editPos_sq3", "1 1 1 0.3", $"RIGHT POINT\n<size=13><color=#FFFFFF><b>{pos_right}</b></color></size>", 10, "0.05 0", "0.95 1", TextAnchor.MiddleCenter, $"robotocondensed-regular.ttf", "0 0 0 0.7", $"0.0 0.0");
                        CUIClass.CreateButton(ref _posValues, "sq3_sq_+", "editPos_div", "0.11 0.11 0.11 0.75", $"+", 13, "0.55 0.56", $"0.745 0.65", $"pos_editing {panelName} posRight + {moveValue}", "", "1 1 1 0.7", 0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf");
                        CUIClass.CreateButton(ref _posValues, "sq3_sq_-", "editPos_div", "0.11 0.11 0.11 0.75", $"-", 13, "0.55 0.24", $"0.745 0.34", $"pos_editing {panelName} posRight - {moveValue}", "", "1 1 1 0.7", 0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf");
                        //SQ4
                        CUIClass.CreateText(ref _posValues, "sq4_value", "editPos_sq4", "1 1 1 0.3", $"TOP POINT\n<size=13><color=#FFFFFF><b>{pos_top}</b></color></size>", 10, "0.05 0", "0.95 1", TextAnchor.MiddleCenter, $"robotocondensed-regular.ttf", "0 0 0 0.7", $"0.0 0.0");
                        CUIClass.CreateButton(ref _posValues, "sq4_sq_+", "editPos_div", "0.11 0.11 0.11 0.75", $"+", 13, "0.755 0.56", $"0.955 0.65", $"pos_editing {panelName} posTop + {moveValue}", "", "1 1 1 0.7", 0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf");
                        CUIClass.CreateButton(ref _posValues, "sq4_sq_-", "editPos_div", "0.11 0.11 0.11 0.75", $"-", 13, "0.755 0.24", $"0.955 0.34", $"pos_editing {panelName} posTop - {moveValue}", "", "1 1 1 0.7", 0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf");
                        //INPUT
                        CUIClass.CreateInput(ref _posValues, "_editPosInput", "editPos_moveby_div", "1 1 1 1", 13, "0.55 0", "1 1", "robotocondensed-bold.ttf", $"custombuttons_movevalue {panelName}", TextAnchor.MiddleLeft);
                        
                        DestroyPosValues(player); 
                        CuiHelper.AddUi(player, _posValues); 

                    }

                    private void DestroyPosValues(BasePlayer player)
                    {  
                        CuiHelper.DestroyUi(player, "empty"); 
                        CuiHelper.DestroyUi(player, "sq1_value"); 
                        CuiHelper.DestroyUi(player, "sq2_value"); 
                        CuiHelper.DestroyUi(player, "sq3_value"); 
                        CuiHelper.DestroyUi(player, "sq4_value"); 
                        CuiHelper.DestroyUi(player, "sq1_sq_+"); 
                        CuiHelper.DestroyUi(player, "sq1_sq_-"); 
                        CuiHelper.DestroyUi(player, "sq2_sq_+"); 
                        CuiHelper.DestroyUi(player, "sq2_sq_-"); 
                        CuiHelper.DestroyUi(player, "sq3_sq_+"); 
                        CuiHelper.DestroyUi(player, "sq3_sq_-"); 
                        CuiHelper.DestroyUi(player, "sq4_sq_+"); 
                        CuiHelper.DestroyUi(player, "sq4_sq_-"); 
                        CuiHelper.DestroyUi(player, "_editPosInput"); 
                        CuiHelper.DestroyUi(player, "button1"); 
                    }


                    private void DestroyEditPosMain(BasePlayer player)
                    {   
                        CuiHelper.DestroyUi(player, "editPos_main"); 
                        DestroyPosValues(player); 
                    }

                #endregion
                #region Image
                    private void EditImage(BasePlayer player, string panelName)
                    {   
                        var _editImage = CUIClass.CreateOverlay("empty", "0 0 0 0.5", "0 0", "0 0", true, 0.0f, $"assets/content/ui/uibackgroundblur.mat");
                        string text = "The image preview is not how the image is going to appear on the actual button. The image will be stretched by default unless you specify anchors. If you are not familiar with anchoring parented elements I advise you to upload an image with the same height/width ratio of the button. ";
                        //ICON URL
                            CUIClass.CreatePanel(ref _editImage, "editImage_url_div", "editmenu_div", "0.11 0.11 0.11 0.95", "0.05 0.82", $"0.95 0.95", false, 0f, "assets/icons/iconmaterial.mat");
                                CUIClass.CreateText(ref _editImage, "editImage_text", "editImage_url_div", "1 1 1 0.3", "IMAGE URL:", 11, "0.03 0", "0.95 0.9", TextAnchor.MiddleLeft, $"robotocondensed-bold.ttf", "0 0 0 0.7", $"0.0 0.0");
                                CUIClass.CreateInput(ref _editImage, "editImage_Input", "editImage_url_div", "1 1 1 1", 11, "0.20 0.0", "1 0.90", "robotocondensed-bold.ttf", $"storemenuvalue imageUrlEdit {panelName}", TextAnchor.MiddleLeft);
                        //ICON ANCHOR MIN
                            CUIClass.CreatePanel(ref _editImage, "editImage_min_div", "editmenu_div", "0.11 0.11 0.11 0.95", "0.05 0.67", $"0.55 0.8", false, 0f, "assets/icons/iconmaterial.mat");
                                CUIClass.CreateText(ref _editImage, "editImage_text", "editImage_min_div", "1 1 1 0.3", " ANCHOR MIN:", 11, "0.03 0", "0.95 0.9", TextAnchor.MiddleLeft, $"robotocondensed-bold.ttf", "0 0 0 0.7", $"0.0 0.0");
                                CUIClass.CreateInput(ref _editImage, "editImage_Input", "editImage_min_div", "1 1 1 1", 11, "0.4 0.0", "1 0.95", "robotocondensed-bold.ttf", $"storemenuvalue imgAnchorMin", TextAnchor.MiddleLeft);
                        //ICON ANCHOR MAX
                            CUIClass.CreatePanel(ref _editImage, "editImage_max_div", "editmenu_div", "0.11 0.11 0.11 0.95", "0.05 0.52", $"0.55 0.65", false, 0f, "assets/icons/iconmaterial.mat");
                                CUIClass.CreateText(ref _editImage, "editImage_text", "editImage_max_div", "1 1 1 0.3", " ANCHOR MAX:", 11, "0.03 0", "0.95 0.9", TextAnchor.MiddleLeft, $"robotocondensed-bold.ttf", "0 0 0 0.7", $"0.0 0.0");
                                CUIClass.CreateInput(ref _editImage, "editImage_Input", "editImage_max_div", "1 1 1 1", 11, "0.4 0.0", "1 0.95", "robotocondensed-bold.ttf", $"storemenuvalue imgAnchorMax", TextAnchor.MiddleLeft);
                        //exp text
                        CUIClass.CreateText(ref _editImage, "editmenu_exp_text", "editmenu_div", "1 1 1 0.4", text, 10, "0.05 0", "0.55 0.50", TextAnchor.UpperLeft, $"robotocondensed-regular.ttf", "0 0 0 0.7", $"0.0 0.0");
                        // save / discard
                        CUIClass.CreateButton(ref _editImage, "editText_saveBtn", "editmenu_div", "0.38 0.51 0.16 0.85", $"SAVE CHANGES", 13, "0.52 0.05","0.95 0.15", $"custombuttons_storechanges image {panelName}", "", "1 1 1 0.7", 0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf");
                        CUIClass.CreateButton(ref _editImage, "editText_backBtn", "editmenu_div", "0.56 0.20 0.15 0.85", $"DISCARD CHANGES", 13, "0.05 0.05","0.48 0.15", $"custombuttons_discardchanges {panelName}", "", "1 1 1 0.7", 0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf");
                       
                        DestroyEditImage(player);
                        DestroyEditMenuCont(player);
                        CuiHelper.AddUi(player, _editImage); 
                        EditImagePreview(player, panelName);

                    }


                    private void DestroyEditImage(BasePlayer player)
                    {   
                        CuiHelper.DestroyUi(player, "empty"); 
                        CuiHelper.DestroyUi(player, "editText_currentPanel"); 
                        CuiHelper.DestroyUi(player, "editText_panelNew"); 
                        CuiHelper.DestroyUi(player, "editText_saveBtn"); 
                        CuiHelper.DestroyUi(player, "editText_backBtn"); 

                    }

                    private void EditImagePreview(BasePlayer player, string panelName)
                {   
                    string imgUrl = "";
                    if (storedCuiFields.ContainsKey("imageUrl"))
                    {
                        imgUrl = storedCuiFields["imageUrl"];  
                    } else { imgUrl = _cuiData[panelName].imageUrl; }
                    var _imagePreview2 = CUIClass.CreateOverlay("empty", "0 0 0 0.0", "0 0", "0 0", false, 0.0f, $"assets/content/ui/uibackgroundblur.mat");
                    CUIClass.CreatePanel(ref _imagePreview2, "editImage_preview", "editmenu_div", "0.11 0.11 0.11 0.95", "0.56 0.2", $"0.95 0.80", false, 0f, "assets/icons/iconmaterial.mat");
                            CUIClass.CreateText(ref _imagePreview2, "uiType_text", "editImage_preview", "1 1 1 0.3", "IMAGE PREVIEW", 10, "0.05 0", "0.95 0.965", TextAnchor.UpperLeft, $"robotocondensed-bold.ttf", "0 0 0 0.7", $"0.0 0.0");
                            CUIClass.CreateImage(ref _imagePreview2, "editImage_preview", $"{imgUrl}", $"0.05 0.03", $"0.95 0.85");
                    DestroyEditImagePreview(player);
                    CuiHelper.AddUi(player, _imagePreview2);
                    CuiHelper.DestroyUi(player, "empty");   
                }

                private void DestroyEditImagePreview(BasePlayer player)
                {
                    CuiHelper.DestroyUi(player, "editImage_preview"); 
                    CuiHelper.DestroyUi(player, "empty"); 
                }


                #endregion
                #region Type
                    private void EditType(BasePlayer player, string panelName)
                    {   
                        string currCommand = _cuiData[panelName].panelChatCmd;
                        string overlay_color = "0.33 0.33 0.33 0.55";
                        string hud_color = "0.33 0.33 0.33 0.55";
                        string btn_color = "0.33 0.33 0.33 0.55";
                        string panel_color = "0.33 0.33 0.33 0.55";
                        string enable_color = "0.33 0.33 0.33 0.55";
                        string disable_color = "0.33 0.33 0.33 0.55";
                        if (_cuiData[panelName].uiType == "Overlay") overlay_color = "0.16 0.34 0.49 0.55";
                        if (_cuiData[panelName].uiType == "Hud") hud_color = "0.16 0.34 0.49 0.55";
                        if (_cuiData[panelName].panelType == "button") btn_color = "0.16 0.34 0.49 0.55";
                        if (_cuiData[panelName].panelType == "panel") panel_color = "0.16 0.34 0.49 0.55";
                        if (_cuiData[panelName].onCmdHide == "true") enable_color = "0.16 0.34 0.49 0.55";
                        if (_cuiData[panelName].onCmdHide == "false") disable_color = "0.16 0.34 0.49 0.55";
                        var _editType = CUIClass.CreateOverlay("empty", "0 0 0 0.5", "0 0", "0 0", true, 0.0f, $"assets/content/ui/uibackgroundblur.mat");
                        string text = "The image preview is not how the image is going to appear on the actual button. The image will be stretched by default unless you specify anchors. If you are not familiar with anchoring parented elements I advise you to upload an image with the same height/width ratio of the button. ";
                        //uiType
                        CUIClass.CreatePanel(ref _editType, "editType_div1", "editmenu_div", "0.11 0.11 0.11 0.95", "0.05 0.82", $"0.95 0.95", false, 0f, "assets/icons/iconmaterial.mat");
                        CUIClass.CreateText(ref _editType, "editImage_text", "editType_div1", "1 1 1 0.3", "UI TYPE", 11, "0.09 0", "0.95 1", TextAnchor.MiddleLeft, $"robotocondensed-bold.ttf", "0 0 0 0.7", $"0.0 0.0");       
                        CUIClass.CreateButton(ref _editType, "uiType_overlay", "editType_div1", overlay_color, $"OVERLAY", 13, "0.3 0.2", $"0.6 0.8", $"storemenuvalue uiTypeEdit {panelName} Overlay", "", "1 1 1 0.7", 0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf");
                        CUIClass.CreateButton(ref _editType, "uiType_hud", "editType_div1", hud_color, $"HUD", 13, "0.65 0.2", $"0.95 0.8", $"storemenuvalue uiTypeEdit {panelName} Hud", "", "1 1 1 0.7", 0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf");
                        //element type
                        CUIClass.CreatePanel(ref _editType, "editType_div2", "editmenu_div", "0.11 0.11 0.11 0.95", "0.05 0.67", $"0.95 0.80", false, 0f, "assets/icons/iconmaterial.mat");
                        CUIClass.CreateText(ref _editType, "editImage_text", "editType_div2", "1 1 1 0.3", "ELEMENT TYPE", 11, "0.04 0", "0.95 1", TextAnchor.MiddleLeft, $"robotocondensed-bold.ttf", "0 0 0 0.7", $"0.0 0.0");       
                        CUIClass.CreateButton(ref _editType, "panelType_btn", "editType_div2", btn_color, $"BUTTON", 13, "0.3 0.2", $"0.6 0.8", $"storemenuvalue panelTypeEdit {panelName} button", "", "1 1 1 0.7", 0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf");
                        CUIClass.CreateButton(ref _editType, "panelType_panel", "editType_div2", panel_color, $"PANEL", 13, "0.65 0.2", $"0.95 0.8", $"storemenuvalue panelTypeEdit {panelName} panel", "", "1 1 1 0.7", 0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf");
                        //Open close 
                        CUIClass.CreatePanel(ref _editType, "editType_div3", "editmenu_div", "0.11 0.11 0.11 0.95", "0.05 0.52", $"0.95 0.65", false, 0f, "assets/icons/iconmaterial.mat");
                        CUIClass.CreateText(ref _editType, "editImage_text", "editType_div3", "1 1 1 0.3", "OPEN/CLOSE", 11, "0.06 0", "0.95 1", TextAnchor.MiddleLeft, $"robotocondensed-bold.ttf", "0 0 0 0.7", $"0.0 0.0");       
                        CUIClass.CreateButton(ref _editType, "btn_enable", "editType_div3", enable_color, $"ENABLE", 11, "0.3 0.2", $"0.6 0.8", $"storemenuvalue onCmdHideEdit {panelName} true", "", "1 1 1 0.7", 0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf");
                        CUIClass.CreateButton(ref _editType, "btn_disable", "editType_div3", disable_color, $"DISABLE", 11, "0.65 0.2", $"0.95 0.8", $"storemenuvalue onCmdHideEdit {panelName} false", "", "1 1 1 0.7", 0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf");
                        //Command
                        CUIClass.CreatePanel(ref _editType, "editType_div4", "editmenu_div", "0.11 0.11 0.11 0.95", "0.05 0.37", $"0.95 0.50", false, 0f, "assets/icons/iconmaterial.mat");
                        CUIClass.CreateText(ref _editType, "editType_text", "editType_div4", "1 1 1 0.3", $"CHAT COMMAND:  <size=14>/  </size><size=13><color=#4E4E4E>{currCommand}</color></size>", 11, "0.03 0", "0.95 0.9", TextAnchor.MiddleLeft, $"robotocondensed-bold.ttf", "0 0 0 0.7", $"0.0 0.0");
                        CUIClass.CreateInput(ref _editType, "editType_Input", "editType_div4", "1 1 1 1", 13, "0.3 0.0", "1 0.90", "robotocondensed-bold.ttf", $"storemenuvalue panelChatCmd", TextAnchor.MiddleLeft);
                        //BUTTONS
                        CUIClass.CreateButton(ref _editType, "editType_saveBtn", "editType_Input", "0.38 0.51 0.16 0.85", $"SAVE", 11, "0.75 0.15","0.97 0.85", $"storemenuvalue panelChatCmdEdit {panelName}", "", "1 1 1 0.7", 0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf");
                        CUIClass.CreateButton(ref _editType, "editType_backBtn", "editmenu_div", "0.11 0.11 0.11 0.95", $"BACK TO EDIT MENU", 13, "0.25 0.05","0.75 0.15", $"custombuttons_discardchanges {panelName}", "", "1 1 1 0.7", 0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf");
                        
                        DestroyEditType(player);
                        DestroyEditMenuCont(player);
                        CuiHelper.AddUi(player, _editType); 
                       

                    }


                    private void DestroyEditType(BasePlayer player)
                    {   
                        CuiHelper.DestroyUi(player, "empty"); 
                        CuiHelper.DestroyUi(player, "btn_back"); 
                        CuiHelper.DestroyUi(player, "editType_div1"); 
                        CuiHelper.DestroyUi(player, "editType_div2"); 
                        CuiHelper.DestroyUi(player, "editType_div3"); 
                        CuiHelper.DestroyUi(player, "editType_div4"); 
                        CuiHelper.DestroyUi(player, "editType_saveBtn"); 
                        CuiHelper.DestroyUi(player, "editType_backBtn"); 

                    }

                #endregion
            #endregion 
            #region Non-static Parts
                private void UiTypeButtons(BasePlayer player)
                {   
                    string overlay_color = "0.33 0.33 0.33 0.55";
                    string hud_color = "0.33 0.33 0.33 0.55";
                    if (storedCuiFields.ContainsKey("uiType") && storedCuiFields["uiType"] != null)
                    {
                        if (storedCuiFields["uiType"] == "Overlay") overlay_color = "0.16 0.34 0.49 0.55";
                        if (storedCuiFields["uiType"] == "Hud") hud_color = "0.16 0.34 0.49 0.55";
                    }
                    var _uiTypeButtons = CUIClass.CreateOverlay("empty", "0 0 0 0.0", "0 0", "0 0", false, 0.0f, $"assets/content/ui/uibackgroundblur.mat");
                    CUIClass.CreateButton(ref _uiTypeButtons, "uiType_overlay", "field_div2", overlay_color, $"OVERLAY", 13, "0.3 0.2", $"0.6 0.8", $"storemenuvalue uiType Overlay", "", "1 1 1 0.7", 0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf");
                    CUIClass.CreateButton(ref _uiTypeButtons, "uiType_hud", "field_div2", hud_color, $"HUD", 13, "0.65 0.2", $"0.95 0.8", $"storemenuvalue uiType Hud", "", "1 1 1 0.7", 0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf");
                    DestroyUiTypeBtn(player);
                    CuiHelper.AddUi(player, _uiTypeButtons);  
                    CuiHelper.DestroyUi(player, "empty"); 
                }

                private void DestroyUiTypeBtn(BasePlayer player)
                {
                    CuiHelper.DestroyUi(player, "uiType_overlay"); 
                    CuiHelper.DestroyUi(player, "uiType_hud");
                }

                private void PanelTypeButtons(BasePlayer player)
                {   
                    string btn_color = "0.33 0.33 0.33 0.55";
                    string panel_color = "0.33 0.33 0.33 0.55";
                    if (storedCuiFields.ContainsKey("panelType") && storedCuiFields["panelType"] != null)
                    {
                        if (storedCuiFields["panelType"] == "button") btn_color = "0.16 0.34 0.49 0.55";
                        if (storedCuiFields["panelType"] == "panel") panel_color = "0.16 0.34 0.49 0.55";
                    }
                    var _panelTypeButtons = CUIClass.CreateOverlay("empty", "0 0 0 0.0", "0 0", "0 0", false, 0.0f, $"assets/content/ui/uibackgroundblur.mat");
                    CUIClass.CreateButton(ref _panelTypeButtons, "panelType_btn", "field_div3", btn_color, $"BUTTON", 13, "0.3 0.2", $"0.6 0.8", $"storemenuvalue panelType button", "", "1 1 1 0.7", 0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf");
                    CUIClass.CreateButton(ref _panelTypeButtons, "panelType_panel", "field_div3", panel_color, $"PANEL", 13, "0.65 0.2", $"0.95 0.8", $"storemenuvalue panelType panel", "", "1 1 1 0.7", 0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf");
                    DestroyPanelTypeBtn(player);
                    CuiHelper.AddUi(player, _panelTypeButtons);   
                    CuiHelper.DestroyUi(player, "empty");
                }

                private void DestroyPanelTypeBtn(BasePlayer player)
                {   
                    CuiHelper.DestroyUi(player, "panelType_panel");
                    CuiHelper.DestroyUi(player, "panelType_btn"); 
                }

                private void ColorDisplay(BasePlayer player)
                {   
                    if (!storedCuiFields.ContainsKey("panelColor"))
                    {
                        
                        return;
                    }
                    var _colorDisplay = CUIClass.CreateOverlay("empty", "0 0 0 0.0", "0 0", "0 0", false, 0.0f, $"assets/content/ui/uibackgroundblur.mat");
                    CUIClass.CreatePanel(ref _colorDisplay, "color_display", "field_div4", storedCuiFields["panelColor"], "0.9 0.2", $"0.975 0.8", false, 0f, "assets/icons/iconmaterial.mat");
                    DestroyColorDisplay(player);
                    CuiHelper.AddUi(player, _colorDisplay);
                    CuiHelper.DestroyUi(player, "empty");   
                }

                private void DestroyColorDisplay(BasePlayer player)
                {
                    CuiHelper.DestroyUi(player, "color_display"); 
                }

                private void ImagePreview(BasePlayer player)
                {   
                    string imgUrl = "";
                    if (storedCuiFields.ContainsKey("imageUrl") && storedCuiFields["imageUrl"] != null)
                    {
                        imgUrl = storedCuiFields["imageUrl"];  
                    }
                    var _imagePreview = CUIClass.CreateOverlay("empty", "0 0 0 0.0", "0 0", "0 0", false, 0.0f, $"assets/content/ui/uibackgroundblur.mat");
                    CUIClass.CreatePanel(ref _imagePreview, "field_div11", "main_div", "0.11 0.11 0.11 0.95", "0.76 0.45", $"0.95 0.705", false, 0f, "assets/icons/iconmaterial.mat");
                            CUIClass.CreateText(ref _imagePreview, "uiType_text", "field_div11", "1 1 1 0.3", "UPLOADED IMAGE", 10, "0.05 0", "0.95 0.965", TextAnchor.UpperLeft, $"robotocondensed-bold.ttf", "0 0 0 0.7", $"0.0 0.0");
                            CUIClass.CreateImage(ref _imagePreview, "field_div11", imgUrl, $"0.05 0.03", $"0.95 0.85");
                    DestroyImagePreview(player);
                    CuiHelper.AddUi(player, _imagePreview);
                    CuiHelper.DestroyUi(player, "empty");   
                }

                private void DestroyImagePreview(BasePlayer player)
                {
                    CuiHelper.DestroyUi(player, "field_div11"); 
                }

                private void OpenCloseOption(BasePlayer player)
                {   
                    string enable_color = "0.33 0.33 0.33 0.55";
                    string disable_color = "0.33 0.33 0.33 0.55";
                    if (storedCuiFields.ContainsKey("onCmdHide") && storedCuiFields["onCmdHide"] != null)
                    {
                        if (storedCuiFields["onCmdHide"] == "true") enable_color = "0.16 0.34 0.49 0.55";
                        if (storedCuiFields["onCmdHide"] == "false") disable_color = "0.16 0.34 0.49 0.55";
                    } 
                    
                    var _panelTypeButtons = CUIClass.CreateOverlay("empty", "0 0 0 0.0", "0 0", "0 0", false, 0.0f, $"assets/content/ui/uibackgroundblur.mat");
                    CUIClass.CreateButton(ref _panelTypeButtons, "btn_enable", "field_div10", enable_color, $"ENABLE", 11, "0.05 0.1", $"0.47 0.6", $"storemenuvalue onCmdHide true", "", "1 1 1 0.7", 0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf");
                    CUIClass.CreateButton(ref _panelTypeButtons, "btn_disable", "field_div10", disable_color, $"DISABLE", 11, "0.5 0.1", $"0.95 0.6", $"storemenuvalue onCmdHide false", "", "1 1 1 0.7", 0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf");
                     
                    DestroyOpenCloseOption(player);
                    CuiHelper.AddUi(player, _panelTypeButtons);   
                    CuiHelper.DestroyUi(player, "empty");
                }

                private void DestroyOpenCloseOption(BasePlayer player)
                {   
                    CuiHelper.DestroyUi(player, "btn_enable");
                    CuiHelper.DestroyUi(player, "btn_disable"); 
                }


            #endregion
            #region Pop-up window
                private void PopUpWindow(BasePlayer player, string msgType, string message) 
                {   
                    string anchorMin = "0 0";
                    string anchorMax = "0 0";
                    if (msgType == "small")
                    {anchorMin = "0.35 0.45"; anchorMax = "0.65 0.65";}
                    if (msgType == "tooltip-offset")
                    {anchorMin = "0.35 0.10"; anchorMax = "0.65 0.85";}

                    var _ppWindow = CUIClass.CreateOverlay("_ppWindow", "0 0 0 0.5", "0 0", "1 1", false, 0.0f, $"assets/content/ui/uibackgroundblur.mat");
                    CUIClass.CreateButton(ref _ppWindow, "_ppWindow_btn", "_ppWindow", "0.22 0.22 0.22 1.0", $"{message}", 13, anchorMin, anchorMax, "custombuttons_open closepp", "", "1 1 1 0.7", 0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf");
                    
                    if (msgType == "tooltip-offset")
                    {   
                        string textOffset = "<size=13><b>OFFSETS</b></size>\n\n<b>Quick Tip:</b> By setting <b>OffsetMin</b> to <b><color=#1b84ca>-50 -50</color></b> and <b>OffsetMax</b> to <b><color=#1b84ca>50 50</color></b>, you will get a small square button right above the Hotbar, which you can adjust later in the Edit Menu → Position Settings. \n\nOffsets are used instead of Anchors as with Offsets the Cui elements will keep the same height/width ratio on wide screens as on standard 16:9. \n\nPositions start at the base point, which is defined by the anchors (in the config file), then by changing the Offset, you can stretch each side by different values (example picture above). \n\n OffsetMin '(<b>left</b>) (<b>bottom</b>)' \nOffsetMax '(<b>right</b>) (<b>top</b>)'";
                        string textAnchor = "<size=13><b>IMAGE ANCHORS</b></size>\n\n<b>Quick Tip:</b> Images are fully stretched on buttons by default, by uploading an image that matches the button size you can leave image anchors on the default value.\n\nEach value represents a starting point for each side of the image inside of the button, <b>Left</b> (0) to <b>Right</b> (1) and <b>Bottom</b> (0) to <b>Top</b> (1). \n\nAnchorMin '<b>0.2</b>(left) <b>0.2</b>(bottom)'\nAnchorMax '<b>0.8</b>(right) <b>0.8</b>(top)'";
                        CUIClass.CreateImage(ref _ppWindow, "_ppWindow_btn", "https://i.ibb.co/6ZR3mxy/offset.png", $"0.30 0.78", $"0.70 0.98");
                        CUIClass.CreateImage(ref _ppWindow, "_ppWindow_btn", "https://i.ibb.co/Yt2JQWn/anchor.png", $"0.27 0.27", $"0.67 0.48");
                        CUIClass.CreateText(ref _ppWindow, "_ppWindow_text", "_ppWindow_btn", "1 1 1 1", textOffset, 9, "0.05 0.45", "0.95 0.76", TextAnchor.UpperCenter, $"robotocondensed-regular.ttf", "0 0 0 0.7", $"0.0 0.0");
                        CUIClass.CreateText(ref _ppWindow, "_ppWindow_text", "_ppWindow_btn", "1 1 1 1", textAnchor, 9, "0.15 0.02", "0.85 0.26", TextAnchor.UpperCenter, $"robotocondensed-regular.ttf", "0 0 0 0.7", $"0.0 0.0");
                        CUIClass.CreateButton(ref _ppWindow, "_ppWindow_close", "_ppWindow", "0 0 0 0", $"", 13, "0 0", "1 1", "custombuttons_open closepp", "", "1 1 1 0.7", 0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf");
                    }
                    DestroyPopUpWindow(player);
                    CuiHelper.AddUi(player, _ppWindow);
                }

                private void DestroyPopUpWindow(BasePlayer player)
                {
                    CuiHelper.DestroyUi(player, "_ppWindow"); 
                }
            #endregion
        #endregion

        #region [CUI Classes]

        public class CUIClass
        {
            public static CuiElementContainer CreateOverlay(string _name, string _color, string _anchorMin, string _anchorMax, bool _cursorOn = false, float _fade = 0f, string _mat ="")
            {   
                
                bool _keyboard = false;
                if (_name != "empty*_")
                    _keyboard = true;
            
                var _element = new CuiElementContainer()
                {
                    {
                        new CuiPanel
                        {
                            Image = { Color = _color, Material = _mat, FadeIn = _fade},
                            RectTransform = { AnchorMin = _anchorMin, AnchorMax = _anchorMax },
                            CursorEnabled = _cursorOn, KeyboardEnabled = _keyboard,
                        },
                        new CuiElement().Parent = "Overlay",
                        _name
                    }
                };
                return _element;
            }

            public static void CreatePanel(ref CuiElementContainer _container, string _name, string _parent, string _color, string _anchorMin, string _anchorMax, bool _cursorOn = false, float _fade = 0f, string _mat2 ="", string _OffsetMin = "", string _OffsetMax = "" )
            {
                _container.Add(new CuiPanel
                {
                    Image = { Color = _color, Material = _mat2, FadeIn = _fade },
                    RectTransform = { AnchorMin = _anchorMin, AnchorMax = _anchorMax, OffsetMin = _OffsetMin, OffsetMax = _OffsetMax },
                    CursorEnabled = _cursorOn
                },
                _parent,
                _name);
            }

            public static void CreateImage(ref CuiElementContainer _container, string _parent, string _image, string _anchorMin, string _anchorMax, float _fade = 1f)
            {
                if (_image.StartsWith("http") || _image.StartsWith("www"))
                {
                    _container.Add(new CuiElement
                    {
                        Parent = _parent,
                        Components =
                        {
                            new CuiRawImageComponent { Url = _image, Sprite = "assets/content/textures/generic/fulltransparent.tga", FadeIn = _fade},
                            new CuiRectTransformComponent { AnchorMin = _anchorMin, AnchorMax = _anchorMax }
                        }
                    });
                }
                else
                {
                    _container.Add(new CuiElement
                    {
                        Parent = _parent,
                        Components =
                        {
                            new CuiRawImageComponent { Png = _image, Sprite = "assets/content/textures/generic/fulltransparent.tga", FadeIn = _fade},
                            new CuiRectTransformComponent { AnchorMin = _anchorMin, AnchorMax = _anchorMax }
                        }
                    });
                }
            }

            public static void CreateInput(ref CuiElementContainer _container, string _name, string _parent, string _color, int _size, string _anchorMin, string _anchorMax, string _font = "permanentmarker.ttf", string _command = "command.processinput", TextAnchor _align = TextAnchor.MiddleCenter)
            {
                _container.Add(new CuiElement
                {
                    Parent = _parent,
                    Name = _name,

                    Components =
                    {
                        new CuiInputFieldComponent
                        {

                            Text = "",
                            CharsLimit = 250,
                            Color = _color,
                            IsPassword = false,
                            Command = _command,
                            Font = _font,
                            FontSize = _size,
                            Align = _align
                        },

                        new CuiRectTransformComponent
                        {
                            AnchorMin = _anchorMin,
                            AnchorMax = _anchorMax

                        }

                    },
                });
            }

            public static void CreateText(ref CuiElementContainer _container, string _name, string _parent, string _color, string _text, int _size, string _anchorMin, string _anchorMax, TextAnchor _align = TextAnchor.MiddleCenter, string _font = "robotocondensed-bold.ttf", string _outlineColor = "", string _outlineScale ="")
            {   
               

                _container.Add(new CuiElement
                {
                    Parent = _parent,
                    Name = _name,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = _text,
                            FontSize = _size,
                            Font = _font,
                            Align = _align,
                            Color = _color,
                            FadeIn = 0f,
                        },

                        new CuiOutlineComponent
                        {
                            
                            Color = _outlineColor,
                            Distance = _outlineScale
                            
                        },

                        new CuiRectTransformComponent
                        {
                             AnchorMin = _anchorMin,
                             AnchorMax = _anchorMax
                        }
                    },
                });
            }

            public static void CreateButton(ref CuiElementContainer _container, string _name, string _parent, string _color, string _text, int _size, string _anchorMin, string _anchorMax, string _command = "", string _close = "", string _textColor = "0.843 0.816 0.78 1", float _fade = 1f, TextAnchor _align = TextAnchor.MiddleCenter, string _font = "")
            {       
               
                _container.Add(new CuiButton
                {
                    Button = { Close = _close, Command = _command, Color = _color, Material = "assets/icons/iconmaterial.mat", FadeIn = _fade},
                    RectTransform = { AnchorMin = _anchorMin, AnchorMax = _anchorMax },
                    Text = { Text = _text, FontSize = _size, Align = _align, Color = _textColor, Font = _font, FadeIn = _fade}
                },
                _parent,
                _name);
            }

        }
        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["wrongColorUsage"] = "<size=45>✘</size>\n<size=18>Wrong color format!</size>\nYou need to use RBGA color code with percentage value type. \n\n Example: 0.16 0.34 0.49 1.0",
                ["wrongAnchorUsage"] = "<size=45>✘</size>\n<size=18>Wrong anchor format!</size>\nAnchor must contain two values, press '?' for more info. \n\n Example: 0.5 0.7",
                ["missingCoreValues"] = "<size=45>✘</size>\n<size=18>Missing mandatory values!</size>\n Panel Name, UI Type, Panel Type and Anchors cannot be empty.",
                ["tooManyArguments"] = "<size=45>✘</size>\n<size=18>Too many arguments!</size>\n For multiple words please put text between 'quote marks'.\n\n Keep in mind that Panel Name has to be one word.",
                ["panelNameNotFound"] = "<size=45>✘</size>\n<size=18>Panel name was not found!</size>\n You can find all panel names in data file or by typing chat command \n\n'/custombuttons_list' ",
                ["changesSaved"] = "<size=45>✔</size>\n<size=18>Changes have been saved!</size>\n\nClick here to continue...",
                

            }, this);
        }

        private string GetLang(string _message) => lang.GetMessage(_message, this);

        #endregion

        #region [Debug]

        private bool _debugEnabled = false;
        private void _debug(string _debugMsg)
        {   
            if (_debugEnabled)
            {
                Puts($"{_debugMsg}");
            }
        }

        [ConsoleCommand("custombuttons_debug")]
        private void custombuttons_debug(ConsoleSystem.Arg arg)
        { 
            var args = arg.Args;
            if (args[0] == "true") { _debugEnabled = true; Puts($"Debug Enabled"); return; } 
            if (args[0] == "false") { _debugEnabled = false; Puts($"Debug Disabled"); return; } 
        }

        #endregion 
        
        #region [Config] 

        private Configuration config;
        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<Configuration>();
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = Configuration.CreateConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        

        class Configuration
        {
            [JsonProperty(PropertyName = "Hide Buttons")]
            public AddSet addSet { get; set; }
            public class AddSet
            {
                [JsonProperty("When using Computer Station")]
                public List<string> whenComputer {get; set;}

                [JsonProperty("When Player is dead")]
                public List<string> whenDead {get; set;}

                [JsonProperty("When Player is looting")]
                public List<string> whenLooting {get; set;}
            } 

            [JsonProperty(PropertyName = "Base Position")]
            public AnchSet anchSet { get; set; }
            public class AnchSet
            {
                [JsonProperty("Anchor Min")]
                public string anchorMin {get; set;}

                [JsonProperty("Anchor Max")]
                public string anchorMax {get; set;}
            } 

            [JsonProperty(PropertyName = "Font Settings")]
            public FontSet fontSet { get; set; }
            public class FontSet
            {
                [JsonProperty("Font Style")]
                public string fontStyle {get; set;}
                
                [JsonProperty("Base Font Size")]
                public int fontSize {get; set;}

                [JsonProperty("Base Font Color")]
                public string fontColor {get; set;}

                [JsonProperty("Font Outline Color")]
                public string fontOutColor {get; set;}

                [JsonProperty("Font Outline Thickness")]
                public string fontOutThic {get; set;}
                
            } 

            [JsonProperty(PropertyName = "Chat Commands")]
            public CommSet commSet { get; set; }
            public class CommSet
            {
                [JsonProperty("Open GUI")]
                public string openCmd { get; set; }

                [JsonProperty("List all Panels")]
                public string listPanels { get; set; }

                [JsonProperty("Toggle buttons on/off")]
                public string togglePanels { get; set; }
            }

            public static Configuration CreateConfig()
            {
                return new Configuration
                {   
                    addSet = new CustomButtons.Configuration.AddSet
                    {   
                        whenComputer = new List<string>
                        {
                                "button_name",
                                "panel_name"
                        },
                        whenDead = new List<string>
                        {
                                "button_name",
                                "panel_name"
                        },
                        whenLooting = new List<string>
                        {
                                "button_name",
                                "panel_name"
                        },
                    },

                    anchSet = new CustomButtons.Configuration.AnchSet
                    {   
                        anchorMin = "0.5 0.2",
                        anchorMax = "0.5 0.2",
                    },

                    fontSet = new CustomButtons.Configuration.FontSet
                    {   
                        fontStyle = "robotocondensed-bold.ttf",
                        fontSize = 13,
                        fontColor = "1 1 1 1",
                        fontOutColor = "0 0 0 1",
                        fontOutThic = "0.1 0.1",
                    },

                    commSet = new CustomButtons.Configuration.CommSet
                    {
                        openCmd = "cb",
                        listPanels = "cb_list",
                        togglePanels = "cb_toggle"
                    },
                };
            }
        }
        #endregion
       
    }
}


///Скачано с дискорд сервера Rust Edit [PRO+]
///discord.gg/9vyTXsJyKR