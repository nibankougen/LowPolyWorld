// LPWSavePhoto.mm
// iOS ネイティブプラグイン: PNG バイト列を写真ライブラリへ保存する。
// Unity 側から PhotoSaver.cs が P/Invoke で呼び出す。
// NSPhotoLibraryAddUsageDescription を Info.plist に追加すること。

#import <Photos/Photos.h>
#import <UIKit/UIKit.h>

extern "C" {

void _LPWSavePhotoToLibrary(const unsigned char* data, int length) {
    if (!data || length <= 0) return;

    NSData* imageData = [NSData dataWithBytes:data length:(NSUInteger)length];
    UIImage* image = [UIImage imageWithData:imageData];
    if (!image) return;

    [PHPhotoLibrary requestAuthorizationForAccessLevel:PHAccessLevelAddOnly
        handler:^(PHAuthorizationStatus status) {
            if (status == PHAuthorizationStatusAuthorized ||
                status == PHAuthorizationStatusLimited) {
                dispatch_async(dispatch_get_main_queue(), ^{
                    UIImageWriteToSavedPhotosAlbum(image, nil, nil, nil);
                });
            }
        }];
}

} // extern "C"
