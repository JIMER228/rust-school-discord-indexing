using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
	[Info("Custom Cassette", "Menevt", "0.0.1")]
	[Description("Загрузка своего звука в касету :3")]
	class CustomCassette : RustPlugin
	{
		private static readonly String perm = "CustomCassette.use";
		void Init() => permission.RegisterPermission(perm, this);

		#region Language
		protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                ["InvalidSyntax"] = "Invalid syntax!\n /сassette [YouTube link] [Volume (0.1 - 1.5)]",
                ["ItemReceived"] = "You have successfully received your audio cassette!",
				["LongVideo"] = "You need to provide a link to a video that is no more than 5 minutes!",
				["InvalidLink"] = "Invalid link! You must provide a direct link to the YouTube video",
				["UnknownError"] = "Something went wrong, please try again or use another link!",
				["NoPermission"] = "You do not have sufficient rights to use this command!",
				["ServerResponse"] = "Waiting for a server response...",
			}, this);

            lang.RegisterMessages(new Dictionary<string, string>()
            {
                ["InvalidSyntax"] = "Неверный синтаксис!\n /сassette [YouTube link] [Громкость (0.1 - 1.5)]",
                ["ItemReceived"] = "Вы успешно получили кассету с вашим звуком!",
                ["LongVideo"] = "Нужно указать ссылку на видео которое не более 5 минут!",
                ["InvalidLink"] = "Неверная ссылка! Вы должны указать прямую ссылку на видео с YouTube",
                ["UnknownError"] = "Что-то пошло не так, попробуйте еще раз или используйте другую ссылку!",
                ["NoPermission"] = "У вас недостаточно прав для использования этой комманды!",
                ["ServerResponse"] = "Ожидаем ответа сервера...",
            }, this, "ru");
        }

        private string _(BasePlayer player, string key, params object[] args)
        {
            return string.Format(lang.GetMessage(key, this, player?.UserIDString), args);
        }
        #endregion

        #region Command

        [ChatCommand("cassette")]
		void cmd_сassette(BasePlayer player, String command, String[] args)
		{
			if (player == null)
				return;
			if (!permission.UserHasPermission(player.UserIDString, perm))
			{
				SendReply(player, _(player, "NoPermission"));
				return;
			}

			if (args == null || args.Length < 1)
			{
				SendReply(player, _(player, "InvalidSyntax"));
				return;
			}
			SendReply(player, _(player, "ServerResponse"));
			Cassette(player, args[0], args.Length != 2 ? 0f : Single.Parse(args[1]));
		}
        #endregion

        #region Metods
        private void Cassette(BasePlayer player, String url, Single volume = 1.0f)
		{
			if (volume == 0f)
				volume = 1.0f;

			webrequest.Enqueue($"https://api.skyplugins.ru/api/getsoundogg?url={url}&volume={volume}", "", (code, response) =>
			{
				switch (code)
				{
					case 200:
						{
							Item item = ItemManager.CreateByName("cassette", 1, 0);
							if (item != null)
							{
								Cassette cassette = ItemModAssociatedEntity<Cassette>.GetAssociatedEntity(item, true);
								if (cassette != null)
								{
									cassette.MaxCassetteLength = 35f;
									byte[] data = Convert.FromBase64String(response);
									uint id = FileStorage.server.Store(data, FileStorage.Type.ogg, cassette.net.ID, 0U);
									cassette.SetAudioId(id, player.userID);
								}
								player.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
								SendReply(player, _(player, "ItemReceived"));
							}
							break;
						}
					case 0:
						{
							PrintError($"Time out waiting for API #2202");
							break;
						}
					case 500:
						{
							if (response == "long")
							{
								SendReply(player, _(player, "LongVideo"));
								return;
							}
							if (response == "wrong-url")
							{
								SendReply(player, _(player, "InvalidLink"));
								return;
							}
							SendReply(player, _(player, "UnknownError"));
							break;
						}
				}
			},this);
		}
        #endregion
    }
}
