# Revisão da entrega CP5

Revisão em 06/10/2026, baseada em D01, D02, D03, D06, D07, D08 e na apresentação fornecida.

## Erros corrigidos

- Oracle informava COMMIT ou ROLLBACK no tratamento de erros de conexão sem ter executado a operação. Agora informa o erro real.
- Senha institucional estava embutida no código. Removida; configuração por variáveis de ambiente.
- Faltavam validações de contas existentes/distintas/ativas e saldo suficiente. Agora ocorrem dentro da transação com bloqueios.
- Comandos ignoravam a quantidade de linhas alteradas. Agora exigem uma linha.
- Valores podiam ter precisão incompatível com DECIMAL(18,2). Entrada validada e parâmetros monetários tipados.
- Oracle dependia da ordem dos parâmetros. Agora usa BindByName, inclusive no débito com parâmetro repetido.
- Falha no Rollback podia ocultar o erro original. Agora as duas falhas são diferenciadas.
- Criação automática do SQL Server omitia FKs e escondia falhas de criação. Removida; S01 é a fonte da estrutura.
- Faltava S02 para criar tabelas e dados Oracle. Incluído com PKs, FKs, identidades e restrições.
- S01 podia rodar em master e não protegia saldo/valor/contas distintas. Incluídos bloqueio de bancos de sistema e restrições.
- Scripts de consulta apagavam os resultados logo depois. Reinicialização agora exige execução explícita do bloco comentado.
- Corrigidos nomes de imagens, arquivo vazio chamado scripts e documentação de configuração/execução.

## Validação realizada

Build .NET: zero erros e zero avisos. Testes reais numa nova base LocalDB: COMMIT, ROLLBACK após débito, saldo insuficiente, origem/destino inexistentes, mesma conta, zero, negativo, fração de centavo, conta inativa e falha no INSERT do histórico. O último caso confirma que débito e crédito são desfeitos e o histórico anterior é preservado. Testado também Oracle sem configuração, que agora informa erro em vez de sucesso. Total: 12 cenários aprovados.

Resultados e identificação da base preservada: `verificacao/resultado-sqlserver.txt`. Testes reproduzíveis: `verificacao/Testar-SqlServer.ps1`.

## Pendências que exigem ambiente ou evidências reais

- Executar S02 e os testes COMMIT/ROLLBACK em Oracle com conexão válida. A revisão não utilizou a senha encontrada no código para acessar o servidor institucional.
- Refazer E01–E06 conforme o roteiro do README. As imagens antigas não comprovam os estados do banco; E06 não mostra todo o fluxo e ficou desatualizada.
- Trocar a senha anteriormente exposta.
- Montar e conferir o ZIP somente após essas verificações, conforme D08. Nenhum ZIP foi gerado com evidências incompletas.
