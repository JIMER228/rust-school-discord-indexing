using UnityEngine;

namespace Carbon.Plugins
{
    [Info("TimeApi", "Carbon", "1.0")]
    [Description("Simplified time management API")]
    public sealed class TimeApi : CarbonPlugin
    {
        #region Configuration
        private sealed class TimeConfig
        {
            public float DayStart { get; set; } = 7.5f;
            public float NightStart { get; set; } = 20f;
        }
        #endregion Configuration

        #region State
        private TimeConfig _config = new();
        private bool _isDay = true;
        private float _lastCheck;
        #endregion State

        #region Framework Hooks
        [HookMethod("OnServerInitialized")]
        private void Initialize()
        {
            LoadConfig();
            _lastCheck = Time.realtimeSinceStartup;
        }

        [HookMethod("OnFrame")]
        private void OnFrame()
        {
            float currentTime = Time.realtimeSinceStartup;
            if (currentTime - _lastCheck < 1f)
            {
                return;
            }
            _lastCheck = currentTime;
            TOD_Sky? sky = TOD_Sky.Instance;
            if (sky != null)
            {
                CheckDayNightTransition(sky.Cycle.Hour);
            }
        }
        #endregion Framework Hooks

        #region Time Logic
        private void CheckDayNightTransition(float hour)
        {
            bool newState = hour >= _config.DayStart && hour < _config.NightStart;

            if (newState != _isDay)
            {
                _isDay = newState;
                _ = CallHook(_isDay ? "OnDayStart" : "OnNightStart");
            }
        }
        #endregion Time Logic

        #region Public API
        [HookMethod("IsDay")]
        public bool IsDay()
        {
            return _isDay;
        }

        [HookMethod("GetCurrentHour")]
        public float GetCurrentHour()
        {
            TOD_Sky? sky = TOD_Sky.Instance;
            return sky?.Cycle.Hour ?? 0f;
        }
        #endregion Public API

        #region Config Handling
        protected override void LoadDefaultConfig()
        {
            _config = new TimeConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<TimeConfig>() ?? new TimeConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }
        #endregion Config Handling
    }
}