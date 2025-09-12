﻿﻿﻿using System;
using System.Collections;
using System.Collections.Generic;
using Oxide.Core.Plugins;
using UnityEngine;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
  using JetBrains.Annotations;
  using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using ProtoBuf;
using Rust;
using UnityEngine.Networking;
using WebSocketSharp;

namespace Oxide.Plugins
{
    [Info("RaidAlert", "Hougan", "0.0.1")]
    public class RaidAlert : RustPlugin
    {
        #region Classes

        private class Result
        {
            public int id;
            public string code;
            public string vk;
            public string telegram;
            public string userId;
            [CanBeNull] public string name;
            public bool sub; 
            public string discord;
            public int mode;
        }

        private class Configuration
        {
            public string GroupToken = "568d6fcbfdd8d5b6dd5f633d7ef0de7910d48dbe6adb168bf97f48e6cbbd85c71f9b051a8edbb414b9d5d";
            public string ServerName = "GREEN"; 
            public string NotTranslatedObject = "неизвестный объект";

            [JsonProperty("Оповещения о начале рейда (%OBJECT%, %INITIATOR%, %SQUARE%, %SERVER%)")]
            public List<string> StartRaidMessages = new List<string>();
            [JsonProperty("Оповещения об окончании рейда через разрушение шкафа (%PLAYER%)")]
            public List<string> EndCupboard = new List<string>();
            [JsonProperty("Оповещения об окончании рейда через окончание времени")]
            public List<string> EndTime = new List<string>();
            [JsonProperty("Оповещения об убийстве, когда игрок не в сети")]
            public List<string> KillMessage = new List<string>();

            public Dictionary<BuildingGrade.Enum, string> Tiers = new Dictionary<BuildingGrade.Enum, string>();
            public Dictionary<string, string> Translations = new Dictionary<string, string>();

            public static Configuration Generate()
            {
                return new Configuration
                {
                    Tiers = new Dictionary<BuildingGrade.Enum, string>
                    {
                        [BuildingGrade.Enum.Twigs] = "солом.",
                        [BuildingGrade.Enum.Wood] = "дерев.",
                        [BuildingGrade.Enum.Stone] = "кам.",
                        [BuildingGrade.Enum.Metal] = "метал.",
                        [BuildingGrade.Enum.TopTier] = "мвк.",
                    }, 
                    EndCupboard = new List<string> 
                    {
                        "Судя по всему вас зарейдили, потому что ваш шкаф разрушили!"
                    },
                    EndTime = new List<string>
                    {
                        "Судя по всему вас прекратили рейдить посмотрим что будет дальше"
                    },
                    StartRaidMessages = new List<string>
                    {
                        "Игрок %INITIATOR% начал разрушать вашу постройку, сломав %OBJECT%, на %SERVER% в квадрате [%SQUARE%]"
                    },
                    KillMessage = new List<string>
                    {
                        "Вас убили пока вы сп(р)али где то в очке [%SQUARE%] - убил вас человек %KILLER%"  
                    },
                    Translations = new Dictionary<string, string>
                    {
                        ["assets/prefabs/building/wall.frame.garagedoor/wall.frame.garagedoor.prefab"] = "гаражная дверь",
                        ["assets/prefabs/building/door.hinged/door.hinged.toptier.prefab"] = "бронированная дверь",
                        ["assets/prefabs/building/door.double.hinged/door.double.hinged.metal.prefab"] = "двойная металлическая дверь",
                        ["assets/prefabs/building/door.double.hinged/door.double.hinged.toptier.prefab"] = "двойная бронированная дверь",
                        ["assets/prefabs/building/door.double.hinged/door.double.hinged.wood.prefab"] = "двойная деревянная дверь",
                        ["assets/prefabs/building/wall.frame.fence/wall.frame.fence.gate.prefab"] = "сетчатая дверь",
                        ["assets/prefabs/building/wall.frame.shopfront/wall.frame.shopfront.metal.prefab"] = "металлическая витрина",
                        ["assets/prefabs/building/wall.frame.cell/wall.frame.cell.gate.prefab"] = "тюремная дверь",
                        ["assets/prefabs/building/wall.frame.cell/wall.frame.cell.prefab"] = "тюремная решётка",
                        ["assets/prefabs/building/wall.frame.fence/wall.frame.fence.prefab"] = "сетчатый забор",
                        ["assets/prefabs/building/wall.frame.shopfront/wall.frame.shopfront.prefab"] = "деревянная витрина", 
                        ["assets/prefabs/building/wall.window.bars/wall.window.bars.wood.prefab"] = "деревянные решётки",
                        ["assets/prefabs/building/wall.window.bars/wall.window.bars.metal.prefab"] = "металлические оконные решётки",
                        ["assets/prefabs/building/wall.window.embrasure/shutter.metal.embrasure.b.prefab"] = "металлическая вертикальная бойница",
                        ["assets/prefabs/building/wall.window.embrasure/shutter.metal.embrasure.a.prefab"] = "металлическая горизонтальная бойница",
                        ["assets/prefabs/building/wall.window.bars/wall.window.bars.toptier.prefab"] = "укреплённые оконные решётки",
                        ["assets/prefabs/building/wall.window.reinforcedglass/wall.window.glass.reinforced.prefab"] = "окно из укреплённого стекла",
                        ["assets/prefabs/deployable/tool cupboard/cupboard.tool.deployed.prefab"] = "шкаф",
                        ["assets/prefabs/deployable/tier 1 workbench/workbench1.deployed.prefab"] = "верстак первого уровня",
                        ["assets/prefabs/deployable/tier 2 workbench/workbench2.deployed.prefab"] = "верстак второго уровня",
                        ["assets/prefabs/deployable/tier 3 workbench/workbench3.deployed.prefab"] = "верстак третьего уровня",
                        ["assets/prefabs/building/door.hinged/door.hinged.wood.prefab"] = "деревянная дверь",
                        ["assets/prefabs/building/door.hinged/door.hinged.metal.prefab"] = "металлическая дверь",
                        ["assets/prefabs/building core/foundation.triangle/foundation.triangle.prefab"] = "треугольный фундамент",
                        ["assets/prefabs/building core/foundation/foundation.prefab"] = "фундамент",
                        ["assets/prefabs/building core/stairs.u/block.stair.ushape.prefab"] = "лестница",
                        ["assets/prefabs/building core/roof/roof.prefab"] = "крыша",
                        ["assets/prefabs/building core/wall.window/wall.window.prefab"] = "стена",
                        ["assets/prefabs/building/floor.ladder.hatch/floor.ladder.hatch.prefab"] = "лестничный люк",
                        ["assets/prefabs/building core/floor.frame/floor.frame.prefab"] = "потолочный каркас",
                        ["assets/prefabs/building core/wall.low/wall.low.prefab"] = "низкая стена",
                        ["assets/prefabs/building core/wall.half/wall.half.prefab"] = "полу-стенка",
                        ["assets/prefabs/building core/wall.frame/wall.frame.prefab"] = "настенный каркас",
                        ["assets/prefabs/deployable/playerioents/generators/solar_panels_roof/solarpanel.large.deployed.prefab"] = "солнечная панель",
                        ["assets/prefabs/npc/autoturret/autoturret_deployed.prefab"] = "автоматическая турель",
                        ["assets/prefabs/deployable/windmill/windmillsmall/electric.windmill.small.prefab"] = "мельница",
                        ["assets/prefabs/building/gates.external.high/gates.external.high.wood/gates.external.high.wood.prefab"] = "высокие деревянные ворота",
                        ["assets/prefabs/building/wall.external.high.wood/wall.external.high.wood.prefab"] = "высокий деревянный забор",
                        ["assets/prefabs/building/wall.external.high.stone/wall.external.high.stone.prefab"] = "высокий каменный забор",
                        ["assets/prefabs/building/gates.external.high/gates.external.high.stone/gates.external.high.stone.prefab"] = "высокие каменные ворота",
                        ["assets/prefabs/building/floor.grill/floor.grill.prefab"] = "настил",
                        ["assets/prefabs/building core/floor.triangle/floor.triangle.prefab"] = "треугольный потолок",
                        ["assets/prefabs/building core/floor/floor.prefab"] = "потолок",
                        ["assets/prefabs/deployable/barricades/barricade.metal.prefab"] = "металлическая баррикада"
                    }
                }; 
            }
        }
        
        /*private class DataBase 
        { 
            public List<uint> NotifyCupboard = new List<uint>();  

            public static DataBase LoadData()
            {
                if (Interface.Oxide.DataFileSystem.ExistsDatafile("RaidDataBase"))
                    return Interface.Oxide.DataFileSystem.ReadObject<DataBase>("RaidDataBase");
                
                return new DataBase();
            }

            public void SaveData() => Interface.Oxide.DataFileSystem.WriteObject("RaidDataBase", this);
        }*/

        private class Raid : MonoBehaviour
        {
            //public byte[] Image;
            //public string UploadURL;
            public string Attachment;
            public int EndTimer = 300;
            //public int MessageTimer = 0;

            public BaseEntity Object;
            public BuildingPrivlidge Cupboard;

            public List<ulong> StartList = new List<ulong>();
            public List<string> DestroyedObjects = new List<string>();

            public void Awake()
            {
                Object = GetComponent<BaseEntity>(); 
                Cupboard = GetComponent<BuildingPrivlidge>();
                StartList = Cupboard.authorizedPlayers.Select(p => p.userid).ToList();
                
                //Image = GetPreparedImage(transform.position);
                /*CallVKMethod("photos.getMessagesUploadServer", new Dictionary<string, string>(), Settings.GroupToken, obj =>
                {
                    UploadURL = obj["response"]["upload_url"].ToString();
                    StartCoroutine(PrepareAttachment());
                });*/
            }
 
            public IEnumerator Initialize(BasePlayer player, string niceName)
            {
                int tries = 0;
                foreach (var check in StartList) 
                {
                    var target = BasePlayer.FindByID(check); 
                    if (target != null && target.IsConnected)
                    {
                        try
                        {
                            if (Vector3.Distance(target.transform.position, Object.transform.position) > 50f)
                                Interface.Oxide.CallHook("AddNotification", target, $"ВАС НАЧАЛИ РЕЙДИТЬ", "НАЖМИТЕ ЧТОБЫ УВЕДОМЛЕНИЕ ПРОПАЛО", "1 0 0 0.5", 60, "assets/prefabs/tools/pager/effects/beep.prefab", RaidImageID);

                        }
                        catch 
                        {
                            _.PrintError($"Failed to show AddNotification for {target.userID} on Initialize");
                        }
                        //continue;
                    }
                } 
                
                /*while (Attachment.IsNullOrEmpty())
                {
                    tries++;
                    if (tries >= 100)
                    {
                        _.PrintWarning("Can not generate, sorry");
                        Attachment = "JOPA";
                        Destroy(this);
                        yield return 0; 
                    }
                    
                    yield return new WaitForSeconds(0.1f);
                } */
                 
                // Sugar
                yield return new WaitForSeconds(0.1f);
                
                foreach (var check in StartList)
                {
                    var target = BasePlayer.FindByID(check);
                    if (target != null && target.IsConnected)
                    {
                        //continue;
                    }

                    // TODO: 
                    try
                    {
                        _.SendMessage(check, Settings.StartRaidMessages.GetRandom().Replace("%INITIATOR%", player.displayName).Replace("%OBJECT%", niceName).Replace("%SERVER%", Settings.ServerName).Replace("%SQUARE%", _.GridReference(Cupboard.transform.position)), Attachment);
                    }
                    catch(Exception e)
                    {
                        //_.PrintError($"Failed to send message to {check} on InitializeRaid");
                        //_.LogToFile("ErrorInitialize", $"Failed to send {check} IR, stacktrace: {e.StackTrace}", _);
                    }
                } 
                InvokeRepeating(nameof(ControlUpdate), 1, 1); 
            }

            public void ControlUpdate()
            { 
                EndTimer--;
                //MessageTimer++;
                 
                if (EndTimer == 0)
                {
                    StopAlert();
                }

                /*if (MessageTimer == 180)
                {
                    MessageTimer = 0;
                    SendStatus();
                }*/
            }

            public void AddDestroy(BaseEntity entity)
            {
                EndTimer = 300;
                
                var niceName = Settings.Translations.ContainsKey(entity.PrefabName) ? Settings.Translations[entity.PrefabName] : Settings.NotTranslatedObject;
                if (entity is BuildingBlock)
                    niceName = $"{Settings.Tiers[((BuildingBlock) entity).grade]} {niceName}";
                
                DestroyedObjects.Add(niceName);
            }

            public void ClearCup()
            {
                if (DestroyedObjects.Count > 0)
                { 
                    //SendStatus();
                    //return;
                }
                
                foreach (var check in StartList)
                {
                    var target = BasePlayer.FindByID(check);
                    if (target != null && target.IsConnected)
                    {
                        //continue;
                    }
                     
                    _.SendMessage(check, Settings.EndCupboard.GetRandom(), string.Empty);
                } 
                
                StartList.Clear();
            }
  
            public void StopAlert(bool cup = false)
            {
                if (DestroyedObjects.Count > 0)
                {
                    //SendStatus();
                    //return;
                }
                
                foreach (var check in StartList)
                {
                    var target = BasePlayer.FindByID(check);
                    if (target != null && target.IsConnected)
                    {
                        //continue;
                    }
                     
                    _.SendMessage(check, cup ? Settings.EndCupboard.GetRandom() : Settings.EndTime.GetRandom(), string.Empty);
                } 
                Destroy(this); 
            }

            /*public void SendStatus()
            { 
                string result = DestroyedObjects.Count == 0 ? "За последние три минуты ничего не произошло!" : "За последние три минуты уничтожено:";
                foreach (var check in DestroyedObjects)
                    result += $"\n - {check}"; 

                DestroyedObjects.Clear();
                
                foreach (var check in StartList)
                {
                    var target = BasePlayer.FindByID(check);
                    if (target != null && target.IsConnected)
                    {
                        //target.ChatMessage($"Ваша постройка была <color=orange>разрушена</color>");
                        //Interface.Oxide.CallHook("API_DrawNotification", target, $"ВАША ПОСТРОЙКА БЫЛА РАЗРУШЕНА", "", "", 10, true);
                        //continue;
                    }
                     
                    _.SendMessage(check, result, string.Empty);
                }
            }*/

            /*public IEnumerator PrepareAttachment()
            {
                WWWForm postForm = new WWWForm();
                postForm.AddBinaryData("file", Image);
                using (WWW www = new WWW(UploadURL, postForm))
                {
                    yield return www;

                    var uploadResponse = www.text;
                    if (uploadResponse.Contains("500 Internal Server Error"))
                    {
                        _.Puts("UploadServer: 500 Internal Server Error");
                        yield break;
                    }

                    JObject uploadObj = JObject.Parse(www.text);

                    string server = uploadObj["server"].ToString();
                    string photo  = uploadObj["photo"].ToString();
                    string hash   = uploadObj["hash"].ToString();
                    var saveArgs = new Dictionary<string, string>()
                    { 
                        {"server", server},
                        {"photo", photo},
                        {"hash", hash}
                    };
                    CallVKMethod("photos.saveMessagesPhoto", saveArgs, Settings.GroupToken, obj =>
                    {
                        var    photoObj   = obj["response"][0];
                        string photoId    = photoObj["id"].ToString();
                        string sender     = photoObj["owner_id"].ToString();
                        string attachment = $"photo{sender}_{photoId}";

                        Attachment = attachment;
                    });
                }
            }*/
 
            public void OnDestroy()
            {
                StopAllCoroutines();
            }
        }
        
        #endregion
 
        #region Variables

        //[PluginReference] private Plugin MapGenerator;

        private static RaidAlert _;
        //private static byte[] RaidImageBytes; 
        //private static DataBase Base = DataBase.LoadData();
        private static Configuration Settings = Configuration.Generate();
        private static string RaidImageID = "";
 
        #endregion

        #region Command
 
        [ConsoleCommand("unverify")]
        private void CmdChatUnverify(ConsoleSystem.Arg args)
        {
            webrequest.Enqueue($"http://api.hougan.space/blood/removeCode/{args.Player().userID}/2281337ABC", "", (i, s) =>
            {  
                PrintWarning("Success");
                args.Player().SendConsoleCommand("chat.say /vk");
            }, this); 
        }

        #endregion

        #region Interface

        private static string Layer = "UI_RaidAlert";
        private void InitializeCode(BasePlayer player, string code, string name = "")  
        {
            CuiHelper.DestroyUi(player, Layer);
            CuiElementContainer container = new CuiElementContainer();
            
            container.Add(new CuiPanel()
            {
                CursorEnabled = true,
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0"},
                Image         = {Color     = "0 0 0 0"}
            }, "Overlay", Layer);

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax    = "1 1", OffsetMax                                       = "0 0"},
                Button        = {Color     = "0 0 0 1", Material = "assets/content/ui/uibackgroundblur-notice.mat", Close = Layer, Command = "closemenu"},
                Text          = {Text      = ""}
            }, Layer);

            bool isConnected = !name.IsNullOrEmpty();
            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax              = $"0 {(!isConnected ? 100 : 130)}"},
                Text          = {Text      = isConnected ? name : code, Align      = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", Color = "1 1 1 0.9", FontSize = 140}
            }, Layer);

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax              = "0 -50"},
                Text          = {Text      = isConnected ? "ЭТО ВАШ АККАУНТ" : "ВАШ КОД ПОДТВЕРЖДЕНИЯ", Align      = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", Color = "1 1 1 0.5", FontSize = 36 }
            }, Layer);

            if (!isConnected)
            {
                container.Add(new CuiLabel
                {
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 -350"},
                    Text = { Text = "Отправьте этот код одним сообщением в нашу группу ВК\n" + "<b>HTTPS://VK.COM/BLOODRUST</b>", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", Color = "1 1 1 0.5", FontSize = 24 }
                }, Layer);
            }
            else
            {
                container.Add(new CuiLabel
                {
                    RectTransform = {AnchorMin = "0 0", AnchorMax                                                                                    = "1 1", OffsetMax              = "0 -350"},
                    Text          = { Text     = "Теперь вам будут приходить оповещения о рейде в ЛС\n" + "<b>НЕ ЗАПРЕЩАЙТЕ СООБЩЕНИЯ ОТ СООБЩЕСТВА</b>", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", Color = "1 1 1 0.5", FontSize = 24 }
                }, Layer);
            }
 
            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"-200 -120", OffsetMax = "200 -70"},
                Button        = {Color     = "1 1 1 0.1"},
                Text          = {Text      = ""}
            }, Layer, Layer + ".H");
 
            if (!isConnected)
            {
                container.Add(new CuiButton
                {
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"3 3", OffsetMax = "-3 -3"},
                    Button        = {Color     = "1 0.6 0.6 0.7"},
                    Text          = {Text      = "НЕ ПОДТВЕРЖДЕНО", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 26, Color = "1 1 1 0.2"}
                }, Layer + ".H", Layer + ".HH");
            }
            else
            {
                container.Add(new CuiButton
                {
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"3 3", OffsetMax = "-3 -3"},
                    Button        = {FadeIn    = 1f, Color        = "0.6 1 0.6 0.7"},
                    Text          = {FadeIn    = 1f, Text         = "ПОДТВЕРЖДЕНО", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 26, Color = "1 1 1 0.4"}
                }, Layer + ".H", Layer + ".HH");
            }
            
            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax    = "1 1", OffsetMax                                       = "0 0"},
                Button        = {Color     = "0 0 0 0", Material = "assets/content/ui/uibackgroundblur-notice.mat", Close = Layer, Command = "closemenu"},
                Text          = {Text      = ""}
            }, Layer);

            if (isConnected)
            {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0.1", AnchorMax = "1 0.2", OffsetMax = "0 0" },
                    Button = { Color = "0 0 0 0", Command = "unverify" },  
                    Text = { Text = "<b>X</b> ОТВЯЗАТЬ АККАУНТ <b>X</b>", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 18, Color = "1 1 1 0.2"}
                }, Layer);
            }

            CuiHelper.AddUi(player, container);
        }        

        #endregion
        
        #region Interface

        [ChatCommand("alert")]
        private void CmdConsoleAlert(BasePlayer player)
        {
            player.SendConsoleCommand("chat.say /menuSecret");
            player.SendConsoleCommand("UI_Settings_Handler ОПОВЕЩЕНИЕ_О_РЕЙДЕ");
            player.SendConsoleCommand("UI_RM_Handler choose 8");        
        }

        [ChatCommand("alertSecret")]
        private void CmdConsoleAlertSecret(BasePlayer player)
        {
            webrequest.Enqueue($"http://185.200.242.130:2008/raid/getCode/{player.userID}/2281337ABC", "", (i, s) =>
            {
                try
                {
                    var result = JsonConvert.DeserializeObject<Result>(s.ToString());

                    //if (!result.id.IsNullOrEmpty())
                    //{=
                        InitializeInterface(player, result.code, result.sub, result.mode, result.vk.Length == 0 ? "-" : result.vk, result.discord.Length == 0 ? "-" : result.discord, result.telegram, result.name ?? "NULL");
                    /*}
                    else
                    { 
                        PrintError(player.userID.ToString());
                        PrintError("Webrequest error occurred");
                    }*/ 
                }
                catch
                {
                    PrintError(player.userID.ToString());
                    PrintWarning(s.ToString());
                    PrintError("Webrequest error occurred");
                }
            }, this); 
            //InitializeInterface(player, "2281337", false, 0, "", "");
        }

        [ConsoleCommand("UI_RaidAlert_Handler")]
        private void CmdConsoleHandler(ConsoleSystem.Arg args)
        {
            var player = args.Player();
            if (!player || !args.HasArgs(1)) return;

            string code = args.Args[2];
            bool @switch = Convert.ToBoolean(args.Args[3]);
            string vk = args.Args[4];
            string discord = args.Args[5];
            
            switch (args.Args[0].ToLower())
            {
                case "mode":
                { 
                    //InitializeInterface(player, code, @switch, int.Parse(args.Args[1]), vk, discord); 
                    webrequest.Enqueue($"http://185.200.242.130:2008/raid/setMode/{player.userID}/{int.Parse(args.Args[1])}/2281337ABC", "",
                        (i, s) =>
                        {
                            player.SendConsoleCommand("chat.say /alertSecret");
                        }, this);
                    
                    
                    break;
                }
                case "switch":
                { 
                    //InitializeInterface(player, code, @switch, int.Parse(args.Args[1]), vk, discord);  
                    webrequest.Enqueue($"http://185.200.242.130:2008/raid/setEnable/{player.userID}/{@switch}/2281337ABC", "",
                        (i, s) =>
                        {
                            player.SendConsoleCommand("chat.say /alertSecret");
                        }, this);
                    
                    
                    break;
                }
            }
        }
        
        private string SettingsLayer = "UI_SettingsLayer";
        private void InitializeInterface(BasePlayer player, string code, bool switchStatus, int mode, string vk, string discord, string telegram, string name = "")
        {
            //var settings = Handler.Settingses[player.userID];
            
            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, SettingsLayer + ".RP");
            
            container.Add(new CuiPanel()
            { 
                CursorEnabled = true,
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"},  
                Image         = {Color = "0 0 0 0" }
            }, SettingsLayer + ".R", SettingsLayer + ".RP");
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"30 -100", OffsetMax = $"0 -30"} ,
                Text = { Text = "ОПОВЕЩЕНИЕ", Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf", FontSize = 48, Color = "0.93 0.89 0.85 1"}
            }, SettingsLayer + ".RP");
             
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 0", OffsetMax = "0 125" },
                Image = { Color = "0.29411 0.27450 0.254901 1" }
            }, SettingsLayer + ".R", SettingsLayer + ".Info");
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"28 -100", OffsetMax = $"-30 -5"} ,
                Text = { Text = "ИНФОРМАЦИЯ", Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf", FontSize = 48, Color = "0.93 0.89 0.85 1"}
            }, SettingsLayer + ".Info");

            string helpText = "Подключив оповещение о рейде тебе автоматически будет сообщаться о рейде твоего дома в личные сообщения соц. сети которую выберешь выше в настройках. Уведомления приходят при разрушении строительных объектов деревянного тира и выше, а также некоторых других важных объектов (двери, высокие каменные стены и т.д.).";
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "30 0", OffsetMax = "0 -60" },
                Text = { Text = helpText, Align = TextAnchor.UpperLeft, Font = "robotocondensed-regular.ttf", FontSize = 12, Color = "0.67 0.63 0.596"}
            }, SettingsLayer + ".Info");
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = $"0 -80", OffsetMax = $"500 -15"} ,
                Text = { Text = "НАСТРОЙКИ", Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf", FontSize = 48, Color = "0.93 0.89 0.85 1"}
            }, SettingsLayer + ".Info");   
            
            
            #region Bool layer Подсказки
             
            var mainCommand = $"UI_RaidAlert_Handler switch {mode} {code} {!switchStatus} {switchStatus} {vk} {discord}"; 
            
            var guid = CuiHelper.GetGuid(); 
             
            string leftCommand = $"UI_RaidAlert_Handler mode {mode - 1} {code} {switchStatus} {vk} {discord}"; 
            string rightCommand = $"UI_RaidAlert_Handler mode {mode + 1} {code} {switchStatus} {vk} {discord}"; 
            
            vk = vk.Replace("-", "");
            discord = discord.Replace("-", "");
            telegram = telegram.Replace("-", ""); 
            
            bool isActive = (vk.Length != 0 && mode == 1) || (discord.Length != 0 && mode == 2) || (telegram.Length != 0 && mode == 3);
            if (switchStatus && !isActive) switchStatus = false;
            container.Add(new CuiLabel 
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"30 -195", OffsetMax = $"-10 -85" },
                Text = { Text = "ОПОВЕЩЕНИЕ", Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf", FontSize = 28, Color = "0.81 0.77 0.74 0.6"}
            }, SettingsLayer + ".RP", guid);

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = $"-250 -30", OffsetMax = $"-30 0"},
                Image = {Color = switchStatus ? "0.23 0.22 0.17 0.8" : "0.23 0.22 0.17 0.5", Material = ""}
            }, guid, guid + ".P");
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"30 0", OffsetMax = $"-30 0" },  
                Text = { Text = switchStatus ? "ВКЛ" : "ВЫКЛ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 18, Color = "0.81 0.77 0.74 0.6"}
            }, guid + ".P");
            
            container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Button = { Color = "0 0 0 0", Command = mainCommand },
                    Text = { Text = "", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter}
                }, guid + ".P");    

            #endregion 
            
            #region Switch layer Цвет

            var possibleStatuses = new Dictionary<int, string>
            {
                [0] = "НЕ ВЫБРАНО",
                [1] = "ВКонтакте",
                [2] = "Discord",
                [3] = "Telegram",
            };
            
            var currentStatus = possibleStatuses[mode];

            bool leftActive = mode > 0;
            bool rightActive = mode < 3; 
            
            guid = CuiHelper.GetGuid(); 
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"30 -170", OffsetMax = $"-10 -120" }, 
                Text = { Text = "КУДА БУДЕТ ПРИХОДИТЬ", Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf", FontSize = 28, Color = "0.81 0.77 0.74 0.6"}
            }, SettingsLayer + ".RP", guid);

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = $"-250 -30", OffsetMax = $"-30 0"},
                Image = {Color = "0.23 0.22 0.17 0.8", Material = ""}
            }, guid, guid + ".P"); 
            
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0 1", OffsetMax = "30 0" },
                Image = { Color = leftActive ? "0.81 0.77 0.74 0.2" : "0.81 0.77 0.74 0.15" }
            }, guid + ".P", guid + ".L"); 
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Command = leftActive ? leftCommand : "" },
                Text = { Text = "<b><</b>", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = leftActive ? "0.8 0.8 0.8 1" : "0.8 0.8 0.8 0.2" }
            }, guid + ".L");
            
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "1 0", AnchorMax = "1 1", OffsetMin = "-30 0", OffsetMax = "0 0" },
                Image = { Color = rightActive ? "0.81 0.77 0.74 0.2" : "0.81 0.77 0.74 0.1"}
            }, guid + ".P", guid + ".R");
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Command = rightActive ? rightCommand : "" },
                Text = { Text = "<b>></b>", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = rightActive ? "0.8 0.8 0.8 1" : "0.8 0.8 0.8 0.2"}
            }, guid + ".R");
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"30 0", OffsetMax = $"-30 0" },  
                Text = { Text = currentStatus, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 18, Color = "0.81 0.77 0.74 0.6"}
            }, guid + ".P");

            #endregion 
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"30 -220", OffsetMax = $"-40 -160"} ,
                Text = { Text = "ПОДКЛЮЧЕНИЕ", Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf", FontSize = 48, Color = "0.93 0.89 0.85 1"}
            }, SettingsLayer + ".RP", SettingsLayer + ".CON");
            
            container.Add(new CuiPanel
            {     
                RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"325 -45", OffsetMax = "0 -8" },
                Image = { Color = "0.231 0.239 0.203 0.64"} 
            }, SettingsLayer + ".CON", SettingsLayer + ".PAST");
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0", OffsetMin = "10 16" },
                Text = { Text = $"ВАШ КОД: {code}", Align = TextAnchor.LowerLeft, Font = "robotocondensed-bold.ttf", Color = "0.67 0.63 0.59 1", FontSize = 16 }
            }, SettingsLayer + ".PAST");

            string activeText = "ОПОВЕЩЕНИЕ НЕ ПОДКЛЮЧЕНО";
            if (mode != 0)
            {
                if (isActive)
                { 
                    activeText = $"АККАУНТ В {(mode == 1 ? "ВК" : mode == 2 ? "ДС" : "TG")} ПОДТВЕРЖДЕН";
                }
                else
                {
                    activeText = $"АККАУНТ В {(mode == 1 ? "ВК" : mode == 2 ? "ДС" : "TG")} НЕ ПОДТВЕРЖДЕН";
                }
            }
            
            container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 -20", OffsetMin = "10 0" },
                    Text = { Text = activeText, Align = TextAnchor.UpperLeft, Font = "robotocondensed-regular.ttf", Color = "0.67 0.63 0.59 1", FontSize = 12 }
                }, SettingsLayer + ".PAST");

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = $"0 -30", OffsetMax = $"0 -5"},
                Image = {Color = mode == 0 ? "0.337 0.196 0.164 0.65" : "0.286 0.337 0.164 0.65"}
            }, SettingsLayer + ".CON", SettingsLayer + ".CONF");

            container.Add(new CuiPanel
                {
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "0 1", OffsetMin = $"0 0", OffsetMax = $"7 0"},
                    Image = {Color = mode == 0 ? "0.337 0.196 0.164 1" : "0.286 0.337 0.164 1"}
                }, SettingsLayer + ".CONF");

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "15 0", OffsetMax = "0 0"},
                Text = {Text = "Выбери выше соц. сеть в которую хочешь получать оповещения", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf", FontSize = 16, Color = $"0.81 0.77 0.74 {(mode == 0 ? "1" : "0.5")}"}
            }, SettingsLayer + ".CONF");

            if (mode > 0)
            {

                container.Add(new CuiPanel
                {
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = $"0 -30", OffsetMax = $"0 -5"},
                    Image = {Color = isActive ? "0.286 0.337 0.164 0.65" : "0.337 0.196 0.164 0.65"}
                }, SettingsLayer + ".CONF", SettingsLayer + ".CONS");

                string text = mode == 1 ? "Отправь шестизначный код в сообщения группы <color=#648bb8><b>vk.com/bloodrust</b></color>" : "Отправь код боту '<color=#c2c2c2><b>БОРОВ</b></color>' в нашем дискорде <color=#8393cd><b>discord.gg/KeYpVkv</b></color>";
                if (mode == 3)
                {
                    text = "Отправь свой шестизначный код боту в telegram <color=#578ea9><b>@bloodrust_bot</b></color>";
                }
                container.Add(new CuiLabel
                {
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "15 0", OffsetMax = "0 0"},
                    Text = {Text = text, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf", FontSize = 16, Color = $"0.81 0.77 0.74 {(!isActive ? "1" : "0.5")}"}
                }, SettingsLayer + ".CONS");

                container.Add(new CuiPanel
                {
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "0 1", OffsetMin = $"0 0", OffsetMax = $"7 0"},
                    Image = {Color = isActive ? "0.286 0.337 0.164 1" : "0.337 0.196 0.164 1"}  
                }, SettingsLayer + ".CONS");
            }

            if (isActive)
            {
                container.Add(new CuiPanel
                    {
                        RectTransform = {AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = $"0 -30", OffsetMax = $"0 -5"},
                        Image = {Color = switchStatus ? "0.286 0.337 0.164 0.65" : "0.337 0.196 0.164 0.65"}
                    }, SettingsLayer + ".CONS", SettingsLayer + ".CONT");

                string text = "Включи оповещения через специальную кнопку";
                container.Add(new CuiLabel
                    {
                        RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "15 0", OffsetMax = "0 0"},
                        Text = {Text = text, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf", FontSize = 16, Color = $"0.81 0.77 0.74 {(!switchStatus ? "1" : "0.5")}"}
                    }, SettingsLayer + ".CONT");

                container.Add(new CuiPanel
                    {
                        RectTransform = {AnchorMin = "0 0", AnchorMax = "0 1", OffsetMin = $"0 0", OffsetMax = $"7 0"},
                        Image = {Color = switchStatus ? "0.286 0.337 0.164 1" : "0.337 0.196 0.164 1"}  
                    }, SettingsLayer + ".CONT");
            }
 
            CuiHelper.AddUi(player, container);
        } 
        
        #endregion

        #region Hooks
        
        private void OnCupboardClearList(BuildingPrivlidge privilege, BasePlayer player)
        {
            var obj = privilege.GetComponent<Raid>();
            if (obj == null) return;

            if (obj.StartList.Contains(player.userID))
                return; 
             
            obj.ClearCup(); 
        }

        /*private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (!(entity is BuildingBlock) || info == null || info.damageTypes.GetMajorityDamageType() != DamageType.Decay) return;
            var obj = entity.GetBuildingPrivilege();
            if (obj == null) return;

            if (Base.NotifyCupboard.Contains(obj.net.ID))
                return; 
            
            Base.NotifyCupboard.Add(obj.net.ID);
            if (entity.OwnerID == 0) return;

            foreach (var check in obj.authorizedPlayers)
            {
                var target = BasePlayer.FindByID(check.userid);
                if (target != null && target.IsConnected) continue;
                 */
                //ServerMgr.Instance.StartCoroutine(StartDecayNotification(check.userid, entity.transform.position));
        //    }
        //}

        /*public IEnumerator StartDecayNotification(ulong targetId, Vector3 pos)
        {
            CallVKMethod("photos.getMessagesUploadServer", new Dictionary<string, string>(), Settings.GroupToken, obj =>
            {
                var url = obj["response"]["upload_url"].ToString();
                
                ServerMgr.Instance.StartCoroutine(PrepareAttachment(targetId, pos, url));
            });
            
            yield return null;
        }*/
        
        /*public IEnumerator PrepareAttachment(ulong targetId, Vector3 pos, string url) 
        {
            WWWForm postForm = new WWWForm();
            
            var image = GetPreparedImage(pos);
            
            postForm.AddBinaryData("file", image);
            using (WWW www = new WWW(url, postForm))
            {
                yield return www;

                var uploadResponse = www.text;
                if (uploadResponse.Contains("500 Internal Server Error"))
                {
                    _.Puts("UploadServer: 500 Internal Server Error");
                    yield break;
                }

                JObject uploadObj = JObject.Parse(www.text);

                string server = uploadObj["server"].ToString();
                string photo  = uploadObj["photo"].ToString();
                string hash   = uploadObj["hash"].ToString();
                var saveArgs = new Dictionary<string, string>()
                { 
                    {"server", server},
                    {"photo", photo},
                    {"hash", hash}
                };
                
                CallVKMethod("photos.saveMessagesPhoto", saveArgs, Settings.GroupToken, obj =>
                {
                    var    photoObj   = obj["response"][0];
                    string photoId    = photoObj["id"].ToString();
                    string sender     = photoObj["owner_id"].ToString();
                    string attachment = $"photo{sender}_{photoId}"; 
                    
                    _.SendMessage(targetId, "⭕ ОПОВЕЩЕНИЕ О ГНИЕНИИ ⭕\nВаша постройка начала гнить, советуем доложить ресурсы в шкаф!", attachment);
                });
            } 
        }*/ 

        private void GetVKId(ulong targetId, Action<string, int> callback)
        { 
            webrequest.Enqueue($"http://185.200.242.130:2008/raid/getCode/{targetId}/2281337ABC", "", (i, s) =>
            {
                try
                {
                    var result = JsonConvert.DeserializeObject<Result>(s.ToString());
                    if (!result.sub)
                    {
                        callback("", 0);
                    }

                    if (result.mode == 1 && !result.vk.IsNullOrEmpty())
                    {
                        callback(result.vk, 1);
                    }
                    else if (result.mode == 2 && !result.discord.IsNullOrEmpty())
                    {
                        callback(result.discord, 2);
                    }
                    else if (result.mode == 3 && !result.telegram.IsNullOrEmpty())
                    {
                        callback(result.telegram, 3);
                    }
                    else
                    {
                        callback("", 0); 
                    }
                }
                catch
                { 
                    PrintError(targetId.ToString());
                    PrintWarning(s.ToString());
                    PrintError("Webrequest error occurred");
                }
            }, this); 
        }

        private void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (player.IsConnected || info == null || player.userID < 76561100000) return; 

            if (info.InitiatorPlayer == null || info?.InitiatorPlayer.userID == player.userID) return; 
            
            string killerInfo = info.InitiatorPlayer == null ? "неизвестного" : info.InitiatorPlayer.displayName; 
            SendMessage(player.userID, Settings.KillMessage.GetRandom().Replace("%KILLER%", killerInfo).Replace("%SQUARE%", GridReference(player.transform.position)).Replace("%SERVER%", Settings.ServerName), ""); 
        } 

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (!CheckObject(entity))
            {
                return;
            }

            if (entity is BuildingPrivlidge)
            {
                var raid = entity.GetComponent<Raid>();
                if (raid != null)
                {
                    raid.StopAlert(true); 
                    return;
                }
                return;
            }
            
            var obj = entity.GetBuildingPrivilege(new OBB(entity.transform, entity.bounds)); 
            if (obj == null)
            {
                return;
            }

            var currentRaid = obj.GetComponent<Raid>();
            if (currentRaid != null)
            {
                currentRaid.AddDestroy(entity);
                return;
            }  
            else
            {
                var initiator = info?.InitiatorPlayer;
                if (initiator == null || initiator.IsBuildingAuthed())
                {
                    return;
                }
                
                var niceName = Settings.Translations.ContainsKey(entity.PrefabName) ? Settings.Translations[entity.PrefabName] : Settings.NotTranslatedObject;
                if (entity is BuildingBlock) 
                    niceName = $"{Settings.Tiers[((BuildingBlock) entity).grade]} {niceName}";

                if (entity is BuildingBlock && ((BuildingBlock) entity).grade == BuildingGrade.Enum.Twigs) return;
                
                ServerMgr.Instance.StartCoroutine(obj.gameObject.AddComponent<Raid>().Initialize(info.InitiatorPlayer, niceName));
            } 
        }

        #endregion

        #region Methods
        
        private void OnServerInitialized()
        {
            _ = this; 

            permission.RegisterPermission("raidalert.perm", this);
            plugins.Find("ImageLibrary").Call("AddImage", "https://i.imgur.com/iQwvDWa.png", "RaidImageNotification");
            //ServerMgr.Instance.StartCoroutine(DownloadImage("https://i.imgur.com/zQV4eqL.png"));
            //DecaySettings();
            
            //FetchImage();
        }

        private void FetchImage()
        {
            if ((bool) plugins.Find("ImageLibrary").Call("HasImage", "RaidImageNotification"))
            {
                RaidImageID = (string) plugins.Find("ImageLibrary").Call("GetImage", "RaidImageNotification");
                PrintWarning("Image is loaded OK!");
            }
            else
            {
                timer.Once(1, FetchImage);
            }
        }
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                Settings = Config.ReadObject<Configuration>();
            }
            catch
            {
                PrintWarning($"Error reading config, creating one new config!");
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig() => Settings = Configuration.Generate();
        protected override void SaveConfig()        => Config.WriteObject(Settings);

        /*private void DecaySettings()
        {
            var dirty = new List<uint>();

            foreach (var check in Base.NotifyCupboard)
            {
                var ent = BaseNetworkable.serverEntities.Find(check);
                if (ent == null)
                {
                    dirty.Add(check);
                    continue;
                }

                var obj = ent.GetComponent<BuildingPrivlidge>(); 
                if (obj == null)
                {
                    dirty.Add(check); 
                    continue;
                }
                 
                if (obj.GetProtectedMinutes() > 60 * 2) 
                {
                    dirty.Add(check);
                }
            }

            foreach (var check in dirty)
            {
                Base.NotifyCupboard.Remove(check); 
            }
            
            timer.Once(5, DecaySettings);
        }*/
        
        private void Unload()
        {  
            //Base.SaveData();
            UnityEngine.Object.FindObjectsOfType<Raid>().ToList().ForEach(UnityEngine.Object.Destroy); 
        }

        #endregion
 
        #region VK Work 
        private string URLEncode(string input)
        {
            while (input.Contains("#")) input = input.Replace("#", "%23");
            while (input.Contains("$")) input = input.Replace("$", "%24");
            while (input.Contains("+")) input = input.Replace("+", "%2B");
            while (input.Contains("/")) input = input.Replace("/", "%2F");
            while (input.Contains("\\")) input = input.Replace("\\", "%23");
            while (input.Contains(":")) input = input.Replace(":", "%3A");
            while (input.Contains(";")) input = input.Replace(";", "%3B"); 
            while (input.Contains("?")) input = input.Replace("?", "%3F");
            while (input.Contains("@")) input = input.Replace("@", "%40");
            
            return input;
        }
         
        private void SendMessage(ulong targetId, string message, string attachment)
        {
            message = URLEncode(message);
            _.GetVKId(targetId, (s, i) =>
            {
                if (s.IsNullOrEmpty())
                {
                    //PrintError($"Wrong answer for GetID for user: {targetId}, message: {message}");
                    //LogToFile("Errors", $"Wrong answer for GetID for user: {targetId}, message: {message}", this);
                    return;
                }
                  
                if (i == 3)
                {
                    webrequest.Enqueue($"http://185.200.242.130:2008/raid/telegram/sendMessage/{s}/{message}/2281337ABC", "", (code, ss) =>
                    {
                        try 
                        {  
                            LogToFile("OK", $"Send message to TS for {targetId} {message} {s}", this);
                            LogToFile("OK", $"http://185.200.242.130:2008/raid/telegram/sendMessage/{s}/{message}/2281337ABC", this);
                        }
                        catch
                        {
                            //PrintError("Webrequest error occurred TS");
                        }
                    }, this); 
                }
                else if (i == 2)
                {
                    webrequest.Enqueue($"http://185.200.242.130:2008/raid/discord/sendMessage/{s}/{message}/2281337ABC", "", (code, ss) =>
                    {
                        try
                        {  
                            LogToFile("OK", $"Send message to DS for {targetId}", this);
                        }
                        catch
                        {
                            //PrintError("Webrequest error occurred DS");
                        }
                    }, this); 
                }
                else
                {                
                    PrintError("Send");    

                    var messageArgs = new Dictionary<string, string>()
                    {
                        {"message", URLEncode(message)}
                    };
            
                    if (!attachment.IsNullOrEmpty())
                        messageArgs["attachment"] = attachment;
             
                    messageArgs.Add("user_id",   s); 
                    messageArgs.Add("peer_id",   $"-160916989");
                    messageArgs.Add("random_id", Oxide.Core.Random.Range(0, 10000).ToString()); 
            
                    CallVKMethod("messages.send", messageArgs, Settings.GroupToken, obj =>
                    { 
                        //LogToFile("OK", $"Send message to VK for {targetId}, with resp: {JsonConvert.SerializeObject(obj)}", this);
                    });
                }
                
            });
             
        }

        #endregion

        #region Utils  
        
        private string GridReference(Vector3 pos)
        {
            int worldSize = ConVar.Server.worldsize;
            const float scale = 150f;
            float x = pos.x + worldSize/2f;
            float z = pos.z + worldSize/2f;
            var lat = (int)(x / scale);
            var latChar = (char)('A' + lat);
            var lon = (int)(worldSize/scale - z/scale);
            return $"{latChar}{lon}";
        }
        
        /*private static byte[] GetPreparedImage(Vector3 position)
        { 
            var mapImageBytes = GenerateMap();
            if (mapImageBytes == null) return null; 
            
            var mapImage    = (Bitmap) (new ImageConverter().ConvertFrom(mapImageBytes));
            var markerImage = (Bitmap) (new ImageConverter().ConvertFrom(RaidImageBytes));  

            var raidhomeViewportPos = ToScreenCoords(position); 
            var raidhomeSize        = new Vector2i(markerImage.Width, markerImage.Height);
            var raidhomePos = new Vector2i((int) (raidhomeViewportPos.x * mapImage.Width),
                mapImage.Height - (int) (raidhomeViewportPos.y * mapImage.Height) -60);
            var raidhomeMin = raidhomePos - raidhomeSize / 2;

            Bitmap                  cutPiece = new Bitmap(mapImage.Width, mapImage.Height); 
            System.Drawing.Graphics graphic  = System.Drawing.Graphics.FromImage(cutPiece); 
            graphic.DrawImage(mapImage, new Rectangle(0, 0, mapImage.Width, mapImage.Height), 0, 0, mapImage.Width,
                mapImage.Height, GraphicsUnit.Pixel);
            
            
            graphic.DrawImage(markerImage, new Rectangle(raidhomeMin.x, raidhomeMin.y, raidhomeSize.x, raidhomeSize.y), 0, 0,
                markerImage.Width, markerImage.Height, GraphicsUnit.Pixel);
            
            graphic.Dispose();
            
            MemoryStream ms = new MemoryStream();
            cutPiece.Save(ms, ImageFormat.Png);

            return ms.ToArray(); 
        }*/
        
        /*private IEnumerator DownloadImage(string url)
        { 
            UnityWebRequest www = UnityWebRequest.Get(url);

            yield return www.SendWebRequest();
            if (www.isNetworkError || www.isHttpError)
            {
                www.Dispose();
                yield break;
            }

            Texture2D texture = new Texture2D(2, 2);
            texture.LoadImage(www.downloadHandler.data);
            if (texture != null)
            {
                RaidImageBytes = texture.EncodeToPNG();
            }
            
            www.Dispose();
        }*/
         
        /*private static Vector2 ToScreenCoords(Vector3 vec)
        {
            float x = (vec.x + (int) World.Size * 0.5f) / (int) World.Size;
            float z = (vec.z + (int) World.Size * 0.5f) / (int) World.Size;
            return new Vector2(x, z);
        }

        private static byte[] GenerateMap()
        {
            var result = (byte[]) _.MapGenerator.Call("GetMapImage");
            if (result == null)
            {
                _.PrintError($"Result from MapGenerator is null!");
                return null;
            } 
            
            return result;
        }*/
        
        private static void CallVKMethod(string method, Dictionary<string, string> args, string token, Action<JObject>      callback = null)
        {
            string url = $"https://api.vk.com/method/{method}?";

            url = args.Aggregate(url, (current, arg) => current + $"{arg.Key}={arg.Value}&");

            url += $"access_token={token}&v=5.95";

            _.webrequest.EnqueueGet(url, (i, s) =>
            {
                if (s.Contains("captcha_sid"))
                {
                    _.PrintError("Detected captcha!"); 
                    return;
                }
                callback?.Invoke(JObject.Parse(s));
            }, _);
        }

        private bool CheckObject(BaseEntity entity) => entity is AutoTurret || entity is BuildingBlock || entity is BuildingPrivlidge || entity is Door || entity is SimpleBuildingBlock;
        
        #endregion
    }
}