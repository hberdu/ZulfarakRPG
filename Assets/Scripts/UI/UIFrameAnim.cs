using UnityEngine;
using UnityEngine.UI;

namespace ZulfarakRPG
{
    // Cycles a UI Image through a set of frames at a fixed fps — used for the campfire flicker on
    // the hero-select screen (and any small looping UI sprite animation).
    [RequireComponent(typeof(Image))]
    public class UIFrameAnim : MonoBehaviour
    {
        public Sprite[] frames;
        public float    fps = 8f;

        Image _img;
        float _t;
        int   _i;

        void Awake() => _img = GetComponent<Image>();

        void Update()
        {
            if (frames == null || frames.Length == 0 || _img == null) return;
            _t += Time.deltaTime;
            if (_t < 1f / Mathf.Max(1f, fps)) return;
            _t = 0f;
            _i = (_i + 1) % frames.Length;
            _img.sprite = frames[_i];
        }
    }
}
