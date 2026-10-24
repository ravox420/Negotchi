# Sequência de implementação

Pedido: concluir as partes incompletas e implementar os itens restantes em sequência.

Cada item só passa a concluído depois de implementação e validação do seu contrato. Limitações externas devem ser registradas; um botão sem integração não conta como funcionalidade pronta.

| Ordem | Funcionalidade | Estado |
| --- | --- | --- |
| 1 | Conversas: renomear/arquivar no Codex e prévia | Concluído: operações nativas, prévia cancelável |
| 2 | Git: push confirmado | Concluído: remoto/branch revisados; teste em remoto local |
| 3 | Permissões isoladas por conversa | Concluído: migração e persistência testadas |
| 4 | Gerenciamento de MCP | Concluído: configuração global revisada; OAuth; origem gerenciada somente leitura |
| 5 | Perfil e atividade de conta | Concluído: /account e clique no resumo da conta |
| 6 | Anexos reais | Em andamento |
| 7 | Explorador e busca de arquivos | Pendente |
| 8 | Prévia de artefatos | Pendente |
| 9 | Edição e anotações inline | Pendente |
| 10 | Revisão visual de diffs | Pendente |
| 11 | Terminal integrado | Pendente |
| 12 | Navegador integrado | Pendente |
| 13 | Mensagens enfileiradas e side chats | Pendente |
| 14 | Execução paralela de conversas | Pendente |
| 15 | Metas persistentes | Pendente |
| 16 | Automações agendadas | Pendente |
| 17 | Gerenciamento de plugins | Pendente |
| 18 | Contextos de execução | Pendente |
| 19 | Hosts remotos e handoff | Pendente |
| 20 | Computer Use no Windows | Pendente |
| 21 | Controle remoto por celular | Pendente |
| 22 | Ditado por voz | Pendente |
| 23 | Predições no compositor | Pendente |
| 24 | Atalhos, múltiplas janelas e navegação | Pendente |

Validação da etapa 1–5: `.build-sequence-validation/test-results.json` (185 verificações) e `ui-smoke-report.json` (83 verificações). Escritas de configuração dos testes ficam em CODEX_HOME isolado; push somente em remoto temporário local. Nenhum servidor MCP foi adicionado à configuração real pelo agente.
