using System.Collections.Generic;

/// <summary>
/// 会話（<see cref="ConversationJson"/>）に登場する話者を求める純粋 C# ヘルパー（9.13）。
/// 会話一覧で「その会話に含まれる話者」を表示するのに使う。名前の解決は <see cref="SpeakerLibraryLogic"/>。
/// </summary>
public static class ConversationSpeakers
{
    /// <summary>
    /// 会話のセリフ行が参照する話者 ID を**初出順・重複なし**で返す（"" = 話者なしの行は除外）。
    /// </summary>
    public static List<string> DistinctSpeakerIds(ConversationJson conv)
    {
        var result = new List<string>();
        if (conv?.lines == null)
            return result;

        var seen = new HashSet<string>();
        foreach (var line in conv.lines)
        {
            if (line == null || string.IsNullOrEmpty(line.speakerId))
                continue;
            if (seen.Add(line.speakerId))
                result.Add(line.speakerId);
        }
        return result;
    }
}
