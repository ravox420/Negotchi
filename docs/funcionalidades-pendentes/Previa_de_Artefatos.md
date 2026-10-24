# Prévia de Artefatos

## Objetivo

Permitir inspecionar arquivos gerados ou referenciados sem sair do Negotchi.

## Contexto

O Negotchi informa limitações para PDF, Word e planilhas e delega abertura ao Windows. O Codex Desktop possui prévias de arquivos ricos.

## Restrição

A prévia é somente leitura e deve informar quando o formato não for suportado. Não substitui editor de documentos nem envia arquivos automaticamente.

## Estado atual — concluído

O explorador abre uma prévia local e somente leitura de textos, imagens, PDFs, DOCX, XLSX e PPTX. Documentos Office recebem uma representação textual explícita, sem reproduzir layout, fórmulas, gráficos ou animações. Arquivos acima dos limites de leitura, corrompidos ou de formatos sem suporte informam o motivo e mantêm disponível a abertura no aplicativo padrão do Windows.

## Finalizado Quando

O usuário consegue abrir, no aplicativo, prévias de Markdown, imagens, PDFs, documentos, planilhas e apresentações suportados, identificando claramente arquivos sem suporte.
