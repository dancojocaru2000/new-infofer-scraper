using System;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using MongoDB.Bson.Serialization.Conventions;
using Server.Models.Database;
using Server.Services.Implementations;
using Server.Services.Interfaces;

namespace Server {
	public class Startup {
		public Startup(IConfiguration configuration) {
			Configuration = configuration;
		}

		public IConfiguration Configuration { get; }

		// This method gets called by the runtime. Use this method to add services to the container.
		public void ConfigureServices(IServiceCollection services) {
			if ((Environment.GetEnvironmentVariable("INSIDE_DOCKER") ?? "").Length > 0) {
				services.Configure<ForwardedHeadersOptions>(options => {
					options.KnownProxies.Add(Dns.GetHostAddresses("host.docker.internal")[0]);
				});
			}

			services.Configure<MongoSettings>(Configuration.GetSection("TrainDataMongo"));
			var conventionPack = new ConventionPack { new CamelCaseElementNameConvention() };
			ConventionRegistry.Register("camelCase", conventionPack, _ => true);
			services.AddSingleton<IDataManager, DataManager>();
			services.AddSingleton<IDatabase, Database>();
			services.AddSingleton<NodaTime.IDateTimeZoneProvider>(NodaTime.DateTimeZoneProviders.Tzdb);
			services.AddControllers()
				.AddJsonOptions(options => {
					options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
				});
			services.AddSwaggerGen(c => {
				c.SwaggerDoc("v1", new OpenApiInfo { Title = "InfoTren Scraper", Version = "v1" });
				c.SwaggerDoc("v2", new OpenApiInfo { Title = "InfoTren Scraper", Version = "v2" });
				c.SwaggerDoc("v3", new OpenApiInfo { Title = "InfoTren Scraper", Version = "v3" });
			});
		}

		// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		public void Configure(IApplicationBuilder app, IWebHostEnvironment env) {
			app.UseForwardedHeaders(new ForwardedHeadersOptions {
				ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
			});

			if (env.IsDevelopment()) {
				app.UseDeveloperExceptionPage();
			}

			app.UseSwagger();
			app.UseSwaggerUI(c => {
				c.SwaggerEndpoint("/swagger/v3/swagger.json", "InfoTren Scraper v3");
				c.SwaggerEndpoint("/swagger/v2/swagger.json", "InfoTren Scraper v2");
				c.SwaggerEndpoint("/swagger/v1/swagger.json", "InfoTren Scraper v1");
			});

			app.MapWhen(x => x.Request.Path.StartsWithSegments("/rapidoc"), appBuilder => {
				appBuilder.Run(async context => {
					context.Response.ContentType = "text/html";
					
					await context.Response.WriteAsync(
						"""
						<!doctype html> <!-- Important: must specify -->
						<html>
						<head>
						  <meta charset="utf-8"> <!-- Important: rapi-doc uses utf8 characters -->
						  <script type="module" src="https://unpkg.com/rapidoc/dist/rapidoc-min.js"></script>
						</head>
						<body>
						  <rapi-doc
							spec-url="/swagger/v3/swagger.json"
							theme = "dark"
						  > </rapi-doc>
						</body>
						</html> 
						"""
					);
				});
			});

			// app.UseHttpsRedirection();

			app.UseRouting();

			app.UseAuthorization();

			app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
		}
	}
}
