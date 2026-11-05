# MechanicShop

MechanicShop is a workshop management system for handling branches, customers, vehicles, work orders, labor assignments, repair tasks, invoices, and schedules through a REST API and a Blazor WebAssembly client.

## Features

- Manage branches and employee branch memberships.
- Manage customers, their vehicles, repair tasks, and parts.
- Create and update work orders, assign labor, move appointments, change order state, and update repair tasks.
- Retrieve daily schedules using a requested date and time zone.
- Issue invoices, record payments, and download invoice PDFs.
- Retrieve dashboard statistics and operating hours.
- Authenticate with JWT and refresh tokens.
- Send work order change notifications through SignalR.

## Architecture

The solution separates HTTP presentation, application use cases, domain rules, and infrastructure concerns. The API connects the application and infrastructure layers. Shared contracts are used by the API and client.

~~~mermaid
flowchart LR
    Client[MechanicShop.Client<br/>Blazor WebAssembly] --> Contracts[MechanicShop.Contracts]
    Client --> Api[MechanicShop.Api]
    Api --> Contracts
    Api --> Application[MechanicShop.Application]
    Api --> Infrastructure[MechanicShop.Infrastructure]
    Application --> Domain[MechanicShop.Domain]
    Infrastructure --> Application
    Infrastructure --> Domain
    Infrastructure --> Sql[(SQL Server)]
    Infrastructure --> Redis[(Redis)]
    Api --> Hub[SignalR Hub<br/>/hubs/workorders]
~~~

### Solution Projects

| Project | Responsibility |
| --- | --- |
| MechanicShop.Api | HTTP endpoints, authentication and authorization, OpenAPI, health checks, and hosting the Blazor client. |
| MechanicShop.Application | Commands, queries, handlers, validators, and application use cases. |
| MechanicShop.Domain | Domain entities, business rules, and operation results. |
| MechanicShop.Infrastructure | EF Core, SQL Server, ASP.NET Core Identity, Redis, PDF generation, and external services. |
| MechanicShop.Contracts | Request and response models shared by the API and client. |
| MechanicShop.Client | Blazor WebAssembly user interface. |
| tests/MechanicShop.Api.IntegrationTests | HTTP and SignalR integration tests using WebApplicationFactory and Testcontainers. |
| tests/MechanicShop.Application.SubcutaneiousTests | Application tests using an integration-style setup. |
| tests/MechanicShop.Application.UniTests | Application unit tests. |
| tests/MechanicShop.Domain.UniTests | Domain unit tests. |
| tests/MechanicShop.Tests.Common | Shared test data and helpers. |

## Domain Model and Database

A customer can own multiple vehicles. A work order belongs to a branch and a vehicle, can be assigned to a labor employee, and can include multiple repair tasks. An invoice is associated with a work order. Employee branch memberships connect employees to the branches they may access and identify a default branch.

~~~mermaid
erDiagram
    CUSTOMER ||--o{ VEHICLE : owns
    BRANCH ||--o{ EMPLOYEE_BRANCH_MEMBERSHIP : permits
    EMPLOYEE ||--o{ EMPLOYEE_BRANCH_MEMBERSHIP : belongs_to
    BRANCH ||--o{ WORK_ORDER : contains
    VEHICLE ||--o{ WORK_ORDER : serviced_by
    EMPLOYEE o|--o{ WORK_ORDER : assigned_labor
    WORK_ORDER }o--o{ REPAIR_TASK : includes
    WORK_ORDER ||--o| INVOICE : billed_by
~~~

AppDbContext uses EF Core. Entity configurations are in MechanicShop.Infrastructure/Date/Configurations, and schema changes are managed with EF Core migrations. During Development startup, ApplicationDbContextInitialiser applies migrations and seeds development data.

Work orders and invoices use EF Core global query filters that apply the current user's branch access scope. Administrative operations that use IgnoreQueryFilters() should be reviewed carefully.

## API

Most resource routes use the /api/v1.0 prefix.

| Resource | Examples |
| --- | --- |
| workorders | List and retrieve work orders, create and update orders, assign labor, modify repair tasks, delete orders, and retrieve a daily schedule. |
| customers | List, retrieve, create, update, and delete customers. |
| repair-tasks | List, retrieve, create, update, and delete repair tasks. |
| labors | Retrieve labor employees. |
| branches | List branches, retrieve branch employees, create a branch, and manage branch memberships and status. |
| invoices | Issue an invoice, retrieve invoice details, download a PDF, and record payments. |
| dashboard | Retrieve dashboard statistics. |
| employees | Employee account creation endpoints. |
| identity | Generate and refresh tokens and retrieve the current user's claims. |
| settings | Retrieve operating hours. |

OpenAPI, Swagger UI, and Scalar API reference are enabled in Development.

## Authentication and Branch Access

The API uses ASP.NET Core Identity for user accounts and JWT Bearer authentication for API requests. Authorization policies evaluate the authenticated user's role and access scope. Work order and invoice queries are filtered using the current branch context.

Authorization decisions must come from authenticated server-side identity and persisted employee and branch data. Client-supplied role, access scope, or branch identifiers are not an authorization source.

## Technology Stack

- .NET 10 and ASP.NET Core
- Blazor WebAssembly
- Entity Framework Core and SQL Server
- ASP.NET Core Identity and JWT Bearer authentication
- MediatR and FluentValidation
- Redis and HybridCache
- SignalR
- QuestPDF
- Serilog and OpenTelemetry
- xUnit v3, Microsoft Testing Platform, Coverlet.MTP, and Testcontainers

## Local Setup

### Prerequisites

- .NET SDK 10
- A reachable SQL Server instance
- A reachable Redis instance
- Docker Desktop for API integration tests that use Testcontainers

Check the installed SDK:

~~~powershell
dotnet --version
~~~

### Configuration

Configure local secrets outside source control. The application reads these settings:

- ConnectionStrings:DefaultConnection
- ConnectionStrings:Redis
- JwtSettings:Secret
- JwtSettings:Issuer
- JwtSettings:Audience
- JwtSettings:TokenExpirationInMinutes
- AppSettings:CorsPolicyName
- AppSettings:AllowedOrigins
- EmailSettings:Host
- EmailSettings:Port
- EmailSettings:SenderName
- EmailSettings:SenderEmail
- EmailSettings:Username
- EmailSettings:Password

For example, from the repository root:

~~~powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<sql-server-connection-string>" --project MechanicShop.Api
dotnet user-secrets set "ConnectionStrings:Redis" "localhost:6379" --project MechanicShop.Api
dotnet user-secrets set "JwtSettings:Secret" "<random-secret-at-least-32-utf8-bytes>" --project MechanicShop.Api
dotnet user-secrets set "JwtSettings:Issuer" "MechanicShop" --project MechanicShop.Api
dotnet user-secrets set "JwtSettings:Audience" "MechanicShop.Client" --project MechanicShop.Api
dotnet user-secrets set "JwtSettings:TokenExpirationInMinutes" "60" --project MechanicShop.Api
dotnet user-secrets set "AppSettings:CorsPolicyName" "LocalClient" --project MechanicShop.Api
dotnet user-secrets set "AppSettings:AllowedOrigins:0" "https://localhost:####" --project MechanicShop.Api
dotnet user-secrets set "EmailSettings:Host" "<smtp-host>" --project MechanicShop.Api
dotnet user-secrets set "EmailSettings:Port" "587" --project MechanicShop.Api
dotnet user-secrets set "EmailSettings:SenderName" "MechanicShop" --project MechanicShop.Api
dotnet user-secrets set "EmailSettings:SenderEmail" "<sender-email>" --project MechanicShop.Api
dotnet user-secrets set "EmailSettings:Username" "<smtp-username>" --project MechanicShop.Api
dotnet user-secrets set "EmailSettings:Password" "<smtp-password>" --project MechanicShop.Api
~~~

Replace the placeholders with values for your local environment. Do not commit passwords, JWT keys, SMTP credentials, or production connection strings.

### Restore and Run

From the repository root:

~~~powershell
dotnet restore MechanicShop.slnx
dotnet run --project MechanicShop.Api/MechanicShop.Api.csproj
~~~

The launch profiles configure a local Development environment. /health reports the registered health checks, including SQL Server and Redis. Both services must be available for normal local operation.

### Database Initialization

During Development startup, the application applies EF Core migrations and seeds development data. Review ApplicationDbContextInitialiser and the selected environment before connecting to a database containing important data. Integration tests use isolated containers and databases.

## Tests and Coverage

The test projects use xUnit v3 with Microsoft Testing Platform. API integration tests start SQL Server and Redis containers through Testcontainers, so Docker must be running.

Run the solution tests with:

~~~powershell
dotnet test MechanicShop.slnx
~~~

In the latest local verification, the three targeted tests for password change, inactive-branch SignalR membership, and work order filtering passed: **3 passed, 0 failed, 0 skipped**. The complete solution test suite was not run in this review.

Coverlet.MTP is referenced by the test projects. A coverage report was not generated in this review, so no coverage percentage is reported here.

## Logging and Health

- Serilog records application and HTTP request events.
- OpenTelemetry instruments HTTP and application activity and can export through OTLP.
- /health reports SQL Server and Redis health.
- QuestPDF generates invoice documents.
- SignalR is mapped at /hubs/workorders for work order change notifications.

## Follow-up Before Release

- Run the complete solution test suite after the remaining local changes are reconciled.
- Generate and retain a fresh coverage report before publishing coverage percentages.
- Review secrets, CORS origins, database settings, and external service settings for the target environment.
