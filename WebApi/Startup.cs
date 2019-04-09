using System;
using System.Linq;
using System.Threading.Tasks;
using AspNet.Security.OpenIdConnect.Primitives;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Core;
using OpenIddict.EntityFrameworkCore.Models;
using WebApi.Data;

namespace WebApi
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<AuthorizationDbContext>(options =>
            {
                options.UseSqlServer(Configuration.GetConnectionString("AuthorizationDatabase"));
                options.UseOpenIddict();
            });

            services.AddIdentity<ApplicationUser, IdentityRole>()
                    .AddEntityFrameworkStores<AuthorizationDbContext>();

            // Configure Identity to use the same JWT claims as OpenIddict instead
            // of the legacy WS-Federation claims it uses by default (ClaimTypes),
            // which saves you from doing the mapping in your authorization controller.
            services.Configure<IdentityOptions>(options =>
            {
                options.ClaimsIdentity.UserNameClaimType = OpenIdConnectConstants.Claims.Name;
                options.ClaimsIdentity.UserIdClaimType = OpenIdConnectConstants.Claims.Subject;
                options.ClaimsIdentity.RoleClaimType = OpenIdConnectConstants.Claims.Role;

                // "Lax" requirements because doesn't make any sense from a security stand point it's more password managers friendly.
                options.Password.RequireDigit = false;
                options.Password.RequiredLength = 8; // increase the length, this is the only thing that "really" matters.
                options.Password.RequiredUniqueChars = 1; // At least 1 char is used in all password managers algorithms so safe to include and may prevent sloppy passwords.
                options.Password.RequireLowercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
            });

            services.ConfigureApplicationCookie(cookieOptions =>
            {
                cookieOptions.Cookie.SameSite = SameSiteMode.None;
                cookieOptions.Cookie.Name = "auth_cookie";

                cookieOptions.Events = new CookieAuthenticationEvents
                {
                    OnRedirectToLogin = redirectContext =>
                    {
                        redirectContext.HttpContext.Response.StatusCode = 401;
                        return Task.CompletedTask;
                    }
                };
            });

            services.AddAntiforgery(antiforgeryOptions =>
            {
                antiforgeryOptions.HeaderName = "X-XSRF-TOKEN";
            });

            services.AddOpenIddict()

                // Register the OpenIddict core services.
                .AddCore(options =>
                {
                    options.UseEntityFrameworkCore()
                           .UseDbContext<AuthorizationDbContext>();
                })

                .AddServer(options =>
                {
                    // Register the ASP.NET Core MVC binder used by OpenIddict.
                    // Note: if you don't call this method, you won't be able to
                    // bind OpenIdConnectRequest or OpenIdConnectResponse parameters.
                    options.UseMvc();

                    options.EnableAuthorizationEndpoint("/connect/authorize")
                           .EnableUserinfoEndpoint("/api/userinfo")
                           .EnableLogoutEndpoint("/connect/logout");

                    options.RegisterScopes(OpenIdConnectConstants.Scopes.Email,
                                           OpenIdConnectConstants.Scopes.Profile,
                                           OpenIddictConstants.Scopes.Roles);

                    options.AllowImplicitFlow();

                    // During development, you can disable the HTTPS requirement.
                    options.DisableHttpsRequirement();

                    // Register a new ephemeral key, that is discarded when the application
                    // shuts down. Tokens signed using this key are automatically invalidated.
                    // This method should only be used during development.
                    options.AddEphemeralSigningKey();

                    // On production, using a X.509 certificate stored in the machine store is recommended.
                    // You can generate a self-signed certificate using Pluralsight's self-cert utility:
                    // https://s3.amazonaws.com/pluralsight-free/keith-brown/samples/SelfCert.zip
                    //
                    // options.AddSigningCertificate("7D2A741FE34CC2C7369237A5F2078988E17A6A75");
                    //
                    // Alternatively, you can also store the certificate as an embedded .pfx resource
                    // directly in this assembly or in a file published alongside this project:
                    //
                    // options.AddSigningCertificate(
                    //     assembly: typeof(Startup).GetTypeInfo().Assembly,
                    //     resource: "AuthorizationServer.Certificate.pfx",
                    //     password: "OpenIddict");

                    // Note: to use JWT access tokens instead of the default
                    // encrypted format, the following line is required:
                    //
                    // options.UseJsonWebTokens();
                    options.SetAccessTokenLifetime(TimeSpan.FromSeconds(100)); // very short access token lifetime to see silentrefresh in action.
                })

                // Register the OpenIddict validation handler.
                // Note: the OpenIddict validation handler is only compatible with the
                // default token format or with reference tokens and cannot be used with
                // JWT tokens. For JWT tokens, use the Microsoft JWT bearer handler.
                .AddValidation();

            services.AddCors();

            services.AddMvc(options =>
            {
                //var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
                //options.Filters.Add(new AuthorizeFilter(policy));
                //options.Filters.Add(new ValidateAntiForgeryTokenAttribute());
            })
            .SetCompatibilityVersion(CompatibilityVersion.Version_2_2);

            // We don't configure cookie here because it's not usable on JS and that's why we create it using an inline "middleware" below (see Configure).
            // Also the context of the antiforgery cookie generated here is not the same as the Antiforgery token? Com'on MS.
            services.AddAntiforgery(antiforgeryOptions =>
            {
                // This is the header ASPNET will inspect for the Antiforgery token. 
                // Must be set (and match) on client (angular) app. See add-csrf-header-interceptor.ts
                antiforgeryOptions.HeaderName = "X-XSRF-TOKEN"; 
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, IAntiforgery antiforgery)
        {

            app.Use(next => context =>
            {
                // Do we need to match a particular endpoint / verb here ??

                var tokens = antiforgery.GetAndStoreTokens(context);
                context.Response.Cookies.Append("XSRF-TOKEN", tokens.RequestToken, new CookieOptions() // we rewrite the cookie as httponly:false.
                {
                    HttpOnly = false
                });
                return next(context);
            });

            app.UseCors(builder => builder
                .WithOrigins("https://localhost:44382")
                .WithOrigins("https://example.com")
                .WithHeaders("Authorization")
                .WithHeaders("x-xsrf-token", "content-type")
                .AllowCredentials()
                .Build());


            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseDatabaseErrorPage();
            }
            else
            {
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }
            app.UseAuthentication();

            app.UseHttpsRedirection();
            app.UseMvc();

            InitializeAsync(app.ApplicationServices).GetAwaiter().GetResult();
        }

        private async Task InitializeAsync(IServiceProvider services)
        {
            using (var scope = services.GetRequiredService<IServiceScopeFactory>().CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AuthorizationDbContext>();
                await context.Database.EnsureCreatedAsync();

                await CreateApplicationsAsync();
                await CreateScopesAsync();

                async Task CreateApplicationsAsync()
                {
                    var manager = scope.ServiceProvider.GetRequiredService<OpenIddictApplicationManager<OpenIddictApplication>>();

                    if (await manager.FindByClientIdAsync("angular-app") == null)
                    {
                        var descriptor = new OpenIddictApplicationDescriptor
                        {
                            ClientId = "angular-app",
                            DisplayName = "Angular app client application",
                            PostLogoutRedirectUris = { new Uri("https://localhost:44382/bye") },
                            RedirectUris = { new Uri("https://localhost:44382/auth-callback"), new Uri("https://localhost:44382/silentrefresh") },
                            Permissions =
                            {
                                OpenIddictConstants.Permissions.Endpoints.Authorization,
                                OpenIddictConstants.Permissions.Endpoints.Logout,
                                OpenIddictConstants.Permissions.GrantTypes.Implicit,
                                OpenIddictConstants.Permissions.Scopes.Email,
                                OpenIddictConstants.Permissions.Scopes.Profile,
                                OpenIddictConstants.Permissions.Scopes.Roles,
                                OpenIddictConstants.Permissions.Prefixes.Scope + "api1"
                            }
                        };

                        await manager.CreateAsync(descriptor);
                    }


                }
                async Task CreateScopesAsync()
                {
                    var manager = scope.ServiceProvider.GetRequiredService<OpenIddictScopeManager<OpenIddictScope>>();

                    if (await manager.FindByNameAsync("api1") == null)
                    {
                        var descriptor = new OpenIddictScopeDescriptor
                        {
                            Name = "api1",
                            Resources = { "resource-server-1" }
                        };

                        await manager.CreateAsync(descriptor);
                    }
                }
            }
        }
    }
}
