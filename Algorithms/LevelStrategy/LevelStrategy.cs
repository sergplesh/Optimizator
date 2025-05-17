using System;
using System.Collections;
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

                // 4. Проверяем что граф - дерево к корню
                if (!graph.IsTreeToRoot())
                {
                    throw new ArgumentException("Граф зависимостей не является деревом к корню");
                }

                // 5. Создаем работников
                var workers = new List<Worker>();
                for (int i = 0; i < numWorkers; i++)
                {
                    workers.Add(new Worker(i + 1) { Name = $"Работник {i + 1}", Productivity = 1.0 });
                }

                // 6. Создаем и решаем проблему расписания
                var problem = new SchedulingProblem(jobs, workers, graph);
                var schedule = CreateSchedule(problem);

                // 7. Форматируем результаты
                return new Dictionary<string, object>
                {
                    ["schedule"] = GetJobOrder(schedule),
                    //["levels"] = GetJobLevels(problem.Jobs),
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
            var graph = problem.DependencyGraph;
            var jobs = problem.Jobs;
            var workers = problem.Workers;

            // 1. Назначаем приоритеты по уровневой стратегии
            AssignLevelPriorities(graph, jobs);

            // 2. Сортируем работы по приоритету
            var sortedJobs = jobs.OrderByDescending(j => j.Priority).ToList();

            // 3. Создаем расписание
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

                var lastTime= workerAvailability
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
                        StageNumber = 1
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

        private static void AssignLevelPriorities(JobGraph graph, List<Job> jobs)
        {
            var levels = new Dictionary<Job, int>();
            var remainingJobs = new HashSet<Job>(jobs);
            int currentLevel = 1;

            // 1. Находим корни (стоки) дерева - работы без зависимостей
            var roots = graph.GetRoots();

            // 2. Назначаем уровень корневым работам
            foreach (var root in roots)
            {
                levels[root] = 1;
                root.Priority = currentLevel;
                currentLevel++;
                remainingJobs.Remove(root);
            }

            // 3. Распределяем работы по уровням
            while (remainingJobs.Count > 0)
            {
                var jobsToAssign = graph.GetToAssign(remainingJobs, levels);

                // Назначаем уровень этим работам
                foreach (var job in jobsToAssign)
                {
                    // Уровень = максимальный уровень зависимостей + 1
                    var maxDependencyLevel = jobs.Where(j => graph.GetDependencies(j).Contains(job))
                        .ToList().Max(d => levels[d]);
                    levels[job] = maxDependencyLevel + 1;
                    job.Priority = currentLevel;
                    currentLevel++;
                    remainingJobs.Remove(job);
                }
            }
        }

        private static List<int> GetJobOrder(Schedule schedule)
        {
            return schedule.Items
                .OrderBy(item => item.StartTime)
                .Select(item => item.Job.Id)
                .ToList();
        }
    }
}
