import { OperationCategoryPage } from './OperationCategoryPage';

export function QualityRunsPage() {
  return (
    <OperationCategoryPage
      category="quality"
      title="Execuções de qualidade"
      subtitle="Executa suites fechadas através dos workflows existentes; não aceita comandos arbitrários."
    />
  );
}
