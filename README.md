# Negotchi

Companheiro de desktop para Windows, com o PimpDogg original e oito pets importados do Codex. O personagem fica sobre a área de trabalho, pode ser arrastado e abre uma conversa ao clicar nele.

A interface é nativa, em C# e WPF. O Codex App Server roda em segundo plano para responder perguntas e trabalhar com arquivos. O aplicativo reutiliza o login e o modelo configurado no Codex instalado, sem armazenar senhas ou chaves no projeto.

## Abrir

1. Dê duplo clique em **`Iniciar-Negotchi.vbs`**. Na primeira execução ele compila o aplicativo, sem abrir um terminal.
2. Arraste o personagem para onde quiser. Clique nele para abrir o chat.
3. Clique em **Conectar conta**, conclua o login no navegador e volte ao chat.
4. Escolha a **Pasta de trabalho** e faça seu pedido.
5. Abra a aba **Pets** e clique em um personagem para trocar.

Se o aplicativo já estiver compilado, também é possível abrir diretamente **`.build\Negotchi.exe`**. O executável contém as imagens e o manifesto do pet; não depende de Python nem de FFmpeg para rodar.

### Requisitos

- Windows 10 ou 11 com .NET Framework 4.8.
- Codex CLI instalado para usar a IA. A integração foi validada com **0.164.0**.
- Conta autenticada e conexão com a internet para conversar. O pet e a abertura manual de arquivos funcionam sem login.

O aplicativo procura o `codex.exe` no PATH e na instalação npm padrão do usuário. Para outra instalação, defina `NEGOTCHI_CODEX_PATH` com o caminho completo do executável nativo, não com um arquivo `.ps1` ou `.cmd`.

## Controles

| Ação | Como usar |
| --- | --- |
| Mover o personagem | Arrastar com o botão esquerdo |
| Conversar | Clicar no personagem ou no balão |
| Opções do pet | Botão direito |
| Trocar personagem | Aba **Pets** no chat; também disponível em **Trocar pet...** no menu ou ícone perto do relógio |
| Alterar tamanho | Roda do mouse sobre o personagem ou menu |
| Manter sobre outras janelas | Opção **Sempre visível** no menu |
| Enviar pergunta | Enter; Shift+Enter quebra a linha |
| Comandos do Codex | Botão **Comandos /** ou digitar `/` no campo de mensagem |
| Interromper a resposta | Botão **Parar** |
| Abrir documento manualmente | **Abrir arquivo**, mesmo sem login |
| Referenciar um arquivo na pergunta | **Anexar caminho**; adiciona uma referência local, sem fazer upload automático |
| Trocar contexto | **Pasta de trabalho**; inicia uma nova conversa nessa pasta |
| Recuperar o pet fora da tela | Ícone perto do relógio → **Trazer pet para a tela** |
| Encerrar | Menu do personagem ou ícone perto do relógio → **Sair** |

Fechar o chat mantém o personagem funcionando. O pet alterna animações de repouso e pequenas ações enquanto fica aberto; durante os pedidos, mostra estados de trabalho e espera.

## Seleção de pets

A aba **Pets** oferece prévias de **PimpDogg, Codex, Dewey, Fireball, Rocky, Seedy, Stacky, BSOD e Null Signal**. Clique no cartão para aplicar a troca imediatamente. O personagem da área de trabalho, o retrato, o nome no chat e o ícone perto do relógio acompanham a escolha.

A seleção é salva e restaurada ao reabrir. Trocar de pet mantém a pasta e a conversa; mensagens anteriores preservam o nome do personagem que respondeu. Durante um pedido, conclua ou interrompa a resposta antes de trocar.

Os oito pets importados ficam incorporados no executável e funcionam sem baixar imagens durante o uso. As miniaturas são carregadas separadamente; a imagem completa de animações é carregada quando um pet é selecionado.

## Comandos do Codex na interface

Clique em **Comandos /** ao lado do campo de mensagem ou comece a digitar `/`. As sugestões filtram por nome; use as setas para navegar, Enter para executar, Tab para completar e Esc para fechar. Abrir o seletor pelo botão preserva o rascunho que você estava escrevendo.

| Comando | Comportamento |
| --- | --- |
| `/model` | Consulta o catálogo do Codex e permite escolher modelo e esforço de raciocínio. `/reasoning` abre o mesmo controle. |
| `/model identificador [esforço]` | Aplica uma escolha que conste no catálogo disponível. |
| `/usage` ou `/status` | Mostra modelo, conversa, tokens reportados, limites e atividade da conta. |
| `/skills [busca]` | Lista skills da pasta atual e permite anexar uma ao próximo pedido. |
| `/memories` | Abre as opções globais de memória do Codex e a permissão desta conversa para contribuir com memórias. |
| `/mcp` | Consulta os servidores MCP e as ferramentas informadas pelo Codex. |
| `/new` | Inicia uma nova conversa na pasta atual. |
| `/login` | Abre o fluxo de conexão da conta. |
| `/pets` | Abre a aba de seleção de personagens. |
| `/help` | Lista os comandos disponíveis nesta versão. |

Os controles usam as operações correspondentes do [Codex App Server](https://learn.chatgpt.com/docs/app-server). `/usage` é o atalho desta interface para as consultas de uso e limites; `/pets` controla o próprio Negotchi. Um comando indisponível recebe uma mensagem local com orientação para `/help`.

Modelo e esforço escolhidos ficam salvos no Negotchi e são aplicados aos próximos pedidos, inclusive após reabrir a conversa. O seletor usa os modelos e esforços que o Codex retorna, respeitando a configuração do provedor.

Ao escolher uma skill, a interface mostra uma identificação acima da mensagem e inclui a referência nativa da skill no próximo pedido. É possível removê-la antes de enviar. Skills desativadas aparecem identificadas e não podem ser selecionadas.

As opções globais de `/memories` são salvas pelo Codex em sua própria configuração e também afetam outros clientes que usam essa configuração. A permissão de contribuição é aplicada à conversa atual. A marcação intermediária mantém a opção atual; memórias já incluídas no histórico continuam nesse histórico.

Uso e limites dependem da autenticação e dos dados retornados pelo serviço. A interface identifica métricas ausentes e falhas de consulta; tokens da conversa aparecem quando o Codex emite a atualização correspondente.

Exemplos de pedidos:

- “Explique este projeto.”
- “Encontre minhas anotações nesta pasta e abra o arquivo.”
- “Abra `C:\Users\MeuUsuario\Documents\relatorio.pdf`.”
- “Leia este arquivo e resuma os pontos principais.”

A abertura usa o aplicativo padrão do Windows. Ler e resumir depende das ferramentas e dos formatos disponíveis no Codex; o Negotchi não inclui um leitor universal de PDF, Word ou planilhas.

## Arquivos e permissões

O agente começa em modo de leitura. Solicitações de executar comandos fora desse modo, alterar arquivos ou obter permissões são apresentadas na interface com os detalhes disponíveis e as opções de autorizar uma vez ou recusar.

A ferramenta `negotchi_open_path` recebe um caminho local existente e o abre no Windows sem montar um comando de shell. Documentos comuns e pastas abrem diretamente. Programas, scripts e extensões desconhecidas exigem confirmação. URLs, caminhos de rede e fluxos alternativos NTFS são recusados por essa ferramenta.

Posição, tamanho, pet escolhido, modelo, esforço, permissão de memória da conversa, tokens reportados, pasta, identificador da conversa e mensagens ficam em **`%LOCALAPPDATA%\Negotchi\state.json`**. O Codex mantém a autenticação, as preferências globais de memória e o histórico dele no local configurado pela própria instalação. As perguntas e os arquivos que o agente efetivamente lê podem ser enviados ao provedor configurado para responder ao pedido.

## Desenvolvimento

Compilar:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\desktop\build.ps1
```

Compilar e executar os testes:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\desktop\build.ps1 -Test
```

O build usa o compilador do .NET Framework que acompanha o Windows, sem restaurar pacotes. Após alterar o código, execute o build novamente antes de abrir o aplicativo. A pasta `.build` é gerada e fica fora do Git.

| Arquivo | Responsabilidade |
| --- | --- |
| `desktop/src/App.cs` | Conversa, autenticação, ações e ciclo de vida |
| `desktop/src/AppCommands.cs` | Execução dos comandos e aplicação das escolhas na conversa |
| `desktop/src/CodexCommands.cs` | Consultas, configurações e dados dos comandos nativos |
| `desktop/src/CommandMenu.cs` | Seletor de comandos e sugestões no campo de mensagem |
| `desktop/src/CommandDialogs.cs` | Janelas de modelo, skills, uso e memórias |
| `desktop/src/PetWindow.cs` | Janela transparente, animações, arraste e menu |
| `desktop/src/ChatWindow.cs` | Interface nativa do chat |
| `desktop/src/PetPicker.cs` | Aba de seleção, prévias e indicação do pet ativo |
| `desktop/src/CodexClient.cs` | Processo oculto e protocolo JSON-RPC bidirecional |
| `desktop/src/DesktopActions.cs` | Resolução de caminhos, abertura e revisão de ações |
| `desktop/src/Core.cs` | Recursos do pet, persistência e componentes visuais |
| `desktop/tests/Tests.cs` | Testes de arquivos, estado, protocolo e renderização |

Os testes cobrem o catálogo, a escolha de pet, a ausência de quadros vazios nas animações, o roteamento dos comandos, o protocolo com um servidor simulado e operações reais do `codex.exe` em um `CODEX_HOME` isolado. As alterações de configuração realizadas pelos testes ficam nesse diretório isolado. Também geram `chat-preview.png`, `pet-preview.png` e `pets-preview.png` para inspeção da interface. Isso não substitui um teste de conversa e de limites com uma conta autenticada.

Para verificar o ciclo de vida com janelas reais, execute `.\.build\Negotchi.Tests.exe --ui-smoke .\.build`. Esse teste abre o pet, o chat e as janelas dos comandos, verifica a persistência e o ícone de bandeja e encerra. Usa preferências isoladas em `.build\ui-smoke-home` e um servidor simulado para as ações de conta e configuração das janelas.

A integração segue o [Codex App Server oficial](https://learn.chatgpt.com/docs/app-server). As ferramentas dinâmicas são experimentais; mudanças no protocolo podem exigir ajustes no cliente.

## Recursos originais

`PimpDogg/package-transparent` é a origem dos recursos incorporados no aplicativo. As outras variantes, o conversor Python e o instalador do pet para o Codex continuam na pasta `PimpDogg`.

`desktop/pets` contém os oito pets padrão importados do cache local do Codex: PNGs com transparência, miniaturas e manifestos. As células de cada estado preservam o espaçamento original da grade de 8 × 9; o PimpDogg mantém seu próprio manifesto. A cadência usada pelo Negotchi está definida nos manifestos importados.

Para refazer a importação a partir de uma instalação local que tenha esses oito arquivos em cache:

```powershell
python .\desktop\import_codex_pets.py
```

Esse comando requer FFmpeg no PATH somente na importação. Não altera os arquivos da instalação do Codex. `desktop/pets/import-report.json` registra os nomes e os hashes das imagens de origem e das cópias convertidas.
