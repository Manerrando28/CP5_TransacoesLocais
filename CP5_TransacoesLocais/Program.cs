using System;
using System.Data;
using System.Data.Common;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Oracle.ManagedDataAccess.Client;

namespace CP5_TransacoesLocais
{
    internal class Program
    {
        private static readonly string SqlServerConnStr = Environment.GetEnvironmentVariable("CP5_SQLSERVER_CONNECTION_STRING")
            ?? @"Server=(localdb)\MSSQLLocalDB;Database=CP5_DB;Trusted_Connection=True;TrustServerCertificate=True;";
        private static readonly string OracleConnStr = Environment.GetEnvironmentVariable("CP5_ORACLE_CONNECTION_STRING") ?? "";

        static void Main(string[] args)
        {
            while (true)
            {
                if (!Console.IsOutputRedirected) Console.Clear();
                Console.WriteLine("==================================================");
                Console.WriteLine("  CP5 - TRANSAÇÕES LOCAIS (SQL SERVER / ORACLE)  ");
                Console.WriteLine("==================================================");
                Console.WriteLine("1 - Executar Transferência no SQL Server");
                Console.WriteLine("2 - Executar Transferência no Oracle");
                Console.WriteLine("0 - Sair");
                Console.WriteLine("==================================================");
                Console.Write("Escolha uma opção: ");

                string? opcao = Console.ReadLine();

                if (opcao is null or "0") break;

                if (opcao == "1" || opcao == "2")
                {
                    Console.WriteLine("\n--- DADOS DA TRANSFERÊNCIA ---");
                    Console.Write("Conta Origem (ex: CC-1001): ");
                    string origem = (Console.ReadLine() ?? "").Trim();

                    Console.Write("Conta Destino (ex: CC-1002): ");
                    string destino = (Console.ReadLine() ?? "").Trim();

                    Console.Write("Valor a transferir (R$): ");
                    if (!decimal.TryParse((Console.ReadLine() ?? "").Replace(',', '.'),
                        NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                        CultureInfo.InvariantCulture, out decimal valor) || valor <= 0 ||
                        valor > 9999999999999999.99m || decimal.Round(valor, 2) != valor)
                    {
                        Console.WriteLine("\n[ERRO] Valor inválido! Use valor positivo com até duas casas decimais, sem separador de milhar (máximo 16 dígitos inteiros).");
                        Pausar();
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(origem) || string.IsNullOrWhiteSpace(destino) ||
                        origem.Length > 20 || destino.Length > 20 || origem.Equals(destino, StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("[ERRO] Informe duas contas distintas, com até 20 caracteres.");
                        Pausar();
                        continue;
                    }

                    Console.Write("Deseja PROVOCAR UM ERRO proposital para testar o ROLLBACK? (S/N): ");
                    string resposta = (Console.ReadLine() ?? "").Trim().ToUpperInvariant();
                    if (resposta is not ("S" or "N"))
                    {
                        Console.WriteLine("[ERRO] Responda S ou N.");
                        continue;
                    }
                    bool simularErro = resposta == "S";

                    if (opcao == "1")
                    {
                        ExecutarTransferenciaSqlServer(origem, destino, valor, simularErro);
                    }
                    else
                    {
                        ExecutarTransferenciaOracle(origem, destino, valor, simularErro);
                    }
                }
                else
                {
                    Console.WriteLine("\nOpção inválida!");
                }

                Pausar();
            }
        }

        private static void Pausar()
        {
            if (Console.IsInputRedirected) return;
            Console.WriteLine("\nPressione qualquer tecla para continuar...");
            Console.ReadKey();
        }

        #region --- SQL SERVER ---

        private static void ExecutarTransferenciaSqlServer(string contaOrigem, string contaDestino, decimal valor, bool simularErro)
        {
            Console.WriteLine("\nIniciando operação no SQL Server...");

            using SqlConnection conexao = new SqlConnection(SqlServerConnStr);
            try
            {
                conexao.Open();

                // INÍCIO DA TRANSAÇÃO LOCAL (ACID)
                using SqlTransaction transacao = conexao.BeginTransaction();

                try
                {
                    ValidarContas(conexao, transacao, false, contaOrigem, contaDestino, valor);
                    // Passo 1: Debitar da Origem
                    string sqlDebito = "UPDATE CONTA SET Saldo = Saldo - @Valor WHERE Numero = @Numero AND Ativa = 1 AND Saldo >= @Valor";
                    using (SqlCommand cmd = new SqlCommand(sqlDebito, conexao, transacao))
                    {
                        var parametroValor = cmd.Parameters.Add("@Valor", SqlDbType.Decimal);
                        parametroValor.Precision = 18;
                        parametroValor.Scale = 2;
                        parametroValor.Value = valor;
                        cmd.Parameters.AddWithValue("@Numero", contaOrigem);
                        if (cmd.ExecuteNonQuery() != 1)
                            throw new InvalidOperationException("A operação não afetou exatamente um registro.");
                    }

                    // Passo 2: SIMULAÇÃO DE ERRO CONTROLADO (ROLLBACK)
                    if (simularErro)
                    {
                        throw new Exception("FALHA PROPOSITAL: Erro forçado após o débito para testar o ROLLBACK!");
                    }

                    // Passo 3: Creditar no Destino
                    string sqlCredito = "UPDATE CONTA SET Saldo = Saldo + @Valor WHERE Numero = @Numero AND Ativa = 1";
                    using (SqlCommand cmd = new SqlCommand(sqlCredito, conexao, transacao))
                    {
                        var parametroValor = cmd.Parameters.Add("@Valor", SqlDbType.Decimal);
                        parametroValor.Precision = 18;
                        parametroValor.Scale = 2;
                        parametroValor.Value = valor;
                        cmd.Parameters.AddWithValue("@Numero", contaDestino);
                        if (cmd.ExecuteNonQuery() != 1)
                            throw new InvalidOperationException("A operação não afetou exatamente um registro.");
                    }

                    // Passo 4: Histórico de Transferência
                    string sqlHistorico = @"INSERT INTO TRANSFERENCIA (ContaOrigemId, ContaDestinoId, Valor, DataTransferencia, Observacao)
                                            VALUES (
                                                (SELECT Id FROM CONTA WHERE Numero = @Origem),
                                                (SELECT Id FROM CONTA WHERE Numero = @Destino),
                                                @Valor, GETDATE(), 'Transferência efetuada via C#'
                                            )";
                    using (SqlCommand cmd = new SqlCommand(sqlHistorico, conexao, transacao))
                    {
                        cmd.Parameters.AddWithValue("@Origem", contaOrigem);
                        cmd.Parameters.AddWithValue("@Destino", contaDestino);
                        var parametroValor = cmd.Parameters.Add("@Valor", SqlDbType.Decimal);
                        parametroValor.Precision = 18;
                        parametroValor.Scale = 2;
                        parametroValor.Value = valor;
                        if (cmd.ExecuteNonQuery() != 1)
                            throw new InvalidOperationException("A operação não afetou exatamente um registro.");
                    }

                    // CONFIRMAÇÃO (COMMIT)
                    transacao.Commit();
                    Console.WriteLine("\n[SUCESSO] Transferência concluída e COMMIT realizado com êxito!");
                }
                catch (Exception ex)
                {
                    // CANCELAMENTO (ROLLBACK)
                    Desfazer(transacao, ex);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[ERRO DE CONEXÃO]: {ex.Message}");
            }
        }

        #endregion

        #region --- ORACLE ---

        private static void ExecutarTransferenciaOracle(string contaOrigem, string contaDestino, decimal valor, bool simularErro)
        {
            Console.WriteLine("\nIniciando operação no Oracle...");

            try
            {
                if (string.IsNullOrWhiteSpace(OracleConnStr))
                    throw new InvalidOperationException("Configure CP5_ORACLE_CONNECTION_STRING antes de usar o Oracle.");
                using OracleConnection conexao = new OracleConnection(OracleConnStr);
                conexao.Open();

                // INÍCIO DA TRANSAÇÃO LOCAL NO ORACLE (ACID)
                using OracleTransaction transacao = conexao.BeginTransaction();

                try
                {
                    ValidarContas(conexao, transacao, true, contaOrigem, contaDestino, valor);
                    // 1. Débito na Conta Origem
                    string sqlDebito = "UPDATE CONTA SET Saldo = Saldo - :Valor WHERE Numero = :Numero AND Ativa = 1 AND Saldo >= :Valor";
                    using (OracleCommand cmd = new OracleCommand(sqlDebito, conexao))
                    {
                        cmd.Transaction = transacao;
                        cmd.BindByName = true;
                        cmd.Parameters.Add(new OracleParameter("Valor", OracleDbType.Decimal) { Value = valor, Precision = 18, Scale = 2 });
                        cmd.Parameters.Add(new OracleParameter("Numero", contaOrigem));
                        if (cmd.ExecuteNonQuery() != 1)
                            throw new InvalidOperationException("A operação não afetou exatamente um registro.");
                    }

                    // 2. SIMULAÇÃO DE ERRO CONTROLADO (E05 - ROLLBACK)
                    if (simularErro)
                    {
                        throw new Exception("FALHA PROPOSITAL: Erro forçado após o débito para testar o ROLLBACK no Oracle!");
                    }

                    // 3. Crédito na Conta Destino
                    string sqlCredito = "UPDATE CONTA SET Saldo = Saldo + :Valor WHERE Numero = :Numero AND Ativa = 1";
                    using (OracleCommand cmd = new OracleCommand(sqlCredito, conexao))
                    {
                        cmd.Transaction = transacao;
                        cmd.BindByName = true;
                        cmd.Parameters.Add(new OracleParameter("Valor", OracleDbType.Decimal) { Value = valor, Precision = 18, Scale = 2 });
                        cmd.Parameters.Add(new OracleParameter("Numero", contaDestino));
                        if (cmd.ExecuteNonQuery() != 1)
                            throw new InvalidOperationException("A operação não afetou exatamente um registro.");
                    }

                    // 4. Registro no Histórico de Transferências
                    string sqlHistorico = @"INSERT INTO TRANSFERENCIA (ContaOrigemId, ContaDestinoId, Valor, DataTransferencia, Observacao)
                                            VALUES (
                                                (SELECT Id FROM CONTA WHERE Numero = :Origem),
                                                (SELECT Id FROM CONTA WHERE Numero = :Destino),
                                                :Valor, SYSDATE, 'Transferência efetuada via C#'
                                            )";
                    using (OracleCommand cmd = new OracleCommand(sqlHistorico, conexao))
                    {
                        cmd.Transaction = transacao;
                        cmd.BindByName = true;
                        cmd.Parameters.Add(new OracleParameter("Origem", contaOrigem));
                        cmd.Parameters.Add(new OracleParameter("Destino", contaDestino));
                        cmd.Parameters.Add(new OracleParameter("Valor", OracleDbType.Decimal) { Value = valor, Precision = 18, Scale = 2 });
                        if (cmd.ExecuteNonQuery() != 1)
                            throw new InvalidOperationException("A operação não afetou exatamente um registro.");
                    }

                    // CONFIRMAÇÃO DA TRANSAÇÃO (E04 - COMMIT)
                    transacao.Commit();
                    Console.WriteLine("\n[SUCESSO] Transferência no Oracle concluída com COMMIT efetuado!");
                }
                catch (Exception ex)
                {
                    // CANCELAMENTO DA TRANSAÇÃO (E05 - ROLLBACK)
                    Desfazer(transacao, ex);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[ERRO DE CONFIGURAÇÃO/CONEXÃO - ORACLE] {ex.Message}");
            }
        }

        // As leituras bloqueiam as contas até o fim da mesma transação usada pelos comandos DML.
        private static void ValidarContas(DbConnection conexao, DbTransaction transacao, bool oracle,
            string origem, string destino, decimal valor)
        {
            int? origemId = null;
            int? destinoId = null;
            foreach (string numero in new[] { origem, destino }.OrderBy(n => n, StringComparer.Ordinal))
            {
                using DbCommand cmd = conexao.CreateCommand();
                cmd.Transaction = transacao;
                cmd.CommandText = oracle
                    ? "SELECT Id, Saldo, Ativa FROM CONTA WHERE Numero = :Numero FOR UPDATE"
                    : "SELECT Id, Saldo, Ativa FROM CONTA WITH (UPDLOCK, HOLDLOCK) WHERE Numero = @Numero";
                if (cmd is OracleCommand oc) oc.BindByName = true;
                DbParameter parametro = cmd.CreateParameter();
                parametro.ParameterName = "Numero";
                parametro.DbType = DbType.AnsiString;
                parametro.Size = 20;
                parametro.Value = numero;
                cmd.Parameters.Add(parametro);
                using DbDataReader leitor = cmd.ExecuteReader();
                if (!leitor.Read()) throw new InvalidOperationException($"Conta {numero} não encontrada.");
                if (Convert.ToInt32(leitor.GetValue(2)) != 1)
                    throw new InvalidOperationException($"Conta {numero} inativa.");
                int id = Convert.ToInt32(leitor.GetValue(0));
                if (numero == origem)
                {
                    origemId = id;
                    if (leitor.GetDecimal(1) < valor) throw new InvalidOperationException("Saldo insuficiente.");
                }
                else destinoId = id;
            }
            if (origemId == destinoId) throw new InvalidOperationException("As contas devem ser distintas.");
        }

        private static void Desfazer(DbTransaction transacao, Exception erro)
        {
            Console.WriteLine($"[ERRO] {erro.Message}");
            try
            {
                transacao.Rollback();
                Console.WriteLine("[ROLLBACK EXECUTADO] As alterações desta transação foram desfeitas.");
            }
            catch (Exception rollbackErro)
            {
                Console.WriteLine($"[FALHA NO ROLLBACK] {rollbackErro.Message}. Confira o banco antes de repetir.");
            }
        }

        #endregion
    }
}