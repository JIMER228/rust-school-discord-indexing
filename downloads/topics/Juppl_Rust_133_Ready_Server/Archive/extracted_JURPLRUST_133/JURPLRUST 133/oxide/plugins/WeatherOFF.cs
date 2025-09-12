namespace Oxide.Plugins
{
    [Info("WeatherOFF", "JURPL RUST", "1.0.1")]
    [Description("Убирает все погодные условия.")]
    public class WeatherOFF : RustPlugin
    {
        void OnServerInitialized()
        {
            ConsoleSystem.Run.Server.Normal("weather.clouds 0");
            ConsoleSystem.Run.Server.Normal("weather.rain 0");
            ConsoleSystem.Run.Server.Normal("weather.wind 0");
            ConsoleSystem.Run.Server.Normal("weather.fog 0");
        }

        [ConsoleCommand("weather.off")]
        void ConsoleWeather(ConsoleSystem.Arg arg)
        {
            ConsoleSystem.Run.Server.Normal("weather.clouds 1");
            ConsoleSystem.Run.Server.Normal("weather.rain 1");
            ConsoleSystem.Run.Server.Normal("weather.wind 1");
            ConsoleSystem.Run.Server.Normal("weather.fog 1");
        }
    }
}
