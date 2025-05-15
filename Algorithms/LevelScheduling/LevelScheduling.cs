using System;
using System.Collections.Generic;

namespace Optimizator.Algorithms.LevelScheduling
{
    public static class LevelScheduling
    {
        public static Dictionary<string, object> Main(Dictionary<string, object> parameters)
        {
            try
            {
                // 1. Получение параметров
                int numJobs = Convert.ToInt32(parameters["num_jobs"]);
                int numWorkers = Convert.ToInt32(parameters["num_workers"]);
                var dependenciesRaw = (List<List<object>>)parameters["dependencies"];

                // 2. Преобразование матрицы зависимостей
                var dependencies = new int[numJobs, numJobs];
                for (int i = 0; i < numJobs; i++)
                {
                    for (int j = 0; j < numJobs; j++)
                    {
                        dependencies[i, j] = Convert.ToInt32(dependenciesRaw[i][j]);
                    }
                }

                // 3. Расчет уровней для работ
                var levels = CalculateLevels(dependencies, numJobs);

                // 4. Сортировка работ по уровням
                var jobsByLevel = new List<JobLevel>();
                for (int job = 0; job < numJobs; job++)
                {
                    jobsByLevel.Add(new JobLevel { Job = job, Level = levels[job] });
                }
                SortJobsByLevel(jobsByLevel);

                // 5. Построение расписания
                var schedule = BuildSchedule(jobsByLevel, dependencies, numWorkers, numJobs);

                // 6. Форматирование результатов
                return FormatResults(levels, schedule, jobsByLevel, numJobs);
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка выполнения алгоритма: {ex.Message}");
            }
        }

        private static int[] CalculateLevels(int[,] dependencies, int numJobs)
        {
            var levels = new int[numJobs];
            var visited = new bool[numJobs];

            for (int job = 0; job < numJobs; job++)
            {
                if (!visited[job])
                {
                    CalculateLevel(job, dependencies, levels, visited, numJobs);
                }
            }

            return levels;
        }

        private static void CalculateLevel(int job, int[,] dependencies, int[] levels, bool[] visited, int numJobs)
        {
            visited[job] = true;
            int maxChildLevel = -1;

            for (int child = 0; child < numJobs; child++)
            {
                if (dependencies[child, job] == 1)
                {
                    if (!visited[child])
                    {
                        CalculateLevel(child, dependencies, levels, visited, numJobs);
                    }
                    if (levels[child] > maxChildLevel)
                    {
                        maxChildLevel = levels[child];
                    }
                }
            }

            levels[job] = maxChildLevel + 1;
        }

        private static void SortJobsByLevel(List<JobLevel> jobsByLevel)
        {
            for (int i = 0; i < jobsByLevel.Count - 1; i++)
            {
                for (int j = 0; j < jobsByLevel.Count - i - 1; j++)
                {
                    if (jobsByLevel[j].Level < jobsByLevel[j + 1].Level)
                    {
                        var temp = jobsByLevel[j];
                        jobsByLevel[j] = jobsByLevel[j + 1];
                        jobsByLevel[j + 1] = temp;
                    }
                }
            }
        }

        private static List<List<List<int>>> BuildSchedule(
            List<JobLevel> jobsByLevel,
            int[,] dependencies,
            int numWorkers,
            int numJobs)
        {
            var schedule = new List<List<List<int>>>();
            var completedJobs = new HashSet<int>();

            while (completedJobs.Count < numJobs)
            {
                var timeSlot = new List<List<int>>();
                for (int w = 0; w < numWorkers; w++)
                {
                    timeSlot.Add(new List<int>());
                }

                var executableJobs = new List<int>();
                foreach (var jobLevel in jobsByLevel)
                {
                    if (!completedJobs.Contains(jobLevel.Job) &&
                        CanExecute(jobLevel.Job, dependencies, completedJobs, numJobs))
                    {
                        executableJobs.Add(jobLevel.Job);
                        if (executableJobs.Count >= numWorkers)
                            break;
                    }
                }

                for (int w = 0; w < Math.Min(numWorkers, executableJobs.Count); w++)
                {
                    var job = executableJobs[w];
                    timeSlot[w].Add(job);
                    completedJobs.Add(job);
                }

                schedule.Add(timeSlot);
            }

            return schedule;
        }

        private static bool CanExecute(int job, int[,] dependencies, HashSet<int> completedJobs, int numJobs)
        {
            for (int i = 0; i < numJobs; i++)
            {
                if (dependencies[job, i] == 1 && !completedJobs.Contains(i))
                {
                    return false;
                }
            }
            return true;
        }

        private static Dictionary<string, object> FormatResults(
            int[] levels,
            List<List<List<int>>> schedule,
            List<JobLevel> jobsByLevel,
            int numJobs)
        {
            // Форматирование уровней
            var levelsStr = "";
            for (int i = 0; i < numJobs; i++)
            {
                levelsStr += $"Работа {i + 1}: уровень {levels[i] + 1}\n";
            }

            // Форматирование расписания
            var scheduleStr = "";
            for (int t = 0; t < schedule.Count; t++)
            {
                scheduleStr += $"Временной слот {t + 1}:\n";
                for (int w = 0; w < schedule[t].Count; w++)
                {
                    var jobs = schedule[t][w];
                    if (jobs.Count > 0)
                    {
                        scheduleStr += $"  Работник {w + 1}: ";
                        for (int j = 0; j < jobs.Count; j++)
                        {
                            scheduleStr += (jobs[j] + 1);
                            if (j < jobs.Count - 1) scheduleStr += ", ";
                        }
                        scheduleStr += "\n";
                    }
                }
            }

            return new Dictionary<string, object>
            {
                ["schedule"] = scheduleStr,
                ["levels"] = levelsStr,
                ["makespan"] = schedule.Count
            };
        }

        private class JobLevel
        {
            public int Job { get; set; }
            public int Level { get; set; }
        }
    }
}