using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Rust.Workshop;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace Oxide.Plugins 
{
    [Info("InstantCraft", "Menevt", "2.7.1")]
    [Description("Instant craft items(includes normal speed list and blacklist)")]
    /*
     * Author info:
     *   E-mail: Vlad-00003@mail.ru
     *   Vk: vk.com/vlad_00003
     */
    internal class InstantCraft : RustPlugin
    {
        #region Quitting

        private void Unload()
        {
            if (_getSkinsCoroutine != null)
                ServerMgr.Instance.StopCoroutine(_getSkinsCoroutine);
        }

        #endregion

        #region Config setup

        private class AllowedItemsConfig
        {
            [JsonProperty("Тип списка (0 - никак не управлять предметами, 1 - чёрный список, 2 - белый список).")]
            private int _type;

            [JsonProperty("Список предметов")]
            public List<string> Items;

            [JsonIgnore]
            public readonly List<ItemDefinition> ItemsDefinitions = new List<ItemDefinition>();

            #region Default Config

            public static AllowedItemsConfig DefaultConfig => new AllowedItemsConfig
            {
                _type = 0,
                Items = new List<string> { "explosive.satchel", "grenade.f1" }
            };

            #endregion

            public bool ShouldRandomizeSkin(ItemDefinition definition)
            {
                ulong playerid = 5824512;
                switch (_type)
                {
                    case 1:
                        return ItemsDefinitions.All(x => x != definition);
                    case 2:
                        return ItemsDefinitions.Any(x => x == definition);
                    default:
                        return true;
                }
            }
        }

        private class SkinSettings
        {
            [JsonProperty("Присваивать случайный скин")]
            public bool RandomSkins;

            [JsonProperty("Привилегия для получения случайного скина (null - все игроки получают случайные скины)")]
            public string Permission;

            [JsonProperty("Использовать основные скины из игры")]
            public bool UseInGame;

            [JsonProperty("Использовать принятые скины из мастерской")]
            public bool UseAccepted;

            [JsonProperty("Использовать дополнительный список скинов из мастерской")]
            public bool UseCustom;

            [JsonProperty("Дополнительные скины из мастерской")]
            public List<ulong> AdditionalSkins;

            [JsonProperty("Список предметов, которым присваивается случайный скин")]
            public AllowedItemsConfig ItemList;

            #region Default Config

            public static SkinSettings DefaultConfig => new SkinSettings
            {
                RandomSkins = false,
                UseInGame = true,
                UseAccepted = true,
                UseCustom = false,
                Permission = nameof(InstantCraft) + ".Random",
                ItemList = AllowedItemsConfig.DefaultConfig,
                AdditionalSkins = new List<ulong> { 2292480999 }
            };

            #endregion

            [JsonIgnore]
            public bool IsPermissionEmpty => string.IsNullOrEmpty(Permission) || Permission == "null";

            public bool CanUse(BasePlayer player)
            {
                return IsPermissionEmpty || player.IPlayer.HasPermission(Permission);
            }
        }

        private class PluginConfig
        {
            [JsonIgnore]
            public readonly List<ItemDefinition> Blacklist = new List<ItemDefinition>();

            [JsonIgnore]
            public readonly List<ItemDefinition> NormalList = new List<ItemDefinition>();

            [JsonProperty("Привилегия для мгновенного крафта (null - мгновенный крафт работает для всех)")]
            public string CraftPermission;

            [JsonProperty("Предметы, крафтить которые запрещено")]
            public List<string> BlacklistString;

            [JsonProperty("Предметы со стандартной скоростью крафта")]
            public List<string> NormalListString;

            [JsonProperty("Формат сообщений в чате")]
            public string ChatFormat;

            [JsonProperty("SteamID отправителя сообщений в чате")]
            public ulong ChatMessageSteamId;

            [JsonProperty("Разделять стаки при крафте")]
            public bool SplitStacks;

            [JsonProperty("Случайные скины при крафте (в случае, если игрок не выбрал скин)")]
            public SkinSettings SkinSettings;

            #region Default Config

            public static PluginConfig DefaultConfig => new PluginConfig
            {
                CraftPermission = "null",
                ChatFormat = "<color=#42d4f4>[InstantCraft]</color>: {0}",
                ChatMessageSteamId = 0,
                NormalListString = new List<string> { "rock", ItemNamePlaceholder },
                BlacklistString = new List<string> { ItemNamePlaceholder },
                SplitStacks = true,
                SkinSettings = SkinSettings.DefaultConfig
            };

            #endregion

            public static void InitList(List<string> stringList, List<ItemDefinition> definitions,
                List<string> wrongNames)
            {
                stringList.ForEach(x =>
                {
                    if (x == ItemNamePlaceholder)
                        return;
                    var def = ItemManager.FindItemDefinition(x);
                    if (def == null)
                    {
                        wrongNames.Add(x);
                        return;
                    }

                    definitions.Add(def);
                });
            }
            [JsonIgnore]
            public bool IsCraftPermissionEmpty => string.IsNullOrEmpty(CraftPermission) || CraftPermission == "null";

            public bool CanInstantCraft(BasePlayer player)
            {
                return IsCraftPermissionEmpty || player.IPlayer.HasPermission(CraftPermission);
            }
        }

        #endregion

        #region Initializing

        private void OnServerInitialized()
        {
            if (!_config.SkinSettings.UseCustom)
                return;
            if (_config.SkinSettings.AdditionalSkins.Count == 0)
            {
                PrintWarning("Custom skins turned on, but no skins specified in the list.");
                return;
            }
            _getSkinsCoroutine = ServerMgr.Instance.StartCoroutine(GetSkins());
        }

        private IEnumerator GetSkins()
        {
            var added = 0;
            var failed = Pool.GetList<string>();
            yield return GetSkins(_config.SkinSettings.AdditionalSkins.Where(x => !_data.ResolvedSkins.ContainsKey(x)),
                (x, y) =>
                {
                    _data.ResolvedSkins[y] = x;
                }, x => failed.Add(x + " [FAILED TO RESOLVE]"));
            foreach (var resolvedSkin in _data.ResolvedSkins)
            {
                if (TryAddCustomSkin(resolvedSkin.Value, new SkinInfo(resolvedSkin.Key)))
                    added++;
                else
                    failed.Add($"{resolvedSkin.Key} [{resolvedSkin.Value}]");
            }
            Puts("Initialized {0} custom skins from the workshop.", added);
            if(failed.Count>0)
                Puts("Failed to add workshop items: {0}",string.Join(", ", failed));
            Pool.FreeList(ref failed);
            SaveData();
        }
        
        #region Workhop Skin Resolver
        
        private IEnumerator GetSkins(IEnumerable<ulong> workshopIds, Action<string,ulong> onSuccess, Action<ulong> onFail)
        {
            foreach (var request in BuildRequests(workshopIds))
            {
                using (var www = UnityWebRequest.Post(GetPublishedAPI, request))
                {
                    yield return www.SendWebRequest();
                   
                    if (!www.isDone)
                    {
                        PrintError("Unable to complete Steam API request!");
                        break;
                    }

                    if (www.isHttpError || www.isNetworkError)
                    {
                        PrintError("Steam API request resulted in error: {0}", www.error);
                        break;
                    }

                    var text = www.downloadHandler.text;
                    if (string.IsNullOrEmpty(text))
                    {
                        PrintError("Steam API return empty response!");
                        break;
                    }

                    var json = JsonConvert.DeserializeObject<SteamApiResponse>(text,
                        _errorHandler);
                    if(json?.response == null || json.response.resultcount == 0)
                    {
                        PrintError("Steam API return empty response!");
                        break;
                    }

                    foreach (var field in json.response.publishedfiledetails)
                    {
                        if (!field.IsValid)
                            continue;
                        ulong id;
                        if (!field.TryGetId(out id))
                            continue;
                        var success = false;
                        foreach (var fieldTag in field.tags)
                        {
                            if (fieldTag.tag.StartsWith("Skin") || fieldTag.tag.StartsWith("Version"))
                                continue;
                            var skinnable = Skinnable.FindForItem(fieldTag.tag);
                            if (string.IsNullOrEmpty(skinnable?.ItemName)) 
                                continue;
                            onSuccess?.Invoke(skinnable.ItemName,id);
                            success = true;
                            break;
                        }
                        if(!success)
                            onFail?.Invoke(id);
                    }
                }
            }
        }

        #region Steam Api Response

        public class Tag
        {
            public string tag { get; set; }
        }

        public class Publishedfiledetail
        {
            public string publishedfileid { get; set; }
            public int result { get; set; }
            public string creator { get; set; }
            public int creator_app_id { get; set; }
            public int consumer_app_id { get; set; }
            public string filename { get; set; }
            public int file_size { get; set; }
            public string preview_url { get; set; }
            public string hcontent_preview { get; set; }
            public string title { get; set; }
            public string description { get; set; }
            public int time_created { get; set; }
            public int time_updated { get; set; }
            public int visibility { get; set; }
            public int banned { get; set; }
            public string ban_reason { get; set; }
            public int subscriptions { get; set; }
            public int favorited { get; set; }
            public int lifetime_subscriptions { get; set; }
            public int lifetime_favorited { get; set; }
            public int views { get; set; }
            public List<Tag> tags { get; set; }

            [JsonIgnore]
            public bool IsValid => !string.IsNullOrEmpty(preview_url) && tags != null;

            public bool TryGetId(out ulong id) => ulong.TryParse(publishedfileid, out id);
        }

        public class Response
        {
            public int result { get; set; }
            public int resultcount { get; set; }
            public List<Publishedfiledetail> publishedfiledetails { get; set; }
        }

        public class SteamApiResponse
        {
            public Response response { get; set; }
        }


        #endregion
        
        private readonly JsonSerializerSettings _errorHandler = new JsonSerializerSettings {Error = (sender, args) => args.ErrorContext.Handled = true};
        private IEnumerable<WWWForm> BuildRequests(IEnumerable<ulong> workshopIds)
        {
            var skins = workshopIds.Select(x => x.ToString()).ToList();
            while (skins.Count > 0)
            {
                var amount = skins.Count < 100 ? skins.Count : 100;
                WWWForm form = new WWWForm();
                form.AddField("itemcount",amount.ToString());
                for (var i = 0; i < amount; i++)
                {
                    form.AddField($"publishedfileids[{i}]",skins[0]);
                    skins.RemoveAt(0);
                }

                yield return form;
            }
        }
        #endregion

        #endregion

        #region Vars

        private PluginConfig _config;
        private PluginData _data;
        private const string ItemNamePlaceholder = "shortname или полное имя предмета на английском";
        private readonly Dictionary<string, HashSet<SkinInfo>> _customSkins = new Dictionary<string, HashSet<SkinInfo>>();
        private Coroutine _getSkinsCoroutine;
        private const string GetPublishedAPI =
            "https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/";

        #endregion

        #region Data

        private class PluginData
        {
            public Dictionary<ulong, string> ResolvedSkins = new Dictionary<ulong, string>();
        }

        #endregion

        #region Config and Data Initialization
        
        #region Data

        private void LoadData()
        {
            try
            {
                _data = Interface.Oxide.DataFileSystem.ReadObject<PluginData>(Title);
            }
            catch (Exception ex)
            {
                PrintError($"Failed to load data (is the file corrupt?) - no previously created recycles would work ({ex.Message})");
                _data = new PluginData();
            }
        }
        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Title, _data);
        }

        #endregion

        #region Config
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<PluginConfig>();
            }
            catch (Exception ex)
            {
                PrintError("Failed to load config file(is the config file corrupt ?)(" + ex.Message + ")");
                _config = PluginConfig.DefaultConfig;
            }

            if (CheckConfig())
                SaveConfig();

            var wrongNames = Pool.GetList<string>();
            PluginConfig.InitList(_config.NormalListString, _config.NormalList, wrongNames);
            if (wrongNames.Count != 0)
                PrintWarning("Wrong name(s) in the normal craft speed list:\n\t{0}", string.Join("\n\t", wrongNames));

            wrongNames.Clear();
            PluginConfig.InitList(_config.BlacklistString, _config.Blacklist, wrongNames);
            if (wrongNames.Count != 0)
                PrintWarning("Wrong name(s) in the blacklist:\n\t{0}", string.Join("\n\t", wrongNames));

            wrongNames.Clear();
            PluginConfig.InitList(_config.SkinSettings.ItemList.Items, _config.SkinSettings.ItemList.ItemsDefinitions, wrongNames);
            if (wrongNames.Count != 0)
                PrintWarning("Wrong name(s) in the skin list:\n\t{0}", string.Join("\n\t", wrongNames));

            Pool.FreeList(ref wrongNames);

            if (!_config.SkinSettings.IsPermissionEmpty)
                permission.RegisterPermission(_config.SkinSettings.Permission, this);
            if (!_config.IsCraftPermissionEmpty)
                permission.RegisterPermission(_config.CraftPermission, this);
            LoadData();
        }

        private bool CheckConfig()
        {
            var res = false;

            //version < 2.2.0
            if (_config.SkinSettings == null)
            {
                PrintWarning("Outdated config detected, SkinSettings added - check the config file.");
                _config.SkinSettings = SkinSettings.DefaultConfig;
                var randomSkins = Config["Случайный скин при крафте (в случае, если игрок не выбрал скин)"];
                if (randomSkins != null)
                    _config.SkinSettings.RandomSkins = Config.ConvertValue<bool>(randomSkins);
                res = true;
            }

            //version < 2.3.0
            if (!CheckExisting<string>("Случайные скины при крафте (в случае, если игрок не выбрал скин)",
                "Привилегия для получения случайного скина (null - все игроки получают случайные скины)"))
            {
                PrintWarning("Outdated config detected, permission to randomize skins on craft added to the config file.");
                _config.SkinSettings.Permission = "null";
                res = true;
            }

            //version < 2.4.0
            if (!CheckExisting<Dictionary<string, object>>("Случайные скины при крафте (в случае, если игрок не выбрал скин)",
                "Список предметов, которым присваивается случайный скин"))
            {
                PrintWarning("Outdated config detected, list that controls which items would have random skins on craft added to the config file.");
                _config.SkinSettings.ItemList = AllowedItemsConfig.DefaultConfig;
                res = true;
            }

            //Removed accidentally left in the version 2.3.0 variable from the config
            if (CheckExisting<bool>("IsPermissionEmpty"))
            {
                Config.Clear();
                res = true;
            }

            //version < 2.6.0
            if (!CheckExisting<string>("Формат сообщений в чате"))
            {
                PrintWarning("Outdated config detected, chat messages format was changed, setting up using existing data.");
                var prefix = Config.Get("Префикс сообщений в чате") as string ?? "[InstantCraft]";
                var color = Config.Get("Цвет префикса") as string ?? "#42d4f4";
                Config.Clear();
                _config.ChatFormat = $"<color={color}>{prefix}</color>: {{0}}";
                res = true;
            }
            //version 2.6.1 config fix
            if (_config.ChatFormat.Contains("{{0}}"))
            {
                _config.ChatFormat = _config.ChatFormat.Replace("{{0}}", "{0}");
                Puts("Chat format fixed.");
                res = true;
            }

            //version < 2.7.0
            if (!CheckExisting<string>("Привилегия для мгновенного крафта (null - мгновенный крафт работает для всех)"))
            {
                PrintWarning("Outdated config detected, permission to use instant craft added to the config file.");
                _config.CraftPermission = "null";
                res = true;
            }

            return res;
        }
        protected override void LoadDefaultConfig()
        {
            _config = PluginConfig.DefaultConfig;
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        #endregion

        #endregion

        #region Main method

        private object OnItemCraft(ItemCraftTask task)
        {
            var player = task.owner;
            ulong playerid = 5824512;
            var amount = task.amount;
            var invAmount = task.owner.inventory.GetAmount(task.blueprint.targetItem.itemid);
            var lastSlot = false;
            var freeSlots = FreeSlots(player);

            if (freeSlots <= 0)
            {
                if (invAmount == 0 || invAmount >= task.blueprint.targetItem.stackable)
                {
                    task.cancelled = true;
                    Interface.CallHook("OnItemCraftCancelled", task);
                    RefundIngredients(task.blueprint, player, task.amount);

                    SendToChat(player, "InvFull");
                    return null;
                }

                lastSlot = true;
            }

            if (_config.Blacklist.Contains(task.blueprint.targetItem))
            {
                task.cancelled = true;
                Interface.CallHook("OnItemCraftCancelled", task);
                RefundIngredients(task.blueprint, player, task.amount);

                SendToChat(player, "Blocked");
                return null;
            }

            var normalSpeed = _config.NormalList.Contains(task.blueprint.targetItem);
            if (normalSpeed || !_config.CanInstantCraft(player))
            {
                if (normalSpeed)
                    SendToChat(player, "NormalSpeed");
                if (task.blueprint.targetItem.HasSkins && task.skinID == 0)
                    task.skinID = RandomSkin(player, task.blueprint.targetItem, true).InventoryId;
                return null;
            }

            task.endTime = 1f;
            int refund;
            if (lastSlot)
            {
                var spaceLeft = task.blueprint.targetItem.stackable - invAmount;
                var canCraft = spaceLeft / task.blueprint.amountToCreate;
                refund = amount - canCraft;
                if (refund > 0)
                {
                    SendToChat(player, "NotEnoughSlots", canCraft, amount);
                    GiveItem(player, canCraft * task.blueprint.amountToCreate, task);
                    RefundIngredients(task.blueprint, player, refund);
                    task.cancelled = true;
                    task.amount = refund;
                    Interface.CallHook("OnItemCraftCancelled", task);
                    return null;
                }

                GiveItem(player, amount * task.blueprint.amountToCreate, task);
                task.cancelled = true;
                return null;
            }

            var totalAmount = amount * task.blueprint.amountToCreate;
            var stacks = CalculateStacks(totalAmount, task.blueprint.targetItem).ToList();
            if (_config.SplitStacks || task.blueprint.targetItem.stackable == 1)
            {
                if (stacks.Count > freeSlots)
                {
                    var refundStacks = stacks.Count() - freeSlots - 1;
                    var refundAmount = refundStacks * stacks.ElementAt(0) + stacks.Last();
                    refund = refundAmount / task.blueprint.amountToCreate;
                    var created = 0;
                    for (var i = 0; i < freeSlots; i++)
                    {
                        GiveItem(player, stacks.ElementAt(i), task);
                        created += stacks.ElementAt(i);
                    }

                    RefundIngredients(task.blueprint, player, refund);
                    SendToChat(player, "NotEnoughSlots", created, amount);
                    task.cancelled = true;
                    return null;
                }

                if (stacks.Count > 1)
                {
                    foreach (var stackAmount in stacks)
                        GiveItem(player, stackAmount, task);
                    task.cancelled = true;
                    return null;
                }
            }

            GiveItem(player, totalAmount, task);
            task.cancelled = true;
            return null;
        }

        #endregion

        #region Give item method


        private void GiveItem(BasePlayer player, int amount, ItemCraftTask task)
        {
            var def = task.blueprint.targetItem;

            if (amount <= 0)
            {
                PrintWarning(
                    $"Player \"{player.displayName}\" is about to create an item {def.shortname} with amount <= 0!\nReport to the developer!");
                return;
            }

            if (!player.IsConnected)
                return;
            var randomSkin = task.skinID == 0 ? RandomSkin(player, def) : new SkinInfo(task);

            var item = ItemManager.Create(def, amount, randomSkin.WorkshopId);
            // ReSharper disable CompareOfFloatsByEqualityOperator
            if (item.hasCondition && task.conditionScale != 1f)
            {
                item.maxCondition *= task.conditionScale;
                item.condition = item.maxCondition;
            }
            // ReSharper restore CompareOfFloatsByEqualityOperator
            item.OnVirginSpawn();

            Facepunch.Rust.Analytics.Crafting(task.blueprint.targetItem.shortname, randomSkin.InventoryId);

            player.Command("note.craft_done", task.taskUID, 1, amount);

            Interface.CallHook("OnItemCraftFinished", task, item);

            if (task.instanceData != null)
                item.instanceData = task.instanceData;

            if (!string.IsNullOrEmpty(task.blueprint.UnlockAchievment) && ConVar.Server.official)
            {
                player.ClientRPCPlayer(null, player, "RecieveAchievement", task.blueprint.UnlockAchievment);
            }

            player.GiveItem(item, BaseEntity.GiveItemReason.Crafted);
        }

        #endregion

        #region Localization

        private string GetMsg(string key, string userId = null)
        {
            return lang.GetMessage(key, this, userId);
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["InvFull"] = "Your <color=yellow>inventory</color> is <color=red>full!</color>",
                ["NormalSpeed"] =
                    "This item <color=red>was removed</color> from InstantCraft and will be crafted with <color=yellow>normal</color> speed.",
                ["Blocked"] = "Crafting of that item is <color=red>blocked</color>!",
                ["NotEnoughSlots"] =
                    "<color=red>Not enough slots</color> in the inventory! Created <color=green>{0}</color>/<color=green>{1}</color>"
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["InvFull"] = "В вашем <color=yellow>инвентаре</color> <color=red>нет свободного места!</color>",
                ["NormalSpeed"] =
                    "Данный предмет <color=red>убран</color> из мгновенного крафта и будет создаваться с <color=yellow>обычной</color> скоростью.",
                ["Blocked"] = "Крафт данного предмета <color=red>запрещён</color>",
                ["NotEnoughSlots"] =
                    "<color=red>Недостаточно слотов</color> для крафта! Создано <color=green>{0}</color>/<color=green>{1}</color>"
            }, this, "ru");
        }

        #endregion

        #region Skins

        private IEnumerable<SkinInfo> AvailableSkins(ItemDefinition def, bool withInventoryId = false)
        {
            if (_config.SkinSettings.UseInGame && def.skins?.Length > 0)
            {
                foreach (var skin in def.skins)
                {
                    yield return new SkinInfo(skin);
                }
            }

            if (_config.SkinSettings.UseAccepted && def.skins2?.Length > 0)
            {
                foreach (var skin in def.skins2)
                {
                    yield return new SkinInfo(skin);
                }
            }

            if (!_config.SkinSettings.UseCustom || withInventoryId)
                yield break;

            HashSet<SkinInfo> hashSet;
            if (!_customSkins.TryGetValue(def.shortname, out hashSet))
                yield break;

            foreach (var skin in hashSet)
            {
                yield return skin;
            }
        }
        private SkinInfo RandomSkin(BasePlayer owner, ItemDefinition def, bool withInventoryId = false)
        {
            if (!ShouldRandomize(owner, def))
                return new SkinInfo();
            return AvailableSkins(def, withInventoryId).ToList().GetRandom();
        }

        private bool TryAddCustomSkin(string shortname, SkinInfo info)
        {
            if (info.WorkshopId == 0)
                return false;

            HashSet<SkinInfo> hashSet;
            if (_customSkins.TryGetValue(shortname, out hashSet))
                return hashSet.Add(info);

            hashSet = new HashSet<SkinInfo>();
            _customSkins[shortname] = hashSet;
            return hashSet.Add(info);
        }

        private struct SkinInfo
        {
            public readonly int InventoryId;
            public readonly ulong WorkshopId;

            public SkinInfo(ulong workshopId)
            {
                InventoryId = 0;
                WorkshopId = workshopId;
            }

            public SkinInfo(ItemCraftTask task)
            {
                InventoryId = task.skinID;
                WorkshopId = ItemDefinition.FindSkin(task.blueprint.targetItem.itemid, task.skinID);
            }

            public SkinInfo(IPlayerItemDefinition def)
            {
                InventoryId = def.DefinitionId;
                WorkshopId = def.WorkshopDownload;
            }

            public SkinInfo(ItemSkinDirectory.Skin skin)
            {
                InventoryId = skin.id;
                WorkshopId = (ulong)InventoryId;
            }

            public override string ToString()
            {
                return $"[{InventoryId}]: {WorkshopId}";
            }
        }

        #endregion

        #region Helpers

        private bool CheckExisting<T>(params string[] path)
        {
            var value = Config.Get(path);
            return value is T;
        }

        private bool ShouldRandomize(BasePlayer player, ItemDefinition definition)
        {
            if (!_config.SkinSettings.RandomSkins)
                return false;
            if (!_config.SkinSettings.ItemList.ShouldRandomizeSkin(definition))
                return false;
            return _config.SkinSettings.CanUse(player);
        }

        //Thanks Norn for this functions and his MagicCraft plugin!
        private static IEnumerable<int> CalculateStacks(int amount, ItemDefinition item)
        {
            var results = Enumerable.Repeat(item.stackable, amount / item.stackable);
            if (amount % item.stackable > 0) results = results.Concat(Enumerable.Repeat(amount % item.stackable, 1));
            return results;
        }

        private static void RefundIngredients(ItemBlueprint bp, BasePlayer player, int amount = 1)
        {
            bp.ingredients.ForEach(x =>
            {
                player.GiveItem(ItemManager.CreateByItemID(x.itemid, Convert.ToInt32(x.amount) * amount));
            });
        }

        private void SendToChat(BasePlayer player, string langKey, params object[] args)
        {
            var message = GetMsg(langKey, player.UserIDString);
            if (args.Length != 0)
                message = string.Format(message, args);
            player.SendConsoleCommand("chat.add", 2, _config.ChatMessageSteamId, string.Format(_config.ChatFormat, message));
        }

        private static int FreeSlots(BasePlayer player)
        {
            var main = player.inventory.containerMain.capacity - player.inventory.containerMain.itemList.Count;
            var belt = player.inventory.containerBelt.capacity - player.inventory.containerBelt.itemList.Count;
            return main + belt;
        }

        #endregion
    }
}
/////////////////////////////////////////////////////////////////////////////////////////////////////////
