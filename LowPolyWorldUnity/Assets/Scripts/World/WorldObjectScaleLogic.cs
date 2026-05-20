/// <summary>
/// ワールドオブジェクトのインスタンスサイズ（W/D/H）を管理するロジッククラス。
/// 値は 0.25m 単位にスナップされ、最小値は 0.25m。
/// ショップオブジェクトにはスケールロックが設定され、変更が拒否される。
/// </summary>
public class WorldObjectScaleLogic
{
    public const float SnapUnit = 0.25f;

    public float Width { get; private set; }
    public float Depth { get; private set; }
    public float Height { get; private set; }
    public bool ScaleLocked { get; }

    public WorldObjectScaleLogic(float width, float depth, float height, bool scaleLocked = false)
    {
        ScaleLocked = scaleLocked;
        Width = SnapValue(width);
        Depth = SnapValue(depth);
        Height = SnapValue(height);
    }

    /// <summary>
    /// W/D/H を設定する。スケールロック中は変更せず false を返す。
    /// 各値は 0.25m 単位にスナップし、0.25m 未満は 0.25m にクランプする。
    /// </summary>
    public bool TrySetScale(float width, float depth, float height)
    {
        if (ScaleLocked)
            return false;
        Width = SnapValue(width);
        Depth = SnapValue(depth);
        Height = SnapValue(height);
        return true;
    }

    /// <summary>
    /// float 値を 0.25m 単位に四捨五入し、0.25m 未満は 0.25m にクランプする。
    /// </summary>
    public static float SnapValue(float value)
    {
        double snapped =
            System.Math.Round((double)value / SnapUnit, System.MidpointRounding.AwayFromZero) * SnapUnit;
        return (float)System.Math.Max((double)SnapUnit, snapped);
    }
}
