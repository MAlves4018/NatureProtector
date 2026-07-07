import { UiLocaleProvider } from './LocaleContext';
import { UiCapabilityProvider } from './CapabilityContext';
import { UiAreaProvider } from './AreaContext';
import { UiRiskProvider } from './RiskContext';
import { UiActivityProvider } from './ActivityContext';
import { UiSimulationProvider } from './SimulationContext';
import { UiObservabilityProvider } from './ObservabilityContext';
import { OperationsProvider } from '../operations/OperationsContext';
import { UiQaTestProvider } from './QaTestContext';
import { UiAlertProvider } from './AlertContext';
import { UiToastProvider } from './ToastContext';

export function UiProvider({ children, isDark }: { children: React.ReactNode; isDark: boolean }) {
  return (
    <UiLocaleProvider>
      <UiCapabilityProvider isDark={isDark}>
        <UiAreaProvider>
          <UiRiskProvider>
            <UiActivityProvider>
              <UiSimulationProvider>
                <UiObservabilityProvider>
                  <OperationsProvider>
                    <UiQaTestProvider>
                      <UiAlertProvider>
                        <UiToastProvider>{children}</UiToastProvider>
                      </UiAlertProvider>
                    </UiQaTestProvider>
                  </OperationsProvider>
                </UiObservabilityProvider>
              </UiSimulationProvider>
            </UiActivityProvider>
          </UiRiskProvider>
        </UiAreaProvider>
      </UiCapabilityProvider>
    </UiLocaleProvider>
  );
}
