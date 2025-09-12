using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StormUI
{
    public class Defines
    {
        // Инициализированно ли меню (нужно для смены языков)
        public static bool isMenuInitialized = false;

        // Отключение теней
        public static bool _noShadows = false;

        // Отключение травы
        public static bool _noGrass = false;

        public static ConsoleSystem.Command NoShadowsCommand;
        public static ConsoleSystem.Command NoGrassCommand;

        // Кастомный перевод (id, новое слово)
        public static Dictionary<string, string> Translation = new Dictionary<string, string>()
        {
            { "experimental", "Бустер" },
            { "options.experimental", "Бустер" },
            { "occlusion_culling", "Убрать тени" },
            { "tooltip.culling", "Убирает тени, повышает фпс" },
            { "grass_shadows", "Убрать траву" },
            { "tooltip.grassshadows", "Убирает траву, возможно ещё добавляет IQ" }
        };
    }
}
