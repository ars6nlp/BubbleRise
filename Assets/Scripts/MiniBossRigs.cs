using UnityEngine;
using static BubbleRise.MiniBossMotion;

namespace BubbleRise
{
    /// <summary>
    /// Характер движения каждого мини-босса — как у самого босса, только мягче.
    ///
    /// Каждая функция отдаёт сдвиг точки рисунка в момент t (единицы холста
    /// [-1, 1]). Области (щупальца, хвост, крылья, клешни) заданы теми же
    /// координатами, что и рисунок в SpriteFactoryMiniBoss*.cs.
    ///
    /// Периоды — от 1.9 до 3.9 с, размахи — несколько процентов размера: ничего не
    /// дёргается и не мигает. Рисунок с изгибом не выходит за круг игрока
    /// (BossSkinTests.Motion_StaysInsideCircleAndIsSlow).
    /// </summary>
    public static class MiniBossRigs
    {
        public static Vector2 Bend(MiniBossKey key, Vector2 p, float t)
        {
            switch (key.Boss)
            {
                case BossKind.Jelly: return key.Guardian ? JellyBall(p, t) : JellyBell(p, t);
                case BossKind.Serpent: return key.Guardian ? SerpentFang(p, t) : SerpentCoil(p, t);
                case BossKind.Octopus: return key.Guardian ? OctopusArm(p, t) : OctopusHead(p, t);
                case BossKind.Swordfish: return key.Guardian ? SwordTip(p, t) : SwordFish(p, t);
                case BossKind.Crab: return key.Guardian ? CrabPincer(p, t) : CrabBody(p, t);
                case BossKind.Ink: return key.Guardian ? InkDrop(p, t) : InkMantle(p, t);
                case BossKind.Piranha: return key.Guardian ? PiranhaJaws(p, t) : PiranhaFish(p, t);
                case BossKind.Turtle: return key.Guardian ? TurtleBoulder(p, t) : TurtleTop(p, t);
                case BossKind.Shark: return key.Guardian ? SharkFin(p, t) : SharkFish(p, t);
                case BossKind.Manta: return key.Guardian ? MantaSting(p, t) : MantaRay(p, t);
            }
            return Vector2.zero;
        }

        /// <summary>Сдвиг в системе рыбы (<see cref="MiniBossPose"/>) → сдвиг на холсте.</summary>
        static Vector2 InFrame(Vector2 p, MiniBossFrame f, System.Func<Vector2, Vector2> local)
            => Sdf.Rot(local(f.ToL(p)) * f.S, f.Deg);

        static Vector2 Breath(Vector2 p, Vector2 c, float ax, float ay, float t, float period)
        {
            float k = Mathf.Sin(Phase(t, period));
            return new Vector2((p.x - c.x) * ax * k, (p.y - c.y) * ay * k);
        }

        // Медуза: купол дышит раз в 2.6 с (как у босса), руки — волна раз в 2.4 с,
        // с запаздыванием к кончикам.
        static Vector2 JellyBell(Vector2 p, float t)
        {
            Vector2 d = Breath(p, new Vector2(0f, 0.3f), -0.015f, 0.025f, t, 2.6f) * Ramp(-0.05f, 0.12f, p.y);
            d.x += WaveX(p, 0.06f, 0.9f, 0.07f, t, 2.4f, 2.6f, p.x * 0.35f);
            return d;
        }

        // Шар молнии: руки медленно закручиваются и раскручиваются, бахрома колышется.
        static Vector2 JellyBall(Vector2 p, float t)
        {
            var o = new Vector2(0f, 0.06f);
            float r = (p - o).magnitude;
            Vector2 d = Turn(p, o, 4.5f * Mathf.Sin(Phase(t, 3f)) * Ramp(0.3f, 0.8f, r));
            d += Breath(p, o, 0.02f, 0.02f, t, 2.6f) * Ramp(0.5f, 0.3f, r);
            d.x += WaveX(p, -0.24f, 0.62f, 0.045f, t, 2.4f, 2.4f);
            return d;
        }

        // Змей: волна бежит от головы к хвосту, голова почти неподвижна.
        static Vector2 SerpentCoil(Vector2 p, float t)
        {
            float s = Sat((0.62f - p.y) / 1.45f);
            float w = Ramp(0.08f, 0.35f, s);
            float ph = Phase(t, 2.4f) - 4.2f * s;
            return new Vector2(0.055f * w * Mathf.Sin(ph), 0.018f * w * Mathf.Cos(ph));
        }

        // Клык: покачивается в челюсти.
        static Vector2 SerpentFang(Vector2 p, float t)
            => Turn(p, new Vector2(0f, -0.48f), 3.5f * Mathf.Sin(Phase(t, 2.8f)) * Ramp(-0.5f, -0.2f, p.y));

        // Спрут: короткие руки колышутся, голова чуть дышит.
        static Vector2 OctopusHead(Vector2 p, float t)
        {
            Vector2 d = Breath(p, new Vector2(0f, 0.22f), 0.015f, 0.02f, t, 2.8f) * Ramp(-0.2f, 0f, p.y);
            d.x += WaveX(p, -0.16f, 0.62f, 0.07f, t, 2.6f, 2.4f, p.x * 0.6f);
            return d;
        }

        // Присоска: спираль подкручивается, стебель качается с запаздыванием.
        static Vector2 OctopusArm(Vector2 p, float t)
        {
            float ph = Phase(t, 2.8f);
            Vector2 d = Turn(p, new Vector2(0.1f, 0.4f), 6f * Mathf.Sin(ph) * Ramp(-0.1f, 0.3f, p.y));
            d.x += 0.045f * Ramp(-0.86f, 0.1f, p.y) * Mathf.Sin(ph - 0.8f);
            return d;
        }

        // Рыба-меч: клинок чуть ходит, хвост качается в противофазе.
        static Vector2 SwordFish(Vector2 p, float t)
            => InFrame(p, MiniBossPose.Swordfish, l =>
                   Turn(l, new Vector2(0.18f, 0f), 2.5f * Mathf.Sin(Phase(t, 2.6f))) * Ramp(0.1f, 0.25f, l.x)
                 + Turn(l, new Vector2(-0.5f, 0f), -7f * Mathf.Sin(Phase(t, 2.6f))) * Ramp(-0.45f, -0.6f, l.x));

        // Остриё: клинок покачивается над головой.
        static Vector2 SwordTip(Vector2 p, float t)
            => Turn(p, new Vector2(0f, -0.32f), 3f * Mathf.Sin(Phase(t, 2.6f)) * Ramp(-0.32f, -0.05f, p.y));

        // Краб: клешни медленно раскрываются, панцирь чуть дышит.
        static Vector2 CrabBody(Vector2 p, float t)
        {
            float ph = Phase(t, 2.6f);
            Vector2 d = Breath(p, Vector2.zero, 0.012f, 0.015f, t, 2.6f) * Ramp(0.3f, 0.1f, p.y);
            for (int s = -1; s <= 1; s += 2)
            {
                float side = Ramp(0.22f, 0.36f, s * p.x);
                float up = Ramp(0.18f, 0.34f, p.y);
                d += Turn(p, new Vector2(s * 0.42f, 0.1f), s * 4f * Mathf.Sin(ph)) * side * up;
                // подвижный палец ближе к центру — открывается чуть позже
                float finger = Ramp(0.12f, 0.03f, (p - new Vector2(s * 0.34f, 0.66f)).magnitude);
                d += Turn(p, new Vector2(s * 0.44f, 0.55f), s * 7f * (0.5f + 0.5f * Mathf.Sin(ph + 0.6f))) * finger;
            }
            return d;
        }

        // Клешня: подвижный палец открывается и смыкается, вся клешня качается.
        static Vector2 CrabPincer(Vector2 p, float t)
        {
            float ph = Phase(t, 2.6f);
            float finger = Ramp(0.02f, 0.14f, p.x) * Ramp(-0.12f, 0.06f, p.y);
            Vector2 d = Turn(p, new Vector2(0.18f, -0.04f), -6f * (0.5f + 0.5f * Mathf.Sin(ph))) * finger;
            d += Turn(p, new Vector2(0f, -0.86f), 1.2f * Mathf.Sin(Phase(t, 3.4f)));
            return d;
        }

        // Морок: руки — волна с запаздыванием, мантия покачивается (±3°, как у босса).
        static Vector2 InkMantle(Vector2 p, float t)
        {
            Vector2 d = Turn(p, new Vector2(0f, 0.05f), 3f * Mathf.Sin(Phase(t, 3.9f))) * Ramp(-0.1f, 0.2f, p.y);
            d.x += WaveX(p, -0.04f, 0.75f, 0.075f, t, 2.4f, 2.8f, p.x * 0.5f);
            return d;
        }

        // Чернила: капля дышит, подтёки тянутся.
        static Vector2 InkDrop(Vector2 p, float t)
        {
            Vector2 d = Breath(p, new Vector2(0f, 0.12f), 0.03f, 0.03f, t, 3f);
            d.x += WaveX(p, -0.18f, 0.6f, 0.05f, t, 2.8f, 2.2f);
            d.y -= 0.025f * Ramp(-0.2f, -0.7f, p.y) * (0.5f + 0.5f * Mathf.Sin(Phase(t, 2.8f, 0.25f)));
            return d;
        }

        // Пиранья: хвост виляет (1.9 с), нижняя челюсть чуть ходит.
        static Vector2 PiranhaFish(Vector2 p, float t)
            => InFrame(p, MiniBossPose.Piranha, l =>
                   Turn(l, new Vector2(-0.48f, 0f), 9f * Mathf.Sin(Phase(t, 1.9f))) * Ramp(-0.42f, -0.58f, l.x)
                 + Turn(l, new Vector2(0.12f, -0.05f), -3f * (0.5f + 0.5f * Mathf.Sin(Phase(t, 2.6f)))) * Ramp(0.14f, 0.24f, l.x) * Ramp(-0.02f, -0.1f, l.y));

        // Челюсть: половины раскрываются и смыкаются.
        static Vector2 PiranhaJaws(Vector2 p, float t)
        {
            float open = 0.5f + 0.5f * Mathf.Sin(Phase(t, 2.4f));
            float up = Ramp(-0.62f, -0.36f, p.y);
            Vector2 d = Vector2.zero;
            for (int s = -1; s <= 1; s += 2)
                d += Turn(p, new Vector2(0f, -0.64f), -s * 4f * open) * Ramp(-0.02f, 0.1f, s * p.x) * up;
            return d;
        }

        // Черепаха: всё тело покачивается, ласты гребут попеременно.
        static Vector2 TurtleTop(Vector2 p, float t)
        {
            Vector2 d = Turn(p, Vector2.zero, 3f * Mathf.Sin(Phase(t, 3.2f)));
            float ph = Phase(t, 2.4f);
            for (int s = -1; s <= 1; s += 2)
            {
                float front = Ramp(0.2f, 0.08f, Sdf.Segment(p, new Vector2(s * 0.16f, 0.2f), new Vector2(s * 0.5f, 0.42f)));
                d += Turn(p, new Vector2(s * 0.16f, 0.2f), s * 10f * Mathf.Sin(ph + (s > 0 ? Mathf.PI : 0f))) * front;
                float back = Ramp(0.16f, 0.06f, Sdf.Segment(p, new Vector2(s * 0.14f, -0.34f), new Vector2(s * 0.36f, -0.56f)));
                d += Turn(p, new Vector2(s * 0.14f, -0.34f), -s * 7f * Mathf.Sin(ph + (s > 0 ? 0f : Mathf.PI))) * back;
            }
            return d;
        }

        // Валун: медленно покачивается на месте.
        static Vector2 TurtleBoulder(Vector2 p, float t)
            => Turn(p, new Vector2(0f, -0.7f), 3f * Mathf.Sin(Phase(t, 3.4f)));

        // Акула: хвост качается (2 с), тело чуть изгибается навстречу.
        static Vector2 SharkFish(Vector2 p, float t)
            => InFrame(p, MiniBossPose.Shark, l =>
                   Turn(l, new Vector2(-0.52f, 0f), 6f * Mathf.Sin(Phase(t, 2f))) * Ramp(-0.46f, -0.62f, l.x)
                 + Turn(l, new Vector2(0.1f, 0f), -1.5f * Mathf.Sin(Phase(t, 2f))) * Ramp(0.05f, -0.3f, l.x) * Ramp(-0.62f, -0.46f, l.x));

        // Плавник: покачивается, будто режет воду.
        static Vector2 SharkFin(Vector2 p, float t)
            => Turn(p, new Vector2(0f, -0.44f), 4f * Mathf.Sin(Phase(t, 2.2f))) * Ramp(-0.44f, -0.2f, p.y);

        // Скат: крылья взмахивают раз в 2.8 с (у босса 2.6), к кончикам — с запаздыванием; хвост колышется.
        static Vector2 MantaRay(Vector2 p, float t)
        {
            float a = Mathf.Abs(p.x);
            float w = Mathf.Pow(Ramp(0.12f, 0.86f, a), 1.3f);
            Vector2 d = new Vector2(0f, 0.075f * w * Mathf.Sin(Phase(t, 2.8f) - 0.7f * a));
            d.x += WaveX(p, -0.12f, 0.75f, 0.06f, t, 2.8f, 2.5f);
            return d;
        }

        // Жало: хвост-хлыст колышется, жало чуть качается в противофазе.
        static Vector2 MantaSting(Vector2 p, float t)
        {
            Vector2 d = Turn(p, new Vector2(0f, -0.1f), -3f * Mathf.Sin(Phase(t, 2.6f))) * Ramp(-0.1f, 0.2f, p.y);
            d.x += WaveX(p, -0.08f, 0.8f, 0.06f, t, 2.6f, 2.6f);
            return d;
        }

        static float Sat(float x) => x < 0f ? 0f : x > 1f ? 1f : x;
    }
}
