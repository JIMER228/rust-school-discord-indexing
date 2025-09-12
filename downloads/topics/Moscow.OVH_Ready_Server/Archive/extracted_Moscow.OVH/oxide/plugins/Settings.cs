using System.Collections.Generic;
using System.Linq;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Settings", "Hougan", "0.0.1")]
    public class Settings : RustPlugin
    {
        #region Variables
        
        private Dictionary<string, string> Settingses = new Dictionary<string,string>
        {
            ["ЧАТ"] = "chat.say /chatSecret",
            ["ОПОВЕЩЕНИЕ_О_РЕЙДЕ"] = "chat.say /alertSecret",
            ["ТЕЛЕПОРТАЦИЯ"] = "chat.say /tpSecret" 
        };
        
        #endregion

        [ChatCommand("settings")]
        private void CmdChatSettings(BasePlayer player) =>
            InitializeSettings(player, Settingses.FirstOrDefault().Key);

        [ConsoleCommand("UI_Settings_Handler")]
        private void CmdConsoleSettings(ConsoleSystem.Arg args)
        {
            var player = args.Player();
            if (!player || !args.HasArgs(1)) return;
            
            InitializeSettings(player, args.Args[0]);
        }

        private string SettingsLayer = "UI_SettingsLayer";

        private void InitializeSettings(BasePlayer player, string currentSection)
        {
            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, SettingsLayer);

            container.Add(new CuiPanel() 
            {
                CursorEnabled = true,
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1.43 1", OffsetMin = "0 0", OffsetMax = "0 0"},
                Image = {Color = "0 0 0 0"}
            }, "UI_RustMenu_Internal", SettingsLayer);
                    
            container.Add(new CuiPanel()
            { 
                CursorEnabled = true,
                RectTransform = {AnchorMin = "-0.04 0", AnchorMax = "0.18 1", OffsetMin = "0 0", OffsetMax = "0 0"},
                Image         = {Color = "0.549 0.270 0.215 0.7", Material = ""}
            }, SettingsLayer, SettingsLayer + ".C");
            
            container.Add(new CuiPanel()
            { 
                CursorEnabled = true,
                RectTransform = {AnchorMin = "0 -0.05", AnchorMax = "1 0.5", OffsetMin = "0 0", OffsetMax = "0 0"},
                Image         = {Color = "0 0 0 0.9", Sprite = "assets/content/ui/ui.gradient.up.psd"}
            }, SettingsLayer + ".C");
                                     
            container.Add(new CuiPanel()
            { 
                CursorEnabled = true,
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 0.4", OffsetMin = "0 0", OffsetMax = "0 0"},
                Image         = {Color = "0 0 0 0.2", Sprite = "assets/content/ui/ui.background.transparent.radial.psd"}
            }, SettingsLayer + ".C");
                 
            float topPosition = (Settingses.Count / 2f * 40 + (Settingses.Count - 1) / 2f * 5);
            foreach (var vip in Settingses) 
            {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0.5", AnchorMax = "1 0.51", OffsetMin = $"0 {topPosition - 40}", OffsetMax = $"0 {topPosition}" },
                    Button = { Color = currentSection == vip.Key ? "0.149 0.145 0.137 0.8" : "0 0 0 0", Command = $"UI_Settings_Handler {vip.Key}"},
                    Text = { Text = "", Align = TextAnchor.MiddleRight, Font = "robotocondensed-bold.ttf", FontSize = 14 }
                }, SettingsLayer + ".C", SettingsLayer + vip.Value); 
                
                if( currentSection == vip.Key)
                    player.SendConsoleCommand(vip.Value); 
                
                container.Add(new CuiLabel 
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"0 0", OffsetMax = $"-20 0" },
                    Text = { Text = vip.Key.ToUpper().Replace("_", " "), Align = TextAnchor.MiddleRight, Font = "robotocondensed-bold.ttf", FontSize = 24, Color = "0.929 0.882 0.847 1"}
                }, SettingsLayer + vip.Value);
                 
                topPosition -= 40 + 5;
            }
                
            container.Add(new CuiPanel()
            { 
                CursorEnabled = true,
                RectTransform = {AnchorMin = "0.18 0", AnchorMax = "0.665 1", OffsetMin = "0 0", OffsetMax = "0 0"},
                Image         = {Color = "0.117 0.121 0.109 0.95" }
            }, SettingsLayer, SettingsLayer + ".R");
                
            CuiHelper.AddUi(player, container);
        }
    }
}