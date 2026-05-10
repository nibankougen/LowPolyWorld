using System.Collections.Generic;

/// <summary>通報理由の種別。API の validViolationReasons と一致させること。</summary>
public enum ReportReason
{
    Spam,
    Harassment,
    HateSpeech,
    Impersonation,    // 詳細テキスト必須
    Inappropriate,
    Violence,
    Misinformation,
    Other,            // 詳細テキスト必須
}

/// <summary>
/// 通報モーダルの入力バリデーションロジック（純粋 C#）。
/// 状態管理のみ担当。API 送信は呼び出し元が行う。
/// 仕様: screens-and-modes.md セクション 6.5
/// </summary>
public class ReportModalLogic
{
    private static readonly HashSet<ReportReason> RequiredDetailReasons = new()
    {
        ReportReason.Impersonation,
        ReportReason.Other,
    };

    public ReportReason? SelectedReason { get; private set; }
    public string DetailText { get; private set; } = string.Empty;
    public bool HideUser { get; private set; }
    public bool IsAlreadyHidden { get; }

    /// <summary>非表示チェックボックスを表示するか（対象がまだ非表示でない場合のみ表示）。</summary>
    public bool ShowHideCheckbox => !IsAlreadyHidden;

    /// <summary>選択中の理由が詳細テキスト必須かどうか。</summary>
    public bool IsDetailRequired =>
        SelectedReason.HasValue && RequiredDetailReasons.Contains(SelectedReason.Value);

    /// <summary>通報送信ボタンを有効にする条件: 理由選択済み かつ 必須詳細が入力済み。</summary>
    public bool CanSubmit =>
        SelectedReason.HasValue && (!IsDetailRequired || !string.IsNullOrEmpty(DetailText));

    /// <param name="isAlreadyHidden">対象ユーザー/コンテンツが既に非表示の場合 true。</param>
    public ReportModalLogic(bool isAlreadyHidden = false)
    {
        IsAlreadyHidden = isAlreadyHidden;
        HideUser = !isAlreadyHidden;
    }

    /// <summary>通報理由を選択する。</summary>
    public void SelectReason(ReportReason reason)
    {
        SelectedReason = reason;
    }

    /// <summary>詳細テキストを更新する。null は空文字列として扱う。</summary>
    public void UpdateDetailText(string text)
    {
        DetailText = text ?? string.Empty;
    }

    /// <summary>
    /// 非表示チェックボックスの値を設定する。
    /// 既に非表示の場合は何もしない（チェックボックス自体が表示されないため）。
    /// </summary>
    public void SetHideUser(bool hide)
    {
        if (!IsAlreadyHidden)
            HideUser = hide;
    }

    /// <summary>選択中の理由を API 送信用の snake_case 文字列に変換する。</summary>
    public string SelectedReasonApiString()
    {
        return SelectedReason switch
        {
            ReportReason.Spam => "spam",
            ReportReason.Harassment => "harassment",
            ReportReason.HateSpeech => "hate_speech",
            ReportReason.Impersonation => "impersonation",
            ReportReason.Inappropriate => "inappropriate",
            ReportReason.Violence => "violence",
            ReportReason.Misinformation => "misinformation",
            ReportReason.Other => "other",
            _ => "other",
        };
    }
}
