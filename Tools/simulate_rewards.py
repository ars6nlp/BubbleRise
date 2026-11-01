# -*- coding: utf-8 -*-
"""
Прогон системы наград по дням, вне Unity.

Повторяет правила RewardsHub.cs: созревание сундуков, серию входов, спасательный
пузырь и сброс календаря. Нужно затем, что эти правила проверяются только
временем — сутками и часами, — а ошибка в них видна игроку сразу и выглядит как
украденная серия.

    python3 Tools/simulate_rewards.py
"""

CHEST_HOURS = 1
CHEST_MAX = 3
RESCUE_EVERY = 7

# RewardTables.DailyCalendar, в монетах (Spin и Chest по объявленной цене)
CALENDAR = [15, 25, 8, 30, 35, 8, 90]


class State:
    def __init__(self):
        self.chest_anchor = None      # час, когда сундук в последний раз созрел
        self.chests = 0
        self.last_login = -1
        self.streak = 0
        self.claimed = False
        self.last_rescue = -1
        self.prev_visit = -1


def tick_chests(st, hour):
    if st.chest_anchor is None:
        st.chest_anchor = hour
        st.chests = 1
        return
    passed = hour - st.chest_anchor
    if passed < CHEST_HOURS:
        return
    matured = passed // CHEST_HOURS
    st.chests = min(CHEST_MAX, st.chests + matured)
    st.chest_anchor += matured * CHEST_HOURS


def tick_login(st, day):
    if st.last_login == day:
        return None
    note = None
    if st.last_login < 0:
        st.streak = 0
    else:
        gap = day - st.last_login
        if gap == 1:
            pass
        elif gap == 2 and (st.last_rescue < 0 or day - st.last_rescue >= RESCUE_EVERY):
            st.last_rescue = day
            note = "пузырь"
        else:
            st.streak = 0
            note = "серия сброшена"
    st.prev_visit = st.last_login
    st.last_login = day
    st.claimed = False
    return note


def claim_daily(st):
    if st.claimed:
        return 0
    coins = CALENDAR[st.streak % len(CALENDAR)]
    st.claimed = True
    st.streak += 1
    return coins


def run(title, schedule, hours_per_visit=4):
    """schedule — список дней, когда игрок заходит."""
    st = State()
    total = 0
    print("\n--- %s ---" % title)
    for day in schedule:
        hour = day * 24 + 19          # заходит вечером
        tick_chests(st, hour)
        note = tick_login(st, day)

        coins = claim_daily(st)
        total += coins

        # за вечер успевает забрать накопленные и те, что созреют за сеанс
        opened = st.chests
        st.chests = 0
        for extra in range(hours_per_visit):
            tick_chests(st, hour + extra + 1)
        opened += st.chests
        st.chests = 0

        print("день %2d  серия %2d  календарь %3d  сундуков %d%s"
              % (day, st.streak, coins, opened, "  [%s]" % note if note else ""))
    print("итого с календаря: %d монет" % total)
    return st


def main():
    # 1. Ежедневный игрок: серия растёт, календарь идёт по кругу.
    st = run("каждый день, две недели", list(range(0, 14)))
    assert st.streak == 14, st.streak

    # 2. Один пропуск: спасательный пузырь обязан сохранить серию.
    st = run("пропустил один день", [0, 1, 2, 4, 5])
    assert st.streak == 5, "пузырь не спас серию: %d" % st.streak

    # 3. Второй пропуск внутри недели: пузырь уже потрачен, серия рвётся.
    st = run("два пропуска за неделю", [0, 1, 2, 4, 6, 7])
    assert st.streak < 6, "серия не оборвалась: %d" % st.streak

    # 4. Долгое отсутствие: сундуки копятся, но не бесконечно.
    st = State()
    tick_chests(st, 0)
    tick_chests(st, 24 * 30)
    assert st.chests == CHEST_MAX, st.chests
    print("\n--- месяц без игры ---")
    print("сундуков накоплено: %d (потолок %d)" % (st.chests, CHEST_MAX))

    # 5. Неделя подряд: средняя награда календаря должна совпасть с расчётом
    #    в RewardTables.
    avg = sum(CALENDAR) / float(len(CALENDAR))
    print("\nсреднее по календарю: %.1f монет в день" % avg)
    assert 28 <= avg <= 32, avg

    print("\nвсе проверки прошли")


if __name__ == "__main__":
    main()
