using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Разрешённый вид одной половины пары: всё, что нужно, чтобы её нарисовать.
    ///
    /// Собирается из данных скина один раз и дальше только читается. Игра и
    /// витрина получают один и тот же объект, поэтому показать разное физически
    /// не могут.
    /// </summary>
    public struct SkinLook
    {
        /// <summary>
        /// Оболочка: силуэт стихии. У трофея — неподвижный кадр мини-босса (справочник,
        /// запасной показ); живой мини-босс рисуется сеткой по <see cref="MiniBoss"/>.
        /// </summary>
        public Sprite Shell;
        /// <summary>Начинка: материал, который течёт под стеклом. null у стекла.</summary>
        public Sprite Core;
        /// <summary>Оттенок скина.</summary>
        public Color Tint;

        /// <summary>Движение, след и финишер — из таблицы эффектов скина.</summary>
        public SkinFxDef Fx;
        /// <summary>Аура, удар, разрушение, статус — из материала или босса.</summary>
        public MaterialFxDef Material;
        /// <summary>Стихия: форма, вибрация, звук.</summary>
        public ArchetypeDef Archetype;

        public Rarity Rarity;

        /// <summary>
        /// Трофей с босса: вместо оболочки — живой мини-босс (<see cref="MiniBossView"/>,
        /// <see cref="MiniBossGraphic"/>), эффекты — в теме босса (<see cref="BossFx"/>).
        /// </summary>
        public bool IsBoss;
        /// <summary>Какой мини-босс рисовать. Смысл имеет только при <see cref="IsBoss"/>.</summary>
        public MiniBossKey MiniBoss;
        /// <summary>Чей трофей. Смысл имеет только при <see cref="IsBoss"/>.</summary>
        public BossKind Boss;
        /// <summary>Паспорт боссового скина — всё о нём в одной строке <see cref="BossSkinTable"/>.</summary>
        public BossSkinDef BossDef => BossSkinTable.Of(Boss);

        /// <summary>Множитель размера рисунка от стихии. ±10 %, хитбокса не касается.</summary>
        public float VisualScale => Archetype.VisualScale;

        /// <summary>
        /// Цвет оболочки. Под начинкой стекло не красится: цвет уже положен
        /// материалом, и второй слой краски глушит сам материал.
        /// </summary>
        public Color ShellColor => IsBoss ? Color.white : Core != null ? Color.white : Tint;
    }

    /// <summary>
    /// Единственное место, где скин превращается в набор слоёв для отрисовки.
    ///
    /// **Зачем понадобилось.** Путей отрисовки было два. Игра строила вид в
    /// <c>GameManager.ApplySkins</c>, витрина — в <c>UIManager.RefreshPreview</c>, и
    /// они разошлись: витрина брала материал выбранного скина, а игра выдавала
    /// обеим половинкам материал ПУЗЫРЬКА. У игрока со стандартным пузырьком
    /// «Пена» материал стеклянный, то есть отсутствует, — и любой купленный страж
    /// в бою оставался без начинки, хотя в магазине она была видна. Два пути
    /// сошлись бы снова только случайно.
    ///
    /// **Как теперь.** <see cref="Resolve"/> — одна функция на всё: игру, витрину,
    /// «Примерить», «Сравнить» и карточки. Отдельных упрощённых копий для магазина
    /// нет, и завести их нельзя, не пройдя мимо этого класса, — а это проверяет
    /// Tools/audit.py.
    ///
    /// **Что чем управляет.** Половинки отвечают за разное, и это теперь честно
    /// написано и в коде, и на карточке в магазине:
    ///
    /// * **Пузырёк** — свой силуэт, свой материал, своё движение, свой след, своя
    ///   аура и эффект разрушения препятствия. Он — то, что игрок бережёт, и
    ///   смотрит он на него весь заплыв.
    /// * **Страж** — свой силуэт, свой материал, своё движение, свой след, своя
    ///   аура и ВСЁ, что происходит в момент удара: эффект удара, звук, вибрация,
    ///   статус на препятствии. Он единственный, кто по препятствиям бьёт, и
    ///   брать удар от пузырька было ошибкой: магазин на вкладке «Шарик» обещал
    ///   стихию стража, а в бою работала стихия пузырька.
    /// </summary>
    public static class SkinVisuals
    {
        /// <summary>
        /// Собрать вид половины.
        /// </summary>
        /// <param name="skin">Скин из каталога.</param>
        /// <param name="guardian">Правда — это страж, ложь — пузырёк.</param>
        /// <param name="index">
        /// Номер в каталоге. Нужен таблице эффектов: движение и след заданы по
        /// номеру скина, а не по материалу.
        /// </param>
        public static SkinLook Resolve(SkinDef skin, bool guardian, int index)
        {
            // У трофейных скинов материала нет — у них свой рисунок самого босса,
            // поэтому аура, удар и разрушение берутся по боссу, а не по стеклу.
            bool boss = skin.Kind == UnlockKind.Boss;
            MaterialFxDef material = boss
                ? MaterialFxTable.OfBoss(skin.UnlockBoss)
                : MaterialFxTable.Of(skin.Material);

            return new SkinLook
            {
                Shell = guardian ? SpriteFactory.GuardianShell(skin) : SpriteFactory.BubbleShell(skin),
                // Трофей — мини-босс целиком: у него нет стекла и начинки под ним,
                // весь вид — рисунок босса (SpriteFactory.MiniBoss).
                Core = boss ? null : SpriteFactory.MaterialCore(skin.Material),
                Tint = skin.Tint,
                Fx = SkinFxTable.Of(index),
                Material = material,
                Archetype = ArchetypeTable.Of(material.Status),
                Rarity = skin.Rarity,
                IsBoss = boss,
                Boss = skin.UnlockBoss,
                MiniBoss = new MiniBossKey(skin.UnlockBoss, guardian),
            };
        }

        /// <summary>Вид выбранного пузырька.</summary>
        public static SkinLook Bubble()
        {
            int i = SkinStore.SelectedBubble;
            return Resolve(SkinStore.Bubble[i], guardian: false, index: i);
        }

        /// <summary>Вид выбранного стража.</summary>
        public static SkinLook Guardian()
        {
            int i = SkinStore.SelectedGuardian;
            return Resolve(SkinStore.Guardian[i], guardian: true, index: i);
        }

        /// <summary>
        /// Вид половины по номеру в её каталоге — витрине и карточкам.
        /// </summary>
        public static SkinLook At(bool guardian, int index)
        {
            var list = guardian ? SkinStore.Guardian : SkinStore.Bubble;
            index = Mathf.Clamp(index, 0, list.Length - 1);
            return Resolve(list[index], guardian, index);
        }

        /// <summary>
        /// Скорость вращения начинки, градусов в секунду.
        ///
        /// Одна на всё: в игре пузырёк крутил материал со скоростью 14, страж —
        /// 20, а витрина обоих со скоростью 14. Начинка одного и того же скина
        /// вращалась с разной скоростью в трёх местах.
        /// </summary>
        public const float CoreSpin = 14f;
    }
}
