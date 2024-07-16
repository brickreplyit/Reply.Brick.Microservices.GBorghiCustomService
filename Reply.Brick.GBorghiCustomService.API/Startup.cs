using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using Newtonsoft.Json;

using Reply.Brick.BaseApi.Auth;
using Reply.Brick.BaseApi.Extensions;
using Reply.Brick.GBorghiCustomService.Service.Implementations;
using Reply.Brick.GBorghiCustomService.Service.Interfaces;
using Reply.Brick.Infrastructure.Configurations;
using Reply.Brick.Infrastructure.Interfaces.Services;
using Reply.Brick.Infrastructure.Persistance;
using System;
using System.Linq;
using System.Reflection;
using System.Text;

using Reply.Brick.Infrastructure.Service;
using NServiceBus;
using NServiceBus.Features;
using Reply.Brick.Messages;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Http;
using Reply.Brick.GBorghiCustomService.API;
using Microsoft.AspNetCore.Authentication;
using System.Collections.Generic;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Reply.Brick.Messages.Messages.FromEventManager;

namespace Reply.Brick.CSPSConnector
{
    public class Startup
    {
       // private object topologyRabbitMQ;
        private static AuthSettings authsetting;

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        //private static AuthSettings authsetting;
        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            #region Data access
            string connectionString = Configuration.GetConnectionString("DefaultConnection");
            string redisconnectionstring = Configuration.GetValue<string>("RedisConnectionString");
            string mongoConnection = Configuration.GetValue<string>("mongoConnection");
            int redisInitialDB = Configuration.GetValue<int>("RedisInitialDB");
            string mongoDB = Configuration.GetValue<string>("mongoDB");
            #endregion

            services.AddCors(Configuration);

            if (Configuration.GetValue<bool>("ConnectionEncrypted"))
            {
                connectionString = Infrastructure.Utils.EncryptUtils.DecryptString(connectionString);
                redisconnectionstring = Infrastructure.Utils.EncryptUtils.DecryptString(redisconnectionstring);
                mongoConnection = Infrastructure.Utils.EncryptUtils.DecryptString(mongoConnection);
            }

            #region Swagger
            services.AddSwaggerGen(options =>
            {
                options.AddSecurityDefinition(name: "Bearer", securityScheme: new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Description = "Enter the Bearer Authorization string as following: `Bearer Generated-JWT-Token`",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                });
                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Name = "Bearer",
                            In = ParameterLocation.Header,
                            Reference = new OpenApiReference
                            {
                                Id = "Bearer",
                                Type = ReferenceType.SecurityScheme
                            }
                        },
                        new List<string>()
                    }
                });
            });
            #endregion

            #region Authentication
            if (authsetting == null)
            {
                authsetting = new AuthSettings(Configuration);
                authsetting.IsIdentityServerActive = false; //viene basato su IdentityServerType
            }

            var identityServerType = Configuration.GetValue<string>("IdentityServerType");
            switch (identityServerType)
            {
                // autenticazione basata su userId che viene mandato nella Query string(UserIdentify) o nel Header
                case "NoAuth":
                    services.AddAuthentication("NoAuth")
                        .AddScheme<AuthenticationSchemeOptions, AuthenticationHandler>("NoAuth", null);
                    break;
                // autenticazione basata keycloak
                case "Keycloak":
                    authsetting.IsIdentityServerActive = true;
                    break;
                // autenticazione basata su userId che viene mandato nella Query string(UserIdentify) o nel Header e su keycloak
                /*case "Hybrid":
                    services.AddJwtBearerAndUserIdAuthentication(authsetting);
                    break;*/
                default:
                    throw new ArgumentNullException("IdentityServerType not valorized in appsettings");
            }
            IList<string> validissuers = new List<string>()
                {
                    Configuration["Jwt:Authority"],
                };
            if (authsetting.IsIdentityServerActive)
            {
                var configManager = new ConfigurationManager<OpenIdConnectConfiguration>($"{validissuers.Last()}/.well-known/openid-configuration", new OpenIdConnectConfigurationRetriever());
                var openidconfig = configManager.GetConfigurationAsync().Result;
                services.AddAuthentication(o =>
                {
                    o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, o =>

                {
                    o.MetadataAddress = Configuration["Jwt:Authority"];
                    o.RequireHttpsMetadata = false; // only for dev
                    o.SaveToken = true;
                    o.TokenValidationParameters = new TokenValidationParameters()
                    {
                        ValidAudience = "account",
                        ValidateAudience = true,
                        ValidateIssuer = true,
                        ValidIssuers = new[] { Configuration["Jwt:Authority"] },

                        ValidateIssuerSigningKey = true,
                        IssuerSigningKeys = openidconfig.SigningKeys,

                        RequireExpirationTime = true,
                        ValidateLifetime = true,
                        RequireSignedTokens = true,
                    };
                    o.Events = new JwtBearerEvents()
                    {
                        OnAuthenticationFailed = c =>
                        {
                            c.NoResult();
                            return c.Response.WriteAsync("An error occured processing your authentication.");
                        }
                    };
                });
            }
            #endregion

            #region Providers
            //services.AddTransient<IImporterProvider, ImporterProvider>(f => new ImporterProvider(connectionString));
            #endregion

            #region Services
            services.AddTransient<IMessageService, MessageService>();
            services.AddTransient<IPrintService, PrintService>();
            #endregion

            #region HostedServices
            /*
            if (Configuration.GetValue<bool>("StartHostService"))
            {
                services.AddHostedService<WatcherFileService>();
            }
            */
            #endregion

            #region Brick Configuration
            services.AddSingleton<IBrickConfigurations>(f => new BrickConfigurations(connectionString, redisconnectionstring, redisInitialDB, mongoConnection, mongoDB));
            services.AddTransient<IBrickPersistanceLayer<ObjectId>, BrickPersistanceLayer<ObjectId>>();
            services.AddScoped<ISubscriptionService, SubscriptionService>();
            #endregion

            if (!authsetting.IsIdentityServerActive)
            {
                services.AddMvc(opts =>
                {
                    opts.Filters.Add(new AllowAnonymousFilter());
                });
            }
            else
            {
                services.AddMvc();
            }

            ContainerBuilder containerBuilder = new ContainerBuilder();
            containerBuilder.Populate(services);

            AutofacServiceProvider autofacProvider = null;
            if (Configuration.GetValue<bool>("ENABLE_SUBSCRIPTION"))
            {
                var container = containerBuilder.Build();
                //Subscribe();
                autofacProvider = new AutofacServiceProvider(container);
            }
            else
            {
                // NServiceBus
                var container = RegisterEventBus(containerBuilder);
                autofacProvider = new AutofacServiceProvider(container);
            }

            return autofacProvider;
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            System.Text.Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            #region Swagger
            if (Configuration.GetValue<bool>("activateSwagger"))
            {
                app.UseSwagger()
                 .UseSwaggerUI(c =>
                 {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "CSPSConnector V1");
                 });
            }
            #endregion

            #region CORS and SSL

            app.UseRouting();
            app.UseCors("CORS");
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapRazorPages();
                endpoints.MapControllers();
            });
            // Mutual TLS/SSL authentication
            bool useClientCertificateValidation = Configuration.GetValue<bool>("CertificateValidation:IsActive");
            if (useClientCertificateValidation)
            {
                app.UseClientCertificateValidationMiddleware();
            }
            #endregion
        }

        private Autofac.IContainer RegisterEventBus(ContainerBuilder containerBuilder)
        {
            IEndpointInstance endpoint = null;
            containerBuilder.Register(c => endpoint)
                .As<IEndpointInstance>()
                .SingleInstance();

            var container = containerBuilder.Build();

            //Endpoint configuration
            var endpointInstance = Configuration.GetSection("EndpointSettings")["EndpointInstance"];
            var endpointConfiguration = new EndpointConfiguration(endpointInstance);

            //Configure RabbitMQ transport
            var transport = endpointConfiguration.UseTransport<RabbitMQTransport>();
            transport.UseConventionalRoutingTopology();
            transport.ConnectionString(GetRabbitConnectionString());

            //Configure persistence
            /*var persistence = endpointConfiguration.UsePersistence<SqlPersistence>();
            persistence.SqlDialect<SqlDialect.MsSqlServer>().Schema("");

            persistence.ConnectionBuilder(connectionBuilder:
                () => new SqlConnection(connectionString));*/

            //User JSON.NET serializer
            endpointConfiguration.UseSerialization<NewtonsoftSerializer>();

            #region OutBox
            bool useOutBox = false;
            bool.TryParse(Configuration.GetSection("EndpointSettings")["UseOutBox"], out useOutBox);
            if (useOutBox)
            {
                var outboxConf = endpointConfiguration.EnableOutbox();
                outboxConf.KeepDeduplicationDataFor(TimeSpan.FromDays(1));
                outboxConf.RunDeduplicationDataCleanupEvery(TimeSpan.FromMinutes(2));
            }
            #endregion

            #region Audit
            //Turn on auditing
            bool useAudit = false;
            bool.TryParse(Configuration.GetSection("EndpointSettings")["UseAudit"], out useAudit);

            if (useAudit)
            {
                int ttl = 0;
                if (int.TryParse(Configuration.GetSection("EndpointSettings")["AuditTTL"], out ttl) == false)
                {
                    ttl = 5;
                }

                var auditQueue = Configuration.GetSection("EndpointSettings")["Audit"];
                endpointConfiguration.AuditProcessedMessagesTo(auditQueue, TimeSpan.FromMinutes(ttl));
            }
            #endregion

            //Make sure NServiceBus creates queues in RabbitMQ table in SQL Server etc.
            endpointConfiguration.EnableInstallers();

            //Error
            var error = Configuration.GetSection("EndpointSettings")["Error"];
            endpointConfiguration.SendFailedMessagesTo(error);

            // Configure the DI container.
            endpointConfiguration.UseContainer<AutofacBuilder>(customizations: customizations =>
            {
                customizations.ExistingLifetimeScope(container);
            });

            var settings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto,
                DateTimeZoneHandling = DateTimeZoneHandling.RoundtripKind
            };
            var serializer = endpointConfiguration.UseSerialization<NewtonsoftSerializer>();
            serializer.Settings(settings);

            // Start the endpoint and register it with ASP.NET Core DI
            var topologyRabbitMQ = transport.UseConventionalRoutingTopology();
            var routingRabbitMq = topologyRabbitMQ.Routing();

            var discriminator = Configuration.GetSection("EndpointSettings")["Discriminator"];
            endpointConfiguration.MakeInstanceUniquelyAddressable(discriminator);

            #region Routes for the requests
            //Requests (Bacth e Serials)
            var mappings = Configuration.GetSection("BusRequestMapping").Get<BusRequestMapping[]>();

            foreach (BusRequestMapping mapping in mappings)
            {
                foreach (string ns in mapping.Namespaces)
                {
                    routingRabbitMq.RouteToEndpoint(
                       assembly: Assembly.Load(mapping.AssemblyPath),
                       @namespace: ns,
                       destination: mapping.Destination);
                }
            }
            #endregion

            endpointConfiguration.EnableCallbacks();

            #region Unsubscribe tu events
            endpointConfiguration.DisableFeature<AutoSubscribe>();
            endpoint = NServiceBus.Endpoint.Start(endpointConfiguration).GetAwaiter().GetResult();

            List<Type> events = 
                Assembly.Load("Reply.Brick.GBorghiCustomService.Service").GetTypes().Where(x => !String.IsNullOrEmpty(x.Namespace) && x.Namespace.Equals("Reply.Brick.IntegrationEvents") && !x.Name.Equals("BaseIntegrationEvent")).ToList();
            List<Type> messageOperation = Assembly.Load("Reply.Brick.Messages").GetTypes().Where(x => x.Namespace.Equals("Reply.Brick.Messages.Events.Declaration") && !x.Name.Equals("BaseEvent")).ToList();

            if (messageOperation != null && messageOperation.Any())
            {
                events.AddRange(messageOperation);
            }

            #region ProductionCall and ProductionReturn Event
            List<Type> productionEvent = Assembly.Load("Reply.Brick.Messages").GetTypes().Where(x => x.Namespace.Equals("Reply.Brick.Messages.Messages.FromWarehouse") && !x.Name.Equals("BaseEvent")).ToList();

            if (productionEvent != null && productionEvent.Any())
            {
                events.AddRange(productionEvent);
            }
            #endregion

            events.ForEach(f =>
            {
                endpoint.Unsubscribe(f).GetAwaiter().GetResult();
            });
            #endregion

            #region Subscribe to Events
            events = new List<Type>();
            events.Add(typeof(BrickEventIntegrationEvent));
            events.ForEach(f =>
            {
                endpoint.Subscribe(f).GetAwaiter().GetResult();
            });
            #endregion

            return container;
        }

        private string GetRabbitConnectionString()
        {
            var host = Configuration["EventBusConnection"];
            var user = Configuration["EventBusUserName"];
            var password = Configuration["EventBusPassword"];

            if (string.IsNullOrEmpty(user))
                return $"host={host}";

            return $"host={host};username={user};password={password};RequestedHeartbeat=100;";
        }
    }
}
