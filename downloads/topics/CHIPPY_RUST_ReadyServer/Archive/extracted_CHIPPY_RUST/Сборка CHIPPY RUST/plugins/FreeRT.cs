using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Free RT", "Menevt", "0.1.5")]
    [Description("Allows players to open card locked doors inside Rad Towns by knocking.")]
    class FreeRT : CovalencePlugin
    {
		#region Variables
        private const string PERMISSION_ALL = "freert.all";
        private const string PERMISSION_GREEN = "freert.green";
        private const string PERMISSION_BLUE = "freert.blue";
        private const string PERMISSION_RED = "freert.red";

        public string GreenDoor = "door.hinged.security.green";
        public string BlueDoor = "door.hinged.security.blue";
        public string RedDoor = "door.hinged.security.red";
		#endregion
		
        private void Init()
        {
            permission.RegisterPermission(PERMISSION_ALL, this);
            permission.RegisterPermission(PERMISSION_GREEN, this);
            permission.RegisterPermission(PERMISSION_BLUE, this);
            permission.RegisterPermission(PERMISSION_RED, this);
		}
		
		
		#region Configuration
		private static Configuration _config;

		private class Configuration
		{
			[JsonProperty(PropertyName = "Close Time")]
			public float CloseTime = 2;

			[JsonProperty(PropertyName = "Show NotAllowed Message")]
			public bool ShowNotAllowedMessage = true;
		}

		protected override void LoadConfig()
		{
			base.LoadConfig();
			try
			{
				_config = Config.ReadObject<Configuration>();
				if (_config == null) throw new Exception();
				SaveConfig();
			}
			catch
			{
				PrintError("Your configuration file contains an error. Using default configuration values.");
				LoadDefaultConfig();
			}
		}

		protected override void SaveConfig() => Config.WriteObject(_config);

		protected override void LoadDefaultConfig() => _config = new Configuration();
		#endregion
		
		#region Language
		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
			["NotAllowed"] = "You do not have permission to open this door without the card!"
			}, this);
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["NotAllowed"] = "У вас недостаточно прав для открытия этой двери без карточки!"
			}, this, "ru");
		}
		#endregion
		
		#region Methods
		private void OpenDoor(Door door)
        {
	        door.SetFlag(BaseEntity.Flags.Open, true);
	        timer.Once(_config.CloseTime, () =>
	        {
		        door.SetFlag(BaseEntity.Flags.Open, false);
	        });
        }
		#endregion
		
		#region OpenDoors
		void OnDoorKnocked(Door door, BasePlayer player)
		{
			if(!(door.ShortPrefabName == GreenDoor || door.ShortPrefabName == BlueDoor || door.ShortPrefabName == RedDoor))
				return;
			if(permission.UserHasPermission(player.UserIDString, PERMISSION_ALL))
			{
				OpenDoor(door);
				return;
			}

			if (door.ShortPrefabName == GreenDoor && permission.UserHasPermission(player.UserIDString, PERMISSION_GREEN))
			{
				OpenDoor(door);
				return;
			}

			if (door.ShortPrefabName == BlueDoor && permission.UserHasPermission(player.UserIDString, PERMISSION_BLUE))
			{
				OpenDoor(door);
				return;
			}

			if (door.ShortPrefabName == RedDoor && permission.UserHasPermission(player.UserIDString, PERMISSION_RED))
			{
				OpenDoor(door);
				return;
			}
			
			if(_config.ShowNotAllowedMessage)
				player.ChatMessage(lang.GetMessage("NotAllowed", this, player.UserIDString));
		}
		#endregion

		#region Unload

		void Unload()
		{
			_config = null;
		}

		#endregion
	}
}