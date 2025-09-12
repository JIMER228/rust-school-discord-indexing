using Facepunch;
using HarmonyLib;
using Network;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Oxide.Plugins.CustomItemDefinitionExtensions;
using SilentOrbit.ProtocolBuffers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using UnityEngine;

#region Library
namespace Oxide.Plugins
{
    [Info("CustomItemDefinitions", "0xF // dsc.gg/0xf-plugins", "2.1.1")]
    [Description("Library of the Future. Allows you to create your own full-fledged custom items with own item definition.")]
    public class CustomItemDefinitions : RustPlugin
    {
        #region Consts
        public const ItemDefinition.Flag CUSTOM_DEFINITION_FLAG = (ItemDefinition.Flag)128;
        public static readonly ItemDefinition FallbackItemDefinition = ItemManager.FindItemDefinition("coal");
        private static readonly Regex regexNumeric = new Regex(@"^-?[0-9]+$");
        #endregion

        #region CustomItemDefinition Class
        public class CustomItemDefinition : Pool.IPooled
        {
            public int parentItemId;
            public string shortname;
            public int? itemId;
            public Translate.Phrase defaultName;
            public Translate.Phrase defaultDescription;
            public ulong defaultSkinId;
            public int? maxStackSize;
            public ItemCategory? category;
            public ItemDefinition.Flag flags;
            public ItemMod[] itemMods;
            public bool repairable;
            public bool craftable;
            public List<ItemAmount> blueprintIngredients;
            public int workbenchLevelRequired;
            public List<(Translate.Phrase label, Translate.Phrase text)> staticOwnerships;

            public ItemDefinition ParentDefinition => ItemManager.FindItemDefinition(parentItemId);

            internal void FillEmptyValues()
            {
                if (!itemId.HasValue && !string.IsNullOrEmpty(shortname))
                    itemId = shortname.GetHashCode();

                if (defaultName != null && string.IsNullOrEmpty(defaultName.token) && !string.IsNullOrEmpty(shortname))
                    defaultName.token = shortname;

                if (defaultDescription != null && string.IsNullOrEmpty(defaultDescription.token) && !string.IsNullOrEmpty(shortname))
                    defaultDescription.token = $"{shortname}.desc";
            }

            internal void Validate(RustPlugin plugin)
            {
                if (ContainsDefinitionInHashSet(this, plugin))
                    throw new Exception(string.Format("Error by the plugin \"{0}\": The provided CustomItemDefinition ({1}) is already contained", plugin.Name, shortname));

                if (string.IsNullOrEmpty(shortname) || parentItemId == default)
                    throw new Exception(string.Format("Error of incorrect data provided by the plugin \"{0}\": The fields shortname, parentItemId is required", plugin.Name));

                if (ItemManager.FindItemDefinition(shortname) != null)
                    throw new Exception(string.Format("Error of incorrect data provided by the plugin \"{0}\": Shortname must be unique! The provided shortname - \"{1}\"", plugin.Name, shortname));

                if (ItemManager.FindItemDefinition(itemId.Value) != null)
                    throw new Exception(string.Format("Error of incorrect data provided by the plugin \"{0}\": ItemId must be unique! The provided itemId - \"{1}\"", plugin.Name, itemId));

                ItemDefinition parent = this.ParentDefinition;
                if (parent == null)
                    throw new Exception(string.Format("Error of incorrect data provided by the plugin \"{0}\": ItemDefinition by parentItemId not found! The provided parentItemId - \"{1}\"", plugin.Name, parentItemId));

                if (parent.gameObject.scene.isLoaded)
                    throw new Exception(string.Format("Error by the plugin \"{0}\": You cannot use a custom ItemDefinition as a parent!", plugin.Name));
            }

            public static CustomItemDefinition FromObject(object @object)
            {
                Type cidType = typeof(CustomItemDefinition);
                Type argType = @object.GetType();
                CustomItemDefinition @new = Pool.Get<CustomItemDefinition>();

                foreach (PropertyInfo propertyInfo in argType.GetProperties())
                    SetField(@new, propertyInfo.Name, propertyInfo.GetValue(@object));

                foreach (FieldInfo fieldInfo in argType.GetFields())
                    SetField(@new, fieldInfo.Name, fieldInfo.GetValue(@object));

                void SetField(CustomItemDefinition cid, string fieldName, object value)
                {
                    FieldInfo fieldInfo = cidType.GetField(fieldName);
                    if (fieldInfo == null)
                    {
                        PluginInstance?.PrintWarning(string.Format("The field named \"{0}\" is missing in the CustomItemDefinition class, skipped.", fieldName));
                        return;
                    }
                    try
                    {
                        if (fieldInfo.FieldType == typeof(Translate.Phrase) && value is string @string)
                            value = new Translate.Phrase(string.Empty, @string);

                        fieldInfo.SetValue(cid, value);
                    }
                    catch (Exception ex)
                    {
                        PluginInstance?.PrintWarning(string.Format("The field named \"{0}\" has error [ {1} ], skipped.", fieldName, ex.Message));
                    }
                }
                return @new;
            }

            public void EnterPool()
            {
                parentItemId = default;
                shortname = default;
                itemId = default;
                defaultName = default;
                defaultDescription = default;
                defaultSkinId = default;
                maxStackSize = default;
                category = default;
                flags = default;
                itemMods = default;
                repairable = default;
                craftable = default;
                blueprintIngredients = default;
                workbenchLevelRequired = default;
                staticOwnerships = default;
            }

            public void LeavePool() { }
        }
        #endregion

        #region Variables
        private static CustomItemDefinitions PluginInstance;
        private static Dictionary<int, ItemDefinition> ExistingDefinitions;
        private static Dictionary<Plugin, HashSet<CustomItemDefinition>> CustomDefinitions = new Dictionary<Plugin, HashSet<CustomItemDefinition>>();
        private static Dictionary<Translate.Phrase, RustPlugin> pluginByPhrase = new Dictionary<Translate.Phrase, RustPlugin>();
        private static Dictionary<string, string> blueprintBaseDisplayName = new Dictionary<string, string>();

        private static AccessTools.FieldRef<NetWrite, BufferStream> NetWriteGetBufferStream = AccessTools.FieldRefAccess<NetWrite, BufferStream>(AccessTools.Field("Network.NetWrite:stream"));
        private static AccessTools.FieldRef<RustPlugin, Lang> PluginGetLang = AccessTools.FieldRefAccess<RustPlugin, Lang>(AccessTools.Field(typeof(RustPlugin), "lang"));
#if OXIDE
        private static AccessTools.FieldRef<Lang, Dictionary<string, Dictionary<string, string>>> LangGetLangFiles = AccessTools.FieldRefAccess<Lang, Dictionary<string, Dictionary<string, string>>>(AccessTools.Field(typeof(Lang), "langFiles"));
#endif

        private static ProtoBuf.Item.InstanceData BlueprintInstanceData = new ProtoBuf.Item.InstanceData()
        {
            ShouldPool = false,
            blueprintTarget = 1394042569
        };
        private const ulong BlueprintBaseIconSkinId = 3530644207;

        private bool IsTranslateInitialized { get => Translate.englishBaseStrings != null; }

        #endregion

        #region Hooks
        void Init()
        {
            PluginInstance = this;

            CacheAlreadyCreated();

            Patcher patcher = new Patcher(this, this.HarmonyInstance);
            patcher.PatchNestedClasses();
            patcher.PatchWriteToStreamClass();
        }

        private static string GetBlueprintBaseDisplayName(string language)
        {
            if (blueprintBaseDisplayName.TryGetValue(language, out string value))
                return value;

            TextAsset textAsset = FileSystem.Load<TextAsset>($"assets/localization/{language}/engine.json", true);
            if (textAsset != null)
            {
                Dictionary<string, string> @object = JsonConvert.DeserializeObject<Dictionary<string, string>>(textAsset.text);
                if (@object != null)
                {
                    value = @object["blueprintbase.name"];
                    Save(value);
                }
                @object.Clear();
            }

            if (value == null)
                Save(GetBlueprintBaseDisplayName("en"));


            return value;

            void Save(string value)
            {
                blueprintBaseDisplayName[language] = value;
            }
        }

        void Loaded()
        {
            if (IsTranslateInitialized)
                CallLibraryLoadedHookGlobal();
        }

        private void OnTranslateInitialized()
        {
            CallLibraryLoadedHookGlobal();
        }

        void OnPluginLoaded(Plugin plugin)
        {
            CallLibraryLoadedHook(plugin);
        }

        private void CallLibraryLoadedHookGlobal()
        {
            foreach (Plugin plugin in Interface.Oxide.RootPluginManager.GetPlugins())
                CallLibraryLoadedHook(plugin);
        }

        private void CallLibraryLoadedHook(Plugin plugin)
        {
            if (IsTranslateInitialized && !CustomDefinitions.ContainsKey(plugin))
                plugin.CallHook("OnCIDLoaded", this);
        }

        private void Unload()
        {
            List<Plugin> list = Pool.Get<List<Plugin>>();
            list.AddRange(CustomDefinitions.Keys);

            foreach (Plugin plugin in list)
                UnloadPluginItemDefinitions(plugin);

            Pool.FreeUnmanaged(ref list);

            CustomDefinitions.Clear();
            Interface.Oxide.CallHook("OnCIDUnloaded");
        }

        private void OnEntitySaved(BaseNetworkable entity, BaseNetworkable.SaveInfo saveInfo)
        {
            try
            {
                Mutate.ToClientSide(saveInfo.msg, saveInfo.forConnection);
            }
            catch (Exception ex)
            {
                ProtoWriteToStream.LibraryFinalizer(ex);
            }
        }

        void OnLootNetworkUpdate(PlayerLoot loot)
        {
            RepairBench repairBench = loot.entitySource as RepairBench;
            if (repairBench == null)
                return;

            Item repairableItem = repairBench.inventory?.GetSlot(0);
            if (repairableItem == null)
                return;

            ItemDefinition itemDefinition = repairableItem.info;
            if (!itemDefinition.Blueprint || !itemDefinition.condition.repairable)
                return;

            BasePlayer player = loot.baseEntity;

            if (IsValidCID(itemDefinition) && player.blueprints.HasUnlocked(itemDefinition))
                SendFakeUnlockedBlueprint(player, repairableItem.info.Parent.itemid);
            else
                player.SendNetworkUpdateImmediate();
        }

        object OnItemCraft(IndustrialCrafter crafter, ItemBlueprint blueprint)
        {
            if (IsValidCID(blueprint.targetItem) && !blueprint.userCraftable)
                return false;
            return null;
        }

        void OnPlayerLanguageChanged(BasePlayer player)
        {
            PlayerLanguages.OnPlayerLanguageChanged(player.userID);

            {
                player.inventory.containerBelt.dirty = true;
                player.inventory.containerMain.dirty = true;
                player.inventory.containerWear.dirty = true;
            }
        }

        private static void OnItemDefinitionBroken(Item item, ProtoBuf.Item protoItem)
        {
            UnityEngine.Debug.LogWarning("Item has broken definition, the fallback item definition will be applied to it.");
            item.info = FallbackItemDefinition;
            item.text = protoItem.itemid.ToString();
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["itemownership.description.phrase"] = "ITEM DESCRIPTION",
            }, this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["itemownership.description.phrase"] = "DESCRIPCIÓN DEL ARTÍCULO",
            }, this, "es-ES");
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["itemownership.description.phrase"] = "ОПИСАНИЕ ПРЕДМЕТА",
            }, this, "ru");
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["itemownership.description.phrase"] = "ОПИС ПРЕДМЕТУ",
            }, this, "uk");
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["itemownership.description.phrase"] = "項目説明",
            }, this, "ja");
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["itemownership.description.phrase"] = "项目描述",
            }, this, "zh-CN");
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["itemownership.description.phrase"] = "項目描述",
            }, this, "zh-TW");
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["itemownership.description.phrase"] = "항목 설명",
            }, this, "ko");
        }
        #endregion

        #region Commands
        [ConsoleCommand("itemid")]
        private void itemid(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin)
                return;

            string shortname = arg.GetString(0);
            if (string.IsNullOrEmpty(shortname))
                return;

            ItemDefinition itemDefinition = ItemManager.FindItemDefinition(shortname);
            if (itemDefinition == null)
            {
                arg.ReplyWith("Item definion for the specified short name was not found.");
                return;
            }

            arg.ReplyWith(itemDefinition.itemid);
        }

        [ConsoleCommand("shortname")]
        private void shortname(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin)
                return;

            int itemId = arg.GetInt(0);
            if (itemId == default)
                return;

            ItemDefinition itemDefinition = ItemManager.FindItemDefinition(itemId);
            if (itemDefinition == null)
            {
                arg.ReplyWith("Item definion for the specified item id was not found.");
                return;
            }

            arg.ReplyWith(itemDefinition.shortname);
        }
        #endregion

        #region Classes
        private static class ProtoWriteToStream
        {
            internal const string HANDLE_METHOD_NAME = nameof(Handle);

            private static Connection GetConnection(BufferStream stream)
            {
                if (NetWritePool.BufferToStream.TryGetValue(stream, out NetWrite netWrite))
                    return netWrite.connections.FirstOrDefault();
                return null;
            }

            private static void Handle(ProtoBuf.Entity __instance, BufferStream stream)
            {
                Connection connection = GetConnection(stream);
                if (connection == null)
                    return;

                Mutate.ToClientSide(__instance, connection);
            }

            private static void Handle(ProtoBuf.PlayerUpdateLoot __instance, BufferStream stream)
            {
                Connection connection = GetConnection(stream);
                if (connection == null)
                    return;

                if (__instance.containers != null)
                    Mutate.ToClientSide(__instance.containers, connection);
            }

            private static void Handle(ProtoBuf.UpdateItem __instance, BufferStream stream)
            {
                Connection connection = GetConnection(stream);
                if (connection == null)
                    return;

                Mutate.ToClientSide(__instance.item, connection);
            }

            private static void Handle(ProtoBuf.UpdateItemContainer __instance, BufferStream stream)
            {
                Connection connection = GetConnection(stream);
                if (connection == null)
                    return;

                Mutate.ToClientSide(__instance.container, connection);
            }

            private static void Handle(ref ProtoBuf.VendingMachine.SellOrderContainer __instance, BufferStream stream)
            {
                __instance = __instance.Copy();
                foreach (ProtoBuf.VendingMachine.SellOrder order in __instance.sellOrders)
                    Mutate.ToClientSide(order);
            }

            private static void Handle(ProtoBuf.VendingMachinePurchaseHistoryMessage __instance, BufferStream stream)
            {
                Mutate.ToClientSide(__instance);
            }

            private static void Handle(ProtoBuf.ItemAmountList __instance, BufferStream stream)
            {
                Connection connection = GetConnection(stream);
                if (connection == null)
                    return;

                List<int> itemID = __instance.itemID;
                int count = itemID.Count;
                for (int i = 0; i < count; i++)
                {
                    int itemId = itemID[i];
                    if (Mutate.Item.ItemId(ref itemId))
                        itemID[i] = itemId;
                }
            }

            private static void Handle(ProtoBuf.AppEntityPayload __instance)
            {
                List<ProtoBuf.AppEntityPayload.Item> items = __instance.items;
                int count = items.Count;
                for (int i = 0; i < count; i++)
                {
                    int itemId = items[i].itemId;
                    if (Mutate.Item.ItemId(ref itemId))
                        items[i].itemId = itemId;
                }
            }

            private static void Handle(ProtoBuf.IndustrialConveyorTransfer __instance)
            {
                List<ProtoBuf.IndustrialConveyorTransfer.ItemTransfer> transfers = __instance.ItemTransfers;
                int count = transfers.Count;
                for (int i = 0; i < count; i++)
                {
                    ProtoBuf.IndustrialConveyorTransfer.ItemTransfer transfer = transfers[i];
                    int itemId = transfer.itemId;
                    if (Mutate.Item.ItemId(ref itemId))
                        transfers[i] = new ProtoBuf.IndustrialConveyorTransfer.ItemTransfer { itemId = itemId, amount = transfer.amount };
                }
            }

            internal static Exception LibraryFinalizer(Exception __exception)
            {
                if (__exception != null)
                    if (__exception != null)
                        Interface.Oxide.LogException($"[{nameof(CustomItemDefinitions)}] There was an error in the client side package handler! This is a very important part! Please inform the developer about it!", __exception);
                return null;
            }

            internal static Exception ExternalFinalizer(Exception __exception)
            {
                if (__exception != null)
                    Interface.Oxide.LogException(string.Format("[{0}]", __exception.TargetSite.DeclaringType), __exception);
                return null;
            }
        }

        private static class PlayerLanguages
        {
            private static Dictionary<ulong, string> playerLanguages = new();

            public static void OnPlayerLanguageChanged(ulong userId)
            {
                UpdatePlayerLanguage(userId);
            }

            private static void UpdatePlayerLanguage(ulong userId)
            {
                playerLanguages[userId] = PluginInstance.lang.GetLanguage(userId.ToString());
            }

            public static string GetPlayerLanguage(ulong userId)
            {
                if (!playerLanguages.TryGetValue(userId, out string lang))
                {
                    UpdatePlayerLanguage(userId);
                    lang = playerLanguages[userId];
                }
                return lang;
            }
        }

        private static class Mutate
        {
            public class Item
            {
                public class Dictionaries
                {
                    public static Dictionary<int, int> ItemId = new();
                    public static Dictionary<int, ulong> SkinId = new();
                    public static Dictionary<int, Translate.Phrase> NamePhrase = new();
                    public static Dictionary<int, Translate.Phrase> DescriptionPhrase = new();
                    public static Dictionary<int, List<(Translate.Phrase label, Translate.Phrase text)>> StaticOwnerships = new();

                    public static void Remove(int itemID)
                    {
                        ItemId.Remove(itemID);
                        SkinId.Remove(itemID);
                        NamePhrase.Remove(itemID);
                        StaticOwnerships.Remove(itemID);
                    }
                }

                private static void AddOwnership(ProtoBuf.Item item, string label, string text)
                {
                    item.ownership ??= Pool.Get<List<ProtoBuf.ItemOwnershipAmount>>();
                    ProtoBuf.ItemOwnershipAmount itemOwnershipAmount = Pool.Get<ProtoBuf.ItemOwnershipAmount>();
                    itemOwnershipAmount.username = label;
                    itemOwnershipAmount.reason = text;
                    itemOwnershipAmount.amount = item.amount;
                    item.ownership.Add(itemOwnershipAmount);
                }

                public static bool IsBlueprint(ProtoBuf.Item item) => item.instanceData != null && item.instanceData.blueprintTarget != default;

                public static bool ItemId(ref int itemID)
                {
                    if (Dictionaries.ItemId.TryGetValue(itemID, out int target))
                    {
                        itemID = target;
                        return true;
                    }
                    return false;
                }

                public static bool SkinId(int itemID, ref ulong skinID)
                {
                    if (Dictionaries.SkinId.TryGetValue(itemID, out ulong target))
                    {
                        skinID = target;
                        return true;
                    }
                    return false;
                }

                public static bool Name(ProtoBuf.Item item, Connection connection)
                {
                    if (!string.IsNullOrEmpty(item.name))
                        return false;

                    bool isBlueprint = IsBlueprint(item);
                    int itemId = isBlueprint ? item.instanceData.blueprintTarget : item.itemid;

                    if (!Dictionaries.NamePhrase.TryGetValue(itemId, out Translate.Phrase phrase))
                        return false;

                    if (string.IsNullOrEmpty(phrase.token))
                        return false;

                    string language = PlayerLanguages.GetPlayerLanguage(connection.userid);

                    item.name = GetMessageByLanguage(phrase.token, pluginByPhrase[phrase], language);
                    if (isBlueprint)
                        item.name = item.name + " " + GetBlueprintBaseDisplayName(language);
                    return true;
                }

                public static bool Description(ProtoBuf.Item item, Connection connection)
                {
                    if (!Dictionaries.DescriptionPhrase.TryGetValue(item.itemid, out Translate.Phrase phrase))
                        return false;

                    if (string.IsNullOrEmpty(phrase.token))
                        return false;

                    string language = PlayerLanguages.GetPlayerLanguage(connection.userid);

                    string text = GetMessageByLanguage(phrase.token, pluginByPhrase[phrase], language);
                    if (string.IsNullOrEmpty(text))
                        return false;

                    string label = "\r\r\r\r\r\r\r\r\r\r\r" + PluginInstance.GetMessageByLanguage("itemownership.description.phrase", language);
                    AddOwnership(item, label, text);
                    return true;
                }

                public static bool StaticOwnerShips(ProtoBuf.Item item, Connection connection)
                {
                    if (!Dictionaries.StaticOwnerships.TryGetValue(item.itemid, out var ownerships))
                        return false;

                    string language = null;
                    foreach (var ownership in ownerships)
                    {
                        language ??= PlayerLanguages.GetPlayerLanguage(connection.userid);

                        string label = GetTranslatedText(ownership.label);
                        string text = GetTranslatedText(ownership.text);

                        string GetTranslatedText(Translate.Phrase phrase)
                        {
                            if (phrase.IsValid())
                            {
                                string result = GetMessageByLanguage(phrase.token, pluginByPhrase[phrase], language);
                                if (!string.IsNullOrEmpty(result))
                                    return result;
                            }

                            return phrase.english;
                        }

                        AddOwnership(item, label, text);
                    }
                    return true;
                }

                public static void ToFallback(ProtoBuf.Item item)
                {
                    item.itemid = FallbackItemDefinition.itemid;
                    item.name = $"Fallback Item [{item.UID}]";
                }
            }


            public static void ToClientSide(ProtoBuf.Item item, Connection connection)
            {
                if (ItemManager.FindItemDefinition(item.itemid) == null)
                {
                    Item.ToFallback(item);
                    return;
                }

                Item.Name(item, connection);
                Item.Description(item, connection);
                Item.StaticOwnerShips(item, connection);
                Item.SkinId(item.itemid, ref item.skinid);

                if (Item.IsBlueprint(item) && Mutate.Item.Dictionaries.ItemId.ContainsKey(item.instanceData.blueprintTarget))
                {
                    item.instanceData = BlueprintInstanceData;
                    if (item.skinid == default)
                        item.skinid = BlueprintBaseIconSkinId;
                }
                else
                {
                    Item.ItemId(ref item.itemid);
                }

                if (item.contents != null)
                    ToClientSide(item.contents, connection);

            }

            public static void ToClientSide(ProtoBuf.ItemContainer container, Connection connection)
            {
                if (container != null && container.contents != null)
                {
                    foreach (ProtoBuf.Item item in container.contents)
                        ToClientSide(item, connection);
                }
            }

            public static void ToClientSide(List<ProtoBuf.ItemContainer> containers, Connection connection)
            {
                foreach (ProtoBuf.ItemContainer container in containers)
                    ToClientSide(container, connection);
            }

            public static void ToClientSide(ProtoBuf.BasePlayer player, Connection connection)
            {
                ToClientSide(player.inventory, connection);
                if (player.itemCrafter != null)
                    ToClientSide(player.itemCrafter, connection);
            }

            public static void NoteInvToClientSide(ref object[] args, Connection connection)
            {
                if (args.Length > 2 && args[0] is int itemId)
                {
                    if (Mutate.Item.Dictionaries.ItemId.ContainsKey(itemId))
                    {
                        List<object> list = Pool.Get<List<object>>();
                        list.Add(Mutate.Item.Dictionaries.ItemId[itemId].ToString());
                        list.Add(args[1]);
                        if (args.Length > 2)
                        {
                            if (args[2] is string arg2Name && !string.IsNullOrEmpty(arg2Name))
                            {
                                list.Add(arg2Name);
                            }
                            else if (Mutate.Item.Dictionaries.NamePhrase.TryGetValue(itemId, out Translate.Phrase phrase))
                            {
                                string lang = PlayerLanguages.GetPlayerLanguage(connection.userid);
                                string name = GetMessageByLanguage(phrase.token, pluginByPhrase[phrase], lang);
                                list.Add(name);
                            }
                        }
                        if (args.Length > 3)
                            list.AddRange(args.Skip(3));

                        args = list.ToArray();

                        Pool.FreeUnmanaged(ref list);
                    }
                }
            }

            public static void ToClientSide(ProtoBuf.PlayerInventory inventory, Connection connection)
            {
                if (inventory.invBelt != null)
                    ToClientSide(inventory.invBelt, connection);
                if (inventory.invMain != null)
                    ToClientSide(inventory.invMain, connection);
                if (inventory.invWear != null)
                    ToClientSide(inventory.invWear, connection);
            }

            public static void ToClientSide(ProtoBuf.ItemCrafter crafter, Connection connection)
            {
                foreach (ProtoBuf.ItemCrafter.Task task in crafter.queue)
                    Item.ItemId(ref task.itemID);
            }

            public static void ToClientSide(ProtoBuf.VendingMachine.SellOrder sellOrder)
            {
                Item.ItemId(ref sellOrder.itemToSellID);
                Item.ItemId(ref sellOrder.currencyID);
            }

            public static void ToClientSide(ProtoBuf.IndustrialConveyor.ItemFilter filter, Connection connection)
            {
                Item.ItemId(ref filter.itemDef);
            }

            public static void ToClientSide(ProtoBuf.WeaponRackItem weaponRackItem, Connection connection)
            {
                Item.SkinId(weaponRackItem.itemID, ref weaponRackItem.skinid);
                Item.ItemId(ref weaponRackItem.itemID);
            }

            public static void ToClientSide(ProtoBuf.VendingMachinePurchaseHistoryMessage purchaseHistoryMessage)
            {
                if (purchaseHistoryMessage == null)
                    return;

                if (purchaseHistoryMessage.transactions != null)
                    foreach (ProtoBuf.VendingMachinePurchaseHistoryEntryMessage entryMessage in purchaseHistoryMessage.transactions)
                        Item.ItemId(ref entryMessage.itemID);

                if (purchaseHistoryMessage.smallTransactions != null)
                    foreach (ProtoBuf.VendingMachinePurchaseHistoryEntrySmallMessage entryMessage in purchaseHistoryMessage.smallTransactions)
                        Item.ItemId(ref entryMessage.itemID);
            }

            public static void ToClientSide(ProtoBuf.Mannequin mannequin, Connection connection)
            {
                if (mannequin.clothingItems == null)
                    return;

                foreach (ProtoBuf.Mannequin.ClothingItem clothingItem in mannequin.clothingItems)
                    ToClientSide(clothingItem, connection);
            }

            public static void ToClientSide(ProtoBuf.Mannequin.ClothingItem clothingItem, Connection connection)
            {
                Item.SkinId(clothingItem.itemId, ref clothingItem.skin);
                Item.ItemId(ref clothingItem.itemId);
            }

            public static void ToClientSide(ProtoBuf.BuriedItems buriedItems, Connection connection)
            {
                if (buriedItems.buriedItems == null)
                    return;

                foreach (ProtoBuf.BuriedItems.StoredBuriedItem clothingItem in buriedItems.buriedItems)
                    ToClientSide(clothingItem, connection);
            }

            public static void ToClientSide(ProtoBuf.BuriedItems.StoredBuriedItem buriedItem, Connection connection)
            {
                Item.SkinId(buriedItem.itemId, ref buriedItem.skinId);
                Item.ItemId(ref buriedItem.itemId);
            }

            public static void ToClientSide(ProtoBuf.Entity entity, Connection connection)
            {
                if (entity.basePlayer != null)
                    ToClientSide(entity.basePlayer, connection);
                if (entity.worldItem?.item != null)
                    ToClientSide(entity.worldItem?.item, connection);
                if (entity.storageBox?.contents != null)
                    ToClientSide(entity.storageBox.contents, connection);
                if (entity.loot?.contents != null)
                    ToClientSide(entity.loot.contents, connection);
                if (entity.mailbox?.inventory != null)
                    ToClientSide(entity.mailbox?.inventory, connection);
                if (entity.lootableCorpse?.privateData?.container != null)
                    ToClientSide(entity.lootableCorpse.privateData.container, connection);
                if (entity.mannequin != null)
                    ToClientSide(entity.mannequin, connection);
                if (entity.mannequin != null)
                    ToClientSide(entity.mannequin, connection);

                if (entity.miningQuarry?.extractor is ProtoBuf.ResourceExtractor extractor)
                {
                    ToClientSide(extractor.fuelContents, connection);
                    ToClientSide(extractor.outputContents, connection);
                }

                if (entity.vendingMachine is ProtoBuf.VendingMachine vendingMachine)
                {
                    if (vendingMachine.sellOrderContainer != null)
                    {
                        vendingMachine.sellOrderContainer = vendingMachine.sellOrderContainer.Copy();
                        foreach (ProtoBuf.VendingMachine.SellOrder order in vendingMachine.sellOrderContainer.sellOrders)
                            ToClientSide(order);
                    }
                }

                if (entity.horse is ProtoBuf.Horse horse)
                {
                    ToClientSide(horse.equipmentContainer, connection);
                    ToClientSide(horse.storageContainer, connection);
                }

                if (entity.FrankensteinTable is ProtoBuf.FrankensteinTable FrankensteinTable)
                {
                    for (int i = 0; i < FrankensteinTable.itemIds.Count; i++)
                    {
                        int itemId = FrankensteinTable.itemIds[i];
                        if (Item.ItemId(ref itemId))
                            FrankensteinTable.itemIds[i] = itemId;
                    }
                }

                if (entity.industrialConveyor is ProtoBuf.IndustrialConveyor industrialConveyor)
                {
                    foreach (ProtoBuf.IndustrialConveyor.ItemFilter filter in industrialConveyor.filters)
                        ToClientSide(filter, connection);
                }

                if (entity.weaponRack is ProtoBuf.WeaponRack weaponRack)
                {
                    foreach (ProtoBuf.WeaponRackItem item in weaponRack.items)
                        ToClientSide(item, connection);
                }


            }

            internal static MethodInfo GetMethod(Type type)
            {
                return AccessTools.Method(typeof(Mutate), nameof(ToClientSide), new Type[] { type, typeof(Connection) });
            }
        }

        public class Patcher
        {
            public static Patcher Instance { get; private set; }

            private RustPlugin plugin;
            private Harmony harmonyInstance;
            private HarmonyMethod protoWriteToStreamFinalizer = new HarmonyMethod(AccessTools.Method(typeof(ProtoWriteToStream), nameof(ProtoWriteToStream.LibraryFinalizer)));
            private HarmonyMethod protoWriteToStreamExternalsFinalizer = new HarmonyMethod(AccessTools.Method(typeof(ProtoWriteToStream), nameof(ProtoWriteToStream.LibraryFinalizer)));
            private AccessTools.FieldRef<PatchClassProcessor, Dictionary<Type, MethodInfo>> PatchClassProcessor__auxilaryMethods = AccessTools.FieldRefAccess<PatchClassProcessor, Dictionary<Type, MethodInfo>>("auxilaryMethods");

            public Patcher(RustPlugin plugin, Harmony harmonyInstance)
            {
                this.plugin = plugin;
                this.harmonyInstance = harmonyInstance;

                Instance = this;
            }

            public void PatchNestedClasses()
            {
                foreach (Type type in typeof(CustomItemDefinitions).GetNestedTypes(BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    object[] attribute = type.GetCustomAttributes(typeof(HarmonyPatch), false);
                    if (attribute.Length >= 1)
                    {
                        try
                        {
                            PatchClassProcessor patchClassProcessor = harmonyInstance.CreateClassProcessor(type);
                            patchClassProcessor.Patch();
                        }
                        catch (Exception ex)
                        {
                            this.plugin.RaiseError(ex.ToString());
                        }
                    }
                }
            }

            public void PatchWriteToStreamClass()
            {
                Type protoType = typeof(IProto);
                MethodInfo PatchWriteToStreamMethod = typeof(Patcher).GetMethod(nameof(PatchWriteToStream), BindingFlags.Public | BindingFlags.Instance);

                foreach (MethodInfo method in typeof(ProtoWriteToStream).GetMethods(BindingFlags.NonPublic | BindingFlags.Static))
                {
                    if (method.Name != ProtoWriteToStream.HANDLE_METHOD_NAME)
                        continue;

                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length < 1)
                        continue;

                    ParameterInfo firstParam = parameters[0];
                    if (!firstParam.ParameterType.GetInterfaces().Contains(protoType))
                        continue;

                    PatchWriteToStreamMethod.MakeGenericMethod(firstParam.ParameterType).Invoke(this, new object[] { method });
                }
            }

            public void PatchWriteToStream<T>(MethodInfo prefix) where T : class, IProto, new()
            {
                PatchProcessor processor = harmonyInstance.CreateProcessor(GetWriteToStreamMethod<T>());
                if (prefix != null)
                    processor.AddPrefix(new HarmonyMethod(prefix));
                processor.AddFinalizer(protoWriteToStreamFinalizer);
                processor.Patch();
            }


            public List<MethodInfo> PatchMutateMethod(MethodInfo targetMethod, Type patchClass, Harmony harmonyInstance)
            {
                if (targetMethod.DeclaringType != typeof(Mutate))
                    throw new ArgumentException();

                List<MethodInfo> list = new List<MethodInfo>();

                try
                {
                    PatchClassProcessor patchClassProcessor = new PatchClassProcessor(harmonyInstance, patchClass);
                    Dictionary<Type, MethodInfo> auxilaryMethods = PatchClassProcessor__auxilaryMethods(patchClassProcessor);
                    auxilaryMethods[typeof(HarmonyTargetMethod)] = HarmonyTargetMethod();
                    auxilaryMethods.Remove(typeof(HarmonyTargetMethods));
                    list.AddRange(patchClassProcessor.Patch());
                }
                catch (Exception ex)
                {
                    throw ex;
                }
                finally
                {
                    PatchProcessor patchProcessor = new PatchProcessor(harmonyInstance, targetMethod);
                    patchProcessor.AddFinalizer(new HarmonyMethod(typeof(ProtoWriteToStream), nameof(ProtoWriteToStream.ExternalFinalizer)));
                    list.Add(patchProcessor.Patch());
                }

                return list;

                MethodInfo HarmonyTargetMethod()
                {
                    DynamicMethod method = new DynamicMethod(
                         Guid.NewGuid().ToString(),
                         typeof(MethodBase),
                         Type.EmptyTypes,
                         patchClass.Module,
                         skipVisibility: true
                     );

                    ILGenerator il = method.GetILGenerator();

                    il.Emit(OpCodes.Ldtoken, targetMethod);
                    il.Emit(OpCodes.Call, typeof(MethodBase).GetMethod(nameof(MethodBase.GetMethodFromHandle), new[] { typeof(RuntimeMethodHandle) }));
                    il.Emit(OpCodes.Ret);

                    return method.CreateDelegate(typeof(Func<MethodBase>)).Method;
                }

            }

            private MethodInfo GetWriteToStreamMethod<T>() where T : class, IProto, new()
            {
                return typeof(T).GetMethod(nameof(IProto.WriteToStream), BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            }
        }
        #endregion

        #region Patches

        [HarmonyPatch(typeof(Translate))]
        private static class Translate_Patches
        {
            [HarmonyPatch(nameof(Translate.Init))]
            [HarmonyPostfix]
            private static void InitPostfix()
            {
                PluginInstance.CallHook("OnTranslateInitialized");
            }

            [HarmonyPatch(nameof(Translate.Get))]
            [HarmonyTranspiler]
            private static IEnumerable<CodeInstruction> FixGettingWithNonInitializedDictionary(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                CodeMatcher codeMatcher = new CodeMatcher(instructions, generator);
                codeMatcher.Advance(2);
                codeMatcher.CreateLabelWithOffsets(0, out Label ldarg_0);
                codeMatcher.CreateLabelWithOffsets(3, out Label brfalse_s);

                codeMatcher.Insert(
                        new CodeInstruction(OpCodes.Dup),
                        new CodeInstruction(OpCodes.Brtrue_S, ldarg_0),
                        new CodeInstruction(OpCodes.Pop),
                        new CodeInstruction(OpCodes.Br_S, brfalse_s)
                    );

                return codeMatcher.Instructions();
            }
        }

        [HarmonyPatch(typeof(NetWrite))]
        private static class NetWritePool
        {
            public static Dictionary<BufferStream, NetWrite> BufferToStream { get; } = new();

            [HarmonyPatch(nameof(Pool.IPooled.EnterPool))]
            [HarmonyPrefix]
            private static void EnterPool(NetWrite __instance)
            {
                lock (BufferToStream)
                    BufferToStream.Remove(NetWriteGetBufferStream(__instance));
            }


            [HarmonyPatch(nameof(NetWrite.Start))]
            [HarmonyPostfix]
            private static void Start(NetWrite __instance)
            {
                lock (BufferToStream)
                    BufferToStream[NetWriteGetBufferStream(__instance)] = __instance;
            }

            [HarmonyPatch(nameof(NetWrite.Send))]
            [HarmonyPrefix]
            private static void Send(NetWrite __instance)
            {
                __instance.connections.Clear();
            }

            [HarmonyPatch(nameof(Pool.IPooled.EnterPool))]
            [HarmonyPatch(nameof(NetWrite.Start))]
            [HarmonyPatch(nameof(NetWrite.Send))]
            [HarmonyFinalizer]
            static Exception Finalizer()
            {
                return null;
            }
        }


        [HarmonyPatch(typeof(BaseEntity))]
        private static class ClientRPCPatches
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                Type protoType = typeof(IProto);
                foreach (MethodInfo methodInfo in typeof(BaseEntity).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    ParameterInfo[] parameters = methodInfo.GetParameters();
                    if (methodInfo.Name == nameof(BaseEntity.ClientRPC) && parameters.Length == 2 && parameters[1].ParameterType.GetInterfaces().Contains(protoType))
                        yield return methodInfo;
                }
            }

            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator gen)
            {
                CodeMatcher matcher = new CodeMatcher(instructions)
                    .MatchStartForward(CodeMatch.Calls(AccessTools.Method("BaseEntity:ClientRPCStart")))
                    .ThrowIfInvalid("Could not find call to BaseEntity:ClientRPCStart")
                    .InsertAndAdvance(
                        CodeMatch.LoadArgument(1)
                    )
                    .SetInstruction(CodeMatch.Call(typeof(ClientRPCStart), nameof(ClientRPCStart.Call)));
                return matcher.Instructions();
            }

        }

        [HarmonyPatch]
        private class ClientRPCStart
        {
            [HarmonyReversePatch]
            [HarmonyPatch(typeof(BaseEntity), "ClientRPCStart", typeof(string))]
            public static NetWrite Call(BaseEntity entity, string funcName, RpcTarget target)
            {
                IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
                {
                    CodeMatcher codeMatcher = new CodeMatcher(instructions, generator)
                        .End()
                        .Insert(
                            new CodeInstruction(OpCodes.Dup),
                            CodeMatch.LoadArgument(2),
                            CodeMatch.Call(typeof(ClientRPCStart), $"<{nameof(Call)}>g__{nameof(__)}|0_1")
                         );

                    return codeMatcher.Instructions();
                }
                _ = Transpiler(null, null);
                return default;

                void __(NetWrite netWrite, RpcTarget target)
                {
                    SendInfo info = target.Connections;
                    if (info.connections != null)
                        netWrite.connections.AddRange(info.connections);
                    if (info.connection != null)
                        netWrite.connections.Add(info.connection);
                }
            }
        }

        [HarmonyPatch(typeof(ConsoleNetwork), nameof(ConsoleNetwork.SendClientCommand), new Type[] { typeof(Connection), typeof(string), typeof(string[]) })]
        private static class ConsoleNetwork_NoteInv_Patch
        {
            private static bool Prefix(Connection __0, string __1, ref object[] __2)
            {
                if (__1 == "note.inv")
                    Mutate.NoteInvToClientSide(ref __2, __0);

                return true;
            }
        }


#if CARBON
        [HarmonyPatch(typeof(Carbon.Core.ModLoader), nameof(Carbon.Core.ModLoader.UninitializePlugin))]
#else
        [HarmonyPatch(typeof(Oxide.Core.OxideMod), nameof(Oxide.Core.OxideMod.UnloadPlugin))]
#endif
        private static class UnloadingPatch
        {
#if CARBON
                    private static void Prefix(RustPlugin plugin)
                    {
                        if (CustomDefinitions.ContainsKey(plugin))
                            UnloadPluginItemDefinitions(plugin);
                    }
#else
            private static void Prefix(string name)
            {
                Plugin plugin = Oxide.Core.Interface.Oxide.RootPluginManager.GetPlugin(name);
                if (plugin == null || (plugin.IsCorePlugin && !Oxide.Core.Interface.Oxide.IsShuttingDown))
                    return;
                if (CustomDefinitions.ContainsKey(plugin))
                    UnloadPluginItemDefinitions(plugin);
            }
#endif
        }


        [HarmonyPatch(typeof(global::Item), nameof(global::Item.Load))]
        private static class Item_Load_Patch
        {
            private static void Prefix(ProtoBuf.Item __0)
            {
                bool IsFallback() => __0.itemid == FallbackItemDefinition.itemid && __0.text != null;
                if (IsFallback() && int.TryParse(__0.text, out int restoreItemId) && ItemManager.itemDictionary.ContainsKey(restoreItemId))
                    __0.itemid = restoreItemId;
            }

            private static void Postfix(Item __instance, ProtoBuf.Item __0)
            {
                if (__instance.info == null)
                    OnItemDefinitionBroken(__instance, __0);
            }
        }


        [HarmonyPatch(typeof(CuiImageComponent))]
        private static class CuiImageComponentPatches
        {
            private static AccessTools.FieldRef<CuiImageComponent, ulong> SkinIdRef = AccessTools.FieldRefAccess<CuiImageComponent, ulong>("<SkinId>k__BackingField");

            [HarmonyPostfix]
            [HarmonyPatch(nameof(CuiImageComponent.ItemId), MethodType.Getter)]
            private static void Getter(CuiImageComponent __instance, ref int __result)
            {
                Mutate.Item.SkinId(__result, ref SkinIdRef(__instance));
                Mutate.Item.ItemId(ref __result);
            }
        }

#if OXIDE
        [HarmonyPatch(typeof(Lang), "MergeMessages")]
        private static class Lang_MergeMessages_Patch
        {
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator gen)
            {
                CodeMatcher codeMatcher = new CodeMatcher(instructions, gen);
                codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Endfinally)).ThrowIfInvalid("Error #OXIDE001");
                codeMatcher.RemoveInstructionsInRange(codeMatcher.Pos + 1, codeMatcher.Length - 3);

                codeMatcher.CreateLabelWithOffsets(1, out Label @return);

                codeMatcher.Start();
                codeMatcher.MatchStartForward(new CodeMatch(OpCodes.Leave_S)).ThrowIfInvalid("Error #OXIDE002");
                codeMatcher.Operand = @return;

                return codeMatcher.Instructions();
            }
        }
#endif
        #endregion

        #region Methods
        #region API
        private object Register(object @object, RustPlugin plugin)
        {
            if (@object is IEnumerable enumerable)
            {
                List<CustomItemDefinition> list = new List<CustomItemDefinition>();
                foreach (object item in enumerable)
                    list.Add(CustomItemDefinition.FromObject(item));

                return RegisterPluginItemDefinitions(list, plugin);
            }
            else
            {
                return RegisterPluginItemDefinition(CustomItemDefinition.FromObject(@object), plugin);
            }
        }

        private List<MethodInfo> PatchMutateMethod(Type argumentType, Type patchClass, Harmony harmonyInstance)
        {
            MethodInfo targetMethod = Mutate.GetMethod(argumentType);
            if (targetMethod == null)
                throw new Exception("Failed to patch the mutation method because no method with the specified type was found");

            return Patcher.Instance.PatchMutateMethod(targetMethod, patchClass, harmonyInstance);
        }

        private ulong GetSkin(ItemDefinition definition)
        {
            return GetSkin(definition.itemid);
        }

        private ulong GetSkin(int itemId)
        {
            ulong skinId = 0UL;
            if (IsCustomDefinition(itemId))
                Mutate.Item.SkinId(itemId, ref skinId);

            return skinId;
        }

        private string GetTranslatedDisplayName(ItemDefinition customDefinition, ulong userID)
        {
            return GetTranslatedDisplayName(customDefinition, PlayerLanguages.GetPlayerLanguage(userID));
        }
        private string GetTranslatedDisplayName(ItemDefinition customDefinition, string languageOrUserID)
        {
            if (string.IsNullOrEmpty(languageOrUserID))
                return null;

            if (!IsValidCID(customDefinition))
                return null;

            if (languageOrUserID.IsSteamId() && ulong.TryParse(languageOrUserID, out ulong userID))
                return GetTranslatedDisplayName(customDefinition, userID);

            string language = languageOrUserID;
            if (customDefinition.displayName.IsValid())
            {
                if (Mutate.Item.Dictionaries.NamePhrase.TryGetValue(customDefinition.itemid, out Translate.Phrase phrase))
                    return GetMessageByLanguage(phrase.token, pluginByPhrase[phrase], language);
            }
            return customDefinition.displayName.english;
        }

        private bool IsCustomDefinition(ItemDefinition definition) => IsCustomDefinition(definition.itemid);

        private bool IsCustomDefinition(int itemId) => Mutate.Item.Dictionaries.ItemId.ContainsKey(itemId);
        #endregion

        #region GetMessageByLanguage
        public string GetMessageByLanguage(string token, string language)
        {
            return lang.GetMessageByLanguage(token, this, language);
        }

        public static string GetMessageByLanguage(string token, RustPlugin plugin = null, string language = "en")
        {
            plugin ??= PluginInstance;
            return PluginGetLang(plugin).GetMessageByLanguage(token, plugin, language);
        }
        #endregion

        #region Registration
        public static HashSet<ItemDefinition> RegisterPluginItemDefinitions(IEnumerable<CustomItemDefinition> definitions, RustPlugin plugin)
        {
            HashSet<ItemDefinition> hashSet = new HashSet<ItemDefinition>();
            foreach (var definition in definitions)
            {
                ItemDefinition resultItemDefinition = RegisterPluginItemDefinition(definition, plugin);
                if (resultItemDefinition != null)
                    hashSet.Add(resultItemDefinition);
            };
            return hashSet;
        }

        public static ItemDefinition RegisterPluginItemDefinition(CustomItemDefinition definition, RustPlugin plugin)
        {
            definition.FillEmptyValues();
            definition.Validate(plugin);

            int itemId = definition.itemId.Value;
            ItemDefinition parent = definition.ParentDefinition;

            ItemDefinition @new = GetOrCloneDefinition(itemId, parent);
            if (@new == null)
                throw new Exception(string.Format("Error by the plugin \"{0}\": Failure to create or search for a new item definition ({1})!", plugin.Name, definition.shortname));

            SettingDefinitionFields(@new, definition);
            SetNameAndDescription(@new, definition, plugin);
            SaveDefaultSkinId(@new, definition);

            ClearItemMods(@new);
            MakeNewMods(definition, @new);

            CreateBlueprint(definition, parent, @new);
            SettingStaticOwnerships(definition, plugin, @new);

            AddDefinitionToManager(@new);

            GetPluginHashSet(plugin).Add(definition);
            return @new;

            static void CreateBlueprint(CustomItemDefinition definition, ItemDefinition parent, ItemDefinition @new)
            {
                if (definition.craftable || definition.repairable)
                {
                    if (!@new.TryGetComponent(out ItemBlueprint blueprint))
                        blueprint = @new.gameObject.AddComponent<ItemBlueprint>();

                    List<ItemAmount> ingredients;
                    if (definition.blueprintIngredients != null && definition.blueprintIngredients.Count > 0)
                        ingredients = definition.blueprintIngredients;
                    else
                        ingredients = parent.Blueprint?.ingredients ?? new List<ItemAmount>();

                    blueprint.defaultBlueprint = false;
                    blueprint.userCraftable = definition.craftable;
                    blueprint.isResearchable = definition.craftable || definition.repairable;
                    blueprint.workbenchLevelRequired = definition.workbenchLevelRequired;
                    blueprint.ingredients = ingredients;
                }
            }

            static void SavePluginPhrase(Translate.Phrase phrase, RustPlugin plugin)
            {
                pluginByPhrase[phrase] = plugin;
            }

            static void SetNameAndDescription(ItemDefinition @new, CustomItemDefinition definition, RustPlugin plugin)
            {
                if (definition.defaultName != null || definition.defaultDescription != null)
                {
                    if (definition.defaultName != null)
                    {
                        SetValueWithCache(definition.defaultName, ref @new.displayName, @new.itemid, Mutate.Item.Dictionaries.NamePhrase);
                        SavePluginPhrase(@new.displayName, plugin);
                    }

                    if (definition.defaultDescription != null)
                    {
                        SetValueWithCache(definition.defaultDescription, ref @new.displayDescription, @new.itemid, Mutate.Item.Dictionaries.DescriptionPhrase);
                        SavePluginPhrase(@new.displayDescription, plugin);
                    }

                    const string lang = "en";

                    Lang pluginLang = PluginGetLang(plugin);
                    Dictionary<string, string> langDictionary = Pool.Get<Dictionary<string, string>>();

                    Dictionary<string, string> existingMessages = pluginLang.GetMessages(lang, plugin);
                    if (existingMessages != null)
                    {
                        foreach (KeyValuePair<string, string> pair in existingMessages)
                            langDictionary.Add(pair.Key, pair.Value);
                    }

                    bool nameHasBeenAdded = langDictionary.TryAdd(@new.displayName.token, @new.displayName.english);
                    bool descriptionHasBeenAdded = langDictionary.TryAdd(@new.displayDescription.token, @new.displayDescription.english);
                    if (nameHasBeenAdded || descriptionHasBeenAdded)
                    {
                        pluginLang.RegisterMessages(langDictionary, plugin);
                    }

                    Pool.FreeUnmanaged(ref langDictionary);
                }
            }

            static void SettingDefinitionFields(ItemDefinition @new, CustomItemDefinition definition)
            {
                @new.itemid = definition.itemId.Value;
                @new.shortname = definition.shortname;
                @new.Parent = definition.ParentDefinition;
                @new.flags = CUSTOM_DEFINITION_FLAG;
                @new.flags |= definition.flags;
                if (definition.category.HasValue)
                    @new.category = definition.category.Value;
                if (definition.maxStackSize.HasValue)
                    @new.stackable = definition.maxStackSize.Value;
                @new.condition.repairable = definition.repairable;
                Mutate.Item.Dictionaries.ItemId[@new.itemid] = @new.Parent.itemid;
            }

            static void MakeNewMods(CustomItemDefinition definition, ItemDefinition @new)
            {
                if (definition.itemMods != null)
                {
                    foreach (ItemMod mod in definition.itemMods)
                    {
                        if ((mod as object) == null)
                            continue;

                        Type type = mod.GetType();
                        Component component = @new.gameObject.AddComponent(type);
                        mod.CopyFields(component as ItemMod);
                    }
                }
                @new.Initialize(ItemManager.itemList);
            }

            static void SaveDefaultSkinId(ItemDefinition @new, CustomItemDefinition definition)
            {
                if (definition.defaultSkinId != default)
                    Mutate.Item.Dictionaries.SkinId[@new.itemid] = definition.defaultSkinId;
            }

            static void SettingStaticOwnerships(CustomItemDefinition definition, RustPlugin plugin, ItemDefinition @new)
            {
                if (definition.staticOwnerships != null)
                {
                    foreach (var ownership in definition.staticOwnerships)
                    {
                        if (ownership.label.IsValid())
                            SavePluginPhrase(ownership.label, plugin);
                        if (ownership.text.IsValid())
                            SavePluginPhrase(ownership.text, plugin);
                    }
                    Mutate.Item.Dictionaries.StaticOwnerships[@new.itemid] = definition.staticOwnerships;
                }
            }
        }
        #endregion

        #region Common
        private static void CacheAlreadyCreated()
        {
            if (ExistingDefinitions == null)
            {
                ExistingDefinitions = new Dictionary<int, ItemDefinition>();
                if (ServerMgr.Instance != null)
                {
                    foreach (GameObject gameObject in ServerMgr.Instance.gameObject.scene.GetRootGameObjects())
                    {
                        if (!regexNumeric.IsMatch(gameObject.name))
                            continue;

                        if (gameObject.TryGetComponent(out ItemDefinition itemDefinition))
                            ExistingDefinitions[itemDefinition.itemid] = itemDefinition;
                    }
                }
            }
        }

        public static bool IsValidCID(ItemDefinition definition)
        {
            return definition != null && PluginInstance.IsCustomDefinition(definition) && definition.HasFlag(CUSTOM_DEFINITION_FLAG) && definition.Parent != null;
        }

        private static void SetValueWithCache<TKey, TValue>(TValue value, ref TValue target, TKey key, Dictionary<TKey, TValue> dictionary)
        {
            target = value;
            dictionary[key] = value;
        }

        private static void ClearItemMods(ItemDefinition @new)
        {
            @new.itemMods = null;
            foreach (ItemMod mod in @new.GetComponentsInChildren<ItemMod>(true))
                UnityEngine.Object.DestroyImmediate(mod);
        }

        private static bool ContainsDefinitionInHashSet(CustomItemDefinition definition, RustPlugin plugin)
        {
            if (!CustomDefinitions.TryGetValue(plugin, out HashSet<CustomItemDefinition> hashSet))
                return false;

            return hashSet.Contains(definition);
        }

        private static HashSet<CustomItemDefinition> GetPluginHashSet(RustPlugin plugin)
        {
            if (!CustomDefinitions.TryGetValue(plugin, out HashSet<CustomItemDefinition> hashSet))
                hashSet = (CustomDefinitions[plugin] = new HashSet<CustomItemDefinition>());
            return hashSet;
        }

        private static ItemDefinition GetOrCloneDefinition(int itemId, ItemDefinition template)
        {
            if (!ExistingDefinitions.TryGetValue(itemId, out ItemDefinition result))
            {
                result = CloneItemDefinition(template);
                result.name = itemId.ToString();
                ExistingDefinitions[itemId] = result;
            }
            return result;
        }
        #endregion

        #region ItemManager
        private static void AddDefinitionToManager(ItemDefinition definition)
        {
            ItemManager.itemDictionary[definition.itemid] = definition;
            ItemManager.itemDictionaryByName[definition.shortname] = definition;
            if (!ItemManager.itemList.Contains(definition))
                ItemManager.itemList.Add(definition);
        }

        private static void RemoveDefinitionFromManager(ItemDefinition definition)
        {
            ItemManager.itemDictionary.Remove(definition.itemid);
            ItemManager.itemDictionaryByName.Remove(definition.shortname);
            ItemManager.itemList.Remove(definition);
        }
        #endregion

        #region Unload
        private static void UnloadPluginItemDefinitions(Plugin plugin)
        {
            if (CustomDefinitions.TryGetValue(plugin, out HashSet<CustomItemDefinition> definitions))
            {
                foreach (var definition in definitions)
                    UnloadCustomItemDefinition(definition);
                definitions.Clear();
                CustomDefinitions.Remove(plugin);
            }
        }

        private static void UnloadPluginItemDefinition(Plugin plugin, CustomItemDefinition definition)
        {
            if (CustomDefinitions.TryGetValue(plugin, out HashSet<CustomItemDefinition> definitions))
            {
                if (definitions.Contains(definition))
                {
                    UnloadCustomItemDefinition(definition);
                    definitions.Remove(definition);
                }
            }
        }

        private static void UnloadCustomItemDefinition(CustomItemDefinition definition)
        {
            if (ItemManager.itemDictionaryByName.TryGetValue(definition.shortname, out ItemDefinition itemDefinition))
            {
                int itemId = itemDefinition.itemid;
                Mutate.Item.Dictionaries.Remove(itemId);

                ClearItemMods(itemDefinition);
                itemDefinition.Initialize(ItemManager.itemList);

                RemoveDefinitionFromManager(itemDefinition);
            }
            Pool.Free(ref definition);
        }
        #endregion

        #region CloneItemDefinition
        private static ItemDefinition CloneItemDefinition(ItemDefinition itemDefinition)
        {
            if (itemDefinition == null)
                return null;
            GameObject clone = UnityEngine.Object.Instantiate(itemDefinition.gameObject);
            UnityEngine.Object.DontDestroyOnLoad(clone);
            return clone.GetComponent<ItemDefinition>();
        }

        private static ItemDefinition CloneItemDefinition(string shortname) => CloneItemDefinition(ItemManager.FindItemDefinition(shortname));
        private static ItemDefinition CloneItemDefinition(int itemId) => CloneItemDefinition(ItemManager.FindItemDefinition(itemId));
        #endregion

        #region Blueprint Bypasses
        private static void SendSnapshotWithUnlockedBlueprint(BasePlayer player, int itemId)
        {
            if (player == null || player.net == null || !player.IsConnected)
                return;

            Network.Connection connection = player.net.connection;
            try
            {
                NetWrite netWrite = Network.Net.sv.StartWrite();
                global::BaseNetworkable.SaveInfo saveInfo = new global::BaseNetworkable.SaveInfo
                {
                    forConnection = player.net.connection,
                    forDisk = false
                };
                netWrite.PacketID(Message.Type.Entities);
                netWrite.UInt32(connection.validate.entityUpdates + 1U);
                using (saveInfo.msg = Facepunch.Pool.Get<ProtoBuf.Entity>())
                {
                    player.Save(saveInfo);
                    ProtoBuf.PersistantPlayer persistantData = saveInfo.msg.basePlayer.persistantData;
                    if (persistantData != null && persistantData.unlockedItems != null && !persistantData.unlockedItems.Contains(itemId))
                        persistantData.unlockedItems.Add(itemId);
                    saveInfo.msg.WriteToStream(netWrite);
                }
                netWrite.Send(new SendInfo(player.net.connection));
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogException(ex);
            }
            finally
            {
                connection.validate.entityUpdates++;
            }
        }

        private static void SendFakeUnlockedBlueprint(BasePlayer player, int itemId)
        {
            SendSnapshotWithUnlockedBlueprint(player, itemId);
            player.ClientRPC<int>(RpcTarget.Player("UnlockedBlueprint", player), 0); // [Clientside RPC] Update blueprints
        }
        #endregion
        #endregion

        #region Obsolate
        [Obsolete]
        private static bool IsValidCustomItemDefinition(ItemDefinition definition) => IsValidCID(definition);
        #endregion
    }
}
#endregion

#region Extensions
namespace Oxide.Plugins.CustomItemDefinitionExtensions
{
    public static class ItemModExtensions
    {
        public static void CopyFields(this ItemMod from, ItemMod to)
        {
            if ((from as object) == null || (to as object) == null)
                return;

            Type type = from.GetType();
            do
            {
                foreach (FieldInfo field in from.GetType().GetFields(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.SetField))
                    field.SetValue(to, field.GetValue(from));

                if (type == typeof(ItemMod))
                    break;

                type = type.BaseType;
            }
            while (type != null);
        }
    }
}
#endregion 