using UnityEngine;

/// <summary>
/// ワールドオブジェクト配置のグリッドスナップ・範囲クランプ（world-creation.md 3.4 / 3.5・12.2b）。純粋 C#。
///
/// - 位置: 0.5m グリッドの整数（原点中心）。グリッド 63×31×63 = X/Z:[-31, 31]・Y:[-15, 15]
/// - 回転: Y 軸 45° 単位を 0〜7 の整数（rotationY）で保持する
/// - プレイ可能範囲: 原点中心の 64m 立方（この外に出たプレイヤーはリスポーンする）
///
/// 値型の Vector3 / IntVec3Json のみ扱い、シーンには依存しない。
/// </summary>
public static class ObjectGridSnap
{
    /// <summary>位置グリッド 1 マス = 0.5m。</summary>
    public const float PositionUnit = 0.5f;

    /// <summary>X/Z の片側マス数（-31〜31 = 63 マス）。</summary>
    public const int HalfExtentXZ = 31;

    /// <summary>Y の片側マス数（-15〜15 = 31 マス）。</summary>
    public const int HalfExtentY = 15;

    /// <summary>回転の段数（45° × 8 = 360°）。rotationY は 0〜7。</summary>
    public const int RotationSteps = 8;

    /// <summary>1 段の回転角（度）。</summary>
    public const int RotationStepDegrees = 360 / RotationSteps;

    /// <summary>プレイ可能範囲（原点中心 64m 立方）の片側の長さ（m）。</summary>
    public const float PlayAreaHalfExtentMeters = 32f;

    /// <summary>ワールド座標（m）1 軸をグリッド整数（0.5m 単位）に丸める。</summary>
    public static int SnapAxis(float worldMeters) => Mathf.RoundToInt(worldMeters / PositionUnit);

    /// <summary>ワールド座標をグリッド整数位置に丸める。</summary>
    public static IntVec3Json SnapPosition(Vector3 worldPos) =>
        new IntVec3Json(SnapAxis(worldPos.x), SnapAxis(worldPos.y), SnapAxis(worldPos.z));

    /// <summary>グリッド整数位置をワールド座標（m）に変換する。</summary>
    public static Vector3 ToWorld(IntVec3Json gridPos) => gridPos.ToVector3(PositionUnit);

    /// <summary>グリッド整数位置が配置可能範囲（63×31×63）内か。</summary>
    public static bool IsInBounds(IntVec3Json p) =>
        Mathf.Abs(p.x) <= HalfExtentXZ && Mathf.Abs(p.y) <= HalfExtentY && Mathf.Abs(p.z) <= HalfExtentXZ;

    /// <summary>グリッド整数位置を配置可能範囲にクランプする。</summary>
    public static IntVec3Json Clamp(IntVec3Json p) =>
        new IntVec3Json(
            Mathf.Clamp(p.x, -HalfExtentXZ, HalfExtentXZ),
            Mathf.Clamp(p.y, -HalfExtentY, HalfExtentY),
            Mathf.Clamp(p.z, -HalfExtentXZ, HalfExtentXZ));

    /// <summary>回転段数を 0〜7 に正規化する（負・8 以上も巻き戻す）。</summary>
    public static int NormalizeRotationStep(int step)
    {
        int s = step % RotationSteps;
        return s < 0 ? s + RotationSteps : s;
    }

    /// <summary>自由角度（度）を 45° 単位の回転段数（0〜7）に丸める。</summary>
    public static int RotationStepFromDegrees(float degrees) =>
        NormalizeRotationStep(Mathf.RoundToInt(degrees / RotationStepDegrees));

    /// <summary>回転段数（0〜7）を度に変換する。</summary>
    public static int RotationToDegrees(int step) => NormalizeRotationStep(step) * RotationStepDegrees;

    /// <summary>プレイヤーがプレイ可能範囲（原点中心 64m 立方）内にいるか。外ならリスポーン対象（3.4）。</summary>
    public static bool IsInsidePlayArea(Vector3 worldPos) =>
        Mathf.Abs(worldPos.x) <= PlayAreaHalfExtentMeters
        && Mathf.Abs(worldPos.y) <= PlayAreaHalfExtentMeters
        && Mathf.Abs(worldPos.z) <= PlayAreaHalfExtentMeters;
}
