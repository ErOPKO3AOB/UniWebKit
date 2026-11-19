const assert = require('assert').strict;
const fs = require('fs');
const path = require('path');
const vm = require('vm');
const { URLSearchParams } = require('url');

const source = fs.readFileSync(path.join(__dirname, '../../Runtime/Providers/VKGames/Plugins/WebGL/UniWebVKBridge.jslib'), 'utf8');
// Replace only the Emscripten callback macros; the actual library body runs unchanged.
const javascript = source.replace(/\{\{\{\s*makeDynCall\('[^']+',\s*'([^']+)'\)\s*\}\}\}/g, '($1)');
let checks = 0;
function check(value, expected, label) { assert.deepEqual(value, expected, label); checks++; }
function deferred() { let resolve, reject; const promise = new Promise((a, b) => { resolve = a; reject = b; }); return { promise, resolve, reject }; }
async function flush() { for (let i = 0; i < 20; i++) await Promise.resolve(); }

function create(bridge, sharedWindow) {
    const library = {}, timers = new Map(), scripts = [], messages = [], listeners = {}, strings = new Map();
    let pointer = 0;
    let time = 0, timerId = 0;
    const context = {
        Promise, console, URLSearchParams, Date: { now: () => time },
        window: sharedWindow || (bridge ? { vkBridge: bridge } : {}),
        document: {
            hidden: false, addEventListener: (name, callback) => { listeners[name] = callback; },
            createElement: tag => ({ tag, style: {}, offsetHeight: 1, offsetLeft: -10000, offsetParent: {}, parentNode: { removeChild() {} } }),
            body: { appendChild() {} }, head: { appendChild: script => scripts.push(script) }
        },
        setTimeout: (fn, delay) => { const id = ++timerId; timers.set(id, { fn, delay }); return id; },
        clearTimeout: id => timers.delete(id),
        SendMessage: (host, method, value) => messages.push({ host, method, value }),
        UTF8ToString: value => value,
        lengthBytesUTF8: text => text.length, stringToUTF8: (text, ptr) => strings.set(ptr, text),
        _malloc: () => ++pointer, _free: ptr => strings.delete(ptr),
        LibraryManager: { library }, mergeInto: Object.assign,
        autoAddDeps: (lib, dependency) => {
            for (const name of Object.keys(lib))
                if (typeof lib[name] === 'function' && !name.startsWith('$'))
                    lib[name + '__deps'] = [...(lib[name + '__deps'] || []), dependency];
        }
    };
    vm.createContext(context);
    vm.runInContext(javascript, context);
    context.vkState = library.$vkState;
    context._UniWebVK_Init = library.UniWebVK_Init;
    return {
        lib: library, context, scripts, messages, listeners, strings,
        advance: milliseconds => { time += milliseconds; },
        expire: delay => { for (const [id, item] of [...timers]) if (item.delay === delay) { timers.delete(id); item.fn(); } }
    };
}

function sdk() {
    const init = deferred(), show = deferred(), calls = [];
    const state = { available: true, supported: true, show, calls, init, throwShow: false,
        order: deferred(), listeners: [], launchParams: {}, profile: { id: 42, first_name: 'First', last_name: 'Player', photo_200: 'avatar' } };
    state.notify = event => state.listeners.forEach(callback => callback(event));
    state.bridge = {
        supports: () => state.supported,
        subscribe(callback) { state.listeners.push(callback); },
        send(method, options) {
            calls.push([method, options]);
            if (method === 'VKWebAppInit') return init.promise;
            if (method === 'VKWebAppCheckNativeAds') return Promise.resolve({ result: state.available });
            if (method === 'VKWebAppGetUserInfo') return Promise.resolve(state.profile);
            if (method === 'VKWebAppGetLaunchParams') return Promise.resolve(state.launchParams);
            if (method === 'VKWebAppShowOrderBox') return state.order.promise;
            if (method === 'VKWebAppShowNativeAds') {
                if (state.throwShow) throw new Error('sync failure');
                return state.show.promise;
            }
            return Promise.resolve({ result: true });
        }
    };
    return state;
}

(async () => {
    const vk = sdk(), env = create(vk.bridge), lib = env.lib;
    const initialized = [];
    lib.UniWebVK_InitWithCallback(value => initialized.push(value));
    lib.UniWebVK_InitWithCallback(value => initialized.push(value));
    await flush();
    check(vk.calls.filter(call => call[0] === 'VKWebAppInit').length, 1, 'concurrent init shares request');
    check(lib.UniWebVK_IsInitialized(), 0, 'initialization awaits confirmation');
    check(lib.UniWebVK_IsAdReady('reward'), 0, 'not ready before SDK');
    vk.init.resolve({ result: true });
    await flush();
    check(initialized, [1, 1], 'initialization callbacks complete once');
    check(lib.UniWebVK_IsSupported(), 1, 'supported after initialization');
    check(lib.UniWebVK_IsAdReady('interstitial'), 1, 'interstitial checked');
    check(lib.UniWebVK_IsAdReady('reward'), 1, 'rewarded checked');
    const other = create(null, env.context.window);
    other.lib.UniWebVK_Init();
    await flush();
    check(vk.calls.filter(call => call[0] === 'VKWebAppInit').length, 1, 'page shares confirmed initialization');

    let opened = 0;
    const results = [], errors = [];
    lib.UniWebVK_ShowNativeAds('reward', () => opened++, value => results.push(value), value => errors.push(value));
    check(opened, 1, 'open callback precedes SDK result');
    check(lib.UniWebVK_IsAdReady('interstitial'), 0, 'all fullscreen ads blocked while busy');
    lib.UniWebVK_ShowNativeAds('interstitial', () => opened++, value => results.push(value), value => errors.push(value));
    check(errors, [1], 'parallel request rejected');
    vk.show.resolve({ result: true });
    await flush();
    check(results, [1], 'confirmed view success');
    check(opened, 1, 'rejected request does not open');
    check(lib.UniWebVK_IsAdReady('reward'), 1, 'prewarm after completion');

    vk.show = deferred();
    lib.UniWebVK_ShowNativeAds('reward', null, value => results.push(value), value => errors.push(value));
    vk.show.resolve({ result: false });
    await flush();
    check(results, [1, 0], 'false result cannot grant reward');
    vk.show = deferred();
    lib.UniWebVK_ShowNativeAds('reward', null, value => results.push(value), value => errors.push(value));
    vk.show.reject({ error_data: { error_code: 4 } });
    await flush();
    check(errors, [1, 1], 'SDK rejection returns one error');
    vk.throwShow = true;
    lib.UniWebVK_ShowNativeAds('reward', null, value => results.push(value), value => errors.push(value));
    await flush();
    check(errors, [1, 1, 2], 'synchronous throw returns one error');
    check(env.context.vkState.adBusy, false, 'synchronous failure releases lane');
    vk.throwShow = false;

    vk.available = false;
    env.advance(6000);
    lib.UniWebVK_IsAdReady('reward');
    await flush();
    check(lib.UniWebVK_IsAdReady('reward'), 0, 'no inventory is not ready');
    lib.UniWebVK_IsAdReady('interstitial');
    await flush();
    const showCount = () => vk.calls.filter(call => call[0] === 'VKWebAppShowNativeAds').length;
    const showsBeforeClick = showCount();
    lib.UniWebVK_ShowNativeAds('interstitial', null, null, value => errors.push(value));
    check(showCount(), showsBeforeClick, 'unavailable interstitial is still gated');
    vk.show = deferred();
    lib.UniWebVK_ShowNativeAds('reward', null, value => results.push(value), value => errors.push(value));
    check(showCount(), showsBeforeClick + 1, 'explicit rewarded request reaches VK with a negative inventory check');
    vk.show.resolve({ result: true });
    await flush();
    check(results, [1, 0, 1], 'only the actual show result confirms the view');
    vk.available = true;
    env.advance(6000);
    lib.UniWebVK_IsAdReady('reward');
    await flush();
    check(lib.UniWebVK_IsAdReady('reward'), 1, 'inventory can recover');
    vk.supported = false;
    check(lib.UniWebVK_IsSupported(), 0, 'SDK support is respected');
    check(lib.UniWebVK_IsAdReady('reward'), 0, 'unsupported methods cannot show');
    vk.supported = true;
    lib.UniWebVK_ShowBannerAd('bottom', 'resize');
    lib.UniWebVK_HideBannerAd();
    await flush();
    check(vk.calls.filter(call => call[0].includes('BannerAd')).map(call => call[0]), ['VKWebAppShowBannerAd', 'VKWebAppHideBannerAd'], 'banner dispatch');

    const failedSdk = sdk(), failed = create(failedSdk.bridge), failures = [];
    failed.lib.UniWebVK_InitWithCallback(value => failures.push(value));
    await flush();
    failedSdk.init.reject(new Error('not VK'));
    await flush();
    check(failures, [0], 'init failure is not success');
    check(failed.lib.UniWebVK_IsInitialized(), 0, 'failed init stays unready');
    check(failed.lib.UniWebVK_IsSupported(), 0, 'browser alone is not platform support');

    const dynamicSdk = sdk(), dynamic = create(null);
    dynamic.lib.UniWebVK_Init(); dynamic.lib.UniWebVK_Init();
    check(dynamic.scripts.length, 1, 'only one SDK script load');
    dynamic.context.window.vkBridge = dynamicSdk.bridge;
    dynamic.scripts[0].onload();
    dynamicSdk.init.resolve({ result: true });
    await flush();
    check(dynamic.lib.UniWebVK_IsAdReady('reward'), 1, 'lazy SDK loading');

    const missing = create(null), loadFailures = [];
    missing.lib.UniWebVK_InitWithCallback(value => loadFailures.push(value));
    missing.scripts[0].onerror();
    await flush();
    check(loadFailures, [0], 'script failure completes callback');

    const dependencies = [...lib.UniWebVK_ShowNativeAds__deps];
    check(dependencies.includes('$vkState') && dependencies.includes('$UTF8ToString'), true, 'native ad linker dependencies');
    check([...lib.UniWebVK_InitWithCallback__deps].includes('UniWebVK_Init'), true, 'cross-library call dependency');
    check([...lib.$vkState__deps].includes('malloc') && [...lib.$vkState__deps].includes('free'), true, 'string memory dependencies');
    const host = 'runtime';
    lib.UniWebVK_RegisterRuntime(host);
    await flush();
    check(env.messages.filter(x => x.method === 'OnVKPause').pop().value, '0', 'initial pause snapshot');
    env.context.document.hidden = true;
    env.listeners.visibilitychange();
    check(lib.UniWebVK_IsPaused(), 1, 'hidden document pauses');
    vk.notify({ detail: { type: 'VKWebAppViewHide' } });
    env.context.document.hidden = false;
    env.listeners.visibilitychange();
    check(lib.UniWebVK_IsPaused(), 1, 'visibility cannot clear native view pause');
    vk.notify({ detail: { type: 'VKWebAppViewRestore' } });
    check(lib.UniWebVK_IsPaused(), 0, 'all visibility reasons cleared');
    check(env.messages.some(x => x.method === 'OnVKProfileInvalidated'), true, 'resume requests profile comparison');

    lib.UniWebVK_Request(host, 'profile1', 'profile', '');
    await flush();
    const reply = id => JSON.parse(env.messages.filter(x => x.method === 'OnVKReply').map(x => x.value).find(x => JSON.parse(x).requestId === id));
    check(reply('profile1').id, '42', 'profile ID is opaque string');
    check(reply('profile1').name, 'First Player', 'profile name mapping');
    vk.profile = { id: 75, first_name: 'Other' };
    lib.UniWebVK_Request(host, 'profile2', 'profile', '');
    await flush();
    check(reply('profile2').id, '75', 'refresh sees another account');

    lib.UniWebVK_Request(host, 'order1', 'purchase', 'coins');
    lib.UniWebVK_Request(host, 'order2', 'purchase', 'coins');
    await flush();
    check(reply('order2').errorCode, 2, 'orders are serialized');
    check(lib.UniWebVK_IsPaused(), 1, 'order box pauses');
    check(lib.UniWebVK_IsAdReady('reward'), 0, 'order blocks advertisement');
    vk.order.resolve({ status: 'chargeable', order_id: 'receipt1' });
    await flush();
    check(reply('order1').status, 'chargeable', 'order status preserved without paid claim');
    check(reply('order1').orderId, 'receipt1', 'real order ID preserved');
    check(lib.UniWebVK_IsPaused(), 0, 'order releases pause');
    vk.order = deferred();
    lib.UniWebVK_Request(host, 'cancel', 'purchase', 'coins');
    await flush();
    vk.order.resolve({ status: 'cancel' });
    await flush();
    check(reply('cancel').success, false, 'cancel is not successful order');
    check(reply('cancel').status, 'cancel', 'explicit cancellation preserved');
    vk.order = deferred();
    lib.UniWebVK_Request(host, 'missing-id', 'purchase', 'coins');
    await flush();
    vk.order.resolve({ status: 'chargeable' });
    await flush();
    check(reply('missing-id').success, false, 'product ID never substitutes a receipt');
    lib.UniWebVK_Request(host, 'catalog', 'catalog', '');
    await flush();
    check(reply('catalog').errorCode, 0, 'unsupported catalog is explicit');
    const messageCount = env.messages.length;
    lib.UniWebVK_UnregisterRuntime(host);
    vk.notify({ detail: { type: 'VKWebAppViewHide' } });
    check(env.messages.length, messageCount, 'unregistered receivers are detached');
    const platforms = {
        desktop_web: 1, desktop_web_messenger: 1, desktop_app_messenger: 1,
        mobile_web: 2, mobile_android: 2, mobile_android_messenger: 2,
        mobile_iphone: 2, mobile_iphone_messenger: 2, mobile_ipad: 2
    };
    for (const [platform, type] of Object.entries(platforms)) {
        const info = create(null);
        info.context.window.location = { search: '?vk_platform=' + platform + '&vk_language=ru' };
        info.context.navigator = { userAgent: 'Other', language: 'en-US' };
        check(info.lib.UniWebVK_GetDeviceType(), type, 'VK platform ' + platform);
        check(info.strings.get(info.lib.UniWebVK_GetLanguage()), 'ru', 'VK language has priority over browser');
        check(info.scripts.length, 0, 'classification does not await SDK load');
    }
    for (const [query, navigator, device, language] of [
        ['', { userAgent: 'Android', language: 'de-DE' }, 2, 'de-DE'],
        ['', { userAgent: 'Macintosh', platform: 'MacIntel', maxTouchPoints: 5, language: 'en' }, 2, 'en'],
        ['', { userAgent: 'Windows NT 10.0', language: 'en-US' }, 1, 'en-US'],
        ['', { userAgent: 'SmartTV Android TV', language: 'ru' }, 0, 'ru'],
        ['', { userAgent: 'Other' }, 0, 'en'],
        ['?vk_language=ua', { userAgent: 'Other' }, 0, 'uk'],
        ['?vk_language=kz', { userAgent: 'Other' }, 0, 'kk'],
        ['?vk_platform=invalid&vk_language=???', { userAgent: 'Windows NT', languages: ['pt_br'] }, 1, 'pt-BR']
    ]) {
        const info = create(null);
        info.context.window.location = { search: query };
        info.context.navigator = navigator;
        check(info.lib.UniWebVK_GetDeviceType(), device, 'browser device fallback');
        check(info.strings.get(info.lib.UniWebVK_GetLanguage()), language, 'language fallback/aliases');
    }
    const launchSdk = sdk(), launch = deferred(), late = create(launchSdk.bridge);
    launchSdk.launchParams = launch.promise;
    late.context.window.location = { search: '' };
    late.context.navigator = { userAgent: 'Windows NT', language: 'en-US' };
    check(late.lib.UniWebVK_GetDeviceType(), 1, 'immediate device before SDK');
    late.lib.UniWebVK_RegisterRuntime('environment');
    launchSdk.init.resolve({ result: true });
    await flush();
    launch.resolve({ vk_platform: 'mobile_ipad', vk_language: 'ru' });
    await flush();
    check(late.lib.UniWebVK_GetDeviceType(), 2, 'SDK can refine initial browser classification');
    check(late.strings.get(late.lib.UniWebVK_GetLanguage()), 'ru', 'late SDK language applied');
    check(late.messages.some(message => message.method === 'OnVKEnvironmentChanged'), true, 'late SDK change notification');
    launchSdk.launchParams = { vk_platform: 'desktop_web', vk_language: 'en' };
    launchSdk.notify({ detail: { type: 'VKWebAppViewRestore' } });
    await flush();
    check(late.lib.UniWebVK_GetDeviceType(), 1, 'resume refreshes environment');
    check(late.strings.get(late.lib.UniWebVK_GetLanguage()), 'en', 'resume refreshes platform locale');
    console.log(`PASS: ${checks} VK JavaScript bridge checks`);
})().catch(error => { console.error(error); process.exitCode = 1; });
