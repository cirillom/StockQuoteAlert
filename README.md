# StockQuoteAlert

A lightweight .NET console application that monitors B3 stock prices and sends email alerts when a target crosses your configured buy or sell thresholds.

---

## Features

- **Real-time price monitoring** — polls a stock's current price on a configurable interval (default: 20 seconds)
- **Buy & sell alerts** — sends an HTML email when the price drops below your buy target or rises above your sell target
- **Automatic retries** — both the quote API and the email sender retry up to 5 times with a 2-second backoff before giving up
- **Graceful shutdown** — pressing Ctrl+C cancels any in-flight request cleanly without spamming error logs
- **Config-driven** — all secrets and settings live in `appsettings.json`; nothing is hardcoded

---

## Usage

```bash
.\StockQuoteAlert.exe <StockName> <SellPrice> <BuyPrice>
```

**Example:**
```bash
.\StockQuoteAlert.exe PETR4 22.67 22.59
```

| Argument | Description | Constraints |
|---|---|---|
| `StockName` | B3 ticker symbol | Alphanumeric, 1–10 characters |
| `SellPrice` | Alert threshold for selling | Must be greater than BuyPrice |
| `BuyPrice` | Alert threshold for buying | Must be greater than zero |

---

## Configuration (`appsettings.json`)

Copy `appsettings.example.json` to `appsettings.json` and fill in your values. This file is gitignored and never committed.

```json
{
  "BrapiKey": "YOUR_BRAPI_TOKEN_HERE",
  "BrapiTimeoutSeconds": 30,
  "PollingDelaySeconds": 60,

  "EmailSettings": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": 587,
    "SecureSocket": "StartTls",
    "SenderEmail": "you@gmail.com",
    "SenderName": "Stock Alert",
    "SenderPassword": "YOUR_APP_PASSWORD_HERE",
    "RecipientEmail": "recipient@gmail.com",
    "RecipientName": "Recipient Name"
  }
}
```

### Field Reference

| Field | Description |
|---|---|
| `BrapiKey` | API token from [brapi.dev](https://brapi.dev). Leave empty to use the free tier (see below) |
| `BrapiTimeoutSeconds` | HTTP request timeout. 30 is a safe default for the free tier |
| `PollingDelaySeconds` | Interval between price checks in seconds. Default: 20 |
| `SmtpHost` | Your SMTP server hostname |
| `SmtpPort` | Typically `587` for StartTls, `465` for SslOnConnect |
| `SecureSocket` | One of: `None`, `Auto`, `SslOnConnect`, `StartTls`, `StartTlsWhenAvailable` |
| `SenderEmail` | The account used to send alerts |
| `SenderPassword` | For Gmail, this must be an App Password (see below) |
| `RecipientEmail` | Where alert emails are delivered |

---

## Design Decisions

### Why Brapi?

The B3 exchange does not offer a free public API. Brapi is the most practical option for individual developers building tools against the Brazilian market — it provides a straightforward REST endpoint for B3 tickers, has a free tier, and requires no institutional access. The tradeoff is that the free tier only includes a subset of the most liquid stocks such as: `PETR4`, `VALE3`, `ITUB4`, `MGLU3`. If you try to monitor a ticker that isn't in the free tier, the application will log a warning and exit.
Getting an API key from Brapi is a simple process that unlocks access to the full universe of B3 tickers, so it's recommended for anyone looking to monitor less common stocks.

### Why Gmail SMTP?

Gmail's SMTP server is free, reliable, and requires no infrastructure setup, making it the right default for a project at this scale. The `EmailClient` is not Gmail-specific — any standard SMTP server works by changing the `EmailSettings` block. The Gmail-specific App Password requirement is a Google security policy, not a limitation of this application.

Gmail requires an **App Password** rather than your account password when using SMTP directly.

1. Enable 2-Step Verification on your Google account
2. Go to [myaccount.google.com/apppasswords](https://myaccount.google.com/apppasswords)
3. Generate a password for "Mail"
4. Paste that 16-character password into `SenderPassword`

### Why prices are stored as integer cents

Floating-point arithmetic is unreliable for price comparisons (`22.67 * 100` can produce `2266.9999...` in IEEE 754). All prices are converted to integer cents on input and only converted back to `decimal` for display, which guarantees exact threshold comparisons.

### Why a static `HttpClient`

`HttpClient` is designed to be reused. Instantiating a new one per request exhausts the socket pool under load (`SocketException: address already in use`). A single static instance handles all outbound requests safely for a single-threaded polling loop.

---

## AI Usage

AI assistants used: **Claude Sonnet 4.6** (Anthropic) and **Gemini 3.1 Pro** (Google).

**Entirely human-written:**
- `Stock.cs` — fetching quotes from the API, parsing the response, and all threshold logic that determines when a buy or sell signal should trigger

**Written with AI assistance:**
- `EmailClient.cs` — the email sending logic was built with AI. The implementation was reviewed, adjusted, and is fully understood, but the initial structure and MailKit usage came from AI generation
- Syntax help and targeted refactoring in several places across the codebase (retry patterns, cancellation token handling, argument parsing)
---

## Building & Publishing

**Run locally (debug):**
```bash
dotnet run -- PETR4 22.67 22.59
```

**Publish a release build:**
```bash
dotnet publish /p:PublishProfile=Release
```

Output will be in `bin/Release/net9.0/win-x64/publish/` and contains only the `.exe` and `appsettings.example.json`.

---

## Known Shortcomings

- **No alert deduplication** — while the price stays out of bounds, an alert email is sent on every poll cycle. This means your inbox will receive a new email every `PollingDelaySeconds` for as long as the condition holds. A proper fix would track the last sent state and only re-alert when the price crosses back through the threshold.

- **Single stock only** — the application monitors one ticker per process. Running multiple stocks requires launching multiple instances.

- **No persistent state** — if the application is restarted while a price is out of bounds, it will immediately send an alert again with no memory of previous ones.