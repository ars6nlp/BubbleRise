namespace BubbleRise
{
    /// <summary>Порядок отрисовки спрайтов (все в Default sorting layer).</summary>
    public static class SortingOrders
    {
        public const int Background = -100;
        public const int BackgroundBubbles = -90;
        public const int Silhouette = -95;
        public const int Plankton = -85;
        public const int LightShaft = -80;
        public const int BubbleVent = -70;
        public const int Scenery = -60;
        public const int ZoneWall = -45;
        public const int ZoneDecor = -30;
        public const int BubbleTrail = 8;
        public const int Obstacle = 0;
        public const int Formation = 1;
        public const int Fish = 2;
        public const int BossAura = 4;
        public const int Boss = 5;

        /// <summary>
        /// Слои грозовой медузы, снизу вверх.
        ///
        /// Своя лестница нужна потому, что раньше слои делили номера с чужими:
        /// дымка стояла на 3 вместе с бахромой, рукава — на 4 вместе с кольцами
        /// поля. У спрайтов с равным порядком очерёдность отрисовки не
        /// определена, и какой из них окажется сверху, от кадра к кадру могло
        /// меняться. [КОД]
        /// </summary>
        public const int JellyHaze = -5;
        /// <summary>Кольца поля отталкивания: позади всего тела.</summary>
        public const int JellyField = -3;
        public const int JellyFringe = -4;
        public const int JellyArm = 3;
        public const int JellyGlow = 4;
        public const int JellyRipple = 6;   // поверх купола, под глазом
        public const int JellyEye = 7;
        public const int Projectile = 8;
        public const int Bubble = 10;
        public const int Guardian = 20;
        public const int Particle = 30;

        /// <summary>
        /// Монеты. Выше частиц и выше всего игрового слоя: они были на Obstacle+2 и
        /// уходили за рыб, пузырёк и стража — выглядело так, будто награда падает
        /// куда-то за дорожку, по которой идёт игра.
        /// </summary>
        public const int Coin = 32;
        // Чернила лежат поверх всего мира, но под интерфейсом: HUD рисуется
        // экранным канвасом и в этой шкале не участвует, поэтому полоса жизней
        // и метры остаются читаемыми даже при полностью залитом экране.
        public const int Ink = 40;
        /// <summary>Метки, которые обязаны быть видны сквозь чернила.</summary>
        public const int InkMarker = 45;
    }
}
