using System;
using System.Linq;
using Newtonsoft.Json;
using Color = UnityEngine.Color;
using Oxide.Core.Plugins;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

///Скачано с дискорд сервера Rust Edit [PRO+]
///discord.gg/9vyTXsJyKR

namespace Oxide.Plugins
{
    [Info("IQHotPoints", "discord.gg/9vyTXsJyKR", "1.0.7")]
    [Description("Ваши маркеры с активностью PVP")]
    internal class IQHotPoints : RustPlugin
    {
        public class PVPMarker : FacepunchBehaviour
        {
            public Boolean IsPvP = false;
            private Configuration.MarkerSetting Settings;
            public MapMarkerGenericRadius marker = null;
            public Coroutine routine;
            public Int32 Stage = 0;
            public Int32 LastStage = 0;
            void Awake()
            {
                this.gameObject.layer = (Int32)Rust.Layer.Reserved1;
                this.gameObject.name = "PVP_MARKER_Z3333";
            }
            public void CreateMarker(Vector3 position, Boolean IsPvP)
            {
                gameObject.SetActive(true);
                enabled = true;

                marker = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", position) as MapMarkerGenericRadius;

                if (marker == null)
                    return;

                this.IsPvP = IsPvP;
                Settings = this.IsPvP ? config.PvPMarker : config.RaidesMarker;
		   		 		  						  	   		  	  			  						  		  		   			
                marker.name = "PVP_ZONE";
                marker.radius = 0;
                marker.Spawn();
                UpdateStage(true, true);
            }
            public void UpdateNetworkMarker()
            {
                if (marker != null && !marker.IsDestroyed)
                    marker.SendUpdate();
            }
            public void UpdateMarker()
            {
                if (Stage >= 0)
                {
                    marker.alpha = GetAlphaMarker(Settings.StagesSettingListList, Stage);
                    if (!ColorUtility.TryParseHtmlString(GetColorMarker(Settings.StagesSettingListList, Stage), out marker.color1))
                    {
                        marker.color1 = Color.black;
                        Debug.Log($"#33421411 Invalid map marker color1: {GetColorMarker(Settings.StagesSettingListList, Stage)}");
                    }
                    if (!ColorUtility.TryParseHtmlString(GetColorOutlineMarker(Settings.StagesSettingListList, Stage), out marker.color2))
                    {
                        marker.color2 = Color.white;
                        Debug.Log($"#23421433 Invalid map marker color2: {GetColorOutlineMarker(Settings.StagesSettingListList, Stage)}");
                    }
                }
                marker.SendUpdate();
                if(routine != null)
                {
                    StopCoroutine(routine);
                    routine = null;
                }
                routine = StartCoroutine(UpdateRadius(GetRadius()));
            }

                        public IEnumerator UpdateRadius(Single Radius)
            {
                Int32 Softness = Settings.Softness;
                if (LastStage > Stage)
                {
                    for (int i = 0; i < Softness; i++)
                    {
                        if (marker.radius > 0)
                        {
                            marker.radius -= (Radius - marker.radius) / -(Single)Softness; /// Порно 18+
                            marker.SendUpdate();
                            yield return CoroutineEx.waitForSeconds(0.6f);
                        } else
                        {
                            if (routine != null)
                            {
                                StopCoroutine(routine);
                                routine = null;
                            }

                            Kill();
                            yield break;
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < Softness; i++)
                    {
                        marker.radius += (Radius - marker.radius) / (Single)Softness;
                        marker.SendUpdate();
                        yield return CoroutineEx.waitForSeconds(0.6f);
                    }
                }

                yield return CoroutineEx.waitForSeconds(GetDurationMarker(Settings.StagesSettingListList, Stage));

                UpdateStage(false, false);
            }

            public void UpdateStage(Boolean increaseOrreduce, Boolean IsNew)
            {
                if (!IsNew)
                {
                    LastStage = Stage;
                    if (increaseOrreduce)
                        Stage++;
                    else Stage--;
                }

                UpdateMarker();
            }
            public Single GetRadius()
            {
                Single Radius = 0;
                Radius = Settings.StartRadius * ((Single)Stage + (Stage == 0 ? 1f : Stage));
		   		 		  						  	   		  	  			  						  		  		   			
                if (Radius > Settings.MaximumRadius)
                    Radius = Settings.MaximumRadius;
		   		 		  						  	   		  	  			  						  		  		   			
                return Radius;
            }
            
            public void OnDestroy()
            {
                Destroy(gameObject);
            }
            public void Kill()
            {
                if (routine != null)
                    routine = null;

                if (ManagerList.Contains(this))
                    ManagerList.Remove(this);

                marker?.Kill();
                Destroy(gameObject);
            }

        }
        void Unload()
        {
            foreach (var Zones in ManagerList.Where(x => x != null))
            {
                if (Zones.marker != null)
                    Zones.marker.Kill();

                UnityEngine.Object.DestroyImmediate(Zones.gameObject);
            }

            _ = null;
        }
        
        
        
              
        
        private static Configuration config = new Configuration();

                [PluginReference] Plugin  Friends, Clans, Battles, Duel, Duelist;
        void OnPlayerDeath(BasePlayer dead, HitInfo info)
        {
            if (dead == null
            || info == null
            || dead.transform == null
            || info.InitiatorPlayer == null
            || info.Weapon == null
            || info.InitiatorPlayer.IsNpc
            || info.damageTypes.GetMajorityDamageType() == Rust.DamageType.Explosion
            || info.damageTypes.GetMajorityDamageType() == Rust.DamageType.Stab
            || info.damageTypes.GetMajorityDamageType() == Rust.DamageType.Blunt) return;

            if (!config.PvPMarker.UseNPCDeath && dead.IsNpc) return;
            if (IsFriends(dead.userID, info.InitiatorPlayer.userID)
            || IsClans(dead.UserIDString, info.InitiatorPlayer.UserIDString)
            || IsDuel(dead.userID)) return;

            DetectedKill(dead.transform.position, true);
        }

        void OnServerInitialize()
        {
            _ = this;

            if (!config.RaidesMarker.UseMarker)
                Unsubscribe("OnEntityDeath");

            if (!config.PvPMarker.UseMarker)
            {
                Unsubscribe("OnEntityTakeDamage");
                Unsubscribe("OnPlayerDeath");
            }
        }
		   		 		  						  	   		  	  			  						  		  		   			
        protected override void LoadDefaultConfig() => config = Configuration.GetNewConfiguration();
        public Boolean IsDuel(UInt64 userID)
        {
            if (Battles)
                return (Boolean)Battles?.Call("IsPlayerOnBattle", userID);
            else if (Duel) return (Boolean)Duel?.Call("IsPlayerOnActiveDuel", BasePlayer.FindByID(userID));
            else if (Duelist) return (Boolean)Duelist?.Call("inEvent", BasePlayer.FindByID(userID));
            else return false;
        }
        private static Int32 GetDurationMarker(List<Configuration.MarkerSetting.StagesSettingList> StageSetting, Int32 Stage = 0)
        {
            if (StageSetting.Count - 1 < Stage) return StageSetting[StageSetting.Count - 1].Duration;
            return StageSetting[Stage].Duration;
        }

                public Boolean IsFriends(UInt64 userID, UInt64 targetID)
        {
            if (Friends)
                return (Boolean)Friends?.Call("HasFriend", userID, targetID);
            else return false;
        }
        
                private void OnPlayerConnected(BasePlayer player)
        {
            if (ManagerList == null || ManagerList.Count == 0) return;

            foreach (PVPMarker marker in ManagerList.Where(x => x != null && !x.marker.IsDestroyed))
                marker.UpdateNetworkMarker();
        }
        
                private void DetectedKill(Vector3 position, Boolean IsPVP)
        {
            if (position == null) return;

            if (IsPVP && !config.PvPMarker.UseMarker) return;
            if (!IsPVP && !config.RaidesMarker.UseMarker) return;
		   		 		  						  	   		  	  			  						  		  		   			
            var Zone = ManagerList.FirstOrDefault(manager => manager != null && manager.marker != null && manager.IsPvP == IsPVP && (Vector3.Distance(manager.marker.transform.position, position) <= 250f * (IsPVP ? config.PvPMarker.MaximumRadius : config.RaidesMarker.MaximumRadius)));
            if (Zone != null && Zone.marker != null)
            {
                Zone.UpdateStage(true,false);
                return;
            }
            else
            {
                PVPMarker marker = new GameObject().AddComponent<PVPMarker>() as PVPMarker;
                if (marker == null) return;

                marker.CreateMarker(position, IsPVP);
                ManagerList.Add(marker);
            }
        }

        private static String GetColorMarker(List<Configuration.MarkerSetting.StagesSettingList> StageSetting, Int32 Stage = 0)
        {
            if (StageSetting.Count - 1 < Stage) return StageSetting[StageSetting.Count - 1].ColorMarker;
            return StageSetting[Stage].ColorMarker;
        }
        void OnEntityDeath(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (entity == null || hitInfo == null || hitInfo.Initiator == null || !IsEntityBlocked(entity))
                return;
		   		 		  						  	   		  	  			  						  		  		   			
            DetectedKill(entity.transform.position, false);
        }
        /// <summary>
        /// - Обновление 1.0.7
        /// - Переписал метод проверки на кланы
        /// 
        /// </summary>
        private const Boolean LanguageEn = false;
        void OnEntityTakeDamage(BasePlayer wanted, HitInfo info)
        {
            if (wanted == null || info == null) return;
            BasePlayer damager = info.InitiatorPlayer;
            if (info.damageTypes.GetMajorityDamageType() == Rust.DamageType.Explosion 
            || info.damageTypes.GetMajorityDamageType() == Rust.DamageType.Stab
            || info.damageTypes.GetMajorityDamageType() == Rust.DamageType.Blunt)
            {
                Unsubscribe("OnPlayerDeath");
                NextTick(() =>
                {
                    if (damager != null && damager.transform.position != null)
                    {
                        if (IsFriends(damager.userID, wanted.userID)
                        || IsClans(damager.UserIDString, wanted.UserIDString)
                        || IsDuel(wanted.userID)) return;

                        if (!wanted.IsDead()) return;
                        DetectedKill(damager.transform.position, true);
                    }
                    Subscribe("OnPlayerDeath");
                });
            }
            return;
        }
        private class Configuration
        {
            [JsonProperty(LanguageEn ? "Setting up a Raid Marker" : "Настройка Raid-Маркера")]
            public MarkerSetting RaidesMarker = new MarkerSetting();

            public static Configuration GetNewConfiguration()
            {
                return new Configuration
                {
                    PvPMarker = new MarkerSetting
                    {
                        Softness = 10,
                        UseNPCDeath = true,
                        StartRadius = 0.13f,
                        MaximumRadius = 1f,
                        StagesSettingListList = new List<MarkerSetting.StagesSettingList>
                        {
                            new MarkerSetting.StagesSettingList
                            {
                                ColorMarker = "#70ff36",
                                ColorOutline = "#70ff36",
                                AlphaMarker = 0.3f,
                                Duration = 5,
                            },
                            new MarkerSetting.StagesSettingList
                            {
                                ColorMarker = "#d5ff36",
                                ColorOutline = "#d5ff36",
                                AlphaMarker = 0.35f,
                                Duration = 10,
                            },
                            new MarkerSetting.StagesSettingList
                            {
                                ColorMarker = "#f7ff36",
                                ColorOutline = "#f7ff36",
                                AlphaMarker = 0.45f,
                                Duration = 15,
                            },
                            new MarkerSetting.StagesSettingList
                            {
                                ColorMarker = "#ffd836",
                                ColorOutline = "#ffd836",
                                AlphaMarker = 0.55f,
                                Duration = 20,
                            },
                            new MarkerSetting.StagesSettingList
                            {
                                ColorMarker = "#ffac36",
                                ColorOutline = "#ffac36",
                                AlphaMarker = 0.65f,
                                Duration = 25,
                            },
                            new MarkerSetting.StagesSettingList
                            {
                                ColorMarker = "#ff9e36",
                                ColorOutline = "#ff9e36",
                                AlphaMarker = 0.75f,
                                Duration = 30,
                            },
                            new MarkerSetting.StagesSettingList
                            {
                                ColorMarker = "#ff9e36",
                                ColorOutline = "#ff9e36",
                                AlphaMarker = 0.8f,
                                Duration = 35,
                            },
                            new MarkerSetting.StagesSettingList
                            {
                                ColorMarker = "#ff6f36",
                                ColorOutline = "#ff6f36",
                                AlphaMarker = 0.85f,
                                Duration = 45,
                            },
                            new MarkerSetting.StagesSettingList
                            {
                                ColorMarker = "#ff5736",
                                ColorOutline = "#ff5736",
                                AlphaMarker = 0.90f,
                                Duration = 50,
                            },
                            new MarkerSetting.StagesSettingList
                            {
                                ColorMarker = "#ff3c1e",
                                ColorOutline = "#ff3c1e",
                                AlphaMarker = 0.95f,
                                Duration = 55,
                            },
                            new MarkerSetting.StagesSettingList
                            {
                                ColorMarker = "#ff0000",
                                ColorOutline = "#ff0000",
                                AlphaMarker = 1f,
                                Duration = 60,
                            },
                        }
                    },
                    RaidesMarker = new MarkerSetting
                    {
                        Softness = 10,
                        UseNPCDeath = true,
                        StartRadius = 0.13f,
                        MaximumRadius = 1f,
                        StagesSettingListList = new List<MarkerSetting.StagesSettingList>
                        {
                            new MarkerSetting.StagesSettingList
                            {
                                ColorMarker = "#36dcff",
                                ColorOutline = "#36dcff",
                                AlphaMarker = 0.3f,
                                Duration = 5,
                            },
                            new MarkerSetting.StagesSettingList
                            {
                                ColorMarker = "#36adff",
                                ColorOutline = "#36adff",
                                AlphaMarker = 0.35f,
                                Duration = 10,
                            },
                            new MarkerSetting.StagesSettingList
                            {
                                ColorMarker = "#3698ff",
                                ColorOutline = "#3698ff",
                                AlphaMarker = 0.45f,
                                Duration = 15,
                            },
                            new MarkerSetting.StagesSettingList
                            {
                                ColorMarker = "#3692ff",
                                ColorOutline = "#3692ff",
                                AlphaMarker = 0.55f,
                                Duration = 20,
                            },
                            new MarkerSetting.StagesSettingList
                            {
                                ColorMarker = "#3670ff",
                                ColorOutline = "#3670ff",
                                AlphaMarker = 0.65f,
                                Duration = 25,
                            },
                            new MarkerSetting.StagesSettingList
                            {
                                ColorMarker = "#3652ff",
                                ColorOutline = "#3652ff",
                                AlphaMarker = 0.75f,
                                Duration = 30,
                            },
                            new MarkerSetting.StagesSettingList
                            {
                                ColorMarker = "#8a36ff",
                                ColorOutline = "#8a36ff",
                                AlphaMarker = 0.8f,
                                Duration = 35,
                            },
                            new MarkerSetting.StagesSettingList
                            {
                                ColorMarker = "#9b36ff",
                                ColorOutline = "#9b36ff",
                                AlphaMarker = 0.85f,
                                Duration = 45,
                            },
                            new MarkerSetting.StagesSettingList
                            {
                                ColorMarker = "#ac36ff",
                                ColorOutline = "#ac36ff",
                                AlphaMarker = 0.90f,
                                Duration = 50,
                            },
                            new MarkerSetting.StagesSettingList
                            {
                                ColorMarker = "#c336ff",
                                ColorOutline = "#c336ff",
                                AlphaMarker = 0.95f,
                                Duration = 55,
                            },
                            new MarkerSetting.StagesSettingList
                            {
                                ColorMarker = "#b236ff",
                                ColorOutline = "#b236ff",
                                AlphaMarker = 1f,
                                Duration = 60,
                            },
                        }
                    }
                    
                };
            }
            internal class MarkerSetting
            {
                internal class StagesSettingList
                {
                    [JsonProperty(LanguageEn ? "HEX : Marker color" : "HEX : Цвет маркера")]
                    public String ColorMarker;
                    [JsonProperty(LanguageEn ? "Marker/Stage Lifetime (Seconds)" : "Время жизни маркера/стадии (секунды)")]
                    public Int32 Duration;
                    [JsonProperty(LanguageEn ? "0.0 - 1.0 - marker transparency" : "0.0 - 1.0 - прозрачность маркера")]
                    public Single AlphaMarker;
                    [JsonProperty(LanguageEn ? "HEX : Marker outline color" : "HEX : Цвет обводки маркера")]
                    public String ColorOutline;
                }
                [JsonProperty(LanguageEn ? "Display marker when killing NPCs" : "Отображать маркер при убийстве NPC")]
                public Boolean UseNPCDeath;
                [JsonProperty(LanguageEn ? "Customizing marker stages" : "Настройка стадий маркера")]
                public List<StagesSettingList> StagesSettingListList = new List<StagesSettingList>();
                [JsonProperty(LanguageEn ? "Use marker? (true - yes/false - no)" : "Использовать маркер? (true - да/false - нет)")]
                public Boolean UseMarker;
                [JsonProperty(LanguageEn ? "Marker start radius" : "Стартовый радиус маркера")]
                public Single StartRadius;
                [JsonProperty(LanguageEn ? "Smoothness of marker stage removal" : "Плавность удаления стадии маркера")]
                public Int32 Softness = 10;
                [JsonProperty(LanguageEn ? "Maximum marker radius" : "Максимальный радиус маркера")]
                public Single MaximumRadius;

            }
            [JsonProperty(LanguageEn ? "Setting up the PVP Marker" : "Настройка PVP-Маркера")]
            public MarkerSetting PvPMarker = new MarkerSetting();
        }
        private static Single GetAlphaMarker(List<Configuration.MarkerSetting.StagesSettingList> StageSetting, Int32 Stage = 0)
        {
            if (StageSetting.Count - 1 < Stage) return StageSetting[StageSetting.Count - 1].AlphaMarker;
            return StageSetting[Stage].AlphaMarker;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();
            }
            catch
            {//
                PrintWarning(LanguageEn ? $"Error #58 reading configuration'oxide/config/{Name}', create a new configuration!" : $"Ошибка #58 чтения конфигурации 'oxide/config/{Name}', создаём новую конфигурацию!!");
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }
        private static String GetColorOutlineMarker(List<Configuration.MarkerSetting.StagesSettingList> StageSetting, Int32 Stage = 0)
        {
            if (StageSetting.Count - 1 < Stage) return StageSetting[StageSetting.Count - 1].ColorOutline;
            return StageSetting[Stage].ColorOutline;
        }

                public static List<PVPMarker> ManagerList = new List<PVPMarker>();
        private bool IsEntityBlocked(BaseCombatEntity entity)
        {
            if (entity is BuildingBlock)
            {
                if (((BuildingBlock)entity).grade == BuildingGrade.Enum.Twigs) return false;
                return true;
            }
            else if (entity is Door)
            {
                return true;
            }
            return false;
        }
        private static IQHotPoints _;
        private bool IsClans(String userID, String targetID)
        {
            if (Clans)
            {
                String TagUserID = (String)Clans?.Call("GetClanOf", userID);
                String TagTargetID = (String)Clans?.Call("GetClanOf", targetID);
                if (TagUserID == null && TagTargetID == null)
                    return false;
                return (bool)(TagUserID == TagTargetID);
            }
            else
                return false;
        }
        protected override void SaveConfig() => Config.WriteObject(config);
        
            }
}
