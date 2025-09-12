using UnityEngine;
using System.Collections.Generic;
using Oxide.Core;
using System.Linq;
using Oxide.Game.Rust.Cui;
using System;
using Oxide.Core.Plugins;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("ZStickers", "JOSH-Z", "1.1.0")]
    [Description("Add collectable stickers that can be used while chatting. Stickers can be shown anywhere on your screen.")]

    class ZStickers : RustPlugin
    {
        // Planned:
        // - Better cleanup: remove deleted stickers from lists
        // - Add stickers from within UI

        // Researching:
        // - 

        // Changelog 1.1.0:
        // - Fixed Collection next/prev buttons
        // - Added Sticker token items - unwrap for a token
        // - Added Admin chat command /sticker spawntoken|st [amount]
        // - Added Improved sticker unlock preview
        // - Added Packages to unlock multiple stickers at once (buy 5 random instead of 1)
        // - Added Buy from specific packages

        // - Added unlock package tier collection option - only unlock stickers from one group of stickers
        // - Added unlock package tier permission option - only unlock if player has permission
        // - Fixed Economics plugin usage

        // - Improved returning to browser after creating trade offer for sticker
        // - Fixed error with removed stickers in RemoveInactive

        // - Added player sticker usage cooldown

        // Changelog 1.1.1:
        // - Patch for Rust update Oct 3


        #region Fields
        private static ZStickers ins;

        [PluginReference] private Plugin ImageLibrary, ZCoins, ServerRewards, Economics;

        const string permAdmin = "zstickers.admin";
        const string permTrade = "zstickers.trade";
        const string permStickers = "zstickers.use";
        const string permStickersTeam = "zstickers.teamchat";

        const string dataFileNameStickers = "ZStickers/stickers";
        const string dataFileNamePlayerStickers = "ZStickers/player_stickers";
        const string dataFileNameMarketData = "ZStickers/market_data";
        const string dataDirStickerPacks = "ZStickers/import"; // no slash on end

        const ulong zcoin_skin = 2811087215;
       //const ulong sticker_token_skin = 3104407217;

        const string UIPanelName = "zstickers_panel";
        const string UIPanelChatStickers = "zstickers_chatstickers";
        const string UIPanelStickerBrowser = "zstickers_stickerbrowser";
        const string UIPanelStickerTrader = "zstickers_stickertrader";
        const string UIPanelStickerUnlock = "zstickers_stickerunlock";

        Dictionary<BasePlayer, List<string>> UIUsers = new Dictionary<BasePlayer, List<string>>();
        Dictionary<BasePlayer, PlayerSettings> playerSettings = new Dictionary<BasePlayer, PlayerSettings>();
        Dictionary<BasePlayer, float> playerNextUnlockBroadcast = new Dictionary<BasePlayer, float>();
        Dictionary<BasePlayer, float> playerCooldowns = new Dictionary<BasePlayer, float>();

        const string icon_lock = "https://i.imgur.com/kwYZWr0.png";
        const string icon_lock2 = "https://i.imgur.com/Sa8zYaj.png";
        const string icon_lock_open = "https://i.imgur.com/rbfmhiV.png";
        const string icon_edit2 = "https://i.imgur.com/hFIst03.png";
        const string icon_eye = "https://i.imgur.com/USgI4Wl.png";

        const string effect_unwrap = "assets/prefabs/misc/xmas/presents/effects/unwrap.prefab";

        public Timer stickerTimeout = null;
        public bool DATA_SAVE_REQUIED = false;
        public bool DEBUG = false;
        #endregion

        #region Hooks
        void Init()
        {
            Debug.Log($"::g:: LOADED STICKERS");

            if (!LoadConfigVariables())
            {
                Debug.Log("Errors in config file, please validate the file and try again");
                return;
            }

            // make sure there's always one default unlock button
            if (configData.stickerUnlockPacks == null || configData.stickerUnlockPacks.Count == 0)
            {
                configData.stickerUnlockPacks.Add(new Tier());
                SaveConf();
            }

            permission.RegisterPermission(permAdmin, this);
            permission.RegisterPermission(permStickers, this);
            permission.RegisterPermission(permStickersTeam, this);
            permission.RegisterPermission(permTrade, this);
        }

        void OnServerInitialized()
        {
            ins = this;

            LoadUIImages();
            //RefreshAllStickerData();
            CleanupStickerTrades();
            RemoveInactive();
            UpdateStickerWeights();

            if (!configData.basicSettings.stickerTokenItemsEnabled)
                Unsubscribe(nameof(OnItemAction));
        }

        void Unload()
        {
            CloseAllUIs();
            SaveData();
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player == null)
                return;

            // keep track of player last seen if player has any unlocked stickers
            if (storedPlayerStickers.unlockedStickers.ContainsKey(player.UserIDString))
                storedPlayerStickers.playersLastSeen[player.UserIDString] = UnixTimeStampUTC();
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (player == null)
                return;

            // keep track of player last seen if player has any unlocked stickers
            if (storedPlayerStickers.unlockedStickers.ContainsKey(player.UserIDString))
                storedPlayerStickers.playersLastSeen[player.UserIDString] = UnixTimeStampUTC();
        }
       
        object OnPlayerChat(BasePlayer player, string message, ConVar.Chat.ChatChannel channel)
        {
            if (!permission.UserHasPermission(player.UserIDString, permAdmin) && !permission.UserHasPermission(player.UserIDString, permStickers))
                return null;

            if (channel == ConVar.Chat.ChatChannel.Cards)
                return null;

            if (channel != ConVar.Chat.ChatChannel.Global && !permission.UserHasPermission(player.UserIDString, permStickersTeam))
                return null;

            var first = message.IndexOf(":");
            var last = message.LastIndexOf(":");
            if (first != -1 && last != -1 && first != last)
            {
                if (!CanSendSticker(player))
                    return null;

                int ssF = message.IndexOf(":");
                int ssT = message.LastIndexOf(":") + 1;
                var tag = message.Substring(ssF, ssT - ssF);

                if (storedStickers.stickers.ContainsKey(tag))
                {
                    List<string> playerStickers;
                    if (!storedPlayerStickers.unlockedStickers.TryGetValue(player.UserIDString, out playerStickers))
                        return null;

                    if (playerStickers.Contains(tag))
                        ShowSticker(tag);

                    return null;
                }
            }
            return null;
        }

        bool CanSendSticker(BasePlayer player)
        {
            if (configData.basicSettings.showStickerCooldown <= 0)
                return true;
                
            float playerCooldown;
            if (playerCooldowns.TryGetValue(player, out playerCooldown))
            {
                var timeleft = Math.Ceiling(playerCooldown - Time.realtimeSinceStartup);
                if(timeleft > 0)
                {
                    player.ChatMessage($"Please wait {Math.Ceiling(timeleft)} seconds before sending another sticker");
                    return false;
                }
            }

            playerCooldowns[player] = Time.realtimeSinceStartup + configData.basicSettings.showStickerCooldown;
            return true;
        }

        object OnItemAction(Item item, string action, BasePlayer player)
        {
            if (configData.basicSettings.stickerTokenItemsEnabled && item.skin == configData.basicSettings.stickerTokenItemSkinID && action == "open" && item.info.shortname == "wrappedgift")
            {
                var amount = item.amount;

                item.Remove();
                Effect effect = new Effect(effect_unwrap, player.transform.position, Vector3.zero);
                if (effect != null) EffectNetwork.Send(effect);               
                AddPlayerTokens(player, amount);
            }

            return null;
        }
        #endregion

        #region ImageLibrary
        private void LoadUIImages()
        {
            if (ImageLibrary == null)
            {
                Debug.LogError("ImageLibray not found!");
                return;
            }

            Dictionary<string, string> newLoadOrder = new Dictionary<string, string>();

            newLoadOrder.Add("icon_lock_url", icon_lock);
            newLoadOrder.Add("icon_lock_url2", icon_lock2);
            newLoadOrder.Add("icon_lock_open_url", icon_lock_open);
            newLoadOrder.Add(configData.currencySettings.icon_currency, configData.currencySettings.icon_currency);
            newLoadOrder.Add("icon_edit_url2", icon_edit2);
            newLoadOrder.Add("icon_eye_url", icon_eye);

            foreach (var img in storedStickers.stickers)
                newLoadOrder.Add(img.Key, img.Value.DUI_URL);

            if (newLoadOrder.Count > 0)
                ImageLibrary.Call("ImportImageList", Title, newLoadOrder);
        }

        private void AddImage(string name, string url)
        {
            if (!(bool)ImageLibrary.Call("HasImage", name))
            {
                ImageLibrary.Call("AddImage", url, name);
            }
        }

        private string GetImage(string filename, ulong skinID = 0UL)
        {
            return ImageLibrary?.Call<string>("GetImage", filename, skinID, false);
        }
        #endregion

        #region Stickers
        void ShowStickerBadge(string stickerTag, BasePlayer player, string message)
        {
            var stickerData = GetStickerData(stickerTag);
            if (stickerData == null || stickerData.DUI_URL == "")
                return;

            var pad = 5f;
            var imgWH = 50f;
            var imgW = stickerData.DUI_HEIGHT > stickerData.DUI_WIDTH ? (stickerData.DUI_WIDTH / stickerData.DUI_HEIGHT) * imgWH : imgWH;
            var imgH = stickerData.DUI_WIDTH > stickerData.DUI_HEIGHT ? (stickerData.DUI_HEIGHT / stickerData.DUI_WIDTH) * imgWH : imgWH;

            var newPlayerUI = new CuiElementContainer();

            newPlayerUI.Add(CreateUIPanel($"0 1", $"0 1", HexToRGBA(color_dark, 0.7f), false, $"30 -120", $"270 -60"), "Overlay", UIPanelName);
            newPlayerUI.Add(CreateUIPanel("0 0", "0 1", "0 0 0 0", false, $"{pad} {pad}", $"{pad + imgWH} -10"), UIPanelName, "badge_panel_img");
            newPlayerUI.Add(CreateNewLabel(message, $"0 0", $"1 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", $"{pad + pad + imgWH} {pad}", $"{-pad} {-pad}", CuiFont.ROBOTO_REGULAR), UIPanelName);

            newPlayerUI.Add(new CuiElement
            {
                Name = CuiHelper.GetGuid(),
                Parent = "badge_panel_img",
                Components = {
                    new CuiRawImageComponent
                    {
                        Png = ImageLibrary.Call<string>("GetImage", stickerTag)
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
                        OffsetMin = $"{-imgW / 2} {-imgH / 2}", OffsetMax = $"{imgW / 2} {imgH / 2}"
                    }
                }
            });

            ResetStickerTimeout();
            List<BasePlayer> trgs = new List<BasePlayer>(BasePlayer.activePlayerList.ToList());
            if (trgs != null)
            {
                foreach (var target in trgs)
                {
                    if (target != null && target.IsConnected && !storedPlayerStickers.playerHideStickers.Contains(target.UserIDString))
                        AddUI(target, newPlayerUI, UIPanelName);
                }
            }
        }

        bool AddSticker(string tag, StickerData stickerData, bool hidden = false)
        {
            if (storedStickers.stickers.ContainsKey(tag))
                return false;

            if (hidden)
                stickerData.TIER = "hidden";

            storedStickers.stickers.Add(tag, stickerData);
            AddImage(tag, stickerData.DUI_URL);

            return true;
        }

        void ShowSticker(string stickerTag, StickerData presetPreset = null)
        {
            var preset = presetPreset ?? GetStickerData(stickerTag);
            if (preset == null || preset.DUI_URL == "")
            {
                Debug.Log($"::r:: sticker {stickerTag} not found");
                return;
            }

            var newPlayerUI = new CuiElementContainer();

            float pHor, pVer, oHorMin, oHorMax, oVerMin, oVerMax;
            GetUIPosition(preset, out pHor, out pVer, out oHorMin, out oHorMax, out oVerMin, out oVerMax);

            CuiPanel newPanel = CreateUIPanel($"{pHor} {pVer}", $"{pHor} {pVer}", "0 0 0 0", false, $"{oHorMin} {oVerMin}", $"{oHorMax} {oVerMax}");
            newPlayerUI.Add(newPanel, "Overlay", UIPanelName);

            newPlayerUI.Add(new CuiElement
            {
                Name = CuiHelper.GetGuid(),
                Parent = UIPanelName,
                Components = {
                    new CuiRawImageComponent
                    {
                        Png = ImageLibrary.Call<string>("GetImage", stickerTag)
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = "0 0", AnchorMax = "1 1"
                    }
                }
            });

            ResetStickerTimeout();

            List<BasePlayer> trgs = new List<BasePlayer>(BasePlayer.activePlayerList.ToList());
            if (trgs != null)
            {
                foreach (var target in trgs)
                {
                    if (target != null && target.IsConnected && !storedPlayerStickers.playerHideStickers.Contains(target.UserIDString))
                        AddUI(target, newPlayerUI, UIPanelName);
                }
            }
        }

        StickerData GetStickerData(string sticker)
        {
            StickerData preset;
            if (!storedStickers.stickers.TryGetValue(sticker, out preset))
                return null;

            return preset;
        }

        void ResetStickerTimeout()
        {
            if (stickerTimeout != null)
            {
                stickerTimeout.Destroy();
                stickerTimeout = null;
            }

            stickerTimeout = timer.Once(configData.basicSettings.stickerDisplayTime, () =>
            {
                CloseAllUIs(UIPanelName);
            });
        }

        Dictionary<StickerPair, int> AVAILABLE_STICKERS_WEIGHTED = new Dictionary<StickerPair, int>();

        class StickerPair
        {
            public string name;
            public StickerData stickerData;
        }
        Dictionary<StickerPair, int> GetAvailableStickers(bool forceUpdate = false)
        {
            if (AVAILABLE_STICKERS_WEIGHTED.Count == 0 || forceUpdate)
            {
                Dictionary<StickerPair, int> newStickers = new Dictionary<StickerPair, int>();
                foreach (var sticker in new Dictionary<string, StickerData>(storedStickers.stickers))
                {
                    if (sticker.Value.TIER == "hidden")
                        continue;

                    // skip if sticker is already in newStickers
                    if (newStickers.Any(x => x.Key.name == sticker.Key))
                        continue;

                    // no more max owners 0 allowed
                    if (sticker.Value.MAX_OWNERS <= 0)
                        continue;

                    if (sticker.Value.CURRENT_OWNERS >= sticker.Value.MAX_OWNERS)
                        continue;

                    var sticker_weight = 0;
                    sticker_weight = sticker.Value.MAX_OWNERS - sticker.Value.CURRENT_OWNERS;

                    newStickers.Add(new StickerPair() { name = sticker.Key, stickerData = sticker.Value }, sticker_weight);
                }

                AVAILABLE_STICKERS_WEIGHTED = newStickers;
            }

            return AVAILABLE_STICKERS_WEIGHTED;
        }

        void ImportStickerPack(string filename, bool hidden = false)
        {
            if (!Interface.Oxide.DataFileSystem.ExistsDatafile($"{dataDirStickerPacks}/{filename}"))
            {
                Debug.Log($"Sticker pack {filename} not found");
                return;
            }

            var stickerPack = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, StickerData>>($"{dataDirStickerPacks}/{filename}");
            if (stickerPack == null)
            {
                Debug.Log($"Corrupt JSON in sticker pack");
                return;
            }

            if (stickerPack.Count == 0)
            {
                Debug.Log($"Corrupt JSON in sticker pack");
                return;
            }

            Debug.Log($"Adding sticker pack '{filename}' with {stickerPack.Count} stickers...");

            List<string> added = new List<string>();
            List<string> failed = new List<string>();
            foreach (var sticker in new Dictionary<string, StickerData>(stickerPack))
            {
                if (AddSticker(sticker.Key, sticker.Value, hidden))
                {
                    added.Add(sticker.Key);
                }
                else
                {
                    failed.Add(sticker.Key);
                }
            }

            Debug.Log($"Finished adding sticker pack '{filename}'. {added.Count} stickers added, {failed.Count} stickers already existed.\n" +
                      $"Added:\n {(string.Join(" ", added))}\n" +
                      $"Failed:\n {(string.Join(" ", failed))}");
        }
        #endregion

        #region Tokens
        private int AddPlayerTokens(BasePlayer player, int amount, bool amountIsMax = false)
        {
            if (!storedPlayerStickers.unlockTokens.ContainsKey(player.UserIDString))
                storedPlayerStickers.unlockTokens.Add(player.UserIDString, 0);

            if (amountIsMax)
            {
                var balance = storedPlayerStickers.unlockTokens[player.UserIDString];
                if (balance < amount)
                {
                    var tokens = amount - balance;
                    storedPlayerStickers.unlockTokens[player.UserIDString] = amount;

                    Debug.Log($"AddPlayerTokens {player.displayName}, amount: {tokens}, new balance: {storedPlayerStickers.unlockTokens[player.UserIDString]}");
                    player.ChatMessage($"<color={colorGreen}>You received <color={colorRed}>{tokens}</color> sticker token{(tokens > 1 ? "s" : "")}</color>");
                }
            }
            else
            {
                storedPlayerStickers.unlockTokens[player.UserIDString] += amount;

                Debug.Log($"AddPlayerTokens {player.displayName}, amount: {amount}, new balance: {storedPlayerStickers.unlockTokens[player.UserIDString]}");
                player.ChatMessage($"<color={colorGreen}>You received <color={colorRed}>{amount}</color> sticker token{(amount > 1 ? "s" : "")}</color>");
            }

            return storedPlayerStickers.unlockTokens[player.UserIDString];
        }
        #endregion

        #region Unlock Stickers
        private bool UnlockPlayerSticker(BasePlayer player, string inputTier)
        {
            if (inputTier == "")
                return false;

            Tier tier = configData.stickerUnlockPacks.FirstOrDefault(x => x.name == inputTier);
            if (tier == null)
                return false;

            if (!storedPlayerStickers.unlockedStickers.ContainsKey(player.UserIDString))
                storedPlayerStickers.unlockedStickers.Add(player.UserIDString, new List<string>());

            var playerTokens = GetPlayerTokens(player);
            if (playerTokens < tier.price)
            {
                player.ChatMessage($"You don't have enough sticker tokens. You have {playerTokens}, required: {tier.price}");
                return false;
            }

            var pSet = GetPlayerSettings(player);
            if (pSet == null)
                return false;


            string unlockedString = "";

            // HERE WE FIND OUT WHAT STICKERS ARE AVAILABLE AND WE PICK A RANDOM WEIGHTED STICKER

            List<string> unlockedStickers;
            if (!GetRandomStickersForPlayer(player, tier.name, out unlockedStickers) || unlockedStickers.Count != tier.amount)
            {
                player.ChatMessage($"No stickers available to unlock");
                return false;
            }

            pSet.justUnlockedStickers.Clear();

            foreach (var unlockedSticker in unlockedStickers)
            {
                storedPlayerStickers.unlockedStickers[player.UserIDString].Add(unlockedSticker);
                storedStickers.stickers[unlockedSticker].CURRENT_OWNERS++;
                Debug.Log($"{player.displayName} unlocked {tier.name} sticker {unlockedSticker}");

                pSet.justUnlockedStickers.Add(unlockedSticker);
            }
            storedPlayerStickers.unlockTokens[player.UserIDString] -= tier.price;

            unlockedString = string.Join(",", unlockedStickers);

            if (configData.basicSettings.showUnlockNotification)
            {
                var message = $"<size=15><color={colorYellow}>{unlockedStickers.First()}</color></size>\nSticker unlocked by\n{player.displayName}";
                ShowStickerBadge(unlockedStickers.First(), player, message);
            }

            storedPlayerStickers.playersLastSeen[player.UserIDString] = UnixTimeStampUTC();

            if (!playerNextUnlockBroadcast.ContainsKey(player))
                playerNextUnlockBroadcast.Add(player, 0);

            if (playerNextUnlockBroadcast[player] < Time.realtimeSinceStartup)
            {
                playerNextUnlockBroadcast[player] = Time.realtimeSinceStartup + 7f;
                PrintToChat($"<size=11><color={GetTierColor(tier.name, 1, true)}><color={GetTierColor(tier.name, 0.5f, true)}>{player.displayName}</color> unlocked a new sticker in the <color=#ffffff>/sticker</color> menu</color></size>");
            }

            player.ChatMessage($"You unlocked sticker(s) {unlockedString}");

            UpdateStickerBrowser(player);
            DATA_SAVE_REQUIED = true;

            return true;
        }

        bool GetRandomStickersForPlayer(BasePlayer player, string inputTier, out List<string> stickernames)
        {
            //var possibleStickers = new Dictionary<string, StickerData>();
            stickernames = new List<string>();

            if (inputTier == "")
                return false;

            Tier tier = configData.stickerUnlockPacks.FirstOrDefault(x => x.name == inputTier);
            if (tier == null)
                return false;

            if (tier.permission != "" && !HasPermission(player.UserIDString, tier.permission))
                return false;

            List<string> playerStickers;
            if (!storedPlayerStickers.unlockedStickers.TryGetValue(player.UserIDString, out playerStickers))
                return false;

            var amount = tier.amount;
            var availableStickers = new Dictionary<StickerPair, int>(GetAvailableStickers());
            if (availableStickers.Count == 0) { player.ChatMessage($"No stickers available to unlock"); return false; }

            if(DEBUG) Debug.Log($"::p:: {availableStickers.Count} available stickers for player {player.displayName}:\n::p:: {string.Join(", ", availableStickers.Keys.ToList())}");

            var totalWeight = availableStickers.Values.Sum();

            for(var i = 0; i < amount; i++)
            {
                var weightLeft = UnityEngine.Random.Range(0, totalWeight - 1);
                if (DEBUG) Debug.Log($"::o:: Total weight all stickers: {totalWeight} - Weight left: {weightLeft}");

                foreach (var sticker in new Dictionary<StickerPair, int>(availableStickers))
                {
                    if (tier.collection != "" && sticker.Key.stickerData.COLLECTION != tier.collection)
                        continue;

                    weightLeft -= sticker.Value;
                    if (DEBUG) Debug.Log($"::o:: Sticker {sticker.Key} weight: {sticker.Value} - Weight left: {weightLeft}");

                    if (weightLeft <= 0)
                    {
                        stickernames.Add(sticker.Key.name);
                        if (DEBUG) Debug.Log($"::g:: Weight left <= 0, picked sticker {sticker.Key} to unlock");
                        break;
                    }
                }
            }

            return stickernames.Count == amount;
        }


        //bool GetRandomStickerForPlayer(BasePlayer player, string inputTier, out string stickername)
        //{
        //    //var possibleStickers = new Dictionary<string, StickerData>();
        //    stickername = "";

        //    if (inputTier == "")
        //        return false;

        //    Tier tier = configData.stickerUnlockPacks.FirstOrDefault(x => x.name == inputTier);
        //    if (tier == null)
        //        return false;

        //    List<string> playerStickers;
        //    if (!storedPlayerStickers.unlockedStickers.TryGetValue(player.UserIDString, out playerStickers))
        //        return false;

        //    #region old
        //    // //int totalStickers = 0;
        //    // int totalWeight = 0;          
        //    //// foreach (var sticker in new Dictionary<string, StickerData>(storedStickers.stickers))
        //    // foreach (var sticker in available)
        //    // {


        //    //     if (sticker.Value.TIER == "hidden")
        //    //         continue;

        //    //     //totalStickers++;

        //    //     //if (possibleStickers.ContainsKey(sticker.Key) || playerStickers.Contains(sticker.Key))
        //    //     //    continue;

        //    //     // we no longer check if the player already owns the sticker
        //    //     if (possibleStickers.ContainsKey(sticker.Key))
        //    //         continue;

        //    //     // will be removed later when tiers get removed
        //    //     if (tier.name != "" && tier.name != "random" && sticker.Value.TIER != tier.name)
        //    //         continue;

        //    //     if (sticker.Value.MAX_OWNERS > 0 && sticker.Value.CURRENT_OWNERS >= sticker.Value.MAX_OWNERS)
        //    //         continue;

        //    //     if (sticker.Value.MAX_OWNERS > 0)
        //    //         totalWeight += sticker.Value.MAX_OWNERS - sticker.Value.CURRENT_OWNERS;

        //    //     possibleStickers.Add(sticker.Key, sticker.Value);
        //    // }
        //    #endregion

        //    var availableStickers = new Dictionary<string, int>(GetAvailableStickers());
        //    var totalWeight = availableStickers.Values.Sum();
        //    var weightLeft = UnityEngine.Random.Range(0, totalWeight - 1);

        //    if (DEBUG)
        //    {
        //        Debug.Log($"::p:: {availableStickers.Count} available stickers for player {player.displayName}:\n::p:: {string.Join(", ", availableStickers.Keys.ToList())}");
        //        Debug.Log($"::o:: Total weight all stickers: {totalWeight} - Weight left: {weightLeft}");
        //    }

        //    if (availableStickers.Count == 0)
        //    {
        //        player.ChatMessage($"No stickers available to unlock");
        //        return false;
        //    }



        //    //foreach (var sticker in new Dictionary<string, StickerData>(possibleStickers))
        //    foreach (var sticker in new Dictionary<string, int>(availableStickers))
        //    {
        //        //var posMaxWeight = sticker.Value.MAX_OWNERS - sticker.Value.CURRENT_OWNERS;
        //        weightLeft -= sticker.Value;

        //        if (DEBUG)
        //            Debug.Log($"::o:: Sticker {sticker.Key} weight: {sticker.Value} - Weight left: {weightLeft}");

        //        if (weightLeft <= 0)
        //        {
        //            stickername = sticker.Key;

        //            if (DEBUG)
        //                Debug.Log($"::g:: Weight left <= 0, picked sticker {sticker.Key} to unlock");

        //            break;
        //        }
        //    }

        //    return true;
        //}

        void AdminLockSticker(BasePlayer player, string sticker)
        {
            if (!storedPlayerStickers.unlockedStickers.ContainsKey(player.UserIDString))
                return;

            List<string> playerStickers;
            if (!storedPlayerStickers.unlockedStickers.TryGetValue(player.UserIDString, out playerStickers))
                return;

            if (!playerStickers.Contains(sticker))
                return;

            playerStickers.Remove(sticker);
            storedStickers.stickers[sticker].CURRENT_OWNERS--;

            player.ChatMessage($"You locked sticker {sticker}");
        }

        void AdminUnlockSticker(BasePlayer player, string sticker)
        {
            if (!storedStickers.stickers.ContainsKey(sticker))
                return;

            if (!storedPlayerStickers.unlockedStickers.ContainsKey(player.UserIDString))
                storedPlayerStickers.unlockedStickers.Add(player.UserIDString, new List<string>());

            List<string> playerStickers;
            if (!storedPlayerStickers.unlockedStickers.TryGetValue(player.UserIDString, out playerStickers))
                return;

            playerStickers.Add(sticker);
            storedStickers.stickers[sticker].CURRENT_OWNERS++;

            player.ChatMessage($"You unlocked sticker {sticker}");
        }
        #endregion

        #region Sticker Trader
        private bool CreateStickerOffer(BasePlayer player, string stickerToSell)
        {
            List<string> playerStickers;
            if (!storedPlayerStickers.unlockedStickers.TryGetValue(player.UserIDString, out playerStickers))
                return false;

            if (!PlayerOwnsSticker(player, stickerToSell, true))
                return false;

            var pSet = GetPlayerSettings(player);
            if (pSet == null)
                return false;

            var stickerInfo = GetStickerData(stickerToSell);
            if (stickerInfo == null)
                return false;

            if (storedMarketData.stickerTrades.Any(x => x.sellerID == player.UserIDString && x.sticker == stickerToSell))
            {
                player.ChatMessage($"You already placed this sticker on the market");
                return false;
            }

            var tradeOffer = new StickerTrade()
            {
                sellerID = player.UserIDString,
                sellerName = player.displayName,
                created = DateTimeOffset.Now.ToUnixTimeSeconds(),
                price = pSet.inputTradeAmount,
                sticker = stickerToSell,
                tier = stickerInfo.TIER
            };

            storedMarketData.stickerTrades.Add(tradeOffer);

            var message = $"{player.displayName} is selling\n" +
                          $"<size=15><color={colorYellow}>{stickerToSell}</color></size>\n" +
                          $"for {pSet.inputTradeAmount} {configData.currencySettings.currencyName}";

            if (configData.basicSettings.showUnlockNotification)            
                ShowStickerBadge(stickerToSell, player, message);

            PrintToChat($"<size=12><color={colorYellow}><color={colorOrange}>{player.displayName}</color> is selling sticker <color={colorRed}>{stickerToSell}</color> on the <color={colorOrange}>/sticker</color> market for <color={colorRed}>{pSet.inputTradeAmount}</color> {configData.currencySettings.currencyName}</color></size>");
            player.ChatMessage($"You successfully placed sticker {stickerToSell} on the market for {pSet.inputTradeAmount} {configData.currencySettings.currencyName}");

            pSet.detailsSticker = "";
            pSet.tab = "browser";
            //pSet.itemListPage = 0;
            SaveData();
            UpdateStickerBrowser(player);

            return true;
        }

        void RemoveStickerOffer(BasePlayer player, string stickerToRemove)
        {
            if (!PlayerOwnsSticker(player, stickerToRemove, true))
                return;

            storedMarketData.stickerTrades.RemoveAll(x => x.sellerID == player.UserIDString && x.sticker == stickerToRemove);

            var pSet = GetPlayerSettings(player);
            if (pSet != null)
                pSet.detailsSticker = "";


            SaveData();
            UpdateStickerBrowser(player);

            player.ChatMessage($"You cancelled your trade offer, sticker is safu");
        }

        void BuyStickerTrade(BasePlayer player, string[] args)
        {
            var pSet = GetPlayerSettings(player);


            var ownerID = args[1];
            var stickerName = args[2];

            var trade = storedMarketData.stickerTrades.FirstOrDefault(x => x.sellerID == ownerID && x.sticker == stickerName);
            if (trade == null) { Debug.Log($"::r:: trade is null - {player.displayName} {player.UserIDString} - ownerID {ownerID} - sticker {stickerName}"); return; }

            if (trade.isSold) { Debug.Log($"::r:: trade.isSold - {player.displayName} {player.UserIDString} - ownerID {ownerID} - sticker {stickerName}"); return; }



            // check ownership seller
            var sellerUnlocked = GetUnlockedStickers(ownerID);
            if (!sellerUnlocked.Contains(stickerName)) { player.ChatMessage($"Something went wrong"); return; }


            var possibleSeller = FindPlayerByName(trade.sellerID);
            var currency = GetCurrency();


            // when using items as currency, the seller has to be connected in order to do a trade
            if (currency == StickerCurrency.Item)
            {
                if (possibleSeller == null || !possibleSeller.IsConnected)
                {
                    player.ChatMessage($"Can't trade sticker: The seller ({trade.sellerName}) is not online.");
                    return;
                }

                // check free slots
                if ((possibleSeller.inventory.containerMain.capacity - possibleSeller.inventory.containerMain.itemList.Count) +
                    (possibleSeller.inventory.containerBelt.capacity - possibleSeller.inventory.containerBelt.itemList.Count) < 1)
                {
                    player.ChatMessage($"Can't trade sticker: The seller ({trade.sellerName}) does not have enough inventory space.");
                    return;
                }
            }

            // check and take buyer currency
            if (PlayerCurrencyAmount(player) < trade.price || !TryTakeCurrency(player, trade.price))
            {
                player.ChatMessage($"You can't afford this!");
                UpdateStickerBrowser(player);
                return;
            }



            // try changing ownership
            if (!TradeStickerOwnership(trade.sellerID, player.UserIDString, stickerName))
            {
                player.ChatMessage($"You can't afford this!");
                Debug.Log($"::r::TradeStickerOwnership false - {player.displayName} {player.UserIDString} - ownerID {ownerID} - sticker {stickerName}");
                UpdateStickerBrowser(player);
                return;
            }


            // SOLD!

            trade.isSold = true;
            trade.buyerID = player.UserIDString;

            player.ChatMessage($"You successfully bought {trade.sellerName}'s sticker {stickerName} for {trade.price} {configData.currencySettings.currencyName}");


            if (!TryGiveCurrency(ulong.Parse(trade.sellerID), trade.price))
            {
                player.ChatMessage($"Something went terribly wrong while selling your sticker. Your sticker and your money is gone, contact an admin");
                Debug.Log($"::r:: Something went terribly wrong while selling your sticker. Your sticker and your money is gone, contact an admin - {player.displayName} {player.UserIDString} - ownerID {ownerID} - sticker {stickerName}");

                if (possibleSeller != null)
                    possibleSeller.ChatMessage($"Something went terribly wrong while selling your sticker. Your sticker and your money is gone, contact an admin");
            }
            else
            {
                Debug.Log($"::g:: Transfering {trade.price} {configData.currencySettings.currencyName} from {player.displayName} to {trade.sellerName} for selling sticker {stickerName}");
                if (possibleSeller != null)
                    possibleSeller.ChatMessage($"{player.displayName} bought your sticker {stickerName} for {trade.price} {configData.currencySettings.currencyName}");
            }

            storedMarketData.stickerTrades.RemoveAll(x => x.sellerID == ownerID && x.sticker == stickerName);
            SaveData();

            if (pSet != null)
            {
                pSet.tab = "stickertrader";
                pSet.detailsSticker = "";
                pSet.detailsStickerOwnerID = "";
            }
            UpdateStickerBrowser(player);
        }

        bool TradeStickerOwnership(string seller, string buyer, string stickerName)
        {
            List<string> sellerStickers;
            if (!storedPlayerStickers.unlockedStickers.TryGetValue(seller, out sellerStickers) || !sellerStickers.Contains(stickerName))
                return false;

            if (!storedPlayerStickers.unlockedStickers.ContainsKey(buyer))
                storedPlayerStickers.unlockedStickers.Add(buyer, new List<string>());

            List<string> buyerStickers;
            //if (!storedPlayerStickers.unlockedStickers.TryGetValue(buyer, out buyerStickers) || buyerStickers.Contains(stickerName))
            if (!storedPlayerStickers.unlockedStickers.TryGetValue(buyer, out buyerStickers))
                return false;

            sellerStickers.Remove(stickerName); // should remove only one
            buyerStickers.Add(stickerName);
            return true;
        }
        #endregion

        #region Currency / Payment
        enum StickerCurrency
        {
            Item,
            ZCoins,
            ServerRewards,
            Economics
        }
        StickerCurrency GetCurrency()
        {
            switch (configData.currencySettings.currency)
            {
                case 0: return StickerCurrency.Item;
                case 1: return StickerCurrency.ZCoins;
                case 2: return StickerCurrency.ServerRewards;
                case 3: return StickerCurrency.Economics;
            }

            return StickerCurrency.Item;
        }

        bool TryTakeCurrency(BasePlayer player, int amount)
        {
            var currency = GetCurrency();

            switch (currency)
            {
                case StickerCurrency.Item:
                    if (TryTakeItems(player, amount))
                        return true;

                    break;
                case StickerCurrency.ZCoins:
                    if (ZCoins == null)
                        return false;

                    if (ZCoins.Call<bool>("TakeCoins", player, amount, true))
                        return true;
                    break;
                case StickerCurrency.ServerRewards:
                    if (ServerRewards == null)
                        return false;

                    if (ServerRewards.Call<bool>("TakePoints", player.userID, amount))
                        return true;

                    break;
                case StickerCurrency.Economics:
                    if (Economics == null)
                        return false;

                    if (Economics.Call<bool>("Withdraw", player.userID, (double)amount))
                        return true;
                    break;
                default:
                    return false;
            }

            return false;
        }

        bool TryTakeItems(BasePlayer player, int amount)
        {
            var items = Facepunch.Pool.GetList<Item>();
            items.AddRange(player.inventory.containerMain.itemList.Where(x => x.info.shortname == configData.currencySettings.currencyItemName && x.skin == configData.currencySettings.currencySkinID));
            items.AddRange(player.inventory.containerBelt.itemList.Where(x => x.info.shortname == configData.currencySettings.currencyItemName && x.skin == configData.currencySettings.currencySkinID));
            var itemAmount = items.Sum(x => x.amount);

            if (itemAmount < amount)
                return false;

            var amountLeft = amount;
            foreach (var item in items)
            {
                if (amountLeft >= item.amount)
                {
                    amountLeft -= item.amount;
                    item.Remove();
                }
                else
                {
                    item.amount -= amountLeft;
                    item.MarkDirty();
                    amountLeft = 0;
                }

                if (amountLeft <= 0)
                    break;
            }

            return amountLeft <= 0;
        }

        bool TryGiveCurrency(ulong playerID, int amount)
        {
            var currency = GetCurrency();
            switch (currency)
            {
                case StickerCurrency.Item:
                    BasePlayer player = FindPlayerByName(playerID.ToString());
                    var item = ItemManager.CreateByName(configData.currencySettings.currencyItemName, amount, configData.currencySettings.currencySkinID);
                    if (player == null || item == null) return false;

                    if (!item.MoveToContainer(player.inventory.containerMain, -1, true, true) && item.MoveToContainer(player.inventory.containerBelt, -1, true, true))
                        item.Drop(player.inventory.containerMain.dropPosition, Vector3.zero, new Quaternion());
                    break;
                case StickerCurrency.ZCoins:
                    if (ZCoins == null || !ZCoins.Call<bool>("GiveOrScheduleCoins", playerID, amount))
                        return false;
                    break;
                case StickerCurrency.ServerRewards:
                    if (ServerRewards == null || !ServerRewards.Call<bool>("AddPoints", playerID, amount))
                        return false;
                    break;
                case StickerCurrency.Economics:
                    if (Economics == null || !Economics.Call<bool>("Deposit", playerID, amount))
                        return false;
                    break;
                default:
                    return false;
            }

            return true;
        }

        int PlayerCurrencyAmount(BasePlayer player)
        {
            var currency = GetCurrency();
            int amount = 0;
            switch (currency)
            {
                case StickerCurrency.Item:
                    foreach (var pc in player.inventory.containerMain.itemList.FindAll(x => x.skin == configData.currencySettings.currencySkinID && x.info.shortname == configData.currencySettings.currencyItemName).ToList())
                        amount += pc.amount;

                    foreach (var pc in player.inventory.containerBelt.itemList.FindAll(x => x.skin == configData.currencySettings.currencySkinID && x.info.shortname == configData.currencySettings.currencyItemName).ToList())
                        amount += pc.amount;

                    break;
                case StickerCurrency.ZCoins:
                    foreach (var pc in player.inventory.containerMain.itemList.FindAll(x => x.skin == zcoin_skin).ToList())
                        amount += pc.amount;

                    foreach (var pc in player.inventory.containerBelt.itemList.FindAll(x => x.skin == zcoin_skin).ToList())
                        amount += pc.amount;
                    break;
                case StickerCurrency.ServerRewards:
                    if (ServerRewards == null) return 0;
                    var obj = ServerRewards?.Call("CheckPoints", player.userID);
                    if (obj == null) return 0;
                    amount = (int)obj;
                    break;
                case StickerCurrency.Economics:
                    if (Economics == null) return 0;
                    var ecBalance = (double)Economics?.Call("Balance", player.userID);
                    amount = Convert.ToInt32(Math.Round(ecBalance));
                    break;
            }

            return amount;
        }
        #endregion

        #region Sticker tokens
        private int GetPlayerTokens(BasePlayer player)
        {
            int tokens;
            if (storedPlayerStickers.unlockTokens.TryGetValue(player.UserIDString, out tokens))
                return tokens;

            storedPlayerStickers.unlockTokens.Add(player.UserIDString, 0);
            return 0;
        }

        class TokenPack
        {
            public string name = "";
            public int price = 0;
            public int tokenAmount = 0;
        }

        #endregion

        #region Commands
        [ConsoleCommand("sticker")]
        private void consoleSticker(ConsoleSystem.Arg arg)
        {           
            var player = arg.Player();
            if (!arg.IsRcon && (player == null || !HasPermission(player.UserIDString, permAdmin)))
            {
                arg.ReplyWith("No permission");
                return;
            }

            if(arg.Args == null || arg.Args.Length == 0)
            {
                arg.ReplyWith("Invalid parameters");
                return;
            }

            var args = arg.Args;

            int amount;
            BasePlayer targetPlayer;

            switch(arg.Args[0])
            {
                case "givetokens":                   
                    if (arg.Args.Length < 3) { arg.ReplyWith($"Usage: sticker givetokens <player> <amount>"); return; }

                    targetPlayer = FindPlayerByName(args[1]);
                    if (targetPlayer == null) { arg.ReplyWith($"Player not found"); return; }
                    if (!int.TryParse(args[2], out amount) || amount <= 0) { arg.ReplyWith($"Invalid amount"); return; }

                    AddPlayerTokens(targetPlayer, amount);
                    arg.ReplyWith($"{amount} sticker tokens given to {targetPlayer.displayName}");                    
                    break;
                case "unlock":
                    if (args.Length < 3) { arg.ReplyWith($"Usage: sticker unlock <player> :sticker:"); return; }

                    targetPlayer = FindPlayerByName(args[1]);
                    if (targetPlayer == null) { arg.ReplyWith($"Player not found"); return; }
                    if (!storedStickers.stickers.ContainsKey(args[2])) { arg.ReplyWith($"Sticker {args[2]} not found"); return; }

                    AdminUnlockSticker(targetPlayer, args[2]);
                    arg.ReplyWith($"Unlocked sticker {args[2]} for {targetPlayer.displayName}");
                    break;
                case "import":
                    if (args.Length < 2) { arg.ReplyWith($"Usage: sticker import <pack file name> [hidden]"); return; }

                    ImportStickerPack(args[1], args.Length > 2 && args[2] == "hidden");
                    arg.ReplyWith($"Trying to add sticker pack {args[1]}, see RCON for results");
                    break;
            }
        }

        [ConsoleCommand("cmdsticker")]
        private void ccmdsticker(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            var args = arg.Args;
            if (args.Length > 0)
            {
                switch (args[0])
                {
                    case "buytokens":
                        TokenPack tokenPack = configData.currencySettings.tokenPacks.FirstOrDefault(x => x.name == args[1]);
                        if (tokenPack == null)
                            return;

                        if (PlayerCurrencyAmount(player) < tokenPack.price || !TryTakeCurrency(player, tokenPack.price))
                        {
                            UpdateStickerBrowser(player);
                            player.ChatMessage($"You can't afford this!");
                            return;
                        }
                        else
                        {
                            AddPlayerTokens(player, tokenPack.tokenAmount);
                            UpdateStickerBrowser(player);
                        }

                        break;
                    case "unlocksticker":
                        if (args.Length > 1)
                            UnlockPlayerSticker(player, args[1]);
                        break;
                    case "sellsticker":
                        if (args.Length > 1)
                            CreateStickerOffer(player, args[1]);
                        break;
                    case "cancelsell":
                        if (args.Length > 1)
                            RemoveStickerOffer(player, args[1]);
                        break;
                    case "buytrade":
                        if (args.Length > 2)
                            BuyStickerTrade(player, args);
                        break;
                    case "recyclestickers":
                        if (args.Length > 1)
                            RecycleStickers(player, args);
                        UpdateStickerBrowser(player);
                        break;
                    case "clearsales":
                        storedMarketData.stickerTrades.Clear();
                        SaveData();
                        player.ChatMessage("sales cleared");
                        break;
                    case "pset":
                        HandlePlayerSettings(player, args);
                        break;       
                    case "close":
                    case "exit":
                        CloseUI(player, UIPanelStickerBrowser);
                        CloseUI(player, UIPanelStickerUnlock);
                        break;



                }
            }
        }

        void HandlePlayerSettings(BasePlayer player, string[] args)
        {
            StickerData sticker;
            // zonec pset tab name
            if (args.Length < 2)
            {
                player.ChatMessage($"Error");
                return;
            }

            var pSet = GetPlayerSettings(player);
            if (pSet == null)
                return;

            switch (args[1])
            {
                case "listpage":
                    if (args.Length > 2)
                    {
                        if (args[2] == "prev")
                        {
                            pSet.itemListPage = pSet.itemListPage == 0 ? 0 : pSet.itemListPage - 1;
                        }
                        else if (args[2] == "next")
                        {
                            pSet.itemListPage = pSet.itemListPage += 1;
                        }
                        else if (args[2] == "set" && args.Length > 3)
                        {
                            pSet.itemListPage = int.Parse(args[3]);
                        }

                        UpdateStickerBrowser(player);
                    }
                    break;
                case "stickerdetails":
                    // stickercmd pset stickerdetails <sticker> [owner_player_id]
                    if (args.Length > 2 && args[2] == "close")
                    {
                        pSet.detailsSticker = "";
                        pSet.detailsStickerOwnerID = "";
                    }
                    else if (args.Length > 2)
                    {
                        sticker = GetStickerData(args[2]);
                        if (sticker == null) return;

                        pSet.detailsSticker = args[2];

                        if (args.Length > 3)
                            pSet.detailsStickerOwnerID = args[3];
                    }

                    UpdateStickerBrowser(player);
                    break;
                case "settradeamount":
                    if (args.Length > 2)
                    {
                        int oldAmount = pSet.inputTradeAmount;
                        int newAmount;
                        if (int.TryParse(args[2], out newAmount) && newAmount >= 1)
                        {
                            pSet.inputTradeAmount = newAmount;
                            UpdateStickerBrowser(player);
                        }
                        else
                        {
                            pSet.inputTradeAmount = oldAmount;
                            UpdateStickerBrowser(player);
                        }
                    }
                    break;
                case "tab":
                    if (args.Length > 2)
                    {
                        pSet.detailsSticker = "";
                        pSet.detailsStickerOwnerID = "";
                        pSet.itemListPage = 0;
                        pSet.tradeFilter = "";
                        pSet.filterShow = "";
                        pSet.filterSearch = "";
                        //pSet.filterCollection = "";
                        pSet.filter = "all";
                        pSet.itemListPage = 0;

                        pSet.tab = args[2];
                        pSet.justUnlockedStickers.Clear();
                        UpdateStickerBrowser(player);
                    }
                    break;
                case "filtercollection":
                    if (args.Length > 2)
                    {
                        pSet.filterCollection = args[2];
                        pSet.itemListPage = 0;

                        UpdateStickerBrowser(player);
                    }
                    break;
                case "filter":
                    if (args.Length > 2)
                    {
                        if (args[2] == "all" || args[2] == "rare" || args[2] == "common" || args[2] == "epic" || args[2] == "legendary" || args[2] == "hidden")
                            pSet.filter = args[2];

                        pSet.itemListPage = 0;

                        UpdateStickerBrowser(player);
                    }
                    break;
                case "filtershow":
                    if (args.Length > 2)
                    {
                        var filterVal = args[2] == "locked" || args[2] == "unlocked" ? args[2] : "all";
                        pSet.filterShow = filterVal;
                        pSet.itemListPage = 0;
                        UpdateStickerBrowser(player);
                    }
                    break;
                case "filtersearch":
                    pSet.filterSearch = args.Length > 2 ? args[2] : "";
                    pSet.itemListPage = 0;
                    //pSet.detailsSticker = "";
                    UpdateStickerBrowser(player);
                    break;
                case "sort":
                    if (args.Length > 2)
                    {
                        var filterVal = args[2] == "date" || args[2] == "name" || args[2] == "collection" || args[2] == "availability" || args[2] == "rarity" ? args[2] : "date";
                        pSet.sortOrder = filterVal;
                        pSet.itemListPage = 0;
                        UpdateStickerBrowser(player);
                    }
                    break;
                case "tier":
                    if (!HasPermission(player.UserIDString, permAdmin)) return;

                    StickerData stickerOptions;
                    if (args.Length > 2 && pSet.editSticker != "" && ins.storedStickers.stickers.TryGetValue(pSet.editSticker, out stickerOptions))
                    {
                        switch (args[2])
                        {
                            case "common": stickerOptions.TIER = "common"; break;
                            case "rare": stickerOptions.TIER = "rare"; break;
                            case "epic": stickerOptions.TIER = "epic"; break;
                            case "legendary": stickerOptions.TIER = "legendary"; break;
                            case "hidden": stickerOptions.TIER = "hidden"; break;
                        }

                        ins.storedStickers.stickers[pSet.editSticker].TIER = stickerOptions.TIER;
                        UpdateStickerBrowser(player);
                    }
                    return;
                case "setmaxowners":
                    if (!HasPermission(player.UserIDString, permAdmin)) return;

                    if (args.Length > 2 && pSet.editSticker != "" && ins.storedStickers.stickers.TryGetValue(pSet.editSticker, out sticker))
                    {
                        int oldAmount = sticker.MAX_OWNERS;
                        int newAmount;
                        if (int.TryParse(args[2], out newAmount) && newAmount >= 0)
                            sticker.MAX_OWNERS = newAmount;

                        UpdateStickerBrowser(player);
                    }

                    break;
                case "unlockpopup":
                    if (!HasPermission(player.UserIDString, permAdmin)) return;

                    // cmdsticker pset unlockpopup test
                    if (args.Length > 2)
                    {
                        if (args[2] == "test")
                        {
                            if(pSet.justUnlockedStickers.Count > 9)
                            {
                                pSet.justUnlockedStickers.Clear();
                                player.ChatMessage($"Fake unlocked sticker cleared");
                            }
                            else
                            {
                                var stickerTags = ins.storedStickers.stickers.Keys.ToList();
                                var tag = stickerTags.GetRandom();
                                pSet.justUnlockedStickers.Add(tag);
                                player.ChatMessage($"Fake unlocked sticker {tag}");
                            }

                            UpdateStickerBrowser(player);
                        }
                        else if (args[2] == "close")
                        {
                            pSet.justUnlockedStickers.Clear();
                            UpdateStickerBrowser(player);
                        }
                    }
                    break;
                case "grouppicker":                  

                    if (args.Length > 2)
                    {
                        if (args[2] == "prev")
                        {
                            pSet.groupPickerPage = pSet.groupPickerPage <= 0 ? pSet.groupPickerTotalPages - 1 : pSet.groupPickerPage - 1;
                        }
                        else if (args[2] == "next")
                        {
                            pSet.groupPickerPage = pSet.groupPickerPage >= pSet.groupPickerTotalPages - 1 ? 0 : pSet.groupPickerPage + 1;
                        }
                    }
                    else
                    {
                        // toggle sticker editor group picker
                        if (!HasPermission(player.UserIDString, permAdmin)) return;

                        pSet.showGroupPicker = !pSet.showGroupPicker;
                        pSet.groupPickerPage = 0;
                    }
                    UpdateStickerBrowser(player);
                    break;
                case "setcollection":
                    if (!HasPermission(player.UserIDString, permAdmin)) return;

                    if (args.Length > 2 && pSet.editSticker != "" && ins.storedStickers.stickers.TryGetValue(pSet.editSticker, out sticker))
                    {
                        var collection = args[2];
                        if (collection.Length < 3 && collection != "none") { player.ChatMessage($"Name is too short"); return; }
                        if (collection == "none")
                            collection = "";

                        sticker.COLLECTION = collection;
                        pSet.showGroupPicker = false;

                        if (!pSet.availableCollections.Contains(collection))
                            pSet.availableCollections = GetAvailableCollections(player);

                        UpdateStickerBrowser(player);
                    }
                    break;
                case "stopediting":
                    if (!HasPermission(player.UserIDString, permAdmin)) return;

                    pSet.editSticker = "";
                    pSet.tab = "browser";
                    pSet.askConfirmDelete = false;
                    pSet.groupPickerPage = 0;
                    UpdateStickerBrowser(player);

                    CloseUI(player, UIPanelName);

                    break;
                case "editsticker":
                    if (!HasPermission(player.UserIDString, permAdmin)) return;
                    if (args.Length < 3) return;

                    sticker = GetStickerData(args[2]);
                    if (sticker == null) return;

                    ins.storedStickers.stickerData = sticker;

                    UpdateStaticUI(player);

                    pSet.editSticker = args[2];
                    pSet.tab = "edit_sticker";
                    UpdateStickerBrowser(player);

                    player.ChatMessage($"Editing sticker {args[2]}");
                    break;
                case "removesticker":
                    if (!HasPermission(player.UserIDString, permAdmin)) return;
                    if (args.Length > 2)
                    {
                        // cmdsticker pset removesticker :isr:
                        if (storedStickers.stickers.ContainsKey(args[2]))
                            storedStickers.stickers.Remove(args[2]);
                    }
                    else
                    {
                        if (pSet.editSticker == "")
                            return;

                        // cmdsticker pset removesticker  >> from ui comes without args[2] (sticker name)
                        if (!pSet.askConfirmDelete)
                        {
                            pSet.askConfirmDelete = true;
                        }
                        else
                        {
                            if (storedStickers.stickers.ContainsKey(pSet.editSticker))
                                storedStickers.stickers.Remove(pSet.editSticker);

                            pSet.editSticker = "";
                            pSet.tab = "browser";
                            pSet.askConfirmDelete = false;
                        }
                    }
                    UpdateStickerBrowser(player);
                    break;
                case "canceldelete":
                    pSet.askConfirmDelete = false;
                    UpdateStickerBrowser(player);
                    break;
                case "lock":
                    if (!HasPermission(player.UserIDString, permAdmin)) return;
                    AdminLockSticker(player, args[2]);
                    UpdateStickerBrowser(player);
                    break;
                case "unlock":
                    if (!HasPermission(player.UserIDString, permAdmin)) return;
                    AdminUnlockSticker(player, args[2]);
                    UpdateStickerBrowser(player);
                    break;
                case "sendview":
                    if (HasPermission(player.UserIDString, permAdmin))
                    {
                        ShowSticker(args[2]);
                    }
                    else
                    {
                        if (!playerNextUnlockBroadcast.ContainsKey(player))
                            playerNextUnlockBroadcast.Add(player, 0);

                        if (playerNextUnlockBroadcast[player] < UnityEngine.Time.realtimeSinceStartup)
                        {
                            playerNextUnlockBroadcast[player] = UnityEngine.Time.realtimeSinceStartup + 5f;
                            // ShowSticker(args[2]);
                            player.SendConsoleCommand($"chat.say \"{args[2]}\"");
                        }
                        else
                        {
                            player.ChatMessage($"Please don't spam the stickers!");
                        }
                    }
                    UpdateStickerBrowser(player);
                    break;
                case "setstickerpos":

                    // cmdsticker pset setstickerpos topleft
                    if (args.Length < 3) return;

                    sticker = GetStickerData(pSet.editSticker);
                    if (sticker == null) return;

                    if (new string[] { "topleft", "topcenter", "topright", "middleleft", "middlecenter", "middleright", "bottomleft", "bottomcenter", "bottomright" }.Contains(args[2]))
                    {
                        ins.storedStickers.stickers[pSet.editSticker].POS = args[2].ToLower();

                        UpdateStaticUI(player);
                        UpdateStickerBrowser(player);
                    }
                    break;
                case "setstickeroption":
                    if (!HasPermission(player.UserIDString, permAdmin)) return;
                    if (args.Length < 4) return;

                    // cmdsticker pset setstickeroption l 100

                    var curoptions = GetStickerData(pSet.editSticker);
                    if (curoptions == null) return;

                    float value;
                    if (!float.TryParse(args[3], out value))
                        return;

                    switch (args[2])
                    {
                        case "w":
                            ins.storedStickers.stickers[pSet.editSticker].DUI_WIDTH = value;
                            break;
                        case "h":
                            ins.storedStickers.stickers[pSet.editSticker].DUI_HEIGHT = value;
                            break;
                        case "hor":
                            ins.storedStickers.stickers[pSet.editSticker].LEFT = value;
                            break;
                        case "ver":
                            ins.storedStickers.stickers[pSet.editSticker].TOP = value;
                            break;

                    }

                    UpdateStaticUI(player);
                    UpdateStickerBrowser(player);

                    break;
            }
        }

        [ChatCommand("sticker")]
        private void cmdsticker(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player.UserIDString, permStickers) && !HasPermission(player.UserIDString, permAdmin))
                return;


            if (args.Length == 0)
            {
                UpdateStickerBrowser(player);
                return;
            }

            var target = player;
            int amount = 1;
            if (args.Length > 0)
            {
                switch (args[0])
                {
                    case "list":
                        PrintUnlockedStickers(player);
                        break;
                    case "browse":
                        UpdateStickerBrowser(player);
                        return;
                    case "hide":
                        if (!ins.storedPlayerStickers.playerHideStickers.Contains(player.UserIDString))
                            ins.storedPlayerStickers.playerHideStickers.Add(player.UserIDString);
                        player.ChatMessage($"Disabled chat stickers - to undo type /sticker show");
                        break;
                    case "show":
                        if (ins.storedPlayerStickers.playerHideStickers.Contains(player.UserIDString))
                            ins.storedPlayerStickers.playerHideStickers.Remove(player.UserIDString);
                        player.ChatMessage($"Enabled chat stickers - to hide type /sticker hide");
                        break;
                    case "give":
                        if (!HasPermission(player.UserIDString, permAdmin))
                            return;

                        if (args.Length < 3) { player.ChatMessage($"Usage: /sticker give <name> <amount>"); return; }

                        int pAm;
                        if (int.TryParse(args[2], out pAm))
                            amount = pAm;

                        if (args[1] == "all")
                        {
                            PrintToChat($"Some bad ass admin made sure everyone has at least {amount} sticker tokens to use in /sticker");
                            foreach (var pl in BasePlayer.activePlayerList)
                            {
                                AddPlayerTokens(pl, amount, true);
                            }
                        }
                        else
                        {
                            target = FindPlayerByName(args[1]);
                            if (target == null) { player.ChatMessage($"Player not found"); return; }

                            AddPlayerTokens(target, amount);
                            player.ChatMessage($"{amount} sticker tokens given to {target.displayName}");
                        }

                        break;
                    case "clearallmystickers":  // this removes all your stickers
                        if (!HasPermission(player.UserIDString, permAdmin))
                            return;

                        if (storedPlayerStickers.unlockedStickers.ContainsKey(player.UserIDString))
                            storedPlayerStickers.unlockedStickers.Remove(player.UserIDString);

                        if (storedPlayerStickers.unlockTokens.ContainsKey(player.UserIDString))
                            storedPlayerStickers.unlockTokens.Remove(player.UserIDString);

                        RefreshAllStickerData(true);
                        player.ChatMessage($"Cleared all your stickers");
                        break;
                    case "config":
                    case "reloadconfig":
                        if (!HasPermission(player.UserIDString, permAdmin))
                            return;

                        try
                        {
                            configData = Config.ReadObject<ConfigData>();
                            SaveConf();
                            player.ChatMessage($"Config reloaded");
                        }
                        catch
                        {
                            player.ChatMessage($"Config could not be loaded");
                            return;
                        }

                        break;                    
                    case "unlock":
                        // /sticker unlock josh :ily:
                        if (!HasPermission(player.UserIDString, permAdmin))
                            return;

                        if (args.Length < 3) return;

                        var targetPlayer = FindPlayerByName(args[1]);
                        if (targetPlayer == null) { player.ChatMessage($"Player not found"); return; }

                        AdminUnlockSticker(targetPlayer, args[2]);

                        player.ChatMessage($"Unlocked {args[2]} for {targetPlayer.displayName}");

                        return;
                    case "validate":
                        if (!HasPermission(player.UserIDString, permAdmin))
                            return;

                        ValidateOwnedStickers();
                        break;
                    case "update":
                        if (!HasPermission(player.UserIDString, permAdmin))
                            return;

                        RefreshAllStickerData(true);
                        player.ChatMessage($"Updating ownership amounts, check RCON for info");
                        break;
                    case "cleanuptrades":
                        CleanupStickerTrades();
                        player.ChatMessage($"Cleaning up trades, check rcon log");
                        break;
                    case "debug":
                        if (!HasPermission(player.UserIDString, permAdmin))
                            return;

                        DEBUG = !DEBUG;
                        player.ChatMessage($"Debug mode toggled to: {DEBUG}");
                        break;
                    case "spawntoken":
                    case "st":
                        if (!HasPermission(player.UserIDString, permAdmin))
                            return;

                        int tAm;
                        var tAmount = args.Length > 1 && int.TryParse(args[1], out tAm) ? tAm : 1;
                        var item = ItemManager.CreateByName("wrappedgift", tAmount, configData.basicSettings.stickerTokenItemSkinID);
                        item.name = "Sticker Token";
                        player.GiveItem(item);
                        player.ChatMessage($"Spawned {tAmount} sticker tokens");
                        break;
                    case "stats":
                        if (!HasPermission(player.UserIDString, permAdmin))
                            return;

                        var allTotal = 0;
                        var allAvailable = 0;
                        foreach (var st in storedStickers.stickers)
                        {
                            allTotal += st.Value.MAX_OWNERS;

                            if (st.Value.MAX_OWNERS > 0)
                                allAvailable += st.Value.MAX_OWNERS - st.Value.CURRENT_OWNERS;
                        }
                        var msg = $"Sticker stats:\n" +
                            $"Unique stickers: {storedStickers.stickers.Count}\n" +
                            $"Available stickers: {allAvailable}\n" +
                            $"Stickers on market: {allTotal}\n";

                        Debug.Log(msg);
                        player.ConsoleMessage(msg);
                        player.ChatMessage($"Printed statistics to your consoles.");
                        break;
                    case "add":
                    case "addurl":
                    case "url":
                        if (!HasPermission(player.UserIDString, permAdmin))
                            return;

                        // /sticker addurl :name: <url> [overwrite]
                        if (args.Length < 3) return;

                        if (args[1].Contains("http://") || args[1].Contains("https://") || !args[2].Contains("http"))
                        {
                            player.ChatMessage($"Wrong order! /sticker add :name: <url>");
                            return;
                        }

                        if (!args[1].StartsWith(":"))
                            args[1] = ":" + args[1];

                        if (!args[1].EndsWith(":"))
                            args[1] = args[1] + ":";                        

                        if (ins.storedStickers.stickers.ContainsKey(args[1]) && (args.Length < 4 || args[3] != "overwrite"))
                        {
                            player.ChatMessage($"A sticker with name {args[1]} already exists, if you want to overwrite it, add the word overwrite to the end of the command.");
                            return;
                        }

                        AddSticker(args[1], new StickerData() { DUI_URL = args[2], TIER = "hidden" });
                        player.ChatMessage($"Added {args[1]} with url: {args[2]}");
                        break;
                }
            }
        }
        #endregion

        #region Recycle Stickers
        void RecycleStickers(BasePlayer player, string[] args)
        {
            if (!configData.recycleSettings.recyclingEnabled)
                return;

            var pSet = GetPlayerSettings(player);
            if (args.Length < 2 || pSet == null || pSet.detailsSticker == "")
                return;

            int amount;
            if (!int.TryParse(args[1], out amount))
                return;

            var sticker = GetStickerData(pSet.detailsSticker);
            if (sticker == null)
                return;

            List<string> playerStickers = new List<string>();
            if (!ins.storedPlayerStickers.unlockedStickers.TryGetValue(player.UserIDString, out playerStickers))
                return;

            var stickerAmountOwned = 0;
            if (playerStickers.Contains(pSet.detailsSticker))
                stickerAmountOwned = playerStickers.FindAll(x => x == pSet.detailsSticker).Count;

            if (stickerAmountOwned < amount)
            {
                Debug.Log($"::r:: {player.displayName} is trying to recycle more stickers than he has, probably via console, this is sus!");
                player.ChatMessage($"You don't have enough copies of this sticker!");
                return;
            }

            var stickerWorth = GetStickerRecyclePrize(pSet.detailsSticker, stickerAmountOwned);
            float earn = AddRecyclePrizeMultiplier(stickerWorth, amount);
            var tokens = Mathf.RoundToInt(earn);

            var removed = 0;
            for (var i = 0; i < amount; i++)
            {
                if (!playerStickers.Contains(pSet.detailsSticker))
                {
                    Debug.Log($"::r:: {player.displayName} recycled more stickers than he had - this should never be possible");
                    break;
                }
                playerStickers.Remove(pSet.detailsSticker);
                storedStickers.stickers[pSet.detailsSticker].CURRENT_OWNERS--;
                removed++;
            }

            AddPlayerTokens(player, tokens);

            player.ChatMessage($"You recycled {removed} x sticker {pSet.detailsSticker} for {tokens} sticker tokens");
            Debug.Log($"::g:: {player.displayName} is recycling {amount} x sticker {pSet.detailsSticker} for {tokens} tokens");

            if (stickerWorth >= 2f)
                PrintToChat($"<size=12><color={colorYellow}><color={colorOrange}>{player.displayName}</color> just recycled <color={colorRed}>{amount}x</color> rare sticker <color={colorOrange}>{pSet.detailsSticker}</color> for <color={colorRed}>{tokens}</color> tokens in <color={colorOrange}>/sticker</color>.</color></size>");

        }

        float AddRecyclePrizeMultiplier(float basePrize, int amount)
        {
            return basePrize * amount + (amount - 1);
        }

        float GetStickerRecyclePrize(string stickerName, int playerOwned = 0)
        {
            var sticker = GetStickerData(stickerName);
            if (sticker == null || sticker.MAX_OWNERS <= 0)
                return 1;

            // the maximum extra points a sticker can get
            // var maxAdditionalValue = 4f;
            var maxAdditionalValue = configData.recycleSettings.maxRecycleAdditionalValue;

            var globRarityMultiplier = (100f - ((100f / (float)AVG_STICKER_WEIGHT) * (float)sticker.MAX_OWNERS));
            //Debug.Log($"::g:: (100f - ((100f / (float)AVG_STICKER_WEIGHT({AVG_STICKER_WEIGHT}) * (float)sticker.MAX_OWNERS({sticker.MAX_OWNERS})) / 100f = globRarityMultiplier {globRarityMultiplier}%");

            float percentageOwned = (100f / (float)sticker.MAX_OWNERS) * (float)sticker.CURRENT_OWNERS;   // percentage already owned (more owners = higher rarity)
            //Debug.Log($"::y:: percentageOwned = (100f / (float)sticker.MAX_OWNERS({sticker.MAX_OWNERS})) * (float)sticker.CURRENT_OWNERS({sticker.CURRENT_OWNERS});");

            float initialBonus = (maxAdditionalValue / 100) * percentageOwned;
            //Debug.Log($"::y:: initialBonus = (maxAdditionalValue({maxAdditionalValue}) / 100) * percentageOwned({percentageOwned})");

            float totalBonus = (initialBonus / 100f) * globRarityMultiplier;
            //Debug.Log($"::y:: totalBonus = (initialBonus({initialBonus}) / 100f) * globRarityMultiplier({globRarityMultiplier})");

            var stickerValue = 1f + Math.Max(0, totalBonus);
            //Debug.Log($"::g:: stickerValue = 1f + Math.Max({Math.Max(0, totalBonus)}) = {stickerValue}");

            return stickerValue;
        }
        #endregion

        #region Tiers
     
        string GetTierColor(string tier, float opacity = 1f, bool hex = false)
        {
            switch (tier)
            {
                case "common": return hex ? colorCommon : HexToRGBA(colorCommon, opacity);
                case "rare": return hex ? colorRare : HexToRGBA(colorRare, opacity);
                case "epic": return hex ? colorEpic : HexToRGBA(colorEpic, opacity);
                case "legendary": return hex ? colorLegendary : HexToRGBA(colorLegendary, opacity);
                case "random": return hex ? colorEpic : HexToRGBA(colorEpic, opacity);
                //case "random5": return hex ? colorLegendary : HexToRGBA(colorEpic, opacity);
            }

            return hex ? colorCommon : "0 0 0 1";
        }
        #endregion

        #region UI
        #region UI Menus / Filters / Sorting
        CuiElementContainer CreateMenu(CuiElementContainer container, PlayerSettings pSet, bool isAdmin)
        {
            var h = -2f;
            var lh = 22f;

            container.Add(CreateUIPanel($"0 0", $"0 1", HexToRGBA("#262626", 0.965f), true, $"10 10", $"200 -10"), UIPanelStickerBrowser, "browser_menu");


            container.Add(CreateNewButton("BROWSE STICKERS", $"cmdsticker pset tab browser", $"0 1", $"1 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", pSet.tab == "browser" ? HexToRGBA(colorGreen, 0.4f) : HexToRGBA(color_dark, 0.4f), $"2 {h - lh}", $"-2 {h}"), "browser_menu");
            h -= lh + 2;

            container.Add(CreateNewButton("BUY STICKERS", $"cmdsticker pset tab buysticker", $"0 1", $"1 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", pSet.tab == "buysticker" ? HexToRGBA(colorGreen, 0.4f) : HexToRGBA(color_dark, 0.4f), $"2 {h - lh}", $"-2 {h}"), "browser_menu");
            h -= lh + 2;

           container.Add(CreateNewButton("TRADE STICKERS", $"cmdsticker pset tab stickertrader", $"0 1", $"1 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", pSet.tab == "stickertrader" ? HexToRGBA(colorGreen, 0.4f) : HexToRGBA(color_dark, 0.4f), $"2 {h - lh}", $"-2 {h}"), "browser_menu");
            h -= lh + 2;

            //if (isAdmin)
            //{
            //    container.Add(CreateNewButton("TOKENS", $"cmdsticker pset tab buytokens", $"0 1", $"1 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", pSet.tab == "buytokens" ? HexToRGBA(colorGreen, 0.4f) : HexToRGBA(color_dark, 0.4f), $"2 {h - lh}", $"-2 {h}"), "browser_menu");
            //    h -= lh + 2;
            //}

            container.Add(CreateNewButton("HELP & INFO", $"cmdsticker pset tab infopage", $"0 1", $"1 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", pSet.tab == "infopage" ? HexToRGBA(colorGreen, 0.4f) : HexToRGBA(color_dark, 0.4f), $"2 {h - lh}", $"-2 {h}"), "browser_menu");
            h -= lh + 2;

            if (pSet.tab == "browser" || pSet.tab == "stickertrader")
            {
                h -= lh + 2;

                container.Add(CreateNewButton("ALL", $"cmdsticker pset filter all", $"0 1", $"1 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", pSet.filter == "all" ? HexToRGBA(colorGreen, 0.4f) : HexToRGBA(color_dark, 0.4f), $"2 {h - lh}", $"-2 {h}"), "browser_menu");
                h -= lh + 2;
                container.Add(CreateNewButton("COMMON", $"cmdsticker pset filter common", $"0 1", $"1 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", pSet.filter == "common" ? HexToRGBA(colorGreen, 0.4f) : HexToRGBA(color_dark, 0.4f), $"2 {h - lh}", $"-2 {h}"), "browser_menu");
                h -= lh + 2;
                container.Add(CreateNewButton("RARE", $"cmdsticker pset filter rare", $"0 1", $"1 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", pSet.filter == "rare" ? HexToRGBA(colorGreen, 0.4f) : HexToRGBA(color_dark, 0.4f), $"2 {h - lh}", $"-2 {h}"), "browser_menu");
                h -= lh + 2;
                container.Add(CreateNewButton("EPIC", $"cmdsticker pset filter epic", $"0 1", $"1 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", pSet.filter == "epic" ? HexToRGBA(colorGreen, 0.4f) : HexToRGBA(color_dark, 0.4f), $"2 {h - lh}", $"-2 {h}"), "browser_menu");
                h -= lh + 2;
                container.Add(CreateNewButton("LEGENDARY", $"cmdsticker pset filter legendary", $"0 1", $"1 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", pSet.filter == "legendary" ? HexToRGBA(colorGreen, 0.4f) : HexToRGBA(color_dark, 0.4f), $"2 {h - lh}", $"-2 {h}"), "browser_menu");
                h -= lh;

                if (isAdmin)
                {
                    container.Add(CreateNewButton("HIDDEN", $"cmdsticker pset filter hidden", $"0 1", $"1 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", pSet.filter == "hidden" ? HexToRGBA(colorGreen, 0.4f) : HexToRGBA(color_dark, 0.4f), $"2 {h - lh}", $"-2 {h}"), "browser_menu");
                    h -= lh;
                }

                h -= lh + 2;


                var buttonHeight = 20f;

                // SORT ORDER
                container.Add(CreateNewLabel($"SORT BY {pSet.sortOrder.ToUpper()}", $"0 1", $"1 1", TextAnchor.MiddleLeft, 12, "1 1 1 1", $"10 {h - lh}", $"-10 {h}"), "browser_menu");
                h -= lh;

                // SORT ORDER
                container.Add(CreateUIPanel($"0 1", $"1 1", "0 0 0 0", false, $"10 {h - (buttonHeight + 4)}", $"-10 {h}"), $"browser_menu", $"browser_menu_sort");
                container = CreateButtonsSort(container, pSet, "browser_menu_sort");
                h -= lh + 2;

                // SHOW LOCKED / UNLOCKED / ALL
                container.Add(CreateUIPanel($"0 1", $"1 1", "0 0 0 0", false, $"10 {h - (buttonHeight + 4)}", $"-10 {h}"), $"browser_menu", $"browser_menu_filtershow");
                container = CreateButtonsFilterShow(container, pSet, "browser_menu_filtershow");

                h -= lh + 2;
                container.Add(CreateUIPanel($"0 1", $"1 1", "0 0 0 0", false, $"10 {h - (buttonHeight + 4)}", $"-10 {h}"), $"browser_menu", $"browser_menu_filtersearch");
                container = CreateSearchBar(container, pSet, "browser_menu_filtersearch");

            }

            container.Add(CreateNewButton("CLOSE", "cmdsticker close", $"0 0", $"1 0", TextAnchor.MiddleCenter, 12, "1 1 1 1", HexToRGBA(colorRed, 0.7f), "2 2", "-2 24"), "browser_menu");


            return container;
        }
        CuiElementContainer CreateRightMenu(CuiElementContainer container, PlayerSettings pSet, bool isAdmin)
        {
            var h = -3f;
            var lh = 22f;
            var btn_pad = 2f;
            var pad = 10f;
            var left = pad;

            container.Add(CreateUIPanel($"1 0", $"1 1", HexToRGBA("#262626", 0), true, $"-200 0", $"0 0"), UIPanelStickerBrowser, "browser_menu_right");

            if (pSet.tab == "browser" || pSet.tab == "stickertrader")
            {
                container = CreateButtonsFilterCollections(container, pSet, "browser_menu_right", h, lh, pad, btn_pad);
            }

            return container;
        }

        CuiElementContainer CreateButtonsFilterCollections(CuiElementContainer container, PlayerSettings pSet, string parent, float h, float lh, float pad, float btn_pad)
        {
            container.Add(CreateUIPanel($"0 1", $"1 1", HexToRGBA("#262626", 0.965f), true, $"{pad} {-(300 + btn_pad)}", $"{-pad} {-pad}"), parent, "filter_collections");

            var max_cols = 2;
            var max_rows = 10;
            var maxShown = max_cols * max_rows;
            var start = pSet.groupPickerPage * maxShown;
            var end = start + maxShown;
            pSet.groupPickerTotalPages = Mathf.CeilToInt((float)pSet.availableCollections.Count / (float)maxShown);



            container.Add(CreateNewButton("PREV", $"cmdsticker pset grouppicker prev", "0 1", "0.33 1", TextAnchor.MiddleCenter, 11, "1 1 1 1", HexToRGBA(color_light, 0.1f), $"{btn_pad} {h - lh}", $"{-btn_pad} {h}"), "filter_collections");
            container.Add(CreateNewLabel($"{pSet.groupPickerPage + 1} / {pSet.groupPickerTotalPages}", "0.34 1", "0.66 1", TextAnchor.MiddleCenter, 11, "1 1 1 1", $"{btn_pad} {h - lh}", $"{-btn_pad} {h}"), "filter_collections");
            container.Add(CreateNewButton("NEXT", $"cmdsticker pset grouppicker next", "0.67 1", "1 1", TextAnchor.MiddleCenter, 11, "1 1 1 1", HexToRGBA(color_light, 0.1f), $"{btn_pad} {h - lh}", $"{-btn_pad} {h}"), "filter_collections");

            h -= (lh + btn_pad);

            container.Add(CreateNewButton("ALL", $"cmdsticker pset filtercollection all", "0 1", "0.5 1", TextAnchor.MiddleCenter, 11, "1 1 1 1", pSet.filterCollection == "all" ? HexToRGBA(colorGreen, 0.4f) : HexToRGBA(color_light, 0.1f), $"{btn_pad} {h - lh}", $"{-btn_pad} {h}"), "filter_collections");
            container.Add(CreateNewButton("NO GROUP", $"cmdsticker pset filtercollection none", "0.5 1", "1 1", TextAnchor.MiddleCenter, 11, "1 1 1 1", pSet.filterCollection == "none" ? HexToRGBA(colorGreen, 0.4f) : HexToRGBA(color_light, 0.1f), $"{btn_pad} {h - lh}", $"{-btn_pad} {h}"), "filter_collections");

            h -= (lh + btn_pad);

            var i = 0;
            var sorted = pSet.availableCollections.OrderBy(x => x);
            foreach (var collection in sorted)
            {
                if (collection == "" || collection == "none")
                    continue;

                if (i < start || i >= end)
                {
                    i++;
                    continue;
                }

                var aMin = i % 2 == 0 ? "0 1" : "0.5 1";
                var aMax = i % 2 == 0 ? "0.5 1" : "1 1";
                var colCmd = collection == "" ? "none" : collection;
                container.Add(CreateNewButton(collection.ToUpper(), $"cmdsticker pset filtercollection {colCmd}", aMin, aMax, TextAnchor.MiddleCenter, 11, "1 1 1 1", pSet.filterCollection == collection ? HexToRGBA(colorGreen, 0.4f) : HexToRGBA(color_light, 0.1f), $"{btn_pad} {h - lh}", $"{-btn_pad} {h}"), "filter_collections");
                i++;
                if (i % 2 == 0)
                    h -= (lh + btn_pad);
            }



            return container;
        }

        CuiElementContainer CreateButtonsFilterShow(CuiElementContainer container, PlayerSettings pSet, string parent)
        {
            var blf = 2f;

            container.Add(CreateNewLabel($"SHOW ", $"0 0", $"0 1", TextAnchor.MiddleLeft, 12, "1 1 1 1", $"2 0", $"50 0"), "browser_menu_filtershow");
            blf += 45f;

            container.Add(CreateNewButton("ALL", $"cmdsticker pset filtershow all", $"0 0", $"0 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", pSet.filterShow == "all" ? HexToRGBA(colorGreen, 0.4f) : HexToRGBA(color_darkest, 0.85f), $"{blf} 2", $"{blf + 27f} -2"), parent, $"bmfs_all");
            blf += 31f;

            container.Add(CreateNewButton("", $"cmdsticker pset filtershow locked", $"0 0", $"0 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", pSet.filterShow == "locked" ? HexToRGBA(colorGreen, 0.4f) : HexToRGBA(color_darkest, 0.85f), $"{blf} 2", $"{blf + 21f} -2"), parent, $"bmfs_locked");
            container.Add(CreateNewImage("icon_lock_url2", $"bmfs_locked", "0.5 0.5", "0.5 0.5", "-6 -6", "6 6"));
            blf += 25f;

            container.Add(CreateNewButton("", $"cmdsticker pset filtershow unlocked", $"0 0", $"0 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", pSet.filterShow == "unlocked" ? HexToRGBA(colorGreen, 0.4f) : HexToRGBA(color_darkest, 0.85f), $"{blf} 2", $"{blf + 21f} -2"), parent, $"bmfs_unlocked");
            container.Add(CreateNewImage("icon_lock_open_url", $"bmfs_unlocked", "0.5 0.5", "0.5 0.5", "-6 -6", "6 6"));

            return container;
        }

        CuiElementContainer CreateButtonsSort(CuiElementContainer container, PlayerSettings pSet, string parent)
        {
            var blf = 0f;

            container.Add(CreateNewButton("NEW", $"cmdsticker pset sort date", $"0 0", $"0 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", pSet.sortOrder == "date" ? HexToRGBA(colorGreen, 0.4f) : HexToRGBA(color_darkest, 0.85f), $"{blf} 2", $"{blf + 35f} -2"), parent);
            blf += 39f;

            container.Add(CreateNewButton("A-Z", $"cmdsticker pset sort name", $"0 0", $"0 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", pSet.sortOrder == "name" ? HexToRGBA(colorGreen, 0.4f) : HexToRGBA(color_darkest, 0.85f), $"{blf} 2", $"{blf + 27f} -2"), parent);
            blf += 31f;

            container.Add(CreateNewButton("RAR", $"cmdsticker pset sort rarity", $"0 0", $"0 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", pSet.sortOrder == "rarity" ? HexToRGBA(colorGreen, 0.4f) : HexToRGBA(color_darkest, 0.85f), $"{blf} 2", $"{blf + 27f} -2"), parent);
            blf += 31f;

            container.Add(CreateNewButton("COL", $"cmdsticker pset sort collection", $"0 0", $"0 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", pSet.sortOrder == "collection" ? HexToRGBA(colorGreen, 0.4f) : HexToRGBA(color_darkest, 0.85f), $"{blf} 2", $"{blf + 27f} -2"), parent);
            blf += 31f;

            container.Add(CreateNewButton("AVL", $"cmdsticker pset sort availability", $"0 0", $"0 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", pSet.sortOrder == "availability" ? HexToRGBA(colorGreen, 0.4f) : HexToRGBA(color_darkest, 0.85f), $"{blf} 2", $"{blf + 27f} -2"), parent);

            return container;
        }

        CuiElementContainer CreateSearchBar(CuiElementContainer container, PlayerSettings pSet, string parent)
        {
            var blf = 2f;
            container.Add(CreateNewLabel($"SEARCH ", $"0 0", $"0 1", TextAnchor.MiddleLeft, 12, "1 1 1 1", $"2 0", $"50 0"), parent);
            blf += 45f;

            container.Add(CreateUIPanel($"0 0", $"1 1", HexToRGBA(color_light, 0.2f), false, $"{blf} 2", $"-2 -2", true), parent, $"searchbar_wrap");
            container.Add(CreateInputField(pSet.filterSearch, $"cmdsticker pset filtersearch", "searchbar_wrap", $"0 0", $"1 1", $"2 0", $"-2 0", TextAnchor.MiddleLeft));
            return container;
        }

        Dictionary<string, StickerData> FilterAndSortItemList(Dictionary<string, StickerData> items, BasePlayer player, PlayerSettings pSet)
        {
            var unlockedStickers = GetUnlockedStickers(player);
            var isAdmin = HasPermission(player.UserIDString, permAdmin);

            var itemList = new Dictionary<string, StickerData>();

            foreach (var ed in items)
            {
                var tag = ed.Key;
                var stickerData = ed.Value;

                if (stickerData.TIER == "hidden" && !isAdmin)
                    continue;

                var selectedFilterCollection = pSet.filterCollection == "none" ? "" : pSet.filterCollection;
                if (pSet.filterCollection != "all" && stickerData.COLLECTION != selectedFilterCollection)
                    continue;

                if (pSet.filter != "all" && pSet.filter != stickerData.TIER)
                    continue;

                if (pSet.filterShow == "unlocked" && !unlockedStickers.Contains(tag))
                    continue;

                if (pSet.filterShow == "locked" && unlockedStickers.Contains(tag))
                    continue;

                if (pSet.filterSearch != "" && !tag.Contains(pSet.filterSearch) && !stickerData.COLLECTION.Contains(pSet.filterSearch))
                    continue;

                itemList.Add(tag, stickerData);
            }


            if (pSet.sortOrder != "date")
            {
                switch (pSet.sortOrder)
                {
                    case "name":
                        itemList = itemList.OrderBy(x => x.Key).ToDictionary(x => x.Key, y => y.Value);
                        break;
                    case "rarity":
                        itemList = itemList.OrderBy(x => x.Value.MAX_OWNERS).ToDictionary(x => x.Key, y => y.Value);
                        break;
                    case "collection":
                        itemList = itemList.OrderBy(x => x.Value.COLLECTION).ToDictionary(x => x.Key, y => y.Value);
                        break;
                    case "availability":
                        itemList = itemList.OrderByDescending(x => x.Value.MAX_OWNERS - x.Value.CURRENT_OWNERS).ToDictionary(x => x.Key, y => y.Value);
                        break;
                }
            }

            return itemList;
        }

        List<StickerTrade> FilterAndSortTrades(List<StickerTrade> items, BasePlayer player, PlayerSettings pSet)
        {
            var unlockedStickers = GetUnlockedStickers(player);
            var isAdmin = HasPermission(player.UserIDString, permAdmin);

            var itemList = new List<StickerTrade>();

            foreach (var stickerTrade in items)
            {
                var tag = stickerTrade.sticker;
                var stickerData = GetStickerData(stickerTrade.sticker);
                if (stickerData == null)
                    continue;

                if (stickerTrade.tier == "hidden" && !isAdmin)
                    continue;

                var selectedFilterCollection = pSet.filterCollection == "none" ? "" : pSet.filterCollection;
                if (pSet.filterCollection != "all" && stickerData.COLLECTION != selectedFilterCollection)
                    continue;

                if (pSet.filter != "all" && pSet.filter != stickerData.TIER)
                    continue;

                if (pSet.filterShow == "unlocked" && !unlockedStickers.Contains(tag))
                    continue;

                if (pSet.filterShow == "locked" && unlockedStickers.Contains(tag))
                    continue;

                stickerTrade.max_owners = stickerData.MAX_OWNERS;
                stickerTrade.collection = stickerData.COLLECTION;

                itemList.Add(stickerTrade);
            }


            if (pSet.sortOrder != "date")
            {
                switch (pSet.sortOrder)
                {
                    case "name":
                        itemList = itemList.OrderBy(x => x.sticker).ToList<StickerTrade>();
                        break;
                    case "rarity":
                        itemList = itemList.OrderBy(x => x.max_owners).ToList<StickerTrade>();
                        break;
                    case "collection":
                        itemList = itemList.OrderBy(x => x.collection).ToList<StickerTrade>();
                        break;
                    case "availability":
                        itemList = itemList.OrderByDescending(x => x.max_owners - x.current_owners).ToList<StickerTrade>();
                        break;
                }
            }

            return itemList;
        }
        #endregion

        #region UI Tabs
        void UpdateStickerBrowser(BasePlayer player)
        {
            var pSet = GetPlayerSettings(player);
            if (pSet == null) return;

            var container = new CuiElementContainer();
            var isAdmin = HasPermission(player.UserIDString, permAdmin);

            var oMin = $"-400 -243";
            var oMax = $"400 243";
            var background = HexToRGBA("#262626", 0.965f);
            var bholder_left = 210f;
            var bholder_right = -10f;
            var bholder_bg = HexToRGBA("#262626", 0.965f);

            if (pSet.tab == "edit_sticker")
            {
                oMin = $"-145 -269";
                oMax = $"145 0";
                background = "0 0 0 0.9";
                bholder_left = 10f;
                bholder_bg = "0 0 0 0";
            }

            if (pSet.tab == "browser" || pSet.tab == "stickertrader")
            {
                oMin = $"-500 -243";
                oMax = $"500 243";
                bholder_right = -200f;
            }


            container.Add(CreateUIPanel($"0.5 0.5", $"0.5 0.5", background, true, oMin, oMax), "Overlay", UIPanelStickerBrowser);

            if (pSet.tab != "edit_sticker")
                container = CreateMenu(container, pSet, isAdmin);

            container.Add(CreateUIPanel($"0 0", $"1 1", bholder_bg, true, $"{bholder_left} 10", $"{bholder_right} -10"), UIPanelStickerBrowser, "browser_holder");

            if (pSet.tab == "browser")
            {
                container = CreateRightMenu(container, pSet, isAdmin);

                var itemList = FilterAndSortItemList(new Dictionary<string, StickerData>(ins.storedStickers.stickers), player, pSet);

                if (pSet.detailsSticker != "")
                {
                    container = CreateStickerDetails(player, container, pSet);
                }
                else
                {
                    container = Create_ItemList(player, container, itemList, pSet, "browser_holder", 6, 4, 60f, 103f, 10f, 580);
                }
            }
            else if (pSet.tab == "buysticker")
            {
                container = CreateStickerMarket(player, container);
            }
            //else if (pSet.tab == "buytokens")
            //{
            //    container = CreateTokenMarket(player, container);
            //}
            else if (pSet.tab == "stickertrader" && configData.stickerTraderSettings.tradingEnabled)
            {
                container = CreateRightMenu(container, pSet, isAdmin);
                container = CreateStickerTrader(player, container, pSet);
            }
            else if (pSet.tab == "infopage")
            {
                container = CreateInfoPage(player, container, pSet);
            }
            else if (pSet.tab == "edit_sticker")
            {
                if (pSet.showGroupPicker)
                {
                    container = CreateGroupPicker(player, container, pSet);
                }
                else
                {
                    container = CreateStickerEditor(player, container, pSet);
                }
            }


            if(pSet.justUnlockedStickers.Count > 0)
            {
                container = CreateUnlockedStickerPopup(player, container, pSet);
            }


            AddUI(player, container, UIPanelStickerBrowser);
        }

        CuiElementContainer CreateUnlockedStickerPopup(BasePlayer player, CuiElementContainer container, PlayerSettings pSet)
        {            
            if (pSet.justUnlockedStickers.Count == 0) return container;

            var popupW = 400f;
            var pad = 10f;
            var titleH = 26f;
            var itemH = 64f;
            var imageH = 60f;
            var itemPad = 3f;

            if (pSet.justUnlockedStickers.Count > 5)
            {
                itemH = 50f;
                imageH = 44f;
                itemPad = 2f;
            }

            var popupH = pSet.justUnlockedStickers.Count * itemH + (pSet.justUnlockedStickers.Count * itemPad) + (2 * pad) + titleH;

            float ossX = 0;
            float ossY = 0;
            float h = -5f;

           

            container.Add(CreateUIPanel($"0.5 0.5", $"0.5 0.5", HexToRGBA("#1f1f1f"), true, $"{-popupW/2} {-popupH/2}", $"{popupW /2} {popupH / 2}"), UIPanelStickerBrowser, UIPanelStickerUnlock);

            container.Add(CreateNewLabel("UNLOCKED STICKERS", $"0 1", $"1 1", TextAnchor.MiddleCenter, 14, "1 1 1 1", $"{pad} {h-titleH}", $"{-pad} {h}", CuiFont.ROBOTO_BOLD), UIPanelStickerUnlock);
            container.Add(CreateNewButton($"X", $"cmdsticker pset unlockpopup close", $"1 1", $"1 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", HexToRGBA(colorRed, 0.25f), $"-60 {h-titleH}", $"{-pad} {h}"), UIPanelStickerUnlock);

            h -= (titleH + 5f);

            var i = 0;
            foreach(var item in pSet.justUnlockedStickers)
            {
                var sticker = GetStickerData(item);
                if (sticker == null) continue;

                // wrap
                container.Add(CreateUIPanel($"0 1", $"1 1", GetTierColor(sticker.TIER, 0.15f), true, $"{pad} {h - (itemH)}", $"{-pad} {h}"), UIPanelStickerUnlock, $"j_unlocked_{i}");


                // image w/h
                if (sticker.DUI_WIDTH >= sticker.DUI_HEIGHT)
                {
                    ossX = imageH / 2;
                    var imgHeight = ((sticker.DUI_HEIGHT / sticker.DUI_WIDTH) * imageH);
                    ossY = imgHeight / 2;
                }
                else
                {
                    ossY = imageH / 2;
                    var imgWidth = ((sticker.DUI_WIDTH / sticker.DUI_HEIGHT) * imageH);
                    ossX = imgWidth / 2;
                }

                // img
                container.Add(CreateUIPanel($"0 0", $"0 1", "0 0 0 0", false, $"{pad} 5", $"{pad + itemH} -5"), $"j_unlocked_{i}", $"j_unlocked_img_{i}");
                container.Add(CreateNewImage($"{item}", $"j_unlocked_img_{i}", "0.5 0.5", "0.5 0.5", $"{-ossX} {-ossY}", $"{ossX} {ossY}"));

                // :tag:
                container.Add(CreateNewLabel($"{item.ToUpper()}", $"0 0", $"1 1", TextAnchor.MiddleLeft, 14, "1 1 1 1", $"{itemH + (pad * 2)} 5", $"{-pad} -5"), $"j_unlocked_{i}");
                container.Add(CreateNewLabel($"{sticker.CURRENT_OWNERS}/{sticker.MAX_OWNERS} OWNED", $"0 0", $"1 1", TextAnchor.MiddleRight, 14, "1 1 1 1", $"{itemH + pad} 5", $"{-pad} -5"), $"j_unlocked_{i}");
  
                h -= (itemH + itemPad);
                i++;
            }

            return container;
        }


        CuiElementContainer CreateStickerMarket(BasePlayer player, CuiElementContainer container)
        {
            var playerTokens = GetPlayerTokens(player);

            var h = 0f;
            var lh = 40f;

            //var bottom = 0f;
            //var right = 0f;

            var currencyAmount = PlayerCurrencyAmount(player);

            container.Add(CreateNewLabel($"Available {configData.currencySettings.currencyName}: {currencyAmount}", $"0 1", $"0.5 1", TextAnchor.MiddleLeft, 14, "1 1 1 1", $"10 {h - lh}", $"-10 {h}"), "browser_holder");
            container.Add(CreateNewLabel($"Available Tokens: {playerTokens}", $"0.5 1", $"1 1", TextAnchor.MiddleLeft, 14, "1 1 1 1", $"10 {h - lh}", $"-10 {h}"), "browser_holder");
            h -= lh;

            lh = 24f;

            // container.Add(CreateNewButton($"{level}", $"cmdzui buy {level}", $"{left} {top - 40}", $"{left +", TextAnchor.MiddleCenter, 14, "1 1 1 1", $"0 0", $"0 0"), "browser_holder");


            container.Add(CreateUIPanel($"0 0.5", $"1 1", "0 0 0 0", true, $"210 10", $"-10 -10"), UIPanelStickerBrowser, "buy_buttons_wrap");


            container.Add(CreateUIPanel($"0 1", $"1 1", HexToRGBA(colorYellow, 0.1f), true, $"10 {h - lh}", $"-10 {h}"), "buy_buttons_wrap", $"tokenbutton_head");
            container.Add(CreateNewLabel($"PACKAGE", $"0 0", $"0.25 1", TextAnchor.MiddleLeft, 12, "1 1 1 1", $"10 0", $"0 0"), $"tokenbutton_head");
            container.Add(CreateNewLabel($"TOKENS", "0.25 0", "0.5 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", $"10 0", $"0 0"), $"tokenbutton_head");
            container.Add(CreateNewLabel($"COST X {configData.currencySettings.currencyName.ToUpper()}", "0.5 0", "0.75 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", $"10 0", $"0 0"), $"tokenbutton_head");

            h -= (lh + 2);

            var ii = 0;
            foreach (var pack in configData.currencySettings.tokenPacks)
            {
                container.Add(CreateUIPanel($"0 1", $"1 1", HexToRGBA(color_light, 0.05f), true, $"10 {h - lh}", $"-10 {h}"), "buy_buttons_wrap", $"tokenbutton_{ii}");

                container.Add(CreateNewLabel($"{pack.name.ToUpper()}", $"0 0", $"0.25 1", TextAnchor.MiddleLeft, 12, "1 1 1 1", $"10 0", $"0 0"), $"tokenbutton_{ii}");
                container.Add(CreateNewLabel($"{pack.tokenAmount}", "0.25 0", "0.5 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", $"10 0", $"0 0"), $"tokenbutton_{ii}");
                container.Add(CreateNewLabel($"{pack.price}", "0.5 0", "0.75 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", $"10 0", $"0 0"), $"tokenbutton_{ii}");

                var btnbg = currencyAmount < pack.price ? HexToRGBA(color_dark, 0.5f) : HexToRGBA(colorGreen, 0.3f);
                container.Add(CreateNewButton($"BUY", $"cmdsticker buytokens {pack.name}", "1 0", "1 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", btnbg, $"-50 0", $"0 0"), $"tokenbutton_{ii}");

                h -= (lh + 2);
                ii++;
            }



            h -= (lh + 2);


            container.Add(CreateUIPanel($"0 1", $"1 1", HexToRGBA(colorYellow, 0.1f), true, $"10 {h - lh}", $"-10 {h}"), "buy_buttons_wrap", $"stickerbutton_head");
            container.Add(CreateNewLabel($"UNLOCK RANDOM", $"0 0", $"0.5 1", TextAnchor.MiddleLeft, 12, "1 1 1 1", $"10 0", $"0 0"), $"stickerbutton_head");
            container.Add(CreateNewLabel($"STICKERS", "0.5 0", "0.65 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", $"10 0", $"0 0"), $"stickerbutton_head");
            container.Add(CreateNewLabel($"TOKEN COST", "0.65 0", "1 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", $"10 0", $"-60 0"), $"stickerbutton_head");

            h -= (lh + 2);


            var i = 0;
            foreach (var pack in configData.stickerUnlockPacks)
            {
                if (!pack.enable || (pack.permission != "" && !HasPermission(player.UserIDString, pack.permission)))
                    continue;

                var bg = pack.background != "" ? HexToRGBA(pack.background, 0.25f) : HexToRGBA(color_light, 0.05f);
                container.Add(CreateUIPanel($"0 1", $"1 1", bg, true, $"10 {h - lh}", $"-10 {h}"), "buy_buttons_wrap", $"packbutton_{i}");

                var name = pack.displayName != "" ? pack.displayName : pack.name.ToUpper();
                container.Add(CreateNewLabel($"{name}", $"0 0", $"0.5 1", TextAnchor.MiddleLeft, 12, "1 1 1 1", $"10 0", $"0 0"), $"packbutton_{i}");
                container.Add(CreateNewLabel($"{pack.amount}", "0.5 0", "0.65 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", $"10 0", $"0 0"), $"packbutton_{i}");
                container.Add(CreateNewLabel($"{pack.price}", "0.65 0", "1 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", $"10 0", $"-60 0"), $"packbutton_{i}");

                var bgc = playerTokens < pack.price ? HexToRGBA(color_dark, 0.5f) : HexToRGBA(colorGreen, 0.3f);
                var command = playerTokens < pack.price ? "" : $"cmdsticker unlocksticker {pack.name}";
                container.Add(CreateNewButton($"BUY", command, "1 0", "1 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", bgc, $"-50 0", $"0 0"), $"packbutton_{i}");

                h -= (lh + 2);
                i++;
            }



            //if (HasPermission(player.UserIDString, permAdmin))
            //{
                

            //    var i = 0;
            //    foreach (var pack in configData.stickerUnlockPacks)
            //    {
            //        if (!pack.enable || (pack.permission != "" && !HasPermission(player.UserIDString, pack.permission)))
            //            continue;

            //        var bg = pack.background != "" ? HexToRGBA(pack.background, 0.25f) : HexToRGBA(color_light, 0.05f);
            //        container.Add(CreateUIPanel($"0 1", $"1 1", bg, true, $"10 {h - lh}", $"-10 {h}"), "buy_buttons_wrap", $"packbutton_{i}");

            //        var name = pack.displayName != "" ? pack.displayName : pack.name.ToUpper();
            //        container.Add(CreateNewLabel($"{name}", $"0 0", $"0.5 1", TextAnchor.MiddleLeft, 12, "1 1 1 1", $"10 0", $"0 0"), $"packbutton_{i}");
            //        container.Add(CreateNewLabel($"{pack.amount}", "0.5 0", "0.65 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", $"10 0", $"0 0"), $"packbutton_{i}");
            //        container.Add(CreateNewLabel($"{pack.price}", "0.65 0", "1 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", $"10 0", $"-60 0"), $"packbutton_{i}");

            //        var bgc = playerTokens < pack.price ? HexToRGBA(color_dark, 0.5f) : HexToRGBA(colorGreen, 0.3f);
            //        var command = playerTokens < pack.price ? "" : $"cmdsticker unlocksticker {pack.name}";
            //        container.Add(CreateNewButton($"BUY", command, "1 0", "1 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", bgc, $"-50 0", $"0 0"), $"packbutton_{i}");

            //        h -= (lh + 2);
            //        i++;
            //    }

            //}
            //else
            //{
            //    var tier = new Tier()
            //    {
            //        name = "random",
            //        price = ins.configData.currencySettings.sticker_price
            //    };

            //    container.Add(CreateUIPanel($"0 1", $"1 1", HexToRGBA(colorEpic, 0.25f), true, $"10 {h - lh}", $"-10 {h}"), "buy_buttons_wrap", $"tierbutton_1");

            //    container.Add(CreateNewLabel($"UNLOCK RANDOM STICKER", $"0 0", $"0.5 1", TextAnchor.MiddleLeft, 12, "1 1 1 1", $"10 0", $"0 0"), $"tierbutton_1");
            //    container.Add(CreateNewLabel($"1", "0.5 0", "0.65 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", $"10 0", $"0 0"), $"packbutton_1");
            //    container.Add(CreateNewLabel($"{tier.price}", "0.65 0", "0.9 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", $"10 0", $"0 0"), $"tierbutton_1");

            //    var bgc = playerTokens < tier.price ? HexToRGBA(color_dark, 0.5f) : HexToRGBA(colorGreen, 0.3f);
            //    var command = playerTokens < tier.price ? "" : $"cmdsticker unlocksticker {tier.name}";
            //    container.Add(CreateNewButton($"BUY", command, "1 0", "1 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", bgc, $"-50 0", $"0 0"), $"tierbutton_1");


            //}


            if (HasPermission(player.UserIDString, permAdmin))
            {
                h -= (lh + 2);

                container.Add(CreateNewButton($"TEST UNLOCK", "cmdsticker pset unlockpopup test", "1 0", "1 0", TextAnchor.MiddleCenter, 12, "1 1 1 1", HexToRGBA(colorGreen, 0.3f), $"-120 10", $"-10 34"), "browser_holder");
            }

            return container;
        }

        //CuiElementContainer CreateTokenMarket(BasePlayer player, CuiElementContainer container)
        //{
        //    var playerTokens = GetPlayerTokens(player);

        //    var h = 0f;
        //    var lh = 40f;

        //    //var bottom = 0f;
        //    //var right = 0f;

        //   // var zcoinAmount = PlayerZCoinAmount(player);
        //    var currencyAmount = PlayerCurrencyAmount(player);


        //    container.Add(CreateNewLabel($"Available {configData.currencyName}: {currencyAmount}", $"0 1", $"0.5 1", TextAnchor.MiddleLeft, 14, "1 1 1 1", $"10 {h - lh}", $"-10 {h}"), "browser_holder");
        //    container.Add(CreateNewLabel($"Available Tokens: {playerTokens}", $"0.5 1", $"1 1", TextAnchor.MiddleLeft, 14, "1 1 1 1", $"10 {h - lh}", $"-10 {h}"), "browser_holder");
        //    h -= lh;

        //    lh = 24f;

        //    // container.Add(CreateNewButton($"{level}", $"cmdzui buy {level}", $"{left} {top - 40}", $"{left +", TextAnchor.MiddleCenter, 14, "1 1 1 1", $"0 0", $"0 0"), "browser_holder");


        //    container.Add(CreateUIPanel($"0 0.5", $"1 1", "0 0 0 0", true, $"210 10", $"-10 -10"), UIPanelStickerBrowser, "buy_buttons_wrap");


        //    container.Add(CreateUIPanel($"0 1", $"1 1", HexToRGBA(colorYellow, 0.1f), true, $"10 {h - lh}", $"-10 {h}"), "buy_buttons_wrap", $"tokenbutton_head");
        //    container.Add(CreateNewLabel($"PACKAGE", $"0 0", $"0.25 1", TextAnchor.MiddleLeft, 12, "1 1 1 1", $"10 0", $"0 0"), $"tokenbutton_head");
        //    container.Add(CreateNewLabel($"TOKENS", "0.25 0", "0.5 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", $"10 0", $"0 0"), $"tokenbutton_head");
        //    container.Add(CreateNewLabel($"COST X ZCOIN", "0.5 0", "0.75 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", $"10 0", $"0 0"), $"tokenbutton_head");

        //    h -= (lh + 2);

        //    var ii = 0;
        //    foreach (var pack in tokenPacks)
        //    {
        //        container.Add(CreateUIPanel($"0 1", $"1 1", HexToRGBA(color_light, 0.05f), true, $"10 {h - lh}", $"-10 {h}"), "buy_buttons_wrap", $"tokenbutton_{ii}");

        //        container.Add(CreateNewLabel($"{pack.Value.name.ToUpper()}", $"0 0", $"0.25 1", TextAnchor.MiddleLeft, 12, "1 1 1 1", $"10 0", $"0 0"), $"tokenbutton_{ii}");
        //        container.Add(CreateNewLabel($"{pack.Value.tokenAmount}", "0.25 0", "0.5 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", $"10 0", $"0 0"), $"tokenbutton_{ii}");
        //        container.Add(CreateNewLabel($"{pack.Value.price}", "0.5 0", "0.75 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", $"10 0", $"0 0"), $"tokenbutton_{ii}");

        //        var bgc = currencyAmount < pack.Value.price ? HexToRGBA(color_dark, 0.5f) : HexToRGBA(colorGreen, 0.3f);
        //        container.Add(CreateNewButton($"BUY", $"cmdsticker buytokens {pack.Value.name}", "1 0", "1 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", bgc, $"-60 0", $"-10 0"), $"tokenbutton_{ii}");

        //        h -= (lh + 2);
        //        ii++;
        //    }



        //    h -= (lh + 2);


        //    container.Add(CreateUIPanel($"0 1", $"1 1", HexToRGBA(colorYellow, 0.1f), true, $"10 {h - lh}", $"-10 {h}"), "buy_buttons_wrap", $"selltokens_head");
        //    container.Add(CreateNewLabel($"EXCHANGE TOKENS", $"0 0", $"0.25 1", TextAnchor.MiddleLeft, 12, "1 1 1 1", $"10 0", $"0 0"), $"selltokens_head");
        //    container.Add(CreateNewLabel($"YOU SELL", "0.25 0", "0.5 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", $"10 0", $"0 0"), $"selltokens_head");
        //    container.Add(CreateNewLabel($"YOU GET", "0.5 0", "0.75 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", $"10 0", $"0 0"), $"selltokens_head");


        //    h -= (lh + 2);


        //    var i = 0;
        //    foreach (var tier in sellTokenTiers.Values)
        //    {
        //        container.Add(CreateUIPanel($"0 1", $"1 1", GetTierColor(tier.name, 0.25f), true, $"10 {h - lh}", $"-10 {h}"), "buy_buttons_wrap", $"tierbutton_{i}");
        //        container.Add(CreateNewLabel($"{tier.name.ToUpper()}", $"0 0", $"0.25 1", TextAnchor.MiddleLeft, 12, "1 1 1 1", $"10 0", $"0 0"), $"tierbutton_{i}");
        //        //container.Add(CreateNewLabel($"", "0.25 0", "0.5 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", $"10 0", $"0 0"), $"tierbutton_{i}");
        //        container.Add(CreateNewLabel($"{tier.price}", "0.5 0", "0.75 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", $"10 0", $"0 0"), $"tierbutton_{i}");

        //        var bgc = playerTokens < tier.price ? HexToRGBA(color_dark, 0.5f) : HexToRGBA(colorGreen, 0.3f);
        //        var command = playerTokens < tier.price ? "" : $"cmdsticker unlocksticker {tier.name}";
        //        container.Add(CreateNewButton($"BUY", command, "1 0", "1 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", bgc, $"-60 0", $"-10 0"), $"tierbutton_{i}");

        //        h -= (lh + 2);
        //        i++;
        //    }

        //    return container;
        //}

        CuiElementContainer CreateStickerTrader(BasePlayer player, CuiElementContainer container, PlayerSettings pSet)
        {
            var h = 0f;
            var lh = 40f;
            h -= lh;
            lh = 24f;


            if (pSet.detailsSticker != "")
            {
                container = CreateTradeDetails(player, container, pSet);
                return container;
            }

            var itemList = FilterAndSortTrades(ins.storedMarketData.stickerTrades, player, pSet);
            container = Create_TradeList(player, container, itemList, pSet, "browser_holder", 6, 4, 60f, 103f, 10f, 580);
            return container;
        }

        CuiElementContainer Create_TradeList(BasePlayer player, CuiElementContainer container, List<StickerTrade> items, PlayerSettings pSet, string parent, int maxCols, int maxRows, float iw, float ih, float padding, float autoWidth = 0f)
        {
            var unlockedStickers = GetUnlockedStickers(player);

            float top = -padding;
            float left = padding;
            var col = 0;
            var row = 0;

            var perPage = (maxCols * maxRows);
            var pagination = false;
            if (items.Count > perPage)
            {
                perPage -= 2;
                pagination = true;
            }

            if (autoWidth > 0)
            {
                var net = (autoWidth - (maxCols * padding)) - padding;
                if (net > 0)
                {
                    if (iw == ih)
                        ih = net / maxCols;

                    iw = net / maxCols;
                }
            }

            var start = pSet.itemListPage * perPage;
            var maxPage = Mathf.CeilToInt(items.Count / perPage);
            var sliced = items.Skip(start).Take(perPage);

            if (pagination)
            {
                var arH = ih < 10f ? ih : 10f;

                // prev button
                container.Add(CreateNewButton("PREV", $"cmdsticker pset listpage {(pSet.itemListPage <= 0 ? $"set {maxPage}" : "prev")}", $"0 1", $"0 1", TextAnchor.MiddleCenter, 14, "1 1 1 1", HexToRGBA(colorYellow, 0.5f), $"{left} {top - ih}", $"{left + iw} {top}"), parent, "prev_button");
                // container.Add(CreateNewImage("arrow_left", "prev_button", $"0.5 0.5", $"0.5 0.5", $"-9 {arH / -2}", $"9 {arH / 2}"));

                left += iw + padding; col++;

                // next button
                var nextLeft = ((maxCols - 1) * (iw + padding)) + padding;
                var nextTop = (padding + ((maxRows - 1) * (ih + padding))) * -1;
                container.Add(CreateNewButton("NEXT", $"cmdsticker pset listpage {(pSet.itemListPage >= maxPage ? "set 0" : "next")}", $"0 1", $"0 1", TextAnchor.MiddleCenter, 14, "1 1 1 1", HexToRGBA(colorYellow, 0.5f), $"{nextLeft} {nextTop - ih}", $"{nextLeft + iw} {nextTop}"), parent, "next_button");
                //container.Add(CreateNewImage("arrow_right", "next_button", $"0.5 0.5", $"0.5 0.5", $"-9 {arH / -2}", $"9 {arH / 2}"));

                container.Add(CreateNewLabel($"PAGE {pSet.itemListPage + 1} / {maxPage + 1}", "0.5 0", "0.5 0", TextAnchor.MiddleCenter, 10, "1 1 1 0.8", "-40 -5", "40 10", CuiFont.ROBOTO_REGULAR), parent);

            }

            foreach (var trade in sliced)
            {
                var stickerData = GetStickerData(trade.sticker);
                if (stickerData == null) continue;

                //var cmd = $"cmdsticker pset tab tradedetails {item.sticker}";

                var cmd = $"cmdsticker pset stickerdetails {trade.sticker} {trade.sellerID}";

                var image = trade.sticker;
                var ossX = 0f;
                var ossY = 0f;
                var opacity = 0.15f;
                var imgAMin = $"0 0";
                var imgAMax = $"1 1";

                var bg = GetTierColor(trade.tier, opacity);
                container.Add(CreateUIPanel($"0 1", $"0 1", bg, false, $"{left} {top - ih}", $"{left + iw} {top}"), parent, $"item_{trade.sticker}");

                container.Add(CreateNewButton("", cmd, $"0 0", $"1 1", TextAnchor.MiddleCenter, 13, "1 1 1 1", "0 0 0 0", $"15 30", $"-15 -10"), $"item_{trade.sticker}", $"btn_{trade.sticker}");
                //container.Add(CreateNewButton(item.Key, $"cmdsticker selectitem {item.Key}", $"0 1", $"0 1", TextAnchor.MiddleCenter, 13, "1 1 1 1", bg, $"{left} { top - ih }", $"{ left + iw } { top }"), $"item_{item.Key}", $"btn_{item.Key}");

                float imgW = iw;
                float imgH = ih;

                if (iw > ih)
                    imgW = (stickerData.DUI_WIDTH / stickerData.DUI_HEIGHT) * ih;

                if (iw <= ih)
                    imgH = (stickerData.DUI_HEIGHT / stickerData.DUI_WIDTH) * iw;

                container.Add(CreateNewImage(image, $"btn_{trade.sticker}", imgAMin, imgAMax, $"{-ossX} {-ossY}", $"{ossX} {ossY}"));
                container.Add(CreateUIPanel($"0 1", $"1 1", "0 0 0 0.5", false, $"0 -15", $"0 0"), $"item_{trade.sticker}", $"item_top_{trade.sticker}");
                container.Add(CreateNewLabel(trade.sellerName, $"0 0", $"1 1", TextAnchor.MiddleCenter, 9, "1 1 1 1", $"2 2", $"-2 -2", CuiFont.ROBOTO_REGULAR), $"item_top_{trade.sticker}");
                container.Add(CreateUIPanel($"0 0", $"1 0", "0 0 0 0.5", false, $"0 0", $"0 25"), $"item_{trade.sticker}", $"item_buttons_{trade.sticker}");
                container.Add(CreateNewImage($"{configData.currencySettings.icon_currency}", $"item_buttons_{trade.sticker}", "0 0", "0 1", "3 3", "22 -3"));
                container.Add(CreateNewLabel($"{trade.price}", $"0 0", $"1 1", TextAnchor.MiddleLeft, 12, "1 1 1 1", $"29 2", $"-7 -2", CuiFont.ROBOTO_REGULAR), $"item_buttons_{trade.sticker}");


                left += iw + padding;
                col++;
                if (col >= maxCols)
                {
                    col = 0;
                    row++;

                    top -= (ih + padding);
                    left = padding;

                    if (row >= maxRows)
                        break;
                }
            }

            return container;
        }

        CuiElementContainer CreateTradeDetails(BasePlayer player, CuiElementContainer container, PlayerSettings pSet)
        {
            container = CreateStickerDetails(player, container, pSet, true);
            return container;
        }

        CuiElementContainer CreateStickerDetails(BasePlayer player, CuiElementContainer container, PlayerSettings pSet, bool isTrade = false)
        {
            var sticker = GetStickerData(pSet.detailsSticker);
            if (pSet.detailsSticker == "" || sticker == null) return container;

            var unlockedStickers = GetUnlockedStickers(player);


            StickerTrade trade = null;
            if (isTrade && pSet.detailsSticker != "")
                trade = ins.storedMarketData.stickerTrades.FirstOrDefault(x => x.sellerID == pSet.detailsStickerOwnerID && x.sticker == pSet.detailsSticker);
            

            var isForSale = false;
            var imgMaxSize = 180f;
            var imgW = imgMaxSize;
            var imgH = imgMaxSize;

            if (sticker.DUI_WIDTH > sticker.DUI_HEIGHT)
            {
                imgH = (sticker.DUI_HEIGHT / sticker.DUI_WIDTH) * imgMaxSize;
            }
            else
            {
                imgW = (sticker.DUI_WIDTH / sticker.DUI_HEIGHT) * imgMaxSize;
            }

            var centerH = ((imgMaxSize - imgW) / 2) + 20;
            var centerV = ((imgMaxSize - imgH) / 2) + 20;

            container.Add(CreateUIPanel($"0 1", $"0 1", "1 1 1 0", true, $"{centerH} {-centerV - imgH}", $"{centerH + imgW} {-centerV}"), "browser_holder", $"image_wrap");
            container.Add(CreateNewImage(pSet.detailsSticker, $"image_wrap", "0 0", "1 1", "25 25", "-25 -25"));

            container.Add(CreateUIPanel($"0 0", $"1 1", "0 0 0 0", false, $"220 10", $"-10 -10"), "browser_holder", $"sticker_info");

            var h = -10f;
            var lh = 30f;
            container.Add(CreateUIPanel($"0 1", $"1 1", HexToRGBA(colorYellow, 0.1f), false, $"0 {h - lh}", $"0 {h}"), "sticker_info");
            container.Add(CreateNewLabel($"{pSet.detailsSticker}", $"0 1", $"1 1", TextAnchor.MiddleCenter, 16, "1 1 1 1", $"10 {h - lh}", $"-10 {h}", CuiFont.ROBOTO_BOLD), "sticker_info");

            h -= (lh + 4);
            lh = 20f;

            container.Add(CreateNewLabel($"Tier", $"0 1", $"0.5 1", TextAnchor.MiddleLeft, 14, "1 1 1 1", $"10 {h - lh}", $"0 {h}", CuiFont.ROBOTO_REGULAR), "sticker_info");
            container.Add(CreateNewLabel($"{sticker.TIER.TitleCase()}", $"0.5 1", $"1 1", TextAnchor.MiddleLeft, 14, GetTierColor(sticker.TIER, 0.75f), $"0 {h - lh}", $"-10 {h}", CuiFont.ROBOTO_REGULAR), "sticker_info");


            h -= (lh);

            container.Add(CreateNewLabel($"Total copies", $"0 1", $"0.5 1", TextAnchor.MiddleLeft, 14, "1 1 1 1", $"10 {h - lh}", $"0 {h}", CuiFont.ROBOTO_REGULAR), "sticker_info");
            container.Add(CreateNewLabel($"{(sticker.MAX_OWNERS == 0 ? "Unlimited" : sticker.MAX_OWNERS.ToString())}", $"0.5 1", $"1 1", TextAnchor.MiddleLeft, 14, "1 1 1 1", $"0 {h - lh}", $"-10 {h}", CuiFont.ROBOTO_REGULAR), "sticker_info");

            h -= (lh);

            container.Add(CreateNewLabel($"Owned by players", $"0 1", $"0.5 1", TextAnchor.MiddleLeft, 14, "1 1 1 1", $"10 {h - lh}", $"0 {h}", CuiFont.ROBOTO_REGULAR), "sticker_info");
            container.Add(CreateNewLabel($"{sticker.CURRENT_OWNERS}", $"0.5 1", $"1 1", TextAnchor.MiddleLeft, 14, "1 1 1 1", $"0 {h - lh}", $"-10 {h}", CuiFont.ROBOTO_REGULAR), "sticker_info");

            h -= (lh);

            container.Add(CreateNewLabel($"Remaining in market", $"0 1", $"0.5 1", TextAnchor.MiddleLeft, 14, "1 1 1 1", $"10 {h - lh}", $"0 {h}", CuiFont.ROBOTO_REGULAR), "sticker_info");
            container.Add(CreateNewLabel(sticker.MAX_OWNERS == 0 ? $"Unlimited" : (sticker.MAX_OWNERS - sticker.CURRENT_OWNERS).ToString(), $"0.5 1", $"1 1", TextAnchor.MiddleLeft, 14, "1 1 1 1", $"0 {h - lh}", $"-10 {h}", CuiFont.ROBOTO_REGULAR), "sticker_info");

            h -= (lh);

            var stickerAmountOwned = 0;
            var ownedTxt = $"<color={colorRed}>No</color>";
            if (unlockedStickers.Contains(pSet.detailsSticker))
            {
                stickerAmountOwned = unlockedStickers.FindAll(x => x == pSet.detailsSticker).Count;
                ownedTxt = $"<color={colorGreen}>Yes</color> {stickerAmountOwned}x";
            }
            container.Add(CreateNewLabel($"Owned by you", $"0 1", $"0.5 1", TextAnchor.MiddleLeft, 14, "1 1 1 1", $"10 {h - lh}", $"0 {h}", CuiFont.ROBOTO_REGULAR), "sticker_info");
            container.Add(CreateNewLabel($"{ownedTxt}", $"0.5 1", $"1 1", TextAnchor.MiddleLeft, 14, GetTierColor(sticker.TIER, 0.75f), $"0 {h - lh}", $"-10 {h}", CuiFont.ROBOTO_REGULAR), "sticker_info");

            h -= (lh);

            if (isTrade)
            {
                var ownerName = trade != null ? trade.sellerName : pSet.detailsStickerOwnerID;
                container.Add(CreateNewLabel($"Seller", $"0 1", $"0.5 1", TextAnchor.MiddleLeft, 14, "1 1 1 1", $"10 {h - lh}", $"0 {h}", CuiFont.ROBOTO_REGULAR), "sticker_info");
                container.Add(CreateNewLabel($"{ownerName}", $"0.5 1", $"1 1", TextAnchor.MiddleLeft, 14, "1 1 1 1", $"0 {h - lh}", $"-10 {h}", CuiFont.ROBOTO_REGULAR), "sticker_info");
                h -= (lh);
            }

          
            container.Add(CreateUIPanel($"0 0", $"1 1", "0 0 0 0", false, $"10 10", $"-10 -230"), "browser_holder", $"sticker_actions");

            container.Add(CreateUIPanel($"0 0", $"0.5 1", "0 0 0 0", false, $"0 0", $"-10 0"), "sticker_actions", $"sticker_actions_market");
            container.Add(CreateUIPanel($"0.5 0", $"1 1", "0 0 0 0", false, $"10 0", $"0 0"), "sticker_actions", $"sticker_actions_recycle");


            if (storedMarketData.stickerTrades.Any(x => x.sellerID == player.UserIDString && x.sticker == pSet.detailsSticker))
                isForSale = true;


            h = 0;
            var pad = 10f;
            var lft = 10f;
            var labelW = 50f;
            var labelWWide = 100f;


            if (isTrade || (pSet.detailsSticker != "" && unlockedStickers.Contains(pSet.detailsSticker)))
            {
                container.Add(CreateUIPanel($"0 1", $"1 1", HexToRGBA(colorYellow, 0.1f), false, $"10 -30", $"-10 0"), "sticker_actions_market", "market_title");
                container.Add(CreateNewLabel($"STICKER MARKET", $"0 0", $"1 1", TextAnchor.MiddleCenter, 16, "1 1 1 1", $"0 0", $"0 0", CuiFont.ROBOTO_BOLD), "market_title");
                h -= (40);
            }


       
            if (pSet.detailsSticker != "" && unlockedStickers.Contains(pSet.detailsSticker))
            {
                // players owns this sticker
                if (isForSale)
                {
                    container.Add(CreateNewLabel($"You are currently selling this sticker on the sticker market", $"0 1", $"1 1", TextAnchor.MiddleLeft, 12, "1 1 1 1", $"10 {h - lh}", $"-10 {h}", CuiFont.ROBOTO_REGULAR), "sticker_actions_market");
                    h -= (lh);

                    container.Add(CreateNewButton($"SHOW MARKET", $"cmdsticker pset tab stickertrader", "0 1", "0 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", HexToRGBA(colorYellow, 0.3f), $"{lft} {h - lh}", $"{lft + labelWWide} {h}"), $"sticker_actions_market");

                    lft += labelWWide + pad;
                    container.Add(CreateNewButton($"REMOVE FROM MARKET", $"cmdsticker cancelsell {pSet.detailsSticker}", "0 1", "0 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", HexToRGBA(colorRed, 0.3f), $"{lft} {h - lh}", $"{lft + labelWWide + 35f} {h}"), $"sticker_actions_market");
                }
                else
                {
                    container.Add(CreateNewLabel($"Add this sticker to the sticker market", $"0 1", $"1 1", TextAnchor.MiddleLeft, 12, "1 1 1 1", $"10 {h - lh}", $"-10 {h}", CuiFont.ROBOTO_REGULAR), "sticker_actions_market");
                    h -= (lh);

                    container.Add(CreateNewLabel($"SELL FOR", "0 1", "0 1", TextAnchor.MiddleLeft, 12, "1 1 1 1", $"{lft} {h - lh}", $"{labelW} {h}"), $"sticker_actions_market");

                    lft += labelW + pad;

                    container.Add(CreateUIPanel("0 1", "0 1", $"{HexToRGBA(color_light, 0.25f)}", true, $"{lft} {h - lh}", $"{lft + labelW} {h}", true), "sticker_actions_market", "input_wrap");
                    container.Add(CreateInputField($"{pSet.inputTradeAmount}", "cmdsticker pset settradeamount", "input_wrap", "0 0", "1 1", $"2 2", $"-2 -2", TextAnchor.MiddleCenter));

                    lft += labelW + pad;

                    container.Add(CreateNewButton($"SELL ON MARKET", $"cmdsticker sellsticker {pSet.detailsSticker}", "0 1", "0 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", HexToRGBA(colorGreen, 0.3f), $"{lft} {h - lh}", $"{lft + labelWWide} {h}"), $"sticker_actions_market");
                }

                h -= (lh);
            }
            else
            {
                //container.Add(CreateNewLabel($"You don't own this sticker", $"0 1", $"1 1", TextAnchor.MiddleLeft, 13, "1 1 1 1", $"10 {h - lh}", $"-10 {h}", CuiFont.ROBOTO_REGULAR), "sticker_actions");
                //h -= (lh);
            }

         

            if (isTrade)
            {
              
                h -= (lh);
                if (trade.sellerID == player.UserIDString)
                {
                    //container.Add(CreateNewButton($"This is your own offer", $"", $"0 1", $"0 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", HexToRGBA(color_dark, 0.7f), $"10 {h - (lh * 2)}", $"120 {h}", CuiFont.ROBOTO_REGULAR), "sticker_actions_market");
                }
                else
                {
                    container.Add(CreateNewLabel($"Buy sticker from <color={colorYellow}>{trade.sellerName}</color>", $"0 1", $"1 1", TextAnchor.MiddleLeft, 12, "1 1 1 1", $"10 {h - lh}", $"-10 {h}", CuiFont.ROBOTO_REGULAR), "sticker_actions_market");
                    h -= (lh);
                    container.Add(CreateNewLabel($"for <color={colorOrange}>{trade.price}</color> {configData.currencySettings.currencyName}?", $"0 1", $"1 1", TextAnchor.MiddleLeft, 12, "1 1 1 1", $"10 {h - lh}", $"-10 {h}", CuiFont.ROBOTO_REGULAR), "sticker_actions_market");
                    h -= (lh);

                    BasePlayer seller;
                    if (GetCurrency() == StickerCurrency.Item && (!FindPlayer(trade.sellerID, out seller) || !seller.IsConnected))
                    {
                        container.Add(CreateNewButton($"OFFLINE", $"cmdsticker buytrade {trade.sellerID} {trade.sticker}", $"0 1", $"0 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", HexToRGBA(colorRed, 0.25f), $"10 {h - (lh)}", $"100 {h}", CuiFont.ROBOTO_REGULAR), "sticker_actions_market");
                    }
                    else
                    {
                        container.Add(CreateNewButton($"BUY NOW", $"cmdsticker buytrade {trade.sellerID} {trade.sticker}", $"0 1", $"0 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", HexToRGBA(colorGreen, 0.25f), $"10 {h - (lh)}", $"100 {h}", CuiFont.ROBOTO_REGULAR), "sticker_actions_market");
                    }
                }
            }
            else
            {
                var amountRecyclable = stickerAmountOwned;
                if (isForSale) amountRecyclable -= 1;

                if (configData.recycleSettings.recyclingEnabled && amountRecyclable > 0)
                {
                    h = 0;

                    var stickerWorth = GetStickerRecyclePrize(pSet.detailsSticker, stickerAmountOwned);

                    container.Add(CreateUIPanel($"0 1", $"1 1", HexToRGBA(colorYellow, 0.1f), false, $"10 {h - 30}", $"-10 {h}"), "sticker_actions_recycle", "recycle_title");
                    container.Add(CreateNewLabel($"RECYCLE", $"0 0", $"1 1", TextAnchor.MiddleCenter, 16, "1 1 1 1", $"0 0", $"0 0", CuiFont.ROBOTO_BOLD), "recycle_title");
                    h -= (40);

                    container.Add(CreateNewLabel($"Recycle stickers and get tokens in return.", $"0 1", $"1 1", TextAnchor.MiddleLeft, 12, "1 1 1 1", $"10 {h - lh}", $"-10 {h}", CuiFont.ROBOTO_REGULAR), "sticker_actions_recycle");
                    h -= (lh);

                    lft = 10f;

                    container.Add(CreateUIPanel($"0 1", $"1 1", HexToRGBA(color_dark, 0.5f), true, $"{lft} {h - lh}", $"{-lft} {h}"), "sticker_actions_recycle", $"recycle_row_head");

                    container.Add(CreateNewLabel($"RECYCLE", "0 0", "0 1", TextAnchor.MiddleLeft, 12, "1 1 1 1", $"{lft} {0}", $"{lft + labelWWide} {0}"), $"recycle_row_head");
                    lft += labelWWide;
                    container.Add(CreateNewLabel($"EARN", "0 0", "0 1", TextAnchor.MiddleLeft, 12, "1 1 1 1", $"{lft} {0}", $"{lft + labelWWide} {0}"), $"recycle_row_head");

                    h -= lh;

                    lft = 10f;


                 

                    if (amountRecyclable > 5)
                        amountRecyclable = 5;


                    for (var oi = 1; oi <= amountRecyclable; oi++)
                    {
                        float earn = AddRecyclePrizeMultiplier(stickerWorth, oi);
                        var tokens = Mathf.RoundToInt(earn);

                        container.Add(CreateUIPanel($"0 1", $"1 1", HexToRGBA(color_light, 0.05f), true, $"{lft} {h - lh}", $"{-lft} {h}"), "sticker_actions_recycle", $"recycle_row_{oi}");


                        container.Add(CreateNewLabel($"{oi} STICKER{(oi > 1 ? "S" : "")}", "0 0", "0 1", TextAnchor.MiddleLeft, 12, "1 1 1 1", $"{lft} {0}", $"{lft + labelWWide} {0}"), $"recycle_row_{oi}");
                        lft += labelWWide + 2f;

                        container.Add(CreateNewLabel($"{tokens} TOKEN{(tokens > 1 ? "S" : "")}", "0 0", "0 1", TextAnchor.MiddleLeft, 12, "1 1 1 1", $"{lft} {0}", $"{lft + labelWWide} {0}"), $"recycle_row_{oi}");
                        lft += labelWWide + 2f;

                        container.Add(CreateNewButton($"RECYCLE {oi}", $"cmdsticker recyclestickers {oi}", "1 0", "1 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", HexToRGBA(colorGreen, 0.3f), $"{-75f} {1f}", $"{-1f} {-1f}"), $"recycle_row_{oi}");

                        h -= (lh + 2f);
                        lft = 10f;
                    }
                }
               
            }


            container.Add(CreateNewButton($"CLOSE", "cmdsticker pset stickerdetails close", "1 0", "1 0", TextAnchor.MiddleCenter, 12, "1 1 1 1", HexToRGBA(colorRed, 0.5f), $"-60 10", $"-11 35"), "sticker_actions");

            return container;

        }

        CuiElementContainer Create_ItemList(BasePlayer player, CuiElementContainer container, Dictionary<string, StickerData> items, PlayerSettings pSet, string parent, int maxCols, int maxRows, float iw, float ih, float padding, float autoWidth = 0f)
        {
            float top = -padding;
            float left = padding;
            var col = 0;
            var row = 0;

            var unlockedStickers = GetUnlockedStickers(player);


            var perPage = (maxCols * maxRows);
            var pagination = false;
            if (items.Count > perPage)
            {
                perPage -= 2;
                pagination = true;
            }

            if (autoWidth > 0)
            {
                var net = (autoWidth - (maxCols * padding)) - padding;
                if (net > 0)
                {
                    if (iw == ih)
                        ih = net / maxCols;

                    iw = net / maxCols;
                }
            }

            var start = pSet.itemListPage * perPage;
            var maxPage = Mathf.CeilToInt(items.Count / perPage);
            var sliced = items.Skip(start).Take(perPage);


            if (pagination)
            {
                var arH = ih < 10f ? ih : 10f;

                // prev button
                container.Add(CreateNewButton("PREV", $"cmdsticker pset listpage {(pSet.itemListPage <= 0 ? $"set {maxPage}" : "prev")}", $"0 1", $"0 1", TextAnchor.MiddleCenter, 14, "1 1 1 1", HexToRGBA(colorYellow, 0.5f), $"{left} {top - ih}", $"{left + iw} {top}"), parent, "prev_button");
                // container.Add(CreateNewImage("arrow_left", "prev_button", $"0.5 0.5", $"0.5 0.5", $"-9 {arH / -2}", $"9 {arH / 2}"));

                left += iw + padding; col++;

                // next button
                var nextLeft = ((maxCols - 1) * (iw + padding)) + padding;
                var nextTop = (padding + ((maxRows - 1) * (ih + padding))) * -1;
                container.Add(CreateNewButton("NEXT", $"cmdsticker pset listpage {(pSet.itemListPage >= maxPage ? "set 0" : "next")}", $"0 1", $"0 1", TextAnchor.MiddleCenter, 14, "1 1 1 1", HexToRGBA(colorYellow, 0.5f), $"{nextLeft} {nextTop - ih}", $"{nextLeft + iw} {nextTop}"), parent, "next_button");
                //container.Add(CreateNewImage("arrow_right", "next_button", $"0.5 0.5", $"0.5 0.5", $"-9 {arH / -2}", $"9 {arH / 2}"));


                container.Add(CreateNewLabel($"PAGE {pSet.itemListPage + 1} / {maxPage + 1}", "0.5 0", "0.5 0", TextAnchor.MiddleCenter, 10, "1 1 1 0.8", "-40 -5", "40 10", CuiFont.ROBOTO_REGULAR), parent);
            }


            // w  512  1  10
            // h  256

            foreach (var item in sliced)
            {
                var cmd = $"cmdsticker pset stickerdetails {item.Key}";
                var image = item.Key;
                var ossX = 0f;
                var ossY = 0f;
                var opacity = 0.15f;
                var imgAMin = $"0.5 0.5";
                var imgAMax = $"0.5 0.5";

                var imgMaxWidth = iw - 20;
                var imgMaxHeight = ih - 50;


                // TODO maybe show them images anyway
                var amountOwned = unlockedStickers.FindAll(x => x == item.Key).Count();
                if (amountOwned == 0)
                {
                    // cmd = "";
                    image = "icon_lock_url";
                    ossX = 26f;
                    ossY = 24f;
                    opacity = 0.07f;
                    imgAMin = $"0.5 0.5";
                    imgAMax = $"0.5 0.5";
                }
                else
                {
                    if (item.Value.DUI_WIDTH >= item.Value.DUI_HEIGHT)
                    {
                        ossX = imgMaxWidth / 2;

                        var imgHeight = ((item.Value.DUI_HEIGHT / item.Value.DUI_WIDTH) * imgMaxWidth);
                        ossY = imgHeight / 2;
                    }
                    else
                    {
                        ossY = imgMaxHeight / 2;

                        var imgWidth = ((item.Value.DUI_WIDTH / item.Value.DUI_HEIGHT) * imgMaxHeight);
                        ossX = imgWidth / 2;
                    }
                }


                var bg = GetTierColor(item.Value.TIER, opacity);
                container.Add(CreateUIPanel($"0 1", $"0 1", bg, false, $"{left} {top - ih}", $"{left + iw} {top}"), parent, $"item_{item.Key}");

                container.Add(CreateNewButton("", cmd, $"0 0", $"1 1", TextAnchor.MiddleCenter, 13, "1 1 1 1", "0 0 0 0", $"10 30", $"-10 -30"), $"item_{item.Key}", $"btn_{item.Key}");
                //container.Add(CreateNewButton(item.Key, $"cmdsticker selectitem {item.Key}", $"0 1", $"0 1", TextAnchor.MiddleCenter, 13, "1 1 1 1", bg, $"{left} { top - ih }", $"{ left + iw } { top }"), $"item_{item.Key}", $"btn_{item.Key}");

                container.Add(CreateNewImage(image, $"btn_{item.Key}", imgAMin, imgAMax, $"{-ossX} {-ossY}", $"{ossX} {ossY}"));

                container.Add(CreateUIPanel($"0 1", $"1 1", "0 0 0 0.5", false, $"0 -21", $"0 0"), $"item_{item.Key}", $"item_buttons_{item.Key}");
                var blf = 2f;

                container.Add(CreateNewButton("", $"cmdsticker pset sendview {item.Key}", $"0 0", $"0 1", TextAnchor.MiddleCenter, 13, "1 1 1 1", HexToRGBA(colorOrange, 0.1f), $"{blf} 2", $"{blf + 17f} -2"), $"item_buttons_{item.Key}", $"btnviewinfo_{item.Key}");
                container.Add(CreateNewImage("icon_eye_url", $"btnviewinfo_{item.Key}", "0 0", "1 1", "2 2", "-2 -2"));

                blf += 17f + 2f;


                if (HasPermission(player.UserIDString, permAdmin))
                {
                    container.Add(CreateNewButton("", $"cmdsticker pset editsticker {item.Key}", $"0 0", $"0 1", TextAnchor.MiddleCenter, 13, "1 1 1 1", HexToRGBA(colorOrange, 0.1f), $"{blf} 2", $"{blf + 17f} -2"), $"item_buttons_{item.Key}", $"btn_{item.Key}");
                    container.Add(CreateNewImage("icon_edit_url2", $"btn_{item.Key}", "0 0", "1 1", "2 2", "-2 -2"));

                    blf += 17f + 2f;

                    if (!unlockedStickers.Contains(item.Key))
                    {
                        container.Add(CreateNewButton("", $"cmdsticker pset unlock {item.Key}", $"0 0", $"0 1", TextAnchor.MiddleCenter, 13, "1 1 1 1", HexToRGBA(colorOrange, 0.1f), $"{blf} 2", $"{blf + 17f} -2"), $"item_buttons_{item.Key}", $"btn2_{item.Key}");
                        container.Add(CreateNewImage("icon_lock_open_url", $"btn2_{item.Key}", "0 0", "1 1", "3 3", "-3 -3"));
                    }
                    else
                    {
                        container.Add(CreateNewButton("", $"cmdsticker pset lock {item.Key}", $"0 0", $"0 1", TextAnchor.MiddleCenter, 13, "1 1 1 1", HexToRGBA(colorOrange, 0.1f), $"{blf} 2", $"{blf + 17f} -2"), $"item_buttons_{item.Key}", $"btn2_{item.Key}");
                        container.Add(CreateNewImage("icon_lock_url2", $"btn2_{item.Key}", "0 0", "1 1", "3 3", "-3 -3"));
                    }

                    blf += 17f + 2f;
                }

                var clr = amountOwned > 0 ? HexToRGBA(colorGreen, 0.7f) : HexToRGBA(colorRed, 0.7f);
                container.Add(CreateNewLabel($"{amountOwned}x", $"1 0", $"1 1", TextAnchor.MiddleRight, 12, clr, $"-36 2", $"-4 -2"), $"item_buttons_{item.Key}");


                container.Add(CreateUIPanel($"0 0", $"1 0", "0 0 0 0.5", false, $"0 0", $"0 21"), $"item_{item.Key}", $"item_info_{item.Key}");

                // container.Add(CreateNewButton("?", "", $"0 0", $"0 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", HexToRGBA(colorYellow, 0.3f), $"2 2", $"27 -2"), $"item_buttons_{item.Key}");
                container.Add(CreateNewLabel(item.Key, $"0 0", $"1 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", $"2 2", $"-2 -2", CuiFont.ROBOTO_REGULAR), $"item_info_{item.Key}");




                left += iw + padding;
                col++;
                if (col >= maxCols)
                {
                    col = 0;
                    row++;

                    top -= (ih + padding);
                    left = padding;

                    if (row >= maxRows)
                        break;
                }
            }

            return container;
        }

        CuiElementContainer CreateInfoPage(BasePlayer player, CuiElementContainer container, PlayerSettings pSet)
        {
            var info = "";

            info += $"<size=16><color={colorYellow}>ABOUT STICKERS</color></size>\n" +
                "Stickers are player-unlockable images showing up on your screen when certain :codes: are used in the chat\n" +
                $"To hide or show stickers use: <color={colorOrange}>/sticker hide</color> or <color={colorOrange}>/sticker show</color>\n\n";


            info +=
                $"<size=16><color={colorYellow}>BROWSE STICKERS</color></size>\n" +
                "View all available stickers and check what's missing in your collection. Click on a sticker to view it's details.\n\n" +

                $"<size=16><color={colorYellow}>BUY STICKERS</color></size>\n" +
                $"You can buy sticker tokens to unlock random stickers with {configData.currencySettings.currencyName} in this tab.\n\n";

            if (configData.stickerTraderSettings.tradingEnabled)
            {
                info +=
                    $"<size=16><color={colorYellow}>TRADE STICKERS</color></size>\n" +
                    "Buy and sell stickers in the sticker market. Open sticker details to add your own to the market.\n" +
                    $"The currency used is {configData.currencySettings.currencyName}.\n" +
                    "Click any of your own stickers in the STICKER BROWSER to view details or to add your own stickers to the market\n\n";
            }


            info += $"<size=16><color={colorYellow}>STICKER DETAILS</color> (click sticker to open)</size>\n" +
                "Every sticker has a maximum amount of copies that can exist, you can check these stats here.\n" +
                "In general, cheaper stickers can be owned by more players than the more expensive stickers can.\n";

            if(configData.stickerTraderSettings.tradingEnabled)
            {
                info += "If you own a sticker, you can try to sell it on the market at the bottom of this tab.\n";
            }

            if (configData.recycleSettings.recyclingEnabled)
            {
                info += "Recycle 1 or more of the same sticker to earn some sticker tokens.\n" +
                    "The recycle rewards are based on the availability of the sticker and the amount of copies recycled.\n";
            }

            info += $"\n<size=16><color={colorYellow}>INACTIVITY</color></size>\n" +
                $"After {configData.basicSettings.maxDaysInactive} days of inactivity, every 24 hours {configData.basicSettings.dailyTakeBack} random sticker(s) will be removed from your inventory (and become available for others again).";


            //info += "\n\nMore info is coming soon.\n";

            container.Add(CreateNewLabel($"{info}", $"0 0", $"1 1", TextAnchor.UpperLeft, 12, "1 1 1 1", $"20 20", $"-20 -20"), $"browser_holder");
            return container;
        }

        CuiElementContainer CreateStickerEditor(BasePlayer player, CuiElementContainer container, PlayerSettings pSet)
        {
            var lh = 24f;
            var pad = 10f;
            var h = 0f;
            var labelW = 55f;
            var left = (pad * 3) + (labelW * 2);

            var settings = storedStickers.stickerData;

            container.Add(CreateUIPanel($"0 0", $"1 1", "0 0 0 0", true, $"{pad} {pad}", $"{-pad} {-pad}", true), UIPanelStickerBrowser, "edit_sticker_wrap");

            container.Add(CreateNewButton("BACK", "cmdsticker pset stopediting", "0 1", "0.5 1", TextAnchor.MiddleCenter, 11, "1 1 1 1", HexToRGBA(colorOrange, 0.7f), $"{pad} {h - lh}", $"{-pad} {h}"), $"edit_sticker_wrap");
            container.Add(CreateNewLabel($"{pSet.editSticker.ToUpper()}", "0.5 1", "1 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", $"{pad} {h - lh}", $"{-pad} {h}"), $"edit_sticker_wrap");

            h -= lh + 3f;


            var pickerHeight = 56f;
            var ppad = 2f;
            var pbsize = (pickerHeight - (4 * ppad)) / 3;
            var pH = -ppad;
            var pL = ppad;
            var clr = HexToRGBA(color_dark, 0.7f);
            var active = HexToRGBA(colorBlue, 0.7f);

            container.Add(CreateUIPanel("0 1", "0 1", $"{HexToRGBA(color_light, 0.25f)}", false, $"{pad} {h - pickerHeight}", $"{pad + pickerHeight} {h}"), "edit_sticker_wrap", "pos_picker");

            #region pickerbuttons
            // TOP
            container.Add(CreateNewButton("", "cmdsticker pset setstickerpos topleft", "0 1", "0 1", TextAnchor.MiddleCenter, 11, "1 1 1 1", settings.POS == "topleft" ? active : clr, $"{pL} {pH - pbsize}", $"{pL + pbsize} {pH}"), $"pos_picker");
            pL += pbsize + ppad;
            container.Add(CreateNewButton("", "cmdsticker pset setstickerpos topcenter", "0 1", "0 1", TextAnchor.MiddleCenter, 11, "1 1 1 1", settings.POS == "topcenter" ? active : clr, $"{pL} {pH - pbsize}", $"{pL + pbsize} {pH}"), $"pos_picker");
            pL += pbsize + ppad;
            container.Add(CreateNewButton("", "cmdsticker pset setstickerpos topright", "0 1", "0 1", TextAnchor.MiddleCenter, 11, "1 1 1 1", settings.POS == "topright" ? active : clr, $"{pL} {pH - pbsize}", $"{pL + pbsize} {pH}"), $"pos_picker");
            pH -= (pbsize + ppad); pL = ppad;

            // MIDDLE
            container.Add(CreateNewButton("", "cmdsticker pset setstickerpos middleleft", "0 1", "0 1", TextAnchor.MiddleCenter, 11, "1 1 1 1", settings.POS == "middleleft" ? active : clr, $"{pL} {pH - pbsize}", $"{pL + pbsize} {pH}"), $"pos_picker");
            pL += pbsize + ppad;
            container.Add(CreateNewButton("", "cmdsticker pset setstickerpos middlecenter", "0 1", "0 1", TextAnchor.MiddleCenter, 11, "1 1 1 1", settings.POS == "middlecenter" ? active : clr, $"{pL} {pH - pbsize}", $"{pL + pbsize} {pH}"), $"pos_picker");
            pL += pbsize + ppad;
            container.Add(CreateNewButton("", "cmdsticker pset setstickerpos middleright", "0 1", "0 1", TextAnchor.MiddleCenter, 11, "1 1 1 1", settings.POS == "middleright" ? active : clr, $"{pL} {pH - pbsize}", $"{pL + pbsize} {pH}"), $"pos_picker");
            pH -= (pbsize + ppad); pL = ppad;

            // BOTTOM
            container.Add(CreateNewButton("", "cmdsticker pset setstickerpos bottomleft", "0 1", "0 1", TextAnchor.MiddleCenter, 11, "1 1 1 1", settings.POS == "bottomleft" ? active : clr, $"{pL} {pH - pbsize}", $"{pL + pbsize} {pH}"), $"pos_picker");
            pL += pbsize + ppad;
            container.Add(CreateNewButton("", "cmdsticker pset setstickerpos bottomcenter", "0 1", "0 1", TextAnchor.MiddleCenter, 11, "1 1 1 1", settings.POS == "bottomcenter" ? active : clr, $"{pL} {pH - pbsize}", $"{pL + pbsize} {pH}"), $"pos_picker");
            pL += pbsize + ppad;
            container.Add(CreateNewButton("", "cmdsticker pset setstickerpos bottomright", "0 1", "0 1", TextAnchor.MiddleCenter, 11, "1 1 1 1", settings.POS == "bottomright" ? active : clr, $"{pL} {pH - pbsize}", $"{pL + pbsize} {pH}"), $"pos_picker");
            pH -= (pbsize + ppad); pL = ppad;
            #endregion


            container.Add(CreateUIPanel("0 1", "1 1", $"{HexToRGBA(color_light, 0.25f)}", false, $"{pad + pickerHeight + pad} {h - pickerHeight}", $"{-pad} {h}"), "edit_sticker_wrap", "pos_sticker_offset");

            //var nlabW = 40f;
            ppad = 4f;
            pH = -3f;

            var txtLeft = settings.POS.EndsWith("right") ? "RIGHT" : "LEFT";
            container.Add(CreateNewLabel($"{txtLeft}", "0.02 1", "0.24 1", TextAnchor.MiddleLeft, 10, "1 1 1 1", $"{ppad} {pH - lh}", $"{-ppad} {pH}"), $"pos_sticker_offset");
            container.Add(CreateUIPanel("0.25 1", "0.49 1", $"{HexToRGBA(color_light, 0.25f)}", false, $"{ppad} {pH - lh}", $"{-ppad} {pH}"), "pos_sticker_offset");
            container.Add(CreateInputField($"{settings.LEFT}", "cmdsticker pset setstickeroption hor", "pos_sticker_offset", "0.25 1", "0.49 1", $"{ppad} {pH - lh}", $"{-ppad} {pH}", TextAnchor.MiddleCenter));


            var txtTop = settings.POS.StartsWith("bottom") ? "BOTTOM" : "TOP";
            container.Add(CreateNewLabel($"{txtTop}", "0.51 1", "0.74 1", TextAnchor.MiddleLeft, 10, "1 1 1 1", $"{ppad} {pH - lh}", $"{-ppad} {pH}"), $"pos_sticker_offset");
            container.Add(CreateUIPanel("0.75 1", "1 1", $"{HexToRGBA(color_light, 0.25f)}", false, $"{ppad} {pH - lh}", $"{-ppad} {pH}"), "pos_sticker_offset");
            container.Add(CreateInputField($"{settings.TOP}", "cmdsticker pset setstickeroption ver", "pos_sticker_offset", "0.75 1", "1 1", $"{ppad} {pH - lh}", $"{-ppad} {pH}", TextAnchor.MiddleCenter));

            pH -= (lh + 2f);

            container.Add(CreateNewLabel($"WIDTH", "0.02 1", "0.24 1", TextAnchor.MiddleLeft, 10, "1 1 1 1", $"{ppad} {pH - lh}", $"{-ppad} {pH}"), $"pos_sticker_offset");
            container.Add(CreateUIPanel("0.25 1", "0.49 1", $"{HexToRGBA(color_light, 0.25f)}", false, $"{ppad} {pH - lh}", $"{-ppad} {pH}"), "pos_sticker_offset");
            container.Add(CreateInputField($"{settings.DUI_WIDTH}", "cmdsticker pset setstickeroption w", "pos_sticker_offset", "0.25 1", "0.49 1", $"{ppad} {pH - lh}", $"{-ppad} {pH}", TextAnchor.MiddleCenter));

            //var ll = pad + nlabW + pad;
            container.Add(CreateNewLabel($"HEIGHT", "0.51 1", "0.74 1", TextAnchor.MiddleLeft, 10, "1 1 1 1", $"{ppad} {pH - lh}", $"{-ppad} {pH}"), $"pos_sticker_offset");
            container.Add(CreateUIPanel("0.75 1", "1 1", $"{HexToRGBA(color_light, 0.25f)}", false, $"{ppad} {pH - lh}", $"{-ppad} {pH}"), "pos_sticker_offset");
            container.Add(CreateInputField($"{settings.DUI_HEIGHT}", "cmdsticker pset setstickeroption h", "pos_sticker_offset", "0.75 1", "1 1", $"{ppad} {pH - lh}", $"{-ppad} {pH}", TextAnchor.MiddleCenter));


            h -= pickerHeight + 2f;

            var lf = pad;
            container.Add(CreateNewLabel($"GROUP", "0 1", "0 1", TextAnchor.MiddleLeft, 11, "1 1 1 1", $"{lf} {h - lh}", $"{lf + labelW} {h}"), $"edit_sticker_wrap");
            lf += labelW + pad + 1f;

            var browseW = 32f;
            container.Add(CreateUIPanel("0 1", "1 1", $"{HexToRGBA(color_light, 0.25f)}", false, $"{lf} {h - lh}", $"{-pad - browseW - pad} {h}"), "edit_sticker_wrap", "group_field_bg");
            container.Add(CreateInputField($"{settings.COLLECTION}", "cmdsticker pset setcollection", "group_field_bg", "0 0", "1 1", $"{pad} 0", $"{-pad} 0", TextAnchor.MiddleLeft));
            lf += (labelW * 2) + pad;
            container.Add(CreateNewButton($"...", "cmdsticker pset grouppicker", "1 1", "1 1", TextAnchor.MiddleCenter, 11, "1 1 1 1", HexToRGBA(colorLightBlue, 0.25f), $"{-pad - browseW} {h - lh}", $"{-pad} {h}"), $"edit_sticker_wrap");



            h -= lh + 2f;


            container.Add(CreateNewLabel($"TIER", "0 1", "0 1", TextAnchor.MiddleLeft, 11, "1 1 1 1", $"{pad} {h - lh}", $"{pad + labelW} {h}"), $"edit_sticker_wrap");

            container.Add(CreateUIPanel("0 1", "1 1", $"0 0 0 0", false, $"{pad + labelW} {h - lh}", $"{-pad} {h}"), "edit_sticker_wrap", "tier_editor");
            container.Add(CreateNewButton("COMN", "cmdsticker pset tier common", "0 0", "0.2 1", TextAnchor.MiddleCenter, 11, "1 1 1 1", HexToRGBA(colorCommon, settings.TIER == "common" ? 0.75f : 0.22f), $"0 0", $"0 0"), $"tier_editor");
            container.Add(CreateNewButton("RARE", "cmdsticker pset tier rare", "0.2 0", "0.4 1", TextAnchor.MiddleCenter, 11, "1 1 1 1", HexToRGBA(colorRare, settings.TIER == "rare" ? 0.75f : 0.22f), $"0 0", $"0 0"), $"tier_editor");
            container.Add(CreateNewButton("EPIC", "cmdsticker pset tier epic", "0.4 0", "0.6 1", TextAnchor.MiddleCenter, 11, "1 1 1 1", HexToRGBA(colorEpic, settings.TIER == "epic" ? 0.75f : 0.22f), $"0 0", $"0 0"), $"tier_editor");
            container.Add(CreateNewButton("LEGY", "cmdsticker pset tier legendary", "0.6 0", "0.8 1", TextAnchor.MiddleCenter, 11, "1 1 1 1", HexToRGBA(colorLegendary, settings.TIER == "legendary" ? 0.75f : 0.22f), $"0 0", $"0 0"), $"tier_editor");
            container.Add(CreateNewButton("HIDE", "cmdsticker pset tier hidden", "0.8 0", "1 1", TextAnchor.MiddleCenter, 11, "1 1 1 1", HexToRGBA(color_light, settings.TIER == "hidden" ? 0.75f : 0.22f), $"0 0", $"0 0"), $"tier_editor");


            h -= lh + 2f;

            lf = pad;
            container.Add(CreateNewLabel($"TOTAL", "0 1", "0 1", TextAnchor.MiddleLeft, 11, "1 1 1 1", $"{lf} {h - lh}", $"{lf + labelW} {h}"), $"edit_sticker_wrap");
            lf = lf + labelW;
            container.Add(CreateUIPanel("0 1", "0 1", $"{HexToRGBA(color_light, 0.25f)}", false, $"{lf} {h - lh}", $"{lf + labelW} {h}"), "edit_sticker_wrap");
            container.Add(CreateInputField($"{settings.MAX_OWNERS}", "cmdsticker pset setmaxowners", "edit_sticker_wrap", "0 1", "0 1", $"{lf} {h - lh}", $"{lf + labelW} {h}", TextAnchor.MiddleCenter));
            lf = lf + labelW + pad;
            container.Add(CreateNewLabel($"OWNERS", "0 1", "0 1", TextAnchor.MiddleCenter, 11, "1 1 1 1", $"{lf} {h - lh}", $"{lf + labelW} {h}"), $"edit_sticker_wrap");
            lf = lf + labelW + pad;
            container.Add(CreateNewLabel($"{settings.CURRENT_OWNERS}", "0 1", "0 1", TextAnchor.MiddleLeft, 11, "1 1 1 1", $"{lf} {h - lh}", $"{lf + labelW} {h}"), $"edit_sticker_wrap");


            h -= lh + 2f;
            lf = pad;
            if (pSet.askConfirmDelete)
            {
                container.Add(CreateNewLabel($"CONFIRM", "0 1", "0 1", TextAnchor.MiddleLeft, 11, "1 1 1 1", $"{lf} {h - lh}", $"{lf + labelW} {h}"), $"edit_sticker_wrap");
                lf = lf + labelW;
                container.Add(CreateNewButton($"DELETE", "cmdsticker pset removesticker", "0 1", "0 1", TextAnchor.MiddleCenter, 11, "1 1 1 1", HexToRGBA(colorRed, 0.25f), $"{lf} {h - lh}", $"{lf + labelW} {h}"), $"edit_sticker_wrap");
                lf = lf + labelW + pad;
                container.Add(CreateNewButton($"CANCEL", "cmdsticker pset canceldelete", "0 1", "0 1", TextAnchor.MiddleCenter, 11, "1 1 1 1", HexToRGBA(colorGreen, 0.25f), $"{lf} {h - lh}", $"{lf + labelW} {h}"), $"edit_sticker_wrap");
            }
            else
            {
                container.Add(CreateNewButton($"DELETE", "cmdsticker pset removesticker", "1 1", "1 1", TextAnchor.MiddleCenter, 11, "1 1 1 1", HexToRGBA(colorRed, 0.25f), $"{-labelW - pad} {h - lh}", $"{-pad} {h}"), $"edit_sticker_wrap");
            }


            return container;

        }

        CuiElementContainer CreateGroupPicker(BasePlayer player, CuiElementContainer container, PlayerSettings pSet)
        {
            var lh = 24f;
            var pad = 10f;
            var btn_pad = 2f;
            var btn_h = 20f;
            var btn_w = 64f;
            var h = 0f;
            var labelW = 55f;
            var left = (pad * 3) + (labelW * 2);
            var max_rows = 7;
            var max_cols = 4;
            btn_w = ((250f - btn_pad) / max_cols) - btn_pad;

            var settings = storedStickers.stickerData;

            container.Add(CreateUIPanel($"0 0", $"1 1", "0 0 0 0", true, $"{pad} {pad}", $"{-pad} {-pad}", true), UIPanelStickerBrowser, "edit_sticker_wrap");

            container.Add(CreateNewButton("CANCEL", "cmdsticker pset grouppicker", "0 1", "0.25 1", TextAnchor.MiddleCenter, 11, "1 1 1 1", HexToRGBA(colorOrange, 0.7f), $"{pad} {h - lh}", $"{-pad} {h}"), $"edit_sticker_wrap");
            container.Add(CreateNewLabel($"SELECT GROUP FOR {pSet.editSticker.ToUpper()}", "0.25 1", "1 1", TextAnchor.MiddleCenter, 12, "1 1 1 1", $"{pad} {h - lh}", $"{-pad} {h}"), $"edit_sticker_wrap");

            h -= (lh + btn_pad);



            container.Add(CreateUIPanel("0 0", "1 1", HexToRGBA(color_dark, 0.25f), false, $"{pad} {pad}", $"{-pad} {h}"), "edit_sticker_wrap", "group_container");



            var maxShown = max_cols * max_rows;
            var start = pSet.groupPickerPage * maxShown;
            var end = start + maxShown;
            pSet.groupPickerTotalPages = Mathf.CeilToInt((float)pSet.availableCollections.Count / (float)maxShown);



            var bl = btn_pad;
            var noGroupColor = settings.COLLECTION == "" ? HexToRGBA(colorGreen, 0.4f) : HexToRGBA(color_dark, 0.25f);


            h = -btn_pad;
            container.Add(CreateNewButton("NO GROUP", $"cmdsticker pset setcollection none", "0 1", "0 1", TextAnchor.MiddleCenter, 10, "1 1 1 1", noGroupColor, $"{bl} {h - btn_h}", $"{bl + btn_w} {h}"), "group_container");
            bl += btn_w + btn_pad;
            container.Add(CreateNewButton("PREV", $"cmdsticker pset grouppicker prev", "0 1", "0 1", TextAnchor.MiddleCenter, 10, "1 1 1 1", HexToRGBA(colorLightBlue, 0.25f), $"{bl} {h - btn_h}", $"{bl + btn_w} {h}"), "group_container");
            bl += btn_w + btn_pad;
            container.Add(CreateNewLabel($"{pSet.groupPickerPage + 1} / {pSet.groupPickerTotalPages}", "0 1", "0 1", TextAnchor.MiddleCenter, 10, "1 1 1 1", $"{bl} {h - btn_h}", $"{bl + btn_w} {h}"), "group_container");
            bl += btn_w + btn_pad;
            container.Add(CreateNewButton("NEXT", $"cmdsticker pset grouppicker next", "0 1", "0 1", TextAnchor.MiddleCenter, 10, "1 1 1 1", HexToRGBA(colorLightBlue, 0.25f), $"{bl} {h - btn_h}", $"{bl + btn_w} {h}"), "group_container");



            var counter = 0;
            h -= (btn_h + btn_pad + 6f);
            bl = btn_pad;

            var i = 0;
            var sorted = pSet.availableCollections.OrderBy(x => x);
            foreach (var collection in sorted)
            {
                if (collection == "")
                    continue;

                if (i < start || i >= end)
                {
                    i++;
                    continue;
                }

                var aMin = "0 1";
                var aMax = "0 1";
                var colCmd = collection == "" ? "none" : collection;
                var bColor = settings.COLLECTION == collection ? HexToRGBA(colorGreen, 0.4f) : HexToRGBA(color_dark, 0.25f);
                container.Add(CreateNewButton(collection.ToUpper(), $"cmdsticker pset setcollection {colCmd}", aMin, aMax, TextAnchor.MiddleCenter, 10, "1 1 1 1", bColor, $"{bl} {h - btn_h}", $"{bl + btn_w} {h}"), "group_container");
                bl += btn_w + btn_pad;
                counter++;
                if (counter >= max_cols)
                {
                    counter = 0;
                    h -= (btn_h + btn_pad);
                    bl = btn_pad;
                }

                i++;
            }

            return container;
        }
        #endregion

        #region UI Helpers & Player Settings
        public class PlayerSettings
        {
            public int itemListPage = 0;
            public string filter = "all";
            public string filterSearch = "";
            public string filterCollection = "all";
            public string tradeFilter = "all";
            public string filterShow = "all";   // all | locked | unlocked
            public string sortOrder = "date";   // date | az | maxowners | available
            public string tab = "browser";
            public string editSticker = "";
            public string detailsSticker = "";
            public string detailsStickerOwnerID = "";
            public bool showGroupPicker = false;
            public int groupPickerPage = 0;
            public int groupPickerTotalPages = 1;
            public int inputTradeAmount = 1;
            public bool hideStickers = false;
            public bool askConfirmDelete = false;
            public List<string> availableCollections = new List<string>();
            public List<string> justUnlockedStickers = new List<string>();
        }

        void GetUIPosition(StickerData options, out float pHor, out float pVer, out float oHorMin, out float oHorMax, out float oVerMin, out float oVerMax)
        {
            pHor = 0f;
            pVer = 0f;

            var left = options.LEFT;
            var top = options.TOP;
            var width = options.DUI_WIDTH;
            var height = options.DUI_HEIGHT;

            oHorMin = left;
            oHorMax = left + width;
            oVerMin = -top - height;
            oVerMax = -top;

            switch (options.POS)
            {
                case "topleft":
                    pHor = 0f; pVer = 1;
                    oHorMin = left;
                    oHorMax = left + width;
                    oVerMin = -top - height;
                    oVerMax = -top;
                    break;

                case "topcenter":
                    pHor = 0.5f; pVer = 1;
                    oHorMin = left - (width / 2);
                    oHorMax = left + (width / 2);
                    oVerMin = -top - height;
                    oVerMax = -top;
                    break;
                case "topright":
                    pHor = 1f; pVer = 1;
                    oHorMin = -left - width;
                    oHorMax = -left;
                    oVerMin = -top - height;
                    oVerMax = -top;
                    break;



                case "middleleft":
                    pHor = 0f; pVer = 0.5f;
                    oHorMin = left;
                    oHorMax = left + width;
                    oVerMin = -(height / 2) - top;
                    oVerMax = (height / 2) - top;
                    break;
                case "middlecenter":
                    pHor = 0.5f; pVer = 0.5f;
                    oHorMin = left - (width / 2);
                    oHorMax = left + (width / 2);
                    oVerMin = -(height / 2) - top;
                    oVerMax = (height / 2) - top;

                    break;
                case "middleright":
                    pHor = 1f; pVer = 0.5f;
                    oHorMin = -left - width;
                    oHorMax = -left;
                    oVerMin = -(height / 2) - top;
                    oVerMax = (height / 2) - top;
                    break;

                case "bottomleft":
                    pHor = 0f; pVer = 0f;
                    oHorMin = left;
                    oHorMax = left + width;
                    oVerMin = top;
                    oVerMax = top + height;
                    break;
                case "bottomcenter":
                    pHor = 0.5f; pVer = 0f;
                    oHorMin = left - (width / 2);
                    oHorMax = left + (width / 2);
                    oVerMin = top;
                    oVerMax = top + height;
                    break;
                case "bottomright":
                    pHor = 1f; pVer = 0f;
                    oHorMin = -left - width;
                    oHorMax = -left;
                    oVerMin = top;
                    oVerMax = top + height;
                    break;
            }
        }

        void UpdateStaticUI(BasePlayer player)
        {
            if (ins.storedStickers.stickerData.DUI_URL == "")
                return;

            float pHor, pVer, oHorMin, oHorMax, oVerMin, oVerMax;
            GetUIPosition(storedStickers.stickerData, out pHor, out pVer, out oHorMin, out oHorMax, out oVerMin, out oVerMax);


            var newPlayerUI = new CuiElementContainer();

            CuiPanel newPanel = CreateUIPanel($"{pHor} {pVer}", $"{pHor} {pVer}", "0 0 0 0", false, $"{oHorMin} {oVerMin}", $"{oHorMax} {oVerMax}");
            newPlayerUI.Add(newPanel, "Overlay", UIPanelName);

            newPlayerUI.Add(new CuiElement
            {
                Name = CuiHelper.GetGuid(),
                Parent = UIPanelName,
                Components =
                    {
                        new CuiRawImageComponent { Url = ins.storedStickers.stickerData.DUI_URL },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                    }
            });

            AddUI(player, newPlayerUI, UIPanelName);
        }

        bool PlayerOwnsSticker(BasePlayer player, string stickerName, bool broadcast = false)
        {
            var unlockedStickers = GetUnlockedStickers(player);
            if (!unlockedStickers.Contains(stickerName))
            {
                Debug.Log($"::r:: {player.displayName} tried to sell un-owned sticker {stickerName}");
                if (broadcast) player.ChatMessage($"You don't own this sticker!");
                return false;
            }

            return true;
        }

        public List<string> GetUnlockedStickers(BasePlayer player)
        {          
            return GetUnlockedStickers(player.UserIDString);
        }

        public List<string> GetUnlockedStickers(string userID)
        {
            List<string> playerStickers = new List<string>();
            if (!ins.storedPlayerStickers.unlockedStickers.TryGetValue(userID, out playerStickers))
            {
                ins.storedPlayerStickers.unlockedStickers.Add(userID, new List<string>());
                playerStickers = ins.storedPlayerStickers.unlockedStickers[userID];
            }

            return playerStickers;
        }

        public PlayerSettings GetPlayerSettings(BasePlayer player)
        {
            if (!playerSettings.ContainsKey(player))
                playerSettings.Add(player, new PlayerSettings());

            PlayerSettings pSet;
            if (!playerSettings.TryGetValue(player, out pSet))
                return null;

            if (pSet.availableCollections.Count == 0)
                pSet.availableCollections = GetAvailableCollections(player);

            return pSet;
        }

        List<string> GetAvailableCollections(BasePlayer player)
        {
            PlayerSettings pSet;
            if (!playerSettings.TryGetValue(player, out pSet))
                return new List<string>();

            List<string> newList = new List<string>();
            foreach (var sticker in storedStickers.stickers)
            {
                var collection = sticker.Value.COLLECTION;
                if (collection == "" || collection == "none")
                    continue;

                if (newList.Contains(collection))
                    continue;

                newList.Add(collection);
            }

            return newList;
        }
        #endregion

        #region UI Core
        public void AddUI(BasePlayer player, CuiElementContainer container, string panelname)
        {
            CloseUI(player, panelname);

            if (!UIUsers.ContainsKey(player))
                UIUsers.Add(player, new List<string>());

            if (!UIUsers[player].Contains(panelname))
                UIUsers[player].Add(panelname);

            CuiHelper.AddUi(player, container);           
        }

        public void CloseUI(BasePlayer player, string panelName)
        {
            if (panelName == "")
            {
                CuiHelper.DestroyUi(player, UIPanelName);
                CuiHelper.DestroyUi(player, UIPanelChatStickers);
                CuiHelper.DestroyUi(player, UIPanelStickerBrowser);
                CuiHelper.DestroyUi(player, UIPanelStickerUnlock);

                if (UIUsers.ContainsKey(player))
                    UIUsers.Remove(player);
            }
            else
            {
                CuiHelper.DestroyUi(player, panelName);

                if (UIUsers.ContainsKey(player) && UIUsers[player].Contains(panelName))
                {
                    UIUsers[player].Remove(panelName);

                    if (UIUsers[player].Count == 0)
                        UIUsers.Remove(player);
                }
            }

            if (DATA_SAVE_REQUIED)
            {
                DATA_SAVE_REQUIED = false;
                SaveData();
            }
        }

        public void CloseAllUIs(string panelname = "")
        {
            foreach (var usr in new Dictionary<BasePlayer, List<string>>(UIUsers))
            {
                CloseUI(usr.Key, panelname);
            }
        }

        public CuiPanel CreateUIPanel(string anchorMin, string anchorMax, string background = "1 0 0 0.7", bool cursor = false, string offsetMin = "0 0", string offsetMax = "0 0", bool keyboard = false)
        {
            CuiPanel wrapPanel = new CuiPanel
            {
                CursorEnabled = cursor,
                KeyboardEnabled = keyboard,
                Image = {
                    Color = background
                },
                RectTransform = {
                    AnchorMin = anchorMin,
                    AnchorMax = anchorMax,
                    OffsetMin = offsetMin,
                    OffsetMax = offsetMax
                }
            };

            return wrapPanel;
        }

        CuiLabel CreateNewLabel(string text, string anchorMin = "0 0", string anchorMax = "1 1", TextAnchor align = TextAnchor.MiddleCenter, int fontSize = 12, string color = "1 1 1 1", string offsetMin = "0 0", string offsetMax = "0 0", CuiFont font = CuiFont.ROBOTO_REGULAR)
        {
            string dFont;
            switch (font)
            {
                case CuiFont.DAUBMARK: dFont = "daubmark.ttf"; break;
                case CuiFont.DROIDSANS_MONO: dFont = "droidsansmono.ttf"; break;
                case CuiFont.ROBOTO_BOLD: dFont = "robotocondensed-bold.ttf"; break;
                case CuiFont.ROBOTO_REGULAR: dFont = "robotocondensed-regular.ttf"; break;
                default: dFont = "robotocondensed-regular.ttf"; break;
            }

            CuiLabel label = new CuiLabel
            {
                Text = {
                    Text = text,
                    Align = align,
                    Color = color,
                    FontSize = fontSize,
                    Font = dFont
                },
                RectTransform = {
                    AnchorMin = anchorMin,
                    AnchorMax = anchorMax,
                    OffsetMin = offsetMin,
                    OffsetMax = offsetMax
                },
                FadeOut = 0
            };

            return label;
        }

        enum CuiFont
        {
            ROBOTO_REGULAR,
            ROBOTO_BOLD,
            DAUBMARK,
            DROIDSANS_MONO
        }

        CuiButton CreateNewButton(string text, string command, string anchorMin = "0 0", string anchorMax = "1 1", TextAnchor align = TextAnchor.MiddleCenter, int fontSize = 12, string color = "1 1 1 1", string background = "0 0 0 0.5", string offsetMin = "0 0", string offsetMax = "0 0", CuiFont font = CuiFont.ROBOTO_REGULAR)
        {
            string dFont;
            switch (font)
            {
                case CuiFont.DAUBMARK: dFont = "daubmark.ttf"; break;
                case CuiFont.DROIDSANS_MONO: dFont = "droidsansmono.ttf"; break;
                case CuiFont.ROBOTO_BOLD: dFont = "robotocondensed-bold.ttf"; break;
                case CuiFont.ROBOTO_REGULAR: dFont = "robotocondensed-regular.ttf"; break;
                default: dFont = "robotocondensed-regular.ttf"; break;
            }

            CuiButton button = new CuiButton
            {
                Button = { Color = background, Command = command, FadeIn = 0f },
                RectTransform = { AnchorMin = anchorMin, AnchorMax = anchorMax, OffsetMin = offsetMin, OffsetMax = offsetMax },
                Text = { Text = text, FontSize = fontSize, Align = align, Color = color, Font = dFont },
                FadeOut = 0
            };

            return button;
        }

        public CuiElement CreateInputField(string text, string command, string parentName, string anchorMin = "0 0", string anchorMax = "1 1", string offsetMin = "0 0", string offsetMax = "0 0", TextAnchor align = TextAnchor.MiddleCenter)
        {
            return new CuiElement
            {
                Name = CuiHelper.GetGuid(),
                Parent = parentName,
                Components =
                    {
                        new CuiInputFieldComponent
                        {
                            Text = text,
                            CharsLimit = 255,
                            Command = command,
                            FontSize = 11,
                            Font = "robotocondensed-regular.ttf",
                            Align = align
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = anchorMin,
                            AnchorMax = anchorMax,
                            OffsetMin = offsetMin,
                            OffsetMax = offsetMax
                        }
                    }
            };
        }

        public CuiElement CreateNewImage(string imageName, string parentName, string anchorMin = "0 0", string anchorMax = "1 1", string offsetMin = "0 0", string offsetMax = "0 0", CuiOutlineComponent outline = null)
        {
            var element = new CuiElement
            {
                Name = CuiHelper.GetGuid(),
                Parent = parentName,
                Components = {
                        new CuiRawImageComponent
                        {
                            Png = GetImage(imageName)
                        },
                        new CuiRectTransformComponent {
                            AnchorMin = anchorMin,
                            AnchorMax = anchorMax,
                            OffsetMin = offsetMin,
                            OffsetMax = offsetMax
                        }
                    }
            };

            if (outline != null)
                element.Components.Add(outline);

            return element;
        }
        #endregion
        #endregion

        #region Config
        private ConfigData configData;

        class Tier
        {
            [JsonProperty(PropertyName = "Enabled true/false")]
            public bool enable = true;

            [JsonProperty(PropertyName = "Name of the package, should be unique and contain no spaces")]
            public string name = "random";

            [JsonProperty(PropertyName = "Package name used in the UI, can be anything (leave empty to use pack name)")]
            public string displayName = "Random Sticker";

            [JsonProperty(PropertyName = "Sticker pack price")]
            public int price = 5;

            [JsonProperty(PropertyName = "Amount of stickers unlocked by buying this pack")]
            public int amount = 1;

            [JsonProperty(PropertyName = "Collection to pick from (leave empty for all stickers)")]
            public string collection = "";

            [JsonProperty(PropertyName = "Permission needed to buy this pack (leave empty if anyone can buy)")]
            public string permission = "";

            [JsonProperty(PropertyName = "Background color in hex format (#ff00aa) (leave empty for none)")]
            public string background = "";
        }


        class ConfigData
        {
            [JsonProperty(PropertyName = "Basic Settings")]
            public BasicSettings basicSettings { get; set; }

            public class BasicSettings
            {
                [JsonProperty(PropertyName = "Amount of inactive days before players start losing stickers")]
                public int maxDaysInactive { get; set; }

                [JsonProperty(PropertyName = "Amount of stickers an inactive player loses every day")]
                public int dailyTakeBack { get; set; }

                [JsonProperty(PropertyName = "Sticker display time in seconds")]
                public float stickerDisplayTime { get; set; }

                [JsonProperty(PropertyName = "Player cooldown in seconds before showing another sticker in chat (0 for no cooldowns)")]
                public float showStickerCooldown { get; set; }

                [JsonProperty(PropertyName = "Show notifications when a player unlocks a new sticker")]
                public bool showUnlockNotification { get; set; }

                [JsonProperty(PropertyName = "Enable physical sticker tokens (item shortname: wrappedgift, unwrap to redeem token)")]
                public bool stickerTokenItemsEnabled { get; set; }

                [JsonProperty(PropertyName = "Skin ID for physical sticker tokens")]
                public ulong stickerTokenItemSkinID { get; set; }


            }

            [JsonProperty(PropertyName = "Sticker packs - additional ways to unlock stickers, multiple allowed")]
            public List<Tier> stickerUnlockPacks { get; set; }



            [JsonProperty(PropertyName = "Currency Settings")]
            public CurrencySettings currencySettings { get; set; }

            public class CurrencySettings
            {
                [JsonProperty(PropertyName = "Currency (0 = Item, 1 = ZCoins, 2 = ServerRewards, 3 = Economics")]
                public int currency { get; set; }

                [JsonProperty(PropertyName = "Currency name (only for display)")]
                public string currencyName { get; set; }

                [JsonProperty(PropertyName = "Image to use for currency")]
                public string icon_currency { get; set; }

                [JsonProperty(PropertyName = "(items only) Shortname of item to use as currency (ie: scrap)")]
                public string currencyItemName { get; set; }

                [JsonProperty(PropertyName = "(items only) Skin ID of item to use as currency, 0 for no skin")]
                public ulong currencySkinID { get; set; }

                [JsonProperty(PropertyName = "Default token cost to unlock a sticker (sticker packs overrule this)")]
                public int sticker_price { get; set; }

                [JsonProperty(PropertyName = "Sticker Token Packs")]
                public List<TokenPack> tokenPacks { get; set; }
            }


            [JsonProperty(PropertyName = "Sticker Trader")]
            public StickerTraderSettings stickerTraderSettings { get; set; }

            public class StickerTraderSettings
            {
                [JsonProperty(PropertyName = "Players can trade stickers with other players for currency")]
                public bool tradingEnabled { get; set; }

                [JsonProperty(PropertyName = "Days before an unsold trade is cancelled")]
                public int tradeExpireDays { get; set; }

                [JsonProperty(PropertyName = "Show new trade notifications")]
                public bool showTradeNotification { get; set; }
            }


            [JsonProperty(PropertyName = "Recycle Settings")]
            public RecycleSettings recycleSettings { get; set; }

            public class RecycleSettings
            {
                [JsonProperty(PropertyName = "Players can recycle stickers to earn tokens (true)")]
                public bool recyclingEnabled { get; set; }

                [JsonProperty(PropertyName = "Maximum amount of bonus tokens returned by recycling (4)")]
                public float maxRecycleAdditionalValue { get; set; }               
            }

            public static ConfigData CreateConfig()
            {
                return new ConfigData()
                {
                    basicSettings = new BasicSettings()
                    {
                        dailyTakeBack = 2,
                        maxDaysInactive = 30,
                        stickerDisplayTime = 3,
                        showStickerCooldown = 0,
                        showUnlockNotification = true,
                        stickerTokenItemsEnabled = true,
                        stickerTokenItemSkinID = 3104407217
                    },
                    stickerUnlockPacks = new List<Tier>()
                    {
                         new Tier()                        
                    },
                    currencySettings = new CurrencySettings()
                    {
                        currency = 0,
                        currencyName = "Sticky Stuff",
                        currencyItemName = "ducttape",
                        currencySkinID = 0,
                        //icon_currency = "https://i.imgur.com/xj0jMeA.png",    // zcoins
                        icon_currency = "https://i.imgur.com/YYWXGeX.png",      // coins
                        sticker_price = 5,
                        tokenPacks = new List<TokenPack>()
                        {
                            {
                                new TokenPack() {
                                    name = "small",
                                    price = 1,
                                    tokenAmount = 1
                                }
                            },
                            {
                                new TokenPack() {
                                    name = "medium",
                                    price = 2,
                                    tokenAmount =  3
                                }
                            },
                            {
                                new TokenPack() {
                                    name = "large",
                                    price = 3,
                                    tokenAmount = 5
                                }
                            },
                            {
                                new TokenPack() {
                                    name = "huge",
                                    price = 5,
                                    tokenAmount = 10
                                }
                            },
                            {
                                new TokenPack() {
                                    name = "family",
                                    price = 10,
                                    tokenAmount = 22
                                }
                            },
                            {
                                new TokenPack() {
                                    name = "bragger",
                                    price = 20,
                                    tokenAmount = 50
                                }
                            }
                        }
                    },
                    recycleSettings = new RecycleSettings()
                    {
                        recyclingEnabled = true,
                        maxRecycleAdditionalValue = 4f
                    },
                    stickerTraderSettings = new StickerTraderSettings()
                    {
                        tradingEnabled = true,
                        tradeExpireDays = 14,
                        showTradeNotification = true
                    }
                };
            }
        }

        private bool LoadConfigVariables()
        {
            try
            {
                configData = Config.ReadObject<ConfigData>();
            }
            catch
            {
                return false;
            }
            SaveConf();
            return true;
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Creating new config file.");
            configData = ConfigData.CreateConfig();
            SaveConf();
        }

        void SaveConf() => Config.WriteObject(configData, true);
        #endregion

        #region Data
        StoredPlayerSticker storedPlayerStickers;
        StoredStickers storedStickers;
        StoredMarketData storedMarketData;

        class StoredPlayerSticker
        {
            public Dictionary<string, int> unlockTokens = new Dictionary<string, int>();
            public Dictionary<string, int> playersLastSeen = new Dictionary<string, int>();
            public Dictionary<string, List<string>> unlockedStickers = new Dictionary<string, List<string>>();
            public List<string> playerHideStickers = new List<string>();
            public int lastInactiveScan = 0;
        }

        class StoredStickers
        {
            public StickerData stickerData = new StickerData();
            public Dictionary<string, StickerData> stickers = new Dictionary<string, StickerData>();
        }

        class StoredMarketData
        {
            public List<StickerTrade> stickerTrades = new List<StickerTrade>();
        }

        public class StickerTrade
        {
            public string sticker = "";
            public string tier = "common";
            public string sellerID = "";
            public string sellerName = "";
            public int price = 0;
            public long created = 0;

            public bool isSold = false;
            public string buyerID = "";

            // added on sort
            [JsonIgnore]
            public int max_owners = 0;
            [JsonIgnore]
            public int current_owners = 0;
            [JsonIgnore]
            public string collection = "";
        }

        public class BidOffer
        {
            public string bidderID = "";
            public string bidderName = "";
            public int amount = 0;
            public string currency = "token";
            public float createdTime = 0;
        }

        public class StickerData
        {
            public string TIER = "common";
            public string COLLECTION = "";
            public int MAX_OWNERS = 0;
            public int CURRENT_OWNERS = 0;
            //public float DUI_LEFT = 100f;
            //public float DUI_RIGHT = 0;
            //public float DUI_BOTTOM = 0;
            //public float DUI_TOP = 100f;

            public string POS = "topleft"; // top middle bottom + left center right
            public float LEFT = 100f;
            public float TOP = 100f;

            public float DUI_WIDTH = 100f;
            public float DUI_HEIGHT = 100f;
            public string DUI_URL = "";

            //public int DUI_FONTSIZE = 14;
            //public string DUI_FONTCOLOR = "#ffffff";
            //public string DUI_BGCOLOR = "#000000";
            //public float DUI_BGOPACITY = 0f;
            //public string DUI_TEXT = "";

            public StickerData()
            {

            }

            public StickerData(StickerData options)
            {
                TIER = options.TIER;
                COLLECTION = options.COLLECTION;
                MAX_OWNERS = options.MAX_OWNERS;
                //DUI_LEFT = options.DUI_LEFT;
                //DUI_RIGHT = options.DUI_RIGHT;
                //DUI_BOTTOM = options.DUI_BOTTOM;
                //DUI_TOP = options.DUI_TOP;
                POS = options.POS;
                LEFT = options.LEFT;
                TOP = options.TOP;
                DUI_WIDTH = options.DUI_WIDTH;
                DUI_HEIGHT = options.DUI_HEIGHT;
                DUI_URL = options.DUI_URL;
                //DUI_FONTSIZE = options.DUI_FONTSIZE;
                //DUI_FONTCOLOR = options.DUI_FONTCOLOR;
                //DUI_BGCOLOR = options.DUI_BGCOLOR;
                //DUI_BGOPACITY = options.DUI_BGOPACITY;
                //DUI_TEXT = options.DUI_TEXT;
            }
        }

        void Loaded()
        {
            LoadData();
        }

        public void LoadData()
        {
            try
            {
                storedStickers = Interface.Oxide.DataFileSystem.ReadObject<StoredStickers>(dataFileNameStickers);
            }
            catch
            {
                storedStickers = new StoredStickers();
                Puts("Invalid datafile, new file generated");

                storedStickers.stickers.Add(":pika:", new StickerData()
                {
                    TIER = "common",
                    MAX_OWNERS = 50,
                    CURRENT_OWNERS = 0,
                    LEFT = 100,
                    TOP = 100,
                    POS = "topleft",
                    DUI_URL = "https://i.imgur.com/GtsjTnf.png",
                });

                SaveData();
            }

            try
            {
                storedPlayerStickers = Interface.Oxide.DataFileSystem.ReadObject<StoredPlayerSticker>(dataFileNamePlayerStickers);
            }
            catch
            {
                storedPlayerStickers = new StoredPlayerSticker();
                Puts("Invalid storedPlayerStickers data file, new file generated");

                SaveData();
            }

            if (storedPlayerStickers == null)
            {
                storedPlayerStickers = new StoredPlayerSticker();
                Puts("Invalid storedPlayerSticker datafile, new file generated");

                SaveData();
            }

            try
            {
                storedMarketData = Interface.Oxide.DataFileSystem.ReadObject<StoredMarketData>(dataFileNameMarketData);
            }
            catch
            {
                storedMarketData = new StoredMarketData();
                Puts("Invalid StoredMarketData data file, new file generated");

                SaveData();
            }
        }

        void SaveData()
        {
            //Interface.Oxide.DataFileSystem.WriteObject(dataFileName, storedData);
            Interface.Oxide.DataFileSystem.WriteObject(dataFileNameStickers, storedStickers);
            Interface.Oxide.DataFileSystem.WriteObject(dataFileNamePlayerStickers, storedPlayerStickers);
            Interface.Oxide.DataFileSystem.WriteObject(dataFileNameMarketData, storedMarketData);

            DATA_SAVE_REQUIED = false;
        }
        #endregion

        #region Manage & Debug
        float AVG_STICKER_WEIGHT = 0;
        void UpdateStickerWeights()
        {
            if (storedStickers.stickers.Count == 0)
                return;

            List<int> sticker_weights = new List<int>();
            foreach (var sticker in new Dictionary<string, StickerData>(storedStickers.stickers))
            {
                if (sticker.Value.MAX_OWNERS > 0)
                    sticker_weights.Add(sticker.Value.MAX_OWNERS);
            }

            if (sticker_weights.Count == 0)
                return;

            AVG_STICKER_WEIGHT = (float)sticker_weights.Average();
        }

        void RemoveInactive()
        {
            var now = UnixTimeStampUTC();
            var checkEverySeconds = 60 * 60 * 24;   // 1 day
            if (now - storedPlayerStickers.lastInactiveScan > checkEverySeconds)
            {
                storedPlayerStickers.lastInactiveScan = now;

                var maxSecondsInactive = 60 * 60 * 24 * configData.basicSettings.maxDaysInactive;
                var inactivePlayers = storedPlayerStickers.playersLastSeen.Where(x => now - x.Value > maxSecondsInactive).ToDictionary(k => k.Key, v => v.Value);
                var releasedStickers = 0;

                var msgInactive = $"";

                msgInactive += $"\n\nChecking inactive players, max days inactive is: {configData.basicSettings.maxDaysInactive}\n";
                foreach (var inactivePlayer in new Dictionary<string, int>(inactivePlayers))
                {
                    msgInactive += $"::o:: {inactivePlayer.Key} - Offline for {now - inactivePlayer.Value}s ({FormatDate(now - inactivePlayer.Value)} days) ";
                    List<string> playerStickers;
                    if (storedPlayerStickers.unlockedStickers.TryGetValue(inactivePlayer.Key, out playerStickers))
                    {
                        if (playerStickers.Count > 0)
                        {
                            var randomSticker = playerStickers.GetRandom();
                            playerStickers.Remove(randomSticker);
                            if (storedStickers.stickers.ContainsKey(randomSticker))
                                storedStickers.stickers[randomSticker].CURRENT_OWNERS--;
                            releasedStickers++;

                            msgInactive += $"- removed 1 sticker, {playerStickers.Count} left ";
                        }

                        if (storedPlayerStickers.unlockTokens.ContainsKey(inactivePlayer.Key))
                        {
                            storedPlayerStickers.unlockTokens[inactivePlayer.Key]--;
                            msgInactive += $"- removed 1 token, {storedPlayerStickers.unlockTokens[inactivePlayer.Key]} left ";
                        }

                        if (playerStickers.Count == 0)
                        {
                            if (storedPlayerStickers.unlockedStickers.ContainsKey(inactivePlayer.Key))
                                storedPlayerStickers.unlockedStickers.Remove(inactivePlayer.Key);

                            if (storedPlayerStickers.playersLastSeen.ContainsKey(inactivePlayer.Key))
                                storedPlayerStickers.playersLastSeen.Remove(inactivePlayer.Key);

                            msgInactive += $"- no more stickers left, remove all player data";
                        }

                        msgInactive += "\n";
                    }
                    else
                    {
                        if (storedPlayerStickers.playersLastSeen.ContainsKey(inactivePlayer.Key))
                            storedPlayerStickers.playersLastSeen.Remove(inactivePlayer.Key);

                        msgInactive += $"- player has no stickers, remove all player data\n";
                    }
                }

                int tDel = 0;
                foreach (var playerTokens in new Dictionary<string, int>(storedPlayerStickers.unlockTokens))
                {

                    if (playerTokens.Value <= 0 || !storedPlayerStickers.playersLastSeen.ContainsKey(playerTokens.Key))
                    {
                        storedPlayerStickers.unlockTokens.Remove(playerTokens.Key);
                        tDel++;
                    }
                }
                msgInactive += $"Removed token stats of {tDel} players (inactive or no balance)\n";

                if (releasedStickers > 0)
                    PrintToChat($"<color={colorOrange}>{releasedStickers} stickers were just released because {inactivePlayers.Count} owners were offline for more than {configData.basicSettings.maxDaysInactive} days</color>");


                Debug.Log(msgInactive);

                foreach (var trade in new List<StickerTrade>(storedMarketData.stickerTrades))
                {
                    List<string> unlockedStickers;
                    if (!storedPlayerStickers.unlockedStickers.TryGetValue(trade.sellerID, out unlockedStickers) || !unlockedStickers.Contains(trade.sticker))
                    {
                        Debug.Log($"::o:: Removed sticker trade {trade.sticker} by {trade.sellerName}");
                        storedMarketData.stickerTrades.RemoveAll(x => x.sellerID == trade.sellerID && x.sticker == trade.sticker);
                    }
                }

                SaveData();
            }
        }

        void CleanupStickerTrades()
        {
            if (configData.stickerTraderSettings.tradeExpireDays > 0)
            {
                var now = DateTimeOffset.Now.ToUnixTimeSeconds();
                var msg = $"::p:: Removing sticker trades older than {configData.stickerTraderSettings.tradeExpireDays} days:\n";
                var countNow = ins.storedMarketData.stickerTrades.Count;
                var count = 0;
                foreach (var trade in new List<StickerTrade>(ins.storedMarketData.stickerTrades.OrderBy(x => x.created)))
                {
                    if (trade.created + (60 * 60 * 24 * configData.stickerTraderSettings.tradeExpireDays) < now)
                    {
                        count++;
                        storedMarketData.stickerTrades.RemoveAll(x => x.sellerID == trade.sellerID && x.sticker == trade.sticker);
                        msg += $"::y:: {trade.sellerName} - {trade.sticker} - createdTime: {trade.created} ({(now - trade.created) / 60 / 60 / 24} days ago)\n";
                    }
                }

                msg += $"::p:: Removed {count} of {countNow} expired trades";
                Debug.Log(msg);
            }
        }

        void RefreshAllStickerData(bool console = false)
        {
            var msg = "Updating stickers:\n";

            msg += $"{"OWNERS",-6}";
            msg += $"{"MAX",-9}";
            msg += $"{"LEFT",-8}";
            msg += $"{"STICKER",-5}\n";

            for (var i = 0; i < ins.storedStickers.stickers.Count; i++)
            {
                var sticker = ins.storedStickers.stickers.ElementAt(i);
                var ownerAmount = 0;

                foreach (var playerStickers in ins.storedPlayerStickers.unlockedStickers)
                {
                    ownerAmount += playerStickers.Value.FindAll(x => x == sticker.Key).Count;
                }

                ins.storedStickers.stickers[sticker.Key].CURRENT_OWNERS = ownerAmount;

                var available = sticker.Value.MAX_OWNERS - sticker.Value.CURRENT_OWNERS;

                if (console)
                    msg += $"{sticker.Value.CURRENT_OWNERS,-12} {sticker.Value.MAX_OWNERS,-12} {available,-12} {sticker.Key}\n";

                if ((sticker.Value.POS == "topleft" || sticker.Value.POS == "topright") && sticker.Value.TOP == 250)
                {
                    ins.storedStickers.stickers[sticker.Key].TOP = ins.storedStickers.stickers[sticker.Key].TOP - 150;
                }

                if ((sticker.Value.POS == "topleft" || sticker.Value.POS == "topright") && sticker.Value.TOP < 0)
                {
                    ins.storedStickers.stickers[sticker.Key].TOP = -ins.storedStickers.stickers[sticker.Key].TOP;
                }

                if ((sticker.Value.POS == "bottomleft" || sticker.Value.POS == "bottomright") && sticker.Value.LEFT < 0)
                {
                    ins.storedStickers.stickers[sticker.Key].LEFT = -ins.storedStickers.stickers[sticker.Key].LEFT;
                }
            }

            UpdateStickerWeights();
        }

        void ValidateOwnedStickers()
        {
            foreach (var player in new Dictionary<string, List<string>>(ins.storedPlayerStickers.unlockedStickers))
            {
                var playerID = player.Key;

                var msg = $"{playerID}: {player.Value.Count} stickers: ";

                foreach (var sticker in player.Value)
                {
                    if (!storedStickers.stickers.ContainsKey(sticker))
                    {
                        msg += $":{sticker}, ";
                    }
                }

                if (player.Value.Count > 0)
                    Debug.Log($"::b:: {msg}");
            }
        }
        #endregion

        #region Helpers

        #region colors
        private string colorGreen = "#A5FFD6";
        private string colorBlue = "#5CC9FF";
        private string colorLightBlue = "#9ED3FF";
        private string colorRed = "#FF686B";
        private string colorPink = "#FFA69E";
        private string colorYellow = "#EAFFA6";
        private string colorOrange = "#FFCC91";

        const string color_light = "#FEF9EF";
        const string color_dark = "#2C2528";
        const string color_darkest = "#121212";

        const string colorCommon = "#32fa5a";
        const string colorRare = "#3299fa";
        const string colorEpic = "#d932fa";
        const string colorLegendary = "#fa9332";
        #endregion

        bool HasPermission(string id, string perm) => permission.UserHasPermission(id, perm);

        void PrintUnlockedStickers(BasePlayer player)
        {
            List<string> playerStickers;
            if (!storedPlayerStickers.unlockedStickers.TryGetValue(player.UserIDString, out playerStickers))
            {
                player.ChatMessage($"You have no unlocked stickers");
                return;
            }

            var unlocked_c = new Dictionary<string, StickerData>();
            var unlocked_r = new Dictionary<string, StickerData>();
            var unlocked_e = new Dictionary<string, StickerData>();
            var unlocked_l = new Dictionary<string, StickerData>();
            for (var i = 0; i < playerStickers.Count; i++)
            {
                StickerData eInfo;
                if (!storedStickers.stickers.TryGetValue(playerStickers[i], out eInfo))
                    continue;

                switch (eInfo.TIER)
                {
                    case "common": unlocked_c.Add(playerStickers[i], eInfo); break;
                    case "rare": unlocked_r.Add(playerStickers[i], eInfo); break;
                    case "epic": unlocked_e.Add(playerStickers[i], eInfo); break;
                    case "legendary": unlocked_l.Add(playerStickers[i], eInfo); break;
                }
            }

            var msg = "";
            msg += $"<color={colorLegendary}>{unlocked_l.Count}/{storedStickers.stickers.Count(x => x.Value.TIER == "legendary")} LEGENDARY </color>\t{string.Join(" ", unlocked_l.Keys)}\n";
            msg += $"<color={colorEpic}>{unlocked_e.Count}/{storedStickers.stickers.Count(x => x.Value.TIER == "epic")} EPIC </color>\t\t{string.Join(" ", unlocked_e.Keys)}\n";
            msg += $"<color={colorRare}>{unlocked_r.Count}/{storedStickers.stickers.Count(x => x.Value.TIER == "rare")} RARE </color>\t\t{string.Join(" ", unlocked_r.Keys)}\n";
            msg += $"<color={colorCommon}>{unlocked_c.Count}/{storedStickers.stickers.Count(x => x.Value.TIER == "common")} COMMON </color>\t{string.Join(" ", unlocked_c.Keys)}\n";

            player.ChatMessage($"<size=13>Unlocked stickers:\n{msg}</size>");
        }

        private BaseEntity getRayEntity(BasePlayer player)
        {
            RaycastHit hit;
            if (UnityEngine.Physics.Raycast(player.eyes.HeadRay(), out hit, 3))
            {
                BaseEntity entity = null;
                entity = hit.GetEntity() as BaseEntity;

                if (entity != null)
                {
                    return entity;
                }
            }

            return null;
        }

        public string HexToRGBA(string colorHex, float opacity = 1.0f)
        {
            var hex = colorHex.Replace("#", "").ToUpper().Trim();
            if (hex.Length != 6 && hex.Length != 8)
                return "1 1 1 1";

            float r, g, b, a = 1.0f;


            r = (float)short.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber) / 255;
            g = (float)short.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber) / 255;
            b = (float)short.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber) / 255;

            if (hex.Length == 8)
                a = (float)short.Parse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber) / 255;

            if (opacity != 1.0f)
                a = opacity;

            return $"{r} {g} {b} {a}";
        }

        public bool FindPlayer(string name, out BasePlayer player)
        {
            player = FindPlayerByName(name);
            return player != null;
        }

        public BasePlayer FindPlayerByName(string playerName)
        {
            BasePlayer target = FindByName(playerName);
            if (target == null)
                return FindByName(playerName, true);

            return target;
        }

        BasePlayer FindByName(string nameOrUid, bool sleepers = false)
        {
            List<BasePlayer> players = !sleepers ? new List<BasePlayer>(BasePlayer.activePlayerList) : new List<BasePlayer>(BasePlayer.sleepingPlayerList);

            ulong userId;
            if (ulong.TryParse(nameOrUid, out userId))
                return players.SingleOrDefault(x => x.userID.Get() == userId);

            string targetName = nameOrUid.ToLower().Replace(" ", "");
            return players.FirstOrDefault(x => x.displayName.ToLower().Replace(" ", "").Contains(targetName));
        }

        private int UnixTimeStampUTC()
        {
            int unixTimeStamp;
            DateTime currentTime = DateTime.Now;
            DateTime zuluTime = currentTime.ToUniversalTime();
            DateTime unixEpoch = new DateTime(1970, 1, 1);
            unixTimeStamp = (int)zuluTime.Subtract(unixEpoch).TotalSeconds;
            return unixTimeStamp;
        }

        string FormatDate(int seconds)
        {
            var days = seconds / 60 / 60 / 24;
            return days.ToString();
        }
        #endregion
    }
}
