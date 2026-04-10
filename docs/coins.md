# コイン・売上・税務管理システム仕様書（最終版）

目的:  
モバイルアプリ内コインの管理、内部売上推定、プラットフォーム確定売上との突合、および将来のクリエイター報酬支払いに対応する会計基盤を提供する。

---

# 1. 基本方針

- コインはアプリ内消費専用のデジタルクレジットとする
- コインは現金化できない
- 取引履歴は削除不可（追記型）
- 監査証跡を永続保存する
- 売上は以下の2層構造とする

1. 内部推定売上（コイン価値ベース）
2. 確定売上（プラットフォームレポートベース）

---

# 2. 税務前提（重要）

## 2.1 プラットフォーム課金に関する消費税

アプリ内課金に含まれる消費税（VAT/売上税）は  
プラットフォームが販売主体として徴収・納税する。

したがって本システムでは：

- ユーザー課金額から消費税を再計算しない
- 消費税を売上税として計上しない
- 売上はプラットフォーム振込額を基準とする

## 2.2 税テーブルの保持理由

税率テーブルは以下の将来拡張のため保持する：

- Web直販対応
- 外部決済導入
- クリエイター支払時の源泉税対応
- 国別税率変更履歴管理

---

# 3. コイン仕様

- コインはアプリ内課金でのみ取得可能
- 有効期限は購入から6ヶ月
- ユーザー間送金不可
- 現金交換不可
- 取引削除不可
- 取消は逆仕訳として記録

## 3.1 コイン残高の計算

コイン残高はバッチで更新するのではなく、**クエリ時に `valid_until > now()` の条件で有効なコインのみを合計**して算出する。有効期限を過ぎたコインは残高に反映されなくなる（DB レコードは削除しない）。

## 3.1.1 `coin_balance_snapshots` テーブル（日次残高スナップショット）

過去任意の日付のユーザー残高を監査時に効率よく参照するための日次スナップショット。**残高に変動があったユーザーのみ**記録する（全ユーザー毎日記録はデータ量が過大なため）。

**テーブル定義:**

| カラム | 型 | 説明 |
|---|---|---|
| id | BIGSERIAL PK | |
| user_id | TEXT NOT NULL | `users.id` 参照 |
| snapshot_date | DATE NOT NULL | スナップショット日付（UTC） |
| balance | INTEGER NOT NULL | その日の残高（有効コイン合計） |
| change_reason | VARCHAR NOT NULL | 変動要因: `purchase` / `consume` / `expire` / `cancel` |
| created_at | TIMESTAMPTZ NOT NULL DEFAULT now() | |

`UNIQUE (user_id, snapshot_date)` — 1ユーザー1日1レコード。

**記録タイミング:**

スナップショットは残高変動イベント（購入・消費・失効・キャンセル）が発生した**当日の最初の変動時**に記録する。`INSERT ... ON CONFLICT (user_id, snapshot_date) DO NOTHING` により1日1件に制限する。

**過去残高の参照方法（監査時）:**

任意日 `T` のユーザー残高は以下の2ステップで再構成できる:

```
1. coin_balance_snapshots から user_id = X かつ snapshot_date <= T の
   最新レコード（最大の snapshot_date）を取得 → base_balance, base_date

2. coin_transactions から user_id = X かつ created_at が
   (base_date の翌日 00:00 UTC) から T の終端までのレコードを集計して差分を計算

T の残高 = base_balance + Σ(差分トランザクション)
```

**保持期間:** スナップショットレコードは `users.id` を参照し PII を含まないため、`active_users` 削除後も 7 年保持する（監査証跡）。

## 3.2 コイン失効通知バッチ

ユーザーへの失効直前通知を送るため、**毎日 UTC 00:00** に日次バッチを実行する。

**処理内容:**

```
1. `valid_until` が現在時刻から 30日以内 かつ 31日以上 のコイン購入記録を持つユーザーを検索
   → アプリ内通知を送信（30日前通知）

2. `valid_until` が現在時刻から 7日以内 かつ 8日以上 のコイン購入記録を持つユーザーを検索
   → アプリ内通知を送信（7日前通知）
```

- 同一ユーザーが複数の購入記録を持つ場合、**最も近い失効日**のものを代表として通知する（1日1通知）
- 既に通知済みの場合は重複送信しない（`coin_expiry_notifications` テーブルで管理。`user_id × valid_until × type` を UNIQUE キーとする）

**冪等性の保証（バッチ再実行時の重複防止）:**

通知の挿入は `INSERT ... ON CONFLICT DO NOTHING` を使用する。これにより、バッチが同一日付に複数回実行された場合（クラッシュ後の再実行・手動再実行等）でも重複通知が発生しない。

```sql
INSERT INTO coin_expiry_notifications (user_id, valid_until, type, notified_at)
VALUES ($1, $2, $3, now())
ON CONFLICT (user_id, valid_until, type) DO NOTHING;
-- 競合（すでに通知済み）の場合は何もせず成功扱い
```

バッチの実行ログには「処理対象件数 / 新規送信件数 / スキップ（重複）件数」を記録し、再実行時の動作を検証可能にする。
- 通知のプッシュ通知対応: ユーザーが「コイン失効通知」のプッシュ通知を有効化している場合は push も送信する（詳細: `docs/screens-and-modes.md` セクション 15 参照）

---

# 4. コイン購入記録

保存項目：

- user_id
- platform（ios / android）
- platform_transaction_id（Apple/Google のトランザクションID・UNIQUE制約）
- storefront_country
- purchase_timestamp
- valid_until（purchase_timestamp + 6ヶ月）
- coins_amount（付与コイン数）
- local_amount（ユーザーが支払ったストアフロント通貨の金額・税込）
- local_currency（ISO 4217 通貨コード。例: JPY / USD）
- fx_rate_to_jpy（購入日時点の為替レート。local_currency が JPY の場合は 1.0）
- converted_jpy_amount（`local_amount × fx_rate_to_jpy` で算出した JPY 換算額。ユーザー支払額ベースの推定値であり、プラットフォームが消費税を処理した後の実際の振込額とは異なる）
- platform_fee_rate_id（外部キー）
- estimated_net_revenue_jpy

計算式：

estimated_net_revenue_jpy =
    converted_jpy_amount × (1 - platform_fee_rate)

※ `converted_jpy_amount` はユーザーが支払った税込金額の JPY 換算値であるため、`estimated_net_revenue_jpy` はあくまで推定値。プラットフォームが税処理を行う国（日本など）では実際の振込額より過大になる場合がある。実際の売上は確定売上（セクション 9）のプラットフォームレポートを正とする。

---

# 5. プラットフォーム手数料テーブル

保存項目：

- platform
- start_date
- end_date
- fee_rate

要件：

- 未来の手数料率登録可能
- 購入日時で自動適用
- 履歴は不変

---

# 6. ユーザー平均コイン価値

各ユーザーに加重平均コイン価値を保持する。この値はコイン消費時の推定消費額計算にのみ使用する推定値であり、`adjustment_factor`（セクション10）で事後補正されるため、高い精度より実装の堅牢さを優先する。

計算：

new_avg =
    (既存価値合計 + 新規購入価値)
    /
    (既存コイン数 + 新規コイン数)

ここで:
- 既存価値合計 = `current_balance × avg_coin_value_jpy`（残高がマイナスの場合は負値になり得る）
- 新規購入価値 = その購入の `estimated_net_revenue_jpy`

**更新タイミング**:

- **コイン購入時**: 上記計算式で更新する
- **コイン消費時**: 更新しない（消費はコイン価値に影響しない）
- **返金・キャンセル時（`coin_purchase_cancellations`）**: 更新しない。残高のみ変更し、avg は変更前の値を維持する

**ゼロ除算の処理**:

分母（既存コイン数 + 新規コイン数）が 0 になる場合（マイナス残高の絶対値と新規購入数がちょうど等しい場合）は、加重平均を計算せず新規購入の単価を採用する:

```
if (current_balance + new_coins_amount) == 0:
    new_avg = estimated_net_revenue_jpy / new_coins_amount
else:
    new_avg = (current_balance × avg_coin_value_jpy + estimated_net_revenue_jpy)
              / (current_balance + new_coins_amount)
```

保存項目：

- user_id
- avg_coin_value_jpy

---

# 7. コイン消費記録

保存項目：

- transaction_id
- idempotency_key（nullable・UNIQUE 制約。クライアントが送信した UUID v4。`api-abstract.md` セクション 11 参照）
- buyer_id
- shop_id
- creator_id
- item_id
- coins_spent
- avg_coin_value_jpy_at_time
- estimated_consumption_value_jpy
- timestamp

計算：

estimated_consumption_value_jpy =
    coins_spent × avg_coin_value_jpy_at_time

---

# 8. 内部売上集計

算出可能にする：

- ショップ別推定売上
- クリエイター別推定売上
- 月別推定売上

---

# 9. 確定売上登録

管理画面から期間ごとに登録する。

保存項目：

- period（YYYY-MM）
- country
- settled_net_revenue_jpy
- refund_adjustment_jpy

計算：

settled_net_revenue_jpy =
    プラットフォーム確定額 - 返金額

---

# 10. 売上補正係数

期間・国別に計算する。

**更新トリガー**: 管理者が確定売上（セクション9）を管理画面から登録した時点で、サーバーが該当 `period` / `country` の補正係数と月次スナップショット（セクション10.1）を自動的に計算・保存する（`docs/screens-and-modes.md` セクション8.5参照）。

**修正権限と監査:**

補正係数（`adjustment_factor`）の手動上書き（自動計算値を変更する場合）は以下の制約を設ける:

- **修正権限**: `super_admin` のみ。`admin` / `moderator` は修正不可
- **理由入力必須**: 修正時は理由テキスト（必須・最大 500 文字）の入力を要求する
- **監査ログ記録**: 修正操作は `admin_audit_logs` に「修正前の値 / 修正後の値 / 理由テキスト」を記録する
- **自動計算の原則**: 手動修正は例外的措置とし、確定売上の再登録（セクション9）による自動再計算を推奨する

adjustment_factor =
    settled_net_revenue_jpy
    /
    total_estimated_consumption_value_jpy

保存項目：

- period
- country
- adjustment_factor
- calculated_at

## 10.1 月次スナップショット

補正係数の算出に使用した元データを月次で保存し、将来のコイン価値予測に活用する。

保存項目：

- period（YYYY-MM）
- country
- total_coins_purchased（期間内の購入コイン総数）
- total_coins_consumed（期間内の消費コイン総数）
- total_coins_expired（期間内に有効期限切れとなったコイン総数）
- total_estimated_purchase_value_jpy（期間内の `estimated_net_revenue_jpy` 合計）
- total_estimated_consumption_value_jpy（期間内の `estimated_consumption_value_jpy` 合計）
- settled_net_revenue_jpy（確定売上・セクション9から参照）
- adjustment_factor（補正係数）
- avg_coin_value_jpy_snapshot（期間末時点の全ユーザー平均コイン価値の中央値）
- transaction_count_purchase（期間内のコイン購入トランザクション件数）
- transaction_count_consumed（期間内のコイン消費トランザクション件数）
- calculated_at

このスナップショットは以下に活用する：

- 補正係数の時系列トレンド分析（実績 vs 推定の乖離幅の変化）
- コイン流通量・消費率の推移把握（購入/消費/失効 の比率）
- 将来のコイン価値予測モデルの入力データ（機械学習・回帰分析等）
- 価格改定判断の根拠データ

---

# 11. 確定消費額

各消費トランザクションに対し算出：

final_consumption_value_jpy =
    estimated_consumption_value_jpy × adjustment_factor

これを会計上の実売上とする。

---

# 12. 確定売上集計

算出可能にする：

- ショップ別確定売上
- クリエイター別確定売上
- 月別確定売上

---

# 13. クリエイター報酬対応

契約テーブル：

- creator_id
- contract_id
- revenue_share_rate
- effective_start
- effective_end

支払算出：

payable_amount =
    final_revenue_jpy × revenue_share_rate

支払実行は外部処理とする。

---

# 14. 税テーブル（外部キー管理）

保存項目：

- tax_id
- country
- tax_type
- tax_rate
- valid_from
- valid_to

用途：

- 外部決済導入時
- クリエイター源泉税
- 将来の直販課税

※App内課金売上には適用しない

---

# 14.5 管理画面での金額表示

管理画面では各金額フィールドを説明付きで表示し、財務担当者・開発者が意味を誤解しないようにする。

## 14.5.1 コイン購入詳細画面（1件あたり）

| 表示ラベル | フィールド | 説明（ツールチップ） |
|---|---|---|
| ユーザー支払額（現地通貨） | `local_amount local_currency` | ユーザーがストアで実際に支払った金額（税込）。プラットフォームの価格設定に基づく。 |
| 為替レート（JPY換算） | `fx_rate_to_jpy` | 購入日時点のレート。後から変更されることはない。 |
| JPY換算支払額（推定） | `converted_jpy_amount` | 上記2つの積。税込・為替変動込みの推定値であり、実際の振込額とは異なる場合がある。 |
| プラットフォーム手数料率 | `fee_rate`（platform_fee_rates参照） | Apple/Google に支払う手数料の比率。購入日時点で適用された値。 |
| 推定純売上（JPY） | `estimated_net_revenue_jpy` | JPY換算支払額 × (1 − 手数料率)。推定値であり、確定売上との差異は補正係数で調整される。 |
| 付与コイン数 | `coins_amount` | この購入でユーザーに付与されたコイン数。 |
| 有効期限 | `valid_until` | このコインの有効期限（購入から6ヶ月）。期限切れのコインは残高に反映されない。 |

## 14.5.2 月次売上サマリー画面

| 表示ラベル | フィールド | 説明（ツールチップ） |
|---|---|---|
| 購入コイン総数 | `total_coins_purchased` | 期間内にユーザーが購入したコインの合計数。 |
| 消費コイン総数 | `total_coins_consumed` | 期間内にショップ購入等で消費されたコインの合計数。 |
| 失効コイン総数 | `total_coins_expired` | 期間内に有効期限切れとなったコインの合計数。失効コインは返金対象外。 |
| 推定純売上合計（JPY） | `total_estimated_purchase_value_jpy` | 期間内のすべての購入における推定純売上の合計。プラットフォームの実際の振込額とは異なる場合がある。 |
| 推定消費額合計（JPY） | `total_estimated_consumption_value_jpy` | 期間内のすべての消費トランザクションにおける推定消費額の合計。 |
| 確定純売上（JPY） | `settled_net_revenue_jpy` | プラットフォームのレポートに基づく実際の振込純額（手数料・返金控除後）。これが財務上の正式な売上金額。 |
| 売上補正係数 | `adjustment_factor` | 確定純売上 ÷ 推定消費額合計。1.0 に近いほど推定精度が高い。大きく乖離している場合は為替・税処理の見直しが必要。 |
| 平均コイン価値（JPY） | `avg_coin_value_jpy_snapshot` | 期間末時点の全ユーザー平均コイン価値の中央値。コイン1枚あたりの実質的な価値の推移を把握するために使用。 |

## 14.5.3 コイン消費詳細画面（1件あたり）

| 表示ラベル | フィールド | 説明（ツールチップ） |
|---|---|---|
| 消費コイン数 | `coins_spent` | この取引で消費されたコイン数。 |
| 消費時点の平均コイン価値（JPY） | `avg_coin_value_jpy_at_time` | 消費した瞬間のユーザーの加重平均コイン価値。購入履歴に基づく推定値。 |
| 推定消費額（JPY） | `estimated_consumption_value_jpy` | 消費コイン数 × 消費時点の平均コイン価値。推定値であり、確定値ではない。 |
| 確定消費額（JPY） | `final_consumption_value_jpy` | 推定消費額 × 売上補正係数。月次で一括計算・更新される財務上の確定値。 |

# 15. 監査要件

- 取引削除禁止
- 逆仕訳のみ許可
- 手数料履歴保持
- FXレート保存必須
- 補正係数履歴保持
- 税率履歴保持

---

# 16. 将来拡張性

本設計は以下に対応可能：

- コイン換金機能
- Web直販
- 国別税制対応
- マーケットプレイス報酬分配
- 監査・税務調査対応

---

# 17. 返金・取引キャンセルシステム

## 17.1 概要

App Store Server Notifications（Apple）および Google Play Real-time Developer Notifications（Google）を使用して返金されたコイン購入を検知し、対象取引をキャンセルとして記録する。管理者が管理画面から手動でキャンセルすることも可能。

---

## 17.2 プラットフォーム通知受信

**Apple — App Store Server Notifications V2:**

- エンドポイント: `POST /webhook/apple`
- 通知形式: JWS（JSON Web Signature）署名付き
- 処理対象イベント: `REFUND`
- 署名検証: Apple 公開鍵による JWS 検証（必須）
- 冪等性: `signedTransactionInfo.originalTransactionId` を一意キーとして重複処理を防ぐ

**Google — Real-time Developer Notifications:**

- エンドポイント: `POST /webhook/google`
- 配信方式: Google Cloud Pub/Sub プッシュ
- 処理対象イベント: `ONE_TIME_PRODUCT_VOIDED`
- 認証: Pub/Sub プッシュサブスクリプションのベアラートークン検証（必須）
- 冪等性: `purchaseToken` を一意キーとして重複処理を防ぐ

---

## 17.3 返金検知時の処理フロー

```
1. Webhook 受信 & 署名/トークン検証（失敗時は 401 を返す）
   → webhook_events に受信ログを記録（processing_status: pending）
2. platform_transaction_id でコイン購入記録を特定
   ├── 対応する購入記録が存在しない → 無視（200 を返して処理終了）
   └── 存在する → 続行
3. 重複チェック（coin_purchase_cancellations に同一 platform_transaction_id が存在）
   ├── 存在する → 冪等応答（200 を返して処理終了）
   └── 存在しない → 続行
4. 対象購入の有効期限確認
   ├── 失効済み（valid_until < 現在時刻）
   │     → coins_deducted = 0 として記録のみ（残高変更なし）
   └── 有効期限内
         → coins_deducted = 購入時の coins_amount として残高を差し引く
            （残高がマイナスになる場合あり）
5. coin_purchase_cancellations に追記（取引削除不可ルールに準じた追記型）
6. ユーザーのコイン残高を更新
```

---

## 17.4 取引キャンセル記録テーブル

保存項目：

- id
- coin_purchase_id（外部キー: コイン購入記録）
- cancellation_type（`platform_refund` / `manual_admin`）
- platform（ios / android / null）
- platform_transaction_id（Apple/Google のトランザクションID・UNIQUE 制約・手動の場合 null）
- coins_deducted（実際に差し引いたコイン数 ※失効済みの場合は 0）
- balance_before（キャンセル前のコイン残高）
- balance_after（キャンセル後のコイン残高）
- cancelled_at
- admin_id（nullable: 手動キャンセル時の操作管理者ID）
- notes（任意メモ）

冪等性保証: `platform_transaction_id` に UNIQUE 制約を設け、同一通知の重複処理を防ぐ。

---

## 17.5 マイナス残高ポリシー

- コイン残高がマイナスになった場合、そのまま残高として保持する（下限なし）
- **購入制限**: 残高 < 0 の間、ショップでのコイン消費（商品購入）を禁止する
- **コイン購入**: 残高マイナスでも新規コイン購入は可能（残高回復の手段として）
- **購入済みアイテム**: 返金・マイナス残高による影響なし（利用継続）
- **残高回復**: 新規コイン購入により残高が 0 以上に戻ると購入制限が解除される

---

## 17.6 有効期限切れコイン返金の扱い

有効期限（6ヶ月）を過ぎて失効済みのコイン購入が返金された場合：

- コイン残高の調整は行わない（当該コインはすでに残高に反映されていないため）
- `coins_deducted = 0` として取引キャンセル記録を保存する
- 管理画面の取引キャンセル一覧に「期限切れ済み（調整なし）」と表示する

---

## 17.7 手動キャンセル

管理者が管理画面から手動でコイン購入をキャンセルできる（カスタマーサポート対応等）。

- 処理フロー: プラットフォーム返金と同じ（有効期限確認 → 残高調整 → 記録）
- `cancellation_type`: `manual_admin`
- `admin_id`: 操作した管理者IDを記録
- `platform_transaction_id`: null（プラットフォームと紐付かないため）

---

## 17.8 確定売上（セクション9）との関係

セクション9の `refund_adjustment_jpy`（確定売上の返金調整額）は、本セクションの取引キャンセル記録から `coins_deducted > 0` の件を集計した推定値として参照できる。なお、セクション9は期間ごとのプラットフォームレポートを正とするため、17.4 の記録はあくまで補助情報とする。

---

## 17.9 Webhook 受信ログテーブル（`webhook_events`）

Webhook を受信した時点で処理の成否にかかわらず生データを記録する。デバッグ・監査・リプレイの基盤となる。

保存項目：

- id（BIGSERIAL PK）
- source（`apple` / `google`）
- event_type（`REFUND` / `ONE_TIME_PRODUCT_VOIDED` 等。プロバイダーの通知種別をそのまま記録）
- external_id（Apple: `originalTransactionId` / Google: `purchaseToken`。重複確認のための参照キー）
- raw_payload（TEXT: 受信した生 Webhook ボディをそのまま保存。Apple は JWS 文字列・Google は JSON 文字列）
- received_at（TIMESTAMPTZ NOT NULL DEFAULT now()）
- processing_status（`pending` / `processed` / `failed` / `ignored` / `permanently_failed`）
- processed_at（TIMESTAMPTZ nullable）
- error_message（TEXT nullable: 処理失敗時のエラー詳細）
- retry_count（SMALLINT NOT NULL DEFAULT 0）
- related_cancellation_id（BIGINT nullable: 処理成功時の `coin_purchase_cancellations.id` 参照）

**冪等性との関係:**

`webhook_events` は受信証跡であるため、同一 `external_id` の通知が複数届いても全件記録する（UNIQUE 制約なし）。重複処理防止は 17.2 に定める `coin_purchase_cancellations.platform_transaction_id` の UNIQUE 制約が担う。

---

## 17.10 Webhook 処理失敗時の再試行ポリシー

Webhook の処理（署名検証後のコイン残高調整）が失敗した場合、asynq のリトライキュー（`webhook_retry` キュー）で再実行する。

| 設定 | 値 |
|---|---|
| 最大リトライ回数 | 5 回 |
| バックオフスケジュール | 1 分・5 分・15 分・1 時間・6 時間 |
| 上限到達後 | `webhook_events.processing_status = permanently_failed` に更新 |
| 永続的失敗時 | 管理画面のシステムアラートに「Webhook 処理失敗」アラートを生成 |

**再試行時の冪等性:**

再試行は `webhook_events.id` を識別子として実行する。`coin_purchase_cancellations.platform_transaction_id` の UNIQUE 制約により、途中まで処理が進んだ後のリトライでも二重キャンセルは発生しない。

**無視扱いのイベント（`ignored`）:**

対応する購入記録が存在しない場合（17.3 ステップ 2 の分岐）は再試行せず即時 `ignored` に設定する。

---

## 17.11 月次返金調整プロセス

プラットフォームの月次レポートとシステム内のキャンセル記録を照合し、確定売上（セクション 9）の `refund_adjustment_jpy` を確定する。

**実施タイミング:** 毎月末〜翌月初旬（プラットフォームが前月分のレポートを公開した後）

**プロセス:**

```
1. プラットフォームレポートの取得
   - Apple: App Store Connect → 「売上と動向」→ 払い戻し金額を確認
   - Google: Google Play Console → 「財務レポート」→ 払い戻し額を確認

2. システム側の集計（管理画面 → 月次売上サマリー画面）
   - 対象月の coin_purchase_cancellations（cancellation_type: platform_refund, coins_deducted > 0）を集計
   - 推定返金額 = Σ（各キャンセルの estimated_net_revenue_jpy）

3. 照合・差異確認
   - プラットフォームレポートの返金額（JPY 換算）と推定返金額を比較
   - 差異が ±10% を超える場合: 個別トランザクションを精査して原因を特定

4. 確定売上の登録（セクション 9）
   - 管理画面から settled_net_revenue_jpy と refund_adjustment_jpy（プラットフォームレポート値）を入力
   - 入力後、補正係数（adjustment_factor）と月次スナップショット（セクション 10）が自動計算される

5. 記録
   - 照合結果のサマリー（差異・特記事項）を admin_audit_logs の notes に記録する
   - action: register_settled_revenue
```

**処理できなかった Webhook の確認:**

月次調整時に `webhook_events.processing_status = permanently_failed` のレコードを確認し、手動で対応または管理画面から再実行する。未処理の返金がある場合は `refund_adjustment_jpy` に手動加算する。
