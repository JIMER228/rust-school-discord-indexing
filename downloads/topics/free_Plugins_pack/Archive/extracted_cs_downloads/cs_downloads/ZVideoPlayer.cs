using UnityEngine;
using System.Collections.Generic;
using Oxide.Core;
using System.Linq;
using System;
using System.Drawing;
using System.IO;
using System.Drawing.Imaging;
using System.Collections;
using UnityEngine.Networking;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("ZVideoPlayer", "JOSH-Z", "1.2.0")]
    [Description("Add interactive video screens to play video tapes in game")]

    class ZVideoPlayer : RustPlugin
    {
        private static ZVideoPlayer ins;

        const string permAdmin = "zvideoplayer.admin";
        const string permNolimits = "zvideoplayer.video.nolimits";
        const string permUse = "zvideoplayer.video.use";
        const string permAddGifs = "zvideoplayer.video.add";
        const string permShop = "zvideoplayer.shop.use";
        const string permTVControl = "zvideoplayer.tv.control";
        const string permTVCreate = "zvideoplayer.tv.create";

        const string dataFileName = "ZVideoPlayer";
        const string dataVideoDir = "ZVideoPlayer";

        private GameObject downloadControllerObject;
        private DownloadController downloadController;

        private const string prefab_recorder = "assets/prefabs/voiceaudio/cassetterecorder/cassetterecorder.deployed.prefab";

        const int cassette_id = 476066818;
        const uint cassette_skin_id_full = 2553733336;
        const uint cassette_skin_id_empty = 2553733160;
        const int neonsign_id = 866332017;
        const uint neonsign_skin_id = 2545641123;


        private Dictionary<BasePlayer, int> playerUIOpened = new Dictionary<BasePlayer, int>();
        const string UIPanelName = "VideoStorePanel";
        const string green = "0.29 0.84 0.53 1";
        const string red = "0.84 0.29 0.29 1";
        const string orange = "0.84 0.45 0.29 1";
        const int videosPerPage = 18;

        #region hooks
        void Init()
        {
            if (!LoadConfigVariables())
            {
                Puts("Errors in config file, please validate the file and try again");
                return;
            }
            permission.RegisterPermission(permAdmin, this);
            permission.RegisterPermission(permUse, this);
            permission.RegisterPermission(permNolimits, this);
            permission.RegisterPermission(permShop, this);
            permission.RegisterPermission(permAddGifs, this);
            permission.RegisterPermission(permTVControl, this);
            permission.RegisterPermission(permTVCreate, this);
        }

        void OnServerInitialized()
        {
            ins = this;
            downloadControllerObject = new GameObject("DownloadController");
            downloadController = downloadControllerObject.AddComponent<DownloadController>();

            timer.Once(2f, () =>
            {
                SetupPlacedSuperSigns();
                CleanupData();
            });
        }

        void CleanupData()
        {
            Debug.Log("Cleaning up datafile");

            var removed = 0;

            var curSpawned = new List<uint>(storedData.spawnedEntities);
            foreach (var entID in curSpawned)
            {
                NetworkableId networkableId = new NetworkableId(entID);
                var ent = BaseNetworkable.serverEntities.Find(networkableId);
                if (ent == null)
                {
                    storedData.spawnedEntities.Remove(entID);
                    removed++;
                }
            }

            var curSigns = new Dictionary<uint, PlacedTV>(storedData.superSigns);
            foreach (var signInfo in curSigns)
            {
                NetworkableId networkableId = new NetworkableId((uint)signInfo.Key);
                var ent = BaseNetworkable.serverEntities.Find(networkableId);
                if (ent == null)
                {
                    storedData.superSigns.Remove(signInfo.Key);
                    removed++;
                }
            }

            var curItems = new List<uint>(storedData.superSignItems);
            foreach (var itemID in curItems)
            {
                NetworkableId networkableId = new NetworkableId(itemID);
                var ent = BaseNetworkable.serverEntities.Find(networkableId);
                if (ent == null)
                {
                    storedData.superSignItems.Remove(itemID);
                    if (configData.showDebugInfo)
                        Puts("Item ID: " + itemID + " removed");
                    removed++;
                }
            }

            //var curTapes = new Dictionary<uint, VideoTape>(storedData.videoTapes);
            //foreach (var itemInfo in curTapes)
            //{
            //    var ent = BaseNetworkable.serverEntities.Find(itemInfo.Key);
            //    if (ent == null)
            //    {
            //        storedData.videoTapes.Remove(itemInfo.Key);
            //        removed++;
            //    }
            //}

            Debug.Log("Removed " + removed + " unexisting items from datafile");
        }

        void SetupPlacedSuperSigns()
        {
            var curTVs = new Dictionary<uint, PlacedTV>(storedData.superSigns);
            foreach (var tv in curTVs)
            {
                var ent = BaseNetworkable.serverEntities.Find(new NetworkableId(tv.Key)) as Signage;
                if (ent == null)
                {
                    storedData.superSigns.Remove(tv.Key);
                    Debug.Log("SuperSign " + tv.Key + " not found: removed from storage");
                    continue;
                }


                CreateSuperSign(ent);
            }
        }

        void Unload()
        {
            var curOpenedUI = new Dictionary<BasePlayer, int>(playerUIOpened);
            foreach (KeyValuePair<BasePlayer, int> playerInfo in curOpenedUI)
            {
                if (playerInfo.Key.IsConnected)
                    DestroyUI(playerInfo.Key, UIPanelName);
            }

            var curTVs = new Dictionary<uint, PlacedTV>(storedData.superSigns);
            foreach (var tv in curTVs)
            {
                NetworkableId networkableId = new NetworkableId(tv.Key);
                var ent = BaseNetworkable.serverEntities.Find(networkableId);
                if (ent == null || ent.IsDestroyed)
                {
                    storedData.superSigns.Remove(tv.Key);
                    Debug.Log("SuperSign " + tv.Key + " not found: removed from storage");
                    continue;
                }

                var superSign = ent.gameObject?.GetComponent<SuperSign>();
                if (superSign != null)
                    superSign.DestroySuperSign();
            }

            SaveData();
        }

        void OnEntityBuilt(Planner planner, GameObject gameObject)
        {
            var neonSign = gameObject.GetComponent<Signage>();
            if (neonSign == null) return;

            var player = planner.GetOwnerPlayer();
            if (player == null || !permission.UserHasPermission(player.UserIDString, permAdmin)) return;

            if (neonSign.skinID == neonsign_skin_id)
            {
                if (ins.configData.showDebugInfo)
                    Debug.Log(player.displayName + " Placed super sign");
                CreateSuperSign(neonSign as Signage, player);
            }
        }

        void OnEntityKill(BaseEntity entity)
        {
            if (entity?.net == null || !storedData.spawnedEntities.Contains((uint)entity.net.ID.Value))
                return;           

            if (IsVideoTapePlayer(entity))
            {
                if (configData.showDebugInfo)
                    Puts("OnEntityKill IsVideoTapePlayer() true");
                var parentSign = GetRecorderParentSign(entity as DeployedRecorder);
                if (parentSign == null) return;
                //parentSign.DestroySuperSign();
                return;
            }

            if (IsSuperSign(entity))
            {
                if (configData.showDebugInfo)
                    Puts("OnEntityKill IsSuperSign() true");
                var superSign = entity.gameObject.GetComponent<SuperSign>();
                superSign.DestroySuperSign();
                return;
            }
        }

        void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            if (item == null || container == null)
                return;

            if (container.entityOwner is DeployedRecorder && IsVideoTape(item))
                LoadFramesToSign(container, item);
        }

        object CanPickupEntity(BasePlayer player, BaseEntity entity)
        {
            if (entity == null || player == null)
                return null;

            if (IsVideoTapePlayer(entity))
            {
                var sign = GetRecorderParentSign(entity as DeployedRecorder);
                if (sign != null)
                    EjectTape(sign, player);

                return false;
            }

            return null;
        }
        #endregion



        #region command Video
        [ConsoleCommand("zvideo")]
        private void consoleCmdZDP(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player != null && !HasPermission(player.UserIDString, permAdmin))
                return;

            BasePlayer targetPlayer = player;

            if (arg.Args.Length > 0)
            {
                switch (arg.Args[0])
                {
                    case "dl":
                    case "download":
                        DownloadGif(arg.Args, player ?? null);
                        break;
                    case "tape":
                    case "givetape":
                        if (arg.Args.Length > 1) targetPlayer = FindByName(arg.Args[1]);
                        GiveTape(targetPlayer ?? null, arg.Args, true);
                        break;
                    case "tv":
                    case "givetv":
                        if (arg.Args.Length > 1) targetPlayer = FindByName(arg.Args[1]);
                        GiveSuperSign(targetPlayer ?? null);
                        break;                   
                }
            }
        }

        [ChatCommand("video")]
        private void cmdVideo(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player.UserIDString, permAdmin) && !HasPermission(player.UserIDString, permUse))
            {
                player.ChatMessage("No permission");
                return;
            }

            if (args.Length > 0)
            {
                switch (args[0])
                {
                    case "download":
                    case "dl":
                        DownloadGif(args, player);
                        break;
                    case "tape":
                    case "newtape":
                        GiveTape(player, args);
                        break;
                    case "shop":
                        TriggerShop(player, args);
                        break;
                    case "info":
                        SignInfo(player, args);
                        break;
                    case "tapeinfo":
                    case "ti":
                        TapeInfo(player, args);
                        break;
                    case "debug":
                        ToggleDebug(player);
                        break;
                }

                return;
            }
            else
            {
                player.ChatMessage("Syntax error");
            }
        }

        void TriggerShop(BasePlayer player, string[] args)
        {
            if (!HasPermission(player.UserIDString, permAdmin) && !HasPermission(player.UserIDString, permShop))
            {
                player.ChatMessage("No permission");
                return;
            }

            if (!playerUIOpened.ContainsKey(player))
            {
                playerUIOpened.Add(player, 0);
                UpdatePlayerUI(player);
            }
            else
            {
                DestroyUI(player, UIPanelName);
            }
        }

        void DownloadGif(string[] args, BasePlayer player = null)
        {
            if (player != null && !HasPermission(player.UserIDString, permAdmin) && !HasPermission(player.UserIDString, permAddGifs))
            {
                player.ChatMessage("No permission");
                return;
            }
            if (configData.showDebugInfo)
                Puts("Downloading gif...");

            if (args.Length < 2)
            {
                if (configData.showDebugInfo)
                    Puts("args.Length < 2");

                if (player != null)
                    player.ChatMessage("Syntax error: /video dl <url> [filename]");
                return;
            }

            var filename = "";
            if (args.Length > 2)
                filename = args[2];

            if (player != null)
                player.ChatMessage("trying to download gif...");

            downloadController.QueueDownload(args[1], filename, player ?? null);
        }

        void TapeInfo(BasePlayer player, string[] args)
        {
            Item activeItem = player.GetActiveItem();
            if (activeItem == null || !IsVideoTape(activeItem))
            {
                player.ChatMessage("Active item is no video tape!");
                return;
            }

            uint videoTapeID = (uint)activeItem.uid.Value;

            VideoTape videoTape;
            if (!storedData.videoTapes.TryGetValue(videoTapeID, out videoTape))
            {
                player.ChatMessage("This video tape contains no video.");
                return;
            }

            player.ChatMessage("Video tape " + videoTapeID + " contains video: " + string.Join(", ", videoTape.filenames));
            return;
        }

        void ToggleDebug(BasePlayer player)
        {
            configData.showDebugInfo = !configData.showDebugInfo;
            player.ChatMessage("Send extra debug info to console: " + configData.showDebugInfo);
        }
        #endregion


        #region command TV
        [ChatCommand("tv")]
        private void cmdSuperSign(BasePlayer player, string command, string[] args)
        {
            Signage targetSign = null;
            RaycastHit hit;
            if (Physics.Raycast(player.eyes.HeadRay(), out hit, 6))
                targetSign = hit.GetEntity() as Signage;

            if (args.Length > 0)
            {
                switch (args[0])
                {
                    case "create":
                        if (HasPermission(player, permTVCreate))
                            CreateSuperSign(targetSign, player);
                        break;
                    case "remove":
                        if (HasPermission(player, permTVCreate))
                            RemoveSuperSign(targetSign, player);
                        break;
                    case "keepframes":
                        if (HasPermission(player, permAdmin))
                            ToggleKeepFrames(targetSign, player); break;
                    case "stop":
                        if (HasPermission(player, permTVControl))
                            StopSuperSign(targetSign, player); break;
                    case "play":
                        var speed = args.Length > 1 ? float.Parse(args[1]) : 1.0f;
                        if (HasPermission(player, permTVControl))
                            PlaySuperSign(targetSign, speed, player); break;
                    case "give":
                        if (HasPermission(player, permAdmin))
                            GiveSuperSign(player); break;
                }
            }
            return;
        }

        void GiveSuperSign(BasePlayer player)
        {
            if (player == null) return;

            Item item = ItemManager.CreateByItemID(neonsign_id, 1, neonsign_skin_id);
            item.name = "Z-Video Player";
            player.inventory.GiveItem(item);

            item.MarkDirty();

            uint tapeID = (uint)item.uid.Value;
            storedData.superSignItems.Add(tapeID);
            player.ChatMessage("You received 1 Super Sign");
        }

        void CreateSuperSign(Signage sign, BasePlayer player = null)
        {
            if (player != null && !HasPermission(player.UserIDString, permAdmin) && !HasPermission(player.UserIDString, permUse))
            {
                player.ChatMessage("No permission");
                return;
            }

            if (sign == null)
            {
                if (player != null)
                    player.ChatMessage("Please look at a neon sign");
                return;
            }

            if (sign.gameObject.HasComponent<SuperSign>())
            {
                if (player != null)
                    player.ChatMessage("Sign is already a TV sign");
                return;
            }

            sign.gameObject.AddComponent<SuperSign>();

            if (!storedData.spawnedEntities.Contains((uint)sign.net.ID.Value))
                storedData.spawnedEntities.Add((uint)sign.net.ID.Value);

            if (player != null)
                player.ChatMessage("Sign is now a TV sign");

            return;
        }

        void RemoveSuperSign(Signage sign, BasePlayer player = null)
        {
            if (sign == null)
            {
                if (player != null)
                    player.ChatMessage("Please look at a TV");
                return;
            }

            if (!sign.gameObject.HasComponent<SuperSign>())
            {
                player.ChatMessage("Sign is not a TV sign");
                return;
            }

            var superSign = sign.gameObject.GetComponent<SuperSign>();
            superSign.DestroySuperSign();

            player.ChatMessage("Television destroyed");
            return;
        }

        void PlaySuperSign(Signage sign, float speed = 1.0f, BasePlayer player = null)
        {
            if (sign == null)
            {
                if (player != null)
                    player.ChatMessage("Please look at a TV");
                return;
            }

            if (!sign.gameObject.HasComponent<SuperSign>())
            {
                player.ChatMessage("Sign is not a Super Sign");
                return;
            }

            var superSign = sign.gameObject.GetComponent<SuperSign>();

            if (superSign.frameTextureIDs.Count == 0)
            {
                player.ChatMessage("Sign has no frames loaded");
                return;
            }

            if (speed > 0)
                superSign.SetSpeed(speed);

            superSign.StartPlaying();
            if (player != null)
                player.ChatMessage("Video started playing");
            return;
        }

        void StopSuperSign(Signage sign, BasePlayer player = null)
        {
            if (sign == null)
            {
                if (player != null)
                    player.ChatMessage("Please look at a TV");
                return;
            }

            if (!sign.gameObject.HasComponent<SuperSign>())
            {
                player.ChatMessage("Sign is not a Super Sign");
                return;
            }

            var superSign = sign.gameObject.GetComponent<SuperSign>();
            superSign.StopPlaying();

            player.ChatMessage("Video stopped");
            return;
        }

        void EjectTape(Signage sign, BasePlayer player = null)
        {
            if (sign == null)
            {
                if (player != null)
                    player.ChatMessage("Please look at a TV");
                return;
            }

            if (!sign.gameObject.HasComponent<SuperSign>())
            {
                if (player != null)
                    player.ChatMessage("Sign is not a Super Sign");
                return;
            }

            var superSign = sign.gameObject.GetComponent<SuperSign>();
            superSign.StopPlaying();
            superSign.EjectTape();

            if (player != null)
                player.ChatMessage("Tape ejected");

            return;
        }

        void ToggleKeepFrames(Signage sign, BasePlayer player = null)
        {
            if (!IsSuperSign(sign))
            {
                if (player != null)
                    player.ChatMessage("No valid TV found");
                return;
            }

            var superSign = sign.gameObject.GetComponent<SuperSign>();
            superSign.clearOnNewTape = !superSign.clearOnNewTape;

            if (player != null)
                player.ChatMessage("Remove old video on new tape: " + superSign.clearOnNewTape);

            return;
        }
        #endregion


        #region functions / actions
        void AddVideoToTape(uint videoTapeID, List<string> filenames, Item tape)
        {
            if (storedData.videoTapes.ContainsKey(videoTapeID))
                storedData.videoTapes.Remove(videoTapeID);

            var vt = new VideoTape();
            vt.filenames = filenames;

            storedData.videoTapes.Add(videoTapeID, vt);

            SetTapeText(tape, string.Join(", ", filenames));
            tape.skin = cassette_skin_id_full;

            if (ins.configData.showDebugInfo)
                Debug.Log("Successfully stored video file " + string.Join(", ", vt.filenames) + " on video tape " + videoTapeID);
        }

        void SetTapeText(Item tape, string text)
        {
            tape.text = text;
            tape.MarkDirty();

            if (configData.showDebugInfo)
                Debug.Log("Tape name is now " + tape.text);
        }

        void GiveTape(BasePlayer player, string[] args, bool isConsoleCmd = false)
        {
            if (player == null) return;


            // remove player name from args when using console
            if (isConsoleCmd && args.Length > 1)
            {
                var argsList = new List<string>(args);
                argsList.RemoveAt(1);
                args = argsList.ToArray();
            }

            if (!isConsoleCmd && !HasPermission(player.UserIDString, permAdmin) && !HasPermission(player.UserIDString, permShop))
            {
                player.ChatMessage("No permission");
                return;
            }

            Item item = ItemManager.CreateByItemID(cassette_id, 1, cassette_skin_id_empty);
            item.name = "Z-Video Tape";

            uint tapeID = (uint)item.uid.Value;
            storedData.videoTapes.Add(tapeID, new VideoTape());

            var filenames = new List<string>();
            if (args.Length > 1)
            {                
                for (var i = 0; i < args.Length; i++)
                {
                    if (i == 0) continue;
                    filenames.Add(args[i]);
                }

                AddVideoToTape(tapeID, filenames, item);
            }

            player.inventory.GiveItem(item);
            player.ChatMessage("You received a Video Tape with video: " + args[1]);

            if (configData.showDebugInfo)
                Debug.Log("Player " + player.displayName + " received a Z-Video Tape with video file(s): " + string.Join(", ", filenames));

            item.MarkDirty();
        }

        void LoadFramesToSign(ItemContainer container, Item item)
        {
            if(configData.showDebugInfo)
                Puts("OnItemAddedToContainer IS VIDEO TAPE! -> " + item.info.name + " -- item: " + item.info.itemid + " -- skin: " + item.skin + " -- item id" + item.uid);

            var recorderParent = container.entityOwner.GetParentEntity() as Signage;
            if (recorderParent != null && recorderParent.gameObject.HasComponent<SuperSign>())
            {
                var superSign = recorderParent.gameObject.GetComponent<SuperSign>();
                superSign.LoadFrames(ins.storedData.videoTapes[(uint)item.uid.Value].filenames);
            }
            else
            {
                Puts("recorderParent is null");
            }
        }

        void SaveVideo(string filename, List<byte[]> frames)
        {
            if (filename == "" || frames.Count == 0)
            {
                Debug.LogError("Could not save video to file");
                return;
            }

            var svid = new StoredVideo();
            svid.totalFrames = frames.Count;
            svid.frames = new List<string>();
            svid.filenames = new List<string>() { filename };

            foreach (var frameByte in frames)
            {
                svid.frames.Add(Convert.ToBase64String(frameByte));
            }

            Interface.Oxide.DataFileSystem.WriteObject(dataVideoDir + "/" + filename, svid);
            Debug.Log("Saved video as " + filename);
        }

        void SignInfo(BasePlayer player, string[] args)
        {
            RaycastHit hit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out hit, 6))
                return;

            string msg = "";
            Signage sign = hit.GetEntity() as Signage;
            if (sign != null)
            {

                msg += "Neon sign " + sign.net.ID;
                msg += "\nNeon sign is super sign: " + sign.gameObject.HasComponent<SuperSign>();

                var ssign = sign.gameObject.GetComponent<SuperSign>();
                if (ssign != null)
                {
                    if (ssign.recorder != null && ssign.recorder.inventory.itemList.Count > 0)
                    {
                        msg += "\nRecorder found: " + ssign.recorder.net.ID + " name: " + ssign.recorder.GetType().Name;

                        var cassette = ssign.recorder.inventory.GetSlot(0);
                        if (cassette != null && IsVideoTape(cassette))
                        {
                            msg += "\nCassete inside found, uid: " + cassette.uid;

                            if (storedData.videoTapes.ContainsKey((uint)cassette.uid.Value))
                            {
                                msg += "\nCassette is Video Tape with file: " + string.Join(", ", storedData.videoTapes[(uint)cassette.uid.Value].filenames);
                            }
                            else
                            {
                                msg += "\nCassette is not a Video Tape";
                            }
                        }
                        else
                        {
                            msg += "\nNo Cassette inside";

                        }
                    }
                    else
                    {
                        msg += "No recorder found";
                    }
                }
            }

            player.ChatMessage(msg);
        }
        #endregion



        #region classes
        class SuperSign : Signage
        {
            Signage sign;
            public DeployedRecorder recorder;
            public List<uint> frameTextureIDs = new List<uint>();

            public int curFrame = 0;

            public float speed = 1.0f;
            bool isReady = false;
            bool shouldPlay = false;
            bool isPlaying = false;
            public bool clearOnNewTape = true;

            void Awake()
            {
                sign = GetComponentInParent<Signage>() as Signage;
                if (sign == null)
                {
                    Debug.Log("Signage not found");
                    return;
                }

                var neon = sign as NeonSign;
                if (neon != null)
                {
                    neon.isAnimating = false;
                    neon.UpdateHasPower(25, 0);
                }

                sign.SetFlag(BaseEntity.Flags.Locked, true, false, true);
                sign.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                sign.OwnerID = 0;


                Invoke(InitSign, 0.2f);
            }

            void InitSign()
            {
                if (ins.configData.showDebugInfo)
                    Debug.Log("InitSign");

                var recorder = ins.GetChildRecorder(sign);
                if (recorder == null)
                    recorder = SpawnRecorder();


                sign.SendNetworkUpdate();
                isReady = true;

                if (ins.configData.showDebugInfo)
                    Debug.Log("Super Sign is initialized");
            }

            VideoTapePlayer SpawnRecorder()
            {
                var sPos = sign.transform.position;
                recorder = GameManager.server.CreateEntity(prefab_recorder, sPos, sign.transform.rotation) as DeployedRecorder;
                if (recorder == null)
                {
                    Debug.Log("Recorder is null");
                    return null;
                }
                recorder.Spawn();

                ins.storedData.spawnedEntities.Add((uint)recorder.net.ID.Value);

                if (!ins.storedData.superSigns.ContainsKey((uint)sign.net.ID.Value))
                    ins.storedData.superSigns.Add((uint)sign.net.ID.Value, new PlacedTV() { signID = (uint)sign.net.ID.Value, recorderID = (uint)recorder.net.ID.Value });


                recorder.SetParent(sign, false, true);
                recorder.transform.position = sign.transform.position;
                recorder.transform.localPosition = new Vector3(0.4f, 0f, 0.05f);
                recorder.transform.localRotation = Quaternion.AngleAxis(0, Vector3.up);
                recorder.transform.hasChanged = true;
                recorder.SendNetworkUpdate();

                var vtplayer = recorder.gameObject.AddComponent<VideoTapePlayer>();
                if (vtplayer == null) return null;

                vtplayer.InitVideoPlayer(sign);

                Invoke(() =>
                {
                    recorder.SetParent(sign, true, true);
                }, 0.5f);

                return vtplayer;

            }

            public void DestroySuperSign(bool alsoKill = false)
            {
                if (recorder != null && !recorder.IsDestroyed)
                {
                    if (ins.configData.showDebugInfo)
                        Debug.Log("Killing recorder");
                    EjectTape();
                    if (recorder.net != null && ins.storedData.spawnedEntities.Contains((uint)recorder.net.ID.Value))
                        ins.storedData.spawnedEntities.Remove((uint)recorder.net.ID.Value);

                    recorder.Kill();
                }

                if (sign != null && !sign.IsDestroyed && sign.gameObject.HasComponent<SuperSign>())
                    Destroy(sign.gameObject.GetComponent<SuperSign>());

                if (sign != null && alsoKill)
                    sign.Kill();

                if (ins.configData.showDebugInfo)
                    Debug.Log("Super Sign is now normal again");
            }

            public void LoadFrames(List<string> filenames)
            {
                isReady = false;

                if (clearOnNewTape)
                {
                    sign.textureIDs = new List<uint>().ToArray();
                    frameTextureIDs.Clear();
                }


               
                List<uint> existingVIDS;
                if (filenames.Count == 1 && ins.storedData.videoTextureIDS.TryGetValue(filenames[0], out existingVIDS) && existingVIDS.Count > 0)
                {
                    List<uint> newTextures = new List<uint>(sign.textureIDs.ToList());
                    newTextures.AddRange(existingVIDS);

                    sign.textureIDs = newTextures.ToArray();
                    frameTextureIDs = newTextures;

                    Debug.Log("Using " + newTextures.Count + " frames from filesystem storage");

                    StartCoroutine(ValidateTexturesAndPlay(newTextures, filenames));
                    return;
                }
                else if(filenames.Count == 1)
                {
                    StoredVideo videoData;
                    try
                    {
                        videoData = Interface.Oxide.DataFileSystem.ReadObject<StoredVideo>($"{dataVideoDir}/{filenames[0]}");
                    }
                    catch (Exception e)
                    {
                        Debug.Log(e.Message);
                        return;
                    }

                    if (videoData.frames.Count == 0)
                    {
                        Debug.Log("No frames found in loaded video tape");
                        return;
                    }

                    videoData.filenames = new List<string>() { filenames[0] };

                    int amount = sign.textureIDs.Length;
                    foreach (string frameData in videoData.frames)
                    {
                        if (frameData != "undefined")
                            amount++;
                    }

                    StartCoroutine(StoreFrames(videoData));
                    Debug.Log("Storing 1 video with " + amount + " frames to Video Player");
                }
                else if(filenames.Count > 1)
                {
                    int amount = sign.textureIDs.Length;
                    StoredVideo newStoredVideo = new StoredVideo();
                    for (var i = 0; i < filenames.Count; i++)
                    {
                        StoredVideo videoData;
                        try
                        {
                            videoData = Interface.Oxide.DataFileSystem.ReadObject<StoredVideo>($"{dataVideoDir}/{filenames[i]}");
                        }
                        catch (Exception e)
                        {
                            Debug.Log(e.Message);
                            continue;
                        }

                        if (videoData.frames.Count == 0)
                        {
                            Debug.Log("No frames found in loaded video tape");
                            continue;
                        }
                       
                        foreach (string frameData in videoData.frames)
                        {
                            if (frameData != "undefined")
                                amount++;
                        }

                        newStoredVideo.filenames.Add(filenames[i]);
                        newStoredVideo.frames.AddRange(videoData.frames);
                    }

                    newStoredVideo.totalFrames = newStoredVideo.frames.Count;
                    StartCoroutine(StoreFrames(newStoredVideo));

                    Debug.Log("Storing " + amount + " frames to Video Player");

                }
                return;
            }

            public IEnumerator RemoveTextures(List<uint> textureIDs, List<string> filenames, bool loadAfter = false)
            {
                foreach (var filename in filenames)
                {
                    Debug.Log("Removing " + textureIDs.Count + " leftover textures for video " + filename);

                    int i = 0;
                    foreach (var textureID in textureIDs)
                    {
                        FileStorage.server.RemoveExact(textureID, FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID, 0);

                        i++;

                        if (i % 25 == 0)
                        {
                            if (ins.configData.showDebugInfo)
                                Debug.Log("Removing leftover textures for video " + filename + ": " + i + "/" + textureIDs.Count);
                            yield return CoroutineEx.waitForEndOfFrame;
                        }
                    }

                    Debug.Log("All leftover textures of video " + filename + " are removed");

                    if (ins.storedData.videoTextureIDS.ContainsKey(filename))
                        ins.storedData.videoTextureIDS.Remove(filename);
                }

                if (loadAfter)
                    LoadFrames(filenames);                

                yield return true;
            }
            public IEnumerator ValidateTexturesAndPlay(List<uint> textureIDs, List<string> filenames)
            {

                Debug.Log("Validating " + textureIDs.Count + " existing frames for video(s): " + string.Join(", ", filenames));

                int i = 0;
                var valid = true;
                foreach (var textureID in textureIDs)
                {
                    if (valid && !ins.TextureExists(textureID))
                    {
                        valid = false;
                        Debug.LogError("Texture ID " + textureID + " not found -> Removing video(s) " + string.Join(", ", filenames) + " from storage");
                        break;
                    }

                    i++;

                    if (i % 10 == 0)
                    {
                        if (ins.configData.showDebugInfo)
                            Debug.Log("Validating existing frames: " + i + "/" + textureIDs.Count);
                        yield return CoroutineEx.waitForEndOfFrame;
                    }
                }


                if (!valid)
                {
                    StartCoroutine(RemoveTextures(textureIDs, filenames, true));
                }
                else
                {
                    isReady = true;
                    SetSpeed(0.1f);
                    StartPlaying();
                }

                yield return true;
            }

            public IEnumerator StoreFrames(StoredVideo videoData)
            {
                Debug.LogError("Storing " + videoData.frames.Count + " frames...");
                Debug.LogError("Filenames: " + string.Join(", ", videoData.filenames));

                var amount = sign.textureIDs.Length + videoData.frames.Count;
                Array.Resize(ref sign.textureIDs, amount);

                var i = 0;
                foreach (string frameData in videoData.frames)
                {
                    if (frameData == "undefined")                    
                        continue;

                    var imageByte = Convert.FromBase64String(frameData);

                    //var currentTextureID = sign.textureIDs[i];
                    //if (currentTextureID != 0)
                    //    FileStorage.server.RemoveExact(currentTextureID, FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID, (uint)i);

                    var newTextureID = FileStorage.server.Store(imageByte, FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID, (uint)i);
                    frameTextureIDs.Add(newTextureID);

                    if (i % 25 == 0 && ins.configData.showDebugInfo)
                        Debug.LogError("Storing frames: " + i + "/" + videoData.frames.Count);

                    i++;

                    if (i % 5 == 0)
                        yield return CoroutineEx.waitForEndOfFrame;
                }

                sign.textureIDs = frameTextureIDs.ToArray();

                var defVideoTitle = videoData.filenames.Count > 1 ? "merged_video_" + videoData.filenames[1] : videoData.filenames[0];
                if (defVideoTitle != "" && !ins.storedData.videoTextureIDS.ContainsKey(defVideoTitle))
                    ins.storedData.videoTextureIDS.Add(defVideoTitle, new List<uint>());

                ins.storedData.videoTextureIDS[defVideoTitle] = frameTextureIDs;
                Debug.Log("Finished storing frames, starting to play!");

                isReady = true;
                SetSpeed(0.1f);
                StartPlaying();

                yield return true;
            }

            public void StartPlaying()
            {
                if (isReady && !isPlaying && frameTextureIDs.Count > 0)
                {
                    if (ins.configData.showDebugInfo)
                        Debug.Log("Super Sign started playing");

                    shouldPlay = true;
                    isPlaying = true;

                    Invoke("PlayNextFrame", speed);
                }
            }

            public void StopPlaying()
            {
                if (ins.configData.showDebugInfo)
                    Debug.Log("Super Sign stopped playing");

                shouldPlay = false;
                isPlaying = false;
            }

            public void PlayNextFrame()
            {
                if (!shouldPlay || frameTextureIDs.Count <= 1 || !isReady)
                {
                    shouldPlay = false;
                    isPlaying = false;

                    if (ins.configData.showDebugInfo)
                        Debug.Log("!shouldPlay || frames.Count <= 1 || !isReady");
                    return;
                }

                if (sign == null || sign.IsDestroyed)
                {
                    shouldPlay = false;
                    isPlaying = false;

                    if (ins.configData.showDebugInfo)
                        Debug.Log("sign == null || sign.IsDestroyed");
                    return;
                }

                var nextFrame = curFrame + 1;
                if (nextFrame > frameTextureIDs.Count - 1)
                    nextFrame = 0;

                sign.textureIDs[0] = frameTextureIDs.ElementAt(nextFrame);
                sign.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                curFrame = nextFrame;

                Invoke("PlayNextFrame", speed);
            }

            public void SetSpeed(float newSpeed)
            {
                if (newSpeed > 0)
                    speed = newSpeed;
            }

            public void EjectTape()
            {
                if (HasCassette(recorder))
                {
                    if (ins.configData.showDebugInfo)
                        Debug.Log("Dropping contents of recorder");

                    var cassette = recorder.inventory.GetSlot(0);
                    cassette.RemoveFromContainer();
                    cassette.Drop((recorder.transform.position + (transform.up * 0.5f) + (transform.forward * 0.5f)), new Vector3(0, 1f, 0), new Quaternion());
                }
            }
        }

        class VideoTapePlayer : DeployedRecorder
        {
            Signage parentSuperSign;
            DeployedRecorder recorder;

            void Awake()
            {

            }

            public bool InitVideoPlayer(Signage parent)
            {
                recorder = GetComponentInParent<DeployedRecorder>();
                if (recorder == null)
                {
                    Debug.Log("Recorder is null");
                    return false;
                }

                if (parent == null)
                {
                    Debug.Log("Recorder parent is null");
                    return false;
                }

                parentSuperSign = parent;

                Debug.Log("VideoTapePlayer initialized");
                return true;
            }

            public void SetParentSign(Signage parent)
            {
                parentSuperSign = parent;

                if (parent != null && recorder != null)
                    recorder.SetParent(parent, false, true);

                if (ins.configData.showDebugInfo)
                    Debug.Log("VideoTapePlayer parent set to sign: " + (parent?.net != null ? parent.net.ID.ToString() : "null"));
            }

        }
        #endregion


        class VideoShopUser
        {
            public string playerID;
            public int userUIPage = 0;
        }

        #region UI
        public void UpdatePlayerUI(BasePlayer player)
        {
            int userPage;
            if (!playerUIOpened.TryGetValue(player, out userPage)) return;

            var newPlayerUI = new CuiElementContainer();

            CuiPanel newPanel = CreateUIPanel("0.80 0.25", "0.98 0.9", "0 0 0.1 0.8");
            string panel = newPlayerUI.Add(newPanel, "Overlay", UIPanelName);

            var lft = "0.05";
            var rgt = "0.95";

            var h = 0.90f;
            var lh = 0.06f;

            newPlayerUI.Add(CreateNewLabel("VIDEO STORE", lft + " 0.85", "0.6 0.95", TextAnchor.MiddleLeft, 14), UIPanelName);
            newPlayerUI.Add(CreateNewButton("TV Sign", "zvideoplayer givetv", "0.6 0.88", rgt + " 0.92", TextAnchor.MiddleCenter, 8, green), UIPanelName);

            var allFilenames = GetAvailableVideoFiles();
            if (allFilenames.Count > 0)
            {
                int vidPP = Math.Min(videosPerPage, allFilenames.Count);
                int start = userPage * vidPP;

                var amountOfPages = Math.Ceiling((decimal)allFilenames.Count / (decimal)vidPP);
                int rangeEnd = vidPP;

                int leftOver = allFilenames.Count - (userPage * vidPP);
                if (leftOver < vidPP)
                    rangeEnd = leftOver;

                var filenames = allFilenames.GetRange(start, rangeEnd);


                var i = 0;
                foreach (var filename in filenames)
                {
                    var leftVal = i == 0 ? lft : "0.51";
                    var rightVal = i == 0 ? "0.49" : rgt;

                    if (i == 0)
                        h = (h - lh) - 0.01f;

                    newPlayerUI.Add(CreateNewButton(filename, "zvideoplayer tape " + filename, leftVal + " " + (h - lh), rightVal + " " + h, TextAnchor.MiddleCenter, 10, "0 0 0 0.5"), UIPanelName);

                    i = i == 0 ? 1 : 0;
                }

                if (allFilenames.Count > videosPerPage)
                //if (amountOfPages > 0)
                {
                    //Debug.Log("amountOfPages: " + amountOfPages);
                    var prevPage = (userPage == 0 ? amountOfPages - 1 : userPage - 1);
                    var nextPage = (userPage == amountOfPages - 1 ? 0 : userPage + 1);
                    newPlayerUI.Add(CreateNewButton("<<", "zvideoplayer uipage " + prevPage, lft + " 0.12", "0.48 0.18", TextAnchor.MiddleCenter, 10, orange), UIPanelName);
                    newPlayerUI.Add(CreateNewButton(">>", "zvideoplayer uipage " + nextPage, "0.52 0.12", rgt + " 0.18", TextAnchor.MiddleCenter, 10, orange), UIPanelName);
                }
            }

            newPlayerUI.Add(CreateNewButton("EXIT", "zvideoplayer close", lft + " 0.05", rgt + " 0.11", TextAnchor.MiddleCenter, 8, red), UIPanelName);
            AddUI(player, newPlayerUI, UIPanelName);
        }

        #region command Console
        [ConsoleCommand("zvideoplayer")]
        private void ConsoleCmdZDP(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null)
                return;

            if (!HasPermission(player.UserIDString, permAdmin)) return;

            int userPage;
            if (!playerUIOpened.TryGetValue(player, out userPage)) return;



            if (!HasPermission(player.UserIDString, permAdmin))
                return;

            if (arg.Args.Length > 0)
            {
                switch (arg.Args[0])
                {
                    case "tape":
                        GiveTape(player, arg.Args);
                        break;
                    case "givetv":
                        GiveSuperSign(player);
                        break;
                    case "uipage":
                        var nPage = 0;
                        if (arg.Args.Length > 1 && int.TryParse(arg.Args[1], out nPage) && nPage >= 0)
                        {
                            if (configData.showDebugInfo)
                                Puts("UI Page for player " + player.displayName + ": " + nPage);
                            playerUIOpened[player] = nPage;
                        }
                        break;
                    case "close":
                        DestroyUI(player, UIPanelName);
                        break;
                }

                UpdatePlayerUI(player);
            }
            else
            {

            }
        }
        #endregion

        public void AddUI(BasePlayer player, CuiElementContainer container, string panel)
        {
            if (playerUIOpened.ContainsKey(player))
                DestroyUI(player, panel);

            if (!playerUIOpened.ContainsKey(player))
                playerUIOpened.Add(player, 0);

            CuiHelper.AddUi(player, container);
        }

        public void DestroyAllPlayerUIs(float delay = 0f)
        {
            timer.Once(delay, () =>
            {
                var curOpenedUI = new Dictionary<BasePlayer, int>(playerUIOpened);
                foreach (KeyValuePair<BasePlayer, int> playerInfo in curOpenedUI)
                {
                    if (playerInfo.Key.IsConnected)
                        DestroyUI(playerInfo.Key, UIPanelName);

                    playerUIOpened.Remove(playerInfo.Key);
                }
            });
        }

        public void DestroyUI(BasePlayer player, string panel)
        {
            CuiHelper.DestroyUi(player, panel);

            if (playerUIOpened.ContainsKey(player))
                playerUIOpened.Remove(player);
        }


        public CuiPanel CreateUIPanel(string anchorMin, string anchorMax, string background = "1 0 0 0.7")
        {
            CuiPanel wrapPanel = new CuiPanel
            {
                CursorEnabled = false,
                Image = {
                    Color = background
                },
                RectTransform = {
                    AnchorMin = anchorMin,
                    AnchorMax = anchorMax
                }
            };
            return wrapPanel;
        }


        public CuiLabel CreateNewLabel(string text, string anchorMin = "0 0", string anchorMax = "1 1", TextAnchor align = TextAnchor.MiddleCenter, int fontSize = 12, string color = "1 1 1 1")
        {
            CuiLabel label = new CuiLabel
            {
                Text = {
                    Text = text,
                    Align = align,
                    Color = color,
                    FontSize = fontSize
                },
                RectTransform = {
                    AnchorMin = anchorMin,
                    AnchorMax = anchorMax
                }
            };

            return label;
        }


        public CuiButton CreateNewButton(string text, string command, string anchorMin = "0 0", string anchorMax = "1 1", TextAnchor align = TextAnchor.MiddleCenter, int fontSize = 12, string color = "1 1 1 1")
        {
            CuiButton button = new CuiButton
            {
                Button = { Color = color, Command = command, FadeIn = 1.0f },
                RectTransform = { AnchorMin = anchorMin, AnchorMax = anchorMax },
                Text = { Text = text, FontSize = fontSize, Align = align }
            };

            return button;
        }


        #endregion


        #region image downloader / processing
        class DownloadRequest
        {
            public string url;
            public BasePlayer player = null;
            public string newFilename;
            public DownloadRequest(string inurl, string inewFilename, BasePlayer inplayer = null)
            {
                url = inurl;
                player = inplayer;
                newFilename = inewFilename;
            }
        }

        private class DownloadController : FacepunchBehaviour
        {
            public bool downloading = false;
            private readonly Queue<DownloadRequest> downloadQueue = new Queue<DownloadRequest>();

            public void QueueDownload(string url, string filename, BasePlayer player = null)
            {
                downloadQueue.Enqueue(new DownloadRequest(url, filename, player ?? null));
                StartNewDownload();
            }

            public void StartNewDownload()
            {
                if (downloading) return;

                if (downloadQueue.Count <= 0)
                {
                    if (ins.configData.showDebugInfo)
                        Debug.Log("Download queue empty, stopped Coroutine");
                    return;
                }
                downloading = true;
                StartCoroutine(StartDownload(downloadQueue.Dequeue()));
            }

            private IEnumerator StartDownload(DownloadRequest request)
            {
                Debug.Log("GIF image download started for " + (request.player != null ? request.player.displayName : "console") + " - Image url: " + request.url);

                UnityWebRequest webRequest = UnityWebRequest.Get(request.url);
                yield return webRequest.SendWebRequest();

                if (webRequest.isNetworkError || webRequest.isHttpError)
                {
                    Debug.LogError("GIF image download failed for image url: " + request.url + " - Error: " + (webRequest.error != null ? webRequest.error : "N/A"));

                    if (request.player != null && request.player.IsConnected)
                        request.player.ChatMessage("GIF download failed");

                    downloading = false;
                    webRequest.Dispose();
                    StartNewDownload();
                    yield break;
                }

                byte[] downloadResponse = webRequest.downloadHandler.data;

                if (request.player != null
                    && !ins.HasPermission(request.player.UserIDString, permNolimits)
                    && webRequest.downloadedBytes > (ins.configData.maxDownloadSizeMB * 1024 * 1000))
                {
                    request.player.ChatMessage("GIF Download failed: File size too big (max " + ins.configData.maxDownloadSizeMB + "MB)");

                    downloading = false;
                    webRequest.Dispose();
                    StartNewDownload();
                    yield break;
                }


                if (request.player != null && request.player.IsConnected)
                    request.player.ChatMessage("GIF download done, processing...");

                ProcessImage(downloadResponse, request);


                downloading = false;
                webRequest.Dispose();
                StartNewDownload();
            }
        }

        private static void ProcessImage(byte[] downloadResponse, DownloadRequest request)
        {
            try
            {
                using (MemoryStream startBytes = new MemoryStream())
                {
                    startBytes.Write(downloadResponse, 0, downloadResponse.Length);

                    //request.originalImageBytes = startBytes.ToArray();
                    ServerMgr.Instance.StartCoroutine(ins.getFrames(Image.FromStream(startBytes), request));
                    return;
                }
            }
            catch (ArgumentException e)
            {
                Debug.LogError(e);

                if (request.player != null)
                    request.player.ChatMessage("GIF is missing data, try to rename/resize/optimize (tip: ezgif.com) and try again");

                return;
            }
        }

        private static void SaveGifToFile(List<byte[]> frameImages, DownloadRequest request)
        {
            var svid = new StoredVideo();
            svid.totalFrames = frameImages.Count;
            svid.frames = new List<string>();
            svid.filenames = new List<string>() { request.newFilename };

            foreach (var frameByte in frameImages)
            {
                svid.frames.Add(Convert.ToBase64String(frameByte));
            }

            if (ins.configData.showDebugInfo)
                Debug.Log("Saving gif...");

            Interface.Oxide.DataFileSystem.WriteObject(dataVideoDir + "/" + request.newFilename, svid);

            if (request.player != null && request.player.IsConnected)
            {
                request.player.ChatMessage("GIF saved as " + request.newFilename + ".json");
            }
            Debug.Log("GIF saved as " + request.newFilename + ".json");
        }



        private IEnumerator getFrames(Image originalImg, DownloadRequest request)
        {
            int numberOfFrames = originalImg.GetFrameCount(FrameDimension.Time);
            if (request.player != null && numberOfFrames > configData.maxFramesPerVideo && !HasPermission(request.player.UserIDString, permNolimits))
            {
                request.player.ChatMessage("Cannot save GIF, the amount of frames is higher than the limit (" + configData.maxFramesPerVideo + ")");
                yield break;
            }

            Debug.Log("Getting and saving " + numberOfFrames + " frames...");

            Image[] frames = new Image[numberOfFrames];
            List<byte[]> newFrames = new List<byte[]>();

            using (var memStream = new MemoryStream())
            {
                for (int i = 0; i < numberOfFrames; i++)
                {
                    // switch frames of original image, then make copy
                    originalImg.SelectActiveFrame(FrameDimension.Time, i);

                    using (var newImg = new Bitmap(originalImg))
                    {
                        newImg.Save(memStream, newImg.RawFormat);

                        var newBytes = memStream.ToArray();
                        newFrames.Add(newBytes);

                        if (i % 10 == 0 && ins.configData.showDebugInfo)
                            Debug.Log("Getting and saving frames: " + i + "/" + numberOfFrames);

                        newImg.Dispose();
                    }

                    // clear mem stream
                    byte[] buffer = memStream.GetBuffer();
                    Array.Clear(buffer, 0, buffer.Length);
                    memStream.Position = 0;
                    memStream.SetLength(0);


                    //if(i % 10 == 0)
                    //    yield return CoroutineEx.waitForEndOfFrame;

                    //yield return null;
                }
            }

            if (request.newFilename != "")
                SaveGifToFile(newFrames, request);

            yield return true;
        }
        #endregion

        #region helpers
        bool HasPermission(string id, string perm) => permission.UserHasPermission(id, perm);
        bool HasPermission(BasePlayer player, string perm)
        {
            if (player == null) return false;

            if (!permission.UserHasPermission(player.UserIDString, perm))
            {
                player.ChatMessage("No permission");
                return false;
            }

            return true;
        }

        private bool VideoFileExists(string name)
        {
            return Interface.Oxide.DataFileSystem.ExistsDatafile($"{dataVideoDir}/{name}");
        }

        public static bool HasCassette(DeployedRecorder recorder)
        {
            return recorder.inventory.itemList.Count > 0;
        }

        bool IsVideoTape(Item item)
        {
            if (item.skin != cassette_skin_id_empty && item.skin != cassette_skin_id_full)
                return false;

            if (item.info.itemid != cassette_id)
                return false;

            if (!ins.storedData.videoTapes.ContainsKey((uint)item.uid.Value))
                return false;

            return true;
        }

        bool IsSuperSign(BaseEntity entity)
        {
            if (entity == null) return false;
            return entity as Signage != null && entity.gameObject.HasComponent<SuperSign>();
        }

        bool IsVideoTapePlayer(BaseEntity entity)
        {
            return entity as DeployedRecorder != null && entity.gameObject.HasComponent<VideoTapePlayer>();
        }

        DeployedRecorder GetChildRecorder(Signage sign)
        {
            if (sign.children == null || sign.children.Count == 0) return null;

            for (int i = 0; i < sign.children.Count; i++)
            {
                var childEnt = sign.children[i] as DeployedRecorder;
                if (childEnt != null) return sign.children[i] as DeployedRecorder;
            }

            return null;
        }

        SuperSign GetRecorderParentSign(DeployedRecorder recorder)
        {
            if (recorder == null || !recorder.HasParent()) return null;

            if (!recorder.GetParentEntity().gameObject.HasComponent<SuperSign>()) return null;

            return recorder.GetParentEntity().gameObject.GetComponent<SuperSign>();
        }

        List<string> GetAvailableVideoFiles()
        {
            var printFiles = Interface.Oxide.DataFileSystem.GetFiles(dataVideoDir);
            var list = new List<string>();

            foreach (var printFile in printFiles)
            {
                if (printFile.IndexOf(".json") < 0) continue;

                var pts = printFile.Split('/', '\\');
                var name = pts[pts.Length - 1].Replace(".json", "");

                if (name == "_placeholder")
                    continue;

                list.Add(name);
            }
            return list;
        }

        public bool TextureExists(uint tid)
        {
            return FileStorage.server.Get(tid, FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID) != null;
        }

        BasePlayer FindByName(string nameOrUid, bool sleepers = false)
        {
            List<BasePlayer> players = !sleepers ?
                new List<BasePlayer>(BasePlayer.activePlayerList) :
                new List<BasePlayer>(BasePlayer.sleepingPlayerList);

            ulong userId;
            if (ulong.TryParse(nameOrUid, out userId))
            {
                return players.SingleOrDefault(x => x.userID == userId);
            }

            string targetName = nameOrUid
                .ToLower()
                .Replace(" ", "");

            return players
                .FirstOrDefault(x => x.displayName
                                      .ToLower()
                                      .Replace(" ", "")
                                      .Contains(targetName));
        }

        #endregion

        #region config
        private ConfigData configData;

        class ConfigData
        {
            public float maxDownloadSizeMB = 5f;
            public int maxFramesPerVideo = 300;
            public bool showDebugInfo = true;
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
            configData = new ConfigData();
            SaveConf();
        }


        void SaveConf() => Config.WriteObject(configData, true);
        #endregion

        #region data
        class VideoTape
        {
            //public string filename = "";
            public List<string> filenames = new List<string>();
        }

        class StoredVideo
        {
            public int totalFrames;
            public List<string> frames = new List<string>();
            //public string filename = "";
            public List<string> filenames = new List<string>();
        }

        class PlacedTV
        {
            public uint signID;
            public uint recorderID;
        }

        StoredData storedData;
        VideoFolder videoFolder;
        class StoredData
        {
            public Dictionary<uint, VideoTape> videoTapes = new Dictionary<uint, VideoTape>();
            public Dictionary<string, List<uint>> videoTextureIDS = new Dictionary<string, List<uint>>();

            public Dictionary<uint, PlacedTV> superSigns = new Dictionary<uint, PlacedTV>();
            public List<uint> superSignItems = new List<uint>();

            public List<uint> spawnedEntities = new List<uint>();
        }

        class VideoFolder
        {

        }

        void Loaded()
        {
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(dataFileName);

            videoFolder = Interface.Oxide.DataFileSystem.ReadObject<VideoFolder>(dataVideoDir + "/_placeholder");
        }

        void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(dataFileName, storedData);
        }
        #endregion
    }
}
