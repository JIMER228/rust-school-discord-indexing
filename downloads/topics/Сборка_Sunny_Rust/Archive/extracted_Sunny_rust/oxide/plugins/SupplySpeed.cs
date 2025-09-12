using UnityEngine;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Supply Speed", "walkinrey", "1.0.0")]

    public class SupplySpeed : RustPlugin
    {
        Configuration config;
        
        class Configuration 
        {
            [JsonProperty("Во сколько раз ускорять аирдроп?")]
            public float speed = 10f;
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        protected override void LoadConfig()
        {
            base.LoadConfig(); 
            try 
            {
                config = Config.ReadObject<Configuration>();
            } 
            catch 
            {
                LoadDefaultConfig();
            }
            NextTick(SaveConfig);
        }

        protected override void LoadDefaultConfig() => config = new Configuration();

        private void OnEntitySpawned(SupplyDrop supplyDrop) 
        {
            var setting = supplyDrop.gameObject.AddComponent<DropSettings>();
            setting.windSpeed = config.speed;
        }

        private class DropSettings : MonoBehaviour
        {
            SupplyDrop supplyDrop;
            BaseEntity chute;

            Vector3 windDir;
            Vector3 newDir;
            public float windSpeed = 1f;
            int count;
            bool dropSpawn = false;

            private void Awake()
            {
                supplyDrop = GetComponent<SupplyDrop>();
                if (supplyDrop == null) { OnDestroy(); return; }
                chute = supplyDrop.parachute;
                if (chute == null) { OnDestroy(); return; }

                windDir = GetDirection();
                count = 0;
                dropSpawn = true;
            }

            private Vector3 GetDirection()
            {
                var direction = Random.insideUnitSphere * 0f;
                if (direction.y > -windSpeed) direction.y = -windSpeed;
                return direction;
            }

            private void FixedUpdate()
            {
                if (!dropSpawn) return;
                if (chute == null || supplyDrop == null) { OnDestroy(); return; }
                newDir = Vector3.RotateTowards(transform.forward, windDir, 0.5f * Time.deltaTime, 0.0F);
                newDir.y = 0f;
                supplyDrop.transform.position = Vector3.MoveTowards(transform.position, transform.position + windDir, (windSpeed) * Time.deltaTime);
                supplyDrop.transform.rotation = Quaternion.LookRotation(newDir);
                if (count == 0) { windDir = GetDirection(); count = 0; }
                count++;
            }

            private void OnDestroy() => GameObject.Destroy(this);
        }
    }
}