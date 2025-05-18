using System;
using System.Collections.Generic;
using System.Linq;
using Optimizer.Library;

namespace Optimizator.Algorithms.Lexicographic
{
    public static class Lexicographic
    {
        public static Dictionary<string, object> Main(Dictionary<string, object> parameters)
        {
            try
            {
                // 1. Получаем входные параметры
                int numJobs = Convert.ToInt32(parameters["num_jobs"]);
                var dependenciesRaw = (List<List<object>>)parameters["dependencies"];

                // 2. Создаем граф работ
                var jobs = new List<Job>();
                for (int i = 0; i < numJobs; i++)
                {
                    jobs.Add(new Job(i + 1) { Name = $"Работа {i + 1}" });
                }

                // 3. Создаем граф зависимостей
                var graph = new JobGraph(jobs);
                for (int i = 0; i < numJobs; i++)
                {
                    for (int j = 0; j < numJobs; j++)
                    {
                        if (Convert.ToInt32(dependenciesRaw[i][j]) == 1)
                        {
                            graph.AddDependency(jobs[j], jobs[i]);
                        }
                    }
                }

                // 4. Удаляем транзитивные ребра
                graph.RemoveTransitiveEdges();

                // 5. Создаем работников
                var workers = new List<Worker>
                {
                    new Worker(1) { Name = "Работник 1", Productivity = 1.0 },
                    new Worker(2) { Name = "Работник 2", Productivity = 1.0 }
                };

                // 6. Создаем и решаем проблему расписания
                var problem = new SchedulingProblem(jobs, workers, graph);
                var schedule = CreateSchedule(problem);

                // 7. Форматируем результаты
                return new Dictionary<string, object>
                {
                    ["schedule"] = GetJobOrder(schedule),
                    //["schedule_details"] = FormatScheduleDetails(schedule),
                    ["gantt_data"] = GanttChartGenerator.GenerateChartData(schedule)
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object>
                {
                    ["error"] = true,
                    ["message"] = $"Ошибка выполнения алгоритма: {ex.Message}"
                };
            }
        }

        private static Schedule CreateSchedule(SchedulingProblem problem)
        {
            var graph = problem.DependencyGraph ?? new JobGraph(problem.Jobs);
            var jobs = problem.Jobs;
            var workers = problem.Workers;

            // 1. Назначаем приоритеты по лексикографической стратегии
            AssignLexicographicPriorities(graph, jobs);

            // 2. Сортируем работы по приоритету (от высокого к низкому)
            var sortedJobs = jobs.OrderByDescending(j => j.Priority).ToList();

            //// 3. Создаем расписание
            var schedule = new Schedule();
            var workerAvailability = workers.ToDictionary(w => w, w => 0.0);
            var completedJobs = new HashSet<Job>();

            while (completedJobs.Count < jobs.Count)
            {
                // Находим задачи, готовые к выполнению (все зависимости выполнены)
                var readyJobs = sortedJobs
                    .Where(j => !completedJobs.Contains(j))
                    .Where(j => graph.GetDependencies(j).All(d => completedJobs.Contains(d)))
                    .ToList();

                if (!readyJobs.Any())
                    throw new InvalidOperationException("Обнаружен deadlock - нет задач для выполнения");

                var lastTime = workerAvailability
                        .OrderBy(kv => kv.Value)
                        .Last().Value;
                foreach (var w in workerAvailability.Keys)
                {
                    workerAvailability[w] = lastTime;
                }

                int count = 0;
                if (readyJobs.Count < workerAvailability.Count)
                {
                    count = readyJobs.Count;
                }
                else count = workerAvailability.Count;

                // Распределяем готовые задачи по свободным работникам
                for (int i = 0; i < count; i++)
                {
                    var job = readyJobs[i];
                    // Находим самого раннего доступного работника
                    var worker = workerAvailability
                        .OrderBy(kv => kv.Value)
                        .First().Key;

                    var stage = new Stage(1)
                    {
                        Name = $"Этап работы {job.Id}",
                        Duration = 1,
                        //StageNumber = 1
                    };

                    var startTime = workerAvailability[worker];
                    var endTime = startTime + stage.Duration;

                    schedule.AddItem(new ScheduleItem(job, stage, worker)
                    {
                        StartTime = startTime,
                        EndTime = endTime
                    });

                    workerAvailability[worker] = endTime;
                    completedJobs.Add(job);
                }
            }

            schedule.CalculateTotalDuration();
            return schedule;
        }

        private static void AssignLexicographicPriorities(JobGraph graph, List<Job> jobs)
        {
            var remainingJobs = new HashSet<Job>(jobs);
            var assignedPriorities = new Dictionary<Job, int>();
            int currentPriority = 1;

            // 1. Находим стоки (работы без зависимостей)
            var sinks = graph.GetRoots();

            // 2. Назначаем первые приоритеты стокам
            foreach (var sink in sinks.OrderBy(j => j.Id))
            {
                assignedPriorities[sink] = currentPriority;
                currentPriority++;
                remainingJobs.Remove(sink);
            }

            // 3. Пока есть работы без приоритетов
            while (remainingJobs.Count > 0)
            {
                var jobsToAssign = graph.GetToAssign(remainingJobs, assignedPriorities);

                // Для каждой готовой работы создаем строку приоритетов зависимостей
                var jobsWithPriorities = jobsToAssign.Select(jFrom => new
                {
                    Job = jFrom,
                    PriorityString = string.Join(",",
                        assignedPriorities.Keys.Where(jTo => graph.GetDependencies(jTo).Contains(jFrom))
                            .Select(d => assignedPriorities[d])
                            .OrderByDescending(p => p))
                }).ToList();

                // Находим работу с лексикографически наименьшей строкой
                var nextJob = jobsWithPriorities
                    .OrderBy(j => j.PriorityString, new NumericStringComparer())
                    .First()
                    .Job;

                //Назначаем приоритет
                assignedPriorities[nextJob] = currentPriority;
                currentPriority++;
                remainingJobs.Remove(nextJob);
            }

            // Устанавливаем приоритеты в объекты Job
            foreach (var kvp in assignedPriorities)
            {
                kvp.Key.Priority = kvp.Value;
            }
        }

        private static List<int> GetJobOrder(Schedule schedule)
        {
            return schedule.Items
                .OrderBy(item => item.StartTime)
                .Select(item => item.Job.Id)
                .ToList();
        }

        //private static string FormatScheduleDetails(Schedule schedule)
        //{
        //    return string.Join("\n", schedule.Items
        //        .OrderBy(item => item.StartTime)
        //        .Select(item =>
        //            $"{item.Worker.Name}: {item.Job.Name} - " +
        //            $"Time: {item.StartTime:0.##}-{item.EndTime:0.##}"));
        //}
    }

    public class NumericStringComparer : IComparer<string>
    {
        public int Compare(string x, string y)
        {
            if (x == y) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            string[] partsX = x.Split(',');
            string[] partsY = y.Split(',');

            int maxLength = Math.Max(partsX.Length, partsY.Length);

            for (int i = 0; i < maxLength; i++)
            {
                if (i >= partsX.Length) return -1; // x короче => x должен быть раньше (если остальные части равны)
                if (i >= partsY.Length) return 1;  // y короче => y должен быть раньше

                int partX = int.Parse(partsX[i]);
                int partY = int.Parse(partsY[i]);

                if (partX != partY)
                    return partX.CompareTo(partY);
            }

            return 0; // если все части равны (но строки разные, хотя такого быть не должно)
        }
    }
}
