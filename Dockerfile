# ---- build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore (copy csproj/sln first for layer caching)
COPY ReceiptsAPI.slnx ./
COPY src/Receipts.Core/Receipts.Core.csproj src/Receipts.Core/
COPY src/Receipts.Infrastructure/Receipts.Infrastructure.csproj src/Receipts.Infrastructure/
COPY src/Receipts.API/Receipts.API.csproj src/Receipts.API/
RUN dotnet restore src/Receipts.API/Receipts.API.csproj

# Build + publish
COPY . .
RUN dotnet publish src/Receipts.API/Receipts.API.csproj -c Release -o /app/publish

# ---- runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Receipts.API.dll"]
