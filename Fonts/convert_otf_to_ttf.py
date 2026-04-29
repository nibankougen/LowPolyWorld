"""
OTF → TTF 変換スクリプト（GPOS/GSUB/GDEF/JSTF/BASE 除去）
Unity TextMesh Pro Font Asset Creator 用

Usage:
    pip install fonttools
    python convert_otf_to_ttf.py

Input : Fonts/otf/*.otf
Output: Fonts/ttf/*.ttf
"""

import os
from pathlib import Path
from fontTools.ttLib import TTFont

BASE = Path(__file__).parent
OTF_DIR = BASE / "otf"
TTF_DIR = BASE / "ttf"
TTF_DIR.mkdir(exist_ok=True)

# TMP の SDF レンダリングには不要なテーブル
TABLES_TO_REMOVE = {"GPOS", "GSUB", "GDEF", "JSTF", "BASE", "morx", "kern"}

otf_files = sorted(OTF_DIR.glob("*.otf"))
if not otf_files:
    print("otf/ に .otf ファイルが見つかりません")
    raise SystemExit(1)

for otf_path in otf_files:
    out_path = TTF_DIR / (otf_path.stem + ".ttf")
    print(f"{otf_path.name} → {out_path.name} ...", end=" ", flush=True)

    font = TTFont(otf_path)

    removed = [t for t in TABLES_TO_REMOVE if t in font]
    for table in removed:
        del font[table]

    font.save(out_path)
    size_mb = out_path.stat().st_size / 1024 / 1024
    print(f"done ({size_mb:.1f} MB, removed: {', '.join(removed) or 'none'})")

print("\n完了。Fonts/ttf/ に TTF ファイルが生成されました。")
