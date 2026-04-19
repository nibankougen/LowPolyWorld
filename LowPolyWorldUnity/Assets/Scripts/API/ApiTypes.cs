using System;
using System.Collections.Generic;

/// <summary>
/// API レスポンスの DTO 定義。すべてシリアライズ可能な純粋 C# クラス。
/// </summary>

[Serializable]
public class ApiEnvelope<T>
{
    public T data;
}

[Serializable]
public class ApiError
{
    public ApiErrorBody error;
}

[Serializable]
public class ApiErrorBody
{
    public string code;
    public string message;
}

[Serializable]
public class ApiVersionResponse
{
    public int min_compatible_version;
    public int latest_version;
}

[Serializable]
public class TokenResponse
{
    public string access_token;
    public string refresh_token;
    public int expires_in;
    public bool name_setup_required;
    public string user_id;
}

[Serializable]
public class RefreshRequest
{
    public string refresh_token;
}

[Serializable]
public class PlanCapabilities
{
    public int max_avatars;
    public int max_accessories;
    public int max_players_limit;
    public int saved_world_object_variants;
    public bool name_change;
    public bool invite_room_create;
}

[Serializable]
public class StartupUserProfile
{
    public string id;
    public string displayName;
    public string name;
    public bool nameSetupRequired;
    public string language;
    public string subscriptionTier;
    public string vivoxId;
    public string createdAt;
}

[Serializable]
public class StartupAvatar
{
    public string id;
    public string name;
    public string vrmUrl;
    public string vrmHash;
    public string textureUrl;
    public string textureHash;
    public string moderationStatus;
    public string createdAt;
}

[Serializable]
public class WorldResponse
{
    public string id;
    public string name;
    public string description;
    public string thumbnailUrl;
    public string glbUrl;
    public bool isPublic;
    public int maxPlayers;
    public int likesCount;
    public string createdAt;
}

[Serializable]
public class WorldListResponse
{
    public List<WorldResponse> data;
    public CursorResponse cursor;
}

[Serializable]
public class CursorResponse
{
    public string next;
    public bool hasMore;
}

[Serializable]
public class StartupResponse
{
    public StartupUserProfile user;
    public PlanCapabilities planCapabilities;
    public string securityNotice;
    public List<StartupAvatar> avatars;
    public List<WorldResponse> worlds;
}

[Serializable]
public class RoomResponse
{
    public string id;
    public string worldId;
    public string roomType;
    public string language;
    public int maxPlayers;
    public int currentPlayers;
    public string createdAt;
}

[Serializable]
public class RecommendedJoinResponse
{
    public string action; // "join" | "create" | "confirm_english"
    public string roomId;
    public string language;
}

[Serializable]
public class AvatarResponse
{
    public string id;
    public string name;
    public string vrmUrl;
    public string vrmHash;
    public string textureUrl;
    public string textureHash;
    public string moderationStatus;
    public string createdAt;
}

[Serializable]
public class AccessoryResponse
{
    public string id;
    public string name;
    public string glbUrl;
    public string glbHash;
    public string textureUrl;
    public string textureHash;
    public string createdAt;
}

[Serializable]
public class SetNameRequest
{
    public string name;
}

[Serializable]
public class SetLanguageRequest
{
    public string language;
}

[Serializable]
public class OAuthCallbackRequest
{
    public string id_token;
}

[Serializable]
public class CreateRoomRequest
{
    public string room_type;
    public string language;
}

// ── Shop ─────────────────────────────────────────────────────────────────────

[Serializable]
public class ShopProductResponse
{
    public string id;
    public string creatorId;
    public string name;
    public string description;
    public string category; // avatar | accessory | world_object | stamp
    public int priceCoins;
    public string assetUrl;
    public string assetHash;
    public string thumbnailUrl;
    public string thumbnailHash;
    public int textureCost;           // 0 = not applicable
    public string colliderSizeCategory; // null = not applicable
    public bool editAllowed;
    public int likesCount;
    public bool likedByMe;
    public bool purchasedByMe;
    public string[] tags;
    public string createdAt;
}

[Serializable]
public class ShopProductListResponse
{
    public List<ShopProductResponse> products;
    public CursorResponse cursor;
}

[Serializable]
public class CoinLotResponse
{
    public string purchaseId;
    public int coinsAmount;
    public string validUntil; // ISO 8601
}

[Serializable]
public class CoinBalanceResponse
{
    public int balance;
    public List<CoinLotResponse> lots;
    public int totalDeducted;
    public int totalSpent;
}

[Serializable]
public class PurchaseProductRequest
{
    public string idempotency_key;
}

[Serializable]
public class MyProductEntry
{
    public string id;
    public string productId;
    public string purchasedAt;
    public ShopProductResponse product;
}

[Serializable]
public class MyProductListResponse
{
    public List<MyProductEntry> products;
    public CursorResponse cursor;
}

[Serializable]
public class RecordCoinPurchaseRequest
{
    public string platform;             // "ios" | "android"
    public string platform_transaction_id;
    public string storefront_country;
    public string purchase_timestamp;   // ISO 8601
    public string valid_until;          // ISO 8601
    public int coins_amount;
    public float local_amount;
    public string local_currency;       // ISO 4217
    public float fx_rate_to_jpy;
}
