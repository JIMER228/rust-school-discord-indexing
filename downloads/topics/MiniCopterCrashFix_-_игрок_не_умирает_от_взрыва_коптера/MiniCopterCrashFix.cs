namespace Oxide.Plugins
{
    [Info("MiniCopterCrashFix", "cheese - lox", "1.0.0")]
    class MiniCopterCrashFix : RustPlugin
    {
        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity is not BasePlayer || info?.Initiator is not BaseEntity initiator) return null;

            if (initiator is Minicopter or ScrapTransportHelicopter or AttackHelicopter)
            {
                info.damageTypes?.ScaleAll(0.0f);
                return true;
            }

            return null;
        }
    }
}