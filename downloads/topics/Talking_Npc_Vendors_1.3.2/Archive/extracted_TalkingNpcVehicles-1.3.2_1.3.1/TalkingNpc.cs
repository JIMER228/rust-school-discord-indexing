using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;
using VLB;

namespace Oxide.Plugins
{
    [Info("TalkingNpc", "Razor/S0N_0F_BISCUIT", "1.3.1")]
    [Description("Adds talking npc to your server")]
    public class TalkingNpc : RustPlugin
    {
        [PluginReference]
        private Plugin CustomVendingSetup, Economics, ServerRewards, SpawnModularCar, Kits, MarkerManager;

        public static TalkingNpc Instance;

        TalkerSpawns TalkerSpawnsData;
        PlayerCooldowns PlayerCooldownsData;
        GlobalCooldowns GlobalCooldownsData;
        DynamicConfigFile TalkerSpawnsFile;
        DynamicConfigFile PlayerCooldownsFile;
        DynamicConfigFile GlobalCooldownsFile;


        Dictionary<ulong, ConversationHandler> PlayerConversations = new Dictionary<ulong, ConversationHandler>();
        Dictionary<InvisibleVendingMachine, string> VendingMachineConfigurations = new Dictionary<InvisibleVendingMachine, string>();
        Dictionary<string, Dictionary<string, object>> VendingMachineDataProviders = new Dictionary<string, Dictionary<string, object>>();

        #region Class Definitions

        static class Commands
        {
            public const string UiAction = "TalkingNpcUIHandler";
            public class UiActions
            {
                public const string OnResponseClicked = "OnResponseClicked";
            }

            public const string ManageNpc = "talking_npc";
            public static class ManageArguments
            {
                public const string Add = "add";
                public const string Monument = "monument";
                public const string Remove = "remove";
            }
            public static class ManageArgumentsHelp
            {
                public const string Add = "<color=#FFFF00>/talking_npc add <Name> <ConversationFile> <true/false></color> - Adds NPC.\n";
                public const string Remove = "<color=#FFFF00>/talking_npc remove <Name> </color> - Removes NPC.\n";
            }

            public const string SpawnVehicle = "talking_npc vehicle spawn";
        }

        static class TalkingNpcHooks
        {
            public const string OnCommand = "OnTalkingNpcCommand";
        }

        static class Permissions
        {
            public const string Admin = "talkingnpc.admin";
        }

        #region User Interface
        public static class Ui
        {
            public static class Anchors
            {
#pragma warning disable IDE0051 // Remove unused private members
                public const string LowerLeft = "0 0";
                public const string UpperLeft = "0 1";
                public const string Center = "0.5 0.5";
                public const string LowerRight = "1 0";
                public const string UpperRight = "1 1";
#pragma warning restore IDE0051 // Remove unused private members
            }

            public static class Colors
            {
                public const string Black = "0 0 0 1";
                public const string Blanc = "0.851 0.820 0.776 1";
                public const string Cloudy = "0.678 0.647 0.620 1";
                public const string DarkGray = "0.12 0.12 0.12 1";
                public const string DarkGreen = "0.145 0.255 0.09 1";
                public const string DarkRed = "0.8 0 0 1";
                public const string DimGray = "0.33 0.33 0.33 1";
                public const string Karaka = "0.16 0.16 0.13 1";
                public const string LightGreen = "0.365 0.447 0.220 1";
                public const string White = "1 1 1 1";

                public static class Transparent
                {
                    public const string Black90 = "0 0 0 0.90";
                    public const string DimGray90 = "0.33 0.33 0.33 0.90";
                    public const string Clear = "0 0 0 0";
                }
            }

            public static class Overlay
            {
                public const string Panel = "Overlay";

                public static class TalkScreen
                {
                    public const string Panel = "TalkingNpc.TalkScreen";
                    public const string TopLetterbox = "TalkingNpc.TalkScreen.TopLetterBox";
                    public const string BottomLetterbox = "TalkingNpc.TalkScreen.BottomLetterBox";

                    public static class Dialog
                    {
                        public const string Panel = "TalkingNpc.TalkScreen.Dialog";
                        public const string CloseButton = "TalkingNpc.TalkScreen.Dialog.CloseButton";

                        public static class Nametag
                        {
                            public const string Panel = "TalkingNpc.TalkScreen.Dialog.Nametag";
                            public const string Text = "TalkingNpc.TalkScreen.Dialog.Nametag.Text";
                        }

                        public static class Message
                        {
                            public const string Panel = "TalkingNpc.TalkScreen.Dialog.Message";
                            public const string Text = "TalkingNpc.TalkScreen.Dialog.Message.Text";
                        }

                        public static class Responses
                        {
                            public const string Panel = "TalkingNpc.TalkScreen.Dialog.Responses";

                            public static class Response
                            {
                                public const string Panel = "TalkingNpc.TalkScreen.Dialog.Response";
                                public const string Text = "TalkingNpc.TalkScreen.Dialog.Response.Text";
                                public const string Button = "TalkingNpc.TalkScreen.Dialog.Response.Button";

                                public static class Number
                                {
                                    public const string Panel = "TalkingNpc.TalkScreen.Dialog.Response.Number";
                                    public const string Text = "TalkingNpc.TalkScreen.Dialog.Response.Number.Text";
                                }
                            }
                        }
                    }
                }
            }

            public class OnResponseClickedCommand
            {
                public string Conversation { get; }
                public uint MessageId { get; }
                public int ResponseIndex { get; }

                public OnResponseClickedCommand(string conversation, uint messageId, int responseIndex)
                {
                    Conversation = conversation;
                    MessageId = messageId;
                    ResponseIndex = responseIndex;
                }

                public override string ToString() => $"{Commands.UiAction} {Commands.UiActions.OnResponseClicked} {Conversation} {MessageId} {ResponseIndex}";
            }
        }

        #endregion

        #region Configuration
        [JsonObject(MemberSerialization.OptIn)]
        class ConfigData
        {
            [JsonProperty(PropertyName = "Settings")]
            public Settings settings { get; set; } = new Settings();

            public class Settings
            {
                [JsonProperty("Admin Permission")]
                public string PermissionUse { get; set; } = Permissions.Admin;

                [JsonProperty("Auto-Close Dialog Distance")]
                public float AutoCloseDialogDistance { get; set; } = 2.6f;
            }

            [JsonProperty(PropertyName = "Version")]
            public VersionNumber Version { get; set; } = Instance.Version;

            public VersionNumber LastBreakingChange { get; private set; } = new VersionNumber(1, 1, 2);
        }
        #endregion

        #region Data

        #region Conversations
        class Currency
        {
            [JsonProperty("Item ID")]
            public int ItemId { get; set; } = -932201673;

            [JsonProperty("Skin ID")]
            public ulong SkinId { get; set; } = 0;
        }

        class Response
        {
            [JsonProperty("Message")]
            public string Message { get; set; }

            [JsonProperty("Needs Permission (null = No)")]
            public string Permission { get; set; }

            [JsonProperty("Player Commands")]
            public List<string> PlayerCommands { get; set; } = new List<string>();

            [JsonProperty("Server Commands")]
            public List<string> ServerCommands { get; set; } = new List<string>();

            [JsonProperty("Next Message (null = Close UI)")]
            public uint? NextMessage { get; set; } = null;

            [JsonProperty("Price")]
            public int Price { get; set; } = 0;

            [JsonProperty("Currency")]
            public Currency Currency { get; set; } = new Currency();

            [JsonProperty("Insufficient Funds Message (null = Close UI)")]
            public uint? InsufficientFundsMessage { get; set; } = null;

            [JsonProperty("Cooldown")]
            public int Cooldown { get; set; } = 0;

            [JsonProperty("Server Wide Cooldown")]
            public bool gCooldown { get; set; } = false;
        }

        class Dialogue
        {
            [JsonProperty("Message")]
            public string Message { get; set; }

            [JsonProperty("Message Display Time (seconds) - Only used if there are no responses")]
            public float MessageDisplayTime { get; set; } = 2f;

            [JsonProperty("Responses")]
            public List<Response> Responses { get; set; } = new List<Response>();
        }

        class ConversationTree : Dictionary<uint, Dialogue> { }

        Dictionary<string, ConversationTree> Conversations = new Dictionary<string, ConversationTree>();

        class ConversationHandler : FacepunchBehaviour
        {
            public NPCTalking NpcTalker { get; private set; } = null;
            public Dictionary<string, InvisibleVendingMachine> VendingMachines { get; private set; } = new Dictionary<string, InvisibleVendingMachine>();

            Dictionary<ulong, BasePlayer> ActiveConversations = new Dictionary<ulong, BasePlayer>();

            private void Awake()
            {
                NpcTalker = GetComponent<NPCTalking>();
            }

            private void OnDestroy()
            {
                foreach (var player in ActiveConversations.Values)
                    EndConversation(player);

                foreach (var vendingMachine in VendingMachines)
                {
                    Instance.VendingMachineConfigurations.Remove(vendingMachine.Value);
                    if (!vendingMachine.Value.IsDestroyed)
                        vendingMachine.Value.Kill();
                }
                VendingMachines.Clear();
            }

            private void CheckPlayersWalkAway()
            {
                foreach (var player in ActiveConversations.Values)
                {
                    if (Vector3.Distance(NpcTalker.transform.position, player.transform.position) > Instance.configData.settings.AutoCloseDialogDistance ||
                        !player.IsConnected || player.IsSleeping())
                        EndConversation(player);
                }

                if (ActiveConversations.Count == 0)
                    CancelInvoke(CheckPlayersWalkAway);
            }

            public void AddVendingMachine(string name, InvisibleVendingMachine vendingMachine) => VendingMachines[name] = vendingMachine;

            public void StartConversation(BasePlayer player, bool walaAway = false)
            {
                if (player == null) return;

                ActiveConversations[player.userID] = player;
                Instance.PlayerConversations[player.userID] = this;

                if (!walaAway && !IsInvoking(CheckPlayersWalkAway))
                    InvokeRepeating(CheckPlayersWalkAway, 0.2f, 0.2f);
            }

            public void EndConversation(BasePlayer player, bool endConversation = true)
            {
                if (player == null) return;

                CuiHelper.DestroyUi(player, Ui.Overlay.TalkScreen.Panel);

                if (endConversation)
                {
                    ActiveConversations.Remove(player.userID);
                    Instance.PlayerConversations.Remove(player.userID);
                }
            }
        }
        #endregion

        #region Cooldowns
        class PlayerCooldowns : Dictionary<ulong, ConversationCooldowns> { }

        class GlobalCooldowns : Dictionary<ulong, ConversationCooldowns> { }

        class ConversationCooldowns : Dictionary<string, MessageCooldowns> { }

        class MessageCooldowns : Dictionary<uint, ResponseCooldowns> { }

        class ResponseCooldowns : Dictionary<int, DateTime> { }
        #endregion

        #region Talker Spawns
        public class TalkerSpawns : Dictionary<string, SavedTalker> { }

        [JsonObject(MemberSerialization.OptIn)]
        public class SavedTalker
        {
            [JsonProperty("Conversation File")]
            public string ConversationFile { get; set; } = "default";

            [JsonProperty("dataFile")]
            private string _ConversationFile { set { ConversationFile = value; } }

            [JsonProperty("Position")]
            public Vector3 Position { get; set; }

            [JsonProperty("position")]
            private Vector3 _Position { set { Position = value; } }

            [JsonProperty("Rotation")]
            public Vector3 Rotation { get; set; }

            [JsonProperty("rotation")]
            private Vector3 _Rotation { set { Rotation = value; } }

            [JsonProperty("Name")]
            public string Name { get; set; }

            [JsonProperty("name")]
            private string _Name { set { Name = value; } }

            [JsonProperty("Kit")]
            public string Kit { get; set; }

            [JsonProperty("NpcUserID")]
            public ulong NpcUserID { get; set; }

            [JsonProperty("CallOnUseNpc")]
            public bool CallOnUseNpc { get; set; }

            [JsonProperty("UseCommand on dialog open closes dialog")]
            public bool useCommandClose { get; set; }

            [JsonProperty("UseCommand on dialog open")]
            public List<string> useCommand { get; set; }

            [JsonProperty("Monument", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public string Monument { get; set; }

            [JsonProperty("monument")]
            private string _Monument { set { Monument = value; } }

            [JsonProperty("Vending Machine Configuration Files")]
            public HashSet<string> VendingMachines { get; set; } = new HashSet<string>();

            [JsonProperty("Umod Marker Manager")]
            public markerInfo UmodMarkersManager { get; set; } = new markerInfo();
           
            public HashSet<ulong> Instances { get; set; } = new HashSet<ulong>();
        }

        public class markerInfo
        {
            public bool enabled = false;
            public string displayName { get; set; } = "newNpc";
            public float radius { get; set; } = 0.4f;
            public string colorMarker { get; set; } = "00FFFF";
            public string colorOutline { get; set; } = "00FFFFFF";
        }
        #endregion

        #endregion

        #endregion

        #region Configuration Handling
        private ConfigData configData;

        protected override void LoadConfig()
        {
            Instance = this;

            base.LoadConfig();
            try
            {
                configData = Config.ReadObject<ConfigData>();
                if (configData == null)
                    LoadDefaultConfig();
                UpdateConfigVersion();
            }
            catch
            {
                PrintError("Your configuration file is invalid");
                UpdateConfig();
                return;
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig() => configData = new ConfigData();

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfig()
        {
            PrintWarning("Invalid config file detected! Backing up current and creating new config...");
            var outdatedConfig = Config.ReadObject<object>();
            Config.WriteObject(outdatedConfig, filename: $"{Name}.Backup");
            LoadDefaultConfig();
            PrintWarning("Config update completed!");
        }

        void UpdateConfigVersion() => configData.Version = Version;
        #endregion

        #region Data Handling
        bool DataFileExists(string path)
        {
            return Interface.Oxide.DataFileSystem.ExistsDatafile($"{Name}/{path}");
        }
        bool ConversationDataFileExists(string path) => DataFileExists($"Conversations/{path}");
        bool VendingMachineDataFileExists(string path) => DataFileExists($"VendingMachines/{path}");

        DynamicConfigFile GetDataFile(string path)
        {
            return Interface.Oxide.DataFileSystem.GetFile($"{Name}/{path}");
        }
        DynamicConfigFile GetConversationDataFile(string path) => GetDataFile($"Conversations/{path}");
        DynamicConfigFile GetVendingMachineDataFile(string path) => GetDataFile($"VendingMachines/{path}");

        bool TryLoadConversation(string filename, out ConversationTree data)
        {
            if (ConversationDataFileExists(filename))
            {
                var file = GetConversationDataFile(filename);
                try
                {
                    data = file.ReadObject<ConversationTree>();
                    return true;
                }
                catch (Exception ex)
                {
                    PrintError($"Error reading data from {file.Filename}: ${ex.Message}");
                }
            }
            data = new ConversationTree();
            return false;
        }

        bool TryLoadVendingMachine(string filename, out JObject data)
        {
            if (VendingMachineDataFileExists(filename))
            {
                var file = GetVendingMachineDataFile(filename);
                try
                {
                    data = file.ReadObject<JObject>();
                    return true;
                }
                catch (Exception ex)
                {
                    PrintError($"Error reading data from {file.Filename}: {ex.Message}");
                }
            }

            data = new JObject();
            return false;
        }

        void SaveConversation(string filename, ConversationTree data)
        {
            GetConversationDataFile(filename).WriteObject(data);
            Conversations[filename] = data;
        }

        void SaveVendingMachine(string filename, JObject vendingMachine)
        {
            GetVendingMachineDataFile(filename).WriteObject(vendingMachine);
        }

        ConversationTree GetConversation(string conversationFile)
        {
            ConversationTree conversation = null;
            if (!Conversations.TryGetValue(conversationFile, out conversation))
                return null;
            return conversation;
        }

        JObject GetVendingMachine(string vendingMachineFile)
        {
            JObject data = new JObject();
            if (!TryLoadVendingMachine(vendingMachineFile, out data))
                return null;
            return data;
        }

        void GenerateDefaultConversation()
        {
            ConversationTree conversation = null;

            if (!TryLoadConversation("default", out conversation))
            {
                if (!ConversationDataFileExists("default"))
                {
                    PrintInfo("Generating default conversation...");

                    conversation.Add(0, new Dialogue()
                    {
                        Message = "Welcome to my shop $displayName. How can I help you?",
                        Responses = new List<Response>()
                        {
                            new Response()
                            {
                                Message = "I would like to know how you are doing.",
                                NextMessage = 1
                            },
                            new Response()
                            {
                                Message = "I am just saying hi.",
                                NextMessage = 5
                            }
                        }
                    });
                    conversation.Add(1, new Dialogue()
                    {
                        Message = "Im doing good how about you?",
                        Responses = new List<Response>()
                        {
                            new Response()
                            {
                                Message = "I am ok today.",
                                NextMessage = 2
                            },
                            new Response()
                            {
                                Message = "i dont know.",
                                NextMessage = 3
                            },
                            new Response()
                            {
                                Message = "Life stinks.",
                                NextMessage = 4
                            }
                        }
                    });
                    conversation.Add(2, new Dialogue()
                    {
                        Message = "That is a nice to hear?",
                        Responses = new List<Response>()
                        {
                            new Response()
                            {
                                Message = "I Will talk to you later",
                                NextMessage = 5
                            }
                        }
                    });
                    conversation.Add(3, new Dialogue()
                    {
                        Message = "Sorry to hear that?",
                        Responses = new List<Response>()
                        {
                            new Response()
                            {
                                Message = "I guess i will talk to you later.",
                                NextMessage = 5
                            }
                        }
                    });
                    conversation.Add(4, new Dialogue()
                    {
                        Message = "That to bad i hope you day gets better?",
                        Responses = new List<Response>()
                        {
                            new Response()
                            {
                                Message = "Thank you i will see you later.",
                                NextMessage = 5
                            }
                        }
                    });
                    conversation.Add(5, new Dialogue()
                    {
                        Message = "Let me know if there's anything I can do to help!"
                    });
                    conversation.Add(6, new Dialogue()
                    {
                        Message = "Go get your ride before someone steals it and please stop by again.",
                        Responses = new List<Response>()
                        {
                            new Response()
                            {
                                Message = "See you later."
                            }
                        }
                    });
                    conversation.Add(7, new Dialogue()
                    {
                        Message = "I am sorry you do not have enough scrap.",
                        Responses = new List<Response>()
                        {
                            new Response()
                            {
                                Message = "I will come back later."
                            }
                        }
                    });

                    SaveConversation("default", conversation);
                }
            }
            else
                SaveConversation("default", conversation);
        }

        public void GenerateModularCarConversation(string convoName = "modularcar")
        {
            ConversationTree conversation = null;

            if (!TryLoadConversation(convoName, out conversation))
            {
                if (!ConversationDataFileExists(convoName))
                {
                    PrintInfo($"Generating {convoName} conversation...");

                    conversation.Add(0, new Dialogue()
                    {
                        Message = "Welcome to my shop $displayName. How can I help you?",
                        Responses = new List<Response>()
                        {
                            new Response()
                            {
                                Message = "I would like to buy a car.",
                                NextMessage = 1
                            },
                            new Response()
                            {
                                Message = "I am just browsing.",
                                NextMessage = 5
                            }
                        }
                    });
                    conversation.Add(1, new Dialogue()
                    {
                        Message = "A ride you say. Which style ride would you like?",
                        Responses = new List<Response>()
                        {
                            new Response()
                            {
                                Message = "I would like a small car.",
                                NextMessage = 2
                            },
                            new Response()
                            {
                                Message = "I would like a medium car.",
                                NextMessage = 3
                            },
                            new Response()
                            {
                                Message = "I would like a large car.",
                                NextMessage = 4
                            }
                        }
                    });
                    conversation.Add(2, new Dialogue()
                    {
                        Message = "That is a nice choice. Do you want to buy this ride?",
                        Responses = new List<Response>()
                        {
                            new Response()
                            {
                                Message = "Here is my 250 scrap.",
                                PlayerCommands = new List<string>()
                                {
                                    $"{Commands.SpawnVehicle} 1 $npcName"
                                },
                                NextMessage = 6,
                                Price = 250,
                                InsufficientFundsMessage = 7
                            },
                            new Response()
                            {
                                Message = "I changed my mind.",
                                NextMessage = 5
                            }
                        }
                    });
                    conversation.Add(3, new Dialogue()
                    {
                        Message = "That is a nice choice. Do you want to buy this ride?",
                        Responses = new List<Response>()
                        {
                            new Response()
                            {
                                Message = "Here is my 350 scrap.",
                                PlayerCommands = new List<string>()
                                {
                                    $"{Commands.SpawnVehicle} 2 $npcName"
                                },
                                NextMessage = 6,
                                Price = 350,
                                InsufficientFundsMessage = 7
                            },
                            new Response()
                            {
                                Message = "I changed my mind.",
                                NextMessage = 5
                            }
                        }
                    });
                    conversation.Add(4, new Dialogue()
                    {
                        Message = "That is a nice choice. Do you want to buy this ride?",
                        Responses = new List<Response>()
                        {
                            new Response()
                            {
                                Message = "Here is my 500 scrap.",
                                PlayerCommands = new List<string>()
                                {
                                    $"{Commands.SpawnVehicle} 3 $npcName"
                                },
                                NextMessage = 6,
                                Price = 500,
                                InsufficientFundsMessage = 7
                            },
                            new Response()
                            {
                                Message = "I changed my mind.",
                                NextMessage = 5
                            }
                        }
                    });
                    conversation.Add(5, new Dialogue()
                    {
                        Message = "Let me know if there's anything I can do to help!"
                    });
                    conversation.Add(6, new Dialogue()
                    {
                        Message = "Go get your ride before someone steals it and please stop by again.",
                        Responses = new List<Response>()
                        {
                            new Response()
                            {
                                Message = "See you later."
                            }
                        }
                    });
                    conversation.Add(7, new Dialogue()
                    {
                        Message = "I am sorry you do not have enough scrap.",
                        Responses = new List<Response>()
                        {
                            new Response()
                            {
                                Message = "I will come back later."
                            }
                        }
                    });

                    SaveConversation(convoName, conversation);
                }
            }
            else
                SaveConversation(convoName, conversation);
        }

        public void GenerateCopterConversation(string convoName = "copter")
        {
            ConversationTree conversation = null;

            if (!TryLoadConversation(convoName, out conversation))
            {
                if (!ConversationDataFileExists(convoName))
                {
                    PrintInfo($"Generating {convoName} conversation...");

                    conversation.Add(0, new Dialogue()
                    {
                        Message = "Welcome to my shop $displayName. How can I help you?",
                        Responses = new List<Response>()
                        {
                            new Response()
                            {
                                Message = "I would like to buy a copter.",
                                NextMessage = 1
                            },
                            new Response()
                            {
                                Message = "I am just browsing.",
                                NextMessage = 4
                            }
                        }
                    });
                    conversation.Add(1, new Dialogue()
                    {
                        Message = "A copter you say. Which style copter would you like?",
                        Responses = new List<Response>()
                        {
                            new Response()
                            {
                                Message = "I would like a minicopter.",
                                NextMessage = 2
                            },
                            new Response()
                            {
                                Message = "I would like a scrapcopter.",
                                NextMessage = 3
                            },
                        }
                    });
                    conversation.Add(2, new Dialogue()
                    {
                        Message = "That is a nice choice. Do you want to buy this minicopter?",
                        Responses = new List<Response>()
                        {
                            new Response()
                            {
                                Message = "Here is my 250 scrap.",
                                PlayerCommands = new List<string>()
                                {
                                    $"{Commands.SpawnVehicle} minicopter $npcName"
                                },
                                NextMessage = 5,
                                Price = 250,
                                InsufficientFundsMessage = 6
                            },
                            new Response()
                            {
                                Message = "I changed my mind.",
                                NextMessage = 5
                            }
                        }
                    });
                    conversation.Add(3, new Dialogue()
                    {
                        Message = "That is a nice choice. Do you want to buy this scrapcopter?",
                        Responses = new List<Response>()
                        {
                            new Response()
                            {
                                Message = "Here is my 350 scrap.",
                                PlayerCommands = new List<string>()
                                {
                                    $"{Commands.SpawnVehicle} scraptransporthelicopter $npcName"
                                },
                                NextMessage = 5,
                                Price = 350,
                                InsufficientFundsMessage = 6
                            },
                            new Response()
                            {
                                Message = "I changed my mind.",
                                NextMessage = 4
                            }
                        }
                    });

                    conversation.Add(4, new Dialogue()
                    {
                        Message = "Let me know if there's anything I can do to help!"
                    });
                    conversation.Add(5, new Dialogue()
                    {
                        Message = "Go get your ride before someone steals it and please stop by again.",
                        Responses = new List<Response>()
                        {
                            new Response()
                            {
                                Message = "See you later."
                            }
                        }
                    });
                    conversation.Add(6, new Dialogue()
                    {
                        Message = "I am sorry you do not have enough scrap.",
                        Responses = new List<Response>()
                        {
                            new Response()
                            {
                                Message = "I will come back later."
                            }
                        }
                    });

                    SaveConversation(convoName, conversation);
                }
            }
            else
                SaveConversation(convoName, conversation);
        }

        public void GenerateSnowMobileConversation(string convoName = "defaultsnowmobile")
        {
            ConversationTree conversation = null;

            if (!TryLoadConversation(convoName, out conversation))
            {
                if (!ConversationDataFileExists(convoName))
                {
                    PrintInfo($"Generating {convoName} conversation...");

                    conversation.Add(0, new Dialogue()
                    {
                        Message = "Welcome to my shop $displayName. How can I help you?",
                        Responses = new List<Response>()
                        {
                            new Response()
                            {
                                Message = "I would like to buy a snowmobile.",
                                NextMessage = 1
                            },
                            new Response()
                            {
                                Message = "I am just browsing.",
                                NextMessage = 4
                            }
                        }
                    });
                    conversation.Add(1, new Dialogue()
                    {
                        Message = "A snowmobile you say. Which style snowmobile would you like?",
                        Responses = new List<Response>()
                        {
                            new Response()
                            {
                                Message = "I would like a standard.",
                                NextMessage = 2
                            },
                            new Response()
                            {
                                Message = "I would like a Tomaha.",
                                NextMessage = 3
                            },
                        }
                    });
                    conversation.Add(2, new Dialogue()
                    {
                        Message = "That is a nice choice. Do you want to buy this standard snowmobile?",
                        Responses = new List<Response>()
                        {
                            new Response()
                            {
                                Message = "Here is my 250 scrap.",
                                PlayerCommands = new List<string>()
                                {
                                    $"{Commands.SpawnVehicle} snow $npcName"
                                },
                                NextMessage = 5,
                                Price = 250,
                                InsufficientFundsMessage = 6
                            },
                            new Response()
                            {
                                Message = "I changed my mind.",
                                NextMessage = 5
                            }
                        }
                    });
                    conversation.Add(3, new Dialogue()
                    {
                        Message = "That is a nice choice. Do you want to buy this Tomaha snowmobile?",
                        Responses = new List<Response>()
                        {
                            new Response()
                            {
                                Message = "Here is my 350 scrap.",
                                PlayerCommands = new List<string>()
                                {
                                    $"{Commands.SpawnVehicle} tomaha $npcName"
                                },
                                NextMessage = 5,
                                Price = 350,
                                InsufficientFundsMessage = 6
                            },
                            new Response()
                            {
                                Message = "I changed my mind.",
                                NextMessage = 4
                            }
                        }
                    });

                    conversation.Add(4, new Dialogue()
                    {
                        Message = "Let me know if there's anything I can do to help!"
                    });
                    conversation.Add(5, new Dialogue()
                    {
                        Message = "Go get your ride before someone steals it and please stop by again.",
                        Responses = new List<Response>()
                        {
                            new Response()
                            {
                                Message = "See you later."
                            }
                        }
                    });
                    conversation.Add(6, new Dialogue()
                    {
                        Message = "I am sorry you do not have enough scrap.",
                        Responses = new List<Response>()
                        {
                            new Response()
                            {
                                Message = "I will come back later."
                            }
                        }
                    });

                    SaveConversation(convoName, conversation);
                }
            }
            else
                SaveConversation(convoName, conversation);
        }

        public void GenerateBoatConversation(string convoName = "defaultboats")
        {
            ConversationTree conversation = null;

            if (!TryLoadConversation(convoName, out conversation))
            {
                if (!ConversationDataFileExists(convoName))
                {
                    PrintInfo($"Generating {convoName} conversation...");

                    conversation.Add(0, new Dialogue()
                    {
                        Message = "Welcome to my shop $displayName. How can I help you?",
                        Responses = new List<Response>()
                        {
                            new Response()
                            {
                                Message = "I would like to buy a boat.",
                                NextMessage = 1
                            },
                            new Response()
                            {
                                Message = "I am just browsing.",
                                NextMessage = 4
                            }
                        }
                    });
                    conversation.Add(1, new Dialogue()
                    {
                        Message = "A boat you say. Which style boat would you like?",
                        Responses = new List<Response>()
                        {
                            new Response()
                            {
                                Message = "I would like a Rowboat.",
                                NextMessage = 2
                            },
                            new Response()
                            {
                                Message = "I would like a RHIB.",
                                NextMessage = 3
                            },
                            new Response()
                            {
                                Message = "I would like a TugBoat.",
                                NextMessage = 7
                            },
                        }
                    });
                    conversation.Add(2, new Dialogue()
                    {
                        Message = "That is a nice choice. Do you want to buy this roawboat?",
                        Responses = new List<Response>()
                        {
                            new Response()
                            {
                                Message = "Here is my 250 scrap.",
                                PlayerCommands = new List<string>()
                                {
                                    $"{Commands.SpawnVehicle} rowboat $npcName"
                                },
                                NextMessage = 5,
                                Price = 250,
                                InsufficientFundsMessage = 6
                            },
                            new Response()
                            {
                                Message = "I changed my mind.",
                                NextMessage = 5
                            }
                        }
                    });
                    conversation.Add(3, new Dialogue()
                    {
                        Message = "That is a nice choice. Do you want to buy this rhib?",
                        Responses = new List<Response>()
                        {
                            new Response()
                            {
                                Message = "Here is my 350 scrap.",
                                PlayerCommands = new List<string>()
                                {
                                    $"{Commands.SpawnVehicle} rhib $npcName"
                                },
                                NextMessage = 5,
                                Price = 350,
                                InsufficientFundsMessage = 6
                            },
                            new Response()
                            {
                                Message = "I changed my mind.",
                                NextMessage = 4
                            }
                        }
                    });

                    conversation.Add(4, new Dialogue()
                    {
                        Message = "Let me know if there's anything I can do to help!"
                    });
                    conversation.Add(5, new Dialogue()
                    {
                        Message = "Go get your ride before someone steals it and please stop by again.",
                        Responses = new List<Response>()
                        {
                            new Response()
                            {
                                Message = "See you later."
                            }
                        }
                    });
                    
                    conversation.Add(6, new Dialogue()
                    {
                        Message = "I am sorry you do not have enough scrap.",
                        Responses = new List<Response>()
                        {
                            new Response()
                            {
                                Message = "I will come back later."
                            }
                        }
                    });

                    conversation.Add(7, new Dialogue()
                    {
                        Message = "That is a nice choice. Do you want to buy this TugBoat?",
                        Responses = new List<Response>()
                        {
                            new Response()
                            {
                                Message = "Here is my 500 scrap.",
                                PlayerCommands = new List<string>()
                                {
                                    $"{Commands.SpawnVehicle} tugboat $npcName"
                                },
                                NextMessage = 5,
                                Price = 500,
                                InsufficientFundsMessage = 6
                            },
                            new Response()
                            {
                                Message = "I changed my mind.",
                                NextMessage = 5
                            }
                        }
                    });

                    SaveConversation(convoName, conversation);
                }
            }
            else
                SaveConversation(convoName, conversation);
        }

        void LoadData()
        {
            try
            {
                TalkerSpawnsData = Interface.Oxide.DataFileSystem.ReadObject<TalkerSpawns>(Name + "/TalkerSpawns");
            }
            catch
            {
                try // Check if legacy data
                {
                    TalkerSpawnsData = Interface.Oxide.DataFileSystem.ReadObject<KeyValuePair<string, TalkerSpawns>>(Name + "/TalkerSpawns").Value;
                }
                catch
                {
                    PrintWarning("Couldn't load NpcInfo data, Creating new NpcInfo Data save file");
                    TalkerSpawnsData = new TalkerSpawns();
                }
            }

            try
            {
                PlayerCooldownsData = Interface.Oxide.DataFileSystem.ReadObject<PlayerCooldowns>(Name + "/PlayerCooldown");
            }
            catch
            {
                try // Check if legacy data
                {
                    PlayerCooldownsData = Interface.Oxide.DataFileSystem.ReadObject<KeyValuePair<string, PlayerCooldowns>>(Name + "/PlayerCooldown").Value;
                }
                catch
                {
                    PrintWarning("Couldn't load Cooldown data, Creating new Cooldown Data save file");
                    PlayerCooldownsData = new PlayerCooldowns();
                }
            }
            try
            {
                GlobalCooldownsData = Interface.Oxide.DataFileSystem.ReadObject<GlobalCooldowns>(Name + "/GlobalCooldown");
            }
            catch
            {
                PrintWarning("Couldn't load global Cooldown data, Creating new global Cooldown Data save file");
                GlobalCooldownsData = new GlobalCooldowns();
            }
        }

        void SavePlayerCooldowns()
        {
            PlayerCooldownsFile.WriteObject(PlayerCooldownsData);
        }

        void SaveGlobalCooldowns()
        {
            GlobalCooldownsFile.WriteObject(GlobalCooldownsData);
        }

        DateTime? GetCooldown(BasePlayer player, string conversation, uint messageId, int responseIndex)
        {
            if (PlayerCooldownsData.ContainsKey(player.userID) &&
                PlayerCooldownsData[player.userID].ContainsKey(conversation) &&
                PlayerCooldownsData[player.userID][conversation].ContainsKey(messageId) &&
                PlayerCooldownsData[player.userID][conversation][messageId].ContainsKey(responseIndex))
                return PlayerCooldownsData[player.userID][conversation][messageId][responseIndex];

            return null;
        }

        DateTime? GetGlobalCooldown(ulong npcID, string conversation, uint messageId, int responseIndex)
        {
            if (GlobalCooldownsData.ContainsKey(npcID) &&
                GlobalCooldownsData[npcID].ContainsKey(conversation) &&
                GlobalCooldownsData[npcID][conversation].ContainsKey(messageId) &&
                GlobalCooldownsData[npcID][conversation][messageId].ContainsKey(responseIndex))
                return GlobalCooldownsData[npcID][conversation][messageId][responseIndex];

            return null;
        }

        void SetCooldown(BasePlayer player, string conversation, uint messageId, int responseIndex)
        {
            if (Conversations.ContainsKey(conversation) &&
                Conversations[conversation].ContainsKey(messageId) &&
                Conversations[conversation][messageId].Responses.Count > responseIndex)
            {
                var response = Conversations[conversation][messageId].Responses[responseIndex];

                if (response.Cooldown > 0)
                {
                    var cooldown = DateTime.Now.AddSeconds(response.Cooldown);

                    if (!PlayerCooldownsData.ContainsKey(player.userID))
                    {
                        var responeCooldowns = new ResponseCooldowns() { { responseIndex, cooldown } };
                        var messageCooldowns = new MessageCooldowns() { { messageId, responeCooldowns } };
                        PlayerCooldownsData[player.userID] = new ConversationCooldowns() { { conversation, messageCooldowns } };

                    }
                    else
                    {
                        if (!PlayerCooldownsData[player.userID].ContainsKey(conversation))
                        {
                            var responeCooldowns = new ResponseCooldowns() { { responseIndex, cooldown } };
                            var messageCooldowns = new MessageCooldowns() { { messageId, responeCooldowns } };
                            PlayerCooldownsData[player.userID].Add(conversation, messageCooldowns);
                        }

                        if (!PlayerCooldownsData[player.userID][conversation].ContainsKey(messageId))
                            PlayerCooldownsData[player.userID][conversation].Add(messageId, new ResponseCooldowns() { { responseIndex, cooldown } });

                        PlayerCooldownsData[player.userID][conversation][messageId][responseIndex] = cooldown;
                    }
                    SavePlayerCooldowns();

                }
            }
        }

        void SetGlobalCooldown(ulong npcID, string conversation, uint messageId, int responseIndex)
        {
            if (Conversations.ContainsKey(conversation) &&
                Conversations[conversation].ContainsKey(messageId) &&
                Conversations[conversation][messageId].Responses.Count > responseIndex)
            {
                var response = Conversations[conversation][messageId].Responses[responseIndex];

                if (response.Cooldown > 0)
                {
                    var cooldown = DateTime.Now.AddSeconds(response.Cooldown);

                    if (!GlobalCooldownsData.ContainsKey(npcID))
                    {
                        var responeCooldowns = new ResponseCooldowns() { { responseIndex, cooldown } };
                        var messageCooldowns = new MessageCooldowns() { { messageId, responeCooldowns } };
                        GlobalCooldownsData[npcID] = new ConversationCooldowns() { { conversation, messageCooldowns } };

                    }
                    else
                    {
                        if (!GlobalCooldownsData[npcID].ContainsKey(conversation))
                        {
                            var responeCooldowns = new ResponseCooldowns() { { responseIndex, cooldown } };
                            var messageCooldowns = new MessageCooldowns() { { messageId, responeCooldowns } };
                            GlobalCooldownsData[npcID].Add(conversation, messageCooldowns);
                        }

                        if (!GlobalCooldownsData[npcID][conversation].ContainsKey(messageId))
                            GlobalCooldownsData[npcID][conversation].Add(messageId, new ResponseCooldowns() { { responseIndex, cooldown } });

                        GlobalCooldownsData[npcID][conversation][messageId][responseIndex] = cooldown;
                    }
                    SaveGlobalCooldowns();

                }
            }
        }
        void SaveTalkerSpawns()
        {
            TalkerSpawnsFile.WriteObject(TalkerSpawnsData);
        }
        #endregion

        #region Initialization/Deinitialization
        void Init()
        {
            TalkerSpawnsFile = Interface.Oxide.DataFileSystem.GetFile(Name + "/TalkerSpawns");
            PlayerCooldownsFile = Interface.Oxide.DataFileSystem.GetFile(Name + "/PlayerCooldown");
            GlobalCooldownsFile = Interface.Oxide.DataFileSystem.GetFile(Name + "/GlobalCooldown");

            LoadData();
            GenerateDefaultConversation();
            GenerateCopterConversation();
            GenerateModularCarConversation();
            GenerateSnowMobileConversation();
            GenerateBoatConversation();
            RegisterPermissions();
        }

        private void OnServerInitialized()
        {
            foreach (var talkerSpawn in TalkerSpawnsData.Values)
            {
                ConversationTree conversation = null;
                if (!TryLoadConversation(talkerSpawn.ConversationFile, out conversation))
                {
                    PrintError($"Unable to spawn NPC \"{talkerSpawn.Name}\": Invalid conversation file {talkerSpawn.ConversationFile}");
                    continue;
                }
                SaveConversation(talkerSpawn.ConversationFile, conversation);

                if (talkerSpawn.VendingMachines.Count > 0 && !CustomVendingSetup)
                    PrintError($"Missing Dependency CustomVendingSetup: Vending commands disabled for \"{talkerSpawn.Name}\"");

                if (!string.IsNullOrEmpty(talkerSpawn.Monument))
                {
                    foreach (var monument in TerrainMeta.Path.Monuments.Where(m => m != null && GetMonumentName(m).Contains(talkerSpawn.Monument, CompareOptions.IgnoreCase)))
                    {
                        if (SpawnTalkingNpc(talkerSpawn, monument) != null)
                            SaveTalkerSpawns();
                    }
                }
                else if (SpawnTalkingNpc(talkerSpawn) != null)
                    SaveTalkerSpawns();
            }

            RegisterDataPermissions();
        }

        void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(player, Ui.Overlay.TalkScreen.Panel);

            foreach (var talkerSpawn in TalkerSpawnsData.Values)
                KillTalkingNpc(talkerSpawn);
        }

        private void RegisterPermissions()
        {
            permission.RegisterPermission(configData.settings.PermissionUse, this);
        }

        private void RegisterDataPermissions()
        {
            foreach (var response in Conversations.Values.SelectMany(c => c.Values.SelectMany(d => d.Responses).Where(r => !string.IsNullOrEmpty(r.Permission))))
            {
                if (!permission.PermissionExists(response.Permission))
                    permission.RegisterPermission(response.Permission, this);
            }
        }

        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Blocked"] = "<color=#ce422b>You can not use this command</color>",
                ["cooldown"] = "Come back in",
                ["itemDrop"] = "Your item was dropped on the ground"
            }, this);
        }
        #endregion

        #region Hooks
        private void GiveItemToPlayer(BasePlayer player, int itemID, int amount = 1, string name = "", ulong skinID = 0)
        {
            if (player == null) return;

            Item item = ItemManager.CreateByItemID(itemID, amount, skinID);
            if (item == null) return;
            
            if (!string.IsNullOrEmpty(name))
                item.name = name;

            if (skinID != 0)
            {
                BaseEntity heldEntity = item.GetHeldEntity();
                if (heldEntity != null)
                {
                    heldEntity.skinID = skinID;
                }
            }
            if (item.MoveToContainer(player.inventory.containerBelt, -1, true))
            {
                player.Command("note.inv", item.info.itemid, amount);
                return;
            }
            else if (item.MoveToContainer(player.inventory.containerMain, -1, true))
            {
                player.Command("note.inv", item.info.itemid, amount);
                return;
            }

            Vector3 velocity = Vector3.zero;
            item.Drop(player.transform.position + new Vector3(0.5f, 1f, 0), velocity);
            SendReply(player, lang.GetMessage("itemDrop", this));
        }

        private void CreateMarker(BaseEntity entity, string name, int duration = 0, float refreshRate = 3f, float radius = 0.4f, string displayName = "Marker", string colorMarker = "00FFFF", string colorOutline = "00FFFFFF")
        {
            Interface.CallHook("API_CreateMarker", entity, name, duration, refreshRate, radius, displayName, colorMarker, colorOutline);
        }

        private void RemoveMarker(string name)
        {
            Interface.CallHook("API_RemoveMarker", name);
        }

        private string FormatTime(TimeSpan dateDifference)
        {
            var days = dateDifference.Days;
            var hours = dateDifference.Hours;
            hours += (days * 24);
            return string.Format("{0:00}:{1:00}:{2:00}", hours, dateDifference.Minutes, dateDifference.Seconds);
        }

        private object CanHelicopterTarget(PatrolHelicopterAI heliAi, NPCTalking player)
        {
            return false;
        }

        private object CanHelicopterStrafeTarget(PatrolHelicopterAI heliAi, NPCTalking target)
        {
            return false;
        }

        object OnNpcConversationStart(NPCTalking npcTalking, BasePlayer player, ConversationData conversationData)
        {
            if (npcTalking != null && player != null && !string.IsNullOrEmpty(npcTalking.displayName) && TalkerSpawnsData.ContainsKey(npcTalking.displayName.ToLower()))
            {
                if (TalkerSpawnsData[npcTalking.displayName.ToLower()].CallOnUseNpc)
                {
                    Interface.Oxide.CallHook("OnUseNPC", npcTalking, player);
                    return false;
                }
                object isRewardsNpc = ServerRewards?.Call("IsRegisteredNPC", npcTalking.UserIDString);
                if (isRewardsNpc != null && isRewardsNpc is bool && (bool)isRewardsNpc)
                {
                    Interface.Oxide.CallHook("OnUseNPC", npcTalking, player);
                    return false;
                }

                if (TalkerSpawnsData[npcTalking.displayName.ToLower()].useCommand != null && TalkerSpawnsData[npcTalking.displayName.ToLower()].useCommand.Count > 0)
                {
                    foreach (string comuse in TalkerSpawnsData[npcTalking.displayName.ToLower()].useCommand)
                    {
                        var newCommand = comuse.Replace("playerID", player.UserIDString).Replace("$userID", player.UserIDString).Replace("$displayName", player.displayName).Replace("$npcName", npcTalking.name);

                        rust.RunServerCommand(newCommand);
                    }
                    if (TalkerSpawnsData[npcTalking.displayName.ToLower()].useCommandClose)
                        return false;
                }

                ConversationHandler handler = npcTalking.GetComponent<ConversationHandler>();
                if (handler == null) return null;

                handler.StartConversation(player);

                BuildTalkScreen(player, TalkerSpawnsData[npcTalking.displayName.ToLower()].ConversationFile, npcTalking.displayName);
                npcTalking?.CleanupConversingPlayers();
                return false;
            }
            return null;
        }

        Dictionary<string, object> OnCustomVendingSetupDataProvider(InvisibleVendingMachine vendingMachine)
        {
            if (VendingMachineConfigurations.ContainsKey(vendingMachine))
            {
                var vendingMachineFile = VendingMachineConfigurations[vendingMachine];

                Dictionary<string, object> dataProvider;
                if (!VendingMachineDataProviders.TryGetValue(vendingMachineFile, out dataProvider))
                {
                    dataProvider = new Dictionary<string, object>
                    {
                        ["GetData"] = new Func<JObject>(() => GetVendingMachine(vendingMachineFile)),
                        ["SaveData"] = new Action<JObject>(data => SaveVendingMachine(vendingMachineFile, data))
                    };
                    VendingMachineDataProviders.Add(vendingMachineFile, dataProvider);
                }

                return dataProvider;
            }

            return null;
        }

        public bool conversationfileExists(string npcname)
        {
            if (TalkerSpawnsData.ContainsKey(npcname.ToLower()))
            {
                return true;
            }
            return false;
        }

        public bool conversationfileCreate(string typeName, string convoName)
        {
            
            if (typeName == "modularcar")
            {
                GenerateModularCarConversation(convoName);
                return true;
            }
            return false;
        }

        #endregion

        #region Commands
        [ConsoleCommand("talkingnpctest")]
        private void CommandViewConversationCounsol(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player != null)
            {
                if (!arg.HasArgs(2))
                    return;
                BuildTalkScreen(player, arg.GetString(0), arg.GetString(1));
            }
            else if (arg.HasArgs(3))
            {
                var ids = default(ulong);
                if (ulong.TryParse(arg.Args[0], out ids))
                {
                    BasePlayer TCplayer = BasePlayer.FindByID(ids);
                    if (TCplayer != null)
                        BuildTalkScreen(TCplayer, arg.Args[1], arg.Args[2]);
                }
            }          
        }

        public void CommandSpawnCall(BasePlayer player, string command, string[] args)
        {
            CommandSpawn(player, command, args);
        }

        [ChatCommand(Commands.ManageNpc)]
        void CommandSpawn(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, configData.settings.PermissionUse))
            {
                SendReply(player, string.Format(lang.GetMessage("Blocked", this, player.UserIDString)));
                return;
            }

            var operation = args.Length > 0 ? args[0].ToLower() : string.Empty;
            var name = args.Length > 1 ? args[1] : string.Empty;

            switch (operation)
            {
                case Commands.ManageArguments.Add:
                    {
                        if (args.Length < 2)
                        {
                            SendReply(player, Commands.ManageArgumentsHelp.Add);
                            return;
                        }

                        if (TalkerSpawnsData.ContainsKey(name.ToLower()))
                        {
                            SendReply(player, "This name is in use already!");
                            return;
                        }

                        string conversationFile = string.Empty;
                        bool monument = false;
                        if (args.Length > 2)
                        {
                            if (!bool.TryParse(args[2], out monument))
                                conversationFile = args[2];
                            if (args.Length > 3 && !bool.TryParse(args[3], out monument))
                            {
                                SendReply(player, Commands.ManageArgumentsHelp.Add);
                                return;
                            }
                        }

                        if (monument)
                        {
                            var closestMonument = GetClosestMonument(player.transform.position);
                            if (closestMonument == null)
                            {
                                SendReply(player, "Couldn't find closest monument!");
                                return;
                            }
                            var talker = CreateTalker(name, player.transform.position, player.viewAngles, conversationFile, closestMonument);
                            SpawnTalkingNpc(talker, closestMonument);
                            TalkerSpawnsData.Add(talker.Name.ToLower(), talker);
                            SaveTalkerSpawns();
                            SendReply(player, string.Format($"Added {talker.Name} spawn to monuments {talker.Monument}"));
                        }
                        else
                        {
                            var talker = CreateTalker(name, player.transform.position, player.viewAngles, conversationFile);
                            SpawnTalkingNpc(talker);
                            TalkerSpawnsData.Add(talker.Name.ToLower(), talker);
                            SaveTalkerSpawns();
                            SendReply(player, string.Format($"Added {talker.Name} spawn to local position"));
                        }
                        break;
                    }

                case Commands.ManageArguments.Remove:
                    {
                        if (args.Length < 2)
                        {
                            SendReply(player, Commands.ManageArgumentsHelp.Remove);
                            return;
                        }

                        var talker = TalkerSpawnsData[name];
                        KillTalkingNpc(talker);
                        TalkerSpawnsData.Remove(name);
                        SaveTalkerSpawns();
                        SendReply(player, string.Format(lang.GetMessage("Removed", this, player.UserIDString)));
                        break;
                    }
                default:
                    if (Interface.Oxide.CallHook(TalkingNpcHooks.OnCommand, player, args) != null)
                        break;
                    SendReply(player, "Usage:\n\n"
                        + $"{Commands.ManageArgumentsHelp.Add}"
                        + $"{Commands.ManageArgumentsHelp.Remove}");
                    break;
            }
        }

        [ConsoleCommand(Commands.UiAction)]
        private void UiActionCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();

            if (player == null && !PlayerConversations.ContainsKey(player.userID)) return;

            string[] subs;
            ulong cSkin = 0;
            int cItem = 0;
            int cAmount = 1;
            string last = "";

            switch (arg.GetString(0))
            {
                case Commands.UiActions.OnResponseClicked:
                    {
                        if (!arg.HasArgs(4))
                        {
                            PrintError($"Invalid Command - Usage: {Commands.UiAction} {Commands.UiActions.OnResponseClicked} <CONVERSATION_FILE> <MESSAGE_ID> <RESPONSE_INDEX>");
                            return;
                        }

                        var conversationFile = arg.GetString(1);
                        var messageId = arg.GetUInt(2);
                        var responseIndex = arg.GetInt(3);
                        var conversation = GetConversation(conversationFile);

                        if (conversation == null)
                        {
                            PrintError($"Conversation file does not exist: {conversationFile}");
                            return;
                        }
                        if (!conversation.ContainsKey(messageId))
                        {
                            PrintError($"Invalid message ID for {conversationFile}: {messageId}");
                            return;
                        }
                        if (responseIndex >= conversation[messageId].Responses.Count)
                        {
                            PrintError($"Invalid response index for message {messageId} in {conversationFile}: {responseIndex}");
                            return;
                        }

                        var response = conversation[messageId].Responses[responseIndex];

                        if (response.Price > 0)
                        {
                            if (response.Currency.ItemId == 0000)
                            {
                                if (ServerRewards == null) return;
                                int PlayerTotal = (int)ServerRewards?.Call("CheckPoints", player.userID);
                                if (PlayerTotal >= response.Price)
                                    ServerRewards?.Call("TakePoints", player.userID, response.Price);
                                else
                                {
                                    UpdateDialogue(player, conversationFile, response.InsufficientFundsMessage);
                                    return;
                                }
                            }
                            else if (response.Currency.ItemId == 0001)
                            {
                                if (Economics == null) return;
                                var PlayerTotal = (int)(double)Economics.Call("Balance", player.userID);
                                if (PlayerTotal >= response.Price)
                                {
                                    Economics?.Call("Withdraw", player.userID, Convert.ToDouble(response.Price));
                                }
                                else
                                {
                                    UpdateDialogue(player, conversationFile, response.InsufficientFundsMessage);
                                    return;
                                }
                            }
                            else
                            {
                                int PlayerTotal = GetAmount(player, response.Currency.ItemId, response.Currency.SkinId);
                                if (PlayerTotal >= response.Price)
                                {
                                    Take(player, response.Currency.ItemId, response.Price, response.Currency.SkinId);
                                }
                                else
                                {
                                    UpdateDialogue(player, conversationFile, response.InsufficientFundsMessage);
                                    return;
                                }
                            }
                        }

                        var openVendingCommands = new List<string>();
                        openVendingCommands.AddRange(response.PlayerCommands.Where(c => c.Contains("OpenVending", CompareOptions.IgnoreCase)));
                        openVendingCommands.AddRange(response.ServerCommands.Where(c => c.Contains("OpenVending", CompareOptions.IgnoreCase)));

                        if (CustomVendingSetup && openVendingCommands.Count > 0)
                        {
                            if (openVendingCommands.Count > 1)
                                PrintWarning($"Too many OpenVending commands in {conversationFile}:Message {messageId}:Response {responseIndex} - Using first");

                            var vendingMachineFile = openVendingCommands.First().Replace("OpenVending", "", StringComparison.OrdinalIgnoreCase).Trim();

                            InvisibleVendingMachine vendingMachine = null;
                            if (PlayerConversations[player.userID].VendingMachines.TryGetValue(vendingMachineFile, out vendingMachine))
                                OpenVendingMachine(player, vendingMachine);
                            else
                                PrintWarning($"Unable to find vending machine for {PlayerConversations[player.userID].NpcTalker.displayName}: {vendingMachineFile}");
                        }

                        foreach (var command in response.PlayerCommands)
                        {
                            if (command.Contains("OpenVending", CompareOptions.IgnoreCase)) continue;
                            if (command.Contains("GiveItem", CompareOptions.IgnoreCase))
                            {                               
                                subs = command.Split(' ');
                                var foo = command.Split(' ').Skip(4).ToArray();
                                last = string.Join(" ", foo);
                                if (subs.Length < 4 || !int.TryParse(subs[1], out cItem) || !int.TryParse(subs[2], out cAmount) || !ulong.TryParse(subs[3], out cSkin))
                                   continue;
                                else
                                    GiveItemToPlayer(player, cItem, cAmount, last, cSkin);
                                    continue;
                            }

                            var newCommand = command.Replace("playerID", player.UserIDString).Replace("$userID", player.UserIDString).Replace("$displayName", player.displayName).Replace("$npcName", conversationFile);

                            if (newCommand.Contains(Commands.ManageNpc, CompareOptions.IgnoreCase))
                                Interface.Oxide.CallHook(TalkingNpcHooks.OnCommand, player, newCommand.Split());

                            player.SendConsoleCommand(newCommand);
                        }

                        foreach (var command in response.ServerCommands)
                        {
                            if (command.Contains("OpenVending", CompareOptions.IgnoreCase)) continue;
                            if (command.Contains("GiveItem", CompareOptions.IgnoreCase))
                            {
                                subs = command.Split(' ');
                                var foo = command.Split(' ').Skip(4).ToArray();
                                last = string.Join(" ", foo);
                                if (subs.Length < 4 || !int.TryParse(subs[1], out cItem) || !int.TryParse(subs[2], out cAmount) || !ulong.TryParse(subs[3], out cSkin))
                                    continue;
                                else
                                    GiveItemToPlayer(player, cItem, cAmount, last, cSkin);
                                continue;
                            }

                            var newCommand = command.Replace("playerID", player.UserIDString).Replace("$userID", player.UserIDString).Replace("$displayName", player.displayName).Replace("$npcName", conversationFile);

                            if (newCommand.Contains(Commands.ManageNpc, CompareOptions.IgnoreCase))
                                Interface.Oxide.CallHook(TalkingNpcHooks.OnCommand, player, newCommand.Split());

                            rust.RunServerCommand(newCommand);
                        }

                        UpdateDialogue(player, conversationFile, response.NextMessage);

                        if (response.Cooldown > 0)
                        {
                            if (response.gCooldown != null && response.gCooldown)
                            {
                                if (PlayerConversations.ContainsKey(player.userID))
                                {
                                    ulong userID = PlayerConversations[player.userID].NpcTalker.userID;
                                    SetGlobalCooldown(userID, conversationFile, messageId, responseIndex);
                                }
                            }
                            else
                                SetCooldown(player, conversationFile, messageId, responseIndex);
                        }

                        return;
                    }
                default:
                    PrintWarning($"Unknown command: {arg.FullString}");
                    return;
            }
        }
        #endregion

        #region Functionality
        private void OpenVendingMachine(BasePlayer player, InvisibleVendingMachine vendingMachine)
        {
            vendingMachine.onlyOneUser = false;
            vendingMachine.SendSellOrders(player);
            vendingMachine.PlayerOpenLoot(player, "vendingmachine.customer", false);
            Interface.CallHook("OnVendingShopOpened", vendingMachine, player);
        }

        public string GetMonumentName(MonumentInfo monument)
        {
            if (monument == null) return string.Empty;

            var gameObject = monument.gameObject;

            while (gameObject.name.StartsWith("assets/") == false && gameObject.transform.parent != null)
            {
                gameObject = gameObject.transform.parent.gameObject;
            }

            var monumentName = gameObject?.name;
            if (monumentName.Contains("monument_marker.prefab"))
                monumentName = monument.gameObject?.gameObject?.transform?.parent?.gameObject?.transform?.root?.name;

            return monumentName;
        }

        MonumentInfo GetClosestMonument(Vector3 position)
        {
            float minDistance = float.MaxValue;
            MonumentInfo closestMonument = null;
            foreach (var monument in UnityEngine.Object.FindObjectsOfType<MonumentInfo>())
            {
                if (string.IsNullOrEmpty(GetMonumentName(monument)) || GetMonumentName(monument).Contains("substation")) continue;
                var distance = Vector3.Distance(position, monument.transform.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestMonument = monument;
                }
            }

            return closestMonument;
        }

        SavedTalker CreateTalker(string name, Vector3 position, Vector3 rotation, string conversationFile, MonumentInfo monument = null)
        {
            var monumentName = GetMonumentName(monument);

            if (monument != null)
            {
                if (string.IsNullOrEmpty(monumentName)) return null;

                position = monument.transform.InverseTransformPoint(position);

                var localY = rotation.y - monument.transform.rotation.eulerAngles.y;
                rotation = new Vector3(0, (localY + 360) % 360, 0); 
            }

            var newTalker = new SavedTalker()
            {
                Name = name,
                Position = position,
                Rotation = rotation,
                Monument = monumentName,
                Kit = "",
                useCommand = new List<string>()
            };

            if (!string.IsNullOrEmpty(conversationFile))
                newTalker.ConversationFile = conversationFile;

            return newTalker;
        }

        private void GetTalkerUserID(SavedTalker talker)
        {
            ulong newId = (ulong)UnityEngine.Random.Range(100000000000, 999999999999);
            talker.NpcUserID = newId;
            SaveTalkerSpawns();
        }

        NPCTalking SpawnTalkingNpc(SavedTalker talker, MonumentInfo monument = null)
        {
            var spawnPosition = monument == null ? talker.Position : monument.transform.TransformPoint(new Vector3(-107.3504f, 12.1489f, -107.7641f));
            spawnPosition = monument == null ? talker.Position : monument.transform.TransformPoint(talker.Position);
            var spawnRotation = monument == null ? Quaternion.Euler(talker.Rotation) : monument.transform.rotation * Quaternion.Euler(talker.Rotation);

            ConversationTree conversation = null;
            if (!TryLoadConversation(talker.ConversationFile, out conversation))
            {
                PrintError($"Unable to spawn Talking NPC {talker.Name} - Invalid conversation file: {talker.ConversationFile}");
                return null;
            }
            SaveConversation(talker.ConversationFile, conversation);

            var talkerNpc = GameManager.server.CreateEntity("assets/prefabs/npc/bandit/shopkeepers/bandit_conversationalist.prefab", spawnPosition, spawnRotation) as NPCTalking;

            if (talkerNpc != null)
            {
                talkerNpc.enableSaving = false;
                if (talker.NpcUserID == 0)
                    GetTalkerUserID(talker);
                if (talker.NpcUserID != 0)
                {
                    talkerNpc.userID = talker.NpcUserID;
                    talkerNpc.UserIDString = talker.NpcUserID.ToString();
                }
                talkerNpc.Spawn();
                talkerNpc.displayName = talker.Name;
                if (talker.Kit == null)
                    talker.Kit = "";
                if (talker.useCommand == null)
                    talker.useCommand = new List<string>();

                timer.Once(1f, () =>
                {
                    if (talkerNpc == null) return;

                    if (!string.IsNullOrEmpty(talker.Kit))
                    {
                        talkerNpc.inventory.Strip();
                        NextTick(() =>
                        {
                            if (talkerNpc == null) return;
                            Kits?.Call("GiveKit", talkerNpc, talker.Kit);
                        });  
                    }

                    if (talker.UmodMarkersManager == null)
                    {
                        TalkerSpawnsData[talker.Name.ToLower()].UmodMarkersManager = new markerInfo();
                        SaveTalkerSpawns();
                    }
                    else if (talker.UmodMarkersManager.enabled)
                    {
                        CreateMarker(talkerNpc, talker.Name.ToLower(), 0, 3600f, talker.UmodMarkersManager.radius, talker.UmodMarkersManager.displayName, talker.UmodMarkersManager.colorMarker, talker.UmodMarkersManager.colorOutline);
                    }

                    talkerNpc.displayName = talker.Name;
                    talkerNpc.SendNetworkUpdateImmediate(true);
                });
                var conversationHandler = talkerNpc.GetOrAddComponent<ConversationHandler>();

                if (CustomVendingSetup)
                {
                    foreach (var vendingMachine in talker.VendingMachines)
                    {
                        var vendingMachineInstance = SpawnVendingMachine(spawnPosition, spawnRotation);
                        if (vendingMachineInstance != null)
                        {
                            conversationHandler.AddVendingMachine(vendingMachine, vendingMachineInstance);
                            VendingMachineConfigurations[vendingMachineInstance] = vendingMachine;
                        }
                    }
                }

                talker.Instances.Add(talkerNpc.net.ID.Value);
            }

            return talkerNpc;
        }

        public BaseNetworkable FindEntity(ulong netID)
        {
            return BaseNetworkable.serverEntities.Find(new NetworkableId(netID));
        }

        private void KillTalkingNpc(SavedTalker talker)
        {
            foreach (var instance in talker.Instances)
            {
                var talkingNpc = FindEntity(instance) as NPCTalking;

                if (talkingNpc != null)
                    talkingNpc.Kill();
            }
        }

        InvisibleVendingMachine SpawnVendingMachine(Vector3 position, Quaternion rotation)
        {
            if (!CustomVendingSetup) return null;

            var vendingMachine = GameManager.server.CreateEntity("assets/prefabs/deployable/vendingmachine/npcvendingmachines/shopkeeper_vm_invis.prefab", position, rotation) as InvisibleVendingMachine;

            if (vendingMachine != null)
            {
                vendingMachine.EnableSaving(false);
                vendingMachine.SetFlag(BaseEntity.Flags.Reserved6, true);
                vendingMachine.Spawn();
                vendingMachine.onlyOneUser = false;
            }

            return vendingMachine;
        }

        public int GetAmount(BasePlayer player, int itemid, ulong skin)
        {
            int num = 0;
            foreach (Item obj in player.inventory.containerMain.itemList)
            {
                if (obj.info.itemid == itemid && obj.skin == skin)
                    num += obj.amount;
            }
            foreach (Item obj in player.inventory.containerBelt.itemList)
            {
                if (obj.info.itemid == itemid && obj.skin == skin)
                    num += obj.amount;
            }
            return num;
        }

        public int Take(BasePlayer player, int itemid, int iAmount, ulong skin)
        {
            int num1 = 0;
            if (iAmount == 0)
                return num1;
            List<Item> list = Facepunch.Pool.GetList<Item>();
            foreach (Item obj in player.inventory.containerMain.itemList)
            {
                if (obj.info.itemid == itemid && obj.skin == skin)
                {
                    int num2 = iAmount - num1;
                    if (num2 > 0)
                    {
                        if (obj.amount > num2)
                        {
                            obj.MarkDirty();
                            obj.amount -= num2;
                            num1 += num2;
                            Item byItemId = ItemManager.CreateByItemID(itemid);
                            byItemId.amount = num2;
                            byItemId.CollectedForCrafting(player);
                            break;
                        }
                        if (obj.amount <= num2)
                        {
                            num1 += obj.amount;
                            list.Add(obj);
                        }
                        if (num1 == iAmount)
                            break;
                    }
                }
            }

            foreach (Item obj in player.inventory.containerBelt.itemList)
            {
                if (obj.info.itemid == itemid && obj.skin == skin)
                {
                    int num2 = iAmount - num1;
                    if (num2 > 0)
                    {
                        if (obj.amount > num2)
                        {
                            obj.MarkDirty();
                            obj.amount -= num2;
                            num1 += num2;
                            Item byItemId = ItemManager.CreateByItemID(itemid);
                            byItemId.amount = num2;
                            byItemId.CollectedForCrafting(player);
                            break;
                        }
                        if (obj.amount <= num2)
                        {
                            num1 += obj.amount;
                            list.Add(obj);
                        }
                        if (num1 == iAmount)
                            break;
                    }
                }
            }

            foreach (Item obj in list)
                obj.RemoveFromContainer();
            Facepunch.Pool.FreeList<Item>(ref list);
            return num1;
        }

        void PrintInfo(string message) => Puts(message);
        #endregion

        #region User Interface
        void DestroyTalkScreen(BasePlayer player, bool endConversation = true)
        {
            if (PlayerConversations.ContainsKey(player.userID))
                PlayerConversations[player.userID].EndConversation(player, endConversation);
        }

        private void BuildTalkScreen(BasePlayer player, string conversationFile, string name)
        {
            DestroyTalkScreen(player, false);

            var conversation = GetConversation(conversationFile);

            if (conversation == null)
            {
                PrintError($"Invalid conversation file: {conversationFile}");
                return;
            }

            if (conversation.Count == 0)
            {
                PrintError($"There are no messages in conversation: {conversationFile}");
                return;
            }

            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = Ui.Colors.Transparent.Clear },
                RectTransform = { AnchorMin = Ui.Anchors.LowerLeft, AnchorMax = Ui.Anchors.UpperRight }
            }, Ui.Overlay.Panel, Ui.Overlay.TalkScreen.Panel);

            #region Letterbox
            container.Add(new CuiPanel
            {
                Image = { Color = Ui.Colors.Black },
                RectTransform = { AnchorMin = Ui.Anchors.UpperLeft, AnchorMax = Ui.Anchors.UpperRight, OffsetMin = "0 -100", OffsetMax = "0 0" }
            }, Ui.Overlay.TalkScreen.Panel, Ui.Overlay.TalkScreen.TopLetterbox);

            container.Add(new CuiPanel
            {
                Image = { Color = Ui.Colors.Black },
                RectTransform = { AnchorMin = Ui.Anchors.LowerLeft, AnchorMax = Ui.Anchors.LowerRight, OffsetMin = "0 0", OffsetMax = "0 100" }
            }, Ui.Overlay.TalkScreen.Panel, Ui.Overlay.TalkScreen.BottomLetterbox);
            #endregion

            #region Add Dialog
            container.Add(new CuiPanel
            {
                Image = { Color = Ui.Colors.Transparent.Black90 },
                RectTransform = { AnchorMin = Ui.Anchors.Center, AnchorMax = Ui.Anchors.Center, OffsetMin = "136 -121", OffsetMax = "435 103" }
            }, Ui.Overlay.TalkScreen.Panel, Ui.Overlay.TalkScreen.Dialog.Panel);

            // Close Button
            container.Add(new CuiButton
            {
                Text = { Text = "X", FontSize = 12, Color = Ui.Colors.Blanc, Align = TextAnchor.MiddleCenter },
                Button = { Color = Ui.Colors.DarkRed, Close = Ui.Overlay.TalkScreen.Panel },
                RectTransform = { AnchorMin = Ui.Anchors.UpperRight, AnchorMax = Ui.Anchors.UpperRight, OffsetMin = "-20 -20", OffsetMax = "0 0" }
            }, Ui.Overlay.TalkScreen.Dialog.Panel, Ui.Overlay.TalkScreen.Dialog.CloseButton);

            // Nametag
            container.Add(new CuiPanel
            {
                Image = { Color = Ui.Colors.Transparent.DimGray90 },
                RectTransform = { AnchorMin = Ui.Anchors.UpperLeft, AnchorMax = Ui.Anchors.UpperLeft, OffsetMin = "10 -30", OffsetMax = "110 -10" }
            }, Ui.Overlay.TalkScreen.Dialog.Panel, Ui.Overlay.TalkScreen.Dialog.Nametag.Panel);

            container.Add(new CuiLabel
            {
                Text = { Text = name, FontSize = 12, Align = TextAnchor.MiddleCenter, Color = Ui.Colors.Blanc },
                RectTransform = { AnchorMin = Ui.Anchors.LowerLeft, AnchorMax = Ui.Anchors.UpperRight }
            }, Ui.Overlay.TalkScreen.Dialog.Nametag.Panel, Ui.Overlay.TalkScreen.Dialog.Nametag.Text);

            // Message Panel
            container.Add(new CuiPanel
            {
                Image = { Color = Ui.Colors.Transparent.DimGray90 },
                RectTransform = { AnchorMin = Ui.Anchors.UpperLeft, AnchorMax = Ui.Anchors.UpperRight, OffsetMin = "10 -115", OffsetMax = "-10 -35" }
            }, Ui.Overlay.TalkScreen.Dialog.Panel, Ui.Overlay.TalkScreen.Dialog.Message.Panel);
            #endregion

            CuiHelper.AddUi(player, container);

            UpdateDialogue(player, conversationFile, 0);
        }

        private void UpdateDialogue(BasePlayer player, string conversationFile, uint? messageId)
        {
            if (messageId == null)
            {
                DestroyTalkScreen(player);
                return;
            }

            CuiHelper.DestroyUi(player, Ui.Overlay.TalkScreen.Dialog.Message.Text);
            CuiHelper.DestroyUi(player, Ui.Overlay.TalkScreen.Dialog.Responses.Panel);

            var conversation = GetConversation(conversationFile);

            Dialogue dialogue = null;
            if (!conversation.TryGetValue((uint)messageId, out dialogue))
            {
                PrintError($"Invalid Message ID: Message {messageId} not in {conversationFile}");
                DestroyTalkScreen(player);
                return;
            }

            var container = new CuiElementContainer();

            var fadeIn = 0.1f;
            var newMessage = dialogue.Message.Replace("playerID", player.UserIDString).Replace("$userID", player.UserIDString).Replace("$displayName", player.displayName);

            container.Add(new CuiElement
            {
                Parent = Ui.Overlay.TalkScreen.Dialog.Message.Panel,
                Name = Ui.Overlay.TalkScreen.Dialog.Message.Text,
                Components =
                {
                    new CuiTextComponent { Text = newMessage, Color = Ui.Colors.Cloudy, Align = TextAnchor.UpperLeft, FontSize = 10, FadeIn = fadeIn },
                    new CuiRectTransformComponent { AnchorMin = Ui.Anchors.LowerLeft, AnchorMax = Ui.Anchors.UpperRight, OffsetMin = "5 5", OffsetMax = "-5 -5" }
                }
            });

            CuiHelper.AddUi(player, container);

            timer.Once(fadeIn, () =>
            {
                UpdateResponses(player, conversationFile, messageId);
            });
        }

        private void UpdateResponses(BasePlayer player, string conversationFile, uint? messageId)
        {
            CuiHelper.DestroyUi(player, Ui.Overlay.TalkScreen.Dialog.Responses.Panel);

            var conversation = GetConversation(conversationFile);

            Dialogue dialogue = null;
            if (!conversation.TryGetValue((uint)messageId, out dialogue))
            {
                PrintError($"Invalid Message ID: Message {messageId} not in {conversationFile}");
                DestroyTalkScreen(player);
                return;
            }

            var responses = dialogue.Responses;

            if (responses.Count > 4)
                PrintWarning($"Message {messageId} in {conversationFile} has too many responses. Displaying first 4.");

            if (responses.Count == 0) // Close the main UI after displaying message
            {
                timer.Once(dialogue.MessageDisplayTime, () =>
                {
                    DestroyTalkScreen(player);
                });
            }
            else // Add response buttons
            {
                var container = new CuiElementContainer();

                container.Add(new CuiPanel
                {
                    Image = { Color = Ui.Colors.Transparent.Clear },
                    RectTransform = { AnchorMin = Ui.Anchors.LowerLeft, AnchorMax = Ui.Anchors.UpperRight, OffsetMin = "10 10", OffsetMax = "-10 -120" }
                }, Ui.Overlay.TalkScreen.Dialog.Panel, Ui.Overlay.TalkScreen.Dialog.Responses.Panel);

                var responseIndex = 0;
                var displayIndex = 0u;
                foreach (var response in responses.GetRange(0, responses.Count > 4 ? 4 : responses.Count))
                {
                    if (string.IsNullOrEmpty(response.Permission) || permission.UserHasPermission(player.UserIDString, response.Permission))
                    {
                        DateTime? cooldown = GetCooldown(player, conversationFile, (uint)messageId, responseIndex);
                        if (response.gCooldown != null && response.gCooldown)
                        {
                            if (PlayerConversations.ContainsKey(player.userID))
                            {
                                ulong userID = PlayerConversations[player.userID].NpcTalker.userID;
                                cooldown = GetGlobalCooldown(userID, conversationFile, (uint)messageId, responseIndex);
                            }
                        }
                        
                        AddResponse(player, container, displayIndex, response.Message, new Ui.OnResponseClickedCommand(conversationFile, (uint)messageId, responseIndex), cooldown);
                        displayIndex++;
                    }
                    responseIndex++;
                }

                CuiHelper.AddUi(player, container);
            }
        }

        private void AddResponse(BasePlayer player, CuiElementContainer container, uint displayIndex, string response, Ui.OnResponseClickedCommand command, DateTime? cooldown = null)
        {
            string cooldownTime = "";
            var spacing = 5;
            var top = -20 * displayIndex - spacing * displayIndex;
            var bottom = -20 + (-20 * displayIndex) - (spacing * displayIndex);

            var cooldownActive = cooldown != null && cooldown > DateTime.Now;
            if (cooldown != null)
                cooldownTime = FormatTime((cooldown.Value - DateTime.Now));

            var responsePanel = $"{Ui.Overlay.TalkScreen.Dialog.Responses.Response.Panel}_{displayIndex}";

            container.Add(new CuiPanel
            {
                Image = { Color = Ui.Colors.Karaka },
                RectTransform = { AnchorMin = Ui.Anchors.UpperLeft, AnchorMax = Ui.Anchors.UpperRight, OffsetMin = $"0 {bottom}", OffsetMax = $"0 {top}" }
            }, Ui.Overlay.TalkScreen.Dialog.Responses.Panel, responsePanel);

            var numberPanel = $"{Ui.Overlay.TalkScreen.Dialog.Responses.Response.Number.Panel}_{displayIndex}";

            container.Add(new CuiPanel
            {
                Image = { Color = cooldownActive ? Ui.Colors.DarkRed : Ui.Colors.LightGreen },
                RectTransform = { AnchorMin = Ui.Anchors.LowerLeft, AnchorMax = Ui.Anchors.UpperLeft, OffsetMin = $"5 3", OffsetMax = $"20 -3" }
            }, responsePanel, numberPanel);

            container.Add(new CuiLabel
            {
                Text = { Text = $"{displayIndex + 1}", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = Ui.Colors.Blanc },
                RectTransform = { AnchorMin = Ui.Anchors.LowerLeft, AnchorMax = Ui.Anchors.UpperRight }
            }, numberPanel, Ui.Overlay.TalkScreen.Dialog.Responses.Response.Number.Text);

            var newMessage = response.Replace("playerID", player.UserIDString).Replace("$userID", player.UserIDString).Replace("$displayName", player.displayName);

            container.Add(new CuiLabel
            {
                Text = { Text = $"{newMessage}{(cooldownActive ? $" (Cooldown: {cooldownTime})" : string.Empty)}", FontSize = 10, Align = TextAnchor.MiddleLeft, Color = Ui.Colors.Cloudy },
                RectTransform = { AnchorMin = Ui.Anchors.LowerLeft, AnchorMax = Ui.Anchors.UpperRight, OffsetMin = $"25 0", OffsetMax = $"-5 0" }
            }, responsePanel, Ui.Overlay.TalkScreen.Dialog.Responses.Response.Text);

            if (!cooldownActive)
            {
                container.Add(new CuiButton
                {
                    Text = { Text = "" },
                    Button = { Color = Ui.Colors.Transparent.Clear, Command = command.ToString() },
                    RectTransform = { AnchorMin = Ui.Anchors.LowerLeft, AnchorMax = Ui.Anchors.UpperRight }
                }, responsePanel, Ui.Overlay.TalkScreen.Dialog.Responses.Response.Button);
            }
        }
        #endregion
    }
}