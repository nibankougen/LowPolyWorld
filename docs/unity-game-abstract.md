# Low-poly world Unityクライアント仕様書 v1.0

## 1. 目的

本アプリはモバイル向け軽量UGCアバター空間サービスである。
ユーザーはアバターを読み込み、ワールドに配置し、他ユーザーと音声会話および空間共有ができる。

Unityクライアントは以下を責務とする：

* アバター表示
* ワールド表示
* プレイヤー移動同期
* 音声会話
* テクスチャ改変
* ネットワーク同期
* フレンド管理
* 撮影機能（スクリーンショット・2D画像フィルター・スタンプ）
* 多言語対応（10言語）
* ワールド作成（グリッド配置・ギミックエディタ・公開管理）


## 2. 対象プラットフォーム

* iOS
* Android
* PC（Windows / Mac）

前提条件：

* パフォーマンスおよびUIはiOS/Androidに最適化する
* **画面向き**: 縦画面（Portrait）固定で設計する
* モバイルGPU性能を考慮
* 低メモリ環境対応
* 60fps目標（最低30fps）


## 3. 描画仕様

### 3.1 シェーダー

アバターおよびワールドのマテリアルは以下に固定：

* 両面描画Unlit Shader
* 影なし
* ライティングなし
* 半透明禁止
* カットアウトのみ許可

理由：
ドローコール削減・GPU負荷軽減


### 3.2 接地影

接地影は以下方式：

* 円形影テクスチャを地面に投影
* スケール調整のみ
* Real-time shadow 不使用


### 3.3 テクスチャ仕様

キャラクター：
* 1キャラ = 256x256 Atlas領域

アクセサリ：
* 1アクセサリ = 最大64x64 Atlas領域
* 最大96スロット（24キャラ × 4個）

共通：
* ミップマップ生成必須
* フィルタリング：Bilinear
* 圧縮：ASTC 6x6


### 3.4 テクスチャ統合方式

Atlasは固定スロット方式・キャラクターとアクセサリを1枚のテクスチャに統合する：

**Atlasレイアウト（1024x2048）：**
* 上段 (1024x1536)：キャラクター24スロット（4列x6行 @256x256）
* 中段 (1024x384)：アクセサリ96スロット（16列x6行 @64x64）
* 下段 (1024x128)：将来拡張用

1テクスチャ・1マテリアルで統一することでキャラクターとアクセサリを同一ドローコールで描画する。

**スロット間パディング：**
* 各スロットの周囲に2〜4pxの余白を確保する
* ミップマップ生成時のアトラスブリード（隣接スロットへの色の滲み出し）を防ぐため必須
* UV座標はパディングを除いた内側領域を参照するよう調整する

実装方式：
* 空きスロットにテクスチャコピー
* RenderTexture経由で合成
* 再生成は非同期処理

Atlas更新は：

* キャラ追加・削除・変更時
* アクセサリ追加・削除・変更時

## 4. アバター仕様

### 4.1 フォーマット

ファイルの読み込みにはVRM 1.0 を採用。ただしアップロード時にはサーバー側で不要なデータを削除することにします。

理由：

* 人型制約あり
* ボーン構造統一
* アニメーション互換性確保

### 4.2 容量制限

1アバター最大：
モデル・テクスチャ合計：500KB以内

### 4.3 ポリゴン制限

* 最大512 tris
* 推奨256 tris


### 4.4 ボーン制限

* Humanoid必須
* 50ボーン以内

### 4.4.1 アバターアップロード バリデーションエラーの表示

VRM アップロード後、サーバーがバリデーションエラーを返した場合（`GET /api/v1/jobs/{id}` の `status: failed`）、アップロード画面に以下のエラーパネルを表示する。

**エラーコードのローカライズマッピング:**

| field | code | 日本語メッセージ |
|---|---|---|
| `file_size` | `too_large` | ファイルサイズが上限（500KB）を超えています |
| `polygon_count` | `too_many` | ポリゴン数（トライアングル数）が上限（512）を超えています |
| `bone_count` | `too_many` | ボーン数が上限（50）を超えています |
| `rig_type` | `not_humanoid` | Humanoid リグが必要です |
| `vrm_version` | `unsupported` | VRM 1.0 形式のみ対応しています |
| （上記以外） | — | 「アバターの検証に失敗しました。モデルを確認してください。」（フォールバック） |

- 複数のエラーが返された場合はすべてのメッセージを列挙する
- `message` フィールド（英語補足）は UI に表示しない
- エラーパネルの下に**「別のファイルを選択」ボタン**を配置し、ファイルピッカーを再度開けるようにする（同じジョブをリトライする機能は不要。バリデーションエラーはファイルを修正しない限り解消しないため）

## 4.5 アクセサリ仕様

### 4.5.1 概要

アバターに対してアクセサリを付与できる。アクセサリはアバターの改変要素であり、ユーザーがアップロード・編集できる。

### 4.5.2 フォーマット

* GLBファイルベース
* テクスチャ：最大64x64（Diffuse 1枚のみ）
* ポリゴン数：最大128 tris

### 4.5.3 アタッチポイント

Humanoidボーンに直接アタッチする：

| アタッチポイント | 対応ボーン |
|---|---|
| 頭 | Head |
| 腕（左） | LeftLowerArm |
| 腕（右） | RightLowerArm |
| 胸 | Chest |
| 脚（左） | LeftUpperLeg |
| 脚（右） | RightUpperLeg |

### 4.5.4 制限

* 1アバターにつき最大4個まで
* 容量制限：GLB + テクスチャ合計 100KB以内

### 4.5.5 アクセサリアップロード バリデーションエラーの表示

アクセサリ GLB アップロード後、サーバーがバリデーションエラーを返した場合（`GET /api/v1/jobs/{id}` の `status: failed`）、アップロード画面に以下のエラーパネルを表示する。

**エラーコードのローカライズマッピング:**

| field | code | 日本語メッセージ |
|---|---|---|
| `file_size` | `too_large` | ファイルサイズが上限（100KB）を超えています |
| `polygon_count` | `too_many` | ポリゴン数（トライアングル数）が上限（128）を超えています |
| `texture_size` | `too_large` | テクスチャサイズが上限（64×64）を超えています |
| `texture_format` | `disallowed` | テクスチャのファイル形式が不正です |
| `mesh_depth` | `too_deep` | モデルの階層構造が複雑すぎます |
| `glb_format` | `invalid` | GLB ファイルの形式が不正です |
| （上記以外） | — | 「アクセサリの検証に失敗しました。ファイルを確認してください。」（フォールバック） |

- 複数のエラーが返された場合はすべてのメッセージを列挙する
- `message` フィールド（英語補足）は UI に表示しない
- エラーパネルの下に**「別のファイルを選択」ボタン**を配置する

### 4.5.6 描画

* マテリアルはキャラクターと同一（カスタムUnlitシェーダー）
* テクスチャはキャラクターAtlasの下段スロット（64x64）を使用
* キャラクターと同一ドローコールで描画

### 4.5.6 テクスチャ改変

* 64x64のためドット絵スタイルで編集可能
* テクスチャペイント機能（セクション8参照）の対象
* 通常レイヤー最大16枚 + 色調補正レイヤー1枚（別枠）・PNG保存
* Edit OK/NG フラグはアクセサリには設定しない（常に編集可能）

## 5. アニメーション

* Humanoidリターゲット使用
* 共通アニメーションセットを再利用

同期対象：

* 位置
* 回転
* 再生アニメーションID
* アニメーション時間

## 6. ネットワーク同期

ネットワーク同期はNetcode for GameObjectsを利用

同期内容：

* プレイヤー座標
* アニメーション状態（変更時）
* 利用アバター変更イベント（変更時）
* ワールドイベント（発生時）

補間：

* クライアント側線形補間

位置・回転の送信制御（Unity Relay 従量課金削減）：

* 最大送信レート: 20Hz（50ms 間隔）
* 前回送信値からの変化量が閾値（位置 > 0.01m または 回転 > 1°）を超えた場合のみ送信
* 静止中のプレイヤーは送信なし・受信側はクライアント補間で滑らかさを維持

### 通信暗号化仕様

| コンポーネント | プロトコル | 備考 |
|---|---|---|
| Unity Transport（Netcode for GameObjects） | **DTLS 1.2**（UDP 暗号化） | `UnityTransport.UseEncryption = true` を設定して有効化。Relay サーバーは暗号化済みトラフィックを中継するのみで復号しない |
| Vivox シグナリング（ログイン・チャンネル参加） | **TLS 1.2+** | Vivox SDK が内部で処理。アプリ側で証明書管理不要 |
| Vivox 音声メディア | **SRTP**（DTLS-SRTP 鍵交換） | Vivox SDK が内部で処理 |

> Unity Relay・Vivox ともにマネージドサービスが暗号化を担保する。アプリ実装で必要な設定は `UnityTransport.UseEncryption = true` のみ。

### ホスト側移動速度サニティチェック

ルームのホスト（Netcode for GameObjects のサーバーロール）は、クライアントから受信した位置更新を適用前に検証する。実装は `PositionSanityCheckLogic`（ロジッククラス）に委譲する。

**速度チェック:**

```
MAX_SPEED_THRESHOLD = 12.0 m/s  // プレイテスト後に調整可

delta_distance = Vector3.Distance(last_valid_position, received_position)
delta_time     = received_timestamp - last_position_timestamp

if delta_time > 0 && delta_distance / delta_time > MAX_SPEED_THRESHOLD:
    violation_count[client_id] += 1
    // 位置更新を破棄し最後の有効位置を維持
else:
    violation_count[client_id] = 0
    last_valid_position = received_position
```

**Y 軸（落下）チェック:**

ワールド定義の最低 Y 座標 − 10m を下回る座標は無効として、直前の有効位置にリセットする。

**連続違反時の処理（3回連続で速度超過）:**

```
if violation_count[client_id] >= 3:
    1. 対象クライアントをルームからキック（Netcode 切断）
    2. API へ速度チート自動通報を起票（reporter_id = ホストのユーザーID、is_auto_generated = true）
       reason: "speed_cheat_auto"、evidence に最大超過速度・違反回数を含める
    3. violation_count[client_id] = 0
```

自動通報は `violation_reports` テーブルに `is_auto_generated = true` フラグで記録される。管理画面では通常の通報と区別して表示し、トラストレベル計算での重みを低く設定する。

### ギミック状態変更の二重検証

ルームオーナー（ホスト）が送信するギミック状態変更は、以下の2層で独立して検証する。

**第1層: ホスト（`GimmickEngine`）側でブロードキャスト前に検証**

`GimmickEngine` はギミックアクションを他クライアントへブロードキャストする前に、ローカルのワールド定義 JSON と照合する:

- ギミック ID がワールド定義に存在するか
- アクション種別が定義されたアクションリストに含まれるか
- トリガー条件の構造が整合しているか

検証を通過しないギミック状態は他クライアントへ送信しない。

**第2層: 受信クライアント側で適用前に独立検証**

ホストから受信したギミック状態変更を、受信クライアントが保持するワールド定義 JSON で独立して照合してから適用する:

- ギミック ID がワールド定義に存在するか
- 受信したアクション種別が許可されているか

検証失敗時はそのギミック状態変更を無視し、ローカルには適用しない（ログに WARN を記録）。

> 悪意あるルームオーナーへの対策は第2層のみが有効。第1層はバグ防止・第2層はセキュリティ防護として役割が異なる。

## 7. 音声通信

音声はvivoxで実装：

* 3D位置音声
* 距離減衰あり
* ワールド単位チャンネル

Unity側責務：

* プレイヤー座標更新
* SDKへ位置通知

## その他通信

アバターを同期させるためのファイル取得にはAPIサーバーを利用。

### HTTP転送時圧縮

APIサーバーはファイル配信エンドポイントで `Content-Encoding: gzip` によるレスポンス圧縮を行う。

- 対象: VRM・GLB・PNG・JSON など全レスポンス
- Goサーバー側: `compress/gzip` でレスポンスを圧縮して返す
- Unityクライアント側: `UnityWebRequest` が `Content-Encoding: gzip` を自動解凍するため実装変更不要
- ストレージ上のファイル自体は非圧縮のまま保存する（解凍処理の複雑化・ロード時間増加を避けるため）

### アセットキャッシュシステム

クライアントはアセットをローカルに保存し、不要なダウンロードを抑制する。

#### 永続キャッシュ（自分のアセット）

- 対象: 自分のアバター（VRM）・アクセサリ（GLB・テクスチャ）・マイワールドオブジェクト（GLB）・ワールドオブジェクトカスタマイズテクスチャ・各レイヤー画像
- 保存先: `Application.persistentDataPath`（OSのアプリサンドボックス内）
- アプリ削除まで保持

#### 一時キャッシュ（他人のアセット）

- 対象: 同じルームにいる他ユーザーのアバター・テクスチャ統合画像・ワールドGLB・ワールドアトラステクスチャ
- 保存先: `Application.temporaryCachePath`（OSのキャッシュ領域）
- TTL: 最終アクセスから一定期間経過後に自動削除

#### ハッシュベース更新チェック

- サーバーはアセットごとに SHA-256 ハッシュを保存し、アセットメタデータ API のレスポンスに含める
- クライアントはキャッシュ済みファイルのハッシュをローカルに記録
- アセット取得時: サーバーハッシュ ＝ ローカルハッシュ → キャッシュを使用 / 不一致 → 再ダウンロード
- 対象: アバター VRM・アクセサリ GLB・テクスチャ統合画像・ワールドオブジェクト GLB・ワールドアトラス PNG

#### CDN 配信とコンテンツアドレス型 URL

- VRM・GLB・PNG・ワールドアトラス・サムネイルなどの静的ファイルは CDN から配信する
- ファイル名はコンテンツの SHA-256 ハッシュ（例: `abc123def.vrm`）を使用し `Cache-Control: immutable` を設定する
  - ファイルが更新された場合のみ新しい URL が生成されるため、CDN キャッシュを最大限活用できる
  - `CacheManager` は URL の変化をキャッシュ更新のトリガーとして使用できる（URL 一致 = 更新なし）
- アセット URL は認証済みの API レスポンスにのみ含まれる（URL 自体はコンテンツハッシュのため推測困難）

#### セキュリティ

- アセット URL は認証済み API レスポンスにのみ含まれる（URL 自体はコンテンツハッシュのため推測困難）
- キャッシュは OS のアプリサンドボックス内に保存（外部アプリからの直接アクセスを防止）

## 8. テクスチャ改変機能

### 8.1 編集対象

| 対象 | 解像度 | テクスチャ区分 |
|---|---|---|
| アバターDiffuse | 256×256 | なし（1テクスチャ） |
| アクセサリDiffuse | 64×64 | なし（1テクスチャ） |
| ワールドオブジェクトDiffuse | 16×16 〜 512×512（オブジェクト種別ごとに固定） | なし（1テクスチャ） |

**半透明ピクセル禁止：** 全ピクセルのアルファ値は 0（完全透明）または 255（完全不透明）のみ許可。中間値は保存時に丸める。

**編集可否フラグ（Edit OK / Edit NG）:**
- ショップで販売するアバターには Edit OK / Edit NG フラグが設定される
- Edit NG のアバターはテクスチャ編集不可（アクセサリ取り付けは可）
- Edit OK のアバターはレイヤーを含む全テクスチャ編集が可能
- プリセット・ローカルVRMファイルは常に Edit OK 扱い
- 詳細は `docs/screens-and-modes.md` セクション3.7 参照

### 8.2 UIレイアウト（縦画面）

```
┌─────────────────────────┐
│   3Dプレビュー（上部）    │  ← アバター/アクセサリ/オブジェクトをリアルタイムプレビュー
├─────────────────────────┤
│                         │
│  2D キャンバス（編集エリア） │  ← ピンチイン・アウトで最大 ×8 程度まで拡大縮小
│                         │
├─────────────────────────┤
│  テクスチャ編集バー        │  ← ツールアイコン群（色選択・ブラシ等）
└─────────────────────────┘
```

* 縦画面時、画面上部に3Dプレビューを常時表示
* レイヤーパネルはテクスチャ編集バーの「レイヤー」ボタンで開閉するパネル（常時表示ではない）
* UI詳細は `docs/screens-and-modes.md` セクション21参照

### 8.3 ペイントツール

テクスチャ編集バー（詳細テクスチャモード）に配置されるツール群。詳細UI仕様は `docs/screens-and-modes.md` セクション21参照。

| ツール | 動作 |
|---|---|
| **色選択** | 色選択パネルを開く（色相サークル + 明度彩度四角形 + 透明度 + RGBA入力 + 色履歴16色） |
| **ブラシ** | 指定色で塗る。種類: 円形（アンチエイリアスなし・デフォルト）/ 円形（アンチエイリアスあり）。サイズ 1〜255px |
| **消しゴム** | 対象ピクセルを完全透明（α=0）に戻す |
| **塗りつぶし** | 連続した同色領域をフラッドフィル。設定: 他レイヤー参照・許容値 |
| **図形** | スライド範囲に図形を描画。種類: 四角形 / 円 / 直線 |
| **範囲選択** | 矩形・楕円・塗りつぶし範囲で選択。複数選択・コピー＆ペースト対応 |
| **移動・拡大縮小** | 現在のレイヤーを移動・拡大縮小。補間: Nearest Neighbor（カクカク）/ Bicubic（なめらか） |
| **レイヤー** | レイヤーパネルを開く |
| **その他** | 画像書き出し・テクスチャサイズ変更・簡単テクスチャ切り替え |

### 8.4 レイヤー仕様

#### 通常レイヤー

* 最大16枚（アバター・アクセサリ・ワールドオブジェクト共通）
* 各レイヤーは独立した画像データ（PNG形式）
* 不透明度・表示/非表示・ブレンドモード（通常のみ）設定可能
* レイヤーグループにまとめることが可能（グループはネスト可）

#### 色調補正レイヤー（効果レイヤー）

* 1テクスチャにつき1枚のみ追加可能（通常レイヤーの16枚制限とは別枠）
* ピクセル描画不可。パラメータのみ: 明度 / 彩度 / コントラスト / 色相シフト
* レイヤースタックの任意の位置に挿入可能（その位置より下のレイヤーへ適用）

#### UV レイヤー（特殊・枚数制限外）

* レイヤー一覧の最上段に固定。UV レイアウトの枠線を表示するオーバーレイ
* 表示・非表示の切り替えのみ可能（描画不可）
* 枠線色はデフォルト `#80808080`。ユーザーが変更可能

#### ベースレイヤー（特殊・枚数制限外）

* レイヤー一覧の最下段に固定。不透明 / 透明を 2 値（0/1）で管理するマスクレイヤー
* ブラシで描くと色によらず不透明（α=255）になる。消しゴムで透明（α=0）になる。半透明不可
* 最終出力 = 通常レイヤー合成結果 × ベースレイヤーマスク（出力に半透明ピクセルは発生しない）
* デフォルト: 全面不透明

#### 透明ピクセルのRGB固定

* α = 0（完全透明）のピクセルは RGB を黒（0, 0, 0）に固定する
* 保存時に自動適用（ASTC 圧縮での容量削減が目的）
* 半透明ピクセルの丸め処理と合わせて: α < 128 → α=0・RGB=(0,0,0)、α ≥ 128 → α=255・RGB 保持

#### レイヤー操作

| 操作 | 概要 |
|---|---|
| **下のレイヤーにマスク** | このレイヤーのアルファを直下のレイヤーのマスクとして適用する |
| **レイヤー結合** | 選択した複数レイヤーを1枚のラスターレイヤーに統合する |
| **並び替え** | ドラッグでスタック順を変更・グループへの出し入れ |
| **複製 / 削除** | 任意のレイヤーを複製または削除 |
| **レイヤーロック** | そのレイヤーへの描画操作をロックする |
| **画像取り込み** | ファイルから読み込んで新規レイヤーとして追加（Bicubic リサイズ対応） |

### 8.5 Undo / Redo

* 20 段階（`docs/screens-and-modes.md` セクション 20.3 参照）
* テクスチャタブ内の操作（ブラシ・消しゴム・レイヤー操作等）が対象
* テクスチャタブを離れると履歴はリセット

### 8.6 テクスチャモード

* **詳細テクスチャモード**: フルペイント機能（デフォルト）
* **簡単テクスチャモード**: プリセット選択のみのシンプルモード（詳細仕様別途定義）
* 切り替え: 「その他」メニュー →「簡単テクスチャを使う」（カスタム内容は削除・デフォルトテクスチャ「ブロック」が適用）

### 8.7 保存フォーマットとアップロード仕様

保存時に以下3種類のデータをAPIサーバーへアップロードする：

#### ① レイヤー画像（編集済みのもののみ）

* 対象：今回のセッションで変更されたレイヤーのみ
* 形式：PNG（256×256 または 64×64）
* ファイル名例：`avatar_{id}_layer_{n}.png` / `accessory_{id}_layer_{n}.png` / `worldobj_{objectTypeId}_layer_{n}.png`
* 色調補正レイヤーはパラメータJSONに含めるためアップロード不要

#### ② レイヤー構造JSON

テクスチャのレイヤー構造・設定を記述するJSONを1ファイルアップロードする。

```json
{
  "version": 2,
  "layers": [
    {
      "id": "group_0", "type": "group", "visible": true, "opacity": 1.0,
      "children": [
        { "id": "layer_0", "type": "raster", "visible": true, "opacity": 1.0, "url": "...", "mask_below": false },
        { "id": "layer_1", "type": "raster", "visible": true, "opacity": 0.8, "url": "...", "mask_below": true }
      ]
    },
    { "id": "layer_2", "type": "color_adjustment", "brightness": 0.1, "saturation": -0.2, "contrast": 0.0, "hue_shift": 0.0 }
  ]
}
```

* UV レイヤー・ベースレイヤーは常に存在するシステム層のため JSON には含まない
* `mask_below: true` = このレイヤーを直下のレイヤーのマスクとして適用（「下のレイヤーにマスク」操作）
* `type: "group"` はネスト構造を持つ。グループ内の children は上記と同じ構造
```

#### ③ 統合画像（256×256 または 64×64 または 16〜512px）

* 全レイヤーを合成した最終画像を1枚アップロード
* アバター・アクセサリ: ワールドプレイ時はこの統合画像を Atlas に貼り付けて使用する
* ワールドオブジェクト: ワールド保存時のサーバー側アトラス生成にカスタムテクスチャとして使用される（同一種別にはこの統合画像が適用される）
* 保存時の自動処理: α < 128 → α=0 かつ RGB=(0,0,0)（黒固定）、α ≥ 128 → α=255（RGB 保持）

### 8.8 実装方式

Unityネイティブではなく、ネイティブRustライブラリ（`paint-engine/`）をP/Invoke経由で呼び出す：

* Unity側: キャンバスデータ・操作コマンドをネイティブライブラリへ送信
* ネイティブ側: レイヤー合成・Undo管理・GPU処理最適化を担当
* 完成画像（PNG）をUnityへ返却しAtlasに反映

理由：

* 高度なレイヤー管理（マスク・色調補正レイヤー含む）
* Undo履歴管理（モバイルでも安定動作）
* GPU処理最適化（モバイルメモリ節約）

### 8.9 編集対象別の差異

| 項目 | アバター編集 | アクセサリ編集 | ワールドオブジェクト編集 |
|---|---|---|---|
| テクスチャサイズ | 256×256（区分なし） | 64×64（区分なし） | 16〜512px（種別ごとに固定） |
| 通常レイヤー数上限 | 最大16枚 | 最大16枚 | 最大16枚 |
| 色調補正レイヤー | 1枚（通常レイヤーとは別枠） | 1枚（通常レイヤーとは別枠） | 1枚（通常レイヤーとは別枠） |
| UV表示 | あり | あり | あり |
| ツール | 鉛筆・ブラシ・塗りつぶし・消しゴム | 同じ（ドット絵スタイル） | 同じ |
| 保存形式 | 同じ（サイズが異なるのみ） | 同じ | 同じ |
| 改変スコープ | ユーザースロット単位 | ユーザーアクセサリスロット単位 | ワールドごとに1カスタマイズ（保存バリアントとして他ワールドでも再利用可・スロット10/100） |

### 8.10 Native プラグインビルド・配置

paint-engine（Rust cdylib / staticlib）のビルド成果物を以下に配置する。Unity はフォルダ名でプラットフォームを自動認識する。

```
Assets/Plugins/
  iOS/
    libpaint_engine.a          # staticlib (aarch64-apple-ios)
  Android/
    arm64-v8a/
      libpaint_engine.so       # cdylib (aarch64-linux-android)
    armeabi-v7a/
      libpaint_engine.so       # cdylib (armv7-linux-androideabi)
  Windows/x86_64/
    paint_engine.dll           # cdylib (x86_64-pc-windows-msvc)
  macOS/
    libpaint_engine.bundle     # cdylib (aarch64-apple-darwin + x86_64-apple-darwin ユニバーサル)
```

各ファイルの Unity Inspector（.meta）でプラットフォーム設定を明示する（CPU / OS を正しく指定しないとビルドに含まれない）。

#### P/Invoke 宣言

iOS は staticlib のため `DllImport` のライブラリ名が異なる。

```csharp
#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
#else
    [DllImport("paint_engine")]
#endif
private static extern IntPtr paint_engine_create();
```

#### ビルドスクリプト

`paint-engine/` ディレクトリに `build.sh`（または `Makefile`）を用意し、各ターゲットをビルドして `Assets/Plugins/` へコピーする運用とする。

#### プラグインバイナリの整合性保護

バイナリは `Assets/Plugins/` にコミットして管理する。以下の2層でサプライチェーン攻撃を抑止する。

**1. SHA-256 チェックサムファイル**

`Assets/Plugins/plugin_checksums.sha256` に各バイナリの SHA-256 ハッシュを記録する。

```
<hash>  iOS/libpaint_engine.a
<hash>  Android/arm64-v8a/libpaint_engine.so
<hash>  Android/armeabi-v7a/libpaint_engine.so
<hash>  Windows/x86_64/paint_engine.dll
<hash>  macOS/libpaint_engine.bundle
```

- バイナリを更新するたびにチェックサムも同じコミットで更新する
- GitHub Actions の CI でバイナリ更新時に自動照合し、不一致はビルド失敗とする
- チェックサムファイル自体の改ざんはブランチ保護（後述）で防ぐ

**2. GitHub ブランチ保護**

`main` ブランチへの直接プッシュを禁止し、すべての変更を Pull Request 経由とする。
これにより、`Assets/Plugins/` のバイナリとチェックサムファイルを同時に書き換えるには PR レビューの突破が必要になる。

**プラットフォーム別の既存保護（追加対応不要）:**

| プラットフォーム | 保護の根拠 |
|---|---|
| iOS | App Store コード署名がアプリ全体（.a 含む）を保護 |
| Android | APK 署名 + Google Play 署名で配布後の改ざんを検知 |
| macOS | Gatekeeper + Notarization |
| Windows | 副次的ターゲットのため追加のコード署名は行わない |

**ライブラリロード時の検証（追加実装不要の根拠）:**

| プラットフォーム | ロード時の動作 | 追加検証の要否 |
|---|---|---|
| iOS | `.a` は静的ライブラリ。ビルド時にアプリ本体バイナリに統合され、実行時に別途ロードされない。App Store 署名はアプリバイナリ全体（`.a` の内容を含む）を保護するため、実行時に差し替えは不可能 | **不要** |
| Android | `.so` は署名済み APK 内の `lib/` に格納。Unity ランタイムが APK 内から直接ロードする。APK 署名を破らずに `.so` を差し替えることは不可能（Google Play 署名により配布後の改ざんも検知される） | **不要** |
| macOS | `.bundle` は Gatekeeper + Notarization で保護されたアプリバンドル内に存在 | **不要** |
| Windows | DLL は理論上外部から差し替え可能だが、副次的ターゲットのため許容する | 対応なし |

### 8.11 C FFI インターフェース

`paint-engine/` は C 互換 FFI (`extern "C"` + `#[no_mangle]`) を公開する。Unity 側は P/Invoke で呼び出す。

#### ハンドル型

```c
typedef void* PaintCanvas;   // Rust 側: *mut Canvas（不透明ポインタ）
```

#### キャンバスライフサイクル

```c
PaintCanvas paint_canvas_create(uint32_t width, uint32_t height);
void        paint_canvas_destroy(PaintCanvas canvas);
void        paint_canvas_clear(PaintCanvas canvas, uint8_t r, uint8_t g, uint8_t b, uint8_t a);
```

#### レイヤー管理（ラスターレイヤー 最大16枚）

```c
// 追加: 成功時はレイヤーID(0〜15)、上限超過時は -1
int32_t paint_layer_add(PaintCanvas canvas);
void    paint_layer_remove(PaintCanvas canvas, int32_t layer_id);
void    paint_layer_set_visible(PaintCanvas canvas, int32_t layer_id, bool visible);
void    paint_layer_set_opacity(PaintCanvas canvas, int32_t layer_id, uint8_t opacity);
void    paint_layer_move(PaintCanvas canvas, int32_t layer_id, int32_t new_index);
void    paint_layer_merge_down(PaintCanvas canvas, int32_t layer_id);
```

#### ベースレイヤー（最下段・2値マスク専用）

ベースレイヤーは `layer_id = -2` の定数で識別する。ブラシ = 完全不透明、消しゴム = 完全透明のみ（半透明不可）。

```c
#define PAINT_LAYER_BASE ((int32_t)(-2))
```

#### ペイント操作（ラスターレイヤー・ベースレイヤー共用）

```c
void paint_brush(PaintCanvas canvas, int32_t layer_id,
                 uint32_t x, uint32_t y,
                 uint8_t r, uint8_t g, uint8_t b, uint8_t a,
                 uint32_t size, bool antialiased);

void paint_erase(PaintCanvas canvas, int32_t layer_id,
                 uint32_t x, uint32_t y, uint32_t size);

void paint_fill(PaintCanvas canvas, int32_t layer_id,
                uint32_t x, uint32_t y,
                uint8_t r, uint8_t g, uint8_t b, uint8_t a,
                float tolerance);

void paint_rect(PaintCanvas canvas, int32_t layer_id,
                uint32_t x, uint32_t y, uint32_t w, uint32_t h,
                uint8_t r, uint8_t g, uint8_t b, uint8_t a, bool filled);

void paint_ellipse(PaintCanvas canvas, int32_t layer_id,
                   uint32_t cx, uint32_t cy, uint32_t rx, uint32_t ry,
                   uint8_t r, uint8_t g, uint8_t b, uint8_t a, bool filled);
```

#### 色調補正レイヤー（常時存在・枚数制限外）

```c
// 各パラメータ: -1.0〜1.0（0.0 = 変更なし）
void paint_color_adjust_set(PaintCanvas canvas,
                             float brightness, float contrast, float saturation);
```

#### Undo / Redo（最大 `PaintUndoMaxSteps = 50` ステップ）

```c
bool    paint_undo(PaintCanvas canvas);   // 実行可能なら true
bool    paint_redo(PaintCanvas canvas);
int32_t paint_undo_count(PaintCanvas canvas);
int32_t paint_redo_count(PaintCanvas canvas);
```

#### PNG エクスポート

全レイヤーを合成し、保存時の透明ピクセル正規化（α < 128 → α=0・RGB=黒、α ≥ 128 → α=255）を適用した上で PNG に変換する。

```c
// Rust 側でアロケート。使用後は必ず paint_free_bytes() で解放すること。
uint8_t* paint_export_png(PaintCanvas canvas, uint32_t* out_size);
void     paint_free_bytes(uint8_t* ptr);
```

#### メモリ管理方針

| オブジェクト | 所有者 | 解放タイミング |
|---|---|---|
| `PaintCanvas` | **Rust** | `paint_canvas_destroy()` 呼び出し時 |
| PNG バイト列（`paint_export_png` 戻り値） | **Rust** | `paint_free_bytes()` 呼び出し時 |
| ペイント操作の引数（座標・色等） | Unity（値渡し） | 不要（スタック上） |

#### Unity 側 P/Invoke 実装例

```csharp
// PaintEngineBinding.cs
internal static class PaintEngineBinding
{
#if UNITY_IOS && !UNITY_EDITOR
    private const string Lib = "__Internal";
#else
    private const string Lib = "paint_engine";
#endif

    [DllImport(Lib)] internal static extern IntPtr paint_canvas_create(uint width, uint height);
    [DllImport(Lib)] internal static extern void paint_canvas_destroy(IntPtr canvas);
    [DllImport(Lib)] internal static extern void paint_brush(
        IntPtr canvas, int layerId, uint x, uint y,
        byte r, byte g, byte b, byte a, uint size, bool antialiased);
    [DllImport(Lib)] internal static extern IntPtr paint_export_png(IntPtr canvas, out uint outSize);
    [DllImport(Lib)] internal static extern void paint_free_bytes(IntPtr ptr);
    // ... 他の関数も同様
}

// PNG エクスポートの使用例
byte[] ExportToPng(IntPtr canvas)
{
    IntPtr ptr = PaintEngineBinding.paint_export_png(canvas, out uint size);
    var bytes = new byte[size];
    Marshal.Copy(ptr, bytes, 0, (int)size);
    PaintEngineBinding.paint_free_bytes(ptr);
    return bytes;
}
```

## 9. メモリ制限

起動時最大メモリ：

* 500MB以内

Atlas最大：

* 1024x2048 x RGBA（ASTC 6x6圧縮時 約0.9MB、ミップマップ込み 約1.2MB）

同時表示キャラ：

* 最大24

## 10. シーン構造

### シーン一覧

| シーン名 | ファイルパス | 役割 |
|---|---|---|
| HomeScene | `Assets/Scenes/HomeScene.unity` | ログイン・ホーム UI・ナビゲーションバー・各管理タブ |
| WorldScene | `Assets/Scenes/WorldScene.unity` | 3D ワールド・マルチプレイヤー・音声通話・ワールド作成編集 |

### シーン遷移

```
HomeScene
    ↓ ルーム参加 / 作成
WorldScene
    ↓ 退室
HomeScene
```

遷移時はローディング画面を挟む。`SceneManager.LoadSceneAsync` で非同期ロード。

### マネージャー配置

#### DontDestroyOnLoad（HomeScene で初期化・WorldScene でも存続）

HomeScene の Awake で生成し、`DontDestroyOnLoad` を適用する。WorldScene に遷移しても破棄されない。

| マネージャー | 役割 |
|---|---|
| UserManager | 認証・ユーザー情報・プランケイパビリティ |
| CacheManager | アセットのローカルキャッシュ管理 |
| LocalizationManager | 表示言語切り替え |
| AudioManager | システム SE 再生・音量設定（PlayerPrefs 永続化） |
| NotificationManager | アプリ内通知受信・表示 |
| FriendManager | フレンド一覧・フレンド関連操作 |
| FollowManager | フォロー一覧・フォロー関連操作 |
| HideManager | 非表示ユーザー管理（ワールド内でも参照） |
| ShopManager | ショップデータ・購入処理（ナビゲーションバーはワールド内でも表示されるため常駐） |

#### ショップ購入リクエストの二重送信防止

ショップ商品購入ボタンのタップ後、APIレスポンスが返るまでの間に同一ボタンを複数回タップしても購入リクエストが重複しないよう、Unity クライアント側で明示的にガードする。

- 購入ボタンをタップした瞬間にボタンを **非インタラクティブ状態**（`interactable = false` または UI Toolkit の `SetEnabled(false)`）にする
- APIレスポンス（成功 / 失敗 問わず）を受け取った後にボタンを再度インタラクティブ状態に戻す
- 購入中はボタン上にスピナー等のローディング表示を行い、処理中であることをユーザーに伝える
- `ShopManager` はフラグ（`bool _isPurchasing`）を保持し、`true` の間は追加の購入リクエストを発行しない
- ネットワークエラー（タイムアウト等）でレスポンスが返らない場合も必ずフラグをリセットする（`try/finally` または対応するコールバックで保証）

サーバー側のアトミック UPDATE（`docs/api-abstract.md` セクション 11）と合わせて二重消費を防ぐ。

#### HomeScene のみ

HomeScene に存在し、WorldScene 遷移時に破棄される。

現時点では HomeScene 専用マネージャーなし。ナビゲーションバー・各 UI パネルは DontDestroyOnLoad マネージャーを参照して描画する。

#### WorldScene のみ（入場時に生成・退室時に破棄）

| マネージャー | 役割 |
|---|---|
| NetworkManager | Netcode for GameObjects ホスト / クライアント管理 |
| AvatarManager | アバターの生成・破棄・スロット管理 |
| AtlasManager | テクスチャアトラス合成・更新 |
| VoiceManager | Vivox 3D 音声接続・位置同期 |
| PlayerController | 自プレイヤーの入力・移動・アニメーション制御 |
| WorldLoader | GLB ワールドデータの読み込み・配置 |
| WorldCreationManager | ワールド編集モード（ボクセル地形・オブジェクト配置） |

### Bootstrapper と初期化順序

`HomeScene` に `Bootstrapper` MonoBehaviour を配置する。DontDestroyOnLoad マネージャーをコルーチンチェーン（各マネージャーの `Initialize()` は `IEnumerator` を返す）で順次初期化する。

#### フェーズ A（起動直後・API 前）

ユーザー情報が不要なマネージャーを先に初期化する。

1. `AudioManager` — PlayerPrefs から音量設定を読み込む
2. `LocalizationManager` — システムロケールで仮設定（ログイン後に上書き）

#### フェーズ B（ログイン・`/startup` API 完了後）

API レスポンスが揃ってから順次初期化する。

1. `UserManager` — レスポンスでユーザー情報・PlanCapabilities を設定
2. `CacheManager` — ユーザー ID に基づくキャッシュディレクトリを初期化
3. `LocalizationManager` — ユーザーの言語設定で上書き再適用
4. `NotificationManager`
5. `FriendManager`
6. `FollowManager`
7. `HideManager`
8. `ShopManager` — `UserManager.Capabilities` を参照

### LoadingCanvas

`HomeScene` に `LoadingCanvas` を配置し `DontDestroyOnLoad` を適用する。シーン遷移中に全面表示し、完了後フェードアウトする。表示・非表示の制御は `Bootstrapper` が行う。

### HomeScene → WorldScene 遷移フロー

```
1. LoadingCanvas フェードイン表示
2. SceneManager.LoadSceneAsync("WorldScene", LoadSceneMode.Single) 開始
3. WorldScene 専用マネージャーを順次 yield return で初期化:
   a. WorldLoader       — GLB ワールドデータ取得・配置
   b. AvatarManager     — アバター生成
   c. AtlasManager      — テクスチャアトラス合成
   d. NetworkManager    — Netcode for GameObjects 接続確立
   e. VoiceManager      — Vivox チャンネル参加
   f. PlayerController  — プレイヤー生成・入力有効化
   g. WorldCreationManager — 編集モード待機状態で初期化
4. 全完了 → LoadingCanvas フェードアウト・プレイ開始
```

### WorldScene → HomeScene 退室フロー

```
1. LoadingCanvas フェードイン表示
2. WorldScene 専用マネージャーを逆順に破棄:
   a. WorldCreationManager 破棄
   b. PlayerController 破棄（入力無効化）
   c. VoiceManager     — Vivox チャンネル退出
   d. NetworkManager   — Netcode 切断
   e. AtlasManager 破棄
   f. AvatarManager 破棄（全アバター破棄）
   g. WorldLoader 破棄（GLB アンロード）
3. SceneManager.LoadSceneAsync("HomeScene", LoadSceneMode.Single) 開始
4. HomeScene 完了 → LoadingCanvas フェードアウト
```

## 11. 初期ロードフロー

1. ログイン（認証トークン取得）
2. スタートアップ一括取得（1リクエスト）: ユーザー設定・言語設定・アバター一覧＋URL・ワールド一覧先頭ページ
3. アバターデータ取得（CDN URL が前回と同一 → キャッシュ使用 / 変更 → 再ダウンロード）
4. Atlas生成
5. プレイヤー生成
6. Netcode for GameObjects接続
7. 音声接続

> 2 を 1 リクエストに統合することでログイン直後のラウンドトリップを削減する。

## 12. 将来拡張考慮

設計は以下を前提とする：

* ワールド分割ロード
* インスタンスサーバー
* アバターマーケット
* カスタムモーション追加

依存関係は疎結合に保つこと。

## 13. 禁止事項

* リアルタイムシャドウ使用禁止
* HDRP使用禁止
* URP ポストプロセスパイプライン使用禁止（撮影機能のフィルターは2D画像処理で実装すること）
* 半透明多用禁止
* 大容量VRM読み込み禁止

## 14. 成功条件

以下を満たすこと：

* 24人同時表示で30fps維持
* メモリ500MB未満
* Atlas更新1秒以内
* アバター読み込み2秒以内

## 15. アプリモード概要

ナビゲーションバーに 5 つのタブを配置する。詳細仕様は `docs/screens-and-modes.md` 参照。

| タブ | 概要 |
|---|---|
| ワールド | ワールド一覧・検索・ルーム参加・3D ワールドモード（ルーム内移動・音声通話） |
| アバター管理 | アバター・アクセサリのスロット管理・テクスチャ編集 |
| ワールド管理 | マイワールドの作成・編集・公開管理・オブジェクト管理 |
| ショップ | ゲーム内コインでアバター・アクセサリ・ワールドオブジェクトを購入 |
| 設定 | アプリ全体の設定（音量・言語・通知・非表示ユーザー等） |

## 16. ユーザーシステム・スロット

### 16.1 アバター・アクセサリスロット

| ユーザー種別 | アバタースロット | アクセサリスロット |
|---|---|---|
| 通常ユーザー | 10個 | 10個 |
| プレミアム会員 | 最大100個 | 最大100個 |

スロットに保存したアバターデータはAPIサーバーに永続化する。プレミアム解約時は11個目以降のスロットをロック（データ削除なし）。

### 16.2 ユーザーID・称号

* ユーザーIDは `@name` 形式で検索に利用できる
* 称号（プレミアム / ショップ開設者 / 開発者 / 運営関係者）はユーザーに付与でき、表示/非表示を個別設定可能
* 詳細: `docs/screens-and-modes.md` セクション5

### 16.3 マイワールドオブジェクトスロット

| ユーザー種別 | マイワールドオブジェクトスロット |
|---|---|
| 通常ユーザー | 10個 |
| プレミアム会員 | 最大100個 |

プレミアム解約時は11個目以降のスロットをロック（データ削除なし）。詳細仕様: `docs/world-creation.md` セクション 3.8 参照。

## 17. プレミアム会員

> 機能判定・制限値のアーキテクチャ設計はセクション 23 参照。将来のプラン追加（Premium Lite・Premium+等）を考慮し、ブール判定ではなくティア比較で実装する。

| 機能 | 通常ユーザー（free） | プレミアム会員（premium） |
|---|---|---|
| アバタースロット | 10個 | 最大100個 |
| アクセサリスロット | 10個 | 最大100個 |
| バックグラウンド通話（iOS/Android） | 不可 | 可 |
| プレミアム称号 | なし | 表示可能 |
| フレンド上限 | 100人 | 1000人 |
| プレミアム限定スタンプ・フィルター | 利用不可 | 利用可 |
| ワールドスロット | 5個 | 50個 |
| ワールドオブジェクト保存バリアントスロット | 10個 | 100個 |
| マイワールドオブジェクトスロット | 10個 | 最大100個 |
| 招待制ルーム作成 | 不可 | 可 |
| ルーム人数上限（maxPlayers） | 最大 6 人 | 最大 24 人 |
| ルーム連続在室上限 | 90分 | 12時間 |
| 放置自動退室 | 10分無操作で退室 | なし |

上位互換の原則: 上位ティアは下位ティアの全機能を包含する。各機能には「最低必要ティア」を定義し、それ以上のティアは自動的にその機能を利用できる。

## 19. アーキテクチャ方針

### 19.1 MonoBehaviour の責務制限

MonoBehaviour は Unity エンジンとの境界（ハードウェア/エンジン境界）のみを担当する。

| 担当する処理 | 例 |
|---|---|
| 物理コールバック受信 | `OnCollisionEnter` / `OnTriggerEnter` → ロジッククラスへ委譲 |
| Unity ライフサイクル | `Awake` / `Start` / `Update` → ロジッククラスのメソッド呼び出しのみ |
| アニメーション操作 | `Animator.SetBool` / `SetFloat` |
| シーン・オブジェクト操作 | `GetComponent` / `Instantiate` / `Destroy` / `transform` |
| 入力受信 | Input System コールバック → ロジッククラスへ委譲 |
| 時間処理 | `Time.deltaTime` / Coroutine → ロジッククラスへ渡す |

**ゲームロジックを MonoBehaviour に書かない。**

### 19.2 ロジッククラス設計（純粋 C#）

ゲームロジックは `UnityEngine` に依存しない純粋 C# クラスで実装する。

**原則:**
- `MonoBehaviour` を継承しない（`new` で生成可能）
- `UnityEngine` 名前空間は原則使用しない
  - 例外: `Vector3` / `Quaternion` / `Color` などの数学・値型（副作用なし）は使用可
- コンストラクタ注入またはインターフェース経由で疎結合にする

**主なロジッククラスの対象:**

| ロジッククラス | 担当 |
|---|---|
| `PlayerMovementLogic` | 移動・ジャンプ・スプリント状態管理 |
| `PositionSanityCheckLogic` | ホスト側の受信座標検証・速度超過検出・連続違反カウント管理 |
| `AtlasLayout` | Atlas スロット割り当て・UV 座標計算・パディング計算 |
| `LayerStack` | レイヤー管理・合成順・Undo 履歴 |
| `AssetCacheStore` | ハッシュ比較・TTL 管理・キャッシュエントリ操作 |
| `GimmickEngine` | イベント評価・条件判定・アクション実行・ループ検出 |
| `InventorySlot` | インベントリ保有ロジック |
| `FriendListLogic` | フレンド上限チェック・申請状態遷移 |
| `CoinLedger` | コイン残高計算・取引バリデーション |
| `WorldVariantSlotManager` | 保存バリアントスロット上限チェック・管理 |
| `TextureCostCalculator` | ワールドテクスチャコスト計算 |
| `SessionTimeLimitLogic` | セッション残り時間計算・警告イベント発火・制限時間到達イベント（通常90分 / プレミアム12時間） |
| `AfkDetectionLogic` | 操作なし経過時間計算・AFK判定・操作受信でタイマーリセット（プレミアムは無効化） |
| `FollowListLogic` | フォロー/フォロー解除・フォロワーリスト管理 |
| `HideListLogic` | 非表示ユーザーリスト管理・描画・音声フィルタリング判定 |
| `NotificationStore` | アプリ内通知の受信・未読管理・種別フィルタリング |

### 19.3 テスト方針

| テスト種別 | ディレクトリ | 用途 |
|---|---|---|
| EditMode | `Assets/Tests/EditMode/` | ロジッククラスのユニットテスト（Unity ランタイム不要・高速） |
| PlayMode | `Assets/Tests/PlayMode/` | Unity ランタイムが必要な統合テスト |

- フレームワーク: NUnit（Unity Test Runner 標準）
- ロジッククラスを新規作成・変更したときは対応するテストを同時に作成・更新する

---

## 18. 管理画面

Go `api/` サービスの `/admin` 以下で提供するWebインターフェース（ログイン必須）。

主な機能: ショップ商品登録・クリエイター登録・アバター審査・ユーザー管理・違反報告対応・売上管理。

詳細仕様: `docs/screens-and-modes.md` セクション8 参照。

---

## 20. オーディオシステム

### 20.1 音量カテゴリ

アプリ内の音声出力は以下 3 カテゴリに分類し、ユーザーが個別に音量を調整できる。

| カテゴリ | 説明 | 制御方式 |
|---|---|---|
| 通話音声 | ルームで聞こえる他ユーザーの声 | Vivox SDK の受話音量 API |
| ワールド効果音 | ギミックサウンド・ワールド環境音など、音声とシステム音以外のすべて | Unity AudioMixer（`WorldSFX` グループ） |
| システム音 | ボタン操作音・入退室通知音など、アプリ基本 UI のサウンド | Unity AudioMixer（`SystemSFX` グループ） |

### 20.2 AudioMixer 構成

```
Master
├── WorldSFX   ← ギミック音・環境音を出力する AudioSource はこのグループへルーティング
└── SystemSFX  ← システム UI の AudioSource はこのグループへルーティング
```

- Vivox の音声出力は Unity AudioMixer を経由しない（Vivox SDK 独自の音声パイプライン）
- `AudioManager` が各グループの exposed parameter を管理する

### 20.3 音量の永続化

- 保存先: `PlayerPrefs`（デバイスローカル）
- キー名: `Vol_Voice`、`Vol_WorldSFX`、`Vol_SystemSFX`
- 値の型: float（0.0f 〜 1.0f）
- デフォルト値: 1.0f（100%）

### 20.4 ギミックサウンドとの関係

- ギミックアクション「音・音楽を鳴らす」の音量設定（0〜100%）は `WorldSFX` グループの相対値として乗算される
- 例: ユーザーの WorldSFX 音量 50%・ギミック音量 80% の場合 → 実効音量 40%

### 20.5 ワールド環境音

- ワールド作成者が内蔵サウンドライブラリから選択し、ワールド定義 JSON に保存
- ワールドに入場するとループ再生（フェードイン・アウトあり）
- 音量設定はワールド作成者が指定した値（0〜100%）× ユーザーの「ワールド効果音」音量
- 詳細仕様: `docs/world-creation.md` セクション 14 参照

---

## 21. サーバーサイドバリデーション方針

### 21.1 基本原則

- **クライアント側のバリデーションは UX 補助（即時フィードバック）にすぎない。**
- すべてのアップロード・入力値について、**API サーバー（Go）が権威的なバリデーションを必ず実施する。**
- サーバーのバリデーションを通過しないデータは一切保存・処理しない。
- バリデーション失敗時は適切な HTTP エラーコード（400 / 413 など）とエラー詳細を返し、クライアントはそれをユーザーに表示する。

> クライアントがバリデーションを行っていても、APIを直接呼び出すリクエストを防ぐため、サーバー側の検証は省略しない。

### 21.2 アップロード・入力別バリデーション一覧

#### ファイルアップロード

| 対象 | サーバーで検証する項目 |
|---|---|
| アバター VRM | ファイルサイズ ≤ 500KB / ポリゴン数 ≤ 512tris / ボーン数 ≤ 50 / VRM 1.0 形式 |
| アクセサリ GLB | ファイルサイズ ≤ 100KB / テクスチャサイズ ≤ 64×64 / GLB 形式 |
| マイオブジェクト GLB | ファイルサイズ ≤ 200KB / ポリゴン数 ≤ 256tris / テクスチャが 16/32/64/128/256px 正方形のいずれか / GLB 形式 |
| ペイントレイヤー画像 | PNG 形式 / 解像度（アバター: 256×256 / アクセサリ: 64×64 / ワールドオブジェクト: 16〜512px 正方形） / ファイルサイズ上限（実装時に決定） |
| テクスチャ統合画像 | 同上（解像度・形式） |
| 背景テクスチャ | PNG 形式 / 解像度 1024×1024 / ファイルサイズ上限（実装時に決定） |
| サムネイル | PNG または JPEG 形式 / ファイルサイズ上限（実装時に決定） |

#### 数値・構造データ

| 対象 | サーバーで検証する項目 |
|---|---|
| ワールド定義 JSON | スキーマ整合性 / オブジェクト合計数 ≤ 400 / テクスチャコスト ≤ 4,096 / コライダー値（0 以上・0.25m 単位）/ ギミックルール構造の整合性 / スポーン位置の存在（公開時）/ ポータルのペア整合性（公開時）/ `maxPlayers` が通常ユーザーは 2〜6 の整数・プレミアムは 2〜24 の整数 |
| 人数上限（`maxPlayers`） | 通常ユーザー: 2〜6 の整数 / プレミアム: 2〜24 の整数（ワールド設定パネルからの更新時・ワールド定義 JSON 保存時） |
| コライダーサイズ値 | 0 以上・0.25m 単位（マイオブジェクトタイプ更新・インスタンス `size` フィールド） |
| ギミックアクション音量値 | 0〜100 の整数 |
| 環境音設定 | `soundId` が許可リスト内 / `volume` が 0〜100 の整数 |
| ワールドタグ | タグ数 ≤ 5 / 1タグ ≤ 20文字 / BAN タグ除外（正規化後に照合） |
| ワールド名 | 文字数上限（実装時に決定） |
| ユーザー名（@name） | 英数字（a-z / A-Z / 0-9）・アンダースコアのみ / 3〜15 文字 / 重複なし / 保存時に小文字正規化 / プレミアム会員による変更は前回から 90 日以上経過していること |

#### スロット上限

| 対象 | 上限（free / premium） |
|---|---|
| アバタースロット | 10 / 100 |
| アクセサリスロット | 10 / 100 |
| マイオブジェクトスロット | 10 / 100 |
| ワールドスロット | 5 / 50 |
| 保存バリアントスロット | 10 / 100 |
| フレンド数 | 100 / 1,000 |

> これらの値はすべて Go の `PlanConfig` マップ（セクション 23.3）を単一の設定源として管理する。コード上に直接ハードコードしない。

---

## 22. トラストレベルシステム

### 22.1 概要

サーバー側でユーザーの信頼度を管理する仕組み。**ゲームクライアントには一切公開しない**（API レスポンスに含めない）。管理画面からのみ参照・操作できる。

### 22.2 トラストレベル定義

| レベル | 説明 |
|---|---|
| `visitor` | 新規作成時の初期値 |
| `new_user` | 一定の活動実績を持つユーザー |
| `user` | 信頼できる通常ユーザー |
| `trusted_user` | 高い信頼性を持つ実績あるユーザー |

### 22.3 トラストポイント計算

公開ルーム（公開種別のルームのみ）を退室するたびに以下の計算でポイントを加算する。

```
平均在室人数 = (参加時の他ユーザー数 + 退室時の他ユーザー数) / 2
参加時間（分）= floor(実際の滞在時間（分）)
加算ポイント  = 平均在室人数 × 参加時間（分）
```

- 「他ユーザー数」は自分を含まない実人数
- 参加時間は小数切り捨て（整数分）
- 非公開ルーム（フレンドのみ / フォロワー限定 / 招待制）はカウント対象外

### 22.4 トラストレベル昇格ルール

**評価タイミング**: 以下のイベント発生後に非同期ジョブで評価する
- 公開ルーム退室（トラストポイント更新後）
- フレンド数変動
- ワールド公開
- ワールドのいいね数変動
- 課金完了
- プレミアム加入・解約

**評価アルゴリズム**: 下記の全条件を同時評価し、**適合する最上位レベルに設定する**。トラストレベルは自動では下がらない（管理者の強制変更を除く）。

| 昇格先 | 条件（いずれかを満たす） |
|---|---|
| `trusted_user` | 公開済みワールド数 ≥ 2 **かつ** いずれか1ワールドの獲得いいね数 ≥ 100 |
| `user` | (トラストポイント ≥ 1000 **かつ** フレンド数 ≥ 5) **または** アプリ内課金履歴あり **または** プレミアム会員中 |
| `new_user` | トラストポイント ≥ 1000 **または** (トラストポイント ≥ 300 **かつ** フレンド数 ≥ 3) |
| `visitor` | いずれの条件も満たさない（デフォルト） |

- プレミアム解約後も昇格済みレベルはそのまま維持される（他条件で `user` を維持しない場合も降格しない）
- 「アプリ内課金履歴あり」は `coin_purchases` テーブルに1件以上の記録があること。返金・キャンセル（`coin_purchase_cancellations`）が発生しても購入記録自体は削除されないため、課金履歴は永続的に残り、条件は維持される

**ロック中の評価ジョブの挙動**:

`trust_level_locked = true` の場合、昇格ジョブは評価（条件チェック）は実行するが**レベルの変更のみスキップ**する。評価結果は `trust_level_logs` に `reason: 'locked_skipped'` として記録する。ロックが解除された後に次のイベントが発生したとき（評価ジョブが再実行されたとき）、その時点の条件で正常に昇格する。

### 22.5 自動ユーザー制限

通報数がしきい値を超えると自動的に **制限状態** に設定する。制限の解除は管理画面からのみ行える。

| トラストレベル | 24時間以内の通報数（重複なし） | 累計通報数（重複なし） |
|---|---|---|
| `visitor` | 2件以上 | 4件以上 |
| `new_user` | 3件以上 | 10件以上 |
| `user` / `trusted_user` | 自動制限なし | 自動制限なし |

- 「通報数」は **通報者を重複カウントしない**（同一ユーザーが複数回通報しても 1 件として扱う）
- 自動制限は通報受信時に同期的にチェックする（リアルタイム性が必要なため）
- `user` 以上は管理画面からの操作時のみ制限される

### 22.6 制限時の行動制限

制限状態のユーザーは以下のルームに入室できない。

| ルーム種別 | 制限状態での入室 |
|---|---|
| 公開ルーム | 不可 |
| フォロワー限定ルーム | 不可 |
| フレンドのみルーム | 可 |
| 招待制ルーム | 可 |

入室を拒否するときのエラーレスポンス: HTTP 403、`reason: "user_restricted"` を含める。クライアントはこのコードを受信したとき問い合わせリンクを表示する（`docs/screens-and-modes.md` セクション 17 参照）。

### 22.7 管理画面での操作

- トラストレベルの手動変更（任意のレベルに強制設定）
- トラストレベルのロック（ロック中は自動昇格・降格を無効化）
- トラストレベルのロック解除（**`super_admin` のみ**。`admin` / `moderator` は実行不可）
- ユーザー一覧のトラストレベルフィルター
- 制限状態の解除
- 制限理由・変更履歴の記録（`trust_level_logs` テーブル）

**`admin_audit_logs` への記録仕様:**

トラストレベルのロック・ロック解除操作は `admin_audit_logs` に以下の形式で記録する:

| フィールド | ロック操作 | ロック解除操作 |
|---|---|---|
| `action` | `lock_trust_level` | `unlock_trust_level` |
| `target_type` | `user` | `user` |
| `target_id` | 対象ユーザーの `users.id` | 対象ユーザーの `users.id` |
| `before_value` | `{"trust_level_locked": false, "trust_level": "<level>"}` | `{"trust_level_locked": true, "trust_level": "<level>"}` |
| `after_value` | `{"trust_level_locked": true, "trust_level": "<level>"}` | `{"trust_level_locked": false, "trust_level": "<level>"}` |
| `notes` | 任意（ロック理由） | 必須（解除理由・`super_admin` が記入） |

`unlock_trust_level` の `notes` はサーバー側で必須バリデーションを行う（空文字列・未入力を拒否）。

スロット上限・フレンド上限はすべて **サーバーで判定**する。クライアントでの上限到達表示はサーバーからのエラーレスポンスを受けて行う。

---

## 23. サブスクリプション・プラン設計

### 23.1 設計方針

将来の多段階プラン（Premium Lite・Premium+など）追加に備え、プレミアム判定を **ブール値ではなくティア（tier）の大小比較** で設計する。

- 上位互換の原則: 上位ティアは下位ティアの全機能を包含する
- 新プラン追加はサーバー側の設定変更のみで完結し、クライアントコードの変更は不要
- 機能制限値（スロット数・セッション時間等）は `PlanConfig` マップを単一の設定源とする

### 23.2 DB 設計

`active_users` テーブルのサブスクリプション管理カラム:

| カラム | 型 | デフォルト | 説明 |
|---|---|---|---|
| `subscription_tier` | `VARCHAR NOT NULL` | `'free'` | 現在のプランティア。現行値: `'free'` / `'premium'`。将来: `'premium_lite'` / `'premium_plus'` 等 |
| `subscription_expires_at` | `TIMESTAMPTZ` | `NULL` | サブスクリプション有効期限。`NULL` = 無料プラン（期限概念なし）。期限切れはリクエスト時にサーバーが評価し、失効していれば `'free'` として扱う |

- `is_premium BOOL` は使用しない。ティア追加・削除に対してスキーマ変更不要

### 23.3 Go（API）サーバー

#### ティア定義と順序

各ティアに整数の順序値を対応させ、`>=` 比較で上位互換を実現する。

```go
type PlanTier string

const (
    PlanFree        PlanTier = "free"
    PlanPremium     PlanTier = "premium"
    // 将来追加例:
    // PlanPremiumLite PlanTier = "premium_lite"
    // PlanPremiumPlus PlanTier = "premium_plus"
)

// ティアの順序（数値が大きいほど上位）。新ティア追加時はここに追記する
var PlanTierOrder = map[PlanTier]int{
    PlanFree:    0,
    PlanPremium: 1,
    // 挿入例: PlanPremiumLite を Free と Premium の間に追加する場合
    //   PlanFree:        0,
    //   PlanPremiumLite: 1,
    //   PlanPremium:     2,
    //   PlanPremiumPlus: 3,
}

// userTier が required 以上かを返す（上位互換チェック）
func TierAtLeast(userTier, required PlanTier) bool {
    return PlanTierOrder[userTier] >= PlanTierOrder[required]
}
```

#### 機能フラグのティア要件定義

ブール系の機能は「最低限必要なティア」として定義する。`TierAtLeast` で判定する。

```go
// 機能キー → 最低必要ティア。新機能追加時はここに追記する
var FeatureMinTier = map[string]PlanTier{
    "background_call":        PlanPremium,
    "invite_room_create":     PlanPremium,
    "name_change":            PlanPremium,
    "premium_stamps_filters": PlanPremium,
    // 将来の機能追加例（PlanPremiumPlus 定義後に追記）:
    // "exclusive_feature_x": PlanPremiumPlus,
}

func HasFeature(userTier PlanTier, featureKey string) bool {
    required, ok := FeatureMinTier[featureKey]
    if !ok {
        return false
    }
    return TierAtLeast(userTier, required)
}
```

#### 数値上限のプラン設定

スロット数・人数上限・セッション時間など数値系の制限は `PlanCapabilities` 構造体とマップで管理する。

```go
type PlanCapabilities struct {
    AvatarSlots     int
    AccessorySlots  int
    WorldSlots      int
    VariantSlots    int
    MyObjectSlots   int
    FriendLimit     int
    MaxPlayersLimit int  // ワールド設定の maxPlayers 上限値
    SessionMinutes  int  // 連続在室上限（0 = 無制限）
    AfkEnabled      bool // 放置自動退室の対象か
}

// 単一の設定源。新プラン追加時はここにエントリを追加する
var PlanConfig = map[PlanTier]PlanCapabilities{
    PlanFree: {
        AvatarSlots:     10,
        AccessorySlots:  10,
        WorldSlots:      5,
        VariantSlots:    10,
        MyObjectSlots:   10,
        FriendLimit:     100,
        MaxPlayersLimit: 6,
        SessionMinutes:  90,
        AfkEnabled:      true,
    },
    PlanPremium: {
        AvatarSlots:     100,
        AccessorySlots:  100,
        WorldSlots:      50,
        VariantSlots:    100,
        MyObjectSlots:   100,
        FriendLimit:     1000,
        MaxPlayersLimit: 24,
        SessionMinutes:  720, // 12時間
        AfkEnabled:      false,
    },
}

func GetCapabilities(tier PlanTier) PlanCapabilities {
    if caps, ok := PlanConfig[tier]; ok {
        return caps
    }
    return PlanConfig[PlanFree] // 未知ティアはフリーにフォールバック
}
```

### 23.4 Unity クライアント

#### 起動 API（`GET /startup`）のレスポンス設計

クライアントはティア文字列を受け取らず、サーバーが計算した **能力値オブジェクト** のみを受け取る。新ティア追加・制限値変更はサーバー側だけで完結し、クライアントコードは変更不要。

```json
{
  "planCapabilities": {
    "avatarSlots": 100,
    "accessorySlots": 100,
    "worldSlots": 50,
    "variantSlots": 100,
    "myObjectSlots": 100,
    "friendLimit": 1000,
    "maxPlayersLimit": 24,
    "sessionMinutes": 720,
    "afkEnabled": false,
    "backgroundCall": true,
    "inviteRoomCreate": true,
    "premiumStampsFilters": true,
    "nameChange": true
  },
  "securityNotice": {
    "id": "breach-2026-04-07",
    "title": "重要なセキュリティに関するお知らせ",
    "body": "..."
  }
}
```

`securityNotice` は未確認の通知がある場合のみ値が入る。ない場合は `null`。

**セキュリティ通知モーダルの処理フロー（Bootstrapper）:**

`/startup` レスポンスの `securityNotice` が `null` でない場合、フェーズ B の初期化完了後・ホーム画面遷移前に強制モーダルを表示する。

```
/startup レスポンス受信
    ↓
securityNotice != null ?
    ├── YES → セキュリティ通知モーダルを表示（screens-and-modes.md セクション 1.5.7）
    │         ユーザーが「確認しました」をタップ
    │         → POST /api/v1/me/security-notice-ack を送信
    │         → ホーム画面へ遷移
    └── NO  → そのままホーム画面へ遷移
```

モーダルは × ボタンなし・モーダル外タップ無効（必ず確認操作が必要）。

#### Unity ロジッククラス

`UserManager` が `PlanCapabilities` 構造体を保持し、全ての機能判定・UI 表示に使用する。

```csharp
// NG: is_premium フラグを直接判定（プラン数が増えるたびに修正が必要）
int slots = UserManager.IsPremium ? 100 : 10;

// OK: 能力値から取得（プラン数に関わらずコード変更不要）
int slots = UserManager.Capabilities.AvatarSlots;
bool canBgCall = UserManager.Capabilities.BackgroundCall;
```

- `SessionTimeLimitLogic` は `capabilities.SessionMinutes` をコンストラクタで受け取る
- `AfkDetectionLogic` は `capabilities.AfkEnabled` をコンストラクタで受け取る
- Unity 側に `IsPremium` プロパティは公開しない

## 24. 入力システム（InputSystem）

### 実装方針

`com.unity.inputsystem` 1.18.0 を使用し、`InputActionAsset` を直接操作して Action Map を有効・無効に切り替える（`PlayerInput` コンポーネントは使用しない）。`.inputactions` ファイルは `Assets/Settings/InputActions.inputactions` に配置する。

### Action Map 定義

#### Player（WorldScene 通常移動時に有効）

| アクション | 型 | バインド（PC） | バインド（Mobile） |
|---|---|---|---|
| Move | Value / Vector2 | WASD / 矢印キー | カスタム TouchInput から注入 |
| Jump | Button | Space | カスタム TouchInput から注入 |
| Sprint | Button | Left Shift | カスタム TouchInput から注入 |
| CameraLook | Value / Vector2 | マウスデルタ | カスタム TouchInput から注入 |

#### UI（常時有効）

Unity 標準の UI Action Map をそのまま使用する（`Navigate` / `Submit` / `Cancel` / `Point` / `Click`）。

#### PhotoMode（撮影モード中のみ有効・Player を無効化して切り替え）

| アクション | 型 | バインド（PC） | バインド（Mobile） |
|---|---|---|---|
| CameraRotate | Value / Vector2 | マウスデルタ | 1 本指ドラッグ（TouchInput） |
| CameraPan | Value / Vector2 | 中ボタンドラッグ | 2 本指ドラッグ（TouchInput） |
| CameraZoom | Value / float | マウスホイール | ピンチ（TouchInput） |

### Action Map 切り替えタイミング

| 状態 | 有効な Action Map |
|---|---|
| HomeScene | UI のみ |
| WorldScene 通常モード | Player + UI |
| 撮影モード | PhotoMode + UI（Player を無効化） |
| ワールド編集モード | UI のみ（Player を無効化） |
| UI パネル・モーダル表示中（ワールド内） | Player は有効のまま（移動しながら閲覧可） |

### モバイルタッチ入力（カスタム TouchInput クラス）

`EnhancedTouchSupport.Enable()` を有効化し、`Touch.activeTouches` を毎フレーム処理する `TouchInput` クラスを実装する（純粋 C# ロジッククラス。`Vector2` 等の値型のみ `UnityEngine` を使用）。`PlayerController` の `Update` から呼び出し、結果を InputSystem の Action に反映する。

#### タッチ領域の定義

| 領域 | 判定条件 | 処理 |
|---|---|---|
| 下半分移動領域 | `screenPosition.y < Screen.height * 0.5f` | 最初のタッチのみ移動入力として採用 |
| 上半分カメラ領域（通常） | `screenPosition.y >= Screen.height * 0.5f` | 1 本指スライド → カメラ回転 |
| 上半分カメラ領域（撮影モード） | 同上・PhotoMode 有効時 | 2 本指ピンチ → ズーム / 2 本指スライド → 平行移動 |

#### 移動・スプリントロジック

- スライド開始と同時に移動開始
- スライド開始から 0.3 秒以内に移動量が閾値内に収まった場合 → スプリント移行
- マルチタッチ: 下半分への最初のタッチのみ移動採用。そのタッチが離れるまで後続は無視

#### ジャンプロジック

- 移動スライド中に別の指でダブルタップ → 移動方向へジャンプ（HUDジャンプボタンが OFF の場合のデフォルト操作）
- HUDジャンプボタンが ON の場合はボタンタップでジャンプ（`OnScreenButton` を使用）

## 25. UI システム

### フレームワーク方針

| 用途 | フレームワーク | 理由 |
|---|---|---|
| スクリーンスペース UI（全画面・パネル・HUD・モーダル） | **UI Toolkit** | Unity 6 推奨・UXML/USS で宣言的に記述 |
| ワールドスペース UI（アバター頭上の名前・発話インジケーター） | **uGUI**（最小限） | UI Toolkit はワールドスペース非対応 |

uGUI の使用箇所は上記のワールドスペース要素のみに限定する。新規 UI は原則 UI Toolkit で実装する。

### ファイル配置

```
Assets/UI/
  Screens/        # 各画面の UXML
  Components/     # 共通コンポーネント UXML
  Styles/         # USS スタイルシート
  Resources/      # フォント等
```

### Safe Area 対応

UI Toolkit ではルート `VisualElement` に C# で `Screen.safeArea` を適用する。`SafeAreaFitter` に相当するロジッククラスを用意し、`UIDocument` のルート要素に対して `Awake` / 画面回転時に適用する。

### 3D プレビュー（RenderTexture）

編集画面の 3D プレビューは `PreviewCamera` が描画した `RenderTexture` を `VisualElement` の `style.backgroundImage` に設定して表示する。

```csharp
previewElement.style.backgroundImage = new StyleBackground(renderTexture);
```

### ワールドスペース UI（uGUI）

各アバター GameObject の子に **World Space Canvas** を配置する。

| 要素 | 内容 |
|---|---|
| 名前表示 | `TextMeshProUGUI`（ユーザー名）。公認バッジ保持者の場合は名前テキストの右隣に `Image`（`icon_verified.png`）を表示 |
| 発話インジケーター | `Image`（`icon_voice_indicator.png`）、発話中に点滅 |

Canvas は常にメインカメラ方向を向くよう `LookAt` で更新する（`AvatarManager` が管理）。

## 26. カメラ設計

### カメラ一覧

| カメラ名 | 所属 | 出力先 | Culling Mask |
|---|---|---|---|
| MainCamera | WorldScene | スクリーン | Default（PreviewLayer を除く） |
| PreviewCamera | DontDestroyOnLoad | RenderTexture 1024×1024 | PreviewLayer のみ |

### MainCamera（追従カメラ）

- WorldScene に配置。プレイヤーを追従する。
- 撮影モード時は同じカメラオブジェクトのまま `PlayerController` が制御を `PhotoModeController` に委譲する（カメラオブジェクトは切り替えない）。
- HomeScene では MainCamera は存在しない。

### PreviewCamera（3D プレビュー用）

- HomeScene に配置し `DontDestroyOnLoad` を適用。WorldScene でも再利用する。
- 使用しないときは無効化（`gameObject.SetActive(false)`）してレンダリングコストをゼロにする。
- プレビュー対象モデルはレイヤー `PreviewLayer` に設定し、**PreviewStage**（座標 `(0, 2000, 0)` 付近）に配置する。MainCamera の Culling Mask から `PreviewLayer` を除外することでワールドに影響しない。
- 編集画面を開いた時点でモデルを PreviewStage に移動し、閉じたら元に戻す（または破棄）。

### 撮影モードカメラ

MainCamera と同一オブジェクト。`PlayerController` が `PhotoModeController` に処理を切り替えることで以下の操作を実現する。

| 操作 | 動作 |
|---|---|
| 1 本指スライド | カメラ回転（水平 / 垂直） |
| 2 本指ピンチ | ズーム |
| 2 本指スライド | カメラ平行移動 |

撮影モード終了時はカメラ位置・回転を開始直前の状態に復元する。

## 27. エラーハンドリング

### HTTP 通信の基本方針

| 設定 | 値 |
|---|---|
| タイムアウト | 10 秒 |
| リトライ回数 | 最大 3 回 |
| バックオフ | 指数（1 秒・2 秒・4 秒） |
| リトライ対象 | 5xx・ネットワークエラー・タイムアウト |
| リトライ非対象 | 4xx（クライアント起因のため即時失敗） |

### HTTP エラーコード対応表

| コード | 対応 |
|---|---|
| 400 | フィールド単位のエラーメッセージを表示 |
| 401 | トークン自動リフレッシュ → 失敗時はログアウトしてタイトル画面へ |
| 403 `user_restricted` | 「利用が制限されています」＋お問い合わせリンク（`nibankougen@gmail.com`）を表示 |
| 403 その他 | 「この操作は許可されていません」を表示 |
| 404 | 「見つかりませんでした」を表示 |
| 409 | 状態に応じたメッセージ（例: 満員 → 「このルームは満員です」） |
| 429 | 「しばらく時間をおいてから再試行してください」を表示 |
| 5xx | リトライ後も失敗時に「サーバーエラーが発生しました」を表示 |

### ネットワーク切断（WorldScene 内）

Netcode / Vivox の切断を検知した場合:

1. 「接続が切断されました」ダイアログを表示
2. 自動再接続を最大 3 回試みる（バックオフ: 2 秒・4 秒・8 秒）
3. 再接続成功 → ダイアログを閉じてプレイ再開
4. 再接続失敗 → HomeScene へ強制退室

## 28. テスト戦略

### テスト種別と対象範囲

| 種別 | 配置先 | 対象 | 実行タイミング |
|---|---|---|---|
| EditMode | `Assets/Tests/EditMode/` | 全ロジッククラス | push ごとに CI で自動実行 |
| PlayMode | `Assets/Tests/PlayMode/` | マネージャー初期化フロー・シーン遷移の状態管理 | リリース前に手動 + CI で実行 |

### カバレッジ目標

- **ロジッククラス**: 80% 以上（`Assets/Scripts/` 以下の非 MonoBehaviour クラス）
- **UI・MonoBehaviour**: 自動テスト対象外（手動テスト）

### CI/CD（GitHub Actions + GameCI）

リポジトリ: `nibankougen/LowPolyWorld`

| ジョブ | トリガー | 内容 |
|---|---|---|
| EditMode テスト | `push`（全ブランチ） | Unity Test Runner で EditMode テストを実行・結果を PR にコメント |
| PlayMode テスト | `push` to `main` | Unity Test Runner で PlayMode テストを実行 |

ライセンス管理には [GameCI](https://game.ci/) の Unity Activation を使用する。シークレットは GitHub リポジトリの Secrets に保存する（`UNITY_LICENSE` / `UNITY_EMAIL` / `UNITY_PASSWORD`）。

### ロジッククラス新規作成・変更時のルール

- 対応する EditMode テストを**同時に**作成・更新する（CLAUDE.md 参照）
- テストファイル名: `{ClassName}Tests.cs`
- アサーションフレームワーク: NUnit（Unity Test Runner 標準）

## 29. ビルド設定・アセット配信

### Addressables は使用しない

アセット配信は以下の方針で完結するため Addressables は導入しない。

| アセット種別 | 配信方法 |
|---|---|
| VRM・GLB（ユーザー生成コンテンツ） | HTTP → `CacheManager` でローカルキャッシュ |
| デフォルトワールドオブジェクト GLB | `StreamingAssets/` にアプリ同梱 |
| アニメーション・音声・UI 素材 | アプリ同梱（`Assets/` 以下） |

### プラットフォーム共通設定

| 項目 | 値 |
|---|---|
| Scripting Backend | IL2CPP |
| API Compatibility Level | .NET Standard 2.1 |
| Managed Stripping Level | Minimal（P/Invoke 使用のため） |

### iOS

| 項目 | 値 |
|---|---|
| Target SDK | Device SDK |
| Architecture | ARM64 |
| Universal Links | Associated Domains に API サーバードメインを追加 |

### Android

| 項目 | 値 |
|---|---|
| Minimum API Level | 26（Android 8.0）|
| Target API Level | Latest |
| Target Architecture | ARM64 + ARMv7 |
| App Links | `assetlinks.json` を API サーバーの `/.well-known/` に配置 |
