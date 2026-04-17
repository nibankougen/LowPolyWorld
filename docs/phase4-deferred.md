# Phase 4 — 着手保留項目

外部依存または環境依存のため、現時点では実装に着手できない項目の記録。
条件が整い次第 `development-plan.md` のチェックを更新し、こちらのエントリを削除する。

---

## 1. クロスコンパイル（iOS / Android）

### ブロッカー
- **iOS**: `aarch64-apple-ios` ターゲットのコンパイルには macOS + Xcode ツールチェーンが必須。Windows 環境ではビルド不可。
- **Android**: NDK (`cargo-ndk`) が必要。CI 環境（ubuntu ランナー）では対応可能だが、ローカル Windows 環境では追加セットアップが必要。

### 条件が整ったときにやること
1. `.cargo/config.toml` にターゲット設定を追加する
   ```toml
   [target.aarch64-apple-ios]
   ar = "ar"
   linker = "cc"

   [target.aarch64-linux-android]
   linker = "aarch64-linux-android-clang"

   [target.armv7-linux-androideabi]
   linker = "armv7a-linux-androideabi-clang"
   ```
2. GitHub Actions で macOS ランナー（iOS staticlib）・ubuntu ランナー（Android cdylib ×2）ビルドジョブを追加する
3. ビルド成果物を `LowPolyWorldUnity/Assets/Plugins/iOS/` および `Assets/Plugins/Android/arm64-v8a/` / `armeabi-v7a/` に配置する
4. 各プラグイン `.meta` ファイルの `platformData` で iOS / Android プラットフォームフラグを ON にする
5. Unity Editor で iOS ビルドターゲットに切り替えて `pe_canvas_create` 等の P/Invoke が `__Internal` 経由で解決されることを確認する

### 備考
- Windows DLL（`x86_64-pc-windows-msvc`）はすでに `Assets/Plugins/Windows/x86_64/paint_engine.dll` に配置済みで Editor で動作確認済み。
- `PaintEngineWrapper.cs` はすでに `#if UNITY_IOS && !UNITY_EDITOR` で `"__Internal"` / `"paint_engine"` を切り替えている。

---

## 2. UV レイヤー（表示オーバーレイ）

### ブロッカー
UV マップをテクスチャとしてベイクする処理の方針が未決定。

### 方針決定（調査済み）

**採用: `Mesh.AcquireReadOnlyMeshData()` + `Texture2D.SetPixels32`**

| 方法 | 精度 | 実装コスト | 備考 |
|------|------|------------|------|
| `Mesh.AcquireReadOnlyMeshData()` + `SetPixels32`（CPU） | 中 | 低 | **採用** |
| `Graphics.Blit` で UV アンラップ用シェーダーを使う（GPU） | 高 | 中 | AsyncGPUReadback が必要で複雑 |
| 外部ツール（Blender 等）で事前ベイクして PNG として同梱 | 高 | 高（手作業） | — |

**`Mesh.uv` を使わない理由**: glTFast (`com.unity.cloud.gltfast`) でロードされたワールドオブジェクトのメッシュは `isReadable = false` になるため、`Mesh.uv` は空配列を返す。`Mesh.AcquireReadOnlyMeshData()` は `isReadable` に関係なくネイティブメモリから読めるため、VRM・GLB 両方に対して同一コードで動作する。

**パフォーマンス**: 一回限りの処理で < 1ms（256×256 アバター・512×512 ワールドオブジェクト両方）。`Allocator.Temp` による NativeArray で GC 負荷なし。

```csharp
// 実装パターン（VRM・GLB 共通）
using var meshDataArray = Mesh.AcquireReadOnlyMeshData(mesh);
var meshData = meshDataArray[0];
var uvs = new NativeArray<Vector2>(meshData.vertexCount, Allocator.Temp);
var indices = new NativeArray<int>(meshData.GetSubMesh(0).indexCount, Allocator.Temp);
meshData.GetUVs(0, uvs);
meshData.GetIndices(indices, 0);
// Bresenham でエッジを Color32[] に書き込む
// texture.SetPixels32(pixels); texture.Apply(false);
uvs.Dispose(); indices.Dispose();
```

### 実装済み（Phase 4）

- [x] Rust: `Canvas.uv_overlay` フィールド追加・`set_uv_overlay` / `set_uv_overlay_visible` メソッド実装
- [x] Rust: `pe_canvas_set_uv_overlay` / `pe_uv_overlay_set_visible` P/Invoke エクスポート追加・DLL 再ビルド
- [x] C#: `PaintEngineWrapper` に DllImport と `SetUvOverlay` ヘルパー追加
- [x] C#: `IPaintSession` に `SetUvOverlay` / `SetUvOverlayVisible` / `HasUvOverlay` / `UvOverlayVisible` 追加
- [x] C#: `AvatarPaintSession` / `AccessoryPaintSession` に実装
- [x] C#: `UvOverlayBaker` ロジッククラス新規作成（`Mesh.AcquireReadOnlyMeshData` + Bresenham）
- [x] C#: `TexturePaintController.RefreshLayerPanel` に UV エントリ（表示切り替えのみ）追加
- [x] C#: `AvatarEditController` に `_uvSourceRenderers` フィールド追加・UV ベイク配線
- [x] テスト: `UvOverlayBakerTests` 9件追加・全通過

### 残り作業
- [ ] ワールドオブジェクト編集コントローラー実装時に `MeshFilter.sharedMesh` から UV をベイクして渡す（Phase 4 ワールドオブジェクト編集実装時）
- [x] `AvatarEditController._uvSourceRenderers` を VRM ロード後に動的にセットする配線 — `SetUvSourceRenderers(SkinnedMeshRenderer[])` メソッドを公開済み。Phase 5A で AvatarManager からの呼び出し配線を追加する

---

## 3. 保存処理（アバター / アクセサリ / ワールドオブジェクト）

### ブロッカー
Phase 9 で実装予定の以下の REST API エンドポイントが未実装。

| エンドポイント | 用途 |
|---------------|------|
| `POST /api/v1/avatars/{id}/texture` | アバター Diffuse テクスチャ（PNG）のアップロード |
| `POST /api/v1/avatars/{id}/texture/layers` | レイヤー構造 JSON + 差分レイヤー画像のアップロード |
| `POST /api/v1/accessories/{id}/texture` | アクセサリ Diffuse テクスチャのアップロード |
| `POST /api/v1/worldobjects/variants` | ワールドオブジェクト保存バリアントの新規作成 |

### 現時点でのローカル代替
- `TexturePaintController.OnSave` は保存ボタン押下時に `CompositePng()` を呼び出しており、**Atlas への即時反映**（ローカル動作確認用）は Phase 4 で実装済み（下記参照）。
- Editor では `OnExportPng` でローカルファイルに PNG 書き出し可能。

### 条件が整ったときにやること（Phase 9）
1. `ITextureUploadService` インターフェースを定義する（テスタブルにするため）
2. `HttpTextureUploadService : ITextureUploadService` を実装する
3. レイヤー構造 JSON のスキーマを API チームと合意する（`PaintLayer` 配列の JSON シリアライズ形式）
4. `OnSave` でローカル Atlas 反映 → `ITextureUploadService.Upload(png)` の順に実行する
5. アップロード失敗時のリトライ UI（トースト通知）を追加する

### レイヤー構造 JSON 想定スキーマ（設計メモ）
```json
{
  "canvasWidth": 256,
  "canvasHeight": 256,
  "layers": [
    {
      "id": 1,
      "name": "Layer 1",
      "type": "normal",
      "opacity": 1.0,
      "visible": true,
      "locked": false,
      "maskBelow": false,
      "groupId": null
    }
  ],
  "colorAdjustment": {
    "brightness": 0.0,
    "saturation": 0.0,
    "contrast": 0.0,
    "hueShift": 0.0
  }
}
```
