using Oxide.Core;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using Oxide.Game.Rust.Cui;
using System.Text.RegularExpressions;
using UnityEngine;

/*	This plugin is originally based off of MisterPixie's Crosshair 3.1.0 and credit is given per the MIT License Agreement.
 *
 *	Drastic overhaul and rewrite have occurred to allow customization of the crosshair and storing toggle state per user.
 *
 *	Future plans:
 *		- create on-screen UI for managing customization.
 *		- make sure that all of the "using" inheritances above are needed.
 *		- Add support-for and use-of Carbon's CUI
 *		- Add an outline or dropshadow to the cursor (and make it customizable, too)
 *
 *	(c) 2024 TheAlchemist
 */
 
namespace Oxide.Plugins
{
    [Info("Crosshair", "TheAlchemist", "1.0.0")]
    [Description("Allows the user to toggle a crosshair. Also allows for minimal customization of the crosshair and if it loads automatically for each player.")]

    class Crosshair : RustPlugin
    {
		private bool DebugEnabled = true;

		#region Fields

        private const string _usePerm = "crosshair.use";

        private string mainUI = "UI_MAIN";
        private CuiElementContainer _ui;
       		
		private static string DetermineDataFilePath() => $"{nameof(Crosshair)}/";
		private static string PreferencesFilePath() => DetermineDataFilePath() + "/Preferences";
		private PreferenceData preferenceData;		
        private UserPreferences userPreferences;

		private HashSet<string> _playersWithCrosshairEnabled = new HashSet<string>();
		
		#endregion Fields
				
        #region Classes for UI
 
		private class UI
        {
            public static CuiElementContainer CreateElementContainer(string panelName, string color, string aMin, string aMax, bool cursor = false)
            {
                var newElement = new CuiElementContainer
                {
                    {
                        new CuiPanel
                        {
                            Image = {Color = color},
                            RectTransform = {AnchorMin = aMin, AnchorMax = aMax},
                            CursorEnabled = cursor
                        },
                        new CuiElement().Parent = "Hud",
                        panelName
                    }
                };

                return newElement;
            }

            public static void CreateLabel(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter, float fadein = 1f)
            {
                container.Add(new CuiLabel
                {
                    Text = { Color = color, FontSize = size, Align = align, FadeIn = fadein, Text = text },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax }
                }, panel, CuiHelper.GetGuid());
            }
        }
 
        #endregion Classes for UI

        #region Methods

        private void CreateUI()
        {			 
            var container = UI.CreateElementContainer(mainUI, $"{HexToColor("000000")} 0", "0.4692 0.4672", "0.5292 0.5272");
			UI.CreateLabel(ref container, mainUI, $"{HexToColor(configData.CrosshairColor)} 0.9", configData.CrosshairText, 25, "0 0", "1 1");            
			_ui = container;
        }
		
        private void CreateUI(BasePlayer player)
        {			
			if (player == null) return;
			
			DestroyCrosshair(player);
			_ui = null;
			
            var container = UI.CreateElementContainer(mainUI, $"{HexToColor("000000")} 0", "0.4692 0.4672", "0.5292 0.5272");
			
			string color = ( string.IsNullOrEmpty(GetUsersPreference(player, "color")) ) ? configData.CrosshairColor : GetUsersPreference(player, "color");
			string text = ( string.IsNullOrEmpty(GetUsersPreference(player, "text")) ) ? configData.CrosshairText : GetUsersPreference(player, "text");

			UI.CreateLabel(ref container, mainUI, $"{HexToColor(color)} 0.9", text, 25, "0 0", "1 1");            

			_ui = container;
			
			if( GetUsersPreference(player, "auto").ToLower() == "true" )
				CrosshairOn(player);
        }

		//
		// This toggle is based solely on whether or not the user currently exists in the HashSet _playersWithCrosshairEnabled that is in memory (not from a file)
		//
        private void ToggleCrosshair(BasePlayer player, string command, string[] args)
        {
            if (_playersWithCrosshairEnabled.Contains(player.UserIDString))
            {
				CrosshairOff(player);    
            } 
			else 
			{
				CrosshairOn(player);				
			}
        }
		
		private void CrosshairOn(BasePlayer player)
		{
			CrosshairOff(player); 									// just making sure we don't double up memory on the HashSet or the UI
            CuiHelper.AddUi(player, _ui); 							// displays whatever ui elements are currently saved in _ui
            _playersWithCrosshairEnabled.Add(player.UserIDString);
		}

		private void CrosshairOff(BasePlayer player)
		{
			DestroyCrosshair(player);
			_playersWithCrosshairEnabled.Remove(player.UserIDString);
		}

        private void DestroyCrosshair(BasePlayer player)
        {
			if (player == null) return;
            CuiHelper.DestroyUi(player, mainUI);
        }
						
        private void DetermineCrosshairPreference(BasePlayer player)
        {
            if (player == null || player.IsNpc || !player.IsAlive() || player.IsSleeping())
                return;

            if (!permission.UserHasPermission(player.UserIDString, _usePerm))
                return;

			if (GetUsersPreference(player, "auto").ToLower() == "true")
				CreateUI(player);
        }

        #endregion Methods

        #region Hooks
		
        private void Init()
        {
            permission.RegisterPermission("crosshair.use", this);

            LoadConfigVariables();			
			LoadPreferenceData();
		
            foreach (var player in BasePlayer.activePlayerList)
            {
				DetermineCrosshairPreference(player);
            }

			if (_ui == null)
				CreateUI();
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyCrosshair(player);
				_playersWithCrosshairEnabled.Remove(player.UserIDString);
            }
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
			if (player.IsNpc) return;
			
            DestroyCrosshair(player);
        }
		
		private void OnPlayerConnected(BasePlayer player) => DetermineCrosshairPreference(player);
		private void OnPlayerRespawned(BasePlayer player) => DetermineCrosshairPreference(player);
        private void OnPlayerSleepEnded(BasePlayer player) => OnPlayerRespawned(player);
        private void OnPlayerSleep(BasePlayer player) => OnPlayerDisconnected(player);		
        private void OnEntityKill(BasePlayer player) => OnPlayerDisconnected(player);
        private void OnEntityDeath(BasePlayer player, HitInfo info) => OnPlayerDisconnected(player);
		
        #endregion Hooks

        #region Config
		
        private ConfigData configData;
        private class ConfigData
        {
			public string CrosshairEnabled;
            public string CrosshairColor;
            public string CrosshairText;
        }

        private void LoadConfigData() => configData = Config.ReadObject<ConfigData>();
        private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        private void SaveConfig() => Config.WriteObject(configData, true);

        private void LoadConfigVariables()
        {
            LoadConfigData();
            SaveConfig();
        }

        protected override void LoadDefaultConfig()				// CrosshairText = "◎"
        {
            var config = new ConfigData
            {
				CrosshairEnabled = "true",
                CrosshairColor = "#FFFFFF",
                CrosshairText = "◎"
            };
            SaveConfig(config);
        }
		
        #endregion Config

        #region Config Data File - User Preferences

		//
		// Global fields are located in Fields region at top of program
		//
		 
		private class PreferenceData // the preference data of all players in the preference data file
		{
			public HashSet<PlayerInfo> Players = new HashSet<PlayerInfo>();
		}
	
        private class UserPreferences // an individual player's preferences
        {
			public bool CrosshairEnabled;
            public string CrosshairColor;
            public string CrosshairText;

			public UserPreferences()
			{
			}
        }

		private class PlayerInfo
		{
			public string Id;
			public string Name;
			public UserPreferences Preferences = new UserPreferences();
			
			public PlayerInfo()
			{
			}

			public PlayerInfo(BasePlayer player)
			{
				Id = player.UserIDString;
				Name = player.displayName;
				Preferences = this.Preferences;
			}

		}
		
		private string GetUsersPreference(BasePlayer player, string key)																// TO-DO:  should we just get the ones in memory, or re-read from the file?
		{
			string val = "";
			
			PlayerInfo playerPreferences = preferenceData.Players.Where(x => x.Id.Equals(player.UserIDString)).FirstOrDefault();
			
			if (playerPreferences != null)
			{
				switch (key)
				{
					case "auto":
						val = playerPreferences.Preferences.CrosshairEnabled.ToString();
						break;
					case "color":
						val = playerPreferences.Preferences.CrosshairColor;
						break;
					case "text":
						val = playerPreferences.Preferences.CrosshairText;
						break;
					default:
						break;
				}
			}
			
			return val;
		}
		
		private void SetUsersPreference(BasePlayer player, string key, string val)
		{
			PlayerInfo playerPreferences = preferenceData.Players.Where(x => x.Id.Equals(player.UserIDString)).FirstOrDefault();
			
			if (playerPreferences != null)
			{
				switch (key)
				{
					case "auto":
						playerPreferences.Preferences.CrosshairEnabled = bool.Parse(ProcessAutoValue(val, player));
						break;
					case "color":
						playerPreferences.Preferences.CrosshairColor = ProcessColorValue(val, player);
						break;
					case "text":
						playerPreferences.Preferences.CrosshairText = ProcessTextValue(val, player);
						break;
					case "default":
						playerPreferences.Preferences.CrosshairEnabled = bool.Parse(configData.CrosshairEnabled);
						playerPreferences.Preferences.CrosshairColor = configData.CrosshairColor;
						playerPreferences.Preferences.CrosshairText = configData.CrosshairText;
						break;
					default:
						break;
				}
			}


// TO-DO                                     Need to reload the file into memory after setting preferences?                        TO-DO						
			return;
		}
			
        private void LoadPreferenceData() => preferenceData = Interface.Oxide.DataFileSystem.ReadObject<PreferenceData>(PreferencesFilePath());
		
        #endregion Data File - User Preferences

		#region Commands

		[Command("crosshair")]
		private void CrosshairCommand(BasePlayer player, string command, string[] args)
		{
            if (!permission.UserHasPermission(player.UserIDString, _usePerm))
                return;
			
			if (args.Length == 0)
			{
				ToggleCrosshair(player, command, args);
				return;
			}
			
			string key = "";
			string valAuto = "";
			string valColor = "";
			string valText = "";
			
			bool refreshUI = false;

			if (args.Length == 1)
			{
				key = args[0].ToLower();
				
				if (key != "auto" && key != "color" && key != "text" && key != "default")
				{
					player.ChatMessage("'" + key + "' is not a valid command or configurable parameter for the Crosshair");
					return;
				}

				switch (key)
				{
					case "auto":
						player.ChatMessage("Current preference setting for Crosshair's auto-enable setting 'auto' is: " + GetUsersPreference(player, key));
						break;
					case "color":
						player.ChatMessage("Current preference setting for Crosshair's color is: " + GetUsersPreference(player, key));
						break;
					case "text":
						player.ChatMessage("Current preference setting for Crosshair's text is: " + GetUsersPreference(player, key));
						break;					
					case "default":
						player.ChatMessage("Resetting the Crosshair to the server's default settings");
						SetUsersPreference(player, key, "");
						refreshUI = true;
						break;					
					default:
						break;
				}

				//return;
			}
			else
			{				
				key = args[0];
				string val = args[1].ToLower();
				
				switch (key)
				{
					case "auto":
						valAuto = ProcessAutoValue(val, player);
						if (valAuto == "") {
							player.ChatMessage("Invalid value for Crosshair's 'auto' setting");
							return;
						}
						break;
					case "color":
						valColor = ProcessColorValue(val, player);
						if (valColor == "") {
							player.ChatMessage("Invalid value for Crosshair's 'color' setting");
							return;
						}
						break;
					case "text":
						valText = ProcessTextValue(val, player);
						if (valText == "") {
							player.ChatMessage("Invalid value for Crosshair's 'text' setting");
							return;
						}
						break;
					default:
						player.ChatMessage("'" + key + "' is not a configurable parameter for the Crosshair");
						return;
						break;
				}
				
				refreshUI = true;
			}

			if (refreshUI)
			{
			
//			PlayerInfo playerInfo = new PlayerInfo(player);
				PlayerInfo playerInfo = new PlayerInfo(player);
				PlayerInfo playerPreferences = preferenceData.Players.Where(x => x.Id.Equals(playerInfo.Id)).FirstOrDefault();
				
				if (playerPreferences != null)
				{				
					player.ChatMessage("Updating your existing preferences...");
					
					playerInfo.Preferences.CrosshairEnabled = (valAuto == "") ? playerPreferences.Preferences.CrosshairEnabled : (valAuto == "true") ? true : false;
					playerInfo.Preferences.CrosshairColor = (valColor == "") ? playerPreferences.Preferences.CrosshairColor : valColor;
					playerInfo.Preferences.CrosshairText = (valText == "") ? playerPreferences.Preferences.CrosshairText : valText;

					preferenceData.Players.Remove(playerPreferences);
				}
				else
				{
					player.ChatMessage("Saving your Crosshair preference to the data file...");
					
					playerInfo.Preferences.CrosshairEnabled = (valAuto == "") ? bool.Parse(configData.CrosshairEnabled) : (valAuto == "true") ? true : false;
					playerInfo.Preferences.CrosshairColor = (valColor == "") ? configData.CrosshairColor: valColor;
					playerInfo.Preferences.CrosshairText = (valText == "") ? configData.CrosshairText : valText;	
				}

				preferenceData.Players.Add(playerInfo);
				Interface.Oxide.DataFileSystem.WriteObject(PreferencesFilePath(), preferenceData);
				
				if (playerInfo.Preferences.CrosshairEnabled || _playersWithCrosshairEnabled.Contains(player.UserIDString) )
				{
					CreateUI(player);
					CrosshairOn(player);
				}
				
				if (!playerInfo.Preferences.CrosshairEnabled && _playersWithCrosshairEnabled.Contains(player.UserIDString) )
				{
					CrosshairOff(player);
				}
			}
		}		
		
		#endregion Commands

		#region Helpers
		
		private string ProcessAutoValue(string val, BasePlayer player)
		{
			string vval = "";
			
			switch (val)
			{
				case "true":
				case "1":
				case "on":
				case "enable":
				case "enabled":
					vval = "true";
					break;
				case "false":
				case "0":
				case "off":
				case "disable":
				case "disabled":
					vval = "false";
					break;
				case "default":
				default:
					player.ChatMessage("'" + val + "' is not a valid value for the 'auto' setting. Resetting to server default value: " + configData.CrosshairEnabled);			
					vval = configData.CrosshairEnabled;
					break;
				
			}
			
			return vval;
		}

		private string ProcessColorValue(string val, BasePlayer player)
		{
			string vval = "";
			
			if (IsValidHexaCode(val) || IsValidHexaCode("#" + val))
			{
				if (val.Length == 6) val = "#" + val;
				vval = val;
			}
			else
			{			
				switch (val)
				{
					case "red":
						vval = "#FF0000";
						break;
					case "green":
						vval = "#00FF00";
						break;
					case "blue":
						vval = "#0000FF";
						break;
					case "white":
						vval = "#FFFFFF";
						break;
					case "black":
						vval = "#000000";
						break;
					case "gray":
					case "grey":
						vval = "#888888";
						break;
					case "default":
					default:
						player.ChatMessage("'" + val + "' is not a valid value for the 'color' setting. Resetting to server default value: " + configData.CrosshairColor);			
						vval = configData.CrosshairColor;
						break;
				}
			}
			
			return vval;
		}

		private string ProcessTextValue(string val, BasePlayer player)   //  Default Text: "◎"
		{
			string vval = "";
			
			if (val == "default" )
			{
				vval = configData.CrosshairText;
			}
			else
			{
				vval = val.Substring(0,1);
			}
			
			return vval;
		}

        private static string HexToColor(string hexColor)
        {
            if (hexColor.IndexOf('#') != -1) hexColor = hexColor.Replace("#", "");

            var red = 0;
            var green = 0;
            var blue = 0;

            if (hexColor.Length == 6)
            {
                red = int.Parse(hexColor.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                green = int.Parse(hexColor.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                blue = int.Parse(hexColor.Substring(4, 2), NumberStyles.AllowHexSpecifier);
            }
            else if (hexColor.Length == 3)
            {
                red = int.Parse(hexColor[0] + hexColor[0].ToString(), NumberStyles.AllowHexSpecifier);
                green = int.Parse(hexColor[1] + hexColor[1].ToString(), NumberStyles.AllowHexSpecifier);
                blue = int.Parse(hexColor[2] + hexColor[2].ToString(), NumberStyles.AllowHexSpecifier);
            }

            return $"{(double)red / 255} {(double)green / 255} {(double)blue / 255}";
        }
		
		public static bool IsValidHexaCode(string str)
		{
			string strRegex = @"^#([A-Fa-f0-9]{6})$";
			
			Regex re = new Regex(strRegex);
			if (re.IsMatch(str))
				return true;
			else
				return false;
		}

		#endregion Helpers
		
		#region Debug
		
        private void PrintDebug(string message, bool warning = false, bool error = false)
        {
            if (DebugEnabled)
            {
                if (error)
                {
                    PrintError(message);
                }
                else if (warning)
                {
                    PrintWarning(message);
                }
                else
                {
                    Puts(message);
                }
            }
        }
		
        #endregion Debug

    }
}