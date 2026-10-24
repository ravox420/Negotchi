# Predições no Compositor

## Objetivo

Sugerir um próximo pedido editável após uma resposta do agente.

## Contexto

O campo de mensagem do Negotchi possui comandos e rascunho, mas não sugere continuação contextual. O Codex Desktop oferece Composer Predictions quando elegível.

## Restrição

A sugestão nunca pode ser enviada automaticamente e deve desaparecer ao usuário começar a escrever outra mensagem.

## Finalizado Quando

Depois de uma resposta elegível, o campo mostra uma sugestão distinta que o usuário pode aceitar com Tab, editar ou ignorar antes de enviar.
