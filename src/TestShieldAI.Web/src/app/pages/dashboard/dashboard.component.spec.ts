import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import {
  askInsightsResponse,
  generateResponse,
  runResponse,
  sampleCoverage,
  sampleFinding,
  sampleProject,
  sampleRisk
} from '../../testing/api-fixtures';
import { DashboardComponent } from './dashboard.component';

describe('DashboardComponent', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DashboardComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { paramMap: convertToParamMap({ projectId: sampleProject.id }) }
          }
        }
      ]
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function loadDashboard() {
    const fixture = TestBed.createComponent(DashboardComponent);
    fixture.detectChanges();
    http.expectOne('/api/projects').flush([sampleProject]);
    fixture.detectChanges();
    return fixture;
  }

  function openAsk(fixture: ReturnType<typeof loadDashboard>) {
    const launcher = fixture.nativeElement.querySelector('[data-testid="ask-launcher"]') as HTMLButtonElement;
    launcher.click();
    fixture.detectChanges();
    return fixture;
  }

  it('loads the selected project dashboard', () => {
    const fixture = loadDashboard();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Orders API');
    expect(text).toContain('https://api.example.com');
  });

  it('displays a Safe decision from the backend', () => {
    const fixture = loadDashboard();
    fixture.componentInstance.runTests();
    http.expectOne(`/api/projects/${sampleProject.id}/tests/run`).flush(runResponse({ decision: 'Safe' }));
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="decision"]')?.textContent).toContain('Safe');
  });

  it('displays a Review decision from the backend', () => {
    const fixture = loadDashboard();
    fixture.componentInstance.runTests();
    http.expectOne(`/api/projects/${sampleProject.id}/tests/run`).flush(runResponse({ decision: 'Review' }));
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="decision"]')?.textContent).toContain('Review');
  });

  it('displays a Block decision from the backend', () => {
    const fixture = loadDashboard();
    fixture.componentInstance.runTests();
    http.expectOne(`/api/projects/${sampleProject.id}/tests/run`).flush(runResponse({ decision: 'Block' }));
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="decision"]')?.textContent).toContain('Block');
  });

  it('displays the backend risk summary without recalculating it', () => {
    const fixture = loadDashboard();
    fixture.componentInstance.runTests();
    http.expectOne(`/api/projects/${sampleProject.id}/tests/run`).flush(runResponse({ risk: sampleRisk }));
    fixture.detectChanges();

    const risk = fixture.nativeElement.querySelector('[data-testid="risk"]')?.textContent ?? '';
    expect(risk).toContain('Medium');
    expect(risk).toContain('Review recommended');
    expect(risk).toContain('The current run requires human review before release.');
    expect(risk).toContain('A status mismatch was detected');
    expect(risk).toContain('Review the current test results.');
  });

  it('displays backend coverage values without recalculating them', () => {
    const fixture = loadDashboard();
    fixture.componentInstance.runTests();
    http.expectOne(`/api/projects/${sampleProject.id}/tests/run`).flush(runResponse({ coverage: sampleCoverage }));
    fixture.detectChanges();

    const coverage = fixture.nativeElement.querySelector('[data-testid="coverage"]')?.textContent ?? '';
    expect(coverage).toContain('8');
    expect(coverage).toContain('3');
    expect(coverage).toContain('5');
    expect(coverage).toContain('12.34%');
    expect(coverage).toContain('2');
    expect(coverage).not.toContain('37.5');
  });

  it('displays coverage gaps from the backend', () => {
    const fixture = loadDashboard();
    fixture.componentInstance.runTests();
    http.expectOne(`/api/projects/${sampleProject.id}/tests/run`).flush(runResponse());
    fixture.detectChanges();

    const coverage = fixture.nativeElement.querySelector('[data-testid="coverage"]')?.textContent ?? '';
    expect(coverage).toContain('Orders');
    expect(coverage).toContain('GET');
    expect(coverage).toContain('/orders/{id}');
    expect(coverage).toContain('No happy-path or negative-missing-required-body test');
  });

  it('displays findings from the backend', () => {
    const fixture = loadDashboard();
    fixture.componentInstance.runTests();
    http.expectOne(`/api/projects/${sampleProject.id}/tests/run`).flush(runResponse({ findings: [sampleFinding] }));
    fixture.detectChanges();

    const findings = fixture.nativeElement.querySelector('[data-testid="findings"]')?.textContent ?? '';
    expect(findings).toContain('Contract');
    expect(findings).toContain('High');
    expect(findings).toContain('Expected status 200 but received 500');
    expect(fixture.nativeElement.querySelector('tr.high')).toBeTruthy();
  });

  it('shows the no-findings empty state', () => {
    const fixture = loadDashboard();
    fixture.componentInstance.runTests();
    http.expectOne(`/api/projects/${sampleProject.id}/tests/run`).flush(runResponse({ findings: [] }));
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="findings-empty"]')?.textContent).toContain(
      'No regression or contract drift findings.'
    );
  });

  it('displays the AI summary from the backend', () => {
    const fixture = loadDashboard();
    fixture.componentInstance.runTests();
    http.expectOne(`/api/projects/${sampleProject.id}/tests/run`).flush(runResponse());
    fixture.detectChanges();

    const ai = fixture.nativeElement.querySelector('[data-testid="ai-summary"]')?.textContent ?? '';
    expect(ai).toContain('Generated');
    expect(ai).toContain('2');
    expect(ai).toContain('One AI scenario was dropped');
  });

  it('displays a neutral AI disabled message', () => {
    const fixture = loadDashboard();
    fixture.componentInstance.runTests();
    http.expectOne(`/api/projects/${sampleProject.id}/tests/run`).flush(
      runResponse({
        ai: { enabled: false, generatedCount: 0, acceptedCount: 0, warningCount: 0, warnings: [] }
      })
    );
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="ai-summary"]')?.textContent).toContain(
      'AI scenario generation is currently disabled.'
    );
  });

  it('calls generate on the correct endpoint and does not treat tests as executed', () => {
    const fixture = loadDashboard();
    fixture.componentInstance.generateTests();
    const request = http.expectOne(`/api/projects/${sampleProject.id}/tests/generate`);
    expect(request.request.method).toBe('POST');
    request.flush(generateResponse());
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('have not been executed');
    expect(text).toContain('HappyPath');
    expect(fixture.nativeElement.querySelector('[data-testid="decision"] .decision-value')).toBeNull();
  });

  it('calls run on the correct endpoint and shows execution results', () => {
    const fixture = loadDashboard();
    fixture.componentInstance.runTests();
    const request = http.expectOne(`/api/projects/${sampleProject.id}/tests/run`);
    expect(request.request.method).toBe('POST');
    request.flush(runResponse());
    fixture.detectChanges();

    const results = fixture.nativeElement.querySelector('[data-testid="test-results"]')?.textContent ?? '';
    expect(results).toContain('Passed');
    expect(results).toContain('200');
  });

  it('disables generate and run actions while a request is in progress', () => {
    const fixture = loadDashboard();
    fixture.componentInstance.generateTests();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="generate-tests"]').disabled).toBeTrue();
    expect(fixture.nativeElement.querySelector('[data-testid="run-tests"]').disabled).toBeTrue();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Working');

    http.expectOne(`/api/projects/${sampleProject.id}/tests/generate`).flush(generateResponse());
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="generate-tests"]').disabled).toBeFalse();
  });

  it('shows a user-friendly API error', () => {
    const fixture = loadDashboard();
    fixture.componentInstance.runTests();
    http.expectOne(`/api/projects/${sampleProject.id}/tests/run`).flush(
      { title: 'Project not found', detail: "No project exists with id 'missing'." },
      { status: 404, statusText: 'Not Found' }
    );
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="error"]')?.textContent).toContain(
      "No project exists with id 'missing'."
    );
  });

  it('renders a floating Ask TestShield AI button instead of a large dashboard card', () => {
    const fixture = loadDashboard();

    expect(fixture.nativeElement.querySelector('[data-testid="ask-launcher"]')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('[data-testid="ask-launcher"]')?.getAttribute('aria-label')).toBe(
      'Open Ask TestShield AI'
    );
    expect(fixture.nativeElement.querySelector('[data-testid="ask-card"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid="ask-panel"]')).toBeNull();
  });

  it('opens the chat panel from the floating button and closes it from Close', () => {
    const fixture = loadDashboard();
    const launcher = fixture.nativeElement.querySelector('[data-testid="ask-launcher"]') as HTMLButtonElement;

    launcher.click();
    fixture.detectChanges();

    const panel = fixture.nativeElement.querySelector('[data-testid="ask-panel"]') as HTMLElement;
    expect(panel).toBeTruthy();
    expect(panel.textContent).toContain('Ask TestShield AI');
    expect(launcher.getAttribute('aria-expanded')).toBe('true');

    (fixture.nativeElement.querySelector('[data-testid="ask-close"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="ask-panel"]')).toBeNull();
    expect(launcher.getAttribute('aria-expanded')).toBe('false');
  });

  it('toggles the chat panel closed when the floating button is clicked again', () => {
    const fixture = loadDashboard();
    const launcher = fixture.nativeElement.querySelector('[data-testid="ask-launcher"]') as HTMLButtonElement;

    launcher.click();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[data-testid="ask-panel"]')).toBeTruthy();

    launcher.click();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[data-testid="ask-panel"]')).toBeNull();
  });

  it('renders suggested questions when the chat panel is empty', () => {
    const fixture = openAsk(loadDashboard());
    const suggestions = Array.from(
      fixture.nativeElement.querySelectorAll('[data-testid="ask-suggestion"]')
    ).map(button => (button as HTMLElement).textContent?.trim());

    expect(suggestions).toEqual([
      'Explain the latest run',
      'What tests failed?',
      'What changed from baseline?',
      'What coverage gaps exist?',
      'What negative scenarios exist?'
    ]);
  });

  it('submits a suggestion through the existing askInsights flow', () => {
    const fixture = openAsk(loadDashboard());
    const suggestion = fixture.nativeElement.querySelector('[data-testid="ask-suggestion"]') as HTMLButtonElement;
    expect(suggestion.textContent?.trim()).toBe('Explain the latest run');
    suggestion.click();
    fixture.detectChanges();

    const request = http.expectOne(`/api/projects/${sampleProject.id}/insights/ask`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ question: 'Explain the latest run' });
    request.flush(askInsightsResponse({ question: 'Explain the latest run', answer: 'Latest run summary.' }));
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="ask-answer"]')?.textContent).toContain('Latest run summary.');
    expect(fixture.nativeElement.querySelector('[data-testid="ask-suggestion"]')).toBeNull();
  });

  it('does not call the API for a blank question', () => {
    const fixture = openAsk(loadDashboard());
    fixture.componentInstance.askQuestion = '   ';
    fixture.componentInstance.askInsights();
    fixture.detectChanges();

    http.expectNone(`/api/projects/${sampleProject.id}/insights/ask`);
    expect(fixture.nativeElement.querySelector('[data-testid="ask-validation"]')?.textContent).toContain(
      'Enter a question before asking.'
    );
  });

  it('rejects questions longer than 500 characters client-side', () => {
    const fixture = openAsk(loadDashboard());
    fixture.componentInstance.askQuestion = 'a'.repeat(501);
    fixture.componentInstance.askInsights();
    fixture.detectChanges();

    http.expectNone(`/api/projects/${sampleProject.id}/insights/ask`);
    expect(fixture.nativeElement.querySelector('[data-testid="ask-validation"]')?.textContent).toContain(
      '500 characters or fewer'
    );
  });

  it('asks the project-scoped insights endpoint with only the question body', () => {
    const fixture = loadDashboard();
    fixture.componentInstance.askQuestion = 'Why was this run blocked?';
    fixture.componentInstance.askInsights();

    const request = http.expectOne(`/api/projects/${sampleProject.id}/insights/ask`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ question: 'Why was this run blocked?' });
    expect(Object.keys(request.request.body)).toEqual(['question']);
    expect(request.request.url).not.toMatch(/openai|azure|api\.openai\.com/i);
    request.flush(askInsightsResponse());
  });

  it('displays a successful LLM answer without treating AI as a dashboard failure', () => {
    const fixture = openAsk(loadDashboard());
    fixture.componentInstance.askQuestion = 'Why was this run blocked?';
    fixture.componentInstance.askInsights();
    http.expectOne(`/api/projects/${sampleProject.id}/insights/ask`).flush(
      askInsightsResponse({
        assistantAvailable: true,
        answerSource: 'llm',
        answer: 'The stored run was blocked by a high-severity finding.',
        warning: null
      })
    );
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="ask-answer"]')?.textContent).toContain(
      'The stored run was blocked by a high-severity finding.'
    );
    expect(fixture.nativeElement.querySelector('[data-testid="ask-source"]')?.textContent).toContain('llm');
    expect(fixture.nativeElement.querySelector('[data-testid="ask-fallback"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid="error"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid="ask-error"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid="ask-suggestion"]')).toBeNull();
  });

  it('displays a deterministic fallback answer normally', () => {
    const fixture = openAsk(loadDashboard());
    fixture.componentInstance.askQuestion = 'Why was this run blocked?';
    fixture.componentInstance.askInsights();
    http.expectOne(`/api/projects/${sampleProject.id}/insights/ask`).flush(askInsightsResponse());
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="ask-answer"]')?.textContent).toContain(
      'This run is Block because a high-severity finding was recorded.'
    );
    expect(fixture.nativeElement.querySelector('[data-testid="ask-source"]')?.textContent).toContain('deterministic');
    expect(fixture.nativeElement.querySelector('[data-testid="ask-fallback"]')?.textContent).toContain(
      'Using deterministic evidence because AI is unavailable.'
    );
    expect(fixture.nativeElement.querySelector('[data-testid="error"]')).toBeNull();
  });

  it('displays a warning when the insights response includes one', () => {
    const fixture = openAsk(loadDashboard());
    fixture.componentInstance.askQuestion = 'Why was this run blocked?';
    fixture.componentInstance.askInsights();
    http.expectOne(`/api/projects/${sampleProject.id}/insights/ask`).flush(
      askInsightsResponse({
        warning: 'AI is unavailable. Showing a deterministic explanation.'
      })
    );
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="ask-warning"]')?.textContent).toContain(
      'AI is unavailable. Showing a deterministic explanation.'
    );
    expect(fixture.nativeElement.querySelector('[data-testid="ask-answer"]')?.textContent).toContain(
      'This run is Block because a high-severity finding was recorded.'
    );
  });

  it('disables Ask while a request is in progress and ignores duplicate clicks', () => {
    const fixture = openAsk(loadDashboard());
    fixture.componentInstance.askQuestion = 'Why was this run blocked?';
    fixture.componentInstance.askInsights();
    fixture.componentInstance.askInsights();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="ask-submit"]').disabled).toBeTrue();
    expect(fixture.nativeElement.querySelector('[data-testid="ask-thinking"]')?.textContent).toContain('Thinking');
    expect(fixture.nativeElement.querySelector('[data-testid="ask-panel"]')).toBeTruthy();
    http.expectOne(`/api/projects/${sampleProject.id}/insights/ask`).flush(askInsightsResponse());
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="ask-submit"]').disabled).toBeFalse();
  });

  it('disables Ask while generate, run, or import is busy', () => {
    const fixture = openAsk(loadDashboard());
    fixture.componentInstance.generateTests();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="ask-submit"]').disabled).toBeTrue();
    http.expectOne(`/api/projects/${sampleProject.id}/tests/generate`).flush(generateResponse());
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="ask-submit"]').disabled).toBeFalse();
  });

  it('shows a friendly Ask error without crashing the dashboard', () => {
    const fixture = openAsk(loadDashboard());
    fixture.componentInstance.askQuestion = 'Why was this run blocked?';
    fixture.componentInstance.askInsights();
    http.expectOne(`/api/projects/${sampleProject.id}/insights/ask`).flush(
      { title: 'Invalid question', detail: 'Question is required.' },
      { status: 400, statusText: 'Bad Request' }
    );
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="ask-error"]')?.textContent).toContain(
      'Question is required.'
    );
    expect(fixture.nativeElement.querySelector('[data-testid="error"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid="decision"]')?.textContent).toContain(
      'Run tests to evaluate Safe / Review / Block'
    );
  });

  it('does not replace existing Decision, Coverage, Risk, or Findings with Ask evidence', () => {
    const fixture = openAsk(loadDashboard());
    fixture.componentInstance.runTests();
    http.expectOne(`/api/projects/${sampleProject.id}/tests/run`).flush(
      runResponse({ decision: 'Safe', findings: [] })
    );
    fixture.detectChanges();

    fixture.componentInstance.askQuestion = 'Why was this run blocked?';
    fixture.componentInstance.askInsights();
    http.expectOne(`/api/projects/${sampleProject.id}/insights/ask`).flush(
      askInsightsResponse({
        evidence: {
          decision: 'Block',
          lastRunCompletedAt: '2026-01-02T00:00:00+00:00',
          retrieved: ['lastRun.decision']
        }
      })
    );
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="decision"]')?.textContent).toContain('Safe');
    expect(fixture.nativeElement.querySelector('[data-testid="decision"]')?.textContent).not.toContain('Block');
    expect(fixture.componentInstance.decision).toBe('Safe');
    expect(fixture.componentInstance.coverage).toEqual(sampleCoverage);
    expect(fixture.componentInstance.risk).toEqual(sampleRisk);
    expect(fixture.componentInstance.findings).toEqual([]);
    expect(fixture.nativeElement.querySelector('[data-testid="ask-panel"]')?.textContent).toContain('Stored decision: Block');
  });

  it('keeps Ask history in memory only and bounds it to 10 turns', () => {
    const fixture = openAsk(loadDashboard());
    spyOn(localStorage, 'setItem');

    for (let i = 0; i < 11; i++) {
      const question = `Question ${i + 1}`;
      fixture.componentInstance.askQuestion = question;
      fixture.componentInstance.askInsights();
      http.expectOne(`/api/projects/${sampleProject.id}/insights/ask`).flush(
        askInsightsResponse({
          question,
          answer: `Answer ${i + 1}`
        })
      );
      fixture.detectChanges();
    }

    expect(localStorage.setItem).not.toHaveBeenCalled();
    expect(fixture.componentInstance.askHistory.map(turn => turn.question)).toEqual(
      Array.from({ length: 10 }, (_, index) => `Question ${index + 2}`)
    );
    expect(fixture.nativeElement.querySelector('[data-testid="ask-history"]')?.textContent).toContain('You: Question 11');
    expect(fixture.nativeElement.querySelector('[data-testid="ask-history"]')?.textContent).not.toContain('You: Question 1Answer');
  });

  it('does not change Decision, Risk, or Coverage when the assistant is opened', () => {
    const fixture = loadDashboard();
    fixture.componentInstance.runTests();
    http.expectOne(`/api/projects/${sampleProject.id}/tests/run`).flush(runResponse({ decision: 'Safe' }));
    fixture.detectChanges();

    openAsk(fixture);

    expect(fixture.componentInstance.decision).toBe('Safe');
    expect(fixture.componentInstance.coverage).toEqual(sampleCoverage);
    expect(fixture.componentInstance.risk).toEqual(sampleRisk);
    expect(fixture.nativeElement.querySelector('[data-testid="decision"]')?.textContent).toContain('Safe');
  });

  it('keeps the conversation after Close and shows it again when reopened', () => {
    const fixture = openAsk(loadDashboard());
    fixture.componentInstance.askQuestion = 'What tests failed?';
    fixture.componentInstance.askInsights();
    http.expectOne(`/api/projects/${sampleProject.id}/insights/ask`).flush(
      askInsightsResponse({ question: 'What tests failed?', answer: 'GET /orders failed.' })
    );
    fixture.detectChanges();

    (fixture.nativeElement.querySelector('[data-testid="ask-close"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="ask-panel"]')).toBeNull();
    expect(fixture.componentInstance.askHistory.length).toBe(1);

    openAsk(fixture);

    expect(fixture.nativeElement.querySelector('[data-testid="ask-history"]')?.textContent).toContain('You: What tests failed?');
    expect(fixture.nativeElement.querySelector('[data-testid="ask-answer"]')?.textContent).toContain('GET /orders failed.');
    expect(fixture.nativeElement.querySelector('[data-testid="ask-suggestion"]')).toBeNull();
  });

  it('clears only the in-memory chat when New Chat is clicked', () => {
    const fixture = openAsk(loadDashboard());
    fixture.componentInstance.runTests();
    http.expectOne(`/api/projects/${sampleProject.id}/tests/run`).flush(
      runResponse({ decision: 'Safe', findings: [] })
    );
    fixture.detectChanges();

    fixture.componentInstance.askQuestion = 'Explain the latest run';
    fixture.componentInstance.askInsights();
    http.expectOne(`/api/projects/${sampleProject.id}/insights/ask`).flush(
      askInsightsResponse({ question: 'Explain the latest run', answer: 'The run is Safe.' })
    );
    fixture.detectChanges();

    (fixture.nativeElement.querySelector('[data-testid="ask-new-chat"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(fixture.componentInstance.askHistory).toEqual([]);
    expect(fixture.nativeElement.querySelector('[data-testid="ask-history"]')).toBeNull();
    expect(fixture.nativeElement.querySelectorAll('[data-testid="ask-suggestion"]').length).toBe(5);
    expect(fixture.componentInstance.decision).toBe('Safe');
    expect(fixture.componentInstance.coverage).toEqual(sampleCoverage);
    expect(fixture.componentInstance.risk).toEqual(sampleRisk);
    expect(fixture.componentInstance.findings).toEqual([]);
    http.verify();
  });

  it('does not submit an empty question from the send button', () => {
    const fixture = openAsk(loadDashboard());
    (fixture.nativeElement.querySelector('[data-testid="ask-submit"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    http.expectNone(`/api/projects/${sampleProject.id}/insights/ask`);
    expect(fixture.nativeElement.querySelector('[data-testid="ask-validation"]')?.textContent).toContain(
      'Enter a question before asking.'
    );
  });

  it('does not force-scroll to the latest message after the user scrolls up', () => {
    const fixture = openAsk(loadDashboard());
    const body = fixture.nativeElement.querySelector('[data-testid="ask-body"]') as HTMLElement;
    Object.defineProperty(body, 'scrollHeight', { configurable: true, get: () => 400 });
    Object.defineProperty(body, 'clientHeight', { configurable: true, get: () => 100 });
    body.scrollTop = 12;
    fixture.componentInstance.onAskBodyScroll();

    fixture.componentInstance.askQuestion = 'What coverage gaps exist?';
    fixture.componentInstance.askInsights();

    expect((fixture.componentInstance as unknown as { askScrollAction: string | null }).askScrollAction).not.toBe(
      'bottom'
    );

    http.expectOne(`/api/projects/${sampleProject.id}/insights/ask`).flush(
      askInsightsResponse({ question: 'What coverage gaps exist?', answer: 'POST /pets is uncovered.' })
    );

    expect((fixture.componentInstance as unknown as { askScrollAction: string | null }).askScrollAction).not.toBe(
      'bottom'
    );
    fixture.detectChanges();
  });
});
