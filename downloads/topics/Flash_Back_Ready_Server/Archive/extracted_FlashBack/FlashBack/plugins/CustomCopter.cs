using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("CustomCopter", "BadMandarin", "1.0.0")]
    [Description("CustomCopter")]
    class CustomCopter : RustPlugin
    {
        #region Classes
        private class PluginConfig
        {
            public List<CopterPart> copterParts;
            public List<Modification> modifications;
            public string CopterImage;
            public CopterItem copterItem;
        }
        
        private class CopterPart
        {
            public string ShortName;
            public string DisplayName;
            public int Amount;
            public ulong SkinId;
            public string Image;
            public int StackSize;
            public List<LootSetting> lootSettings;
        }

        private class Modification
        {
            public string KeyName;
            public string DisplayName;
            public string Image;
            public string PriceName;
            public int PriceAmount;
        }

        private class LootSetting
        {
            public string ShortName;
            public int MinAmount;
            public int MaxAmount;
            public int Chance;
        }

        private class CopterItem
        {
            public string ShortName;
            public string DisplayName;
            public ulong SkinId;
            public List<string> modifications;
        }
        private class TempData
        {
            public List<string> modifications;
        }
        #endregion

        #region Variables
        [PluginReference] private Plugin ImageLibrary;

        private PluginConfig config;
        private bool initiated = false;

        private Dictionary<uint, List<string>> customCopters;
        private string DataPath = "CustomCopter/Data";

        private Dictionary<ulong, List<string>> playerTemp;
        #endregion

        #region Oxide
        private void Init()
        {
            try
            {
                config = Config.ReadObject<PluginConfig>();
            }
            catch (Exception e) { LoadDefaultConfig(); }
        }

        void OnServerInitialized()
        {
            initiated = false;
            LoadImages(10);

            playerTemp = new Dictionary<ulong, List<string>>();

            customCopters = new Dictionary<uint, List<string>>();
            if (Interface.Oxide.DataFileSystem.ExistsDatafile(DataPath))
                customCopters = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<uint, List<string>>>(DataPath);

            if (customCopters == null) customCopters = new Dictionary<uint, List<string>>();
        }

        void Unload()
        {
            playerTemp.Clear();
            Interface.Oxide.DataFileSystem.WriteObject(DataPath, customCopters);
        }

        private Item OnItemSplit(Item item, int amount)
        {
            CopterPart citem = config.copterParts.FirstOrDefault(x => x.SkinId == item.skin);
            if (citem != null)
            {
                Item x = ItemManager.CreateByPartialName(citem.ShortName, amount);
                x.name = citem.DisplayName;
                x.skin = citem.SkinId;
                x.amount = amount;
                item.amount -= amount;
                return x;
            }

            return null;
        }

        object OnMaxStackable(Item item)
        {
            if (item.skin == 0) return null;
            CopterPart citem = config.copterParts.FirstOrDefault(x => x.SkinId == item.skin);
            if (citem != null)
            {
                return citem.StackSize;
            }
            return null;
        }

        void OnEntityBuilt(Planner planner, GameObject gameobject)
        {
            if (!initiated) return;
            BaseEntity entity = gameobject.ToBaseEntity();
            BasePlayer player = planner.GetOwnerPlayer();

            if (player == null || entity == null) return;

            if (entity.skinID != config.copterItem.SkinId) return;

            NextTick(() => entity.Kill());

            Vector3 ePos = entity.transform.position;

            BaseEntity Copter = GameManager.server.CreateEntity("assets/content/vehicles/minicopter/minicopter.entity.prefab", ePos, entity.transform.rotation, true);

            RaycastHit rHit;

            if (Physics.Raycast(new Vector3(ePos.x, ePos.y + 1, ePos.z), Vector3.down, out rHit, 2f, LayerMask.GetMask(new string[] { "Construction" })) && rHit.GetEntity() != null)
            {
                SendReply(player, "Коптер может быть установлен только на землю!");
                Item copter = ItemManager.CreateByName(config.copterItem.ShortName, 1, config.copterItem.SkinId);
                copter.name = config.copterItem.DisplayName;
                player.GiveItem(copter, BaseEntity.GiveItemReason.Crafted);
                Copter.Kill();
                return;
            }

            Copter.OwnerID = player.userID;
            Copter.Spawn();


            uint itemid = planner.GetItem().uid;
            if (customCopters.ContainsKey(itemid))
            {
                if (customCopters[itemid].Contains("chair"))
                    SetChair(Copter);
                if (customCopters[itemid].Contains("health"))
                    SetupProtection(Copter as BaseCombatEntity, 1500f);

                customCopters.Remove(itemid);
            }

        }

        private void SetupProtection(BaseCombatEntity copter, float health)
        {
            copter._maxHealth = health;
            copter.health = health;
        }

        void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (player == null || entity == null) return;

            var container = entity as LootContainer;
            if (container == null || container.inventory.itemList == null || container.OwnerID != 0 || container.flags.HasFlag(BaseEntity.Flags.Reserved6)) return;

            foreach(var i in config.copterParts)
            {
                var n = i.lootSettings.FirstOrDefault(x => x.ShortName.Contains(entity.ShortPrefabName));
                if (n != null)
                {
                    if (Core.Random.Range(0, 100) <= n.Chance)
                    {
                        var item = ItemManager.CreateByName(i.ShortName, Core.Random.Range(n.MinAmount, n.MaxAmount), i.SkinId);
                        item.name = i.DisplayName;
                        if (container.inventory.itemList.Count == container.inventory.capacity)
                            container.inventory.capacity++;

                        item.MoveToContainer(container.inventory);
                    }
                }
            }
            container.SetFlag(BaseEntity.Flags.Reserved6, true);
        }
        #endregion

        #region Interface
        string UI_Layer = "UI_CustomCopter";
        private void Draw_CopterCraft(BasePlayer player)
        {
            if (!playerTemp.ContainsKey(player.userID)) playerTemp.Add(player.userID, new List<string>());

            CuiElementContainer container = new CuiElementContainer()
            {
                {
                    new CuiPanel
                    {
                        CursorEnabled = true,
                        RectTransform =
                        {
                                AnchorMin = $"0 0",
                                AnchorMax = $"1 1",
                                OffsetMin = "0 0",
                                OffsetMax = "0 0"
                        },
                        Image = {Color = "0 0 0 0.5", Material = "assets/content/ui/uibackgroundblur.mat" }
                    }, "Overlay", UI_Layer
                },
                {
                    new CuiElement
                    {
                        Parent = UI_Layer,
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = "КРАФТ КОПТЕРА", Align = TextAnchor.UpperCenter, Font = "RobotoCondensed-Bold.ttf", Color = GetColor("#FFFFFF", 0.8f), FontSize = 30
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = $"0 0",
                                AnchorMax = $"1 1",
                                OffsetMin = "0 0",
                                OffsetMax = "0 -10"
                            }
                        }
                    }
                },
                {
                    new CuiElement
                    {
                        Parent = UI_Layer,
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                Png = GetImage(GetNameByURL(config.CopterImage))
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = $"0.5 1",
                                AnchorMax = $"0.5 1",
                                OffsetMin = "-100 -300",
                                OffsetMax = "100 -100"
                            }
                        }
                    }
                },
                {
                    new CuiElement
                    {
                        Parent = UI_Layer,
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = "НЕОБХОДИМЫЕ ПРЕДМЕТЫ ДЛЯ КРАФТА", Align = TextAnchor.UpperCenter, Font = "RobotoCondensed-Bold.ttf", Color = GetColor("#FFFFFF", 0.8f), FontSize = 30
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = $"0 0",
                                AnchorMax = $"1 1",
                                OffsetMin = "0 0",
                                OffsetMax = "0 -350"
                            }
                        }
                    }
                },
                {
                    new CuiElement
                    {
                        Parent = UI_Layer,
                        Name = $"{UI_Layer}.ReceipeBoxBG",
                        Components =
                        {
                            new CuiImageComponent
                            {
                                Color = GetColor("#000000", 0.5f)
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = $"0 1",
                                AnchorMax = $"1 1",
                                OffsetMin = $"0 -500",
                                OffsetMax = $"0 -400"
                            }
                        }
                    }
                },
                {
                    new CuiElement
                    {
                        Parent = $"{UI_Layer}.ReceipeBoxBG",
                        Name = $"{UI_Layer}.ReceipeBox",
                        Components =
                        {
                            new CuiImageComponent
                            {
                                Color = GetColor("#000000", 0f)
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = $"0.5 0",
                                AnchorMax = $"0.5 1",
                                OffsetMin = $"{GetBlockWidth(config.copterParts.Count(), 5, 100)/-2} 0",
                                OffsetMax = $"{GetBlockWidth(config.copterParts.Count(), 5, 100)/2} 0"
                            }
                        }
                    }
                },
                {
                    new CuiElement
                    {
                        Parent = UI_Layer,
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = "ВЫБЕРИТЕ НУЖНЫЕ МОДИФИКАЦИИ", Align = TextAnchor.UpperCenter, Font = "RobotoCondensed-Bold.ttf", Color = GetColor("#FFFFFF", 0.8f), FontSize = 30
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = $"0 0",
                                AnchorMax = $"1 1",
                                OffsetMin = "0 0",
                                OffsetMax = "0 -510"
                            }
                        }
                    }
                },
                {
                    new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Button = { Color = "0 0 0 0", Close = UI_Layer },
                        Text = { Text = "" }
                    }, UI_Layer
                },
                {
                    new CuiElement
                    {
                        Parent = UI_Layer,
                        Name = $"{UI_Layer}.ModifiBoxG",
                        Components =
                        {
                            new CuiImageComponent
                            {
                                Color = GetColor("#000000", 0.5f)
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = $"0 1",
                                AnchorMax = $"1 1",
                                OffsetMin = "0 -650",
                                OffsetMax = "0 -550"
                            }
                        }
                    }
                },
                {
                    new CuiElement
                    {
                        Parent = $"{UI_Layer}.ModifiBoxG",
                        Name = $"{UI_Layer}.ModifiBox",
                        Components =
                        {
                            new CuiImageComponent
                            {
                                Color = GetColor("#000000", 0f)
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = $"0.5 0",
                                AnchorMax = $"0.5 1",
                                OffsetMin = $"{GetBlockWidth(config.modifications.Count(), 5, 100)/-2} 0",
                                OffsetMax = $"{GetBlockWidth(config.modifications.Count(), 5, 100)/2} 0"
                            }
                        }
                    }
                },
                {
                    new CuiButton
                    {
                        RectTransform = {
                            AnchorMin = $"0.5 0",
                            AnchorMax = $"0.5 0",
                            OffsetMin = "-80 10",
                            OffsetMax = "80 60"
                        },
                        Button = { Color = GetColor("#000000", 0.5f), Command = "UI_CCHandler craft" },
                        Text = { Text = "СОЗДАТЬ", Align = TextAnchor.MiddleCenter, Font = "RobotoCondensed-Bold.ttf", Color = GetColor("#FFFFFF", 0.8f), FontSize = 30 }
                    }, UI_Layer
                },
            };

            int counter = 0;
            foreach(var part in config.copterParts)
            {
                container.Add(new CuiElement
                {
                    Parent = $"{UI_Layer}.ReceipeBox",
                    Name = $"{UI_Layer}.ReceipeBox.{counter}",
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = GetColor("#000000", 0.5f)
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "0 1",
                            OffsetMin = $"{5 + counter * 95} 5",
                            OffsetMax = $"{95 + counter * 95} -5"
                        },
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = $"{UI_Layer}.ReceipeBox.{counter}",
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = GetImage(GetNameByURL(part.Image))
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.2 0.2",
                            AnchorMax = "0.8 0.8",
                        },
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = $"{UI_Layer}.ReceipeBox.{counter}",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = "x" + part.Amount, Align = TextAnchor.LowerCenter, Font = "RobotoCondensed-Regular.ttf", Color = GetColor("#FFFFFF", 0.8f)
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1",
                        },
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = $"{UI_Layer}.ReceipeBox.{counter}",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = part.DisplayName, Align = TextAnchor.UpperCenter, Font = "RobotoCondensed-Regular.ttf", Color = GetColor("#FFFFFF", 0.8f)
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1",
                        },
                        new CuiOutlineComponent { Distance = "0.5 -0.5", Color = GetColor("#000000", 1f)}
                    }
                });
                counter++;
            }


            counter = 0;
            foreach (var part in config.modifications)
            {
                container.Add(new CuiElement
                {
                    Parent = $"{UI_Layer}.ModifiBox",
                    Name = $"{UI_Layer}.ModifiBox.{counter}",
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = GetColor("#000000", 0.5f)
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "0 1",
                            OffsetMin = $"{5 + counter * 95} 5",
                            OffsetMax = $"{95 + counter * 95} -5"
                        },
                    }
                });
                string image = part.Image.Contains("http") ? GetNameByURL(part.Image) : part.Image;
                container.Add(new CuiElement
                {
                    Parent = $"{UI_Layer}.ModifiBox.{counter}",
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = GetImage(image)
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.2 0.2",
                            AnchorMax = "0.8 0.8",
                        },
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = $"{UI_Layer}.ModifiBox.{counter}",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = part.DisplayName, Align = TextAnchor.UpperCenter, Font = "RobotoCondensed-Regular.ttf", Color = GetColor("#FFFFFF", 0.8f)
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1",
                        },
                        new CuiOutlineComponent { Distance = "0.5 -0.5", Color = GetColor("#000000", 1f)}
                    }
                });
                bool contains = playerTemp[player.userID].Contains(part.KeyName);
                int itemid = ItemManager.FindItemDefinition(part.PriceName).itemid;
                if (player.inventory.GetAmount(itemid) < part.PriceAmount)
                {
                    container.Add(new CuiElement
                    {
                        Parent = $"{UI_Layer}.ModifiBox.{counter}",
                        Name = $"{UI_Layer}.ModifiBox.{counter}.Price",
                        Components =
                        {
                            new CuiImageComponent
                            {
                                Color = GetColor("#FA5858", 0.5f)
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = $"0 0",
                                AnchorMax = $"1 0",
                                OffsetMin = "0 0",
                                OffsetMax = "0 20"
                            },
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = $"{UI_Layer}.ModifiBox.{counter}.Price",
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                Png = GetImage(part.PriceName)
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = $"0 0",
                                AnchorMax = $"0 1",
                                OffsetMin = "10 0",
                                OffsetMax = "30 0"
                            },
                        }
                    });
                    container.Add(new CuiElement
                    {
                        Parent = $"{UI_Layer}.ModifiBox.{counter}.Price",
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = $"x{part.PriceAmount}", Align = TextAnchor.MiddleLeft, Font = "RobotoCondensed-Regular.ttf", Color = GetColor("#FFFFFF", 0.8f)
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = $"0 0",
                                AnchorMax = $"1 1",
                                OffsetMin = "30 0",
                                OffsetMax = "0 0"
                            },
                        }
                    });
                }
                else
                {
                    container.Add(new CuiButton
                    {
                        RectTransform = {
                            AnchorMin = $"0 0",
                            AnchorMax = $"1 0",
                            OffsetMin = "0 0",
                            OffsetMax = "0 20"
                        },
                        Button = { Color = GetColor(contains ? "#FA5858" : "#82FA58", 0.5f), Command = $"UI_CCHandler modif {part.KeyName}" },
                        Text = { Text = contains ? "УБРАТЬ" : "ДОБАВИТЬ", Align = TextAnchor.MiddleCenter, Font = "RobotoCondensed-Bold.ttf", Color = GetColor("#FFFFFF", 0.8f) }
                    }, $"{UI_Layer}.ModifiBox.{counter}");
                }
                counter++;
            }
            
            CuiHelper.DestroyUi(player, UI_Layer);
            CuiHelper.AddUi(player, container);
        }
        #endregion

        #region Commands
        [ConsoleCommand("UI_CCHandler")]
        private void Console_CCHandler(ConsoleSystem.Arg arg)
        {
            if (!initiated) return;
            BasePlayer player = arg.Player();
            if (player == null) return;
            if (arg.Args.Length < 1) return;
            switch (arg.Args[0])
            {
                case "craft":
                    Dictionary<Item, int> items = new Dictionary<Item, int>();
                    bool success = true;
                    foreach (var craftedItem in config.copterParts)
                    {
                        var def = ItemManager.FindItemDefinition(craftedItem.ShortName);
                        var haveItem = HaveItem(player, def.itemid, craftedItem.SkinId, craftedItem.Amount);
                        if (!haveItem)
                        {
                            success = false;
                            SendReply(player,
                                "Вы не можете скрафтить предмет! Не хватает ингредиента!");
                            return;
                        }
                        var itemCraft = FindItem(player, def.itemid, craftedItem.SkinId, craftedItem.Amount);
                        
                        items.Add(itemCraft, craftedItem.Amount);
                    }

                    if (playerTemp[player.userID].Count() > 0)
                    {
                        foreach(var m in config.modifications)
                        {
                            if (playerTemp[player.userID].Contains(m.KeyName))
                            {
                                var def = ItemManager.FindItemDefinition(m.PriceName);
                                var haveItem = HaveItem(player, def.itemid, 0, m.PriceAmount);
                                if (!haveItem)
                                {
                                    success = false;
                                    SendReply(player,
                                        "Вы не можете скрафтить предмет! Не хватает ингредиента!");
                                    return;
                                }
                                var itemCraft = FindItem(player, def.itemid, 0, m.PriceAmount);
                                
                                items.Add(itemCraft, m.PriceAmount);
                            }
                        }
                    }

                    foreach (var itemCraft in items)
                    {
                        itemCraft.Key.UseItem(itemCraft.Value);
                    }

                    if (success)
                    {
                        Item copter = ItemManager.CreateByName(config.copterItem.ShortName, 1, config.copterItem.SkinId);
                        copter.name = config.copterItem.DisplayName;
                        player.GiveItem(copter, BaseEntity.GiveItemReason.Crafted);
                        if(playerTemp[player.userID].Count() > 0)
                        {
                            customCopters.Add(copter.uid, new List<string>());
                            foreach (var m in playerTemp[player.userID]) customCopters[copter.uid].Add(m);
                        }
                    }
                    break;
                case "modif":
                    string keyname = arg.Args[1];
                    if (playerTemp[player.userID].Contains(keyname)) playerTemp[player.userID].Remove(keyname);
                    else playerTemp[player.userID].Add(keyname);
                    Draw_CopterCraft(player);
                    break;
            }
        }
        
        [ChatCommand("copter")]
        void Chat_CraftCopter(BasePlayer player, string command, string[] args)
        {
            if (!initiated) return;
            if (!playerTemp.ContainsKey(player.userID)) playerTemp.Add(player.userID, new List<string>());
            playerTemp[player.userID] = new List<string>();
            Draw_CopterCraft(player);
        }
        #endregion

        #region Utils
        private void SetChair(BaseEntity Copter)
        {
            BaseEntity Chair = GameManager.server.CreateEntity("assets/bundled/prefabs/static/chair.static.prefab", Copter.transform.localPosition, Copter.transform.localRotation, true);
            Chair.Spawn();
            Chair.SetParent(Copter);
            Chair.transform.localPosition = new Vector3(-0.01f, 0.8f, -0.4f);
            Chair.transform.localRotation = new Quaternion(0, 1, 0, 0);
        }

        public Item FindItem(BasePlayer player, int itemID, ulong skinID, int amount)
        {
            Item item = null;

            if (skinID == 0U)
            {
                if (player.inventory.FindItemID(itemID) != null && player.inventory.FindItemID(itemID).amount >= amount)
                    return player.inventory.FindItemID(itemID);
            }
            else
            {

                List<Item> items = new List<Item>();

                items.AddRange(player.inventory.FindItemIDs(itemID));

                foreach (var findItem in items)
                {
                    if (findItem.skin == skinID && findItem.amount >= amount)
                    {
                        return findItem;
                    }
                }
            }

            return item;
        }

        public bool HaveItem(BasePlayer player, int itemID, ulong skinID, int amount)
        {
            if (skinID == 0U)
            {
                if (player.inventory.FindItemID(itemID) != null &&
                    player.inventory.FindItemID(itemID).amount >= amount) return true;
                return false;
            }

            List<Item> items = new List<Item>();

            items.AddRange(player.inventory.FindItemIDs(itemID));

            foreach (var item in items)
            {
                if (item.skin == skinID && item.amount >= amount)
                {
                    return true;
                }
            }

            return false;
        }

        private int GetBlockWidth(int itms, int distance, int size)
        {
            return distance * itms + 1 + size * itms;
        }

        public static string GetColor(string hex, float alpha = 1f)
        {
            var color = ColorTranslator.FromHtml(hex);
            var r = Convert.ToInt16(color.R) / 255f;
            var g = Convert.ToInt16(color.G) / 255f;
            var b = Convert.ToInt16(color.B) / 255f;

            return $"{r} {g} {b} {alpha}";
        }

        private string GetImage(string name, ulong skinid = 0)
        {
            string ID = (string)ImageLibrary?.Call("GetImage", name, skinid);
            if (ID == "")
                ID = (string)ImageLibrary?.Call("GetImage", name, skinid) ?? ID;

            return ID;
        }

        private void AddImage(string name)
        {
            if (!name.Contains("http")) return;
            if (!(bool)ImageLibrary.Call("HasImage", GetNameByURL(name)))
                ImageLibrary.Call("AddImage", name, GetNameByURL(name));
        }

        private static string GetNameByURL(string url)
        {
            var splitted = url.Split('/');
            var endUrl = splitted[splitted.Length - 1];
            var name = endUrl.Split('.')[0];
            return name;
        }

        private void LoadImages(int times)
        {
            if(times < 1)
            {
                PrintError("Plugin start failed after several retries, please, check ImageLibrary.");
                return;
            }

            if (plugins.Exists("ImageLibrary"))
            {
                PrintWarning($"ImageLibrary found! Loading Images...");
                AddImage(config.CopterImage);
                foreach (var im in config.copterParts) AddImage(im.Image);
                foreach (var im in config.modifications) AddImage(im.Image);

                CheckStatus();
            }
            else
            {
                PrintWarning($"ImageLibrary not found! Trying to find it again... ({times})");
                timer.Once(2f, () => LoadImages(--times));
            }
        }

        private void CheckStatus()
        {
            if (!(bool)ImageLibrary.Call("IsReady"))
            {
                PrintError("Plugin is not ready! Images are loading.");
                timer.Once(10f, () => CheckStatus());
            }
            else
            {
                initiated = true;
                PrintWarning("Plugin succesfully loaded! Author: BadMandarin.");
            }
        }

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(GetDefaultConfig(), true);
        }

        private PluginConfig GetDefaultConfig()
        {
            return new PluginConfig
            {
                CopterImage = "https://i.imgur.com/Wq5J3Im.png",
                copterParts = new List<CopterPart>()
                {
                    new CopterPart()
                    {
                        ShortName = "glue",
                        DisplayName = "Двигатель",
                        SkinId = 1969843330,
                        Image = "https://i.imgur.com/6CO3a4M.png",
                        StackSize = 1,
                        Amount = 1,
                        lootSettings = new List<LootSetting>()
                        {
                            new LootSetting()
                            {
                                ShortName = "crate_elite",
                                MinAmount = 1,
                                MaxAmount = 1,
                                Chance = 10
                            }
                        }
                    },
                    new CopterPart()
                    {
                        ShortName = "glue",
                        DisplayName = "Лопасть",
                        SkinId = 1969843074,
                        Image = "https://i.imgur.com/5OSx2SN.png",
                        StackSize = 4,
                        Amount = 4,
                        lootSettings = new List<LootSetting>()
                        {
                            new LootSetting()
                            {
                                ShortName = "crate_normal",
                                MinAmount = 1,
                                MaxAmount = 2,
                                Chance = 40
                            }
                        }
                    },
                },
                modifications = new List<Modification>()
                {
                    new Modification()
                    {
                        KeyName = "chair",
                        DisplayName = "Стул",
                        PriceName = "chair",
                        PriceAmount = 1,
                        Image = "chair"
                    },
                    new Modification()
                    {
                        KeyName = "health",
                        DisplayName = "Здоровье",
                        PriceName = "metal.refined",
                        PriceAmount = 50,
                        Image = "metal.refined"
                    },
                },
                copterItem = new CopterItem()
                {
                    ShortName = "stash.small",
                    DisplayName = "Коптер",
                    SkinId = 1764533101,
                    modifications = new List<string>()
                }
            };
        }
        #endregion
    }
}