using UnityEngine;

namespace ZulfarakRPG
{
    public class CameraFollow2D : MonoBehaviour
    {
        public Transform target;
        public float     smoothSpeed = 8f;
        public float     minX        = -1000f;
        public float     maxX        =  1000f;
        public float     fixedY      = 0f;

        // Biases the hero LEFT of centre, as a fraction of the half-view-width (0 = centred,
        // 0.4 ≈ hero sits ~30% from the left edge). Used in the dungeon so enemies rushing in
        // from the right are visible ahead of the hero. Width-relative, so it adapts if the
        // window is resized.
        public float     leftBias    = 0f;

        Camera _cam;

        void Awake() { _cam = GetComponent<Camera>(); }

        void LateUpdate()
        {
            if (!target) return;
            if (_cam == null) _cam = GetComponent<Camera>();
            float halfW = (_cam != null && _cam.orthographic) ? _cam.orthographicSize * _cam.aspect : 0f;
            // Camera centre sits to the RIGHT of the hero by leftBias·halfW, pushing the hero
            // toward the left of the screen.
            float tx = Mathf.Clamp(target.position.x + leftBias * halfW, minX, maxX);
            var desired = new Vector3(tx, fixedY, transform.position.z);
            transform.position = Vector3.Lerp(transform.position, desired, smoothSpeed * Time.deltaTime);
        }
    }
}
