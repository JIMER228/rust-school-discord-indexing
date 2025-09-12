using UnityEngine;
using System.Collections.Generic;
using Oxide.Core;
using Newtonsoft.Json;
using System.Collections;
using UnityEngine.Networking;
using System.Drawing;
using System.IO;
using System.Drawing.Imaging;
using System;

namespace Oxide.Plugins
{
    [Info("ZBillBoards", "JOSH-Z", "1.4.1")]
    [Description("Create huge (or small) billboards")]
    public class ZBillBoards : RustPlugin
    {
        private static ZBillBoards ins;
        private GameObject downloadControllerObject;
        private DownloadController downloadController;
        private GameObject pasteControllerObject;
        private PasteController pasteController;

        const string permAdmin = "zbillboards.admin";
        const string permConsole = "zbillboards.console";
        const string permTier1 = "zbillboards.tier1";
        const string permTier2 = "zbillboards.tier2";
        const string permTier3 = "zbillboards.tier3";
        const string dataFileName = "ZBillBoards";

        #region Hooks
        void Init()
        {
            if (!LoadConfigVariables())
            {
                Puts("An error was found in your config file!");
                return;
            }

            permission.RegisterPermission(permAdmin, this);
            permission.RegisterPermission(permConsole, this);
            permission.RegisterPermission(permTier1, this);
            permission.RegisterPermission(permTier2, this);
            permission.RegisterPermission(permTier3, this);
        }

        void OnServerInitialized()
        {
            ins = this;

            downloadControllerObject = new GameObject("DownloadController");
            downloadController = downloadControllerObject.AddComponent<DownloadController>();

            pasteControllerObject = new GameObject("PasteController");
            pasteController = pasteControllerObject.AddComponent<PasteController>();

            ValidateAllBillboards();
        }

        void OnEntityKill(Signage sign)
        {
            if (sign == null || sign.net == null)
                return;

            if (!IsPartOfBillboard(sign))
                return;

            Signage mainSign = GetMainSign(sign.net.ID.Value);
            if (mainSign == null)
                return;

            BillBoardData billBoardData;
            if (!ins.storedData.billBoards.TryGetValue(mainSign.net.ID.Value, out billBoardData))
                return;

            // to prevent signs triggering this function again
            if (billBoardData.destroyed)
                return;

            // always destroy if main sign is killed, otherwise destroy if set in config 
            if (mainSign.net.ID == sign.net.ID || configData.destroyOnMissingSigns)
                DestroyBillBoard(mainSign.net.ID.Value);       
            
            Debug.Log($"[ZBillBoards] Billboard sign got destroyed");
        }

        void Unload()
        {
            SaveData();

            UnityEngine.Object.Destroy(downloadControllerObject);
            UnityEngine.Object.Destroy(pasteControllerObject);          
        }
        #endregion

        #region Download controller
        private static void ProcessImage(byte[] bytes, int targetWidth, int targetHeight, DownloadRequest request)
        {
            //byte[] bytesEditedImage;

            using (MemoryStream startBytes = new MemoryStream(), newBytes = new MemoryStream())
            {
                startBytes.Write(bytes, 0, bytes.Length);

                Bitmap image = new Bitmap(startBytes);
                Bitmap finalImage = new Bitmap(targetWidth, targetHeight);

                using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(finalImage))
                {
                    graphics.DrawImage(image, new Rectangle(0, 0, targetWidth, targetHeight));
                }

                finalImage.Save(newBytes, ImageFormat.Png);

                SplitImageAndPaste(request, finalImage, ins.configData.imageSize, ins.configData.imageSize);

                image.Dispose();
                finalImage.Dispose();
            }
        }

        private static void SplitImageAndPaste(DownloadRequest request, Bitmap sourceImage, int defWidth, int defHeight)
        {
            if(ins.configData.debug)
                Debug.Log("Image url: " + request.imageURL);

            if (request.targetSign == null)
            {
                ins.pasteController.PasteNextFromList();
                return;
            }

            BillBoardData billBoardData;
            if (!ins.storedData.billBoards.TryGetValue(request.targetSign.net.ID.Value, out billBoardData)) return;

            var sTotCols = request.width;
            var curCol = 1;
            var curRow = 1;

            foreach (ulong signID in billBoardData.billBoardSigns)
            {
                byte[] imagePart = GetImagePart(sourceImage, request, ins.configData.imageSize, ins.configData.imageSize, curCol, curRow);
                ins.pasteController.AddToPasteList(imagePart, signID, curCol, curRow);

                if (curCol == sTotCols)
                {
                    curCol = 1;
                    curRow++;
                }
                else
                {
                    curCol++;
                }
            }

            sourceImage.Dispose();

            ins.pasteController.PasteNextFromList();
        }

        private class DownloadController : FacepunchBehaviour
        {            
            private byte downloading;
            private readonly Queue<DownloadRequest> downloadQueue = new Queue<DownloadRequest>();

            public void QueueDownload(string url, BasePlayer player, Signage sign, BillBoardData billboardData)
            {
                downloadQueue.Enqueue(new DownloadRequest(url, player, sign, billboardData));
                StartNewDownload();
            }

            private byte[] GetDownloadResponse(UnityWebRequest webRequest)
            {
                Texture2D texture = new Texture2D(2, 2);
                texture.LoadImage(webRequest.downloadHandler.data);

                byte[] image;
                image = texture.EncodeToPNG();

                DestroyImmediate(texture);
                return image;
            }

            private void StartNewDownload()
            {
                if (downloading >= 1) return;                
                if (downloadQueue.Count <= 0)
                {
                    if(ins.configData.debug)
                        Debug.Log("Billboard download queue empty, stopped Coroutine");
                    return;
                }
                downloading++;
                StartCoroutine(StartDownload(downloadQueue.Dequeue()));
            }

            private IEnumerator StartDownload(DownloadRequest request)
            {
                Debug.Log("Billboard image download started for " + request.player.displayName + " - Image url: " + request.imageURL);

                UnityWebRequest webRequest = UnityWebRequest.Get(request.imageURL);
                yield return webRequest.SendWebRequest();

                if (webRequest.isNetworkError || webRequest.isHttpError)
                {
                    Debug.Log("Billboard image download failed for image url: " + request.imageURL + " - Error: " + (webRequest.error != null ? webRequest.error : "N/A"));
                    if(request.player != null && request.player.IsConnected)
                        request.player.ChatMessage("Failed to download your image, error: " + (webRequest.error != null ? webRequest.error : "Unknown"));

                    webRequest.Dispose();

                    downloading--;
                    StartNewDownload();
                    yield break;
                }

                byte[] downloadResponse;
                downloadResponse = GetDownloadResponse(webRequest);

                int defWidth = ins.configData.imageSize * request.width;
                int defHeight = ins.configData.imageSize * request.height;

                ProcessImage(downloadResponse, defWidth, defHeight, request);

                //if(request.adjustBrightness)
                //    resizedImageBytes = RecolorImage(downloadResponse, defWidth, defHeight);


                downloading--;

                yield return new WaitForEndOfFrame();

                StartNewDownload();
                webRequest.Dispose();

            }                   
        }

        private class DownloadRequest
        {
            public BasePlayer player { get; }
            public Signage targetSign { get; }
            public string imageURL { get; set; }
            public int width { get; set; } = 1;
            public int height { get; set; } = 1;
            public float brightness { get; set; } = 1f;

            public DownloadRequest(string url, BasePlayer inputPlayer, Signage inputSign, BillBoardData billboardData)
            {
                imageURL = url;
                player = inputPlayer;
                targetSign = inputSign;
                width = billboardData.signsHorizontal;
                height = billboardData.signsVertical;
                brightness = billboardData.brightness;
            }
        }
        #endregion

        #region Paste Controller
        private class PasteController : FacepunchBehaviour
        {
            private bool isPasting = false;
            private List<PasteRequest> pasteList = new List<PasteRequest>();

            public void AddToPasteList(byte[] imgData, ulong signID, int coordsX, int coordsY)
            {
                pasteList.Add(new PasteRequest(imgData, signID, coordsX, coordsY));
            }

            public void PasteNextFromList()
            {
                if (isPasting) return;
                if (pasteList.Count == 0) return;

                isPasting = true;

                var request = pasteList[0];
                pasteList.RemoveAt(0);

                PasteImage(request);
            }

            public void ClearPasteList()
            {
                isPasting = true;
                pasteList.Clear();
                isPasting = false;
            }

            public int GetEmptyFrame(Signage sign)
            {
                for (int index = 0; index < sign.textureIDs.Length; index++)
                {
                    if (sign.textureIDs[index] == 0)
                        return index;
                }
                return 0;
            }

            private void PasteImage(PasteRequest request)
            {
                Signage targetSign = BaseNetworkable.serverEntities.Find(new NetworkableId(request.signID)) as Signage;
                if (targetSign == null)
                {
                    isPasting = false;
                    return;
                }

                targetSign.EnsureInitialized();

                var frameNumber = GetEmptyFrame(targetSign);
                var currentTextureID = targetSign.textureIDs[frameNumber];

                if (currentTextureID != 0)
                    FileStorage.server.RemoveExact(currentTextureID, FileStorage.Type.png, targetSign.net.ID, (uint)frameNumber);

                var textureId = FileStorage.server.Store(request.imgData, FileStorage.Type.png, targetSign.net.ID, (uint)frameNumber);
                targetSign.textureIDs[frameNumber] = textureId;
                targetSign.SendNetworkUpdate();

                if(ins.configData.debug)
                    Debug.Log("Pasted image on sign " + targetSign.net.ID + ", " + ins.pasteController.pasteList.Count + " signs left");


                isPasting = false;

                Invoke("PasteNextFromList", ins.configData.pasteDelay);
            }
        }

        private class PasteRequest
        {
            public ulong signID { get; }
            public byte[] imgData { get; }
            public int signX { get; }
            public int signY { get; }

            public PasteRequest(byte[] inpImgData, ulong inpSignID, int coordsX, int coordsY)
            {
                imgData = inpImgData;
                signID = inpSignID;
                signX = coordsX;
                signY = coordsY;
            }
        }

        private static byte[] GetImagePart(Bitmap sourceImage, DownloadRequest request, int targetWidth, int targetHeight, int splitX, int splitY)
        {
            byte[] bytesClipped;

            var streamResized = new MemoryStream();
            Bitmap targetImage = new Bitmap(targetWidth, targetHeight);

            int pixelsX = (splitX - 1) * targetWidth;
            int pixelsY = (splitY - 1) * targetHeight;
            Point[] targetRectPoints = { new Point(0, 0), new Point(targetWidth, 0), new Point(0, targetHeight) };

            using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(targetImage))
            {
                var sourceRectangle = new Rectangle(pixelsX, pixelsY, targetWidth, targetHeight);

                var iattr = new ImageAttributes();

                if (request.brightness != 1f)
                {
                    float b = request.brightness;                    
                    var cm = new ColorMatrix(new float[][]
                    {
                        new float[] {b, 0, 0, 0, 0},
                        new float[] {0, b, 0, 0, 0},
                        new float[] {0, 0, b, 0, 0},
                        new float[] {0, 0, 0, 1, 0},
                        new float[] {0, 0, 0, 0, 1}
                    });
                    iattr.SetColorMatrix(cm);
                }

                graphics.DrawImage(sourceImage, targetRectPoints, sourceRectangle, GraphicsUnit.Pixel, iattr);
               // graphics.DrawImage(sourceImage, targetRectPoints, sourceRectangle, GraphicsUnit.Pixel);
            }

            AddRandomPixel(targetImage);

            targetImage.Save(streamResized, ImageFormat.Png);
            bytesClipped = streamResized.ToArray();
            targetImage.Dispose();

            return bytesClipped;
        }

        #endregion

        #region Commands
        [ConsoleCommand("billboard.toggle")]
        private void consoleCmdZDP(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null)
                return;

            if (!HasPermission(player.UserIDString, permConsole))
                return;

            if (arg.Args.Length > 0)
            {
                ulong posID;
                if (!ulong.TryParse(arg.Args[0], out posID)) return;

                NeonSign mainSign = GetMainSign(posID) as NeonSign;
                if (mainSign == null) return;

                if (configData.debug)
                    Puts("Console command used to toggle power on billboard " + posID);

                SwitchBillBoardPower(mainSign);
            }
        }

        [ChatCommand("billboard")]
        private void cmdBillBoard(BasePlayer player, string command, string[] args)
        {
            var maxSigns = 0;
            var maxBillboards = 0;
            if(HasPermission(player.UserIDString, permAdmin)) {
                maxSigns = configData.maxSignsAdmin;
                maxBillboards = configData.maxBillboardsAdmin;
            }
            else if (HasPermission(player.UserIDString, permTier3)) {
                maxSigns = configData.maxSignsTier3;
                maxBillboards = configData.maxBillboardsTier3;
            }
            else if (HasPermission(player.UserIDString, permTier2)) {
                maxSigns = configData.maxSignsTier2;
                maxBillboards = configData.maxBillboardsTier2;
            }
            else if (HasPermission(player.UserIDString, permTier1)) {
                maxSigns = configData.maxSignsTier1;
                maxBillboards = configData.maxBillboardsTier1;
            }
            else
            {
                player.ChatMessage("No permission");
                return;
            }

            if (args.Length > 0)
            {
                switch (args[0])
                {
                    case "create": CreateBillBoard(player, args, maxSigns, maxBillboards); break;
                    case "remove":
                    case "destroy": DestroyBillBoard(player); break;
                    case "destroyall": DestroyBillBoards(player); break;
                    case "info": ShowBillboardInfo(player); break;
                    case "debug": ToggleDebug(player); break;
                    case "addsign": AddSignToBillboard(player, args); break;
                    case "toggle": ToggleBillBoardPower(player); break;
                    case "speed": ChangeBillBoardSpeed(player, args); break;
                    case "dimmer": ChangeBillBoardDimmer(player, args); break;
                    case "next": CommandPasteNext(player); break;
                    case "emptypaste": CommandPasteEmpty(player); break;
                    case "sil": SilBillBoard(player, args); break;
                    //case "draw": DrawingToBillBoard(player, args); break;
                }
            }
        }
        #endregion

        #region Functions
        // for next version
        //public void DrawingToBillBoard(BasePlayer player, string[] args)
        //{
        //    if (!selectedSigns.ContainsKey(player))
        //        selectedSigns.Add(player, null);

        //    if(args.Length == 3)
        //    {
        //        int cols;
        //        int rows;
        //        if (!int.TryParse(args[1], out cols) || !int.TryParse(args[2], out rows))
        //        {
        //            player.ChatMessage("Incorrect syntax: /billboard draw 3 2");
        //            return;
        //        }

        //        var startSign = GetBillBoard(player);
        //        if (startSign == null) return;

        //        Signage selectedSign;
        //        if (!selectedSigns.TryGetValue(player, out selectedSign) || selectedSign == null)
        //        {
        //            player.ChatMessage("Select a source sign first: /billboard draw select");
        //            return;
        //        }

        //        player.ChatMessage("Drawing painting from sign to billboard");
        //        ProcessDrawing(selectedSign, cols, rows, new DownloadRequest("", player, startSign, cols, rows, false));
        //    }
        //    else if(args.Length > 1)
        //    {
        //        switch(args[1])
        //        {
        //            case "select":
        //                RaycastHit hit;
        //                if (!Physics.Raycast(player.eyes.HeadRay(), out hit, 6f))
        //                {
        //                    player.ChatMessage("Please look at a sign!");
        //                    return;
        //                }

        //                var sourceSign = hit.GetEntity() as Signage;
        //                if(sourceSign == null)
        //                {
        //                    player.ChatMessage("Please look at a sign!");
        //                    return;
        //                }

        //                selectedSigns[player] = sourceSign;
        //                player.ChatMessage("Selected " + sourceSign.ShortPrefabName);
        //                break;
        //        }
        //    }
        //    else
        //    {
        //        player.ChatMessage("Incorrect syntax: /billboard draw select | /billboard draw 3 2");
        //        return;
        //    }
        //}

        public void ValidateAllBillboards()
        {
            var allBB = new Dictionary<ulong, BillBoardData>((IEnumerable<KeyValuePair<ulong, BillBoardData>>)storedData.billBoards);
            Puts("Validating " + allBB.Count + " billboards...");
            foreach(var mainSign  in allBB)
            {
                ValidateBillboard(mainSign.Key);               
            }

            Puts("All billboards validated");
        }

        public void ValidateBillboard(ulong mainSignID)
        {
            BillBoardData billBoardData;
            if(!storedData.billBoards.TryGetValue(mainSignID, out billBoardData))
            {
                Puts("Trying to validate a non-existing billboard");
                return;
            }

            Signage signage = BaseNetworkable.serverEntities.Find(new NetworkableId(mainSignID)) as Signage;
            if (signage == null)
            {
                storedData.billBoards.Remove(mainSignID);

                RemoveFromUserBillboards(mainSignID);

                Puts("Deleted billboard " + mainSignID + " as it does not exist anymore");
                return;
            }

            var speed = 3f;
            if (billBoardData.speed > 0)
                speed = billBoardData.speed;

            Puts("Billboard " + mainSignID + " has " + billBoardData.billBoardSigns.Count + " signs, validating...");
            foreach (var subSign in new List<ulong>(billBoardData.billBoardSigns))
            {
                Signage subSignage = BaseNetworkable.serverEntities.Find(new NetworkableId(subSign)) as Signage;
                if (subSignage == null)
                {
                    storedData.billBoards[mainSignID].billBoardSigns.Remove(subSign);
                    Puts("Deleted sign " + subSign + " from billboard " + mainSignID);
                    continue;
                }

                var subSignNeon = subSignage as NeonSign;
                if (subSignNeon != null)
                {
                    subSignNeon.animationSpeed = speed;
                    subSignNeon.UpdateHasPower(0, 0);
                }
            }

            if (signage is NeonSign)
                SwitchBillBoardPower(signage as NeonSign, 25);

            Puts("Billboard validated");
        }

        public void ToggleBillBoardPower(BasePlayer player)
        {
            var billboardSign = GetBillBoard(player) as NeonSign;
            if (billboardSign == null) return;

            SwitchBillBoardPower(billboardSign);

            player.ChatMessage("Billboard power toggled");
            return;
        }

        public void AddSignToBillboard(BasePlayer player, string[] args)
        {
            if (args.Length == 0) return;

            var newSign = GetBillBoard(player, false);
            if (newSign == null) return;

           

            ulong inpID;
            if (!ulong.TryParse(args[1], out inpID))
            {
                player.ChatMessage("Invalid billboard ID");
                return;
            }


            if (!storedData.billBoards.ContainsKey(inpID))
            {
                player.ChatMessage("Billboard not found");
                return;
            }

            if (storedData.billBoards[inpID].billBoardSigns.Contains(newSign.net.ID.Value))
            {
                player.ChatMessage("Already in billboard!");
                return;
            }

            storedData.billBoards[inpID].billBoardSigns.Add(newSign.net.ID.Value);

            newSign.UpdateHasPower(25, 0);
            newSign.SendNetworkUpdate();

            player.ChatMessage("Sign added to billboard");
            return;
        }

        public void ToggleDebug(BasePlayer player = null)
        {
            configData.debug = !configData.debug;
            SaveConf();

            player.ChatMessage("Debug mode set to: " + configData.debug);
            return;
        }

        private void SwitchBillBoardPower(NeonSign billboardSign, int forcePower = -1)
        {
            int newPower = billboardSign.IsPowered() ? 0 : 25;
            if (forcePower >= 0)
                newPower = forcePower;

            BillBoardData billBoardData;
            if (!ins.storedData.billBoards.TryGetValue(billboardSign.net.ID.Value, out billBoardData)) return;

            foreach (ulong signID in billBoardData.billBoardSigns)
            {
                Signage signage = BaseNetworkable.serverEntities.Find(new NetworkableId(signID)) as Signage;
                if (signage == null) continue;

                Interface.CallHook("OnBillboardPowerToggled", billboardSign.net.ID, signage, newPower > 0);

                signage.UpdateHasPower(newPower, 0);
                signage.SendNetworkUpdate();
            }
        }

        public void ChangeBillBoardSpeed(BasePlayer player, string[] args)
        {
            var billboardSign = GetBillBoard(player) as NeonSign;
            if (billboardSign == null) return;

            if (args.Length != 2)
            {
                player.ChatMessage("Wrong usage: /billboard speed 3");
                return;
            }

            BillBoardData billBoardData;
            if (!storedData.billBoards.TryGetValue(billboardSign.net.ID.Value, out billBoardData))
            {
                player.ChatMessage("Billboard not found");
                return;
            }

            float newSpeed;
            if (!float.TryParse(args[1], out newSpeed) && newSpeed >= 0) {
                player.ChatMessage("Invalid speed, usage: /billboard speed 3");
                return;
            }            

            billBoardData.speed = newSpeed;

            foreach (ulong signID in billBoardData.billBoardSigns)
            {
                NeonSign signage = BaseNetworkable.serverEntities.Find(new NetworkableId(signID)) as NeonSign;
                if (signage == null) continue;

                signage.animationSpeed = newSpeed;
                signage.SendNetworkUpdate();
            }

            SwitchBillBoardPower(billboardSign);
            SwitchBillBoardPower(billboardSign);

            player.ChatMessage("Billboard speed set to: "+ newSpeed);
            return;
        }

        public void ChangeBillBoardDimmer(BasePlayer player, string[] args)
        {
            var billboardSign = GetBillBoard(player);
            if (billboardSign == null) return;

            if (args.Length != 2)
            {
                player.ChatMessage("Wrong usage: /billboard dimmer 2");
                return;
            }

            float newIntensity = 2f;
            float intensity = 1f;
            if (float.TryParse(args[1], out intensity) && intensity >= 0) newIntensity = intensity;

            BillBoardData billBoardData;
            if (!ins.storedData.billBoards.TryGetValue(billboardSign.net.ID.Value, out billBoardData)) return;

            foreach (ulong signID in billBoardData.billBoardSigns)
            {
                NeonSign signage = BaseNetworkable.serverEntities.Find(new NetworkableId(signID)) as NeonSign;
                if (signage == null) return;

                signage.lightIntensity = newIntensity;
                signage.SendNetworkUpdate();
            }

            player.ChatMessage("Billboard dimmer set to: " + newIntensity);
            return;
        }

        public void SilBillBoard(BasePlayer player, string[] args)
        {
            if (args.Length < 2)
            {
                //player.ChatMessage("Incorrect syntax: /billboard sil <url> [darker]");
                player.ChatMessage("Incorrect syntax: /billboard sil <url>");
                return;
            }

            var startSign = GetBillBoard(player);
            if (startSign == null) return;

            BillBoardData billBoardData;
            if(!storedData.billBoards.TryGetValue(startSign.net.ID.Value, out billBoardData))
            {
                player.ChatMessage("This sign does not seem te be part of a billboard");
                return;
            }


            billBoardData.brightness = 1f;

            float customBrightness;
            if (args.Length > 2 && float.TryParse(args[2], out customBrightness) && customBrightness >= 0f && customBrightness <= 100f)
            {
                if (customBrightness > 1f)
                    customBrightness = customBrightness / 100;

                billBoardData.brightness = customBrightness;
            }

            downloadController.QueueDownload(args[1], player, startSign, billBoardData);
            player.ChatMessage("Loading images...");
        }

        public void DestroyBillBoards(BasePlayer player)
        {
            if (!HasPermission(player.UserIDString, permAdmin))
                return;            

            foreach (var billBoardData in storedData.billBoards)
            {
                foreach (ulong signID in billBoardData.Value.billBoardSigns)
                {
                    BaseEntity ent = BaseNetworkable.serverEntities.Find(new NetworkableId(signID)) as BaseEntity;
                    if (ent != null)
                    {
                        ent.Kill();
                    }
                }
            }
            storedData.billBoards.Clear();
            storedData.userBillboards.Clear();
        }

        public void DestroyBillBoard(BasePlayer player)
        {
            var sign = GetBillBoard(player);
            if (sign == null) return;

            DestroyBillBoard(sign.net.ID);

            if (configData.refundOnDestroy)
            {
                var signItemName = sign is NeonSign ? "sign.neon.xl.animated" : "sign.pictureframe.xl";
                var item = ItemManager.CreateByName(signItemName);
                if(item != null)
                    player.GiveItem(item);
            }

            player.ChatMessage("Billboard destroyed");
        }

        private void DestroyBillBoard(NetworkableId iD)
        {
            throw new NotImplementedException();
        }

        void DestroyBillBoard(ulong mainSignID)
        {
            BillBoardData billBoardData;
            if (!ins.storedData.billBoards.TryGetValue(mainSignID, out billBoardData)) return;

            if (billBoardData.destroyed)
                return;

            // prevent destroying the same billboard again
            billBoardData.destroyed = true;

            
            foreach (ulong signID in new List<ulong>(billBoardData.billBoardSigns))
            {
                // remove before kill, so OnEntityKill doesn't find this signage in the billboards list
                billBoardData.billBoardSigns.Remove(signID);

                BaseEntity ent = BaseNetworkable.serverEntities.Find(new NetworkableId(signID)) as BaseEntity;
                if (ent != null)
                    ent.Kill();
            }

            storedData.billBoards.Remove(mainSignID);
            RemoveFromUserBillboards(mainSignID);
        }

        public void RemoveFromUserBillboards(ulong sid)
        {
            var userBBs = new Dictionary<string, List<ulong>>(storedData.userBillboards);
            foreach (var ubb in userBBs)
            {
                if (!ubb.Value.Contains(sid)) continue;

                storedData.userBillboards[ubb.Key].Remove(sid);

                if (storedData.userBillboards[ubb.Key].Count == 0)
                    storedData.userBillboards.Remove(ubb.Key);
            }
        }

        public Signage GetBillBoard(BasePlayer player, bool checkIfBillBoard = true)
        {
            RaycastHit hit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out hit, 6f))
            {
                player.ChatMessage("Please look at a billboard!");
                return null;
            }

            Signage billboardSign = null;
            billboardSign = hit.GetEntity() as Signage;

            
            if (billboardSign == null || (!billboardSign.ShortPrefabName.Contains("sign.neon.xl.animated") && !billboardSign.ShortPrefabName.Contains("sign.pictureframe.xl")))
            {
                player.ChatMessage("Cannot find Billboard / XL Animated Neon sign / XL Pictureframe!");
                return null;
            }

            if(!checkIfBillBoard)
            {
                return billboardSign;
            }

            Signage mainSign = GetMainSign(billboardSign.net.ID.Value);
            if (checkIfBillBoard && mainSign == null)
            {
                player.ChatMessage("Please look at a billboard!");
                return null;
            }
           
            return mainSign;            
        }

        public Signage GetMainSign(ulong raySignID)
        {
            ulong parentSignID = 0;

            foreach (var billBoardData in storedData.billBoards)
            {
                foreach (ulong subSignID in billBoardData.Value.billBoardSigns)
                {
                    if(subSignID == raySignID)
                    {
                        parentSignID = billBoardData.Key;
                        break;
                    }                    
                }

                if (parentSignID != 0) break;
            }

            if (parentSignID == 0)
                return null;

            Signage ent = BaseNetworkable.serverEntities.Find(new NetworkableId(parentSignID)) as Signage;
            if (ent == null)
                return null;

            return ent;        
        }

        public bool IsPartOfBillboard(Signage sign)
        {
            if (sign == null || sign.net == null)
                return false;

            if (storedData.billBoards.ContainsKey(sign.net.ID.Value))
                return true;

            foreach (var billBoardData in storedData.billBoards)
            {
                if (billBoardData.Value.billBoardSigns.Contains(sign.net.ID.Value))
                    return true;
            }

            return false;
        }

        public void ShowBillboardInfo(BasePlayer player)
        {
            var billboard = GetBillBoard(player);
            if(billboard != null)
                player.ChatMessage("Billboard ID: " + billboard.net.ID + "\nLocation: " + billboard.transform.position);
        }

        public void CreateBillBoard(BasePlayer player, string[] args, int maxSigns, int maxBillboards)
        {
            int horizontal = 3;
            int vertical = 2;

            List<ulong> userBillboards;
            if(maxBillboards > 0 && storedData.userBillboards.TryGetValue(player.UserIDString, out userBillboards) && userBillboards.Count >= maxBillboards)
            {
                player.ChatMessage("Failed creating billboard: You have reached the limit (" + maxBillboards + ")");
                return;
            }

            if (args.Length == 3)
            {
                int inputH;
                int inputV;
                if (int.TryParse(args[1], out inputH) && int.TryParse(args[2], out inputV) && inputH > 0 && inputV > 0 && (inputH * inputV) < maxSigns)
                {
                    horizontal = inputH;
                    vertical = inputV;
                }
            }

            if(!HasPermission(player.UserIDString, permAdmin) && player.IsBuildingBlocked())
            {
                player.ChatMessage("You can not create billboards in building blocked zones");
                return;
            }

            if(horizontal * vertical > maxSigns)
            {
                player.ChatMessage("The total of signs can not be higher than " + maxSigns);
                return;
            }

            var startSign = GetBillBoard(player, false);
            if (startSign == null)
            {
                player.ChatMessage("Can not find the main billboard sign");
                return;
            }

            if (storedData.billBoards.ContainsKey(startSign.net.ID.Value))
            {
                player.ChatMessage("This is already a billboard");
                return;
            }

            storedData.billBoards.Add(startSign.net.ID.Value, new BillBoardData());

            BillBoardData billBoardData;
            if (!storedData.billBoards.TryGetValue(startSign.net.ID.Value, out billBoardData))
            {
                player.ChatMessage("Something went wrong while creating the billboard");
                return;
            }

            billBoardData.billBoardSigns.Add(startSign.net.ID.Value);
            billBoardData.signsHorizontal = horizontal;
            billBoardData.signsVertical = vertical;

            if (!storedData.userBillboards.ContainsKey(player.UserIDString))
                storedData.userBillboards.Add(player.UserIDString, new List<ulong>());
            storedData.userBillboards[player.UserIDString].Add(startSign.net.ID.Value);

            if (configData.lockSigns)
                startSign.SetFlag(BaseEntity.Flags.Locked, true, false, true);

            string prefab = "assets/prefabs/misc/xmas/neon_sign/sign.neon.xl.animated.prefab";
            float offset = -2.5f;
            var rotation = startSign.transform.rotation;

            var startSignNeon = startSign as NeonSign;
            if (startSignNeon != null)
            {
                startSignNeon.UpdateHasPower(25, 0);
                startSignNeon.SendNetworkUpdate();                
            }
            else
            {
                prefab = "assets/prefabs/deployable/signs/sign.pictureframe.xl.prefab";
                offset = -2.7f;
            }

            

            for (int col = 0; col < vertical; col++)
            {
                for (int row = 0; row < horizontal; row++)
                {
                    if (row == 0 && col == 0) continue;

                    var newPosition = startSign.transform.position + (startSign.transform.right * row * offset) + (startSign.transform.up * col * offset);

                    Signage sign = GameManager.server.CreateEntity(prefab, newPosition, rotation) as Signage;
                    if (sign == null) return;
                    sign.Spawn();
                    sign.OwnerID = player.userID;

                    if (configData.lockSigns)
                        sign.SetFlag(BaseEntity.Flags.Locked, true, false, true);

                    if (sign is NeonSign)
                    {
                        sign.UpdateHasPower(25, 0);
                        sign.SendNetworkUpdate();
                    }

                    //storedData.billBoards[startSign.net.ID].billBoardSigns.Add(sign.net.ID);
                    billBoardData.billBoardSigns.Add(sign.net.ID.Value);
                    
                    if(configData.debug)
                        Puts("Billboard part " + row + " x " + col + " spawned at " + newPosition);
                }
            }
        }
        
        public void CommandPasteNext(BasePlayer player)
        {
            if (!HasPermission(player.UserIDString, permAdmin))
                return;

            if (pasteController != null)
            {
                pasteController.PasteNextFromList();
                player.ChatMessage("Pasting next frame...");
                return;
            }
        }

        public void CommandPasteEmpty(BasePlayer player)
        {
            if (!HasPermission(player.UserIDString, permAdmin))
                return;

            if (pasteController != null)
            {
                pasteController.ClearPasteList();
                player.ChatMessage("PasteList cleared...");
                return;
            }
        }
        #endregion

        #region Helpers
        private static void AddRandomPixel(Bitmap image)
        {
            var color = System.Drawing.Color.FromArgb(
                UnityEngine.Random.Range(0, 255),
                UnityEngine.Random.Range(0, 255),
                UnityEngine.Random.Range(0, 255),
                128 //Alpha (transparency)
            );
            image.SetPixel(image.Width - 1, 1, color);
        }

        bool HasPermission(string id, string perm) => permission.UserHasPermission(id, perm);

        #endregion

        #region Config
        private ConfigData configData;

        class ConfigData
        {
            [JsonProperty(PropertyName = "Maximum amount of signs in total (width x height) Tier 1")]
            public int maxSignsTier1 = 6;

            [JsonProperty(PropertyName = "Maximum amount of billboards (any size, 0 = unlimited) Tier 1")]
            public int maxBillboardsTier1 = 1;

            [JsonProperty(PropertyName = "Maximum amount of signs in total (width x height) Tier 2")]
            public int maxSignsTier2 = 12;

            [JsonProperty(PropertyName = "Maximum amount of billboards (any size, 0 = unlimited) Tier 2")]
            public int maxBillboardsTier2 = 3;

            [JsonProperty(PropertyName = "Maximum amount of signs in total (width x height) Tier 3")]
            public int maxSignsTier3 = 16;

            [JsonProperty(PropertyName = "Maximum amount of billboards (any size, 0 = unlimited) Tier 3")]
            public int maxBillboardsTier3 = 5;

            [JsonProperty(PropertyName = "Maximum amount of signs in total (width x height) Admin")]
            public int maxSignsAdmin = 150;

            [JsonProperty(PropertyName = "Maximum amount of billboards (any size, 0 = unlimited) Admin")]
            public int maxBillboardsAdmin = 0;

            [JsonProperty(PropertyName = "Width and height of each neon sign image in pixels")]
            public int imageSize = 150;

            [JsonProperty(PropertyName = "Seconds between pasting images")]
            public float pasteDelay = 0.5f;

            [JsonProperty(PropertyName = "Brightness of the images pasted on the billboards (experimental)")]
            public float brightness = 0.5f;

            [JsonProperty(PropertyName = "Lock signs to owner after creating billboard")]
            public bool lockSigns = false;

            [JsonProperty(PropertyName = "Give back a Neon Sign when a billboard is removed with the destroy command")]
            public bool refundOnDestroy = true;

            [JsonProperty(PropertyName = "Destroy billboard when any of it's signs gets removed, picked up or destroyed")]
            public bool destroyOnMissingSigns = true;

            [JsonProperty(PropertyName = "Log extra output to console")]
            public bool debug = false;
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

        #region Data
        StoredData storedData;
        class StoredData
        {
            public Dictionary<ulong, BillBoardData> billBoards = new Dictionary<ulong, BillBoardData>();
            public Dictionary<string, List<ulong>> userBillboards = new Dictionary<string, List<ulong>>();
        }

        class BillBoardData
        {
            public List<ulong> billBoardSigns = new List<ulong>();
            public int signsHorizontal = 0;
            public int signsVertical = 0;
            public float speed = 3f;
            public float brightness = 1f;
            public bool destroyed = false;
        }

        void Loaded()
        {
            LoadData();
        }

        public void LoadData()
        {
            try
            {
                storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(dataFileName);
            }
            catch
            {
                storedData = new StoredData();
                Puts("Invalid datafile, new file generated");

                SaveData();
            }

            if (storedData == null)
            {
                storedData = new StoredData();
                Puts("Invalid datafile, new file generated");

                SaveData();
            }
        }

        void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(dataFileName, storedData);
        }
        #endregion
    }
}
