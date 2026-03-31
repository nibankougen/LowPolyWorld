# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**LowPolyWorld** is a mobile UGC avatar space — users upload 3D avatars (VRM 1.0), enter shared worlds, move around, and talk via 3D positional voice. Repository: https://github.com/nibankougen/LowPolyWorld

### Monorepo Structure

| Directory | Role | Runtime |
|---|---|---|
| `LowPolyWorldUnity/` | Unity 6 game client (iOS/Android) | Unity 6.0.3.10f1 |
| `api/` | Go REST API — avatar file serving, world data, auth | Docker |
| `optimizer/` | VRM optimization service — strips unnecessary data on upload | Docker |
| `paint-engine/` | Native 2D texture paint engine called from Unity via P/Invoke | Rust (cdylib) |

Start all server services: `docker compose up --build`

---

## Unity Client

### Platform & Performance Targets

- **Target**: iOS / Android / PC (Windows・Mac) — performance and UI optimized for iOS/Android
- **FPS**: 60fps target, 30fps minimum (with 24 simultaneous avatars)
- **Memory**: < 500MB total
- **Max simultaneous avatars**: 24
- **Safe Area**: All buttons, text, and interactive elements must be placed within `Screen.safeArea`. Apply a `SafeAreaFitter` component to every canvas root RectTransform. Never place interactive or readable content in notch / Dynamic Island (iOS) or camera punch-hole / gesture navigation bar (Android) areas.

### Key Packages

| Package | Purpose |
|---|---|
| `com.unity.render-pipelines.universal` 17.3.0 | URP — Mobile + PC renderer profiles |
| `com.unity.netcode.gameobjects` 2.4.0 | Multiplayer state sync |
| `com.unity.services.vivox` 16.5.0 | 3D positional voice |
| `com.unity.cloud.gltfast` 6.10.0 | GLTF/GLB world loading |
| `com.vrmc.gltf` + `com.vrmc.vrm` 0.128.2 | VRM 1.0 avatar loading (OpenUPM: `com.vrmc`) |
| `com.unity.inputsystem` 1.18.0 | Input (Player + UI action maps) |
| `com.unity.ai.navigation` 2.0.10 | NavMesh |
| `com.unity.services.vivox` | Voice (Vivox SDK) |

### Rendering Constraints (GPU budget — do not violate)

- **Shader**: Unlit, double-sided, no lighting, no real-time shadows, no transparency — cutout only
- **Shadows**: Blob shadow only (circular texture projected onto ground plane, scale-adjusted)
- **Post-processing**: Prohibited (no heavy effects)
- **HDRP**: Prohibited — URP only
- URP renderer configs: `Assets/Settings/PC_RPAsset.asset` / `Mobile_RPAsset.asset`

### Avatar System

- **Format**: VRM 1.0 (downloaded at runtime from API server)
- **Polygon limit**: max 512 tris (recommended 256)
- **Bone limit**: max 50 bones, Humanoid rig required
- **File size**: max 500KB per avatar (model + textures combined)
- **Animation**: Humanoid retargeting with shared animation set; sync = position, rotation, animation ID, animation time

### Texture Atlas System

1枚のテクスチャ・1マテリアルでキャラクターとアクセサリを同一ドローコールで描画する。

**レイアウト (1024×2048, ASTC 6×6 ≈ 0.9MB、ミップマップ込み ≈ 1.2MB):**
- 上段 (1024×1536): キャラクター 24スロット（4列×6行 @ 256×256）
- 中段 (1024×384): アクセサリ 96スロット（16列×6行 @ 64×64）
- 下段 (1024×128): 将来拡張用

- Bilinear filtering, mipmaps required
- **Slot padding**: 2〜4px per slot boundary (required to prevent mipmap atlas bleeding)
- UV coordinates reference inner area excluding padding
- Composition: via RenderTexture (async regeneration)
- Atlas updated on: avatar / accessory add / remove / change
- Performance target: atlas update < 1 second

### World System

- **Format**: GLTF / GLB (loaded at runtime via `com.unity.cloud.gltfast`)
- Loaded by `WorldLoader` manager

### Network Architecture

- **State sync**: Netcode for GameObjects (UDP via Unity Transport)
  - Synced: player position, animation state (on change), avatar change events, world events
  - Position send: max 20Hz, only when delta > 0.01m or rotation > 1° (Unity Relay cost reduction)
  - Interpolation: client-side linear
- **Avatar file fetching**: HTTP to Go API server; static files (VRM/GLB/PNG/atlas) served from CDN via content-addressed URLs (`{sha256}.ext`, `Cache-Control: immutable`); assets cached locally (persistent for own, temporary for others)
- **Voice**: Vivox — 3D positional, per-world channel, distance attenuation; Unity side sends position updates to Vivox SDK

### Accessory System

- **Format**: GLB file, Diffuse texture max 64×64, max 100KB per accessory
- **Max simultaneous avatars**: 24
- **Max per avatar**: 4
- **Attach points**: Head / LeftLowerArm / RightLowerArm / Chest / LeftUpperLeg / RightUpperLeg (Humanoid bones)
- **Rendering**: same custom Unlit shader + Atlas lower-half slots → same draw call as characters
- **Texture editing**: 64×64 = pixel-art style paint (same paint-engine as avatar Diffuse)

### Texture Paint Feature

- Scope: avatar Diffuse (256×256, single texture), accessory Diffuse (64×64), and world object Diffuse (16×16〜512×512; one customization per object type per world (world-scoped); saveable as reusable "saved variants" via slots — 10 normal / 100 premium; saved variants are treated as independent atlas entries)
- Method: 2D paint, **max 16 raster layers + 1 color adjustment layer (counted separately)**, PNG output
- Transparent pixel rule: α < 128 → α=0 and RGB=(0,0,0) fixed (improves ASTC compression); α ≥ 128 → α=255 (RGB preserved). Applied at save time.
- Implementation: **native Rust library** (`paint-engine/`) called from Unity via P/Invoke
  - Unity sends canvas data → native lib processes → returns composited PNG
  - Handles layer management, Undo history, GPU-optimized processing
- Unity plugin binaries placed under `Assets/Plugins/`

### Required Scene Managers

`NetworkManager`, `AvatarManager`, `AtlasManager`, `VoiceManager`, `PlayerController`, `WorldLoader`, `CacheManager`

### Startup Load Flow

1. API version compatibility check (`GET /api/version`) — show update modal and block if incompatible
2. Login (auth via API server — auto-login if token exists, else show account creation modal with terms/privacy consent)
3. World data fetch (API)
4. Avatar list fetch (API)
5. Atlas generation
6. Player spawn
7. Netcode for GameObjects session start
8. Vivox voice connection

### Future Extension (design for loose coupling)

- World split-loading
- Instance servers
- Avatar marketplace
- Custom motion system

---

## Unity Editor Interaction (uloop MCP)

This project uses **uloop MCP** (`io.github.hatayama.uloopmcp` v0.67.5):

- `uloop-compile` — compile and check for errors after editing C# scripts
- `uloop-get-logs` — read Unity Console output
- `uloop-get-hierarchy` — inspect scene GameObject tree
- `uloop-find-game-objects` — locate GameObjects or components
- `uloop-execute-dynamic-code` — editor automation (wiring, AddComponent, batch ops)
- `uloop-control-play-mode` — enter/exit Play mode
- `uloop-run-tests` — run Unity Test Runner
- `uloop-unity-search` — find assets

Always run `uloop-compile` after editing C# files.

---

## C# Formatting

CSharpier (v0.29.0) runs automatically as a git pre-commit hook on staged `.cs` files.

- Config: `.csharpierrc.json` (120 char width, 4-space indent, LF)
- Tool manifest: `.config/dotnet-tools.json`
- New machine setup: `dotnet tool restore` at repo root

---

## C# Architecture Guidelines

### MonoBehaviour の責務制限

MonoBehaviour は Unity エンジンとの境界のみを担当する。**ゲームロジックを MonoBehaviour に書かない。**

担当する処理:
- 物理コールバック（`OnCollisionEnter` / `OnTriggerEnter` など）→ ロジッククラスへ委譲
- Unity ライフサイクル（`Awake` / `Start` / `Update`）→ ロジッククラスのメソッド呼び出しのみ
- アニメーション操作（`Animator.SetBool` / `SetFloat` など）
- シーン参照・オブジェクト操作（`GetComponent` / `Instantiate` / `Destroy` / `transform`）
- Input System コールバック受信 → ロジッククラスへ委譲
- Coroutine / `Time.deltaTime` → ロジッククラスへ渡す

### ロジッククラス（純粋 C#）

ゲームロジックは `UnityEngine` に依存しない純粋 C# クラスで実装する:

- `MonoBehaviour` を継承しない（`new` で生成可能）
- `UnityEngine` 名前空間は原則使用しない
  - 例外: `Vector3` / `Quaternion` / `Color` などの数学・値型（副作用なし）は使用可
- コンストラクタ注入またはインターフェース経由で疎結合にする

### テスト方針

ロジッククラスは **Unity Test Runner** でユニットテストを作成する:

- EditMode テスト: `Assets/Tests/EditMode/`（Unity ランタイム不要・高速）
- PlayMode テスト: `Assets/Tests/PlayMode/`（Unity ランタイムが必要な統合テスト）
- フレームワーク: NUnit（Unity Test Runner 標準）
- **ロジッククラスを新規作成・変更したときは対応するテストを同時に作成・更新する**

---

## C# Scripting Notes

- New scripts: `Assets/Scripts/`
- Player input: `UnityEngine.InputSystem` namespace
- NavMesh: `UnityEngine.AI` namespace
- No `.asmdef` files set up yet
- VRM models use a custom unlit shader override (not UniVRM's default shaders) to meet rendering constraints
