using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using VLB;


/*
 * Скачано с дискорд сервера Rust Edit [PRO+]
 * discord.gg/9vyTXsJyKR
*/

namespace Oxide.Plugins
{
    [Info("Team Marker", "discord.gg/9vyTXsJyKR", "0.0.2")]
    public class TeamMarker : RustPlugin
    {
        #region Clasess

        private class MarkerHandler : MonoBehaviour
        {
            public BasePlayer Player;
            public List<BasePlayer> TeamMates = new List<BasePlayer>();
            
            public bool CanShowMarker = true; 
            
            public void Awake()
            {
                Player = GetComponent<BasePlayer>();
                InvokeRepeating(nameof(RefreshTeam), 0f, 1f);
            }

            public void RefreshTeam()
            {
                TeamMates.Clear();

                if (Player.currentTeam != 0)
                {
                    foreach (var check in RelationshipManager.ServerInstance.FindTeam(Player.currentTeam).members)
                    {
                        var target = BasePlayer.FindByID(check);
                        if (target == null || !target.IsConnected) continue;
                    
                        if (!TeamMates.Contains(target))
                            TeamMates.Add(BasePlayer.FindByID(check));
                    }
                } 
                
                if (!TeamMates.Contains(Player))
                    TeamMates.Add(Player); 
            }

            public void DrawMarker()
            {
                if (!CanShowMarker) return;

                RaycastHit hitInfo;
                if (!Physics.Raycast(Player.eyes.position, Player.eyes.HeadForward(), out hitInfo, Settings.MaxDistance, LayerMask.GetMask(new [] { "World", "Construction", "Terrain" }))) return;

                Vector3 position = hitInfo.point;
                foreach (var check in TeamMates)
                    SendMarker(check, hitInfo.distance, position);

                CanShowMarker = false;
                Invoke(nameof(AllowMarker), Settings.MarkerDelay);
            }

            public void AllowMarker() => CanShowMarker = true;
        }
        
        private class Configuration
        {
            [JsonProperty("Задержка перед установкой нового маркера")]
            public float MarkerDelay;
            [JsonProperty("Максимальная дистанция установки маркера")]
            public float MaxDistance;
            [JsonProperty("Длительность видимости маркера")]
            public float MarkerLifeTime;

            [JsonProperty("Разрешение на использование маркера")]
            public string Permission;

            public static Configuration Generate()
            {
                return new Configuration
                {
                    MarkerDelay    = 1f,
                    MaxDistance    = 300f,
                    MarkerLifeTime = 1,
                    Permission     = "TeamMarker.Use"
                };
            }
        } 
        
        #endregion 

        #region Variables

        private static Configuration Settings;

        #endregion

        #region Hooks
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                Settings = Config.ReadObject<Configuration>();
            }
            catch
            {
                PrintWarning($"Error reading config, creating one new config!");
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig() => Settings = Configuration.Generate();
        protected override void SaveConfig()        => Config.WriteObject(Settings);

        private void OnServerInitialized()
        {
            permission.RegisterPermission(Settings.Permission, this);
            timer.Once(1, () => BasePlayer.activePlayerList.ToList().ForEach(OnPlayerConnected));
        }
        private void Unload() => UnityEngine.Object.FindObjectsOfType<MarkerHandler>().ToList().ForEach(UnityEngine.Object.Destroy);
        
        private void OnPlayerConnected(BasePlayer player) => player.GetOrAddComponent<MarkerHandler>();

        private void OnPlayerInput(BasePlayer player, InputState state)
        {
            if (!state.WasJustPressed(BUTTON.FIRE_THIRD) || !permission.UserHasPermission(player.UserIDString, Settings.Permission)) return;
             
            player.GetComponent<MarkerHandler>()?.DrawMarker();
        }

        #endregion

        #region Methods

        private static void SendMarker(BasePlayer player, float distance, Vector3 position)
        {
            player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true); 
                    
            player.SendEntityUpdate();   
            player.SendConsoleCommand("ddraw.text", Settings.MarkerLifeTime, Color.white, position, $"          <size=60><color=#f5424250>◉</color></size>{distance.ToString("F0")} m."); 
            player.SendConsoleCommand("camspeed 0");
                    
            if (player.Connection.authLevel < 2)
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                    
            player.SendEntityUpdate();
        }

        #endregion
    }
}