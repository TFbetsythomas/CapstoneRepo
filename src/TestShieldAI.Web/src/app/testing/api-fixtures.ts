import {
  AskInsightsResponse,
  Coverage,
  GenerateTestsResponse,
  Project,
  RegressionFinding,
  RiskSummary,
  RunTestsResponse
} from '../core/models/testshield.models';

export const sampleProject: Project = {
  id: '11111111-1111-1111-1111-111111111111',
  name: 'Orders API',
  baseUrl: 'https://api.example.com',
  createdAt: '2026-01-01T00:00:00+00:00'
};

export const sampleCoverage: Coverage = {
  totalOperations: 8,
  coveredOperations: 3,
  uncoveredOperations: 5,
  coveragePercent: 12.34,
  aiCoveredOperations: 2,
  scenarios: {
    happyPath: 3,
    negative: 1,
    aiPositive: 1,
    aiNegative: 1,
    aiEdge: 0
  },
  gaps: [
    {
      specKey: 'Orders',
      method: 'GET',
      path: '/orders/{id}',
      reason: 'No happy-path or negative-missing-required-body test'
    }
  ]
};

export const sampleRisk: RiskSummary = {
  level: 'Medium',
  title: 'Review recommended',
  summary: 'The current run requires human review before release.',
  reasons: ['A status mismatch was detected', 'Coverage is incomplete'],
  recommendations: [
    'Review the current test results.',
    'Confirm the baseline is trusted before release.',
    'Review contract drift findings if present.'
  ]
};

export const sampleFinding: RegressionFinding = {
  category: 'Contract',
  code: 'StatusMismatch',
  severity: 'High',
  kind: 'HappyPath',
  method: 'GET',
  path: '/orders',
  expected: '200',
  current: '500',
  jsonPath: '$',
  message: 'Expected status 200 but received 500'
};

export function generateResponse(): GenerateTestsResponse {
  return {
    tests: [
      {
        kind: 'HappyPath',
        method: 'GET',
        pathTemplate: '/orders',
        path: '/orders',
        parameters: [],
        expectedStatus: 200,
        sourceMethod: 'GET',
        sourcePath: '/orders',
        specKey: 'Orders'
      }
    ],
    coverage: sampleCoverage,
    risk: sampleRisk
  };
}

export function runResponse(overrides: Partial<RunTestsResponse> = {}): RunTestsResponse {
  return {
    baseUrl: sampleProject.baseUrl,
    decision: 'Safe',
    baselineEstablishedAt: '2026-01-02T00:00:00+00:00',
    findings: [],
    results: [
      {
        kind: 'HappyPath',
        method: 'GET',
        path: '/orders',
        sourceMethod: 'GET',
        sourcePath: '/orders',
        specKey: 'Orders',
        expectedStatus: 200,
        actualStatus: 200,
        headers: { 'content-type': 'application/json' },
        body: '{"id":1}',
        durationMs: 12,
        statusMatched: true,
        error: null,
        validation: {
          outcome: 'Passed',
          statusValid: true,
          schemaValid: true,
          failures: []
        }
      }
    ],
    ai: {
      enabled: true,
      generatedCount: 2,
      acceptedCount: 1,
      warningCount: 1,
      warnings: ['One AI scenario was dropped']
    },
    coverage: sampleCoverage,
    risk: sampleRisk,
    ...overrides
  };
}

export function askInsightsResponse(overrides: Partial<AskInsightsResponse> = {}): AskInsightsResponse {
  return {
    question: 'Why was this run blocked?',
    intent: 'ExplainDecision',
    answer: 'This run is Block because a high-severity finding was recorded.',
    assistantAvailable: false,
    answerSource: 'deterministic',
    warning: null,
    evidence: {
      decision: 'Block',
      lastRunCompletedAt: '2026-01-02T00:00:00+00:00',
      retrieved: ['lastRun.decision', 'lastRun.findings']
    },
    ...overrides
  };
}
