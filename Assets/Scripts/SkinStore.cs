using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Какой рисунок у скина. Classic — обычный пузырёк или страж, остальное —
    /// трофеи с боссов: каждый повторяет того, с кого упал.
    /// </summary>
    /// <summary>
    /// Чем открывается скин. Раньше условий было два, и различались они по знаку
    /// глубины — трюк, который перестал работать, как только условий стало пять.
    /// </summary>
    public enum UnlockKind
    {
        /// <summary>Рекорд глубины.</summary>
        Depth,
        /// <summary>Победа над конкретным боссом.</summary>
        Boss,
        /// <summary>Столько-то рыб, отбитых стражем за всё время.</summary>
        Fish,
        /// <summary>Столько-то отбитых обломков.</summary>
        Debris,
        /// <summary>Открыты все остальные скины — награда за полную коллекцию.</summary>
        Mastery,
        /// <summary>Куплен за монеты.</summary>
        Purchase,
    }

    public enum SkinArt
    {
        Classic = 0,
        Jelly, Serpent, Octopus, Swordfish, Crab, Ink, Piranha, Turtle, Shark, Manta,
        /// <summary>Золотая звезда — награда за полную коллекцию.</summary>
        Star,
    }

    /// <summary>
    /// Один скин: имя, рисунок, оттенок и условие открытия.
    ///
    /// Условий два, и они взаимоисключающие: либо рекорд глубины, либо победа над
    /// конкретным боссом. Различаются по знаку <see cref="UnlockDepth"/> — держать
    /// ещё одно поле-признак ради этого было бы лишним.
    /// </summary>
    public struct SkinDef
    {
        /// <summary>Ключ локализации, а не готовая строка: имя резолвится при показе.</summary>
        public string NameKey;
        public Color Tint;
        public SkinArt Art;
        public UnlockKind Kind;
        /// <summary>Порог: метры для Depth, число отбитых для Fish и Debris.</summary>
        public int Amount;
        public BossKind UnlockBoss;
        /// <summary>Редкость. У открываемых достижениями тоже есть: она красит рамку
        /// и решает, какой эффект показывать при разрушении препятствия.</summary>
        public Rarity Rarity;
        /// <summary>Цена в монетах. Ноль у всего, что не продаётся.</summary>
        public int Price;
        /// <summary>Материал — то, что видно внутри шарика. Glass значит «только стекло».</summary>
        public SkinMaterial Material;

        public bool ByBoss => Kind == UnlockKind.Boss;
        public bool ForSale => Kind == UnlockKind.Purchase;
        public string Name => Loc.Get(NameKey);

        /// <summary>Скин за рекорд глубины. Рисунок обычный, отличие только в цвете.</summary>
        public static SkinDef Depth(string nameKey, Color tint, int depth, Rarity rarity = Rarity.Common,
                                    SkinMaterial material = SkinMaterial.Glass)
        {
            return new SkinDef
            {
                NameKey = nameKey,
                Tint = tint,
                Art = SkinArt.Classic,
                Kind = UnlockKind.Depth,
                Amount = depth,
                UnlockBoss = BossKind.Jelly,
                Rarity = rarity,
                Material = material,
            };
        }

        /// <summary>Скин за отбитое: столько-то рыб или обломков, принятых стражем.</summary>
        public static SkinDef Tally(string nameKey, Color tint, UnlockKind kind, int amount,
                                    Rarity rarity = Rarity.Rare,
                                    SkinMaterial material = SkinMaterial.Glass)
        {
            return new SkinDef
            {
                NameKey = nameKey,
                Tint = tint,
                Art = SkinArt.Classic,
                Kind = kind,
                Amount = amount,
                UnlockBoss = BossKind.Jelly,
                Rarity = rarity,
                Material = material,
            };
        }

        /// <summary>
        /// Награда за полную коллекцию: открывается, когда открыто всё остальное.
        /// Единственный скин, который нельзя получить, гоняясь за чем-то одним.
        /// </summary>
        public static SkinDef Mastery(string nameKey, SkinArt art)
        {
            return new SkinDef
            {
                NameKey = nameKey,
                Tint = Color.white,
                Art = art,
                Kind = UnlockKind.Mastery,
                Amount = 0,
                UnlockBoss = BossKind.Jelly,
                Rarity = Rarity.Legendary,
                Material = SkinMaterial.Starlight,
            };
        }

        /// <summary>
        /// Скин за монеты. Рисунок обычный: платное отличается цветом и эффектом, а
        /// не формой — иначе покупка давала бы ещё и другой силуэт, а силуэт в этой
        /// игре обязан совпадать с кругом столкновения.
        /// </summary>
        public static SkinDef Buy(string nameKey, Color tint, Rarity rarity, int price,
                                  SkinMaterial material)
        {
            return new SkinDef
            {
                NameKey = nameKey,
                Tint = tint,
                Art = SkinArt.Classic,
                Kind = UnlockKind.Purchase,
                Amount = 0,
                UnlockBoss = BossKind.Jelly,
                Rarity = rarity,
                Price = price,
                Material = material,
            };
        }

        /// <summary>
        /// Трофей с босса. Оттенок белый: у такого скина свой рисунок со своими
        /// цветами, и подкрашивать его сверху значило бы их портить.
        /// </summary>
        public static SkinDef Trophy(string nameKey, BossKind boss, SkinArt art, int price)
        {
            return new SkinDef
            {
                NameKey = nameKey,
                Tint = Color.white,
                Art = art,
                Kind = UnlockKind.Boss,
                Amount = 0,
                UnlockBoss = boss,
                Rarity = Rarity.Boss,
                Price = price,
            };
        }
    }

    /// <summary>
    /// Каталог скинов и выбор игрока.
    ///
    /// Скины двух родов и открываются по-разному:
    ///
    /// * **За глубину** — только оттенок обычного спрайта. Валюты в игре нет и
    ///   вводить её ради магазина не хочется, поэтому платит игрок той же цифрой,
    ///   за которую и так борется.
    /// * **Трофеи с боссов** — у каждого свой рисунок: победил краба, получил
    ///   пузырёк-крабика и стража-клешню. Это единственная награда в игре, которую
    ///   нельзя добыть просто упорством: нужно именно победить конкретного босса.
    ///
    /// Спрайты у трофеев тоже процедурные, так что и они не стоят ни байта в сборке.
    /// Силуэт у всех круглый — и у пузырька, и у стража столкновения круглые, и скин
    /// не имеет права врать про их размер.
    /// </summary>
    public static class SkinStore
    {
        const string BubbleKey = SaveData.SkinBubbleKey;
        const string GuardianKey = SaveData.SkinGuardianKey;

        public static readonly SkinDef[] Bubble =
        {
            SkinDef.Depth("skin.bubble.0", Palette.Hex("FFFFFF"),    0),
            SkinDef.Depth("skin.bubble.1", Palette.Hex("6BE88A"), 1000, Rarity.Common, SkinMaterial.Verdigris),
            SkinDef.Depth("skin.bubble.2", Palette.Hex("FF5B4A"), 5000, Rarity.Rare, SkinMaterial.Cinnabar),

            // Два скина за руки, а не за глубину: их нельзя получить, просто заплыв
            // далеко один раз. Считается всё отбитое за всё время, а не за партию.
            SkinDef.Tally("skin.bubble.3", Palette.Hex("2B2F36"), UnlockKind.Fish, 100, Rarity.Rare, SkinMaterial.Obsidian),
            SkinDef.Tally("skin.bubble.4", Palette.Hex("FFC83D"), UnlockKind.Debris, 100, Rarity.Rare, SkinMaterial.Brass),

            SkinDef.Trophy("skin.bubble.jelly",     BossKind.Jelly,     SkinArt.Jelly, 10000),
            SkinDef.Trophy("skin.bubble.serpent",   BossKind.Serpent,   SkinArt.Serpent, 10400),
            SkinDef.Trophy("skin.bubble.octopus",   BossKind.Octopus,   SkinArt.Octopus, 10800),
            SkinDef.Trophy("skin.bubble.swordfish", BossKind.Swordfish, SkinArt.Swordfish, 11200),
            SkinDef.Trophy("skin.bubble.crab",      BossKind.Crab,      SkinArt.Crab, 11600),
            SkinDef.Trophy("skin.bubble.ink",       BossKind.Ink,       SkinArt.Ink, 12000),
            SkinDef.Trophy("skin.bubble.piranha",   BossKind.Piranha,   SkinArt.Piranha, 12400),
            SkinDef.Trophy("skin.bubble.turtle",    BossKind.Turtle,    SkinArt.Turtle, 12800),
            SkinDef.Trophy("skin.bubble.shark",     BossKind.Shark,     SkinArt.Shark, 13200),
            SkinDef.Trophy("skin.bubble.manta",     BossKind.Manta,     SkinArt.Manta, 13600),

            SkinDef.Mastery("skin.bubble.star", SkinArt.Star),

            // === за монеты =====================================================
            // Порядок и редкость обязаны совпадать с SkinFxTable: эффект берётся по
            // тому же индексу, и перестановка строки здесь молча отдала бы скину
            // чужую анимацию. За соответствием следит Tools/audit.py.
            //
            // Ключ имени один на оба списка: это пара, покупается она целиком, и
            // называть половинки по-разному значило бы продавать игроку две вещи.
            SkinDef.Buy("skin.buy.coral",   Palette.Hex("FF9EB5"), Rarity.Common,   360, SkinMaterial.Coral),
            SkinDef.Buy("skin.buy.pearl",   Palette.Hex("FFF2E0"), Rarity.Common,   430, SkinMaterial.Pearl),
            SkinDef.Buy("skin.buy.silt",    Palette.Hex("8FA86B"), Rarity.Common,   500, SkinMaterial.Silt),
            SkinDef.Buy("skin.buy.sand",    Palette.Hex("E8D8A8"), Rarity.Common,   580, SkinMaterial.Sand),
            SkinDef.Buy("skin.buy.tide",    Palette.Hex("8FD8FF"), Rarity.Common,   680, SkinMaterial.Tide),

            SkinDef.Buy("skin.buy.frost",   Palette.Hex("DFF4FF"), Rarity.Rare,  1100, SkinMaterial.Ice),
            SkinDef.Buy("skin.buy.toxin",   Palette.Hex("A8FF5B"), Rarity.Rare,  1300, SkinMaterial.Toxin),
            SkinDef.Buy("skin.buy.neon",    Palette.Hex("FF5BE8"), Rarity.Rare,  1500, SkinMaterial.Neon),
            SkinDef.Buy("skin.buy.resin",   Palette.Hex("FFB45B"), Rarity.Rare,  1700, SkinMaterial.Resin),
            SkinDef.Buy("skin.buy.storm",   Palette.Hex("CFE4FF"), Rarity.Rare,  2000, SkinMaterial.Storm),

            SkinDef.Buy("skin.buy.ember",   Palette.Hex("FF7A3A"), Rarity.Epic,  2900, SkinMaterial.Ember),
            SkinDef.Buy("skin.buy.volt",    Palette.Hex("9ADFFF"), Rarity.Epic,  3400, SkinMaterial.Plasma),
            SkinDef.Buy("skin.buy.wraith",  Palette.Hex("CFE8E0"), Rarity.Epic,  3900, SkinMaterial.Wraith),
            SkinDef.Buy("skin.buy.prism",   Palette.Hex("FFFFFF"), Rarity.Epic,  4400, SkinMaterial.Prism),
            SkinDef.Buy("skin.buy.pixel",   Palette.Hex("5BFF9A"), Rarity.Epic,  5000, SkinMaterial.Pixel),

            SkinDef.Buy("skin.buy.nebula",  Palette.Hex("B88FFF"), Rarity.Legendary,  6200, SkinMaterial.Nebula),
            SkinDef.Buy("skin.buy.magma",   Palette.Hex("FF5B2E"), Rarity.Legendary,  7000, SkinMaterial.Magma),
            SkinDef.Buy("skin.buy.aurora",  Palette.Hex("8FFFD8"), Rarity.Legendary,  7700, SkinMaterial.Aurora),
            SkinDef.Buy("skin.buy.abyss",   Palette.Hex("6F5BD0"), Rarity.Legendary,  8500, SkinMaterial.Abyss),
            SkinDef.Buy("skin.buy.leviath", Palette.Hex("3FD8C8"), Rarity.Legendary,  9400, SkinMaterial.Leviathan),
        };

        public static readonly SkinDef[] Guardian =
        {
            // Цвета и условия те же, что у пузырька: это пары, и собирать их порознь
            // незачем. Разные наборы читались как ошибка — игрок видел у стража цвет,
            // которого у пузырька нет вовсе.
            SkinDef.Depth("skin.guardian.0", Palette.Hex("FFFFFF"),    0),
            SkinDef.Depth("skin.guardian.1", Palette.Hex("6BE88A"), 1000, Rarity.Common, SkinMaterial.Verdigris),
            SkinDef.Depth("skin.guardian.2", Palette.Hex("FF5B4A"), 5000, Rarity.Rare, SkinMaterial.Cinnabar),
            SkinDef.Tally("skin.guardian.3", Palette.Hex("2B2F36"), UnlockKind.Fish, 100, Rarity.Rare, SkinMaterial.Obsidian),
            SkinDef.Tally("skin.guardian.4", Palette.Hex("FFC83D"), UnlockKind.Debris, 100, Rarity.Rare, SkinMaterial.Brass),

            SkinDef.Trophy("skin.guardian.jelly",     BossKind.Jelly,     SkinArt.Jelly, 10000),
            SkinDef.Trophy("skin.guardian.serpent",   BossKind.Serpent,   SkinArt.Serpent, 10400),
            SkinDef.Trophy("skin.guardian.octopus",   BossKind.Octopus,   SkinArt.Octopus, 10800),
            SkinDef.Trophy("skin.guardian.swordfish", BossKind.Swordfish, SkinArt.Swordfish, 11200),
            SkinDef.Trophy("skin.guardian.crab",      BossKind.Crab,      SkinArt.Crab, 11600),
            SkinDef.Trophy("skin.guardian.ink",       BossKind.Ink,       SkinArt.Ink, 12000),
            SkinDef.Trophy("skin.guardian.piranha",   BossKind.Piranha,   SkinArt.Piranha, 12400),
            SkinDef.Trophy("skin.guardian.turtle",    BossKind.Turtle,    SkinArt.Turtle, 12800),
            SkinDef.Trophy("skin.guardian.shark",     BossKind.Shark,     SkinArt.Shark, 13200),
            SkinDef.Trophy("skin.guardian.manta",     BossKind.Manta,     SkinArt.Manta, 13600),

            SkinDef.Mastery("skin.guardian.star", SkinArt.Star),

            // === за монеты =====================================================
            // Порядок и редкость обязаны совпадать с SkinFxTable: эффект берётся по
            // тому же индексу, и перестановка строки здесь молча отдала бы скину
            // чужую анимацию. За соответствием следит Tools/audit.py.
            //
            // Ключ имени один на оба списка: это пара, покупается она целиком, и
            // называть половинки по-разному значило бы продавать игроку две вещи.
            SkinDef.Buy("skin.buy.coral",   Palette.Hex("FF9EB5"), Rarity.Common,   360, SkinMaterial.Coral),
            SkinDef.Buy("skin.buy.pearl",   Palette.Hex("FFF2E0"), Rarity.Common,   430, SkinMaterial.Pearl),
            SkinDef.Buy("skin.buy.silt",    Palette.Hex("8FA86B"), Rarity.Common,   500, SkinMaterial.Silt),
            SkinDef.Buy("skin.buy.sand",    Palette.Hex("E8D8A8"), Rarity.Common,   580, SkinMaterial.Sand),
            SkinDef.Buy("skin.buy.tide",    Palette.Hex("8FD8FF"), Rarity.Common,   680, SkinMaterial.Tide),

            SkinDef.Buy("skin.buy.frost",   Palette.Hex("DFF4FF"), Rarity.Rare,  1100, SkinMaterial.Ice),
            SkinDef.Buy("skin.buy.toxin",   Palette.Hex("A8FF5B"), Rarity.Rare,  1300, SkinMaterial.Toxin),
            SkinDef.Buy("skin.buy.neon",    Palette.Hex("FF5BE8"), Rarity.Rare,  1500, SkinMaterial.Neon),
            SkinDef.Buy("skin.buy.resin",   Palette.Hex("FFB45B"), Rarity.Rare,  1700, SkinMaterial.Resin),
            SkinDef.Buy("skin.buy.storm",   Palette.Hex("CFE4FF"), Rarity.Rare,  2000, SkinMaterial.Storm),

            SkinDef.Buy("skin.buy.ember",   Palette.Hex("FF7A3A"), Rarity.Epic,  2900, SkinMaterial.Ember),
            SkinDef.Buy("skin.buy.volt",    Palette.Hex("9ADFFF"), Rarity.Epic,  3400, SkinMaterial.Plasma),
            SkinDef.Buy("skin.buy.wraith",  Palette.Hex("CFE8E0"), Rarity.Epic,  3900, SkinMaterial.Wraith),
            SkinDef.Buy("skin.buy.prism",   Palette.Hex("FFFFFF"), Rarity.Epic,  4400, SkinMaterial.Prism),
            SkinDef.Buy("skin.buy.pixel",   Palette.Hex("5BFF9A"), Rarity.Epic,  5000, SkinMaterial.Pixel),

            SkinDef.Buy("skin.buy.nebula",  Palette.Hex("B88FFF"), Rarity.Legendary,  6200, SkinMaterial.Nebula),
            SkinDef.Buy("skin.buy.magma",   Palette.Hex("FF5B2E"), Rarity.Legendary,  7000, SkinMaterial.Magma),
            SkinDef.Buy("skin.buy.aurora",  Palette.Hex("8FFFD8"), Rarity.Legendary,  7700, SkinMaterial.Aurora),
            SkinDef.Buy("skin.buy.abyss",   Palette.Hex("6F5BD0"), Rarity.Legendary,  8500, SkinMaterial.Abyss),
            SkinDef.Buy("skin.buy.leviath", Palette.Hex("3FD8C8"), Rarity.Legendary,  9400, SkinMaterial.Leviathan),
        };

        // ---------------------------------------------------------- счётчики

        /// <summary>Сколько рыб страж отбил за всё время.</summary>
        public static int FishBlocked
        {
            get => PlayerPrefs.GetInt(SaveData.FishBlockedKey, 0);
            private set => PlayerPrefs.SetInt(SaveData.FishBlockedKey, value);
        }

        /// <summary>Сколько обломков страж отбил за всё время.</summary>
        public static int DebrisBlocked
        {
            get => PlayerPrefs.GetInt(SaveData.DebrisBlockedKey, 0);
            private set => PlayerPrefs.SetInt(SaveData.DebrisBlockedKey, value);
        }

        /// <summary>
        /// Засчитать отбитое. Возвращает true, если счётчик только что дошёл до порога
        /// какого-нибудь скина, — тогда игроку стоит показать, что открылась награда.
        ///
        /// Пишем в PlayerPrefs не на каждый удар, а только когда число ещё может на
        /// что-то повлиять: за длинную партию отбитых бывают сотни, и сохранять их все
        /// значило бы дёргать хранилище на ровном месте.
        /// </summary>
        public static bool CountBlocked(UnlockKind kind, int amount = 1)
        {
            if (kind != UnlockKind.Fish && kind != UnlockKind.Debris) return false;

            int before = kind == UnlockKind.Fish ? FishBlocked : DebrisBlocked;
            int needed = HighestThreshold(kind);
            if (before >= needed) return false;

            int after = before + amount;
            if (kind == UnlockKind.Fish) FishBlocked = after; else DebrisBlocked = after;

            // PlayerPrefs.Save() в WebGL синхронизирует IndexedDB, и дёргать его на
            // каждый отбитый обломок нельзя — за партию их бывают сотни. Число
            // держим в памяти PlayerPrefs, а на диск сбрасываем, только когда
            // открылась награда; остальное допишет обычное сохранение профиля.
            if (!CrossedThreshold(kind, before, after)) return false;

            PlayerPrefs.Save();
            return true;
        }

        static int HighestThreshold(UnlockKind kind)
        {
            int best = 0;
            foreach (var skin in Bubble) if (skin.Kind == kind) best = Mathf.Max(best, skin.Amount);
            foreach (var skin in Guardian) if (skin.Kind == kind) best = Mathf.Max(best, skin.Amount);
            return best;
        }

        static bool CrossedThreshold(UnlockKind kind, int before, int after)
        {
            foreach (var skin in Bubble)
                if (skin.Kind == kind && before < skin.Amount && after >= skin.Amount) return true;
            foreach (var skin in Guardian)
                if (skin.Kind == kind && before < skin.Amount && after >= skin.Amount) return true;
            return false;
        }

        /// <summary>
        /// Разовый перенос старого прогресса.
        ///
        /// Трофеи с боссов раньше выдавались за одну победу, без монет. С появлением
        /// цены игрок, победивший кракена полгода назад, лишился бы уже заработанной
        /// награды — а отнимать выданное нельзя ни при какой правке баланса. Поэтому
        /// при первом запуске после обновления все трофеи за уже побеждённых боссов
        /// записываются как купленные. Платить будут только за тех, кого победят
        /// после обновления.
        /// </summary>
        public static void MigrateBossTrophies()
        {
            if (PlayerPrefs.GetInt(SaveData.BossGrantKey, 0) != 0) return;

            long owned = SaveData.GetMask(SaveData.OwnedBubbleKey);
            for (int i = 0; i < Bubble.Length; i++)
            {
                if (Bubble[i].Kind != UnlockKind.Boss) continue;
                if (!IsBossDefeated(Bubble[i].UnlockBoss)) continue;
                owned |= 1L << i;
            }

            SaveData.SetMask(SaveData.OwnedBubbleKey, owned);
            SaveData.SetMask(SaveData.OwnedGuardianKey, owned);
            PlayerPrefs.SetInt(SaveData.BossGrantKey, 1);
            PlayerPrefs.Save();
        }

        // --------------------------------------------------------- покупки

        /// <summary>
        /// Куплен ли скин с этим индексом.
        ///
        /// Покупка одна на пару: заплатив за «Магму», игрок получает и пузырёк, и
        /// стража. Продавать половинки порознь значило бы удвоить цену за ту же
        /// вещь — списки и так устроены как пары, с одинаковыми именами и порядком.
        /// Маски при этом две: если однажды понадобится продавать раздельно, данные
        /// для этого уже лежат в сохранении.
        /// </summary>
        public static bool IsOwned(int index)
        {
            if (index < 0 || index >= 63) return false;
            return (SaveData.GetMask(SaveData.OwnedBubbleKey) & (1L << index)) != 0L;
        }

        /// <summary>
        /// Купить. Возвращает false, если не хватило монет или скин не продаётся.
        /// Списание и пометка идут вместе: разорвать их нельзя, иначе при неудачном
        /// моменте игрок остался бы без монет и без скина.
        /// </summary>
        public static bool Buy(int index)
        {
            if (index < 0 || index >= Bubble.Length) return false;

            var skin = Bubble[index];
            if (skin.Kind != UnlockKind.Purchase && skin.Kind != UnlockKind.Boss) return false;
            if (skin.Kind == UnlockKind.Boss && !IsBossDefeated(skin.UnlockBoss)) return false;
            if (IsOwned(index)) return false;
            if (!Wallet.Spend(skin.Price)) return false;

            long bit = 1L << index;
            SaveData.SetMask(SaveData.OwnedBubbleKey, SaveData.GetMask(SaveData.OwnedBubbleKey) | bit);
            SaveData.SetMask(SaveData.OwnedGuardianKey, SaveData.GetMask(SaveData.OwnedGuardianKey) | bit);
            PlayerPrefs.Save();
            return true;
        }

        // ------------------------------------------------------ побеждённые боссы

        /// <summary>Битовая маска побеждённых боссов. Хранится вместе с остальным профилем.</summary>
        public static int DefeatedMask
        {
            get => PlayerPrefs.GetInt(SaveData.BossesKey, 0);
            private set { PlayerPrefs.SetInt(SaveData.BossesKey, value); PlayerPrefs.Save(); }
        }

        public static bool IsBossDefeated(BossKind kind) => (DefeatedMask & (1 << (int)kind)) != 0;

        /// <summary>Возвращает true, если победа над этим боссом засчитана впервые.</summary>
        public static bool MarkBossDefeated(BossKind kind)
        {
            int bit = 1 << (int)kind;
            int mask = DefeatedMask;
            if ((mask & bit) != 0) return false;

            DefeatedMask = mask | bit;
            MarkBossMet(kind);   // победил — значит точно видел
            return true;
        }

        // ------------------------------------------------------ встреченные боссы

        /// <summary>
        /// Маска боссов, с которыми игрок уже сталкивался. От неё открываются статьи
        /// справочника: читать про того, кого ещё не видел, — спойлер.
        ///
        /// Победы подмешиваются сюда при чтении: у профилей, сделанных до появления
        /// справочника, этой маски нет вовсе, и без слияния ветеран игры открыл бы
        /// пустой справочник.
        /// </summary>
        public static int MetMask
        {
            get => PlayerPrefs.GetInt(SaveData.BossesMetKey, 0) | DefeatedMask;
            private set { PlayerPrefs.SetInt(SaveData.BossesMetKey, value); PlayerPrefs.Save(); }
        }

        public static bool IsBossMet(BossKind kind) => (MetMask & (1 << (int)kind)) != 0;

        /// <summary>Возвращает true, если встреча засчитана впервые.</summary>
        public static bool MarkBossMet(BossKind kind)
        {
            int bit = 1 << (int)kind;
            int mask = MetMask;
            if ((mask & bit) != 0) return false;

            MetMask = mask | bit;
            return true;
        }

        // --------------------------------------------------------------- выбор

        public static int SelectedBubble
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(BubbleKey, 0), 0, Bubble.Length - 1);
            set { PlayerPrefs.SetInt(BubbleKey, value); PlayerPrefs.Save(); }
        }

        public static int SelectedGuardian
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(GuardianKey, 0), 0, Guardian.Length - 1);
            set { PlayerPrefs.SetInt(GuardianKey, value); PlayerPrefs.Save(); }
        }

        public static Color BubbleTint => Bubble[SelectedBubble].Tint;
        public static Color GuardianTint => Guardian[SelectedGuardian].Tint;
        public static SkinArt BubbleArt => Bubble[SelectedBubble].Art;
        public static SkinArt GuardianArt => Guardian[SelectedGuardian].Art;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Дев-режим: все скины считаются открытыми. Нужно, чтобы смотреть их
        /// рисунки, не отбивая по сотне рыб каждый раз. В релизной сборке поля нет
        /// вовсе — препроцессор вырезает и его, и проверку ниже.
        /// </summary>
        public static bool DevUnlockAll = true;
#endif

        public static bool IsUnlocked(SkinDef skin, int bestDepth)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DevUnlockAll) return true;
#endif
            switch (skin.Kind)
            {
                case UnlockKind.Boss: return IsBossDefeated(skin.UnlockBoss) && IsOwned(IndexOf(skin));
                case UnlockKind.Fish: return FishBlocked >= skin.Amount;
                case UnlockKind.Debris: return DebrisBlocked >= skin.Amount;
                case UnlockKind.Mastery: return AllOthersUnlocked(bestDepth);
                case UnlockKind.Purchase: return IsOwned(IndexOf(skin));
                default: return bestDepth >= skin.Amount;
            }
        }

        /// <summary>
        /// Индекс скина в каталоге. Нужен покупкам: маска считает по нему.
        ///
        /// Ищем в обоих списках. Они параллельны, но у трофеев стража свой ключ
        /// («skin.guardian.jelly»), и поиск только по пузырькам давал −1: купленный
        /// трофей-страж не открывался, а выбор тут же сбрасывался на базовый.
        /// </summary>
        static int IndexOf(SkinDef skin)
        {
            for (int i = 0; i < Bubble.Length; i++)
                if (Bubble[i].NameKey == skin.NameKey) return i;
            for (int i = 0; i < Guardian.Length; i++)
                if (Guardian[i].NameKey == skin.NameKey) return i;
            return -1;
        }

        /// <summary>
        /// Открыто ли всё остальное. Сама награда за коллекцию в проверке не участвует —
        /// иначе условие ссылалось бы на себя и не выполнилось бы никогда.
        ///
        /// Покупаемые скины тоже не в счёт, и это важнее, чем кажется. Награда за
        /// коллекцию существовала до магазина, и у части игроков она уже открыта.
        /// Начни мы требовать ещё и все покупки — звезда исчезла бы у тех, кто её
        /// честно собрал, а отнимать выданное нельзя ни при каких правках баланса.
        /// </summary>
        static bool AllOthersUnlocked(int bestDepth)
        {
            foreach (var skin in Bubble)
                if (skin.Kind != UnlockKind.Mastery && skin.Kind != UnlockKind.Purchase
                    && !IsUnlocked(skin, bestDepth)) return false;
            foreach (var skin in Guardian)
                if (skin.Kind != UnlockKind.Mastery && skin.Kind != UnlockKind.Purchase
                    && !IsUnlocked(skin, bestDepth)) return false;
            return true;
        }

        /// <summary>Условие открытия одной строкой — для плитки и статуса в магазине.</summary>
        public static string UnlockText(SkinDef skin)
        {
            switch (skin.Kind)
            {
                case UnlockKind.Boss:
                    // Два условия подряд: сперва победа, потом монеты. Показываем то,
                    // которое мешает прямо сейчас, — иначе игрок копит на трофей
                    // босса, которого ещё ни разу не видел.
                    if (!IsBossDefeated(skin.UnlockBoss))
                        // имена боссов хранятся с восклицательным знаком — он нужен баннеру,
                        // но в строке условия выглядит выкриком не к месту
                        return Loc.Get("shop.unlockBoss")
                               + Loc.Get("boss." + BossKey(skin.UnlockBoss)).TrimEnd('!');

                    return Wallet.CanAfford(skin.Price)
                        ? Loc.Get("shop.buyFor") + skin.Price
                        : Loc.Get("shop.needCoins") + (skin.Price - Wallet.Balance);

                case UnlockKind.Fish:
                    return Loc.Get("shop.unlockFish") + FishBlocked + " / " + skin.Amount;

                case UnlockKind.Debris:
                    return Loc.Get("shop.unlockDebris") + DebrisBlocked + " / " + skin.Amount;

                case UnlockKind.Mastery:
                    return Loc.Get("shop.unlockAll");

                case UnlockKind.Purchase:
                    return Wallet.CanAfford(skin.Price)
                        ? Loc.Get("shop.buyFor") + skin.Price
                        : Loc.Get("shop.needCoins") + (skin.Price - Wallet.Balance);

                default:
                    return Loc.Get("shop.unlockAt") + skin.Amount + Loc.Get("hud.meters");
            }
        }

        static string BossKey(BossKind kind)
        {
            switch (kind)
            {
                case BossKind.Jelly: return "jelly";
                case BossKind.Serpent: return "serpent";
                case BossKind.Octopus: return "octopus";
                case BossKind.Swordfish: return "swordfish";
                case BossKind.Crab: return "crab";
                case BossKind.Ink: return "ink";
                case BossKind.Piranha: return "piranhas";
                case BossKind.Shark: return "shark";
                case BossKind.Manta: return "manta";
                default: return "turtles";
            }
        }

        /// <summary>Если выбранный скин почему-то заблокирован — откатываемся на базовый.</summary>
        public static void ValidateSelection(int bestDepth)
        {
            if (!IsUnlocked(Bubble[SelectedBubble], bestDepth)) SelectedBubble = 0;
            if (!IsUnlocked(Guardian[SelectedGuardian], bestDepth)) SelectedGuardian = 0;
        }
    }
}
