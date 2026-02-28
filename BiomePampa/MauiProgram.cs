using Microsoft.Extensions.Logging;
using BiomePampa.Application.Services;
using BiomePampa.Infrastructure.Repositories;
using BiomePampa.Infrastructure.Data;
using BiomePampa.ViewModels;
using BiomePampa.Views;
using Microsoft.EntityFrameworkCore;

namespace BiomePampa
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
            builder.Logging.AddDebug();
#endif

            // Database
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "biomepampa.db");
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite($"Data Source={dbPath}"));

            // Repositories
            builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

            // Services
            builder.Services.AddScoped<IProductService, ProductService>();
            builder.Services.AddScoped<IBatchService, BatchService>();
            builder.Services.AddScoped<ICustomerService, CustomerService>();

            // ViewModels
            builder.Services.AddTransient<ProductListViewModel>();
            builder.Services.AddTransient<ProductDetailViewModel>();
            builder.Services.AddTransient<BatchListViewModel>();
            builder.Services.AddTransient<BatchDetailViewModel>();
            builder.Services.AddTransient<CustomerListViewModel>();
            builder.Services.AddTransient<CustomerDetailViewModel>();
            builder.Services.AddTransient<DatabaseInfoViewModel>();

            // Pages
            builder.Services.AddTransient<ProductListPage>();
            builder.Services.AddTransient<ProductDetailPage>();
            builder.Services.AddTransient<BatchListPage>();
            builder.Services.AddTransient<BatchDetailPage>();
            builder.Services.AddTransient<CustomerListPage>();
            builder.Services.AddTransient<CustomerDetailPage>();
            builder.Services.AddTransient<DatabaseInfoPage>();

            return builder.Build();
        }
    }
}
