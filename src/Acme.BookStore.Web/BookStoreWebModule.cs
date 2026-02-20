using System;
using System.IO;
using Acme.BookStore.EntityFrameworkCore;
using Acme.BookStore.Localization;
using Acme.BookStore.MultiTenancy;
using Acme.BookStore.Permissions;
using Acme.BookStore.Web.HealthChecks;
using Acme.BookStore.Web.Menus;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Authentication.Twitter;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Logging;
using Microsoft.OpenApi;
using OpenIddict.Server.AspNetCore;
using OpenIddict.Validation.AspNetCore;
using TickerQ.DependencyInjection;
using TickerQ.Dashboard.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Account.Admin.Web;
using Volo.Abp.Account.Public.Web;
using Volo.Abp.Account.Public.Web.ExternalProviders;
using Volo.Abp.Account.Web;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.Localization;
using Volo.Abp.AspNetCore.Mvc.UI.Bundling;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.LeptonXLite;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.LeptonXLite.Bundling;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Shared;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Shared.Toolbars;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.AuditLogging.Web;
using Volo.Abp.Autofac;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Gdpr.Web;
using Volo.Abp.Gdpr.Web.Extensions;
using Volo.Abp.Identity;
using Volo.Abp.Identity.Web;
using Volo.Abp.LanguageManagement;
using Volo.Abp.Modularity;
using Volo.Abp.OpenIddict;
using Volo.Abp.OpenIddict.Pro.Web;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Security.Claims;
using Volo.Abp.Studio;
using Volo.Abp.Studio.Client.AspNetCore;
using Volo.Abp.Swashbuckle;
using Volo.Abp.UI.Navigation;
using Volo.Abp.UI.Navigation.Urls;
using Volo.Abp.VirtualFileSystem;
using Volo.Saas.Host;

namespace Acme.BookStore.Web;

[DependsOn(
    typeof(BookStoreHttpApiModule),
    typeof(BookStoreApplicationModule),
    typeof(BookStoreEntityFrameworkCoreModule),
    typeof(AbpAutofacModule),
    typeof(AbpStudioClientAspNetCoreModule),
    typeof(AbpIdentityWebModule),
    typeof(AbpAspNetCoreMvcUiLeptonXLiteThemeModule),
    typeof(AbpAccountPublicWebOpenIddictModule),
    typeof(AbpAuditLoggingWebModule),
    typeof(SaasHostWebModule),
    typeof(AbpAccountAdminWebModule),
    typeof(AbpOpenIddictProWebModule),
    typeof(LanguageManagementWebModule),
    typeof(AbpGdprWebModule),
    typeof(AbpFeatureManagementWebModule),
    typeof(AbpSwashbuckleModule),
    typeof(AbpAspNetCoreSerilogModule)
)]
public class BookStoreWebModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        var hostingEnvironment = context.Services.GetHostingEnvironment();
        var configuration = context.Services.GetConfiguration();

        context.Services.PreConfigure<AbpMvcDataAnnotationsLocalizationOptions>(options =>
        {
            options.AddAssemblyResource(
                typeof(BookStoreResource),
                typeof(BookStoreDomainModule).Assembly,
                typeof(BookStoreDomainSharedModule).Assembly,
                typeof(BookStoreApplicationModule).Assembly,
                typeof(BookStoreApplicationContractsModule).Assembly,
                typeof(BookStoreWebModule).Assembly
            );
        });

        PreConfigure<OpenIddictBuilder>(builder =>
        {
            builder.AddValidation(options =>
            {
                options.AddAudiences("BookStore");
                options.UseLocalServer();
                options.UseAspNetCore();
            });
        });

        if (!hostingEnvironment.IsDevelopment())
        {
            PreConfigure<AbpOpenIddictAspNetCoreOptions>(options =>
            {
                options.AddDevelopmentEncryptionAndSigningCertificate = false;
            });

            PreConfigure<OpenIddictServerBuilder>(serverBuilder =>
            {
                serverBuilder.AddProductionEncryptionAndSigningCertificate("openiddict.pfx",
                    configuration["AuthServer:CertificatePassPhrase"]!);
                serverBuilder.SetIssuer(new Uri(configuration["AuthServer:Authority"]!));
            });
        }
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var hostingEnvironment = context.Services.GetHostingEnvironment();
        var configuration = context.Services.GetConfiguration();

        context.Services.AddTickerQ(options =>
        {
            options.AddDashboard(dashboardOptions =>
            {
                dashboardOptions.BasePath = "/tickerq/dashboard";

                if (hostingEnvironment.IsDevelopment())
                {
                    dashboardOptions.UseHostAuthentication = false;
                    dashboardOptions.EnableBuiltInAuth = false;
                    dashboardOptions.EnableBasicAuth = false;
                }
                else
                {
                    dashboardOptions.UseHostAuthentication = true;
                }
            });
        });

        if (!configuration.GetValue<bool>("App:DisablePII"))
        {
            IdentityModelEventSource.ShowPII = true;
            IdentityModelEventSource.LogCompleteSecurityArtifact = true;
        }

        if (!configuration.GetValue<bool>("AuthServer:RequireHttpsMetadata"))
        {
            Configure<OpenIddictServerAspNetCoreOptions>(options =>
            {
                options.DisableTransportSecurityRequirement = true;
            });

            Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedProto;
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
            });
        }

        ConfigureStudio(hostingEnvironment);
        ConfigureBundles();
        ConfigureUrls(configuration);
        ConfigureHealthChecks(context);
        ConfigurePages(configuration);
        ConfigureImpersonation(context, configuration);
        ConfigureExternalProviders(context);
        ConfigureCookieConsent(context);
        ConfigureAuthentication(context);
        ConfigureVirtualFileSystem(hostingEnvironment);
        ConfigureNavigationServices();
        ConfigureAutoApiControllers();
        ConfigureSwaggerServices(context.Services);

        Configure<PermissionManagementOptions>(options => { options.IsDynamicPermissionStoreEnabled = true; });
    }

    private void ConfigureCookieConsent(ServiceConfigurationContext context)
    {
        context.Services.AddAbpCookieConsent(options =>
        {
            options.IsEnabled = true;
            options.CookiePolicyUrl = "/CookiePolicy";
            options.PrivacyPolicyUrl = "/PrivacyPolicy";
        });
    }


    private void ConfigureHealthChecks(ServiceConfigurationContext context)
    {
        context.Services.AddBookStoreHealthChecks();
    }

    private void ConfigureStudio(IHostEnvironment hostingEnvironment)
    {
        if (hostingEnvironment.IsProduction())
        {
            Configure<AbpStudioClientOptions>(options => { options.IsLinkEnabled = false; });
        }
    }

    private void ConfigureBundles()
    {
        Configure<AbpBundlingOptions>(options =>
        {
            options.StyleBundles.Configure(
                LeptonXLiteThemeBundles.Styles.Global,
                bundle => { bundle.AddFiles("/global-styles.css"); }
            );

            options.ScriptBundles.Configure(
                LeptonXLiteThemeBundles.Scripts.Global,
                bundle => { bundle.AddFiles("/global-scripts.js"); }
            );
        });
    }

    private void ConfigurePages(IConfiguration configuration)
    {
        Configure<RazorPagesOptions>(options =>
        {
            options.Conventions.AuthorizePage("/HostDashboard", BookStorePermissions.Dashboard.Host);
            options.Conventions.AuthorizePage("/TenantDashboard", BookStorePermissions.Dashboard.Tenant);
        });
    }

    private void ConfigureUrls(IConfiguration configuration)
    {
        Configure<AppUrlOptions>(options => { options.Applications["MVC"].RootUrl = configuration["App:SelfUrl"]; });
    }

    private void ConfigureAuthentication(ServiceConfigurationContext context)
    {
        context.Services.ForwardIdentityAuthenticationForBearer(OpenIddictValidationAspNetCoreDefaults
            .AuthenticationScheme);
        context.Services.Configure<AbpClaimsPrincipalFactoryOptions>(options =>
        {
            options.IsDynamicClaimsEnabled = true;
        });
    }

    private void ConfigureImpersonation(ServiceConfigurationContext context, IConfiguration configuration)
    {
        context.Services.Configure<AbpSaasHostWebOptions>(options => { options.EnableTenantImpersonation = true; });
        context.Services.Configure<AbpIdentityWebOptions>(options => { options.EnableUserImpersonation = true; });
        context.Services.Configure<AbpAccountOptions>(options =>
        {
            options.TenantAdminUserName = "admin";
            options.ImpersonationTenantPermission = SaasHostPermissions.Tenants.Impersonation;
            options.ImpersonationUserPermission = IdentityPermissions.Users.Impersonation;
        });
    }

    private void ConfigureVirtualFileSystem(IWebHostEnvironment hostingEnvironment)
    {
        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<BookStoreWebModule>();

            if (hostingEnvironment.IsDevelopment())
            {
                options.FileSets.ReplaceEmbeddedByPhysical<BookStoreDomainSharedModule>(
                    Path.Combine(hostingEnvironment.ContentRootPath,
                        string.Format("..{0}Acme.BookStore.Domain.Shared", Path.DirectorySeparatorChar)));
                options.FileSets.ReplaceEmbeddedByPhysical<BookStoreDomainModule>(
                    Path.Combine(hostingEnvironment.ContentRootPath,
                        string.Format("..{0}Acme.BookStore.Domain", Path.DirectorySeparatorChar)));
                options.FileSets.ReplaceEmbeddedByPhysical<BookStoreApplicationContractsModule>(
                    Path.Combine(hostingEnvironment.ContentRootPath,
                        string.Format("..{0}Acme.BookStore.Application.Contracts", Path.DirectorySeparatorChar)));
                options.FileSets.ReplaceEmbeddedByPhysical<BookStoreApplicationModule>(
                    Path.Combine(hostingEnvironment.ContentRootPath,
                        string.Format("..{0}Acme.BookStore.Application", Path.DirectorySeparatorChar)));
                options.FileSets.ReplaceEmbeddedByPhysical<BookStoreHttpApiModule>(
                    Path.Combine(hostingEnvironment.ContentRootPath,
                        string.Format("..{0}..{0}src{0}Acme.BookStore.HttpApi", Path.DirectorySeparatorChar)));
                options.FileSets.ReplaceEmbeddedByPhysical<BookStoreWebModule>(hostingEnvironment.ContentRootPath);
            }
        });
    }

    private void ConfigureNavigationServices()
    {
        Configure<AbpNavigationOptions>(options => { options.MenuContributors.Add(new BookStoreMenuContributor()); });

        Configure<AbpToolbarOptions>(options => { options.Contributors.Add(new BookStoreToolbarContributor()); });
    }

    private void ConfigureAutoApiControllers()
    {
        Configure<AbpAspNetCoreMvcOptions>(options =>
        {
            options.ConventionalControllers.Create(typeof(BookStoreApplicationModule).Assembly);
        });
    }

    private void ConfigureSwaggerServices(IServiceCollection services)
    {
        services.AddAbpSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo { Title = "BookStore API", Version = "v1" });
                options.DocInclusionPredicate((docName, description) => true);
                options.CustomSchemaIds(type => type.FullName);
            }
        );
    }

    private void ConfigureExternalProviders(ServiceConfigurationContext context)
    {
        context.Services.AddAuthentication()
            .AddGoogle(GoogleDefaults.AuthenticationScheme,
                options => { options.ClaimActions.MapJsonKey(AbpClaimTypes.Picture, "picture"); })
            .WithDynamicOptions<GoogleOptions, GoogleHandler>(
                GoogleDefaults.AuthenticationScheme,
                options =>
                {
                    options.WithProperty(x => x.ClientId);
                    options.WithProperty(x => x.ClientSecret, isSecret: true);
                }
            )
            .AddMicrosoftAccount(MicrosoftAccountDefaults.AuthenticationScheme, options =>
            {
                //Personal Microsoft accounts as an example.
                options.AuthorizationEndpoint = "https://login.microsoftonline.com/consumers/oauth2/v2.0/authorize";
                options.TokenEndpoint = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";

                options.ClaimActions.MapCustomJson("picture", _ => "https://graph.microsoft.com/v1.0/me/photo/$value");
                options.SaveTokens = true;
            })
            .WithDynamicOptions<MicrosoftAccountOptions, MicrosoftAccountHandler>(
                MicrosoftAccountDefaults.AuthenticationScheme,
                options =>
                {
                    options.WithProperty(x => x.ClientId);
                    options.WithProperty(x => x.ClientSecret, isSecret: true);
                }
            )
            .AddTwitter(TwitterDefaults.AuthenticationScheme, options =>
            {
                options.ClaimActions.MapJsonKey(AbpClaimTypes.Picture, "profile_image_url_https");
                options.RetrieveUserDetails = true;
            })
            .WithDynamicOptions<TwitterOptions, TwitterHandler>(
                TwitterDefaults.AuthenticationScheme,
                options =>
                {
                    options.WithProperty(x => x.ConsumerKey);
                    options.WithProperty(x => x.ConsumerSecret, isSecret: true);
                }
            );
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();
        var env = context.GetEnvironment();

        app.UseForwardedHeaders();

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseAbpRequestLocalization();

        if (!env.IsDevelopment())
        {
            app.UseErrorPage();
            app.UseHsts();
        }

        app.UseAbpCookieConsent();
        app.UseCorrelationId();
        app.UseRouting();
        // app.MapAbpStaticAssets();
        app.UseAbpStudioLink();
        app.UseAbpSecurityHeaders();
        app.UseAuthentication();
        app.UseAbpOpenIddictValidation();

        if (MultiTenancyConsts.IsEnabled)
        {
            app.UseMultiTenancy();
        }

        app.UseUnitOfWork();
        app.UseDynamicClaims();
        app.UseAuthorization();
        app.UseSwagger();
        app.UseAbpSwaggerUI(options => { options.SwaggerEndpoint("/swagger/v1/swagger.json", "BookStore API"); });
        app.UseAuditing();
        app.UseAbpSerilogEnrichers();
        app.UseAbpTickerQ();
        app.UseConfiguredEndpoints();
    }
}