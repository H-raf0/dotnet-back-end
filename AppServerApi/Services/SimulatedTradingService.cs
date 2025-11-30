using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Collections.Generic;
using AppServerApi.Models;

namespace AppServerApi.Services
{
    public class SimulatedTradingService : BackgroundService
    {
        private readonly IServiceProvider _provider;
        private readonly ILogger<SimulatedTradingService> _logger;
        private readonly IConfiguration _config;

        public SimulatedTradingService(IServiceProvider provider, ILogger<SimulatedTradingService> logger, IConfiguration config)
        {
            _provider = provider;
            _logger = logger;
            _config = config;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // configurable values with sensible defaults
            var intervalMs = _config.GetValue<int?>("SimulatedTrading:TickMs") ?? 2000;
            var tradeChance = _config.GetValue<double?>("SimulatedTrading:TradeChance") ?? 0.30;

            var rnd = new Random();

            _logger.LogInformation("SimulatedTradingService starting with interval {ms}ms and tradeChance {chance}", intervalMs, tradeChance);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(intervalMs, stoppingToken);

                    using (var scope = _provider.CreateScope())
                    {
                        var db = (AppDbContext)scope.ServiceProvider.GetService(typeof(AppDbContext));
                        if (db == null) continue;

                        lock (MarketState.Locker)
                        {
                            var stocksToUpdate = db.Stocks.ToList();

                            // small random walk + mean reversion for each stock
                            foreach (var stk in stocksToUpdate)
                            {
                                var prev = stk.Price;
                                var noise = (rnd.NextDouble() - 0.5) * 0.02; // ±1%
                                var meanRevert = 1 + (0 - noise) * 0.001;
                                var newP = Math.Round(prev * (1 + noise) * meanRevert, 2);
                                if (newP <= 0) newP = Math.Round(prev * (1 + noise), 2);

                                stk.Price = newP;
                                stk.Change = Math.Round((newP - prev) / (prev == 0 ? 1 : prev) * 100, 2);

                                if (!MarketState.PriceHistories.ContainsKey(stk.Id)) MarketState.PriceHistories[stk.Id] = new List<double> { prev };
                                MarketState.PriceHistories[stk.Id].Add(stk.Price);
                                if (MarketState.PriceHistories[stk.Id].Count > 250) MarketState.PriceHistories[stk.Id].RemoveAt(0);
                            }

                            // Occasionally simulate random user transactions to make the market look alive
                            try
                            {
                                if (rnd.NextDouble() < tradeChance)
                                {
                                    var users = db.Users.ToList();
                                    if (users.Count > 0 && stocksToUpdate.Count > 0)
                                    {
                                        int actions = rnd.Next(1, 4);
                                        for (int a = 0; a < actions; a++)
                                        {
                                            var user = users[rnd.Next(users.Count)];
                                            var stock = stocksToUpdate[rnd.Next(stocksToUpdate.Count)];
                                            var doBuy = rnd.NextDouble() < 0.6;
                                            var qty = rnd.Next(1, 25);

                                            var portfolio = new Dictionary<string, int>();
                                            try
                                            {
                                                if (!string.IsNullOrEmpty(user.StocksJson))
                                                    portfolio = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, int>>(user.StocksJson) ?? new();
                                            }
                                            catch { portfolio = new(); }

                                            var prev = stock.Price;

                                            if (doBuy)
                                            {
                                                var cost = stock.Price * qty;
                                                if (user.Balance >= cost && cost > 0)
                                                {
                                                    user.Balance -= cost;
                                                    if (portfolio.ContainsKey(stock.Symbol)) portfolio[stock.Symbol] += qty;
                                                    else portfolio[stock.Symbol] = qty;

                                                    var impact = 1 + Math.Min(0.08, 0.002 * Math.Sqrt(qty));
                                                    stock.Price = Math.Round(stock.Price * impact, 2);
                                                }
                                            }
                                            else
                                            {
                                                var owned = portfolio.ContainsKey(stock.Symbol) ? portfolio[stock.Symbol] : 0;
                                                if (owned > 0)
                                                {
                                                    var sellQty = Math.Min(owned, qty);
                                                    var proceeds = stock.Price * sellQty;
                                                    user.Balance += proceeds;
                                                    portfolio[stock.Symbol] = owned - sellQty;
                                                    if (portfolio[stock.Symbol] <= 0) portfolio.Remove(stock.Symbol);

                                                    var impact = 1 - Math.Min(0.08, 0.002 * Math.Sqrt(sellQty));
                                                    stock.Price = Math.Round(stock.Price * impact, 2);
                                                }
                                            }

                                            try
                                            {
                                                user.StocksJson = System.Text.Json.JsonSerializer.Serialize(portfolio);
                                                user.UpdatedAt = DateTime.UtcNow;
                                            }
                                            catch { }

                                            if (!MarketState.PriceHistories.ContainsKey(stock.Id)) MarketState.PriceHistories[stock.Id] = new List<double> { prev };
                                            MarketState.PriceHistories[stock.Id].Add(stock.Price);
                                            if (MarketState.PriceHistories[stock.Id].Count > 250) MarketState.PriceHistories[stock.Id].RemoveAt(0);
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Simulated trading action failed");
                            }

                            db.SaveChanges();
                        }
                    }
                }
                catch (TaskCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SimulatedTradingService loop error");
                }
            }

            _logger.LogInformation("SimulatedTradingService stopping");
        }
    }
}
