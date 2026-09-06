FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Directory.Build.props", "Directory.Packages.props", "./"]
COPY ["src/Web/OrderFlow.Web/OrderFlow.Web.csproj", "src/Web/OrderFlow.Web/"]
RUN dotnet restore "src/Web/OrderFlow.Web/OrderFlow.Web.csproj"
COPY . .
WORKDIR "/src/src/Web/OrderFlow.Web"
RUN dotnet publish "OrderFlow.Web.csproj" -c Release -o /app/publish --no-restore

FROM nginxinc/nginx-unprivileged:1.27-alpine AS runtime
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html
COPY infra/web/nginx.conf /etc/nginx/nginx.conf
EXPOSE 8080
