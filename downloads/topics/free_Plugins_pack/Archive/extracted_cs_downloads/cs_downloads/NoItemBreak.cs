using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("No Item Break", "noname", "1.0.0")]
    [Description("Предотвращает поломку всех предметов.")]
    public class NoItemBreak : CovalencePlugin
    {
        void Init()
        {
            Puts("Плагин NoItemBreak загружен! Все предметы теперь не ломаются.");
        }

        object OnLoseCondition(Item item, ref float amount)
        {
            amount = 0f;
            return false;
        }
    }
}
