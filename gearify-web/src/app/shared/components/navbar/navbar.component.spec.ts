import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NavbarComponent } from './navbar.component';
import { AuthService } from '@core/services/auth.service';
import { Router, provideRouter, RouterLink } from '@angular/router';
import { signal } from '@angular/core';
import { Component } from '@angular/core';

// Dummy components for routing
@Component({ template: '' })
class DummyComponent {}

describe('NavbarComponent', () => {
  let component: NavbarComponent;
  let fixture: ComponentFixture<NavbarComponent>;

  beforeEach(async () => {
    const authServiceMock = {
      user: jest.fn().mockReturnValue(signal({ user: null, tokens: null, isAuthenticated: false, isLoading: false })),
      isAuthenticated: jest.fn().mockReturnValue(false),
      logout: jest.fn()
    } as any;

    await TestBed.configureTestingModule({
      imports: [NavbarComponent],
      providers: [
        provideRouter([
          { path: '', component: DummyComponent },
          { path: 'home', component: DummyComponent },
          { path: 'products', component: DummyComponent },
          { path: 'showcase', component: DummyComponent },
          { path: 'auth/login', component: DummyComponent },
          { path: 'auth/register', component: DummyComponent },
          { path: 'account/profile', component: DummyComponent },
          { path: 'account/orders', component: DummyComponent }
        ]),
        { provide: AuthService, useValue: authServiceMock }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(NavbarComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should render navbar', () => {
    const compiled = fixture.nativeElement;
    expect(compiled.querySelector('nav') || compiled.querySelector('.navbar')).toBeTruthy();
  });

  it('should initialize with user menu closed', () => {
    expect(component.showUserMenu).toBe(false);
  });

  it('should toggle user menu', () => {
    component.showUserMenu = false;
    component.toggleUserMenu();
    expect(component.showUserMenu).toBe(true);

    component.toggleUserMenu();
    expect(component.showUserMenu).toBe(false);
  });
});
