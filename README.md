# Inventory Management API

A C# backend development portfolio project built
using ASP.NET Core (.NET 10).

## Project Overview

This project demonstrates backend API development
for an inventory and sales management application.

It is based on real-world business workflow concepts,
using fictional sample data.

## Technology Stack

- C#
- ASP.NET Core (.NET 10)
- Git
- SQLite (planned)
- JavaScript frontend (planned)

## Implemented Features

- Application information endpoint
- API health check endpoint
- Developer information endpoint

## API Endpoints

| Method | Endpoint | Description             |
| ------ | -------- | ----------------------- |
| GET    | /        | Application information |
| GET    | /health  | API health status       |
| GET    | /about   | Developer information   |

## Running the Application

Requirements:

- .NET 10 SDK

Open a terminal in the project directory.

Run:

dotnet run --urls http://localhost:5050

Visit:

- http://localhost:5050/
- http://localhost:5050/health
- http://localhost:5050/about

## Planned Features

- Product management
- SQLite database integration
- Inventory stock movements
- Sales transactions
- Monthly historical reporting
- Automated testing

## Reporting Rules

- Analytics will display the previous completed month.
- Reporting will account for a 3 AM business-day cutoff.
- Real-time graph updates are not required.

## Development Status

Day 1: Initial API endpoints implemented.
