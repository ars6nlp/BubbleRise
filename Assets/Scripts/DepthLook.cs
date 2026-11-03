using UnityEngine;

namespace BubbleRise
{
    /// <summary>Биом по высоте подъёма: от бездны у дна к свету под поверхностью.</summary>
    public enum Biome { Abyss, Trench, Canyon, Ruins, Shallows }

    /// <summary>
    /// Вид воды на высоте подъёма — пять ступеней на круг в 10 000 м.
    ///
    /// Игра — «подъём из глубин»: на 0 м темно, к 10 000 м (финальный босс)
    /// вода светлеет до бирюзы. Дальше ряд боссов начинается заново, и вода за
    /// 400 м медленно «ныряет» обратно в бездну: без рывка, без смены яркости
    /// за кадр.
    ///
    /// Главное ограничение — читаемость. Вода за игровой колонкой не светлее
    /// L ≈ 0.022 на всей высоте экрана [РАСЧЁТ]: только так коралл (L 0.17)
    /// держит контраст 3:1. Светлеет у поверхности не центр, а края, верхняя
    /// кромка, лучи и каустики — их задают EdgeLight и две силы ниже.
    /// </summary>
    public struct DepthLook
    {
        public Color WaterTop, WaterMid, WaterBottom;
        /// <summary>Цвет света у краёв и верхней кромки.</summary>
        public Color EdgeLight;
        /// <summary>Цвет тумана: в него тонут дальние слои.</summary>
        public Color Fog;
        /// <summary>Сила лучей, каустик, верхнего свечения, 0…1.</summary>
        public float Shafts, Caustics, TopGlow;
        /// <summary>Плотность и цвет морского снега.</summary>
        public float Snow;
        public Color SnowTint;
        /// <summary>Гул глубины, 0…1: на дне громче.</summary>
        public float Hum;

        /// <summary>Длина круга, м: на ней же повторяется ряд боссов.</summary>
        public const float Cycle = 10000f;
        /// <summary>За сколько метров после круга вода возвращается в бездну.</summary>
        public const float Dive = 400f;
        /// <summary>Ширина плавного перехода между ступенями, м.</summary>
        public const float Blend = 300f;

        /// <summary>Нижние границы ступеней внутри круга, м.</summary>
        public static readonly float[] StepStart = { 0f, 1500f, 4000f, 7000f, 9500f };

        static readonly DepthLook[] Steps =
        {
            // Бездна: индиго, тусклый холодный снег, лучей нет
            Make("0A1024", "070B1A", "04060F", "1A2448", "0E1530", 0f, 0f, 0.15f, 1.0f, "6E7FA4", 1.0f),
            // Впадина: первый еле заметный свет сверху
            Make("0C1830", "091226", "060B19", "1E3256", "112040", 0.25f, 0f, 0.25f, 0.85f, "7A8EAE", 0.8f),
            // Каньон: два слоя лучей, туман
            Make("0E2034", "0A182A", "07101D", "22405E", "0E1E31", 0.45f, 0.15f, 0.3f, 0.7f, "8498B0", 0.55f),
            // Руины: каустики у кромки, тёплые акценты у декора
            Make("0D2131", "0A1927", "08131F", "28506A", "0F2131", 0.5f, 0.3f, 0.35f, 0.55f, "8CA2B2", 0.35f),
            // Под поверхностью: бирюзовая полоса у верхней кромки, рябь
            Make("0D2031", "0B1D2C", "091724", "2E6E80", "102737", 0.45f, 0.3f, 0.35f, 0.45f, "94AEB8", 0.15f),
        };

        static DepthLook Make(string top, string mid, string bottom, string edge, string fog,
                              float shafts, float caustics, float glow, float snow, string snowTint, float hum)
            => new DepthLook
            {
                WaterTop = Palette.Hex(top), WaterMid = Palette.Hex(mid), WaterBottom = Palette.Hex(bottom),
                EdgeLight = Palette.Hex(edge), Fog = Palette.Hex(fog),
                Shafts = shafts, Caustics = caustics, TopGlow = glow,
                Snow = snow, SnowTint = Palette.Hex(snowTint), Hum = hum,
            };

        /// <summary>Положение внутри круга и доля «ныряния» обратно в бездну.</summary>
        static float CycleMeters(float meters, out float dive)
        {
            dive = 0f;
            if (meters < Cycle) return Mathf.Max(0f, meters);
            float m = Mathf.Repeat(meters, Cycle);
            if (m < Dive)
            {
                // ныряем: от последней ступени к первой за Dive метров
                dive = 1f - Mathf.SmoothStep(0f, 1f, m / Dive);
                return 0f;
            }
            return m;
        }

        /// <summary>Вид воды на высоте подъёма <paramref name="meters"/>.</summary>
        public static DepthLook At(float meters)
        {
            float m = CycleMeters(meters, out float dive);
            if (dive > 0f) return Lerp(Steps[0], Steps[Steps.Length - 1], dive);

            int i = 0;
            while (i + 1 < StepStart.Length && m >= StepStart[i + 1]) i++;
            var look = Steps[i];
            // плавный вход в следующую ступень: последние Blend/2 метров до неё и первые после
            if (i + 1 < StepStart.Length)
            {
                float t = Mathf.InverseLerp(StepStart[i + 1] - Blend * 0.5f, StepStart[i + 1] + Blend * 0.5f, m);
                if (t > 0f) look = Lerp(look, Steps[i + 1], Mathf.SmoothStep(0f, 1f, t));
            }
            if (i > 0)
            {
                float t = Mathf.InverseLerp(StepStart[i] - Blend * 0.5f, StepStart[i] + Blend * 0.5f, m);
                if (t < 1f) look = Lerp(Steps[i - 1], look, Mathf.SmoothStep(0f, 1f, t));
            }
            return look;
        }

        /// <summary>Биом на высоте: от него зависит набор декораций слоёв.</summary>
        public static Biome BiomeAt(float meters)
        {
            float m = CycleMeters(meters, out float dive);
            if (dive > 0.5f) return Biome.Shallows;
            int i = 0;
            while (i + 1 < StepStart.Length && m >= StepStart[i + 1]) i++;
            return (Biome)i;
        }

        /// <summary>
        /// Биом на высоте и следующий за ним с долей перехода: у границы ступени
        /// новые декорации появляются вперемешку со старыми, а не стеной.
        /// </summary>
        public static Biome BiomeMix(float meters, out Biome next, out float t)
        {
            float m = CycleMeters(meters, out float dive);
            if (dive > 0f)
            {
                next = Biome.Abyss;
                t = 1f - dive;
                return Biome.Shallows;
            }
            int i = 0;
            while (i + 1 < StepStart.Length && m >= StepStart[i + 1]) i++;
            next = (Biome)Mathf.Min(i + 1, StepStart.Length - 1);
            t = i + 1 < StepStart.Length
                ? Mathf.InverseLerp(StepStart[i + 1] - Blend * 1.5f, StepStart[i + 1], m)
                : 0f;
            return (Biome)i;
        }

        public static DepthLook Lerp(DepthLook a, DepthLook b, float t)
            => new DepthLook
            {
                WaterTop = Color.Lerp(a.WaterTop, b.WaterTop, t),
                WaterMid = Color.Lerp(a.WaterMid, b.WaterMid, t),
                WaterBottom = Color.Lerp(a.WaterBottom, b.WaterBottom, t),
                EdgeLight = Color.Lerp(a.EdgeLight, b.EdgeLight, t),
                Fog = Color.Lerp(a.Fog, b.Fog, t),
                Shafts = Mathf.Lerp(a.Shafts, b.Shafts, t),
                Caustics = Mathf.Lerp(a.Caustics, b.Caustics, t),
                TopGlow = Mathf.Lerp(a.TopGlow, b.TopGlow, t),
                Snow = Mathf.Lerp(a.Snow, b.Snow, t),
                SnowTint = Color.Lerp(a.SnowTint, b.SnowTint, t),
                Hum = Mathf.Lerp(a.Hum, b.Hum, t),
            };
    }
}
