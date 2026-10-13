using UnityEngine;

namespace LiANN.MahjongTones
{
    /// <summary>
    /// Bakes the Pinyin Tiles porcelain tile (spec D8) into textures once at start-up:
    /// 145° highlight→face→shade gradient, white radial highlight at 28% 18%, 1px rim, 2px bottom edge,
    /// and the coral corner wedge for word tiles. Built in code so the package needs no image assets.
    /// </summary>
    public static class TileArt
    {
        public const int W = 200, H = 210, Radius = 20;
        static Texture2D _word, _meaning, _shadow;

        public static Texture2D WordFace => _word ? _word : (_word = Bake(true));
        public static Texture2D MeaningFace => _meaning ? _meaning : (_meaning = Bake(false));
        public static Texture2D Shadow => _shadow ? _shadow : (_shadow = BakeShadow());

        static float RoundRectDistance(float px, float py, float w, float h, float r)
        {
            // Signed distance to a rounded rectangle spanning [0,w]×[0,h]; negative inside.
            float qx = Mathf.Abs(px - w * 0.5f) - (w * 0.5f - r);
            float qy = Mathf.Abs(py - h * 0.5f) - (h * 0.5f - r);
            float outside = new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0) - r;
        }

        static Texture2D Bake(bool wedge)
        {
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = wedge ? "LiANN word tile" : "LiANN meaning tile" };
            var px = new Color32[W * H];
            var dir = new Vector2(Mathf.Sin(145 * Mathf.Deg2Rad), Mathf.Cos(145 * Mathf.Deg2Rad) * -1f); // CSS 145deg, y down
            float span = Mathf.Abs(dir.x) * W + Mathf.Abs(dir.y) * H;
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float fx = x + 0.5f, fy = y + 0.5f;           // fy measured from the top
                float d = RoundRectDistance(fx, fy, W, H, Radius);
                float coverage = Mathf.Clamp01(0.5f - d);
                if (coverage <= 0) { px[(H - 1 - y) * W + x] = new Color32(0, 0, 0, 0); continue; }

                float t = Mathf.Clamp01(((fx - W * 0.5f) * dir.x + (fy - H * 0.5f) * dir.y) / span + 0.5f);
                Color c = t < 0.46f
                    ? Color.Lerp(Theme.PorcelainHighlight, Theme.PorcelainFace, t / 0.46f)
                    : Color.Lerp(Theme.PorcelainFace, Theme.PorcelainShade, (t - 0.46f) / 0.54f);

                float hx = (fx - W * 0.28f) / W, hy = (fy - H * 0.18f) / H;
                float glow = Mathf.Clamp01(1f - Mathf.Sqrt(hx * hx + hy * hy) / 0.22f);
                c = Color.Lerp(c, Color.white, 0.72f * glow * glow);

                if (wedge && fx + fy < W * 0.32f) c = Theme.TileCoral;           // top-left triangle, 32%

                if (fy > H - 3f) c = Theme.PorcelainEdge;                                                   // 2px inset bottom edge
                else if (fy < 2f) c = Color.Lerp(c, Theme.PorcelainHighlight, 0.8f);                       // inset top highlight
                if (d > -1.5f) c = Theme.PorcelainRim;                                                       // 1px rim

                c.a = coverage;
                px[(H - 1 - y) * W + x] = c;
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        static Texture2D BakeShadow()
        {
            const int pad = 24, w = W + pad * 2, h = H + pad * 2;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "LiANN tile shadow" };
            var px = new Color32[w * h];
            var ink = new Color(43 / 255f, 43 / 255f, 40 / 255f);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float d = RoundRectDistance(x - pad + 0.5f, y - pad + 0.5f, W, H, Radius);
                float a = 0.42f * Mathf.Exp(-Mathf.Max(0, d + 4) / 7f);   // soft contact + ambient shadow
                var c = ink; c.a = Mathf.Clamp01(a);
                px[(h - 1 - y) * w + x] = c;
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }
    }
}
