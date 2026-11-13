const assert = require('assert').strict;
const fs = require('fs');
const path = require('path');
const vm = require('vm');

const source = fs.readFileSync(path.join(__dirname, '../../Runtime/Providers/VKGames/Plugins/WebGL/UniWebVKBridge.jslib'), 'utf8');
// Replace only the Emscripten callback macros; the actual library body runs unchanged.
const javascript = source.replace(/\{\{\{\s*makeDynCall\('[^']+',\s*'([^']+)'\)\s*\}\}\}/g, '($1)');
let checks = 0;
function check(value, expected, label) { assert.deepEqual(value, expected, label); checks++; }
function deferred() { let resolve, reject; const promise = new Promise((a, b) => { resolve = a; reject = b; }); return { promise, resolve, reject }; }
async function flush() { for (let i = 0; i < 20; i++) await Promise.resolve(); }

function create(bridge, sharedWindow) {
    const library = {}, timers = new Map(), scripts = [];
    let time = 0, timerId = 0;
    const context = {
        Promise, console, Date: { now: () => time },
        window: sharedWindow || (bridge ? { vkBridge: bridge } : {}),
        document: {
            hidden: false, addEventListener() {},
            createElement: tag => ({ tag, style: {}, offsetHeight: 1, offsetLeft: -10000, offsetParent: {}, parentNode: { removeChild() {} } }),
            body: { appendChild() {} }, head: { appendChild: script => scripts.push(script) }
        },
        setTimeout: (fn, delay) => { const id = ++timerId; timers.set(id, { fn, delay }); return id; },
        clearTimeout: id => timers.delete(id),
        UTF8ToString: value => value,
        lengthBytesUTF8: text => text.length, stringToUTF8() {}, _malloc: () => 1, _free() {},
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
        lib: library, context, scripts,
        advance: milliseconds => { time += milliseconds; },
        expire: delay => { for (const [id, item] of [...timers]) if (item.delay === delay) { timers.delete(id); item.fn(); } }
    };
}

function sdk() {
    const init = deferred(), show = deferred(), calls = [];
    const state = { available: true, supported: true, show, calls, init, throwShow: false };
    state.bridge = {
        supports: () => state.supported,
        subscribe() {},
        send(method, options) {
            calls.push([method, options]);
            if (method === 'VKWebAppInit') return init.promise;
            if (method === 'VKWebAppCheckNativeAds') return Promise.resolve({ result: state.available });
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

    vk.available = false;
    env.advance(6000);
    lib.UniWebVK_IsAdReady('reward');
    await flush();
    check(lib.UniWebVK_IsAdReady('reward'), 0, 'no inventory is not ready');
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
    console.log(`PASS: ${checks} VK JavaScript bridge checks`);
})().catch(error => { console.error(error); process.exitCode = 1; });
