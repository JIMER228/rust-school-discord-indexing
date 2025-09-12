using Oxide.Core;
using Oxide.Game.Rust.Cui;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("OnScreenLogo", "https://discord.gg/9vyTXsJyKR", "1.0.0")]
    internal class OnScreenLogo : RustPlugin
    {
		
		#region Variables
		
        private static string PanelName = "GsAdX1wazasdsHs2";        
        private static OnScreenLogo instance;
		private int CountRefresh = 30;
		private static string ImageDefault = null;
		
		#endregion

		#region Hooks
		
		private void Init()
		{
			instance = this;
			LoadVariables();
			ImageDefault = null; 	// гребаный раст, возможно нужно делать проверку на удаленность crc записей			
		}
		
		private void OnServerInitialized() => timer.Once(3f, RunCheckTimer);
		
		private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList.Where(x=> x != null && x.userID.IsSteamId()))
                CuiHelper.DestroyUi(player, PanelName);           
        }
		
		private void OnPlayerConnected(BasePlayer player)
		{	
			if (player == null) return;
			
			if (player.IsReceivingSnapshot)
            {
                timer.Once(0.1f, () => OnPlayerConnected(player));
                return;
            }
			
			OnPlayerSleepEnded(player);
		}
		
		private void OnPlayerSleepEnded(BasePlayer player) 
		{
			if (player == null) return;
			timer.Once(0.5f, ()=> CreateButton(player));
		}
		
		#endregion		        

        #region Main
		
		private void RunCheckTimer()
		{
			if (string.IsNullOrEmpty(ImageDefault))
			{
				DownloadImages();
				timer.Once(5f, RunCheckTimer);
				return;
			}
			
			foreach (BasePlayer player in BasePlayer.activePlayerList.Where(x=> x != null && x.userID.IsSteamId()))                
				CreateButton(player);

			if (CountRefresh > 0)
			{
				timer.Once(3f, RunCheckTimer);
				CountRefresh--;
			}
		}		
        
		private void DownloadImages() => ServerMgr.Instance.StartCoroutine(DownloadImage(configData.DefaultLogo));
		
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
                    var img = FileStorage.server.Store(bytes, FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID).ToString();										
					ImageDefault = img;
                    UnityEngine.Object.DestroyImmediate(tex);
                    yield break;
                }
            }
        }
		
        #endregion

        #region UI
		
        private void CreateButton(BasePlayer player)
        {
			if (player == null) return;
			
            CuiHelper.DestroyUi(player, PanelName);
            CuiElementContainer elements = new CuiElementContainer();
			
            var panel = elements.Add(new CuiPanel
            {
                Image = {
                    Color = "0.00 0.00 0.00 0.00"
                },
                RectTransform = {
                    AnchorMin = configData.MinAnchor,
                    AnchorMax = configData.MaxAnchor
                },
                CursorEnabled = false
            }, "Hud", PanelName);
			
            var comp = new CuiRawImageComponent { Sprite = "assets/content/textures/generic/fulltransparent.tga" };
			var img = ImageDefault;
            if (!string.IsNullOrEmpty(img)) comp.Png = img;
            
            elements.Add(new CuiElement
            {
                Parent = PanelName,
                Components =
                {
                    comp,
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });
			
			elements.Add(new CuiButton
			{
				Button = { Command = "chat.say /info", Color = "0 0 0 0" },
				RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
				Text = { Text = "" }
			}, PanelName);
			
            CuiHelper.AddUi(player, elements); 
        }
		
        #endregion
		
		#region Config
		
        private static ConfigData configData;
		
        private class ConfigData
        {            
			[JsonProperty(PropertyName = "Ссылка на лого")]
			public string DefaultLogo;			
			[JsonProperty(PropertyName = "Минимальный якорь")]
			public string MinAnchor;
			[JsonProperty(PropertyName = "Максимальный якорь")]
			public string MaxAnchor;
        }
		
        private void LoadVariables() => configData = Config.ReadObject<ConfigData>();        
		
        protected override void LoadDefaultConfig()
        {
            configData = new ConfigData
            {
                DefaultLogo = "https://i.imgur.com/6j0uHy0.png",
				MinAnchor = "0.9519 0.919",
				MaxAnchor = "0.999 0.998"
            };
            SaveConfig(configData);
			timer.Once(0.1f, ()=> SaveConfig(configData));
        }        
		
        private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
		
        #endregion
		
    }
}
