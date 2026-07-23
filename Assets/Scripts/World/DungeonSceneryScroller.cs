using System.Collections.Generic;
using UnityEngine;

namespace ZulfarakRPG
{
    // Scrolls the (otherwise static) dungeon scenery sideways during the inter-wave march so the
    // hero reads as actually TRAVELLING instead of running on the spot. The scenery is the same
    // city-style scatter MapScenery builds; this just slides each piece left at a per-layer speed
    // (near = fast, far = slow → parallax) and wraps it around the play width so the field never
    // empties during the march. Cities never get one of these, so their scenery stays put.
    public class DungeonSceneryScroller : MonoBehaviour
    {
        public static DungeonSceneryScroller Active;

        struct Item { public Transform t; public float speed; }
        readonly List<Item> _items = new List<Item>();
        float _leftEdge, _span;

        void Awake() { Active = this; }
        void OnDestroy() { if (Active == this) Active = null; }

        public void Configure(float minX, float maxX)
        {
            _leftEdge = minX - 1f;
            _span     = (maxX - minX) + 2f;   // wrap distance: left edge → just past the right edge
        }

        public void Register(Transform t, float speed) => _items.Add(new Item { t = t, speed = speed });

        // Called each frame of WaveManager.RunToNextWave with the hero's march delta.
        public void Scroll(float dx)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                var t = _items[i].t;
                if (t == null) continue;
                var p = t.position;
                p.x -= dx * _items[i].speed;
                if (p.x < _leftEdge) p.x += _span;   // recycle to the right so the field never drains
                t.position = p;
            }
        }
    }
}
