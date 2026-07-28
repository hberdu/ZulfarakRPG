using System.Collections.Generic;
using UnityEngine;

namespace ZulfarakRPG
{
    // Pixel-art class emblems (wizard hat / bow / sword) used by the party frame instead of a
    // cropped character portrait: the row identifies the member's CLASS, not their sprite.
    //
    // Authored as 16×16 bitmaps and rendered Point-filtered at 1 texel = 1 UI pixel, so they stay
    // crisp — a scaled-down character crop was the blurry part of the old frame.
    public static class ClassEmblem
    {
        const int N = 16;

        static readonly string[] Mage =
        {
            "................",
            ".......#........",
            "......###.......",
            "......###.......",
            ".....#####......",
            ".....#####......",
            "....#######.....",
            "....#######.....",
            "...#########....",
            "...#########....",
            "..###########...",
            "..###########...",
            ".#############..",
            "###############.",
            "###############.",
            "................",
        };

        static readonly string[] Archer =
        {
            "................",
            "..###...........",
            ".##.##..........",
            ".#...##.........",
            ".#....##........",
            ".#.....#........",
            ".#.....#..##....",
            ".#..##########..",
            ".#.....#..##....",
            ".#.....#........",
            ".#....##........",
            ".#...##.........",
            ".##.##..........",
            "..###...........",
            "................",
            "................",
        };

        static readonly string[] Warrior =
        {
            ".......#........",
            "......###.......",
            "......###.......",
            "......###.......",
            "......###.......",
            "......###.......",
            "......###.......",
            "......###.......",
            "......###.......",
            "...#########....",
            "......###.......",
            "......###.......",
            "......###.......",
            ".....#####......",
            "................",
            "................",
        };

        static readonly Dictionary<ClassType, Sprite> _cache = new Dictionary<ClassType, Sprite>();

        public static Sprite For(ClassType cls)
        {
            if (_cache.TryGetValue(cls, out var cached) && cached != null) return cached;
            var rows = cls switch
            {
                ClassType.Mage   => Mage,
                ClassType.Archer => Archer,
                _                => Warrior,
            };
            var sp = Build(rows);
            _cache[cls] = sp;
            return sp;
        }

        // Lit pixels in warm parchment ink with a baked 1 px black outline, so the emblem reads on
        // both the dark row background and the gold leader frame.
        static Sprite Build(string[] rows)
        {
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var px  = new Color32[N * N];
            var ink = new Color32(255, 240, 205, 255);

            for (int row = 0; row < N && row < rows.Length; row++)
                for (int x = 0; x < N && x < rows[row].Length; x++)
                    if (rows[row][x] == '#')
                        px[(N - 1 - row) * N + x] = ink;   // row 0 = top; texture Y grows up

            var outlined = (Color32[])px.Clone();
            var edge = new Color32(0, 0, 0, 210);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    if (px[y * N + x].a != 0) continue;
                    bool near = false;
                    for (int dy = -1; dy <= 1 && !near; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= N || ny >= N) continue;
                            if (px[ny * N + nx].a != 0) { near = true; break; }
                        }
                    if (near) outlined[y * N + x] = edge;
                }

            tex.SetPixels32(outlined);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 16f);
        }
    }
}
