import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { HttpService } from './http.service';
import { API_CONFIG } from '@shared/constants/api.constants';

export interface SubcategoryDto {
  id: string;
  categoryId: string;
  sectionId: string;
  name: string;
  slug: string;
  description: string;
  imageUrl: string;
  displayOrder: number;
  productCount: number;
  isActive: boolean;
}

export interface SectionWithItemsDto {
  id: string;
  title: string;
  slug: string;
  showTitle: boolean;
  displayOrder: number;
  items: SubcategoryDto[];
}

export interface CategoryDto {
  id: string;
  name: string;
  slug: string;
  description: string;
  icon: string;
  imageUrl: string;
  displayOrder: number;
  isActive: boolean;
}

export interface CategoryWithDetailsDto {
  category: CategoryDto;
  sections: SectionWithItemsDto[];
}

@Injectable({
  providedIn: 'root'
})
export class CategoryService {
  private http = inject(HttpService);

  /**
   * Get all categories with complete mega menu data
   */
  getMegaMenuData(): Observable<CategoryWithDetailsDto[]> {
    return this.http.get<CategoryWithDetailsDto[]>(`${API_CONFIG.ENDPOINTS.CATALOG}/categories/mega-menu`);
  }

  /**
   * Get all categories (without details)
   */
  getAllCategories(): Observable<CategoryDto[]> {
    return this.http.get<CategoryDto[]>(`${API_CONFIG.ENDPOINTS.CATALOG}/categories`);
  }

  /**
   * Get single category with details
   */
  getCategoryDetails(categoryId: string): Observable<CategoryWithDetailsDto> {
    return this.http.get<CategoryWithDetailsDto>(`${API_CONFIG.ENDPOINTS.CATALOG}/categories/${categoryId}/details`);
  }

  /**
   * Seed initial category data (dev/admin only)
   */
  seedCategories(): Observable<any> {
    return this.http.post<any>(`${API_CONFIG.ENDPOINTS.CATALOG}/seed/categories`, {});
  }
}
