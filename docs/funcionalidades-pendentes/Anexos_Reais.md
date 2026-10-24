# Anexos Reais

## Objetivo

Permitir incluir arquivos e imagens como anexos da conversa, além de referências de caminho.

## Contexto

Hoje “Anexar caminho” somente escreve o caminho local no rascunho; o agente precisa decidir se consegue ler o arquivo. O Codex Desktop aceita anexos e colagem de conteúdo.

## Restrição

A interface deve deixar explícito se o arquivo será enviado, apenas referenciado ou processado localmente, além de aplicar limites de tipo e tamanho.

## Estado atual — concluído

PNG, JPEG, WebP, BMP e GIF recebem pré-visualização antes do envio e aparecem dentro da mensagem enviada; GIFs são animados pelo próprio WPF. MP4, WebM, WMV, AVI, MOV e MKV recebem um player na prévia e no chat, conforme os codecs disponíveis no Windows. O vídeo não é enviado como binário ao agente: apenas seu caminho local é informado, de forma explícita. Arquivos de texto são lidos somente na preparação do envio; imagens podem ser escolhidas, arrastadas ou coladas e podem ser removidas antes de enviar.

## Finalizado Quando

O usuário consegue arrastar, escolher ou colar um arquivo ou imagem compatível, vê-lo listado antes do envio e remove-o sem que seus dados sejam enviados antecipadamente.
