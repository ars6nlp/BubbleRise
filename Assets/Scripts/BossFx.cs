using System.Collections.Generic;
using UnityEngine;

namespace BubbleRise
{
    /// <summary>Одна фирменная частица: форма, место, движение, цвет и жизнь.</summary>
    public struct FxSpec
    {
        public FxShape Shape;
        /// <summary>
        /// Смещение от точки рождения. У ауры и следа — в долях радиуса тела, у удара
        /// и разрушения — в юнитах мира (до множителя размера игрока).
        /// </summary>
        public Vector2 Offset;
        /// <summary>Скорость, юнитов в секунду (до множителя размера игрока).</summary>
        public Vector2 Velocity;
        public Color Color;
        public float Life, Size, Alpha;
        /// <summary>Поворот формы и вращение, градусы и градусы в секунду.</summary>
        public float Angle, Spin;
        /// <summary>Вытянутость вдоль оси формы.</summary>
        public float Stretch;
        public float FadeIn, Gravity, Drag;
    }

    /// <summary>
    /// Фирменные эффекты боссовых скинов: аура у кромки, след, удар стража и
    /// разрушение препятствия. Каждый — уменьшенная отсылка к атаке босса: разряды
    /// медузы, чернила морока, зубы акулы, росчерк клинка.
    ///
    /// **Одна функция на игру и витрину.** Описание частиц строится здесь, а рисует
    /// его игра (<see cref="FxManager.EmitShape"/>) или витрина (точки на холсте) —
    /// поэтому «Примерить» и «Сравнить» показывают то же, что будет в бою.
    ///
    /// **Правила для глаз — числами.** Проявление 0.15 с, затухание 0.3–0.6 с, белого
    /// нет (цвета из <see cref="BossSkinDef"/> прошли SafeLight), разряд живёт не
    /// меньше 0.25 с и рождается не чаще 4 раз в секунду, вспышек и колец на весь
    /// шарик нет. Богаче легендарных боссовые скины формой частиц, а не яркостью.
    /// </summary>
    public static class BossFx
    {
        /// <summary>Проявление фирменной частицы, с.</summary>
        public const float FadeIn = 0.15f;
        /// <summary>Затухание после проявления, с: от и до.</summary>
        public const float FadeOutMin = 0.3f, FadeOutMax = 0.6f;
        /// <summary>Сколько живёт разряд ауры медузы: форма держится, а не мерцает.</summary>
        public const float BoltLife = 0.5f;
        /// <summary>Потолок частиц ауры в секунду — и перегенерации разряда.</summary>
        public const float MaxAuraRate = 4f;
        /// <summary>Частиц в фирменном ударе и в разрушении — потолок.</summary>
        public const int MaxStrike = 14, MaxBreak = 16;
        /// <summary>
        /// Общий множитель размера фирменных частиц. Размеры в строках ниже — в юнитах
        /// до него; полтора раза — чтобы форма (зуб, разряд, чешуйка) читалась в
        /// размере пальца, а не сливалась в точку [ТЕСТ]. Число частиц не растёт.
        /// </summary>
        public const float SizeScale = 1.2f;

        /// <summary>
        /// Прозрачность удара и разрушения при «Без вспышек» (по умолчанию включено):
        /// эффект остаётся целиком — форма, число, движение, — но на пятую часть тише.
        /// Аура и след не меняются: они медленные и вспышками не бывают.
        /// </summary>
        public static float BurstAlpha => Visuals.NoFlashes ? 0.8f : 1f;

        /// <summary>Общая прозрачность частиц трофеев: эффекты — тема босса, а не свет.</summary>
        public const float AlphaScale = 0.75f;

        static float R(float a, float b) => Random.Range(a, b);
        static float Life() => FadeIn + R(FadeOutMin, FadeOutMax);
        static Vector2 Dir(float rad) => new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        static float Deg(Vector2 v) => Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
        static Vector2 Rot(Vector2 v, float rad)
        {
            float s = Mathf.Sin(rad), c = Mathf.Cos(rad);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        static FxSpec S(FxShape shape, Vector2 offset, Vector2 velocity, Color color, float size,
                        float alpha = 0.8f, float angle = 0f, float stretch = 1f, float spin = 0f,
                        float gravity = 0f, float drag = 1.6f, float life = -1f)
            => new FxSpec
            {
                Shape = shape, Offset = offset, Velocity = velocity, Color = color, Size = size * SizeScale,
                Alpha = alpha * AlphaScale, Angle = angle, Stretch = stretch, Spin = spin, Gravity = gravity,
                Drag = drag, FadeIn = FadeIn, Life = life > 0f ? life : Life(),
            };

        // ------------------------------------------------------------- аура

        /// <summary>Одна частица ауры у кромки. Смещение — в радиусах тела.</summary>
        public static FxSpec Aura(BossSkinDef d)
        {
            float a = R(0f, Mathf.PI * 2f);
            Vector2 dir = Dir(a), tan = new Vector2(-dir.y, dir.x);
            Vector2 at = dir * 1.04f;
            bool second = Random.value < 0.35f;
            Color c = second ? d.FxB : d.FxA;
            switch (d.Aura)
            {
                case BossAura.Arcs:
                    // короткий разряд вдоль кромки: форма живёт целиком BoltLife
                    return S(FxShape.Bolt, at, tan * 0.05f, c, 0.12f, 0.72f, Deg(tan), 1.5f, life: BoltLife);
                case BossAura.Thorns:
                    return second
                        ? S(FxShape.Streak, at, tan * 0.08f, c, 0.12f, 0.4f, Deg(tan), 2.2f)
                        : S(FxShape.Thorn, at, dir * 0.08f, c, 0.09f, 0.8f, Deg(dir), 1.3f);
                case BossAura.Suckers:
                    return S(FxShape.Sucker, at, dir * 0.1f + Vector2.up * 0.05f, c, 0.07f, 0.7f);
                case BossAura.ColdStreaks:
                    return S(FxShape.Streak, at, tan * 0.25f, c, 0.12f, 0.55f, Deg(tan), 2.2f);
                case BossAura.Sand:
                    return second
                        ? S(FxShape.Shell, at, new Vector2(0f, -0.12f), c, 0.06f, 0.75f, R(-30f, 30f), gravity: -0.2f)
                        : S(FxShape.Grain, at, new Vector2(R(-0.05f, 0.05f), -0.1f), c, 0.035f, 0.8f, R(0f, 90f), gravity: -0.2f);
                case BossAura.InkPuffs:
                    // клубы чернил и редкие светлые «глаза» морока
                    return Random.value < 0.2f
                        ? S(FxShape.Spot, at, dir * 0.04f, d.FxB, 0.05f, 0.75f)
                        : S(FxShape.Blob, at, dir * 0.1f, d.FxA, 0.14f, 0.45f, R(0f, 360f), spin: R(-20f, 20f), drag: 2f);
                case BossAura.RedSparks:
                    return second
                        ? S(FxShape.Dot, at, Vector2.up * 0.15f, c, 0.03f, 0.75f)
                        : S(FxShape.Ring, at, Vector2.up * 0.15f, c, 0.05f, 0.7f);
                case BossAura.Shells:
                    return second
                        ? S(FxShape.Hex, at, new Vector2(0f, -0.1f), c, 0.05f, 0.7f, R(0f, 60f), gravity: -0.15f)
                        : S(FxShape.Grain, at, new Vector2(R(-0.05f, 0.05f), -0.1f), c, 0.035f, 0.8f, R(0f, 90f), gravity: -0.2f);
                case BossAura.Bubbles:
                    return second
                        ? S(FxShape.Curl, at, tan * 0.1f, c, 0.09f, 0.55f, R(0f, 360f), spin: 40f)
                        : S(FxShape.Ring, at, Vector2.up * 0.3f, c, 0.05f, 0.7f);
                default: // RedDots — мягкие красные точки ската и бирюзовые «глаза»
                    return second
                        ? S(FxShape.Spot, at, dir * 0.03f, d.FxA, 0.06f, 0.6f)
                        : S(FxShape.Dot, at, dir * 0.04f, d.FxB, 0.05f, 0.65f);
            }
        }

        // ------------------------------------------------------------- след

        /// <summary>Одна частица следа. Смещение — в радиусах тела, от центра.</summary>
        public static FxSpec Trail(BossSkinDef d, float phase)
        {
            float a = R(0f, Mathf.PI * 2f);
            Vector2 edge = Dir(a) * 0.8f;
            Vector2 below = new Vector2(R(-0.3f, 0.3f), -0.6f);
            bool second = Random.value < 0.4f;
            Color c = second ? d.FxB : d.FxA;
            switch (d.Trail)
            {
                case BossTrail.Threads:
                    // электрические нити: тонкие ломаные, повисают и гаснут
                    return S(FxShape.Bolt, below, new Vector2(R(-0.1f, 0.1f), R(0.15f, 0.3f)), c, 0.08f, 0.6f,
                             90f + R(-30f, 30f), 1.3f);
                case BossTrail.BoneDust:
                    return second
                        ? S(FxShape.Grain, edge, new Vector2(R(-0.1f, 0.1f), -0.3f), c, 0.03f, 0.75f, R(0f, 90f), gravity: -0.2f)
                        : S(FxShape.Scale, edge, new Vector2(R(-0.15f, 0.15f), 0.2f), c, 0.06f, 0.7f, R(0f, 360f), spin: 60f);
                case BossTrail.InkDrops:
                    return second
                        ? S(FxShape.Sucker, edge, new Vector2(R(-0.1f, 0.1f), 0.25f), c, 0.05f, 0.65f)
                        : S(FxShape.Blob, edge, new Vector2(R(-0.12f, 0.12f), 0.2f), c, 0.06f, 0.65f, R(0f, 360f));
                case BossTrail.Streaks:
                    return S(FxShape.Streak, below, new Vector2(0f, -0.6f), c, 0.1f, 0.55f, 90f, 2.6f, drag: 1f);
                case BossTrail.ShellSparks:
                    return second
                        ? S(FxShape.Dot, edge, Dir(a) * 0.5f, c, 0.03f, 0.8f)
                        : S(FxShape.Shell, edge, new Vector2(R(-0.15f, 0.15f), 0.15f), c, 0.06f, 0.8f, R(-40f, 40f), spin: 50f);
                case BossTrail.InkPlume:
                    // чернильный шлейф: крупные кляксы, медленно расплываются
                    return Random.value < 0.15f
                        ? S(FxShape.Spot, below, new Vector2(R(-0.05f, 0.05f), 0.1f), d.FxB, 0.05f, 0.7f)
                        : S(FxShape.Blob, below, new Vector2(R(-0.08f, 0.08f), 0.15f), d.FxA, 0.16f, 0.5f,
                            R(0f, 360f), spin: R(-15f, 15f), drag: 1.5f);
                case BossTrail.RedBubbles:
                    return second
                        ? S(FxShape.Dot, edge, new Vector2(R(-0.1f, 0.1f), 0.5f), c, 0.03f, 0.75f)
                        : S(FxShape.Ring, edge, new Vector2(R(-0.1f, 0.1f), 0.6f), c, 0.05f, 0.75f);
                case BossTrail.SandShells:
                    return second
                        ? S(FxShape.Hex, edge, new Vector2(R(-0.1f, 0.1f), -0.2f), c, 0.05f, 0.7f, R(0f, 60f), gravity: -0.2f)
                        : S(FxShape.Grain, edge, new Vector2(R(-0.1f, 0.1f), -0.25f), c, 0.03f, 0.8f, R(0f, 90f), gravity: -0.25f);
                case BossTrail.WaterRibbons:
                    // ленты воды: вытянутые струи с колыханием и пузырьки
                    return second
                        ? S(FxShape.Ring, edge, new Vector2(R(-0.1f, 0.1f), 0.6f), c, 0.045f, 0.7f)
                        : S(FxShape.Streak, below, new Vector2(Mathf.Sin(phase * 3f) * 0.2f, -0.35f), c, 0.09f, 0.45f,
                            90f + Mathf.Sin(phase * 3f) * 15f, 3f, drag: 1f);
                default: // ShadowWaves — тёмные волны крыла и редкие красные точки
                    return Random.value < 0.25f
                        ? S(FxShape.Dot, below, new Vector2(R(-0.1f, 0.1f), 0.1f), d.FxB, 0.04f, 0.65f)
                        : S(FxShape.Curl, below, new Vector2(R(-0.15f, 0.15f), -0.2f), d.FxA, 0.12f, 0.45f,
                            R(0f, 360f), spin: 30f);
            }
        }

        // ------------------------------------------------------- удар стража

        /// <summary>
        /// Удар стража — уменьшенная фирменная атака босса. Смещения от точки касания,
        /// n — от стража к препятствию. Не больше <see cref="MaxStrike"/> частиц, разлёт —
        /// около диаметра шарика: скорость гасит вязкость (Drag).
        /// </summary>
        public static void Strike(BossSkinDef d, Vector2 n, int count, List<FxSpec> output)
        {
            output.Clear();
            count = Mathf.Clamp(count, 4, MaxStrike);
            Vector2 tan = new Vector2(-n.y, n.x);
            switch (d.Strike)
            {
                case BossStrike.Arc:
                    // дуга молнии: ломаная цепочка от стража к препятствию
                    for (int i = 0; i < count; i++)
                    {
                        float k = i / (float)Mathf.Max(1, count - 1);
                        Vector2 at = n * (0.03f + k * 0.26f) + tan * (((i & 1) == 0 ? 1f : -1f) * 0.05f);
                        output.Add(S(FxShape.Bolt, at, n * 0.25f, (i & 1) == 0 ? d.FxA : d.FxB, 0.1f, 0.85f,
                                     Deg(n) + ((i & 1) == 0 ? 25f : -25f), 1.4f, drag: 3f));
                    }
                    break;
                case BossStrike.Coil:
                    // змеиный виток: чешуйки по спирали и пара шипов
                    for (int i = 0; i < count; i++)
                    {
                        float ang = i * 0.9f;
                        Vector2 dir = Rot(n, ang);
                        output.Add(i % 4 == 3
                            ? S(FxShape.Thorn, dir * 0.06f, dir * 0.9f, d.FxB, 0.08f, 0.85f, Deg(dir), 1.3f, drag: 3f)
                            : S(FxShape.Scale, dir * (0.02f + 0.012f * i), new Vector2(-dir.y, dir.x) * 0.9f + dir * 0.3f,
                                d.FxA, 0.07f, 0.85f, Deg(dir) - 90f, spin: 120f, drag: 3f));
                    }
                    break;
                case BossStrike.Grip:
                    // хватка присосок: кольцо присосок вокруг точки, стягивается внутрь
                    for (int i = 0; i < count; i++)
                    {
                        Vector2 dir = Dir(i / (float)count * Mathf.PI * 2f);
                        output.Add(S(FxShape.Sucker, n * 0.08f + dir * 0.17f, -dir * 0.35f,
                                     (i & 1) == 0 ? d.FxA : d.FxB, 0.07f, 0.85f, drag: 2.5f));
                    }
                    break;
                case BossStrike.Slash:
                    // росчерк клинка: ровная линия поперёк удара и пара осколков
                    for (int i = 0; i < count; i++)
                    {
                        float k = i / (float)Mathf.Max(1, count - 1) - 0.5f;
                        output.Add(i >= count - 2
                            ? S(FxShape.Shard, n * 0.05f, Rot(n, R(-0.6f, 0.6f)) * 0.9f, d.FxB, 0.06f, 0.85f, R(0f, 360f), drag: 3f)
                            : S(FxShape.Streak, n * 0.06f + tan * (k * 0.34f), tan * 0.6f, d.FxA, 0.09f, 0.85f,
                                Deg(tan), 2.4f, drag: 3f));
                    }
                    break;
                case BossStrike.Pincer:
                    // щелчок клешни: два клыка навстречу и песок
                    for (int i = 0; i < count; i++)
                    {
                        if (i < 2)
                        {
                            float side = i == 0 ? 1f : -1f;
                            output.Add(S(FxShape.Fang, n * 0.1f + tan * (0.16f * side), -tan * side * 0.45f, d.FxA, 0.14f, 0.9f,
                                         Deg(n) + (side > 0 ? -60f : 60f), drag: 3f));
                        }
                        else
                            output.Add(S(FxShape.Grain, n * 0.08f, Rot(n, R(-1f, 1f)) * R(0.4f, 0.9f), d.FxB, 0.035f, 0.85f,
                                         R(0f, 90f), gravity: -0.4f, drag: 2.5f));
                    }
                    break;
                case BossStrike.Splash:
                    // брызги чернил веером и пара «глаз»
                    for (int i = 0; i < count; i++)
                    {
                        Vector2 dir = Rot(n, R(-0.8f, 0.8f));
                        output.Add(i % 5 == 4
                            ? S(FxShape.Spot, n * 0.05f, dir * 0.5f, d.FxB, 0.05f, 0.85f, drag: 2.5f)
                            : S(FxShape.Blob, n * 0.04f, dir * R(0.6f, 1.0f), d.FxA, R(0.07f, 0.11f), 0.75f, R(0f, 360f), drag: 3f));
                    }
                    break;
                case BossStrike.Nibble:
                    // укус стаи: россыпь мелких зубов и красные искры
                    for (int i = 0; i < count; i++)
                    {
                        Vector2 dir = Rot(n, R(-1f, 1f));
                        output.Add((i & 1) == 0
                            ? S(FxShape.Tooth, n * 0.05f, dir * R(0.6f, 1.0f), d.FxB, 0.06f, 0.85f, Deg(dir) - 90f, drag: 3f)
                            : S(FxShape.Dot, n * 0.05f, dir * R(0.6f, 1.1f), d.FxA, 0.035f, 0.85f, drag: 3f));
                    }
                    break;
                case BossStrike.Crack:
                    // скол панциря: пластины и крошка веером
                    for (int i = 0; i < count; i++)
                    {
                        Vector2 dir = Rot(n, R(-0.9f, 0.9f));
                        output.Add((i % 3) == 0
                            ? S(FxShape.Grain, n * 0.05f, dir * R(0.5f, 0.9f), d.FxB, 0.035f, 0.85f, R(0f, 90f), gravity: -0.4f, drag: 2.5f)
                            : S(FxShape.Hex, n * 0.05f, dir * R(0.5f, 0.9f), d.FxA, 0.07f, 0.85f, R(0f, 60f), spin: R(-90f, 90f), drag: 3f));
                    }
                    break;
                case BossStrike.Jaws:
                    // пасть: два ряда зубов смыкаются на точке удара
                    for (int i = 0; i < count; i++)
                    {
                        int row = i & 1;                // верхний и нижний ряд
                        int col = i / 2;
                        int cols = Mathf.Max(1, count / 2);
                        float k = cols > 1 ? col / (float)(cols - 1) - 0.5f : 0f;
                        // ряд вдоль линии удара, по обе её стороны; зубы смотрят друг на друга
                        Vector2 side = row == 0 ? tan : -tan;
                        Vector2 at = n * (0.1f + k * 0.22f) + side * 0.15f;
                        output.Add(S(FxShape.Tooth, at, -side * 0.45f, row == 0 ? d.FxB : d.FxA, 0.07f, 0.9f,
                                     Deg(-side) - 90f, drag: 3f));
                    }
                    break;
                default: // Wave — взмах крыла: дуга мягких волн и красные точки
                    for (int i = 0; i < count; i++)
                    {
                        float k = count > 1 ? i / (float)(count - 1) - 0.5f : 0f;
                        Vector2 dir = Rot(n, k * 2f);
                        output.Add(i % 4 == 3
                            ? S(FxShape.Dot, dir * 0.1f, dir * 0.4f, d.FxB, 0.04f, 0.8f, drag: 2.5f)
                            : S(FxShape.Curl, dir * 0.1f, dir * 0.6f, d.FxA, 0.09f, 0.7f, Deg(dir), spin: 60f, drag: 2.5f));
                    }
                    break;
            }
        }

        // ------------------------------------------------- разрушение препятствия

        /// <summary>Разрушение: фирменная атака целиком. Смещения — от центра препятствия.</summary>
        public static void Break(BossSkinDef d, int count, List<FxSpec> output)
        {
            output.Clear();
            count = Mathf.Clamp(count, 6, MaxBreak);
            for (int i = 0; i < count; i++)
            {
                float a = i / (float)count * Mathf.PI * 2f + R(-0.15f, 0.15f);
                Vector2 dir = Dir(a);
                Vector2 tan = new Vector2(-dir.y, dir.x);
                bool second = (i % 3) == 2;
                Color c = second ? d.FxB : d.FxA;
                switch (d.Break)
                {
                    case BossBreak.Storm:
                        output.Add(second
                            ? S(FxShape.Dot, dir * 0.08f, dir * 0.5f, c, 0.04f, 0.8f, drag: 2f)
                            : S(FxShape.Bolt, dir * 0.14f, dir * 0.6f, c, 0.12f, 0.85f, Deg(dir), 1.4f, drag: 2.5f));
                        break;
                    case BossBreak.Spiral:
                        output.Add(second
                            ? S(FxShape.Grain, dir * 0.1f, dir * 0.6f, c, 0.035f, 0.8f, R(0f, 90f), gravity: -0.4f, drag: 2f)
                            : S(FxShape.Scale, dir * 0.1f, tan * 1.1f + dir * 0.4f, c, 0.08f, 0.85f, Deg(dir) - 90f, spin: 140f, drag: 2.5f));
                        break;
                    case BossBreak.Tentacles:
                        output.Add(second
                            ? S(FxShape.Blob, dir * 0.08f, dir * 0.4f, c, 0.08f, 0.7f, R(0f, 360f), drag: 2f)
                            : S(FxShape.Curl, dir * 0.12f, dir * 0.7f, c, 0.12f, 0.8f, Deg(dir), spin: 90f, drag: 2.5f));
                        break;
                    case BossBreak.Cross:
                    {
                        // две диагонали крестом и осколки между ними
                        if (second)
                            output.Add(S(FxShape.Shard, dir * 0.06f, dir * 0.8f, c, 0.07f, 0.85f, R(0f, 360f), spin: 90f, drag: 2.5f));
                        else
                        {
                            Vector2 axis = (i & 1) == 0 ? new Vector2(0.707f, 0.707f) : new Vector2(-0.707f, 0.707f);
                            float k = (i % 6) / 5f - 0.5f;
                            output.Add(S(FxShape.Streak, axis * (k * 0.5f), axis * (k >= 0 ? 0.5f : -0.5f), c, 0.1f, 0.85f,
                                         Deg(axis), 2.6f, drag: 2.5f));
                        }
                        break;
                    }
                    case BossBreak.Shards:
                        output.Add(second
                            ? S(FxShape.Grain, dir * 0.08f, dir * 0.5f, c, 0.035f, 0.8f, R(0f, 90f), gravity: -0.5f, drag: 2f)
                            : S(FxShape.Shard, dir * 0.1f, dir * 0.9f, c, 0.08f, 0.85f, R(0f, 360f), spin: R(-120f, 120f), drag: 2.5f));
                        break;
                    case BossBreak.InkCloud:
                        output.Add(i % 4 == 3
                            ? S(FxShape.Spot, dir * 0.12f, dir * 0.15f, d.FxB, 0.06f, 0.8f, drag: 2f)
                            : S(FxShape.Blob, dir * 0.08f, dir * 0.45f, d.FxA, R(0.12f, 0.18f), 0.55f, R(0f, 360f), spin: R(-20f, 20f), drag: 2f));
                        break;
                    case BossBreak.TeethScatter:
                        output.Add(second
                            ? S(FxShape.Ring, dir * 0.06f, dir * 0.4f + Vector2.up * 0.3f, d.FxA, 0.05f, 0.75f, drag: 2f)
                            : S(FxShape.Tooth, dir * 0.08f, dir * 0.9f, d.FxB, 0.07f, 0.85f, Deg(dir) - 90f, spin: R(-90f, 90f), drag: 2.5f));
                        break;
                    case BossBreak.PlateRing:
                        output.Add(second
                            ? S(FxShape.Grain, dir * 0.06f, dir * 0.5f, d.FxB, 0.035f, 0.8f, R(0f, 90f), gravity: -0.5f, drag: 2f)
                            : S(FxShape.Hex, dir * 0.12f, dir * 0.75f, d.FxA, 0.08f, 0.85f, R(0f, 60f), spin: R(-60f, 60f), drag: 2.5f));
                        break;
                    case BossBreak.Whirl:
                        output.Add(second
                            ? S(FxShape.Ring, dir * 0.08f, dir * 0.3f + Vector2.up * 0.4f, d.FxB, 0.05f, 0.75f, drag: 2f)
                            : S(FxShape.Curl, dir * 0.14f, tan * 0.9f, d.FxA, 0.11f, 0.75f, Deg(dir), spin: 120f, drag: 2.5f));
                        break;
                    default: // Ripples — расходящиеся кольца крыла и красные «глаза»
                        output.Add(second
                            ? S(FxShape.Spot, dir * 0.1f, dir * 0.3f, d.FxB, 0.05f, 0.8f, drag: 2f)
                            : S(FxShape.Ring, dir * 0.12f, dir * 0.7f, d.FxA, 0.07f, 0.75f, drag: 2.5f));
                        break;
                }
            }
        }
    }
}
