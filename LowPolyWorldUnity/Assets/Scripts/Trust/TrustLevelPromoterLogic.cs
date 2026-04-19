/// <summary>
/// クライアント側トラストレベル評価ロジック（純粋 C#・MonoBehaviour 非依存）。
/// サーバー側と同一のアルゴリズムを実装し、UI 表示や状態予測に使用する。
///
/// 仕様: unity-game-abstract.md §22.4
/// </summary>
public class TrustLevelPromoterLogic
{
    public const string Visitor = "visitor";
    public const string NewUser = "new_user";
    public const string User = "user";
    public const string TrustedUser = "trusted_user";

    /// <summary>
    /// スナップショットデータからトラストレベルを評価する。
    /// TrustLevelLocked の場合も評価自体は実行する（呼び出し元が DB 書き込みをスキップする）。
    /// </summary>
    public string Evaluate(TrustSnapshot snapshot)
    {
        // trusted_user: 公開ワールド数 >= 2 かつ いずれか1ワールドのいいね数 >= 100
        if (snapshot.PublicWorldCount >= 2 && snapshot.MaxWorldLikes >= 100)
            return TrustedUser;

        // user: (ポイント >= 1000 かつ フレンド数 >= 5) OR コイン購入履歴あり OR プレミアム
        if ((snapshot.TrustPoints >= 1000 && snapshot.FriendCount >= 5)
            || snapshot.HasCoinPurchase
            || snapshot.IsPremium)
            return User;

        // new_user: ポイント >= 1000 OR (ポイント >= 300 かつ フレンド数 >= 3)
        if (snapshot.TrustPoints >= 1000
            || (snapshot.TrustPoints >= 300 && snapshot.FriendCount >= 3))
            return NewUser;

        return Visitor;
    }

    /// <summary>
    /// レベルの序列を返す（大きいほど信頼度が高い）。
    /// </summary>
    public static int LevelRank(string level)
    {
        return level switch
        {
            TrustedUser => 3,
            User => 2,
            NewUser => 1,
            _ => 0,
        };
    }
}

/// <summary>
/// トラストレベル評価に必要なユーザーデータのスナップショット。
/// Phase 8/9 で FriendCount・HasCoinPurchase が実際に埋まるまでゼロ値を使用する。
/// </summary>
public struct TrustSnapshot
{
    public float TrustPoints;
    public bool IsPremium;
    public bool TrustLevelLocked;
    public int PublicWorldCount;
    public long MaxWorldLikes;
    public int FriendCount;     // Phase 9 で利用
    public bool HasCoinPurchase; // Phase 8 で利用
}
