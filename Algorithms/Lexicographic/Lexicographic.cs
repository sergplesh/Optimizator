using System;
using System.Collections.Generic;

namespace Optimizator.Algorithms.Lexicographic
{
    public static class Lexicographic
    {
        public static Dictionary<string, object> Main(Dictionary<string, object> parameters)
        {
            try
            {
                // Получаем параметры
                int numJobs = Convert.ToInt32(parameters["num_jobs"]);
                var dependenciesRaw = (List<List<object>>)parameters["dependencies"];

                // Преобразуем матрицу зависимостей
                var dependencies = new int[numJobs][];
                for (int i = 0; i < numJobs; i++)
                {
                    dependencies[i] = new int[numJobs];
                    for (int j = 0; j < numJobs; j++)
                    {
                        dependencies[i][j] = Convert.ToInt32(dependenciesRaw[i][j]);
                    }
                }

                // Удаление транзитивных зависимостей
                var graph = RemoveTransitiveDependencies(dependencies, numJobs);

                // Построение расписания
                var schedule = BuildSchedule(graph, numJobs);

                // Построение диаграммы Ганта
                var ganttChart = BuildGanttChart(schedule);

                return new Dictionary<string, object>
                {
                    ["schedule"] = FormatSchedule(schedule),
                    ["gantt_chart"] = ganttChart
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка выполнения алгоритма: {ex.Message}");
            }
        }

        private static int[][] RemoveTransitiveDependencies(int[][] graph, int n)
        {
            var result = new int[n][];
            for (int i = 0; i < n; i++)
            {
                result[i] = new int[n];
                for (int j = 0; j < n; j++)
                {
                    result[i][j] = graph[i][j];
                }
            }

            for (int k = 0; k < n; k++)
            {
                for (int i = 0; i < n; i++)
                {
                    for (int j = 0; j < n; j++)
                    {
                        if (result[i][k] == 1 && result[k][j] == 1)
                        {
                            result[i][j] = 0;
                        }
                    }
                }
            }

            return result;
        }

        private static List<int> BuildSchedule(int[][] graph, int n)
        {
            var priorities = new int[n];
            var remaining = new List<int>();
            for (int i = 0; i < n; i++)
            {
                remaining.Add(i);
            }

            int currentPriority = n;

            while (remaining.Count > 0)
            {
                // Находим работы без зависимостей
                var independentJobs = new List<int>();
                foreach (var job in remaining)
                {
                    bool hasDependencies = false;
                    foreach (var i in remaining)
                    {
                        if (graph[i][job] == 1)
                        {
                            hasDependencies = true;
                            break;
                        }
                    }
                    if (!hasDependencies)
                    {
                        independentJobs.Add(job);
                    }
                }

                // Назначаем приоритеты
                foreach (var job in independentJobs)
                {
                    priorities[job] = currentPriority--;
                    remaining.Remove(job);
                }
            }

            // Сортируем работы по приоритету
            var schedule = new List<int>(n);
            for (int p = n; p > 0; p--)
            {
                for (int i = 0; i < n; i++)
                {
                    if (priorities[i] == p)
                    {
                        schedule.Add(i);
                        break;
                    }
                }
            }

            return schedule;
        }

        private static string BuildGanttChart(List<int> schedule)
        {
            var worker1Tasks = new List<string>();
            var worker2Tasks = new List<string>();
            int time1 = 0;
            int time2 = 0;

            foreach (var job in schedule)
            {
                if (time1 <= time2)
                {
                    worker1Tasks.Add($"Работа {job + 1} ({time1}-{time1 + 1})");
                    time1++;
                }
                else
                {
                    worker2Tasks.Add($"Работа {job + 1} ({time2}-{time2 + 1})");
                    time2++;
                }
            }

            return $"Работник 1: {string.Join(" → ", worker1Tasks)}\n" +
                   $"Работник 2: {string.Join(" → ", worker2Tasks)}";
        }

        private static string FormatSchedule(List<int> schedule)
        {
            var parts = new List<string>();
            foreach (var job in schedule)
            {
                parts.Add($"Работа {job + 1}");
            }
            return string.Join(" → ", parts);
        }
    }
}