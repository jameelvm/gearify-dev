import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';

export interface SubCategory {
  name: string;
  slug: string;
}

export interface MegaMenuSection {
  title: string;
  items: SubCategory[];
}

export interface Category {
  id: string;
  name: string;
  slug: string;
  icon?: string;
  megaMenu?: MegaMenuSection[];
}

@Component({
  selector: 'app-category-nav',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule],
  templateUrl: './category-nav.component.html',
  styleUrls: ['./category-nav.component.scss']
})
export class CategoryNavComponent {
  searchQuery = signal('');
  activeCategory = signal<string | null>(null);
  hoveredCategory = signal<string | null>(null);

  categories: Category[] = [
    {
      id: '1',
      name: 'Cricket Bats',
      slug: 'cricket-bats',
      megaMenu: [
        {
          title: 'Shop New Arrivals',
          items: [
            { name: 'English Willow Bats', slug: 'english-willow' },
            { name: 'Kashmir Willow Bats', slug: 'kashmir-willow' },
            { name: 'Tennis Bats', slug: 'tennis-bats' },
            { name: 'Junior Bats', slug: 'junior-bats' }
          ]
        },
        {
          title: 'By Brand',
          items: [
            { name: 'SS', slug: 'ss' },
            { name: 'Kookaburra', slug: 'kookaburra' },
            { name: 'Gray-Nicolls', slug: 'gray-nicolls' },
            { name: 'SG', slug: 'sg' },
            { name: 'MRF', slug: 'mrf' }
          ]
        },
        {
          title: 'By Player Level',
          items: [
            { name: 'Professional', slug: 'professional' },
            { name: 'Intermediate', slug: 'intermediate' },
            { name: 'Beginner', slug: 'beginner' }
          ]
        }
      ]
    },
    {
      id: '2',
      name: 'Cricket Balls',
      slug: 'cricket-balls',
      megaMenu: [
        {
          title: 'Shop New Arrivals',
          items: [
            { name: 'Leather Balls', slug: 'leather-balls' },
            { name: 'Tennis Balls', slug: 'tennis-balls' },
            { name: 'Practice Balls', slug: 'practice-balls' },
            { name: 'Match Balls', slug: 'match-balls' }
          ]
        },
        {
          title: 'By Type',
          items: [
            { name: 'Test Cricket Balls', slug: 'test-balls' },
            { name: 'ODI Cricket Balls', slug: 'odi-balls' },
            { name: 'T20 Cricket Balls', slug: 't20-balls' },
            { name: 'Indoor Cricket Balls', slug: 'indoor-balls' }
          ]
        }
      ]
    },
    {
      id: '3',
      name: 'Protective Gear',
      slug: 'protective-gear',
      megaMenu: [
        {
          title: 'Shop New Arrivals',
          items: [
            { name: 'Batting Pads', slug: 'batting-pads' },
            { name: 'Batting Gloves', slug: 'batting-gloves' },
            { name: 'Helmets', slug: 'helmets' },
            { name: 'Thigh Guards', slug: 'thigh-guards' },
            { name: 'Chest Guards', slug: 'chest-guards' }
          ]
        },
        {
          title: 'Wicket Keeping',
          items: [
            { name: 'WK Gloves', slug: 'wk-gloves' },
            { name: 'WK Pads', slug: 'wk-pads' },
            { name: 'WK Inner Gloves', slug: 'wk-inner-gloves' }
          ]
        }
      ]
    },
    {
      id: '4',
      name: 'Clothing',
      slug: 'clothing',
      megaMenu: [
        {
          title: 'Shop New Arrivals',
          items: [
            { name: 'Cricket Jerseys', slug: 'jerseys' },
            { name: 'Cricket Pants', slug: 'pants' },
            { name: 'Cricket Shirts', slug: 'shirts' },
            { name: 'Sweaters', slug: 'sweaters' }
          ]
        },
        {
          title: 'Collections',
          items: [
            { name: 'Team India Collection', slug: 'team-india' },
            { name: 'IPL Collection', slug: 'ipl' },
            { name: 'Training Wear', slug: 'training-wear' }
          ]
        }
      ]
    },
    {
      id: '5',
      name: 'Footwear',
      slug: 'footwear',
      megaMenu: [
        {
          title: 'Shop New Arrivals',
          items: [
            { name: 'Spikes', slug: 'spikes' },
            { name: 'Rubber Sole Shoes', slug: 'rubber-sole' },
            { name: 'Training Shoes', slug: 'training-shoes' }
          ]
        }
      ]
    },
    {
      id: '6',
      name: 'Accessories',
      slug: 'accessories',
      megaMenu: [
        {
          title: 'Shop New Arrivals',
          items: [
            { name: 'Cricket Bags', slug: 'bags' },
            { name: 'Grips', slug: 'grips' },
            { name: 'Bat Care', slug: 'bat-care' },
            { name: 'Stumps & Bails', slug: 'stumps-bails' }
          ]
        }
      ]
    },
    {
      id: '7',
      name: 'Training Equipment',
      slug: 'training-equipment',
      megaMenu: [
        {
          title: 'Shop New Arrivals',
          items: [
            { name: 'Bowling Machines', slug: 'bowling-machines' },
            { name: 'Cricket Nets', slug: 'nets' },
            { name: 'Practice Pitches', slug: 'practice-pitches' },
            { name: 'Cones & Markers', slug: 'cones-markers' }
          ]
        }
      ]
    },
    {
      id: '8',
      name: 'Team Kits',
      slug: 'team-kits',
      megaMenu: [
        {
          title: 'Shop Team Kits',
          items: [
            { name: 'Complete Team Sets', slug: 'complete-sets' },
            { name: 'Custom Team Jerseys', slug: 'custom-jerseys' },
            { name: 'Team Accessories', slug: 'team-accessories' }
          ]
        }
      ]
    }
  ];

  onSearch(): void {
    const query = this.searchQuery();
    if (query.trim()) {
      console.log('Searching for:', query);
      // TODO: Navigate to search results or filter products
    }
  }

  onCategoryHover(categoryId: string): void {
    this.hoveredCategory.set(categoryId);
  }

  onCategoryLeave(): void {
    this.hoveredCategory.set(null);
  }

  onCategoryClick(category: Category): void {
    console.log('Category clicked:', category);
    // TODO: Navigate to category page or filter products
  }

  onSubCategoryClick(category: Category, subCategory: SubCategory): void {
    console.log('SubCategory clicked:', category.slug, subCategory.slug);
    this.hoveredCategory.set(null);
    // TODO: Navigate to subcategory page
  }

  getActiveCategory(): Category | undefined {
    const categoryId = this.hoveredCategory();
    if (!categoryId) return undefined;
    return this.categories.find(cat => cat.id === categoryId);
  }
}
