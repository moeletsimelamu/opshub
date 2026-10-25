import { HttpErrorResponse } from '@angular/common/http';
import { FormControl, FormGroup } from '@angular/forms';

import { ApiError } from './api-error';

function httpError(status: number, body: unknown = null): HttpErrorResponse {
  return new HttpErrorResponse({ status, error: body });
}

describe('ApiError', () => {
  it('reads the fields of an RFC 9457 ProblemDetails body', () => {
    const error = ApiError.from(
      httpError(409, { title: 'Conflict', status: 409, detail: 'Closed tickets cannot be changed.', code: 'ticket_closed', traceId: '00-abc' }),
    );

    expect(error.status).toBe(409);
    expect(error.code).toBe('ticket_closed');
    expect(error.message).toBe('Closed tickets cannot be changed.');
    expect(error.traceId).toBe('00-abc');
    expect(error.isUnreachable).toBe(false);
  });

  it('treats no response at all (status 0) as unreachable', () => {
    const error = ApiError.from(httpError(0));

    expect(error.isUnreachable).toBe(true);
    expect(error.message).toContain('can’t reach the server');
  });

  it.each([502, 503, 504])('treats gateway status %i as unreachable', (status) => {
    expect(ApiError.from(httpError(status)).isUnreachable).toBe(true);
  });

  it('treats a bare 500 (no ProblemDetails, e.g. from the dev proxy) as unreachable', () => {
    expect(ApiError.from(httpError(500, '')).isUnreachable).toBe(true);
  });

  it('does NOT treat the API’s own 500 ProblemDetails as unreachable', () => {
    const error = ApiError.from(httpError(500, { title: 'An error occurred', status: 500, traceId: '00-x' }));

    expect(error.isUnreachable).toBe(false);
    expect(error.message).toBe('An error occurred');
  });

  it('copies field errors onto matching form controls', () => {
    const form = new FormGroup({ email: new FormControl('taken@marula.example'), name: new FormControl('Ok') });
    const error = ApiError.from(httpError(400, { title: 'Invalid', status: 400, errors: { email: ['Already in use.'] } }));

    const applied = error.applyTo(form);

    expect(applied).toBe(true);
    expect(form.controls.email.errors).toEqual({ server: 'Already in use.' });
    expect(form.controls.email.touched).toBe(true);
    expect(form.controls.name.errors).toBeNull();
  });

  it('reports false when no field error matches the form', () => {
    const form = new FormGroup({ title: new FormControl('') });
    const error = ApiError.from(httpError(409, { title: 'Conflict', status: 409, code: 'concurrency_conflict' }));

    expect(error.applyTo(form)).toBe(false);
  });
});
