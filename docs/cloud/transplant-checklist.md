# Checklist de transplante para o NatureProtector

## Antes de copiar

- [ ] Fazer backup ou criar uma branch de integração.
- [ ] Confirmar que não existe já um script com a mesma responsabilidade.
- [ ] Não substituir scripts cloud existentes sem comparação.
- [ ] Não alterar `.env`, `.env.example` ou `NatureProtector.brain`.

## Copiar

Copiar para a raiz do repositório:

```text
scripts/cloud/
config/cloud/
docs/cloud/
tests/cloud/
gitignore.cloud-snippet.txt
```

Não é obrigatório copiar este `README.md`; pode ser fundido com a documentação existente.

## Reconciliar caminhos

- [ ] Confirmar que `scripts/cloud` é a convenção usada no repositório.
- [ ] Confirmar o diretório real de Terraform.
- [ ] Confirmar onde evidence local deve ser guardada.
- [ ] Confirmar se `artifacts/` já está ignorado.
- [ ] Confirmar se a região oficial continua `europe-southwest1`.

## Reconciliar configuração

- [ ] Substituir apenas o Project ID predefinido nos exemplos, nunca hardcodar billing.
- [ ] Rever `config/cloud/required-apis.txt`.
- [ ] Remover APIs não usadas.
- [ ] Acrescentar APIs só quando o plano Terraform ou aplicação as justificar.
- [ ] Ajustar o montante do budget à moeda da billing account.

## Validar sem mutações

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass

.\tests\cloud\Test-CloudScaffold.Static.ps1

.\scripts\cloud\Initialize-CloudProject.ps1

.\scripts\cloud\Test-CloudSetup.ps1
```

## Aplicar apenas depois da revisão

```powershell
.\scripts\cloud\Initialize-CloudProject.ps1 -Apply
```

Use `-AllowBillingLink` apenas quando a ligação de billing estiver incorreta e a mudança tiver sido aprovada.

## Git

Antes do commit:

- [ ] Confirmar que não existe Billing Account ID no diff.
- [ ] Confirmar que não existem emails pessoais hardcoded.
- [ ] Confirmar que não existem tokens, ADC ou chaves JSON.
- [ ] Confirmar que evidence local não entrou no Git.
- [ ] Executar o teste estático.
- [ ] Rever o diff completo.
