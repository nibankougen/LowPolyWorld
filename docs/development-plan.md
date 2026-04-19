# Lo-Res World 開発計画

## 方針

- **ローカルファースト**: まずオフラインでゲームとして動くものを作る
- **段階的オンライン化**: 動作確認できてからAPIサーバーを統合する
- **ローカル開発環境**: APIサーバーは `docker compose up` でlocalhost起動
- **MonoBehaviour 責務制限**: Unity エンジンとの接続部分（物理コールバック・ライフサイクル・アニメーション・シーン参照）のみ。ゲームロジックは含めない
- **ロジッククラス（純粋 C#）**: `MonoBehaviour` に依存しない純粋クラスでゲームロジックを実装する（詳細: `docs/unity-game-abstract.md` セクション 19）
- **テスト**: ロジッククラスは Unity Test Runner EditMode（`Assets/Tests/EditMode/`）でユニットテストを作成する。ロジッククラスの新規作成・変更時は対応するテストも作成・更新する

---

## Phase 1 — ローカル基盤

**目標**: プレイヤーがワールドを歩き回れる状態

### Unityクライアント
- [x] カスタムUnlitシェーダー（両面描画・カットアウト・影なし・ライティングなし）
- [x] テスト用ワールドシーン（地面 + 障害物の簡易GLBをGLTFastで読み込み）
- [x] PlayerController（移動・ジャンプ・スプリント）
  - [x] 物理コライダー設定（カプセル 直径0.4m / 高さ 1.45m）
  - [x] タッチ判定コライダー設定（直径1.0m / 高さ2.0m）
  - [x] Physics Layer 設定: `Player` レイヤーと `World` レイヤーを作成し、Layer Collision Matrix で `Player`-`Player` 間の衝突を無効化（アバター同士は重なることができる。`Player`-`World` 衝突は有効）
  - [x] タッチ操作（画面下半分: 移動・ジャンプ / 上半分: カメラ回転）
    - [x] 移動入力ロジック: スライド開始で移動開始。0.3 秒以内にスライド移動量が誤差範囲内に収まったらスプリント移行
    - [x] マルチタッチ優先制御: 移動スライドは最初のタッチのみ有効。最初のタッチが離れるまで後続タッチの移動入力を無視
    - [x] ジャンプ入力: 画面下半分のダブルタップでジャンプ（高さ 0.55）
    - [x] 方向ジャンプ: 移動入力中に別指でダブルタップ → 移動方向へジャンプ
    - [x] ダブルタップ→スライド移行: 2 回目タップからスライドに移行した場合、そのスライド方向へジャンプ
    - [x] コントロールボタン（設定 ON 時）: ジャンプボタン・スプリントボタンを画面下部に表示。ボタン有効時はダブルタップジャンプ・スライド速度スプリントを無効化
    - [x] 設定 19.5（コントロール設定）: ジャンプボタン/スプリントボタン表示 ON/OFF（PlayerPrefs 保存）
- [x] 追従カメラ
- [x] ブロブシャドウ（円形テクスチャを地面に投影）
- [x] 共通アニメーションセット（待機・歩行・走り・ジャンプ）
- [x] UI基盤（ナビゲーションバー: ワールド / アバター管理 / ワールド管理 / ショップ / 設定 の 5 タブ・デフォルト: ワールドタブ・画面遷移フロー）
- [x] `SafeAreaFitter` コンポーネント実装（`Screen.safeArea` を参照してキャンバスルートの RectTransform を調整）・全キャンバスに適用
  - [x] ワールドタブ（ホーム / フォロー中 / 新着順 / いいね の 4 タブ構成は Phase 5 でフル実装・Phase 1 はスタブ画面）
  - [x] アバター管理タブ（Phase 2 でフル実装・Phase 1 はスタブ画面）
  - [x] ワールド管理タブ（Phase 12 でフル実装・Phase 1 はスタブ画面）
  - [x] ショップタブ（Phase 8 でフル実装・Phase 1 はスタブ画面）
  - [x] 設定タブ（音量設定 3 スライダー・言語設定は Phase 5 で実装）
- [x] インワールドHUD（右上: カメラ/ベル/☰ ボタン・左下: ジャンプボタン（設定ON時）・下中央: アクションボタン・右下: スプリントボタン（設定ON時））
- [x] インワールドメニュー（☰タップで表示・右上×で閉じる・4タブ: ルーム/アバター/ワールド一覧/設定）
  - [ ] ルームタブ: ワールド詳細情報・ルーム情報（言語設定/上限/セッション残り時間）・再入室ボタン・ルームメンバー一覧（Phase 3 でフル実装・Phase 1 はスタブ）
    - [ ] 言語設定変更（ルーム作成者のみ）
    - [ ] ルームメンバーパネル（プロフィール・フォロー/フレンド申請ボタン・その他（…）→非表示/通報）
    - [ ] 非表示メンバーをリスト下部に薄く表示
  - [ ] アバタータブ: 所持アバターサムネイルグリッド・サブタブ3種（スロット/ショップ購入/デフォルト）・アバター変更確認モーダル（Phase 2 でフル実装・Phase 1 はスタブ）
  - [ ] ワールド一覧タブ: 入室時のワールド一覧をキャッシュして表示（Phase 5 でフル実装・Phase 1 はスタブ）
  - [x] 設定タブ: 音量3スライダー（設定タブと共有）+ コントロール設定（セクション 19.5 と共有）
- [x] ローカライゼーション基盤（`com.unity.localization` パッケージ設定・日本語/英語 String Table 初期作成・システム言語フォールバック実装）
- [x] オーディオシステム
  - [x] AudioMixer 設定（`WorldSFX` / `SystemSFX` グループ・exposed parameter 登録）
  - [x] `AudioManager` 実装（各グループ音量制御・PlayerPrefs 永続化）
  - [x] `AmbientSoundPlayer` 実装（soundId と volume を受け取り内蔵ライブラリからループ再生・フェードイン/アウト。ワールド設定 UI との接続は Phase 12 で行う）
  - [x] システム音 SE の基本セット定義と再生（ボタン音など）
- [x] `WorldSettingsLogic`（純粋 C#: 3 カテゴリの音量管理・クランプ・変更イベント通知）

### テスト（EditMode）
- [x] `PlayerMovementLogic`: 移動状態遷移（待機→歩行→走り・ジャンプ可否判定）
- [x] `CameraFollowLogic`: 追従オフセット計算・仰角クランプ
- [x] `WorldSettingsLogic`: 初期値（1.0f）・クランプ（0f〜1f に収まる）・変更イベント発火・同値では発火しない

---

## Phase 2 — ローカルアバター・アクセサリシステム

**目標**: ローカルのVRMとアクセサリを読み込んでアバターとして動かせる状態

### Unityクライアント — アバター
- [x] UniVRM 1.0でVRMファイル読み込み（`StreamingAssets/` からローカル参照）
- [x] 読み込んだVRMにカスタムUnlitシェーダーを適用（UniVRM標準シェーダーを上書き）
- [x] Humanoidリターゲット（共通アニメーションをVRMに適用）
- [x] `AvatarManager` 実装（アバターの生成・破棄・管理）
- [x] `AtlasManager` 実装（1024×2048 固定スロット方式・RenderTextureで非同期合成）
  - 上段スロット: キャラクター24枠（4列×6行 @256×256、y:0〜1535）
  - 中段スロット: アクセサリ96枠（16列×6行 @64×64、y:1536〜1919）
  - 下段: 将来拡張用（y:1920〜2047）

### Unityクライアント — アクセサリ
- [x] GLBファイル読み込み（GLTFastで`StreamingAssets/`からローカル参照）
- [x] Humanoidボーンへのアタッチ（Head / LeftLowerArm / RightLowerArm / Chest / LeftUpperLeg / RightUpperLeg）
- [x] アクセサリテクスチャをAtlas下段スロットに書き込み
- [x] 1アバター最大4個の制限管理

### Unityクライアント — 編集画面UI
- [x] アバター編集画面（セクション 3.5）
  - [x] 下部アイコンタブ（テクスチャ / アクセサリ）
  - [x] アクセサリタブ: 4 サブタブ（利用中 / 保存・編集 / ショップ購入 / デフォルトプリセット）
  - [x] アクセサリ選択状態: チェックマーク・設置ボーン選択フォーム・削除ボタン・ギズモ（オフセット/回転/拡大縮小フィールド）<!-- Phase 4 テクスチャペイントと並行して実装 -->
  - [x] アクセサリの D&D による取り付け<!-- Phase 4 と並行 -->
- [x] アクセサリ編集画面（セクション 3.6）
  - [x] 下部アイコンタブ（テクスチャ / 配置）
  - [x] 配置タブ: 3D プレビュー拡大・つける場所選択フォーム・アバター切り替えボタン・ギズモ
- [x] 編集画面共通UI（セクション 20）
  - [x] 3D プレビュー: 1本指カメラ回転・2本指平行移動/ズーム・カメラ制限（対象が映っている状態を維持）
  - [x] 下部タブ最小化（▽/△ボタン・イースイン・アウトスライドアニメーション）
  - [x] Undo/Redo 50段階（定数 `PaintUndoMaxSteps` で管理・モバイルメモリに応じて調整可）（タブ直上左上 < > ボタン）

### テスト（EditMode）
- [x] `AtlasLayout`: スロット割り当て（キャラ24枠・アクセサリ96枠）・UV 座標計算・スロット間パディング計算
- [x] `AtlasLayout`: スロット満杯時の割り当て失敗・解放後の再利用

---

## Phase 3 — ローカルマルチプレイヤー

**目標**: 同一マシン上でホスト＋クライアントの複数人動作を確認できる状態

### 開発環境整備
- [ ] **ParrelSync** 導入（同一マシンで複数Unityエディタを同時起動してマルチ検証）

### Unityクライアント
- [x] `NetworkManager` セットアップ（Netcode for GameObjects・Direct Connection）
- [x] プレイヤー位置・回転の同期（クライアント側線形補間）
  - [x] 送信レート制御: 最大 20Hz・変化量が閾値（位置 > 0.01m または 回転 > 1°）を超えた場合のみ送信（Unity Relay 従量課金削減）
- [x] アニメーション状態の同期（変更時のみ送信）
- [x] アバター変更イベントの同期
- [x] `WorldLoader` 実装（GLTF/GLBをAPIなしで静的パスから読み込む暫定版）
- [x] `SessionTimeLimitLogic` 実装（セッション残り時間計算・警告イベント（残り10分・5分・1分）・制限時間到達イベント）
- [x] `AfkDetectionLogic` 実装（操作なし経過時間計算・AFK判定・操作受信でタイマーリセット）
- [x] ルームセッション制限 UI
  - [x] 設定パネルへの残り時間表示（`残り 時:分:秒` 形式）
  - [x] フラッシュメッセージ表示（残り10分・5分・1分のタイミング）
  - [x] 制限時間到達ダイアログ（OK タップまたは数秒後に自動退室）
  - [x] 放置自動退室（通常ユーザーのみ・10分無操作で即時退室）
- [x] `ReconnectionLogic` 実装（指数バックオフ再接続: 1→2→4→8→16 秒・最大 5 回）
- [x] ネットワーク切断 UI
  - [x] 再接続中モーダル（「接続が切断されました。再接続しています…」＋「タイトルに戻る」ボタン・閉じ不可）
  - [x] 接続失敗モーダル（「接続に失敗しました」＋「タイトルに戻る」ボタン・閉じ不可）
  - [x] 再接続成功後: 通常入室と同じスポーンフローを再実行
- [x] ルームオーナー移譲
  - [x] オーナー退席時（切断・退室・放置退室）: サーバーが最長在室ユーザーに自動移譲・全クライアントへ通知
  - [x] 新オーナーがギミックステートマスターの役割を引き継ぐ（言語・人数設定の変更権は元作成者のみ維持）
  - [x] 元オーナー再入室時: 現オーナーから通常の後入室同期フローで状態を受け取る

> Unity Relay（本番用）はPhase 5以降で設定する。ローカル開発はDirect Connectionで行う。

### テスト（EditMode）
- [x] `SessionTimeLimitLogic`: 残り時間計算の正確性・警告イベント発火タイミング（10分・5分・1分前）・制限時間到達イベント
- [x] `SessionTimeLimitLogic`: 制限時間をゼロ以下にしたとき到達イベントが1回だけ発火すること
- [x] `AfkDetectionLogic`: 10分無操作でAFK判定・操作受信でタイマーリセット・リセット後は再びカウント開始
- [x] `ReconnectionLogic`: 指数バックオフ待機時間の計算（試行 n → 2^(n-1) 秒）・最大試行回数到達でFailureイベント発火・成功でSuccessイベント発火

---

## Phase 4 — テクスチャペイント（ネイティブライブラリ）

**目標**: アバターのDiffuseテクスチャをペイントして保存できる状態
※ Phase 3と並行して進められる

詳細仕様: `docs/unity-game-abstract.md` セクション8 参照

### `paint-engine/`（Rust）

#### コアエンジン
- [x] レイヤーモデル実装（通常レイヤー最大16枚 + 色調補正レイヤー1枚/テクスチャ・別枠）（`layer.rs`）
- [x] UV レイヤー（最上段固定・表示オーバーレイのみ・枚数制限外）（`canvas.rs` `uv_overlay` フィールド・`UvOverlayBaker.cs`）
- [x] ベースレイヤー（最下段固定・2値マスク・枚数制限外）実装: ブラシ=不透明・消しゴム=透明・半透明不可
- [x] レイヤーグループ実装（フォルダ状にネスト・グループ単位の表示切り替え）（`canvas.rs` `groups` フィールド・`pe_group_*` エクスポート・C# `IPaintSession`/`AvatarPaintSession`）<!-- UI のグループ折り畳み表示は未実装 -->
- [x] レイヤー合成エンジン（通常合成 → ベースレイヤーでマスク → PNG出力）（`compositor.rs`）
- [x] 色調補正レイヤー処理（明度・彩度・コントラスト・色相シフト）（HSV変換、`compositor.rs`）
- [x] 「下のレイヤーにマスク」（`mask_below`）対応
- [x] レイヤー結合処理（選択レイヤーを1枚のラスターに統合）（`canvas.rs` `merge_down`）
- [x] Undo/Redo履歴管理（50ステップ・定数 `PaintUndoMaxSteps` で管理・タブ単位でリセット）（`undo.rs`）
- [x] 透明ピクセル処理（保存時: α < 128 → α=0・RGB=(0,0,0)黒固定、α ≥ 128 → α=255・RGB 保持）（`compositor.rs` `process_for_save`）
- [x] テクスチャ範囲外データのクリーンアップ（編集終了時に自動実行）（保存時 `pe_canvas_cleanup` 呼び出し・リサイズ時に Undo/選択範囲をクリア）

#### ペイントツール処理
- [x] ブラシツール: 円形アンチエイリアスなし（Nearest Neighbor・デフォルト）/ 円形アンチエイリアスあり（ソフトエッジ）。サイズ 1〜255px（`tools.rs`）
- [x] 消しゴムツール（α=0 に戻す）（`tools.rs`）
- [x] 塗りつぶしツール（フラッドフィル・許容値設定・他レイヤー参照オプション）（`tools.rs`）
- [x] 図形ツール（四角形 / 円 / 直線）（`tools.rs`）
- [x] 範囲選択ツール（矩形 / 楕円 / 選択範囲への操作制限）（`canvas.rs` `selection` フィールド・`pe_selection_*`・`TexturePaintController.cs` SelectRect/SelectEllipse）<!-- 塗りつぶし範囲選択・複数選択は未実装 -->
- [x] 移動・拡大縮小ツール（レイヤー移動・最近傍補間スケール）（`canvas.rs` `translate_layer`/`scale_layer_nn`・`pe_layer_translate`/`pe_layer_scale_nn`）<!-- Bicubic 補間は未実装 -->
- [x] レイヤー取り込み（PNG ファイルから読み込み・最近傍リサイズ）（`canvas.rs` `import_layer_png`・`TexturePaintController.OnImportLayerPng`）<!-- Bicubic リサイズは未実装 -->

#### C ABI エクスポート
- [x] `#[no_mangle]` + P/Invoke用インターフェース設計（`lib.rs`）
- [x] コマンド送受信API（キャンバスデータ・操作コマンド → 合成済みPNG返却）（`lib.rs` 全 `pe_*` 関数）

#### クロスコンパイル設定
- [ ] iOS: `aarch64-apple-ios`（staticlib）
- [ ] Android: `aarch64-linux-android`・`armv7-linux-androideabi`（cdylib）
- [x] PC: `x86_64-pc-windows-msvc`（cdylib）— `Assets/Plugins/Windows/x86_64/paint_engine.dll` 配置済み

### Unityクライアント — ペイントUI共通基盤
- [x] `Assets/Plugins/Windows/x86_64/` にビルド済みライブラリを配置（`paint_engine.dll`）
- [ ] `Assets/Plugins/` iOS/Android プラットフォーム設定（Unity Inspector で platform 指定）
- [x] P/Invokeラッパークラス実装（`PaintEngineWrapper.cs`）
- [x] テクスチャ編集バー UI（ブラシ / 消しゴム / 塗りつぶし / 四角形 / 円 / 直線 / レイヤー / その他 ツールボタン）（`AvatarEditScreen.uxml`・`TexturePaintController.cs`）
- [x] 色選択パネル UI（色相リング + 明度彩度四角形 + 透明度スライダー + RGBA入力 + 色履歴16色）（`ColorPickerLogic.cs`・`TexturePaintController.cs`）
- [x] ブラシサイズバー（画面左・1〜255px・Slider）（`BrushSettingsLogic.cs`・UXML/USS）
- [x] 2Dキャンバスのホイール・ピンチによるズーム操作実装（最大8倍・最小0.5倍）（`PaintCanvasLogic.cs`）
- [x] 3Dプレビュー（ペイント内容をリアルタイムにアバターへ反映して表示）（`AvatarEditController.OnPreviewTextureUpdated` → `_previewAvatarRenderer.material.mainTexture`）<!-- プレビューアバターのメッシュ設定は Inspector で手動割り当て -->
- [x] レイヤーパネル UI（レイヤー追加・色調補正追加・表示非表示・選択切り替え）（`TexturePaintController.cs`・UXML/USS）
  - [x] サムネイル（Texture2D で各レイヤーのピクセルを表示）（`TexturePaintController.cs`・`PaintEngineWrapper.cs`・`lib.rs` `pe_layer_get_pixels`）
  - [x] その他アクション（削除/透明度/マスク/ロック）（`TexturePaintController.cs`・`AvatarPaintSession.cs`・USS）
  - [x] ドラッグ並び替え（`TexturePaintController.cs` PointerDown/Move/Up によるドラッグ実装済み）
  - [x] UV レイヤー・ベースレイヤーの表示（`BuildUvOverlayItem`/`BuildBaseLayerItem`）
- [x] その他メニュー UI（画像書き出し `OnExportPng` / テクスチャサイズ変更 `OnTextureResize` / PNG取り込み `OnImportLayerPng`）<!-- 簡単テクスチャ切り替えモーダルは仕様未定のためスタブ -->
- [ ] 簡単テクスチャモード UI（スタブ・詳細仕様別途定義）
- [x] Undo/Redo（テクスチャタブ内操作・< > ボタン）（`AvatarEditController.cs` + `AvatarPaintSession` の `PaintCommandHistory`）

### Unityクライアント — アバターペイントUI
- [x] 256×256キャンバス表示・編集（`AvatarPaintSession.cs`・`TexturePaintController.cs`）
- [ ] 保存処理（Phase 9 API 実装後）
  - [ ] 変更済みレイヤー画像のみアップロード（256×256）
  - [ ] レイヤー構造JSONアップロード
  - [ ] 256×256統合画像アップロード（`AvatarPaintSession.CompositePng()`）
- [x] 統合画像をAtlas上段スロットに反映（`AvatarEditController.OnSaveRgbaToAtlas` → `AtlasManager.WriteCharacterTexture`）

### Unityクライアント — アクセサリペイントUI
- [x] 64×64キャンバス表示・編集（`AccessoryPaintSession.cs`・`AccessoryEditController.cs`）
- [ ] 保存処理（Phase 9 API 実装後）
  - [ ] 変更済みレイヤー画像のみアップロード
  - [ ] レイヤー構造JSONアップロード
  - [ ] 64×64統合画像アップロード
- [x] 統合画像をAtlas中段スロットに反映（`AccessoryEditController.OnSaveRgbaToAtlas` → `AtlasManager.WriteAccessoryTexture`）

### テスト（EditMode）
- [x] `LayerStack`: レイヤー追加・削除・複製・並び替え・上限チェック（16枚）
- [x] `LayerStack`: 色調補正レイヤーの1枚制限・通常レイヤーとの独立カウント
- [x] `LayerStack`: レイヤー結合（選択レイヤーを1枚に統合した結果の検証）
- [x] `PaintCommandHistory`: Undo/Redo ステップ管理（最大50ステップ・境界値）
- [x] `TransparentPixelProcessor`: α < 128 → α=0・RGB 黒固定 / α ≥ 128 → α=255 の変換

### Unityクライアント — ワールドオブジェクトペイントUI
> **Phase 12 に移管。** キャンバス単体は今作れるが、保存処理（Phase 9 API 待ち）・ワールドエディタからの遷移（Phase 12 待ち）が揃わないと動作確認できないため、ワールドエディタと一緒に実装する。

- [ ] オブジェクト種別のテクスチャサイズに合わせたキャンバス表示・編集（16〜512px・区分なし）
- [ ] ワールドスコープカスタマイズの保存処理（そのワールドのみに適用）
  - [ ] 変更済みレイヤー画像のみアップロード
  - [ ] レイヤー構造JSONアップロード
  - [ ] 統合画像アップロード（ワールド定義の worldObjectCustomizations に格納）
- [ ] 「保存バリアントに保存」処理
  - [ ] スロット上限チェック（通常10 / プレミアム100）
  - [ ] バリアント名入力 → サーバーに保存（`POST /me/worldobjects/variants`）
- [ ] ワールドエディタからの遷移（オブジェクトパレットまたは配置済みオブジェクト選択時）
- [ ] UV オーバーレイ: `MeshFilter.sharedMesh` から UV をベイクして渡す（`UvOverlayBaker` 使用）

---

## Phase 5A — コア API 統合

**目標**: `docker compose up` で立ち上げたローカルAPIとUnityが連携できる状態。プレイヤーがログインしてワールドに参加し動ける状態を達成する

### 前提タスク（非技術・GDPR P0）

> コーディング開始前に完了している必要がある。`Constants.cs` の URL 定数が空のままでは Phase 5A の同意 UI が完成しない。

- [ ] PP 執筆前調査: Vivox SDK・Unity Gaming Services（Relay）が収集・転送するデータの内容を各サービスのドキュメント / DPA で確認する（「第三者サービス」セクションの記載根拠になる）
  - **Vivox 音声録音ポリシーの独自確認（security-todo Low 項目）**: 「音声は録音・保存されない」ことを Vivox 公式ドキュメントで確認し、根拠 URL を `gdpr-todo.md §10` に記録する。確認後にプライバシーポリシーへ「音声は保存・自動分析されない」旨を明記する
- [ ] プライバシーポリシー本文を作成・公開 URL を確定する
  - 必須記載: 取得情報・利用目的・第三者提供（Google Cloud / Cloudflare / Unity Gaming Services / Resend）・保存期間・国際データ転送根拠（SCC）・音声は保存・自動分析されない旨
  - GDPR データ請求窓口の明記: 「アクセス権・移転・削除等の請求は nibankougen@gmail.com まで。30 日以内に対応します」
  - 訂正権（Art. 16）の明記: 表示名・@name は設定画面から変更可能。それ以外の訂正はメール窓口で受け付ける旨を記載
  - 同意撤回権（Art. 7）の明記: アカウント削除 = 同意撤回として機能する旨・通知設定からプッシュ通知をオプトアウトできる旨を記載
- [ ] 利用規約本文を作成・公開 URL を確定する
- [ ] `Constants.cs` の `TermsOfServiceUrl`・`PrivacyPolicyUrl` を確定 URL に更新する（仮 URL のままリリース不可）
- [ ] App Store Connect・Google Play Console のプライバシーポリシー欄に登録する
- [ ] **App Store Privacy Nutrition Label**（App Store Connect → App Privacy）を正確に入力する
  - 収集するデータ種別: メールアドレス・ユーザー ID（プロバイダー ID）・購入履歴・デバイス ID（プッシュトークン）・ユーザーコンテンツ（UGC）・使用状況データ（IP・UA）
  - 「データとデバイスのリンク」: あり（アカウントに紐付く）
  - 「トラッキング」: なし（Unity Analytics / Ads を無効化済み）
  - 入力内容は `privacy-policy-elements.md §3・§6` を参照
- [ ] **Google Play データセーフティセクション**（Play Console → ストアの掲載情報 → データセーフティ）を正確に入力する
  - 収集・共有するデータ: 上記 Nutrition Label と同じカテゴリ
  - データ暗号化: あり（転送中 TLS）
  - 削除リクエスト対応: あり（アプリ内設定から実行可能）
  - 入力内容は `privacy-policy-elements.md §3・§6` を参照
- [ ] App Store Connect・Google Play Console の **配信地域から EU/EEA 30 か国を除外**する（GDPR 適用対象外とするため）
  - 除外対象: オーストリア・ベルギー・ブルガリア・クロアチア・キプロス・チェコ・デンマーク・エストニア・フィンランド・フランス・ドイツ・ギリシャ・ハンガリー・アイルランド・イタリア・ラトビア・リトアニア・ルクセンブルク・マルタ・オランダ・ポーランド・ポルトガル・ルーマニア・スロバキア・スロベニア・スペイン・スウェーデン（EU 27 か国）＋ アイスランド・リヒテンシュタイン・ノルウェー（EEA 追加 3 か国）
  - 日本・米国・その他の地域は通常配信
  - **EU 配信解禁チェックリスト**（以下が揃ったタイミングで除外を解除する）:
    - [ ] GDPR Art. 27 EU 代理人を指定する（EU 拠点を持つ代理人サービスを契約）
    - [ ] Google Cloud との SCC（Standard Contractual Clauses）締結確認
    - [ ] Cloudflare GDPR Data Processing Addendum の確認・署名
    - [ ] Unity Gaming Services GDPR Compliance Agreement の確認
    - [ ] プライバシーポリシーに「EU 代理人の連絡先」「国際データ転送根拠（SCC）」を追記
    - [ ] `breach-notification-plan.md §5` を選択肢 B（PPC + EU DPA）へ移行
    - [ ] App Store Connect・Google Play Console の配信地域に EU/EEA を追加
- [ ] カスタムドメインのメール受信設定（Cloudflare Email Routing + Gmail）
  - Cloudflare ダッシュボード → Email → Email Routing を有効化
  - 受信アドレスを作成してルーティングを設定（MX レコードは Cloudflare が自動追加）
    - `support@yourdomain.com` → `nibankougen@gmail.com`（GDPR 請求・お問い合わせ窓口）
    - 他の用途（将来のサポート自動返信等）が増えた際もここに追加する
  - Gmail から `support@yourdomain.com` 差出人で返信できるよう「Send mail as」を設定
    - Gmail 設定 → アカウント → 「他のメールアドレスを追加」→ SMTP サーバーに Resend を指定
    - Resend SMTP: ホスト `smtp.resend.com`・ポート `587`・ユーザー名 `resend`・パスワード: Resend API キー
  - PP の GDPR 請求窓口を `nibankougen@gmail.com` から `support@yourdomain.com` に更新する

### 開発環境設定
- [x] Unityに接続先URL設定（`ScriptableObject` またはシンボリック設定ファイルで切り替え）
  - 開発: `http://localhost:8080`
  - 本番: 環境変数で注入

### `api/`（Go）

#### クライアント向けAPI
- [x] API バージョンエンドポイント（`GET /api/version`）— `{ "min_compatible_version": <int>, "latest_version": <int> }` を返す。認証不要
- [x] 認証エンドポイント — ソーシャルサインイン（Google / Apple）OAuth コールバック処理・JWT 発行
  - `POST /auth/google/callback`・`POST /auth/apple/callback`
  - 認証プロバイダーを抽象化した設計（将来のメール・SMS 認証追加に対応）
  - 初回サインイン時は自動でアカウント作成（@name は未設定状態で作成し、セットアップ画面へ誘導）
- [ ] 年齢確認・保護者同意フロー（GDPR Art. 8・P0。設計詳細: `api-abstract.md` §4・`screens-and-modes.md` §1.5.6）
  - [ ] Resend セットアップ（特別設定あり）
    - Resend アカウント作成・API キー発行・環境変数 `RESEND_API_KEY` を設定
    - **送信ドメインの DNS 認証が必須**: Resend ダッシュボード → Domains → Add Domain で DKIM レコード（CNAME 2〜3件）を取得し DNS に追加。未設定のままでは Gmail / Yahoo 等でスパム判定されリマインドメールが届かないリスクがある
    - SPF レコード追加: Resend ダッシュボードの指示に従って `TXT` レコードを DNS に追加
    - DMARC レコード追加（推奨）: `v=DMARC1; p=none; rua=mailto:nibankougen@gmail.com` を DNS に追加（監視モードから開始し、将来 `p=quarantine` へ強化）
    - 抑制リスト: ハードバウンス・迷惑メール報告は Resend が自動管理するため自前 DB への保持は不要
  - [x] `active_users` に `parental_email VARCHAR` カラムを追加（保護者同意フロー中のみ保持。検証完了または 14 日タイムアウト時に NULL 化）
  - [x] `active_users` に `age_group VARCHAR` カラムを追加（値: `adult` / `teen_13_15` / `child_under_13`）
  - [ ] `parental_consents` テーブル実装（監査証跡・5 年保持・`users.id` 参照）
    - `parental_email_hash VARCHAR` — `SHA-256(parental_email)` のみ保持（平文は持たない）
    - `email_sent_at TIMESTAMPTZ`・`reminder_sent_at TIMESTAMPTZ`・`verified_at TIMESTAMPTZ`・`expired_at TIMESTAMPTZ`
  - [ ] サインイン時の年齢入力処理
    - 生年月日を受け取り `age_group` を計算・保存。生年月日自体は保存しない（データ最小化）
    - 13 歳未満 → 即時拒否（アカウント作成不可）
    - 13〜15 歳 → 保護者同意フローへ進む（JWT は発行するがアクセスを保護者同意待ち状態でブロック）
    - 16 歳以上 → 通常フロー
  - [ ] 保護者同意メール送信処理（Resend API 経由）
    - 確認メール送信（検証リンク付き・有効期限 14 日）
    - `active_users.parental_email` に平文アドレスを一時保存
    - `parental_consents` に `parental_email_hash`・`email_sent_at` を記録
    - Cloud Tasks: 7 日後に Day 7 リマインドジョブ・14 日後にタイムアウトジョブを予約（サインアップ時点で予約）
  - [ ] 保護者検証リンク処理（`GET /auth/parental-consent/verify?token=...`）
    - トークン検証 → `parental_consents.verified_at` を記録
    - `active_users.parental_email` を NULL 化（平文削除）・ユーザーのブロック解除
  - [ ] Day 7 リマインドジョブ: 未検証の場合のみ Resend でリマインドメール送信・`parental_consents.reminder_sent_at` を記録
  - [ ] 14 日タイムアウトジョブ: 未検証の場合 → `active_users.parental_email` を NULL 化・`parental_consents.expired_at` を記録・アカウントを削除状態へ移行
  - [ ] `POST /admin/users/{id}/delete-underage`: 13 歳未満誤登録時の即時物理削除（2 段階確認・管理監査ログ記録）
- [x] @name 設定・更新エンドポイント（`PUT /me/name`・初回設定は全ユーザー可・変更はプレミアム会員のみ・90 日制限チェック）
- [x] プロバイダー連携管理エンドポイント（`GET /me/auth-providers`・`POST /me/auth-providers`・`DELETE /me/auth-providers/{provider}`・最低 1 プロバイダー維持の制約）
- [x] アカウント削除エンドポイント（`DELETE /me`・`active_users.deleted_at` を設定してソフトデリート・セッション全無効化・公開ワールドを非公開へ変更・`vivox_id` を `gen_random_uuid()` で再生成）
  - [ ] **削除ユーザー ID の公開 API 漏洩防止確認**（`api-abstract.md §13` 準拠・ソフトデリート後 30 日間の確認）
    - `deleted_at IS NOT NULL` のユーザーを参照しうる全パブリックエンドポイントで 404 を返すことを実装・テストで確認する
    - 対象エンドポイント（`deleted_at IS NOT NULL` チェックが必要）:
      - `GET /api/v1/users/{id}` — ユーザープロフィール
      - `GET /api/v1/users/{id}/avatars` — ユーザーのアバター一覧
      - `GET /api/v1/users/{id}/worlds` — ユーザーのワールド一覧（実装時に追加）
      - フレンド申請・フォロー・違反報告など対象ユーザーを指定する全操作
    - 実装方針: 認証済みエンドポイントは認証ミドルウェアで `deleted_at IS NOT NULL` → 403 を一元返却済み。ユーザー ID をパスパラメータに取る公開エンドポイントは個別に 404 を返す処理を追加する
    - テスト: 削除済みユーザー ID で上記エンドポイントを叩いて 404 が返ることを Go 統合テストで確認する
- [x] アカウント削除バッチ処理（`active_users.deleted_at` から 30 日後に `active_users` レコードを物理削除・関連アップロードリソースも物理削除。`users` レコードおよび財務・監査データは `users.id` への参照を維持したまま保持。匿名化処理は不要）
  - 本番: Cloud Scheduler（毎日 JST 03:00）→ `POST /admin/internal/run-batch/delete-expired-accounts`
  - 失敗ポリシー（`api-abstract.md §13` 全バッチ共通ポリシー準拠）:
    - Cloud Scheduler リトライ設定: 最大 3 回・30 分間隔（コンソール設定）
    - 正常完了時に `{"event": "batch_completed", "batch": "delete-expired-accounts", "affected_count": N}` 構造化ログを出力
    - 3 回失敗後: Cloud Monitoring → Pub/Sub → Discord 通知（Phase 5B モニタリング設定で追加）
- [x] アクセスログ個人情報削除バッチ（GDPR 保持期間対応・`api-abstract.md` §13）: DB 上の IP アドレス・User-Agent カラムを記録から 1 年後に NULL 化する。Cloud Logging へのリアルタイム転送済みのため外部保全は完了している
  - 本番: Cloud Scheduler（毎日 JST 03:30）→ `POST /admin/internal/run-batch/cleanup-access-logs`（内部エンドポイント・認証必須）
  - 開発: `docker compose run api go run cmd/batch/main.go cleanup-access-logs` で手動実行
  - 失敗ポリシー: 共通ポリシー準拠（Cloud Scheduler 3 回リトライ → Discord 通知）
  - 正常完了時に `{"event": "batch_completed", "batch": "cleanup-access-logs", "affected_count": N}` 構造化ログを出力
- [x] アカウント復元エンドポイント（管理画面用・`PATCH /admin/users/{id}/restore`・`active_users.deleted_at` をクリア・`active_users` レコードが存在する 30 日以内のみ有効）
- [x] VRMアップロードエンドポイント（`POST /api/v1/me/avatars`・optimizer なしでローカルストレージに直接保存・Phase 5 polish で optimizer 統合）
- [x] アバター取得・一覧エンドポイント（`GET /api/v1/me/avatars`・VRM/テクスチャ URL + hash を返す）
- [x] アバターテクスチャ更新エンドポイント（`PUT /api/v1/me/avatars/{id}/texture`）
- [x] アバター削除エンドポイント（`DELETE /api/v1/me/avatars/{id}`）
- [x] アクセサリ一覧・アップロード・テクスチャ更新・削除エンドポイント（`GET/POST /api/v1/me/accessories`・`PUT /api/v1/me/accessories/{id}/texture`・`DELETE /api/v1/me/accessories/{id}`）
- [x] ワールドデータ取得エンドポイント（`GET /api/v1/worlds/{id}`・GLB URL を返す）
- [x] アセットメタデータへの SHA-256 ハッシュフィールド追加（アバター VRM・テクスチャ・アクセサリ GLB のハッシュをレスポンスに含める）
- [ ] ワールドオブジェクト保存バリアント管理エンドポイント（`GET/POST /me/worldobjects/variants`・`DELETE /me/worldobjects/variants/{id}`・レスポンスに SHA-256 ハッシュフィールドを含める）
- [ ] ワールドスコープカスタマイズのレイヤー画像・統合画像の保存エンドポイント（ワールド定義 JSON の `worldObjectCustomizations` と連動）
- [x] ワールド一覧取得エンドポイント（4 タブ対応）
  - `GET /worlds/new` — 新着順（全公開ワールド・新着順）
  - `GET /worlds/liked` — 自分がいいねしたワールド一覧（いいねした日時降順）
  - `GET /worlds/following` — フォロー中ユーザーのワールド一覧（新着順）
  - ホームタブ向けは Phase 5 では暫定的に `/worlds/new` を流用（パーソナライズフィード本実装は Phase 14）
  - レスポンス共通フィールド: サムネイル・ワールド名・総プレイヤー数・タグ・いいね数
  - ページネーション: ID ベースカーソル方式（`?after=<last_world_id>&limit=<n>`）— オフセット方式（`?page=&limit=`）は使用しない
- [x] `world_likes` テーブル実装（world_id / user_id / UNIQUE 制約）・`worlds.likes_count` デノームカラム追加（`CREATE INDEX idx_worlds_likes_count ON worlds(likes_count DESC)`）
- [x] `POST /worlds/{id}/like` / `DELETE /worlds/{id}/like` エンドポイント（自己いいね禁止 403・重複いいね 409・likes_count インクリメント/デクリメント）
- [x] `worlds` テーブルに `max_players INTEGER NOT NULL DEFAULT 6` カラムを追加
- [x] ルーム一覧取得エンドポイント（ワールド別・公開/フレンドのみ種別・参加人数・ワールドの `maxPlayers`（人数上限）・言語）
  - [x] 公開ルームをビューワーの言語で優先ソート（言語一致を上位・不一致を下位に配置。新しい順/人気順はグループ内で維持）
- [x] ルーム参加時の人数上限チェック: 現在の参加人数 ≥ `room.max_players` の場合は参加を拒否（409 Conflict）。`POST /api/v1/rooms/{roomID}/join`・`POST /worlds/{worldID}/rooms/recommended-join` の両方で CTE で原子チェック
- [x] ワールド/ルーム検索エンドポイント（ワールドID・ルームID・ワールド名・タグで検索）
  - [x] `pg_trgm` 拡張を有効化し `worlds.name` に GIN インデックスを作成（ワールド名の部分一致検索）
  - [x] `world_tags` テーブル実装（world_id・tag_text・tag_normalized）および AND 検索クエリ
  - [x] タグ正規化処理（小文字化・全角半角統一・前後トリム・Unicode NFC）をサーバー側で実施
  - [x] タグ BAN リストテーブル実装・登録/取得時の除外処理
- [x] ルーム作成エンドポイント（公開 / フレンドのみ / フォロワー限定 種別選択）
  - [x] 公開ルーム作成時: 言語コードを `language` フィールドに保存（未指定時は作成者のアカウント言語設定を自動適用）
  - [x] ルーム内からの言語変更エンドポイント（`PATCH /rooms/{id}/language`・作成者のみ変更可）
  - [x] `rooms` テーブルに `max_players INTEGER NOT NULL` カラムを追加
  - [x] ルーム作成時に `room.max_players` を自動計算して設定（通常ユーザー: `min(6, world.max_players)`・プレミアム: `world.max_players`）
- [x] 推奨公開ルーム参加エンドポイント（`POST /worlds/{id}/rooms/recommended-join`）
  - [x] ステップ1: 自言語の公開ルームで 1人以上・空きあり → 最多人数ルームに参加
  - [x] ステップ2: 自言語ルームが満席のみ存在 → 自言語で新規ルーム作成・参加
  - [x] ステップ3: 英語ルームで 1人以上・空きあり → 最多人数を返却（クライアントが言語不一致時はモーダル表示後に確定 API を呼ぶ）
  - [x] ステップ4: いずれも該当なし → 自言語で新規ルーム作成・参加
  - [x] レスポンス: `{ action: "join" | "create" | "confirm_english", roomId?, language? }` を返し、クライアントが次アクションを決定する
- [ ] フレンドが参加中のルーム一覧取得エンドポイント（`GET /me/friends/rooms`・全ワールド横断・フレンドのいるルームを返す）
- [x] 言語設定の保存エンドポイント（`PATCH /api/v1/me/language`）・表示名更新（`PATCH /api/v1/me/display-name`）
- [x] HTTP転送時圧縮（`compress/gzip` ミドルウェアを全エンドポイントに適用・`Content-Encoding: gzip`）
- [x] スタートアップ一括取得エンドポイント（`GET /startup`・1リクエストでユーザー設定・言語設定・アバター一覧＋URL・ワールド一覧先頭ページを返す）
- [x] 全リスト系 API へのページネーション実装（ワールド一覧・フレンド一覧・購入履歴・コイン取引履歴等）
  - ワールド一覧: ID ベースカーソル方式（`?after=<last_world_id>&limit=<n>`）
  - その他リスト: ID ベースカーソル方式を基本とし、用途に応じて選択
- [x] コンテンツアドレス型ファイル URL（ファイル名 = SHA-256 ハッシュ.ext）+ CDN 配信設定（`Cache-Control: immutable`）
- [x] サーバーサイドキャッシュ（Redis）導入: ワールド一覧・ショップ商品一覧など頻繁読み取り・低変更頻度のエンドポイントに適用
- [x] DB スキーマ基盤（`docs/unity-game-abstract.md` セクション 22・23 参照）
  - [x] `users` / `active_users` テーブル設計
    - `users`（不変レコード）: `id`（PK）・`created_at` のみを持つ。購入履歴・消費履歴・違反報告・モデレーションログなど財務・監査レコードの外部キーは `users.id` を参照することで、アカウント削除後も参照整合性を維持する
    - `active_users`（個人情報レコード）: `user_id`（PK・`users.id` への FK・1対1）を持ち、表示名・@name・ソーシャルプロバイダー紐づけ・言語設定・信頼スコア・削除タイムスタンプなど個人情報と設定を格納する。アカウント削除時にこのレコードを削除することで個人データを消去できる
  - [x] `active_users` テーブルに以下のカラムを追加:
    - `trust_level` VARCHAR DEFAULT `'visitor'`
    - `trust_points` FLOAT DEFAULT `0`
    - `trust_level_locked` BOOL DEFAULT `false`
    - `is_restricted` BOOL DEFAULT `false`
    - `deleted_at` TIMESTAMPTZ DEFAULT `NULL`
    - `subscription_tier` VARCHAR NOT NULL DEFAULT `'free'` — プランティア。現行値: `'free'` / `'premium'`。将来の多段階プラン追加を考慮し `is_premium BOOL` は使用しない（設計詳細: `docs/unity-game-abstract.md` セクション 23）
    - `subscription_expires_at` TIMESTAMPTZ DEFAULT `NULL` — サブスクリプション有効期限。NULL = 無料プラン
    - `last_name_change_at` TIMESTAMPTZ DEFAULT `NULL` — @name の最終変更日時。NULL = まだ変更していない。初回セットアップ（@name 設定画面での確定）時と、プレミアム会員による変更時に `now()` で更新する
    - `vivox_id` UUID NOT NULL DEFAULT `gen_random_uuid()` — Vivox ログイン用仮名 ID。実ユーザー UUID の代わりに渡す。アカウント削除（ソフトデリート）時に再生成する（`api-abstract.md §4` 参照）
  （トラストレベル変更ロジック・昇格ジョブ・関連テーブル・管理画面は Phase 5B で実装）

### `optimizer/`（Docker）
- [ ] アップロードされたVRMから不要データを削除（UniVRM仕様準拠）
- [ ] 容量・ポリゴン・ボーン数の検証（500KB / 512tris / 50bones）
- [ ] 最適化済みVRMを保存・返却

### Unityクライアント
- [x] `AppConfig` ScriptableObject 作成（`ApiBaseUrl`・`TermsOfServiceUrl`・`PrivacyPolicyUrl` フィールド。`Assets/Scripts/API/AppConfig.cs`）
- [x] `ApiTypes.cs` — API レスポンス DTO 定義（`Assets/Scripts/API/ApiTypes.cs`）
- [x] `ApiClient.cs` — HTTP クライアント（GET/POST/PUT/PATCH/DELETE・認証ヘッダー自動付与・`Assets/Scripts/API/ApiClient.cs`）
- [x] `UserManager.cs` — DontDestroyOnLoad JWT/リフレッシュトークン管理・プロフィール保持（`Assets/Scripts/API/UserManager.cs`）
- [x] `CacheManager.cs` — DontDestroyOnLoad ハッシュベースアセットキャッシュ（own=永続/others=TTL7日・`Assets/Scripts/API/CacheManager.cs`）
- [x] `Bootstrapper.cs` — HomeScene 起動時の DontDestroyOnLoad マネージャー一元初期化（`Assets/Scripts/Core/Bootstrapper.cs`）
- [x] タイトル画面実装（`TitleScreen.uxml/uss` + `TitleScreenController.cs`）
  - [x] 起動時 API バージョン互換性チェック（`GET /api/version`）
  - [x] 非互換時: アップデート促進モーダル表示（iOS: App Store / Android: Google Play への誘導ボタン・閉じ不可）
  - [x] 互換確認後: ローカルトークンの有無で自動ログインとアカウント作成モーダルを分岐
  - [x] 自動ログイン: ローディングインジケーター表示 → リフレッシュ → `/startup` → ホームへ遷移
  - [x] 自動ログイン失敗時: エラーメッセージ ＋ 再試行ボタン表示
  - [x] アカウント作成モーダル: 利用規約・プライバシーポリシー同意チェックボックス（両方 ON でサインインボタン有効化）・外部リンクをブラウザで開く
  - [x] アカウント作成モーダル: Google / Apple ソーシャルサインインボタン（縦並び）
  - [x] サインイン後分岐: 既存アカウント → ホーム遷移 / 新規 → @name 設定画面へ
- [x] 初回セットアップ画面（@name 入力・バリデーション・`PUT /api/v1/me/name`）— タイトル画面内モーダルとして実装
- [x] HTTP経由でのアバター一覧取得・ダウンロード（`/startup` レスポンスから取得→ `CacheManager` 経由でローカルキャッシュ・先頭アバターを自動選択して `UserManager.SelectedAvatarLocalPath` にセット）
- [ ] VRMアップロードUI（ネイティブファイルピッカー対応が必要なため Phase 2 に移管）
- [x] ワールドデータ取得・`WorldLoader` 本番版実装（`WorldSessionData` → `CacheManager` 経由でダウンロード → `WorldLoader` に渡す）
- [x] ワールド一覧画面（`WorldTab.uxml/uss` + `WorldListController.cs` — `HomeScreenController` から呼び出し）
  - [x] 4 タブ構成（ホーム / フォロー中 / 新着順 / いいね）
  - [x] ホームタブ: 暫定的に新着順を表示（パーソナライズフィード本実装は Phase 14）・ワールド検索バー
  - [x] フォロー中タブ: スタブ（フォローシステムは後フェーズ）
  - [x] 新着順タブ: 全公開ワールド（新着順・カーソルページネーション）
  - [x] いいねタブ: 自分がいいねしたワールド（いいねした日時降順）
  - [x] ワールドカード・無限スクロール（ID ベースカーソルページネーション）・サムネイル非同期読み込み
- [x] ワールド詳細・ルーム参加画面（`WorldDetail.uxml/uss` + `WorldDetailController.cs`）
  - [x] 「公開ルームに参加」ボタン → 推奨ルーム参加 API 呼び出し → ローディング表示
    - [x] レスポンス `action: "join"` → 指定ルームへ参加（`POST /api/v1/rooms/{roomID}/join`）
    - [x] レスポンス `action: "create"` → 新規ルーム作成後参加
    - [x] レスポンス `action: "confirm_english"` → 英語ルームのみ確認モーダル表示 → OK で参加確定 API を呼ぶ / キャンセルで新規ルーム作成
  - [x] 「フレンドのみのルームを作成」ボタン → ルーム作成 API（種別: friends_only）呼び出し
  - [x] 「その他のルームを見る」ボタン → その他ルーム画面へ遷移
- [x] その他ルーム画面（`RoomList.uxml/uss` + `RoomListController.cs`）
- [x] 起動時にサーバーから言語設定を取得し `LocalizationSettings.SelectedLocale` に反映

### テスト（EditMode）
- [x] `AssetCacheStore`: ハッシュ一致 → キャッシュヒット / 不一致 → 再ダウンロード判定
- [x] `AssetCacheStore`: TTL 期限切れエントリの検出・削除
- [x] `AssetCacheStore`: 自分のアセット（永続）と他人のアセット（一時）の保存先分岐
- [x] `LikeLogic`（World）: 自己いいね禁止判定・重複いいね禁止判定・いいね解除の状態遷移

---

## Phase 5B — 管理基盤・トラストレベル

**目標**: 管理画面でユーザー・ワールド・ショップ・売上を管理できる状態。トラストレベルシステムが稼働し、不正ユーザーへの自動制限が機能する状態

**前提**: Phase 5A 完了

### `api/`（Go）

#### トラストレベルシステム（`docs/unity-game-abstract.md` セクション 22 参照）
- [x] `trust_level_logs` テーブル実装（変更日時・変更前後レベル・理由・操作者 admin_id・`user_id` は `users.id` を参照）
- [x] `room_trust_events` テーブル実装（公開ルーム退室時の join_count / exit_count / duration_minutes を記録・`user_id` は `users.id` を参照）
- [x] トラストポイント加算処理（公開ルーム退室時: `floor((join+exit)/2 * floor(minutes))` を `trust_points` に加算）
- [ ] トラストレベル昇格非同期ジョブ（**asynq** を使用・イベント駆動。トラストポイント更新・フレンド数変動・ワールドいいね変動・課金完了・プレミアム変更 の各イベント後にジョブをエンキューして実行。全条件を評価し最上位レベルを設定。ロック中はスキップ）※現在はゴルーチンで同期実行・asynq 移行は Phase 8/9 イベント追加時に対応
- [x] `user_violation_reports` テーブル実装（違反報告の保存・`reporter_id` と `target_id` は `users.id` を参照）
- [x] 違反報告の自動制限トリガー（`visitor`: 24h以内2件以上 または 累計4件以上で自動制限。`new_user`: 3件/10件）

#### 管理者操作監査ログ
- [x] `admin_audit_logs` テーブル実装（詳細: `docs/api-abstract.md` セクション9参照）
- [x] すべての管理者操作エンドポイントで `admin_audit_logs` に自動記録するミドルウェア実装

#### 管理画面（`/admin`）
- [x] 管理者ログイン認証（セッションまたはJWT）
- [x] ショップ商品登録UI（アバター・アクセサリのGLB・テクスチャ・価格・Edit OK/NGフラグ）— `POST/GET/PATCH /admin/products`
- [x] ショップクリエイター登録UI（クリエイター情報・ユーザーアカウント紐づけ）— `POST/GET/PATCH /admin/creators`
- [x] アバター審査画面（承認/拒否操作）— `GET /admin/avatars`・`PATCH /admin/avatars/{id}/moderation`
  - 検疫キュー（`moderation_status = 'pending'`）のアバターを一覧表示（status クエリパラメータで切り替え）
  - CSAM アラート（`admin_alerts`）を優先表示・IHC 通報ガイダンスを管理画面内に記載
  - [ ] 3Dビューアコンポーネント（`@pixiv/three-vrm` + `THREE.OrbitControls`・クリック時 lazy load）
  - [ ] optimizer による前面/背面スクリーンショット生成（審査用・保存してAPIから配信）
- [x] ユーザー管理 API（一覧・@name検索・BAN/BAN解除・制限・制限解除・トラストレベル変更）
  - [x] 削除申請済みアカウントの表示（「削除予定 YYYY-MM-DD」バッジ）・30 日以内の復元操作（`PATCH /admin/users/{id}/restore`）
  - [x] トラストレベル手動変更・ロック操作・制限解除・変更履歴表示・ユーザー一覧のトラストレベルフィルター
  - [x] 個人データエクスポート機能（GDPR Art. 15 / 20 メール対応支援）
    - API: `GET /admin/users/{id}/data-export` → アカウント情報・購入/消費履歴・フォロー/フレンド/非表示リスト・アバター/ワールドメタデータ・CDN URL を JSON で返却
    - 管理画面 UI: ユーザー詳細画面に「個人データを JSON でダウンロード」ボタン → ブラウザの `<a download>` でファイル保存
    - ユーザーからの請求メール受信後、このボタンで JSON を生成して返送する運用フロー
- [x] 違反報告管理 API（報告一覧・target_id フィルター・カーソルページネーション）
- [x] 売上管理（期間別確定売上登録・調整係数自動計算）— `POST/GET /admin/settled-revenues`（`docs/coins.md` セクション14.5参照）
- [x] ワールド管理 API（一覧・名前検索・有効/無効切り替え）
- [ ] 取引キャンセル管理画面（一覧・絞り込み・手動キャンセル操作）（`docs/coins.md` セクション17参照）
- [x] 管理者操作ログ API（`admin_audit_logs` の一覧・管理者名/操作種別/対象で絞り込み・`docs/api-abstract.md` セクション9参照）

#### 本番環境モニタリング設定（侵害検知アラート）

詳細仕様: `infra-abstract.md §10`・`breach-notification-plan.md §9`

- [ ] Pub/Sub トピック `lowpolyworld-security-alerts` を作成する
- [ ] Discord Webhook URL を Secret Manager に登録する（キー名: `DISCORD_WEBHOOK_URL`）
- [ ] `discord-notifier` Cloud Functions（Go）を実装・デプロイする
  - Pub/Sub トリガー → Cloud Monitoring の JSON ペイロードを Discord Embed 形式に変換 → Webhook POST
- [ ] **レイヤー 1** Cloud Monitoring メトリクスアラートを設定する（コンソール操作のみ・コード変更なし）
  - `high-5xx-error-rate`: Cloud Run 5xx エラーレート 5 分間 5% 超
  - `high-sql-connections`: Cloud SQL 接続数 80% 超（5 分間）
  - `high-sql-cpu`: Cloud SQL CPU 90% 超（5 分間）
  - `api-uptime-check`: 既存アップタイムチェックを Pub/Sub 通知チャンネルに追加
- [ ] **レイヤー 2** Cloud Logging ログベースアラートを設定する（コンソール操作のみ）
  - `mass-admin-delete`: `admin_audit_logs` で 1 時間以内に 100 件超の `delete_*` アクション
  - `after-hours-admin-op`: 23:00〜7:00 JST に管理者操作が 1 件でも発生
- [ ] **レイヤー 3** Go API 構造化ログを実装し Cloud Logging アラートを設定する（コード実装あり）
  - 認証ミドルウェア: レートリミット発動時に `brute_force_attempt` ログを出力
  - トークン無効化処理: `mass_token_revocation` ログを出力（1 時間 100 ユーザー超でアラート）
  - レートリミットミドルウェア: `high_api_rate` ログを出力（同一ユーザー 500 req/分超）
  - 上記 3 種を Cloud Logging ログベースアラートに設定し Pub/Sub トピックへ接続
  - 上記 3 種を `system_alerts` テーブルにも挿入（管理画面に表示）
- [ ] 全アラートポリシーに `lowpolyworld-security-alerts` Pub/Sub トピックを Notification Channel として追加する
- [ ] **バッチ失敗監視** Cloud Scheduler 失敗メトリクスアラートを設定する（コンソール操作のみ）
  - 対象: アカウント物理削除・アクセスログ削除・通報者匿名化（全 3 バッチ）
  - 条件: `cloudscheduler.googleapis.com/job/last_attempt_result = failed`（3 回リトライ後）
  - 通知: `lowpolyworld-security-alerts` Pub/Sub → Discord（既存パイプライン利用）
- [ ] **バッチ完了ログ監視** Cloud Logging ログベースアラートを設定する（コンソール操作のみ）
  - 条件: 各バッチの実行予定時刻から 26 時間以内に `batch_completed` ログが出力されない場合
  - 通知: 同上（部分失敗・HTTP 200 だが処理 0 件などのサイレント失敗を検知）
- [ ] **フォールバックログのディスク保護** Go コードに 2 段階閾値チェックを実装する（`infra-abstract.md §9` 参照）
  - 書き込みのたびにファイルサイズを確認する
  - 40MB 到達（警告）: 書き込み継続 + `system_alerts` 記録（`audit_fallback_log_warning`）+ `lowpolyworld-security-alerts` Pub/Sub パブリッシュ → Discord 通知
  - 50MB 到達（停止）: 新規書き込み停止 + `system_alerts` 記録（`audit_fallback_log_full`）+ Pub/Sub → Discord 通知（法的リスクあり・即時対応要求）
  - DB への `admin_audit_logs` 書き込みは停止しない（監査記録自体は継続）

### テスト（EditMode）
- [x] `TrustPointCalculator`: 公開ルーム退室時のポイント計算（`floor((join+exit)/2 * floor(minutes))`）
- [x] `TrustLevelPromoter`: 全条件評価・最上位レベル設定・ロック中スキップ

---

## Phase 6 — 音声通信

**目標**: ワールド内で3D位置音声が動作する状態

### Unityクライアント
- [x] Vivox SDK 初期化・ログイン（`/startup` レスポンスの `vivoxId` を Vivox ユーザー識別子として使用する。実ユーザー UUID は渡さない）
- [x] ワールド単位チャンネルの入退室管理
- [x] プレイヤー座標をVivox SDKへ通知
- [x] 距離減衰パラメータ調整
- [x] 通話音声スライダーの音量値を Vivox SDK 受話音量に反映（`WorldSettingsLogic.OnVoiceVolumeChanged` → Vivox SDK API）

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

## Phase 8 — ショップ・コインシステム

**目標**: コインでアバター・アクセサリ・ワールドオブジェクト・スタンプを購入できる状態
※ Phase 5B（管理画面・ショップ商品登録 UI）が完成していることが前提

### `api/`（Go）

**DBスキーマ**
- [x] `products.recent_purchase_count` カラム追加（人気順ソート用キャッシュ）
  - 購入時にインクリメント、48時間バッチで期限切れ購入をデクリメント/リセット
  - `CREATE INDEX idx_products_popularity ON products(recent_purchase_count DESC) WHERE recent_purchase_count >= 3`
- [x] `product_likes` テーブル実装（product_id / user_id / UNIQUE 制約）・`products.likes_count` デノームカラム追加
- [x] スタンプ商品カテゴリ対応（`products.category` に `stamp` 追加）

**商品 API**
- [x] ショップ商品一覧取得エンドポイント（カテゴリ別・ソート4種・名前/タグ検索・IDベースカーソルページネーション）
  - ソート: `popularity`（`recent_purchase_count >= 3` の降順）/ `likes` / `newest` / `oldest`
  - フィルター: オブジェクトのみ `texture_cost` 範囲・`collider_size_category`（small/medium/large）
- [x] 商品詳細取得エンドポイント（Edit OK/NGフラグ・いいね数・自分がいいね済みかフラグ含む）
- [x] `POST /shop/products/{id}/like` / `DELETE /shop/products/{id}/like` エンドポイント（自己いいね禁止 403・重複いいね 409・likes_count インクリメント/デクリメント）
- [x] 商品購入エンドポイント（コイン消費記録・購入一覧への追加・残高 < 0 時は 402 を返す）
- [x] 購入済み商品一覧取得エンドポイント
- [x] クリエイター情報取得エンドポイント

**コイン・課金 API**
- [x] コイン購入記録エンドポイント（`docs/coins.md` セクション4準拠）
- [x] コイン残高取得エンドポイント（ロット別有効期限付き）
- [ ] コイン有効期限チェック・失効処理（購入から6ヶ月）
- [x] App Store Server Notifications V2 受信エンドポイント（`POST /webhook/apple`）
  - [ ] JWS 署名検証（Apple 公開鍵）（TODO: 非同期ワーカーで実装）
  - [ ] `REFUND` イベント処理（購入特定 → 有効期限確認 → 残高調整 → キャンセル記録）（TODO: 非同期ワーカーで実装）
  - [x] 冪等性保証（`platform_transaction_id` の UNIQUE 制約）
- [x] Google Real-time Developer Notifications 受信エンドポイント（`POST /webhook/google`）
  - [ ] Pub/Sub プッシュサブスクリプションのベアラートークン検証（TODO: 非同期ワーカーで実装）
  - [ ] `ONE_TIME_PRODUCT_VOIDED` イベント処理（購入特定 → 有効期限確認 → 残高調整 → キャンセル記録）（TODO: 非同期ワーカーで実装）
  - [x] 冪等性保証（`purchaseToken` の UNIQUE 制約）
- [x] 取引キャンセル一覧取得エンドポイント（管理画面用）
- [x] 手動キャンセル実行エンドポイント（管理画面用・admin_id 記録）

**管理画面**
- [ ] スタンプ商品登録UI（画像・価格・タグ）
- [ ] 各商品への `collider_size_category` / `texture_cost` 設定 UI（ワールドオブジェクト）

### Unityクライアント
- [ ] `ShopManager` 実装
- [ ] Unity IAP 初期化・商品情報フェッチ（App Store / Google Play）
  - [ ] コイン購入商品 20 種（100〜1000 を100単位 / 2000〜10000 を1000単位）の Product ID 定義
  - [ ] 価格取得失敗時: 「価格を取得できませんでした」表示・購入ボタン無効化
- [ ] ショップ画面固定ヘッダー（5タブ + コイン残高）
- [ ] 商品一覧 UI（アバター / アクセサリ / オブジェクト / スタンプ 各タブ）
  - [ ] ソート4種（人気順デフォルト / いいね順 / 新しい順 / 古い順）
  - [ ] 名前検索・タグ検索
  - [ ] オブジェクトタブのみ: テクスチャコスト別フィルター・コライダーサイズ別フィルター
  - [ ] IDベースカーソルページネーション（スクロールで追加読み込み）
  - [ ] いいね数表示・いいね/いいね解除ボタン（自分の商品にはボタン非表示）
- [ ] プレミアムタブ UI（機能説明 + プラン選択購入。実際の課金バックエンドは Phase 10 で実装）
  - [ ] 年額プラン（上段・月換算価格目立て）・月額プラン（下段）表示
  - [ ] Unity IAP のサブスクリプション Product ID 定義（購入フローは Phase 10 で配線）
- [ ] コイン残高表示（マイナス時は赤字）・タップでコイン詳細画面へ
- [ ] コイン詳細画面（残高・ロット別有効期限一覧・「コインを購入」ボタン）
- [ ] コイン購入画面（20種選択 → IAP 購入 → 残高即時更新）
- [ ] Edit OK/NG バッジ表示
- [ ] 購入後のアバター・アクセサリ・ワールドオブジェクト・スタンプ自動追加

### テスト（EditMode）
- [x] `CoinLedger`: 残高計算（複数取引の合算）・マイナス残高の判定
- [x] `CoinLedger`: 残高マイナス時のショップ購入ブロック / コイン購入は許可
- [x] `CoinLedger`: 返金キャンセル処理（coins_deducted 計算・有効期限切れ時は 0）
- [x] `ShopProductFilter`: 人気順で `recent_purchase_count < 3` の商品が除外されること
- [x] `ShopProductFilter`: オブジェクトのテクスチャコスト/コライダーサイズフィルターが正しく機能すること

---

## Phase 9 — ワールドモードソーシャルUI

**目標**: ユーザー間のインタラクションUIが動作する状態
※ Phase 6（音声）・Phase 8（ショップ）が完成していることが前提

### `api/`（Go）
- [ ] ユーザー情報取得エンドポイント（@name・称号・ショップリンク・フォロワー数・フォロー中数）
- [ ] 違反報告送信エンドポイント（対象種別: ユーザー / ワールド / ショップ商品・通報理由8種・詳細テキスト・非表示同時実行オプション）
- [ ] アバター直接利用（購入アバターをスロットなしで使用）のセッション管理
- [ ] フレンド申請送信エンドポイント（上限チェック: 通常100人 / プレミアム1000人）
- [ ] フレンド申請承認 / 拒否エンドポイント
- [ ] フレンド申請キャンセルエンドポイント
- [ ] フレンドリスト取得エンドポイント（承認済み / 申請中 / 申請受信）
- [ ] フレンド解除エンドポイント
- [ ] フレンドのみルーム参加制御（参加時にルーム作成者のフレンドリストをチェック）
- [ ] フォローエンドポイント（`POST /users/{id}/follow`・`DELETE /users/{id}/follow`・レート制限付き）
- [ ] フォロワーリスト / フォロー中リスト取得エンドポイント（`GET /users/{id}/followers`・`GET /users/{id}/following`）
- [ ] フォロワー限定ルーム参加制御（参加時にルーム作成者のフォロワーリストをチェック）
- [ ] 招待リンク発行エンドポイント（`POST /rooms/{id}/invite-link`・プレミアム権限チェック・既存リンクを無効化して新規発行）。使用回数上限はルームの `room.max_players` と同じ値を使用
- [ ] 招待リンク情報取得エンドポイント（`GET /rooms/{id}/invite-link`・使用回数・有効期限）
- [ ] 招待リンク検証・入室エンドポイント（`POST /invite/{token}/join`・有効期限/使用回数/ルーム状態を検証・初回入室ユーザーのみカウントアップ）
- [ ] ルーム状態管理（OPEN / LOCKED / CLOSED）: 作成者退室時に LOCKED へ遷移・全員退室で CLOSED へ遷移
- [ ] ユーザー非表示エンドポイント（`POST /me/hidden-users/{id}`・`DELETE /me/hidden-users/{id}`・レート制限付き）
- [ ] ユーザー非表示リスト取得エンドポイント（`GET /me/hidden-users`）
- [ ] ワールド非表示エンドポイント（`POST /me/hidden-worlds/{id}`・`DELETE /me/hidden-worlds/{id}`）
- [ ] ワールド非表示リスト取得エンドポイント（`GET /me/hidden-worlds`）
- [ ] 非表示時のフレンド関係自動解除処理
- [ ] 非表示ユーザーからのフレンド申請をサーバー側でサイレント破棄する処理
- [ ] アプリ内通知基盤（通知レコード作成・一覧取得・既読更新エンドポイント）
  - [ ] フレンド申請受信時に通知レコードを生成
  - [ ] フォロー中ユーザーのワールド公開時に通知レコードを生成（レート制限: 同一フォロー元1日1件）
  - [ ] フォロー中ユーザーの商品販売開始時に通知レコードを生成（レート制限: 同一フォロー元1日1件）

### Unityクライアント
- [ ] 発話インジケーター（アバター頭上アイコンの点灯・Vivox発話状態連動）
- [ ] アバタータップUI（ユーザー情報パネル・称号表示・フォロワー数/フォロー中数・ショップリンク・フォロー/フォロー解除ボタン・フレンド申請ボタン・非表示ボタン・違反報告ボタン）
- [ ] ワールドモード入場時のアバター選択UI（スロット一覧 + 購入アバター直接利用）
- [ ] バックグラウンド通話モード（iOS/Android・プレミアム限定）
  - [ ] `OnApplicationPause` でのVivox接続維持
  - [ ] 3D描画停止処理
  - [ ] プレミアム判定によるON/OFFの切り替え
- [ ] フレンド管理画面（フレンド一覧タブ・申請中タブ・申請受信タブ・フレンドID検索）
- [ ] フレンドがいるルーム一覧画面（`GET /me/friends/rooms` を使用・全ワールド横断・ワールド名/フレンドアイコン/参加人数表示・タップで参加）
- [ ] フォロー管理画面（フォロー中タブ・フォロワータブ・フォロー解除ボタン）
- [ ] 招待制ルーム作成フロー（プレミアム判定 → ルーム種別「招待制」選択 → 作成後に招待リンクパネルを表示）
- [ ] 招待リンク管理 UI（コピーボタン・再発行ボタン・使用回数表示・有効期限残り時間表示）
- [ ] Deep Link ハンドリング（iOS: Universal Links / Android: App Links）
  - [ ] アプリ未起動時: 起動後にリンク処理を継続
  - [ ] アプリ起動中: フォアグラウンドでリンクを受け取りルーム参加フローへ
- [ ] 招待リンクエラー表示（有効期限切れ / 使用回数上限 / ルーム入室不可 / 無効リンク）
- [ ] `HideListLogic` 実装
  - [ ] 非表示リストをローカルに保持（ルーム参加時にサーバーから取得）
  - [ ] 非表示ユーザーのアバター描画スキップ（`AvatarManager` 連携）
  - [ ] 非表示ユーザーの Vivox 音声ミュート（`VoiceManager` 連携）
  - [ ] 非表示ユーザーの物理・ギミック処理は通常通り維持（ゲーム状態に影響しない）
- [ ] ユーザー非表示リスト管理画面（設定画面から遷移・非表示解除ボタン）
- [ ] 通報モーダル共通UI（通報理由8種・必須詳細テキスト・非表示チェックボックス（デフォルトON）・通報/キャンセルボタン）
- [ ] ワールド詳細画面: クイックいいねボタン（サムネイル右下ハートアイコン）・いいねボタン・その他アクションボタン（…）→ 非表示/通報
- [ ] ワールド非表示: 一覧・ポータルサムネイル非表示・ポータル移動不可・「非表示にしました」フラッシュメッセージ
- [ ] ワールド非表示リスト管理画面（設定タブから遷移・非表示解除ボタン）
- [ ] 各アクション完了時のフラッシュメッセージ表示（「通報しました」「非表示にしました」）
- [ ] アプリ内通知センター（ベルアイコン・未読バッジ・一覧表示・タップで対象画面遷移）
- [ ] 通知設定 UI（設定画面内: 種別ごとの ON/OFF・ベルマーク（プッシュ有効化））
- [ ] 撮影機能
  - [ ] 撮影モード切り替え: 通常 HUD を非表示にして撮影専用 HUD（×/シャッター/スタンプ）に切り替え
  - [ ] 撮影モード専用カメラ操作: 2 本指ピンチ/スライドでカメラ位置変更（ズーム・平行移動）。終了時に撮影前の位置に復元
  - [ ] スタンプオーバーレイ配置: メニュー下から出す → メニュー外ドラッグで配置。ドラッグ/2本指回転/ピンチ縮小で操作
  - [ ] ドラッグ中ゴミ箱アイコン表示 → ゴミ箱に重ねてスタンプ削除
  - [ ] スタンプ配置状態をルームセッション中メモリ保持（撮影モード再入時に復元）
  - [ ] スタンプ選択メニュー: タブ構成（デフォルト/プレミアム/購入グループ）・プレミアムロック表示・最終選択タブのセッション保持
  - [ ] スタンプ込み・UI なしのスクリーンショット撮影（`ScreenCapture.CaptureScreenshotAsTexture` + スタンプを RenderTexture で合成）
  - [ ] 撮影と同時にカメラロールへ保存（iOS: `Photos API` / Android: `MediaStore API`）
  - [ ] 撮影後左下サムネイル表示・タップで拡大・再タップまたは × で閉じる
  - [ ] プレミアム限定スタンプのロック制御（非プレミアム: ロックアイコン + 購入誘導）
  - [ ] 色変えスタンプ: サムネイル右下にカラーサークルインジケーターを表示
  - [ ] 文字入れスタンプ 3 種類の実装（透明背景/角丸白文字/角丸黒文字）
  - [ ] 文字入れスタンプ: 配置後テキスト入力モード移行（キーボード表示）・文字外タップで完了・スタンプ再タップで再編集
  - [ ] 色変えスタンプ用カラーパレット UI（12 色横並び・横スクロール・スポイトボタン先頭）
    - 文字入れスタンプ: キーボード直上に表示（文字編集と同タイミング）
    - それ以外: 画面下部に表示
    - スタンプ外・パレット外タップで非表示・スタンプ再タップで再表示
  - [ ] スポイトモード: 他UI非表示・円形スポイトカーソル・色サークル表示（白縁取り）・指を離して 0.4 秒で色確定・0.4 秒以内の再タップで再スポイト開始
  - [ ] スポイト色サークルの貫通サンプリング（色サークル UI 自体を除外してスクリーンカラーを取得）
- [ ] 多言語対応 — 残り 8 言語追加（zh-Hans / zh-Hant / ko / fr / es / it / de / pt-BR）
  - [ ] 全 UI 文字列の String Table 翻訳データ作成
  - [ ] フォント対応（CJK 文字対応フォントの組み込み）

### テスト（EditMode）
- [ ] `FriendListLogic`: フレンド上限チェック（通常100・プレミアム1000）
- [ ] `FriendListLogic`: 申請状態遷移（未申請→申請中→承認済み / 未申請→申請中→拒否）
- [ ] `FriendListLogic`: 相互承認の成立条件・解除処理
- [ ] `FollowListLogic`: フォロー/フォロー解除の状態遷移
- [ ] `HideListLogic`: 非表示追加・解除・一覧取得
- [ ] `InviteRoomLogic`: ルーム状態遷移（OPEN → LOCKED → CLOSED）・入室可否判定
- [ ] `HideListLogic`: 非表示ユーザーの描画・音声フィルタリング判定（リストにいる場合のみ true）
- [ ] `NotificationStore`: 未読件数カウント・既読マーク・種別フィルタリング
- [ ] `StampColorPickerLogic`: 色選択・スポイト確定タイマー（0.4 秒）・再タップでリセット
- [ ] `TextStampLogic`: テキスト編集状態遷移（未編集 → 編集中 → 完了 → 再編集）

---

## Phase 10 — サブスクリプション・プレミアム機能

**目標**: プレミアム機能が動作する状態

### `api/`（Go）
- [ ] `PlanConfig` マップ実装（`docs/unity-game-abstract.md` セクション 23.3 参照）
  - `PlanTier` 型・`PlanTierOrder` マップ・`TierAtLeast` 関数
  - `FeatureMinTier` マップ（`background_call` / `invite_room_create` / `name_change` / `premium_stamps_filters`）・`HasFeature` 関数
  - `PlanCapabilities` 構造体・`PlanConfig` マップ・`GetCapabilities` 関数
- [ ] サブスクリプション管理エンドポイント（`subscription_tier` 更新・`subscription_expires_at` 管理・期限切れ時の `'free'` フォールバック処理）
- [ ] スロット上限チェックを `GetCapabilities(user.SubscriptionTier).XxxSlots` で実装（定数ハードコード禁止）
- [ ] 解約後スロットロック処理（データ保持・ロードのみ不可）
- [ ] 招待制ルーム作成時の権限チェック: `HasFeature(user.SubscriptionTier, "invite_room_create")` が false なら 403
- [ ] `GET /startup` レスポンスに `planCapabilities` オブジェクトを追加（`GetCapabilities` + `HasFeature` で計算したものをクライアントへ返す）

### Unityクライアント
- [ ] `UserManager` 実装（ユーザーID・称号・`PlanCapabilities` 構造体保持）
  - `PlanCapabilities` は起動時に `/startup` レスポンスから取得してキャッシュ
  - `IsPremium` プロパティは公開しない。全ての判定は `UserManager.Capabilities.XxxField` で行う
- [ ] アバタースロット上限チェック: `UserManager.Capabilities.AvatarSlots` を使用
- [ ] プレミアム解約後のスロットロック表示
- [ ] ユーザー設定画面（@name設定・称号表示/非表示切り替え・プレミアム加入/解約・言語選択）
  - [ ] 言語選択UI（10言語ドロップダウン / リスト）・変更即時反映・サーバーへ保存
  - [ ] お問い合わせリンク（nibankougen@gmail.com）をテキストリンクとして設置
  - [ ] @name 変更 UI（現在の @name 表示・次回変更可能日表示・注意文言表示・`Capabilities.NameChange == false` の場合はロック）
  - [ ] ソーシャルプロバイダー連携管理 UI（連携中プロバイダー一覧・追加ボタン・解除ボタン・最低 1 つ維持の制約をクライアントでも表示）
  - [ ] アカウント削除リンク（赤字テキスト・確認ダイアログ → `DELETE /me` 呼び出し → 即時ログアウト）
- [ ] `SessionTimeLimitLogic` のプラン対応: `capabilities.SessionMinutes` をコンストラクタで受け取る（直接 90/720 をハードコードしない）
- [ ] `AfkDetectionLogic` のプラン対応: `capabilities.AfkEnabled` をコンストラクタで受け取る

---

## Phase 11 — 違反報告・モデレーション

**目標**: 違反報告から管理画面での対応まで一連のフローが動作する状態
※ Phase 9（ソーシャルUI）・Phase 5（管理画面）が前提

### `api/`（Go）
- [ ] 警告記録・BAN記録データモデル実装
- [ ] `avatars` / `accessories` テーブルに `moderation_status VARCHAR(10) NOT NULL DEFAULT 'pending'` 追加・マイグレーション作成
- [ ] `admin_alerts` テーブル実装（CSAM 検知等のアラート記録）
- [ ] Optimizer ワーカーにモデレーションステップ追加
  - [ ] CSAM ハッシュ照合インターフェース（`ContentModerationService` と同様に差し替え可能な no-op から開始）
  - [ ] アップロード完了時に trust_level で `moderation_status` を確定（visitor/new_user → pending、user 以上 → approved）
- [ ] `GET /api/v1/avatars/{id}` に visibility チェック追加（pending は本人以外 404）
- [ ] `GET /api/v1/users/{id}/avatars` で `moderation_status = 'approved'` のみ返すよう絞り込み
- [ ] アバターBANエンドポイント（`moderation_status = 'rejected'` 設定）
- [ ] ユーザーBAN/BAN解除エンドポイント（ログイン不可・データ凍結）
- [ ] 警告発行エンドポイント（警告数カウント・累積2回でBAN推奨フラグ）
- [ ] 称号付与エンドポイント（開発者・運営関係者）
- [ ] 公認バッジ付与/剥奪エンドポイント（`PATCH /admin/users/{id}/verified`・`admin` / `super_admin` ロールのみ）
- [ ] 自動ユーザー制限ロジック（通報受信時に同期実行・`docs/unity-game-abstract.md` セクション 22.5 参照）
  - [ ] `report_records` テーブル実装（reporter_id / target_id / reported_at）
  - [ ] 通報受信時: ターゲットのトラストレベルを確認し visitor / new_user のしきい値チェック
    - visitor: 直近 24h の重複なし通報者数 ≥ 2 **または** 累計重複なし通報者数 ≥ 4 → `is_restricted = true`
    - new_user: 直近 24h の重複なし通報者数 ≥ 3 **または** 累計重複なし通報者数 ≥ 10 → `is_restricted = true`
    - ロック中のユーザーは自動制限対象外
  - [ ] ルーム参加時の制限チェック（公開・フォロワー限定ルームへの参加試行時に `is_restricted` を確認・true なら 403 `user_restricted` を返す）

### Unityクライアント
- [ ] BANユーザーのログイン拒否処理
- [ ] 検疫中アバター（`moderation_status = 'pending'`）の扱い
  - 本人のアバター一覧に「審査中」バッジ表示
  - ルーム内で他プレイヤーが pending アバターを持つプレイヤーを見た場合、フォールバックアバター（システム既定）を表示
- [ ] BAN済みアバター（`moderation_status = 'rejected'`）の非表示処理（Atlas更新・スロット表示）
- [ ] ルーム参加時に 403 `user_restricted` を受信したとき制限エラー画面を表示（お問い合わせメールリンク: nibankougen@gmail.com）
- [ ] 公認バッジ表示対応（`is_verified` フィールドを受け取り、表示名の右横に `icon_verified.png` を表示）
  - ワールド内アバター頭上の World Space Canvas 名前タグ
  - ユーザー情報パネル（セクション 2.4）
  - アカウント情報画面（セクション 22.1）
  - ルームメンバー一覧・フレンド一覧・フォロー/フォロワー一覧など表示名が出るすべての箇所

---

## Phase 12 — ワールド作成システム

**目標**: ユーザーがワールドを作成・編集・公開できる状態
※ Phase 3（マルチプレイヤー同期）・Phase 5（API）・Phase 8（ショップ）が前提
技術仕様: `docs/world-creation.md` 参照

### `api/`（Go）
- [ ] ワールドスロット管理エンドポイント（通常5個 / プレミアム50個・スロット一覧・解約後ロック）
- [ ] 保存バリアントスロット管理エンドポイント（通常10個 / プレミアム100個・スロット一覧・解約後ロック）（Phase 5 で追加済みのエンドポイントを使用）
- [ ] ワールド定義 JSON 保存・取得エンドポイント（`worldObjectCustomizations` フィールド対応・version 2）
- [ ] ワールドアトラス生成処理（サーバー側テクスチャパック・UV マッピング情報生成）
  - [ ] objectTypeId のワールドスコープカスタマイズテクスチャ（存在する場合）またはデフォルトテクスチャをアトラスにパック
  - [ ] savedVariantId のバリアントテクスチャをアトラスにパック
- [ ] ワールドアトラス PNG アップロード・配信エンドポイント
- [ ] 背景テクスチャアップロード・配信エンドポイント
- [ ] サムネイルアップロード・リサイズ処理（オリジナルサイズ保存なし）
- [ ] デフォルトワールドオブジェクト配信エンドポイント（GLB・テクスチャ）
- [ ] ユーザー購入済みワールドオブジェクト一覧取得エンドポイント
- [ ] ギミック状態同期（Netcode for GameObjects 経由でルームオーナーをマスターとする設計）
  - [ ] タイマー同期: `{ startTimestamp, isRunning, elapsedAtStop }` 形式（操作時のみ送信・逐次ブロードキャスト不要）
- [ ] フレンド招待通知エンドポイント（ルームへのアプリ内招待通知送信）
- [ ] 管理画面: デフォルトワールドオブジェクト登録・編集・有効/無効切り替え
- [ ] 管理画面: ショップワールドオブジェクトへのスケールロックフラグ設定（ON/OFF、デフォルト OFF）
- [ ] 管理画面: ユーザー作成ワールドの一覧・強制非公開・削除
- [ ] 管理画面: タグ BAN リスト管理（BAN タグ登録・解除・使用ワールド数確認）
- [ ] マイワールドオブジェクトアップロードエンドポイント（GLB 受信・ポリゴン/容量/テクスチャサイズ検証・スロット上限チェック・保存）
- [ ] マイワールドオブジェクト一覧取得エンドポイント（SHA-256 ハッシュフィールド含む）
- [ ] マイワールドオブジェクト削除エンドポイント（スロット解放）
- [ ] マイワールドオブジェクト名前更新エンドポイント
- [ ] 管理画面: マイワールドオブジェクト一覧・BAN処理

### Unityクライアント
- [ ] `WorldCreationManager` 実装
- [ ] ワールドスロット一覧画面（新規作成・既存編集・削除）
- [ ] ワールド設定パネルのタグ入力 UI（最大5個・テキスト入力・チップ表示・削除）
- [ ] ワールド設定パネルの環境音選択 UI（内蔵ライブラリ一覧・音量スライダー 0〜100%）
- [ ] ワールド定義 JSON への `ambientSound` フィールド対応（保存・読み込み）
- [ ] ワールド設定パネルの人数上限 UI（デフォルト 6・通常ユーザー: 2〜6 / プレミアム: 2〜24）
- [ ] ワールド定義 JSON への `maxPlayers` フィールド対応（保存・読み込み）
- [ ] 環境音プレイヤーへのワールド定義 JSON の反映（入場時に再生・退場時に停止）
- [ ] ワールド選択画面のタグフィルター UI（横スクロールチップ・複数選択）
- [ ] ワールドエディタ UI（セクション 11.7）
  - [ ] レイアウト: ヘッダー・上部中央ギズモ切り替えボタン（移動/拡大縮小/回転）・3D ビュー・下部タブ（地形/オブジェクト/ギミック/設定）
  - [ ] グリッドスナップ（移動: 0.5m 単位 / 63×31×63 グリッド / 原点中心）
  - [ ] プレイヤーリスポーン: 64×64×64 の範囲外に出たらスポーン位置に自動リスポーン
  - [ ] テクスチャコストのリアルタイム計算と表示（右上・上限 4,096）
  - [ ] オブジェクト数上限チェック（合計 400）
  - [ ] **オブジェクトタブ**（セクション 11.7.3）
    - [ ] タブ固定ヘッダー: テクスチャコスト合計/上限 + オブジェクト数合計/上限
    - [ ] 利用中: サムネイルグリッド（横並び折り返し）・左下にテクスチャコスト表示
    - [ ] 利用中: D&D 並び替え（描画順対応）・複製は直後に挿入
    - [ ] グループ: 作成（選択中アイテムからグループ化）・名前設定（1〜20文字・連番デフォルト）・▶/▼ 開閉トグル・D&D で出し入れ
    - [ ] グループ: 最大 4 段ネスト・合計 32 個上限
    - [ ] グループ操作: 移動/拡大縮小/回転を各オブジェクトに個別適用・いずれか不可の場合キャンセル＋フラッシュメッセージ
    - [ ] 複数選択: 同一階層レベルのみ・異階層選択時エラー・複数選択時は移動のみ（拡大縮小/回転不可）
    - [ ] 保存・編集サブタブ: 編集オブジェクトスロット + 保存バリアント一覧
    - [ ] 所有サブタブ: デフォルト・購入済み・特殊（スポーン位置・エリア・ポータル等）
    - [ ] 利用中以外タップ/D&D → 原点（0,0,0）に配置・選択状態
    - [ ] 選択状態: 複製ボタン・削除ボタン・テクスチャ編集ボタン
  - [ ] ギズモ操作: 移動（0.5m グリッドスナップ）・回転（Y軸45°）・拡大縮小（W/D/H 0.25m・スケールロック判定）
  - [ ] 重複判定: スポーン・ポータルのコライダー重複を検出 → 警告表示・プレイ/公開ブロック
  - [ ] 地形透過ボタン（3D ビュー左上・全地形を半透明にする）
- [ ] オブジェクト編集画面（セクション 11.8）
  - [ ] 下部アイコンタブ（テクスチャ / コライダー）
  - [ ] コライダータブ（マイオブジェクト専用）: W/D/H 増減・位置オフセット・装飾オブジェクトトグル（`docs/world-creation.md` セクション 13）
- [ ] マイワールドオブジェクト管理（セクション 11.3 スロット操作）
  - [ ] アップロードフロー（ファイルピッカー → 検証エラー表示 → コライダー自動検出表示 → 確定）
  - [ ] GLB AABB 算出・0.25m 切り上げ処理（Go API サーバー側でコライダー自動検出）
- [ ] ワールドスコープカスタマイズのテクスチャ編集（テクスチャ編集ボタン → ペイントUI → ワールドスコープ保存 → アトラス再生成トリガー）
- [ ] 保存バリアントスロット管理 UI（「保存バリアントに保存」ボタン・バリアント名入力・スロット一覧・削除・上限超過エラー）
- [ ] CacheManager: マイワールドオブジェクト GLB を persistentDataPath に永続保存
- [ ] 特殊オブジェクトのゲームロジック
  - [ ] スポーン位置: 1×1.5×1 コライダー・原点 (0.5,0,0.5)・入場時プレイヤースポーン
  - [ ] ルーム内ポータル: 白（入口）→ 黒（出口）への転送処理・最大 8 つ
  - [ ] ワールドポータル: 別ワールドのスポーン位置へ遷移・サムネイル表示・最大 4 つ
  - [ ] 公開条件チェック: 全入口ポータルに出口設定済み・スポーン/ポータルの重複なし
- [ ] **ギミックタブ UI**（セクション 11.7.4）
  - [ ] タブ固定ヘッダー: ルール・グループ合計 / 100
  - [ ] ステート定義エリア: ワールドステート(0〜9)・プレイヤーステート(0〜3)・タイマー(0〜4) の名前+初期値入力
  - [ ] ルール一覧: D&D 並び替え・タイトル名変更・グループ化（オブジェクトグループと同仕様・合計 100 上限）
  - [ ] ルール編集画面（タップでタブ拡大）
    - [ ] 入力イベントセクション（+で追加・最大 20・OR 結合）
    - [ ] 条件セクション（+で追加・最大 20・AND 結合）
    - [ ] アクションセクション（+で追加・最大 20・順番実行）
    - [ ] 値入力種別: 固定値 / ワールドステート参照 / 関わったプレイヤーのステート参照 / 全プレイヤーステート合計 / 範囲乱数（オーナー生成・共有）
    - [ ] 比較演算: 大小等 / X で割った余りが Y
    - [ ] 対象プレイヤー選択: 入力プレイヤー / 相手プレイヤー（存在するとき）/ 全員
    - [ ] オブジェクト指定: 3D ビュータップ or 利用中オブジェクト一覧タップ
    - [ ] 文字メッセージアクション: デフォルト単言語入力 / 詳細で言語別入力（各 80 文字以内・フォールバック英語優先）
- [ ] 設定タブ（ワールド設定パネル UI）
  - [ ] テストプレイボタン（オフライン・シングルプレイヤー）・「＜」戻るボタン
  - [ ] ステートデバッグ表示トグルボタン（テストプレイ中 + 通常ワールドモードの Unity デバッグビルド時）
  - [ ] サムネイル設定フロー
    - [ ] 写真から選択（9:16 クロップ調整 → クライアント側解像度正規化 → アップロード）
    - [ ] ゲーム内で撮影（テストプレイモード移行 → 撮影機能 → 確認モーダル「サムネイルに設定」/「撮り直す」）
  - [ ] ワールド名・タグ（最大 5 個）・背景設定・環境カラー（カラーピッカー・V ≥ 0.25 制限）・環境音・人数上限・公開/非公開
  - [ ] フォグ設定 UI（ON/OFF トグル・カラーピッカー・開始/終了距離スライダー）
  - [ ] スクリーンエフェクト設定 UI（種類ドロップダウン・強度スライダー）
  - [ ] 「新しいバージョンを公開」ボタン（詳細は 11.7.6 公開フロー参照）
  - [ ] バックアップ保存（プレミアムのみ・5 枠・上書き確認ダイアログ・異常ファイルサイズのみバリデーション）
  - [ ] バックアップ復元（プレミアムのみ・復元確認ダイアログ）
- [ ] ギミック実行エンジン（入力イベント検知・条件評価・アクション実行）
  - [ ] 新入力イベント: プレイヤー同士の接触 / リスポーン / ルーム内ポータル利用
  - [ ] 相手プレイヤーの確定・伝播（接触イベント / 距離条件 / 視線条件）
  - [ ] 新条件: プレイヤー同士が重なっている / 距離 / 視線レイキャスト / プレイヤー番号 / 順位比較（X番目に大きい/小さい）
  - [ ] 条件の比較演算に mod (X で割った余り) 追加
  - [ ] アクション値参照: ワールドステート / プレイヤーステート / 全プレイヤーステート合計 / 範囲乱数
  - [ ] 新アクション: プレイヤー移動（出口ポータルへワープ）/ 状態リセット / エフェクト再生
  - [ ] 無限ループ検出（連鎖100回超でエラーメッセージ + ルームリセット）
- [ ] インベントリシステム（オブジェクト保有・退出時リセット）
- [ ] ルーム内アクションボタン UI（接触オブジェクトに対応する画面上ボタン）
- [ ] ルーム内ポータル・ワールドポータルの遷移処理
- [ ] 数字オブジェクト（プレミアム限定・上限 30 個・ワールドステート/プレイヤーステート/固定値参照・ステート変更時即時更新）
- [ ] 背景レンダリング（単色・グラデーション・テクスチャ）
- [ ] 環境カラー対応
  - [ ] 地形シェーダーに `_AmbientColor` プロパティを追加（`texel × vertex_AO × ambient`）
  - [ ] 共通 Unlit シェーダー（アバター・オブジェクト用）に `_AmbientColor` プロパティを追加（`texel × ambient`）
  - [ ] ワールド読み込み時に `ambientColor` を JSON から取得してシェーダーに渡す
  - [ ] world JSON への `ambientColor` フィールド対応（保存・読み込み。デフォルト `#FFFFFF`）
- [ ] フォグ対応（詳細: `docs/world-creation.md` セクション 15.18）
  - [ ] 地形シェーダー・共通 Unlit シェーダーに `#pragma multi_compile_fog` と `UNITY_FOG_COORDS` / `UNITY_TRANSFER_FOG` / `UNITY_APPLY_FOG` マクロを追加
  - [ ] ワールド読み込み時に `fog` フィールドを JSON から取得して `RenderSettings` に反映（`fog` / `fogColor` / `fogStartDistance` / `fogEndDistance`）
  - [ ] ワールド退出時に `RenderSettings.fog = false` にリセット
  - [ ] world JSON への `fog` フィールド対応（保存・読み込み）
- [ ] スクリーンオーバーレイエフェクト対応（詳細: `docs/world-creation.md` セクション 15.19）
  - [ ] `ScreenEffectController` 実装（種類・強度の切り替え・`type = "none"` 時は `SetActive(false)`）
  - [ ] 雨エフェクト実装（Screen Space Canvas 上のパーティクルシステム・強度に比例した放出レート制御）
  - [ ] world JSON への `screenEffect` フィールド対応（保存・読み込み）
- [ ] ワールド公開フロー（必須条件チェック + ギミックループ検出 → サーバー送信）
- [ ] ワールド編集中フレンド招待（ルーム ID 共有 + アプリ内通知）
- [ ] 地形システム
  - [ ] **データ構造**
    - [ ] 地形種別 UID（int64 文字列）・パレット（最大 16 種類）
    - [ ] ボクセルバイト定義（上位 4bit: shape、下位 4bit: palette index）
    - [ ] チャンク構造（16×16×16、格納順 X→Z→Y）・空チャンク省略
    - [ ] RLE 圧縮（`value, count` 1 バイトペア）・バイナリファイル形式（magic + チャンクリスト）
    - [ ] world JSON への terrain フィールド追加（palette, voxelDataUrl, terrainAtlasUrl, terrainAtlasUVMap）
  - [ ] **サーバー（Go API）**
    - [ ] 地形種別 UID 単位でテクスチャデータを返すエンドポイント
    - [ ] ワールド保存時: ボクセルデータ RLE 圧縮 → バイナリ blob 保存・URL 生成
    - [ ] ワールド保存時: 地形テクスチャアトラス生成（1024×1024、ランダム→固定順で貪欲パック）
  - [ ] **Unity クライアント**
    - [ ] ボクセルデータのデシリアライズ（RLE 展開・チャンク構造復元）
    - [ ] 動的メッシュ生成（チャンク単位・隣接判定による面カリング）
      - [ ] cube / ramp / diag 各形状の面生成
      - [ ] 隣接判定ルール実装（セクション 15.12 の形状別テーブル準拠）
      - [ ] チャンク境界の隣チャンク参照（未ロード時は「地形あり」扱い）
      - [ ] 下面メッシュは撮影モード時のみ生成
    - [ ] テクスチャ領域選択ルール（上面/上面中間/側面系/坂側面系/下面）
    - [ ] ランダム地形テクスチャのバリアント選択ハッシュ関数実装
    - [ ] UV 設定: 領域内 [0.005,0.005]〜[0.995,0.995] + アトラス変換・坂面三角形 UV
    - [ ] Filter Mode: Point (no filter) を地形テクスチャに設定
    - [ ] `Graphics.DrawMeshInstanced` によるバッチレンダリング（地形種別 × 面種でグループ化）
    - [ ] 頂点カラー AO 計算（メッシュ生成時に 1 度だけ実行・頂点カラーとして保存）
      - [ ] 通常面（cube）: 隣接ブロックの有無をウェイト 1.0 で加算し明度 0〜0.75 にマッピング
      - [ ] 坂/斜め面（ramp/diag）: 斜め面限定で同高さ隣接ブロックも参照・重複ウェイトは大きい方を採用する形状 × 頂点ごとのルールテーブルを実装
    - [ ] コライダー生成（グリーディーメッシュ法・XZ 優先 BoxCollider 結合。ramp/diag は全体近似）
    - [ ] Height Culling（プレイヤー真上レイキャスト → 閾値以上を非表示・上面中間テクスチャ切り替え）
    - [ ] **地形タブ UI**（セクション 11.7.2）
      - [ ] 斜め上固定カメラ・高さスライス表示・グリッド境界描画
      - [ ] 高さバー（右側スクロール）・上方半透明表示・上方非表示トグル
      - [ ] 地形サブタブ（利用中 / 保存・編集 / 所有）・透明地形 `!` 警告アイコン
      - [ ] ブラシモード（タップ/スライドで配置・上書き）
      - [ ] 消しゴムモード（タップ/スライドで削除）
      - [ ] 図形モード（スライド範囲に一括配置）
      - [ ] タイプ変更モード（cube → ramp → diag → cube サイクル・条件チェック・エラーフラッシュ）
      - [ ] 範囲選択モード（四角形 / 塗りつぶし範囲・複数選択・コピー＆他高さへのペースト）
      - [ ] 移動モード（選択範囲または現在高さ全体・XZ 平面・範囲外削除）
    - [ ] 地形テクスチャ編集（ベースレイヤー編集可・透明ピクセル対応）

### テスト（EditMode）
- [ ] `GimmickEngine`: 複数入力イベント（OR）のいずれかが発火でルール起動すること
- [ ] `GimmickEngine`: イベント発火 → 条件評価（AND 結合）→ アクション実行の基本フロー
- [ ] `GimmickEngine`: 無限ループ検出（連鎖 100 回超でエラー・ループしない場合は通過）
- [ ] `WorldPublishValidator`: ギミックループ検出テスト（内部テストプレイで検知 → 原因ルール特定）
- [ ] `GimmickEngine`: 複数ルールが同一フレームで発火したとき定義順に実行されること
- [ ] `GimmickEngine`: 相手プレイヤー確定後にアクション対象として正しく渡されること
- [ ] `GimmickValueResolver`: 固定値 / ステート参照 / プレイヤーステート参照 / 乱数それぞれが正しい値を返すこと
- [ ] `NumberObjectSync`: ステート更新時に参照している数字オブジェクトが即座に更新されること
- [ ] `InventorySlot`: 「持つ」反応でオブジェクトが消え保有状態になること
- [ ] `InventorySlot`: 別オブジェクトを持とうとしたとき既存アイテムが元の位置に戻ること
- [ ] `InventorySlot`: ルーム退出時リセット
- [ ] `WorldVariantSlotManager`: スロット上限チェック（通常10 / プレミアム100）・上限超過エラー
- [ ] `WorldVariantSlotManager`: プレミアム解約後のロック判定
- [ ] `MyObjectSlotManager`: スロット上限チェック（通常10 / プレミアム100）・上限超過エラー
- [ ] `MyObjectSlotManager`: プレミアム解約後のロック判定
- [ ] `TextureCostCalculator`: objectTypeId / savedVariantId ごとの独立カウント
- [ ] `TextureCostCalculator`: ギミック非表示オブジェクトをコスト対象外にすること
- [ ] `TextureCostCalculator`: 上限 4,096 到達時の追加ブロック判定
- [ ] `WorldObjectScaleLogic`: インスタンスサイズ設定・0.25m クランプ・スケールロック判定（ロック時は変更を拒否）
- [ ] `ColliderSizeRounding`: AABB float 値 → 0.25m 単位切り上げ・全軸 0 時の装飾判定

---

## Phase 13 — プッシュ通知インフラ

**目標**: バックグラウンド・スリープ中でもフォロー通知を受け取れる状態
※ Phase 9（ソーシャルUI・アプリ内通知基盤）が完成していることが前提

### `api/`（Go）
- [ ] APNs 送信実装（HTTP/2・`p8` キー認証）
- [ ] FCM 送信実装（Firebase Admin SDK v1 API）
- [ ] デバイストークン登録エンドポイント（`POST /me/push-tokens`・プラットフォーム種別付き）
- [ ] デバイストークン削除エンドポイント（`DELETE /me/push-tokens/{token}`）
- [ ] プッシュ通知送信ジョブ（非同期キュー処理）
  - [ ] ユーザー通知設定（ベルマーク ON/OFF）をチェックしてから送信
  - [ ] レート制限チェック（同一フォロー元ユーザー: 1日1件 / 全体受信上限）
  - [ ] 上限超過時はアプリ内通知のみ作成（プッシュは送信しない）
  - [ ] APNs/FCM から invalid token レスポンスを受信した場合にトークンを自動削除

### Unityクライアント
- [ ] 起動時のプッシュ通知権限リクエスト（ベルマークを初めて ON にしたときに発火）
  - [ ] iOS: `UNUserNotificationCenter.RequestAuthorization`
  - [ ] Android: `POST_NOTIFICATIONS` 権限リクエスト（Android 13 以上）
- [ ] デバイストークン取得・API サーバーへの登録
  - [ ] iOS: `application:didRegisterForRemoteNotificationsWithDeviceToken:`
  - [ ] Android: Firebase Messaging `OnTokenRefresh`
- [ ] トークン更新時の自動再登録
- [ ] フォアグラウンド中にプッシュ通知を受信した場合: OS 通知バナーを抑制してアプリ内通知センターに統合

### テスト（EditMode）
- [ ] `NotificationStore`: 未読バッジカウント・既読マーク・種別フィルタリング（OFF 設定の種別は受信しない）

---

## Phase 14 — ホームレコメンドシステム

**目標**: ホームタブのパーソナライズフィードが動作する状態
※ Phase 9（ソーシャルUI・フォロー/フレンド・いいね・入室記録）・Phase 11（違反報告）が完成していることが前提

### `api/`（Go）

**DB スキーマ**
- [ ] `worlds.recent_report_count` カラム追加（通報フィルターキャッシュ）
- [ ] `user_recommendations` テーブル（`user_id`, `world_id`, `created_at`, `read_at`）
- [ ] `user_recommendation_seeds` テーブル（最後にシード収集した日時を記録し、1日1回制御に使用）

**通報フィルターキャッシュ管理**
- [ ] 通報受信時に `recent_report_count` をインクリメントするトリガー/フック
- [ ] 72時間バッチジョブ: 期限切れ通報を集計し `recent_report_count` を更新（超過分をデクリメント/リセット）

**フィード API**
- [ ] `GET /home/feed`: フォロー中・フレンド新着（同期）+ メインフィード（アクティブ判定自動）を返す
  - フォロー中・フレンド新着: 24時間以内公開・いいね/入室なし・公開日時降順
  - アクティブ判定: 直近72時間以内のログインがあるかチェック
  - パターンA（非アクティブ）: 24時間いいね上位・いいね/入室なし・ランダム順
  - パターンB（アクティブ）: 未読専用レコメンドと24時間いいね上位を2:1混在・ランダム順
  - 全経路で `recent_report_count < 3` のみ返す
  - カーソルページネーション
- [ ] `POST /home/feed/read`: フィード取得アイテムの既読マーク（`user_recommendations.read_at` 更新）

**専用レコメンド生成バックグラウンドジョブ**
- [ ] アクティブユーザー（72時間以内ログインあり）を対象に1日1回実行
- [ ] シード収集: 直近72時間のいいね（新しい順・最大10件）→ 不足分を入室記録で補完（計10件）
- [ ] シードワールドの全タグ → おすすめタグ（共起ベース）
- [ ] シードワールドの作成者 → おすすめユーザー
- [ ] おすすめタグマッチ・未いいね/未入室ワールドを以下の4クエリで収集（重複は後工程で除去）:
  - 24時間以内公開の新着順（最大10件）
  - 24時間入室数降順（24時間入室数 ≥ 6 のみ、最大20件）
  - 24時間いいね数降順（24時間いいね数 ≥ 6 のみ、最大20件）
  - 累計いいね数降順（累計いいね数 ≥ 12 のみ、最大20件）
- [ ] おすすめユーザーのワールド・未いいね/未入室・累計いいね数降順（累計いいね数 ≥ 6 のみ、最大30件）
- [ ] 重複除去・シャッフルして `user_recommendations` に保存

### テスト（Go）
- [ ] 通報フィルター: `recent_report_count ≥ 3` のワールドがフィードに含まれないこと
- [ ] フォロー新着: 24時間以内・フォロー/フレンドのみ・いいね/入室済みは除外
- [ ] パターンA: 72時間ログインなしのユーザーには専用レコメンドが含まれないこと
- [ ] パターンB: 未読専用レコメンドと24時間上位の比率が 2:1 になること
- [ ] 専用レコメンド全既読後はパターンAと同じ動作になること
- [ ] レコメンド生成ジョブ: おすすめタグが全シードワールドのタグの和集合になること
- [ ] レコメンド生成ジョブ: 重複除去後にシャッフルされた結果が保存されること
- [ ] レコメンド生成ジョブ: いいね/入室済みワールドが結果に含まれないこと
- [ ] 既読マーク: `POST /home/feed/read` 後に対象アイテムが `read_at` 設定されること

---

## 依存関係

```
Phase 1
  └─ Phase 2
       └─ Phase 3 ──────────── Phase 5 ──── Phase 6
            │                      │              └─ Phase 9 ─── Phase 11 ─── Phase 14
       Phase 4 (並行可)            Phase 8              │           │
                                       └─ Phase 9    Phase 10   Phase 13
                                   Phase 7（最適化・Phase 5以降並行可）

Phase 3 + Phase 5 + Phase 8 → Phase 12
Phase 9 → Phase 13
Phase 9 + Phase 11 → Phase 14
```

---

## Docker ローカル開発

```bash
# APIサーバー + optimizer を起動
docker compose up --build

# Unityは http://localhost:8080 に接続（Phase 5以降）
```
