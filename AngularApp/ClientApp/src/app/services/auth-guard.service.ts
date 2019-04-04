import { Injectable } from '@angular/core';
import { CanActivate, Router, ActivatedRouteSnapshot, RouterStateSnapshot } from '@angular/router';

import { AuthService } from '../services/auth.service'
import { Observable, of } from 'rxjs';
import { tap, switchMap } from 'rxjs/operators';

@Injectable()
export class AuthGuardService implements CanActivate {

  constructor(private authService: AuthService, private router: Router) {
  }
  canActivate(
    _next: ActivatedRouteSnapshot,
    state: RouterStateSnapshot): Observable<boolean> | Promise<boolean> | boolean {

    if (this.authService.isLoggedIn()) {
      return true;
    }

    return this.authService.startAuthentication().then(res => {
      return true;
    },

      err => {
        this.router.navigate(['login'], {
          queryParams: {
            ['returnUrl']: state.url
          }
        });
        return false;
      });

  }

}
