using System.Collections.Generic;
using UnityEngine;

/// <summary>配置済みスタンプ1枚のデータ。</summary>
public class StampData
{
    public string StampId { get; }
    public Vector2 Position { get; internal set; } // 正規化スクリーン座標 (0-1)
    public float Rotation { get; internal set; }   // 度数法
    public float Scale { get; internal set; } = 1f;

    internal StampData(string stampId, Vector2 position)
    {
        StampId = stampId;
        Position = position;
    }
}

/// <summary>
/// 撮影モードのスタンプオーバーレイ配置ロジック（純粋 C#）。
/// ルームセッション中はスタンプの配置状態をメモリに保持し、
/// 撮影モード再入時に前回の状態を復元できる。
/// 仕様: screens-and-modes.md セクション 2.7.2
/// </summary>
public class StampOverlayLogic
{
    private const float MinScale = 0.1f;

    private readonly List<StampData> _stamps = new();

    public IReadOnlyList<StampData> Stamps => _stamps;

    /// <summary>スタンプを追加して返す。初期 Rotation=0、Scale=1。</summary>
    public StampData AddStamp(string stampId, Vector2 position)
    {
        var stamp = new StampData(stampId, position);
        _stamps.Add(stamp);
        return stamp;
    }

    /// <summary>スタンプを削除する。リスト内に存在しない場合は false を返す。</summary>
    public bool RemoveStamp(StampData stamp)
    {
        return _stamps.Remove(stamp);
    }

    /// <summary>スタンプの位置を更新する。</summary>
    public void MoveStamp(StampData stamp, Vector2 position)
    {
        stamp.Position = position;
    }

    /// <summary>スタンプの回転角度（度数法）を更新する。</summary>
    public void RotateStamp(StampData stamp, float rotation)
    {
        stamp.Rotation = rotation;
    }

    /// <summary>スタンプのスケールを更新する。MinScale 未満はクランプされる。</summary>
    public void ScaleStamp(StampData stamp, float scale)
    {
        stamp.Scale = Mathf.Max(MinScale, scale);
    }

    /// <summary>全スタンプを削除する（ルーム退室・アプリ終了時に呼び出す）。</summary>
    public void Clear()
    {
        _stamps.Clear();
    }
}
