#if !NOLoader_DEV
using System.Collections.Generic;
using NOLoader.API;
using NOLoader.Core.Logging;
using NOLoader.Core.Runtime;
using UnityEngine;
using UnityEngine.Rendering;

namespace NOLoader.Core.GpuRender
{
    internal static class GpuHudPass
    {
        private static Mesh? _quad;
        private static Material? _material;
        private static readonly List<Matrix4x4> Matrices = new List<Matrix4x4>(256);
        private static bool _initialized;
        private static int _lastDrawn;

        internal static int LastDrawn => _lastDrawn;

        internal static void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;

            if (!RuntimeConfig.GpuHudPassEnabled && !RuntimeConfig.GpuFxInstancingEnabled)
            {
                RingBufferLog.WriteAscii("[GpuRender] GpuHudPass skipped (no hud/fx sub-flags)");
                return;
            }

            if (RuntimeConfig.GpuHudPassEnabled)
            {
                _quad = CreateQuad();
                Shader? shader = Shader.Find("Sprites/Default");
                if (shader == null)
                    shader = Shader.Find("Unlit/Color");
                if (shader != null)
                    _material = new Material(shader);
            }

            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            RingBufferLog.WriteAscii("[GpuRender] GpuHudPass initialized hud=" + RuntimeConfig.GpuHudPassEnabled);
        }

        internal static void Shutdown()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            if (_material != null)
                Object.Destroy(_material);
            if (_quad != null)
                Object.Destroy(_quad);
            _initialized = false;
        }

        internal static void UploadMarkers(IReadOnlyList<Vector3> screenPositions, IReadOnlyList<Color> colors)
        {
            Matrices.Clear();
            for (int i = 0; i < screenPositions.Count; i++)
            {
                Vector3 p = screenPositions[i];
                float scale = 12f;
                Matrices.Add(Matrix4x4.TRS(p, Quaternion.identity, new Vector3(scale, scale, 1f)));
            }
        }

        private static void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera == null || camera.cameraType != CameraType.Game)
                return;

            bool hudDraw = RuntimeConfig.GpuHudPassEnabled && _material != null && _quad != null;
            bool gpuMods = GpuComputeService.Instance.HasRegisteredMods;
            if (!hudDraw && !gpuMods)
                return;

            if (hudDraw)
            {
                _lastDrawn = Matrices.Count;
                if (_lastDrawn > 0)
                {
                    var props = new MaterialPropertyBlock();
                    for (int batch = 0; batch < _lastDrawn; batch += 1023)
                    {
                        int count = System.Math.Min(1023, _lastDrawn - batch);
                        var slice = Matrices.GetRange(batch, count);
                        Graphics.DrawMeshInstanced(_quad, 0, _material, slice, props);
                    }
                }
            }

            if (gpuMods)
                DispatchModCompute(context);
        }

        private static void DispatchModCompute(ScriptableRenderContext context)
        {
            var cmd = new CommandBuffer { name = "NOLoader.GpuCompute" };
            var ctx = default(NOModContext);
            GpuComputeService.Instance.Dispatch(cmd, ref ctx);
            if (cmd.sizeInBytes > 0)
                context.ExecuteCommandBuffer(cmd);
            cmd.Release();
        }

        private static Mesh CreateQuad()
        {
            var mesh = new Mesh { name = "NOLoader.GpuHudQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
#endif
