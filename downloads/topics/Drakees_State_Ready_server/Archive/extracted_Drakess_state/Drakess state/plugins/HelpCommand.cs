
namespace Oxide.Plugins
{
    [Info("HelpCommand", "Vokidu", "1.0.2")]
    public class HelpCommand: RustPlugin
    {
		private static ConfigData cfg;		

		private class ConfigData
        {
            public string[] Help;
            public Hash<string, string> PrivHelp;
            public Hash<string, string[]> CustomCommands;
        }
		
		protected override void LoadDefaultConfig()
        {			
			cfg = new ConfigData
            {
                Help = new string[]{
					"За помощью к админам.",
					"Тут нет помощи.",
					"Вообще нет помощи.\nРешайте свои проблемы сами."
				},
                PrivHelp = new Hash<string, string>{
					["backpacks.use"] = "<color=#f1c40f>> bind b backpack.open</color> - Бинд на открытие рюкзака.\n<color=#f1c40f>/backpack - открыть рюкзак.</color>",
					["skins.change"] = "<color=#f1c40f>/skin - Открыть окно смены скинов.</color>"
				},
				CustomCommands = new Hash<string, string[]>{
					["rules"] = new string[]{
						"Правила сервера:",
						"- Запрещены спам, флуд, реклама, оскорбления и т.д.",
						"- Запрещено использование стороннего ПО и багов игры",
						"- На вашем ПК должен быть установлен Skype (Вас могут -вызвать на проверку в абсолютно любой момент)",
						"Будьте адекватными!"
					}
				}
                
            };
            Config.WriteObject(cfg, true);
        }
		
		private void Init()
        {
            cfg = Config.ReadObject<ConfigData>();
			if(cfg.CustomCommands == null){
				cfg.CustomCommands = new Hash<string, string[]>{
					["rules"] = new string[]{
						"Правила сервера:",
						"- Запрещены спам, флуд, реклама, оскорбления и т.д.",
						"- Запрещено использование стороннего ПО и багов игры",
						"- На вашем ПК должен быть установлен Skype (Вас могут -вызвать на проверку в абсолютно любой момент)",
						"Будьте адекватными!"
					}
				};
				Config.WriteObject(cfg, true);
			}
			foreach(var command in cfg.CustomCommands)
				cmd.AddChatCommand(command.Key.ToLower(), this, "CustomCommand");
		}
		
		[ChatCommand("help")]
        private void PrintHelp(BasePlayer player, string command, string[] args)
        {
			foreach(var help in cfg.Help) {
				SendReply(player, help);
			}
			var PayHelp = "ВИП КОМАНДЫ:";
			foreach(var help in cfg.PrivHelp) {
				if(permission.UserHasPermission(player.UserIDString, help.Key))
				PayHelp = PayHelp+"\n"+help.Value;
			}
			SendReply(player, PayHelp);
		}
		
        private void CustomCommand(BasePlayer player, string command, string[] args)
        {
			foreach(var str in cfg.CustomCommands[command.ToLower()])
				SendReply(player, str);
		}
    }
	
}