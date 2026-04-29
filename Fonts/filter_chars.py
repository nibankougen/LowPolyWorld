"""
TMP_Japan1.txt から NotoSansCJKjp-Regular.ttf に含まれないグリフを除去するスクリプト。
実行: python Fonts/filter_chars.py
"""

import os
from fontTools.ttLib import TTFont

FONT_PATH = os.path.join(os.path.dirname(__file__), "ttf", "NotoSansCJKjp-Regular.ttf")
TEXT_PATH = os.path.join(os.path.dirname(__file__), "texts", "TMP_Japan1.txt")

font = TTFont(FONT_PATH)
cmap = font.getBestCmap()  # dict: codepoint(int) -> glyph name
covered = set(cmap.keys())

with open(TEXT_PATH, "r", encoding="utf-8") as f:
    original = f.read()

missing = []
kept = []
for ch in original:
    cp = ord(ch)
    if cp in covered:
        kept.append(ch)
    else:
        missing.append(ch)

missing_unique = sorted(set(missing), key=ord)
print(f"元の文字数       : {len(original)}")
print(f"フォントにある文字: {len(kept)}")
print(f"フォントにない文字: {len(missing)} 個（ユニーク {len(missing_unique)} 種）")

if missing_unique:
    print("欠けている文字一覧 (U+xxxx):")
    for ch in missing_unique:
        print(f"  U+{ord(ch):04X}  {ch!r}")

    with open(TEXT_PATH, "w", encoding="utf-8", newline="") as f:
        f.write("".join(kept))
    print(f"\nTMP_Japan1.txt を上書きしました（{len(kept)} 文字）。")
else:
    print("すべての文字がフォントに含まれています。変更なし。")
