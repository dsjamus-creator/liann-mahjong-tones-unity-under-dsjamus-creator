using LiANN.MahjongTones.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace LiANN.MahjongTones
{
    /// <summary>One porcelain tile: word tile (hanzi + toneless pinyin, coral wedge) or meaning tile (English, coral underline).</summary>
    public sealed class TileView : VisualElement
    {
        public readonly int SlotId;
        readonly VisualElement _shadow, _face, _ring, _underline;
        readonly Label _main, _sub;
        public float Size { get; private set; }

        public TileView(int slotId)
        {
            SlotId = slotId;
            style.position = Position.Absolute;
            pickingMode = PickingMode.Position;

            _shadow = new VisualElement { pickingMode = PickingMode.Ignore };
            _shadow.style.position = Position.Absolute;
            _shadow.style.backgroundImage = TileArt.Shadow;
            Add(_shadow);

            _face = new VisualElement { pickingMode = PickingMode.Ignore };
            _face.style.position = Position.Absolute;
            _face.style.left = 0; _face.style.top = 0; _face.style.right = 0; _face.style.bottom = 0;
            _face.style.alignItems = Align.Center;
            _face.style.justifyContent = Justify.Center;
            Add(_face);

            _main = new Label { pickingMode = PickingMode.Ignore };
            _sub = new Label { pickingMode = PickingMode.Ignore };
            foreach (var l in new[] { _main, _sub })
            {
                l.style.color = Theme.Glyph;
                l.style.unityTextAlign = TextAnchor.MiddleCenter;
                l.style.whiteSpace = WhiteSpace.Normal;
                l.style.marginTop = 0; l.style.marginBottom = 0; l.style.paddingTop = 0; l.style.paddingBottom = 0;
                _face.Add(l);
            }

            _underline = new VisualElement { pickingMode = PickingMode.Ignore };
            _underline.style.position = Position.Absolute;
            _underline.style.left = new Length(24, LengthUnit.Percent);
            _underline.style.right = new Length(24, LengthUnit.Percent);
            _underline.style.height = 3;
            _underline.style.backgroundColor = Theme.TileCoral;
            SetRadius(_underline, 2);
            Add(_underline);

            _ring = new VisualElement { pickingMode = PickingMode.Ignore };
            _ring.style.position = Position.Absolute;
            _ring.style.left = -3; _ring.style.top = -3; _ring.style.right = -3; _ring.style.bottom = -3;
            SetRadius(_ring, 13);
            _ring.style.display = DisplayStyle.None;
            Add(_ring);
        }

        static void SetRadius(VisualElement e, float r)
        {
            e.style.borderTopLeftRadius = r; e.style.borderTopRightRadius = r;
            e.style.borderBottomLeftRadius = r; e.style.borderBottomRightRadius = r;
        }

        static void SetBorder(VisualElement e, float width, Color color)
        {
            e.style.borderTopWidth = width; e.style.borderBottomWidth = width; e.style.borderLeftWidth = width; e.style.borderRightWidth = width;
            e.style.borderTopColor = color; e.style.borderBottomColor = color; e.style.borderLeftColor = color; e.style.borderRightColor = color;
        }

        public void Show(Word word, TileKind kind, bool showPinyin, Font hanziFont, Font uiFont)
        {
            bool isWord = kind == TileKind.WordTile;
            _face.style.backgroundImage = isWord ? TileArt.WordFace : TileArt.MeaningFace;
            _underline.style.display = isWord ? DisplayStyle.None : DisplayStyle.Flex;
            if (isWord)
            {
                _main.text = word.Hanzi;
                _main.style.unityFontDefinition = FontDefinition.FromFont(hanziFont);
                _sub.text = word.Toneless;
                _sub.style.unityFontDefinition = FontDefinition.FromFont(uiFont);
                _sub.style.display = showPinyin ? DisplayStyle.Flex : DisplayStyle.None;
            }
            else
            {
                _main.text = word.English;
                _main.style.unityFontDefinition = FontDefinition.FromFont(uiFont);
                _sub.text = "";
                _sub.style.display = DisplayStyle.None;
            }
            _main.style.unityFontStyleAndWeight = FontStyle.Bold;
            _sub.style.unityFontStyleAndWeight = FontStyle.Bold;
            _sub.style.opacity = 0.75f;
            ApplySize(word, kind);
        }

        Word _word; TileKind _kind;

        void ApplySize(Word word, TileKind kind) { _word = word; _kind = kind; Resize(Size); }

        public void Resize(float size)
        {
            Size = size;
            if (size <= 0) return;
            float h = size * TileArt.H / TileArt.W;
            style.width = size; style.height = h;
            _shadow.style.left = -size * 0.12f; _shadow.style.top = -size * 0.08f;
            _shadow.style.width = size * 1.24f; _shadow.style.height = h * 1.24f;
            _underline.style.bottom = size * 0.05f;
            if (_word == null) return;
            if (_kind == TileKind.WordTile)
            {
                int n = _word.Hanzi.Length;
                _main.style.fontSize = size * (n <= 2 ? 0.34f : n == 3 ? 0.26f : 0.2f);
                _sub.style.fontSize = size * (_word.Toneless.Length > 9 ? 0.11f : 0.14f);
            }
            else
            {
                int len = _word.English.Length;
                _main.style.fontSize = size * (len <= 6 ? 0.18f : len <= 12 ? 0.15f : 0.12f);
                _main.style.paddingLeft = size * 0.06f; _main.style.paddingRight = size * 0.06f;
            }
        }

        public void SetSelected(bool on)
        {
            _ring.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            SetBorder(_ring, 3, Theme.Ink);
            _ring.style.backgroundColor = Color.clear;
        }

        public void SetHint(bool on, float pulse = 1)
        {
            _ring.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            var c = Theme.Coral; c.a = Mathf.Lerp(0.35f, 1f, pulse);
            SetBorder(_ring, 4, c);
        }

        public void SetDimmed(bool on) => _face.style.opacity = on ? 0.55f : 1f;
    }
}
