// Version: 1.2.65
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries;
using UnityEngine;
using Oxide.Game.Rust.Cui;
using Network;
using VLB;

namespace Oxide.Plugins
{
    [Info("Shop", "David", "1.2.65")]
    public class Shop : RustPlugin
    {   
        [PluginReference]
        private Plugin WelcomePanel, ImageLibrary, Economics, ServerRewards, NoEscape;

        static Shop plugin;

        #region ItemTake Replacement

        private bool TakeFromInventory(BasePlayer player, string shortname, int amount, ulong skinid = 0)
        {
            if (player == null)
                return false;

            var item = ItemManager.FindItemDefinition(shortname);

            if (item == null)
            {
                SendReply(player, "Plugin configuration error, please let server admin know about it.");
                Puts($"{shortname} is not valid shortname");
                return false;
            }

            if (GetAmount(player, shortname, skinid) < amount)
                return false;

            if (RemoveItem(player, amount, shortname, skinid))
                return true;

            return false;
        }

        private int GetAmount(BasePlayer player, string shortname, ulong? skin)
        {
            var items = Facepunch.Pool.GetList<Item>();

            items.AddRange(player.inventory.containerBelt.itemList
                .Where(item => item.skin == skin && item.info.shortname == shortname));
            items.AddRange(player.inventory.containerMain.itemList
                .Where(item => item.skin == skin && item.info.shortname == shortname));

            var currentAmount = items.Sum(x => x.amount);

            Facepunch.Pool.FreeList(ref items);
            return currentAmount;
        }

        private bool RemoveItem(BasePlayer player, int amount, string shortname, ulong? skin)
        {
            if (amount == 0) return false;

            var items = Facepunch.Pool.GetList<Item>();
            try
            {

                items.AddRange(player.inventory.containerBelt.itemList
                    .Where(item => item.skin == skin && item.info.shortname == shortname));

                items.AddRange(player.inventory.containerMain.itemList
                    .Where(item => item.skin == skin && item.info.shortname == shortname));

                var amountLeft = amount;
                foreach (var item in items)
                {
                    if (amountLeft <= 0) break;

                    item.MarkDirty();
                    var oldItemAmount = item.amount;

                    if (amountLeft >= item.amount)
                    {
                        item.MarkDirty();
                        item.RemoveFromContainer();
                    }
                    else item.amount -= amountLeft;

                    amountLeft -= oldItemAmount;
                }
            }
            catch
            {
                SendReply(player, "Something failed while taking items, please let server admin know about it.");
                Puts($"Something failed while taking items from {player} -> (shortname:{shortname}/amount:{amount}/skin:{skin})");
                return false;
            }

            Facepunch.Pool.FreeList(ref items);
            return true;
        }

        #endregion

        #region freshspawn block

        void OnPlayerRespawned(BasePlayer player)
        {   
            if (config.ms.freshSpawnBlock == 0) return;

            if (!spawnedAt.ContainsKey(player))
                spawnedAt.Add(player, DateTimeOffset.Now.ToUnixTimeSeconds());
            else
                spawnedAt[player] = DateTimeOffset.Now.ToUnixTimeSeconds();
        }

        private Dictionary<BasePlayer, long> spawnedAt = new Dictionary<BasePlayer, long>();

        bool canUseShop(BasePlayer player)
        {
            if (config.ms.freshSpawnBlock != 0)
            {
                if (spawnedAt.ContainsKey(player))
                {
                    if ((int) config.ms.freshSpawnBlock - (int) (DateTimeOffset.Now.ToUnixTimeSeconds() - spawnedAt[player]) > 0)
                    {   
                        cantUseShopOverlay(player);
                        return false;
                    }
                    
                }
                else
                {
                    return true;
                }
            }
            return true;
        }

        [ChatCommand("tb")]
        void cantUseShopOverlay(BasePlayer player)
        {   
            CuiHelper.DestroyUi(player, "block_container");

            int timeleft = (int) config.ms.freshSpawnBlock - (int) (DateTimeOffset.Now.ToUnixTimeSeconds() - spawnedAt[player]);
            
            var blockOverlay = new CuiElementContainer();
            CUIClass.CreatePanel(ref blockOverlay, "block_container", "Overlay", "0 0 0 0.9", "0 0", "1 1", true, 0.3f, 0f, "assets/content/ui/uibackgroundblur.mat");
            blockOverlay.Add(new CuiElement
            {
                Parent = "block_container",
                Name = "block_container_text",
                Components = {
                    new CuiTextComponent{ Text = "<size=21><b>You can't use shop right after respawning.</b></size>\n\nPlease wait %TIME_LEFT% seconds.", FontSize = 17, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1", Font = "robotocondensed-regular.ttf",},
                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1" },
                    new CuiCountdownComponent { EndTime = 0, StartTime = timeleft, Command = "close_block_container"}
                },
                FadeOut = 0f
            });
            CUIClass.CreateButton(ref blockOverlay, "gotit_button", "block_container", "0.506 0.671 0.149 0.87", "Got it!", 15, "0.47 0.35", $"0.53 0.4", "close_block_container", "", "1 1 1 0.8", 0.5f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf", "assets/icons/iconmaterial.mat");


            CuiHelper.AddUi(player, blockOverlay);
        }

        [ConsoleCommand("close_block_container")]
        private void close_block_container(ConsoleSystem.Arg arg)
        {
            CuiHelper.DestroyUi(arg?.Player(), "block_container");
        }

        #endregion

        #region [Hooks]

        private void Init() => plugin = this;

        private void OnServerInitialized()
        {
            //data
            LoadCategoryData();
            LoadItemData();
            LoadCommandData();
            LoadHumanNPCData();
            LoadWipeBlockData();
            LoadPlayerData();
            LoadCdData();
            //permissions
            RegisterPerms();
            //commands
            RegisterCustomCmds();
            //other
            SelectLayout();
            DownloadImages();

            if (!config.us.buttons.ContainsKey("Category Buttons"))
            { 
                config.us.buttons.Add("Category Buttons", "0.115 0.115 0.115 0.9");
                SaveConfig();
            }

            //store existing categories
            foreach (var category in categories.Keys)
            {
                storedCategories.Add(category);
                CategoryNamesCheck(category);
            }

            if (config.gl.gradually)
                timer.Once(0.5f, () => { PlayerComponent(true); });
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (config.gl.gradually)
                PlayerComponent(true, player);

            PreloadImages(player);
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            if (config.gl.gradually)
                PlayerComponent(false, player);
        }

        private void Unload()
        {
            if (config.gl.gradually)
                PlayerComponent(false);

            foreach (var player in BasePlayer.activePlayerList)
                DestroyShop(player);

            SavePlayerData();
        }

        private void OnNewSave(string filename)
        {   
            LoadWipeBlockData();

            if (!wipeBlock["Wipe Block"].StartOnMapWipe) return;

            wipeBlock["Wipe Block"].WipeTimeStamp = DateTimeOffset.Now.ToUnixTimeSeconds();
            SaveWipeBlockData();
        }

        #endregion

        #region [Methods/Functions]

        private readonly Dictionary<string, string> headers = new Dictionary<string, string>
        {
            ["Content-Type"] = "application/json"
        };

        private class TextField
        {
            public string name;
            public string value;
            public bool inline;
        };

        private void SendDiscordMessage(BasePlayer player, string item, int amount, int price, bool buy = true, string icon = "https://swiki.rustclash.com//img/items180/cratecostume.png")
        {    
            if (string.IsNullOrEmpty(config.ms.webhook)) return;

            string msgContent = $"Bought {amount}x {item} for price of {price}";
            long colorE = 3066993;

            if (!buy)
            {   
                msgContent = $"Sold {amount}x {item} for price of {price}";
                colorE = 3447003;
            }

            #region JsonObject

            TextField[] fields = new[]  { new TextField { name="▪️", value="▪️", inline=false }};

            Dictionary<string, string> footer = new Dictionary<string, string>()
            {
                {"text", msgContent},
                {"icon_url", icon},
            };  

            Dictionary<string, string> image = new Dictionary<string, string>()
            {
                {"url", "https://i.ibb.co/939hNM0/rustlogo.jpg"},
            }; 
           
            string json = JsonConvert.SerializeObject(new
            {
                content = "",
                embeds = new[]
                {
                    new
                    {
                        description = $"**{player.displayName}**[*{player.userID}*]",
                        color = colorE,
                        footer
                    }
                }

            });
        #endregion
        
            webrequest.Enqueue(config.ms.webhook, json, (code, response) =>          
            {
                if (code == 204 || response == null)
                {
                    //    Puts($"Webhook message failed");
                }
                
            }, this, RequestMethod.POST, headers );
        }

        private void RegisterPerms()
        {
            //register main perm
            if (config.ms.requirePerm)
                permission.RegisterPermission($"{Name}.use", this);
            //register perms for sales
            foreach (string perm in config.gs.perms.Keys)
                permission.RegisterPermission($"{perm}", this);
            //register category permission
            foreach (string category in categories.Keys)
            {
                if (categories[category].Permission != null)
                    permission.RegisterPermission($"{Name}.{categories[category].Permission}", this);
            }
        }

        private bool TakeFrom(BasePlayer player, string currencyType, int amount, ulong skin = 0)
        {
            if (currencyType.ToLower() != "rp" && currencyType.ToLower() != "eco")
            {   
                if (currencyType.Contains("{"))
                {   
                    string[] item_ = currencyType.Split('{');
                    currencyType = item_[0];
                }

                //correct for flamethrower
                if (currencyType == "military_flamethrower")
                    currencyType = "military flamethrower";

                if (config.ms.checkSkin)
                { 
                    return TakeFromInventory(player, currencyType, amount, skin);
                }
                else
                {   

                    var itemDef = ItemManager.FindItemDefinition(currencyType);
                    if (itemDef == null)
                    { 
                        Puts($" <color=#C2291D>!</color> '{currencyType}' <color=#C2291D>is not correct shortname.</color>"); 
                        return false; 
                    }

                    int inventory = player.inventory.GetAmount(itemDef.itemid);
                    if (inventory < amount)
                    {
                        return false;
                    }

                    player.inventory.Take(null, itemDef.itemid, amount);
                    return true;
                }
            }

            if (currencyType == "rp")
            {
                if (ServerRewards == null)
                { Puts("ServerRewards is not loaded!"); return false; }

                var playersRP = ServerRewards?.Call<int>("CheckPoints", player.userID);

                if (playersRP < amount) return false;

                ServerRewards?.Call("TakePoints", player.userID, amount);
                CurrencyBtns(player);
                return true;
            }

            if (currencyType == "eco")
            {
                if (Economics == null)
                { Puts("Economics is not loaded!"); return false; }

                double playersEco = Economics.Call<double>("Balance", player.UserIDString);

                if (playersEco < Convert.ToDouble(amount))
                {
                    return false;
                }
                Economics.CallHook("Withdraw", player.UserIDString, Convert.ToDouble(amount));
                CurrencyBtns(player);
                return true;
            }
            return false;

        }

        private bool GiveTo(BasePlayer player, string itemType, int amount, string name = "default", ulong skinId = 0)
        {
            if (itemType.ToLower() != "rp" && itemType.ToLower() != "eco")
            {
                if (itemType.Contains("{"))
                {
                    string[] shortnameSplit = itemType.Split('{');
                    itemType = shortnameSplit[0];
                }

                if (itemType == "military_flamethrower")
                    itemType = "military flamethrower";

                var item = ItemManager.CreateByName(itemType, amount, skinId);
                if (item != null)
                {
                    if (name != "default")
                        item.name = name;

                    player.GiveItem(item);
                    return true;

                }
            }

            if (itemType == "rp")
            {
                if (ServerRewards == null)
                { Puts("ServerRewards is not loaded!"); return false; }

                ServerRewards?.Call("AddPoints", player.userID, amount);
                //refresh currency text
                CurrencyBtns(player);
                return true;
            }

            if (itemType == "eco")
            {
                if (Economics == null)
                { Puts("Economics is not loaded!"); return false; }

                Economics.CallHook("Deposit", player.UserIDString, (double)amount);
                CurrencyBtns(player);
                return true;
            }

            return false;
        }

        void CreateCooldownEntry(BasePlayer player, string item)
        {
            if (config.ms.cooldowns)
            {
                if (cooldownData.ContainsKey(item))
                {
                    if (!playerData.ContainsKey(player.userID))
                        playerData.Add(player.userID, new PlayerData());

                    if (playerData[player.userID].cooldowns.ContainsKey(item))
                        playerData[player.userID].cooldowns[item] = DateTimeOffset.Now.ToUnixTimeSeconds();
                    else
                        playerData[player.userID].cooldowns.Add(item, DateTimeOffset.Now.ToUnixTimeSeconds());
                }
            }
        }

        private bool BuildChecks(BasePlayer player)
        {
            //is building blocked?
            if (!player.CanBuild() && config.ms.requireBuild)
            {
                if (config.ns.cuiFx)
                    _RunNotif(player, 0.45f, 1);

                if (config.ns.notifs)
                {
                    //create message if missing
                    if (!config.ns.msgs.ContainsKey("buildingBlocked"))
                    {
                        config.ns.msgs.Add("buildingBlocked", "<size=13><b>BUILDING BLOCKED</b></size>\n\nYou can't use shop while you are building blocked.");
                        SaveConfig();
                    }
                    _RunNotif(player, config.ns.duration, 2, new string[] { config.ns.fail["Panel Color"], config.ns.fail["Accent Color"], config.ns.msgs["buildingBlocked"], config.ns.fail["Sound Prefab"] });
                }

                return false;
            }

            //is building authed?
            if (!player.IsBuildingAuthed() && config.ms.requireTC)
            {
                if (config.ns.cuiFx)
                    _RunNotif(player, 0.45f, 1);

                if (config.ns.notifs)
                {
                    //create message if missing
                    if (!config.ns.msgs.ContainsKey("notTCAuthed"))
                    {
                        config.ns.msgs.Add("notTCAuthed", "<size=13><b>NO BUILDING PRIV.</b></size>\n\nYou need to be TC authorized in order to use shop.");
                        SaveConfig();
                    }
                    _RunNotif(player, config.ns.duration, 2, new string[] { config.ns.fail["Panel Color"], config.ns.fail["Accent Color"], config.ns.msgs["notTCAuthed"], config.ns.fail["Sound Prefab"] });
                }

                return false;
            }

            return true;
        }

        private void Buy(BasePlayer player, string category, string itemName, int amount)
        {
            if (!BuildChecks(player)) return;

            //combat block check
            if (InCombat(player))
            {
                if (config.ns.cuiFx)
                    _RunNotif(player, 0.45f, 1);

                if (config.ns.notifs)
                    _RunNotif(player, config.ns.duration, 2, new string[] { config.ns.fail["Panel Color"], config.ns.fail["Accent Color"], config.ns.msgs["noEscape"], config.ns.fail["Sound Prefab"] });

                return;
            }

            //category permission check
            if (categories[category].Permission != null)
            {
                if (!permission.UserHasPermission(player.UserIDString, $"shop.{categories[category].Permission}"))
                {
                    if (config.ns.cuiFx)
                        _RunNotif(player, 0.45f, 1);

                    if (config.ns.notifs)
                        _RunNotif(player, config.ns.duration, 2, new string[] { config.ns.fail["Panel Color"], config.ns.fail["Accent Color"], config.ns.msgs["noPermission"], config.ns.fail["Sound Prefab"] });

                    return;
                }
            }
            //if item is cooldown limited
            if (config.ms.cooldowns)
            {
                if (cooldownData.ContainsKey(itemName))
                {
                    //can buy only one
                    if (amount > 1)
                    {
                        _RunNotif(player, config.ns.duration, 2, new string[] { config.ns.fail["Panel Color"], config.ns.fail["Accent Color"], "This item is cooldown restricted, you can buy only one.", config.ns.fail["Sound Prefab"] });
                        return;
                    }
                    // is player on cooldown
                    if (playerData.ContainsKey(player.userID))
                    {
                        if (playerData[player.userID].cooldowns.ContainsKey(itemName))
                        {
                            long timeNow = DateTimeOffset.Now.ToUnixTimeSeconds();
                            long timeDif = timeNow - playerData[player.userID].cooldowns[itemName];
                            if (timeDif < cooldownData[itemName])
                            {
                                _RunNotif(player, config.ns.duration, 2, new string[] { config.ns.fail["Panel Color"], config.ns.fail["Accent Color"], "You are on cooldown, please wait.", config.ns.fail["Sound Prefab"] });
                                return;
                            }


                        }
                    }
                }
            }

            if (items[itemName].BlockAmountChange && items[itemName].DefaultAmount != amount)
            {
                if (config.ns.notifs)
                    _RunNotif(player, config.ns.duration, 2, new string[] { config.ns.fail["Panel Color"], config.ns.fail["Accent Color"], $"Error", config.ns.fail["Sound Prefab"] });

                return;
            }

            //check sales
            int price = PriceAfterSales(player, category, itemName);

            if (itemName.StartsWith("cmd") || itemName.StartsWith("command"))
            {
                //skip
            }
            else
                price = (int)Math.Ceiling((double)PriceAfterSales(player, category, itemName) * ((double)amount / (double)items[itemName].DefaultAmount));

            //price = price * amount; 
            //take currency
            if (TakeFrom(player, items[itemName].Currency, price))
            {
                CreateCooldownEntry(player, itemName);
                //if success   
                GiveTo(player, itemName, amount, items[itemName].DisplayName, items[itemName].Skin);

                string image = items[itemName].Image;
                if (!image.StartsWith("http"))
                    image = "https://wiki.rustclash.com/img/items180/" + image;
                    
                SendDiscordMessage(player, itemName, amount, price, true, image);

                if (config.ns.cuiFx)
                    _RunNotif(player, 0.45f, 0);

                if (config.ns.notifs)
                    _RunNotif(player, config.ns.duration, 2, new string[] { config.ns.suc["Panel Color"], config.ns.suc["Accent Color"], $"{ReplaceNT(config.ns.msgs["onPurchase"], itemName, price, amount)}", config.ns.suc["Sound Prefab"] });

            }
            else
            {
                //if failed   
                if (config.ns.cuiFx)
                    _RunNotif(player, 0.45f, 1);

                if (config.ns.notifs)
                    _RunNotif(player, config.ns.duration, 2, new string[] { config.ns.fail["Panel Color"], config.ns.fail["Accent Color"], $"{ReplaceNT(config.ns.msgs["noFunds"], itemName, price, amount)}", config.ns.fail["Sound Prefab"] });
            }

        }

        private void Sell(BasePlayer player, string category, string itemName, int amount)
        {
            if (!BuildChecks(player)) return;

            //combat block check
            if (InCombat(player))
            {
                if (config.ns.cuiFx)
                    _RunNotif(player, 0.45f, 1);

                if (config.ns.notifs)
                    _RunNotif(player, config.ns.duration, 2, new string[] { config.ns.fail["Panel Color"], config.ns.fail["Accent Color"], config.ns.msgs["noEscape"], config.ns.fail["Sound Prefab"] });

                return;
            }


            //category permission check
            if (categories[category].Permission != null)
            {
                if (!permission.UserHasPermission(player.UserIDString, $"shop.{categories[category].Permission}"))
                {
                    if (config.ns.cuiFx)
                        _RunNotif(player, 0.45f, 1);

                    if (config.ns.notifs)
                        _RunNotif(player, config.ns.duration, 2, new string[] { config.ns.fail["Panel Color"], config.ns.fail["Accent Color"], config.ns.msgs["noPermission"], config.ns.fail["Sound Prefab"] });

                    return;
                }
            }

            //check sales
            int price = PriceAfterSales(player, category, itemName, true);

            if (itemName.StartsWith("cmd") || itemName.StartsWith("command"))
            {
                //skip
            }
            else
                price = (int)((double)PriceAfterSales(player, category, itemName, true) * ((double)amount / (double)items[itemName].DefaultAmount));

            if (price == 0) return;

            //take currency
            if (TakeFrom(player, itemName, amount, items[itemName].Skin))
            {
                GiveTo(player, items[itemName].Currency, price);

                string image = items[itemName].Image;
                if (!image.StartsWith("http"))
                    image = "https://wiki.rustclash.com/img/items180/" + image;
                    
                SendDiscordMessage(player, itemName, amount, price, false, image);

                if (config.ns.cuiFx)
                    _RunNotif(player, 0.45f, 0);

                if (config.ns.notifs)
                    _RunNotif(player, config.ns.duration, 2, new string[] { config.ns.suc["Panel Color"], config.ns.suc["Accent Color"], $"{ReplaceNT(config.ns.msgs["onSale"], itemName, price, amount)}", config.ns.suc["Sound Prefab"] });
            }
            else
            {
                if (config.ns.cuiFx)
                    _RunNotif(player, 0.45f, 1);

                if (config.ns.notifs)
                    _RunNotif(player, config.ns.duration, 2, new string[] { config.ns.fail["Panel Color"], config.ns.fail["Accent Color"], $"{ReplaceNT(config.ns.msgs["noItemToSell"], itemName, price, amount)}", config.ns.fail["Sound Prefab"] });

            }
        }

        private void BuyCommand(BasePlayer player, string category, string command)
        {
            //category permission check
            if (categories[category].Permission != null)
            {
                if (!permission.UserHasPermission(player.UserIDString, $"shop.{categories[category].Permission}"))
                {
                    if (config.ns.cuiFx)
                        _RunNotif(player, 0.45f, 1);

                    if (config.ns.notifs)
                        _RunNotif(player, config.ns.duration, 2, new string[] { config.ns.fail["Panel Color"], config.ns.fail["Accent Color"], config.ns.msgs["noPermission"], config.ns.fail["Sound Prefab"] });

                    return;
                }
            }

            if (!BuildChecks(player)) return;

            if (InCombat(player))
            {
                if (config.ns.cuiFx)
                    _RunNotif(player, 0.45f, 1);

                if (config.ns.notifs)
                    _RunNotif(player, config.ns.duration, 2, new string[] { config.ns.fail["Panel Color"], config.ns.fail["Accent Color"], config.ns.msgs["noEscape"], config.ns.fail["Sound Prefab"] });

                return;
            }

            string[] splits = command.Split('/');
            string cmd = commands[splits[1]].Command;

            //if item is cooldown limited
            if (config.ms.cooldowns)
            {
                if (cooldownData.ContainsKey(command))
                {
                    // is player on cooldown
                    if (playerData.ContainsKey(player.userID))
                    {
                        if (playerData[player.userID].cooldowns.ContainsKey(command))
                        {
                            long timeNow = DateTimeOffset.Now.ToUnixTimeSeconds();
                            long timeDif = timeNow - playerData[player.userID].cooldowns[command];
                            if (timeDif < cooldownData[command])
                            {
                                _RunNotif(player, config.ns.duration, 2, new string[] { config.ns.fail["Panel Color"], config.ns.fail["Accent Color"], "You are on cooldown, please wait.", config.ns.fail["Sound Prefab"] });
                                return;
                            }


                        }
                    }
                }
            }

            int price = PriceAfterSales(player, category, command);

            if (TakeFrom(player, commands[splits[1]].Currency, price))
            {
                CreateCooldownEntry(player, command);

                SendDiscordMessage(player, $"{command}", 1, price);

                if (cmd.Contains("|"))
                {
                    string[] cmdSplits = cmd.Split('|');
                    foreach (string c in cmdSplits)
                    {
                        Server.Command(c.Replace("{steamid}", $"{player.userID}").Replace("{playername}", $"{player.displayName}"));
                    }
                }
                else
                {
                    Server.Command(cmd.Replace("{steamid}", $"{player.userID}").Replace("{playername}", $"{player.displayName}"));
                }

                SendReply(player, commands[splits[1]].Message);

                if (config.ns.cuiFx)
                    _RunNotif(player, 0.45f, 0);

                if (config.ns.notifs)
                    _RunNotif(player, config.ns.duration, 2, new string[] { config.ns.suc["Panel Color"], config.ns.suc["Accent Color"], $"{ReplaceNT(config.ns.msgs["onPurchase"], commands[splits[1]].DisplayName, price, 1, true)}", config.ns.suc["Sound Prefab"] });
            }
            else
            {
                //if failed   
                if (config.ns.cuiFx)
                    _RunNotif(player, 0.45f, 1);

                if (config.ns.notifs)
                    _RunNotif(player, config.ns.duration, 2, new string[] { config.ns.fail["Panel Color"], config.ns.fail["Accent Color"], $"{ReplaceNT(config.ns.msgs["noFunds"], commands[splits[1]].DisplayName, price, 1, true)}", config.ns.fail["Sound Prefab"] });
            }

        }

        private int PriceAfterSales(BasePlayer player, string category, string itemName, bool sell = false, bool fullprice = false)
        {
            int price = 0;

            if (itemName.Contains("cmd/") || itemName.Contains("command/"))
            {
                string[] splits = itemName.Split('/');
                price = commands[splits[1]].BuyPrice;
            }
            else
            {
                price = items[itemName].BuyPrice;
            }

            if (fullprice) return price;

            if (sell) price = items[itemName].SellPrice;

            List<float> availableSales = new List<float> { };

            //check if category has sale
            if (categories[category].Sale != null && categories[category].Sale != 0) availableSales.Add(categories[category].Sale);
            //check global sale for item

            if (config.gs.items.ContainsKey(itemName)) availableSales.Add(config.gs.items[itemName]);

            //check player permission sales
            foreach (string perm in config.gs.perms.Keys)
            {   
                if (permission.UserHasPermission(player.UserIDString, perm))
                {   
                    if (!availableSales.Contains(config.gs.perms[perm]))
                       availableSales.Add(config.gs.perms[perm]);
                }
            }

            
            float discount = 0;
            int dec = 0;

            if (availableSales.Count != 0)
                discount = availableSales.Max();

            if (discount != 0 && discount > 0)
                dec = Convert.ToInt32((Convert.ToSingle(price) / 100f) * (discount * 100f));

            return price - dec;
        }

        private string GetCurrencyType(BasePlayer player, string itemName)
        {
            string currency = "";
            if (itemName.Contains("cmd/") || itemName.Contains("command/"))
            {
                string[] splits = itemName.Split('/');
                currency = commands[splits[1]].Currency;
            }
            else
            {
                currency = items[itemName].Currency;
            }

            return currency;
        }

        private long IsWipeBlocked(string item)
        {   
            if (!wipeBlock["Wipe Block"].Enabled)
                return 0;

            if (wipeBlock["Wipe Block"].WipeTimeStamp == 0 && wipeBlock["Wipe Block"].WipeTimeStamp == null)
                return 0;

            if (!wipeBlock["Wipe Block"].Items.ContainsKey(item))
                return 0;

            long itemTimeBlock = wipeBlock["Wipe Block"].Items[item];
            long timeNow = DateTimeOffset.Now.ToUnixTimeSeconds();
            long timePassed = timeNow - wipeBlock["Wipe Block"].WipeTimeStamp;

            if (timePassed < itemTimeBlock)
                return itemTimeBlock - timePassed;
            else
                return 0;
        }

        private bool InCombat(BasePlayer player)
        {
            if (NoEscape == null) return false;

            if (config.cb.combatBlock && (bool)NoEscape.Call("IsCombatBlocked", player))
                return true;

            if (config.cb.raidBlock && (bool)NoEscape.Call("IsRaidBlocked", player))
                return true;

            return false;
        }

        string ReplaceNT(string msg, string item, int price, int amount, bool command = false)
        {
            if (command)
            {
                msg = msg.Replace("{item}", item).Replace("{price}", $"{price}").Replace("{amount}", $"{amount}");
                return msg;
            }

            if (!item.StartsWith("cmd/") && !item.StartsWith("command/"))
            {
                if (items[item].DisplayName == "default")
                {
                    var itemDef = ItemManager.FindItemDefinition(item);
                    if (itemDef != null)
                        item = itemDef.displayName.translated;
                }
                else
                    item = items[item].DisplayName;
            }

            msg = msg.Replace("{item}", item).Replace("{price}", $"{price}").Replace("{amount}", $"{amount}");
            return msg;

        }

        bool IsDigitsOnly(string str)
        {
            foreach (char c in str)
            {
                if (c < '0' || c > '9')
                    return false;
            }
            return true;
        }

        #endregion

        #region [UI Functions]

        #region     Commands

        private void RegisterCustomCmds()
        {
            cmd.AddChatCommand(config.ms.cmd, this, "CustomShopCommands");
        }

        void CustomShopCommands(BasePlayer player, string command, string[] args)
        {
            if (!readyCheck(player)) return;

            if (!config._as.addon)
            {
                if (command == config.ms.cmd)
                {   
                    if (!canUseShop(player))
                        return;

                    try
                    {   
                        CuiHelper.DestroyUi(player, "bg");
                        CuiHelper.DestroyUi(player, "shopD_containeOverlayr");
                        CuiHelper.DestroyUi(player, "shopD_container");
                        CuiHelper.DestroyUi(player, "empty");
                        CuiHelper.DestroyUi(player, "container");
                        CuiHelper.DestroyUi(player, "close_btn");

                        //default category
                        var list = ItemOrder(player, storedCategories[0], 18, 0);
                        CreateDefaultCui(player);
                        CreateCui(player);
                        CategorySelection(player);
                        OpenShopPage(player, storedCategories[0], 0);
                        ButtonHL(player, 0);
                    }
                    catch
                    {
                        Puts($"\nError found in configuration files.\nPlease make sure that:\n\n• You didn't break json formating. Online Json validator can be used to check it.\n• You not missing any file in your data/Shop folder\n• Check if Item.json and Categories.json are not empty.\n\n\n If you still having issues, feel free to open support ticket and I will assist you soon as I can. (within 16 hours usually)");
                        SendReply(player, "<color=#E74F17><size=18>Error found in configuration files.</size></color>\nPlease make sure that:\n\n• You didn't break json formating. Online Json validator can be used to check it.\n• You not missing any file in your data/Shop folder\n• Check if Item.json and Categories.json are not empty.\n\n\n If you still having issues, feel free to open support ticket and I will assist you soon as I can. (within 16 hours usually)");
                    }

                }
            }
            else
            {
                if (player.IsAdmin)
                    SendReply(player, "Shop is set to be used as addon for WelcomePanel.");
            }
        }

        [ConsoleCommand("shopclose")]
        private void CloseShop(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            if (arg.Player() == null) return;

            CuiHelper.DestroyUi(player, "bg");
            CuiHelper.DestroyUi(player, "shopD_containeOverlayr");
            CuiHelper.DestroyUi(player, "shopD_container");
            CuiHelper.DestroyUi(player, "empty");
            CuiHelper.DestroyUi(player, "container");
            CuiHelper.DestroyUi(player, "close_btn");
        }


        [ConsoleCommand("shop_page")] // OPEN PAGE UI BUTTON COMMAND
        private void shop_page(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            var args = arg.Args;
            if (player == null) return;

            if (!readyCheck(player)) return;

            if (!config._as.addon)
            {
                CreateCui(player);
                if (args.Length == 3)
                    ButtonHL(player, Convert.ToInt32(args[2]));
                //$"shop_page {storedCategories[i + startingIndex]} 0 {i + startingIndex}"
                OpenShopPage(player, args[0], Convert.ToInt32(args[1]));
            }
            else
            {
                CreateCui(player);
                OpenShopPage(player, args[0], Convert.ToInt32(args[1]));
            }
        }

        [ConsoleCommand("shop_categories")] //???
        private void shop_categories(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            if (player == null) return;
            var args = arg?.Args;

            if (!readyCheck(player)) return;

#if CARBON
            if (args.Length == 0)
            {
                CreateCui(player);
                CategorySelection(player);
            }
            else
            {
                CreateCui(player);
                CategorySelection(player, Convert.ToInt32(args[0]));
            }

            return;
#endif
           
            if (args == null)
            {
                CreateCui(player);
                CategorySelection(player);
            }
            else
            {
                CreateCui(player);
                CategorySelection(player, Convert.ToInt32(args[0]));
            }
        }

        [ConsoleCommand("shopDefault_categories")] // ???
        private void shopDefault_categories(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            if (player == null) return;
            var args = arg.Args;

            CategorySelection(player, Convert.ToInt32(args[0]));
        }

        [ConsoleCommand("shop_cmd")] // SHOP ACTIONS UI BUTTON COMMANDS
        private void shop_cmd(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            if (player == null) return;
            var args = arg.Args;

            if (!readyCheck(player)) return;

            if (args[0] == "buy")
                Buy(player, args[1], args[2], Convert.ToInt32(args[3]));
            //          category  item    amount

            if (args[0] == "sell")
                Sell(player, args[1], args[2], Convert.ToInt32(args[3]));

            if (args[0] == "buyCmd")
                BuyCommand(player, args[1], args[2]);


            if (args[0] == "select")
            {
                //  index                    item    category  currency type
                CreateSelectionCui(player, Convert.ToInt32(args[2]), args[1], args[3], args[4]);
            }

            if (args[0] == "swap")
            {
                if (args[5] == "sell")
                    CreateSelectionCui(player, Convert.ToInt32(args[2]), args[1], args[3], args[4], true);

                if (args[5] == "buy")
                    CreateSelectionCui(player, Convert.ToInt32(args[2]), args[1], args[3], args[4], false);
            }

            if (args[0] == "default_text")
                CuiHelper.DestroyUi(player, "amount_btn");

            if (args[0] == "input")
            {
                try
                {
                    if (IsDigitsOnly(args[6]))
                    {
                        if (!amountStorage.ContainsKey(player.userID))
                            amountStorage.Add(player.userID, Convert.ToInt32(args[6]));
                        else
                            amountStorage[player.userID] = Convert.ToInt32(args[6]);
                    }
                    else
                        CreateSelectionCui(player, Convert.ToInt32(args[2]), args[1], args[3], args[4], false, false);

                    if (args[5] == "buy")
                    {             //index               item    category-currency-sell   onInput?
                        CreateSelectionCui(player, Convert.ToInt32(args[2]), args[1], args[3], args[4], false, true);
                    }
                    if (args[5] == "sell")
                    {             //index               item    category-currency-sell   onInput?
                        CreateSelectionCui(player, Convert.ToInt32(args[2]), args[1], args[3], args[4], true, true);
                    }
                }
                catch { }
            }

        }

        #endregion

        #region     Shared

        // list of all categories
        private List<string> storedCategories = new List<string>();

        // create categories buttons
        private void CategorySelection(BasePlayer player, int page = 0)
        {
            // position to start from list of categories
            int startingIndex = 6 * page;

            if (!config._as.addon)
                startingIndex = 10 * page;

            // count list of categories
            int countWhole = storedCategories.Count();
            int _lenght = countWhole - startingIndex;

            // anchors for category buttons (standalone version)
            string[] cm_Anchors = {
                "0.01 0.81/0.98 0.885",
                "0.01 0.735/0.98 0.81",
                "0.01 0.66/0.98 0.735",
                "0.01 0.585/0.98 0.660",
                "0.01 0.510/0.98 0.585",
                "0.01 0.435/0.98 0.510",
                "0.01 0.360/0.98 0.435",
                "0.01 0.285/0.98 0.360",
                "0.01 0.210/0.98 0.285",
                "0.01 0.135/0.98 0.210",
            };

            //ref
            var _cM = CUIClass.CreateOverlay("empty", "0 0 0 0", "0 0", "0 0", false, 0.0f, "assets/icons/iconmaterial.mat"); //assets/content/ui/uibackgroundblur.mat

            if (config._as.addon)
            {
                //adjust anchors based on number of buttons to show up
                cm_Anchors = new string[] {
                    "0.13 0.67/0.49 0.97", "0.505 0.67/0.87 0.97",
                    "0.13 0.35/0.49 0.65", "0.505 0.35/0.87 0.65",
                    "0.13 0.03/0.49 0.33", "0.505 0.03/0.87 0.33", };

                if (_lenght < 5)
                    cm_Anchors = new string[] {
                        "0.13 0.51/0.49 0.81", "0.13 0.19/0.49 0.49",
                        "0.505 0.51/0.87 0.81", "0.505 0.19/0.87 0.49", };

                if (_lenght < 3) //&&
                    cm_Anchors = new string[] { "0.13 0.35/0.49 0.65", "0.51 0.35/0.87 0.65", };


                //create categories
                for (int i = 0; i < _lenght; i++)
                {
                    if (cm_Anchors.Length < i + 1) continue;

                    string[] anchorSplit = cm_Anchors[i].Split('/');
                    CUIClass.CreateButton(ref _cM, "cm_btn", "container", config.us.buttons["Category Buttons"], $"", 22, anchorSplit[0], anchorSplit[1], $"shop_page {storedCategories[i + startingIndex]} 0", "", "1 1 1 0.7", 0.2f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf", "assets/content/ui/uibackgroundblur.mat");
                    CUIClass.CreateImage(ref _cM, "cm_btn_img", $"cm_btn", $"{Img(categories[storedCategories[i + startingIndex]].Image)}", "0.12 0.35", "0.27 0.65", 0.2f);
                    CUIClass.CreateText(ref _cM, "cm_btn_text", "cm_btn", "1 1 1 0.8", $"{storedCategories[i + startingIndex].ToUpper().Replace('_', ' ')}", 18, "0.30 0", "0.87 1", TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf", 0.2f);
                }

                //page buttons
                if (page != 0)
                    CUIClass.CreateButton(ref _cM, "cm_btn_prev", "container", "0 0 0 0", "‹", 80, "0.0 0", "0.13 1", $"shop_categories {page - 1}", "", "1 1 1 0.8", 0.0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf", "assets/icons/iconmaterial.mat");

                if (countWhole - startingIndex > 6)
                    CUIClass.CreateButton(ref _cM, "cm_btn_next", "container", "0 0 0 0", "›", 80, "0.87 0", "1 1", $"shop_categories {page + 1}", "", "1 1 1 0.8", 0.0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf", "assets/icons/iconmaterial.mat");
            }
            else
            {
                //color for buttons, stupid but works
                string[] cm_btnColors = {
                    config.us.buttons["Button Color 1."],
                    config.us.buttons["Button Color 2."],
                    config.us.buttons["Button Color 1."],
                    config.us.buttons["Button Color 2."],
                    config.us.buttons["Button Color 1."],
                    config.us.buttons["Button Color 2."],
                    config.us.buttons["Button Color 1."],
                    config.us.buttons["Button Color 2."],
                    config.us.buttons["Button Color 1."],
                    config.us.buttons["Button Color 2."],
                };

                if (page != 0 && countWhole - startingIndex > 10)
                {
                    CUIClass.CreateButton(ref _cM, "cm_btn_prev", "shopD_side", "0 0 0 0", "ˆ ", 40, "0.0 0.0", "0.5 0.125", $"shopDefault_categories {page - 1}", "", "1 1 1 0.8", 0.0f, TextAnchor.MiddleRight, $"robotocondensed-bold.ttf", "assets/icons/iconmaterial.mat");
                    CUIClass.CreateButton(ref _cM, "cm_btn_next", "shopD_side", "0 0 0 0", " ˇ", 40, "0.5 0.0", "1 0.125", $"shopDefault_categories {page + 1}", "", "1 1 1 0.8", 0.0f, TextAnchor.UpperLeft, $"robotocondensed-bold.ttf", "assets/icons/iconmaterial.mat");

                }
                else
                {
                    // page buttons
                    if (page != 0)
                        CUIClass.CreateButton(ref _cM, "cm_btn_prev", "shopD_side", "0 0 0 0", "ˆ", 40, "0.0 0.0", "1 0.125", $"shopDefault_categories {page - 1}", "", "1 1 1 0.8", 0.0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf", "assets/icons/iconmaterial.mat");

                    if (countWhole - startingIndex > 10)
                        CUIClass.CreateButton(ref _cM, "cm_btn_next", "shopD_side", "0 0 0 0", "ˇ", 40, "0.0 0.0", "1 0.125", $"shopDefault_categories {page + 1}", "", "1 1 1 0.8", 0.0f, TextAnchor.UpperCenter, $"robotocondensed-bold.ttf", "assets/icons/iconmaterial.mat");
                }

                // create categories
                for (int i = 0; i < _lenght; i++)
                {
                    if (cm_Anchors.Length < i + 1) continue;

                    string[] anchorSplit = cm_Anchors[i].Split('/');
                    CUIClass.CreateButton(ref _cM, $"cm_btn{i}", "shopD_side", "0 0 0 0", $"", 22, anchorSplit[0], anchorSplit[1], $"shop_page {storedCategories[i + startingIndex]} 0 {i + startingIndex}", "", "1 1 1 0.7", 0.2f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf", "assets/icons/iconmaterial.mat");

                    // apply ui asset on every second button only
                    if (cm_btnColors[i] == config.us.buttons["Button Color 1."])
                        CUIClass.PullFromAssets(ref _cM, "btn_highlight2", $"cm_btn{i}", "0 0 0 0.6", "assets/content/ui/ui.background.transparent.linearltr.tga", 0.2f, 0.4f, "0 0", "1.2 1");

                    CUIClass.CreateImage(ref _cM, "cm_btn_img", $"cm_btn{i}", $"{Img(categories[storedCategories[i + startingIndex]].Image)}", "0.05 0.2", "0.165 0.8", 0.2f);
                    CUIClass.CreateText(ref _cM, "cm_btn_text", $"cm_btn{i}", "1 1 1 0.8", $"{storedCategories[i + startingIndex].Replace('_', ' ')}", 15, "0.22 0", "0.87 1", TextAnchor.MiddleLeft, $"robotocondensed-bold.ttf", 0.2f);

                    CuiHelper.DestroyUi(player, $"cm_btn{i}");
                }

                // destroy all before AddUi
                for (var i = 0; i < 11; i++)
                    CuiHelper.DestroyUi(player, $"cm_btn{i}");
            }

            CuiHelper.DestroyUi(player, "cm_btn_next");
            CuiHelper.DestroyUi(player, "cm_btn_prev");
            CuiHelper.AddUi(player, _cM);
            CuiHelper.DestroyUi(player, "empty");
        }

        // open certain page from category
        private void OpenShopPage(BasePlayer player, string category, int page)
        {
            int maxItems = 18;
            var list = ItemOrder(player, category, maxItems, page);
            ShowItems(player, list, category);
        }

        // create list of items to show
        private List<string> ItemOrder(BasePlayer player, string category, int numberOfItems, int page)
        {
            List<string> storedList = Facepunch.Pool.GetList<string>();
            int startingIndex = numberOfItems * page;
            int countWhole = categories[category].Items.Count();
            int itemsToShow = countWhole - (startingIndex + 1);
            int _length = numberOfItems;
            bool last = false;
            if (itemsToShow < numberOfItems)
                _length = itemsToShow + 1;

            if (countWhole - startingIndex < numberOfItems + 1)
                last = true;

            CreatePageBtns(player, category, page, last);

            for (var i = startingIndex; i < startingIndex + _length; i++)
                storedList.Add(categories[category].Items[i]);

            return storedList;
        }

        // show items from created list
        private void ShowItems(BasePlayer player, List<string> items, string category)
        {
            if (config.gl.gradually)
            {
                var run = _monoBehavior[player];
                if (run == null) PlayerComponent(true, player);
                if (run != null) run.CreateItems(player, items, category);
            }
            else
            {
                int count = 0;
                foreach (string item in items)
                {
                    CreateItemCui(player, count, items[count], category);
                    count++;
                }
                Facepunch.Pool.FreeList(ref items);
            }

            CurrencyBtns(player);
        }

        // create ui container for items
        private void CreateCui(BasePlayer player)
        {
            string anchorMin = "0.5 0.5";
            string anchorMax = "0.5 0.5";
            string offsetMin = container_Offset[0];
            string offsetMax = container_Offset[1];

            if (config._as.addon)
            {
                anchorMin = config._as.offset["AnchorMin"];
                anchorMax = config._as.offset["AnchorMax"];
                offsetMin = config._as.offset["OffSetMin"];
                offsetMax = config._as.offset["OffSetMax"];

            }
            var _base = CUIClass.CreateOverlay("empty", "0 0 0 0", "0 0", "0 0", false, 0.0f, "assets/icons/iconmaterial.mat"); //assets/content/ui/uibackgroundblur.mat
            //offset
            CUIClass.CreatePanel(ref _base, "container", layer, "0.25 0.23 0.22 0", anchorMin, anchorMax, true, 0.3f, 0f, "assets/icons/iconmaterial.mat", offsetMin, offsetMax, true);
            //parented panel
            CuiHelper.DestroyUi(player, "empty");
            CuiHelper.DestroyUi(player, "container");
            CuiHelper.DestroyUi(player, "close_btn");

            CuiHelper.AddUi(player, _base);
            CuiHelper.DestroyUi(player, "empty");
        }

        // create single item ui
        private void CreateItemCui(BasePlayer player, int index, string item = "test", string category = "Attire")
        {
            string img = "";
            string displayName = null;
            if (item.Contains("cmd/") || item.Contains("command/"))
            {
                string[] splits = item.Split('/');
                if (commands[splits[1]].Image == null)
                    img = "https://rustplugins.net/products/shop/rustlogo.jpg";
                else
                    img = commands[splits[1]].Image;

                if (commands[splits[1]].ShowDisplayName != null && commands[splits[1]].ShowDisplayName)
                    displayName = commands[splits[1]].DisplayName;
            }
            else
            {
                if (items[item].Image == null)
                    img = "https://rustplugins.net/products/shop/rustlogo.jpg";
                else
                    img = items[item].Image;

                if (items[item].ShowDisplayName != null && items[item].ShowDisplayName)
                    displayName = items[item].DisplayName;

                if (displayName == "default")
                    displayName = ItemManager.FindItemDefinition(item).displayName.translated;
            }


            string prefix = "https://wiki.rustclash.com/img/items180/";
            int fullPrice = PriceAfterSales(player, category, item, false, true);
            int salePrice = PriceAfterSales(player, category, item);
            string currencyType = GetCurrencyType(player, item);
            string price = $"0 <b>{gl("eco_symbol")}</b>";
            string pAnchor = "0.94";
            string[] anchorSplit = anchorSet[index].Split('/');

            //ui
            var _item = CUIClass.CreateOverlay("empty", "0 0 0 0", "0 0", "0 0", false, 0.0f, "assets/icons/iconmaterial.mat"); //assets/content/ui/uibackgroundblur.mat
            //item  0.115 0.115 0.115 0.8
            CUIClass.CreateButton(ref _item, $"item{index}", "container", $"{config.gl.itemUI["Background Color"]}", $"\n     <size=11>{displayName}</size>", 4, anchorSplit[0], anchorSplit[1], $"shop_cmd select {item} {index} {category} {currencyType}", "", "1 1 1 0.7", config.gl.fadeIn, TextAnchor.UpperLeft, $"robotocondensed-regular.ttf", "assets/content/ui/uibackgroundblur-ingamemenu.mat");
            
            if (img.StartsWith("http"))
                CUIClass.CreateImage(ref _item, "img", $"item{index}", $"{Img(img)}", "0.18 0.20", "0.82 0.82", config.gl.fadeIn);
            else {
                var itemDef = ItemManager.FindItemDefinition(item.Split('{')[0]);
                if (itemDef == null) 
                {
                    CUIClass.CreateImage(ref _item, "img", $"item{index}", $"{Img(img)}", "0.18 0.20", "0.82 0.82", config.gl.fadeIn);
                }
                else 
                {
                    CUIClass.GameIcon(ref _item, "img", $"item{index}", itemDef.itemid, items[item].Skin, config.gl.fadeIn, 0f, "0.18 0.20", "0.82 0.82");
                }
            }

            //is wipe blocked
            if (IsWipeBlocked(item) > 0)
            {
                long wipeBlock = (long)IsWipeBlocked(item);
                string cooldownFormated = "";
                if (wipeBlock > 0)
                {
                    TimeSpan cooldownTS = TimeSpan.FromSeconds(wipeBlock);
                    cooldownFormated = string.Format("{0:D1}D : {1:D2}H : {2:D2}M", cooldownTS.Days, cooldownTS.Hours, cooldownTS.Minutes);
                    if (wipeBlock < 86400) cooldownFormated = string.Format("{0:D2}H : {1:D2}M", cooldownTS.Hours, cooldownTS.Minutes);

                    CUIClass.CreatePanel(ref _item, "red_box", $"item{index}", "0.56 0.20 0.15 1.0", "0.125 0.40", $"0.885 0.61", false, 0.1f, 0f, "assets/content/ui/uibackgroundblur.mat");
                    CUIClass.CreateButton(ref _item, $"wipeblock_cvr{index}", "container", "0.115 0.115 0.115 0.05", cooldownFormated, 11 + fontAdjustment, anchorSplit[0], anchorSplit[1], $"", "", "1 1 1 0.7", 0.5f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf", "assets/icons/iconmaterial.mat");
                }
            }
            else
            {
                //is on cooldown
                if (config.ms.cooldowns)
                {
                    if (cooldownData.ContainsKey(item) || cooldownData.ContainsKey("cmd/" + item) || cooldownData.ContainsKey("command/" + item))
                    {
                        // is player on cooldown
                        if (playerData.ContainsKey(player.userID))
                        {
                            if (playerData[player.userID].cooldowns.ContainsKey(item))
                            {
                                long timeNow = DateTimeOffset.Now.ToUnixTimeSeconds();
                                long timeDif = timeNow - playerData[player.userID].cooldowns[item];
                                if (timeDif < cooldownData[item])
                                {

                                    TimeSpan countdownFormated = TimeSpan.FromSeconds(cooldownData[item] - timeDif);
                                    string countDownText = string.Format("{0:D1}D : {1:D2}H : {2:D2}M", countdownFormated.Days, countdownFormated.Hours, countdownFormated.Minutes);

                                    CUIClass.CreatePanel(ref _item, "red_box", $"item{index}", "0.56 0.20 0.15 1.0", "0.125 0.40", $"0.885 0.61", false, 0.1f, 0f, "assets/content/ui/uibackgroundblur.mat");
                                    CUIClass.CreateButton(ref _item, $"wipeblock_cvr{index}", "container", "0.115 0.115 0.115 0.05", countDownText, 11 + fontAdjustment, anchorSplit[0], anchorSplit[1], $"", "", "1 1 1 0.7", 0.5f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf", "assets/icons/iconmaterial.mat");

                                }
                            }
                        }
                    }
                }
            }

            //is on sale?
            if (fullPrice != salePrice)
            {
                price = $"<size={7 + fontAdjustment}>-{fullPrice}-</size>  {salePrice}";

                CUIClass.CreatePanel(ref _item, "sale", $"item{index}", "0.808 0.259 0.161 1", "0.05 0.80", $"0.35 0.945", false, 0.1f, 0f, "assets/content/ui/uibackgroundblur.mat");
                CUIClass.CreateText(ref _item, "text", "sale", "1 1 1 0.8", $"SALE", 10 + fontAdjustment, "0 0", "1 0.9", TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf", 0.5f);
            }
            else
            {
                price = $"{fullPrice}";
            }

            if(fullPrice == 0)
            {
                price = $"{PriceAfterSales(player, category, item, true)}";
            }

            //currency type?
            if (currencyType == "rp")
                price += $"<b>{gl("rp_symbol")}</b>";

            if (currencyType == "eco")
                price += $"<b>{gl("eco_symbol")}</b>";

            if (currencyType != "eco" && currencyType != "rp")
            {
                price += "";
                pAnchor = "0.79";

                if (config.gl.forceImage != null)
                {
                    CUIClass.CreateImage(ref _item, "img", $"item{index}", $"{Img(config.gl.forceImage)}", "0.805 0.045", "0.95 0.20", 0.5f);
                }
                else 
                {
                    var itemDef = ItemManager.FindItemDefinition(currencyType);
                    if (itemDef != null) 
                    {
                        CUIClass.GameIcon(ref _item, "img", $"item{index}", itemDef.itemid, 0, config.gl.fadeIn, 0f, "0.805 0.045", "0.95 0.20");
                    }
                }

                //CUIClass.CreateImage(ref _item, "img", $"item{index}", $"{Img(currencyImage)}", "0.805 0.045", "0.95 0.20", 0.5f);
            }

            //item price
            CUIClass.CreateText(ref _item, "text", $"item{index}", $"{config.gl.itemUI["Text Color"]}", price, 11 + fontAdjustment, "0.03 0.05", $"{pAnchor} 1", TextAnchor.LowerRight, $"robotocondensed-regular.ttf", 0.5f);
            //end
            CuiHelper.AddUi(player, _item);
            CuiHelper.DestroyUi(player, "empty");


        }

        // create page buttons for list of items
        private void CreatePageBtns(BasePlayer player, string category, int currentPage = 0, bool last = false, bool npc = false, string npcId = "0")
        {
            string upCmd = $"shop_page {category} {currentPage - 1}";
            string downCmd = $"shop_page {category} {currentPage + 1}";

            if (npc)
            {
                upCmd = $"npc_page {npcId} {currentPage - 1}";
                downCmd = $"npc_page {npcId} {currentPage + 1}";
            }

            var _btnP = CUIClass.CreateOverlay("empty", "0 0 0 0", "0 0", "0 0", false, 0.0f, "assets/icons/iconmaterial.mat"); //assets/content/ui/uibackgroundblur.mat

            if (currentPage != 0)
                CUIClass.CreateButton(ref _btnP, "btn_up", "container", $"{config.us.buttons["Page Button Color"]}", "▲", 11, "0.825 -0.09", "0.9 0", upCmd, "", "1 1 1 0.7", 0.0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf", "assets/content/ui/uibackgroundblur.mat");

            if (!last)
                CUIClass.CreateButton(ref _btnP, "btn_down", "container", $"{config.us.buttons["Page Button Color"]}", "▼", 11, "0.91 -0.09", "0.985 0", downCmd, "", "1 1 1 0.7", 0.0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf", "assets/content/ui/uibackgroundblur.mat");

            if (config._as.addon && categories.Count() > 1)
                CUIClass.CreateButton(ref _btnP, "btn_back", "container", $"{config.us.buttons["Page Button Color"]}", gl("back_button"), 9, "0.0 -0.09", "0.16 0", $"shop_categories", "", "1 1 1 0.7", 0.0f, TextAnchor.MiddleCenter, $"robotocondensed-regular.ttf", "assets/content/ui/uibackgroundblur.mat");

            /* if (config._as.addon)
            {
                try
                {
                    int template = (int)WelcomePanel.Call("GetLayout_API");
                    if (template == 2 || template == 5)
                        CUIClass.CreateText(ref _btnP, "category_text", "container", "1 1 1 0.7", $" {category}", 30, "0 1", "1 1.15", TextAnchor.MiddleLeft, $"robotocondensed-bold.ttf", 0.1f);
                }
                catch
                {
                    //
                }
            } */

            CuiHelper.DestroyUi(player, "category_text");
            CuiHelper.DestroyUi(player, "btn_back");
            CuiHelper.DestroyUi(player, "btn_up");
            CuiHelper.DestroyUi(player, "btn_down");
            CuiHelper.AddUi(player, _btnP);
            CuiHelper.DestroyUi(player, "empty");

        }

        // item amount storage from user input
        private Dictionary<ulong, int> amountStorage = new Dictionary<ulong, int>();

        int ItemAmount(BasePlayer player, string shortname)
        {
            if (shortname.Contains("{"))
            {
                string[] shortnameSplit = shortname.Split('{');
                shortname = shortnameSplit[0];
            }
            int inventory = 0;
            var itemDef = ItemManager.FindItemDefinition(shortname);
            if (itemDef != null)
                inventory = player.inventory.GetAmount(itemDef.itemid);

            return inventory;
        }

        // create buttons on top of selected item (buy/sell/amount...)
        private void CreateSelectionCui(BasePlayer player, int index, string item, string category, string currencyType, bool sell = false, bool onInput = false)
        {
            //ui ref
            var _slc = CUIClass.CreateOverlay("empty", "0 0 0 0", "0 0", "0 0", false, 0.0f, "assets/icons/iconmaterial.mat"); //assets/content/ui/uibackgroundblur.mat

            // if item is command
            if (item.Contains("cmd/") || item.Contains("command/"))
            {
                string _price = "";
                if (currencyType == "rp")
                    _price += gl("rp_symbol");

                if (currencyType == "eco")
                    _price += gl("eco_symbol");

                if (currencyType != "eco" && currencyType != "rp")
                    _price += "";



                int _buyPrice = PriceAfterSales(player, category, item);

                string _btnText = $"{gl("buy_btn")} <size={11 + fontAdjustment}><b>{_buyPrice}{_price}</b></size>";
                string _btnColor = config.gl.itemUI["Buy Button Color"];
                string _cmd = $"shop_cmd buyCmd {category} {item}";


                //overlay to prevent double click
                CUIClass.CreateButton(ref _slc, "block", $"item{index}", "0 0 0 0", "", 8 + fontAdjustment, "0 0", $"1 1", "", "", "1 1 1 0.8", 0f, TextAnchor.MiddleCenter, $"robotocondensed-regular.ttf", "assets/icons/iconmaterial.mat");
                //btn
                CUIClass.CreateButton(ref _slc, "buy_btn", $"item{index}", _btnColor, _btnText, 8 + fontAdjustment, "0.055 0.055", $"0.945 0.23", _cmd, "", "1 1 1 0.8", 0.1f, TextAnchor.MiddleCenter, $"robotocondensed-regular.ttf", "assets/content/ui/uibackgroundblur.mat");
                //currency item image
                if (currencyType != "eco" && currencyType != "rp")
                {
                    /* string currencyImage = $"https://wiki.rustclash.com/img/items180/{currencyType}.png";
                    if (config.gl.forceImage != null) currencyImage = config.gl.forceImage;

                    CUIClass.CreateImage(ref _slc, "buy_btn_currencyImg", $"buy_btn", $"{Img(currencyImage)}", "0.83 0.15", "0.97 0.85", 0f);
                    */

                    if (config.gl.forceImage != null)
                    {
                        CUIClass.CreateImage(ref _slc, "buy_btn_currencyImg", $"buy_btn", $"{Img(config.gl.forceImage)}", "0.83 0.15", "0.97 0.85", 0f);
                    }
                    else 
                    {
                        var itemDef = ItemManager.FindItemDefinition(currencyType);
                        if (itemDef != null) 
                        {
                            CUIClass.GameIcon(ref _slc, "buy_btn_currencyImg", $"buy_btn", itemDef.itemid, 0, config.gl.fadeIn, 0f, "0.83 0.15", "0.97 0.85");
                        }
                    }
                }

                //end
                CuiHelper.DestroyUi(player, "block");
                CuiHelper.DestroyUi(player, "input_panel");
                CuiHelper.DestroyUi(player, "buy_btn");
                CuiHelper.DestroyUi(player, "amount_btn");
                CuiHelper.DestroyUi(player, "swap_btn");
                CuiHelper.DestroyUi(player, "buy_btn_currencyImg");
                CuiHelper.AddUi(player, _slc);
                CuiHelper.DestroyUi(player, "empty");
                return;
            }

            // when changing item amount
            if (!onInput)
            {
                if (!amountStorage.ContainsKey(player.userID))
                {
                    if (items.ContainsKey(item))
                        amountStorage.Add(player.userID, items[item].DefaultAmount);
                    else
                        amountStorage.Add(player.userID, 1);
                }
                else
                {
                    if (items.ContainsKey(item))
                        amountStorage[player.userID] = items[item].DefaultAmount;
                    else
                        amountStorage[player.userID] = 1;
                }
            }

            // currency text for button
            string price = "";
            if (currencyType == "rp")
                price += gl("rp_symbol");

            if (currencyType == "eco")
                price += gl("eco_symbol");

            if (currencyType != "eco" && currencyType != "rp")
                price += "";

            // calculate currency based on amount
            if (sell && config.ms.prefill) amountStorage[player.userID] = ItemAmount(player, item);
            int buyPrice = (int)Math.Ceiling((double)PriceAfterSales(player, category, item) * ((double)amountStorage[player.userID] / (double)items[item].DefaultAmount));
            int sellPrice = (int)((double)PriceAfterSales(player, category, item, true) * ((double)amountStorage[player.userID] / (double)items[item].DefaultAmount));

            // text and commands for buy/sell buttons
            string btnText = $"{gl("buy_btn")} <size={11 + fontAdjustment}><b>{buyPrice}{price}</b></size>";
            string btnColor = config.gl.itemUI["Buy Button Color"];
            string cmd = $"shop_cmd buy {category} {item} {amountStorage[player.userID]}";
            string swapCmd = $"shop_cmd swap {item} {index} {category} {currencyType} sell";
            string inputCmd = $"shop_cmd input {item} {index} {category} {currencyType} buy";

            if (buyPrice <= 0)
                sell = true;

            // dumb but it is what it is
            if (sell && config.ms.prefill) amountStorage[player.userID] = ItemAmount(player, item);

            if (sell)
            {
                btnText = $"{gl("sell_btn")} <size={11 + fontAdjustment}><b>{sellPrice}{price}</b></size>";
                btnColor = config.gl.itemUI["Sell Button Color"];
                cmd = $"shop_cmd sell {category} {item} {amountStorage[player.userID]}";
                swapCmd = $"shop_cmd swap {item} {index} {category} {currencyType} buy";
                inputCmd = $"shop_cmd input {item} {index} {category} {currencyType} sell";
            }
            
            //overlay to prevent double click
            CUIClass.CreateButton(ref _slc, "block", $"item{index}", "0 0 0 0", "", 8 + fontAdjustment, "0 0", $"1 1", "", "", "1 1 1 0.8", 0f, TextAnchor.MiddleCenter, $"robotocondensed-regular.ttf", "assets/icons/iconmaterial.mat");
            //btn
            CUIClass.CreateButton(ref _slc, "buy_btn", $"item{index}", btnColor, btnText, 8 + fontAdjustment, "0.25 0.055", $"0.945 0.23", cmd, "", "1 1 1 0.8", 0.1f, TextAnchor.MiddleCenter, $"robotocondensed-regular.ttf", "assets/content/ui/uibackgroundblur.mat");
            //amount input
            CUIClass.CreatePanel(ref _slc, "input_panel", $"item{index}", $"{config.gl.itemUI["Input Field Color"]}", "0.035 0.05", $"0.22 0.23", false, 0.1f, 0f, "assets/icons/iconmaterial.mat");
            CUIClass.CreateInput(ref _slc, "input_field", "input_panel", "1 1 1 0.8", 9 + fontAdjustment, "0 0", "1 1", $"{amountStorage[player.userID]}", "robotocondensed-regular.ttf", $"{inputCmd}", TextAnchor.MiddleCenter, 4);
            
            if (items[$"{item}"].BlockAmountChange)
                CUIClass.CreatePanel(ref _slc, "input_panel2", $"item{index}", $"0 0 0 0", "0.035 0.05", $"0.22 0.23", false, 0.1f, 0f, "assets/icons/iconmaterial.mat");
            
            
            //swap button
            if (PriceAfterSales(player, category, item, true) != 0 && PriceAfterSales(player, category, item, true) != null)
            {
                CUIClass.CreateButton(ref _slc, "swap_btn", $"item{index}", "0 0 0 0", "", 8, "0.75 0.75", "1 1", swapCmd, "", "1 1 1 0.8", 0.1f, TextAnchor.MiddleCenter, $"robotocondensed-regular.ttf", "assets/icons/iconmaterial.mat");
                CUIClass.PullFromAssets(ref _slc, "swap_btn_icon", $"swap_btn", "1 1 1 0.5", "assets/icons/refresh.png", 0.1f, 0f, "0.25 0.25", "0.75 0.75");
            }
            //currency item image
            if (currencyType != "eco" && currencyType != "rp")
            {
                /* string currencyImage = $"https://wiki.rustclash.com/img/items180/{currencyType}.png";
                if (config.gl.forceImage != null) currencyImage = config.gl.forceImage;

                CUIClass.CreateImage(ref _slc, "buy_btn_currencyImg", $"buy_btn", $"{Img(currencyImage)}", "0.81 0.15", "0.97 0.85", 0f); */

                /* if (config.gl.forceImage != null)
                {
                    CUIClass.CreateImage(ref _slc, "buy_btn_currencyImg", $"buy_btn", $"{Img(config.gl.forceImage)}", "0.83 0.15", "0.97 0.85", 0f);
                }
                else 
                {                        
                    var itemDef = ItemManager.FindItemDefinition(currencyType);
                    if (itemDef != null) 
                    {
                        CUIClass.GameIcon(ref _slc, "buy_btn_currencyImg", $"buy_btn", itemDef.itemid, 0, config.gl.fadeIn, 0f, "0.83 0.15", "0.97 0.85");
                    }
                } */
            }
            //end
            CuiHelper.DestroyUi(player, "block");
            CuiHelper.DestroyUi(player, "input_panel");
            CuiHelper.DestroyUi(player, "buy_btn");
            CuiHelper.DestroyUi(player, "amount_btn");
            CuiHelper.DestroyUi(player, "swap_btn");
            CuiHelper.DestroyUi(player, "buy_btn_currencyImg");
            CuiHelper.AddUi(player, _slc);
            CuiHelper.DestroyUi(player, "empty");
        }

        // create panels with player's balance
        private void CurrencyBtns(BasePlayer player)
        {
            var _crBtn = CUIClass.CreateOverlay("empty", "0 0 0 0", "0 0", "0 0", false, 0.0f, "assets/icons/iconmaterial.mat"); //assets/content/ui/uibackgroundblur.mat
            string anchorMin = "0.0 -0.09";
            string anchorMax = "0.18 0";
            string text = gl("balance_panel");

            //anchors needs to be adjusted due "BACK TO CATEG." button
            if (config._as.addon)
            {
                anchorMin = "0.165 -0.09"; anchorMax = "0.35 0";

                if (categories.Count() < 2)
                    anchorMin = "0.0 -0.09";
            }

            if (Economics != null)
            {   
                //int inventory = player.inventory.GetAmount(-1779183908);
                double playersEco = Math.Round(Economics.Call<double>("Balance", player.UserIDString));
                text += $"<b><color=#F9F9F9>{playersEco}</color></b>{gl("eco_symbol")}";

                if (ServerRewards != null)
                {
                    var playersRP = ServerRewards?.Call<int>("CheckPoints", player.userID);
                    text += $" <size=11>/</size> <b><color=#F9F9F9>{playersRP}</color></b>{gl("rp_symbol")}";
                    anchorMax = "0.325 0";

                    //anchors needs to be adjusted due "BACK TO CATEG." button
                    if (config._as.addon) anchorMax = "0.492 0";
                }
            }
            else
            {
                if (ServerRewards != null)
                {
                    var playersRP = ServerRewards?.Call<int>("CheckPoints", player.userID);
                    text += $"{playersRP}<b>{gl("rp_symbol")}</b>";
                }
            }

            if (Economics != null || ServerRewards != null)
            {
                CUIClass.CreatePanel(ref _crBtn, "crBtn_eco", $"container", $"{config.gl.itemUI["Background Color"]}", anchorMin, anchorMax, false, 0.0f, 0.0f, "assets/content/ui/uibackgroundblur.mat");
                CUIClass.CreateText(ref _crBtn, "crBtn_eco_text", $"crBtn_eco", "1 1 1 0.35", text, 12, "0.0 0", "1 1", TextAnchor.MiddleCenter, $"robotocondensed-regular.ttf", 0.0f);
            }

            CuiHelper.DestroyUi(player, "crBtn_eco");
            CuiHelper.AddUi(player, _crBtn);
            CuiHelper.DestroyUi(player, "empty");
        }

        // notifications
        private void Notifs(BasePlayer player, int type = 0, string[] properties = null)
        {
            // properties = panel color, accent color, message, sound prefab, 

            // ref
            var _btnH = CUIClass.CreateOverlay("empty", "0 0 0 0", "0 0", "0 0", false, 0.0f, "assets/icons/iconmaterial.mat"); //assets/content/ui/uibackgroundblur.mat

            // success highlight for button
            if (type == 0)
            {
                CUIClass.PullFromAssets(ref _btnH, "btn_highlight_bg", $"block", "0.40 0.48 0.25 0.4", "assets/content/ui/ui.background.transparent.radial.psd", 0.2f, 0.4f, "0.0 0.0", "1 1");
                CUIClass.PullFromAssets(ref _btnH, "btn_highlight", $"block", "0.40 0.48 0.25 1", "assets/icons/check.png", 0.2f, 0.4f, "0.3 0.3", "0.7 0.7");
                CuiHelper.DestroyUi(player, "btn_highlight");
                CuiHelper.DestroyUi(player, "btn_highlight_bg");
            }

            // fail highlight for button
            if (type == 1)
            {
                CUIClass.PullFromAssets(ref _btnH, "btn_highlight_bg", $"block", "0.56 0.20 0.15 0.4", "assets/content/ui/ui.background.transparent.radial.psd", 0.2f, 0.4f, "0.0 0.0", "1 1");
                CUIClass.PullFromAssets(ref _btnH, "btn_highlight", $"block", "0.56 0.20 0.15 1.0", "assets/icons/close.png", 0.2f, 0.4f, "0.25 0.25", "0.75 0.75");
                CuiHelper.DestroyUi(player, "btn_highlight");
                CuiHelper.DestroyUi(player, "btn_highlight_bg");
            }

            // customizable large notification 
            if (type == 2)
            {
                CUIClass.CreatePanel(ref _btnH, "notif_panel", "Overlay", $"{properties[0]}", "1 0.5", "1 0.5", false, 0.0f, 0.0f, "assets/content/ui/uibackgroundblur.mat", "-190 100", "0 170");
                CUIClass.CreatePanel(ref _btnH, "notif_panel_color", "notif_panel", $"{properties[1]}", "0 0", "0.025 1", false, 0.2f, 0.3f, "assets/content/ui/uibackgroundblur.mat");
                CUIClass.CreateText(ref _btnH, "notif_text", "notif_panel", "1 1 1 0.8", $"{properties[2]}", 11, "0.08 0", "0.92 0.94", TextAnchor.UpperLeft, $"robotocondensed-regular.ttf", 0.2f, 0.3f);
                //CUIClass.PullFromAssets(ref _btnH, "notif_icon", $"container", "1 1 1 1", "assets/icons/close.png", 0.2f, 0.3f, "0.21 -0.07", "0.245 -0.02");
                DestroyLNotif(player);
                PlayFx(player, $"{properties[3]}");
            }

            CuiHelper.AddUi(player, _btnH);
            CuiHelper.DestroyUi(player, "empty");
        }

        // destroy notification - used in mono
        private void DestroyLNotif(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "notif_panel");
            CuiHelper.DestroyUi(player, "notif_panel_color");
            CuiHelper.DestroyUi(player, "notif_text");
            CuiHelper.DestroyUi(player, "notif_icon");
        }

        // destroy whole ui
        private void DestroyShop(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "bg");
            CuiHelper.DestroyUi(player, "shopD_base_img");
            CuiHelper.DestroyUi(player, "shopD_side_img");
            CuiHelper.DestroyUi(player, "shopD_containeOverlayr");
            CuiHelper.DestroyUi(player, "shopD_container");
            CuiHelper.DestroyUi(player, "empty");
            CuiHelper.DestroyUi(player, "container");
            CuiHelper.DestroyUi(player, "close_btn");
        }

        // play sound prefab
        private void PlayFx(BasePlayer player, string fx)
        {
            if (!config.ns.sounds) return;
            if (player == null) return;

            var EffectInstance = new Effect();
            EffectInstance.Init(Effect.Type.Generic, player, 0, Vector3.up, Vector3.zero);
            EffectInstance.pooledstringid = StringPool.Get(fx);
            NetWrite netWrite = Net.sv.StartWrite();
            netWrite.PacketID(Message.Type.Effect);
            EffectInstance.WriteToStream(netWrite);
            netWrite.Send(new SendInfo(player.net.connection));
            EffectInstance.Clear();
        }

        private void PreloadImages(BasePlayer player)
        {
            var _renameUi = CUIClass.CreateOverlay("empty", "0 0 0 0", "0 0", "0 0", false, 0.0f, "assets/icons/iconmaterial.mat"); //assets/content/ui/uibackgroundblur.mat
            CUIClass.CreateImage(ref _renameUi, "shopD_base_img", "Overlay", $"{Img(config.us.main["Image"])}", "1 1", "2 2", 0.0f);
            CUIClass.CreateImage(ref _renameUi, "shopD_side_img", "Overlay", $"{Img(config.us.side["Image"])}", "1 1", "2 2", 0.0f);
            CuiHelper.AddUi(player, _renameUi);
        }

        #endregion

        #region     Standalone

        // base layout
        private void CreateDefaultCui(BasePlayer player)
        {
            int count = storedCategories.Count;

            //background
            var _def = CUIClass.CreateOverlay("bg", $"0 0 0 {config.us.bgBlurr}", "0 0", "1 1", false, 0.3f, "assets/content/ui/uibackgroundblur.mat");
            CUIClass.CreatePanel(ref _def, "bg2", "bg", $"{config.us.bgColor}", "0 0", "1 1", true, 0.3f, 0f, "assets/icons/iconmaterial.mat");
            //offset
            CUIClass.CreatePanel(ref _def, "shopD_containeOverlayr", "Overlay", "0 0 0 0.0", "0 0", "1 1", false, 0.0f, 0f, "assets/icons/iconmaterial.mat");

            CUIClass.CreatePanel(ref _def, "shopD_container", "shopD_containeOverlayr", "0 0 0 0.0", $"{config.us.offset["AnchorMin"]}", $"{config.us.offset["AnchorMax"]}", false, 0.0f, 0f, "assets/icons/iconmaterial.mat", $"{config.us.offset["OffSetMin"]}", $"{config.us.offset["OffSetMax"]}");
            //main
            CUIClass.CreatePanel(ref _def, "shopD_base", "shopD_container", $"{config.us.main["Color"]}", $"{config.us.main["AnchorMin"]}", $"{config.us.main["AnchorMax"]}", false, 0.0f, 0f, "assets/icons/iconmaterial.mat");
            if (config.us.main["Image"].StartsWith("http"))
                CUIClass.CreateImage(ref _def, "shopD_base_img", "shopD_base", $"{Img(config.us.main["Image"])}", "0 0", "1 1", 0.0f);
            //side
            CUIClass.CreatePanel(ref _def, "shopD_side", "shopD_container", $"{config.us.side["Color"]}", $"{config.us.side["AnchorMin"]}", $"{config.us.side["AnchorMax"]}", false, 0.0f, 0f, "assets/icons/iconmaterial.mat");
            if (config.us.side["Image"].StartsWith("http"))
                CUIClass.CreateImage(ref _def, "shopD_side_img", "shopD_side", $"{Img(config.us.side["Image"])}", "0 0", "1 1", 0.0f);
            //title
            CUIClass.CreatePanel(ref _def, "shopD_title", "shopD_container", $"{config.us.title["Color"]}", $"{config.us.title["AnchorMin"]}", $"{config.us.title["AnchorMax"]}", false, 0.0f, 0f, "assets/icons/iconmaterial.mat");
            CUIClass.CreateText(ref _def, "shopD_title_text", "shopD_title", "1 1 1 0.85", $"{config.us.title["Text"]}", 15, "0.055 0.00", "1 1", TextAnchor.MiddleLeft, $"robotocondensed-regular.ttf", 0.0f);
            if (config.us.logo["Image"].StartsWith("http"))
                CUIClass.CreateImage(ref _def, "shopD_title_logo", "shopD_title", $"{Img(config.us.logo["Image"])}", $"{config.us.logo["AnchorMin"]}", $"{config.us.logo["AnchorMax"]}", 0.0f);

            CUIClass.CreateButton(ref _def, "shopD_closeBtn", "shopD_title", $"{config.us.buttons["Close Button Color"]}", "✘ CLOSE ", 11, "0.92 0.2", $"0.992 0.8", $"shopclose", "", "1 1 1 0.7", 1f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf", "assets/icons/iconmaterial.mat");


            CuiHelper.DestroyUi(player, "bg");
            CuiHelper.DestroyUi(player, "shopD_container");
            CuiHelper.AddUi(player, _def);
        }

        // highlight selected button
        private void ButtonHL(BasePlayer player, int index = 0)
        {
            int index_corrected = index;
            // correction when you are on second page to not break index order from stored list of categories
            if (index > 9)
            {
                index_corrected -= 10;
            }
            var _btnHL = CUIClass.CreateOverlay("empty", "0 0 0 0", "0 0", "0 0", false, 0.0f, "assets/icons/iconmaterial.mat"); //assets/content/ui/uibackgroundblur.mat

            CUIClass.CreatePanel(ref _btnHL, "btn_HL", $"cm_btn{index_corrected}", $"{config.us.buttons["Active Button Color"]}", "0 0", "0.965 1", false, 0.2f, 0.0f, "assets/content/ui/uibackgroundblur.mat");
            CUIClass.CreateImage(ref _btnHL, "cm_btn_img", $"btn_HL", $"{Img(categories[storedCategories[index]].Image)}", "0.05 0.2", "0.165 0.8", 0.15f);
            CUIClass.CreateText(ref _btnHL, "cm_btn_text", $"btn_HL", "1 1 1 0.8", $"{storedCategories[index].Replace('_', ' ')}", 15, "0.22 0", "0.87 1", TextAnchor.MiddleLeft, $"robotocondensed-bold.ttf", 0.0f);

            CuiHelper.DestroyUi(player, "btn_HL");
            CuiHelper.AddUi(player, _btnHL);
            CuiHelper.DestroyUi(player, "empty");
        }

        #endregion

        #region     as Addon

        private void ShowShop_API(BasePlayer player)
        {
            if (!_ready)
            {
                SendReply(player, "Shop is not ready for use. Please contact server admin.");
                return;
            }

            if (config._as.addon)
            {   
                if (!canUseShop(player))
                    return;

                if (categories.Count() < 2)
                {
                    try
                    {
                        CreateCui(player);
                        OpenShopPage(player, storedCategories[0], 0);
                    }
                    catch
                    {
                        Puts($"\nError found in configuration files.\nPlease make sure that:\n\n• You didn't break json formating. Online Json validator can be used to check it.\n• You not missing any file in your data/Shop folder\n• Check if Item.json and Categories.json are not empty.\n\n\n If you still having issues, feel free to open support ticket and I will assist you soon as I can. (within 16 hours usually)");
                        SendReply(player, "<color=#E74F17><size=18>Error found in configuration files.</size></color>\nPlease make sure that:\n\n• You didn't break json formating. Online Json validator can be used to check it.\n• You not missing any file in your data/Shop folder\n• Check if Item.json and Categories.json are not empty.\n\n\n If you still having issues, feel free to open support ticket and I will assist you soon as I can. (within 16 hours usually)");
                    }
                }
                else
                {
                    try
                    {
                        var list = ItemOrder(player, storedCategories[0], 18, 0);
                        CreateCui(player);
                        CategorySelection(player);
                    }
                    catch
                    {
                        Puts($"\nError found in configuration files.\nPlease make sure that:\n\n• You didn't break json formating. Online Json validator can be used to check it.\n• You not missing any file in your data/Shop folder\n• Check if Item.json and Categories.json are not empty.\n\n\n If you still having issues, feel free to open support ticket and I will assist you soon as I can. (within 16 hours usually)");
                        SendReply(player, "<color=#E74F17><size=18>Error found in configuration files.</size></color>\nPlease make sure that:\n\n• You didn't break json formating. Online Json validator can be used to check it.\n• You not missing any file in your data/Shop folder\n• Check if Item.json and Categories.json are not empty.\n\n\n If you still having issues, feel free to open support ticket and I will assist you soon as I can. (within 16 hours usually)");
                    }
                }
            }
        }

        private void CloseShop_API(BasePlayer player) => DestroyShop(player);

        #endregion

        #region     as NPCShop

        private void NPCShop_MainCui(BasePlayer player, string shopname)
        {
            //background
            var _npcMain = CUIClass.CreateOverlay("bg", $"0 0 0 {config.us.bgBlurr}", "0 0", "1 1", false, 0.3f, "assets/content/ui/uibackgroundblur.mat");
            CUIClass.CreatePanel(ref _npcMain, "bg2", "bg", $"{config.us.bgColor}", "0 0", "1 1", true, 0.3f, 0f, "assets/icons/iconmaterial.mat");
            //offset
            CUIClass.CreatePanel(ref _npcMain, "shopD_container", "Overlay", "0 0 0 0.0", $"0.5 0.5", $"0.5 0.5", false, 0.0f, 0f, "assets/icons/iconmaterial.mat", $"-330 -200", $"330 200");
            //main
            CUIClass.CreatePanel(ref _npcMain, "shopD_base", "shopD_container", $"{config.us.main["Color"]}", "0 0", $"1 1", false, 0.0f, 0f, "assets/icons/iconmaterial.mat");
            if (config.us.main["Image"].StartsWith("http"))
                CUIClass.CreateImage(ref _npcMain, "shopD_base_img", "shopD_base", $"{Img(config.us.main["Image"])}", "0 0", "1 1", 0.0f);
            //items container
            CUIClass.CreatePanel(ref _npcMain, "container", "shopD_container", "0.25 0.23 0.22 0", "0.005 0.085", "1.01 0.9", true, 0.3f, 0f, "assets/icons/iconmaterial.mat");
            //title
            CUIClass.CreatePanel(ref _npcMain, "shopD_title", "shopD_container", $"{config.us.title["Color"]}", $"0 0.9", $"1 1", false, 0.0f, 0f, "assets/icons/iconmaterial.mat");
            CUIClass.CreateText(ref _npcMain, "shopD_title_text", "shopD_title", "1 1 1 0.85", $"{shopname}", 15, "0.055 0.00", "1 1", TextAnchor.MiddleLeft, $"robotocondensed-regular.ttf", 0.0f);
            if (config.us.logo["Image"].StartsWith("http"))
                CUIClass.CreateImage(ref _npcMain, "shopD_title_logo", "shopD_title", $"{Img(config.us.logo["Image"])}", $"{config.us.logo["AnchorMin"]}", $"{config.us.logo["AnchorMax"]}", 0.0f);

            CUIClass.CreateButton(ref _npcMain, "shopD_closeBtn", "shopD_title", "0.56 0.20 0.15 1.0", "✘ CLOSE ", 11, "0.92 0.2", $"0.992 0.8", $"shopclose", "", "1 1 1 0.7", 1f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf", "assets/icons/iconmaterial.mat");


            CuiHelper.DestroyUi(player, "bg");
            CuiHelper.DestroyUi(player, "shopD_container");
            CuiHelper.AddUi(player, _npcMain);
        }

        [ConsoleCommand("npc_page")]
        private void npc_page(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            var args = arg.Args;
            if (player == null) return;

            var itemList = NPCShop_ItemOrder(player, args[0], 18, Convert.ToInt32(args[1]));
            ShowItems(player, itemList, storedCategories[0]);

        }

        // create list of items to show
        private List<string> NPCShop_ItemOrder(BasePlayer player, string npcID, int numberOfItems, int page)
        {
            List<string> storedList = Facepunch.Pool.GetList<string>();
            int startingIndex = numberOfItems * page;
            int countWhole = humanNPC[npcID].Items.Count();
            int itemsToShow = countWhole - (startingIndex + 1);
            int _length = numberOfItems;
            bool last = false;
            if (itemsToShow < numberOfItems)
                _length = itemsToShow + 1;

            if (countWhole - startingIndex < numberOfItems + 1)
                last = true;

            for (var i = startingIndex; i < startingIndex + _length; i++)
                storedList.Add(humanNPC[npcID].Items[i]);

            NextTick(() => {
                CreatePageBtns(player, npcID, page, last, true, npcID);
            });

            for (var i = 0; i < 19; i++)
            {
                CuiHelper.DestroyUi(player, $"item{i}");
                CuiHelper.DestroyUi(player, $"wipeblock_cvr{i}");
            }

            return storedList;
        }

        private void OnUseNPC(BasePlayer npc, BasePlayer player)
        {
            if (humanNPC.ContainsKey($"{npc.userID}"))
            {
                var itemList = NPCShop_ItemOrder(player, $"{npc.userID}", 18, 0);
                NPCShop_MainCui(player, humanNPC[$"{npc.userID}"].ShopName);
                //ignore category
                ShowItems(player, itemList, storedCategories[0]);
            }
        }

        #endregion

        #endregion

        #region [MonoBehaviour]

        private void PlayerComponent(bool add, BasePlayer player = null)
        {
            if (player != null)
            {
                if (add)
                {
                    if (!_monoBehavior.ContainsKey(player))
                        _monoBehavior.Add(player, player.GetOrAddComponent<Mono>());
                }
                else
                {
                    var run = player.GetComponent<Mono>();
                    if (run != null)
                        UnityEngine.Object.Destroy(run);

                    if (_monoBehavior.ContainsKey(player))
                        _monoBehavior.Remove(player);
                }
                return;
            }
            if (add)
            {
                foreach (var _player in BasePlayer.activePlayerList)
                {
                    if (!_monoBehavior.ContainsKey(_player))
                        _monoBehavior.Add(_player, _player.GetOrAddComponent<Mono>());
                }
            }
            else
            {
                foreach (var _player in BasePlayer.activePlayerList)
                {
                    var run = _player.GetComponent<Mono>();
                    if (run != null)
                        UnityEngine.Object.Destroy(run);

                }
            }
        }

        private Dictionary<BasePlayer, Mono> _monoBehavior = new Dictionary<BasePlayer, Mono>();

        private void _RunNotif(BasePlayer player, float duration, int type, string[] properties = null)
        {
            var run = _monoBehavior[player];
            if (run == null) PlayerComponent(true, player);
            if (run != null) run.RunNotif(player, duration, type, properties);
        }

        private class Mono : FacepunchBehaviour
        {
            BasePlayer player;
            int itemsToShow;
            int index;
            string category;
            List<string> order;


            void Awake() => player = GetComponent<BasePlayer>();

            void RunSq()
            {

                if (itemsToShow <= 0)
                {
                    if (IsInvoking(nameof(RunSq)) == true)
                        CancelInvoke(nameof(RunSq));

                    itemsToShow = 0;
                    index = 0;
                    return;
                }

                try
                {
                    plugin.CreateItemCui(player, index, order[index], category);
                }
                catch
                {
                    plugin.Puts($" Item({order[index]}) listed in Category({category}) does not exist. You can only list items/commands which exists in data/Shop/Item.json or /Commands.json");
                }

                index++;
                itemsToShow--;
            }

            public void CreateItems(BasePlayer player, List<string> _order, string _category)
            {
                if (player == null) return;

                order = _order;
                category = _category;
                index = 0;
                itemsToShow = order.Count();

                if (IsInvoking(nameof(RunSq)) == true)
                {
                    CancelInvoke(nameof(RunSq));
                }
                InvokeRepeating(nameof(RunSq), plugin.config.gl.interval, plugin.config.gl.interval);
            }

            void DestroyNotif()
            {
                CuiHelper.DestroyUi(player, "btn_highlight");
                CuiHelper.DestroyUi(player, "btn_highlight_bg");
            }

            void DestroyLargeNotif()
            {
                CuiHelper.DestroyUi(player, "notif_panel");
                CuiHelper.DestroyUi(player, "notif_panel_color");
                CuiHelper.DestroyUi(player, "notif_text");
                CuiHelper.DestroyUi(player, "notif_icon");
            }

            public void RunNotif(BasePlayer player, float duration, int type, string[] properties)
            {
                if (player == null) return;

                if (type > 1)
                {
                    if (IsInvoking(nameof(DestroyLargeNotif)) == true)
                    {
                        CuiHelper.DestroyUi(player, "notif_panel");
                        CuiHelper.DestroyUi(player, "notif_panel_color");
                        CuiHelper.DestroyUi(player, "notif_text");
                        CuiHelper.DestroyUi(player, "notif_icon");
                        CancelInvoke(nameof(DestroyLargeNotif));
                    }
                    plugin.Notifs(player, type, properties);
                    Invoke(nameof(DestroyLargeNotif), duration);
                }
                else
                {
                    if (IsInvoking(nameof(DestroyNotif)) == true)
                    {
                        CuiHelper.DestroyUi(player, "btn_highlight");
                        CuiHelper.DestroyUi(player, "btn_highlight_bg");
                        CancelInvoke(nameof(DestroyNotif));
                    }
                    plugin.Notifs(player, type, properties);
                    Invoke(nameof(DestroyNotif), duration);
                }
            }
        }

        #endregion

        #region [CUI Class]

        public class CUIClass
        {
            public static CuiElementContainer CreateOverlay(string _name, string _color, string _anchorMin, string _anchorMax, bool _cursorOn = false, float _fade = 0f, string _mat = "")
            {
                var _element = new CuiElementContainer()
                {
                    {
                        new CuiPanel
                        {
                            Image = { Color = _color, Material = _mat, FadeIn = _fade},
                            RectTransform = { AnchorMin = _anchorMin, AnchorMax = _anchorMax },
                            CursorEnabled = _cursorOn
                        },
                        new CuiElement().Parent = "Overlay",
                        _name
                    }
                };
                return _element;
            }

            public static void CreatePanel(ref CuiElementContainer _container, string _name, string _parent, string _color, string _anchorMin, string _anchorMax, bool _cursorOn = false, float _fadeIn = 0f, float _fadeOut = 0f, string _mat2 = "", string _OffsetMin = "", string _OffsetMax = "", bool keyboard = false)
            {
                _container.Add(new CuiPanel
                {
                    Image = { Color = _color, Material = _mat2, FadeIn = _fadeIn },
                    RectTransform = { AnchorMin = _anchorMin, AnchorMax = _anchorMax, OffsetMin = _OffsetMin, OffsetMax = _OffsetMax },
                    FadeOut = _fadeOut,
                    CursorEnabled = _cursorOn,
                    KeyboardEnabled = keyboard
                },
                _parent,
                _name);
            }

            public static void CreateImage(ref CuiElementContainer _container, string _name, string _parent, string _image, string _anchorMin, string _anchorMax, float _fadeIn = 0f, float _fadeOut = 0f, string _OffsetMin = "", string _OffsetMax = "")
            {
                if (_image.StartsWith("http") || _image.StartsWith("www"))
                {
                    _container.Add(new CuiElement
                    {
                        Name = _name,
                        Parent = _parent,
                        FadeOut = _fadeOut,
                        Components =
                        {
                            new CuiRawImageComponent { Url = _image, Sprite = "assets/content/textures/generic/fulltransparent.tga", FadeIn = _fadeIn},
                            new CuiRectTransformComponent { AnchorMin = _anchorMin, AnchorMax = _anchorMax, OffsetMin = _OffsetMin, OffsetMax = _OffsetMax }
                        }

                    });
                }
                else
                {
                    _container.Add(new CuiElement
                    {
                        Parent = _parent,
                        Components =
                        {
                            new CuiRawImageComponent { Png = _image, Sprite = "assets/content/textures/generic/fulltransparent.tga", FadeIn = _fadeIn},
                            new CuiRectTransformComponent { AnchorMin = _anchorMin, AnchorMax = _anchorMax }
                        }
                    });
                }
            }

            public static void PullFromAssets(ref CuiElementContainer _container, string _name, string _parent, string _color, string _sprite, float _fadeIn = 0f, float _fadeOut = 0f, string _anchorMin = "0 0", string _anchorMax = "1 1", string _material = "assets/icons/iconmaterial.mat")
            {
                //assets/content/textures/generic/fulltransparent.tga MAT
                _container.Add(new CuiElement
                {
                    Parent = _parent,
                    Name = _name,
                    Components =
                            {
                                new CuiImageComponent { Material = _material, Sprite = _sprite, Color = _color, FadeIn = _fadeIn},
                                new CuiRectTransformComponent {AnchorMin = _anchorMin, AnchorMax = _anchorMax}
                            },
                    FadeOut = _fadeOut
                });
            }

            public static void CreateInput(ref CuiElementContainer _container, string _name, string _parent, string _color, int _size, string _anchorMin, string _anchorMax, string _defaultText, string _font = "permanentmarker.ttf", string _command = "command.processinput", TextAnchor _align = TextAnchor.MiddleCenter, int _charsLimit = 200)
            {
                _container.Add(new CuiElement
                {
                    Parent = _parent,
                    Name = _name,

                    Components =
                    {
                        new CuiInputFieldComponent
                        {

                            Text = _defaultText,
                            CharsLimit = _charsLimit,
                            Color = _color,
                            IsPassword = false,
                            Command = _command,
                            Font = _font,
                            FontSize = _size,
                            Align = _align
                        },

                        new CuiRectTransformComponent
                        {
                            AnchorMin = _anchorMin,
                            AnchorMax = _anchorMax

                        }

                    },
                });
            }

            public static void CreateText(ref CuiElementContainer _container, string _name, string _parent, string _color, string _text, int _size, string _anchorMin, string _anchorMax, TextAnchor _align = TextAnchor.MiddleCenter, string _font = "robotocondensed-bold.ttf", float _fadeIn = 0f, float _fadeOut = 0f, string _outlineColor = "0 0 0 0", string _outlineScale = "0 0")
            {
                _container.Add(new CuiElement
                {
                    Parent = _parent,
                    Name = _name,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = _text,
                            FontSize = _size,
                            Font = _font,
                            Align = _align,
                            Color = _color,
                            FadeIn = _fadeIn,
                        },

                        new CuiOutlineComponent
                        {

                            Color = _outlineColor,
                            Distance = _outlineScale

                        },

                        new CuiRectTransformComponent
                        {
                             AnchorMin = _anchorMin,
                             AnchorMax = _anchorMax
                        }
                    },
                    FadeOut = _fadeOut
                });
            }

            public static void CreateButton(ref CuiElementContainer _container, string _name, string _parent, string _color, string _text, int _size, string _anchorMin, string _anchorMax, string _command = "", string _close = "", string _textColor = "0.843 0.816 0.78 1", float _fade = 1f, TextAnchor _align = TextAnchor.MiddleCenter, string _font = "", string _material = "assets/content/ui/uibackgroundblur-ingamemenu.mat")
            {

                _container.Add(new CuiButton
                {
                    Button = { Close = _close, Command = _command, Color = _color, Material = _material, FadeIn = _fade },
                    RectTransform = { AnchorMin = _anchorMin, AnchorMax = _anchorMax },
                    Text = { Text = _text, FontSize = _size, Align = _align, Color = _textColor, Font = _font, FadeIn = _fade }
                },
                _parent,
                _name);
            }

            public static void GameIcon(ref CuiElementContainer _container, string _name, string _parent, int itemid, ulong skin, float _fadeIn = 0f, float _fadeOut = 0f, string _anchorMin = "0 0", string _anchorMax = "1 1", string _material = "assets/icons/iconmaterial.mat")
            {
                //assets/content/textures/generic/fulltransparent.tga MAT
                _container.Add(new CuiElement
                {
                    Parent = _parent,
                    Name = _name,
                    Components =
                            {
                                new CuiImageComponent { ItemId = itemid, SkinId = skin, FadeIn = _fadeIn}, 
                                new CuiRectTransformComponent {AnchorMin = _anchorMin, AnchorMax = _anchorMax}
                            },
                    FadeOut = _fadeOut
                });
            }
        }
        #endregion

        #region [Image Handling]

        //list for load order
        private List<string> imgList = new List<string>();

        private void DownloadImages()
        {
            if (ImageLibrary == null)
            { Puts($"ImageLibrary not found!"); return; }

            ImageLibrary.Call("AddImage", "https://rustplugins.net/products/shop/rustlogo.jpg", "https://rustplugins.net/products/shop/rustlogo.jpg");

            if (config.gl.forceImage != null) 
            {
                ImageLibrary.Call("AddImage", config.gl.forceImage, config.gl.forceImage);
            }

            //add item images
            foreach (string item in items.Keys)
            {
                if (items[item].Image == null || items[item].Image == "")
                    continue;

                if (items[item].Image.StartsWith("http") && !(bool)ImageLibrary.Call("HasImage", items[item].Image))
                {
                    ImageLibrary.Call("AddImage", items[item].Image, items[item].Image);
                }
            }


            //add category images
            foreach (string category in categories.Keys)
            {
                if (categories[category].Image == null) continue;

                if (!(bool)ImageLibrary.Call("HasImage", categories[category].Image))
                    ImageLibrary.Call("AddImage", categories[category].Image, categories[category].Image);

               
            }

            //add ui images
            ImageLibrary.Call("AddImage", config.us.main["Image"], config.us.main["Image"]); 
            ImageLibrary.Call("AddImage", config.us.side["Image"], config.us.side["Image"]); 
            ImageLibrary.Call("AddImage", config.us.logo["Image"], config.us.logo["Image"]); 

        }

        private string Img(string url)
        {   
            //img url been used as image names
            if (ImageLibrary != null)
            {
                if (!(bool)ImageLibrary.Call("HasImage", url))
                    return url;
                else
                    return (string)ImageLibrary?.Call("GetImage", url);
            }
            else
                return url;
        }

        #endregion

        #region [Category Data]

        private void SaveCategoryData()
        {
            if (categories != null)
                Interface.Oxide.DataFileSystem.WriteObject($"{Name}/Categories", categories);
        }

        private Dictionary<string, CategoryData> categories;

        private class CategoryData
        {
            public string Image;
            public string Permission;
            public float Sale;
            public List<string> Items = new List<string> { };
        }

        private void LoadCategoryData()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile($"{Name}/Categories"))
            {
                categories = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, CategoryData>>($"{Name}/Categories");


            }
            else
            {
                categories = new Dictionary<string, CategoryData>();

                SaveCategoryData();
                //CreateCategories();
            }
        }

        void CreateCategories()
        {
            string[] defaultCategories = {"Weapon", "Construction", "Items", "Resources", "Attire", "Tool", "Medical",
            "Food", "Ammunition", "Traps", "Misc", "Component", "Electrical", "Fun"};

            foreach (string category in defaultCategories)
            {
                categories.Add(category, new CategoryData());
            }


            var items = ItemManager.itemList;
            foreach (var item in items)
            {

                if (!categories.ContainsKey($"{item.category}"))
                    continue;

                categories[$"{item.category}"].Items.Add(item.shortname);
            }

            SaveCategoryData();
        }



        #endregion

        #region [Items Data]

        private void SaveItemData()
        {
            if (items != null)
                Interface.Oxide.DataFileSystem.WriteObject($"{Name}/Items", items);
        }

        private Dictionary<string, ItemsData> items;

        private class ItemsData
        {
            public string DisplayName;
            public ulong Skin;
            public string Image;
            public int DefaultAmount;
            public bool BlockAmountChange;
            public bool ShowDisplayName;
            public int BuyPrice;
            public int SellPrice;
            public string Currency;
        }

        private void LoadItemData()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile($"{Name}/Items"))
            {
                items = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, ItemsData>>($"{Name}/Items");
            }
            else
            {
                items = new Dictionary<string, ItemsData>();
                //CreateDefaultItems();
                SaveItemData();
            }
        }

        private void CreateDefaultItems()
        {
            string[] defaultCategories = {"Weapon", "Construction", "Items", "Resources", "Attire", "Tool", "Medical",
            "Food", "Ammunition", "Traps", "Misc", "Component", "Electrical", "Fun"};

            string[] ignore = { "workcart", "submarineduo", "submarinesolo", "hazmatsuit.spacesuit", "vehicle.chassis", "vehicle.module" };

            var _items = ItemManager.itemList;
            foreach (var item in _items)
            {
                if (ignore.Contains(item.shortname))
                    continue;

                if (!defaultCategories.Contains($"{item.category}"))
                    continue;

                items.Add(item.shortname, new ItemsData());
                items[$"{item.shortname}"].DisplayName = "default";
                items[$"{item.shortname}"].Skin = 0;
                items[$"{item.shortname}"].Image = "";
                items[$"{item.shortname}"].DefaultAmount = 1;
                items[$"{item.shortname}"].BuyPrice = 50;
                items[$"{item.shortname}"].SellPrice = 25;
                items[$"{item.shortname}"].Currency = "eco";
            }

            SaveCategoryData();
        }

        #endregion

        #region [Commands Data]

        private void SaveCommandData()
        {
            if (commands != null)
                Interface.Oxide.DataFileSystem.WriteObject($"{Name}/Commands", commands);
        }

        private Dictionary<string, CmdData> commands;

        private class CmdData
        {
            public string DisplayName;
            public string Image;
            public string Message;
            public string Command;
            public int BuyPrice;
            public string Currency;
            public bool ShowDisplayName;
        }

        private void LoadCommandData()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile($"{Name}/Commands"))
            {
                commands = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, CmdData>>($"{Name}/Commands");
            }
            else
            {
                commands = new Dictionary<string, CmdData>();

                commands.Add("YourCommandName", new CmdData());
                commands["YourCommandName"].DisplayName = "default";
                commands["YourCommandName"].Command = "chat.say {playername} or {steamid}";
                commands["YourCommandName"].Message = "You just bought command";
                commands["YourCommandName"].Image = $"https://wiki.rustclash.com/img/items180/water.salt.png";
                commands["YourCommandName"].BuyPrice = 50;
                commands["YourCommandName"].Currency = "eco";

                commands.Add("YourCommandName2", new CmdData());
                commands["YourCommandName2"].DisplayName = "default";
                commands["YourCommandName2"].Command = "chat.say {playername} or {steamid}";
                commands["YourCommandName2"].Message = "You just bought command";
                commands["YourCommandName2"].Image = $"https://wiki.rustclash.com/img/items180/water.salt.png";
                commands["YourCommandName2"].BuyPrice = 50;
                commands["YourCommandName2"].Currency = "eco";

                SaveCommandData();
            }
        }



        #endregion

        #region [Wipe Block Data]

        private void SaveWipeBlockData()
        {
            if (wipeBlock != null)
                Interface.Oxide.DataFileSystem.WriteObject($"{Name}/ItemsWipeBlock", wipeBlock);
        }

        private Dictionary<string, WipeBlock> wipeBlock;

        private class WipeBlock
        {
            public bool Enabled;
            public bool StartOnMapWipe;
            public long WipeTimeStamp;
            public Dictionary<string, long> Items = new Dictionary<string, long> { };
        }

        private void LoadWipeBlockData()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile($"{Name}/ItemsWipeBlock"))
            {
                wipeBlock = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, WipeBlock>>($"{Name}/ItemsWipeBlock");
            }
            else
            {
                wipeBlock = new Dictionary<string, WipeBlock>();
                wipeBlock.Add("Wipe Block", new WipeBlock());
                wipeBlock["Wipe Block"].Enabled = false;
                wipeBlock["Wipe Block"].StartOnMapWipe = true;
                wipeBlock["Wipe Block"].WipeTimeStamp = DateTimeOffset.Now.ToUnixTimeSeconds();
                wipeBlock["Wipe Block"].Items.Add("explosive.timed", 604800);
                wipeBlock["Wipe Block"].Items.Add("ammo.rocket.basic", 604800);
                wipeBlock["Wipe Block"].Items.Add("rocket.launcher", 604800);
                SaveWipeBlockData();
            }
        }



        #endregion

        #region [HumanNPC Data]

        private void SaveHumanNPCData()
        {
            if (humanNPC != null)
                Interface.Oxide.DataFileSystem.WriteObject($"{Name}/HumanNPC_Shops", humanNPC);
        }

        private Dictionary<string, NPC_Shop> humanNPC;

        private class NPC_Shop
        {
            public string ShopName;
            public List<string> Items = new List<string> { };
        }

        private void LoadHumanNPCData()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile($"{Name}/HumanNPC_Shops"))
            {
                humanNPC = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, NPC_Shop>>($"{Name}/HumanNPC_Shops");
            }
            else
            {
                humanNPC = new Dictionary<string, NPC_Shop>();
                SaveHumanNPCData();
            }
        }



        #endregion

        #region [Cooldown Data]

        private void SaveCdData()
        {
            if (cooldownData != null)
                Interface.Oxide.DataFileSystem.WriteObject($"{Name}/Cooldowns", cooldownData);
        }

        private Dictionary<string, long> cooldownData;



        private void LoadCdData()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile($"{Name}/Cooldowns"))
            {
                cooldownData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, long>>($"{Name}/Cooldowns");
            }
            else
            {
                cooldownData = new Dictionary<string, long>();

                CreateCdExamples();
                SaveCdData();
            }
        }

        private void CreateCdExamples()
        {
            cooldownData.Add("rifle.ak", 2500);
            cooldownData.Add("rifle.lr300", 2500);
        }

        #endregion

        #region [Player Data]

        private void SavePlayerData()
        {
            if (playerData != null)
                Interface.Oxide.DataFileSystem.WriteObject($"{Name}/PlayerData", playerData);
        }

        private Dictionary<ulong, PlayerData> playerData;

        private class PlayerData
        {
            public Dictionary<string, long> cooldowns = new Dictionary<string, long> { };
        }

        private void LoadPlayerData()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile($"{Name}/PlayerData"))
            {
                playerData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerData>>($"{Name}/PlayerData");
            }
            else
            {
                playerData = new Dictionary<ulong, PlayerData>();
                SavePlayerData();
            }
        }


        #endregion

        #region [Layouts]

        //layer, in case of addons it's parented to welcome panel
        string layer = "Overlay";

        //offset for item container
        private string[] container_Offset = {
                    "-230 -157", "420 200",
                };
        int fontAdjustment = 0;

        //anchors for item list
        string[] anchorSet = {
            "0.0 0.68/0.16 1", "0.165 0.68/0.325 1", "0.33 0.68/0.49 1", "0.495 0.68/0.655 1", "0.66 0.68/0.82 1", "0.825 0.68/0.985 1",
            "0.0 0.35/0.16 0.67", "0.165 0.35/0.325 0.67", "0.33 0.35/0.49 0.67", "0.495 0.35/0.655 0.67", "0.66 0.35/0.82 0.67", "0.825 0.35/0.985 0.67",
            "0.0 0.02/0.16 0.34", "0.165 0.02/0.325 0.34", "0.33 0.02/0.49 0.34", "0.495 0.02/0.655 0.34", "0.66 0.02/0.82 0.34", "0.825 0.02/0.985 0.34",
        };

        private void SelectLayout()
        {
            if (config._as.addon)
            {
                
                layer = "WelcomePanel_content";

                if (WelcomePanel.Version.Major > 3)
                    layer = "content";

                container_Offset = new string[] { config._as.offset["OffSetMin"], config._as.offset["OffSetMax"] };
            }
            else
                container_Offset = new string[] { "-230 -157", "425 168" };
        }

        void OnWelcomePanelPageOpen(BasePlayer player, int tab, int page, string addon)
        {   
            if(addon == null) return;

            if (addon.ToLower() == "shop")
            {   
                // 4.3.4 version
                layer = "wp_content";

                // 3.2 version
                if (WelcomePanel.Version.Major == 3)
                    layer = "WelcomePanel_content";

                // 4.0.9 version
                if (WelcomePanel.Version.Major == 4 && WelcomePanel.Version.Minor < 3)
                    layer = "content";

                ShowShop_API(player);
            }
        }


        #endregion

        #region [Config] 

        private Configuration config;
        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<Configuration>();
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = Configuration.CreateConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        class Configuration
        {
            [JsonProperty(PropertyName = "Main Settings")]
            public MS ms { get; set; }

            public class MS
            {
                [JsonProperty("» Require permission to open Shop")]
                public bool requirePerm { get; set; }

                [JsonProperty("» Block shop usage while building blocked")]
                public bool requireBuild { get; set; }

                [JsonProperty("» Block shop usage after fresh spawn (seconds)")]
                public float freshSpawnBlock { get; set; }

                [JsonProperty("» Require TC Auth. to use shop")]
                public bool requireTC { get; set; }

                [JsonProperty("» Shop Chat Command")]
                public string cmd { get; set; }

                [JsonProperty("» Pre-fill sell amount based on player's inventory")]
                public bool prefill { get; set; }

                [JsonProperty("» Check item skin when taking items from players")]
                public bool checkSkin { get; set; }

                [JsonProperty("» Enable purchase cooldowns (data/Shop/Cooldowns.json)")]
                public bool cooldowns { get; set; }

                [JsonProperty("» Discord Webhook for purchase logs")]
                public string webhook { get; set; }
            }

            [JsonProperty(PropertyName = "Global Sales")]
            public GS gs { get; set; }

            public class GS
            {
                [JsonProperty("» Permission Sales")]
                public Dictionary<string, float> perms { get; set; }

                [JsonProperty("» Item Sales")]
                public Dictionary<string, float> items { get; set; }
            }

            [JsonProperty(PropertyName = "Block Shop Usage When (NoEscape Umod)")]
            public CB cb { get; set; }

            public class CB
            {
                [JsonProperty("» Is Raid Blocked")]
                public bool raidBlock { get; set; }

                [JsonProperty("» Is Combat Blocked")]
                public bool combatBlock { get; set; }
            }

            [JsonProperty(PropertyName = "Addon Settings")]
            public AS _as { get; set; }

            public class AS
            {
                [JsonProperty("» Use Shop as WelcomePanel Addon")]
                public bool addon { get; set; }

                [JsonProperty("» Layout Container (only if auto detect is false)")]
                public Dictionary<string, string> offset { get; set; }
            }

            [JsonProperty(PropertyName = "Item List Displaying")]
            public GL gl { get; set; }

            public class GL
            {
                [JsonProperty("» Show items gradually")]
                public bool gradually { get; set; }

                [JsonProperty("» 'Tick' interval (in seconds)")]
                public float interval { get; set; }

                [JsonProperty("» FadeIn Effect")]
                public float fadeIn { get; set; }

                [JsonProperty("» Custom image for currency (only for game item type)")]
                public string forceImage { get; set; }

                [JsonProperty("» Item UI Settings")]
                public Dictionary<string, string> itemUI { get; set; }
            }

            [JsonProperty(PropertyName = "Notifications and Sounds")]
            public NS ns { get; set; }

            public class NS
            {
                [JsonProperty("» Enable Notifications")]
                public bool notifs { get; set; }

                [JsonProperty("» Duration (in seconds)")]
                public float duration { get; set; }

                [JsonProperty("» Enable Sounds")]
                public bool sounds { get; set; }

                [JsonProperty("» Enable on item Cui Effect")]
                public bool cuiFx { get; set; }

                [JsonProperty("» Success Notification")]
                public Dictionary<string, string> suc { get; set; }

                [JsonProperty("» Warning Notification")]
                public Dictionary<string, string> fail { get; set; }

                [JsonProperty("» Positions")]
                public Dictionary<string, string> pos { get; set; }

                [JsonProperty("» Messages")]
                public Dictionary<string, string> msgs { get; set; }
            }

            [JsonProperty(PropertyName = "UI Settings")]
            public US us { get; set; }

            public class US
            {
                [JsonProperty("» Background Color")]
                public string bgColor { get; set; }

                [JsonProperty("» Background Blurr Intensity")]
                public string bgBlurr { get; set; }

                [JsonProperty("» Main Panel")]
                public Dictionary<string, string> main { get; set; }

                [JsonProperty("» Side Panel")]
                public Dictionary<string, string> side { get; set; }

                [JsonProperty("» Side Buttons")]
                public Dictionary<string, string> buttons { get; set; }

                [JsonProperty("» Title")]
                public Dictionary<string, string> title { get; set; }

                [JsonProperty("» Logo (parented to title)")]
                public Dictionary<string, string> logo { get; set; }

                [JsonProperty("» Item Container")]
                public Dictionary<string, string> itemUiContainer { get; set; }

                [JsonProperty("» Layout Container")]
                public Dictionary<string, string> offset { get; set; }
            }

            public static Configuration CreateConfig()
            {
                return new Configuration
                {
                    ms = new Shop.Configuration.MS
                    {
                        requirePerm = false,
                        requireBuild = false,
                        freshSpawnBlock = 0f,
                        requireTC = false,
                        cmd = "shop",
                        prefill = false,
                        checkSkin = false,
                        cooldowns = false,
                        webhook = "",

                    },
                    gs = new Shop.Configuration.GS
                    {
                        perms = new Dictionary<string, float>
                        {
                            { "shop.sale30", 0.3f },
                            { "shop.sale75", 0.75f },
                        },
                        items = new Dictionary<string, float>
                        {
                            { "wood", 0.15f },
                            { "stones", 0.15f },
                            { "metal.fragments", 0.10f },
                            { "sulfur", 0.05f },
                        }
                    },
                    cb = new Shop.Configuration.CB
                    {
                        raidBlock = false,
                        combatBlock = false
                    },
                    _as = new Shop.Configuration.AS
                    {
                        addon = false,
                        offset = new Dictionary<string, string>
                        {
                            { "OffSetMin", "0 0" },
                            { "OffSetMax", "0 0" },
                            { "AnchorMin", "0 0.09" },
                            { "AnchorMax", "1.01 0.99" },
                        },
                    },
                    gl = new Shop.Configuration.GL
                    {
                        gradually = true,
                        interval = 0.03f,
                        fadeIn = 0.5f,
                        forceImage = null,
                        itemUI = new Dictionary<string, string>
                        {
                            { "Background Color", "0.115 0.115 0.115 0.8" },
                            { "Text Color", "1 1 1 0.6" },
                            { "Buy Button Color", "0.40 0.48 0.25 1" },
                            { "Sell Button Color", "0.16 0.34 0.49 1.0" },
                            { "Input Field Color", "0 0 0 0.6" }
                        },
                    },
                    ns = new Shop.Configuration.NS
                    {
                        notifs = true,
                        duration = 2.5f,
                        sounds = true,
                        cuiFx = true,
                        suc = new Dictionary<string, string>
                        {
                            { "Panel Color", "0.11 0.11 0.11 1" },
                            { "Accent Color", "0.40 0.48 0.25 1.0" },
                            { "Sound Prefab", "assets/prefabs/deployable/vendingmachine/effects/vending-machine-purchase-human.prefab" },
                        },
                        fail = new Dictionary<string, string>
                        {
                            { "Panel Color", "0.11 0.11 0.11 1" },
                            { "Accent Color", "0.56 0.20 0.15 1.0" },
                            { "Sound Prefab", "assets/prefabs/locks/keypad/effects/lock.code.denied.prefab" },
                        },
                        pos = new Dictionary<string, string>
                        {
                            { "OffSetMin", "-190 100" },
                            { "OffSetMax", "0 170" },
                            { "AnchorMin", "1 0.5" },
                            { "AnchorMax", "1 0.5" },
                        },
                        msgs = new Dictionary<string, string>
                        {
                            { "onPurchase", "<size=13><b>PURCHASE SUCCESSFUL</b></size>\n\nYour puchase of <b>{amount}</b>x <b>{item}</b> was successfull." },
                            { "noFunds", "<size=13><b>INSUFFICIENT FUNDS</b></size>\n\nYou don't have enough funds to purchase <b>{amount}</b>x <b>{item}</b>." },
                            { "onSale", "<size=13><b>ITEM SOLD</b></size>\n\nYou successfully sold <b>{amount}</b>x <b>{item}</b>." },
                            { "noItemToSell", "<size=13><b>MISSING ITEMS</b></size>\n\nYou don't have enough <b>{item}</b> to make this action." },
                            { "noEscape", "<size=13><b>SHOP ACCESS RESTRICTED</b></size>\n\nYou are either <b>combat blocked</b> or <b>raid blocked</b>. Please wait before you try to use shop." },
                            { "noPermission", "<size=13><b>MISSING PERMISSIONS</b></size>\n\nYou don't have permission to buy/sell in this category." },
                        },

                    },
                    us = new Shop.Configuration.US
                    {
                        bgColor = "0 0 0 0.6",
                        bgBlurr = "0.3",
                        main = new Dictionary<string, string>
                        {
                            { "Color", "0 0 0 0" },
                            { "Image", "https://rustplugins.net/products/welcomepanel/3/main.png" },
                            { "AnchorMin", "0.317 0.2" },
                            { "AnchorMax", "0.81 0.8" },
                        },
                        side = new Dictionary<string, string>
                        {
                            { "Color", "0 0 0 0" },
                            { "Image", "https://rustplugins.net/products/welcomepanel/3/side.png" },
                            { "AnchorMin", "0.190 0.2" },
                            { "AnchorMax", "0.33 0.8" },
                        },
                        buttons = new Dictionary<string, string>
                        { 
                            { "Text Size", "15" },
                            { "Text Color", "1 1 1 0.8" },
                            { "Button Color 1.", "0 0 0 0.4" },
                            { "Button Color 2.", "0 0 0 0" },
                            { "Active Button Color", "0.56 0.20 0.15 1.0" },
                            { "Page Button Color", "0.115 0.115 0.115 0.9" },
                            { "Close Button Color", "0.56 0.20 0.15 1.0" },
                            { "Category Buttons", "0.115 0.115 0.115 0.9"}
                        },
                        title = new Dictionary<string, string>
                        {
                            { "Text", "YOUR <b>SHOP TITLE</b>" },
                            { "Color", "0 0 0 0" },
                            { "AnchorMin", "0.190 0.74" },
                            { "AnchorMax", "0.81 0.802" },
                        },
                        logo = new Dictionary<string, string>
                        {
                            { "Image", "https://rustplugins.net/products/welcomepanel/3/rustlogo.png" },
                            { "AnchorMin", "0.013 0.25" },
                            { "AnchorMax", "0.04 0.76" },
                        },
                        itemUiContainer = new Dictionary<string, string>
                        {
                            { "OffSetMin", "-680 -360" },
                            { "OffSetMax", "680 360" },

                        },
                        offset = new Dictionary<string, string>
                        {
                            { "OffSetMin", "-680 -360" },
                            { "OffSetMax", "680 360" },
                            { "AnchorMin", "0.5 0.5" },
                            { "AnchorMax", "0.5 0.5" },
                        },

                    },
                };
            }
        }
        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["eco_symbol"] = "$",
                ["rp_symbol"] = "RP",
                ["buy_btn"] = "BUY",
                ["sell_btn"] = "SELL",
                ["eco_symbol"] = "$",
                ["balance_panel"] = "Your Balance: ",
                ["back_button"] = "BACK TO CATEGORIES",
            }, this);
        }

        string gl(string _message) => lang.GetMessage(_message, this);

        #endregion

        #region !Error Handling

        bool _ready = true;

        bool readyCheck(BasePlayer player)
        {
            if (!_ready)
            {
                if (player.IsAdmin)
                {
                    foreach (var category in categories.Keys)
                        CategoryNamesCheck(category);
                }
                else
                {
                    SendReply(player, "Shop is not ready for use. Please contact server admin.");
                }
                return false;
            }

            return true;
        }

        bool CategoryNamesCheck(string category)
        {
            if (category.Contains(" "))
            {
                Puts($"Please rename category '{category}' to '{category.Replace(' ', '_')}'");

                foreach (var player in BasePlayer.activePlayerList)
                {
                    if (player.IsAdmin)
                        SendReply(player, $"Please rename <color=#E73717>'{category}'</color> to <color=#7EBB14>'{category.Replace(' ', '_')}'</color>");

                    DestroyShop(player);
                }
                _ready = false;
                return false;
            }
            return true;
        }

        #endregion
    }
}