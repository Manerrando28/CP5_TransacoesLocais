param([string]$Dll = "$env:TEMP/CP5-validacao-artifacts/bin/CP5_TransacoesLocais/debug/CP5_TransacoesLocais.dll")
$ErrorActionPreference = 'Stop'
$database = 'CP5_Validacao_' + [Guid]::NewGuid().ToString('N').Substring(0,8)
function Sql([string]$Query) {
    $result = & sqlcmd -S '(localdb)\MSSQLLocalDB' -d $database -E -b -h -1 -W -Q "SET NOCOUNT ON; $Query" 2>&1
    if ($LASTEXITCODE -ne 0) { throw ($result -join "`n") }
    return ($result -join "`n").Trim()
}
function Estado { Sql "SELECT CONCAT(Numero, ':', Saldo) FROM CONTA ORDER BY Numero; SELECT CONCAT('historico:', COUNT(*), ':', COALESCE(SUM(Valor),0)) FROM TRANSFERENCIA;" }
function Rodar([string[]]$Entradas) {
    return (($Entradas -join "`n") | & dotnet $Dll) -join "`n"
}
$anteriorSql = $env:CP5_SQLSERVER_CONNECTION_STRING
$anteriorOracle = $env:CP5_ORACLE_CONNECTION_STRING
try {
    & sqlcmd -S '(localdb)\MSSQLLocalDB' -d master -E -b -Q "CREATE DATABASE [$database]"
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao criar base isolada.' }
    & sqlcmd -S '(localdb)\MSSQLLocalDB' -d $database -E -b -i "$PSScriptRoot/../CP5_TransacoesLocais/scripts/S01_SQLServer_Estrutura_Dados.sql"
    if ($LASTEXITCODE -ne 0) { throw 'Falha no S01.' }
    $env:CP5_SQLSERVER_CONNECTION_STRING = "Server=(localdb)\MSSQLLocalDB;Database=$database;Trusted_Connection=True;TrustServerCertificate=True;"
    $env:CP5_ORACLE_CONNECTION_STRING = ''
    Write-Output "Base isolada: $database"
    $inicial = Estado
    Write-Output "ANTES:`n$inicial"
    $saida = Rodar @('1','CC-1001','CC-1002','200','S','0')
    if ($saida -notmatch 'FALHA PROPOSITAL' -or $saida -notmatch 'ROLLBACK EXECUTADO' -or (Estado) -ne $inicial) { throw 'ROLLBACK falhou.' }
    Write-Output "PASS ROLLBACK após débito`n$(Estado)"
    $saida = Rodar @('1','CC-1001','CC-1002','200','N','0')
    $depois = Estado
    if ($saida -notmatch '\[SUCESSO\]' -or $depois -notmatch 'CC-1001:800.00' -or $depois -notmatch 'CC-1002:700.00' -or $depois -notmatch 'historico:1:200.00') { throw "COMMIT falhou: $saida $depois" }
    Write-Output "PASS COMMIT`n$depois"
    $casos = @(
        @{Nome='saldo insuficiente'; Entrada=@('1','CC-1001','CC-1002','900','N','0'); Mensagem='Saldo insuficiente'},
        @{Nome='destino inexistente'; Entrada=@('1','CC-1001','INEXISTENTE','10','N','0'); Mensagem='não encontrada'},
        @{Nome='origem inexistente'; Entrada=@('1','INEXISTENTE','CC-1002','10','N','0'); Mensagem='não encontrada'},
        @{Nome='mesma conta'; Entrada=@('1','CC-1001','CC-1001','10','0'); Mensagem='contas distintas'},
        @{Nome='valor zero'; Entrada=@('1','CC-1001','CC-1002','0','0'); Mensagem='Valor inválido'},
        @{Nome='valor negativo'; Entrada=@('1','CC-1001','CC-1002','-10','0'); Mensagem='Valor inválido'},
        @{Nome='fração de centavo'; Entrada=@('1','CC-1001','CC-1002','0,001','0'); Mensagem='Valor inválido'},
        @{Nome='Oracle sem configuração'; Entrada=@('2','CC-1001','CC-1002','10','N','0'); Mensagem='Configure CP5_ORACLE'}
    )
    foreach ($caso in $casos) {
        $saida = Rodar $caso.Entrada
        if ($saida -notmatch $caso.Mensagem -or $saida -match '\[SUCESSO\]' -or (Estado) -ne $depois) { throw "Falhou: $($caso.Nome): $saida" }
        Write-Output "PASS $($caso.Nome)"
    }
    Sql "UPDATE CONTA SET Ativa=0 WHERE Numero='CC-1002'" | Out-Null
    $saida = Rodar @('1','CC-1001','CC-1002','10','N','0')
    if ($saida -notmatch 'inativa' -or (Estado) -ne $depois) { throw 'Conta inativa falhou.' }
    Write-Output 'PASS conta inativa'
    Sql "UPDATE CONTA SET Ativa=1 WHERE Numero='CC-1002'; ALTER TABLE TRANSFERENCIA ADD CONSTRAINT CK_Teste_Falha CHECK (Valor <> 13);" | Out-Null
    $saida = Rodar @('1','CC-1001','CC-1002','13','N','0')
    if ($saida -notmatch 'ROLLBACK EXECUTADO' -or (Estado) -ne $depois) { throw 'Falha no histórico deixou alteração parcial.' }
    Write-Output 'PASS falha no INSERT desfaz débito e crédito e preserva histórico anterior'
    Sql 'ALTER TABLE TRANSFERENCIA DROP CONSTRAINT CK_Teste_Falha;' | Out-Null
    Write-Output 'Todos os 12 cenários passaram. Base preservada para inspeção.'
}
finally {
    $env:CP5_SQLSERVER_CONNECTION_STRING = $anteriorSql
    $env:CP5_ORACLE_CONNECTION_STRING = $anteriorOracle
}
