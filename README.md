# BookShelf API

ASP.NET Core Web API for the BookShelf full-stack app. The API uses Entity Framework Core, ASP.NET Identity, JWT authentication, and SQL Server-compatible migrations so it can run against Azure SQL Database.

# Getting Started

1. Install .NET 9 SDK and SQL Server LocalDB or SQL Server.
2. Set the connection string in `appsettings.Development.json` or with an environment variable:
   `ConnectionStrings__DefaultConnection`
3. Run the API from `backend/BookShelf.Api`:
   `dotnet restore`
   `dotnet run`

For Azure App Service, configure the production connection string as an app setting or connection string named `DefaultConnection`. A typical Azure SQL string looks like this:

`Server=tcp:<server-name>.database.windows.net,1433;Initial Catalog=BookShelfDb;Persist Security Info=False;User ID=<admin-user>;Password=<password>;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;`

# Database Notes

- SQLite has been removed from this API.
- The app now uses SQL Server only, which is compatible with Azure SQL Database.
- On startup the app runs `Database.Migrate()` automatically, so the Azure SQL database schema is created or updated during deployment.

# File Storage Notes

Uploaded PDFs and audio files are stored in Azure Blob Storage. Configure:

- `AzureStorage__ConnectionString`
- `AzureStorage__ContainerName` (default: `uploads`)

# Build

Run:

`dotnet build`
