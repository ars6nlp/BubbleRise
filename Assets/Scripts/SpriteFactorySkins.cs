using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Спрайты трофейных скинов — тех, что падают с боссов.
    ///
    /// **Пузырёк — это сам босс, только уменьшенный.** Медузка, крабик, пиранья: у
    /// каждого трофея силуэт его хозяина, а не абстрактная эмблема. Круглые по природе
    /// существа строятся поверх общей стеклянной основы, вытянутые — рыба-меч, змей —
    /// рисуются как есть; хитбокс пузырька круглый, и такой скин показывает его с
    /// запасом, то есть в пользу игрока, а не наоборот.
    ///
    /// **Страж — оружие или часть босса**, и круглым быть не обязан: клешня, клинок,
    /// клык. Радиус защиты при этом остаётся круглым и живёт отдельно от картинки,
    /// поэтому под нестандартными формами лежит мягкий ореол (SkinAura) — по нему
    /// видно, где защита работает на самом деле.
    /// </summary>
    public static partial class SpriteFactory
    {
        /// <summary>
        /// Оболочка по скину. Под материалом нужна прозрачная: обычная залита
        /// бирюзой и гасит начинку до одинакового бледного шарика у всех скинов.
        /// Одна функция на игру и на витрину — чтобы они не могли разойтись.
        /// </summary>
        /// <summary>
        /// Оболочка пузырька. Теперь у каждой стихии свой силуэт.
        ///
        /// Раньше здесь возвращалось одно и то же стекло для всех покупаемых
        /// скинов, и отличались они только цветом начинки — то есть не отличались
        /// ничем, пока на них не посмотришь в упор. Форма берётся из архетипа, а
        /// архетип — из материала, поэтому новому скину её не нужно прописывать.
        ///
        /// У трофейных скинов форма своя, нарисованная под конкретного босса: им
        /// силуэт стихии не нужен, он бы её и заменил.
        /// </summary>
        public static Sprite BubbleShell(SkinDef skin)
            => skin.Art == SkinArt.Classic
                ? ShapeShell(ArchetypeTable.Of(skin).Shape)
                : BubbleSkin(skin.Art);

        public static Sprite GuardianShell(SkinDef skin)
            => skin.Art == SkinArt.Classic
                ? ShapeShellGuardian(ArchetypeTable.Of(skin).Shape)
                : GuardianSkin(skin.Art);

        /// <summary>
        /// Трофейный скин: силуэт в 256 плюс общие слои из
        /// <see cref="BossSkinTable"/> — кайма, фактура, блик, тень и обводка.
        ///
        /// 256 вместо общих 128: трофеи показывают крупно, и на 128 кайма с
        /// фактурой превращались в кашу. [РАСЧЁТ]
        ///
        /// Слои накладываются НА БУФЕР, до заливки в текстуру: готовая текстура
        /// уже не читается, и второй проход по ней упал бы и в редакторе, и на
        /// устройстве.
        /// </summary>
        static Sprite BuildSkin(SkinArt art, bool guardian, System.Func<Vector2, Color> shade)
        {
            string key = (guardian ? "boss_g_" : "boss_b_") + art;
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var px = Render(shade, SkinSize);

            // Звезда коллекции — не трофей босса: у неё своя золотая палитра и
            // общие слои ей не нужны.
            if (BossSkinTable.TryBoss(art, out var boss))
                BossSkinFinish(px, SkinSize, BossSkinTable.Of(boss), guardian);

            return Upload(key, px, SkinSize);
        }

        public static Sprite BubbleSkin(SkinArt art) => BubbleSilhouette(art);

        public static Sprite GuardianSkin(SkinArt art) => GuardianSilhouette(art);

        static Sprite BubbleSilhouette(SkinArt art)
        {
            switch (art)
            {
                case SkinArt.Jelly: return BubbleJelly();
                case SkinArt.Serpent: return BubbleSerpent();
                case SkinArt.Octopus: return BubbleOctopus();
                case SkinArt.Swordfish: return BubbleSwordfish();
                case SkinArt.Crab: return BubbleCrab();
                case SkinArt.Ink: return BubbleInk();
                case SkinArt.Piranha: return BubblePiranha();
                case SkinArt.Turtle: return BubbleTurtle();
                case SkinArt.Shark: return BubbleShark();
                case SkinArt.Manta: return BubbleManta();
                case SkinArt.Star: return BubbleStar();
                default: return Bubble();
            }
        }

        static Sprite GuardianSilhouette(SkinArt art)
        {
            switch (art)
            {
                case SkinArt.Jelly: return GuardianJelly();
                case SkinArt.Serpent: return GuardianSerpent();
                case SkinArt.Octopus: return GuardianOctopus();
                case SkinArt.Swordfish: return GuardianSwordfish();
                case SkinArt.Crab: return GuardianCrab();
                case SkinArt.Ink: return GuardianInk();
                case SkinArt.Piranha: return GuardianPiranha();
                case SkinArt.Turtle: return GuardianTurtle();
                case SkinArt.Shark: return GuardianShark();
                case SkinArt.Manta: return GuardianManta();
                case SkinArt.Star: return GuardianStar();
                default: return Guardian();
            }
        }

        // ------------------------------------------------------------- основы

        const float SkinEdge = 0.94f;

        /// <summary>
        /// Тело скина-пузырька: шар с бликом и подсвеченной кромкой. Возвращает
        /// прозрачный цвет за пределами круга, поэтому годится и как проверка «внутри».
        /// </summary>
        static Color SkinBody(Vector2 p, Color body, Color rim)
        {
            float d = p.magnitude;
            if (d > SkinEdge) return Color.clear;

            Color c = body;
            if (d > SkinEdge - 0.16f) c = Color.Lerp(c, rim, Mathf.InverseLerp(SkinEdge - 0.16f, SkinEdge, d));

            // блик сверху-слева: он и превращает плоский круг в шар
            float gloss = 1f - Mathf.Clamp01(Vector2.Distance(p, new Vector2(-0.32f, 0.34f)) / 0.26f);
            if (gloss > 0f) c = Color.Lerp(c, Color.white, gloss * 0.55f);

            c.a = 1f;
            return c;
        }

        /// <summary>Ядро скина-стража: светящийся шар, к которому дорисовывается мотив.</summary>
        static Color SkinCore(Vector2 p, Color core, Color edge)
        {
            float d = p.magnitude;
            if (d > SkinEdge) return Color.clear;

            Color c = Color.Lerp(core, edge, Mathf.SmoothStep(0f, 1f, d / SkinEdge));
            float gloss = 1f - Mathf.Clamp01(Vector2.Distance(p, new Vector2(-0.28f, 0.30f)) / 0.30f);
            if (gloss > 0f) c = Color.Lerp(c, Color.white, gloss * 0.5f);
            c.a = 1f;
            return c;
        }

        static bool InCircle(Vector2 p, Vector2 centre, float radius)
            => Vector2.Distance(p, centre) < radius;

        /// <summary>
        /// Мягкий ореол под скином стража. Форма трофея может быть какой угодно —
        /// клешня, клинок, клык, — но радиус защиты остаётся круглым, и игрок обязан
        /// видеть, где она работает. Ореол это и показывает, не мешая читать саму форму.
        /// </summary>
        static Color SkinAura(Vector2 p, Color tint)
        {
            float d = p.magnitude;
            if (d > SkinEdge) return Color.clear;

            Color c = tint;
            c.a = Mathf.SmoothStep(0.34f, 0f, Mathf.InverseLerp(0.20f, SkinEdge, d));
            return c;
        }

        // -------------------------------------------------- скины пузырька

        /// <summary>Медузка: купол сверху, короткие щупальца внутри круга.</summary>
        static Sprite BubbleJelly() => BuildSkin(SkinArt.Jelly, false, p =>
        {
            // щупальца — волнистые ленты в нижней половине
            if (p.y < 0.05f)
            {
                for (int i = -2; i <= 2; i++)
                {
                    float x0 = i * 0.20f;
                    float wobble = Mathf.Sin(p.y * 9f + i) * 0.05f;
                    if (Mathf.Abs(p.x - x0 - wobble) < 0.045f && p.y > -0.86f)
                        return Palette.StormSpark;
                }
            }

            // купол: приплюснутый сверху
            var d = new Vector2(p.x / 0.92f, (p.y - 0.10f) / 0.72f);
            if (d.magnitude > 1f) return Color.clear;

            Color c = SkinBody(p * 0.9f, Palette.StormBell, Palette.StormDeep);
            if (c.a <= 0f) c = Palette.StormBell;

            // искры внутри купола — по ним медуза и опознаётся
            for (int i = 0; i < 5; i++)
            {
                float a = i * 2.399f;
                var spark = new Vector2(Mathf.Cos(a) * 0.34f, 0.16f + Mathf.Sin(a) * 0.24f);
                if (InCircle(p, spark, 0.075f)) return Palette.StormCore;
            }
            return c;
        });

        /// <summary>
        /// Змейка: тело буквой S и крупная голова, поднятая вверх. Пузырёк всплывает,
        /// поэтому «вперёд» для него — это вверх, и все скины-существа смотрят туда же.
        /// </summary>
        static Sprite BubbleSerpent() => BuildSkin(SkinArt.Serpent, false, p =>
        {
            // тело: три звена по вертикальной S-дуге, к хвосту тоньше
            var a0 = new Vector2(-0.34f, -0.78f);
            var a1 = new Vector2(0.22f, -0.44f);
            var a2 = new Vector2(-0.16f, -0.02f);
            var a3 = new Vector2(0.06f, 0.30f);

            bool body = TaperedSegment(p, a0, a1, 0.12f, 0.19f) ||
                        TaperedSegment(p, a1, a2, 0.19f, 0.23f) ||
                        TaperedSegment(p, a2, a3, 0.23f, 0.26f);

            // гребень по хребту: пластины наружу от изгиба
            for (int i = 0; i < 5; i++)
            {
                float t = i / 4f;
                var at = Vector2.Lerp(a1, a3, t);
                var outward = new Vector2(-0.22f, 0.06f);
                if (InTriangle(p, at + outward * 0.45f + new Vector2(0f, -0.07f),
                                  at + outward * 0.45f + new Vector2(0f, 0.07f),
                                  at + outward * 1.6f))
                    return Palette.SerpentSpine;
            }

            // голова смотрит вверх
            var head = new Vector2(0.02f, 0.60f);
            if (InCircle(p, head, 0.34f))
            {
                if (InCircle(p, head + new Vector2(0.12f, 0.06f), 0.085f)) return Palette.SerpentEye;
                if (InCircle(p, head + new Vector2(0.13f, 0.07f), 0.035f))
                    return new Color(0.05f, 0.03f, 0.02f, 1f);
                // пасть на макушке — змей идёт вперёд с раскрытой пастью
                if (p.y > head.y + 0.12f && Mathf.Abs(p.x - head.x) < 0.18f)
                    return Palette.Hex("2A1418");
                return Palette.SerpentBody;
            }

            if (!body) return Color.clear;

            Color c = Palette.SerpentBody;
            if (Mathf.Repeat((p.x + p.y) * 7f, 1f) < 0.45f)
                c = Color.Lerp(c, Palette.SerpentBelly, 0.5f);
            c.a = 1f;
            return c;
        });

        /// <summary>Спрутик: круглая голова, два глаза, щупальца по низу.</summary>
        static Sprite BubbleOctopus() => BuildSkin(SkinArt.Octopus, false, p =>
        {
            if (p.y < -0.10f)
            {
                for (int i = -2; i <= 2; i++)
                {
                    var root = new Vector2(i * 0.22f, -0.20f);
                    var tip = new Vector2(i * 0.34f, -0.88f + Mathf.Abs(i) * 0.10f);
                    if (TaperedSegment(p, root, tip, 0.10f, 0.035f)) return Palette.OctopusDark;
                }
            }

            Color c = SkinBody(p, Palette.OctopusBody, Palette.OctopusDark);
            if (c.a <= 0f) return c;

            for (int side = -1; side <= 1; side += 2)
            {
                var eye = new Vector2(side * 0.30f, 0.18f);
                if (InCircle(p, eye, 0.20f))
                {
                    if (InCircle(p, eye + new Vector2(side * 0.03f, -0.02f), 0.085f))
                        return new Color(0.05f, 0.03f, 0.06f, 1f);
                    return Palette.OctopusEye;
                }
            }

            // присоски по низу головы
            for (int i = -1; i <= 1; i++)
                if (InCircle(p, new Vector2(i * 0.24f, -0.42f), 0.065f)) return Palette.OctopusSucker;

            return c;
        });

        /// <summary>
        /// Клинок: сама рыба-меч, уменьшенная и поставленная носом вверх — туда же,
        /// куда всплывает пузырёк. По диагонали она выглядела плывущей поперёк движения.
        /// </summary>
        static Sprite BubbleSwordfish() => BuildSkin(SkinArt.Swordfish, false, p =>
        {
            var tail = new Vector2(-0.06f, -0.78f);
            var mid = new Vector2(0.04f, -0.22f);
            var head = new Vector2(-0.02f, 0.26f);
            var tip = new Vector2(0.02f, 0.98f);

            // меч
            if (TaperedSegment(p, head, tip, 0.055f, 0.012f)) return Palette.SwordBlade;

            // хвостовые лопасти
            if (InTriangle(p, tail, new Vector2(-0.44f, -0.96f), new Vector2(-0.12f, -0.62f)) ||
                InTriangle(p, tail, new Vector2(0.36f, -0.94f), new Vector2(0.10f, -0.62f)))
                return Palette.SwordFin;

            // спинной парус — сбоку, как у настоящей
            if (InTriangle(p, new Vector2(-0.14f, 0.02f), new Vector2(-0.10f, 0.26f),
                              new Vector2(-0.58f, 0.10f)))
                return Palette.SwordFin;

            bool body = TaperedSegment(p, tail, mid, 0.07f, 0.23f) ||
                        TaperedSegment(p, mid, head, 0.23f, 0.11f);
            if (!body) return Color.clear;

            // спина слева тёмная, брюхо справа светлое: свет падает сбоку
            Color c = Color.Lerp(Palette.SwordBack, Palette.SwordBelly,
                                 Mathf.InverseLerp(-0.20f, 0.20f, p.x));
            c.a = 1f;

            if (InCircle(p, new Vector2(-0.07f, 0.22f), 0.055f)) return new Color(0.05f, 0.06f, 0.09f, 1f);
            return c;
        });

        /// <summary>Крабик: панцирь, глаза на стебельках, две клешни по бокам.</summary>
        static Sprite BubbleCrab() => BuildSkin(SkinArt.Crab, false, p =>
        {
            // глаза на стебельках — единственное, что выходит за круг
            for (int side = -1; side <= 1; side += 2)
            {
                var root = new Vector2(side * 0.22f, 0.44f);
                var tip = new Vector2(side * 0.28f, 0.86f);
                if (TaperedSegment(p, root, tip, 0.065f, 0.055f)) return Palette.CrabShellDk;
                if (InCircle(p, tip + new Vector2(0f, 0.05f), 0.11f))
                    return InCircle(p, tip + new Vector2(side * 0.02f, 0.04f), 0.05f)
                        ? new Color(0.05f, 0.02f, 0.03f, 1f) : Palette.CrabHilite;
            }

            // клешни по бокам, прижаты к панцирю
            for (int side = -1; side <= 1; side += 2)
            {
                var hand = new Vector2(side * 0.68f, -0.16f);
                if (InCircle(p, hand, 0.28f))
                {
                    // раскрытая щель между клыками
                    Vector2 local = p - hand;
                    if (Mathf.Abs(local.y) < 0.045f + Mathf.Abs(local.x) * 0.25f && local.x * side > 0f)
                        return Color.clear;
                    return Palette.CrabShell;
                }
            }

            Color c = SkinBody(p, Palette.CrabShell, Palette.CrabShellDk);
            if (c.a <= 0f) return c;

            // бугры брони и тёмная кромка панциря
            for (int i = -2; i <= 2; i++)
                if (InCircle(p, new Vector2(i * 0.26f, 0.08f - Mathf.Abs(i) * 0.05f), 0.09f))
                    c = Blend(c, Palette.CrabHilite.WithAlpha(0.45f));

            // ротовые пластины
            if (Mathf.Abs(p.x) < 0.22f && p.y < -0.34f) c = Blend(c, new Color(0f, 0f, 0f, 0.4f));
            return c;
        });

        /// <summary>Каракатица: крапчатая мантия и горизонтальные зрачки.</summary>
        static Sprite BubbleInk() => BuildSkin(SkinArt.Ink, false, p =>
        {
            // короткие руки снизу
            if (p.y < -0.28f)
            {
                for (int i = -2; i <= 2; i++)
                {
                    var root = new Vector2(i * 0.18f, -0.34f);
                    var tip = new Vector2(i * 0.28f, -0.90f);
                    if (TaperedSegment(p, root, tip, 0.075f, 0.025f)) return Palette.InkMantleDk;
                }
            }

            Color c = SkinBody(p, Palette.InkMantle, Palette.InkMantleDk);
            if (c.a <= 0f) return c;

            // крап каракатицы
            for (int i = 0; i < 10; i++)
            {
                float a = i * 2.399f;
                float r = 0.18f + 0.5f * Mathf.Sqrt((i + 0.5f) / 10f);
                if (InCircle(p, new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.85f), 0.075f))
                    c = Blend(c, new Color(0f, 0f, 0f, 0.30f));
            }

            // глаза с горизонтальным зрачком — примета головоногого
            for (int side = -1; side <= 1; side += 2)
            {
                var eye = new Vector2(side * 0.34f, 0.10f);
                if (Vector2.Distance(new Vector2(p.x - eye.x, (p.y - eye.y) * 1.6f), Vector2.zero) < 0.20f)
                {
                    if (Mathf.Abs(p.y - eye.y) < 0.035f && Mathf.Abs(p.x - eye.x) < 0.13f)
                        return new Color(0.03f, 0.02f, 0.05f, 1f);
                    return Palette.InkGlow;
                }
            }
            return c;
        });

        /// <summary>
        /// Пиранья: та же рыба, что и в стае-боссе, только уменьшенная и развёрнутая
        /// пастью вверх — по ходу всплытия пузырька.
        /// </summary>
        static Sprite BubblePiranha() => BuildSkin(SkinArt.Piranha, false, p =>
        {
            // хвост снизу
            if (InTriangle(p, new Vector2(0f, -0.60f), new Vector2(-0.40f, -0.98f), new Vector2(-0.04f, -0.72f)) ||
                InTriangle(p, new Vector2(0f, -0.60f), new Vector2(0.40f, -0.98f), new Vector2(0.04f, -0.72f)))
                return Palette.PiranhaSwarmFin;

            // спинной и анальный плавники по бокам
            if (InTriangle(p, new Vector2(0.40f, 0.06f), new Vector2(0.36f, -0.26f), new Vector2(0.80f, -0.10f)) ||
                InTriangle(p, new Vector2(-0.40f, -0.06f), new Vector2(-0.28f, -0.42f), new Vector2(-0.74f, -0.24f)))
                return Palette.PiranhaSwarmFin;

            // клыки нижней челюсти торчат вбок за контур, как у босса
            for (int i = 0; i < 2; i++)
            {
                float fy = 0.28f + i * 0.14f;
                if (InTriangle(p, new Vector2(-0.24f, fy - 0.045f), new Vector2(-0.24f, fy + 0.045f),
                                  new Vector2(-0.48f, fy + 0.01f)))
                    return Palette.PiranhaSwarmTooth;
            }

            // тело: высокий диск, длинная ось теперь вертикальна
            float ey = p.y / 0.76f;
            float basic = 1f - Mathf.Pow(Mathf.Abs(ey), 2.4f);
            if (basic <= 0f) return Color.clear;
            float half = 0.58f * Mathf.Sqrt(basic);
            if (Mathf.Abs(p.x) > half) return Color.clear;

            // разинутая пасть клином вверх
            var mawBack = new Vector2(0.02f, 0.04f);
            var mawUpper = new Vector2(0.20f, 0.70f);
            var mawLower = new Vector2(-0.30f, 0.68f);
            if (InTriangle(p, mawBack, mawUpper, mawLower))
            {
                float saw = Mathf.Repeat((p.y - 0.04f) * 20f, 1f);
                float depth = 0.026f + 0.038f * saw;
                if (DistToSegment(p, mawBack, mawUpper) < depth ||
                    DistToSegment(p, mawBack, mawLower) < depth)
                    return Palette.PiranhaSwarmTooth;
                return Palette.PiranhaSwarmMaw;
            }

            Color c = Color.Lerp(Palette.PiranhaSwarmBack, Palette.PiranhaSwarmBody,
                                 Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.40f, -0.30f, p.x)));
            if (p.x < -0.04f) c = Color.Lerp(c, Palette.PiranhaSwarmBelly, Mathf.InverseLerp(-0.04f, -0.50f, p.x));
            c.a = 1f;

            if (InCircle(p, new Vector2(0.24f, 0.24f), 0.115f))
                return InCircle(p, new Vector2(0.24f, 0.24f), 0.05f)
                    ? new Color(0.04f, 0.03f, 0.04f, 1f) : Palette.PiranhaSwarmEye;

            return c;
        });

        /// <summary>Черепашка: панцирь со щитками и шипами, голова сверху.</summary>
        static Sprite BubbleTurtle() => BuildSkin(SkinArt.Turtle, false, p =>
        {
            // голова выглядывает сверху
            if (InCircle(p, new Vector2(0f, 0.78f), 0.20f))
            {
                for (int side = -1; side <= 1; side += 2)
                    if (InCircle(p, new Vector2(side * 0.08f, 0.84f), 0.035f)) return Palette.TurtleEye;
                return Palette.TurtleSkin;
            }

            // ласты по бокам
            for (int side = -1; side <= 1; side += 2)
                if (TaperedSegment(p, new Vector2(side * 0.55f, 0.22f), new Vector2(side * 0.92f, 0.48f), 0.13f, 0.06f) ||
                    TaperedSegment(p, new Vector2(side * 0.55f, -0.30f), new Vector2(side * 0.88f, -0.56f), 0.11f, 0.05f))
                    return Palette.TurtleSkin;

            float d = p.magnitude;

            // шипы по кромке панциря
            if (d > 0.86f && d < 1f)
            {
                float a = Mathf.Atan2(p.y, p.x);
                float phase = Mathf.Repeat((a + Mathf.PI) / (Mathf.PI * 2f) * 12f, 1f);
                if (d < 0.86f + (1f - Mathf.Abs(phase - 0.5f) * 2f) * 0.14f) return Palette.TurtleShellDark;
            }

            Color c = SkinBody(p, Palette.TurtleShell, Palette.TurtleShellDark);
            if (c.a <= 0f) return c;

            // щитки: шесть секторов вокруг центрального
            float ang = Mathf.Atan2(p.y, p.x);
            if (d > 0.40f && Mathf.Repeat((ang + Mathf.PI) / (Mathf.PI * 2f) * 6f, 1f) < 0.07f)
                c = Blend(c, new Color(0f, 0f, 0f, 0.45f));
            if (Mathf.Abs(d - 0.40f) < 0.04f) c = Blend(c, new Color(0f, 0f, 0f, 0.45f));

            return c;
        });

        // ---------------------------------------------------- скины стража

        /// <summary>Шаровая молния: ядро с разрядами внутри.</summary>
        static Sprite GuardianJelly() => BuildSkin(SkinArt.Jelly, true, p =>
        {
            Color c = SkinCore(p, Palette.StormCore, Palette.StormBell);
            if (c.a <= 0f) return c;

            // ломаные разряды: три луча, каждый со сдвигом фазы
            for (int i = 0; i < 3; i++)
            {
                float a = i * (Mathf.PI * 2f / 3f);
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var side = new Vector2(-dir.y, dir.x);
                Vector2 prev = Vector2.zero;
                for (int k = 1; k <= 3; k++)
                {
                    float t = k / 3f;
                    Vector2 next = dir * (t * 0.84f) + side * (0.14f * ((k % 2 == 0) ? 1f : -1f));
                    if (TaperedSegment(p, prev, next, 0.055f, 0.035f)) return Palette.StormSpark;
                    prev = next;
                }
            }
            return c;
        });

        /// <summary>
        /// Клык змея — сам по себе, без кольца вокруг. Изогнут: прямой клык читается
        /// как гвоздь, изгиб делает его зубом.
        /// </summary>
        static Sprite GuardianSerpent() => BuildSkin(SkinArt.Serpent, true, p =>
        {
            // корень клыка утоплен в десну
            if (InTriangle(p, new Vector2(-0.34f, 0.66f), new Vector2(0.34f, 0.66f),
                              new Vector2(0f, 0.30f)))
                return Palette.SerpentBelly;

            // тело клыка: два звена с изломом дают изгиб
            bool fang = TaperedSegment(p, new Vector2(-0.02f, 0.62f), new Vector2(0.10f, -0.06f), 0.26f, 0.15f) ||
                        TaperedSegment(p, new Vector2(0.10f, -0.06f), new Vector2(0.02f, -0.82f), 0.15f, 0.015f);

            if (fang)
            {
                // блик по передней грани — от него клык выглядит гладким и острым
                if (TaperedSegment(p, new Vector2(-0.08f, 0.56f), new Vector2(0.00f, -0.60f), 0.06f, 0.012f))
                    return Color.white;
                return Palette.Hex("F2EFE2");
            }

            return SkinAura(p, Palette.SerpentSpine);
        });

        /// <summary>Присоска щупальца: концентрические кольца.</summary>
        static Sprite GuardianOctopus() => BuildSkin(SkinArt.Octopus, true, p =>
        {
            Color c = SkinCore(p, Palette.OctopusSucker, Palette.OctopusBody);
            if (c.a <= 0f) return c;

            float d = p.magnitude;
            if (d < 0.30f) return Palette.OctopusDark;                       // жерло
            if (Mathf.Abs(d - 0.42f) < 0.06f) c = Color.Lerp(c, Palette.OctopusDark, 0.7f);
            if (Mathf.Abs(d - 0.62f) < 0.05f) c = Color.Lerp(c, Palette.OctopusDark, 0.5f);

            // мелкие присоски по кругу
            for (int i = 0; i < 8; i++)
            {
                float a = i * (Mathf.PI * 2f / 8f);
                if (InCircle(p, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.78f, 0.085f))
                    c = Color.Lerp(c, Palette.OctopusEye, 0.55f);
            }
            return c;
        });

        /// <summary>
        /// Клинок рыбы-меча — просто клинок, наискось во всю ширину. Диска под ним
        /// больше нет: он только мешал читать форму.
        /// </summary>
        static Sprite GuardianSwordfish() => BuildSkin(SkinArt.Swordfish, true, p =>
        {
            var hilt = new Vector2(-0.72f, -0.60f);
            var tip = new Vector2(0.80f, 0.70f);

            // основание: утолщение у рукояти, чтобы клинок не висел в пустоте
            if (TaperedSegment(p, hilt, Vector2.Lerp(hilt, tip, 0.18f), 0.20f, 0.14f))
                return Palette.SwordFin;

            if (TaperedSegment(p, hilt, tip, 0.14f, 0.015f))
            {
                // грань: светлая полоса вдоль верхней стороны
                if (TaperedSegment(p, Vector2.Lerp(hilt, tip, 0.12f), tip, 0.035f, 0.008f))
                    return Color.white;
                return Palette.SwordBlade;
            }

            // зазубрины по нижней кромке — клинок рабочий, а не парадный
            for (int i = 0; i < 3; i++)
            {
                var at = Vector2.Lerp(hilt, tip, 0.34f + i * 0.18f);
                if (InTriangle(p, at + new Vector2(0.02f, -0.06f), at + new Vector2(0.12f, -0.02f),
                                  at + new Vector2(0.10f, -0.16f)))
                    return Palette.SwordBelly;
            }

            return SkinAura(p, Palette.SwordBlade);
        });

        /// <summary>
        /// Две клешни краба — у него их и правда две. Раскрытые, как в бою: кольцом
        /// сворачивать их больше не нужно, круглый силуэт скина ничего не решает,
        /// радиус защиты живёт отдельно и показан ореолом.
        ///
        /// Обе подняты вверх и стоят рядом — левая и правая, как у краба в угрожающей
        /// позе. Вторая не копия первой, а её зеркало: у настоящей пары пальцы
        /// раскрываются наружу, в разные стороны, а не в одну.
        /// </summary>
        static Sprite GuardianCrab() => BuildSkin(SkinArt.Crab, true, p =>
        {
            if (ClawShape(p, new Vector2(-0.38f, -0.26f), 90f, 0.52f, false, out Color left)) return left;
            if (ClawShape(p, new Vector2(0.38f, -0.26f), 90f, 0.52f, true, out Color right)) return right;

            return SkinAura(p, Palette.CrabHilite);
        });

        /// <summary>
        /// Одна клешня в своей системе координат: сустав, ладонь и два пальца, мордой
        /// вдоль +x. Точку экрана переводим в эту систему, а не строим геометрию заново
        /// под каждый угол — иначе вторую клешню пришлось бы выписывать целиком.
        /// </summary>
        /// <param name="mirror">Зеркалить поперёк своей оси — так получается вторая
        /// клешня пары, а не второй экземпляр первой.</param>
        static bool ClawShape(Vector2 p, Vector2 origin, float angleDeg, float scale,
                              bool mirror, out Color colour)
        {
            colour = default;
            if (scale < 0.01f) return false;

            Vector2 q = RotateVec(p - origin, -angleDeg) / scale;
            if (mirror) q.y = -q.y;

            var palmA = new Vector2(-0.42f, -0.06f);
            var palmB = new Vector2(0.30f, 0.06f);
            var fixedTip = new Vector2(1.32f, 0.52f);
            var moveTip = new Vector2(1.16f, -0.36f);

            // сустав руки
            if (TaperedSegment(q, new Vector2(-1.05f, -0.34f), palmA, 0.20f, 0.28f))
            {
                colour = Palette.CrabShellDk;
                return true;
            }

            // ладонь
            if (TaperedSegment(q, palmA, palmB, 0.36f, 0.38f))
            {
                colour = ClawShade(q, palmA, palmB);
                return true;
            }

            // неподвижный палец — верхний и длинный
            if (TaperedSegment(q, palmB, fixedTip, 0.26f, 0.055f))
            {
                colour = ClawTeeth(q, palmB, fixedTip, -1f) ? Palette.CrabJoint : Palette.CrabShell;
                return true;
            }

            // подвижный палец — короче и толще
            if (TaperedSegment(q, palmB + new Vector2(0.02f, -0.12f), moveTip, 0.22f, 0.05f))
            {
                colour = ClawTeeth(q, palmB, moveTip, 1f) ? Palette.CrabJoint : Palette.CrabShellDk;
                return true;
            }

            return false;
        }

        /// <summary>Объём ладони: светлее к внешнему краю, темнее к сгибу.</summary>
        static Color ClawShade(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 axis = (b - a).normalized;
            float across = (p - a).x * -axis.y + (p - a).y * axis.x;
            Color c = Color.Lerp(Palette.CrabShellDk, Palette.CrabShell, Mathf.InverseLerp(-0.36f, 0.36f, across));
            if (across > 0.18f) c = Color.Lerp(c, Palette.CrabHilite, 0.45f);
            c.a = 1f;
            return c;
        }

        /// <summary>Пила по внутренней стороне пальца — тем и страшна клешня.</summary>
        static bool ClawTeeth(Vector2 p, Vector2 root, Vector2 tip, float side)
        {
            Vector2 axis = (tip - root).normalized;
            Vector2 normal = new Vector2(-axis.y, axis.x) * side;

            float along = Vector2.Dot(p - root, axis);
            float across = Vector2.Dot(p - root, normal);
            float length = (tip - root).magnitude;
            if (along < length * 0.15f || along > length * 0.95f) return false;

            float saw = Mathf.Repeat(along * 7f, 1f);
            float edge = Mathf.Lerp(0.24f, 0.06f, along / length);
            return across > edge - (0.03f + 0.06f * saw);
        }

        /// <summary>Чернильный сгусток: тёмное ядро в лиловом ореоле.</summary>
        static Sprite GuardianInk() => BuildSkin(SkinArt.Ink, true, p =>
        {
            Color c = SkinCore(p, Palette.InkGlow, Palette.InkMantleDk);
            if (c.a <= 0f) return c;

            float angle = Mathf.Atan2(p.y, p.x);
            float edge = 0.62f + 0.10f * Mathf.Sin(angle * 3f + 0.7f) + 0.05f * Mathf.Sin(angle * 5f);
            if (p.magnitude < edge)
            {
                Color ink = Palette.InkBody;
                if (InCircle(p, new Vector2(-0.20f, 0.22f), 0.14f)) ink = Color.Lerp(ink, Palette.InkGlow, 0.4f);
                ink.a = 1f;
                return ink;
            }
            return c;
        });

        /// <summary>Кольцо зубов: пасть пираньи, свёрнутая в круг.</summary>
        static Sprite GuardianPiranha() => BuildSkin(SkinArt.Piranha, true, p =>
        {
            Color c = SkinCore(p, Palette.PiranhaSwarmBelly, Palette.PiranhaSwarmBack);
            if (c.a <= 0f) return c;

            float d = p.magnitude;
            if (d < 0.34f) return Palette.PiranhaSwarmMaw;                    // глотка

            // зубы: клинья, растущие внутрь от кромки
            float angle = Mathf.Atan2(p.y, p.x);
            float phase = Mathf.Repeat((angle + Mathf.PI) / (Mathf.PI * 2f) * 14f, 1f);
            float peak = 1f - Mathf.Abs(phase - 0.5f) * 2f;
            if (d > 0.86f - peak * 0.44f && d < 0.90f) return Palette.PiranhaSwarmTooth;
            if (d > 0.34f && d < 0.86f - peak * 0.44f) return Palette.PiranhaSwarmMaw;

            return c;
        });

        /// <summary>
        /// Панцирь черепахи. Валун сюда просился по механике — им черепахи стреляют, —
        /// но серый камень ничего не говорит о том, с кого трофей. Панцирь говорит.
        /// </summary>
        static Sprite GuardianTurtle() => BuildSkin(SkinArt.Turtle, true, p =>
        {
            float d = p.magnitude;
            float ang = Mathf.Atan2(p.y, p.x);

            // шипы по кромке
            if (d > 0.74f && d < 0.98f)
            {
                float phase = Mathf.Repeat((ang + Mathf.PI) / (Mathf.PI * 2f) * 12f, 1f);
                float peak = 1f - Mathf.Abs(phase - 0.5f) * 2f;
                if (d < 0.74f + peak * 0.24f) return Palette.TurtleShellDark;
            }

            if (d > 0.78f) return SkinAura(p, Palette.TurtleShell);

            // купол панциря: светлее к макушке, темнее к кромке
            Color c = Color.Lerp(Palette.TurtleShell, Palette.TurtleShellDark, d / 0.78f);
            float gloss = 1f - Mathf.Clamp01(Vector2.Distance(p, new Vector2(-0.26f, 0.28f)) / 0.30f);
            if (gloss > 0f) c = Color.Lerp(c, Color.white, gloss * 0.35f);

            // щитки: шесть секторов вокруг центрального
            if (d > 0.34f && Mathf.Repeat((ang + Mathf.PI) / (Mathf.PI * 2f) * 6f, 1f) < 0.07f)
                c = Blend(c, new Color(0f, 0f, 0f, 0.45f));
            if (Mathf.Abs(d - 0.34f) < 0.035f) c = Blend(c, new Color(0f, 0f, 0f, 0.45f));

            // трещины от кромки внутрь — панцирь боевой
            for (int i = 0; i < 3; i++)
            {
                float a = 0.9f + i * 2.1f;
                if (TaperedSegment(p, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.72f,
                                      new Vector2(Mathf.Cos(a + 0.5f), Mathf.Sin(a + 0.5f)) * 0.40f,
                                      0.022f, 0.007f))
                    c = Blend(c, new Color(0f, 0f, 0f, 0.5f));
            }

            c.a = 1f;
            return c;
        });

        // ------------------------------------------------------------- акула

        /// <summary>
        /// Акулёнок: та же акула, только маленькая и носом вверх — как и остальные
        /// трофеи-пузырьки. Тело развёрнуто по вертикали, поэтому спина уходит вправо,
        /// а белое брюхо влево; спинной плавник торчит вбок.
        /// </summary>
        static Sprite BubbleShark() => BuildSkin(SkinArt.Shark, false, p =>
        {
            // хвост снизу: лопасти разной длины, как у взрослой
            if (InTriangle(p, new Vector2(0.02f, -0.56f), new Vector2(0.34f, -1.00f), new Vector2(0.02f, -0.76f)) ||
                InTriangle(p, new Vector2(-0.02f, -0.56f), new Vector2(-0.26f, -0.90f), new Vector2(-0.02f, -0.76f)))
                return Palette.SharkFin;

            // спинной плавник — главный опознавательный знак, торчит за контур
            if (InTriangle(p, new Vector2(0.34f, 0.22f), new Vector2(0.30f, -0.22f), new Vector2(0.86f, -0.04f)))
                return Palette.SharkFin;
            // грудной — с брюшной стороны
            if (InTriangle(p, new Vector2(-0.32f, 0.04f), new Vector2(-0.26f, -0.30f), new Vector2(-0.78f, -0.26f)))
                return Palette.SharkFin;

            // тело: веретено вдоль вертикали, самое широкое в передней трети
            float t = Mathf.InverseLerp(0.86f, -0.78f, p.y);
            if (t < 0f || t > 1f) return Color.clear;

            float half = 0.44f * Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.62f));
            if (t > 0.70f) half = Mathf.Max(half, 0.055f);        // хвостовой стебель
            if (Mathf.Abs(p.x) > half) return Color.clear;

            // разинутая пасть клином вверх, с зубами по кромкам
            var mawBack = new Vector2(0.02f, 0.10f);
            var mawUpper = new Vector2(0.24f, 0.74f);
            var mawLower = new Vector2(-0.26f, 0.70f);
            if (InTriangle(p, mawBack, mawUpper, mawLower))
            {
                float saw = Mathf.Repeat((p.y - 0.10f) * 18f, 1f);
                float depth = 0.024f + 0.034f * saw;
                if (DistToSegment(p, mawBack, mawUpper) < depth ||
                    DistToSegment(p, mawBack, mawLower) < depth)
                    return Palette.SharkTooth;
                return Palette.SharkMaw;
            }

            // спина справа, брюхо слева — резкой границей, как у взрослой
            float shade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-half * 0.30f, half * 0.18f, p.x));
            Color c = shade < 0.5f
                ? Color.Lerp(Palette.SharkBelly, Palette.SharkBody, shade * 2f)
                : Color.Lerp(Palette.SharkBody, Palette.SharkBack, (shade - 0.5f) * 2f);
            c.a = 1f;

            // жаберные щели поперёк тела
            if (p.y > 0.06f && p.y < 0.34f && Mathf.Repeat((p.y + p.x * 0.18f) * 20f, 1f) < 0.30f)
                c = Blend(c, Palette.SharkGill.WithAlpha(0.5f));

            if (InCircle(p, new Vector2(0.18f, 0.44f), 0.085f)) return Palette.SharkEye;

            return c;
        });

        /// <summary>
        /// Спинной плавник — то, что видно от акулы раньше всей акулы. Круглым его
        /// не делаем: остальные трофеи-стражи тоже держат форму оружия, а не шара.
        ///
        /// Форма держится на двух кромках: передняя почти прямая, задняя вогнутая и
        /// уводит вершину назад. Без этой вогнутости получается парус, а не акула.
        /// </summary>
        static Sprite GuardianShark() => BuildSkin(SkinArt.Shark, true, p =>
        {
            const float BaseY = -0.70f;
            const float TipY = 0.88f;

            float t = Mathf.InverseLerp(BaseY, TipY, p.y);
            if (t < 0f || t > 1f) return SkinAura(p, Palette.SharkBody);

            float front = Mathf.Lerp(0.58f, -0.18f, Mathf.Pow(t, 0.75f));   // правая кромка
            float back = Mathf.Lerp(-0.62f, -0.18f, Mathf.Pow(t, 0.42f));   // левая, вогнутая
            if (p.x > front || p.x < back) return SkinAura(p, Palette.SharkBody);

            // светлее у передней кромки — плавник читается объёмным, а не плоским пятном
            float across = Mathf.InverseLerp(back, front, p.x);
            Color c = Color.Lerp(Palette.SharkFin, Palette.SharkBody, Mathf.SmoothStep(0f, 1f, across) * 0.75f);

            // тонкая светлая кайма по передней кромке
            if (front - p.x < 0.055f) c = Color.Lerp(c, Palette.SharkBelly, 0.55f);

            // рубец у основания: плавник трофейный, а не музейный
            if (t < 0.34f && DistToSegment(p, new Vector2(-0.30f, -0.52f), new Vector2(0.16f, -0.18f)) < 0.032f)
                c = Blend(c, new Color(0f, 0f, 0f, 0.42f));

            // тень у самого основания — плавник «сидит» на спине
            if (t < 0.12f) c = Blend(c, new Color(0f, 0f, 0f, 0.35f * (1f - t / 0.12f)));

            c.a = 1f;
            return c;
        });
    
        // ------------------------------------------------------ великая тень

        /// <summary>
        /// Скатик: тот же силуэт, что у финального босса, только маленький и смотрит
        /// вверх, как остальные трофеи-пузырьки. Обвод берётся из общей функции —
        /// трофей обязан быть узнаваемо тем же зверем, а не похожим на него.
        /// </summary>
        static Sprite BubbleManta() => BuildSkin(SkinArt.Manta, false, p =>
        {
            bool onBody = MantaSilhouette(p, out float depth);
            bool onHead = MantaHead(p);
            bool onTail = MantaTail(p, out bool sting);

            if (!onBody && !onHead && !onTail) return SkinAura(p, Palette.MantaEdge);
            if (sting) return Palette.MantaSpine;

            Color c = Palette.MantaBack;

            // кромка светится — по ней тень и узнают
            if (onBody && depth < 0.22f) c = Color.Lerp(Palette.MantaEdge, c, depth / 0.22f);

            // пара пятен на спине
            for (int i = 0; i < 2; i++)
            {
                float sx = 0.30f + i * 0.20f;
                float sy = 0.08f - i * 0.07f;
                if (InCircle(p, new Vector2(sx, sy), 0.085f) || InCircle(p, new Vector2(-sx, sy), 0.085f))
                    c = Blend(c, Palette.MantaSpot.WithAlpha(0.6f));
            }

            c.a = 1f;
            return c;
        });

        /// <summary>
        /// Жало — костяной шип с хвоста. Круглым не делаем: у трофейных стражей форма
        /// оружия, а радиус защиты показывает ореол под ним.
        /// </summary>
        static Sprite GuardianManta() => BuildSkin(SkinArt.Manta, true, p =>
        {
            // остриё вверх, к основанию расширяется
            float t = Mathf.InverseLerp(0.92f, -0.78f, p.y);
            if (t < 0f || t > 1f) return SkinAura(p, Palette.MantaSpine);

            float half = Mathf.Lerp(0.02f, 0.30f, Mathf.Pow(t, 0.8f));
            if (Mathf.Abs(p.x) > half) return SkinAura(p, Palette.MantaSpine);

            // светлее по оси — шип читается объёмным
            float across = Mathf.Abs(p.x) / Mathf.Max(0.001f, half);
            Color c = Color.Lerp(Color.white, Palette.MantaSpine, Mathf.SmoothStep(0f, 1f, across));

            // зазубрины по кромкам: у ската жало пильчатое
            if (t > 0.18f && across > 0.72f && Mathf.Repeat(t * 9f, 1f) < 0.42f)
                c = Color.Lerp(c, Palette.MantaGill, 0.55f);

            // тёмное основание
            if (t > 0.82f) c = Color.Lerp(c, Palette.MantaGill, (t - 0.82f) / 0.18f);

            c.a = 1f;
            return c;
        });
    
        // ------------------------------------------------------------- звезда

        /// <summary>
        /// Пятиконечная звезда: 0 — мимо, иначе насколько точка близка к центру
        /// (1 в середине, 0 у самого острия). Строится по лучам, а не по многоугольнику:
        /// так кончики получаются острыми при любом разрешении.
        /// </summary>
        static float StarShape(Vector2 p, float outer, float inner)
        {
            float d = p.magnitude;
            if (d < 0.0001f) return 1f;
            if (d > outer) return 0f;

            // угол до ближайшего луча; первый смотрит вверх
            float angle = Mathf.Atan2(p.x, p.y);
            float sector = Mathf.PI * 2f / 5f;
            float local = Mathf.Abs(Mathf.Repeat(angle + sector * 0.5f, sector) - sector * 0.5f);

            // радиус кромки между остриём и впадиной
            float t = local / (sector * 0.5f);
            float edge = Mathf.Lerp(outer, inner, t);

            return d > edge ? 0f : 1f - d / Mathf.Max(0.001f, edge);
        }

        /// <summary>
        /// Звезда глубин — награда за полную коллекцию. Единственный скин, который
        /// нельзя получить, гоняясь за чем-то одним, поэтому и выглядит он не как
        /// трофей с кого-то, а как отдельный знак.
        /// </summary>
        static Sprite BubbleStar() => BuildSkin(SkinArt.Star, false, p =>
        {
            float core = StarShape(p, 0.95f, 0.40f);
            if (core <= 0f)
            {
                // мягкое золотое сияние вокруг лучей
                float halo = StarShape(p, 1.0f, 0.46f);
                return halo > 0f ? Palette.StarGlow.WithAlpha(0.35f * halo) : Color.clear;
            }

            // к центру светлее, к остриям — насыщенное золото
            Color c = Color.Lerp(Palette.StarDeep, Palette.StarGold, Mathf.SmoothStep(0f, 1f, core));
            if (core > 0.62f) c = Color.Lerp(c, Palette.StarGlow, (core - 0.62f) / 0.38f);

            // блик сверху-слева — звезда читается объёмной, а не наклейкой
            float gloss = 1f - Mathf.Clamp01(Vector2.Distance(p, new Vector2(-0.22f, 0.26f)) / 0.28f);
            if (gloss > 0f) c = Color.Lerp(c, Color.white, gloss * 0.45f);

            c.a = 1f;
            return c;
        });

        /// <summary>
        /// Страж-звезда. Круглым не делаем, как и остальные трофейные: форму показывает
        /// сама звезда, а рабочий радиус защиты — ореол под ней.
        /// </summary>
        static Sprite GuardianStar() => BuildSkin(SkinArt.Star, true, p =>
        {
            float core = StarShape(p, 0.92f, 0.38f);
            if (core <= 0f) return SkinAura(p, Palette.StarGold);

            Color c = Color.Lerp(Palette.StarGold, Palette.StarGlow, Mathf.SmoothStep(0f, 1f, core));

            // тёмная кайма по кромке лучей, иначе звезда расплывается свечением
            if (core < 0.16f) c = Color.Lerp(Palette.StarDeep, c, core / 0.16f);

            c.a = 1f;
            return c;
        });
    }
}
