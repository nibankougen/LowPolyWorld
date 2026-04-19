using System;
using System.Collections.Generic;

/// <summary>
/// コイン残高ロジック。ロット管理・残高計算・購入可否判定を担う純粋 C# クラス。
/// </summary>
public class CoinLedger
{
    public struct CoinLot
    {
        public string PurchaseId;
        public int Coins;
        public DateTime ValidUntil;
    }

    private readonly List<CoinLot> _lots = new();
    private int _totalDeducted;
    private int _totalSpent;

    public IReadOnlyList<CoinLot> Lots => _lots;

    /// <summary>APIから取得したロット一覧で状態を初期化する。</summary>
    public void SetFromApi(IEnumerable<CoinLot> lots, int totalDeducted, int totalSpent)
    {
        _lots.Clear();
        foreach (var lot in lots)
            _lots.Add(lot);
        _totalDeducted = totalDeducted;
        _totalSpent = totalSpent;
    }

    /// <summary>現在時刻での有効コイン残高を返す。</summary>
    public int GetBalance(DateTime now)
    {
        int raw = 0;
        foreach (var lot in _lots)
        {
            if (lot.ValidUntil > now)
                raw += lot.Coins;
        }
        return raw - _totalDeducted - _totalSpent;
    }

    /// <summary>マイナス残高の場合はショップ購入を禁止する。コイン購入（IAP）は常に許可。</summary>
    public bool CanPurchaseProduct(int priceCoins, DateTime now)
    {
        return GetBalance(now) >= priceCoins;
    }

    /// <summary>
    /// 返金キャンセル時に差し引くコイン数を計算する。
    /// 有効期限切れのロットは coins_deducted = 0。
    /// </summary>
    public int ComputeCancellationDeduction(string purchaseId, int coinsAmount, DateTime validUntil, DateTime now)
    {
        if (validUntil < now)
            return 0;
        return coinsAmount;
    }
}
