using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text.Json;

namespace StockQuoteAlert
{
    public enum RecommendationState { Buy, Sell, Nothing }

    internal class Stock
    {
        public string Name { get; private set; }
        public decimal SellPrice => sellPriceCents / 100m;
        public decimal BuyPrice => buyPriceCents / 100m;
        public decimal CurrentPrice => currentPriceCents / 100m;

        private int sellPriceCents;
        private int buyPriceCents;
        private int currentPriceCents;
        private static readonly HttpClient client = new HttpClient();
        private readonly ILogger logger;

        static Stock()
        {
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
            client.Timeout = TimeSpan.FromSeconds(10);
        }

        private Stock(string name, int sellPriceCents, int buyPriceCents, ILogger logger)
        {
            Name = name;
            this.sellPriceCents = sellPriceCents;
            this.buyPriceCents = buyPriceCents;
            this.logger = logger;
        }

        public static async Task<Stock> CreateAsync(string name, int sellPriceCents, int buyPriceCents, ILogger logger, IConfiguration config, CancellationToken ct = default)
        {
            logger.LogDebug("Creating tracker for {StockName}.", name);

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Stock name cannot be null or empty.");

            string? apiKey = config["BrapiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                logger.LogCritical("BrapiKey is missing from appsettings.json.");
                throw new InvalidOperationException("BrapiKey is missing from appsettings.json.");
            }

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            logger.LogDebug("API key loaded.");

            var stock = new Stock(name, sellPriceCents, buyPriceCents, logger);
            await stock.UpdateCurrentPriceAsync(ct);

            return stock;
        }

        private async Task<decimal> FetchQuotePriceAsync(string ticker, CancellationToken ct = default)
        {
            string url = $"https://brapi.dev/api/quote/{ticker}";
            int maxRetries = 5;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    using HttpResponseMessage response = await client.GetAsync(url, ct);

                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                        throw new ArgumentException($"Stock '{ticker}' not found in the API.");

                    response.EnsureSuccessStatusCode();

                    string json = await response.Content.ReadAsStringAsync();
                    using JsonDocument doc = JsonDocument.Parse(json);
                    return doc.RootElement
                        .GetProperty("results")[0]
                        .GetProperty("regularMarketPrice")
                        .GetDecimal();
                }
                catch (JsonException ex)
                {
                    logger.LogError(ex, "Unexpected JSON from API for '{Ticker}'.", ticker);
                    throw;
                }
                catch (Exception ex) when (ex is not ArgumentException and not OperationCanceledException)
                {
                    this.logger.LogWarning(ex, "Attempt {Attempt} of {MaxRetries} to fetch quote for '{Ticker}' failed.", attempt, maxRetries, ticker);
                    if (attempt == maxRetries)
                    {
                        this.logger.LogError(ex, "All {MaxRetries} attempts to fetch quote for '{Ticker}' exhausted.", maxRetries, ticker);
                        throw;
                    }
                    await Task.Delay(2000, ct);
                }
            }

            throw new InvalidOperationException("Unreachable.");
        }

        public async Task UpdateCurrentPriceAsync(CancellationToken ct = default)
        {
            this.logger.LogDebug("Updating price for {StockName}.", Name);
            decimal price = await FetchQuotePriceAsync(Name, ct);
            currentPriceCents = (int)decimal.Round(price * 100, 0, MidpointRounding.AwayFromZero);
            this.logger.LogInformation("Updated price for {StockName}: R${CurrentPrice:F2}.", Name, CurrentPrice);
        }

        public async Task<RecommendationState> GetRecommendationAsync(CancellationToken ct = default)
        {
            await UpdateCurrentPriceAsync(ct);

            if (currentPriceCents > sellPriceCents) return RecommendationState.Sell;
            if (currentPriceCents < buyPriceCents) return RecommendationState.Buy;
            return RecommendationState.Nothing;
        }
    }
}