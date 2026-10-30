using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Все настройки баланса в одном месте. Висит на объекте GameBootstrap —
    /// правится прямо в инспекторе, менять код не нужно.
    ///
    /// Единицы: мир 10 юнитов в высоту. Коэффициент пересчёта в «пиксели прототипа» — 76 px/юнит,
    /// поэтому metersPerUnit = 76/12 ≈ 6.333 (в прототипе метры = px/12).
    /// </summary>
    public class GameConfig : MonoBehaviour
    {
        [Header("Мир")]
        public float worldHeight = 10f;
        [Tooltip("Соотношение ширины к высоте игровой зоны (портрет 480x760 ≈ 0.63).")]
        public float designAspect = 0.64f;
        public float metersPerUnit = 7.218f;

        [Header("Пузырёк")]
        [Tooltip("Общий множитель размера пузырька и стража. 0.8 = на 20 % меньше.")]
        [Range(0.5f, 1.5f)] public float playerScaleMultiplier = 0.68f;

        public float bubbleRadius = 0.28f;
        [Tooltip("Позиция пузырька: доля высоты экрана снизу.")]
        public float bubbleYFactor = 0.32f;
        public int startLives = 3;
        public float invulnerabilityTime = 1.2f;
        public float wobbleSpeed = 3f;
        public float wobbleAmplitude = 0.045f;

        [Header("Страж")]
        public float guardianRadius = 0.45f;
        [Tooltip("Максимальное удаление стража от пузырька. По умолчанию фактически безлимитно — " +
                 "уменьшите, если понадобится «поводок».")]
        public float guardianMaxDistanceFromBubble = 1000f;
        [Tooltip("Минимальное расстояние от пузырька. Без него стража можно посадить прямо " +
                 "на пузырёк, и он перехватывал бы всё с любой стороны — игра ломается.")]
        public float guardianMinDistanceFromBubble = 0.85f;
        [Tooltip("Куда страж возвращается, когда его отпустили (смещение от пузырька).")]
        public Vector2 guardianHomeOffset = new Vector2(0f, 0.95f);
        public float guardianReturnLerp = 7f;
        public float guardianFollowLerp = 26f;
        [Tooltip("true — хватать стража можно нажатием в любой точке экрана (удобнее на телефоне). " +
                 "false — только нажатием по самому стражу.")]
        public bool grabAnywhere = true;
        [Tooltip("Во сколько раз зона захвата больше радиуса стража (при grabAnywhere = false).")]
        public float guardianGrabRadiusMultiplier = 1.8f;
        [Tooltip("Зона захвата, когда стража зашвырнуло (Чернильный морок). Шире обычной: " +
                 "искать в залитом экране и при этом попадать пиксель в пиксель — нечестно. " +
                 "Пока страж заброшен, grabAnywhere не действует, иначе он прилетал бы сам.")]
        public float guardianDisplacedGrabMultiplier = 3.4f;
        [Tooltip("Перезарядка после каждого отбитого объекта. Пока страж «остывает», он пропускает удары — " +
                 "поэтому нельзя просто держать его над пузырьком и не двигать. 0 — отключить.")]
        public float guardianDeflectCooldown = 0.28f;

        [Header("Скорость и сложность")]
        public float baseScrollSpeed = 1.04f;       // юнитов/сек
        public float scrollAcceleration = 0.0161f;  // юнитов/сек^2
        [Tooltip("Потолок скорости прокрутки, юнитов/сек. Без него разгон не " +
                 "останавливается никогда: к десяти тысячам метров экран обновлялся бы " +
                 "дважды в секунду, и дело было бы уже не в мастерстве, а в том, что " +
                 "увидеть летящее просто не успеваешь. Потолок взят высоким — до первой " +
                 "тысячи метров игрок его не заметит.")]
        public float maxScrollSpeed = 4.56f;
        public float baseSpawnInterval = 1.47f;
        [Tooltip("Насколько плотно идут препятствия на пределе сложности. Главная ручка " +
                 "сложности перегонов между боссами.")]
        public float minSpawnInterval = 1.04f;
        public float spawnIntervalDecay = 0.00074f;  // за секунду
        [Tooltip("Во сколько раз растёт потолок скорости после каждого побеждённого " +
                 "босса. 1.1 — на десять процентов. Прибавка множителем, а не слагаемым: " +
                 "иначе на быстрых скоростях она перестаёт ощущаться.")]
        public float bossClearedSpeedFactor = 1.1f;
        [Tooltip("Насколько потолок вообще может вырасти относительно исходного. " +
                 "Без этого предела рост за боссов возвращает ту же беду, ради которой " +
                 "потолок и заводился: на десятом ряду мир летел бы мимо быстрее, чем " +
                 "игрок физически успевает увидеть летящее.")]
        public float maxScrollSpeedGrowth = 2.2f;
        public float bossClearedSpawnBonus = 0.05f;

        [Header("Препятствия")]
        public float obstacleMinRadius = 0.21f;
        public float obstacleMaxRadius = 0.37f;
        [Tooltip("Множитель скорости прокрутки. Держите близко к 1: разнобой вертикальных " +
                 "скоростей читается как «предметы падают», а не «мы поднимаемся мимо них».")]
        public float obstacleSpeedMin = 0.97f;
        public float obstacleSpeedMax = 1.06f;
        [Tooltip("Амплитуда покачивания обломка вбок. Чем больше — тем хуже работает " +
                 "статичный страж прямо над пузырьком.")]
        public float obstacleSwayMin = 0.25f;
        public float obstacleSwayMax = 0.85f;
        public float obstacleSpinMax = 1.2f;
        [Tooltip("Импульс, с которым страж отбивает препятствие.")]
        public float deflectImpulse = 3.42f;
        [Tooltip("Сколько метров начисляется за отбитое препятствие.")]
        public float deflectMeterBonus = 2f;

        [Tooltip("Монет за разбитый коралл: шар локации, сегмент шпиля, блок строя. " +
                 "У препятствий награда своя, она задана в HazardTable.")]
        public int coralCoinReward = 2;

        [Tooltip("Доля частиц следа от скина, 0.3–1. Ниже единицы — щадящий режим для " +
                 "слабых телефонов: сам эффект остаётся узнаваемым, но плотность падает. " +
                 "На эффекты разрушения не влияет: они короткие и редкие, а именно " +
                 "постоянный след стоит кадров.")]
        [Range(0.3f, 1f)]
        public float skinTrailDensity = 1f;

        [Header("Локации")]
        [Tooltip("С какой глубины (в метрах) начинают встречаться локации.")]
        public float zoneFirstAt = 250f;
        public float zoneIntervalMin = 360f;
        public float zoneIntervalMax = 520f;
        [Tooltip("Длина локации в юнитах. Экран — 10, так что 38 это почти четыре экрана. " +
                 "Число композиций внутри подстраивается автоматически.")]
        public float zoneHeight = 38f;
        public float zoneBlobMinRadius = 0.34f;
        public float zoneBlobMaxRadius = 0.62f;
        [Tooltip("Сколько коралловых шаров максимум допускается прямо над пузырьком.")]
        public int zoneMaxCenterBlobs = 2;
        [Tooltip("Метров за каждый разбитый шар.")]
        public float zoneMeterBonus = 3f;
        [Tooltip("Показывать название локации баннером на входе.")]
        public bool zoneShowBanner = true;

        [Header("Длинные кораллы")]
        [Tooltip("С какой глубины (в метрах) начинают встречаться длинные кораллы.")]
        public float spireFirstSpawnAt = 80f;
        public float spireInterval = 13.37f;
        public float spireMinInterval = 8.7f;
        public float spireIntervalDecay = 0.005f;
        public float spireMinLength = 3.2f;
        public float spireMaxLength = 5.6f;
        [Tooltip("Длина одного звена ветви.")]
        public float spireSegmentSize = 0.8f;
        [Tooltip("Шаг между звеньями в долях их длины. Меньше 1 — звенья внахлёст, " +
                 "ветвь выглядит сплошной; слишком мало — одного удара не хватит на проход.")]
        public float spireSegmentSpacing = 0.75f;
        [Tooltip("Радиус столкновения звена в долях его длины.")]
        public float spireThickness = 0.31f;
        [Tooltip("Максимальный наклон от горизонтали. Чем круче ветвь, тем ближе друг к другу " +
                 "оказываются соседние звенья по горизонтали и тем чаще нужен второй удар. " +
                 "Вертикальную ветвь пробить не успеть вовсе.")]
        public float spireMaxAngle = 35f;
        public float spireSpinMax = 8f;
        [Tooltip("Сколько звеньев максимум допускается прямо над пузырьком.")]
        public int spireMaxCenterSegments = 2;
        [Tooltip("Метров за каждое отломанное звено.")]
        public float spireMeterBonus = 2.5f;

        [Header("Коралловые постройки")]
        [Tooltip("С какой глубины (в метрах) начинают встречаться фигуры из кораллов.")]
        public float formationFirstSpawnAt = 120f;
        public float formationInterval = 12.04f;
        public float formationMinInterval = 9.37f;
        public float formationIntervalDecay = 0.0047f;
        [Tooltip("Размер одного блока постройки в юнитах.")]
        public float formationCellSize = 0.6f;
        [Tooltip("Радиус столкновения блока. Чуть меньше половины клетки — углы прощаются.")]
        public float formationBlockRadius = 0.29f;
        [Tooltip("Максимальная скорость вращения компактных фигур, град/сек.")]
        public float formationSpinMax = 6f;
        [Tooltip("Сколько блоков максимум допускается прямо над пузырьком: столько ударов " +
                 "стражем придётся успеть нанести, чтобы пробить проход.")]
        public int formationMaxCenterBlocks = 2;
        [Tooltip("Метров за каждый разбитый блок постройки.")]
        public float formationMeterBonus = 3f;

        [Header("Рыбы")]
        [Tooltip("С какой глубины (в метрах) начинают появляться рыбы.")]
        public float fishFirstSpawnAt = 150f;
        public float fishBaseInterval = 6.46f;
        [Tooltip("Как часто приплывают рыбы на пределе сложности. Считается вместе с " +
                 "minSpawnInterval: угрозы идут из двух источников сразу, и складывать " +
                 "их плотность надо в уме.")]
        public float fishMinInterval = 3.69f;
        public float fishIntervalDecay = 0.0023f;
        public float fishRadius = 0.30f;
        public float fishSpeedMin = 1.32f;
        public float fishSpeedMax = 2.17f;
        [Tooltip("Скорость поворота, рад/сек. Меньше — рыба хуже доворачивает и чаще промахивается.")]
        public float fishTurnRate = 1.9f;
        [Tooltip("Насколько сильно рыбу сбивает с прямого курса. 0 — плывёт точно в пузырёк.")]
        public float fishWanderStrength = 0.8f;
        [Tooltip("Через сколько секунд рыба теряет интерес и уплывает по прямой.")]
        public float fishLifetime = 15f;
        [Range(0f, 1f)] public float lurkerChance = 0.18f;

        [Header("Медузы")]
        [Tooltip("Какая доля спавнов «рыб» приходится на медуз. Медузы не охотятся: " +
                 "они просто висят в толще воды на пути наверх.")]
        [Range(0f, 1f)] public float jellyfishChance = 0.22f;
        public float jellyfishRadius = 0.42f;
        [Tooltip("Насколько сильно медузу сносит вбок.")]
        public float jellyfishDriftMax = 0.55f;
        [Tooltip("Во сколько раз реже рыбы появляются во время боя с боссом.")]
        public float fishBossIntervalMultiplier = 1.9f;

        [Header("Расписание боссов")]
        [Tooltip("Шаг сетки боссов в метрах. Боссы стоят строго на круглых отметках: " +
                 "1000 — первый, 2000 — второй и так до десятого на 10000. Никакой " +
                 "случайности в расписании нет намеренно: игрок должен знать, на какой " +
                 "глубине его ждут и кто именно.\n\n" +
                 "Дальше ряд идёт заново, но тяжелее: на 11000 снова первый босс, только " +
                 "с удвоенным запасом здоровья и сразу разъярённый (см. BossTuning).")]
        public float bossEveryMeters = 1000f;

        [Header("Босс: Грозовая медуза")]
        // Размеры — в долях игровой колонки (6.4 юнита): читаются одинаково на любом экране.
        public int jellyHp = 9;
        [Tooltip("Радиус купола медузы в долях игровой колонки.")]
        public float jellyBellSpan = 0.20f;
        [Tooltip("Радиус поля молний. Ближе страж не подпускается — иначе бой сводился " +
                 "к тому, чтобы поставить шарик вплотную к боссу.")]
        public float jellyFieldSpan = 0.36f;
        [Tooltip("Радиус попадания отражённой молнии, в долях радиуса купола.")]
        public float jellyHitFraction = 0.58f;
        [Tooltip("Точка зависания: доля высоты экрана сверху.")]
        public float jellyHoverFactor = 0.24f;
        public float jellySwayAmplitude = 0.46f;   // доля от половины ширины игровой зоны
        public float jellySwaySpeed = 1.15f;
        public float jellyShootIntervalMin = 0.85f;
        public float jellyShootIntervalMax = 1.35f;
        public float jellyBoltSpeed = 1.97f;
        public float jellyBoltRadius = 0.16f;
        public float jellyReflectSpeed = 3.42f;
        [Tooltip("Скорость подруливания отражённой молнии к боссу, град/сек. 0 — строго по прямой.")]
        public float jellyReflectHoming = 100f;

        [Header("Босс: Змей бездны")]
        [Tooltip("Сколько раз нужно подставить стража под рывок, чтобы его прогнать.")]
        public int serpentHp = 8;
        [Tooltip("Радиус, по которому змей наматывает круги вокруг пузырька.")]
        public float serpentOrbitRadius = 2.5f;
        [Tooltip("Угловая скорость обхода, рад/сек.")]
        public float serpentOrbitSpeed = 1.05f;
        [Range(4, 24)] public int serpentSegmentCount = 16;
        public float serpentSegmentSpacing = 0.42f;
        [Tooltip("Размер головы в юнитах; звенья тела считаются от неё.")]
        public float serpentHeadSize = 1.05f;
        [Tooltip("Радиус столкновения головы во время рывка.")]
        public float serpentHeadRadius = 0.40f;

        [Tooltip("Скорость поворота головы, град/сек. Змей не разворачивается на месте: " +
                 "он закладывает дугу, и поэтому читается как плывущий, а не как курсор.")]
        public float serpentTurnRate = 210f;
        [Tooltip("Насколько тело виляет вбок при плавании, юнитов. Размах растёт вместе " +
                 "со скоростью, так что на рывке змей хлещет хвостом.")]
        public float serpentWaveAmplitude = 0.3f;
        [Tooltip("Темп бегущей по телу волны, рад/сек.")]
        public float serpentWaveSpeed = 7.5f;
        [Tooltip("Сколько звеньев укладывается в одну волну. Меньше — тело извивается чаще.")]
        public float serpentWaveLength = 7f;

        [Tooltip("Сколько змей свободно плавает по экрану перед заходом в круг, при полном и последнем HP.")]
        public float serpentRoamTimeMax = 4.6f;
        public float serpentRoamTimeMin = 2.6f;
        [Tooltip("Какую долю экрана занимает область плавания. 1 — весь экран целиком.")]
        public float serpentRoamSpread = 1f;
        [Tooltip("Ближе этого расстояния к пузырьку змей во время плавания не подходит. " +
                 "Достать он должен только рывком, а не проплывая мимо.")]
        public float serpentRoamMinDistance = 3.4f;
        [Tooltip("Скорость плавания между путевыми точками, юнитов/сек.")]
        public float serpentRoamMoveSpeed = 4.6f;
        [Tooltip("Насколько далеко берётся следующая путевая точка, в долях диагонали " +
                 "экрана. Из-за этого он режет экран из конца в конец, а не топчется в углу.")]
        public float serpentRoamHopFactor = 0.55f;
        [Tooltip("Длительность резкого стягивания на орбиту. Это сигнал, что сейчас будет атака.")]
        public float serpentCoilTime = 0.55f;
        [Tooltip("Сколько змей идёт по кругу перед замахом, при полном и при последнем HP.")]
        public float serpentCircleTimeMax = 1.4f;
        public float serpentCircleTimeMin = 0.8f;
        [Tooltip("Длительность замаха — это и есть окно реакции игрока.")]
        public float serpentWindupMax = 0.85f;
        public float serpentWindupMin = 0.5f;
        public float serpentLungeSpeedMin = 7f;
        public float serpentLungeSpeedMax = 11f;
        public float serpentRecoverTime = 0.55f;

        [Header("Босс: Клешнерук (краб)")]
        // ВАЖНО: почти все размеры краба заданы в долях игровой колонки (6.4 юнита),
        // а не в юнитах — так он одинаково крупно читается на портрете и на широком экране.
        public int crabHp = 11;
        [Tooltip("Точка зависания: доля высоты экрана сверху.")]
        public float crabHoverFactor = 0.36f;
        [Tooltip("Ширина панциря в долях игровой колонки. Больше 1 — краб шире зоны игры.")]
        public float crabShellSpan = 1.44f;
        [Tooltip("Размер клешни в долях игровой колонки.")]
        public float crabPincerSpan = 0.70f;
        [Tooltip("Толщина звена руки в долях игровой колонки.")]
        public float crabLimbSpan = 0.27f;

        [Tooltip("Радиус сжатой клешни в долях её размера — ею она бьёт как дубиной.")]
        public float crabClawRadius = 0.42f;
        [Tooltip("Где находится пасть относительно центра клешни, в долях её размера. " +
                 "Должно совпадать с нарисованной мишенью (0.17), иначе хитбокс и картинка разъезжаются.")]
        public float crabMouthFraction = 0.17f;
        [Tooltip("Как далеко вперёд достают раскрытые клыки, в долях размера клешни.")]
        public float crabJawSpan = 0.48f;
        [Tooltip("Полуразмах раскрытых клыков в долях размера клешни. Попал сюда, " +
                 "но мимо осевой — клешня схватит стража.")]
        public float crabJawSpread = 0.46f;
        [Tooltip("Полуширина коридора урона по осевой, в долях размера клешни.")]
        public float crabTargetFraction = 0.14f;
        [Tooltip("ГЛАВНОЕ ПРАВИЛО раскрытой клешни: чтобы удар засчитался, страж должен быть " +
                 "дальше этой доли игровой колонки от пузырька, то есть игрок обязан ВЫВЕСТИ его " +
                 "навстречу клешне. Пока страж отсиживается у пузырька, клешня его хватает — " +
                 "именно так «защита» и отличается от «атаки».")]
        public float crabAttackReachSpan = 0.42f;
        [Tooltip("Радиус острия — им клешня достаёт до пузырька.")]
        public float crabTipRadius = 0.30f;

        [Tooltip("С какого расстояния клешня начинает удар, в долях игровой колонки. " +
                 "Должно с запасом превышать длину клыков, иначе клешня бьёт в упор.")]
        public float crabStrikeSpan = 0.85f;
        [Tooltip("Центральный угол захода клешни: краб сверху, поэтому клешни падают сверху-сбоку.")]
        public float crabApproachAngle = 55f;
        public float crabApproachSpread = 50f;
        public float crabAttackIntervalMax = 2.3f;
        public float crabAttackIntervalMin = 1.3f;
        public float crabWindupMax = 0.85f;
        public float crabWindupMin = 0.55f;
        [Tooltip("Замах добивающего удара второй клешнёй, пока страж в захвате.")]
        public float crabPunishWindup = 0.55f;
        [Tooltip("Длительность выпада. Растёт вместе с дистанцией, иначе клешня летит слишком быстро.")]
        public float crabLungeTime = 0.42f;
        public float crabReturnTime = 0.55f;
        [Range(3, 8)] public int crabArmSegments = 5;
        public float crabSwaySpeed = 0.65f;
        public float crabSwayAmplitude = 0.30f;

        [Header("Босс: Клинок бездны")]
        public int swordfishHp = 8;
        [Tooltip("Длина рыбы в юнитах.")]
        public float swordfishLength = 3.51f;
        [Tooltip("Радиус острия клинка — им он и колет.")]
        public float swordfishTipRadius = 0.34f;
        [Tooltip("Пауза за кадром между заходами, при полном и последнем HP. " +
                 "По экрану между атаками он не плавает — вышел за край и сразу заходит снова.")]
        public float swordfishRestTimeMax = 0.9f;
        public float swordfishRestTimeMin = 0.35f;
        [Tooltip("Прицеливание за краем экрана — окно реакции. Виден силуэт и линия разгона.")]
        public float swordfishAimTimeMax = 0.75f;
        public float swordfishAimTimeMin = 0.45f;
        public float swordfishChargeSpeedMin = 13f;
        public float swordfishChargeSpeedMax = 18f;

        [Header("Босс: Стая пираний")]
        [Tooltip("Сколько пираний в стае. Это же и есть HP босса: один отбитый — минус один.")]
        public int piranhaCount = 15;
        [Tooltip("Длина пираньи в долях игровой колонки.")]
        public float piranhaSizeSpan = 0.204f;
        [Tooltip("Радиус столкновения в долях длины.")]
        public float piranhaHitFraction = 0.28f;

        [Tooltip("Расстояние между полосами строя. Реальный шаг ужимается под ширину " +
                 "ЭКРАНА: десять тел длиной в полтора юнита в игровую колонку не влезают.")]
        public float piranhaLaneStep = 1.15f;
        [Tooltip("Насколько крайние в строю отстают от середины. Ноль — ровная линейка. " +
                 "Отставание разводит стаю не только вширь, но и по глубине, поэтому на " +
                 "узком экране тела не наезжают друг на друга.")]
        public float piranhaWedgeLag = 1.15f;
        [Tooltip("Скорость, с которой стая уходит за кромку строиться перед проходом.")]
        public float piranhaRegroupSpeed = 9f;
        [Tooltip("Сколько ждать отставших на построении, прежде чем начать проход без них.")]
        public float piranhaRegroupTimeout = 2.2f;
        [Tooltip("Скорость прохода строем, при полном и последнем HP.")]
        public float piranhaSweepSpeedMin = 5.5f;
        public float piranhaSweepSpeedMax = 8f;

        [Tooltip("Сколько стая расходится по арене перед серией одиночных рывков.")]
        public float piranhaScatterTime = 1.6f;
        [Tooltip("Скорость плавания между точками. У каждого пираньи она ещё и своя, " +
                 "с разбросом ±25%: одинаковый темп читался бы одним объектом.")]
        public float piranhaRoamSpeed = 3.8f;
        [Tooltip("Ближе этого к пузырьку пираньи не блуждают: доставать должен рывок, " +
                 "а не случайный проплыв мимо.")]
        public float piranhaRoamMinDistance = 3.2f;

        [Tooltip("Сколько одиночных рывков между двумя проходами строем.")]
        public int piranhaSoloPerRound = 3;
        [Tooltip("Пауза между рывками.")]
        public float piranhaSoloGap = 0.55f;
        [Tooltip("Замах перед рывком — окно реакции, при полном и последнем HP.")]
        public float piranhaAimTimeMax = 0.9f;
        public float piranhaAimTimeMin = 0.55f;
        [Tooltip("С какой скоростью атакующий пятится на замахе. Это и есть сигнал, кто пойдёт.")]
        public float piranhaRecoilSpeed = 1.2f;
        [Tooltip("Скорость рывка, при полном и последнем HP.")]
        public float piranhaDashSpeedMin = 9f;
        public float piranhaDashSpeedMax = 13f;

        [Header("Босс: Каменный панцирь")]
        [Tooltip("Сколько черепах в кольце. Это же и есть HP: отбил камень — стрелок выбыл.")]
        public int turtleCount = 12;
        [Tooltip("Размер черепахи в долях игровой колонки. Считайте вместе с turtleCount: " +
                 "двенадцать панцирей прежнего размера смыкались в кольце в сплошную стену, " +
                 "и было не разобрать, кто из них сейчас стреляет.")]
        public float turtleSizeSpan = 0.17f;
        [Tooltip("Радиус кольца вокруг пузырька в долях игровой колонки. Меньше — " +
                 "страж успевает перекрывать сразу несколько направлений, и ритм пропадает.")]
        public float turtleRingSpan = 0.55f;
        [Tooltip("Сколько отбитых камней выдерживает панцирь, прежде чем черепаха выбывает. " +
                 "Единица делала бой разменом «камень за черепаху», и кольцо таяло быстрее, " +
                 "чем игрок успевал попасть в ритм. С двойкой каждая черепаха требует " +
                 "возврата дважды, и полоса HP ползёт вдвое медленнее.")]
        public int turtleShellHits = 2;
        [Tooltip("Шанс, что очередь останется на той же черепахе и она выстрелит второй " +
                 "раз подряд. Именно это ломает привычку «отбил — смотрю на соседнюю».")]
        [Range(0f, 1f)] public float turtleRepeatChance = 0.3f;
        [Tooltip("Вращение кольца, град/сек. Без него позиции заучиваются наизусть.")]
        public float turtleRingSpin = 9f;
        [Tooltip("Сколько кольцо съезжается к пузырьку на выходе.")]
        public float turtleEnterTime = 1.2f;
        [Tooltip("Пауза перед первым выстрелом: игрок ещё читает баннер с именем босса.")]
        public float turtleFirstPause = 0.7f;

        [Tooltip("Замах перед броском, при полном и последнем HP. Черепаха отходит " +
                 "наружу кольца — это видно даже боковым зрением.")]
        public float turtleAimTimeMax = 0.85f;
        public float turtleAimTimeMin = 0.5f;
        [Tooltip("На сколько юнитов черепаха отходит наружу на замахе.")]
        public float turtleRecoil = 0.45f;

        [Tooltip("Радиус камня.")]
        public float turtleRockRadius = 0.32f;
        [Tooltip("Скорость камня, при полном и последнем HP.")]
        public float turtleRockSpeedMin = 5.5f;
        public float turtleRockSpeedMax = 8f;

        [Tooltip("Пауза между очередями, при полном и последнем HP.")]
        public float turtleCycleGapMax = 0.75f;
        public float turtleCycleGapMin = 0.35f;
        [Tooltip("Сколько выстрелов подряд идёт в очереди на последнем HP.")]
        public int turtleBurstMax = 3;
        [Tooltip("Пауза внутри очереди. ОБЯЗАНА быть больше guardianDeflectCooldown, " +
                 "иначе серию физически нечем отбить.")]
        public float turtleBurstGapMax = 0.62f;
        public float turtleBurstGapMin = 0.42f;
        [Tooltip("Вероятность, что очередь перескочит на случайную черепаху вместо " +
                 "соседней. Ноль — идеальная карусель, которую можно крутить вслепую.")]
        public float turtleJumpChance = 0.45f;

        [Tooltip("С какой доли выбитых черепах в ход идут веерные залпы: три камня, " +
                 "но в пузырёк летит только средний. Два крайних — обманки, и потратить " +
                 "на них перезарядку стража значит пропустить настоящий.")]
        public float turtleFanFromRage = 0.25f;
        [Tooltip("Как часто выбирается веер вместо одиночного броска.")]
        public float turtleFanChance = 0.35f;
        [Tooltip("Разлёт обманок от настоящего камня, градусы. Слишком мало — не отличить, " +
                 "слишком много — обманки очевидны и приём не работает.")]
        public float turtleFanSpread = 15f;

        [Tooltip("С какой доли выбитых черепах начинается таран панцирем: черепаха " +
                 "втягивается, раскручивается и катится через всю арену сквозь пузырёк.")]
        public float turtleChargeFromRage = 0.45f;
        [Tooltip("Как часто вместо броска идёт таран.")]
        public float turtleChargeChance = 0.30f;
        [Tooltip("Раскрутка перед тараном — окно реакции. Видно, кто именно пошёл.")]
        public float turtleChargeAimTime = 0.85f;
        [Tooltip("Скорость тарана, юнитов/сек.")]
        public float turtleChargeSpeed = 10f;
        [Tooltip("Радиус столкновения катящегося панциря.")]
        public float turtleChargeRadius = 0.46f;

        [Tooltip("Во сколько раз сжимается кольцо к концу боя. Меньше единицы — " +
                 "черепахи подступают ближе, и времени на камень остаётся меньше.")]
        public float turtleRingTighten = 0.84f;

        [Header("Босс: Белая смерть (акула)")]
        [Tooltip("Урон идёт только с блока тарана: охота за стражем — это испытание " +
                 "на выживание, а не окно для атаки. Поэтому HP заметно меньше, чем " +
                 "у боссов, которых можно бить каждый цикл.")]
        public int sharkHp = 6;
        [Tooltip("Длина акулы в долях игровой колонки. Она обязана читаться как крупный " +
                 "зверь: мелкая акула теряет всю угрозу и путается с пираньями.")]
        public float sharkLengthSpan = 0.81f;
        [Tooltip("Радиус рыла — им она таранит пузырёк.")]
        public float sharkNoseRadius = 0.38f;
        [Tooltip("Радиус раскрытой пасти. Больше рыла: промахнуться пастью по стражу " +
                 "должно быть тяжелее, чем рылом по пузырьку.")]
        public float sharkMawRadius = 0.52f;

        [Tooltip("Скорость патрулирования между атаками.")]
        public float sharkCruiseSpeed = 3.4f;
        [Tooltip("Сколько она кружит перед следующей атакой, при полном и последнем HP.")]
        public float sharkCruiseTimeMax = 1.6f;
        public float sharkCruiseTimeMin = 0.8f;
        [Tooltip("На каком расстоянии от пузырька она держится, пока не атакует.")]
        public float sharkOrbitSpan = 0.60f;

        [Tooltip("Скорость, с которой акула уходит за край экрана перед тараном.")]
        public float sharkRamRetreatSpeed = 11f;
        [Tooltip("Предохранитель на уход за кадр: если она почему-то не добралась до " +
                 "точки засады, бросок всё равно начнётся.")]
        public float sharkRamRetreatLimit = 2f;
        [Tooltip("Сколько акула ждёт за кадром перед броском. Пауза важнее, чем кажется: " +
                 "именно она превращает возвращение в бросок из засады, а не в проплывание.")]
        public float sharkRamLurkTime = 0.4f;
        [Tooltip("Скорость тарана, при полном и последнем HP.")]
        public float sharkRamSpeedMin = 14f;
        public float sharkRamSpeedMax = 19f;
        [Tooltip("Сколько она уходит по инерции после тарана, прежде чем развернуться.")]
        public float sharkRamRecoverTime = 0.75f;

        [Tooltip("Пасть раскрывается заранее — это сигнал, что теперь охота идёт за " +
                 "стражем, а не за пузырьком.")]
        public float sharkOpenTelegraph = 0.55f;
        [Tooltip("Сколько длится охота за стражем. Продержался — акула просто отстаёт " +
                 "и возвращается к тарану. Это чистое испытание на уклонение, и его " +
                 "длительность — главная ручка сложности всего босса.")]
        public float sharkHuntTime = 10f;
        [Tooltip("Длительность одного броска на стража.")]
        public float sharkDartTime = 0.30f;
        [Tooltip("Пауза между бросками, при полном и последнем HP. В неё игрок и уводит " +
                 "стража: направление броска фиксируется на старте и больше не доводится, " +
                 "иначе уклонение было бы невозможно в принципе.")]
        public float sharkDartGapMax = 0.34f;
        public float sharkDartGapMin = 0.20f;
        [Tooltip("Скорость броска на стража, при полном и последнем HP.")]
        public float sharkDartSpeedMin = 13.5f;
        public float sharkDartSpeedMax = 17.5f;

        [Tooltip("Сколько акула держит стража ПОСЛЕ удара по пузырьку, прежде чем " +
                 "отпустить. До удара отсчёт не идёт вовсе: раньше таймер мог истечь " +
                 "на подлёте, и захват заканчивался ничем.")]
        public float sharkBiteHoldTime = 0.6f;
        [Tooltip("Предохранитель на весь захват: вечного захвата быть не должно.")]
        public float sharkBiteFuse = 4f;
        [Tooltip("Скорость, с которой она несёт зажатого стража на пузырёк.")]
        public float sharkBiteRushSpeed = 9.5f;
        [Tooltip("Сколько акула отваливает после сорвавшейся охоты, прежде чем снова " +
                 "взяться за пузырёк.")]
        public float sharkWithdrawTime = 1.3f;

        [Header("Босс: Великая тень (финал)")]
        [Tooltip("HP каждого из трёх актов. Общая полоса делится на три равные части.")]
        public int mantaHpPerAct = 5;
        [Tooltip("Размах крыльев. Считается не от игровой колонки, а между ней и полной " +
                 "шириной экрана, поэтому на телефоне и на мониторе он занимает " +
                 "сопоставимую долю кадра. Больше единицы — кромки уходят за кадр.")]
        public float mantaSpanFactor = 2.37f;
        [Tooltip("Насколько тело уменьшено, пока висит в фоне: так читается глубина.")]
        public float mantaBackgroundScale = 0.78f;
        [Tooltip("Прозрачность тела в фоне. Он тень, а не ещё одна рыба на экране.")]
        public float mantaBackgroundAlpha = 0.85f;
        [Tooltip("Скорость дрейфа тела к новой точке зависания.")]
        public float mantaDriftSpeed = 1.9f;
        [Tooltip("Насколько скат сносит вбок в первом акте, в долях игровой колонки. " +
                 "Висящий строго над пузырьком, он давал одну верную позицию стража.")]
        public float mantaDriftSway = 0.62f;
        [Tooltip("Период взмаха крыльев, секунд.")]
        public float mantaFlapPeriod = 2.6f;
        [Tooltip("Пауза перед выходом: музыка молчит, тень наползает.")]
        public float mantaArrivalTime = 2.6f;
        [Tooltip("Сколько длится переворот брюхом перед третьим актом: он уходит вверх, " +
                 "переворачивается вне поля зрения и опускается уже брюхом вперёд.")]
        public float mantaTurnTime = 2.2f;

        [Tooltip("Акт 1. Сколько молний в очереди, при первом и последнем HP акта.")]
        public int mantaVolleyMin = 2;
        public int mantaVolleyMax = 4;
        [Tooltip("Пауза внутри очереди. Ниже перезарядки стража она всё равно не опустится.")]
        public float mantaVolleyGapMax = 0.62f;
        public float mantaVolleyGapMin = 0.42f;
        [Tooltip("Пауза между очередями — окно, чтобы перевести дух.")]
        public float mantaVolleyRestMax = 1.5f;
        public float mantaVolleyRestMin = 0.85f;
        [Tooltip("Разброс точек вылета по кромке крыла, в долях полуразмаха. Вместе со " +
                 "сносом туши это и даёт разные углы захода.")]
        public float mantaVolleySpread = 0.78f;

        [Tooltip("Радиус ядра — единственного места, куда проходит отражённый разряд " +
                 "в первом акте. Их два, по одному на крыло.")]
        public float mantaCoreRadius = 0.85f;
        [Tooltip("Как часто ядро перебирается на новое место, секунд.")]
        public float mantaCoreMoveTime = 3.2f;
        [Tooltip("Сколько ядро гаснет после попадания.")]
        public float mantaCoreHideTime = 1.1f;

        [Tooltip("Акт 2. Скорость прохода через арену.")]
        public float mantaPassSpeed = 13f;
        [Tooltip("Размер туши на проходе: в полный рост она накрывала весь экран.")]
        public float mantaPassScale = 0.6f;
        [Tooltip("Радиус головы на таране: в этих пределах страж считается принявшим удар.")]
        public float mantaRamRadius = 0.95f;
        [Tooltip("На какой доле пути хвост бьёт молнией вдогонку. Страж, только что " +
                 "принявший таран, стоит не с той стороны, и его надо успеть перебросить.")]
        [Range(0.3f, 0.9f)] public float mantaTailShotAt = 0.6f;

        [Tooltip("Акт 3. Размер туши, когда она нависает над игроком.")]
        public float mantaCloseScale = 0.62f;
        [Tooltip("На какой доле полуразмаха от центра туши крепится плеть.")]
        public float mantaLashRootSpan = 0.18f;
        [Tooltip("Запас длины плети сверх расстояния до пузырька. Дуга обязана проходить " +
                 "через пузырёк, поэтому запас небольшой.")]
        public float mantaLashReach = 1.12f;
        [Tooltip("Толщина плети в юнитах. Радиус столкновения задаётся отдельно и " +
                 "намеренно больше рисунка — в пользу игрока.")]
        public float mantaLashThickness = 0.3f;
        [Tooltip("Радиус жала: по нему засчитывается и блок стражем, и удар по пузырьку.")]
        public float mantaLashRadius = 0.7f;
        [Tooltip("Замах перед размахом хвоста — единственное окно, чтобы встретить его.")]
        public float mantaLashWindup = 0.85f;
        [Tooltip("Сколько длится сам размах.")]
        public float mantaLashSweep = 0.62f;
        [Tooltip("Дуга размаха в градусах: хвост проходит через пузырёк от края до края.")]
        public float mantaLashArc = 150f;

        [Tooltip("Сколько шаровых разрядов в граде.")]
        public int mantaOrbCount = 3;
        [Tooltip("Скорость шара. Он намеренно медленный: вся задача в том, какие гасить " +
                 "первыми, а не в том, чтобы успеть заметить.")]
        public float mantaOrbSpeed = 2.6f;
        public float mantaOrbRadius = 0.46f;
        [Tooltip("Пауза между шарами внутри града.")]
        public float mantaOrbGap = 0.6f;
        [Tooltip("Радиус кольца, по которому рождаются разряды, в долях игровой колонки. " +
                 "Они выходят из воды вокруг пузырька, а не из брюха.")]
        public float mantaOrbRingSpan = 0.75f;
        [Tooltip("Доля заряженных шаров. Погасив заряженный, страж отправляет разряд " +
                 "обратно в ската — это и есть урон в третьем акте.")]
        [Range(0f, 1f)] public float mantaOrbChargedShare = 0.4f;

        [Tooltip("Цепь: сколько искр летит по одной линии и с каким интервалом. Заряжена " +
                 "последняя. Интервал не опустится ниже перезарядки стража.")]
        public int mantaChainLinks = 2;
        public float mantaChainGap = 0.42f;
        public float mantaChainSpeed = 5.4f;
        public float mantaChainRadius = 0.24f;

        [Tooltip("Раскольник: через сколько секунд полёта он разлетается на три осколка " +
                 "и с каким разбросом в градусах. Целый он для ската безобиден.")]
        public float mantaSplitDelay = 0.75f;
        public float mantaSplitSpread = 26f;
        public float mantaSplitSpeed = 3.4f;
        public float mantaSplitRadius = 0.5f;

        [Tooltip("Разряды: страж отражает их обратно, как у грозовой медузы.")]
        public float mantaBoltRadius = 0.26f;
        public float mantaBoltSpeed = 5.2f;
        [Tooltip("Пауза между одиночными разрядами.")]
        public float mantaBoltGapMax = 1.7f;
        public float mantaBoltGapMin = 0.95f;
        [Tooltip("Скорость отражённого разряда и его доводка на цель, град/сек.")]
        public float mantaReflectSpeed = 13f;
        public float mantaReflectHoming = 160f;
        [Tooltip("Доводка отражённого в первом акте. Намеренно слабая: молния отскакивает " +
                 "от стража как от зеркала, и попасть в ядро — задача игрока.")]
        public float mantaMirrorHoming = 35f;

        [Tooltip("Удар хвостом: замах с подсветкой линии.")]
        public float mantaTailAimTime = 0.8f;
        public float mantaTailSpeed = 13f;
        public float mantaTailRadius = 0.34f;
        [Tooltip("Пауза внутри серии ударов хвостом.")]
        public float mantaTailBurstGap = 0.55f;
        [Tooltip("Веер шипов: настоящий только один, остальные обманки.")]
        public float mantaFanSpread = 16f;
        public int mantaFanCount = 3;
        [Tooltip("Сколько детёнышей выходит строем.")]
        public int mantaPupCount = 6;
        public float mantaPupSize = 0.2f;
        public float mantaPupSpeed = 5.5f;
        [Tooltip("Сколько держится затемнение, когда крыло накрывает экран.")]
        public float mantaEclipseHold = 1.5f;
        public float mantaEclipseFade = 1.2f;

        [Header("Босс: Чернильный морок")]
        public int inkHp = 8;
        [Tooltip("Размер мантии в долях игровой колонки.")]
        public float inkBodySpan = 0.62f;
        [Tooltip("Точка зависания: доля высоты экрана сверху.")]
        public float inkHoverFactor = 0.22f;
        [Tooltip("Скорость дрейфа к новой позиции, юнитов/сек.")]
        public float inkDriftSpeed = 2.6f;

        [Tooltip("Замах перед плевком — единственное предупреждение, что сейчас зальёт экран.")]
        public float inkChargeTime = 0.85f;
        [Tooltip("Полёт сгустка к камере.")]
        public float inkSpitTime = 0.40f;

        [Tooltip("Сколько экран закрыт наглухо, при полном и при последнем HP. " +
                 "В это время не видно ничего: ни стража, ни сцены. Искать нечего — " +
                 "это пауза вслепую, и она же главная ручка сложности боя.")]
        public float inkBlackoutTimeMin = 1.2f;
        public float inkBlackoutTimeMax = 1.9f;
        [Tooltip("Плотность заливки. Единица — полная темнота; чуть меньше оставляет " +
                 "сцену едва угадываемой, и это читается лучше глухого прямоугольника.")]
        public float inkBlackoutAlpha = 0.94f;
        [Tooltip("За сколько секунд чернила сходят после глухой фазы.")]
        public float inkFadeTime = 0.9f;

        [Tooltip("Окно на поиск стража и вывод его на линию удара. Считается с момента, " +
                 "когда чернила начали сходить и картинка вернулась: искать в темноте " +
                 "нечего, поиск начинается вместе с обзором.")]
        public float inkSearchTimeMax = 2.8f;
        public float inkSearchTimeMin = 1.7f;

        [Tooltip("Скорость чернильной струи, юнитов/сек.")]
        public float inkStrikeSpeed = 9f;
        [Tooltip("Радиус столкновения струи.")]
        public float inkJetRadius = 0.42f;
        [Tooltip("Передышка после удара.")]
        public float inkRestTime = 1.1f;

        [Header("Босс: Спрут глубин")]
        // Наверху висит только голова. Щупальца к ней не крепятся: они торчат из-за краёв
        // экрана по всему периметру, так что спрут окружает арену целиком.
        // Размеры — в долях игровой колонки (6.4 юнита), чтобы читались на любом экране.
        public int octopusHp = 9;
        [Tooltip("Точка зависания головы: доля высоты экрана сверху. Голова крупная, " +
                 "выше — залезет на полосу HP.")]
        public float octopusHeadHover = 0.30f;
        [Tooltip("Радиус головы в долях игровой колонки.")]
        public float octopusHeadRadiusSpan = 0.28f;

        [Tooltip("Сколько щупалец торчит из-за краёв. Они раскиданы по всей окружности, " +
                 "так что спрут держит арену со всех сторон разом.")]
        [Range(4, 14)] public int octopusArmsTotal = 10;
        [Tooltip("Звеньев в щупальце. Они тянутся из-за экрана, поэтому звеньев нужно много, " +
                 "иначе цепочка разрывается на бусины.")]
        [Range(8, 40)] public int octopusArmSegments = 26;
        public float octopusArmWidth = 0.42f;
        [Tooltip("Насколько далеко за краем экрана прячутся основания щупалец.")]
        public float octopusAnchorMargin = 1.6f;
        [Tooltip("Радиус кольца, по которому кончики щупалец висят в покое, " +
                 "в долях игровой колонки. Они смыкаются вокруг пузырька.")]
        public float octopusIdleRingSpan = 0.55f;
        [Tooltip("Разброс глубины захода щупалец: часть подбирается вплотную, часть " +
                 "остаётся у краёв. Без него все кончики висели бы на одной окружности.")]
        [Range(0f, 0.8f)] public float octopusIdleRingSpread = 0.45f;
        [Tooltip("На каком расстоянии от пузырька щупальце замирает перед ударом, " +
                 "в долях игровой колонки. Именно туда игрок должен подвести стража.")]
        public float octopusStrikeSpan = 0.45f;
        [Tooltip("Разброс точки удара вокруг направления своего щупальца. Каждое бьёт " +
                 "со своей стороны — видно, какое именно замахнулось.")]
        public float octopusStrikeArc = 50f;
        [Tooltip("Радиус столкновения кончика щупальца.")]
        public float octopusTipRadius = 0.30f;

        public float octopusAttackIntervalMax = 2.4f;
        public float octopusAttackIntervalMin = 1.3f;
        [Tooltip("Замах — окно реакции. Щупальце заранее встаёт на позицию удара.")]
        public float octopusWindupMax = 0.85f;
        public float octopusWindupMin = 0.55f;
        public float octopusStrikeTime = 0.30f;
        public float octopusHoldTime = 0.12f;
        public float octopusRetractTime = 0.60f;
        [Tooltip("Задержка между щупальцами в серийной атаке. Должна быть больше перезарядки " +
                 "стража, иначе серию физически нечем отбить.")]
        public float octopusSerialDelay = 0.55f;
        public float octopusSwaySpeed = 0.7f;
        public float octopusSwayAmplitude = 0.32f;

        [Header("Грозовая медуза: паттерны молний")]
        [Tooltip("Каждый N-й залп — веер из трёх снарядов.")]
        public int bossSpreadEveryNShots = 3;
        public float bossSpreadAngle = 20f;
        [Tooltip("Когда HP босса ниже половины, каждый N-й залп — круговая волна.")]
        public int bossRingEveryNShots = 4;
        public int bossRingCount = 8;

        [Header("Декорации (ощущение подъёма)")]
        [Tooltip("Средний интервал между декорациями, сек.")]
        public float sceneryInterval = 2.3f;
        [Tooltip("Сколько декораций расставить на экране при старте партии.")]
        public int sceneryPrefillCount = 4;
        public float sceneryNearParallaxMin = 0.85f;
        public float sceneryNearParallaxMax = 1.05f;
        [Tooltip("Дальний план (постройки, арки) движется медленнее — отсюда чувство глубины.")]
        public float sceneryFarParallaxMin = 0.45f;
        public float sceneryFarParallaxMax = 0.65f;
        [Tooltip("Интервал между боковыми зарослями. Они появляются только на широких экранах, " +
                 "где игровая колонка не закрывает всю ширину, и заполняют пустые бока.")]
        public float sceneryEdgeInterval = 0.85f;

        [Header("Крабы на боковых камнях")]
        [Tooltip("Средний интервал между крабами, сек. 0 — отключить их совсем.")]
        public float sideCrabInterval = 60f;
        [Tooltip("Размер краба в юнитах (по ширине спрайта).")]
        public float sideCrabSizeMin = 0.85f;
        public float sideCrabSizeMax = 1.40f;
        [Tooltip("Своя скорость ползания по стене, юнитов/сек. Держите заметно ниже " +
                 "baseScrollSpeed: иначе краб зависнет на экране и подъём перестанет читаться.")]
        public float sideCrabCrawlMin = 0.20f;
        public float sideCrabCrawlMax = 0.50f;
        [Tooltip("С какой вероятностью краб ползёт вверх по стене, а не вниз.")]
        public float sideCrabUpwardChance = 0.35f;
        [Tooltip("Наклон краба к стене, градусы. 90 — смотрит строго внутрь арены, " +
                 "меньше — вверх и внутрь.")]
        public float sideCrabTiltMin = 55f;
        public float sideCrabTiltMax = 76f;

        [Header("Фон: жизнь океана")]
        [Tooltip("Сколько лучей света падает с поверхности. 0 — отключить.")]
        [Range(0, 8)] public int lightShaftCount = 4;
        [Range(0f, 1f)] public float lightShaftIntensity = 0.55f;
        [Tooltip("Размер стайки декоративных рыбёшек. 0 — отключить.")]
        [Range(0, 20)] public int schoolSize = 9;
        public float schoolIntervalMin = 7f;
        public float schoolIntervalMax = 16f;
        [Tooltip("Светящийся планктон. В отличие от лучей света, ярче на глубине. 0 — отключить.")]
        [Range(0, 80)] public int planktonCount = 34;
        [Range(0f, 1f)] public float planktonIntensity = 0.7f;
        [Tooltip("Пузырьковые струи со дна, проносящиеся по краям экрана.")]
        public float ventIntervalMin = 6f;
        public float ventIntervalMax = 13f;
        [Range(3, 24)] public int ventBubbleCount = 10;
        [Tooltip("Далёкие силуэты китов и скатов в глубине кадра.")]
        public float silhouetteIntervalMin = 22f;
        public float silhouetteIntervalMax = 45f;

        [Header("Фон")]
        public int backgroundBubbleCount = 26;
        [Tooltip("Насколько быстро частицы воды проносятся вниз относительно прокрутки.")]
        public float backgroundParallaxMin = 0.45f;
        public float backgroundParallaxMax = 1.15f;
        [Tooltip("Собственная плавучесть частиц (вверх). Меньше скорости прокрутки — иначе " +
                 "фон поедет вверх и будет казаться, что мы опускаемся.")]
        public float backgroundBuoyancy = 0.3f;

        [Header("След пузырька")]
        [Tooltip("Интервал между пузырьками следа, сек. 0 — выключить след.")]
        public float trailInterval = 0.055f;
        public float trailLifetime = 1.1f;
        [Range(0f, 1f)] public float trailAlpha = 0.45f;
        [Tooltip("На какой глубине (в метрах) фон полностью «просветляется».")]
        public float surfaceDepth = 2200f;

        [Header("Эффекты")]
        public int maxParticles = 340;

        [Header("Звук")]
        [Tooltip("Полностью отключить звуковую подсистему (отдельно от кнопки-глушилки у игрока).")]
        public bool audioEnabled = true;
        public bool musicEnabled = true;
        [Range(0f, 1f)] public float sfxVolume = 0.55f;
        [Range(0f, 1f)] public float musicVolume = 0.26f;
        [Tooltip("Громкость боевой темы. Обычно чуть выше спокойной — бой должен давить.")]
        [Range(0f, 1f)] public float bossMusicVolume = 0.32f;
        [Tooltip("Тема финального босса. Громче остальных намеренно: она должна " +
                 "давить, а не подыгрывать — на общем уровне её просто не замечали.")]
        [Range(0f, 1f)] public float finaleMusicVolume = 0.46f;
        [Range(0f, 1f)] public float ambientVolume = 0.20f;
        [Tooltip("Через сколько метров звучит колокольчик-веха.")]
        public float milestoneEvery = 500f;
        [Tooltip("Интервал сердцебиения, когда осталась одна жизнь. 0 — отключить.")]
        public float heartbeatInterval = 1.15f;
        [Tooltip("Средний интервал бульканья пузырька. 0 — отключить.")]
        public float bubbleBlipInterval = 1.4f;

        [Header("Разработка")]
        [Tooltip("Панель разработчика: прыжок к боссам, спавн контента, бессмертие, ускорение " +
                 "времени. Открывается по F1 или кнопке DEV в углу.\n\n" +
                 "В релизной сборке её нет в принципе: весь код обёрнут в " +
                 "#if UNITY_EDITOR || DEVELOPMENT_BUILD и вырезается препроцессором. " +
                 "Эта галка нужна, только чтобы убрать панель с глаз в редакторе.")]
        public bool devTools = true;

        [Header("Яндекс Игры")]
        [Tooltip("Межстраничная реклама показывается на каждом N-м экране поражения.")]
        public int interstitialEveryNDeaths = 3;
        [Tooltip("Межстраничная на переходах: старт партии, возврат в меню, вход в магазин. " +
                 "Это логические паузы — показывать рекламу внутри партии площадка запрещает. " +
                 "Реальную частоту всё равно ограничивает шаблон: не чаще раза в минуту и " +
                 "никогда в первую минуту сессии.")]
        public bool interstitialOnRunStart = true;
        public bool interstitialOnMenu = true;
        public bool interstitialOnShop = true;
        [Tooltip("Баннер RTB на экранах вне партии. В геймплее он скрывается: там он " +
                 "перекрывал бы поле, по которому игрок водит стража.")]
        public bool bannerEnabled = true;
        [Tooltip("Показывать на экране поражения кнопку «доп. жизнь за рекламу».")]
        public bool rewardedExtraLifeEnabled = true;
        public string leaderboardName = "depth";
    }
}
