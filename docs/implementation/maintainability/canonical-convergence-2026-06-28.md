# Convergência canónica — 28 de junho de 2026

O repositório `NatureProtector-master (8)(3).zip` (`b44c0d38e55bef14b00fc7177e20c19a54e2230206f1000869ffa79a61a5eff2`) foi usado como única fonte de verdade para deployment.

Foram preservados byte a byte os ficheiros existentes sob `.config/cloud`, `.github`, `deploy`, `infra/gcp`, `scripts/cloud`, `scripts/ci`, `scripts/release`, `tests/cloud` e documentação cloud. A única adição em `.github` é `quality-guardrails.yml`, que não substitui nenhum workflow de deployment.

As alterações integradas abrangem maintainability fora dessas superfícies, decomposição sustentável da UI, centralização de dependências, auditores, quality gates e tooling de evidência remediado.

O tooling de evidência deixa de fabricar B/C, deixa de usar uma baseline fixa, deriva estados dos summaries e aceita pacotes parciais honestos.
