if (args.Length == 3)
{

    string stockName = args[0];
    string sellPriceStr = args[1];
    string buyPriceStr = args[2];

    int sellPriceCents = (int)(decimal.Parse(sellPriceStr) * 100);
    int buyPriceCents = (int)(decimal.Parse(buyPriceStr) * 100);

    Console.WriteLine($"Stock: {stockName}");
    Console.WriteLine($"Sell Price: R${sellPriceCents/100.0}");
    Console.WriteLine($"Buy Price: R${buyPriceCents/100.0}");
}
else
{
    Console.WriteLine("No command-line arguments string provided.");
}
