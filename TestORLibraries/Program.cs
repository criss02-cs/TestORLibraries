// Definisco i dati del problema

using TestORLibraries;
using TestORLibraries.Entity;

var allJobs =
            new[] {
                [
                    // job0
                    new JobTask(0,3,1), // task0
                    new JobTask(1,2,2), // task1
                    new JobTask(2,2,3) // task 2
                ],
                [
                    // job1
                    new JobTask(0,2, 1), // task0
                    new JobTask(2,1,2), // task1
                    new JobTask(1,4,3) // task 2
                ],
                new JobTask[] {
                    // job2
                    new(1,4,1), // task0
                    new(2,3,2), // task1
                }
                    .ToList(),
            }
                .ToList();

int numMachines = 0;
foreach (var job in allJobs)
{
    foreach (var task in job)
    {
        numMachines = Math.Max(numMachines, 1 + task.Machine);
    }
}
int[] allMachines = Enumerable.Range(0, numMachines).ToArray();

// Computes horizon dynamically as the sum of all durations.
int horizon = 0;
foreach (var job in allJobs)
{
    foreach (var task in job)
    {
        horizon += task.Duration;
    }
}

var taskOptano = new Task(() =>
{
    var optanoSolver = new OptanoSolver();
    optanoSolver.Solve(allJobs, horizon, numMachines, allMachines);
});
var taskGoogle = new Task(() =>
{
    var googleSolver = new GoogleSolver();
    googleSolver.Solve(allJobs, horizon, numMachines, allMachines);
});
List<Task> tasks = [taskOptano, taskGoogle];
tasks.ForEach(x => x.Start());
await Task.WhenAll(tasks);



public class AssignedTask : IComparable
{
    public int jobID;
    public int taskID;
    public int start;
    public int duration;

    public AssignedTask(int jobID, int taskID, int start, int duration)
    {
        this.jobID = jobID;
        this.taskID = taskID;
        this.start = start;
        this.duration = duration;
    }

    public int CompareTo(object obj)
    {
        if (obj == null)
            return 1;

        AssignedTask otherTask = obj as AssignedTask;
        if (otherTask != null)
        {
            if (this.start != otherTask.start)
                return this.start.CompareTo(otherTask.start);
            else
                return this.duration.CompareTo(otherTask.duration);
        }
        else
            throw new ArgumentException("Object is not a Temperature");
    }
}