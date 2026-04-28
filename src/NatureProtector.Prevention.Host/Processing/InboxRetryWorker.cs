using Microsoft.Extensions.Options;
using NatureProtector.Prevention.Host.Configuration;
using NatureProtector.Shared.Observability;

namespace NatureProtector.Prevention.Host.Processing;

/*
 * Este worker procura eventos cuja nova tentativa já é devida e reentra-os no
 * fluxo operacional.
 *
 * Rationale:
 * - Os retries não devem ficar dependentes da receção de novas mensagens no
 *   broker.
 * - Um worker dedicado simplifica a observação do comportamento de retries e
 *   reduz acoplamento com o consumidor principal.
 *
 * Design considerations:
 * - O worker processa todos os retries devidos antes de voltar ao ciclo de
 *   espera.
 * - Em caso de falha inesperada faz backoff simples usando o intervalo de
 *   polling configurado.
 */

public sealed class InboxRetryWorker(
    ILogger<InboxRetryWorker> logger,
    IOptions<PreventionHostOptions> preventionHostOptions,
    IReadingEventInbox readingEventInbox,
    ReadingEventProcessingService processingService) : BackgroundService
{
    private readonly PreventionHostOptions _options = preventionHostOptions.Value;

    /// <summary>
    /// Mantém ativo o ciclo de polling e processamento das tentativas já
    /// devidas.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var processedAny = false;
            var encounteredFailure = false;

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    var workItem = await readingEventInbox.TryStartDueRetryAsync(
                        "reading_risk_pipeline",
                        stoppingToken);

                    if (workItem is null)
                    {
                        break;
                    }

                    processedAny = true;

                    logger.LogInformation(
                        "Picked up due retry from inbox. InboxEventId={InboxEventId} Attempt={AttemptNumber}",
                        workItem.Lease.InboxEventId,
                        workItem.Lease.AttemptNumber);
                    PreventionHostTelemetry.RetryPickedEvents.Add(1);

                    await processingService.ProcessAsync(
                        workItem.Envelope,
                        workItem.Lease,
                        stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                encounteredFailure = true;
                logger.LogError(
                    ex,
                    "Inbox retry worker hit an unexpected failure while polling or processing due retries.");
            }

            if (processedAny && !encounteredFailure)
            {
                continue;
            }

            // Quando não há trabalho pendente, o worker espera antes de voltar a
            // consultar o inbox para evitar polling agressivo.
            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(Math.Max(1, _options.RetryPollingIntervalSeconds)),
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }
}
