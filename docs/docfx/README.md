# NatureProtector, documentação com DocFX

Este diretório contém a configuração e os ficheiros do site de documentação DocFX do repositório **NatureProtector**.

O objetivo deste site é concentrar, num único ponto de entrada, a documentação conceptual, arquitetural e operacional do projeto, juntamente com a referência gerada da API .NET. O DocFX não substitui o Doxygen; os dois complementam-se. O DocFX funciona como portal principal de leitura e navegação, enquanto o Doxygen serve melhor a exploração técnica ao nível do código, grafos, relações entre classes, chamadas e páginas manuais focadas nos fluxos de implementação.

## 1. O que é o DocFX neste repositório

Neste projeto, o DocFX é usado como **site principal de documentação**. A sua função é:

- reunir documentação escrita em Markdown;
- publicar documentação técnica e arquitetural num formato navegável;
- integrar referência gerada a partir dos projetos `.csproj` e da documentação XML do código;
- preparar o repositório para, no futuro, incluir também uma secção de API HTTP baseada em OpenAPI.

Em termos práticos, isto permite ter um ponto de consulta mais orientado à leitura humana, à navegação entre temas e à organização do conhecimento do projeto.

## 2. O que entra no site

O site DocFX pode agregar vários níveis de documentação:

### 2.1. Documentação conceptual e arquitetural
Vem principalmente de:

- `docs/architecture/`
- `docs/planning/`
- `README.md` do repositório
- `data/README.md`
- `scripts/data/README.md`

### 2.2. Referência gerada da API .NET
É construída a partir dos projetos `src/**/*.csproj`, assumindo que o repositório é compilado localmente com documentação XML ativa.

### 2.3. Documentação complementar gerada por Doxygen
O Doxygen continua a ser útil para:

- grafos de classes;
- caller graphs e call graphs;
- relações mais próximas da implementação;
- páginas manuais sobre fluxos de runtime e componentes.

O DocFX deve apontar para esta documentação como complemento, não como duplicado.

## 3. O que o DocFX faz bem e o que não faz

### Faz bem
- organizar páginas Markdown;
- criar navegação, índice e estrutura de leitura;
- agregar documentação escrita e referência gerada;
- servir como site principal de documentação do repositório;
- funcionar bem como ponto de entrada para quem chega ao projeto pela primeira vez.

### Não faz tão bem, sozinho
- mostrar grafos de runtime e relações detalhadas de implementação como o Doxygen;
- substituir documentação arquitetural viva se os ficheiros fonte estiverem incompletos;
- produzir automaticamente documentação HTTP útil sem uma exportação OpenAPI estável.

## 4. Estado atual

O estado atual esperado desta configuração é o seguinte:

- o site DocFX serve como portal principal da documentação;
- a documentação escrita vem de Markdown distribuído pelo repositório;
- a referência .NET é gerada a partir dos projetos do código;
- o `Backoffice.Api` já está estruturalmente preparado para uma futura integração OpenAPI;
- essa integração futura ainda não é obrigatória nem está fechada como parte fixa do processo de geração.

Ou seja, o repositório já está preparado para evoluir para uma documentação HTTP mais completa, mas a prioridade atual é ter primeiro uma base sólida, navegável e estável para a documentação técnica geral.

## 5. Estrutura típica deste diretório

Uma estrutura típica em `docs/docfx/` é:

```text
docs/docfx/
  docfx.json
  index.md
  toc.yml
  openapi-future.md
  api/
  artifacts/
  output/
````

### Significado das pastas e ficheiros

* `docfx.json`: configuração principal do site DocFX.
* `index.md`: página de entrada do site.
* `toc.yml`: navegação principal.
* `openapi-future.md`: nota de planeamento sobre futura integração OpenAPI.
* `api/`: entrada gerada ou preparada para referência de API.
* `artifacts/`: artefactos intermédios de geração.
* `output/`: site gerado para visualização local ou publicação.

Regra prática: **não editar manualmente o conteúdo gerado em `output/`**. O que deve ser editado é a fonte, isto é, Markdown, configuração e eventualmente os inputs gerados de forma controlada.

## 6. Relação entre DocFX, Doxygen e OpenAPI

### DocFX

É o portal principal de documentação.

### Doxygen

É o complemento técnico para inspeção detalhada da implementação.

### OpenAPI

É uma integração futura desejável para a API HTTP, mas ainda não padronizada no processo de build da documentação.

A intenção correta é esta:

* **DocFX** para navegação e leitura;
* **Doxygen** para profundidade técnica e grafos;
* **OpenAPI** como futura superfície dedicada da API HTTP, quando a exportação estiver estabilizada.

## 7. Requisitos para uso local

Os comandos abaixo assumem que tens:

* .NET SDK instalado;
* o repositório numa cópia funcional;
* o executável `docfx` disponível no `PATH`, ou outra forma equivalente de o invocar no teu ambiente.

Também é recomendado que o repositório compile antes da geração da documentação, para evitar referência .NET incompleta.

## 8. Como limpar os artefactos gerados

Antes de regenerar, pode ser útil limpar a saída anterior. Em PowerShell:

```powershell
Remove-Item .\docs\docfx\output -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item .\docs\docfx\api -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item .\docs\docfx\artifacts -Recurse -Force -ErrorAction SilentlyContinue
```

Se o repositório usar `.gitkeep` ou outros placeholders rastreados pelo Git, confirma depois se precisam de ser repostos.

A limpeza é útil quando queres:

* confirmar que não estás a ver ficheiros antigos;
* garantir que a geração atual é reprodutível;
* diagnosticar se algum output veio de execuções anteriores.

## 9. Como gerar o site

Fluxo recomendado:

### 9.1. Compilar primeiro a solução

```powershell
dotnet build .\NatureProtector.sln
```

### 9.2. Gerar o site DocFX

```powershell
docfx .\docs\docfx\docfx.json
```

Isto deverá regenerar a saída do site em `docs/docfx/output/`, conforme a configuração atual.

## 10. Como abrir localmente

Para servir localmente e navegar no site no browser:

```powershell
docfx .\docs\docfx\docfx.json --serve
```

Ou, em ambientes em que o processo esteja separado:

```powershell
docfx build .\docs\docfx\docfx.json
docfx serve .\docs\docfx\output
```

Depois abre o endereço local indicado pelo DocFX no terminal.

## 11. Fluxo recomendado de trabalho

Quando fores mexer nesta documentação, a sequência mais segura é:

1. editar Markdown e configuração;
2. compilar a solução;
3. limpar artefactos antigos, se necessário;
4. regenerar o site;
5. abrir localmente;
6. verificar navegação, links, páginas e secções geradas.

Isto evita muitos falsos positivos causados por output antigo ou por referência gerada desatualizada.

## 12. O que verificar após a geração

Depois de gerar, convém verificar pelo menos:

* se a página inicial abre corretamente;
* se a navegação principal está coerente;
* se as páginas Markdown aparecem com os títulos certos;
* se a referência da API .NET foi gerada;
* se os links para documentação complementar fazem sentido;
* se não há secções vazias, duplicadas ou órfãs;
* se a linguagem, terminologia e nomes estão consistentes.

## 13. Como manter a documentação organizada

### Editar sempre a fonte

Nunca usar `output/` como origem de verdade.

### Evitar duplicação desnecessária

Se algo já está bem explicado no Doxygen ou noutro documento, o DocFX deve apontar para isso ou resumir, não copiar cegamente.

### Separar estável de provisório

* documentação consolidada deve ficar nas páginas principais;
* notas de evolução, planeamento ou integração futura devem estar claramente marcadas como tal.

### Manter a navegação útil

O DocFX vale pela legibilidade e pela organização. Se a estrutura crescer sem controlo, perde-se a principal vantagem da ferramenta.

## 14. Integração futura com OpenAPI

O `Backoffice.Api` já tem os pontos base necessários para uma futura integração OpenAPI. Isso significa que o repositório está estruturalmente preparado para, mais tarde, acrescentar ao site DocFX uma secção dedicada à API HTTP.

Essa integração ainda não foi forçada neste momento por três razões principais:

1. a prioridade atual é consolidar o site DocFX base;
2. o caminho de exportação e o artefacto gerado ainda não estão padronizados;
3. a documentação técnica existente já é coberta, por agora, pela combinação de DocFX e Doxygen.

Quando essa integração for estabilizada, o caminho esperado é:

1. gerar um artefacto OpenAPI determinístico durante o build local ou CI;
2. colocar esse artefacto em `docs/docfx/` como input gerado;
3. acrescentar essa superfície HTTP à navegação do site.

Enquanto isso não estiver fechado, esta nota deve ser tratada como orientação de evolução, não como funcionalidade obrigatória.

## 15. Limitações e cuidados

### O DocFX não substitui a arquitetura

Se os documentos de arquitetura estiverem incompletos, o site ficará organizado mas continuará incompleto em conteúdo.

### A referência gerada depende do build

Se a solução não compilar, a documentação gerada da API pode ficar incompleta ou falhar.

### OpenAPI ainda não é a fonte principal

Mesmo com suporte estrutural no `Backoffice.Api`, a API HTTP ainda não deve ser tratada como parte fechada do pipeline documental sem uma estratégia de exportação estável.

### Doxygen continua relevante

Para fluxos de runtime, grafos e inspeção técnica detalhada, o Doxygen continua a ser parte importante do ecossistema documental do projeto.

## 16. Leitura recomendada dentro do projeto

Uma ordem de leitura útil para alguém novo no repositório é:

1. `README.md` da raiz do projeto;
2. `docs/architecture/README.md`;
3. documentos de arquitetura e estado atual;
4. documentos de planeamento, para perceber sequência e lacunas;
5. referência gerada da API, quando for preciso detalhe de tipos e membros;
6. Doxygen, quando for preciso explorar relações de implementação, grafos e fluxos internos.

## 17. Resumo prático

Em termos simples:

* o **DocFX** é o site principal de documentação;
* o **Doxygen** é o complemento técnico de exploração do código;
* a **integração OpenAPI** é futura e preparada, mas ainda não obrigatória;
* o que deve ser editado são os ficheiros fonte, não o output;
* antes de confiar no resultado, convém limpar, gerar e verificar localmente.

## 18. Comandos úteis

### Limpar

```powershell
Remove-Item .\docs\docfx\output -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item .\docs\docfx\api -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item .\docs\docfx\artifacts -Recurse -Force -ErrorAction SilentlyContinue
```

### Compilar

```powershell
dotnet build .\NatureProtector.sln
```

### Gerar

```powershell
docfx .\docs\docfx\docfx.json
```

### Servir localmente

```powershell
docfx .\docs\docfx\docfx.json --serve
```

## 19. Nota final

Este diretório deve ser tratado como a base do portal documental do NatureProtector. A utilidade do DocFX não depende apenas da ferramenta, depende sobretudo da disciplina com que a documentação é mantida, gerada e revista.
