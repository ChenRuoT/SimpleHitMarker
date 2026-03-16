using System;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using EFT;
using SPT.Reflection.Utils;

namespace SimpleHitMarker.Localization
{
    /// <summary>
    /// Small reflection based bridge that lets us call Tarkov's internal localization helpers.
    /// </summary>
    internal static class LocalizedHelper
    {
        // Simple caches to avoid expensive reflection/delegate costs on hot paths
        // Lazily created to keep type initialization fast
        private static ConcurrentDictionary<string, string> LocalizedCache;
        private static ConcurrentDictionary<string, string> TransliterationCache;

        // Delegates for faster invocation
        private static Func<string, string, string> LocalizedWithPrefixDelegate;
        private static Func<string, EStringCase, string> LocalizedWithCaseDelegate;
        private static Func<string, string> TransliterateSingleArgDelegate;
        private static Func<string, string, string> TransliterateWithLocaleDelegate;

        private static bool LocalizationAvailable;
        private static bool TransliterationAvailable;
        private static bool loggedLocalizationError;
        private static bool loggedTransliterationError;

        // Quick mapping for common Cyrillic characters to Latin approximations
        private static IReadOnlyDictionary<char, string> CyrillicToLatinMapBacking;

        private static IReadOnlyDictionary<char, string> CyrillicToLatinMap
        {
            get
            {
                if (CyrillicToLatinMapBacking != null) return CyrillicToLatinMapBacking;
                var map = new Dictionary<char, string>
                {
                    ['А'] = "A", ['Б'] = "B", ['В'] = "V", ['Г'] = "G", ['Д'] = "D",
                    ['Е'] = "E", ['Ё'] = "E", ['Ж'] = "Zh", ['З'] = "Z", ['И'] = "I",
                    ['Й'] = "I", ['К'] = "K", ['Л'] = "L", ['М'] = "M", ['Н'] = "N",
                    ['О'] = "O", ['П'] = "P", ['Р'] = "R", ['С'] = "S", ['Т'] = "T",
                    ['У'] = "U", ['Ф'] = "F", ['Х'] = "Kh", ['Ц'] = "Ts", ['Ч'] = "Ch",
                    ['Ш'] = "Sh", ['Щ'] = "Shch", ['Ъ'] = "", ['Ы'] = "Y", ['Ь'] = "",
                    ['Э'] = "E", ['Ю'] = "Yu", ['Я'] = "Ya",
                    ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "g", ['д'] = "d",
                    ['е'] = "e", ['ё'] = "e", ['ж'] = "zh", ['з'] = "z", ['и'] = "i",
                    ['й'] = "i", ['к'] = "k", ['л'] = "l", ['м'] = "m", ['н'] = "n",
                    ['о'] = "o", ['п'] = "p", ['р'] = "r", ['с'] = "s", ['т'] = "t",
                    ['у'] = "u", ['ф'] = "f", ['х'] = "kh", ['ц'] = "ts", ['ч'] = "ch",
                    ['ш'] = "sh", ['щ'] = "shch", ['ъ'] = "", ['ы'] = "y", ['ь'] = "",
                    ['э'] = "e", ['ю'] = "yu", ['я'] = "ya",
                };

                System.Threading.Interlocked.CompareExchange(ref CyrillicToLatinMapBacking, map, null);
                return CyrillicToLatinMapBacking;
            }
        }

        static LocalizedHelper()
        {
            // Kick off delegate discovery asynchronously so the first call to this
            // type does not block the main thread with reflection/delegate creation.
            Task.Run(() => InitializeDelegates());
        }

        private static void InitializeDelegates()
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
                // Try cache first (lazy init)
                if (LocalizedCache == null)
                {
                    System.Threading.Interlocked.CompareExchange(ref LocalizedCache, new ConcurrentDictionary<string, string>(), null);
                }

                if (LocalizedCache.TryGetValue(key, out var cached)) return cached;

                var sw = Stopwatch.StartNew();
                string result = null;

                if (!string.IsNullOrEmpty(prefix) && LocalizedWithPrefixDelegate != null)
                {
                    result = LocalizedWithPrefixDelegate(key, prefix);
                }
                else if (LocalizedWithCaseDelegate != null)
                {
                    result = LocalizedWithCaseDelegate(key, stringCase);
                }

                sw.Stop();
                if (sw.ElapsedMilliseconds > 20)
                {
                    Plugin.Log?.LogWarning($"[SimpleHitMarker] LocalizedHelper.Localized slow for '{key}': {sw.ElapsedMilliseconds} ms");
                }

                if (result != null)
                {
                    LocalizedCache[key] = result;
                    return result;
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
        /// <summary>
        /// Warm up localization system by pre-initializing delegates.
        /// Call this once on startup to avoid first-call overhead during gameplay.
        /// </summary>
        public static void WarmupLocalization()
        {
            Task.Run(() =>
            {
                try
                {
                    // Force delegate initialization by accessing a test key
                    _ = Localized("test_warmup");
                    Plugin.Log?.LogInfo("[SimpleHitMarker] LocalizedHelper warmup completed");
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogWarning($"[SimpleHitMarker] LocalizedHelper warmup failed (non-critical): {ex}");
                }
            });
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
                // cache first (lazy init)
                if (TransliterationCache == null)
                {
                    System.Threading.Interlocked.CompareExchange(ref TransliterationCache, new ConcurrentDictionary<string, string>(), null);
                }

                if (TransliterationCache.TryGetValue(value, out var cached)) return cached;

                // Fast-path fallback: perform a cheap, non-blocking transliteration and
                // immediately return it to avoid blocking the main thread.
                var swTotal = Stopwatch.StartNew();
                var swQuick = Stopwatch.StartNew();
                string quick = QuickTransliterateFallback(value);
                swQuick.Stop();

                // store the quick result
                TransliterationCache[value] = quick;

                swTotal.Stop();

                // Diagnostic logging for unexpected slowness
                try
                {
                    bool debug = Plugin.Instance?.ConfigManager?.DebugMode?.Value == true;
                    if (debug || swTotal.ElapsedMilliseconds > 20)
                    {
                        Plugin.Log?.LogWarning($"[SimpleHitMarker] LocalizedHelper.Transliterate timings(ms): total={swTotal.ElapsedMilliseconds}, quick={swQuick.ElapsedMilliseconds}, TransliterationAvailable={TransliterationAvailable}, thread={System.Threading.Thread.CurrentThread.ManagedThreadId}");
                    }
                }
                catch { }

                return quick;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug($"[SimpleHitMarker] LocalizedHelper.Transliterate failed for '{value}': {ex}");
            }

            return value;
        }

        private static string QuickTransliterateFallback(string value)
        {
            try
            {
                // If string contains Cyrillic characters, perform a fast mapping to Latin
                // to produce readable names immediately on the main thread.
                bool hasCyr = false;
                foreach (char c in value)
                {
                    if ((c >= '\u0400' && c <= '\u04FF') || (c >= '\u0500' && c <= '\u052F'))
                    {
                        hasCyr = true; break;
                    }
                }

                if (hasCyr)
                {
                    var outSb = new StringBuilder(value.Length * 2);
                    foreach (char c in value)
                    {
                        if (CyrillicToLatinMap.TryGetValue(c, out var mapped))
                        {
                            outSb.Append(mapped);
                        }
                        else
                        {
                            // preserve ASCII and other characters
                            outSb.Append(c);
                        }
                    }

                    var mappedRes = outSb.ToString();
                    if (!string.IsNullOrWhiteSpace(mappedRes)) return mappedRes;
                }

                // Fallback: remove diacritics for latin scripts
                string normalized = value.Normalize(NormalizationForm.FormKD);
                var sb = new StringBuilder(normalized.Length);
                foreach (char c in normalized)
                {
                    var cat = CharUnicodeInfo.GetUnicodeCategory(c);
                    if (cat == UnicodeCategory.NonSpacingMark) continue;
                    sb.Append(c);
                }

                string res = sb.ToString();
                if (string.IsNullOrWhiteSpace(res)) return value;
                return res;
            }
            catch
            {
                return value;
            }
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


