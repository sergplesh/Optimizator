using System;
using System.Collections.Generic;
using System.Linq;
using Optimizer.Library;

namespace Optimizator.Algorithms.Johnson
{
    public static class Johnson
    {
        public static Dictionary<string, object> Main(Dictionary<string, object> parameters)
        {
            try
            {
                // 1. Получаем и проверяем входные параметры
                if (!parameters.TryGetValue("num_jobs", out var numJobsObj) ||
                    !int.TryParse(numJobsObj.ToString(), out int numJobs) ||
                    numJobs <= 0)
                {
                    throw new ArgumentException("Некорректное количество работ");
                }

                if (!parameters.TryGetValue("job_times", out var jobTimesObj) ||
                    !(jobTimesObj is List<List<object>> jobTimesRaw))
                {
                    throw new ArgumentException("Некорректный формат данных времен выполнения");
                }

                // 2. Создаем список работ (Job) с этапами (Stage)
                var jobs = new List<Job>();
                for (int i = 0; i < numJobs; i++)
                {
                    if (jobTimesRaw[i].Count != 2)
                    {
                        throw new ArgumentException($"Работа {i + 1} должна содержать ровно 2 этапа");
                    }

                    var job = new Job(i + 1) { Name = $"Job {i + 1}" };

                    // Добавляем этапы
                    job.AddStage(new Stage(1)
                    {
                        Name = $"Stage 1 of Job {i + 1}",
                        Duration = Convert.ToDouble(jobTimesRaw[i][0]),
                        StageNumber = 1
                    });

                    job.AddStage(new Stage(2)
                    {
                        Name = $"Stage 2 of Job {i + 1}",
                        Duration = Convert.ToDouble(jobTimesRaw[i][1]),
                        StageNumber = 2
                    });

                    jobs.Add(job);
                }

                // 3. Создаем работников (Worker) для каждого типа этапа
                var workers = new List<Worker>
                {
                    new Worker(1)
                    {
                        Name = "Worker 1 (Stage 1)",
                        Type = WorkerType.StageSpecific,
                        SupportedStageType = 1
                    },
                    new Worker(2)
                    {
                        Name = "Worker 2 (Stage 2)",
                        Type = WorkerType.StageSpecific,
                        SupportedStageType = 2
                    }
                };

                // 4. Создаем и решаем проблему расписания
                var problem = new SchedulingProblem(jobs, workers);
                var schedule = CreateJohnsonSchedule(problem);

                // 5. Форматируем результаты
                var orderedJobs = schedule.Items
                    .Where(item => item.Stage.StageNumber == 1)
                    .OrderBy(item => item.StartTime)
                    .Select(item => item.Job.Id)
                    .ToList();

                return new Dictionary<string, object>
                {
                    ["optimal_time"] = schedule.TotalDuration,
                    ["schedule"] = orderedJobs,
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

        private static Schedule CreateJohnsonSchedule(SchedulingProblem problem)
        {
            ValidateInput(problem);

            // Разделяем работы на две группы
            var (group1, group2) = SplitAndSortJobs(problem.Jobs);

            // Объединяем группы в оптимальном порядке
            var orderedJobs = group1.Concat(group2).ToList();

            // Строим расписание
            return BuildSchedule(problem.Workers, orderedJobs);
        }

        private static void ValidateInput(SchedulingProblem problem)
        {
            if (problem.Workers.Count != 2)
                throw new ArgumentException("Johnson algorithm requires exactly 2 workers");

            if (problem.Workers.Any(w => w.Type != WorkerType.StageSpecific || !w.SupportedStageType.HasValue))
                throw new ArgumentException("Both workers must be stage-specific");

            if (problem.Jobs.Any(j => j.Stages.Count != 2))
                throw new ArgumentException("Each job must contain exactly 2 stages");

            var stageTypes = problem.Workers.Select(w => w.SupportedStageType.Value).Distinct().ToList();
            if (stageTypes.Count != 2 || stageTypes.Min() != 1 || stageTypes.Max() != 2)
                throw new ArgumentException("Workers must specialize in stage types 1 and 2");
        }

        private static (List<Job> group1, List<Job> group2) SplitAndSortJobs(List<Job> jobs)
        {
            // Группа 1: где длительность первого этапа <= второго
            var group1 = jobs
                .Where(j => j.Stages[0].Duration <= j.Stages[1].Duration)
                .OrderBy(j => j.Stages[0].Duration)
                .ToList();

            // Группа 2: остальные работы, сортируем по убыванию длительности второго этапа
            var group2 = jobs
                .Where(j => j.Stages[0].Duration > j.Stages[1].Duration)
                .OrderByDescending(j => j.Stages[1].Duration)
                .ToList();

            return (group1, group2);
        }

        private static Schedule BuildSchedule(List<Worker> workers, List<Job> orderedJobs)
        {
            var schedule = new Schedule();
            var stage1Worker = workers.First(w => w.SupportedStageType == 1);
            var stage2Worker = workers.First(w => w.SupportedStageType == 2);

            double timeStage1 = 0;
            double timeStage2 = 0;

            foreach (var job in orderedJobs)
            {
                var stage1 = job.Stages[0];
                var stage2 = job.Stages[1];

                // Планируем первый этап
                var start1 = timeStage1;
                var end1 = start1 + stage1.Duration;
                schedule.AddItem(new ScheduleItem(job, stage1, stage1Worker)
                {
                    StartTime = start1,
                    EndTime = end1
                });
                timeStage1 = end1;

                // Планируем второй этап
                var start2 = Math.Max(end1, timeStage2);
                var end2 = start2 + stage2.Duration;
                schedule.AddItem(new ScheduleItem(job, stage2, stage2Worker)
                {
                    StartTime = start2,
                    EndTime = end2
                });
                timeStage2 = end2;
            }

            schedule.CalculateTotalDuration();
            return schedule;
        }

        private static string FormatScheduleDetails(Schedule schedule)
        {
            return string.Join("\n", schedule.Items
                .OrderBy(item => item.StartTime)
                .Select(item =>
                    $"{item.Worker.Name}: {item.Job.Name} ({item.Stage.Name}) - " +
                    $"Time: {item.StartTime:0.##}-{item.EndTime:0.##}"));
        }
    }
}