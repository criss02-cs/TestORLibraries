using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.OrTools.Sat;
using TestORLibraries.Entity;

namespace TestORLibraries;
internal class GoogleSolver : ISolver
{
    public void Solve(List<List<JobTask>> allJobs, int horizon, int numMachines, int[] allMachines)
    {
        // Creates the model.
        var model = new CpModel();

        var allTasks =
            new Dictionary<Tuple<int, int>, Tuple<IntVar, IntVar, IntervalVar>>(); // (start, end, duration)
        var machineToIntervals = new Dictionary<int, List<IntervalVar>>();
        for (var jobId = 0; jobId < allJobs.Count; ++jobId)
        {
            var job = allJobs[jobId];
            for (var taskId = 0; taskId < job.Count; ++taskId)
            {
                var task = job[taskId];
                var suffix = $"_{jobId}_{taskId}";
                var start = model.NewIntVar(0, horizon, "start" + suffix);
                var end = model.NewIntVar(0, horizon, "end" + suffix);
                var interval = model.NewIntervalVar(start, task.Duration, end, "interval" + suffix);
                var key = Tuple.Create(jobId, taskId);
                allTasks[key] = Tuple.Create(start, end, interval);
                if (!machineToIntervals.ContainsKey(task.Machine))
                {
                    machineToIntervals.Add(task.Machine, []);
                }
                machineToIntervals[task.Machine].Add(interval);
            }
        }

        // Create and add disjunctive constraints.
        foreach (var machine in allMachines)
        {
            model.AddNoOverlap(machineToIntervals[machine]);
        }

        // Precedences inside a job.
        for (var jobId = 0; jobId < allJobs.Count; ++jobId)
        {
            var job = allJobs[jobId];
            for (var taskId = 0; taskId < job.Count - 1; ++taskId)
            {
                var key = Tuple.Create(jobId, taskId);
                var nextKey = Tuple.Create(jobId, taskId + 1);
                model.Add(allTasks[nextKey].Item1 >= allTasks[key].Item2);
            }
        }

        // Makespan objective.
        var objVar = model.NewIntVar(0, horizon, "makespan");

        var ends = new List<IntVar>();
        for (int jobID = 0; jobID < allJobs.Count(); ++jobID)
        {
            var job = allJobs[jobID];
            var key = Tuple.Create(jobID, job.Count() - 1);
            ends.Add(allTasks[key].Item2);
        }
        model.AddMaxEquality(objVar, ends);
        model.Minimize(objVar);

        // Solve
        var solver = new CpSolver();
        var stopwatch = new Stopwatch();
        stopwatch.Start();
        var status = solver.Solve(model);
        stopwatch.Stop();
        // print elapsed milliseconds
        Console.WriteLine($"Elapsed time: {stopwatch.ElapsedMilliseconds} ms");
        Console.WriteLine($"Solve status: {status}");

        if (status is CpSolverStatus.Optimal or CpSolverStatus.Feasible)
        {
            Console.WriteLine("Solution:");
            var assignedJobs = new Dictionary<int, List<AssignedTask>>();
            for (var jobId = 0; jobId < allJobs.Count; ++jobId)
            {
                var job = allJobs[jobId];
                for (var taskId = 0; taskId < job.Count; ++taskId)
                {
                    var task = job[taskId];
                    var key = Tuple.Create(jobId, taskId);
                    var start = (int)solver.Value(allTasks[key].Item1);
                    if (!assignedJobs.ContainsKey(task.Machine))
                    {
                        assignedJobs.Add(task.Machine, []);
                    }
                    assignedJobs[task.Machine].Add(new AssignedTask(jobId, taskId, start, task.Duration));
                }
            }

            // Create per machine output lines.
            var output = "";
            foreach (var machine in allMachines)
            {
                // Sort by starting time.
                assignedJobs[machine].Sort();
                var solLineTasks = $"Machine {machine}: ";
                var solLine = "           ";

                foreach (var assignedTask in assignedJobs[machine])
                {
                    var name = $"job_{assignedTask.jobID}_task_{assignedTask.taskID}";
                    // Add spaces to output to align columns.
                    solLineTasks += $"{name,-15}";

                    var solTmp = $"[{assignedTask.start},{assignedTask.start + assignedTask.duration}]";
                    // Add spaces to output to align columns.
                    solLine += $"{solTmp,-15}";
                }
                output += solLineTasks + "\n";
                output += solLine + "\n";
            }
            // Finally print the solution found.
            Console.WriteLine($"Optimal Schedule Length: {solver.ObjectiveValue}");
            Console.WriteLine($"\n{output}");
        }
        else
        {
            Console.WriteLine("No solution found.");
        }

        Console.WriteLine("Statistics");
        Console.WriteLine($"  conflicts: {solver.NumConflicts()}");
        Console.WriteLine($"  branches : {solver.NumBranches()}");
        Console.WriteLine($"  wall time: {solver.WallTime()}s");
    }
}
