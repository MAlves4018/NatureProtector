# Implementação Atual: Roles e Autorização

## 1. Finalidade do documento

Este documento consolida a implementação atual de roles e autorização na API de Backoffice, com foco nas rotas, nos acessos e na estrutura de dados associada.

O objetivo é descrever o que existe hoje, sem projetar arquitetura futura. Sempre que houver conflito entre este texto e o código da branch, prevalece o código.

Este documento também serve como guia de leitura para localizar rapidamente o que está implementado, sem exigir uma leitura direta do código.

## 2. Como ler este documento

O texto está organizado por vistas curtas, cada uma com um propósito claro. A leitura recomendada é:

1. Visão rápida e modelo de roles (o que existe e quem pode fazer o quê).
2. Persistência e catálogo de identidades (estrutura de dados real).
3. Fluxo de autorização e enforcement (onde o controlo é aplicado).
4. Rotas e superfície da API (contrato efetivo de acesso).
5. Configuração, tokens e limites (token atual e restrições conhecidas).
6. Observabilidade e evolução (lacunas e próximos passos).

## 3. Visão rápida

### 3.1. Lista de roles suportadas

- Admin: gestão de utilizadores e roles, e operações sensíveis do control plane.
- Sim: operações de simulação e alterações operacionais.
- Pipeline: leitura do estado operacional e resultados de simulação.

### 3.2. Onde são aplicadas (API, UI, processos)

- API (Backoffice.Api) aplica roles nas rotas de control plane e user/roles.
- UI não define regras aqui; a API é o ponto de enforcement.

## 4. Modelo de roles

### 4.1. Nomes canónicos e descrições

- Admin: acesso completo ao plano de controlo e gestão de identidades.
- Sim: pode iniciar runs, ativar configurações e executar diagnósticos em ambiente de desenvolvimento.
- Pipeline: acesso apenas de leitura a dados de simulação e estado operacional.

### 4.2. Relação com permissões e claims

- A autorização usa atributos `Authorize(Roles = "...")` nas rotas.
- O JWT inclui claims básicos (userId, username, email); os roles devem estar presentes no token para cumprir as guardas de API.

## 5. Persistência e catálogo de identidades

### 5.1. Origem dos utilizadores

- Persistência local em PostgreSQL, schema `user_base`.
- Entidades principais: `Users`, `Roles`, `UserRoles`.

### 5.2. Estrutura das tabelas

Roles
- `Id` (short) - identificador do role.
- `Name` (varchar(100)) - nome canónico do role.

Users
- `Id` (uuid) - identificador do utilizador.
- `Username` (varchar(100)) - nome curto.
- `Email` (varchar(200)) - email do utilizador.
- `PasswordHash` (varchar(500)) - hash da password.
- `Organization` (text) - organização do utilizador.
- `CreatedAt` (timestamp) - data de criação.

UserRoles
- `UserId`
- `RoleId`

### 5.3. Mapeamento de roles

- `UserRoles` liga utilizadores a roles (UserId -> RoleId).
- `Roles.Id` e `Roles.Name` definem o catálogo de roles.
- Valores são adicionados via DbContext em `NatureProtector.Infrastructure.Postgres`.

## 6. Fluxo de autorização

### 6.1. Ponto de entrada

- Base route de utilizadores e roles: `api/users-roles`.
- Login e logout geram e invalidam tokens para chamadas autenticadas.
- Control plane exposto em `api/control/*` com guardas por role.

### 6.2. Validação e enforcement

- `[Authorize]` ao nível do controller exige token por defeito.
- `[AllowAnonymous]` permite acesso público em rotas específicas.
- Roles especificadas por rota com `Authorize(Roles = "...")`.
- Rotas de leitura em `api/control/areas` permitem acesso anónimo quando indicadas.

## 7. Implementação no código

### 7.1. Projetos relevantes

- Backoffice.Api: control plane e user plane.
- Infrastructure.Postgres: definições de DB e mapeamentos.

### 7.2. Componentes principais

- User plane e rotas de autenticação:
  - [src/NatureProtector.Backoffice.Api/Controllers/UserAndRolesController.cs](src/NatureProtector.Backoffice.Api/Controllers/UserAndRolesController.cs)
- Control plane e restrições por role:
  - [src/NatureProtector.Backoffice.Api/Controllers/ControlAreasController.cs](src/NatureProtector.Backoffice.Api/Controllers/ControlAreasController.cs)
  - [src/NatureProtector.Backoffice.Api/Controllers/ControlConfigurationsController.cs](src/NatureProtector.Backoffice.Api/Controllers/ControlConfigurationsController.cs)
  - [src/NatureProtector.Backoffice.Api/Controllers/ControlRuntimeController.cs](src/NatureProtector.Backoffice.Api/Controllers/ControlRuntimeController.cs)
  - [src/NatureProtector.Backoffice.Api/Controllers/ControlSimulationRunsController.cs](src/NatureProtector.Backoffice.Api/Controllers/ControlSimulationRunsController.cs)

### 7.3. Rotas de user/roles

| Método           | Rota                                            | Roles         |
| ---------------- | ----------------------------------------------- | ------------- |
| POST             | `api/users-roles/login`                         | anónimo       |
| POST             | `api/users-roles/logout`                        | qualquer role |
| POST             | `api/users-roles/users`                         | Admin         |
| GET, PUT, DELETE | `api/users-roles/users/{userId}`                | Admin         |
| POST             | `api/users-roles/roles`                         | Admin         |
| GET, PUT, DELETE | `api/users-roles/roles/{roleId}`                | Admin         |
| PUT, DELETE      | `api/users-roles/users/{userId}/roles/{roleId}` | Admin         |
| GET              | `api/users-roles/roles/{roleId}/users`          | Admin         |
| GET              | `api/users-roles/users/{userId}/roles`          | qualquer role |
| GET              | `api/users-roles/users/{userId}/roles/{roleId}` | qualquer role |
| GET              | `api/users-roles/me`                            | qualquer role |

### 7.4. Rotas do control plane com roles

| Método | Rota                                                   | Roles                |
| ------ | ------------------------------------------------------ | -------------------- |
| GET    | `api/control/areas/{areaCode}/scenarios`               | Sim, Pipeline, Admin |
| GET    | `api/control/areas/{areaCode}/operational-state`       | Sim, Pipeline, Admin |
| GET    | `api/control/areas/{areaCode}/cells/operational-state` | Sim, Pipeline, Admin |
| GET    | `api/control/configurations`                           | Sim, Pipeline, Admin |
| GET    | `api/control/configurations/active`                    | Sim, Pipeline, Admin |
| POST   | `api/control/configurations/{versionNumber}/activate`  | Sim, Admin           |
| GET    | `api/control/runtime/summary`                          | Sim, Pipeline, Admin |
| GET    | `api/control/runtime/diagnostics`                      | Sim, Pipeline, Admin |
| POST   | `api/control/runtime/diagnostics/{diagnosticId}`       | Sim, Admin           |
| POST   | `api/control/runtime/runs`                             | Sim, Admin           |
| POST   | `api/control/runtime/reset`                            | Sim, Admin           |
| GET    | `api/control/runtime/runs/latest`                      | Sim, Pipeline, Admin |
| GET    | `api/control/runtime/runs/{runId}`                     | Sim, Pipeline, Admin |
| GET    | `api/control/runtime/runs/{runId}/audit`               | Sim, Pipeline, Admin |
| GET    | `api/control/runtime/runs/{runId}/timings`             | Sim, Pipeline, Admin |
| GET    | `api/control/simulation-runs`                          | Sim, Pipeline, Admin |
| GET    | `api/control/simulation-runs/{runId}`                  | Sim, Pipeline, Admin |

### 7.5. Rotas do control plane com acesso anónimo

| Método | Rota | Roles |
| --- | --- | --- |
| GET | `api/control/areas` | anónimo |
| GET | `api/control/areas/{areaCode}` | anónimo |
| GET | `api/control/areas/{areaCode}/GeoJSON` | anónimo |
| GET | `api/control/areas/{areaCode}/grid-cells` | anónimo |
| GET | `api/control/areas/{areaCode}/sensor-nodes` | anónimo |
| GET | `api/control/areas/{areaCode}/alerts/active` | anónimo |

## 8. Configuração e dados

### 8.1. Configs em appsettings/env

- JWT com claims básicos (userId, username, email).
- Token atual tem tempo de vida de 1h.
- Implementação atual é simples, sem refresh token.

### 8.2. Seeds e migrations relevantes

- Tabelas `user_base` criadas e preenchidas via DbContext em `NatureProtector.Infrastructure.Postgres`.

## 9. Casos de uso
### 9.1. Exemplo de acesso permitido

- Admin cria utilizador em `POST api/users-roles/users` e adiciona role Sim em `PUT api/users-roles/users/{userId}/roles/{roleId}`.
- Sim inicia uma run em `POST api/control/runtime/runs` (apenas Development).

### 9.2. Exemplo de acesso negado

- Pipeline não pode ativar configurações em `POST api/control/configurations/{versionNumber}/activate` (Sim/Admin only).

### 9.3. Exemplo de uso (ficheiro .http)
- No ficheiro [src\NatureProtector.Backoffice.Api\NatureProtector.Backoffice.Api.http](src\NatureProtector.Backoffice.Api\NatureProtector.Backoffice.Api.http) estão definidos passos para testar a utilização geral do sistema de roles e utilizadores
## 10. Planos Futuros

- Sistema de refresh token (token persistente em cookies + token temporário de autorização) ainda não implementado.
- Implementar UI de Administrador para criar utilizadores e adicionar/criar roles.