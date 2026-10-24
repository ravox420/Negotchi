# Hosts Remotos e Handoff

## Objetivo

Permitir executar e continuar conversas em máquinas conectadas por SSH ou em host remoto compatível.

## Contexto

O Negotchi opera somente no computador local. O Codex Desktop pode conectar hosts remotos e transferir uma thread para um contexto correspondente.

## Restrição

Hosts precisam ser identificados e autorizados explicitamente. O handoff não pode transferir credenciais, arquivos locais ou processos sem suporte e confirmação.

## Finalizado Quando

O usuário consegue cadastrar um host, iniciar uma conversa nele e transferir uma conversa compatível entre o computador local e o host, vendo onde ela está ativa.
