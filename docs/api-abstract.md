# API・バックエンド 設計概要

Go REST API / VRM optimizer / Rust paint-engine のアーキテクチャ方針をまとめる。

---

## 1. サービス構成

```
┌─────────────────┐   HTTP    ┌──────────────────┐
│  Unity Client   │ ────────▶ │   Go API Server  │ ─── PostgreSQL
└─────────────────┘           │   (port 8080)    │ ─── Redis
                              └──────────────────┘
                                      │ Job Queue (Redis)
                                      ▼
                              ┌──────────────────┐
                              │  VRM Optimizer   │
                              │   (port 9090)    │
                              └──────────────────┘
                                      │
                              ┌──────────────────┐
                              │ Cloudflare R2    │  ← 本番ストレージ
                              │ (/ Local volume) │  ← 開発ストレージ
                              └──────────────────┘
                                      │ CDN
                              ┌──────────────────┐
                              │ Cloudflare CDN   │
                              └──────────────────┘
```

---

## 2. Docker Compose 構成（ローカル開発）

```yaml
services:
  api:
    build: ./api
    ports:
      - "8080:8080"
    environment:
      - DATABASE_URL=postgres://user:password@db:5432/lowpolyworld
      - REDIS_URL=redis://cache:6379
      - OPTIMIZER_URL=http://optimizer:9090
      - STORAGE_BACKEND=local
      - STORAGE_LOCAL_PATH=/data/assets
      - JWT_PRIVATE_KEY_PATH=/run/secrets/jwt_private_key
      - JWT_PUBLIC_KEY_PATH=/run/secrets/jwt_public_key
    volumes:
      - asset_data:/data/assets
    depends_on:
      db:
        condition: service_healthy
      cache:
        condition: service_started

  optimizer:
    build: ./optimizer
    ports:
      - "9090:9090"
    environment:
      - REDIS_URL=redis://cache:6379
      - STORAGE_BACKEND=local
      - STORAGE_LOCAL_PATH=/data/assets
    volumes:
      - asset_data:/data/assets
    depends_on:
      - cache

  db:
    image: postgres:16-alpine
    environment:
      - POSTGRES_USER=user
      - POSTGRES_PASSWORD=password
      - POSTGRES_DB=lowpolyworld
    volumes:
      - db_data:/var/lib/postgresql/data
    ports:
      - "5432:5432"
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U user"]
      interval: 10s
      timeout: 5s
      retries: 5

  cache:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    volumes:
      - cache_data:/data

volumes:
  db_data:
  cache_data:
  asset_data:
```

---

## 3. DB 設計方針

### エンジン・バージョン

- **PostgreSQL 16**
- テキスト検索に `pg_trgm` 拡張を使用（ワールド名・ユーザー名検索）

### マイグレーション管理

- ツール: **golang-migrate** (`github.com/golang-migrate/migrate/v4`)
- ファイル配置: `api/migrations/`
- ファイル命名: `{連番}_{説明}.up.sql` / `{連番}_{説明}.down.sql`
  - 例: `001_initial_schema.up.sql`, `002_add_trust_level.up.sql`
- アプリ起動時に自動マイグレーション実行（`migrate.Up()`）
- CI での実行: `make migrate-up` / `make migrate-down`

---

## 4. 認証システム（JWT）

### アルゴリズム・トークン仕様

| 項目 | 値 |
|---|---|
| 署名アルゴリズム | **RS256**（非対称鍵。将来のサービス分離に対応） |
| アクセストークン有効期限 | **7日** |
| リフレッシュトークン有効期限 | **90日**（使用のたびに更新される） |
| 鍵管理 | PEM 形式。開発はファイル、本番は Secrets Manager |

### JWT payload（アクセストークン）

```json
{
  "sub": "<user_id>",
  "rev": 1,
  "iat": 1704067200,
  "exp": 1704672000
}
```

- `sub`: `users.id`
- `rev`: `active_users.token_revision`（強制無効化に使用）

### リフレッシュトークン

アクセストークンの有効期限切れ後もログイン状態を維持するための長命トークン。

**形式:** クライアントには不透明な乱数文字列（128bit）を渡す。DB には SHA-256 ハッシュのみ保存する。

**`refresh_tokens` テーブル:**

| カラム | 型 | 説明 |
|---|---|---|
| id | BIGSERIAL PK | |
| user_id | TEXT NOT NULL | `users.id` 参照 |
| token_hash | TEXT NOT NULL UNIQUE | 乱数トークンの SHA-256 ハッシュ |
| expires_at | TIMESTAMPTZ NOT NULL | 発行から 90 日 |
| revoked_at | TIMESTAMPTZ | 無効化日時（NULL = 有効） |
| created_at | TIMESTAMPTZ NOT NULL DEFAULT now() | |

**`refresh_tokens` 追加カラム:**

| カラム | 型 | 説明 |
|---|---|---|
| `device_name` | TEXT | デバイス名（省略可。例: `iPhone 15`）。セッション一覧表示用 |

**複数デバイスセッションポリシー: 最大 3 デバイスまで同時ログイン可**

ログイン（`POST /auth/{provider}/callback` または `POST /auth/refresh` での新規発行）時に同一ユーザーのアクティブセッション数を確認する。

```
有効セッション = revoked_at IS NULL AND expires_at > now()

有効セッション数 < 3  → 通常通り新しいリフレッシュトークンを発行
有効セッション数 >= 3 → created_at が最も古いリフレッシュトークンを
                        revoked_at = now() で失効させてから新しいトークンを発行
```

失効させられた旧デバイスは次回 `POST /auth/refresh` で `401 refresh_token_invalid` を受け取り、ログアウト状態になる。

**Token Rotation:** `POST /auth/refresh` を呼ぶたびに旧リフレッシュトークンを即時無効化し、新しいアクセストークン＋リフレッシュトークンのペアを発行する。

**盗難検知フロー（Refresh Token Rotation による検知）:**

```
POST /auth/refresh を受信
  ├── token_hash で refresh_tokens を検索
  │     ├── レコードが存在しない
  │     │   → 401 refresh_token_invalid（改ざん・存在しないトークン）
  │     │
  │     ├── レコードが存在し、revoked_at IS NULL かつ expires_at > now()
  │     │   → 通常のローテーション処理:
  │     │       1. 旧トークンの revoked_at = now() を SET（無効化）
  │     │       2. 新しいアクセストークン + リフレッシュトークンを発行・返却
  │     │
  │     ├── レコードが存在し、expires_at <= now()
  │     │   → 401 refresh_token_expired（期限切れ）
  │     │
  │     └── レコードが存在し、revoked_at IS NOT NULL  ← 盗難の可能性
  │         （すでに無効化されたトークンが再使用された）
  │         → 盗難検知処理:
  │             1. 該当 user_id の全リフレッシュトークンを即時無効化
  │                UPDATE refresh_tokens SET revoked_at = now()
  │                WHERE user_id = $1 AND revoked_at IS NULL
  │             2. active_users.token_revision をインクリメント
  │                （既存アクセストークンを全て無効化）
  │             3. 401 refresh_token_invalid を返す
  │             4. WARN レベルでログを記録（user_id・source_ip・request_id）
  │             5. 管理画面アラートは生成しない（正規ユーザーが自分で気づける）
```

> 盗難されたトークンが使用された場合、正規ユーザーが次回 refresh を呼んだ時点で全セッションが強制ログアウトされる。ユーザーは再ログインが必要になるが、攻撃者も同時にセッションを失う。

**ブルートフォース耐性について:**

`POST /auth/refresh` は `POST /auth/*` のレート制限カテゴリに含まれ、IP 単位で **5 rpm** が適用される。リフレッシュトークンは 128bit 乱数（約 3.4×10³⁸ の組み合わせ）であるため、5 rpm の制限下での総当たりは計算上不可能（宇宙の年齢を超える時間が必要）。追加のロックアウト機構は不要と判断する。

現実的な脅威はブルートフォースではなくトークン盗難であり、これは上記の Token Rotation による盗難検知フローで対応する。

**エンドポイント:**

```
POST /auth/refresh
  body: { "refresh_token": "<opaque>" }
  → 200: { "data": { "access_token": "<JWT>", "refresh_token": "<new_opaque>", "expires_in": 604800 } }
  → 401: refresh_token_expired / refresh_token_invalid

POST /auth/logout
  header: Authorization: Bearer <access_token>
  body: { "refresh_token": "<opaque>" }
  → 204: リフレッシュトークンを revoked_at に設定して無効化
```

### 年齢確認・age_group

**`active_users` 追加カラム:**

| カラム | 型 | 説明 |
|---|---|---|
| `age_group` | `ENUM('young_teen','teen','adult') NOT NULL` | 13〜15歳 / 16〜17歳 / 18歳以上。生年月日は保存しない（GDPR データ最小化原則） |
| `age_verified_at` | `TIMESTAMPTZ NOT NULL` | 年齢確認実施時刻 |
| `parental_consent_verified_at` | `TIMESTAMPTZ` | 保護者同意完了時刻。`young_teen` のみ必要。NULL = 未完了または不要 |
| `locale` | `VARCHAR(10) NOT NULL DEFAULT 'ja-JP'` | アカウント作成時点のデバイスロケール（例: `ja-JP`・`de-DE`）。GDPR Art. 33 対応でのデータ侵害報告先 DPA 特定に使用。取得方法: iOS `Locale.current.identifier` / Android `Locale.getDefault().toLanguageTag()`。アカウント作成時に `POST /auth/{provider}/callback` リクエストボディに含めてサーバー側で保存する |
| `last_seen_security_notice_id` | `TEXT` | 最後に確認済みのセキュリティ通知 ID（`system_security_notices.id`）。NULL = 未確認の通知なし。起動時に未確認通知の有無判定に使用 |
| `vivox_id` | `UUID NOT NULL DEFAULT gen_random_uuid()` | Vivox ログイン時に渡す仮名 ID。実ユーザー UUID の代わりに使用し、Vivox 側に実 ID を渡さない。アカウント削除（ソフトデリート）時に再生成して旧 ID を切り離す |

**新規アカウント作成時の判定:**

`POST /auth/{provider}/callback` に `birth_date: "YYYY-MM-DD"` を含める（新規アカウントのみ）。

```
1. birth_date から年齢を計算（サーバー側の現在日時を基準）
2. 13歳未満   → 403 age_restricted。アカウントは作成しない
3. 13〜15歳   → age_group = 'young_teen'。アカウント作成後に保護者同意フローを開始
4. 16〜17歳   → age_group = 'teen'
5. 18歳以上   → age_group = 'adult'
6. birth_date は DB に保存せず即時破棄
```

レスポンスに `parental_consent_required: true` を含め、クライアントは保護者同意画面へ誘導する（`docs/screens-and-modes.md` セクション 1.5.6 参照）。

**既存ユーザーの移行:**

既存アカウントはサービス開始前に作成されたため `age_group = 'adult'`・`age_verified_at = マイグレーション実行時刻` を初期値として設定する。

### 保護者同意フロー

#### `parental_consents` テーブル

`active_users.parental_consent_verified_at` とは別に、監査証跡として同意記録を保持するテーブル。

```sql
CREATE TABLE parental_consents (
    id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id       UUID NOT NULL REFERENCES users(id) ON DELETE NO ACTION,
    verified_at   TIMESTAMPTZ NOT NULL,
    consent_version VARCHAR(20) NOT NULL  -- 同意時点の利用規約・PPバージョン（例: "2026-04-01"）
);
```

- `user_id` は `users.id`（不変テーブル）を参照 → `active_users` 削除後も整合性が保たれる
- メールアドレスは保存しない（GDPR データ最小化原則）
- **保持期間: アカウント削除後も 5 年間保持**（COPPA・GDPR 監査証跡要件。2025 年 COPPA 改正で書面保持が義務化）
- 5 年後の物理削除: 毎日実行の削除バッチ（`DELETE FROM parental_consents WHERE verified_at < NOW() - INTERVAL '5 years'`）

#### API エンドポイント

```
POST /api/v1/me/parental-consent/send
  認証: 必須（young_teen アカウントのみ有効。他の age_group は 403）
  body: { "email": "parent@example.com" }
  処理:
    1. JWT（有効期限 72 時間・sub = user_id・jti = UUID）を発行
    2. 指定メールアドレスへ確認メールを送信
    3. active_users.parental_email を指定メールアドレスで UPDATE（再送で別アドレスを指定した場合も上書き）
    4. jti を Redis に保存（TTL 72 時間。アドレス変更時の旧リンク無効化に使用）
  レート制限: 1 日 1 回（超過時 429）
  → 204 No Content

GET  /api/v1/parental-consent/verify?token=<jwt>
  認証: 不要（ブラウザで直接アクセス）
  処理:
    1. JWT 署名検証・有効期限確認
    2. Redis で jti が有効か確認（無効化済みなら失敗）
    3. active_users.parental_consent_verified_at を記録
    4. parental_consents テーブルに監査レコードを挿入
    5. タイムアウトジョブ（Day 7 リマインド・Day 14 削除）をキャンセル
  成功時: 「同意が完了しました。アプリをご確認ください。」HTML を返す
  失敗 / 期限切れ / 無効化済み: 「リンクが無効または期限切れです。アプリで再送してください。」HTML を返す
```

#### タイムアウトジョブ（asynq）

`young_teen` アカウント作成時に以下の 2 つのジョブをキューに登録する。同意完了時にキャンセル（`jti` をキャンセルキーとして使用）。

| ジョブ | 実行タイミング | 処理 |
|---|---|---|
| `parental_consent_reminder` | アカウント作成から 7 日後（同意未完了の場合のみ） | リマインドメールを送信（「あと 7 日で登録が取り消されます」）。送信先メールアドレスは `active_users.parental_email`（DB）から取得する |
| `parental_consent_timeout` | アカウント作成から 14 日後（同意未完了の場合のみ） | `active_users` を即時物理削除（ソフトデリートを経由しない）。アップロードファイルを削除キューへ追加。操作を `admin_audit_logs` に記録（`action = 'parental_consent_timeout_deletion'`） |

**保護者メールアドレスの保持:**

`active_users.parental_email` に保持する（DB）。`send` エンドポイント呼び出しのたびに UPDATE（再送で別アドレスを指定した場合も上書き）。同意完了時・14 日タイムアウト時に NULL 化する。Redis には保存しない。

#### 13 歳未満誤登録の削除エンドポイント

生年月日を偽ってアカウントを作成した 13 歳未満ユーザーを管理者が削除するためのエンドポイント。COPPA §312.10「13 歳未満と判明したデータは速やかに削除」の要件に対応。

```
POST /api/v1/admin/users/{id}/delete-underage
  権限: super_admin のみ
  body: { "reason": "保護者からの通報。..." }
  処理:
    1. active_users を即時物理削除（ソフトデリートなし・30 日猶予なし）
    2. アップロードファイルを即時削除キューへ追加
    3. セッション無効化（レコード削除によりトークン検証が 401 になる）
    4. admin_audit_logs に記録（action = 'underage_deletion'、reason を notes に保存）
  → 204 No Content
```

管理画面の操作フロー（取り消し不可のため 2 段階確認）:
1. 削除ボタン → 「本当に削除しますか？この操作は取り消せません。」確認ダイアログ
2. 理由入力（必須）+ 「UNDERAGE」文字入力 → 削除実行

### 強制無効化（アカウント削除・不正検知時）

`active_users.token_revision` をインクリメントする。API サーバーはアクセストークン検証時に `rev` と DB の `token_revision` を照合し、不一致なら 401 を返す。`POST /auth/refresh` でも同様に照合し、不一致なら新しいアクセストークンを発行しない。

### ソーシャルサインイン フロー

```
1. Unity → POST /auth/{provider}/callback  (OAuth code を送信)
2. API → OAuth プロバイダーでトークン検証・ユーザー情報取得
3. API → users / active_users にアカウント作成 or 照合
4. API → アクセストークン（7日）＋リフレッシュトークン（90日）を生成して返却
5. Unity → 両トークンを Application.persistentDataPath に保存
6. API 呼び出し時 → アクセストークンを Authorization ヘッダに設定
7. アクセストークン期限切れ → POST /auth/refresh でサイレント更新
8. リフレッシュトークン期限切れ → 再ログイン
```

対応プロバイダー: Google / Apple（iOS 必須）

**サインイン・リフレッシュのレスポンス形式:**

```json
{
  "data": {
    "access_token": "<JWT>",
    "refresh_token": "<opaque>",
    "expires_in": 604800,
    "name_setup_required": false
  }
}
```

### @name 変更エンドポイント（`PUT /me/name`）

サーバーは以下の順で検証してから名前を更新する:

```
1. プレミアム会員か確認 → false なら 403 forbidden
2. last_name_change_at が NULL または (now() - last_name_change_at) ≥ 90日 → false なら 409 name_change_too_soon
3. NFKC 正規化・小文字化を適用（例: 全角 `Ａ` → 半角小文字 `a`）
4. @name の形式バリデーション（英数字・アンダースコア・3〜15文字）→ 不正なら 400 validation_error
5. @name の重複チェック → 重複なら 409 name_already_taken
6. active_users.name = 新しい名前、last_name_change_at = now() を同一トランザクションで更新
```

`last_name_change_at` は初回 @name セットアップ完了時にも設定する（初回設定からも 90 日カウント）。

### 初回サインイン時の @name 自動生成

新規アカウント作成時、`active_users.name` は NOT NULL 制約を持つ。OAuth 直後に一時 @name を自動生成してからレコードを作成する。

- 生成規則: `user_` + 8文字のランダム英小文字・数字（例: `user_a3k9mx2p`）
- 衝突時は再生成する（衝突率は極めて低いが最大 5 回リトライ）
- クライアントはレスポンスに `name_setup_required: true` が含まれる場合、@name 設定画面へ誘導する
- @name 設定画面で上書きするまで一時 @name が有効（他ユーザーからは見えない状態になるよう、ルームへの参加はセットアップ完了前に制限する）
- セットアップ完了後: `name_setup_required: false` として以降のレスポンスで返す

```json
// POST /auth/{provider}/callback レスポンス（新規アカウント時）
{
  "data": {
    "token": "<JWT>",
    "name_setup_required": true
  }
}
```

### 認証ミドルウェアの処理順序

すべての認証必須エンドポイントで以下の順に検証する:

```
1. JWT 署名検証・有効期限チェック → 失敗時: 401 unauthorized
2. JWT の rev と active_users.token_revision の照合 → 不一致時: 401 unauthorized
3. active_users.deleted_at の確認 → 設定済みの場合: 403 account_deleted
```

### PII と取引レコードの分離設計

個人情報（PII）と財務・監査レコードを別テーブルに分離することで、GDPR「忘れられる権利」とデータ保存義務を同時に満たす。

**テーブル分離の原則:**

| テーブル | 役割 | 削除タイミング |
|---|---|---|
| `users` | `id`（PK）と `created_at` のみ保持。変更・削除しない不変テーブル | 削除しない（財務・監査レコードの参照先として永続保持） |
| `active_users` | 表示名・@name・メールアドレス・OAuth 紐付け・設定・トラストレベル等の PII | 削除申請時に PII カラムを即時 NULL 化。30 日後に物理削除 |

**財務・監査レコードの参照方針:**

`coin_purchases`・`coin_transactions`・`user_violation_reports`・`admin_audit_logs` 等の長期保持が必要なレコードはすべて `users.id` を外部キーとして参照する（`active_users.user_id` ではない）。

これにより:
- `active_users` が削除されても財務・監査レコードの整合性は `users.id` で維持される
- 個人を特定できる情報（`active_users` の PII）は削除・NULL 化されるため、残存レコードは匿名化済みとみなせる
- 購入履歴の 7 年保持義務（法人税法）と GDPR「忘れられる権利」を両立できる

**`ON DELETE` 制約:**

```sql
-- 財務・監査テーブルの FK は ON DELETE NO ACTION（users レコードは削除しない設計）
coin_purchases.user_id       → users.id  ON DELETE NO ACTION
coin_transactions.user_id    → users.id  ON DELETE NO ACTION
admin_audit_logs.admin_id    → users.id  ON DELETE NO ACTION
```

---

### 強制無効化（アカウント削除・不正検知時）

`active_users.token_revision` をインクリメントする。API サーバーはリクエストごとに JWT の `rev` と DB の `token_revision` を照合し、不一致なら 401 を返す。ブラックリスト不要。

**アカウント削除時の無効化手順（同一トランザクション内で実行）:**

```sql
BEGIN;
UPDATE active_users
SET deleted_at = now(),
    token_revision = token_revision + 1,
    -- PII を即時 NULL 化（GDPR データ最小化: セクション13参照）
    display_name   = NULL,
    name           = NULL,
    email          = NULL,
    google_sub     = NULL,
    apple_sub      = NULL,
    x_sub          = NULL,
    -- vivox_id を再生成（旧 ID を Vivox 側の保持データから切り離す）
    vivox_id       = gen_random_uuid()
WHERE user_id = $1;
COMMIT;
```

`deleted_at` 設定・`token_revision` インクリメント・PII NULL 化を必ず同一トランザクションで行う。これにより:
- セッションが即時無効化される（削除申請後のログイン不可）
- 個人情報（表示名・@name・メールアドレス・ソーシャルプロバイダー紐付け）が即時消去される（GDPR「忘れられる権利」対応）
- `active_users` レコード自体は 30 日後のバッチ処理で物理削除される（その間、コイン購入履歴等が参照する `users.id` の整合性が保たれる）

認証ミドルウェアは rev 照合に加えて `deleted_at` の確認も行い（上記の処理順序を参照）、二重の防護とする。

**削除済みユーザーの公開 API 除外仕様:**

`active_users.deleted_at IS NOT NULL` のユーザーは認証済み API だけでなく、公開エンドポイントからも即時除外する。

| エンドポイント | 除外方法 |
|---|---|
| `GET /api/v1/users/{id}` | `deleted_at IS NOT NULL` の場合 404 を返す（アカウントの存在を開示しない） |
| `GET /api/v1/worlds` 等のワールド一覧 | `owner` フィールドのユーザー情報を返さない（ワールド自体は削除申請時に非公開化済みのため一覧に表示されない） |
| ルーム一覧のプレイヤー表示 | 削除済みユーザーのアバター・名前を表示しない（ゴーストとして非表示） |
| 違反報告・フレンド申請等の対象指定 | 削除済みユーザーへの操作は 404 を返す |

これにより、削除申請から 30 日後の `active_users` 物理削除を待たずに、削除申請の瞬間からユーザーが公開 API 上に存在しない状態になる。`is_deleted` フラグは設けず `deleted_at IS NOT NULL` を判定条件とする（同一の情報源で管理）。

---

## 5. ファイルストレージ・CDN

### バックエンド

| 環境 | ストレージ | CDN |
|---|---|---|
| 開発（Docker） | ローカルファイルシステム（Docker volume）| なし（API サーバーが直接配信） |
| 本番 | **Cloudflare R2**（S3 互換 API） | **Cloudflare CDN**（R2 に統合） |

開発・本番の切り替えは環境変数 `STORAGE_BACKEND=local|r2` で行う。コードは S3 互換 API 経由でアクセスするため実装変更なし。

### ファイル命名規則

コンテンツアドレス型（SHA-256 ハッシュ）で管理する。

```
{sha256_hex}.{ext}
例: abc123def456...789.vrm
```

### ディレクトリ構成

```
/avatars/{hash}.vrm          # 最適化済み VRM
/avatars/{hash}.png          # テクスチャ統合画像
/worlds/{hash}.glb           # ワールド GLB
/worlds/{hash}.json          # ワールド定義 JSON
/worlds/{hash}_terrain.png   # 地形アトラス
/worlds/{hash}_objects.png   # オブジェクトアトラス
/accessories/{hash}.glb      # アクセサリ GLB
/thumbnails/{hash}.png       # サムネイル（リサイズ済み）
/tmp/{upload_id}/            # アップロード処理中の一時領域
```

### キャッシュ設定

```
Content-Type: application/octet-stream (VRM/GLB) / image/png
Cache-Control: public, max-age=31536000, immutable   ← コンテンツアドレス型なので永久キャッシュ可
```

サムネイル・ワールド定義 JSON など更新される可能性のあるファイルは新しい SHA-256 → 新しい URL になるため、キャッシュ無効化処理は不要。

### ファイル削除ポリシー

#### テクスチャの非共有原則

**同一テクスチャファイルを複数のモデル・オブジェクトが参照することはない。** あるモデルから別のモデルへテクスチャをコピーする操作が発生した場合、そのコピー時点でファイルを物理複製し、コピー先は独立した新しいファイルを参照する。これにより「あるレコードのファイル = そのレコードだけが参照する」という 1:1 の対応が常に成立する。

#### 即時削除の原則

ファイルへの参照が DB レコードから外れた時点で、対応するストレージファイルを即時削除する。参照カウントや GC バッチは不要。

| 操作 | 削除されるファイル |
|---|---|
| アバター更新（新しいVRMをアップロード） | 旧 `/avatars/{hash}.vrm`・旧 `/avatars/{hash}.png`（テクスチャ統合画像） |
| アバター削除 | `/avatars/{hash}.vrm`・`/avatars/{hash}.png` |
| アクセサリ更新・削除 | 旧 `/accessories/{hash}.glb` |
| ワールド GLB 更新 | 旧 `/worlds/{hash}.glb` |
| ワールド定義 JSON 更新 | 旧 `/worlds/{hash}.json` |
| 地形・オブジェクトアトラス更新 | 旧 `/worlds/{hash}_terrain.png`・旧 `/worlds/{hash}_objects.png` |
| テクスチャレイヤー保存（上書き） | 旧レイヤー画像ファイル |
| サムネイル更新 | 旧 `/thumbnails/{hash}.png` |
| アカウント削除（30 日後バッチ） | 上記すべて（当該ユーザーが所有していたファイル全件） |

**削除手順（アプリケーション層）:**

```
1. DB トランザクション開始
2. 旧ファイルのパス（hash）を SELECT して取得
3. DB レコードを新しい hash で UPDATE（または DELETE）
4. トランザクション COMMIT
5. ストレージから旧ファイルを削除（非同期でよい）
```

ステップ 5 はトランザクション外で実行するため、削除に失敗しても DB 整合性は保たれる。

**ストレージ削除失敗時のリトライ:**

削除失敗時は即座に asynq の削除リトライキュー（`storage_delete` キュー）へ積む。

| 設定 | 値 |
|---|---|
| 自動リトライ | 最大 5 回（指数バックオフ: 1分・5分・15分・1時間・6時間） |
| リトライ上限到達後 | DLQ に移動 |
| DLQ 保持期間 | 30 日間 |

**管理画面への通知:**

同一ファイルへの削除が 3 回以上連続して失敗した場合（リトライ3回目時点）、管理画面のシステムアラート（`docs/screens-and-modes.md` セクション 8.9）に「ストレージ削除失敗」アラートを生成する。また `lowpolyworld-security-alerts` Pub/Sub へパブリッシュして Discord にも通知する（GDPR Art. 17 — 削除済みユーザーデータが残存しているため法的リスクあり）。管理者はアラート画面から孤立ファイルの一覧を確認し、手動削除を実行できる。

### 孤立ファイル管理エンドポイント

```
GET    /api/v1/admin/storage/orphaned-files          → 孤立ファイル一覧取得
DELETE /api/v1/admin/storage/orphaned-files/{file_id} → 指定ファイルの手動削除
```

**`GET /admin/storage/orphaned-files`**

DLQ（`storage_delete` キュー）に残留しているファイルの一覧を返す。

レスポンス:
```json
{
  "data": {
    "items": [
      {
        "file_id": "file_abc123",
        "path": "/avatars/abc123def456.vrm",
        "failed_attempts": 5,
        "last_failed_at": "2026-04-01T03:00:00Z",
        "enqueued_at": "2026-03-28T12:00:00Z"
      }
    ],
    "total": 3
  }
}
```

**`DELETE /admin/storage/orphaned-files/{file_id}`**

指定ファイルを Cloudflare R2 から物理削除し、DLQ から当該エントリを除去する。削除理由（`reason`）をリクエストボディに必須とする。

リクエスト:
```json
{ "reason": "ストレージ削除リトライ上限到達。手動で削除。" }
```

レスポンス: `204 No Content`

- 権限: `super_admin` のみ
- 削除操作は `admin_audit_logs` に記録する（下記参照）

**手動ファイル削除の `admin_audit_logs` 記録:**

| フィールド | 値 |
|---|---|
| `action` | `delete_storage_file` |
| `target_type` | `storage_file` |
| `target_id` | ファイルID（`file_id`） |
| `before_value` | `{"path": "/avatars/...", "failed_attempts": 5}` |
| `after_value` | `null`（削除済み） |
| `notes` | リクエストボディの `reason`（必須） |

---

## 5.5 アクセスログ

### 概要

Go API はすべての HTTP リクエストに対してアクセスログをミドルウェアで記録し、Cloud Logging に送信する。IP アドレスを含む個人情報の取り扱いについては `docs/api-abstract.md` セクション 13 のデータ保持方針に従う。

### ログエントリの構造

```json
{
  "severity": "INFO",
  "type": "access_log",
  "timestamp": "2026-04-07T12:34:56.789Z",
  "method": "GET",
  "path": "/api/v1/worlds",
  "status": 200,
  "latency_ms": 45,
  "ip": "203.0.113.1",
  "user_id": "usr_abc123",
  "user_agent": "LowPolyWorld/1.0 (iOS 17.4)"
}
```

- `user_id` は認証済みリクエストのみ付与。未認証エンドポイント（`GET /api/version`・`POST /auth/{provider}/callback`）は `null`
- `path` はクエリパラメータを除いたパスのみ記録（クエリパラメータに個人情報が含まれることを避けるため）
- `ip` はリバースプロキシ（Cloud Run）経由の場合 `X-Forwarded-For` ヘッダの最初の値を使用

### Cloud Logging への送信

- ロググループ名: `lowpolyworld-access`
- Cloud Logging の _Default バケットの保存期間を **1年** に設定する
- 取り込み量は最大スケール（MAU 100,000）でも月 ~9 GB 程度であり、無料枠（50 GiB/月）以内に収まる
- 保存料金: $0.01/GiB/月（31日目以降）。MAU 100,000 時点でも年間 ~$6 程度

### 利用目的

| 目的 | 説明 |
|---|---|
| セキュリティ監査・不正アクセス調査 | 侵害発生時の証跡 |
| ブリーチ時の影響国特定 | IP ジオロケーションで EU 居住ユーザーを特定し、GDPR Art. 33 の報告先 DPA を確定する（`docs/breach-notification-plan.md` 参照） |
| デバッグ・障害調査 | 本番環境での問題再現・原因特定 |
| 不正検知・レートリミット証跡 | IP ベースのレートリミット発動記録（セキュリティイベントログと併用） |

### 保存期間

1年後に自動削除（Cloud Logging の保存期間設定で制御）。1年を超える保持は行わない（GDPR データ最小化原則）。

### ログを記録しないもの

- リクエスト・レスポンスボディ（個人情報・決済情報が含まれる可能性）
- クエリパラメータ（上記と同様の理由）
- 内部ヘルスチェックエンドポイント（`GET /healthz` 等）

---

## 6. API 設計規約

### URL バージョニング

すべてのクライアント向けエンドポイントは `/api/v1/` プレフィックスを持つ。

```
GET  /api/version          ← バージョン互換性チェック（認証不要・バージョンプレフィックスなし）
POST /auth/{provider}/callback
GET  /startup

GET  /api/v1/me
GET  /api/v1/worlds
POST /api/v1/worlds/{id}/rooms/recommended-join
...

GET  /admin/...            ← 管理画面用（別認証・詳細は docs/screens-and-modes.md セクション8参照）
```

> **WebSocket について**: このプロジェクトの Go API は純粋 REST である。マルチプレイヤー同期は Netcode for GameObjects（Unity Transport / UDP）、音声は Vivox がそれぞれ独立して処理する。Go API サーバーに WebSocket エンドポイントは存在しない。

### レスポンスフォーマット

#### 成功（単一オブジェクト）

```json
{
  "data": {
    "id": "world_abc123",
    "name": "My World",
    "created_at": "2026-01-01T00:00:00Z"
  }
}
```

#### 成功（リスト・カーソルページネーション）

```json
{
  "data": [
    { "id": "world_abc", "name": "World A" },
    { "id": "world_def", "name": "World B" }
  ],
  "cursor": {
    "next": "world_def",
    "has_more": true
  }
}
```

#### エラー

```json
{
  "error": {
    "code": "validation_error",
    "message": "One or more fields are invalid",
    "details": [
      { "field": "name", "code": "too_long", "message": "must be 50 characters or less" }
    ]
  }
}
```

`details` はバリデーションエラー時のみ。その他のエラーでは省略。

#### 本番環境でのエラーメッセージ抑制ポリシー

本番環境（`APP_ENV=production`）では、5xx 系エラーのレスポンスボディに詳細なエラーメッセージを含めない。クライアントには汎用コードと `request_id` のみを返し、詳細は Cloud Logging のみに記録する。

| 環境 | 4xx レスポンス | 5xx レスポンス |
|---|---|---|
| 開発（`APP_ENV=development`） | 通常の `code` + `message` | `code` + `message`（スタックトレース含む） |
| 本番（`APP_ENV=production`） | 通常の `code` + `message` | `code: "internal_server_error"` + `request_id` のみ |

4xx（クライアントエラー）は開発・本番ともに通常のエラーコードとメッセージを返す（バリデーションエラー詳細は UI 表示に必要なため）。5xx（サーバー内部エラー）は本番環境ではスタックトレース・DB エラー詳細・内部パス等の情報漏洩を防ぐため汎用レスポンスのみ返す。

```json
// 本番環境での 5xx レスポンス例
{
  "error": {
    "code": "internal_server_error",
    "request_id": "req_abc123xyz"
  }
}
```

エラー調査は `request_id` をキーに Cloud Logging で詳細ログを参照する。

#### エラーコード一覧

| HTTP | code | 用途 |
|---|---|---|
| 400 | `validation_error` | 入力値不正 |
| 401 | `unauthorized` | 未認証・トークン期限切れ |
| 403 | `forbidden` | 権限なし |
| 403 | `user_restricted` | トラストレベルによる制限 |
| 403 | `account_deleted` | 削除申請済みアカウントによるアクセス |
| 404 | `not_found` | リソース不在 |
| 409 | `conflict` | 競合（`reason` フィールドで詳細を示す。下表参照） |
| 402 | `insufficient_coins` | コイン残高不足（購入不可） |
| 429 | `rate_limit_exceeded` | レート制限（`retry_after_seconds` フィールドを付与） |
| 500 | `internal_server_error` | サーバー内部エラー（`request_id` フィールドを付与） |

#### 409 conflict の reason 一覧

| reason | 発生箇所 | 意味 |
|---|---|---|
| `room_full` | ルーム参加 | 対象ルームの人数が上限に達している |
| `slot_limit_exceeded` | アクセサリ装備 | アバターのアクセサリ装備スロットが上限（4つ）に達している |
| `name_already_taken` | @name 設定・変更 | 指定した @name はすでに他のユーザーが使用している |
| `name_change_too_soon` | @name 変更 | 前回変更から 90 日が経過していない |
| `world_already_liked` | ワールドいいね | すでにいいね済みのワールドに再度いいねしようとした |
| `friend_request_exists` | フレンド申請 | 既に申請中または既フレンドのユーザーへの重複申請 |
| `follow_already_exists` | フォロー | すでにフォロー中のユーザーへの重複フォロー |
| `email_already_registered` | OAuth サインイン（新規） | （廃止: セキュリティ上の理由によりユーザー列挙対策のため 200 に変更。詳細: セクション14）|
| `upload_in_progress` | VRM アップロード | 同一ユーザーの VRM 最適化ジョブがすでに処理中 |
| `draft_version_conflict` | ワールドドラフト保存 | 別デバイスが先にドラフトを保存しており、送信した `draft_version` がサーバーと不一致 |

#### レスポンスヘッダ

```
Content-Type: application/json; charset=utf-8
X-Request-ID: <uuid>        ← 全レスポンスに付与（ログ追跡用）
```

### 規約

- 日時: ISO 8601 UTC（`2026-01-01T00:00:00Z`）
- ID: 文字列型（int64 は JSON の精度損失を避けるため文字列化）
- null フィールド: 省略せず `null` を明示
- スネークケース統一

### レート制限

カテゴリ別に以下の上限を適用する。超過時は 429 `rate_limit_exceeded`（`retry_after_seconds` 付き）を返す。制限はユーザー単位（JWT の `sub`）でカウントする。未認証エンドポイントは IP アドレス単位。

| カテゴリ | 対象エンドポイント例 | 上限 |
|---|---|---|
| 認証系 | `POST /auth/*` | **5 rpm** |
| アップロード系 | `POST /avatars/upload`, `POST /me/accessories/upload` | **3 rpm** |
| 書き込み系（重い） | `POST /worlds`, `PUT /worlds/{id}`, `POST /shop/products` | **10 rpm** |
| 書き込み系（軽い） | `POST /worlds/{id}/like`, `POST /users/{id}/follow`, `POST /me/friends` 等 | **30 rpm** |
| 読み取り系 | `GET /worlds`, `GET /users/{id}` 等 | **120 rpm** |
| 管理画面系 | `POST /admin/*`, `PUT /admin/*`, `DELETE /admin/*` | **30 rpm** |

- 上記はカテゴリ別の API サーバー側レート制限。アプリケーションビジネスロジックの制限（通報1日100件等）は別途各仕様に定義する
- 実装: Redis による Sliding Window カウンター

**Redis キー形式:**

| ユーザー種別 | キー形式 |
|---|---|
| 認証済みユーザー | `rate_limit:v1:{user_id}:{category}` |
| 未認証（IP 制限） | `rate_limit:v1:ip:{ip_address}:{category}` |

例: `rate_limit:v1:usr_abc123:write_heavy`、`rate_limit:v1:ip:203.0.113.1:auth`

**分散環境（Cloud Run 複数インスタンス）での atomicity:**

Cloud Run の複数インスタンスが同一 Redis に並行アクセスしても二重カウントが発生しないよう、カウント取得・判定・インクリメントを Lua スクリプトで単一アトミック操作として実行する（`EVALSHA`）。

```lua
local key    = KEYS[1]           -- rate_limit:v1:{identifier}:{category}
local window = tonumber(ARGV[1]) -- ウィンドウ幅（秒）
local limit  = tonumber(ARGV[2]) -- 上限リクエスト数
local now    = tonumber(ARGV[3]) -- 現在時刻（unix ミリ秒）
redis.call('ZREMRANGEBYSCORE', key, 0, now - window * 1000)
local count = redis.call('ZCARD', key)
if count < limit then
    redis.call('ZADD', key, now, now)
    redis.call('EXPIRE', key, window)
    return 1  -- 許可
end
return 0  -- 拒否（429 を返す）
```

### 文字列入力の正規化ポリシー

サーバーはすべての文字列入力に対して以下の順で正規化を適用してからバリデーションを行う。

**NFKC 正規化（全フィールド共通）**

Go: `golang.org/x/text/unicode/norm` パッケージの `norm.NFKC.String(input)` を適用する。全角英数字・互換文字・合字などを正規形に変換し、続くバリデーションで一貫した結果を得る。

| フィールド | 正規化後の処理 |
|---|---|
| `@name` | NFKC → 小文字化 → ASCII 英数字・アンダースコアのみ検証（詳細: `PUT /me/name` 手順） |
| 表示名 | NFKC → trim → 文字数チェック（1〜30文字） |
| ワールド名・商品名 | NFKC → trim → 文字数チェック（各フィールドの上限に従う） |
| タグ | NFKC → 小文字化 → trim → 文字数チェック |

**Confusable Characters フラグ（表示名のみ）**

表示名の保存時（新規作成・変更）に Unicode Confusable Characters（視覚的に紛らわしい文字、例: キリル `о` U+043E ↔ ラテン `o` U+006F）の存在を検出し、`active_users.has_confusable_chars` を更新する。

- **登録・変更は拒否しない**（法的問題なし・正当な多言語ユーザーへの配慮）
- 通報を受けた際、管理画面のユーザー詳細に ⚠️ インジケーターを表示して成りすまし確認を容易にする

`active_users` 追加カラム:

| カラム | 型 | 説明 |
|---|---|---|
| `has_confusable_chars` | `BOOLEAN NOT NULL DEFAULT false` | 表示名に Confusable Characters が含まれる場合 true。表示名変更のたびに再計算 |

---

## 6b. 公認バッジ

### DB スキーマ

`active_users` 追加カラム:

| カラム | 型 | 説明 |
|---|---|---|
| `is_verified` | `BOOLEAN NOT NULL DEFAULT false` | 公認バッジが付与されている場合 true。管理画面からのみ変更可能 |

### 管理 API エンドポイント

```
PATCH /admin/users/{id}/verified
  認証: 管理者（admin / super_admin ロール必須。moderator 不可）
  body: { "is_verified": true }  // または false（剥奪）
  → 204 No Content
  → 403 forbidden_role（moderator でアクセスした場合）
  → 404 user_not_found
```

操作は `admin_audit_logs` に記録される（セクション10）。

### ユーザー API レスポンスへの追加

`is_verified` フィールドをユーザー情報を返すすべてのエンドポイントのレスポンスに含める:

- `GET /api/v1/users/{id}`
- `GET /api/v1/me`
- ルームメンバー一覧など、`display_name` を含むすべてのユーザーオブジェクト

```json
{
  "id": "...",
  "display_name": "...",
  "name": "...",
  "is_verified": true,
  ...
}
```

---

## 7. APIバージョニング 後方互換ポリシー

### マイナーアップデート（v1.x）— 後方互換を維持

- レスポンスへのフィールド**追加**は許容（クライアントは未知フィールドを無視する実装にする）
- 既存フィールドの**削除・型変更・意味変更**は禁止
- 新しいエンドポイントの追加は許容

### メジャーアップデート（v2）— 破壊的変更

- v2 エンドポイント（`/api/v2/`）を追加した時点で v1 の廃止予告を開始
- v1 サポート期間: v2 リリースから **12ヶ月**
- 廃止予告ヘッダー（廃止対象エンドポイントのレスポンスに付与）:
  ```
  Deprecation: true
  Sunset: Sat, 01 Jan 2028 00:00:00 GMT
  ```
- Unity クライアントのアップデート誘導 UI と合わせて段階移行する

### エンドポイント命名規則

- リソース名: 複数形スネークケース（`worlds`, `room_members`）
- アクション（非 CRUD）: `POST /{resource}/{id}/{verb}` 形式
  - 例: `POST /worlds/{id}/like`, `POST /worlds/{id}/rooms/recommended-join`
- 検索・フィルタ: クエリパラメータ（`?after=<cursor>&limit=20&sort=newest`）

## 8. VRM Optimizer（非同期ジョブ）

### ジョブキューの実装

**ライブラリ**: [`github.com/hibiken/asynq`](https://github.com/hibiken/asynq)（Redis ベースの Go 向けジョブキューライブラリ）

- Redis は既存インフラを共有する
- リトライ・DLQ・タイムアウトを設定で制御できる
- Asynq UI（Web ダッシュボード）でジョブ状態・DLQ を可視化できる（開発・運用監視用）

### 処理フロー

```
1. Unity → POST /api/v1/avatars/upload (multipart/form-data: vrm_file)
2. API   → 同一ユーザーの処理中ジョブ（pending / processing）が存在するか確認
           → 存在する場合: 409 conflict（reason: "upload_in_progress"）を返して終了
           → 存在しない場合: 続行
         → ファイルを /tmp/{upload_id}/ に保存
         → asynq に job をエンキュー { job_id, upload_id, user_id }
         → 202 Accepted: { "job_id": "job_abc123" }

3. Optimizer worker（asynq worker）→ キューからジョブを取得・処理:
                    a. バリデーション（サイズ / ポリゴン / ボーン数 / メッシュ階層深度 / ZIP構造）
                    b. 不要データ削除（アニメーション制約等）
                    c. テクスチャ圧縮
                    d. 前面・背面スクリーンショット生成（審査用）
                    e. [モデレーション] テクスチャ画像を CSAM ハッシュ照合
                       → ヒット: avatars.moderation_status = 'rejected' + admin_alerts に記録 → ジョブ failed で終了
                    f. [モデレーション] ContentModerationService.CheckImage（NSFW 自動検知）
                       → NullModerationService（ローンチ時）。将来 Rekognition 等に差し替え
                       → 高スコア: avatars.moderation_status = 'pending'（trust_level に関わらず）
                    g. 最適化済み VRM を /avatars/{hash}.vrm に保存
                    h. [モデレーション] avatars.moderation_status を確定
                       → uploader の trust_level が visitor / new_user → 'pending'（検疫）
                       → uploader の trust_level が user / trusted_user → 'approved'（即時公開）
                    → DB の job ステータスを更新（completed / failed）
                    → tmp ファイルを削除

4. Unity → GET /api/v1/jobs/{job_id} をポーリング（1〜3 秒間隔）
         → status: pending | processing | completed | failed
         → completed 時: { "avatar_url": "https://cdn.../abc123.vrm", "metadata": {...} }
```

### ジョブタイムアウト・リトライ・DLQ

| 設定 | 値 |
|---|---|
| 1ジョブの処理タイムアウト | 60 秒 |
| 自動リトライ | 最大 3 回（指数バックオフ） |
| リトライ上限到達後 | DLQ（Dead Letter Queue）に移動 |
| DLQ の保持期間 | 7 日間（asynq の `Retention` で設定） |
| ポーリング上限 | 120 秒（超過時はクライアント側でエラー表示） |

DLQ に移動したジョブは Asynq UI から再実行・削除が可能。

### 連続アップロード（同一ユーザーが処理中に再アップロード）

既存ジョブが `pending` または `processing` の状態でアップロードリクエストが来た場合:

```json
HTTP 409 Conflict
{
  "error": {
    "code": "conflict",
    "message": "An upload is already in progress. Please wait for it to complete.",
    "reason": "upload_in_progress"
  }
}
```

クライアントはアップロード画面に「処理中です。完了後に再試行してください。」を表示し、進行中ジョブのポーリングへ誘導する。

### コンテンツモデレーション（VRM アバター）

#### `avatars` テーブル 追加カラム

| カラム | 型 | 説明 |
|---|---|---|
| `moderation_status` | `VARCHAR(10) NOT NULL DEFAULT 'pending'` | `'pending'`（検疫中）/ `'approved'`（公開済み）/ `'rejected'`（BAN） |

#### visibility ルール

| moderation_status | アップロード本人 | ルーム内他プレイヤー | 他ユーザーのプロフィール閲覧 | 管理画面 |
|---|---|---|---|---|
| `pending` | 装備・使用可。アバター一覧に「審査中」バッジ表示 | 見えない（フォールバック表示） | 見えない | 審査キューに表示 |
| `approved` | 通常 | 通常 | 通常 | 通常 |
| `rejected` | 使用不可。アバター一覧に「利用不可」バッジ表示 | 見えない | 見えない | 通常 |

**ルーム内フォールバック**: 他プレイヤーが `moderation_status != 'approved'` のアバター ID を受信した場合、VRM の取得を試みず、デフォルトアバター（システム既定の最軽量 VRM）を表示する。

#### API 挙動

- `GET /api/v1/me/avatars` → 本人の全アバターを返す（`moderation_status` フィールドを含む）
- `GET /api/v1/users/{id}/avatars` → `moderation_status = 'approved'` のみ返す
- `GET /api/v1/avatars/{id}` → `moderation_status != 'approved'` かつリクエスト者が本人以外の場合は 404

#### CSAM 対応

| 項目 | 内容 |
|---|---|
| 検知手段 | VRM テクスチャを抽出し CSAM ハッシュデータベースと照合（Google CSAI Match） |
| ヒット時の処理 | `moderation_status = 'rejected'` に即時設定。`admin_alerts` テーブルに記録。`lowpolyworld-security-alerts` Pub/Sub へパブリッシュして Discord に通知（IHC 法的通報義務 — 時間的制約あり）。管理者に通知 |
| 通報義務 | 管理者が確認後、IHC（インターネットホットラインセンター: https://ihc.or.jp）へ通報。対応記録を `admin_audit_logs` に残す |
| ローンチ時の実装 | Google CSAI Match の API 申請・統合が完了するまでは Optimizer 側で no-op ハンドラーを挿入しておく（`ContentModerationService` と同様の差し替え可能インターフェース） |

#### `admin_alerts` テーブル

```sql
CREATE TABLE admin_alerts (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    alert_type  VARCHAR(30) NOT NULL,  -- 'csam_detected' など
    target_type VARCHAR(20) NOT NULL,  -- 'avatar' / 'accessory'
    target_id   UUID NOT NULL,
    user_id     UUID NOT NULL REFERENCES users(id) ON DELETE NO ACTION,
    detail      JSONB,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    resolved_at TIMESTAMPTZ
);
```

### プッシュ通知トークンの無効化処理

プッシュ通知送信時（APNs / FCM）にプロバイダーから invalid token レスポンスを受信した場合、**同期的に** `push_tokens` テーブルの該当トークンをソフトデリートする（`deleted_at = now()`）。

| プロバイダー | エラーコード | 対応 |
|---|---|---|
| APNs | `InvalidDeviceToken` / `Unregistered` | 該当トークンを即時ソフトデリート |
| FCM | `UNREGISTERED` | 該当トークンを即時ソフトデリート |

- ソフトデリートは送信処理の中で行う（asynq ジョブ内で完結）
- 対象が存在しない・すでにソフトデリート済みの場合は無視（冪等）
- ソフトデリート後は他のトークン（同一ユーザーの別デバイス）へのリトライは行わない

**ソフトデリートの目的と復旧手段:**

APNs / FCM の誤判定やバグによる大量削除が発生した場合に `deleted_at = NULL` に戻すことで復元できる。`deleted_at IS NOT NULL` のトークンはプッシュ送信対象から除外するが、30 日間はレコードを保持する。

`push_tokens` 追加カラム:

| カラム | 型 | 説明 |
|---|---|---|
| `deleted_at` | `TIMESTAMPTZ` | NULL = 有効。NULL 以外 = 削除済み（無効トークン） |

**30 日後の物理削除バッチ（毎日 JST 04:00）:**
```sql
DELETE FROM push_tokens
WHERE deleted_at IS NOT NULL
  AND deleted_at < now() - interval '30 days';
```

### バリデーションエラー時

最適化に進まずジョブを `failed` にし（リトライなし）、エラー内容を返す。Unity 側でアップロード画面にエラーメッセージを表示する。

```json
{
  "status": "failed",
  "error": {
    "code": "validation_error",
    "details": [
      { "field": "polygon_count", "code": "too_many", "message": "must be 512 or less (got 1024)" }
    ]
  }
}
```

`field` と `code` の組み合わせはクライアント側マッピングの安定した契約とする。`message` は人間可読な英語の補足情報であり、UI 表示には使用しない。Unity クライアントは `field`/`code` をキーにしてローカライズ済みメッセージを表示する（マッピング仕様: `docs/unity-game-abstract.md` セクション 8 参照）。

**バリデーション field / code 一覧:**

| field | code | 意味 |
|---|---|---|
| `file_size` | `too_large` | ファイルサイズが 500KB を超えている |
| `polygon_count` | `too_many` | ポリゴン数（トライアングル数）が 512 を超えている |
| `bone_count` | `too_many` | ボーン数が 50 を超えている |
| `rig_type` | `not_humanoid` | Humanoid リグではない |
| `vrm_version` | `unsupported` | VRM 1.0 形式ではない |
| `zip_structure` | `disallowed_file` | ZIP 内に許可されていないファイル形式が含まれている |
| `zip_structure` | `zip_bomb` | 圧縮比（展開後/圧縮後 > 100）または展開後合計サイズ（> 2 MB）が上限を超えている |
| `zip_structure` | `path_traversal` | ZIP エントリのパスに `..` またはルート絶対パスが含まれている |
| `texture_format` | `disallowed` | テクスチャのマジックバイトが拡張子と不一致（偽装ファイル） |
| `mesh_depth` | `too_deep` | glTF ノードの階層深度が上限（初期値 8）を超えている |

**ZIP 構造検証（セキュリティ対策）:**

VRM は ZIP 形式のコンテナであるため、以下の検証をすべて通過しない場合はバリデーションエラーとする。

**1. ファイル拡張子ホワイトリスト**

以下の許可リストに含まれない拡張子を持つファイルが ZIP 内に存在する場合、拒否する（`zip_structure` / `disallowed_file`）。

許可される内部ファイル拡張子: `.gltf`, `.glb`, `.bin`, `.png`, `.jpg`, `.jpeg`

**2. ZIP 爆弾対策**

- 各エントリの `uncompressed_size / compressed_size` が 100 を超える場合は拒否する
- 全エントリの展開後合計サイズが 2 MB を超える場合は拒否する

エラーコード: `zip_structure` / `zip_bomb`

> 上限値（圧縮比 100・展開後 2 MB）は将来の VRM 仕様変更に応じて段階的に緩和できる。変更時はこのセクションと上記 field/code 一覧の説明を合わせて更新すること。

**3. ZIP Slip 対策**

各エントリのパスに以下が含まれる場合は拒否する（`zip_structure` / `path_traversal`）:

- `..`（ディレクトリトラバーサル）
- `/` で始まる絶対パス

**4. テクスチャ フォーマット検証（マジックバイト）**

拡張子が `.png` / `.jpg` / `.jpeg` のエントリは、拡張子に加えてファイルヘッダー（マジックバイト）も検証する。拡張子とヘッダーが一致しない場合は拒否する（`texture_format` / `disallowed`）。

| 拡張子 | 期待するマジックバイト（先頭バイト列） |
|---|---|
| `.png` | `89 50 4E 47 0D 0A 1A 0A` |
| `.jpg` / `.jpeg` | `FF D8 FF` |

**5. GLB メッシュ階層深度**

glTF ノードツリーの最大深度が上限を超える場合は拒否する（`mesh_depth` / `too_deep`）。

| 設定 | 値 |
|---|---|
| 初期上限 | 8 |
| 変更方針 | 将来の仕様変更に応じて段階的に拡大可。変更時はこのセクションを更新 |

**要確認フラグ（`avatars.needs_review`）:**

バリデーションを通過した場合でも、以下の条件に該当するアバターには `needs_review = true` フラグを立てて管理画面（セクション 8.2）の審査キューに追加する:

| 条件 | 理由 |
|---|---|
| アップロードしたユーザーの `trust_level` が `visitor` または `new_user` | 新規ユーザーによる不審なアップロードを優先確認 |
| そのユーザーに未処理の違反報告が 1 件以上ある | 通報済みユーザーのコンテンツを優先確認 |
| ZIP 内のテクスチャファイル総容量が 300KB を超える | 技術制限内でも過大なテクスチャは目視確認 |

---

## 9. アクセサリ GLB Optimizer（非同期ジョブ）

VRM Optimizer（セクション 8）と同じ asynq ジョブキューを使用する。アクセサリ GLB はテクスチャ圧縮は不要だが、審査用スクリーンショットの生成が必要なため非同期処理とする。

### 処理フロー

```
1. Unity → POST /api/v1/me/accessories/upload (multipart/form-data: glb_file)
2. API   → 同一ユーザーの処理中アクセサリジョブ（pending / processing）が存在するか確認
           → 存在する場合: 409 Conflict（reason: "upload_in_progress"）を返して終了
           → 存在しない場合: 続行
         → ファイルを /tmp/{upload_id}/ に保存
         → asynq に job をエンキュー { job_id, upload_id, user_id }
         → 202 Accepted: { "job_id": "job_abc123" }

3. Optimizer worker → キューからジョブを取得・処理:
                    a. バリデーション（ファイルサイズ / ポリゴン数 / テクスチャサイズ / GLB 形式 / メッシュ階層深度）
                    b. マジックバイト検証（テクスチャ偽装ファイル検出）
                    c. スクリーンショット生成（前面・背面、審査用）
                    d. [モデレーション] テクスチャ画像を CSAM ハッシュ照合（VRM と同様）
                       → ヒット: accessories.moderation_status = 'rejected' + admin_alerts に記録 → ジョブ failed で終了
                    e. [モデレーション] accessories.moderation_status を確定
                       → uploader の trust_level が visitor / new_user → 'pending'
                       → uploader の trust_level が user / trusted_user → 'approved'
                    f. GLB を /accessories/{hash}.glb に保存
                    → DB の job ステータスを更新（completed / failed）
                    → tmp ファイルを削除

4. Unity → GET /api/v1/jobs/{job_id} をポーリング（1〜3 秒間隔）
         → status: pending | processing | completed | failed
         → completed 時: { "accessory_url": "https://cdn.../abc123.glb", "metadata": {...} }
```

### ジョブタイムアウト・リトライ・DLQ

| 設定 | 値 |
|---|---|
| 1ジョブの処理タイムアウト | 30 秒 |
| 自動リトライ | 最大 3 回（指数バックオフ） |
| リトライ上限到達後 | DLQ（Dead Letter Queue）に移動 |
| DLQ の保持期間 | 7 日間 |
| ポーリング上限 | 60 秒（超過時はクライアント側でエラー表示） |

### バリデーション field / code 一覧

| field | code | 意味 |
|---|---|---|
| `file_size` | `too_large` | ファイルサイズが 100KB を超えている |
| `polygon_count` | `too_many` | ポリゴン数（トライアングル数）が 128 を超えている |
| `texture_size` | `too_large` | テクスチャサイズが 64×64 を超えている |
| `texture_format` | `disallowed` | テクスチャのマジックバイトが拡張子と不一致（偽装ファイル） |
| `mesh_depth` | `too_deep` | glTF ノードの階層深度が上限（8）を超えている |
| `glb_format` | `invalid` | GLB ファイルのヘッダーまたは構造が不正 |

バリデーションエラー時はリトライなしでジョブを `failed` にし、エラー内容を返す。Unity 側のローカライズマッピング仕様: `docs/unity-game-abstract.md` セクション 4.5.5 参照。

**コンテンツモデレーション（アクセサリ）:**

VRM アバターと同一の `moderation_status` カラム（`'pending'` / `'approved'` / `'rejected'`）を `accessories` テーブルに追加する。visibility ルール・API 挙動・CSAM 対応は VRM（セクション 8）と同様。

旧 `needs_review` フラグは廃止し `moderation_status = 'pending'` に統一する。管理画面審査キューへの追加条件（trust_level が visitor / new_user、または未処理違反報告あり）は `moderation_status = 'pending'` の判定ロジックに組み込む。

---

## 10. サーバーサイドキャッシュ戦略（Redis）

### 基本方針

**短い TTL + 書き込み時に明示的なキャッシュ削除**を組み合わせる。更新操作が発生した時点でキャッシュを削除し、次のリクエストで DB から再取得してキャッシュを再生成する（Write-Around パターン）。

ユーザー固有データ（`GET /startup`・`GET /me` 等）はユーザーごとにキャッシュキーが異なり無効化が複雑になるためキャッシュ対象から除外する。

### キャッシュ対象・TTL・無効化トリガー一覧

| エンドポイント | キャッシュキー | TTL | 無効化トリガー |
|---|---|---|---|
| `GET /worlds/new` | `worlds:new:{cursor}` | 2分 | ワールド公開・非公開・削除 |
| `GET /worlds/following` | `worlds:following:{user_id}:{cursor}` | 1分 | フォロー中ユーザーのワールド公開・非公開 |
| `GET /worlds/liked` | `worlds:liked:{user_id}:{cursor}` | 1分 | 自分のいいね操作 |
| `GET /worlds/{id}` | `world:{id}` | 5分 | ワールド情報更新・いいね数変動 |
| `GET /api/v1/shop/products` | `shop:products:{category}:{sort}:{cursor}` | 10分 | 商品登録・更新・削除（管理画面操作） |
| `GET /api/v1/shop/products/{id}` | `shop:product:{id}` | 10分 | 商品更新（管理画面操作） |
| `GET /rooms` (ルーム一覧) | `rooms:{world_id}` | 30秒 | ルーム作成・参加・退室 |

### 無効化の実装方針

```
// 例: ワールド公開操作時
func PublishWorld(worldID string) {
    db.UpdateWorld(worldID, published=true)       // DB 更新
    redis.Del("world:" + worldID)                 // 単一ワールドキャッシュ削除
    redis.Del("worlds:new:*")                     // 新着一覧キャッシュ削除（パターン削除）
    redis.Del("worlds:following:*:" + worldID)    // フォロー中一覧（関連するもの）削除
}
```

パターン削除（`DEL worlds:new:*`）は Redis の `SCAN` + `DEL` で実装する（`KEYS` コマンドはブロッキングのため使用しない）。

### キャッシュなし対象

以下は**キャッシュしない**：

- `GET /startup`・`GET /me`（ユーザー固有・更新頻度が高い）
- `GET /api/v1/jobs/{id}`（リアルタイム性が必要）
- 認証・書き込み系エンドポイント（全て）
- 管理画面エンドポイント（`/admin/*`・件数が少なく鮮度が重要）

---

## 10. 管理者操作監査ログ

### admin_audit_logs テーブル

すべての管理者操作を追記型で記録する。削除・更新は禁止（コイン取引と同様）。

| カラム | 型 | 説明 |
|---|---|---|
| id | BIGSERIAL PK | |
| admin_id | TEXT NOT NULL | 操作した管理者の ID |
| action | VARCHAR NOT NULL | 操作種別（下表参照） |
| target_type | VARCHAR NOT NULL | 操作対象のリソース種別（`user` / `world` / `shop_item` / `avatar` / `room` 等） |
| target_id | TEXT NOT NULL | 操作対象のリソース ID |
| before_value | JSONB | 変更前の状態（省略可） |
| after_value | JSONB | 変更後の状態（省略可） |
| notes | TEXT | 任意メモ（理由・備考） |
| created_at | TIMESTAMPTZ NOT NULL DEFAULT now() | |

`admin_id` は `users.id` を参照するが、管理者アカウントの削除後も記録を保持するため、ON DELETE NO ACTION とする。

### action 種別一覧

| action | 意味 |
|---|---|
| `ban_user` | ユーザー BAN |
| `unban_user` | BAN 解除 |
| `warn_user` | 警告発行 |
| `restrict_user` | 制限設定（手動） |
| `unrestrict_user` | 制限解除 |
| `change_trust_level` | トラストレベル手動変更 |
| `lock_trust_level` | トラストレベルロック |
| `unlock_trust_level` | トラストレベルロック解除（`super_admin` のみ実行可） |
| `grant_title` | 称号付与 |
| `revoke_title` | 称号剥奪 |
| `disable_world` | ワールド無効化 |
| `enable_world` | ワールド有効化 |
| `approve_avatar` | アバター審査承認 |
| `reject_avatar` | アバター審査拒否 |
| `cancel_coin_purchase` | コイン購入手動キャンセル（`coins.md` セクション17.7と連動） |
| `register_settled_revenue` | 確定売上登録（`coins.md` セクション9と連動） |
| `modify_adjustment_factor` | 補正係数手動上書き（`super_admin` のみ・`notes` に理由必須・`before_value` / `after_value` に変更前後の係数値を記録） |
| `delete_storage_file` | 孤立ファイルの手動削除（`super_admin` のみ・`notes` に削除理由必須） |
| `delete_world` | ユーザーワールドの強制削除 |
| `delete_shop_item` | ショップ商品の削除 |

### admin_audit_logs テーブル（追加カラム）

セキュリティ監査のため、失敗リクエストを含む全操作を記録する。

| カラム | 型 | 説明 |
|---|---|---|
| response_status | SMALLINT NOT NULL | HTTP レスポンスステータスコード（200・403・404 等） |
| error_code | VARCHAR | 失敗時のエラーコード（成功時は NULL） |

### ミドルウェア実装方針

管理者向けエンドポイント（`/admin/*`）に監査ログミドルウェアを適用し、**成功・失敗を問わず全リクエストを** `admin_audit_logs` へ記録する。4xx / 5xx のレスポンスも記録対象とする。

**改ざん防止:**
- `admin_audit_logs` テーブルに対して API サーバーの DB ユーザーは INSERT のみ許可。DELETE・UPDATE 権限を付与しない
- Cloud Logging へリアルタイム転送し、DB 外部にコピーを保持する（`docs/infra-abstract.md` セクション9参照）

---

## 11. コイン消費トランザクション

### 購買前チェック

コイン消費エンドポイント（ショップ商品購入等）は処理前に以下の順で検証する:

```
1. coin_balances.balance >= 0
   → 満たさない場合: 403 insufficient_coins（マイナス残高保護）
2. coin_balances.balance >= 必要コイン数
   → 満たさない場合: 402 insufficient_coins
```

マイナス残高は本来発生しないが、万一発生した場合でも購買を通過させないための多重防護として 1 を設ける。

### トランザクション分離レベル

コイン消費の一連の処理（冪等性チェック → 残高ロック → `coin_transactions` INSERT → `coin_balances` UPDATE）は、単一トランザクション内で以下の分離レベルで実行する。

```sql
BEGIN ISOLATION LEVEL SERIALIZABLE;
-- 冪等性チェック → 残高ロック → INSERT → UPDATE
COMMIT;
```

**分離レベル: `SERIALIZABLE`**

write skew・phantom read などの直列化異常を DB レベルで防止する。PostgreSQL がシリアライゼーション失敗（エラーコード `40001`）を返した場合、アプリケーション層で最大 3 回・指数バックオフ（100ms → 200ms → 400ms）でリトライする。3 回失敗した場合は 503 `service_unavailable` を返す。

### 残高ロックとアトミック消費

**Step 1: `SELECT ... FOR UPDATE` で行ロック**

冪等性チェック通過後、残高チェック前に `coin_balances` 行を排他ロックする。

```sql
SELECT balance FROM coin_balances WHERE user_id = $1 FOR UPDATE;
```

同一ユーザーへの並行コイン消費リクエストがここでシリアライズされる。ロック取得まで他のトランザクションは待機する。

**Step 2: 購買前チェック**

ロック取得後、残高を検証する（購買前チェックと同内容）。

**Step 3: 残高チェックと消費のアトミック UPDATE**

条件付き UPDATE 1 クエリで残高チェックと消費を同時に実行する。`SELECT → チェック → UPDATE` の 2 ステップではなく 1 クエリで完結させることで、ロック解放後の残高変動にも対応する最終防護とする。

```sql
UPDATE coin_balances
SET balance = balance - $1,
    updated_at = now()
WHERE user_id = $2
  AND balance >= $1
  AND balance >= 0
RETURNING balance;
```

- 更新行数 = 1: 消費成功。`RETURNING balance` で消費後残高を取得
- 更新行数 = 0: 残高不足またはマイナス残高 → **HTTP 402** `insufficient_coins` を返す

> `SERIALIZABLE`（分離レベル）・`SELECT FOR UPDATE`（行ロック）・条件付き UPDATE（アトミック更新）の三重防護で多重消費を防ぐ。

### 冪等性（Idempotency Key）

コイン消費エンドポイントはネットワーク障害等によるリトライ時の二重引き落としを防ぐため、冪等性キーをサポートする。

**クライアント実装:**

```
POST /api/v1/shop/purchase
Idempotency-Key: <UUID v4>   ← クライアントがリクエストごとに新規生成
```

- ネットワークエラーでリトライする場合は同一キーをそのまま再送する
- キーの省略も許容する（後方互換性のため）。省略時は冪等性は保証されない

**サーバー実装:**

`coin_transactions.idempotency_key` カラム（`TEXT UNIQUE NULL`）で管理する。

```
1. Idempotency-Key ヘッダーが存在する場合:
   a. coin_transactions に同一 idempotency_key が存在するか確認
      ├── 存在する → 元のトランザクションを SELECT して同一レスポンスを 200 で返す（処理スキップ）
      └── 存在しない → 通常の消費処理を実行し idempotency_key を記録
   b. 同一キーで異なる商品・金額のリクエストが来た場合: 422 idempotency_key_mismatch を返す
2. ヘッダーが存在しない場合: 通常の消費処理を実行（idempotency_key = NULL）
```

- PostgreSQL の UNIQUE 制約は NULL を複数許容するため、キー省略リクエストは複数共存できる
- 同時に同一キーで 2 件のリクエストが届いた場合、後発の INSERT が UNIQUE 違反となり安全に弾かれる
- `idempotency_key` は `coin_transactions` レコードとともに永続保存される（金融監査証跡として有用）

### GET /me のコイン残高

`GET /api/v1/me` のレスポンスには以下を含める:

```json
{
  "coin_balance": 1500
}
```

クライアントはこの値を用いてショップ画面で購買可否の UI 制御を行う（残高不足商品のグレーアウト等）。

### GET /me/data-export（GDPR Art. 15 アクセス権・Art. 20 データ移植権）

ユーザーが自身の個人データすべてを機械可読形式（JSON）で取得するエンドポイント。GDPR Art. 15（アクセス権）および Art. 20（データ移植権）への準拠を目的とする。設定画面（`screens-and-modes.md` §19.6）から呼び出す。

```
GET /api/v1/me/data-export
  認証: 必須
  レート制限: 1 回 / 時（超過時 429）
  → 200 OK  Content-Type: application/json
```

**レスポンス構造:**

```json
{
  "data": {
    "exported_at": "2026-04-09T12:00:00Z",
    "account": {
      "id": "<user_id>",
      "display_name": "...",
      "name": "...",
      "age_group": "adult",
      "locale": "ja-JP",
      "subscription_tier": "free",
      "created_at": "...",
      "parental_consent_verified_at": null
    },
    "coin_balance": 1500,
    "coin_purchases": [
      {
        "id": "...",
        "coin_amount": 500,
        "price_jpy": 610,
        "purchased_at": "..."
      }
    ],
    "coin_transactions": [
      {
        "id": "...",
        "amount": -100,
        "description": "アバター購入",
        "transacted_at": "..."
      }
    ],
    "follows": ["<user_id>", "..."],
    "friends": ["<user_id>", "..."],
    "hidden_users": ["<user_id>", "..."],
    "hidden_worlds": ["<world_id>", "..."],
    "avatars": [
      {
        "id": "...",
        "name": "...",
        "vrm_url": "https://cdn.example.com/avatars/{hash}.vrm",
        "thumbnail_url": "https://cdn.example.com/thumbnails/{hash}.png",
        "created_at": "..."
      }
    ],
    "worlds": [
      {
        "id": "...",
        "name": "...",
        "glb_url": "https://cdn.example.com/worlds/{hash}.glb",
        "thumbnail_url": "https://cdn.example.com/thumbnails/{hash}.png",
        "created_at": "..."
      }
    ]
  }
}
```

**設計上の注意:**

- `follows` / `friends` / `hidden_users` はユーザー ID のみ返す（他ユーザーの個人情報は含めない）
- アバター・ワールドのファイル本体はコンテンツアドレス URL（CDN）で参照する。URL は永続的（immutable）なため、ユーザーは URL を使って各ファイルを個別にダウンロードできる（Art. 20「移植可能な形式」の要件を満たす）
- `followers`（フォロワー一覧）は他ユーザーのデータのため含めない
- 受信した違反報告・管理者操作ログも含めない（内部運用データ）
- GDPR Art. 15 が求める処理メタ情報（目的・保持期間・第三者提供先等）はプライバシーポリシーの URL をレスポンスには含めず、設定画面の UI 上にプライバシーポリシーへのリンクを別途設置することで対応する
- 対応期間（30 日以内）と請求窓口（nibankougen@gmail.com）をプライバシーポリシーに明記する

### 削除権限

ワールドおよびショップ商品の削除権限は以下の通り:

| 操作 | 権限 |
|---|---|
| ユーザーによる自分のワールド削除 | オーナー本人のみ |
| ユーザーによる自分のショップ商品削除 | オーナー本人のみ |
| 管理者によるワールド強制削除 | `super_admin` / `admin` / `moderator` |
| 管理者によるショップ商品削除 | `super_admin` / `admin` |

すべての管理者削除操作は `admin_audit_logs` に記録される（`delete_world` / `delete_shop_item`）。

---

## 13. データ保持方針

アカウント削除・退会後のデータ保持期間と削除タイミングを定める。

| データ種別 | 削除タイミング | 保持理由 |
|---|---|---|
| 表示名・@name・OAuthプロバイダー紐付け・メールアドレス | **削除申請時即時 NULL 化**（GDPR データ最小化） | — |
| アバター・ワールド・テクスチャファイル（R2） | 退会から 30 日後（物理削除） | — |
| トラストポイント・トラストレベル | 退会から 30 日後（`active_users` 削除と同時） | — |
| フレンド・フォロー関係 | 退会時即時削除 | 関係性は個人情報 |
| コイン購入履歴・消費履歴・取引キャンセル記録 | **7 年保持** | 税務・会計上の法定保存義務（日本: 法人税法 7 年。GDPR は会計・税務目的の保持を Art. 6(1)(c) 法的義務として明示的に許容） |
| 違反報告（受信側記録）| **7 年保持** | 再犯・不正調査。`users.id` 参照で個人情報なし |
| 違反報告（通報者 `reporter_id`）| 退会から 30 日後に NULL 化（匿名化） | 通報者情報は個人情報 |
| 管理者操作ログ（`admin_audit_logs`）| **1 年保持** | セキュリティ監査。重大インシデント発生時は手動アーカイブ |
| リフレッシュトークン（失効・失効済み） | `expires_at` から 7 日後にバッチ削除 | — |
| アクセスログ（IP アドレス・User-Agent）| **1 年後削除** | セキュリティ監査・不正アクセス調査・レート制限証拠。データ侵害の検知遅延に備えてジオロケーション推定（EU ユーザー特定・GDPR 報告先 DPA 特定）に使用する。Cloud Logging `lowpolyworld-access` に格納（セクション 5.5 参照） |

`reporter_id` の匿名化は 30 日後バッチ（アカウント物理削除と同じジョブ）で `NULL` に更新する。

**全バッチ共通失敗ポリシー:**

個人データの残存は法的リスクが高いため、削除系バッチはすべて以下のポリシーに従う。

| 項目 | 仕様 |
|---|---|
| リトライ | Cloud Scheduler の自動リトライ（最大 3 回・30 分間隔）|
| 3 回失敗後の Discord 通知 | Cloud Monitoring が `cloudscheduler.googleapis.com/job/last_attempt_result = failed` を検知 → `lowpolyworld-security-alerts` Pub/Sub → `discord-notifier` → Discord（`infra-abstract.md §10` 参照）|
| 部分失敗の検知 | バッチ正常完了時に `{"event": "batch_completed", "batch": "<name>", "affected_count": N}` 構造化ログを出力。Cloud Logging アラートで 26 時間以内にログが出ない場合も同じ Discord チャンネルへ通知 |
| 冪等性 | すべてのバッチは冪等に実装する（複数回実行しても結果が変わらない）|

対象バッチ（毎日 JST 03:00 台に順次実行）:

| バッチ名 | スケジュール | `batch` ログ値 |
|---|---|---|
| アカウント物理削除 | JST 03:00 | `delete-expired-accounts` |
| 通報者匿名化 | JST 03:05 | `anonymize-reporters` |
| アクセスログ IP/UA NULL 化 | JST 03:30 | `cleanup-access-logs` |

**通報者匿名化バッチ仕様:**

| 項目 | 仕様 |
|---|---|
| スケジュール | 毎日 JST 03:00（Cloud Scheduler による cron トリガー） |
| 対象 | `user_violation_reports` テーブルの `reporter_id IS NOT NULL` かつ、`reporter_id` が指す `active_users.deleted_at < now() - interval '30 days'` であるレコード |
| 処理 | 対象レコードの `reporter_id` を `NULL` に UPDATE（`anonymized_at = now()` も同時に記録） |
| 冪等性 | 条件に `anonymized_at IS NULL` を追加することで、同一日付に複数回実行しても重複 UPDATE が発生しない |
| 失敗時処理 | 全バッチ共通ポリシー準拠（Cloud Scheduler 3 回リトライ → 失敗時 Discord 通知）。正常完了時に `{"event": "batch_completed", "batch": "anonymize-reporters", "affected_count": N}` 構造化ログを出力 |
| ロールバック | UPDATE は 1,000 件ずつバッチ分割してコミットする。中断時は次回実行で残りを処理できる（冪等） |

`user_violation_reports` テーブルに `anonymized_at TIMESTAMPTZ` カラムを追加する。

**IP アドレス・User-Agent の利用目的:**
アクセスログに記録する IP アドレスおよび User-Agent は以下の目的のみに使用し、1 年経過後に自動削除する。

| 目的 | 詳細 |
|---|---|
| セキュリティ監査 | 不正アクセス・アカウント乗っ取り調査時の証跡 |
| 不正検知・レート制限 | IP ベースのレート制限（セクション 6 参照）の証拠保全 |
| サービス品質改善 | クライアントプラットフォーム分布の統計分析（個人識別には使用しない） |
| データ侵害時の報告先特定 | ブリーチ発生時に影響ユーザーの IP アドレスをジオロケーションで国コードに変換し、EU 居住ユーザーが含まれるか確認する。該当する場合、各国 DPA（Data Protection Authority）への報告先特定に使用する（GDPR Art. 33 対応） |

**保持期間を 90 日から 1 年に延長した理由:**
データ侵害の発覚が検知から数ヶ月遅延するケースに備え、ブリーチ発生時点のアクセスログを遡って参照できるようにする。GDPR Art. 6(1)(f)（正当な利益）に基づく保持。プライバシーポリシーに明記する。

利用目的・保管期間はプライバシーポリシーに明記する。

**購入履歴保存期間の法的根拠:**

保存期間は一律 **7 年** とする。居住国別の細分化は現時点では行わない。

| 根拠 | 内容 |
|---|---|
| 日本（法人税法 126 条） | 帳簿書類の保存義務 7 年 |
| GDPR | 会計・税務目的のデータ保持は Art. 6(1)(c)「法的義務の履行」として適法。「忘れられる権利」（Art. 17）は法的義務に基づく処理には適用されない |
| その他主要地域 | 米国・韓国等の一般的な税務帳簿保存義務も 5〜7 年の範囲であり 7 年保持は適合する |

> 将来的に EU 法人格を取得・EU 向けサービス拡大が決定した時点で、EU 各国の税務保存義務（国によっては 10 年）を反映した居住国別保存期間テーブルへの移行を検討する。

---

## 13.1 セキュリティ通知（大規模データ侵害時のユーザー通知）

GDPR Art. 34（高リスクブリーチ時の本人通知義務）に対応するため、アプリ起動時に強制モーダルを表示する仕組みを実装する。プッシュ通知は利用者が無効化できるため「直接通知（directly）」の要件を満たせない可能性があり、強制モーダルを正式な通知手段として位置づける。

### system_security_notices テーブル

```sql
CREATE TABLE system_security_notices (
  id          TEXT        PRIMARY KEY,   -- 例: "breach-2026-04-07"
  title       TEXT        NOT NULL,
  body        TEXT        NOT NULL,
  active      BOOLEAN     NOT NULL DEFAULT false,
  created_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);
```

- `active = true` のレコードは最大 1 件（管理画面から `super_admin` が操作）
- `active = false` にしても `active_users.last_seen_security_notice_id` の記録は残す（既確認ユーザーに再表示しないため）

### GET /startup レスポンスへの追加

`active = true` の通知があり、かつ `active_users.last_seen_security_notice_id != notice.id` のユーザーには `securityNotice` フィールドを返す。確認済みユーザーには `null` を返す。

```json
{
  "planCapabilities": { ... },
  "vivoxId": "550e8400-e29b-41d4-a716-446655440000",
  "securityNotice": {
    "id": "breach-2026-04-07",
    "title": "重要なセキュリティに関するお知らせ",
    "body": "..."
  }
}
```

通知がない場合・確認済みの場合:

```json
{
  "planCapabilities": { ... },
  "vivoxId": "550e8400-e29b-41d4-a716-446655440000",
  "securityNotice": null
}
```

### 確認記録エンドポイント

```
POST /api/v1/me/security-notice-ack
Body: { "noticeId": "breach-2026-04-07" }
```

- `active_users.last_seen_security_notice_id = noticeId` を更新する
- クライアントはモーダルの「確認しました」ボタンタップ時に呼び出す
- 認証必須（JWT）

---

## 14. ブロック機能

ユーザーが他のユーザーをブロックする機能。ブロックは永続的でセッションをまたいで有効。

### `user_blocks` テーブル

| カラム | 型 | 説明 |
|---|---|---|
| id | BIGSERIAL PK | |
| blocker_id | TEXT NOT NULL | ブロックしたユーザーの `users.id` |
| blocked_id | TEXT NOT NULL | ブロックされたユーザーの `users.id` |
| created_at | TIMESTAMPTZ NOT NULL DEFAULT now() | |

`UNIQUE (blocker_id, blocked_id)`

### エンドポイント

```
POST   /api/v1/users/{id}/block    → 204（ブロック実行）
DELETE /api/v1/users/{id}/block    → 204（ブロック解除）
GET    /api/v1/me/blocks           → ブロックリスト取得（IDカーソルページネーション）
```

### ブロックの効果

| 効果 | 詳細 |
|---|---|
| ワールド内でアバターを非表示 | ブロックしたユーザーのアバターはレンダリングしない |
| 音声接続を遮断 | Vivox / LiveKit の channel から相互に聞こえない状態にする |
| フレンド・フォロー関係を削除 | ブロック実行時に双方向のフレンド・フォロー関係を解除 |
| プロフィールの非表示 | ブロック相手のプロフィール画面は表示しない（404相当） |
| ルーム参加の制限なし | ブロックしていても同じルームには入れる（表示・音声のみ遮断） |

ブロック関係の存在はブロックされた側に通知しない（相手に知られない）。

---

## 15. 本番インフラ方針（マネージドサービス）

開発環境は Docker Compose（セクション2）を使用する。本番環境ではすべてのステートフルコンポーネントをマネージドサービスに委譲し、HA・バックアップ・スケーリングを自前で管理しない。

### コンポーネント別方針

| コンポーネント | 方針 | 根拠 |
|---|---|---|
| PostgreSQL | マネージド DB サービス（Cloud SQL / Supabase / Neon 等）を使用。自動フェイルオーバー・定期バックアップ・ポイントインタイムリカバリが付属 | 手動でのレプリケーション管理・Patroni 設定は運用コストが高く小規模チームには不適 |
| Redis | マネージド Redis サービス（Upstash / Cloud Memorystore 等）を使用。Sentinel 構成・永続化設定が付属 | セッション・キャッシュ消失は再起動で回復可能だが可用性は維持したい |
| Go API | コンテナホスティング（Cloud Run / Fly.io 等）を使用。ロードバランサ・自動スケールアウト付属 | ステートレスなため複数インスタンス化が容易 |
| VRM Optimizer | Go API と同じコンテナホスティングで複数インスタンス起動。Redis キューを共有してジョブを分散処理 | Optimizer がダウンしてもアバターアップロードのみ影響・ゲーム本体は継続 |
| Cloudflare R2 + CDN | 現行設計通り（HA・バックアップは Cloudflare が担保） | — |

### 具体的なサービス選定

具体的なクラウドプロバイダー・サービス名は運用フェーズで確定する。上記の「マネージドサービスを利用する」という方針のみ本文書で定める。

### 開発・本番の切り替え

| 設定 | 開発 | 本番 |
|---|---|---|
| `DATABASE_URL` | `postgres://user:password@db:5432/lowpolyworld` | マネージド DB の接続文字列（Secrets Manager 注入） |
| `REDIS_URL` | `redis://cache:6379` | マネージド Redis のエンドポイント（Secrets Manager 注入） |
| `STORAGE_BACKEND` | `local` | `r2` |
| JWT 鍵 | ファイルパス | Secrets Manager |

---

## 14. OAuth プロバイダー検証

### 検証方針

サーバーサイドで各プロバイダーのトークンを直接検証する（外部認証サービスへの依存なし）。検証を省略すると任意の `sub`（ユーザー ID）を偽造したリクエストでアカウント乗っ取りが可能になるため、必ず実施する。

### プロバイダー別検証方法

#### Google

- Unity → `POST /auth/google/callback` に **ID Token**（JWT）を送信
- API → Google の JWKS エンドポイント（`https://www.googleapis.com/oauth2/v3/certs`）から公開鍵を取得してローカル検証
- 検証項目: 署名・`iss`（`accounts.google.com`）・`aud`（自アプリのクライアント ID）・`exp`
- JWKS はキャッシュし、後述の「JWKS キャッシュ管理」の方針に従って更新する

#### Apple

- Unity → `POST /auth/apple/callback` に **ID Token**（JWS）と **Authorization Code** を送信
- API → Apple の JWKS エンドポイント（`https://appleid.apple.com/auth/keys`）から公開鍵を取得してローカル検証
- 検証項目: 署名・`iss`（`https://appleid.apple.com`）・`aud`（自アプリのバンドル ID）・`exp`
- Apple **Sign in with Apple** の場合、`email` フィールドは初回のみ返される。2回目以降は `sub`（Apple の安定した一意 ID）で照合する

### メールアドレスの一意性

**同一メールアドレスでの複数アカウント作成を禁止する。**

- `active_users.email` に UNIQUE 制約を設ける
- 複数プロバイダーの連携は、ログイン済み状態でのプロバイダー追加操作（`POST /me/auth-providers`）によってのみ行う

#### ユーザー列挙攻撃対策

OAuth サインイン時にメールアドレスが既存アカウントと一致する場合、**アカウントの存在を開示せず**以下の処理を行う:

1. 登録済みメールアドレスへ「別のプロバイダーでサインインするか、プロバイダー連携を行ってください」という案内メールを送信する
2. クライアントには `200 OK` で `{"data": {"result": "provider_link_email_sent"}}` を返す（アカウントの存在有無を区別しないレスポンス）

攻撃者はメールアドレスの登録有無を API レスポンスから判別できない。

**定時間レスポンス:** メールアドレスが存在するケースと存在しないケースでレスポンス時間に差が生じないよう、存在しない場合も一定のダミー処理（メール送信処理相当の遅延）を実施する。

**Apple プライベートリレーメールの扱い:**

Apple はユーザーの選択により `xxx@privaterelay.appleid.com` 形式のリレーアドレスを返す場合がある。このアドレスは固有の email として通常通り UNIQUE 制約で管理する。ユーザーが後から Google と連携したい場合は設定画面からプロバイダー追加操作で行う。

### JWKS キャッシュ管理

**通常時のキャッシュ更新:**

| 条件 | 動作 |
|---|---|
| `Cache-Control: max-age` が存在する | max-age に従って TTL を設定（Google は通常 24 時間） |
| `Cache-Control` ヘッダーがない | デフォルト TTL = **24 時間** |
| TTL 期限到達 | バックグラウンドで JWKS を再取得。再取得完了まで既存キャッシュを使い続ける |

**`kid` ミスマッチ時（キーローテーション検知）:**

JWT の `kid` がキャッシュに存在しない場合、TTL に関わらず即時に JWKS を再取得する。再取得後も該当 `kid` が存在しない場合は署名検証失敗として処理する。

**JWKS エンドポイントが一時的に到達不能な場合:**

```
再取得に失敗（タイムアウト / 5xx）した場合:
  → 新規ログインリクエスト（POST /auth/{provider}/callback）を 503 service_unavailable で拒否する
  → 既存の JWT は引き続き有効（revocation チェックは DB で行うため影響なし）
  → リトライ: 30 秒・1 分・5 分 の指数バックオフで最大 3 回再試行
  → 3 回失敗後: 管理画面にシステムアラートを生成（「JWKS 取得失敗: {provider}」）+ `lowpolyworld-security-alerts` Pub/Sub → Discord（新規ログイン全停止中）
```

> セキュリティ優先方針。JWKS ダウン中の新規ログインは拒否し、失効した鍵での誤検証を防ぐ。

**キャッシュの実装:**

Redis を用いてインプロセスキャッシュ＋永続化する。キャッシュキー: `jwks:{provider}`（例: `jwks:google`・`jwks:apple`）。

### 署名検証失敗時のログ

| ケース | ログレベル | アラート |
|---|---|---|
| 正規の `kid` で署名検証失敗 | **ERROR**（鍵偽造・トークン改ざんの疑い） | 5 分間に 10 件以上で管理画面にアラート生成 + `lowpolyworld-security-alerts` Pub/Sub → Discord（トークン偽造攻撃の疑い） |
| `kid` がキャッシュに存在しない（再取得後も） | **WARN**（不正な `kid` またはキーローテーション遅延） | なし |
| `exp` 期限切れ | **INFO**（正常なトークン期限切れ） | なし |
| `iss` / `aud` 不一致 | **WARN**（異なる向けのトークンを誤送信） | なし |

**記録するフィールド:**

```json
{
  "level": "ERROR",
  "event": "oauth_signature_verification_failed",
  "provider": "google",
  "kid": "<key_id>",
  "iss": "<issuer>",
  "aud": "<audience>",
  "error_reason": "signature_mismatch",
  "source_ip": "<ip>",
  "request_id": "<uuid>"
}
```

`sub`・`email` などの個人情報はログに含めない。

### ソーシャルサインイン フロー（更新版）

```
1. Unity → POST /auth/{provider}/callback（Google: ID Token / Apple: ID Token + Authorization Code を送信）
2. API → プロバイダー別の検証処理（上記参照）でトークンを検証・ユーザー情報取得
3. API → provider_id で既存アカウントを照合
   ├── 一致あり → JWT を発行して返却（既存ユーザーのログイン）
   └── 一致なし → email で既存アカウントを照合
       ├── email 一致あり → 案内メール送信 → 200（provider_link_email_sent）を返す（アカウント存在を開示しない）
       └── email 一致なし → 新規アカウント作成 → JWT 発行 → name_setup_required: true を返す
4. Unity → JWT を Application.persistentDataPath に保存し以降のリクエストに使用
```
