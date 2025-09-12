using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using System;
using System.Linq;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Plugins;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("XNickController", "rustmods.ru", "1.0.0")]
    public class XNickController : RustPlugin
    {
		
		void OnPlayerConnected(BasePlayer player)
		{
			if (player.IsReceivingSnapshot)
            {
                NextTick(() => OnPlayerConnected(player));
                return;
            }
			
			foreach (var symbol in config.Symbol.SymbolList)
			{
				if (player.displayName.Contains(symbol))
				{
					player.displayName = player.displayName.Replace(symbol, "");
				}
			}
		}
        protected override void LoadConfig()
        {
            base.LoadConfig();

            config = Config.ReadObject<NickConfig>();
        }

        		
				
		private void OnServerInitialized()
		{
			PrintWarning("\n-----------------------------\n" +
			"     Forum - RustMods.ru\n" +
			"-----------------------------");
			
			foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
				foreach (var symbol in config.Symbol.SymbolList)
			    {
				    if (player.displayName.Contains(symbol))
				    {
					    player.displayName = player.displayName.Replace(symbol, "");
				    }
			    }
            }
		}
		
        private NickConfig config;
        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }
		   		 		  						  	   		  	   		  	 				  		 			  	 		
        private class NickConfig
        {		
            
			[JsonProperty("Список символов/слов которые нужно удалять из ника")]
            public SymbolSetting Symbol = new SymbolSetting();										
			internal class SymbolSetting
            {
                [JsonProperty("?_?")]
                public List<string> SymbolList = new List<string>();           				
            }			
			
			public static NickConfig GetNewConfiguration()
            {
                return new NickConfig
                {
					Symbol = new SymbolSetting
					{
						SymbolList = new List<string>
						{
							"#XRUST",
							"#LALARUST",
							"#XRUST RUST",
							".ua",
							".ru",
							".com"
						}
					},
				};
			}
        }
		   		 		  						  	   		  	   		  	 				  		 			  	 		
        protected override void LoadDefaultConfig()
        {
            config = NickConfig.GetNewConfiguration();

            PrintWarning("Создание начальной конфигурации плагина!!!");
        }
		
			}
}
