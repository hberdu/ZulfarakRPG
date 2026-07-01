using UnityEngine;

namespace ZulfarakRPG
{
    public class CameraFollow2D : MonoBehaviour
    {
        public Transform target;
        public float     smoothSpeed = 8f;
        public float     minX        = 0f;
        public float     maxX        = 50f;
        public float     fixedY      = 0f;

        void LateUpdate()
        {
            if (!target) return;
            float tx = Mathf.Clamp(target.position.x, minX, maxX);
            var desired = new Vector3(tx, fixedY, transform.position.z);
            transform.position = Vector3.Lerp(transform.position, desired, smoothSpeed * Time.deltaTime);
        }
    }
}
