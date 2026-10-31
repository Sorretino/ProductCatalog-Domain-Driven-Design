# Plano e validação

Data: 28/10/2026. Ambiente: Windows x64; projeto novo, sem código anterior a preservar.

## Etapas verificáveis

| Etapa | Critério de conclusão | Resultado |
| --- | --- | --- |
| Inspeção e premissas | Ambiente e requisitos disponíveis identificados | Pasta vazia; runtime 8; sem SDK/Docker; premissas registradas |
| Domínio | Invariantes, encapsulamento e operações atômicas testados | Product e ProductPrice implementados e testados |
| Aplicação | Cinco casos de uso, validação e persistência por interfaces | Handlers reais com testes NSubstitute |
| Persistência | PostgreSQL, migration, filtro lógico e concorrência | Validado em PostgreSQL 17.11 real |
| HTTP e segurança | CRUD, contratos, JWT, scopes, erros e ETags | Validado com WebApplicationFactory e JWTs assinados |
| Execução local | Dockerfile, Compose, identidade local e instruções | Compose validado; execução de containers depende de Docker |
| Entrega | Restore fixado, build, testes, publicação e documentação | Build Release e publicação concluídos |

## Validações executadas

- SDK .NET 10.0.403 portátil, runtime 10.0.14; SHA-512 comparado ao metadata oficial Microsoft.
- PostgreSQL 17.11 portátil EDB; SHA-256 comparado ao checksum publicado pelo mantenedor. Instância temporária apenas em `127.0.0.1:55432`, sem serviço Windows.
- Restore dos oito projetos com versões centralizadas e lock files.
- Compilação Release da solução: **zero erros e zero warnings**.
- Suíte completa em banco real: **54 testes aprovados, zero falhas e zero skips**.
- Verificação `dotnet format --verify-no-changes --no-restore`: aprovada após padronização.
- Auditoria NuGet de dependências diretas e transitivas: nenhuma vulnerabilidade conhecida reportada na consulta. Resultado em `artifacts/dependency-audit.json`.
- Publicação Release da API em `artifacts/publish`.
- Geração do SQL idempotente em `artifacts/migrations.sql`.
- Execução de `--migrate` usando a API publicada, em banco descartável: aprovada.
- Execução real da API publicada no Kestrel em localhost: liveness **200**, readiness PostgreSQL **200**, OpenAPI **200** e catálogo sem autenticação **401**. Processo encerrado e banco descartável removido.
- `docker compose config --quiet` com binário oficial Compose 5.6.1 e segredos locais gerados: aprovado. Esse comando não inicia containers.

| Projeto de testes | Aprovados | Cobertura principal |
| --- | ---: | --- |
| Domain.Tests | 14 | Limites, normalização, precisão decimal, consistência após falha, exclusão e versões |
| Application.Tests | 16 | Criação, atualização, exclusão, consulta, paginação, validação antes de persistir, versão obsoleta e CancellationToken |
| Infrastructure.IntegrationTests | 5 | Migration/modelo, roundtrip real, soft delete, corrida de gravação, ILIKE literal e constraints |
| Api.IntegrationTests | 19 | Status HTTP, JSON, campos obrigatórios, ProblemDetails, OpenAPI, JWT inválido/expirado, scopes, ETags e ciclo HTTP completo no PostgreSQL |

Os testes PostgreSQL criam bancos com nomes aleatórios e fazem a limpeza ao final. Nenhum banco de aplicação existente foi alterado durante a validação. Os testes HTTP usam autenticação JWT real; apenas a fonte de chaves de teste é substituída em uma factory exclusiva do assembly de testes.

## Reproduzir

```powershell
# Verificação padrão, sem dependência de banco: testes PostgreSQL são explicitamente skipped.
./scripts/Verify.ps1

# Todos os testes, com Docker/Testcontainers.
./scripts/Verify.ps1 -Postgres

# Todos os testes com PostgreSQL de testes existente.
$env:TEST_POSTGRES_CONNECTION = 'Host=localhost;Port=5432;Database=postgres;Username=tests;Password=...'
./scripts/Verify.ps1
```

O usuário da conexão de testes precisa de CREATEDB. Os relatórios TRX ficam em `artifacts/test-results`; cada execução inclui o horário no nome. As ferramentas locais usadas nesta máquina ficam em `F:\DotNet-projetos\.tools`, fora do código distribuível.

## Limites da validação

O host não tem Docker Engine/Docker Desktop. Portanto, a imagem Linux não foi construída/executada, o lifecycle de Testcontainers não foi exercitado e o realm Keycloak do Compose não foi iniciado. O YAML e suas interpolações foram validados pelo Compose oficial. O mesmo PostgreSQL usado pela fixture foi exercitado via conexão real externa ao Testcontainers.

JWT, assinatura, audience, expiração e autorização foram exercitados no ASP.NET Core. A descoberta OIDC e o fluxo `client_credentials` com o Keycloak local ainda precisam da execução do Compose em um host Docker. O [README](../README.md) descreve essa execução. Esta validação não constitui teste de carga, disponibilidade, pentest, recuperação de backups ou homologação de infraestrutura de produção.
