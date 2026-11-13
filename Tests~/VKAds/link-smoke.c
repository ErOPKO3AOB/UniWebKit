// Compile with Unity's emcc and --js-library pointing to UniWebVKBridge.jslib.
#include <stdlib.h>
extern char *UniWebStorage_VkProfile(void);
extern int UniWebStorage_VkAvailable(void);
extern void UniWebStorage_Request(const char *host, const char *id, const char *backend, const char *operation,
    const char *key, const char *profile, const char *document, const char *bindings);
extern void UniWebVK_Init(void);
extern void UniWebVK_InitWithCallback(void (*callback)(int));
extern int UniWebVK_IsSupported(void);
extern int UniWebVK_IsInitialized(void);
extern int UniWebVK_IsAdReady(const char *format);
extern int UniWebVK_IsAdBlockEnabled(void);
extern void UniWebVK_ShowNativeAds(const char *format, void (*opened)(void), void (*result)(int), void (*error)(int));
extern void UniWebVK_ShowBannerAd(const char *location, const char *layout);
extern void UniWebVK_HideBannerAd(void);
extern int UniWebVK_GetDeviceType(void);
extern char *UniWebVK_GetLanguage(void);
extern void UniWebVK_RegisterRuntime(const char *host);
extern void UniWebVK_UnregisterRuntime(const char *host);
extern int UniWebVK_SupportsMethod(const char *method);
extern void UniWebVK_Request(const char *host, const char *id, const char *operation, const char *value);
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
    received += UniWebVK_GetDeviceType();
    free(UniWebVK_GetLanguage());
    UniWebVK_RegisterRuntime("receiver");
    received += UniWebVK_SupportsMethod("VKWebAppGetUserInfo");
    UniWebVK_Request("receiver", "init", "init", "");
    UniWebVK_Request("receiver", "profile", "profile", "");
    UniWebVK_Request("receiver", "order", "purchase", "coins");
    UniWebVK_UnregisterRuntime("receiver");
    char *profile = UniWebStorage_VkProfile();
    received += UniWebStorage_VkAvailable();
    UniWebStorage_Request("storage", "read", "vk", "read", "save", profile, "", "");
    free(profile);
    return 0;
}
