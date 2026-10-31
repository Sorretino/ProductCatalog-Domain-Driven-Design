# Arquitetura, premissas e decisões

## Contexto e inspeção

Em 28/10/2026 a pasta `F:\DotNet-projetos` estava vazia, sem repositório Git. Havia runtime .NET 8.0.20, mas nenhum SDK .NET ou Docker. O texto fornecido termina na seção 4, em “Não introduza padrões de DDD tático apenas por formal”. A implementação atende ao conteúdo disponível, sem pressupor requisitos de seções ausentes.

Foi adotado o nome **ProductCatalog** e um agregado Product por identidade UUID. O SDK 10.0.403 e os runtimes 10.0.14 foram disponibilizados de forma portátil na pasta de trabalho. .NET 10 foi escolhido por ser a LTS ativa; não se migrou nem sobrescreveu código existente.

## Premissas de negócio

- Nome obrigatório, normalizado com Trim, de 1 a 200 caracteres; nomes repetidos são permitidos, pois não houve requisito de unicidade/SKU.
- Descrição opcional até 2.000 caracteres, normalizada; vazia equivale a ausente.
- Preço em BRL de zero a 999.999.999.999,99, com até duas casas decimais. Preço zero é permitido; números negativos ou com fração menor que centavo são rejeitados, sem arredondamento implícito.
- Estado ativo/inativo é independente da existência: produtos inativos continuam consultáveis, inclusive sem filtro de estado. Não se pressupõe que catálogo seja público.
- Exclusão é lógica, irreversível pelos casos de uso disponíveis. Marca DeletedAt, desativa e troca a versão. Consultas normais ocultam excluídos; operações posteriores retornam 404.
- Datas UTC vêm de TimeProvider na aplicação; o domínio recebe o instante. Não há restauração, moeda variável, estoque, tributação, categorias, eventos ou integrações comerciais.

## Responsabilidades

**Domain** contém Product e o objeto de valor imutável ProductPrice. Não possui referência a outros projetos ou dependências externas. Operações validam todos os dados antes de alterar o estado e impedem alterações após a exclusão. As exceções não contêm status HTTP.

**Application** define interfaces de repositório e unidade de trabalho, DTOs, comandos, consultas, validadores e cinco handlers. FluentValidation executa de forma explícita e assíncrona antes da persistência, mesmo quando o caso de uso é chamado fora de HTTP. A validação de entrada melhora as mensagens, enquanto o domínio continua sendo a autoridade sobre invariantes. O CancellationToken atravessa os casos de uso e a persistência.

**Infrastructure** implementa as interfaces com um DbContext scoped. A mesma instância rastreia o agregado e confirma uma operação por SaveChanges; a transação implícita do EF Core é suficiente para este único agregado. Não se adicionaram transações manuais, retry indiscriminado ou UnitOfWork genérico. A listagem usa AsNoTracking e filtros parametrizados. O design-time factory permite migrations sem subir a API nem configurar identidade.

**API** traduz contratos HTTP, ETags e erros. A referência à infraestrutura fica no ponto de composição. Os controllers não recebem nem acessam DbContext. A API valida assinatura, expiração, issuer e audience dos JWTs, e exige scopes de leitura/escrita. Ela não emite tokens. Health checks são públicos, retornando apenas status; o Swagger fica em Development.

## Concorrência e consistência

Version é um UUID trocado em cada mutação. A resposta individual inclui um ETag forte e as escritas exigem If-Match. O handler rejeita a versão já desatualizada; o token de concorrência EF protege também a corrida entre leitura e gravação. Uma atualização concorrente ou uma exclusão concorrente causa falha de pré-condição, sem sobrescrita silenciosa.

O banco possui numeric(14,2), limites de texto e check constraints para preço, nome e exclusão/inatividade. O objeto de valor impede que a aplicação entregue um decimal que o PostgreSQL precisaria arredondar. As constraints não substituem o domínio. A soft deletion usa filtro global no EF e é demonstrada em teste lendo explicitamente a linha com IgnoreQueryFilters.

Cada página é ordenada por CreatedAt e Id. Count e items são consultas separadas no isolamento padrão do PostgreSQL; sob escritas simultâneas podem refletir instantes próximos diferentes. Isso é aceito para a listagem administrativa. A paginação por offset fica limitada e não promete um snapshot entre requisições. Em volumes maiores, adotar cursor e índices de pesquisa após medir as consultas.

## Dependências

| Dependência | Finalidade | Licença principal |
| --- | --- | --- |
| .NET/ASP.NET Core e extensões Microsoft | Host, DI, JWT, observabilidade básica | MIT |
| EF Core + design tools + relational | Mapeamento, persistência e migrations | MIT |
| Npgsql provider | PostgreSQL real | PostgreSQL |
| FluentValidation | Validação assíncrona de entrada | Apache-2.0 |
| Swashbuckle | OpenAPI e Swagger UI | MIT |
| xUnit v3 | Testes e runner Microsoft Testing Platform | Apache-2.0 |
| NSubstitute | Única biblioteca de mocks | BSD-3-Clause |
| Testcontainers.PostgreSql | PostgreSQL descartável quando Docker está disponível | MIT |

As versões exatas estão em Directory.Packages.props e nos lock files. Não se usa FluentAssertions para evitar necessidade de avaliar uma licença adicional. Dependências transitivas e respectivas licenças continuam precisando ser avaliadas pela organização na distribuição do produto.

Foi necessário referenciar explicitamente Microsoft.EntityFrameworkCore.Relational 10.0.14: somente o piso transitivo do Npgsql selecionava um patch diferente do EF usado pelas ferramentas de design. O alinhamento foi confirmado por compilação sem warnings.

## Riscos e limites operacionais

| Risco | Decisão adotada |
| --- | --- |
| Atualizações simultâneas | ETag/If-Match e token de concorrência no banco |
| Dependência do provedor de identidade | Descoberta OIDC e chaves rotacionáveis; fail closed para requisições protegidas |
| Mudança de schema na inicialização de várias réplicas | Migrations em comando explícito, separadas da inicialização normal |
| Injeção em pesquisa | LINQ parametrizado e escape dos metacaracteres de ILIKE |
| Carga excessiva | Página limitada, corpo até 64 KiB em Kestrel e limiter por instância |
| Vazamento de detalhes internos | ProblemDetails sanitizado com traceId; detalhes internos apenas nos logs operacionais |
| Sem Docker nesta máquina | PostgreSQL portátil para validar banco real; Compose/Testcontainers têm limites de validação registrados |
| Crescimento do catálogo | Medir ILIKE/offset antes de incluir trigramas, cursor, cache ou motor de busca |

O health de readiness verifica conectividade PostgreSQL; não verifica todas as dependências nem garante que o schema esteja na última migration. O processo de implantação deve aplicar a migration antes de habilitar tráfego. Não há garantia de disponibilidade, auditoria regulatória, multi-tenancy ou regras de acesso por proprietário sem requisitos próprios.

## Referências consultadas

- [Política oficial de suporte .NET](https://dotnet.microsoft.com/en-us/platform/support/policy)
- [JWT bearer no ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0)
- [FluentValidation e DI](https://docs.fluentvalidation.net/en/latest/di.html)
- [xUnit v3 e Microsoft Testing Platform](https://xunit.net/docs/getting-started/v3/microsoft-testing-platform)
- [Testcontainers PostgreSQL](https://testcontainers.com/modules/postgresql/)
- [Keycloak: hostname e backchannel](https://www.keycloak.org/server/hostname)
- [Keycloak: importação e variáveis de ambiente](https://www.keycloak.org/server/importExport)
- [PostgreSQL para Windows](https://www.postgresql.org/download/windows/)
