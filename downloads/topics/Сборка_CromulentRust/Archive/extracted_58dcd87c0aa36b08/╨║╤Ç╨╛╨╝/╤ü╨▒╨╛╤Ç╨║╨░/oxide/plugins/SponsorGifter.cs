using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Oxide.Plugins
{
    [Info("SponsorGifter", "FourTeen", "1.0.1")]
    class SponsorGifter : RustPlugin
    {
        public List<string> Perms { get; set; } = new List<string>()
        {
            "sponsor001", "sponsor002", "sponsor003", "sponsor004", "sponsor005",
            "sponsor006", "sponsor007", "sponsor008", "sponsor009", "sponsor010",
            "sponsor011", "sponsor012", "sponsor013", "sponsor014", "sponsor015",
            "sponsor016", "sponsor017", "sponsor018", "sponsor019", "sponsor020",
            "sponsor021", "sponsor022", "sponsor023", "sponsor024", "sponsor025",
            "sponsor026", "sponsor027", "sponsor028", "sponsor029", "sponsor030",
            "sponsor031", "sponsor032", "sponsor033", "sponsor034", "sponsor035",
            "sponsor036", "sponsor037", "sponsor038", "sponsor039", "sponsor040",
            "sponsor041", "sponsor042", "sponsor043", "sponsor044", "sponsor045",
            "sponsor046", "sponsor047", "sponsor048", "sponsor049", "sponsor050"
        };

        [ConsoleCommand("sponsorgifti")]
        void CommandSponsorGift(ConsoleSystem.Arg args)
        {
            ulong targetID;
            if (!ulong.TryParse(args.Args[0], out targetID))
            {
                return;
            }

            BasePlayer targetPlayer = BasePlayer.FindByID(targetID);
            if (targetPlayer == null)
            {
                args.ReplyWith("Игрок не найден.");
                return;
            }

            string currentPerm = null;
            int currentNum = 0;

            foreach (var group in permission.GetGroups())
            {
                if (permission.UserHasGroup(targetID.ToString(), group) && Perms.Contains(group))
                {
                    int num;
                    if (int.TryParse(Regex.Match(group, @"\d+").Value, out num))
                    {
                        if (num > currentNum)
                        {
                            currentNum = num;
                            currentPerm = group;
                        }
                    }
                }
            }


            if (currentPerm == null)
            {
                rust.RunServerCommand($"addgroup {targetID} sponsor001 999d");
                targetPlayer.ChatMessage($"Успешно получена спонсорка 1");
                return;
            }

            int nextNum = currentNum + 1;
            string nextPerm = $"sponsor{nextNum:D3}";
            if (Perms.Contains(nextPerm))
            {
                rust.RunServerCommand($"addgroup {targetID} {nextPerm} 999d");
                targetPlayer.ChatMessage($"Успешно получена спонсорка {nextNum}");
            }
        }
    }
}
