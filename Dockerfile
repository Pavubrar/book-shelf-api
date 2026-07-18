# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

WORKDIR /src

COPY BookShelf.Api.csproj .

RUN dotnet restore BookShelf.Api.csproj

COPY . .

RUN dotnet publish BookShelf.Api.csproj -c Release -o /app/publish


# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0

WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "BookShelf.Api.dll"]