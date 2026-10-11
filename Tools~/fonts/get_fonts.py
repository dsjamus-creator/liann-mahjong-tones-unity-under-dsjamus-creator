# Subset the game fonts from Google Fonts (OFL). Needs fontTools + brotli. Usage: python3 -I get_fonts.py <package root>
import sys,re,io,json,urllib.request,urllib.parse
from fontTools.ttLib import TTFont
pkg=sys.argv[1]
words=json.load(open(pkg+'/Runtime/Resources/LiANNMahjongTones/words.json',encoding='utf-8'))['words']
hz=set(''.join(w['hanzi'] for w in words))|set('你好一般半')
ascii_=''.join(chr(c) for c in range(32,127))
vowels='āáǎàēéěèīíǐìōóǒòūúǔùǖǘǚǜüĀÁǍÀĒÉĚÈĪÍǏÌŌÓǑÒŪÚǓÙǕǗǙǛÜ·—–…‘’“”▶−'
ua={"User-Agent":"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120 Safari/537.36"}
def fetch(family,text,out):
    url="https://fonts.googleapis.com/css2?family="+urllib.parse.quote(family,safe=':@;')+"&text="+urllib.parse.quote(text)
    css=urllib.request.urlopen(urllib.request.Request(url,headers=ua)).read().decode()
    srcs=re.findall(r"url\((https://fonts.gstatic.com/[^)]+)\)",css); assert len(srcs)==1,srcs
    f=TTFont(io.BytesIO(urllib.request.urlopen(srcs[0]).read())); f.flavor=None; f.save(out)
    cm=TTFont(out).getBestCmap(); miss=[c for c in text if ord(c) not in cm and c!=' ']
    print(out.split('/')[-1], len(cm),'glyphs; missing:', ''.join(miss) or 'none')
d=pkg+'/Runtime/Resources/LiANNMahjongTones/Fonts/'
fetch("Noto Sans SC:wght@700",''.join(sorted(hz))+ascii_+vowels,d+'NotoSansSC-Bold.ttf')
fetch("Nunito:wght@800",ascii_+vowels,d+'Nunito-ExtraBold.ttf')
