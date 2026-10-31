using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Спрайты стайных боссов — пираний и черепах. Обе стаи рисуются одним спрайтом
    /// на особь: их много, и каждая должна стоить дёшево.
    ///
    /// Оба нарисованы **носом вправо**: контроллеры разворачивают их поворотом,
    /// так что зеркальных вариантов не нужно.
    ///
    /// Общий принцип: на экране особь занимает полтора юнита, мелкая штриховка на
    /// ней всё равно не прочитается. Поэтому вся работа уходит в силуэт, в пасть
    /// и в глаз — то, что читается на любом размере.
    /// </summary>
    public static partial class SpriteFactory
    {
        /// <summary>
        /// Пиранья из стаи-босса. Не увеличенная копия обычной рыбы-пираньи, которая
        /// плавает по уровню, а отдельный хищник.
        ///
        /// Страшной её делают четыре вещи, и все они работают на силуэт — на полутора
        /// юнитах экрана только силуэт и читается:
        ///
        /// * **Разинутая пасть** клином от морды вглубь тела, с зубами по обеим
        ///   челюстям. Не щель, а именно провал.
        /// * **Клыки, торчащие ниже челюсти** — единственная деталь, которая ломает
        ///   контур наружу. Она и опознаётся как «хищник» раньше всего остального.
        /// * **Горб на спине.** Верхний профиль вздут к переду: рыба выглядит
        ///   сгорбленной и напряжённой, а не обтекаемой.
        /// * **Скошенное надбровье** над горящим глазом. Наклон к морде — вся разница
        ///   между «смотрит» и «злится».
        /// </summary>
        public static Sprite PiranhaSwarm() => BuildRect("piranha_sw", 256, 224, p =>
        {
            Color back = Palette.PiranhaSwarmBack;
            Color body = Palette.PiranhaSwarmBody;
            Color belly = Palette.PiranhaSwarmBelly;
            Color fin = Palette.PiranhaSwarmFin;
            Color maw = Palette.PiranhaSwarmMaw;
            Color tooth = Palette.PiranhaSwarmTooth;

            // ---- хвост: раздвоенный, с рваным вырезом
            if (p.x < -0.58f)
            {
                var root = new Vector2(-0.56f, 0f);
                if (InTriangle(p, root, new Vector2(-0.99f, 0.46f), new Vector2(-0.70f, 0.05f)) ||
                    InTriangle(p, root, new Vector2(-0.99f, -0.46f), new Vector2(-0.70f, -0.05f)))
                {
                    // выкус в задней кромке — плавник потрёпан в драках
                    if (!InTriangle(p, new Vector2(-0.99f, 0.18f), new Vector2(-0.83f, 0.10f),
                                       new Vector2(-0.99f, -0.02f)))
                        return fin;
                }
            }

            if (p.x < -0.50f && p.x > -0.70f && Mathf.Abs(p.y) < 0.13f) return Color.Lerp(back, body, 0.4f);

            // ---- спинной плавник: три зубца разной высоты вместо ровного треугольника
            if (InTriangle(p, new Vector2(0.16f, 0.36f), new Vector2(-0.04f, 0.34f), new Vector2(0.02f, 0.78f)) ||
                InTriangle(p, new Vector2(0.02f, 0.36f), new Vector2(-0.16f, 0.32f), new Vector2(-0.10f, 0.66f)) ||
                InTriangle(p, new Vector2(-0.14f, 0.32f), new Vector2(-0.30f, 0.28f), new Vector2(-0.24f, 0.54f)))
                return fin;
            if (InTriangle(p, new Vector2(-0.40f, 0.20f), new Vector2(-0.54f, 0.18f), new Vector2(-0.47f, 0.36f)))
                return fin;

            // ---- анальный плавник
            if (InTriangle(p, new Vector2(-0.06f, -0.38f), new Vector2(-0.52f, -0.26f), new Vector2(-0.28f, -0.68f)))
                return fin;

            // ---- грудной плавник
            if (InTriangle(p, new Vector2(0.26f, -0.14f), new Vector2(0.08f, -0.18f), new Vector2(0.12f, -0.48f)))
                return fin;

            // ---- клыки нижней челюсти. Рисуются ДО проверки тела: они обязаны
            // торчать за контур, иначе не читаются как клыки.
            for (int i = 0; i < 3; i++)
            {
                float fx = 0.33f + i * 0.11f;
                if (InTriangle(p, new Vector2(fx - 0.036f, -0.22f), new Vector2(fx + 0.036f, -0.22f),
                                  new Vector2(fx + 0.008f, -0.42f + i * 0.02f)))
                    return tooth;
            }

            // ---- тело: высокий диск с горбом на спине. Показатель по X больше двойки,
            // поэтому профиль тупой спереди и сзади, а не заострённый, как у веретена.
            float ex = (p.x + 0.02f) / 0.68f;
            float basic = 1f - Mathf.Pow(Mathf.Abs(ex), 2.4f);
            if (basic <= 0f) return Color.clear;

            float half = 0.46f * Mathf.Sqrt(basic);
            float hump = 0.14f * Mathf.Exp(-Mathf.Pow((p.x - 0.08f) / 0.32f, 2f));
            float top = half + hump;
            float bottom = -half * 0.96f;
            if (p.y > top || p.y < bottom) return Color.clear;

            // ---- разинутая пасть: клин от морды внутрь тела
            var mawBack = new Vector2(0.10f, -0.02f);
            var mawUpper = new Vector2(0.62f, 0.15f);
            var mawLower = new Vector2(0.60f, -0.27f);
            if (InTriangle(p, mawBack, mawUpper, mawLower))
            {
                // Зубы пилой по обеим челюстям. Шаг мельче к морде — так пасть
                // выглядит забитой зубами, а не с двумя рядами колышков.
                float saw = Mathf.Repeat((p.x - 0.10f) * 24f, 1f);
                float depth = 0.022f + 0.034f * saw;
                if (DistToSegment(p, mawBack, mawUpper) < depth ||
                    DistToSegment(p, mawBack, mawLower) < depth)
                    return tooth;
                return maw;
            }

            // Спина почти чёрная, брюхо рыжее — главная примета цвета у пираньи.
            float shade = Mathf.InverseLerp(0.34f, -0.30f, p.y);
            Color c = Color.Lerp(back, body, Mathf.SmoothStep(0f, 1f, shade * 1.4f));
            if (p.y < -0.04f) c = Color.Lerp(c, belly, Mathf.InverseLerp(-0.04f, -0.40f, p.y));

            // крап по боку
            for (int i = 0; i < 9; i++)
            {
                float a = i * 2.399f;                        // золотой угол — без сетки
                float r = 0.16f + 0.34f * Mathf.Sqrt((i + 0.5f) / 9f);
                var spot = new Vector2(-0.06f + Mathf.Cos(a) * r * 1.3f, Mathf.Sin(a) * r * 0.9f);
                if (Vector2.Distance(p, spot) < 0.033f)
                    c = Blend(c, new Color(0f, 0f, 0f, 0.24f));
            }

            // шрамы поперёк бока
            if (TaperedSegment(p, new Vector2(-0.22f, 0.20f), new Vector2(-0.10f, -0.02f), 0.012f, 0.005f) ||
                TaperedSegment(p, new Vector2(-0.36f, 0.06f), new Vector2(-0.28f, -0.12f), 0.010f, 0.004f))
                c = Blend(c, new Color(1f, 1f, 1f, 0.20f));

            // жаберная дуга
            if (Mathf.Abs(p.x - 0.14f) < 0.014f && Mathf.Abs(p.y) < 0.32f)
                c = Blend(c, new Color(0f, 0f, 0f, 0.32f));

            // ---- глаз под скошенным надбровьем
            var eye = new Vector2(0.32f, 0.17f);
            float de = Vector2.Distance(p, eye);
            if (de < 0.105f)
            {
                // Наклон к морде — вся разница между «смотрит» и «злится».
                float brow = eye.y + 0.050f - (p.x - eye.x) * 0.60f;
                if (p.y > brow) return Color.Lerp(back, Color.black, 0.55f);
                if (de < 0.046f) return new Color(0.04f, 0.03f, 0.04f, 1f);
                return Palette.PiranhaSwarmEye;
            }

            return c;
        });

        /// <summary>
        /// Черепаха-камнемёт. Панцирь — приплюснутый шестиугольник с шипами по кромке
        /// и растрескавшимися щитками; из-под него торчат голова с крючковатым клювом
        /// и четыре когтистых ласта.
        ///
        /// Отличие от «мирной» черепахи — в кромке: шипы меняют силуэт, а силуэт на
        /// таком размере и есть всё узнавание.
        /// </summary>
        public static Sprite Turtle() => BuildRect("turtle", 288, 224, p =>
        {
            Color shell = Palette.TurtleShell;
            Color shellDark = Palette.TurtleShellDark;
            Color skin = Palette.TurtleSkin;
            Color eye = Palette.TurtleEye;

            // ---- шипы по кромке панциря: считаем до тела, они торчат наружу
            float ang = Mathf.Atan2(p.y, p.x);
            float dist = new Vector2(p.x / 0.60f, p.y / 0.48f).magnitude;
            if (dist > 0.88f && dist < 1.34f)
            {
                float spikePhase = Mathf.Repeat((ang + Mathf.PI) / (Mathf.PI * 2f) * 11f, 1f);
                float peak = 1f - Mathf.Abs(spikePhase - 0.5f) * 2f;          // треугольная волна
                if (dist < 0.88f + peak * 0.42f) return shellDark;
            }

            // ---- ласты с когтями
            if (TaperedSegment(p, new Vector2(0.22f, 0.16f), new Vector2(0.54f, 0.48f), 0.10f, 0.05f) ||
                TaperedSegment(p, new Vector2(0.22f, -0.16f), new Vector2(0.54f, -0.48f), 0.10f, 0.05f) ||
                TaperedSegment(p, new Vector2(-0.34f, 0.14f), new Vector2(-0.58f, 0.36f), 0.075f, 0.035f) ||
                TaperedSegment(p, new Vector2(-0.34f, -0.14f), new Vector2(-0.58f, -0.36f), 0.075f, 0.035f))
                return skin;

            for (int side = -1; side <= 1; side += 2)
            {
                var claw = new Vector2(0.58f, side * 0.52f);
                if (InTriangle(p, claw + new Vector2(-0.03f, side * -0.03f),
                                  claw + new Vector2(0.03f, side * 0.02f),
                                  claw + new Vector2(0.09f, side * 0.09f)))
                    return shellDark;
            }

            // ---- голова с крючковатым клювом
            if (Vector2.Distance(p, new Vector2(0.62f, 0f)) < 0.165f)
            {
                // Глаза узкие и горящие — это единственное «лицо», которое видно
                // на реальном размере, поэтому оно и делает черепаху злой.
                for (int side = -1; side <= 1; side += 2)
                {
                    var e = new Vector2(0.67f, side * 0.062f);
                    if (Mathf.Abs(p.x - e.x) < 0.045f && Mathf.Abs(p.y - e.y) < 0.020f)
                        return eye;
                }
                return skin;
            }
            if (InTriangle(p, new Vector2(0.74f, 0.075f), new Vector2(0.74f, -0.075f), new Vector2(0.93f, -0.03f)))
                return shellDark;                                 // клюв, загнутый вниз

            // ---- панцирь
            if (dist > 1f) return Color.clear;

            Color c = Color.Lerp(shell, shellDark, dist * 0.6f);

            // Щитки: шесть секторов вокруг центрального — узор, по которому черепаха
            // узнаётся даже размером в сантиметр.
            if (dist > 0.42f && Mathf.Repeat((ang + Mathf.PI) / (Mathf.PI * 2f) * 6f, 1f) < 0.07f)
                c = Blend(c, new Color(0f, 0f, 0f, 0.40f));
            if (Mathf.Abs(dist - 0.42f) < 0.038f)
                c = Blend(c, new Color(0f, 0f, 0f, 0.40f));

            // трещины: короткие сколы от кромки внутрь
            for (int i = 0; i < 4; i++)
            {
                float a = 0.7f + i * 1.9f;
                var from = new Vector2(Mathf.Cos(a) * 0.55f, Mathf.Sin(a) * 0.44f);
                var to = new Vector2(Mathf.Cos(a + 0.4f) * 0.30f, Mathf.Sin(a + 0.4f) * 0.24f);
                if (TaperedSegment(p, from, to, 0.020f, 0.006f))
                    c = Blend(c, new Color(0f, 0f, 0f, 0.45f));
            }

            if (dist > 0.90f) c = Blend(c, new Color(0f, 0f, 0f, 0.30f));

            return c;
        });
    }
}
