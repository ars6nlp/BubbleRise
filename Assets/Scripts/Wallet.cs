using System;
using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Монеты: единственная валюта игры.
    ///
    /// Зарабатываются ровно одним способом — за уничтоженное препятствие. Ни за
    /// метры, ни за время, ни за дожитый до конца заплыв монет не дают. Это не
    /// прихоть: плата за дистанцию превратила бы магазин в счётчик терпения, а
    /// плата за уничтожение платит за то единственное действие, которым игрок в
    /// этой игре вообще управляет, — за вовремя подставленного стража.
    ///
    /// Класс намеренно статический, как <see cref="SkinStore"/> рядом: баланс
    /// нужен и в бою, и в меню, и в магазине, и заводить ради него объект в сцене
    /// значило бы искать его из трёх мест.
    ///
    /// Правда о балансе живёт в PlayerPrefs, а не в поле: так её подхватывает
    /// облачное сохранение Яндекса через <see cref="SaveData"/>, не зная про
    /// кошелёк ничего.
    /// </summary>
    public static class Wallet
    {
        /// <summary>Баланс изменился — счётчикам на экране пора обновиться.</summary>
        public static event Action<int> Changed;

        /// <summary>
        /// Сколько монет заработано за текущий заплыв. Нужен только экрану итогов,
        /// поэтому в хранилище не едет: после смерти цифра своё отработала.
        /// </summary>
        public static int EarnedThisRun { get; private set; }

        public static int Balance
        {
            get => Mathf.Max(0, PlayerPrefs.GetInt(SaveData.CoinsKey, 0));
            private set
            {
                PlayerPrefs.SetInt(SaveData.CoinsKey, Mathf.Max(0, value));
                Changed?.Invoke(Balance);
            }
        }

        /// <summary>
        /// Начислить за уничтоженное препятствие.
        ///
        /// На диск здесь не пишем специально: за партию препятствий бывают сотни, а
        /// PlayerPrefs.Save() в WebGL синхронизирует IndexedDB и стоит заметных
        /// миллисекунд. Число держится в памяти PlayerPrefs, а на диск уходит в
        /// конце заплыва — ровно там же, где сохраняется остальной профиль.
        /// </summary>
        public static void Earn(int amount)
        {
            if (amount <= 0) return;

            EarnedThisRun += amount;
            Balance = Balance + amount;
        }

        /// <summary>
        /// Начислить из системы возвращения: сундук, календарь, квест, колесо,
        /// достижение.
        ///
        /// Отдельный метод, а не вызов <see cref="Earn"/>, по двум причинам. Первая:
        /// «заработано за заплыв» на экране итогов должно означать именно добычу в
        /// бою — подарок из сундука туда попадать не должен, иначе цифра перестанет
        /// что-либо значить. Вторая: награды редки и важны, их пишут на диск сразу,
        /// в отличие от начислений за препятствия.
        ///
        /// Разделение заодно делает правило экономики проверяемым: достаточно
        /// посмотреть, кто зовёт <see cref="Earn"/>, а кто — этот метод.
        /// </summary>
        public static void EarnFromReward(int amount)
        {
            if (amount <= 0) return;

            Balance = Balance + amount;
            PlayerPrefs.Save();
        }

        /// <summary>Списать за покупку. Возвращает false, если не хватило.</summary>
        public static bool Spend(int amount)
        {
            if (amount <= 0) return true;
            if (Balance < amount) return false;

            Balance = Balance - amount;

            // Покупка — редкое и важное событие, в отличие от начисления: её теряют
            // болезненнее всего, поэтому пишем на диск сразу.
            PlayerPrefs.Save();
            return true;
        }

        public static bool CanAfford(int price) => Balance >= price;

        /// <summary>Начало заплыва: счётчик заработанного обнуляется, баланс — нет.</summary>
        public static void BeginRun() => EarnedThisRun = 0;

        /// <summary>
        /// Конец заплыва: сбрасываем накопленное на диск. Отдельный метод, а не
        /// Save() внутри Earn, по той же причине, по которой счётчики отбитого не
        /// сохраняются на каждый удар.
        /// </summary>
        public static void FlushRun() => PlayerPrefs.Save();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Отладка: выдать монет, чтобы проверять магазин без фарма.</summary>
        public static void DevGrant(int amount)
        {
            Balance = Balance + Mathf.Max(0, amount);
            PlayerPrefs.Save();
        }
#endif
    }
}
