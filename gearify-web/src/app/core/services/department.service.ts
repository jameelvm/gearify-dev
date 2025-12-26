import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Department, DepartmentWithCategories, CategorySummary } from '../models/department.model';
import { API_CONFIG } from '@shared/constants/api.constants';

@Injectable({
  providedIn: 'root'
})
export class DepartmentService {
  private http = inject(HttpClient);
  private apiUrl = API_CONFIG.BASE_URL;

  /**
   * Get all departments for the current tenant
   */
  getDepartments(): Observable<Department[]> {
    console.log('[DepartmentService] Fetching all departments');
    return this.http.get<Department[]>(`${this.apiUrl}${API_CONFIG.ENDPOINTS.DEPARTMENTS}`);
  }

  /**
   * Get a specific department by slug with its categories
   */
  getDepartmentBySlug(slug: string): Observable<DepartmentWithCategories> {
    console.log('[DepartmentService] Fetching department:', slug);
    const url = `${this.apiUrl}${API_CONFIG.ENDPOINTS.DEPARTMENT_BY_SLUG(slug)}`;
    return this.http.get<DepartmentWithCategories>(url);
  }

  /**
   * Get categories for a specific department
   */
  getDepartmentCategories(slug: string): Observable<CategorySummary[]> {
    console.log('[DepartmentService] Fetching categories for department:', slug);
    const url = `${this.apiUrl}${API_CONFIG.ENDPOINTS.DEPARTMENT_CATEGORIES(slug)}`;
    return this.http.get<CategorySummary[]>(url);
  }
}
