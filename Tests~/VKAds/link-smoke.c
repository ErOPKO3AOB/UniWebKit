// Compile with Unity's emcc and --js-library pointing to UniWebVKBridge.jslib.
extern void UniWebVK_Init(void);
extern void UniWebVK_InitWithCallback(void (*callback)(int));
extern int UniWebVK_IsSupported(void);
extern int UniWebVK_IsInitialized(void);
extern int UniWebVK_IsAdReady(const char *format);
extern int UniWebVK_IsAdBlockEnabled(void);
extern void UniWebVK_ShowNativeAds(const char *format, void (*opened)(void), void (*result)(int), void (*error)(int));
extern void UniWebVK_ShowBannerAd(const char *location, const char *layout);
extern void UniWebVK_HideBannerAd(void);
static volatile int received;
static void opened(void) { received++; }
static void result(int value) { received += value; }
int main(void) {
    UniWebVK_Init();
    UniWebVK_InitWithCallback(result);
    received += UniWebVK_IsSupported() + UniWebVK_IsInitialized() + UniWebVK_IsAdReady("reward") + UniWebVK_IsAdBlockEnabled();
    UniWebVK_ShowNativeAds("reward", opened, result, result);
    UniWebVK_ShowBannerAd("bottom", "resize");
    UniWebVK_HideBannerAd();
    return 0;
}
