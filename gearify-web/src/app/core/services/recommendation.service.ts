import { Injectable, inject } from '@angular/core';
import { HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { HttpService } from '@core/services/http.service';
import { API_CONFIG } from '@shared/constants/api.constants';

export interface ProductRecommendation {
  productId: string;
  name: string;
  price: number;
  thumbnailUrl: string | null;
  category: string;
  brand: string;
  score: number;
  recommendationReason: string | null;
}

export interface RecommendationResponse {
  items: ProductRecommendation[];
  source: string;
  totalCount: number;
}

@Injectable({ providedIn: 'root' })
export class RecommendationService {
  private http = inject(HttpService);

  getForYou(limit = 10): Observable<RecommendationResponse> {
    const params = new HttpParams().set('limit', limit.toString());
    return this.http.get<RecommendationResponse>(
      API_CONFIG.ENDPOINTS.RECOMMENDATIONS_FOR_YOU,
      params
    );
  }

  getSimilar(productId: string, limit = 10): Observable<RecommendationResponse> {
    const params = new HttpParams().set('limit', limit.toString());
    return this.http.get<RecommendationResponse>(
      API_CONFIG.ENDPOINTS.RECOMMENDATIONS_SIMILAR(productId),
      params
    );
  }

  getComplementary(productId: string, limit = 10): Observable<RecommendationResponse> {
    const params = new HttpParams().set('limit', limit.toString());
    return this.http.get<RecommendationResponse>(
      API_CONFIG.ENDPOINTS.RECOMMENDATIONS_COMPLEMENTARY(productId),
      params
    );
  }

  recordInteraction(productId: string, eventType: string, eventValue?: number): Observable<any> {
    return this.http.post(API_CONFIG.ENDPOINTS.RECOMMENDATIONS_INTERACTIONS, {
      productId,
      eventType,
      eventValue
    });
  }
}
