import { TestBed } from '@angular/core/testing';

import { TicketStatus } from '../../core/api/models';
import { PriorityMeter } from './priority-meter';
import { StatusTag } from './status-tag';

describe('StatusTag', () => {
  it.each<[TicketStatus, string]>([
    ['Open', 'Open'],
    ['InProgress', 'In progress'],
    ['WaitingForInformation', 'Waiting for info'],
    ['Resolved', 'Resolved'],
    ['Closed', 'Closed'],
  ])('shows %s as readable text plus a shape (never colour alone)', async (status, label) => {
    const fixture = TestBed.createComponent(StatusTag);
    fixture.componentRef.setInput('status', status);
    await fixture.whenStable();

    const tag: HTMLElement = fixture.nativeElement.querySelector('.tag');
    expect(tag.textContent?.trim()).toBe(label);
    expect(tag.querySelector('.glyph')).not.toBeNull();
  });
});

describe('PriorityMeter', () => {
  it('fills one bar per priority level and keeps the word for screen readers when compact', async () => {
    const fixture = TestBed.createComponent(PriorityMeter);
    fixture.componentRef.setInput('priority', 'High');
    fixture.componentRef.setInput('compact', true);
    await fixture.whenStable();

    const root: HTMLElement = fixture.nativeElement;
    expect(root.querySelectorAll('.bar.filled').length).toBe(3);
    expect(root.querySelector('.sr-only')?.textContent).toBe('High');
  });
});
