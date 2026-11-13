namespace UnityEngine
{
    public enum SystemLanguage { English, Unknown, Afrikaans, Arabic, Basque, Belarusian, Bulgarian, Catalan, Chinese, ChineseSimplified, ChineseTraditional, Czech, Danish, Dutch, Estonian, Faroese, Finnish, French, German, Greek, Hebrew, Hungarian, Icelandic, Indonesian, Italian, Japanese, Korean, Latvian, Lithuanian, Norwegian, Polish, Portuguese, Romanian, Russian, SerboCroatian, Slovak, Slovenian, Spanish, Swedish, Thai, Turkish, Ukrainian, Vietnamese }
    public enum DeviceType { Unknown, Handheld, Desktop }
    public static class Application { public static SystemLanguage systemLanguage = SystemLanguage.Russian; }
    public static class SystemInfo { public static DeviceType deviceType = DeviceType.Desktop; }
}
