using UnityEngine;
using UnityEngine.UI;

namespace BubbleRise
{
    /// <summary>
    /// Вступление: три экрана при первом запуске.
    ///
    /// **Почему вообще нужно.** Управление в этой игре не очевидно: пузырёк плывёт
    /// сам, а палец водит другой шарик — стража. Без объяснения новичок первые
    /// заплывы пытается управлять пузырьком, ничего не выходит, и игра кажется
    /// сломанной, а не сложной.
    ///
    /// **Почему именно три.** Ровно три вещи, без которых играть нельзя: чем
    /// управляешь, зачем это нужно, откуда монеты. Четвёртый экран был бы уже про
    /// боссов или магазин — а это игрок прекрасно узнает сам, когда дойдёт.
    ///
    /// **Почему не подсказки прямо в игре.** Всплывающая подсказка поверх заплыва
    /// закрывает препятствия, а первый заплыв и без того самый трудный. Три экрана
    /// до начала стоят десяти секунд и ничего не портят.
    ///
    /// Пропускается одним нажатием и больше не возвращается: отметка едет в облако
    /// вместе с остальным профилем.
    /// </summary>
    public partial class UIManager
    {
        const int TutorialPages = 3;

        GameObject _tutorialScreen;
        readonly GameObject[] _tutorialPages = new GameObject[TutorialPages];
        Text _tutorialNext;
        int _tutorialPage;

        /// <summary>Вступление закрыто — можно показывать всё остальное.</summary>
        public event System.Action TutorialFinished;

        /// <summary>Нужно ли вообще показывать: один раз за всю жизнь профиля.</summary>
        public static bool TutorialPending => PlayerPrefs.GetInt(SaveData.TutorialKey, 0) == 0;

        void BuildTutorialScreen()
        {
            _tutorialScreen = BuildScreenRoot("TutorialScreen");

            var content = NewRect("Content", _tutorialScreen.transform);
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 0.5f);
            content.pivot = new Vector2(0.5f, 0.5f);
            content.sizeDelta = new Vector2(ScreenWidth, 900f);

            string[] titles = { "tutor.title1", "tutor.title2", "tutor.title3" };
            string[] bodies = { "tutor.body1", "tutor.body2", "tutor.body3" };

            for (int i = 0; i < TutorialPages; i++)
            {
                var page = NewRect("Page" + i, content);
                Stretch(page);
                _tutorialPages[i] = page.gameObject;

                BuildTutorialArt(page, i);

                var title = NewText("Title", page, Loc.Get(titles[i]), 44, Palette.Aqua, TextAnchor.MiddleCenter);
                Anchor(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f),
                       new Vector2(ScreenWidth, 60f));
                title.fontStyle = FontStyle.Bold;

                var body = NewText("Body", page, Loc.Get(bodies[i]), 26,
                                   Palette.Foam.WithAlpha(0.88f), TextAnchor.UpperCenter);
                Anchor(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -110f),
                       new Vector2(ScreenWidth - 40f, 140f));
                body.horizontalOverflow = HorizontalWrapMode.Wrap;
            }

            // Точки-страницы: без них непонятно, сколько ещё осталось терпеть.
            for (int i = 0; i < TutorialPages; i++)
            {
                var dot = NewRect("Dot" + i, content);
                Anchor(dot, new Vector2(0.5f, 0.5f),
                       new Vector2((i - 1) * 28f, -270f), new Vector2(12f, 12f));
                var image = dot.gameObject.AddComponent<Image>();
                image.sprite = SpriteFactory.Disc();
                image.color = Palette.Foam.WithAlpha(0.3f);
                image.raycastTarget = false;
                _tutorialDots[i] = image;
            }

            var next = NewButton("Next", content, Loc.Get("tutor.next"), Palette.Aqua, Palette.Deep);
            Anchor(next.rt, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(380f, 96f));
            next.text.fontSize = 32;
            next.button.onClick.AddListener(() => { ButtonClicked?.Invoke(); NextTutorialPage(); });
            _tutorialNext = next.text;

            // «Пропустить» мелкое и в углу: выход должен быть, но предлагать его
            // наравне с «дальше» — значит советовать пропустить объяснение.
            var skip = NewButton("Skip", content, Loc.Get("tutor.skip"),
                                 Palette.Foam.WithAlpha(0.2f), Palette.Foam);
            Anchor(skip.rt, new Vector2(0.5f, 1f), new Vector2(150f, -24f), new Vector2(180f, 56f));
            skip.text.fontSize = 20;
            skip.button.onClick.AddListener(() => { ButtonClicked?.Invoke(); FinishTutorial(); });

            _tutorialScreen.SetActive(false);
        }

        readonly Image[] _tutorialDots = new Image[TutorialPages];

        /// <summary>
        /// Картинка страницы. Рисуется из тех же спрайтов, что и сама игра: палец у
        /// стража, страж у препятствия, монета. Показывать в объяснении не те
        /// предметы, которые игрок потом увидит, — верный способ объяснить не ту
        /// игру.
        /// </summary>
        void BuildTutorialArt(RectTransform page, int index)
        {
            var art = NewRect("Art", page);
            Anchor(art, new Vector2(0.5f, 0.5f), new Vector2(0f, 200f), new Vector2(ScreenWidth, 220f));

            switch (index)
            {
                case 0:
                {
                    // Пузырёк сверху, страж под ним, стрелка движения.
                    Icon(art, SpriteFactory.Bubble(), new Vector2(0f, 60f), 92f, Palette.Aqua);
                    Icon(art, SpriteFactory.GuardianGlass(), new Vector2(-60f, -40f), 78f, Color.white);
                    Icon(art, SpriteFactory.AimChevron(), new Vector2(40f, -40f), 54f, Palette.Gold);
                    break;
                }
                case 1:
                {
                    Icon(art, SpriteFactory.GuardianGlass(), new Vector2(-70f, 0f), 86f, Color.white);
                    Icon(art, SpriteFactory.Ring(), new Vector2(0f, 0f), 70f, Palette.Gold);
                    Icon(art, SpriteFactory.Rock(), new Vector2(72f, 0f), 80f, Color.white);
                    break;
                }
                default:
                {
                    Icon(art, SpriteFactory.Rock(), new Vector2(-80f, 0f), 72f, Color.white);
                    Icon(art, SpriteFactory.Coin(), new Vector2(10f, 10f), 64f, Color.white);
                    Icon(art, SpriteFactory.Coin(), new Vector2(70f, -26f), 48f, Color.white);
                    break;
                }
            }
        }

        void Icon(RectTransform parent, Sprite sprite, Vector2 position, float size, Color tint)
        {
            var rt = NewRect("Icon", parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = new Vector2(size, size);

            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = tint;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        /// <summary>Показать вступление, если его ещё не видели.</summary>
        public bool TryShowTutorial()
        {
            if (!TutorialPending || _tutorialScreen == null) return false;

            SetScreen(7);
            HideAllScreens();
            _tutorialPage = 0;
            ShowTutorialPage(0);
            _tutorialScreen.SetActive(true);
            _tutorialScreen.transform.SetAsLastSibling();
            return true;
        }

        void ShowTutorialPage(int index)
        {
            for (int i = 0; i < TutorialPages; i++)
            {
                if (_tutorialPages[i] != null) _tutorialPages[i].SetActive(i == index);
                if (_tutorialDots[i] != null)
                    _tutorialDots[i].color = i == index ? Palette.Aqua : Palette.Foam.WithAlpha(0.3f);
            }

            if (_tutorialNext != null)
                _tutorialNext.text = Loc.Get(index == TutorialPages - 1 ? "tutor.play" : "tutor.next");
        }

        void NextTutorialPage()
        {
            _tutorialPage++;
            if (_tutorialPage >= TutorialPages) { FinishTutorial(); return; }
            ShowTutorialPage(_tutorialPage);
        }

        void FinishTutorial()
        {
            PlayerPrefs.SetInt(SaveData.TutorialKey, 1);
            PlayerPrefs.Save();

            if (_tutorialScreen != null) _tutorialScreen.SetActive(false);
            ShowStart();
            TutorialFinished?.Invoke();
        }
    }
}
