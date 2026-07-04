export interface ScenarioResponse {
  id: string;
  code: string;
  name: string;
  scenarioKind: string;
  configurationVersionNumber: number;
  description: string | null;
  baseScenarioCode: string | null;
  datasetBindingCount: number;
}
