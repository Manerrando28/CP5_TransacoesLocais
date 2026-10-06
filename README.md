# Checkpoint 5 — Transações locais em C#

**FIAP — C# Software Development — Turma 3ESPH — 2026.2**

## Integrantes

- Fernando Carlos Colque Huaranca — RM558095
- Gabriel Guilherme Leste — RM558638
- Gabriel Lacerda Araújo — RM558307
- Julia Carolina Ferreira Silva — RM558896

## Objetivo e tecnologia

Transferir valores entre contas com débito, crédito e registro em `TRANSFERENCIA` dentro da mesma transação local. Cada execução utiliza SQL Server ou Oracle separadamente. A aplicação usa C#, .NET 8 e ADO.NET, com Microsoft.Data.SqlClient 7.1.1 e Oracle.ManagedDataAccess.Core 23.26.301.

O programa valida contas existentes, ativas e distintas, valor positivo com até duas casas decimais e saldo suficiente. As consultas de validação mantêm bloqueios nas contas até o fim da transação. Todos os comandos recebem a mesma conexão/transação, e cada alteração deve afetar exatamente uma linha. No Oracle, `BindByName` associa parâmetros por nome.

`Commit()` ocorre após débito, crédito e histórico. Se houver exceção, `Rollback()` desfaz as alterações. O teste deliberado lança uma exceção após o débito. Falhas de conexão/configuração são informadas como erro e não representam teste de rollback. Uma falha no próprio rollback também é informada, sem afirmar que ele foi concluído.

## Estrutura

```text
CP5_TransacoesLocais.slnx
README.md
CP5_TransacoesLocais/
  CP5_TransacoesLocais.csproj
  Program.cs
  scripts/
    S01_SQLServer_Estrutura_Dados.sql
    S02_Oracle_Estrutura_Dados.sql
    S03_SQLServer_Testes_Commit_Rollback.sql
    S04_Oracle_Testes_Commit_Rollback.sql
  evidencias/
    01_Estrutura.png
    02_sqlserver_commit.png
    03_sqlserver_rollback.png
    04_oracle_commit.png
    05_oracle_rollback.png
    06_codigo_transacao.png
verificacao/
  Testar-SqlServer.ps1
  resultado-sqlserver.txt
```

O modelo possui `CLIENTE`, `TIPO_CONTA`, `CONTA` e `TRANSFERENCIA`, com chaves estrangeiras entre elas. Os scripts usam os mesmos nomes de colunas nos dois bancos, incluindo `ContaOrigemId` e `ContaDestinoId`.

## Preparação do SQL Server

Instale SQL Server LocalDB ou utilize uma instância disponível. Crie uma database própria, por exemplo `CP5_DB`, e selecione-a na ferramenta SQL. A criação da database pode ser feita no contexto administrativo; os scripts de tabelas devem ser executados dentro da database do exercício.

```sql
CREATE DATABASE CP5_DB;
```

Execute `CP5_TransacoesLocais/scripts/S01_SQLServer_Estrutura_Dados.sql` dentro de `CP5_DB`. **S01 recria as tabelas e apaga os dados anteriores do exercício.** O script recusa os bancos de sistema. A aplicação não cria bancos nem tabelas automaticamente.

Por padrão, o programa conecta a `(localdb)\MSSQLLocalDB`, database `CP5_DB`, com autenticação integrada. Para outra database/instância, configure no PowerShell que executará o programa:

```powershell
$env:CP5_SQLSERVER_CONNECTION_STRING = 'Server=SEU_SERVIDOR;Database=SUA_DATABASE_CP5;Integrated Security=True;TrustServerCertificate=True;'
```

## Preparação do Oracle

Utilize Oracle 12c ou superior, com suporte a colunas `IDENTITY`. Conecte com um usuário/schema próprio, nunca `SYS` ou `SYSTEM`. No Oracle XE local, use o serviço/PDB correto, normalmente `XEPDB1`; o usuário precisa de `CREATE SESSION`, `CREATE TABLE`, `CREATE SEQUENCE` e quota no tablespace. No ambiente institucional, use os privilégios fornecidos.

Execute `CP5_TransacoesLocais/scripts/S02_Oracle_Estrutura_Dados.sql` uma vez num schema do exercício sem essas tabelas. Para novos testes, use o bloco de reinicialização de S04. Se houver tabelas antigas com colunas diferentes, use um schema novo ou adapte-as antes da execução. Não execute DDL durante testes transacionais, pois o Oracle faz commits implícitos.

Configure a conexão localmente, substituindo os marcadores e sem salvar a senha no projeto:

```powershell
$env:CP5_ORACLE_CONNECTION_STRING = 'User Id=SEU_USUARIO;Password=SUA_SENHA_AQUI;Data Source=localhost:1521/XEPDB1;'
```

No servidor institucional, substitua host, porta e serviço pelos dados fornecidos pela instituição. Desative o Auto-commit da ferramenta SQL para testes manuais de rollback.

## Executar

A partir da raiz do projeto, com SDK .NET 8 ou superior capaz de compilar `net8.0` e runtime .NET 8 instalado:

```powershell
dotnet restore CP5_TransacoesLocais/CP5_TransacoesLocais.csproj
dotnet build CP5_TransacoesLocais/CP5_TransacoesLocais.csproj
dotnet run --project CP5_TransacoesLocais/CP5_TransacoesLocais.csproj
```

O arquivo `.csproj` também pode ser aberto no Visual Studio. A solução `.slnx` requer uma versão da IDE/SDK com suporte a esse formato.

## Testar COMMIT e ROLLBACK

Execute o roteiro separadamente para cada banco. Use S03 para consultar SQL Server e S04 para Oracle. As consultas mostram contas, soma dos saldos, histórico e quantidade de transferências. O bloco de reinicialização está comentado e deve ser executado separadamente para restaurar os dados iniciais antes de cada cenário.

1. Reinicialize os dados e consulte: `CC-1001 = 1000,00`, `CC-1002 = 500,00`, histórico vazio.
2. Execute o programa, escolha o banco, origem `CC-1001`, destino `CC-1002`, valor `200` e resposta `N`.
3. Consulte novamente: saldos `800,00` e `700,00`, total `1500,00`, um registro de `200,00` em `TRANSFERENCIA` com origem/destino corretos.
4. Reinicialize os dados e consulte o estado inicial novamente.
5. Repita a transferência com resposta `S`. A falha ocorre após um débito real dentro da transação.
6. Consulte após o ROLLBACK: saldos `1000,00` e `500,00`, total `1500,00`, histórico vazio.

Também verifique saldo insuficiente, conta inexistente, conta repetida, conta inativa, valor zero/negativo e fração de centavo. Nenhum desses casos deve confirmar uma transferência.

## Testes automatizados

O script `verificacao/Testar-SqlServer.ps1` verifica COMMIT, ROLLBACK após o débito, falha na gravação do histórico e validações da operação. Também verifica o tratamento de configuração ausente do Oracle. Os testes transacionais automatizados utilizam SQL Server LocalDB.

Cada execução cria uma database isolada, preservada para consulta, sem alterar a database configurada para uso normal. É necessário ter `sqlcmd` e SQL Server LocalDB disponíveis.

```powershell
dotnet build CP5_TransacoesLocais/CP5_TransacoesLocais.csproj --artifacts-path "$env:TEMP/CP5-validacao-artifacts"
./verificacao/Testar-SqlServer.ps1
```

O roteiro da seção anterior permite reproduzir os testes de COMMIT e ROLLBACK nos dois bancos.

## Evidências

As evidências estão na pasta `CP5_TransacoesLocais/evidencias` e estão organizadas por cenário:

| Evidência | Arquivo | Conteúdo |
|---|---|---|
| E01 | [01_Estrutura.png](CP5_TransacoesLocais/evidencias/01_Estrutura.png) | Estrutura da solução e organização dos arquivos do projeto |
| E02 | [02_sqlserver_commit.png](CP5_TransacoesLocais/evidencias/02_sqlserver_commit.png) | Transferência com COMMIT no SQL Server, saldos e histórico |
| E03 | [03_sqlserver_rollback.png](CP5_TransacoesLocais/evidencias/03_sqlserver_rollback.png) | Falha deliberada e ROLLBACK no SQL Server, com preservação dos saldos e histórico |
| E04 | [04_oracle_commit.png](CP5_TransacoesLocais/evidencias/04_oracle_commit.png) | Transferência com COMMIT no Oracle, saldos e histórico |
| E05 | [05_oracle_rollback.png](CP5_TransacoesLocais/evidencias/05_oracle_rollback.png) | Falha deliberada e ROLLBACK no Oracle, com preservação dos saldos e histórico |
| E06 | [06_codigo_transacao.png](CP5_TransacoesLocais/evidencias/06_codigo_transacao.png) | Código de início da transação, débito, crédito, histórico, COMMIT e ROLLBACK |

## Atomicidade

Débito, crédito e registro da transferência formam uma única unidade de trabalho. O COMMIT confirma o conjunto somente após a conclusão de todas as etapas. Se uma etapa falhar, o ROLLBACK desfaz as alterações da transação, impedindo que um débito permaneça sem o crédito correspondente ou que um histórico parcial seja gravado.

## Configuração e credenciais

As strings de conexão são configuradas por variáveis de ambiente. Os exemplos deste README utilizam marcadores que devem ser substituídos localmente. Senhas e configurações pessoais não devem ser incluídas no código, nos scripts ou nas imagens.

## Entrega

O arquivo deve seguir o padrão `CP5_NOME_RM.zip`, com o nome e RM do integrante responsável pelo envio. O pacote deve incluir o projeto, os scripts SQL, as seis evidências e este README, sem pastas `bin/`, `obj/`, `.vs/` ou arquivos locais com credenciais.
