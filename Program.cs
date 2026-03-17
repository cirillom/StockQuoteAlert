using StockQuoteAlert;

if (args.Length != 3)
{
    Console.WriteLine("Please provide the stock name, sell price, and buy price as command-line arguments.");
    Console.WriteLine("Usage: .\\StockQuotAlert <StockName> <SellPrice> <BuyPrice>");
    return;
}

string stockName = args[0];
string sellPriceStr = args[1];
string buyPriceStr = args[2];

int sellPriceCents = (int)(decimal.Parse(sellPriceStr) * 100);
int buyPriceCents = (int)(decimal.Parse(buyPriceStr) * 100);

Console.WriteLine($"Stock: {stockName}");
Console.WriteLine($"Sell Price: R${sellPriceCents/100.0}");
Console.WriteLine($"Buy Price: R${buyPriceCents/100.0}");

Stock stock = new Stock(stockName, sellPriceCents, buyPriceCents);

// loop to continuously check the stock price and print the recommendation
bool isRunning = true;

Console.CancelKeyPress += (sender, e) =>
{
    Console.WriteLine("\nStopping monitor...");
    e.Cancel = true;
    isRunning = false;
};

Console.WriteLine("Starting to monitor stock price. Press Ctrl+C to stop.");

while (isRunning)
{
    var recommendation = stock.GetRecommentation();
    switch (recommendation)
    {
        case RecommendationState.Buy:
            Console.ForegroundColor = ConsoleColor.Green;
            //send email to user saying to Buy the stock
            break;
        case RecommendationState.Sell:
            //send email to user saying to Sell the stock
            Console.ForegroundColor = ConsoleColor.Red;
            break;
        default:
            break;
    }
    Console.WriteLine($"Current recommendation for {stock.Name}: {recommendation}");
    Console.ResetColor();


    await Task.Delay(1000);
}
