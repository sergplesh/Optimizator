using Optimizator.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace Optimizator.Services
{
    public interface IAlgorithmService
    {
        List<AlgorithmInfo> GetAllAlgorithms();
        AlgorithmDefinition GetAlgorithmDefinition(string name);
        Dictionary<string, object> ExecuteAlgorithm(string name, Dictionary<string, object> parameters);
    }

    public class AlgorithmService : IAlgorithmService
    {
        private readonly AlgorithmCollection _algorithmCollection;
        private readonly AlgorithmBuilder _algorithmBuilder;

        public AlgorithmService(AlgorithmCollection algorithmCollection, AlgorithmBuilder algorithmBuilder)
        {
            _algorithmCollection = algorithmCollection;
            _algorithmBuilder = algorithmBuilder;
        }

        public List<AlgorithmInfo> GetAllAlgorithms() => _algorithmCollection.GetAllAlgorithms();

        public AlgorithmDefinition GetAlgorithmDefinition(string name) => _algorithmCollection.GetAlgorithm(name);

        public Dictionary<string, object> ExecuteAlgorithm(string name, Dictionary<string, object> parameters)
        {
            try
            {
                var algorithm = _algorithmCollection.GetAlgorithm(name);
                var preparedParameters = PrepareParameters(parameters, algorithm);

                var finalParameters = new Dictionary<string, object>();
                foreach (var param in preparedParameters)
                {
                    finalParameters[param.Key] = param.Value;
                }

                var method = _algorithmBuilder.GetAlgorithmMethod(name);
                if (method == null)
                {
                    throw new InvalidOperationException($"Метод Main не найден для алгоритма {name}");
                }

                // параметры как Dictionary<string, object>
                var result = method.Invoke(null, new object[] { finalParameters });

                if (result is not Dictionary<string, object> resultDict)
                {
                    throw new InvalidOperationException("Алгоритм должен возвращать Dictionary<string, object>");
                }

                //ValidateOutputs(algorithm, resultDict);
                return resultDict;
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка выполнения алгоритма {name}: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        private Dictionary<string, object> PrepareParameters(Dictionary<string, object> rawParameters, AlgorithmDefinition algorithmDef)
        {
            var result = new Dictionary<string, object>();

            foreach (var paramDef in algorithmDef.Parameters)
            {
                if (!rawParameters.TryGetValue(paramDef.Name, out var paramValue))
                {
                    throw new ArgumentException($"Не указан параметр: {paramDef.Name}");
                }

                try
                {
                    object processedValue;

                    // Обработка в зависимости от формы данных
                    switch (paramDef.DataShape)
                    {
                        case DataShape.DYNAMIC_MATRIX:
                            processedValue = ProcessMatrixParameter(paramValue, paramDef);
                            break;

                        case DataShape.LIST:
                            processedValue = ProcessListParameter(paramValue, paramDef);
                            break;

                        default: // SCALAR
                            processedValue = ProcessScalarParameter(paramValue, paramDef);
                            break;
                    }

                    result[paramDef.Name] = processedValue;
                }
                catch (Exception ex)
                {
                    throw new ArgumentException($"Ошибка обработки параметра {paramDef.Name}: {ex.Message}");
                }
            }

            return result;
        }

        private object ProcessMatrixParameter(object paramValue, DataElement paramDef)
        {
            try
            {
                var matrix = new List<List<object>>();

                if (paramValue is string jsonString)
                {
                    // Десериализуем JSON напрямую в нужную структуру
                    var jsonArray = JsonSerializer.Deserialize<JsonElement>(jsonString);

                    foreach (var rowElement in jsonArray.EnumerateArray())
                    {
                        var row = new List<object>();
                        foreach (var cellElement in rowElement.EnumerateArray())
                        {
                            row.Add(ConvertJsonElement(cellElement, paramDef.DataType));
                        }
                        matrix.Add(row);
                    }
                }
                else if (paramValue is JsonElement jsonElement)
                {
                    foreach (var rowElement in jsonElement.EnumerateArray())
                    {
                        var row = new List<object>();
                        foreach (var cellElement in rowElement.EnumerateArray())
                        {
                            row.Add(ConvertJsonElement(cellElement, paramDef.DataType));
                        }
                        matrix.Add(row);
                    }
                }
                else if (paramValue is List<object> outerList)
                {
                    foreach (var rowObj in outerList)
                    {
                        if (rowObj is List<object> row)
                        {
                            matrix.Add(row);
                        }
                        else if (rowObj is JsonElement rowElement)
                        {
                            var convertedRow = new List<object>();
                            foreach (var cellElement in rowElement.EnumerateArray())
                            {
                                convertedRow.Add(ConvertJsonElement(cellElement, paramDef.DataType));
                            }
                            matrix.Add(convertedRow);
                        }
                    }
                }
                else
                {
                    throw new ArgumentException("Некорректный формат матрицы");
                }

                // Проверка размерности
                if (paramDef.Dimensions != null && paramDef.Dimensions.Cols is int cols)
                {
                    if (matrix.Any(row => row.Count != cols))
                    {
                        throw new ArgumentException($"Ожидается {cols} столбцов в матрице");
                    }
                }

                return matrix;
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Ошибка обработки матричного параметра: {ex.Message}");
            }
        }

        private object ProcessListParameter(object paramValue, DataElement paramDef)
        {
            List<object> list;

            if (paramValue is string jsonString)
            {
                list = JsonSerializer.Deserialize<List<JsonElement>>(jsonString)
                    .Select(element => ConvertJsonElement(element, paramDef.DataType))
                    .ToList();
            }
            else if (paramValue is JsonElement jsonElement)
            {
                list = JsonSerializer.Deserialize<List<JsonElement>>(jsonElement.GetRawText())
                    .Select(element => ConvertJsonElement(element, paramDef.DataType))
                    .ToList();
            }
            else if (paramValue is List<object> objList)
            {
                list = objList.Select(item =>
                    item is JsonElement e ? ConvertJsonElement(e, paramDef.DataType) : item)
                    .ToList();
            }
            else
            {
                throw new ArgumentException("Некорректный формат списка");
            }

            return list;
        }

        private object ProcessScalarParameter(object paramValue, DataElement paramDef)
        {
            if (paramValue is JsonElement jsonElement)
            {
                return paramDef.DataType switch
                {
                    DataType.INT => jsonElement.GetInt32(),
                    DataType.FLOAT => jsonElement.GetDouble(),
                    DataType.BOOL => jsonElement.GetBoolean(),
                    _ => jsonElement.ToString()
                };
            }

            if (paramValue is string strValue && strValue.StartsWith('[') && strValue.EndsWith(']'))
            {
                throw new ArgumentException("Ожидается скалярное значение, получен массив");
            }

            return paramDef.DataType switch
            {
                DataType.INT => Convert.ToInt32(paramValue),
                DataType.FLOAT => Convert.ToDouble(paramValue),
                DataType.BOOL => Convert.ToBoolean(paramValue),
                _ => paramValue.ToString()
            };
        }

        private object ConvertJsonElement(JsonElement element, DataType targetType)
        {
            try
            {
                if (element.ValueKind == JsonValueKind.Null)
                    return null;

                return targetType switch
                {
                    DataType.INT => element.ValueKind == JsonValueKind.Number ?
                                   element.GetInt32() :
                                   int.Parse(element.GetString()),
                    DataType.FLOAT => element.ValueKind == JsonValueKind.Number ?
                                    element.GetDouble() :
                                    double.Parse(element.GetString()),
                    DataType.BOOL => element.ValueKind == JsonValueKind.True ? true :
                                    element.ValueKind == JsonValueKind.False ? false :
                                    bool.Parse(element.GetString()),
                    _ => element.ValueKind == JsonValueKind.String ?
                         element.GetString() :
                         element.GetRawText()
                };
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Ошибка преобразования JSON элемента: {ex.Message}");
            }
        }

        // проверка выходных данных, реализовать позже
        //private void ValidateOutputs(AlgorithmDefinition algorithm, Dictionary<string, object> result) { }
    }
}