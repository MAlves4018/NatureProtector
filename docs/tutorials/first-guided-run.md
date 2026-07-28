---
id: NP-TUTORIAL-FIRST-RUN
status: CURRENT
owner: Miguel Alves
audience: new developer, presenter
source_of_truth: scripts/np.ps1, runtime API and current webUI routes
last_verified_against: NatureProtector repository snapshot 2026-07-22
last_verified_at: 2026-07-22
review_triggers: runtime command, route, scenario or authentication changes
---

# Tutorial: primeira run guiada

## 1. Preparar o ambiente

A partir da raiz do repositório:

```powershell
.\scripts\np.ps1 doctor
.\scripts\np.ps1 init-local -Force
.\scripts\np.ps1 prepare-local
.\scripts\np.ps1 clean-local
.\scripts\np.ps1 up
.\scripts\np.ps1 start -OpenBrowser
.\scripts\np.ps1 health
```

Todos os health checks selecionados devem passar antes de iniciar a simulação.

## 2. Autenticar

Abrir `http://127.0.0.1:5173/login` e usar, apenas em Development:

```text
admin / admin123
```

## 3. Iniciar uma run nominal

Abrir `http://127.0.0.1:5173/simulation` e selecionar:

```text
Área: proenca-a-nova
Cenário: scenario_b
Sensores: 6
Ciclos: 5
Intervalo: 1 segundo
Seed: 12345
Perfis de degradação: none
Collect evidence: ativo
```

Rever os valores requested/resolved antes de iniciar.

## 4. Acompanhar o lifecycle

A interface deve apresentar request/operation/run e progredir até estado terminal. Não declarar sucesso apenas porque o processo produtor terminou: aguardar a convergência do pipeline e `Settled=true`.

Para esta dimensão, o total nominal calculado é:

```text
6 sensores × 5 ciclos = 30 observações
```

O audit deve confirmar, para o caminho nominal, total aceite coerente, ausência de rejected/quarantined e uma avaliação por leitura elegível.

## 5. Inspecionar resultados

Abrir:

- `/runs` para summary, audit e timings;
- `/queries` para executar diagnósticos preparados;
- `/pipeline` para inbox/tentativas/observabilidade;
- `/evidence` para artefactos associados à execução.

Verificar também que o `SimulationRunId` selecionado é propagado entre páginas e pertence à run acabada de executar.

## 6. Executar o cenário degradado

Repetir em `/simulation` com:

```text
Cenário: scenario_c
Perfis de degradação: missing-readings
```

Depois de settled, abrir `/scenario-compare`. O cenário C deve apresentar menos observações aceites do que o total esperado e `missing = expected - accepted`, sem reutilizar uma run histórica.

## 7. Confirmar encerramento

```powershell
Get-CimInstance Win32_Process |
  Where-Object { $_.CommandLine -like '*NatureProtector.Simulator.Host*' }
```

Depois da run terminar, não deve existir processo do simulador.

## 8. Parar o ambiente

```powershell
.\scripts\np.ps1 stop
.\scripts\np.ps1 down
```

Os números observados em campanhas anteriores são apenas exemplos. Toda a afirmação de aceitação deve incluir a identidade e timestamp da execução atual.
