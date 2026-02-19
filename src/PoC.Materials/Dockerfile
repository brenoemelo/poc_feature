FROM public.ecr.aws/lambda/dotnet:10 AS base

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/PoC.Materials/PoC.Materials.csproj", "PoC.Materials/"]
COPY ["src/PoC.Shared/PoC.Shared.csproj", "PoC.Shared/"]
RUN dotnet restore "PoC.Materials/PoC.Materials.csproj"
COPY src/PoC.Materials/ PoC.Materials/
COPY src/PoC.Shared/ PoC.Shared/
WORKDIR "/src/PoC.Materials"
RUN dotnet build "PoC.Materials.csproj" --configuration Release --output /app/build

FROM build AS publish
RUN dotnet publish "PoC.Materials.csproj" --configuration Release --output /app/publish

FROM base AS final
WORKDIR /var/task
COPY --from=publish /app/publish .
CMD ["PoC.Materials"]
