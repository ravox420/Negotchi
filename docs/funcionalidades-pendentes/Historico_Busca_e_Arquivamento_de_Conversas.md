# Histórico, Busca e Arquivamento de Conversas

## Objetivo

Permitir recuperar e organizar conversas anteriores.

## Contexto

O Negotchi persiste somente a conversa corrente em seu estado local. O Codex Desktop oferece histórico, busca e arquivamento de chats.

## Restrição

Não inclui o conteúdo de conversas remotas, nem substitui a gestão de projetos. A busca deve respeitar os dados locais disponíveis e não executar ações no projeto.

### Ampliação solicitada: histórico do Codex instalado

O seletor de conversas também deve listar as sessões do Codex instalado como `codex resume --all`, sem filtrar por pasta, com paginação completa, busca e visual inspirado no CLI. Esse histórico é acessado pelo App Server local (`thread/list`, `thread/read`, `thread/turns/list`), sem conexão a hosts remotos. Abrir uma sessão arquivada pela ação explícita **Restaurar e retomar** usa `thread/unarchive`. A leitura deve concluir antes de substituir a conversa ativa. A gestão de projetos e o arquivamento local permanecem acessíveis separadamente.

## Finalizado Quando

O usuário consegue visualizar conversas anteriores, pesquisar por título ou conteúdo disponível, arquivar e restaurar uma conversa, sem apagar seu histórico.
