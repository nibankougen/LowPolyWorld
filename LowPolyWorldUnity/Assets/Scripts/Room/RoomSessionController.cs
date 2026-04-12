using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// ルームセッション制限・再接続 UI を統合管理する MonoBehaviour。
/// SessionTimeLimitLogic / AfkDetectionLogic / ReconnectionLogic を所有する。
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class RoomSessionController : MonoBehaviour
{
    private const float FlashDisplaySeconds = 4f;
    private const float SessionExpiredAutoExitSeconds = 5f;

    public event Action OnExitRequested;
    public event Action OnReconnectBackRequested;

    // ロジック
    private SessionTimeLimitLogic _sessionLimit;
    private AfkDetectionLogic _afkDetection;
    private ReconnectionLogic _reconnection;

    // UI 要素
    private UIDocument _document;
    private VisualElement _flashMessage;
    private Label _flashLabel;
    private VisualElement _sessionExpiredDialog;
    private VisualElement _reconnectingModal;
    private Label _labelAttempt;
    private VisualElement _connectFailedModal;

    private void Awake()
    {
        _document = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        var root = _document.rootVisualElement;
        _flashMessage = root.Q<VisualElement>("flash-message");
        _flashLabel = root.Q<Label>("flash-label");
        _sessionExpiredDialog = root.Q<VisualElement>("session-expired-dialog");
        _reconnectingModal = root.Q<VisualElement>("reconnecting-modal");
        _labelAttempt = root.Q<Label>("label-attempt");
        _connectFailedModal = root.Q<VisualElement>("connect-failed-modal");

        root.Q<Button>("btn-session-ok")?.RegisterCallback<ClickEvent>(_ => OnExitRequested?.Invoke());
        root.Q<Button>("btn-reconnect-back")?.RegisterCallback<ClickEvent>(_ => OnReconnectBackRequested?.Invoke());
        root.Q<Button>("btn-failed-back")?.RegisterCallback<ClickEvent>(_ => OnReconnectBackRequested?.Invoke());
    }

    /// <summary>セッションを開始する。</summary>
    public void StartSession(bool isPremium)
    {
        _sessionLimit = new SessionTimeLimitLogic(isPremium);
        _sessionLimit.OnWarning += OnWarning;
        _sessionLimit.OnExpired += OnSessionExpired;

        float afkThreshold = isPremium
            ? float.MaxValue
            : AfkDetectionLogic.DefaultAfkThresholdSeconds;
        _afkDetection = new AfkDetectionLogic(afkThreshold);
        _afkDetection.OnAfkDetected += OnAfkDetected;
    }

    private void Update()
    {
        _sessionLimit?.Tick(Time.deltaTime);
        _afkDetection?.Tick(Time.deltaTime);
        _reconnection?.Tick(Time.deltaTime);
    }

    /// <summary>ユーザー操作を通知する（AFK タイマーリセット）。</summary>
    public void NotifyUserInput()
    {
        _afkDetection?.NotifyInput();
    }

    /// <summary>残り時間を取得する（設定パネルの表示用）。</summary>
    public float GetRemainingSeconds() => _sessionLimit?.RemainingSeconds ?? 0f;

    // ---- セッション制限 ----

    private void OnWarning(float remaining)
    {
        int minutes = Mathf.CeilToInt(remaining / 60f);
        ShowFlash($"残り {minutes} 分です");
    }

    private void OnSessionExpired()
    {
        Show(_sessionExpiredDialog);
        StartCoroutine(AutoExitCoroutine());
    }

    private IEnumerator AutoExitCoroutine()
    {
        yield return new WaitForSeconds(SessionExpiredAutoExitSeconds);
        if (_sessionExpiredDialog != null && _sessionExpiredDialog.style.display == DisplayStyle.Flex)
            OnExitRequested?.Invoke();
    }

    private void OnAfkDetected()
    {
        // 通常ユーザーのみ放置退室（告知なし）
        OnExitRequested?.Invoke();
    }

    // ---- 再接続 ----

    /// <summary>再接続シーケンスを開始する。</summary>
    public void StartReconnection()
    {
        _reconnection = new ReconnectionLogic();
        _reconnection.OnAttemptStarted += OnReconnectAttempt;
        _reconnection.OnSuccess += OnReconnectSuccess;
        _reconnection.OnFailure += OnReconnectFailed;

        Show(_reconnectingModal);
        Hide(_connectFailedModal);
        _reconnection.Start();
    }

    /// <summary>再接続成功を通知する。</summary>
    public void NotifyReconnectSuccess() => _reconnection?.NotifySuccess();

    /// <summary>再接続失敗を通知する。</summary>
    public void NotifyReconnectFailure() => _reconnection?.NotifyFailure();

    private void OnReconnectAttempt(int attempt, float waitSeconds)
    {
        if (_labelAttempt != null)
            _labelAttempt.text = $"試行 {attempt}/{ReconnectionLogic.MaxAttempts}";
    }

    private void OnReconnectSuccess()
    {
        Hide(_reconnectingModal);
    }

    private void OnReconnectFailed()
    {
        Hide(_reconnectingModal);
        Show(_connectFailedModal);
    }

    // ---- ヘルパー ----

    private void ShowFlash(string message)
    {
        if (_flashMessage == null) return;
        if (_flashLabel != null) _flashLabel.text = message;
        Show(_flashMessage);
        StopCoroutine(nameof(HideFlashCoroutine));
        StartCoroutine(nameof(HideFlashCoroutine));
    }

    private IEnumerator HideFlashCoroutine()
    {
        yield return new WaitForSeconds(FlashDisplaySeconds);
        Hide(_flashMessage);
    }

    private static void Show(VisualElement el)
    {
        if (el != null) el.style.display = DisplayStyle.Flex;
    }

    private static void Hide(VisualElement el)
    {
        if (el != null) el.style.display = DisplayStyle.None;
    }
}
