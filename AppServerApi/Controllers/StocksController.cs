using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Security.Claims;
using AppServerApi.Models;
using AppServerApi.Services;

namespace AppServerApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StocksController : ControllerBase
{
    private readonly AppDbContext _db;

    public StocksController(AppDbContext db)
    {
        _db = db;

        // Seed initial stocks in DB if none exist
        lock (MarketState.Locker)
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
                MarketState.PriceHistories["1"] = new List<double>{120,125,130,135,140,145,148,150,152,150.25};
                MarketState.PriceHistories["2"] = new List<double>{90,92,94,93,95,96,95.5,96,96.5,95.8};
                MarketState.PriceHistories["3"] = new List<double>{75,76,77,78,77.5,78,78.2,78.8,78.3,78.5};
                MarketState.PriceHistories["4"] = new List<double>{200,202,205,207,208,209,210,210.5,210.2,210.1};
            }

            // Ensure price histories exist for any stocks (including persisted ones)
            var allStocks = _db.Stocks.ToList();
            foreach (var s in allStocks)
            {
                if (!MarketState.PriceHistories.ContainsKey(s.Id))
                {
                    // initialize with a short history that ends with the persisted price
                    MarketState.PriceHistories[s.Id] = new List<double> { Math.Round(s.Price, 2) };
                }
            }
        }
    }

    // Helper method to get the current user ID from JWT token
    private int GetUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (userIdClaim != null && int.TryParse(userIdClaim.Value, out var userId))
        {
            return userId;
        }
        return 0;
    }

    [HttpGet]
    public ActionResult<List<StockDto>> GetStocks()
    {
        lock (MarketState.Locker)
        {
            var stocks = _db.Stocks.ToList();
            var dtos = stocks.Select(s => new StockDto(s.Id, s.Symbol, s.Name, s.Price, MarketState.PriceHistories.ContainsKey(s.Id) ? MarketState.PriceHistories[s.Id] : new List<double> { s.Price })).ToList();
            return Ok(dtos);
        }
    }

    [HttpGet("portfolio")]
    [Authorize]
    public ActionResult<PortfolioDto> GetPortfolio()
    {
        var userId = GetUserId();
        if (userId == 0) return Unauthorized(new { message = "Invalid or missing token", code = "INVALID_TOKEN" });

        lock (MarketState.Locker)
        {
            var user = _db.Users.FirstOrDefault(u => u.Id == userId);
            if (user == null) return NotFound(new { message = "User not found", code = "USER_NOT_FOUND" });

            var stocks = new Dictionary<string, int>();
            try
            {
                if (!string.IsNullOrEmpty(user.StocksJson))
                {
                    stocks = JsonSerializer.Deserialize<Dictionary<string, int>>(user.StocksJson) ?? new();
                }
            }
            catch
            {
                stocks = new();
            }

            return Ok(new PortfolioDto { Balance = user.Balance, Stocks = stocks });
        }
    }

    [HttpPost("portfolio/addMoney")]
    [Authorize]
    public ActionResult<PortfolioDto> AddMoney([FromBody] AddMoneyRequest req)
    {
        if (req == null || req.Amount <= 0) return BadRequest(new { message = "Invalid amount", code = "INVALID_AMOUNT" });

        var userId = GetUserId();
        if (userId == 0) return Unauthorized(new { message = "Invalid or missing token", code = "INVALID_TOKEN" });

        lock (MarketState.Locker)
        {
            var user = _db.Users.FirstOrDefault(u => u.Id == userId);
            if (user == null) return NotFound(new { message = "User not found", code = "USER_NOT_FOUND" });

            user.Balance += req.Amount;
            user.UpdatedAt = DateTime.UtcNow;
            _db.SaveChanges();

            var stocks = new Dictionary<string, int>();
            try
            {
                if (!string.IsNullOrEmpty(user.StocksJson))
                {
                    stocks = JsonSerializer.Deserialize<Dictionary<string, int>>(user.StocksJson) ?? new();
                }
            }
            catch
            {
                stocks = new();
            }

            return Ok(new PortfolioDto { Balance = user.Balance, Stocks = stocks });
        }
    }

    [HttpPost("portfolio/buy")]
    [Authorize]
    public ActionResult<PortfolioDto> Buy([FromBody] BuySellRequest req)
    {
        if (req == null || req.Quantity <= 0) return BadRequest(new { message = "Invalid quantity", code = "INVALID_QUANTITY" });

        var userId = GetUserId();
        if (userId == 0) return Unauthorized(new { message = "Invalid or missing token", code = "INVALID_TOKEN" });

        lock (MarketState.Locker)
        {
            var user = _db.Users.FirstOrDefault(u => u.Id == userId);
            if (user == null) return NotFound(new { message = "User not found", code = "USER_NOT_FOUND" });

            var stock = _db.Stocks.FirstOrDefault(s => s.Id == req.StockId || s.Symbol == req.StockId);
            if (stock == null) return NotFound(new { message = "Stock not found", code = "STOCK_NOT_FOUND" });

            var cost = stock.Price * req.Quantity;
            if (user.Balance < cost) return BadRequest(new { message = "Insufficient funds", code = "INSUFFICIENT_FUNDS" });

            // Update user balance and stocks
            user.Balance -= cost;

            var stocks = new Dictionary<string, int>();
            try
            {
                if (!string.IsNullOrEmpty(user.StocksJson))
                {
                    stocks = JsonSerializer.Deserialize<Dictionary<string, int>>(user.StocksJson) ?? new();
                }
            }
            catch
            {
                stocks = new();
            }

            if (stocks.ContainsKey(stock.Symbol)) stocks[stock.Symbol] += req.Quantity;
            else stocks[stock.Symbol] = req.Quantity;

            user.StocksJson = JsonSerializer.Serialize(stocks);
            user.UpdatedAt = DateTime.UtcNow;

            // small price impact persisted to DB
            var prev = stock.Price;
            var impact = 1 + Math.Min(0.3, 0.005 * Math.Sqrt(req.Quantity));
            stock.Price = Math.Round(stock.Price * impact, 2);
            stock.Change = Math.Round((stock.Price - prev) / (prev == 0 ? 1 : prev) * 100, 2);
            _db.SaveChanges();

            // keep expanded price history in-memory only
            if (!MarketState.PriceHistories.ContainsKey(stock.Id)) MarketState.PriceHistories[stock.Id] = new List<double> { prev };
            MarketState.PriceHistories[stock.Id].Add(stock.Price);
            if (MarketState.PriceHistories[stock.Id].Count > 250) MarketState.PriceHistories[stock.Id].RemoveAt(0);

            return Ok(new PortfolioDto { Balance = user.Balance, Stocks = stocks });
        }
    }

    [HttpPost("portfolio/sell")]
    [Authorize]
    public ActionResult<PortfolioDto> Sell([FromBody] BuySellRequest req)
    {
        if (req == null || req.Quantity <= 0) return BadRequest(new { message = "Invalid quantity", code = "INVALID_QUANTITY" });

        var userId = GetUserId();
        if (userId == 0) return Unauthorized(new { message = "Invalid or missing token", code = "INVALID_TOKEN" });

        lock (MarketState.Locker)
        {
            var user = _db.Users.FirstOrDefault(u => u.Id == userId);
            if (user == null) return NotFound(new { message = "User not found", code = "USER_NOT_FOUND" });

            var stock = _db.Stocks.FirstOrDefault(s => s.Id == req.StockId || s.Symbol == req.StockId);
            if (stock == null) return NotFound(new { message = "Stock not found", code = "STOCK_NOT_FOUND" });

            var stocks = new Dictionary<string, int>();
            try
            {
                if (!string.IsNullOrEmpty(user.StocksJson))
                {
                    stocks = JsonSerializer.Deserialize<Dictionary<string, int>>(user.StocksJson) ?? new();
                }
            }
            catch
            {
                stocks = new();
            }

            var owned = stocks.ContainsKey(stock.Symbol) ? stocks[stock.Symbol] : 0;
            if (owned < req.Quantity) return BadRequest(new { message = "Not enough shares", code = "INSUFFICIENT_SHARES" });

            var proceeds = stock.Price * req.Quantity;
            user.Balance += proceeds;
            stocks[stock.Symbol] = owned - req.Quantity;
            if (stocks[stock.Symbol] <= 0) stocks.Remove(stock.Symbol);

            user.StocksJson = JsonSerializer.Serialize(stocks);
            user.UpdatedAt = DateTime.UtcNow;

            // small price impact persisted to DB
            var prev = stock.Price;
            var impact = 1 - Math.Min(0.3, 0.005 * Math.Sqrt(req.Quantity));
            stock.Price = Math.Round(stock.Price * impact, 2);
            stock.Change = Math.Round((stock.Price - prev) / (prev == 0 ? 1 : prev) * 100, 2);
            _db.SaveChanges();

            // keep expanded price history in-memory only
            if (!MarketState.PriceHistories.ContainsKey(stock.Id)) MarketState.PriceHistories[stock.Id] = new List<double> { prev };
            MarketState.PriceHistories[stock.Id].Add(stock.Price);
            if (MarketState.PriceHistories[stock.Id].Count > 250) MarketState.PriceHistories[stock.Id].RemoveAt(0);

            return Ok(new PortfolioDto { Balance = user.Balance, Stocks = stocks });
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
