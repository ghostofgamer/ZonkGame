// Мост между C# и Yandex Games SDK.
// Все вызовы асинхронные: C# передаёт requestId, JS отвечает через Module.SendMessage
// на объект YandexSdkBridge, метод OnBridgeResponse. События рекламы идут в OnBridgeEvent.
// Ответ всегда имеет вид {id, ok, result, error}, где result это строка с JSON.

mergeInto(LibraryManager.library, {

  $YandexBridge: {
    RECEIVER: 'YandexSdkBridge',
    SAVE_KEY: 'base_save',

    sdk: null,
    player: null,
    payments: null,

    err: function (e) {
      if (!e) return 'unknown error';
      if (typeof e === 'string') return e;
      return e.message || e.code || JSON.stringify(e);
    },

    respond: function (id, ok, result, error) {
      var payload = JSON.stringify({
        id: id,
        ok: !!ok,
        result: result || '',
        error: error || ''
      });
      Module.SendMessage(YandexBridge.RECEIVER, 'OnBridgeResponse', payload);
    },

    ok: function (id, obj) {
      YandexBridge.respond(id, true, JSON.stringify(obj || {}), null);
    },

    fail: function (id, e) {
      YandexBridge.respond(id, false, null, YandexBridge.err(e));
    },

    event: function (name) {
      Module.SendMessage(YandexBridge.RECEIVER, 'OnBridgeEvent', name);
    },

    // Проверка перед любым вызовом, кроме инициализации.
    ready: function (id) {
      if (!YandexBridge.sdk) {
        YandexBridge.fail(id, 'SDK is not initialized');
        return false;
      }
      return true;
    },

    playerInfo: function () {
      var sdk = YandexBridge.sdk;
      var p = YandexBridge.player;
      var authorized = false;
      try { authorized = p ? p.isAuthorized() : false; } catch (e) { authorized = false; }

      // Язык страница уже прочитала на старте, см. index.html. Здесь только запасной путь.
      var lang = (typeof window !== 'undefined' && window.ysdkLang) || null;
      if (!lang) {
        try { lang = sdk.environment.i18n.lang || 'ru'; } catch (e) { lang = 'ru'; }
      }

      // desktop, mobile, tablet или tv. По нему игра выбирает качество графики.
      var device = '';
      try { device = (sdk.deviceInfo && sdk.deviceInfo.type) || ''; } catch (e) { device = ''; }

      var id = '';
      var name = '';
      try { id = p ? (p.getUniqueID() || '') : ''; } catch (e) { id = ''; }
      try { name = (p && authorized) ? (p.getName() || '') : ''; } catch (e) { name = ''; }

      return { lang: lang, device: device, isAuthorized: authorized, playerId: id, playerName: name };
    },

    // getPlayer со scopes: false не показывает окно разрешений при загрузке.
    loadPlayer: function () {
      return YandexBridge.sdk.getPlayer({ scopes: false })
        .then(function (player) { YandexBridge.player = player; })
        .catch(function () { YandexBridge.player = null; });
    },

    // Документация SDK менялась: раньше колбэки лежали в callbacks, сейчас в корне.
    // Передаём оба варианта, лишние поля SDK игнорирует.
    withCallbacks: function (handlers) {
      var options = {};
      for (var key in handlers) {
        if (handlers.hasOwnProperty(key)) options[key] = handlers[key];
      }
      options.callbacks = handlers;
      return options;
    },

    leaderboards: function () {
      var sdk = YandexBridge.sdk;
      if (sdk.leaderboards) return Promise.resolve(sdk.leaderboards);
      return sdk.getLeaderboards();
    },

    // Новый API: setScore/getEntries/getPlayerEntry. Старый: setLeaderboardScore и т.д.
    lbCall: function (lb, modern, legacy, args) {
      if (typeof lb[modern] === 'function') return lb[modern].apply(lb, args);
      return lb[legacy].apply(lb, args);
    },

    mapEntries: function (response) {
      var myId = '';
      try { myId = YandexBridge.player ? YandexBridge.player.getUniqueID() : ''; } catch (e) { myId = ''; }

      var source = (response && response.entries) || [];
      var items = [];
      for (var i = 0; i < source.length; i++) {
        var entry = source[i];
        var player = entry.player || {};
        var uid = player.uniqueID || '';
        items.push({
          rank: entry.rank || 0,
          score: entry.score || 0,
          playerName: player.publicName || '',
          isCurrentPlayer: !!myId && uid === myId
        });
      }
      return { items: items };
    }
  },

  // ---------- Жизненный цикл ----------

  YandexBridgeInit__deps: ['$YandexBridge'],
  YandexBridgeInit: function (requestId) {
    try {
      // Страница инициализирует SDK сама и кладёт промис в window.ysdkReady.
      // YaGames.init() здесь это запасной путь для шаблона без такой инициализации.
      var promise = null;
      if (typeof window !== 'undefined' && window.ysdkReady) {
        promise = window.ysdkReady;
      } else if (typeof YaGames !== 'undefined') {
        promise = YaGames.init();
      } else {
        YandexBridge.fail(requestId, 'YaGames is not defined: sdk.js is not connected in index.html');
        return;
      }

      promise
        .then(function (sdk) {
          YandexBridge.sdk = sdk;
          return YandexBridge.loadPlayer();
        })
        .then(function () {
          YandexBridge.ok(requestId, YandexBridge.playerInfo());
        })
        .catch(function (e) { YandexBridge.fail(requestId, e); });
    } catch (e) {
      YandexBridge.fail(requestId, e);
    }
  },

  YandexBridgeGameReady__deps: ['$YandexBridge'],
  YandexBridgeGameReady: function () {
    try {
      var f = YandexBridge.sdk && YandexBridge.sdk.features;
      if (f && f.LoadingAPI && f.LoadingAPI.ready) f.LoadingAPI.ready();
    } catch (e) { console.error('[Yandex] LoadingAPI.ready failed', e); }
  },

  YandexBridgeGameplayStart__deps: ['$YandexBridge'],
  YandexBridgeGameplayStart: function () {
    try {
      var f = YandexBridge.sdk && YandexBridge.sdk.features;
      if (f && f.GameplayAPI && f.GameplayAPI.start) f.GameplayAPI.start();
    } catch (e) { console.error('[Yandex] GameplayAPI.start failed', e); }
  },

  YandexBridgeGameplayStop__deps: ['$YandexBridge'],
  YandexBridgeGameplayStop: function () {
    try {
      var f = YandexBridge.sdk && YandexBridge.sdk.features;
      if (f && f.GameplayAPI && f.GameplayAPI.stop) f.GameplayAPI.stop();
    } catch (e) { console.error('[Yandex] GameplayAPI.stop failed', e); }
  },

  YandexBridgeAuthorize__deps: ['$YandexBridge'],
  YandexBridgeAuthorize: function (requestId) {
    if (!YandexBridge.ready(requestId)) return;
    try {
      YandexBridge.sdk.auth.openAuthDialog()
        .then(function () { return YandexBridge.sdk.getPlayer(); })
        .then(function (player) {
          YandexBridge.player = player;
          YandexBridge.ok(requestId, YandexBridge.playerInfo());
        })
        .catch(function (e) { YandexBridge.fail(requestId, e); });
    } catch (e) {
      YandexBridge.fail(requestId, e);
    }
  },

  // ---------- Реклама ----------

  YandexBridgeShowInterstitial__deps: ['$YandexBridge'],
  YandexBridgeShowInterstitial: function (requestId) {
    if (!YandexBridge.ready(requestId)) return;
    try {
      var settled = false;
      var opened = false;

      var finish = function (ok, wasShown, error) {
        if (settled) return;
        settled = true;
        if (opened) YandexBridge.event('adClosed');
        if (ok) YandexBridge.ok(requestId, { shown: !!wasShown });
        else YandexBridge.fail(requestId, error);
      };

      YandexBridge.sdk.adv.showFullscreenAdv(YandexBridge.withCallbacks({
        onOpen: function () { opened = true; YandexBridge.event('adOpened'); },
        onClose: function (wasShown) { finish(true, wasShown); },
        onError: function (e) { finish(false, false, e); }
      }));
    } catch (e) {
      YandexBridge.fail(requestId, e);
    }
  },

  YandexBridgeShowRewarded__deps: ['$YandexBridge'],
  YandexBridgeShowRewarded: function (requestId) {
    if (!YandexBridge.ready(requestId)) return;
    try {
      var settled = false;
      var opened = false;
      var rewarded = false;

      var finish = function (ok, wasShown, error) {
        if (settled) return;
        settled = true;
        if (opened) YandexBridge.event('adClosed');
        if (ok) YandexBridge.ok(requestId, { rewarded: rewarded, shown: !!wasShown });
        else YandexBridge.fail(requestId, error);
      };

      YandexBridge.sdk.adv.showRewardedVideo(YandexBridge.withCallbacks({
        onOpen: function () { opened = true; YandexBridge.event('adOpened'); },
        onRewarded: function () { rewarded = true; },
        onClose: function (wasShown) { finish(true, wasShown); },
        onError: function (e) { finish(false, false, e); }
      }));
    } catch (e) {
      YandexBridge.fail(requestId, e);
    }
  },

  // ---------- Покупки ----------

  YandexBridgeInitPayments__deps: ['$YandexBridge'],
  YandexBridgeInitPayments: function (requestId) {
    if (!YandexBridge.ready(requestId)) return;
    try {
      if (YandexBridge.payments) {
        YandexBridge.ok(requestId, { available: true });
        return;
      }
      YandexBridge.sdk.getPayments({ signed: false })
        .then(function (payments) {
          YandexBridge.payments = payments;
          YandexBridge.ok(requestId, { available: true });
        })
        .catch(function (e) { YandexBridge.fail(requestId, e); });
    } catch (e) {
      YandexBridge.fail(requestId, e);
    }
  },

  YandexBridgeGetCatalog__deps: ['$YandexBridge'],
  YandexBridgeGetCatalog: function (requestId) {
    if (!YandexBridge.ready(requestId)) return;
    if (!YandexBridge.payments) { YandexBridge.fail(requestId, 'payments are not initialized'); return; }
    try {
      YandexBridge.payments.getCatalog()
        .then(function (catalog) {
          var items = [];
          for (var i = 0; i < catalog.length; i++) {
            var p = catalog[i];
            items.push({
              id: p.id || '',
              title: p.title || '',
              description: p.description || '',
              price: p.price || '',
              imageURI: p.imageURI || ''
            });
          }
          YandexBridge.ok(requestId, { items: items });
        })
        .catch(function (e) { YandexBridge.fail(requestId, e); });
    } catch (e) {
      YandexBridge.fail(requestId, e);
    }
  },

  YandexBridgePurchase__deps: ['$YandexBridge'],
  YandexBridgePurchase: function (requestId, productIdPtr) {
    if (!YandexBridge.ready(requestId)) return;
    if (!YandexBridge.payments) { YandexBridge.fail(requestId, 'payments are not initialized'); return; }
    try {
      var productId = UTF8ToString(productIdPtr);
      YandexBridge.payments.purchase({ id: productId })
        .then(function (purchase) {
          YandexBridge.ok(requestId, {
            productID: purchase.productID || productId,
            purchaseToken: purchase.purchaseToken || ''
          });
        })
        .catch(function (e) { YandexBridge.fail(requestId, e); });
    } catch (e) {
      YandexBridge.fail(requestId, e);
    }
  },

  YandexBridgeGetPurchases__deps: ['$YandexBridge'],
  YandexBridgeGetPurchases: function (requestId) {
    if (!YandexBridge.ready(requestId)) return;
    if (!YandexBridge.payments) { YandexBridge.fail(requestId, 'payments are not initialized'); return; }
    try {
      YandexBridge.payments.getPurchases()
        .then(function (purchases) {
          var items = [];
          for (var i = 0; i < purchases.length; i++) {
            items.push({
              productID: purchases[i].productID || '',
              purchaseToken: purchases[i].purchaseToken || ''
            });
          }
          YandexBridge.ok(requestId, { items: items });
        })
        .catch(function (e) { YandexBridge.fail(requestId, e); });
    } catch (e) {
      YandexBridge.fail(requestId, e);
    }
  },

  YandexBridgeConsume__deps: ['$YandexBridge'],
  YandexBridgeConsume: function (requestId, tokenPtr) {
    if (!YandexBridge.ready(requestId)) return;
    if (!YandexBridge.payments) { YandexBridge.fail(requestId, 'payments are not initialized'); return; }
    try {
      YandexBridge.payments.consumePurchase(UTF8ToString(tokenPtr))
        .then(function () { YandexBridge.ok(requestId, {}); })
        .catch(function (e) { YandexBridge.fail(requestId, e); });
    } catch (e) {
      YandexBridge.fail(requestId, e);
    }
  },

  // ---------- Сохранения ----------

  YandexBridgeSave__deps: ['$YandexBridge'],
  YandexBridgeSave: function (requestId, jsonPtr) {
    if (!YandexBridge.ready(requestId)) return;
    if (!YandexBridge.player) { YandexBridge.fail(requestId, 'player is not available'); return; }
    try {
      var data = {};
      data[YandexBridge.SAVE_KEY] = UTF8ToString(jsonPtr);
      YandexBridge.player.setData(data, true)
        .then(function () { YandexBridge.ok(requestId, {}); })
        .catch(function (e) { YandexBridge.fail(requestId, e); });
    } catch (e) {
      YandexBridge.fail(requestId, e);
    }
  },

  YandexBridgeLoad__deps: ['$YandexBridge'],
  YandexBridgeLoad: function (requestId) {
    if (!YandexBridge.ready(requestId)) return;
    if (!YandexBridge.player) { YandexBridge.fail(requestId, 'player is not available'); return; }
    try {
      YandexBridge.player.getData([YandexBridge.SAVE_KEY])
        .then(function (data) {
          var json = (data && data[YandexBridge.SAVE_KEY]) || '';
          YandexBridge.ok(requestId, { json: json });
        })
        .catch(function (e) { YandexBridge.fail(requestId, e); });
    } catch (e) {
      YandexBridge.fail(requestId, e);
    }
  },

  // ---------- Лидерборды ----------

  YandexBridgeSubmitScore__deps: ['$YandexBridge'],
  YandexBridgeSubmitScore: function (requestId, boardPtr, score) {
    if (!YandexBridge.ready(requestId)) return;
    try {
      var board = UTF8ToString(boardPtr);
      YandexBridge.leaderboards()
        .then(function (lb) {
          return YandexBridge.lbCall(lb, 'setScore', 'setLeaderboardScore', [board, score]);
        })
        .then(function () { YandexBridge.ok(requestId, {}); })
        .catch(function (e) { YandexBridge.fail(requestId, e); });
    } catch (e) {
      YandexBridge.fail(requestId, e);
    }
  },

  YandexBridgeGetTop__deps: ['$YandexBridge'],
  YandexBridgeGetTop: function (requestId, boardPtr, count) {
    if (!YandexBridge.ready(requestId)) return;
    try {
      var board = UTF8ToString(boardPtr);
      var options = { quantityTop: Math.max(1, Math.min(20, count)), includeUser: true };
      YandexBridge.leaderboards()
        .then(function (lb) {
          return YandexBridge.lbCall(lb, 'getEntries', 'getLeaderboardEntries', [board, options]);
        })
        .then(function (response) { YandexBridge.ok(requestId, YandexBridge.mapEntries(response)); })
        .catch(function (e) { YandexBridge.fail(requestId, e); });
    } catch (e) {
      YandexBridge.fail(requestId, e);
    }
  },

  YandexBridgeGetPlayerEntry__deps: ['$YandexBridge'],
  YandexBridgeGetPlayerEntry: function (requestId, boardPtr) {
    if (!YandexBridge.ready(requestId)) return;
    try {
      var board = UTF8ToString(boardPtr);
      YandexBridge.leaderboards()
        .then(function (lb) {
          return YandexBridge.lbCall(lb, 'getPlayerEntry', 'getLeaderboardPlayerEntry', [board]);
        })
        .then(function (entry) {
          if (!entry) { YandexBridge.ok(requestId, { found: false }); return; }
          YandexBridge.ok(requestId, {
            found: true,
            rank: entry.rank || 0,
            score: entry.score || 0,
            playerName: (entry.player && entry.player.publicName) || '',
            isCurrentPlayer: true
          });
        })
        .catch(function (e) {
          // Игрока нет в таблице: это не ошибка вызова.
          YandexBridge.ok(requestId, { found: false });
        });
    } catch (e) {
      YandexBridge.fail(requestId, e);
    }
  }
});
