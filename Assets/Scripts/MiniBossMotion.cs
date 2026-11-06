using System;
using UnityEngine;

namespace BubbleRise
{
    /// <summary>Какой мини-босс: чей и какая половина пары.</summary>
    public struct MiniBossKey : IEquatable<MiniBossKey>
    {
        public BossKind Boss;
        public bool Guardian;

        public MiniBossKey(BossKind boss, bool guardian) { Boss = boss; Guardian = guardian; }

        public bool Equals(MiniBossKey o) => Boss == o.Boss && Guardian == o.Guardian;
        public override bool Equals(object o) => o is MiniBossKey k && Equals(k);
        public override int GetHashCode() => (int)Boss * 2 + (Guardian ? 1 : 0);
        public override string ToString() => (Guardian ? "g_" : "b_") + Boss;
    }

    /// <summary>
    /// Живость мини-босса: сетка поверх рисунка и то, как она гнётся во времени.
    ///
    /// Рисунок один и неподвижный (<see cref="SpriteFactory.MiniBoss"/>); двигаются
    /// вершины сетки 17×17 над ним. Щупальцам — волна с запаздыванием к кончикам,
    /// хвостам — качание от основания, крыльям — взмах от тела, клешням — поворот
    /// у сустава. Всё медленно: самый быстрый цикл — 1.8 с, без рывков и мигания.
    ///
    /// Одна функция <see cref="Fill"/> на игру и витрину: оба места получают одни и
    /// те же вершины в один и тот же момент.
    ///
    /// Координаты вершин — доли диаметра игрока, центр в нуле: [-0.5, 0.5]. Изгиб
    /// считается в единицах рисунка [-1, 1] и не выносит его за круг: это проверяет
    /// BossSkinTests.
    /// </summary>
    public static class MiniBossMotion
    {
        /// <summary>Клеток сетки по стороне.</summary>
        public const int Grid = 16;
        public const int Side = Grid + 1;
        public const int VertexCount = Side * Side;

        public static readonly Vector2[] Uvs = BuildUvs();
        public static readonly int[] Triangles = BuildTriangles();

        /// <summary>Часы для проверок: пока задано, и игра, и витрина берут время отсюда.</summary>
        public static float? ClockOverride;

        /// <summary>Время живости — нескалированное: витрина и игра в одном темпе, как у начинок.</summary>
        public static float Now => ClockOverride ?? Time.unscaledTime;

        /// <summary>
        /// Размах движения. «Тихо» — половина: рисунок и силуэт те же, только спокойнее.
        /// </summary>
        public static float Amplitude => Visuals.Motion;

        static Vector2[] BuildUvs()
        {
            var uv = new Vector2[VertexCount];
            for (int j = 0; j < Side; j++)
                for (int i = 0; i < Side; i++)
                    uv[j * Side + i] = new Vector2((float)i / Grid, (float)j / Grid);
            return uv;
        }

        static int[] BuildTriangles()
        {
            var t = new int[Grid * Grid * 6];
            int k = 0;
            for (int j = 0; j < Grid; j++)
                for (int i = 0; i < Grid; i++)
                {
                    int v = j * Side + i;
                    t[k++] = v; t[k++] = v + Side; t[k++] = v + Side + 1;
                    t[k++] = v; t[k++] = v + Side + 1; t[k++] = v + 1;
                }
            return t;
        }

        /// <summary>Вершины сетки в момент <paramref name="t"/>: доли диаметра, центр в нуле.</summary>
        public static void Fill(MiniBossKey key, float t, float amp, Vector2[] pos)
        {
            for (int i = 0; i < VertexCount; i++)
            {
                var p = Uvs[i] * 2f - Vector2.one;
                pos[i] = (p + Bend(key, p, t, amp)) * 0.5f;
            }
        }

        /// <summary>Сдвиг точки рисунка <paramref name="p"/> (единицы [-1, 1]) в момент <paramref name="t"/>.</summary>
        public static Vector2 Bend(MiniBossKey key, Vector2 p, float t, float amp)
        {
            if (amp <= 0f) return Vector2.zero;
            return MiniBossRigs.Bend(key, p, t) * amp;
        }

        // ------------------------------------------------------------ приёмы

        /// <summary>Фаза 0…2π цикла длиной <paramref name="period"/> с.</summary>
        public static float Phase(float t, float period, float offset = 0f) => (t / period + offset) * Mathf.PI * 2f;

        /// <summary>Вес области между <paramref name="a"/> и <paramref name="b"/>: 0 до a, 1 после b, плавно.</summary>
        public static float Ramp(float a, float b, float x) => Sdf.Smooth(a, b, x);

        /// <summary>Поворот точки вокруг <paramref name="pivot"/> на угол в градусах — как сдвиг.</summary>
        public static Vector2 Turn(Vector2 p, Vector2 pivot, float deg)
            => deg == 0f ? Vector2.zero : Sdf.Rot(p - pivot, deg) - (p - pivot);

        /// <summary>
        /// Волна вдоль оси вниз от <paramref name="y0"/>: сдвиг по x, растёт к кончику и
        /// запаздывает с глубиной (<paramref name="lag"/> — радиан на единицу длины).
        /// </summary>
        public static float WaveX(Vector2 p, float y0, float length, float amp, float t, float period, float lag, float offset = 0f)
        {
            float s = (y0 - p.y) / length;
            if (s <= 0f) return 0f;
            s = Mathf.Min(s, 1.2f);
            return amp * Mathf.Pow(s, 1.4f) * Mathf.Sin(Phase(t, period, offset) - lag * s);
        }
    }
}
