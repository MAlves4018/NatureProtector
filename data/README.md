# Data Workspace

Este diretório é o espaço de trabalho dos dados do projeto.

Se o `src/` contém o código da aplicação, aqui vive a matéria-prima que alimenta:

- o simulador;
- a construção dos cenários;
- os atributos territoriais da área piloto;
- o histórico de fogo;
- e a referência meteorológica usada antes de existirem sensores reais.

## Como ler esta estrutura

Em vez de pensar nesta pasta como "uma coleção de ficheiros", devemos pensar nela como uma pequena cadeia de produção:

### `external/`

Aqui entram os dados brutos.

São ficheiros vindos de fontes oficiais ou públicas:

- DGT
- ICNF
- IPMA
- LNEG
- Open-Meteo
- PT-FireSprd

Estes ficheiros ainda não são o formato final do projeto.

### `baseline/`

Aqui ficam os artefactos curados.

Esta é a camada mais importante para o projeto porque representa:

- o que já foi transformado;
- o que já está em formato estável;
- o que o simulador e o resto do sistema devem consumir.

### `manifests/`

Aqui fica a memória administrativa da pipeline:

- o que existe;
- de onde veio;
- qual o estado;
- o que falta;
- e, agora, os cenários executáveis `A/B/C` já formalizados.

### `runtime/`

Aqui vão ficar saídas temporárias:

- simulações
- exports
- artefactos operacionais

Não deve ser confundido com `baseline`.

## Regra prática

Os dados em `external/` não são a fonte de verdade técnica.

A ordem certa de confiança é:

1. manifest do dataset
2. ficheiro curado em `baseline/`
3. mais tarde, o registo no plano de controlo em PostgreSQL

## Onde está a documentação detalhada

A explicação completa de setup, comandos, ordem dos passos e resultado esperado de cada etapa está em:

- [scripts/data/README.md](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/scripts/data/README.md)
- [docs/planing/pipeline-gap-and-dependency-map.md](C:/Users/Miguel/UNI/6sem/PS/IMP/A/NatureProtector/docs/planning/pipeline-gap-and-dependency-map.md)

Esse README conta a história completa da aquisição e curadoria de dados para `Proença-a-Nova`.

