using System.Collections.Generic;

namespace AppServerApi.Services
{
    // Simple static holder for in-memory market state used by controller and hosted service.
    public static class MarketState
    {
        // Lock for synchronizing access from controller and background service
        public static readonly object Locker = new object();

        // Price histories kept in memory (not persisted)
        public static readonly Dictionary<string, List<double>> PriceHistories = new Dictionary<string, List<double>>();
    }
}
