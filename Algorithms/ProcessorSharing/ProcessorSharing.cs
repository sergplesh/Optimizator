using System;
using System.Collections.Generic;
using System.Linq;
using Optimizer.Library;

namespace Optimizator.Algorithms.ProcessorSharing
{
    public static class ProcessorSharing
    {
        public static Dictionary<string, object> Main(Dictionary<string, object> parameters)
        {
            try
            {
                // Валидация входных параметров
                ValidateParameters(parameters);

                // Получаем и подготавливаем данные
                var (jobs, workers) = PrepareData(parameters);

                // Создаем и решаем проблему расписания
                var schedule = CreateSchedule(jobs, workers);

                // Форматируем результаты
                return FormatResults(schedule);
            }
            catch (Exception ex)
            {
                // Логируем ошибку для диагностики
                Console.Error.WriteLine($"Ошибка в ProcessorSharing: {ex}");

                // Возвращаем понятное сообщение об ошибке пользователю
                return new Dictionary<string, object>
                {
                    ["error"] = true,
                    ["message"] = GetUserFriendlyErrorMessage(ex),
                    ["details"] = ex.Message,
                    ["stack_trace"] = ex.StackTrace
                };
            }
        }

        private static void ValidateParameters(Dictionary<string, object> parameters)
        {
            if (!parameters.ContainsKey("num_jobs") || !parameters.ContainsKey("num_workers") ||
                !parameters.ContainsKey("job_durations") || !parameters.ContainsKey("worker_productivities"))
            {
                throw new ArgumentException("Не все обязательные параметры предоставлены");
            }

            int numJobs = Convert.ToInt32(parameters["num_jobs"]);
            int numWorkers = Convert.ToInt32(parameters["num_workers"]);

            if (numWorkers > numJobs)
            {
                throw new ArgumentException("Количество работников не может превышать количество работ");
            }

            if (numJobs <= 0 || numWorkers <= 0)
            {
                throw new ArgumentException("Количество работ и работников должно быть положительным");
            }
        }

        private static (List<Job>, List<Worker>) PrepareData(Dictionary<string, object> parameters)
        {
            int numJobs = Convert.ToInt32(parameters["num_jobs"]);
            int numWorkers = Convert.ToInt32(parameters["num_workers"]);

            var jobDurationsRaw = (List<List<object>>)parameters["job_durations"];
            var workerProductivitiesRaw = (List<List<object>>)parameters["worker_productivities"];

            // Проверяем соответствие размеров массивов
            if (jobDurationsRaw.Count != numJobs || workerProductivitiesRaw.Count != numWorkers)
            {
                throw new ArgumentException("Несоответствие размеров входных данных");
            }

            var jobs = new List<Job>();
            for (int i = 0; i < numJobs; i++)
            {
                try
                {
                    jobs.Add(new Job(i + 1)
                    {
                        Name = $"Job {i + 1}",
                        RemainingDuration = Convert.ToDouble(jobDurationsRaw[i][0])
                    });
                }
                catch
                {
                    throw new ArgumentException($"Некорректная длительность для работы {i + 1}");
                }
            }

            var workers = new List<Worker>();
            for (int i = 0; i < numWorkers; i++)
            {
                try
                {
                    workers.Add(new Worker(i + 1)
                    {
                        Name = $"Worker {i + 1}",
                        Productivity = Convert.ToDouble(workerProductivitiesRaw[i][0])
                    });
                }
                catch
                {
                    throw new ArgumentException($"Некорректная производительность для работника {i + 1}");
                }
            }

            // Сортируем работы и работников
            jobs = jobs.OrderByDescending(j => j.RemainingDuration).ToList();
            workers = workers.OrderByDescending(w => w.Productivity).ToList();

            return (jobs, workers);
        }

        private static Schedule CreateSchedule(List<Job> jobs, List<Worker> workers)
        {
            var schedule = new Schedule();
            double currentTime = 0;

            while (jobs.Any(j => j.RemainingDuration > 0))
            {
                var activeJobs = jobs.Where(j => j.RemainingDuration > 0)
                                   .OrderByDescending(j => j.RemainingDuration)
                                   .ToList();

                var assignments = AssignJobsToWorkers(activeJobs, workers);
                double timeStep = CalculateTimeStep(assignments, activeJobs);
                UpdateSchedule(schedule, assignments, timeStep, ref currentTime);
            }

            schedule.CalculateTotalDuration();
            return schedule;
        }

        private static Dictionary<Worker, List<Job>> AssignJobsToWorkers(List<Job> activeJobs, List<Worker> workers)
        {
            var assignments = new Dictionary<Worker, List<Job>>();
            foreach (var worker in workers)
            {
                assignments[worker] = new List<Job>();
            }

            var priorityGroups = activeJobs
                .GroupBy(j => j.RemainingDuration)
                .OrderByDescending(g => g.Key)
                .ToList();

            int workerIndex = 0;
            foreach (var group in priorityGroups)
            {
                var jobsInGroup = group.ToList();
                int jobsCount = jobsInGroup.Count;

                if (jobsCount >= workers.Count)
                {
                    for (int i = 0; i < jobsCount; i++)
                    {
                        assignments[workers[i % workers.Count]].Add(jobsInGroup[i]);
                    }
                    break;
                }
                else
                {
                    while (jobsCount < workers.Count && workerIndex < priorityGroups.Count - 1)
                    {
                        workerIndex++;
                        jobsInGroup.AddRange(priorityGroups[workerIndex].ToList());
                        jobsCount = jobsInGroup.Count;
                    }

                    for (int i = 0; i < jobsCount; i++)
                    {
                        assignments[workers[i % workers.Count]].Add(jobsInGroup[i]);
                    }
                    break;
                }
            }

            return assignments;
        }

        private static double CalculateTimeStep(Dictionary<Worker, List<Job>> assignments, List<Job> activeJobs)
        {
            double timeStep = double.MaxValue;

            foreach (var assignment in assignments)
            {
                if (assignment.Value.Any())
                {
                    double productivityPerJob = assignment.Key.Productivity / assignment.Value.Count;
                    foreach (var job in assignment.Value)
                    {
                        timeStep = Math.Min(timeStep, job.RemainingDuration / productivityPerJob);
                    }
                }
            }

            var distinctRemainingTimes = activeJobs.Select(j => j.RemainingDuration)
                                                 .Distinct()
                                                 .OrderBy(d => d)
                                                 .ToList();

            for (int i = 1; i < distinctRemainingTimes.Count; i++)
            {
                double diff = distinctRemainingTimes[i] - distinctRemainingTimes[i - 1];
                if (diff > 0)
                {
                    timeStep = Math.Min(timeStep, diff);
                }
            }

            if (timeStep == double.MaxValue)
            {
                throw new InvalidOperationException("Не удалось определить время следующего шага");
            }

            return timeStep;
        }

        private static void UpdateSchedule(Schedule schedule, Dictionary<Worker, List<Job>> assignments,
                                         double timeStep, ref double currentTime)
        {
            foreach (var assignment in assignments)
            {
                if (assignment.Value.Any())
                {
                    double productivityPerJob = assignment.Key.Productivity / assignment.Value.Count;
                    foreach (var job in assignment.Value)
                    {
                        double completed = timeStep * productivityPerJob;
                        job.RemainingDuration -= completed;

                        schedule.AddItem(new ScheduleItem(job, new Stage(1)
                        {
                            StartTime = currentTime,
                            EndTime = currentTime + timeStep
                        });
                    }
                }
            }

            currentTime += timeStep;
        }

        private static Dictionary<string, object> FormatResults(Schedule schedule)
        {
            return new Dictionary<string, object>
            {
                ["total_duration"] = schedule.TotalDuration,
                ["schedule_details"] = FormatScheduleDetails(schedule),
                ["gantt_data"] = GanttChartGenerator.GenerateChartData(schedule),
                ["success"] = true
            };
        }

        private static string FormatScheduleDetails(Schedule schedule)
        {
            return string.Join("\n", schedule.Items
                .OrderBy(item => item.StartTime)
                .Select(item => $"{item.Worker.Name}: {item.Job.Name} - " +
                               $"Time: {item.StartTime:0.##}-{item.EndTime:0.##} " +
                               $"(Duration: {item.EndTime - item.StartTime:0.##})"));
        }

        private static string GetUserFriendlyErrorMessage(Exception ex)
        {
            return ex switch
            {
                ArgumentException _ => $"Ошибка входных данных: {ex.Message}",
                InvalidOperationException _ => $"Ошибка выполнения алгоритма: {ex.Message}",
                _ => "Произошла непредвиденная ошибка при выполнении алгоритма"
            };
        }
    }
}