# Structurizr DSL

Esta pasta contém o modelo C4 em Structurizr DSL para a implementação atual do NatureProtector. O objetivo é manter uma vista arquitetural pequena, versionada, validável e exportável a partir do repositório, sem substituir os diagramas Draw.io usados na narrativa mais longa em `docs/architecture/`.

O modelo aqui presente descreve a baseline atual da solução. Não pretende antecipar a arquitetura final nem servir como único artefacto de documentação arquitetural.

## Objetivo

O workspace Structurizr existe para dar ao repositório uma representação arquitetural:

- próxima do código e dos componentes reais;
- validável automaticamente;
- exportável para outros formatos, como PlantUML;
- útil para inspeção rápida da runtime atual.

Este modelo deve evoluir com a implementação real. Quando houver divergência entre o modelo e o código, o código prevalece.

## Ficheiros

- `workspace.dsl`: workspace Structurizr DSL com o modelo C4 atual.
- `output/`: destino local para exports gerados, por exemplo PlantUML.
- `README.md`: guia de utilização, limpeza, validação e exportação do modelo.

## O que é fonte e o que é gerado

Nesta pasta, os ficheiros de fonte são sobretudo:

- `workspace.dsl`
- `README.md`

São artefactos gerados localmente e podem ser removidos e recriados:

- `output/`
- `.structurizr/`
- `workspace.json` (se for criado em execução local)

## Vistas modeladas

O workspace define apenas vistas alinhadas com elementos reais do repositório:

- `contexto-sistema`: pessoas e sistema NatureProtector.
- `runtime-atual`: containers principais da runtime e da baseline local.
- `componentes-prevention-host`: componentes centrais do `NatureProtector.Prevention.Host`.
- `baseline-local-docker-compose`: deployment local com hosts executados no posto de desenvolvimento e infraestrutura em Docker Compose.

## Pré-requisitos e ferramenta recomendada

A forma recomendada de trabalhar este modelo é através de Docker com a imagem `structurizr/structurizr`.

Isto evita depender de uma instalação local da CLI Structurizr, que pode não existir na máquina. Também reduz diferenças de ambiente entre utilizadores.

A imagem `structurizr/lite` não é o caminho recomendado neste projeto, porque está descontinuada. Deve ser usada a imagem `structurizr/structurizr`.

## Como validar

O caminho recomendado em Windows PowerShell é usar Docker:

```powershell
docker run --rm -v "$($PWD.Path):/usr/local/structurizr" structurizr/structurizr validate -workspace docs/structurizr/workspace.dsl
````

Se a CLI `structurizr` estiver instalada localmente, o comando equivalente é:

```powershell
structurizr validate -workspace docs/structurizr/workspace.dsl
```

## Como exportar PlantUML

```powershell
docker run --rm -v "$($PWD.Path):/usr/local/structurizr" structurizr/structurizr export -workspace docs/structurizr/workspace.dsl -format plantuml -output docs/structurizr/output
```

Com CLI local:

```powershell
structurizr export -workspace docs/structurizr/workspace.dsl -format plantuml -output docs/structurizr/output
```

## Como abrir localmente

Para explorar o workspace localmente:

```powershell
docker run --rm -it -p 8181:8080 -v "$($PWD.Path)\docs\structurizr:/usr/local/structurizr" structurizr/structurizr local
```

Depois abrir:

```text
http://localhost:8181
```

## Fluxo de trabalho recomendado

Sempre que alterares o modelo:

1. editar `workspace.dsl`;
2. validar o DSL;
3. exportar para PlantUML;
4. abrir localmente para inspeção visual, quando necessário;
5. limpar artefactos locais que não devam ficar no repositório.

## Limpeza de artefactos locais

Para limpar os artefactos gerados localmente e voltar a gerar tudo de raiz:

```powershell
Remove-Item .\docs\structurizr\output\* -Force -ErrorAction SilentlyContinue
Remove-Item .\docs\structurizr\.structurizr -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item .\docs\structurizr\workspace.json -Force -ErrorAction SilentlyContinue
```

Se quiseres preservar o `.gitkeep`, podes recriá-lo no fim:

```powershell
New-Item .\docs\structurizr\output\.gitkeep -ItemType File -Force | Out-Null
```

## Ciclo completo de regeneração

Um ciclo típico de limpeza, validação e exportação pode ser feito assim:

```powershell
Remove-Item .\docs\structurizr\output\* -Force -ErrorAction SilentlyContinue
Remove-Item .\docs\structurizr\.structurizr -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item .\docs\structurizr\workspace.json -Force -ErrorAction SilentlyContinue
New-Item .\docs\structurizr\output\.gitkeep -ItemType File -Force | Out-Null

docker run --rm -v "$($PWD.Path):/usr/local/structurizr" structurizr/structurizr validate -workspace docs/structurizr/workspace.dsl

docker run --rm -v "$($PWD.Path):/usr/local/structurizr" structurizr/structurizr export -workspace docs/structurizr/workspace.dsl -format plantuml -output docs/structurizr/output
```

## O que esperar quando corre bem

Quando a validação corre bem com Docker, o comando pode terminar sem imprimir erros nem mensagens adicionais relevantes.

Quando o export PlantUML corre bem, devem ser gerados ficheiros `.puml` em `docs/structurizr/output/`, normalmente:

* um ficheiro por vista;
* um ficheiro adicional `-key` com a legenda dessa vista.

Ao abrir a interface local, pode acontecer que alguns thumbnails não apareçam imediatamente. Isso, por si só, não significa que o modelo esteja inválido.

A interface pode também apresentar inspeções ou recomendações adicionais. Essas observações ajudam a melhorar o modelo, mas não significam necessariamente que o DSL esteja incorreto ou inutilizável.

## Problemas comuns

### O comando `structurizr` não existe

Se aparecer um erro do tipo:

```text
The term 'structurizr' is not recognized
```

isso significa apenas que a CLI não está instalada localmente. Usa os comandos com Docker.

### O browser abre mas o modelo não aparece corretamente

Confirmar:

* que o `workspace.dsl` está dentro da pasta montada no contentor;
* que o comando Docker está a montar a pasta correta;
* que não houve erros de parsing no terminal.

### O export não gera ficheiros

Confirmar:

* que o `workspace.dsl` está válido;
* que o caminho de output está correto;
* que estás a usar `structurizr/structurizr`;
* que o mount Docker aponta para a raiz esperada.

### A porta já está em uso

Trocar a porta local. Exemplo:

```powershell
docker run --rm -it -p 8181:8080 -v "$($PWD.Path)\docs\structurizr:/usr/local/structurizr" structurizr/structurizr local
```

### Há erros de parsing no DSL

Quando isso acontecer, o terminal do contentor normalmente indica a linha e a instrução problemática. A correção deve ser feita no `workspace.dsl` antes de voltar a validar ou exportar.

## Relação com os restantes diagramas

Os diagramas Draw.io em `docs/architecture/diagrams/` continuam a ser a fonte dos diagramas narrativos e das figuras usadas nos documentos longos.

O workspace Structurizr acrescenta:

* uma baseline C4 pequena;
* uma representação validável do estado atual;
* uma vista mais próxima dos elementos reais da runtime.

Este modelo não substitui a documentação narrativa nem os diagramas explicativos de maior detalhe.

## Limitações do modelo atual

Este workspace descreve a implementação atual e não a arquitetura alvo completa.

O modelo foi mantido deliberadamente pequeno e focado nos elementos mais úteis para explicar:

* o contexto do sistema;
* os containers principais da runtime atual;
* os componentes centrais do `NatureProtector.Prevention.Host`;
* a baseline local de execução.

O modelo só deve incluir elementos reais do repositório e relações que continuem úteis para explicar a solução atual.
