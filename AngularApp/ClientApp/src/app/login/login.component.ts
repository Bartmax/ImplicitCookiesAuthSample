import { Component, OnInit } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { AuthService } from '../services/auth.service';
import { switchMap, concatMap } from 'rxjs/operators';
import { Observable, from } from 'rxjs';
import { Router, ActivatedRoute } from '@angular/router';

@Component({
  selector: 'app-login',
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.css']
})
export class LoginComponent implements OnInit {
  loginFeedback: string = '';
  returnUrl: string;

  constructor(private authService: AuthService,
    private route: ActivatedRoute,
    private router: Router) { }

  ngOnInit() {
    this.returnUrl = this.route.snapshot.queryParams['returnUrl'] || '/';
  }


  login(email: string, password: string): Observable<any> {
    this.loginFeedback = '';

    let login$ = this.authService.login(email, password);

    login$.subscribe(_ => {
      this.loginFeedback = 'Login successful. Try the requests that requires user to be logged in.';
      this.authService.startAuthentication().then(_ => {
        this.router.navigateByUrl(this.returnUrl);
      });
    }, (errorResponse: HttpErrorResponse) => {
      this.loginFeedback = errorResponse.error;
    });
    return login$;

  }

  logout() {
    this.authService.logout().subscribe();
  }

}
