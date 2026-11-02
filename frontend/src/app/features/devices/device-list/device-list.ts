import { DatePipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatToolbarModule } from '@angular/material/toolbar';
import { Router } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';
import { Device, DEVICE_STATE_LABELS, DEVICE_STATES, DeviceState } from '../device.model';
import { DeviceService } from '../device.service';
import { MatDialog } from '@angular/material/dialog';
import { firstValueFrom } from 'rxjs';
import { DeviceFormDialog, DeviceFormDialogData } from '../device-form-dialog/device-form-dialog';
import {MatIconModule} from '@angular/material/icon';
import { problemMessage } from '../../../core/http/problem-details';
import { ConfirmDialog, ConfirmDialogData } from '../../../shared/confirm-dialog/confirm-dialog';

@Component({
  imports: [
    DatePipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    MatTableModule,
    MatToolbarModule,
    MatIconModule,
  ],
  selector: 'app-device-list',
  styleUrl: './device-list.scss',
  templateUrl: './device-list.html',
})
export class DeviceList implements OnInit {
  private readonly deviceService = inject(DeviceService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

   private readonly dialog = inject(MatDialog);

  protected readonly states = DEVICE_STATES;

  protected stateLabel(state: DeviceState): string {
    return DEVICE_STATE_LABELS[state];
  }

  protected readonly columns = ['name', 'brand', 'state', 'updatedAtUtc', 'createdAtUtc', 'actions'];

  protected readonly filters = inject(NonNullableFormBuilder).group({
    brand: '',
    state: '' as DeviceState | '',
  });

  protected readonly devices = signal<Device[]>([]);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  ngOnInit(): void {
    void this.load();
  }

  protected async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    const { brand, state } = this.filters.getRawValue();

    try {
      this.devices.set(await this.deviceService.list({ brand, state: state || undefined }));
    } catch {
      this.error.set('Could not load devices. Please try again.');
    } finally {
      this.loading.set(false);
    }
  }

  protected async clearFilters(): Promise<void> {
    this.filters.reset();
    await this.load();
  }

  protected async logout(): Promise<void> {
    await this.auth.logout();
    await this.router.navigate(['/login']);
  }

  protected async openForm(device?: Device): Promise<void> {
    const saved = await firstValueFrom(
      this.dialog
        .open<DeviceFormDialog, DeviceFormDialogData, Device>(DeviceFormDialog, { data: { device } })
        .afterClosed(),
    );

    if (saved) {
      await this.load();
    }
  }

  protected async remove(device: Device): Promise<void> {
    const confirmed = await firstValueFrom(
      this.dialog
        .open<ConfirmDialog, ConfirmDialogData, boolean>(ConfirmDialog, {
          data: {
            title: 'Delete device',
            message: `Delete "${device.name}" (${device.brand})? This cannot be undone.`,
            confirmText: 'Delete',
          },
        })
        .afterClosed(),
    );

    if (!confirmed) {
      return;
    }

    try {
      await this.deviceService.delete(device.id);
      await this.load();
    } catch (error) {
      // e.g. 409 "A device in use cannot be deleted." comes straight from the API.
      this.error.set(problemMessage(error, 'Could not delete the device. Please try again.'));
    }
  }
}
