using NOLoader.API;
using NOLoader.API.World;
using UnityEngine;

namespace NOLoader.Core.EngineTweaker
{
    internal sealed class EngineFrameCacheImpl : INOModFrameCache
    {
        public static readonly EngineFrameCacheImpl Instance = new EngineFrameCacheImpl();

        private int _frameId;
        private bool _hasCamera;
        private bool _hasAircraft;
        private Vector3 _cameraPosition;
        private Vector3 _aircraftPosition;
        private long _reads;
        private long _nativeFallbacks;

        internal long Reads => _reads;
        internal long NativeFallbacks => _nativeFallbacks;

        public int FrameId => _frameId;

        internal void ResetStats()
        {
            _reads = 0;
            _nativeFallbacks = 0;
        }

        internal void PopulateFromCameraState(object csm)
        {
            if (!Runtime.RuntimeConfig.FrameCacheEnabled)
                return;

            int frame = Time.frameCount;
            if (frame == _frameId)
                return;

            _frameId = frame;
            _hasCamera = false;
            _hasAircraft = false;

            if (!EngineTweakerGameAccess.TryReadCameraState(csm, out _, out object? followingUnit, out Camera? mainCamera))
                return;

            if (mainCamera != null)
            {
                _cameraPosition = mainCamera.transform.position;
                _hasCamera = true;
            }

            if (followingUnit != null && EngineTweakerGameAccess.TryReadTransformPosition(followingUnit, out Vector3 pos))
            {
                _aircraftPosition = pos;
                _hasAircraft = true;
            }
        }

        public bool TryGetCameraPosition(out NOVec3 position)
        {
            _reads++;
            if (_hasCamera)
            {
                position = new NOVec3(_cameraPosition.x, _cameraPosition.y, _cameraPosition.z);
                return true;
            }

            _nativeFallbacks++;
            position = default;
            return false;
        }

        public bool TryGetLocalAircraftPosition(out NOVec3 position)
        {
            _reads++;
            if (_hasAircraft)
            {
                position = new NOVec3(_aircraftPosition.x, _aircraftPosition.y, _aircraftPosition.z);
                return true;
            }

            _nativeFallbacks++;
            position = default;
            return false;
        }
    }
}
