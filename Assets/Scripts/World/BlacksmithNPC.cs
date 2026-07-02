using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZulfarakRPG
{
    // City NPC: the blacksmith ("Ferreiro"). Auto-spawned in Zulfarak using the
    // Swordsman idle sheet (Resources/Swordsman-Idle), sliced into frames at
    // runtime so no scene/prefab wiring is needed. Falls back to borrowed Soldier
    // frames only if that sheet is missing.
    public class BlacksmithNPC : MonoBehaviour
    {
        // ── Auto-spawn ────────────────────────────────────────────────────
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RegisterSceneHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "Zulfarak") return;
            if (Object.FindAnyObjectByType<BlacksmithNPC>() != null) return;

            // Swordsman idle sheet, sliced at runtime. Fall back to borrowed
            // Soldier frames only if the sheet can't be loaded.
            Sprite[] frames = LoadSwordsmanIdleFrames();
            if (frames == null || frames.Length == 0)
            {
                var player = Object.FindAnyObjectByType<PlayerController2D>();
                if (player != null && player.soldierIdleFrames != null &&
                    player.soldierIdleFrames.Length > 0)
                    frames = player.soldierIdleFrames;
                if (frames == null || frames.Length == 0)
                {
                    var master = Object.FindAnyObjectByType<ClassMasterNPC>();
                    if (master != null) frames = master.warriorIdleFrames;
                }
            }
            if (frames == null || frames.Length == 0) return;

            // Slot just to the right of the ClassMaster (x≈2.55) and well clear of
            // the DungeonPortal (x≈4.5) so the three NPCs line up as a group.
            SpawnAt(new Vector3(3.10f, -1.144f, 0f), frames);
        }

        public static BlacksmithNPC SpawnAt(Vector3 worldPos, Sprite[] idleFrames)
        {
            var go = new GameObject("Ferreiro_NPC");
            go.transform.position   = worldPos;
            go.transform.localScale = new Vector3(2f, 2f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = idleFrames[0];
            // Show the Swordsman art in its true colors.
            sr.color        = Color.white;
            sr.sortingOrder = 4;
            // Back to the portal (which sits to the right) — source art faces right,
            // so flipX rotates the sprite to face left.
            sr.flipX        = true;

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size      = new Vector2(0.3f, 0.2f);
            col.offset    = new Vector2(0f, 0.5f);

            // Static at its spawn Y (Kael's ground line, y=-1.144) with only the trigger
            // collider above — so it rests correctly AND doesn't physically block the
            // player from walking past to the portal.

            var anim = go.AddComponent<SimpleIdleAnim>();
            anim.frames = idleFrames;
            anim.fps    = 6f;

            var inter = go.AddComponent<Interactable2D>();
            inter.tooltipText   = "Ferreiro";
            inter.tooltipOffset = new Vector2(0f, 0.85f);
            inter.popupTitle    = "Ferreiro";
            inter.popupBody     =
                "Forja em construção.\n\n" +
                "Em breve você poderá aprimorar suas armas e armaduras aqui.";

            // No floating name tag — the name shows on hover (Interactable2D tooltip).

            return go.AddComponent<BlacksmithNPC>();
        }

        // ── Swordsman idle frames ─────────────────────────────────────────
        static Sprite[] _swordsmanFrames;

        // Loads the Swordsman idle sheet (600×100 = 6 frames of 100×100) from
        // Resources and slices it into per-frame sprites at runtime. Cached.
        static Sprite[] LoadSwordsmanIdleFrames()
        {
            if (_swordsmanFrames != null) return _swordsmanFrames;
            var src = Resources.Load<Sprite>("Swordsman-Idle");
            var tex = src != null ? src.texture : Resources.Load<Texture2D>("Swordsman-Idle");
            if (tex == null)
            {
                Debug.LogWarning("[Ferreiro] Swordsman-Idle não encontrado em Resources — usando fallback.");
                return null;
            }

            const int fw = 100, fh = 100;
            int count = Mathf.Max(1, tex.width / fw);
            var frames = new Sprite[count];
            for (int i = 0; i < count; i++)
                frames[i] = Sprite.Create(tex, new Rect(i * fw, 0, fw, fh),
                                          new Vector2(0.5f, 0.5f), 100f);
            _swordsmanFrames = frames;
            Debug.Log($"[Ferreiro] Swordsman-Idle carregado: {count} frames ({tex.width}x{tex.height}).");
            return frames;
        }
    }
}
