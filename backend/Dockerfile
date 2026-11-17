# syntax=docker/dockerfile:1

# ---- build: restore (cached layer) and compile the whole solution ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY SchoolAdmission.slnx ./
COPY src/SchoolAdmission.Api/SchoolAdmission.Api.csproj src/SchoolAdmission.Api/
COPY tests/SchoolAdmission.Api.Tests/SchoolAdmission.Api.Tests.csproj tests/SchoolAdmission.Api.Tests/
RUN dotnet restore SchoolAdmission.slnx
COPY . .
RUN dotnet build SchoolAdmission.slnx -c Release --no-restore

# ---- test: `docker compose --profile test run --rm backend-tests` ----
FROM build AS test
ENTRYPOINT ["dotnet", "test", "SchoolAdmission.slnx", "-c", "Release", "--no-build", "--logger", "console;verbosity=normal"]

# ---- publish ----
FROM build AS publish
RUN dotnet publish src/SchoolAdmission.Api/SchoolAdmission.Api.csproj -c Release --no-build -o /app/publish

# ---- runtime: small ASP.NET image, non-root user ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production
COPY --from=publish /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "SchoolAdmission.Api.dll"]
