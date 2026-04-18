using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// REST API との HTTP 通信を担当するクライアント。
/// UserManager からアクセストークンを受け取り Authorization ヘッダーに付与する。
/// </summary>
public class ApiClient
{
    private readonly string _baseUrl;
    private readonly Func<string> _accessTokenProvider;

    public ApiClient(string baseUrl, Func<string> accessTokenProvider = null)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _accessTokenProvider = accessTokenProvider;
    }

    // ── GET ─────────────────────────────────────────────────────────────────

    public async Task<(T result, string error)> GetAsync<T>(string path, CancellationToken ct = default)
    {
        using var req = UnityWebRequest.Get(_baseUrl + path);
        AddAuthHeader(req);
        await SendAsync(req, ct);
        return ParseResponse<T>(req);
    }

    // ── POST (JSON) ──────────────────────────────────────────────────────────

    public async Task<(T result, string error)> PostJsonAsync<T>(
        string path,
        object body,
        CancellationToken ct = default
    )
    {
        using var req = BuildJsonRequest("POST", _baseUrl + path, body);
        AddAuthHeader(req);
        await SendAsync(req, ct);
        return ParseResponse<T>(req);
    }

    public async Task<string> PostJsonNoBodyAsync(string path, object body, CancellationToken ct = default)
    {
        using var req = BuildJsonRequest("POST", _baseUrl + path, body);
        AddAuthHeader(req);
        await SendAsync(req, ct);
        return IsSuccess(req.responseCode) ? null : ExtractError(req);
    }

    // ── POST (multipart) ─────────────────────────────────────────────────────

    public async Task<(T result, string error)> PostMultipartAsync<T>(
        string path,
        List<IMultipartFormSection> sections,
        CancellationToken ct = default
    )
    {
        using var req = UnityWebRequest.Post(_baseUrl + path, sections);
        AddAuthHeader(req);
        await SendAsync(req, ct);
        return ParseResponse<T>(req);
    }

    // ── PUT (JSON) ───────────────────────────────────────────────────────────

    public async Task<(T result, string error)> PutJsonAsync<T>(
        string path,
        object body,
        CancellationToken ct = default
    )
    {
        using var req = BuildJsonRequest("PUT", _baseUrl + path, body);
        AddAuthHeader(req);
        await SendAsync(req, ct);
        return ParseResponse<T>(req);
    }

    // ── PATCH (JSON) ─────────────────────────────────────────────────────────

    public async Task<(T result, string error)> PatchJsonAsync<T>(
        string path,
        object body,
        CancellationToken ct = default
    )
    {
        using var req = BuildJsonRequest("PATCH", _baseUrl + path, body);
        AddAuthHeader(req);
        await SendAsync(req, ct);
        return ParseResponse<T>(req);
    }

    // ── DELETE ───────────────────────────────────────────────────────────────

    public async Task<string> DeleteAsync(string path, CancellationToken ct = default)
    {
        using var req = UnityWebRequest.Delete(_baseUrl + path);
        req.downloadHandler = new DownloadHandlerBuffer();
        AddAuthHeader(req);
        await SendAsync(req, ct);
        return IsSuccess(req.responseCode) ? null : ExtractError(req);
    }

    // ── Byte download ─────────────────────────────────────────────────────────

    public async Task<(byte[] data, string error)> GetBytesAsync(
        string url,
        CancellationToken ct = default
    )
    {
        using var req = UnityWebRequest.Get(url);
        await SendAsync(req, ct);
        if (!IsSuccess(req.responseCode))
            return (null, $"HTTP {req.responseCode}");
        return (req.downloadHandler.data, null);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static UnityWebRequest BuildJsonRequest(string method, string url, object body)
    {
        var json = body != null ? JsonUtility.ToJson(body) : "{}";
        var bytes = Encoding.UTF8.GetBytes(json);
        var req = new UnityWebRequest(url, method)
        {
            uploadHandler = new UploadHandlerRaw(bytes),
            downloadHandler = new DownloadHandlerBuffer(),
        };
        req.SetRequestHeader("Content-Type", "application/json");
        return req;
    }

    private void AddAuthHeader(UnityWebRequest req)
    {
        var token = _accessTokenProvider?.Invoke();
        if (!string.IsNullOrEmpty(token))
            req.SetRequestHeader("Authorization", "Bearer " + token);
    }

    private static Task SendAsync(UnityWebRequest req, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<bool>();
        var op = req.SendWebRequest();
        op.completed += _ =>
        {
            if (ct.IsCancellationRequested)
                tcs.TrySetCanceled(ct);
            else
                tcs.TrySetResult(true);
        };
        return tcs.Task;
    }

    private static (T result, string error) ParseResponse<T>(UnityWebRequest req)
    {
        if (!IsSuccess(req.responseCode))
            return (default, ExtractError(req));

        var text = req.downloadHandler?.text;
        if (string.IsNullOrEmpty(text))
            return (default, null);

        try
        {
            var result = JsonUtility.FromJson<T>(text);
            return (result, null);
        }
        catch
        {
            return (default, "json_parse_error");
        }
    }

    private static bool IsSuccess(long code) => code >= 200 && code < 300;

    private static string ExtractError(UnityWebRequest req)
    {
        var text = req.downloadHandler?.text;
        if (!string.IsNullOrEmpty(text))
        {
            try
            {
                var err = JsonUtility.FromJson<ApiError>(text);
                if (err?.error != null && !string.IsNullOrEmpty(err.error.code))
                    return err.error.code;
            }
            catch { }
        }
        return $"HTTP {req.responseCode}";
    }
}
