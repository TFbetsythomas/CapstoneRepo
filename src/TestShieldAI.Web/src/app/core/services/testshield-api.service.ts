import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AskInsightsResponse,
  GenerateTestsResponse,
  ImportSpecResponse,
  RunTestsResponse
} from '../models/testshield.models';

@Injectable({ providedIn: 'root' })
export class TestShieldApiService {
  constructor(private readonly http: HttpClient) {}

  importSpec(projectId: string, specification: string): Observable<ImportSpecResponse> {
    return this.http.post<ImportSpecResponse>(
      `/api/projects/${projectId}/specs/import`,
      { specification }
    );
  }

  generateTests(projectId: string): Observable<GenerateTestsResponse> {
    return this.http.post<GenerateTestsResponse>(
      `/api/projects/${projectId}/tests/generate`,
      {}
    );
  }

  runTests(projectId: string): Observable<RunTestsResponse> {
    return this.http.post<RunTestsResponse>(
      `/api/projects/${projectId}/tests/run`,
      {}
    );
  }

  askInsights(projectId: string, question: string): Observable<AskInsightsResponse> {
    return this.http.post<AskInsightsResponse>(
      `/api/projects/${projectId}/insights/ask`,
      { question }
    );
  }
}
