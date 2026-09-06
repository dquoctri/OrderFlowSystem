FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Directory.Build.props", "Directory.Packages.props", "./"]
COPY ["src/Web/OrderFlow.Bff/OrderFlow.Bff.csproj", "src/Web/OrderFlow.Bff/"]
RUN dotnet restore "src/Web/OrderFlow.Bff/OrderFlow.Bff.csproj"
COPY . .
WORKDIR "/src/src/Web/OrderFlow.Bff"
RUN dotnet publish "OrderFlow.Bff.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl adduser \
    && rm -rf /var/lib/apt/lists/* \
    && adduser --disabled-password --gecos "" appuser
WORKDIR /app
COPY --from=build /app/publish .
USER appuser
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
HEALTHCHECK --interval=10s --timeout=3s --retries=5 CMD ["curl", "-f", "http://localhost:8080/health/live"]
ENTRYPOINT ["dotnet", "OrderFlow.Bff.dll"]
