# === 1) Build Stage ===
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy the solution file and project files
COPY MyIoTProject.sln ./
COPY MyIoTProject.Core/*.csproj MyIoTProject.Core/
COPY MyIoTProject.Application/*.csproj MyIoTProject.Application/
COPY MyIoTProject.Infrastructure/*.csproj MyIoTProject.Infrastructure/
COPY MyIoTProject.Presentation/*.csproj MyIoTProject.Presentation/

# Restore NuGet packages
RUN dotnet restore

# Copy all source code and publish a Release build
COPY . .
RUN dotnet publish MyIoTProject.Presentation/MyIoTProject.Presentation.csproj \
    -c Release \
    -o /app/publish

# === 2) Runtime Stage ===
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Copy published output from the build stage
COPY --from=build /app/publish .

# Instruct ASP.NET Core to listen on the port provided by Render
ENV ASPNETCORE_URLS=http://0.0.0.0:${PORT}

# Start the application
ENTRYPOINT ["dotnet", "MyIoTProject.Presentation.dll"]