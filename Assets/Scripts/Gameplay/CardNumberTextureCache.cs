using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BreathCasino.Gameplay
{
    /// <summary>
    /// Кэш текстур с цифрами/символами для трипланарного наложения на карты.
    /// Генерирует текстуры в runtime через Canvas + RenderTexture.
    /// </summary>
    public static class CardNumberTextureCache
    {
        private const int TexSize = 128;
        private static readonly Dictionary<string, Texture2D> Cache = new();
        private static Font _font;

        public static Texture2D GetTextureForMarking(string marking)
        {
            if (string.IsNullOrEmpty(marking)) marking = "?";
            if (Cache.TryGetValue(marking, out var tex) && tex != null)
                return tex;

            tex = RenderMarkingToTexture(marking);
            if (tex != null)
                Cache[marking] = tex;
            return tex;
        }

        private static Texture2D RenderMarkingToTexture(string marking)
        {
            if (_font == null)
            {
                _font = BCRuntimeFontProvider.Get(64);
            }
            if (_font == null) return CreateFallbackTexture();

            var desc = new RenderTextureDescriptor(TexSize, TexSize, RenderTextureFormat.ARGB32, 16);
            var rt = RenderTexture.GetTemporary(desc);
            var prev = RenderTexture.active;

            var camObj = new GameObject("_TempCardNumberCam");
            var cam = camObj.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 0.5f;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 2f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.targetTexture = rt;
            cam.transform.position = new Vector3(0, 0, -1);
            cam.enabled = false;

            var canvasObj = new GameObject("_TempCanvas");
            canvasObj.transform.position = Vector3.forward * 10f;
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam;

            var rectRoot = canvas.GetComponent<RectTransform>();
            rectRoot.sizeDelta = new Vector2(1f, 1f);
            rectRoot.localScale = Vector3.one;

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(canvasObj.transform, false);
            var text = textObj.AddComponent<Text>();
            text.text = marking;
            text.font = _font;
            text.fontSize = 64;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;

            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            cam.Render();

            var tex2D = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false);
            RenderTexture.active = rt;
            tex2D.ReadPixels(new Rect(0, 0, TexSize, TexSize), 0, 0);
            tex2D.Apply();

            RenderTexture.active = prev;
            cam.targetTexture = null;
            Object.DestroyImmediate(camObj);
            Object.DestroyImmediate(canvasObj);
            RenderTexture.ReleaseTemporary(rt);

            return tex2D;
        }

        private static Texture2D CreateFallbackTexture()
        {
            var tex = new Texture2D(4, 4);
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < 4; x++)
                    tex.SetPixel(x, y, new Color(1, 1, 1, 0.5f));
            tex.Apply();
            return tex;
        }
    }
}
