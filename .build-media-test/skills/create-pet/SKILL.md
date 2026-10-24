---
name: create-pet
description: Cria pets animados para o aplicativo desktop Negotchi a partir de uma descricao ou imagem de referencia, com onze estados, spritesheet transparente, manifesto, previas e validacao. Use para criar ou reparar pacotes desse aplicativo.
---

Crie um personagem consistente e animacoes reais para o Negotchi. Preserve o estilo e os detalhes assimetricos da referencia. O pacote e local; nao use o ciclo de upload/ativacao de Pets do ChatGPT Work.

Leia [references/animation-contract.md](references/animation-contract.md) para os estados, os movimentos e o formato. O minimo e: idle, thinking, running-right, running-left, waving, jumping, failed, waiting, running, review e responding. `move-right` e `move_right` sao aliases de running-right; inclua os equivalentes para a esquerda. Aliases nao substituem animacoes obrigatorias.

Use o gerador de imagens disponivel na sessao para produzir uma faixa horizontal com oito quadros por estado, fundo transparente, grade regular, mesmo tamanho de celula, escala e ponto de apoio. Use uma referencia visual compartilhada entre as faixas. Inspecione a referencia antes de edita-la. Gere quadros com poses diferentes e transicoes coerentes; nao transforme uma imagem parada em falsa animacao por translacao, espelhamento, zoom ou repeticao. Nao espelhe caracteristicas assimetricas para criar a direcao oposta.

Verifique a disponibilidade real de geracao de imagens antes de prometer o resultado. A ferramenta existente em outro cliente nao e automaticamente acessivel ao Codex App Server. Se esta sessao nao tiver gerador utilizavel, explique a limitacao, preserve a especificacao do personagem e indique quais faixas ainda precisam ser produzidas. Nao anuncie um pet pronto nem instale um pacote incompleto. Nao introduza chaves ou provedores externos sem um pedido do usuario.

Salve as onze faixas como `<estado>.png` em uma pasta de trabalho separada. Para montar e validar o pacote, execute o helper desta skill (os caminhos abaixo sao relativos a pasta da skill):

```powershell
uv run --with pillow python scripts/pet_pack.py pack --input CAMINHO_DAS_FAIXAS --output CAMINHO_DO_PACOTE --id meu-pet --name "Meu pet" --description "Descricao do personagem"
uv run --with pillow python scripts/pet_pack.py validate CAMINHO_DO_PACOTE
```

Se Python ja tiver Pillow, pode usar `python` diretamente. O helper nao gera a arte: recorta a grade, ajusta cada celula inteira para 192 x 208 sem deformar, monta o atlas, produz aliases, manifesto, miniatura, GIFs por estado e um relatorio tecnico. Nao recorte cada pose pelo proprio contorno, pois isso desloca a ancora e muda a escala entre quadros. Ele recusa destino existente; para reparar, trabalhe em outra pasta e preserve o original.

Inspecione `contact-sheet.png` e cada GIF em `animations/`: identidade, anatomia, recorte, apoio, suavidade, fechamento dos ciclos e diferenciacao entre pensar, esperar, executar, revisar e responder. A verificacao automatica encontra quadros vazios/repetidos, opacidade e bordas cortadas; ela nao comprova qualidade artistica nem significado do movimento. Corrija as faixas com o gerador e repita a montagem quando necessario. Informe separadamente validacao tecnica e inspecao visual.

No aplicativo, quando a ferramenta `negotchi_import_pet` estiver disponivel e o pedido autorizar criar o pet, chame-a com a pasta absoluta do pacote aprovado. Ela valida e registra na aba Pets sem recompilar. Somente declare a importacao depois do retorno de sucesso. Se a ferramenta nao estiver disponivel, entregue o caminho de `pet.json` para `/import-pet`; nao altere configuracoes globais nem substitua pets existentes.

A animacao durante a conversa e responsabilidade do controlador do Negotchi, que usa eventos do App Server. A skill fornece as faixas corretas. Nao instrua o agente a escrever marcadores de animacao nas respostas nem dependa de acesso a raciocinio privado.
