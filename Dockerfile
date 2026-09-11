FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["FiapDonateWorker.slnx", "./"]
COPY ["src/FiapDonateWorker.Api/FiapDonateWorker.Api.csproj", "src/FiapDonateWorker.Api/"]
COPY ["src/FiapDonateWorker.Infrastructure/FiapDonateWorker.Infrastructure.csproj", "src/FiapDonateWorker.Infrastructure/"]
COPY ["src/FiapDonateWorker.Domain/FiapDonateWorker.Domain.csproj", "src/FiapDonateWorker.Domain/"]
RUN dotnet restore "src/FiapDonateWorker.Api/FiapDonateWorker.Api.csproj"

COPY src/ src/
RUN dotnet publish "src/FiapDonateWorker.Api/FiapDonateWorker.Api.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "FiapDonateWorker.Api.dll"]
