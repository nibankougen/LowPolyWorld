using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 1024×2048 テクスチャアトラスの生成・更新を行う MonoBehaviour。
/// AtlasLayout でスロット管理、RenderTexture で非同期合成する。
/// </summary>
public class AtlasManager : MonoBehaviour
{
    public static AtlasManager Instance { get; private set; }

    /// <summary>生成済みアトラステクスチャ。他コンポーネントから参照する。</summary>
    public RenderTexture AtlasTexture { get; private set; }

    /// <summary>アトラス更新完了イベント。</summary>
    public event Action OnAtlasUpdated;

    private readonly AtlasLayout _layout = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        AtlasTexture = new RenderTexture(
            AtlasLayout.AtlasWidth,
            AtlasLayout.AtlasHeight,
            0,
            RenderTextureFormat.ARGB32
        )
        {
            name = "AvatarAtlas",
            filterMode = FilterMode.Bilinear,
            useMipMap = true,
            autoGenerateMips = false,
        };
        AtlasTexture.Create();

        // 初期クリア（透明）
        var prev = RenderTexture.active;
        RenderTexture.active = AtlasTexture;
        GL.Clear(true, true, Color.clear);
        RenderTexture.active = prev;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        AtlasTexture?.Release();
    }

    // ---- スロット管理（AvatarManager から呼ぶ） ----

    public int AllocateCharacterSlot() => _layout.AllocateCharacterSlot();
    public void ReleaseCharacterSlot(int slot) => _layout.ReleaseCharacterSlot(slot);
    public int AllocateAccessorySlot() => _layout.AllocateAccessorySlot();
    public void ReleaseAccessorySlot(int slot) => _layout.ReleaseAccessorySlot(slot);

    public Rect GetCharacterUV(int slot) => _layout.GetCharacterUV(slot);
    public Rect GetAccessoryUV(int slot) => _layout.GetAccessoryUV(slot);

    // ---- テクスチャ書き込み ----

    /// <summary>
    /// キャラクタースロットにテクスチャを書き込む。
    /// 書き込み後に ScheduleAtlasUpdate を呼ぶこと。
    /// </summary>
    public void WriteCharacterTexture(int slot, Texture2D texture)
    {
        if (texture == null) return;
        var px = _layout.GetCharacterPixelRect(slot);
        BlitToAtlas(texture, px);
    }

    /// <summary>
    /// アクセサリスロットにテクスチャを書き込む。
    /// </summary>
    public void WriteAccessoryTexture(int slot, Texture2D texture)
    {
        if (texture == null) return;
        var px = _layout.GetAccessoryPixelRect(slot);
        BlitToAtlas(texture, px);
    }

    /// <summary>ミップマップ再生成とイベント通知を行う。</summary>
    public void ScheduleAtlasUpdate()
    {
        StartCoroutine(UpdateAtlasCoroutine());
    }

    private IEnumerator UpdateAtlasCoroutine()
    {
        // エンドオブフレームまで待機してからミップマップ生成
        yield return new WaitForEndOfFrame();
        AtlasTexture.GenerateMips();
        OnAtlasUpdated?.Invoke();
    }

    private void BlitToAtlas(Texture src, RectInt dstPixel)
    {
        var mat = new Material(Shader.Find("Hidden/BlitCopy"));
        var prev = RenderTexture.active;
        RenderTexture.active = AtlasTexture;

        GL.PushMatrix();
        GL.LoadPixelMatrix(0, AtlasLayout.AtlasWidth, 0, AtlasLayout.AtlasHeight);

        Graphics.DrawTexture(
            new Rect(dstPixel.x, dstPixel.y, dstPixel.width, dstPixel.height),
            src
        );

        GL.PopMatrix();
        RenderTexture.active = prev;
        Destroy(mat);
    }
}
