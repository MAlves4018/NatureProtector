# V1-000 - Baseline Evidence

## ID

V1-000

## Título

Baseline técnica e evidência de execução pré-V1

## Objetivo

Congelar o estado técnico atual do repositório antes de qualquer implementação V1, com evidência reproduzível e auditável.

## Escopo

- Executar apenas recolha de baseline técnica.
- Registar metadados de git, ambiente .NET, restore, build e testes.
- Guardar outputs em ficheiros de evidência definidos.

## Pré-condições

- Repositório local disponível.
- `dotnet` instalado e acessível no terminal.
- `docker` instalado e acessível no terminal (mesmo que não seja usado em runtime).
- Sem alterações de código durante a execução desta tarefa.

## Comandos a executar (ordem obrigatória)

```text
git status
git rev-parse HEAD
dotnet --info
docker --version
dotnet clean NatureProtector.sln
dotnet restore NatureProtector.sln
dotnet build NatureProtector.sln
dotnet test NatureProtector.sln
```

## Outputs a guardar

- `git_metadata.txt`
- `dotnet_info.txt`
- `dotnet_restore_output.txt`
- `dotnet_build_output.txt`
- `dotnet_test_output.txt`

## Mapeamento comando -> output

| Comando | Output obrigatório |
|---|---|
| `git status` + `git rev-parse HEAD` | `git_metadata.txt` |
| `dotnet --info` + `docker --version` | `dotnet_info.txt` |
| `dotnet restore NatureProtector.sln` | `dotnet_restore_output.txt` |
| `dotnet build NatureProtector.sln` | `dotnet_build_output.txt` |
| `dotnet test NatureProtector.sln` | `dotnet_test_output.txt` |

## Regra de classificação de falhas

- Se o `dotnet build NatureProtector.sln` falhar por permissão local relacionada com `NuGet.Config`, classificar como **limitação ambiental**.
- Nesse caso, **não** classificar como falha funcional confirmada da aplicação.
- Registar explicitamente a mensagem de erro e o contexto local.

## Evidência mínima por output

- Data/hora da execução.
- Comando executado.
- Exit code observado.
- Excerto relevante do output (ou output completo).

## O que não alterar

- Não alterar código de produção.
- Não alterar testes.
- Não alterar contratos RabbitMQ.

## Critério de pronto

- Todos os comandos executados na ordem definida.
- Todos os 5 ficheiros de output gerados.
- Classificação de falhas aplicada conforme regra de limitação ambiental.
- Evidência anexada em `docs/evidence/`.

## Limitações esperadas

- Diferenças de ambiente local podem alterar tempos e mensagens de tooling.
- Falhas de acesso/permissão local devem ser marcadas como limitação ambiental quando aplicável.
