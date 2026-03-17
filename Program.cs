using StockQuoteAlert;
using EmailClientApp;
using Microsoft.Extensions.Logging;

class Program
{
    public static ILogger GlobalLogger;

    static async Task<int> Main(string[] args)
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "[HH:mm:ss] ";
            });
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        GlobalLogger = loggerFactory.CreateLogger<Program>();

        if (args.Length != 3)
        {
            GlobalLogger.LogError("Please provide the stock name, sell price, and buy price as command-line arguments.");
            GlobalLogger.LogWarning("Usage: .\\StockQuotAlert <StockName> <SellPrice> <BuyPrice>");
            return -1;
        }

        GlobalLogger.LogInformation("Launching Stock Quote Alert");

        EmailClient client = new EmailClient("D:\\Projects\\StockQuoteAlert\\appsettings.json");

        string stockName = args[0];
        int sellPriceCents = (int)(decimal.Parse(args[1]) * 100);
        int buyPriceCents = (int)(decimal.Parse(args[2]) * 100);
        GlobalLogger.LogDebug("Parsed command-line arguments: StockName={StockName}, SellPrice={SellPrice}, BuyPrice={BuyPrice}", stockName, sellPriceCents, buyPriceCents);

        Stock stock = new Stock(stockName, sellPriceCents, buyPriceCents);

        // loop to continuously check the stock price and print the recommendation
        bool isRunning = true;

        Console.CancelKeyPress += (sender, e) =>
        {
            GlobalLogger.LogInformation("Stopping monitor...");
            e.Cancel = true;
            isRunning = false;
        };

        GlobalLogger.LogInformation("Starting stock price monitor. Press Ctrl+C to stop.");

        while (isRunning)
        {
            var recommendation = stock.GetRecommentation();
            switch (recommendation)
            {
                case RecommendationState.Buy:
                    await client.SendEmailAsync(
                        subject: $"Buy Alert: {stock.Name}",
                        body: $"<h1>Buy Alert</h1><p>It is recommended to <strong>BUY</strong> {stock.Name}.</p><p>Current Price: R${stock.CurrentPrice:F2}<br>Buy Target: R${stock.BuyPrice:F2}</p>",
                        isHtml: true
                    );
                    GlobalLogger.LogInformation("Buy {StockName}.", stock.Name);
                    break;
                case RecommendationState.Sell:
                    await client.SendEmailAsync(
                        subject: $"Sell Alert: {stock.Name}",
                        body: $"<h1>Sell Alert</h1><p>It is recommended to <strong>SELL</strong> {stock.Name}.</p><p>Current Price: R${stock.CurrentPrice:F2}<br>Sell Target: R${stock.SellPrice:F2}</p>",
                        isHtml: true
                    );
                    GlobalLogger.LogInformation("Sell {StockName}.", stock.Name);
                    break;
                default:
                    break;
            }


            await Task.Delay(20*1000);
        }

        return 0;
    }
}

