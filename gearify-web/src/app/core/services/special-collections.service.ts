import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { HttpService } from './http.service';
import { API_CONFIG } from '@shared/constants/api.constants';

export interface SpecialCollectionDto {
  id: string;       // GUID for tracking
  slug: string;     // URL-friendly identifier
  label: string;
  icon: string;
  filterAttribute: string;
  filterType: string;
  displayOrder: number;
  isActive: boolean;
  badgeText?: string;
  badgeColor?: string;
  description?: string;
}

export interface SpecialCollectionsResponse {
  collections: SpecialCollectionDto[];
}

@Injectable({
  providedIn: 'root'
})
export class SpecialCollectionsService {
  private http = inject(HttpService);

  /**
   * Get special product collections for the tenant
   * Used to render special collection links in mega menu
   */
  getSpecialCollections(departmentSlug?: string): Observable<SpecialCollectionsResponse> {
    const params = departmentSlug ? `?departmentSlug=${departmentSlug}` : '';
    return this.http.get<SpecialCollectionsResponse>(`${API_CONFIG.ENDPOINTS.CATALOG}/special-collections${params}`);
  }
}
