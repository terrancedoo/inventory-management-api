# Inventory Management API

A portfolio REST API built with **C#**, **ASP.NET Core (.NET 10)**, **Entity Framework Core**, and **SQLite**.

This project demonstrates backend development, database persistence, CRUD operations, input validation, asynchronous programming, and Git-based version control. It is inspired by inventory and sales workflows, using fictional demonstration data.

## Technology Stack

- C# and ASP.NET Core Minimal APIs (.NET 10)
- Entity Framework Core 10 with SQLite
- LINQ and asynchronous database operations
- Git and GitHub
- PowerShell for manual API testing

**Planned:** JavaScript frontend, automated tests, inventory movements, sales, and reporting.

## Implemented Features

- Retrieve all products or a product by ID
- Create products with server-generated IDs
- Update product information
- Delete products (permanent deletion for now)
- Validate names, categories, purchase costs, and selling prices
- Return appropriate HTTP status codes
- Persist products in a local SQLite database
- Manage database schema with EF Core migrations

### Inventory Management

- Four inventory locations: Store, Locker, Office and Wine Chiller
- Record stock receipts from external sources
- Transfer stock between locations
- Record outgoing stock issues
- Maintain a timestamped stock movement history
- Calculate stock balances from recorded movements
- Calculate usable stock separately from total stock
- Validate products, locations and movement quantities
- Reject movements that exceed available stock
- Prevent deletion of products with stock movement history

**Usable Stock:** Store + Wine Chiller

**Total Stock:** Store + Locker + Office + Wine Chiller

### Stock Movement Rules

Stock movements use positive quantities and nullable source
and destination locations:

- Receipt: source is null, destination is a stock location
- Transfer: both source and destination are stock locations
- Issue: source is a stock location, destination is null

Both locations cannot be null, and transfers to the same
location are rejected.

Current stock validation prevents negative balances during
sequential requests. Concurrency protection is planned.

## API Endpoints

| Method | Route                           | Description                                 | Success        |
| ------ | ------------------------------- | ------------------------------------------- | -------------- |
| GET    | `/`                             | Application information                     | 200 OK         |
| GET    | `/health`                       | API health check                            | 200 OK         |
| GET    | `/about`                        | Developer information                       | 200 OK         |
| GET    | `/products`                     | List products                               | 200 OK         |
| GET    | `/products/{id}`                | Get product by ID                           | 200 OK         |
| POST   | `/products`                     | Create product                              | 201 Created    |
| PUT    | `/products/{id}`                | Replace editable product fields             | 204 No Content |
| DELETE | `/products/{id}`                | Permanently delete product                  | 204 No Content |
| GET    | `/locations`                    | Retrieve inventory locations                |
| GET    | `/products/{id}/stock`          | Retrieve product stock balances             |
| GET    | `/stock-movements`              | Retrieve stock movement history             |
| GET    | `/stock-movements/{id}`         | Retrieve a movement by ID                   |
| POST   | `/stock-movements`              | Record a receipt, transfer or issue         |
| POST   | `/stock-movements/{id}/reverse` | Reverse a movement while preserving history |

Invalid product data returns **400 Bad Request**. Requests for products that do not exist return **404 Not Found**.

## Product Model

| Property       | C# Type   | Description                     |
| -------------- | --------- | ------------------------------- |
| `Id`           | `int`     | Database-generated product ID   |
| `Name`         | `string`  | Product name (1–120 characters) |
| `Category`     | `string`  | Category (1–60 characters)      |
| `PurchaseCost` | `decimal` | Zero or greater                 |
| `SellingPrice` | `decimal` | Greater than zero               |

### Example: Create a Product

**Request:** `POST /products`

```json
{
  "name": "Demo Tequila",
  "category": "Tequila",
  "purchaseCost": 48.5,
  "sellingPrice": 125.0
}
```

**Response:** `201 Created`, with the created product and a `Location` header.

### Example: Update a Product

**Request:** `PUT /products/1`

```json
{
  "name": "Demo Premium Tequila",
  "category": "Tequila",
  "purchaseCost": 60.0,
  "sellingPrice": 150.0
}
```

**Response:** `204 No Content`. All four editable fields are required.

### Example: Delete a Product

**Request:** `DELETE /products/1`

**Response:** `204 No Content`. The product is removed permanently. Use only test records until soft deletion is implemented.

## Getting Started

### Requirements

- .NET 10 SDK
- Git
- `dotnet-ef` CLI compatible with EF Core 10

### Clone and Restore

```powershell
git clone https://github.com/terrancedoo/inventory-management-api.git
cd inventory-management-api
dotnet restore
```

### Create the Local Database

```powershell
dotnet ef database update
```

This applies the committed migrations and creates the local SQLite database. The database file is excluded from Git.

### Run the API

```powershell
dotnet run --urls http://localhost:5050
```

Test these URLs in a browser:

- `http://localhost:5050/health`
- `http://localhost:5050/products`

A new database has no products until you create them through `POST /products`.

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
│   └── CreateStockMovementRequest.cs
├── Migrations/
│   ├── (InitialCreate migration files)
│   ├── (AddStockLocationsAndMovements migration files)
│   └── InventoryDbContextModelSnapshot.cs
├── Program.cs
├── InventoryApi.csproj
├── appsettings.json
├── .gitignore
└── README.md

## Progress

## Development Progress

| Day | Milestone | Status |
| --- | --- | --- |
| Day 1 | Project setup and basic API endpoints | Completed |
| | Product models, GET and POST endpoints, validation | Completed |
| | SQLite, Entity Framework Core, and migrations | Completed |
| | Product update and delete endpoints | Completed |
| | Inventory locations, stock movement history and stock balances | Completed |
| | Automated integration tests, transaction safety and movement reversals | Completed |

## Planned Features

- Inventory locations: Store, Locker, Office, Wine Chiller
- Stock receipts, deductions, and transfers with movement history
- Prevention of negative stock balances
- Sales transactions and product margin calculations
- Previous completed month's historical reporting (no real-time graphs)
- 3 AM business-day cutoff for reports
- JavaScript frontend
- Automated unit and integration tests
- Product archiving instead of permanent deletion
- Authentication and authorisation

## Current Limitations

- Products can be managed through the API, but there is no graphical frontend yet.
- Inventory quantities, stock movements, and sales are not yet implemented.
- DELETE permanently removes records; historical references will require a different strategy.
- The API does not currently implement authentication or authorisation.
- The SQLite database is intended for local development.
- Automated tests are planned; current endpoint testing is manual.

## Author

GitHub: [terrancedoo](https://github.com/terrancedoo)
```
