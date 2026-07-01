using System.Collections.Generic;
using UnityEngine;

namespace SlideAndMatch
{
    /// <summary>
    /// Static lookup for tile background and text colours.
    /// Hand-tuned dark-mode palette — no instance needed.
    /// </summary>
    public static class TileStyleLookup
    {
        private struct TileStyle
        {
            public Color tileColor;
            public Color textColor;
        }

        private static readonly Dictionary<int, TileStyle> styles;
        private static readonly TileStyle defaultStyle;

        static TileStyleLookup()
        {
            styles = new Dictionary<int, TileStyle>();

            AddStyle(2,    "#1e293b", "#f8fafc");
            AddStyle(4,    "#334155", "#f8fafc");
            AddStyle(8,    "#f97316", "#ffffff");
            AddStyle(16,   "#ea580c", "#ffffff");
            AddStyle(32,   "#ef4444", "#ffffff");
            AddStyle(64,   "#dc2626", "#ffffff");
            AddStyle(128,  "#eab308", "#1e1b18");
            AddStyle(256,  "#ca8a04", "#1e1b18");
            AddStyle(512,  "#22c55e", "#ffffff");
            AddStyle(1024, "#16a34a", "#ffffff");
            AddStyle(2048, "#8b5cf6", "#ffffff");
            AddStyle(4096, "#6d28d9", "#ffffff");

            ColorUtility.TryParseHtmlString("#6d28d9", out Color defTile);
            ColorUtility.TryParseHtmlString("#ffffff", out Color defText);
            defaultStyle = new TileStyle { tileColor = defTile, textColor = defText };
        }

        private static void AddStyle(int value, string tileHex, string textHex)
        {
            ColorUtility.TryParseHtmlString(tileHex, out Color tileColor);
            ColorUtility.TryParseHtmlString(textHex, out Color textColor);
            styles[value] = new TileStyle { tileColor = tileColor, textColor = textColor };
        }

        public static Color GetTileColor(int value)
        {
            return styles.TryGetValue(value, out var s) ? s.tileColor : defaultStyle.tileColor;
        }

        public static Color GetTextColor(int value)
        {
            return styles.TryGetValue(value, out var s) ? s.textColor : defaultStyle.textColor;
        }
    }
}
