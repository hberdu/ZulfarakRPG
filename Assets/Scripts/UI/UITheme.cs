using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ZulfarakRPG
{
    // Central color/style definitions for the Zulfarak desert theme.
    // Reference UITheme.Colors.* anywhere you need a color.
    public static class UITheme
    {
        public static class Colors
        {
            // Palette — inspired by Qatar desert / sand / gold
            public static readonly Color Background     = Hex("110A03");
            public static readonly Color PanelDark      = Hex("1C1006CC");
            public static readonly Color PanelMid       = Hex("2E1C08CC");
            public static readonly Color PanelLight     = Hex("43280ACC");
            public static readonly Color Gold           = Hex("C8922A");
            public static readonly Color GoldBright     = Hex("FFD56B");
            public static readonly Color Sand           = Hex("B88B4A");
            public static readonly Color SandLight      = Hex("E8C97A");
            public static readonly Color TextPrimary    = Hex("F0DEB0");
            public static readonly Color TextSecondary  = Hex("A08050");
            public static readonly Color TextDim        = Hex("6A5030");
            public static readonly Color AccentTeal     = Hex("2AB8B0");   // health/heal
            public static readonly Color AccentRed      = Hex("C83030");   // damage / warning
            public static readonly Color AccentPurple   = Hex("7840C8");   // magic
            public static readonly Color Online         = Hex("40C860");
            public static readonly Color Offline        = Hex("C84040");

            // Rarity
            public static readonly Color Common    = Hex("CCCCCC");
            public static readonly Color Uncommon  = Hex("4DCC4D");
            public static readonly Color Rare      = Hex("4080FF");
            public static readonly Color Epic      = Hex("A633FF");
            public static readonly Color Legendary = Hex("FF9900");
        }

        public static class FontSizes
        {
            public const int Title     = 32;
            public const int Header    = 22;
            public const int Body      = 15;
            public const int Small     = 12;
            public const int Tiny      = 10;
        }

        // Applies the full Zulfarak style to a Button
        public static void StyleButton(Button btn, ButtonVariant variant = ButtonVariant.Primary)
        {
            var img = btn.GetComponent<Image>();
            if (img == null) return;

            Color baseColor = variant switch
            {
                ButtonVariant.Primary  => Colors.Gold,
                ButtonVariant.Secondary => Colors.PanelMid,
                ButtonVariant.Danger   => Colors.AccentRed,
                ButtonVariant.Success  => Colors.Online,
                _ => Colors.Gold
            };

            img.color = baseColor;
            var c = btn.colors;
            c.normalColor      = baseColor;
            c.highlightedColor = baseColor * 1.25f;
            c.pressedColor     = baseColor * 0.70f;
            c.selectedColor    = baseColor;
            c.fadeDuration     = 0.08f;
            btn.colors = c;

            var tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.color     = variant == ButtonVariant.Primary ? Colors.Background : Colors.TextPrimary;
                tmp.fontStyle = FontStyles.Bold;
            }
        }

        // Applies panel background style
        public static void StylePanel(Image img, PanelVariant variant = PanelVariant.Dark)
        {
            img.color = variant switch
            {
                PanelVariant.Dark  => Colors.PanelDark,
                PanelVariant.Mid   => Colors.PanelMid,
                PanelVariant.Light => Colors.PanelLight,
                _ => Colors.PanelDark
            };
        }

        private static Color Hex(string hex)
        {
            if (hex.Length == 6)  hex += "FF";
            if (hex.Length == 8)
            {
                byte r = System.Convert.ToByte(hex[0..2], 16);
                byte g = System.Convert.ToByte(hex[2..4], 16);
                byte b = System.Convert.ToByte(hex[4..6], 16);
                byte a = System.Convert.ToByte(hex[6..8], 16);
                return new Color32(r, g, b, a);
            }
            return Color.magenta;
        }
    }

    public enum ButtonVariant  { Primary, Secondary, Danger, Success }
    public enum PanelVariant   { Dark, Mid, Light }
}
