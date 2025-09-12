using Oxide.Core.Plugins;
using Oxide.Core;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("GrazeZones", "YourName", "1.3.0")]
    [Description("Создает зону защиты, где постройки неуязвимы для любого урона.")]
    public class GrazeZones : RustPlugin
    {
        private const string GRAZE_PERMISSION = "grazezones.admin";
        private const string UI_PARENT = "GrazeProgressBarUI";
        private readonly List<GrazeZone> activeZones = new List<GrazeZone>();
        private readonly List<PlayerUI> activeUIs = new List<PlayerUI>();

        // Параметры зоны защиты
        private float ZoneRadius = 150f;  // Радиус зоны защиты 150 метров
        private float SphereRadius = 150f;  // Радиус сферы 150 метров
        private bool RestoreHealth = true;
        private bool LogAllDamage = false; // Отключение логов в консоль
        private string SphereColor = "0.5 0.0 1.0 0.04"; // Фиолетовый цвет сферы

        private class GrazeZone
        {
            public Vector3 Position;
            public BaseEntity Sphere;
            public float EndTime;
            public float Duration;
            public Timer Timer;
        }

        private class PlayerUI
        {
            public string PlayerId;
            public CuiElementContainer Container;
        }

        private void Init()
        {
            permission.RegisterPermission(GRAZE_PERMISSION, this);
            timer.Repeat(1f, 0, UpdatePlayerUI); // Запуск обновления UI
        }

        private void Unload()
        {
            foreach (var zone in activeZones.ToList())
            {
                DestroyGrazeSphere(zone);
            }
            activeZones.Clear();
            DestroyAllUI();
        }

        [ChatCommand("graze")]
        private void GrazeCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, GRAZE_PERMISSION))
            {
                SendReply(player, "<color=#FF5555>[!] GRAZE PROTECTION: У вас нет прав для использования этой команды!</color>");
                return;
            }

            if (args.Length < 1)
            {
                SendReply(player, "<color=#55FF55>[+] GRAZE PROTECTION: Использование:</color> <color=#FFFFFF>/graze start <секунды> | /graze stop</color>");
                return;
            }

            switch (args[0].ToLower())
            {
                case "start":
                    if (args.Length < 2 || !int.TryParse(args[1], out int seconds) || seconds <= 0)
                    {
                        SendReply(player, "<color=#FF5555>[!] GRAZE PROTECTION: Укажите время в секундах!</color> <color=#FFFFFF>Пример: /graze start 300</color>");
                        return;
                    }

                    var newZone = new GrazeZone
                    {
                        Position = player.transform.position,
                        EndTime = Time.realtimeSinceStartup + seconds,
                        Duration = seconds
                    };

                    CreateGrazeSphere(newZone);
                    activeZones.Add(newZone);
                    SendZoneMessage(newZone, "<color=#FF00FF>═════ GRAZE PROTECTION ACTIVE ═════</color>\n<color=#FFFFFF>✦ Зона защиты активирована на " + seconds + " секунд (радиус: " + ZoneRadius + " м)</color>");
                    SendReply(player, $"<color=#FF00FF>[+] GRAZE PROTECTION: Зона защиты активирована на {seconds} секунд (радиус: {ZoneRadius} м)!</color>");

                    newZone.Timer = timer.Once(seconds, () =>
                    {
                        DestroyGrazeSphere(newZone);
                        activeZones.Remove(newZone);
                        SendZoneMessage(newZone, "<color=#FF00FF>═════ GRAZE PROTECTION NO ACTIVE ═════</color>\n<color=#FFFFFF>✦ Зона защиты деактивирована</color>");
                        SendReply(player, "<color=#FF00FF>[!] GRAZE PROTECTION: Зона защиты истекла.</color>");
                        UpdatePlayerUI();
                    });

                    UpdatePlayerUI();
                    break;

                case "stop":
                    if (activeZones.Count == 0)
                    {
                        SendReply(player, "<color=#FF5555>[!] GRAZE PROTECTION: Нет активных зон защиты для остановки.</color>");
                        return;
                    }

                    // Найти ближайшую зону, в которой стоит игрок
                    GrazeZone closestZone = null;
                    float minDistance = float.MaxValue;
                    foreach (var zone in activeZones)
                    {
                        float distance = Vector3.Distance(player.transform.position, zone.Position);
                        if (distance <= ZoneRadius && distance < minDistance)
                        {
                            minDistance = distance;
                            closestZone = zone;
                        }
                    }

                    if (closestZone == null)
                    {
                        SendReply(player, "<color=#FF5555>[!] GRAZE PROTECTION: Вы не находитесь в зоне защиты!</color>");
                        return;
                    }

                    DestroyGrazeSphere(closestZone);
                    closestZone.Timer?.Destroy();
                    SendZoneMessage(closestZone, "<color=#FF00FF>═════ GRAZE PROTECTION NO ACTIVE ═════</color>\n<color=#FFFFFF>✦ Зона защиты остановлена вручную</color>");
                    activeZones.Remove(closestZone);
                    SendReply(player, "<color=#FF00FF>[+] GRAZE PROTECTION: Зона защиты остановлена вручную.</color>");
                    UpdatePlayerUI();
                    break;

                default:
                    SendReply(player, "<color=#55FF55>[+] GRAZE PROTECTION: Использование:</color> <color=#FFFFFF>/graze start <секунды> | /graze stop</color>");
                    break;
            }
        }

        private void CreateGrazeSphere(GrazeZone zone)
        {
            DestroyGrazeSphere(zone);

            zone.Sphere = GameManager.server.CreateEntity("assets/bundled/prefabs/modding/events/twitch/br_sphere_purple.prefab", zone.Position) as BaseEntity;
            if (zone.Sphere == null)
            {
                return;
            }

            var sphereEntity = zone.Sphere as SphereEntity;
            if (sphereEntity != null)
            {
                sphereEntity.currentRadius = SphereRadius * 2;
                sphereEntity.lerpSpeed = 0f;

                var renderer = zone.Sphere.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    var colorValues = SphereColor.Split(' ').Select(float.Parse).ToArray();
                    var material = new Material(Shader.Find("Hidden/Internal-Colored"))
                    {
                        color = new Color(colorValues[0], colorValues[1], colorValues[2], colorValues[3])
                    };
                    material.SetInt("_SrcBlend", 5);
                    material.SetInt("_DstBlend", 10);
                    material.SetInt("_Cull", 0);
                    material.SetInt("_ZWrite", 0);
                    renderer.material = material;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }

                var collider = zone.Sphere.GetComponent<SphereCollider>() ?? zone.Sphere.gameObject.AddComponent<SphereCollider>();
                collider.radius = SphereRadius;
                collider.isTrigger = true;

                zone.Sphere.gameObject.layer = LayerMask.NameToLayer("Transparent");
                zone.Sphere.Spawn();
            }
        }

        private void DestroyGrazeSphere(GrazeZone zone)
        {
            if (zone.Sphere != null)
            {
                zone.Sphere.Kill();
                zone.Sphere = null;
            }
        }

        private void SendZoneMessage(GrazeZone zone, string message)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (Vector3.Distance(player.transform.position, zone.Position) <= ZoneRadius)
                {
                    SendReply(player, message);
                }
            }
        }

        private void UpdatePlayerUI()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (IsInGrazeZone(player.transform.position))
                {
                    ShowUI(player);
                }
                else
                {
                    DestroyUI(player);
                }
            }
        }

        private void ShowUI(BasePlayer player)
        {
            DestroyUI(player);

            // Найти ближайшую зону
            GrazeZone closestZone = null;
            float minDistance = float.MaxValue;
            foreach (var zone in activeZones)
            {
                float distance = Vector3.Distance(player.transform.position, zone.Position);
                if (distance <= ZoneRadius && distance < minDistance)
                {
                    minDistance = distance;
                    closestZone = zone;
                }
            }

            if (closestZone == null) return;

            float remainingTime = closestZone.EndTime - Time.realtimeSinceStartup;
            if (remainingTime < 0) remainingTime = 0;

            float progress = remainingTime / closestZone.Duration;
            int minutes = Mathf.FloorToInt(remainingTime / 60);
            int seconds = Mathf.FloorToInt(remainingTime % 60);
            string timerText = $"{minutes:00}:{seconds:00}";

            var elements = new CuiElementContainer();

            elements.Add(new CuiPanel
            {
                Image = { Color = "0.2 0.2 0.2 0.8" },
                RectTransform = { AnchorMin = "0.3 0.9", AnchorMax = "0.7 0.92" },
                CursorEnabled = false
            }, "Overlay", UI_PARENT);

            elements.Add(new CuiPanel
            {
                Image = { Color = "0.1 0.1 0.1 0.8" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, UI_PARENT);

            elements.Add(new CuiPanel
            {
                Image = { Color = "0.5 0.0 1.0 0.9" }, // Фиолетовый цвет прогресса
                RectTransform = { AnchorMin = "0 0", AnchorMax = $"{Mathf.Clamp01(progress)} 1" }
            }, UI_PARENT);

            elements.Add(new CuiLabel
            {
                Text = { Text = $"GRAZE PROTECTION: {timerText}", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, UI_PARENT);

            CuiHelper.AddUi(player, elements);
            activeUIs.Add(new PlayerUI { PlayerId = player.UserIDString, Container = elements });
        }

        private void DestroyUI(BasePlayer player)
        {
            var ui = activeUIs.Find(u => u.PlayerId == player.UserIDString);
            if (ui != null)
            {
                CuiHelper.DestroyUi(player, UI_PARENT);
                activeUIs.Remove(ui);
            }
        }

        private void DestroyAllUI()
        {
            foreach (var ui in new List<PlayerUI>(activeUIs))
            {
                var player = BasePlayer.FindByID(ulong.Parse(ui.PlayerId));
                if (player != null)
                {
                    CuiHelper.DestroyUi(player, UI_PARENT);
                }
                activeUIs.Remove(ui);
            }
        }

        private bool IsStructure(BaseEntity entity)
        {
            return entity is BuildingBlock || entity is DecayEntity;
        }

        private bool IsInGrazeZone(Vector3 position)
        {
            return activeZones.Any(zone => Vector3.Distance(position, zone.Position) <= ZoneRadius);
        }

        private void BlockDamage(BaseCombatEntity entity, HitInfo info, string hookName)
        {
            if (entity == null || info == null) return;

            var attacker = info.Initiator as BasePlayer;
            if (attacker != null && permission.UserHasPermission(attacker.UserIDString, GRAZE_PERMISSION))
            {
                return;
            }

            // Обнуляем весь урон
            if (info.damageTypes != null)
            {
                info.damageTypes.ScaleAll(0f);
                for (int i = 0; i < info.damageTypes.types.Length; i++)
                {
                    info.damageTypes.types[i] = 0f;
                }
            }

            // Восстанавливаем здоровье
            if (RestoreHealth)
            {
                entity.health = entity.MaxHealth();
            }
        }

        [HookMethod("OnEntityTakeDamage")]
        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || !IsStructure(entity))
            {
                return null;
            }

            if (IsInGrazeZone(entity.transform.position))
            {
                BlockDamage(entity, info, "OnEntityTakeDamage");
                return true;
            }

            return null;
        }

        [HookMethod("OnStructureAttack")]
        private object OnStructureAttack(BaseCombatEntity entity, BasePlayer player, HitInfo info)
        {
            if (entity == null || !IsStructure(entity))
            {
                return null;
            }

            if (IsInGrazeZone(entity.transform.position))
            {
                BlockDamage(entity, info, "OnStructureAttack");
                return true;
            }

            return null;
        }

        [HookMethod("OnExplosiveDamage")]
        private object OnExplosiveDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || !IsStructure(entity))
            {
                return null;
            }

            if (IsInGrazeZone(entity.transform.position))
            {
                BlockDamage(entity, info, "OnExplosiveDamage");
                return true;
            }

            return null;
        }
    }
}