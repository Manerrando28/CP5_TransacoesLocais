-- Execute as consultas antes e depois de cada teste na aplicação C#.
-- COMMIT: CC-1001 -> CC-1002, 200, N: saldos 800/700 e um novo histórico.
-- ROLLBACK: reinicie antes; execute 200, S: saldos 1000/500 e zero históricos.
-- Oracle: desative Auto-commit para testes manuais de rollback.
SELECT Id, Numero, Saldo, Ativa FROM CONTA ORDER BY Numero;
SELECT SUM(Saldo) AS TotalSaldos FROM CONTA;
SELECT * FROM TRANSFERENCIA ORDER BY Id;
SELECT COUNT(*) AS QuantidadeTransferencias FROM TRANSFERENCIA;

-- REINICIALIZAÇÃO OPCIONAL: selecione e execute somente o bloco abaixo,
-- removendo os delimitadores de comentário. Apaga o histórico do exercício.
-- Faça isso ANTES do próximo teste, nunca antes de capturar o estado final.
/*
DELETE FROM TRANSFERENCIA;
UPDATE CONTA SET Saldo = 1000.00, Ativa = 1 WHERE Numero = 'CC-1001';
UPDATE CONTA SET Saldo = 500.00, Ativa = 1 WHERE Numero = 'CC-1002';
COMMIT;
*/
