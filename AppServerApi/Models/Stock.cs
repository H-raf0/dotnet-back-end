using System.ComponentModel.DataAnnotations;

namespace AppServerApi.Models;

public class Stock
{
    [Key]
    public string Id { get; set; } = string.Empty;

    public string Symbol { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    // Latest price (persisted)
    public double Price { get; set; }

    // Percentage change vs previous persisted price
    public double Change { get; set; }
}
