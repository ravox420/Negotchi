# BMO para Negotchi

Pacote v2 local: onze estados obrigatorios e `dancing`, com oito desenhos por
estado. Atlas RGBA de 1536 x 2496, celulas de 192 x 208, 96 quadros no total.
`dancing` ocupa a ultima linha, indices 88 a 95, a 6 fps em loop e fallback `idle`.

Abra `preview.html` para ver, pausar e avancar os quadros. `animations/dancing.gif`
e a previa da danca; os GIFs usam fundo cinza somente para visualizacao.

Para importar, use `/import-pet` no Negotchi e selecione o `pet.json` desta pasta.
A ferramenta de importacao do aplicativo nao estava disponivel na sessao de
criacao; o pacote nao foi instalado nem ativado automaticamente.

O Negotchi aciona `dancing` automaticamente enquanto o aplicativo Spotify esta
reproduzindo. Pausar ou fechar a sessao restaura o repouso. Trabalho do agente,
arraste e reacoes pontuais tem prioridade sobre a danca. Recompile e reinicie
o aplicativo para carregar a implementacao desse monitor.

Geracao feita com a ferramenta integrada `image_gen`. O conjunto final de
prompts esta em `generation.json`; as faixas originais e normalizadas, parametros
da grade e script de montagem ficam em `../../pet-work/bmo/`.
O script usa o helper original da skill sem alterar seus arquivos e acrescenta
`dancing` a lista de faixas durante a montagem. Para reconstruir, use Python com
Pillow e `python pet-work/bmo/build.py --output NOVA_PASTA` a partir do projeto.

`validation.json` registra a verificacao tecnica. `visual-review.json` registra
a inspecao visual separadamente, incluindo os limites da avaliacao.
