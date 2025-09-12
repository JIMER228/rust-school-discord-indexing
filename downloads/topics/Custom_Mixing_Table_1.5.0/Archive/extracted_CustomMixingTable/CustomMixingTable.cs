using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using Oxide.Game.Rust.Cui;
using Network;
using VLB;

namespace Oxide.Plugins
{
    [Info("CustomMixingTable", "David", "1.5.0")]
    public class CustomMixingTable : RustPlugin
    {
        #region [Hooks]

        private void OnServerInitialized()
        {
            LoadConfig();
            LoadData();
            LoadNameData();
            DownloadImages();
            ImageQueCheck();
            if (config.main.perm) permission.RegisterPermission($"{Name}.use", this);
            timer.Once(0.5f, () => { TableComponent(true); });
        }

        void Unload()
        {
            TableComponent(false);
            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyCui(player);
                CuiHelper.DestroyUi(player, "DrugMixing_info_main");
                CuiHelper.DestroyUi(player, "mixpanel_main");
            }
        }

        void OnEntitySpawned(MixingTable table) => TableComponent(true, table);

        void OnEntityKill(MixingTable table) => TableComponent(false, table);

        void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            if (container.entityOwner is MixingTable)
            {
                try
                {
                    var table = (MixingTable)container.entityOwner;
                    var run = _monoBehavior[table];
                    if (run == null) TableComponent(true, table);

                    string recipe = run.recipe;

                    if (recipe != null)
                    {
                        if (rcp.ContainsKey(recipe))
                        {
                            var tableContent = GetTableContent(table);
                            if (TryMix(tableContent, rcp[recipe].Ingredients))
                            {
                                foreach (var player in run.playerLooting)
                                    CreateMixingButton(player, table.net.ID.Value, 1, recipe);
                            }
                            else
                            {
                                foreach (var player in run.playerLooting)
                                    CreateMixingButton(player, table.net.ID.Value, 0, recipe);
                            }
                        }
                    }
                }
                catch
                {
                    //kek 
                }
            }
        }

        void OnItemRemovedFromContainer(ItemContainer container, Item item) => OnItemAddedToContainer(container, item);

        void OnMixingTableToggle(MixingTable table) => NextTick(() => { table.StopMixing(); });

        object CanLootEntity(BasePlayer player, MixingTable table)
        {
            if (config.main.perm && !permission.UserHasPermission(player.UserIDString, $"{Name}.use")) return false;
            table.inventory.canAcceptItem = (Item item, int amount) => { return true; };
            StartLooting(player, table.inventory, "generic_resizable");

            CreateBaseCui(player, $"{table.net.ID}");

            var run = _monoBehavior[table];
            if (run == null) TableComponent(true, table);
            if (run != null)
            {

                run.IsLooting(player, true);
                if (run.recipe != null && run.IsMixing())
                {
                    MixPanel(player, rcp[run.recipe].Name);
                    CreateMixingButton(player, table.net.ID.Value, 2, run.recipe);
                    ProgressBar_Base(player);
                    run.ShowMixingProgress();
                }
            }

            return true;
        }

        private void OnLootEntityEnd(BasePlayer player, MixingTable table)
        {
            var run = _monoBehavior[table];
            if (run == null) TableComponent(true, table);
            if (run != null)
            {

                run.IsLooting(player, false);
            }
            DestroyCui(player);
            CuiHelper.DestroyUi(player, "DrugMixing_info_main");
            CuiHelper.DestroyUi(player, "mixpanel_main");
        }

        #endregion

        #region [Functions]

        public class StoredContent
        {
            public string name;
            public string shortname;
            public ulong skin;
            public int amount;
        }

        private Dictionary<int, StoredContent> GetTableContent(MixingTable table)
        {
            Dictionary<int, StoredContent> content = new Dictionary<int, StoredContent>(); 

            foreach (var item in table.inventory.itemList)
            {
                bool foundDuplicateItem = false;
                foreach (KeyValuePair<int, StoredContent> item2 in content) 
                {
                    if (item2.Value.shortname == item.info.shortname && item2.Value.skin == item.skin)
                    {
                        item2.Value.amount = item2.Value.amount + item.amount;
                        foundDuplicateItem = true;
                    }

                }
                if (!foundDuplicateItem)
                {
                    content.Add(item.position, new StoredContent());
                    content[item.position].name = item.name;
                    content[item.position].shortname = item.info.shortname;
                    content[item.position].skin = item.skin;
                    content[item.position].amount = item.amount;
                }
            }


            return content;
        }

        private int GetMultiplier(Dictionary<int, StoredContent> content, Dictionary<string, int> recipe)
        {
            List<int> results = Facepunch.Pool.GetList<int>();
            foreach (string ingredient in recipe.Keys)
            {
                int amount = recipe[ingredient];
                if (IsDigitsOnly(ingredient))
                {
                    foreach (var item in content.Keys) //
                    {
                        if (content[item].skin == Convert.ToUInt64(ingredient))
                        {
                            int check = Convert.ToInt32(Math.Floor((double)content[item].amount / (double)amount));
                            results.Add(check);
                            //Puts($"{item} {check}");
                        }
                    }

                }
                else
                {
                    foreach (var item in content.Keys)
                    {
                        if (content[item].shortname == ingredient && content[item].skin == 0)
                        {
                            int check = Convert.ToInt32(Math.Floor((double)content[item].amount / (double)amount));
                            results.Add(check);
                            //Puts($"{item} {check}");
                        }
                    }
                }
            }
            int result = results.Min();
            Facepunch.Pool.FreeList(ref results);
            return result;
        }

        private bool TryMix(Dictionary<int, StoredContent> content, Dictionary<string, int> recipe, MixingTable table = null)
        {
            int multiplier = 1;
            if (table != null)
                multiplier = GetMultiplier(content, recipe);

            foreach (string ingredient in recipe.Keys)
            {
                int amount = recipe[ingredient] * multiplier;
                if (IsDigitsOnly(ingredient))
                {
                    //loop thru every item in mixing table and find correct ingredient
                    foreach (var item in content.Keys)
                    {
                        // if ingredient was already found exit loop
                        if (amount <= 0)
                            continue;

                        if (content[item].skin == Convert.ToUInt64(ingredient))
                        {
                            //if there is more than needed, deduct from table content
                            if (content[item].amount >= amount)
                            {
                                content[item].amount -= amount;
                                amount = 0;
                            }
                            else //if there is not enough, deduct from amount
                            {
                                amount -= content[item].amount;
                                content[item].amount = 0;
                            }
                        }
                    }

                    //if ingredients was not found at end of the loop
                    if (amount > 0)
                        return false;

                }
                else
                {
                    foreach (var item in content.Keys)
                    {
                        if (amount <= 0)
                            continue;

                        if (content[item].shortname == ingredient && content[item].skin == 0)
                        {
                            if (content[item].amount >= amount)
                            {
                                content[item].amount -= amount;
                                amount = 0;
                            }
                            else
                            {
                                amount -= content[item].amount;
                                content[item].amount = 0;
                            }
                        }
                    }

                    if (amount > 0)
                        return false;
                }
            }

            if (table != null)
            {
                var run = _monoBehavior[table];
                if (run == null) TableComponent(true, table);
                if (run != null) run.multiplier = multiplier;
                //Puts($"sending {multiplier}, run is {run.multiplier}");
            }

            return true;
        }

        void HandleContents(MixingTable table, string recipe)
        {
            var tableContent = GetTableContent(table);
            string shortname = recipe;

            if (TryMix(tableContent, rcp[recipe].Ingredients, table))
            {
                table.inventory.Clear();
                table.inventory.capacity = 12;
                foreach (var item in tableContent.Keys)
                {
                    if (tableContent[item].amount <= 0) continue;
                    var _item = ItemManager.CreateByName(tableContent[item].shortname, tableContent[item].amount, tableContent[item].skin);
                    if (tableContent[item].name != null)
                        _item.name = tableContent[item].name;
                    _item.MoveToContainer(table.inventory);
                }

                if (shortname.Contains("{"))
                {
                    string[] split = shortname.Split('{');
                    shortname = split[0];
                }

                int amount = 1;
                var run = _monoBehavior[table];
                if (run == null) TableComponent(true, table);
                if (run != null) amount = run.multiplier;

                var itemCrafted = ItemManager.CreateByName(shortname, amount, rcp[recipe].SkinID);
                itemCrafted.name = rcp[recipe].Name;
                itemCrafted.MoveToContainer(table.inventory);
                table.inventory.capacity = 6;
            }
        }

        void StartLooting(BasePlayer player, ItemContainer container, string panelName)
        {
            player.inventory.loot.PositionChecks = false;
            player.inventory.loot.entitySource = container.entityOwner;
            container.capacity = 6;
            //container.SetLocked(false);
            player.inventory.loot.AddContainer(container);
            player.inventory.loot.SendImmediate();

            player.ClientRPCPlayer(null, player, "RPC_OpenLootPanel", panelName);
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

        private void PlayFx(BasePlayer player, string fx)
        {
            if (!config.main.fxOn) return;
            if (player == null) return;
            var EffectInstance = new Effect();
            EffectInstance.Init(Effect.Type.Generic, player, 0, Vector3.up, Vector3.zero);
            EffectInstance.pooledstringid = StringPool.Get(fx);
            NetWrite netWrite = Net.sv.StartWrite();
            netWrite.PacketID(Message.Type.Effect);
            EffectInstance.WriteToStream(netWrite);
            netWrite.Send(new SendInfo(player.net.connection));
            EffectInstance.Clear(true);
        }

        private List<string> CreateRecipeOrder(BasePlayer player)
        {
            List<string> recipeOrder = new List<string>();
            //add available blueprints
            foreach (var item in rcp)
            {
                if (!string.IsNullOrEmpty(item.Value.Permission))
                {
                    
                    if (!player.IPlayer.HasPermission(item.Value.Permission))
                    {
                        continue;
                    }
                    
                }
                recipeOrder.Add(item.Key);
                
            }

            return recipeOrder;
        }

        #endregion

        #region [Console Commands]

        [ConsoleCommand("DrugMixing_cmd")]
        private void DrugMixing_cmd(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            var args = arg.Args;
            if (arg.Player() == null) return;
            if (args == null) return;

            if (args[0] == "select")
            {                    //shortname     //index                //selection highlight
                CreateRecipe(player, args[1], Convert.ToInt32(args[2]), args[3], true);
                PlayFx(player, config.main.fx["click"]);
                return;
            }
            if (args[0] == "info")
            {                    //shortname     
                CreateInfo(player, args[1]);
                PlayFx(player, config.main.fx["click"]);
                return;
            }

            if (args[0] == "pageup")
            {
                //if (Convert.ToInt32(args[3]) < 0) return;
                //page nullHit.HitPositionWorld, 1f, 163495f);
                ShowRcps(player, Convert.ToInt32(args[1]), args[2]);
                PlayFx(player, config.main.fx["click"]);
                return;
            }
            if (args[0] == "pagedown")
            {                      //page
                ShowRcps(player, Convert.ToInt32(args[1]), args[2]);
                PlayFx(player, config.main.fx["click"]);
                return;
            }
        }

        [ConsoleCommand("dm_set")]
        private void dm_set(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            var args = arg.Args;
            if (player == null) return;

            PlayFx(player, config.main.fx["set"]);

            var table = BaseNetworkable.serverEntities.Find(new NetworkableId(Convert.ToUInt32(args[0]))) as MixingTable;
            if (table is MixingTable)
            {
                var run = _monoBehavior[table];
                if (run == null) TableComponent(true, table);
                if (run != null)
                {

                    if (run.IsMixing()) return;

                    run.recipe = $"{args[1]}";
                    MixPanel(player, rcp[args[1]].Name);
                    OnItemAddedToContainer(table.inventory, null);
                }
            }
        }

        [ConsoleCommand("dm_startmixing")]
        private void dm_startmixing(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            var args = arg.Args;

            var table = BaseNetworkable.serverEntities.Find(new NetworkableId(Convert.ToUInt32(args[0]))) as MixingTable;
            if (table is MixingTable)
            {
                if (table.inventory.IsFull()) { SendReply(player, "Mixing table is full!"); return; }
                // if (!TryMix(tableContent, rcp[recipe].Ingredients)

                var run = _monoBehavior[table];
                if (run == null) TableComponent(true, table);
                if (run != null)
                {

                    run.StartMixing(args[1]);
                }
            }
        }

        [ConsoleCommand("dm_stopmixing")]
        private void dm_stopmixing(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            var args = arg.Args;

            var table = BaseNetworkable.serverEntities.Find(new NetworkableId(Convert.ToUInt32(args[0]))) as MixingTable;
            if (table is MixingTable)
            {
                var run = _monoBehavior[table];
                if (run == null) TableComponent(true, table);
                if (run != null)
                {

                    run.StopMixing();
                }
            }
        }

        #endregion

        #region [CUI]

        private void CreateBaseCui(BasePlayer player, string tableId)
        {
            string main = "DrugMixing_base";

            var DrugMixing_base = new CuiElementContainer();
            //offset
            CUIClass.CreatePanel(ref DrugMixing_base, main, "Overlay", "0.25 0.23 0.22 0", "0.5 0.0", "0.5 0.0", true, 0.1f, 0f, "assets/icons/iconmaterial.mat", "193 235", "573 550");
            CUIClass.CreateText(ref DrugMixing_base, main + "_title", main, "0.91 0.86 0.82 1", config.main.ui["title"], 21, "0.000 0.90", "1 1", TextAnchor.UpperLeft, $"robotocondensed-bold.ttf", 0.1f);
            //recipies
            CUIClass.CreatePanel(ref DrugMixing_base, main + "_recipies_title", main, "0.70 0.67 0.65 0.17", "0.0 0.845", "1 0.915", true, 0.1f, 0f, "assets/content/ui/uibackgroundblur.mat");
            CUIClass.CreateText(ref DrugMixing_base, main + "_recipies_title_text", main + "_recipies_title", "0.73 0.70 0.67 1", "Recipies", 13, "0.02 0.00", "1 1", TextAnchor.MiddleLeft, $"robotocondensed-bold.ttf", 0.1f);
            //recipies panel
            CUIClass.CreatePanel(ref DrugMixing_base, main + "_recipies_container", main, "0.70 0.67 0.65 0.00", "0 0.0", "1 0.83", true, 0.1f, 0f, "assets/icons/iconmaterial.mat");

            DestroyCui(player);
            CuiHelper.AddUi(player, DrugMixing_base);
            ShowRcps(player, 0, tableId);
        }

        private void DestroyCui(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "DrugMixing_base");
        }

        private string[] ba = {
            "indexing",
            "0.01 0.77-0.99 1",
            "0.01 0.52-0.99 0.755",
            "0.01 0.27-0.99 0.505",
            "0.01 0.02-0.99 0.255"

        };

        private void ShowRcps(BasePlayer player, int page, string tableId)
        {
            List<string> recipeOrder = CreateRecipeOrder(player);
            int index = 4 * page;
            //destroy recipies
            for (int i = 1; i < 5; i++)
                CuiHelper.DestroyUi(player, $"DrugMixing_recipe{i}");

            int totalItems = recipeOrder.Count() - 1;
            for (int i = 0; i < 4; i++)
            {
                if (totalItems - index < i)
                    break;

                CreateRecipe(player, recipeOrder[i + index], i + 1, tableId);
                
            }

            if (recipeOrder.Count() >= 5)
            {
                CreatePageBtns(player, page, tableId);
            }
            else
            {
                CuiHelper.DestroyUi(player, "craft_page_up");
                CuiHelper.DestroyUi(player, "craft_page_down");
                CuiHelper.DestroyUi(player, "bp_page_count");
            }

            if (page == 0)
                CuiHelper.DestroyUi(player, "dm_page_up");

            if (totalItems - index < 4)
                CuiHelper.DestroyUi(player, "dm_page_down");
        }

        private void CreatePageBtns(BasePlayer player, int currentPage, string tableId)
        {
            var DrugMixing_pageBtns = new CuiElementContainer();
            int pageup = currentPage - 1;
            int pagedown = 1 + currentPage;
            //Puts($"pageup {pageup}");
            CUIClass.CreateButton(ref DrugMixing_pageBtns, "dm_page_up", "DrugMixing_base_recipies_title", "0.80 0.25 0.16 0.0", "▲", 11, "0.86 0.16", $"0.89 0.84", $"DrugMixing_cmd pageup {pageup} {tableId}", "", "1 1 1 0.4", 0.1f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf", "assets/icons/iconmaterial.mat");
            CUIClass.CreateButton(ref DrugMixing_pageBtns, "dm_page_down", "DrugMixing_base_recipies_title", "0.80 0.25 0.16 0.0", "▼", 11, "0.89 0.16", $"0.94 0.84", $"DrugMixing_cmd pagedown {pagedown} {tableId}", "", "1 1 1 0.4", 0.1f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf", "assets/icons/iconmaterial.mat");//rBody.AddExplosionForce(Mathf.Min(10 * 650f, 163490f);
            CUIClass.CreateText(ref DrugMixing_pageBtns, "dm_page_count", "DrugMixing_base_recipies_title", "1 1 1 0.4", $"{currentPage + 1}", 11, "0.94 0.0", "0.98 1", TextAnchor.MiddleCenter, $"robotocondensed-regular.ttf", 0.1f);

            CuiHelper.DestroyUi(player, "dm_page_up");
            CuiHelper.DestroyUi(player, "dm_page_down");
            CuiHelper.DestroyUi(player, "dm_page_count");
            CuiHelper.AddUi(player, DrugMixing_pageBtns);
        }


        private void CreateRecipe(BasePlayer player, string shortName, int index, string tableId, bool selected = false) 
        {
            string main = "DrugMixing_base_recipies_container";
            string recipe = $"DrugMixing_recipe{index}";
            var DrugMixing_recipe = new CuiElementContainer();

            string[] splitA = ba[index].Split('-');

            string anchorMin = splitA[0];
            string anchorMax = splitA[1];

            //rustlabs image
            string img = $"{rcp[shortName].Image}";
            if (!img.StartsWith("http"))
                img = "https://rustlabs.com/img/items180/" + img;
            //resources
            string resource = "";
            foreach (string item in rcp[shortName].Ingredients.Keys)
            {

                if (IsDigitsOnly(item))
                {
                    if (customNames.ContainsKey(item))
                        resource = resource + $"{rcp[shortName].Ingredients[item]} {customNames[item]}, ";
                    else
                        resource = resource + $"{rcp[shortName].Ingredients[item]} {item}, ";
                }
                else
                {
                    var itemDef = ItemManager.FindItemDefinition(item);
                    if (itemDef == null)
                    {
                        SendReply(player, $" <color=#C2291D>!</color> '{item}' <color=#C2291D>is not correct shortname.</color>");
                        return;
                    }
                    string itemDisplayName = itemDef.displayName.translated;

                    if (customNames.ContainsKey(item))
                        itemDisplayName = customNames[item];

                    resource = resource + $"{rcp[shortName].Ingredients[item]} {itemDisplayName}, ";
                }
            }
            resource = resource.Remove(resource.Length - 2);

            if (selected)
            {
                CUIClass.CreateButton(ref DrugMixing_recipe, "selected_btn", recipe, "0.38 0.51 0.16 0.85", config.main.ui["btnText"], 12, "0.75 0.21", $"0.90 0.81", $"dm_set {tableId} {shortName}", "", "1 1 1 0.6", 0.1f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf", "assets/content/ui/uibackgroundblur.mat");
                CUIClass.PullFromAssets(ref DrugMixing_recipe, "dm_mix_icon", "selected_btn", "1 1 1 0.65", config.main.ui["btnAsset"], 0.1f, 0f, "0.14 0.34", "0.32 0.67");
                CUIClass.CreateButton(ref DrugMixing_recipe, "info_btn", recipe, "0.70 0.67 0.65 0.17", "", 11, "0.90 0.21", $"0.98 0.81", $"DrugMixing_cmd info {shortName}", "", "1 1 1 0.6", 0.1f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf", "assets/content/ui/uibackgroundblur.mat");
                //CUIClass.PullFromAssets(ref DrugMixing_recipe, "dm_mix_icon", "info_btn", "1 1 1 0.65", "assets/icons/menu_dots.png", 0.1f, 0f, "-0.1 0.15", "1.1 0.85");   
                CUIClass.CreateImage(ref DrugMixing_recipe, "dm_mix_icon", "info_btn", Img($"https://rustplugins.net/products/consumables/text.png"), "0.2 0.2", "0.8 0.8", 0.1f);

            }
            else
            {//data
                CUIClass.CreatePanel(ref DrugMixing_recipe, recipe, main, "0.70 0.67 0.65 0.07", anchorMin, anchorMax, false, 0.1f, 0f, "assets/content/ui/uibackgroundblur.mat");
                CUIClass.CreateImage(ref DrugMixing_recipe, "bp_image", recipe, Img($"{img}"), "0.03 0.12", "0.15 0.88", 0.1f);
                CUIClass.CreateText(ref DrugMixing_recipe, "bp_name", recipe, "0.78 0.74 0.71 1", $"{rcp[shortName].Name}", 17, "0.17 0.45", "0.80 0.79", TextAnchor.UpperLeft, $"robotocondensed-bold.ttf", 0.1f);
                CUIClass.CreateText(ref DrugMixing_recipe, "bp_resource", recipe, "1 1 1 0.4", resource, 11, "0.17 0.14", "0.80 0.42", TextAnchor.UpperLeft, $"robotocondensed-regular.ttf", 0.1f);
                CUIClass.PullFromAssets(ref DrugMixing_recipe, "dm_click_icon", recipe, "1 1 1 0.11", "assets/icons/exit.png", 0.1f, 0f, "0.85 0.15", "0.95 0.85");
                //select btn
                CUIClass.CreateButton(ref DrugMixing_recipe, "select_btn", recipe, "0 0 0 0", "", 11, "0 0", $"1 1", $"DrugMixing_cmd select {shortName} {index} {tableId}", "", "1 1 1 0.4", 1f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf", "assets/icons/iconmaterial.mat");

            }

            if (!selected)
                CuiHelper.DestroyUi(player, recipe);
            else
            {
                CuiHelper.DestroyUi(player, "selected_btn");
                CuiHelper.DestroyUi(player, "info_btn");
            }

            CuiHelper.DestroyUi(player, "DrugMixing_info_main");
            CuiHelper.AddUi(player, DrugMixing_recipe);
            CuiHelper.DestroyUi(player, "empty");
            return;
        }

        private void CreateInfo(BasePlayer player, string shortName)
        {
            //rustlabs image
            string img = $"{rcp[shortName].Image}";
            if (!img.StartsWith("http"))
                img = "https://rustlabs.com/img/items180/" + img;

            var DrugMixing_info = new CuiElementContainer();
            //offset
            CUIClass.CreatePanel(ref DrugMixing_info, "DrugMixing_info_main", "Overlay", "0.25 0.23 0.22 0", "0.5 0.0", "0.5 0.0", true, 0.1f, 0f, "assets/icons/iconmaterial.mat", "-195 394", "185 523");

            //title
            CUIClass.CreatePanel(ref DrugMixing_info, "info_title", "DrugMixing_info_main", "0.70 0.67 0.65 0.17", "0 0.83", "1 1.005", true, 0.1f, 0f, "assets/content/ui/uibackgroundblur.mat");
            CUIClass.CreateText(ref DrugMixing_info, "info_title_text", "info_title", "0.73 0.70 0.67 1", $"{rcp[shortName].Name}", 13, "0.02 0.00", "1 1", TextAnchor.MiddleLeft, $"robotocondensed-bold.ttf", 0.1f);
            //main title
            CUIClass.CreateText(ref DrugMixing_info, "DrugMixing_info_title", "info_title", "0.91 0.86 0.82 1", "RECIPE INFO", 21, "0.000 1", "1 2.15", TextAnchor.UpperLeft, $"robotocondensed-bold.ttf", 0.1f);

            //main
            CUIClass.CreatePanel(ref DrugMixing_info, "info_main", "DrugMixing_info_main", "0.70 0.67 0.65 0.07", "0.015 0.0", "0.985 0.795", true, 0.1f, 0f, "assets/content/ui/uibackgroundblur.mat");
            CUIClass.CreateText(ref DrugMixing_info, "info_main_text", "info_main", "1 1 1 0.6", $"{rcp[shortName].Description}", 9, "0.02 0.05", "0.36 0.95", TextAnchor.UpperLeft, $"robotocondensed-regular.ttf", 0.5f);
            CUIClass.CreateImage(ref DrugMixing_info, "info_main_image", "info_main", Img($"{rcp[shortName].Image}"), "0.735 0.05", "1 0.95", 0.5f);

            CUIClass.CreatePanel(ref DrugMixing_info, "info_main_stats", "info_main", "0.70 0.67 0.65 0.25", "0.375 0.05", "0.735 0.95", true, 0.1f, 0f, "assets/icons/iconmaterial.mat");
            CUIClass.CreateText(ref DrugMixing_info, "info_main_text", "info_main_stats", "1 1 1 0.6", $"{rcp[shortName].Stats}", 10, "0.03 0.00", "1 0.98", TextAnchor.UpperLeft, $"robotocondensed-regular.ttf", 0.5f);

            CuiHelper.DestroyUi(player, "DrugMixing_info_main");
            CuiHelper.AddUi(player, DrugMixing_info);
            CuiHelper.DestroyUi(player, "empty");
        }

        private void MixPanel(BasePlayer player, string recipeName)
        {
            var cnfrUI = new CuiElementContainer(); //CUIClass.CreateOverlay("empty", "0 0 0 0", "0 0", "0 0", false, 0.0f, "assets/icons/iconmaterial.mat"); //assets/content/ui/uibackgroundblur.mat
            CUIClass.CreatePanel(ref cnfrUI, "mixpanel_main", "Overlay", "0 0 0 0", "0.5 0.0", "0.5 0.0", false, 0.0f, 0f, "assets/icons/iconmaterial.mat", "193 17", "425 102");
            //title
            CUIClass.CreatePanel(ref cnfrUI, "mixpanel_title_panel", "mixpanel_main", "0.70 0.67 0.65 0.07", "0.0 0.75", "1 1", false, 0.0f, 0f, "assets/content/ui/uibackgroundblur.mat");
            CUIClass.CreateText(ref cnfrUI, "mixpanel_title_text", "mixpanel_title_panel", "0.91 0.86 0.82 1", $" {recipeName}", 12, "0.03 0.00", "1 1", TextAnchor.MiddleLeft, $"robotocondensed-regular.ttf", 0.0f);

            //content
            CUIClass.CreatePanel(ref cnfrUI, "mixpanel_content_panel", "mixpanel_main", "0.70 0.67 0.65 0.07", "0.025 0.0", "0.975 0.70", false, 0.0f, 0f, "assets/content/ui/uibackgroundblur.mat");
            CUIClass.CreateText(ref cnfrUI, "mixpanel_content_text", "mixpanel_content_panel", "0.91 0.86 0.82 0.75", $"Input ingredients from recipe and start mixing", 10, "0.42 0.00", "0.96 1", TextAnchor.MiddleCenter, $"robotocondensed-regular.ttf", 0.0f);

            CuiHelper.DestroyUi(player, "mixpanel_main");
            CuiHelper.AddUi(player, cnfrUI);
        }

        private void CreateMixingButton(BasePlayer player, ulong tableID, int type, string recipe = "syringe.medical{0}") // 0 = not enough, 1 = ready, 2 = stop  /0.70 0.67 0.65 0.07 /0.38 0.51 0.16 0.2
        {
            var mixBtn = new CuiElementContainer();
            if (type == 0)
            {
                CUIClass.CreatePanel(ref mixBtn, "mix_btn", "mixpanel_content_panel", "0.70 0.67 0.65 0.15", "0.03 0.15", "0.4 0.85", false, 0.0f, 0f, "assets/content/ui/uibackgroundblur.mat");
                CUIClass.CreateText(ref mixBtn, "mix_btn_text", "mix_btn", "0.91 0.86 0.82 0.75", $"NOT ENOUGH RESOURCES", 10, "0.00 0.00", "1 1", TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf", 0.0f);

            }
            if (type == 1)
            {
                CUIClass.CreateButton(ref mixBtn, "mix_btn", "mixpanel_content_panel", "0.38 0.51 0.16 0.8", "", 12, "0.03 0.15", "0.4 0.85", $"dm_startmixing {tableID} {recipe}", "", "1 1 1 0.7", 0.0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf", "assets/content/ui/uibackgroundblur.mat");
                CUIClass.CreateText(ref mixBtn, "mix_btn_text", "mix_btn", "0.64 0.74 0.44 1", $"START MIXING", 11, "0.42 0.00", "1 1", TextAnchor.MiddleLeft, $"robotocondensed-bold.ttf", 0.0f);
                CUIClass.PullFromAssets(ref mixBtn, "mix_btn_image", "mix_btn", "0.64 0.74 0.44 1", "assets/icons/power.png", 0.0f, 0f, "0.125001337 0.35", "0.33 0.65");

                //CUIClass.CreateImage(ref mixBtn, "mix_btn_image", "mix_btn", $"https://rustplugins.net/products/shop/flask.png", "0.09 0.25", "0.37 0.75", 0.0f);
            }

            if (type == 2)
            {
                CUIClass.CreateButton(ref mixBtn, "mix_btn", "mixpanel_content_panel", "0.56 0.20 0.15 0.8", "", 12, "0.03 0.15", "0.4 0.85", $"dm_stopmixing {tableID}", "", "1 1 1 0.7", 0.0f, TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf", "assets/content/ui/uibackgroundblur.mat");
                CUIClass.CreateText(ref mixBtn, "mix_btn_text", "mix_btn", "0.94 0.48 0.30 0.95", $"STOP MIXING", 11, "0.42 0.00", "1 1", TextAnchor.MiddleLeft, $"robotocondensed-bold.ttf", 0.0f);
                CUIClass.PullFromAssets(ref mixBtn, "mix_btn_image", "mix_btn", "0.94 0.48 0.30 0.95", "assets/icons/power.png", 0.0f, 0f, "0.125 0.35", "0.33 0.65");
            }
            CuiHelper.DestroyUi(player, "mix_btn");
            CuiHelper.AddUi(player, mixBtn);
        }


        private void ProgressBar_Base(BasePlayer player)
        {
            var pbBase = new CuiElementContainer();
            CUIClass.CreatePanel(ref pbBase, "prog_base", "mixpanel_content_panel", "0.40 0.48 0.25 1", "0.43 0.15", "0.97 0.85", false, 0.1f, 0f, "assets/content/ui/uibackgroundblur.mat");
            CuiHelper.DestroyUi(player, "prog_base");
            CuiHelper.AddUi(player, pbBase);
        }

        private void ProgressBar_Prog(BasePlayer player, float progress = 0.25f)
        {

            var pbProg = new CuiElementContainer();
            if (progress >= 1) progress = 1;

            CUIClass.CreatePanel(ref pbProg, "prog_prog", "prog_base", "0.38 0.58 0.16 1", "0 0", $"{progress} 1", false, 0.0f, 0f, "assets/content/ui/uibackgroundblur.mat");
            CUIClass.CreateText(ref pbProg, "prog_prog_text", "prog_base", "1 1 1 0.5", $"MIXING {progress * 100:0}%", 11, "0.0 0.00", "1 1", TextAnchor.MiddleCenter, $"robotocondensed-bold.ttf", 0.0f);

            CuiHelper.DestroyUi(player, "prog_prog");
            CuiHelper.DestroyUi(player, "prog_prog_text");
            CuiHelper.AddUi(player, pbProg);
        }

        #endregion

        #region [MonoBehaviour]

        static CustomMixingTable plugin;

        private void Init() => plugin = this;

        private void TableComponent(bool add, MixingTable table = null)
        {
            if (table != null)
            {
                if (add)
                {
                    if (!_monoBehavior.ContainsKey(table))
                        _monoBehavior.Add(table, table.GetOrAddComponent<TableBehavior>());
                    else
                        _monoBehavior[table] = table.GetOrAddComponent<TableBehavior>();
                }
                else
                {
                    _monoBehavior.Remove(table);
                    var run = table.GetComponent<TableBehavior>();
                    table.inventory.SetLocked(false);
                    table.inventory.capacity = 5;
                    if (run != null)
                        UnityEngine.Object.Destroy(run);
                }

            }
            if (add)
            {
                foreach (var entity in BaseNetworkable.serverEntities)
                {
                    if (entity is MixingTable)
                    {
                        var ent = entity as MixingTable;
                        if (ent == null) return;
                        if (!_monoBehavior.ContainsKey(ent))
                            _monoBehavior.Add(ent, ent.GetOrAddComponent<TableBehavior>());
                        else
                            _monoBehavior[ent] = ent.GetOrAddComponent<TableBehavior>();
                    }
                }
            }
            else
            {
                foreach (var entity in BaseNetworkable.serverEntities)
                {
                    if (entity is MixingTable)
                    {
                        var _entity = (MixingTable)entity;
                        var run = _entity.GetComponent<TableBehavior>();
                        _entity.inventory.SetLocked(false);
                        _entity.inventory.capacity = 5;
                        UnityEngine.Object.Destroy(run);
                    }
                }
            }
        }

        private Dictionary<MixingTable, TableBehavior> _monoBehavior = new Dictionary<MixingTable, TableBehavior>();
        
        private class TableBehavior : FacepunchBehaviour
        {
            MixingTable table;
            public List<BasePlayer> playerLooting = new List<BasePlayer>();
            public string recipe;
            public int multiplier;
            float progress;
            float updateFreq = 0.1f;


            void Awake() => table = GetComponent<MixingTable>();

            public bool IsMixing()
            {
                return IsInvoking(nameof(RunSQ));
            }

            float GetTickValue()
            {
                if (plugin.config.timers.specTimer.ContainsKey(recipe))
                    return 1 / (plugin.config.timers.specTimer[recipe] * 10);
                else
                    return 1 / (plugin.config.timers.defaultTimer * 10);
            }

            public void ShowMixingProgress()
            {
                if (IsInvoking(nameof(RunSQ)) == true)
                {
                    foreach (var player in playerLooting)
                    {
                        plugin.ProgressBar_Base(player);
                        plugin.CreateMixingButton(player, table.net.ID.Value, 2);
                    }
                }
            }

            public void IsLooting(BasePlayer player, bool looting)
            {
                if (looting)
                {
                    if (!playerLooting.Contains(player))
                        playerLooting.Add(player);
                }
                else
                {
                    if (playerLooting.Contains(player))
                        playerLooting.Remove(player);
                }
            }

            public void StartMixing(string _recipe)
            {
                recipe = _recipe; //plugin.Puts(recipe);
                progress = 0;
                foreach (var player in playerLooting)
                {
                    plugin.ProgressBar_Base(player);
                    plugin.CreateMixingButton(player, table.net.ID.Value, 2);
                }
                InvokeRepeating(nameof(RunSQ), 0.1f, updateFreq);
                table.inventory.SetLocked(true);
                Effect.server.Run(plugin.config.main.fx["mix"], (BaseEntity)table, 0U, Vector3.zero, Vector3.zero);
            }

            public void StopMixing()
            {
                if (IsInvoking(nameof(RunSQ)) == true)
                    CancelInvoke(nameof(RunSQ));

                table.inventory.SetLocked(false);


                foreach (var player in playerLooting)
                {
                    CuiHelper.DestroyUi(player, "prog_base");
                    plugin.CreateMixingButton(player, table.net.ID.Value, 1, recipe);
                }
            }

            private void RunSQ()
            {
                if (progress >= 1)
                {
                    if (IsInvoking(nameof(RunSQ)) == true)
                        CancelInvoke(nameof(RunSQ));

                    progress = 0;

                    try
                    {
                        Effect.server.Run(plugin.config.main.fx["finished"], (BaseEntity)table, 0U, Vector3.zero, Vector3.zero);
                    }
                    catch
                    {
                        //kek 
                    }

                    plugin.HandleContents(table, recipe);
                    //plugin.Puts($"{multiplier}");
                    plugin.NextTick(() => {
                        table.inventory.SetLocked(false);
                    });

                    foreach (var player in playerLooting)
                        CuiHelper.DestroyUi(player, "prog_base");

                    return;
                }
                progress += GetTickValue();
                foreach (var player in playerLooting)
                {
                    plugin.ProgressBar_Prog(player, progress);
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

            public static void CreatePanel(ref CuiElementContainer _container, string _name, string _parent, string _color, string _anchorMin, string _anchorMax, bool _cursorOn = false, float _fadeIn = 0f, float _fadeOut = 0f, string _mat2 = "", string _OffsetMin = "", string _OffsetMax = "")
            {
                _container.Add(new CuiPanel
                {
                    Image = { Color = _color, Material = _mat2, FadeIn = _fadeIn },
                    RectTransform = { AnchorMin = _anchorMin, AnchorMax = _anchorMax, OffsetMin = _OffsetMin, OffsetMax = _OffsetMax },
                    FadeOut = _fadeOut,
                    CursorEnabled = _cursorOn
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

            public static void CreateInput(ref CuiElementContainer _container, string _name, string _parent, string _color, int _size, string _anchorMin, string _anchorMax, string _font = "permanentmarker.ttf", string _command = "command.processinput", TextAnchor _align = TextAnchor.MiddleCenter)
            {
                _container.Add(new CuiElement
                {
                    Parent = _parent,
                    Name = _name,

                    Components =
                    {
                        new CuiInputFieldComponent
                        {

                            Text = "0",
                            CharsLimit = 250,
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
        }
        #endregion

        #region [Recipies Data]

        private void SaveData()
        {
            if (rcp != null)
                Interface.Oxide.DataFileSystem.WriteObject($"{Name}/Recipies", rcp);
        }

        private Dictionary<string, Recipe> rcp;

        private class Recipe
        {
            public string Name;
            public string Image;
            public ulong SkinID;
            public string Description;
            public string Stats;
            public string Permission;
            public Dictionary<string, int> Ingredients = new Dictionary<string, int> { };
        }

        private void LoadData()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile($"{Name}/Recipies"))
            {
                rcp = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, Recipe>>($"{Name}/Recipies");
                SaveData();
            }
            else
            {
                rcp = new Dictionary<string, Recipe>();

                CreateExamples();
                SaveData();
            }

            foreach (KeyValuePair<string, Recipe> item in rcp)
            {
                if (!string.IsNullOrEmpty(item.Value.Permission))
                {
                    permission.RegisterPermission(item.Value.Permission, this);
                }
            }
        }

        private void CreateExamples()
        {
            rcp.Add("antiradpills", new Recipe());
            rcp["antiradpills"].Name = "Use datafile included with plugin.";
            rcp["antiradpills"].Image = "https://rustlabs.com/img/items180/antiradpills.png";
            rcp["antiradpills"].Description = "test";
            rcp["antiradpills"].Ingredients.Add("blood", 750);
            rcp["antiradpills"].Ingredients.Add("metal.refined", 150);

            SaveData();
        }

        #endregion

        #region [Data]

        private void SaveNameData()
        {
            if (customNames != null)
                Interface.Oxide.DataFileSystem.WriteObject($"{Name}/Custom_Names(ingredients)", customNames);
        }

        private Dictionary<string, string> customNames;



        private void LoadNameData()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile($"{Name}/Custom_Names(ingredients)"))
            {
                customNames = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, string>>($"{Name}/Custom_Names(ingredients)");
            }
            else
            {
                customNames = new Dictionary<string, string>();
                customNames.Add("metal.fragments", "Metal");
                customNames.Add("metal.refined", "HQM");
                customNames.Add("2561876619", "Meth");
                SaveNameData();
            }
        }

        #endregion

        #region Localization

        Dictionary<string, string> resources = new Dictionary<string, string>();

        private void GetAllResources()
        {

        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(resources, this);
        }

        private string GetLang(string _message) => lang.GetMessage(_message, this);

        #endregion

        #region [Image Handling]

        [PluginReference] Plugin ImageLibrary;

        //list for load order
        private List<string> imgList = new List<string>();

        private void DownloadImages()
        {
            if (ImageLibrary == null)
            { Puts($"(! MISSING) ImageLibrary not found, image load speed will be significantly slower."); return; }


            string prefix = "https://rustlabs.com/img/items180/";
            foreach (string item in rcp.Keys)
            {
                if (!rcp[item].Image.StartsWith("http"))
                {
                    ImageLibrary.Call("AddImage", prefix + rcp[item].Image, prefix + rcp[item].Image);
                    if (!imgList.Contains(rcp[item].Image))
                        imgList.Add(prefix + rcp[item].Image);
                }
                else
                {
                    ImageLibrary.Call("AddImage", rcp[item].Image, rcp[item].Image);
                    if (!imgList.Contains(rcp[item].Image))
                        imgList.Add(rcp[item].Image);
                }
            }

            imgList.Add("https://rustplugins.net/products/consumables/text.png");
            ImageLibrary.Call("AddImage", "https://rustplugins.net/products/consumables/text.png", "https://rustplugins.net/products/consumables/text.png");


            //call load order
            ImageLibrary.Call("ImportImageList", "CustomMixingTable", imgList);

        }

        private void ImageQueCheck()
        {
            int imgCount = imgList.Count();
            int downloaded = 0;
            foreach (string img in imgList)
            {
                if ((bool)ImageLibrary.Call("HasImage", img))
                    downloaded++;
            }

            if (imgCount > downloaded)
                Puts($"(!) Stored Images ({downloaded}/{imgCount}). Reload ImageLibrary and then CustomMixingTable plugin to start download order.");

            if (imgCount == downloaded)
                Puts($"Stored Images ({downloaded}). All images has been successfully stored in image library.");
        }

        private string Img(string url)
        {   //img url been used as image names
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
            [JsonProperty(PropertyName = "Mixing Timers")]
            public Timers timers { get; set; }

            public class Timers
            {
                [JsonProperty("Global Timer")]
                public float defaultTimer { get; set; }

                [JsonProperty("Specific Timers")]
                public Dictionary<string, float> specTimer { get; set; }
            }

            [JsonProperty(PropertyName = "Plugin Settings")]
            public MainSet main { get; set; }

            public class MainSet
            {
                [JsonProperty("Sound Effects On")]
                public bool fxOn { get; set; }

                [JsonProperty("Permission required?")]
                public bool perm { get; set; }

                [JsonProperty("Access at (prefab)")]
                public string prefab { get; set; }

                [JsonProperty("Sound Effects")]
                public Dictionary<string, string> fx { get; set; }

                [JsonProperty("UI Settings")]
                public Dictionary<string, string> ui { get; set; }
            }

            public static Configuration CreateConfig()
            {
                return new Configuration
                {
                    timers = new CustomMixingTable.Configuration.Timers
                    {
                        defaultTimer = 5f,
                        specTimer = new Dictionary<string, float>
                        {
                            { "syringe.medical", 15f },
                            { "antiradpills", 7f },
                        },
                    },

                    main = new CustomMixingTable.Configuration.MainSet
                    {
                        perm = false,
                        fxOn = true,
                        prefab = "assets/prefabs/deployable/mixingtable/mixingtable.deployed.prefab",
                        fx = new Dictionary<string, string>
                        {
                            { "set", "assets/bundled/prefabs/fx/notice/item.select.fx.prefab" },
                            { "click", "assets/bundled/prefabs/fx/notice/loot.copy.fx.prefab" },
                            { "mix", "assets/prefabs/food/small water bottle/effects/water-bottle-fill-world.prefab" },
                            { "finished", "assets/prefabs/deployable/research table/effects/research-success.prefab" },

                        },
                        ui = new Dictionary<string, string>
                        {
                            { "title", "DRUG MIXING" },
                            { "btnText", "    MIX" },
                            { "btnAsset", "assets/icons/bleeding.png" },
                            { "mainWindow_offSetMin", "-200 0" },
                            { "mainWindow_offSetMax", "181 291" },
                            { "infoWindow_offSetMin", "193 148" },
                            { "infoWindow_offSetMax", "573 264" },
                        },
                    },
                };
            }
        }
        #endregion
    }
}


