"""
各言語の TMP テキストファイルを対応フォントで検証し、
フォントに含まれない文字を除去するスクリプト。
実行: python Fonts/filter_all.py
"""

import os
from fontTools.ttLib import TTFont

BASE = os.path.dirname(__file__)
TEXTS = os.path.join(BASE, "texts")
TTFS = os.path.join(BASE, "ttf")

TASKS = [
    {
        "font": "NotoSansCJKjp-Regular.ttf",
        "files": ["TMP_Japan1.txt"],
        "label": "日本語",
    },
    {
        "font": "NotoSansCJKsc-Regular.ttf",
        "files": ["TMP_GB1_1.txt", "TMP_GB1_2.txt", "TMP_GB1_3.txt"],
        "label": "簡体字",
    },
    {
        "font": "NotoSansCJKtc-Regular.ttf",
        "files": ["TMP_CN1_1.txt", "TMP_CN1_2.txt", "TMP_CN1_3.txt"],
        "label": "繁体字",
    },
    {
        "font": "NotoSansCJKkr-Regular.ttf",
        "files": ["TMP_KR_1.txt", "TMP_KR_2.txt"],
        "label": "韓国語",
    },
]

total_removed = 0

for task in TASKS:
    font_path = os.path.join(TTFS, task["font"])
    font = TTFont(font_path)
    covered = set(font.getBestCmap().keys())
    print(f"\n=== {task['label']} ({task['font']}) ===")

    for fname in task["files"]:
        fpath = os.path.join(TEXTS, fname)
        with open(fpath, "r", encoding="utf-8") as f:
            original = f.read()

        kept = []
        missing = []
        for ch in original:
            cp = ord(ch)
            if cp in covered:
                kept.append(ch)
            else:
                missing.append(ch)

        missing_unique = sorted(set(missing), key=ord)
        removed = len(missing)
        total_removed += removed

        status = "変更なし" if removed == 0 else f"{removed} 文字削除（ユニーク {len(missing_unique)} 種）"
        print(f"  {fname}: {len(original)} → {len(kept)}  [{status}]")

        if missing_unique:
            for ch in missing_unique:
                print(f"    U+{ord(ch):04X}  {ch!r}")
            with open(fpath, "w", encoding="utf-8", newline="") as f:
                f.write("".join(kept))

print(f"\n合計削除文字数: {total_removed}")
