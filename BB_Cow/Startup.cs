using BB_Cow.Services;
using BBCowDataLibrary.SQL;
using Microsoft.Extensions.Caching.Memory;
using MudBlazor.Services;


namespace LP4U
{
    public class Startup
    {
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
            DatabaseService.GetDBStringFromCSV();
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMemoryCache();
            services.AddMudServices();
            services.AddSingleton<ClawTreatmentService>();
            services.AddSingleton<CowTreatmentService>();
            services.AddSingleton<PClawTreatmentService>();
            services.AddSingleton<PCowTreatmentService>();

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            services.AddSingleton<IMemoryCache>(memoryCache);

            services.AddRazorPages();
            services.AddServerSideBlazor();
            services.AddHttpContextAccessor();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (System.Diagnostics.Debugger.IsAttached)
            {
                Console.WriteLine("Debug");
            }
            else
            {
                Console.WriteLine("Release");
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseAuthentication();
            app.UseRouting();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapBlazorHub();
                endpoints.MapFallbackToPage("/_Host");
            });
        }
    }
}
