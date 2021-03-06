using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Constants;
using Microsoft.AspNetCore.Filters;
using Microsoft.AspNetCore.Middlewares;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Core;
using StackExchange.Redis;
using Swashbuckle.AspNetCore.Filters;
using Newtonsoft.Json;
using Koala.Core;

namespace Microsoft.AspNetCore.Extensions
{
    public static class AspNetCoreExtensions
    {
        public static string SwaggerPageTitle { get; set; }

        public static string SwaggerPageDescription { get; set; }

        public static string CorsPolicyName { get; set; } = "ArchiSharpAspNetCorsPolicy";

        public static string RedisConnectionStringName { get; set; } = "RedisInstance";

        public static string[] AllowedOrigins { get; set; } = new[]
        {
            "https://develop.app.archistar.io",
            "https://staging.app.archistar.io",
            "https://app.archistar.ai",
            "https://localhost",
            "http://localhost"
        };

        public static IHostBuilder GetHostBuilder<TStartup>(this Guid appGuid, int port, string[] args, out ILogger logger, out LoggingLevelSwitch loggingLevelSwitch, out IConfiguration configuration) where TStartup : AbstractStartup
        {
            var resolvedConfiguration = appGuid.GetAppConfiguration();
            var resolvedLogger = appGuid.GetLogger(out var resolvedLoggingLevelSwitch);

            configuration = resolvedConfiguration;
            logger = resolvedLogger;
            loggingLevelSwitch = resolvedLoggingLevelSwitch;
            
            AbstractStartup.AppGuid = appGuid;
            AbstractStartup.DefaultLogger = resolvedLogger;
            AbstractStartup.DefaultLoggingLevelSwitch = resolvedLoggingLevelSwitch;

            //if (false)
            //{
            //    var targetStartupClassType = typeof(TStartup);
            //    
            //    var loggerProp = targetStartupClassType.GetProperty(nameof(ArchiSharpStartup.DefaultLogger), BindingFlags.Public | BindingFlags.Static);
            //    loggerProp?.SetValue(targetStartupClassType, resolvedLogger, null);
            //    
            //    var loggingLevelSwitchProp = targetStartupClassType.GetProperty(nameof(ArchiSharpStartup.DefaultLoggingLevelSwitch), BindingFlags.Public | BindingFlags.Static);
            //    loggingLevelSwitchProp?.SetValue(targetStartupClassType, resolvedLoggingLevelSwitch, null);
            //}

            var webHostBuilder = Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder
                        .UseUrls($"http://*:{port}")
                        .UseKestrel()
                        .UseConfiguration(resolvedConfiguration)
                        .UseSerilog(resolvedLogger)
                        .UseStartup(typeof(TStartup));
                });

            Console.OutputEncoding = Encoding.UTF8;

            return webHostBuilder;
        }

        public static IServiceCollection ConfigureBasicServices(this IServiceCollection services, IConfiguration configuration, LoggingLevelSwitch loggingLevelSwitch, IList<Assembly> assemblies = null)
        {
            services.AddSingleton(configuration);
            services.AddSingleton(loggingLevelSwitch);
            
            services.AddCors(options =>
            {
                options.AddDefaultPolicy(builder =>
                {
                    builder
                        .SetIsOriginAllowed(
                            origin => AllowedOrigins.Contains(
                                origin,
                                StringComparer.InvariantCultureIgnoreCase
                            )
                        )
                        .AllowCredentials()
                        .AllowAnyMethod()
                        .AllowAnyHeader();
                });

                options.AddPolicy(CorsPolicyName, builder =>
                {
                    builder
                        .AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader();
                });
            });
            
            services.AddResponseCompression();
            
            services
                .AddControllers(options =>
                {
                    options.Conventions.Add(new ActionHidingConvention());
                })
                .AddNewtonsoftJson(options =>
                {
                    options.SerializerSettings.GetOrMergeDefaultJsonSerializerSettings();
                });

            services
                .AddControllersWithViews()
                .AddNewtonsoftJson(options =>
                {
                    options.SerializerSettings.GetOrMergeDefaultJsonSerializerSettings();
                });

            services
                .AddRazorPages()
                .AddNewtonsoftJson(options =>
                {
                    options.SerializerSettings.GetOrMergeDefaultJsonSerializerSettings();
                });

            services
                .AddMvc()
                .AddNewtonsoftJson(options =>
                {
                    options.SerializerSettings.GetOrMergeDefaultJsonSerializerSettings();
                });

            var redisConfigurationOptions = configuration.GetRedisConfigurationOptions(RedisConnectionStringName);

            if (redisConfigurationOptions != null)
            {
                services
                    .AddStackExchangeRedisCache(options =>
                    {
                        options.ConfigurationOptions = redisConfigurationOptions;
                    });

                services
                    .AddSingleton<IConnectionMultiplexer>(
                        ConnectionMultiplexer.Connect(redisConfigurationOptions)
                    );
            }

            services.AddMemoryCache();

            services.AddHttpContextAccessor();

            services.AddSwaggerGen(c =>
            {
                try
                {
                    var xmlFile = $"{Assembly.GetEntryAssembly()?.GetName().Name}.xml";
                    var xmlFilePath = Path.Combine(AppContext.BaseDirectory, xmlFile);

                    c.SwaggerDoc(SetConstants.ApiPageV1Version, new OpenApiInfo
                    {
                        Version = services.GetVersion(),
                        Title = SwaggerPageTitle ?? SetConstants.ApiPageV1Title,
                        Description = $"v{new{}.GetVersion()}<br><br>{SwaggerPageDescription ?? SetConstants.ApiPageV1Description}<br><br>{new {}.GetContainerIPsString()}<br>{new {}.GetHostNamesString()}",
                        TermsOfService = new Uri(SetConstants.ApiPageTermsLink),
                        Contact = new OpenApiContact
                        {
                            Name = SetConstants.ApiPageContactName,
                            Url = new Uri(SetConstants.ApiPageContactUrl),
                            Email = SetConstants.ApiPageContactEmail
                        }
                    });

                    c.ExampleFilters();

                    c.SchemaFilter<SchemaFilter>();
                    c.DocumentFilter<SwaggerPathPrefixFilter>();
                    c.OperationFilter<UploadFileOperationFilter>();

                    if (File.Exists(xmlFilePath))
                    {
                        c.IncludeXmlComments(xmlFilePath);
                    }

                    c.OperationFilter<RequestHeadersRequireSwaggerFilter>();
                }
                catch(Exception ex)
                {
                    Log.Logger.Error(ex, ex.Message);
                }
            });

            // Register Swagger Newtonsoft Support
            services.AddSwaggerGenNewtonsoftSupport();

            // Swagger Request Examples
            if (assemblies != null && assemblies.Any())
            {
                services.AddSwaggerExamplesFromAssemblies(assemblies.ToArray());
            }
            else
            {
                services.AddSwaggerExamplesFromAssemblies(Assembly.GetEntryAssembly());
            }
            
            return services;
        }

        public static IApplicationBuilder ConfigureBasicApp(this IApplicationBuilder app)
        {
            var rewriteOptions = new RewriteOptions()
                .AddRewrite("/api(.*)", "/api$1", true);

            app.UseRewriter(rewriteOptions);
            app.UseExceptionHandler($"/api/xerror/{new {}.IsDebugMode()}");
            app.UseStaticFiles();
            app.UseRouting();
            app.UseCors(CorsPolicyName);
            app.UseAuthorization();
            app.UseResponseCompression();
            
            app.UseMiddleware(typeof(RequestAuthorizationMiddleware));
            app.UseMiddleware(typeof(DefaultHeadersMiddleware));

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers()
                    .RequireCors(
                        CorsPolicyName
                    );
            });
            
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                try
                {
                    c.SwaggerEndpoint(SetConstants.ApiPageV1JsonPath, SetConstants.ApiPageV1Version);
                    c.RoutePrefix = SetConstants.ApiPageUrlPrefix;
                }
                catch (Exception ex)
                {
                    Log.Logger.Error(ex, ex.Message);
                }
            });

            return app;
        }

        public static ConfigurationOptions GetRedisConfigurationOptions(this IConfiguration configuration, string name)
        {
            var redisConnection = configuration.GetConnectionString(name);
            var redisEndpoint = default(string);
            var redisPassword = default(string);

            if (!string.IsNullOrWhiteSpace(redisConnection))
            {
                var redisConnectionInfo = redisConnection.Split(";").ForEach(x => x.Trim());

                redisEndpoint = redisConnectionInfo.Count >= 1
                    ? redisConnectionInfo[0]
                    : null;

                redisPassword = redisConnectionInfo.Count >= 2
                    ? redisConnectionInfo[1]
                    : null;
            }

            if (!string.IsNullOrWhiteSpace(redisEndpoint))
            {
                return new ConfigurationOptions
                {
                    EndPoints = { redisEndpoint },
                    Password = redisPassword,
                    AbortOnConnectFail = false,
                    ConnectTimeout = 1000,
                };
            }

            return null;
        }

        public static IConfigurationRoot GetAppConfiguration(this object input)
        {
            var configuration = new ConfigurationBuilder()
                    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                    .AddJsonFile("appsettings.json", true)
                    .AddEnvironmentVariables()
                    .Build();

            return configuration;
        }
    }
}