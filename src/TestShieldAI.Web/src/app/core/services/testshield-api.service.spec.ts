import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { askInsightsResponse, generateResponse, runResponse, sampleProject } from '../../testing/api-fixtures';
import { TestShieldApiService } from './testshield-api.service';

describe('TestShieldApiService', () => {
  let service: TestShieldApiService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(TestShieldApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('imports a specification', () => {
    service.importSpec(sampleProject.id, '{"openapi":"3.0.0"}').subscribe();

    const request = http.expectOne(`/api/projects/${sampleProject.id}/specs/import`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ specification: '{"openapi":"3.0.0"}' });
    request.flush({ operations: [] });
  });

  it('posts generate to the project tests endpoint', () => {
    service.generateTests(sampleProject.id).subscribe();

    const request = http.expectOne(`/api/projects/${sampleProject.id}/tests/generate`);
    expect(request.request.method).toBe('POST');
    request.flush(generateResponse());
  });

  it('posts run to the project tests endpoint', () => {
    service.runTests(sampleProject.id).subscribe();

    const request = http.expectOne(`/api/projects/${sampleProject.id}/tests/run`);
    expect(request.request.method).toBe('POST');
    request.flush(runResponse());
  });

  it('posts ask insights to the project-scoped endpoint with only the question', () => {
    service.askInsights(sampleProject.id, 'Why was this run blocked?').subscribe();

    const request = http.expectOne(`/api/projects/${sampleProject.id}/insights/ask`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ question: 'Why was this run blocked?' });
    expect(Object.keys(request.request.body)).toEqual(['question']);
    expect(request.request.url).not.toMatch(/openai|azure/i);
    request.flush(askInsightsResponse());
  });
});
