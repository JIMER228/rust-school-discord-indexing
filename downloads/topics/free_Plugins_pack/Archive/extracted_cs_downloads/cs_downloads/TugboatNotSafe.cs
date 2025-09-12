using Oxide.Core;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using UnityEngine;
using System.Linq;

namespace Oxide.Plugins{
    [Info("TugboatNotSafe", "ninco90", "1.0.1")]
    [Description("Prevents players from leaving the Tugboat in a safe zone.")]

    public class TugboatNotSafe : RustPlugin{

        private static TugboatNotSafe Instance { get; set; }
        private List<Vector3> Fishing = new List<Vector3>();
        private HashSet<BaseEntity> spheres = new HashSet<BaseEntity>();
        private Dictionary<int, TugZoneBlock> zonerad = new Dictionary<int, TugZoneBlock>();

        #region Hooks

        void OnServerInitialized(){
            Instance = this;
            Fishing = UnityEngine.Object.FindObjectsOfType<MonumentInfo>().Where(x => x.gameObject.name.ToLower().Contains("fishing")).Select(x => x.transform.position).ToList();

            for (int i = 0; i < Fishing.Count; i++){
                TugZoneBlock newZone = new GameObject().AddComponent<TugZoneBlock>();
                newZone.Config(i, Fishing[i]);
                zonerad.Add(i, newZone);
            }
		}

        private void Unload(){
            foreach (BaseEntity sphere in spheres) { if (!sphere.IsDestroyed) sphere.Kill(); }
            var list = zonerad.ToList();
            foreach (var x in list){
                if (x.Value == null) continue;
                UnityEngine.Object.Destroy(x.Value);
            }
            Instance = null;
        }
        #endregion

        private class TugZoneBlock : MonoBehaviour
        {
            private BaseEntity sphere;
            public bool sphereactive = false;
            private List<Tugboat> joins = new List<Tugboat>();
            public Vector3 position;

            private void Awake(){
                gameObject.layer = (int)Rust.Layer.Reserved1;
                enabled = false;
            }

            private void OnDestroy() => Destroy(gameObject);

            public void Config(int type, Vector3 position){
                this.position = position;

                gameObject.name = "fishing"+type;
                transform.position = position;
                SphereCollider sphere = gameObject.GetComponent<SphereCollider>() ?? gameObject.AddComponent<SphereCollider>();
                sphere.isTrigger = true;
                sphere.radius = Instance.config.GLOBAL.zoneRadius;

                if(Instance.config.GLOBAL.zoneShow && !this.sphereactive) Sphere();
            }

            public void Sphere(){
                this.sphere = GameManager.server.CreateEntity("assets/bundled/prefabs/modding/events/twitch/br_sphere.prefab", this.position);
                SphereEntity entity = this.sphere as SphereEntity;
                entity.currentRadius = Instance.config.GLOBAL.zoneRadius * 2;
                entity.lerpSpeed = 0f;
                this.sphere.enableSaving = false;
                this.sphere.Spawn();
                this.sphereactive = true;
                Instance.spheres.Add(sphere);
            }

            private void OnTriggerEnter(Collider obj){
                Tugboat tug = obj?.GetComponentInParent<Tugboat>();
                if (tug != null) {
                    if(!joins.Contains(tug)){
                        joins.Add(tug);
                        if(Instance.config.GLOBAL.debug) Instance.Puts("Tugboat Enter Zone!");

                        if(Instance.config.GLOBAL.mode_expulsion){
                            Vector3 euler = tug.transform.eulerAngles;
                            tug.transform.rotation = Quaternion.Euler(euler.x, euler.y - 180f, euler.z);
                            tug.rigidBody.velocity *= -1f;
                            Instance.timer.Once(0.5f, () => {
                                if(joins.Contains(tug)){
                                    joins.Remove(tug);
                                    if(Instance.config.GLOBAL.debug) Instance.Puts("Tugboat Delete in Zone!");
                                }
                            });

                            if (Instance.config.GLOBAL.chat && tug.GetDriver() != null) Instance.PrintToChat(tug.GetDriver(), Instance.Languaje("EnterZoneExpulsion", tug.GetDriver().UserIDString));
                        }

                        if(Instance.config.GLOBAL.mode_damage){
                            TugDamage an = tug.gameObject.GetComponent<TugDamage>();
                            if (an == null){
                                an = tug.gameObject.AddComponent<TugDamage>();
                                an.damage = true;
                            } else {
                                an.damage = true;
                                an.Damage();
                            }

                            if (!Instance.config.GLOBAL.mode_expulsion && Instance.config.GLOBAL.chat && tug.GetDriver() != null) Instance.PrintToChat(tug.GetDriver(), Instance.Languaje("EnterZoneDamage", tug.GetDriver().UserIDString, Instance.config.GLOBAL.timeDelay));
                        }
                    }
                }
            }

            private void OnTriggerExit(Collider obj){
                Tugboat tug = obj?.GetComponentInParent<Tugboat>();
                if (tug != null) {
                    tug.gameObject.GetComponent<TugDamage>().damage = false;
                    if(!Instance.config.GLOBAL.mode_expulsion && joins.Contains(tug)){
                        joins.Remove(tug);
                        if(Instance.config.GLOBAL.debug) Instance.Puts("Tugboat Delete in Zone!");
                        if(Instance.config.GLOBAL.chat && tug.GetDriver() != null) Instance.PrintToChat(tug.GetDriver(), Instance.Languaje("ExitZoneDamage", tug.GetDriver().UserIDString));
                    } 
                }
            }
        }

        private class TugDamage : MonoBehaviour
        {
            private Tugboat tug;
            public bool damage = false;

            private void Awake(){
                tug = GetComponent<Tugboat>();
                if (tug == null) Destroy(this);
                if(Instance.config.GLOBAL.debug) Instance.Puts("Tugboat System Damage Added!");
                Damage();
            }

            private void OnDestroy() => Destroy(gameObject);

            public void Damage(){
                Instance.timer.Once(Instance.config.GLOBAL.timeDelay, () => Damage2());
            }

            public void Damage2(){
                if(!damage) return;
                if(tug == null) return;
                if(Instance.config.GLOBAL.debug) Instance.Puts("Damage Enabled: " + damage + " - Dying: " + tug.IsDying + " - IsDestroyed: " + tug.IsDestroyed + " - Health: " + tug.health);
                if(tug.health == 0.0f){
                    tug.OnDied(null);
                    if(Instance.config.GLOBAL.timeKill != 0.0f) Instance.timer.Once(Instance.config.GLOBAL.timeKill, () => tug.Kill());
                    return;
                } else {
                    if (Instance.config.GLOBAL.chat && tug.GetDriver() != null) Instance.PrintToChat(tug.GetDriver(), Instance.Languaje("AlertDamage", tug.GetDriver().UserIDString));

                    Vector3 pos = tug.transform.position;
                    Effect.server.Run(Instance.config.GLOBAL.soundPrefab, new Vector3(pos.x, pos.y + 0.40f, pos.z));
                    tug.health -= Instance.config.GLOBAL.amountDamage;
                }
                tug.SendNetworkUpdate();

                Instance.timer.Once(Instance.config.GLOBAL.timeDamage, () => Damage2());
            }
        }

        #region Config
        private ConfigData config;

        private class ConfigData {
			[JsonProperty(PropertyName = "Global Config")]
			public GlobalConfig GLOBAL;
        }

		private class GlobalConfig {
            [JsonProperty(PropertyName = "Debug")]
            public bool debug;

			[JsonProperty(PropertyName = "Notice in the Chat")]
            public bool chat;

            [JsonProperty(PropertyName = "Zone Radius")]
            public float zoneRadius;

            [JsonProperty(PropertyName = "Show Zone Boundary")]
            public bool zoneShow;

            [JsonProperty(PropertyName = "Mode: Expulsion (Eject tugboats when they try to enter the fishing village)")]
            public bool mode_expulsion;

            [JsonProperty(PropertyName = "Mode: Damage (Gradually damage Tugboats within the fishing village)")]
            public bool mode_damage;

            [JsonProperty(PropertyName = "Warning Time Before Damage")]
            public float timeDelay;

            [JsonProperty(PropertyName = "Time Between Damage")]
            public float timeDamage;

            [JsonProperty(PropertyName = "Amount Damage")]
            public float amountDamage;

            [JsonProperty(PropertyName = "Time in seconds to eliminate a destroyed Tugboat (Only in Safe Zone) [0.0 = Decay Default Tugboat]")]
            public float timeKill;

            [JsonProperty(PropertyName = "Effects Prefab")]
            public string soundPrefab;
        }

		private ConfigData GetDefaultConfig() {
            return new ConfigData {
				GLOBAL = new GlobalConfig {
                    debug = false,
                    chat = true,
                    zoneRadius = 75.0f,
                    zoneShow = false,
                    mode_expulsion = true,
                    mode_damage = true,
                    timeDelay = 60.0f,
                    timeDamage = 15.0f,
                    amountDamage = 100.0f,
                    timeKill = 0.0f,
                    soundPrefab = "assets/prefabs/npc/patrol helicopter/damage_effect_debris.prefab"
                }
			};
		}

		protected override void LoadConfig(){
            base.LoadConfig();
            try {
                config = Config.ReadObject<ConfigData>();
                if (config == null){
                    LoadDefaultConfig();
                }
            } catch {
                PrintError("Configuration file is corrupt! Unloading plugin...");
                Interface.Oxide.RootPluginManager.RemovePlugin(this);
                return;
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig(){
            config = GetDefaultConfig();
        }

        protected override void SaveConfig(){
            Config.WriteObject(config);
        }
		#endregion

        #region Languaje
        protected override void LoadDefaultMessages() {
            lang.RegisterMessages(new Dictionary<string, string> {
                ["EnterZoneDamage"] = "Warning! You have <color=#FFA500>{0} seconds</color> to leave the safe zone before your Tugboat starts taking damage.",
                ["EnterZoneExpulsion"] = "Careful! You cannot enter a Tugboat in a <color=#FFA500>Safe Zone</color>. You don't want to use this protection site so you don't get raided.",
                ["ExitZoneDamage"] = "You have left the Safe Zone so your Tugboat will no longer take any more damage.",
                ["AlertDamage"] = "<color=#FFA500>Leave the area to stop taking damage!</color>"
            }, this);

            lang.RegisterMessages(new Dictionary<string, string> {
                ["EnterZoneDamage"] = "¡Advertencia! Tienes <color=#FFA500>{0} segundos</color> para abandonar la zona segura antes de que tu remolcador comience a sufrir daños.",
                ["EnterZoneExpulsion"] = "¡Cuidado! No puedes ingresar un remolcador en una <color=#FFA500>Zona segura</color>. No intentes utilizar esta zona como protección anti-raideo.",
                ["ExitZoneDamage"] = "Has abandonado la zona segura, por lo que tu remolcador ya no sufrirá más daños.",
                ["AlertDamage"] = "<color=#FFA500>¡Sal del área para dejar de sufrir daños!</color>"
            }, this, "es-ES");
        }

        private string Languaje(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private void PrintToChat(BasePlayer player, string message) => Player.Message(player, "<color=#f74d31>TugboatNotSafe:</color> " + message);
        #endregion
    }
}