using System;
using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Устойчивый идентификатор босса. Нужен там, где ссылка на объект не годится:
    /// в сохранении побеждённых боссов и в условиях открытия скинов.
    ///
    /// Значения привязаны к именам, а не к порядку в массиве: перестановка боссов
    /// в GameBootstrap не должна сбрасывать игроку открытые скины.
    /// </summary>
    public enum BossKind
    {
        Jelly = 0,
        Serpent = 1,
        Octopus = 2,
        Swordfish = 3,
        Crab = 4,
        Ink = 5,
        Piranha = 6,
        Turtle = 7,
        Shark = 8,
        Manta = 9,
    }

    /// <summary>
    /// Общий контракт боссов. Боссы выходят по очереди и проверяют разные навыки:
    /// «Страж глубин» — отражение снарядов, «Змей бездны» — позиционирование стража
    /// на линии рывка. GameManager работает с ними одинаково.
    /// </summary>
    public interface IBoss
    {
        /// <summary>Имя для баннера и полосы здоровья.</summary>
        string DisplayName { get; }

        /// <summary>Кто это — для сохранений и скинов, которые падают с боссов.</summary>
        BossKind Kind { get; }

        int Hp { get; }
        int MaxHp { get; }

        /// <summary>Боссу нанесён урон (позиция для эффекта).</summary>
        event Action<Vector2> Damaged;
        /// <summary>Босс уничтожен.</summary>
        event Action<Vector2> Defeated;
        /// <summary>Босс достал пузырёк.</summary>
        event Action<Vector2> BubbleHit;
        /// <summary>Страж успешно принял атаку на себя.</summary>
        event Action<Vector2> GuardianBlocked;

        void Spawn();
        void Tick(float dt);
        void Despawn();

        /// <summary>
        /// Прервать текущую атаку, не трогая здоровье и не убирая босса с арены.
        ///
        /// Нужно ровно для одного случая: игрок погиб и вернулся за доп. жизнь. Бой
        /// продолжается с того же места, но всё, что босс начал ещё до смерти —
        /// летящие снаряды, зажатый в клешне страж, недоигранный рывок, — должно
        /// исчезнуть. Иначе возрождённый игрок получает удар в первый же кадр, не
        /// успев ничего сделать.
        /// </summary>
        void Interrupt();
    }
}
