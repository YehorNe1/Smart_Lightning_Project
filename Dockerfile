# === 1) Build Stage ===
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY MyIoTProject.sln ./
COPY MyIoTProject.Core/*.csproj MyIoTProject.Core/
COPY MyIoTProject.Application/*.csproj MyIoTProject.Application/
COPY MyIoTProject.Infrastructure/*.csproj MyIoTProject.Infrastructure/
COPY MyIoTProject.Presentation/*.csproj MyIoTProject.Presentation/

# Restore NuGet packages
RUN dotnet restore

# Copy all source code
COPY . .

# Ensure optional appsettings.Development.json exists (prevents MSB3030 error)
RUN test -f MyIoTProject.Presentation/appsettings.Development.json \
    || echo "{}" > MyIoTProject.Presentation/appsettings.Development.json

# Publish Release build to /app/publish
RUN dotnet publish MyIoTProject.Presentation/MyIoTProject.Presentation.csproj \
    -c Release \
    -o /app/publish

# === 2) Runtime Stage ===
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Copy published output
COPY --from=build /app/publish .

# Listen on the port provided by Render
ENV ASPNETCORE_URLS=http://0.0.0.0:${PORT}

# Launch the app
ENTRYPOINT ["dotnet", "MyIoTProject.Presentation.dll"]