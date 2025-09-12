using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Oxide.Game.Rust.Cui;
using System.Globalization;
using Oxide.Core;
using Oxide.Core.Plugins;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("MagicLogo", "Kirtne", "0.1.0")]
    public class MagicLogo : RustPlugin
    {
        private static MagicLogo _MagicLogo;
        
        #region Config
        private static Configuration config = new Configuration();
        private class Configuration
        {
	        [JsonProperty("Настройка контента меню")]
	        public Content Content_ = new Content();
	        
	        internal class Content
	        {
		        [JsonProperty("Ссылка на логотип")]
		        public string ImageLink;
		        
		        [JsonProperty("Имя плагина (имя плагина в котором мы будем вызывать хук ниже)")]
		        public string PluginName;

		        [JsonProperty("Хук плагина (при нажатии будет вызван он)")]
		        public string PluginHook;
                
                [JsonProperty("Если вы глупый, то воспользуйтесь командой (если вы все таки умный, оставьте пустым)")]
                public string Command;
	        }

	        public static Configuration GetNewConfiguration()
	        {
		        return new Configuration
		        {
			        Content_ = new Content
			        {
				        ImageLink = "",
				        PluginName = "",
				        PluginHook = "",
                        Command = ""
			        }
		        };
	        }
        }
	    
        protected override void LoadDefaultConfig() => config = Configuration.GetNewConfiguration();
        protected override void SaveConfig() => Config.WriteObject(config);
        
        protected override void LoadConfig()
        {
	        base.LoadConfig();
	        try
	        {
		        config = Config.ReadObject<Configuration>();
		        if (config == null) LoadDefaultConfig();
	        }
	        catch
	        {
		        PrintWarning("Ошибка #178" + $"чтения конфигурации 'oxide/config/{Name}', создаём новую конфигурацию! #33");
		        LoadDefaultConfig();
	        }
	        NextTick(SaveConfig);
        }
        #endregion

        #region ImageLibrary
        [PluginReference] private Plugin? ImageLibrary;
        
        private string GetImage(string shortname, ulong skin = 0) => (string)ImageLibrary?.Call("GetImage", shortname, skin); 
        private bool AddImage(string url, string shortname, ulong skin = 0) => (bool)ImageLibrary?.Call("AddImage", url, shortname, skin);
        #endregion

        #region Helpers
        public string HexToCuiColor(string HEX = "#FFFFFF", float Alpha = 100)
        {
            var str = HEX.Trim('#');
            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

            return $"{(double)r / 255} {(double)g / 255} {(double)b / 255} {Alpha / 100}";
        }
        #endregion
        
        #region Hooks
        private void OnServerInitialized()
        {
            ValidateImages();
            foreach (var player in BasePlayer.activePlayerList)
                OnPlayerConnected(player);
        }
        
        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(player, Layer);
        }

        private void OnPlayerConnected(BasePlayer player) => CreateCUI(player);
        #endregion

        #region Methods

        private void ValidateImages()
        {
            if (string.IsNullOrEmpty(config.Content_.ImageLink))
            {
                Puts("Тупой пидорас, установи IMAGELIBRARY!!!!!");
                Interface.Oxide.UnloadPlugin(Name);
            }
            else
                AddImage(config.Content_.ImageLink, "imagename");
        }
        
        #region Var
        private Dictionary<string, string> UICommands = new Dictionary<string, string>();
        #endregion

        #region Log
        private static void Error(string text)
        {
            _MagicLogo.Puts(text);
        }
        #endregion
        
        private string UICommand<T>(Action<BasePlayer, T, string> callback, T arg, string commandName)
        {
            var argument = $" {JsonConvert.SerializeObject(arg)}~INPUT_LIMITTER~";

            if (UICommands.ContainsKey(commandName))
            {
                return UICommands[commandName] + argument;
            }

            var commandUuid = CuiHelper.GetGuid();

            UICommands.Add(commandName, commandUuid);

            cmd.AddConsoleCommand(commandUuid, this, (args) =>
            {
                var player = args.Player();

                try
                {
                    var str = "";
                    var input = "";

                    args?.Args?.ToList()?.ForEach(v =>
                    {
                        str += $"{v} ";
                    });

                    if (str.Contains("~INPUT_LIMITTER~"))
                    {
                        try
                        {
                            string[] parts = str.Split(new string[] { "~INPUT_LIMITTER~" }, StringSplitOptions.None);

                            input = parts[1].Trim();
                            str = parts[0];
                        }
                        catch (Exception exc4)
                        {
                            Error($"Failed to parse UICommand arguments (input): {args?.FullString} {str} {input}");
                            Error(exc4.ToString());
                        }
                    }

                    try
                    {
                        while (str.Contains("\\r "))
                        {
                            str = str.Replace("\\r ", "");
                        }
                        while (str.Contains("\r "))
                        {
                            str = str.Replace("\r ", "");
                        }
                        while (str.Contains("\r"))
                        {
                            str = str.Replace("\r", "");
                        }

                        var restoredArgument = JsonConvert.DeserializeObject<T>(str);

                        try
                        {
                            callback(player, restoredArgument, input ?? "");
                        }
                        catch (Exception exc3)
                        {
                            Error($"Failed to parse UICommand arguments (callback): {args?.FullString} {str} {input}");
                            Error(exc3.ToString());
                        }
                    }
                    catch (Exception exc2)
                    {
                        Error($"Failed to parse UICommand arguments (deserialize): {args?.FullString} {str} {input}");
                        Error(exc2.ToString());
                    }
                }
                catch (Exception exc)
                {
                    Error($"Failed to parse UICommand arguments: {args?.FullString}");
                    Error(exc.ToString());
                }

                return true;
            });

            return commandUuid + argument;
        }
        #endregion

        #region CUI

        #region InterfaceVar
        private const string Layer = "LogoUI";
        #endregion
        
        private void CreateCUI(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "15 -45", OffsetMax = "45 -15" },
                Button =
                {
                    Color = HexToCuiColor("#ddd2c9", 25), Material = "assets/content/ui/uibackgroundblur.mat", 
                    Command = UICommand((player, args, input) =>
                    {
                        if (!string.IsNullOrEmpty(config.Content_.Command))
                            ConsoleNetwork.SendClientCommand(player.Connection, config.Content_.Command);
                        else
                        {
                            if (plugins.Exists(args.pluginname))
                                plugins.Find(args.pluginname).Call(args.pluginhook, player);
                        }
                    }, new { pluginname = config.Content_.PluginName, pluginhook = config.Content_.PluginHook, consolecommand = config.Content_.Command }, "customCommand")
                },
                Text = { Text = "" }
            }, "OverlayNonScaled", Layer, Layer);
            
            container.Add(new CuiElement
            {
                Name = Layer + "Image",
                Parent = Layer,
                Components = {
                    new CuiRawImageComponent { Color = HexToCuiColor(), Png = GetImage("imagename"), Sprite = "assets/icons/loading.png" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" }
                }
            });

            CuiHelper.AddUi(player, container);
        }
        #endregion
    }
}