/*
*  < ----- End-User License Agreement ----->
*  
*  You may not copy, modify, merge, publish, distribute, sublicense, or sell copies of this software without the developer’s consent.
*
*  THIS SOFTWARE IS PROVIDED BY IIIaKa AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, 
*  THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS 
*  BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE 
*  GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT 
*  LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*
*  Developer: IIIaKa
*      https://t.me/iiiaka
*      Discord: @iiiaka
*      https://github.com/IIIaKa
*      https://umod.org/user/IIIaKa
*      https://codefling.com/iiiaka
*      https://lone.design/vendor/iiiaka/
*      https://www.patreon.com/iiiaka
*      https://boosty.to/iiiaka
*  Codefling plugin page: https://codefling.com/plugins/vanish-status
*  Codefling license: https://codefling.com/plugins/vanish-status?tab=downloads_field_4
*  
*  Lone.Design plugin page: https://lone.design/product/vanish-status/
*
*  Copyright © 2023-2025 IIIaKa
*/

using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Core;

namespace Oxide.Plugins
{
	[Info("Vanish Status", "IIIaKa", "0.1.7")]
	[Description("The plugin displays an invisibility indication in the status bar. Depends on AdvancedStatus plugin.")]
	class VanishStatus : RustPlugin
	{
		[PluginReference]
		private Plugin ImageLibrary, AdvancedStatus;

		#region ~Variables~
		private bool _imgLibIsLoaded = false, _vanishEffect = false, _reappearEffect = false;
		private const string BarID = "VanishStatus_Vanish", Str_Invis = "invis", StatusCreateBar = "CreateBar", StatusUpdateContent = "UpdateContent", StatusDeleteBar = "DeleteBar";
		private readonly string[] HttpScheme = new string[2] { "http://", "https://" };
		private Dictionary<int, object> _statusBar;
		#endregion

        #region ~Configuration~
        private static Configuration _config;

		private class Configuration
        {
			[JsonProperty(PropertyName = "Sound effect played upon disappearance. An empty string disables the effect")]
			public string VanishEffect = "assets/prefabs/npc/patrol helicopter/effects/rocket_fire.prefab";
			
			[JsonProperty(PropertyName = "Sound effect played upon appearance. An empty string disables the effect")]
			public string ReappearEffect = "assets/prefabs/npc/patrol helicopter/effects/rocket_fire.prefab";
			
			[JsonProperty(PropertyName = "Status. Bar - Height")]
            public int Status_Bar_Height = 26;

            [JsonProperty(PropertyName = "Status. Bar - Order")]
            public int Status_Bar_Order = 10;

            [JsonProperty(PropertyName = "Status. Background - Color(Hex or RGBA)")]
            public string Status_Main_Color = "#15AC9D";

            [JsonProperty(PropertyName = "Status. Background - Transparency")]
            public float Status_Main_Transparency = 0.7f;

            [JsonProperty(PropertyName = "Status. Background - Material(empty to disable)")]
            public string Status_Main_Material = string.Empty;

            [JsonProperty(PropertyName = "Status. Image - Url")]
            public string Status_Image_Url = "https://i.imgur.com/3D1JIaU.png";

            [JsonProperty(PropertyName = "Status. Image - Local(Leave empty to use Image_Url)")]
            public string Status_Image_Local = "VanishStatus_Vanish";

            [JsonProperty(PropertyName = "Status. Image - Sprite(Leave empty to use Image_Local or Image_Url)")]
            public string Status_Image_Sprite = string.Empty;

            [JsonProperty(PropertyName = "Status. Image - Is raw image")]
            public bool Status_Image_IsRawImage = false;

            [JsonProperty(PropertyName = "Status. Image - Color(Hex or RGBA)")]
            public string Status_Image_Color = "#15AC9D";

            [JsonProperty(PropertyName = "Status. Image - Transparency")]
            public float Status_Image_Transparency = 1f;
			
			[JsonProperty(PropertyName = "Status. Image Outline - Is it worth enabling an outline for the image?")]
			public bool Status_Image_Outline_Enabled = false;
			
			[JsonProperty(PropertyName = "Status. Image Outline - Color(Hex or RGBA)")]
			public string Status_Image_Outline_Color = "0.1 0.3 0.8 0.9";
			
			[JsonProperty(PropertyName = "Status. Image Outline - Transparency")]
			public float Status_Image_Outline_Transparency = 1f;
			
			[JsonProperty(PropertyName = "Status. Image Outline - Distance")]
            public string Status_Image_Outline_Distance = "0.75 0.75";
			
			[JsonProperty(PropertyName = "Status. Text - Size")]
            public int Status_Text_Size = 12;

            [JsonProperty(PropertyName = "Status. Text - Color(Hex or RGBA)")]
            public string Status_Text_Color = "#FFFFFF";

            [JsonProperty(PropertyName = "Status. Text - Font(https://umod.org/guides/rust/basic-concepts-of-gui#fonts)")]
            public string Status_Text_Font = "RobotoCondensed-Bold.ttf";
			
			[JsonProperty(PropertyName = "Status. Text - Offset Horizontal")]
			public int Status_Text_Offset_Horizontal = 0;

			[JsonProperty(PropertyName = "Status. Text Outline - Is it worth enabling an outline for the text?")]
			public bool Status_Text_Outline_Enabled = false;
			
			[JsonProperty(PropertyName = "Status. Text Outline - Color(Hex or RGBA)")]
			public string Status_Text_Outline_Color = "#000000";
			
			[JsonProperty(PropertyName = "Status. Text Outline - Transparency")]
			public float Status_Text_Outline_Transparency = 1f;
			
			[JsonProperty(PropertyName = "Status. Text Outline - Distance")]
			public string Status_Text_Outline_Distance = "0.75 0.75";
			
			[JsonProperty(PropertyName = "Status. SubText - Size")]
            public int Status_SubText_Size = 12;

            [JsonProperty(PropertyName = "Status. SubText - Color(Hex or RGBA)")]
            public string Status_SubText_Color = "#FFFFFF";

            [JsonProperty(PropertyName = "Status. SubText - Font")]
            public string Status_SubText_Font = "RobotoCondensed-Bold.ttf";

			[JsonProperty(PropertyName = "Status. SubText Outline - Is it worth enabling an outline for the sub text?")]
			public bool Status_SubText_Outline_Enabled = false;
			
			[JsonProperty(PropertyName = "Status. SubText Outline - Color(Hex or RGBA)")]
			public string Status_SubText_Outline_Color = "0.5 0.6 0.7 0.5";
			
			[JsonProperty(PropertyName = "Status. SubText Outline - Transparency")]
			public float Status_SubText_Outline_Transparency = 1f;
			
			[JsonProperty(PropertyName = "Status. SubText Outline - Distance")]
			public string Status_SubText_Outline_Distance = "0.75 0.75";
			
			public Oxide.Core.VersionNumber Version;
		}
		
		protected override void LoadConfig()
		{
			base.LoadConfig();
			try { _config = Config.ReadObject<Configuration>(); }
			catch (Exception ex) { PrintError($"{ex.Message}\n\n[{Title}] Your configuration file contains an error."); }
			if (_config == null || _config.Version == new VersionNumber())
			{
				PrintWarning("The configuration file is not found or contains errors. Creating a new one...");
				LoadDefaultConfig();
			}
			else if (_config.Version < Version)
			{
				PrintWarning($"Your configuration file version({_config.Version}) is outdated. Updating it to {Version}.");
				_config.Version = Version;
				PrintWarning($"The configuration file has been successfully updated to version {_config.Version}!");
			}
			
			_statusBar = new Dictionary<int, object>
			{
                { 0, BarID },
                { 1, Name },
				{ 4, _config.Status_Bar_Order },
                { 5, _config.Status_Bar_Height },
                { 6, _config.Status_Main_Color },
                { 11, _config.Status_Image_IsRawImage },
				{ 16, _config.Status_Text_Size },
                { 17, _config.Status_Text_Color },
                { 18, _config.Status_Text_Font },
                { 23, _config.Status_SubText_Size },
                { 24, _config.Status_SubText_Color },
                { 25, _config.Status_SubText_Font }
			};
			
			if (_config.Status_Main_Color.StartsWith("#"))
				_statusBar.Add(-6, _config.Status_Main_Transparency);
			if (!string.IsNullOrWhiteSpace(_config.Status_Main_Material))
				_statusBar.Add(7, _config.Status_Main_Material);
			if (!_config.Status_Image_IsRawImage)
            {
                _statusBar.Add(12, _config.Status_Image_Color);
                if (_config.Status_Image_Color.StartsWith("#"))
					_statusBar.Add(-12, _config.Status_Image_Transparency);
			}
			if (_config.Status_Image_Outline_Enabled)
			{
				_statusBar.Add(13, _config.Status_Image_Outline_Color);
				if (_config.Status_Image_Outline_Color.StartsWith("#"))
					_statusBar.Add(-13, _config.Status_Image_Outline_Transparency);
				_statusBar.Add(14, _config.Status_Image_Outline_Distance);
			}
			if (_config.Status_Text_Offset_Horizontal != 0)
				_statusBar.Add(19, _config.Status_Text_Offset_Horizontal);
			if (_config.Status_Text_Outline_Enabled)
			{
				_statusBar.Add(20, _config.Status_Text_Outline_Color);
				if (_config.Status_Text_Outline_Color.StartsWith("#"))
					_statusBar.Add(-20, _config.Status_Text_Outline_Transparency);
				_statusBar.Add(21, _config.Status_Text_Outline_Distance);
			}
			if (_config.Status_SubText_Outline_Enabled)
			{
				_statusBar.Add(26, _config.Status_SubText_Outline_Color);
				if (_config.Status_SubText_Outline_Color.StartsWith("#"))
					_statusBar.Add(-26, _config.Status_SubText_Outline_Transparency);
				_statusBar.Add(27, _config.Status_SubText_Outline_Distance);
			}
			
			SaveConfig();
		}
		
		protected override void SaveConfig() => Config.WriteObject(_config);
		protected override void LoadDefaultConfig() => _config = new Configuration() { Version = Version };
		#endregion

		#region ~Language~
		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string> { ["MsgText"] = "You are invisible" }, this);
			lang.RegisterMessages(new Dictionary<string, string> { ["MsgText"] = "Вы невидимы" }, this, "ru");
		}
		#endregion

		#region ~Methods~
		private void LoadImages()
        {
			if (string.IsNullOrWhiteSpace(_config.Status_Image_Sprite) && string.IsNullOrWhiteSpace(_config.Status_Image_Local) && _config.Status_Image_Url.StartsWithAny(HttpScheme))
				ImageLibrary?.Call("AddImage", _config.Status_Image_Url, BarID, 0uL);
		}
		
		private void SelectBarImage()
		{
			_statusBar.Remove(10);
			_statusBar.Remove(9);
			_statusBar.Remove(8);
			if (!string.IsNullOrWhiteSpace(_config.Status_Image_Sprite))
				_statusBar.Add(10, _config.Status_Image_Sprite);
			else if (!string.IsNullOrWhiteSpace(_config.Status_Image_Local))
				_statusBar.Add(9, _config.Status_Image_Local);
			else
				_statusBar.Add(8, _imgLibIsLoaded && _config.Status_Image_Url.StartsWithAny(HttpScheme) ? BarID : _config.Status_Image_Url);
		}
		
		private void SendBar(BasePlayer player)
		{
			if (player.limitNetworking)
			{
				AdvancedStatus?.Call(StatusCreateBar, player.userID, new Dictionary<int, object>(_statusBar) { { 15, lang.GetMessage("MsgText", this, player.UserIDString) } });
				if (_vanishEffect)
					EffectNetwork.Send(new Effect(_config.VanishEffect, player.transform.position, Vector3.zero), player.Connection);
			}
		}
		
		private void ToggleImageLib(bool isLoaded)
        {
            _imgLibIsLoaded = isLoaded;
            if (_imgLibIsLoaded)
                LoadImages();
            SelectBarImage();
        }
		#endregion

        #region ~Oxide Hooks~
        void OnPlayerConnected(BasePlayer player) => SendBar(player);
		
		void OnPlayerLanguageChanged(BasePlayer player, string key)
		{
            if (!player.limitNetworking) return;
			var parameters = new Dictionary<int, object>
            {
                { 0, BarID },
                { 1, Name },
                { 15, lang.GetMessage("MsgText", this, player.UserIDString) }
            };
            AdvancedStatus?.Call(StatusUpdateContent, player.userID, parameters);
        }
		
		void OnPlayerVanish(BasePlayer player)
        {
			if (player.IsConnected)
				SendBar(player);
		}

        void OnPlayerUnvanish(BasePlayer player)
        {
			if (!player.limitNetworking)
			{
				AdvancedStatus?.Call(StatusDeleteBar, player.userID, BarID, Name);
				if (_reappearEffect)
					EffectNetwork.Send(new Effect(_config.ReappearEffect, player.transform.position, Vector3.zero), player.Connection);
			}
		}
		
		void OnVanishDisappear(BasePlayer player) => NextTick(() => OnPlayerVanish(player));
		void OnVanishReappear(BasePlayer player) => NextTick(() => OnPlayerUnvanish(player));
		
		void OnAdvancedStatusLoaded()
		{
			if (!string.IsNullOrWhiteSpace(_config.Status_Image_Local))
				AdvancedStatus?.Call("LoadImage", _config.Status_Image_Local);
			SelectBarImage();
			foreach (var player in BasePlayer.activePlayerList)
			{
				if (player.userID.IsSteamId())
					SendBar(player);
			}
			Subscribe(nameof(OnPlayerConnected));
            Subscribe(nameof(OnPlayerLanguageChanged));
			Subscribe(nameof(OnPlayerVanish));
			Subscribe(nameof(OnPlayerUnvanish));
			Subscribe(nameof(OnVanishDisappear));
			Subscribe(nameof(OnVanishReappear));
		}
		
		void OnPluginLoaded(Plugin plugin)
        {
			if (plugin == ImageLibrary)
				ToggleImageLib(true);
		}

		void OnPluginUnloaded(Plugin plugin)
		{
			if (plugin.Name == "ImageLibrary")
				ToggleImageLib(false);
			else if (plugin.Name == "AdvancedStatus")
			{
				Unsubscribe(nameof(OnPlayerConnected));
				Unsubscribe(nameof(OnPlayerLanguageChanged));
				Unsubscribe(nameof(OnPlayerVanish));
				Unsubscribe(nameof(OnPlayerUnvanish));
				Unsubscribe(nameof(OnVanishDisappear));
				Unsubscribe(nameof(OnVanishReappear));
			}
		}
		
		void Init()
		{
			Unsubscribe(nameof(OnPlayerConnected));
			Unsubscribe(nameof(OnPlayerLanguageChanged));
			Unsubscribe(nameof(OnPlayerVanish));
			Unsubscribe(nameof(OnPlayerUnvanish));
			Unsubscribe(nameof(OnVanishDisappear));
			Unsubscribe(nameof(OnVanishReappear));
			Unsubscribe(nameof(OnAdvancedStatusLoaded));
		}
		
		void OnServerInitialized(bool initial)
        {
			bool shouldSave = false;
			if (!string.IsNullOrWhiteSpace(_config.VanishEffect))
			{
				_vanishEffect = true;
				if (!StringPool.toNumber.ContainsKey(_config.VanishEffect))
				{
					PrintError($"Effect {_config.VanishEffect} not found. Default is being used.");
					_config.VanishEffect = "assets/prefabs/npc/patrol helicopter/effects/rocket_fire.prefab";
					shouldSave = true;
				}
			}
			if (!string.IsNullOrWhiteSpace(_config.ReappearEffect))
            {
				_reappearEffect = true;
				if (!StringPool.toNumber.ContainsKey(_config.ReappearEffect))
                {
                    PrintError($"Effect {_config.ReappearEffect} not found. Default is being used.");
                    _config.ReappearEffect = "assets/prefabs/npc/patrol helicopter/effects/rocket_fire.prefab";
                    shouldSave = true;
                }
            }
			if (shouldSave)
				SaveConfig();
			
			_imgLibIsLoaded = ImageLibrary != null && ImageLibrary.IsLoaded;
			if (_imgLibIsLoaded)
				LoadImages();
			if (AdvancedStatus != null && AdvancedStatus?.Call("IsReady") != null)
				OnAdvancedStatusLoaded();
			else
			{
				if (initial && AdvancedStatus != null)
					PrintWarning("AdvancedStatus plugin found, but not ready yet. Waiting for it to load...");
				else
					PrintWarning("AdvancedStatus plugin not found! To function, it is necessary to install it!\n* https://codefling.com/plugins/advanced-status\n* https://lone.design/product/advanced-status/");
            }
			Subscribe(nameof(OnAdvancedStatusLoaded));
		}
		
		void Unload() => _config = null;
		#endregion
	}
} 