FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/PoC.Lambda/PoC.Lambda.csproj", "src/PoC.Lambda/"]
COPY ["src/PoC.Shared/PoC.Shared.csproj", "src/PoC.Shared/"]
RUN dotnet restore "src/PoC.Lambda/PoC.Lambda.csproj"
COPY src/PoC.Lambda/ src/PoC.Lambda/
COPY src/PoC.Shared/ src/PoC.Shared/
RUN dotnet build "src/PoC.Lambda/PoC.Lambda.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "src/PoC.Lambda/PoC.Lambda.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /var/task
COPY --from=publish /app/publish .
CMD ["PoC.Lambda::PoC.Lambda.Function::FunctionHandler"]
