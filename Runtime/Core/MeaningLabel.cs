using System;

namespace LiANN.MahjongTones.Core
{
    /// <summary>
    /// Font size for a meaning tile's English label, as a fraction of the tile width. Capped so the longest
    /// word fits on one line: UI Toolkit otherwise breaks inside the word ("supermark / et").
    /// </summary>
    public static class MeaningLabel
    {
        /// <summary>Text width available: tile width minus 6 % padding each side.</summary>
        public const float Available = 0.88f;

        /// <summary>
        /// Rendered width over the font's advance widths. Measured on DSJ-PC: "supermarket" at 17.2 px is
        /// 117 px in Unity against 103.6 px from the advances (×1.13); 1.18 leaves a margin.
        /// </summary>
        public const float RenderFactor = 1.18f;

        // Nunito ExtraBold advance widths in em (hmtx / unitsPerEm), lower case a–z then upper case A–Z.
        static readonly float[] Lower =
        {
            0.557f, 0.610f, 0.478f, 0.610f, 0.549f, 0.382f, 0.615f, 0.595f, 0.268f, 0.273f, 0.558f, 0.333f, 0.889f,
            0.595f, 0.589f, 0.610f, 0.610f, 0.412f, 0.492f, 0.404f, 0.590f, 0.534f, 0.861f, 0.559f, 0.533f, 0.480f,
        };
        static readonly float[] Upper =
        {
            0.753f, 0.695f, 0.684f, 0.774f, 0.605f, 0.570f, 0.742f, 0.780f, 0.297f, 0.372f, 0.688f, 0.573f, 0.876f,
            0.753f, 0.796f, 0.664f, 0.796f, 0.696f, 0.641f, 0.632f, 0.743f, 0.727f, 1.120f, 0.685f, 0.631f, 0.615f,
        };

        public static float CharEm(char c)
        {
            if (c >= 'a' && c <= 'z') return Lower[c - 'a'];
            if (c >= 'A' && c <= 'Z') return Upper[c - 'A'];
            switch (c)
            {
                case '-': return 0.44f;
                case '\'': return 0.256f;
                case '?': return 0.468f;
                case ',': case '.': return 0.26f;
                case '(': case ')': return 0.382f;
                case '/': return 0.331f;
                default: return 0.65f;   // digits and anything unexpected: a little wider than average
            }
        }

        /// <summary>Width in em of the widest space-separated word (hyphenated words count as one).</summary>
        public static float LongestWordEm(string english)
        {
            float best = 0;
            foreach (var part in (english ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                float w = 0;
                foreach (char c in part) w += CharEm(c);
                best = Math.Max(best, w);
            }
            return best;
        }

        public static float FontScale(string english)
        {
            int len = (english ?? "").Length;
            float scale = len <= 6 ? 0.18f : len <= 12 ? 0.15f : 0.12f;
            float longest = LongestWordEm(english);
            if (longest > 0) scale = Math.Min(scale, Available / (RenderFactor * longest));
            return scale;
        }
    }
}
