# Local Runtime Launcher

StartedAt: 2026-05-17T16:47:03.6623742+02:00
Repository: C:\Users\Miguel\UNI\6sem\PS\IMP\D\NatureProtector
ForceRestart: True
SkipDocker: True
SkipBootstrap: True

## Effective Targets

- Backoffice API: http://127.0.0.1:5254
- webUI: http://127.0.0.1:5173
- Developer Runtime View: http://127.0.0.1:5173/dev/runtime
- PostgreSQL: localhost:5433/natureprotector as np
- RabbitMQ AMQP: localhost:5672
- InfluxDB: http://localhost:8181

## Processes

- Backoffice API: PID 27100, Port 5254, URL http://127.0.0.1:5254, Log C:\Users\Miguel\UNI\6sem\PS\IMP\D\NatureProtector\docs\evidence\dev-runtime\20260517-164701\backoffice-api.log, ErrorLog C:\Users\Miguel\UNI\6sem\PS\IMP\D\NatureProtector\docs\evidence\dev-runtime\20260517-164701\backoffice-api.err.log, Script C:\Users\Miguel\UNI\6sem\PS\IMP\D\NatureProtector\docs\evidence\dev-runtime\20260517-164701\start-Backoffice-API.ps1
- Prevention Host: PID 21680, Port , URL , Log C:\Users\Miguel\UNI\6sem\PS\IMP\D\NatureProtector\docs\evidence\dev-runtime\20260517-164701\prevention-host.log, ErrorLog C:\Users\Miguel\UNI\6sem\PS\IMP\D\NatureProtector\docs\evidence\dev-runtime\20260517-164701\prevention-host.err.log, Script C:\Users\Miguel\UNI\6sem\PS\IMP\D\NatureProtector\docs\evidence\dev-runtime\20260517-164701\start-Prevention-Host.ps1
- webUI: PID 9132, Port 5173, URL http://127.0.0.1:5173, Log C:\Users\Miguel\UNI\6sem\PS\IMP\D\NatureProtector\docs\evidence\dev-runtime\20260517-164701\webui.log, ErrorLog C:\Users\Miguel\UNI\6sem\PS\IMP\D\NatureProtector\docs\evidence\dev-runtime\20260517-164701\webui.err.log, Script C:\Users\Miguel\UNI\6sem\PS\IMP\D\NatureProtector\docs\evidence\dev-runtime\20260517-164701\start-webUI.ps1

## Notes

- The launcher aborts if the API or webUI port is occupied, unless -ForceRestart can safely stop a local NatureProtector process.
- PostgreSQL connectivity is checked before starting application processes.
