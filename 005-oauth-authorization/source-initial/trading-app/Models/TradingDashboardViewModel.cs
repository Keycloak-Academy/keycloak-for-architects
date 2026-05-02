namespace trading_app.Models;

public class TradingDashboardViewModel
{
    public object[] Stocks         { get; set; } = [];
    public string?  StocksError    { get; set; }
    public object[] Portfolio      { get; set; } = [];
    public string?  PortfolioError { get; set; }
    public double   TotalValue     { get; set; }
}
