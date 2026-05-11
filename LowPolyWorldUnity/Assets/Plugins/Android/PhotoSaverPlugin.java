// PhotoSaverPlugin.java
// Android ネイティブプラグイン: PNG バイト列を MediaStore 経由でカメラロールへ保存する。
// Unity 側から PhotoSaver.cs が AndroidJavaClass 経由で呼び出す。
// AndroidManifest に WRITE_EXTERNAL_STORAGE 権限 (API < 29) が必要。

package com.nibankougen.lowpolyworld;

import android.content.ContentValues;
import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.net.Uri;
import android.os.Build;
import android.provider.MediaStore;
import com.unity3d.player.UnityPlayer;

import java.io.OutputStream;

public class PhotoSaverPlugin {

    public static void saveToGallery(byte[] pngData, String albumName, String fileName) {
        if (pngData == null || pngData.length == 0) return;

        Context ctx = UnityPlayer.currentActivity.getApplicationContext();
        Bitmap bmp = BitmapFactory.decodeByteArray(pngData, 0, pngData.length);
        if (bmp == null) return;

        ContentValues cv = new ContentValues();
        cv.put(MediaStore.Images.Media.DISPLAY_NAME, fileName + ".png");
        cv.put(MediaStore.Images.Media.MIME_TYPE, "image/png");
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            cv.put(MediaStore.Images.Media.RELATIVE_PATH, "Pictures/" + albumName);
            cv.put(MediaStore.Images.Media.IS_PENDING, 1);
        }

        Uri uri = ctx.getContentResolver().insert(MediaStore.Images.Media.EXTERNAL_CONTENT_URI, cv);
        if (uri == null) return;

        try (OutputStream os = ctx.getContentResolver().openOutputStream(uri)) {
            bmp.compress(Bitmap.CompressFormat.PNG, 100, os);
        } catch (Exception e) {
            // silent fail — caller shows a generic error
        } finally {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
                cv.clear();
                cv.put(MediaStore.Images.Media.IS_PENDING, 0);
                ctx.getContentResolver().update(uri, cv, null, null);
            }
        }
    }
}
