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
                    jobs.Add(new Job(i + 1) { Name = $"Job {i + 1}" });
                }

                // 3. Создаем граф зависимостей
                var graph = new JobGraph(jobs);
                for (int i = 0; i < numJobs; i++)
                {
                    for (int j = 0; j < numJobs; j++)
                    {
                        if (Convert.ToInt32(dependenciesRaw[i][j]) == 1)
                        {
                            graph.AddDependency(jobs[i], jobs[j]);
                        }
                    }
                }

                // 4. Удаляем транзитивные ребра
                graph.RemoveTransitiveEdges();

                // 5. Создаем работников
                var workers = new List<Worker>
                {
                    new Worker(1) { Name = "Worker 1", Productivity = 1.0 },
                    new Worker(2) { Name = "Worker 2", Productivity = 1.0 }
                };

                // 6. Создаем и решаем проблему расписания
                var problem = new SchedulingProblem(jobs, workers, graph);
                var schedule = CreateSchedule(problem);

                // 7. Форматируем результаты
                return new Dictionary<string, object>
                {
                    ["schedule"] = GetJobOrder(schedule),
                    ["schedule_details"] = FormatScheduleDetails(schedule),
                    ["gantt_data"] = GanttChartGenerator.GenerateChartData(schedule)
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object>
                {
                    ["error"] = true,
                    ["message"] = $"Ошибка выполнения алгоритма: {ex.Message}",
                    ["stack_trace"] = ex.StackTrace
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

            // 3. Создаем расписание
            var schedule = new Schedule();
            var availableWorkers = new Queue<Worker>(workers);
            var workerAvailability = workers.ToDictionary(w => w, w => 0.0);

            foreach (var job in sortedJobs)
            {
                // Для каждой работы создаем один этап (по условию длительность = 1)
                var stage = new Stage(1)
                {
                    Name = $"Stage of Job {job.Id}",
                    Duration = 1,
                    StageNumber = 1
                };

                // Находим самого раннего доступного работника
                var worker = workerAvailability
                    .OrderBy(kv => kv.Value)
                    .First().Key;

                var startTime = workerAvailability[worker];
                var endTime = startTime + stage.Duration;

                schedule.AddItem(new ScheduleItem(job, stage, worker)
                {
                    StartTime = startTime,
                    EndTime = endTime
                });

                workerAvailability[worker] = endTime;
            }

            schedule.CalculateTotalDuration();
            return schedule;
        }

        private static void AssignLexicographicPriorities(JobGraph graph, List<Job> jobs)
        {
            var remainingJobs = new HashSet<Job>(jobs);
            var assignedPriorities = new Dictionary<Job, int>();
            int currentPriority = jobs.Count;

            // 1. Находим стоки (работы без зависимостей)
            var sinks = graph.GetSources();

            // 2. Назначаем первые приоритеты стокам
            foreach (var sink in sinks.OrderBy(j => j.Id))
            {
                assignedPriorities[sink] = currentPriority--;
                remainingJobs.Remove(sink);
            }

            // 3. Пока есть работы без приоритетов
            while (remainingJobs.Count > 0)
            {
                // Находим работы, все зависимости которых имеют приоритеты
                var readyJobs = remainingJobs
                    .Where(j => graph.GetDependencies(j)
                        .All(d => assignedPriorities.ContainsKey(d)))
                    .ToList();

                // Для каждой готовой работы создаем строку приоритетов зависимостей
                var jobsWithPriorities = readyJobs.Select(j => new
                {
                    Job = j,
                    PriorityString = string.Join(",",
                        graph.GetDependencies(j)
                            .Select(d => assignedPriorities[d])
                            .OrderByDescending(p => p))
                }).ToList();

                // Находим работу с лексикографически наименьшей строкой
                var nextJob = jobsWithPriorities
                    .OrderBy(j => j.PriorityString)
                    .First()
                    .Job;

                // Назначаем приоритет
                assignedPriorities[nextJob] = currentPriority--;
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

        private static string FormatScheduleDetails(Schedule schedule)
        {
            return string.Join("\n", schedule.Items
                .OrderBy(item => item.StartTime)
                .Select(item =>
                    $"{item.Worker.Name}: {item.Job.Name} - " +
                    $"Time: {item.StartTime:0.##}-{item.EndTime:0.##}"));
        }
    }
}
