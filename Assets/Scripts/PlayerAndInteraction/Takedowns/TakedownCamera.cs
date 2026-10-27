using UnityEngine;
using Blackout.Shared;

namespace Blackout.Takedowns
{
    /// <summary>Temporary pose on the existing camera. This project has no Cinemachine package.</summary>
    internal sealed class TakedownCamera
    {
        private Camera camera;
        private Vector3 localPosition, entrancePosition, returnPosition;
        private Quaternion localRotation, entranceRotation, returnRotation;
        private float fieldOfView, returnFov;
        private const int WorldMask = 1 << PhysicsLayers.Default | 1 << PhysicsLayers.Level | 1 << PhysicsLayers.Interactable;

        public void Begin(Camera value)
        {
            camera = value;
            localPosition = camera.transform.localPosition;
            localRotation = camera.transform.localRotation;
            fieldOfView = camera.fieldOfView;
            entrancePosition = camera.transform.position;
            entranceRotation = camera.transform.rotation;
        }

        public void Frame(Vector3 anchor, Quaternion facing, TakedownProfile profile, float blend, float progress)
        {
            if (camera == null) return;
            Vector3 focus = anchor + Vector3.up * profile.focusHeight;
            Vector3 desired = anchor + facing * Vector3.Lerp(profile.cameraOffset, profile.cameraEndOffset, progress);
            Vector3 position = Vector3.Lerp(entrancePosition, desired, blend);
            Vector3 ray = position - focus;
            if (ray.magnitude > 0.01f && Physics.SphereCast(focus, 0.12f, ray.normalized, out RaycastHit hit,
                ray.magnitude, WorldMask, QueryTriggerInteraction.Ignore)) position = hit.point + hit.normal * 0.15f;
            Quaternion rotation = Quaternion.LookRotation(focus - position, Vector3.up);
            camera.transform.SetPositionAndRotation(position, Quaternion.Slerp(entranceRotation, rotation, blend));
            camera.fieldOfView = Mathf.Lerp(fieldOfView, profile.fieldOfView, blend);
        }

        public void BeginReturn()
        {
            if (camera == null) return;
            returnPosition = camera.transform.position;
            returnRotation = camera.transform.rotation;
            returnFov = camera.fieldOfView;
        }

        public void Return(float progress)
        {
            if (camera == null) return;
            Transform parent = camera.transform.parent;
            Vector3 position = parent != null ? parent.TransformPoint(localPosition) : localPosition;
            Quaternion rotation = parent != null ? parent.rotation * localRotation : localRotation;
            camera.transform.SetPositionAndRotation(Vector3.Lerp(returnPosition, position, progress), Quaternion.Slerp(returnRotation, rotation, progress));
            camera.fieldOfView = Mathf.Lerp(returnFov, fieldOfView, progress);
        }

        public void Restore()
        {
            if (camera == null) return;
            camera.transform.SetLocalPositionAndRotation(localPosition, localRotation);
            camera.fieldOfView = fieldOfView;
            camera = null;
        }
    }
}
