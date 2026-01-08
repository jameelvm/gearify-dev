import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { HttpService } from './http.service';
import { API_CONFIG } from '@shared/constants/api.constants';

export interface SortOption {
  id: string;
  label: string;
  value: string;
  sortType: string;
  filterField?: string;
  sortField: string;
  sortDirection: string;
  displayOrder: number;
  isActive: boolean;
}

export interface SortOptionsResponse {
  sortOptions: SortOption[];
}

@Injectable({
  providedIn: 'root'
})
export class SortOptionsService {
  private http = inject(HttpService);

  /**
   * Get sort options for the tenant
   * Used to render sort dropdown options
   */
  getSortOptions(): Observable<SortOptionsResponse> {
    return this.http.get<SortOptionsResponse>(API_CONFIG.ENDPOINTS.SORT_OPTIONS);
  }
}
