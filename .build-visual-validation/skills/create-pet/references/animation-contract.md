# Contrato de animacao do Negotchi

O pacote possui `pet.json`, `spritesheet.png` RGBA e `preview.png`. Os extras `contact-sheet.png`, `animations/*.gif` e `validation.json` servem para revisao. O padrao e uma grade de 8 colunas x 11 linhas, celulas 192 x 208, atlas 1536 x 2288. O runtime aceita outras grades declaradas no manifesto; o helper usa este padrao para simplificar a criacao.

Cada faixa de entrada possui oito celulas iguais na horizontal. A dimensao pode ser maior que a de destino; o helper ajusta todas as celulas com o mesmo fator, mantendo a celula inteira e a transparencia. As onze faixas devem ter as mesmas dimensoes. Reserve margem transparente em cada celula e mantenha o apoio proximo de (96, 196) no destino. Saltos e passos podem afastar os pes desse ponto conforme a acao. Evite texto, legendas e marcadores de grade dentro da arte.

| Estado / linha | Movimento esperado | Reproducao |
| --- | --- | --- |
| idle / 0 | Respiracao discreta e piscada | Loop, 6 fps |
| running-right / 1 | Ciclo de corrida orientado a direita | Loop, 8 fps |
| running-left / 2 | Corrida orientada a esquerda preservando a anatomia | Loop, 8 fps |
| waving / 3 | Levantar a mao, acenar e baixar | Uma vez, 6 fps |
| jumping / 4 | Agachar, impulsionar, subir, descer, pousar | Uma vez, 8 fps |
| failed / 5 | Reacao clara de frustracao/falha e recuperacao | Uma vez, 6 fps |
| waiting / 6 | Espera atenta por resposta ou autorizacao | Loop, 6 fps |
| running / 7 | Trabalho ativo, distinto do deslocamento lateral | Loop, 8 fps |
| review / 8 | Examinar/inspecionar algo com atencao | Loop, 6 fps |
| thinking / 9 | Refletir: olhar, mao na cabeca/queixo, pausa deliberada | Loop, 6 fps |
| responding / 10 | Expressao e gestos de fala | Loop, 8 fps |

Aliases: `move-right`, `move_right` -> running-right; `move-left`, `move_left` -> running-left; `wave` -> waving; `bounce` -> jumping; `sad` -> failed. Reutilize os mesmos indices e propriedades. Todos os fallbacks sao `idle`.

O manifesto gerado tem `schemaVersion: 2`, `id` (ate 64 letras ASCII, numeros, hifen ou sublinhado; sem nomes reservados do Windows), `displayName`, `description`, `spritesheetPath: "spritesheet.png"`, `frame: {width, height, columns, rows}` e `animations`. Cada animacao declara `frames` (indices globais, linha * 8 + coluna), `fps`, `loop` e `fallback`.

O validador exige os onze estados, oito quadros por estado, pelo menos quatro desenhos diferentes por faixa, todos os quadros visiveis com margem transparente, nenhuma faixa canonica identica a outra e aliases consistentes. Isso e verificacao tecnica; uma faixa formada por deslocamentos artificiais ainda precisa ser recusada na inspecao visual.

Durante o uso, o controlador acompanha thread/turn/item. Processamento -> thinking; ferramentas -> running; leitura/inspecao ou review explicito -> review; streaming de resposta -> responding; perguntas/aprovacoes -> waiting; sucesso -> jumping e idle; falha -> failed; interrupcao -> idle. Arraste assume running-left/right temporariamente e restaura o estado atual ao soltar. Estados de pets antigos tem fallback thinking -> waiting e responding -> review.
