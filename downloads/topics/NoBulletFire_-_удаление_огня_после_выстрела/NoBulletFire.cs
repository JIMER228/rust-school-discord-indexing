namespace Oxide.Plugins
{
    [Info("NoBulletFire", "cheese - lox", "1.0.0")]
    class NoBulletFire : RustPlugin
    {
        private static readonly string[] FirePatterns = 
        { 
            "/fireball", "/napalm", "/fire", "/flame", "/molotov", "oil" 
        };

        private void OnEntitySpawned(BaseEntity entity)
        {
            if (entity == null) return;
            var prefab = entity.PrefabName;

            if (string.IsNullOrEmpty(prefab)) return;
            
            for (int i = 0; i < 6; i++)
            {
                if (prefab.Contains(FirePatterns[i]))
                {
                    entity.Kill();
                    return;
                }
            }
        }
    }
}