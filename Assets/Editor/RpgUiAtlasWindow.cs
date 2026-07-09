#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ZulfarakRPG.EditorTools
{
    // Editor window for tuning RpgUiSprites rects visually. Loads the atlas, lets you
    // pick an X/Y/W/H rect and previews the crop side-by-side with the full atlas.
    // Use this whenever a HUD element looks off — read the rect, adjust the numbers,
    // and paste the corrected values back into RpgUiSprites.cs.
    //
    // Open with: Zulfarak → RPG UI Atlas Inspector
    public class RpgUiAtlasWindow : EditorWindow
    {
        Texture2D _atlas;
        int _x = 12, _y = 24, _w = 26, _h = 32;
        Vector2 _scroll;

        // Bookmarks (name → rect) so tuning several rects doesn't lose the previous one.
        readonly List<(string name, Rect rect)> _bookmarks = new List<(string, Rect)>();
        string _newBookmarkName = "sprite_name";

        [MenuItem("Zulfarak/RPG UI Atlas Inspector")]
        public static void Open() => GetWindow<RpgUiAtlasWindow>("RPG UI Atlas");

        void OnEnable() => ReloadAtlas();

        void ReloadAtlas()
        {
            _atlas = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/rpg_ui.png");
            if (_atlas == null)
                Debug.LogWarning("[RpgUiAtlasWindow] Não achei Assets/Resources/rpg_ui.png. Copie o Ui.png do pack para lá.");
        }

        void OnGUI()
        {
            if (_atlas == null)
            {
                EditorGUILayout.HelpBox("Assets/Resources/rpg_ui.png não encontrado.", MessageType.Warning);
                if (GUILayout.Button("Recarregar")) ReloadAtlas();
                return;
            }

            EditorGUILayout.LabelField("Atlas", $"{_atlas.name} ({_atlas.width} × {_atlas.height})");
            EditorGUILayout.Space(6);

            EditorGUILayout.LabelField("Rect (top-left origin, pixels)", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            _x = EditorGUILayout.IntField("x", _x);
            _y = EditorGUILayout.IntField("y", _y);
            _w = EditorGUILayout.IntField("w", _w);
            _h = EditorGUILayout.IntField("h", _h);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("←")) _x--;
            if (GUILayout.Button("→")) _x++;
            if (GUILayout.Button("↑")) _y--;
            if (GUILayout.Button("↓")) _y++;
            if (GUILayout.Button("W-")) _w = Mathf.Max(1, _w - 1);
            if (GUILayout.Button("W+")) _w++;
            if (GUILayout.Button("H-")) _h = Mathf.Max(1, _h - 1);
            if (GUILayout.Button("H+")) _h++;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Preview (crop @ 4×)", EditorStyles.boldLabel);
            DrawCrop(_x, _y, _w, _h, 4f);

            EditorGUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            _newBookmarkName = EditorGUILayout.TextField("Name", _newBookmarkName);
            if (GUILayout.Button("Bookmark", GUILayout.Width(90)))
                _bookmarks.Add((_newBookmarkName, new Rect(_x, _y, _w, _h)));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Bookmarks", EditorStyles.boldLabel);
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(120));
            for (int i = 0; i < _bookmarks.Count; i++)
            {
                var (name, rect) = _bookmarks[i];
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"{name}", $"({rect.x},{rect.y},{rect.width},{rect.height})");
                if (GUILayout.Button("Load", GUILayout.Width(60)))
                {
                    _x = (int)rect.x; _y = (int)rect.y; _w = (int)rect.width; _h = (int)rect.height;
                }
                if (GUILayout.Button("×", GUILayout.Width(30)))
                {
                    _bookmarks.RemoveAt(i);
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            if (_bookmarks.Count > 0 && GUILayout.Button("Copy all as C#"))
            {
                var sb = new StringBuilder();
                foreach (var (name, rect) in _bookmarks)
                    sb.AppendLine($"public static Sprite {name}() => Slice(\"{name}\", {(int)rect.x}, {(int)rect.y}, {(int)rect.width}, {(int)rect.height});");
                EditorGUIUtility.systemCopyBuffer = sb.ToString();
                Debug.Log("[RpgUiAtlasWindow] Sprite accessors copiados para a área de transferência.");
            }
        }

        void DrawCrop(int x, int y, int w, int h, float zoom)
        {
            if (_atlas == null || w <= 0 || h <= 0) return;
            Rect atlasRect = new Rect(
                x / (float)_atlas.width,
                1f - (y + h) / (float)_atlas.height,
                w  / (float)_atlas.width,
                h  / (float)_atlas.height);
            Rect drawRect = GUILayoutUtility.GetRect(w * zoom, h * zoom, GUILayout.ExpandWidth(false));
            GUI.DrawTextureWithTexCoords(drawRect, _atlas, atlasRect);
        }
    }
}
#endif
