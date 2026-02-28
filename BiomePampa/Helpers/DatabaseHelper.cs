using BiomePampa.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BiomePampa.Helpers
{
    public static class DatabaseHelper
    {
        public static string GetDatabasePath()
        {
            return Path.Combine(FileSystem.AppDataDirectory, "biomepampa.db");
        }

        public static async Task<bool> DatabaseExistsAsync()
        {
            var dbPath = GetDatabasePath();
            return await Task.Run(() => File.Exists(dbPath));
        }

        public static async Task<long> GetDatabaseSizeAsync()
        {
            var dbPath = GetDatabasePath();
            if (!File.Exists(dbPath))
                return 0;

            var fileInfo = new FileInfo(dbPath);
            return await Task.Run(() => fileInfo.Length);
        }

        public static async Task<bool> InitializeDatabaseAsync(ApplicationDbContext context)
        {
            try
            {
                // Cria o banco de dados se não existir
                await context.Database.EnsureCreatedAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<bool> DeleteDatabaseAsync()
        {
            try
            {
                var dbPath = GetDatabasePath();
                if (File.Exists(dbPath))
                {
                    await Task.Run(() => File.Delete(dbPath));
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<string> GetDatabaseInfoAsync(ApplicationDbContext context)
        {
            var dbPath = GetDatabasePath();
            var exists = await DatabaseExistsAsync();
            var size = await GetDatabaseSizeAsync();

            var info = $"Database Path: {dbPath}\n";
            info += $"Exists: {exists}\n";
            info += $"Size: {size / 1024.0:F2} KB\n";

            if (exists)
            {
                try
                {
                    var productsCount = await context.Products.CountAsync();
                    var batchesCount = await context.Batches.CountAsync();
                    var customersCount = await context.Customers.CountAsync();

                    info += $"\nRecords:\n";
                    info += $"- Products: {productsCount}\n";
                    info += $"- Batches: {batchesCount}\n";
                    info += $"- Customers: {customersCount}\n";
                }
                catch (Exception ex)
                {
                    info += $"\nError reading counts: {ex.Message}";
                }
            }

            return info;
        }
    }
}
