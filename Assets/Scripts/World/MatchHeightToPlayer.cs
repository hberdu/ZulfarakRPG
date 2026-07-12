using System.Collections;
using UnityEngine;

namespace ZulfarakRPG
{
    // Scales an NPC at spawn so its VISIBLE (alpha-trimmed) height equals the player's — different
    // character sprites fill their 100px frame by different amounts, so a shared 2× scale made
    // some NPCs (Wizard/Soldier) read bigger than an Archer hero. Re-grounds after scaling so the
    // feet stay on the floor. Waits one frame so class-swapped sprites (ClassMaster) are set first.
    [RequireComponent(typeof(SpriteRenderer))]
    public class MatchHeightToPlayer : MonoBehaviour
    {
        public float ratio = 1f;                 // NPC visible height = ratio × player's
        const float FeetOffset = 0.40f;          // visible feet in sprite-local units (wizard FEET_OFFSET)

        IEnumerator Start()
        {
            yield return null;                   // let other Start() set final sprites
            var sr = GetComponent<SpriteRenderer>();
            var player = FindAnyObjectByType<PlayerController2D>();
            if (sr == null || sr.sprite == null || player == null) yield break;
            var psr = player.GetComponent<SpriteRenderer>();
            if (psr == null || psr.sprite == null) yield break;

            var pab = SpriteAlphaBounds.Get(psr.sprite);
            float playerVisH = (pab.topFromBottom - pab.bottomFromBottom) * Mathf.Abs(player.transform.lossyScale.y);
            var nab = SpriteAlphaBounds.Get(sr.sprite);
            float perUnit = nab.topFromBottom - nab.bottomFromBottom;           // NPC visible height per unit scale
            if (perUnit <= 0.0001f || playerVisH <= 0.0001f) yield break;

            float feetY = transform.position.y + FeetOffset * transform.localScale.y;  // current grounded feet
            float s = playerVisH * ratio / perUnit;
            transform.localScale = new Vector3(s, s, transform.localScale.z);
            transform.position   = new Vector3(transform.position.x, feetY - FeetOffset * s, transform.position.z);
        }
    }
}
