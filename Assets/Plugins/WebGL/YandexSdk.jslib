// Мост Unity -> Yandex Games SDK.
// Вся JS-логика намеренно вынесена в WebGL-шаблон (window.BubbleRiseYSDK),
// здесь только тонкие вызовы: так мост не ломается при обновлении SDK и
// его можно править без пересборки проекта.

mergeInto(LibraryManager.library, {

  YG_IsAvailable: function () {
    try {
      return (typeof window !== 'undefined' && window.BubbleRiseYSDK && window.BubbleRiseYSDK.isReady()) ? 1 : 0;
    } catch (e) {
      return 0;
    }
  },

  YG_GameReady: function () {
    try { window.BubbleRiseYSDK.gameReady(); } catch (e) { console.warn('[YSDK] gameReady', e); }
  },

  YG_GameplayStart: function () {
    try { window.BubbleRiseYSDK.gameplayStart(); } catch (e) { console.warn('[YSDK] gameplayStart', e); }
  },

  YG_GameplayStop: function () {
    try { window.BubbleRiseYSDK.gameplayStop(); } catch (e) { console.warn('[YSDK] gameplayStop', e); }
  },

  YG_ShowInterstitial: function () {
    try { window.BubbleRiseYSDK.showInterstitial(); } catch (e) { console.warn('[YSDK] interstitial', e); }
  },

  YG_ShowRewarded: function () {
    try { window.BubbleRiseYSDK.showRewarded(); } catch (e) { console.warn('[YSDK] rewarded', e); }
  },

  YG_ShowBanner: function () {
    try { window.BubbleRiseYSDK.showBanner(); } catch (e) { console.warn('[YSDK] showBanner', e); }
  },

  YG_HideBanner: function () {
    try { window.BubbleRiseYSDK.hideBanner(); } catch (e) { console.warn('[YSDK] hideBanner', e); }
  },

  YG_SaveState: function (jsonPtr) {
    try {
      window.BubbleRiseYSDK.saveState(UTF8ToString(jsonPtr));
    } catch (e) {
      console.warn('[YSDK] saveState', e);
    }
  },

  YG_RequestLang: function () {
    try { window.BubbleRiseYSDK.requestLang(); } catch (e) { console.warn('[YSDK] requestLang', e); }
  },

  YG_LoadState: function () {
    try { window.BubbleRiseYSDK.loadState(); } catch (e) { console.warn('[YSDK] loadState', e); }
  },

  YG_SubmitScore: function (boardPtr, value) {
    try {
      var board = UTF8ToString(boardPtr);
      window.BubbleRiseYSDK.submitScore(board, value);
    } catch (e) {
      console.warn('[YSDK] submitScore', e);
    }
  }
});
