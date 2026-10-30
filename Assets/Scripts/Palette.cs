using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Палитра прототипа. Все цвета игры берутся отсюда, чтобы стиль оставался единым.
    /// </summary>
    public static class Palette
    {
        public static readonly Color Deep    = Hex("081522"); // фон, глубина
        public static readonly Color Deep2   = Hex("0D2436"); // тени
        public static readonly Color Teal    = Hex("12414A"); // ближе к поверхности
        public static readonly Color Aqua    = Hex("5BE9D1"); // страж, пузырёк, добро
        public static readonly Color AquaDim = Hex("2B8F82");
        public static readonly Color Coral   = Hex("FF6B5B"); // опасность
        public static readonly Color CoralDim= Hex("A8392C");
        public static readonly Color Foam    = Hex("EAF6F2"); // текст
        public static readonly Color Gold    = Hex("FFD166"); // баннеры

        // Оттенки препятствий (из прототипа)
        public static readonly Color Rock       = Hex("7D8B93");
        public static readonly Color UrchinBody = Hex("3A2540");
        public static readonly Color CoralBody  = Hex("B5573F");
        public static readonly Color CoralTip   = Hex("D97B5F");
        public static readonly Color BossBody   = Hex("3E1A28");
        public static readonly Color BossSpike  = Hex("5E2536");
        public static readonly Color BossMaw    = Hex("140A12");
        public static readonly Color BossTeeth  = Hex("E8DCD0");

        // Грозовая медуза (первый босс)
        public static readonly Color StormBell   = Hex("3A2E6B");
        public static readonly Color StormDeep   = Hex("221A45");
        public static readonly Color StormSpark  = Hex("7FE8FF");
        public static readonly Color StormCore   = Hex("E9FBFF");

        // Змей бездны
        public static readonly Color SerpentBody  = Hex("26404E");
        public static readonly Color SerpentBelly = Hex("4E7A88");
        public static readonly Color SerpentSpine = Hex("FF6B5B");
        public static readonly Color SerpentEye   = Hex("FF4A2E");   // красный: жёлтый выглядел добродушно

        // Спрут глубин
        public static readonly Color OctopusBody   = Hex("5B2E63");
        public static readonly Color OctopusDark   = Hex("38193F");
        public static readonly Color OctopusSucker = Hex("E8A9C8");
        public static readonly Color OctopusEye    = Hex("FFD166");

        // Клинок бездны (рыба-меч)
        // Спина уведена почти в чёрный, а брюхо приглушено: прежняя сине-серая рыба
        // выглядела скорее нарядной, чем опасной.
        public static readonly Color SwordBack  = Hex("172B3C");
        public static readonly Color SwordBelly = Hex("8299AC");
        public static readonly Color SwordBlade = Hex("E6EEF4");
        public static readonly Color SwordFin   = Hex("101E2E");
        public static readonly Color SwordScar  = Hex("6E8699");   // старые рубцы на боку
        public static readonly Color SwordEye   = Hex("FF5A3C");   // злой глаз

        // Стая пираний (босс). Имена с суффиксом Swarm: обычная рыба-пиранья,
        // которая плавает по уровню, уже занимает Piranha* выше.
        public static readonly Color PiranhaSwarmBack  = Hex("35434D");   // тёмная спина, но не чернее фона
        public static readonly Color PiranhaSwarmBody  = Hex("7C8D96");
        public static readonly Color PiranhaSwarmBelly = Hex("C4451C");   // рыжее брюхо
        public static readonly Color PiranhaSwarmFin   = Hex("1E272E");
        public static readonly Color PiranhaSwarmMaw   = Hex("4A0C15");   // нутро пасти
        public static readonly Color PiranhaSwarmTooth = Hex("F6F2E4");
        public static readonly Color PiranhaSwarmEye   = Hex("FF5A2B");

        // Белая смерть (акула). Спина почти сливается с водой, брюхо белое —
        // в профиль её видно целиком, в лоб остаётся только светлая полоса.
        public static readonly Color SharkBack  = Hex("222E38");
        public static readonly Color SharkBody  = Hex("4E6069");
        public static readonly Color SharkBelly = Hex("E4E9EA");
        public static readonly Color SharkFin   = Hex("27333C");
        public static readonly Color SharkMaw   = Hex("5B1220");   // нутро раскрытой пасти
        public static readonly Color SharkTooth = Hex("FBF7EC");
        public static readonly Color SharkEye   = Hex("101418");   // чёрный глаз без блика — мёртвый взгляд
        public static readonly Color SharkGill  = Hex("2D3A43");

        // Великая тень (скат). Тело темнее самой воды: в фоне он должен читаться
        // дырой в свете, а не серым пятном. Серый силуэт на тёмном фоне выглядел
        // безобидным скатом из документального фильма, а не финальным боссом.
        public static readonly Color MantaBack   = Hex("04070B");
        public static readonly Color MantaEdge   = Hex("22454F");   // холодная кромка крыла
        public static readonly Color MantaSpot   = Hex("16252E");   // отметины, чуть светлее тела
        public static readonly Color MantaEye    = Hex("FF4A3A");   // глаза — единственное яркое пятно
        public static readonly Color MantaBelly  = Hex("C3CED6");
        public static readonly Color MantaGill   = Hex("1B242E");
        public static readonly Color MantaGlow   = Hex("6FE9D8");   // свет раскрытой жабры — мишень
        public static readonly Color MantaSpine  = Hex("E8E2D0");   // шип на хвосте

        // Золотая звезда — награда за полную коллекцию
        public static readonly Color StarGold = Hex("FFD24A");
        public static readonly Color StarDeep = Hex("C98A12");
        public static readonly Color StarGlow = Hex("FFF3C4");

        // Каменный панцирь (черепахи)
        public static readonly Color TurtleShell     = Hex("5E7F3E");
        public static readonly Color TurtleShellDark = Hex("374E26");
        public static readonly Color TurtleSkin      = Hex("8AA65E");
        public static readonly Color TurtleEye       = Hex("FFB13D");

        // Чернильный морок
        public static readonly Color InkBody    = Hex("0A0710");   // сами чернила
        public static readonly Color InkMantle  = Hex("2E1B4A");
        public static readonly Color InkMantleDk= Hex("170C28");
        public static readonly Color InkGlow    = Hex("9B6BFF");   // сифон и глаза
        public static readonly Color InkSucker  = Hex("C8A2E8");

        // Клешнерук (краб)
        public static readonly Color CrabShell   = Hex("B4432A");
        public static readonly Color CrabShellDk = Hex("7E2C1B");
        public static readonly Color CrabHilite  = Hex("E0764F");
        public static readonly Color CrabJoint   = Hex("5C1E12");

        // Рыбы
        // Два вида рыб различаются только цветом, поэтому цвета взяты яркие и
        // предельно несхожие: красный и зелёный. Прежние — приглушённый кирпичный
        // и тёмно-фиолетовый — на тёмной воде сливались и друг с другом, и с фоном,
        // а игроку нужно опознать рыбу за долю секунды.
        public static readonly Color PiranhaBody = Hex("FF4030");
        public static readonly Color PiranhaFin  = Hex("B52A1E");
        public static readonly Color LurkerBody  = Hex("44E06A");
        public static readonly Color LurkerFin   = Hex("1E8F42");
        public static readonly Color JellyBell   = Hex("C9A6FF");
        public static readonly Color JellyCore   = Hex("FF9BD2");

        /// <summary>Цвет фона на глубине -> у поверхности (верх градиента).</summary>
        public static readonly Color GradTopDeep    = Hex("081522");
        public static readonly Color GradTopSurface = Hex("14465C");
        public static readonly Color GradBotDeep    = Hex("050C14");
        public static readonly Color GradBotSurface = Hex("0A1E2C");

        public static Color Hex(string hex, float alpha = 1f)
        {
            hex = hex.TrimStart('#');
            int r = System.Convert.ToInt32(hex.Substring(0, 2), 16);
            int g = System.Convert.ToInt32(hex.Substring(2, 2), 16);
            int b = System.Convert.ToInt32(hex.Substring(4, 2), 16);
            return new Color(r / 255f, g / 255f, b / 255f, alpha);
        }

        public static Color WithAlpha(this Color c, float a)
        {
            c.a = a;
            return c;
        }

        /// <summary>Потолок суммы каналов для всего, что светится: без белого пересвета.</summary>
        public const float LightLimit = 2.3f;

        /// <summary>
        /// Цвет без белого пересвета. Если сумма каналов больше limit, все три
        /// уменьшаются в одной пропорции: оттенок тот же, яркость ниже. Альфа не
        /// трогается.
        /// </summary>
        public static Color SafeLight(Color c, float limit = LightLimit)
        {
            float sum = c.r + c.g + c.b;
            if (sum <= limit || sum <= 0f) return c;
            float k = limit / sum;
            return new Color(c.r * k, c.g * k, c.b * k, c.a);
        }
    }
}
