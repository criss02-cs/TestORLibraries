using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestORLibraries.Entity;

namespace TestORLibraries;
public interface ISolver
{
    void Solve(List<List<JobTask>> allJobs, int horizon, int numMachines, int[] allMachines);
}
