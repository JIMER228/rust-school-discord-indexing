using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("NameFix", "Visagalis", "1.0.1")]
    [Description("Removes advertisements from player names when they login.")]

    class NameFix : CovalencePlugin
    {
        static List<object> StringsToRemove = new List<object> { "hellcase.com", "csgolottery.com" };
        static List<object> DomainsToRemove = new List<object> {"com", "lt", "net", "org", "gg"};

        void OnUserConnected(IPlayer player)
        {
			string pattern = $"[A-Za-z0-9]+\\.({string.Join("|", DomainsToRemove.Select(d=> d.ToString()).ToArray())})";
            Puts(pattern);
            Regex rgx = new Regex(pattern, RegexOptions.IgnoreCase);
            string oldName = player.Name;
            string newName = rgx.Replace(oldName, "").Trim();
            newName = RemoveUnwantedNames(newName);
            if (oldName != newName)
                player.Rename(string.IsNullOrEmpty(newName) ? player.Id : newName);
        }

        private string RemoveUnwantedNames(string currName)
        {
            string newName = currName;
            foreach (string text in StringsToRemove)
            {
                newName = newName.Replace(text, "");
            }
            return newName;
        }

        void Init()
        {
            CheckCfg("Unwanted Names", ref StringsToRemove);
            CheckCfg("Unwanted Domains", ref DomainsToRemove);

            SaveConfig();
        }

        protected override void LoadDefaultConfig() {}

        private void CheckCfg<T>(string key, ref T var)
        {
            if (Config[key] is T)
            {
                var = (T) Config[key];
            }
            else
            {
                Config[key] = var;
            }
        }
    }
}
