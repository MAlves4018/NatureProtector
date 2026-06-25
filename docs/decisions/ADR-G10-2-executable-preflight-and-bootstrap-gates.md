# ADR G10.2 — Preflight executável e bootstrap cloud em duas etapas

## Estado

**Aceite para integração; nenhum recurso cloud foi criado nesta fase.**

## Contexto

A arquitetura cloud e o contrato local/cloud estão implementados estaticamente,
mas a primeira execução continua dependente de dados humanos e de ferramentas
externas: IDs numéricos do GitHub, IDs globais dos projetos, conta `gcloud`
ativa, janela de qualificação, custo observado e responsável pelo teardown.
Executar Terraform antes de fechar estes dados criaria risco de custo, identidade
incorreta e mistura com o projeto académico de Computação na Nuvem.

## Decisão

1. O bootstrap passa a usar um documento JSON validado pelo schema
   `g10-2-bootstrap-input.schema.json`.
2. O preflight é estritamente read-only: valida código, ferramentas, GitHub e GCP,
   mas não cria projetos, ativa APIs nem executa Terraform apply.
3. A criação inicial, quando autorizada, fica limitada a três projetos vazios
   (`platform`, `staging`, `production`) e à associação da billing account aprovada.
4. State bucket, WIF, Artifact Registry, Cloud Deploy, ambientes e data plane são
   fases posteriores e continuam com flags de criação `false`.
5. A primeira qualificação é efémera, no projeto staging, com janela máxima de sete
   dias e teardown owner explícito.
6. Production pode ser criada como projeto vazio para reservar isolamento e ID,
   mas não recebe APIs ou workloads nesta etapa.
7. Budgets são alertas, não hard caps. O saldo observado deve ser confirmado no
   momento da execução, e deve existir um valor mínimo de crédito a preservar.
8. O projeto `cn2526-t4-g04` nunca é um destino NatureProtector; apenas a billing
   account `0109B8-93144E-B93C1C` pode ser reutilizada como payer.

## Consequências

- a informação humana passa a ser verificável e reproduzível;
- o preflight pode ser repetido sem efeitos cloud;
- a criação de projetos exige switch, confirmação literal e `ShouldProcess`;
- nenhuma ausência de ferramenta pode ser confundida com falha do código;
- deployment continua não provado até existirem apply, imagens, workloads, smoke,
  observabilidade, rollback e evidence ligados ao mesmo commit.
