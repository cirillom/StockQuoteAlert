using EmailClientApp;
using StockQuoteAlert;
using Microsoft.Extensions.Logging;
using System.Globalization;

class Program
{
    private static ILogger logger;
    const int PollIntervalMs = 20 * 1000;

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

        logger = loggerFactory.CreateLogger<Program>();
        logger.LogInformation("Launching Stock Quote Alert");

        EmailClient client;
        try
        {
            string configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            client = new EmailClient(configPath, logger);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Failed to initialize EmailClient. Exiting...");
            return -1;
        }

        //TODO: encapsulate this in a method and add error handling
        if (args.Length != 3)
        {
            logger.LogError("Please provide the stock name, sell price, and buy price as command-line arguments.");
            logger.LogWarning("Usage: .\\StockQuoteAlert <StockName> <SellPrice> <BuyPrice>");
            return -1;
        }

        string stockName = args[0];
        if (!System.Text.RegularExpressions.Regex.IsMatch(stockName, @"^[A-Z0-9]{1,10}$"))
        {
            logger.LogError("Invalid stock name '{StockName}'. Must be alphanumeric, e.g. PETR4.", stockName);
            return -1;
        }

        if (!TryParsePriceToCents(args[1], "SellPrice", out int sellPriceCents, logger))
            return -1;
        if (!TryParsePriceToCents(args[2], "BuyPrice", out int buyPriceCents, logger))
            return -1;

        if (buyPriceCents >= sellPriceCents)
        {
            logger.LogError(
                "BuyPrice (R${BuyPrice}) must be strictly less than SellPrice (R${SellPrice}).",
                buyPriceCents / 100m, sellPriceCents / 100m);
            return -1;
        }

        logger.LogDebug(
            "Parsed arguments: StockName={StockName}, SellPrice={SellPrice}, BuyPrice={BuyPrice}",
            stockName, sellPriceCents / 100m, buyPriceCents / 100m);

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, e) =>
        {
            logger.LogInformation("Stopping monitor...");
            e.Cancel = true;
            cts.Cancel();
        };

        Stock stock;
        try
        {
            stock = await Stock.CreateAsync(stockName, sellPriceCents, buyPriceCents, logger, cts.Token);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Failed to initialize stock tracker for {StockName}. Exiting...", stockName);
            return -1;
        }

        logger.LogInformation("Starting stock price monitor. Press Ctrl+C to stop.");
        while (!cts.Token.IsCancellationRequested)
        {
            RecommendationState recommendation;
            try
            {
                recommendation = await stock.GetRecommendationAsync(cts.Token);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while fetching recommendation for {StockName}.", stock.Name);
                try { await Task.Delay(PollIntervalMs, cts.Token); } catch (TaskCanceledException) { break; }
                continue;
            }

            bool sent = false;
            switch (recommendation)
            {
                case RecommendationState.Buy:
                    sent = await client.SendEmailAsync(
                        subject: $"Buy Alert: {stock.Name}",
                        body: $"<h1>Buy Alert</h1><p>It is recommended to <strong>BUY</strong> {stock.Name}.</p><p>Current Price: R${stock.CurrentPrice:F2}<br>Buy Target: R${stock.BuyPrice:F2}</p>",
                        isHtml: true,
                        cts.Token
                    );
                    if (sent)
                        logger.LogInformation("Buy alert sent for {StockName}.", stock.Name);
                    else
                        logger.LogWarning("Buy alert FAILED for {StockName}.", stock.Name);
                    break;
                    
                case RecommendationState.Sell:
                    sent = await client.SendEmailAsync(
                        subject: $"Sell Alert: {stock.Name}",
                        body: $"<h1>Sell Alert</h1><p>It is recommended to <strong>SELL</strong> {stock.Name}.</p><p>Current Price: R${stock.CurrentPrice:F2}<br>Sell Target: R${stock.SellPrice:F2}</p>",
                        isHtml: true,
                        cts.Token
                    );
                    if (sent)
                        logger.LogInformation("Sell alert sent for {StockName}.", stock.Name);
                    else
                        logger.LogWarning("Sell alert FAILED for {StockName}.", stock.Name);
                    break;
                default:
                    logger.LogDebug("No action for {StockName}. Current price R${Price:F2} is within bounds.", stock.Name, stock.CurrentPrice);
                    break;
            }


            try { await Task.Delay(PollIntervalMs, cts.Token); } catch (TaskCanceledException) { break; }
        }

        return 0;
    }

    private static bool TryParsePriceToCents(string input, string paramName, out int cents, ILogger logger)
    {
        cents = 0;

        if (!decimal.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal price))
        {
            logger.LogError("Invalid {ParamName} value: '{Input}'. Must be a number (e.g. 22.67).", paramName, input);
            return false;
        }

        if (price <= 0)
        {
            logger.LogError("Invalid {ParamName} value: '{Input}'. Price must be greater than zero.", paramName, input);
            return false;
        }

        // decimal.Round avoids any edge case like 22.679999... before truncation
        cents = (int)decimal.Round(price * 100, 0, MidpointRounding.AwayFromZero);
        return true;
    }

}

