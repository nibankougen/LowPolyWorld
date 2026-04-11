# セキュリティ TODO

セキュリティレビュー結果（2026-04-11）。優先度順に記載。

---

## Critical

- [x] **GLBファイルのバリデーション仕様追加**
  - アクセサリGLB: 128 tris上限・Optimizerワーカー経由（非同期）・スクリーンショット生成（審査用）を仕様化
  - `api-abstract.md §9`（アクセサリ GLB Optimizer）・`unity-game-abstract.md §4.5.2 / §4.5.5` に追記済み
  - マイオブジェクトGLB（`world-creation.md §3.8`）は元から仕様定義済みのため対応不要

- [x] **P/Invokeネイティブプラグインの整合性検証**
  - `Assets/Plugins/plugin_checksums.sha256` による SHA-256 照合 + GitHub ブランチ保護（PR必須）の2層構成を仕様化
  - iOS/Android/macOS はプラットフォーム署名で担保済み。Windows は副次的ターゲットのため追加対応なし
  - `unity-game-abstract.md §8.10`「プラグインバイナリの整合性保護」に追記済み
  - 残作業: `plugin_checksums.sha256` ファイルの実作成・GitHub ブランチ保護ルールの設定（実装フェーズで対応）

- [x] **リフレッシュトークンのブルートフォース対策**
  - `POST /auth/*` の 5rpm（IP単位）が既に `POST /auth/refresh` をカバーしており、128bit トークンにより総当たりは計算上不可能と確認
  - Token Rotation による盗難検知フローが現実的な脅威（トークン盗難）をカバー済み
  - 根拠を `api-abstract.md §4`「ブルートフォース耐性について」に明記済み。追加実装なし

---

## High

- [x] **保護者同意メールの再送UI**
  - 再送ボタン（1日1回）は既に `screens-and-modes.md §1.5.6` に仕様化済みだった
  - 追記: 再送上限超過時のグレーアウト表示・メッセージ文言
  - 追記: リンク期限切れ（72h超過）による再送は1日1回制限を免除（`reason=link_expired`）

- [x] **速度チート検知閾値のプレイテスト**
  - ソーシャルゲームの性質上、速度チートによる実害は軽微（対戦・経済的損害なし）
  - 精密な誤検知対策より実装・プレイテスト時のエンジニア判断に委ねる方針に決定
  - 既存コメント「プレイテスト後に調整可」をそのまま維持。ローンチ前確認事項として下記に記録
  - **ローンチ前確認**: 実際のプレイで MAX_SPEED_THRESHOLD = 12m/s が誤検知を起こさないか確認し、必要に応じて調整する

- [x] **アップロードコンテンツのモデレーション実装定義**
  - `avatars` / `accessories` テーブルに `moderation_status VARCHAR(10)` を追加（`'pending'` / `'approved'` / `'rejected'`）
  - trust_level が `visitor` / `new_user` のアップロードは `pending`（検疫）、`user` 以上は `approved`（即時公開）
  - **検疫中の visibility**: アップロード本人は装備・使用可（動作確認のため）。ルーム内他プレイヤーにはフォールバック表示。他ユーザーのプロフィール閲覧からは不可視
  - CSAM 対応: テクスチャをハッシュ照合（Google CSAI Match、ローンチ時は no-op ハンドラー）→ ヒット時は即時 `rejected` + `admin_alerts` 記録 + IHC 通報フロー
  - `admin_alerts` テーブルを新設（CSAM 検知等のアラート記録用）
  - 旧 `accessories.needs_review` フラグは廃止し `moderation_status` に統一
  - 詳細: `api-abstract.md §8`（VRM Optimizer）・`§9`（アクセサリ GLB Optimizer）に追記済み

- [x] **EU拡張前のGDPR DPA対応確定**
  - ローンチ時は App Store / Google Play 配信地域から EU/EEA 30 か国を除外し、GDPR 適用対象外とする方針に確定
  - EU 配信解禁チェックリスト（Art.27 代理人・SCC・DPA 締結等）を `development-plan.md` に追加済み
  - `gdpr-todo.md §3` に「EU 配信解禁時に対応」として先送り理由と対応項目を明記済み

---

## Medium

- [x] **侵害通知の自動アラート整備**
  - 3 レイヤーの検知アラート仕様は `infra-abstract.md §10`・`breach-notification-plan.md §9` に定義済み
  - 実装タスク（Pub/Sub・discord-notifier・Cloud Monitoring・Cloud Logging・Go 構造化ログ）を `development-plan.md` Phase 5B「本番環境モニタリング設定」として追加済み

- [x] **削除ユーザーIDのパブリックAPI漏洩防止確認**
  - 一般方針は `api-abstract.md §13` に既に定義済み（`deleted_at IS NOT NULL` → 公開 API からも即時除外）
  - 対象エンドポイント一覧・実装方針・Go 統合テスト要件を `development-plan.md` Phase 5A「アカウント削除エンドポイント」サブタスクとして追加済み

- [x] **バッチ削除（IPアドレス等1年自動削除）の失敗監視**
  - 全バッチ共通失敗ポリシー（3 回リトライ → Discord 通知）を `api-abstract.md §13` に定義済み
  - Cloud Scheduler 失敗メトリクスアラート・バッチ完了ログ監視（サイレント失敗検知）を `development-plan.md` Phase 5B に追加済み
  - `gdpr-todo.md §11` もチェック済み

- [x] **保護者同意メールのRedis消失対策**
  - `api-abstract.md` の「Redis に保存・DB には保存しない」を廃止し、`active_users.parental_email`（DB）を唯一の保持場所に統一
  - `send` エンドポイントで毎回 UPDATE・再送で別アドレス指定時も上書き・同意完了/タイムアウト時に NULL 化
  - Day 7 リマインドジョブは DB から取得するため Redis クラッシュの影響なし
  - `development-plan.md` Phase 5A の実装設計（`parental_email` カラム + Cloud Tasks）とも整合

---

## Low

- [x] **Vivox音声非録音の独自確認**
  - Vivox プライバシーポリシーを確認済み: 音声データはエフェメラル（録音・保存なし）
  - Vivox が最大 30 日間保持するデータ（ユーザー ID・デバイス ID・末尾切り詰め IP）を `privacy-policy-elements.md §6` に正確に反映済み
  - Vivox には実ユーザー UUID の代わりに `vivox_id`（仮名 ID）を渡す設計に変更。アカウント削除時に再生成して旧 ID を切り離す（`api-abstract.md §4`）
  - `gdpr-todo.md §10` の Vivox 関連項目をクローズ済み

- [x] **サードパーティプライバシーポリシーの定期レビュー**
  - 四半期レビューチェックリスト（年 4 回・1月/4月/7月/10月の第1週）を `gdpr-todo.md §14` に定義済み
  - 対象 8 サービスのポリシー確認・削除バッチ実行ログレビュー・PP 整合性確認・インシデント記録レビューを網羅

- [x] **Cloud Loggingフォールバック時のディスク容量監視**
  - 2 段階閾値設計（40MB 警告・50MB 停止）を `infra-abstract.md §9` に定義済み
  - 警告は書き込みを継続しつつ早期通知。停止時は管理画面 + Discord（法的リスクとして Pub/Sub パイプライン経由）に通知
  - 実装タスクを `development-plan.md` Phase 5B「フォールバックログのディスク保護」として追加済み

- [x] **P/Invokeプラグインのライブラリロード検証**
  - iOS: `.a` は静的リンクでビルド時にアプリ本体に統合 → App Store 署名で保護・追加検証不要
  - Android: `.so` は署名済み APK 内に格納 → APK 署名を破らずに差し替え不可・追加検証不要
  - 根拠を `unity-game-abstract.md §8.10`「ライブラリロード時の検証」に明記済み
