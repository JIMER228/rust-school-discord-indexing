using System;
using System.Collections.Generic;
using System.Linq;
using Facepunch;
using Rust;
using UnityEngine;
using Random = System.Random;

namespace Oxide.Plugins
{
    [Info("OreEvent", "OxideRussia", "0.0.1")]
    public class OreEvent : RustPlugin
    {
        public string SulfurOre = "assets/bundled/prefabs/autospawn/resource/ores/sulfur-ore.prefab";
        public string MetalOre = "assets/bundled/prefabs/autospawn/resource/ores/metal-ore.prefab";
        public string StonesOre = "assets/bundled/prefabs/autospawn/resource/ores/stone-ore.prefab";
        
        void OnServerInitialized()
        {
            timer.Every(7200f, () =>
            {
                SpawnOre();
            });
        }

        void Unload()
        {
            if (OreList.Count > 0)
            {
                foreach (var bases in OreList)
                {
                    if (bases != null && !bases.IsDestroyed)
                        bases.Kill();
                }
                OreList?.Clear();
            }
        }

        private string GetGridString(Vector3 position)
        {
            Vector2 adjPosition = new Vector2((World.Size / 2f) + position.x, (World.Size / 2f) - position.z);
            return $"{NumberToString((int)(adjPosition.x / 150f))}{(int)(adjPosition.y / 150f)}";
        }
        private string NumberToString(int number)
        {
            bool a = number > 25;
            Char c = (Char)(65 + (a ? number - 26 : number));
            return a ? "A" + c : c.ToString();
        }
        
        static readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);
        static double CurrentTime() { return DateTime.UtcNow.Subtract(epoch).TotalSeconds; }
        
        private double IsBlocked() 
        {
            var lefTime = SaveRestore.SaveCreatedTime.ToUniversalTime().Subtract(epoch).TotalSeconds + 8640000 - CurrentTime();
            return lefTime > 0 ? lefTime : 0;
        }

        static float GetGroundPosition(Vector3 pos)
        {
            float y = TerrainMeta.HeightMap.GetHeight(pos);
            RaycastHit hit;
            if (Physics.Raycast(new Vector3(pos.x, pos.y + 20f, pos.z), Vector3.down, out hit, Mathf.Infinity, LayerMask.GetMask(new[] {
                "Terrain", "World", "Default", "Construction", "Deployed"
            }
            )) && !hit.collider.name.Contains("rock_cliff")) return Mathf.Max(hit.point.y, y);
            return y;
        }

        static Vector3 RandomCircle(Vector3 center, float radius = 2)
        {
            float ang = UnityEngine.Random.value * 360;
            Vector3 pos = Vector3.zero;
            pos.x = center.x + radius * Mathf.Sin(ang * Mathf.Deg2Rad);
            pos.z = center.z + radius * Mathf.Cos(ang * Mathf.Deg2Rad);
            pos.y = TerrainMeta.HeightMap.GetHeight(pos);



            return pos;
        }
        
        HashSet<BaseEntity> OreList = new HashSet<BaseEntity>();
        [ChatCommand("cosmofarm")]
        void startcosmo(BasePlayer player)
        {
            if (!player.IsAdmin) return;
            var online = BasePlayer.activePlayerList.Count;
            if (online < 1)
            {
                player.ChatMessage("Ты лох");
                return;
            }
            SpawnOre();
        }
        void SpawnOre()
        {

            if (OreList.Count > 0)
            {
                foreach (var bases in OreList)
                {
                    if (bases != null && !bases.IsDestroyed)
                        bases.Kill();
                }
                OreList?.Clear();
            }
            var online = BasePlayer.activePlayerList.Count;
            if (online < 1) return;

            var monument = UnityEngine.Object.FindObjectsOfType<MonumentInfo>().FirstOrDefault(p => p.name.Contains("launch_site_1"));
            if (monument == null)
            {
                Puts("Air Field not found! Spawn Ore disabled!");
                return;
            }

            var pos = monument.transform.position + monument.transform.rotation * new Vector3(30f, 4f, 30f);

            for (int i = 0; i < 80; i++)
            {
                var entity = GameManager.server.CreateEntity(SulfurOre, RandomCircle(pos, UnityEngine.Random.Range(-10, 10)));
                entity.enableSaving = false;
                entity.Spawn();
                OreList.Add(entity);
            }
            for (int i = 0; i < 80; i++)
            {
                var entity = GameManager.server.CreateEntity(MetalOre, RandomCircle(pos, UnityEngine.Random.Range(-15, 15)));
                entity.enableSaving = false;

                entity.Spawn();
                OreList.Add(entity);
            }
            for (int i = 0; i < 100; i++)
            {
                var entity = GameManager.server.CreateEntity(StonesOre, RandomCircle(pos, UnityEngine.Random.Range(-17, 17)));
                entity.enableSaving = false;
                entity.Spawn();
                OreList.Add(entity);
            }
            Server.Broadcast("<color=#a5e664>ВНИМАНИЕ!!!</color>\nНа сервере запустился <color=#ffa500>ивент</color>, в центре космодрома куча камней, успейте собрать все до единого.");

            /*timer.In(7200, () =>
            {
                SpawnOre();
            });*/
        }
    }
}