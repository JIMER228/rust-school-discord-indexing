using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins;

[Info("UltraLauncher", "TEGIR", "1.0.0")]
public class UltraLauncher : RustPlugin
{
    #region Configuration

    private static Configuration _config = new Configuration();
    public class Configuration
    {
        [JsonProperty("Колличество доп ракет которые могут быть заряжены в лаунчер")]
        public int RocketsCount { get; set; } = 4;

        [JsonProperty("Время (секунды) интервала между выпуском ракет из 1 лаунчера")]
        public float TimeInterval { get; set; } = 0.2f;

        [JsonProperty(
            "Может ли человек без пермишна использовать заряженную ракетницу? (иначе будут разряжаться ракеты при выстреле)")]
        public bool CanShootWhitoutPermission { get; set; } = false;


        [JsonProperty("Блеклист ракет для мультизапуска")]
        public List<string> Blacklist  = new List<string>();


        public static Configuration GetNewConfiguration()
        {
            return new Configuration
            {
                Blacklist = new List<string>{"ammo.rocket.fire"}
            };
        }
    }
    protected override void LoadConfig()
    {
        base.LoadConfig();
        try
        {
            _config = Config.ReadObject<Configuration>();
            if (_config == null) LoadDefaultConfig();
        }
        catch
        {
            Puts("!!!!ОШИБКА КОНФИГУРАЦИИ!!!! создаем новую");
            LoadDefaultConfig();
        }

        NextTick(SaveConfig);
    }
    protected override void LoadDefaultConfig() => _config = Configuration.GetNewConfiguration();
    protected override void SaveConfig() => Config.WriteObject(_config);

    #endregion
    void OnServerInitialized()
    {
        permission.RegisterPermission("UltraLauncher.use", this);
    }
   
    
    
    void OnRocketLaunched(BasePlayer player, BaseEntity entity)
    {
        if(entity.ShortPrefabName != "rocket_basic") return;
        BaseProjectile proj =  player.GetHeldEntity() as BaseProjectile;
        if (proj != null && proj.primaryMagazine.contents > 0)
        {
            if (!_config.CanShootWhitoutPermission)
            {
                if (!permission.UserHasPermission(player.UserIDString, "UltraLauncher.use"))
                {
                    Item itemtodrop = ItemManager.CreateByItemID(proj.primaryMagazine.ammoType.itemid,
                        proj.primaryMagazine.contents);
                    itemtodrop.Drop(player.transform.position, player.eyes.HeadForward().normalized * 1,
                        player.eyes.rotation);
                    player.ChatMessage(
                        "Упс.. Дополнительные ракеты для запуска выпали из ракетницы потому что вы не можете пользоваться мультизапуском ракет.");
                    proj.primaryMagazine.contents = 0;
                    return;
                }
                
            }
            
            ServerMgr.Instance.StartCoroutine(SendRockets(player, proj.primaryMagazine.contents, GetPrefabRocket(proj.primaryMagazine.ammoType.shortname)));
            proj.primaryMagazine.contents = 0;
            proj.SendNetworkUpdate();
        }

    }

    string GetPrefabRocket(string shortname)
    {
        switch (shortname)
        {
            case "ammo.rocket.hv":
                return "assets/prefabs/ammo/rocket/rocket_hv.prefab|35|0.01";
            case "ammo.rocket.basic":
                return "assets/prefabs/ammo/rocket/rocket_basic.prefab|17|0.2";
            case "ammo.rocket.fire":
                return "assets/prefabs/ammo/rocket/rocket_fire.prefab|17|0.2";
            default:
                return "assets/prefabs/ammo/rocket/rocket_basic.prefab|17|0.2";
        }
    }

    IEnumerator SendRockets(BasePlayer player, int rockets, string prefab)
    {
        yield return new WaitForSeconds(_config.TimeInterval);
        for (int i = 0; i < rockets; i++)
        {
            if(!player.IsAlive()) yield break;
            Effect x = new Effect("assets/prefabs/weapons/rocketlauncher/effects/attack.prefab", player, 0, new Vector3(), new Vector3()); 
            EffectNetwork.Send(x, player.Connection);
            var prop = GameManager.server.CreateEntity(prefab.Split("|")[0], player.eyes.position + player.eyes.BodyForward().normalized, player.eyes.rotation);
            ServerProjectile sProjectile = prop.GetComponent<ServerProjectile>();
            sProjectile.InitializeVelocity(player.eyes.HeadForward().normalized * int.Parse(prefab.Split("|")[1]));
            sProjectile.gravityModifier = float.Parse(prefab.Split("|")[2]);
            prop.OwnerID = player.userID;
            prop.Spawn();
            yield return new WaitForSeconds(_config.TimeInterval);
        }
        yield break;
    }
    
     object OnWeaponReload(BaseProjectile weapon, BasePlayer player)
     {
         if (weapon.ShortPrefabName != "rocket_launcher.entity") return null;
         if (permission.UserHasPermission(player.UserIDString, "UltraLauncher.use"))
         {
             if (_config.Blacklist.Contains(weapon.primaryMagazine.ammoType.shortname))
             {
                 weapon.primaryMagazine.capacity = 1;
                 return null;
             }
             weapon.primaryMagazine.capacity = _config.RocketsCount + 1;
         }
         else
         {
             weapon.primaryMagazine.capacity = 1;
         }
         return null;
     }
    
    

   

    

    
}