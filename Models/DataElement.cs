using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Optimizator.Models
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum DataType
    {
        INT,
        FLOAT,
        STRING,
        BOOL
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum DataShape
    {
        SCALAR,
        DYNAMIC_MATRIX,
        LIST
    }

    public class MatrixDimensions
    {
        public string Rows { get; set; }
        public object Cols { get; set; }

        public int GetColsValue(Dictionary<string, object> parameters)
        {
            if (Cols is int i) return i;
            if (Cols is string s && parameters.TryGetValue(s, out var value))
            {
                return Convert.ToInt32(value);
            }
            throw new ArgumentException($"Не удалось определить количество столбцов {Cols}");
        }
    }

    public class DataElement
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("data_type")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public DataType DataType { get; set; }

        [JsonPropertyName("data_shape")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public DataShape DataShape { get; set; }

        [JsonPropertyName("default_value")]
        public object DefaultValue { get; set; }

        [JsonPropertyName("matrix_controller")]
        public bool? MatrixController { get; set; }

        [JsonPropertyName("dimensions")]
        public MatrixDimensions Dimensions { get; set; }

        [JsonPropertyName("column_labels")]
        public List<string> ColumnLabels { get; set; }

        public DataElement() { }

        public DataElement(string name, string title, string description,
                          DataType dataType, DataShape dataShape,
                          object defaultValue)
        {
            Name = name;
            Title = title;
            Description = description;
            DataType = dataType;
            DataShape = dataShape;
            DefaultValue = defaultValue;
        }
    }
}