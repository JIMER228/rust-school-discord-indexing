using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.Networking;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Plugins;
using Newtonsoft.Json;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("IQMenu", "Mercury_REIN", "1.4.7")]
    [Description("Information HUD menu")]
    class IQMenu : RustPlugin
    {
        [PluginReference] private Plugin ImageLibrary;
        [PluginReference] private Plugin IQFakeActive;

        private void Init() => _ = this;

        private class ImageUi
        {
            private static Coroutine coroutineImg = null;
            private static Dictionary<String, String> Images = new Dictionary<String, String>();

            private static List<String> KeyImages = new List<String>();
            public static void DownloadImages() { coroutineImg = ServerMgr.Instance.StartCoroutine(AddImage()); }

            private static IEnumerator AddImage()
            {
                if (_ == null)
                    yield break;
                _.PrintWarning(LanguageEn ? "We generate the interface, wait ~10-15 seconds!" : "Генерируем интерфейс, ожидайте ~10-15 секунд!");
                foreach (String URL in KeyImages)
                {
                    String KeyName = URL;
                    if (KeyName == null) throw new ArgumentNullException(nameof(KeyName));

                    UnityWebRequest www = UnityWebRequestTexture.GetTexture(URL);
                    yield return www.SendWebRequest();

                    if (www.result == UnityWebRequest.Result.ConnectionError || 
                        www.result == UnityWebRequest.Result.ProtocolError)
                    {
                        _.PrintWarning($"Image download error! Error: {www.error}, Image name: {KeyName}");
                        www.Dispose();
                        coroutineImg = null;
                        yield break;
                    }

                    Texture2D texture = DownloadHandlerTexture.GetContent(www);
                    if (texture != null)
                    {
                        Byte[] bytes = texture.EncodeToPNG();

                        String image = FileStorage.server.Store(bytes, FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID).ToString();
                        if (!Images.ContainsKey(KeyName))
                            Images.Add(KeyName, image);
                        else
                            Images[KeyName] = image;
                        UnityEngine.Object.DestroyImmediate(texture);
                    }

                    www.Dispose();
                    yield return CoroutineEx.waitForSeconds(0.02f);
                }
                yield return CoroutineEx.waitForSeconds(0.02f);
                coroutineImg = null;

                _interface = new InterfaceBuilder();
                _.PrintWarning(LanguageEn ? "Interface loaded successfully!" : "Интерфейс успешно загружен!");
                
                foreach (BasePlayer basePlayer in BasePlayer.activePlayerList)
                    _.OnPlayerConnected(basePlayer);

                if (config.UsedButtonsSetting.UseClock || config.UsedButtonsSetting.UseOnline)
                {
                    TimerUpdate = _.timer.Every(60f, () => 
                    {
                        foreach (BasePlayer player in BasePlayer.activePlayerList)
                        {
                            if (_.lootedContainerPlayers.Contains(player)) continue;
                            if (config.UsedButtonsSetting.UseClock)
                                _.DrawUI_ClockLabel(player);

                            if (config.UsedButtonsSetting.UseOnline)
                            {
                                _.DrawUI_OnlineLabel(player);
                                _.DrawUI_SleepingLabel(player);
                            }
                        }
                    });
                }
            }

            public static String GetImage(String ImgKey) => Images.ContainsKey(ImgKey) ? Images[ImgKey] : string.Empty;

            public static void Initialize()
            {
                KeyImages = new List<String>();
                Images = new Dictionary<String, String>();
                
                if (!KeyImages.Contains(config.PresetAdditionalButtonSetting.PNG))
                    KeyImages.Add(config.PresetAdditionalButtonSetting.PNG);

                if (!KeyImages.Contains(config.TitleMenu.PNG))
                    KeyImages.Add(config.TitleMenu.PNG);
                
                foreach (Configuration.PresetsButtons presetsButtons in config.ButtonsDropList)
                {
                    if (!KeyImages.Contains(presetsButtons.PNG))
                        KeyImages.Add(presetsButtons.PNG);
                }
                
            }
            public static void Unload()
            {
                coroutineImg = null;
                foreach (KeyValuePair<String, String> item in Images)
                    FileStorage.server.RemoveExact(UInt32.Parse(item.Value), FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID, 0U);

                KeyImages.Clear();
                KeyImages = null;
                Images.Clear();
                Images = null;
            }
        }

        private void DrawUI_OnlinePanel(BasePlayer player)
        {
            String Interface = InterfaceBuilder.GetInterface("UI_IQMENU_PANEL_ONLINE");
            if (Interface == null) return;

            Interface = Interface.Replace("%OFFSET_MIN%", "-45.596 -25.667");
            Interface = Interface.Replace("%OFFSET_MAX%", "-10.604 -11");

            CuiHelper.DestroyUi(player, "PanelOnline");
            CuiHelper.AddUi(player, Interface);
            
            DrawUI_OnlineLabel(player);
        }

        private class InterfaceBuilder
        {
            public static InterfaceBuilder Instance;
            public const String UI_PANEL_IQMENU = "UI_PANEL_IQMENU";
            public Dictionary<String, String> Interfaces;

            public InterfaceBuilder()
            {
                Instance = this;
                Interfaces = new Dictionary<String, String>();

                BuildingPlayer_Menu();
                
                Building_Clock_Panel();
                Building_Clock();
                
                Building_Online_Panel();
                Building_Online();

                Building_Sleeping_Panel();
                Building_Sleeping();
                
                Building_Button_DropMenu();

                BuildingIQFakeActive();
            }

            public static void AddInterface(String name, String json)
            {
                if (Instance.Interfaces.ContainsKey(name))
                {
                    _.PrintError($"Error! Tried to add existing cui elements! -> {name}");
                    return;
                }

                Instance.Interfaces.Add(name, json);
            }
            public static String GetInterface(String name)
            {
                String json = String.Empty;
                if (Instance != null && Instance.Interfaces.TryGetValue(name, out json) == false)
                {
                    _.PrintWarning($"Warning! UI elements not found by name! -> {name}");
                }

                return json;
            }

            public static void DestroyAll()
            {
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                {
                    CuiHelper.DestroyUi(player, UI_PANEL_IQMENU);
                }
            }

            private void BuildingPlayer_Menu()
            {
                CuiElementContainer container = new CuiElementContainer();

                container.Add(new CuiPanel
                {
                    CursorEnabled = false, 
                    Image = { Color = "0 0 0 0" },
                    RectTransform =
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "-0.001 -35.461", OffsetMax = "265.97 0.008"
                    }
                }, "%LAYER_UI%", UI_PANEL_IQMENU);

                container.Add(new CuiElement
                {
                    Name = "TitleServer",
                    Parent = UI_PANEL_IQMENU,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = "%NAME_SERVER%", Font = "robotocondensed-regular.ttf", FontSize = 26,
                            Align = TextAnchor.MiddleLeft, Color = "1 1 1 1"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-129.008 -13.402",
                            OffsetMax = "132.985 17.735"
                        }
                    }
                });

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = config.TitleMenu.ColorPanel },
                    RectTransform =
                    {
                        AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-79.067 -25.667",
                        OffsetMax = "-64.4 -11"
                    }
                }, UI_PANEL_IQMENU, "PanelMenuButton");

                container.Add(new CuiElement
                {
                    Name = "ImageBtnMenu",
                    Parent = "PanelMenuButton",
                    Components =
                    {
                        new CuiRawImageComponent { Color = "1 1 1 1", Png = ImageUi.GetImage(config.TitleMenu.PNG) },
                        new CuiRectTransformComponent
                            { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-6 -6", OffsetMax = "6 6" }
                    }
                });

                container.Add(new CuiButton
                {
                    Button = { Color = "1 1 1 0", Command = "menu.ui.dropbuttons"},
                    Text =
                    {
                        Text = "", Font = "robotocondensed-regular.ttf", FontSize = 18,
                        Align = TextAnchor.LowerLeft, Color = "0 0 0 0"
                    },
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1"
                    }
                }, "ImageBtnMenu", "ButtonFunc_Menu_OpenDroplist");

                container.Add(new CuiButton
                {
                    Button = { Color = "1 1 1 0", Command = "menu.ui.dropbuttons"},
                    Text =
                    {
                        Text = "%TITLE_MENU_NAME%", Font = "robotocondensed-regular.ttf", FontSize = 18,
                        Align = TextAnchor.LowerLeft, Color = "1 1 1 1"
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-127.804 -28.523", OffsetMax = "-76.863 -8.143"
                    }
                }, UI_PANEL_IQMENU, "ButtonFunc_Menu_OpenDroplist");

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = config.PresetAdditionalButtonSetting.ColorPanel },
                    RectTransform =
                    {
                        AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-62.157 -25.667",
                        OffsetMax = "-47.491 -11"
                    }
                }, UI_PANEL_IQMENU, "AdditionalBtn");

                container.Add(new CuiElement
                {
                    Name = "AdditionalBtn_Img",
                    Parent = "AdditionalBtn",
                    Components =
                    {
                        new CuiRawImageComponent
                            { Color = "1 1 1 1", Png = ImageUi.GetImage(config.PresetAdditionalButtonSetting.PNG) },
                        new CuiRectTransformComponent
                            { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-6 -6", OffsetMax = "6 6" }
                    }
                });

                container.Add(new CuiButton
                {
                    Button = { Color = "1 1 1 0", Command = config.PresetAdditionalButtonSetting.Command },
                    Text =
                    {
                        Text = "", Font = "robotocondensed-regular.ttf", FontSize = 18,
                        Align = TextAnchor.LowerLeft, Color = "0 0 0 0"
                    },
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1"
                    }
                }, "AdditionalBtn_Img", "ButtonFunc_AdditionalBtn_Img");

                AddInterface("UI_IQMENU_PANEL", container.ToJson());
            }

            private void Building_Clock_Panel()
            {
                CuiElementContainer container = new CuiElementContainer();

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = config.UsedButtonsSetting.ColorPanelClock },
                    RectTransform =
                    {
                        AnchorMin = "0.5 0.5", 
                        AnchorMax = "0.5 0.5", 
                        OffsetMin = "45.388 -25.667",
                        OffsetMax = "87.38 -11"
                    }
                }, UI_PANEL_IQMENU, "PanelClock");

                container.Add(new CuiElement
                {
                    Parent = "PanelClock",
                    Components =
                    {
                        new CuiRawImageComponent
                            {
                                Color = "1 1 1 1",
                                Png = UIIcons.Get("CLOCK_ICON")
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0.5",
                                AnchorMax = "0 0.5",
                                OffsetMin = "2 -7",
                                OffsetMax = "16 7"
                            }
                    }
                });

                AddInterface("UI_IQMENU_PANEL_CLOCK", container.ToJson());
            }     
            
            private void Building_Clock()
            {
                CuiElementContainer container = new CuiElementContainer();

                container.Add(new CuiElement
                {
                    Name = "ClockTime",
                    Parent = "PanelClock",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = "%TIME%", 
                            Font = "robotocondensed-regular.ttf", 
                            FontSize = 10,
                            Align = TextAnchor.MiddleCenter, 
                            Color = config.UsedButtonsSetting.ColorTextClock
                        },
                        new CuiRectTransformComponent
                            { 
                                AnchorMin = "0 0", 
                                AnchorMax = "1 1", 
                                OffsetMin = "16 0", 
                                OffsetMax = "-2 0"
                            }
                    }
                });

                AddInterface("UI_IQMENU_UPDATE_CLOCK", container.ToJson());
            }

            private void Building_Online_Panel()
            {
                CuiElementContainer container = new CuiElementContainer();

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = config.UsedButtonsSetting.ColorPanelOnline },
                    RectTransform =
                    {
                        AnchorMin = "0.5 0.5", 
                        AnchorMax = "0.5 0.5", 
                        OffsetMin = "-45.596 -25.667", 
                        OffsetMax = "12.396 -11"
                    }
                }, UI_PANEL_IQMENU, "PanelOnline");

                container.Add(new CuiElement
                {
                    Parent = "PanelOnline",
                    Components =
                    {
                        new CuiRawImageComponent
                            {
                                Color = "1 1 1 1",
                                Png = UIIcons.Get("ONLINE_ICON")
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0.5",
                                AnchorMax = "0 0.5",
                                OffsetMin = "2 -7",
                                OffsetMax = "16 7"
                            }
                    }
                });

                AddInterface("UI_IQMENU_PANEL_ONLINE", container.ToJson());
            }     
            
            private void Building_Online()
            {
                CuiElementContainer container = new CuiElementContainer();

                container.Add(new CuiElement
                {
                    Name = "OnlineCount",
                    Parent = "PanelOnline",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = "%ONLINE%/%MAXPLAYERS%+%JOINING%",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 10,
                            Align = TextAnchor.MiddleCenter,
                            Color = config.UsedButtonsSetting.ColorTextOnline
                        },
                        new CuiRectTransformComponent
                        { 
                            AnchorMin = "0 0", 
                            AnchorMax = "1 1", 
                            OffsetMin = "16 0",
                            OffsetMax = "-2 0"
                        }
                    }
                });

                AddInterface("UI_IQMENU_UPDATE_ONLINE", container.ToJson());
            }

            private void Building_Sleeping_Panel()
            {
                CuiElementContainer container = new CuiElementContainer();

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = config.UsedButtonsSetting.ColorPanelOnline },
                    RectTransform =
                    {
                        AnchorMin = "0.5 0.5", 
                        AnchorMax = "0.5 0.5", 
                        OffsetMin = "14.396 -25.667",
                        OffsetMax = "44.388 -11"
                    }
                }, UI_PANEL_IQMENU, "PanelSleeping");

                container.Add(new CuiElement
                {
                    Parent = "PanelSleeping",
                    Components =
                    {
                        new CuiRawImageComponent
                            {
                                Color = "1 1 1 1",
                                Png = UIIcons.Get("SLEEPING_ICON")
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0.5",
                                AnchorMax = "0 0.5",
                                OffsetMin = "2 -7",
                                OffsetMax = "16 7"
                            }
                    }
                });

                AddInterface("UI_IQMENU_PANEL_SLEEPING", container.ToJson());
            }

            private void Building_Sleeping()
            {
                CuiElementContainer container = new CuiElementContainer();

                container.Add(new CuiElement
                {
                    Name = "SleepingCount",
                    Parent = "PanelSleeping",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = "%SLEEPING%", 
                            Font = "robotocondensed-regular.ttf", 
                            FontSize = 10,
                            Align = TextAnchor.MiddleRight,
                            Color = config.UsedButtonsSetting.ColorTextOnline
                        },
                        new CuiRectTransformComponent
                            { 
                                AnchorMin = "0 0", 
                                AnchorMax = "1 1", 
                                OffsetMin = "16 0", 
                                OffsetMax = "-4 0"
                            }
                    }
                });

                AddInterface("UI_IQMENU_UPDATE_SLEEPING", container.ToJson());
            }

            private void Building_Button_DropMenu()
            {
                CuiElementContainer container = new CuiElementContainer();

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0 0 0 0" },
                    RectTransform ={ AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "%OFFSET_MIN%", OffsetMax = "%OFFSET_MAX%" }
                },UI_PANEL_IQMENU,"%NAME_BUTTON%");

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "%COLOR_PANEL%" },
                    RectTransform ={ AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-65.187 -7.333", OffsetMax = "-50.52 7.333" }
                },"%NAME_BUTTON%","PanelTemplateButtonDrop");

                container.Add(new CuiElement
                {
                    Name = "Image",
                    Parent = "PanelTemplateButtonDrop",
                    Components = {
                        new CuiRawImageComponent { Color = "1 1 1 1", Png = "%PNG_BUTTON%" },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-6 -6", OffsetMax = "6 6" }
                    }
                });

                container.Add(new CuiElement
                {
                    Name = "TitleName",
                    Parent = "%NAME_BUTTON%",
                    Components = {
                        new CuiTextComponent { Text = "%TITLE_BUTTON%", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.UpperLeft, Color = "1 1 1 1" },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-48.766 -8.461", OffsetMax = "65.1852987 8.46431"  }
                    }
                });

                container.Add(new CuiButton
                {
                    Button = { Color = "1 1 1 0", Command = "%COMMAND_BUTTON%"},
                    Text = { Text = "", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0 0 0 0" },
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" }
                },"%NAME_BUTTON%","ButtonFunc");

                AddInterface("UI_IQMENU_DROP_BUTTON", container.ToJson());
            }

            private void BuildingIQFakeActive()
            {
                CuiElementContainer container = new CuiElementContainer();

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.3445 0.65433 0.5552985 0.2" },
                    RectTransform =
                    {
                        AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-45.596 -25.667", OffsetMax = "-7.604 -11"
                    }
                }, UI_PANEL_IQMENU, "IQFakePanel");
                
                AddInterface("UI_IQMENU_PANEL_IQFAKEACTIVE", container.ToJson());
            }     
        }

        private void DrawUI_ClockLabel(BasePlayer player)
        {
            if (_interface == null) return;
            String Interface = InterfaceBuilder.GetInterface("UI_IQMENU_UPDATE_CLOCK");
            if (Interface == null) return;
            
            Interface = Interface.Replace("%TIME%", config.UsedButtonsSetting.UseFormatTimeClock ? $"{TOD_Sky.Instance.Cycle.DateTime:HH:mm}" : $"{TOD_Sky.Instance.Cycle.DateTime:hh:mm}");

            CuiHelper.DestroyUi(player, "ClockTime");
            CuiHelper.AddUi(player, Interface);
        }	

        private void DestroyButtons(BasePlayer player)
        {
            for (Int32 Y = 0; Y < config.ButtonsDropList.Count; Y++)
                CuiHelper.DestroyUi(player, $"DROP_BUTTON_{Y}");
        }

        protected override void LoadDefaultConfig() => config = Configuration.GetNewConfiguration();
        
        private Int32 FakeOnline() => (Int32)IQFakeActive?.Call("GetOnline");
        /// <summary>
        /// - Исправил синхронизацию онлайна с IQFakeActive
        /// </summary>
        
        private const Boolean LanguageEn = false;

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();

                if (config.UsedButtonsSetting.ColorTextClock == null ||
                    String.IsNullOrWhiteSpace(config.UsedButtonsSetting.ColorTextClock))
                    config.UsedButtonsSetting.ColorTextClock = "1 1 1 1";
                
                if (config.UsedButtonsSetting.ColorPanelClock == null ||
                    String.IsNullOrWhiteSpace(config.UsedButtonsSetting.ColorPanelClock))
                    config.UsedButtonsSetting.ColorPanelClock = "1 1 1 0.2";
                
                if (config.UsedButtonsSetting.ColorTextOnline == null ||
                    String.IsNullOrWhiteSpace(config.UsedButtonsSetting.ColorTextOnline))
                    config.UsedButtonsSetting.ColorTextOnline = "1 1 1 1";
                
                if (config.UsedButtonsSetting.ColorPanelOnline == null ||
                    String.IsNullOrWhiteSpace(config.UsedButtonsSetting.ColorPanelOnline))
                    config.UsedButtonsSetting.ColorPanelOnline = "1 1 1 0.2";
                
                if (config.TitleMenu.ColorPanel == null ||
                    String.IsNullOrWhiteSpace(config.TitleMenu.ColorPanel))
                    config.TitleMenu.ColorPanel = "1 1 1 0.2";
                
                if (config.PresetAdditionalButtonSetting.ColorPanel == null ||
                    String.IsNullOrWhiteSpace(config.PresetAdditionalButtonSetting.ColorPanel))
                    config.PresetAdditionalButtonSetting.ColorPanel = "1 1 1 0.2";

                foreach (Configuration.PresetsButtons presetsButtons in config.ButtonsDropList)
                {
                    if (presetsButtons.ColorPanel == null ||
                        String.IsNullOrWhiteSpace(presetsButtons.ColorPanel))
                        presetsButtons.ColorPanel = "1 1 1 0.2";
                }
            }
            catch
            {
                PrintWarning(LanguageEn ? $"Error reading #54327 configuration 'oxide/config/{Name}', creating a new configuration!!" : $"Ошибка чтения #54327 конфигурации 'oxide/config/{Name}', создаём новую конфигурацию!!");
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }

        [ConsoleCommand("take.command.menu")]
        private void TakeCommandMenu(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            String Command = arg.HasArgs() ? String.Join(" ", arg.Args) : null;

            if (config.CloseButtonsForTake)
            {
                if (OpenedUI.Contains(player))
                    OpenedUI.Remove(player);
                DestroyButtons(player);
            }

            player.SendConsoleCommand(Command);
        }
        
        [ConsoleCommand("menu.ui.dropbuttons")]
        private void MenuUiCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();

            if (OpenedUI.Contains(player))
            {
                DestroyButtons(player);
                OpenedUI.Remove(player);
            }
            else
            {
                for (Int32 Y = 0; Y < config.ButtonsDropList.Count; Y++)
                {
                    Configuration.PresetsButtons Button = config.ButtonsDropList[Y];
                    DrawUI_ButtonDropMenu(player, Y, Button.Command, ImageUi.GetImage(Button.PNG),
                        Button.Titles.GetLanguageText(player), Button.ColorPanel);
                }
                
                OpenedUI.Add(player);
            }
        }
        
        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null || player.IsNpc) return;
            if (_interface == null) return;

            DrawUI_Menu(player);

            if (config.UsedButtonsSetting.UseClock)
                DrawUI_ClockPanel(player);

            if (config.UsedButtonsSetting.UseOnline)
            {
                DrawUI_OnlinePanel(player);
                DrawUI_SleepingPanel(player);
            }
        }

        private void DrawUI_ClockPanel(BasePlayer player)
        {
            if (_interface == null) return;
            String Interface = InterfaceBuilder.GetInterface("UI_IQMENU_PANEL_CLOCK");
            if (Interface == null) return;

            String OffsetMin = config.UsedButtonsSetting.UseOnline ? "34.388 -25.667" : "-45.596 -25.667";
            String OffsetMax = config.UsedButtonsSetting.UseOnline ? "69.38 -11" : "-10.604 -11";
            
            Interface = Interface.Replace("%OFFSET_MIN%", OffsetMin);
            Interface = Interface.Replace("%OFFSET_MAX%", OffsetMax);

            CuiHelper.DestroyUi(player, "PanelClock");
            CuiHelper.AddUi(player, Interface);
            
            DrawUI_ClockLabel(player);
        }	
        
        private void OnEntityLeave(TriggerWorkbench trigger, BasePlayer player)
        {
            if (player == null || player.IsNpc) return;
            if (!lootedContainerPlayers.Contains(player)) return;
            
            DrawUI_Menu(player);
            
            lootedContainerPlayers.Remove(player);
        }

        private List<BasePlayer> OpenedUI = new List<BasePlayer>();
        private static InterfaceBuilder _interface;

        private void DrawUI_Menu(BasePlayer player)
        {
            if (_interface == null) return;
            String Interface = InterfaceBuilder.GetInterface("UI_IQMENU_PANEL");
            if (Interface == null) return;

            Interface = Interface.Replace("%LAYER_UI%", config.LayerUI);
            Interface = Interface.Replace("%NAME_SERVER%", config.TitleServer.GetLanguageText(player));
            Interface = Interface.Replace("%TITLE_MENU_NAME%", config.TitleMenu.Titles.GetLanguageText(player));

            CuiHelper.DestroyUi(player, InterfaceBuilder.UI_PANEL_IQMENU);
            CuiHelper.AddUi(player, Interface);

            if (config.UsedButtonsSetting.UseClock)
                DrawUI_ClockPanel(player);
            
            if (config.UsedButtonsSetting.UseOnline)
                DrawUI_OnlinePanel(player);
        }
        private static Timer TimerUpdate;
        protected override void SaveConfig() => Config.WriteObject(config);

        private static Configuration config = new Configuration();
        private class Configuration
        {
            [JsonProperty(LanguageEn ? "Layer setting (Overlay, Hud)" : "Настройка слоя (Overlay, Hud, Hud.Menu)")]
            public String LayerUI;
            [JsonProperty(LanguageEn ? "Setting the menu display" : "Настройка отображения в меню")]
            public UsedButtons UsedButtonsSetting = new UsedButtons();
            [JsonProperty(LanguageEn ? "Use IQFakeActive (true - yes/false - no)" : "Использовать IQFakeActive (true - да/false - нет)")]
            public Boolean UseIQFakeActive;

            public static Configuration GetNewConfiguration() 
            {
                return new Configuration
                {
                    LayerUI = "Hud.Menu",
                    UseIQFakeActive = false,
                    CloseButtonsForTake = false,
                    TitleServer = new LanguageTitles()
                    {
                        RussuianText = "<size=26><b><color=#EFEDEE>СУПЕР <color=#A0F49E>СЕРВЕР</color> | MAX 3</color></b></size>",
                        EnglishText = "<size=26><b><color=#EFEDEE>SUPER <color=#A0F49E>SERVER</color> | MAX 3</color></b></size>"
                    },
                    TitleMenu = new PresetButtonMenu()
                    {
                        Titles = new LanguageTitles()
                        {
                            RussuianText = "<size=18><b><color=#EFEDEE>МЕНЮ</color></b></size>",
                            EnglishText = "<size=18><b><color=#EFEDEE>MENU</color></b></size>"
                        },
                        PNG = "https://i.ibb.co/VDJrBHR/dVAeMdr.png",
                        ColorPanel = "1 1 1 0.2",
                    },
                    UsedButtonsSetting = new UsedButtons()
                    {
                        UseClock = true,
                        UseOnline = true,
                        UseFormatTimeClock = true,
                        ColorTextClock = "1 1 1 1",
                        ColorPanelClock = "1 1 1 0.2",
                        ColorTextOnline = "1 1 1 1",
                        ColorPanelOnline = "1 1 1 0.2",
                    },
                    PresetAdditionalButtonSetting = new PresetAdditionalButton()
                    {
                        PNG = "https://i.ibb.co/qpJBhSX/0jjyrgj.png",
                        Command = "chat.say /store",
                        ColorPanel = "1 1 1 0.2",
                    },
                    ButtonsDropList = new List<PresetsButtons>()
                    {
                        new PresetsButtons()
                        {
                            Command = "chat.say /info",
                            PNG = "https://i.ibb.co/9WqFbxb/M1sFQcE.png",
                            ColorPanel = "1 1 1 0.2",
                            Titles = new LanguageTitles()
                            {
                                RussuianText = "<size=14><b><color=#EFEDEE>ИНФОРМАЦИЯ</color></b></size>",
                                EnglishText = "<size=14><b><color=#EFEDEE>INFROMATION</color></b></size>"
                            }
                        },
                        new PresetsButtons()
                        {
                            Command = "chat.say /report",
                            PNG = "https://i.ibb.co/4Sp8pCX/l74eQhW.png",
                            ColorPanel = "1 1 1 0.2",
                            Titles = new LanguageTitles()
                            {
                                RussuianText = "<size=14><b><color=#EFEDEE>ЖАЛОБЫ</color></b></size>",
                                EnglishText = "<size=14><b><color=#EFEDEE>REPORTS</color></b></size>"
                            }
                        },
                        new PresetsButtons()
                        {
                            Command = "chat.say /block",
                            PNG = "https://i.ibb.co/CtVWnc8/ycD1PuT.png",
                            ColorPanel = "1 1 1 0.2",
                            Titles = new LanguageTitles()
                            {
                                RussuianText = "<size=14><b><color=#EFEDEE>ВАЙПБЛОК</color></b></size>",
                                EnglishText = "<size=14><b><color=#EFEDEE>WIPEBLOCK</color></b></size>"
                            }
                        },
                        new PresetsButtons()
                        {
                            Command = "chat.say /kits",
                            PNG = "https://i.ibb.co/D4cpym4/3vycLfy.png",
                            ColorPanel = "1 1 1 0.2",
                            Titles = new LanguageTitles()
                            {
                                RussuianText = "<size=14><b><color=#EFEDEE>НАБОРЫ</color></b></size>",
                                EnglishText = "<size=14><b><color=#EFEDEE>KITS</color></b></size>"
                            }
                        },
                        new PresetsButtons()
                        {
                            Command = "chat.say /case",
                            PNG = "https://i.ibb.co/qgVdfwb/KfGO6vR.png",
                            ColorPanel = "1 1 1 0.2",
                            Titles = new LanguageTitles()
                            {
                                RussuianText = "<size=14><b><color=#EFEDEE>КЕЙСЫ</color></b></size>",
                                EnglishText = "<size=14><b><color=#EFEDEE>CASES</color></b></size>"
                            }
                        },
                    }
                };
            }
            [JsonProperty(LanguageEn ? "Close dropdown list after button selection (true - yes/false - no)" : "Закрывать выпадющий список после выбора кнопки (true - да/false - нет)")]
            public Boolean CloseButtonsForTake;
            [JsonProperty(LanguageEn ? "Customizing Buttons in the Dropdown Menu" : "Настройка кнопок в выпадающем меню")]
            public List<PresetsButtons> ButtonsDropList = new List<PresetsButtons>();
            internal class PresetButtonMenu
            {
                [JsonProperty(LanguageEn ? "Setting the text for the button" : "Настройка текста для кнопки")]
                public LanguageTitles Titles;
                [JsonProperty(LanguageEn ? "Image link .png (18x18)" : "Ссылка на картинку .png (18x18)")]
                public String PNG;
                [JsonProperty(LanguageEn ? "The color of the panel where the menu icon is located (RGBA)" : "Цвет панели на котором расположена иконка меню (RGBA)")]
                public String ColorPanel;
            }
            [JsonProperty(LanguageEn ? "Setting the button Menu" : "Настройка кнопки Меню")]
            public PresetButtonMenu TitleMenu = new PresetButtonMenu();
            internal class PresetAdditionalButton
            {
                [JsonProperty(LanguageEn ? "Image link .png (18x18)" : "Ссылка на картинку .png (18x18)")]
                public String PNG;
                [JsonProperty(LanguageEn ? "Command which will recoup on behalf of the player" : "Команда которая отыграется от имени игрока")]
                public String Command;
                [JsonProperty(LanguageEn ? "The color of the panel where the icon is located (RGBA)" : "Цвет панели на котором расположена иконка (RGBA)")]
                public String ColorPanel;
            }
            internal class PresetsButtons
            {
                [JsonProperty(LanguageEn ? "Setting the text for the button" : "Настройка текста для кнопки")]
                public LanguageTitles Titles;
                [JsonProperty(LanguageEn ? "Image link .png (18x18)" : "Ссылка на картинку .png (18x18)")]
                public String PNG;
                [JsonProperty(LanguageEn ? "Command which will recoup on behalf of the player" : "Команда которая отыграется от имени игрока")]
                public String Command;
                [JsonProperty(LanguageEn ? "The color of the panel where the icon is located (RGBA)" : "Цвет панели на котором расположена иконка (RGBA)")]
                public String ColorPanel;
            }
            [JsonProperty(LanguageEn ? "Additional Button Setting" : "Настройка дополнительной кнопки")]
            public PresetAdditionalButton PresetAdditionalButtonSetting = new PresetAdditionalButton();
            internal class UsedButtons
            {
                [JsonProperty(LanguageEn ? "The color of the clock text (RGBA)" : "Цвет текста часов (RGBA)")]
                public String ColorTextClock;
                [JsonProperty(LanguageEn ? "The color of the panel where the online text is located (RGBA)" : "Цвет панели на котором расположен текст с онлайном (RGBA)")]
                public String ColorPanelOnline;
                [JsonProperty(LanguageEn ? "Use the online indicator (true - yes / false - no)" : "Использовать показатель онлайна (true - да/false - нет)")]
                public Boolean UseOnline;
                [JsonProperty(LanguageEn ? "Use a 24-hour time format, otherwise a 12-hour one (true - yes / false - no)" : "Использовать 24х-часовой формат времени, иначе 12-часовой (true - да/false - нет)")]
                public Boolean UseFormatTimeClock;
                [JsonProperty(LanguageEn ? "The color of the panel where the clock text is located (RGBA)" : "Цвет панели на котором расположен текст с часами (RGBA)")]
                public String ColorPanelClock;
                [JsonProperty(LanguageEn ? "Use clock (shows server time) (true - yes / false - no)" : "Использовать часы (показывает серверное время) (true - да/false - нет)")]
                public Boolean UseClock;
                [JsonProperty(LanguageEn ? "The color of the online text (RGBA)" : "Цвет текста онлайна (RGBA)")]
                public String ColorTextOnline;
                [JsonProperty(LanguageEn ? "Icon for online panel (from ImageLibrary)" : "Иконка для панели онлайна (из ImageLibrary)")]
                public string OnlineIcon = "online_icon";
                [JsonProperty(LanguageEn ? "Icon for clock panel (from ImageLibrary)" : "Иконка для панели времени (из ImageLibrary)")]
                public string ClockIcon = "clock_icon";
                [JsonProperty(LanguageEn ? "Icon for sleeping panel (from ImageLibrary)" : "Иконка для панели спящих (из ImageLibrary)")]
                public string SleepingIcon = "sleeping_icon";
            }
            [JsonProperty(LanguageEn ? "Setting the server name" : "Настройка названия сервера")]
            public LanguageTitles TitleServer = new LanguageTitles();
            
            internal class LanguageTitles
            {
                [JsonProperty(LanguageEn ? "Text in Russian" : "Текст на русском")]
                public String RussuianText;
                [JsonProperty(LanguageEn ? "Text in English" : "Текст на английском")]
                public String EnglishText;

                public String GetLanguageText(BasePlayer player) => _.lang.GetLanguage(player.UserIDString).Equals("ru") ? RussuianText : EnglishText;
            }
        }

        private Timer _updateTimer;

        void OnServerInitialized(bool initial)
        {
            ImageUi.Initialize();
            ImageUi.DownloadImages();
            UIIcons.LoadImages(this);
            _updateTimer = timer.Every(5f, UpdateOnlineUI);
        }
        
        void UpdateOnlineUI()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                DrawUI_OnlineLabel(player);
                DrawUI_SleepingLabel(player);
            }
        }

        private void DrawUI_ButtonDropMenu(BasePlayer player, Int32 Y, String Command, String PNG, String TitleBtn, String ColorRGBA)
        {
            String Interface = InterfaceBuilder.GetInterface("UI_IQMENU_DROP_BUTTON");
            if (Interface == null) return;

            Interface = Interface.Replace("%NAME_BUTTON%", $"DROP_BUTTON_{Y}");
            Interface = Interface.Replace("%OFFSET_MIN%", $"-129.008 {-44.333 - (Y * 20f)}");
            Interface = Interface.Replace("%OFFSET_MAX%", $"1.365 {-29.667 - (Y * 20f)}");
            Interface = Interface.Replace("%COMMAND_BUTTON%", $"take.command.menu {Command}");
            Interface = Interface.Replace("%PNG_BUTTON%", PNG);
            Interface = Interface.Replace("%TITLE_BUTTON%", TitleBtn);
            Interface = Interface.Replace("%COLOR_PANEL%", ColorRGBA);

            CuiHelper.AddUi(player, Interface);
        }

        private void Unload()
        {
            if (_updateTimer != null)
            {
                _updateTimer.Destroy();
            }
            
            InterfaceBuilder.DestroyAll();
            ImageUi.Unload();

            if (TimerUpdate != null && !TimerUpdate.Destroyed)
            {
                TimerUpdate.Destroy();
                TimerUpdate = null;
            }
            
            OpenedUI.Clear();
            
            _ = null;
        }

        private static class UIIcons 
        {
            private static Dictionary<string, string> _imageCache = new Dictionary<string, string>();
            
            private static readonly Dictionary<string, string> _iconUrls = new Dictionary<string, string>
            {
                ["ONLINE_ICON"] = "https://i.ibb.co/YBHV4W9/dleUaul.png",
                ["CLOCK_ICON"] = "https://i.ibb.co/0XfX1bB/T3-IN2asdadqz.png",
                ["SLEEPING_ICON"] = "https://i.ibb.co/mXZ5T7G/24asda51.png"
            };
            
            public static void LoadImages(IQMenu plugin)
            {
                if (plugin.ImageLibrary == null) 
                {
                    plugin.PrintWarning("ImageLibrary not found! Icons won't be loaded.");
                    return;
                }

                foreach(var icon in _iconUrls)
                {
                    // Сначала удаляем старое изображение
                    plugin.ImageLibrary.Call("RemoveImage", icon.Key);
                    
                    // Загружаем новое изображение
                    plugin.PrintWarning($"Loading image for {icon.Key}");
                    plugin.ImageLibrary.Call("AddImage", icon.Value, icon.Key, 0UL);
                    
                    // Получаем ID загруженного изображения
                    var imageId = plugin.ImageLibrary.Call("GetImage", icon.Key);
                    if (imageId != null)
                    {
                        _imageCache[icon.Key] = imageId.ToString();
                        plugin.PrintWarning($"Successfully loaded/cached image for {icon.Key}: {imageId}");
                    }
                    else
                    {
                        plugin.PrintWarning($"Failed to load image for {icon.Key}");
                    }
                }
            }
            
            public static string Get(string key)
            {
                if (!_imageCache.ContainsKey(key))
                {
                    _.PrintWarning($"Image not found in cache: {key}");
                    return string.Empty;
                }

                // Возвращаем ID изображения из кэша
                return _imageCache[key];
            }
        }

        private void DrawUI_OnlineLabel(BasePlayer player)
        {
            String Interface = InterfaceBuilder.GetInterface("UI_IQMENU_UPDATE_ONLINE");
            if (Interface == null) return;
            
            Int32 Online = config.UseIQFakeActive && IQFakeActive != null ? FakeOnline() : BasePlayer.activePlayerList.Count;
            Int32 Joining = ServerMgr.Instance.connectionQueue.Joining;

            Interface = Interface.Replace("%ONLINE%", $"{Online}");
            Interface = Interface.Replace("%MAXPLAYERS%", $"{ConVar.Server.maxplayers}");
            Interface = Interface.Replace("%JOINING%", $"{(Joining > 0 ? $"{Joining}" : "")}");

            CuiHelper.DestroyUi(player, "OnlineCount");
            CuiHelper.AddUi(player, Interface);
        }	
        
        private void OnEntityEnter(TriggerWorkbench trigger, BasePlayer player)
        {
            if (player == null || player.IsNpc) return;
            
            if (lootedContainerPlayers.Contains(player)) return;
            CuiHelper.DestroyUi(player, InterfaceBuilder.UI_PANEL_IQMENU);
            lootedContainerPlayers.Add(player);
            
            if (OpenedUI.Contains(player))
                OpenedUI.Remove(player);
        }
        public List<BasePlayer> lootedContainerPlayers = new();

        private void DrawUI_SleepingLabel(BasePlayer player)
        {
            String Interface = InterfaceBuilder.GetInterface("UI_IQMENU_UPDATE_SLEEPING");
            if (Interface == null) return;
            
            Int32 Sleeping = BasePlayer.sleepingPlayerList.Count;
            Interface = Interface.Replace("%SLEEPING%", $"{Sleeping}");

            CuiHelper.DestroyUi(player, "SleepingCount");
            CuiHelper.AddUi(player, Interface);
        }

        private void DrawUI_SleepingPanel(BasePlayer player)
        {
            String Interface = InterfaceBuilder.GetInterface("UI_IQMENU_PANEL_SLEEPING");
            if (Interface == null) return;

            CuiHelper.DestroyUi(player, "PanelSleeping");
            CuiHelper.AddUi(player, Interface);
            
            DrawUI_SleepingLabel(player);
        }

        private static IQMenu _;

        private void OnPlayerSleep(BasePlayer player)
        {
            if (player == null || player.IsNpc) return;
            if (_interface == null) return;

            foreach (BasePlayer activePlayer in BasePlayer.activePlayerList)
            {
                if (lootedContainerPlayers.Contains(activePlayer)) continue;
                DrawUI_OnlineLabel(activePlayer);
                DrawUI_SleepingLabel(activePlayer);
            }
        }

        private void OnPlayerSleepEnded(BasePlayer player)
        {
            if (player == null || player.IsNpc) return;
            if (_interface == null) return;

            foreach (BasePlayer activePlayer in BasePlayer.activePlayerList)
            {
                if (lootedContainerPlayers.Contains(activePlayer)) continue;
                DrawUI_OnlineLabel(activePlayer);
                DrawUI_SleepingLabel(activePlayer);
            }
        }

        private string GetLibraryImage(string fileName)
        {
            if (ImageLibrary == null) return string.Empty;
            var imageId = ImageLibrary.Call("GetImage", fileName);
            return imageId?.ToString() ?? string.Empty;
        }
    }
}
