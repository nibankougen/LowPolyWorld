using System;
using UnityEngine;

/// <summary>
/// iOS Universal Links / Android App Links からアプリが起動または復帰したとき、
/// URL を解析して招待トークンを抽出し、OnInviteTokenReceived イベントで通知する。
/// DontDestroyOnLoad シングルトン。Bootstrapper 完了後に MarkReady() を呼ぶこと。
/// 仕様: screens-and-modes.md セクション 9.6
/// </summary>
public class DeepLinkHandler : MonoBehaviour
{
    public static DeepLinkHandler Instance { get; private set; }

    /// <summary>招待トークンが受信されたとき発火する。引数はトークン文字列。</summary>
    public event Action<string> OnInviteTokenReceived;

    private string _pendingToken;
    private bool _ready;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Application.deepLinkActivated += OnDeepLinkActivated;

        // コールドスタート: 起動 URL がすでに設定されている場合
        if (!string.IsNullOrEmpty(Application.absoluteURL))
            ProcessUrl(Application.absoluteURL);
    }

    private void OnDestroy()
    {
        Application.deepLinkActivated -= OnDeepLinkActivated;
    }

    /// <summary>
    /// ログイン完了・HomeScene 準備完了後に呼び出す。
    /// 保留中のトークンがあれば即座に OnInviteTokenReceived を発火する。
    /// </summary>
    public void MarkReady()
    {
        _ready = true;
        if (!string.IsNullOrEmpty(_pendingToken))
        {
            var token = _pendingToken;
            _pendingToken = null;
            OnInviteTokenReceived?.Invoke(token);
        }
    }

    private void OnDeepLinkActivated(string url) => ProcessUrl(url);

    private void ProcessUrl(string url)
    {
        var token = ExtractInviteToken(url);
        if (string.IsNullOrEmpty(token)) return;

        if (_ready)
            OnInviteTokenReceived?.Invoke(token);
        else
            _pendingToken = token; // 認証完了まで保留
    }

    /// <summary>
    /// URL から招待トークンを抽出する。
    /// https://lowpolyworld.app/invite/{token} → token
    /// </summary>
    internal static string ExtractInviteToken(string url)
    {
        if (string.IsNullOrEmpty(url)) return null;
        const string prefix = "/invite/";
        int idx = url.IndexOf(prefix, StringComparison.Ordinal);
        if (idx < 0) return null;
        var token = url[(idx + prefix.Length)..].TrimEnd('/');
        return string.IsNullOrEmpty(token) ? null : token;
    }
}
