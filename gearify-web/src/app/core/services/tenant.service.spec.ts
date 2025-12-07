import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { TenantService, TenantValidationResponse } from './tenant.service';
import { environment } from '@environments/environment';

describe('TenantService', () => {
  let service: TenantService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        TenantService,
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(TenantService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  describe('validateTenant', () => {
    it('should return true for valid and active tenant', (done) => {
      const tenantId = 'test-tenant';
      const mockResponse: TenantValidationResponse = {
        tenantId: 'test-tenant',
        isValid: true,
        isActive: true
      };

      service.validateTenant(tenantId).subscribe((result) => {
        expect(result).toBe(true);
        done();
      });

      const req = httpMock.expectOne(`${environment.apiUrl}/api/tenants/validate/${tenantId}`);
      expect(req.request.method).toBe('GET');
      req.flush(mockResponse);
    });

    it('should return false for invalid tenant', (done) => {
      const tenantId = 'invalid-tenant';
      const mockResponse: TenantValidationResponse = {
        tenantId: 'invalid-tenant',
        isValid: false,
        isActive: true
      };

      service.validateTenant(tenantId).subscribe((result) => {
        expect(result).toBe(false);
        done();
      });

      const req = httpMock.expectOne(`${environment.apiUrl}/api/tenants/validate/${tenantId}`);
      req.flush(mockResponse);
    });

    it('should return false for inactive tenant', (done) => {
      const tenantId = 'inactive-tenant';
      const mockResponse: TenantValidationResponse = {
        tenantId: 'inactive-tenant',
        isValid: true,
        isActive: false
      };

      service.validateTenant(tenantId).subscribe((result) => {
        expect(result).toBe(false);
        done();
      });

      const req = httpMock.expectOne(`${environment.apiUrl}/api/tenants/validate/${tenantId}`);
      req.flush(mockResponse);
    });

    it('should return false for both invalid and inactive tenant', (done) => {
      const tenantId = 'bad-tenant';
      const mockResponse: TenantValidationResponse = {
        tenantId: 'bad-tenant',
        isValid: false,
        isActive: false
      };

      service.validateTenant(tenantId).subscribe((result) => {
        expect(result).toBe(false);
        done();
      });

      const req = httpMock.expectOne(`${environment.apiUrl}/api/tenants/validate/${tenantId}`);
      req.flush(mockResponse);
    });

    it('should return false when tenantId is empty', (done) => {
      service.validateTenant('').subscribe((result) => {
        expect(result).toBe(false);
        done();
      });

      httpMock.expectNone(`${environment.apiUrl}/api/tenants/validate/`);
    });

    it('should return false when tenantId is null', (done) => {
      service.validateTenant(null as any).subscribe((result) => {
        expect(result).toBe(false);
        done();
      });

      httpMock.expectNone(`${environment.apiUrl}/api/tenants/validate/null`);
    });

    it('should handle HTTP errors gracefully', (done) => {
      const tenantId = 'error-tenant';
      const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation();

      service.validateTenant(tenantId).subscribe((result) => {
        expect(result).toBe(false);
        expect(consoleErrorSpy).toHaveBeenCalled();
        consoleErrorSpy.mockRestore();
        done();
      });

      const req = httpMock.expectOne(`${environment.apiUrl}/api/tenants/validate/${tenantId}`);
      req.error(new ProgressEvent('Network error'), { status: 500, statusText: 'Server Error' });
    });

    it('should handle 404 errors', (done) => {
      const tenantId = 'not-found-tenant';
      const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation();

      service.validateTenant(tenantId).subscribe((result) => {
        expect(result).toBe(false);
        expect(consoleErrorSpy).toHaveBeenCalled();
        consoleErrorSpy.mockRestore();
        done();
      });

      const req = httpMock.expectOne(`${environment.apiUrl}/api/tenants/validate/${tenantId}`);
      req.error(new ProgressEvent('Not Found'), { status: 404, statusText: 'Not Found' });
    });

    it('should handle network timeout errors', (done) => {
      const tenantId = 'timeout-tenant';
      const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation();

      service.validateTenant(tenantId).subscribe((result) => {
        expect(result).toBe(false);
        expect(consoleErrorSpy).toHaveBeenCalled();
        consoleErrorSpy.mockRestore();
        done();
      });

      const req = httpMock.expectOne(`${environment.apiUrl}/api/tenants/validate/${tenantId}`);
      req.error(new ProgressEvent('Timeout'), { status: 0, statusText: 'Unknown Error' });
    });

    it('should validate multiple tenants sequentially', (done) => {
      const tenant1 = 'tenant1';
      const tenant2 = 'tenant2';

      service.validateTenant(tenant1).subscribe((result1) => {
        expect(result1).toBe(true);

        service.validateTenant(tenant2).subscribe((result2) => {
          expect(result2).toBe(false);
          done();
        });

        const req2 = httpMock.expectOne(`${environment.apiUrl}/api/tenants/validate/${tenant2}`);
        req2.flush({ tenantId: tenant2, isValid: false, isActive: true });
      });

      const req1 = httpMock.expectOne(`${environment.apiUrl}/api/tenants/validate/${tenant1}`);
      req1.flush({ tenantId: tenant1, isValid: true, isActive: true });
    });

    it('should use correct API URL from environment', (done) => {
      const tenantId = 'test';

      service.validateTenant(tenantId).subscribe(() => done());

      const req = httpMock.expectOne(`${environment.apiUrl}/api/tenants/validate/${tenantId}`);
      expect(req.request.url).toContain(environment.apiUrl);
      req.flush({ tenantId: 'test', isValid: true, isActive: true });
    });
  });

  describe('listTenants', () => {
    it('should return list of tenants', (done) => {
      const mockTenants = ['tenant1', 'tenant2', 'tenant3'];

      service.listTenants().subscribe((tenants) => {
        expect(tenants).toEqual(mockTenants);
        expect(tenants.length).toBe(3);
        done();
      });

      const req = httpMock.expectOne(`${environment.apiUrl}/api/tenants/list`);
      expect(req.request.method).toBe('GET');
      req.flush(mockTenants);
    });

    it('should return empty array when no tenants exist', (done) => {
      service.listTenants().subscribe((tenants) => {
        expect(tenants).toEqual([]);
        expect(tenants.length).toBe(0);
        done();
      });

      const req = httpMock.expectOne(`${environment.apiUrl}/api/tenants/list`);
      req.flush([]);
    });

    it('should handle errors when listing tenants', (done) => {
      service.listTenants().subscribe({
        next: () => fail('Should have failed'),
        error: (error) => {
          expect(error).toBeDefined();
          done();
        }
      });

      const req = httpMock.expectOne(`${environment.apiUrl}/api/tenants/list`);
      req.error(new ProgressEvent('Network error'), { status: 500 });
    });
  });
});
