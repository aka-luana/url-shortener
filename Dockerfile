FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY url-shortener.csproj ./
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
# Diagnóstico: atualiza o OpenSSL/CA da imagem base para testar se a falha de
# handshake TLS com o MongoDB Atlas ("tlsv1 alert internal error") é causada
# por um bug/patch pendente na versão do libssl3t64 que vem na imagem oficial.
RUN apt-get update \
    && apt-get upgrade -y libssl3t64 ca-certificates \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "url-shortener.dll"]
