# FiapDonateWorker — Design

## Contexto

Este repositório implementa o microsserviço **Worker/Consumer de Doações** da
plataforma "Conexão Solidária" (Hackathon FIAP, ONG Esperança Solidária). Ele
é um dos dois microsserviços obrigatórios pelo enunciado do hackathon: a API
de Campanhas/Usuários/Autenticação vive em outro repositório do time
(`davidjmarinho/...`); este repositório cobre exclusivamente o serviço que
consome eventos de doação de uma fila e atualiza o valor arrecadado da
campanha correspondente.

Requisito de origem (do PDF do hackathon):

> Ao receber uma nova doação, a API NÃO deve atualizar o valor arrecadado da
> campanha diretamente no banco de dados. A API deve publicar um evento
> (ex.: `DoacaoRecebidaEvent`) em um broker de mensageria (RabbitMQ ou Kafka).
> Um segundo serviço (Worker/Consumer) deve consumir essa fila e, então,
> atualizar o "Valor Total Arrecadado" da respectiva campanha.

## Escopo

**Dentro do escopo deste repositório:**
- Consumo do evento `DoacaoRecebidaEvent` via RabbitMQ.
- Persistência do histórico de doações e atualização do valor arrecadado da
  campanha, com idempotência.
- Endpoints `/health` e `/metrics` para probes de Kubernetes e scraping de
  observabilidade (Grafana/Prometheus).
- Dockerfile, manifests Kubernetes (Deployment/Service/ConfigMap) e pipeline
  de CI (GitHub Actions) — todos escopados a este único serviço.
- Testes de unidade (xUnit) das regras de negócio do domínio.

**Fora do escopo** (responsabilidade do outro repositório do time):
- Autenticação/JWT, RBAC (`GestorONG`/`Doador`).
- CRUD de campanhas, cadastro de doador, painel de transparência público.
- Publicação do evento `DoacaoRecebidaEvent` (produtor).
- API Gateway.

## Decisões de arquitetura

### Biblioteca de mensageria: MassTransit + RabbitMQ

Avaliadas 3 opções:

1. **MassTransit + RabbitMQ (escolhida)** — abstração de alto nível sobre
   `RabbitMQ.Client`. Fornece retry/redelivery configurável, fila de erro
   automática (`<queue>_error`) e consumers fortemente tipados via DI. Padrão
   amplamente documentado em projetos .NET, reduzindo risco sob o prazo
   curto do hackathon.
2. **RabbitMQ.Client puro + BackgroundService manual** — descartada: exige
   implementar reconexão, ack/nack e retry manualmente, maior risco de bugs.
3. **Wolverine** — descartada: biblioteca mais nova, menos madura/documentada
   para um projeto que será lido e avaliado por terceiros.

### Hosting: ASP.NET Core Minimal API (não "Worker Service" puro)

Em vez do template clássico `Worker Service` (sem HTTP), o serviço roda como
uma aplicação ASP.NET Core Minimal API (`WebApplication`) que hospeda o
`IHostedService` do MassTransit em background **e** expõe HTTP para
`/health` e `/metrics`. Isso permite um único processo/pod atender tanto ao
consumo de fila quanto às probes do Kubernetes e ao scraping do
Grafana/Prometheus, sem precisar de dois processos separados.

### .NET 8 (LTS)

Escolhido sobre .NET 9/10 disponíveis na máquina por ser a versão com maior
compatibilidade de bibliotecas (MassTransit, Npgsql, EF Core) e imagens
Docker oficiais mais maduras — reduz risco sob prazo curto.

### Banco de dados: PostgreSQL compartilhado com a API

O Worker conecta no mesmo banco PostgreSQL usado pela API principal. Isso
evita ter que sincronizar dados entre bancos separados em um MVP de
hackathon com dois times/repositórios trabalhando em paralelo. O Worker
mapeia via EF Core apenas o subconjunto de colunas que precisa da tabela
`Campanhas` (não é dono do schema completo) e é dono exclusivo da nova
tabela `Doacoes`.

## Estrutura do projeto

```
FiapDonateWorker/
├── FiapDonateWorker.slnx
├── src/
│   ├── FiapDonateWorker.Worker/          # host ASP.NET Core + MassTransit consumer + endpoints /health /metrics
│   ├── FiapDonateWorker.Domain/          # entidades Campanha (parcial) e Doacao, regras de negócio
│   └── FiapDonateWorker.Infrastructure/  # DbContext (EF Core + Npgsql), migrations, persistência
├── tests/
│   └── FiapDonateWorker.Domain.Tests/    # xUnit — regras de negócio isoladas
├── Dockerfile
├── docker-compose.yml                      # Postgres + RabbitMQ para desenvolvimento local
├── k8s/
│   ├── configmap.yaml
│   ├── deployment.yaml
│   ├── service.yaml
│   └── secret.example.yaml
├── .github/workflows/ci.yml
└── README.md
```

Camadas isoladas por responsabilidade: `Domain` não depende de nada externo
(testável sem banco/fila real); `Infrastructure` implementa persistência
concreta (EF Core/Npgsql); `Worker` é a camada de composição (DI, consumer
MassTransit, endpoints HTTP, configuração).

## Modelo de dados

### `Campanhas` (tabela existente, dona da API — mapeamento parcial)

O Worker mapeia apenas as colunas que precisa ler/escrever:

| Coluna          | Tipo               | Uso pelo Worker                          |
|-----------------|---------------------|-------------------------------------------|
| `Id`            | `uuid` (PK)         | localizar a campanha do evento             |
| `Status`        | enum (`Ativa`/`Concluida`/`Cancelada`) | validar se pode receber crédito |
| `ValorArrecadado` | `decimal`         | valor atualizado a cada doação processada  |

Colunas de propriedade exclusiva da API (`Titulo`, `Descricao`, `DataInicio`,
`DataFim`, `MetaFinanceira`) **não** são mapeadas pelo Worker.

### `Doacoes` (tabela nova, dona do Worker)

| Coluna               | Tipo       | Descrição                                        |
|----------------------|------------|---------------------------------------------------|
| `Id`                 | `uuid` (PK) | = `doacaoId` do evento; garante idempotência      |
| `IdCampanha`         | `uuid` (FK) | referência à campanha                             |
| `ValorDoacao`        | `decimal`   | valor doado                                       |
| `DataHoraRecebida`   | `timestamptz` | timestamp de quando a API recebeu a intenção    |
| `DataHoraProcessada` | `timestamptz` | timestamp de quando o Worker processou o evento |
| `Status`             | enum (`Creditada`/`Rejeitada`) | resultado do processamento         |

## Contrato do evento `DoacaoRecebidaEvent`

Proposta de contrato (a ser confirmado/ajustado junto ao repositório da API
para garantir compatibilidade do produtor):

```json
{
  "doacaoId": "guid",
  "idCampanha": "guid",
  "valorDoacao": 0.00,
  "dataHoraRecebida": "2026-08-17T00:00:00Z"
}
```

- **Exchange/Fila**: convenção padrão do MassTransit a partir do nome do
  tipo do evento (`DoacaoRecebidaEvent` → fila `doacao-recebida-queue`).
- **Broker**: RabbitMQ.

## Regras de negócio do Worker

1. **Idempotência**: ao consumir uma mensagem, o Worker tenta inserir um
   registro em `Doacoes` com `Id = doacaoId`. Se já existir (violação de
   chave única), a mensagem é confirmada (ack) sem re-creditar o valor —
   protege contra redelivery do RabbitMQ (semântica *at-least-once*).
2. **Defesa contra corrida**: se a campanha não existir, ou já estiver com
   `Status` `Cancelada` ou `Concluida` no momento do processamento, a doação
   é registrada em `Doacoes` com `Status = Rejeitada` (sem incrementar
   `ValorArrecadado`) — evita creditar uma campanha que foi encerrada entre
   o envio da intenção de doação (na API) e o processamento assíncrono.
3. **Falha transitória** (ex.: banco indisponível): retry do MassTransit (3
   tentativas com backoff curto); se persistir, a mensagem é movida para a
   fila de erro `doacao-recebida-queue_error` para inspeção manual — a
   doação não é perdida silenciosamente.

## Observabilidade

- `/health` — `Microsoft.Extensions.Diagnostics.HealthChecks`, validando
  conectividade com PostgreSQL e RabbitMQ.
- `/metrics` — `prometheus-net.AspNetCore`, expondo métricas padrão
  (HTTP, GC, threads) mais um contador customizado de doações processadas
  (sucesso/rejeitada), para dar ao dashboard do Grafana uma métrica de
  negócio além de infraestrutura.

## Deploy

- **Dockerfile**: multi-stage — build/publish com SDK `.NET 8`, imagem final
  em `aspnet:8.0`.
- **Kubernetes** (`k8s/`):
  - `configmap.yaml` — configuração não sensível (host do RabbitMQ, nome da
    fila, host do Postgres).
  - `deployment.yaml` — pod do Worker com `livenessProbe`/`readinessProbe`
    em `/health`; variáveis sensíveis (senha do banco, credenciais do
    RabbitMQ) via `Secret` referenciado (não versionado em texto puro —
    incluído apenas `secret.example.yaml` como template).
  - `service.yaml` — `ClusterIP` expondo a porta HTTP para scraping interno
    de métricas.
- **`docker-compose.yml`**: sobe PostgreSQL + RabbitMQ localmente para
  desenvolvimento, separado dos manifests de produção do Kubernetes.

## CI/CD

`.github/workflows/ci.yml`, acionado em push/PR para `main`:

1. `dotnet restore`
2. `dotnet build`
3. `dotnet test` (executa os testes xUnit do domínio)
4. `docker build` da imagem (push para registry opcional/comentado — não é
   exigido pelo enunciado, apenas a geração da imagem no CI).

## Testes

Testes de unidade (xUnit) cobrindo as regras de negócio do domínio de forma
isolada (sem banco/fila reais):
- Doação duplicada (mesmo `doacaoId`) não credita o valor duas vezes.
- Doação para campanha `Cancelada`/`Concluida` é rejeitada e não credita.
- Cálculo do novo `ValorArrecadado` após uma doação válida.

## README

Deve conter passo a passo para:
1. Subir PostgreSQL + RabbitMQ localmente via `docker-compose`.
2. Rodar as migrations do EF Core.
3. Rodar o Worker localmente.
4. Simular uma mensagem `DoacaoRecebidaEvent` manualmente via a interface de
   management do RabbitMQ, para validar o fluxo fim a fim sem depender da
   API principal estar no ar.
