using System;
using System.Collections.Generic;

namespace Optimizator.Algorithms.Johnson
{
    public static class Johnson
    {
        public static Dictionary<string, object> Main(Dictionary<string, object> parameters)
        {
            try
            {
                // Получаем количество работ
                int numJobs = Convert.ToInt32(parameters["num_jobs"]);

                // Получаем и преобразуем матрицу времен выполнения
                var jobTimes = new List<List<double>>();
                var jobTimesRaw = (List<List<object>>)parameters["job_times"];

                foreach (var rowObj in jobTimesRaw)
                {
                    var row = new List<double>();
                    var rowList = rowObj as List<object>;

                    if (rowList == null)
                        throw new ArgumentException("Некорректный формат данных матрицы");

                    foreach (var item in rowList)
                    {
                        row.Add(Convert.ToDouble(item));
                    }
                    jobTimes.Add(row);
                }

                // Проверка входных данных
                if (jobTimes.Count != numJobs)
                {
                    throw new ArgumentException($"Ожидается {numJobs} работ, но получено {jobTimes.Count}");
                }

                foreach (var row in jobTimes)
                {
                    if (row.Count != 2)
                    {
                        throw new ArgumentException("Каждая работа должна содержать 2 значения времени выполнения");
                    }
                }

                // Алгоритм Джонсона
                var jobs = new List<int>();
                for (int i = 0; i < numJobs; i++) jobs.Add(i);

                // Разделение на группы
                var group1 = new List<int>();
                var group2 = new List<int>();

                foreach (var job in jobs)
                {
                    if (jobTimes[job][0] <= jobTimes[job][1])
                        group1.Add(job);
                    else
                        group2.Add(job);
                }

                // Сортировка групп
                group1.Sort((a, b) => jobTimes[a][0].CompareTo(jobTimes[b][0]));
                group2.Sort((a, b) => jobTimes[b][1].CompareTo(jobTimes[a][1]));

                // Объединение расписания
                var schedule = new List<int>();
                schedule.AddRange(group1);
                schedule.AddRange(group2);

                // Расчет времени
                double machine1Time = 0;
                double machine2Time = 0;

                foreach (var job in schedule)
                {
                    machine1Time += jobTimes[job][0];
                    machine2Time = Math.Max(machine1Time, machine2Time) + jobTimes[job][1];
                }

                // Преобразование нумерации (начинаем с 1)
                var resultSchedule = new List<int>();
                foreach (var job in schedule)
                {
                    resultSchedule.Add(job + 1);
                }

                return new Dictionary<string, object>
                {
                    ["optimal_time"] = machine2Time,
                    ["schedule"] = resultSchedule
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка выполнения алгоритма: {ex.Message}");
            }
        }
    }
}