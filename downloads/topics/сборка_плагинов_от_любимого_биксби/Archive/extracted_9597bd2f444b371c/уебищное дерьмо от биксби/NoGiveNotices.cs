namespace Oxide.Plugins
{
    [Info("NoGiveNotices", "b1xbyy", "1.0.0")]
    class NoGiveNotices : RustPlugin
    {
        private const string ServerName = "SERVER";
        private const string KeyWord = "gave";

        object OnServerMessage(string message, string name)
        {
            if (name == ServerName && message.Contains(KeyWord))
            {
                return true;
            }
            return null;
        }
    }
}