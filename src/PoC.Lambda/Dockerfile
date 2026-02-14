FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["PoC.Lambda.csproj", "./"]
RUN dotnet restore "PoC.Lambda.csproj"
COPY . .
WORKDIR "/src"
RUN dotnet build "PoC.Lambda.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "PoC.Lambda.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /var/task
COPY --from=publish /app/publish .
CMD ["PoC.Lambda::PoC.Lambda.Function::FunctionHandler"]
