using System;
using UnityEngine;

/// <summary>
/// 2D ペイントキャンバスのパン・ズーム状態と座標変換を管理する純粋 C# クラス。
/// UI Toolkit の VisualElement 座標系を使用する。
/// </summary>
public class PaintCanvasLogic
{
    /// <summary>最小ズーム倍率（キャンバス全体が表示される最小サイズ）。</summary>
    public const float MinZoom = 0.5f;

    /// <summary>最大ズーム倍率（約 8 倍）。</summary>
    public const float MaxZoom = 8f;

    /// <summary>現在のズーム倍率。</summary>
    public float Zoom { get; private set; } = 1f;

    /// <summary>キャンバス左上のオフセット（VisualElement 座標系）。</summary>
    public Vector2 Offset { get; private set; } = Vector2.zero;

    private readonly uint _canvasWidth;
    private readonly uint _canvasHeight;

    // ピンチジェスチャー追跡
    private float _pinchStartZoom;
    private float _pinchStartDistance;
    private bool _isPinching;

    public PaintCanvasLogic(uint canvasWidth, uint canvasHeight)
    {
        _canvasWidth = canvasWidth;
        _canvasHeight = canvasHeight;
    }

    // ---- パン ----

    /// <summary>
    /// パン操作を適用する。
    /// <paramref name="delta"/> は VisualElement 座標系のピクセル移動量。
    /// </summary>
    public void Pan(Vector2 delta)
    {
        Offset += delta;
    }

    // ---- ズーム ----

    /// <summary>
    /// ピンチ開始を通知する。
    /// <paramref name="distance"/> は 2 本指間の初期距離（ピクセル）。
    /// </summary>
    public void BeginPinch(float distance)
    {
        _pinchStartZoom = Zoom;
        _pinchStartDistance = distance;
        _isPinching = true;
    }

    /// <summary>
    /// ピンチ更新。<paramref name="currentDistance"/> は現在の 2 本指間距離。
    /// <paramref name="pivot"/> はズームの基点（VisualElement 座標）。
    /// </summary>
    public void UpdatePinch(float currentDistance, Vector2 pivot)
    {
        if (!_isPinching || _pinchStartDistance <= 0f)
            return;

        float newZoom = Mathf.Clamp(
            _pinchStartZoom * (currentDistance / _pinchStartDistance),
            MinZoom,
            MaxZoom
        );
        SetZoom(newZoom, pivot);
    }

    /// <summary>ピンチ終了。</summary>
    public void EndPinch()
    {
        _isPinching = false;
    }

    /// <summary>
    /// マウスホイールによるズーム。
    /// <paramref name="delta"/> は正で拡大、負で縮小（通常 ±1 ステップ）。
    /// </summary>
    public void ZoomByWheel(float delta, Vector2 pivot)
    {
        float factor = delta > 0 ? 1.15f : 1f / 1.15f;
        float newZoom = Mathf.Clamp(Zoom * factor, MinZoom, MaxZoom);
        SetZoom(newZoom, pivot);
    }

    // ---- 座標変換 ----

    /// <summary>
    /// VisualElement 座標（ポインター位置）をキャンバスのピクセル座標（整数）に変換する。
    /// キャンバス範囲外の場合も変換するが、範囲内かどうかは呼び出し元が確認すること。
    /// </summary>
    public (int x, int y) ScreenToCanvas(Vector2 screenPos)
    {
        Vector2 local = (screenPos - Offset) / Zoom;
        return ((int)local.x, (int)local.y);
    }

    /// <summary>指定キャンバス座標が範囲内かどうかを返す。</summary>
    public bool IsInCanvas(int x, int y)
    {
        return x >= 0 && y >= 0 && x < (int)_canvasWidth && y < (int)_canvasHeight;
    }

    /// <summary>
    /// 現在の Zoom と Offset に基づいたキャンバスの表示 Rect（VisualElement 座標）を返す。
    /// </summary>
    public Rect GetCanvasRect()
    {
        return new Rect(Offset.x, Offset.y, _canvasWidth * Zoom, _canvasHeight * Zoom);
    }

    /// <summary>
    /// キャンバスをコンテナ中央に配置し、ズームを 1 にリセットする。
    /// <paramref name="containerSize"/> はコンテナの VisualElement サイズ。
    /// </summary>
    public void FitToContainer(Vector2 containerSize)
    {
        Zoom = 1f;
        Offset = new Vector2(
            (containerSize.x - _canvasWidth) * 0.5f,
            (containerSize.y - _canvasHeight) * 0.5f
        );
    }

    // ---- 内部 ----

    private void SetZoom(float newZoom, Vector2 pivot)
    {
        // pivot を中心にズームするよう Offset を補正
        float ratio = newZoom / Zoom;
        Offset = pivot + (Offset - pivot) * ratio;
        Zoom = newZoom;
    }
}
