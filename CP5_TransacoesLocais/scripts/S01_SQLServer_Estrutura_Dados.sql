-- ATENÇÃO: recria as tabelas e apaga os dados do exercício.
-- Selecione a database do CP5 antes de executar. Nunca use master.
IF DB_NAME() IN ('master', 'model', 'msdb', 'tempdb')
    THROW 50001, 'Selecione a database do CP5 antes de executar S01.', 1;
SET XACT_ABORT ON;
BEGIN TRANSACTION;
-- Criar tabelas para o SQL Server
IF OBJECT_ID('TRANSFERENCIA', 'U') IS NOT NULL DROP TABLE TRANSFERENCIA;
IF OBJECT_ID('CONTA', 'U') IS NOT NULL DROP TABLE CONTA;
IF OBJECT_ID('TIPO_CONTA', 'U') IS NOT NULL DROP TABLE TIPO_CONTA;
IF OBJECT_ID('CLIENTE', 'U') IS NOT NULL DROP TABLE CLIENTE;

CREATE TABLE CLIENTE (
    Id INT PRIMARY KEY IDENTITY(1,1),
    Nome VARCHAR(100) NOT NULL,
    Documento VARCHAR(20) NOT NULL,
    Ativo BIT NOT NULL DEFAULT 1
);

CREATE TABLE TIPO_CONTA (
    Id INT PRIMARY KEY IDENTITY(1,1),
    Descricao VARCHAR(50) NOT NULL
);

CREATE TABLE CONTA (
    Id INT PRIMARY KEY IDENTITY(1,1),
    ClienteId INT NOT NULL FOREIGN KEY REFERENCES CLIENTE(Id),
    TipoContaId INT NOT NULL FOREIGN KEY REFERENCES TIPO_CONTA(Id),
    Numero VARCHAR(20) NOT NULL UNIQUE,
    Saldo DECIMAL(18,2) NOT NULL CHECK (Saldo >= 0),
    Ativa BIT NOT NULL DEFAULT 1
);

CREATE TABLE TRANSFERENCIA (
    Id INT PRIMARY KEY IDENTITY(1,1),
    ContaOrigemId INT NOT NULL FOREIGN KEY REFERENCES CONTA(Id),
    ContaDestinoId INT NOT NULL FOREIGN KEY REFERENCES CONTA(Id),
    Valor DECIMAL(18,2) NOT NULL CHECK (Valor > 0),
    DataTransferencia DATETIME NOT NULL DEFAULT GETDATE(),
    Observacao VARCHAR(255) NULL,
    CHECK (ContaOrigemId <> ContaDestinoId)
);

-- Inserir dados iniciais
INSERT INTO CLIENTE (Nome, Documento, Ativo) VALUES ('Cliente Origem', '11111111111', 1);
INSERT INTO CLIENTE (Nome, Documento, Ativo) VALUES ('Cliente Destino', '22222222222', 1);

INSERT INTO TIPO_CONTA (Descricao) VALUES ('Conta Corrente');

INSERT INTO CONTA (ClienteId, TipoContaId, Numero, Saldo, Ativa) VALUES (1, 1, 'CC-1001', 1000.00, 1);
INSERT INTO CONTA (ClienteId, TipoContaId, Numero, Saldo, Ativa) VALUES (2, 1, 'CC-1002', 500.00, 1);
COMMIT;
