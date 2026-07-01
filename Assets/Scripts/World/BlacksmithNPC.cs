using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZulfarakRPG
{
    // City NPC: the blacksmith ("Ferreiro"). Auto-spawned in Zulfarak with the
    // Knight (Soldier) idle frames borrowed from PlayerController2D so we don't
    // need to wire sprite assets through the scene file. Note: the source
    // sprite still includes a sword/shield — a true unarmed knight needs new
    // art; this is the closest fit with the existing character set.
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

            // Borrow Soldier idle frames from the in-scene player or the class master.
            Sprite[] frames = null;
            var player = Object.FindAnyObjectByType<PlayerController2D>();
            if (player != null && player.soldierIdleFrames != null &&
                player.soldierIdleFrames.Length > 0)
                frames = player.soldierIdleFrames;
            if (frames == null || frames.Length == 0)
            {
                var master = Object.FindAnyObjectByType<ClassMasterNPC>();
                if (master != null) frames = master.warriorIdleFrames;
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
            // Sooty/leather tone so the blacksmith reads as a tradesman, not a
            // soldier (and slightly mutes the sword the source sprite carries).
            sr.color        = new Color(0.62f, 0.50f, 0.38f, 1f);
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

            // Tiny floating name tag above the head, matching player + class master.
            NameTag.Attach(sr, "Ferreiro", yOffsetWorld: 0.62f);

            return go.AddComponent<BlacksmithNPC>();
        }
    }
}
