using System;
using System.IO;
using System.Text.Json;
using System.Reflection;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using System.Text.Json.Serialization;

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

                Console.WriteLine($"- Визуализация: {definition.OutputVisualization?.Type ?? "нет"}");

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

            // Динамически компилируем код из файла
            var code = File.ReadAllText(algorithmFile);
            var compilation = CSharpCompilation.Create(algorithmName)
                .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                .AddReferences(
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(Dictionary<string, object>).Assembly.Location)
                )
                .AddSyntaxTrees(CSharpSyntaxTree.ParseText(code));

            using var ms = new MemoryStream();
            var result = compilation.Emit(ms);

            if (!result.Success)
            {
                var errors = string.Join("\n", result.Diagnostics);
                throw new InvalidOperationException($"Компиляция провалена:\n{errors}");
            }

            ms.Seek(0, SeekOrigin.Begin);
            var assembly = Assembly.Load(ms.ToArray());
            var algorithmType = assembly.GetType($"SheduleOpt.Algorithms.{algorithmName}.{algorithmName}");
            return algorithmType?.GetMethod("Main");
        }
    }
}