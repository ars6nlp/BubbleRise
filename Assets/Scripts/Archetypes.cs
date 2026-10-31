using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Силуэт шарика. Форма, а не цвет, — первое, что игрок различает на экране,
    /// и единственное, что видно даже боковым зрением.
    ///
    /// Все формы вписаны в один и тот же круг: хитбокс у скинов общий, и шипы
    /// торчат за его пределы только на глаз. Иначе дорогой скин был бы ещё и
    /// легче в игре, а дешёвый — тяжелее.
    /// </summary>
    public enum SkinShape
    {
        /// <summary>Ровный шар. База, с которой всё начинается.</summary>
        Sphere = 0,
        /// <summary>Капля: вытянута вверх, низ тяжёлый.</summary>
        Drop,
        /// <summary>Огранённый: многоугольник с резкими рёбрами.</summary>
        Faceted,
        /// <summary>Шипастый: корона коротких игл по кругу.</summary>
        Spiky,
        /// <summary>С кольцом: шар и плоское кольцо вокруг него.</summary>
        Ringed,
        /// <summary>Клубящийся: рваный край, ничего твёрдого.</summary>
        Wisp,
        /// <summary>Бугристый: неровная поверхность с наростами.</summary>
        Lumpy,
        /// <summary>Звезда: четыре длинных луча.</summary>
        Star,
        /// <summary>Гранёный блок: углы вместо круга.</summary>
        Blocky,
    }

    /// <summary>Что отличает одну стихию от другой — всё в одной строке.</summary>
    public struct ArchetypeDef
    {
        /// <summary>Название стихии для магазина. Тот же ключ, что у заданий.</summary>
        public string NameKey;
        public SkinShape Shape;
        public HapticPattern Haptic;

        /// <summary>
        /// Множитель размера рисунка. ±10 % по заданию: заметно, но на сложность
        /// не влияет — хитбокс у всех один.
        /// </summary>
        public float VisualScale;

        /// <summary>
        /// Цвет стихии. Раньше им вспыхивал весь экран в момент удара — вспышка
        /// убрана целиком, от неё болели глаза. Цвет остался: им красятся частицы
        /// удара и подписи характеристик в магазине.
        /// </summary>
        public Color Tone;

        /// <summary>Базовая высота тона звука удара — у каждой стихии своя.</summary>
        public float SoundPitch;
        /// <summary>Ключ названия звука для карточки в магазине.</summary>
        public string SoundKey;
        /// <summary>Ключ названия следа для карточки в магазине.</summary>
        public string TrailKey;
    }

    /// <summary>
    /// Стихии.
    ///
    /// **Почему архетип — это <see cref="StatusKind"/>.** Статус, который скин
    /// оставляет на препятствии, уже и есть его стихия: магма жжёт, иней морозит,
    /// гроза бьёт разрядом. Заводить рядом второй перечень «архетипов» значило бы
    /// держать два списка, которые обязаны совпадать, — и они разошлись бы при
    /// первом же новом скине.
    ///
    /// Так же решается и забывчивость: архетип не прописывают у скина руками, он
    /// достаётся из материала. Добавить скин — значит выбрать материал, и форма,
    /// вибрация и звук приедут сами.
    ///
    /// **Что именно различается.** Пять признаков, и все обязаны читаться за
    /// секунду без чтения описания: силуэт, цвет и материал, характер движения,
    /// звук удара, рисунок вибрации. Движение и цвет уже живут в SkinFxTable и
    /// MaterialFxTable; здесь — остальные три.
    /// </summary>
    public static class ArchetypeTable
    {
        static Color C(string hex) => Palette.Hex(hex);

        static ArchetypeDef D(string nameKey, SkinShape shape, HapticPattern haptic, float scale,
                              string tone,
                              float pitch, string soundKey, string trailKey)
            => new ArchetypeDef
            {
                NameKey = nameKey, Shape = shape, Haptic = haptic, VisualScale = scale,
                Tone = C(tone),
                SoundPitch = pitch, SoundKey = soundKey, TrailKey = trailKey,
            };

        /// <summary>Порядок строго как в <see cref="StatusKind"/>.</summary>
        public static readonly ArchetypeDef[] All =
        {
            // Огонь: дёргается, бьёт коротко и часто, вспышка тёплая.
            /* Burn     */ D("status.burn", SkinShape.Drop, HapticPattern.Flame, 1.04f,
                             "FF7A3A", 1.18f, "fx.sound.crackle", "fx.trail.embers"),

            // Лёд: один чистый щелчок.
            /* Freeze   */ D("status.freeze", SkinShape.Faceted, HapticPattern.Ice, 0.98f,
                             "DFF4FF", 1.46f, "fx.sound.chime", "fx.trail.shards"),

            // Электричество: быстрая серия, яркая вспышка.
            /* Shock    */ D("status.shock", SkinShape.Spiky, HapticPattern.Shock, 1.02f,
                             "CFE4FF", 1.62f, "fx.sound.zap", "fx.trail.sparks"),

            // Кислота: шипение, дрожащая вибрация.
            /* Corrode  */ D("status.corrode", SkinShape.Lumpy, HapticPattern.Acid, 1.03f,
                             "A8FF5B", 0.92f, "fx.sound.hiss", "fx.trail.droplets"),

            // Золото: тяжёлый двойной удар и звон.
            /* Gild     */ D("status.gild", SkinShape.Ringed, HapticPattern.Gold, 1.06f,
                             "FFD98A", 1.28f, "fx.sound.ring", "fx.trail.dust"),

            // Тень: длинный тяжёлый отклик, почти без свечения — тьма не светит.
            /* Shadow   */ D("status.shadow", SkinShape.Wisp, HapticPattern.Shadow, 1.00f,
                             "2A2140", 0.64f, "fx.sound.thud", "fx.trail.veil"),

            // Космос: гулкий нарастающий удар, широкая мягкая вспышка.
            /* Stardust */ D("status.stardust", SkinShape.Star, HapticPattern.Cosmos, 1.05f,
                             "B88FFF", 0.86f, "fx.sound.hum", "fx.trail.stardust"),

            // Кристалл: два звонких.
            /* Crystal  */ D("status.crystal", SkinShape.Faceted, HapticPattern.Crystal, 0.97f,
                             "E6DCFF", 1.72f, "fx.sound.chime", "fx.trail.shards"),

            // Вода: мягкий одиночный, тихая рябь. База, от которой считается всё.
            /* Soak     */ D("status.soak", SkinShape.Sphere, HapticPattern.Water, 1.00f,
                             "8FD8FF", 1.00f, "fx.sound.splash", "fx.trail.bubbles"),

            // Призрак: едва заметный отклик.
            /* Haze     */ D("status.haze", SkinShape.Wisp, HapticPattern.Ghost, 1.02f,
                             "CFE8E0", 0.78f, "fx.sound.breath", "fx.trail.veil"),

            // Радуга: переливчатая вибрация, цветная вспышка.
            /* Spectrum */ D("status.spectrum", SkinShape.Ringed, HapticPattern.Prism, 1.03f,
                             "FF8FE8", 1.34f, "fx.sound.shimmer", "fx.trail.rings"),

            // Взвесь: глухой удар, пыль.
            /* Silt     */ D("status.silt", SkinShape.Lumpy, HapticPattern.Acid, 1.02f,
                             "8FA86B", 0.80f, "fx.sound.thud", "fx.trail.dust"),

            // Камень: тяжёлый и тупой.
            /* Petrify  */ D("status.petrify", SkinShape.Blocky, HapticPattern.Gold, 1.04f,
                             "E8D8A8", 0.74f, "fx.sound.thud", "fx.trail.dust"),

            // Распад: рваная серия, мигающая вспышка.
            /* Glitch   */ D("status.glitch", SkinShape.Blocky, HapticPattern.Shock, 1.00f,
                             "5BFF9A", 1.90f, "fx.sound.zap", "fx.trail.motes"),
        };

        public static ArchetypeDef Of(StatusKind kind)
            => All[Mathf.Clamp((int)kind, 0, All.Length - 1)];

        /// <summary>Архетип скина: берётся из материала, а у трофеев — из босса.</summary>
        public static ArchetypeDef Of(SkinDef skin)
        {
            var fx = skin.Kind == UnlockKind.Boss
                ? MaterialFxTable.OfBoss(skin.UnlockBoss)
                : MaterialFxTable.Of(skin.Material);
            return Of(fx.Status);
        }

        /// <summary>Стихия скина одним значением — для подписей и сравнения.</summary>
        public static StatusKind KindOf(SkinDef skin)
        {
            var fx = skin.Kind == UnlockKind.Boss
                ? MaterialFxTable.OfBoss(skin.UnlockBoss)
                : MaterialFxTable.Of(skin.Material);
            return fx.Status;
        }
    }
}
