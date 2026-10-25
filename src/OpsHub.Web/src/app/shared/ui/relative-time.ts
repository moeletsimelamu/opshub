import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

const UNITS: [Intl.RelativeTimeFormatUnit, number][] = [
  ['year', 365 * 24 * 3600],
  ['month', 30 * 24 * 3600],
  ['week', 7 * 24 * 3600],
  ['day', 24 * 3600],
  ['hour', 3600],
  ['minute', 60],
];

const relative = new Intl.RelativeTimeFormat(undefined, { numeric: 'auto', style: 'short' });
const absolute = new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' });
const dateOnly = new Intl.DateTimeFormat(undefined, { dateStyle: 'medium' });

export function formatRelative(iso: string, now = Date.now()): string {
  const seconds = Math.round((new Date(iso).getTime() - now) / 1000);
  for (const [unit, size] of UNITS) {
    if (Math.abs(seconds) >= size) {
      return relative.format(Math.round(seconds / size), unit);
    }
  }
  return 'just now';
}

export function formatAbsolute(iso: string): string {
  return absolute.format(new Date(iso));
}

/**
 * "3 hr. ago", with the exact date and time in a <time> element (and tooltip),
 * in the viewer's own time zone and locale. The API sends UTC.
 */
@Component({
  selector: 'app-relative-time',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<time class="tabular" [attr.datetime]="value()" [title]="exact()">{{ text() }}</time>`,
})
export class RelativeTime {
  readonly value = input.required<string>();
  /** Show the exact date and time instead of "2 days ago". */
  readonly exactOnly = input(false);
  /** Show just the date (e.g. when an account was added), where the time is noise. */
  readonly dateOnly = input(false);

  protected readonly exact = computed(() => formatAbsolute(this.value()));
  protected readonly text = computed(() => {
    if (this.dateOnly()) return dateOnly.format(new Date(this.value()));
    return this.exactOnly() ? this.exact() : formatRelative(this.value());
  });
}
