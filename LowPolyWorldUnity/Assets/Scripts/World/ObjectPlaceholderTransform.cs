using UnityEngine;

/// <summary>
/// 配置データ（位置グリッド・回転段・サイズ 0.25m）→ プレースホルダ Cube のワールド変換を算出する
/// 純 C# ヘルパー（world-creation.md 3.4: 配置基準点 = ピボット = 底面中心）。
///
/// size = (W, D, H) = (x, y, z) の 0.25m 単位整数。(0,0,0) は種別デフォルト（defaultSize）で解決する。
/// Unity Cube はメッシュ中心が原点なので、底面中心をグリッド位置に合わせるため Y に高さの半分を足す。
/// </summary>
public static class ObjectPlaceholderTransform
{
    /// <summary>サイズグリッド 1 = 0.25m。</summary>
    public const float SizeUnit = 0.25f;

    /// <summary>センチネル (0,0,0) を defaultSize で解決する（default も null/ゼロなら 1×1×1）。</summary>
    public static IntVec3Json ResolveSize(IntVec3Json size, IntVec3Json defaultSize)
    {
        if (size != null && !size.IsZero)
            return size;
        if (defaultSize != null && !defaultSize.IsZero)
            return defaultSize;
        return new IntVec3Json(1, 1, 1);
    }

    /// <summary>Cube のワールドスケール（x = W, y = H, z = D の各 0.25m）。</summary>
    public static Vector3 WorldScale(IntVec3Json size, IntVec3Json defaultSize)
    {
        var s = ResolveSize(size, defaultSize);
        return new Vector3(s.x * SizeUnit, s.z * SizeUnit, s.y * SizeUnit);
    }

    /// <summary>Cube のワールド中心（底面中心がグリッド位置・上に高さ H の半分だけオフセット）。</summary>
    public static Vector3 WorldCenter(IntVec3Json position, IntVec3Json size, IntVec3Json defaultSize)
    {
        var s = ResolveSize(size, defaultSize);
        float unit = ObjectGridSnap.PositionUnit;
        return new Vector3(
            position.x * unit,
            position.y * unit + s.z * SizeUnit * 0.5f,
            position.z * unit);
    }

    /// <summary>Y 回転（度）。</summary>
    public static float WorldRotationDegrees(int rotationY) => ObjectGridSnap.RotationToDegrees(rotationY);

    /// <summary>Y 回転（Quaternion）。</summary>
    public static Quaternion WorldRotation(int rotationY) =>
        Quaternion.Euler(0f, WorldRotationDegrees(rotationY), 0f);
}
