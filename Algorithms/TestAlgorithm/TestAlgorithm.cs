using System;
using System.Collections.Generic;

namespace Optimizator.Algorithms.TestAlgorithm
{
    public static class TestAlgorithm
    {
        public static Dictionary<string, object> Main(Dictionary<string, object> parameters)
        {
            int input = Convert.ToInt32(parameters["input_value"]);
            return new Dictionary<string, object>
            {
                ["result"] = input * 2
            };
        }
    }
}