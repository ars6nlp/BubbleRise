namespace BubbleRise
{
    /// <summary>
    /// Справочник по боссам: кто это, где у него слабое место и как его пройти.
    ///
    /// Нужен затем, что игра не объясняет боссов текстом во время боя — все подсказки
    /// в ней визуальные и живут ровно те секунды, пока идёт замах. Если игрок не
    /// разобрался с третьего раза, ему некуда пойти и прочитать; справочник и есть
    /// это место.
    ///
    /// **Статья открывается только после встречи с боссом.** Читать про приёмы того,
    /// кого ещё не видел, — спойлер: половина удовольствия от нового босса в том,
    /// чтобы разгадать его самому. Отметка ставится на выходе босса, а не на победе:
    /// справочник нужен именно тому, кто до него дошёл и застрял.
    ///
    /// Порядок массива обязан совпадать с порядком боссов в GameBootstrap: из индекса
    /// считается глубина, на которой босс ждёт.
    /// </summary>
    public static class Codex
    {
        /// <summary>Шаг расписания в метрах: первый босс на 1000 м, десятый на 10000 м.</summary>
        public const int Step = 1000;

        /// <summary>Боссы в порядке выхода. Индекс + 1, умноженный на Step, — их глубина.</summary>
        public static readonly BossKind[] Order =
        {
            BossKind.Jelly,      // 1000 м
            BossKind.Serpent,    // 2000 м
            BossKind.Swordfish,  // 3000 м
            BossKind.Turtle,     // 4000 м
            BossKind.Piranha,    // 5000 м
            BossKind.Ink,        // 6000 м
            BossKind.Octopus,    // 7000 м
            BossKind.Crab,       // 8000 м
            BossKind.Shark,      // 9000 м
            BossKind.Manta,      // 10000 м
        };

        public static int Count => Order.Length;

        public static int DepthOf(int index) => (index + 1) * Step;

        /// <summary>Ключ имени босса в Loc. Имена там с восклицательным знаком — он для баннера.</summary>
        public static string NameKey(BossKind kind)
        {
            switch (kind)
            {
                case BossKind.Jelly:     return "boss.jelly";
                case BossKind.Serpent:   return "boss.serpent";
                case BossKind.Swordfish: return "boss.swordfish";
                case BossKind.Turtle:    return "boss.turtles";
                case BossKind.Piranha:   return "boss.piranhas";
                case BossKind.Ink:       return "boss.ink";
                case BossKind.Octopus:   return "boss.octopus";
                case BossKind.Crab:      return "boss.crab";
                case BossKind.Shark:     return "boss.shark";
                default:                 return "boss.manta";
            }
        }

        /// <summary>Имя без восклицательного знака: в списке оно не выкрикивается.</summary>
        public static string Name(BossKind kind) => Loc.Get(NameKey(kind)).TrimEnd('!');

        /// <summary>Общий корень ключей статьи: codex.jelly.what и так далее.</summary>
        static string Root(BossKind kind) => "codex." + kind.ToString().ToLowerInvariant();

        public static string What(BossKind kind) => Loc.Get(Root(kind) + ".what");
        public static string Weak(BossKind kind) => Loc.Get(Root(kind) + ".weak");
        public static string How(BossKind kind)  => Loc.Get(Root(kind) + ".how");

        /// <summary>Рисунок для статьи — тот же, что у трофейного скина с этого босса.</summary>
        public static SkinArt Art(BossKind kind)
        {
            switch (kind)
            {
                case BossKind.Jelly:     return SkinArt.Jelly;
                case BossKind.Serpent:   return SkinArt.Serpent;
                case BossKind.Swordfish: return SkinArt.Swordfish;
                case BossKind.Turtle:    return SkinArt.Turtle;
                case BossKind.Piranha:   return SkinArt.Piranha;
                case BossKind.Ink:       return SkinArt.Ink;
                case BossKind.Octopus:   return SkinArt.Octopus;
                case BossKind.Crab:      return SkinArt.Crab;
                case BossKind.Shark:     return SkinArt.Shark;
                default:                 return SkinArt.Manta;
            }
        }
    }
}
