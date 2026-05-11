using System;
using System.IO;
using UnityEngine;

#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

/// <summary>
/// プラットフォームごとの写真保存処理を抽象化するユーティリティクラス。
/// iOS: LPWSavePhoto.mm ネイティブプラグイン (P/Invoke)
/// Android: PhotoSaverPlugin.java (AndroidJavaClass)
/// Editor: デスクトップの PNG ファイルに書き出す（動作確認用）
/// </summary>
public static class PhotoSaver
{
#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void _LPWSavePhotoToLibrary(byte[] data, int length);
#endif

    private const string AlbumName = "LowPolyWorld";

    /// <summary>
    /// テクスチャをカメラロールに PNG として保存する。
    /// 非同期で処理されるため、失敗時は警告ログのみ出力する。
    /// </summary>
    public static void SaveToGallery(Texture2D tex)
    {
        if (tex == null) return;
        var png = tex.EncodeToPNG();
        var fileName = $"LPW_{DateTime.Now:yyyyMMdd_HHmmss}";

#if UNITY_IOS && !UNITY_EDITOR
        _LPWSavePhotoToLibrary(png, png.Length);
#elif UNITY_ANDROID && !UNITY_EDITOR
        SaveAndroid(png, AlbumName, fileName);
#else
        SaveEditor(png, fileName);
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private static void SaveAndroid(byte[] png, string album, string fileName)
    {
        try
        {
            using var cls = new AndroidJavaClass("com.nibankougen.lowpolyworld.PhotoSaverPlugin");
            cls.CallStatic("saveToGallery", png, album, fileName);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PhotoSaver] Android save failed: {e.Message}");
        }
    }
#endif

    private static void SaveEditor(byte[] png, string fileName)
    {
        try
        {
            var dir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var path = Path.Combine(dir, fileName + ".png");
            File.WriteAllBytes(path, png);
            Debug.Log($"[PhotoSaver] Saved to {path}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PhotoSaver] Editor save failed: {e.Message}");
        }
    }
}
