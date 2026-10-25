import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

import { FieldError, isInvalid } from '../../shared/ui/field-error';

export interface StatusDialogData {
  title: string;
  description: string;
  noteLabel: string;
  noteHint?: string;
  noteRequired: boolean;
  confirmLabel: string;
}

/** Asks for the note that goes with a status change. Closes with the note, or undefined if cancelled. */
@Component({
  selector: 'app-status-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, FieldError],
  styleUrl: '../../shared/ui/dialog.css',
  template: `
    <div class="dialog-header">
      <h2 id="status-dialog-title">{{ data.title }}</h2>
      <p class="dialog-description">{{ data.description }}</p>
    </div>

    <form [formGroup]="form" (ngSubmit)="confirm()" novalidate>
      <div class="field">
        <label class="field-label" for="status-note">
          {{ data.noteLabel }}
          @if (!data.noteRequired) {
            <span class="field-optional">(optional)</span>
          }
        </label>
        <textarea
          id="status-note"
          class="textarea"
          formControlName="note"
          rows="4"
          maxlength="500"
          cdkFocusInitial
          [attr.aria-invalid]="isInvalid(form.controls.note)"
          aria-describedby="status-note-hint status-note-error"
        ></textarea>
        @if (data.noteHint) {
          <p class="field-hint" id="status-note-hint">{{ data.noteHint }}</p>
        }
        <app-field-error [control]="form.controls.note" errorId="status-note-error" label="A note" />
      </div>

      <div class="dialog-actions">
        <button type="button" class="btn btn-secondary" (click)="dialogRef.close()">Cancel</button>
        <button type="submit" class="btn btn-primary">{{ data.confirmLabel }}</button>
      </div>
    </form>
  `,
})
export class StatusDialog {
  protected readonly data = inject<StatusDialogData>(DIALOG_DATA);
  protected readonly dialogRef = inject<DialogRef<string>>(DialogRef);
  protected readonly isInvalid = isInvalid;

  protected readonly form = inject(NonNullableFormBuilder).group({
    note: ['', this.data.noteRequired ? [Validators.required, Validators.maxLength(500)] : [Validators.maxLength(500)]],
  });

  protected confirm(): void {
    this.form.markAllAsTouched();
    if (this.form.valid) {
      this.dialogRef.close(this.form.controls.note.value.trim());
    }
  }
}
