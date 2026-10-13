"""Pictures on meaning tiles (spec D13): concrete words only, Lucide line icons (ISC; see lucide/LICENSE).
Reads picture-map.json (readingKey -> Lucide icon name), sets each word's "picture" in words.json, and renders
each icon to Runtime/Resources/LiANNMahjongTones/Pictures/<icon>.png in the tile glyph colour.
Words without a fitting icon keep "picture": "" (no stand-ins). Run after build_words.py and add_sense_ids.py.
Usage: pip install cairosvg; python3 -I Tools~/data/apply_pictures.py <package root>"""
import json, os, sys
import cairosvg

root = sys.argv[1]
here = os.path.join(root, "Tools~", "data")
res = os.path.join(root, "Runtime", "Resources", "LiANNMahjongTones")
out = os.path.join(res, "Pictures")
GLYPH, SIZE = "#3D3C34", 128  # Theme.Glyph; 128 px covers the largest tile at 2x

mapping = json.load(open(os.path.join(here, "picture-map.json"), encoding="utf-8"))
path = os.path.join(res, "words.json")
data = json.load(open(path, encoding="utf-8"))
keys = {w["readingKey"] for w in data["words"]}
unknown = sorted(set(mapping) - keys)
if unknown: sys.exit(f"picture-map.json names words that are not in words.json: {unknown}")
for w in data["words"]: w["picture"] = mapping.get(w["readingKey"], "")
json.dump(data, open(path, "w", encoding="utf-8"), ensure_ascii=False, indent=1)

os.makedirs(out, exist_ok=True)
icons = sorted(set(mapping.values()))
for name in os.listdir(out):  # drop pictures no word uses any more
    if name.endswith(".png") and name[:-4] not in icons:
        os.remove(os.path.join(out, name))
        if os.path.exists(os.path.join(out, name + ".meta")): os.remove(os.path.join(out, name + ".meta"))
for icon in icons:
    svg = open(os.path.join(here, "lucide", icon + ".svg"), encoding="utf-8").read().replace("currentColor", GLYPH)
    cairosvg.svg2png(bytestring=svg.encode(), write_to=os.path.join(out, icon + ".png"), output_width=SIZE, output_height=SIZE)
print(f"{sum(1 for w in data['words'] if w['picture'])} of {len(data['words'])} words have a picture; {len(icons)} icons rendered")
