using System;
using System.Collections.Generic;
using System.Linq;
using Optimizer.Library;

namespace Optimizator.Algorithms.LevelStrategy
{
    public static class LevelStrategy
    {
        public static Dictionary<string, object> Main(Dictionary<string, object> parameters)
        {
            try
            {
                // 1. Получаем входные параметры
                int numJobs = Convert.ToInt32(parameters["num_jobs"]);
                int numWorkers = Convert.ToInt32(parameters["num_workers"]);
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
                            graph.AddDependency(jobs[j], jobs[i]);
                        }
                    }
                }

                // 4. Проверяем что граф - дерево к корню
                if (!graph.IsTreeToRoot())
                {
                    throw new ArgumentException("Граф зависимостей не является деревом к корню");
                }

                // 5. Создаем работников
                var workers = new List<Worker>();
                for (int i = 0; i < numWorkers; i++)
                {
                    workers.Add(new Worker(i + 1) { Name = $"Worker {i + 1}", Productivity = 1.0 });
                }

                // 6. Создаем и решаем проблему расписания
                var problem = new SchedulingProblem(jobs, workers, graph);
                var schedule = CreateSchedule(problem);

                // 7. Форматируем результаты
                return new Dictionary<string, object>
                {
                    ["schedule"] = "",
                    ["levels"] = "",
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
            var graph = problem.DependencyGraph;
            var jobs = problem.Jobs;
            var workers = problem.Workers;

            // 1. Назначаем приоритеты по уровневой стратегии
            AssignLevelPriorities(graph, jobs);

            // 2. Сортируем работы по приоритету (от высокого к низкому)
            var sortedJobs = jobs.OrderByDescending(j => j.Priority).ToList();

            // 3. Создаем расписание
            var schedule = new Schedule();
            var workerAvailability = workers.ToDictionary(w => w, w => 0.0);

            foreach (var job in sortedJobs)
            {
                // Для каждой работы создаем один этап (длительность = 1)
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

        private static void AssignLevelPriorities(JobGraph graph, List<Job> jobs)
        {
            var levels = new Dictionary<Job, int>();
            var remainingJobs = new HashSet<Job>(jobs);
            int currentLevel = 1;

            // 1. Находим корни (стоки) дерева - работы без зависимостей
            var roots = graph.GetSources();

            // 2. Назначаем уровень 1 корневым работам
            foreach (var root in roots)
            {
                levels[root] = currentLevel;
                remainingJobs.Remove(root);
            }

            // 3. Распределяем работы по уровням
            while (remainingJobs.Count > 0)
            {
                currentLevel++;
                var jobsToAssign = new List<Job>();

                // Находим работы, все зависимости которых уже имеют уровень
                foreach (var job in remainingJobs)
                {
                    var dependencies = graph.GetDependencies(job);
                    if (dependencies.All(d => levels.ContainsKey(d)))
                    {
                        jobsToAssign.Add(job);
                    }
                }

                // Назначаем уровень этим работам
                foreach (var job in jobsToAssign)
                {
                    // Уровень = максимальный уровень зависимостей + 1
                    var maxDependencyLevel = graph.GetDependencies(job)
                        .Max(d => levels[d]);
                    levels[job] = maxDependencyLevel + 1;
                    remainingJobs.Remove(job);
                }
            }

            // Преобразуем уровни в приоритеты (чем выше уровень - тем выше приоритет)
            var maxLevel = levels.Values.Max();
            foreach (var job in jobs)
            {
                job.Priority = maxLevel - levels[job] + 1;
            }
        }

        private static List<int> GetJobOrder(Schedule schedule)
        {
            return schedule.Items
                .OrderBy(item => item.StartTime)
                .Select(item => item.Job.Id)
                .ToList();
        }

        private static Dictionary<int, int> GetJobLevels(List<Job> jobs)
        {
            return jobs.ToDictionary(j => j.Id, j => (int)j.Priority);
        }

        private static string FormatScheduleDetails(Schedule schedule)
        {
            return string.Join("\n", schedule.Items
                .OrderBy(item => item.StartTime)
                .Select(item =>
                    $"{item.Worker.Name}: {item.Job.Name} (Priority {item.Job.Priority}) - " +
                    $"Time: {item.StartTime:0.##}-{item.EndTime:0.##}"));
        }
    }
}