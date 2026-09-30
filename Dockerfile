FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Necessário só quando esse estágio é compilado sob emulação QEMU (build
# cross-arch num Mac Apple Silicon fazendo a imagem para x86_64): o QEMU não
# suporta a proteção de memória W^X que o .NET usa por padrão, e o processo
# aborta (SIGABRT) durante o restore/publish. Em hardware nativo (é o caso do
# Fargate, que roda x86_64 real) essa variável não faz diferença.
ENV DOTNET_EnableWriteXorExecute=0
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
