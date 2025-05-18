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
                // 1. Подготовка данных
                var (jobs, workers) = PrepareData(parameters);

                // 2. Построение расписания
                var schedule = BuildSchedule(jobs, workers);

                foreach (var j in jobs) Console.WriteLine(j.Name);
                foreach (var j in jobs) Console.WriteLine(j.RemainingDuration);
                foreach (var w in workers) Console.WriteLine(w.Name);
                foreach (var w in workers) Console.WriteLine(w.Productivity);

                // 3. Форматирование результатов
                return FormatResults(schedule);
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object>
                {
                    ["error"] = true,
                    ["message"] = ex.Message
                };
            }
        }

        private static (List<Job>, List<Worker>) PrepareData(Dictionary<string, object> parameters)
        {
            int numJobs = Convert.ToInt32(parameters["num_jobs"]);
            int numWorkers = Convert.ToInt32(parameters["num_workers"]);
            var jobDurations = (List<List<object>>)parameters["job_durations"];
            var workerProductivities = (List<List<object>>)parameters["worker_productivities"];

            // Проверки
            if (numWorkers > numJobs)
                throw new ArgumentException("Количество работников не может превышать количество работ");
            if (jobDurations.Count != numJobs || workerProductivities.Count != numWorkers)
                throw new ArgumentException("Несоответствие размеров входных данных");

            // Создание работ
            var jobs = new List<Job>();
            for (int i = 0; i < numJobs; i++)
            {
                jobs.Add(new Job(i + 1)
                {
                    Name = $"Работа {i + 1}",
                    RemainingDuration = Convert.ToDouble(jobDurations[i][0])
                });
            }

            // Создание работников
            var workers = new List<Worker>();
            for (int i = 0; i < numWorkers; i++)
            {
                workers.Add(new Worker(i + 1)
                {
                    Name = $"Работник {i + 1}",
                    Productivity = Convert.ToDouble(workerProductivities[i][0])
                });
            }

            // Сортировка
            jobs = jobs.OrderByDescending(j => j.RemainingDuration).ToList();
            workers = workers.OrderByDescending(w => w.Productivity).ToList();

            return (jobs, workers);
        }

        private static Schedule BuildSchedule(List<Job> jobs, List<Worker> workers)
        {
            foreach (var j in jobs) Console.WriteLine(j.Name);
            foreach (var j in jobs) Console.WriteLine(j.RemainingDuration);
            foreach (var w in workers) Console.WriteLine(w.Name);
            foreach (var w in workers) Console.WriteLine(w.Productivity);

            var schedule = new Schedule();
            double currentTime = 0;

            var assignments = new Dictionary<Worker, List<Job>>();
            var timeStep = 0.0;
            workers = workers.OrderByDescending(w => w.Productivity).ToList();
            // пока все работы не сравнялись по длительности
            while (!jobs.All(j => Math.Abs(j.RemainingDuration - jobs[0].RemainingDuration) < 0.0001))
            {
                Console.WriteLine("ППППППППППППППППППППП");
                // перед назначением округляем длительности до определённой точности
                // из за работы с дробными числами 

                // 1. назначаем работы работникам
                assignments = AssignJobs(jobs, workers);

                // 2. расчет времени до следующего назначения
                timeStep = CalculateTimeStep(assignments, jobs, workers);
                if (timeStep == 0) break;

                // 3. выполняем работы
                ProcessTimeStep(schedule, assignments, timeStep, ref currentTime);
            }

            // после того, как все работы сравнялись:
            assignments = new Dictionary<Worker, List<Job>>();
            foreach (var worker in workers)
                assignments[worker] = new List<Job>();

            jobs = jobs.OrderBy(j => j.Name).ToList();
            int count = 0;
            foreach(var worker in workers)
            {
                var jobsAssign = new List<Job>();
                for (int i = count; i < jobs.Count; i++)
                {
                    jobsAssign.Add(jobs[i]);
                }
                for (int i = 0; i < count; i++)
                {
                    jobsAssign.Add(jobs[i]);
                }
                assignments[worker] = jobsAssign;
                count++;
            }

            timeStep = (jobs[0].RemainingDuration * jobs.Count) / (workers.Sum(w => w.Productivity));

            // выполняем работы
            ProcessTimeStep(schedule, assignments, timeStep, ref currentTime);

            schedule.CalculateTotalDuration();
            return schedule;
        }

        private static Dictionary<string, object> FormatResults(Schedule schedule)
        {
            return new Dictionary<string, object>
            {
                ["gantt_data"] = GanttChartGenerator.GenerateChartData(schedule)
            };
        }

        private static double RoundToThreeDigits(double value)
        {
            return Math.Round(value, 3, MidpointRounding.AwayFromZero);
        }

        private static Dictionary<Worker, List<Job>> AssignJobs(List<Job> jobs, List<Worker> workers)
        {
            var assignments = new Dictionary<Worker, List<Job>>();
            foreach (var worker in workers)
                assignments[worker] = new List<Job>();

            var priorityGroups = jobs
                .GroupBy(j => j.RemainingDuration)
                .OrderByDescending(g => g.Key)
                .ToList();

            int workersCount = workers.Count;
            int assignedJobs = 0;

            if (priorityGroups.Count >= workersCount)
            {
                int count = 0;
                foreach (var group in priorityGroups)
                {
                    if (count < workersCount)
                    {
                        var jobsInGroup = group.ToList();
                        assignments[workers[count]] = jobsInGroup;
                        count++;
                    }
                }
            }
            else
            {
                int count = 0;
                foreach (var group in priorityGroups)
                {
                    var jobsInGroup = group.ToList();
                    if (jobsInGroup.Count > workersCount)
                    {
                        for (int i = 0; i < workersCount; i++)
                        {
                            assignments[workers[i]] = new List<Job>(jobsInGroup);
                            count++;
                        }
                    }
                    else
                    {
                        for (int i = 0; i < jobsInGroup.Count; i++)
                        {
                            if (count < workersCount)
                            {
                                assignments[workers[count]] = new List<Job> { jobsInGroup[i] };
                                count++;
                            }
                        }
                    }
                }
            }

            return assignments;
        }

        private static double CalculateTimeStep(Dictionary<Worker, List<Job>> assignments, List<Job> jobs, List <Worker> workers)
        {
            double minTime = double.MaxValue;

            // 1. Время до завершения любой из работ
            foreach (var assignment in assignments)
            {
                if (assignment.Value.Any())
                {
                    double productivityPerJob = assignment.Key.Productivity / assignment.Value.Count;
                    foreach (var job in assignment.Value)
                    {
                        double time = job.RemainingDuration / productivityPerJob;
                        minTime = Math.Min(minTime, time);
                    }
                }
            }

            // 2. Время до сравнения приоритетов

            for (int i = 0; i < workers.Count; i++)
            {
                for (int j = i; j < workers.Count; j++)
                {
                    double productivityPerJob1 = workers[i].Productivity / assignments[workers[i]].Count;
                    double productivityPerJob2 = workers[j].Productivity / assignments[workers[j]].Count;

                    double diffDur = Math.Abs(assignments[workers[i]][0].RemainingDuration - assignments[workers[j]][0].RemainingDuration);
                    double diffProd = Math.Abs(productivityPerJob1 - productivityPerJob2);

                    double time = diffDur / diffProd;

                    // time > 0.001 и для сравнения задач между собой разница их длительностей diffDur > 0.001
                    if (time > 0.001 && diffDur > 0.001)
                    {
                        minTime = Math.Min(minTime, time);
                    }
                }
            }

            // сравняется с наиболее приоритетной из неназначенных работ
            var lastWorker = workers[workers.Count - 1];
            double prodPerJob = lastWorker.Productivity / assignments[lastWorker].Count;
            // время наиболее приоритетной из неназначенных работ
            var lastJob = new Job(-1);
            foreach(var j in jobs)
            {
                if (j.RemainingDuration < assignments[lastWorker][0].RemainingDuration)
                {
                    lastJob = j;
                    break;
                }
            }
            if (lastJob.Id != -1)
            {
                double lastDiffDur = Math.Abs(assignments[lastWorker][0].RemainingDuration - lastJob.RemainingDuration);
                double lastTime = lastDiffDur / prodPerJob;
                // time > 0.001 и для сравнения задач между собой разница их длительностей diffDur > 0.001
                if (lastTime > 0.001 && lastDiffDur > 0.001)
                {
                    minTime = Math.Min(minTime, lastTime);
                }
            }

            if (minTime == double.MaxValue)
                throw new InvalidOperationException("Не удалось определить время шага");


            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("РАСЧЁТ ШАГА ДЛЯ НАЗНАЧЕННЫХ РАБОТ");
            foreach (var worker in workers)
            {
                foreach (var jobf in assignments[worker])
                {
                    Console.WriteLine(jobf.Name);
                }
            }
            Console.WriteLine(minTime);
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine();

            // округлять minTime
            return minTime;
        }

        private static void ProcessTimeStep(Schedule schedule, Dictionary<Worker, List<Job>> assignments,
                                         double timeStep, ref double currentTime)
        {
            foreach (var assignment in assignments)
            {
                if (assignment.Value.Any())
                {
                    double productivityPerJob = assignment.Key.Productivity / assignment.Value.Count;

                    var timeStepForJobs = timeStep / assignment.Value.Count;
                    var currentTimeForJobs = currentTime;
                    foreach (var job in assignment.Value)
                    {
                        double workDone = timeStep * productivityPerJob;
                        job.RemainingDuration -= workDone;
                        // и округлить  с какой то точностью

                        schedule.AddItem(new ScheduleItem(job, new Stage(1)
                        {
                            Name = $"Выполнение {job.Name}",
                            Duration = workDone
                        }, assignment.Key)
                        {
                            StartTime = currentTimeForJobs,
                            EndTime = currentTimeForJobs + timeStepForJobs
                        });
                        currentTimeForJobs += timeStepForJobs;
                    }
                }
            }

            // округлять currentTime
            currentTime += timeStep;
        }
    }
}