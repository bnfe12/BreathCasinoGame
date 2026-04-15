using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace BreathCasino.Gameplay
{
    public static class BCRuntimeFontProvider
    {
        private static readonly Dictionary<int, Font> Cache = new();
        private static readonly Dictionary<string, TMP_FontAsset> TmpCache = new();
        private static readonly string[] PreferredFonts = { "Segoe UI", "Tahoma", "Arial" };
        private const string TitleFontResourcePath = "Fonts/Gradientico";
        private const string FallbackTitleFontName = "Arial";

        public static Font Get(int size)
        {
            int normalizedSize = Mathf.Max(12, size);
            if (Cache.TryGetValue(normalizedSize, out Font cached) && cached != null)
            {
                return cached;
            }

            Font resolved = TryCreateDynamicFont(normalizedSize) ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Cache[normalizedSize] = resolved;
            return resolved;
        }

        public static Font GetTitleFont(int size)
        {
            int normalizedSize = Mathf.Max(12, size);
            int key = -normalizedSize;
            if (Cache.TryGetValue(key, out Font cached) && cached != null)
            {
                return cached;
            }

            Font resolved = Resources.Load<Font>(TitleFontResourcePath);
            if (resolved == null)
            {
                resolved = TryCreateFallbackTitleFont(normalizedSize) ?? Get(normalizedSize);
            }

            Cache[key] = resolved;
            return resolved;
        }

        public static TMP_FontAsset GetTitleTmpFont(int size)
        {
            Font titleFont = GetTitleFont(size);
            if (titleFont == null)
            {
                return Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            }

            string cacheKey = $"{titleFont.name}:{Mathf.Max(12, size)}";
            if (TmpCache.TryGetValue(cacheKey, out TMP_FontAsset cached) && cached != null)
            {
                return cached;
            }

            TMP_FontAsset created = null;
            try
            {
                created = TMP_FontAsset.CreateFontAsset(titleFont);
                if (created != null)
                {
                    created.name = $"{titleFont.name}_TMP_Runtime";
                    created.hideFlags = HideFlags.DontSave;
                }
            }
            catch
            {
                created = null;
            }

            if (created == null)
            {
                created = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            }

            TmpCache[cacheKey] = created;
            return created;
        }

        private static Font TryCreateDynamicFont(int size)
        {
            try
            {
                return Font.CreateDynamicFontFromOSFont(PreferredFonts, size);
            }
            catch
            {
                return null;
            }
        }

        private static Font TryCreateFallbackTitleFont(int size)
        {
            try
            {
                return Font.CreateDynamicFontFromOSFont(FallbackTitleFontName, size);
            }
            catch
            {
                return null;
            }
        }
    }
}
