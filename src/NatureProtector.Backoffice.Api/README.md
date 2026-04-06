# NatureProtector.Backoffice.Api

Este projeto representa a fronteira HTTP da solução, mas ainda está numa fase inicial. Hoje devemos lê-lo como um esqueleto de ASP.NET Core preparado para crescer, não como uma API já funcional do plano de controlo.

## O que existe hoje

- `Program.cs`
  - arranque base de uma aplicação ASP.NET Core
- suporte a controladores
- suporte a OpenAPI em desenvolvimento
- um ficheiro HTTP de apoio:
  - `NatureProtector.Backoffice.Api.http`

## O que ainda não existe

- controladores funcionais;
- endpoints de configuração;
- endpoints de cenários;
- endpoints de consulta de risco, alertas ou projeções;
- integração com PostgreSQL;
- modelo de autenticação ou autorização digno de um backoffice real.

## Observações importantes

- A pasta `Controllers/` existe, mas está vazia.
- O ficheiro `NatureProtector.Backoffice.Api.http` continua a apontar para `/weatherforecast`, mas esse endpoint não existe no código atual. Deve ser lido como artefacto de arranque de template.
- O projeto já referencia `NatureProtector.Core` e `NatureProtector.Shared`, o que mostra a intenção de o ligar ao domínio e aos contratos, mas essa ligação ainda não foi concretizada em endpoints.

## Como devemos posicionar este módulo

Hoje, este projeto serve sobretudo para:

- reservar a fronteira HTTP do produto;
- estabilizar a presença da API na solução;
- evitar que toda a lógica de produto acabe embutida nos workers.

Para perceber a direção futura, devemos cruzar este projeto com [../../docs/planning/project-completion-roadmap.md](../../docs/planning/project-completion-roadmap.md).
