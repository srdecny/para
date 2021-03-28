using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;

namespace dns_netcore
{
	/// <summary>
	/// Representing result of one tested recursive query (and the time it took to process it).
	/// </summary>
	struct TestResult
	{
		public readonly string domain;
		public readonly IP4Addr address;
		public readonly long elapsedMilliseconds;

		public TestResult(string domain, IP4Addr address, long elapsedMilliseconds)
		{
			this.domain = domain;
			this.address = address;
			this.elapsedMilliseconds = elapsedMilliseconds;
		}
	}

	class Program
	{
		/// <summary>
		/// Run some simple recursive queries (as many as we have logical cores) to warm up the thread pool.
		/// This should ensure the pool have sufficient treads warm and ready afterwards.
		/// </summary>
		static void WarmUpThreadPool()
		{
			var tasks = new Task[Environment.ProcessorCount];
			for (int i = 0; i < tasks.Length; ++i) {
				tasks[i] = Task.Delay(50);
			}
			Task.WaitAll(tasks);
		}

		/// <summary>
		/// Start measuring task that executes query and measures its latency.
		/// </summary>
		/// <param name="resolver">Resolver implementation being tested</param>
		/// <param name="domain">Domain to be resolved</param>
		/// <returns>Task which yields TestResult representing this test</returns>
		static Task<TestResult> MeasureQuery(IRecursiveResolver resolver, string domain)
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			var t = resolver.ResolveRecursive(domain);
			return t.ContinueWith<TestResult>(t => {
				stopwatch.Stop();
				return new TestResult(domain, t.Result, stopwatch.ElapsedMilliseconds);
			});
		}

		/// <summary>
		/// Run a batch of queries simultaneously and wait for them all to finish.
		/// </summary>
		/// <param name="resolver">Resolver implementation being tested</param>
		/// <param name="domains">Array of domains to be resolved</param>
		/// <returns>Sum of measured times (in milliseconds)</returns>
		static long RunTestBatch(IRecursiveResolver resolver, string[] domains)
		{
			Console.Write("Starting ... ");
			var tests = domains.Select(domain => MeasureQuery(resolver, domain)).ToArray();
			Console.WriteLine("{0} tests", tests.Length);
			Task.WaitAll(tests);

			long sum = 0;
			foreach (var test in tests) {
				Console.WriteLine("Domain {0} has IP {1} (elapsed time {2} ms) ",
					test.Result.domain, test.Result.address, test.Result.elapsedMilliseconds);
				sum += test.Result.elapsedMilliseconds;
			}
			if (tests.Length > 0) {
				Console.WriteLine("Avg delay {0} ms", sum / tests.Length);
			}
			return sum;
		}

		static void Main(string[] args)
		{
			DNSClient client = InitClient();

            var separator = new string('=', 10);

			IList<IRecursiveResolver> resolvers = new List<IRecursiveResolver>()
            {
				// First serial should perform same as expected.
                new SerialRecursiveResolver(client),

				// Final Solution will be here.
                new RecursiveResolver(client),
            };

			Console.WriteLine($"{separator}Starting Tests{separator} Threads: {ThreadPool.ThreadCount}");

            //RunTest(resolvers, originalArray);
            //RunTest(resolvers, originalArray2);
            //RunTest(resolvers, reverseArray);
            //RunTest(resolvers, inorderLong);
            //RunTest(resolvers, outorderLong);
            //RunTest(resolvers, customShort);
            //RunTest(resolvers, customMedium);
            //RunTest(resolvers, customLong);
            //RunTest(resolvers, customUniqueOnly);
            RunTest(resolvers, customAllAvailable);

            Console.WriteLine($"{separator}Repeated Test are after this:{separator}");



			Console.WriteLine($"{separator}Ending Tests{separator} Threads: {ThreadPool.ThreadCount}");
		}


        private static string[] originalArray = new string[]
        {
            "www.ksi.ms.mff.cuni.cz",
            "ksi.ms.mff.cuni.cz",
            "ms.mff.cuni.cz",
            "mff.cuni.cz",
            "cuni.cz",
            "cz",
        };
        private static string[] originalArray2 = new string[]
        {
            "www.parlab.ms.mff.cuni.cz",
            "www.seznam.cz",
            "www.google.com",
        };
        private static string[] reverseArray = new string[]
        {
            "cz",
            "cuni.cz",
            "mff.cuni.cz",
            "ms.mff.cuni.cz",
            "ksi.ms.mff.cuni.cz",
            "www.ksi.ms.mff.cuni.cz",
        };
        private static string[] outorderLong = new string[]
        {
            "Z",
            "Y.Z",
            "X.Y.Z",
            "W.X.Y.Z",
            "V.W.X.Y.Z",
            "U.V.W.X.Y.Z",
            "T.U.V.W.X.Y.Z",
            "S.T.U.V.W.X.Y.Z",
            "R.S.T.U.V.W.X.Y.Z",
            "Q.R.S.T.U.V.W.X.Y.Z",
            "P.Q.R.S.T.U.V.W.X.Y.Z",
            "O.P.Q.R.S.T.U.V.W.X.Y.Z",
            "N.O.P.Q.R.S.T.U.V.W.X.Y.Z",
            "M.N.O.P.Q.R.S.T.U.V.W.X.Y.Z",
            "L.M.N.O.P.Q.R.S.T.U.V.W.X.Y.Z",
            "K.L.M.N.O.P.Q.R.S.T.U.V.W.X.Y.Z",
            "J.K.L.M.N.O.P.Q.R.S.T.U.V.W.X.Y.Z",
            "I.J.K.L.M.N.O.P.Q.R.S.T.U.V.W.X.Y.Z",
            "H.I.J.K.L.M.N.O.P.Q.R.S.T.U.V.W.X.Y.Z",
            "G.H.I.J.K.L.M.N.O.P.Q.R.S.T.U.V.W.X.Y.Z",
            "F.G.H.I.J.K.L.M.N.O.P.Q.R.S.T.U.V.W.X.Y.Z",
            "E.F.G.H.I.J.K.L.M.N.O.P.Q.R.S.T.U.V.W.X.Y.Z",
            "D.E.F.G.H.I.J.K.L.M.N.O.P.Q.R.S.T.U.V.W.X.Y.Z",
            "C.D.E.F.G.H.I.J.K.L.M.N.O.P.Q.R.S.T.U.V.W.X.Y.Z",
            "B.C.D.E.F.G.H.I.J.K.L.M.N.O.P.Q.R.S.T.U.V.W.X.Y.Z",
            "A.B.C.D.E.F.G.H.I.J.K.L.M.N.O.P.Q.R.S.T.U.V.W.X.Y.Z",
        };
        private static string[] inorderLong = new string[]
        {
            "A.B.C.D.E.F.G.H.I.J.K.L.M.N.O.P.Q.R.S.T.U.V.W.X.Y.Z",
            "B.C.D.E.F.G.H.I.J.K.L.M.N.O.P.Q.R.S.T.U.V.W.X.Y.Z",
            "C.D.E.F.G.H.I.J.K.L.M.N.O.P.Q.R.S.T.U.V.W.X.Y.Z",
            "D.E.F.G.H.I.J.K.L.M.N.O.P.Q.R.S.T.U.V.W.X.Y.Z",
            "E.F.G.H.I.J.K.L.M.N.O.P.Q.R.S.T.U.V.W.X.Y.Z",
            "F.G.H.I.J.K.L.M.N.O.P.Q.R.S.T.U.V.W.X.Y.Z",
            "G.H.I.J.K.L.M.N.O.P.Q.R.S.T.U.V.W.X.Y.Z",
            "H.I.J.K.L.M.N.O.P.Q.R.S.T.U.V.W.X.Y.Z",
            "I.J.K.L.M.N.O.P.Q.R.S.T.U.V.W.X.Y.Z",
            "J.K.L.M.N.O.P.Q.R.S.T.U.V.W.X.Y.Z",
            "K.L.M.N.O.P.Q.R.S.T.U.V.W.X.Y.Z",
            "L.M.N.O.P.Q.R.S.T.U.V.W.X.Y.Z",
            "M.N.O.P.Q.R.S.T.U.V.W.X.Y.Z",
            "N.O.P.Q.R.S.T.U.V.W.X.Y.Z",
            "O.P.Q.R.S.T.U.V.W.X.Y.Z",
            "P.Q.R.S.T.U.V.W.X.Y.Z",
            "Q.R.S.T.U.V.W.X.Y.Z",
            "R.S.T.U.V.W.X.Y.Z",
            "S.T.U.V.W.X.Y.Z",
            "T.U.V.W.X.Y.Z",
            "U.V.W.X.Y.Z",
            "V.W.X.Y.Z",
            "W.X.Y.Z",
            "X.Y.Z",
            "Y.Z",
            "Z",
        };
        private static string[] customShort = new string[]
        {
            "ShortestPossibleAddress"
        };
        private static string[] customMedium = new string[]
        {
            "intranet.android.runner.for.ever.eu",
        };
        private static string[] customLong = new string[]
        {
            "A.B.C.D.E.F.G.H.I.J.K.L.M.N.O.P.Q.R.S.T.U.V.W.X.Y.Z"
        };
        private static string[] customUniqueOnly = new string[]
        {
            "A.B.C.D.E.F.G.H.I.J.K.L.M.N.O.P.Q.R.S.T.U.V.W.X.Y.Z",
            "intranet.android.runner.for.ever.eu",
            "www.parlab.ms.mff.cuni.cz",
            "www.google.com",
        };
        private static string[] customAllAvailable = new string[]
        {
                "www.parlab.ms.mff.cuni.cz",
                "www.ksi.ms.mff.cuni.cz",
                "www.seznam.cz",
                "parlab.ms.mff.cuni.cz",
                "ksi.ms.mff.cuni.cz",
                "seznam.cz",
                "ms.mff.cuni.cz",
                "mff.cuni.cz",
                "cuni.cz",
                "cz",
                "www.google.com",
                "google.com",
                "com",
                "A.B.C.D.E.F.G.H.I.J.K.L.M.N.O.P.Q.R.S.T.U.V.W.X.Y.Z",
                "B.C.D.E.F.G.H.I.J.K.L.M.N.O.P.Q.R.S.T.U.V.W.X.Y.Z",
                "C.D.E.F.G.H.I.J.K.L.M.N.O.P.Q.R.S.T.U.V.W.X.Y.Z",
                "D.E.F.G.H.I.J.K.L.M.N.O.P.Q.R.S.T.U.V.W.X.Y.Z",
                "E.F.G.H.I.J.K.L.M.N.O.P.Q.R.S.T.U.V.W.X.Y.Z",
                "F.G.H.I.J.K.L.M.N.O.P.Q.R.S.T.U.V.W.X.Y.Z",
                "G.H.I.J.K.L.M.N.O.P.Q.R.S.T.U.V.W.X.Y.Z",
                "H.I.J.K.L.M.N.O.P.Q.R.S.T.U.V.W.X.Y.Z",
                "I.J.K.L.M.N.O.P.Q.R.S.T.U.V.W.X.Y.Z",
                "J.K.L.M.N.O.P.Q.R.S.T.U.V.W.X.Y.Z",
                "K.L.M.N.O.P.Q.R.S.T.U.V.W.X.Y.Z",
                "L.M.N.O.P.Q.R.S.T.U.V.W.X.Y.Z",
                "M.N.O.P.Q.R.S.T.U.V.W.X.Y.Z",
                "N.O.P.Q.R.S.T.U.V.W.X.Y.Z",
                "O.P.Q.R.S.T.U.V.W.X.Y.Z",
                "P.Q.R.S.T.U.V.W.X.Y.Z",
                "Q.R.S.T.U.V.W.X.Y.Z",
                "R.S.T.U.V.W.X.Y.Z",
                "S.T.U.V.W.X.Y.Z",
                "T.U.V.W.X.Y.Z",
                "U.V.W.X.Y.Z",
                "V.W.X.Y.Z",
                "W.X.Y.Z",
                "X.Y.Z",
                "Y.Z",
                "Z",
                "intranet.android.runner.for.ever.eu",
                "android.runner.for.ever.eu",
                "runner.for.ever.eu",
                "for.ever.eu",
                "ever.eu",
                "eu",
                "ShortestPossibleAddress",
                "a.b.c.x.y.z",
                "b.c.x.y.z",
                "c.x.y.z",
                "x.y.z",
                "y.z",
                "z",
        };

        private static void RunTest(IEnumerable<IRecursiveResolver> resolvers, string[] array)
        {
            foreach (var resolver in resolvers)
            {
                Console.WriteLine($"\n{resolver.GetType().FullName}");
                RunTestBatch(resolver, array.Select(a => (string) a.Clone()).ToArray());
            }
        }

		private static DNSClient InitClient()
        {
            var client = new DNSClient();
            client.InitData(new string[]
            {
                "www.parlab.ms.mff.cuni.cz",
                "www.ksi.ms.mff.cuni.cz",
                "www.seznam.cz",
                "www.google.com",
                "A.B.C.D.E.F.G.H.I.J.K.L.M.N.O.P.Q.R.S.T.U.V.W.X.Y.Z",
                "intranet.android.runner.for.ever.eu",
                "ShortestPossibleAddress",
                "a.b.c.x.y.z",
            });
            Console.WriteLine("Total {0} logical processors detected.", Environment.ProcessorCount);
            Console.WriteLine("Warming up thread pool...");
            WarmUpThreadPool();
            Console.WriteLine("{0}", ThreadPool.ThreadCount);
            return client;
        }
	}
}