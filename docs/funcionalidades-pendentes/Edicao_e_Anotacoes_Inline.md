# Edição e Anotações Inline

## Objetivo

Permitir editar Markdown e código e solicitar alterações ao agente a partir de um trecho selecionado.

## Contexto

As mensagens do Negotchi são exibidas como texto e os arquivos são tratados externamente. O Codex Desktop permite edição e anotações dentro do aplicativo.

## Restrição

Edições locais devem ser identificáveis antes de salvar. Uma seleção ou anotação não pode modificar o arquivo sem ação explícita do usuário ou aprovação requerida.

## Estado atual — concluído

Pelo explorador, **Editar / anotar** abre arquivos de texto de até 2 MB dentro da pasta da conversa. Alterações permanecem locais até o botão **Salvar**, que exige a política de escrita da conversa e confirmação; o editor preserva codificação e quebras de linha e recusa sobrescrever uma alteração externa. **Anotar seleção** acrescenta ao rascunho o caminho, a linha, o trecho e a solicitação do usuário, sem enviar ou alterar o arquivo automaticamente.

## Finalizado Quando

O usuário consegue editar um arquivo suportado, selecionar um trecho, adicionar uma anotação e enviar esse contexto ao agente; o salvamento respeita a política de permissões.
