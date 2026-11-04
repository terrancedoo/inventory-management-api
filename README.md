# Inventory Management API

A RESTful inventory and sales management API built with **C#**, **ASP.NET Core Minimal APIs (.NET 10)**, **Entity Framework Core**, and **SQLite**.

This developer portfolio project covers product management, inventory movements, auditable reversals, transactional sales, historical financial snapshots, category-based BACK calculations, daily receipt numbering, and staff sales allocations. The focus is on backend business rules, data integrity, transaction safety, and automated testing.

> **Project status:** Active development. **Day 1, Day 2, and Day 3 completed.** Latest local verification: **45 tests passed, 0 failed**.

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
- Delete products only when they have no movement history.
- Validate product names, categories, purchase costs, and selling prices.
- Store product records in SQLite with database-generated IDs.

### Inventory Locations and Stock Balances

- Four seeded locations: **Store**, **Locker**, **Office**, and **Wine Chiller**.
- Record stock receipts, transfers, and issues with UTC timestamps and optional references.
- Calculate balances from incoming and outgoing stock movements.
- Reject movements that would leave insufficient stock at the source location.
- Calculate **usable stock** from **Store** and **Wine Chiller** only.

**Total stock** = Store + Locker + Office + Wine Chiller  
**Usable stock** = Store + Wine Chiller

### Stock Movement Reversals

- Correct a stock movement with a linked compensating movement instead of deleting history.
- Require a reversal reason of 1–200 characters.
- Prevent duplicate reversals and reversals of reversals.
- Check available stock before recording a reversal that removes inventory.
- Prevent direct reversal of stock movements linked to sales.
- Use database uniqueness constraints to protect reversal integrity.

### Sales and Financial Snapshots

- Create multi-item sales and deduct stock automatically from usable locations.
- Validate combined quantities across duplicate sale lines to prevent overselling.
- Save sale headers, sale items, and stock movements atomically in a SQLite transaction.
- Capture historical product name, category, unit selling price, and unit purchase cost for each sale item.
- Calculate total revenue, total purchase cost, and gross profit.
- Preserve historical receipt values when current product details change.
- Record a UTC timestamp and the corresponding **Singapore business date**, using a **3 AM Singapore time cutoff**.

### BACK Calculation

- Calculate BACK using **(unit selling price − unit purchase cost) × quantity × 15%**.
- Eligible categories: **Champagne, Red Wine, White Wine, Red & White Wine, Tequila, and Sake**.
- Other categories, including a product labelled only `Wine`, currently calculate zero BACK.
- Save category and BACK snapshots on each sale item so historical BACK does not change when a product is edited.
- Include item-level `backAmount` and receipt-level `totalBack` in sales responses.
- Keep BACK separate from gross profit and allocated net sales.

### Receipt Numbering

- Generate receipt numbers in the format `YYYYMMDD-001`, `YYYYMMDD-002`, and so on.
- Use the **3 AM Singapore business-day cutoff** for the date portion.
- Restart the sequence at `001` for each business date.
- Store the receipt number and daily sequence on the sale.
- Use unique database indexes on receipt number and `(BusinessDate, ReceiptSequence)`.
- Generate receipt numbers inside the sales transaction; a rejected sale does not keep a receipt number.
- Support concurrency safely: a contested request may return HTTP 503 rather than commit a duplicate receipt.

### Sales Allocation (Up to Eight Ladies)

- Optionally attach **0–8 unique lady names** to a receipt.
- Accept an optional, nonnegative `excludedAmount` that cannot exceed the sale's revenue.
- Calculate **eligible sales = total revenue − excluded amount**.
- Divide eligible sales, total bottle costs, and BACK equally among assigned ladies.
- Preserve cent-level sales/cost totals and four-decimal BACK totals by distributing rounding remainders.
- Save each allocation with its position and name snapshot.
- Calculate each allocated **net sales = allocated eligible sales − allocated bottle cost**.
- Do **not** create extra stock movements or duplicate revenue when a receipt has multiple allocations.
- Allow receipts with no ladies assigned; their allocation list is empty.

### Transaction Safety and Audit Integrity

- Use serializable SQLite transactions for operations that check and change stock.
- Return HTTP 503 for database busy/locked conditions that cannot be resolved.
- Roll back sale, stock movement, and allocation records together when a sale fails.
- Preserve references between sale items and their stock movement records.
- Protect products with stock history from deletion.

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
| GET    | `/products/{id}/stock`          | Stock totals and balances by location       |
| GET    | `/stock-movements`              | List movements; optional `productId` filter |
| GET    | `/stock-movements/{id}`         | Get one movement                            |
| POST   | `/stock-movements`              | Receive, transfer, or issue stock           |
| POST   | `/stock-movements/{id}/reverse` | Create an audited reversal                  |

### Sales

| Method | Endpoint      | Description                                                                          |
| ------ | ------------- | ------------------------------------------------------------------------------------ |
| POST   | `/sales`      | Create sale, deduct stock, calculate BACK, assign receipt number, and allocate sales |
| GET    | `/sales/{id}` | Retrieve historical receipt, financial totals, BACK, and allocations                 |

The API returns standard HTTP status codes such as **201 Created**, **204 No Content**, **400 Bad Request**, **404 Not Found**, **409 Conflict**, and **503 Service Unavailable** where applicable.

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

BACK is recorded separately; it is **not subtracted from net sales**.

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Git
- EF Core CLI (`dotnet-ef`) compatible with EF Core 10

Install the EF Core CLI if needed:

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

Assuming no earlier movements, this leaves 10 total units, including 7 usable units in Store and 3 non-usable units in Locker.

### Reverse a stock movement

**POST** `/stock-movements/2/reverse`

```json
{
  "reason": "Transferred to the wrong location"
}
```

Replace example product and movement IDs with the IDs returned by your own database.

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

`excludedAmount` and `ladyNames` are optional. The sale requires real products and sufficient stock at the requested locations.

**Illustrative result:** If product 1 is Champagne at S$180 selling price / S$75 cost and product 2 is a generic `Wine` product at S$80 selling price / S$30 cost:

| Metric              |   Amount |
| ------------------- | -------: |
| Total revenue       | S$440.00 |
| Total purchase cost | S$180.00 |
| Gross profit        | S$260.00 |
| Total BACK          |  S$31.50 |
| Excluded amount     |  S$40.00 |
| Eligible sales      | S$400.00 |

Allocations in the example:

| Metric                   |     Hana |     Yuna |
| ------------------------ | -------: | -------: |
| Allocated eligible sales | S$200.00 | S$200.00 |
| Allocated bottle cost    |  S$90.00 |  S$90.00 |
| Net sales                | S$110.00 | S$110.00 |
| Allocated BACK           |  S$15.75 |  S$15.75 |

The response includes the generated sale ID, UTC timestamp, Singapore business date, receipt number, historical item snapshots, financial totals, and allocations. Use **GET** `/sales/{id}` to retrieve that historical receipt later.

## Automated Testing

The solution includes xUnit unit tests and integration tests. API integration tests use an in-process test server and isolated SQLite databases with EF Core migrations, rather than the normal local `inventory.db`.

Run the entire suite:

```powershell
dotnet test InventoryApi.sln
```

Run a specific test class, for example:

```powershell
dotnet test InventoryApi.sln --filter "FullyQualifiedName~SalesEndpointsTests"
```

**Latest locally reported result: 45 tests passed, 0 failed, 0 skipped.**

Test coverage includes:

- Product and inventory CRUD, location balances, validation, and reversal rules.
- Concurrent inventory movements, duplicate reversals, and transaction contention.
- Multi-item sales, combined stock requirements, atomic rollback, and audit relationships.
- Historical price, category, cost, and BACK snapshots.
- Receipt numbers, business-day cutoff, and concurrent receipt uniqueness.
- Two-lady and eight-lady allocations, rounding reconciliation, and saved historical allocations.
- Invalid lady lists, zero-lady receipts, excluded amount validation, and rollback on failed allocations.

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
│   └── (request models)
├── Services/
│   ├── BusinessDateCalculator.cs
│   ├── BackCalculator.cs
│   └── AllocationSplitter.cs
├── Migrations/
│   ├── (EF Core migration files)
│   └── InventoryDbContextModelSnapshot.cs
├── tests/
│   └── InventoryApi.Tests/
│       ├── InventoryApiFactory.cs
│       ├── InventoryEndpointsTests.cs
│       ├── StockMovementReversalTests.cs
│       ├── BusinessDateCalculatorTests.cs
│       ├── SalesEndpointsTests.cs
│       ├── SalesReliabilityTests.cs
│       └── BackCalculatorTests.cs
├── Program.cs
├── InventoryApi.csproj
├── InventoryApi.sln
├── appsettings.json
├── .gitignore
└── README.md
```

## Development Progress

| Day   | Milestone                                                                                                                                                       | Status    |
| ----- | --------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------- |
| Day 1 | Project setup, products, EF Core/SQLite migrations, inventory locations, stock ledger, safe reversals, and integration tests                                    | Completed |
| Day 2 | Sales models, multi-item sales, 3 AM business date, historical price snapshots, concurrency protection, rollback, and audit tests                               | Completed |
| Day 3 | BACK calculation and historical snapshots, receipt numbers, equal sales allocation for up to eight ladies, rounding, validations, and receipt integration tests | Completed |

## Current Limitations

- No graphical frontend or user authentication/authorization.
- Historical monthly reporting and analytics endpoints are not yet implemented.
- No implemented sales cancellation/refund or full sales-correction workflow.
- Product create/update endpoints do not yet consistently restrict prices to two decimal places; sales with staff allocations reject unsupported precision.
- Existing sales created before some Day 3 migrations may have `null` historical BACK or receipt-number fields; no automatic backfill is documented.
- Stock balances currently use movement history loaded into memory instead of database-side aggregation.
- SQLite supports one active writer at a time; high contention can return HTTP 503.
- No request idempotency keys for safely retrying potentially uncertain submissions.
- No authenticated staff identity attached to inventory corrections.
- No product archive / soft-delete workflow.
- GitHub Actions continuous integration and deployment are not yet configured.

## Planned Features

- Historical business-day and previous-completed-month financial reports.
- Sales corrections / voids with a complete audit trail.
- Product archiving and consistent currency-precision validation.
- Authentication and role-based authorization.
- Request idempotency and more robust contention handling.
- Database-side stock balance aggregation.
- JavaScript frontend.
- GitHub Actions CI and deployment.

## Author

GitHub: [terrancedoo](https://github.com/terrancedoo)
