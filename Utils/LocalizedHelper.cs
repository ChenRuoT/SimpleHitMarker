using System;
using System.Reflection;
using EFT;
using SPT.Reflection.Utils;

namespace SimpleHitMarker.Localization
{
    /// <summary>
    /// Small reflection based bridge that lets us call Tarkov's internal localization helpers.
    /// </summary>
    internal static class LocalizedHelper
    {
        private static readonly MethodInfo LocalizedWithPrefixMethod;
        private static readonly MethodInfo LocalizedWithCaseMethod;
        private static readonly MethodInfo TransliterateSingleArgMethod;
        private static readonly MethodInfo TransliterateWithLocaleMethod;
        private static readonly bool LocalizationAvailable;
        private static readonly bool TransliterationAvailable;
        private static bool loggedLocalizationError;
        private static bool loggedTransliterationError;

        static LocalizedHelper()
        {
            try
            {
                Type localizationType = RefTool.GetEftType("ParseLocalization");
                if (localizationType != null)
                {
                    LocalizedWithPrefixMethod = localizationType.GetMethod(
                        "Localized",
                        BindingFlags.Public | BindingFlags.Static,
                        null,
                        new[] { typeof(string), typeof(string) },
                        null);

                    LocalizedWithCaseMethod = localizationType.GetMethod(
                        "Localized",
                        BindingFlags.Public | BindingFlags.Static,
                        null,
                        new[] { typeof(string), typeof(EStringCase) },
                        null);
                }

                Type transliterationType = RefTool.GetEftType("Transliterate");
                if (transliterationType != null)
                {
                    TransliterateSingleArgMethod = transliterationType.GetMethod(
                        "Transliterate",
                        BindingFlags.Public | BindingFlags.Static,
                        null,
                        new[] { typeof(string) },
                        null);

                    TransliterateWithLocaleMethod = transliterationType.GetMethod(
                        "Transliterate",
                        BindingFlags.Public | BindingFlags.Static,
                        null,
                        new[] { typeof(string), typeof(string) },
                        null);
                }

                LocalizationAvailable = LocalizedWithPrefixMethod != null || LocalizedWithCaseMethod != null;
                TransliterationAvailable = TransliterateSingleArgMethod != null || TransliterateWithLocaleMethod != null;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[SimpleHitMarker] Failed to initialize LocalizedHelper: {ex}");
            }
        }

        public static string Localized(string key, string prefix = null, EStringCase stringCase = EStringCase.None)
        {
            if (string.IsNullOrEmpty(key))
            {
                return key ?? string.Empty;
            }

            if (!LocalizationAvailable)
            {
                LogLocalizationMissingOnce();
                return key;
            }

            try
            {
                if (!string.IsNullOrEmpty(prefix) && LocalizedWithPrefixMethod != null)
                {
                    return (string)LocalizedWithPrefixMethod.Invoke(null, new object[] { key, prefix });
                }

                if (LocalizedWithCaseMethod != null)
                {
                    return (string)LocalizedWithCaseMethod.Invoke(null, new object[] { key, stringCase });
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug($"[SimpleHitMarker] LocalizedHelper.Localized failed for '{key}': {ex}");
            }

            return key;
        }

        public static string LocalizedEnum<TEnum>(TEnum value, string prefix = null, EStringCase stringCase = EStringCase.None) where TEnum : struct, Enum
        {
            string key = $"{typeof(TEnum).Name}/{value}";
            return Localized(key, prefix, stringCase);
        }

        public static string Transliterate(string value, string locale = null)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value ?? string.Empty;
            }

            if (!TransliterationAvailable)
            {
                LogTransliterationMissingOnce();
                return value;
            }

            try
            {
                if (!string.IsNullOrEmpty(locale) && TransliterateWithLocaleMethod != null)
                {
                    return (string)TransliterateWithLocaleMethod.Invoke(null, new object[] { value, locale });
                }

                if (TransliterateSingleArgMethod != null)
                {
                    return (string)TransliterateSingleArgMethod.Invoke(null, new object[] { value });
                }

                if (TransliterateWithLocaleMethod != null)
                {
                    return (string)TransliterateWithLocaleMethod.Invoke(null, new object[] { value, null });
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug($"[SimpleHitMarker] LocalizedHelper.Transliterate failed for '{value}': {ex}");
            }

            return value;
        }

        private static void LogLocalizationMissingOnce()
        {
            if (loggedLocalizationError)
            {
                return;
            }

            loggedLocalizationError = true;
            Plugin.Log?.LogWarning("[SimpleHitMarker] EFT localization methods could not be resolved; falling back to raw keys.");
        }

        private static void LogTransliterationMissingOnce()
        {
            if (loggedTransliterationError)
            {
                return;
            }

            loggedTransliterationError = true;
            Plugin.Log?.LogWarning("[SimpleHitMarker] EFT transliteration methods could not be resolved; names will not be transliterated.");
        }
    }
}


