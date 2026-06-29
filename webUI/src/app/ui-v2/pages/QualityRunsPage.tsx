import { OperationCategoryPage } from './OperationCategoryPage';

export function QualityRunsPage() {
  return (
    <OperationCategoryPage
      category="quality"
      title="Quality Runs"
      subtitle="Executa suites fechadas através dos workflows existentes; não aceita comandos arbitrários."
    />
  );
}
