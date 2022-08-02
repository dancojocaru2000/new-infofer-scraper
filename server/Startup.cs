using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
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
			if (env.IsDevelopment()) {
				app.UseDeveloperExceptionPage();
				app.UseSwagger();
				app.UseSwaggerUI(c => {
					c.SwaggerEndpoint("/swagger/v3/swagger.json", "InfoTren Scraper v3");
					c.SwaggerEndpoint("/swagger/v2/swagger.json", "InfoTren Scraper v2");
					c.SwaggerEndpoint("/swagger/v1/swagger.json", "InfoTren Scraper v1");
				});
			}

			// app.UseHttpsRedirection();

			app.UseRouting();

			app.UseAuthorization();

			app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
		}
	}
}
