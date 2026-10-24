# Execução Paralela e Coordenação de Threads

## Objetivo

Permitir que tarefas independentes sejam conduzidas em conversas separadas ao mesmo tempo.

## Contexto

O Negotchi bloqueia o envio enquanto há um turno em andamento e trabalha com uma única thread. O Codex Desktop coordena threads, subagentes e tarefas em segundo plano.

## Restrição

Não cria metas persistentes nem automações agendadas. Cada tarefa deve manter isolamento de contexto e deixar claro qual está em execução.

## Finalizado Quando

O usuário consegue iniciar e acompanhar pelo menos duas tarefas independentes, alternar entre elas e receber o resultado de cada uma sem misturar mensagens, permissões ou alterações.
