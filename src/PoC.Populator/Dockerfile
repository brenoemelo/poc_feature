FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/PoC.Populator/PoC.Populator.csproj", "src/PoC.Populator/"]
COPY ["src/PoC.Shared/PoC.Shared.csproj", "src/PoC.Shared/"]
RUN dotnet restore "src/PoC.Populator/PoC.Populator.csproj"
COPY src/PoC.Populator/ src/PoC.Populator/
COPY src/PoC.Shared/ src/PoC.Shared/
RUN dotnet build "src/PoC.Populator/PoC.Populator.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "src/PoC.Populator/PoC.Populator.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /var/task
COPY --from=publish /app/publish .
CMD ["PoC.Populator::PoC.Populator.Functions.PopulatorWorkerFunction::FunctionHandler"]
