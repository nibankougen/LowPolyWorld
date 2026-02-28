# LowPolyWorld 開発計画

## 方針

- **ローカルファースト**: まずオフラインでゲームとして動くものを作る
- **段階的オンライン化**: 動作確認できてからAPIサーバーを統合する
- **ローカル開発環境**: APIサーバーは `docker compose up` でlocalhost起動

---

## Phase 1 — ローカル基盤

**目標**: プレイヤーがワールドを歩き回れる状態

### Unityクライアント
- [ ] カスタムUnlitシェーダー（両面描画・カットアウト・影なし・ライティングなし）
- [ ] テスト用ワールドシーン（地面 + 障害物の簡易GLBをGLTFastで読み込み）
- [ ] PlayerController（移動・ジャンプ・スプリント）
- [ ] 追従カメラ
- [ ] ブロブシャドウ（円形テクスチャを地面に投影）
- [ ] 共通アニメーションセット（待機・歩行・走り・ジャンプ）
- [ ] UI基盤（メニューオーバーレイ実装済み・画面遷移フロー）

---

## Phase 2 — ローカルアバター・アクセサリシステム

**目標**: ローカルのVRMとアクセサリを読み込んでアバターとして動かせる状態

### Unityクライアント — アバター
- [ ] UniVRM 1.0でVRMファイル読み込み（`StreamingAssets/` からローカル参照）
- [ ] 読み込んだVRMにカスタムUnlitシェーダーを適用（UniVRM標準シェーダーを上書き）
- [ ] Humanoidリターゲット（共通アニメーションをVRMに適用）
- [ ] `AvatarManager` 実装（アバターの生成・破棄・管理）
- [ ] `AtlasManager` 実装（1024×2048 固定スロット方式・RenderTextureで非同期合成）
  - 上段スロット: キャラクター24枠（4列×6行 @256×256、y:0〜1535）
  - 中段スロット: アクセサリ96枠（16列×6行 @64×64、y:1536〜1919）
  - 下段: 将来拡張用（y:1920〜2047）

### Unityクライアント — アクセサリ
- [ ] GLBファイル読み込み（GLTFastで`StreamingAssets/`からローカル参照）
- [ ] Humanoidボーンへのアタッチ（Head / LeftLowerArm / RightLowerArm / Chest / LeftUpperLeg / RightUpperLeg）
- [ ] アタッチ位置・回転のオフセット設定UI
- [ ] アクセサリテクスチャをAtlas下段スロットに書き込み
- [ ] 1アバター最大4個の制限管理

---

## Phase 3 — ローカルマルチプレイヤー

**目標**: 同一マシン上でホスト＋クライアントの複数人動作を確認できる状態

### 開発環境整備
- [ ] **ParrelSync** 導入（同一マシンで複数Unityエディタを同時起動してマルチ検証）

### Unityクライアント
- [ ] `NetworkManager` セットアップ（Netcode for GameObjects・Direct Connection）
- [ ] プレイヤー位置・回転の同期（クライアント側線形補間）
- [ ] アニメーション状態の同期（変更時のみ送信）
- [ ] アバター変更イベントの同期
- [ ] `WorldLoader` 実装（GLTF/GLBをAPIなしで静的パスから読み込む暫定版）

> Unity Relay（本番用）はPhase 5以降で設定する。ローカル開発はDirect Connectionで行う。

---

## Phase 4 — テクスチャペイント（ネイティブライブラリ）

**目標**: アバターのDiffuseテクスチャをペイントして保存できる状態
※ Phase 3と並行して進められる

### `paint-engine/`（Rust）
- [ ] レイヤー合成エンジン（最大8レイヤー・PNG出力）
- [ ] Undo履歴管理
- [ ] C ABI エクスポート（`#[no_mangle]` + P/Invoke用インターフェース設計）
- [ ] クロスコンパイル設定
  - iOS: `aarch64-apple-ios`（staticlib）
  - Android: `aarch64-linux-android`・`armv7-linux-androideabi`（cdylib）
  - PC: `x86_64-pc-windows-msvc` / `x86_64-apple-darwin`（cdylib）

### Unityクライアント
- [ ] `Assets/Plugins/` にビルド済みライブラリを配置・platform設定
- [ ] P/Invokeラッパークラス実装
- [ ] アバターペイントUI（256×256 キャンバス・ブラシ操作）
- [ ] アクセサリペイントUI（64×64 ドット絵スタイル・ピクセル単位編集）
- [ ] 完成画像をAtlasに反映（アバター→上段スロット / アクセサリ→下段スロット）

---

## Phase 5 — APIサーバー統合（Docker localhost）

**目標**: `docker compose up` で立ち上げたローカルAPIとUnityが連携できる状態

### 開発環境設定
- [ ] Unityに接続先URL設定（`ScriptableObject` またはシンボリック設定ファイルで切り替え）
  - 開発: `http://localhost:8080`
  - 本番: 環境変数で注入

### `api/`（Go）
- [ ] 認証エンドポイント（ログイン・トークン発行）
- [ ] VRMアップロードエンドポイント（optimizerへ転送）
- [ ] アバター取得エンドポイント（最適化済みVRMを返す）
- [ ] ワールドデータ取得エンドポイント（GLB URLを返す）
- [ ] アバター一覧取得エンドポイント

### `optimizer/`（Docker）
- [ ] アップロードされたVRMから不要データを削除（UniVRM仕様準拠）
- [ ] 容量・ポリゴン・ボーン数の検証（500KB / 512tris / 50bones）
- [ ] 最適化済みVRMを保存・返却

### Unityクライアント
- [ ] ログイン画面 + 認証フロー実装
- [ ] HTTP経由でのアバター一覧取得・ダウンロード
- [ ] VRMアップロードUI
- [ ] ワールドデータ取得・`WorldLoader` 本番版実装

---

## Phase 6 — 音声通信

**目標**: ワールド内で3D位置音声が動作する状態

### Unityクライアント
- [ ] Vivox SDK初期化・ログイン
- [ ] ワールド単位チャンネルの入退室管理
- [ ] プレイヤー座標をVivox SDKへ通知
- [ ] 距離減衰パラメータ調整

---

## Phase 7 — 最適化・成功条件達成

**目標**: 成功条件をすべて満たす

### チェックリスト（`docs/unity-game-abstract.md` より）

- [ ] 24人同時表示で30fps維持
- [ ] メモリ500MB未満
- [ ] Atlas更新1秒以内
- [ ] アバター読み込み2秒以内

### 作業
- [ ] プロファイリング（Unity Profiler・Memory Profiler）
- [ ] ドローコール最適化（GPU Instancing・Batching確認）
- [ ] AtlasManager非同期処理のチューニング
- [ ] VRMダウンロード・デコードの非同期化
- [ ] 低スペック端末での動作確認

---

## 依存関係

```
Phase 1
  └─ Phase 2
       └─ Phase 3 ──────────── Phase 5
            │                      └─ Phase 6
       Phase 4 (並行可)                  └─ Phase 7
```

---

## Docker ローカル開発

```bash
# APIサーバー + optimizer を起動
docker compose up --build

# Unityは http://localhost:8080 に接続（Phase 5以降）
```
