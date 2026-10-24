# Terminal Integrado

## Objetivo

Expor terminais associados à conversa para acompanhar e executar comandos com contexto visível.

## Contexto

O Negotchi executa o App Server ocultamente e apresenta aprovações, mas não oferece terminal ao usuário. O Codex Desktop possui terminais por thread.

## Restrição

O terminal deve respeitar o contexto, sandbox e política de aprovação da conversa. Não pode expor comandos ou saídas de outra conversa.

## Finalizado Quando

O usuário consegue abrir mais de uma aba de terminal de uma conversa, acompanhar saída e encerrar processos, com indicação do diretório e contexto de execução.
