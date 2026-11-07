# Inventory Management API

A RESTful inventory and sales management API built with **C#**, **ASP.NET Core Minimal APIs (.NET 10)**, **Entity Framework Core**, and **SQLite**.

This developer portfolio project covers product management, inventory movements, auditable reversals, transactional sales, historical financial snapshots, category-based BACK calculations, automatic receipt numbering, staff sales allocation, and historical sales reporting. It focuses on backend business rules, data integrity, transaction safety, and automated testing.

> **Project status:** Active development. **Day 1–Day 4 completed.** The Day 4 test target is **58 passing tests**; confirm the latest count with `dotnet test InventoryApi.sln`.

## Tech Stack

| Technology                       | Purpose                                     |
| -------------------------------- | ------------------------------------------- |
| C# / .NET 10                     | Backend development                         |
| ASP.NET Core Minimal APIs        | HTTP endpoints                              |
| Entity Framework Core            | Persistence, relationships, and migrations  |
| SQLite                           | Local relational database                   |
| LINQ                             | Database queries and financial calculations |
| xUnit                            | Unit and integration testing                |
| Microsoft.AspNetCore.Mvc.Testing | In-process API integration tests            |
| Git / GitHub                     | Version control                             |

## Implemented Features

### Product Management

- Create, retrieve, and update products.
- Delete products only when they have no stock movement history.
- Validate product names, categories, purchase costs, and selling prices.
- Store product records in SQLite with database-generated IDs.

### Inventory Locations and Stock Balances

- Four seeded locations: **Store**, **Locker**, **Office**, and **Wine Chiller**.
- Record stock receipts, transfers, and issues with UTC timestamps and optional references.
- Calculate balances from incoming and outgoing stock movements.
- Reject movements that would leave insufficient stock at the source location.
- Count only **Store** and **Wine Chiller** toward usable stock.

**Total stock** = Store + Locker + Office + Wine Chiller  
**Usable stock** = Store + Wine Chiller

### Stock Movement Reversals

- Correct a movement with a linked compensating movement instead of deleting history.
- Require a reversal reason of 1–200 characters.
- Prevent duplicate reversals and reversals of reversals.
- Check stock availability before a reversal that removes inventory.
- Prevent direct reversal of stock movements linked to sales.
- Use database uniqueness constraints to protect reversal integrity.

### Sales and Financial Snapshots

- Create multi-item sales and deduct stock automatically from usable locations.
- Validate combined quantities across duplicate sale lines to prevent overselling.
- Save sale headers, items, and stock movements atomically in a SQLite transaction.
- Capture historical product names, categories, selling prices, and purchase costs.
- Calculate total revenue, total cost, and gross profit.
- Preserve receipt values when product details change later.
- Record sale timestamps in UTC and a **Singapore business date with a 3 AM cutoff**.

### BACK Calculation

- Calculate BACK as **(unit selling price − unit purchase cost) × quantity × 15%** for eligible categories.
- Eligible categories: **Champagne, Red Wine, White Wine, Red & White Wine, Tequila, and Sake**.
- Other categories, including a product labelled only `Wine`, currently calculate zero BACK.
- Save historical category and BACK values on each sale item.
- Include item-level `backAmount` and receipt-level `totalBack` in sales responses.
- Track BACK separately from gross profit and allocated net sales.

### Receipt Numbering

- Generate `YYYYMMDD-001`, `YYYYMMDD-002`, and subsequent receipt numbers.
- Use the **3 AM Singapore business-day cutoff** for the date prefix.
- Restart sequences at `001` for each business date.
- Store receipt number and sequence on the sale.
- Enforce unique indexes on receipt number and `(BusinessDate, ReceiptSequence)`.
- Generate numbers inside the sale transaction; rejected sales do not retain receipt numbers.
- During SQLite write contention, one request may return HTTP 503 rather than commit a duplicate receipt.

### Sales Allocation (Up to Eight Ladies)

- Assign **zero to eight unique lady names** to a receipt.
- Accept an optional, nonnegative `excludedAmount` that cannot exceed revenue.
- Calculate **eligible sales = total revenue − excluded amount**.
- Split eligible sales, total bottle costs, and BACK equally among assigned ladies.
- Distribute rounding remainders so sales/cost totals reconcile to cents and BACK totals to four decimal places.
- Preserve each lady's name snapshot, position, and allocated amounts.
- Calculate **net sales = allocated eligible sales − allocated bottle cost**.
- Avoid creating additional stock movements or duplicating revenue when multiple ladies share a receipt.
- Allow receipts without any lady allocations.

### Historical Ladies' Sales Reporting (Day 4)

- **Daily report:** Group allocations across receipts for a selected Singapore business date.
- **Monthly report:** Aggregate allocations for a selected year and month.
- **Previous completed month:** Automatically choose the latest fully completed business month using the 3 AM Singapore cutoff.
- Group names case-insensitively (for example, `Hana` and `hana`).
- Sort ladies by allocated sales, highest first.
- Return each lady's allocated sales, bottle costs, net sales, and BACK, plus summary totals.
- Reuse the same summary calculator for daily and monthly results.
- Calculate reports from **stored historical allocations**, not current product prices.
- Test monthly totals against the sum of daily totals.

> **Reporting scope:** These endpoints report **allocated ladies' performance**, not total store revenue. Receipts without assigned ladies do not contribute to ladies' report totals.

### Transaction Safety and Audit Integrity

- Use serializable SQLite transactions for operations that check and change stock.
- Return HTTP 503 for database busy/locked conditions that cannot be resolved.
- Roll back sales, stock movements, and allocations together when a sale fails.
- Preserve references between sale items and their stock movements.
- Protect products with inventory history from deletion.

## API Endpoints

### General

| Method | Endpoint  | Description             |
| ------ | --------- | ----------------------- |
| GET    | `/`       | Application information |
| GET    | `/health` | Health check            |
| GET    | `/about`  | Project information     |

### Products

| Method | Endpoint         | Description                            |
| ------ | ---------------- | -------------------------------------- |
| GET    | `/products`      | List products                          |
| GET    | `/products/{id}` | Get a product                          |
| POST   | `/products`      | Create a product                       |
| PUT    | `/products/{id}` | Update a product                       |
| DELETE | `/products/{id}` | Delete a product without stock history |

### Inventory

| Method | Endpoint                        | Description                                 |
| ------ | ------------------------------- | ------------------------------------------- |
| GET    | `/locations`                    | List seeded locations                       |
| GET    | `/products/{id}/stock`          | Stock balances by location                  |
| GET    | `/stock-movements`              | List movements; optional `productId` filter |
| GET    | `/stock-movements/{id}`         | Get one movement                            |
| POST   | `/stock-movements`              | Receive, transfer, or issue stock           |
| POST   | `/stock-movements/{id}/reverse` | Create an audited reversal                  |

### Sales and Historical Receipts

| Method | Endpoint      | Description                                                                              |
| ------ | ------------- | ---------------------------------------------------------------------------------------- |
| POST   | `/sales`      | Create a sale, deduct stock, calculate BACK, assign a receipt number, and allocate sales |
| GET    | `/sales/{id}` | Retrieve a historical receipt with financial totals, BACK, and allocations               |

### Ladies' Sales Reporting

| Method | Endpoint                                    | Description                                    |
| ------ | ------------------------------------------- | ---------------------------------------------- |
| GET    | `/reports/ladies/daily?date=YYYY-MM-DD`     | Daily report for a business date               |
| GET    | `/reports/ladies/monthly?year=YYYY&month=M` | Historical report for a selected month         |
| GET    | `/reports/ladies/previous-month`            | Report for the latest completed business month |

The API uses HTTP responses including **201 Created**, **204 No Content**, **400 Bad Request**, **404 Not Found**, **409 Conflict**, and **503 Service Unavailable** as applicable.

## Business Rules

| Metric                          | Formula or rule                                                           |
| ------------------------------- | ------------------------------------------------------------------------- |
| Line revenue                    | Unit selling price × quantity                                             |
| Line cost                       | Unit purchase cost × quantity                                             |
| Gross profit                    | Total revenue − total cost                                                |
| BACK (eligible categories only) | (Unit selling price − unit purchase cost) × quantity × 15%                |
| Eligible sales                  | Total revenue − excluded amount                                           |
| Per-lady net sales              | Allocated eligible sales − allocated bottle cost                          |
| Business date                   | Singapore local date, with sales before 3 AM assigned to the previous day |
| Daily/monthly ladies' sales     | Sum of allocated eligible sales for the selected business date(s)         |

BACK is recorded separately; it is **not subtracted from net sales**.

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Git
- EF Core CLI (`dotnet-ef`) compatible with EF Core 10

If needed, install the EF Core CLI version matching the project's EF Core packages:

```powershell
dotnet tool install --global dotnet-ef --version 10.0.14
```

### 1. Clone the repository

```powershell
git clone https://github.com/terrancedoo/inventory-management-api.git
cd inventory-management-api
```

### 2. Restore dependencies

```powershell
dotnet restore InventoryApi.sln
```

### 3. Apply migrations

```powershell
dotnet ef database update
```

This creates or updates the local SQLite database, including seeded inventory locations.

### 4. Run the API

```powershell
dotnet run --project InventoryApi.csproj --urls http://localhost:5050
```

Try `http://localhost:5050/health`, `http://localhost:5050/products`, or `http://localhost:5050/locations`.

## Example Requests

### Create a product

**POST** `/products`

```json
{
  "name": "Demo Champagne",
  "category": "Champagne",
  "purchaseCost": 75.0,
  "sellingPrice": 180.0
}
```

The response contains the new product ID. Use the returned ID in later requests.

### Receive stock into Store

**POST** `/stock-movements`

```json
{
  "productId": 1,
  "fromLocationId": null,
  "toLocationId": 1,
  "quantity": 10,
  "reference": "DEMO-RECEIPT-001"
}
```

### Transfer stock from Store to Locker

**POST** `/stock-movements`

```json
{
  "productId": 1,
  "fromLocationId": 1,
  "toLocationId": 2,
  "quantity": 3,
  "reference": "DEMO-TRANSFER-001"
}
```

Assuming no earlier movements, this leaves 10 total units: 7 usable units in Store and 3 units in Locker.

### Reverse a stock movement

**POST** `/stock-movements/2/reverse`

```json
{
  "reason": "Transferred to the wrong location"
}
```

Replace example product and movement IDs with those returned by your own API.

### Create a sale with allocations

**POST** `/sales`

```json
{
  "excludedAmount": 40.0,
  "ladyNames": ["Hana", "Yuna"],
  "items": [
    {
      "productId": 1,
      "fromLocationId": 1,
      "quantity": 2
    },
    {
      "productId": 2,
      "fromLocationId": 4,
      "quantity": 1
    }
  ]
}
```

`excludedAmount` and `ladyNames` are optional. The sale requires existing products and sufficient stock at the specified locations.

**Illustrative result:** If product 1 is Champagne at S$180 selling price / S$75 cost and product 2 is a generic `Wine` at S$80 selling price / S$30 cost:

| Metric              |   Amount |
| ------------------- | -------: |
| Total revenue       | S$440.00 |
| Total purchase cost | S$180.00 |
| Gross profit        | S$260.00 |
| Total BACK          |  S$31.50 |
| Excluded amount     |  S$40.00 |
| Eligible sales      | S$400.00 |

| Allocation     |     Hana |     Yuna |
| -------------- | -------: | -------: |
| Eligible sales | S$200.00 | S$200.00 |
| Bottle cost    |  S$90.00 |  S$90.00 |
| Net sales      | S$110.00 | S$110.00 |
| BACK           |  S$15.75 |  S$15.75 |

The response includes the sale ID, UTC timestamp, Singapore business date, receipt number, historical item snapshots, financial totals, and allocations. Use **GET** `/sales/{id}` to retrieve the historical receipt.

### Retrieve ladies' reports

**Daily report:**

```text
GET /reports/ladies/daily?date=2026-11-08
```

**Selected monthly report:**

```text
GET /reports/ladies/monthly?year=2026&month=10
```

**Previous completed business month:**

```text
GET /reports/ladies/previous-month
```

A report returns its business date or period, the number of ladies, combined `totals`, and individual `ladies` rows with `sales`, `totalBottleCost`, `netSales`, and `back`.

For example, on **December 1, 2026 at 2:59 AM Singapore time**, the latest completed business month is **October 2026**; at **3:00 AM**, it becomes **November 2026**.

## Automated Testing

The solution includes xUnit unit tests and integration tests. API integration tests use an in-process test server and isolated SQLite databases with EF Core migrations, rather than the normal local `inventory.db`.

Run the entire suite:

```powershell
dotnet test InventoryApi.sln
```

Run a specific test class:

```powershell
dotnet test InventoryApi.sln --filter "FullyQualifiedName~SalesEndpointsTests"
```

**Day 4 target: 58 passing tests, 0 failures, 0 skipped.** The latest full run explicitly reported in this conversation was 57 passing tests; the daily/monthly reconciliation test was added afterward and should bring the total to 58. Verify this before committing.

Test coverage includes:

- Product and inventory workflows, stock balances, and reversal rules.
- Concurrent inventory operations, duplicate reversal prevention, and transaction contention.
- Multi-item sales, combined stock requirements, atomic rollback, and audit relationships.
- Historical price, category, cost, and BACK snapshots.
- Receipt numbers, 3 AM business-day cutoff, and concurrent receipt uniqueness.
- Two-lady and eight-lady allocations, rounding reconciliation, and historical allocations.
- Invalid lady lists, zero-lady receipts, excluded amount validation, and rollback.
- Daily summary grouping, case-insensitive names, and empty results.
- Daily business-date filtering and selected-month boundaries.
- Previous completed business-month selection, year-end, and leap-year boundaries.
- Reconciliation between daily reports and monthly totals.

## Project Structure

```text
InventoryApi/
├── Data/
│   └── InventoryDbContext.cs
├── Models/
│   ├── Product.cs
│   ├── StockLocation.cs
│   ├── StockMovement.cs
│   ├── Sale.cs
│   ├── SaleItem.cs
│   ├── SaleAllocation.cs
│   ├── LadyDailySummaryRow.cs
│   └── (request models)
├── Services/
│   ├── BusinessDateCalculator.cs
│   ├── BackCalculator.cs
│   ├── AllocationSplitter.cs
│   ├── LadyDailySummaryCalculator.cs
│   └── PreviousCompletedMonthCalculator.cs
├── Migrations/
│   ├── (EF Core migration files)
│   └── InventoryDbContextModelSnapshot.cs
├── tests/
│   └── InventoryApi.Tests/
│       ├── InventoryApiFactory.cs
│       ├── InventoryEndpointsTests.cs
│       ├── StockMovementReversalTests.cs
│       ├── BusinessDateCalculatorTests.cs
│       ├── BackCalculatorTests.cs
│       ├── LadyDailySummaryCalculatorTests.cs
│       ├── PreviousCompletedMonthCalculatorTests.cs
│       ├── SalesEndpointsTests.cs
│       └── SalesReliabilityTests.cs
├── Program.cs
├── InventoryApi.csproj
├── InventoryApi.sln
├── appsettings.json
├── .gitignore
└── README.md
```

## Development Progress

| Day   | Milestone                                                                                                                                     | Status    |
| ----- | --------------------------------------------------------------------------------------------------------------------------------------------- | --------- |
| Day 1 | Project setup, products, EF Core/SQLite migrations, inventory locations, stock ledger, safe reversals, and integration tests                  | Completed |
| Day 2 | Sales models, multi-item sales, 3 AM business date, historical price snapshots, concurrency protection, rollback, and audit tests             | Completed |
| Day 3 | BACK calculation and historical snapshots, receipt numbers, equal allocation for up to eight ladies, rounding, validations, and receipt tests | Completed |
| Day 4 | Daily ladies' sales reporting                                                                                                                 | Completed |
|       | Selected monthly historical reporting                                                                                                         | Completed |
|       | Previous completed business-month reporting                                                                                                   | Completed |
|       | Reporting calculators and automated tests                                                                                                     | Completed |
|       | Daily/monthly financial reconciliation                                                                                                        | Completed |

## Current Limitations

- No graphical frontend or user authentication/authorization.
- Reporting covers allocated ladies' sales; it is not a full store revenue or general analytics dashboard.
- No sales cancellation, refund, or full sales-correction workflow.
- Product create/update endpoints do not consistently restrict prices to two decimal places; sales with staff allocations reject unsupported precision.
- Historical sales created before Day 3 may have `null` BACK or receipt-number fields; no automatic backfill is implemented.
- Stock balance calculations currently load movement history into memory instead of aggregating in SQL.
- SQLite supports one active writer at a time; heavy contention can return HTTP 503.
- No request idempotency keys for safely retrying potentially uncertain submissions.
- No authenticated staff identity attached to inventory corrections.
- No product archiving / soft-delete workflow.
- GitHub Actions CI and deployment are not yet configured.

## Planned Features

- Full store-level revenue and profitability reporting, separate from ladies' allocations.
- Audited sales corrections, refunds, and voids.
- Product archiving and consistent currency-precision validation.
- Authentication and role-based authorization.
- Request idempotency and stronger contention handling.
- Database-side stock balance aggregation.
- JavaScript frontend.
- GitHub Actions CI and deployment.

## Author

GitHub: [terrancedoo](https://github.com/terrancedoo)
