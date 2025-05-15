using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Optimizator.Models
{
    public class AlgorithmDefinition
    {
        public string Name { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public List<DataElement> Parameters { get; set; } = new List<DataElement>();
        public List<DataElement> Outputs { get; set; } = new List<DataElement>();

        [JsonPropertyName("output_visualization")]
        public OutputVisualization OutputVisualization { get; set; }

        public AlgorithmDefinition() { }

        public AlgorithmDefinition(string name, string title, string description)
        {
            Name = name;
            Title = title;
            Description = description;
        }
    }
}