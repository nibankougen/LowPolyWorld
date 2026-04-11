# GDPR 対応 TODO リスト

調査日: 2026-04-07  
対象: `docs/` 以下の全ドキュメント（8ファイル）

---

## 優先度凡例

- 🔴 **P0**: ローンチブロッカー（リリース前に必須）
- 🟠 **P1**: ローンチ後 1〜2 ヶ月以内
- 🟡 **P2**: ローンチ後 3〜6 ヶ月以内

---

## P0: ローンチ前に必須

### 1. プライバシーポリシー・利用規約の作成・公開

- [ ] プライバシーポリシー本文を作成して公開 URL を確定する
  - 必須記載: 取得情報・利用目的・第三者提供（Google Cloud / Cloudflare / Unity Gaming Services）・保存期間・削除請求先
  - 参考: `human-asset-todo.md` のプライバシーポリシー要点
- [ ] 利用規約本文を作成して公開 URL を確定する
- [ ] `Constants.cs` の仮 URL を確定 URL に置き換える
- [ ] App Store Connect・Google Play Console のプライバシーポリシー欄に登録する

### 2. サブプロセッサとの DPA（データ処理委託契約）締結確認

- [ ] **Google Cloud** — Standard Contractual Clauses (SCC) への同意確認
- [ ] **Cloudflare** — GDPR Data Processing Addendum の確認・署名
- [ ] **Unity Gaming Services（Vivox / Relay）** — GDPR Compliance Agreement の確認
- [ ] 確認済み DPA のリストをドキュメント化する

### 3. 国際データ転送の適法化（GDPR Chapter V）

> **現状**: ローンチ時は App Store / Google Play の配信地域から EU/EEA を除外するため、GDPR Chapter V は現時点で適用対象外。以下の項目は **EU 配信解禁時**に対応する。

- [ ] Google Cloud・Cloudflare は米国企業のため、SCC の適用状況を確認する
- [ ] プライバシーポリシーに「EU 代理人の連絡先」「国際データ転送根拠（SCC）」を追記する
- [ ] GDPR Art. 27 に基づく EU 代理人を指定する（EU 拠点を持つ代理人サービスを契約）
- [ ] `breach-notification-plan.md §5` を選択肢 B（PPC + EU DPA）へ移行する

EU 配信解禁の全チェックリストは `development-plan.md`「EU 配信解禁チェックリスト」を参照。

### 4. データ侵害（ブリーチ）通知計画の策定

- [x] インシデント対応責任者・連絡先を指定する（nibankougen@gmail.com）
- [x] GDPR Art. 33: 監督機関への 72 時間以内通知フローを文書化する（`breach-notification-plan.md`）
- [x] GDPR Art. 34: 影響を受けるユーザーへの通知フロー・テンプレートを作成する（`breach-notification-plan.md`）
- [x] アプリ起動時強制モーダルの設計（`securityNotice` フィールド・`screens-and-modes.md` セクション 1.5.7）
- [x] EU ユーザー特定手段の設計: ロケール保存（`active_users.locale`）+ 全アクセスログを Cloud Logging に1年保持（`api-abstract.md` セクション 5.5）+ ブリーチ時オンデマンド GeoLite2 処理（`tools/breach-geo-report/`）
- [x] Cloud Logging のアラートと連携した侵害検知の仕組みを設計する（`infra-abstract.md` セクション 10・`breach-notification-plan.md` セクション 9）
  - 通知先: メール + Discord（Pub/Sub → Cloud Functions → Discord Webhook）
  - レイヤー 1: Cloud Monitoring メトリクスアラート（5xx・SQL 接続・CPU）
  - レイヤー 2: Cloud Logging ログベースアラート（大量 DELETE・深夜管理者操作）
  - レイヤー 3: Go API 構造化ログ（ブルートフォース・大量トークン無効化・API 連打）

### 5. 保護者同意フローの実装詳細設計

- [x] 13〜15 歳ユーザーへの保護者同意メール検証フローを詳細設計する
  - 保護者メールアドレスの収集・確認メール送信・検証完了の記録
  - 完全ブロック方針・14 日タイムアウト・Day 7 リマインド（`screens-and-modes.md` §1.5.6・`api-abstract.md` §4）
- [x] `parental_consent_verified_at` の保持期間と削除ルールを定義する
  - `active_users` の一部: アカウント削除時（即時または 14 日タイムアウト）に削除
  - 別テーブル `parental_consents`（`users.id` 参照）: 監査証跡として 5 年保持（`api-abstract.md` §4）
- [x] 13 歳未満が誤登録した場合の速やかな削除プロセスをドキュメント化する
  - `POST /admin/users/{id}/delete-underage`: 即時物理削除・2 段階確認・監査ログ記録（`api-abstract.md` §4）

---

## P1: ローンチ後 1〜2 ヶ月以内

### 6. アクセス権対応（GDPR Art. 15）

- [x] ユーザーが自身の個人情報を取得できる API エンドポイントを実装する
  - `GET /api/v1/me/data-export`: アカウント情報・購入履歴・消費履歴・フォロー/フレンド/非表示・アバター/ワールドメタデータを JSON で返却（`api-abstract.md` §6）
  - ローンチ時実装に前倒し（P0 相当）
- [x] 対応期間（30 日以内）と請求窓口（nibankougen@gmail.com）をプライバシーポリシーに明記する
  - エンドポイントの設計注意事項として `api-abstract.md` §6 に記載済み

### 7. データ移植権対応（GDPR Art. 20）

- [x] ユーザーが自身のコンテンツを一括エクスポートできる機能を実装する
  - `GET /api/v1/me/data-export` の JSON レスポンスにアバター VRM・ワールド GLB の CDN URL（永続的・コンテンツアドレス）を含める。ユーザーは URL 経由でファイルを個別ダウンロード可能（Art. 20「移植可能な形式」の要件を満たす）
  - ローンチ時実装に前倒し（P0 相当）
- [x] エクスポート機能を設定画面 UI に追加する
  - 設定タブ §19.6「個人データをダウンロード」ボタンとして追加済み

### 8. 訂正権対応（GDPR Art. 16）

- [ ] 訂正可能な個人情報項目と UI を明確にする
  - 現状: 表示名・@name は変更可能だが、GDPR 観点での訂正権として整理されていない
- [ ] プライバシーポリシーに訂正権の行使方法を記載する

### 9. 同意撤回権の整備（GDPR Art. 7）

- [ ] 初回ログイン時の同意（利用規約・プライバシーポリシー）を撤回する手段を設ける
  - 実質的にはアカウント削除と紐付く形で可
- [ ] 通知設定のオプトアウト（プッシュ通知 ON/OFF）をプライバシーポリシーに記載する

### 10. Vivox・Unity SDK のデータ収集ポリシー確認

- [x] Vivox SDK が収集・転送するデータの内容を確認する
  - 音声: エフェメラル（録音・保存なし）
  - Vivox が最大 30 日間保持: 仮名ユーザー ID（`vivox_id`）・デバイス ID・末尾切り詰め済み IP アドレス
  - テキストデータ: デフォルト 7 日（最大 30 日）— 本サービスはテキストチャット非使用のため対象外
- [ ] Unity Gaming Services（Relay）が収集するデータを確認する
- [x] 確認結果をプライバシーポリシーの「第三者サービス」セクションに反映する（Vivox 分のみ完了・`privacy-policy-elements.md §6` 更新済み。Relay 分は上記確認後に反映）
- [x] プライバシーポリシーに「音声は保存・自動分析されない」旨を明記する（Vivox）
  - `privacy-policy-elements.md §3.3`「収集しない情報」に記載済み
  - 対応策: Vivox に実ユーザー UUID の代わりに `vivox_id`（仮名 ID）を渡す設計に変更済み（`api-abstract.md §4`・`development-plan.md` Phase 5A / Phase 6）

### 11. IP アドレス・User-Agent の自動削除バッチ確認

- [x] 1 年後の自動削除（`api-abstract.md` セクション 13）がバッチで確実に実行されることを確認する
  - Cloud Scheduler（毎日 JST 03:30）→ `cleanup-access-logs` バッチとして `development-plan.md` Phase 5A に実装タスク追加済み
- [x] バッチ失敗時のアラート通知を設定する
  - 全バッチ共通ポリシー（Cloud Scheduler 3 回リトライ → Discord 通知）を `api-abstract.md §13` に定義済み
  - Cloud Scheduler 失敗メトリクスアラート・完了ログ監視アラートを `development-plan.md` Phase 5B に実装タスク追加済み

---

## P2: ローンチ後 3〜6 ヶ月以内

### 12. データ保護影響評価（DPIA）の実施（GDPR Art. 35）

- [ ] 高リスク処理について DPIA を実施する
  - 対象: 3D 音声通話（Vivox）・UGC（アバター・ワールド）・年齢確認フロー・トラストレベルシステム
- [ ] DPIA の結果に基づき追加的な保護措置を検討する

### 13. 処理活動記録（RoPA）の整備（GDPR Art. 30）

- [ ] 正式な処理活動記録を作成する
  - 処理目的・データカテゴリ・受取人・保持期間・セキュリティ対策を一覧化
  - 現在 `api-abstract.md` セクション13 に分散している情報を統合

### 14. 定期コンプライアンス監査の仕組み化

- [x] 四半期ごとの確認項目を定める

  **四半期レビューチェックリスト（年 4 回・1月/4月/7月/10月の第1週に実施）:**

  #### サードパーティ ポリシー確認（各社プライバシーポリシー・DPA の変更有無）
  - [ ] Google Cloud（Cloud Run / Cloud SQL / Cloud Tasks / Cloud Logging / Pub/Sub / Secret Manager）— https://cloud.google.com/terms/data-processing-addendum
  - [ ] Cloudflare（R2 / CDN）— https://www.cloudflare.com/privacypolicy/
  - [ ] Unity Vivox — https://unity.com/legal/privacy-policy
  - [ ] Unity Gaming Services（Relay）— https://unity.com/legal/privacy-policy
  - [ ] Apple（Sign-In / StoreKit / APNs）— https://www.apple.com/legal/privacy/
  - [ ] Google（Sign-In / Play Billing / FCM）— https://policies.google.com/privacy
  - [ ] Resend（メール送信）— https://resend.com/legal/privacy-policy
  - [ ] Upstash / Cloud Memorystore（Redis）— 利用サービスのポリシーを確認
  - 変更があった場合: プライバシーポリシー本文・`privacy-policy-elements.md §6` を更新する

  #### 削除バッチの実行ログレビュー
  - [ ] Cloud Logging で過去 3 ヶ月分の `batch_completed` ログを確認し、失敗・スキップがないことを検証する
  - [ ] `system_alerts` テーブルにバッチ関連のアラートが残っていないことを確認する

  #### プライバシーポリシー・利用規約の整合性確認
  - [ ] プライバシーポリシー本文と `privacy-policy-elements.md` の内容が一致しているか確認する
  - [ ] 新機能追加・仕様変更でデータ収集内容に変化がないか確認する

  #### インシデント記録レビュー
  - [ ] `docs/incident-log.md`（または管理画面 `system_alerts`）を開き、未対応・未記録のインシデントがないか確認する
  - [ ] レベル 2 以上のインシデントがあった場合、監督機関（PPC）への報告が完了しているか確認する

- [x] インシデント記録の定期レビュープロセスを定義する（上記チェックリスト「インシデント記録レビュー」として統合）

### 15. 子ども向けコンプライアンスの継続監視

- [ ] 13〜15 歳ユーザーの保護者同意取得率をモニタリングする仕組みを設ける
- [ ] EU 各国の年齢要件（16 歳を定める国が多い）への対応を検討する
  - 現在は 13 歳以上だが、EU では Art. 8 により 16 歳未満に保護者同意が必要な国もある

---

## 参考: 対応済み（問題なし）

以下は docs の調査時点で GDPR に準拠していると判断した項目。

| 項目 | 対応内容 |
|---|---|
| データ最小化 | 生年月日を計算後に破棄・年齢グループのみ保存（`api-abstract.md` §4） |
| 削除権（Art. 17） | PII の即時 NULL 化・財務記録の匿名化分離・30 日後物理削除（`api-abstract.md` §13） |
| データ保持期間の定義 | 各データ種別の保持期間が明確に定義済み（`api-abstract.md` §13） |
| 転送中の暗号化 | DTLS 1.2 / TLS 1.2+ / SRTP が全経路で実装済み（`unity-game-abstract.md` §7.2） |
| アクセス制御 | RBAC・最小権限・監査ログ改ざん防止設計済み（`api-abstract.md` §10） |
| 監査ログの外部保存 | Cloud Logging へリアルタイム転送（改ざん防止）（`infra-abstract.md` §9） |
| 利用規約・PP 同意 UI | 初回ログイン時に同意チェックボックスを表示する設計済み（`screens-and-modes.md` §1） |
| 年齢確認メカニズム | 13 歳未満拒否・13〜15 歳保護者同意フロー設計済み（`api-abstract.md` §4） |
| アカウント削除フロー | ソフトデリート即時 → 30 日後物理削除のフロー設計済み（`screens-and-modes.md` §18） |
