# Local Runtime Launcher

StartedAt: 2026-05-18T19:47:38.0693831+02:00
Repository: C:\Users\Miguel\UNI\6sem\PS\IMP\D\NatureProtector
ForceRestart: True
SkipDocker: False
SkipBootstrap: True

## Effective Targets

- Backoffice API: http://127.0.0.1:5254
- webUI: http://127.0.0.1:5173
- Developer Runtime View: http://127.0.0.1:5173/dev/runtime
- PostgreSQL: localhost:5433/natureprotector as np
- RabbitMQ AMQP: localhost:5672
- InfluxDB: http://localhost:8181

## Processes

- Backoffice API: PID 33528, Port 5254, URL http://127.0.0.1:5254, Log C:\Users\Miguel\UNI\6sem\PS\IMP\D\NatureProtector\docs\evidence\dev-runtime\20260518-194733\backoffice-api.log, ErrorLog C:\Users\Miguel\UNI\6sem\PS\IMP\D\NatureProtector\docs\evidence\dev-runtime\20260518-194733\backoffice-api.err.log, Script C:\Users\Miguel\UNI\6sem\PS\IMP\D\NatureProtector\docs\evidence\dev-runtime\20260518-194733\start-Backoffice-API.ps1
- Prevention Host: PID 30860, Port , URL , Log C:\Users\Miguel\UNI\6sem\PS\IMP\D\NatureProtector\docs\evidence\dev-runtime\20260518-194733\prevention-host.log, ErrorLog C:\Users\Miguel\UNI\6sem\PS\IMP\D\NatureProtector\docs\evidence\dev-runtime\20260518-194733\prevention-host.err.log, Script C:\Users\Miguel\UNI\6sem\PS\IMP\D\NatureProtector\docs\evidence\dev-runtime\20260518-194733\start-Prevention-Host.ps1
- webUI: PID 32916, Port 5173, URL http://127.0.0.1:5173, Log C:\Users\Miguel\UNI\6sem\PS\IMP\D\NatureProtector\docs\evidence\dev-runtime\20260518-194733\webui.log, ErrorLog C:\Users\Miguel\UNI\6sem\PS\IMP\D\NatureProtector\docs\evidence\dev-runtime\20260518-194733\webui.err.log, Script C:\Users\Miguel\UNI\6sem\PS\IMP\D\NatureProtector\docs\evidence\dev-runtime\20260518-194733\start-webUI.ps1

## Notes

- The launcher aborts if the API or webUI port is occupied, unless -ForceRestart can safely stop a local NatureProtector process.
- PostgreSQL connectivity is checked before starting application processes.
