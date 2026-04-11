using NUnit.Framework;
using UnityEngine;

public class AtlasLayoutTests
{
    // ---- キャラクタースロット割り当て ----

    [Test]
    public void AllocateCharacterSlot_FirstCall_ReturnsZero()
    {
        var layout = new AtlasLayout();
        Assert.AreEqual(0, layout.AllocateCharacterSlot());
    }

    [Test]
    public void AllocateCharacterSlot_Sequential_ReturnsIncreasing()
    {
        var layout = new AtlasLayout();
        for (int i = 0; i < AtlasLayout.CharacterSlotCount; i++)
            Assert.AreEqual(i, layout.AllocateCharacterSlot());
    }

    [Test]
    public void AllocateCharacterSlot_WhenFull_ReturnsMinusOne()
    {
        var layout = new AtlasLayout();
        for (int i = 0; i < AtlasLayout.CharacterSlotCount; i++)
            layout.AllocateCharacterSlot();

        Assert.AreEqual(-1, layout.AllocateCharacterSlot());
    }

    [Test]
    public void ReleaseCharacterSlot_ThenAllocate_ReusesSlot()
    {
        var layout = new AtlasLayout();
        for (int i = 0; i < AtlasLayout.CharacterSlotCount; i++)
            layout.AllocateCharacterSlot();

        layout.ReleaseCharacterSlot(5);
        Assert.AreEqual(5, layout.AllocateCharacterSlot());
    }

    [Test]
    public void CharacterSlotCount_Is24()
    {
        Assert.AreEqual(24, AtlasLayout.CharacterSlotCount);
    }

    // ---- アクセサリスロット割り当て ----

    [Test]
    public void AllocateAccessorySlot_FirstCall_ReturnsZero()
    {
        var layout = new AtlasLayout();
        Assert.AreEqual(0, layout.AllocateAccessorySlot());
    }

    [Test]
    public void AllocateAccessorySlot_WhenFull_ReturnsMinusOne()
    {
        var layout = new AtlasLayout();
        for (int i = 0; i < AtlasLayout.AccessorySlotCount; i++)
            layout.AllocateAccessorySlot();

        Assert.AreEqual(-1, layout.AllocateAccessorySlot());
    }

    [Test]
    public void ReleaseAccessorySlot_ThenAllocate_ReusesSlot()
    {
        var layout = new AtlasLayout();
        for (int i = 0; i < AtlasLayout.AccessorySlotCount; i++)
            layout.AllocateAccessorySlot();

        layout.ReleaseAccessorySlot(10);
        Assert.AreEqual(10, layout.AllocateAccessorySlot());
    }

    [Test]
    public void AccessorySlotCount_Is96()
    {
        Assert.AreEqual(96, AtlasLayout.AccessorySlotCount);
    }

    // ---- キャラクターピクセル矩形 ----

    [Test]
    public void GetCharacterPixelRect_Slot0_CorrectPosition()
    {
        var layout = new AtlasLayout();
        var rect = layout.GetCharacterPixelRect(0);

        Assert.AreEqual(AtlasLayout.SlotPadding, rect.x);
        Assert.AreEqual(AtlasLayout.SlotPadding, rect.y);
    }

    [Test]
    public void GetCharacterPixelRect_SlotSize_ExcludesPadding()
    {
        var layout = new AtlasLayout();
        var rect = layout.GetCharacterPixelRect(0);
        int expected = AtlasLayout.CharacterSlotSize - AtlasLayout.SlotPadding * 2;

        Assert.AreEqual(expected, rect.width);
        Assert.AreEqual(expected, rect.height);
    }

    [Test]
    public void GetCharacterPixelRect_Slot1_CorrectColumn()
    {
        var layout = new AtlasLayout();
        var rect = layout.GetCharacterPixelRect(1);
        int expectedX = AtlasLayout.CharacterSlotSize + AtlasLayout.SlotPadding;

        Assert.AreEqual(expectedX, rect.x);
        Assert.AreEqual(AtlasLayout.SlotPadding, rect.y);
    }

    [Test]
    public void GetCharacterPixelRect_Slot4_SecondRow()
    {
        var layout = new AtlasLayout();
        var rect = layout.GetCharacterPixelRect(4); // 列0・行1
        int expectedY = AtlasLayout.CharacterSlotSize + AtlasLayout.SlotPadding;

        Assert.AreEqual(AtlasLayout.SlotPadding, rect.x);
        Assert.AreEqual(expectedY, rect.y);
    }

    // ---- アクセサリピクセル矩形 ----

    [Test]
    public void GetAccessoryPixelRect_Slot0_StartsAtAccessoryRegionY()
    {
        var layout = new AtlasLayout();
        var rect = layout.GetAccessoryPixelRect(0);
        int expectedY = AtlasLayout.AccessoryRegionY + AtlasLayout.SlotPadding;

        Assert.AreEqual(AtlasLayout.SlotPadding, rect.x);
        Assert.AreEqual(expectedY, rect.y);
    }

    [Test]
    public void GetAccessoryPixelRect_SlotSize_ExcludesPadding()
    {
        var layout = new AtlasLayout();
        var rect = layout.GetAccessoryPixelRect(0);
        int expected = AtlasLayout.AccessorySlotSize - AtlasLayout.SlotPadding * 2;

        Assert.AreEqual(expected, rect.width);
        Assert.AreEqual(expected, rect.height);
    }

    [Test]
    public void GetAccessoryRegionY_Is1536()
    {
        Assert.AreEqual(1536, AtlasLayout.AccessoryRegionY);
    }

    // ---- UV 座標 ----

    [Test]
    public void GetCharacterUV_Slot0_XStartsAtPaddingRatio()
    {
        var layout = new AtlasLayout();
        var uv = layout.GetCharacterUV(0);
        float expectedX = (float)AtlasLayout.SlotPadding / AtlasLayout.AtlasWidth;

        Assert.That(uv.x, Is.EqualTo(expectedX).Within(0.0001f));
    }

    [Test]
    public void GetCharacterUV_Width_ExcludesPadding()
    {
        var layout = new AtlasLayout();
        var uv = layout.GetCharacterUV(0);
        float expected = (float)(AtlasLayout.CharacterSlotSize - AtlasLayout.SlotPadding * 2)
            / AtlasLayout.AtlasWidth;

        Assert.That(uv.width, Is.EqualTo(expected).Within(0.0001f));
    }

    [Test]
    public void GetAccessoryUV_Width_ExcludesPadding()
    {
        var layout = new AtlasLayout();
        var uv = layout.GetAccessoryUV(0);
        float expected = (float)(AtlasLayout.AccessorySlotSize - AtlasLayout.SlotPadding * 2)
            / AtlasLayout.AtlasWidth;

        Assert.That(uv.width, Is.EqualTo(expected).Within(0.0001f));
    }

    [Test]
    public void GetCharacterUV_AllWithinZeroToOne()
    {
        var layout = new AtlasLayout();
        for (int i = 0; i < AtlasLayout.CharacterSlotCount; i++)
        {
            var uv = layout.GetCharacterUV(i);
            Assert.That(uv.x, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f), $"slot {i} uv.x");
            Assert.That(uv.y, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f), $"slot {i} uv.y");
            Assert.That(uv.xMax, Is.LessThanOrEqualTo(1f), $"slot {i} uv.xMax");
            Assert.That(uv.yMax, Is.LessThanOrEqualTo(1f), $"slot {i} uv.yMax");
        }
    }

    [Test]
    public void GetAccessoryUV_AllWithinZeroToOne()
    {
        var layout = new AtlasLayout();
        for (int i = 0; i < AtlasLayout.AccessorySlotCount; i++)
        {
            var uv = layout.GetAccessoryUV(i);
            Assert.That(uv.x, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f), $"slot {i} uv.x");
            Assert.That(uv.y, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f), $"slot {i} uv.y");
            Assert.That(uv.xMax, Is.LessThanOrEqualTo(1f), $"slot {i} uv.xMax");
            Assert.That(uv.yMax, Is.LessThanOrEqualTo(1f), $"slot {i} uv.yMax");
        }
    }
}
