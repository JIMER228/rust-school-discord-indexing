using System.Collections.Generic;
using Oxide.Game.Rust.Cui;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("kryWhiteList", "xkrystalll", "1.0.0")]
    class kryWhiteList : RustPlugin
    {
        public const string LRbuttons = @"[
{
    ""name"": ""wlToggle.buttonR"",
    ""parent"": ""wlToggle.playersInList"",
    ""components"": [
      {
        ""type"": ""UnityEngine.UI.RawImage"",
        ""sprite"": ""assets/content/textures/generic/fulltransparent.tga"",
        ""color"": ""1 0 0 1"",
        ""url"": ""https://media.discordapp.net/attachments/746052324635574337/766737700710645820/arrow_right.png"",
        ""png"": ""https://media.discordapp.net/attachments/746052324635574337/766737700710645820/arrow_right.png""
      },
      {
        ""type"": ""RectTransform"",
        ""anchormin"": ""0.8910891 0.01324522"",
        ""anchormax"": ""0.9966998 0.2251656"",
        ""offsetmin"": ""0 0"",
        ""offsetmax"": ""0 0""
      }
    ]
  },
  {
    ""name"": ""wlToggle.buttonR.b"",
    ""parent"": ""wlToggle.buttonR"",
    ""components"": [
      {
        ""type"": ""UnityEngine.UI.Button"",
        ""command"": ""kryWhiteListnext"",
        ""color"": ""1 1 1 0""
      },
      {
        ""type"": ""RectTransform"",
        ""anchormin"": ""0 0"",
        ""anchormax"": ""1 1"",
        ""offsetmax"": ""0 0""
      }
    ]
  },
  {
    ""name"": ""wlToggle.buttonL"",
    ""parent"": ""wlToggle.playersInList"",
    ""components"": [
      {
        ""type"": ""UnityEngine.UI.RawImage"",
        ""sprite"": ""assets/content/textures/generic/fulltransparent.tga"",
        ""color"": ""0 0 0 1"",
        ""url"": ""https://media.discordapp.net/attachments/746052324635574337/766737698404171896/arrow_left.png"",
        ""png"": ""https://media.discordapp.net/attachments/746052324635574337/766737698404171896/arrow_left.png""
      },
      {
        ""type"": ""RectTransform"",
        ""anchormin"": ""0.003300287 0.01324522"",
        ""anchormax"": ""0.1089109 0.2251656"",
        ""offsetmin"": ""0 0"",
        ""offsetmax"": ""0 0""
      }
    ]
  },
  {
    ""name"": ""wlToggle.buttonL.b"",
    ""parent"": ""wlToggle.buttonL"",
    ""components"": [
      {
        ""type"": ""UnityEngine.UI.Button"",
        ""command"": ""kryWhiteListprev"",
        ""color"": ""1 1 1 0""
      },
      {
        ""type"": ""RectTransform"",
        ""anchormin"": ""0 0"",
        ""anchormax"": ""1 1"",
        ""offsetmax"": ""0 0""
      }
    ]
  }
]";
        public const string permToggle = "kryWhiteList.use";
        public const string InputField = @"[
{
    ""name"": ""InputField.bg"",
    ""parent"": ""wlToggle.bg"",
    ""components"": [
      {
        ""type"": ""UnityEngine.UI.Image"",
        ""sprite"": """",
        ""material"": """",
        ""color"": ""1 1 1 0.1103616""
      },
      {
        ""type"": ""RectTransform"",
        ""anchormin"": ""0.3372222 0.04722217"",
        ""anchormax"": ""0.703125 0.1524444"",
        ""offsetmax"": ""0 0""
      }
    ]
  },
  {
    ""name"": ""inputfield"",
    ""parent"": ""InputField.bg"",
    ""components"": [
      {
        ""type"": ""UnityEngine.UI.InputField"",
        ""text"": ""Введите Steam64 или ник"",
        ""fontSize"": 18,
        ""font"": ""robotocondensed-bold.ttf"",
        ""align"": ""MiddleCenter"",
        ""color"": ""1 1 1 0.8019574"",
        ""characterLimit"": 32,
        ""command"": ""kryWhiteListAdd ""
      },
      {
        ""type"": ""RectTransform"",
        ""anchormin"": ""0 0"",
        ""anchormax"": ""1 1"",
        ""offsetmax"": ""0 0""
      }
    ]
  },
  {
    ""name"": ""IF.label"",
    ""parent"": ""wlToggle.bg"",
    ""components"": [
      {
        ""type"": ""UnityEngine.UI.Text"",
        ""text"": ""Введите ID или ник игрока, что бы добавить его"",
        ""font"": ""robotocondensed-bold.ttf"",
        ""align"": ""MiddleCenter""
      },
      {
        ""type"": ""RectTransform"",
        ""anchormin"": ""0.3395833 0.1777778"",
        ""anchormax"": ""0.7010417 0.325"",
        ""offsetmax"": ""0 0""
      }
    ]
  },
  {
    ""name"": ""CuiElement"",
    ""parent"": ""Overlay"",
    ""components"": [
      {
        ""type"": ""RectTransform"",
        ""anchormin"": ""0.0625 0.1111111"",
        ""anchormax"": ""0.125 0.2222222"",
        ""offsetmax"": ""0 0""
      }
    ]
  }
]";
        public const string wlToggleHudButton = @"[
  {
    ""name"": ""wlToggle.button.menu"",
    ""parent"": ""Hud"",
    ""components"": [
      {
        ""type"": ""UnityEngine.UI.RawImage"",
        ""color"": ""1 1 1 1"",
        ""url"": ""https://media.discordapp.net/attachments/746052324635574337/767428273462050846/wlicon_1.png"",
        ""png"": ""https://media.discordapp.net/attachments/746052324635574337/767428273462050846/wlicon_1.png""
      },
      {
        ""type"": ""RectTransform"",
        ""anchormin"": ""0.7318751 0.01277782"",
        ""anchormax"": ""0.7707502 0.08966653"",
        ""offsetmin"": ""0 0"",
        ""offsetmax"": ""0 0""
      }
    ]
  },
  {
    ""name"": ""wlToggle.button"",
    ""parent"": ""wlToggle.button.menu"",
    ""components"": [
      {
        ""type"": ""UnityEngine.UI.Button"",
        ""command"": ""chat.say /whitelist"",
        ""color"": ""0 0 0 0""
      },
      {
        ""type"": ""RectTransform"",
        ""anchormin"": ""0 0"",
        ""anchormax"": ""1 1"",
        ""offsetmax"": ""0 0""
      }
    ]
  }
]";
		public const string hasPlayerUI = @"";

        #region [GUIBUILDER]
        protected CuiElement Panel(string name, string sprite, string color, string anMin, string anMax, string parent, string png, bool cursor)
			{
				var Element = new CuiElement()
				
				{
					Name = name,
					Parent = parent,
					Components =
					{
						new CuiImageComponent { Color = color, Sprite = sprite, Png = png },
						new CuiRectTransformComponent { AnchorMin = anMin, AnchorMax = anMax }
					}
				};
				if (cursor)
				{
					Element.Components.Add(new CuiNeedsCursorComponent());
				}
				return Element;
			}
			protected CuiElement Text(string name, string parent, string color, string text, TextAnchor pos, int fsize, string anMin, string anMax, string fname = "robotocondensed-bold.ttf")
			{
				var Element = new CuiElement()
				{
                    Name = name,
					Parent = parent,
					Components =
					{
						new CuiTextComponent() { Color = color, Text = text, Align = pos, Font = fname, FontSize = fsize },
						new CuiRectTransformComponent{ AnchorMin = anMin, AnchorMax = anMax }
					}
				};
				return Element;
			}
			protected CuiElement Button(string name, string parent, string sprite, string command, string color, string anMin, string anMax)
			{
				var Element = new CuiElement()
				{
					Name = name,
					Parent = parent,
					Components =
					{
						new CuiButtonComponent { Command = command, Color = color, Sprite = sprite },
						new CuiRectTransformComponent{ AnchorMin = anMin, AnchorMax = anMax }
					}
				};
				return Element;
			}
        #endregion
        
        #region [FUNCTIONS]
        // Функции плагина
			private int countWhiteListed = 0;
            private List<string> openUIs = new List<string>();
			private List<string> openWarns = new List<string>();
            private List<string> polMin = new List<string>();
            private List<string> polMax = new List<string>();
			private Dictionary<string, int> playerStr = new Dictionary<string, int>(); 

            private void fillPol(string[] polMinAdd, string[] polMaxAdd)
            {
                foreach (string polmin in polMinAdd)
                {
                    polMin.Add(polmin);
                }
                foreach (string polmax in polMaxAdd)
                {
                    polMax.Add(polmax);
                }
            }

			
            public bool hasPlayerInWhiteList(string id)
            {
                bool hasPlayer = false;
                bool CheckHasPlayer = checkWhiteList(id);
                if (CheckHasPlayer)
                {
                    hasPlayer = true;
                }
                return hasPlayer;
            }
            private bool checkWhiteList(string playerIDCheck)
            {
                bool hasPlayer = false;
                foreach (string playerID in Configuration.whitelistedUsers)
                {
                    if (playerID == playerIDCheck)
                    {
                        hasPlayer = true;
                        break;
                    }
                }
                return hasPlayer;
            }
            public void closeTrigger(BasePlayer player)
            {
                if (openUIs.Contains(player.UserIDString))
                {
                    openUIs.Remove(player.UserIDString);
                    CuiHelper.DestroyUi(player, "wlToggle.bg");
                }
            }
            public void showWhUI(BasePlayer player)
            {
				if (!playerStr.ContainsKey(player.UserIDString)) { playerStr.Add(player.UserIDString, 1); }
                if (openUIs.Contains(player.UserIDString)) { return; }
                CuiElementContainer whUI1 = new CuiElementContainer();
                whUI1.Add(Panel("wlToggle.bg", "assets/content/ui/ui.background.tile.psd", "0.752457 0.752457 0.752457 0.679359", "0.2 0.3", "0.8 0.7", "Overlay", "", true));
                whUI1.Add(Panel("wlToggle.playersInList", "assets/content/ui/ui.icon.rust.png", "1 1 1 0.3372549", "0.01562499 0.04722217", "0.33125 0.4666666", "wlToggle.bg", "", false));
                for (int iter = 0; iter < 5; iter++)
                {
                    whUI1.Add(Panel($"wlToggle.pol{iter}", "assets/content/ui/ui.icon.rust.png", "0 0 0 1", $"{polMin[iter]}", $"{polMax[iter]}", "wlToggle.playersInList", "", false));
                }
                whUI1.Add(Panel("wlToggle.list", "assets/content/ui/ui.icon.rust.png", "1 1 1 0", "0 0.25", "1 1", "wlToggle.playersInList", "", false));
                whUI1.Add(Button("wlToggle.exit", "wlToggle.bg", "assets/icons/close.png", "kryWhiteListclose", "1 1 1 0.6663844", "0.9614583 0.9055555", "0.9947919 0.9972221"));
                whUI1.Add(Text("wlToggle.label", "wlToggle.bg", "1 1 1 0.7830964", "WhiteListUI", TextAnchor.MiddleCenter, 17, "0.3 0.9", "0.7 0.98"));
                whUI1.Add(Panel("wlToggle.toggle.bg", "assets/content/ui/ui.icon.rust.png", "1 1 1 0", "0.01145835 0.903", "0.1156249 0.973", "wlToggle.bg", "", false));
                whUI1.Add(Panel("wlToggle.list.enter", "assets/content/ui/ui.icon.rust.png", "1 1 1 0", "0.01 0.037", "0.99 0.95", "wlToggle.list", "", false));
				for (int iter = 5; iter < 9; iter++)
                {
                    whUI1.Add(Panel($"wlToggle.pols{iter}", "assets/content/ui/ui.icon.rust.png", "0 0 0 1", $"{polMin[iter]}", $"{polMax[iter]}", "wlToggle.bg", "", false));
                }
                if (Configuration.isEnabled.Trim().ToLower() == "yes")
                {
                    whUI1.Add(Panel("wlToggle.on.bg", "assets/content/ui/ui.icon.rust.png", "0 1 0.1239945 0.6", "0 0", "0.5 1", "wlToggle.toggle.bg", "", false));
                    whUI1.Add(Text("wlToggle.on.text", "wlToggle.on.bg", "1 1 1 0.7", "ON", TextAnchor.MiddleCenter, 12, "0 0", "1 1"));
                    whUI1.Add(Button("wlToggle.off.bg", "wlToggle.toggle.bg", "assets/content/ui/ui.icon.rust.png", "kryWhiteListOff", "1 0 0 0.2", "0.5 0", "0.98 1"));
                    whUI1.Add(Text("wlToggle.off.text", "wlToggle.off.bg", "1 1 1 0.7", "OFF", TextAnchor.MiddleCenter, 12, "0 0", "1 1"));
                }
                else 
                {
                    //on
                    whUI1.Add(Button("wlToggle.on.bg", "wlToggle.toggle.bg", "assets/content/ui/ui.icon.rust.png", "kryWhiteListOn", "1 0 0 0.2", "0 0", "0.5 1"));
                    whUI1.Add(Text("wlToggle.on.text", "wlToggle.on.bg", "1 1 1 0.7", "ON", TextAnchor.MiddleCenter, 12, "0 0", "1 1"));
                    //off
                    whUI1.Add(Panel("wlToggle.off.bg", "assets/content/ui/ui.icon.rust.png", "1 0 0 0.6", "0.5 0", "1 1", "wlToggle.toggle.bg", "", false));
                    whUI1.Add(Text("wlToggle.off.text", "wlToggle.off.bg", "1 1 1 0.7", "OFF", TextAnchor.MiddleCenter, 12, "0 0", "0.98 1"));
                }
                whUI1.Add(Text("wl.toggle.list.label", "wlToggle.bg", "1 1 1 1", "Игроки в WhiteList'e", TextAnchor.MiddleCenter, 20, "0.01770832 0.4777779", "0.3302083 0.5861111"));
				CuiHelper.AddUi(player, whUI1);
                CuiHelper.AddUi(player, LRbuttons);
				CuiHelper.AddUi(player, InputField);
                fillWhiteListUI(player);
                openUIs.Add(player.UserIDString);
            }
			public void addWarn(BasePlayer player, string type)
			{
				if (openWarns.Contains(player.UserIDString)) { return; }
				openWarns.Add(player.UserIDString);
                CuiElementContainer warnUI = new CuiElementContainer();
				warnUI.Add(Panel("wlToggle.warn", "", "1 1 1 0.302", "0.2458333 0.1722222", "0.76875 0.775", "wlToggle.bg", "", true));
				warnUI.Add(Panel("wlToggle.warn.icon", "assets/icons/warning_2.png", "1 1 1 0.556", "0.005976152 0.8018434", "0.0916335 0.9839173", "wlToggle.warn", "", false));
				warnUI.Add(Button("wlToggle.warn.button.bg", "wlToggle.warn", "", "kryWhiteListCloseWarn", "0.6112108 1 0.456998 0.4153024", "0.3 0.04", "0.7 0.23"));
				warnUI.Add(Text("wlToggle.warn.button.desc", "wlToggle.warn.button.bg", "1 1 1 0.7", "Понятно", TextAnchor.MiddleCenter, 20, "0 0", "1 1"));

				if (type == "hasPlayer") 
				{
				 	warnUI.Add(Text("wlToggle.warn.label", "wlToggle.warn", "1 1 1 1", "Игрок уже в WhiteList'е", TextAnchor.MiddleCenter, 16, "0.121514 0.83", "0.8426296 0.95"));
					warnUI.Add(Text("wlToggle.warn.desc", "wlToggle.warn", "1 1 1 0.707", "Указанный игрок уже существует в WhiteList'e.", TextAnchor.UpperCenter, 14, "0.09521896 0.4792627" ,"0.8884462 0.7741937"));
				}
				if (type == "noPlayer")
				{
					warnUI.Add(Text("wlToggle.warn.label", "wlToggle.warn", "1 1 1 1", "Игрок нету в WhiteList'е", TextAnchor.MiddleCenter, 16, "0.121514 0.83", "0.8426296 0.95"));
					warnUI.Add(Text("wlToggle.warn.desc", "wlToggle.warn", "1 1 1 0.707", "Указанный игрок не существует в WhiteList'e.", TextAnchor.UpperCenter, 14, "0.09521896 0.4792627" ,"0.8884462 0.7741937"));
				}

				CuiHelper.AddUi(player, warnUI);
			}
			public void closeWarn(BasePlayer player)
			{
				CuiHelper.DestroyUi(player, "wlToggle.warn");
				openWarns.Remove(player.UserIDString);
			}
			private List<string> playerIDstr = new List<string>();
			private List<int> playerListStr = new List<int>();
			private void fillListsUp()
			{
				foreach (string s in playerStr.Keys)
				{
					playerIDstr.Add(s);
					
				}
				foreach (int s in playerStr.Values)
				{
					playerListStr.Add(s);
				}
			}
            public void fillWhiteListUI(BasePlayer player)
            {
				string pID = player.UserIDString;
				int str = 0;
				fillListsUp();
				for (int iter = 0; iter < playerStr.Keys.Count; iter++)
				{
					if (playerIDstr[iter] == pID)
					{
						str = playerListStr[iter];
						break;
					}
				}
                string anMinX = "0.015";
                string anMaxX = "0.98";

                double anMinY = 0.7;
                double anMaxY = 0.97;
                int iter1 = 0;
                CuiElementContainer whUI1 = new CuiElementContainer();
				int formulaStr1 = (str * 3) - 3;
				for (int iterator = formulaStr1; iterator < Configuration.whitelistedUsers.Count; iterator++)
				{
					string dispName;
                    try
                    {
                        BasePlayer playerr = BasePlayer.FindByID(System.Convert.ToUInt64(Configuration.whitelistedUsers[iterator]));
                        dispName = playerr.displayName;
                    }
                    catch
                    {
                        dispName = Configuration.whitelistedUsers[iterator];
                    }
                    int ost = iter1 % 3;
                    if (ost == 0 && iter1 != 0)
                    {
                        break;
                    }
                    
                    whUI1.Add(Panel($"wlToggle.player.{iter1}", "assets/content/ui/ui.icon.rust.png", "1 1 1 0.5", $"{anMinX} {anMinY.ToString()}", $"{anMaxX} {anMaxY.ToString()}", "wlToggle.list.enter", "", false));
                    whUI1.Add(Text($"wlToggle.player.text.{iter1}", $"wlToggle.player.{iter1}", "0 0 0 0.8", $"{dispName}", TextAnchor.MiddleCenter, 12, "0 0", "1 1"));
					whUI1.Add(Button($"wlToggle.player.button.{iter1}", $"wlToggle.player.{iter1}", "assets/icons/close.png", $"kryWhiteListRemove {Configuration.whitelistedUsers[iterator]}", "1 0 0 0.7", "0.89 0.03", "0.995 0.97"));
                    
                    ++iter1;
                    anMinY -= 0.33;
                    anMaxY -= 0.33;
				}
                CuiHelper.AddUi(player, whUI1);
            }
			private int calculatePages()
			{
				int lists = Configuration.whitelistedUsers.Count;
				int calc = 1;
				for (int calc1 = 0; lists > 3; calc1++)
				{
					lists -= 3;
					calc++;
				}
				return calc;
			}
		#endregion
	
        #region [INITIALIZE PLUGIN]

        // При загрузке плагина
        void Loaded()
        {
            LoadConfig();
            LoadButton();
			foreach (string d in Configuration.whitelistedUsers)
			{
				countWhiteListed += 1;
			}
		    string[] polmin = {"0 0",     "0 0",   "0 0.99", "0.995 0", "0 0.27", "0.116 0.9", "0.01045835 0.98", "0.01045835 0.898", "0.01045835 0.9"};
            string[] polmax = {"0.005 1", "1 0.01", "1 1",   "1 1",     "1 0.28", "0.117 0.98", "0.1165 0.982", "0.1165 0.9", "0.011 0.98"};
            fillPol(polmin, polmax);
			permission.RegisterPermission(permToggle, this);
        }
        // При отгрузке плагина
        void Unload()
        {
			SaveConfig(Configuration);
            UnloadAll();
        }
        #endregion
        // Main Code
        #region [MAIN]
        void LoadButton()
        {
            foreach (BasePlayer p in BasePlayer.activePlayerList)
            {
                if (permission.UserHasPermission(p.UserIDString, permToggle))
                {
                    CuiHelper.AddUi(p, wlToggleHudButton);
                }
            }
        }
        void UnloadAll()
        {
            foreach (BasePlayer p in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(p, "wlToggle.bg");
                CuiHelper.DestroyUi(p, "wlToggle.button.menu");
            }
        }
        void OnPlayerConnected(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, permToggle))
            {
                CuiHelper.AddUi(player, wlToggleHudButton);
            }
        }
        object CanUserLogin(string name, string id, string ipAddress)
        {
            if (Configuration.isEnabled.ToLower().Trim() == "yes")
            {
				bool canConnect = true;
                if (!hasPlayerInWhiteList(id)) { canConnect = false; Server.Broadcast($"Игрок: {name} \nID: {id} \n IP: {ipAddress} \nПопытка соединения с сервером <color=red>не удалась.</color>\nПричина - игрок не записан в WhiteList.", "<color=purple>[WhiteList]</color>"); return Configuration.textOnKick; }
                return canConnect;
            }

            else
            {
                return true;
            }
        }
		void OnServerSave()
		{
			SaveConfig(Configuration);
		}
        #endregion
        // Plugin Config
        #region [CONFIGURATION]
        public ConfigData Configuration;
        public class ConfigData
        {
            [JsonProperty("Пользователи в white list (steamid) через запятую")]
            public List<string> whitelistedUsers = new List<string>();
            [JsonProperty("Включить whitelist? (yes/no)")]
            public string isEnabled = "yes";
            [JsonProperty("Текст при кике с сервера")]
            public string textOnKick = "На сервере ведутся тех. работы. Зайдите позже";
        }
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData();
            SaveConfig(config);
        } 
        void LoadConfig() 
        {
            Configuration = Config.ReadObject<ConfigData>();
            SaveConfig(Configuration);
        }
        void SaveConfig(object config) => Config.WriteObject(config, true);
        #endregion
        // Commands
        #region [COMMANDS]
        [ChatCommand("whitelist")]
        void whitelistcommand(BasePlayer player)
        {
             showWhUI(player);
        }

        [ConsoleCommand("kryWhiteListclose")]
        void closeUI(ConsoleSystem.Arg argument)
        {
            closeTrigger(argument.Player());
        }
		[ConsoleCommand("kryWhiteListAdd")]
        void addtowhitelist(ConsoleSystem.Arg arg)
        {
			string arg1;
			try
			{
				arg1 = arg.Args[0];
			}
			catch
			{
				arg1 = "nothing";
			}

			countWhiteListed = Configuration.whitelistedUsers.Count;
			if (arg1 == "nothing") { return; }
            if (Configuration.whitelistedUsers.Contains(arg.Args[0])) 
			{ 
				addWarn(arg.Player(), "hasPlayer");
				return; 
			}
			Configuration.whitelistedUsers.Add(arg.Args[0]);
			for(int i = 0; i < countWhiteListed; i++)
			{
				CuiHelper.DestroyUi(arg.Player(), $"wlToggle.player.{i}");
			}
			fillWhiteListUI(arg.Player());
        }
		[ConsoleCommand("kryWhiteListprev")]
        void prevpage(ConsoleSystem.Arg arg)
        {
			for(int isd = 0; isd < countWhiteListed; isd++)
			{
				CuiHelper.DestroyUi(arg.Player(), $"wlToggle.player.{isd}");
			}
			for (int i = 0; i < playerIDstr.Count; i++)
			{
				if (playerIDstr[i] == arg.Player().UserIDString)
				{
					if (playerListStr[i] <= 1) { break;}
					playerIDstr.RemoveAt(i);
					int curstr = playerListStr[i];
					playerListStr.RemoveAt(i);

					playerIDstr.Add(arg.Player().UserIDString);
					playerListStr.Add(curstr -= 1);
					int res = curstr - 1;
					PrintWarning(res + " prevPage");
					break;
				}
			}
			fillWhiteListUI(arg.Player());

        }
		[ConsoleCommand("kryWhiteListnext")]
        void nextpage(ConsoleSystem.Arg arg)
        {
			int pages = calculatePages();
			for(int isd = 0; isd < countWhiteListed; isd++)
			{
				CuiHelper.DestroyUi(arg.Player(), $"wlToggle.player.{isd}");
			}
			for (int i = 0; i < playerIDstr.Count; i++)
			{
				if (playerIDstr[i] == arg.Player().UserIDString)
				{
					if (playerListStr[i] >= pages) { break;}
					playerIDstr.RemoveAt(i);
					int curstr = playerListStr[i];
					playerListStr.RemoveAt(i);

					playerIDstr.Add(arg.Player().UserIDString);
					playerListStr.Add(curstr += 1);
					int res = curstr + 1;
					PrintWarning(res + " nextPage");
					break;
				}
			}
			fillWhiteListUI(arg.Player());
        }
		[ConsoleCommand("kryWhiteListRemove")]
		void removefromwhitelist(ConsoleSystem.Arg arg)
		{
			countWhiteListed = Configuration.whitelistedUsers.Count;
			if (!Configuration.whitelistedUsers.Contains(arg.Args[0])) 
			{ 
				addWarn(arg.Player(), "noPlayer");
				return; 
			}
			Configuration.whitelistedUsers.Remove(arg.Args[0]);
			for(int i = 0; i < countWhiteListed; i++)
			{
				CuiHelper.DestroyUi(arg.Player(), $"wlToggle.player.{i}");
			}
			fillWhiteListUI(arg.Player());
		}

		[ConsoleCommand("kryWhiteListCloseWarn")]
		void closeWarn(ConsoleSystem.Arg arg)
		{
			closeWarn(arg.Player());
		}
        [ConsoleCommand("kryWhiteListOn")]
        void wlOn(ConsoleSystem.Arg argument)
        {
            Configuration.isEnabled = "yes";
            SaveConfig(Configuration);
            CuiHelper.DestroyUi(argument.Player(), "wlToggle.toggle.bg");

            CuiElementContainer whUI = new CuiElementContainer();

            whUI.Add(Panel("wlToggle.toggle.bg", "assets/content/ui/ui.icon.rust.png", "1 1 1 0", "0.01145835 0.903", "0.1156249 0.973", "wlToggle.bg", "", false));
            whUI.Add(Panel("wlToggle.on.bg", "assets/content/ui/ui.icon.rust.png", "0 1 0.1239945 0.6", "0 0", "0.5 1", "wlToggle.toggle.bg", "", false));
            whUI.Add(Text("wlToggle.on.text", "wlToggle.on.bg", "1 1 1 0.7", "ON", TextAnchor.MiddleCenter, 12, "0 0", "1 1"));
            whUI.Add(Button("wlToggle.off.bg", "wlToggle.toggle.bg", "assets/content/ui/ui.icon.rust.png", "kryWhiteListOff", "1 0 0 0.2", "0.5 0", "1 1"));
            whUI.Add(Text("wlToggle.off.text", "wlToggle.off.bg", "1 1 1 0.7", "OFF", TextAnchor.MiddleCenter, 12, "0 0", "1 1"));
            CuiHelper.AddUi(argument.Player(), whUI);
        }

        [ConsoleCommand("kryWhiteListOff")]
        void wlOff(ConsoleSystem.Arg argument)
        {
            Configuration.isEnabled = "no";
            SaveConfig(Configuration);
            CuiHelper.DestroyUi(argument.Player(), "wlToggle.toggle.bg");

            CuiElementContainer whUI = new CuiElementContainer();

            whUI.Add(Panel("wlToggle.toggle.bg", "assets/content/ui/ui.icon.rust.png", "1 1 1 0", "0.01145835 0.903", "0.1156249 0.973", "wlToggle.bg", "", false));
            whUI.Add(Button("wlToggle.on.bg", "wlToggle.toggle.bg", "assets/content/ui/ui.icon.rust.png", "kryWhiteListOn", "1 0 0 0.2", "0 0", "0.5 1"));
            whUI.Add(Text("wlToggle.on.text", "wlToggle.on.bg", "1 1 1 0.7", "ON", TextAnchor.MiddleCenter, 12, "0 0", "1 1"));
            whUI.Add(Panel("wlToggle.off.bg", "assets/content/ui/ui.icon.rust.png", "1 0 0 0.6", "0.5 0", "0.98 1", "wlToggle.toggle.bg", "", false));
            whUI.Add(Text("wlToggle.off.text", "wlToggle.off.bg", "1 1 1 0.7", "OFF", TextAnchor.MiddleCenter, 12, "0 0", "1 1"));
            CuiHelper.AddUi(argument.Player(), whUI);
        }
        #endregion
    }
    
}