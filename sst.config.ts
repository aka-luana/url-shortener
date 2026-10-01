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
    const mongoUrl = new sst.Secret("MongoUrl");
    const hashidsSalt = new sst.Secret("HashidsSalt");

    const vpc = new sst.aws.Vpc("Vpc");
    const cluster = new sst.aws.Cluster("Cluster", { vpc });

    const api = new sst.aws.ApiGatewayV2("Gateway", { vpc });

    const service = new sst.aws.Service("Api", {
      cluster,
      architecture: "arm64",
      cpu: "0.25 vCPU",
      memory: "1 GB",
      capacity: "spot",
      serviceRegistry: { port: 8080 },
      // Diagnóstico: habilita o ECS Exec para abrirmos um shell dentro do
      // container rodando na AWS e investigar ao vivo a falha de TLS com o
      // Atlas (testar MTU, rodar openssl s_client, etc). Remover depois.
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
            ConnectionStrings__Mongo: mongoUrl.value,
            ConnectionStrings__Redis: "localhost:6379,abortConnect=false",
            UrlShortener__HashidsSalt: hashidsSalt.value,
            UrlShortener__BaseUrl: $interpolate`${api.url}`,
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
