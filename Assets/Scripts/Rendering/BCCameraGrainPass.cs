// Assets/Scripts/Rendering/BCCameraGrainPass.cs
// ScriptableRenderPass — выполняет full-screen blit шейдера BlockoutCameraGrain.
// Используется только внутри BCCameraGrainFeature.

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace BreathCasino.Rendering
{
    /// <summary>
    /// Full-screen blit pass. Срабатывает после прозрачных объектов.
    /// Читает activeColorTexture → пишет обработанный результат обратно.
    /// </summary>
    internal sealed class BCCameraGrainPass : ScriptableRenderPass
    {
        private Material _material;

        internal BCCameraGrainPass()
        {
            // Выполняем самыми последними чтобы не сломать прозрачность
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        internal void Setup(Material mat) => _material = mat;

        // ── Render Graph API (Unity 6 / URP 17) ─────────────────────
        public override void RecordRenderGraph(
            RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_material == null) return;

            var resourceData = frameData.Get<UniversalResourceData>();

            // Не бежим если активная цель — back-buffer (нет промежуточного RT)
            if (resourceData.isActiveTargetBackBuffer) return;

            TextureHandle src = resourceData.activeColorTexture;

            // Создаём временный RT через RenderTextureDescriptor (совместимо с URP 17 / Unity 6)
            var srcDesc = renderGraph.GetTextureDesc(src);
            var rtDesc  = new UnityEngine.RenderTextureDescriptor(
                srcDesc.width, srcDesc.height,
                srcDesc.colorFormat, 0);
            rtDesc.useMipMap        = false;
            rtDesc.autoGenerateMips = false;

            TextureHandle dst = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, rtDesc, "BlockoutGrainDst", false);

            using var builder = renderGraph.AddRasterRenderPass<PassData>(
                "BlockoutCameraGrain", out var passData);

            passData.Material = _material;
            passData.Source   = src;

            builder.UseTexture(src);
            builder.SetRenderAttachment(dst, 0);

            builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
            {
                Blitter.BlitTexture(ctx.cmd,
                    data.Source,
                    new Vector4(1f, 1f, 0f, 0f),
                    data.Material,
                    0); // pass index = 0 = "BlockoutCameraGrain"
            });

            // Подменяем активный цветовой буфер результатом
            resourceData.cameraColor = dst;
        }

        // PassData — данные передаваемые в лямбду рендер-функции
        private class PassData
        {
            public Material      Material;
            public TextureHandle Source;
        }
    }
}
