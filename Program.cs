using Microsoft.OpenApi.Models;
using Optimizator.Models;
using Optimizator.Services;

namespace Optimizator
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            var algorithmsPath = Path.Combine(builder.Environment.ContentRootPath, "Algorithms");
            if (!Directory.Exists(algorithmsPath))
            {
                Directory.CreateDirectory(algorithmsPath);
            }

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                });
            });

            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            builder.Services.AddSingleton<AlgorithmBuilder>(_ =>
                new AlgorithmBuilder(algorithmsPath));

            builder.Services.AddSingleton<AlgorithmCollection>(provider =>
            {
                var builder = provider.GetRequiredService<AlgorithmBuilder>();
                return new AlgorithmCollection(algorithmsPath, builder);
            });

            builder.Services.AddScoped<IAlgorithmService, AlgorithmService>();

            var app = builder.Build();

            // Проверка загрузки алгоритмов - вывод в консоли
            using (var scope = app.Services.CreateScope())
            {
                var algorithmCollection = scope.ServiceProvider.GetRequiredService<AlgorithmCollection>();
                try
                {
                    var algorithms = algorithmCollection.GetAllAlgorithms();
                    Console.WriteLine($"Загружено {algorithms.Count} алгоритмов:");
                    foreach (var alg in algorithms)
                    {
                        Console.WriteLine($"- {alg.Name}: {alg.Title}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка загрузки алгоритмов: {ex.Message}");
                }
            }

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwagger();
            }

            app.UseHttpsRedirection();
            app.UseCors("AllowAll");
            app.UseAuthorization();

            app.MapControllers();
            app.Run();
        }
    }
}