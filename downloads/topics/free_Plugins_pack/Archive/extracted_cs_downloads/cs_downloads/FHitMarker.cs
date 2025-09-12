using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

namespace Oxide.Plugins 
{ 
    [Info("FHitMarker", ".h1pex", "1.0.6")]
    class FHitMarker : RustPlugin
    {
        #region Cfg
        
        private ConfigData _config;
        public class ConfigData
        {
            [JsonProperty("Использовать иконку попадания: ")]
            public bool UseHit = false;
            
            [JsonProperty("Через сколько пропадет маркер: ")]
            public float TimeToDestroy = 0.0f;
            
            [JsonProperty("Цвет которым отмечаются друзья: ")]
            public string ColorToFriend = "#9ACD32";

            public static ConfigData GetNewCong()
            {
                ConfigData newConfig = new ConfigData();
                newConfig.UseHit = false;
                newConfig.TimeToDestroy = 1.0f;
                newConfig.ColorToFriend = "#9ACD32";
                return newConfig;
            }
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<ConfigData>();
                
            }
            catch
            {
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }
        protected override void LoadDefaultConfig() => _config = ConfigData.GetNewCong();
        protected override void SaveConfig() => Config.WriteObject(_config);
        
        #endregion
        
        #region Hooks
        [PluginReference] private Plugin ImageLibrary;
        private void OnServerInitialized()
        {
            LoadImages();
        }
        void LoadImages()
        {
            foreach (var imgKey in Images)
            {
                ImageLibrary.Call("AddImage", imgKey.Value, imgKey.Key);
            }
        }
        Dictionary<string, string> Images = new Dictionary<string, string>()
        {
            { "hitmarker.kill", "https://i.postimg.cc/Jhcm0VNr/image.png" },
            { "hitmarker.hit.normal", "" },
            { "hitmarker.hit.wound", "https://i.postimg.cc/t4690V7V/removebg-preview.png" },
        };
        
        private void OnPlayerAttack(BasePlayer attacker, HitInfo info)
        {
            BaseEntity entity = info.HitEntity;
            
            if (entity == null || !(entity is BasePlayer)) return;
            
            NextTick(() =>
            {
                BasePlayer victim = entity as BasePlayer;
                if (victim.IsDead() || victim.IsWounded()) return;
                
                if (attacker.currentTeam == victim.currentTeam && attacker.currentTeam != 0)
                {
                    HitGUI(attacker, $"<color={_config.ColorToFriend}>{victim.displayName}</color>");
                }
                else if (info.isHeadshot)
                {
                    HitGUI(attacker, $"<color=#ffffff>{info.damageTypes.Total().ToString("F0")}</color>");
                    HitPng(attacker, "hitmarker.hit.normal", "1 1 1 0.6");
                }
                else
                {
                    HitGUI(attacker, info.damageTypes.Total().ToString("F0"));
                    HitPng(attacker, "hitmarker.hit.normal", "1 1 1 0.6");
                }
            });
        }
        
        private void OnExplosiveThrown(BasePlayer player, BaseEntity entity)
        {
            if (entity is TimedExplosive || entity.name.Contains("rocket"))
            {
                entity.gameObject.AddComponent<ExplosiveTracker>().Init(player, this);
            }
        }
        
        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            var player = entity.ToPlayer();
            if (player == null) return;
            var attacker = info?.Initiator as BasePlayer;
            if (attacker == null) return;
            bool check = false;
            if (info.isHeadshot)
                check = true;
            CuiHelper.DestroyUi(attacker, "normalhit");
            HitPng(attacker, "hitmarker.kill", check == true ? "1 0 0 0.6" : "1 1 1 0.6");
        }
        
        void OnPlayerWound(BasePlayer player)
        {
            var attacker = player?.lastAttacker as BasePlayer;
            if (attacker == null) return;
            CuiHelper.DestroyUi(attacker, "normalhit");
            HitPng(attacker, "hitmarker.hit.wound", "1 1 1 0.6");
        }

        private void OnExplosionDamage(BasePlayer victim, HitInfo info)
        {
            if (victim == null || info == null) return;
            
            var attacker = info.Initiator as BasePlayer;
            if (attacker == null) return;

            NextTick(() =>
            {
                if (victim.IsDead() || victim.IsWounded()) return;
                
                if (attacker.currentTeam == victim.currentTeam && attacker.currentTeam != 0)
                {
                    HitGUI(attacker, $"<color={_config.ColorToFriend}>{victim.displayName}</color>");
                }
                else
                {
                    float damage = info.damageTypes.Total();
                    HitGUI(attacker, damage.ToString("F0"));
                    if (_config.UseHit)
                    {
                        HitPng(attacker, "hitmarker.hit.normal", "1 1 1 0.6");
                    }
                }
            });
        }

        #endregion

        #region GUI

        private void HitGUI(BasePlayer attacker, string text)
        {
            CuiHelper.DestroyUi(attacker, "normalhit");
            CuiElementContainer container = new CuiElementContainer();
            
            container.Add(new CuiElement
            {
                Parent = "Hud",
                Name = "normalhit",
                FadeOut = 0.2f,
                Components =
                {
                    new CuiTextComponent { Text = text, Align = TextAnchor.MiddleCenter, FontSize = 13, FadeIn = 0.4f },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-100 -25", OffsetMax = "100 -5" },
                    new CuiOutlineComponent { Color = "0 0 0 0", Distance = "0.15 0.15" }
                }
            });    
            
            CuiHelper.AddUi(attacker, container);
            attacker.Invoke(() => CuiHelper.DestroyUi(attacker, "normalhit"), _config.TimeToDestroy);
        }

        private void HitPng(BasePlayer attacker, string png, string check)
        {
            CuiHelper.DestroyUi(attacker, "hitpng");
            CuiElementContainer container = new CuiElementContainer();
            string offmax = "20 20";
            string offmin = "-20 -20";
            float Fade = 0.5f;
            if (png == "hitmarker.hit.normal")
            {
                offmax = "10 10";
                offmin = "-10 -10";
                Fade = 0.1f;
                if (!_config.UseHit)
                    return;
            }
            container.Add(new CuiElement
            {
                Parent = "Hud",
                Name = "hitpng",
                FadeOut = Fade,
                Components =
                {
                    new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", png), Color = check },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = offmin, OffsetMax = offmax }
                }
            });

            CuiHelper.AddUi(attacker, container);
            attacker.Invoke(() => CuiHelper.DestroyUi(attacker, "hitpng"), _config.TimeToDestroy);
        }
        #endregion
        
        #region Explosive Tracker
        public class ExplosiveTracker : MonoBehaviour
        {
            private BasePlayer _owner;
            private BaseEntity _explosive;
            private FHitMarker _plugin;
            
            public void Init(BasePlayer owner, FHitMarker plugin)
            {
                _owner = owner;
                _explosive = GetComponent<BaseEntity>();
                _plugin = plugin;
                InvokeRepeating(nameof(CheckExplosion), 0.1f, 0.1f);
            }
            
            void CheckExplosion()
            {
                if (_explosive == null || _explosive.IsDestroyed)
                {
                    Destroy(this);
                    return;
                }
            }
            
            void OnDestroy()
            {
                CancelInvoke();
            }
        }
        #endregion
    }
}