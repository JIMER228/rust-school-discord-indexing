using System;
namespace Oxide.Plugins
{
///Скачано с дискорд сервера Rust Edit [PRO+]
///discord.gg/9vyTXsJyKR
    [Info("kryAlwaysday", "discord.gg/9vyTXsJyKR", "1.0.0")]
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