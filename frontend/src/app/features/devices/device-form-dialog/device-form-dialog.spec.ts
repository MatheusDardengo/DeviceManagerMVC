import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { HarnessLoader } from '@angular/cdk/testing';
import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { MatButtonHarness } from '@angular/material/button/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MatInputHarness } from '@angular/material/input/testing';
import { MatSelectHarness } from '@angular/material/select/testing';
import { Device, DeviceRequest } from '../device.model';
import { DeviceService } from '../device.service';
import { DeviceFormDialog, DeviceFormDialogData } from './device-form-dialog';

describe('DeviceFormDialog', () => {
  let fixture: ComponentFixture<DeviceFormDialog>;
  let loader: HarnessLoader;

  const create = vi.fn<(request: DeviceRequest) => Promise<Device>>();
  const update = vi.fn<(id: number, request: DeviceRequest) => Promise<Device>>();
  const close = vi.fn<(result?: Device) => void>();

  const existing: Device = {
    id: 7,
    name: 'iPhone 15',
    brand: 'Apple',
    state: 'InUse',
    createdAtUtc: '2026-01-10T09:00:00Z',
    updatedAtUtc: '2026-01-10T09:00:00Z',
  };

  async function open(data: DeviceFormDialogData): Promise<void> {
    create.mockReset();
    update.mockReset();
    close.mockReset();

    await TestBed.configureTestingModule({
      imports: [DeviceFormDialog],
      providers: [
        { provide: DeviceService, useValue: { create, update } },
        { provide: MatDialogRef, useValue: { close } },
        { provide: MAT_DIALOG_DATA, useValue: data },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(DeviceFormDialog);
    loader = TestbedHarnessEnvironment.loader(fixture);
    await fixture.whenStable();
  }

  async function settle(): Promise<void> {
    await new Promise((resolve) => setTimeout(resolve));
    await fixture.whenStable();
  }

  const field = (name: 'name' | 'brand') =>
    loader.getHarness(MatInputHarness.with({ selector: `[formControlName="${name}"]` }));

  async function click(text: string): Promise<void> {
    await (await loader.getHarness(MatButtonHarness.with({ text }))).click();
    await settle();
  }

  const page = () => fixture.nativeElement as HTMLElement;

  it('starts empty in create mode, with state Available', async () => {
    await open({});

    expect(page().textContent).toContain('Add device');
    expect(await (await field('name')).getValue()).toBe('');
    expect(await (await loader.getHarness(MatSelectHarness)).getValueText()).toBe('Available');
  });

  it('creates the device and closes with it', async () => {
    await open({});
    const created = { ...existing, id: 8, name: 'Pixel 9', brand: 'Google', state: 'Available' as const };
    create.mockResolvedValue(created);
    await (await field('name')).setValue('Pixel 9');
    await (await field('brand')).setValue('Google');

    await click('Save');

    expect(create).toHaveBeenCalledWith({ name: 'Pixel 9', brand: 'Google', state: 'Available' });
    expect(close).toHaveBeenCalledWith(created);
  });

  it('starts filled with the device in edit mode', async () => {
    await open({ device: existing });

    expect(page().textContent).toContain('Edit device');
    expect(await (await field('name')).getValue()).toBe('iPhone 15');
    expect(await (await field('brand')).getValue()).toBe('Apple');
  });

  it('updates the device and closes with it', async () => {
    await open({ device: existing });
    update.mockResolvedValue({ ...existing, state: 'Available' });
    const state = await loader.getHarness(MatSelectHarness);
    await state.open();
    await state.clickOptions({ text: 'Available' });

    await click('Save');

    expect(update).toHaveBeenCalledWith(7, { name: 'iPhone 15', brand: 'Apple', state: 'Available' });
    expect(close).toHaveBeenCalled();
  });

  it('does not save an invalid form and shows the field error', async () => {
    await open({ device: existing });
    await (await field('name')).setValue('');

    await click('Save');

    expect(update).not.toHaveBeenCalled();
    expect(page().textContent).toContain('Name is required.');
  });

  it('shows the API message and stays open when the save is rejected', async () => {
    await open({ device: existing });
    update.mockRejectedValue(
      new HttpErrorResponse({
        status: 409,
        error: { detail: 'Name and brand cannot be changed while the device is in use.' },
      }),
    );
    await (await field('name')).setValue('iPhone 16');

    await click('Save');

    expect(page().querySelector('[role="alert"]')?.textContent).toContain(
      'Name and brand cannot be changed while the device is in use.',
    );
    expect(close).not.toHaveBeenCalled();
  });

  it('cancel closes without a result', async () => {
    await open({});

    await click('Cancel');

    expect(close).toHaveBeenCalledWith();
  });
});
