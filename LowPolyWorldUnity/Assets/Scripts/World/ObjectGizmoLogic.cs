/// <summary>
/// ワールドオブジェクトのギズモ操作（移動・回転・拡大縮小）ロジック（world-creation.md 3.5 / 11.7.3）。純粋 C#。
///
/// WorldObjectInstance を直接更新し、成否を bool で返す。**不可時はインスタンスを変更しない**ため、
/// グループ一括適用の「いずれか 1 つでも適用不可なら操作全体をキャンセル」（11.7.3）を
/// 上位レイヤーが実装できる。
///
/// size は (W, D, H) = (x, y, z) の 0.25m 単位整数。(0,0,0) は種別デフォルトサイズのセンチネル。
/// </summary>
public static class ObjectGizmoLogic
{
    /// <summary>サイズ最小値（0.25m = 1 単位）。</summary>
    public const int MinSizeUnits = 1;

    /// <summary>指定グリッド位置へ移動する。配置可能範囲外なら変更せず false。</summary>
    public static bool TryMoveTo(WorldObjectInstance obj, IntVec3Json target)
    {
        if (obj == null || target == null)
            return false;
        if (!ObjectGridSnap.IsInBounds(target))
            return false;
        obj.position = new IntVec3Json(target.x, target.y, target.z);
        return true;
    }

    /// <summary>現在位置からグリッド差分だけ移動する。範囲外なら変更せず false。</summary>
    public static bool TryMoveBy(WorldObjectInstance obj, int dx, int dy, int dz)
    {
        if (obj == null)
            return false;
        return TryMoveTo(obj, new IntVec3Json(obj.position.x + dx, obj.position.y + dy, obj.position.z + dz));
    }

    /// <summary>Y 軸を 45° × steps 回転する（0〜7 に正規化・常に成功）。</summary>
    public static void RotateBy(WorldObjectInstance obj, int steps)
    {
        if (obj == null)
            return;
        obj.rotationY = ObjectGridSnap.NormalizeRotationStep(obj.rotationY + steps);
    }

    /// <summary>
    /// W/D/H を 0.25m 単位（int）で増減する。スケールロック中、または
    /// いずれかの軸が最小（0.25m）未満になる場合は変更せず false を返す
    /// （グループ一括の「最小サイズ超過でキャンセル」用）。
    /// size がセンチネル (0,0,0) のときは defaultSize（種別デフォルト・0.25m 単位）を起点にする。
    /// </summary>
    public static bool TryScaleBy(
        WorldObjectInstance obj, int dW, int dD, int dH, IntVec3Json defaultSize, bool scaleLocked)
    {
        if (obj == null)
            return false;
        if (scaleLocked)
            return false;

        IntVec3Json current = obj.size.IsZero
            ? (defaultSize ?? new IntVec3Json(MinSizeUnits, MinSizeUnits, MinSizeUnits))
            : obj.size;
        int w = current.x + dW;
        int d = current.y + dD;
        int h = current.z + dH;
        if (w < MinSizeUnits || d < MinSizeUnits || h < MinSizeUnits)
            return false;

        obj.size = new IntVec3Json(w, d, h);
        return true;
    }
}
