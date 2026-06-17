/// <summary>
/// ルール一覧の各行に表示する「きっかけ → アクション」の 1 行サマリーを生成する純粋 C# ロジック。
/// 初心者が一覧を見ただけで各ルールの動作を把握できるようにする（ギミックタブ UX）。
/// 表示ラベルは <see cref="GimmickTypeCatalog"/> の日本語ラベルを使う。
/// </summary>
public static class GimmickRuleSummary
{
    /// <summary>ルールの要約文を返す（例: "オブジェクトに接触したとき → ワールド変数を変更（条件あり）"）。</summary>
    public static string Of(GimmickRule rule)
    {
        if (rule == null)
            return "";

        int trigCount = rule.triggers?.Length ?? 0;
        int actCount = rule.actions?.Length ?? 0;
        int condCount = rule.conditions?.Length ?? 0;

        string trigPart = trigCount == 0
            ? "（きっかけ未設定）"
            : GimmickTypeCatalog.TriggerLabel(TypeOf(rule.triggers, 0)) + (trigCount > 1 ? " ほか" : "");

        string actPart = actCount == 0
            ? "（アクション未設定）"
            : GimmickTypeCatalog.ActionLabel(ActionTypeOf(rule.actions, 0)) + (actCount > 1 ? " ほか" : "");

        string condPart = condCount > 0 ? "（条件あり）" : "";

        return $"{trigPart} → {actPart}{condPart}";
    }

    private static string TypeOf(GimmickTrigger[] triggers, int i) =>
        triggers != null && i < triggers.Length && triggers[i] != null ? triggers[i].type ?? "" : "";

    private static string ActionTypeOf(GimmickAction[] actions, int i) =>
        actions != null && i < actions.Length && actions[i] != null ? actions[i].type ?? "" : "";
}
