using System;
using System.Threading.Tasks;
using AspNet.Security.OpenIdConnect.Primitives;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
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
            });

            services.Configure<IdentityOptions>(options =>
            {
                // "Lax" requirements because doesn't make any sense from a security stand point it's more password managers friendly.
                options.Password.RequireDigit = false;
                options.Password.RequiredLength = 8; // increase the length, this is the only thing that "really" matters.
                options.Password.RequiredUniqueChars = 1; // At least 1 char is used in all password managers algorithms so safe to include and may prevent sloppy passwords.
                options.Password.RequireLowercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
            });

            services.AddOpenIddict()

                // Register the OpenIddict core services.
                .AddCore(options =>
                {
                    // Register the Entity Framework stores and models.
                    options.UseEntityFrameworkCore()
                           .UseDbContext<AuthorizationDbContext>();
                })

                // Register the OpenIddict server handler.
                .AddServer(options =>
                {
                    // Register the ASP.NET Core MVC binder used by OpenIddict.
                    // Note: if you don't call this method, you won't be able to
                    // bind OpenIdConnectRequest or OpenIdConnectResponse parameters.
                    options.UseMvc();

                    // Enable the authorization, logout, userinfo, and introspection endpoints.
                    options.EnableAuthorizationEndpoint("/connect/authorize")
                           .EnableLogoutEndpoint("/connect/logout")
                           .EnableIntrospectionEndpoint("/connect/introspect")
                           .EnableUserinfoEndpoint("/api/userinfo");

                    // Mark the "email", "profile" and "roles" scopes as supported scopes.
                    options.RegisterScopes(OpenIdConnectConstants.Scopes.Email,
                                           OpenIdConnectConstants.Scopes.Profile,
                                           OpenIddictConstants.Scopes.Roles);

                    // Note: the sample only uses the implicit code flow but you can enable
                    // the other flows if you need to support implicit, password or client credentials.
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
                })

                // Register the OpenIddict validation handler.
                // Note: the OpenIddict validation handler is only compatible with the
                // default token format or with reference tokens and cannot be used with
                // JWT tokens. For JWT tokens, use the Microsoft JWT bearer handler.
                .AddValidation();

            services.AddCors();

            services.AddMvc(options =>
            {
                var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
                options.Filters.Add(new AuthorizeFilter(policy));
                //options.Filters.Add(new ValidateAntiForgeryTokenAttribute());
            })
            .SetCompatibilityVersion(CompatibilityVersion.Version_2_2);

            services.AddAntiforgery(antiforgeryOptions =>
            {
                antiforgeryOptions.HeaderName = "X-XSRF-TOKEN";
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {

            app.UseCors(builder => builder
                .WithOrigins("https://localhost:*")
                .WithOrigins("https://example.com")
                .WithHeaders("Authorization")
                .WithHeaders("x-xsrf-token", "content-type")
                .AllowCredentials());

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

                async Task CreateApplicationsAsync()
                {
                    var manager = scope.ServiceProvider.GetRequiredService<OpenIddictApplicationManager<OpenIddictApplication>>();

                    if (await manager.FindByClientIdAsync("angular-app") == null)
                    {
                        var descriptor = new OpenIddictApplicationDescriptor
                        {
                            ClientId = "angular-app",
                            DisplayName = "Angular app client application",
                            PostLogoutRedirectUris = { new Uri("http://localhost:9000/signout-oidc") },
                            RedirectUris = { new Uri("http://localhost:9000/signin-oidc") },
                            Permissions =
                            {
                                OpenIddictConstants.Permissions.Endpoints.Authorization,
                                OpenIddictConstants.Permissions.Endpoints.Logout,
                                OpenIddictConstants.Permissions.GrantTypes.Implicit,
                                OpenIddictConstants.Permissions.Scopes.Email,
                                OpenIddictConstants.Permissions.Scopes.Profile,
                                OpenIddictConstants.Permissions.Scopes.Roles
                            }
                        };

                        await manager.CreateAsync(descriptor);
                    }


                }
            }
        }
    }
}
