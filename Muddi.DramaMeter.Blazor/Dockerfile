FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Muddi.DramaMeter.Blazor/Muddi.DramaMeter.Blazor.csproj", "Muddi.DramaMeter.Blazor/"]
RUN dotnet restore "Muddi.DramaMeter.Blazor/Muddi.DramaMeter.Blazor.csproj"
COPY . .
WORKDIR "/src/Muddi.DramaMeter.Blazor"
RUN dotnet build "./Muddi.DramaMeter.Blazor.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Muddi.DramaMeter.Blazor.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Muddi.DramaMeter.Blazor.dll"]
