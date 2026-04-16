using System;

/// <summary>
/// アバター・アクセサリ共通のペイントセッションインターフェース。
/// TexturePaintController はこのインターフェース経由でセッションを操作し、
/// キャンバスサイズに依存しない汎用ペイント UI を実現する。
/// </summary>
public interface IPaintSession : IDisposable
{
    uint CanvasWidth { get; }
    uint CanvasHeight { get; }
    LayerStack LayerStack { get; }
    bool CanUndo { get; }
    bool CanRedo { get; }
    bool IsReady { get; }

    PaintLayer AddNormalLayer(string name = null);
    PaintLayer AddColorAdjustmentLayer();
    bool RemoveLayer(uint layerId);
    bool MoveLayer(uint layerId, int newIndex);
    void SetLayerVisible(uint layerId, bool visible);
    void SetLayerOpacity(uint layerId, float opacity);
    void SetLayerLocked(uint layerId, bool locked);
    void SetLayerMaskBelow(uint layerId, bool maskBelow);

    void Brush(uint layerId, int cx, int cy, uint radius, byte r, byte g, byte b, byte a, bool antialiased);
    void Eraser(uint layerId, int cx, int cy, uint radius);
    void FloodFill(uint layerId, int x, int y, byte r, byte g, byte b, byte a, byte tolerance);
    void DrawRect(uint layerId, int x1, int y1, int x2, int y2, byte r, byte g, byte b, byte a, bool filled);
    void DrawCircle(uint layerId, int x1, int y1, int x2, int y2, byte r, byte g, byte b, byte a, bool filled);
    void DrawLine(uint layerId, int x1, int y1, int x2, int y2, byte r, byte g, byte b, byte a);

    /// <summary>指定レイヤーの RGBA ピクセルを返す。色調補正レイヤーや存在しない ID は null。</summary>
    byte[] GetLayerPixels(uint layerId);

    byte[] CompositeRgba();
    byte[] CompositePng();

    bool Undo();
    bool Redo();
    void ClearHistory();

    // ---- レイヤー変形 ----

    void TranslateLayer(uint layerId, int dx, int dy);
    void ScaleLayerNn(uint layerId, float scaleX, float scaleY);

    // ---- 範囲選択 ----

    void SelectionSetRect(int x1, int y1, int x2, int y2);
    void SelectionSetEllipse(int x1, int y1, int x2, int y2);
    void SelectionClear();
    void SelectionInvert();
    bool SelectionHas { get; }

    // ---- グループ管理 ----

    uint AddGroup(string name);
    bool RemoveGroup(uint groupId);
    bool SetGroupVisible(uint groupId, bool visible);
    bool SetLayerGroup(uint layerId, uint groupId);

    // ---- 保存後クリーンアップ ----

    void CleanupUndo();

    // ---- キャンバスリサイズ ----

    /// <summary>キャンバスを最近傍補間でリサイズする。Undo 履歴・選択範囲はクリアされる。</summary>
    void ResizeCanvas(uint newWidth, uint newHeight);

    // ---- レイヤー取り込み ----

    /// <summary>PNG バイト列を指定レイヤーに読み込む。サイズ不一致時は最近傍でリサイズ。</summary>
    bool ImportLayerPng(uint layerId, byte[] pngData);

    // ---- UV オーバーレイ ----

    /// <summary>UV チャートをベイクした RGBA バイト列（width×height×4）を設定する。設定後は visible=true になる。</summary>
    void SetUvOverlay(byte[] rgba);

    /// <summary>UV オーバーレイの表示/非表示を切り替える。</summary>
    void SetUvOverlayVisible(bool visible);

    /// <summary>UV オーバーレイが設定されているか。</summary>
    bool HasUvOverlay { get; }

    /// <summary>UV オーバーレイが表示状態か。</summary>
    bool UvOverlayVisible { get; }
}
