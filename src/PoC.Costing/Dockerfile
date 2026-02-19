FROM public.ecr.aws/lambda/dotnet:10 AS base

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/PoC.Costing/PoC.Costing.csproj", "PoC.Costing/"]
COPY ["src/PoC.Shared/PoC.Shared.csproj", "PoC.Shared/"]
RUN dotnet restore "PoC.Costing/PoC.Costing.csproj"
COPY src/PoC.Costing/ PoC.Costing/
COPY src/PoC.Shared/ PoC.Shared/
WORKDIR "/src/PoC.Costing"
RUN dotnet build "PoC.Costing.csproj" --configuration Release --output /app/build

FROM build AS publish
RUN dotnet publish "PoC.Costing.csproj" --configuration Release --output /app/publish

FROM base AS final
WORKDIR /var/task
COPY --from=publish /app/publish .
