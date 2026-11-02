import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Service } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { Device, DeviceFilters, DeviceRequest } from './device.model';

@Service()
export class DeviceService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/devices';

  list(filters: DeviceFilters = {}): Promise<Device[]> {
    let params = new HttpParams();
    const brand = filters.brand?.trim();
    if (brand) {
      params = params.set('brand', brand);
    }
    if (filters.state) {
      params = params.set('state', filters.state);
    }

    return firstValueFrom(this.http.get<Device[]>(this.baseUrl, { params }));
  }

  create(request: DeviceRequest): Promise<Device> {
    return firstValueFrom(this.http.post<Device>(this.baseUrl, request));
  }

  update(id: number, request: DeviceRequest): Promise<Device> {
    return firstValueFrom(this.http.put<Device>(`${this.baseUrl}/${id}`, request));
  }

  delete(id: number): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.baseUrl}/${id}`));
  }
}
