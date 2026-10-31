namespace BubbleRise
{
    /// <summary>
    /// Раскладка экрана наград — одной таблицей, как у магазина.
    ///
    /// Причина та же и выучена дорого: <c>Anchor</c> вешает блок ВНИЗ от указанного
    /// Y, и пока числа разбросаны по коду построения, они читаются как «по центру».
    /// Магазин на этом разъехался целиком — вкладки оказались внутри витрины,
    /// «Назад» легла на список. Здесь экранов четыре, и разъехаться им было бы ещё
    /// проще.
    ///
    /// Поэтому каждая полоса объявлена парой «верх + высота», соседи стыкуются по
    /// построению, а Tools/audit.py проверяет, что полосы не пересекаются, и падает,
    /// если подвинуть одно число и забыть про другое.
    ///
    /// Единицы — опорные единицы холста (ширина 500, высота 1010), те же, что в
    /// <see cref="ShopLayout"/>: экран наград открывается оттуда же и должен
    /// выглядеть его родственником, а не гостем.
    /// </summary>
    public static class RewardsLayout
    {
        public const float ContentWidth = 500f;
        public const float ContentHeight = 1010f;
        public const float SidePadding = 12f;

        // --- общий каркас ----------------------------------------------------
        public const float HeaderY = -16f;
        public const float HeaderH = 52f;

        public const float TabsY = -80f;
        public const float TabsH = 50f;

        /// <summary>Окно страницы: внутри него живёт одна из четырёх вкладок.</summary>
        public const float PageY = -142f;
        public const float PageH = 744f;

        public const float BackBottom = 40f;
        public const float BackH = 80f;

        /// <summary>Нижний край страницы в системе координат полос.</summary>
        public static float PageBottom => PageY - PageH;

        /// <summary>Верхний край кнопки «Назад» в той же системе.</summary>
        public static float BackTop => -(ContentHeight - BackBottom - BackH);

        /// <summary>Четыре вкладки в ряд, с зазором по краям.</summary>
        public const int TabCount = 4;
        public const float TabW = 118f;
        public const float TabStep = 120f;

        // --- страница «Подарки»: сундук сверху, календарь под ним ------------
        public const float ChestPanelY = 0f;
        public const float ChestPanelH = 236f;

        public const float CalendarPanelY = -248f;
        public const float CalendarPanelH = 460f;

        /// <summary>Клетка календаря и шаг сетки: четыре в первом ряду, три во втором.</summary>
        public const float CellW = 104f;
        public const float CellH = 126f;
        public const float CellStepX = 112f;
        public const int CellsTopRow = 4;

        public const float CalendarTitleY = -12f;
        public const float CalendarTitleH = 28f;
        public const float CalendarRow1Y = -46f;
        public const float CalendarRow2Y = -182f;
        public const float CalendarStreakY = -318f;
        public const float CalendarStreakH = 30f;
        public const float CalendarClaimY = -352f;
        public const float CalendarClaimH = 76f;

        // --- страница «Задания»: прокручиваемый список -----------------------
        public const float QuestCardH = 104f;
        public const float QuestCardStep = 114f;
        public const float QuestSectionH = 34f;
        public const float QuestCardW = 476f;

        // --- страница «Колесо» ----------------------------------------------
        public const float WheelSize = 324f;
        public const float WheelY = -28f;
        public const float WheelSpinsY = -366f;
        public const float WheelSpinsH = 34f;
        public const float WheelButtonY = -406f;
        public const float WheelButtonH = 88f;
        public const float WheelResultY = -502f;
        public const float WheelResultH = 40f;
        public const float WheelHintY = -548f;
        public const float WheelHintH = 60f;

        // --- страница «Достижения»: прокручиваемый список --------------------
        public const float AchCardH = 112f;
        public const float AchCardStep = 122f;
        public const float AchCardW = 476f;

        /// <summary>Высота полосы прогресса — одна на задания и достижения.</summary>
        public const float BarH = 14f;

        // --- внутренности карточки ------------------------------------------
        //
        // Кнопка действия прижата к правому нижнему углу, полоса прогресса идёт
        // слева на той же высоте. Пока ширина полосы жила числом в коде сборки,
        // она у достижений кончалась на 348 при кнопке, начинающейся на 328, —
        // полоса уходила под кнопку. Числа здесь, и audit.py проверяет, что
        // колонки не встречаются.

        /// <summary>Кнопка «Забрать»: ширина и отступ от правого края карточки.</summary>
        public const float ActionW = 132f;
        public const float ActionMargin = 16f;

        /// <summary>Минимальный просвет между левой колонкой и кнопкой.</summary>
        public const float ActionGap = 10f;

        /// <summary>Задание: текст и полоса начинаются у самого края.</summary>
        public const float QuestTextLeft = 16f;
        public const float QuestBarW = 300f;

        /// <summary>Достижение: слева медаль, поэтому колонка начинается правее.</summary>
        public const float AchTextLeft = 98f;
        public const float AchBarW = 216f;

        /// <summary>Левый край кнопки действия — в координатах от левого края карточки.</summary>
        public static float ActionLeft(float cardWidth) => cardWidth - ActionMargin - ActionW;
    }
}
