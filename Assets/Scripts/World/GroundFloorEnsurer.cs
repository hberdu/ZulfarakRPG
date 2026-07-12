using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZulfarakRPG
{
    // Defensive floor: guarantees a static physics collider exists along the ground
    // line in every gameplay scene. Without this, if the scene's "Ground" object
    // lost its Collider2D (e.g. after a scene edit), the gravity-driven player and
    // skeletons fall through the world forever.
    //
    // Auto-spawns on scene load — no scene editing required. Spans a wide horizontal
    // strip at GroundAlignUtil.FindGroundTopY() with its TOP edge on the ground line,
    // so characters with feet-fitted colliders rest exactly on the visible ground.
    public class GroundFloorEnsurer : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RegisterSceneHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!MapBounds.IsGameplayScene(scene.name)) return;
            if (Object.FindAnyObjectByType<GroundFloorEnsurer>() != null) return;

            float groundTop = GroundAlignUtil.FindGroundTopY();

            var go = new GameObject("GroundFloor");
            go.AddComponent<GroundFloorEnsurer>();

            // Wide, thick static box; its TOP edge sits exactly on the ground line.
            const float width = 40f, thickness = 4f;
            go.transform.position = new Vector3(2.5f, groundTop - thickness * 0.5f, 0f);

            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(width, thickness);

            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;

            Debug.Log($"[GroundFloorEnsurer] Floor created at groundTop={groundTop:F3} (top edge of collider).");
        }
    }
}
