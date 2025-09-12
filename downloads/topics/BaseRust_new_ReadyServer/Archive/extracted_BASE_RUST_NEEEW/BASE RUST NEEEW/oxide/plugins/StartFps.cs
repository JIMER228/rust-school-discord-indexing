   namespace Oxide.Plugins
{
    [Info("StartFps", "Molik", "1.0.0")]
    public class StartFps : RustPlugin
    {
        void OnServerInitialized()
        {
            Server.Command("fps.limit 200");
            Server.Command("spawn.min_density 3");
            Server.Command("spawn.max_density 5");
            Server.Command("spawn.min_rate 3");
            Server.Command("spawn.max_rate 5");
        }
    }
}