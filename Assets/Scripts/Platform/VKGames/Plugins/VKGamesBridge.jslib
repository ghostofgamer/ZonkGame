// Мост между C# и VK Bridge (Игры ВКонтакте).
// Схема та же, что у Яндекса: C# передаёт requestId, JS отвечает через Module.SendMessage
// на объект VKGamesSdkBridge, метод OnBridgeResponse. События рекламы идут в OnBridgeEvent.
// Ответ всегда имеет вид {id, ok, result, error}, где result это строка с JSON.
//
// VKWebAppInit отправляет страница (index.html) и кладёт промис в window.vkInitPromise,
// параметры запуска лежат в window.vkLaunchParams.

mergeInto(LibraryManager.library, {

  $VKGamesBridge: {
    RECEIVER: 'VKGamesSdkBridge',

    // Вне VK (локальный сервер, чужой хостинг) VKWebAppInit может не ответить никогда.
    INIT_TIMEOUT_MS: 10000,
    USER_INFO_TIMEOUT_MS: 5000,

    // Хранилище VK: значение до 4096 символов, для сериализованной строки заявлено 2236.
    // Берём 1000 с запасом на экранирование. Ключей до 1000 на пользователя,
    // вызовов до 1000 в час: одно сохранение стоит (число кусков + 1) вызовов.
    SAVE_CHUNK: 1000,
    SAVE_META_KEY: 'save_meta',
    SAVE_PREFIX: 'save_',

    // VKWebAppCheckNativeAds при отсутствии рекламы запускает её загрузку и отвечает false,
    // поэтому проверяем заранее и повторяем по таймеру, как советует документация VK.
    AD_PRELOAD_INTERVAL_MS: 30000,
    AD_FORMATS: ['interstitial', 'reward'],
    adReady: { interstitial: false, reward: false },

    initialized: false,
    // Слот последнего сохранения: 'a' или 'b'. Пишем всегда в другой, мету в конце,
    // поэтому обрыв посреди записи не портит предыдущее сохранение.
    saveSlot: null,

    err: function (e) {
      if (!e) return 'unknown error';
      if (typeof e === 'string') return e;
      if (e.error_data) {
        var d = e.error_data;
        return (e.error_type || 'error') + ' ' + (d.error_code || '') + ': ' +
          (d.error_reason || d.error_msg || d.error_description || JSON.stringify(d));
      }
      return e.message || JSON.stringify(e);
    },

    errorCode: function (e) {
      return (e && e.error_data && e.error_data.error_code) || 0;
    },

    respond: function (id, ok, result, error) {
      var payload = JSON.stringify({
        id: id,
        ok: !!ok,
        result: result || '',
        error: error || ''
      });
      Module.SendMessage(VKGamesBridge.RECEIVER, 'OnBridgeResponse', payload);
    },

    ok: function (id, obj) {
      VKGamesBridge.respond(id, true, JSON.stringify(obj || {}), null);
    },

    fail: function (id, e) {
      VKGamesBridge.respond(id, false, null, VKGamesBridge.err(e));
    },

    event: function (name) {
      Module.SendMessage(VKGamesBridge.RECEIVER, 'OnBridgeEvent', name);
    },

    ready: function (id) {
      if (!VKGamesBridge.initialized) {
        VKGamesBridge.fail(id, 'VK Bridge is not initialized');
        return false;
      }
      return true;
    },

    send: function (method, params) {
      return vkBridge.send(method, params || {});
    },

    withTimeout: function (promise, ms, message) {
      return new Promise(function (resolve, reject) {
        var timer = setTimeout(function () { reject(new Error(message)); }, ms);
        promise.then(
          function (value) { clearTimeout(timer); resolve(value); },
          function (error) { clearTimeout(timer); reject(error); });
      });
    },

    launchParams: function () {
      return (typeof window !== 'undefined' && window.vkLaunchParams) || {};
    },

    readMeta: function () {
      return VKGamesBridge.send('VKWebAppStorageGet', { keys: [VKGamesBridge.SAVE_META_KEY] })
        .then(function (data) {
          var value = (data && data.keys && data.keys.length) ? data.keys[0].value : '';
          var parts = (value || '').split(':');
          if (parts.length !== 2) return null;
          var count = parseInt(parts[1], 10);
          if ((parts[0] !== 'a' && parts[0] !== 'b') || !(count >= 0)) return null;
          return { slot: parts[0], count: count };
        });
    },

    chunkKey: function (slot, index) {
      return VKGamesBridge.SAVE_PREFIX + slot + '_' + index;
    },

    adParams: function (format) {
      var params = { ad_format: format };
      // Для rewarded без водопада: иначе VK может подменить её межстраничной,
      // и result: true уже не будет значить, что награду нужно выдать.
      if (format === 'reward') params.use_waterfall = false;
      return params;
    },

    preloadAd: function (format) {
      return VKGamesBridge.send('VKWebAppCheckNativeAds', VKGamesBridge.adParams(format))
        .then(function (data) { VKGamesBridge.adReady[format] = !!(data && data.result); })
        .catch(function (e) {
          VKGamesBridge.adReady[format] = false;
          console.warn('[VK] VKWebAppCheckNativeAds ' + format + ' failed', e);
        });
    },

    preloadAds: function () {
      VKGamesBridge.AD_FORMATS.forEach(function (format) {
        if (!VKGamesBridge.adReady[format]) VKGamesBridge.preloadAd(format);
      });
    }
  },

  // ---------- Жизненный цикл ----------

  VKGamesBridgeInit__deps: ['$VKGamesBridge'],
  VKGamesBridgeInit: function (requestId) {
    try {
      if (typeof vkBridge === 'undefined') {
        VKGamesBridge.fail(requestId, 'vkBridge is not defined: vk-bridge.min.js is not connected in index.html');
        return;
      }

      // Запасной путь для шаблона, который сам не отправил VKWebAppInit.
      var initPromise = (typeof window !== 'undefined' && window.vkInitPromise) || vkBridge.send('VKWebAppInit');
      var params = VKGamesBridge.launchParams();

      VKGamesBridge.withTimeout(initPromise, VKGamesBridge.INIT_TIMEOUT_MS,
          'VKWebAppInit timeout: the game is probably running outside VK')
        .then(function () {
          VKGamesBridge.initialized = true;

          VKGamesBridge.preloadAds();
          setInterval(VKGamesBridge.preloadAds, VKGamesBridge.AD_PRELOAD_INTERVAL_MS);

          // Имя игрока нужно только для отображения, поэтому ошибка здесь не мешает запуску.
          return VKGamesBridge.withTimeout(VKGamesBridge.send('VKWebAppGetUserInfo'),
              VKGamesBridge.USER_INFO_TIMEOUT_MS, 'VKWebAppGetUserInfo timeout')
            .then(function (user) {
              return ((user.first_name || '') + ' ' + (user.last_name || '')).trim();
            })
            .catch(function (e) {
              console.warn('[VK] VKWebAppGetUserInfo failed', e);
              return '';
            });
        })
        .then(function (userName) {
          VKGamesBridge.ok(requestId, {
            lang: params.vk_language || '',
            platform: params.vk_platform || '',
            userId: params.vk_user_id || '',
            userName: userName || ''
          });
        })
        .catch(function (e) { VKGamesBridge.fail(requestId, e); });
    } catch (e) {
      VKGamesBridge.fail(requestId, e);
    }
  },

  // ---------- Реклама ----------

  // Реклама предзагружена (последний VKWebAppCheckNativeAds ответил true). 1 или 0.
  VKGamesBridgeIsAdReady__deps: ['$VKGamesBridge'],
  VKGamesBridgeIsAdReady: function (formatPtr) {
    var format = UTF8ToString(formatPtr);
    return VKGamesBridge.adReady[format] ? 1 : 0;
  },

  // format: 'interstitial' или 'reward'.
  // VKWebAppShowNativeAds вызывается всегда, даже если предзагрузка не успела:
  // по документации VK метод сам запросит рекламу и покажет её при получении.
  // У нативной рекламы VK нет событий открытия и закрытия, поэтому adOpened отправляется
  // перед вызовом показа, а adClosed после ответа. Отвечает ли VK при закрытии рекламы
  // или в момент показа, документация не уточняет: проверить на живой площадке.
  VKGamesBridgeShowAd__deps: ['$VKGamesBridge'],
  VKGamesBridgeShowAd: function (requestId, formatPtr) {
    if (!VKGamesBridge.ready(requestId)) return;
    try {
      var format = UTF8ToString(formatPtr);
      var wasPreloaded = !!VKGamesBridge.adReady[format];

      var finish = function () {
        VKGamesBridge.event('adClosed');
        // Показанная реклама израсходована, сразу грузим следующую.
        VKGamesBridge.adReady[format] = false;
        VKGamesBridge.preloadAd(format);
      };

      VKGamesBridge.event('adOpened');
      VKGamesBridge.send('VKWebAppShowNativeAds', VKGamesBridge.adParams(format))
        .then(function (data) {
          finish();
          var shown = !!(data && data.result);
          VKGamesBridge.ok(requestId, {
            shown: shown,
            reason: shown ? '' : 'result=false, preloaded=' + wasPreloaded
          });
        })
        .catch(function (e) {
          finish();
          // Код 20 значит "нет рекламы": это не ошибка вызова.
          if (VKGamesBridge.errorCode(e) === 20) {
            VKGamesBridge.ok(requestId, { shown: false, reason: 'no ads (20), preloaded=' + wasPreloaded });
            return;
          }
          VKGamesBridge.fail(requestId, e);
        });
    } catch (e) {
      VKGamesBridge.fail(requestId, e);
    }
  },

  // ---------- Сохранения ----------

  VKGamesBridgeSave__deps: ['$VKGamesBridge'],
  VKGamesBridgeSave: function (requestId, jsonPtr) {
    if (!VKGamesBridge.ready(requestId)) return;
    try {
      var json = UTF8ToString(jsonPtr);
      var chunks = [];
      for (var i = 0; i < json.length; i += VKGamesBridge.SAVE_CHUNK) {
        chunks.push(json.substring(i, i + VKGamesBridge.SAVE_CHUNK));
      }

      var knownSlot = VKGamesBridge.saveSlot
        ? Promise.resolve(VKGamesBridge.saveSlot)
        : VKGamesBridge.readMeta().then(function (meta) { return meta ? meta.slot : null; });

      knownSlot
        .then(function (current) {
          var slot = current === 'a' ? 'b' : 'a';

          // Куски пишутся по одному: параллельные вызовы рискуют упереться во flood control.
          var chain = Promise.resolve();
          chunks.forEach(function (chunk, index) {
            chain = chain.then(function () {
              return VKGamesBridge.send('VKWebAppStorageSet', { key: VKGamesBridge.chunkKey(slot, index), value: chunk });
            });
          });

          return chain.then(function () {
            return VKGamesBridge.send('VKWebAppStorageSet', { key: VKGamesBridge.SAVE_META_KEY, value: slot + ':' + chunks.length });
          }).then(function () {
            VKGamesBridge.saveSlot = slot;
          });
        })
        .then(function () { VKGamesBridge.ok(requestId, {}); })
        .catch(function (e) { VKGamesBridge.fail(requestId, e); });
    } catch (e) {
      VKGamesBridge.fail(requestId, e);
    }
  },

  VKGamesBridgeLoad__deps: ['$VKGamesBridge'],
  VKGamesBridgeLoad: function (requestId) {
    if (!VKGamesBridge.ready(requestId)) return;
    try {
      VKGamesBridge.readMeta()
        .then(function (meta) {
          if (!meta || meta.count === 0) return '';

          VKGamesBridge.saveSlot = meta.slot;
          var keys = [];
          for (var i = 0; i < meta.count; i++) keys.push(VKGamesBridge.chunkKey(meta.slot, i));

          return VKGamesBridge.send('VKWebAppStorageGet', { keys: keys })
            .then(function (data) {
              var byKey = {};
              var items = (data && data.keys) || [];
              for (var j = 0; j < items.length; j++) byKey[items[j].key] = items[j].value || '';

              var json = '';
              for (var k = 0; k < keys.length; k++) json += byKey[keys[k]] || '';
              return json;
            });
        })
        .then(function (json) { VKGamesBridge.ok(requestId, { json: json }); })
        .catch(function (e) { VKGamesBridge.fail(requestId, e); });
    } catch (e) {
      VKGamesBridge.fail(requestId, e);
    }
  }
});
