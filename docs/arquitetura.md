# Arquitetura do Negotchi

O Negotchi é um monólito WPF nativo: um processo, um executável e recursos de
pets incorporados. O código está organizado por responsabilidade dentro de
`desktop/src`; o script de build compila as subpastas em um único assembly.

| Pasta | Responsabilidade |
| --- | --- |
| `application` | Inicialização, ciclo de vida, coordenação das janelas e estado da aplicação. |
| `features/account` | Apresentação de conta e uso. |
| `features/appearance` | Edição das preferências visuais. |
| `features/chat` | Interface da conversa, mensagens e anexos. |
| `features/commands` | Catálogo e interface dos comandos. |
| `features/conversations` | Histórico e seleção de conversas. |
| `features/pets` | Catálogo, pacotes, animações, janela e monitor do Spotify. |
| `features/git` | Interface de Git e revisão de diffs. |
| `features/workspace` | Ações locais, arquivos, edição e prévias. |
| `features/terminal` | Terminal, pseudoterminal e processos. |
| `features/integrations` | Gestão das integrações MCP. |
| `infrastructure/codex` | Processo do App Server e transporte JSON-RPC. |
| `infrastructure/persistence` | Leitura e gravação do estado local. |
| `shared` | Serialização JSON e recursos visuais compartilhados. |

`application` coordena os módulos e o estado persistido. Cada funcionalidade
deve manter suas regras e telas próximas, recorrendo à infraestrutura para
acesso externo e a `shared` apenas para recursos efetivamente comuns. Os testes
específicos ficam nas subpastas correspondentes de `desktop/tests`; `Tests.cs`
reúne o executor e os testes integrados ainda compartilhados.

Esta organização não impõe isolamento pelo compilador: os arquivos ainda usam o
namespace `Negotchi` e compartilham um assembly. `NegotchiApp` e `Tests` ainda
contêm partes grandes e referências entre funcionalidades. Ao ampliar um módulo,
prefira extrair responsabilidades coesas e definir contratos pequenos onde uma
dependência real os justificar; não dependa de acesso ao estado interno de uma
outra janela. A separação em projetos ou assemblies pode ser avaliada quando
houver necessidade concreta de isolamento, sem antecipá-la neste rearranjo.

## Arquivos gerados

`.build/` contém o executável usado pelos scripts de inicialização. `.artifacts/`
concentra saídas temporárias: `tests/` para uma pasta nova por execução da suíte,
`build/` para destinos de validação escolhidos manualmente, `restart/` para o
build de reinicialização e `legacy/` para diretórios históricos movidos da raiz.
Esses arquivos são locais e ignorados pelo Git. Os materiais de criação de pets
em `pets/creation` são fontes e pacotes de trabalho, não saídas de teste.

Ao modificar o build, preserve os nomes de recursos `Negotchi.pets.*` e a cópia
da skill `create-pet`: o catálogo e a criação de pets dependem desses contratos.
