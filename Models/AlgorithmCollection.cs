using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Optimizator.Models
{
    public class AlgorithmCollection
    {
        private readonly Dictionary<string, AlgorithmDefinition> _algorithms = new();
        private readonly AlgorithmBuilder _builder;
        private readonly string _algorithmsPath;

        public AlgorithmCollection(string algorithmsPath, AlgorithmBuilder builder)
        {
            _algorithmsPath = algorithmsPath;
            _builder = builder;
            LoadAlgorithms();
        }

        private void LoadAlgorithms()
        {
            _algorithms.Clear();

            if (!Directory.Exists(_builder._algorithmsPath))
            {
                throw new DirectoryNotFoundException($"Папка с алгоритмами не найдена: {_builder._algorithmsPath}");
            }

            foreach (var algorithmDir in Directory.GetDirectories(_builder._algorithmsPath))
            {
                try
                {
                    var algorithmName = Path.GetFileName(algorithmDir);
                    var definition = _builder.BuildAlgorithm(algorithmName);
                    _algorithms[algorithmName] = definition;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка загрузки алгоритма: {ex.Message}");
                }
            }
        }

        public List<AlgorithmInfo> GetAllAlgorithms()
        {
            return _algorithms.Values.Select(a => new AlgorithmInfo
            {
                Name = a.Name,
                Title = a.Title,
                Description = a.Description
            }).ToList();
        }

        public AlgorithmDefinition GetAlgorithm(string algorithmName)
        {
            if (string.IsNullOrWhiteSpace(algorithmName))
                throw new ArgumentException("имя алгоритма не может быть пустым");

            return _algorithms.TryGetValue(algorithmName, out var algorithm)
                ? algorithm
                : throw new KeyNotFoundException($"Алгоритм '{algorithmName}' не найден");
        }
    }

    public class AlgorithmInfo
    {
        public string Name { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
    }
}