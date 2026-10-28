# Inventory Management API

A RESTful inventory management API built with **C#**, **ASP.NET Core (.NET 10)**, **Entity Framework Core**, and **SQLite**.

This portfolio project models inventory operations such as product management, stock receipts, transfers, issues, and corrections. It focuses on practical backend development, database relationships, transaction safety, automated integration testing, and auditable stock history.

> **Project status:** Active development. All completed milestones so far are grouped under **Day 1** in the development log.

## Tech Stack

| Technology                       | Purpose                                 |
| -------------------------------- | --------------------------------------- |
| C# / .NET 10                     | Backend development                     |
| ASP.NET Core Minimal APIs        | HTTP endpoints                          |
| Entity Framework Core            | Database access and migrations          |
| SQLite                           | Local relational database               |
| LINQ                             | Database queries and stock calculations |
| xUnit                            | Automated integration tests             |
| Microsoft.AspNetCore.Mvc.Testing | In-process API test server              |
| Git and GitHub                   | Version control                         |

## Implemented Features

### Product Management

- Create products with database-generated IDs.
- Retrieve all products or one product by ID.
- Update product details.
- Delete products that have no stock movement history.
- Validate product name, category, purchase cost, and selling price.
- Store product records persistently in SQLite.

### Inventory Locations and Stock Balances

- Four predefined locations: **Store**, **Locker**, **Office**, and **Wine Chiller**.
- Receive stock into a location.
- Transfer stock between locations.
- Issue stock out of tracked inventory.
- Record every movement with a UTC timestamp and optional reference.
- Calculate stock balances from incoming and outgoing movement history.
- Reject movements that would make the source location's stock negative.

Only **Store** and **Wine Chiller** count toward usable stock.

**Total stock** = Store + Locker + Office + Wine Chiller  
**Usable stock** = Store + Wine Chiller

### Transaction Safety

- Use SQLite serializable transactions when validating and saving stock movements.
- Keep stock availability checks and movement creation within the same transaction.
- Handle SQLite busy/locked errors with an HTTP 503 response when contention cannot be resolved.
- Prevent product deletion when stock movement history exists.

### Stock Movement Reversals

- Reverse an existing movement by recording a new movement in the opposite direction.
- Preserve the original movement in the audit history.
- Link a reversal to its original movement.
- Require a correction reason of 1–200 characters.
- Reject duplicate reversals and attempts to reverse a reversal.
- Reject reversals that would result in insufficient stock.
- Enforce a unique database constraint on the original movement being reversed.

### Automated Testing

- Run integration tests against isolated temporary SQLite databases.
- Apply the real EF Core migrations to test databases.
- Test product-related inventory workflows, stock balances, validation, concurrency, and reversals.
- **13 integration tests** in the current suite.

## API Endpoints

### General

| Method | Endpoint  | Description                   |
| ------ | --------- | ----------------------------- |
| GET    | `/`       | Application information       |
| GET    | `/health` | Health check                  |
| GET    | `/about`  | Project/developer information |

### Products

| Method | Endpoint         | Description                               | Success        |
| ------ | ---------------- | ----------------------------------------- | -------------- |
| GET    | `/products`      | Get all products                          | 200 OK         |
| GET    | `/products/{id}` | Get one product                           | 200 OK         |
| POST   | `/products`      | Create a product                          | 201 Created    |
| PUT    | `/products/{id}` | Update a product                          | 204 No Content |
| DELETE | `/products/{id}` | Delete a product without movement history | 204 No Content |

### Inventory

| Method | Endpoint                        | Description                                      | Success     |
| ------ | ------------------------------- | ------------------------------------------------ | ----------- |
| GET    | `/locations`                    | List stock locations                             | 200 OK      |
| GET    | `/products/{id}/stock`          | Get a product's balances by location             | 200 OK      |
| GET    | `/stock-movements`              | List movements; optionally filter by `productId` | 200 OK      |
| GET    | `/stock-movements/{id}`         | Get one movement                                 | 200 OK      |
| POST   | `/stock-movements`              | Record a receipt, transfer, or issue             | 201 Created |
| POST   | `/stock-movements/{id}/reverse` | Record a linked reversal                         | 201 Created |

The API also uses status codes such as **400 Bad Request**, **404 Not Found**, **409 Conflict**, and **503 Service Unavailable** where appropriate.

## Data Models

### Product

| Property       | Type      | Description                     |
| -------------- | --------- | ------------------------------- |
| `Id`           | `int`     | Database-generated identifier   |
| `Name`         | `string`  | Product name (1–120 characters) |
| `Category`     | `string`  | Category (1–60 characters)      |
| `PurchaseCost` | `decimal` | Cost, zero or greater           |
| `SellingPrice` | `decimal` | Price, greater than zero        |

### StockLocation

| Property   | Type     | Description                    |
| ---------- | -------- | ------------------------------ |
| `Id`       | `int`    | Location identifier            |
| `Name`     | `string` | Location name                  |
| `IsUsable` | `bool`   | Whether stock counts as usable |

### StockMovement

| Property             | Type       | Description                             |
| -------------------- | ---------- | --------------------------------------- |
| `Id`                 | `int`      | Movement identifier                     |
| `ProductId`          | `int`      | Product being moved                     |
| `FromLocationId`     | `int?`     | Source; `null` for external receipts    |
| `ToLocationId`       | `int?`     | Destination; `null` for external issues |
| `Quantity`           | `int`      | Positive quantity                       |
| `OccurredAtUtc`      | `DateTime` | Server-generated UTC timestamp          |
| `Reference`          | `string?`  | Optional reference                      |
| `ReversesMovementId` | `int?`     | Original movement ID when reversing     |
| `ReversalReason`     | `string?`  | Explanation for a reversal              |

A stock movement uses **one or two locations**. Both source and destination cannot be `null`, and they cannot be the same location.

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Git
- EF Core CLI (`dotnet-ef`) compatible with EF Core 10

If needed, install the EF Core CLI:

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

### 3. Apply database migrations

```powershell
dotnet ef database update
```

This creates or updates the local SQLite database, including the seeded inventory locations.

### 4. Run the API

```powershell
dotnet run --project InventoryApi.csproj --urls http://localhost:5050
```

Try these URLs:

- `http://localhost:5050/health`
- `http://localhost:5050/products`
- `http://localhost:5050/locations`

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

The response includes the new product ID. Use that ID in later requests.

### Receive 10 units into Store

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

### Transfer 3 units from Store to Locker

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

After these two movements, product 1 has **10 total units** and **7 usable units** (7 in Store, 3 in Locker), assuming there were no earlier movements.

### View stock balances

**GET** `/products/1/stock`

### Reverse a movement

**POST** `/stock-movements/2/reverse`

```json
{
  "reason": "Transferred to the wrong location"
}
```

Replace the example product and movement IDs with IDs returned by your own API.

## Automated Testing

The solution contains an xUnit integration-test project. Tests use ASP.NET Core's test server and isolated SQLite databases so they do not modify the normal local `inventory.db`.

Run all tests:

```powershell
dotnet test InventoryApi.sln
```

Run only reversal tests:

```powershell
dotnet test InventoryApi.sln --filter "FullyQualifiedName~StockMovementReversalTests"
```

The test suite covers:

- Seeded locations and usable-stock flags
- Receipts and transfers updating stock balances
- Insufficient-stock rejection
- Simultaneous issue attempts
- Transaction lock contention
- Transfer, receipt, and issue reversals
- Duplicate reversal prevention
- Invalid reversal reasons and missing movement IDs
- Concurrent reversal attempts
- Deletion protection for products with movement history

## Project Structure

```text
InventoryApi/
├── Data/
│   └── InventoryDbContext.cs
├── Models/
│   ├── Product.cs
│   ├── CreateProductRequest.cs
│   ├── UpdateProductRequest.cs
│   ├── StockLocation.cs
│   ├── StockMovement.cs
│   ├── CreateStockMovementRequest.cs
│   └── ReverseStockMovementRequest.cs
├── Migrations/
│   ├── (InitialCreate migration files)
│   ├── (AddStockLocationsAndMovements migration files)
│   ├── (AddStockMovementReversals migration files)
│   └── InventoryDbContextModelSnapshot.cs
├── tests/
│   └── InventoryApi.Tests/
│       ├── InventoryApi.Tests.csproj
│       ├── InventoryApiFactory.cs
│       ├── InventoryEndpointsTests.cs
│       └── StockMovementReversalTests.cs
├── Program.cs
├── InventoryApi.csproj
├── InventoryApi.sln
├── appsettings.json
├── .gitignore
└── README.md
```

## Development Progress

| Day   | Milestone                                                               | Status    |
| ----- | ----------------------------------------------------------------------- | --------- |
| Day 1 | Project setup and basic API endpoints                                   | Completed |
|       | Product models, GET and POST endpoints, validation                      | Completed |
|       | SQLite, Entity Framework Core, and migrations                           | Completed |
|       | Product update and delete endpoints                                     | Completed |
|       | Inventory locations, stock movement history, and stock balances         | Completed |
|       | Automated integration tests, transaction safety, and movement reversals | Completed |

## Current Limitations

- No graphical frontend or user authentication/authorisation.
- Sales transactions, commissions, and reporting are not yet implemented.
- Stock balance calculations currently load movement history into memory rather than aggregating in SQL.
- SQLite supports one active writer at a time; excessive lock contention can return HTTP 503.
- No request idempotency keys for safely retrying uncertain requests.
- Corrections record a reason but not an authenticated staff identity.
- Product archiving is not yet implemented; products without movement history can be permanently deleted.
- Automated tests run locally; GitHub Actions continuous integration is not configured yet.

## Planned Features

- Sales transactions linked to stock deductions
- Revenue, cost, gross profit, and category-based commission calculations
- 3 AM business-day cutoff for sales reporting
- Previous completed month's historical reports (rather than real-time graphs)
- Product archiving / soft deletion
- Authentication and role-based authorisation
- Request idempotency and more robust error handling
- Database-side stock balance aggregation
- JavaScript frontend
- GitHub Actions CI and deployment

## Author

GitHub: [terrancedoo](https://github.com/terrancedoo)
