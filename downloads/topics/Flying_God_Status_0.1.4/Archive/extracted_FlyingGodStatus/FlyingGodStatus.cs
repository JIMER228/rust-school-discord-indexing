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
*  Codefling plugin page: https://codefling.com/plugins/flying-god-status
*  Codefling license: https://codefling.com/plugins/flying-god-status?tab=downloads_field_4
*  
*  Lone.Design plugin page: https://lone.design/product/flying-god-status/
*
*  Copyright © 2024 IIIaKa
*/

using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
	[Info("Flying God Status", "IIIaKa", "0.1.4")]
	[Description("The plugin displays godmode and noclip indicators in the status bar. Depends on AdvancedStatus plugin.")]
	class FlyingGodStatus : RustPlugin
	{
		[PluginReference]
		private Plugin ImageLibrary, Godmode, AdvancedStatus;
		
		#region ~Variables~
		private bool _imgLibIsLoaded = false;
		private const string StatusCreateBar = "CreateBar", StatusUpdateContent = "UpdateContent", StatusDeleteBar = "DeleteBar";
		private readonly string[] HttpScheme = new string[2] { "http://", "https://" };
		private Dictionary<int, object> _godBar, _noclipBar;
		private Timer _timer;
		private HashSet<ulong> _gods = new HashSet<ulong>(), _noclips = new HashSet<ulong>(), _pluginGods = new HashSet<ulong>();
		#endregion

		#region ~Configuration~
		private static Configuration _config;

		private class Configuration
		{
			[JsonProperty(PropertyName = "Chat command")]
			public string Command = "fgs";
			
			[JsonProperty(PropertyName = "Is it worth enabling GameTips for messages?")]
			public bool GameTips_Enabled = true;
			
			[JsonProperty(PropertyName = "Check interval in seconds")]
            public float Check_Interval = 1f;
			
			[JsonProperty(PropertyName = "Status Bar Settings for God")]
			public BarSettings GodBar;
			
			[JsonProperty(PropertyName = "Status Bar Settings for Noclip")]
			public BarSettings NoclipBar;
			
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
			
			if (_config.GodBar == null)
				_config.GodBar = new BarSettings();
			if (_config.NoclipBar == null)
			{
				_config.NoclipBar = new BarSettings()
				{
					Main_Color = "#66A4D2",
					Image_Url = "https://i.imgur.com/LY0AUMG.png",
                    Image_Local = "FlyingGodStatus_Noclip",
					Image_Color = "#31648B",
					Text_Key = "MsgNoclip"
				};
			}
			
			_config.GodBar.BarId = $"{Name}_God";
			_godBar = PrepareStatusBar(_config.GodBar);
			
			_config.NoclipBar.BarId = $"{Name}_Noclip";
			_noclipBar = PrepareStatusBar(_config.NoclipBar);
			
			SaveConfig();
		}
		
		protected override void SaveConfig() => Config.WriteObject(_config);
		protected override void LoadDefaultConfig() => _config = new Configuration() { Version = Version };
		#endregion
		
		#region ~DataFile~
		private static StoredData _storedData;

		private class StoredData
		{
			[JsonProperty(PropertyName = "List of players.")]
			public Dictionary<ulong, PlayerData> PlayersList = new Dictionary<ulong, PlayerData>();
		}
		
		public class PlayerData
		{
			public bool ShowGod = true;
			public bool ShowNoclip = true;
		}
		
		private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, _storedData);
		#endregion
		
		#region ~Language~
		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["MsgGod"] = "You are immortal",
				["MsgNoclip"] = "You are flying",
				["MsgGodEnabled"] = "Display of Godmode bar enabled!",
				["MsgGodDisabled"] = "Display of Godmode bar disabled!",
				["MsgNoclipEnabled"] = "Display of Noclip bar enabled!",
				["MsgNoclipDisabled"] = "Display of Noclip bar disabled!"
			}, this);
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["MsgGod"] = "Вы неуязвимы",
				["MsgNoclip"] = "Вы в полете",
				["MsgGodEnabled"] = "Отображение Godmode бара включено!",
				["MsgGodDisabled"] = "Отображение Godmode бара выключено!",
				["MsgNoclipEnabled"] = "Отображение Noclip бара включено!",
				["MsgNoclipDisabled"] = "Отображение Noclip бара выключено!"
			}, this, "ru");
		}
        #endregion

        #region ~Methods~
		private void LoadImages()
        {
			if (string.IsNullOrWhiteSpace(_config.GodBar.Image_Sprite) && string.IsNullOrWhiteSpace(_config.GodBar.Image_Local) && _config.GodBar.Image_Url.StartsWithAny(HttpScheme))
                ImageLibrary?.Call("AddImage", _config.GodBar.Image_Url, _config.GodBar.BarId, 0uL);
			if (string.IsNullOrWhiteSpace(_config.GodBar.Image_Sprite) && string.IsNullOrWhiteSpace(_config.GodBar.Image_Local) && _config.GodBar.Image_Url.StartsWithAny(HttpScheme))
                ImageLibrary?.Call("AddImage", _config.GodBar.Image_Url, _config.GodBar.BarId, 0uL);
		}
		
		private Dictionary<int, object> PrepareStatusBar(BarSettings barSettings)
        {
            var result = new Dictionary<int, object>
            {
                { 0, barSettings.BarId },
                { 1, Name },
				{ 4, barSettings.Order },
                { 5, barSettings.Height },
                { 6, barSettings.Main_Color },
                { 11, barSettings.Image_IsRawImage },
                { 12, barSettings.Image_Color },
                { 16, barSettings.Text_Size },
                { 17, barSettings.Text_Color },
                { 18, barSettings.Text_Font }
            };

            if (barSettings.Main_Color.StartsWith("#"))
                result.Add(-6, barSettings.Main_Transparency);
            if (!string.IsNullOrWhiteSpace(barSettings.Main_Material))
                result.Add(7, barSettings.Main_Material);
            if (barSettings.Image_Color.StartsWith("#"))
                result.Add(-12, barSettings.Image_Transparency);
            if (barSettings.Image_Outline_Enabled)
            {
                result.Add(13, barSettings.Image_Outline_Color);
                if (barSettings.Image_Outline_Color.StartsWith("#"))
                    result.Add(-13, barSettings.Image_Outline_Transparency);
                result.Add(14, barSettings.Image_Outline_Distance);
            }
            if (barSettings.Text_Offset_Horizontal != 0)
                result.Add(19, barSettings.Text_Offset_Horizontal);
            if (barSettings.Text_Outline_Enabled)
            {
                result.Add(20, barSettings.Text_Outline_Color);
                if (barSettings.Text_Outline_Color.StartsWith("#"))
                    result.Add(-20, barSettings.Text_Outline_Transparency);
                result.Add(21, barSettings.Text_Outline_Distance);
            }
			
			return result;
		}
		
		private void SelectBarImage(Dictionary<int, object> target, BarSettings barSettings)
        {
            target.Remove(10);
            target.Remove(9);
            target.Remove(8);
            if (!string.IsNullOrWhiteSpace(barSettings.Image_Sprite))
                target.Add(10, barSettings.Image_Sprite);
            else if (!string.IsNullOrWhiteSpace(barSettings.Image_Local))
                target.Add(9, barSettings.Image_Local);
            else
                target.Add(8, _imgLibIsLoaded && barSettings.Image_Url.StartsWithAny(HttpScheme) ? barSettings.BarId : barSettings.Image_Url);
        }
		
		private void SendBar(BasePlayer player, Dictionary<int, object> source, string textKey) => AdvancedStatus?.Call(StatusCreateBar, player.userID.Get(), new Dictionary<int, object>(source) { { 15, lang.GetMessage(textKey, this, player.UserIDString) } });
		private void UpdateText(ulong userID, string barId, string text) => AdvancedStatus?.Call(StatusUpdateContent, userID, new Dictionary<int, object> { { 0, barId }, { 1, Name }, { 15, text } });
		
		private void CheckPlayers()
		{
			foreach (var player in BasePlayer.activePlayerList)
            {
				if (!player.userID.IsSteamId()) continue;
				_storedData.PlayersList.TryGetValue(player.userID, out var playerData);
				if (playerData == null || playerData.ShowGod)
                {
					if (_pluginGods.Contains(player.userID) || player.IsGod())
					{
						if (_gods.Add(player.userID))
							SendBar(player, _godBar, _config.GodBar.Text_Key);
					}
					else if (_gods.Remove(player.userID))
						AdvancedStatus?.Call(StatusDeleteBar, player.userID.Get(), _config.GodBar.BarId, Name);
				}
				if (playerData == null || playerData.ShowNoclip)
                {
					if (player.IsFlying)
					{
						if (_noclips.Add(player.userID))
							SendBar(player, _noclipBar, _config.NoclipBar.Text_Key);
					}
					else if (_noclips.Remove(player.userID))
						AdvancedStatus?.Call(StatusDeleteBar, player.userID.Get(), _config.NoclipBar.BarId, Name);
				}
			}
		}
		
		private void ToggleImageLib(bool isLoaded)
        {
            _imgLibIsLoaded = isLoaded;
            if (_imgLibIsLoaded)
                LoadImages();
			SelectBarImage(_godBar, _config.GodBar);
			SelectBarImage(_noclipBar, _config.NoclipBar);
		}
		
		private void InitGodmode()
        {
			_pluginGods.Clear();
			var gods = Godmode?.Call("AllGods") as string[];
            if (gods != null)
            {
                for (int i = 0; i < gods.Length; i++)
                    OnGodmodeToggled(gods[i], true);
            }
        }
		#endregion

        #region ~Hooks~
        void OnPlayerDisconnected(BasePlayer player)
        {
			_gods.Remove(player.userID);
			_noclips.Remove(player.userID);
		}
		
		void OnPlayerLanguageChanged(BasePlayer player, string key)
		{
			if (_gods.Contains(player.userID))
				UpdateText(player.userID, _config.GodBar.BarId, lang.GetMessage(_config.GodBar.Text_Key, this, player.UserIDString));
			if (_noclips.Contains(player.userID))
				UpdateText(player.userID, _config.NoclipBar.BarId, lang.GetMessage(_config.NoclipBar.Text_Key, this, player.UserIDString));
		}

		void OnGodmodeToggled(string userIDStr, bool isEnabled)
		{
			if (ulong.TryParse(userIDStr, out var userID))
			{
				if (isEnabled)
					_pluginGods.Add(userID);
				else
					_pluginGods.Remove(userID);
			}
		}
		
		void OnAdvancedStatusLoaded()
		{
            if (!string.IsNullOrWhiteSpace(_config.GodBar.Image_Local))
                AdvancedStatus?.Call("LoadImage", _config.GodBar.Image_Local);
            if (!string.IsNullOrWhiteSpace(_config.NoclipBar.Image_Local))
                AdvancedStatus?.Call("LoadImage", _config.NoclipBar.Image_Local);
            SelectBarImage(_godBar, _config.GodBar);
            SelectBarImage(_noclipBar, _config.NoclipBar);
            _timer = timer.Every(_config.Check_Interval, () => { CheckPlayers(); });
            Subscribe(nameof(OnPlayerDisconnected));
            Subscribe(nameof(OnPlayerLanguageChanged));
        }
		
		void OnPluginLoaded(Plugin plugin)
		{
			if (plugin == ImageLibrary)
				ToggleImageLib(true);
			else if (plugin == Godmode)
				InitGodmode();
		}

		void OnPluginUnloaded(Plugin plugin)
		{
			if (plugin.Name == "ImageLibrary")
				ToggleImageLib(false);
			else if (plugin.Name == "AdvancedStatus")
			{
				Unsubscribe(nameof(OnPlayerDisconnected));
				Unsubscribe(nameof(OnPlayerLanguageChanged));
				if (_timer != null)
					_timer.Destroy();
				_gods.Clear();
				_noclips.Clear();
            }
			else if (plugin.Name == "Godmode")
				_pluginGods.Clear();
		}

		void Init()
        {
			Unsubscribe(nameof(OnPluginLoaded));
			Unsubscribe(nameof(OnPluginUnloaded));
			Unsubscribe(nameof(OnPlayerDisconnected));
			Unsubscribe(nameof(OnPlayerLanguageChanged));
			Unsubscribe(nameof(OnAdvancedStatusLoaded));
			AddCovalenceCommand(_config.Command, nameof(FlyingGodStatus_Command));
			_storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
		}

		void OnServerInitialized()
        {
			_imgLibIsLoaded = ImageLibrary != null && ImageLibrary.IsLoaded;
			if (_imgLibIsLoaded)
				LoadImages();
			if (AdvancedStatus != null && AdvancedStatus?.Call("IsReady") != null)
				OnAdvancedStatusLoaded();
			InitGodmode();
			Subscribe(nameof(OnPluginLoaded));
			Subscribe(nameof(OnPluginUnloaded));
			Subscribe(nameof(OnAdvancedStatusLoaded));
		}
		#endregion
		
		#region ~Commands~
		private void FlyingGodStatus_Command(IPlayer player, string command, string[] args)
		{
			if (player.Object is not BasePlayer bPlayer || args == null || args.Length < 1) return;
			if (!_storedData.PlayersList.TryGetValue(bPlayer.userID, out var playerData))
				_storedData.PlayersList[bPlayer.userID] = playerData = new PlayerData();
			
			string replyKey = string.Empty;
			bool isWarning = false;
			if (args[0] == "god")
			{
				playerData.ShowGod = !playerData.ShowGod;
				if (!playerData.ShowGod && _gods.Remove(bPlayer.userID))
					AdvancedStatus?.Call(StatusDeleteBar, bPlayer.userID.Get(), _config.GodBar.BarId, Name);
				replyKey = playerData.ShowGod ? "MsgGodEnabled" : "MsgGodDisabled";
				isWarning = !playerData.ShowGod;
			}
			else if (args[0] == "fly")
			{
				playerData.ShowNoclip = !playerData.ShowNoclip;
				if (!playerData.ShowNoclip && _noclips.Remove(bPlayer.userID))
					AdvancedStatus?.Call(StatusDeleteBar, bPlayer.userID.Get(), _config.NoclipBar.BarId, Name);
				replyKey = playerData.ShowNoclip ? "MsgNoclipEnabled" : "MsgNoclipDisabled";
				isWarning = !playerData.ShowNoclip;
			}

			if (!string.IsNullOrWhiteSpace(replyKey))
			{
				if (!player.IsServer && _config.GameTips_Enabled)
					player.Command("gametip.showtoast", (int)(isWarning ? GameTip.Styles.Error : GameTip.Styles.Blue_Long), lang.GetMessage(replyKey, this, player.Id), string.Empty);
				else
					player.Reply(lang.GetMessage(replyKey, this, player.Id));
			}
		}
		#endregion

		#region ~Unload~
		void Unload()
		{
			if (_timer != null)
				_timer.Destroy();
			SaveData();
			_config = null;
			_storedData = null;
		}
        #endregion

        #region ~Classes~
		public class BarSettings
        {
			[JsonIgnore] public string BarId { get; set; }
			
			public int Order { get; set; } = 20;
            public int Height { get; set; } = 26;

            [JsonProperty(PropertyName = "Main_Color(Hex or RGBA)")]
            public string Main_Color { get; set; } = "#E3BA2B";

            public float Main_Transparency { get; set; } = 0.8f;
            public string Main_Material { get; set; } = string.Empty;
            public string Image_Url { get; set; } = "https://i.imgur.com/XmZBOuP.png";

            [JsonProperty(PropertyName = "Image_Local(Leave empty to use Image_Url)")]
            public string Image_Local { get; set; } = "FlyingGodStatus_God";

            [JsonProperty(PropertyName = "Image_Sprite(Leave empty to use Image_Local or Image_Url)")]
            public string Image_Sprite { get; set; } = string.Empty;

            public bool Image_IsRawImage { get; set; } = false;

            [JsonProperty(PropertyName = "Image_Color(Hex or RGBA)")]
            public string Image_Color { get; set; } = "#FFD33A";

            public float Image_Transparency { get; set; } = 1f;

            [JsonProperty(PropertyName = "Is it worth enabling an outline for the image?")]
            public bool Image_Outline_Enabled { get; set; }

            [JsonProperty(PropertyName = "Image_Outline_Color(Hex or RGBA)")]
            public string Image_Outline_Color { get; set; } = "0.1 0.3 0.8 0.9";

            public float Image_Outline_Transparency { get; set; } = 1f;
            public string Image_Outline_Distance { get; set; } = "0.75 0.75";
			public string Text_Key { get; set; } = "MsgGod";
			public int Text_Size { get; set; } = 12;

            [JsonProperty(PropertyName = "Text_Color(Hex or RGBA)")]
            public string Text_Color { get; set; } = "1 1 1 1";

            [JsonProperty(PropertyName = "Text_Font(https://umod.org/guides/rust/basic-concepts-of-gui#fonts)")]
            public string Text_Font { get; set; } = "RobotoCondensed-Bold.ttf";

            public int Text_Offset_Horizontal { get; set; } = 0;

            [JsonProperty(PropertyName = "Is it worth enabling an outline for the text?")]
            public bool Text_Outline_Enabled { get; set; }

            [JsonProperty(PropertyName = "Text_Outline_Color(Hex or RGBA)")]
            public string Text_Outline_Color { get; set; } = "#000000";

            public float Text_Outline_Transparency { get; set; } = 1f;
            public string Text_Outline_Distance { get; set; } = "0.75 0.75";

            [JsonProperty(PropertyName = "SubText(Leave empty to disable)")]
            public string SubText { get; set; } = string.Empty;

            public int SubText_Size { get; set; } = 12;

            [JsonProperty(PropertyName = "SubText_Color(Hex or RGBA)")]
            public string SubText_Color { get; set; } = "1 1 1 1";

            public string SubText_Font { get; set; } = "RobotoCondensed-Bold.ttf";

            [JsonProperty(PropertyName = "Is it worth enabling an outline for the sub text?")]
            public bool SubText_Outline_Enabled { get; set; }

            [JsonProperty(PropertyName = "SubText_Outline_Color(Hex or RGBA)")]
            public string SubText_Outline_Color { get; set; } = "0.5 0.6 0.7 0.5";

            public float SubText_Outline_Transparency { get; set; } = 1f;
            public string SubText_Outline_Distance { get; set; } = "0.75 0.75";
		}
        #endregion
	}
} 