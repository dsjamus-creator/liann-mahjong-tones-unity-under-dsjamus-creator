using UnityEngine;

namespace LiANN.MahjongTones
{
    /// <summary>
    /// Pinyin Tiles "liann" design system (spec D8, D11). The porcelain tile keeps one material in both themes;
    /// the page around it switches between light and dark.
    /// </summary>
    public static class Theme
    {
        static Color Hex(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

        // Porcelain tile (same in light and dark)
        public static readonly Color PorcelainHighlight = Hex("#FFF7DF");
        public static readonly Color PorcelainFace = Hex("#FDEFCB");
        public static readonly Color PorcelainShade = Hex("#F4E2B8");
        public static readonly Color PorcelainRim = Hex("#CFB996");
        public static readonly Color PorcelainEdge = Hex("#947641");
        public static readonly Color PorcelainEdgeDeep = Hex("#6E5730");
        public static readonly Color Glyph = Hex("#3D3C34");
        public static readonly Color TileCoral = Hex("#C24837");

        public static bool Dark;

        public static Color Ground => Dark ? Hex("#1B1C18") : Hex("#FBF6EE");
        public static Color Card => Dark ? Hex("#292B24") : Hex("#FFFDF9");
        public static Color Ink => Dark ? Hex("#F5EFE5") : Hex("#2B2B28");
        public static Color Muted => Dark ? Hex("#B9BDAF") : Hex("#66705F");
        public static Color Hairline => Dark ? Hex("#34392E") : Hex("#DCE4D7");
        public static Color BorderStrong => Dark ? Hex("#718267") : Hex("#8A9B7E");
        public static Color BoardFrame => Dark ? Hex("#718267") : Hex("#5F7457");
        public static Color Coral => Dark ? Hex("#EE7A67") : Hex("#C24837");
        public static Color OnCoral => Hex("#24120E");
        public static Color Blush => Dark ? new Color(0.886f, 0.376f, 0.306f, 0.2f) : Hex("#F6D9D3");
        public static Color Good => Dark ? Hex("#8FD3AE") : Hex("#2F6B4F");
    }
}
