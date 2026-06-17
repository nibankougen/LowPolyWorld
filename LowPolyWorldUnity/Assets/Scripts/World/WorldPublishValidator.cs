using System.Collections.Generic;

/// <summary>
/// ワールド公開前バリデーションロジック（screens-and-modes.md セクション 11.7.6）。
/// ギミック無限ループは <see cref="GimmickLoopPrecheck"/> の内部テストプレイ結果を
/// 受け取って判定する。
/// </summary>
public class WorldPublishValidator
{
    /// <summary>
    /// 公開バリデーションを実行し、検出されたエラーの一覧を返す。
    /// 空リストなら公開可。
    /// </summary>
    /// <param name="def">ワールド定義。</param>
    /// <param name="textureCost">現在の合計テクスチャコスト。</param>
    /// <param name="objectCount">配置オブジェクト総数。</param>
    /// <param name="hasThumbnail">サムネイルが設定されているか。</param>
    /// <param name="publishedVersion">現在の公開バージョン番号（INT_MAX 到達チェック用）。</param>
    /// <param name="spawnPortalOverlap">
    /// スポーン位置・ポータルのいずれかが地形/オブジェクト/相互と重複しているか
    /// （呼び出し側が <see cref="SpecialObjectOverlap"/> + 占有クエリで算出して渡す）。
    /// </param>
    /// <param name="gimmickLoopRuleId">
    /// 内部テストプレイ（<see cref="GimmickLoopPrecheck"/>）で無限ループが検出された場合の原因ルール ID。
    /// 空 / null ならループなし。呼び出し側が原因ルールの特定表示に使う。
    /// </param>
    public IReadOnlyList<PublishError> Validate(
        WorldDefinitionJson def,
        int textureCost,
        int objectCount,
        bool hasThumbnail,
        int publishedVersion = 0,
        bool spawnPortalOverlap = false,
        string gimmickLoopRuleId = null)
    {
        var errors = new List<PublishError>();

        if (def == null || string.IsNullOrWhiteSpace(def.worldName))
            errors.Add(PublishError.WorldNameEmpty);

        if (!hasThumbnail)
            errors.Add(PublishError.ThumbnailMissing);

        if (!HasSpawn(def))
            errors.Add(PublishError.SpawnNotSet);

        if (HasPortalWithoutExit(def))
            errors.Add(PublishError.PortalExitMissing);

        if (spawnPortalOverlap)
            errors.Add(PublishError.SpawnPortalOverlap);

        if (!string.IsNullOrEmpty(gimmickLoopRuleId))
            errors.Add(PublishError.GimmickLoopDetected);

        if (textureCost > TextureCostCalculator.CostLimit)
            errors.Add(PublishError.TextureCostExceeded);

        if (objectCount > TextureCostCalculator.ObjectCountLimit)
            errors.Add(PublishError.ObjectCountExceeded);

        if (publishedVersion == int.MaxValue)
            errors.Add(PublishError.VersionNumberOverflow);

        return errors;
    }

    // ── Private helpers ─────────────────────────────────────────────────────

    // SpawnPointData.isSet フラグでスポーン設定済みを判定する。
    // float 座標での等値比較は使わない（origin が有効な位置のため）。
    private static bool HasSpawn(WorldDefinitionJson def) =>
        def?.specialObjects?.spawn?.isSet == true;

    // 入口ポータルが存在するとき、すべての入口に出口が設定されているかを確認する
    private static bool HasPortalWithoutExit(WorldDefinitionJson def)
    {
        if (def?.specialObjects?.portals == null) return false;
        foreach (var portal in def.specialObjects.portals)
            if (string.IsNullOrEmpty(portal.exitId))
                return true;
        return false;
    }
}

public enum PublishError
{
    WorldNameEmpty,
    ThumbnailMissing,
    SpawnNotSet,
    PortalExitMissing,
    SpawnPortalOverlap, // スポーン/ポータルが地形・オブジェクト・相互と重複
    TextureCostExceeded,
    ObjectCountExceeded,
    VersionNumberOverflow,
    GimmickLoopDetected, // 内部テストプレイ（GimmickLoopPrecheck）で無限ループを検出
}
