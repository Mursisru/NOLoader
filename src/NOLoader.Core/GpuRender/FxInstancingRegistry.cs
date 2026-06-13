#if !NOLoader_DEV
using System.Collections.Generic;
using NOLoader.Core.Logging;
using NOLoader.Core.Runtime;
using UnityEngine;

namespace NOLoader.Core.GpuRender
{
    internal sealed class FxInstancingEntry
    {
        internal Transform Transform = null!;
        internal Renderer? Renderer;
        internal int TypeId;
    }

    internal static class FxInstancingRegistry
    {
        private static readonly List<FxInstancingEntry> Active = new List<FxInstancingEntry>(128);
        private static Mesh? _mesh;
        private static Material? _material;
        private static readonly List<Matrix4x4> Matrices = new List<Matrix4x4>(128);
        private static bool _initialized;
        private static int _registered;
        private static int _lastDrawn;

        internal static int RegisteredTotal => _registered;
        internal static int LastDrawn => _lastDrawn;

        internal static void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;
            _mesh = CreateProxyMesh();
            Shader? shader = Shader.Find("Unlit/Color");
            if (shader != null)
                _material = new Material(shader) { color = new Color(1f, 0.85f, 0.2f, 0.8f) };

            RingBufferLog.WriteAscii("[GpuRender] FxInstancingRegistry initialized");
        }

        internal static void Register(Transform transform, int typeId)
        {
            if (transform == null)
                return;

            Renderer? r = transform.GetComponentInChildren<Renderer>();
            if (r != null)
                r.enabled = false;

            Active.Add(new FxInstancingEntry { Transform = transform, Renderer = r, TypeId = typeId });
            _registered++;
        }

        internal static void TickAndDraw()
        {
            if (!RuntimeConfig.GpuFxInstancingEnabled || _material == null || _mesh == null)
                return;

            Matrices.Clear();
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                FxInstancingEntry entry = Active[i];
                if (entry.Transform == null)
                {
                    Active.RemoveAt(i);
                    continue;
                }

                Matrices.Add(Matrix4x4.TRS(entry.Transform.position, entry.Transform.rotation, Vector3.one * 0.5f));
            }

            _lastDrawn = Matrices.Count;
            if (_lastDrawn == 0)
                return;

            for (int batch = 0; batch < _lastDrawn; batch += 1023)
            {
                int count = System.Math.Min(1023, _lastDrawn - batch);
                Graphics.DrawMeshInstanced(_mesh, 0, _material, Matrices.GetRange(batch, count));
            }
        }

        private static Mesh CreateProxyMesh()
        {
            var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(temp);
            return mesh;
        }
    }
}
#endif
