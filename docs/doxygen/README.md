# NatureProtector, documentação com Doxygen

Este diretório contém a configuração e os ficheiros de suporte da documentação **Doxygen** do repositório **NatureProtector**.

No contexto deste projeto, o Doxygen não é o site principal de documentação. Esse papel pertence ao DocFX. O Doxygen funciona como **companheiro técnico de implementação**, sendo especialmente útil para:

- navegar classes, namespaces, ficheiros e membros;
- visualizar grafos de classes, dependências e chamadas;
- concentrar páginas manuais sobre fluxos internos;
- cruzar documentação escrita com código real;
- validar se a documentação XML e as páginas técnicas continuam coerentes com a implementação.

## 1. O que existe neste diretório

Estrutura atual esperada:

```text
docs/doxygen/
  config/
  output-local/
  pages/
  Doxyfile.local
  README.md
````

### Significado de cada parte

* `Doxyfile.local`
  Ficheiro principal de configuração do Doxygen para uso local.

* `pages/`
  Páginas manuais escritas em Markdown, focadas em explicar partes importantes da implementação em Português de Portugal.

* `config/`
  Pasta reservada para configuração auxiliar, caso seja necessário separar ou expandir a configuração no futuro.

* `output-local/`
  Saída gerada localmente pelo Doxygen. É um artefacto de geração, não deve ser tratado como fonte.

* `README.md`
  Este ficheiro.

## 2. Papel do Doxygen no repositório

Neste projeto, o Doxygen serve para documentar a **implementação atual** com mais detalhe do que o DocFX.

É particularmente útil para:

* ver a estrutura dos namespaces e tipos;
* abrir documentação de classes e métodos diretamente ligada ao código;
* explorar call graphs e caller graphs;
* ver grafos de diretórios e relações entre componentes;
* juntar páginas manuais da equipa com a documentação gerada automaticamente.

Em resumo:

* **DocFX**: portal principal de documentação;
* **Doxygen**: exploração técnica detalhada da implementação.

## 3. Conteúdo manual importante

As páginas em `pages/` são a parte mais valiosa desta documentação, porque explicam o sistema com contexto técnico e não apenas com listagens automáticas.

Exemplos de páginas esperadas:

* `mainpage.md`
* `control-plane-and-bootstrap.md`
* `persistence-model.md`
* `prevention-flow.md`
* `simulator-flow.md`
* `tests-as-documentation.md`

Estas páginas devem estar em **Português de Portugal**, com foco em:

* objetivo;
* âmbito;
* componentes;
* fluxo real;
* decisões relevantes;
* limitações atuais;
* pontos importantes do repositório.

## 4. Pré-requisitos para gerar a documentação

Para a geração local funcionar corretamente, deves ter:

* **Doxygen** instalado e disponível no `PATH`;
* **Java** instalado;
* **Graphviz** instalado, com o comando `dot` disponível;
* **PlantUML** disponível localmente, se a configuração usar diagramas PlantUML.

### Verificações úteis

#### Confirmar Doxygen

```powershell
doxygen --version
```

#### Confirmar Java

```powershell
java -version
```

#### Confirmar Graphviz

```powershell
dot -V
```

#### Confirmar PlantUML
Exemplo:
```powershell
java -jar "C:\Users\Miguel\Tools\plantuml\plantuml.jar" -version
```

## 5. Nota importante sobre PlantUML

O ficheiro `Doxyfile.local` pode apontar para um caminho local específico do `plantuml.jar`.

Se esse caminho não existir na tua máquina, tens de:

* instalar o `plantuml.jar`;
* ajustar a opção `PLANTUML_JAR_PATH` no `Doxyfile.local`.

Caso contrário, a geração pode continuar, mas os diagramas PlantUML não serão processados corretamente.

## 6. Como limpar a geração anterior

A forma mais simples de garantir que não estás a ver artefactos antigos é apagar a saída local antes de regenerar.

A partir da raiz do repositório:

```powershell
Remove-Item .\docs\doxygen\output-local -Recurse -Force -ErrorAction SilentlyContinue
```

Se quiseres também remover logs temporários antigos que não fazem parte da fonte:

```powershell
Remove-Item .\.codex_tmp_doxygen.log -Force -ErrorAction SilentlyContinue
```

Isto é útil quando queres confirmar que tudo o que aparece foi realmente gerado na execução atual.

## 7. Como regenerar a documentação

A partir da raiz do repositório:

```powershell
doxygen .\docs\doxygen\Doxyfile.local
```

Se tudo correr bem, o Doxygen volta a criar a pasta `docs/doxygen/output-local/` e gera aí a documentação HTML e os restantes artefactos configurados.

## 8. Como abrir a documentação gerada

Depois da geração, o ponto de entrada mais comum é:

```text
docs/doxygen/output-local/html/index.html
```

Podes abrir esse ficheiro diretamente no browser.

## 9. O que verificar depois de gerar

Depois de abrir a documentação no browser, convém confirmar várias coisas.

### 9.1. Página principal

Verifica se a main page aparece corretamente e se explica o papel do Doxygen no projeto.

### 9.2. Related Pages

Confirma se as páginas manuais estão visíveis, por exemplo:

* controlo e bootstrap;
* persistência;
* fluxo do simulador;
* fluxo da prevenção;
* testes como documentação.

### 9.3. Namespaces

Confirma se os namespaces principais do projeto aparecem.

### 9.4. Classes

Confirma se o Doxygen encontrou as classes principais e se gerou páginas para elas.

### 9.5. Grafos

Verifica se estão a ser gerados:

* grafos de classes;
* call graphs;
* caller graphs;
* grafos de diretórios;
* diagramas PlantUML, se aplicável.

### 9.6. Warnings

No terminal, confirma se a geração terminou sem warnings relevantes.

## 10. O que esta documentação gera bem

O Doxygen é especialmente útil para:

* documentação de tipos e membros a partir do código;
* relações entre classes;
* dependências entre ficheiros e diretórios;
* call graphs e caller graphs;
* páginas técnicas de apoio à exploração da implementação atual.

## 11. O que esta documentação não substitui

O Doxygen não substitui:

* documentação de arquitetura mais narrativa e de leitura guiada;
* documentação de produto;
* documentação funcional de alto nível;
* documentação HTTP mais completa baseada em OpenAPI.

Também não deve ser tratado como a única fonte de explicação do sistema. Sem páginas manuais bem escritas, o Doxygen degrada-se rapidamente para um inventário técnico difícil de ler.

## 12. Convenções recomendadas para as páginas manuais

As páginas em `pages/` devem seguir estas regras:

* escrever em **Português de Portugal**;
* focar o sistema real, não um estado idealizado;
* distinguir claramente o que já existe do que ainda falta;
* usar nomes técnicos corretos dos projetos, componentes e fluxos;
* evitar duplicar texto de outros ficheiros sem necessidade;
* referir código, componentes e decisões concretas.

Uma boa página Doxygen deve responder a perguntas como:

* o que faz este fluxo;
* quem participa;
* onde está no repositório;
* que persistência usa;
* que limitações tem;
* que comportamento real foi confirmado no código.

## 13. Relação com a documentação XML do código

O Doxygen aproveita também a documentação XML presente no código C#.

Por isso, quando surgirem warnings do género:

* parâmetros não documentados;
* `@ref` não resolvidos;
* comentários incompletos;

a correção pode passar por:

* melhorar as páginas Markdown;
* corrigir comentários XML em classes, construtores ou métodos;
* ajustar referências Doxygen entre páginas e elementos.

## 14. Problemas comuns

### O Doxygen gera mas faltam diagramas

Normalmente significa problema com:

* `dot` não encontrado;
* Java não disponível;
* `plantuml.jar` não encontrado;
* configuração incompleta no `Doxyfile.local`.

### A página existe mas não aparece onde esperas

Pode estar em:

* **Related Pages**;
* **Main Page**;
* **Files**;
* ou com um título diferente do nome do ficheiro.

### Aparecem warnings de parâmetros não documentados

Faltam comentários XML no código.

### Aparecem referências não resolvidas

Há um `@ref` ou `\ref` que não corresponde exatamente a um símbolo ou página válida.

### A documentação parece antiga

Provavelmente não limpaste `output-local/` antes de regenerar.

## 15. Fluxo de trabalho recomendado

Quando fores mexer nesta documentação, usa esta sequência:

1. editar páginas em `docs/doxygen/pages/` e, se necessário, comentários XML no código;
2. limpar `output-local/`;
3. correr o Doxygen;
4. abrir o `index.html`;
5. confirmar páginas, grafos, classes e warnings;
6. só depois considerar a geração validada.

## 16. Comandos úteis

### Limpar saída anterior

```powershell
Remove-Item .\docs\doxygen\output-local -Recurse -Force -ErrorAction SilentlyContinue
```

### Limpar log temporário opcional

```powershell
Remove-Item .\.codex_tmp_doxygen.log -Force -ErrorAction SilentlyContinue
```

### Gerar documentação

```powershell
doxygen .\docs\doxygen\Doxyfile.local
```

### Abrir o índice gerado

```powershell
start .\docs\doxygen\output-local\html\index.html
```

## 17. Resumo prático

Em termos simples:

* `pages/` contém a explicação manual e mais importante;
* `Doxyfile.local` controla a geração;
* `output-local/` é apenas saída gerada;
* a geração local depende de Doxygen, Java, Graphviz e possivelmente PlantUML;
* o Doxygen é a ferramenta certa para explorar implementação, grafos e relações técnicas;
* a documentação só é realmente útil se as páginas manuais e os comentários XML forem mantidos com disciplina.
