# StockQuoteAlert

This project requires a Brapi API key to fetch live stock data. You must never hardcode your API key into the source files or commit it to this repository.

To set up your local development environment, you need to use the .NET Secret Manager. Open the project in Visual Studio, right-click the project name in the Solution Explorer, and click on Manage User Secrets. This action will generate and open a local `secrets.json` file securely hidden in your Windows user directory.

Inside that opened file, create a JSON object with the property named "BrapiKey" and paste your personal API token as the string value. The application is configured to automatically read this hidden file at runtime. You can now compile and test the code safely without risking your credentials.