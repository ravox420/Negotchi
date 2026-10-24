# Contextos de Execução

## Objetivo

Permitir escolher onde e como uma conversa executa trabalho: pasta local, worktree, WSL ou ambiente em nuvem.

## Contexto

O Negotchi inicia a thread na pasta selecionada. O Codex Desktop permite selecionar contextos isolados e locais de execução distintos.

## Restrição

Cada contexto deve declarar claramente seus arquivos, serviços e limites. A escolha de ambiente não concede acesso automático a credenciais, processos ou arquivos de outro contexto.

## Finalizado Quando

Ao criar uma conversa, o usuário pode escolher um contexto suportado e a interface exibe onde ela está executando; mudanças feitas nela permanecem no contexto selecionado.
