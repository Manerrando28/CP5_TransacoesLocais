# Checkpoint 5 — Transações locais em C#

**FIAP — C# Software Development — Turma 3ESPH — 2026.2**

## Integrantes

- Fernando Carlos Colque Huaranca — RM558095
- Gabriel Guilherme Leste — RM558638
- Gabriel Lacerda Araújo — RM558307
- Julia Carolina Ferreira Silva — RM558896

## Objetivo e tecnologia

Transferir valores entre contas com débito, crédito e registro em `TRANSFERENCIA` dentro da mesma transação local. Cada execução utiliza SQL Server ou Oracle separadamente. A aplicação usa C#, .NET 8 e ADO.NET, com Microsoft.Data.SqlClient 7.1.1 e Oracle.ManagedDataAccess.Core 23.26.301. O enunciado recomenda .NET 9, mas permite versões compatíveis.

O programa valida contas existentes, ativas e distintas, valor positivo com até duas casas decimais e saldo suficiente. As consultas de validação mantêm bloqueios nas contas até o fim da transação. Todos os comandos recebem a mesma conexão/transação, e cada alteração deve afetar exatamente uma linha. No Oracle, `BindByName` associa parâmetros por nome.

`Commit()` ocorre após débito, crédito e histórico. Se houver exceção, `Rollback()` desfaz as alterações. O teste deliberado lança uma exceção após o débito. Falhas de conexão/configuração são informadas como erro e não representam teste de rollback. Uma falha no próprio rollback também é informada, sem afirmar que ele foi concluído.

## Estrutura

```text
CP5_TransacoesLocais.slnx
README.md
REVISAO.md
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

Utilize Oracle 12c ou superior, com suporte a colunas `IDENTITY`. Conecte com um usuário/schema próprio, nunca `SYS` ou `SYSTEM`. No Oracle XE local, use o serviço/PDB correto, normalmente `XEPDB1`; o usuário precisa de `CREATE SESSION`, `CREATE TABLE`, `CREATE SEQUENCE` e quota no tablespace. No ambiente institucional, use os privilégios fornecidos. S00 é opcional e não integra esta entrega.

Execute `CP5_TransacoesLocais/scripts/S02_Oracle_Estrutura_Dados.sql` uma vez num schema do exercício sem essas tabelas. Para novos testes, use o bloco de reinicialização de S04. Se houver tabelas antigas com colunas diferentes, use um schema novo ou adapte-as antes da execução. Não execute DDL durante testes transacionais, pois o Oracle faz commits implícitos.

Configure a conexão localmente, substituindo os marcadores e sem salvar a senha no projeto:

```powershell
$env:CP5_ORACLE_CONNECTION_STRING = 'User Id=SEU_USUARIO;Password=SUA_SENHA_AQUI;Data Source=localhost:1521/XEPDB1;'
```

No servidor institucional, substitua host, porta e serviço pelos dados fornecidos pela instituição. Desative o Auto-commit da ferramenta SQL para testes manuais de rollback.

## Executar

A partir da raiz da entrega, com SDK .NET 8 ou superior capaz de compilar `net8.0` e runtime .NET 8 instalado:

```powershell
dotnet restore CP5_TransacoesLocais/CP5_TransacoesLocais.csproj
dotnet build CP5_TransacoesLocais/CP5_TransacoesLocais.csproj
dotnet run --project CP5_TransacoesLocais/CP5_TransacoesLocais.csproj
```

O arquivo `.csproj` também pode ser aberto no Visual Studio. A solução `.slnx` requer uma versão da IDE/SDK com suporte a esse formato.

## Testar COMMIT e ROLLBACK

Execute o roteiro separadamente para cada banco. Use S03 para consultar SQL Server e S04 para Oracle. As consultas mostram contas, soma dos saldos, histórico e quantidade de transferências. A reinicialização está comentada para que consultar não apague a evidência: selecione e execute apenas esse bloco quando quiser restaurar o cenário.

1. Reinicialize os dados e consulte: `CC-1001 = 1000,00`, `CC-1002 = 500,00`, histórico vazio.
2. Execute o programa, escolha o banco, origem `CC-1001`, destino `CC-1002`, valor `200` e resposta `N`.
3. Consulte novamente: saldos `800,00` e `700,00`, total `1500,00`, um registro de `200,00` em `TRANSFERENCIA` com origem/destino corretos. Capture o estado antes/depois e a mensagem de COMMIT.
4. Reinicialize os dados e consulte o estado inicial novamente.
5. Repita a transferência com resposta `S`. A falha ocorre após um débito real dentro da transação.
6. Consulte após o ROLLBACK: saldos `1000,00` e `500,00`, total `1500,00`, histórico vazio. Capture a falha e os resultados das consultas antes de reinicializar.

Também verifique saldo insuficiente, conta inexistente, conta repetida, conta inativa, valor zero/negativo e fração de centavo. Nenhum desses casos deve confirmar uma transferência.

## Verificação automatizada

A revisão compilou o projeto sem erros nem avisos e executou 12 cenários, incluindo COMMIT, falha após débito, falha no INSERT após débito/crédito, validações e Oracle sem configuração. Veja `verificacao/resultado-sqlserver.txt`. Os testes SQL Server usam uma nova database LocalDB, preservada para inspeção; não alteram a database configurada para uso normal.

Para repetir:

```powershell
dotnet build CP5_TransacoesLocais/CP5_TransacoesLocais.csproj --artifacts-path "$env:TEMP/CP5-validacao-artifacts"
./verificacao/Testar-SqlServer.ps1
```

**COMMIT e ROLLBACK reais no Oracle ainda precisam ser validados com acesso ao banco.** O teste de configuração ausente não comprova a transação Oracle.

## Evidências e pendências

As seis imagens originais foram preservadas; nomes com extensão duplicada foram corrigidos. Elas ainda precisam ser atualizadas para a versão corrigida:

| Evidência | Situação |
|---|---|
| E01 — estrutura | Atualizar para incluir S02 e os demais arquivos novos |
| E02 — SQL Server COMMIT | Imagem antiga mostra apenas console; acrescentar consultas antes/depois e histórico |
| E03 — SQL Server ROLLBACK | Acrescentar consultas que comprovem saldos e histórico preservados |
| E04 — Oracle COMMIT | Refazer com execução real e consultas; a versão anterior ocultava erros de conexão |
| E05 — Oracle ROLLBACK | Refazer após falha deliberada real e consultar saldos/histórico |
| E06 — código | Atualizar para mostrar início da transação, débito, crédito, INSERT, COMMIT e ROLLBACK |

Os logs de teste ajudam a verificar o comportamento, mas não substituem as seis imagens exigidas. O D07 é opcional. A entrega ainda não deve ser considerada concluída enquanto faltarem a validação Oracle e a atualização das evidências.

## Segurança e entrega

As conexões usam variáveis de ambiente e não contêm senha institucional no código. A senha que estava no arquivo original deve ser trocada pelo titular, pois sua remoção não elimina cópias anteriores.

Depois de atualizar e conferir as evidências, compacte como `CP5_NOME_RM.zip`, substituindo pelo integrante que fará o upload. Exclua `bin/`, `obj/`, `.vs/` e qualquer configuração com senha. Todos os integrantes estão identificados acima.
