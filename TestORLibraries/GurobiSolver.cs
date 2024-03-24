
using Gurobi;
using ILOG.CPLEX;
using System.Diagnostics;
using TestORLibraries.Entity;

namespace TestORLibraries;
internal class GurobiSolver : ISolver
{ 
    public void Solve(List<List<JobTask>> allJobs, int horizon, int numMachines, int[] allMachines)
    {
        var env = new GRBEnv();
        var model = new GRBModel(env);
        
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

        var startTimes = new GRBVar[allJobs.Count][];
        for (var i = 0; i < allJobs.Count; i++)
        {
            startTimes[i] = new GRBVar[allJobs[i].Count];
            for (var j = 0; j < allJobs[i].Count; j++)
            {
                startTimes[i][j] = model.AddVar(0, int.MaxValue, 0, GRB.INTEGER, $"start_{i}_{j}");
            }
        }

        // vincolo di precedenza
        for (var i = 0; i < allJobs.Count; i++)
        {
            for (var j = 0; j < allJobs[i].Count - 1; j++)
            {
                var leftSide = new GRBLinExpr(startTimes[i][j + 1], 1);
                model.AddConstr(leftSide, GRB.GREATER_EQUAL, startTimes[i][j] + durations[i, j], $"precedence_{i}{j}");
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
                    var prec = model.AddVar(0, 1, 0, GRB.BINARY, $"{i1}-{j1}_precedes_{i2}{j2}");
                    var s = startTimes[i1][j1] + durations[i1, j1] - horizon * (1 - prec);
                    model.AddConstr(s, GRB.LESS_EQUAL, startTimes[i2][j2], $"non_overlap_{i1}{j1}_{i2}{j2}");
                    s = startTimes[i2][j2] + durations[i2, j2] - horizon * prec;
                    model.AddConstr(s, GRB.LESS_EQUAL, startTimes[i1][j1], $"non_overlap_{i2}{j2}_{i1}{j1}");
                }
            }
        }

        // funzione obiettivo
        var maxCompletionTime = model.AddVar(0, int.MaxValue, 0, GRB.INTEGER, "maxCompletionTime");
        for (int i = 0; i < allJobs.Count; i++)
        {
            for (int j = 0; j < allJobs[i].Count; j++)
            {
                // Il tempo di completamento massimo deve essere maggiore o uguale al tempo di completamento di ogni task
                model.AddConstr(maxCompletionTime, GRB.GREATER_EQUAL, startTimes[i][j] + durations[i, j], $"max_{i}_{j}");
                //cplex.AddGe(maxCompletionTime, cplex.Sum(startTimes[i][j], durations[i, j]));
            }
        }

        model.SetObjective(new GRBLinExpr(maxCompletionTime, 1), GRB.MINIMIZE);
        var stopwatch = new Stopwatch();
        stopwatch.Start();
        model.Optimize();
        stopwatch.Stop();
        Console.ForegroundColor = ConsoleColor.Yellow;
        System.Console.WriteLine("########### GUROBI #########################");
        Console.WriteLine($"Elapsed time: {stopwatch.ElapsedMilliseconds} ms");
        Console.WriteLine($"Solve status: {model.Status}");
        if (model.Status == GRB.Status.OPTIMAL)
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
                            double startTime = model.GetVarByName($"start_{j}_{i}").X;
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
            Console.WriteLine($"Optimal Schedule Length: {model.ObjVal}");
            Console.WriteLine($"\n{output}");
        }
        else
        {
            Console.WriteLine("No solution found.");
        }
        System.Console.WriteLine("################ GUROBI ####################");
    }
}
