using Newtonsoft.Json;
using Oxide.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{	
    [Info("Sign Artist", "Nimant", "1.0.4")]    
    class SignArtist : RustPlugin
    {
		
		#region Variables 
		
        private static Dictionary<ulong, float> cooldowns = new Dictionary<ulong, float>();
		private static Dictionary<uint, ImageInfo> SignUploads = new Dictionary<uint, ImageInfo>();
		private static Dictionary<string, ImageSize> ImageSizePerAsset = new Dictionary<string, ImageSize>();
        private GameObject imageDownloaderGameObject;
        private ImageDownloader imageDownloader;                
        
		private class ImageInfo
		{
			public ulong userID;
			public string url;
			public string pos;
		}
		
        private class DownloadRequest
        {
            public BasePlayer Sender;
            public Signage Sign;
            public string Url;
            public bool Raw;
            
            public DownloadRequest(string url, BasePlayer player, Signage sign, bool raw)
            {
                Url = url;
                Sender = player;
                Sign = sign;
                Raw = raw;
            }
        }

        private class RestoreRequest
        {            
            public Signage Sign;
            public bool Raw;
            
            public RestoreRequest(Signage sign, bool raw)
            {                
                Sign = sign;
                Raw = raw;
            }
        }
        
        public class ImageSize
        {
            public int Width;
            public int Height;			
            public int ImageWidth;
            public int ImageHeight;
			public int ImageOffsetX;
            public int ImageOffsetY;
            
            public ImageSize(int width, int height) : this(width, height, 0, 0, width, height) { }
            
            public ImageSize(int width, int height, int imageOffsetX, int imageOffsetY, int imageWidth, int imageHeight)
            {
                Width = width;
                Height = height;
                ImageWidth = imageWidth;
                ImageHeight = imageHeight;
				ImageOffsetX = imageOffsetX;
                ImageOffsetY = imageOffsetY;
            }
        }
		
		#endregion
        
		#region ImageDownloader class
		
        private class ImageDownloader : MonoBehaviour
        {
            private byte activeDownloads;
            private byte activeRestores;

            private readonly SignArtist signArtist = (SignArtist)Interface.Oxide.RootPluginManager.GetPlugin(nameof(SignArtist));
            private readonly Queue<DownloadRequest> downloadQueue = new Queue<DownloadRequest>();
            private readonly Queue<RestoreRequest> restoreQueue = new Queue<RestoreRequest>();
            
            public void QueueDownload(string url, BasePlayer player, Signage sign, bool raw)
            {                
                bool existingRequest = downloadQueue.Any(request => request.Sign == sign) || restoreQueue.Any(request => request.Sign == sign);
                if (existingRequest)
                {
                    signArtist.SendMessage(player, "ActionQueuedAlready");
                    return;
                }
                
                downloadQueue.Enqueue(new DownloadRequest(url, player, sign, raw));                
                StartNextDownload();
            }
            
            public void QueueRestore(Signage sign, bool raw)
            {                
                bool existingRequest = downloadQueue.Any(request => request.Sign == sign) || restoreQueue.Any(request => request.Sign == sign);
                if (existingRequest)                										
                    return;                
                
                restoreQueue.Enqueue(new RestoreRequest(sign, raw));                
                StartNextRestore();
            }
            
            private void StartNextDownload(bool reduceCount = false)
            {                
                if (reduceCount)                
                    activeDownloads--;                
                
                if (activeDownloads >= configData.MaxActiveDownloads)                
                    return;                
                
                if (downloadQueue.Count <= 0)                
                    return;                
                
                activeDownloads++;
                StartCoroutine(DownloadImage(downloadQueue.Dequeue()));
            }
            
            private void StartNextRestore(bool reduceCount = false)
            {
                if (reduceCount)                
                    activeRestores--;                
                
                if (activeRestores >= configData.MaxActiveDownloads)                
                    return;                
                
                if (restoreQueue.Count <= 0)                
                    return;                
                
                activeRestores++;
                StartCoroutine(RestoreImage(restoreQueue.Dequeue()));
            }
            
            private IEnumerator DownloadImage(DownloadRequest request)
            {
                using (WWW www = new WWW(request.Url))
                {                    
                    yield return www;
                 
                    if (signArtist == null)                    
                        throw new NullReferenceException("signArtist");                    
                    
                    if (www.error != null)
                    {                        
                        signArtist.SendMessage(request.Sender, "WebErrorOccurred", www.error);
                        StartNextDownload(true);

                        yield break;
                    }
                    
                    if (configData.MaxSizeKB > 0 && www.bytesDownloaded > configData.MaxSizeKB*1024)
                    {                        
                        signArtist.SendMessage(request.Sender, "FileTooLarge", configData.MaxSizeKB);
                        StartNextDownload(true);

                        yield break;
                    }
                    
                    byte[] imageBytes;

                    if (request.Raw)                    
                        imageBytes = www.bytes;                    
                    else                    
                        imageBytes = GetImageBytes(www);                    

                    ImageSize size = GetImageSizeFor(request.Sign);
                    
                    if (size == null || imageBytes == null)
                    {
                        signArtist.SendMessage(request.Sender, "ErrorOccurred");
                        signArtist.PrintWarning($"Couldn't find the required image size for {request.Sign.PrefabName}, please report this in the plugin's thread.");
                        StartNextDownload(true);

                        yield break;
                    }
                    
                    byte[] resizedImageBytes = ResizeImage(imageBytes, size, configData.EnforceJpeg && !request.Raw);

                    if (configData.MaxSizeKB > 0 && resizedImageBytes.Length > configData.MaxSizeKB*1024)
                    {
                        signArtist.SendMessage(request.Sender, "FileTooLarge", configData.MaxSizeKB);
                        StartNextDownload(true);

                        yield break;
                    }
					
					if (configData.MaxWidth > 0 && configData.MaxHeight > 0)
					{	
						using (MemoryStream originalBytesStream = new MemoryStream())
						{			
							originalBytesStream.Write(imageBytes, 0, imageBytes.Length);
							Bitmap image = new Bitmap(originalBytesStream);
						
							if (image.Width > configData.MaxWidth)
							{
								signArtist.SendMessage(request.Sender, "FileSizeWLarge", configData.MaxWidth);
								StartNextDownload(true);
								yield break;
							}	
							if (image.Height > configData.MaxHeight)
							{
								signArtist.SendMessage(request.Sender, "FileSizeHLarge", configData.MaxHeight);
								StartNextDownload(true);
								yield break;
							}	
						}
					}				
                   
                    if (request.Sign.textureID > 0)
                        FileStorage.server.Remove(request.Sign.textureID, FileStorage.Type.png, request.Sign.net.ID);                    
                    
                    request.Sign.textureID = FileStorage.server.Store(resizedImageBytes, FileStorage.Type.png, request.Sign.net.ID);
                    request.Sign.SendNetworkUpdate();
                    
                    signArtist.SendMessage(request.Sender, "ImageLoaded");
					
					if (request.Sender != null)
						signArtist.SetCooldown(request.Sender);
					
                    signArtist.SetImageOwner(request.Sender, request.Sign.net.ID, request.Url, request.Sign.transform.position);
                    Interface.Oxide.CallHook("OnSignUpdated", request.Sign, request.Sender);                                        					
					
					if (request.Sender != null)
					{
						var message = string.Format(signArtist.GetTranslation("LogEntry"), request.Sender.displayName,
									  request.Sender.userID, request.Url, request.Sign.ShortPrefabName, request.Sign.transform.position);
						signArtist.Puts(message);
						
						if (configData.FileLogging)                    					
							signArtist.LogToFile("log", message, signArtist);                    
					}
                    
                    StartNextDownload(true);
                }
            }
            
            private IEnumerator RestoreImage(RestoreRequest request)
            {
                if (signArtist == null)                
                    throw new NullReferenceException("signArtist");                

                byte[] imageBytes;
                
                if (!SignUploads.ContainsKey(request.Sign.net.ID))
                {                 					
                    StartNextRestore(true);
                    yield break;
                }				                				
				
				if (request.Sign.textureID == 0)
				{																												
					downloadQueue.Enqueue(new DownloadRequest(SignUploads[request.Sign.net.ID].url, null, request.Sign, false));                
					StartNextDownload();
					
					StartNextRestore(true);
                    yield break;
				}
								
                imageBytes = FileStorage.server.Get(request.Sign.textureID, FileStorage.Type.png, request.Sign.net.ID);
                ImageSize size = GetImageSizeFor(request.Sign);
                
                if (size == null || imageBytes == null)
                {                                        
                    downloadQueue.Enqueue(new DownloadRequest(SignUploads[request.Sign.net.ID].url, null, request.Sign, false));                
					StartNextDownload();
					
                    StartNextRestore(true);
                    yield break;
                }
                											
                byte[] resizedImageBytes = ResizeImage(imageBytes, size, configData.EnforceJpeg && !request.Raw);								
                
                request.Sign.textureID = FileStorage.server.Store(resizedImageBytes, FileStorage.Type.png, request.Sign.net.ID);
                request.Sign.SendNetworkUpdate();
								
				FileStorage.server.Remove(request.Sign.textureID, FileStorage.Type.png, request.Sign.net.ID);
                                            
                Interface.Oxide.CallHook("OnSignUpdated", request.Sign, null);                
                StartNextRestore(true);
            }
            
            private ImageSize GetImageSizeFor(Signage signage)
            {
                if (ImageSizePerAsset.ContainsKey(signage.PrefabName))                
                    return ImageSizePerAsset[signage.PrefabName];                

                return null;
            }
            
            private byte[] GetImageBytes(WWW www)
            {
                Texture2D texture = www.texture;
                byte[] image;

                if (texture.format == TextureFormat.ARGB32 && !configData.EnforceJpeg)                
                    image = texture.EncodeToPNG();                
                else                
                    image = texture.EncodeToJPG(configData.Quality);                

                DestroyImmediate(texture);
                return image;
            }
			
			private bool IsTextureUse(uint textureID, uint ID)
			{
				foreach (Signage sign in BaseNetworkable.serverEntities.OfType<Signage>())
				{
					if (sign.net.ID == ID) continue;
					if (sign.textureID == textureID)
						return true;
				}
				
				return false;
			}
			
			private uint GetOriginalTexture(uint ID)
			{
				if (!SignUploads.ContainsKey(ID)) 
					return 0;
				
				var currInfo = SignUploads[ID];
				
				foreach(var pair in SignUploads.Where(x=> x.Value.url == currInfo.url))
				{
					if (pair.Key == ID) continue;
					
					var sign = BaseNetworkable.serverEntities.OfType<Signage>().FirstOrDefault(x=> x.net.ID == pair.Key);
					if (sign == null) continue;
					
					if (sign.textureID > 0)
						return sign.textureID;					
				}
				
				return 0;
			}
			
			private void UpdateOldTexture(uint oldTextureID, uint newTextureID)
			{
				foreach (Signage sign in BaseNetworkable.serverEntities.OfType<Signage>().ToList())
				{
					if (sign.textureID == oldTextureID)
					{
						sign.textureID = newTextureID;
						sign.SendNetworkUpdate();
					}
				}
			}
        }
		
		#endregion
		
		#region Hooks
        
        private void Init()
        {
			LoadVariables();			            
			LoadData();
			foreach(var priv in configData.Privileges) permission.RegisterPermission(priv.Key, this);
            
            ImageSizePerAsset = new Dictionary<string, ImageSize>()
            {
                ["assets/prefabs/deployable/signs/sign.pictureframe.landscape.prefab"] = new ImageSize(256, 128), // Landscape Picture Frame
                ["assets/prefabs/deployable/signs/sign.pictureframe.portrait.prefab"] = new ImageSize(128, 256),  // Portrait Picture Frame
                ["assets/prefabs/deployable/signs/sign.pictureframe.tall.prefab"] = new ImageSize(128, 512),      // Tall Picture Frame
                ["assets/prefabs/deployable/signs/sign.pictureframe.xl.prefab"] = new ImageSize(512, 512),        // XL Picture Frame
                ["assets/prefabs/deployable/signs/sign.pictureframe.xxl.prefab"] = new ImageSize(1024, 512),      // XXL Picture Frame
             
                ["assets/prefabs/deployable/signs/sign.small.wood.prefab"] = new ImageSize(128, 64),              // Small Wooden Sign
                ["assets/prefabs/deployable/signs/sign.medium.wood.prefab"] = new ImageSize(256, 128),            // Wooden Sign
                ["assets/prefabs/deployable/signs/sign.large.wood.prefab"] = new ImageSize(256, 128),             // Large Wooden Sign
                ["assets/prefabs/deployable/signs/sign.huge.wood.prefab"] = new ImageSize(512, 128),              // Huge Wooden Sign

                ["assets/prefabs/deployable/signs/sign.hanging.banner.large.prefab"] = new ImageSize(64, 256),    // Large Banner Hanging
                ["assets/prefabs/deployable/signs/sign.pole.banner.large.prefab"] = new ImageSize(64, 256),       // Large Banner on Pole

                ["assets/prefabs/deployable/signs/sign.hanging.prefab"] = new ImageSize(128, 256),                // Two Sided Hanging Sign
                ["assets/prefabs/deployable/signs/sign.hanging.ornate.prefab"] = new ImageSize(256, 128),         // Two Sided Ornate Hanging Sign
                
                ["assets/prefabs/deployable/signs/sign.post.single.prefab"] = new ImageSize(128, 64),             // Single Sign Post
                ["assets/prefabs/deployable/signs/sign.post.double.prefab"] = new ImageSize(256, 256),            // Double Sign Post
                ["assets/prefabs/deployable/signs/sign.post.town.prefab"] = new ImageSize(256, 128),              // One Sided Town Sign Post
                ["assets/prefabs/deployable/signs/sign.post.town.roof.prefab"] = new ImageSize(256, 128),         // Two Sided Town Sign Post
                
                ["assets/prefabs/deployable/spinner_wheel/spinner.wheel.deployed.prefab"] = new ImageSize(512, 512, 0, 11, 270, 270), // Spinning Wheel
            };
        }                                   
        
        private void OnServerInitialized()
        {            
            imageDownloaderGameObject = new GameObject("ImageDownloader");
            imageDownloader = imageDownloaderGameObject.AddComponent<ImageDownloader>();
			ClearEmpty();
			timer.Once(2.2f, RestoreAllSings);
        }
        
        private void Unload() => UnityEngine.Object.Destroy(imageDownloaderGameObject);                    
		
		private void OnNewSave()
		{
			SignUploads.Clear();
			SaveData();
		}
		
		private void OnServerSave() => ClearEmpty();
		
		#endregion

        #region Commands
		
        [ChatCommand("sil")]
        private void SilChatCommand(BasePlayer player, string command, string[] args)
        {    
			if (!HasPermission(player) && !player.IsAdmin)
            {                
                SendMessage(player, "NoPermission");
                return;
            }			
				
			if (args.Length < 1)
			{         
				if (!player.IsAdmin)
					SendMessage(player, "SyntaxSilCommand");
				else
					SendMessage(player, "SyntaxSilCommandAdmin");
				return;
			}			                        
            			
			if (!player.IsAdmin && HasUploadLimit(player))
			{                
                SendMessage(player, "UploadLimit");
                return;
            }			

			if (!player.IsAdmin && HasCooldown(player))
            {                
                SendMessage(player, "Cooldown", TimeToString(GetCooldown(player)));
                return;
            }			
			
            Signage sign;
            if (!IsLookingAtSign(player, out sign))
            {                
                SendMessage(player, "NoSignFound");
                return;
            }
            
            if (!player.IsAdmin && !CanChangeSign(player, sign))
            {
                SendMessage(player, "SignNotOwned");
                return;
            }                        
             
			if (player.IsAdmin && args[0].ToLower()=="who")
			{
				if (!SignUploads.ContainsKey(sign.net.ID))				
					SendMessage(player, "WhoNobody");					
				else
					SendMessage(player, "WhoLastUpdater", GetPlayerName(SignUploads[sign.net.ID].userID), player.userID);
				return;
			}	
						
            imageDownloader.QueueDownload(args[0], player, sign, false);            												
			SendMessage(player, "DownloadQueued");
        }                         

		#endregion
		
        #region Common
		
        private bool HasCooldown(BasePlayer player)
        {          
			if (player.IsAdmin) return false;			
			
			int cooldown = 0;			
			foreach(var priv in configData.Privileges.OrderBy(x=>x.Value.Cooldown))
			{				
				if (permission.UserHasPermission(player.UserIDString, priv.Key))
				{
					cooldown = priv.Value.Cooldown;
					break;
				}
			}
			
            if (cooldown <= 0) return false;
                                    
            if (!cooldowns.ContainsKey(player.userID))            
                cooldowns.Add(player.userID, 0);            
            			
            return (Time.realtimeSinceStartup - cooldowns[player.userID]) < cooldown;
        }
        
        private long GetCooldown(BasePlayer player)
		{
			int cooldown = 0;			
			foreach(var priv in configData.Privileges.OrderBy(x=>x.Value.Cooldown))
			{				
				if (permission.UserHasPermission(player.UserIDString, priv.Key))
				{
					cooldown = priv.Value.Cooldown;					
					break;
				}
			}						
			
			return cooldown - (long)Math.Round(Time.realtimeSinceStartup - cooldowns[player.userID]);			
		}	
        
        private void SetCooldown(BasePlayer player)
        {
			if (player.IsAdmin) return;
			
			int cooldown = 0;			
			foreach(var priv in configData.Privileges.OrderBy(x=>x.Value.Cooldown))
			{
				if (permission.UserHasPermission(player.UserIDString, priv.Key))
				{
					cooldown = priv.Value.Cooldown;
					break;
				}
			}
			
            if (cooldown <= 0) return;						                       
            
            if (!cooldowns.ContainsKey(player.userID))            
                cooldowns.Add(player.userID, 0);            
            
            cooldowns[player.userID] = Time.realtimeSinceStartup;
        }

		private bool HasUploadLimit(BasePlayer player)
        {          
			if (player.IsAdmin) return false;
						
			foreach(var priv in configData.Privileges.OrderBy(x=>x.Value.MaxUploads))
			{
				if (permission.UserHasPermission(player.UserIDString, priv.Key))
				{
					if (priv.Value.MaxUploads <= 0)
						return false;
				}
			}
			foreach(var priv in configData.Privileges.OrderByDescending(x=>x.Value.MaxUploads))
			{
				if (permission.UserHasPermission(player.UserIDString, priv.Key))
				{
					if (SignUploads.Values.Where(x=> x.userID == player.userID).Count() >= priv.Value.MaxUploads)
						return true;
					
					return false;
				}
			}
			
            return false;
        }				
		
		private void SetImageOwner(BasePlayer player, uint signID, string url, Vector3 pos)
		{
			ulong userID = 0;
			
			if (!SignUploads.ContainsKey(signID))
				SignUploads.Add(signID, new ImageInfo());
			else
				userID = SignUploads[signID].userID;
			
			SignUploads[signID].userID = player != null ? player.userID : userID;
			SignUploads[signID].url = url;
			SignUploads[signID].pos = pos.ToString();
			
			SaveData();
		}
		
		private static bool IsElementExists(int val, int[] mas) {
			foreach (int elem in mas)
				if (elem==val)
					return true;
			return false;
		}
		
		private static string GetStringDays(int days)
		{
			if (IsElementExists(days, (new int[] {1,21})))
				return days.ToString()+" день";
			else if (IsElementExists(days, (new int[] {2,3,4,22,23,24})))
				return days.ToString()+" дня";
			else return days.ToString()+" дней";						
		}
		
		private static string GetStringHours(int hours)
		{
			if (IsElementExists(hours, (new int[] {1,21})))
				return hours.ToString()+" час";
			else if (IsElementExists(hours, (new int[] {2,3,4,22,23,24})))
				return hours.ToString()+" часа";
			else return hours.ToString()+" часов";						
		}
		
		private static string GetStringMinutes(int minutes)
		{
			if (IsElementExists(minutes, (new int[] {1,21,31,41,51})))
				return minutes.ToString()+" минута";
			else if (IsElementExists(minutes, (new int[] {2,3,4,22,23,24,32,33,34,42,43,44,52,53,54})))
				return minutes.ToString()+" минуты";
			else return minutes.ToString()+" минут";						
		}
		
		private static string GetStringSeconds(int seconds)
		{
			if (IsElementExists(seconds, (new int[] {1,21,31,41,51})))
				return seconds.ToString()+" секунда";
			else if (IsElementExists(seconds, (new int[] {2,3,4,22,23,24,32,33,34,42,43,44,52,53,54})))
				return seconds.ToString()+" секунды";
			else return seconds.ToString()+" секунд";						
		}
		
		private string TimeToString(long time)
        {
            TimeSpan elapsedTime = TimeSpan.FromSeconds(time);
            int hours = elapsedTime.Hours;
            int minutes = elapsedTime.Minutes;
            int seconds = elapsedTime.Seconds;
            int days = Mathf.FloorToInt((float)elapsedTime.Days);
            string s = "";
			int count = 0;

            if (days > 0) 
			{	
				s += GetStringDays(days) + ", ";
				if (++count==2) return s.Trim(' ',',');
			}	
            if (hours > 0) 
			{					
				s += GetStringHours(hours) + ", ";
				if (++count==2) return s.Trim(' ',',');
			}	
            if (minutes > 0) 
			{
				s += GetStringMinutes(minutes) + ", ";
				if (++count==2) return s.Trim(' ',',');
			}	
            if (seconds > 0)
			{
				s += GetStringSeconds(seconds) + ", ";
				if (++count==2) return s.Trim(' ',',');
			}	            					
			
            return s.Trim(' ',',');
        }		        
        
        private bool IsLookingAtSign(BasePlayer player, out Signage sign)
        {
            RaycastHit hit;
            sign = null;
            
            if (Physics.Raycast(player.eyes.HeadRay(), out hit, configData.MaxDistance))
                sign = hit.GetEntity() as Signage;            
            
            return sign != null;
        }
        
        private bool CanChangeSign(BasePlayer player, Signage sign) => sign.CanUpdateSign(player);        
        
        private bool HasPermission(BasePlayer player)
        {
			foreach(var priv in configData.Privileges)
				if (permission.UserHasPermission(player.UserIDString, priv.Key))
					return true;
			
			return false;
        }
        
        private void SendMessage(BasePlayer player, string key, params object[] args)
        {
			if (player != null)
				player.ChatMessage(string.Format(GetTranslation(key, player), args));
        }
        
        private string GetTranslation(string key, BasePlayer player = null)
        {
            return lang.GetMessage(key, this, player?.UserIDString);
        }				
		
		private static byte[] ResizeImage(byte[] bytes, ImageSize size, bool enforceJpeg)
		{
			byte[] resizedImageBytes;

			using (MemoryStream originalBytesStream = new MemoryStream(), resizedBytesStream = new MemoryStream())
			{			
				originalBytesStream.Write(bytes, 0, bytes.Length);
				Bitmap image = new Bitmap(originalBytesStream);
			
				if (image.Width != size.Width || image.Height != size.Height)
				{
					Bitmap resizedImage = new Bitmap(size.Width, size.Height);
				
					using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(resizedImage))
					{
						graphics.DrawImage(image, new Rectangle(size.ImageOffsetX, size.ImageOffsetY, size.ImageWidth, size.ImageHeight));
					}

					if (enforceJpeg)					
						resizedImage.Save(resizedBytesStream, ImageFormat.Jpeg);					
					else					
						resizedImage.Save(resizedBytesStream, ImageFormat.Png);					
					
					resizedImageBytes = resizedBytesStream.ToArray();
					resizedImage.Dispose();
				}
				else				
					resizedImageBytes = bytes;				
				
				image.Dispose();
			}
			
			return resizedImageBytes;
		}
		
		private static string EscapeForUrl(string stringToEscape)
		{
			stringToEscape = Uri.EscapeDataString(stringToEscape);
			stringToEscape = stringToEscape.Replace("%5Cr%5Cn", "%5Cn").Replace("%5Cr", "%5Cn").Replace("%5Cn", "%0A");
			
			return stringToEscape;
		}
		
		private void RestoreAllSings()
		{			            
            foreach (Signage sign in BaseNetworkable.serverEntities.OfType<Signage>())            
                imageDownloader.QueueRestore(sign, false);            
		}
		
		private string GetPlayerName(ulong userID)
		{
			var data = permission.GetUserData(userID.ToString());															
			return data.LastSeenNickname;
		}
		
		private static void ClearEmpty()
		{						
			var allSigns = BaseNetworkable.serverEntities.OfType<Signage>().ToList();
			var toDel = SignUploads.Keys.Where(x=> !allSigns.Exists(y=> y.net.ID == x)).ToList();
			
			foreach(var signId in toDel)			
				SignUploads.Remove(signId);
			
			if (toDel.Count() > 0) SaveData();
		}
		
		#endregion
		
		#region Lang
		
		protected override void LoadDefaultMessages()
        {            
            lang.RegisterMessages(new Dictionary<string, string>
            {                
                ["WebErrorOccurred"] = "Ошибка при загрузке изображения: {0}.",
                ["FileTooLarge"] = "Ошибка при загрузке изображения, превышен максимально допустимый размер изображения в {0} Кб.",
				["FileSizeWLarge"] = "Ошибка при загрузке изображения, изображение превышает допустимый размер по ширине в {0} пикселей.",
				["FileSizeHLarge"] = "Ошибка при загрузке изображения, изображение превышает допустимый размер по высоте в {0} пикселей.",
                ["ErrorOccurred"] = "Неизвестная ошибка при загрузке изображения.",                
                ["DownloadQueued"] = "Изображение добавлено в очередь загрузки.",                                
                ["ImageLoaded"] = "Загрузка изображения завершена.",                
                ["LogEntry"] = "Игрок \"{0} ({1})\" загрузил изображение {2} на {3} в точке {4}.",
                ["NoSignFound"] = "Табличка не найдена, подойдите ближе.",
                ["Cooldown"] = "Подождите {0} перед повторной загрузкой изображения.",
				["UploadLimit"] = "Вы загрузили максимальное количество изображений. Уничтожьте одну из ваших табличек с изображением и попробуйте снова.",
                ["SignNotOwned"] = "Вы не можете загружать на эту табличку, т.к. она защищена шкафом.",
                ["ActionQueuedAlready"] = "Табличка занята, дождитесь загрузки предыдущего изображения.",
                ["SyntaxSilCommand"] = "ДОСТУПНЫЕ КОМАНДЫ:\n/sil \"ссылка\" - загрузить изображение на табличку.",                
				["SyntaxSilCommandAdmin"] = "ДОСТУПНЫЕ КОМАНДЫ:\n/sil \"ссылка\" - загрузить изображение на табличку\n/sil who - получить информацию о табличке.",                
                ["NoPermission"] = "У вас нет доступа к этой команде!",
				["WhoNobody"] = "Не найден владелец изображения.",         
				["WhoLastUpdater"] = "Изображение загрузил \"{0} ({1})\"."        				
            }, this);
        } 
		
		#endregion
		
		#region Config        				
		
        private static ConfigData configData;
		
        private class ConfigData
        {            			
            [JsonProperty(PropertyName = "Максимальное количество одновременных загрузок")]
            public int MaxActiveDownloads;
            [JsonProperty(PropertyName = "Максимальная дистанция для поиска таблички")]
            public int MaxDistance;
            [JsonProperty(PropertyName = "Максимальный размер изображения (в килобайтах)")]
            public float MaxSizeKB;
            [JsonProperty(PropertyName = "Конвертировать все изображение в формат JPG (рекомендуется)")]
            public bool EnforceJpeg;
            [JsonProperty(PropertyName = "Уровень сжатия изображения в формате JPG (0-100)")]
            public int Quality;            			
			[JsonProperty(PropertyName = "Максимальная ширина изображения")]
            public int MaxWidth;			
			[JsonProperty(PropertyName = "Максимальная высота изображения")]
            public int MaxHeight;
            [JsonProperty("Вести лог успешных загрузок")]
            public bool FileLogging;            			
			[JsonProperty("Привилегии для ограничения загрузки изображений")]
			public Dictionary<string, PrivLimit> Privileges;
        }
		
		private class PrivLimit
		{
			[JsonProperty(PropertyName = "Длительность задержки между загрузками (в секундах)")]
            public int Cooldown;			
			[JsonProperty(PropertyName = "Максимальное количество загруженных изображений")]
            public int MaxUploads;
		}
		
        private void LoadVariables() => configData = Config.ReadObject<ConfigData>();        
		
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {                
				MaxActiveDownloads = 3,		
				MaxDistance = 3,	
				MaxSizeKB = 512,
				EnforceJpeg = true,			
				Quality = 70, 							
				MaxWidth = 2048,						
				MaxHeight = 2048,		
				FileLogging = true,       							
				Privileges = new Dictionary<string, PrivLimit>()
				{
					{ "signartist.vip", new PrivLimit() { Cooldown = 60, MaxUploads = 10 } },
					{ "signartist.premium", new PrivLimit() { Cooldown = 60, MaxUploads = 20 } }
				}
            };
            SaveConfig(config);
			timer.Once(0.1f, ()=> SaveConfig(config));
        }        
		
        private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
		
        #endregion
		
		#region Data
		
		private void LoadData() => SignUploads = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<uint, ImageInfo>>("SignArtistData");					
		
		private static void SaveData() => Interface.GetMod().DataFileSystem.WriteObject("SignArtistData", SignUploads);		
		
		#endregion
		
    }
        
}
