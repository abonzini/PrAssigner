using System.Numerics;
using System.Text;

namespace PrDistribute
{
    internal class Program
    {
        class Reviewer
        {
            public string Name;
            public int Remaining;
            public HashSet<string> Excluded = [];
            public override string ToString()
            {
                return $"{Name} ({Remaining} left)";
            }
            public Reviewer Clone()
            {
                return new Reviewer()
                {
                    Name = Name,
                    Remaining = Remaining,
                    Excluded = [.. Excluded]
                };
            }
        }
        class Team
        {
            public string Name;
            public HashSet<Reviewer> Reviewers;
            public override string ToString()
            {
                return Name;
            }
        }
        /// <summary>
        /// Given reviewers, generates comb of n elements (e.g. A,B,C A,B,D etc)
        /// </summary>
        /// <param name="reviewers">List of reviewers</param>
        /// <param name="n">N</param>
        /// <returns>All possible combinations. Null if invalid</returns>
        static List<HashSet<Reviewer>> CreateReviewerCombinations(List<Reviewer> reviewers, int n)
        {
            List<Reviewer> availableReviewers = [.. reviewers.Where(r => r.Remaining > 0)];
            if (availableReviewers.Count < n) return [];

            return [.. Recurse(availableReviewers, 0, n)];

            static IEnumerable<HashSet<Reviewer>> Recurse(List<Reviewer> revs, int index, int left)
            {
                if (left == 0)
                {
                    yield return new HashSet<Reviewer>();
                    yield break;
                }

                for (int i = index; i <= revs.Count - left; i++)
                {
                    foreach (HashSet<Reviewer> tail in Recurse(revs, i + 1, left - 1))
                    {
                        tail.Add(revs[i]);
                        yield return tail;
                    }
                }
            }
        }
        static void ShuffleList<T>(List<T> list, Random rng)
        {
            int n = list.Count;
            while (n > 1) // Fischer yates
            {
                n--;
                int k = rng.Next(n + 1);
                (list[k], list[n]) = (list[n], list[k]); // Swap
            }
        }
        static void Main(string[] args)
        {
            Console.WriteLine("Seed? (empty if random)");
            int seed;
            string seedInput = Console.ReadLine();
            if (seedInput == "")
            {
                seed = Guid.NewGuid().GetHashCode();
                Console.WriteLine($"Seed is {seed}");
            }
            else
            {
                seed = int.Parse(seedInput);
            }
            Random rng = new Random(seed);

            List<Reviewer> reviewers = new List<Reviewer>();
            List<Team> teams = new List<Team>();
            List<HashSet<Reviewer>> reviewerCombo = null;

            Console.WriteLine("Main folder?");
            string directory = Console.ReadLine();
            Console.WriteLine("How many review per team?");
            int n = int.Parse(Console.ReadLine());
            Console.WriteLine("How many divs?");
            int divs = int.Parse(Console.ReadLine());

            // Obtain teams
            foreach (string team in File.ReadAllLines(Path.Combine(directory, "teams_to_review.txt")))
            {
                teams.Add(new Team()
                {
                    Name = team
                });
            }
            int closestCoprime = 1;
            for (int i = teams.Count / divs; i < teams.Count; i++) // On the search for the closest coprime
            {
                if (BigInteger.GreatestCommonDivisor(teams.Count, i) == 1) // Search for coprime closest to the div marker (but larger)
                {
                    closestCoprime = i;
                    break;
                }
            } // With this there's a better jump to iterate through teams
            // Obtain reviewers
            foreach (string reviewer in File.ReadAllLines(Path.Combine(directory, "reviewers.csv")))
            {
                string[] fields = reviewer.Split(',');
                HashSet<string> exc = [];
                for (int i = 2; i < fields.Length; i++)
                {
                    exc.Add(fields[i]);
                }
                reviewers.Add(new Reviewer()
                {
                    Name = fields[0],
                    Remaining = int.Parse(fields[1]),
                    Excluded = [.. exc]
                });
            }

            // Now, assign reviewers to every team until something works
            bool finished = false;
            int tryCount = 1;
            while (!finished)
            {
                Console.WriteLine($"Try #{tryCount}");
                foreach (Team team in teams) team.Reviewers = null; // Reset all reviews
                List<Reviewer> thisReviewerInstance = [.. reviewers.Select(r => r.Clone())]; // Deep clone so I can remember the initial amount of reviews per
                reviewerCombo = []; // Reset all!

                finished = true; // Works unless error
                for (int teamCounter = 0; teamCounter < teams.Count; teamCounter++)
                {
                    // Iterate jumping through teams
                    int nextTeamIndex = (teamCounter * closestCoprime) % teams.Count;
                    Team team = teams[nextTeamIndex];
                    if (reviewerCombo.Count == 0) // No more reviewers, try again
                    {
                        reviewerCombo = CreateReviewerCombinations(thisReviewerInstance, n);
                        if (reviewerCombo.Count == 0)
                        {
                            // No more options, error!
                            finished = false;
                            break; // Try again...
                        }
                        // If succesful, shuffle them
                        ShuffleList(reviewerCombo, rng);
                    }
                    // Then, assign next valid reviewer
                    HashSet<Reviewer> validReviewerSet = null;
                    for (int i = 0; i < reviewerCombo.Count; i++)
                    {
                        HashSet<Reviewer> revSet = reviewerCombo[i];
                        // Check first possible valid one
                        if (revSet.Any(r => r.Remaining == 0))
                        {
                            // Skip if some reviewer has no more left, also remove this one
                            reviewerCombo.RemoveAt(i);
                            i--; // i doesn't move
                        }
                        else if (revSet.Any(r => r.Name == team.Name))
                        {
                            // Skip if reviewer reviews their own team
                        }
                        else if (revSet.Any(r => r.Excluded.Contains(team.Name)))
                        {
                            // Skip if in reviewer's ignore list
                        }
                        else
                        {
                            // Atp the reviewer set is valid, stop here
                            validReviewerSet = revSet;
                            reviewerCombo.RemoveAt(i); // Remove this one too
                            break;
                        }
                    }
                    if (validReviewerSet == null)
                    {
                        // No valid reviewers, error!
                        finished = false;
                        break; // Try again...
                    }
                    team.Reviewers = validReviewerSet;
                    foreach (Reviewer reviewer in validReviewerSet) reviewer.Remaining--;
                }
                // If reached the end here and valid result, every team has a valid set of reviewers
                tryCount++;
            }
            Console.WriteLine("Reviewers found for each team!");
            Dictionary<string, HashSet<string>> reviewersDuties = new Dictionary<string, HashSet<string>>();
            // Export txt ready for google doc of each player and their reviewers
            StringBuilder exportedDoc = new StringBuilder();
            foreach (Team team in teams)
            {
                exportedDoc.AppendLine(team.Name);
                foreach (Reviewer rev in team.Reviewers)
                {
                    exportedDoc.AppendLine($"\t\t- {rev.Name}");
                    if (!reviewersDuties.ContainsKey(rev.Name))
                    {
                        reviewersDuties[rev.Name] = [];
                    }
                    reviewersDuties[rev.Name].Add(team.Name);
                }
            }
            File.WriteAllText(Path.Combine(directory, "team_with_reviewers.txt"), exportedDoc.ToString());
            // And a aux csv for reviewer checklist
            exportedDoc = new StringBuilder();
            foreach (KeyValuePair<string, HashSet<string>> kvp in reviewersDuties)
            {
                exportedDoc.Append(kvp.Key);
                foreach (string next in kvp.Value)
                {
                    exportedDoc.Append($",{next},FALSE");
                }
                exportedDoc.AppendLine();
            }
            File.WriteAllText(Path.Combine(directory, "reviewer_checklist.txt"), exportedDoc.ToString());
        }
    }
}
