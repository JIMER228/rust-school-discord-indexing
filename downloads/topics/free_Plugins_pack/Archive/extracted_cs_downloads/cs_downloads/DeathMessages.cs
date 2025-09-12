using System.Collections.Generic;
using Newtonsoft.Json;
using Rust.Ai.Gen2;
using Facepunch;
using Oxide.Core;
using Rust;
using System.Linq;
//Reference: Unity.TextMeshPro
using System.Collections;
using UnityEngine;
using static Oxide.Plugins.DeathMessages;
using Oxide.Game.Rust.Cui;
using TMPro;
using System;

namespace Oxide.Plugins
{
    public static class RowOffsetCalculator
    {
        private const float BaseWidthOffset = -55;
        private const float WeaponSpacing = 18;
        private const float ElementSpacing = 5;
        private const float WeaponBaseOffset = 20;
        private const float BaseSideOffset = 5;
        private const float EndRowOffset = 15;
        /// <summary>
        /// Calculates the row offsets for displaying death messages.
        /// </summary>
        /// <param name = "deathRow">The DeathRow object containing computed widths and weapon mod icons.</param>
        /// <returns>A RowOffsets struct with calculated offset values.</returns>
        public static RowOffsets CalculateRowOffsets(DeathRow deathRow)
        {
            float width = BaseWidthOffset - BaseSideOffset - deathRow.VictimComputedWidth - deathRow.InitiatorComputedWidth - deathRow.MessageComputedWidth - WeaponBaseOffset - (deathRow.WeaponModsIcons.Count * WeaponSpacing) - BaseSideOffset - EndRowOffset;
            float victimOffsetMax = BaseWidthOffset - BaseSideOffset;
            float victimOffsetMin = victimOffsetMax - deathRow.VictimComputedWidth;
            float weaponOffsetMax = victimOffsetMin - ElementSpacing;
            float weaponOffsetMin = weaponOffsetMax - WeaponBaseOffset - (deathRow.WeaponModsIcons.Count * WeaponSpacing);
            float initiatorOffsetMax = weaponOffsetMin - ElementSpacing;
            float initiatorOffsetMin = initiatorOffsetMax - deathRow.InitiatorComputedWidth - deathRow.MessageComputedWidth - BaseSideOffset;
            return new RowOffsets()
            {
                Width = width,
                VictimOffsetMin = victimOffsetMin,
                VictimOffsetMax = victimOffsetMax,
                InitiatorOffsetMin = initiatorOffsetMin,
                InitiatorOffsetMax = initiatorOffsetMax,
                WeaponOffsetMin = weaponOffsetMin,
                WeaponOffsetMax = weaponOffsetMax
            };
        }

        /// <summary>
        /// Calculates the total width of a row for displaying death messages.
        /// </summary>
        /// <param name = "deathRow">The DeathRow object containing computed widths and weapon mod icons.</param>
        /// <returns>The total width of the row as a float.</returns>
        public static float CalculateRowWidth(DeathRow deathRow)
        {
            return BaseWidthOffset - BaseSideOffset - deathRow.VictimComputedWidth - deathRow.InitiatorComputedWidth - deathRow.MessageComputedWidth - WeaponBaseOffset - (deathRow.WeaponModsIcons.Count * WeaponSpacing) - BaseSideOffset - EndRowOffset;
        }
    }
}
namespace Oxide.Plugins
{
    [Info("DeathMessages", "VooDoo", "2.1.3")]
    public partial class DeathMessages : RustPlugin
    {

        private void CreateNote(string initiator, string victim, float distance, int weaponIcon, List<int> weaponMods, DamageType damageType)
        {
            new DeathRow(initiator, victim, distance.ToString("F1") + "m", weaponIcon, weaponMods, damageType).Run();
        }

        private void AddUIRows(BasePlayer player)
        {
            if (Instance.PlayersData.TryGetValue(player.userID, out PlayerData playerData))
            {
                if (playerData.HideMessages)
                    return;
            }

            CuiElementContainer container = new CuiElementContainer();
            for (int i = Instance.DeathMessagesConfig.UIConfig.Rows - 1; i >= 0; i--)
            {
                if (i < DeathRow.DeathRows.Count)
                {
                    int currentRowIndex = (LAST_ROW_ID + i) % Instance.DeathMessagesConfig.UIConfig.Rows;
                    RowOffsets rowOffsets = RowOffsetCalculator.CalculateRowOffsets(DeathRow.DeathRows[i]);
                    container.Add(new CuiElement { Name = PANEL_NAME + "R" + currentRowIndex, Parent = PANEL_NAME, Components = { new CuiRectTransformComponent { OffsetMin = $"{rowOffsets.Width} {-20 - i * 25}", OffsetMax = $"0 {0 - i * 25}" }, }, Update = true });
                    container.Add(new CuiElement { Name = $"{PANEL_NAME}R{currentRowIndex}DT", Components = { new CuiTextComponent { Text = DeathRow.DeathRows[i].Distance }, }, Update = true });
                    container.Add(new CuiElement { Name = $"{PANEL_NAME}R{currentRowIndex}KV", Components = { new CuiTextComponent { Text = DeathRow.DeathRows[i].VictimMessage }, new CuiRectTransformComponent { OffsetMin = $"{rowOffsets.VictimOffsetMin} 0", OffsetMax = $"{rowOffsets.VictimOffsetMax} 20" }, }, Update = true });
                    container.Add(new CuiElement { Name = $"{PANEL_NAME}R{currentRowIndex}WP", Components = { new CuiRectTransformComponent { OffsetMin = $"{rowOffsets.WeaponOffsetMin} 0", OffsetMax = $"{rowOffsets.WeaponOffsetMax} 20" }, }, Update = true });
                    container.Add(new CuiElement { Name = $"{PANEL_NAME}R{currentRowIndex}IW", Components = { new CuiImageComponent { ItemId = DeathRow.DeathRows[i].WeaponIcon }, }, Update = true });
                    for (int j = 0; j < DeathRow.DeathRows[i].WeaponModsIcons.Count; j++)
                    {
                        container.Add(new CuiElement { Name = $"{PANEL_NAME}R{currentRowIndex}IM{5 - j}", Components = { new CuiImageComponent { ItemId = DeathRow.DeathRows[i].WeaponModsIcons[j] }, }, Update = true });
                    }

                    container.Add(new CuiElement { Name = $"{PANEL_NAME}R{currentRowIndex}K", Components = { new CuiTextComponent { Text = Instance.DeathMessagesConfig.UIConfig.Image.Center ? DeathRow.DeathRows[i].InitiatorMessage : DeathRow.DeathRows[i].Message }, new CuiRectTransformComponent { OffsetMin = $"{rowOffsets.InitiatorOffsetMin} 0", OffsetMax = $"{rowOffsets.InitiatorOffsetMax} 20" }, }, Update = true });
                }
            }
		   		 		  						  	   		   					  	  			  		 			  		 	
            CuiHelper.AddUi(player, container);
        }
		   		 		  						  	   		   					  	  			  		 			  		 	
        private string CreateUITemplate()
        {
            CuiElementContainer container = new CuiElementContainer();
            container.Add(new CuiElement { Name = PANEL_NAME, Parent = Instance.DeathMessagesConfig.UIConfig.UILayer, Components = { new CuiRectTransformComponent { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = $"{-385 - Instance.DeathMessagesConfig.UIConfig.OffsetX} {-250 - Instance.DeathMessagesConfig.UIConfig.OffsetY}", OffsetMax = $"{-5 - Instance.DeathMessagesConfig.UIConfig.OffsetX} {-5 - Instance.DeathMessagesConfig.UIConfig.OffsetY}" }, new CuiImageComponent { Color = "0 0 0 0" }, } });
            for (int i = 0; i < Instance.DeathMessagesConfig.UIConfig.Rows; i++)
            {
                container.Add(new CuiElement { Name = PANEL_NAME + "R" + i, Parent = PANEL_NAME, Components = { new CuiRectTransformComponent { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = $"380 {-20 - i * 25}", OffsetMax = $"380 {0 - i * 25}" }, new CuiImageComponent { Color = HexToRGBA(Instance.DeathMessagesConfig.UIConfig.Row.PanelColor) }, } });
                container.Add(new CuiElement { Name = PANEL_NAME + "R" + i + "D", Parent = PANEL_NAME + "R" + i, Components = { new CuiRectTransformComponent { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = Instance.DeathMessagesConfig.UIConfig.Distance.Circle == false ? "-55 0" : $"-55 -20", OffsetMax = Instance.DeathMessagesConfig.UIConfig.Distance.Circle == false ? "0 20" : $"5 40" }, new CuiRawImageComponent { Color = HexToRGBA(Instance.DeathMessagesConfig.UIConfig.Distance.PanelColor), Material = Instance.DeathMessagesConfig.UIConfig.Distance.Circle == false ? "" : "assets/icons/iconmaterial.mat", Sprite = Instance.DeathMessagesConfig.UIConfig.Distance.Circle == false ? "" : $"assets/icons/subtract.png", }, } });
                container.Add(new CuiElement { Name = PANEL_NAME + "R" + i + "D" + "T", Parent = PANEL_NAME + "R" + i, Components = { new CuiTextComponent { Text = "", Color = HexToRGBA(Instance.DeathMessagesConfig.UIConfig.Distance.Color), Align = TextAnchor.MiddleCenter, FontSize = Instance.DeathMessagesConfig.UIConfig.Distance.Size, Font = "robotocondensed-bold.ttf", }, new CuiRectTransformComponent { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = $"-55 0", OffsetMax = Instance.DeathMessagesConfig.UIConfig.Distance.Circle == false ? "0 20" : $"5 20" } } });
                if (Instance.DeathMessagesConfig.UIConfig.Distance.Outline)
                    container[container.Count - 1].Components.Add(new CuiOutlineComponent { Color = HexToRGBA(Instance.DeathMessagesConfig.UIConfig.Distance.OutlineColor), Distance = "-0.5 0.5" });
                container.Add(new CuiElement { Name = PANEL_NAME + "R" + i + "WP", Parent = PANEL_NAME + "R" + i, Components = { new CuiScrollViewComponent { Vertical = false, Horizontal = true, MovementType = UnityEngine.UI.ScrollRect.MovementType.Unrestricted, ContentTransform = new CuiRectTransform { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "0 0", OffsetMax = "113 20" }, HorizontalScrollbar = new CuiScrollbar { Size = 0, HandleColor = "0 0 0 0", HighlightColor = "0 0 0 0", PressedColor = "0 0 0 0", TrackColor = "0 0 0 0", }, VerticalScrollbar = new CuiScrollbar { Size = 0, HandleColor = "0 0 0 0", HighlightColor = "0 0 0 0", PressedColor = "0 0 0 0", TrackColor = "0 0 0 0", } }, new CuiImageComponent { Color = "0 0 0 0", }, new CuiRectTransformComponent { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = $"{192} 0", OffsetMax = $"{325} 20" }, } });
                container.Add(new CuiElement { Name = PANEL_NAME + "R" + i + "I" + "W", Parent = PANEL_NAME + "R" + i + "WP", Components = { new CuiImageComponent { ItemId = 1545779598, Color = "1 1 1 1", }, new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = $"{1} 1", OffsetMax = $"{19} 19" } } });
                if (Instance.DeathMessagesConfig.UIConfig.Image.Outline)
                    container[container.Count - 1].Components.Add(new CuiOutlineComponent { Color = HexToRGBA(Instance.DeathMessagesConfig.UIConfig.Image.OutlineColor), Distance = "-0.5 0.5" });
                for (int j = 0; j < 6; j++)
                {
                    container.Add(new CuiElement { Name = PANEL_NAME + "R" + i + "I" + "M" + j, Parent = PANEL_NAME + "R" + i + "WP", Components = { new CuiImageComponent { ItemId = 952603248, Color = "1 1 1 1", }, new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = $"{112 - j * 18} 2", OffsetMax = $"{128 - j * 18} 18" } } });
                    if (Instance.DeathMessagesConfig.UIConfig.Image.Outline)
                        container[container.Count - 1].Components.Add(new CuiOutlineComponent { Color = HexToRGBA(Instance.DeathMessagesConfig.UIConfig.Image.OutlineColor), Distance = "-0.5 0.5" });
                }

                container.Add(new CuiElement { Name = PANEL_NAME + "R" + i + "K", Parent = PANEL_NAME + "R" + i, Components = { new CuiTextComponent { Text = "Initiator", Color = HexToRGBA(Instance.DeathMessagesConfig.UIConfig.Row.TextColor), Align = TextAnchor.MiddleCenter, FontSize = Instance.DeathMessagesConfig.UIConfig.Row.TextSize, Font = "robotocondensed-bold.ttf", }, new CuiRectTransformComponent { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = $"0 0", OffsetMax = $"190 20" } } });
                if (Instance.DeathMessagesConfig.UIConfig.Row.Outline)
                    container[container.Count - 1].Components.Add(new CuiOutlineComponent { Color = HexToRGBA(Instance.DeathMessagesConfig.UIConfig.Row.OutlineColor), Distance = "-0.5 0.5" });
                container.Add(new CuiElement { Name = PANEL_NAME + "R" + i + "KV", Parent = PANEL_NAME + "R" + i, Components = { new CuiTextComponent { Text = "Victim", Color = HexToRGBA(Instance.DeathMessagesConfig.UIConfig.Row.TextColor), Align = TextAnchor.MiddleCenter, FontSize = Instance.DeathMessagesConfig.UIConfig.Row.TextSize, Font = "robotocondensed-bold.ttf", }, new CuiRectTransformComponent { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = $"0 0", OffsetMax = $"190 20" } } });
                if (Instance.DeathMessagesConfig.UIConfig.Row.Outline)
                    container[container.Count - 1].Components.Add(new CuiOutlineComponent { Color = HexToRGBA(Instance.DeathMessagesConfig.UIConfig.Row.OutlineColor), Distance = "-0.5 0.5" });
            }

            return container.ToJson();
        }

        private void SetDefaultConfig()
        {
            DeathMessagesConfig = GetDefaultConfig();
            Config.WriteObject(DeathMessagesConfig, true);
        }

        private bool? SwitchHideMessages(BasePlayer player)
        {
            if (PlayersData.TryGetValue(player.userID, out PlayerData playerData))
            {
                var nowSeconds = new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds();
                if (playerData.LastConnectTime + 15000 > nowSeconds)
                {
                    SendReply(player, $"This option is not yet available for modification. It will be accessible in: {(playerData.LastConnectTime + 15000 - nowSeconds) / 1000} sec.");
                    return null;
                }

                playerData.HideMessages = !playerData.HideMessages;
                playerData.LastConnectTime = nowSeconds;
                if (playerData.HideMessages == false)
                {
                    AddTemplateUI(player);
                    AddUIRows(player);
                }
                else
                {
                    RemoveUITemplate(player);
                }
            }
            else
            {
                PlayersData[player.userID] = playerData = new PlayerData()
                {
                    HideMessages = true,
                    HideMyName = false,
                    LastConnectTime = new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds()
                };
                RemoveUITemplate(player);
            }

            return playerData.HideMessages;
        }

        public DeathMessagesConfiguration DeathMessagesConfig;

        public DeathMessagesConfiguration GetDefaultConfig()
        {
            return new DeathMessagesConfiguration
            {
                CoreConfig = new DeathMessagesConfiguration.CoreConfiguration()
                {
                    Names = Names,
                    Messages = Messages,
                    Animal = true,
                    NPC = true,
                    PlayerKilledByAnimal = true,
                    PlayerKilledByNPC = true,
                    PlayerKilledByEnv = true,
                    RenameNPC = true,
                    Bradley = true,
                    Helicopter = true,
                    PlayerSuicide = true,
                },
                UIConfig = new DeathMessagesConfiguration.UIConfiguration
                {
                    Row = new DeathMessagesConfiguration.RowPanelProperty()
                    {
                        Size = 11,
                        TextSize = 10,
                        Color = "#D3D3D3",
                        TextColor = "#F0F0F0",
                        Outline = true,
                        OutlineColor = "#000000",
                        PanelColor = "#000000A0",
                    },
                    Image = new DeathMessagesConfiguration.ImagePanelProperty()
                    {
                        Outline = true,
                        OutlineColor = "#000000",
                        Center = true,
                        Mods = true
                    },
                    Distance = new DeathMessagesConfiguration.DistancePanelProperty()
                    {
                        Size = 11,
                        Color = "FFFFFF60",
                        Outline = true,
                        OutlineColor = "#000000",
                        PanelColor = "#FFFFFF60",
                        Circle = true,
                    },
                    Colors = new Dictionary<string, string>()
                    {
                        ["deathmessages.deluxe"] = "#7303c0",
                        ["deathmessages.vip"] = "#f9ff55",
                        ["deathmessages.premium"] = "#55ff8a",
                    },
                    Gradients = new Dictionary<string, string[]>()
                    {
                        ["deathmessages.gradient"] = new string[]
                        {
                            "#0ebeff",
                            "#29b0f7",
                            "#44a2ee",
                            "#5e95e6",
                            "#7987dd",
                            "#9479d5",
                            "#af6bcc",
                            "#c95ec4",
                            "#e450bb",
                            "#ff42b3"
                        },
                    },
                    Rows = 5,
                    Time = 5f,
                    OffsetX = 0,
                    OffsetY = 0,
                    EnableUISettings = true,
                    EnableUISettingsButton = true,
                    UILayer = "Hud",
                    NameSize = 16,
                },
            };
        }

        private void RemoveUITemplate(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, PANEL_NAME);
        }

        private void SwitchPlayerNameHidden(BasePlayer player)
        {
            SwitchHideMyName(player);
        }

        private void OnEntityDeath(BaseCombatEntity victim, HitInfo info)
        {
            if (victim == null)
                return;
            if (victim.IsDestroyed || victim.IsValid() == false)
                return;
            if (info == null)
            {
                if (victim.IsNonNpcPlayer())
                    new DeathRow(victim as BasePlayer, null).Run();
                return;
            }

            if (info.damageTypes == null)
                return;
            DeathRow outputRow = null;
            if (info.Initiator != null && info.Initiator is BaseCombatEntity)
            {
                if (info.Initiator.IsDestroyed || info.Initiator.IsValid() == false)
                    return;
                if (victim is BasePlayer && info.Initiator is BasePlayer)
                {
                    if (victim.IsNonNpcPlayer() && info.Initiator.IsNpcPlayer())
                    {
                        outputRow = new DeathRow(victim as BasePlayer, info.Initiator as NPCPlayer, info);
                    }

                    if (victim.IsNpcPlayer() && info.Initiator.IsNonNpcPlayer())
                    {
                        outputRow = new DeathRow(victim as NPCPlayer, info.Initiator as BasePlayer, info);
                    }

                    if (victim.IsNonNpcPlayer() && info.Initiator.IsNonNpcPlayer())
                    {
                        outputRow = new DeathRow(victim as BasePlayer, info.Initiator as BasePlayer, info);
                    }
                }
		   		 		  						  	   		   					  	  			  		 			  		 	
                if (victim is BaseAnimalNPC || victim is BaseNPC2 || victim is SimpleShark)
                {
                    if (info.Initiator.IsNonNpcPlayer())
                    {
                        outputRow = new DeathRow(victim, info.Initiator as BasePlayer, info);
                    }
                }

                if (victim is BradleyAPC bradleyAPC && info.Initiator.IsNonNpcPlayer())
                {
                    outputRow = new DeathRow(bradleyAPC, info.Initiator as BasePlayer, info);
                }

                if (victim.IsNonNpcPlayer())
                {
                    if (info.Initiator is BaseAnimalNPC || info.Initiator is BaseNPC2 || info.Initiator is SimpleShark)
                    {
                        outputRow = new DeathRow(victim as BasePlayer, info.Initiator as BaseCombatEntity, info);
                    }

                    if (info.Initiator is DecayEntity)
                    {
                        outputRow = new DeathRow(victim as BasePlayer, info.Initiator as DecayEntity, info);
                    }
                }
            }

            if (info.Initiator == null)
            {
                if (victim.IsNonNpcPlayer())
                    outputRow = new DeathRow(victim as BasePlayer, info);
            }

            outputRow?.Run();
        }
        public class StringCalculation
        {
            internal TextGenerator Generator { get; private set; }
            internal TextGenerationSettings GeneratorSettings { get; private set; }

            public StringCalculation()
            {
                Generator = new TextGenerator();
                GeneratorSettings = new TextGenerationSettings()
                {
                    textAnchor = TextAnchor.UpperLeft,
                    color = Color.black,
                    font = Instance.DynamicUIComponent.GetFont(),
                    fontSize = Instance.DeathMessagesConfig.UIConfig.Row.Size,
                    lineSpacing = 1,
                    richText = true,
                    scaleFactor = 1,
                    updateBounds = false,
                    horizontalOverflow = HorizontalWrapMode.Wrap,
                    verticalOverflow = VerticalWrapMode.Truncate,
                };
            }
		   		 		  						  	   		   					  	  			  		 			  		 	
            public float GetStringWidth(string value)
            {
                return Generator.GetPreferredWidth(value, GeneratorSettings) + 5;
            }
        }

        private void CreateNote(string initiator, string victim, float distance, int weaponIcon, List<int> weaponMods)
        {
            new DeathRow(initiator, victim, distance.ToString("F1") + "m", weaponIcon, weaponMods).Run();
        }
        internal double LAST_ROW_MODIFY_TIME = 0;
        public class DeathMessagesConfiguration
        {

            public class ImagePanelProperty
            {
                [JsonProperty("[1] Включить обводку иконок")]
                public bool Outline = true;
                [JsonProperty("[2] Цвет обводки иконок")]
                public string OutlineColor = "#000000";
                [JsonProperty("[3] Размещать иконки по центру (Initiator [x] Victim)")]
                public bool Center = true;
                [JsonProperty("[4] Показывать модули")]
                public bool Mods = true;
            }

            public class DistancePanelProperty
            {
                [JsonProperty("[1] Размер")]
                public int Size = 11;
                [JsonProperty("[2] Цвет панели")]
                public string PanelColor = "#FFFFFF60";
                [JsonProperty("[3] Цвет текста")]
                public string Color = "#FFFFFF";
                [JsonProperty("[4] Включить обводку текста")]
                public bool Outline = true;
                [JsonProperty("[5] Цвет обводки текста")]
                public string OutlineColor = "#000000";
                [JsonProperty("[6] Использовать закругления углов панели")]
                public bool Circle = true;
            }
            public class CoreConfiguration
            {
                [JsonProperty("[5] Заменять имена NPC на имена из конфига")]
                public bool RenameNPC = true;
                [JsonProperty("[4.2] Показывать, когда игрок умирает от окружения")]
                public bool PlayerKilledByEnv = true;
                [JsonProperty("[2] Варианты сообщений для киллбара")]
                public Dictionary<string, List<string>> Messages;
                [JsonProperty("[1] Названия NPC")]
                public Dictionary<string, string> Names;
                [JsonProperty("[3.2] Показывать смерть NPC")]
                public bool NPC = true;
                [JsonProperty("[4.3] Показывать, когда животные убивают игрока")]
                public bool PlayerKilledByAnimal = true;
                [JsonProperty("[4.4] Показывать суицид")]
                public bool PlayerSuicide = true;
                [JsonProperty("[3.1] Показывать смерть животных")]
                public bool Animal = true;
                [JsonProperty("[3.4] Показывать смерть BradleyAPC")]
                public bool Bradley = true;
                [JsonProperty("[3.3] Показывать смерть Helicopter")]
                public bool Helicopter = true;
                [JsonProperty("[4.1] Показывать, когда NPC убивают игрока")]
                public bool PlayerKilledByNPC = true;
            }
            [JsonProperty("[1] Настройки плагина")]
            public CoreConfiguration CoreConfig = new CoreConfiguration();

            public class RowPanelProperty
            {
                [JsonProperty("[1] Размер имен в панели")]
                public int Size = 11;
                [JsonProperty("[2] Размер текста в панели")]
                public int TextSize = 10;
                [JsonProperty("[3] Цвет имен в панели")]
                public string Color = "#D3D3D3";
                [JsonProperty("[4] Цвет текста в панели")]
                public string TextColor = "#F0F0F0";
                [JsonProperty("[5] Включить обводку текста")]
                public bool Outline = true;
                [JsonProperty("[6] Цвет обводки текста")]
                public string OutlineColor = "#000000";
                [JsonProperty("[7] Цвет панели")]
                public string PanelColor = "#000000A0";
            }

            public class UIConfiguration
            {
                [JsonProperty("[1.1] Настройка строки")]
                public RowPanelProperty Row = new RowPanelProperty();
                [JsonProperty("[1.2] Настройка иконок")]
                public ImagePanelProperty Image = new ImagePanelProperty();
                [JsonProperty("[1.3] Дистанция")]
                public DistancePanelProperty Distance = new DistancePanelProperty();
                [JsonProperty("[2] Настройка цвета ника по привилегиям")]
                public Dictionary<string, string> Colors = new Dictionary<string, string>();
                [JsonProperty("[2.1] Настройка градиента ника по привилегиям")]
                public Dictionary<string, string[]> Gradients = new Dictionary<string, string[]>();
                [JsonProperty("[3] Количество строк")]
                public int Rows = 5;
                [JsonProperty("[4] Время, через которое пропадёт последняя строка")]
                public float Time = 5;
                [JsonProperty("[5.1] Отступ сверху в пикселях (при разрешении 1360x768)")]
                public int OffsetY = 0;
                [JsonProperty("[5.2] Отступ сбоку в пикселях (при разрешении 1360x768)")]
                public int OffsetX = 0;
                [JsonProperty("[6.1] Включить возможность скрыть имя и/или весь UI")]
                public bool EnableUISettings = true;
                [JsonProperty("[6.2] Отображать иконку настроек")]
                public bool EnableUISettingsButton = true;
                [JsonProperty("[7] Слой UI")]
                public string UILayer = "Hud";
                [JsonProperty("[8] Максимальная длина ника игрока")]
                public int NameSize = 16;
            }
            [JsonProperty("[2] Настройки UI")]
            public UIConfiguration UIConfig = new UIConfiguration();
        }
		   		 		  						  	   		   					  	  			  		 			  		 	
        internal string GetNpcName(string shortPrefabName)
        {
            if (DeathMessagesConfig.CoreConfig.Names.TryGetValue(shortPrefabName, out string name))
                return name;
            return shortPrefabName;
        }

        private bool IsPlayerHideMessages(BasePlayer player)
        {
            return PlayersData.TryGetValue(player.userID, out PlayerData playerData) && playerData.HideMessages;
        }

        public struct RowOffsets
        {
            public float Width;
            public float VictimOffsetMin;
            public float VictimOffsetMax;
            public float InitiatorOffsetMin;
            public float InitiatorOffsetMax;
            public float WeaponOffsetMin;
            public float WeaponOffsetMax;
        }

        [ConsoleCommand("deathmessages")]
        private void CMDMain(ConsoleSystem.Arg arg)
        {
            if (Instance.DeathMessagesConfig.UIConfig.EnableUISettings == false || Instance.DeathMessagesConfig.UIConfig.EnableUISettingsButton == false)
                return;
            BasePlayer player = arg.Player();
            if (player != null && arg.HasArgs())
            {
                string command = arg.Args[0];
                switch (command)
                {
                    case "show":
                    {
                        UpdateUISettingsButton(player, "hide");
                        AddUISettingsWidget(player);
                        break;
                    }

                    case "hide":
                    {
                        CuiHelper.DestroyUi(player, "DMUISettings");
                        UpdateUISettingsButton(player, "show");
                        break;
                    }

                    case "hidemyname":
                    {
                        SwitchHideMyName(player);
                        UpdateUISettingsWidget(player);
                        break;
                    }

                    case "hidemessages":
                    {
                        SwitchHideMessages(player);
                        UpdateUISettingsWidget(player);
                        break;
                    }
                }
            }
        }

        private void Init()
        {
            DeathMessagesConfig = Config.ReadObject<DeathMessagesConfiguration>();
            if (DeathMessagesConfig.CoreConfig == null || DeathMessagesConfig.UIConfig == null || DeathMessagesConfig.CoreConfig.Names == null || DeathMessagesConfig.CoreConfig.Names.Count == 0 || DeathMessagesConfig.CoreConfig.Messages == null || DeathMessagesConfig.CoreConfig.Messages.Count == 0 || DeathMessagesConfig.UIConfig.Colors == null)
            {
                SetDefaultConfig();
                return;
            }

            Config.WriteObject(DeathMessagesConfig, true);
        }

        private void SwitchPlayerHideMessages(BasePlayer player)
        {
            SwitchHideMessages(player);
        }

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(GetDefaultConfig(), true);
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
        }

        private void OnServerInitialized()
        {
            Instance = this;
            PlayersData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerData>>("DeathMessages\\PlayersData");
            if (PlayersData == null)
                PlayersData = new Dictionary<ulong, PlayerData>();
            List<ulong> tempData = new List<ulong>();
            foreach (var playerData in PlayersData)
            {
                if (playerData.Value.LastConnectTime + 604800 < new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds())
                    tempData.Add(playerData.Key);
            }

            foreach (var userID in tempData)
                PlayersData.Remove(userID);
            foreach (var groupColor in DeathMessagesConfig.UIConfig.Colors)
            {
                permission.RegisterPermission(groupColor.Key, this);
            }

            foreach (var groupColor in DeathMessagesConfig.UIConfig.Gradients)
            {
                permission.RegisterPermission(groupColor.Key, this);
            }

            foreach (var player in BasePlayer.activePlayerList)
            {
                RemoveUITemplate(player);
                AddTemplateUI(player);
                RemoveUISettings(player);
                if (Instance.DeathMessagesConfig.UIConfig.EnableUISettings && Instance.DeathMessagesConfig.UIConfig.EnableUISettingsButton)
                    AddUISettings(player);
            }

            foreach (ItemDefinition itemDefinition in ItemManager.GetItemDefinitions())
            {
                Item cacheItem = ItemManager.CreateByName(itemDefinition.shortname, 1, 0);
                BaseEntity entity = cacheItem.GetHeldEntity();
                if (entity != null)
                {
                    Prefab2Item[entity.prefabID] = itemDefinition.itemid;
                    PrefabName2Item[entity.ShortPrefabName] = itemDefinition.itemid;
                    PrefabName2Item[entity.ShortPrefabName.Replace(".entity", ".deployed")] = itemDefinition.itemid;
                    PrefabName2Item[entity.ShortPrefabName + ".deployed"] = itemDefinition.itemid;
                }

                if (itemDefinition.HasComponent<ItemModDeployable>())
                {
                    string deployablePrefab = itemDefinition.GetComponent<ItemModDeployable>()?.entityPrefab?.resourcePath;
                    if (string.IsNullOrEmpty(deployablePrefab) == false)
                    {
                        string shortPrefabName = GameManager.server.FindPrefab(deployablePrefab)?.GetComponent<BaseEntity>()?.ShortPrefabName;
                        if (string.IsNullOrEmpty(shortPrefabName) == false)
                        {
                            PrefabName2Item[shortPrefabName] = itemDefinition.itemid;
                        }
                    }
                }

                cacheItem.Remove();
            }

            if (DynamicUIObject == null)
            {
                DynamicUIObject = new GameObject();
                DynamicUIComponent = DynamicUIObject.AddComponent<DynamicUI>();
            }

            timer.Every(1.0f, () =>
            {
                try
                {
                    RemoveLastUIRow();
                }
                catch (Exception ex)
                {
                    PrintError(ex.ToString());
                }
            });
        }
        internal Dictionary<uint, int> Prefab2Item = new Dictionary<uint, int>();
		   		 		  						  	   		   					  	  			  		 			  		 	
        internal static DeathMessages Instance;
		   		 		  						  	   		   					  	  			  		 			  		 	
        [Obsolete]
        private void CreateNote(string message, float distance, int weaponIcon, List<int> weaponMods)
        {
            PrintWarning("API Method CreateNote(string message, float distance, int weaponIcon, List<int> weaponMods) deprecate.");
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            AddTemplateUI(player);
            AddUIRows(player);
            if (PlayersData.TryGetValue(player.userID, out PlayerData playerData))
                playerData.LastConnectTime = new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds();
            if (Instance.DeathMessagesConfig.UIConfig.EnableUISettings && Instance.DeathMessagesConfig.UIConfig.EnableUISettingsButton)
                AddUISettings(player);
        }

        private void AddUISettings(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();
            container.Add(new CuiElement { Name = "DMUI", Parent = "Overlay", Components = { new CuiRectTransformComponent { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = $"{-385} {-25}", OffsetMax = $"{-365} {-5}" }, new CuiImageComponent { Color = "1 1 1 0.8", Sprite = "assets/icons/tools.png", Material = "assets/icons/iconmaterial.mat", } } });
            container[container.Count - 1].Components.Add(new CuiOutlineComponent { Color = "0 0 0 1", Distance = "-0.5 0.5" });
            container.Add(new CuiButton { Button = { Command = $"deathmessages show", Color = "0 0 0 0", }, RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", }, Text = { Text = "", }, }, "DMUI", "DMUI.Button");
            CuiHelper.AddUi(player, container);
        }
        private void AddUIRow(DeathRow deathRow)
        {
            LAST_ROW_MODIFY_TIME = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            CuiElementContainer container = new CuiElementContainer();
            for (int i = Instance.DeathMessagesConfig.UIConfig.Rows - 1; i > 0; i--)
            {
                if (i < DeathRow.DeathRows.Count)
                {
                    container.Add(new CuiElement { Name = $"{PANEL_NAME}R{(LAST_ROW_ID + i - 1) % Instance.DeathMessagesConfig.UIConfig.Rows}", Components = { new CuiRectTransformComponent { OffsetMin = $"{RowOffsetCalculator.CalculateRowWidth(DeathRow.DeathRows[i])} {-20 - i * 25}", OffsetMax = $"0 {0 - i * 25}" } }, Update = true });
                }
                else
                {
                    container.Add(new CuiElement { Name = $"{PANEL_NAME}R{(LAST_ROW_ID + i - 1) % Instance.DeathMessagesConfig.UIConfig.Rows}", Components = { new CuiRectTransformComponent { OffsetMin = $"1380 1000", OffsetMax = $"1380 1000" } }, Update = true });
                }
            }

            LAST_ROW_ID = (LAST_ROW_ID - 1 + Instance.DeathMessagesConfig.UIConfig.Rows) % Instance.DeathMessagesConfig.UIConfig.Rows;
            RowOffsets rowOffsets = RowOffsetCalculator.CalculateRowOffsets(deathRow);
            // ROW
            container.Add(new CuiElement { Name = $"{PANEL_NAME}R{LAST_ROW_ID}", Components = { new CuiRectTransformComponent { OffsetMin = $"{rowOffsets.Width} -20", OffsetMax = "0 0" } }, Update = true });
            // Distance Text
            container.Add(new CuiElement { Name = $"{PANEL_NAME}R{LAST_ROW_ID}DT", Components = { new CuiTextComponent { Text = deathRow.Distance }, }, Update = true });
            // Victim Message
            container.Add(new CuiElement { Name = $"{PANEL_NAME}R{LAST_ROW_ID}KV", Components = { new CuiTextComponent { Text = deathRow.VictimMessage }, new CuiRectTransformComponent { OffsetMin = $"{rowOffsets.VictimOffsetMin} 0", OffsetMax = $"{rowOffsets.VictimOffsetMax} 20" }, }, Update = true });
            // WeaponIcon Placeholder
            container.Add(new CuiElement { Name = $"{PANEL_NAME}R{LAST_ROW_ID}WP", Components = { new CuiRectTransformComponent { OffsetMin = $"{rowOffsets.WeaponOffsetMin} 0", OffsetMax = $"{rowOffsets.WeaponOffsetMax} 20" }, }, Update = true });
            // WeaponIcon
            container.Add(new CuiElement { Name = $"{PANEL_NAME}R{LAST_ROW_ID}IW", Components = { new CuiImageComponent { ItemId = deathRow.WeaponIcon }, }, Update = true });
            // WeaponModIcons
            for (int j = 0; j < deathRow.WeaponModsIcons.Count; j++)
            {
                container.Add(new CuiElement { Name = $"{PANEL_NAME}R{LAST_ROW_ID}IM{5 - j}", Components = { new CuiImageComponent { ItemId = deathRow.WeaponModsIcons[j] }, }, Update = true });
            }

            // Initiator or Full Message
            container.Add(new CuiElement { Name = $"{PANEL_NAME}R{LAST_ROW_ID}K", Components = { new CuiTextComponent { Text = Instance.DeathMessagesConfig.UIConfig.Image.Center ? deathRow.InitiatorMessage : deathRow.Message }, new CuiRectTransformComponent { OffsetMin = $"{rowOffsets.InitiatorOffsetMin} 0", OffsetMax = $"{rowOffsets.InitiatorOffsetMax} 20" }, }, Update = true });
            string jsonContainer = container.ToJson();
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (Instance.PlayersData.TryGetValue(player.userID, out PlayerData playerData))
                {
                    if (playerData.HideMessages)
                        continue;
                }
		   		 		  						  	   		   					  	  			  		 			  		 	
                CuiHelper.AddUi(player, jsonContainer);
            }
        }
        internal Dictionary<ulong, HitInfo> LastAttacks = new Dictionary<ulong, HitInfo>();
		   		 		  						  	   		   					  	  			  		 			  		 	
        internal string[] GetGradientFor(BasePlayer player)
        {
            foreach (var color in Instance.DeathMessagesConfig.UIConfig.Gradients)
            {
                if (Instance.permission.UserHasPermission(player.UserIDString, color.Key))
                {
                    return color.Value;
                }
            }

            return null;
        }

        private void AddTemplateUI(BasePlayer player)
        {
            if (Instance.PlayersData.TryGetValue(player.userID, out PlayerData playerData))
            {
                if (playerData.HideMessages)
                    return;
            }

            if (string.IsNullOrEmpty(TEMPLATE_JSON_CONTAINER))
                TEMPLATE_JSON_CONTAINER = CreateUITemplate();
            CuiHelper.AddUi(player, TEMPLATE_JSON_CONTAINER);
        }
        internal Dictionary<string, List<string>> Messages = new Dictionary<string, List<string>>()
        {
            ["AntiVehicle"] = new List<string>()
            {
                "{1} убил {0}"
            },
            ["Arrow"] = new List<string>()
            {
                "{1} убил {0}"
            },
            ["Bite"] = new List<string>()
            {
                "{1} убил {0}"
            },
            ["Bleeding"] = new List<string>()
            {
                "{0} умер от кровотечения"
            },
            ["Blunt"] = new List<string>()
            {
                "{1} убил {0}"
            },
            ["Bullet"] = new List<string>()
            {
                "{1} убил {0}"
            },
            ["Cold"] = new List<string>()
            {
                "{0} замерз"
            },
            ["ColdExposure"] = new List<string>()
            {
                "{0} замерз"
            },
            ["Collision"] = new List<string>()
            {
                "{1} убил {0}"
            },
            ["Decay"] = new List<string>()
            {
                "{1} убил {0}"
            },
            ["Drowned"] = new List<string>()
            {
                "{0} утонул"
            },
            ["ElectricShock"] = new List<string>()
            {
                "{0} заискрился"
            },
            ["Explosion"] = new List<string>()
            {
                "{1} убил {0}"
            },
            ["Fall"] = new List<string>()
            {
                "{0} упал с высоты"
            },
            ["Fun_Water"] = new List<string>()
            {
                "{0} погиб"
            },
            ["Generic"] = new List<string>()
            {
                "{0} погиб"
            },
            ["Heat"] = new List<string>()
            {
                "{1} убил {0}"
            },
            ["Hunger"] = new List<string>()
            {
                "{0} умер от голода"
            },
            ["Poison"] = new List<string>()
            {
                "{0} умер от отравления"
            },
            ["Radiation"] = new List<string>()
            {
                "{0} умер от радиации"
            },
            ["RadiationExposure"] = new List<string>()
            {
                "{0} умер от радиации"
            },
            ["Slash"] = new List<string>()
            {
                "{1} убил {0}"
            },
            ["Stab"] = new List<string>()
            {
                "{1} убил {0}"
            },
            ["Suicide"] = new List<string>()
            {
                "{0} убил себя"
            },
            ["Thirst"] = new List<string>()
            {
                "{0} умер от жажды"
            },
        };
        private bool SwitchHideMyName(BasePlayer player)
        {
            if (PlayersData.TryGetValue(player.userID, out PlayerData playerData))
            {
                playerData.HideMyName = !playerData.HideMyName;
            }
            else
            {
                PlayersData[player.userID] = playerData = new PlayerData()
                {
                    HideMessages = false,
                    HideMyName = true,
                    LastConnectTime = new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds()
                };
            }

            return playerData.HideMyName;
        }

        private void AddUISettingsWidget(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();
            container.Add(new CuiElement { Name = "DMUISettings", Parent = "DMUI", Components = { new CuiRectTransformComponent { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = $"{5} {-43}", OffsetMax = $"{180} {5}" }, new CuiImageComponent { Color = "1 0.95 0.85 0.2", Material = "assets/icons/iconmaterial.mat", }, }, });
            Instance.PlayersData.TryGetValue(player.userID, out PlayerData playerData);
            container.Add(new CuiButton { Button = { Command = "deathmessages hidemyname", Color = "0 0 0 0", }, RectTransform = { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "5 3", OffsetMax = "170 23" }, Text = { Text = playerData != null && playerData.HideMyName ? "☑ Hide my name in DeathMessages" : "☐ Hide my name in DeathMessages", Align = TextAnchor.MiddleLeft, Color = "1 1 1 0.8", Font = "robotocondensed-bold.ttf", FontSize = 11 }, }, "DMUISettings", "DMUISettings.HideMyName");
            container.Add(new CuiButton { Button = { Command = "deathmessages hidemessages", Color = "0 0 0 0", }, RectTransform = { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "5 23", OffsetMax = "170 43" }, Text = { Text = playerData != null && playerData.HideMessages ? "☑ Hide DeathMessages" : "☐ Hide DeathMessages", Align = TextAnchor.MiddleLeft, Color = "1 1 1 0.8", Font = "robotocondensed-bold.ttf", FontSize = 11 }, }, "DMUISettings", "DMUISettings.HideMessages");
            CuiHelper.AddUi(player, container);
        }

        public class DynamicUI : MonoBehaviour
        {
            private TextMeshProUGUI TextMeshComponent;
            public StringCalculation Calculator;
            void Start()
            {
                this.TextMeshComponent = gameObject.AddComponent<TextMeshProUGUI>();
                this.Calculator = new StringCalculation();
            }

            public UnityEngine.Font GetFont()
            {
                return this.TextMeshComponent.font.sourceFontFile;
            }
        }

        private void RemoveUISettings(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "DMUI");
        }

        internal string GetColorFor(BasePlayer player)
        {
            foreach (var color in Instance.DeathMessagesConfig.UIConfig.Colors)
            {
                if (Instance.permission.UserHasPermission(player.UserIDString, color.Key))
                {
                    return color.Value;
                }
            }

            return Instance.DeathMessagesConfig.UIConfig.Row.Color;
        }

        internal const string PANEL_NAME = "DM";

        [Obsolete]
        private void CreateNote(string vName, string iName, float distance, string vColor, string iColor, string weaponName, string[] weaponMods, bool isHeadshot, bool needRenameVictim, bool needRenameInitiator)
        {
            PrintWarning("API Method CreateNote(string vName, string iName, float distance, string vColor, string iColor, string weaponName, string[] weaponMods, bool isHeadshot, bool needRenameVictim, bool needRenameInitiator) deprecate.");
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                RemoveUITemplate(player);
                RemoveUISettings(player);
            }

            Interface.Oxide.DataFileSystem.WriteObject("DeathMessages\\PlayersData", PlayersData);
            UnityEngine.Object.DestroyImmediate(DynamicUIObject);
            DeathRow.DeathRows.Clear();
            Instance = null;
            DynamicUIObject = null;
        }
        internal Dictionary<string, int> PrefabName2Item = new Dictionary<string, int>()
        {
            ["40mm_grenade_he"] = -1123473824,
            ["rocket_basic"] = 442886268,
            ["rocket_admin"] = 442886268,
            ["rocket_hv"] = 442886268,
            ["rocket_fire"] = 442886268,
        };

        private void UpdateUISettingsButton(BasePlayer player, string command)
        {
            CuiElementContainer container = new CuiElementContainer();
            container.Add(new CuiButton { Button = { Command = $"deathmessages {command}", Color = "0 0 0 0", }, RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", }, Text = { Text = "", }, }, "DMUI", "DMUI.Button", "DMUI.Button");
            CuiHelper.AddUi(player, container);
        }

        internal static string HexToRGBA(string hexColor)
        {
            if (ColorUtility.TryParseHtmlString(hexColor, out Color color))
                return string.Format("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a);
            return "1 1 1 1";
        }

        [ChatCommand("dm")]
        private void CMDChatMain(BasePlayer player, string cmd, string[] args)
        {
            if (Instance.DeathMessagesConfig.UIConfig.EnableUISettings == false)
                return;
            PlayersData.TryGetValue(player.userID, out PlayerData playerData);
            if (args.Length > 0)
            {
                switch (args[0])
                {
                    case "hidemyname":
                    {
                        bool result = SwitchHideMyName(player);
                        SendReply(player, $"<size=16><color=#ffa>DeathMessages</color></size>\nEnabled:\t\t {(playerData != null && playerData.HideMessages ? "On" : "Off")}\nHide name:\t\t {(result ? "On" : "Off")}\nUsage:\t\t/dm hidemessages|hidemyname");
                        break;
                    }

                    case "hidemessages":
                    {
                        bool? result = SwitchHideMessages(player);
                        if (result.HasValue)
                            SendReply(player, $"<size=16><color=#ffa>DeathMessages</color></size>\nEnabled:\t\t {(result.Value ? "On" : "Off")}\nHide name:\t\t {(playerData != null && playerData.HideMyName ? "On" : "Off")}\nUsage:\t\t/dm hidemessages|hidemyname");
                        break;
                    }
                }
            }
            else
            {
                SendReply(player, $"<size=16><color=#ffa>DeathMessages</color></size>\nEnabled:\t\t {(playerData != null && playerData.HideMessages ? "On" : "Off")}\nHide name:\t\t {(playerData != null && playerData.HideMyName ? "On" : "Off")}\nUsage:\t\t/dm hidemessages|hidemyname");
            }
        }
        internal Dictionary<ulong, PlayerData> PlayersData = new Dictionary<ulong, PlayerData>();

        private void RemoveLastUIRow()
        {
            if (DeathRow.DeathRows.Count == 0)
                return;
            if ((DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds - LAST_ROW_MODIFY_TIME < Instance.DeathMessagesConfig.UIConfig.Time)
                return;
            LAST_ROW_MODIFY_TIME = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            int lastVisibleRowId = (LAST_ROW_ID + (DeathRow.DeathRows.Count - 1) + Instance.DeathMessagesConfig.UIConfig.Rows) % Instance.DeathMessagesConfig.UIConfig.Rows;
            string jsonContainer = $"[{{\"name\":\"DMR{lastVisibleRowId}\",\"parent\":\"DM\",\"components\":[{{\"type\":\"RectTransform\",\"offsetmin\":\"0 100\",\"offsetmax\":\"0 100\"}}],\"update\":true}}]";
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (Instance.PlayersData.TryGetValue(player.userID, out PlayerData playerData))
                {
                    if (playerData.HideMessages)
                        continue;
                }

                CuiHelper.AddUi(player, jsonContainer);
            }

            DeathRow.DeathRows.RemoveAt(DeathRow.DeathRows.Count - 1);
        }

        private void UpdateUISettingsWidget(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();
            Instance.PlayersData.TryGetValue(player.userID, out PlayerData playerData);
            container.Add(new CuiButton { Button = { Command = "deathmessages hidemyname", Color = "0 0 0 0", }, RectTransform = { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "5 3", OffsetMax = "170 23" }, Text = { Text = playerData != null && playerData.HideMyName ? "☑ Hide my name in DeathMessages" : "☐ Hide my name in DeathMessages", Align = TextAnchor.MiddleLeft, Color = "1 1 1 0.8", Font = "robotocondensed-bold.ttf", FontSize = 11 }, }, "DMUISettings", "DMUISettings.HideMyName", "DMUISettings.HideMyName");
            container.Add(new CuiButton { Button = { Command = "deathmessages hidemessages", Color = "0 0 0 0", }, RectTransform = { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "5 23", OffsetMax = "170 43" }, Text = { Text = playerData != null && playerData.HideMessages ? "☑ Hide DeathMessages" : "☐ Hide DeathMessages", Align = TextAnchor.MiddleLeft, Color = "1 1 1 0.8", Font = "robotocondensed-bold.ttf", FontSize = 11 }, }, "DMUISettings", "DMUISettings.HideMessages", "DMUISettings.HideMessages");
            CuiHelper.AddUi(player, container);
        }
        internal string TEMPLATE_JSON_CONTAINER = string.Empty;

        private void OnPatrolHelicopterKill(PatrolHelicopter victim, HitInfo info)
        {
            if (victim == null)
                return;
            if (info == null)
                return;
            if (info.damageTypes == null)
                return;
            if (info.InitiatorPlayer == null)
                return;
            new DeathRow(victim, info.InitiatorPlayer, info).Run();
        }
        internal DynamicUI DynamicUIComponent;

        public class DeathRow
        {
            public string InitiatorMessage { get; private set; }
            public string VictimMessage { get; private set; }
            public string Distance { get; private set; }
            public float VictimComputedWidth { get; set; }
            public float InitiatorComputedWidth { get; set; }
            public string Message { get; private set; }
            public float MessageComputedWidth { get; set; }
            public int WeaponIcon { get; private set; }
            public List<int> WeaponModsIcons { get; private set; }

            public bool Initialized() => string.IsNullOrEmpty(this.Message) == false || string.IsNullOrEmpty(this.VictimMessage) == false;
            public static List<DeathRow> DeathRows = new List<DeathRow>();
            public DeathRow(BasePlayer victim, BasePlayer initiator, HitInfo info)
            {
                if (Instance.DeathMessagesConfig.CoreConfig.PlayerSuicide == false && victim.userID == initiator.userID)
                    return;
                FillFields(victim, initiator, info);
            }

            public DeathRow(BasePlayer victim, NPCPlayer initiator, HitInfo info)
            {
                if (Instance.DeathMessagesConfig.CoreConfig.PlayerKilledByNPC == false)
                    return;
                FillFields(victim, initiator, info);
            }

            public DeathRow(NPCPlayer victim, BasePlayer initiator, HitInfo info)
            {
                if (Instance.DeathMessagesConfig.CoreConfig.NPC == false)
                    return;
                FillFields(victim, initiator, info);
            }

            public DeathRow(BaseCombatEntity victim, BasePlayer initiator, HitInfo info)
            {
                if (Instance.DeathMessagesConfig.CoreConfig.Animal == false)
                    return;
                FillFields(victim, initiator, info);
            }

            public DeathRow(BasePlayer victim, BaseCombatEntity initiator, HitInfo info)
            {
                if (Instance.DeathMessagesConfig.CoreConfig.PlayerKilledByAnimal == false)
                    return;
                FillFields(victim, initiator, info);
            }

            public DeathRow(PatrolHelicopter victim, BasePlayer initiator, HitInfo info)
            {
                if (Instance.DeathMessagesConfig.CoreConfig.Helicopter == false)
                    return;
                FillFields(victim, initiator, info);
            }
		   		 		  						  	   		   					  	  			  		 			  		 	
            public DeathRow(BradleyAPC victim, BasePlayer initiator, HitInfo info)
            {
                if (Instance.DeathMessagesConfig.CoreConfig.Bradley == false)
                    return;
                FillFields(victim, initiator, info);
            }

            public DeathRow(BasePlayer victim, DecayEntity initiator, HitInfo info)
            {
                if (Instance.DeathMessagesConfig.CoreConfig.PlayerKilledByEnv == false)
                    return;
                FillFields(victim, initiator, info);
            }

            public DeathRow(BasePlayer victim, HitInfo info)
            {
                if (Instance.DeathMessagesConfig.CoreConfig.PlayerKilledByEnv == false)
                    return;
                FillFields(victim, null, info);
            }

            public DeathRow(string initiator, string victim, string distance, int weaponIcon, List<int> weaponMods, DamageType damageType = DamageType.Bullet)
            {
                this.InitiatorMessage = initiator;
                this.VictimMessage = victim;
                this.Distance = distance;
                this.WeaponIcon = weaponIcon;
                this.WeaponModsIcons = weaponMods;
                if (Instance.DeathMessagesConfig.UIConfig.Image.Center == false)
                {
                    string messageVariable = Instance.GetDeathMessage(damageType);
                    if (string.IsNullOrEmpty(messageVariable) == false)
                    {
                        this.Message = string.Format(messageVariable, this.VictimMessage, this.InitiatorMessage);
                    }
                }
            }

            private void FillFields(BaseCombatEntity victim, BaseCombatEntity initiator = null, HitInfo info = null)
            {
                FillDistance(victim, initiator);
                FillIcons(info, initiator);
                FillMessage(victim, initiator, info);
            }

            private void FillMessage(BaseCombatEntity victim, BaseCombatEntity initiator = null, HitInfo info = null)
            {
                if (Instance.DeathMessagesConfig.UIConfig.Image.Center == false)
                {
                    string messageVariable = Instance.GetDeathMessage(info?.damageTypes?.GetMajorityDamageType() ?? DamageType.Generic);
                    if (string.IsNullOrEmpty(messageVariable) == false)
                    {
                        string victimName = GetEntityName(victim);
                        string initiatorName = initiator != null ? GetEntityName(initiator) : info?.damageTypes?.GetMajorityDamageType().ToString();
                        this.Message = string.Format(messageVariable, victimName, initiatorName);
                    }
                }
                else
                {
                    this.VictimMessage = GetEntityName(victim);
                    this.InitiatorMessage = initiator != null ? GetEntityName(initiator) : info?.damageTypes?.GetMajorityDamageType().ToString();
                }
            }

            private void FillIcons(HitInfo info, BaseCombatEntity initiator = null)
            {
                this.WeaponModsIcons = new List<int>(5);
                if (info == null)
                {
                    this.WeaponIcon = 996293980;
                    return;
                }

                if (info.isHeadshot)
                    this.WeaponModsIcons.Add(996293980);
                if (initiator != null && initiator is DecayEntity)
                {
                    ItemDefinition initiatorDefinition = GetInitiatorDefinition(initiator);
                    if (initiatorDefinition != null)
                    {
                        this.WeaponIcon = initiatorDefinition.itemid;
                        if (Instance.DeathMessagesConfig.UIConfig.Image.Mods)
                        {
                            Item attackItem = GetAttackItem(info);
                            if (attackItem == null)
                            {
                                ItemDefinition attackDefinition = GetAttackDefinition(info);
                                if (attackDefinition != null && attackDefinition.itemid != this.WeaponIcon)
                                    this.WeaponModsIcons.Add(attackDefinition.itemid);
                            }
                            else
                            {
                                this.WeaponModsIcons.Add(attackItem.info.itemid);
                                this.WeaponModsIcons.AddRange(attackItem.contents.itemList.Select(x => x.info.itemid).ToList());
                            }
                        }
                    }
                }
                else
                {
                    Item attackItem = GetAttackItem(info);
                    if (attackItem == null)
                    {
                        ItemDefinition attackDefinition = GetAttackDefinition(info);
                        if (attackDefinition != null)
                        {
                            this.WeaponIcon = attackDefinition.itemid;
                        }
                        else
                        {
                            if (info.isHeadshot == false)
                                this.WeaponIcon = 996293980;
                        }
                    }
                    else
                    {
                        this.WeaponIcon = attackItem.info.itemid;
                        if (Instance.DeathMessagesConfig.UIConfig.Image.Mods && attackItem.contents != null)
                            this.WeaponModsIcons.AddRange(attackItem.contents.itemList.Select(x => x.info.itemid).ToList());
                    }
                }
            }
		   		 		  						  	   		   					  	  			  		 			  		 	
            private void FillDistance(BaseCombatEntity victim, BaseCombatEntity initiator = null)
            {
                if (initiator != null)
                    this.Distance = $"{Vector3.Distance(victim.transform.position, initiator.transform.position).ToString("F1")}m";
                else
                    this.Distance = "0m";
            }

            private string GetEntityName(BaseCombatEntity entity)
            {
                if (entity is BasePlayer player && player.IsNonNpcPlayer())
                {
                    return GetPlayerName(player);
                }

                if (entity is BasePlayer npcPlayer && npcPlayer.IsNpcPlayer())
                {
                    if (Instance.DeathMessagesConfig.CoreConfig.RenameNPC)
                        return GetPrefabName(npcPlayer.ShortPrefabName);
                    else
                        return GetNpcName(npcPlayer.displayName);
                }

                return GetPrefabName(entity.ShortPrefabName);
            }

            public void Run()
            {
                if (Initialized())
                {
                    if (DeathRows.Count == Instance.DeathMessagesConfig.UIConfig.Rows)
                        DeathRows.RemoveAt(DeathRows.Count - 1);
                    DeathRows.Insert(0, this);
                    if (Instance.DeathMessagesConfig.UIConfig.Image.Center)
                    {
                        this.VictimComputedWidth = Instance.DynamicUIComponent.Calculator.GetStringWidth(this.VictimMessage);
                        this.InitiatorComputedWidth = Instance.DynamicUIComponent.Calculator.GetStringWidth(this.InitiatorMessage);
                        Instance.AddUIRow(this);
                    }
                    else
                    {
                        this.MessageComputedWidth = Instance.DynamicUIComponent.Calculator.GetStringWidth(this.Message);
                        Instance.AddUIRow(this);
                    }
                }
            }

            private Item GetAttackItem(HitInfo info)
            {
                Item attackItem = GetWeaponFromEntity(info.Weapon);
                if (attackItem == null)
                    attackItem = GetWeaponFromEntity(info.WeaponPrefab);
                if (attackItem == null)
                    attackItem = GetWeaponFromEntity(info.ProjectilePrefab?.sourceWeaponPrefab);
                return attackItem;
            }
		   		 		  						  	   		   					  	  			  		 			  		 	
            private ItemDefinition GetAttackDefinition(HitInfo info)
            {
                ItemDefinition attackDefinition = GetWeaponFromEntityName(info.Weapon);
                if (attackDefinition == null)
                    attackDefinition = GetWeaponFromEntityName(info.WeaponPrefab);
                if (attackDefinition == null)
                    attackDefinition = GetWeaponFromEntityName(info.ProjectilePrefab?.sourceWeaponPrefab);
                if (attackDefinition == null)
                    attackDefinition = GetWeaponFromEntityName(info.ProjectilePrefab?.sourceProjectilePrefab?.sourceWeaponPrefab);
                return attackDefinition;
            }

            private ItemDefinition GetInitiatorDefinition(BaseCombatEntity entity)
            {
                return GetWeaponFromEntityName(entity);
            }
		   		 		  						  	   		   					  	  			  		 			  		 	
            private Item GetWeaponFromEntity(BaseEntity attackEntity)
            {
                if (attackEntity == null || attackEntity.GetItem() == null)
                    return null;
                return attackEntity.GetItem();
            }

            private ItemDefinition GetWeaponFromEntityName(BaseEntity attackEntity)
            {
                if (attackEntity == null)
                    return null;
                if (!Instance.Prefab2Item.TryGetValue(attackEntity.prefabID, out int itemID))
                {
                    if (!Instance.PrefabName2Item.TryGetValue(attackEntity.ShortPrefabName, out itemID))
                    {
                        return null;
                    }
                }
		   		 		  						  	   		   					  	  			  		 			  		 	
                if (!ItemManager.itemDictionary.TryGetValue(itemID, out ItemDefinition item))
                    return null;
                return item;
            }

            private string GetPlayerName(BasePlayer player)
            {
                string playerName = player.displayName;
                if (player.IsNpc == false && Instance.PlayersData.TryGetValue(player.userID, out PlayerData playerData) && playerData.HideMyName)
                    playerName = RandomUsernames.Get(player.userID + (ulong)((long)UnityEngine.Random.Range(0, 100000)));
                playerName = playerName.Substring(0, Math.Min(Instance.DeathMessagesConfig.UIConfig.NameSize, playerName.Length));
                return $"<size={Instance.DeathMessagesConfig.UIConfig.Row.Size}>{GetColoredPlayerName(player, playerName)}</size>";
            }

            private string GetColoredPlayerName(BasePlayer player, string playerName)
            {
                string[] gradients = Instance.GetGradientFor(player);
                if (gradients != null && gradients.Length > 2)
                {
                    string coloredPlayerName = string.Empty;
                    int colorCount = gradients.Length;
                    for (int i = 0; i < playerName.Length; i++)
                    {
                        int colorIndex = i % (2 * (colorCount - 1));
                        if (colorIndex >= colorCount)
                        {
                            colorIndex = 2 * (colorCount - 1) - colorIndex;
                        }
		   		 		  						  	   		   					  	  			  		 			  		 	
                        coloredPlayerName += $"<color={gradients[colorIndex]}>{playerName[i]}\u200B</color>";
                    }

                    return coloredPlayerName;
                }

                return $"<color={Instance.GetColorFor(player)}>{playerName}</color>";
            }

            private string GetNpcName(string name)
            {
                return $"<size={Instance.DeathMessagesConfig.UIConfig.Row.Size}><color={Instance.DeathMessagesConfig.UIConfig.Row.Color}>{name}</color></size>";
            }

            private string GetPrefabName(string name)
            {
                string npcName = Instance.GetNpcName(name);
                return $"<size={Instance.DeathMessagesConfig.UIConfig.Row.Size}><color={Instance.DeathMessagesConfig.UIConfig.Row.Color}>{npcName}</color></size>";
            }
        }

        private void OnServerSave()
        {
            Interface.Oxide.DataFileSystem.WriteObject("DeathMessages\\PlayersData", PlayersData);
        }

        private bool IsPlayerNameHidden(BasePlayer player)
        {
            return PlayersData.TryGetValue(player.userID, out PlayerData playerData) && playerData.HideMyName;
        }
        public class PlayerData
        {
            public double LastConnectTime = 0;
            public bool HideMessages = false;
            public bool HideMyName = false;
        }

        internal Dictionary<string, string> Names = new Dictionary<string, string>()
        {
            ["npcplayer"] = "NPC",
            ["guntrap.deployed"] = "Guntrap",
            ["landmine"] = "Landmine",
            ["beartrap"] = "Bear trap",
            ["flameturret.deployed"] = "Flame turret",
            ["flameturret_fireball"] = "Flame turret",
            ["autoturret_deployed"] = "Turret",
            ["sentry.scientist.static"] = "Turret NPC",
            ["sentry.bandit.static"] = "Turret NPC",
            ["spikes.floor"] = "Spikes",
            ["spikes_static"] = "Spikes",
            ["teslacoil.deployed"] = "Tesla",
            ["barricade.wood"] = "Barricade",
            ["barricade.woodwire"] = "Barricade",
            ["barricade.metal"] = "Barricade",
            ["bradleyapc"] = "BradleyAPC",
            ["gates.external.high.wood"] = "Gates",
            ["gates.external.high.stone"] = "Gates",
            ["icewall"] = "Ice Wall",
            ["wall.external.high.ice"] = "Ice wall",
            ["wall.external.high.stone"] = "Wall",
            ["wall.external.high.wood"] = "Wall",
            ["campfire"] = "Campfire",
            ["skull_fire_pit"] = "Campfire",
            ["lock.code"] = "Codelock",
            ["boar"] = "Boar",
            ["bear"] = "Bear",
            ["polarbear"] = "Polar Bear",
            ["wolf"] = "Wolf",
            ["stag"] = "Stag",
            ["chicken"] = "Chicken",
            ["horse"] = "Horse",
            ["minicopter.entity"] = "Minicopter",
            ["scraptransporthelicopter"] = "Transport helicopter",
            ["patrolhelicopter"] = "Patrol helicopter",
            ["napalm"] = "Napalm",
            ["fireball_small"] = "Fire",
            ["fireball_small_shotgun"] = "Fire",
            ["fireball_small_arrow"] = "Fire",
            ["sam_site_turret_deployed"] = "SAM",
            ["cactus-1"] = "Cactus",
            ["cactus-2"] = "Cactus",
            ["cactus-3"] = "Cactus",
            ["cactus-4"] = "Cactus",
            ["cactus-5"] = "Cactus",
            ["cactus-6"] = "Cactus",
            ["cactus-7"] = "Cactus",
            ["hotairballoon"] = "Hot Air Balloon",
            ["cave_lift_trigger"] = "Lift",
            ["wolf2"] = "Wolf",
            ["simpleshark"] = "Shark",
            ["door_barricade_b"] = "Barricade",
            ["modular_car_1mod_storage"] = "Car",
            ["modular_car_1mod_trade"] = "Car",
            ["modular_car_2mod_fuel_tank"] = "Car",
            ["modular_car_camper_storage"] = "Car",
            ["modular_car_fuel_storage"] = "Car",
            ["modular_car_i4_engine_storage"] = "Car",
            ["modular_car_sleepingbag_campervan"] = "Car",
            ["modular_car_v8_engine_storage"] = "Car",
            ["sam_static"] = "SAM",
            ["sentry.bandit.static"] = "Bandit",
            ["sentry.scientist.static"] = "Scientist",
            ["supply_drop"] = "Supply Drop",
            ["npc_bandit_guard"] = "Bandit Guard",
            ["scientistnpc_arena"] = "Scientist",
            ["scientistnpc_bradley"] = "Scientist",
            ["scientistnpc_bradley_heavy"] = "Heavy Scientist",
            ["scientistnpc_cargo"] = "Scientist",
            ["scientistnpc_cargo_turret_any"] = "Scientist",
            ["scientistnpc_cargo_turret_lr300"] = "Scientist",
            ["scientistnpc_ch47_gunner"] = "Scientist",
            ["scientistnpc_excavator"] = "Scientist",
            ["scientistnpc_full_any"] = "Scientist",
            ["scientistnpc_full_lr300"] = "Scientist",
            ["scientistnpc_full_mp5"] = "Scientist",
            ["scientistnpc_full_pistol"] = "Scientist",
            ["scientistnpc_full_shotgun"] = "Scientist",
            ["scientistnpc_heavy"] = "Heavy Scientist",
            ["scientistnpc_junkpile_pistol"] = "Scientist",
            ["scientistnpc_oilrig"] = "Scientist",
            ["scientistnpc_patrol"] = "Scientist",
            ["scientistnpc_peacekeeper"] = "Scientist",
            ["scientistnpc_roam"] = "Scientist",
            ["scientistnpc_roam_nvg_variant"] = "Scientist",
            ["scientistnpc_roamtethered"] = "Scientist",
            ["npc_tunneldweller"] = "Tunnel Dweller",
            ["npc_tunneldwellerspawned"] = "Tunnel Dweller",
            ["npc_underwaterdweller"] = "Underwater Dweller",
            ["npcplayertest"] = "Player Test",
        };
        internal int LAST_ROW_ID = 0;
        internal string GetDeathMessage(DamageType damageType)
        {
            if (DeathMessagesConfig.CoreConfig.Messages.TryGetValue(damageType.ToString(), out List<string> variables))
                return variables[UnityEngine.Random.Range(0, variables.Count)];
            return null;
        }

        internal GameObject DynamicUIObject;
    }
}
