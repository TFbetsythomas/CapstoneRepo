export interface Project {
  id: string;
  name: string;
  baseUrl: string;
  createdAt: string;
}

export interface CreateProjectRequest {
  name: string;
  baseUrl: string;
}

export interface ImportSpecRequest {
  specification: string;
}

export interface ImportedOperation {
  method: string;
  path: string;
}

export interface ImportSpecResponse {
  operations: ImportedOperation[];
}

export interface GeneratedApiParameter {
  name: string;
  location: string;
  required: boolean;
  placeholder: string;
}

export interface GeneratedApiTestCase {
  kind: string;
  method: string;
  pathTemplate: string;
  path: string;
  parameters: GeneratedApiParameter[];
  requestBody?: string | null;
  expectedStatus: number;
  sourceMethod: string;
  sourcePath: string;
  specKey?: string | null;
}

export interface CoverageScenarios {
  happyPath: number;
  negative: number;
  aiPositive: number;
  aiNegative: number;
  aiEdge: number;
}

export interface CoverageGap {
  specKey: string;
  method: string;
  path: string;
  reason: string;
}

export interface Coverage {
  totalOperations: number;
  coveredOperations: number;
  uncoveredOperations: number;
  coveragePercent: number;
  aiCoveredOperations: number;
  scenarios: CoverageScenarios;
  gaps: CoverageGap[];
}

export interface RiskSummary {
  level: string;
  title: string;
  summary: string;
  reasons: string[];
  recommendations: string[];
}

export interface GenerateTestsResponse {
  tests: GeneratedApiTestCase[];
  coverage: Coverage;
  risk: RiskSummary;
}

export interface ContractValidationFailure {
  code: string;
  jsonPath: string;
  message: string;
}

export interface ContractValidationResult {
  outcome: string;
  statusValid: boolean;
  schemaValid?: boolean | null;
  failures: ContractValidationFailure[];
}

export interface ApiTestExecutionResult {
  kind: string;
  method: string;
  path: string;
  sourceMethod: string;
  sourcePath: string;
  specKey?: string | null;
  expectedStatus: number;
  actualStatus?: number | null;
  headers: Record<string, string>;
  body?: string | null;
  durationMs: number;
  statusMatched: boolean;
  error?: string | null;
  validation: ContractValidationResult;
}

export interface RegressionFinding {
  category: string;
  code: string;
  severity: string;
  kind?: string | null;
  method: string;
  path: string;
  expected: string;
  current: string;
  jsonPath?: string | null;
  message: string;
}

export interface AiRunSummary {
  enabled: boolean;
  generatedCount: number;
  acceptedCount: number;
  warningCount: number;
  warnings: string[];
}

export interface RunTestsResponse {
  baseUrl: string;
  results: ApiTestExecutionResult[];
  decision: string;
  baselineEstablishedAt?: string | null;
  findings: RegressionFinding[];
  ai: AiRunSummary;
  coverage: Coverage;
  risk: RiskSummary;
}

export interface AskInsightsRequest {
  question: string;
}

export interface AskInsightsEvidence {
  decision?: string | null;
  lastRunCompletedAt?: string | null;
  retrieved: string[];
}

export interface AskInsightsResponse {
  question: string;
  intent: string;
  answer: string;
  assistantAvailable: boolean;
  answerSource: string;
  warning?: string | null;
  evidence: AskInsightsEvidence;
}
