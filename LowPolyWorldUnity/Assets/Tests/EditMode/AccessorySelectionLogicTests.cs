using NUnit.Framework;
using UnityEngine;

public class AccessorySelectionLogicTests
{
    private AccessorySelectionLogic _logic;

    [SetUp]
    public void SetUp()
    {
        _logic = new AccessorySelectionLogic();
    }

    // ---- 初期状態 ----

    [Test]
    public void InitialState_NoSelection_NoSlotOccupied()
    {
        Assert.AreEqual(-1, _logic.SelectedIndex);
        Assert.IsFalse(_logic.HasSelection);
        Assert.AreEqual(0, _logic.UsedSlotCount);
        Assert.IsTrue(_logic.CanAddAccessory);
    }

    [Test]
    public void InitialState_AllSlotsEmpty()
    {
        for (int i = 0; i < AccessorySelectionLogic.MaxSlots; i++)
            Assert.IsFalse(_logic.Slots[i].IsOccupied);
    }

    // ---- TryAddAccessory ----

    [Test]
    public void TryAddAccessory_EmptySlot_SucceedsAndOccupiesSlot0()
    {
        bool ok = _logic.TryAddAccessory("acc_hat", HumanBodyBones.Head, out int slot);

        Assert.IsTrue(ok);
        Assert.AreEqual(0, slot);
        Assert.IsTrue(_logic.Slots[0].IsOccupied);
        Assert.AreEqual("acc_hat", _logic.Slots[0].FileId);
        Assert.AreEqual(HumanBodyBones.Head, _logic.Slots[0].Bone);
    }

    [Test]
    public void TryAddAccessory_MultipleItems_FillsSlotsInOrder()
    {
        _logic.TryAddAccessory("a", HumanBodyBones.Head, out _);
        _logic.TryAddAccessory("b", HumanBodyBones.Chest, out _);
        _logic.TryAddAccessory("c", HumanBodyBones.LeftLowerArm, out _);
        _logic.TryAddAccessory("d", HumanBodyBones.RightLowerArm, out _);

        Assert.AreEqual(4, _logic.UsedSlotCount);
        Assert.IsFalse(_logic.CanAddAccessory);
    }

    [Test]
    public void TryAddAccessory_WhenFull_ReturnsFalse()
    {
        for (int i = 0; i < AccessorySelectionLogic.MaxSlots; i++)
            _logic.TryAddAccessory($"acc_{i}", HumanBodyBones.Head, out _);

        bool ok = _logic.TryAddAccessory("extra", HumanBodyBones.Chest, out int slot);

        Assert.IsFalse(ok);
        Assert.AreEqual(-1, slot);
    }

    [Test]
    public void TryAddAccessory_FiresOnSlotChanged()
    {
        int firedSlot = -99;
        _logic.OnSlotChanged += s => firedSlot = s;

        _logic.TryAddAccessory("acc_hat", HumanBodyBones.Head, out int slot);

        Assert.AreEqual(slot, firedSlot);
    }

    // ---- Select / Deselect ----

    [Test]
    public void Select_OccupiedSlot_SetsSelectedIndex()
    {
        _logic.TryAddAccessory("acc_hat", HumanBodyBones.Head, out _);
        _logic.Select(0);

        Assert.AreEqual(0, _logic.SelectedIndex);
        Assert.IsTrue(_logic.HasSelection);
    }

    [Test]
    public void Select_SameSlotTwice_Deselects()
    {
        _logic.TryAddAccessory("acc_hat", HumanBodyBones.Head, out _);
        _logic.Select(0);
        _logic.Select(0);

        Assert.AreEqual(-1, _logic.SelectedIndex);
        Assert.IsFalse(_logic.HasSelection);
    }

    [Test]
    public void Select_EmptySlot_IndexSetButHasSelectionFalse()
    {
        _logic.Select(0);

        Assert.AreEqual(0, _logic.SelectedIndex);
        Assert.IsFalse(_logic.HasSelection);
    }

    [Test]
    public void Select_FiresOnSelectionChanged()
    {
        _logic.TryAddAccessory("acc_hat", HumanBodyBones.Head, out _);
        int fired = -99;
        _logic.OnSelectionChanged += i => fired = i;

        _logic.Select(0);

        Assert.AreEqual(0, fired);
    }

    [Test]
    public void Deselect_WhenSelected_ClearsSelection()
    {
        _logic.TryAddAccessory("acc_hat", HumanBodyBones.Head, out _);
        _logic.Select(0);
        _logic.Deselect();

        Assert.AreEqual(-1, _logic.SelectedIndex);
    }

    [Test]
    public void Deselect_WhenNotSelected_NoEvent()
    {
        int callCount = 0;
        _logic.OnSelectionChanged += _ => callCount++;

        _logic.Deselect();

        Assert.AreEqual(0, callCount);
    }

    // ---- RemoveSelected ----

    [Test]
    public void RemoveSelected_WhenSelected_ClearsSlotAndDeselects()
    {
        _logic.TryAddAccessory("acc_hat", HumanBodyBones.Head, out _);
        _logic.Select(0);
        _logic.RemoveSelected();

        Assert.IsFalse(_logic.Slots[0].IsOccupied);
        Assert.AreEqual(-1, _logic.SelectedIndex);
        Assert.IsFalse(_logic.HasSelection);
    }

    [Test]
    public void RemoveSelected_WhenNoSelection_DoesNothing()
    {
        _logic.TryAddAccessory("acc_hat", HumanBodyBones.Head, out _);
        int callCount = 0;
        _logic.OnSlotChanged += _ => callCount++;

        _logic.RemoveSelected();

        Assert.AreEqual(0, callCount);
        Assert.IsTrue(_logic.Slots[0].IsOccupied);
    }

    [Test]
    public void RemoveSelected_ReleasedSlotCanBeReused()
    {
        _logic.TryAddAccessory("acc_hat", HumanBodyBones.Head, out _);
        _logic.Select(0);
        _logic.RemoveSelected();

        bool ok = _logic.TryAddAccessory("acc_sword", HumanBodyBones.RightLowerArm, out int slot);

        Assert.IsTrue(ok);
        Assert.AreEqual(0, slot);
    }

    // ---- ChangeSelectedBone ----

    [Test]
    public void ChangeSelectedBone_WhenSelected_UpdatesBone()
    {
        _logic.TryAddAccessory("acc_hat", HumanBodyBones.Head, out _);
        _logic.Select(0);
        _logic.ChangeSelectedBone(HumanBodyBones.Chest);

        Assert.AreEqual(HumanBodyBones.Chest, _logic.Slots[0].Bone);
    }

    [Test]
    public void ChangeSelectedBone_WhenNoSelection_DoesNothing()
    {
        _logic.TryAddAccessory("acc_hat", HumanBodyBones.Head, out _);
        int callCount = 0;
        _logic.OnSlotChanged += _ => callCount++;

        _logic.ChangeSelectedBone(HumanBodyBones.Chest);

        Assert.AreEqual(0, callCount);
    }

    // ---- SetSelectedTransform ----

    [Test]
    public void SetSelectedTransform_WhenSelected_UpdatesTransform()
    {
        _logic.TryAddAccessory("acc_hat", HumanBodyBones.Head, out _);
        _logic.Select(0);

        var pos = new Vector3(0.1f, 0.2f, 0.0f);
        var rot = Quaternion.Euler(0, 45, 0);
        var scale = new Vector3(1.5f, 1.5f, 1.5f);
        _logic.SetSelectedTransform(pos, rot, scale);

        Assert.AreEqual(pos, _logic.Slots[0].LocalPosition);
        Assert.AreEqual(rot, _logic.Slots[0].LocalRotation);
        Assert.AreEqual(scale, _logic.Slots[0].LocalScale);
    }

    [Test]
    public void SetSelectedTransform_WhenNoSelection_DoesNothing()
    {
        int callCount = 0;
        _logic.OnSlotChanged += _ => callCount++;

        _logic.SetSelectedTransform(Vector3.one, Quaternion.identity, Vector3.one);

        Assert.AreEqual(0, callCount);
    }

    // ---- SelectedSlot プロパティ ----

    [Test]
    public void SelectedSlot_WhenNoSelection_ReturnsNull()
    {
        Assert.IsNull(_logic.SelectedSlot);
    }

    [Test]
    public void SelectedSlot_WhenSelected_ReturnsSlotData()
    {
        _logic.TryAddAccessory("acc_hat", HumanBodyBones.Head, out _);
        _logic.Select(0);

        Assert.IsNotNull(_logic.SelectedSlot);
        Assert.AreEqual("acc_hat", _logic.SelectedSlot.FileId);
    }
}
