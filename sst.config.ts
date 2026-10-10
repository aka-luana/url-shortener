/// <reference path="./.sst/platform/config.d.ts" />

export default $config({
  app(input) {
    return {
      name: "url-shortener",
      removal: input?.stage === "production" ? "retain" : "remove",
      protect: input?.stage === "production",
      home: "aws",
      providers: { aws: { region: "us-east-1" } },
    };
  },
  async run() {
    const hashidsSalt = new sst.Secret("HashidsSalt");
    const otelExporterEndpoint = new sst.Secret("OtelExporterEndpoint");
    const otelExporterHeaders = new sst.Secret("OtelExporterHeaders");

    const vpc = new sst.aws.Vpc("Vpc");
    const cluster = new sst.aws.Cluster("Cluster", { vpc });

    const api = new sst.aws.ApiGatewayV2("Gateway", {
      vpc,
      domain: "linkzin.click",
    });

    // Substitui o MongoDB Atlas: o proxy multi-tenant do nível grátis (M0)
    // rejeita o handshake TLS de clientes OpenSSL (bug confirmado do lado da
    // Atlas, não do nosso código). DynamoDB é nativo da AWS, sem esse salto
    // de rede entre provedores, e o free tier é permanente.
    const table = new sst.aws.Dynamo("Urls", {
      fields: {
        Id: "number",
        Shard: "string",
      },
      primaryIndex: { hashKey: "Id" },
      globalIndexes: {
        // Usado só pelo RedisCounterSeeder, para achar o maior Id gravado
        // depois que o Redis perde o contador (restart/spot interrompido).
        HighestIdIndex: { hashKey: "Shard", rangeKey: "Id" },
      },
    });

    const service = new sst.aws.Service("Api", {
      cluster,
      architecture: "arm64",
      cpu: "0.25 vCPU",
      memory: "1 GB",
      capacity: "spot",
      serviceRegistry: { port: 8080 },
      // ECS Exec: deixado habilitado (sem custo relevante) por ter sido útil
      // na depuração do bug do Atlas; pode ser removido se não for mais usado.
      permissions: [
        {
          actions: [
            "ssmmessages:CreateControlChannel",
            "ssmmessages:CreateDataChannel",
            "ssmmessages:OpenControlChannel",
            "ssmmessages:OpenDataChannel",
          ],
          resources: ["*"],
        },
        {
          actions: ["dynamodb:GetItem", "dynamodb:PutItem", "dynamodb:Query"],
          resources: [table.arn, $interpolate`${table.arn}/index/*`],
        },
      ],
      transform: {
        service: {
          enableExecuteCommand: true,
        },
      },
      containers: [
        {
          name: "app",
          image: { context: ".", dockerfile: "Dockerfile" },
          environment: {
            ConnectionStrings__Redis: "localhost:6379,abortConnect=false",
            UrlShortener__HashidsSalt: hashidsSalt.value,
            UrlShortener__BaseUrl: $interpolate`${api.url}`,
            UrlShortener__DynamoTableName: table.name,
            // Observabilidade: o SDK do OpenTelemetry lê essas variáveis
            // padrão sozinho (nenhum endpoint/token fica no código).
            OTEL_SERVICE_NAME: "url-shortener",
            OTEL_RESOURCE_ATTRIBUTES: `deployment.environment=${$app.stage}`,
            OTEL_EXPORTER_OTLP_PROTOCOL: "http/protobuf",
            OTEL_EXPORTER_OTLP_ENDPOINT: otelExporterEndpoint.value,
            OTEL_EXPORTER_OTLP_HEADERS: otelExporterHeaders.value,
          },
        },
        {
          name: "redis",
          image: "public.ecr.aws/docker/library/redis:8-alpine",
          command: ["redis-server", "--save", "", "--appendonly", "no"],
        },
      ],
    });

    api.routePrivate("$default", service.nodes.cloudmapService.arn);

    return { url: api.url };
  },
});
