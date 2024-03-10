using Google.OrTools.Sat;

namespace TestORLibraries;

internal class GoogleSolutionPrinter(
    int[] allNurses,
    int[] allDays,
    int[] allShifts,
    Dictionary<(int, int, int), BoolVar> shifts,
    int solutionLimit)
    : CpSolverSolutionCallback
{
    private int[] _allNurses = allNurses;
    private int[] _allDays = allDays;
    private int[] _allShifts = allShifts;
    private Dictionary<(int, int, int), BoolVar> _shifts = shifts;
    private int _solutionLimit = solutionLimit;


    public override void OnSolutionCallback()
    {
        Console.WriteLine($"Solution #{SolutionCount}:");
        foreach (var d in _allDays)
        {
            Console.WriteLine($"Day {d}");
            foreach (var n in _allNurses)
            {
                var isWorking = false;
                foreach (var s in _allShifts)
                {
                    if (Value(_shifts[(n, d, s)]) == 1L)
                    {
                        isWorking = true;
                        Console.WriteLine($" Nurse {n} work shift {s}");
                    }
                }

                if (!isWorking)
                {
                    Console.WriteLine($" Nurse {d} does not work");
                }
            }
        }

        SolutionCount++;
        if (SolutionCount >= _solutionLimit)
        {
            Console.WriteLine($"Stop search after {_solutionLimit} solutions");
            StopSearch();
        }
    }
    public int SolutionCount { get; private set; } = 0;
}