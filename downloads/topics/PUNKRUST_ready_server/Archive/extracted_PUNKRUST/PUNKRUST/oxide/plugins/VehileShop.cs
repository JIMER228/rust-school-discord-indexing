using System.Collections.Generic;
using System.Linq;
using Facepunch.Extend;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Rust.Modular;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("VehileShop","Netrunner","1.0")]
    public class VehileShop : RustPlugin
    {
        #region Fields

        [PluginReference] private Plugin ImageLibrary,EconomicsEvo,Broadcast,WMenu;

        #endregion

        #region config

        class CurrencySettinfgs
        {
            [JsonProperty("Ключ валюты")]
            public string CurrencyKey = "eddie";
            [JsonProperty("Отображаемое имя")]
            public string DisplayName = "ЕВРОДОЛЛАРОВ";
        }

        class VehileSetting
        {
            [JsonProperty("Префаб")]
            public string Prefab="assets/content/vehicles/modularcar/2module_car_spawned.entity.prefab";
            [JsonProperty("Имя")]
            public string Name = "Двухмодульная машина - Такси";
            [JsonProperty("Картинка")]
            public string Image = "https://gspics.org/images/2022/06/20/01Ls88.png";
            [JsonProperty("Цена")]
            public int Price = 10;
            [JsonProperty("Водный")]
            public bool Water = false;
            [JsonProperty("Начальное топливо")]
            public int Fuel = 10;
            [JsonProperty("Растояние")]
            public double Distance = 10;
            [JsonProperty("Модули")]
            public List<string> Modules = new List<string>();
            [JsonProperty("Части двигателя")]
            public List<string> EngineParts = new List<string>();
        }
        
        static Configuration config = new Configuration();

        class Configuration
        {
            [JsonProperty("Настройка Привилегий")] public Dictionary<string,double> Permissions = new Dictionary<string, double>();
            
            [JsonProperty("Настройка валюты")] public CurrencySettinfgs CurrencySettinfgs;
            
            [JsonProperty("Транспорт")] public List<VehileSetting> VehileSettings = new List<VehileSetting>();
            

            
            public static Configuration GetNewConfiguration()
            {
                return new Configuration
                {
                    Permissions = new Dictionary<string, double>
                    {
                        ["vehileshop.vip"] = 0.9,
                        ["vehileshop.admin"] = 0.1
                    },
                    CurrencySettinfgs = new CurrencySettinfgs
                    {
                        CurrencyKey = "caps",
                        DisplayName = "КРЫШЕК"
                    },
                    VehileSettings = new List<VehileSetting>
                    {
                       new VehileSetting(),new VehileSetting()
                       
                    }
                };
            }
        }
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) PrintWarning("NULL");
                if (config == null) LoadDefaultConfig();
            }
            catch
            {
                PrintError($"Не удалось найти конфигурацию 'oxide/config/{Name}', Создание конфига!!");
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }
        protected override void LoadDefaultConfig() => config = Configuration.GetNewConfiguration();
        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion

        #region hooks

        void OnServerInitialized()
        {
            ImageLibrary.Call("AddImage", "https://i.ibb.co/zVBbxnm/vbg.png", "https://i.ibb.co/zVBbxnm/vbg.png");
            foreach (var vehile in config.VehileSettings) ImageLibrary.Call("AddImage", vehile.Image, vehile.Image);
            foreach (var perm in config.Permissions) permission.RegisterPermission(perm.Key,this);
        }

        #endregion

        #region methods

        double PriceModifier(BasePlayer player)
        {
            double modifier = 1;
            foreach (var perm in config.Permissions)
                if (permission.UserHasPermission(player.UserIDString,perm.Key))
                    if (perm.Value<modifier) modifier = perm.Value;

            return modifier;
        }

        void SpawnVehile(BasePlayer player, int number)
        {
            if (player.IsBuildingBlocked())
            {
               Broadcast.Call("GetPlayerNotice", player, "Автомастерская","Неподходящие место. Выберете другое место для спавна","infoicn","assets/bundled/prefabs/fx/invite_notice.prefab");
                SendReply(player,"Неподходяшие место");
                return;
            }
            VehileSetting element = config.VehileSettings[number];
            
            if (element.Water && player.modelState != null && player.modelState.waterLevel <= 0f)
            {
                Broadcast.Call("GetPlayerNotice", player, "Автомастерская","Неподходящие место. Выберете другое место для спавна","infoicn","assets/bundled/prefabs/fx/invite_notice.prefab");
                SendReply(player,"Неподходяшие место");
                return;
            }
            Vector3 vector = player.transform.position + (player.eyes.MovementForward() * (float) element.Distance) + Vector3.up * 2f;
            RaycastHit hit;
            if (!Physics.Raycast(vector, Vector3.down, out hit, 5f, LayerMask.GetMask("Terrain", "World", "Construction"), QueryTriggerInteraction.Ignore))
            {
                Broadcast.Call("GetPlayerNotice", player, "Автомастерская","Неподходящие место. Выберете другое место для спавна","infoicn","assets/bundled/prefabs/fx/invite_notice.prefab");
                SendReply(player,"Неподходяшие место");
                return;
            }
            Vector3 spawnpos = hit.point;
            float water = TerrainMeta.WaterMap.GetHeight(vector);
            if (element.Water) spawnpos.y = TerrainMeta.WaterMap.GetHeight(vector) + 2f;
            else if(water > hit.point.y)
            {
                Broadcast.Call("GetPlayerNotice", player, "Автомастерская","Неподходящие место. Выберете другое место для спавна","infoicn","assets/bundled/prefabs/fx/invite_notice.prefab");
                SendReply(player,"Неподходяшие место.");
                return;
            }

            BaseEntity entity = GameManager.server.CreateEntity(element.Prefab, spawnpos);
            if (entity == null)
            {
                Debug.LogError($"[VVehicle] Префаб для {element.Prefab} не существует!");
                return;
            }

            
            entity.OwnerID = player.userID;
            if (entity is ModularCar) (entity as ModularCar).spawnSettings.useSpawnSettings = false;
            entity.Spawn();
            
            if (entity is ModularCar)
            {
                ModularCar modularCar = entity as ModularCar;
                foreach (var x in element.Modules)
                {
                    Item moduleItem = ItemManager.CreateByName(x);
                    if (moduleItem == null) continue;
                    if (!modularCar.TryAddModule(moduleItem)) moduleItem.Remove();
                }

                NextTick(() =>
                {
                    EngineStorage engineStorage = null;
                    for (int index = 0; index < modularCar.AttachedModuleEntities.Count; ++index)
                    {
                        engineStorage = GetEngineStorage(modularCar.AttachedModuleEntities[index]);
                        if (engineStorage != null) break;
                    }

                    // Debug.Log("baseVehicleModule??");
                    if (engineStorage != null)
                    {
                        engineStorage.AdminAddParts(3);
                        /*
                        foreach (var x in element.components)
                        {
                            Item item = ItemManager.CreateByName(x);
                            if (item != null)
                            {
                                int slot = 0;
                                if (x.Contains("carburetor")) slot = 1;
                                else if (x.Contains("plug")) slot = 2;
                                else if (x.Contains("piston")) slot = 4;
                                else if (x.Contains("valve")) slot = 3;
                                item.MoveToContainer(engineStorage.inventory, slot, false);
                            }
                        }*/
                    }
                });
            }
            foreach (var baseEntity in entity.children)
            {
                //Debug.Log(baseEntity.PrefabName);
                if (baseEntity.PrefabName.Contains("fuel"))
                {
                    StorageContainer storageContainer = baseEntity as StorageContainer;
                    if (storageContainer != null)
                    {
                        storageContainer.inventory.Clear();
                        FillTheTank(storageContainer.inventory, element.Fuel);
                    }
                }
            }

            double priceModifier = PriceModifier(player);
            EconomicsEvo.Call("RemoveBalanceByID", player.userID,config.CurrencySettinfgs.CurrencyKey,element.Price*priceModifier);
            WMenu.Call("UpdateBalance", player);
            Broadcast.Call("GetPlayerNotice", player, "Автомастерская",$"Транспорт \"{element.Name}\" успешно приобретен!",config.VehileSettings[number].Image,"assets/bundled/prefabs/fx/invite_notice.prefab");
            Effect fx = new Effect("assets/bundled/prefabs/fx/notice/loot.drag.dropsuccess.fx.prefab", player, 0, new Vector3(), new Vector3());
            EffectNetwork.Send(fx, player.Connection);
            CuiHelper.DestroyUi(player, "MenuUI");
            /*player.SendConsoleCommand("gametip.showgametip", );
            timer.Once(3f, () => player?.SendConsoleCommand("gametip.hidegametip"));*/
        }
        
        private static EngineStorage GetEngineStorage(BaseVehicleModule module)
        {
            var engineModule = module as VehicleModuleEngine;
            if (engineModule == null) return null;

            return engineModule.GetContainer() as EngineStorage;
        }

        private void FillTheTank(ItemContainer container, int amount) => ItemManager.CreateByItemID(-946369541, amount).MoveToContainer(container);

        #endregion

        #region UI

        private string _layer = "VSHopUI";

        void OpenUI(BasePlayer player,int page = 0)
        {
            double priceModifier = PriceModifier(player);
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = {AnchorMin = "0.5 0.5",AnchorMax = "0.5 0.5",OffsetMin = "-490 -270",OffsetMax = "510 270"},
                Image = {Color = "0 0 0 0"}
            }, "ContentUI", _layer);

            double startx = 0, starty = 0, panelHeight = 118,panelLenght = 300;
            int x = 0;
            if (config.VehileSettings.Count>(page+1)*12)
            {
                PrintWarning(config.VehileSettings.Count.ToString());
                container.Add(new CuiButton
                {
                    RectTransform = {AnchorMin = "0 0",AnchorMax = "0 0",OffsetMin = "120 -50",OffsetMax = "200 -10"},
                    Button = {Color = "0.27 0.27 0.27 0.9",Close = _layer,Command = $"vehilepage {page+1}"},
                    Text = {Text = "Вперед",Align = TextAnchor.MiddleCenter}
                },_layer);
            }

            if (page>0)
            {
                container.Add(new CuiButton
                {
                    RectTransform = {AnchorMin = "0 0",AnchorMax = "0 0",OffsetMin = "20 -50",OffsetMax = "100 -10"},
                    Button = {Color = "0.27 0.27 0.27 0.9",Close = _layer,Command = $"vehilepage {page-1}"},
                    Text = {Text = "Назад",Align = TextAnchor.MiddleCenter}
                },_layer);
            }

            /*int a = 1;
            foreach (var vehile in config.VehileSettings)
            {
                PrintWarning($"{a}.{vehile.Name}");
                a++;
            }*/

            int i = 0;
            foreach (var vehile in config.VehileSettings.Skip(page*12).Take(12))
            {
                container.Add(new CuiPanel
                {
                    RectTransform = {AnchorMin = "0 1",AnchorMax = "0 1",OffsetMin = $"{startx} {starty-panelHeight}",OffsetMax = $"{startx+panelLenght} {starty}"},
                    Image = {Color = "0 0 0 0"},
                }, _layer, $"Vehile{i}");

                container.Add(new CuiElement
                {
                    Parent = $"Vehile{i}",
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",AnchorMax = "1 1"
                        },
                        new CuiRawImageComponent{Png = (string) ImageLibrary.Call("GetImage","https://i.ibb.co/zVBbxnm/vbg.png")}
                    }
                });
                container.Add(new CuiLabel
                {
                    RectTransform = {AnchorMin = "0 1",AnchorMax = "0 1",OffsetMin = $"{panelHeight+2} -55",OffsetMax = $"{panelLenght} -10"},
                    Text = {Align = TextAnchor.UpperCenter,Text = vehile.Name,FontSize = 14}
                }, $"Vehile{i}");

                container.Add(new CuiElement
                {
                    Parent = $"Vehile{i}",
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",AnchorMax = "0 0",OffsetMin = "5 5",OffsetMax = $"{panelHeight-5} {panelHeight-5}"
                        },
                        new CuiRawImageComponent{Png = (string) ImageLibrary.Call("GetImage",vehile.Image)}
                    }
                });

                container.Add(new CuiLabel
                {
                    RectTransform = {AnchorMin = "0 1",AnchorMax = "0 1",OffsetMin = $"{panelHeight} {-panelHeight}",OffsetMax = $"{panelLenght} -20"},
                    Text = {Text = $"{vehile.Price*priceModifier} {config.CurrencySettinfgs.DisplayName}",FontSize = 15,Align = TextAnchor.MiddleCenter}
                }, $"Vehile{i}");
                container.Add(new CuiButton
                {
                    RectTransform = {AnchorMin = "0 0",AnchorMax = "0 0",OffsetMin = $"{panelHeight+5} 5",OffsetMax = $"{panelLenght-5} 25"},
                    //Button = {Color = "0.20 0.25 0.37 1",Command = $"buyvehile {i}"},
                    Button = {Color = "0.88 0.83 0.63 0.9",Command = $"buyvehile {i}"},
                    Text = {Text = "КУПИТЬ",FontSize = 15,Align = TextAnchor.MiddleCenter, Color = "0.47 0 0 0.95"}
                }, $"Vehile{i}");
                starty -= panelHeight + 5;
                if (x == 3)
                {
                    startx += panelLenght + 5;
                    starty = 0;
                    x = -1;

                }

                i++;
                x++;
            }
            /*for (int i = 0; i < 12; i++)
            {
                VehileSetting vehile = config.VehileSettings[page*12+i];
                container.Add(new CuiPanel
                {
                    RectTransform = {AnchorMin = "0 1",AnchorMax = "0 1",OffsetMin = $"{startx} {starty-panelHeight}",OffsetMax = $"{startx+panelLenght} {starty}"},
                    Image = {Color = "0 0 0 0"},
                }, _layer, $"Vehile{i}");

                container.Add(new CuiElement
                {
                    Parent = $"Vehile{i}",
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",AnchorMax = "1 1"
                        },
                        new CuiRawImageComponent{Png = (string) ImageLibrary.Call("GetImage","https://i.ibb.co/jV0BbrF/cars.png")}
                    }
                });
                container.Add(new CuiLabel
                {
                    RectTransform = {AnchorMin = "0 1",AnchorMax = "0 1",OffsetMin = $"{panelHeight+2} -55",OffsetMax = $"{panelLenght} 0"},
                    Text = {Align = TextAnchor.UpperCenter,Text = vehile.Name,FontSize = 18}
                }, $"Vehile{i}");

                container.Add(new CuiElement
                {
                    Parent = $"Vehile{i}",
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",AnchorMax = "0 0",OffsetMin = "5 5",OffsetMax = $"{panelHeight-5} {panelHeight-5}"
                        },
                        new CuiRawImageComponent{Png = (string) ImageLibrary.Call("GetImage",vehile.Image)}
                    }
                });

                container.Add(new CuiLabel
                {
                    RectTransform = {AnchorMin = "0 1",AnchorMax = "0 1",OffsetMin = $"{panelHeight} {-panelHeight}",OffsetMax = $"{panelLenght} -20"},
                    Text = {Text = $"{vehile.Price*priceModifier} {config.CurrencySettinfgs.DisplayName}",FontSize = 15,Align = TextAnchor.MiddleCenter}
                }, $"Vehile{i}");
                container.Add(new CuiButton
                {
                    RectTransform = {AnchorMin = "0 0",AnchorMax = "0 0",OffsetMin = $"{panelHeight+5} 5",OffsetMax = $"{panelLenght-5} 25"},
                    //Button = {Color = "0.20 0.25 0.37 1",Command = $"buyvehile {i}"},
                    Button = {Color = "0.27 0.27 0.27 1",Command = $"buyvehile {i}"},
                    Text = {Text = "Купить",FontSize = 15,Align = TextAnchor.MiddleCenter}
                }, $"Vehile{i}");
                starty -= panelHeight + 5;
                if (x == 3)
                {
                    startx += panelLenght + 5;
                    starty = 0;
                    x = -1;

                }

                x++;

            }*/

            CuiHelper.AddUi(player, container);
            
        }

        #endregion

        #region commands

        [ConsoleCommand("vehilemenu")]
        void OpenMenu(ConsoleSystem.Arg arg)
        {
            OpenUI(arg.Player());
        }
        
        [ConsoleCommand("vehilepage")]
        void PageOpen(ConsoleSystem.Arg arg)
        {
            OpenUI(arg.Player(),arg.Args[0].ToInt());
        }

        [ConsoleCommand("buyvehile")]
        void BuyVehileCommand(ConsoleSystem.Arg arg)
        {
            double priceModifier = PriceModifier(arg.Player());
            VehileSetting vehile = config.VehileSettings[arg.Args[0].ToInt()];
            if ((double)EconomicsEvo.Call("GetBalance",arg.Player().userID,config.CurrencySettinfgs.CurrencyKey) >= vehile.Price*priceModifier)
            {
                SpawnVehile(arg.Player(),arg.Args[0].ToInt());
            }
            else
            {
                Broadcast.Call("GetPlayerNotice", arg.Player(), "Покупка транспорта","НЕ ХВАТАЕТ ЭДДИ!","infoicn","assets/bundled/prefabs/fx/entities/loot_barrel/impact.prefab");
                
            }
        }

        #endregion
    }
}