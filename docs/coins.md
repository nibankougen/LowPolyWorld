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
- local_amount
- local_currency
- fx_rate_to_jpy
- converted_jpy_amount
- platform_fee_rate_id（外部キー）
- estimated_net_revenue_jpy

計算式：

estimated_net_revenue_jpy =
    converted_jpy_amount × (1 - platform_fee_rate)

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

各ユーザーに加重平均コイン価値を保持する。

計算：

new_avg =
    (既存価値合計 + 新規購入価値)
    /
    (既存コイン数 + 新規コイン数)

保存項目：

- user_id
- avg_coin_value_jpy

---

# 7. コイン消費記録

保存項目：

- transaction_id
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

adjustment_factor =
    settled_net_revenue_jpy
    /
    total_estimated_consumption_value_jpy

保存項目：

- period
- country
- adjustment_factor
- calculated_at

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
