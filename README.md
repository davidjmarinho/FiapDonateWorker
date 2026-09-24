# FiapDonateWorker

Worker/Consumer de doações da plataforma "Conexão Solidária" (Hackathon FIAP).
Consome o evento `DoacaoRecebidaEvent` de uma fila RabbitMQ e atualiza o valor
arrecadado da campanha correspondente em SQL Server, de forma idempotente.

> Este repositório nasceu como `FiapDonateReceiver` e foi migrado/renomeado
> para `FiapDonateWorker` para refletir corretamente o papel que exerce no
> hackathon (o "Worker/Consumer de Processamento de Doações" exigido pelo
> enunciado). O histórico de commits original foi preservado na migração.

Este repositório cobre **apenas** este microsserviço. A API de
Campanhas/Usuários/Autenticação (que publica o evento) vive em outro
repositório do time. RabbitMQ e SQL Server **não** são subidos aqui — eles
vêm do repositório `FiapDonateServices`.

## Arquitetura

Veja `docs/superpowers/specs/2026-08-17-fiapdonatereceiver-design.md` para o
desenho completo (decisões de arquitetura, modelo de dados, contrato do
evento e regras de negócio).

> **Arquitetura database-per-service:** este Worker é dono do seu próprio banco
> (`conexao_solidaria`), incluindo uma **réplica local** da tabela `Campanhas`
> criada pelas migrations deste repositório. A tabela `Campanhas` da API (fonte
> da verdade) **não é compartilhada**. A integração entre os dois serviços é
> feita **100% por eventos** no RabbitMQ:
>
> 1. A API publica `DoacaoRecebidaEvent` (só para campanhas ativas — ela valida
>    a atividade no intake). Ao recebê-lo, o Worker cria a réplica local da
>    campanha como `Ativa` (se ainda não existir) e credita o valor.
> 2. Após creditar, o Worker publica de volta `ValorArrecadadoAtualizadoEvent`
>    com o **valor total absoluto** da campanha, para que a API atualize seu
>    próprio `Campaigns.ValorArrecadado` e o Painel de Transparência reflita a
>    doação processada.

## Pré-requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Infraestrutura compartilhada do `FiapDonateServices` no ar (SQL Server na
  porta `1433` e RabbitMQ nas portas `5672` / `15672`)
- (Opcional, para deploy local em Kubernetes) `kubectl` + um cluster local
  (Minikube, Kind ou Docker Desktop Kubernetes)

## Apontamento para o FiapDonateServices

O Worker espera os mesmos hosts/contratos da infra compartilhada:

| Recurso | Host (local / `dotnet run`) | Host (Docker/K8s na rede `fiapdonate`) |
|---|---|---|
| SQL Server | `localhost,1433` | `sqlserver,1433` |
| Banco | `conexao_solidaria` (contrato do `FiapDonateServices`) | `conexao_solidaria` |
| RabbitMQ AMQP | `localhost:5672` | `rabbitmq:5672` |
| RabbitMQ UI | http://localhost:15672 | Service `rabbitmq` porta `15672` |
| Usuário RabbitMQ | `fiapdonate` | `fiapdonate` |
| Fila | `doacao-recebida-queue` | `doacao-recebida-queue` |

As senhas **não** ficam versionadas. Copie-as dos arquivos em
`FiapDonateServices/secrets/` (`sqlserver_sa_password.txt` e
`rabbitmq_password.txt`) e injete via variável de ambiente (local) ou
`Secret` (Kubernetes).

## Subindo o Worker localmente

1. No repositório `FiapDonateServices`, suba a infra compartilhada:

   ```bash
   docker compose up -d
   ```

2. Restaure as ferramentas locais (inclui o `dotnet-ef`):

   ```bash
   dotnet tool restore
   ```

3. Exporte as senhas da infra (PowerShell):

   ```powershell
   $sa = Get-Content ..\FiapDonateServices\secrets\sqlserver_sa_password.txt -Raw
   $rmq = Get-Content ..\FiapDonateServices\secrets\rabbitmq_password.txt -Raw
   $env:ConnectionStrings__WorkerDb = "Server=localhost,1433;Database=conexao_solidaria;User Id=sa;Password=$sa;TrustServerCertificate=True;"
   $env:RabbitMq__Password = $rmq.Trim()
   ```

4. (Opcional) Aplique as migrations do banco manualmente (cria as tabelas
   `Doacoes` e `Campanhas`):

   ```bash
   dotnet ef database update --project src/FiapDonateWorker.Infrastructure/FiapDonateWorker.Infrastructure.csproj --startup-project src/FiapDonateWorker.Infrastructure/FiapDonateWorker.Infrastructure.csproj
   ```

   > Este passo é opcional: o Worker aplica automaticamente as migrations
   > pendentes na inicialização (`dbContext.Database.MigrateAsync()` em
   > `Program.cs`), então basta rodar o passo 5 abaixo. Use este comando
   > manual se quiser aplicar as migrations sem subir o Worker (por exemplo,
   > para inspecionar o schema antes de rodar o serviço).
   >
   > As tabelas `Doacoes` **e** `Campanhas` são criadas pelas migrations deste
   > repositório (o Worker é dono do seu banco). Não é preciso criar `Campanhas`
   > manualmente: o Worker cria a réplica local da campanha automaticamente ao
   > receber o primeiro `DoacaoRecebidaEvent` dela.

5. Rode o Worker:

   ```bash
   dotnet run --project src/FiapDonateWorker.Api/FiapDonateWorker.Api.csproj
   ```

6. Confirme que o serviço está saudável:

   ```bash
   curl http://localhost:5229/health
   curl http://localhost:5229/metrics
   ```

   > O serviço expõe três endpoints de health check, pensados para uso
   > separado em liveness vs. readiness probes do Kubernetes:
   >
   > - `/health/live` — só confirma que o processo está de pé, sem checar
   >   dependências externas. Usado pela `livenessProbe` (`k8s/deployment.yaml`):
   >   uma falha aqui gera restart do pod, o que não faz sentido se o problema
   >   for uma dependência externa fora do ar.
   > - `/health/ready` — checa SQL Server e RabbitMQ. Usado pela
   >   `readinessProbe`: uma falha aqui tira o pod de circulação sem reiniciá-lo.
   > - `/health` — mantido como alias de `/health/ready`, por compatibilidade.

## Testando o fluxo fim a fim (sem depender da API estar no ar)

1. Abra a interface de management do RabbitMQ em
   [http://localhost:15672](http://localhost:15672) (usuário `fiapdonate` e
   senha de `FiapDonateServices/secrets/rabbitmq_password.txt`).
2. Vá em **Queues** → `doacao-recebida-queue` → **Publish message**.
3. Publique uma mensagem com o header `content_type: application/vnd.masstransit+json`
   e o seguinte corpo (ajuste `idCampanha` para o `Id` de uma campanha ativa
   existente no banco):

   ```json
   {
     "message": {
       "doacaoId": "22222222-2222-2222-2222-222222222222",
       "idCampanha": "11111111-1111-1111-1111-111111111111",
       "valorDoacao": 50.00,
       "dataHoraRecebida": "2026-08-17T12:00:00Z"
     },
     "messageType": ["urn:message:FiapDonateWorker.Api.Events:DoacaoRecebidaEvent"]
   }
   ```

4. Confirme no SQL Server compartilhado que o valor foi creditado (o
   `ValorArrecadado` da campanha deve ter subido em 50.00, e deve existir
   uma linha em `Doacoes` com `Status = Creditada`).

## Rodando os testes automatizados

```bash
dotnet test FiapDonateWorker.slnx
```

## Deploy local em Kubernetes

Os manifests deste repositório apontam para os Services `rabbitmq` e
`sqlserver` do `FiapDonateServices` (namespace `fiapdonate`). Suba a infra
compartilhada primeiro; depois:

```bash
docker build -t fiapdonateworker:local .
cp k8s/secret.example.yaml k8s/secret.yaml   # cole as senhas de FiapDonateServices/secrets
kubectl apply -f k8s/configmap.yaml -f k8s/secret.yaml -f k8s/deployment.yaml -f k8s/service.yaml
kubectl get pods -n fiapdonate
```

## Estrutura do projeto

```
src/
  FiapDonateWorker.Domain/          entidades e regras de negócio (sem dependências externas)
  FiapDonateWorker.Infrastructure/  EF Core, migrations, persistência
  FiapDonateWorker.Api/             host ASP.NET Core + consumer MassTransit + /health /metrics
tests/
  FiapDonateWorker.Domain.Tests/
  FiapDonateWorker.Infrastructure.Tests/
k8s/                                  manifests Kubernetes do Worker (apontam para a infra compartilhada)
docs/superpowers/specs/               documento de design
docs/superpowers/plans/               este plano de implementação
```
