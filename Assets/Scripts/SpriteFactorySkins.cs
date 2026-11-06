using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Оболочки скинов по рисунку (<see cref="SkinArt"/>).
    ///
    /// **Трофеи с боссов — мини-боссы.** Их рисует <see cref="MiniBoss"/>
    /// (SpriteFactoryMiniBoss*.cs): пузырёк — тело или голова босса, страж — его
    /// оружие или характерная часть. Здесь только переадресация к ним.
    ///
    /// **Звезда коллекции** рисуется здесь же, как и раньше.
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
        /// Рисунок скина 256² — у звезды коллекции. Трофеи с боссов сюда больше не
        /// ходят: они — мини-боссы (<see cref="MiniBoss"/>).
        /// </summary>
        static Sprite BuildSkin(SkinArt art, bool guardian, System.Func<Vector2, Color> shade)
        {
            string key = (guardian ? "boss_g_" : "boss_b_") + art;
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;
            return Upload(key, Render(shade, SkinSize), SkinSize);
        }

        public static Sprite BubbleSkin(SkinArt art) => BubbleSilhouette(art);

        public static Sprite GuardianSkin(SkinArt art) => GuardianSilhouette(art);

        static Sprite BubbleSilhouette(SkinArt art)
        {
            // Трофей босса — мини-босс: его тело или голова
            if (BossSkinTable.TryBoss(art, out var boss)) return MiniBoss(new MiniBossKey(boss, false));
            switch (art)
            {
                case SkinArt.Star: return BubbleStar();
                default: return Bubble();
            }
        }

        static Sprite GuardianSilhouette(SkinArt art)
        {
            // Трофей босса — оружие или характерная часть мини-босса
            if (BossSkinTable.TryBoss(art, out var boss)) return MiniBoss(new MiniBossKey(boss, true));
            switch (art)
            {
                case SkinArt.Star: return GuardianStar();
                default: return Guardian();
            }
        }

        // ------------------------------------------------------------- основы

        const float SkinEdge = 0.94f;

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
