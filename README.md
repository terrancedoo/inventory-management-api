# Inventory Management API

A C# ASP.NET Core portfolio project for managing
products, inventory, sales, and business reporting.

## Project Overview

This application demonstrates backend development
using C#, ASP.NET Core, and REST APIs.

The project is inspired by real-world inventory
management workflows and uses fictional data.

## Technology Stack

- C#
- ASP.NET Core (.NET 10)
- REST APIs
- Git and GitHub
- SQLite (planned)
- JavaScript frontend (planned)

## Implemented Features

### Day 1 - API Foundation

- Created ASP.NET Core Minimal API
- Added application information endpoint
- Added health check endpoint
- Added developer information endpoint

### Day 2 - Product Management

- Created a Product class
- Implemented in-memory product storage
- Retrieve all products
- Retrieve a product by ID
- Create new products using POST requests
- Validate incoming product information
- Return appropriate HTTP status codes

## API Endpoints

| Method | Endpoint       | Description             |
| ------ | -------------- | ----------------------- |
| GET    | /              | Application information |
| GET    | /health        | Health check            |
| GET    | /about         | Developer information   |
| GET    | /products      | Retrieve all products   |
| GET    | /products/{id} | Retrieve product by ID  |
| POST   | /products      | Create a new product    |

## Product Data Structure

Each product contains:

- Id
- Name
- Category
- PurchaseCost
- SellingPrice

## Example POST Request

Endpoint: POST /products

Request body:

```json
{
  "name": "Demo Tequila",
  "category": "Tequila",
  "purchaseCost": 48.5,
  "sellingPrice": 125.0
}
```

## Validation Rules

- Product name must not be empty.
- Product category must not be empty.
- Purchase cost cannot be negative.
- Selling price must be greater than zero.

## HTTP Response Codes

- 200 OK - Successful GET request
- 201 Created - Product created successfully
- 400 Bad Request - Invalid product data
- 404 Not Found - Product does not exist

## Running the Application

Requirements:

- .NET 10 SDK

Run:

```powershell
dotnet run --urls http://localhost:5050
```

Example URLs:

- http://localhost:5050/
- http://localhost:5050/health
- http://localhost:5050/products
- http://localhost:5050/products/1

## Current Limitations

Product data is stored in memory.

New products are lost when the application restarts.

Database persistence will be implemented using SQLite.

## Planned Features

- SQLite database integration
- Inventory location management
- Stock movement records
- Sales management
- Historical monthly reporting
- Automated unit tests
- JavaScript frontend

## Reporting Design

Analytics will display data from the previous
completed month rather than real-time graphs.

Sales reporting will support a 3 AM business-day cutoff.

## Development Progress

- Day 1: API foundation completed
- Day 2: Product management API completed
