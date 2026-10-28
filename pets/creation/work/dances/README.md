# Faixas de dança dos pets incorporados

As faixas foram geradas com a ferramenta integrada de geração de imagens, usando
as prévias dos personagens como referência. O pedido comum foi: oito poses de
dança em uma linha, grade regular, fundo transparente, mesma escala e apoio,
preservando aparência e detalhes assimétricos do personagem. Para Stacky foi
solicitado manter as cores das antenas em seus lados; para Guts, manter a espada,
o olho fechado e o braço metálico em todos os quadros.

Fontes incorporadas: `codex.png`, `dewey.png`, `fireball.png`, `rocky.png`,
`seedy-v2.png`, `stacky-v2.png`, `bsod.png`, `null-signal.png` e `Guts-v3.png`
em `raw/`. As demais são tentativas que não passaram na revisão visual ou de
grade. Em particular, PimpDogg ainda apresenta resíduos ao redor do personagem.

`../../assemble_dance.py` monta uma cópia em destino novo, aplicando uma única
escala à faixa e mantendo os quadros antigos. É necessário Pillow; confira a
grade e a aparência antes de substituir arquivos incorporados e recompile.
