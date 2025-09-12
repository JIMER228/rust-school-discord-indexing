using System;
using System.Linq;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Game.Rust.Cui;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("AspectRatio", "Nimant", "1.0.2")]	
    public class AspectRatio : RustPlugin
    {
		
		#region Variables
        
		private static Dictionary<ulong, int> ResolutionData = new Dictionary<ulong, int>();		
		
		#endregion		
		
		#region Init				

        private void Init()
		{
			LoadVariables();
			LoadDefaultMessages();
			LoadData();			
		}	                      

        private void Unload() 
		{						
			foreach (BasePlayer player in BasePlayer.activePlayerList)				
				CuiHelper.DestroyUi(player, "ResolutionMain");			
		}	

        private void OnPlayerSleepEnded(BasePlayer player)
        {
            if (!configData.ShowOnPlayerInit) return;
            if (player == null) return;
            if (ResolutionData.ContainsKey(player.userID)) return;

            if (player.IsReceivingSnapshot)
            {
                timer.Once(1f, () => OnPlayerSleepEnded(player));
                return;
            }

            ShowResolutionMenu(player);
        }
		
		#endregion
		
		#region GUI

		private void ShowResolutionMenu(BasePlayer player)
		{
			CuiHelper.DestroyUi(player, "ResolutionMain");

			CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiElement
            {
                Name = "ResolutionMain",
				Parent = "Hud",
                Components =
                {
                    new CuiImageComponent { Color = "0 0 0 1" },					
					new CuiNeedsCursorComponent(),
                    new CuiRectTransformComponent()					
                }
            });            

            container.Add(new CuiElement
            {
                Name = CuiHelper.GetGuid(),
                Parent = "ResolutionMain",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = GetLangMessage("RES_TITLE"),
                        FontSize = 25,
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.2 0.85",
                        AnchorMax = "0.8 0.95"
                    },
                    new CuiOutlineComponent() { Color = "0 0 0 1" }
                }
            });

            /* 
				Формула:
				для разрешение 16/9: 
					Х = 9/16
					B = (xMax-xMin)/X
					Ymin = Ymax-B
			*/

            int userResolution = ApiGetRatio(player.userID);

            CreateBox(container, "0.2 0.4444", "0.4 0.8", 0, userResolution == 0);
            CreateBox(container, "0.6 0.48",   "0.8 0.8", 1, userResolution == 1);
            CreateBox(container, "0.2 0.1333", "0.4 0.4", 2, userResolution == 2);
            CreateBox(container, "0.6 0.15",   "0.8 0.4", 3, userResolution == 3);
            											
			CuiHelper.AddUi(player, container); 
		}
				
        private void CreateBox(CuiElementContainer container, string AnchorMin, string AnchorMax, int Resolution, bool Active = false)
        {
            string BoxName = CuiHelper.GetGuid();

            container.Add(new CuiElement
            {
                Name = BoxName,
                Parent = "ResolutionMain",
                Components =
                {                    
					new CuiRawImageComponent 
					{
						Url = "https://i.imgur.com/oZUlnsn.png",
						Color = "1 1 1 1",
						Sprite = "assets/content/textures/generic/fulltransparent.tga"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = AnchorMin,
                        AnchorMax = AnchorMax
                    }
                }
            });			
			
            container.Add(new CuiElement
            {
                Name = CuiHelper.GetGuid(),
                Parent = BoxName,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = GetSimilar(Resolution),
                        FontSize = 10,
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent(),
                    new CuiOutlineComponent() { Color = "0 0 0 1" }
                }
            });
			
			container.Add(new CuiElement
            {
                Name = CuiHelper.GetGuid(),
                Parent = BoxName,
                Components =
                {
                    new CuiButtonComponent
                    {
                        Command = "aspectratio.select " + Resolution,
                        Close = "ResolutionMain",
                        Color = "1 1 1 0"
                    },
                    new CuiRectTransformComponent()                    
                }
            });
        }									
		
		[ConsoleCommand("aspectratio.select")]
		private void ConsoleCmdSelect(ConsoleSystem.Arg arg)
		{
			if (arg.Connection != null)
            {
				int selectedResolution = Convert.ToInt32(arg.Args[0]);

                switch(selectedResolution)
                {
                    case 0:
                    case 1:
                    case 2:
                    case 3: break;

                    default: return;
                }

                BasePlayer player = arg.Player();
				if (player == null) return;

				ResolutionData[player.userID] = selectedResolution;
                Interface.Oxide.CallHook("ApiSetRatio", player, selectedResolution);
				SaveData();

				SendReply(player, $"{string.Format(GetLangMessage("RES_SELECTED"), GetTextRatio(selectedResolution))}");
			}
		}
		
		#endregion
		
		#region Commands
		
		[ChatCommand("ar")]
        private void ChatCmdRatio(BasePlayer player, string command, string[] args) => ShowResolutionMenu(player);
		
		#endregion
		
		#region Common
		
		private ulong ConvertSteamId64to32(ulong userID) => (ulong)Convert.ToInt64(userID.ToString().Substring(3)) - 61197960265728;
		
		private ulong ConvertSteamId32to64(ulong userID) => (ulong)Convert.ToInt64("765" + (userID + 61197960265728).ToString());
		
		private string GetSimilar(int resolution)
		{
			switch(resolution)
			{
				case 0: return "\n<size=40>16:9</size><size=20>\n\n1920x1080\n1600x900\n1366x768\n1280x720\n</size>";
				case 1: return "\n<size=40>16:10</size><size=20>\n\n1920x1200\n1680x1050\n1440x900\n1280x800\n</size>";				
				case 2: return "\n<size=40>4:3</size><size=20>\n\n1400x1050\n1280x960\n1152x864\n1024x768\n</size>";
				case 3: return "\n<size=40>5:4</size><size=20>\n\n1280x1024\n</size>";

				default: return string.Empty;
			}
			
			return string.Empty;
		}
		
		private string GetTextRatio(int ratio)
		{
			switch(ratio)
			{
				case 0: return "16:9";
				case 1: return "16:10";
				case 2: return "4:3";
				case 3: return "5:4";

				default: return "16:9";
			}
			return "16:9";
		}
		
		#endregion

		#region ExtHook
        
		int ApiGetRatio(ulong userId)
        {
            int resolutionState;

            if (ResolutionData.TryGetValue(userId, out resolutionState))
                return resolutionState;

            return 0;
        }
		
		/* void ApiSetRatio(BasePlayer player, int resolution); */
		
		#endregion
		
		#region Lang
		
        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"RES_TITLE", "<color=#FFFFFF>Выберите свое разрешение экрана, если не нашли его,\nвыберите <color=#D3442E>НАИБОЛЕЕ КРУГЛОЕ</color> изображение логотипа Rust.</color>"},
                {"RES_SELECTED", "Соотношение сторон экрана изменено на \"{0}\".\nИспользуйте /ar для повторной калибровки интерфейса."}                
            }, this);
        }

        private string GetLangMessage(string key, string steamID = null) => lang.GetMessage(key, this, steamID);
		
		#endregion
		
		#region Config        	
		
        private ConfigData configData;
		
        private class ConfigData
        {            
			[JsonProperty(PropertyName = "Показывать калибровку интерфейса при первом входе на сервер")]
			public bool ShowOnPlayerInit;
        }
		
        private void LoadVariables() => configData = Config.ReadObject<ConfigData>();        
		
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                ShowOnPlayerInit = false
            };
            SaveConfig(config);
			timer.Once(0.1f, ()=>SaveConfig(config));
        }        
		
        private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
		
        #endregion
		
		#region Data
		
		private void LoadData() 
		{
			var resolutionData_ = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<ulong, int>>("AspectRatioData");
			
			// импортируем старые данные
			var resolutionOldData_ = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<ulong, int>>("AspectRatio");
			if (resolutionOldData_ != null)
			{
				foreach (var pair in resolutionOldData_)				
					if (!resolutionData_.ContainsKey(pair.Key))
						resolutionData_.Add(pair.Key, pair.Value);
			}						
			
			ResolutionData = resolutionData_.ToDictionary(x=>ConvertSteamId32to64(x.Key), x=>x.Value);
			SaveData();
		}	
		
		private void SaveData() 
		{						
			var resolutionData_ = ResolutionData.ToDictionary(x=>ConvertSteamId64to32(x.Key), x=>x.Value);									
			Interface.GetMod().DataFileSystem.WriteObject("AspectRatioData", resolutionData_);		
		}	
		
		#endregion
		
    }
}