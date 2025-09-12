namespace Oxide.Plugins
{
    [Info("Abobus", "FourTeen", "1.0.0")]
    public class Abobus : RustPlugin
    {
        void OnServerInitialized()
        {
            rust.RunServerCommand("sentry.maxinterference 100");
            //rust.RunServerCommand("server.encryption 1");
        }
        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null || info.Initiator == null) return;
            if (info.Initiator is NPCAutoTurret)
            {
                var npcturret = (NPCAutoTurret)info.Initiator;
                if (npcturret != null)
                {
                    if (npcturret.OwnerID == 0)
                    {
                        info.damageTypes.Set(info.damageTypes.GetMajorityDamageType(), 10000f);
                    }
                }
            }
        }
        void OnEntityTakeDamage(BaseEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return;
            if (entity.OwnerID == 8767)
            {
                info.damageTypes.ScaleAll(0);
            }
        }
    }
}