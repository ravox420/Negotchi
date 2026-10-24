# Revisão Visual de Diffs

## Objetivo

Permitir revisar alterações de arquivos de forma visual antes de aceitá-las ou prosseguir.

## Contexto

O Negotchi sinaliza atividades de revisão na animação do pet, mas não possui painel de diff ou comentários por trecho. O Codex Desktop oferece essa superfície.

## Restrição

Não deve aplicar, reverter ou publicar mudanças automaticamente. Comentários precisam permanecer vinculados ao arquivo e ao trecho revisado enquanto ele existir.

## Estado atual — concluído

`/diff` apresenta os arquivos alterados, a versão no último commit à esquerda e a versão atual em disco à direita. A navegação usa os trechos reais do patch Git. Comentários são vinculados ao arquivo, à linha, ao trecho e ao hash da versão revisada; antes de adicioná-los ao rascunho, o Negotchi confere se o arquivo ainda corresponde àquela revisão. A janela é somente de revisão: não aplica, reverte nem publica alterações.

## Finalizado Quando

O usuário consegue abrir uma árvore de arquivos alterados, comparar versões, navegar entre trechos e registrar comentários inline que podem ser enviados ao agente como contexto.
