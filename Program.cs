using EmailClientApp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StockQuoteAlert;
using System.Globalization;
using System.Text.RegularExpressions;

class Program
{
    const int PollIntervalMs = 20 * 1000;

    private record StockArgs(string StockName, int SellPriceCents, int BuyPriceCents);

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

        ILogger logger = loggerFactory.CreateLogger<Program>();
        logger.LogInformation("Launching Stock Quote Alert.");

        if (!TryLoadConfig(out IConfiguration? config, logger)) return -1;

        if (!TryInitEmailClient(config!, out EmailClient? client, logger)) return -1;

        StockArgs? stockArgs = ParseArgs(args, logger);
        if (stockArgs is null) return -1;

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            logger.LogInformation("Stopping monitor...");
            e.Cancel = true;
            cts.Cancel();
        };

        Stock stock;
        try
        {
            stock = await Stock.CreateAsync(
                stockArgs.StockName, stockArgs.SellPriceCents, stockArgs.BuyPriceCents,
                logger, config!, cts.Token);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Failed to initialize stock tracker for {StockName}. Exiting...", stockArgs.StockName);
            return -1;
        }

        await RunMonitorLoopAsync(stock, client!, logger, cts.Token);
        return 0;
    }




    private static bool TryLoadConfig(out IConfiguration? config, ILogger logger)
    {
        config = null;
        try
        {
            config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .Build();
            return true;
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Failed to load appsettings.json. Exiting...");
            return false;
        }
    }

    private static bool TryInitEmailClient(IConfiguration config, out EmailClient? client, ILogger logger)
    {
        client = null;
        try
        {
            client = new EmailClient(config, logger);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Failed to initialize EmailClient. Exiting...");
            return false;
        }
    }

    private static StockArgs? ParseArgs(string[] args, ILogger logger)
    {
        if (args.Length != 3)
        {
            logger.LogError("Please provide the stock name, sell price, and buy price as command-line arguments.");
            logger.LogInformation("Usage: .\\StockQuoteAlert <StockName> <SellPrice> <BuyPrice>");
            return null;
        }

        string stockName = args[0].ToUpper();
        if (!Regex.IsMatch(stockName, @"^[A-Z0-9]{1,10}$"))
        {
            logger.LogError("Invalid stock name '{StockName}'. Must be alphanumeric, e.g. PETR4.", stockName);
            return null;
        }

        if (!TryParsePriceToCents(args[1], "SellPrice", out int sellPriceCents, logger)) return null;
        if (!TryParsePriceToCents(args[2], "BuyPrice", out int buyPriceCents, logger)) return null;

        if (buyPriceCents >= sellPriceCents)
        {
            logger.LogError(
                "BuyPrice (R${BuyPrice:F2}) must be strictly less than SellPrice (R${SellPrice:F2}).",
                buyPriceCents / 100m, sellPriceCents / 100m);
            return null;
        }

        logger.LogDebug(
            "Parsed arguments: StockName={StockName}, SellPrice={SellPrice}, BuyPrice={BuyPrice}.",
            stockName, sellPriceCents / 100m, buyPriceCents / 100m);

        return new StockArgs(stockName, sellPriceCents, buyPriceCents);
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

        cents = (int)decimal.Round(price * 100, 0, MidpointRounding.AwayFromZero);
        return true;
    }

    private static async Task RunMonitorLoopAsync(Stock stock, EmailClient client,
        ILogger logger, CancellationToken ct)
    {
        logger.LogInformation("Starting stock price monitor. Press Ctrl+C to stop.");

        while (!ct.IsCancellationRequested)
        {
            RecommendationState recommendation;
            try
            {
                recommendation = await stock.GetRecommendationAsync(ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while fetching recommendation for {StockName}.", stock.Name);
                try { await Task.Delay(PollIntervalMs, ct); } catch (TaskCanceledException) { break; }
                continue;
            }

            if (recommendation != RecommendationState.Nothing)
                try
                {
                    await SendAlertAsync(client, stock, recommendation, logger, ct);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Unexpected error while sending alert for {StockName}.", stock.Name);
                }
            else
                logger.LogDebug(
                    "No action for {StockName}. Current price R${Price:F2} is within bounds.",
                    stock.Name, stock.CurrentPrice);

            try { await Task.Delay(PollIntervalMs, ct); } catch (TaskCanceledException) { break; }
        }

        logger.LogInformation("Monitor stopped.");
    }

    private static async Task SendAlertAsync(EmailClient client, Stock stock,
        RecommendationState recommendation, ILogger logger, CancellationToken ct)
    {
        bool isBuy = recommendation == RecommendationState.Buy;
        string action = isBuy ? "BUY" : "SELL";
        string target = isBuy
            ? $"Buy Target: R${stock.BuyPrice:F2}"
            : $"Sell Target: R${stock.SellPrice:F2}";

        bool sent = await client.SendEmailAsync(
            subject: $"{action} Alert: {stock.Name}",
            body: $"<h1>{action} Alert</h1>"
                + $"<p>It is recommended to <strong>{action}</strong> {stock.Name}.</p>"
                + $"<p>Current Price: R${stock.CurrentPrice:F2}<br>{target}</p>",
            isHtml: true,
            ct
        );

        if (sent)
            logger.LogInformation("{Action} alert sent for {StockName}.", action, stock.Name);
        else
            logger.LogWarning("{Action} alert FAILED for {StockName}.", action, stock.Name);
    }
}