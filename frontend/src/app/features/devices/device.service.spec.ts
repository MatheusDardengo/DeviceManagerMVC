import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Device, DeviceRequest } from './device.model';
import { DeviceService } from './device.service';

describe('DeviceService', () => {
  let service: DeviceService;
  let http: HttpTestingController;

  const device: Device = {
    id: 7,
    name: 'iPhone 15',
    brand: 'Apple',
    state: 'Available',
    createdAtUtc: '2026-01-10T09:00:00Z',
    updatedAtUtc: '2026-01-10T09:00:00Z',
  };
  const request: DeviceRequest = { name: 'iPhone 15', brand: 'Apple', state: 'Available' };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(DeviceService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('list without filters sends no query parameters', async () => {
    const list = service.list();

    const call = http.expectOne((r) => r.url === '/api/devices');
    expect(call.request.method).toBe('GET');
    expect(call.request.params.keys()).toEqual([]);
    call.flush([device]);

    expect(await list).toEqual([device]);
  });

  it('list sends brand (trimmed) and state as query parameters', async () => {
    const list = service.list({ brand: '  Apple ', state: 'InUse' });

    const call = http.expectOne((r) => r.url === '/api/devices');
    expect(call.request.params.get('brand')).toBe('Apple');
    expect(call.request.params.get('state')).toBe('InUse');
    call.flush([]);
    await list;
  });

  it('list ignores a blank brand', async () => {
    const list = service.list({ brand: '   ' });

    const call = http.expectOne((r) => r.url === '/api/devices');
    expect(call.request.params.has('brand')).toBe(false);
    call.flush([]);
    await list;
  });

  it('create posts the request and returns the created device', async () => {
    const create = service.create(request);

    const call = http.expectOne('/api/devices');
    expect(call.request.method).toBe('POST');
    expect(call.request.body).toEqual(request);
    call.flush(device);

    expect(await create).toEqual(device);
  });

  it('update puts the request to the device url', async () => {
    const update = service.update(7, request);

    const call = http.expectOne('/api/devices/7');
    expect(call.request.method).toBe('PUT');
    expect(call.request.body).toEqual(request);
    call.flush(device);

    expect(await update).toEqual(device);
  });

  it('delete sends DELETE to the device url', async () => {
    const remove = service.delete(7);

    const call = http.expectOne('/api/devices/7');
    expect(call.request.method).toBe('DELETE');
    call.flush(null, { status: 204, statusText: 'No Content' });

    await remove;
  });
});
