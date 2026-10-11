"""Write Unity .meta files for every asset and folder (git packages are read-only in Unity, so metas must be committed).
GUIDs are derived from the path, so re-running never changes an existing GUID. Usage: python3 -I make_meta.py <package root>"""
import hashlib, os, sys
root = sys.argv[1]
def guid(rel): return hashlib.md5(("com.liann.mahjongtones/" + rel).encode()).hexdigest()
made = 0
for dirpath, dirnames, filenames in os.walk(root):
    dirnames[:] = [d for d in dirnames if not d.startswith('.') and not d.endswith('~')]
    rel_dir = os.path.relpath(dirpath, root)
    entries = [(d, True) for d in dirnames] + [(f, False) for f in filenames if not f.endswith('.meta') and not f.startswith('.')]
    for name, is_dir in entries:
        rel = os.path.normpath(os.path.join(rel_dir, name))
        meta = os.path.join(root, rel + '.meta')
        if os.path.exists(meta): continue
        body = f"fileFormatVersion: 2\nguid: {guid(rel)}\n"
        if is_dir: body += "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
        with open(meta, 'w', newline='\n') as f: f.write(body)
        made += 1
print('meta files written:', made)
