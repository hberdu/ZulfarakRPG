using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZulfarakRPG
{
    // Map-travel mini-cutscene: a riderless horse gallops in from off-screen to the hero, then
    // the screen fades and the selected settlement loads. Triggered by the world map (native
    // popup via OverlayWindow, or the editor WorldMapPanel) instead of teleporting instantly.
    public class HorseCutscene : MonoBehaviour
    {
        static bool _running;
        string _scene;

        // Statics survive editor play sessions when domain reload is disabled; reset on every play
        // start so a cutscene interrupted by the scene load can't leave _running stuck true (which
        // would silently block every future map-travel cutscene).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState() => _running = false;

        public static void Play(string targetScene)
        {
            if (_running || string.IsNullOrEmpty(targetScene)) return;
            _running = true;
            var go = new GameObject("HorseCutscene");
            go.AddComponent<HorseCutscene>()._scene = targetScene;
        }

        void Start() => StartCoroutine(Run());

        // Guard wrapper: guarantees _running is cleared even if RunInner throws before it resets.
        // A wedged _running would make Play() early-return forever, silently killing every future
        // map-travel cutscene (the horse would "stop appearing" after one bad run).
        IEnumerator Run()
        {
            try { yield return RunInner(); }
            finally { _running = false; }
        }

        IEnumerator RunInner()
        {
            var frames = LoadHorseFrames();
            var player = FindAnyObjectByType<PlayerController2D>();

            if (frames == null || frames.Length == 0)   // no art → just fade + load
            {
                SceneFader.FadeToBlack(0.4f, () => SceneManager.LoadScene(_scene));
                _running = false;
                yield break;
            }

            Vector3 ppos = player != null ? player.transform.position : new Vector3(1f, -0.35f, 0f);

            // Size the horse relative to the hero's VISIBLE height (alpha-trimmed so transparent
            // frame padding doesn't count), so it reads at a believable scale next to the hero.
            float playerH = 0.8f;
            var psr = player != null ? player.GetComponent<SpriteRenderer>() : null;
            if (psr != null && psr.sprite != null)
            {
                var ab = SpriteAlphaBounds.Get(psr.sprite);
                playerH = Mathf.Max(0.2f, (ab.topFromBottom - ab.bottomFromBottom) * Mathf.Abs(player.transform.lossyScale.y));
            }
            const float horseFrameWorldH = 21f / 100f;          // 21px frame @100PPU
            // Horse a touch SMALLER than the hero, clamped so a bad playerH read can't make it tiny
            // (invisible) or huge (fills the screen). ponytail: visual knobs — nudge if it reads off.
            float horseWorldH = Mathf.Clamp(playerH * 0.85f, 0.5f, 0.85f);
            float horseScale  = horseWorldH / horseFrameWorldH;
            // Rider sits DOWN on the horse's back — legs overlap the body, upper body above — like the
            // Lancer's mounted pose, instead of standing tall on top with a gap under it.
            float backY = horseWorldH * 0.35f;

            var cam = Camera.main;
            float halfW = cam != null ? cam.orthographicSize * cam.aspect : 4f;
            float camX  = cam != null ? cam.transform.position.x : ppos.x;
            float rightX = camX + halfW + 1.5f;            // start just off the right edge

            var horse = new GameObject("CutsceneHorse");
            var sr = horse.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 250;
            sr.sprite  = frames[0];
            sr.flipX   = true;                             // art faces RIGHT; flip to gallop LEFT toward the hero
            horse.transform.position   = new Vector3(rightX, ppos.y, 0f);
            horse.transform.localScale = Vector3.one * horseScale;

            int fi = 0; float animT = 0f, bobT = 0f;

            // ── 1) Gallop in from the right, stop just beside the hero ──────────────
            // Keep the stop point INSIDE the visible frame. Map-travel is usually triggered at the
            // right-edge portal, where ppos.x + 0.55 would sit off-screen and the whole gallop +
            // mount played out of view — only the rider's motion showed, "no horse".
            float stopX = Mathf.Clamp(ppos.x + 0.55f, camX - halfW + 0.9f, camX + halfW - 0.9f);
            const float inSpeed = 7.5f;
            while (horse.transform.position.x - stopX > 0.12f)
            {
                bobT += Time.deltaTime * 14f;
                var p = horse.transform.position;
                p.x -= inSpeed * Time.deltaTime;
                p.y  = ppos.y + Mathf.Abs(Mathf.Sin(bobT)) * 0.08f;   // gallop bob
                horse.transform.position = p;
                animT += Time.deltaTime;
                if (animT >= 0.07f) { animT = 0f; fi = (fi + 1) % frames.Length; sr.sprite = frames[fi]; }
                yield return null;
            }

            // ── 2) The hero mounts: lift onto the horse's back over a short beat ────
            if (player != null) player.BeginRide();
            Vector3 mountFrom = player != null ? player.transform.position : horse.transform.position;
            float m = 0f;
            const float mountDur = 0.35f;
            while (m < mountDur)
            {
                m += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, m / mountDur);
                var hp = horse.transform.position;
                if (player != null)
                    player.transform.position = Vector3.Lerp(mountFrom, new Vector3(hp.x - 0.06f, hp.y + backY, hp.z), k);
                animT += Time.deltaTime;
                if (animT >= 0.09f) { animT = 0f; fi = (fi + 1) % frames.Length; sr.sprite = frames[fi]; }
                yield return null;
            }

            // ── 3) Ride off to the left; fade to black partway; load the settlement ─
            PortalSmoke.BurstAt(new Vector3(horse.transform.position.x, ppos.y + 0.1f, 0f), 6);
            bool faded = false;
            float rideT = 0f;
            const float rideDur = 1.15f, rideSpeed = 6.5f;
            while (rideT < rideDur)
            {
                rideT += Time.deltaTime;
                bobT  += Time.deltaTime * 14f;
                var hp = horse.transform.position;
                hp.x -= rideSpeed * Time.deltaTime;
                hp.y  = ppos.y + Mathf.Abs(Mathf.Sin(bobT)) * 0.09f;
                horse.transform.position = hp;
                if (player != null)
                    player.transform.position = new Vector3(hp.x - 0.06f, hp.y + backY, hp.z);
                animT += Time.deltaTime;
                if (animT >= 0.06f) { animT = 0f; fi = (fi + 1) % frames.Length; sr.sprite = frames[fi]; }
                if (!faded && rideT >= rideDur * 0.45f)
                {
                    faded = true;                          // hero + horse ride off, then the scene swaps
                    _running = false;                      // reset BEFORE the scene load destroys us
                    SceneFader.FadeToBlack(0.5f, () => SceneManager.LoadScene(_scene));
                }
                yield return null;
            }

            if (!faded)
            {
                _running = false;
                SceneFader.FadeToBlack(0.4f, () => SceneManager.LoadScene(_scene));
            }
        }

        // Public so the settlement's grazing horse can reuse the exact same art (the "same horse
        // from the travel animation"). Pivot is bottom-centre so callers seat it on the ground.
        public static Sprite[] LoadHorseFrames()
        {
            var tex = Resources.Load<Texture2D>("horse_gallop");
            if (tex == null) return null;

            // Horizontal strip of frames authored at a 43×21 aspect. Derive the frame size from
            // the texture's ACTUAL height (instead of a hardcoded 21) so it still slices correctly
            // if the art was exported at a different scale — a too-short fixed height would clip
            // the frame to a near-empty strip and the horse would render invisible.
            int fh = tex.height;
            int guessW = Mathf.Max(1, Mathf.RoundToInt(fh * 43f / 21f));
            int n  = Mathf.Max(1, Mathf.RoundToInt(tex.width / (float)guessW));
            int fw = Mathf.Max(1, tex.width / n);   // exact division so frames tile the sheet cleanly

            var arr = new Sprite[n];
            for (int i = 0; i < n; i++)
                arr[i] = Sprite.Create(tex, new Rect(i * fw, 0, fw, fh), new Vector2(0.5f, 0f), 100f);
            return arr;
        }
    }
}
