
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("AutoKit", "", "1.0.1")]
    class AutoKit : RustPlugin
    {
        [PluginReference] Plugin ImageLibrary;
        #region Configuration
        private static Configuration _config = new Configuration();

        public class Configuration
        {
            [JsonProperty("Удалять индивидуальные киты когда происходит вайп?")]
            public bool DeleteOnVipe { get; set; } = false;

            [JsonProperty("Ключи китов и их привилегии(чем выше, тем больше приоритет)")]
            public List<Kit> Kits = new List<Kit>();

            internal class Kit
            {
                [JsonProperty("Название файла в oxide/date/AutoKit")]
                public string file;

                [JsonProperty("Permission для кита")] public string perm;
            }

            public static Configuration GetNewConfiguration()
            {
                return new Configuration
                {
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) LoadDefaultConfig();
            }
            catch
            {
                Puts("!!!!ОШИБКА КОНФИГУРАЦИИ!!!! Проверьте парамеры конфига!");
            }

            NextTick(SaveConfig);
        }

        protected override void LoadDefaultConfig() => _config = Configuration.GetNewConfiguration();
        protected override void SaveConfig() => Config.WriteObject(_config);

        #endregion

        internal enum TypeContent
        {
            Ammo,
            Contents
        }

        #region Data

        public class Inventory
        {
            [JsonProperty("DEL")] public bool deleted { get; set; } = false;
            [JsonProperty("Perm")] public string perm;
            [JsonProperty("hotslots")] public List<DatItem> hotslots;
            [JsonProperty("main")] public List<DatItem> main;
            [JsonProperty("wear")] public List<DatItem> wear;
            
        }

        public class DatItem
        {
            [JsonProperty("shortname")] public string shortname;
            [JsonProperty("skin")] public ulong SkinId;
            [JsonProperty("pos")] public int pos;
            [JsonProperty("ammount")] public int ammount;
            [JsonProperty("content")] public List<ItemContent> content;
        }

        public class ItemContent
        {
            [JsonProperty("type")] public TypeContent Type;
            [JsonProperty("shortname")] public string shortname;
            [JsonProperty("amount")] public int amount;
        }

        #endregion

        public string json;

        List<ItemContent> getItemContent(Item it)
        {

            List<ItemContent> ItemContent = new List<ItemContent>();
            if (it.contents != null)
                foreach (Item item in it.contents.itemList)
                {
                    ItemContent t = new ItemContent();
                    t.Type = TypeContent.Contents;
                    t.shortname = item.info.shortname;
                    t.amount = item.amount;
                    ItemContent.Add(t);
                }

            BaseProjectile Weapon = it.GetHeldEntity() as BaseProjectile;
            if (Weapon != null)
            {
                ItemContent t = new ItemContent();
                t.Type = TypeContent.Ammo;
                t.shortname = Weapon.primaryMagazine.ammoType.shortname;
                t.amount = Weapon.primaryMagazine.contents == 0 ? 1 : Weapon.primaryMagazine.contents;
                ItemContent.Add(t);
            }

            return ItemContent;
        }

        void OnServerInitialized()
        {
            foreach (Configuration.Kit t in _config.Kits)
            {
                permission.RegisterPermission(t.perm, this);
            }

            ImageLibrary.Call("AddImage", "https://imgur.com/JbWFTYR.png", "https://imgur.com/JbWFTYR.png");
            timer.Once(5f, () => CreateUI());
            AddCovalenceCommand("loadout", nameof(AnotherCommandsExec));
            AddCovalenceCommand("savekit", nameof(AnotherCommandsExec));
        }
        
        void CreateUI()
        {
            CuiElementContainer container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0 0 0 0"
                },
                RectTransform =
                {
                    AnchorMin = "0.5 0",
                    AnchorMax = "0.5 0",
                    OffsetMin = "-350 170",
                    OffsetMax = "-217 370"
                }
            }, "Overlay", "AutoKit_main");
            container.Add(new CuiElement
            {
            Parent = "AutoKit_main",
            Name = "AutoKit_plate",
            Components =
            {
                new CuiRawImageComponent
                {
                    Png = ImageLibrary.Call<string>("GetImage", "https://imgur.com/JbWFTYR.png"),
                },
                new CuiRectTransformComponent
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 1"
                }
            }
            });
            
            container.Add(new CuiButton
            {
                Button =
                {
                    Color = "0 0 0 0",
                    Command = "AutoKit_cons close",
                },
                Text =
                {
                    Text = ""
                },
                RectTransform =
                {
                    AnchorMin = "1 1",
                    AnchorMax = "1 1",
                    OffsetMin = "-35 -25",
                    OffsetMax = "0 0"
                }
            }, "AutoKit_main");
            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0.5 0.5",
                    AnchorMax = "0.5 0.5",
                    OffsetMin = "-60 20",
                    OffsetMax = "60 50"
                },
                Text =
                {
                    Text = "<b>ВАШ КИТ:</b>",
                    Align = TextAnchor.MiddleCenter
                }
            }, "AutoKit_main");
            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0.5 0.5",
                    AnchorMax = "0.5 0.5",
                    OffsetMin = "-60 -10",
                    OffsetMax = "60 20"
                },
                Text =
                {
                    Text = "<b>[S]</b>",
                    Align = TextAnchor.MiddleCenter,
                    Color = "[C]"
                }
            }, "AutoKit_main");
            container.Add(new CuiButton
            {
                Button =
                {
                    Color = "0 0 0 0",
                    Command = "AutoKit_cons save",
                },
                Text =
                {
                    Text = ""
                },
                RectTransform =
                {
                    AnchorMin = "0.5 0",
                    AnchorMax = "0.5 0",
                    OffsetMin = "-60 5",
                    OffsetMax = "-5 27"
                }
            }, "AutoKit_main");
            container.Add(new CuiButton
            {
                Button =
                {
                    Color = "0 0 0 0",
                    Command = "AutoKit_cons remove",
                },
                Text =
                {
                    Text = ""
                },
                RectTransform =
                {
                    AnchorMin = "0.5 0",
                    AnchorMax = "0.5 0",
                    OffsetMin = "5 5",
                    OffsetMax = "60 27"
                }
            }, "AutoKit_main");
            
            json = container.ToJson();

        }

        [ChatCommand("AKit_create")]
        void CmdCreate(BasePlayer player, string command, string[] arg)
        {
            if (!player.IsAdmin)
            {
                player.ChatMessage("You don't admin!");
            }

            if (arg.Length < 1)
            {
                player.ChatMessage("Invalid Syntax\n" +
                                   "/AKit_create [kitname]");
            }

            CreateItemData(player, arg[0]);
        }
        
        void CreateItemData(BasePlayer player, string filename)
        {
            Inventory dat;

            List<DatItem> hs = new List<DatItem>();
            List<DatItem> wr = new List<DatItem>();
            List<DatItem> mn = new List<DatItem>();
            //hot
            foreach (Item item in player.inventory.containerBelt.itemList)
            {
                if (item != null)
                    hs.Add(new DatItem
                    {
                        shortname = item.info.shortname,
                        SkinId = item.skin,
                        pos = item.position,
                        ammount = item.amount,
                        content = getItemContent(item)
                    });
            }

            //wear
            foreach (Item item in player.inventory.containerWear.itemList)
            {
                if (item != null)
                    wr.Add(new DatItem
                    {
                        shortname = item.info.shortname,
                        SkinId = item.skin,
                        pos = item.position,
                        ammount = item.amount,
                        content = getItemContent(item)
                    });
            }

            //main
            foreach (Item item in player.inventory.containerMain.itemList)
            {
                if (item != null)
                    mn.Add(new DatItem
                    {
                        shortname = item.info.shortname,
                        SkinId = item.skin,
                        pos = item.position,
                        ammount = item.amount,
                        content = getItemContent(item)

                    });
            }


            dat = new Inventory();
            dat.hotslots = hs;
            dat.wear = wr;
            dat.main = mn;
            Interface.Oxide.DataFileSystem.WriteObject<Inventory>($"AutoKit/{filename}", dat);
            _config.Kits.Add(new Configuration.Kit
                {
                    file = filename,
                    perm = $"AutoKit.{filename}"
                }
            );
            permission.RegisterPermission($"AutoKit.{filename}", this);
            SaveConfig();
            player.ChatMessage($"Created new kit file with name {filename}!");

        }


        [ChatCommand("giveKit")]
        void AdminGive(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
            {
                player.ChatMessage("Ты не админ!");
                return;
            }

            if (args.Length > 1)
            {
                player.ChatMessage("Неверный синтакс\n" +
                                   "/giveKit [kitname]");
                return;
            }

            if (!Interface.Oxide.DataFileSystem.ExistsDatafile($"AutoKit/{args[0]}"))
            {
                player.ChatMessage("Набора с таким именем не существует!");
                return;
            }

            GiveInit(player, args[0]);
        }

        void GiveInit(BasePlayer player, string filename)
        {
            Inventory dat = Interface.Oxide.DataFileSystem.ReadObject<Inventory>($"AutoKit/{filename}");

            foreach (DatItem t in dat.hotslots)
            {
                Item t1 = ItemManager.CreateByName(t.shortname, t.ammount, t.SkinId);
                t1.position = t.pos;
                foreach (var content in t.content)
                {
                    Item itemContent = ItemManager.CreateByName(content.shortname, content.amount);
                    switch (content.Type)
                    {
                        case TypeContent.Contents:
                        {
                            itemContent.MoveToContainer(t1.contents);
                            break;
                        }
                        case TypeContent.Ammo:
                        {
                            BaseProjectile weap = t1.GetHeldEntity() as BaseProjectile;
                            if (weap != null)
                            {
                                weap.primaryMagazine.contents = itemContent.amount;
                                weap.primaryMagazine.ammoType = ItemManager.FindItemDefinition(content.shortname);
                            }

                            break;
                        }

                    }
                }

                t1.SetParent(player.inventory.containerBelt);
            }


            foreach (DatItem t in dat.wear)
            {
                Item t1 = ItemManager.CreateByName(t.shortname, t.ammount, t.SkinId);
                t1.position = t.pos;
                foreach (var content in t.content)
                {
                    Item itemContent = ItemManager.CreateByName(content.shortname, content.amount);
                    switch (content.Type)
                    {
                        case TypeContent.Contents:
                        {
                            itemContent.MoveToContainer(t1.contents);
                            break;
                        }
                        case TypeContent.Ammo:
                        {
                            BaseProjectile weap = t1.GetHeldEntity() as BaseProjectile;
                            if (weap != null)
                            {
                                weap.primaryMagazine.contents = itemContent.amount;
                                weap.primaryMagazine.ammoType = ItemManager.FindItemDefinition(content.shortname);
                            }

                            break;
                        }

                    }
                }

                t1.SetParent(player.inventory.containerWear);
            }

            foreach (DatItem t in dat.main)
            {
                Item t1 = ItemManager.CreateByName(t.shortname, t.ammount, t.SkinId);
                t1.position = t.pos;
                foreach (var content in t.content)
                {
                    Item itemContent = ItemManager.CreateByName(content.shortname, content.amount);
                    switch (content.Type)
                    {
                        case TypeContent.Contents:
                        {
                            itemContent.MoveToContainer(t1.contents);
                            break;
                        }
                        case TypeContent.Ammo:
                        {
                            BaseProjectile weap = t1.GetHeldEntity() as BaseProjectile;
                            if (weap != null)
                            {
                                weap.primaryMagazine.contents = itemContent.amount;
                                weap.primaryMagazine.ammoType = ItemManager.FindItemDefinition(content.shortname);
                            }

                            break;
                        }

                    }
                }

                t1.SetParent(player.inventory.containerMain);
            }

        }

        object OnPlayerRespawned(BasePlayer player)
        {
            player.inventory.Strip();
            if (Interface.Oxide.DataFileSystem.ExistsDatafile($"AutoKit/Individuals/{player.userID}"))
            {
                Inventory date =
                    Interface.Oxide.DataFileSystem.ReadObject<Inventory>($"AutoKit/Individuals/{player.userID}");
                if (!date.deleted)
                {
                    GiveInit(player, $"Individuals/{player.userID}");
                    return null;
                }
            }

            foreach (Configuration.Kit kit in _config.Kits)
            {
                if (permission.UserHasPermission(player.UserIDString, kit.perm))
                {
                    GiveInit(player, kit.file);
                    return null;
                }
            }

            return null;
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if(Interface.Oxide.DataFileSystem.ExistsDatafile($"AutoKit/Individuals/{player.userID}"))
            {
                Inventory date =
                    Interface.Oxide.DataFileSystem.ReadObject<Inventory>($"AutoKit/Individuals/{player.userID}");
                if (!permission.UserHasPermission(player.UserIDString, date.perm))
                {
                    date.deleted = true;
                    Interface.Oxide.DataFileSystem.WriteObject($"AutoKit/Individuals/{player.userID}", date);
                }
            }
        }
        

        #region IndividualKits

        void AnotherCommandsExec(IPlayer user)
        {
            BasePlayer player = user.Object as BasePlayer;
            UICommand(player);
        }

        [ChatCommand("AutoKit")]
        void UICommand(BasePlayer player)
        {
            string s;
            string c;
            CommunityEntity.ServerInstance.ClientRPCEx(
                new Network.SendInfo {connection = player.net.connection},
                null, "DestroyUI", "AutoKit_main");

            if (!Interface.Oxide.DataFileSystem.ExistsDatafile($"AutoKit/Individuals/{player.userID}"))
            {
                s = "ОТСУТСТВУЕТ";
                c = "1 0.3 0.3 1";
            }
            else
            {
                Inventory date =
                    Interface.Oxide.DataFileSystem.ReadObject<Inventory>($"AutoKit/Individuals/{player.userID}");
                if (date.deleted)
                {
                    s = "ОТСУТСТВУЕТ";
                    c = "1 0.3 0.3 1";
                }
                else
                {
                    s = "СОХРАНЕН";
                    c = "0.3 1 0.3 1";
                }
            }
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo {connection = player.net.connection},
                null, "AddUI", json.Replace("[S]", s).Replace("[C]", c));
        }
        
        [ConsoleCommand("AutoKit_cons")]
        void AutoKItCons(ConsoleSystem.Arg args)
        {
            if(args.Args.Length <= 0) return;
            BasePlayer player = args.Player();
            switch (args.Args[0])
            {
                case "close":
                {
                    CommunityEntity.ServerInstance.ClientRPCEx(
                        new Network.SendInfo {connection = player.net.connection},
                        null, "DestroyUI", "AutoKit_main");
                    break;
                }
                case "save":
                {
                    SetKit(player);
                    CommunityEntity.ServerInstance.ClientRPCEx(
                        new Network.SendInfo {connection = player.net.connection},
                        null, "DestroyUI", "AutoKit_main");
                    UICommand(player);
                    break;
                }
                case "remove":
                {
                    DelKit(player);
                    CommunityEntity.ServerInstance.ClientRPCEx(
                        new Network.SendInfo {connection = player.net.connection},
                        null, "DestroyUI", "AutoKit_main");
                    UICommand(player);
                    break;
                }
            }
        }
    

    [ChatCommand("delkit")]
        void DelKit(BasePlayer player)
        {
            if (!Interface.Oxide.DataFileSystem.ExistsDatafile($"AutoKit/Individuals/{player.userID}"))
            {
                
                player.ChatMessage("Вы не можете удалить еще не созданную раскладку!");
            }
            else
            {
                Inventory date =
                    Interface.Oxide.DataFileSystem.ReadObject<Inventory>($"AutoKit/Individuals/{player.userID}");
                if (date.deleted)
                {
                    player.ChatMessage("Раскладка уже удалена!");
                    return;
                }

                date.deleted = true;
                Interface.Oxide.DataFileSystem.WriteObject($"AutoKit/Individuals/{player.userID}", date);
                player.ChatMessage("Ваш автокит удален!");
            }
        }
        [ChatCommand("setkit")]
        void SetKit(BasePlayer player)
        {
            foreach (Configuration.Kit kit in _config.Kits)
            {
                if (permission.UserHasPermission(player.UserIDString, kit.perm))
                {
                    if(_config.DeleteOnVipe) SaveIDs(player.userID);
                    SetIndividual(player, kit.file, kit.perm);
                    return;
                }
            }
        }

        void SaveIDs(ulong id)
        {
            List<ulong> ids = Interface.Oxide.DataFileSystem.ReadObject<List<ulong>>("AutoKit/IDS");
            if (!ids.Contains(id))
            {
                ids.Add(id);
                Interface.Oxide.DataFileSystem.WriteObject<List<ulong>>("AutoKit/IDS", ids);
            }
        }
        void SetIndividual(BasePlayer player, string file, string perm)
        {
            List<DatItem> hs = new List<DatItem>();
            List<DatItem> wr = new List<DatItem>();
            List<DatItem> mn = new List<DatItem>();
            Inventory dat = Interface.Oxide.DataFileSystem.ReadObject<Inventory>($"AutoKit/{file}");
            
            Dictionary<string, DatItem> allitems = new Dictionary<string, DatItem>();
            
            
            foreach (DatItem t in dat.hotslots) 
            {
                int i = 0;
                if (allitems.ContainsKey(t.shortname))
                {
                    allitems.Add(t.shortname + i, t);
                    i++;
                } else allitems.Add(t.shortname, t);
                
            }
            foreach (DatItem t in dat.wear)
            {
                int i = 0;
                if (allitems.ContainsKey(t.shortname))
                {
                    allitems.Add(t.shortname + i, t);
                    i++;
                }
                else allitems.Add(t.shortname, t);
            }
            foreach (DatItem t in dat.main)
            {
                int i = 0;
                if (allitems.ContainsKey(t.shortname))
                {
                    allitems.Add(t.shortname + i, t);
                    i++;
                }
                else allitems.Add(t.shortname, t);
            }
            
            
            foreach (Item t1 in player.inventory.containerBelt.itemList)
            {
                if (allitems.ContainsKey(t1.info.shortname))
                {
                    hs.Add(new DatItem
                        {
                            shortname = t1.info.shortname,
                            ammount = allitems[t1.info.shortname].ammount,
                            content = getItemContent(t1),
                            SkinId = allitems[t1.info.shortname].SkinId,
                            pos = t1.position
                        });
                    allitems.Remove(t1.info.shortname);
                }
                else
                {
                    for (int i1 = 0; i1 < 10; i1++)
                    {
                        if (allitems.ContainsKey(t1.info.shortname + i1))
                        {
                            hs.Add(new DatItem
                            {
                                shortname = t1.info.shortname,
                                ammount = allitems[t1.info.shortname + i1].ammount,
                                content = getItemContent(t1),
                                SkinId = allitems[t1.info.shortname + i1].SkinId,
                                pos = t1.position
                            });
                            if (!allitems.Remove(t1.info.shortname + i1)) return;
                            
                        }
                            
                    }
                }
            }
            foreach (Item t1 in player.inventory.containerWear.itemList)
            {
                if (allitems.ContainsKey(t1.info.shortname))
                {
                    wr.Add(new DatItem
                        {
                            shortname = t1.info.shortname,
                            ammount = allitems[t1.info.shortname].ammount,
                            content = getItemContent(t1),
                            SkinId = allitems[t1.info.shortname].SkinId,
                            pos = t1.position
                        });
                    allitems.Remove(t1.info.shortname);
                }
                else
                {
                    for (int i1 = 0; i1 < 10; i1++)
                    {
                        if (allitems.ContainsKey(t1.info.shortname + i1))
                        {
                            wr.Add(new DatItem
                            {
                                shortname = t1.info.shortname,
                                ammount = allitems[t1.info.shortname + i1].ammount,
                                content = getItemContent(t1),
                                SkinId = allitems[t1.info.shortname + i1].SkinId,
                                pos = t1.position
                            });
                            if (!allitems.Remove(t1.info.shortname + i1)) return;
                        }
                    }
                }
            }
            foreach (Item t1 in player.inventory.containerMain.itemList)
            {
                if (allitems.ContainsKey(t1.info.shortname))
                {
                    mn.Add(new DatItem
                        {
                            shortname = t1.info.shortname,
                            ammount = allitems[t1.info.shortname].ammount,
                            content = getItemContent(t1),
                            SkinId = allitems[t1.info.shortname].SkinId,
                            pos = t1.position
                        });
                    allitems.Remove(t1.info.shortname);
                }
                else
                {
                    for (int i1 = 0; i1 < 10; i1++)
                    {
                        if (allitems.ContainsKey(t1.info.shortname + i1))
                        {
                            mn.Add(new DatItem
                            {
                                shortname = t1.info.shortname,
                                ammount = allitems[t1.info.shortname + i1].ammount,
                                content = getItemContent(t1),
                                SkinId = allitems[t1.info.shortname + i1].SkinId,
                                pos = t1.position
                            });
                            if (!allitems.Remove(t1.info.shortname + i1)) return;
                            
                        }
                            
                    }
                }
            }

            Inventory individual = new Inventory();
            individual.hotslots = hs;
            individual.wear = wr;
            individual.main = mn;
            individual.perm = perm;
            Interface.Oxide.DataFileSystem.WriteObject<Inventory>($"AutoKit/Individuals/{player.userID}", individual);
            player.ChatMessage("Раскладка успешно создана!");
        }
        
        
        #endregion
        void OnServerVipes()
        {
            if(!_config.DeleteOnVipe) return;
            List<ulong> ids = Interface.Oxide.DataFileSystem.ReadObject<List<ulong>>("AutoKit/IDS");
            foreach (ulong id in ids)
            {
                Inventory dat = Interface.Oxide.DataFileSystem.ReadObject<Inventory>($"AutoKit/Individuals/{id}");
                dat.deleted = true;
                Interface.Oxide.DataFileSystem.WriteObject<Inventory>($"AutoKit/Individuals/{id}", dat);
            }
            PrintWarning("Индивидуальные настройки китов удалены!");
        }
        #region ConsoleCommands

        [ConsoleCommand("deleteAutokit")]
        void DeletePlayerAutoKit(ConsoleSystem.Arg args)
        {
            var player = args.Player();
            if (player.IsAdmin || player == null)
            {
                ulong id = ulong.Parse(args.Args[0]);
                if (!Interface.Oxide.DataFileSystem.ExistsDatafile($"AutoKit/Individuals/{id}"))
                {

                    player.ChatMessage($"Игрок {id} еще не создал раскладку!");
                }
                else
                {
                    Inventory date =
                        Interface.Oxide.DataFileSystem.ReadObject<Inventory>($"AutoKit/Individuals/{id}");
                    if (date.deleted)
                    {
                        player.ChatMessage($"У игрока {id} уже удалена раскладка!");
                        return;
                    }

                    date.deleted = true;
                    Interface.Oxide.DataFileSystem.WriteObject($"AutoKit/Individuals/{id}", date);
                    player.ChatMessage($"Вы удалили раскладку игрока {id}");
                }
            }
        }

        #endregion
        
    }
    
}