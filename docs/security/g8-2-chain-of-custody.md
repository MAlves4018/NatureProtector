# G8.2 chain of custody

## Trust boundaries

- GitHub Actions OIDC autentica workflows específicos através de WIF.
- Cada service account está ligada a um `workflow_ref` exato.
- O candidato é identificado por commit SHA e release manifest SHA-256.
- Artifacts são verificados por workflow signer, source digest e source ref.
- O evidence index é closed-world: o conjunto de ficheiros tem de coincidir exatamente.
- Governance usa assinaturas OpenSSH detached e namespaces separados.

## Defesas implementadas

- schemas com `additionalProperties=false`;
- `FormatChecker` para datas/UUIDs;
- normalização de paths;
- rejeição de `..`, caminhos absolutos, barras invertidas e symlinks;
- hashes e tamanhos de todos os ficheiros;
- digest determinístico da árvore;
- run metadata validada pela API GitHub;
- run IDs únicos;
- timestamps não futuros;
- identidade do segundo operador distinta;
- severidades e estados fechados por enum;
- autorizações com validade limitada;
- revisão, autorização e execução por identidades diferentes;
- workflows G8 antigos bloqueados com `exit 1`.

## Não-confiança explícita

Não são fontes de verdade isoladas:

- nomes de artifacts;
- um workflow verde sem validação de metadata;
- JSON enviado pelo utilizador;
- resumo de métricas sem ficheiros brutos;
- assinatura sem allow-list e namespace;
- presença de um objeto no bucket sem receipt e controles do bucket.

## Secrets

Conteúdos de secrets, credenciais, tokens, chaves privadas, Terraform state e payloads do Secret Manager nunca entram na evidence. Apenas nomes, versões, identidades, hashes públicos e resultados de rotação podem ser registados.
