"""Build Runtime/Resources/LiANNMahjongTones/words.json (+ audio + review CSV) from a pinyin-tiles-playtest checkout.

Usage: python3 -I build_words.py <pinyin-tiles-playtest checkout> <package root> [<lwl-backend>/data/unified-dictionary.ndjson]

Keeps HSK 1-3 words that have a recorded MP3 in the Tiles pronunciation pack, a parseable reading key and
a short English label (1-3 words after overrides). Same-hanzi words with two readings are dropped, because the
tone answer would be ambiguous. Homophone mix-ups are dropped: keys in homophone-denylist.json, and, when the unified
dictionary is given, any key whose HSK-band gloss sense does not belong to the headword's own dictionary entry
(the gloss table is keyed by reading, so homophones share it). Tone-pair groups = same toneless pinyin, different hanzi.
"""
import csv, glob, json, os, re, shutil, sys
from collections import defaultdict

src, pkg = sys.argv[1], sys.argv[2]
here = os.path.dirname(os.path.abspath(__file__))
pub = os.path.join(src, "public")
audio_index = json.load(open(glob.glob(os.path.join(pub, "word-audio", "audio-index.*.json"))[0], encoding="utf-8"))
corpus = json.load(open(glob.glob(os.path.join(pub, "novice-assets", "corpus-*.json"))[0], encoding="utf-8"))
glosses = corpus["glosses"]

def dictionary_records(path):
    """Yield dictionary senses from AlongSide's signed learner release (public/classroom/dictionary/
    unified-dictionary.browser.json, packed rows) or from the older data/unified-dictionary.ndjson."""
    if path.endswith(".json"):
        d = json.load(open(path, encoding="utf-8"))
        for row in d["records"]:
            yield dict(zip(d["fields"], row))
        return
    for line in open(path, encoding="utf-8"):
        if line.strip():
            yield json.loads(line)


deny = {k for k in json.load(open(os.path.join(here, "homophone-denylist.json"), encoding="utf-8")) if not k.startswith("_")}
dictionary = defaultdict(list)
if len(sys.argv) > 3:
    for r in dictionary_records(sys.argv[3]):
        dictionary[r["entryId"]].append(r)
overrides = {k: v for k, v in json.load(open(os.path.join(here, "label-overrides.json"), encoding="utf-8")).items() if not k.startswith("_")}

KEY = re.compile(r"^(?:[a-z]+[1-5])+$")

def first_clause(s):
    return re.sub(r"\([^)]*\)", "", s or "").split(";")[0].strip().lower()

def homophone_mismatch(key, entry_id, band):
    """True when the gloss senses for this HSK band are not senses of the headword's own dictionary entry."""
    own = dictionary.get(entry_id)
    g = glosses.get(key)
    if not own or not g or len(g) < 4 or not g[3]: return False
    band_senses = [s for s, b in g[3] if b == band]
    if not band_senses: return False
    own_text = " ; ".join(r["gloss"] for r in own).lower()
    return not any(first_clause(s) and first_clause(s) in own_text for s in band_senses)

def short_label(first):
    s = re.sub(r"\([^)]*\)", "", first or "")
    s = re.split(r"[;,/]", s)[0].strip()
    s = re.sub(r"\s+", " ", s)
    return s

entries = audio_index["entries"]
by_hanzi = defaultdict(list)
for key, e in entries.items():
    by_hanzi[e["headword"]].append(key)

kept, dropped = [], defaultdict(list)
out_audio = os.path.join(pkg, "Runtime", "Resources", "LiANNMahjongTones", "Audio")
os.makedirs(out_audio, exist_ok=True)
for old in os.listdir(out_audio):   # start clean so dropped words leave no clip (or .meta) behind
    os.remove(os.path.join(out_audio, old))
for key in sorted(entries):
    e = entries[key]
    band = e.get("hskBand")
    if band not in ("1", "2", "3"):
        continue
    if not KEY.match(key) or len(re.findall(r"\d", key)) < 2:
        dropped["reading key"].append(key); continue
    if key in deny or homophone_mismatch(key, e["entryId"], band):
        dropped["homophone: HSK word is a different hanzi"].append(key); continue
    if len(by_hanzi[e["headword"]]) > 1:
        dropped["same hanzi, two readings"].append(key); continue
    mp3 = e.get("encodings", {}).get("mp3")
    path = mp3 and os.path.join(pub, mp3["url"].replace("/playtest/", "", 1))
    if not path or not os.path.exists(path):
        dropped["no MP3"].append(key); continue
    g = glosses.get(key)
    label = overrides.get(key) or short_label(g[0] if g else "")
    if not label or len(label.split()) > 3:
        dropped["no short English label"].append(key); continue
    shutil.copyfile(path, os.path.join(out_audio, key + ".mp3"))
    kept.append({
        "readingKey": key, "entryId": e["entryId"], "hanzi": e["headword"], "english": label,
        # some gloss rows carry the HSK band in slot 1 instead of the full gloss; fall back to slot 0 then
        "fullGloss": (g[1] if g and len(g) > 1 and not str(g[1]).strip().isdigit() else (g[0] if g else "")), "hskBand": int(band),
        "audio": key, "picture": "",
        "_labelSource": "override" if key in overrides else "dictionary-first-sense",
    })

# English labels must be unique per board; the selector enforces it, but flag collisions for proofreading.
by_label = defaultdict(list)
for w in kept:
    by_label[w["english"].lower()].append(w["readingKey"])

groups = defaultdict(list)
for w in kept:
    groups[re.sub(r"\d", "", w["readingKey"])].append(w["readingKey"])
tone_pairs = [sorted(v) for k, v in sorted(groups.items()) if len(v) > 1]

data = {
    "schemaVersion": 1,
    "source": "pinyin-tiles-playtest word-audio index + Tiles corpus glosses (CC-CEDICT-derived, CC BY-SA 4.0); labels per Tools~/data/label-overrides.json",
    "words": [{k: v for k, v in w.items() if not k.startswith("_")} for w in kept],
    "tonePairs": [{"keys": g} for g in tone_pairs],
}
res = os.path.join(pkg, "Runtime", "Resources", "LiANNMahjongTones")
with open(os.path.join(res, "words.json"), "w", encoding="utf-8") as f:
    json.dump(data, f, ensure_ascii=False, indent=1)
with open(os.path.join(pkg, "Tools~", "data", "english-labels-review.csv"), "w", encoding="utf-8", newline="") as f:
    wr = csv.writer(f)
    wr.writerow(["readingKey", "hanzi", "hskBand", "label", "labelSource", "sharedLabelWith", "fullGloss"])
    for w in kept:
        same = [k for k in by_label[w["english"].lower()] if k != w["readingKey"]]
        wr.writerow([w["readingKey"], w["hanzi"], w["hskBand"], w["english"], w["_labelSource"], " ".join(same), w["fullGloss"]])

print("kept", len(kept), {k: len(v) for k, v in dropped.items()}, "tone-pair groups", len(tone_pairs))
print("label collisions", sum(1 for v in by_label.values() if len(v) > 1))
