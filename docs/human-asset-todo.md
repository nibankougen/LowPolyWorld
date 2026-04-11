# 人間が用意するアセット TODO

開発者が用意する必要があるファイルの一覧です。
ファイルを用意したら ✅ をつけてください。

---

## 🔴 Phase 1 着手前に必要（最優先）

### 1. アプリアイコン・ロゴ

iOS / Android 用アプリアイコンと、UI 内（ログインモーダル等）で使用するロゴ画像。

| ✅ | ファイル名 | 配置先 | 用途 |
|---|---|---|---|
| ✅ | AppIcon.png | `LowPolyWorldUnity/Assets/Textures/UI/AppIcon.png` | OS アイコン用（背景色あり） |
| ✅ | AppLogo.png | `LowPolyWorldUnity/Assets/Textures/UI/AppLogo.png` | UI 内表示用（背景透明） |

**AppIcon.png 仕様:**
- サイズ: 1024×1024 px（Unity が各 OS 向けにリサイズ）
- 形式: PNG（アルファチャンネルなし・背景透明不可）
- 内容: アプリのブランドアイコン（背景色あり）

**AppLogo.png 仕様:**
- サイズ: 512×512 px 推奨（UI 内でスケール調整）
- 形式: PNG（アルファチャンネルあり・背景透明）
- 内容: AppIcon と同デザインのロゴマーク（背景色なし）。ログインモーダル等の UI 上に重ねて表示する

---

### 2. UIアイコンセット

アプリ全体で使用する共通UIアイコン。

**配置先:** `LowPolyWorldUnity/Assets/Textures/UI/Icons/`

| ✅ | ファイル名 | 用途 |
|---|---|---|
| ✅ | icon_nav_world.png | ナビゲーションバー「ワールド」タブ |
| ✅ | icon_nav_avatar.png | ナビゲーションバー「アバター管理」タブ |
| ✅ | icon_nav_worldmgr.png | ナビゲーションバー「ワールド管理」タブ |
| ✅ | icon_nav_shop.png | ナビゲーションバー「ショップ」タブ |
| ✅ | icon_nav_settings.png | ナビゲーションバー「設定」タブ |
| ✅ | icon_close.png | × 閉じるボタン |
| ✅ | icon_back.png | ← 戻るボタン |
| ✅ | icon_search.png | 検索 |
| ✅ | icon_heart.png | ♥ いいね（未いいね状態） |
| ✅ | icon_heart_filled.png | ♥ いいね（いいね済み状態） |
| ✅ | icon_more.png | … その他メニュー |
| ✅ | icon_lock.png | 🔒 ロック（プレミアム解約後スロット等） |
| ✅ | icon_bell.png | 🔔 通知ベル（未読バッジは UI コードで描画するため 1 種類のみ） |

**仕様:**
- サイズ: 128×128 px
- 形式: PNG（アルファチャンネルあり・背景透明）
- 色: **白単色**（アルファで形状を表現。Unity 側で Color プロパティを乗算してカラーリング）

---

### 3. システムSEファイル

アプリ基本 UI で使用するサウンドエフェクト。

**配置先:** `LowPolyWorldUnity/Assets/Audio/System/SE`

| ✅ | ファイル名 | 用途 |
|---|---|---|
| ✅ | se_button_tap.wav | 汎用ボタンタップ音 |
| ✅ | se_accept.wav | 決定操作音（確認モーダル等の決定時など、重要度の高い決定操作時に利用） |
| ✅ | se_cancel.wav | キャンセル操作音（確認モーダル等のキャンセル時に利用） |
| ✅ | se_enter_room.wav | ルーム入室通知音 |
| ✅ | se_exit_room.wav | ルーム退室通知音 |
| ✅ | se_notification.wav | アプリ内通知受信音 |
| ✅ | se_error.wav | エラー / 操作不可音 |

**仕様:**
- 形式: WAV（44.1kHz・16bit・モノラル）
- 長さ: 0.5 秒以内推奨
- 著作権: ロイヤリティフリー素材を使用（商用利用可）

---

### 4. Humanoidアニメーション

Humanoidリグに対応したアニメーションファイル。
**Mixamo**（https://www.mixamo.com/ 無料）からFBXでダウンロード推奨。

| ✅ | ファイル名 | 配置先 |
|---|---|---|
| ✅ | Idle.fbx | `LowPolyWorldUnity/Assets/Animations/Common/Idle.fbx` |
| ✅ | Walk.fbx | `LowPolyWorldUnity/Assets/Animations/Common/Walk.fbx` |
| ✅ | Run.fbx | `LowPolyWorldUnity/Assets/Animations/Common/Run.fbx` |
| ✅ | Jump.fbx | `LowPolyWorldUnity/Assets/Animations/Common/Jump.fbx` |

**Mixaroダウンロード時の設定:**
- Format: `FBX for Unity (.fbx)`
- Skin: `Without Skin`（アニメーションのみ）
- FPS: `30`

---

### 5. ブロブシャドウテクスチャ

アバター足元に投影する丸い影テクスチャ。

| ✅ | ファイル名 | 配置先 |
|---|---|---|
| ✅ | BlobShadow.png | `LowPolyWorldUnity/Assets/Textures/BlobShadow.png` |

**仕様:**
- サイズ: 128×128 px
- 形式: PNG（アルファチャンネルあり）
- 内容: 中心が白・周囲に向かって透明になる円形グラデーション
- 背景: 透明

> Photoshop / GIMP / Affinity Photo 等で「円形グラデーション（黒→透明）」を描くだけでOK。

---

### 6. テスト用ワールドGLB

Phase 1 の動作確認に使う簡易シーン。

| ✅ | ファイル名 | 配置先 |
|---|---|---|
| ✅ | TestWorld.glb | `LowPolyWorldUnity/Assets/StreamingAssets/Worlds/TestWorld.glb` |

**仕様:**
- 形式: GLB（バイナリGLTF）
- 内容: 高さ1、縦横8*8の直方体（ワールドとして扱う）
- スケール: Unityの1unit = 1mに合わせる（Blenderではエクスポート時に `Apply Transform` を有効にする）
- マテリアル: 何でもOK（ゲーム内ではUnlitシェーダーに差し替えます）
- ポリゴン数: 制限なし（テスト用のため）

> Blenderで平面を作りGLBエクスポートするだけでOK。

---

### 7. インワールドHUDアイコン

ルーム内（ワールドモード）の操作HUDで使用するアイコン。

**配置先:** `LowPolyWorldUnity/Assets/Textures/UI/Icons/`

| ✅ | ファイル名 | 用途 |
|---|---|---|
| ✅ | icon_hud_camera.png | HUD カメラボタン（撮影モードへ移行） |
| ✅ | icon_hud_menu.png | HUD ☰ メニューボタン |
| ✅ | icon_hud_jump.png | HUD ジャンプボタン |
| ✅ | icon_hud_sprint.png | HUD スプリントボタン |
| ✅ | icon_hud_action.png | HUD アクションボタン（ギミック接触時） |

**仕様:**
- サイズ: 128×128 px
- 形式: PNG（アルファチャンネルあり・背景透明）
- 色: **白単色**（アルファで形状を表現。Unity 側で Color プロパティを乗算してカラーリング）

---

## 🟡 Phase 2 着手前に必要

### 8. テスト用VRM 1.0アバター

アバターシステムの動作確認に使うVRMファイル。

| ✅ | ファイル名 | 配置先 |
|---|---|---|
| ✅ | Yuyu.vrm | `LowPolyWorldUnity/Assets/StreamingAssets/Avatars/Yuyu.vrm` |

**仕様:**
- 形式: VRM **1.0**（VRM 0.x は不可）
- ポリゴン: 512 tris 以下推奨（本番仕様の確認のため）
- ボーン: Humanoid・50本以内
- ファイルサイズ: 500KB以内推奨

**入手方法:**
- `LowPolyWorldUnity\Assets\3DModels\Avatars\Yuyu\Yuyu.fbx`のVRM書き出し版

---

## 🟢 Phase 4 着手前に必要

### 9. テスト用アクセサリGLB

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

---

### 10. テクスチャペイント機能アイコン

テクスチャペイント機能のツールバー（`docs/screens-and-modes.md` セクション 21.2）・カラーパレット UI で使用するアイコン。

**配置先:** `LowPolyWorldUnity/Assets/Textures/UI/Icons/`

| ✅ | ファイル名 | 用途 |
|---|---|---|
| ☐ | icon_eyedropper.png | カラーパレットのスポイトアイコン |
| ☐ | icon_tool_brush.png | ツールバー「ブラシ」 |
| ☐ | icon_tool_eraser.png | ツールバー「消しゴム」 |
| ☐ | icon_tool_fill.png | ツールバー「塗りつぶし」 |
| ☐ | icon_tool_shape.png | ツールバー「図形」 |
| ☐ | icon_tool_select.png | ツールバー「範囲選択」 |
| ☐ | icon_tool_move.png | ツールバー「移動・拡大縮小」 |
| ☐ | icon_tool_layer.png | ツールバー「レイヤー」 |
| ☐ | icon_tool_more.png | ツールバー「その他」 |

**仕様:**
- サイズ: 128×128 px
- 形式: PNG（アルファチャンネルあり・背景透明）
- 色: **白単色**（アルファで形状を表現。Unity 側で Color プロパティを乗算してカラーリング）

---

## 🟣 Phase 5 着手前に必要

### 11. 称号アイコン画像

ユーザー情報パネル・プロフィール画面に表示する称号アイコン（4 種）。

**配置先:** `LowPolyWorldUnity/Assets/Textures/UI/Titles/`

| ✅ | ファイル名 | 称号 |
|---|---|---|
| ☐ | title_premium.png | プレミアム |
| ☐ | title_shop_owner.png | ショップ開設者 |
| ☐ | title_developer.png | 開発者 |
| ☐ | title_staff.png | 運営関係者 |

**仕様:**
- サイズ: 64×64 px（ユーザー情報パネル内で小さく並んで表示される）
- 形式: PNG（アルファチャンネルあり・背景透明）
- 色: **白単色**（アルファで形状を表現。Unity 側で Color プロパティを乗算してカラーリング）
- 内容: 称号の種類が一目でわかるバッジ形式のアイコン

---

## 🟠 Phase 9 着手前に必要

### 12. ソーシャル・撮影機能アイコン

発話インジケーター・撮影機能（Phase 9）で使用するアイコン。

**配置先:** `LowPolyWorldUnity/Assets/Textures/UI/Icons/`

| ✅ | ファイル名 | 用途 |
|---|---|---|
| ☐ | icon_voice_indicator.png | 発話インジケーター（アバター頭上に表示） |
| ☐ | icon_verified.png | 公認バッジ チェックマーク（表示名の右横・アバター頭上名前タグに表示） |
| ☐ | icon_stamp_trash.png | スタンプ削除用ゴミ箱アイコン |
| ☐ | se_shutter.wav | シャッター音（撮影モードで撮影ボタンタップ時） |

**仕様:**
- サイズ: 128×128 px
- 形式: PNG（アルファチャンネルあり・背景透明）
- 色: **白単色**（アルファで形状を表現。Unity 側で Color プロパティを乗算してカラーリング）
- 発話インジケーターのみ: 演出として点滅するため、アニメーション用に複数フレームでなく単一アイコンでOK（点滅はコードで制御）

---

### 13. 多言語翻訳文字列（8言語）

アプリの全 UI 文字列を日本語・英語以外の 8 言語に翻訳したもの。
Unity Localization の String Table に取り込む CSV 形式で用意する。

**対象言語:**

| ✅ | 言語コード | 言語 |
|---|---|---|
| ☐ | zh-Hans | 中国語（簡体字） |
| ☐ | zh-Hant | 中国語（繁体字） |
| ☐ | ko | 韓国語 |
| ☐ | fr | フランス語 |
| ☐ | es | スペイン語 |
| ☐ | it | イタリア語 |
| ☐ | de | ドイツ語 |
| ☐ | pt-BR | ポルトガル語（ブラジル） |

**作業手順:**
1. Unity Localization の String Table から全キーと日本語・英語テキストを CSV エクスポート
2. CSV を各言語に翻訳（DeepL 等の機械翻訳 + ネイティブチェック推奨）
3. 翻訳済み CSV を `LowPolyWorldUnity/Assets/Localization/` 以下に言語コード別に配置

---

### 14. CJK 対応フォント

中国語（簡体字・繁体字）・韓国語を正しく表示するためのフォントファイル。
日本語・欧文は既存フォントで対応可。

| ✅ | ファイル名 | 配置先 |
|---|---|---|
| ☐ | NotoSansCJK-Regular.otf（または .ttf） | `LowPolyWorldUnity/Assets/Fonts/NotoSansCJK-Regular.otf` |
| ☐ | NotoSansCJK-Bold.otf（任意・太字用） | `LowPolyWorldUnity/Assets/Fonts/NotoSansCJK-Bold.otf` |

**推奨フォント:**
- **Noto Sans CJK**（Google Fonts / SIL Open Font License・無償）
  - https://fonts.google.com/noto/specimen/Noto+Sans+JP
  - 日本語・簡体字・繁体字・韓国語・ラテン文字を 1 フォントでカバー

**注意:** Unity の TextMeshPro で使用するため、フォントインポート後に TMP Font Asset を生成すること。

---

### 15. テキストスタンプ用フォント

文字入れスタンプ（撮影機能）で使用する装飾フォント。

**配置先:** `LowPolyWorldUnity/Assets/Fonts/Stamps/`

| ✅ | ファイル名 | 用途 |
|---|---|---|
| ☐ | StampFont_Regular.otf | スタンプ用フォント（通常） |
| ☐ | StampFont_Bold.otf（任意） | スタンプ用フォント（太字） |

**仕様:**
- 日本語・英数字・記号を含むこと（CJK 対応必須）
- ライセンス: 商用利用可・アプリへの同梱可のもの
- 推奨: **Noto Sans JP**（CJKフォントと同じファミリーで代用可）または装飾的な和文フォント
- Unity の TextMeshPro で使用するため、インポート後に TMP Font Asset を生成すること

---

### 16. スタンプ画像セット

撮影機能のスタンプとして使用する 2D 画像。

**配置先:** `LowPolyWorldUnity/Assets/UI/Stamps/`

| ✅ | ファイル名（例） | 内容 |
|---|---|---|
| ☐ | stamp_heart.png | ハートマーク |
| ☐ | stamp_star.png | 星 |
| ☐ | stamp_speech_bubble.png | 吹き出し（空白） |
| ☐ | stamp_crown.png | 王冠 |
| ☐ | stamp_rainbow.png | 虹 |
| ☐ | （プレミアム限定） stamp_premium_*.png | プレミアム限定スタンプ（種類は任意） |

**仕様:**
- 形式: PNG（アルファチャンネルあり・背景透明）
- サイズ: 256×256 px 推奨（UI 表示時にスケール調整されます）
- 最低点数: 無料 5 点以上・プレミアム限定 5 点以上
- ファイル名は `stamp_` プレフィックスで統一

---

## 🔵 Phase 12 着手前に必要

### 17. テスト用地形テクスチャ

Phase 12 の地形システム動作確認に使うテクスチャ。ランダム地形テクスチャ 2 種・固定地形テクスチャ 1 種を用意し、両形式・アトラス化・UV 選択ルールをすべて検証する。

管理画面からアップロードして地形種別として登録する（ファイル自体のローカル保管先は `Assets/StreamingAssets/TerrainTextures/`）。

| ✅ | ファイル名 | 種別 | 内容 |
|---|---|---|---|
| ☐ | terrain_grass.png | ランダム地形テクスチャ | 草 |
| ☐ | terrain_brick.png | ランダム地形テクスチャ | レンガ |
| ☐ | terrain_stone.png | 固定地形テクスチャ | 石 |

**仕様（ランダム地形テクスチャ）:**
- サイズ: **256 × 256** px
- 形式: PNG（アルファチャンネルあり可。透明ピクセルは保存時に α=0/RGB=黒 または α=255 に正規化）
- レイアウト: **8 × 8** グリッド（各セル 32 × 32 px）

| 行（下から） | 列 0〜3 | 列 4〜7 |
|---|---|---|
| 行 0（最下行） | 下面バリアント 0〜3 | 将来拡張用 |
| 行 1 | 坂側面下端 0〜3 | 坂側面 0〜3 |
| 行 2 | 側面上端下端 0〜3 | 側面上端下端 4〜7 |
| 行 3 | 側面下端 0〜3 | 側面下端 4〜7 |
| 行 4 | 側面 0〜3 | 側面 4〜7 |
| 行 5 | 側面上端 0〜3 | 側面上端 4〜7 |
| 行 6 | 上面中間 0〜3 | 上面中間 4〜7 |
| 行 7（最上行） | 上面 0〜3 | 上面 4〜7 |

- Filter Mode: **Point (no filter)**（Unity インポート設定で必ず指定）
- 内容ヒント:
  - **草**: 上面は緑、側面は緑と土のグラデーション、下面は土色
  - **レンガ**: 全面レンガパターン（バリアント間でずらし・ランダムつなぎ目）

> Aseprite / GIMP 等で 256 × 256 のドット絵を描き、8 × 8 グリッド（各 32 px）に面ごとのパターンを配置する。行 0 の列 4〜7 は空白（将来拡張用）のままでよい。バリアントが多いほど自然に見えるが、最低限各面バリアント 0 の 1 列だけ描いても動作確認は可能。

**仕様（固定地形テクスチャ）:**
- サイズ: **64 × 256** px
- 形式: PNG（アルファチャンネルあり可。透明ピクセルの扱いはランダム地形テクスチャと同様）
- レイアウト: **2 × 8** グリッド（各セル 32 × 32 px）

| 行（下から） | 列 0（左 32px） | 列 1（右 32px） |
|---|---|---|
| 行 0（最下行） | 下面 | 将来拡張用 |
| 行 1 | 坂側面 | 坂側面下端 |
| 行 2 | 側面上端下端 | 将来拡張用 |
| 行 3 | 側面下端 | 将来拡張用 |
| 行 4 | 側面 | 将来拡張用 |
| 行 5 | 側面上端 | 将来拡張用 |
| 行 6 | 上面中間 | 将来拡張用 |
| 行 7（最上行） | 上面 | 将来拡張用 |

- Filter Mode: **Point (no filter)**（Unity インポート設定で必ず指定）
- 内容ヒント:
  - **石**: 全面に石材の模様（均一感のある素材で固定テクスチャの向き固定の見え方を確認するのに適している）

> Aseprite / GIMP 等で 64 × 256 のドット絵を描き、32 px ごとのグリッドに面ごとのパターンを配置する。「将来拡張用」セルは空白のままでよい。

---

### 18. デフォルトワールドオブジェクト GLB セット（最小構成）

ワールド作成モードで最初から使用できるオブジェクトセット。
テストと本番の両方に使用するため、本番仕様で作成すること。

**配置先:** 管理画面からアップロードして登録（ファイル自体のローカル保管先は `Assets/StreamingAssets/WorldObjects/Default/`）

| ✅ | ファイル名（例） | 内容 | コライダー (W×D×H) | テクスチャ |
|---|---|---|---|---|
| ☐ | floor_1x1.glb | 床パネル 1×1 | 1×1×0.25 | 64×64 |
| ☐ | floor_2x2.glb | 床パネル 2×2 | 2×2×0.25 | 128×128 |
| ☐ | wall_1x2.glb | 壁 幅1×高さ2 | 1×0.25×2 | 64×64 |
| ☐ | wall_2x2.glb | 壁 幅2×高さ2 | 2×0.25×2 | 128×128 |
| ☐ | box_1x1x1.glb | ボックス 1×1×1 | 1×1×1 | 64×64 |
| ☐ | box_2x1x1.glb | ボックス 2×1×1 | 2×1×1 | 64×64 |
| ☐ | step_1x0.5.glb | 階段ステップ | 1×1×0.5 | 64×64 |
| ☐ | deco_tree.glb | 木（装飾） | 0×0×0 | 128×128 |
| ☐ | deco_rock.glb | 岩（装飾） | 0×0×0 | 64×64 |

**仕様（本番仕様で作成すること）:**
- 形式: GLB
- テクスチャ: Diffuse 1枚のみ（上記サイズ。16/32/64/128/256/512 のいずれか）
- マテリアル: 単色または簡易テクスチャ（ゲーム内で Unlit シェーダーに差し替え）
- スケール: 1 unit = 1m（Blender エクスポート時に `Apply Transform` を有効化）
- ピボット: 底面中心

> 最小構成として上記 9 点（物理 7 点・装飾 2 点）があればワールド作成の動作確認が可能。本番リリースに向けて種類を拡充する。

---

### 19. 環境音ライブラリ

ワールド作成設定で選択できる環境音（ループ再生・`docs/world-creation.md` セクション 13.2 参照）。

**配置先:** `LowPolyWorldUnity/Assets/Audio/Ambient/`

| ✅ | ファイル名 | soundId | 内容 |
|---|---|---|---|
| ☐ | ambient_forest.ogg | `forest` | 森・鳥のさえずり |
| ☐ | ambient_rain.ogg | `rain` | 雨音 |
| ☐ | ambient_ocean.ogg | `ocean` | 波音・海辺 |
| ☐ | ambient_wind.ogg | `wind` | 風音 |
| ☐ | ambient_city.ogg | `city` | 街のざわめき |
| ☐ | ambient_cave.ogg | `cave` | 洞窟・残響 |
| ☐ | ambient_night.ogg | `night` | 夜・虫の声 |

**仕様:**
- 形式: OGG（ループポイント設定必須・シームレスにつながること）
- 長さ: 30 秒以上推奨
- 著作権: ロイヤリティフリー素材（商用利用可）
- 推奨素材サイト: Freesound（https://freesound.org）・効果音ラボ等

---

### 20. 内蔵サウンドエフェクトライブラリ

ギミックの「音・音楽を鳴らす」反応で選択できる効果音・BGM ファイル。

**配置先:** `LowPolyWorldUnity/Assets/Audio/WorldGimmick/`

| ✅ | ファイル名（例） | 内容 |
|---|---|---|
| ☐ | se_coin.wav | コイン取得音 |
| ☐ | se_button.wav | ボタン・スイッチ音 |
| ☐ | se_door_open.wav | ドア開閉音 |
| ☐ | se_magic.wav | 魔法・エフェクト音 |
| ☐ | se_fanfare.wav | ファンファーレ・クリア音 |
| ☐ | se_damage.wav | ダメージ・失敗音 |
| ☐ | se_footstep.wav | 足音 |
| ☐ | bgm_cheerful.ogg | 明るい BGM（ループ） |
| ☐ | bgm_calm.ogg | 落ち着いた BGM（ループ） |
| ☐ | bgm_suspense.ogg | シリアス系 BGM（ループ） |

**仕様:**
- 形式: SE は WAV（44.1kHz・16bit）、BGM は OGG（ループポイント設定推奨）
- 著作権: ロイヤリティフリー素材を使用（商用利用可・クレジット表記不要のもの推奨）
- 推奨素材サイト: Freesound（https://freesound.org）・効果音ラボ（https://soundeffect-lab.info）等
- 最低点数: SE 5 点以上・BGM 2 点以上

---

## 🔴 ローンチ前に必要（コンテンツ）

### 21. プリセットアバター

新規ユーザーが最初から利用できるデフォルトアバター（`docs/screens-and-modes.md` セクション 3.3「デフォルト」タブに表示される）。管理画面からアップロードして登録する。

**配置先:** 管理画面からアップロードして登録（ファイル自体のローカル保管先は `Assets/StreamingAssets/Avatars/Presets/`）

| ✅ | ファイル名（例） | 内容 |
|---|---|---|
| ☐ | preset_avatar_01.vrm | プリセットアバター 1 体目 |
| ☐ | preset_avatar_02.vrm | プリセットアバター 2 体目 |
| ☐ | （必要に応じて追加） | |

**仕様（本番仕様と同じ制限で作成すること）:**
- 形式: VRM **1.0**（VRM 0.x 不可）
- ポリゴン: 512 tris 以下
- ボーン: Humanoid・50 本以内
- ファイルサイズ: 500KB 以内
- Edit OK 扱い（全ユーザーがテクスチャ編集可能）
- 最低点数: 1 体以上（新規ユーザーが入室できる状態にするため）

---

### 22. プリセットアクセサリ

全ユーザーが最初から利用できるデフォルトアクセサリ（`docs/screens-and-modes.md` セクション 3.4「デフォルトプリセット」タブに表示される）。管理画面からアップロードして登録する。

**配置先:** 管理画面からアップロードして登録（ファイル自体のローカル保管先は `Assets/StreamingAssets/Accessories/Presets/`）

| ✅ | ファイル名（例） | アタッチポイント | 内容 |
|---|---|---|---|
| ☐ | preset_acc_head_01.glb | Head | 頭部アクセサリ 1 種目 |
| ☐ | preset_acc_chest_01.glb | Chest | 胸部アクセサリ 1 種目 |
| ☐ | （必要に応じて追加） | | |

**仕様（本番仕様と同じ制限で作成すること）:**
- 形式: GLB
- テクスチャ: 最大 64×64 px（Diffuse 1 枚のみ）
- ファイルサイズ: 100KB 以内
- スケール: 1 unit = 1m
- 最低点数: 2 種以上推奨

---

## 🔴 ローンチ前に必要（法的要件）

### プライバシーポリシー・利用規約

**作成方法（推奨）:** Termly / iubenda 等の自動生成 SaaS を利用し、以下の要点を必ず含めること。

---

#### 利用規約に必要な要点

| ✅ | 項目 | 内容 |
|---|---|---|
| ☐ | 対象年齢 | 13 歳以上を対象とすること。13 歳未満は利用不可である旨 |
| ☐ | 禁止事項 | 他ユーザーへのハラスメント・なりすまし・不正アクセス・違法コンテンツのアップロード等 |
| ☐ | UGC ポリシー | ユーザーがアップロードしたアバター・ワールドの著作権帰属と、運営がモデレーション目的でコンテンツを削除できる権限 |
| ☐ | コイン・課金 | コインは返金不可（法定通貨への換金不可）・プレミアム解約後の残コインの扱い・購入完了後のキャンセルポリシー |
| ☐ | アカウント停止 | 違反時の BAN・制限措置の権限と、異議申し立て手段（お問い合わせ先） |
| ☐ | サービス変更・終了 | 運営都合でのサービス変更・終了時の通知方法と未使用コインの扱い |
| ☐ | 免責事項 | ユーザー間トラブルへの不介入・UGC コンテンツへの免責 |
| ☐ | 準拠法・管轄 | 日本法準拠・東京地裁を専属合意管轄として明示 |

---

#### プライバシーポリシーに必要な要点

| ✅ | 項目 | 内容 |
|---|---|---|
| ☐ | 取得する情報 | Google / Apple アカウント情報（メールアドレス・プロバイダー ID）、表示名・@name、IPアドレス・ユーザーエージェント、購入履歴、アップロードコンテンツ（VRM・GLB・PNG）、生年月日（年齢確認用） |
| ☐ | 利用目的 | サービス提供・本人確認・不正利用防止・統計分析（個人を特定しない形） |
| ☐ | 第三者提供 | Google Cloud（コンピュート・DB）、Cloudflare（ストレージ・CDN）、Unity Gaming Services（Relay・Voice Chat）の各サービスへのデータ転送と、各プロバイダーのプライバシーポリシーへのリンク |
| ☐ | 保存期間 | アカウントデータ：退会後 30 日でソフトデリート・30 日後に物理削除 / 購入履歴・違反報告受信記録：法的要件に基づき 7 年保持 / 監査ログ：1 年保持 |
| ☐ | 削除・開示請求 | ユーザーが自分の情報の開示・訂正・削除を申請できる手段（お問い合わせ先）と対応期間（30 日以内） |
| ☐ | Cookie / トラッキング | アプリ内でのデータ収集方式（ネイティブアプリのため Cookie 不使用である旨、または使用する場合はその目的） |
| ☐ | 未成年者 | 13 歳未満は利用不可。万が一 13 歳未満のデータを取得した場合は速やかに削除する旨 |
| ☐ | お問い合わせ先 | nibankougen@gmail.com |

---

#### 公開・配置

| ✅ | 作業 | 内容 |
|---|---|---|
| ☐ | URL を確定する | 例: `https://lowpolyworld.example.com/terms` / `https://lowpolyworld.example.com/privacy` |
| ☐ | `Assets/Scripts/Constants.cs` の仮 URL を置き換える | `TermsOfServiceUrl` / `PrivacyPolicyUrl` |
| ☐ | アプリストア申請画面にも同じ URL を登録する | App Store Connect / Google Play Console の「プライバシーポリシー URL」欄 |
| ☐ | App Store の年齢レーティングを **17+** に設定する | 音声チャット・UGC を含むため |
| ☐ | Google Play のコンテンツレーティングを申請する | 「コンテンツの評価」アンケートで音声チャット・UGC を申告 |
