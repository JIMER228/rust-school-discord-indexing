using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using ProtoBuf;

namespace Oxide.Plugins
{
    [Info("AntiRaid", "walkinrey", "1.0.5")]
    class AntiRaid : RustPlugin
    {
        #region Variables
        Configuration config;
        BuildingPrivlidge privilege;
        IPlayer playerOwner;
        BasePlayer initiator;
        #endregion
        #region Config
        class Configuration
        {
            [JsonProperty("Сообщение если у игрока есть антирейд")] public string msgAntiRaid = "Ты Ахуел это Админ нельзя Рейдить.";
            [JsonProperty("Shortname дополнительных объектов, чтобы на них действовал антирейд")] public string[] shortnames = {"wall.frame.cell.gate", "wall.frame.cell", "wall.window.glass.reinforced", "wall.window.bars.toptier", "floor.grill", "floor.triangle.grill", "gates.external.high.stone", "wall.external.high.stone", "gates.external.high.wood", "wall.external.high"};
        }
        protected override void LoadConfig()
		{
			base.LoadConfig();
			try
			{
				config = Config.ReadObject<Configuration>();
				if(config == null) LoadDefaultConfig();
			}
			catch
			{
				LoadDefaultConfig();
			}
			Config.WriteObject(config, true);
        }
        protected override void LoadDefaultConfig() => config = new Configuration();
        #endregion
        #region Hooks
        private void Init() => permission.RegisterPermission("antiraid.use", this);
        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if(entity == null || info == null) {SetNull(); return null;}
            if(entity.OwnerID.IsSteamId()) playerOwner = covalence.Players.FindPlayerById(entity.OwnerID.ToString());
            else {SetNull(); return null;}
            if(info != null) initiator = info?.InitiatorPlayer;
            if(initiator == null) {SetNull(); return null;}
            if(entity is BuildingBlock || entity is Door || entity is Construction || entity is Item || config.shortnames.Contains(entity.ShortPrefabName.ToString()))
            {
                if(playerOwner == null) {SetNull(); return null;}
                if(playerOwner.Id.ToString() != info.InitiatorPlayer.userID.ToString())
                {
                    if(playerOwner.HasPermission("antiraid.use"))
                    {
                        SendReply(info.InitiatorPlayer, config.msgAntiRaid);
                        SetNull();
                        return false;
                    }
                }
                else {SetNull(); return null;}
                
                /* Проверка, если игрок наносит урон в зоне шкафа 

                privilege = initiator.GetBuildingPrivilege();
                if(privilege == null) {SetNull(); return null;}
                if(playerOwner != null && playerOwner.HasPermission("antiraid.use") && initiator.IsBuildingAuthed() == false)
                {
                    foreach(PlayerNameID users in privilege.authorizedPlayers)
                    {
                        if(users.username == playerOwner.Name) {SendReply(initiator, config.msgAntiRaid); SetNull(); return false;}
                        else {SetNull(); return null;}
                    }
                }
                else {SetNull(); return null;}
                
                */
            }
            SetNull(); 
            return null;
        }
        #endregion
        #region Helpers
        void SetNull() {playerOwner = null; privilege = null; initiator = null;}
        #endregion
    }
}