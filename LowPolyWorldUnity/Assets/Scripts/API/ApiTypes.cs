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
