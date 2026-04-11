using UnityEngine;

/// <summary>
/// Pure C# logic for mobile touch input.
/// 画面下半分: 移動・ジャンプ / 上半分: カメラ回転。
/// MonoBehaviour から各 OnTouch* メソッドとフレームごとの Tick を呼ぶ。
/// </summary>
public class TouchInputLogic
{
    /// <summary>スプリント移行までに指を静止させる秒数。</summary>
    public const float SprintSettleSeconds = 0.3f;

    /// <summary>スプリント判定の静止閾値（ピクセル）。</summary>
    public const float SprintDeltaThreshold = 8f;

    /// <summary>ダブルタップとみなす最大間隔（秒）。</summary>
    public const float DoubleTapMaxInterval = 0.3f;

    /// <summary>ダブルタップとみなす最大距離（ピクセル）。</summary>
    public const float DoubleTapMaxDistance = 50f;

    /// <summary>ダブルタップ後のスライドジャンプ判定閾値（ピクセル）。</summary>
    public const float JumpSlideThreshold = 10f;

    private float _screenWidth;
    private float _screenHeight;

    // 移動タッチ
    private int _moveTouchId = -1;
    private Vector2 _moveTouchStart;
    private Vector2 _moveTouchCurrent;
    private float _sprintTimer;
    private bool _isSprinting;

    // ダブルタップ
    private float _lastLowerTapTime = float.NegativeInfinity;
    private Vector2 _lastLowerTapPos;

    // ダブルタップ後スライドジャンプ待機
    private bool _awaitingDoubleTapSlide;
    private int _doubleTapSlideId = -1;
    private Vector2 _doubleTapSlideStart;

    // ルックタッチ
    private int _lookTouchId = -1;
    private Vector2 _accLookDelta;

    // 出力プロパティ
    public Vector2 MoveDirection { get; private set; }
    public bool IsSprinting { get; private set; }
    public bool JumpRequested { get; private set; }
    public Vector2 LookDelta { get; private set; }
    public bool IsMoving => _moveTouchId >= 0 && MoveDirection.sqrMagnitude > 0.001f;

    public void SetScreenSize(float width, float height)
    {
        _screenWidth = width;
        _screenHeight = height;
    }

    /// <summary>ジャンプ入力を消費する。PlayerController が 1 フレームで 1 回呼ぶ。</summary>
    public void ConsumeJump() => JumpRequested = false;

    /// <summary>タッチ開始イベント。</summary>
    /// <param name="id">フィンガー ID。</param>
    /// <param name="pos">スクリーン座標。</param>
    /// <param name="currentTime">現在時刻（秒）。Time.realtimeSinceStartup を渡す。</param>
    public void OnTouchBegan(int id, Vector2 pos, float currentTime)
    {
        bool isLower = pos.y < _screenHeight * 0.5f;

        if (isLower)
        {
            float elapsed = currentTime - _lastLowerTapTime;
            bool isDoubleTap =
                elapsed <= DoubleTapMaxInterval
                && Vector2.Distance(pos, _lastLowerTapPos) <= DoubleTapMaxDistance;

            if (isDoubleTap)
            {
                _lastLowerTapTime = float.NegativeInfinity;

                if (_moveTouchId >= 0)
                {
                    // 移動中に別指ダブルタップ → 移動方向へジャンプ
                    JumpRequested = true;
                }
                else
                {
                    // スライドジャンプ待機
                    _awaitingDoubleTapSlide = true;
                    _doubleTapSlideId = id;
                    _doubleTapSlideStart = pos;
                }
            }
            else
            {
                _lastLowerTapTime = currentTime;
                _lastLowerTapPos = pos;

                // 最初のタッチのみ移動タッチとして採用
                if (_moveTouchId < 0)
                {
                    _moveTouchId = id;
                    _moveTouchStart = pos;
                    _moveTouchCurrent = pos;
                    _sprintTimer = 0f;
                    _isSprinting = false;
                }
            }
        }
        else
        {
            // 上半分 → ルック
            if (_lookTouchId < 0)
            {
                _lookTouchId = id;
            }
        }
    }

    /// <summary>タッチ移動イベント。</summary>
    public void OnTouchMoved(int id, Vector2 pos, Vector2 delta)
    {
        if (id == _moveTouchId)
        {
            _moveTouchCurrent = pos;
        }

        if (id == _doubleTapSlideId && _awaitingDoubleTapSlide)
        {
            float dist = Vector2.Distance(pos, _doubleTapSlideStart);
            if (dist >= JumpSlideThreshold)
            {
                // スライド方向へジャンプ
                JumpRequested = true;
                _awaitingDoubleTapSlide = false;
                _doubleTapSlideId = -1;

                // スライド開始点から移動タッチとして継続
                if (_moveTouchId < 0)
                {
                    _moveTouchId = id;
                    _moveTouchStart = _doubleTapSlideStart;
                    _moveTouchCurrent = pos;
                    _sprintTimer = 0f;
                    _isSprinting = false;
                }
            }
        }

        if (id == _lookTouchId)
        {
            _accLookDelta += delta;
        }
    }

    /// <summary>タッチ終了イベント。</summary>
    public void OnTouchEnded(int id, Vector2 pos, float currentTime)
    {
        if (id == _moveTouchId)
        {
            _moveTouchId = -1;
            _isSprinting = false;
            _sprintTimer = 0f;
        }

        if (id == _doubleTapSlideId)
        {
            // スライドなしでタップ終了 → 即ジャンプ
            if (_awaitingDoubleTapSlide)
            {
                JumpRequested = true;
                _awaitingDoubleTapSlide = false;
                _doubleTapSlideId = -1;
            }
        }

        if (id == _lookTouchId)
        {
            _lookTouchId = -1;
        }
    }

    /// <summary>フレームごとの更新。PlayerController.Update から呼ぶ。</summary>
    public void Tick(float deltaTime)
    {
        LookDelta = _accLookDelta;
        _accLookDelta = Vector2.zero;

        if (_moveTouchId >= 0)
        {
            var delta = _moveTouchCurrent - _moveTouchStart;
            float dist = delta.magnitude;
            MoveDirection = dist > 1f ? delta / dist : Vector2.zero;

            if (!_isSprinting)
            {
                if (dist < SprintDeltaThreshold)
                    _sprintTimer += deltaTime;
                else
                    _sprintTimer = 0f;

                if (_sprintTimer >= SprintSettleSeconds)
                    _isSprinting = true;
            }
        }
        else
        {
            MoveDirection = Vector2.zero;
            _isSprinting = false;
            _sprintTimer = 0f;
        }

        IsSprinting = _isSprinting;
    }
}
