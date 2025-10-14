import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';

export interface User {
  id: string;
  email: string;
  name: string;
  role: string;
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private currentUserSubject = new BehaviorSubject<User | null>(null);
  public currentUser$: Observable<User | null> = this.currentUserSubject.asObservable();

  login(email: string, password: string): boolean {
    const user: User = {
      id: '1',
      email: email,
      name: 'John Doe',
      role: 'customer'
    };
    this.currentUserSubject.next(user);
    localStorage.setItem('auth_token', 'fake-jwt-token');
    return true;
  }

  logout() {
    this.currentUserSubject.next(null);
    localStorage.removeItem('auth_token');
  }

  isAuthenticated(): boolean {
    return this.currentUserSubject.value !== null;
  }
}
