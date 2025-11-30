using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using AppServerApi.Models;

namespace AppServerApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StocksController : ControllerBase
{
    private readonly AppDbContext _db;
    private static readonly object Locker = new();

    // Keep price histories and other non-critical time-series in memory only.
    private static readonly Dictionary<string, List<double>> PriceHistories = new();

    // In-memory portfolio for demo purposes. In a real app this should be per-user and persisted.
    private static readonly PortfolioDto Portfolio = new()
    {
        Balance = 10000,
        Stocks = new Dictionary<string,int>()
    };

    public StocksController(AppDbContext db)
    {
        _db = db;

        // Seed initial stocks in DB if none exist
        lock (Locker)
        {
            if (!_db.Stocks.Any())
            {
                var initial = new List<Stock>
                {
                    new Stock { Id = "1", Symbol = "TECH", Name = "TechCorp", Price = 150.25, Change = 0 },
                    new Stock { Id = "2", Symbol = "FIN", Name = "FinanceInc", Price = 95.8, Change = 0 },
                    new Stock { Id = "3", Symbol = "ENERGY", Name = "EnergyPlus", Price = 78.5, Change = 0 },
                    new Stock { Id = "4", Symbol = "HEALTH", Name = "HealthCare", Price = 210.1, Change = 0 },
                };
                _db.Stocks.AddRange(initial);
                _db.SaveChanges();

                // seed price histories in memory
                PriceHistories["1"] = new List<double>{120,125,130,135,140,145,148,150,152,150.25};
                PriceHistories["2"] = new List<double>{90,92,94,93,95,96,95.5,96,96.5,95.8};
                PriceHistories["3"] = new List<double>{75,76,77,78,77.5,78,78.2,78.8,78.3,78.5};
                PriceHistories["4"] = new List<double>{200,202,205,207,208,209,210,210.5,210.2,210.1};
            }
        }
    }

    [HttpGet]
    public ActionResult<List<StockDto>> GetStocks()
    {
        lock (Locker)
        {
            var stocks = _db.Stocks.ToList();
            var dtos = stocks.Select(s => new StockDto(s.Id, s.Symbol, s.Name, s.Price, PriceHistories.ContainsKey(s.Id) ? PriceHistories[s.Id] : new List<double> { s.Price })).ToList();
            return Ok(dtos);
        }
    }

    [HttpGet("portfolio")]
    public ActionResult<PortfolioDto> GetPortfolio()
    {
        lock (Locker)
        {
            return Ok(Portfolio);
        }
    }

    [HttpPost("portfolio/addMoney")]
    public ActionResult<PortfolioDto> AddMoney([FromBody] AddMoneyRequest req)
    {
        if (req == null || req.Amount <= 0) return BadRequest(new { message = "Invalid amount", code = "INVALID_AMOUNT" });
        lock (Locker)
        {
            Portfolio.Balance += req.Amount;
            return Ok(Portfolio);
        }
    }

    [HttpPost("portfolio/buy")]
    public ActionResult<PortfolioDto> Buy([FromBody] BuySellRequest req)
    {
        if (req == null || req.Quantity <= 0) return BadRequest(new { message = "Invalid quantity", code = "INVALID_QUANTITY" });
        lock (Locker)
        {
            var stock = _db.Stocks.FirstOrDefault(s => s.Id == req.StockId || s.Symbol == req.StockId);
            if (stock == null) return NotFound(new { message = "Stock not found", code = "STOCK_NOT_FOUND" });

            var cost = stock.Price * req.Quantity;
            if (Portfolio.Balance < cost) return BadRequest(new { message = "Insufficient funds", code = "INSUFFICIENT_FUNDS" });

            Portfolio.Balance -= cost;
            if (Portfolio.Stocks.ContainsKey(stock.Symbol)) Portfolio.Stocks[stock.Symbol] += req.Quantity;
            else Portfolio.Stocks[stock.Symbol] = req.Quantity;

            // small price impact persisted to DB
            var prev = stock.Price;
            var impact = 1 + Math.Min(0.3, 0.005 * Math.Sqrt(req.Quantity));
            stock.Price = Math.Round(stock.Price * impact, 2);
            stock.Change = Math.Round((stock.Price - prev) / (prev == 0 ? 1 : prev) * 100, 2);
            _db.SaveChanges();

            // keep expanded price history in-memory only
            if (!PriceHistories.ContainsKey(stock.Id)) PriceHistories[stock.Id] = new List<double> { prev };
            PriceHistories[stock.Id].Add(stock.Price);
            if (PriceHistories[stock.Id].Count > 250) PriceHistories[stock.Id].RemoveAt(0);

            return Ok(Portfolio);
        }
    }

    [HttpPost("portfolio/sell")]
    public ActionResult<PortfolioDto> Sell([FromBody] BuySellRequest req)
    {
        if (req == null || req.Quantity <= 0) return BadRequest(new { message = "Invalid quantity", code = "INVALID_QUANTITY" });
        lock (Locker)
        {
            var stock = _db.Stocks.FirstOrDefault(s => s.Id == req.StockId || s.Symbol == req.StockId);
            if (stock == null) return NotFound(new { message = "Stock not found", code = "STOCK_NOT_FOUND" });

            var owned = Portfolio.Stocks.ContainsKey(stock.Symbol) ? Portfolio.Stocks[stock.Symbol] : 0;
            if (owned < req.Quantity) return BadRequest(new { message = "Not enough shares", code = "INSUFFICIENT_SHARES" });

            var proceeds = stock.Price * req.Quantity;
            Portfolio.Balance += proceeds;
            Portfolio.Stocks[stock.Symbol] = owned - req.Quantity;
            if (Portfolio.Stocks[stock.Symbol] <= 0) Portfolio.Stocks.Remove(stock.Symbol);

            // small price impact persisted to DB
            var prev = stock.Price;
            var impact = 1 - Math.Min(0.3, 0.005 * Math.Sqrt(req.Quantity));
            stock.Price = Math.Round(stock.Price * impact, 2);
            stock.Change = Math.Round((stock.Price - prev) / (prev == 0 ? 1 : prev) * 100, 2);
            _db.SaveChanges();

            // keep expanded price history in-memory only
            if (!PriceHistories.ContainsKey(stock.Id)) PriceHistories[stock.Id] = new List<double> { prev };
            PriceHistories[stock.Id].Add(stock.Price);
            if (PriceHistories[stock.Id].Count > 250) PriceHistories[stock.Id].RemoveAt(0);

            return Ok(Portfolio);
        }
    }

    public class StockDto
    {
        public StockDto(string id, string symbol, string name, double price, List<double> priceHistory)
        {
            Id = id;
            Symbol = symbol;
            Name = name;
            Price = price;
            PriceHistory = priceHistory;
            Change = priceHistory.Count > 0 ? Math.Round((price - priceHistory.Last()) / (priceHistory.Last() == 0 ? 1 : priceHistory.Last()) * 100, 2) : 0;
        }

        public string Id { get; set; }
        public string Symbol { get; set; }
        public string Name { get; set; }
        public double Price { get; set; }
        public List<double> PriceHistory { get; set; }
        public double Change { get; set; }
    }

    public class PortfolioDto
    {
        public double Balance { get; set; }
        public Dictionary<string,int> Stocks { get; set; } = new();
    }

    public class AddMoneyRequest { public double Amount { get; set; } }
    public class BuySellRequest { public string StockId { get; set; } = string.Empty; public int Quantity { get; set; } }
}
