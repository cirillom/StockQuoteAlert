using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace StockQuoteAlert
{
    public enum RecommendationState
    {
        Buy,
        Sell,
        Nothing
    }

    internal class Stock
    {
        private string name;
        // all prices are stored in cents to avoid floating-point precision issues
        private int sellPriceCents;
        private int buyPriceCents;
        private int currentPriceCents;
        static readonly HttpClient client = new HttpClient();

        public Stock(string name, int sellPriceCents, int buyPriceCents)
        {
            Program.GlobalLogger.LogDebug("Creating tracker for {StockName}", name);
            var config = new ConfigurationBuilder().AddUserSecrets<Program>().Build();
            string? apiKey = config["BrapiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException("API key is missing from Secret Manager.");
            }
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            Program.GlobalLogger.LogDebug("Initialized HttpClient with API key and User-Agent header.");

            SetNameAsync(name).Wait();
            UpdateCurrentPrice().Wait();
            this.sellPriceCents = sellPriceCents;
            this.buyPriceCents = buyPriceCents;
        }

        public string Name
        {
            get { return name; }
            private set { name = value; }
        }

        public decimal SellPrice => sellPriceCents / 100m;
        public decimal BuyPrice => buyPriceCents / 100m;
        public decimal CurrentPrice => currentPriceCents / 100m;

        public async Task SetNameAsync(string value)
        {
            Program.GlobalLogger.LogDebug("Checking '{StockName}' existence.", value);

            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Stock name cannot be null or empty.");

            string url = $"https://brapi.dev/api/quote/{value}";
            using HttpResponseMessage response = await client.GetAsync(url);
            if (response.IsSuccessStatusCode)
                Name = value;
            else
                throw new ArgumentException($"Stock name '{value}' is not valid or not found in the API.");

            Program.GlobalLogger.LogDebug("Tracking {StockName}.", Name);
        }

        public async Task UpdateCurrentPrice()
        {
            Program.GlobalLogger.LogDebug("Updating current price for {StockName}.", Name);
            string url = $"https://brapi.dev/api/quote/{this.Name}";
            string json = await client.GetStringAsync(url);

            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            this.currentPriceCents = (int)(root.GetProperty("results")[0].GetProperty("regularMarketPrice").GetDecimal() * 100);

            Program.GlobalLogger.LogInformation("Updated current price for {StockName}: R${CurrentPrice}.", Name, currentPriceCents / 100.0);
        }

        public RecommendationState GetRecommentation()
        {
            UpdateCurrentPrice().Wait();

            if (this.currentPriceCents > this.sellPriceCents)
            {
                return RecommendationState.Sell;

            }
            else if (this.currentPriceCents < this.buyPriceCents)
            {
                return RecommendationState.Buy;
            }

            return RecommendationState.Nothing;
        }
    }
}
