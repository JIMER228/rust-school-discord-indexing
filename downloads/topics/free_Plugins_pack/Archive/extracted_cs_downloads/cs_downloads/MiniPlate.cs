 using Newtonsoft.Json;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Minicopter Licence Plate", "The Friendly Chap", "1.1.4")]
    [Description("Spawn a licence plate (Small Wooden Board) at the back of the minicopter, with optional permissions.")]
    class MiniPlate : RustPlugin
    {
        const string _platePrefab = "assets/prefabs/deployable/signs/sign.small.wood.prefab";
        private static readonly Vector3 PlatePosition = new Vector3(0.0f, 0.20f, -0.85f);
        private static readonly Quaternion PlateRotation = Quaternion.Euler(180, 0, 180);
        #region ConfigData
        private ConfigData configData;
        class ConfigData
        {
            [JsonProperty(PropertyName = "Use Permissions")]
            public bool miniPerms = false;
            [JsonProperty(PropertyName = "Debug")]
            public bool miniDebug = false;
        }
        private bool LoadConfigVariables()
        {
            try
            {
                configData = Config.ReadObject<ConfigData>();
            }
            catch
            {
                return false;
            }
            SaveConfig(configData);
            return true;
        }
        void Init()
        {
            permission.RegisterPermission("miniplate.use", this);

            if (!LoadConfigVariables())
            {
                Puts("Config file issue detected. Please delete file, or check syntax and fix.");
            }
        }
        protected override void LoadDefaultConfig()
        {
            Puts("Creating new config file.");
            configData = new ConfigData();
            SaveConfig(configData);
        }
        void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, true);
        }
        #endregion
        private void OnServerInitialized()
        {
            FindBabies();
        }
        private void Fstartup()
        {
            timer.Once(10f, () =>
            {
                try
                {
                    if (Rust.Application.isLoading)
                    {
                        Fstartup();
                        return;
                    }
                }
                catch { }
                Fstartup();
            });
        }
        void FindBabies()
        {
            foreach (var playerMini in BaseNetworkable.serverEntities.OfType<Minicopter>())
            {
                if (!(playerMini.children == null))
                {
                    if (configData.miniDebug) Puts($"Debug NotNull : {playerMini.children}");
                    {
                        foreach (var babyEnt in playerMini.children)
                        {
                            if (configData.miniDebug) Puts($"Debug Child {babyEnt}");
                            DestroyMeshCollider(babyEnt);
                            DestroyGroundComp(babyEnt);
                            FixRigidBody(babyEnt);
                        }
                    }
                }
            }
        }
        void OnEntitySpawned(Minicopter mini, ConfigData config)
        {
            if (configData.miniPerms)
            {
                string userID = $"{mini.OwnerID}";
                if (!permission.UserHasPermission(userID, "miniplate.use")) return;
                SetupMini(mini);
                return;
            }
            else
            {
                SetupMini(mini);
                return;
            }
        }
        public void SetupMini(BaseVehicle vehicle)
        {
            MakeNumplate(vehicle, PlatePosition);
        }
        void MakeNumplate(BaseVehicle vehicle, Vector3 position)
        {   //Make a Prefab
            BaseEntity entity = GameManager.server.CreateEntity(_platePrefab, vehicle.transform.position, PlateRotation);
            //Kick out if it fucked up             
            if (entity == null) return;
            //Make it just a image (bmg version)   
            DestroyGroundComp(entity);
            DestroyMeshCollider(entity);
            FixRigidBody(entity);
            // Glue it to the Mini
            entity.SetParent(vehicle);
            entity.transform.localPosition = position;
            entity.Spawn();
        }
        void FixRigidBody(BaseEntity ent)
        {
            if (configData.miniDebug) Puts($"FRB Called {ent}");
            foreach (var rb in ent.GetComponentsInChildren<Rigidbody>())
            {
                if (rb != null)
                {
                    if (configData.miniDebug) Puts($"FRB Acti. {rb}");
                    rb.drag = 0f;
                    rb.useGravity = false;
                    rb.isKinematic = false;
                }
            }
        }
        void DestroyGroundComp(BaseEntity ent)
        {
            if (configData.miniDebug) Puts($"DGC Called {ent}");
            //No break if not grounded
            UnityEngine.Object.DestroyImmediate(ent.GetComponent<DestroyOnGroundMissing>());
            //No break if hits ground
            UnityEngine.Object.DestroyImmediate(ent.GetComponent<GroundWatch>());
            //Stops Decay
            UnityEngine.Object.DestroyImmediate(ent.GetComponent<DeployableDecay>());
        }
        void DestroyMeshCollider(BaseEntity ent)
        {
            if (configData.miniDebug) Puts($"DMC Called {ent}");
            //Makes it basically only visable since everything passes though it.
            foreach (var mesh in ent.GetComponentsInChildren<MeshCollider>())
            {
                if (configData.miniDebug) Puts($"DMC Acti {mesh}");
                UnityEngine.Object.DestroyImmediate(mesh);
                if (ent.PrefabName == _platePrefab)
                {
                    var boxCollider = ent.gameObject.AddComponent<BoxCollider>();
                    boxCollider.size = new Vector3(ent.bounds.size.x, ent.bounds.size.y, ent.bounds.size.z);
                }
                else return;
            }
            // boxCollider.gameObject.layer = (int)Layer.Reserved1;
            // boxCollider.isTrigger = true;
        }
    }
}