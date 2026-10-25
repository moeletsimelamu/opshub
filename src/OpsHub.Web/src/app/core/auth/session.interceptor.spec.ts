import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';

import { sessionInterceptor } from './session.interceptor';
import { SessionService } from './session.service';

describe('sessionInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;
  const handleSessionExpired = vi.fn();

  beforeEach(() => {
    handleSessionExpired.mockReset();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([sessionInterceptor])),
        provideHttpClientTesting(),
        { provide: SessionService, useValue: { handleSessionExpired } },
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  async function request(url: string, status: number): Promise<void> {
    const result = firstValueFrom(http.get(url)).catch(() => undefined);
    backend.expectOne(url).flush({ title: 'x', status }, { status, statusText: 'x' });
    await result;
  }

  it('treats a 401 from an ordinary API call as an ended session', async () => {
    await request('/api/tickets', 401);

    expect(handleSessionExpired).toHaveBeenCalledTimes(1);
  });

  it.each(['/api/auth/login', '/api/auth/me'])('ignores the expected 401 from %s', async (url) => {
    await request(url, 401);

    expect(handleSessionExpired).not.toHaveBeenCalled();
  });

  it('ignores other errors such as 403 and 409', async () => {
    await request('/api/users', 403);
    await request('/api/tickets/1', 409);

    expect(handleSessionExpired).not.toHaveBeenCalled();
  });
});
