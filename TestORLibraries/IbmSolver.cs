using ILOG.CPLEX;
using ILOG.Concert;
using TestORLibraries.Entity;
using System.Diagnostics;

namespace TestORLibraries;
public class IbmSolver : ISolver
{
    //    // Crea il modello
    //    Cplex cplex = new Cplex();

    //    // Definisci i dati del problema
    //    int numJobs = 3;
    //    int numMachines = 2;
    //    int[,] durations = { { 3, 2 }, { 2, 1 }, { 1, 3 } };
    //    int[,] machines = { { 0, 1 }, { 1, 0 }, { 0, 1 } };

    //    // Crea le variabili
    //    INumVar[][] startTimes = new INumVar[numJobs][];
    //        for (int i = 0; i<numJobs; ++i)
    //        {
    //            startTimes[i] = new INumVar[numMachines];
    //            for (int j = 0; j<numMachines; ++j)
    //            {
    //                startTimes[i][j] = cplex.NumVar(0, double.MaxValue, NumVarType.Float, $"start_{i}_{j}");
    //            }
    //        }

    //        // Aggiungi i vincoli
    //        for (int i = 0; i < numJobs; ++i)
    //{
    //    for (int j = 0; j < numMachines - 1; ++j)
    //    {
    //        // Vincolo di precedenza: un task può iniziare solo se il precedente è terminato
    //        cplex.AddGe(startTimes[i][j + 1], cplex.Sum(startTimes[i][j], durations[i, j]));
    //    }
    //}

    //for (int j = 0; j < numMachines; ++j)
    //{
    //    for (int i = 0; i < numJobs - 1; ++i)
    //    {
    //        for (int k = i + 1; k < numJobs; ++k)
    //        {
    //            if (machines[i, j] == machines[k, j])
    //            {
    //                // Vincolo di non sovrapposizione: una macchina può eseguire solo un task alla volta
    //                cplex.AddLe(startTimes[i][j], cplex.Diff(startTimes[k][j], durations[i, j]));
    //                cplex.AddLe(startTimes[k][j], cplex.Diff(startTimes[i][j], durations[k, j]));
    //            }
    //        }
    //    }
    //}

    //// Definisci la funzione obiettivo: minimizza il tempo di completamento massimo
    //INumVar maxCompletionTime = cplex.NumVar(0, double.MaxValue, NumVarType.Float, "maxCompletionTime");
    //for (int i = 0; i < numJobs; ++i)
    //{
    //    for (int j = 0; j < numMachines; ++j)
    //    {
    //        // Il tempo di completamento massimo deve essere maggiore o uguale al tempo di completamento di ogni task
    //        cplex.AddGe(maxCompletionTime, cplex.Sum(startTimes[i][j], durations[i, j]));
    //    }
    //}
    //cplex.AddMinimize(maxCompletionTime);

    //// Risolve il problema
    //if (cplex.Solve())
    //{
    //    System.Console.WriteLine("Solution status = " + cplex.GetStatus());
    //    System.Console.WriteLine("Solution value  = " + cplex.ObjValue);
    //    for (int i = 0; i < numJobs; ++i)
    //    {
    //        for (int j = 0; j < numMachines; ++j)
    //        {
    //            System.Console.WriteLine($"Job {i}, machine {j} starts at {cplex.GetValue(startTimes[i][j])}");
    //        }
    //    }
    //}

    //cplex.End();
    public void Solve(List<List<JobTask>> allJobs, int horizon, int numMachines, int[] allMachines)
    {
        Cplex cplex = new Cplex();

        // Definisci le variabili decisionali
        INumVar[][][] x = new INumVar[allJobs.Count][][];
        for (int j = 0; j < allJobs.Count; j++)
        {
            x[j] = new INumVar[allJobs[j].Count][];
            for (int i = 0; i < allJobs[j].Count; i++)
            {
                x[j][i] = cplex.BoolVarArray(horizon);
            }
        }

        // Definisci l'obiettivo
        ILinearNumExpr objective = cplex.LinearNumExpr();
        for (int j = 0; j < allJobs.Count; j++)
        {
            for (int i = 0; i < allJobs[j].Count; i++)
            {
                for (int t = 0; t < horizon; t++)
                {
                    objective.AddTerm(t, x[j][i][t]);
                }
            }
        }
        cplex.AddMinimize(objective);

        // Aggiungi le restrizioni
        for (int j = 0; j < allJobs.Count; j++)
        {
            for (int i = 0; i < allJobs[j].Count; i++)
            {
                cplex.AddEq(cplex.Sum(x[j][i]), 1);  // Ogni compito deve essere schedulato una volta
            }
        }

        for (int m = 0; m < numMachines; m++)
        {
            for (int t = 0; t < horizon; t++)
            {
                ILinearNumExpr sum = cplex.LinearNumExpr();
                for (int j = 0; j < allJobs.Count; j++)
                {
                    for (int i = 0; i < allJobs[j].Count; i++)
                    {
                        if (allJobs[j][i].Machine == m)
                        {
                            for (int d = 0; d < allJobs[j][i].Duration; d++)
                            {
                                if (t - d >= 0)
                                {
                                    sum.AddTerm(1, x[j][i][t - d]);
                                }
                            }
                        }
                    }
                }
                cplex.AddLe(sum, 1);  // Non possono essere eseguiti più compiti contemporaneamente su una stessa macchina
            }
        }
        for (int j = 0; j < allJobs.Count; j++)
        {
            for (int i = 0; i < allJobs[j].Count - 1; i++)  // Per ogni coppia di task consecutivi
            {
                for (int t = 0; t < horizon; t++)
                {
                    ILinearNumExpr expr = cplex.LinearNumExpr();
                    for (int d = 0; d < allJobs[j][i].Duration; d++)
                    {
                        if (t - d >= 0)
                        {
                            expr.AddTerm(1.0, x[j][i][t - d]);
                        }
                    }
                    cplex.AddLe(x[j][i + 1][Math.Min(t + allJobs[j][i].Duration, horizon - 1)], cplex.Diff(1, expr));  // Il task i+1 non può iniziare prima che il task i sia terminato
                }
            }
        }




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
                            for (int t = 0; t < horizon; t++)
                            {
                                if (cplex.GetValue(x[j][i][t]) > 0.5)  // Se il compito inizia al tempo t
                                {
                                    assignedTasks.Add(new AssignedTask(j, i + 1, t, t + allJobs[j][i].Duration));
                                }
                            }
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
        System.Console.WriteLine("####################################");
    }
}
