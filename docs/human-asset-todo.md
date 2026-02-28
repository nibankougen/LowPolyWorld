# 人間が用意するアセット TODO

開発者が用意する必要があるファイルの一覧です。
ファイルを用意したら ✅ をつけてください。

---

## 🔴 Phase 1 着手前に必要（最優先）

### 1. Humanoidアニメーション

Humanoidリグに対応したアニメーションファイル。
**Mixamo**（https://www.mixamo.com/ 無料）からFBXでダウンロード推奨。

| ✅ | ファイル名 | 配置先 |
|---|---|---|
| ☐ | Idle.fbx | `LowPolyWorldUnity/Assets/Animations/Common/Idle.fbx` |
| ☐ | Walk.fbx | `LowPolyWorldUnity/Assets/Animations/Common/Walk.fbx` |
| ☐ | Run.fbx | `LowPolyWorldUnity/Assets/Animations/Common/Run.fbx` |
| ☐ | Jump.fbx | `LowPolyWorldUnity/Assets/Animations/Common/Jump.fbx` |

**Mixaroダウンロード時の設定:**
- Format: `FBX for Unity (.fbx)`
- Skin: `Without Skin`（アニメーションのみ）
- FPS: `30`

---

### 2. ブロブシャドウテクスチャ

アバター足元に投影する丸い影テクスチャ。

| ✅ | ファイル名 | 配置先 |
|---|---|---|
| ☐ | BlobShadow.png | `LowPolyWorldUnity/Assets/Textures/BlobShadow.png` |

**仕様:**
- サイズ: 128×128 px
- 形式: PNG（アルファチャンネルあり）
- 内容: 中心が黒・周囲に向かって透明になる円形グラデーション
- 背景: 透明

> Photoshop / GIMP / Affinity Photo 等で「円形グラデーション（黒→透明）」を描くだけでOK。

---

### 3. テスト用ワールドGLB

Phase 1 の動作確認に使う簡易シーン。

| ✅ | ファイル名 | 配置先 |
|---|---|---|
| ☐ | TestWorld.glb | `LowPolyWorldUnity/Assets/StreamingAssets/Worlds/TestWorld.glb` |

**仕様:**
- 形式: GLB（バイナリGLTF）
- 内容: 地面（平面）＋障害物（箱・壁など）数点で十分
- スケール: Unityの1unit = 1mに合わせる（Blenderではエクスポート時に `Apply Transform` を有効にする）
- マテリアル: 何でもOK（ゲーム内ではUnlitシェーダーに差し替えます）
- ポリゴン数: 制限なし（テスト用のため）

> Blenderで平面を作りGLBエクスポートするだけでOK。

---

## 🟡 Phase 2 着手前に必要

### 4. テスト用VRM 1.0アバター

アバターシステムの動作確認に使うVRMファイル。

| ✅ | ファイル名 | 配置先 |
|---|---|---|
| ☐ | TestAvatar.vrm | `LowPolyWorldUnity/Assets/StreamingAssets/Avatars/TestAvatar.vrm` |

**仕様:**
- 形式: VRM **1.0**（VRM 0.x は不可）
- ポリゴン: 512 tris 以下推奨（本番仕様の確認のため）
- ボーン: Humanoid・50本以内
- ファイルサイズ: 500KB以内推奨

**入手方法:**
- Booth等で配布されている無料VRM 1.0モデルを使う

---

## 🟢 Phase 4 着手前に必要

### 5. テスト用アクセサリGLB

アクセサリシステムの動作確認用。複数あると望ましい。

| ✅ | ファイル名 | 配置先 |
|---|---|---|
| ☐ | AccessoryHead.glb | `LowPolyWorldUnity/Assets/StreamingAssets/Accessories/AccessoryHead.glb` |
| ☐ | AccessoryChest.glb | `LowPolyWorldUnity/Assets/StreamingAssets/Accessories/AccessoryChest.glb` |

**仕様（本番と同じ制限で作ること）:**
- 形式: GLB
- テクスチャ: 最大64×64 px（Diffuse 1枚のみ）
- ファイルサイズ: 100KB以内
- 内容: シンプルな形状でOK（帽子・リボン・バッジ等）
- スケール: Unityの1unit = 1mに合わせる
