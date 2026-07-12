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

        public static void Play(string targetScene)
        {
            if (_running || string.IsNullOrEmpty(targetScene)) return;
            _running = true;
            var go = new GameObject("HorseCutscene");
            go.AddComponent<HorseCutscene>()._scene = targetScene;
        }

        void Start() => StartCoroutine(Run());

        IEnumerator Run()
        {
            var frames = LoadHorseFrames();
            if (frames == null || frames.Length == 0)   // no art → just fade + load
            {
                SceneFader.FadeToBlack(0.4f, () => SceneManager.LoadScene(_scene));
                _running = false;
                yield break;
            }

            var player = FindAnyObjectByType<PlayerController2D>();
            Vector3 ppos = player != null ? player.transform.position : new Vector3(1f, -0.35f, 0f);

            var cam = Camera.main;
            float halfW = cam != null ? cam.orthographicSize * cam.aspect : 4f;
            float camX  = cam != null ? cam.transform.position.x : ppos.x;
            float rightX = camX + halfW + 1.5f;            // start just off the right edge
            bool  goLeft = ppos.x < rightX;                // gallop toward the hero (usually left)

            var horse = new GameObject("CutsceneHorse");
            var sr = horse.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 250;
            sr.sprite  = frames[0];
            sr.flipX   = goLeft;                            // art faces RIGHT; flip to face left when galloping left
            horse.transform.position   = new Vector3(rightX, ppos.y, 0f);
            horse.transform.localScale = Vector3.one * 4.5f;

            float stopX = ppos.x + (goLeft ? 0.95f : -0.95f);
            const float speed = 7.5f;
            float animT = 0f; int fi = 0; float bobT = 0f;
            while (Mathf.Abs(horse.transform.position.x - stopX) > 0.12f)
            {
                float dir = Mathf.Sign(stopX - horse.transform.position.x);
                bobT += Time.deltaTime * 14f;
                var p = horse.transform.position;
                p.x += dir * speed * Time.deltaTime;
                p.y  = ppos.y + Mathf.Abs(Mathf.Sin(bobT)) * 0.08f;   // gallop bob
                horse.transform.position = p;

                animT += Time.deltaTime;
                if (animT >= 0.07f) { animT = 0f; fi = (fi + 1) % frames.Length; sr.sprite = frames[fi]; }
                yield return null;
            }

            // Arrived at the hero — dust puff, fade to black, load the settlement.
            PortalSmoke.BurstAt(new Vector3(stopX, ppos.y + 0.1f, 0f), 8);
            _running = false;
            SceneFader.FadeToBlack(0.4f, () => SceneManager.LoadScene(_scene));
        }

        static Sprite[] LoadHorseFrames()
        {
            var tex = Resources.Load<Texture2D>("horse_gallop");
            if (tex == null) return null;
            const int fw = 43, fh = 21;
            int n = Mathf.Max(1, tex.width / fw);
            var arr = new Sprite[n];
            for (int i = 0; i < n; i++)
                arr[i] = Sprite.Create(tex, new Rect(i * fw, 0, fw, fh), new Vector2(0.5f, 0f), 100f);
            return arr;
        }
    }
}
