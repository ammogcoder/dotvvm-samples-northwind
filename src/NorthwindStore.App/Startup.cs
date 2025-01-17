using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Castle.Facilities.TypedFactory;
using Castle.Windsor;
using Castle.Windsor.Installer;
using Castle.Windsor.MsDependencyInjection;
using DotVVM.Framework.Hosting;
using DotVVM.Tracing.ApplicationInsights.AspNetCore;
using DotVVM.Tracing.MiniProfiler.AspNetCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NorthwindStore.App.Installers;
using StackExchange.Profiling;
using StackExchange.Profiling.Storage;
using DotVVM.Framework.Controls.DynamicData;
using DotVVM.Framework.Controls.DynamicData.Configuration;
using NorthwindStore.App.Controls;
using DotVVM.Framework.Controls.DynamicData.PropertyHandlers.FormEditors;
using NorthwindStore.App.Resources;

namespace NorthwindStore.App
{
    public class Startup
    {
        private static WindsorContainer container;


        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            services.AddDataProtection();
            services.AddAuthorization();
            services.AddWebEncoders();

            services.AddDotVVM(options =>
            {
                options.AddDefaultTempStorages("Temp");
                options.AddMiniProfilerEventTracing();
                options.AddApplicationInsightsTracing();

                var dynamicDataConfig = new AppDynamicDataConfiguration();
                options.AddDynamicData(dynamicDataConfig);
            });

            services
                .AddAuthentication("Cookie")
                .AddCookie("Cookie", options =>
                {
                    options.LoginPath = new PathString("/");
                    options.Events = new CookieAuthenticationEvents
                    {
                        OnRedirectToReturnUrl = c => DotvvmAuthenticationHelper.ApplyRedirectResponse(c.HttpContext, c.RedirectUri),
                        OnRedirectToAccessDenied = c => DotvvmAuthenticationHelper.ApplyStatusCodeResponse(c.HttpContext, 403),
                        OnRedirectToLogin = c => DotvvmAuthenticationHelper.ApplyRedirectResponse(c.HttpContext, c.RedirectUri),
                        OnRedirectToLogout = c => DotvvmAuthenticationHelper.ApplyRedirectResponse(c.HttpContext, c.RedirectUri)
                    };
                });

            services.AddMemoryCache();

            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            return WindsorRegistrationHelper.CreateServiceProvider(InitializeWindsor(), services);
        }

        private static IWindsorContainer InitializeWindsor()
        {
            container = new WindsorContainer();
            container.AddFacility<TypedFactoryFacility>();
            container.Install(FromAssembly.Containing<Startup>());

            AutoMapperInstaller.InitAutoMapper(container);

            return container;
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole();

            app.UseMiniProfiler(options =>
            {
                options.RouteBasePath = "~/profiler";
                options.Storage = new MemoryCacheStorage(new MemoryCache(new MemoryCacheOptions()), TimeSpan.FromMinutes(60));
            });

            app.UseAuthentication();

            // use DotVVM
            var dotvvmConfiguration = app.UseDotVVM<DotvvmStartup>(env.ContentRootPath);

            // use static files
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(env.WebRootPath)
            });
        }

        internal static T Resolve<T>()
        {
            return container.Resolve<T>();
        }
    }
}
