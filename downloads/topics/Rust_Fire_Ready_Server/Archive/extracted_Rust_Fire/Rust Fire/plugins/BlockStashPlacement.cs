namespace Oxide.Plugins
{
    [Info("Block Stash Placement", "Orange", "1.0.2")]
    public class BlockStashPlacement : RustPlugin
    {
        private object CanBuild(Planner planner, Construction prefab, Construction.Target target)
        {
            var player = planner.GetOwnerPlayer();
            var name = prefab.fullName;
            if (name.ToLower().Contains("stash"))
            {
                player?.ChatMessage("<color=#F84949>Ошибка!</color> Нельзя ставить <color=#ff7300>Small Stash</color>");
                return true;
            }
            
            return null;
        }
    }
}