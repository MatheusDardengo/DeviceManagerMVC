export type DeviceState = 'Available' | 'InUse' | 'Inactive';

export const DEVICE_STATES: readonly DeviceState[] = ['Available', 'InUse', 'Inactive'];

export interface Device {
  id: number;
  name: string;
  brand: string;
  state: DeviceState;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface DeviceRequest {
  name: string;
  brand: string;
  state: DeviceState;
}

export interface DeviceFilters {
  brand?: string;
  state?: DeviceState;
}
