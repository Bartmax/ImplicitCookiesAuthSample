import { Injectable } from '@angular/core';
import { UserManager, UserManagerSettings, User } from 'oidc-client';
import { environment } from '../../environments/environment';
import { Observable, of, from } from 'rxjs';
import { switchMap, map, concatMap } from 'rxjs/operators';

import { HttpClient } from '@angular/common/http';

@Injectable({
  providedIn: 'root'
})
export class AuthService {


  private manager = new UserManager(getClientSettings());

  private user: User = null;

  constructor(private http: HttpClient) {
    this.manager.getUser().then(user => { this.user = user });
  }


  getUsername(): Observable<any | null> {
    return this.http.get(`${environment.apiUrl}/api/account/username`, {
      responseType: 'text'
    });
  }

  isLoggedIn(): boolean {
    return this.user != null && !this.user.expired;
  }

  getClaims(): any {
    return this.user.profile;
  }

  getAuthorizationHeaderValue(): string {
    return `${this.user.token_type} ${this.user.access_token}`;
  }

  startAuthentication(): Promise<User> {
    return this.manager.signinSilent().then(_ => this.manager.getUser().then(user => { this.user = user; return user; }));
    //return this.manager.signinRedirect();
  }

  completeAuthentication(): Promise<void> {
    return this.manager.signinRedirectCallback().then(user => {
      this.user = user;
    });
  }

  login(email: string, password: string): Observable<any> {
    return this.http.post(`${environment.apiUrl}/api/account/login`, {
      email,
      password
    }, {
        responseType: 'text',
        withCredentials: true
      });
  }

  register(email: string, password: string) {
    return this.http.post(`${environment.apiUrl}/api/account/register`, {
      email,
      password
    }, {
        responseType: 'text'
      });
  }

  logout(): Observable<any> {

    let logout$ = this.http.post(`${environment.apiUrl}/api/account/logout`, {}, { responseType: 'text', withCredentials: true });
    return logout$.pipe(switchMap(_=> from(this.manager.signoutRedirect())));
  }

}


export function getClientSettings(): UserManagerSettings {
  return {
    authority: `${environment.apiUrl}`,
    client_id: 'angular-app',
    redirect_uri: 'https://localhost:44382/auth-callback',
    post_logout_redirect_uri: 'https://localhost:44382/bye',
    response_type: "id_token token",
    scope: "openid profile api1",
    filterProtocolClaims: true,
    loadUserInfo: true,
    automaticSilentRenew: true,
    silent_redirect_uri: 'https://localhost:44382/silentrefresh'
  };
}
