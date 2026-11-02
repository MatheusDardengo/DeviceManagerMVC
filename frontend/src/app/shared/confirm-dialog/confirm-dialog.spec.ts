import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HarnessLoader } from '@angular/cdk/testing';
import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { MatButtonHarness } from '@angular/material/button/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { ConfirmDialog, ConfirmDialogData } from './confirm-dialog';

describe('ConfirmDialog', () => {
  let fixture: ComponentFixture<ConfirmDialog>;
  let loader: HarnessLoader;
  const close = vi.fn<(confirmed: boolean) => void>();

  const data: ConfirmDialogData = {
    title: 'Delete device',
    message: 'Delete "iPhone 15" (Apple)? This cannot be undone.',
    confirmText: 'Delete',
  };

  beforeEach(async () => {
    close.mockReset();

    await TestBed.configureTestingModule({
      imports: [ConfirmDialog],
      providers: [
        { provide: MatDialogRef, useValue: { close } },
        { provide: MAT_DIALOG_DATA, useValue: data },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ConfirmDialog);
    loader = TestbedHarnessEnvironment.loader(fixture);
    await fixture.whenStable();
  });

  it('shows the title and the message', () => {
    const text = (fixture.nativeElement as HTMLElement).textContent;

    expect(text).toContain('Delete device');
    expect(text).toContain('Delete "iPhone 15" (Apple)? This cannot be undone.');
  });

  it('closes with true when confirmed', async () => {
    await (await loader.getHarness(MatButtonHarness.with({ text: 'Delete' }))).click();

    expect(close).toHaveBeenCalledWith(true);
  });

  it('closes with false when cancelled', async () => {
    await (await loader.getHarness(MatButtonHarness.with({ text: 'Cancel' }))).click();

    expect(close).toHaveBeenCalledWith(false);
  });
});
