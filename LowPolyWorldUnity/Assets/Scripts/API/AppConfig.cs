using UnityEngine;

/// <summary>
/// API 接続先など環境依存設定を保持する ScriptableObject。
/// Assets/Settings/AppConfig.asset に配置し、各 MonoBehaviour が参照する。
/// </summary>
[CreateAssetMenu(fileName = "AppConfig", menuName = "Lo-Res World/AppConfig")]
public class AppConfig : ScriptableObject
{
    [Tooltip("API サーバーのベース URL（末尾スラッシュなし）")]
    [SerializeField] private string _apiBaseUrl = "http://localhost:8080";

    [Tooltip("開発環境フラグ（true = 詳細エラーを表示）")]
    [SerializeField] private bool _isDevelopment = true;

    public string ApiBaseUrl => _apiBaseUrl.TrimEnd('/');
    public bool IsDevelopment => _isDevelopment;

    public string TermsOfServiceUrl => "https://lo-res.world/terms";
    public string PrivacyPolicyUrl => "https://lo-res.world/privacy";
}
