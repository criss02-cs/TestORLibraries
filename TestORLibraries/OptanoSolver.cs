using System.Diagnostics;
using System.Linq;
using OperationsResearch;
using OPTANO.Modeling.Optimization;
using OPTANO.Modeling.Optimization.Enums;
using OPTANO.Modeling.Optimization.Operators;
using OPTANO.Modeling.Optimization.Solver;
using OPTANO.Modeling.Optimization.Solver.Cplex128;
using OPTANO.Modeling.Optimization.Solver.Gurobi1100;
using OPTANO.Modeling.Optimization.Solver.Z3;
using TestORLibraries.Entity;

namespace TestORLibraries;
public class OptanoSolver : ISolver
{
    public void Solve(List<List<JobTask>> allJobs, int horizon, int numMachines, int[] allMachines)
    {
        var model = new Model();
        var startTimes = new VariableCollection<JobTask> (
            model,
            allJobs.SelectMany(x => x),
            "startTimes",
            (t) => $"StartTime_t{t}",
            (_) => 0,
            (_) => int.MaxValue,
            (_) => VariableType.Integer,
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

        //foreach (var machine in allMachines)
        //{
        //    var tasksOnMachine = allJobs
        //        .SelectMany(job => job)
        //        .Where(task => task.Machine == machine)
        //        .ToList();
        //    model.AddConstraint(Expression.Sum(tasksOnMachine.Select(x => overlap[x, machine])) <= 1, $"MaxOneTaskPerMachine_m{machine}");;
        //    //}
        //}


        // vincolo di precedenza
        foreach (var job in allJobs)
        {
            var durationSum = 0;
            for (int i = 0; i < job.Count - 1; i++)
            {
                var predecessor = job[i];
                var successor = job[i + 1];
                //durationSum += predecessor.Duration;
                //var first = startTimes[successor] >= startTimes[predecessor] + predecessor.Duration;
                //model.AddConstraint(first, $"StartAfter_{predecessor}_t{successor}");

                //var second = startTimes[successor] >= durationSum;
                //model.AddConstraint(second, $"StartsAfterSum_t{predecessor}_t{successor}");

                model.AddConstraint(startTimes[successor] >= startTimes[predecessor] + predecessor.Duration);
            }
        }

        // vincolo di non sovrapposizione
        foreach (var machine in allMachines)
        {
            var tasksOnMachine = new List<(int i, int j)>();
            for (var i = 0; i < allJobs.Count; i++)
            {
                for (var j = 0; j < allJobs[i].Count; j++)
                {
                    if (allJobs[i][j].Machine == machine)
                    {
                        tasksOnMachine.Add((i, j));
                    }
                }
            }

            for (var t1 = 0; t1 < tasksOnMachine.Count; t1++)
            {
                for (var t2 = t1 + 1; t2 < tasksOnMachine.Count; t2++)
                {
                    var (i1, j1) = tasksOnMachine[t1];
                    var (i2, j2) = tasksOnMachine[t2];
                    var task1 = allJobs[i1][j1];
                    var task2 = allJobs[i2][j2];
                    var prec = new Variable($"{i1}-{j1}_precedes_{i2}{j2}", 0, 1, VariableType.Binary);
                    model.AddConstraint(
                        startTimes[task1] + task1.Duration - horizon * (1 - prec) <= startTimes[task2]);
                    model.AddConstraint(
                        startTimes[task2] + task2.Duration - horizon * prec <= startTimes[task1]);
                }
            }
        }

        // funzione obiettivo
        foreach (var machine in allMachines)
        {
            var tasksOnMachine = allJobs.SelectMany(job => job).Where(task => task.Machine == machine).ToList();
            foreach (var task in tasksOnMachine)
            {
                model.AddConstraint(latestEnd >= startTimes[task] + task.Duration, $"LatestEnd_t{task}_m{machine}");
            }
        }

        model.AddObjective(new Objective(latestEnd, "minimize",
            ObjectiveSense.Minimize));
        //model.AddObjective(new Objective(objVar, "", ObjectiveSense.Minimize));
        using var solver = new Z3Solver();
        var stopwatch = new Stopwatch();
        stopwatch.Start();
        var result = solver.Solve(model);
        stopwatch.Stop();
        Console.ForegroundColor = ConsoleColor.Magenta;
        System.Console.WriteLine("########### Optano Microsoft Z3 #########################");
        //Console.WriteLine(model.ToString());
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
                    var startTime = result.VariableValues[startTimes[task].Name];
                    if (!assignedJobs.ContainsKey(task.Machine))
                    {
                        assignedJobs.Add(task.Machine, []);
                    }

                    assignedJobs[task.Machine].Add(new AssignedTask(allJobs.IndexOf(job), job.IndexOf(task) + 1,
                        (int)startTime, task.Duration));
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
        System.Console.WriteLine("########### Optano Microsoft Z3 #########################");
    }
}
