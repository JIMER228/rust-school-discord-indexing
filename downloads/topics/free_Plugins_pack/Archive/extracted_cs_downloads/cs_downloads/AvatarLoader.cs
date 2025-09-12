using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Oxide.Plugins
{
	[Info("AvatarLoader", "MaltrzD", "0.0.1")]
	class AvatarLoader : RustPlugin
	{
		private readonly Regex _avatarRegex = new Regex(@"<avatarFull><!\[CDATA\[(.*)\]\]></avatarFull>", RegexOptions.Compiled);
		[PluginReference] private Plugin ImageLibrary;

		private void OnServerInitialized()
		{
			foreach (var item in BasePlayer.activePlayerList)
			{
				OnPlayerConnected(item);
			}
		}
		public bool HasImage(String imageName) => (bool)ImageLibrary?.Call("HasImage", imageName);

		private void OnPlayerConnected(BasePlayer player) => SteamAvatarAdd(player.UserIDString);

		private void SteamAvatarAdd(String userid)
		{
			if (ImageLibrary == null) return;
			if (HasImage(userid)) return;

			webrequest.Enqueue($"https://steamcommunity.com/profiles/{userid}?xml=1", null,
				(code, response) =>
				{
					if (response == null || code != 200)
						return;

					string avatarUrl = _avatarRegex.Match(response).Groups[1].ToString();
					if (!string.IsNullOrEmpty(avatarUrl))
					{
						ImageLibrary?.Call("AddImage", avatarUrl, userid);
					}
				}, this);
		}
	}
}
