import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';

import { CategoriesApi } from '../../core/api/categories-api';
import { CategoryAdmin } from '../../core/api/models';
import { CategoryDialog, CategoryDialogData } from './category-dialog';

const printers: CategoryAdmin = { id: 6, name: 'Printers', isActive: true, ticketCount: 2 };

/** The add/edit category dialog: validation, the API call it makes, and how it shows API errors. */
describe('CategoryDialog', () => {
  let fixture: ComponentFixture<CategoryDialog>;
  const api = { create: vi.fn(), update: vi.fn() };
  const dialogRef = { close: vi.fn() };

  async function open(data: CategoryDialogData): Promise<void> {
    api.create.mockReset();
    api.update.mockReset();
    dialogRef.close.mockReset();
    TestBed.configureTestingModule({
      providers: [
        { provide: CategoriesApi, useValue: api },
        { provide: DialogRef, useValue: dialogRef },
        { provide: DIALOG_DATA, useValue: data },
      ],
    });
    fixture = TestBed.createComponent(CategoryDialog);
    await fixture.whenStable();
  }

  const root = (): HTMLElement => fixture.nativeElement;

  async function setName(value: string): Promise<void> {
    const input = root().querySelector<HTMLInputElement>('#category-name')!;
    input.value = value;
    input.dispatchEvent(new Event('input'));
    await fixture.whenStable();
  }

  async function submit(): Promise<void> {
    root().querySelector<HTMLFormElement>('form')!.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    await new Promise((r) => setTimeout(r)); // let the API call settle, then re-render
    await fixture.whenStable();
  }

  it('treats a name of only spaces as empty and doesn’t call the API', async () => {
    await open({ category: null });

    await setName('   ');
    await submit();

    expect(api.create).not.toHaveBeenCalled();
    expect(root().querySelector('#category-name-error')?.textContent?.trim()).toBe('Name is required.');
  });

  it('creates the category with a trimmed name and closes with it', async () => {
    await open({ category: null });
    api.create.mockReturnValue(of(printers));

    await setName('  Printers  ');
    await submit();

    expect(api.create).toHaveBeenCalledWith('Printers');
    expect(dialogRef.close).toHaveBeenCalledWith(printers);
  });

  it('shows a duplicate name on the name field, and stays open', async () => {
    await open({ category: null });
    api.create.mockReturnValue(throwError(() => new HttpErrorResponse({
      status: 409,
      error: { title: 'Conflict', status: 409, code: 'category_name_in_use', detail: 'A category with this name already exists.' },
    })));

    await setName('network');
    await submit();

    expect(root().querySelector('#category-name-error')?.textContent?.trim()).toBe('A category with this name already exists.');
    expect(dialogRef.close).not.toHaveBeenCalled();
  });

  it('shows the last-active-category rule as a message, when deactivating', async () => {
    await open({ category: printers });
    api.update.mockReturnValue(throwError(() => new HttpErrorResponse({
      status: 409,
      error: { title: 'Conflict', status: 409, code: 'last_active_category', detail: 'At least one category must stay active.' },
    })));

    root().querySelector<HTMLInputElement>('input[type=checkbox]')!.click();
    await submit();

    expect(api.update).toHaveBeenCalledWith(6, { name: 'Printers', isActive: false });
    expect(root().querySelector('[role=alert]')?.textContent).toContain('At least one category must stay active.');
    expect(dialogRef.close).not.toHaveBeenCalled();
  });
});
