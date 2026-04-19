using System;

/// <summary>
/// クライアント側トラストポイント計算ロジック（純粋 C#・MonoBehaviour 非依存）。
/// サーバー側と同一のアルゴリズムを実装し、UI 表示や楽観更新に使用する。
///
/// 仕様: unity-game-abstract.md §22.3
///   加算ポイント = floor( (joinCount + exitCount) / 2.0 * floor(durationSec / 60) )
/// </summary>
public class TrustPointCalculatorLogic
{
    /// <summary>
    /// 公開ルーム退室時のトラストポイントを計算する。
    /// </summary>
    /// <param name="joinCount">入室時の他ユーザー数（自分を含まない）。</param>
    /// <param name="exitCount">退室時の他ユーザー数（自分を含まない）。</param>
    /// <param name="durationSeconds">在室時間（秒）。60 未満は 0 ポイント。</param>
    /// <returns>加算すべきトラストポイント（0 以上）。</returns>
    public float Calculate(int joinCount, int exitCount, float durationSeconds)
    {
        if (durationSeconds < 60f)
            return 0f;

        int floorMinutes = (int)(durationSeconds / 60f);
        double avg = (joinCount + exitCount) / 2.0;
        return (float)Math.Floor(avg * floorMinutes);
    }
}
