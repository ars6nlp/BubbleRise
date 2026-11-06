using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Своя система координат для мини-боссов, нарисованных боком (рыбы, змей):
    /// u — от хвоста к носу, v — от брюха к спине. Рыба повёрнута на <see cref="Deg"/>
    /// и ужата в <see cref="S"/>, чтобы целиком лечь в круг игрока.
    /// </summary>
    public struct MiniBossFrame
    {
        public Vector2 O;
        public float Deg, S;

        public MiniBossFrame(Vector2 o, float deg, float s) { O = o; Deg = deg; S = s; }

        /// <summary>Точка холста → (u, v) рыбы.</summary>
        public Vector2 ToL(Vector2 p) => Sdf.Rot(p - O, -Deg) / S;
        /// <summary>(u, v) рыбы → точка холста.</summary>
        public Vector2 ToW(Vector2 l) => O + Sdf.Rot(l * S, Deg);

        /// <summary>Рамка на холсте вокруг прямоугольника (u, v).</summary>
        public Rect Box(float u0, float v0, float u1, float v1)
        {
            Vector2 a = ToW(new Vector2(u0, v0)), b = ToW(new Vector2(u1, v0)), c = ToW(new Vector2(u0, v1)), d = ToW(new Vector2(u1, v1));
            return Rect.MinMaxRect(Mathf.Min(Mathf.Min(a.x, b.x), Mathf.Min(c.x, d.x)) - 0.02f,
                                   Mathf.Min(Mathf.Min(a.y, b.y), Mathf.Min(c.y, d.y)) - 0.02f,
                                   Mathf.Max(Mathf.Max(a.x, b.x), Mathf.Max(c.x, d.x)) + 0.02f,
                                   Mathf.Max(Mathf.Max(a.y, b.y), Mathf.Max(c.y, d.y)) + 0.02f);
        }
    }

    /// <summary>
    /// Как стоят мини-боссы — одно место на рисунок (SpriteFactoryMiniBoss*) и на
    /// живость (<see cref="MiniBossRigs"/>).
    ///
    /// **Вперёд — это вверх.** Ни пузырёк, ни страж при движении не поворачиваются
    /// (BubbleController, GuardianOrb: вращается только начинка обычных скинов), и
    /// витрина рисует ту же сетку без поворота. Значит, куда смотрит рисунок, туда
    /// мини-босс и смотрит везде: в игре, в карточке, в превью, в «Показать эффекты»,
    /// «Примерить» и «Сравнить». Пузырёк всплывает вверх — нос, голова и остриё
    /// смотрят вверх.
    ///
    /// Раньше рыбы боком (рыба-меч, пиранья, акула) и голова змея были наклонены на
    /// 40–52° вправо, и одни и те же углы стояли отдельно в рисунке и в живости.
    /// </summary>
    public static class MiniBossPose
    {
        /// <summary>Направление «вперёд», градусы от оси x: вверх.</summary>
        public const float Forward = 90f;

        public static readonly MiniBossFrame Swordfish = new MiniBossFrame(new Vector2(0f, -0.02f), Forward, 0.9f);
        public static readonly MiniBossFrame Piranha = new MiniBossFrame(new Vector2(0f, 0.1f), Forward, 0.92f);
        public static readonly MiniBossFrame Shark = new MiniBossFrame(new Vector2(0f, 0.04f), Forward, 0.9f);

        /// <summary>Голова змея: центр, «вперёд» и масштаб — головой вверх над изгибом тела.</summary>
        public static readonly MiniBossFrame SerpentHead = new MiniBossFrame(new Vector2(0.02f, 0.5f), Forward, 1.25f);
    }
}
