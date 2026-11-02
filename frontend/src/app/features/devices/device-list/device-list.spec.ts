import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HarnessLoader } from '@angular/cdk/testing';
import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { MatButtonHarness } from '@angular/material/button/testing';
import { MatDialog } from '@angular/material/dialog';
import { MatInputHarness } from '@angular/material/input/testing';
import { MatSelectHarness } from '@angular/material/select/testing';
import { MatTableHarness } from '@angular/material/table/testing';
import { provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { DeviceFormDialog } from '../device-form-dialog/device-form-dialog';
import { Device, DeviceFilters, DeviceState } from '../device.model';
import { DeviceService } from '../device.service';
import { DeviceList } from './device-list';
import { HttpErrorResponse } from '@angular/common/http';
import { ConfirmDialog } from '../../../shared/confirm-dialog/confirm-dialog';


describe('DeviceList', () => {
  let fixture: ComponentFixture<DeviceList>;
  let loader: HarnessLoader;
  let router: Router;

  const list = vi.fn<(filters?: DeviceFilters) => Promise<Device[]>>();
  const deleteDevice = vi.fn<(id: number) => Promise<void>>();
  const logout = vi.fn<() => Promise<void>>();
  const open = vi.fn();

  const device = (id: number, name: string, brand: string, state: DeviceState): Device => ({
    id,
    name,
    brand,
    state,
    createdAtUtc: '2026-01-10T09:00:00Z',
    updatedAtUtc: '2026-01-10T09:00:00Z',
  });

  /** Makes the next dialog close with this result (undefined = cancelled). */
  const dialogClosesWith = (result: unknown) => open.mockReturnValue({ afterClosed: () => of(result) });

  // Lets the async work finish, then lets Angular re-render.
  async function settle(): Promise<void> {
    await new Promise((resolve) => setTimeout(resolve));
    await fixture.whenStable();
  }

  async function click(text: string): Promise<void> {
    await (await loader.getHarness(MatButtonHarness.with({ text }))).click();
    await settle();
  }

  const page = () => fixture.nativeElement as HTMLElement;

  beforeEach(async () => {
    list.mockReset().mockResolvedValue([
      device(1, 'iPhone 15', 'Apple', 'InUse'),
      device(2, 'Galaxy S24', 'Samsung', 'Available'),
    ]);
    logout.mockReset().mockResolvedValue();
    open.mockReset();
    dialogClosesWith(undefined);

    await TestBed.configureTestingModule({
      imports: [DeviceList],
      providers: [
        provideRouter([]),
        { provide: DeviceService, useValue: { list, delete: deleteDevice } },
        { provide: AuthService, useValue: { logout } },
        { provide: MatDialog, useValue: { open } },
      ],
    }).compileComponents();

    router = TestBed.inject(Router);
    vi.spyOn(router, 'navigate').mockResolvedValue(true);

    fixture = TestBed.createComponent(DeviceList);
    loader = TestbedHarnessEnvironment.loader(fixture);
    await settle();

    deleteDevice.mockReset().mockResolvedValue();
  });

  it('loads every device on start and shows them in the table', async () => {
    const table = await loader.getHarness(MatTableHarness);
    const columns = await table.getCellTextByColumnName();

    expect(list).toHaveBeenCalledTimes(1);
    expect(columns['name'].text).toEqual(['iPhone 15', 'Galaxy S24']);
    expect(columns['brand'].text).toEqual(['Apple', 'Samsung']);
    expect(columns['state'].text).toEqual(['In use', 'Available']);
  });

  it('filters by brand and state', async () => {
    await (await loader.getHarness(MatInputHarness)).setValue('Apple');
    const state = await loader.getHarness(MatSelectHarness);
    await state.open();
    await state.clickOptions({ text: 'In use' });

    await click('Filter');

    expect(list).toHaveBeenLastCalledWith({ brand: 'Apple', state: 'InUse' });
  });

  it('clear resets the filters and reloads everything', async () => {
    const brand = await loader.getHarness(MatInputHarness);
    await brand.setValue('Apple');

    await click('Clear');

    expect(await brand.getValue()).toBe('');
    expect(list).toHaveBeenLastCalledWith({ brand: '', state: undefined });
  });

  it('shows a message when no device matches', async () => {
    list.mockResolvedValue([]);

    await click('Filter');

    expect(page().textContent).toContain('No devices found.');
  });

  it('shows an error when the devices cannot be loaded', async () => {
    list.mockRejectedValue(new Error('network'));

    await click('Filter');

    expect(page().querySelector('[role="alert"]')?.textContent).toContain('Could not load devices');
  });

  it('logs out and goes to /login', async () => {
    await click('Log out');

    expect(logout).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['/login']);
  });

  it('Add device opens the form in create mode and reloads after saving', async () => {
    dialogClosesWith(device(3, 'Pixel 9', 'Google', 'Available'));

    await click('Add device');

    expect(open).toHaveBeenCalledWith(DeviceFormDialog, { data: { device: undefined } });
    expect(list).toHaveBeenCalledTimes(2);
  });

  it('Edit opens the form with that row and does not reload when cancelled', async () => {
    const [firstEdit] = await loader.getAllHarnesses(
      MatButtonHarness.with({ selector: '[aria-label="Edit device"]' }),
    );

    await firstEdit.click();
    await settle();

    expect(open).toHaveBeenCalledWith(DeviceFormDialog, {
      data: { device: expect.objectContaining({ id: 1, name: 'iPhone 15' }) },
    });
    expect(list).toHaveBeenCalledTimes(1);
  });

    async function clickFirstDelete(): Promise<void> {
    const [firstDelete] = await loader.getAllHarnesses(
      MatButtonHarness.with({ selector: '[aria-label="Delete device"]' }),
    );
    await firstDelete.click();
    await settle();
  }

  it('Delete asks for confirmation, deletes the row and reloads', async () => {
    dialogClosesWith(true);

    await clickFirstDelete();

    expect(open).toHaveBeenCalledWith(
      ConfirmDialog,
      expect.objectContaining({ data: expect.objectContaining({ confirmText: 'Delete' }) }),
    );
    expect(deleteDevice).toHaveBeenCalledWith(1);
    expect(list).toHaveBeenCalledTimes(2);
  });

  it('Delete does nothing when the confirmation is cancelled', async () => {
    dialogClosesWith(false);

    await clickFirstDelete();

    expect(deleteDevice).not.toHaveBeenCalled();
    expect(list).toHaveBeenCalledTimes(1);
  });

  it('Delete shows the API message when the device is in use', async () => {
    dialogClosesWith(true);
    deleteDevice.mockRejectedValue(
      new HttpErrorResponse({ status: 409, error: { detail: 'A device in use cannot be deleted.' } }),
    );

    await clickFirstDelete();

    expect(page().querySelector('[role="alert"]')?.textContent).toContain('A device in use cannot be deleted.');
  });

});
