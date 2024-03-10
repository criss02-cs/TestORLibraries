using System.Diagnostics;
using System.Linq;
using OperationsResearch;
using OPTANO.Modeling.Optimization;
using OPTANO.Modeling.Optimization.Enums;
using OPTANO.Modeling.Optimization.Operators;
using OPTANO.Modeling.Optimization.Solver;
using OPTANO.Modeling.Optimization.Solver.Gurobi1100;
using OPTANO.Modeling.Optimization.Solver.Z3;
using TestORLibraries.Entity;

namespace TestORLibraries;
public class OptanoSolver : ISolver
{
    public void Solve(List<List<JobTask>> allJobs, int horizon, int numMachines, int[] allMachines)
    {
        var model = new Model();
        var startTimes = new VariableCollection<JobTask, int>(
            model,
            allJobs.SelectMany(x => x),
            allMachines,
            "startTimes",
            (t, m) => $"StartTime_t{t}_m{m}",
            (_, _) => 0,
            (_, _) => double.PositiveInfinity,
            (_, _) => VariableType.Continuous,
            null);
        var overlap = new VariableCollection<JobTask, int>(
            model,
            allJobs.SelectMany(job => job),
            allMachines,
            "overlap",
            (j, m) => $"Assignment_t{j.TaskId}_m{m}",
            (_, _) => 0,
            (_, _) => 1,
            (_, _) => VariableType.Binary,
            (t, _) => t.TaskId);
        var latestEnd = new Variable("LatestEnd", 0, Double.MaxValue, VariableType.Continuous);
        foreach (var machine in allMachines)
        {
            var tasksOnMachine = allJobs.SelectMany(job => job).Where(task => task.Machine == machine).ToList();
            model.AddConstraint(Expression.Sum(tasksOnMachine.Select(x => overlap[x, machine])) <= 1, $"MaxOneTaskPerMachine_m{machine}");;
            //}
        }
        foreach (var job in allJobs)
        {
            var durationSum = 0;
            for (int i = 0; i < job.Count - 1; i++)
            {
                var predecessor = job[i];
                var successor = job[i + 1];
                durationSum += predecessor.Duration;
                var first = startTimes[successor, successor.Machine] >= startTimes[predecessor, predecessor.Machine] + predecessor.Duration;
                model.AddConstraint(first, $"StartAfter_{predecessor}_t{successor}");

                var second = startTimes[successor, successor.Machine] >= durationSum;
                model.AddConstraint(second, $"StartsAfterSum_t{predecessor}_t{successor}");
            }
        }

        foreach (var machine in allMachines)
        {
            var tasksOnMachine = allJobs.SelectMany(job => job).Where(task => task.Machine == machine).ToList();
            foreach (var task in tasksOnMachine)
            {
                model.AddConstraint(latestEnd >= startTimes[task, machine] + task.Duration, $"LatestEnd_t{task}_m{machine}");
            }
        }

        var objExpr = latestEnd +
                      Expression.Sum(allJobs.SelectMany(job => job)
                          .Select(task =>
                              (overlap[task, task.Machine] * task.TaskId * 0.001) +
                              (startTimes[task, task.Machine] * 0.01)));
        model.AddObjective(new Objective(objExpr, "minimize",
            ObjectiveSense.Minimize));
        //model.AddObjective(new Objective(objVar, "", ObjectiveSense.Minimize));
        using (var solver = new GurobiSolver())
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var result = solver.Solve(model);
            stopwatch.Stop();
            Console.WriteLine(model.ToString());
            Console.WriteLine($"Elapsed time: {stopwatch.ElapsedMilliseconds} ms");
            Console.WriteLine($"Solve status: {result.Status}");
            if (result.Status is SolutionStatus.Feasible or SolutionStatus.Optimal)
            {
                var assignedJobs = new Dictionary<int, List<AssignedTask>>();
                foreach (var job in allJobs)
                {
                    foreach (var task in job)
                    {
                        var key = Tuple.Create(allJobs.IndexOf(job), job.IndexOf(task));
                        var startTime = result.VariableValues[startTimes[task, task.Machine].Name];
                        if (!assignedJobs.ContainsKey(task.Machine))
                        {
                            assignedJobs.Add(task.Machine, []);
                        }

                        assignedJobs[task.Machine].Add(new AssignedTask(allJobs.IndexOf(job), job.IndexOf(task),
                            (int)startTime, task.Duration));
                        Console.WriteLine($"Task {task.TaskId} del Job {allJobs.IndexOf(job)} inizia al tempo {startTime} assegnato alla macchina {task.Machine}");
                    }
                }
                var output = "";
                foreach (var machine in allMachines)
                {
                    assignedJobs[machine].Sort();
                    var solLineTasks = $"Machine {machine}: ";
                    var solLine = "           ";
                    foreach (var assignedTask in assignedJobs[machine])
                    {
                        String name = $"job_{assignedTask.jobID}_task_{assignedTask.taskID}";
                        // Add spaces to output to align columns.
                        solLineTasks += $"{name,-15}";

                        String solTmp = $"[{assignedTask.start},{assignedTask.start + assignedTask.duration}]";
                        // Add spaces to output to align columns.
                        solLine += $"{solTmp,-15}";
                    }
                    output += solLineTasks + "\n";
                    output += solLine + "\n";
                }
                Console.WriteLine($"Optimal Schedule Length: {result.ObjectiveValues.First().Value}");
                Console.WriteLine($"Latest end: {latestEnd.Value}");
                Console.WriteLine($"\n{output}");
            }
            else
            {
                Console.WriteLine("Non è stata trovata una soluzione ottimale");
            }
        }
    }
}
