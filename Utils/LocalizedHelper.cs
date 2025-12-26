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
        // Delegates for faster invocation
        private static Func<string, string, string> LocalizedWithPrefixDelegate;
        private static Func<string, EStringCase, string> LocalizedWithCaseDelegate;
        private static Func<string, string> TransliterateSingleArgDelegate;
        private static Func<string, string, string> TransliterateWithLocaleDelegate;

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
                    var prefixMethod = localizationType.GetMethod(
                        "Localized",
                        BindingFlags.Public | BindingFlags.Static,
                        null,
                        new[] { typeof(string), typeof(string) },
                        null);

                    if (prefixMethod != null)
                    {
                        LocalizedWithPrefixDelegate = (Func<string, string, string>)Delegate.CreateDelegate(typeof(Func<string, string, string>), prefixMethod);
                    }

                    var caseMethod = localizationType.GetMethod(
                        "Localized",
                        BindingFlags.Public | BindingFlags.Static,
                        null,
                        new[] { typeof(string), typeof(EStringCase) },
                        null);

                    if (caseMethod != null)
                    {
                        LocalizedWithCaseDelegate = (Func<string, EStringCase, string>)Delegate.CreateDelegate(typeof(Func<string, EStringCase, string>), caseMethod);
                    }
                }

                Type transliterationType = RefTool.GetEftType("Transliterate");
                if (transliterationType != null)
                {
                    var singleArgMethod = transliterationType.GetMethod(
                        "Transliterate",
                        BindingFlags.Public | BindingFlags.Static,
                        null,
                        new[] { typeof(string) },
                        null);

                    if (singleArgMethod != null)
                    {
                        TransliterateSingleArgDelegate = (Func<string, string>)Delegate.CreateDelegate(typeof(Func<string, string>), singleArgMethod);
                    }

                    var withLocaleMethod = transliterationType.GetMethod(
                        "Transliterate",
                        BindingFlags.Public | BindingFlags.Static,
                        null,
                        new[] { typeof(string), typeof(string) },
                        null);

                    if (withLocaleMethod != null)
                    {
                        TransliterateWithLocaleDelegate = (Func<string, string, string>)Delegate.CreateDelegate(typeof(Func<string, string, string>), withLocaleMethod);
                    }
                }

                LocalizationAvailable = LocalizedWithPrefixDelegate != null || LocalizedWithCaseDelegate != null;
                TransliterationAvailable = TransliterateSingleArgDelegate != null || TransliterateWithLocaleDelegate != null;
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
                if (!string.IsNullOrEmpty(prefix) && LocalizedWithPrefixDelegate != null)
                {
                    return LocalizedWithPrefixDelegate(key, prefix);
                }

                if (LocalizedWithCaseDelegate != null)
                {
                    return LocalizedWithCaseDelegate(key, stringCase);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug($"[SimpleHitMarker] LocalizedHelper.Localized failed for '{key}': {ex}");
            }

            return key;
        }

        public static string LocalizedEnum<TEnum>(TEnum value, string prefix = null, EStringCase stringCase = EStringCase.None) where TEnum : Enum
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
                if (!string.IsNullOrEmpty(locale) && TransliterateWithLocaleDelegate != null)
                {
                    return TransliterateWithLocaleDelegate(value, locale);
                }

                if (TransliterateSingleArgDelegate != null)
                {
                    return TransliterateSingleArgDelegate(value);
                }

                if (TransliterateWithLocaleDelegate != null)
                {
                    return TransliterateWithLocaleDelegate(value, null);
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


