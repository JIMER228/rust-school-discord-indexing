using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("ChatTags", "FourTeen", "1.0.2")]
    class ChatTags : RustPlugin
    {
        private static string PERM = "chattags.notags";
        private Dictionary<ulong, int> kvp = new Dictionary<ulong, int>();
        private Dictionary<string, string> sponsorTags = new Dictionary<string, string>
        {
            { "sponsor001", "[SPONSOR-LVL1] " },
            { "sponsor002", "[SPONSOR-LVL2] " },
            { "sponsor003", "[SPONSOR-LVL3] " },
            { "sponsor004", "[SPONSOR-LVL4] " },
            { "sponsor005", "[SPONSOR-LVL5] " },
            { "sponsor006", "[SPONSOR-LVL6] " },
            { "sponsor007", "[SPONSOR-LVL7] " },
            { "sponsor008", "[SPONSOR-LVL8] " },
            { "sponsor009", "[SPONSOR-LVL9] " },
            { "sponsor010", "[SPONSOR-LVL10] " },
            { "sponsor011", "[SPONSOR-LVL11] " },
            { "sponsor012", "[SPONSOR-LVL12] " },
            { "sponsor013", "[SPONSOR-LVL13] " },
            { "sponsor014", "[SPONSOR-LVL14] " },
            { "sponsor015", "[SPONSOR-LVL15] " },
            { "sponsor016", "[SPONSOR-LVL16] " },
            { "sponsor017", "[SPONSOR-LVL17] " },
            { "sponsor018", "[SPONSOR-LVL18] " },
            { "sponsor019", "[SPONSOR-LVL19] " },
            { "sponsor020", "[SPONSOR-LVL20] " },
            { "sponsor021", "[SPONSOR-LVL21] " },
            { "sponsor022", "[SPONSOR-LVL22] " },
            { "sponsor023", "[SPONSOR-LVL23] " },
            { "sponsor024", "[SPONSOR-LVL24] " },
            { "sponsor025", "[SPONSOR-LVL25] " },
            { "sponsor026", "[SPONSOR-LVL26] " },
            { "sponsor027", "[SPONSOR-LVL27] " },
            { "sponsor028", "[SPONSOR-LVL28] " },
            { "sponsor029", "[SPONSOR-LVL29] " },
            { "sponsor030", "[SPONSOR-LVL30] " },
            { "sponsor031", "[SPONSOR-LVL31] " },
            { "sponsor032", "[SPONSOR-LVL32] " },
            { "sponsor033", "[SPONSOR-LVL33] " },
            { "sponsor034", "[SPONSOR-LVL34] " },
            { "sponsor035", "[SPONSOR-LVL35] " },
            { "sponsor036", "[SPONSOR-LVL36] " },
            { "sponsor037", "[SPONSOR-LVL37] " },
            { "sponsor038", "[SPONSOR-LVL38] " },
            { "sponsor039", "[SPONSOR-LVL39] " },
            { "sponsor040", "[SPONSOR-LVL40] " },
            { "sponsor041", "[SPONSOR-LVL41] " },
            { "sponsor042", "[SPONSOR-LVL42] " },
            { "sponsor043", "[SPONSOR-LVL43] " },
            { "sponsor044", "[SPONSOR-LVL44] " },
            { "sponsor045", "[SPONSOR-LVL45] " },
            { "sponsor046", "[SPONSOR-LVL46] " },
            { "sponsor048", "[SPONSOR-LVL47] " },
            { "sponsor049", "[SPONSOR-LVL49] " },
            { "sponsor050", "[SPONSOR-LVL50] " }
        };

        private Dictionary<string, string> radarTags = new Dictionary<string, string>
        {
            { "adminradar.lvl1", "[RADAR-LVL1] " },
            { "adminradar.lvl2", "[RADAR-LVL2] " },
            { "adminradar.lvl3", "[RADAR-LVL3] " },
            { "adminradar.lvl4", "[RADAR-LVL4] " },
            { "adminradar.lvl5", "[RADAR-LVL5] " },
            { "adminradar.lvl6", "[RADAR-LVL6] " },
            { "adminradar.lvl7", "[RADAR-LVL7] " },
            { "adminradar.lvl8", "[RADAR-LVL8] " },
            { "adminradar.lvl9", "[RADAR-LVL9] " },
            { "adminradar.lvl10", "[RADAR-LVL10] " },
            { "adminradar.lvl11", "[RADAR-LVL11] " },
            { "adminradar.lvl12", "[RADAR-LVL12] " },
            { "adminradar.lvl13", "[RADAR-LVL13] " },
            { "adminradar.lvl14", "[RADAR-LVL14] " },
            { "adminradar.lvl15", "[RADAR-LVL15] " },
            { "adminradar.lvl16", "[RADAR-LVL16] " },
            { "adminradar.lvl17", "[RADAR-LVL17] " },
            { "adminradar.lvl18", "[RADAR-LVL18] " },
            { "adminradar.lvl19", "[RADAR-LVL19] " },
            { "adminradar.lvl20", "[RADAR-LVL20] " },
            { "adminradar.lvl21", "[RADAR-LVL21] " },
            { "adminradar.lvl22", "[RADAR-LVL22] " },
            { "adminradar.lvl23", "[RADAR-LVL23] " },
            { "adminradar.lvl24", "[RADAR-LVL24] " },
            { "adminradar.lvl25", "[RADAR-LVL25] " },
            { "adminradar.lvl26", "[RADAR-LVL26] " },
            { "adminradar.lvl27", "[RADAR-LVL27] " },
            { "adminradar.lvl28", "[RADAR-LVL28] " },
            { "adminradar.lvl29", "[RADAR-LVL29] " },
            { "adminradar.lvl30", "[RADAR-LVL30] " },
            { "adminradar.lvl31", "[RADAR-LVL31] " },
            { "adminradar.lvl32", "[RADAR-LVL32] " },
            { "adminradar.lvl33", "[RADAR-LVL33] " },
            { "adminradar.lvl34", "[RADAR-LVL34] " },
            { "adminradar.lvl35", "[RADAR-LVL35] " },
            { "adminradar.lvl36", "[RADAR-LVL36] " },
            { "adminradar.lvl37", "[RADAR-LVL37] " },
            { "adminradar.lvl38", "[RADAR-LVL38] " },
            { "adminradar.lvl39", "[RADAR-LVL39] " },
            { "adminradar.lvl40", "[RADAR-LVL40] " },
            { "adminradar.lvl41", "[RADAR-LVL41] " },
            { "adminradar.lvl42", "[RADAR-LVL42] " },
            { "adminradar.lvl43", "[RADAR-LVL43] " },
            { "adminradar.lvl44", "[RADAR-LVL44] " },
            { "adminradar.lvl45", "[RADAR-LVL45] " },
            { "adminradar.lvl46", "[RADAR-LVL46] " },
            { "adminradar.lvl47", "[RADAR-LVL47] " },
            { "adminradar.lvl48", "[RADAR-LVL48] " },
            { "adminradar.lvl49", "[RADAR-LVL49] " },
            { "adminradar.lvl50", "[RADAR-LVL50] " }
        };

        private Dictionary<string, string> hundredPercentTags = new Dictionary<string, string>
        {
            { "buildprotection.lvl1", "[100%-LVL1] " },
            { "buildprotection.lvl2", "[100%-LVL2] " },
            { "buildprotection.lvl3", "[100%-LVL3] " },
            { "buildprotection.lvl4", "[100%-LVL4] " },
            { "buildprotection.lvl5", "[100%-LVL5] " },
            { "buildprotection.lvl6", "[100%-LVL6] " },
            { "buildprotection.lvl7", "[100%-LVL7] " },
            { "buildprotection.lvl8", "[100%-LVL8] " },
            { "buildprotection.lvl9", "[100%-LVL9] " },
            { "buildprotection.lvl10", "[100%-LVL10] " },
            { "buildprotection.lvl11", "[100%-LVL11] " },
            { "buildprotection.lvl12", "[100%-LVL12] " },
            { "buildprotection.lvl13", "[100%-LVL13] " },
            { "buildprotection.lvl14", "[100%-LVL14] " },
            { "buildprotection.lvl15", "[100%-LVL15] " },
            { "buildprotection.lvl16", "[100%-LVL16] " },
            { "buildprotection.lvl17", "[100%-LVL17] " },
            { "buildprotection.lvl18", "[100%-LVL18] " },
            { "buildprotection.lvl19", "[100%-LVL19] " },
            { "buildprotection.lvl20", "[100%-LVL20] " },
            { "buildprotection.lvl21", "[100%-LVL21] " },
            { "buildprotection.lvl22", "[100%-LVL22] " },
            { "buildprotection.lvl23", "[100%-LVL23] " },
            { "buildprotection.lvl24", "[100%-LVL24] " },
            { "buildprotection.lvl25", "[100%-LVL25] " },
            { "buildprotection.lvl26", "[100%-LVL26] " },
            { "buildprotection.lvl27", "[100%-LVL27] " },
            { "buildprotection.lvl28", "[100%-LVL28] " },
            { "buildprotection.lvl29", "[100%-LVL29] " },
            { "buildprotection.lvl30", "[100%-LVL30] " },
            { "buildprotection.lvl31", "[100%-LVL31] " },
            { "buildprotection.lvl32", "[100%-LVL32] " },
            { "buildprotection.lvl33", "[100%-LVL33] " },
            { "buildprotection.lvl34", "[100%-LVL34] " },
            { "buildprotection.lvl35", "[100%-LVL35] " },
            { "buildprotection.lvl36", "[100%-LVL36] " },
            { "buildprotection.lvl37", "[100%-LVL37] " },
            { "buildprotection.lvl38", "[100%-LVL38] " },
            { "buildprotection.lvl39", "[100%-LVL39] " },
            { "buildprotection.lvl40", "[100%-LVL40] " },
            { "buildprotection.lvl41", "[100%-LVL41] " },
            { "buildprotection.lvl42", "[100%-LVL42] " },
            { "buildprotection.lvl43", "[100%-LVL43] " },
            { "buildprotection.lvl44", "[100%-LVL44] " },
            { "buildprotection.lvl45", "[100%-LVL45] " },
            { "buildprotection.lvl46", "[100%-LVL46] " },
            { "buildprotection.lvl47", "[100%-LVL47] " },
            { "buildprotection.lvl48", "[100%-LVL48] " },
            { "buildprotection.lvl49", "[100%-LVL49] " },
            { "buildprotection.lvl50", "[100%-LVL50] " },
        };
        private string GetSponsorTag(BasePlayer player)
        {
            var playerGroups = permission.GetUserGroups(player.UserIDString).Select(g => g.ToLower()).ToList();
            int highestSponsorLevel = 0;
            string highestSponsorTag = null;
            foreach (var perm in playerGroups)
            {
                if (sponsorTags.TryGetValue(perm, out string tag))
                {
                    int level = int.Parse(perm.Substring(7));
                    if (level > highestSponsorLevel)
                    {
                        highestSponsorLevel = level;
                        highestSponsorTag = tag;
                    }
                }
            }
            return highestSponsorTag;
        }
        private string GetRadarTag(BasePlayer player)
        {
            string highestRadarTag = null;

            foreach (var permTag in radarTags)
            {
                if (permission.UserHasPermission(player.UserIDString, permTag.Key))
                {
                    int currentRadarLevel = int.Parse(permTag.Key.Replace("adminradar.lvl", ""));
                    int highestRadarLevel = highestRadarTag != null ? int.Parse(highestRadarTag.Replace("[RADAR-LVL", "").Replace("]", "")) : 0;
                    if (currentRadarLevel > highestRadarLevel)
                        highestRadarTag = permTag.Value;
                }
            }

            return highestRadarTag;
        }

        private string GetHundredPercentTag(BasePlayer player)
        {
            string highestHundredPercentTag = null;

            foreach (var permTag in hundredPercentTags)
            {
                if (permission.UserHasPermission(player.UserIDString, permTag.Key))
                {
                    int currentHundredPercentLevel = int.Parse(permTag.Key.Replace("buildprotection.lvl", ""));
                    int highestHundredPercentLevel = highestHundredPercentTag != null ? int.Parse(highestHundredPercentTag.Replace("[100%-LVL", "").Replace("]", "")) : 0;

                    if (currentHundredPercentLevel > highestHundredPercentLevel)
                        highestHundredPercentTag = permTag.Value;
                }
            }

            return highestHundredPercentTag;
        }

        private string GetTag(BasePlayer player)
        {
            if (!kvp.ContainsKey(player.userID.Get())) kvp.Add(player.userID.Get(), 1);
            int i;
            if (kvp.TryGetValue(player.userID.Get(), out i))
            {
                if (i == 1)
                {
                    string radarTag = GetRadarTag(player);
                    kvp.Remove(player.userID.Get());
                    kvp.Add(player.userID.Get(), 2);
                    if (!string.IsNullOrEmpty(radarTag))
                        return "<color=#6b53f5>" + radarTag + "</color>";
                    return GetTag(player);
                }
                else if (i == 2)
                {
                    string hundredPercentTag = GetHundredPercentTag(player);
                    kvp.Remove(player.userID.Get());
                    kvp.Add(player.userID.Get(), 3);
                    if (!string.IsNullOrEmpty(hundredPercentTag))
                        return "<color=#6b53f5>" + hundredPercentTag + "</color>";
                    return GetTag(player);
                }
                else if (i == 3)
                {
                    string sponsorTag = GetSponsorTag(player);
                    kvp.Remove(player.userID.Get());
                    kvp.Add(player.userID.Get(), 1);
                    if (!string.IsNullOrEmpty(sponsorTag))
                        return "<color=#6b53f5>" + sponsorTag + "</color>";
                }
            }
            return null;
        }
        private string GetPlayerTag(BasePlayer player)
        {
            string sponsorTag = GetSponsorTag(player);
            string radarTag = GetRadarTag(player);
            string hundredPercentTag = GetHundredPercentTag(player);

            return $"{sponsorTag} {radarTag} {hundredPercentTag}".Trim();
        }
        private void OnServerInitialized()
        {
            permission.RegisterPermission(PERM, this);
            Subscribe(nameof(OnChatReferenceTags));
        }
        private string OnChatReferenceTags(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, PERM))
            {
                return string.Empty;
            }
            return GetTag(player);
        }
    }
}