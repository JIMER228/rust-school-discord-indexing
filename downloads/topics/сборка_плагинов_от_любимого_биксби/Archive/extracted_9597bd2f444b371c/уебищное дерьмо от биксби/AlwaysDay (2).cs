namespace Oxide.Plugins
{
    [Info("AlwaysDay", "b1xbyy", "1.0.0")]
    public class AlwaysDay : RustPlugin
    {
        private Timer _dayTimer;
        private void OnServerInitialized()
        {
            KeepDaylight();
            StartDayTimer();
        }

        private void KeepDaylight()
        {
            if (TOD_Sky.Instance != null)
            {
                TOD_Sky.Instance.Cycle.Hour = 12f;
                TOD_Sky.Instance.Components.Time.ProgressTime = false;
            }
        }

        private void StartDayTimer()
        {
            _dayTimer = timer.Every(5f, () =>
            {
                if (TOD_Sky.Instance?.Cycle.Hour != 12f)
                {
                    KeepDaylight();
                }
            });
        }
    }
}