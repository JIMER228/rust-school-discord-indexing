using System;
using System.Collections.Generic;
using Oxide.Core;
using UnityEngine;
using Oxide.Core.Plugins;
using Physics = UnityEngine.Physics;

namespace Oxide.Plugins
{
    [Info("Portable Recycler", "", "3.0.0")]
	[Description("Позволяет выдавать карманные переработчики как на Magic Rust")]
    class PortableRecycler : RustPlugin
    {   
	    [PluginReference]
        Plugin ImageLibrary;
		
		private Dictionary<uint, uint> Recyclers = new Dictionary<uint, uint>();

	    private bool pAvailable, pPrivelege, pOwner, pRadtown, dGround, dStructure;

        #region Oxide Hooks
        string szIcon;
        ulong uIcon = 753314436643165431;
        void OnServerInitialized()
        {
            bool exists = (bool)ImageLibrary.Call("HasImage", "RecIcon", uIcon);
            if (exists == false)
            {
                ImageLibrary.Call("AddImage", "https://i.imgur.com/lu1L4Tr.png", "RecIcon", uIcon);

            }
        }
/*
        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            var ent = entity as Recycler;
            if (ent == entity)
            {
                ent.;
                Puts(ent.health.ToString());
            }
            return null;
        }*/

        void Init()
	    {
		    Recyclers = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<uint, uint>>("PortableRecyclers");
		    LoadDefaultConfig();
		    lang.RegisterMessages(Messages, this);
	    }
	    
		void OnEntityBuilt(Planner planner, GameObject gameobject)
        {
	        BaseEntity entity = gameobject.ToBaseEntity(); 
	        BasePlayer player = planner.GetOwnerPlayer();
	        if(player == null || entity == null) return;
	        
	        if (entity.skinID != skin) return;   
	        
	        NextTick(() => entity.Kill());

	        Vector3 ePos = entity.transform.position;

	        BaseEntity Recycler = GameManager.server.CreateEntity("assets/bundled/prefabs/static/recycler_static.prefab", ePos, entity.transform.rotation, true);
	        
	        RaycastHit rHit;
	        
	        if (Physics.Raycast(new Vector3(ePos.x,ePos.y+1, ePos.z), Vector3.down, out rHit, 2f, LayerMask.GetMask(new string[] {"Construction"})) && rHit.GetEntity() != null)
	        {
		        if (!dStructure)
		        {
			        player.ChatMessage(lang.GetMessage("D.STRUCTURE", this));
			        GiveRecycler(player);
			        Recycler.Kill();
			        return;
		        }
		        
			    entity = rHit.GetEntity();

		        if(!Recyclers.ContainsKey(entity.net.ID)) Recyclers.Add(entity.net.ID, 0);
	        }
	        else
	        {
		        if (!dGround)
		        {
			        player.ChatMessage(lang.GetMessage("D.GROUND", this));
			        GiveRecycler(player);
			        Recycler.Kill();
			        return;
		        }
	        }
	        
	        
	        
	        Recycler.OwnerID = player.userID;
	        Recycler.Spawn();
	        
	        if(entity is BuildingBlock) Recyclers[entity.net.ID] = Recycler.net.ID; 
	        Interface.Oxide.DataFileSystem.WriteObject("PortableRecyclers", Recyclers);
        }

	    void OnHammerHit(BasePlayer player, HitInfo info)
	    {
		    BaseEntity entity = info.HitEntity;
		    if(entity == null) return;
		    
		    if(!entity.ShortPrefabName.Contains("recycler")) return;

		    if (!pAvailable)
		    {
			    player.ChatMessage(lang.GetMessage("P.AVAILABLE", this));
			    return; 
		    }

		    if (!pRadtown && entity.OwnerID == 0)
		    {
			    player.ChatMessage(lang.GetMessage("P.RADTOWN", this));
			    return;
		    }

		    if (pPrivelege && !player.CanBuild())
		    {
			    player.ChatMessage(lang.GetMessage("P.PRIVELEGE", this));
			    return;
		    }

		    if (pOwner && entity.OwnerID != player.userID && entity.OwnerID != 0)
		    {
			    player.ChatMessage(lang.GetMessage("P.OWNER", this));
			    return;
		    }

		    entity.Kill();
		    GiveRecycler(player);
		    player.ChatMessage(lang.GetMessage("R.PICKUP", this));
	    }	    
	    
	    void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
	    {
		    if (info == null || entity == null) return;
		    
		    if (entity is BuildingBlock && Recyclers.ContainsKey(entity.net.ID)) DestroyRecycler(entity);
	    }
	    
	    void OnEntityKill(BaseNetworkable entity)
	    {
		    if(entity == null) return;
		    
		    if (entity is BuildingBlock && Recyclers.ContainsKey(entity.net.ID)) DestroyRecycler(entity);
	    }
	    
	    object CanStackItem(Item item, Item targetItem)
	    {
		    if (item.info.shortname.Contains("bench"))
			    if(item.skin != 0 || targetItem.skin != 0)
				    return false;

		    return null;
	    }
	    
	    object CanCombineDroppedItem(DroppedItem item, DroppedItem targetItem)
	    {
		    if (item.item.info.shortname.Contains("bench"))
			    if(item.skinID != 0 || targetItem.skinID != 0)
				    return false;
		    
		    return null;
	    }

	    #endregion
	    
	    [ConsoleCommand("recycler.add")]
	    private void AddRecycler(ConsoleSystem.Arg arg)
	    {
		    if (!arg.IsAdmin)
		    {
			    SendError(arg, "[Ошибка] У вас нет доступа к этой команде!");
			    return;
		    }

		    if (!arg.HasArgs())
		    {
			    PrintError(":\n[Ошибка] Введите recycler.add steamid/nickname\n[Пример] recyler.add Имя\n[Пример] recyler.add 76561198311240000");
			    return;
		    }
		    
		    BasePlayer player = BasePlayer.Find(arg.Args[0]);
		    if (player == null)
		    {
			    PrintError($"[Ошибка] Не удается найти игрока {arg.Args[0]}");
			    return;
		    }
		    
		    GiveRecycler(player);
	    }

	    private void GiveRecycler(BasePlayer player)
	    {
            Item rec = ItemManager.CreateByName("box.repair.bench", 1, skin);
		    rec.MoveToContainer(player.inventory.containerMain);
	    }

	    private void DestroyRecycler(BaseNetworkable entity)
	    {
		    BaseNetworkable rEntity = BaseNetworkable.serverEntities.Find(Recyclers[entity.net.ID]);
		    if(rEntity != null) rEntity.Kill();    
		    Recyclers.Remove(entity.net.ID);
		    Interface.Oxide.DataFileSystem.WriteObject("PortableRecyclers", Recyclers);
	    }
	    
	    #region Language and Config

	    Dictionary<string,string> Messages = new Dictionary<string, string>()
	    {
		    // Pickup
		    ["P.PRIVELEGE"] = "Вам нужно право на строительство чтобы подобрать переработчик",
		    ["P.AVAILABLE"] = "Подбор переработчиков выключен",
		    ["P.RADTOWN"] = "Подбор переработчиков с радтауна запрещен",
		    ["P.OWNER"] = "Переработчики может подобрать только их владельцы",
		    // Deploy
		    ["D.GROUND"] = "Переработчики нельзя ставить на землю",
		    ["D.STRUCTURE"] = "Переработчики нельзя ставить на строения (фундамент, потолки и тд)",
		    // Other
		    ["R.GIVED"] = "Вам был успешно выдан переработчик",
		    ["R.PICKUP"] = "Вы успешно подобрали переработчик"
	    };

	    protected override void LoadDefaultConfig()
	    {
		    // Pickup
		    Config["Можно ли подбирать переработчик"] = pAvailable = GetConfig("Можно ли подбирать переработчик", false);
		    Config["Подбор только владельцем"] = pOwner = GetConfig("Подбор только владельцем", false);
		    Config["Право на постройку для подбора"] = pPrivelege = GetConfig("Право на постройку для подбора", false);
		    Config["Подбор переработчиков с РТ"] = pRadtown = GetConfig("Подбор переработчиков с РТ", false);
		    // Deploy
		    Config["Установка на землю"] = dGround = GetConfig("Установка на землю", false);
		    Config["Установка на строения"] = dStructure = GetConfig("Установка на строения", false);
		    // Other
		    // Config["Логи выдачи"] = lGive = GetConfig("Логи выдачи", true);
		    // Config["Логи поднятий"] = lPickup = GetConfig("Логи поднятий", true);
		    // Config["Логи установки"] = lDeploy = GetConfig("Логи установки", true);
		    SaveConfig();
	    }

	    T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)Convert.ChangeType(Config[name], typeof(T));

	    private const ulong skin = 1626502596;
	    
	    #endregion
    }
}