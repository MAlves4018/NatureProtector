export interface CapabilityProfileResponse {
  roles: string[];
  capabilities: string[];
  authority: string;
  evaluatedAt: string;
}

export interface OperationInputDefinitionResponse {
  name: string;
  description: string;
  required: boolean;
  defaultValue: string | null;
}

export interface OperationDefinitionResponse {
  operationId: string;
  category: 'quality' | 'evidence' | 'deployment' | 'cloud' | string;
  displayName: string;
  description: string;
  requiredCapability: string;
  riskLevel: string;
  requiresConfirmation: boolean;
  requiresApproval: boolean;
  environments: string[];
  inputs: OperationInputDefinitionResponse[];
  workflow: string;
  confirmationTemplate: string;
  authorized: boolean;
  availability: string;
  evidenceLevel: string;
  limitation: string | null;
}

export interface StartOperationRequest {
  operationId: string;
  environment: string;
  ref: string | null;
  inputs: Record<string, string>;
  collectEvidence: boolean;
  confirmation: string | null;
}

export interface OperationStepResponse {
  sequence: number;
  name: string;
  status: string;
  at: string;
  detail: string | null;
}

export interface OperationArtifactResponse {
  artifactId: string;
  name: string;
  kind: string;
  reference: string;
  sha256: string | null;
  sizeBytes: number | null;
  evidenceLevel: string;
}

export interface OperationApprovalResponse {
  decision: string;
  reviewer: string;
  at: string;
  comment: string | null;
}

export interface EngineeringOperationResponse {
  id: string;
  operationId: string;
  category: string;
  displayName: string;
  status: string;
  environment: string;
  ref: string;
  requestedBy: string;
  requestedByRoles: string[];
  requestedByCapabilities: string[];
  requestedAt: string;
  updatedAt: string;
  collectEvidence: boolean;
  riskLevel: string;
  requiresApproval: boolean;
  provider: string | null;
  providerReference: string | null;
  workflow: string | null;
  planHash: string | null;
  evidenceLevel: string;
  inputs: Record<string, string>;
  steps: OperationStepResponse[];
  artifacts: OperationArtifactResponse[];
  approvals: OperationApprovalResponse[];
  limitations: string[];
}

export interface OperationComparisonResponse {
  leftOperationId: string;
  rightOperationId: string;
  leftStatus: string;
  rightStatus: string;
  onlyOnLeft: string[];
  onlyOnRight: string[];
  sharedArtifacts: string[];
  evidenceLevel: string;
}

export interface CloudResourceDeclarationResponse {
  resourceType: string;
  name: string;
  scope: string;
  state: string;
  source: string;
}

export interface CloudEnvironmentResponse {
  environment: string;
  projectId: string;
  region: string;
  deployable: boolean;
  configurationSource: string;
  observedState: string;
  evidenceLevel: string;
  resources: CloudResourceDeclarationResponse[];
  limitations: string[];
}

export interface AdminUserResponse {
  id: string;
  username: string;
  email: string;
  roles: string[];
}

export interface AdminRoleResponse {
  id: number;
  name: string;
}
