# Terminal Integrado

## Objetivo

Expor terminais associados à conversa para acompanhar e executar comandos com contexto visível.

## Contexto

O Negotchi executa o App Server ocultamente e apresenta aprovações, mas não oferece terminal ao usuário. O Codex Desktop possui terminais por thread.

## Restrição

O terminal deve respeitar o contexto, sandbox e política de aprovação da conversa. Não pode expor comandos ou saídas de outra conversa.

## Estado atual — concluído

`/terminal` abre abas PowerShell isoladas da conversa atual. Cada execução informa a pasta, a política de sandbox, exige confirmação e tem limite de cinco minutos e 1 MB por fluxo de saída. A saída chega em streaming apenas à aba cujo processo a originou; **Interromper processo** e o fechamento da janela solicitam o encerramento dos processos dessas abas.

## Finalizado Quando

O usuário consegue abrir mais de uma aba de terminal de uma conversa, acompanhar saída e encerrar processos, com indicação do diretório e contexto de execução.
