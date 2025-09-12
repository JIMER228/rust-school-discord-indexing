using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("InfoMenu", "https://discord.gg/9vyTXsJyKR", "1.0.0")]
    public class InfoMenu : RustPlugin
    {
		
		#region Variables
		
		[PluginReference] 
		private Plugin ImageLibrary;
		
		[PluginReference] 
		private Plugin WipeBlock;				
		
		private static Dictionary<ulong, PlayerClasses> PlayersList = new Dictionary<ulong, PlayerClasses>();
		private static List<ulong> NeedInitPlayers = new List<ulong>();

        private class PlayerClasses
        {
            public bool Firts = false;
        }
		
		private enum Months
        {
            Января = 1,
            Февраля = 2,
            Марта = 3,
            Апреля = 4,
            Мая = 5,
            Июня = 6,
            Июля = 7,
            Августа = 8,
            Сентября = 9,
            Октября = 10,
            Ноября = 11,
            Декабря = 12
        }
		
		#endregion
        
		#region Hooks	            
		
		private void OnPlayerConnected(BasePlayer player)
        {
			if (!NeedInitPlayers.Contains(player.userID))
				NeedInitPlayers.Add(player.userID);
			
            if (config.OpenToConnectionFirst)
            {
                if (player.IsReceivingSnapshot)
                {
                    timer.In(1f, () => OnPlayerConnected(player));
                    return;
                }
                if (!PlayersList.ContainsKey(player.userID))
                {
                    PlayersList.Add(player.userID, new PlayerClasses() { Firts = true });
                    CreateMenu(player, 0, 0, 3, 0, true);
                    return;
                }
            }
            if (config.OpenToConnection)
            {
                if (player.IsReceivingSnapshot)
                {
                    timer.In(1f, () => OnPlayerConnected(player));
                    return;
                }
                CreateMenu(player, 0, 0, 3, 0, true);
                return;
            }
        }
		
        private void OnServerInitialized()
        {
            PrintWarning("\n-----------------------------\n" +
            "     Author - https://topplugin.ru/\n" +
            "     VK - https://vk.com/rustnastroika\n" +
            "     Discord - https://discord.com/invite/5DPTsRmd3G\n" +
            "-----------------------------");
            LoadData();
            if (!ImageLibrary)
            {
                PrintError("Imagelibrary not found!");
                return;
            }

            IEnumerable<Images> images = from message in config.tabs from attachment in message.pages from image in attachment.Images select image;

            foreach (var image in images.ToList())            
                ImageLibrary?.Call("AddImage", image.URL, image.URL);            

            IEnumerable<Pages> MenuIcons = from message in config.tabs from attachment in message.pages select attachment;

            foreach (var icon in MenuIcons)            
                ImageLibrary?.Call("AddImage", icon.URL, icon.URL);

            if (!string.IsNullOrEmpty(config.MenuBackgroundImage))            
                ImageLibrary?.Call("AddImage", config.MenuBackgroundImage, config.MenuBackgroundImage);            

            config.Commands.ForEach(c => cmd.AddChatCommand(c, this, cmdOpenInfoMenu));

            BasePlayer.activePlayerList.ToList().ForEach(OnPlayerConnected);
        }                      

        private void Unload()
        {
            BasePlayer.activePlayerList.ToList().ForEach(player => 
				{ 
					CuiHelper.DestroyUi(player, BaseLayer);					
					CuiHelper.DestroyUi(player, MainLayer);
					CuiHelper.DestroyUi(player, InitPanel);
					CuiHelper.DestroyUi(player, $"{MainLayer}SideBarImages"); 
				});
            SaveData();
        }
		
		#endregion
		
		#region GUI
		
		private static string BaseLayer = "InfoMenu_base";
		private static string MainLayer = "InfoMenu_main";
		private static string InitPanel = "FarmerInitPanel";
		
		[ChatCommand("block")]
        private void cmdBlockDraw(BasePlayer player, string command, string[] args) => CreateMenu(player, 3, 0, 0, 0, true);        

        private void cmdOpenInfoMenu(BasePlayer player, string command, string[] args) => CreateMenu(player, 0, 0, 3, 0, true);

        [ConsoleCommand("infomenu_selectpage")]
        private void cmdSelectPage(ConsoleSystem.Arg args)
        {
            var player = args.Player();
            var type = args.GetInt(0);
            var page = args.GetInt(1);
			var oldType = args.GetInt(2);
            var oldPage = args.GetInt(3);
			
			if (type == oldType && page == oldPage) return;
			
            CreateMenu(player, type, page, oldType, oldPage);
        } 
		
		[ConsoleCommand("infomenu.close")]
        private void IMClose(ConsoleSystem.Arg arg)
        {
            CuiHelper.DestroyUi(arg.Player(), BaseLayer);
            CuiHelper.DestroyUi(arg.Player(), $"{MainLayer}SideBarImages");
        }

        private void CreateMenu(BasePlayer player, int type, int page, int oldType, int oldPage, bool isStart = false)
        {            
            CuiElementContainer container = new CuiElementContainer();
			
			if (isStart)
			{
				/*if (NeedInitPlayers.Contains(player.userID))
				{
					DoInitImages(player);
					NeedInitPlayers.Remove(player.userID);
					timer.Once(0.1f, ()=> CreateMenu(player, type, page, oldType, oldPage, isStart));
					return;
				}*/
				
				CuiHelper.DestroyUi(player, InitPanel);
				
				// меню
				container.Add(new CuiPanel
				{
					CursorEnabled = true,
					Image =
					{
						 Color = ParseColorFromRGBA(config.MenuColor),
						 Material=  "Assets/Content/UI/UI.Background.Tile.psd",
						 FadeIn = 0.5f
					},
					RectTransform =
					{
						AnchorMin = $"{1 - config.MenuAnchorWidth} {1- config.MenuAnchorHeight}",
						AnchorMax = $"{config.MenuAnchorWidth} {config.MenuAnchorHeight}"
					}
				}, "Overlay", BaseLayer);

				// изображение фона меню
				if (!string.IsNullOrEmpty(config.MenuBackgroundImage))
				{
					container.Add(new CuiElement()
					{
						Parent = BaseLayer,
						Components = {
								new CuiRawImageComponent {
									Png = (string)ImageLibrary?.Call("GetImage", config.MenuBackgroundImage), FadeIn = 0.5f, Color = "1 1 1 1"									
								}, 
								new CuiRectTransformComponent {
									AnchorMin=$"0 0", AnchorMax= $"1 1"
								}
						}
					});
				}

				// кнопка выход фоновая
				container.Add(new CuiButton
				{
					Button =
					{
						Command = "infomenu.close",
						Color = "0 0 0 0"
					},
					Text =
					{
						Text = ""
					},
					RectTransform =
					{
						AnchorMin = "-100 -100",
						AnchorMax = "100 100"
					}
				}, BaseLayer);
				
				// левая панель меню под кнопки
				container.Add(new CuiPanel
				{
					Image =
					{
						Color = ParseColorFromRGBA(config.SidebarColor),
					    Sprite=  "Assets/Content/UI/UI.Background.Tile.psd",
						FadeIn = 0.5f
					},
					RectTransform =
					{
						AnchorMin = $"0 0",
						AnchorMax = $"{0 + config.SidebarWidth} 0.998"
					}
				}, BaseLayer, $"{BaseLayer}SideBar");
				
				// правая панель под содержимое (дубль)
				container.Add(new CuiPanel
				{
					Image =
					{
						Color = "0 0 0 0",
					},
					RectTransform =
					{
						AnchorMin = $"{0 + config.SidebarWidth} 0",
						AnchorMax = $"1 0.998"
					}
				}, BaseLayer, $"{BaseLayer}Info");								          
			}

			if (type != oldType)
			{
				CuiHelper.DestroyUi(player, $"{BaseLayer}SideBarImages");
				
				// нижний бар у иконок                
				container.Add(new CuiPanel
				{
					Image =
				{
					Color = "0 0 0 0",
				   Material=  "Assets/Content/UI/UI.Background.Tile.psd",
				},
						RectTransform =
				{
					AnchorMin = $"0 0",
					AnchorMax = $"1 0.13"
				}
				}, $"{BaseLayer}Info", $"{BaseLayer}SideBarImages");    
			}
			
			CuiHelper.DestroyUi(player, MainLayer);
			CuiHelper.DestroyUi(player, $"{MainLayer}SideBarImages");
			
			// меню дубль 2
            container.Add(new CuiPanel
            {                
                Image =
                {
                     Color = "0 0 0 0",
                     Material=  "",

                },
                RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 1"
                }
            }, BaseLayer, MainLayer);
			
			// левая панель меню под кнопки
			container.Add(new CuiPanel
			{
				Image =
				{
					Color = "0 0 0 0"					
				},
				RectTransform =
				{
					AnchorMin = $"0 0",
					AnchorMax = $"{0 + config.SidebarWidth} 0.998"
				}
			}, MainLayer, $"{MainLayer}SideBar");

            double amin = 1 - config.TabTopIndent;
            double amax = 1 - config.TabTopIndent - config.TabHeight;
            int pages = 0;
			
			// кнопки левого меню			
            foreach (var button in config.tabs)
            {								
				if ((pages == type || pages == oldType || isStart) && (type != oldType))
				{
					if (!isStart)
						CuiHelper.DestroyUi(player, $"{BaseLayer}Panel{button.Title}");
					
					// цвет кнопок				
					container.Add(new CuiPanel
					{
						FadeOut = 0.1f,
						Image =
						{
							Color = config.tabs[type].Title == button.Title ? ParseColorFromRGBA(config.TabActiveColor) : ParseColorFromRGBA(config.TabColor),
							Material = "Assets/Content/UI/UI.Background.Tile.psd",
							FadeIn = isStart ? 0.5f : 0.1f
						},
						RectTransform =
						{
							AnchorMin = $"{1- config.TabWidth} {amax}",
							AnchorMax = $"{config.TabWidth} {amin}"
						}
					}, $"{BaseLayer}SideBar", $"{BaseLayer}Panel{button.Title}");
					
					// текст кнопок
					container.Add(new CuiElement
					{
						Name = CuiHelper.GetGuid(),
						Parent = $"{BaseLayer}Panel{button.Title}",
						Components =
						{
							new CuiTextComponent { Text = $"{button.Title}", FontSize = config.TabFontSize, Align = TextAnchor.MiddleCenter, Color = ParseColorFromRGBA(config.TabTextColor),Font="robotocondensed-bold.ttf", FadeIn = isStart ? 0.5f : 0f },
							new CuiRectTransformComponent{ AnchorMin = "0 0", AnchorMax = "0.98 1" }
						}
					});
				}
				
				// кнопки меню слева (прозрачные)
                container.Add(new CuiButton
                {
                    Button =
					{
						Color = "0 0 0 0",
						Command = $"infomenu_selectpage {pages} 0 {type} {page}"						
					},
						Text =
					{
						Text = ""
					},
						RectTransform =
					{
						AnchorMin = $"{1- config.TabWidth} {amax}",
						AnchorMax = $"{config.TabWidth} {amin}"
					}
                }, $"{MainLayer}SideBar", $"{MainLayer}Button{button.Title}");				
				
                amin = amax - config.TabTopIndent;
                amax = amax - config.TabTopIndent - config.TabHeight;
                pages++;
            }			
			
			// правая панель под содержимое
            container.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0 0 0 0",
                },
                RectTransform =
                {
                    AnchorMin = $"{0 + config.SidebarWidth} 0",
                    AnchorMax = $"1 0.998"
                }
            }, MainLayer, $"{MainLayer}Info");
			
			// нижний бар у иконок (трешовая реализация)               
			container.Add(new CuiPanel
			{
				Image =
			{
				Color = "0 0 0 0",
			    Material=  "Assets/Content/UI/UI.Background.Tile.psd"				
			},
					RectTransform =
			{
				AnchorMin = $"{1 - config.MenuAnchorWidth + config.SidebarWidth - 0.04} {1- config.MenuAnchorHeight}", // 0.04 - возможно нужно будет менять если изменится общий размер меню
				AnchorMax = $"{config.MenuAnchorWidth} {1- config.MenuAnchorHeight + 0.104}" // 0.104 - возможно нужно будет менять если изменится общий размер меню
			}
			}, "Overlay", $"{MainLayer}SideBarImages");    

            var PanelInfo = config.tabs[type];

            var PageSelect = PanelInfo.pages[page];

			// расставляем изображения на выбранной странице
            if (PageSelect.Images.Count > 0)
            {
                foreach (var image in PageSelect.Images)
                {
                    container.Add(new CuiElement()
                    {
                        Parent = $"{MainLayer}Info",
                        Components = {
                            new CuiRawImageComponent {
                                Png = (string)ImageLibrary?.Call("GetImage", image.URL), Color = "1 1 1 0.9",
								FadeIn = isStart ? 0.5f : 0.2f
                            }
                            , new CuiRectTransformComponent {
                                AnchorMin=$"{image.PositionHorizontal} {image.PositionVertical - image.Height}", AnchorMax= $"{image.PositionHorizontal + image.Width} {image.PositionVertical}"
                            },
                        }
                    }
                    );
                }
            }
						
			var prevWipe = Interface.CallHook("GetLastWipeInfo") as string;
			var nextWipe = Interface.CallHook("GetNextWipeInfo") as string;			
            var dateTime = $"{SaveRestore.SaveCreatedTime.ToLocalTime().Day} {(Months)SaveRestore.SaveCreatedTime.ToLocalTime().Month}";
			
			// расставляем текст на выбранной странице
            foreach (var block in PageSelect.blocks)
            {
                foreach (var select in block.colums)
                {
                    var blockMin = block.Height < 0.5 ? 1 - block.Height : 0;
                    var blockMax = block.Height > 0.5 ? block.Height : 1 - 0.03;
					
					var text = $"{string.Join("\n", select.TextList).Replace("{datewipe}",  dateTime).Replace("{name}", player.displayName).Replace("{online}",  BasePlayer.activePlayerList.Count.ToString()).Replace("{que}", $"{ServerMgr.Instance.connectionQueue.Joining + ServerMgr.Instance.connectionQueue.Queued}")}";
					
					if (!string.IsNullOrEmpty(prevWipe))
						text = text.Replace("{GetLastWipeInfo}", prevWipe);
					else
						text = text.Replace("{GetLastWipeInfo}", "...");
					
					if (!string.IsNullOrEmpty(nextWipe))
						text = text.Replace("{GetNextWipeInfo}", nextWipe);
					else
						text = text.Replace("{GetNextWipeInfo}", "...");
					
                    container.Add(new CuiElement
                    {
                        Name = CuiHelper.GetGuid(),
                        Parent = $"{MainLayer}Info", 
                        Components =
                            {
                                new CuiTextComponent { Text = text, FontSize = select.TextSize, Align = select.Anchor, Color = "1 1 1 1",Font="robotocondensed-bold.ttf" , FadeIn = isStart ? 0.5f : 0.2f},
                                new CuiRectTransformComponent{ AnchorMin = $"0.01 {blockMin}", AnchorMax = $"{select.ColumnWidth-0.01} {blockMax}" },
                                new CuiOutlineComponent {Color = ParseColorFromRGBA(select.OutlineColor), Distance = "0.5 -0.5" }
                            }
                    });
                }
            }
			
			// иконки у страницы с плагинами
            if (!string.IsNullOrEmpty(PageSelect.URL))
            {
                var position = GetPositions(PanelInfo.pages.Count, 1, 0.05f, 0.001f);
                int pos = 0;				

				var bigWidth = 1.4f;
				var bigX1 = bigWidth >= 1f ? -1f*(bigWidth - 1f)/2f : (1f - bigWidth)/2f;
				var bigX2 = bigWidth >= 1f ?  1f + (bigWidth - 1f)/2f : 1f - (1f - bigWidth)/2f;
				var smallWidth = bigWidth / 1.632f;
				var smallX1 = smallWidth >= 1f ? -1f*(smallWidth - 1f)/2f : (1f - smallWidth)/2f;
				var smallX2 = smallWidth >= 1f ?  1f + (smallWidth - 1f)/2f : 1f - (1f - smallWidth)/2f;				
				
				//Puts($"{bigX1} {bigX2}");
				//Puts($"{smallX1} {smallX2}");
				
				// отрисовка всех иконок на странице
                foreach (var page1 in PanelInfo.pages)
                {
					var color = page == pos ? "1 1 1 0.9" : "1 1 1 0.3";
					
					var anchorMin = page == pos ? $"{bigX1} 0.19" : $"{smallX1} 0.31"; // менять размеры иконок (у большой ширина 1.32 высота 0.62, у малой ширина 0.9 и высота 0.38)
                    var anchorMax = page == pos ? $"{bigX2} 0.81" : $"{smallX2} 0.69"; // менять размеры иконок										

					if (pos == page || pos == oldPage || type != oldType)
					{
						if (pos == page || pos == oldPage)
							CuiHelper.DestroyUi(player, $"{MainLayer}SideBarImages{page1.URL}");
						
						container.Add(new CuiElement
						{
							Name = $"{MainLayer}SideBarImages{page1.URL}",
							Parent = $"{BaseLayer}SideBarImages",
							Components = {
							new CuiImageComponent {
								Color = "0 0 0 0",
							},
							  new CuiRectTransformComponent {
								AnchorMin = position[pos].AnchorMin,
								AnchorMax = position[pos].AnchorMax
							},
						}});

						container.Add(new CuiElement()
						{
							Parent = $"{MainLayer}SideBarImages{page1.URL}",
							Components = {
								new CuiRawImageComponent {
									Png = (string)ImageLibrary?.Call("GetImage", page1.URL), FadeIn = 0.2f, Color = color
								}
								, new CuiRectTransformComponent {
									AnchorMin=$"{anchorMin}", AnchorMax= anchorMax
								}								
							}
						});					
					}
					
                    container.Add(new CuiButton
                    {
                        Button =
                                {
                                    Command = $"infomenu_selectpage {type} {pos} {type} {page}",
                                    Color = "0 0 0 0" , Material=  "Assets/Content/UI/UI.Background.Tile.psd",
                                },
                        Text =
                                {
                                    Text = $"", FontSize = 25, Align = TextAnchor.MiddleCenter
                                },
                        RectTransform =
                                {
                                    AnchorMin = $"{Convert.ToSingle(position[pos].AnchorMin.Split(' ')[0])-0.01f} {Convert.ToSingle(position[pos].AnchorMin.Split(' ')[1])}",
									AnchorMax = $"{Convert.ToSingle(position[pos].AnchorMax.Split(' ')[0])+0.01f} {Convert.ToSingle(position[pos].AnchorMax.Split(' ')[1])}"
                                }
                    }, $"{MainLayer}SideBarImages");
					
                    pos++;
                }
            }
			// страницы на странице ?
            else
				if (PanelInfo.pages.Count > 1)
				{
					if (PanelInfo.pages.Count > page + 1)
					{
						container.Add(new CuiButton
						{
							Button =
						{
							Command = $"infomenu_selectpage {type} {page + 1} {type} {page}",
							Color = "0 0 0 0" , Material=  "Assets/Content/UI/UI.Background.Tile.psd",
						},
								Text =
						{
							Text = $"{page}▶", FontSize = 25, Align = TextAnchor.MiddleCenter
						},
								RectTransform =
						{
							AnchorMin = $"0.9 0",
							AnchorMax = $"0.99 0.1"
						}
						}, $"{MainLayer}Info", $"buttonNext");
					}
					else
					{
						container.Add(new CuiButton
						{
							Button =
						{
							Command = $"infomenu_selectpage {type} {page - 1} {type} {page}",
							Color = "0 0 0 0" , Material=  "Assets/Content/UI/UI.Background.Tile.psd",
						},
								Text =
						{
							Text = $"◀{page}", FontSize = 25, Align = TextAnchor.MiddleCenter
						},
								RectTransform =
						{
							AnchorMin = $"0.9 0",
							AnchorMax = $"0.99 0.1"
						}
						}, $"{MainLayer}Info", $"buttonPrev");
					}
				}

			CuiHelper.DestroyUi(player, $"{BaseLayer}Online");	
				
			// онлайн
            container.Add(new CuiElement()
            {
				Name = $"{BaseLayer}Online",
				FadeOut = 0.1f,
                Parent = $"{BaseLayer}SideBar",
                Components = {
					new CuiTextComponent {
						Color = "1 1 1 1", Text = $"Онлайн: {BasePlayer.activePlayerList.Count + "/" + ConVar.Server.maxplayers}\nВремя: {covalence.Server.Time.ToShortTimeString()}", Align = TextAnchor.MiddleCenter, FontSize = 20,Font="robotocondensed-bold.ttf",
						FadeIn = isStart ? 0.5f : 0.1f
					}
					, new CuiRectTransformComponent {
						AnchorMin=$"0.05 0", AnchorMax= "1 0.1"
					}					
				}
            });

            CuiHelper.AddUi(player, container);

			// отображение блока оружия на странице
            if (PanelInfo.Title.ToLower().Contains("блок"))            
                WipeBlock?.Call("DrawBlockGUI", player);
			
			if (NeedInitPlayers.Contains(player.userID))
			{
				DoInitImages(player);
				NeedInitPlayers.Remove(player.userID);
			}
        }
		
		private void InitImage(ref CuiElementContainer container, string png, string ipanel)
		{
			container.Add(new CuiElement
			{
				Name = CuiHelper.GetGuid(),
				Parent = ipanel,				
				Components =
				{
					new CuiRawImageComponent { Png = png },
					new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
				}
			});
		}
		
		private void DoInitImages(BasePlayer player)
		{
			if (player == null) return;						
			
			var container = new CuiElementContainer();			
			
			container.Add(new CuiPanel
			{				
				RectTransform = { AnchorMin = "1.1 1.1", AnchorMax = "1.2 1.2" },
				CursorEnabled = false
			}, "Overlay", InitPanel);
			
			IEnumerable<Images> images = from message in config.tabs from attachment in message.pages from image in attachment.Images select image;

            foreach (var image in images.ToList())
				InitImage(ref container, (string)ImageLibrary?.Call("GetImage", image.URL), InitPanel);
			
			CuiHelper.DestroyUi(player, InitPanel);
			CuiHelper.AddUi(player, container);	
		}
		
		#endregion

        #region Helper
		
        private class Position
        {
            public float Xmin;
            public float Xmax;
            public float Ymin;
            public float Ymax;

            public string AnchorMin =>
                $"{Math.Round(Xmin, 4).ToString(CultureInfo.InvariantCulture)} {Math.Round(Ymin, 4).ToString(CultureInfo.InvariantCulture)}";
            public string AnchorMax =>
                $"{Math.Round(Xmax, 4).ToString(CultureInfo.InvariantCulture)} {Math.Round(Ymax, 4).ToString(CultureInfo.InvariantCulture)}";

            public override string ToString()
            {
                return $"----------\nAmin:{AnchorMin}\nAmax:{AnchorMax}\n----------";
            }
        }

        [SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator")]
        private static List<Position> GetPositions(int colums, int rows, float colPadding = 0, float rowPadding = 0, bool columsFirst = false)
        {
            if (colums == 0)
                throw new ArgumentException("Can't create positions for gui!", nameof(colums));
            if (rows == 0)
                throw new ArgumentException("Can't create positions for gui!", nameof(rows));

            List<Position> result = new List<Position>();
            result.Clear();
            var colsDiv = 1f / colums;
            var rowsDiv = 1f / rows;
            if (colPadding == 0) colPadding = colsDiv / 2;
            if (rowPadding == 0) rowPadding = rowsDiv / 2;
            if (!columsFirst)
                for (int j = rows; j >= 1; j--)
                {
                    for (int i = 1; i <= colums; i++)
                    {
                        Position pos = new Position
                        {
                            Xmin = (i - 1) * colsDiv + colPadding / 2f,
                            Xmax = i * colsDiv - colPadding / 2f,
                            Ymin = (j - 1) * rowsDiv + rowPadding / 2f,
                            Ymax = j * rowsDiv - rowPadding / 2f
                        };
                        result.Add(pos);
                    }
                }
            else
                for (int i = 1; i <= colums; i++)
                {
                    for (int j = rows; j >= 1; j--)
                    {
                        Position pos = new Position
                        {
                            Xmin = (i - 1) * colsDiv + colPadding / 2f,
                            Xmax = i * colsDiv - colPadding / 2f,
                            Ymin = (j - 1) * rowsDiv + rowPadding / 2f,
                            Ymax = j * rowsDiv - rowPadding / 2f
                        };
                        result.Add(pos);
                    }
                }
            return result;
        }

        public static string ParseColorFromRGBA(string cssColor)
        {
            cssColor = cssColor.Trim();
            string[] parts = cssColor.Split(' ');
            int r = int.Parse(parts[0], CultureInfo.InvariantCulture);
            int g = int.Parse(parts[1], CultureInfo.InvariantCulture);
            int b = int.Parse(parts[2], CultureInfo.InvariantCulture);
            float a = float.Parse(parts[3], CultureInfo.InvariantCulture);
            var finish = System.Drawing.Color.FromArgb((int)(a * 255), r, g, b);
            cssColor = "#" + finish.R.ToString("X2") + finish.G.ToString("X2") + finish.B.ToString("X2") + finish.A.ToString("X2");
            var str = cssColor.Trim('#');
            if (str.Length == 6)
                str += "FF";
            if (str.Length != 8)
            {
                throw new Exception(cssColor);
                throw new InvalidOperationException("Cannot convert a wrong format.");
            }
            var r1 = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g1 = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b1 = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);
            var a1 = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);
            Color color = new Color32(r1, g1, b1, a1);
            return string.Format("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a);
        }

        #endregion
		
		#region Config
		
        private PluginConfig config;
		
		private class PluginConfig
        {
            [JsonProperty("Версия конфигурации")]
            public VersionNumber PluginVersion = new VersionNumber();
            [JsonProperty("Команды для открытия меню")]
            public List<string> Commands = new List<string>();
            [JsonProperty("Показывать при подключении")]
            public bool OpenToConnection = false;
            [JsonProperty("Показывать только при первом подключении")]
            public bool OpenToConnectionFirst = true;
            [JsonProperty("Меню: высота (0.0 - 1.0)")]
            public double MenuAnchorHeight = 1;
            [JsonProperty("Меню: ширина (0.0 - 1.0)")]
            public double MenuAnchorWidth = 1;
            [JsonProperty("Меню: цвет фона (RGBA)")]
            public string MenuColor;
            [JsonProperty("Меню: фоновое изображение (ссылка)")]
            public string MenuBackgroundImage;
            [JsonProperty("Боковое меню: ширина (0.0 - 1.0)")]
            public double SidebarWidth;
            [JsonProperty("Боковое меню: цвет фона (RGBA)")]
            public string SidebarColor;
            [JsonProperty("Вкладки: ширина (0.0 - 1.0)")]
            public double TabWidth;
            [JsonProperty("Вкладки: высота (0.0 - 1.0)")]
            public double TabHeight;
            [JsonProperty("Вкладки: промежуточный отступ (0.0 - 1.0)")]
            public double TabIndent;
            [JsonProperty("Вкладки: верхний отступ (0.0 - 1.0)")]
            public double TabTopIndent;
            [JsonProperty("Вкладки: цвет фона (RGBA)")]
            public string TabColor;
            [JsonProperty("Вкладки: активный цвет фона (RGBA)")]
            public string TabActiveColor;
            [JsonProperty("Вкладки: размер шрифта")]
            public int TabFontSize;
            [JsonProperty("Вкладки: цвет текста (RGBA)")]
            public string TabTextColor;
            [JsonProperty("Вкладки: цвет тени текста (RGBA)")]
            public string TabTextOutlineColor;
            [JsonProperty("Страницы: верхний и нижний отступ (0.0 - 1.0)")]
            public double PageUpperAndLowerIndent;
            [JsonProperty("Страницы: левый и правый отступ (0.0 - 1.0)")]
            public double PageIndentLeftRight;
            [JsonProperty("Вкладки")]
            public List<Tabs> tabs = new List<Tabs>();
            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    PluginVersion = new VersionNumber(),
                };
            }
        }

        public class Tabs
        {
            [JsonProperty("Заголовок вкладки")]
            public string Title;
            [JsonProperty("Страницы")]
            public List<Pages> pages = new List<Pages>();
        }

        public class Pages
        {
            [JsonProperty("Изображения")]
            public List<Images> Images = new List<Images>();
            [JsonProperty("Блоки текста")]
            public List<TextBlocks> blocks = new List<TextBlocks>();
            [JsonProperty("Ссылка на иконку если это уникальные плагины")]
            public string URL = "";
        }

        public class Images
        {
            [JsonProperty("Ссылка")]
            public string URL;
            [JsonProperty("Позиция по вертикали (0.0 - 1.0)")]
            public double PositionVertical;
            [JsonProperty("Позиция по горизонтали (0.0 - 1.0)")]
            public double PositionHorizontal;
            [JsonProperty("Высота (0.0 - 1.0)")]
            public double Height;
            [JsonProperty("Ширина (0.0 - 1.0)")]
            public double Width;
        }

        public class TextBlocks
        {
            [JsonProperty("Высота блока (0.0 - 1.0)")]
            public double Height;
            [JsonProperty("Колонки текста")]
            public List<TextColumns> colums = new List<TextColumns>();
        }

        public class TextColumns
        {
            [JsonProperty("Ширина колонки (0.0 - 1.0)")]
            public double ColumnWidth;
            [JsonProperty("Выравнивание (Left/Center/Right))")]
            public TextAnchor Anchor;

            [JsonProperty("Размер шрифта")]
            public int TextSize;
            [JsonProperty("Цвет тени текста (RGBA)")]
            public string OutlineColor;
            [JsonProperty("Строки текста")]
            public List<string> TextList = new List<string>();
        }    
		
        protected override void LoadDefaultConfig()
        {            
            config = PluginConfig.DefaultConfig();
        }
		
        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();

            if (config.PluginVersion < Version)
                UpdateConfigValues();

            Config.WriteObject(config, true);
        }

        private void UpdateConfigValues()
        {
            PluginConfig baseConfig = PluginConfig.DefaultConfig();
            if (config.PluginVersion < new VersionNumber(1, 1, 0))
            {
                PrintWarning("Config update detected! Updating config values...");
                foreach (var page in config.tabs)
                {
                    foreach (var paa in page.pages)
                    {
                        if (paa.URL == null)
                            paa.URL = "";
                    }
                }
                PrintWarning("Config update completed!");
            }
            config.PluginVersion = Version;
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }
		
		#endregion
		
		#region Data
		
		private void LoadData()
        {
            try
            {
                PlayersList = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerClasses>>(Name);
            }
            catch
            {
                PlayersList = new Dictionary<ulong, PlayerClasses>();
            }
        }

        private void SaveData()
        {
            if (PlayersList != null)
                Interface.Oxide.DataFileSystem.WriteObject(Name, PlayersList);
        }
		
		#endregion
    }
}
