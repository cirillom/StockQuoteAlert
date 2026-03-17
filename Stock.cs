using System;
using System.Collections.Generic;
using System.Text;

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
        private int sellPriceCents;
        private int buyPriceCents;
        private int currentPriceCents;
        private RecommendationState recommendation;

        public Stock(string name, int sellPriceCents, int buyPriceCents)
        {
            this.Name = name;
            this.sellPriceCents = sellPriceCents;
            this.buyPriceCents = buyPriceCents;
            this.currentPriceCents = 2257;//UpdateCurrentPrice();
        }

        public string Name
        {
            get { return name; }
            set
            {
                // Validate that the name is not null or empty
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentException("Stock name cannot be null or empty.");
                }

                // TODO: Check if this is stock name exist inside B3
                name = value;
            }
        }

        public void UpdateCurrentPrice()
        {
            /*string currentPriceStr;
            // TODO: Implement logic to fetch the current price from an API or data source
            currentPriceStr = "22.65"; // Placeholder value for demonstration

            this.currentPriceCents = (int)(decimal.Parse(currentPriceStr) * 100);
            */
            this.currentPriceCents += 1; // Placeholder logic to simulate price changes for demonstration
            Console.WriteLine($"Updated current price for {Name}: R${currentPriceCents / 100.0}");
        }

        public RecommendationState GetRecommentation()
        {
            UpdateCurrentPrice();

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
