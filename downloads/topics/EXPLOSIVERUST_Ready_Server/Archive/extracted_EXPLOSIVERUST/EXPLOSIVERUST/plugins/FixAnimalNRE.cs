using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("FixAnimalNRE", "TopPlugin.ru", "1.0.3")]
    internal class FixAnimalNRE : RustPlugin
    {

        private void OnServerInitialized()
        {
            PrintWarning("\n-----------------------------\n" +
            "     Author - https://topplugin.ru/\n" +
            "     VK - https://vk.com/rustnastroika\n" +
            "     Discord - https://discord.com/invite/5DPTsRmd3G\n" +
            "-----------------------------");
            foreach (var npc in BaseNetworkable.serverEntities.OfType<BaseAnimalNPC>().Where(x => x != null).ToList())
                OnEntitySpawned(npc);



            //foreach (var npc in BaseNetworkable.serverEntities.OfType<BaseEntity>().Where(x => x != null).ToList())
            //{
            //    if (npc.parentEntity.uid == 1716549 || npc.parentEntity.uid == 1716537)
            //    {
            //        Puts(npc.ShortPrefabName + $" {npc.transform.position}");

            //        npc.Kill();
            //    }
            //}
        }

        private void OnEntitySpawned(BaseAnimalNPC npc)
        {
            if (npc == null || npc.IsDestroyed) return;
            AnimalBaseNavigatorFix.Get(npc);
            var value = npc.GetComponent<BaseAIBrain<BaseAnimalNPC>>();
            if (value == null)
            {
                var _animalremovalQueue=  AIThinkManager._animalremovalQueue;
                if (!_animalremovalQueue.Contains(npc))
                {
                    _animalremovalQueue.Add(npc);
                    Puts($"Исправлен багованный npc '{npc.ShortPrefabName}' в точке {npc.transform.position}");
                }
            }
        }

        class AnimalBaseNavigatorFix : MonoBehaviour
        {
            private BaseAnimalNPC animal;
            private BaseNavigator baseNavigator;

            public static AnimalBaseNavigatorFix Get(BaseAnimalNPC animal)
            {
                return animal.GetComponent<AnimalBaseNavigatorFix>() ?? animal.gameObject.AddComponent<AnimalBaseNavigatorFix>();
            }

            void Awake()
            {
                animal = gameObject.GetComponent<BaseAnimalNPC>();
                baseNavigator = gameObject.GetComponent<BaseNavigator>();
                if (baseNavigator.CanUseNavMesh)
                    InvokeRepeating("CheckDestination", 0.1f, 0.1f);
                else
                    Destroy(this);
            }

            void CheckDestination()
            {
                Vector3 vector;
                float maxRange = baseNavigator.IsSwimming() ? 30f : 6f;
                if (!baseNavigator.GetNearestNavmeshPosition(animal.transform.position, out vector, maxRange))
                {
                    baseNavigator.CanUseNavMesh = false;
                    if (animal.transform.position.ToString() != GetGroundPosition(animal.transform.position).ToString())
                        animal.transform.position = GetGroundPosition(animal.transform.position);
                    Destroy(this);
                    Debug.Log($"[FixAnimalNRE] Animal disabled use NavMesh {animal.ShortPrefabName}, position {animal.transform.position}, {vector}");
                }
            }


            Vector3 GetGroundPosition(Vector3 pos)
            {
                float y = TerrainMeta.HeightMap.GetHeight(pos);
                RaycastHit hit;
                if (Physics.Raycast(new Vector3(pos.x, pos.y + 20f, pos.z), Vector3.down, out hit, Mathf.Infinity,
                    LayerMask.GetMask(new[] { "Terrain", "World", "Default", "Construction", "Deployed" })) && !hit.collider.name.Contains("rock_cliff")) y = Mathf.Max(hit.point.y, y);
                pos.y = y;
                return pos;
            }

            void Update()
            {
               
            }
        }

        void Unload()
        {
            foreach (var npc in BaseNetworkable.serverEntities.OfType<BaseAnimalNPC>().Where(x => x != null && x.GetComponent<AnimalBaseNavigatorFix>() != null).ToList())
            {
                UnityEngine.Component.DestroyImmediate(npc.GetComponent<AnimalBaseNavigatorFix>());
            }
        }




    }
}