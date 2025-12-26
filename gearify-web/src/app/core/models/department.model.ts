/**
 * Department model representing a top-level product category
 * (e.g., Cricket, Perfume, Electronics)
 */
export interface Department {
  id: string;
  name: string;
  slug: string;
  description: string;
  icon: string;
  imageUrl: string;
  displayOrder: number;
  categoryCount: number;
}

/**
 * Department with its categories
 */
export interface DepartmentWithCategories extends Department {
  categories: CategorySummary[];
}

/**
 * Category summary (lightweight version without sections)
 */
export interface CategorySummary {
  id: string;
  name: string;
  slug: string;
  description: string;
  icon: string;
  imageUrl: string;
  displayOrder: number;
}
