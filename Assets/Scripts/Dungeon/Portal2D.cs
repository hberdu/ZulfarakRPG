using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZulfarakRPG
{
    [RequireComponent(typeof(CircleCollider2D))]
    public class Portal2D : MonoBehaviour
    {
        [Header("Destination")]
        public string destinationScene = "Dungeon";

        [Header("Visual")]
        public SpriteRenderer glowSprite;

        [Header("State")]
        public bool openOnStart = true;

        // RANK A challenge portal (red). Instead of loading a scene, entering it announces
        // "PORTAL RANK A" and summons the extremely hard Minotaur boss into the current scene.
        [Header("Rank A")]
        public bool rankA = false;

        [Header("Tooltip")]
        // Persistent label rendered above the portal, e.g. "1-1" (dungeon 1, phase 1).
        // Leave blank to render no tooltip.
        public string tooltipText;

        public bool IsOpen => _open;

        private bool _open;
        private bool _transitioning;
        private SpriteRenderer[] _rings;
        private GameObject _tooltipRoot;
        private float _hoverRadius;

        // Ring layout: outer → inner
        static readonly float[] RingSizes   = { 1.40f, 1.00f, 0.55f };
        static readonly Color[]  RingColors = {
            new Color(0.55f, 0.18f, 0.95f, 0.55f), // outer: dim purple
            new Color(0.75f, 0.45f, 1.00f, 0.80f), // mid: bright purple
            new Color(0.95f, 0.88f, 1.00f, 0.95f), // inner: white-violet core
        };
        // Red palette for the RANK A challenge portal (same three-ring look, danger colours).
        static readonly Color[]  RankARingColors = {
            new Color(0.95f, 0.15f, 0.12f, 0.55f), // outer: dim red
            new Color(1.00f, 0.35f, 0.26f, 0.80f), // mid: bright red
            new Color(1.00f, 0.85f, 0.80f, 0.95f), // inner: white-hot core
        };

        void Start()
        {
            _open = openOnStart;
            // Tight trigger — only fires when the player actually reaches the bright portal
            // art (or clicks it), instead of a wide entry zone around the glow.
            var pcol = GetComponent<CircleCollider2D>();
            pcol.isTrigger = true;
            pcol.radius    = 0.40f;

            // Build concentric glow rings using the portal sprite
            Sprite spr = glowSprite != null ? glowSprite.sprite : null;
            if (spr == null)
            {
                spr = MakeProceduralRing();
                if (glowSprite != null) glowSprite.sprite = spr;
            }
            var ringColors = rankA ? RankARingColors : RingColors;
            _rings = new SpriteRenderer[RingSizes.Length];
            for (int i = 0; i < RingSizes.Length; i++)
            {
                var go = new GameObject($"GlowRing_{i}");
                go.transform.SetParent(transform, false);
                go.transform.localScale = Vector3.one * RingSizes[i];
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite       = spr;
                sr.color        = ringColors[i];
                sr.sortingOrder = i + 3;
                _rings[i] = sr;
                go.SetActive(_open);
            }

            if (glowSprite) glowSprite.gameObject.SetActive(_open);

            if (!string.IsNullOrEmpty(tooltipText)) BuildTooltip();
        }

        void BuildTooltip()
        {
            _tooltipRoot = new GameObject("Tooltip");
            _tooltipRoot.transform.SetParent(transform, false);
            // Centered ON the portal (slight Z bias so it draws in front of the rings).
            _tooltipRoot.transform.localPosition = new Vector3(0f, 0f, -0.3f);

            // Black balloon background (a 1×1 white pixel sprite tinted to opaque black)
            var bgGO = new GameObject("Bg");
            bgGO.transform.SetParent(_tooltipRoot.transform, false);
            bgGO.transform.localPosition = Vector3.zero;
            bgGO.transform.localScale    = new Vector3(0.30f, 0.14f, 1f);
            var bgSr = bgGO.AddComponent<SpriteRenderer>();
            bgSr.sprite       = MakeWhitePixel();
            bgSr.color        = new Color(0f, 0f, 0f, 0.92f);
            bgSr.sortingOrder = 12;

            // White bold label on top of the balloon
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(_tooltipRoot.transform, false);
            labelGO.transform.localPosition = new Vector3(0f, 0f, -0.1f);
            var tmp = labelGO.AddComponent<TextMeshPro>();
            tmp.text      = tooltipText;
            tmp.fontSize  = 1.4f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color     = Color.white;
            tmp.fontStyle = FontStyles.Bold;
            var mr = labelGO.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 14;

            // Hidden until the mouse hovers the portal collider area
            _tooltipRoot.SetActive(false);

            // Cache the hover radius in WORLD units (CircleCollider2D.radius * lossy scale)
            var col = GetComponent<CircleCollider2D>();
            float scale = transform.lossyScale.x;
            _hoverRadius = col != null ? col.radius * scale : 0.5f * scale;
        }

        static Sprite _whitePixel;
        static Sprite MakeWhitePixel()
        {
            if (_whitePixel != null) return _whitePixel;
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Point;
            t.SetPixel(0, 0, Color.white);
            t.Apply();
            _whitePixel = Sprite.Create(t, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            return _whitePixel;
        }

        // Procedural fallback when the wizard didn't assign a portal sprite. Renders a soft WHITE
        // ring (visible at any scale) so the ring tint (purple / rank-A red) shows true.
        static Sprite _proceduralRing;
        static Sprite MakeProceduralRing()
        {
            if (_proceduralRing != null) return _proceduralRing;
            const int N  = 64;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Bilinear;
            float cx = (N - 1) * 0.5f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x - cx) / cx;
                    float dy = (y - cx) / cx;
                    float d  = Mathf.Sqrt(dx*dx + dy*dy);
                    float a  = Mathf.Clamp01(1f - Mathf.Abs(d - 0.9f) * 4f);
                    a       += Mathf.Clamp01(1f - d) * 0.30f;
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(a)));
                }
            t.Apply();
            _proceduralRing = Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            return _proceduralRing;
        }

        private float _fogTimer;

        void Update()
        {
            if (!_open) return;
            float t = Time.time;

            // Gentle mystic mist curling up out of the portal mouth ("névoa do portal").
            _fogTimer -= Time.deltaTime;
            if (_fogTimer <= 0f)
            {
                _fogTimer = 0.38f;   // gentler, less frequent wisps
                PortalSmoke.WispAt(transform.position);
            }

            // Base sprite: slow, gentle pulse — stays clearly visible at all times
            if (glowSprite != null)
            {
                var c = glowSprite.color;
                c.a = 0.90f + Mathf.Sin(t * 2.0f) * 0.10f;
                glowSprite.color = c;
            }

            if (_rings == null) return;

            float[] speeds   = { 2.2f,  3.5f,  5.2f  };
            float[] phases   = { 0f,    2.09f, 4.19f  }; // 0, 2π/3, 4π/3 — evenly spread
            float[] baseAlp  = { 0.55f, 0.78f, 0.92f  };
            float[] ampAlp   = { 0.10f, 0.12f, 0.08f  };
            float[] baseSc   = RingSizes;
            float[] ampSc    = { 0.08f, 0.05f, 0.04f  };

            for (int i = 0; i < _rings.Length; i++)
            {
                if (_rings[i] == null) continue;
                float s = Mathf.Sin(t * speeds[i] + phases[i]);
                _rings[i].transform.localScale = Vector3.one * (baseSc[i] + s * ampSc[i]);
                var c = _rings[i].color;
                c.a = baseAlp[i] + s * ampAlp[i];
                _rings[i].color = c;
            }

            // Tooltip mouse-hover toggle (only relevant if a tooltip was built)
            if (_tooltipRoot != null && Camera.main != null)
            {
                Vector3 mw = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                mw.z = transform.position.z;
                bool hovering = (mw - transform.position).sqrMagnitude <= _hoverRadius * _hoverRadius;
                if (_tooltipRoot.activeSelf != hovering)
                    _tooltipRoot.SetActive(hovering);
            }
        }

        // Walk INTO the portal (collider) → enter.
        void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            TryEnter();
        }

        // CLICK the portal → enter. Lets the player CHOOSE between the purple (leave) and the red
        // (RANK A) portal in the final dungeon without having to walk into one.
        void OnMouseDown() => TryEnter();

        void TryEnter()
        {
            if (!_open || _transitioning) return;
            // In a multi-player lobby only the leader actually initiates the group transit —
            // followers wait for the leader's PORTAL broadcast, so transitions don't fire out of order.
            var lobby = SteamLobbyManager.Instance;
            if (lobby != null && lobby.InLobby && !lobby.IsLeader) return;
            _transitioning = true;
            StartCoroutine(rankA ? RankAChallenge() : Transition());
        }

        // RANK A: no scene swap. Close the portal, splash "PORTAL RANK A" (same banner as
        // CLEAR/BOSS), then summon the Minotaur into THIS scene for the hero to fight.
        IEnumerator RankAChallenge()
        {
            _open = false;
            if (glowSprite) glowSprite.gameObject.SetActive(false);
            if (_rings != null) foreach (var r in _rings) if (r) r.gameObject.SetActive(false);
            if (_tooltipRoot) _tooltipRoot.SetActive(false);

            // Every party member flashes the surprised "!" as the boss challenge opens.
            MultiplayerSync.Instance?.EmoteAll();
            PixelBanner.Show("PORTAL RANK A", new Color(0.95f, 0.20f, 0.16f));
            yield return new WaitForSeconds(1.5f);

            // Spawn on the far side of the arena so the boss visibly stalks in.
            float groundY = GroundAlignUtil.FindGroundTopY();
            var spawn = new Vector3(MapBounds.MaxX - 0.3f, groundY + 0.5f, 0f);
            MinotaurBoss.Spawn(spawn);

            Destroy(gameObject, 0.4f);   // the challenge is claimed — remove the spent portal
        }

        IEnumerator Transition()
        {
            // 1.2 s sequence before the scene swap:
            //   • Player freezes in idle and grows a pulsing white halo.
            //   • Portal rings keep pulsing on their own Update loop.
            //   • In the final 0.30 s we fade the screen to black via SceneFader,
            //     so the scene swap itself happens off-screen and the new scene
            //     fades back in cleanly (no visible Unity scene-load flicker).
            // Returning to the city lingers noticeably longer than diving into the
            // dungeon, so the trip home reads as a slow, deliberate fade-out.
            // ── Co-op: GATHER the whole party at the portal first ──────────────────────────────
            // Everyone walks to the portal; the transition only starts once ALL have arrived (or a
            // 6 s safety timeout), so nobody gets left behind / warped mid-fight.
            var lobbyG = SteamLobbyManager.Instance;
            bool grouped = lobbyG != null && lobbyG.InLobby && lobbyG.MemberSteamIds.Count >= 2;
            if (grouped)
            {
                float portalX = transform.position.x;
                MultiplayerSync.Instance?.BroadcastGather(portalX);
                UnityEngine.Object.FindAnyObjectByType<PlayerController2D>()?.AutoWalkToX(portalX);
                float deadline = Time.time + 6f;
                while (Time.time < deadline && !MultiplayerSync.AllPartyNearX(portalX, 0.6f))
                    yield return null;
            }

            bool returningToCity = string.Equals(destinationScene, "Zulfarak", StringComparison.OrdinalIgnoreCase);
            float absorb = returningToCity ? 2.4f : 1.2f;

            var player = UnityEngine.Object.FindAnyObjectByType<PlayerController2D>();
            if (player != null) player.StartPortalAbsorb(absorb);

            // If we're the lobby leader, tell every follower to transit too.
            var lobby = SteamLobbyManager.Instance;
            if (lobby != null && lobby.InLobby && lobby.IsLeader)
                MultiplayerSync.Instance?.BroadcastPortalEnter(destinationScene);

            yield return new WaitForSeconds(absorb - 0.3f);
            SceneFader.FadeToBlack(0.3f);
            yield return new WaitForSeconds(0.3f);
            SceneManager.LoadScene(destinationScene);
        }

        public void Open()
        {
            _open = true;
            if (glowSprite) glowSprite.gameObject.SetActive(true);
            if (_rings != null)
                foreach (var r in _rings)
                    if (r) r.gameObject.SetActive(true);
        }

        // Programmatic entry point used by the HUD "return to city" button so the
        // click reuses the same fade/absorb/lobby-broadcast pipeline as walking into
        // the portal collider.
        public void ForceEnter()
        {
            if (!_open || _transitioning) return;
            var lobby = SteamLobbyManager.Instance;
            if (lobby != null && lobby.InLobby && !lobby.IsLeader) return;
            _transitioning = true;
            StartCoroutine(Transition());
        }

        // Finds a Portal2D in the currently loaded scenes that leads to Zulfarak and
        // triggers it. Returns false if none exists (caller then decides its fallback).
        public static bool TriggerReturnToCity()
        {
            var portals = UnityEngine.Object.FindObjectsByType<Portal2D>(FindObjectsSortMode.None);
            foreach (var p in portals)
            {
                if (p == null) continue;
                if (string.Equals(p.destinationScene, "Zulfarak", StringComparison.OrdinalIgnoreCase))
                {
                    p.ForceEnter();
                    return true;
                }
            }
            return false;
        }
    }
}
