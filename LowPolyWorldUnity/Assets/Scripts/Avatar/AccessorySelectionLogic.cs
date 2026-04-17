using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// アバター編集画面のアクセサリスロット選択状態を管理する純粋 C# ロジッククラス。
/// </summary>
public class AccessorySelectionLogic
{
    public const int MaxSlots = 4;

    private readonly AccessorySlotData[] _slots;
    private int _selectedIndex = -1;

    public IReadOnlyList<AccessorySlotData> Slots => _slots;
    public int SelectedIndex => _selectedIndex;
    public AccessorySlotData SelectedSlot => HasSelection ? _slots[_selectedIndex] : null;

    /// <summary>選択中スロットにアクセサリが入っているか。</summary>
    public bool HasSelection => _selectedIndex >= 0 && _slots[_selectedIndex].IsOccupied;

    /// <summary>選択インデックスが変わったとき発火（-1 = 選択解除）。</summary>
    public event Action<int> OnSelectionChanged;

    /// <summary>スロット内容が変わったとき発火（スロットインデックスを渡す）。</summary>
    public event Action<int> OnSlotChanged;

    public AccessorySelectionLogic()
    {
        _slots = new AccessorySlotData[MaxSlots];
        for (int i = 0; i < MaxSlots; i++)
            _slots[i] = new AccessorySlotData(i);
    }

    /// <summary>
    /// スロットを選択する。同じインデックスを渡すと選択解除になる。
    /// </summary>
    public void Select(int index)
    {
        if (index < 0 || index >= MaxSlots) return;
        int next = (index == _selectedIndex) ? -1 : index;
        _selectedIndex = next;
        OnSelectionChanged?.Invoke(_selectedIndex);
    }

    /// <summary>選択を解除する。</summary>
    public void Deselect()
    {
        if (_selectedIndex == -1) return;
        _selectedIndex = -1;
        OnSelectionChanged?.Invoke(-1);
    }

    /// <summary>
    /// 空きスロットにアクセサリを追加する。
    /// </summary>
    /// <param name="fileId">アクセサリファイル識別子。</param>
    /// <param name="bone">初期アタッチボーン。</param>
    /// <param name="slotIndex">追加されたスロットのインデックス。失敗時は -1。</param>
    /// <returns>追加に成功した場合 true。満杯の場合 false。</returns>
    public bool TryAddAccessory(string fileId, HumanBodyBones bone, out int slotIndex)
    {
        slotIndex = -1;
        for (int i = 0; i < MaxSlots; i++)
        {
            if (!_slots[i].IsOccupied)
            {
                slotIndex = i;
                break;
            }
        }
        if (slotIndex < 0) return false;

        _slots[slotIndex].Set(fileId, bone);
        OnSlotChanged?.Invoke(slotIndex);
        return true;
    }

    /// <summary>選択中スロットのアクセサリを削除する。</summary>
    public void RemoveSelected()
    {
        if (!HasSelection) return;
        int index = _selectedIndex;
        _slots[index].Clear();
        _selectedIndex = -1;
        OnSlotChanged?.Invoke(index);
        OnSelectionChanged?.Invoke(-1);
    }

    /// <summary>選択中スロットのボーンを変更する。</summary>
    public void ChangeSelectedBone(HumanBodyBones bone)
    {
        if (!HasSelection) return;
        _slots[_selectedIndex].Bone = bone;
        OnSlotChanged?.Invoke(_selectedIndex);
    }

    /// <summary>選択中スロットのローカル変換を設定する（ギズモ操作結果の反映用）。</summary>
    public void SetSelectedTransform(Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
    {
        if (!HasSelection) return;
        _slots[_selectedIndex].LocalPosition = localPosition;
        _slots[_selectedIndex].LocalRotation = localRotation;
        _slots[_selectedIndex].LocalScale = localScale;
        OnSlotChanged?.Invoke(_selectedIndex);
    }

    /// <summary>使用中スロット数。</summary>
    public int UsedSlotCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < MaxSlots; i++)
                if (_slots[i].IsOccupied) count++;
            return count;
        }
    }

    /// <summary>アクセサリを追加できるか（スロットに空きがあるか）。</summary>
    public bool CanAddAccessory => UsedSlotCount < MaxSlots;
}

/// <summary>
/// アクセサリスロット 1 枠のデータ（純粋データクラス）。
/// </summary>
public class AccessorySlotData
{
    public int Index { get; }
    public string FileId { get; private set; }
    public HumanBodyBones Bone { get; set; }
    public Vector3 LocalPosition { get; set; }
    public Quaternion LocalRotation { get; set; }
    public Vector3 LocalScale { get; set; }

    /// <summary>アクセサリがセットされているか。</summary>
    public bool IsOccupied => FileId != null;

    public AccessorySlotData(int index)
    {
        Index = index;
        LocalScale = Vector3.one;
        LocalRotation = Quaternion.identity;
    }

    internal void Set(string fileId, HumanBodyBones bone)
    {
        FileId = fileId;
        Bone = bone;
        LocalPosition = Vector3.zero;
        LocalRotation = Quaternion.identity;
        LocalScale = Vector3.one;
    }

    internal void Clear()
    {
        FileId = null;
    }
}
