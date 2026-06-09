using System.Collections.Generic;

/// <summary>
/// ワールドタグの追加・削除・バリデーションを管理するロジッククラス。
/// 最大 5 個 / 1 タグ最大 20 文字。
/// </summary>
public class WorldTagLogic
{
    public const int MaxTags = 5;
    public const int MaxTagLength = 20;

    private readonly List<string> _tags;

    public int Count => _tags.Count;
    public bool IsFull => _tags.Count >= MaxTags;

    public WorldTagLogic() => _tags = new List<string>();

    public WorldTagLogic(IEnumerable<string> initialTags)
    {
        _tags = new List<string>(initialTags);
    }

    /// <summary>
    /// タグを追加する。
    /// </summary>
    public TagAddResult TryAdd(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return TagAddResult.Empty;
        if (tag.Length > MaxTagLength)
            return TagAddResult.TooLong;
        if (IsFull)
            return TagAddResult.LimitReached;
        if (_tags.Contains(tag))
            return TagAddResult.AlreadyExists;
        _tags.Add(tag);
        return TagAddResult.Success;
    }

    /// <summary>タグを削除する。</summary>
    public void Remove(string tag) => _tags.Remove(tag);

    /// <summary>全タグを削除する。</summary>
    public void Clear() => _tags.Clear();

    /// <returns>全タグの読み取り専用リスト。</returns>
    public IReadOnlyList<string> GetTags() => _tags.AsReadOnly();
}

public enum TagAddResult
{
    Success,
    Empty,
    TooLong,
    LimitReached,
    AlreadyExists,
}
