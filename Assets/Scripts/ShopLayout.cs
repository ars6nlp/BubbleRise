namespace BubbleRise
{
    /// <summary>
    /// Раскладка экрана магазина — одной таблицей.
    ///
    /// Вынесена из кода построения не ради красоты. Экран собирается вручную,
    /// абсолютными координатами, и опорная точка у <c>Anchor</c> верхняя: блок
    /// висит ВНИЗ от указанного Y. Пока числа были разбросаны по методу, это
    /// понималось как «по центру», и экран разъехался: вкладки редкостей оказались
    /// внутри панели предпросмотра, кнопка «Назад» легла на листалку, а подписи
    /// наложились друг на друга.
    ///
    /// Теперь каждая полоса объявлена парой «верх + высота», полосы не
    /// пересекаются по построению, а Tools/audit.py это проверяет и падает, если
    /// кто-то подвинет одно число, забыв про соседнее.
    ///
    /// Все значения — в опорных единицах холста (ширина 500, высота содержимого
    /// 1010), то есть относительные: на узком телефоне и на мониторе раскладка
    /// одна и та же, меняется только масштаб.
    /// </summary>
    public static class ShopLayout
    {
        public const float ContentWidth = 500f;
        public const float ContentHeight = 1010f;
        public const float SidePadding = 12f;

        // --- полосы сверху вниз: Y верхнего края и высота --------------------
        public const float HeaderY = -16f;
        public const float HeaderH = 52f;

        /// <summary>Вкладки «Пузырь» и «Шарик». Крупные: это главный выбор экрана.</summary>
        public const float TypeTabsY = -84f;
        public const float TypeTabsH = 58f;

        /// <summary>
        /// Строка пояснения под вкладками.
        ///
        /// Отдельная полоса, а не вторая строка внутри кнопки: пояснение меняется
        /// вместе с вкладкой, и держать его внутри активной кнопки значило бы
        /// прятать половину смысла за нажатием.
        /// </summary>
        public const float TabHintY = -148f;
        public const float TabHintH = 26f;

        /// <summary>Панель выбранного: шарик, имя, редкость, три метки, две кнопки.</summary>
        public const float PreviewY = -180f;
        public const float PreviewH = 248f;

        /// <summary>
        /// Строка редкостей. ВСЕГДА видима — это главный способ сузить каталог из
        /// тридцати шести скинов, и прятать его за кнопкой оказалось ошибкой:
        /// выпадающая панель накрывала верхний ряд карточек, а до самих фильтров
        /// нужно было сначала догадаться.
        /// </summary>
        public const float RarityY = -434f;
        public const float RarityH = 44f;

        /// <summary>
        /// Сортировка — одной кнопкой сразу под строкой редкостей.
        ///
        /// Просили справа от неё, но справа помещается только значок шириной в
        /// полсотни точек, а значок, который нечем расшифровать, хуже строки
        /// текста. Отдельная тонкая полоса стоит тридцати двух точек списка и
        /// зато всегда говорит, как именно отсортировано.
        /// </summary>
        public const float SortY = -482f;
        public const float SortH = 32f;

        /// <summary>Окно списка: внутри него прокрутка, карточки за край не вылезают.</summary>
        public const float ListY = -520f;
        public const float ListH = 352f;

        /// <summary>«Назад» живёт у нижнего края и в полосу списка не заходит.</summary>
        public const float BackBottom = 40f;
        public const float BackH = 80f;

        /// <summary>
        /// Чипы редкости: шесть в один ряд по ширине холста.
        ///
        /// 6 × 80 = 480 при ширине 500 — помещаются всегда, на любом из трёх
        /// соотношений сторон, потому что ширина холста считается от высоты и
        /// самая узкая (19.5:9) даёт 591. Ряд всё равно лежит в горизонтальной
        /// прокрутке: если однажды добавится седьмая редкость, он не сломается,
        /// а станет прокручиваемым.
        /// </summary>
        public const int FilterCount = 6;
        public const float FilterChipW = 76f;
        public const float FilterChipStepX = 80f;

        /// <summary>Нижний запас в списке, чтобы последний ряд доезжал до конца.</summary>
        public const float ListBottomPad = 24f;

        // --- карточка --------------------------------------------------------
        public const int Columns = 2;
        public const float CardW = 228f;
        public const float CardH = 150f;
        public const float CardStepX = 238f;
        public const float CardStepY = 162f;

        // --- панель выбранного скина ----------------------------------------
        //
        // Две колонки. Левая — шарики: выбранный и, по кнопке «Сравнить», надетый
        // сейчас. Правая — всё про скин. Колонки не пересекаются по построению:
        // левая кончается на 140 от левого края панели, правая начинается со 168.
        //
        // Напоминание, которое уже стоило одной правки: Anchor ставит pivot.x в
        // 0.5, поэтому X — это ЦЕНТР блока, а не его левый край. Метки
        // характеристик, поставленные по левому краю, легли прямо на шарик.

        /// <summary>Центр левой колонки от левого края панели.</summary>
        public const float PreviewLeftX = 84f;
        /// <summary>Центр правой колонки и её ширина.</summary>
        public const float PreviewRightX = 314f;
        public const float PreviewRightW = 292f;

        public const float PreviewBallY = -16f;
        public const float PreviewBallSize = 112f;
        public const float PreviewCompareY = -134f;
        public const float PreviewCompareSize = 68f;

        public const float PreviewNameY = -12f;
        public const float PreviewRarityY = -40f;
        public const float PreviewStatusY = -60f;
        public const float PreviewTraitsY = -88f;
        /// <summary>Шаг между тремя метками характеристик.</summary>
        public const float PreviewTraitStep = 18f;
        public const float PreviewActionY = -162f;
        public const float PreviewActionH = 42f;
        public const float PreviewButtonsY = -208f;
        public const float PreviewButtonsH = 36f;

        /// <summary>Нижний край полосы списка в той же системе координат, что и Y полос.</summary>
        public static float ListBottom => ListY - ListH;

        /// <summary>Верхний край кнопки «Назад» в той же системе координат.</summary>
        public static float BackTop => -(ContentHeight - BackBottom - BackH);
    }
}
