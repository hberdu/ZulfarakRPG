using TMPro;
using UnityEngine;

namespace ZulfarakRPG
{
    // Small floating name tag rendered above a character's head. Used for
    // identifying named NPCs (Mestre, Ferreiro, Kael) and remote players in
    // the world — uses the same tiny-bold style as WorldHealthBar.SetName so
    // every character label reads consistently.
    public class NameTag : MonoBehaviour
    {
        TextMeshPro _tmp;

        public static NameTag Attach(SpriteRenderer anchor, string label, float yOffsetWorld = 0.55f)
        {
            if (anchor == null || string.IsNullOrEmpty(label)) return null;
            var go = new GameObject("NameTag");
            go.transform.SetParent(anchor.transform, false);
            // Anchor offset is in the PARENT's local space; convert the requested
            // world-Y offset so the label sits at the same world height regardless
            // of the character's transform scale.
            float scaleY = anchor.transform.lossyScale.y;
            float localY = scaleY > 1e-4f ? yOffsetWorld / scaleY : yOffsetWorld;
            go.transform.localPosition = new Vector3(0f, localY, -0.1f);

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text                  = label;
            tmp.fontSize              = 0.32f;
            tmp.alignment             = TextAlignmentOptions.Center;
            tmp.color                 = new Color(0.96f, 0.96f, 0.86f, 1f);
            tmp.fontStyle             = FontStyles.Bold;
            tmp.enableWordWrapping    = false;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 10;

            var tag = go.AddComponent<NameTag>();
            tag._tmp = tmp;
            return tag;
        }

        public void SetLabel(string label)
        {
            if (_tmp == null) return;
            if (string.IsNullOrEmpty(label)) { _tmp.gameObject.SetActive(false); return; }
            _tmp.text = label;
            _tmp.gameObject.SetActive(true);
        }
    }
}
