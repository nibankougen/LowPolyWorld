using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// アバターの生成・破棄・管理を行う MonoBehaviour。
/// WorldScene のみで動作する（DontDestroyOnLoad しない）。
/// </summary>
public class AvatarManager : MonoBehaviour
{
    public static AvatarManager Instance { get; private set; }

    /// <summary>ローカルプレイヤーのアバター。</summary>
    public AvatarInstance LocalAvatar { get; private set; }

    private readonly Dictionary<string, AvatarInstance> _avatars = new();

    [SerializeField] private GameObject _pendingFallbackPrefab;
    [SerializeField] private GameObject _nameTagPrefab;

    private AtlasManager _atlasManager;
    private HideListLogic _hideList;

    /// <summary>ルーム参加時に HideListLogic を注入する。</summary>
    public void SetHideList(HideListLogic hideList) => _hideList = hideList;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        _atlasManager = GetComponent<AtlasManager>();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// VRM GameObject からアバターインスタンスを登録する。
    /// </summary>
    public AvatarInstance RegisterAvatar(string userId, GameObject vrmRoot, bool isLocal)
    {
        if (_avatars.ContainsKey(userId))
        {
            DestroyAvatar(userId);
        }

        int slot = _atlasManager != null ? _atlasManager.AllocateCharacterSlot() : -1;
        var instance = new AvatarInstance(userId, vrmRoot, slot);
        _avatars[userId] = instance;

        if (_nameTagPrefab != null && vrmRoot != null)
        {
            var tagGo = Instantiate(_nameTagPrefab, vrmRoot.transform);
            instance.SetNameTag(tagGo.GetComponent<NameTagController>());
        }

        if (isLocal)
            LocalAvatar = instance;
        else if (_hideList != null && _hideList.ShouldSkipRendering(userId))
            vrmRoot.SetActive(false);

        return instance;
    }

    /// <summary>
    /// アバターを破棄してスロットを解放する。
    /// </summary>
    public void DestroyAvatar(string userId)
    {
        if (!_avatars.TryGetValue(userId, out var instance))
            return;

        if (instance.CharacterSlot >= 0)
            _atlasManager?.ReleaseCharacterSlot(instance.CharacterSlot);

        foreach (var slot in instance.AccessorySlots)
            _atlasManager?.ReleaseAccessorySlot(slot);

        if (instance.Root != null)
            Destroy(instance.Root);

        _avatars.Remove(userId);

        if (LocalAvatar == instance)
            LocalAvatar = null;
    }

    public bool TryGetAvatar(string userId, out AvatarInstance instance) =>
        _avatars.TryGetValue(userId, out instance);

    public IReadOnlyDictionary<string, AvatarInstance> AllAvatars => _avatars;

    /// <summary>
    /// 非表示状態の変化をアバターの描画に反映する（HideListLogic から呼び出す）。
    /// </summary>
    public void ApplyHideState(string userId, bool shouldHide)
    {
        if (!_avatars.TryGetValue(userId, out var instance)) return;
        if (instance.Root != null)
            instance.Root.SetActive(!shouldHide);
    }

    /// <summary>
    /// アバターを拒否済みとしてマークし、アトラス上のキャラクタースロットを透明クリアする。
    /// モデレーションで rejected になったアバターの表示を消去するために呼ぶ。
    /// </summary>
    public void MarkAvatarRejected(string userId)
    {
        if (!_avatars.TryGetValue(userId, out var instance))
            return;

        instance.MarkRejected();

        if (instance.CharacterSlot >= 0)
            _atlasManager?.ClearCharacterSlot(instance.CharacterSlot);
    }

    /// <summary>
    /// 他プレイヤーのアバターを検疫中（pending）としてマークし、
    /// VRM メッシュを非表示にしてフォールバックアバターを表示する。
    /// _pendingFallbackPrefab が未設定の場合はカプセルプリミティブで代替する。
    /// </summary>
    public void MarkAvatarPending(string userId)
    {
        if (!_avatars.TryGetValue(userId, out var instance))
            return;

        instance.MarkPending();

        if (instance.Root == null)
            return;

        foreach (var r in instance.Root.GetComponentsInChildren<Renderer>(true))
            r.enabled = false;

        GameObject fallback;
        if (_pendingFallbackPrefab != null)
        {
            fallback = Instantiate(_pendingFallbackPrefab, instance.Root.transform);
        }
        else
        {
            fallback = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            fallback.transform.SetParent(instance.Root.transform, worldPositionStays: false);
            fallback.transform.localPosition = new Vector3(0f, 1f, 0f);
            fallback.transform.localScale = Vector3.one;
        }
    }

    /// <summary>
    /// アバターの名前タグに表示名と公認バッジを設定する。
    /// NameTagPrefab が設定されていない場合は何もしない。
    /// </summary>
    public void SetAvatarNameTag(string userId, string displayName, bool isVerified)
    {
        if (!_avatars.TryGetValue(userId, out var instance)) return;
        instance.NameTag?.SetNameTag(displayName, isVerified);
    }

    /// <summary>
    /// アバターの発話インジケーターを更新する。
    /// </summary>
    public void SetAvatarVoiceActive(string userId, bool active)
    {
        if (!_avatars.TryGetValue(userId, out var instance)) return;
        instance.NameTag?.SetVoiceActive(active);
    }
}

/// <summary>
/// アバター1体の状態を保持するデータクラス。
/// </summary>
public class AvatarInstance
{
    public const int MaxAccessories = 4;

    public string UserId { get; }
    public GameObject Root { get; }
    public int CharacterSlot { get; }

    /// <summary>モデレーションにより拒否されたアバターかどうか。</summary>
    public bool IsRejected { get; private set; }

    /// <summary>モデレーション検疫中（pending）のアバターかどうか。</summary>
    public bool IsPending { get; private set; }

    /// <summary>頭上の名前タグコントローラー。_nameTagPrefab 未設定時は null。</summary>
    public NameTagController NameTag { get; private set; }

    private readonly List<int> _accessorySlots = new();
    public IReadOnlyList<int> AccessorySlots => _accessorySlots;

    public AvatarInstance(string userId, GameObject root, int characterSlot)
    {
        UserId = userId;
        Root = root;
        CharacterSlot = characterSlot;
    }

    public bool CanAddAccessory => _accessorySlots.Count < MaxAccessories;

    public void AddAccessorySlot(int slot) => _accessorySlots.Add(slot);

    public void RemoveAccessorySlot(int slot) => _accessorySlots.Remove(slot);

    /// <summary>このアバターを拒否済みとしてマークする。</summary>
    public void MarkRejected() => IsRejected = true;

    /// <summary>このアバターを検疫中としてマークする。</summary>
    public void MarkPending() => IsPending = true;

    /// <summary>名前タグコントローラーを設定する（AvatarManager からのみ呼ぶ）。</summary>
    public void SetNameTag(NameTagController tag) => NameTag = tag;
}
