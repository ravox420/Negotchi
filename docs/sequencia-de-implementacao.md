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
| 6 | Anexos reais | Concluído: escolha, arrastar/soltar e colagem de imagem; prévia local de imagem, GIF e vídeo; texto e imagem enviados apenas no envio; vídeo referenciado por caminho |
| 7 | Explorador e busca de arquivos | Concluído: árvore sob a pasta da conversa, busca por nome, prévia, abertura revisada no Windows e referência ao rascunho |
| 8 | Prévia de artefatos | Concluído: prévia local somente leitura de texto, imagens, PDF, DOCX, XLSX e PPTX, com limites e mensagem para formatos sem suporte |
| 9 | Edição e anotações inline | Concluído: editor de texto limitado ao contexto, anotação de seleção no rascunho, confirmação e política de sandbox no salvamento |
| 10 | Revisão visual de diffs | Concluído: comparação HEAD/disco, navegação por trechos e comentários versionados enviados apenas ao rascunho |
| 11 | Terminal integrado | Concluído: abas por conversa, execução revisada com sandbox, saída em streaming e interrupção de processos |
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
| 25 | [Orquestrador de modelos por complexidade do prompt](funcionalidades-pendentes/Orquestrador_de_Modelos_por_Complexidade.md) | Pendente |

Validação das etapas 1–11: `.build-incomplete-validation/test-results.json` (205 verificações) e `ui-smoke-report.json` (87 verificações). Escritas de configuração dos testes ficam em CODEX_HOME isolado; push somente em remoto temporário local. Nenhum servidor MCP foi adicionado à configuração real pelo agente.
