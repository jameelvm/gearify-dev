import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { HttpService } from './http.service';
import { API_CONFIG } from '@shared/constants/api.constants';

export interface UpdateProfileRequest {
  firstName?: string;
  lastName?: string;
  phone?: string;
  addressLine1?: string;
  addressLine2?: string;
  city?: string;
  state?: string;
  zipCode?: string;
  country?: string;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

@Injectable({
  providedIn: 'root'
})
export class UserService {
  private http = inject(HttpService);

  updateProfile(data: UpdateProfileRequest): Observable<void> {
    return this.http.put<void>(API_CONFIG.ENDPOINTS.UPDATE_PROFILE, data);
  }

  changePassword(data: ChangePasswordRequest): Observable<void> {
    return this.http.post<void>(API_CONFIG.ENDPOINTS.CHANGE_PASSWORD, data);
  }
}
