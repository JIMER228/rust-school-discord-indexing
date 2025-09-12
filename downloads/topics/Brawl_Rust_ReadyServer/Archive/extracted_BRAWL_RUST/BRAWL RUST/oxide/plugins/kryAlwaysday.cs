using System;
namespace Oxide.Plugins
{
    [Info("kryAlwaysday", "xkrystalll", "1.0.0")]
    class kryAlwaysday : RustPlugin
    {
        TOD_Sky Sky;
        private void OnServerInitialized()
        {
            Sky = TOD_Sky.Instance;
            Timer time = timer.Every(60f, () =>{
                Sky.Cycle.DateTime = Sky.Cycle.DateTime.Date + TimeSpan.Parse("12:00:00");
            });
        }
    }
}