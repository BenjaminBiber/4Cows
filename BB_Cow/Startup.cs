//using Microsoft.Extensions.Caching.Memory;
//using MudBlazor.Services;


//namespace LP4U
//{
//    public class Startup
//    {
//        public IConfiguration Configuration { get; }

//        public Startup(IConfiguration configuration)
//        {
//            Configuration = configuration;
//        }

//        public void ConfigureServices(IServiceCollection services)
//        {
//            services.AddMemoryCache();
//            services.AddMudServices();

//            var memoryCache = new MemoryCache(new MemoryCacheOptions());
//            services.AddSingleton<IMemoryCache>(memoryCache);

//            services.AddRazorPages();
//            services.AddServerSideBlazor();
//            services.AddHttpContextAccessor();
//        }

//        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
//        {
//            if (System.Diagnostics.Debugger.IsAttached)
//            {
//                Console.WriteLine("Debug");
//                InitializeLogger(true);
//            }
//            else
//            {
//                Console.WriteLine("Release");
//                InitializeLogger();
//            }

//            app.UseHttpsRedirection();
//            app.UseStaticFiles();
//            app.UseAuthentication();
//            app.UseRouting();
//            app.UseAuthorization();

//            app.UseEndpoints(endpoints =>
//            {
//                endpoints.MapControllers();
//                endpoints.MapBlazorHub();
//                endpoints.MapFallbackToPage("/_Host");
//            });
//        }

//        private static void InitializeLogger(bool debugMode = false)
//        {
//            string loggerPath = AppDomain.CurrentDomain.BaseDirectory + @"\4Cows_LOGS\";
//            if (!Directory.Exists(loggerPath))
//            {
//                Directory.CreateDirectory(loggerPath);
//            }

//            loggerPath += "LP4U_.txt";
//            if (debugMode)
//            {
//                Log.Logger = new LoggerConfiguration()
//                    .MinimumLevel.Debug()
//                    .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Information)
//                    .WriteTo.File(loggerPath, rollingInterval: RollingInterval.Day)
//                    .CreateLogger();
//            }
//            else
//            {
//                Log.Logger = new LoggerConfiguration()
//                    .MinimumLevel.Information()
//                    .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Information)
//                    .WriteTo.File(loggerPath, rollingInterval: RollingInterval.Day)
//                    .CreateLogger();
//            }

//            Log.Information("Init Logger");
//        }
//    }
//}
