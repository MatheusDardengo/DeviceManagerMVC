import { Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { problemMessage } from '../../../core/http/problem-details';
import { Device, DEVICE_STATE_LABELS, DEVICE_STATES, DeviceState } from '../device.model';
import { DeviceService } from '../device.service';

/** Data passed when opening the dialog: no device means "create". */
export interface DeviceFormDialogData {
  device?: Device;
}

/** Same limit as Device.NameMaxLength / BrandMaxLength in the API. */
const MAX_LENGTH = 100;

/** Creates or edits a device. Closes with the saved device, or with nothing when cancelled. */
@Component({
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule],
  selector: 'app-device-form-dialog',
  styleUrl: './device-form-dialog.scss',
  templateUrl: './device-form-dialog.html',
})
export class DeviceFormDialog {
  private readonly deviceService = inject(DeviceService);
  private readonly dialogRef = inject<MatDialogRef<DeviceFormDialog, Device>>(MatDialogRef);
  private readonly device = inject<DeviceFormDialogData>(MAT_DIALOG_DATA).device;

  protected readonly isEdit = this.device !== undefined;
  protected readonly states = DEVICE_STATES;
  protected readonly maxLength = MAX_LENGTH;

  protected readonly form = inject(NonNullableFormBuilder).group({
    name: [this.device?.name ?? '', [Validators.required, Validators.maxLength(MAX_LENGTH)]],
    brand: [this.device?.brand ?? '', [Validators.required, Validators.maxLength(MAX_LENGTH)]],
    state: [this.device?.state ?? ('Available' as DeviceState)],
  });

  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  protected stateLabel(state: DeviceState): string {
    return DEVICE_STATE_LABELS[state];
  }

  protected async save(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    const request = this.form.getRawValue();

    try {
      const saved = this.device
        ? await this.deviceService.update(this.device.id, request)
        : await this.deviceService.create(request);
      this.dialogRef.close(saved);
    } catch (error) {
      this.error.set(problemMessage(error, 'Could not save the device. Please try again.'));
    } finally {
      this.saving.set(false);
    }
  }

  protected cancel(): void {
    this.dialogRef.close();
  }
}
