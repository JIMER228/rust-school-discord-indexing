//Reference: Ionic.Zip.Reduced

using Ionic.Crc;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Oxide.Plugins
{
    [Info("Tape Library", "Nikedemos", "0.0.1")]
    [Description("Provides the foundation for downloading, storing, loading, saving and converting OGG files for playable tapes and ambient noises")]
    public class TapeLibrary : RustPlugin
    {
        private static TapeLibrary Instance;

        private static uint CommunityEntityInstanceNetID = 0;

        #region HOOK SUBSCRIPTIONS
        void OnServerInitialized()
        {
            Instance = this;
            CommunityEntity.ServerInstance.EnableGlobalBroadcast(true);
            CommunityEntityInstanceNetID = CommunityEntity.ServerInstance.net.ID;

            OggVorbis.OnServerInitialized();
            AudioDirectoryWatcher.OnServerInitialized();
            TapeFactory.OnServerInitialized();
            WebDownloader.OnServerInitialized();

            AddCovalenceCommand(CMD_DOWNLOAD, nameof(CommandDownload));
            AddCovalenceCommand(CMD_GIVE, nameof(CommandGiveTape));
        }

        void Unload()
        {
            AudioDirectoryWatcher.Unload();
            OggVorbis.Unload();
            TapeFactory.Unload();
            WebDownloader.Unload();
            Instance = null;
            CommunityEntityInstanceNetID = 0;
        }
        #endregion

        #region API/CMD SHARED

        [HookMethod(nameof(TryRecordOnExistingTapeByPartialName))]
        public uint TryRecordOnExistingTapeByPartialName(string partialFilename, Item tapeItem, ulong userID = 0, ulong skinID = 0)
        {
            if (!TapeFactory.IsValidTape(tapeItem))
            {
                return 0;
            }

            var foundMatchingWrapper = TryFindOggByPartialName(partialFilename);

            if (foundMatchingWrapper == null)
            {
                return 0;
            }

            TapeFactory.TryApplyContentsToCassette(foundMatchingWrapper.Crc32, tapeItem, userID, skinID, foundMatchingWrapper.Filename);

            return foundMatchingWrapper.Crc32;
        }

        [HookMethod(nameof(TryProduceAndGiveTapeByPartialName))]
        public Item TryProduceAndGiveTapeByPartialName(string partialFilename, BasePlayer player, bool forceLongTape = true, ulong skinID = 0)
        {
            var tryProduceTape = TryProduceRecordedTapeByName(partialFilename, forceLongTape, skinID, player.userID);

            if (tryProduceTape == null)
            {
                return null;
            }

            player.GiveItem(tryProduceTape, BaseEntity.GiveItemReason.PickedUp);

            return tryProduceTape;
        }



        [HookMethod(nameof(TryFindOggByPartialName))]
        public OggVorbis.OggWrapper TryFindOggByPartialName(string partialFilename)
        {
            OggVorbis.OggWrapper foundMatchingWrapper = null;

            string firstArgToLower = partialFilename.ToLower();

            foreach (var entry in OggVorbis.CacheOggFilesByFilename)
            {
                if (!entry.Value.IsValid)
                {
                    continue;
                }

                if (entry.Key.ToLower().Contains(firstArgToLower))
                {
                    foundMatchingWrapper = entry.Value;
                    break;
                }
            }

            return foundMatchingWrapper;
        }

        [HookMethod(nameof(TryFindOggByID))]
        public OggVorbis.OggWrapper TryFindOggByID(uint crc32)
        {
            OggVorbis.OggWrapper foundMatchingWrapper;

            if (!OggVorbis.CacheOggFilesByDataCRC.TryGetValue(crc32, out foundMatchingWrapper))
            {
                return null;
            }

            if (!foundMatchingWrapper.IsValid)
            {
                return null;
            }

            return foundMatchingWrapper;

        }

        [HookMethod(nameof(TryProduceRecordedTapeByName))]
        public Item TryProduceRecordedTapeByName(string partialFilename, bool forceLongTape, ulong skinID, ulong userID)
        {
            var foundMatchingWrapper = TryFindOggByPartialName(partialFilename);

            if (foundMatchingWrapper == null)
            {
                return null;
            }

            return TapeFactory.TryProduceTape(foundMatchingWrapper, forceLongTape, skinID, userID);
        }

        [HookMethod(nameof(ProduceKnownTape))]
        public Item ProduceKnownTape(uint crc32, ulong userID, ulong skinID, string text)
        {
            return TapeFactory.ProduceTapeWithKnownID(crc32, userID, skinID, text);
        }

        [HookMethod(nameof(TryProduceRecordedTapeByID))]
        public Item TryProduceRecordedTapeByID(uint crc32, bool forceLongTape, ulong skinID, ulong userID, bool skipIDCheck = false)
        {
            var foundMatchingWrapper = TryFindOggByID(crc32);

            if (foundMatchingWrapper == null)
            {
                return null;
            }

            return TapeFactory.TryProduceTape(foundMatchingWrapper, forceLongTape, skinID, userID);
        }
        
        [HookMethod(nameof(TryProduceAndGiveTapeByID))]
        public Item TryProduceAndGiveTapeByID(uint crc32, BasePlayer player, bool forceLongTape, ulong skinID)
        {
            var tryProduceTape = TryProduceRecordedTapeByID(crc32, forceLongTape, skinID, player.userID);

            if (tryProduceTape == null)
            {
                return null;
            }

            player.GiveItem(tryProduceTape, BaseEntity.GiveItemReason.PickedUp);

            return tryProduceTape;
        }

        [HookMethod(nameof(RequestOggDownloadToDirectory))]
        public void RequestOggDownloadToDirectory(string url, int existingFileHandling = 0, Action<bool, string, string> actionOnDone = null)
        {
            if (!WebDownloader.IsURLValid(url))
            {
                if (actionOnDone != null)
                {
                actionOnDone(false, url, "ERROR: This does not appear to be a valid URL");
                }

                return;
            }

            WebDownloader.TryEnqueue(url, (WebDownloader.ExistingFileHandling)existingFileHandling, null, null, actionOnDone);
            
        }

        #endregion

        #region CMD
        public const string CMD_DOWNLOAD = "tl.download";
        public const string CMD_DUMP = "tl.dump";
        public const string CMD_GIVE = "tl.give";

        private void CommandGiveTape(IPlayer iplayer, string command, string[] args)
        {
            if (!OggVorbis.CacheOggFilesByDataCRC.Any())
            {
                iplayer.Reply("There doesn't seem to be any Tapes in the Library at the moment.");
                return;
            }

            if (args.Length < (iplayer.IsServer ? 2 : 1))
            {
                StringBuilder buildList = new StringBuilder();

                foreach (var entry in OggVorbis.CacheOggFilesByDataCRC)
                {
                    buildList.Append("    ");
                    buildList.Append(entry.Key);
                    buildList.Append(" : ");
                    buildList.Append(entry.Value.Filename);

                    if (!entry.Value.IsValid)
                    {
                        buildList.Append(" (INVALID)");
                    }

                    buildList.Append("\n");
                }

                iplayer.Reply($"USAGE: {CMD_GIVE} [ID or partial filename] [partial player name or full steam ID]. If a player executes it in the chat or console in-game and they don't specify a recipient, it will be given to the player executing this command. Executing from the server console requires specifying the player.\nHere's a list of available IDs and their corresponding filenames:\n\n{buildList}");
                return;
            }

            BasePlayer playerToBequeef;

            if (args.Length > 1)
            {
                playerToBequeef = covalence.Players.FindPlayer(args[1])?.Object as BasePlayer ?? null;
            }
            else
            {
                playerToBequeef = iplayer.Object as BasePlayer;
            }

            if (playerToBequeef == null)
            {
                iplayer.Reply($"No player matching \"{args[1]}\" was found");
                return;
            }

            uint crc32;

            Item productedTape;

            if (!uint.TryParse(args[0], out crc32))
            {
                productedTape = TryProduceAndGiveTapeByPartialName(args[0], playerToBequeef);
            }
            else
            {
                productedTape = TryProduceAndGiveTapeByID(crc32, playerToBequeef, true, 0);
            }

            if (productedTape == null)
            {
                iplayer.Reply($"No valid Tapes match the partial name or exact ID \"{args[0]}\". Type {CMD_GIVE} with no arguments to list all available ones.");

                return;
            }

            iplayer.Reply($"{playerToBequeef.displayName} ({playerToBequeef.userID}) was given a new Tape \"{productedTape.name}\"");
        }

        private void CommandDownload(IPlayer iplayer, string command, string[] args)
        {
            if (args.Length == 0)
            {
                iplayer.Message("Please provide the URL of the OGG file to download.");
                return;
            }

            if (!WebDownloader.IsURLValid(args[0]))
            {
                iplayer.Message("This does not appear to be a valid URL");
                return;
            }

            Item tapeItem = null;
            var player = iplayer.Object as BasePlayer;

            if (player != null)
            {
                var activeItem = player.GetActiveItem();

                if (TapeFactory.IsValidTape(activeItem))
                {
                    tapeItem = activeItem;
                }
            }

            int existingFileHandling = 0;

            if (args.Length > 1)
            {
                if (!int.TryParse(args[1], out existingFileHandling))
                {
                    existingFileHandling = 0;
                }
            }

            iplayer.Message($"Requesting download of {args[0]} with existing file handling: {(WebDownloader.ExistingFileHandling)existingFileHandling}, please wait...");
            WebDownloader.TryEnqueue(args[0], (WebDownloader.ExistingFileHandling)existingFileHandling, iplayer, tapeItem);
        }

        #endregion

        #region OGG VORBIS
        public static class OggVorbis
        {
            public static byte[] StringBytes;

            public static Dictionary<string, OggWrapper> CacheOggFilesByFilename;
            public static Dictionary<uint, OggWrapper> CacheOggFilesByDataCRC;
            public static CRC32 Crc32;

            //quick lookup for that string's crc32
            public static Dictionary<string, uint> CacheStringToCRC32;

            public static void OnServerInitialized()
            {
                CacheStringToCRC32 = new Dictionary<string, uint>();

                CacheOggFilesByFilename = new Dictionary<string, OggWrapper>();
                CacheOggFilesByDataCRC = new Dictionary<uint, OggWrapper>();
                Crc32 = new CRC32();
            }
            public static void Unload()
            {
                CacheStringToCRC32 = null;

                CacheOggFilesByFilename = null;
                CacheOggFilesByDataCRC = null;
                Crc32 = null;
            }

            public class OggWrapper
            {
                public string Filename;
                public DateTime DateCreated;
                public DateTime DateModified;
                public byte[] RawData;
                public float SizeInMegabytes = 0F;

                public double AudioLength = 0D;
                public uint Crc32 = 0;

                public int SampleRate = -1;
                public int BitRate = -1;

                public bool IsValidLength = false;
                public bool IsValidSize = false;
                public bool IsValid = false;

                public string Comments = string.Empty;

                public override string ToString()
                {
                    return $"{Filename}.ogg (CRC32: {Crc32}): {(IsValid ? "OK" : "ERROR")} ; SIZE : {SizeInMegabytes} MB ({(IsValidSize ? "V" : "X")}) ; DURATION:  {AudioLength} s  ({(IsValidLength ? "V" : "X")}) ; SAMPLE RATE : {SampleRate} ; BIT RATE : {BitRate}; COMMENTS: {Comments}";
                }
            }

            public static uint GetCRC32(byte[] data)
            {
                Crc32.Reset();
                Crc32.SlurpBlock(data, 0, data.Length);
                Crc32.UpdateCRC((byte)FileStorage.Type.ogg);
                return (uint)Crc32.Crc32Result;
            }
            public static string ReadOGGComments(byte[] data)
            {
                StringBuilder output = new StringBuilder();
                int pos = 0;
                int length = data.Length;

                // Read OGG Vorbis Header
                while (pos < length && data[pos] == 0x01)
                {
                    pos += 27; // skip over header bytes
                }

                // Read Vorbis Comment Header
                if (pos < length && data[pos] == 0x03)
                {
                    pos += 7; // skip over header bytes

                    // Read Vorbis Comment fields
                    int commentLength = BitConverter.ToInt32(data, pos);
                    pos += 4;

                    for (int i = 0; i < commentLength; i++)
                    {
                        int len = BitConverter.ToInt32(data, pos);
                        pos += 4;
                        string field = System.Text.Encoding.UTF8.GetString(data, pos, len);
                        pos += len;

                        // Check for key/value pair format
                        int index = field.IndexOf('=');
                        if (index >= 0)
                        {
                            string key = field.Substring(0, index);
                            string value = field.Substring(index + 1);
                            output.AppendLine(key + ": " + value);
                        }
                        else
                        {
                            output.AppendLine(field);
                        }
                    }
                }
                return output.ToString();
            }

            public static Tuple<int, int> GetSampleRateBitRateAndChannelCount(byte[] oggData)
            {
                if (oggData.IsNullOrEmpty())
                {
                    return new Tuple<int, int>(-1, -1);
                }
                if (oggData.Length < 28 + 12 + 4 + 4 + 4)
                {
                    return new Tuple<int, int>(-1, -1);
                }

                using (var stream = new MemoryStream(oggData))
                {
                    using (var reader = new BinaryReader(stream))
                    {
                        // Read the first 28 bytes, which is the header of the Ogg file
                        byte[] header = reader.ReadBytes(28);


                        // Check if the header starts with "OggS"
                        if (header[0] != 'O' || header[1] != 'g' || header[2] != 'g' || header[3] != 'S')
                        {
                            //return a default tuple
                            return new Tuple<int, int>(-1, -1);
                        }

                        //skip 12 bytes...
                        reader.ReadBytes(12);

                        // Read the sample rate from the header
                        int sampleRate = reader.ReadInt32();

                        //skip 4 bytes...
                        reader.ReadBytes(4);

                        // Read the bit rate from the header
                        int bitRate = reader.ReadInt32();

                        return new Tuple<int, int>(sampleRate, bitRate);
                    }
                }
            }

            //TODO NEXT VERSION, HONESTLY CAN'T BE BOTHERED WITH ALL THE EDGE CASES NOW
            /*
            public static Dictionary<string, string> GetOggVorbisMetadata(byte[] oggData)
            {
                int offsetOfKeyValuePairCount = 0;

                for (var b = 0; b < oggData.Length-4; b++)
                {
                    if (oggData[b] != 'e' || oggData[b +1] != 'n' || oggData[b +2] != 't' || oggData[b +3] != ')')
                    {
                        continue;
                    }

                    offsetOfKeyValuePairCount = b + 4;
                }

                if (offsetOfKeyValuePairCount == 0)
                {
                    return null;
                }

                if (offsetOfKeyValuePairCount > oggData.Length-1)
                {
                    return null;
                }

                int keyValuePairCount = oggData[offsetOfKeyValuePairCount];

                int currentOffset = offsetOfKeyValuePairCount + 1;

                Dictionary<string, string> result = new Dictionary<string, string>();

                return result;
            }*/

            public static uint GetCRC32(string data)
            {
                uint result;

                if (!CacheStringToCRC32.TryGetValue(data, out result))
                {
                    StringBytes = Encoding.UTF8.GetBytes(data);
                    result = GetCRC32(StringBytes);

                    StringBytes = null;

                    CacheStringToCRC32.Add(data, result);
                }

                return result;
            }

            public static OggWrapper TryLoadNew(string fullPath)
            {
                if (!File.Exists(fullPath))
                {
                    return null;
                }

                var newOggFile = new OggWrapper();

                if (!TryPopulateOggWrapperFromPathAndAddToCachesIfSuccess(fullPath, newOggFile))
                {
                    return null;
                }

                return newOggFile;
            }

            public static OggWrapper TryUpdateExisting(string fullPath)
            {
                if (!File.Exists(fullPath))
                {
                    return null;
                }

                var filename = Path.GetFileNameWithoutExtension(fullPath);

                OggWrapper maybeExisting;
                if (!CacheOggFilesByFilename.TryGetValue(filename, out maybeExisting))
                {
                    return null;
                }

                if (!TryPopulateOggWrapperFromPathAndAddToCachesIfSuccess(fullPath, maybeExisting))
                {
                    return null;
                }

                return maybeExisting;
            }

            public static OggWrapper TryRenameExisting(string oldFullPath, string newFullPath)
            {
                var oldFilename = Path.GetFileNameWithoutExtension(oldFullPath);

                OggWrapper maybeExisting;
                if (!CacheOggFilesByFilename.TryGetValue(oldFilename, out maybeExisting))
                {
                    return null;
                }

                var newFilename = Path.GetFileNameWithoutExtension(newFullPath);

                maybeExisting.Filename = newFilename;

                //remove from cache under old name...

                CacheOggFilesByFilename.Remove(oldFilename);

                //and add to cache under new name

                CacheOggFilesByFilename.Add(maybeExisting.Filename, maybeExisting);

                return maybeExisting;
            }

            public static uint TryForgetExisting(string fullPath)
            {
                var filename = Path.GetFileNameWithoutExtension(fullPath);

                OggWrapper maybeExisting;
                if (!CacheOggFilesByFilename.TryGetValue(filename, out maybeExisting))
                {
                    return 0;
                }

                var crc32 = maybeExisting.Crc32;

                maybeExisting.RawData = null;

                //and remove from caches

                CacheOggFilesByFilename.Remove(filename);
                RemoveFromCrc32CacheIfExists(crc32);

                return crc32;
            }

            public static void RemoveFromCrc32CacheIfExists(uint crc32)
            {
                if (!CacheOggFilesByDataCRC.ContainsKey(crc32))
                {
                    return;
                }

                CacheOggFilesByDataCRC.Remove(crc32);
            }

            public static bool TryPopulateOggWrapperFromPathAndAddToCachesIfSuccess(string fullPath, OggWrapper oggWrapper)
            {
                bool result = TryPopulateOggWraperFromPath(fullPath, oggWrapper);

                if (result)
                {
                    CacheOggFilesByFilename.Add(oggWrapper.Filename, oggWrapper);
                    CacheOggFilesByDataCRC.Add(oggWrapper.Crc32, oggWrapper);
                }

                return result;
            }

            public static float ByteToMegabyte(ulong byteSize)
            {
                return (float)byteSize / 1024f / 1024f;
            }

            public static float ByteToMegabyte(int byteSize)
            {
                return (float)byteSize / 1024f / 1024f;
            }

            public static bool TryPopulateOggWraperFromPath(string fullPath, OggWrapper oggWrapper)
            {
                oggWrapper.IsValidLength = false;
                oggWrapper.IsValidSize = false;
                oggWrapper.IsValid = false;

                oggWrapper.AudioLength = 0;
                oggWrapper.SizeInMegabytes = 0;

                oggWrapper.SampleRate = -1;
                oggWrapper.BitRate = -1;

                oggWrapper.Filename = Path.GetFileNameWithoutExtension(fullPath);
                oggWrapper.DateCreated = File.GetCreationTime(fullPath);
                oggWrapper.DateModified = File.GetLastWriteTime(fullPath);
                oggWrapper.RawData = File.ReadAllBytes(fullPath);

                oggWrapper.Comments = string.Empty;

                uint previousCrcData = oggWrapper.Crc32;
                oggWrapper.Crc32 = GetCRC32(oggWrapper.RawData);

                //remove from old cache, only data crc32 since the name is what's common...

                if (previousCrcData != default(uint))
                {
                    if (previousCrcData != oggWrapper.Crc32)
                    {
                        RemoveFromCrc32CacheIfExists(previousCrcData);
                    }
                }

                if (CacheOggFilesByFilename.ContainsKey(oggWrapper.Filename))
                {
                    CacheOggFilesByFilename.Remove(oggWrapper.Filename);
                }

                if (CacheOggFilesByDataCRC.ContainsKey(oggWrapper.Crc32))
                {
                    CacheOggFilesByDataCRC.Remove(oggWrapper.Crc32);
                }

                oggWrapper.SizeInMegabytes = ByteToMegabyte(oggWrapper.RawData.Length);
                oggWrapper.IsValidSize = oggWrapper.SizeInMegabytes < Cassette.MaxCassetteFileSizeMB;

                var headerTuple = GetSampleRateBitRateAndChannelCount(oggWrapper.RawData);

                oggWrapper.SampleRate = headerTuple.Item1;
                oggWrapper.BitRate = headerTuple.Item2;

                if (!oggWrapper.IsValidSize)
                {
                    return false;
                }

                if (oggWrapper.SampleRate == -1 || oggWrapper.BitRate == -1)
                {
                    return false;
                }

                oggWrapper.AudioLength = Cassette.GetOggLength(oggWrapper.RawData);
                oggWrapper.IsValidLength = oggWrapper.AudioLength > 0D && oggWrapper.AudioLength <= 36D;

                if (!oggWrapper.IsValidLength)
                {
                    return false;
                }

                //check if that crc32 already exists in server storage - if not, skip this step

                if (oggWrapper.RawData != null)
                {
                    oggWrapper.IsValid = true;

                    if (TapeFactory.TryGetBytesByCrc32FromServerStorage(oggWrapper.Crc32) != null)
                    {
                        Instance.PrintError($"OK: CRC32 {oggWrapper.Crc32} already exists in server storage DB.");
                        return true;
                    }


                    uint obtainedCrc32 = TapeFactory.TryWriteBytesToServerStorage(oggWrapper.RawData);

                    if (obtainedCrc32 != oggWrapper.Crc32)
                    {
                        Instance.PrintError($"WARNING: MISMATCH BETWEEN CRC32 OBTAINED FROM STORAGE ({obtainedCrc32}) AND PREDICTED ONE ({oggWrapper.Crc32}). DEFAULTING TO OBTAINED.");
                        oggWrapper.Crc32 = obtainedCrc32;
                    }
                    else
                    {
                        Instance.PrintWarning($"CRC32 CHECK OK: {obtainedCrc32}");
                    }
                }
                else
                {
                    Instance.PrintError("ERROR: RAW DATA IS NULL!");
                    return false;
                }

                oggWrapper.Comments = ReadOGGComments(oggWrapper.RawData);

                return true;
            }

        }
        #endregion

        #region AUDIO DIRECTORY WATCHER
        public static class AudioDirectoryWatcher
        {
            public static string AudioPath;
            public static string AudioPathFilenameFormatShortWithExtension;
            public static string AudioPathFilenameFormatDuplicate;
            public static string AudioPathFilenameFormatShortWithExtensionDuplicate;
            public static string AudioPathFilenameFormatFull;
            public static string AudioPathFilenameFormatFullDuplicate;

            public static FileSystemWatcher TheFSWatcher;

            public static void OnServerInitialized()
            {
                AudioPathFilenameFormatDuplicate = "{0}_{1}";

                AudioPath = Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "ogg";

                if (!Directory.Exists(AudioPath))
                {
                    Directory.CreateDirectory(AudioPath);
                }

                AudioPathFilenameFormatShortWithExtension = "{0}.ogg";
                AudioPathFilenameFormatShortWithExtensionDuplicate = AudioPathFilenameFormatDuplicate + ".ogg";

                AudioPathFilenameFormatFull = AudioPath + Path.DirectorySeparatorChar + AudioPathFilenameFormatShortWithExtension;
                AudioPathFilenameFormatFullDuplicate = AudioPath + Path.DirectorySeparatorChar + AudioPathFilenameFormatShortWithExtensionDuplicate;

                TheFSWatcher = new FileSystemWatcher(AudioPath);

                TheFSWatcher.EnableRaisingEvents = true;
                TheFSWatcher.Filter = "*.ogg";
                TheFSWatcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.LastWrite | NotifyFilters.DirectoryName | NotifyFilters.Size;
                TheFSWatcher.IncludeSubdirectories = false;

                TheFSWatcher.Error += AudioDirectoryWatcher_Error;
                TheFSWatcher.Created += AudioDirectoryWatcher_Created;
                TheFSWatcher.Changed += AudioDirectoryWatcher_Changed;
                TheFSWatcher.Deleted += AudioDirectoryWatcher_Deleted;
                TheFSWatcher.Renamed += AudioDirectoryWatcher_Renamed;

                ProcessOGGDataDirectory();
            }

            public static void ProcessOGGDataDirectory()
            {
                var oggFilesInDirectory = Directory.GetFiles(AudioPath, TheFSWatcher.Filter, SearchOption.TopDirectoryOnly);

                for (var f = 0; f < oggFilesInDirectory.Length; f++)
                {
                    //will return null if the file doesn't exist, but in this case, it most certainly does
                    var tryWrap = OggVorbis.TryLoadNew(oggFilesInDirectory[f]);

                    if (tryWrap == null)
                    {


                        Instance.PrintError($"ERROR PROCESSING {oggFilesInDirectory[f]}");
                        continue;
                    }

                    Instance.PrintError(tryWrap.ToString());
                }
            }

            private static void AudioDirectoryWatcher_Error(object sender, ErrorEventArgs e)
            {
                throw new System.NotImplementedException();
            }

            public static bool ExtensionIsExactlyOggAndNothingElse(string fullPath)
            {
                return string.Equals(Path.GetExtension(fullPath), ".ogg", StringComparison.OrdinalIgnoreCase);
            }

            private static void AudioDirectoryWatcher_Created(object sender, FileSystemEventArgs e)
            {
                if (!ExtensionIsExactlyOggAndNothingElse(e.FullPath))
                {
                    return;
                }

                Instance.timer.Once(3F, () =>
                {
                    if (Instance == null)
                    {
                        return;
                    }

                    Instance.PrintError($">>>> CREATED FILE {e.FullPath}... ");
                    var tryWrap = OggVorbis.TryLoadNew(e.FullPath);

                    if (tryWrap == null)
                    {
                        Instance.PrintError($"ERROR PROCESSING {e.FullPath}");
                        return;
                    }

                    Instance.PrintWarning(tryWrap.ToString());
                });

            }


            private static void AudioDirectoryWatcher_Changed(object sender, FileSystemEventArgs e)
            {
                if (!ExtensionIsExactlyOggAndNothingElse(e.FullPath))
                {
                    return;
                }

                Instance.PrintError($">>>> CHANGED FILE {e.FullPath}...");
                var tryWrap = OggVorbis.TryUpdateExisting(e.FullPath);

                if (tryWrap == null)
                {
                    Instance.PrintError($"ERROR PROCESSING {e.FullPath}");
                    return;
                }

                Instance.PrintWarning(tryWrap.ToString());

            }

            private static void AudioDirectoryWatcher_Deleted(object sender, FileSystemEventArgs e)
            {
                if (!ExtensionIsExactlyOggAndNothingElse(e.FullPath))
                {
                    return;
                }

                Instance.timer.Once(0.8F, () =>
                {
                    Instance.PrintError($">>>> DELETED FILE {e.FullPath}...");
                    var tryCrc32 = OggVorbis.TryForgetExisting(e.FullPath);

                    if (tryCrc32 == 0)
                    {
                        return;
                    }

                    Instance.PrintError($"{tryCrc32}");
                });

            }

            private static void AudioDirectoryWatcher_Renamed(object sender, RenamedEventArgs e)
            {
                if (!ExtensionIsExactlyOggAndNothingElse(e.FullPath))
                {
                    return;
                }

                Instance.PrintError($">>>> RENAMED FILE FROM {e.OldFullPath} TO {e.FullPath}...");
                var tryWrap = OggVorbis.TryRenameExisting(e.OldFullPath, e.FullPath);

                if (tryWrap == null)
                {
                    Instance.PrintError($"ERROR PROCESSING {e.FullPath}");
                    return;
                }

                Instance.PrintError(tryWrap.ToString());
            }

            public static void Unload()
            {
                AudioPath = null;
                AudioPathFilenameFormatFull = null;
                AudioPathFilenameFormatShortWithExtension = null;

                TheFSWatcher.Dispose();
                TheFSWatcher = null;
            }
        }

        #endregion

        #region TAPE FACTORY
        public static class TapeFactory
        {
            public const string FORMAT_DUMP_FILENAME = "rec_{0}.{1}.{2}_{3}_{4}_{5}";

            public const string ITEM_SHORTNAME_TAPE_SHORT = "cassette.short";
            public const string ITEM_SHORTNAME_TAPE_MEDIUM = "cassette.medium";
            public const string ITEM_SHORTNAME_TAPE_LONG = "cassette";

            public static ItemDefinition TapeDefinitionShort;
            public static ItemDefinition TapeDefinitionMedium;
            public static ItemDefinition TapeDefinitionLong;

            public static string LastError;
            public static string LastWarning;

            public static Dictionary<Item, Cassette> CacheTapeToCassette;

            public static string SaveBytesAsOGG(byte[] bytes, string filenameWithoutExtension)
            {
                //the directory should exist - if it doesn't, it will be created
                //if the file exists, some random 

                if (!Directory.Exists(AudioDirectoryWatcher.AudioPath))
                {
                    Directory.CreateDirectory(AudioDirectoryWatcher.AudioPath);
                }

                if (File.Exists(string.Format(AudioDirectoryWatcher.AudioPathFilenameFormatFull, filenameWithoutExtension)))
                {
                    LastWarning = "File already exists at that location, appending random characters to the new name";
                    filenameWithoutExtension = string.Format(AudioDirectoryWatcher.AudioPathFilenameFormatDuplicate, filenameWithoutExtension, Path.GetRandomFileName());
                }

                return filenameWithoutExtension;
            }

            public static void OnServerInitialized()
            {
                CacheTapeToCassette = new Dictionary<Item, Cassette>();

                TapeDefinitionShort = ItemManager.FindItemDefinition(ITEM_SHORTNAME_TAPE_SHORT);
                TapeDefinitionMedium = ItemManager.FindItemDefinition(ITEM_SHORTNAME_TAPE_MEDIUM);
                TapeDefinitionLong = ItemManager.FindItemDefinition(ITEM_SHORTNAME_TAPE_LONG);
            }

            public static void Unload()
            {
                TapeDefinitionShort = null;
                TapeDefinitionMedium = null;
                TapeDefinitionLong = null;

                CacheTapeToCassette = null;

                LastError = null;
                LastWarning = null;
            }

            public static bool IsValidTape(Item item)
            {
                if (item == null)
                {
                    LastError = $"Invalid tape Item: Item is NULL";
                    return false;
                }

                if (!item.info.shortname.Contains(ITEM_SHORTNAME_TAPE_LONG))
                {
                    LastError = $"Invalid tape Item: expected one of the three Cassettes, got {item.info.displayName.translated} instead";
                    return false;
                }

                if (item.instanceData == null)
                {
                    LastError = "Invalid tape Item: instanceData is NULL";
                    return false;
                }

                if (item.instanceData.subEntity == default(uint))
                {
                    LastError = "Invalid tape Item: subEntity reference is 0";
                    return false;
                }

                return true;

            }

            public static Cassette TryGetCassetteFromTape(Item tapeItem)
            {
                if (!IsValidTape(tapeItem))
                {
                    return null;
                }

                Cassette tryCassette;

                if (!CacheTapeToCassette.TryGetValue(tapeItem, out tryCassette))
                {
                    tryCassette = BaseNetworkable.serverEntities.Find(tapeItem.instanceData.subEntity) as Cassette;

                    //whether it's null or not, now we have it in the cache
                    CacheTapeToCassette.Add(tapeItem, tryCassette);
                }

                if (tryCassette == null)
                {
                    LastError = "Tape has a null Cassette";
                }

                return tryCassette;
            }

            public static Item MakeBlankTape(ItemDefinition useThisDefinition, ulong skinID = 0)
            {
                if (useThisDefinition == null)
                {
                    LastError = "Item Definition is null";
                    return null;
                }

                var newItem = ItemManager.Create(useThisDefinition, 1, skinID);

                if (newItem == null)
                {
                    LastError = "Item Manager produced null Item";
                    return null;
                }

                return newItem;
            }

            public static Item TryCloneTape(Item originalTape, bool markCloneInName = true)
            {
                LastError = string.Empty;
                LastWarning = string.Empty;

                if (!IsValidTape(originalTape))
                {
                    return null;
                }

                var originalCassette = TryGetCassetteFromTape(originalTape);

                if (originalCassette == null)
                {
                    return null;
                }

                var clonedTape = MakeBlankTape(originalTape.info, originalTape.skin);

                if (clonedTape == null)
                {
                    return null;
                }

                if (markCloneInName)
                {
                    clonedTape.text = originalTape.text + " (COPY)";
                }
                else
                {
                    clonedTape.text = originalTape.text;
                }

                if (!TryApplyContentsToCassette(originalCassette.AudioId, clonedTape, originalCassette.CreatorSteamId, originalCassette.skinID))
                {
                    clonedTape.Remove();
                    return null;
                }

                return clonedTape;
            }

            public static Item ProduceTapeWithKnownID(uint crc32, ulong userID, ulong skinID, string text)
            {
                var newTapeItem = MakeBlankTape(TapeDefinitionLong, skinID);

                if (!TryApplyContentsToCassette(crc32, newTapeItem, userID, skinID, text))
                {
                    newTapeItem.Remove();
                    return null;
                }

                return newTapeItem;
            }

            public static Item TryProduceTape(OggVorbis.OggWrapper oggWrapper, bool forceLongTape = false, ulong skinID = 0, ulong userID = 0)
            {
                ItemDefinition useThisDefinition = TapeDefinitionLong;

                if (!forceLongTape)
                {
                    if (oggWrapper.AudioLength <= 12D)
                    {
                        useThisDefinition = TapeDefinitionShort;
                    }
                    else
                    {
                        if (oggWrapper.AudioLength <= 24D)
                        {
                            useThisDefinition = TapeDefinitionMedium;
                        }
                    }
                }

                var newTapeItem = MakeBlankTape(useThisDefinition, skinID);

                if (!TryApplyContentsToCassette(oggWrapper.Crc32, newTapeItem, userID, skinID, oggWrapper.Filename))
                {
                    newTapeItem.Remove();
                    return null;
                }

                return newTapeItem;
            }

            public static bool TryApplyContentsToCassette(uint crc32, Item tape, ulong userID = 0, ulong skinID = 0, string text = default(string))
            {
                var tryCassette = TryGetCassetteFromTape(tape);

                if (tryCassette == null)
                {
                    return false;
                }

                tryCassette.SetAudioId(crc32, userID);


                if (skinID != 0)
                {
                    if (tryCassette.skinID != skinID)
                    {
                        tryCassette.skinID = skinID;
                    }
                }

                if (text != default(string))
                {
                    tape.text = text.Replace("_", " ");
                }

                tape.MarkDirty();

                tryCassette.SendNetworkUpdateImmediate();

                return true;
            }

            public static byte[] TryGetBytesByCrc32FromServerStorage(uint crc32)
            {
                return FileStorage.server.Get(crc32, FileStorage.Type.ogg, CommunityEntityInstanceNetID, 0);
            }

            public static uint TryWriteBytesToServerStorage(byte[] bytes)
            {
                return FileStorage.server.Store(bytes, FileStorage.Type.ogg, CommunityEntityInstanceNetID, 0);
            }

            public static string TryDumpCassetteContentsToOGG(Item tape, string filename = default(string))
            {
                LastError = string.Empty;
                LastWarning = string.Empty;

                if (!IsValidTape(tape))
                {
                    return null;
                }

                var cassette = TryGetCassetteFromTape(tape);

                if (cassette == null)
                {
                    return null;
                }

                if (cassette.AudioId == default(uint))
                {
                    LastError = "Cassette is blank, nothing to dump!";
                    return null;
                }

                if (filename == default(string))
                {
                    var timestamp = DateTime.Now;

                    filename = string.Format(FORMAT_DUMP_FILENAME, timestamp.Day, timestamp.Month, timestamp.Year, timestamp.Hour, timestamp.Minute, timestamp.Second);
                }

                var oggData = TryGetBytesByCrc32FromServerStorage(cassette.AudioId);

                if (oggData == null)
                {
                    LastError = "Data not found in SQLite storage!";
                    return null;
                }

                SaveBytesAsOGG(oggData, filename);

                return filename;
            }
        }
        #endregion

        #region WEB DOWNLOADER
        public static class WebDownloader
        {
            public enum ExistingFileHandling
            {
                LeaveExisting,
                OverwriteExisting,
                WriteUnderNewName
            }

            public static Coroutine LastDownloadCoroutine;

            public static bool IsURLValid(string url)
            {
                return Uri.IsWellFormedUriString(url, UriKind.Absolute);
            }

            public static void OnServerInitialized()
            {
                RequestQueue = new Queue<OggDownloadRequest>();
                LastDownloadCoroutine = null;
            }

            public static void Unload()
            {
                if (LastDownloadCoroutine != null)
                {
                    CommunityEntity.ServerInstance.StopCoroutine(LastDownloadCoroutine);
                }

                RequestQueue = null;
            }

            public static Queue<OggDownloadRequest> RequestQueue;

            public class OggDownloadRequest : IDisposable
            {
                public string SourceURL = string.Empty;
                public IPlayer TargetIPlayer = null;
                public Item TargetTapeItem = null;
                public Action<bool, string, string> ActionOnDone = null;

                public string TargetFilenameWithoutExtension = string.Empty;
                public string TargetPath = string.Empty;

                public byte[] DataBytes = null;

                public void Dispose()
                {
                    DataBytes = null;
                }
            }

            public static void TryEnqueue(string sourceURL, ExistingFileHandling existingFileHandling, IPlayer targetIPlayer = null, Item targetTapeItem = null, Action<bool, string, string> actionOnDone = null)
            {
                Instance.PrintWarning($"TRYING TO ENQUEUE {sourceURL}...");

                string targetFilenameWithoutExtension = Path.GetFileNameWithoutExtension(sourceURL);

                for (var i = 0; i < RequestQueue.Count; i++)
                {
                    var currentRequest = RequestQueue.ElementAt(i);

                    if (currentRequest.TargetFilenameWithoutExtension == targetFilenameWithoutExtension)
                    {
                        var resultMessage = $"INFO: File \"{targetFilenameWithoutExtension}\" is already in the download queue, please wait till it finishes downloading";

                        if (actionOnDone != null)
                        {
                            actionOnDone(false, sourceURL, resultMessage);
                        }

                        if (targetIPlayer != null)
                        {
                            targetIPlayer.Message(resultMessage);
                            return;
                        }

                    }
                }

                //check if that file exists...

                string targetPath = string.Format(AudioDirectoryWatcher.AudioPathFilenameFormatFull, targetFilenameWithoutExtension);


                if (File.Exists(targetPath))
                {
                    switch (existingFileHandling)
                    {
                        case ExistingFileHandling.LeaveExisting:
                            {
                                var resultMessage = $"OK: File \"{targetFilenameWithoutExtension}\" already exists and it was requested to leave it alone.";

                                if (actionOnDone != null)
                                {
                                    actionOnDone(true, sourceURL, resultMessage);
                                }

                                if (targetIPlayer != null)
                                {
                                    targetIPlayer.Message(resultMessage);
                                    return;
                                }

                                return;
                            }
                        case ExistingFileHandling.OverwriteExisting:
                            {
                                if (targetIPlayer != null)
                                {
                                    targetIPlayer.Message($"INFO: File \"{targetFilenameWithoutExtension}\" already exists, it's now going to be overwritten.");
                                }

                                break;
                            }
                        case ExistingFileHandling.WriteUnderNewName:
                            {
                                var oldFileNameWithoutExtension = targetFilenameWithoutExtension;

                                targetFilenameWithoutExtension = string.Format(AudioDirectoryWatcher.AudioPathFilenameFormatDuplicate, targetFilenameWithoutExtension, Path.GetRandomFileName());

                                targetPath = string.Format(AudioDirectoryWatcher.AudioPathFilenameFormatFull, targetFilenameWithoutExtension);

                                if (targetIPlayer != null)
                                {
                                    targetIPlayer.Message($"INFO: File \"{oldFileNameWithoutExtension}\" already exists, the duplicate is going to be downloaded as {targetFilenameWithoutExtension}");
                                }
                                break;
                            }
                    }

                }

                //now enqueue a new request

                RequestQueue.Enqueue(new OggDownloadRequest
                {
                    SourceURL = sourceURL,
                    TargetFilenameWithoutExtension = targetFilenameWithoutExtension,
                    TargetPath = targetPath,
                    TargetIPlayer = targetIPlayer,
                    TargetTapeItem = targetTapeItem,
                    ActionOnDone = actionOnDone,
                });

                ProcessNextQueueElementIfAnything();
            }

            public static void ProcessNextQueueElementIfAnything()
            {
                if (LastDownloadCoroutine != null)
                {
                    Instance.PrintWarning("ALREADY RUNNING, NOT STARTING ANOTHER COROUTINE!");
                    //already running, return
                    return;
                }

                if (RequestQueue.Count == 0)
                {
                    //nothing to process, return
                    Instance.PrintWarning("NOTHING TO PROCESS, TERMINATING!");
                    return;
                }

                var dequeued = RequestQueue.Dequeue();

                LastDownloadCoroutine = CommunityEntity.ServerInstance.StartCoroutine(DownloadRequestCoroutine(dequeued));
            }

            public static void DownloadRequestFinalize(OggDownloadRequest downloadRequestToDispose, string myFinalMessageGoodbye, bool success = false)
            {
                Instance.PrintError("FINALIZING DOWNLOAD REQUEST...");
                if (downloadRequestToDispose.TargetIPlayer != null)
                {
                    downloadRequestToDispose.TargetIPlayer.Message(myFinalMessageGoodbye);
                }

                if (downloadRequestToDispose.ActionOnDone != null)
                {
                    downloadRequestToDispose.ActionOnDone(success, downloadRequestToDispose.SourceURL, myFinalMessageGoodbye);
                }

                LastDownloadCoroutine = null;
                downloadRequestToDispose.Dispose();
                ProcessNextQueueElementIfAnything();
            }

            public static IEnumerator DownloadRequestCoroutine(OggDownloadRequest downloadRequest)
            {
                Instance.PrintError("STARTING DOWNLOAD REQUEST COROUTINE...");

                using (var webRequest = UnityWebRequest.Get(downloadRequest.SourceURL))
                {
                    yield return webRequest.SendWebRequest();

                    if (webRequest.error != null)
                    {
                        DownloadRequestFinalize(downloadRequest, $"ERROR: Could not download \"{downloadRequest.SourceURL}\": {webRequest.error}");
                        yield break;
                    }

                    var fileSizeInMB = OggVorbis.ByteToMegabyte(webRequest.downloadedBytes);

                    if (fileSizeInMB >= Cassette.MaxCassetteFileSizeMB)
                    {
                        DownloadRequestFinalize(downloadRequest, $"ERROR: The downloaded file has an invalid size: {fileSizeInMB} MB. Max size is {Cassette.MaxCassetteFileSizeMB} MB.");
                        yield break;
                    }

                    //ok. data.
                    downloadRequest.DataBytes = webRequest.downloadHandler.data;

                    //TODO: check the audio length.

                    var checkLength = Cassette.GetOggLength(downloadRequest.DataBytes);

                    if (checkLength <= 0D || checkLength > 36D)
                    {
                        DownloadRequestFinalize(downloadRequest, $"ERROR: The downloaded file has an invalid length: {checkLength}. Max length is 36 seconds. It may also mean this is not a valid OGG file.");
                        yield break;
                    }

                    try
                    {
                        if (!Directory.Exists(AudioDirectoryWatcher.AudioPath))
                        {
                            Directory.CreateDirectory(AudioDirectoryWatcher.AudioPath);
                        }

                        File.WriteAllBytes(downloadRequest.TargetPath, downloadRequest.DataBytes);
                    }
                    catch (Exception e)
                    {
                        DownloadRequestFinalize(downloadRequest, $"ERROR: Exception while trying to save the file: {e.Message}");
                        yield break;
                    }

                    var crc32 = OggVorbis.GetCRC32(downloadRequest.DataBytes);



                    if (downloadRequest.TargetTapeItem != null)
                    {
                        ulong userID = downloadRequest.TargetIPlayer == null ? 0 : (downloadRequest.TargetIPlayer.IsServer ? 0 : (downloadRequest.TargetIPlayer.Object as BasePlayer)?.userID ?? 0);
                        var tryApply = TapeFactory.TryApplyContentsToCassette(crc32, downloadRequest.TargetTapeItem, userID, 0, downloadRequest.TargetFilenameWithoutExtension);

                        string finalMessage = tryApply ? $"OK: Succesfully downloaded \"{downloadRequest.TargetFilenameWithoutExtension}\" and record it onto the provided Tape Item" : $"WARNING: Successfully downloaded \"{downloadRequest.TargetFilenameWithoutExtension}\", but could not record it onto the provided Item (is it not a Tape?)";

                        DownloadRequestFinalize(downloadRequest, finalMessage, true);
                        yield break;
                        
                    }

                    DownloadRequestFinalize(downloadRequest, $"OK: Successfully downloaded \"{downloadRequest.TargetFilenameWithoutExtension}\".", true);
                    yield break;

                }
            }
        }
        #endregion
    }
}
