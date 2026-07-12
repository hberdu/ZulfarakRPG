using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZulfarakRPG
{
    // Runtime dressing for the settlement hubs, applied on scene load (same pattern as
    // BackgroundLayers / MapBounds) so the build reflects the latest design without re-running
    // the editor scene wizards:
    //   • Campfire  → click to fully heal, with a "Recuperar Vida" hover tooltip.
    //   • Camps beyond the first city (Camp_*) drop their NPCs and get the travel horse grazing
    //     next to the wagon.
    public static class CampSceneHooks
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Hook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            WireCampfire();

            // Every settlement EXCEPT the first city (Zulfarak) is authored as "Camp_*".
            if (scene.name.StartsWith("Camp_"))
            {
                StripNpcs();
                AddGrazingHorse();
            }
        }

        // Turn the campfire into a rest point: hover shows "Recuperar Vida", click heals to full.
        static void WireCampfire()
        {
            var fire = GameObject.Find("Campfire");
            if (fire == null || fire.GetComponent<Interactable2D>() != null) return;

            if (fire.GetComponent<Collider2D>() == null)
            {
                var col = fire.AddComponent<BoxCollider2D>();
                col.isTrigger = true;
                col.size   = new Vector2(0.42f, 0.50f);
                col.offset = new Vector2(0f, 0.22f);
            }

            var it = fire.AddComponent<Interactable2D>();
            it.tooltipText   = "Recuperar Vida";
            it.tooltipOffset = new Vector2(0f, 0.62f);
            it.onClick = () =>
            {
                var p = Object.FindAnyObjectByType<PlayerController2D>();
                if (p != null) p.Heal(p.MaxHealthValue);   // top the hero up to max
            };
        }

        static void StripNpcs()
        {
            foreach (var n in new[] { "Kael_NPC", "ClassMaster_NPC" })
            {
                var go = GameObject.Find(n);
                if (go != null) Object.Destroy(go);
            }
        }

        static void AddGrazingHorse()
        {
            var wagon = GameObject.Find("Wagon");
            if (wagon == null) return;
            Vector3 wp = wagon.transform.position;
            GrazingHorse.Spawn(new Vector3(wp.x - 0.95f, wp.y, 0f));   // just left of the wagon
        }
    }

    // A decorative horse — the SAME art as the travel cutscene (horse_gallop frame 0) — grazing
    // beside the wagon. No walk-in/mount; it just lowers its head to the grass on a slow loop.
    public class GrazingHorse : MonoBehaviour
    {
        // Withers height as a fraction of the hero's visible height. Slightly under 1 reads as a
        // realistic decorative horse; nudge here if it still looks too big/small in a settlement.
        const float HorseHeightVsHero = 0.9f;

        SpriteRenderer _sr;
        Vector3 _groundPos, _basePos, _baseScale;
        bool    _ready;
        float   _t;

        public static void Spawn(Vector3 groundPos)
        {
            var frames = HorseCutscene.LoadHorseFrames();
            if (frames == null || frames.Length == 0) return;

            var go = new GameObject("GrazingHorse");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = frames[0];
            sr.sortingOrder = -3;                 // by the wagon (-4), behind the hero (1)

            var gh = go.AddComponent<GrazingHorse>();
            gh._sr = sr; gh._groundPos = groundPos;
        }

        // Size + seat a few frames in — AFTER PlayerController2D.Start() shrinks the hero. Sizing
        // synchronously on scene-load read the hero's pre-shrink height and made the horse oversized.
        System.Collections.IEnumerator Start()
        {
            yield return null; yield return null; yield return null;

            float playerH = 0.8f;
            var player = Object.FindAnyObjectByType<PlayerController2D>();
            var psr = player != null ? player.GetComponent<SpriteRenderer>() : null;
            if (psr != null && psr.sprite != null)
            {
                var pab = SpriteAlphaBounds.Get(psr.sprite);
                playerH = Mathf.Max(0.2f, (pab.topFromBottom - pab.bottomFromBottom) * Mathf.Abs(player.transform.lossyScale.y));
            }
            const float frameWorldH = 21f / 100f;                    // 21px frame @100PPU
            float scale = playerH * HorseHeightVsHero / frameWorldH;
            transform.localScale = Vector3.one * scale;

            // Seat the alpha-trimmed feet on the ground (frame pivot is bottom-centre).
            var ab = SpriteAlphaBounds.Get(_sr.sprite);
            transform.position = new Vector3(_groundPos.x, _groundPos.y - ab.bottomFromBottom * scale, 0f);

            _basePos   = transform.position;
            _baseScale = transform.localScale;
            _ready     = true;
        }

        void Update()
        {
            if (!_ready) return;
            _t += Time.deltaTime;

            // Graze loop: head (art faces RIGHT) dips to the grass, nibbles, lifts, brief idle.
            const float cycle = 5.0f;
            float u = (_t % cycle) / cycle;
            float dip;
            if      (u < 0.25f) dip = Mathf.SmoothStep(0f, 1f, u / 0.25f);            // head down
            else if (u < 0.60f) dip = 1f;                                             // eating
            else if (u < 0.72f) dip = Mathf.SmoothStep(1f, 0f, (u - 0.60f) / 0.12f);  // head up
            else                dip = 0f;                                             // idle

            // Nibble while eating: a quick small bob on top of the full dip so the head never freezes.
            float nibble = (u >= 0.25f && u < 0.60f) ? Mathf.Sin(_t * 14f) * 3.0f : 0f;
            // Idle sway when the head is up, plus an occasional tail-flick-style twitch.
            float sway   = Mathf.Sin(_t * 0.9f) * 1.2f * (1f - dip);
            float twitch = 0f;
            if (u >= 0.72f) { float e = Mathf.Repeat(_t, 1.3f); if (e < 0.15f) twitch = Mathf.Sin(e / 0.15f * Mathf.PI) * 2.5f; }

            transform.rotation = Quaternion.Euler(0f, 0f, -12f * dip - nibble + sway + twitch);

            // Breathing pulse — always on, so the horse is never a frozen statue.
            var sc = _baseScale; sc.y *= 1f + 0.018f * Mathf.Sin(_t * 2.2f); transform.localScale = sc;
            transform.position = _basePos + new Vector3(0f, -0.02f * dip, 0f);
        }
    }
}
