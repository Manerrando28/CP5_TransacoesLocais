# Checkpoint 5 — Transações Locais em C#

**FIAP — C# Software Development — Turma 3ESPH — 2026.2**

## Integrantes

- Fernando Carlos Colque Huaranca — RM558095
- Gabriel Guilherme Leste — RM558638
- Gabriel Lacerda Araújo — RM558307
- Julia Carolina Ferreira Silva — RM558896

---

## Sobre o projeto

Aplicação console desenvolvida em C# para demonstrar o uso de **transações locais** com SQL Server e Oracle Database.

O cenário implementado simula uma transferência entre contas bancárias. Débito, crédito e registro da movimentação são executados dentro da mesma transação, garantindo que a operação seja confirmada integralmente com `Commit()` ou desfeita com `Rollback()` em caso de falha.

O projeto também possui um modo de erro proposital, utilizado para demonstrar que alterações executadas antes de uma exceção não são persistidas quando a transação é revertida.

### Tecnologias

- C# / .NET 8
- ADO.NET
- Microsoft.Data.SqlClient 7.1.1
- Oracle.ManagedDataAccess.Core 23.26.301
- SQL Server
- Oracle Database

---

## Estrutura

```text
CP5_TransacoesLocais.slnx
README.md

CP5_TransacoesLocais/
├── CP5_TransacoesLocais.csproj
├── Program.cs
├── scripts/
│   ├── S01_SQLServer_Estrutura_Dados.sql
│   ├── S02_Oracle_Estrutura_Dados.sql
│   ├── S03_SQLServer_Testes_Commit_Rollback.sql
│   └── S04_Oracle_Testes_Commit_Rollback.sql
└── evidencias/
    ├── 01_Estrutura.png
    ├── 02_sqlserver_commit.png
    ├── 03_sqlserver_rollback.png
    ├── 04_oracle_commit.png
    ├── 05_oracle_rollback.png
    └── 06_codigo_transacao.png

verificacao/
├── Testar-SqlServer.ps1
└── resultado-sqlserver.txt
```

O modelo utiliza as tabelas `CLIENTE`, `TIPO_CONTA`, `CONTA` e `TRANSFERENCIA`.

---

## Executando com SQL Server

Por padrão, a aplicação utiliza SQL Server LocalDB:

```text
Server=(localdb)\MSSQLLocalDB;
Database=CP5_DB;
Trusted_Connection=True;
TrustServerCertificate=True;
```

### 1. Verificar o LocalDB

```powershell
sqllocaldb info
```

Caso a instância padrão esteja parada:

```powershell
sqllocaldb start MSSQLLocalDB
```

No SQL Server Management Studio, conecte-se utilizando:

```text
(localdb)\MSSQLLocalDB
```

com **Windows Authentication**.

### 2. Criar o banco

```sql
CREATE DATABASE CP5_DB;
GO

USE CP5_DB;
GO
```

### 3. Criar a estrutura

Execute:

```text
CP5_TransacoesLocais/scripts/S01_SQLServer_Estrutura_Dados.sql
```

O script cria as tabelas e carrega os dados utilizados nos cenários de teste.

> A execução do script recria as tabelas do exercício e remove os dados anteriores delas.

### Outra instância SQL Server

Caso seja utilizada outra instância, configure a conexão por variável de ambiente:

```powershell
$env:CP5_SQLSERVER_CONNECTION_STRING = 'Server=SEU_SERVIDOR;Database=CP5_DB;Integrated Security=True;TrustServerCertificate=True;'
```

---

## Executando com Oracle

Execute o script:

```text
CP5_TransacoesLocais/scripts/S02_Oracle_Estrutura_Dados.sql
```

em um schema Oracle destinado ao projeto.

Configure a conexão:

```powershell
$env:CP5_ORACLE_CONNECTION_STRING = 'User Id=SEU_USUARIO;Password=SUA_SENHA;Data Source=HOST:PORTA/SERVICO;'
```

Exemplo com Oracle XE:

```powershell
$env:CP5_ORACLE_CONNECTION_STRING = 'User Id=SEU_USUARIO;Password=SUA_SENHA;Data Source=localhost:1521/XEPDB1;'
```

---

## Executando a aplicação

Na raiz do repositório:

```powershell
dotnet restore CP5_TransacoesLocais/CP5_TransacoesLocais.csproj

dotnet build CP5_TransacoesLocais/CP5_TransacoesLocais.csproj

dotnet run --project CP5_TransacoesLocais/CP5_TransacoesLocais.csproj
```

O programa apresenta as opções:

```text
1 - Executar Transferência no SQL Server
2 - Executar Transferência no Oracle
0 - Sair
```

Após selecionar o banco, informe a conta de origem, conta de destino, valor e se deseja provocar uma falha para testar o rollback.

---

## Fluxo da transação

Em uma operação concluída normalmente:

```text
Débito
  ↓
Crédito
  ↓
Registro em TRANSFERENCIA
  ↓
COMMIT
```

Se ocorrer uma exceção:

```text
Débito
  ↓
Falha
  ↓
ROLLBACK
```

O rollback desfaz as alterações realizadas dentro da transação, evitando que a operação seja persistida parcialmente.

---

## Cenário de COMMIT

Estado inicial:

```text
CC-1001 = 1000,00
CC-1002 = 500,00
```

Transferência:

```text
Origem: CC-1001
Destino: CC-1002
Valor: 200
Erro proposital: N
```

Resultado esperado:

```text
CC-1001 = 800,00
CC-1002 = 700,00
```

Além da atualização dos saldos, deve existir um novo registro de `200,00` em `TRANSFERENCIA`.

---

## Cenário de ROLLBACK

Após restaurar o cenário inicial:

```text
CC-1001 = 1000,00
CC-1002 = 500,00
```

execute:

```text
Origem: CC-1001
Destino: CC-1002
Valor: 200
Erro proposital: S
```

A exceção é provocada após o débito da conta de origem.

Depois do rollback, o estado deve permanecer:

```text
CC-1001 = 1000,00
CC-1002 = 500,00
```

Nenhum registro referente à tentativa deve permanecer em `TRANSFERENCIA`.

---

## Consultas de verificação

Os scripts:

```text
S03_SQLServer_Testes_Commit_Rollback.sql
S04_Oracle_Testes_Commit_Rollback.sql
```

auxiliam na validação dos cenários.

No SQL Server, as principais consultas são:

```sql
SELECT Numero, Saldo
FROM CONTA
WHERE Numero IN ('CC-1001', 'CC-1002');

SELECT SUM(Saldo) AS SaldoTotal
FROM CONTA
WHERE Numero IN ('CC-1001', 'CC-1002');

SELECT *
FROM TRANSFERENCIA;
```

---

## Validações

Antes da transferência, a aplicação verifica:

- existência das contas;
- contas ativas;
- origem e destino diferentes;
- valor positivo;
- até duas casas decimais;
- saldo suficiente.

Uma transferência inválida não é confirmada.

---

## Testes automatizados

O script:

```text
verificacao/Testar-SqlServer.ps1
```

executa automaticamente os principais cenários no SQL Server LocalDB, incluindo COMMIT, ROLLBACK e validações da operação.

```powershell
dotnet build CP5_TransacoesLocais/CP5_TransacoesLocais.csproj --artifacts-path "$env:TEMP/CP5-validacao-artifacts"

./verificacao/Testar-SqlServer.ps1
```

O resultado pode ser consultado em:

```text
verificacao/resultado-sqlserver.txt
```

---

## Evidências

| Evidência | Arquivo | Descrição |
|---|---|---|
| E01 | [01_Estrutura.png](CP5_TransacoesLocais/evidencias/01_Estrutura.png) | Estrutura do projeto |
| E02 | [02_sqlserver_commit.png](CP5_TransacoesLocais/evidencias/02_sqlserver_commit.png) | COMMIT no SQL Server e persistência dos saldos e histórico |
| E03 | [03_sqlserver_rollback.png](CP5_TransacoesLocais/evidencias/03_sqlserver_rollback.png) | ROLLBACK no SQL Server e preservação do estado anterior |
| E04 | [04_oracle_commit.png](CP5_TransacoesLocais/evidencias/04_oracle_commit.png) | COMMIT no Oracle |
| E05 | [05_oracle_rollback.png](CP5_TransacoesLocais/evidencias/05_oracle_rollback.png) | ROLLBACK no Oracle |
| E06 | [06_codigo_transacao.png](CP5_TransacoesLocais/evidencias/06_codigo_transacao.png) | Implementação da transação no código |

---

## Atomicidade

Débito, crédito e histórico pertencem à mesma unidade de trabalho.

O `Commit()` só é executado após a conclusão de todas as etapas. Caso qualquer etapa falhe, o `Rollback()` desfaz as alterações realizadas durante a transação.

Esse comportamento impede estados inconsistentes, como uma conta ser debitada sem que o valor seja creditado na conta de destino.
