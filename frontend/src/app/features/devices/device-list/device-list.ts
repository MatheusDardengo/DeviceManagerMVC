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
  ],
  selector: 'app-device-list',
  styleUrl: './device-list.scss',
  templateUrl: './device-list.html',
})
export class DeviceList implements OnInit {
  private readonly deviceService = inject(DeviceService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly states = DEVICE_STATES;
  protected readonly stateLabels = DEVICE_STATE_LABELS;
  protected readonly columns = ['name', 'brand', 'state', 'updatedAtUtc', 'createdAtUtc'];

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
}
