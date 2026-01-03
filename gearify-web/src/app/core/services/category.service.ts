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
  brandId?: string;
  priceRangeId?: string;
  filterType?: string;
  minPrice?: number;
  maxPrice?: number;
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

export interface DepartmentMenuDto {
  id: string;
  name: string;
  slug: string;
  icon: string;
  displayOrder: number;
  categories: CategoryWithDetailsDto[];
}

export interface MegaMenuDto {
  departments: DepartmentMenuDto[];
}

@Injectable({
  providedIn: 'root'
})
export class CategoryService {
  private http = inject(HttpService);

  /**
   * Get all categories with complete mega menu data
   * Returns department-aware structure that supports both single and multi-department tenants
   */
  getMegaMenuData(): Observable<MegaMenuDto> {
    return this.http.get<MegaMenuDto>(`${API_CONFIG.ENDPOINTS.CATALOG}/categories/mega-menu`);
  }

  /**
   * Seed initial category data (dev/admin only)
   */
  seedCategories(): Observable<any> {
    return this.http.post<any>(`${API_CONFIG.ENDPOINTS.CATALOG}/seed/categories`, {});
  }
}
