# インフラ構成概要

個人運営を前提にコスト最適化した構成。

---

## 1. 採用構成

```
Cloudflare R2 + CDN（ストレージ・静的配信）
    ↕
Cloud Run（Go API + VRM Optimizer）   ← リクエストゼロ時は課金なし
    ↕
Cloud SQL PostgreSQL f1-micro         ← $7/月〜
    ↕
Cloud Tasks（ジョブキュー）           ← ほぼ無料
```

---

## 2. サービス選定理由

### Cloudflare R2 + CDN（ストレージ）

- **egress 料金ゼロ**。VRM/GLB/PNGなど静的ファイルの配信コストがスケールしない
- S3互換APIのため実装変更なし（環境変数 `STORAGE_BACKEND=r2` で切替）
- 静的ファイルはCDNキャッシュから直接配信。APIサーバーを経由しない
- Google Cloud Storage と比較した場合、compute → storage の書き込みパス（アバター500KB上限）は微差で負けるが、支配的なread配信コストで圧倒的に有利

### Google Cloud Run（コンピュート）

- リクエストがない時間帯はCPU課金ゼロ（個人運営の低トラフィック帯に最適）
- ECS FargateやApp Runnerと比べてシンプルな設定で動作
- Cloud SQL Auth Proxy が無料で組み込まれており、DB接続管理が容易
- VRM Optimizer も別サービスとして Cloud Run にデプロイ（非同期ジョブのみ起動）

### Cloud SQL PostgreSQL（DB）

- Cloud Run と同一Google Cloudネットワーク内のため低レイテンシ
- AWSのRDS（最小~$13/月）よりCloud SQL f1-micro（~$7/月）が安い
- RDS Proxyが不要（Cloud SQL Auth Proxyは無料）

### Cloud Tasks（ジョブキュー）

- Redisインスタンスを廃止できる（VRMアップロードのキューにのみ使用していたため）
- 最初の100万タスク/月が無料
- VRM Optimizer は Cloud Tasks からのHTTPリクエストで起動する Cloud Run サービスとして実装

### Unity Gaming Services（Relay / Vivox）

- **Relay**: Netcode for GameObjects のP2P中継。モバイル環境のNAT越えに必要
- **Vivox（Unity Voice Chat）**: 3D空間音声。PCUベース課金（後述）

---

## 3. コスト概算

### 使用量モデルの前提

| 指標 | 仮定 |
|---|---|
| DAU/MAU比 | 30% |
| 平均セッション | 30分・1.5回/日 |
| API呼び出し | 約2,250 req/月/MAU |
| Relayデータ | ~1MB/セッション/人 |
| 音声通話時間 | セッションの50%（15分） |
| ユーザー分布 | 日本集中（ピーク20〜22時の2時間に60%集中） |

PCU推計式:

```
DAU = MAU × 30%
ピーク平均同時接続 = (DAU × 1.5 × 30分) × 60% ÷ 120分
PCU ≈ ピーク平均 × 1.5（ピーク係数）
→ PCU ≈ MAU × 10%
```

### MAU別コスト概算

| MAU | PCU | Cloud Run | Cloud SQL | Relay | Vivox | R2 | 月額合計 |
|---|---|---|---|---|---|---|---|
| 数人 | ~1 | $0 | $7 | $0 | $0 | $0 | **~$7** |
| 100 | ~10 | $0 | $7 | $0 | $0 | $0 | **~$7** |
| 1万 | ~1,000 | $20 | $27 | $5 | $0 | $0 | **~$52** |
| 5万 | ~5,000 | $95 | $120 | $34 | $0 | $1 | **~$250** |
| 5万強 | ~5,100 | $100 | $125 | $35 | $2,000 | $1 | **~$2,260** |
| 100万 | ~100,000 | $2,300 | $350 | $730 | $33,000 | $20 | **~$36,400** |

---

## 4. Vivox料金体系と注意点

### 料金表（PCUベース月額）

| PCUレンジ | 5,000PCUあたり |
|---|---|
| 〜5,000 | **無料** |
| 5,001〜50,000 | $2,000 |
| 50,001〜100,000 | $1,500 |
| 100,001〜200,000 | $1,250 |
| 200,001〜 | $1,000 |

### PCU 5,000の崖

PCU 5,000（≒ MAU 5万）を1でも超えた瞬間に**最初のブロック$2,000が丸ごと発生する**。なだらかな増加ではなくステップ状の段差。

```
PCU 5,000:  $0
PCU 5,001:  $2,000  ← 崖
PCU 10,000: $2,000
PCU 10,001: $4,000  ← 次の崖
```

### 対策（LiveKit移行）

PCU 4,000（≒ MAU 4万）到達時点でCloud MonitoringのアラートをトリガーしてLiveKit移行作業を開始し、PCU 5,000到達前に切り替えを完了させる。

| | Vivox（PCU 5,001〜） | LiveKit OSS自己ホスト |
|---|---|---|
| コスト（MAU5万超時点） | $2,000〜/月 | GCE e2-medium ~$50〜100/月 |
| Unity SDK | あり | あり |

LiveKitはWebRTCのUDPポートが必要なためCloud Runでは動かない。GCE（VM）で運用する。

---

## 5. スケール時の構成変更判断

| MAU | 対応 |
|---|---|
| 〜1万 | Cloud SQL f1-micro で十分 |
| 1万〜5万 | Cloud SQL g1-small〜n1-standard-2 に昇格 |
| **4万到達** | **LiveKit移行作業開始（Vivox崖の手前）** |
| 5万〜 | LiveKit（GCE）に移行済み |
| 10万〜 | Relay自前化を検討（UGS Relay が高額になるため） |
| 100万〜 | Cloud SQL read replica・Cloud Run スケールアウト |

---

## 6. PCU モニタリング

Vivox 無料枠（PCU 5,000）への接近を検知するためのスナップショット収集と管理画面連携。

### 計測方法

Cloud Tasks の cron ジョブが **1分ごと**に現在のルーム参加者総数を集計し `pcu_snapshots` テーブルに記録する。

```
現在PCU = SELECT COUNT(*) FROM room_participants WHERE left_at IS NULL
```

これは Vivox 接続中ユーザー数と同義（ルーム参加中 ＝ Vivox 接続中）。

### DB スキーマ

```sql
CREATE TABLE pcu_snapshots (
  id           BIGSERIAL    PRIMARY KEY,
  recorded_at  TIMESTAMPTZ  NOT NULL DEFAULT now(),
  concurrent_users INTEGER  NOT NULL
);
CREATE INDEX ON pcu_snapshots (recorded_at DESC);
```

- 保持期間: 90日。90日以上経過したレコードは定期バッチで削除する
- 1分ごと記録 × 90日 = 最大 129,600 レコード（容量影響は軽微）

### 管理画面 API

```
GET /admin/metrics/pcu?period=7d|30d|90d
```

```json
{
  "data": {
    "current": 1234,
    "max_in_period": 2100,
    "vivox_free_limit": 5000,
    "alert_threshold": 4000,
    "snapshots": [
      { "recorded_at": "2026-04-02T13:00:00Z", "max_concurrent_users": 980 },
      ...
    ]
  }
}
```

- `snapshots` は1時間ごとの最大値に集約して返す（グラフ描画用）
- UI仕様: `docs/screens-and-modes.md` セクション 8.10 参照

### アラート

直近1時間の最大PCUが **4,000 を超えた**場合、`pcu_threshold_warning` アラートを生成する（`docs/screens-and-modes.md` セクション 8.9 参照）。

- 同一アラートの重複生成を避けるため、未対応の `pcu_threshold_warning` が存在する間は新規生成しない
- PCUが4,000を下回った状態が24時間継続したら抑制解除する（再上昇時に再通知）

---

## 7. Relay 使用量モニタリング

UGS Relay の無料枠（月5GB）の消化状況を把握するための計測。崖はないが、月次コストに現れる最初のサービスであり、LiveKit 移行の判断材料にもなる。

### 計測方法

Relay の使用量は UGS ダッシュボード（Unity Gaming Services コンソール）から取得できる。ただし API 経由での自動取得が困難なため、**Go API 側でルームセッションのデータ量を推計して記録する**。

推計式:

```
セッションのRelay推計量(MB) = セッション時間(秒) × 参加人数 × 単位レート(KB/秒/人)
```

単位レートは実測値で校正するが、初期値として **0.3 KB/秒/人** を使用する（20Hz・30バイト/パケット・移動率30%の仮定）。

セッション終了時（退室 API 呼び出し時）に推計値を `relay_usage_log` テーブルに記録する。

### DB スキーマ

```sql
CREATE TABLE relay_usage_log (
  id              BIGSERIAL   PRIMARY KEY,
  room_id         TEXT        NOT NULL,
  session_end_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
  duration_sec    INTEGER     NOT NULL,
  participant_count INTEGER   NOT NULL,
  estimated_mb    NUMERIC(10,3) NOT NULL
);
CREATE INDEX ON relay_usage_log (session_end_at DESC);
```

- 保持期間: 90日

### 管理画面 API

```
GET /admin/metrics/relay?period=current_month|7d|30d
```

```json
{
  "data": {
    "period_usage_mb": 3820,
    "period_usage_gb": 3.73,
    "free_limit_gb": 5.0,
    "overage_gb": 0,
    "estimated_month_end_gb": 4.9,
    "overage_cost_usd": 0
  }
}
```

- `estimated_month_end_gb`: 当月の日割り推計（当月1日〜現在の使用量から月末を線形予測）
- UI仕様: `docs/screens-and-modes.md` セクション 8.10 参照

### アラート

| 条件 | アラート種別 |
|---|---|
| 当月推計が月末に 4.5GB を超える見込み | `relay_usage_warning` |

重複抑制: 未対応の同種アラートが存在する間は新規生成しない。翌月1日に自動リセット。

---

## 8. バックアップ・障害復旧

### Cloud SQL

| 設定 | 値 |
|---|---|
| 自動バックアップ | 毎日（保持 7 日） |
| PITR（ポイントインタイムリカバリ） | 有効（最大 35 日） |
| バックアップ取得時刻 | 深夜 4:00 JST（低トラフィック帯） |

PITR を有効にすることで、誤ったデータ削除・更新を任意の時点に巻き戻せる。Cloud SQL の自動バックアップは同一リージョン内の別ゾーンに保存されるため、ゾーン障害には耐性がある。

### Cloudflare R2

R2 は Cloudflare 内部で冗長化済みのため、追加バックアップは不要。バージョニングは **無効**（このプロジェクトはコンテンツアドレス型で上書きが発生しないため、バージョニングによる旧ファイル保持は不要かつコスト増になる）。

### RTO / RPO 目標

| 指標 | 目標値 |
|---|---|
| RPO（許容データ損失時間） | 1 時間 |
| RTO（サービス復旧目標時間） | 4 時間 |

個人運営のため SLA 保証は設けないが、障害発生時の復旧手順の目安として定める。

---

## 9. 監査ログの外部転送

管理者操作監査ログ（`admin_audit_logs`）を改ざん防止のため Cloud Logging へリアルタイム転送する。

**目的:** DB が侵害・操作されてもログの外部コピーが残る。

**実装方針:**
- Go API の監査ログミドルウェアが `admin_audit_logs` への INSERT と同時に Cloud Logging SDK でログエントリを書き込む
- Cloud Logging のロググループ名: `lowpolyworld-admin-audit`
- 保持期間: Cloud Logging 側で 1 年に設定
- アクセス権限: Cloud Logging への書き込みは API サービスアカウントに付与。読み取りは `super_admin` ロールのみ

**転送失敗時のフォールバック:**

Cloud Logging への書き込みは `admin_audit_logs` への INSERT とは独立して行う（Cloud Logging の失敗が DB 書き込みをロールバックしない）。

| 状態 | 対応 |
|---|---|
| Cloud Logging SDK 書き込み失敗 | エラーをローカルファイルログ（`/var/log/app/audit-fallback.log`）に記録し処理を継続 |
| フォールバックログ蓄積 | バックグラウンドリトライゴルーチンが 30秒・2分・10分 の指数バックオフで最大 3 回再送を試みる |
| 3 回失敗後 | 管理画面のシステムアラートに「Cloud Logging 転送失敗」アラートを生成（`docs/screens-and-modes.md` セクション 8.9 参照）。フォールバックログは Cloud Logging 復旧後に手動インポートまたは再起動時の自動再送で取り込む |
| Cloud Logging 長期停止（1 時間以上） | Cloud Monitoring のアップタイムチェックが検知し、アラートポリシー経由でメール通知 |

`admin_audit_logs` テーブルの DB 書き込みは常に保証される。Cloud Logging は改ざん防止の外部コピーであり、一時的な転送失敗は DB 側の記録で補完できる。

DB ユーザーの権限制限（`admin_audit_logs` への DELETE / UPDATE 禁止）と合わせて二重の改ざん防止とする（`docs/api-abstract.md` セクション 10 参照）。

---

## 10. 侵害検知・アラート

データ侵害（ブリーチ）の早期検知のため、3 レイヤーの検知を組み合わせる。すべてのアラートは **メール（nibankougen@gmail.com）と Discord（運営者専用サーバー）** に通知する。

### 10.1 通知チャネル設定

#### メール通知
Cloud Monitoring の Notification Channel に `nibankougen@gmail.com` を登録する。

#### Discord 通知
Cloud Monitoring はネイティブで Discord に対応していないため、以下のアダプターを経由する。

```
Cloud Monitoring アラート発火
    ↓ Pub/Sub トピック（lowpolyworld-security-alerts）
    ↓ Cloud Functions（Go）discord-notifier
    ↓ Discord Webhook URL（Secret Manager で管理）
    ↓ 運営者専用 Discord サーバー #security-alerts チャンネル
```

**`discord-notifier` Cloud Functions の仕様:**
- ランタイム: Go（既存 API と同言語）
- トリガー: Pub/Sub（`lowpolyworld-security-alerts` トピック）
- 処理内容: Cloud Monitoring の JSON ペイロードを Discord Embed 形式に変換して Webhook POST
- シークレット: Discord Webhook URL を Secret Manager に格納（環境変数 `DISCORD_WEBHOOK_URL`）
- 費用: Cloud Functions 無料枠（200 万回/月）内で収まる

**Cloud Monitoring の全アラートポリシーに `lowpolyworld-security-alerts` トピックを Notification Channel として設定する。**

---

### 10.2 レイヤー 1：インフラ異常（Cloud Monitoring メトリクスアラート）

Cloud コンソールのみで設定。コード変更なし。

| アラートポリシー名 | 条件 | 通知 |
|---|---|---|
| `high-5xx-error-rate` | Cloud Run の 5xx エラーレートが 5 分間で 5% 超 | メール・Discord |
| `high-sql-connections` | Cloud SQL 接続数が最大接続数の 80% 超（5 分間持続） | メール・Discord |
| `high-sql-cpu` | Cloud SQL CPU 使用率が 90% 超（5 分間持続） | メール・Discord |
| `api-uptime-check` | Cloud Run API が 1 分以上無応答（既存設定を流用） | メール・Discord |

設定場所: Google Cloud Console → Monitoring → Alerting

---

### 10.3 レイヤー 2：監査ログ異常（Cloud Logging ログベースアラート）

`lowpolyworld-admin-audit` ロググループに対するログベースアラート。コード変更なし。

| アラート名 | Cloud Logging フィルタ（例） | 集計ウィンドウ | しきい値 | 通知 |
|---|---|---|---|---|
| `mass-admin-delete` | `logName="lowpolyworld-admin-audit" AND jsonPayload.action=~"delete_"` | 1 時間 | 100 件超 | メール・Discord |
| `after-hours-admin-op` | `logName="lowpolyworld-admin-audit" AND timestamp >= "T23:00:00+09:00"` もしくは `<= "T07:00:00+09:00"` | — | 1 件でも発生 | メール・Discord |

設定場所: Google Cloud Console → Logging → Log-based Alerts

---

### 10.4 レイヤー 3：アプリケーション異常（Go API 構造化ログ + Cloud Logging アラート）

Go API が検知イベントを Cloud Logging に構造化ログとして出力し、Cloud Logging 側でアラートを発火する。アラートロジックは Cloud Logging に集約し、Go 側はログ出力のみ担当するため維持コストは低い。

**ログ出力フォーマット:**

```go
// 全セキュリティイベント共通フォーマット
logger.Warn("security_event",
    "type",    "<イベント種別>",  // 下表参照
    "ip",      clientIP,
    "user_id", userID,           // 認証済みの場合のみ
    "detail",  "<補足情報>",
)
```

**検知対象・しきい値・Go 実装箇所:**

| イベント種別（`type`） | しきい値 | 実装箇所 | Cloud Logging アラート条件 |
|---|---|---|---|
| `brute_force_attempt` | 同一 IP が 1 分間に認証失敗 20 回超（レートリミット発動時に出力） | 認証ミドルウェア（`/auth/{provider}/callback`） | 1 時間以内に同一 IP で 5 件以上 |
| `mass_token_revocation` | 1 時間以内に 100 ユーザー以上の `token_revision` インクリメント | `POST /auth/logout`・強制無効化処理 | 1 時間以内に 100 件以上 |
| `high_api_rate` | 同一 `user_id` が 1 分間に 500 リクエスト超（既存レートリミット発動時） | API レートリミットミドルウェア | 1 時間以内に同一 user_id で 10 件以上 |

**しきい値の調整方法:** Cloud Logging のアラートポリシー設定変更のみ。Go コードの変更は不要。

**Cloud Logging フィルタ（例）:**
```
jsonPayload.type="brute_force_attempt"
```

---

### 10.5 システムアラートテーブルへの統合

レイヤー 3 のイベントは `system_alerts` テーブル（管理画面 8.9）にも挿入し、管理画面から確認できるようにする。

追加するアラート種別:

| 種別 | 発生条件 | 手動対応 |
|---|---|---|
| `brute_force_detected` | 同一 IP が 1 時間以内に 5 回以上 `brute_force_attempt` を出力 | 対象 IP のアクセスログを確認。必要に応じて Cloud Armor 等でブロック |
| `mass_token_revocation_detected` | 1 時間以内に 100 ユーザー以上でトークン無効化 | 管理者操作ログを確認。意図しない操作なら侵害として対応 |
| `high_api_rate_detected` | 同一ユーザーが 1 時間以内に 10 回以上レートリミット超過 | 対象ユーザーのアクセスログを確認 |

---

## 11. 環境構成（ローカル開発 / 本番）

| 環境 | コンピュート | DB | ストレージ | キュー |
|---|---|---|---|---|
| ローカル開発 | Docker Compose | PostgreSQL（Docker volume）| ローカルファイルシステム | Redis（Docker）|
| 本番 | Cloud Run | Cloud SQL PostgreSQL | Cloudflare R2 | Cloud Tasks |

本番と開発の切替は環境変数で行う（`STORAGE_BACKEND=local\|r2`、`QUEUE_BACKEND=redis\|cloudtasks`）。
