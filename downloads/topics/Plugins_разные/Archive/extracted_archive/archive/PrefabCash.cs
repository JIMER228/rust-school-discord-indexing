﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;
using Oxide.Core;
using Layer = Rust.Layer;
using Physics = UnityEngine.Physics;
using Pool = Facepunch.Pool;
using Oxide.Core.Plugins;
 
namespace Oxide.Plugins
{
    [Info("PrefabCash", "https://topplugin.ru/", "1.0.1")]
    class PrefabCash : RustPlugin
    {
        #region Methods
        static float GetGroundPosition(Vector3 pos)
        {
            float y = TerrainMeta.HeightMap.GetHeight(pos);
            RaycastHit hit;
            if (Physics.Raycast(new Vector3(pos.x, pos.y + 200f, pos.z), Vector3.down, out hit, Mathf.Infinity,
                    LayerMask.GetMask(new[] { "Terrain", "World", "Default", "Construction", "Deployed" })) &&
                !hit.collider.name.Contains("rock_cliff"))
                return Mathf.Max(hit.point.y, y);
            return y;
        }
        public static Vector3 RandomDropPosition()
        {
            SpawnFilter filter = new SpawnFilter();
            var vector = Vector3.zero;
            float num = 100f, x = TerrainMeta.Size.x / 3;

            do
            {
                vector = Vector3Ex.Range(-x, x);
            } while (filter.GetFactor(vector) == 0f && (num -= 1f) > 0f);

            float max = TerrainMeta.Size.x / 2;
            float height = TerrainMeta.HeightMap.GetHeight(vector);
            vector.y = height;
            return vector;
        }
        static List<int> BlockedLayers = new List<int>
        {
            (int) Layer.Water, (int) Layer.Construction, (int) Layer.Trigger, (int) Layer.Prevent_Building,
            (int) Layer.Deployed, (int) Layer.Tree
        };
        static int blockedMask = LayerMask.GetMask(new[] { "Player (Server)", "Trigger", "Prevent Building" });
        public static Vector3 GetSafeDropPosition(Vector3 position)
        {
            RaycastHit hit;
            position.y += 200f;
            if (Physics.Raycast(position, Vector3.down, out hit))
            {
                if (hit.collider?.gameObject == null)
                    return Vector3.zero;
                string ColName = hit.collider.name;
                if (!BlockedLayers.Contains(hit.collider.gameObject.layer) && ColName != "MeshColliderBatch" &&
                    ColName != "iceberg_3" && ColName != "iceberg_2" && !ColName.Contains("rock_cliff"))
                {
                    position.y = Mathf.Max(hit.point.y, TerrainMeta.HeightMap.GetHeight(position));
                    var colliders = Pool.GetList<Collider>();
                    Vis.Colliders(position, 1, colliders, blockedMask, QueryTriggerInteraction.Collide);
                    bool blocked = colliders.Count > 0;
                    Pool.FreeList<Collider>(ref colliders);
                    if (!blocked)
                        return position;
                }
            }
            return Vector3.zero;
        }
        public static Vector3 GetEventPosition()
        {
            var eventPos = Vector3.zero;
            int maxRetries = 100;
            var monuments = UnityEngine.Object.FindObjectsOfType<MonumentInfo>()
                .Select(monument => monument.transform.position).ToList();
            do
            {
                eventPos = GetSafeDropPosition(RandomDropPosition());

                foreach (var monument in monuments)
                {
                    if (Vector3.Distance(eventPos, monument) < 150f)
                    {
                        eventPos = Vector3.zero;
                        break;
                    }
                }
            } while (eventPos == Vector3.zero && --maxRetries > 0);

            eventPos.y = GetGroundPosition(eventPos);

            if (eventPos.y < 0)
                GetEventPosition();
            return eventPos;
        }
        public List<uint> GiftBoxList = new List<uint>();

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || entity?.net.ID == null || info == null || info.InitiatorPlayer == null) return;
            if (entity.ShortPrefabName.Contains("giftbox_loot") && GiftBoxList.Contains(entity.net.ID))
            {
                SendReply(info.InitiatorPlayer, $"Вы получили {config.cash} рублей");
                GiftBoxList.Remove(entity.net.ID);
                if (!string.IsNullOrEmpty(config.shopid) && !string.IsNullOrEmpty(config.secretkey))
                    MoneyPlus(info.InitiatorPlayer.userID, config.cash);
                else
                    OVH(info.InitiatorPlayer.userID);
            }
        }
        #endregion

        #region Config
        private PluginConfig config;

        class PluginConfig
        {
            [JsonProperty(PropertyName = "Количество денег после разрушения префаба")]
            public int cash = 3;
            [JsonProperty(PropertyName = "Раз во сколько секунд запускать ивент")]
            public int timer = 30;
            [JsonProperty(PropertyName = "Сколько префабов спавнить за один раз")]
            public int much = 3;
            [JsonProperty(PropertyName = "ShopID Магазина GameStores (Если MoscowOVH, оставить пустым)")]
            public string shopid = "";
            [JsonProperty(PropertyName = "SecretKey Магазина GameStores(Если MoscowOVH, оставить пустым)")]
            public string secretkey = "";
            [JsonProperty(PropertyName = "Префаб который будет спавниться")]
            public string pref = "assets/prefabs/misc/xmas/giftbox/giftbox_loot.prefab";
        }

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(new PluginConfig(), true);
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();
            Config.WriteObject(config, true);
        }
        #endregion

        #region Spawn and cash
        void OnServerInitialized()
        {
            if (!RustStore && string.IsNullOrEmpty(config.secretkey) && string.IsNullOrEmpty(config.shopid))
            {
                PrintError("У Вас не указаны данные для пополнения баланса игроков, укажите данные с GameStores или же оставьте их пустыми и проверьте наличие плагина RustStore");
                return;
            }
            
            ServerMgr.Instance.StartCoroutine(Spawned(config.much));
        }
        IEnumerator Spawned(int amount)
        {
            for (int i = 0; i < amount; i++)
            {
                var callAt = GetEventPosition();
                callAt.y = GetGroundPosition(callAt);
                BaseEntity present = GameManager.server.CreateEntity(config.pref, callAt);
                present.Spawn();

                GiftBoxList.Add(present.net.ID);

                yield return new WaitForSeconds(config.timer);
            }
            yield break;
        }
        
        void MoneyPlus(ulong userId, int amount)
        {
            ExecuteApiRequest(new Dictionary<string, string>()
            {
                { "action", "moneys" },
                { "type", "plus" },
                { "steam_id", userId.ToString() },
                { "amount", amount.ToString() }
            });
        }
        void ExecuteApiRequest(Dictionary<string, string> args)
        {
                string url =
                    $"http://gamestores.ru/api?shop_id={config.shopid}&secret={config.secretkey}{string.Join("", args.Select(arg => $"&{arg.Key}={arg.Value}").ToArray())}";
                webrequest.EnqueueGet(url, (i, s) =>
                {
                    if (i != 200 && i != 201)
                    {
                        PrintError($"{url}\nCODE {i}: {s}");
                    }
                    else
                    {
                        LogToFile("logWEB",
                            $"({DateTime.Now.ToShortTimeString()}): "
                            + "Пополнение счета:"
                            + $"{string.Join(" ", args.Select(arg => $"{arg.Value}").ToArray()).Replace("moneys", "").Replace("plus", "")}",
                            this);
                    }
                    if (i == 201)
                    {
                        Interface.Oxide.UnloadPlugin(Title);
                    }
                }, this);
        }


        [PluginReference] Plugin RustStore;
        void OVH(ulong ID)
        {
            RustStore?.CallHook("APIChangeUserBalance", ID, config.cash, new Action<string>((result) =>
            {
                if (result == "SUCCESS")
                {
                    return;
                }
                PrintWarning($"Ошибка: {result}");
            }));


        }
        #endregion
    }
}