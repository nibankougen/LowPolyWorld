using UnityEngine;

/// <summary>
/// HomeScene 起動時に最初に実行される初期化エントリポイント。
/// DontDestroyOnLoad マネージャーをここで一元管理する。
/// Phase A (API 不要): AudioManager, LocalizationManager
/// Phase B (API 後): UserManager, CacheManager は各自の Awake で Singleton を登録する
/// </summary>
public class Bootstrapper : MonoBehaviour
{
    [Header("Phase A — API 不要マネージャー")]
    [SerializeField] private GameObject _audioManagerPrefab;

    [Header("Phase B — API 後マネージャー")]
    [SerializeField] private GameObject _userManagerPrefab;
    [SerializeField] private GameObject _cacheManagerPrefab;
    [SerializeField] private GameObject _shopManagerPrefab;

    private static bool _initialized;

    private void Awake()
    {
        if (_initialized)
        {
            Destroy(gameObject);
            return;
        }
        _initialized = true;
        DontDestroyOnLoad(gameObject);

        // Phase A managers
        if (_audioManagerPrefab != null && AudioManager.Instance == null)
            Instantiate(_audioManagerPrefab);

        // Phase B managers — instantiate here so they persist before API calls
        if (_userManagerPrefab != null && UserManager.Instance == null)
            Instantiate(_userManagerPrefab);

        if (_cacheManagerPrefab != null && CacheManager.Instance == null)
            Instantiate(_cacheManagerPrefab);

        if (_shopManagerPrefab != null && ShopManager.Instance == null)
            Instantiate(_shopManagerPrefab);
    }

    private void OnDestroy()
    {
        // Allow re-init if scene is completely reloaded in editor
        if (this != null)
            _initialized = false;
    }
}
