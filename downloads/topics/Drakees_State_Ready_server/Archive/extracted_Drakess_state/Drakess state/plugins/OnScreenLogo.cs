using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using UnityEngine;
using JSON;

namespace Oxide.Plugins
{
	/* Based on version 1.1.5 by Vlad-00003 */
    [Info("OnScreenLogo", "Nimant", "1.0.5", ResourceId = 2601)]
    [Description("Displays the button on the player screen.")]
    //Author info:
    //E-mail: Vlad-00003@mail.ru
    //Vk: vk.com/vlad_00003
    class OnScreenLogo : RustPlugin
    {
		
		[PluginReference]
  		private Plugin AspectRatio;
		
        private string PanelName = "GsAdX1wazasdsHs";
        private string Image = null;
        private static OnScreenLogo instance;
		private static List<ulong> ExcludePlayers = new List<ulong>();
		private const string PermToggleAllow = "onscreenlogo.toggle";
		
		private float MinX, MinY, MaxX, MaxY;

        #region Config Setup
		
        private string Amax = "0.34 0.105";
        private string Amin = "0.26 0.025";
        private string ImageAddress = "https://i.imgur.com/Fx2iX0x.png";				
		
        #endregion

        #region Initialization
		
		private void Init()
		{
			instance = this;
			permission.RegisterPermission(PermToggleAllow, this);
			LoadData();
		}
		
        protected override void LoadDefaultConfig() => PrintWarning("Generating default configuration file...");
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
            
			GetConfig("Image. Link or name of the file in the data folder", ref ImageAddress);
            GetConfig("Minimum anchor", ref Amin);
            GetConfig("Maximum anchor", ref Amax);
			
			MinX = Convert.ToSingle(Amin.Split(' ')[0]);
			MinY = Convert.ToSingle(Amin.Split(' ')[1]);
			MaxX = Convert.ToSingle(Amax.Split(' ')[0]);
			MaxY = Convert.ToSingle(Amax.Split(' ')[1]);
            
			if (!ImageAddress.ToLower().Contains("http"))            
                ImageAddress = "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + ImageAddress;
                        
            SaveConfig();
        }
        
        private void OnServerInitialized() => RunCheckTimer();
		
        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)            
                CuiHelper.DestroyUi(player, PanelName);            
        }
		
        #endregion
		
		#region Commands
		
		[ChatCommand("logo")]
        private void cmdChatToggleLogo(BasePlayer player, string command, string[] args)
        {
            if (player == null || (!permission.UserHasPermission(player.UserIDString, PermToggleAllow) && !player.IsAdmin)) return;
			
			if (args==null || args.Length==0 || (args[0] != "on" && args[0] != "off"))
			{
				SendReply(player, "Использование: logo <on|off> - включить или выключить отображение логотипа.");
				return;
			}	
			
			if (args[0]=="off")
			{	
				if (!ExcludePlayers.Contains(player.userID))
					ExcludePlayers.Add(player.userID);
				CuiHelper.DestroyUi(player, PanelName);   
				SendReply(player, "Отображение логотипа выключено.");
			}	
			
			if (args[0]=="on")
			{					
				if (ExcludePlayers.Contains(player.userID))
					ExcludePlayers.Remove(player.userID);
				CreateButton(player, false);
				SendReply(player, "Отображение логотипа включено.");
			}	
			
			SaveData();
		}
		
		#endregion

        #region Image Downloading
        
		private int CountRefresh = 30;
		
		private void RunCheckTimer()
		{
			if (Image == null)
			{
				DownloadImage();
				timer.Once(1f, RunCheckTimer);
				return;
			}
			
			foreach (BasePlayer player in BasePlayer.activePlayerList)                
				CreateButton(player, false);

			if (CountRefresh > 0)
			{
				Image = null;
				timer.Once(2f, RunCheckTimer);
				CountRefresh--;
			}
		}
		
        private void DownloadImage() => ServerMgr.Instance.StartCoroutine(DownloadImage(ImageAddress));        
		
        private IEnumerator DownloadImage(string url)
        {
            using (var www = new WWW(url))
            {
                yield return www;
                if (instance == null) yield break;
                if (www.error != null)                
                    PrintError($"Failed to add image. File address possibly invalide\n {url}");                
                else
                {
                    var tex = www.texture;
                    byte[] bytes = tex.EncodeToPNG();
                    Image = FileStorage.server.Store(bytes, FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID).ToString();                                        															
                    UnityEngine.Object.DestroyImmediate(tex);
                    yield break;
                }
            }
        }
		
        #endregion
		
		#region Resolution

		private static Dictionary<ulong, int> PlayerResolution = new Dictionary<ulong, int>();
		
		private static int GetUserResolution(ulong userID, bool refresh = false)
		{
			int resolution = 1;
			
			if (!refresh && PlayerResolution.ContainsKey(userID))
				resolution = PlayerResolution[userID];
			else
				if (instance.AspectRatio != null)
				{
					resolution = (int)instance.AspectRatio?.Call("ApiGetRatio", userID);
					
					if (!PlayerResolution.ContainsKey(userID))
						PlayerResolution.Add(userID, resolution);
					else
						PlayerResolution[userID] = resolution;
				}
				
			return resolution;
		}
		
		private void ApiSetRatio(BasePlayer player, int resolution) => CreateButton(player, true);
		
		public static float GetCorrectAnchors(ulong userID, float MinX, float MinY, float MaxX, float MaxY, ref string anchorMin, ref string anchorMax, bool refresh)
		{			
			float defaultX = 0f, deltaX = 0f;																	
			var resolution = GetUserResolution(userID, refresh);												
			defaultX = (MaxY - MinY) / (16f/10f);
			
			switch (resolution)
			{
				case 0: deltaX = (MaxY - MinY) / (16f/9f); break;
				case 3: deltaX = (MaxY - MinY) / (5f/4f); break;
				case 2: deltaX = (MaxY - MinY) / (4f/3f); break;
				default:
				case 1: deltaX = (MaxY - MinY) / (16f/10f); break;
			}
						
			defaultX -= deltaX;
			
			anchorMin = $"{MinX + defaultX/2f} {MinY}";
			anchorMax = $"{MaxX - defaultX/2f} {MaxY}";
			
			return defaultX;
		}
		
		#endregion

        #region UI
		
        private void OnPlayerSleepEnded(BasePlayer player) => CreateButton(player, false);
		
        private void CreateButton(BasePlayer player, bool refresh)
        {
			if (ExcludePlayers.Contains(player.userID)) return;
			
            CuiHelper.DestroyUi(player, PanelName);
            CuiElementContainer elements = new CuiElementContainer();
			
			string AnchorMin = Amin, AnchorMax = Amax;
			
			GetCorrectAnchors(player.userID, MinX, MinY, MaxX, MaxY, ref AnchorMin, ref AnchorMax, refresh);
			
            var panel = elements.Add(new CuiPanel
            {
                Image = {
                    Color = "0.00 0.00 0.00 0.00"
                },
                RectTransform = {
                    AnchorMin = AnchorMin,
                    AnchorMax = AnchorMax
                },
                CursorEnabled = false
            }, "Hud", PanelName);
			
            var comp = new CuiRawImageComponent { Sprite = "assets/content/textures/generic/fulltransparent.tga" };
			
            if (Image != null)
                comp.Png = Image;
            
            elements.Add(new CuiElement
            {
                Parent = PanelName,
                Components =
                {
                    comp,
                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });
			
            CuiHelper.AddUi(player, elements);			
			Interface.CallHook("API_OnLogoDrew", player);
        }								
		
        #endregion

        #region Helpers
		
        private void GetConfig<T>(string Key, ref T var)
        {
            if (Config[Key] != null)
            {
                var = (T)Convert.ChangeType(Config[Key], typeof(T));
            }
            Config[Key] = var;
        }
		
        #endregion
		
		#region API
		
		private List<string> API_GetLogoSize(ulong userID)
		{
			string AnchorMin = Amin, AnchorMax = Amax;			
			GetCorrectAnchors(userID, MinX, MinY, MaxX, MaxY, ref AnchorMin, ref AnchorMax, false);
			return new List<string>() { AnchorMin, AnchorMax };			
		}
		
		#endregion
		
		#region Data
		
		private void LoadData() => ExcludePlayers = Interface.GetMod().DataFileSystem.ReadObject<List<ulong>>("OnScreenLogoData");					
		
		private void SaveData() => Interface.GetMod().DataFileSystem.WriteObject("OnScreenLogoData", ExcludePlayers);		
		
		#endregion
		
    }
}
