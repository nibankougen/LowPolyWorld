using System;
using System.Collections.Generic;

/// <summary>
/// ワールドオブジェクトのテクスチャコストを計算するロジッククラス（world-creation.md セクション 4）。
///
/// コスト計算式: (テクスチャサイズ / 16)²
/// カウント単位: ワールドで使用する一意の objectTypeId または savedVariantId（別エントリ）
/// ギミックで常時非表示に設定されているオブジェクト種別はコスト対象外
/// </summary>
public static class TextureCostCalculator
{
    public const int CostLimit = 4096;
    public const int ObjectCountLimit = 400;

    /// <summary>テクスチャサイズ（px）からコストを計算する。</summary>
    public static int CostForSize(int textureSizePx)
    {
        int factor = textureSizePx / 16;
        return factor * factor;
    }

    /// <summary>
    /// ワールドに配置されたオブジェクト一覧の合計テクスチャコストを計算する。
    /// </summary>
    /// <param name="objects">配置オブジェクト一覧。</param>
    /// <param name="textureSizeGetter">
    /// objectTypeId または savedVariantId → テクスチャサイズ（px）のマッパー。
    /// 存在しないキーには 64（デフォルト）を返すことを推奨。
    /// </param>
    /// <param name="alwaysHiddenTypeIds">
    /// ギミックで常時非表示にされた objectTypeId の集合。null = 除外なし。
    /// </param>
    public static int Calculate(
        IEnumerable<WorldObjectInstance> objects,
        Func<string, int> textureSizeGetter,
        HashSet<string> alwaysHiddenTypeIds = null)
    {
        var uniqueKeys = new HashSet<string>();

        foreach (var obj in objects)
        {
            if (alwaysHiddenTypeIds != null && alwaysHiddenTypeIds.Contains(obj.objectTypeId))
                continue;

            // savedVariantId が設定されている場合はそちらを独立エントリとしてカウント
            var key = string.IsNullOrEmpty(obj.savedVariantId) ? obj.objectTypeId : obj.savedVariantId;
            if (!string.IsNullOrEmpty(key))
                uniqueKeys.Add(key);
        }

        int total = 0;
        foreach (var key in uniqueKeys)
            total += CostForSize(textureSizeGetter(key));

        return total;
    }

    /// <summary>現在のコストに additionalCost を加算してもコスト上限以内か確認する。</summary>
    public static bool CanAdd(int currentCost, int additionalCost) =>
        currentCost + additionalCost <= CostLimit;

    /// <summary>コスト使用率（0.0〜1.0）を返す。</summary>
    public static float UsageRatio(int currentCost) =>
        Math.Clamp((float)currentCost / CostLimit, 0f, 1f);
}
