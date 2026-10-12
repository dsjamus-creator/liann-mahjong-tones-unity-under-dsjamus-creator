"""Add the dictionary senseId to every word in words.json, in place, and repair any fullGloss that is only a digit
(an HSK band picked up by mistake) from the dictionary entry's senses (AlongSide's game-events upload needs entryId +
senseId + headword; lwl-backend docs/PINYIN-GAME-SETLISTS.md). build_words.py does the same when given the dictionary.

Usage: python3 -I add_sense_ids.py <package root> <lwl-backend>/public/classroom/dictionary/unified-dictionary.browser.json
(the signed learner release AlongSide resolves uploads against; the older data/unified-dictionary.ndjson also works)
The sense chosen is the first (by sourceLine) whose translation or gloss contains the tile label; otherwise the
entry's first sense. Fails if a word's entryId is not in the dictionary.
"""
import json, os, re, sys
from collections import defaultdict

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


pkg, dict_path = sys.argv[1], sys.argv[2]
senses = defaultdict(list)
for r in dictionary_records(dict_path):
    senses[r["entryId"]].append(r)

def pick_sense(entry_id, label):
    own = sorted(senses.get(entry_id, []), key=lambda r: r.get("sourceLine") or 0)
    if not own: return None
    want = re.sub(r"^to ", "", (label or "").lower()).strip()
    for r in own:
        text = ((r.get("translation") or "") + " ; " + (r.get("gloss") or "")).lower()
        if want and want in text: return r["senseId"]
    return own[0]["senseId"]

path = os.path.join(pkg, "Runtime", "Resources", "LiANNMahjongTones", "words.json")
data = json.load(open(path, encoding="utf-8"))
missing = []
for w in data["words"]:
    if re.fullmatch(r"\s*\d*\s*", w.get("fullGloss") or ""):
        own = sorted(senses.get(w["entryId"], []), key=lambda r: r.get("sourceLine") or 0)
        w["fullGloss"] = "; ".join(dict.fromkeys(r["gloss"] for r in own if r.get("gloss"))) or w["english"]
    sid = pick_sense(w["entryId"], w["english"])
    if not sid: missing.append(w["readingKey"]); continue
    # keep key order stable: insert senseId right after entryId
    items = list(w.items()); w.clear()
    for k, v in items:
        if k == "senseId": continue
        w[k] = v
        if k == "entryId": w["senseId"] = sid
if missing:
    sys.exit("entryId not in dictionary: " + ", ".join(missing))
json.dump(data, open(path, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print("senseId added to", len(data["words"]), "words")
