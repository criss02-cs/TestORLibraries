using ILOG.CPLEX;
using ILOG.Concert;
using TestORLibraries.Entity;
using System.Diagnostics;
using static ILOG.CPLEX.Cplex;

namespace TestORLibraries;
public class IbmSolver : ISolver
{
    public void Solve(List<List<JobTask>> allJobs, int horizon, int numMachines, int[] allMachines)
    {
        var cplex = new Cplex();
        var numTasks = allJobs.Max(x => x.Count);

        int[,] durations = new int[allJobs.Count, numTasks];
        int[,] machines = new int[allJobs.Count, numTasks];

        for (var i = 0; i < allJobs.Count; i++)
        {
            for (var j = 0; j < allJobs[i].Count; j++)
            {
                durations[i, j] = allJobs[i][j].Duration;
                machines[i, j] = allJobs[i][j].Machine;
            }
        }

        var startTimes = new INumVar[allJobs.Count][];
        for (var i = 0; i < allJobs.Count; i++)
        {
            startTimes[i] = new INumVar[allJobs[i].Count];
            for (var j = 0; j < allJobs[i].Count; j++)
            {
                startTimes[i][j] = cplex.NumVar(0, int.MaxValue, NumVarType.Int, $"start_{i}_{j}");
            }
        }


        // vincolo di precedenza
        for (var i = 0; i < allJobs.Count; i++)
        {
            for (var j = 0; j < allJobs[i].Count - 1; j++)
            {
                cplex.AddGe(startTimes[i][j + 1], cplex.Sum(startTimes[i][j], durations[i, j]));
            }
        }

        // vincolo di non sovrapposizione
        for (var m = 0; m < numMachines; m++)
        {
            var tasksOnMachine = new List<(int i, int j)>();
            for (var i = 0; i < allJobs.Count; i++)
            {
                for (var j = 0; j < allJobs[i].Count; j++)
                {
                    if (machines[i, j] == m)
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
                    var prec = cplex.BoolVar($"{i1}-{j1}_precedes_{i2}{j2}");
                    var s = cplex.Sum(startTimes[i1][j1], cplex.Diff(durations[i1, j1], cplex.Prod(horizon, cplex.Diff(1, prec))));
                    cplex.AddLe(s, startTimes[i2][j2]);
                    cplex.AddLe(cplex.Sum(startTimes[i2][j2], cplex.Diff(durations[i2, j2], cplex.Prod(horizon, prec))), startTimes[i1][j1]);
                }
            }
        }

        // funzione obiettivo
        INumVar maxCompletionTime = cplex.NumVar(0, int.MaxValue, NumVarType.Int, "maxCompletionTime");
        for (int i = 0; i < allJobs.Count; i++)
        {
            for (int j = 0; j < allJobs[i].Count; j++)
            {
                // Il tempo di completamento massimo deve essere maggiore o uguale al tempo di completamento di ogni task
                cplex.AddGe(maxCompletionTime, cplex.Sum(startTimes[i][j], durations[i, j]));
            }
        }

        cplex.AddMinimize(maxCompletionTime);

        // Risoluzione del problema
        var stopwatch = new Stopwatch();
        stopwatch.Start();
        cplex.Solve();
        stopwatch.Stop();
        System.Console.WriteLine("########### IBM #########################");
        Console.WriteLine($"Elapsed time: {stopwatch.ElapsedMilliseconds} ms");
        Console.WriteLine($"Solve status: {cplex.GetStatus()}");
        if (cplex.GetStatus() == Cplex.Status.Optimal || cplex.GetStatus() == Cplex.Status.Feasible)
        {
            var output = "";
            for (int m = 0; m < numMachines; m++)
            {
                List<AssignedTask> assignedTasks = new List<AssignedTask>();

                for (int j = 0; j < allJobs.Count; j++)
                {
                    for (int i = 0; i < allJobs[j].Count; i++)
                    {
                        if (allJobs[j][i].Machine == m)
                        {
                            double startTime = cplex.GetValue(startTimes[j][i]);
                            assignedTasks.Add(new AssignedTask(j, i + 1, (int)startTime, allJobs[j][i].Duration));
                        }
                    }
                }

                assignedTasks.Sort();

                var solLineTasks = $"Machine {m}: ";
                var solLine = "           ";

                //System.Console.WriteLine("JobId\tTaskId\tStart\tEnd");
                foreach (AssignedTask task in assignedTasks)
                {
                    var name = $"job_{task.jobID}_task_{task.taskID}";
                    solLineTasks += $"{name,-15}";
                    var solTmp = $"[{task.start},{task.start + task.duration}]";
                    solLine += $"{solTmp,-15}";
                }
                output += solLineTasks + "\n";
                output += solLine + "\n";
            }
            Console.WriteLine($"Optimal Schedule Length: {cplex.ObjValue}");
            Console.WriteLine($"\n{output}");
        }
        else
        {
            Console.WriteLine("No solution found.");
        }
        System.Console.WriteLine("################ IBM ####################");
    }
}
