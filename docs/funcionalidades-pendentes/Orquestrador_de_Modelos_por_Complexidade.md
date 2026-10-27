# Orquestrador de Modelos por Complexidade do Prompt

## Objetivo

Economizar o uso total de tokens ao direcionar cada solicitação para um agente ou subagente configurado com um modelo adequado à complexidade do prompt, entre Sol, Terra e Luna, preservando o atendimento aos critérios de aceite e a precisão da execução.

## Contexto

Funcionalidade planejada para implementação futura. A cada solicitação, um modelo preparado para avaliação (evaluation) deverá analisar a complexidade do prompt e encaminhar a tarefa ao agente ou subagente configurado para esse nível. O mapeamento dos níveis para Sol, Terra e Luna será definido na implementação e validado pelos resultados dos testes.

Após a implementação, a taxa de sucesso será verificada por testes das tarefas executadas. A avaliação deverá comparar o cumprimento dos critérios de aceite, a precisão da execução, a diferença de tokens gastos e a velocidade de execução, registrando também o total bruto de tokens e o tempo de implementação de cada tarefa avaliada.

## Restrições

- Este card registra uma funcionalidade futura; sua inclusão não ativa o orquestrador nem altera a seleção atual de modelos.
- O roteamento deve respeitar os modelos e agentes configurados e disponíveis, além dos limites de contexto, sandbox e aprovações da solicitação.
- Os testes, os critérios de aceite, os mínimos de sucesso, os pesos numéricos e a normalização das métricas deverão estar definidos antes de comparar os resultados. Não fixar percentuais ou limites sem essa definição.
- A ponderação deve obedecer à ordem: **critérios de aceite e precisão da execução com peso maior; diferença de tokens gastos com peso intermediário; velocidade de execução com peso menor**. Economia ou rapidez não compensam o descumprimento dos critérios mínimos de aceite.
- A diferença de tokens e de tempo deve ser calculada contra uma execução de referência definida, usando as mesmas tarefas, critérios e condições comparáveis. Registrar valores absolutos e relativos, sem presumir que houve economia.
- O total bruto deve contabilizar os tokens de toda a solicitação, incluindo o modelo avaliador, o agente, eventuais subagentes e reexecuções. O tempo total da tarefa deve incluir avaliação, encaminhamento, implementação e validação, com o tempo de implementação identificado separadamente.

## Finalizado Quando

- Cada solicitação tem sua complexidade avaliada e é encaminhada ao agente ou subagente configurado com Sol, Terra ou Luna, com registro do nível identificado e do destino escolhido.
- Os testes executados após a implementação das tarefas permitem verificar os critérios de aceite e a precisão, calcular a taxa de sucesso como tarefas aprovadas sobre tarefas avaliadas e apresentar os resultados por nível de complexidade e modelo.
- A comparação com a execução de referência apresenta, por tarefa e no conjunto avaliado, o total bruto de tokens, a diferença absoluta e percentual de tokens, o tempo de implementação, o tempo total e a diferença de velocidade de execução.
- O resultado ponderado segue os pesos definidos, mantendo critérios de aceite acima de economia de tokens e economia de tokens acima de velocidade.
- Há evidência reproduzível de que o roteamento atende aos mínimos de sucesso e à redução de tokens definidos para a avaliação, com falhas e limitações registradas.
