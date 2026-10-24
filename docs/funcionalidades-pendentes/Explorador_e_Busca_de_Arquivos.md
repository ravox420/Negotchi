# Explorador e Busca de Arquivos

## Objetivo

Permitir localizar e abrir arquivos da pasta de trabalho sem depender de caminhos digitados.

## Contexto

O Negotchi permite anexar um caminho escolhido pelo diálogo nativo, mas não mostra a árvore da pasta nem busca de arquivos. O Codex Desktop oferece ambos.

## Restrição

O explorador deve permanecer limitado ao contexto da conversa e à política de acesso. Localizar ou visualizar arquivo não autoriza sua modificação.

## Estado atual — concluído

`/files` abre uma árvore limitada à pasta da conversa, sem percorrer links ou junctions. A busca por nome percorre as subpastas em segundo plano, com limite de mil resultados. Um arquivo encontrado pode ser pré-visualizado pelo visualizador adequado, aberto no aplicativo padrão do Windows após a revisão exigida ou inserido como referência no rascunho. A edição continua submetida à política de sandbox da conversa.

## Finalizado Quando

O usuário consegue navegar pela árvore da pasta, buscar por nome e abrir um arquivo encontrado no visualizador adequado ou no aplicativo padrão.
