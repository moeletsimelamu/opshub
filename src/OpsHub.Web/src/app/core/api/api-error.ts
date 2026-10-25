import { HttpErrorResponse } from '@angular/common/http';
import { AbstractControl, FormGroup } from '@angular/forms';

/**
 * A failed API call in one predictable shape. The API returns RFC 9457 ProblemDetails
 * (title, detail, status, traceId, and for business rules a stable `code`; for
 * validation an `errors` map keyed by JSON field name).
 */
export class ApiError {
  constructor(
    readonly status: number,
    readonly title: string,
    readonly detail: string | null,
    readonly code: string | null,
    readonly fieldErrors: Record<string, string[]>,
    readonly traceId: string | null,
    /** True when the body was the API's own ProblemDetails (it always sends one for errors). */
    readonly fromApi = true,
  ) {}

  /**
   * The request never reached a working API: no network (status 0), a gateway status
   * (502/503/504), or any 5xx without the API's ProblemDetails body, which means something
   * in front of the API answered instead (e.g. the dev proxy returns a bare 500 when the
   * API is stopped).
   */
  get isUnreachable(): boolean {
    return (
      this.status === 0 ||
      this.status === 502 ||
      this.status === 503 ||
      this.status === 504 ||
      (this.status >= 500 && !this.fromApi)
    );
  }

  /** A sentence that is safe and useful to show to the user. */
  get message(): string {
    if (this.isUnreachable) {
      return 'OpsHub can’t reach the server right now. Check your connection (or that the API is running) and try again.';
    }
    return this.detail ?? this.title;
  }

  static from(error: unknown): ApiError {
    if (error instanceof ApiError) {
      return error;
    }

    if (error instanceof HttpErrorResponse) {
      const body = (typeof error.error === 'object' && error.error !== null ? error.error : {}) as Record<string, unknown>;
      return new ApiError(
        error.status,
        typeof body['title'] === 'string' ? body['title'] : statusTitle(error.status),
        typeof body['detail'] === 'string' ? body['detail'] : null,
        typeof body['code'] === 'string' ? body['code'] : null,
        isFieldErrors(body['errors']) ? body['errors'] : {},
        typeof body['traceId'] === 'string' ? body['traceId'] : null,
        typeof body['title'] === 'string' && typeof body['status'] === 'number',
      );
    }

    return new ApiError(-1, 'Something went wrong', String(error), null, {}, null);
  }

  /**
   * Copies server-side field errors onto the matching form controls (as `server`
   * errors), so they appear next to the right input. Returns true if any were applied.
   */
  applyTo(form: FormGroup): boolean {
    let applied = false;
    for (const [field, messages] of Object.entries(this.fieldErrors)) {
      const control: AbstractControl | null = form.get(field);
      if (control && messages.length > 0) {
        control.setErrors({ ...control.errors, server: messages[0] });
        control.markAsTouched();
        applied = true;
      }
    }
    return applied;
  }
}

function isFieldErrors(value: unknown): value is Record<string, string[]> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function statusTitle(status: number): string {
  // Called only when the body wasn't the API's ProblemDetails, so a 5xx here means the
  // response came from something in front of the API.
  if (status >= 500) {
    return 'Server unreachable';
  }

  switch (status) {
    case 0:
      return 'Server unreachable';
    case 401:
      return 'Please sign in';
    case 403:
      return 'Not allowed';
    case 404:
      return 'Not found';
    case 409:
      return 'Conflict';
    default:
      return 'Request failed';
  }
}
