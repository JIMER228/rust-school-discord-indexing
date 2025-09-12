using Oxide.Game.Rust.Cui;
using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using System.Globalization;
using System.Reflection;

namespace Oxide.Plugins
{
    [Info("WUIAttachments", "Amino", "1.0.4")]
    [Description("Welcome UI Attachments")]
    public class WUIAttachments : RustPlugin
    {
        [PluginReference] private Plugin ImageLibrary, WelcomeController;

        #region Config
        public static Configuration _config;
        public UIElements _uiColors;
        public class Configuration
        {
            [JsonProperty(PropertyName = "Questions And Answers")]
            public List<QAConstructor> QuestionAndAnswers = new List<QAConstructor>();
            [JsonProperty(PropertyName = "Links")]
            public List<LinksComponent> Links = new List<LinksComponent>();
            [JsonProperty(PropertyName = "VIP RANKS")]
            public List<RanksComponent> VipRanks = new List<RanksComponent>();
            [JsonProperty(PropertyName = "Plugin images")]
            public Images Images { get; set; } = new Images();
            [JsonProperty(PropertyName = "UI Elements")]
            public UIElements UIElements { get; set; } = new UIElements();
            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    Images = new Images
                    {
                        BlueprintWipeIcon = "https://i.ibb.co/MNNw1W1/Buy-Command3.png",
                        MapWipeIcon = "https://i.ibb.co/CWrwbQH/Money-Bag2.png"
                    },
                    QuestionAndAnswers = new List<QAConstructor>()
                    {
                        new QAConstructor()
                        {
                            Identifer = "Q&A_1",
                            Title = "QUESTION AND ANSWERS",
                            Questions = new List<QAConstructor.QuestionsComponent>
                            {
                                new QAConstructor.QuestionsComponent { Question = "Where do I link?", Answer = "Link at https://linkhere.com/", Icon = "https://i.ibb.co/QrRWZDD/1f517.png" },
                                new QAConstructor.QuestionsComponent { Question = "Where can I find the rules?", Answer = "You can find the rules on the rules tab to your left!", Icon = "https://i.ibb.co/QKpqxBJ/scroll-1f4dc.png" }
                            }
                        }
                    },
                    Links = new List<LinksComponent>() {
                        new LinksComponent {
                            ButtonLink = "https://discord.gg/rustmania",
                            Icon = "https://i.ibb.co/HqWYxyN/DLogo.png",
                            Title = "DISCORD",
                            Description = "Join our discord and link for perks!"
                        },
                        new LinksComponent {
                            ButtonLink = "https://store.rustmania.net/",
                            Icon = "https://i.ibb.co/D9jmC9v/Store-Icon.png",
                            Title = "STORE",
                            Description = "Our store offeres a wide range\nof VIP ranks! Check it out!"
                        }
                    },
                    VipRanks = new List<RanksComponent>()
                    {
                        new RanksComponent {
                            ButtonLink = "https://store.rustmania.net/",
                            Icon = "https://i.ibb.co/0F0f9jb/VIPBanner.png",
                            Title = "STORE",
                            Description = new List<string>
                            {
                                "- 10 Sec TP",
                                "- 5 Sec outpost TP",
                                "- MyMini",
                                "- Queue Skip",
                                "- SkinBox",
                                "- /sil"
                            }
                        }
                    }
                };
            }
        }

        public class QAConstructor
        {
            public string Identifer { get; set; }
            public string Title { get; set; } = string.Empty;
            public List<QuestionsComponent> Questions = new List<QuestionsComponent>();

            public class QuestionsComponent
            {
                public string Question { get; set; } = string.Empty;
                public string Answer { get; set; } = string.Empty;
                public string Icon { get; set; } = string.Empty;
            }
        }

        public class LinksComponent
        {
            public string ButtonColor { get; set; } = "0 0 0 0";
            public string Title { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string ButtonLink { get; set; } = string.Empty;
            public string Icon { get; set; } = string.Empty;
        }

        public class RanksComponent
        {
            public string ButtonColor { get; set; } = "0 0 0 0";
            public string Title { get; set; } = string.Empty;
            public List<string> Description { get; set; }
            public string ButtonLink { get; set; } = string.Empty;
            public string Icon { get; set; } = string.Empty;
        }

        public class Images
        {
            [JsonProperty(PropertyName = "Blueprint Wipe Icon")]
            public string BlueprintWipeIcon { get; set; }
            [JsonProperty(PropertyName = "Map Wipe Icon")]
            public string MapWipeIcon { get; set; }
        }

        public class UIElements
        {
            public string CtrlCText = "CTRL + C";
            public string TitleBackgroundColor = "0 0 0 .7";
            public string MainPanelColor { get; set; } = "0 0 0 .6";
            public string PopupBackgroundColor { get; set; } = "0 0 0 .6";
            public string PopupBackgroundColorOverlay { get; set; } = "1 1 1 .15";
            public string PopupPanelColor { get; set; } = ".17 .17 .17 1";
            public string PopupPanelLabelColor { get; set; } = "1 1 1 .1";
            public string PopupPanelLinkColor { get; set; } = "0.41 0.67 1 1";
            public string AnswerBackgroundColor { get; set; } = "0 0 0 .5";
            public string QuestionBackgroundColor { get; set; } = "0 0 0 .6";
            public string QuestionBackgroundColor2 { get; set; } = "0 0 0 .7";
            public string PrimaryButtonColor { get; set; } = ".39 .76 1 .4";
            public string PrimaryButtonTextColor { get; set; } = ".39 .76 1 .6";
            public string SecondaryButtonColor { get; set; } = "0.46 0.46 0.46 .4";
            public string SecondaryButtonTextColor { get; set; } = "0.46 0.46 0.46 .5";
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) LoadDefaultConfig();
                _uiColors = _config.UIElements;
                SaveConfig();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                PrintWarning("Creating new config file.");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig() => _config = Configuration.DefaultConfig();
        protected override void SaveConfig() => Config.WriteObject(_config);
        #endregion

        #region Commands
        [ConsoleCommand("wui_main")]
        private void CMDSocMain(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            switch (arg.Args[0])
            {
                case "closepopup":
                    CuiHelper.DestroyUi(player, "WUIDisplayPanel");
                    break;
                case "openquestion":
                    var theQuestion = _config.QuestionAndAnswers.FirstOrDefault(x => x.Identifer.Equals(String.Join(" ", arg.Args.Skip(2)), StringComparison.OrdinalIgnoreCase));
                    UIOpenQNA(player, theQuestion, int.Parse(arg.Args[1]));
                    break;
                case "sociallinks":
                    UIShowSocial(player, int.Parse(arg.Args[1]));
                    break;
                case "viprank":
                    UIShowRank(player, int.Parse(arg.Args[1]));
                    break;
                case "rankspage":
                    UIOpenVIPRanks(player, int.Parse(arg.Args[1]));
                    break;
            }
        }
        #endregion

        #region Hook
        public static Dictionary<string, string> ConvertToDictionary(UIElements uiElements)
        {
            var dictionary = new Dictionary<string, string>();
            foreach (PropertyInfo property in typeof(UIElements).GetProperties())
            {
                string key = property.Name;
                string value = property.GetValue(uiElements)?.ToString();
                dictionary[key] = value;
            }
            return dictionary;
        }

        void OnWCRequestColors(string pluginName)
        {
            if (!pluginName.Equals("WUIAttachments", StringComparison.OrdinalIgnoreCase)) return;
            Interface.CallHook("WCSendColors", ConvertToDictionary(_uiColors), pluginName);
        }

        void OnWCSentThemeColors(List<string> pluginNames, Dictionary<string, string> themeColors)
        {
            if (!pluginNames.Contains("WUIAttachments")) return;

            _config.UIElements.TitleBackgroundColor = themeColors["BackgroundColor"];
            _config.UIElements.MainPanelColor = themeColors["BackgroundColor"];

            _config.UIElements.PopupBackgroundColor = themeColors["BackgroundColor"];
            _config.UIElements.PopupPanelColor = themeColors["PopupMainColor"];
            _config.UIElements.PopupPanelLabelColor = themeColors["PopupSecondaryColor"];
            _config.UIElements.PopupPanelLinkColor = themeColors["PrimaryButtonColor"];

            _config.UIElements.AnswerBackgroundColor = themeColors["BackgroundColor"];
            _config.UIElements.QuestionBackgroundColor = themeColors["SecondaryColor"];
            _config.UIElements.QuestionBackgroundColor2 = themeColors["SecondaryColor"];
            _config.UIElements.PrimaryButtonColor = themeColors["PrimaryButtonColor"];
            _config.UIElements.PrimaryButtonTextColor = "1 1 1 .7";
            SaveConfig();
        }

        void OnServerInitialized(bool initial)
        {
            RegisterImages();
        }

        private void Unload()
        {
            if (!Interface.Oxide.IsShuttingDown)
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                {
                    CuiHelper.DestroyUi(player, "WUIMainPanel"); 
                    CuiHelper.DestroyUi(player, "WUIDisplayPanel");
                }
            _config = null;
        }

        void OnWCRequestedUIPanel(BasePlayer player, string panelName, string neededPlugin)
        {
            if (!neededPlugin.Contains("WUIAttachments", CompareOptions.OrdinalIgnoreCase)) return;
            var requestedAttachment = neededPlugin.Substring(15);

            if (requestedAttachment.Equals("sociallinks", StringComparison.OrdinalIgnoreCase))
            {
                UIOpenSocial(player);
                return;
            }

            if (requestedAttachment.Equals("vipranks", StringComparison.OrdinalIgnoreCase))
            {
                UIOpenVIPRanks(player);
                return;
            }
           
            foreach (var qna in _config.QuestionAndAnswers)
            {
                if (qna.Identifer.Equals(requestedAttachment, StringComparison.OrdinalIgnoreCase)) UIOpenQNA(player, qna);
            }
        }
        #endregion

        #region UI
        void UIShowSocial(BasePlayer player, int index)
        {
            var theLink = _config.Links[index];

            var container = new CuiElementContainer();
            var panel = CreatePanel(ref container, "0 0", "1 1", _uiColors.PopupBackgroundColor, "Overlay", "WUIDisplayPanel", true);
            CreatePanel(ref container, "0 0", "1 1", _uiColors.PopupBackgroundColorOverlay, panel);

            var mainPanel = CreatePanel(ref container, ".275 .35", ".725 .65", _uiColors.PopupPanelColor, panel);

            CreatePanel(ref container, ".01 .25", ".3 .97", _uiColors.PopupPanelLabelColor, mainPanel);
            CreateImagePanel(ref container, ".04 .29", ".27 .94", GetImage($"SocialLinks-{index}"), mainPanel);
            CreateLabel(ref container, "0 1.01", ".99 1.3", "0 0 0 0", "1 1 1 1", theLink.Title, 40, TextAnchor.LowerLeft, mainPanel);
            CreatePanel(ref container, ".31 .25", ".99 .97", _uiColors.PopupPanelLabelColor, mainPanel);
            CreateLabel(ref container, ".34 .28", ".96 .94", "0 0 0 0", "1 1 1 1", theLink.Description, 20, TextAnchor.UpperLeft, mainPanel);
            var inputPanel = CreatePanel(ref container, ".01 .02", ".99 .23", _uiColors.PopupPanelLabelColor, mainPanel);
            CreateLabel(ref container, ".01 0", ".25 1", "0 0 0 0", "1 1 1 .05", _uiColors.CtrlCText, 30, TextAnchor.MiddleLeft, inputPanel);
            CreateInput(ref container, "0 0", "1 1", "", "0 0 0 0", _uiColors.PopupPanelLinkColor, theLink.ButtonLink, 25, TextAnchor.MiddleCenter, inputPanel);

            CreateButton(ref container, ".93 .8", "1 1", "0 0 0 0", "1 1 1 1", "X", 25, "wui_main closepopup", mainPanel);

            CuiHelper.DestroyUi(player, "WUIDisplayPanel");
            CuiHelper.AddUi(player, container);
        }

        void UIOpenSocial(BasePlayer player)
        {
            var container = new CuiElementContainer();
            var panel = CreatePanel(ref container, "0 0", "1 1", "0 0 0 0", "WCSocialsPanel", "WUIMainPanel");

            int i = 0;
            foreach (var link in _config.Links)
            {
                CreateImageButton(ref container, $"{.01 + (i * .0575)} .1", $"{.0475 + (i * .0575)} .9", link.ButtonColor, $"wui_main sociallinks {i}", GetImage($"SocialLinks-{i}"), panel);
                i++;
            }

            CuiHelper.DestroyUi(player, "WUIMainPanel");
            CuiHelper.AddUi(player, container);
        }

        void UIOpenQNA(BasePlayer player, QAConstructor qna, double theQuestionIndex = 100)
        {
            var container = new CuiElementContainer();
            var panel = CreatePanel(ref container, "0 0", "1 1", _uiColors.MainPanelColor, "WCSourcePanel", "WUIMainPanel");

            var buttonDepth = .1075;
            var space = .02;

            string qnaMainPanel;

            if (qna.Questions.Count > 5)
            {
                var panelDepth = buttonDepth * qna.Questions.Count + (buttonDepth * 3);
                space /= panelDepth;
                buttonDepth /= panelDepth;

                qnaMainPanel = CreateScrollPanel(ref container, "0 0", "1 .87", "0 0 0 0", $"{1 - panelDepth}", "WUIMainPanel", "WUIQNAScroll");
            }
            else
            {
                qnaMainPanel = CreatePanel(ref container, "0 0", "1 .87", "0 0 0 0", "WUIMainPanel", "WUIQNAScroll");
            }

            CreateLabel(ref container, ".015 .88", ".985 .98", _uiColors.TitleBackgroundColor, "1 1 1 1", qna.Title, 40, TextAnchor.MiddleCenter, panel);

            int i = 0;
            int ii = 0;
            foreach (var question in qna.Questions)
            {
                var yMax = .98 - (i * buttonDepth);
                var yMin = yMax - buttonDepth + space;

                var qnaPanel = CreatePanel(ref container, $".025 {yMin}", $".975 {yMax}", i % 2 == 0 ? _uiColors.QuestionBackgroundColor : _uiColors.QuestionBackgroundColor2, qnaMainPanel);

                if (theQuestionIndex == 100 || theQuestionIndex == i)
                {
                    theQuestionIndex = 0;
                    i += 3;
                    var txtPanel = CreatePanel(ref container, $".025 {yMin - (buttonDepth * 3)}", $".975 {yMax - buttonDepth + space}", _uiColors.AnswerBackgroundColor, qnaMainPanel);
                    CreateLabel(ref container, ".02 .02", ".98 .98", "0 0 0 0", "1 1 1 1", question.Answer, 20, TextAnchor.MiddleCenter, txtPanel);
                }

                var hasIcon = !string.IsNullOrEmpty(question.Icon);

                CreateLabel(ref container, hasIcon ? ".06 0" : ".01 0", "1 1", "0 0 0 0", "1 1 1 1", qna.Questions[ii].Question, 20, TextAnchor.MiddleLeft, qnaPanel);
                if(hasIcon) CreateImagePanel(ref container, ".005 .13", ".05 .85", GetImage($"{qna.Identifer.Replace("_", "-")}-{ii}"), qnaPanel);
                CreateLabel(ref container, ".95 0", "1 1", "1 1 1 0", "1 1 1 1", "+", 30, TextAnchor.MiddleCenter, qnaPanel);
                CreateButton(ref container, "0 0", "1 1", "0 0 0 0", "1 1 1 1", " ", 15, $"wui_main openquestion {ii} {qna.Identifer}", qnaPanel);
                i++;
                ii++;
            }

            CuiHelper.DestroyUi(player, "WUIMainPanel");
            CuiHelper.AddUi(player, container);
        }

        void UIOpenVIPRanks(BasePlayer player, int page = 0)
        {
            var container = new CuiElementContainer();
            var panel = CreatePanel(ref container, "0 0", "1 1", _uiColors.MainPanelColor, "WCSourcePanel", "WUIMainPanel");

            var maxPage = (_config.VipRanks.Count - 1) / 4;
            if (page > maxPage) page = 0;
            if (page < 0) page = maxPage;

            int i = 0;
            int ii = 0;
            var topHeight = .98;
            foreach (var rank in _config.VipRanks.Skip(page * 4).Take(4))
            {
                if (i == 2)
                {
                    i = 0;
                    topHeight = .4925;
                }

                var vipPanel = CreatePanel(ref container, $"{.06 + (i * .445)} {topHeight - .4725}", $"{.495 + (i * .445)} {topHeight}", "0 0 0 .6", panel);
                CreateImagePanel(ref container, ".02 .68", ".98 .97", GetImage($"VIPRanks-{(page == 0 ? ii : page * 4 + ii)}"), vipPanel);
                var txtPanel = CreatePanel(ref container, ".02 .2", ".98 .66", "0 0 0 .5", vipPanel);
                CreateButton(ref container, ".02 .02", ".975 .175", rank.ButtonColor, "1 1 1 1", rank.Title, 20, $"wui_main viprank {(page == 0 ? ii : page * 4 + ii)}", vipPanel);
                i++;
                ii++;

                var lineIndex = 0;
                var xMin = .02;
                foreach (var line in rank.Description.Take(10))
                {
                    if (lineIndex == 5)
                    {
                        xMin = .51;
                        lineIndex = 0;
                    }

                    CreateLabel(ref container, $"{xMin} {.78 - (lineIndex * .20)}", $"{xMin + .47} {.98 - (lineIndex * .20)}", "0 0 0 0", "1 1 1 1", line, 14, TextAnchor.MiddleLeft, txtPanel);
                    lineIndex++;
                }
            }

            if (_config.VipRanks.Count > 4)
            {
                CreateButton(ref container, ".01 .02", ".05 .98", "0 0 0 .5", "1 1 1 1", "<", 15, $"wui_main rankspage {page - 1}", panel);
                CreateButton(ref container, ".95 .02", ".99 .98", "0 0 0 .5", "1 1 1 1", ">", 15, $"wui_main rankspage {page + 1}", panel);
            }

            CuiHelper.DestroyUi(player, "WUIMainPanel");
            CuiHelper.AddUi(player, container);
        }

        void UIShowRank(BasePlayer player, int index)
        {
            var theLink = _config.VipRanks[index];

            var container = new CuiElementContainer();
            var panel = CreatePanel(ref container, "0 0", "1 1", _uiColors.PopupBackgroundColor, "Overlay", "WUIDisplayPanel", true);
            CreatePanel(ref container, "0 0", "1 1", _uiColors.PopupBackgroundColorOverlay, panel);

            var mainPanel = CreatePanel(ref container, ".3 .1", ".7 .9", _uiColors.PopupPanelColor, panel);
            CreateImagePanel(ref container, ".02 .79", ".98 .98", GetImage($"VIPRanks-{index}"), mainPanel);
            CreateLabel(ref container, ".02 .7", ".98 .78", _uiColors.PopupPanelLabelColor, "1 1 1 1", theLink.Title, 35, TextAnchor.MiddleCenter, mainPanel);

            var txtPanel = CreatePanel(ref container, ".02 .15", ".98 .69", _uiColors.PopupPanelLabelColor, mainPanel);

            var lineIndex = 0;
            var xMin = .02;
            foreach (var line in theLink.Description.Take(20))
            {
                if (lineIndex == 10)
                {
                    xMin = .51;
                    lineIndex = 0;
                }

                CreateLabel(ref container, $"{xMin} {.88 - (lineIndex * .10)}", $"{xMin + .47} {.98 - (lineIndex * .10)}", "0 0 0 0", "1 1 1 1", line, 17, TextAnchor.MiddleLeft, txtPanel);
                lineIndex++;
            }

            var txtLabel = CreateLabel(ref container, ".02 .02", ".98 .14", _uiColors.PopupPanelLabelColor, "1 1 1 .02", _uiColors.CtrlCText, 6, TextAnchor.MiddleCenter, mainPanel);
            CreateInput(ref container, "0 0", "1 1", "", "0 0 0 0", _uiColors.PopupPanelLinkColor, theLink.ButtonLink, 20, TextAnchor.MiddleCenter, txtLabel);
            CreateButton(ref container, ".9 .89", "1 1", "0 0 0 0", "1 1 1 1", "X", 35, "wui_main closepopup", mainPanel);

            CuiHelper.DestroyUi(player, "WUIDisplayPanel");
            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Methods
        string GetImage(string imageName)
        {
            var imgInfo = ImageLibrary?.Call<string>("GetImage", "WUI" + imageName, 0UL);
            return imgInfo;
        }

        private void RegisterImages()
        {
            Dictionary<string, string> commandImageList = new Dictionary<string, string>();
            foreach (var qna in _config.QuestionAndAnswers)
            {
                int i = 0;
                foreach (var question in qna.Questions)
                {
                    if(!string.IsNullOrEmpty(question.Icon)) commandImageList.Add($"WUI{qna.Identifer.Replace("_", "-")}-{i}", question.Icon);
                    i++;
                }
            }

            int ii = 0;
            foreach (var lk in _config.Links)
            {
                if (!string.IsNullOrEmpty(lk.Icon)) commandImageList.Add($"WUISocialLinks-{ii}", lk.Icon);
                ii++;
            }

            ii = 0;
            foreach (var lk in _config.VipRanks)
            {
                if (!string.IsNullOrEmpty(lk.Icon)) commandImageList.Add($"WUIVIPRanks-{ii}", lk.Icon);
                ii++;
            }


            commandImageList.Add("WUIBlueprintWipeIcon", _config.Images.BlueprintWipeIcon);
            commandImageList.Add("WUIMapWipeIcon", _config.Images.MapWipeIcon);

            ImageLibrary?.Call("ImportImageList", "WUIAttachments", commandImageList, 0UL, true);
        }
        #endregion

        #region UI Methods
        private static string CreateScrollPanel(ref CuiElementContainer container, string anchorMin, string anchorMax, string color, string panelDepth, string parentName, string panelName)
        {
            container.Add(new CuiElement()
            {
                Name = panelName,
                Parent = parentName,
                Components = {
                    new CuiImageComponent()
                    {
                        FadeIn = 0.2f,
                        Color = color
                    },
                    new CuiScrollViewComponent()
                    {
                        Horizontal = false,
                        Vertical = true,
                        MovementType = UnityEngine.UI.ScrollRect.MovementType.Elastic,
                        Elasticity = 0.25f,
                        Inertia = true,
                        DecelerationRate = 0.3f,
                        ScrollSensitivity = 30.0f,
                        ContentTransform = new CuiRectTransform()
                        {
                            AnchorMin = $"0 {panelDepth}",
                            AnchorMax = "1 1"
                        },
                        VerticalScrollbar = new CuiScrollbar {
                                Invert = false,
                                AutoHide = false,
                                HandleSprite = "assets/content/ui/ui.rounded.tga",
                                HandleColor = "1 1 1 .2",
                                HighlightColor = "0.17 0.17 0.17 .5",
                                TrackSprite = "assets/content/ui/ui.background.tile.psd",
                                TrackColor = ".09 .09 .09 .2",
                                Size = 3,
                                PressedColor = ".17 .17 .17 .7"
                        }
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = anchorMin,
                        AnchorMax = anchorMax
                    }
                }
            });

            return panelName;
        }

        private static string CreateLabel(ref CuiElementContainer container, string anchorMin, string anchorMax, string panelColor, string textColor, string labelText, int fontSize, TextAnchor alignment, string parent = "Overlay", string panelName = null)
        {
            var panel = CreatePanel(ref container, anchorMin, anchorMax, panelColor, parent, panelName);
            container.Add(new CuiLabel
            {
                Text =
                {
                    Color = textColor,
                    Text = labelText,
                    Align = alignment,
                    FontSize = fontSize,
                    Font = "robotocondensed-bold.ttf"
                }
            }, panel);
            return panel;
        }

        private static string CreatePanel(ref CuiElementContainer container, string anchorMin, string anchorMax, string panelColor, string parent = "Overlay", string panelName = null, bool blur = false, bool isMainPanel = false)
        {
            CuiPanel panel = new CuiPanel
            {
                RectTransform =
            {
                AnchorMin = anchorMin,
                AnchorMax = anchorMax
            },
                Image = { Color = panelColor }
            };

            if (blur) panel.Image.Material = "assets/content/ui/uibackgroundblur.mat";
            if (isMainPanel) panel.CursorEnabled = true;
            return container.Add(panel, parent, panelName);
        }

        private static void CreateImagePanel(ref CuiElementContainer container, string anchorMin, string anchorMax, string panelImage, string parent = "Overlay", string panelName = null, bool isUrl = false)
        {
            var panel = new CuiElement
            {
                Parent = parent,
                Name = panelName,
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = anchorMin,
                        AnchorMax = anchorMax
                    }
                }
            };

            if (isUrl) panel.Components.Add(new CuiRawImageComponent { Url = panelImage });
            else panel.Components.Add(new CuiRawImageComponent { Png = panelImage });

            container.Add(panel);
        }

        private static void CreateImageButton(ref CuiElementContainer container, string anchorMin, string anchorMax, string buttonColor, string buttonCommand, string panelImage, string parent = "Overlay", string panelName = null)
        {
            var panel = CreatePanel(ref container, anchorMin, anchorMax, buttonColor, parent, panelName);
            CreateImagePanel(ref container, "0 0", "1 1", panelImage, panel);

            container.Add(new CuiButton
            {
                Button = { Color = "0 0 0 0", Command = $"{buttonCommand}" }
            }, panel);
        }

        private static string CreateButton(ref CuiElementContainer container, string anchorMin, string anchorMax, string buttonColor, string textColor, string buttonText, int fontSize, string buttonCommand, string parent = "Overlay", TextAnchor labelAnchor = TextAnchor.MiddleCenter)
        {
            var panel = CreatePanel(ref container, anchorMin, anchorMax, "0 0 0 0", parent);

            container.Add(new CuiButton
            {
                Button = { Color = buttonColor, Command = $"{buttonCommand}" },
                Text = { Align = labelAnchor, Color = textColor, FontSize = fontSize, Text = buttonText }
            }, panel);
            return panel;
        }

        private static string CreateInput(ref CuiElementContainer container, string anchorMin, string anchorMax, string command, string backgroundColor, string textColor, string labelText, int fontSize, TextAnchor alignment, string parent = "Overlay", string labelName = null)
        {
            var panel = CreatePanel(ref container, anchorMin, anchorMax, backgroundColor, parent, labelName);

            container.Add(new CuiElement
            {
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Color = textColor,
                        Text = labelText,
                        Align = alignment,
                        FontSize = fontSize,
                        Font = "robotocondensed-bold.ttf",
                        NeedsKeyboard = true,
                        Command = command
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    }
                },
                Parent = panel
            });

            return panel;
        }
        #endregion
    }
}
