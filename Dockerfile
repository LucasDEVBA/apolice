# Uma imagem por papel (api / worker) a partir do mesmo estágio de build: restore e compilação
# acontecem uma vez só e cada target final leva só o próprio publish.
#   docker build --target api    -t apolice-api .
#   docker build --target worker -t apolice-worker .
#
# Bases fixadas por digest (com a tag ao lado para leitura): o build é reproduzível e os patches
# de segurança da imagem chegam como PR do Dependabot, que renova o digest.

FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:747cbade5bfd918a175ac32f8f1d931edec2c7d0d5397a3de72eaef1ac1f66d2 AS build
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1
WORKDIR /src

# Manifestos antes do código: a camada de restore só é refeita quando as dependências mudam,
# não a cada alteração de .cs.
COPY global.json Directory.Build.props Directory.Packages.props Apolice.slnx ./
COPY src/Apolice.Domain/Apolice.Domain.csproj src/Apolice.Domain/
COPY src/Apolice.Application/Apolice.Application.csproj src/Apolice.Application/
COPY src/Apolice.Infrastructure/Apolice.Infrastructure.csproj src/Apolice.Infrastructure/
COPY src/Apolice.Api/Apolice.Api.csproj src/Apolice.Api/
COPY src/Apolice.Worker/Apolice.Worker.csproj src/Apolice.Worker/
RUN dotnet restore src/Apolice.Api/Apolice.Api.csproj \
 && dotnet restore src/Apolice.Worker/Apolice.Worker.csproj

# O .editorconfig entra porque TreatWarningsAsErrors + EnforceCodeStyleInBuild dependem dele:
# sem ele a imagem compilaria com severidades diferentes das do CI.
COPY .editorconfig ./
COPY src/ src/

# Domain, Application e Infrastructure compilam uma vez para os dois papéis; os publishes abaixo
# só copiam a saída (--no-build). UseAppHost e a geração do OpenAPI mudam o que o build produz,
# por isso os mesmos valores aqui e nos publishes.
# A geração do OpenAPI executaria a aplicação; aqui ela não serve para nada (o contrato já é
# verificado no CI) e só tornaria o build da imagem frágil.
RUN dotnet build src/Apolice.Api/Apolice.Api.csproj -c Release --no-restore \
      -p:UseAppHost=false -p:OpenApiGenerateDocuments=false \
 && dotnet build src/Apolice.Worker/Apolice.Worker.csproj -c Release --no-restore \
      -p:UseAppHost=false -p:OpenApiGenerateDocuments=false

FROM build AS publish-api
RUN dotnet publish src/Apolice.Api/Apolice.Api.csproj -c Release --no-build -o /app \
    -p:UseAppHost=false -p:OpenApiGenerateDocuments=false

FROM build AS publish-worker
RUN dotnet publish src/Apolice.Worker/Apolice.Worker.csproj -c Release --no-build -o /app \
    -p:UseAppHost=false -p:OpenApiGenerateDocuments=false

# aspnet:10.0 (Ubuntu) e não a variante -noble-chiseled, que não tem shell: o healthcheck da api
# no docker-compose.yml usa o bash da imagem para consultar /health/ready sem instalar curl. Esta
# imagem só serve o ambiente local e o E2E; em produção roda o zip na Lambda.
# O worker também usa a imagem aspnet: o Infrastructure tem FrameworkReference para
# Microsoft.AspNetCore.App, então o runtime "puro" não basta.
FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:d5633778c754e89a386882eddf8dbee6942447a8a4b8267c19cf1e1158d0b0ac AS runtime
WORKDIR /app
# Binários ficam com dono root e o processo roda sem privilégio: a aplicação não consegue
# alterar o próprio código mesmo se comprometida.
# APP_UID é numérico (1654), definido pela imagem base da Microsoft; o hadolint só não
# consegue expandir a variável.
# hadolint ignore=DL3066
USER $APP_UID

FROM runtime AS worker
COPY --from=publish-worker /app .
# Sem argumentos sobe o host local (relay + consumidor); "migrate" e "seed" chegam como command.
ENTRYPOINT ["dotnet", "Apolice.Worker.dll"]

# Último estágio = target padrão de um "docker build ." sem --target.
FROM runtime AS api
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=publish-api /app .
ENTRYPOINT ["dotnet", "Apolice.Api.dll"]
