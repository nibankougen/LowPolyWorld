using System.Collections.Generic;

/// <summary>
/// ワールド公開前バリデーションロジック（screens-and-modes.md セクション 11.7.6）。
/// ギミックループ検出は GimmickEngine 実装後に別途追加する。
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
    public IReadOnlyList<PublishError> Validate(
        WorldDefinitionJson def,
        int textureCost,
        int objectCount,
        bool hasThumbnail,
        int publishedVersion = 0)
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
    TextureCostExceeded,
    ObjectCountExceeded,
    VersionNumberOverflow,
    GimmickLoopDetected, // GimmickEngine 実装後に使用
}
