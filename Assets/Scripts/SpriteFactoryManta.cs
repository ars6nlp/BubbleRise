using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Спрайты «Великой тени» — финального босса.
    ///
    /// Скат нарисован видом сверху и смотрит в +Y: он парит в фоне над игроком, и
    /// разворачивать его в профиль незачем — вся узнаваемость ската в силуэте
    /// сверху, в размахе крыльев.
    ///
    /// Тело почти чёрное. Это сознательно: в фоне он обязан читаться как тень, а не
    /// как ещё одна рыба на экране. Опознаётся по подсвеченной кромке крыла и по
    /// светлым пятнам на спине — их и видно, когда он проплывает за кораллами.
    /// </summary>
    public static partial class SpriteFactory
    {
        /// <summary>Смещение средней линии крыла: кончики отогнуты назад.</summary>
        static float MantaWingCentre(float a) => -0.10f * a * a;

        /// <summary>
        /// Полутолщина крыла на расстоянии a от осевой линии (a = 0 центр, 1 — кончик).
        /// Эллиптический профиль: у корпуса крыло толстое, к концу сходит в остриё.
        /// </summary>
        static float MantaWingHalf(float a)
        {
            if (a >= 1f) return 0f;
            return 0.34f * Mathf.Pow(1f - a * a, 0.55f);
        }

        /// <summary>
        /// Обвод ската. Возвращает false, если точка вне тела; иначе выдаёт, насколько
        /// точка близка к кромке (0 — у самого края, 1 — в глубине тела).
        /// </summary>
        static bool MantaSilhouette(Vector2 p, out float depth)
        {
            depth = 0f;
            float a = Mathf.Abs(p.x);
            if (a > 1f) return false;

            float centre = MantaWingCentre(a);
            float half = MantaWingHalf(a);
            if (half <= 0.0001f) return false;

            // Передняя кромка прямее задней, задняя подрезана в середине крыла —
            // без этого выреза получается ровный ромб, а не скат.
            float up = half * 0.85f;
            float down = half * (1.15f - 0.45f * Mathf.Sin(Mathf.PI * a));

            // Задняя кромка рваная: гладкая дуга читалась как аккуратная рыба, а
            // изорванный край — как что-то, что давно живёт внизу и цело не осталось.
            down *= 1f - 0.16f * Mathf.Abs(Mathf.Sin(a * 34f)) * Mathf.SmoothStep(0f, 1f, a);

            float dy = p.y - centre;
            if (dy > up || dy < -down) return false;

            depth = Mathf.Min(dy < 0f ? (dy + down) / down : (up - dy) / up, (1f - a) / 0.25f);
            depth = Mathf.Clamp01(depth);
            return true;
        }

        /// <summary>Голова с парой цефальных «рожков» — ими скат загоняет добычу в рот.</summary>
        static bool MantaHead(Vector2 p)
        {
            if (Mathf.Abs(p.x) < 0.19f && p.y > 0f && p.y < 0.46f) return true;

            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 root = new Vector2(side * 0.15f, 0.34f);
                Vector2 tip = new Vector2(side * 0.24f, 0.56f);
                if (TaperedSegment(p, root, tip, 0.055f, 0.030f)) return true;
            }
            return false;
        }

        /// <summary>Хвост-плеть: длинный, тонкий, с жалом на конце.</summary>
        static bool MantaTail(Vector2 p, out bool sting)
        {
            sting = false;
            if (p.y > -0.20f || Mathf.Abs(p.x) > 0.09f) return false;

            if (TaperedSegment(p, new Vector2(0f, -0.20f), new Vector2(0f, -0.62f), 0.045f, 0.012f))
                return true;

            // жало у основания хвоста — у ската оно именно там, а не на кончике
            if (InTriangle(p, new Vector2(-0.045f, -0.30f), new Vector2(0.045f, -0.30f),
                              new Vector2(0f, -0.46f)))
            {
                sting = true;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Скат сверху. Размах крыльев ложится на всю ширину спрайта, поэтому при
        /// scale = размах он занимает ровно столько, сколько ему назначили.
        /// </summary>
        public static Sprite MantaBack(bool mask) => BuildRect(MaskKey("manta_back", mask), 512, 320, p =>
        {
            Color body = Palette.MantaBack;
            Color edge = Palette.MantaEdge;
            Color spot = Palette.MantaSpot;

            bool onBody = MantaSilhouette(p, out float depth);
            bool onHead = MantaHead(p);
            bool onTail = MantaTail(p, out bool sting);

            if (!onBody && !onHead && !onTail) return Color.clear;
            if (sting) return Palette.MantaSpine;

            Color c = body;

            // Светящаяся кромка по всему обводу: единственное, по чему тень видно
            // на тёмном фоне. Без неё он сливается с водой полностью.
            if (onBody && depth < 0.16f)
                c = Color.Lerp(edge, body, depth / 0.16f);

            // Отметины на спине, симметрично — по ним скат и опознаётся сверху.
            for (int i = 0; i < 4; i++)
            {
                float sx = 0.26f + i * 0.17f;
                float sy = 0.10f - i * 0.06f;
                float r = 0.075f - i * 0.011f;
                if (InCircle(p, new Vector2(sx, sy), r) || InCircle(p, new Vector2(-sx, sy), r))
                    c = Blend(c, spot.WithAlpha(0.55f));
            }

            // Костяной гребень вдоль хребта: ряд шипов, которые торчат вверх и в
            // фоне видны раньше самого тела.
            if (Mathf.Abs(p.x) < 0.05f && p.y < 0.34f && p.y > -0.18f)
                c = Blend(c, spot.WithAlpha(0.55f));
            for (int i = 0; i < 5; i++)
            {
                float sy = 0.24f - i * 0.085f;
                if (InTriangle(p, new Vector2(-0.020f, sy), new Vector2(0.020f, sy),
                                  new Vector2(0f, sy + 0.042f)))
                    return Palette.MantaSpot;
            }

            // Глаза. Единственное яркое пятно на всём звере — и то, из-за чего он
            // перестаёт быть декорацией: в фоне сначала видно два огонька, и только
            // потом понятно, какого они размера.
            for (int side = -1; side <= 1; side += 2)
            {
                var eye = new Vector2(side * 0.085f, 0.30f);
                float d = Vector2.Distance(new Vector2(p.x, (p.y - eye.y) * 1.7f + eye.y), eye);
                if (d < 0.030f)
                {
                    Color glow = Palette.MantaEye;
                    return Color.Lerp(Color.white, glow, Mathf.SmoothStep(0f, 1f, d / 0.030f));
                }
                if (d < 0.052f) c = Blend(c, Palette.MantaEye.WithAlpha(0.35f * (1f - d / 0.052f)));
            }

            c.a = 1f;
            return c;
        }, 2);

        /// <summary>
        /// Скат снизу — третий акт, когда он висит прямо над игроком. Брюхо светлое,
        /// поперёк него идёт рот, и больше ничего: жаберные щели убраны вместе с
        /// механикой, которая на них держалась, а сами по себе они превращали брюхо
        /// в полосатый ковёр, на котором терялось всё остальное.
        /// </summary>
        public static Sprite MantaBelly(bool mask) => BuildRect(MaskKey("manta_belly", mask), 512, 320, p =>
        {
            Color body = Palette.MantaBelly;
            Color dark = Palette.MantaGill;

            // Хвоста здесь нет намеренно: в третьем акте его роль играет живая плеть,
            // и нарисованный на спрайте отросток выглядел вторым хвостом рядом с ней.
            bool onBody = MantaSilhouette(p, out float depth);
            bool onHead = MantaHead(p);

            if (!onBody && !onHead) return Color.clear;

            Color c = body;

            // к кромке крыла брюхо темнеет — иначе силуэт расплывается белым пятном
            if (onBody) c = Color.Lerp(dark, body, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(depth / 0.35f)));

            // рот поперёк головы
            if (p.y > 0.26f && p.y < 0.33f && Mathf.Abs(p.x) < 0.17f)
                c = Blend(c, new Color(0f, 0f, 0f, 0.75f));


            c.a = 1f;
            return c;
        }, 2);

        /// <summary>
        /// Раскрытая жаберная щель — мишень третьего акта. Рисуется поверх брюха
        /// отдельным спрайтом: щели открываются по очереди, и каждая живёт свою жизнь.
        ///
        /// Закрытая щель тёмная и в глаза не бросается, открытая горит холодным
        /// светом. Спутать их нельзя — от этого зависит, куда отправлять разряд.
        /// </summary>
        public static Sprite MantaGill(bool open) => Build(open ? "manta_gill_open" : "manta_gill", p =>
        {
            // вытянутая поперёк щель
            var q = new Vector2(p.x / 0.95f, p.y / 0.42f);
            float d = q.magnitude;
            if (d > 1f) return Color.clear;

            if (!open)
            {
                Color shut = Palette.MantaGill;
                shut.a = Mathf.SmoothStep(0.85f, 0.2f, d);
                return shut;
            }

            // Свет из нутра: ярче к центру, с тёмной каймой по краю щели.
            Color c = Color.Lerp(Color.white, Palette.MantaGlow, Mathf.SmoothStep(0f, 1f, d));
            if (d > 0.78f) c = Color.Lerp(c, Palette.MantaGill, (d - 0.78f) / 0.22f);
            c.a = 1f;
            return c;
        });

        /// <summary>
        /// Свечение глаза — рисуется поверх тела и пульсирует. Отдельным спрайтом,
        /// потому что тело в фоне полупрозрачное, а огоньки гаснуть вместе с ним не
        /// должны: именно они и держат всё ощущение присутствия.
        /// </summary>
        public static Sprite MantaEyeGlow() => Build("manta_eye_glow", p =>
        {
            float d = p.magnitude;
            if (d > 1f) return Color.clear;

            Color c = Color.Lerp(Color.white, Palette.MantaEye, Mathf.SmoothStep(0f, 1f, d * 1.4f));
            c.a = Mathf.SmoothStep(1f, 0f, d) * 0.9f;
            return c;
        });

        /// <summary>
        /// Детёныш: тот же силуэт, только упрощённый. Пятна и хребет убраны — на таком
        /// размере они превращаются в грязь, а узнаваемость держится на одном обводе.
        /// </summary>
        public static Sprite MantaPup() => BuildRect("manta_pup", 192, 120, p =>
        {
            Color body = Palette.MantaBack;
            Color edge = Palette.MantaEdge;

            bool onBody = MantaSilhouette(p, out float depth);
            bool onHead = MantaHead(p);
            bool onTail = MantaTail(p, out bool sting);
            if (!onBody && !onHead && !onTail) return Color.clear;

            Color c = body;
            if (onBody && depth < 0.30f) c = Color.Lerp(edge, body, depth / 0.30f);
            if (sting) c = Palette.MantaSpine;
            c.a = 1f;
            return c;
        }, 2);

        /// <summary>
        /// Хвост-плеть третьего акта. Длинная, тонкая и почти чёрная — как продолжение
        /// самой тени, а не отдельная лента поперёк экрана.
        ///
        /// Спрайт вытянут 8:1, и по высоте у него всего ±0.0625 в нормализованных
        /// координатах. Первая версия рисовалась в пределах ±0.30 — то есть заведомо
        /// за краем, — и вместо плети выходила сплошная трапеция во всю высоту.
        /// </summary>
        public static Sprite MantaLash(bool lit) => BuildRect(lit ? "manta_lash_lit" : "manta_lash", 512, 64, p =>
        {
            const float Edge = 0.0625f;                  // половина высоты спрайта
            float t = Mathf.InverseLerp(-1f, 1f, p.x);   // 0 у основания, 1 у жала

            // Плеть сходит на нет к концу: у основания толщиной почти во всю высоту,
            // у жала — в нить.
            float half = Mathf.Lerp(Edge * 0.85f, Edge * 0.10f, Mathf.Pow(t, 0.55f));

            // лёгкая волна вдоль — плеть живая, а не палка
            float wave = Mathf.Sin(t * 6.5f) * Edge * 0.35f * (1f - t);
            float d = Mathf.Abs(p.y - wave);

            // Жало: костяное остриё. Растёт из самой плети, а не приставлено к ней —
            // при прежней ширине в четыре толщины тела оно читалось отдельным
            // треугольником, висящим на отлёте.
            if (t > 0.86f)
            {
                float u = (t - 0.86f) / 0.14f;
                float spike = Mathf.Lerp(half * 1.9f, 0f, u * u);
                if (d > spike) return Color.clear;
                return lit ? Color.white : Palette.MantaSpine;
            }

            if (d > half) return Color.clear;

            // Тело почти чёрное, и только по самой кромке идёт холодный контур —
            // иначе на тёмной воде плеть теряется целиком.
            float rim = Mathf.Clamp01(1f - (half - d) / Mathf.Max(0.0001f, half * 0.45f));
            Color c = Color.Lerp(Palette.MantaBack, Palette.MantaEdge, rim * 0.9f);

            // На замахе по плети бежит свет — это и есть предупреждение об ударе.
            if (lit)
            {
                float glow = Mathf.Clamp01(1f - d / Mathf.Max(0.0001f, half));
                c = Color.Lerp(c, Palette.MantaGlow, glow * 0.55f);
            }

            c.a = 1f;
            return c;
        }, 3);

        /// <summary>
        /// Тяжёлый разряд — основная форма молний третьего акта. Не шарик, а сгусток
        /// с хвостом: вытянут по ходу движения, с ядром впереди и гаснущим следом
        /// позади.
        ///
        /// Круглые шары на тёмной воде читались конфетти, особенно когда их несколько:
        /// разряд обязан выглядеть летящим, а не висящим.
        /// </summary>
        public static Sprite MantaOrb(bool charged) => BuildRect(
            charged ? "manta_orb_hot" : "manta_orb", 256, 96, p =>
        {
            Color shell = charged ? Palette.MantaGlow : Palette.MantaEye;

            // Голова у правого края — снаряды разворачиваются по скорости.
            var head = new Vector2(0.52f, 0f);
            float toHead = Vector2.Distance(p, head);

            // ядро
            if (toHead < 0.16f)
                return Color.Lerp(Color.white, shell, Mathf.SmoothStep(0f, 1f, toHead / 0.16f));

            // Хвост: сужается и гаснет к концу, с лёгкой рябью — это ток, а не комета.
            float t = Mathf.InverseLerp(0.52f, -1f, p.x);
            if (t >= 0f && t <= 1f)
            {
                float half = Mathf.Lerp(0.155f, 0.012f, Mathf.Pow(t, 0.55f));
                float ripple = Mathf.Sin(t * 22f) * 0.02f * (1f - t);

                if (Mathf.Abs(p.y - ripple) < half)
                {
                    Color tail = Color.Lerp(Color.white, shell, Mathf.SmoothStep(0f, 1f, t * 1.4f));
                    tail.a = Mathf.SmoothStep(1f, 0f, t * t);
                    return tail;
                }
            }

            // ореол вокруг головы
            if (toHead < 0.30f)
            {
                Color halo = shell;
                halo.a = Mathf.SmoothStep(0.5f, 0f, (toHead - 0.16f) / 0.14f);
                return halo;
            }

            return Color.clear;
        }, 3);

        /// <summary>
        /// Искра — звено цепи. Короткий разряд-стрела: тонкая, с изломом и ярким
        /// остриём. Мелкий кружок в этой роли невозможно было отличить от частицы
        /// эффекта.
        /// </summary>
        public static Sprite MantaSpark() => BuildRect("manta_spark", 192, 48, p =>
        {
            float t = Mathf.InverseLerp(1f, -1f, p.x);      // 0 у острия
            if (t < 0f || t > 1f) return Color.clear;

            // излом посередине — разряд, а не палочка
            float bend = Mathf.Abs(t - 0.45f) < 0.2f ? (t - 0.45f) * 0.55f : 0f;
            float half = Mathf.Lerp(0.02f, 0.085f, Mathf.Sin(t * Mathf.PI * 0.9f));

            float d = Mathf.Abs(p.y - bend);
            if (d > half) return Color.clear;

            Color c = Color.Lerp(Color.white, Palette.MantaEye, Mathf.SmoothStep(0f, 1f, t));
            c.a = Mathf.SmoothStep(1f, 0.25f, t);
            return c;
        }, 3);

        /// <summary>
        /// Раскольник — тяжёлый разряд, который на полпути разлетается на три.
        /// Внутри перекрещиваются трещины ровно по числу осколков: по ним его и
        /// отличают, и по ним видно, что целым он не долетит.
        /// </summary>
        public static Sprite MantaSplitter() => BuildRect("manta_splitter", 192, 128, p =>
        {
            // приплюснутое по ходу движения ядро, а не ровный круг
            var q = new Vector2(p.x / 0.62f, p.y / 0.48f);
            float d = q.magnitude;
            if (d > 1f) return Color.clear;

            Color c = Color.Lerp(Color.white, Palette.MantaSpine, Mathf.SmoothStep(0f, 1f, d));

            // три трещины наружу — по числу будущих осколков
            float ang = Mathf.Atan2(q.y, q.x) + Mathf.PI;
            float sector = Mathf.PI * 2f / 3f;
            float local = Mathf.Abs(Mathf.Repeat(ang, sector) - sector * 0.5f);

            if (local < 0.18f && d > 0.25f) c = Color.Lerp(c, Palette.MantaEye, 0.9f);
            if (d < 0.28f) c = Color.white;

            // тёмная кайма: без неё снаряд расплывается светлым пятном
            if (d > 0.86f) c = Color.Lerp(c, Palette.MantaGill, (d - 0.86f) / 0.14f);

            c.a = 1f;
            return c;
        }, 3);

        /// <summary>
        /// Шип. Летит тем же механизмом, что и молния медузы, но отражать его нельзя —
        /// только принять на стража, поэтому и выглядит он совершенно иначе: костяное
        /// остриё вместо электрического разряда.
        /// </summary>
        public static Sprite MantaSpike() => BuildRect("manta_spike", 128, 40, p =>
        {
            // остриё смотрит в +X, к хвостовику расширяется и темнеет
            float t = Mathf.InverseLerp(1f, -1f, p.x);
            if (t < 0f || t > 1f) return Color.clear;

            float half = Mathf.Lerp(0.02f, 0.15f, Mathf.Pow(t, 0.7f));
            if (Mathf.Abs(p.y) > half) return Color.clear;

            Color c = Color.Lerp(Color.white, Palette.MantaSpine, t);
            if (t > 0.72f) c = Color.Lerp(c, Palette.MantaGill, (t - 0.72f) / 0.28f);

            // зазубрина вдоль острия
            if (Mathf.Abs(p.y) < half * 0.22f && t > 0.2f)
                c = Blend(c, new Color(0f, 0f, 0f, 0.35f));

            c.a = 1f;
            return c;
        }, 3);
    }
}
