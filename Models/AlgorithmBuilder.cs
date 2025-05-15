using System;
using System.IO;
using System.Text.Json;
using System.Reflection;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using System.Text.Json.Serialization;
using System.Runtime.Loader;
using Optimizator.Models;

namespace Optimizator.Models
{
    public class AlgorithmBuilder
    {
        public readonly string _algorithmsPath;

        public AlgorithmBuilder(string algorithmsPath)
        {
            _algorithmsPath = algorithmsPath;
        }

        public AlgorithmDefinition BuildAlgorithm(string algorithmName)
        {
            var algorithmPath = Path.Combine(_algorithmsPath, algorithmName);
            var definitionPath = Path.Combine(algorithmPath, "definition.json");

            if (!File.Exists(definitionPath))
            {
                throw new FileNotFoundException($"Не найден файл с описанием для алгоритма {algorithmName}");
            }

            try
            {
                var json = File.ReadAllText(definitionPath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new JsonStringEnumConverter() }
                };

                var definition = JsonSerializer.Deserialize<AlgorithmDefinition>(json, options);
                definition.Name = algorithmName;

                Console.WriteLine($"Загружен алгоритм {algorithmName}:");
                Console.WriteLine($"- Параметры:");
                foreach (var param in definition.Parameters)
                {
                    Console.WriteLine($"  - {param.Name}: Тип данных={param.DataType}, Форма данных={param.DataShape}");
                }

                return definition;
            }
            catch (JsonException ex)
            {
                throw new JsonException($"Провалена попытка преобразовать описание для алгоритма {algorithmName}", ex);
            }
        }
        public MethodInfo GetAlgorithmMethod(string algorithmName)
        {
            var algorithmPath = Path.Combine(_algorithmsPath, algorithmName);
            var algorithmFile = Path.Combine(algorithmPath, $"{algorithmName}.cs");

            if (!File.Exists(algorithmFile))
            {
                throw new FileNotFoundException($"Не найдена реализация для алгоритма {algorithmName}");
            }

            // 1. Получаем все системные сборки, используемые в текущем домене
            var trustedAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator);
            var references = trustedAssemblies
                .Select(path => MetadataReference.CreateFromFile(path))
                .ToList();

            // 2. Добавляем специальные сборки, которые могут отсутствовать в списке
            var additionalAssemblies = new[]
            {
                typeof(object).Assembly,
                typeof(Enumerable).Assembly,
                typeof(System.Linq.Expressions.Expression).Assembly,
                typeof(System.Runtime.GCSettings).Assembly,
                typeof(Microsoft.CSharp.RuntimeBinder.RuntimeBinderException).Assembly,
                Assembly.GetExecutingAssembly()
            };

            for (int i = 0; i < additionalAssemblies.Length; i++)
            {
                if (!references.Any(r => r.Display == additionalAssemblies[i].Location))
                {
                    references.Add(MetadataReference.CreateFromFile(additionalAssemblies[i].Location));
                }
            }

            // 3. Настройки компиляции
            var compilationOptions = new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release,
                assemblyIdentityComparer: DesktopAssemblyIdentityComparer.Default);

            // 4. Компиляция
            var compilation = CSharpCompilation.Create(
                assemblyName: $"{algorithmName}_DynamicAssembly",
                syntaxTrees: new[] { CSharpSyntaxTree.ParseText(File.ReadAllText(algorithmFile)) },
                references: references,
                options: compilationOptions);

            using var ms = new MemoryStream();
            var emitResult = compilation.Emit(ms);

            // 5. Обработка ошибок компиляции
            if (!emitResult.Success)
            {
                var errors = emitResult.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => $"{d.Id}: {d.GetMessage()}")
                    .Distinct();
                throw new InvalidOperationException($"Ошибка компиляции:\n{string.Join("\n", errors)}");
            }

            ms.Seek(0, SeekOrigin.Begin);

            // 6. Загрузка сборки в контекст
            var assembly = Assembly.Load(ms.ToArray());

            // 7. Поиск метода Main
            var algorithmType = assembly.GetTypes()
                .FirstOrDefault(t => t.GetMethod("Main", BindingFlags.Public | BindingFlags.Static) != null);

            return algorithmType?.GetMethod("Main");
        }
    }
}