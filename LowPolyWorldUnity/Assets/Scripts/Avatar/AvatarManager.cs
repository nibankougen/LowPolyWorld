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

    private AtlasManager _atlasManager;

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
        if (_avatars.TryGetValue(userId, out var existing))
        {
            DestroyAvatar(userId);
        }

        int slot = _atlasManager != null ? _atlasManager.AllocateCharacterSlot() : -1;
        var instance = new AvatarInstance(userId, vrmRoot, slot);
        _avatars[userId] = instance;

        if (isLocal)
            LocalAvatar = instance;

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
}
